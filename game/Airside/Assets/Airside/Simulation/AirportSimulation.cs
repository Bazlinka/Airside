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

        private readonly ISimulationClock _clock;
        private readonly IRandomSource _random;
        private readonly ReservationTable _reservations;
        private SimulationTime _cycleStartedAt;
        private SimulationTime _lastUpdatedAt;
        private bool _flightSettled;
        private int _daysSettled;
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
            _groundTraffic = new[]
            {
                new GroundTrafficAircraft(new StableId("GT-201"), _reservations, GroundTrafficRole.ArriveDepart, 0),
                new GroundTrafficAircraft(new StableId("GT-202"), _reservations, GroundTrafficRole.Reposition, 25)
            };
            StartCycle();
            SynchronizeReservations();
            foreach (var aircraft in _groundTraffic)
                aircraft.Reposition(_clock.Now, TrafficWaits, AssignedStand, mayEnterCorridor: true);
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
        public AirportTaxiNetwork TaxiNetwork { get; }
        public TaxiRoute ActiveTaxiRoute { get; private set; }
        public OperationalEventLog EventLog { get; }
        public TrafficWaitMonitor TrafficWaits { get; }
        public IReadOnlyList<GroundTrafficAircraft> GroundTraffic => _groundTraffic;
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

        /// <summary>
        /// Expected daily income and cost under the current weather, staffing and
        /// route book, assuming today's flight cadence continues with no delays.
        /// </summary>
        public DailyFinanceBrief DailyFinance
        {
            get
            {
                var operatingCost = BaseDailyOperatingCost
                    + Weather.DailyOperatingCost(CurrentWeather)
                    + Staffing.DailyWage;
                var incomePerCycle = AirportEconomy.TurnaroundRevenue + Routes.IncomePerFlight;
                var expectedIncome = incomePerCycle * DayCycle.DaySeconds / CycleLengthSeconds;
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
            var proposal = Routes.Pending;
            if (proposal == null || !Routes.Decline())
                return false;

            Record(_lastUpdatedAt, "Route declined", $"{proposal.Airline} to {proposal.Destination}");
            return true;
        }

        public bool HireGroundCrew()
        {
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
            if (!Staffing.Release())
                return false;

            Record(_lastUpdatedAt, "Crew released",
                $"Ground crew now {Staffing.GroundCrew} · payroll ${Staffing.DailyWage:N0}/day");
            return true;
        }

        private void AdvanceOneSecond(SimulationTime now)
        {
            Routes.Update(now);
            SettleDaysUpTo(now);

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
                var cost = BaseDailyOperatingCost + Weather.DailyOperatingCost(weather) + Staffing.DailyWage;
                Economy.PayOperatingCosts(cost);
                Record(now, $"Day {_daysSettled} closed",
                    $"Running cost -${cost:N0} ({Weather.Describe(weather)}, {Staffing.GroundCrew} crew) · cash ${Economy.Cash:N0}");
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
                aircraft.Reposition(now, TrafficWaits, AssignedStand,
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
            AssignedStand = _random.NextInt(0, 2) == 0 ? StandOne : StandTwo;
            ActiveTaxiRoute = TaxiNetwork.RouteTo(AssignedStand);
            ActiveAircraft = new AircraftOperation(aircraftId.Value, _cycleStartedAt);
            ActiveTurnaround = null;
            _flightSettled = false;
            Record(_cycleStartedAt, "Flight inbound", $"Assigned {AssignedStand.Value}");
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

        private void Record(SimulationTime time, string title, string detail)
        {
            EventLog.Add(new OperationalEvent(time, ActiveAircraft?.AircraftId ?? string.Empty, title, detail));
        }
    }
}
