using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    public sealed class AirportSimulation
    {
        public const long CycleLengthSeconds = 160;
        public const long DepartureResetSeconds = 6;
        public const long BaseDailyOperatingCost = 400;
        public const int SecondFlightThreshold = 4;

        public static readonly StableId Runway = new("RUNWAY-09-27");
        public static readonly StableId ApronLane = new("APRON-LANE");
        public static readonly StableId StandOne = new("STAND-1");
        public static readonly StableId StandTwo = new("STAND-2");
        public static readonly StableId StandThree = new("STAND-3");

        private readonly ISimulationClock _clock;
        private readonly IRandomSource _random;
        private readonly ReservationTable _reservations;
        private readonly List<CommercialFlight> _flights = new();
        private SimulationTime _lastUpdatedAt;
        private int _daysSettled;
        private int _dayStartCycles;
        private long _dayStartCash;
        private long _dayStartRevenue;
        private long _dayStartRouteIncome;
        private long _dayStartDelayCost;
        private int _dayStartReputation;
        private int _nextAircraftNumber = 101;
        private readonly GroundTrafficAircraft[] _groundTraffic;
        private bool _approachWaitLogged;

        public AirportSimulation(ISimulationClock clock, IRandomSource random, ReservationTable reservations)
            : this(clock, random, reservations, AirportLocation.Default)
        {
        }

        public AirportSimulation(ISimulationClock clock, IRandomSource random, ReservationTable reservations, AirportLocation location)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _reservations = reservations ?? throw new ArgumentNullException(nameof(reservations));
            Location = location;
            _lastUpdatedAt = clock.Now;
            Economy = new AirportEconomy();
            TaxiNetwork = new AirportTaxiNetwork();
            EventLog = new OperationalEventLog();
            TrafficWaits = new TrafficWaitMonitor();
            Routes = new AirportRoutes(clock.Now);
            Reputation = new AirportReputation();
            Staffing = new AirportStaffing();
            Research = new AirportResearch();
            Capacity = new AirportCapacity();
            DailyReports = new AirportDailyReports();
            CaptureDayBaseline();
            _groundTraffic = new[]
            {
                new GroundTrafficAircraft(new StableId("GT-201"), _reservations, GroundTrafficRole.ArriveDepart, 0),
                new GroundTrafficAircraft(new StableId("GT-202"), _reservations, GroundTrafficRole.Reposition, 25)
            };
            SpawnCommercial(_clock.Now, preferredStand: null, consumeRandomWhenChoosing: true);
            SynchronizeCommercialReservations();
            foreach (var aircraft in _groundTraffic)
                aircraft.Reposition(_clock.Now, TrafficWaits, CommercialStandOccupancy(), Capacity.StandCount,
                    mayEnterCorridor: true);
        }

        public AirportLocation Location { get; }
        public DayCycle TimeOfDay => new(_clock.Now);
        public IReadOnlyList<CommercialFlight> Flights => _flights;

        /// <summary>Migration accessor for the first commercial flight's operation.</summary>
        public AircraftOperation ActiveAircraft => FocusFlight.Operation;

        /// <summary>Migration accessor for the first commercial flight's stand.</summary>
        public StableId AssignedStand => FocusFlight.AssignedStand;

        public int CompletedCycles { get; private set; }
        public int ReservationConflicts { get; private set; }
        public TurnaroundWorkflow ActiveTurnaround => FocusFlight.Turnaround;
        public AirportEconomy Economy { get; }
        public AirportRoutes Routes { get; }
        public AirportReputation Reputation { get; }
        public AirportStaffing Staffing { get; }
        public AirportResearch Research { get; }
        public AirportCapacity Capacity { get; }
        public AirportDailyReports DailyReports { get; }
        public bool IsInsolvent => Economy.IsInsolvent;
        public AirportTaxiNetwork TaxiNetwork { get; }
        public TaxiRoute ActiveTaxiRoute => FocusFlight.TaxiRoute;
        public OperationalEventLog EventLog { get; }
        public TrafficWaitMonitor TrafficWaits { get; }
        public IReadOnlyList<GroundTrafficAircraft> GroundTraffic => _groundTraffic;
        public StableId CurrentTaxiSegment => FocusFlight.SegmentFor(_clock.Now);
        public long LastDelaySeconds { get; private set; }
        public string LastDelayCause { get; private set; } = string.Empty;
        public long CurrentDelaySeconds => FocusFlight.Operation.Phase == AircraftPhase.AtStand && FocusFlight.Turnaround != null
            ? FocusFlight.Turnaround.DelaySeconds(_clock.Now)
            : 0;
        public string CurrentDelayCause => FocusFlight.Operation.Phase == AircraftPhase.AtStand && FocusFlight.Turnaround != null
            ? FocusFlight.Turnaround.DelayCause(_clock.Now)
            : string.Empty;
        public SimulationTime CycleStartedAt => Primary.CycleStartedAt;
        public ReservationTable Reservations => _reservations;
        public int MaxScheduledFlightsPerDay => AirportRoutes.MaxScheduledFlightsPerDay(Capacity.StandCount);

        /// <summary>Hard cap on concurrent commercials — one per built stand.</summary>
        public int MaxConcurrentCommercialFlights => Capacity.StandCount;

        private CommercialFlight Primary => _flights[0];

        /// <summary>
        /// Player-facing focus: prefer any commercial currently at stand (turnaround /
        /// priority crew / delay HUD), otherwise the earliest-spawned flight.
        /// </summary>
        private CommercialFlight FocusFlight
        {
            get
            {
                foreach (var flight in _flights)
                {
                    if (flight.Operation.Phase == AircraftPhase.AtStand && flight.Turnaround != null)
                        return flight;
                }

                return Primary;
            }
        }
        public bool BuildThirdStand()
        {
            if (IsInsolvent)
                return false;
            if (!Capacity.CanExpand)
                return false;
            if (!Economy.TrySpend(AirportCapacity.ThirdStandCost) || !Capacity.Expand())
                return false;

            Record(_lastUpdatedAt, "Stand 3 built",
                $"Capacity now {Capacity.StandCount} stands · -${AirportCapacity.ThirdStandCost:N0}");
            return true;
        }

        public bool StartOperationsResearch()
        {
            if (IsInsolvent)
                return false;
            if (!Research.CanStartOperationsEfficiency)
                return false;
            if (!Economy.TrySpend(AirportResearch.OperationsEfficiencyCost))
                return false;
            var startedAt = _clock.Now;
            if (!Research.StartOperationsEfficiency(startedAt))
                return false;

            Record(startedAt, "Research started",
                $"{AirportResearch.OperationsEfficiencyName} · {AirportResearch.OperationsEfficiencyDurationSeconds}s · -${AirportResearch.OperationsEfficiencyCost:N0}");
            return true;
        }

        public bool StartPassengerServicesResearch()
        {
            if (IsInsolvent)
                return false;
            if (!Research.CanStartPassengerServices)
                return false;
            if (!Economy.TrySpend(AirportResearch.PassengerServicesCost))
                return false;
            var startedAt = _clock.Now;
            if (!Research.StartPassengerServices(startedAt))
                return false;

            Record(startedAt, "Research started",
                $"{AirportResearch.PassengerServicesName} · {AirportResearch.PassengerServicesDurationSeconds}s · -${AirportResearch.PassengerServicesCost:N0}");
            return true;
        }

        /// <summary>
        /// Expected daily income and cost under the current weather, staffing and
        /// route book, assuming today's flight cadence — every commercial aircraft
        /// currently operating — continues with no delays.
        /// </summary>
        public DailyFinanceBrief DailyFinance
        {
            get
            {
                var operatingCost = Math.Max(0, BaseDailyOperatingCost - Research.DailyOperatingDiscount)
                    + Weather.DailyOperatingCost(CurrentWeather)
                    + Staffing.DailyWage;
                var incomePerCycle = AirportEconomy.TurnaroundRevenue + Routes.IncomePerFlight + Research.RouteIncomeBonus;
                // Every commercial aircraft flies its own cycle, so the projection has to
                // count them all — otherwise the brief halves the moment a second one starts.
                var concurrentFlights = Math.Max(1, _flights.Count);
                var expectedIncome = incomePerCycle * concurrentFlights * DayCycle.DaySeconds / CycleLengthSeconds;
                return new DailyFinanceBrief(operatingCost, expectedIncome, Economy.Cash);
            }
        }

        public void Update()
        {
            if (_clock.Now.CompareTo(_lastUpdatedAt) < 0)
                throw new InvalidOperationException("Simulation time cannot move backwards.");

            while (_lastUpdatedAt.CompareTo(_clock.Now) < 0)
            {
                _lastUpdatedAt = _lastUpdatedAt.Advance(1);
                AdvanceOneSecond(_lastUpdatedAt);
            }
        }

        public bool EnablePriorityCrew()
        {
            if (IsInsolvent)
                return false;

            var target = FocusFlight;
            if (target.Operation.Phase != AircraftPhase.AtStand || target.Turnaround == null || target.Turnaround.PriorityCrewEnabled)
                return false;
            if (!Economy.PurchasePriorityCrew())
                return false;

            // Scale only remaining unfinished tasks from enable time (see TurnaroundWorkflow).
            target.Turnaround.EnablePriorityCrew(_clock.Now);
            Record(_lastUpdatedAt, target.AircraftId, "Priority crew assigned", "$300 schedule recovery decision");
            return true;
        }

        public bool AcceptPendingRoute()
        {
            if (IsInsolvent)
                return false;
            var proposal = Routes.Pending;
            if (proposal == null || !Routes.Accept(_lastUpdatedAt, Reputation.Score, Reputation.IncomeBonus, Capacity.StandCount))
                return false;

            var paid = proposal.IncomePerFlight + Reputation.IncomeBonus;
            Record(_lastUpdatedAt, Primary.AircraftId, "Route accepted",
                $"{proposal.Airline} · {proposal.FlightsPerDay}/day to {proposal.Destination} · +${paid}/flight");
            return true;
        }

        public bool DeclinePendingRoute()
        {
            if (IsInsolvent)
                return false;
            var proposal = Routes.Pending;
            if (proposal == null || !Routes.Decline())
                return false;

            Record(_lastUpdatedAt, Primary.AircraftId, "Route declined", $"{proposal.Airline} to {proposal.Destination}");
            return true;
        }

        public bool HireGroundCrew()
        {
            if (IsInsolvent)
                return false;
            if (Staffing.GroundCrew >= AirportStaffing.MaximumGroundCrew)
                return false;
            if (!Economy.TrySpend(AirportStaffing.HireCost) || !Staffing.Hire())
                return false;

            Record(_lastUpdatedAt, Primary.AircraftId, "Crew hired",
                $"Ground crew now {Staffing.GroundCrew} · payroll ${Staffing.DailyWage:N0}/day");
            return true;
        }

        public bool ReleaseGroundCrew()
        {
            if (IsInsolvent)
                return false;
            if (!Staffing.Release())
                return false;

            Record(_lastUpdatedAt, Primary.AircraftId, "Crew released",
                $"Ground crew now {Staffing.GroundCrew} · payroll ${Staffing.DailyWage:N0}/day");
            return true;
        }

        private void AdvanceOneSecond(SimulationTime now)
        {
            if (IsInsolvent)
                return;


            Routes.Update(now);
            if (Research.Update(now))
            {
                var detail = Research.LastCompletedProjectId == AirportResearch.PassengerServicesId
                    ? $"{AirportResearch.PassengerServicesName} · +${AirportResearch.PassengerServicesRouteBonus:N0}/flight route income"
                    : $"{AirportResearch.OperationsEfficiencyName} · daily running cost -${AirportResearch.OperationsEfficiencyDailyDiscount:N0}";
                Record(now, "Research complete", detail);
            }

            SettleDaysUpTo(now);
            // The closing report can declare insolvency during this tick. Do not
            // move aircraft or settle another flight after the terminal event.
            if (IsInsolvent)
                return;
            TrySpawnSecondCommercial(now);

            // Ground traffic yields before commercials move, so a commercial never
            // stalls on a segment a fleet aircraft would have released this tick.
            YieldGroundTrafficToCommercials(now);

            var active = _flights.ToArray();
            foreach (var flight in active)
                AdvanceCommercial(flight, now);

            SynchronizeAllTraffic(now);
        }

        private void YieldGroundTrafficToCommercials(SimulationTime now)
        {
            var required = new List<StableId>();
            foreach (var flight in _flights)
            {
                if (flight.Operation.IsComplete)
                    continue;

                foreach (var resource in flight.RequiredResources(now))
                    required.Add(resource);

                // Also clear the next phase so CanLeavePhase is not blocked by GT.
                var next = (AircraftPhase)((int)flight.Operation.Phase + 1);
                if (next <= AircraftPhase.Departed)
                {
                    foreach (var resource in flight.ResourcesForPhase(next, now))
                        required.Add(resource);
                }
            }

            foreach (var aircraft in _groundTraffic)
                aircraft.Yield(required);
        }

        private void AdvanceCommercial(CommercialFlight flight, SimulationTime now)
        {
            if (flight.Operation.IsComplete)
            {
                // Departed aircraft must not keep the runway for the reset window.
                _reservations.Release(flight.OwnerId);
                TrafficWaits.Clear(flight.OwnerId);
                if (now.CompareTo(flight.Operation.PhaseStartedAt.Advance(DepartureResetSeconds)) >= 0)
                    TryRespawnCommercial(flight, now);
                return;
            }

            // Hold current resources before advancing. If another commercial owns a
            // shared taxi/runway segment, stall schedule progress rather than occupy it.
            if (!_reservations.TryReplace(flight.OwnerId, flight.RequiredResources(now), out var blocked))
            {
                if (!IsCommercialOwner(blocked))
                    ReservationConflicts++;
                TrafficWaits.SetWaiting(flight.OwnerId, blocked, now);
                flight.Operation.StallOneSecond();
                return;
            }

            // Holding short after TaxiOut: ResourcesForPhase is empty, so TryReplace
            // succeeded without claiming the runway. Surface the takeoff wait instead
            // of silently clearing TrafficWaits.
            if (flight.Operation.Phase == AircraftPhase.TaxiOut
                && flight.Operation.SecondsRemaining(now) <= 0
                && !_reservations.CanReplace(flight.OwnerId,
                    flight.ResourcesForPhase(AircraftPhase.Takeoff, now), out var runwayBlocked))
            {
                TrafficWaits.SetWaiting(flight.OwnerId, runwayBlocked, now);
                return;
            }

            TrafficWaits.Clear(flight.OwnerId);

            var previousPhase = flight.Operation.Phase;
            flight.Operation.AdvanceTo(now, phase => CanLeavePhase(flight, phase, now));

            if (previousPhase != flight.Operation.Phase)
            {
                // Re-acquire resources for the new phase. CanLeavePhase already checked
                // availability against the table at transition time.
                if (!_reservations.TryReplace(flight.OwnerId, flight.RequiredResources(now), out blocked))
                {
                    if (!IsCommercialOwner(blocked))
                        ReservationConflicts++;
                    TrafficWaits.SetWaiting(flight.OwnerId, blocked, now);
                }
                else
                {
                    TrafficWaits.Clear(flight.OwnerId);
                }
            }

            if (previousPhase == flight.Operation.Phase)
                return;

            if (flight.Operation.Phase == AircraftPhase.AtStand)
            {
                // Live staffing factor — hiring mid-turnaround takes effect immediately.
                flight.Turnaround = new TurnaroundWorkflow(
                    flight.Operation.PhaseStartedAt,
                    _random.NextInt(0, 3) == 0,
                    () => Staffing.TurnaroundSpeedFactor);
                flight.Operation.BindAtStandProgress(t => flight.Turnaround.Progress01(t));
                Record(now, flight.AircraftId, "On stand", $"Arrived at {flight.AssignedStand.Value}");
            }

            if (previousPhase == AircraftPhase.AtStand && flight.Turnaround != null)
            {
                flight.LastDelaySeconds = flight.Turnaround.DelaySeconds(now);
                // Every delay has to name a cause the player can act on — understaffing
                // included, not just cabin-cleaning disruptions.
                flight.LastDelayCause = flight.LastDelaySeconds > 0
                    ? flight.Turnaround.OverrunCause
                    : string.Empty;
                LastDelaySeconds = flight.LastDelaySeconds;
                LastDelayCause = flight.LastDelayCause;
                if (flight.LastDelaySeconds > 0)
                    Record(now, flight.AircraftId, $"Delayed {flight.LastDelaySeconds}s", flight.LastDelayCause);
                Record(now, flight.AircraftId, "Turnaround complete", "Pushback approved");
            }

            if (flight.Operation.Phase == AircraftPhase.Departed && !flight.FlightSettled)
            {
                Economy.CompleteFlight(flight.LastDelaySeconds);
                Reputation.RecordDeparture(flight.LastDelaySeconds);
                if (Routes.IncomePerFlight > 0 || Research.RouteIncomeBonus > 0)
                {
                    var paid = Routes.IncomePerFlight + Research.RouteIncomeBonus;
                    Economy.AddRouteIncome(paid);
                    Record(now, flight.AircraftId, "Route income",
                        $"+${paid:N0} from {Routes.Accepted.Count} scheduled route(s)");
                }

                flight.FlightSettled = true;
                CompletedCycles++;
                Record(now, flight.AircraftId, "Departed",
                    $"Net flight result ${AirportEconomy.TurnaroundRevenue - flight.LastDelaySeconds * AirportEconomy.DelayCostPerSecond:N0}");
                _reservations.Release(flight.OwnerId);
                TrafficWaits.Clear(flight.OwnerId);
            }
            else if (flight.Operation.Phase == AircraftPhase.Landing)
            {
                Record(now, flight.AircraftId, "Landing", Runway.Value);
            }
        }

        private void TrySpawnSecondCommercial(SimulationTime now)
        {
            if (_flights.Count >= MaxConcurrentCommercialFlights)
                return;
            if (Routes.ScheduledFlightsPerDay < SecondFlightThreshold)
                return;

            var primary = Primary;
            var dueAt = primary.CycleStartedAt.Advance(CycleLengthSeconds / 2);
            if (now.CompareTo(dueAt) < 0)
                return;

            if (!TryPickStand(out var stand, consumeRandomWhenChoosing: false))
            {
                if (!_approachWaitLogged)
                {
                    Record(now, primary.AircraftId, "Approach hold", "Second commercial waiting for a free stand");
                    _approachWaitLogged = true;
                }

                return;
            }

            _approachWaitLogged = false;
            SpawnCommercial(now, stand, consumeRandomWhenChoosing: false);
        }

        private void SpawnCommercial(SimulationTime at, StableId? preferredStand, bool consumeRandomWhenChoosing)
        {
            StableId stand;
            if (preferredStand.HasValue)
            {
                stand = preferredStand.Value;
            }
            else if (!TryPickStand(out stand, consumeRandomWhenChoosing))
            {
                throw new InvalidOperationException("A commercial flight requires a free stand.");
            }

            var id = $"AS-{_nextAircraftNumber++:000}";
            var flight = new CommercialFlight(id, at, stand, TaxiNetwork.RouteTo(stand));
            _flights.Add(flight);
            _flights.Sort(CompareFlights);
            Record(at, id, "Flight inbound", $"Assigned {stand.Value}");
        }

        private void TryRespawnCommercial(CommercialFlight flight, SimulationTime now)
        {
            if (!TryPickStand(out var stand, consumeRandomWhenChoosing: true))
                return;

            _reservations.Release(flight.OwnerId);

            var id = $"AS-{_nextAircraftNumber++:000}";
            var index = _flights.IndexOf(flight);
            _flights[index] = new CommercialFlight(id, now, stand, TaxiNetwork.RouteTo(stand));
            _flights.Sort(CompareFlights);
            Record(now, id, "Flight inbound", $"Assigned {stand.Value}");
        }

        private static int CompareFlights(CommercialFlight a, CommercialFlight b)
        {
            var bySpawn = a.SpawnedAt.ElapsedSeconds.CompareTo(b.SpawnedAt.ElapsedSeconds);
            return bySpawn != 0 ? bySpawn : string.CompareOrdinal(a.AircraftId, b.AircraftId);
        }

        /// <summary>
        /// Cycle to the next built stand after <paramref name="primaryStand"/>.
        /// With three stands: 1→2→3→1.
        /// </summary>
        public static StableId AlternateStand(StableId primaryStand, int standCount)
        {
            if (standCount < 3)
                return primaryStand.Equals(StandOne) ? StandTwo : StandOne;

            if (primaryStand.Equals(StandOne))
                return StandTwo;
            if (primaryStand.Equals(StandTwo))
                return StandThree;
            return StandOne;
        }

        private StableId[] AvailableStands()
        {
            if (Capacity.StandCount >= 3)
                return new[] { StandOne, StandTwo, StandThree };
            return new[] { StandOne, StandTwo };
        }

        private bool TryPickStand(out StableId stand, bool consumeRandomWhenChoosing)
        {
            var occupied = CommercialStandOccupancy();
            var stands = AvailableStands();
            var free = new List<StableId>(stands.Length);
            foreach (var candidate in stands)
            {
                if (IsStandOccupied(candidate, occupied))
                    continue;
                free.Add(candidate);
            }

            if (free.Count == 0)
            {
                stand = default;
                return false;
            }

            if (free.Count == 1 || !consumeRandomWhenChoosing)
            {
                if (!consumeRandomWhenChoosing && _flights.Count > 0)
                {
                    var primaryStand = Primary.AssignedStand;
                    foreach (var candidate in free)
                    {
                        if (!candidate.Equals(primaryStand))
                        {
                            stand = candidate;
                            return true;
                        }
                    }
                }

                stand = free[0];
                return true;
            }

            stand = free[_random.NextInt(0, free.Count)];
            return true;
        }

        private bool IsStandOccupied(StableId candidate, List<StableId> commercialOccupied)
        {
            foreach (var busy in commercialOccupied)
            {
                if (busy.Equals(candidate))
                    return true;
            }

            // Reservation-table holders (including GT parked on / taxiing onto the stand).
            if (_reservations.IsReserved(candidate))
            {
                if (!_reservations.TryGetOwner(candidate, out var owner) || !IsFlightOwner(owner))
                    return true;
            }

            foreach (var aircraft in _groundTraffic)
            {
                if (aircraft.CurrentSegment.Equals(candidate))
                    return true;
                if (aircraft.IsAtStand && aircraft.TargetStand.Equals(candidate))
                    return true;
            }

            return false;
        }

        private bool IsFlightOwner(StableId owner)
        {
            foreach (var flight in _flights)
            {
                if (flight.OwnerId.Equals(owner))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Stands claimed by any non-departed commercial, including Approach/Landing
        /// assignments so GT does not park on an inbound stand.
        /// </summary>
        private List<StableId> CommercialStandOccupancy()
        {
            var stands = new List<StableId>(_flights.Count);
            foreach (var flight in _flights)
            {
                if (flight.Operation.IsComplete)
                    continue;
                stands.Add(flight.AssignedStand);
            }

            return stands;
        }

        public WeatherKind CurrentWeather => Weather.At(_lastUpdatedAt);

        private void SettleDaysUpTo(SimulationTime now)
        {
            var day = new DayCycle(now).DaysElapsed;
            while (_daysSettled < day)
            {
                _daysSettled++;
                var closeTime = new SimulationTime((long)(DayCycle.DaySeconds * (_daysSettled - 8.0 / 24.0)));
                var weather = Weather.At(closeTime);
                var baseCost = Math.Max(0, BaseDailyOperatingCost - Research.DailyOperatingDiscount);
                var cost = baseCost + Weather.DailyOperatingCost(weather) + Staffing.DailyWage;
                Economy.PayOperatingCosts(cost);

                var report = new DailyReport(
                    dayNumber: _daysSettled,
                    closingWeather: weather,
                    flightsCompleted: CompletedCycles - _dayStartCycles,
                    turnaroundRevenue: (Economy.TotalRevenue - Economy.TotalRouteIncome) - (_dayStartRevenue - _dayStartRouteIncome),
                    routeIncome: Economy.TotalRouteIncome - _dayStartRouteIncome,
                    delayCost: Economy.TotalDelayCost - _dayStartDelayCost,
                    operatingCost: cost,
                    netCashChange: Economy.Cash - _dayStartCash,
                    reputationChange: Reputation.Score - _dayStartReputation,
                    groundCrew: Staffing.GroundCrew);
                DailyReports.Add(report);
                CaptureDayBaseline();

                Record(now, $"Day {_daysSettled} closed",
                    $"{report.FlightsCompleted} flight(s) · net {(report.NetCashChange >= 0 ? "+" : "")}${report.NetCashChange:N0} · running -${cost:N0} ({Weather.Describe(weather)}, {Staffing.GroundCrew} crew) · cash ${Economy.Cash:N0}");

                Economy.EvaluateDayEndSolvency();
                if (IsInsolvent)
                {
                    Record(now, "Insolvent",
                        $"Cash remained negative for {AirportEconomy.InsolvencyConsecutiveDays} consecutive days · cash ${Economy.Cash:N0}");
                    break;
                }
            }
        }

        private void SynchronizeAllTraffic(SimulationTime now)
        {
            // Commercials already advanced and held their resources this tick. Reconcile
            // the table (in case a segment changed mid-taxi), then let the fleet move
            // into whatever space is left. Fleet aircraft queue through the corridor lock.
            SynchronizeCommercialReservations();

            var grantee = ChooseCorridorGrantee(now);
            var occupied = CommercialStandOccupancy();
            foreach (var aircraft in _groundTraffic)
                aircraft.Reposition(now, TrafficWaits, occupied, Capacity.StandCount,
                    mayEnterCorridor: aircraft.OnCorridor || ReferenceEquals(aircraft, grantee));
        }

        private void SynchronizeCommercialReservations()
        {
            foreach (var flight in _flights)
            {
                if (flight.Operation.IsComplete)
                {
                    _reservations.Release(flight.OwnerId);
                    TrafficWaits.Clear(flight.OwnerId);
                    continue;
                }

                var owner = flight.OwnerId;
                var now = _lastUpdatedAt;
                if (!_reservations.TryReplace(owner, flight.RequiredResources(now), out var blocked))
                {
                    if (!IsCommercialOwner(blocked))
                        ReservationConflicts++;
                    TrafficWaits.SetWaiting(owner, blocked, now);
                }
                else if (flight.Operation.Phase == AircraftPhase.TaxiOut
                    && flight.Operation.SecondsRemaining(now) <= 0
                    && !_reservations.CanReplace(owner,
                        flight.ResourcesForPhase(AircraftPhase.Takeoff, now), out var runwayBlocked))
                {
                    // Holding short: RequiredResources is empty so TryReplace succeeded,
                    // but takeoff is still blocked — keep the runway wait visible.
                    TrafficWaits.SetWaiting(owner, runwayBlocked, now);
                }
                else
                {
                    TrafficWaits.Clear(owner);
                }
            }
        }

        private bool IsCommercialOwner(StableId resource)
        {
            if (!_reservations.TryGetOwner(resource, out var owner))
                return false;

            return IsFlightOwner(owner);
        }

        private GroundTrafficAircraft ChooseCorridorGrantee(SimulationTime now)
        {
            if (_reservations.TryGetOwner(AirportTaxiNetwork.Corridor, out var owner))
            {
                foreach (var aircraft in _groundTraffic)
                {
                    if (aircraft.Id.Equals(owner))
                        return aircraft;
                }

                return null;
            }

            GroundTrafficAircraft best = null;
            var bestWaitStart = long.MaxValue;
            foreach (var aircraft in _groundTraffic)
            {
                if (!aircraft.WantsCorridorNow)
                    continue;

                var waitStart = TrafficWaits.TryGetWaitStart(aircraft.Id, out var startedAt)
                    ? startedAt.ElapsedSeconds
                    : now.ElapsedSeconds;
                if (waitStart < bestWaitStart)
                {
                    bestWaitStart = waitStart;
                    best = aircraft;
                }
            }

            return best;
        }

        private bool CanLeavePhase(CommercialFlight flight, AircraftPhase phase, SimulationTime now)
        {
            if (phase == AircraftPhase.AtStand
                && (flight.Turnaround == null || !flight.Turnaround.IsComplete(now)))
                return false;

            var next = (AircraftPhase)((int)phase + 1);
            if (next >= AircraftPhase.Departed)
                return true;

            return _reservations.CanReplace(flight.OwnerId, flight.ResourcesForPhase(next, now), out _);
        }

        private void CaptureDayBaseline()
        {
            _dayStartCycles = CompletedCycles;
            _dayStartCash = Economy.Cash;
            _dayStartRevenue = Economy.TotalRevenue;
            _dayStartRouteIncome = Economy.TotalRouteIncome;
            _dayStartDelayCost = Economy.TotalDelayCost;
            _dayStartReputation = Reputation.Score;
        }

        private void Record(SimulationTime time, string title, string detail) =>
            Record(time, string.Empty, title, detail);

        private void Record(SimulationTime time, string flightId, string title, string detail)
        {
            EventLog.Add(new OperationalEvent(time, flightId ?? string.Empty, title, detail));
        }
    }
}
