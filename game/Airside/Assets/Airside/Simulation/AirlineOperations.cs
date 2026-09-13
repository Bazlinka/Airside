using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>Outcome of a player command, with a reason the HUD can show when refused.</summary>
    public readonly struct CommandResult
    {
        private CommandResult(bool accepted, string reason)
        {
            Accepted = accepted;
            Reason = reason;
        }

        public bool Accepted { get; }
        public string Reason { get; }

        public static CommandResult Ok => new(true, string.Empty);
        public static CommandResult Refused(string reason) => new(false, reason);
    }

    /// <summary>A state change worth telling the player about.</summary>
    public readonly struct FleetEvent
    {
        public FleetEvent(SimulationTime at, FleetAircraft aircraft, FleetState state)
        {
            At = at;
            Aircraft = aircraft;
            State = state;
        }

        public SimulationTime At { get; }
        public FleetAircraft Aircraft { get; }
        public FleetState State { get; }
    }

    /// <summary>
    /// Airlines, their fleets and the tower at the home airport (ADR 0045).
    ///
    /// Event-driven: time jumps straight from one state change to the next, so a
    /// two-hour flight costs a handful of steps and any mix of frame sizes, time
    /// rates or skip-to-next-event lands on exactly the same state.
    ///
    /// The airport runs itself. The tower owns the single runway — arrivals before
    /// departures, one movement at a time with wake separation. Owners only choose
    /// where and when an aircraft goes, and which stand it parks on. AI airlines
    /// make both choices automatically.
    /// </summary>
    public sealed class AirlineOperations
    {
        public const long TaxiOutSeconds = 7 * 60;
        public const long TaxiInSeconds = 5 * 60;
        public const long DestinationTurnaroundSeconds = 40 * 60;
        public const long AiStandTurnaroundSeconds = 45 * 60;
        public const long RunwaySeparationSeconds = 90;
        public const int MaxRecentEvents = 30;

        /// <summary>Entering the runway at the holding point and backtracking to the takeoff position.</summary>
        public const long LineupSeconds = 60;

        /// <summary>Backtracking from the rollout end and clearing the runway at the exit.</summary>
        public const long VacateSeconds = 90;

        /// <summary>Runway time for lineup, the takeoff roll and initial climb, from the flown circuit.</summary>
        public static long TakeoffRunwaySeconds => LineupSeconds + CircuitProfile.TakeoffSeconds;

        /// <summary>
        /// Runway time from the landing clearance on long final through flare, rollout
        /// and vacating — the whole of it is drawn in 3D, so it is flown in full.
        /// </summary>
        public static long LandingRunwaySeconds =>
            CircuitProfile.ApproachSeconds + CircuitProfile.LandingSeconds + VacateSeconds;

        public static readonly IReadOnlyList<StableId> AdelaideRegionalBays = new[]
        {
            new StableId("BAY-1"), new StableId("BAY-2"), new StableId("BAY-3"), new StableId("BAY-4")
        };

        private readonly ISimulationClock _clock;
        private readonly IRandomSource _random;
        private readonly List<Airline> _airlines = new();
        private readonly List<FleetAircraft> _fleet = new();
        private readonly List<StableId> _stands;
        private readonly List<FleetEvent> _recentEvents = new();
        private SimulationTime _processedTo;
        private SimulationTime _runwayFreeAt;

        public AirlineOperations(ISimulationClock clock, IRandomSource random, Destination home, IReadOnlyList<StableId> stands)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            if (stands == null || stands.Count == 0)
                throw new ArgumentException("At least one stand is required.", nameof(stands));

            Home = home;
            _stands = new List<StableId>(stands);
            _processedTo = clock.Now;
            _runwayFreeAt = clock.Now;
        }

        /// <summary>
        /// The ADR 0045 starting position at Adelaide: the player's airline with one
        /// ATR, and Emu Air with two.
        /// </summary>
        public static AirlineOperations StartAtAdelaide(ISimulationClock clock, IRandomSource random, Airline player)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (!player.IsPlayer) throw new ArgumentException("The starting airline must be the player's.", nameof(player));

            var operations = new AirlineOperations(clock, random, DestinationCatalogue.Adelaide, AdelaideRegionalBays);
            var emu = Airline.EmuAir();
            operations.AddAirline(player);
            operations.AddAirline(emu);
            operations.AddAircraft(player, "VH-PAX", AircraftType.Atr42, AdelaideRegionalBays[0]);
            operations.AddAircraft(emu, "VH-EMA", AircraftType.Atr42, AdelaideRegionalBays[1]);
            operations.AddAircraft(emu, "VH-EMB", AircraftType.Atr42, AdelaideRegionalBays[2]);
            return operations;
        }

        public Destination Home { get; }
        public IReadOnlyList<Airline> Airlines => _airlines;
        public IReadOnlyList<FleetAircraft> Fleet => _fleet;
        public IReadOnlyList<StableId> Stands => _stands;

        /// <summary>Newest last, capped at <see cref="MaxRecentEvents"/>.</summary>
        public IReadOnlyList<FleetEvent> RecentEvents => _recentEvents;

        /// <summary>Events emitted since start, so a reader can tell which recent ones are new.</summary>
        public long TotalEvents { get; private set; }

        public Airline PlayerAirline => _airlines.Find(a => a.IsPlayer);

        public IEnumerable<FleetAircraft> FleetOf(Airline airline)
        {
            foreach (var aircraft in _fleet)
                if (ReferenceEquals(aircraft.Airline, airline))
                    yield return aircraft;
        }

        public void AddAirline(Airline airline)
        {
            if (airline == null) throw new ArgumentNullException(nameof(airline));
            foreach (var existing in _airlines)
            {
                if (existing.Id.Equals(airline.Id))
                    throw new InvalidOperationException($"Airline {airline.Id} already operates here.");
                if (existing.IsPlayer && airline.IsPlayer)
                    throw new InvalidOperationException("Only one player airline is allowed.");
            }

            _airlines.Add(airline);
        }

        public FleetAircraft AddAircraft(Airline airline, string registration, AircraftType type, StableId stand)
        {
            if (!_airlines.Contains(airline))
                throw new InvalidOperationException("Add the airline before its aircraft.");
            if (_fleet.Exists(a => string.Equals(a.Registration, registration, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"{registration} is already registered.");
            if (!IsStandFree(stand))
                throw new InvalidOperationException($"{stand} is not free.");

            var aircraft = new FleetAircraft(registration, airline, type, stand, _processedTo);
            _fleet.Add(aircraft);
            if (!airline.IsPlayer)
                ScheduleAiDeparture(aircraft, _processedTo);
            return aircraft;
        }

        // ---- Queries -------------------------------------------------------------

        public double DistanceKm(Destination destination) => Home.DistanceKmTo(destination);

        public bool CanReach(FleetAircraft aircraft, Destination destination) =>
            !destination.Equals(Home) && aircraft.Type.CanReach(DistanceKm(destination));

        /// <summary>Every catalogue destination except home, reachable or not, for the map.</summary>
        public IEnumerable<Destination> MapDestinations()
        {
            foreach (var destination in DestinationCatalogue.Australia)
                if (!destination.Equals(Home))
                    yield return destination;
        }

        public long AirborneSeconds(FleetAircraft aircraft, Destination destination) =>
            LegTiming.AirborneSeconds(DistanceKm(destination), aircraft.Type);

        public bool IsStandFree(StableId stand)
        {
            if (!_stands.Contains(stand))
                return false;
            foreach (var aircraft in _fleet)
                if (HoldsStand(aircraft) && aircraft.Stand.Equals(stand))
                    return false;
            return true;
        }

        public IEnumerable<StableId> FreeStands()
        {
            foreach (var stand in _stands)
                if (IsStandFree(stand))
                    yield return stand;
        }

        /// <summary>
        /// The next moment anything changes on its own, for skip-to-next-event. Null when
        /// everything is waiting on an owner — such as a player aircraft needing a stand.
        /// </summary>
        public SimulationTime? NextEventAt()
        {
            SimulationTime? next = null;
            var now = _processedTo;

            void Consider(SimulationTime candidate)
            {
                if (candidate.CompareTo(now) > 0 && (next == null || candidate.CompareTo(next.Value) < 0))
                    next = candidate;
            }

            var runwayWanted = false;
            foreach (var aircraft in _fleet)
            {
                if (aircraft.StateEndsAt.HasValue)
                    Consider(aircraft.StateEndsAt.Value);
                if (aircraft.State == FleetState.AtStand && aircraft.Scheduled.HasValue)
                    Consider(aircraft.Scheduled.Value.DepartAt);
                if (aircraft.State is FleetState.HoldingShort or FleetState.HoldingForLanding)
                    runwayWanted = true;
            }

            if (runwayWanted)
                Consider(_runwayFreeAt);

            return next;
        }

        // ---- Owner commands ------------------------------------------------------

        public CommandResult ScheduleDeparture(FleetAircraft aircraft, Destination destination, SimulationTime departAt)
        {
            if (aircraft == null || !_fleet.Contains(aircraft))
                return CommandResult.Refused("Unknown aircraft.");
            if (aircraft.State != FleetState.AtStand)
                return CommandResult.Refused($"{aircraft.Registration} is not parked on a stand.");
            if (destination.Equals(Home))
                return CommandResult.Refused($"{aircraft.Registration} is already at {Home.Name}.");
            if (!CanReach(aircraft, destination))
                return CommandResult.Refused(
                    $"{destination.Name} is {DistanceKm(destination):0} km — beyond the {aircraft.Type.Name}'s {aircraft.Type.PracticalRangeKm:0} km range.");
            if (departAt.CompareTo(_processedTo) < 0)
                return CommandResult.Refused("Departure time is in the past.");

            aircraft.Scheduled = new ScheduledDeparture(destination, departAt);
            return CommandResult.Ok;
        }

        public CommandResult CancelDeparture(FleetAircraft aircraft)
        {
            if (aircraft == null || !_fleet.Contains(aircraft))
                return CommandResult.Refused("Unknown aircraft.");
            if (aircraft.State != FleetState.AtStand || !aircraft.Scheduled.HasValue)
                return CommandResult.Refused($"{aircraft.Registration} has no departure waiting to start.");

            aircraft.Scheduled = null;
            return CommandResult.Ok;
        }

        public CommandResult AssignStand(FleetAircraft aircraft, StableId stand)
        {
            if (aircraft == null || !_fleet.Contains(aircraft))
                return CommandResult.Refused("Unknown aircraft.");
            if (aircraft.State != FleetState.AwaitingStand)
                return CommandResult.Refused($"{aircraft.Registration} is not waiting for a stand.");
            if (!_stands.Contains(stand))
                return CommandResult.Refused($"{stand} is not a stand here.");
            if (!IsStandFree(stand))
                return CommandResult.Refused($"{stand} is occupied.");

            aircraft.Stand = stand;
            Transition(aircraft, FleetState.TaxiIn, _processedTo, TaxiInSeconds);
            return CommandResult.Ok;
        }

        // ---- Time ----------------------------------------------------------------

        public void Update()
        {
            var target = _clock.Now;
            if (target.CompareTo(_processedTo) < 0)
                throw new InvalidOperationException("Simulation time cannot move backwards.");

            while (true)
            {
                ProcessDue(_processedTo);
                var next = NextEventAt();
                if (next == null || next.Value.CompareTo(target) > 0)
                    break;
                _processedTo = next.Value;
            }

            _processedTo = target;
            ProcessDue(_processedTo);
        }

        /// <summary>Resolve everything due at <paramref name="now"/> until nothing else changes.</summary>
        private void ProcessDue(SimulationTime now)
        {
            bool changed;
            do
            {
                changed = false;
                foreach (var aircraft in _fleet)
                    changed |= AdvanceAircraft(aircraft, now);
                changed |= RunTower(now);
            } while (changed);
        }

        private bool AdvanceAircraft(FleetAircraft aircraft, SimulationTime now)
        {
            if (aircraft.StateEndsAt.HasValue && aircraft.StateEndsAt.Value.CompareTo(now) > 0)
                return false;

            switch (aircraft.State)
            {
                case FleetState.AtStand:
                    if (!aircraft.Scheduled.HasValue || aircraft.Scheduled.Value.DepartAt.CompareTo(now) > 0)
                        return false;
                    aircraft.CurrentDestination = aircraft.Scheduled.Value.Destination;
                    aircraft.Scheduled = null;
                    aircraft.Stand = default;
                    Transition(aircraft, FleetState.TaxiOut, now, TaxiOutSeconds);
                    return true;

                case FleetState.TaxiOut:
                    Transition(aircraft, FleetState.HoldingShort, now, null);
                    return true;

                case FleetState.TakingOff:
                    Transition(aircraft, FleetState.Outbound, now, LegAirborne(aircraft));
                    return true;

                case FleetState.Outbound:
                    Transition(aircraft, FleetState.AtDestination, now, DestinationTurnaroundSeconds);
                    return true;

                case FleetState.AtDestination:
                    Transition(aircraft, FleetState.Inbound, now, LegAirborne(aircraft));
                    return true;

                case FleetState.Inbound:
                    Transition(aircraft, FleetState.HoldingForLanding, now, null);
                    return true;

                case FleetState.Landing:
                    Transition(aircraft, FleetState.AwaitingStand, now, null);
                    return true;

                case FleetState.AwaitingStand:
                    if (aircraft.Airline.IsPlayer)
                        return false;
                    foreach (var stand in _stands)
                    {
                        if (!IsStandFree(stand))
                            continue;
                        aircraft.Stand = stand;
                        Transition(aircraft, FleetState.TaxiIn, now, TaxiInSeconds);
                        return true;
                    }
                    return false;

                case FleetState.TaxiIn:
                    aircraft.CompletedTrips++;
                    aircraft.CurrentDestination = null;
                    Transition(aircraft, FleetState.AtStand, now, null);
                    if (!aircraft.Airline.IsPlayer)
                        ScheduleAiDeparture(aircraft, now);
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>One runway movement at a time; arrivals first, then longest-waiting.</summary>
        private bool RunTower(SimulationTime now)
        {
            if (_runwayFreeAt.CompareTo(now) > 0)
                return false;

            var next = LongestWaiting(FleetState.HoldingForLanding) ?? LongestWaiting(FleetState.HoldingShort);
            if (next == null)
                return false;

            var landing = next.State == FleetState.HoldingForLanding;
            var runwaySeconds = landing ? LandingRunwaySeconds : TakeoffRunwaySeconds;
            Transition(next, landing ? FleetState.Landing : FleetState.TakingOff, now, runwaySeconds);
            _runwayFreeAt = now.Advance(runwaySeconds + RunwaySeparationSeconds);
            return true;
        }

        private FleetAircraft LongestWaiting(FleetState state)
        {
            FleetAircraft best = null;
            foreach (var aircraft in _fleet)
            {
                if (aircraft.State != state)
                    continue;
                if (best == null || aircraft.StateStartedAt.CompareTo(best.StateStartedAt) < 0)
                    best = aircraft;
            }

            return best;
        }

        private long LegAirborne(FleetAircraft aircraft) =>
            aircraft.CurrentDestination.HasValue ? AirborneSeconds(aircraft, aircraft.CurrentDestination.Value) : 0;

        private static bool HoldsStand(FleetAircraft aircraft) =>
            aircraft.State is FleetState.AtStand or FleetState.TaxiIn;

        private void ScheduleAiDeparture(FleetAircraft aircraft, SimulationTime now)
        {
            var reachable = new List<Destination>();
            foreach (var destination in MapDestinations())
                if (CanReach(aircraft, destination))
                    reachable.Add(destination);
            if (reachable.Count == 0)
                return;

            var pick = reachable[_random.NextInt(0, reachable.Count)];
            aircraft.Scheduled = new ScheduledDeparture(pick, now.Advance(AiStandTurnaroundSeconds));
        }

        private void Transition(FleetAircraft aircraft, FleetState state, SimulationTime now, long? durationSeconds)
        {
            aircraft.Enter(state, now, durationSeconds);
            _recentEvents.Add(new FleetEvent(now, aircraft, state));
            TotalEvents++;
            if (_recentEvents.Count > MaxRecentEvents)
                _recentEvents.RemoveAt(0);
        }
    }
}
