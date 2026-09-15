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
        public const long DestinationTurnaroundSeconds = 40 * 60;
        public const long AiStandTurnaroundSeconds = 45 * 60;
        public const long RunwaySeparationSeconds = 90;
        public const int MaxRecentEvents = 30;

        // Ground times are measured off the real Adelaide routes with ATR speed limits
        // (AdelaideGround), never picked: a taxi takes as long as driving it takes.

        /// <summary>Pushback, tug disconnect and taxi from <paramref name="stand"/> to the runway 05 holding point.</summary>
        public static long TaxiOutSecondsFrom(StableId stand) => AdelaideGround.TaxiOut(stand).WholeSeconds;

        /// <summary>Taxi from the E2 holding point into <paramref name="stand"/>.</summary>
        public static long TaxiInSecondsTo(StableId stand) => AdelaideGround.TaxiIn(stand).WholeSeconds;

        /// <summary>Holding point onto the centreline at the 05 threshold.</summary>
        public static long LineupSeconds => AdelaideGround.Lineup.WholeSeconds;

        /// <summary>Rollout end, along the runway to exit E2 and clear to its holding point.</summary>
        public static long VacateSeconds => AdelaideGround.Vacate.WholeSeconds;

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
            new StableId("BAY-1"), new StableId("BAY-2"), new StableId("BAY-3"), new StableId("BAY-4"),
            new StableId("BAY-5"), new StableId("BAY-6")
        };

        /// <summary>Terminal gates (ADR 0047): jets only, a separate stand system from the regional bays.</summary>
        public static readonly IReadOnlyList<StableId> AdelaideTerminalGates = new[] { new StableId("GATE-13") };

        /// <summary>Every stand at Adelaide: the regional bays, then the terminal gates.</summary>
        public static readonly IReadOnlyList<StableId> AdelaideStands = new List<StableId>(AdelaideRegionalBays)
        {
            AdelaideTerminalGates[0]
        };

        /// <summary>
        /// The fictional terminal jet operator and its aircraft, each tied to its gate. New games
        /// start with it; older saves gain it on load (<see cref="AddMissingTerminalOperators"/>).
        /// </summary>
        public static readonly IReadOnlyList<(Func<Airline> Make, (string Registration, AircraftType Type, StableId Gate)[] Fleet)> TerminalOperators = new (Func<Airline>, (string, AircraftType, StableId)[])[]
        {
            (Airline.WattlebirdJet, new[] { ("VH-WTJ", AircraftType.Boeing7378, new StableId("GATE-13")) })
        };

        /// <summary>
        /// Wattlebird Jet's mainland rotation, flown in order by completed trips — Melbourne and
        /// Sydney most, then Brisbane, Perth and Canberra. Deterministic and drawn from no random
        /// numbers, so adding the jet leaves the regional carriers' random sequence untouched.
        /// </summary>
        public static readonly IReadOnlyList<string> WattlebirdRotation = new[] { "MEL", "SYD", "MEL", "BNE", "SYD", "PER", "MEL", "CBR" };

        /// <summary>Jets use terminal gates; turboprops use the regional bays. Never the other way.</summary>
        public static bool NeedsTerminalGate(AircraftType type) =>
            AircraftCatalogue.TryFor(type, out var spec) && spec.StandClass == StandClass.TerminalGate;

        public static bool StandFits(AircraftType type, StableId stand) =>
            AdelaideGround.IsTerminalGate(stand) == NeedsTerminalGate(type);

        /// <summary>
        /// Real regional carriers that share Adelaide's regional apron with the player and
        /// Emu Air. New games start with them; older saves gain them on load
        /// (<see cref="AddMissingRegionalCarriers"/>). Six aircraft in all on six bays, so
        /// everyone always has a stand.
        /// </summary>
        public static readonly IReadOnlyList<(Func<Airline> Make, (string Registration, AircraftType Type)[] Fleet)> RegionalCarriers = new (Func<Airline>, (string, AircraftType)[])[]
        {
            (Airline.Rex, new[] { ("VH-ZRC", AircraftType.Saab340), ("VH-ZRD", AircraftType.Saab340) }),
            (Airline.QantasLink, new[] { ("VH-QOK", AircraftType.Dash8Q400) })
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

        /// <summary>First Emu Air departures after a new game starts, so the field is not empty for 45 real minutes.</summary>
        public static readonly long[] AiOpeningDepartureSeconds = { 10 * 60, 20 * 60, 32 * 60, 45 * 60, 58 * 60 };

        /// <summary>
        /// The ADR 0045 starting position at Adelaide: the player's airline with one
        /// ATR, and Emu Air with two whose first flights leave within twenty minutes.
        /// </summary>
        public static AirlineOperations StartAtAdelaide(ISimulationClock clock, IRandomSource random, Airline player,
            AirlineClock airlineClock = null)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (!player.IsPlayer) throw new ArgumentException("The starting airline must be the player's.", nameof(player));

            var operations = new AirlineOperations(clock, random, DestinationCatalogue.Adelaide, AdelaideStands);
            var emu = Airline.EmuAir();
            operations.AddAirline(player);
            operations.AddAirline(emu);
            operations.AddAircraft(player, "VH-PAX", AircraftType.Atr42, AdelaideRegionalBays[0]);
            var aiFleet = new List<FleetAircraft>
            {
                operations.AddAircraft(emu, "VH-EMA", AircraftType.Atr42, AdelaideRegionalBays[1]),
                operations.AddAircraft(emu, "VH-EMB", AircraftType.Atr42, AdelaideRegionalBays[2])
            };
            operations.AddMissingRegionalCarriers(aiFleet);
            // Stagger the first departures so a new game sees a movement every ~12 minutes.
            for (var i = 0; i < aiFleet.Count && i < AiOpeningDepartureSeconds.Length; i++)
            {
                if (aiFleet[i].Scheduled is { } first)
                    aiFleet[i].Scheduled = new ScheduledDeparture(first.Destination,
                        operations.ProcessedTo.Advance(AiOpeningDepartureSeconds[i]));
            }

            // The terminal jet joins after the regional openings are fixed, so they are unchanged.
            operations.AddMissingTerminalOperators();

            operations.Clock = airlineClock ?? AirlineClock.Default;
            return operations;
        }

        /// <summary>
        /// Add any <see cref="RegionalCarriers"/> airline that is not flying here yet, parking
        /// each of its aircraft on a free stand (skipped if none is free) with an AI departure
        /// booked. Returns how many aircraft joined. Safe to call on every load.
        /// </summary>
        public int AddMissingRegionalCarriers(List<FleetAircraft> added = null)
        {
            var count = 0;
            foreach (var (make, fleet) in RegionalCarriers)
            {
                var airline = make();
                if (_airlines.Exists(a => a.Id.Equals(airline.Id)))
                    continue;
                AddAirline(airline);
                foreach (var (registration, type) in fleet)
                {
                    if (_fleet.Exists(a => string.Equals(a.Registration, registration, StringComparison.OrdinalIgnoreCase)))
                        continue;
                    StableId? free = null;
                    foreach (var stand in FreeStands())
                    {
                        free = stand;
                        break;
                    }

                    if (!free.HasValue)
                        break;
                    var aircraft = AddAircraft(airline, registration, type, free.Value);
                    added?.Add(aircraft);
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Add any <see cref="TerminalOperators"/> airline and aircraft missing here, parked at its
        /// own gate with its first rotation flight booked. Skipped when this airport has no such
        /// gate or it is taken. Returns how many aircraft joined; safe to call on every load.
        /// </summary>
        public int AddMissingTerminalOperators()
        {
            var count = 0;
            foreach (var (make, fleet) in TerminalOperators)
            {
                var template = make();
                var airline = _airlines.Find(a => a.Id.Equals(template.Id));
                foreach (var (registration, type, gate) in fleet)
                {
                    if (_fleet.Exists(a => string.Equals(a.Registration, registration, StringComparison.OrdinalIgnoreCase)))
                        continue;
                    if (!_stands.Contains(gate) || !IsStandFree(gate))
                        continue;
                    if (airline == null)
                    {
                        airline = template;
                        AddAirline(airline);
                    }

                    AddAircraft(airline, registration, type, gate);
                    count++;
                }
            }

            return count;
        }

        public Destination Home { get; }

        /// <summary>Maps simulation time to real Adelaide time. Live: one simulated second per real second.</summary>
        public AirlineClock Clock { get; internal set; } = AirlineClock.Default;

        /// <summary>Simulation time everything has been resolved up to.</summary>
        public SimulationTime ProcessedTo => _processedTo;

        /// <summary>When the tower may next clear a runway movement.</summary>
        public SimulationTime RunwayFreeAt => _runwayFreeAt;

        /// <summary>Generator state for saving; 0 when the source is not a <see cref="SeededRandomSource"/>.</summary>
        public uint RandomState => _random is SeededRandomSource seeded ? seeded.State : 0;
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
            if (!StandFits(type, stand))
                throw new InvalidOperationException($"A {type.Name} cannot park on {stand}.");

            var aircraft = new FleetAircraft(registration, airline, type, stand, _processedTo);
            _fleet.Add(aircraft);
            if (!airline.IsPlayer)
                ScheduleAiDeparture(aircraft, _processedTo);
            return aircraft;
        }

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
            aircraft.Restore(state, stateStartedAt, stateEndsAt);
            if (HoldsStand(aircraft) && (!_stands.Contains(stand) || !IsStandFree(stand)))
                throw new FormatException($"{registration} is on stand '{stand}', which is missing, unknown or taken.");
            if (HoldsStand(aircraft) && !StandFits(type, stand))
                throw new FormatException($"{registration} ({type.Name}) cannot be on stand '{stand}'.");

            aircraft.Stand = stand;
            aircraft.DepartureStand = departureStand;
            aircraft.CurrentDestination = currentDestination;
            aircraft.Scheduled = scheduled;
            aircraft.CompletedTrips = Math.Max(0, completedTrips);
            _fleet.Add(aircraft);
        }

        internal void RestoreTower(SimulationTime runwayFreeAt, long totalEvents)
        {
            _runwayFreeAt = runwayFreeAt;
            TotalEvents = Math.Max(0, totalEvents);
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
                if (StandHolder(aircraft, stand))
                    return false;
            return true;
        }

        /// <summary>
        /// Free regional bays — the stands a turboprop (every player aircraft) can use. Terminal
        /// gates are never offered here; see <see cref="FreeStandsFor"/>.
        /// </summary>
        public IEnumerable<StableId> FreeStands()
        {
            foreach (var stand in _stands)
                if (!AdelaideGround.IsTerminalGate(stand) && IsStandFree(stand))
                    yield return stand;
        }

        /// <summary>Free stands this aircraft type may use: terminal gates for jets, bays otherwise.</summary>
        public IEnumerable<StableId> FreeStandsFor(AircraftType type)
        {
            foreach (var stand in _stands)
                if (StandFits(type, stand) && IsStandFree(stand))
                    yield return stand;
        }

        /// <summary>
        /// Who holds a ground resource right now: a stand id, or a gate's
        /// <see cref="AdelaideGround.LeadInResource"/>. Null when free. Derived from aircraft state,
        /// so it is always consistent with a save and with catch-up.
        /// </summary>
        public FleetAircraft GroundResourceHolder(string resource)
        {
            foreach (var aircraft in _fleet)
            {
                if (aircraft.State is FleetState.AtStand or FleetState.TaxiIn && aircraft.Stand.Value == resource)
                    return aircraft;
                if (aircraft.State == FleetState.TaxiOut && AdelaideGround.IsTerminalGate(aircraft.DepartureStand)
                    && aircraft.DepartureStand.Value == resource)
                    return aircraft;
                if (UsesLeadIn(aircraft, out var gate) && AdelaideGround.LeadInResource(gate) == resource)
                    return aircraft;
            }

            return null;
        }

        /// <summary>
        /// Terminal gates stay held through the whole taxi out (the pushback happens on the gate and
        /// its lead-in) and while taxiing in; regional bays keep their original rule.
        /// </summary>
        private static bool StandHolder(FleetAircraft aircraft, StableId stand)
        {
            if (HoldsStand(aircraft) && aircraft.Stand.Equals(stand))
                return true;
            return aircraft.State == FleetState.TaxiOut && aircraft.DepartureStand.Equals(stand)
                   && AdelaideGround.IsTerminalGate(stand);
        }

        /// <summary>A gate's lead-in is in use while an aircraft taxis in to it or out from it.</summary>
        private static bool UsesLeadIn(FleetAircraft aircraft, out StableId gate)
        {
            gate = aircraft.State switch
            {
                FleetState.TaxiIn => aircraft.Stand,
                FleetState.TaxiOut => aircraft.DepartureStand,
                _ => default
            };
            return gate.Value != null && AdelaideGround.IsTerminalGate(gate);
        }

        private bool IsLeadInFree(StableId gate, FleetAircraft except)
        {
            var holder = GroundResourceHolder(AdelaideGround.LeadInResource(gate));
            return holder == null || ReferenceEquals(holder, except);
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
            if (!StandFits(aircraft.Type, stand))
                return CommandResult.Refused($"A {aircraft.Type.Name} cannot use {AdelaideGround.StandLabel(stand)}.");
            if (!IsStandFree(stand))
                return CommandResult.Refused($"{stand} is occupied.");
            if (AdelaideGround.IsTerminalGate(stand) && !IsLeadInFree(stand, aircraft))
                return CommandResult.Refused($"{AdelaideGround.StandLabel(stand)}'s lead-in is in use.");

            aircraft.Stand = stand;
            Transition(aircraft, FleetState.TaxiIn, _processedTo, TaxiInSecondsTo(stand));
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
                    // A gate pushback needs its lead-in clear before the tug moves; it is re-checked
                    // whenever anything else finishes, since that is the only way it frees.
                    if (AdelaideGround.IsTerminalGate(aircraft.Stand) && !IsLeadInFree(aircraft.Stand, aircraft))
                        return false;
                    aircraft.CurrentDestination = aircraft.Scheduled.Value.Destination;
                    aircraft.Scheduled = null;
                    aircraft.DepartureStand = aircraft.Stand;
                    aircraft.Stand = default;
                    Transition(aircraft, FleetState.TaxiOut, now, TaxiOutSecondsFrom(aircraft.DepartureStand));
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
                        if (!StandFits(aircraft.Type, stand) || !IsStandFree(stand))
                            continue;
                        if (AdelaideGround.IsTerminalGate(stand) && !IsLeadInFree(stand, aircraft))
                            continue;
                        aircraft.Stand = stand;
                        Transition(aircraft, FleetState.TaxiIn, now, TaxiInSecondsTo(stand));
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

        /// <summary>Emu Air pushes back no earlier than this Adelaide hour…</summary>
        public const int AiFirstDepartureHour = 6;

        /// <summary>…and no later than this one, like a regional operator's day.</summary>
        public const int AiLastDepartureHour = 21;

        /// <summary>
        /// Emu Air's network, weighted by how often a regional carrier from Adelaide serves
        /// each port: the Eyre Peninsula, Kangaroo Island and Mount Gambier most, the outback
        /// and Melbourne less. Airports not listed are not Emu Air routes.
        /// </summary>
        public static readonly IReadOnlyList<(string Code, int Weight)> AiNetwork = new[]
        {
            ("KGC", 3), ("PLO", 3), ("WYA", 2), ("MGB", 2), ("MEL", 2),
            ("CED", 1), ("CPD", 1), ("MQL", 1), ("BHQ", 1)
        };

        /// <summary>
        /// Rex from Adelaide: the South Australian regional network plus Broken Hill and
        /// Mildura — Port Lincoln and Mount Gambier busiest. An approximation of its real
        /// pattern, not a copy of a published timetable.
        /// </summary>
        public static readonly IReadOnlyList<(string Code, int Weight)> RexNetwork = new[]
        {
            ("PLO", 3), ("MGB", 3), ("KGC", 2), ("WYA", 2), ("CED", 2), ("BHQ", 2), ("CPD", 1), ("MQL", 1)
        };

        /// <summary>QantasLink from Adelaide: Port Lincoln and Alice Springs (approximate).</summary>
        public static readonly IReadOnlyList<(string Code, int Weight)> QantasLinkNetwork = new[]
        {
            ("PLO", 2), ("ASP", 2)
        };

        /// <summary>Wattlebird Jet's destinations, weighted by how often <see cref="WattlebirdRotation"/> visits them.</summary>
        public static readonly IReadOnlyList<(string Code, int Weight)> WattlebirdNetwork = new[]
        {
            ("MEL", 3), ("SYD", 2), ("BNE", 1), ("PER", 1), ("CBR", 1)
        };

        public static IReadOnlyList<(string Code, int Weight)> AiNetworkFor(Airline airline) => airline.Id.Value switch
        {
            "REX" => RexNetwork,
            "QLK" => QantasLinkNetwork,
            "WTB" => WattlebirdNetwork,
            _ => AiNetwork
        };

        private void ScheduleAiDeparture(FleetAircraft aircraft, SimulationTime now)
        {
            if (aircraft.Airline.Id.Value == "WTB")
            {
                // Rotation, not a random draw (see WattlebirdRotation).
                var code = WattlebirdRotation[aircraft.CompletedTrips % WattlebirdRotation.Count];
                if (DestinationCatalogue.TryFind(code, out var next) && CanReach(aircraft, next))
                    aircraft.Scheduled = new ScheduledDeparture(next, AiDepartureWithinHours(now.Advance(AiStandTurnaroundSeconds)));
                return;
            }

            var total = 0;
            var candidates = new List<(Destination destination, int weight)>();
            foreach (var (code, weight) in AiNetworkFor(aircraft.Airline))
            {
                if (!DestinationCatalogue.TryFind(code, out var destination) || !CanReach(aircraft, destination))
                    continue;
                candidates.Add((destination, weight));
                total += weight;
            }

            if (total == 0)
                return;

            // One draw, as before, so a timeline stays reproducible from its seed.
            var roll = _random.NextInt(0, total);
            var pick = candidates[0].destination;
            foreach (var (destination, weight) in candidates)
            {
                if (roll < weight)
                {
                    pick = destination;
                    break;
                }
                roll -= weight;
            }

            aircraft.Scheduled = new ScheduledDeparture(pick, AiDepartureWithinHours(now.Advance(AiStandTurnaroundSeconds)));
        }

        /// <summary>The ready time if it falls in Emu Air's operating day, otherwise the next 06:00 in Adelaide.</summary>
        internal SimulationTime AiDepartureWithinHours(SimulationTime readyAt)
        {
            var local = Clock.LocalAt(readyAt);
            var first = local.Date.AddHours(AiFirstDepartureHour);
            var last = local.Date.AddHours(AiLastDepartureHour);
            if (local >= first && local <= last)
                return readyAt;
            var next = local < first ? first : first.AddDays(1);
            var at = Clock.AtLocal(next);
            return at.CompareTo(readyAt) > 0 ? at : readyAt;
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
