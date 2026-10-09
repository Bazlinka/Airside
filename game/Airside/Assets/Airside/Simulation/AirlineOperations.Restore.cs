using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    public sealed partial class AirlineOperations
    {
        /// <summary>
        /// Re-add an aircraft exactly as saved. Unlike <see cref="AddAircraft"/> this never
        /// schedules an AI departure, so the random sequence resumes where it left off.
        /// </summary>
        internal void RestoreAircraft(
            string registration, Airline airline, AircraftType type, FleetState state,
            SimulationTime stateStartedAt, SimulationTime? stateEndsAt, StableId stand, StableId departureStand,
            Destination? currentDestination, ScheduledDeparture? scheduled, int completedTrips)
        {
            if (!_airlines.Contains(airline))
                throw new FormatException($"{registration}: airline not restored.");
            if (string.IsNullOrWhiteSpace(registration)
                || _fleet.Exists(a => string.Equals(a.Registration, registration, StringComparison.OrdinalIgnoreCase)))
                throw new FormatException($"Duplicate or missing registration '{registration}'.");

            var aircraft = new FleetAircraft(registration, airline, type, default, stateStartedAt);
            aircraft.Owner = this;
            aircraft.Restore(state, stateStartedAt, stateEndsAt);
            if (RequiresTripDestination(state) && currentDestination == null)
                throw new FormatException($"{registration} is {state} with no destination.");
            if (RequiresDepartureStand(state) && string.IsNullOrEmpty(departureStand.Value))
                throw new FormatException($"{registration} is {state} with no departure stand.");
            // AtStand/TaxiIn always require a stand. Newer saves may also contain an AI
            // arrival reservation while holding or landing; older saves legitimately do not.
            var requiresStand = state is FleetState.AtStand or FleetState.TaxiIn
                                || state == FleetState.Maintenance && !string.IsNullOrEmpty(stand.Value);
            var hasReservation = !string.IsNullOrEmpty(stand.Value)
                                 && state is FleetState.HoldingForLanding or FleetState.Landing
                                     or FleetState.GoAround or FleetState.AwaitingStand;
            if ((requiresStand || hasReservation) && (!_stands.Contains(stand) || !IsStandFree(stand)))
                throw new FormatException($"{registration} is on stand '{stand}', which is missing, unknown or taken.");
            if ((requiresStand || hasReservation) && !StandClassFits(type, stand))
                throw new FormatException($"{registration} ({type.Name}) cannot be on stand '{stand}'.");

            aircraft.Stand = stand;
            aircraft.DepartureStand = departureStand;
            aircraft.CurrentDestination = currentDestination;
            aircraft.Scheduled = scheduled;
            aircraft.CompletedTrips = Math.Max(0, completedTrips);
            _fleet.Add(aircraft);
        }

        internal void RestoreMovementData(string registration, RunwayDirection runway, bool wentAroundThisTrip, bool arrivalCommittedBeforeStorm = false)
        {
            var aircraft = _fleet.Find(a => string.Equals(a.Registration, registration, StringComparison.OrdinalIgnoreCase));
            if (aircraft == null)
                throw new FormatException($"{registration}: movement data has no aircraft.");
            aircraft.AssignedRunway = runway;
            aircraft.WentAroundThisTrip = wentAroundThisTrip;
            aircraft.ArrivalCommittedBeforeStorm = arrivalCommittedBeforeStorm;
        }

        internal void RestorePrepData(string registration, SimulationTime? prepStartedAt)
        {
            var aircraft = _fleet.Find(a => string.Equals(a.Registration, registration, StringComparison.OrdinalIgnoreCase));
            if (aircraft == null)
                throw new FormatException($"{registration}: prep data has no aircraft.");
            aircraft.PrepStartedAt = prepStartedAt;
        }

        internal void RestorePushbackLateness(string registration, int latenessSeconds, string delay = null)
        {
            var aircraft = _fleet.Find(a => string.Equals(a.Registration, registration, StringComparison.OrdinalIgnoreCase));
            if (aircraft == null)
                throw new FormatException($"{registration}: pushback lateness has no aircraft.");
            aircraft.PushbackLatenessSeconds = latenessSeconds;
            // v16 (ADR 0128): the saved breakdown; older saves have none, so it is all "Other".
            aircraft.PushbackDelay = DelayBreakdown.Parse(latenessSeconds, delay);
        }

        internal void RestoreTower(SimulationTime mainRunwayFreeAt, SimulationTime crossRunwayFreeAt, long totalEvents)
        {
            _mainRunwayFreeAt = mainRunwayFreeAt;
            _crossRunwayFreeAt = crossRunwayFreeAt;
            TotalEvents = Math.Max(0, totalEvents);
        }

        /// <summary>
        /// After restore, both strips must stay busy until any in-progress landing or
        /// takeoff ends — including dual-strip saves whose free times drifted past now.
        /// </summary>
        internal void ReconcileRunwayFreeAt()
        {
            ReconcileStripFreeAt(mainStrip: true);
            ReconcileStripFreeAt(mainStrip: false);
        }

        private void ReconcileStripFreeAt(bool mainStrip)
        {
            SimulationTime? holdUntil = null;
            foreach (var aircraft in _fleet)
            {
                if (aircraft.State is not (FleetState.TakingOff or FleetState.Landing) || aircraft.Type.IsRotorcraft)
                    continue;
                if (RunwayWeather.IsMainRunway(aircraft.AssignedRunway) != mainStrip)
                    continue;
                var until = StripBusyUntil(aircraft) ?? _processedTo;
                until = until.Advance(RunwaySeparationSeconds);
                if (!holdUntil.HasValue || until.CompareTo(holdUntil.Value) > 0)
                    holdUntil = until;
            }

            if (!holdUntil.HasValue)
                return;
            if (mainStrip)
            {
                if (_mainRunwayFreeAt.CompareTo(holdUntil.Value) < 0)
                    _mainRunwayFreeAt = holdUntil.Value;
            }
            else if (_crossRunwayFreeAt.CompareTo(holdUntil.Value) < 0)
            {
                _crossRunwayFreeAt = holdUntil.Value;
            }
        }

        /// <summary>Rebuilds career state (ADR 0053 / 0056) from a v6+ save, or a fresh
        /// Provisional state when migrating an older one — never from anything it doesn't recognise.</summary>
        internal void RestoreCareerState(
            long funds, int reliability, string tier, string activeContractId,
            long contractAcceptedAtSeconds, int contractCompletedRotations, IEnumerable<string> processedSettlementKeys,
            IEnumerable<string> completedContractIds = null, int completedPlayerRotations = 0,
            RouteContractDefinition activeSnapshot = null, long lifetimeRevenue = 0,
            IEnumerable<CompletedContractRecord> contractHistory = null,
            PlayerBaseLevel? baseLevel = null, string pinnedGoalId = null,
            IEnumerable<string> servedDestinations = null, IEnumerable<string> outstationBases = null,
            IEnumerable<long> recentServiceMargins = null, int manualRotations = 0,
            long activePlaySeconds = 0, long regionalAtSeconds = 0, long domesticAtSeconds = 0,
            long internationalAtSeconds = 0, long finaleAtSeconds = 0,
            CareerDifficulty difficulty = CareerDifficulty.Standard)
        {
            if (string.IsNullOrWhiteSpace(tier)
                || !Enum.TryParse(tier, out OperatingTier parsedTier)
                || !Enum.IsDefined(typeof(OperatingTier), parsedTier)
                || !string.Equals(parsedTier.ToString(), tier.Trim(), StringComparison.Ordinal))
                throw new FormatException($"Unknown career tier '{tier}'.");

            IEnumerable<RouteContractDefinition> issued = activeSnapshot == null
                ? null
                : new[] { activeSnapshot };

            ActiveRouteContract contract = null;
            if (!string.IsNullOrEmpty(activeContractId))
            {
                var known = RouteContractCatalogue.TryFind(activeContractId, out _)
                            || (activeSnapshot != null
                                && string.Equals(activeSnapshot.Id, activeContractId, StringComparison.Ordinal));
                if (!known)
                    throw new FormatException($"Unknown contract '{activeContractId}'.");
                contract = new ActiveRouteContract(
                    activeContractId, new SimulationTime(contractAcceptedAtSeconds), contractCompletedRotations);
            }

            var effectiveBase = baseLevel ?? PlayerBase.MinimumFor(PlayerOwnedTypes(), PlayerFleetCount());
            _announcedTier = null;
            _announcedGoals.Clear();
            _careerEvents.Clear();
            CareerState = new AirlineCareerState(funds, reliability, parsedTier, contract, processedSettlementKeys,
                completedContractIds, completedPlayerRotations, issued, lifetimeRevenue, contractHistory, effectiveBase,
                pinnedGoalId, servedDestinations, outstationBases, recentServiceMargins, manualRotations,
                activePlaySeconds, regionalAtSeconds, domesticAtSeconds, internationalAtSeconds, finaleAtSeconds,
                difficulty);
        }

        internal void RestoreAutomatedTrip(string registration, bool automated)
        {
            var aircraft = _fleet.Find(a => a.Registration == registration && a.Airline.IsPlayer);
            if (aircraft != null) aircraft.AutomatedTrip = automated;
        }

        internal void RestoreNetworkState(IEnumerable<OutstationAircraft> aircraft,
            IEnumerable<RepeatSchedule> schedules)
        {
            _outstationFleet.Clear();
            _repeatSchedules.Clear();
            if (aircraft != null) _outstationFleet.AddRange(aircraft);
            if (schedules != null) _repeatSchedules.AddRange(schedules);
            _repeatSchedulesLive = false;
        }

        internal void RestoreToday(DaySnapshot day)
        {
            _today.Reset(day.StartReliability ?? CareerState?.Reliability ?? 0);
            _today.StartReliability = day.StartReliability;
            _today.Flights = Math.Max(0, day.Flights);
            _today.Revenue = day.Revenue;
            _today.Cost = day.Cost;
            _today.BestCode = day.BestCode ?? string.Empty;
            _today.BestMargin = day.BestMargin;
            _today.LateFlights = Math.Max(0, day.LateFlights);
            _today.LateSeconds = Math.Max(0, day.LateSeconds);
            _reportedDay = day.ReportedDay;
        }

        internal void RestoreMaintenance(string registration, int rotationsSinceCheck, long checkUntilSeconds)
        {
            var aircraft = _fleet.Find(a => string.Equals(a.Registration, registration, StringComparison.OrdinalIgnoreCase));
            if (aircraft == null)
                return;
            aircraft.RotationsSinceCheck = Math.Max(0, rotationsSinceCheck);
            aircraft.CheckUntil = checkUntilSeconds > 0 ? new SimulationTime(checkUntilSeconds) : null;
        }

        internal void RestoreFerry(string registration)
        {
            var aircraft = _fleet.Find(a => string.Equals(a.Registration, registration, StringComparison.OrdinalIgnoreCase));
            if (aircraft == null)
                throw new FormatException($"{registration}: ferry flag has no aircraft.");
            aircraft.IsFerry = true;
        }

        internal void RestoreFreighter(string registration)
        {
            var aircraft = _fleet.Find(a => string.Equals(a.Registration, registration, StringComparison.OrdinalIgnoreCase));
            if (aircraft == null)
                throw new FormatException($"{registration}: freighter role has no aircraft.");
            aircraft.IsFreighter = true;
        }

        internal void RestoreAircraftHistory(string registration, SimulationTime joinedAt, bool founding,
            long lifetimeRevenue, int historyFlights, IEnumerable<AircraftRouteTally> routes)
        {
            var aircraft = _fleet.Find(a => string.Equals(a.Registration, registration, StringComparison.OrdinalIgnoreCase));
            if (aircraft == null)
                throw new FormatException($"{registration}: history has no aircraft.");
            aircraft.RestoreHistory(joinedAt, founding, lifetimeRevenue, historyFlights, routes);
        }
    }
}
