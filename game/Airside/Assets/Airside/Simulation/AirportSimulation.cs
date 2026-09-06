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
        public const int MaxConcurrentCommercialFlights = 2;

        public static readonly StableId Runway = new("RUNWAY-09-27");
        public static readonly StableId ApronLane = new("APRON-LANE");
        public static readonly StableId StandOne = new("STAND-1");
        public static readonly StableId StandTwo = new("STAND-2");
        public static readonly StableId StandThree = new("STAND-3");

        /// <summary>
        /// Physical stand count until a buildable capacity upgrade lands. Used to
        /// gate route acceptance against schedule density.
        /// </summary>
        public const int StandCount = AirportRoutes.BaselineStandCount;

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
                aircraft.Reposition(_clock.Now, TrafficWaits, CommercialStandOccupancy(), mayEnterCorridor: true);
        }

        public AirportLocation Location { get; }
        public DayCycle TimeOfDay => new(_clock.Now);
        public IReadOnlyList<CommercialFlight> Flights => _flights;

        /// <summary>Migration accessor for the first commercial flight's operation.</summary>
        public AircraftOperation ActiveAircraft => Primary.Operation;

        /// <summary>Migration accessor for the first commercial flight's stand.</summary>
        public StableId AssignedStand => Primary.AssignedStand;

        public int CompletedCycles { get; private set; }
        public int ReservationConflicts { get; private set; }
        public TurnaroundWorkflow ActiveTurnaround => Primary.Turnaround;
        public AirportEconomy Economy { get; }
        public AirportRoutes Routes { get; }
        public AirportReputation Reputation { get; }
        public AirportStaffing Staffing { get; }
        public AirportResearch Research { get; }
        public AirportCapacity Capacity { get; }
        public AirportDailyReports DailyReports { get; }
        public bool IsInsolvent => Economy.IsInsolvent;
        public AirportTaxiNetwork TaxiNetwork { get; }
        public TaxiRoute ActiveTaxiRoute => Primary.TaxiRoute;
        public OperationalEventLog EventLog { get; }
        public TrafficWaitMonitor TrafficWaits { get; }
        public IReadOnlyList<GroundTrafficAircraft> GroundTraffic => _groundTraffic;
        public StableId CurrentTaxiSegment => Primary.SegmentFor(_clock.Now);
        public long LastDelaySeconds { get; private set; }
        public string LastDelayCause { get; private set; } = string.Empty;
        public long CurrentDelaySeconds => Primary.Operation.Phase == AircraftPhase.AtStand && Primary.Turnaround != null
            ? Primary.Turnaround.DelaySeconds(_clock.Now)
            : 0;
        public string CurrentDelayCause => Primary.Operation.Phase == AircraftPhase.AtStand && Primary.Turnaround != null
            ? Primary.Turnaround.DelayCause(_clock.Now)
            : string.Empty;
        public SimulationTime CycleStartedAt => Primary.CycleStartedAt;
        public ReservationTable Reservations => _reservations;
        public int MaxScheduledFlightsPerDay => AirportRoutes.MaxScheduledFlightsPerDay(Capacity.StandCount);

        private CommercialFlight Primary => _flights[0];

        
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
            if (!Research.StartOperationsEfficiency(_lastUpdatedAt))
                return false;

            Record(_lastUpdatedAt, "Research started",
                $"{AirportResearch.OperationsEfficiencyName} · {AirportResearch.OperationsEfficiencyDurationSeconds}s · -${AirportResearch.OperationsEfficiencyCost:N0}");
            return true;
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
            if (Primary.Operation.Phase != AircraftPhase.AtStand || Primary.Turnaround == null || Primary.Turnaround.PriorityCrewEnabled)
                return false;
            if (!Economy.PurchasePriorityCrew())
                return false;

            Primary.Turnaround.EnablePriorityCrew();
            Record(_lastUpdatedAt, Primary.AircraftId, "Priority crew assigned", "$300 schedule recovery decision");
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
                Record(now, "Research complete",
                    $"{AirportResearch.OperationsEfficiencyName} · daily running cost -${AirportResearch.OperationsEfficiencyDailyDiscount:N0}");
            SettleDaysUpTo(now);
            TrySpawnSecondCommercial(now);

            var active = _flights.ToArray();
            foreach (var flight in active)
                AdvanceCommercial(flight, now);

            SynchronizeAllTraffic(now);
        }

        private void AdvanceCommercial(CommercialFlight flight, SimulationTime now)
        {
            if (flight.Operation.IsComplete)
            {
                if (now.CompareTo(flight.Operation.PhaseStartedAt.Advance(DepartureResetSeconds)) >= 0)
                    TryRespawnCommercial(flight, now);
                return;
            }

            var previousPhase = flight.Operation.Phase;
            flight.Operation.AdvanceTo(now, phase => CanLeavePhase(flight, phase));

            if (previousPhase == flight.Operation.Phase)
                return;

            if (flight.Operation.Phase == AircraftPhase.AtStand)
            {
                flight.Turnaround = new TurnaroundWorkflow(
                    flight.Operation.PhaseStartedAt, _random.NextInt(0, 3) == 0, Staffing.TurnaroundSpeedFactor);
                Record(now, flight.AircraftId, "On stand", $"Arrived at {flight.AssignedStand.Value}");
            }

            if (previousPhase == AircraftPhase.AtStand && flight.Turnaround != null)
            {
                flight.LastDelaySeconds = flight.Turnaround.DelaySeconds(now);
                flight.LastDelayCause = flight.LastDelaySeconds > 0 && flight.Turnaround.HasCleaningDisruption
                    ? "Cabin cleaning disruption"
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
                if (Routes.IncomePerFlight > 0)
                {
                    Economy.AddRouteIncome(Routes.IncomePerFlight);
                    Record(now, flight.AircraftId, "Route income",
                        $"+${Routes.IncomePerFlight:N0} from {Routes.Accepted.Count} scheduled route(s)");
                }

                flight.FlightSettled = true;
                Record(now, flight.AircraftId, "Departed",
                    $"Net flight result ${AirportEconomy.TurnaroundRevenue - flight.LastDelaySeconds * AirportEconomy.DelayCostPerSecond:N0}");
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
            CompletedCycles++;

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

        
        
        public static StableId AlternateStand(StableId primaryStand, int standCount)
        {
            if (standCount < 3)
                return primaryStand.Equals(StandOne) ? StandTwo : StandOne;
            if (!primaryStand.Equals(StandOne)) return StandOne;
            if (!primaryStand.Equals(StandTwo)) return StandTwo;
            return StandThree;
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
                var taken = false;
                foreach (var busy in occupied)
                {
                    if (busy.Equals(candidate))
                    {
                        taken = true;
                        break;
                    }
                }

                if (!taken)
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
            // Commercials have priority: every ground-traffic aircraft releases any
            // resource any commercial needs this tick, commercials then take their
            // reservations in SpawnedAt FIFO order, and the fleet moves into whatever
            // space is left. Fleet aircraft queue through the shared corridor lock.
            var required = new List<StableId>();
            foreach (var flight in _flights)
            {
                if (flight.Operation.IsComplete)
                    continue;
                foreach (var resource in flight.RequiredResources(now))
                    required.Add(resource);
            }

            foreach (var aircraft in _groundTraffic)
                aircraft.Yield(required);

            SynchronizeCommercialReservations();

            var grantee = ChooseCorridorGrantee(now);
            var occupied = CommercialStandOccupancy();
            foreach (var aircraft in _groundTraffic)
                aircraft.Reposition(now, TrafficWaits, occupied,
                    mayEnterCorridor: aircraft.OnCorridor || ReferenceEquals(aircraft, grantee));
        }

        private void SynchronizeCommercialReservations()
        {
            foreach (var flight in _flights)
            {
                if (flight.Operation.IsComplete)
                {
                    TrafficWaits.Clear(flight.OwnerId);
                    continue;
                }

                var owner = flight.OwnerId;
                if (!_reservations.TryReplace(owner, flight.RequiredResources(_lastUpdatedAt), out var blocked))
                {
                    if (!IsCommercialOwner(blocked))
                        ReservationConflicts++;
                    TrafficWaits.SetWaiting(owner, blocked, _lastUpdatedAt);
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

            foreach (var flight in _flights)
            {
                if (flight.OwnerId.Equals(owner))
                    return true;
            }

            return false;
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

        private bool CanLeavePhase(CommercialFlight flight, AircraftPhase phase)
        {
            return phase != AircraftPhase.AtStand || flight.Turnaround == null || flight.Turnaround.IsComplete(_lastUpdatedAt);
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
            Record(time, Primary?.AircraftId ?? string.Empty, title, detail);

        private void Record(SimulationTime time, string flightId, string title, string detail)
        {
            EventLog.Add(new OperationalEvent(time, flightId ?? string.Empty, title, detail));
        }
    }
}
