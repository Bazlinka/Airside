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

        public static readonly StableId Runway = new("RUNWAY-09-27");
        public static readonly StableId ApronLane = new("APRON-LANE");
        public static readonly StableId StandOne = new("STAND-1");
        public static readonly StableId StandTwo = new("STAND-2");
        public static readonly StableId StandThree = new("STAND-3");

        private readonly ISimulationClock _clock;
        private readonly IRandomSource _random;
        private readonly ReservationTable _reservations;
        private SimulationTime _cycleStartedAt;
        private SimulationTime _lastUpdatedAt;
        private bool _flightSettled;
        private int _daysSettled;
        private int _dayStartCycles;
        private long _dayStartCash;
        private long _dayStartRevenue;
        private long _dayStartRouteIncome;
        private long _dayStartDelayCost;
        private int _dayStartReputation;
        private readonly GroundTrafficAircraft[] _groundTraffic;

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
            _cycleStartedAt = clock.Now;
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
            StartCycle();
            SynchronizeReservations();
            foreach (var aircraft in _groundTraffic)
                aircraft.Reposition(_clock.Now, TrafficWaits, AssignedStand, Capacity.StandCount, mayEnterCorridor: true);
        }

        public AirportLocation Location { get; }
        public DayCycle TimeOfDay => new(_clock.Now);
        public AircraftOperation ActiveAircraft { get; private set; }
        public StableId AssignedStand { get; private set; }
        public int CompletedCycles { get; private set; }
        public int ReservationConflicts { get; private set; }
        public TurnaroundWorkflow ActiveTurnaround { get; private set; }
        public AirportEconomy Economy { get; }
        public AirportRoutes Routes { get; }
        public AirportReputation Reputation { get; }
        public AirportStaffing Staffing { get; }
        public AirportResearch Research { get; }
        public AirportCapacity Capacity { get; }
        public AirportDailyReports DailyReports { get; }
        public AirportTaxiNetwork TaxiNetwork { get; }
        public TaxiRoute ActiveTaxiRoute { get; private set; }
        public OperationalEventLog EventLog { get; }
        public TrafficWaitMonitor TrafficWaits { get; }
        public IReadOnlyList<GroundTrafficAircraft> GroundTraffic => _groundTraffic;
        public bool IsInsolvent => Economy.IsInsolvent;
        public StableId CurrentTaxiSegment => SegmentFor(ActiveAircraft.Phase, ActiveAircraft.PhaseProgress(_clock.Now));
        public long LastDelaySeconds { get; private set; }
        public string LastDelayCause { get; private set; } = string.Empty;
        public long CurrentDelaySeconds => ActiveAircraft.Phase == AircraftPhase.AtStand && ActiveTurnaround != null
            ? ActiveTurnaround.DelaySeconds(_clock.Now)
            : 0;
        public string CurrentDelayCause => ActiveAircraft.Phase == AircraftPhase.AtStand && ActiveTurnaround != null
            ? ActiveTurnaround.DelayCause(_clock.Now)
            : string.Empty;
        public SimulationTime CycleStartedAt => _cycleStartedAt;
        public ReservationTable Reservations => _reservations;

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
            if (ActiveAircraft.Phase != AircraftPhase.AtStand || ActiveTurnaround == null || ActiveTurnaround.PriorityCrewEnabled)
                return false;
            if (!Economy.PurchasePriorityCrew())
                return false;

            ActiveTurnaround.EnablePriorityCrew();
            Record(_lastUpdatedAt, "Priority crew assigned", "$300 schedule recovery decision");
            return true;
        }

        public bool AcceptPendingRoute()
        {
            if (IsInsolvent)
                return false;
            var proposal = Routes.Pending;
            if (proposal == null || !Routes.Accept(_lastUpdatedAt, Reputation.Score, Reputation.IncomeBonus))
                return false;

            var paid = proposal.IncomePerFlight + Reputation.IncomeBonus;
            Record(_lastUpdatedAt, "Route accepted",
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

            Record(_lastUpdatedAt, "Route declined", $"{proposal.Airline} to {proposal.Destination}");
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

            Record(_lastUpdatedAt, "Crew hired",
                $"Ground crew now {Staffing.GroundCrew} · payroll ${Staffing.DailyWage:N0}/day");
            return true;
        }

        public bool ReleaseGroundCrew()
        {
            if (IsInsolvent)
                return false;
            if (!Staffing.Release())
                return false;

            Record(_lastUpdatedAt, "Crew released",
                $"Ground crew now {Staffing.GroundCrew} · payroll ${Staffing.DailyWage:N0}/day");
            return true;
        }

        public bool BuildThirdStand()
        {
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
            if (!Research.StartOperationsEfficiency(_lastUpdatedAt))
                return false;

            Record(_lastUpdatedAt, "Research started",
                $"{AirportResearch.OperationsEfficiencyName} · {AirportResearch.OperationsEfficiencyDurationSeconds}s · -${AirportResearch.OperationsEfficiencyCost:N0}");
            return true;
        }

        private void AdvanceOneSecond(SimulationTime now)
        {
            if (IsInsolvent)
                return;

            Routes.Update(now);
            if (Research.Update(now))
                Record(now, "Research complete",
                    $"{AirportResearch.OperationsEfficiencyName} · daily running cost -${AirportResearch.OperationsEfficiencyDailyDiscount:N0}");
            SettleDaysUpTo(now);
            if (IsInsolvent)
                return;

            if (ActiveAircraft.IsComplete)
            {
                if (now.CompareTo(ActiveAircraft.PhaseStartedAt.Advance(DepartureResetSeconds)) >= 0)
                {
                    _reservations.Release(new StableId(ActiveAircraft.AircraftId));
                    CompletedCycles++;
                    _cycleStartedAt = now;
                    StartCycle();
                }

                SynchronizeAllTraffic(now);
                return;
            }

            var previousPhase = ActiveAircraft.Phase;
            ActiveAircraft.AdvanceTo(now, CanLeavePhase);

            if (previousPhase != ActiveAircraft.Phase)
            {
                if (ActiveAircraft.Phase == AircraftPhase.AtStand)
                {
                    ActiveTurnaround = new TurnaroundWorkflow(
                        ActiveAircraft.PhaseStartedAt, _random.NextInt(0, 3) == 0, Staffing.TurnaroundSpeedFactor);
                    Record(now, "On stand", $"Arrived at {AssignedStand.Value}");
                }

                if (previousPhase == AircraftPhase.AtStand && ActiveTurnaround != null)
                {
                    LastDelaySeconds = ActiveTurnaround.DelaySeconds(now);
                    LastDelayCause = LastDelaySeconds > 0 && ActiveTurnaround.HasCleaningDisruption
                        ? "Cabin cleaning disruption"
                        : string.Empty;
                    if (LastDelaySeconds > 0)
                        Record(now, $"Delayed {LastDelaySeconds}s", LastDelayCause);
                    Record(now, "Turnaround complete", "Pushback approved");
                }

                if (ActiveAircraft.Phase == AircraftPhase.Departed && !_flightSettled)
                {
                    Economy.CompleteFlight(LastDelaySeconds);
                    Reputation.RecordDeparture(LastDelaySeconds);
                    if (Routes.IncomePerFlight > 0)
                    {
                        Economy.AddRouteIncome(Routes.IncomePerFlight);
                        Record(now, "Route income", $"+${Routes.IncomePerFlight:N0} from {Routes.Accepted.Count} scheduled route(s)");
                    }
                    _flightSettled = true;
                    Record(now, "Departed", $"Net flight result ${AirportEconomy.TurnaroundRevenue - LastDelaySeconds * AirportEconomy.DelayCostPerSecond:N0}");
                }
                else if (ActiveAircraft.Phase == AircraftPhase.Landing)
                    Record(now, "Landing", Runway.Value);
            }

            SynchronizeAllTraffic(now);
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
            // The primary flight has priority: every ground-traffic aircraft releases
            // any resource the flight needs this tick, the flight then takes its
            // reservations, and the fleet moves into whatever space is left. Fleet
            // aircraft queue behind one another through the shared taxi corridor lock.
            var required = new List<StableId>(RequiredResources());
            foreach (var aircraft in _groundTraffic)
                aircraft.Yield(required);

            SynchronizeReservations();

            var grantee = ChooseCorridorGrantee(now);
            foreach (var aircraft in _groundTraffic)
                aircraft.Reposition(now, TrafficWaits, AssignedStand, Capacity.StandCount,
                    mayEnterCorridor: aircraft.OnCorridor || ReferenceEquals(aircraft, grantee));
        }

        // The corridor is single-file. If nobody holds it, hand it to the fleet
        // aircraft that has been waiting for it longest (fleet order breaks ties),
        // so a preempted aircraft does not always jump back ahead of one that has
        // been holding short.
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

        private bool CanLeavePhase(AircraftPhase phase)
        {
            return phase != AircraftPhase.AtStand || ActiveTurnaround == null || ActiveTurnaround.IsComplete(_lastUpdatedAt);
        }

        private void StartCycle()
        {
            var aircraftId = new StableId($"AS-{CompletedCycles + 101:000}");
            var standIndex = _random.NextInt(0, Capacity.StandCount);
            AssignedStand = StandAt(standIndex);
            ActiveTaxiRoute = TaxiNetwork.RouteTo(AssignedStand);
            ActiveAircraft = new AircraftOperation(aircraftId.Value, _cycleStartedAt);
            ActiveTurnaround = null;
            _flightSettled = false;
            Record(_cycleStartedAt, "Flight inbound", $"Assigned {AssignedStand.Value}");
        }

        public static StableId StandAt(int index) => index switch
        {
            0 => StandOne,
            1 => StandTwo,
            2 => StandThree,
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

        /// <summary>
        /// The alternate stand a ground-traffic arrival should use while the primary
        /// flight holds <paramref name="primaryStand"/>. With two stands this is the
        /// historical other-stand rule (seed-identical). With three, the lowest-index
        /// free stand.
        /// </summary>
        public static StableId AlternateStand(StableId primaryStand, int standCount)
        {
            if (standCount < 3)
                return primaryStand.Equals(StandOne) ? StandTwo : StandOne;

            if (!primaryStand.Equals(StandOne)) return StandOne;
            if (!primaryStand.Equals(StandTwo)) return StandTwo;
            return StandThree;
        }

        private void SynchronizeReservations()
        {
            var owner = new StableId(ActiveAircraft.AircraftId);
            if (!_reservations.TryReplace(owner, RequiredResources(), out var blocked))
            {
                ReservationConflicts++;
                TrafficWaits.SetWaiting(owner, blocked, _lastUpdatedAt);
            }
            else
            {
                TrafficWaits.Clear(owner);
            }
        }

        private IEnumerable<StableId> RequiredResources()
        {
            switch (ActiveAircraft.Phase)
            {
                case AircraftPhase.Landing:
                case AircraftPhase.Takeoff:
                    yield return Runway;
                    break;
                case AircraftPhase.TaxiIn:
                    yield return SegmentFor(ActiveAircraft.Phase, ActiveAircraft.PhaseProgress(_lastUpdatedAt));
                    yield return AssignedStand;
                    break;
                case AircraftPhase.AtStand:
                    yield return AssignedStand;
                    break;
                case AircraftPhase.Pushback:
                    yield return AssignedStand;
                    yield return ApronLane;
                    break;
                case AircraftPhase.TaxiOut:
                    yield return SegmentFor(ActiveAircraft.Phase, ActiveAircraft.PhaseProgress(_lastUpdatedAt));
                    break;
            }
        }

        private StableId SegmentFor(AircraftPhase phase, double progress)
        {
            if (phase != AircraftPhase.TaxiIn && phase != AircraftPhase.TaxiOut)
                return default;
            var count = ActiveTaxiRoute.SegmentIds.Count;
            var index = Math.Min(count - 1, (int)(Math.Max(0, Math.Min(0.999999, progress)) * count));
            if (phase == AircraftPhase.TaxiOut)
                index = count - 1 - index;
            return ActiveTaxiRoute.SegmentIds[index];
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

        private void Record(SimulationTime time, string title, string detail)
        {
            EventLog.Add(new OperationalEvent(time, ActiveAircraft?.AircraftId ?? string.Empty, title, detail));
        }
    }
}
