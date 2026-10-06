using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    public sealed partial class AirlineOperations
    {
        /// <summary>Call after away catch-up; saved repeat plans never generate offline flights.</summary>
        public void ResumeRepeatSchedules() => _repeatSchedulesLive = true;

        /// <summary>
        /// Every purchase gate but money is met now: tier, reliability, flights, base support and fleet
        /// room (ADR 0138). What the featured contracts use to decide an aircraft is within reach.
        /// </summary>
        public bool CouldBuyNow(AircraftType type)
        {
            if (CareerState == null || type == null || !AircraftAcquisition.TryFor(type, out var offer))
                return false;
            return CareerState.Tier >= offer.RequiredTier
                   && CareerState.Reliability >= offer.RequiredReliability
                   && CareerState.CompletedPlayerRotations >= offer.RequiredRotations
                   && PlayerBase.Supports(CareerState.BaseLevel, type)
                   && PlayerFleetCount() < AircraftAcquisition.MaxPlayerAircraft;
        }

        /// <summary>Buy one more aircraft of an authored type when funds, tier, reliability and rotations clear (ADR 0056).</summary>
        public CommandResult BuyAircraft(AircraftType type)
        {
            if (PlayerAirline == null)
                return CommandResult.Refused("No player airline.");
            if (type == null || !AircraftAcquisition.TryFor(type, out var offer))
                return CommandResult.Refused("That type is not for sale.");

            var owned = 0;
            foreach (var aircraft in _fleet)
                if (aircraft.Airline.IsPlayer)
                    owned++;
            if (PlayerFleetCount() >= AircraftAcquisition.MaxPlayerAircraft)
                return CommandResult.Refused($"Your fleet is full at {AircraftAcquisition.MaxPlayerAircraft} aircraft.");
            if (owned >= CareerState.Base.FleetCapacity)
                return CommandResult.Refused($"The {CareerState.Base.Title} holds {CareerState.Base.FleetCapacity} aircraft. Expand your Adelaide base first.");
            if (!PlayerBase.Supports(CareerState.BaseLevel, type))
            {
                var needed = PlayerBase.For(PlayerBase.RequiredLevel(type));
                return CommandResult.Refused($"{Article.CapitalA(type.Name)} needs the {needed.Title}. Expand your Adelaide base first.");
            }
            if (CareerState.Tier < offer.RequiredTier)
                return CommandResult.Refused($"{Article.CapitalA(type.Name)} needs the {offer.RequiredTier} tier.");
            if (CareerState.Reliability < offer.RequiredReliability)
                return CommandResult.Refused($"{Article.CapitalA(type.Name)} needs {offer.RequiredReliability}% reliability.");
            if (CareerState.CompletedPlayerRotations < offer.RequiredRotations)
                return CommandResult.Refused(
                    $"{Article.CapitalA(type.Name)} needs {offer.RequiredRotations} completed flights.");
            if (!CareerState.CanAfford(offer.Price))
                return CommandResult.Refused($"{Article.CapitalA(type.Name)} costs ${offer.Price:N0}. You have ${CareerState.Funds:N0}.");

            var stand = SuggestPurchaseStand(type);
            if (!CareerState.TryChargePurchase(offer.Price))
                return CommandResult.Refused($"{Article.CapitalA(type.Name)} costs ${offer.Price:N0}. You have ${CareerState.Funds:N0}.");

            var registration = NextPlayerRegistration(_fleet, CareerState.HasSettlementHistory);
            if (stand.HasValue)
                AddAircraft(PlayerAirline, registration, type, stand.Value);
            else
                AddDeliveryInbound(PlayerAirline, registration, type);

            AdvanceCareer();
            return CommandResult.Ok;
        }

        /// <summary>"Needs 88% reliability, 12 more flights." for the next outstation, or empty when met.</summary>
        public string NextOutstationRequirement()
        {
            if (NextOutstationGate is not { } gate)
                return string.Empty;
            var parts = new List<string>();
            if (CareerState.Tier < OperatingTier.Domestic)
                parts.Add("Domestic tier");
            if (CareerState.Reliability < gate.Reliability)
                parts.Add($"{gate.Reliability}% reliability");
            if (CareerState.CompletedPlayerRotations < gate.Flights)
            {
                var more = gate.Flights - CareerState.CompletedPlayerRotations;
                parts.Add($"{more} more flight{(more == 1 ? "" : "s")}");
            }

            return parts.Count == 0 ? string.Empty : "Needs " + string.Join(", ", parts) + ".";
        }

        public CommandResult OpenOutstationBase(string code)
        {
            if (CareerState.Tier < OperatingTier.Domestic)
                return CommandResult.Refused("You need the Domestic tier to open another base.");
            var allowed = false;
            foreach (var candidate in OutstationCandidates)
                if (candidate == code) allowed = true;
            if (!allowed) return CommandResult.Refused("Outstations can open in Melbourne, Sydney, Brisbane or Perth.");
            if (CareerState.HasOutstationBase(code)) return CommandResult.Refused("That base is already open.");
            if (CareerState.OutstationBases.Count >= 3) return CommandResult.Refused("You already have three outstations.");
            if (NextOutstationGate is { } gate)
            {
                if (CareerState.Reliability < gate.Reliability)
                    return CommandResult.Refused($"Your next outstation needs {gate.Reliability}% reliability. You have {CareerState.Reliability}%.");
                if (CareerState.CompletedPlayerRotations < gate.Flights)
                    return CommandResult.Refused($"Your next outstation needs {gate.Flights} flights. You have flown {CareerState.CompletedPlayerRotations}.");
            }
            var cost = NextOutstationCost;
            if (!CareerState.TryChargePurchase(cost))
                return CommandResult.Refused($"Opening this base costs ${cost:N0}. You have ${CareerState.Funds:N0}.");
            CareerState.AddOutstationBase(code);
            AdvanceCareer();
            return CommandResult.Ok;
        }

        public CommandResult BuyAircraftAtOutstation(AircraftType type, string baseCode)
        {
            if (!CareerState.HasOutstationBase(baseCode))
                return CommandResult.Refused("Open that base first.");
            if (type == null || !AircraftAcquisition.TryFor(type, out var offer))
                return CommandResult.Refused("That aircraft is not for sale.");
            if (type.IsRotorcraft)
                return CommandResult.Refused("A helicopter flies from the Adelaide helipad; it cannot be based at an outstation.");
            if (PlayerFleetCount() >= AircraftAcquisition.MaxPlayerAircraft)
                return CommandResult.Refused("Your fleet is full.");
            var based = 0;
            foreach (var aircraft in _outstationFleet)
                if (aircraft.BaseCode == baseCode) based++;
            if (based >= OutstationCapacity)
                return CommandResult.Refused($"{baseCode} holds {OutstationCapacity} aircraft.");
            if (!PlayerBase.Supports(CareerState.BaseLevel, type) || CareerState.Tier < offer.RequiredTier
                || CareerState.Reliability < offer.RequiredReliability
                || CareerState.CompletedPlayerRotations < offer.RequiredRotations)
                return CommandResult.Refused("You don't meet this aircraft's requirements yet. See Fleet for what it needs.");
            if (!HasOutstationRoute(type, baseCode))
                return CommandResult.Refused(
                    $"{Article.CapitalA(type.Name)} at {baseCode} would have nowhere in range to fly. Choose a longer-range type.");
            if (!CareerState.TryChargePurchase(offer.Price))
                return CommandResult.Refused($"{Article.CapitalA(type.Name)} costs ${offer.Price:N0}. You have ${CareerState.Funds:N0}.");
            var number = 1;
            while (true)
            {
                var registration = $"VH-O{number:00}";
                var used = false;
                foreach (var existing in _fleet)
                    if (existing.Registration == registration) used = true;
                foreach (var existing in _outstationFleet)
                    if (existing.Registration == registration) used = true;
                if (!used)
                {
                    _outstationFleet.Add(new OutstationAircraft(registration, type, baseCode));
                    break;
                }
                number++;
            }
            AdvanceCareer();
            return CommandResult.Ok;
        }

        /// <summary>
        /// True when a type based at <paramref name="baseCode"/> has at least one non-Adelaide
        /// destination within range and its route band — an ATR at Perth has none, and an aircraft
        /// that can never fly would only pad the fleet goal while costing money.
        /// </summary>
        public bool HasOutstationRoute(AircraftType type, string baseCode)
        {
            if (type == null || !DestinationCatalogue.TryFind(baseCode, out var origin)) return false;
            foreach (var destination in DestinationCatalogue.All)
            {
                if (destination.Code == baseCode || destination.Code == Home.Code) continue;
                if (type.CanReach(origin.DistanceKmTo(destination)) && RouteAccess.Allows(type, destination))
                    return true;
            }
            return false;
        }

        public CommandResult ScheduleOutstationService(string registration, string destinationCode, SimulationTime departAt) =>
            ScheduleOutstationService(registration, destinationCode, departAt, automated: false);

        private CommandResult ScheduleOutstationService(string registration, string destinationCode,
            SimulationTime departAt, bool automated)
        {
            OutstationAircraft aircraft = null;
            foreach (var candidate in _outstationFleet)
                if (candidate.Registration == registration) aircraft = candidate;
            if (aircraft == null) return CommandResult.Refused("Unknown outstation aircraft.");
            if (aircraft.HasFlight) return CommandResult.Refused("That aircraft already has a flight.");
            if (aircraft.InCheck(_processedTo.ElapsedSeconds))
                return CommandResult.Refused("That aircraft is in its check.");
            if (aircraft.CheckDue)
                return CommandResult.Refused("A check is due before its next flight.");
            if (!DestinationCatalogue.TryFind(aircraft.BaseCode, out var origin)
                || !DestinationCatalogue.TryFind(destinationCode, out var destination))
                return CommandResult.Refused("Unknown network destination.");
            if (destinationCode == aircraft.BaseCode) return CommandResult.Refused("Choose another destination.");
            // Any Adelaide movement must use the rendered airport's real runway and stand
            // reservations. Network aircraft therefore work only routes outside Adelaide.
            if (destinationCode == Home.Code)
                return CommandResult.Refused("Flights from Adelaide need an aircraft based at Adelaide.");
            var km = origin.DistanceKmTo(destination);
            if (!aircraft.Type.CanReach(km) || !RouteAccess.Allows(aircraft.Type, destination))
                return CommandResult.Refused("That aircraft cannot operate this route.");
            if (CareerState.Tier < RouteAccess.RequiredTier(RouteAccess.BandOf(destination)))
                return CommandResult.Refused("International flights need the International tier.");
            if (departAt.CompareTo(_processedTo) < 0)
                return CommandResult.Refused("Departure time is in the past.");
            var cost = DispatchCost(aircraft.Type, km);
            if (!CareerState.TryChargeDispatch(cost))
                return CommandResult.Refused($"This flight costs ${cost:N0}. You have ${CareerState.Funds:N0}.");
            var duration = 2 * LegTiming.AirborneSeconds(km, aircraft.Type) + 45 * 60;
            aircraft.Plan(destinationCode, departAt.ElapsedSeconds, departAt.ElapsedSeconds + duration, automated);
            return CommandResult.Ok;
        }

        public CommandResult StartOutstationCheck(string registration)
        {
            var aircraft = _outstationFleet.Find(a => a.Registration == registration);
            if (aircraft == null) return CommandResult.Refused("Unknown outstation aircraft.");
            if (aircraft.HasFlight) return CommandResult.Refused("Let it finish its flight first.");
            if (aircraft.InCheck(_processedTo.ElapsedSeconds))
                return CommandResult.Refused("It is already in its check.");
            if (!aircraft.CheckDue) return CommandResult.Refused("It isn't due for a check yet.");
            // An outstation is equipped for the types it is allowed to base, so its checks are
            // priced and timed like a local check at the matching Adelaide capability.
            var outstationCapability = AircraftCatalogue.IsWidebody(aircraft.Type) ? PlayerBaseLevel.International
                : NeedsTerminalGate(aircraft.Type) ? PlayerBaseLevel.JetGate : PlayerBaseLevel.ExpandedRegional;
            var cost = Maintenance.CheckCost(aircraft.Type, outstationCapability);
            if (!CareerState.TryChargePurchase(cost))
                return CommandResult.Refused($"A check costs ${cost:N0}. You have ${CareerState.Funds:N0}.");
            aircraft.StartCheck(_processedTo.ElapsedSeconds
                + Maintenance.CheckSeconds(aircraft.Type, outstationCapability));
            return CommandResult.Ok;
        }

        public CommandResult SetRepeatSchedule(string registration, string destinationCode, int intervalHours)
        {
            if (!DelegationUnlocked)
                return CommandResult.Refused("Plan 12 flights yourself to unlock repeat schedules.");
            if (intervalHours != 6 && intervalHours != 12 && intervalHours != 24)
                return CommandResult.Refused("Repeat every 6, 12 or 24 hours.");
            var local = _fleet.Find(a => a.Registration == registration && a.Airline.IsPlayer);
            var remote = _outstationFleet.Find(a => a.Registration == registration);
            if (local == null && remote == null) return CommandResult.Refused("Unknown player aircraft.");
            if (!DestinationCatalogue.TryFind(destinationCode, out var destination))
                return CommandResult.Refused("Unknown destination.");
            if (local != null && !CanOperate(local, destination))
                return CommandResult.Refused("That aircraft can't fly this route.");
            if (remote != null)
            {
                if (destinationCode == Home.Code || destinationCode == remote.BaseCode
                    || !DestinationCatalogue.TryFind(remote.BaseCode, out var origin)
                    || !remote.Type.CanReach(origin.DistanceKmTo(destination))
                    || !RouteAccess.Allows(remote.Type, destination)
                    || CareerState.Tier < RouteAccess.RequiredTier(RouteAccess.BandOf(destination)))
                    return CommandResult.Refused("That aircraft can't fly this route from its base.");
            }
            _repeatSchedules.RemoveAll(p => p.Registration == registration);
            _repeatSchedules.Add(new RepeatSchedule(registration, destinationCode, intervalHours,
                _processedTo.ElapsedSeconds));
            return CommandResult.Ok;
        }

        public CommandResult PauseRepeatSchedule(string registration, bool paused)
        {
            foreach (var plan in _repeatSchedules)
            {
                if (plan.Registration != registration) continue;
                plan.Paused = paused;
                if (!paused) plan.Exception = string.Empty;
                return CommandResult.Ok;
            }
            return CommandResult.Refused("No repeat schedule for that aircraft.");
        }

        public CommandResult RemoveRepeatSchedule(string registration)
        {
            return _repeatSchedules.RemoveAll(p => p.Registration == registration) > 0
                ? CommandResult.Ok : CommandResult.Refused("No repeat schedule for that aircraft.");
        }

        private void ProcessOutstationServices(SimulationTime target)
        {
            foreach (var aircraft in _outstationFleet)
            {
                if (!aircraft.HasFlight || aircraft.ReturnAtSeconds > target.ElapsedSeconds) continue;
                var destinationCode = aircraft.DestinationCode;
                var wasAutomated = aircraft.Automated;
                DestinationCatalogue.TryFind(aircraft.BaseCode, out var origin);
                DestinationCatalogue.TryFind(destinationCode, out var destination);
                var returnedAt = new SimulationTime(aircraft.ReturnAtSeconds);
                var km = origin.DistanceKmTo(destination);
                var forecast = Forecast(origin, destination, aircraft.Type);
                aircraft.Complete();
                RouteContractDefinition matching = null;
                var active = CareerState.ActiveContract;
                if (active != null && CareerState.TryFindDefinition(active.DefinitionId, out var definition)
                    && definition.MatchesAircraft(aircraft.Type, isFreighter: false)
                    && definition.MatchesRoute(aircraft.BaseCode, destinationCode))
                    matching = definition;
                var settlement = CareerState.RecordCompletedRotation(
                    new SettlementId(aircraft.Registration, aircraft.CompletedServices),
                    forecast.Revenue, matching,
                    PlayerOwnedTypes(), returnedAt, PlayerFleetCount());
                if (settlement == null) continue;
                var completionBonus = settlement.Value.ContractFulfilled ? matching.CompletionReward : 0;
                CareerState.RecordService(destinationCode,
                    settlement.Value.Payment - completionBonus - forecast.Cost, !wasAutomated);
                AdvanceCareer();
                _recentSettlements.Add(settlement.Value);
                TotalSettlements++;
                if (_recentSettlements.Count > MaxRecentEvents)
                    _recentSettlements.RemoveAt(0);
            }
        }

        private void ProcessRepeatSchedules(SimulationTime target)
        {
            foreach (var plan in _repeatSchedules)
            {
                if (plan.Paused || plan.NextEligibleAtSeconds > target.ElapsedSeconds) continue;
                var local = _fleet.Find(a => a.Registration == plan.Registration && a.Airline.IsPlayer);
                var remote = _outstationFleet.Find(a => a.Registration == plan.Registration);
                if (local != null && (local.State != FleetState.AtStand || local.Scheduled.HasValue
                    || Maintenance.InCheck(local, target))) continue;
                if (remote != null && remote.HasFlight) continue;
                if (remote != null && remote.InCheck(target.ElapsedSeconds)) continue;
                if (remote != null && remote.CheckDue)
                {
                    plan.Paused = true;
                    plan.Exception = "Paused: a check is due.";
                    continue;
                }
                if (local == null && remote == null)
                {
                    plan.Paused = true;
                    plan.Exception = "Paused: the aircraft is gone.";
                    continue;
                }
                if (!DestinationCatalogue.TryFind(plan.DestinationCode, out var destination))
                {
                    plan.Paused = true;
                    plan.Exception = "Paused: that destination is closed.";
                    continue;
                }
                var departAt = target.Advance(local == null ? 15 * 60
                    : DeparturePrep.TotalSeconds(local.Type, CareerState.BaseLevel) + 60);
                var result = local != null
                    ? ScheduleDeparture(local, destination, departAt)
                    : ScheduleOutstationService(remote.Registration, destination.Code, departAt, automated: true);
                if (!result.Accepted)
                {
                    plan.Paused = true;
                    plan.Exception = result.Reason;
                    continue;
                }
                if (local != null) local.AutomatedTrip = true;
                plan.Exception = string.Empty;
                plan.NextEligibleAtSeconds = target.ElapsedSeconds + plan.IntervalHours * 3600L;
            }
        }

        /// <summary>Expand the player's leased Adelaide operating footprint (ADR 0091).</summary>
        public CommandResult UpgradePlayerBase()
        {
            if (CareerState == null)
                return CommandResult.Refused("No career to expand.");
            if (!PlayerBase.TryNext(CareerState.BaseLevel, out var next))
                return CommandResult.Refused("Your Adelaide base is already at its largest.");
            if (CareerState.Tier < next.RequiredTier)
                return CommandResult.Refused($"The {next.Title} needs the {next.RequiredTier} tier.");
            if (CareerState.CompletedPlayerRotations < next.RequiredRotations)
                return CommandResult.Refused($"The {next.Title} needs {next.RequiredRotations} completed flights.");
            if (CareerState.Reliability < next.RequiredReliability)
                return CommandResult.Refused($"The {next.Title} needs {next.RequiredReliability}% reliability. You have {CareerState.Reliability}%.");
            if (!CareerState.TryChargePurchase(next.UpgradeCost))
                return CommandResult.Refused("The " + next.Title + " costs $" + next.UpgradeCost.ToString("N0")
                                             + ". You have $" + CareerState.Funds.ToString("N0") + ".");

            CareerState.BaseLevel = next.Level;
            AdvanceCareer();
            return CommandResult.Ok;
        }

        /// <summary>
        /// Takes a parked player aircraft out of service for its routine check (ADR 0085): pays
        /// for it, grounds it for <see cref="Maintenance.CheckSeconds"/>, and resets its wear.
        /// </summary>
        public CommandResult StartCheck(FleetAircraft aircraft)
        {
            if (aircraft == null || !_fleet.Contains(aircraft))
                return CommandResult.Refused("No such aircraft.");
            if (!aircraft.Airline.IsPlayer)
                return CommandResult.Refused("You can only send your own aircraft for a check.");
            if (aircraft.State != FleetState.AtStand)
                return CommandResult.Refused($"{aircraft.Registration} has to be parked for a check.");
            if (aircraft.Scheduled.HasValue)
                return CommandResult.Refused($"{aircraft.Registration} has a flight booked. Cancel it first.");
            if (Maintenance.InCheck(aircraft, _processedTo))
                return CommandResult.Refused($"{aircraft.Registration} is already in its check.");
            if (CareerState == null)
                return CommandResult.Refused("No career to charge the check against.");
            var seconds = Maintenance.CheckSeconds(aircraft.Type, CareerState.BaseLevel);
            var berth = HangarBays.Assign(_fleet, aircraft, _processedTo.ElapsedSeconds, _processedTo.ElapsedSeconds + seconds,
                CareerState.BaseLevel);
            if (berth.Full)
                return CommandResult.Refused($"Every hangar that fits {aircraft.Registration} is full until "
                    + $"{Clock.TimeText(new SimulationTime(berth.FreeAtSeconds))}.");
            var cost = Maintenance.CheckCost(aircraft.Type, CareerState.BaseLevel);
            if (!CareerState.TryChargePurchase(cost))
                return CommandResult.Refused($"A check costs ${cost:N0}. You have ${CareerState.Funds:N0}.");

            aircraft.RotationsSinceCheck = 0;
            aircraft.CheckUntil = _processedTo.Advance(Maintenance.CheckSeconds(aircraft.Type, CareerState.BaseLevel));
            return CommandResult.Ok;
        }

        /// <summary>
        /// True when this aircraft has a list price the player may sell against. The airline's
        /// only Saab is the one they were given, so it is not cashed out (ADR 0164).
        /// </summary>
        public bool CanResell(FleetAircraft aircraft)
        {
            if (aircraft == null || !aircraft.Airline.IsPlayer)
                return false;
            if (!AircraftAcquisition.TryFor(aircraft.Type, out _))
                return false;
            if (aircraft.IsFoundingAircraft)
                return false;
            if (aircraft.Type.Id != AircraftType.Saab340.Id)
                return true;
            var saabs = 0;
            foreach (var other in _fleet)
                if (other.Airline.IsPlayer && other.Type.Id == AircraftType.Saab340.Id)
                    saabs++;
            return saabs > 1;
        }

        /// <summary>
        /// Sells a player aircraft back for a fraction of its purchase price — the market side
        /// of a fleet the player over-committed to, or wants to specialise out of a type.
        /// Refuses a mid-rotation aircraft: only one <see cref="FleetState.AtStand"/> is safe to
        /// remove from the simulation without leaving a schedule, a taxi route or a runway
        /// booking pointing at a registration that no longer exists.
        /// </summary>
        public CommandResult SellAircraft(FleetAircraft aircraft)
        {
            if (aircraft == null)
                return CommandResult.Refused("No such aircraft.");
            if (!aircraft.Airline.IsPlayer)
                return CommandResult.Refused("You can only sell your own aircraft.");
            if (aircraft.State != FleetState.AtStand)
                return CommandResult.Refused($"{aircraft.Registration} has to be parked to sell.");
            if (Maintenance.InCheck(aircraft, _processedTo))
                return CommandResult.Refused($"{aircraft.Registration} is in its check until {Clock.TimeText(aircraft.CheckUntil.Value)}.");
            if (!CanResell(aircraft))
                return CommandResult.Refused(aircraft.Type.Id == AircraftType.Saab340.Id
                    ? "Your first Saab stays with the airline."
                    : $"Nobody is buying {aircraft.Type.Name}s.");

            AircraftAcquisition.TryFor(aircraft.Type, out var offer);
            var refund = (long)Math.Round(offer.Price * ResaleFraction);
            CareerState.RefundDispatch(refund);
            _fleet.Remove(aircraft);
            return CommandResult.Ok;
        }

        private StableId? SuggestPurchaseStand(AircraftType type)
        {
            if (CareerState != null)
                return SuggestPlayerStandFor(type);

            if (NeedsTerminalGate(type))
                return SuggestStandFor(type);
            var reserved = PlayerTurbopropsNeedingABay();
            var free = 0;
            foreach (var _ in FreeStandsFor(type))
                free++;
            return free <= reserved ? null : SuggestStandFor(type);
        }

        public static string NextPlayerRegistration(IReadOnlyList<FleetAircraft> fleet) =>
            NextPlayerRegistration(fleet, null);

        /// <summary>
        /// The first free <c>VH-P??</c> mark. <paramref name="retired"/> reports marks a sold aircraft
        /// already flew under: reissuing one would restart its trip numbers at settlement keys that
        /// are already spent, so the new aircraft's first flights would silently never pay.
        /// </summary>
        public static string NextPlayerRegistration(IReadOnlyList<FleetAircraft> fleet, Func<string, bool> retired)
        {
            for (var a = 'A'; a <= 'Z'; a++)
            for (var b = 'A'; b <= 'Z'; b++)
            {
                var candidate = $"VH-P{a}{b}";
                var taken = false;
                if (fleet != null)
                {
                    foreach (var aircraft in fleet)
                        if (string.Equals(aircraft.Registration, candidate, StringComparison.OrdinalIgnoreCase))
                            taken = true;
                }

                if (!taken && (retired == null || !retired(candidate)))
                    return candidate;
            }

            throw new InvalidOperationException("No player registration left.");
        }
    }
}
