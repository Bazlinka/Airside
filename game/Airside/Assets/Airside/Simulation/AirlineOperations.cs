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
    /// The airport runs itself. The tower owns each physical strip separately —
    /// 05/23 and 12/30 can move in parallel — with arrivals before departures and
    /// wake separation on that strip. Owners only choose where and when an aircraft
    /// goes, and which stand it parks on. AI airlines make both choices automatically.
    /// </summary>
    public sealed class AirlineOperations
    {
        public const long DestinationTurnaroundSeconds = 40 * 60;
        public const long AiStandTurnaroundSeconds = 45 * 60;
        public const long RunwaySeparationSeconds = 90;
        public const long GoAroundCircuitSeconds = 4 * 60;
        /// <summary>
        /// Floor between pushback clearances on one apron. The live gate waits until
        /// the aircraft already rolling has cleared the stands — see
        /// <see cref="TaxiClearSecondsFrom(StableId, AircraftType, RunwayDirection)"/>.
        /// </summary>
        public const long TaxiReleaseSeparationSeconds = 60;
        /// <summary>
        /// Two aircraft may taxi on the same apron at once. A third waits until the
        /// earliest of those two has cleared the stands. One-at-a-time made every
        /// push wait in a single-file queue even when the taxilane was empty.
        /// </summary>
        public const int MaxSimultaneousTaxiOutsPerApron = 2;
        public const int MaxRecentEvents = 30;

        // Ground times are measured off the real Adelaide routes with ATR speed limits
        // (AdelaideGround), never picked: a taxi takes as long as driving it takes.

        /// <summary>Pushback, tug disconnect and taxi from <paramref name="stand"/> to the runway 05 holding point.</summary>
        public static long TaxiOutSecondsFrom(StableId stand) => AdelaideGround.TaxiOut(stand).WholeSeconds;
        public static long TaxiOutSecondsFrom(StableId stand, AircraftType type) => AdelaideGround.TaxiOut(stand, type).WholeSeconds;
        public static long TaxiOutSecondsFrom(StableId stand, AircraftType type, RunwayDirection runway) =>
            AdelaideGround.TaxiOut(stand, type, runway).WholeSeconds;

        /// <summary>
        /// How long the next same-apron pushback must wait: the first aircraft's
        /// pushback, tug disconnect, and enough taxi to leave the stands. Sixty
        /// seconds used to release the neighbour while the first was still on T4.
        /// </summary>
        public static long TaxiClearSecondsFrom(StableId stand) =>
            TaxiClearSecondsFrom(stand, AdelaideGround.IsTerminalGate(stand) ? AircraftType.Boeing7378 : AircraftType.Atr42);

        public static long TaxiClearSecondsFrom(StableId stand, AircraftType type) =>
            TaxiClearSecondsFrom(stand, type, RunwayDirection.Runway05);

        public static long TaxiClearSecondsFrom(StableId stand, AircraftType type, RunwayDirection runway)
        {
            var leg = AdelaideGround.TaxiOut(stand, type, runway);
            if (leg.Parts.Count < 2)
                return Math.Max(TaxiReleaseSeparationSeconds, (long)Math.Ceiling(leg.Seconds * 0.4));

            var push = leg.Parts[0];
            var taxi = leg.Parts[1];
            var clearMetres = Math.Min(180f, taxi.Path.Length * 0.35f);
            var alongTaxi = taxi.Path.SecondsAtDistance(clearMetres);
            var wait = push.Seconds + taxi.PauseBeforeSeconds + Math.Max(90.0, alongTaxi);
            return Math.Max(TaxiReleaseSeparationSeconds, (long)Math.Ceiling(wait));
        }

        /// <summary>Taxi from the E2 holding point into <paramref name="stand"/>.</summary>
        public static long TaxiInSecondsTo(StableId stand) => AdelaideGround.TaxiIn(stand).WholeSeconds;
        public static long TaxiInSecondsTo(StableId stand, AircraftType type) => AdelaideGround.TaxiIn(stand, type).WholeSeconds;
        public static long TaxiInSecondsTo(StableId stand, AircraftType type, RunwayDirection runway) =>
            AdelaideGround.TaxiIn(stand, type, runway).WholeSeconds;

        /// <summary>Holding point onto the centreline at the 05 threshold.</summary>
        public static long LineupSeconds => AdelaideGround.Lineup.WholeSeconds;

        /// <summary>Rollout end, along the runway to exit E2 and clear to its holding point.</summary>
        public static long VacateSeconds => AdelaideGround.Vacate.WholeSeconds;
        public static long VacateSecondsFor(AircraftType type) => AdelaideGround.VacateFor(type).WholeSeconds;

        /// <summary>Runway time for lineup, the takeoff roll and initial climb, from the flown circuit.</summary>
        public static long TakeoffRunwaySeconds => LineupSeconds + CircuitProfile.TakeoffSeconds;
        public static long TakeoffRunwaySecondsFor(AircraftType type) =>
            TakeoffRunwaySecondsFor(type, RunwayDirection.Runway05);
        public static long TakeoffRunwaySecondsFor(AircraftType type, RunwayDirection runway) =>
            AdelaideGround.LineupFor(runway).WholeSeconds + AircraftPerformance.For(type).TakeoffSeconds;

        /// <summary>
        /// Runway time from the landing clearance on long final through flare, rollout
        /// and vacating — the whole of it is drawn in 3D, so it is flown in full.
        /// </summary>
        public static long LandingRunwaySeconds =>
            CircuitProfile.ApproachSeconds + CircuitProfile.LandingSeconds + VacateSeconds;
        public static long LandingRunwaySecondsFor(AircraftType type) =>
            LandingRunwaySecondsFor(type, RunwayDirection.Runway05);

        public static long LandingRunwaySecondsFor(AircraftType type, RunwayDirection runway)
        {
            var profile = AircraftPerformance.For(type);
            return profile.ApproachSeconds + profile.LandingSeconds
                   + AdelaideGround.VacateFor(type, runway).WholeSeconds;
        }

        public static readonly IReadOnlyList<StableId> AdelaideRegionalBays = StandIds(AdelaideLayout.Bays);

        /// <summary>Terminal gates (ADR 0047): jets only, a separate stand system from the regional bays.</summary>
        public static readonly IReadOnlyList<StableId> AdelaideTerminalGates = StandIds(AdelaideLayout.TerminalGates);

        /// <summary>Every stand at Adelaide: the regional bays, then the terminal gates.</summary>
        public static readonly IReadOnlyList<StableId> AdelaideStands = CombinedStands();

        private static IReadOnlyList<StableId> StandIds(AdelaideBay[] bays)
        {
            var ids = new StableId[bays.Length];
            for (var i = 0; i < bays.Length; i++)
                ids[i] = new StableId(bays[i].Id);
            return ids;
        }

        private static IReadOnlyList<StableId> StandIds(AdelaideTerminalGate[] gates)
        {
            var ids = new StableId[gates.Length];
            for (var i = 0; i < gates.Length; i++)
                ids[i] = new StableId(gates[i].Id);
            return ids;
        }

        private static IReadOnlyList<StableId> CombinedStands()
        {
            var all = new StableId[AdelaideRegionalBays.Count + AdelaideTerminalGates.Count];
            for (var i = 0; i < AdelaideRegionalBays.Count; i++)
                all[i] = AdelaideRegionalBays[i];
            for (var i = 0; i < AdelaideTerminalGates.Count; i++)
                all[AdelaideRegionalBays.Count + i] = AdelaideTerminalGates[i];
            return all;
        }

        /// <summary>
        /// L/R parking lines on the same T1 pier — AIP treats them as one gate.
        /// GATE-18 and GATE-20 are the original 18L / 20L ids.
        /// </summary>
        public static readonly IReadOnlyList<(StableId A, StableId B)> SharedPierPairs = new[]
        {
            (new StableId("GATE-16L"), new StableId("GATE-16R")),
            (new StableId("GATE-18"), new StableId("GATE-18R")),
            (new StableId("GATE-20"), new StableId("GATE-20R")),
            (new StableId("GATE-22L"), new StableId("GATE-22R")),
            (new StableId("GATE-28L"), new StableId("GATE-28R")),
        };

        /// <summary>
        /// The terminal jet operator and its aircraft, tied to its gate. New games
        /// start with it; older saves gain it on load (<see cref="AddMissingTerminalOperators"/>).
        /// </summary>
        public static readonly IReadOnlyList<(Func<Airline> Make, (string Registration, AircraftType Type, StableId Gate)[] Fleet)> TerminalOperators = new (Func<Airline>, (string, AircraftType, StableId)[])[]
        {
            (Airline.VirginAustralia, new[]
            {
                ("VH-8IA", AircraftType.Boeing7378, new StableId("GATE-13")),
                ("VH-8IB", AircraftType.Boeing7378, new StableId("GATE-14L")),
                ("VH-8IC", AircraftType.Boeing7378, new StableId("GATE-19"))
            }),
            (Airline.Qantas, new[]
            {
                ("VH-VZX", AircraftType.Boeing737800, new StableId("GATE-21")),
                ("VH-VZY", AircraftType.Boeing737800, new StableId("GATE-24")),
                ("VH-VZZ", AircraftType.Boeing737800, new StableId("GATE-23"))
            }),
            (Airline.Jetstar, new[]
            {
                ("VH-VFH", AircraftType.AirbusA320200, new StableId("GATE-17")),
                ("VH-VFI", AircraftType.AirbusA321Neo, new StableId("GATE-16L"))
            }),
            (Airline.AirNewZealand, new[] { ("ZK-NNA", AircraftType.AirbusA321Neo, new StableId("GATE-15")) }),
            (Airline.CathayPacific, new[] { ("B-LRB", AircraftType.AirbusA350900, new StableId("GATE-18")) }),
            (Airline.SingaporeAirlines, new[] { ("9V-SCA", AircraftType.Boeing78710, new StableId("GATE-20")) }),
            (Airline.MalaysiaAirlines, new[] { ("9M-MAB", AircraftType.AirbusA330900, new StableId("GATE-25")) }),
            (Airline.Emirates, new[] { ("A6-EVA", AircraftType.AirbusA350900, new StableId("GATE-22L")) }),
            (Airline.QatarAirways, new[] { ("A7-ANC", AircraftType.AirbusA350900, new StableId("GATE-26L")) }),
            (Airline.FijiAirways, new[] { ("DQ-FAE", AircraftType.Boeing7378, new StableId("GATE-12L")) })
        };

        /// <summary>
        /// Virgin Australia's representative mainland rotation — Melbourne and
        /// Sydney most, then Brisbane, Perth and Canberra. Deterministic and drawn from no random
        /// numbers, so adding the jet leaves the regional carriers' random sequence untouched.
        /// </summary>
        public static readonly IReadOnlyList<string> VirginRotation = new[] { "MEL", "SYD", "MEL", "BNE", "SYD", "PER", "MEL", "CBR" };

        /// <summary>Representative Air New Zealand trans-Tasman rotation from Adelaide.</summary>
        public static readonly IReadOnlyList<string> AirNewZealandRotation = new[] { "AKL", "AKL", "CHC", "AKL" };

        /// <summary>Qantas mainline rotation from Adelaide — east-coast heaviest, plus Auckland.</summary>
        public static readonly IReadOnlyList<string> QantasRotation = new[] { "SYD", "MEL", "BNE", "AKL", "PER", "MEL", "SYD", "CBR", "BNE" };

        /// <summary>Jetstar domestic rotation from Adelaide, plus the busy Bali leisure run.</summary>
        public static readonly IReadOnlyList<string> JetstarRotation = new[] { "MEL", "SYD", "BNE", "DPS", "OOL", "MEL", "PER" };

        /// <summary>Jets use terminal gates; turboprops use the regional bays. Never the other way.</summary>
        public static bool NeedsTerminalGate(AircraftType type) =>
            AircraftCatalogue.TryFor(type, out var spec) && spec.StandClass == StandClass.TerminalGate;

        /// <summary>AIP walk-out stands 2A and 10A–10D are SF340 / marshaller only.</summary>
        public static bool IsWalkOutStand(StableId stand)
        {
            foreach (var bay in AdelaideLayout.Bays)
            {
                if (bay.Id != stand.Value)
                    continue;
                return bay.Reference is "2A" or "10A" or "10B" or "10C" or "10D";
            }

            return false;
        }

        public static bool StandFits(AircraftType type, StableId stand)
        {
            if (AdelaideGround.IsTerminalGate(stand) != NeedsTerminalGate(type))
                return false;
            // AIP walk-outs are SF340 / marshaller only — not ATR or Dash 8.
            return !IsWalkOutStand(stand) || ReferenceEquals(type, AircraftType.Saab340);
        }

        /// <summary>
        /// Real regional carriers that share Adelaide's regional apron with the player and
        /// the player's airline. New games start with them; older saves gain them on load
        /// (<see cref="AddMissingRegionalCarriers"/>). Three Rex Saabs and three
        /// QantasLink Q400s share the 50-series and walk-outs with the player.
        /// Extra 50G and the 10-series sit empty until someone needs them.
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
        private SimulationTime _mainRunwayFreeAt;
        private SimulationTime _crossRunwayFreeAt;

        public AirlineOperations(ISimulationClock clock, IRandomSource random, Destination home, IReadOnlyList<StableId> stands)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            if (stands == null || stands.Count == 0)
                throw new ArgumentException("At least one stand is required.", nameof(stands));

            Home = home;
            _stands = new List<StableId>(stands);
            _processedTo = clock.Now;
            _mainRunwayFreeAt = clock.Now;
            _crossRunwayFreeAt = clock.Now;
            CareerState = new AirlineCareerState();
        }

        /// <summary>
        /// Staggered opening departures — about one every five minutes, a normal
        /// Adelaide peak rather than a three-minute pile-up.
        /// </summary>
        public static readonly long[] AiOpeningDepartureSeconds =
            { 3 * 60, 7 * 60, 12 * 60, 17 * 60, 22 * 60, 27 * 60, 33 * 60, 39 * 60 };

        /// <summary>
        /// The ADR 0045 / 0077 starting position at Adelaide: the player's airline with one
        /// Saab 340B, real Adelaide operators on the apron, and a bank of aircraft already inbound.
        /// </summary>
        public static AirlineOperations StartAtAdelaide(ISimulationClock clock, IRandomSource random, Airline player,
            AirlineClock airlineClock = null)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (!player.IsPlayer) throw new ArgumentException("The starting airline must be the player's.", nameof(player));

            var operations = new AirlineOperations(clock, random, DestinationCatalogue.Adelaide, AdelaideStands);
            operations.AddAirline(player);
            operations.AddAircraft(player, "VH-PAX", AircraftType.Saab340, AdelaideRegionalBays[0]);
            var aiFleet = new List<FleetAircraft>();
            operations.AddMissingRegionalCarriers(aiFleet);
            // Opening peak: regionals on 12/30, jets on 05/23, about one arrival
            // every three minutes across the field — a busy Adelaide morning, not
            // a dump and then a hole. One QantasLink and the player stay parked
            // so the 50-series apron is not empty.
            operations.TrySeedOpeningInbound(aiFleet, "QLK", "PLO", 2 * 60);
            operations.TrySeedOpeningInbound(aiFleet, "REX", "MGB", 5 * 60);
            operations.TrySeedOpeningInbound(aiFleet, "REX", "PLO", 9 * 60);
            operations.TrySeedOpeningInbound(aiFleet, "REX", "CED", 21 * 60);

            var terminalFleet = new List<FleetAircraft>();
            operations.AddMissingTerminalOperators(terminalFleet);
            operations.TrySeedOpeningInbound(terminalFleet, "ANZ", "AKL", 7 * 60);
            operations.TrySeedOpeningInbound(terminalFleet, "VOZ", "MEL", 11 * 60);
            operations.TrySeedOpeningInbound(terminalFleet, "QFA", "SYD", 15 * 60);
            operations.TrySeedOpeningInbound(terminalFleet, "JST", "MEL", 18 * 60);
            operations.TrySeedOpeningInbound(terminalFleet, "VOZ", "SYD", 24 * 60);
            operations.TrySeedOpeningInbound(terminalFleet, "SIA", "SIN", 28 * 60);
            operations.TrySeedOpeningInbound(terminalFleet, "QFA", "BNE", 32 * 60);
            operations.TrySeedOpeningInbound(terminalFleet, "JST", "SYD", 37 * 60);
            operations.TrySeedOpeningInbound(terminalFleet, "MAS", "KUL", 41 * 60);
            operations.TrySeedOpeningInbound(terminalFleet, "FJI", "NAN", 45 * 60);
            operations.TrySeedOpeningInbound(terminalFleet, "UAE", "DXB", 49 * 60);
            operations.TrySeedOpeningInbound(terminalFleet, "QTR", "DOH", 54 * 60);

            var departureIndex = 0;
            foreach (var aircraft in operations.Fleet)
            {
                if (departureIndex >= AiOpeningDepartureSeconds.Length)
                    break;
                if (aircraft.Airline.IsPlayer || aircraft.State != FleetState.AtStand || aircraft.Scheduled is not { } first)
                    continue;
                aircraft.Scheduled = new ScheduledDeparture(first.Destination,
                    operations.ProcessedTo.Advance(AiOpeningDepartureSeconds[departureIndex++]));
            }

            operations.Clock = airlineClock ?? AirlineClock.Default;
            return operations;
        }

        private void TrySeedOpeningInbound(List<FleetAircraft> fleet, string airlineId, string destinationCode,
            long secondsToCircuit)
        {
            if (fleet == null || !DestinationCatalogue.TryFind(destinationCode, out var destination))
                return;
            for (var i = fleet.Count - 1; i >= 0; i--)
            {
                var aircraft = fleet[i];
                if (aircraft.Airline.Id.Value != airlineId || aircraft.State != FleetState.AtStand)
                    continue;
                SeedOpeningInbound(aircraft, destination, secondsToCircuit);
                return;
            }
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
                    var stand = gate;
                    if (!_stands.Contains(stand) || !IsStandFree(stand) || !StandFits(type, stand))
                    {
                        // Seasonal Cathay used to vanish for the whole summer when GATE-18
                        // (or its pier sibling) was taken. Fall back to any free fitting gate.
                        var alt = SuggestStandFor(type);
                        if (!alt.HasValue)
                            continue;
                        stand = alt.Value;
                    }

                    if (airline == null)
                    {
                        airline = template;
                        AddAirline(airline);
                    }

                    var aircraft = AddAircraft(airline, registration, type, stand);
                    added?.Add(aircraft);
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Cathay's summer service leaves when the season ends. Aircraft still flying
        /// finish their trip; once they are back on the stand they are removed so
        /// GATE-18 is not held through winter.
        /// </summary>
        public int RetireOutOfSeasonOperators(SimulationTime? at = null)
        {
            var asOf = at ?? _processedTo;
            if (IsCathaySeason(asOf))
                return 0;

            var removed = 0;
            for (var i = _fleet.Count - 1; i >= 0; i--)
            {
                var aircraft = _fleet[i];
                if (aircraft.Airline.Id.Value != "CPA")
                    continue;
                if (aircraft.State != FleetState.AtStand)
                    continue;
                aircraft.Scheduled = null;
                aircraft.PrepStartedAt = null;
                _fleet.RemoveAt(i);
                removed++;
            }

            if (removed > 0 && !_fleet.Exists(a => a.Airline.Id.Value == "CPA"))
            {
                for (var i = _airlines.Count - 1; i >= 0; i--)
                    if (_airlines[i].Id.Value == "CPA")
                        _airlines.RemoveAt(i);
            }

            return removed;
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

        /// <summary>When the tower may next clear a movement on the 05/23 strip.</summary>
        public SimulationTime RunwayFreeAt => _mainRunwayFreeAt;

        /// <summary>When the tower may next clear a movement on the 12/30 strip.</summary>
        public SimulationTime CrossRunwayFreeAt => _crossRunwayFreeAt;

        public SurfaceWind Wind => RunwayWeather.At(Clock, _processedTo);
        public RunwayDirection ActiveRunway => RunwayWeather.Select(Wind);

        /// <summary>Current forecast, for HUD and tower gating alike.</summary>
        public WeatherKind CurrentWeather => Weather.At(_processedTo);

        /// <summary>
        /// True while a storm holds every new landing/takeoff clearance (ADR 0058).
        /// Movements already underway continue; this only stops the tower handing out
        /// the next one.
        /// </summary>
        public bool IsGroundStopped => CurrentWeather == WeatherKind.Storm;

        /// <summary>Wind-selected end of the cross strip (12/30), for HUD and planners.</summary>
        public RunwayDirection ActiveCrossRunway =>
            RunwayWeather.Select(Wind, AircraftType.Atr42, null, Home);

        /// <summary>The runway this aircraft should use right now, given wind, type and destination.</summary>
        public RunwayDirection RunwayFor(FleetAircraft aircraft) =>
            RunwayWeather.Select(Wind, aircraft?.Type, DestinationForRunwayChoice(aircraft), Home);

        /// <summary>
        /// Dest-aligned runway preference is for departures (Perth jets take 23 in a
        /// light easterly). Arrivals and go-around rejoins follow the wind only —
        /// otherwise light wind lands them on the end they would take off toward.
        /// </summary>
        private static Destination? DestinationForRunwayChoice(FleetAircraft aircraft)
        {
            if (aircraft == null)
                return null;
            if (aircraft.State is FleetState.Inbound or FleetState.GoAround
                or FleetState.HoldingForLanding or FleetState.Landing
                or FleetState.AwaitingStand or FleetState.TaxiIn
                or FleetState.AtDestination)
                return null;
            return aircraft.CurrentDestination ?? aircraft.Scheduled?.Destination;
        }

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
                throw new InvalidOperationException($"{Article.CapitalA(type.Name)} cannot park on {stand}.");

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
            if (RequiresDepartureStand(state) && string.IsNullOrEmpty(departureStand.Value))
                throw new FormatException($"{registration} is {state} with no departure stand.");
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

        internal void RestorePrepData(string registration, SimulationTime? prepStartedAt)
        {
            var aircraft = _fleet.Find(a => string.Equals(a.Registration, registration, StringComparison.OrdinalIgnoreCase));
            if (aircraft == null)
                throw new FormatException($"{registration}: prep data has no aircraft.");
            aircraft.PrepStartedAt = prepStartedAt;
        }

        internal void RestorePushbackLateness(string registration, int latenessSeconds)
        {
            var aircraft = _fleet.Find(a => string.Equals(a.Registration, registration, StringComparison.OrdinalIgnoreCase));
            if (aircraft == null)
                throw new FormatException($"{registration}: pushback lateness has no aircraft.");
            aircraft.PushbackLatenessSeconds = latenessSeconds;
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

        /// <summary>
        /// Pre-dual-strip saves leave <see cref="CrossRunwayFreeAt"/> at 0. Prefer
        /// <see cref="ReconcileRunwayFreeAt"/>; this entry keeps older call sites working.
        /// </summary>
        internal void ReconcileCrossRunwayFreeAt() => ReconcileStripFreeAt(mainStrip: false);

        private void ReconcileStripFreeAt(bool mainStrip)
        {
            SimulationTime? holdUntil = null;
            foreach (var aircraft in _fleet)
            {
                if (aircraft.State is not (FleetState.TakingOff or FleetState.Landing))
                    continue;
                if (RunwayWeather.IsMainRunway(aircraft.AssignedRunway) != mainStrip)
                    continue;
                var until = StripBusyUntil(aircraft) ?? _processedTo;
                until = until.Advance(WakeSeparationSeconds(aircraft.Type));
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

        /// <summary>
        /// True while this aircraft still occupies its assigned strip. Landing
        /// keeps the long taxi to E2 in its state duration so the drawing does
        /// not teleport; the strip itself is free once the aircraft is clear
        /// of the pavement.
        /// </summary>
        public bool IsOccupyingRunway(FleetAircraft aircraft)
        {
            if (aircraft == null)
                return false;
            if (aircraft.State is not (FleetState.TakingOff or FleetState.Landing))
                return false;
            var until = StripBusyUntil(aircraft);
            return until.HasValue && until.Value.CompareTo(_clock.Now) > 0;
        }

        /// <summary>
        /// When the strip may accept the next movement. Landing frees at clear-of-runway,
        /// not after the long taxi to E2 that still belongs to the Landing state.
        /// </summary>
        private static SimulationTime? StripBusyUntil(FleetAircraft aircraft)
        {
            if (aircraft.State != FleetState.Landing || !aircraft.StateEndsAt.HasValue)
                return aircraft.StateEndsAt;

            if (IsMissedApproachLanding(aircraft))
                return aircraft.StateEndsAt;

            var vacate = AdelaideGround.VacateFor(aircraft.Type, aircraft.AssignedRunway).WholeSeconds;
            var clear = AdelaideGround.ClearOfRunwaySeconds(aircraft.Type, aircraft.AssignedRunway);
            var duration = aircraft.StateEndsAt.Value.ElapsedSeconds - aircraft.StateStartedAt.ElapsedSeconds;
            if (duration <= vacate)
                return aircraft.StateEndsAt;

            // duration = final + roll + vacate; free when clear, which is earlier than vacate end.
            return aircraft.StateStartedAt.Advance(duration - vacate + clear);
        }

        /// <summary>Rebuilds career state (ADR 0053 / 0056) from a v6+ save, or a fresh
        /// Provisional state when migrating an older one — never from anything it doesn't recognise.</summary>
        internal void RestoreCareerState(
            long funds, int reliability, string tier, string activeContractId,
            long contractAcceptedAtSeconds, int contractCompletedRotations, IEnumerable<string> processedSettlementKeys,
            IEnumerable<string> completedContractIds = null, int completedPlayerRotations = 0,
            RouteContractDefinition activeSnapshot = null, long lifetimeRevenue = 0,
            IEnumerable<CompletedContractRecord> contractHistory = null)
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

            CareerState = new AirlineCareerState(funds, reliability, parsedTier, contract, processedSettlementKeys,
                completedContractIds, completedPlayerRotations, issued, lifetimeRevenue, contractHistory);
        }

        // ---- Queries -------------------------------------------------------------

        public double DistanceKm(Destination destination) => Home.DistanceKmTo(destination);

        public bool CanReach(FleetAircraft aircraft, Destination destination) =>
            !destination.Equals(Home) && aircraft.Type.CanReach(DistanceKm(destination));

        /// <summary>
        /// Range plus, for the player, the type's route band (ADR 0056). AI still uses range
        /// alone — they already fly their own networks.
        /// </summary>
        public bool CanOperate(FleetAircraft aircraft, Destination destination) =>
            CanReach(aircraft, destination)
            && (!aircraft.Airline.IsPlayer || RouteAccess.Allows(aircraft.Type, destination));

        /// <summary>Distinct types the player currently owns, for the contract market and purchase gates.</summary>
        public List<AircraftType> PlayerOwnedTypes()
        {
            var types = new List<AircraftType>();
            if (PlayerAirline == null)
                return types;
            foreach (var aircraft in _fleet)
            {
                if (!aircraft.Airline.IsPlayer)
                    continue;
                var seen = false;
                foreach (var existing in types)
                    if (existing.Id == aircraft.Type.Id)
                        seen = true;
                if (!seen)
                    types.Add(aircraft.Type);
            }

            return types;
        }

        public IReadOnlyList<RouteContractDefinition> MarketOffers() =>
            ContractMarket.At(_processedTo, PlayerOwnedTypes(), CareerState.Reliability, CareerState.Tier);

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
            {
                if (StandHolder(aircraft, stand) || StandHolder(aircraft, PierSibling(stand)))
                    return false;
            }

            return true;
        }

        private static StableId PierSibling(StableId stand)
        {
            foreach (var (a, b) in SharedPierPairs)
            {
                if (stand.Equals(a))
                    return b;
                if (stand.Equals(b))
                    return a;
            }

            return default;
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
                // Bays and gates both stay held through taxi-out so a landing cannot
                // take the same stand while the departure is still on the apron.
                if (aircraft.State == FleetState.TaxiOut && aircraft.DepartureStand.Value == resource)
                    return aircraft;
                if (UsesLeadIn(aircraft, out var gate) && AdelaideGround.LeadInResource(gate) == resource)
                    return aircraft;
            }

            return null;
        }

        /// <summary>
        /// Stands stay held through taxi-out (pushback and the first apron chord) and
        /// while taxiing in, so a second aircraft cannot take the bay mid-push.
        /// </summary>
        private static bool StandHolder(FleetAircraft aircraft, StableId stand)
        {
            if (HoldsStand(aircraft) && aircraft.Stand.Equals(stand))
                return true;
            return aircraft.State == FleetState.TaxiOut && aircraft.DepartureStand.Equals(stand);
        }

        /// <summary>A gate's lead-in is in use while an aircraft taxis in to it, out from it,
        /// or is still on the apron after pushback (holding short / lining up).</summary>
        private static bool UsesLeadIn(FleetAircraft aircraft, out StableId gate)
        {
            gate = aircraft.State switch
            {
                FleetState.TaxiIn => aircraft.Stand,
                FleetState.TaxiOut or FleetState.HoldingShort or FleetState.TakingOff
                    => aircraft.DepartureStand,
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
            SimulationTime? earliest = null;
            var rolling = 0;
            foreach (var aircraft in _fleet)
            {
                if (aircraft.State != FleetState.TaxiOut)
                    continue;
                if (AdelaideGround.IsTerminalGate(aircraft.DepartureStand) != terminalGate)
                    continue;
                rolling++;
                var candidate = aircraft.StateStartedAt.Advance(
                    TaxiClearSecondsFrom(aircraft.DepartureStand, aircraft.Type, aircraft.AssignedRunway));
                if (candidate.CompareTo(now) > 0 && (earliest == null || candidate.CompareTo(earliest.Value) < 0))
                    earliest = candidate;
            }

            return rolling >= MaxSimultaneousTaxiOutsPerApron ? earliest : null;
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
                    if (aircraft.Scheduled.Value.Cancelled)
                    {
                        if (!aircraft.Airline.IsPlayer)
                            Consider(aircraft.Scheduled.Value.DepartAt.Advance(3 * 3600));
                    }
                    else
                    {
                        var readyAt = aircraft.Scheduled.Value.DepartAt;
                        if (aircraft.Airline.IsPlayer)
                        {
                            var prepEnd = (aircraft.PrepStartedAt ?? now)
                                .Advance(DeparturePrep.TotalSeconds(aircraft.Type));
                            if (prepEnd.CompareTo(readyAt) > 0)
                                readyAt = prepEnd;
                        }

                        Consider(readyAt);
                        if (readyAt.CompareTo(now) <= 0)
                        {
                            // Ready but still at the stand: it may be waiting for the ground to
                            // clear, which is re-checked on the grid.
                            Consider(GroundTraffic.NextGrid(now));
                            if (AdelaideGround.IsTerminalGate(aircraft.Stand))
                                taxiReleaseWantedGate = true;
                            else
                                taxiReleaseWantedBay = true;
                        }
                    }
                }
                if (aircraft.State is FleetState.HoldingShort or FleetState.HoldingForLanding)
                    runwayWanted = true;
                // Waiting at the exit with a stand to go to: the taxi-in may be held for traffic.
                if (aircraft.State == FleetState.AwaitingStand && SuggestStand(aircraft) != null)
                    Consider(GroundTraffic.NextGrid(now));
            }

            if (runwayWanted)
            {
                Consider(_mainRunwayFreeAt);
                Consider(_crossRunwayFreeAt);
                // An arrival still holding with its strip already free is being held for taxiing
                // traffic (or a storm): re-check it on the ground-control grid.
                foreach (var aircraft in _fleet)
                {
                    if (aircraft.State != FleetState.HoldingForLanding)
                        continue;
                    var stripFree = RunwayWeather.IsMainRunway(aircraft.AssignedRunway) ? _mainRunwayFreeAt : _crossRunwayFreeAt;
                    if (stripFree.CompareTo(now) <= 0)
                    {
                        Consider(GroundTraffic.NextGrid(now));
                        break;
                    }
                }
                // ADR 0058: a strip can sit free-at-or-before now yet still be withheld by
                // a storm, which RunTowerOnStrip checks against `now` itself rather than
                // any tracked "reopens at" time. Without this, a big skip-to-next-event
                // step could land past the moment the storm actually cleared and grant a
                // clearance later than a series of small steps would have — the same
                // storm, checked at a different `now`, must not answer differently. The
                // next weather block boundary is always a candidate stop while a runway is
                // wanted and it is currently storm-closed, so catch-up revisits the check
                // at the same granularity live play would.
                if (Weather.At(now) == WeatherKind.Storm)
                    Consider(new SimulationTime((now.ElapsedSeconds / Weather.BlockSeconds + 1) * Weather.BlockSeconds));
            }
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
            if (aircraft.Airline.IsPlayer && !RouteAccess.Allows(aircraft.Type, destination))
                return CommandResult.Refused(
                    $"{Article.CapitalA(aircraft.Type.Name)} is cleared for {RouteAccess.Ceiling(aircraft.Type)} routes — {destination.Name} is {RouteAccess.BandOf(destination)}.");
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
                // Align prep with the booked pushback: start TotalSeconds before depart
                // (or now if that is already later). Recompute on every book so an earlier
                // rebook cannot leave a future PrepStartedAt that blocks pushback forever.
                var total = DeparturePrep.TotalSeconds(aircraft.Type);
                var start = departAt.ElapsedSeconds - total;
                if (start < _processedTo.ElapsedSeconds)
                    start = _processedTo.ElapsedSeconds;
                if (start < 0)
                    start = 0;
                // Keep progress already pumped when the new start is not later than the old one.
                if (!aircraft.PrepStartedAt.HasValue
                    || aircraft.PrepStartedAt.Value.ElapsedSeconds > start)
                    aircraft.PrepStartedAt = new SimulationTime(start);
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
                && CareerState.TryFindDefinition(CareerState.ActiveContract.DefinitionId, out var contract)
                && contract.EligibleType == aircraft.Type
                && contract.MatchesRoute(Home.Code, aircraft.Scheduled.Value.Destination.Code))
                CareerState.PenalizeCancellation(contract.Id, contract.ReliabilityLossOnCancel);

            aircraft.Scheduled = null;
            aircraft.PrepStartedAt = null;
            return CommandResult.Ok;
        }

        /// <summary>
        /// Accepts a route contract (authored or a live market offer) as the player's one
        /// active career contract. Market definitions are remembered so settlement still
        /// works after the offer window rolls (ADR 0056).
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

            CareerState.Remember(definition);
            CareerState.ActiveContract = new ActiveRouteContract(definition.Id, _processedTo);
            return CommandResult.Ok;
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
            if (owned >= AircraftAcquisition.MaxPlayerAircraft)
                return CommandResult.Refused($"Fleet is full ({AircraftAcquisition.MaxPlayerAircraft} aircraft).");
            if (CareerState.Tier < offer.RequiredTier)
                return CommandResult.Refused($"Buying {Article.A(type.Name)} needs {offer.RequiredTier} tier.");
            if (CareerState.Reliability < offer.RequiredReliability)
                return CommandResult.Refused($"Buying {Article.A(type.Name)} needs {offer.RequiredReliability}% reliability.");
            if (CareerState.CompletedPlayerRotations < offer.RequiredRotations)
                return CommandResult.Refused(
                    $"Buying {Article.A(type.Name)} needs {offer.RequiredRotations} completed rotations.");
            if (!CareerState.CanAfford(offer.Price))
                return CommandResult.Refused($"{Article.CapitalA(type.Name)} costs ${offer.Price:N0}; you have ${CareerState.Funds:N0}.");

            var stand = SuggestPurchaseStand(type);
            if (!CareerState.TryChargePurchase(offer.Price))
                return CommandResult.Refused($"{Article.CapitalA(type.Name)} costs ${offer.Price:N0}; you have ${CareerState.Funds:N0}.");

            var registration = NextPlayerRegistration(_fleet);
            if (stand.HasValue)
                AddAircraft(PlayerAirline, registration, type, stand.Value);
            else
                AddDeliveryInbound(PlayerAirline, registration, type);

            CareerState.EvaluateTier(PlayerOwnedTypes());
            ClaimCampaignRewards();
            return CommandResult.Ok;
        }

        /// <summary>The career campaign as the airline stands now (ADR 0083).</summary>
        public IReadOnlyList<CampaignChapter> CampaignChapters() =>
            Campaign.Evaluate(CareerState, PlayerAirline == null ? null : PlayerOwnedTypes());

        /// <summary>Pays each newly completed campaign chapter's reward, once.</summary>
        private bool ClaimCampaignRewards()
        {
            if (CareerState == null || PlayerAirline == null)
                return false;
            var paid = false;
            foreach (var chapter in CampaignChapters())
            {
                if (!chapter.Complete)
                    break;
                if (!chapter.Rewarded)
                    paid |= CareerState.TryAward(Campaign.RewardKey(chapter.Number), chapter.Reward);
            }

            return paid;
        }

        /// <summary>Renames the player's own airline. AI operators use real airline names and cannot be renamed.</summary>
        public CommandResult RenameAirline(string name)
        {
            if (PlayerAirline == null)
                return CommandResult.Refused("No player airline.");
            try
            {
                PlayerAirline.Rename(name);
            }
            catch (ArgumentException e)
            {
                return CommandResult.Refused(e.Message);
            }

            return CommandResult.Ok;
        }

        /// <summary>Repaints the player's own airline's livery. AI liveries are authored and fixed.</summary>
        public CommandResult SetLivery(string liveryHex)
        {
            if (PlayerAirline == null)
                return CommandResult.Refused("No player airline.");
            try
            {
                PlayerAirline.Repaint(liveryHex);
            }
            catch (ArgumentException e)
            {
                return CommandResult.Refused(e.Message);
            }

            return CommandResult.Ok;
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
                return CommandResult.Refused("Only your own aircraft can be sold.");
            if (aircraft.State != FleetState.AtStand)
                return CommandResult.Refused($"{aircraft.Registration} must be parked at its stand to sell.");
            if (!AircraftAcquisition.TryFor(aircraft.Type, out var offer))
                return CommandResult.Refused($"{aircraft.Type.Name} has no resale listing.");

            var refund = (long)Math.Round(offer.Price * ResaleFraction);
            CareerState.RefundDispatch(refund);
            _fleet.Remove(aircraft);
            return CommandResult.Ok;
        }

        /// <summary>
        /// Resale value as a fraction of the original purchase price — well under 1 so buying
        /// an aircraft only to immediately resell it is always a loss, the same way it would be
        /// for a real operator paying for delivery, registration and crew training up front.
        /// </summary>
        public const double ResaleFraction = 0.55;

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
                && CareerState.TryFindDefinition(contract.DefinitionId, out var definition)
                && definition.EligibleType == aircraft.Type
                && definition.MatchesRoute(Home.Code, justFlown.Value.Code))
                matching = definition;

            var settlementId = new SettlementId(aircraft.Registration, aircraft.CompletedTrips);
            var pay = FlightEconomics.FlightPay(aircraft.Type, DistanceKm(justFlown.Value),
                RouteAccess.BandOf(justFlown.Value));
            if (aircraft.Airline.IsPlayer && aircraft.PushbackLatenessSeconds.HasValue)
            {
                CareerState.ApplyPunctuality(
                    FlightEconomics.PunctualityReliabilityDelta(aircraft.PushbackLatenessSeconds.Value));
                aircraft.PushbackLatenessSeconds = null;
            }

            var settlement = CareerState.RecordCompletedRotation(
                settlementId, pay, matching, PlayerOwnedTypes(), now);
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
                return CommandResult.Refused($"{Article.CapitalA(aircraft.Type.Name)} cannot use {AdelaideGround.StandLabel(stand)}.");
            if (!IsStandFree(stand))
                return CommandResult.Refused($"{stand} is occupied.");
            if (AdelaideGround.IsTerminalGate(stand) && !IsLeadInFree(stand, aircraft))
                return CommandResult.Refused($"{AdelaideGround.StandLabel(stand)}'s lead-in is in use.");

            aircraft.Stand = stand;
            Transition(aircraft, FleetState.TaxiIn, _processedTo, TaxiInSecondsTo(stand, aircraft.Type, aircraft.AssignedRunway));
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
            else
                RetireOutOfSeasonOperators(at: target);

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
                changed |= ClaimCampaignRewards();
            } while (changed);
        }

        private bool AdvanceAircraft(FleetAircraft aircraft, SimulationTime now)
        {
            if (aircraft.StateEndsAt.HasValue && aircraft.StateEndsAt.Value.CompareTo(now) > 0)
                return false;

            switch (aircraft.State)
            {
                case FleetState.AtStand:
                    if (aircraft.Scheduled is { Cancelled: true } cancelled)
                    {
                        if (!aircraft.Airline.IsPlayer
                            && now.ElapsedSeconds >= cancelled.DepartAt.ElapsedSeconds + 3 * 3600)
                        {
                            aircraft.Scheduled = null;
                            ScheduleAiDeparture(aircraft, now);
                            return aircraft.Scheduled.HasValue;
                        }

                        return false;
                    }

                    if (!aircraft.Scheduled.HasValue || aircraft.Scheduled.Value.DepartAt.CompareTo(now) > 0)
                        return false;
                    if (aircraft.Airline.IsPlayer && !aircraft.PrepStartedAt.HasValue)
                    {
                        var inferred = aircraft.Scheduled.Value.DepartAt.ElapsedSeconds
                            - DeparturePrep.TotalSeconds(aircraft.Type);
                        aircraft.PrepStartedAt = new SimulationTime(inferred < 0 ? 0 : inferred);
                    }
                    if (!DeparturePrep.IsReady(aircraft, now))
                        return false;
                    var pushingBackFromGate = AdelaideGround.IsTerminalGate(aircraft.Stand);
                    if (NextTaxiReleaseAt(now, pushingBackFromGate).HasValue)
                        return false;
                    // A gate pushback needs its lead-in clear before the tug moves; it is re-checked
                    // whenever anything else finishes, since that is the only way it frees.
                    if (pushingBackFromGate && !IsLeadInFree(aircraft.Stand, aircraft))
                        return false;
                    // Ground control: push only when the whole route to the runway is clear of
                    // the traffic already moving. A departure that has to wait for it goes on the
                    // grid, so the moment it moves does not depend on how the clock is stepped.
                    var departureRunway = RunwayFor(aircraft);
                    var readyAt = DepartureReadyAt(aircraft);
                    if (!now.Equals(readyAt) && !GroundTraffic.OnGrid(now))
                        return false;
                    if (!GroundTraffic.PathClear(_fleet, aircraft,
                            AdelaideGround.TaxiOut(aircraft.Stand, aircraft.Type, departureRunway),
                            departureRunway, taxiOut: true, now,
                            includeStationary: now.ElapsedSeconds - readyAt.ElapsedSeconds < GroundTraffic.MaxWaitSeconds))
                        return false;
                    if (aircraft.Airline.IsPlayer)
                    {
                        var lateness = (int)(now.ElapsedSeconds - aircraft.Scheduled.Value.DepartAt.ElapsedSeconds);
                        aircraft.PushbackLatenessSeconds = lateness;
                    }

                    aircraft.CurrentDestination = aircraft.Scheduled.Value.Destination;
                    aircraft.Scheduled = null;
                    aircraft.PrepStartedAt = null;
                    aircraft.DepartureStand = aircraft.Stand;
                    aircraft.Stand = default;
                    aircraft.AssignedRunway = departureRunway;
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
                    var inboundSeconds = LegAirborne(aircraft);
                    if (!aircraft.Airline.IsPlayer)
                    {
                        var inbound = FlightDisruption.For(
                            $"{aircraft.Registration}:in:{aircraft.CompletedTrips}", now, Clock);
                        if (inbound.Cancelled)
                            inboundSeconds += 3 * 3600;
                        else
                            inboundSeconds += inbound.DelayMinutes * 60L;
                    }

                    Transition(aircraft, FleetState.Inbound, now, inboundSeconds);
                    return true;

                case FleetState.Inbound:
                    aircraft.AssignedRunway = RunwayFor(aircraft);
                    Transition(aircraft, FleetState.HoldingForLanding, now, null);
                    return true;

                case FleetState.GoAround:
                    aircraft.AssignedRunway = RunwayFor(aircraft);
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
                    var chosen = SuggestStand(aircraft);
                    if (chosen == null)
                        return false;
                    // Ground control, as for a pushback: the taxi-in waits at the exit until its
                    // route is clear, moving only on the grid once it has had to wait.
                    if (!now.Equals(aircraft.StateStartedAt) && !GroundTraffic.OnGrid(now))
                        return false;
                    if (!GroundTraffic.PathClear(_fleet, aircraft,
                            AdelaideGround.TaxiIn(chosen.Value, aircraft.Type, aircraft.AssignedRunway),
                            aircraft.AssignedRunway, taxiOut: false, now,
                            includeStationary: now.ElapsedSeconds - aircraft.StateStartedAt.ElapsedSeconds
                                               < GroundTraffic.MaxWaitSeconds))
                        return false;
                    aircraft.Stand = chosen.Value;
                    Transition(aircraft, FleetState.TaxiIn, now, TaxiInSecondsTo(chosen.Value, aircraft.Type, aircraft.AssignedRunway));
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

            // Any stand: a wide jet next to a parked 737 at gates 43 m apart overlapped wingtips.
            var here = AdelaideGround.StandPose(stand);
            var half = GroundTraffic.HalfSpan(type);
            foreach (var aircraft in _fleet)
            {
                var held = HoldsStand(aircraft) ? aircraft.Stand
                    : aircraft.State == FleetState.TaxiOut ? aircraft.DepartureStand
                    : default;
                if (string.IsNullOrEmpty(held.Value) || held.Equals(stand))
                    continue;
                if (GroundTraffic.TooClose(here, half, AdelaideGround.StandPose(held), GroundTraffic.HalfSpan(aircraft.Type)))
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

            // Player turboprops that are away reserve that many regional bays, so a second
            // ATR coming home is not stranded by AI filling the apron (ADR 0056).
            if (!aircraft.Airline.IsPlayer && !NeedsTerminalGate(aircraft.Type) && PlayerAirline != null)
            {
                var reserved = PlayerTurbopropsNeedingABay();
                var free = 0;
                foreach (var stand in FreeStandsFor(aircraft.Type))
                    free++;
                if (free <= reserved)
                    return null;
            }

            return SuggestStandFor(aircraft.Type, aircraft);
        }

        private int PlayerTurbopropsNeedingABay()
        {
            var count = 0;
            foreach (var aircraft in _fleet)
                if (aircraft.Airline.IsPlayer && !NeedsTerminalGate(aircraft.Type) && !HoldsStand(aircraft))
                    count++;
            return count;
        }

        private StableId? SuggestPurchaseStand(AircraftType type)
        {
            if (NeedsTerminalGate(type))
                return SuggestStandFor(type);
            var reserved = PlayerTurbopropsNeedingABay();
            var free = 0;
            foreach (var _ in FreeStandsFor(type))
                free++;
            return free <= reserved ? null : SuggestStandFor(type);
        }

        public static string NextPlayerRegistration(IReadOnlyList<FleetAircraft> fleet)
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

                if (!taken)
                    return candidate;
            }

            throw new InvalidOperationException("No player registration left.");
        }

        private void AddDeliveryInbound(Airline airline, string registration, AircraftType type)
        {
            if (_fleet.Exists(a => string.Equals(a.Registration, registration, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"{registration} is already registered.");

            var from = DeliveryOrigin(type);
            var aircraft = new FleetAircraft(registration, airline, type, default, _processedTo);
            aircraft.CurrentDestination = from;
            aircraft.Restore(FleetState.Inbound, _processedTo, _processedTo.Advance(8 * 60));
            _fleet.Add(aircraft);
        }

        private Destination DeliveryOrigin(AircraftType type)
        {
            foreach (var destination in DestinationCatalogue.Australia)
            {
                if (destination.Equals(Home))
                    continue;
                if (type.CanReach(DistanceKm(destination)) && RouteAccess.Allows(type, destination))
                    return destination;
            }

            DestinationCatalogue.TryFind("KGC", out var kingscote);
            return kingscote;
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
        /// One movement per physical strip at a time. Arrivals normally go first on
        /// that strip; a departure that has held short past <see cref="DepartureMaxHoldSeconds"/>,
        /// and longer than the first arrival has circled, goes instead. Derived from
        /// state times only, so saves need nothing new beyond the per-strip free times.
        /// </summary>
        private bool RunTower(SimulationTime now)
        {
            var changed = false;
            changed |= RunTowerOnStrip(now, mainStrip: true);
            changed |= RunTowerOnStrip(now, mainStrip: false);
            return changed;
        }

        private bool RunTowerOnStrip(SimulationTime now, bool mainStrip)
        {
            var freeAt = mainStrip ? _mainRunwayFreeAt : _crossRunwayFreeAt;
            if (freeAt.CompareTo(now) > 0)
                return false;
            // ADR 0058: a storm holds every new clearance. An aircraft already landing
            // or taking off keeps going — this only stops the tower starting the next one.
            if (Weather.At(now) == WeatherKind.Storm)
                return false;

            var arrival = LongestWaiting(FleetState.HoldingForLanding, mainStrip);
            var departure = LongestWaiting(FleetState.HoldingShort, mainStrip);
            var next = arrival ?? departure;
            if (arrival != null && departure != null
                && now.ElapsedSeconds - departure.StateStartedAt.ElapsedSeconds >= DepartureMaxHoldSeconds
                && departure.StateStartedAt.CompareTo(arrival.StateStartedAt) < 0)
                next = departure;
            // An arrival whose vacate runs through an aircraft holding short (12's exit passes the
            // 30 hold) would drive through it: send the holder first, then land the arrival.
            if (next == null)
                return false;
            if (next == arrival && departure != null && VacateCrossesHolder(arrival, mainStrip))
                next = departure;
            // Nor may its vacate run into traffic already taxiing. If it would, a waiting
            // departure goes first; otherwise the arrival holds a little longer, re-checked on
            // the ground-control grid so the result does not depend on how the clock steps.
            if (next == arrival && !VacateClearOfTaxiing(arrival, now))
            {
                if (departure != null)
                    next = departure;
                else
                    return false;
            }
            if (next == arrival && !now.Equals(freeAt) && !now.Equals(arrival.StateStartedAt)
                && !GroundTraffic.OnGrid(now))
                return false;
            if (next == null)
                return false;

            // Keep the end they taxied to / held final on. Refreshing 05↔23 (or 12↔30)
            // here teleported lined-up and short-final traffic across the strip when the
            // wind was near a tie. Go-around rejoin still reassigns via AdvanceAircraft.
            var landing = next.State == FleetState.HoldingForLanding;
            var profile = AircraftPerformance.For(next.Type);
            if (landing && ShouldGoAround(next, now, mainStrip))
            {
                // Abort off short final — same remaining final the holder is already flying —
                // so the strip frees quickly and the aircraft does not teleport 4 km out.
                next.WentAroundThisTrip = true;
                var missedFinal = ApproachHold.RemainingFinalSeconds(profile.ApproachSeconds, next.Registration);
                Transition(next, FleetState.Landing, now, missedFinal);
                SetStripFreeAt(mainStrip, now.Advance(missedFinal + WakeSeparationSeconds(next.Type)));
                return true;
            }

            if (landing)
            {
                // Match the short-final visual budget so the strip is not locked for a
                // full long-final that the aircraft is not flying.
                var finalSeconds = ApproachHold.RemainingFinalSeconds(profile.ApproachSeconds, next.Registration);
                var vacateSeconds = AdelaideGround.VacateFor(next.Type, next.AssignedRunway).WholeSeconds;
                var clearSeconds = AdelaideGround.ClearOfRunwaySeconds(next.Type, next.AssignedRunway);
                var runwaySeconds = finalSeconds + profile.LandingSeconds + vacateSeconds;
                Transition(next, FleetState.Landing, now, runwaySeconds);
                SetStripFreeAt(mainStrip,
                    now.Advance(finalSeconds + profile.LandingSeconds + clearSeconds
                                + WakeSeparationSeconds(next.Type)));
                return true;
            }

            var lineupSeconds = AdelaideGround.LineupFor(next.AssignedRunway).WholeSeconds;
            var takeoffSeconds = lineupSeconds + profile.TakeoffSeconds;
            Transition(next, FleetState.TakingOff, now, takeoffSeconds);
            // The strip itself is only occupied through the ground roll to rotation — the
            // arrival side already draws this distinction (ClearOfRunwaySeconds vs. the fuller
            // vacate/landing duration used for the visible state); this used to reuse the whole
            // TakingOff *state* duration (roll + the initial climb already well clear of the
            // tarmac) instead, so the next holder waited through the departing aircraft's climb
            // as well as its wake separation — the unrealistically long gap a real play session
            // reported before the next departure gets moving.
            var rollSeconds = (long)Math.Round(profile.TakeoffRollExactSeconds);
            SetStripFreeAt(mainStrip,
                now.Advance(lineupSeconds + rollSeconds + WakeSeparationSeconds(next.Type)));
            return true;
        }

        /// <summary>
        /// When the tower is expected to clear <paramref name="aircraft"/> off short final,
        /// for an arrival still inbound or holding. Replays the tower's own rule for its strip:
        /// the strip's free time, arrivals ahead in wait order, a departure that has held past
        /// <see cref="DepartureMaxHoldSeconds"/> going first, and no clearances in a storm
        /// (ADR 0058). Presentation flies the arrival in down an extended final to reach the
        /// hold point just as it is cleared, instead of parking it motionless in mid-air there
        /// for the whole wait. Null for anything else. An estimate: traffic that has not yet
        /// reached the queue can still change it.
        /// </summary>
        /// <summary>When a departure at its stand became ready to push: booked time, or prep end.</summary>
        private SimulationTime DepartureReadyAt(FleetAircraft aircraft)
        {
            var ready = aircraft.Scheduled.Value.DepartAt;
            if (aircraft.Airline.IsPlayer && aircraft.PrepStartedAt.HasValue)
            {
                var prepEnd = aircraft.PrepStartedAt.Value.Advance(DeparturePrep.TotalSeconds(aircraft.Type));
                if (prepEnd.CompareTo(ready) > 0)
                    ready = prepEnd;
            }

            return ready;
        }

        public SimulationTime? ExpectedLandingClearance(FleetAircraft aircraft, out RunwayDirection runway)
        {
            runway = RunwayDirection.Runway05;
            if (aircraft == null)
                return null;
            SimulationTime joins;
            if (aircraft.State == FleetState.HoldingForLanding)
            {
                runway = aircraft.AssignedRunway;
                joins = aircraft.StateStartedAt;
            }
            else if (aircraft.State == FleetState.Inbound && aircraft.StateEndsAt.HasValue)
            {
                // Not assigned yet: the end the tower would give it now.
                runway = RunwayFor(aircraft);
                joins = aircraft.StateEndsAt.Value;
            }
            else
            {
                return null;
            }

            var mainStrip = RunwayWeather.IsMainRunway(runway);
            var arrivals = new List<FleetAircraft>();
            var departures = new List<FleetAircraft>();
            foreach (var other in _fleet)
            {
                if (ReferenceEquals(other, aircraft) || RunwayWeather.IsMainRunway(other.AssignedRunway) != mainStrip)
                    continue;
                if (other.State == FleetState.HoldingForLanding && Before(other, other.StateStartedAt, aircraft, joins))
                    arrivals.Add(other);
                else if (other.State == FleetState.HoldingShort)
                    departures.Add(other);
            }

            arrivals.Sort((a, b) => Before(a, a.StateStartedAt, b, b.StateStartedAt) ? -1 : 1);
            departures.Sort((a, b) => a.StateStartedAt.CompareTo(b.StateStartedAt));

            var at = mainStrip ? _mainRunwayFreeAt : _crossRunwayFreeAt;
            if (at.CompareTo(_clock.Now) < 0)
                at = _clock.Now;
            // Until it joins the queue nothing is waiting to land, so holders depart freely.
            while (aircraft.State == FleetState.Inbound && departures.Count > 0 && at.CompareTo(joins) < 0)
            {
                at = AfterStorms(at);
                if (at.CompareTo(joins) >= 0)
                    break;
                at = at.Advance(DepartureRunwaySeconds(departures[0]));
                departures.RemoveAt(0);
            }

            if (at.CompareTo(joins) < 0 && aircraft.State == FleetState.Inbound)
                at = joins;

            for (var guard = 0; guard < 256; guard++)
            {
                at = AfterStorms(at);
                var nextArrivalJoined = arrivals.Count > 0 ? arrivals[0].StateStartedAt : joins;
                if (departures.Count > 0
                    && departures[0].StateStartedAt.CompareTo(nextArrivalJoined) < 0
                    && at.ElapsedSeconds - departures[0].StateStartedAt.ElapsedSeconds >= DepartureMaxHoldSeconds)
                {
                    at = at.Advance(DepartureRunwaySeconds(departures[0]));
                    departures.RemoveAt(0);
                    continue;
                }

                if (arrivals.Count == 0)
                    return at;

                var ahead = arrivals[0];
                arrivals.RemoveAt(0);
                var landing = AircraftPerformance.For(ahead.Type);
                at = at.Advance(ApproachHold.RemainingFinalSeconds(landing.ApproachSeconds, ahead.Registration)
                                + landing.LandingSeconds
                                + AdelaideGround.ClearOfRunwaySeconds(ahead.Type, ahead.AssignedRunway)
                                + WakeSeparationSeconds(ahead.Type));
            }

            return at;
        }

        /// <summary>Its vacate, flown from now, stays clear of every aircraft already moving on the ground.</summary>
        private bool VacateClearOfTaxiing(FleetAircraft arrival, SimulationTime now)
        {
            // Past MaxWaitSeconds on final, land anyway: taxiing traffic always finishes, but the
            // arrival must not circle behind a steady stream of it.
            if (now.ElapsedSeconds - arrival.StateStartedAt.ElapsedSeconds >= GroundTraffic.MaxWaitSeconds)
                return true;
            var profile = AircraftPerformance.For(arrival.Type);
            var touchdownIn = ApproachHold.RemainingFinalSeconds(profile.ApproachSeconds, arrival.Registration)
                              + profile.LandingSeconds;
            return GroundTraffic.PathClear(_fleet, arrival, AdelaideGround.VacateFor(arrival.Type, arrival.AssignedRunway),
                arrival.AssignedRunway, taxiOut: false, now.Advance(touchdownIn), includeStationary: false);
        }

        private bool VacateCrossesHolder(FleetAircraft arrival, bool mainStrip)
        {
            var vacate = AdelaideGround.VacateFor(arrival.Type, arrival.AssignedRunway);
            var half = GroundTraffic.HalfSpan(arrival.Type);
            foreach (var holder in _fleet)
            {
                if (holder.State != FleetState.HoldingShort || RunwayWeather.IsMainRunway(holder.AssignedRunway) != mainStrip)
                    continue;
                var pose = AdelaideGround.HoldingShortPose(holder.DepartureStand,
                    FleetVisual.QueueSlot(_fleet, holder, _processedTo), holder.AssignedRunway, holder.Type);
                for (var s = 0.0; s <= vacate.Seconds; s += 2.0)
                    if (GroundTraffic.TooClose(vacate.PoseAt(s), half, pose, GroundTraffic.HalfSpan(holder.Type)))
                        return true;
            }

            return false;
        }

        /// <summary>How long a departure keeps its strip from the tower: lineup, roll, wake.</summary>
        private static long DepartureRunwaySeconds(FleetAircraft departure) =>
            AdelaideGround.LineupFor(departure.AssignedRunway).WholeSeconds
            + (long)Math.Round(AircraftPerformance.For(departure.Type).TakeoffRollExactSeconds)
            + WakeSeparationSeconds(departure.Type);

        private static bool Before(FleetAircraft a, SimulationTime aJoined, FleetAircraft b, SimulationTime bJoined)
        {
            var order = aJoined.CompareTo(bJoined);
            return order < 0 || order == 0 && string.CompareOrdinal(a.Registration, b.Registration) < 0;
        }

        /// <summary>The first moment at or after <paramref name="at"/> that is not in a storm hold.</summary>
        private static SimulationTime AfterStorms(SimulationTime at)
        {
            for (var i = 0; i < 48 && Weather.At(at) == WeatherKind.Storm; i++)
                at = new SimulationTime((at.ElapsedSeconds / Weather.BlockSeconds + 1) * Weather.BlockSeconds);
            return at;
        }

        private void SetStripFreeAt(bool mainStrip, SimulationTime at)
        {
            if (mainStrip)
                _mainRunwayFreeAt = at;
            else
                _crossRunwayFreeAt = at;
        }

        private bool ShouldGoAround(FleetAircraft aircraft, SimulationTime now, bool mainStrip)
        {
            if (aircraft.WentAroundThisTrip)
                return false;
            var arrivals = 0;
            foreach (var candidate in _fleet)
            {
                if (candidate.State != FleetState.HoldingForLanding)
                    continue;
                if (RunwayWeather.IsMainRunway(candidate.AssignedRunway) != mainStrip)
                    continue;
                arrivals++;
            }

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

        private FleetAircraft LongestWaiting(FleetState state, bool mainStrip)
        {
            FleetAircraft best = null;
            foreach (var aircraft in _fleet)
            {
                if (aircraft.State != state)
                    continue;
                if (RunwayWeather.IsMainRunway(aircraft.AssignedRunway) != mainStrip)
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

        /// <summary>Outbound ground states must remember which stand they left.</summary>
        internal static bool RequiresDepartureStand(FleetState state) => state is
            FleetState.TaxiOut or FleetState.HoldingShort or FleetState.TakingOff;

        /// <summary>
        /// The tower stores a missed approach as <see cref="FleetState.Landing"/> lasting only
        /// the approach. A subsequent real landing is longer, so <see cref="FleetAircraft.WentAroundThisTrip"/>
        /// can stay set (no second go-around) without trapping the aircraft in the circuit.
        /// </summary>
        public static bool IsMissedApproachLanding(FleetAircraft aircraft)
        {
            if (aircraft == null || !aircraft.StateEndsAt.HasValue)
                return false;
            var duration = aircraft.StateEndsAt.Value.ElapsedSeconds - aircraft.StateStartedAt.ElapsedSeconds;
            return duration <= AircraftPerformance.For(aircraft.Type).ApproachSeconds;
        }

        /// <summary>
        /// First Adelaide push hour. The airfield is 24 h; the first domestics
        /// go around 05:00, not 06:00.
        /// </summary>
        public const int AiFirstDepartureHour = 5;

        /// <summary>
        /// Last Adelaide push hour. No SYD-style curfew — late internationals
        /// still leave after 21:00. Regionals skip the late hole via the hour profile.
        /// </summary>
        public const int AiLastDepartureHour = 23;

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
        public static readonly IReadOnlyList<(string Code, int Weight)> QantasNetwork = new[]
        {
            ("SYD", 3), ("MEL", 3), ("BNE", 2), ("AKL", 1), ("PER", 1), ("CBR", 1)
        };
        public static readonly IReadOnlyList<(string Code, int Weight)> JetstarNetwork = new[]
        {
            ("MEL", 3), ("SYD", 2), ("BNE", 2), ("DPS", 2), ("OOL", 1), ("PER", 1)
        };
        public static readonly IReadOnlyList<(string Code, int Weight)> MalaysiaNetwork = new[] { ("KUL", 1) };
        public static readonly IReadOnlyList<(string Code, int Weight)> EmiratesNetwork = new[] { ("DXB", 1) };
        public static readonly IReadOnlyList<(string Code, int Weight)> QatarNetwork = new[] { ("DOH", 1) };
        public static readonly IReadOnlyList<(string Code, int Weight)> FijiNetwork = new[] { ("NAN", 1) };

        public static IReadOnlyList<(string Code, int Weight)> AiNetworkFor(Airline airline) => airline.Id.Value switch
        {
            "REX" => RexNetwork,
            "QLK" => QantasLinkNetwork,
            "VOZ" => VirginNetwork,
            "QFA" => QantasNetwork,
            "JST" => JetstarNetwork,
            "ANZ" => AirNewZealandNetwork,
            "SIA" => SingaporeNetwork,
            "CPA" => CathayNetwork,
            "MAS" => MalaysiaNetwork,
            "UAE" => EmiratesNetwork,
            "QTR" => QatarNetwork,
            "FJI" => FijiNetwork,
            _ => AiNetwork
        };

        /// <summary>Single-city international home for operators that only fly one Adelaide route.</summary>
        public static string LongHaulHomeOf(string airlineId) => airlineId switch
        {
            "SIA" => "SIN",
            "CPA" => "HKG",
            "MAS" => "KUL",
            "UAE" => "DXB",
            "QTR" => "DOH",
            "FJI" => "NAN",
            _ => null
        };

        private void ScheduleAiDeparture(FleetAircraft aircraft, SimulationTime now)
        {
            if (aircraft.Airline.Id.Value == "VOZ")
            {
                // Rotation, not a random draw, so the mainline timetable is stable.
                var code = VirginRotation[aircraft.CompletedTrips % VirginRotation.Count];
                if (DestinationCatalogue.TryFind(code, out var next) && CanReach(aircraft, next))
                    BookAiDeparture(aircraft, next, now);
                return;
            }

            if (aircraft.Airline.Id.Value == "QFA")
            {
                var code = QantasRotation[aircraft.CompletedTrips % QantasRotation.Count];
                if (DestinationCatalogue.TryFind(code, out var next) && CanReach(aircraft, next))
                    BookAiDeparture(aircraft, next, now);
                return;
            }

            if (aircraft.Airline.Id.Value == "JST")
            {
                var code = JetstarRotation[aircraft.CompletedTrips % JetstarRotation.Count];
                if (DestinationCatalogue.TryFind(code, out var next) && CanReach(aircraft, next))
                    BookAiDeparture(aircraft, next, now);
                return;
            }

            if (aircraft.Airline.Id.Value == "ANZ")
            {
                // Rotation, not a random draw, so international traffic is stable too.
                var code = AirNewZealandRotation[aircraft.CompletedTrips % AirNewZealandRotation.Count];
                if (DestinationCatalogue.TryFind(code, out var next) && CanReach(aircraft, next))
                    BookAiDeparture(aircraft, next, now);
                return;
            }
            var longHaulHome = LongHaulHomeOf(aircraft.Airline.Id.Value);
            if (longHaulHome != null)
            {
                if (aircraft.Airline.Id.Value == "CPA" && !IsCathaySeason(now))
                    return;
                if (DestinationCatalogue.TryFind(longHaulHome, out var next) && CanReach(aircraft, next))
                    BookAiDeparture(aircraft, next, now);
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

            BookAiDeparture(aircraft, pick, now);
        }

        private void BookAiDeparture(FleetAircraft aircraft, Destination destination, SimulationTime now)
        {
            var departAt = AiDepartureWithinHours(now.Advance(AiTurnaroundSeconds(aircraft)), aircraft.Type);
            var disruption = FlightDisruption.For(
                $"{aircraft.Registration}:{aircraft.CompletedTrips}:{destination.Code}",
                departAt, Clock);
            if (disruption.Cancelled)
            {
                aircraft.Scheduled = new ScheduledDeparture(destination, departAt, 0, cancelled: true);
                return;
            }

            if (disruption.Delayed)
                departAt = AiDepartureWithinHours(departAt.Advance(disruption.DelayMinutes * 60L), aircraft.Type);
            aircraft.Scheduled = new ScheduledDeparture(destination, departAt, disruption.DelayMinutes);
        }

        /// <summary>
        /// Repeatable turnarounds sized like a real Adelaide gate dwell, not a
        /// game-speed hop. Domestics sit 28–44 minutes (turboprop) or 40–58
        /// (narrowbody); widebodies 55–89 like a T1 international turn. Short
        /// enough that the field stays busy across a soak day; long enough that
        /// "due to depart" means a real turnaround, not a ten-minute flip.
        /// </summary>
        private static long AiTurnaroundSeconds(FleetAircraft aircraft)
        {
            unchecked
            {
                var hash = 17;
                foreach (var ch in aircraft.Registration)
                    hash = hash * 31 + ch;
                hash = hash * 31 + aircraft.CompletedTrips;
                var wide = AircraftCatalogue.IsWidebody(aircraft.Type);
                var jet = NeedsTerminalGate(aircraft.Type);
                var minutes = wide ? 55 + Math.Abs(hash % 35)
                    : jet ? 40 + Math.Abs(hash % 19)
                    : 28 + Math.Abs(hash % 17);
                return minutes * 60L;
            }
        }

        /// <summary>
        /// Regional ready-times jump the afternoon hole onto the next bank.
        /// Jets keep the 05:00–23:00 window — a 787 ready at 21:40 still goes.
        /// </summary>
        internal SimulationTime AiDepartureWithinHours(SimulationTime readyAt, AircraftType type = null)
        {
            var local = Clock.LocalAt(readyAt);
            DateTime useful;
            if (type != null && NeedsTerminalGate(type))
            {
                var first = local.Date.AddHours(AiFirstDepartureHour);
                var last = local.Date.AddHours(AiLastDepartureHour);
                if (local >= first && local <= last)
                    return readyAt;
                useful = local < first ? first : first.AddDays(1);
            }
            else
            {
                useful = AdelaideHourProfile.NextUsefulLocal(local, AiFirstDepartureHour, AiLastDepartureHour);
                if (useful == local)
                    return readyAt;
            }

            var at = Clock.AtLocal(useful);
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
