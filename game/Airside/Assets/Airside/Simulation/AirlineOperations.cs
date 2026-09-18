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
        public const long GoAroundCircuitSeconds = 4 * 60;
        /// <summary>
        /// Minimum interval between pushback clearances. This keeps two aircraft from being
        /// released onto the shared apron/taxi route together while still allowing a useful queue.
        /// </summary>
        public const long TaxiReleaseSeparationSeconds = 60;
        public const int MaxRecentEvents = 30;

        // Ground times are measured off the real Adelaide routes with ATR speed limits
        // (AdelaideGround), never picked: a taxi takes as long as driving it takes.

        /// <summary>Pushback, tug disconnect and taxi from <paramref name="stand"/> to the runway 05 holding point.</summary>
        public static long TaxiOutSecondsFrom(StableId stand) => AdelaideGround.TaxiOut(stand).WholeSeconds;
        public static long TaxiOutSecondsFrom(StableId stand, AircraftType type) => AdelaideGround.TaxiOut(stand, type).WholeSeconds;
        public static long TaxiOutSecondsFrom(StableId stand, AircraftType type, RunwayDirection runway) =>
            AdelaideGround.TaxiOut(stand, type, runway).WholeSeconds;

        /// <summary>Taxi from the E2 holding point into <paramref name="stand"/>.</summary>
        public static long TaxiInSecondsTo(StableId stand) => AdelaideGround.TaxiIn(stand).WholeSeconds;
        public static long TaxiInSecondsTo(StableId stand, AircraftType type) => AdelaideGround.TaxiIn(stand, type).WholeSeconds;

        /// <summary>Holding point onto the centreline at the 05 threshold.</summary>
        public static long LineupSeconds => AdelaideGround.Lineup.WholeSeconds;

        /// <summary>Rollout end, along the runway to exit E2 and clear to its holding point.</summary>
        public static long VacateSeconds => AdelaideGround.Vacate.WholeSeconds;
        public static long VacateSecondsFor(AircraftType type) => AdelaideGround.VacateFor(type).WholeSeconds;

        /// <summary>Runway time for lineup, the takeoff roll and initial climb, from the flown circuit.</summary>
        public static long TakeoffRunwaySeconds => LineupSeconds + CircuitProfile.TakeoffSeconds;
        public static long TakeoffRunwaySecondsFor(AircraftType type) =>
            LineupSeconds + AircraftPerformance.For(type).TakeoffSeconds;

        /// <summary>
        /// Runway time from the landing clearance on long final through flare, rollout
        /// and vacating — the whole of it is drawn in 3D, so it is flown in full.
        /// </summary>
        public static long LandingRunwaySeconds =>
            CircuitProfile.ApproachSeconds + CircuitProfile.LandingSeconds + VacateSeconds;
        public static long LandingRunwaySecondsFor(AircraftType type)
        {
            var profile = AircraftPerformance.For(type);
            return profile.ApproachSeconds + profile.LandingSeconds + VacateSecondsFor(type);
        }

        public static readonly IReadOnlyList<StableId> AdelaideRegionalBays = new[]
        {
            new StableId("BAY-1"), new StableId("BAY-2"), new StableId("BAY-3"), new StableId("BAY-4"),
            new StableId("BAY-5"), new StableId("BAY-6")
        };

        /// <summary>Terminal gates (ADR 0047): jets only, a separate stand system from the regional bays.</summary>
        public static readonly IReadOnlyList<StableId> AdelaideTerminalGates = new[]
        {
            new StableId("GATE-13"), new StableId("GATE-15"), new StableId("GATE-18"), new StableId("GATE-20")
        };

        /// <summary>Every stand at Adelaide: the regional bays, then the terminal gates.</summary>
        public static readonly IReadOnlyList<StableId> AdelaideStands = new List<StableId>(AdelaideRegionalBays)
        {
            AdelaideTerminalGates[0], AdelaideTerminalGates[1],
            AdelaideTerminalGates[2], AdelaideTerminalGates[3]
        };

        /// <summary>
        /// The terminal jet operator and its aircraft, tied to its gate. New games
        /// start with it; older saves gain it on load (<see cref="AddMissingTerminalOperators"/>).
        /// </summary>
        public static readonly IReadOnlyList<(Func<Airline> Make, (string Registration, AircraftType Type, StableId Gate)[] Fleet)> TerminalOperators = new (Func<Airline>, (string, AircraftType, StableId)[])[]
        {
            (Airline.VirginAustralia, new[] { ("VH-8IA", AircraftType.Boeing7378, new StableId("GATE-13")) }),
            (Airline.AirNewZealand, new[] { ("ZK-NNA", AircraftType.AirbusA321Neo, new StableId("GATE-15")) }),
            (Airline.CathayPacific, new[] { ("B-LRB", AircraftType.AirbusA350900, new StableId("GATE-18")) }),
            (Airline.SingaporeAirlines, new[] { ("9V-SCA", AircraftType.Boeing78710, new StableId("GATE-20")) })
        };

        /// <summary>
        /// Virgin Australia's representative mainland rotation — Melbourne and
        /// Sydney most, then Brisbane, Perth and Canberra. Deterministic and drawn from no random
        /// numbers, so adding the jet leaves the regional carriers' random sequence untouched.
        /// </summary>
        public static readonly IReadOnlyList<string> VirginRotation = new[] { "MEL", "SYD", "MEL", "BNE", "SYD", "PER", "MEL", "CBR" };

        /// <summary>Representative Air New Zealand trans-Tasman rotation from Adelaide.</summary>
        public static readonly IReadOnlyList<string> AirNewZealandRotation = new[] { "AKL", "AKL", "CHC", "AKL" };

        /// <summary>Jets use terminal gates; turboprops use the regional bays. Never the other way.</summary>
        public static bool NeedsTerminalGate(AircraftType type) =>
            AircraftCatalogue.TryFor(type, out var spec) && spec.StandClass == StandClass.TerminalGate;

        public static bool StandFits(AircraftType type, StableId stand) =>
            AdelaideGround.IsTerminalGate(stand) == NeedsTerminalGate(type);

        /// <summary>
        /// Real regional carriers that share Adelaide's regional apron with the player and
        /// the player's airline. New games start with them; older saves gain them on load
        /// (<see cref="AddMissingRegionalCarriers"/>). Six aircraft in all on six bays, so
        /// everyone always has a stand.
        /// </summary>
        public static readonly IReadOnlyList<(Func<Airline> Make, (string Registration, AircraftType Type)[] Fleet)> RegionalCarriers = new (Func<Airline>, (string, AircraftType)[])[]
        {
            (Airline.Rex, new[] { ("VH-ZRC", AircraftType.Saab340), ("VH-ZRD", AircraftType.Saab340), ("VH-ZRE", AircraftType.Saab340) }),
            (Airline.QantasLink, new[] { ("VH-QOK", AircraftType.Dash8Q400), ("VH-QOL", AircraftType.Dash8Q400), ("VH-QOM", AircraftType.Dash8Q400) })
        };

        private readonly ISimulationClock _clock;
        private readonly IRandomSource _random;
        private readonly List<Airline> _airlines = new();
        private readonly List<FleetAircraft> _fleet = new();
        private readonly List<StableId> _stands;
        private readonly List<FleetEvent> _recentEvents = new();
        private readonly List<FlightSettlement> _recentSettlements = new();
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
            CareerState = new AirlineCareerState();
        }

        /// <summary>Staggered opening departures; an arrival is already inbound as play begins.</summary>
        public static readonly long[] AiOpeningDepartureSeconds = { 7 * 60, 18 * 60, 31 * 60 };

        /// <summary>
        /// The ADR 0045 starting position at Adelaide: the player's airline with one
        /// ATR, real Adelaide operators on the apron, and one aircraft already inbound.
        /// </summary>
        public static AirlineOperations StartAtAdelaide(ISimulationClock clock, IRandomSource random, Airline player,
            AirlineClock airlineClock = null)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (!player.IsPlayer) throw new ArgumentException("The starting airline must be the player's.", nameof(player));

            var operations = new AirlineOperations(clock, random, DestinationCatalogue.Adelaide, AdelaideStands);
            operations.AddAirline(player);
            operations.AddAircraft(player, "VH-PAX", AircraftType.Atr42, AdelaideRegionalBays[0]);
            var aiFleet = new List<FleetAircraft>();
            operations.AddMissingRegionalCarriers(aiFleet);
            // One QantasLink service is already returning from Port Lincoln. This puts
            // an arrival on screen in the opening minutes instead of only after a
            // complete out-and-back cycle, while leaving room for the player's return.
            var inbound = aiFleet.FindLast(a => a.Airline.Id.Value == "QLK");
            if (inbound != null && DestinationCatalogue.TryFind("PLO", out var portLincoln))
            {
                operations.SeedOpeningInbound(inbound, portLincoln, 4 * 60);
                var secondInbound = aiFleet.FindLast(a => a.Airline.Id.Value == "REX");
                if (secondInbound != null && DestinationCatalogue.TryFind("MGB", out var mountGambier))
                    operations.SeedOpeningInbound(secondInbound, mountGambier, 11 * 60);
            }

            var departureIndex = 0;
            for (var i = 0; i < aiFleet.Count && departureIndex < AiOpeningDepartureSeconds.Length; i++)
            {
                if (aiFleet[i].State == FleetState.AtStand && aiFleet[i].Scheduled is { } first)
                    aiFleet[i].Scheduled = new ScheduledDeparture(first.Destination,
                        operations.ProcessedTo.Advance(AiOpeningDepartureSeconds[departureIndex++]));
            }

            // The terminal jet joins after the regional openings are fixed, so they are unchanged.
            var terminalFleet = new List<FleetAircraft>();
            operations.AddMissingTerminalOperators(terminalFleet);
            var internationalInbound = terminalFleet.Find(a => a.Airline.Id.Value == "ANZ");
            if (internationalInbound != null && DestinationCatalogue.TryFind("AKL", out var auckland))
                operations.SeedOpeningInbound(internationalInbound, auckland, 17 * 60);

            operations.Clock = airlineClock ?? AirlineClock.Default;
            return operations;
        }

        private void SeedOpeningInbound(FleetAircraft aircraft, Destination destination, long secondsToCircuit)
        {
            aircraft.DepartureStand = aircraft.Stand;
            aircraft.Stand = default;
            aircraft.Scheduled = null;
            aircraft.CurrentDestination = destination;
            aircraft.Restore(FleetState.Inbound, _processedTo, _processedTo.Advance(secondsToCircuit));
        }

        /// <summary>
        /// Add any <see cref="RegionalCarriers"/> aircraft that is not flying here yet, parking
        /// each on a free stand (skipped if none is free) with an AI departure booked. An
        /// airline is added only when at least one of its aircraft can park, and a later load
        /// still fills remaining registrations — same idea as
        /// <see cref="AddMissingTerminalOperators"/>. Returns how many aircraft joined.
        /// </summary>
        public int AddMissingRegionalCarriers(List<FleetAircraft> added = null)
        {
            var count = 0;
            foreach (var (make, fleet) in RegionalCarriers)
            {
                var template = make();
                var airline = _airlines.Find(a => a.Id.Equals(template.Id));
                foreach (var (registration, type) in fleet)
                {
                    if (_fleet.Exists(a => string.Equals(a.Registration, registration, StringComparison.OrdinalIgnoreCase)))
                        continue;
                    var stand = SuggestStandFor(type);
                    if (!stand.HasValue)
                        break;
                    if (airline == null)
                    {
                        airline = template;
                        AddAirline(airline);
                    }

                    var aircraft = AddAircraft(airline, registration, type, stand.Value);
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
        /// <param name="at">The time to evaluate seasonal operators (e.g. Cathay) against.
        /// Defaults to <see cref="_processedTo"/> for the load/startup call sites, which are
        /// evaluating "now". <see cref="Update"/> must pass its own target time explicitly —
        /// it calls this before advancing <see cref="_processedTo"/>, so the default would
        /// check the time being advanced *from*, not the time being advanced *to*.</param>
        public int AddMissingTerminalOperators(List<FleetAircraft> added = null, SimulationTime? at = null)
        {
            var asOf = at ?? _processedTo;
            var count = 0;
            foreach (var (make, fleet) in TerminalOperators)
            {
                var template = make();
                if (template.Id.Value == "CPA" && !IsCathaySeason(asOf))
                    continue;
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

                    var aircraft = AddAircraft(airline, registration, type, gate);
                    added?.Add(aircraft);
                    count++;
                }
            }

            return count;
        }

        /// <summary>Cathay's Adelaide service operates through the southern summer season.</summary>
        public bool IsCathaySeason(SimulationTime at)
        {
            var local = Clock.LocalAt(at);
            return local.Month == 11 && local.Day >= 10
                   || local.Month == 12
                   || local.Month <= 2
                   || local.Month == 3 && local.Day <= 27;
        }

        public Destination Home { get; }

        /// <summary>Maps simulation time to real Adelaide time. Live: one simulated second per real second.</summary>
        public AirlineClock Clock { get; internal set; } = AirlineClock.Default;

        /// <summary>Simulation time everything has been resolved up to.</summary>
        public SimulationTime ProcessedTo => _processedTo;

        /// <summary>When the tower may next clear a runway movement.</summary>
        public SimulationTime RunwayFreeAt => _runwayFreeAt;
        public SurfaceWind Wind => RunwayWeather.At(Clock, _processedTo);
        public RunwayDirection ActiveRunway => RunwayWeather.Select(Wind);

        /// <summary>Generator state for saving; 0 when the source is not a <see cref="SeededRandomSource"/>.</summary>
        public uint RandomState => _random is SeededRandomSource seeded ? seeded.State : 0;
        public IReadOnlyList<Airline> Airlines => _airlines;
        public IReadOnlyList<FleetAircraft> Fleet => _fleet;
        public IReadOnlyList<StableId> Stands => _stands;

        /// <summary>Newest last, capped at <see cref="MaxRecentEvents"/>.</summary>
        public IReadOnlyList<FleetEvent> RecentEvents => _recentEvents;

        /// <summary>Events emitted since start, so a reader can tell which recent ones are new.</summary>
        public long TotalEvents { get; private set; }

        /// <summary>Newest last, capped at <see cref="MaxRecentEvents"/>. Not persisted — a
        /// fresh session has no settlement history to show, only the career totals it produced.</summary>
        public IReadOnlyList<FlightSettlement> RecentSettlements => _recentSettlements;

        /// <summary>Settlements applied since start, so a reader can tell which recent ones are new.</summary>
        public long TotalSettlements { get; private set; }

        public Airline PlayerAirline => _airlines.Find(a => a.IsPlayer);

        /// <summary>The player airline's career progress (ADR 0053): funds, reliability, tier and contract.</summary>
        public AirlineCareerState CareerState { get; private set; }

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
            if (RequiresTripDestination(state) && currentDestination == null)
                throw new FormatException($"{registration} is {state} with no destination.");
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

        internal void RestoreMovementData(string registration, RunwayDirection runway, bool wentAroundThisTrip)
        {
            var aircraft = _fleet.Find(a => string.Equals(a.Registration, registration, StringComparison.OrdinalIgnoreCase));
            if (aircraft == null)
                throw new FormatException($"{registration}: movement data has no aircraft.");
            aircraft.AssignedRunway = runway;
            aircraft.WentAroundThisTrip = wentAroundThisTrip;
        }

        internal void RestoreTower(SimulationTime runwayFreeAt, long totalEvents)
        {
            _runwayFreeAt = runwayFreeAt;
            TotalEvents = Math.Max(0, totalEvents);
        }

        /// <summary>Rebuilds career state (ADR 0053) from a v6+ save, or a fresh Provisional
        /// state when migrating an older one — never from anything it doesn't recognise.</summary>
        internal void RestoreCareerState(
            long funds, int reliability, string tier, string activeContractId,
            long contractAcceptedAtSeconds, int contractCompletedRotations, IEnumerable<string> processedSettlementKeys,
            IEnumerable<string> completedContractIds = null)
        {
            if (string.IsNullOrWhiteSpace(tier)
                || !Enum.TryParse(tier, out OperatingTier parsedTier)
                || !Enum.IsDefined(typeof(OperatingTier), parsedTier)
                || !string.Equals(parsedTier.ToString(), tier.Trim(), StringComparison.Ordinal))
                throw new FormatException($"Unknown career tier '{tier}'.");

            ActiveRouteContract contract = null;
            if (!string.IsNullOrEmpty(activeContractId))
            {
                if (!RouteContractCatalogue.TryFind(activeContractId, out _))
                    throw new FormatException($"Unknown contract '{activeContractId}'.");
                contract = new ActiveRouteContract(
                    activeContractId, new SimulationTime(contractAcceptedAtSeconds), contractCompletedRotations);
            }

            CareerState = new AirlineCareerState(funds, reliability, parsedTier, contract, processedSettlementKeys,
                completedContractIds);
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
        /// Next time ground can issue another pushback clearance on the same apron as
        /// <paramref name="terminalGate"/>. It is derived from active taxi-out state, so
        /// save files and event-driven catch-up remain deterministic.
        ///
        /// Regional bays and terminal gates sit on separate aprons with their own taxi
        /// routes (<see cref="AirportTaxiNetwork"/>) and never share pavement, so this used
        /// to serialise every pushback in the fleet through one global 60 s gate — a Rex
        /// Saab pushing back from a regional bay held up an unrelated Virgin 737 push from
        /// the terminal gates for no physical reason.
        /// </summary>
        private SimulationTime? NextTaxiReleaseAt(SimulationTime now, bool terminalGate)
        {
            SimulationTime? release = null;
            foreach (var aircraft in _fleet)
            {
                if (aircraft.State != FleetState.TaxiOut)
                    continue;
                if (AdelaideGround.IsTerminalGate(aircraft.DepartureStand) != terminalGate)
                    continue;
                var candidate = aircraft.StateStartedAt.Advance(TaxiReleaseSeparationSeconds);
                if (candidate.CompareTo(now) > 0 && (release == null || candidate.CompareTo(release.Value) > 0))
                    release = candidate;
            }

            return release;
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
            var taxiReleaseWantedBay = false;
            var taxiReleaseWantedGate = false;
            foreach (var aircraft in _fleet)
            {
                if (aircraft.StateEndsAt.HasValue)
                    Consider(aircraft.StateEndsAt.Value);
                if (aircraft.State == FleetState.AtStand && aircraft.Scheduled.HasValue)
                {
                    Consider(aircraft.Scheduled.Value.DepartAt);
                    if (aircraft.Scheduled.Value.DepartAt.CompareTo(now) <= 0)
                    {
                        if (AdelaideGround.IsTerminalGate(aircraft.Stand))
                            taxiReleaseWantedGate = true;
                        else
                            taxiReleaseWantedBay = true;
                    }
                }
                if (aircraft.State is FleetState.HoldingShort or FleetState.HoldingForLanding)
                    runwayWanted = true;
            }

            if (runwayWanted)
                Consider(_runwayFreeAt);
            // Bay and gate pushback releases are tracked separately (NextTaxiReleaseAt):
            // the two aprons never share pavement, so one waiting on the other's release
            // would skip past its own.
            if (taxiReleaseWantedBay && NextTaxiReleaseAt(now, terminalGate: false) is { } bayRelease)
                Consider(bayRelease);
            if (taxiReleaseWantedGate && NextTaxiReleaseAt(now, terminalGate: true) is { } gateRelease)
                Consider(gateRelease);

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

            if (aircraft.Airline.IsPlayer)
            {
                var cost = FlightEconomics.DispatchCost(aircraft.Type, DistanceKm(destination));
                var alreadyPaid = aircraft.Scheduled.HasValue
                    ? FlightEconomics.DispatchCost(aircraft.Type, DistanceKm(aircraft.Scheduled.Value.Destination))
                    : 0;
                if (CareerState.Funds + alreadyPaid < cost)
                    return CommandResult.Refused(
                        $"This flight costs ${cost:N0}; you have ${CareerState.Funds:N0}.");
                if (alreadyPaid > 0)
                    CareerState.RefundDispatch(alreadyPaid);
                CareerState.TryChargeDispatch(cost);
            }

            aircraft.Scheduled = new ScheduledDeparture(destination, departAt);
            return CommandResult.Ok;
        }

        public CommandResult CancelDeparture(FleetAircraft aircraft)
        {
            if (aircraft == null || !_fleet.Contains(aircraft))
                return CommandResult.Refused("Unknown aircraft.");
            if (aircraft.State != FleetState.AtStand || !aircraft.Scheduled.HasValue)
                return CommandResult.Refused($"{aircraft.Registration} has no departure waiting to start.");

            if (aircraft.Airline.IsPlayer && aircraft.Scheduled.HasValue)
                CareerState.RefundDispatch(FlightEconomics.DispatchCost(aircraft.Type,
                    DistanceKm(aircraft.Scheduled.Value.Destination)));

            // ADR 0053: a broken commitment against the active career contract costs
            // reliability — only when the cancelled flight would actually have counted
            // (right airline, aircraft, route); an unrelated cancellation is free.
            if (aircraft.Airline.IsPlayer && CareerState.ActiveContract != null
                && RouteContractCatalogue.TryFind(CareerState.ActiveContract.DefinitionId, out var contract)
                && contract.EligibleType == aircraft.Type
                && contract.MatchesRoute(Home.Code, aircraft.Scheduled.Value.Destination.Code))
                CareerState.PenalizeCancellation(contract.Id, contract.ReliabilityLossOnCancel);

            aircraft.Scheduled = null;
            return CommandResult.Ok;
        }

        /// <summary>
        /// Accepts an authored route contract (ADR 0053) as the player's one active career
        /// contract. Refused while another is already active, or the player hasn't reached
        /// the tier it requires.
        /// </summary>
        public CommandResult AcceptContract(RouteContractDefinition definition)
        {
            if (definition == null)
                return CommandResult.Refused("Unknown contract.");
            if (CareerState.ActiveContract != null)
                return CommandResult.Refused("Already operating a contract.");
            if (CareerState.HasCompleted(definition.Id))
                return CommandResult.Refused($"{definition.Id} is already complete.");
            if (CareerState.Tier < definition.RequiredTier)
                return CommandResult.Refused($"{definition.Id} needs {definition.RequiredTier} tier.");

            CareerState.ActiveContract = new ActiveRouteContract(definition.Id, _processedTo);
            return CommandResult.Ok;
        }

        /// <summary>
        /// Settles a player aircraft's just-completed rotation: every flight pays its
        /// operating revenue, and a matching active contract adds its bonus. Idempotent
        /// via <see cref="AirlineCareerState.RecordCompletedRotation"/>.
        /// </summary>
        private void TrySettleFlight(FleetAircraft aircraft, Destination? justFlown, SimulationTime now)
        {
            if (!justFlown.HasValue)
                return;

            RouteContractDefinition matching = null;
            var contract = CareerState.ActiveContract;
            if (contract != null
                && RouteContractCatalogue.TryFind(contract.DefinitionId, out var definition)
                && definition.EligibleType == aircraft.Type
                && definition.MatchesRoute(Home.Code, justFlown.Value.Code))
                matching = definition;

            var settlementId = new SettlementId(aircraft.Registration, aircraft.CompletedTrips);
            var settlement = CareerState.RecordCompletedRotation(
                settlementId, FlightEconomics.FlightPay(aircraft.Type, DistanceKm(justFlown.Value)), matching);
            if (settlement == null)
                return;

            _recentSettlements.Add(settlement.Value);
            TotalSettlements++;
            if (_recentSettlements.Count > MaxRecentEvents)
                _recentSettlements.RemoveAt(0);
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
            Transition(aircraft, FleetState.TaxiIn, _processedTo, TaxiInSecondsTo(stand, aircraft.Type));
            return CommandResult.Ok;
        }

        // ---- Time ----------------------------------------------------------------

        public void Update()
        {
            var target = _clock.Now;
            if (target.CompareTo(_processedTo) < 0)
                throw new InvalidOperationException("Simulation time cannot move backwards.");

            // A save may cross into the Cathay summer season while it remains open.
            // Backfill the seasonal operator at that point; the method is idempotent.
            // Passes target explicitly: _processedTo (the default) is still the time
            // being advanced *from* at this point in Update(), so checking the season
            // against it instead of target used to miss the exact call where the season
            // boundary was crossed, catching up only on the next Update() call.
            if (IsCathaySeason(target))
                AddMissingTerminalOperators(at: target);

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
                    var pushingBackFromGate = AdelaideGround.IsTerminalGate(aircraft.Stand);
                    if (NextTaxiReleaseAt(now, pushingBackFromGate).HasValue)
                        return false;
                    // A gate pushback needs its lead-in clear before the tug moves; it is re-checked
                    // whenever anything else finishes, since that is the only way it frees.
                    if (pushingBackFromGate && !IsLeadInFree(aircraft.Stand, aircraft))
                        return false;
                    aircraft.CurrentDestination = aircraft.Scheduled.Value.Destination;
                    aircraft.Scheduled = null;
                    aircraft.DepartureStand = aircraft.Stand;
                    aircraft.Stand = default;
                    aircraft.AssignedRunway = ActiveRunway;
                    aircraft.WentAroundThisTrip = false;
                    Transition(aircraft, FleetState.TaxiOut, now,
                        TaxiOutSecondsFrom(aircraft.DepartureStand, aircraft.Type, aircraft.AssignedRunway));
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
                    aircraft.AssignedRunway = ActiveRunway;
                    Transition(aircraft, FleetState.HoldingForLanding, now, null);
                    return true;

                case FleetState.GoAround:
                    Transition(aircraft, FleetState.HoldingForLanding, now, null);
                    return true;

                case FleetState.Landing:
                    // A missed approach is stored as Landing for ApproachSeconds only. The
                    // real landing after that is a longer state (approach + roll + vacate)
                    // and must still reach a stand even though WentAroundThisTrip stays set
                    // so the tower will not send them around again on the same trip.
                    if (aircraft.WentAroundThisTrip && IsMissedApproachLanding(aircraft))
                    {
                        Transition(aircraft, FleetState.GoAround, now, GoAroundCircuitSeconds);
                        return true;
                    }
                    Transition(aircraft, FleetState.AwaitingStand, now, null);
                    return true;

                case FleetState.AwaitingStand:
                    if (aircraft.Airline.IsPlayer)
                        return false;
                    var chosen = SuggestStand(aircraft);
                    if (chosen == null)
                        return false;
                    aircraft.Stand = chosen.Value;
                    Transition(aircraft, FleetState.TaxiIn, now, TaxiInSecondsTo(chosen.Value, aircraft.Type));
                    return true;

                case FleetState.TaxiIn:
                    var justFlown = aircraft.CurrentDestination;
                    aircraft.CompletedTrips++;
                    aircraft.CurrentDestination = null;
                    aircraft.WentAroundThisTrip = false;
                    Transition(aircraft, FleetState.AtStand, now, null);
                    if (aircraft.Airline.IsPlayer)
                        TrySettleFlight(aircraft, justFlown, now);
                    else
                        ScheduleAiDeparture(aircraft, now);
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Bays too close together for a Dash 8-400 beside another aircraft: 50D and 50E are
        /// 3.3–3.5 m apart with a Q400 on one (ADR 0049), under the 4.5 m code C clearance.
        /// </summary>
        public static readonly IReadOnlyList<(StableId A, StableId B)> TightBayPairs = new[]
        {
            (new StableId("BAY-1"), new StableId("BAY-5"))
        };

        /// <summary>
        /// True when parking <paramref name="type"/> on <paramref name="stand"/> would put a
        /// Dash 8-400 next to another aircraft on a <see cref="TightBayPairs"/> neighbour.
        /// </summary>
        public bool CrowdsNeighbour(AircraftType type, StableId stand)
        {
            foreach (var (a, b) in TightBayPairs)
            {
                StableId other;
                if (stand.Equals(a))
                    other = b;
                else if (stand.Equals(b))
                    other = a;
                else
                    continue;

                foreach (var aircraft in _fleet)
                    if (StandHolder(aircraft, other)
                        && (ReferenceEquals(type, AircraftType.Dash8Q400) || ReferenceEquals(aircraft.Type, AircraftType.Dash8Q400)))
                        return true;
            }

            return false;
        }

        /// <summary>
        /// The stand an aircraft waiting for one should take — other operators taxi straight to
        /// it, and the player's "Quickest" button offers it: a free stand that fits, preferring
        /// one that does not crowd a tight neighbour, then the shortest taxi in, then list
        /// order. Never refuses a free stand only for clearance, so nobody is stranded.
        /// </summary>
        public StableId? SuggestStand(FleetAircraft aircraft)
        {
            if (aircraft == null)
                return null;

            // Scheduled terminal operators return to their own gate when it is available.
            // This keeps the Air New Zealand and Virgin streams operationally legible while
            // still allowing an alternate compatible gate if the home position is occupied.
            if (NeedsTerminalGate(aircraft.Type)
                && !string.IsNullOrEmpty(aircraft.DepartureStand.Value)
                && _stands.Contains(aircraft.DepartureStand)
                && IsStandFree(aircraft.DepartureStand)
                && IsLeadInFree(aircraft.DepartureStand, aircraft))
                return aircraft.DepartureStand;

            // The player should never return to find every regional bay occupied by AI.
            // When their aircraft is away, keep the last compatible bay reserved; AI
            // arrivals can queue at E2 until another operator pushes back.
            if (!aircraft.Airline.IsPlayer && !NeedsTerminalGate(aircraft.Type)
                && PlayerAirline != null && !PlayerOwnsRegionalStand())
            {
                var free = 0;
                foreach (var stand in FreeStandsFor(aircraft.Type))
                    free++;
                if (free <= 1)
                    return null;
            }

            return SuggestStandFor(aircraft.Type, aircraft);
        }

        private bool PlayerOwnsRegionalStand()
        {
            foreach (var aircraft in _fleet)
                if (aircraft.Airline.IsPlayer && HoldsStand(aircraft) && !AdelaideGround.IsTerminalGate(aircraft.Stand))
                    return true;
            return false;
        }

        /// <summary>
        /// Same ranking as <see cref="SuggestStand"/> for a type that is not yet on the field
        /// (regional backfill). <paramref name="except"/> is the aircraft already allowed to
        /// use a busy gate lead-in; null means the lead-in must be empty.
        /// </summary>
        public StableId? SuggestStandFor(AircraftType type, FleetAircraft except = null)
        {
            if (type == null)
                return null;

            StableId? best = null;
            var bestCrowds = true;
            var bestSeconds = long.MaxValue;
            foreach (var stand in _stands)
            {
                if (!StandFits(type, stand) || !IsStandFree(stand))
                    continue;
                if (AdelaideGround.IsTerminalGate(stand) && !IsLeadInFree(stand, except))
                    continue;
                var crowds = CrowdsNeighbour(type, stand);
                var seconds = TaxiInSecondsTo(stand, type);
                if (best != null && (crowds && !bestCrowds || crowds == bestCrowds && seconds >= bestSeconds))
                    continue;
                best = stand;
                bestCrowds = crowds;
                bestSeconds = seconds;
            }

            return best;
        }

        /// <summary>
        /// A departure holding short this long gets the runway ahead of arrivals that have
        /// waited less, so a steady arrival stream can no longer hold it forever.
        /// </summary>
        public const long DepartureMaxHoldSeconds = 6 * 60;

        /// <summary>
        /// One runway movement at a time. Arrivals normally go first; a departure that has held
        /// short past <see cref="DepartureMaxHoldSeconds"/>, and longer than the first arrival
        /// has circled, goes instead. Derived from state times only, so saves need nothing new.
        /// </summary>
        private bool RunTower(SimulationTime now)
        {
            if (_runwayFreeAt.CompareTo(now) > 0)
                return false;

            var arrival = LongestWaiting(FleetState.HoldingForLanding);
            var departure = LongestWaiting(FleetState.HoldingShort);
            var next = arrival ?? departure;
            if (arrival != null && departure != null
                && now.ElapsedSeconds - departure.StateStartedAt.ElapsedSeconds >= DepartureMaxHoldSeconds
                && departure.StateStartedAt.CompareTo(arrival.StateStartedAt) < 0)
                next = departure;
            if (next == null)
                return false;

            var landing = next.State == FleetState.HoldingForLanding;
            var profile = AircraftPerformance.For(next.Type);
            if (landing && ShouldGoAround(next, now))
            {
                // Fly the approach so the go-around is visible off short final, then abort
                // before the landing roll. The runway frees at the abort, not after the
                // four-minute circuit the missed approach continues into.
                next.WentAroundThisTrip = true;
                Transition(next, FleetState.Landing, now, profile.ApproachSeconds);
                _runwayFreeAt = now.Advance(profile.ApproachSeconds + WakeSeparationSeconds(next.Type));
                return true;
            }
            var runwaySeconds = landing
                ? profile.ApproachSeconds + profile.LandingSeconds
                  + AdelaideGround.VacateFor(next.Type, next.AssignedRunway).WholeSeconds
                : AdelaideGround.LineupFor(next.AssignedRunway).WholeSeconds + profile.TakeoffSeconds;
            Transition(next, landing ? FleetState.Landing : FleetState.TakingOff, now, runwaySeconds);
            _runwayFreeAt = now.Advance(runwaySeconds + WakeSeparationSeconds(next.Type));
            return true;
        }

        private bool ShouldGoAround(FleetAircraft aircraft, SimulationTime now)
        {
            if (aircraft.WentAroundThisTrip)
                return false;
            var arrivals = 0;
            foreach (var candidate in _fleet)
                if (candidate.State == FleetState.HoldingForLanding)
                    arrivals++;
            var seed = GoAroundSeed(aircraft.CompletedTrips, aircraft.Registration, now.ElapsedSeconds);
            return arrivals >= 2 && Math.Abs(seed % 11) == 0;
        }

        /// <summary>
        /// Deterministic per-aircraft, per-hour go-around seed. Every registration in the
        /// fleet follows the same "VH-XXX" format (<see cref="StableRegistrationHash"/>
        /// exists because of this): a registration's *length* is therefore the same for
        /// every aircraft in the game and contributed nothing to this seed, so any two
        /// aircraft with the same completed-trip count in the same hour (e.g. sister ships
        /// fresh out of the gate) went around, or didn't, in lockstep forever. Hashing the
        /// whole registration instead of just its length gives each aircraft its own draw.
        /// </summary>
        internal static int GoAroundSeed(int completedTrips, string registration, long nowSeconds) =>
            unchecked(completedTrips * 17 + StableRegistrationHash(registration) + (int)(nowSeconds / 3600));

        internal static int StableRegistrationHash(string value)
        {
            unchecked
            {
                var hash = 23;
                foreach (var ch in value ?? string.Empty)
                    hash = hash * 31 + ch;
                return hash;
            }
        }

        /// <summary>Wingspan (m) at or above which a departing/landing aircraft is Heavy wake category.</summary>
        public const double HeavyWakeWingspanMetres = 50.0;

        /// <summary>Wingspan (m) at or above which a departing/landing aircraft is Medium wake category.</summary>
        public const double MediumWakeWingspanMetres = 30.0;

        /// <summary>
        /// Wake-turbulence separation owed to whoever uses the runway next, derived from the
        /// leading aircraft's own wingspan (<see cref="AircraftCatalogue"/>) rather than a
        /// hand-picked list of type IDs — a second, disconnected classification that would
        /// silently give any newly added heavy jet only the smallest 90 s separation if its
        /// ID were never added here to match.
        /// </summary>
        public static long WakeSeparationSeconds(AircraftType type)
        {
            var wingspan = AircraftCatalogue.TryFor(type, out var spec) ? spec.WingspanMetres : 0;
            if (wingspan >= HeavyWakeWingspanMetres)
                return 180;
            return wingspan >= MediumWakeWingspanMetres ? 120 : RunwaySeparationSeconds;
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

        /// <summary>
        /// These states are mid-trip. A save that omits <see cref="FleetAircraft.CurrentDestination"/>
        /// would collapse every airborne leg to zero seconds on the next tick.
        /// </summary>
        internal static bool RequiresTripDestination(FleetState state) => state is
            FleetState.TaxiOut or FleetState.HoldingShort or FleetState.TakingOff
            or FleetState.Outbound or FleetState.AtDestination or FleetState.Inbound
            or FleetState.HoldingForLanding or FleetState.GoAround or FleetState.Landing;

        /// <summary>
        /// The tower stores a missed approach as <see cref="FleetState.Landing"/> lasting only
        /// the approach. A subsequent real landing is longer, so <see cref="FleetAircraft.WentAroundThisTrip"/>
        /// can stay set (no second go-around) without trapping the aircraft in the circuit.
        /// </summary>
        private static bool IsMissedApproachLanding(FleetAircraft aircraft)
        {
            if (!aircraft.StateEndsAt.HasValue)
                return false;
            var duration = aircraft.StateEndsAt.Value.ElapsedSeconds - aircraft.StateStartedAt.ElapsedSeconds;
            return duration <= AircraftPerformance.For(aircraft.Type).ApproachSeconds;
        }

        /// <summary>AI aircraft push back no earlier than this Adelaide hour…</summary>
        public const int AiFirstDepartureHour = 6;

        /// <summary>…and no later than this one, like a regional operator's day.</summary>
        public const int AiLastDepartureHour = 21;

        /// <summary>
        /// Fallback regional network for a future AI operator.
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

        /// <summary>Virgin Australia's representative domestic network from Adelaide.</summary>
        public static readonly IReadOnlyList<(string Code, int Weight)> VirginNetwork = new[]
        {
            ("MEL", 3), ("SYD", 2), ("BNE", 1), ("PER", 1), ("CBR", 1)
        };

        /// <summary>Air New Zealand trans-Tasman service from Adelaide.</summary>
        public static readonly IReadOnlyList<(string Code, int Weight)> AirNewZealandNetwork = new[]
        {
            ("AKL", 3), ("CHC", 1)
        };

        public static readonly IReadOnlyList<(string Code, int Weight)> SingaporeNetwork = new[] { ("SIN", 1) };
        public static readonly IReadOnlyList<(string Code, int Weight)> CathayNetwork = new[] { ("HKG", 1) };

        public static IReadOnlyList<(string Code, int Weight)> AiNetworkFor(Airline airline) => airline.Id.Value switch
        {
            "REX" => RexNetwork,
            "QLK" => QantasLinkNetwork,
            "VOZ" => VirginNetwork,
            "ANZ" => AirNewZealandNetwork,
            "SIA" => SingaporeNetwork,
            "CPA" => CathayNetwork,
            _ => AiNetwork
        };

        private void ScheduleAiDeparture(FleetAircraft aircraft, SimulationTime now)
        {
            if (aircraft.Airline.Id.Value == "VOZ")
            {
                // Rotation, not a random draw, so the mainline timetable is stable.
                var code = VirginRotation[aircraft.CompletedTrips % VirginRotation.Count];
                if (DestinationCatalogue.TryFind(code, out var next) && CanReach(aircraft, next))
                    aircraft.Scheduled = new ScheduledDeparture(next, AiDepartureWithinHours(now.Advance(AiTurnaroundSeconds(aircraft))));
                return;
            }

            if (aircraft.Airline.Id.Value == "ANZ")
            {
                // Rotation, not a random draw, so international traffic is stable too.
                var code = AirNewZealandRotation[aircraft.CompletedTrips % AirNewZealandRotation.Count];
                if (DestinationCatalogue.TryFind(code, out var next) && CanReach(aircraft, next))
                    aircraft.Scheduled = new ScheduledDeparture(next, AiDepartureWithinHours(now.Advance(AiTurnaroundSeconds(aircraft))));
                return;
            }
            if (aircraft.Airline.Id.Value is "SIA" or "CPA")
            {
                var code = aircraft.Airline.Id.Value == "SIA" ? "SIN" : "HKG";
                if (DestinationCatalogue.TryFind(code, out var next) && CanReach(aircraft, next))
                    aircraft.Scheduled = new ScheduledDeparture(next,
                        AiDepartureWithinHours(now.Advance(AiTurnaroundSeconds(aircraft))));
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

            aircraft.Scheduled = new ScheduledDeparture(pick, AiDepartureWithinHours(now.Advance(AiTurnaroundSeconds(aircraft))));
        }

        /// <summary>
        /// Repeatable 30–64 minute turnarounds. The variation is tied to registration
        /// and trip number, so traffic feels human without changing every load.
        /// </summary>
        private static long AiTurnaroundSeconds(FleetAircraft aircraft)
        {
            unchecked
            {
                var hash = 17;
                foreach (var ch in aircraft.Registration)
                    hash = hash * 31 + ch;
                hash = hash * 31 + aircraft.CompletedTrips;
                var minutes = 30 + Math.Abs(hash % 35);
                return minutes * 60L;
            }
        }

        /// <summary>The ready time if it falls in the operating day, otherwise the next 06:00 in Adelaide.</summary>
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
