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
            Registration = aircraft?.Registration ?? string.Empty;
            AirlineName = aircraft?.Airline?.Name ?? string.Empty;
            LiveryHex = aircraft?.Airline?.LiveryHex ?? string.Empty;
            TypeName = aircraft?.Type?.Name ?? string.Empty;
            IsPlayer = aircraft?.Airline?.IsPlayer ?? false;
            var dest = aircraft?.CurrentDestination ?? aircraft?.Scheduled?.Destination;
            DestinationCode = dest?.Code ?? string.Empty;
            DestinationName = dest?.Name ?? string.Empty;
            Stand = aircraft?.Stand ?? default;
            if (string.IsNullOrEmpty(Stand.Value) && aircraft != null)
                Stand = aircraft.DepartureStand;
        }

        public SimulationTime At { get; }
        public FleetAircraft Aircraft { get; }
        public FleetState State { get; }

        /// <summary>Frozen at the moment of the event — safe for history after the aircraft moves on.</summary>
        public string Registration { get; }
        public string AirlineName { get; }
        public string LiveryHex { get; }
        public string TypeName { get; }
        public bool IsPlayer { get; }
        public string DestinationCode { get; }
        public string DestinationName { get; }
        public StableId Stand { get; }
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
        public const int MaxRecentEvents = 80;

        /// <summary>
        /// How long a player aircraft waits at the exit for a manual stand choice before the
        /// tower parks it on the suggested stand (ADR 0056). Fixed from
        /// <see cref="FleetAircraft.StateStartedAt"/> so skip-to-next-event stays deterministic.
        /// </summary>
        public const long PlayerStandAutoSeconds = 90;

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
                ("VH-8IC", AircraftType.Boeing7378, new StableId("GATE-19")),
                // ADR 0111: extra frames with no home gate — any free code C gate, else they
                // night-stop at the other end and fly in with the morning arrivals.
                ("VH-8ID", AircraftType.Boeing7378, default),
                ("VH-8IE", AircraftType.Boeing7378, default),
                ("VH-8IF", AircraftType.Boeing7378, default),
                ("VH-8IG", AircraftType.Boeing7378, default)
            }),
            (Airline.Qantas, new[]
            {
                ("VH-VZX", AircraftType.Boeing737800, new StableId("GATE-21")),
                ("VH-VZY", AircraftType.Boeing737800, new StableId("GATE-24")),
                ("VH-VZZ", AircraftType.Boeing737800, new StableId("GATE-23")),
                ("VH-VZU", AircraftType.Boeing737800, default),
                ("VH-VZV", AircraftType.Boeing737800, default),
                ("VH-VZW", AircraftType.Boeing737800, default),
                ("VH-VZR", AircraftType.Boeing737800, default),
                ("VH-VZS", AircraftType.Boeing737800, default),
                ("VH-VZT", AircraftType.Boeing737800, default)
            }),
            (Airline.Jetstar, new[]
            {
                ("VH-VFH", AircraftType.AirbusA320200, new StableId("GATE-17")),
                ("VH-VFI", AircraftType.AirbusA321Neo, new StableId("GATE-16L")),
                ("VH-VFJ", AircraftType.AirbusA320200, default),
                ("VH-VFK", AircraftType.AirbusA321Neo, default),
                ("VH-VFL", AircraftType.AirbusA320200, default)
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

        /// <summary>
        /// T1 contact positions that take a code E widebody (A330, 787, A350): the MARS
        /// centre lines of the 18 / 20 / 22 / 28 piers and the 25 / 26 international gates,
        /// which is where Cathay, Singapore, Malaysia, Emirates, Qatar and the player's
        /// International widebody lease already park. Every other gate is code C
        /// (737 / A320 family and smaller). ADR 0110.
        /// </summary>
        public static readonly IReadOnlyList<StableId> CodeEGates = new[]
        {
            new StableId("GATE-18"), new StableId("GATE-20"), new StableId("GATE-22L"),
            new StableId("GATE-25"), new StableId("GATE-26L"), new StableId("GATE-28L")
        };

        /// <summary>Largest ICAO code letter this stand can take (regional bays and most gates: C).</summary>
        public static char StandCodeLetter(StableId stand)
        {
            foreach (var gate in CodeEGates)
                if (gate.Equals(stand))
                    return 'E';
            return 'C';
        }

        /// <summary>
        /// Gate or bay suits the aircraft: terminal vs regional class, the ICAO code letter
        /// (no A350 on a narrowbody gate), and the SF340-only walk-outs.
        /// </summary>
        public static bool StandFits(AircraftType type, StableId stand)
        {
            if (!StandClassFits(type, stand))
                return false;
            return AircraftCatalogue.CodeLetter(type) <= StandCodeLetter(stand);
        }

        /// <summary>
        /// The pre-ADR 0110 fit (stand class and walk-outs, no code letter). Only used when
        /// restoring a save, so an older game with a widebody parked on a code C gate still
        /// loads; the aircraft keeps that gate until it departs.
        /// </summary>
        public static bool StandClassFits(AircraftType type, StableId stand)
        {
            if (AdelaideGround.IsTerminalGate(stand) != NeedsTerminalGate(type))
                return false;
            // AIP walk-outs are SF340 / marshaller only — not ATR or Dash 8.
            return !IsWalkOutStand(stand) || ReferenceEquals(type, AircraftType.Saab340);
        }

        /// <summary>
        /// True when parking here would take a position another type needs more: a code C jet
        /// on a code E gate, or a Saab on a 50-series bay while a walk-out could take it.
        /// </summary>
        public static bool WastesStand(AircraftType type, StableId stand) =>
            IsOversized(type, stand)
            || ReferenceEquals(type, AircraftType.Saab340) && !AdelaideGround.IsTerminalGate(stand)
               && !IsWalkOutStand(stand);

        /// <summary>True when a smaller aircraft would take a gate a bigger one needs.</summary>
        public static bool IsOversized(AircraftType type, StableId stand) =>
            StandCodeLetter(stand) > 'C' && AircraftCatalogue.CodeLetter(type) < StandCodeLetter(stand);

        /// <summary>
        /// Real regional carriers that share Adelaide's regional apron with the player and
        /// the player's airline. New games start with them; older saves gain them on load
        /// (<see cref="AddMissingRegionalCarriers"/>). Six Rex Saabs and four QantasLink
        /// Q400s share the 50-series and walk-outs with the player; whoever has no free bay
        /// night-stops at the other end and flies in with the morning arrivals (ADR 0111).
        /// </summary>
        public static readonly IReadOnlyList<(Func<Airline> Make, (string Registration, AircraftType Type)[] Fleet)> RegionalCarriers = new (Func<Airline>, (string, AircraftType)[])[]
        {
            (Airline.Rex, new[]
            {
                ("VH-ZRC", AircraftType.Saab340), ("VH-ZRD", AircraftType.Saab340),
                ("VH-ZRE", AircraftType.Saab340), ("VH-ZRF", AircraftType.Saab340),
                ("VH-ZRG", AircraftType.Saab340), ("VH-ZRH", AircraftType.Saab340)
            }),
            (Airline.QantasLink, new[]
            {
                ("VH-QOK", AircraftType.Dash8Q400), ("VH-QOL", AircraftType.Dash8Q400), ("VH-QOM", AircraftType.Dash8Q400),
                ("VH-QON", AircraftType.Dash8Q400)
            })
        };

        /// <summary>
        /// RFDS emergency turboprop. Uses a Saab 340 stand-in for the real PC-12 / King Air.
        /// Exempt from the 23:00–05:00 curfew. Parked on a leftover regional bay when one is free.
        /// </summary>
        public static readonly IReadOnlyList<(Func<Airline> Make, (string Registration, AircraftType Type)[] Fleet)> EmergencyOperators = new (Func<Airline>, (string, AircraftType)[])[]
        {
            (Airline.Rfds, new[] { ("VH-FDA", AircraftType.Saab340) })
        };

        private readonly ISimulationClock _clock;
        private readonly IRandomSource _random;
        private readonly List<Airline> _airlines = new();
        private readonly List<FleetAircraft> _fleet = new();
        private readonly List<StableId> _stands;
        private readonly List<FleetEvent> _recentEvents = new();
        private readonly List<FlightSettlement> _recentSettlements = new();
        private readonly List<OutstationAircraft> _outstationFleet = new();
        private readonly List<RepeatSchedule> _repeatSchedules = new();
        private bool _repeatSchedulesLive = true;
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
        /// Opening pushbacks shaped like an Adelaide morning bank: clustered minutes with
        /// intentional doubles (ADR 0100). Tower/ground still serialise the strip — same
        /// DepartAt is fine; the apron should look busy, not single-file.
        /// </summary>
        public static readonly long[] AiOpeningDepartureSeconds =
        {
            2 * 60, 2 * 60,
            5 * 60, 5 * 60,
            8 * 60, 8 * 60,
            12 * 60,
            15 * 60, 15 * 60,
            20 * 60,
            25 * 60, 25 * 60,
            32 * 60,
            38 * 60
        };

        /// <summary>
        /// The ADR 0045 / 0077 / 0100 starting position at Adelaide: the player's airline with one
        /// Saab 340B, real Adelaide operators filling the apron, a short inbound bank already
        /// flying, and opening departures clustered like an ADL morning peak.
        /// </summary>
        public static AirlineOperations StartAtAdelaide(ISimulationClock clock, IRandomSource random, Airline player,
            AirlineClock airlineClock = null)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (!player.IsPlayer) throw new ArgumentException("The starting airline must be the player's.", nameof(player));

            var operations = new AirlineOperations(clock, random, DestinationCatalogue.Adelaide, AdelaideStands);
            // Book against the real Adelaide clock first. Assigning it after scheduling
            // used to stamp an 08:00 morning peak onto whatever live hour you launched.
            operations.Clock = airlineClock ?? AirlineClock.Default;
            operations.AddAirline(player);
            operations.AddAircraft(player, "VH-PAX", AircraftType.Saab340, AdelaideRegionalBays[0]);
            var aiFleet = new List<FleetAircraft>();
            // RFDS first so the emergency aircraft always has a bay; the larger regional
            // fleets overflow to night-stops away rather than squeezing it out (ADR 0111).
            operations.AddMissingEmergencyOperators(aiFleet);
            operations.AddMissingRegionalCarriers(aiFleet);
            var terminalFleet = new List<FleetAircraft>();
            operations.AddMissingTerminalOperators(terminalFleet);
            operations.SeedOpeningTraffic(aiFleet, terminalFleet);
            return operations;
        }

        /// <summary>
        /// Short opening arrival bank so most authored metal stays on stands (ADR 0100).
        /// Morning: ~7 inbound over ~40 minutes. Evening: stretch those onto the remaining
        /// time before 22:50. Curfew: no commercial opening peak.
        /// </summary>
        private void SeedOpeningTraffic(List<FleetAircraft> regionalFleet, List<FleetAircraft> terminalFleet)
        {
            var local = Clock.LocalAt(_processedTo);
            if (AirportCurfew.IsClosed(local))
                return;

            var evening = local.Hour >= 19;
            // Keep a readable short-final stream; leave the rest of T1 / the bays parked.
            TrySeedOpeningInbound(regionalFleet, "QLK", "PLO", FitOpeningSeconds(local, 3 * 60, evening));
            TrySeedOpeningInbound(regionalFleet, "REX", "MGB", FitOpeningSeconds(local, 8 * 60, evening));
            TrySeedOpeningInbound(terminalFleet, "VOZ", "MEL", FitOpeningSeconds(local, 12 * 60, evening));
            TrySeedOpeningInbound(terminalFleet, "QFA", "SYD", FitOpeningSeconds(local, 18 * 60, evening));
            TrySeedOpeningInbound(terminalFleet, "JST", "MEL", FitOpeningSeconds(local, 24 * 60, evening));
            TrySeedOpeningInbound(terminalFleet, "ANZ", "AKL", FitOpeningSeconds(local, 30 * 60, evening));
            TrySeedOpeningInbound(terminalFleet, "SIA", "SIN", FitOpeningSeconds(local, 40 * 60, evening));

            if (evening)
                return;

            var departureIndex = 0;
            foreach (var aircraft in _fleet)
            {
                if (departureIndex >= AiOpeningDepartureSeconds.Length)
                    break;
                if (aircraft.Airline.IsPlayer || aircraft.Airline.IsEmergency
                    || aircraft.State != FleetState.AtStand || aircraft.Scheduled is not { } first)
                    continue;
                aircraft.Scheduled = new ScheduledDeparture(first.Destination,
                    OpeningDepartureAt(AiOpeningDepartureSeconds[departureIndex++]));
            }
        }

        /// <summary>
        /// An opening pushback published on the next 5-minute clock mark at or after the
        /// ladder offset, so the board reads 07:05 / 07:05 / 07:10 like a real departures
        /// screen rather than launch-relative odd minutes (ADR 0110).
        /// </summary>
        private SimulationTime OpeningDepartureAt(long offsetSeconds)
        {
            var at = _processedTo.Advance(offsetSeconds);
            var local = Clock.LocalAt(at);
            var minutes = local.Hour * 60 + local.Minute + (local.Second > 0 || local.Millisecond > 0 ? 1 : 0);
            var mark = (minutes + 4) / 5 * 5;
            var published = Clock.AtLocal(local.Date.AddMinutes(mark));
            return published.CompareTo(at) >= 0 ? published : at;
        }

        /// <summary>
        /// Last commercial arrival of the opening bank: 22:50, ten minutes before curfew.
        /// </summary>
        public const int LastOpeningArrivalMinute = AirportCurfew.ClosedFromHour * 60 - 10;

        private long FitOpeningSeconds(DateTime local, long nominalSeconds, bool evening)
        {
            if (!evening)
                return nominalSeconds;
            var last = Clock.AtLocal(local.Date.AddMinutes(LastOpeningArrivalMinute));
            var remaining = last.ElapsedSeconds - _processedTo.ElapsedSeconds;
            if (remaining < 8 * 60)
                return -1;
            const long nominalLast = 40 * 60;
            const long nominalFirst = 3 * 60;
            var span = Math.Max(1, remaining - nominalFirst);
            var t = (nominalSeconds - nominalFirst) / (double)(nominalLast - nominalFirst);
            return nominalFirst + (long)Math.Round(t * span);
        }

        private void TrySeedOpeningInbound(List<FleetAircraft> fleet, string airlineId, string destinationCode,
            long secondsToCircuit)
        {
            if (secondsToCircuit < 0 || fleet == null
                || !DestinationCatalogue.TryFind(destinationCode, out var destination))
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
                    if (!stand.HasValue && !HasStandFor(type))
                        break;
                    if (airline == null)
                    {
                        airline = template;
                        AddAirline(airline);
                    }

                    var aircraft = stand.HasValue
                        ? AddAircraft(airline, registration, type, stand.Value)
                        : AddAircraftAway(airline, registration, type);
                    added?.Add(aircraft);
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Add the RFDS aircraft if this field still has a free regional bay. Safe on every load.
        /// </summary>
        public int AddMissingEmergencyOperators(List<FleetAircraft> added = null)
        {
            var count = 0;
            foreach (var (make, fleet) in EmergencyOperators)
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
            // Pass 0 parks the aircraft that own a gate; pass 1 the extra frames, which take
            // only a spare gate their size (never a widebody's code E gate) or start away.
            for (var pass = 0; pass < 2; pass++)
            foreach (var (make, fleet) in TerminalOperators)
            {
                var template = make();
                if (template.Id.Value == "CPA" && !IsCathaySeason(asOf))
                    continue;
                var airline = _airlines.Find(a => a.Id.Equals(template.Id));
                foreach (var (registration, type, gate) in fleet)
                {
                    if (string.IsNullOrEmpty(gate.Value) != (pass == 1))
                        continue;
                    if (_fleet.Exists(a => string.Equals(a.Registration, registration, StringComparison.OrdinalIgnoreCase)))
                        continue;
                    StableId? stand = gate;
                    if (string.IsNullOrEmpty(gate.Value) || !_stands.Contains(gate) || !IsStandFree(gate)
                        || !StandFits(type, gate))
                    {
                        // Seasonal Cathay used to vanish for the whole summer when GATE-18
                        // (or its pier sibling) was taken. Fall back to any free fitting gate;
                        // with none, the aircraft is away and flies in (ADR 0111).
                        stand = SuggestStandFor(type);
                        if (pass == 1 && stand.HasValue && IsOversized(type, stand.Value))
                            stand = null;
                    }

                    // An airport with no stand this type could ever use does not get it at all.
                    if (!stand.HasValue && !HasStandFor(type))
                        continue;

                    if (airline == null)
                    {
                        airline = template;
                        AddAirline(airline);
                    }

                    var aircraft = stand.HasValue
                        ? AddAircraft(airline, registration, type, stand.Value)
                        : AddAircraftAway(airline, registration, type);
                    added?.Add(aircraft);
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Cathay's summer service leaves when the season ends. A parked A350 keeps its
        /// gate until its booked departure, flies home, and is removed while away (off the
        /// map) so GATE-18 is not held through winter and no aircraft vanishes from a stand.
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
                // The apron keeps its aircraft until they depart (ADR 0110): a parked A350
                // flies its last rotation home and is retired off-map, never lifted off GATE-18.
                // Only a parked jet with nothing booked (nothing left to fly) is removed here.
                var offMap = aircraft.State is FleetState.AtDestination or FleetState.Inbound;
                var idleOnStand = aircraft.State == FleetState.AtStand && !aircraft.Scheduled.HasValue;
                if (!offMap && !idleOnStand)
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
        public IReadOnlyList<OutstationAircraft> OutstationFleet => _outstationFleet;
        public IReadOnlyList<RepeatSchedule> RepeatSchedules => _repeatSchedules;
        public bool DelegationUnlocked => CareerState != null && CareerState.ManualRotations >= 12;

        /// <summary>Call after away catch-up; saved repeat plans never generate offline flights.</summary>
        public void ResumeRepeatSchedules() => _repeatSchedulesLive = true;
        public void RecordActivePlaySecond() => CareerState?.AddActivePlaySecond();


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
            if (!airline.IsPlayer && !airline.IsEmergency)
                ScheduleAiDeparture(aircraft, _processedTo);
            return aircraft;
        }

        /// <summary>
        /// Add an AI aircraft that has no free stand here: it is at its outstation and flies
        /// in on a real arrival — soon if the field is open, otherwise with tomorrow's
        /// 06:00–08:30 morning arrivals — so the apron never holds more than it has room
        /// for (ADR 0111). Draws no random numbers.
        /// </summary>
        private FleetAircraft AddAircraftAway(Airline airline, string registration, AircraftType type)
        {
            if (!_airlines.Contains(airline))
                throw new InvalidOperationException("Add the airline before its aircraft.");
            var aircraft = new FleetAircraft(registration, airline, type, default, _processedTo);
            aircraft.CurrentDestination = AwayBaseFor(airline, type);
            var key = FirstWaveKey(aircraft);
            var local = Clock.LocalAt(_processedTo);
            // After the ADR 0100 opening bank (first 45 min), so the start is not a pile-up.
            var soon = _processedTo.Advance((50 + key % 120) * 60L);
            var landsAt = AirportCurfew.IsClosed(Clock.LocalAt(soon)) || AirportCurfew.IsClosed(local)
                ? MorningArrivalAt(aircraft, _processedTo)
                : soon;
            aircraft.Restore(FleetState.Inbound, _processedTo, landsAt);
            _fleet.Add(aircraft);
            return aircraft;
        }

        /// <summary>True when this airport has at least one stand the type could ever use.</summary>
        private bool HasStandFor(AircraftType type)
        {
            foreach (var stand in _stands)
                if (StandFits(type, stand))
                    return true;
            return false;
        }

        /// <summary>The airline's busiest reachable city — where a spare frame night-stops.</summary>
        private Destination AwayBaseFor(Airline airline, AircraftType type)
        {
            var longHaul = LongHaulHomeOf(airline.Id.Value);
            if (longHaul != null && DestinationCatalogue.TryFind(longHaul, out var home))
                return home;
            Destination? best = null;
            var bestWeight = -1;
            foreach (var (code, weight) in AiNetworkFor(airline))
            {
                if (!DestinationCatalogue.TryFind(code, out var destination) || destination.Equals(Home))
                    continue;
                if (!type.CanReach(DistanceKm(destination)) || weight <= bestWeight)
                    continue;
                best = destination;
                bestWeight = weight;
            }

            return best ?? DeliveryOrigin(type);
        }

        /// <summary>
        /// Next morning arrival for an aircraft kept out overnight: 06:00–08:30, spread by
        /// registration, never earlier than the 05:00 opening (ADR 0111). Replaces landing
        /// every overnight inbound at 05:00 exactly.
        /// </summary>
        internal SimulationTime MorningArrivalAt(FleetAircraft aircraft, SimulationTime now)
        {
            var local = Clock.LocalAt(now);
            var day = local.Hour < AirportCurfew.OpensAtHour ? local.Date : local.Date.AddDays(1);
            var at = Clock.AtLocal(day.AddHours(6).AddMinutes(FirstWaveKey(aircraft) % 150));
            return at.CompareTo(now) > 0 ? at : now;
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
            // AtStand/TaxiIn always require a stand. Newer saves may also contain an AI
            // arrival reservation while holding or landing; older saves legitimately do not.
            var requiresStand = state is FleetState.AtStand or FleetState.TaxiIn;
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
            IEnumerable<CompletedContractRecord> contractHistory = null,
            PlayerBaseLevel? baseLevel = null, string pinnedGoalId = null,
            IEnumerable<string> servedDestinations = null, IEnumerable<string> outstationBases = null,
            IEnumerable<long> recentServiceMargins = null, int manualRotations = 0,
            long activePlaySeconds = 0, long regionalAtSeconds = 0, long domesticAtSeconds = 0,
            long internationalAtSeconds = 0, long finaleAtSeconds = 0)
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
            CareerState = new AirlineCareerState(funds, reliability, parsedTier, contract, processedSettlementKeys,
                completedContractIds, completedPlayerRotations, issued, lifetimeRevenue, contractHistory, effectiveBase,
                pinnedGoalId, servedDestinations, outstationBases, recentServiceMargins, manualRotations,
                activePlaySeconds, regionalAtSeconds, domesticAtSeconds, internationalAtSeconds, finaleAtSeconds);
        }

        internal void RestoreAutomatedTrip(string registration, bool automated)
        {
            var aircraft = _fleet.Find(a => a.Registration == registration && a.Airline.IsPlayer);
            if (aircraft != null) aircraft.AutomatedTrip = automated;
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
        public int PlayerFleetCount()
        {
            var count = 0;
            foreach (var aircraft in _fleet)
                if (aircraft.Airline.IsPlayer)
                    count++;
            return count + _outstationFleet.Count;
        }

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

            foreach (var aircraft in _outstationFleet)
            {
                var seen = false;
                foreach (var existing in types)
                    if (existing.Id == aircraft.Type.Id) seen = true;
                if (!seen) types.Add(aircraft.Type);
            }
            return types;
        }

        private List<AircraftType> AdelaideOwnedTypes()
        {
            var types = new List<AircraftType>();
            foreach (var aircraft in _fleet)
            {
                if (!aircraft.Airline.IsPlayer) continue;
                if (!types.Exists(type => type.Id == aircraft.Type.Id)) types.Add(aircraft.Type);
            }
            return types;
        }

        /// <summary>How many authored career contracts head the offers at once.</summary>
        public const int FeaturedCareerContracts = 2;

        /// <summary>
        /// The contracts on offer: the next authored career contracts the airline's tier allows
        /// and it has not yet fulfilled (ADR 0084), then the rotating market. The authored ones used
        /// to appear nowhere but the objective card's fallback.
        /// </summary>
        public IReadOnlyList<RouteContractDefinition> MarketOffers()
        {
            var offers = new List<RouteContractDefinition>();
            if (CareerState != null)
            {
                foreach (var definition in RouteContractCatalogue.All)
                {
                    if (offers.Count >= FeaturedCareerContracts)
                        break;
                    if (CareerState.HasCompleted(definition.Id) || CareerState.Tier < definition.RequiredTier)
                        continue;
                    if (CareerState.ActiveContract != null && CareerState.ActiveContract.DefinitionId == definition.Id)
                        continue;
                    offers.Add(definition);
                }
            }

            var localTypes = AdelaideOwnedTypes();
            offers.AddRange(ContractMarket.At(_processedTo, localTypes, CareerState?.Reliability ?? 0,
                CareerState?.Tier ?? OperatingTier.Provisional));
            var usable = false;
            foreach (var offer in offers)
                if (!CareerState.HasCompleted(offer.Id) && localTypes.Exists(t => t.Id == offer.EligibleType.Id))
                    usable = true;
            // A usable-looking offer is still a dead end when the airline cannot afford
            // even one dispatch. Keep a funded recovery path visible in that case too.
            var hasRecoveryRoute = DestinationCatalogue.TryFind("KGC", out var recovery);
            var strandedForCash = hasRecoveryRoute && localTypes.Count > 0 && CareerState.Funds <
                FlightEconomics.DispatchCost(localTypes[0], DistanceKm(recovery));
            if ((!usable || strandedForCash) && localTypes.Count > 0 && hasRecoveryRoute)
            {
                var type = localTypes[0];
                if (RouteAccess.Allows(type, recovery) && type.CanReach(DistanceKm(recovery)))
                {
                    var id = $"REC-{CareerState.CompletedPlayerRotations}-{type.Id}";
                    var basePay = FlightEconomics.FlightPay(type, DistanceKm(recovery));
                    offers.Insert(0, new RouteContractDefinition(id, Home.Code, recovery.Code, type, 2,
                        Math.Max(200, basePay / 2), Math.Max(200, basePay), 1,
                        OperatingTier.Provisional, reliabilityLossOnCancel: 2));
                }
            }
            return offers;
        }

        /// <summary>Every catalogue destination except home, reachable or not, for the map.</summary>
        public IEnumerable<Destination> MapDestinations()
        {
            foreach (var destination in DestinationCatalogue.Australia)
                if (!destination.Equals(Home))
                    yield return destination;
        }

        public long AirborneSeconds(FleetAircraft aircraft, Destination destination) =>
            LegTiming.AirborneSeconds(DistanceKm(destination), aircraft.Type);

        public bool IsStandFree(StableId stand) => IsStandFree(stand, null);

        private bool IsStandFree(StableId stand, FleetAircraft except)
        {
            if (!_stands.Contains(stand))
                return false;
            foreach (var aircraft in _fleet)
            {
                if (ReferenceEquals(aircraft, except))
                    continue;
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
        /// Stands the player (or any owner) may assign right now: free, type-fit, lead-in clear,
        /// and inside the player's base allocation when that applies. Best/suggested stand is
        /// listed first when it is among them.
        /// </summary>
        public IReadOnlyList<StableId> AssignableStands(FleetAircraft aircraft)
        {
            var list = new List<StableId>();
            if (aircraft == null || aircraft.State != FleetState.AwaitingStand)
                return list;

            foreach (var stand in FreeStandsFor(aircraft.Type))
            {
                if (aircraft.Airline.IsPlayer && CareerState != null
                    && !PlayerBase.CanUseStand(CareerState.BaseLevel, aircraft.Type, stand))
                    continue;
                if (AdelaideGround.IsTerminalGate(stand) && !IsLeadInFree(stand, aircraft))
                    continue;
                list.Add(stand);
            }

            list.Sort((a, b) =>
            {
                var byTaxi = TaxiInSecondsTo(a, aircraft.Type, aircraft.AssignedRunway)
                    .CompareTo(TaxiInSecondsTo(b, aircraft.Type, aircraft.AssignedRunway));
                return byTaxi != 0 ? byTaxi : string.CompareOrdinal(a.Value, b.Value);
            });

            var suggested = SuggestStand(aircraft);
            if (suggested.HasValue)
            {
                for (var i = 0; i < list.Count; i++)
                {
                    if (!list[i].Equals(suggested.Value))
                        continue;
                    if (i > 0)
                    {
                        list.RemoveAt(i);
                        list.Insert(0, suggested.Value);
                    }
                    break;
                }
            }

            return list;
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

        /// <summary>A gate's lead-in is in use while an aircraft taxis in to it or pushes
        /// back from it. It is free once the departure reaches Holding Short.</summary>
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
                                .Advance(DeparturePrep.TotalSeconds(aircraft.Type, CareerState.BaseLevel));
                            if (prepEnd.CompareTo(readyAt) > 0)
                                readyAt = prepEnd;
                        }

                        Consider(readyAt);
                        if (readyAt.CompareTo(now) <= 0)
                        {
                            // Ready but still at the stand: it may be waiting for the ground to
                            // clear, which is re-checked on the grid. Curfew is a wall-clock wait.
                            if (!ExemptFromCurfew(aircraft) && AirportCurfew.IsClosed(now, Clock))
                                Consider(AirportCurfew.OpensAt(now, Clock));
                            else
                                Consider(GroundTraffic.NextGrid(now));
                            if (AdelaideGround.IsTerminalGate(aircraft.Stand))
                                taxiReleaseWantedGate = true;
                            else
                                taxiReleaseWantedBay = true;
                        }
                    }
                }
                if (aircraft.Airline.IsEmergency && aircraft.State == FleetState.AtStand
                    && !aircraft.Scheduled.HasValue)
                {
                    if (AirportCurfew.IsClosed(now, Clock))
                        Consider(GroundTraffic.NextGrid(now));
                    else
                    {
                        var local = Clock.LocalAt(now);
                        // First closed instant: 23:00 is still the last open minute.
                        var tonight = local.Date.AddMinutes(AirportCurfew.LastMovementMinute).AddSeconds(1);
                        if (local >= tonight)
                            tonight = tonight.AddDays(1);
                        Consider(Clock.AtLocal(tonight));
                    }
                }
                if (aircraft.State is FleetState.HoldingShort or FleetState.HoldingForLanding)
                    runwayWanted = true;
                // Waiting at the exit with a stand to go to: the taxi-in may be held for traffic.
                // Player aircraft first get a fixed decision window to pick a stand themselves.
                if (aircraft.State == FleetState.AwaitingStand)
                {
                    if (aircraft.Airline.IsPlayer)
                    {
                        var autoAt = aircraft.StateStartedAt.Advance(PlayerStandAutoSeconds);
                        if (autoAt.CompareTo(now) > 0)
                            Consider(autoAt);
                        else if (SuggestStand(aircraft) != null)
                            Consider(GroundTraffic.NextGrid(now));
                    }
                    else if (SuggestStand(aircraft) != null)
                        Consider(GroundTraffic.NextGrid(now));
                }
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
                        if (AirportCurfew.IsClosed(now, Clock) && !ExemptFromCurfew(aircraft))
                            Consider(AirportCurfew.OpensAt(now, Clock));
                        else
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
                if (AirportCurfew.IsClosed(now, Clock))
                {
                    var anyExempt = false;
                    foreach (var waiting in _fleet)
                    {
                        if (waiting.State is not (FleetState.HoldingForLanding or FleetState.HoldingShort))
                            continue;
                        if (!ExemptFromCurfew(waiting))
                            continue;
                        anyExempt = true;
                        break;
                    }

                    if (!anyExempt)
                        Consider(AirportCurfew.OpensAt(now, Clock));
                }
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
            if (aircraft.CheckUntil is { } checkEnds && departAt.CompareTo(checkEnds) < 0)
                return CommandResult.Refused(
                    $"{aircraft.Registration} is in its check until {Clock.TimeText(checkEnds)}. Pick a later time.");
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
                var recoveryCredit = CareerState.Funds + alreadyPaid < cost
                    && alreadyPaid == 0
                    && CareerState.TryChargeRecoveryDispatch(cost, destination.Code);
                if (CareerState.Funds + alreadyPaid < cost && !recoveryCredit)
                    return CommandResult.Refused(
                        $"This flight costs ${cost:N0}; you have ${CareerState.Funds:N0}.");
                if (!recoveryCredit)
                {
                    if (alreadyPaid > 0) CareerState.RefundDispatch(alreadyPaid);
                    CareerState.TryChargeDispatch(cost);
                }
                // Align prep with the booked pushback: start TotalSeconds before depart
                // (or now if that is already later). Recompute on every book so an earlier
                // rebook cannot leave a future PrepStartedAt that blocks pushback forever.
                var total = DeparturePrep.TotalSeconds(aircraft.Type, CareerState.BaseLevel);
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
            if (PlayerFleetCount() >= AircraftAcquisition.MaxPlayerAircraft)
                return CommandResult.Refused($"Fleet is full ({AircraftAcquisition.MaxPlayerAircraft} aircraft).");
            if (owned >= CareerState.Base.FleetCapacity)
                return CommandResult.Refused($"{CareerState.Base.Title} supports {CareerState.Base.FleetCapacity} aircraft. Expand your Adelaide base first.");
            if (!PlayerBase.Supports(CareerState.BaseLevel, type))
            {
                var needed = AircraftCatalogue.IsWidebody(type) ? PlayerBaseLevel.International : PlayerBaseLevel.JetGate;
                return CommandResult.Refused($"{Article.CapitalA(type.Name)} needs the {PlayerBase.For(needed).Title}.");
            }
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

            CareerState.EvaluateTier(PlayerOwnedTypes(), PlayerFleetCount());
            CheckCareerFinale();
            return CommandResult.Ok;
        }

        public const int OutstationCapacity = 8;
        private static readonly string[] OutstationCandidates = { "MEL", "SYD", "BNE", "PER" };

        public long NextOutstationCost => CareerState.OutstationBases.Count == 0 ? 40_000 : 65_000;

        public CommandResult OpenOutstationBase(string code)
        {
            if (CareerState.Tier < OperatingTier.Domestic)
                return CommandResult.Refused("A domestic operating tier is required for another base.");
            var allowed = false;
            foreach (var candidate in OutstationCandidates)
                if (candidate == code) allowed = true;
            if (!allowed) return CommandResult.Refused("Choose Melbourne, Sydney, Brisbane or Perth for an outstation base.");
            if (CareerState.OutstationBases.Contains(code)) return CommandResult.Refused("That base is already open.");
            if (CareerState.OutstationBases.Count >= 3) return CommandResult.Refused("The network already has three outstation bases.");
            var cost = NextOutstationCost;
            if (!CareerState.TryChargePurchase(cost))
                return CommandResult.Refused($"Opening this base costs ${cost:N0}; you have ${CareerState.Funds:N0}.");
            CareerState.AddOutstationBase(code);
            CareerState.EvaluateTier(PlayerOwnedTypes(), PlayerFleetCount());
            CheckCareerFinale();
            return CommandResult.Ok;
        }

        public CommandResult BuyAircraftAtOutstation(AircraftType type, string baseCode)
        {
            if (!CareerState.OutstationBases.Contains(baseCode))
                return CommandResult.Refused("Open that base first.");
            if (type == null || !AircraftAcquisition.TryFor(type, out var offer))
                return CommandResult.Refused("That aircraft is not for sale.");
            if (PlayerFleetCount() >= AircraftAcquisition.MaxPlayerAircraft)
                return CommandResult.Refused("The airline fleet is full.");
            var based = 0;
            foreach (var aircraft in _outstationFleet)
                if (aircraft.BaseCode == baseCode) based++;
            if (based >= OutstationCapacity)
                return CommandResult.Refused($"{baseCode} supports {OutstationCapacity} based aircraft.");
            if (!PlayerBase.Supports(CareerState.BaseLevel, type) || CareerState.Tier < offer.RequiredTier
                || CareerState.Reliability < offer.RequiredReliability
                || CareerState.CompletedPlayerRotations < offer.RequiredRotations)
                return CommandResult.Refused("Aircraft capability, reliability or service requirement is not met.");
            if (!CareerState.TryChargePurchase(offer.Price))
                return CommandResult.Refused($"{type.Name} costs ${offer.Price:N0}; you have ${CareerState.Funds:N0}.");
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
            CareerState.EvaluateTier(PlayerOwnedTypes(), PlayerFleetCount());
            CheckCareerFinale();
            return CommandResult.Ok;
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
            if (aircraft.HasFlight) return CommandResult.Refused("That aircraft already has a service.");
            if (aircraft.InCheck(_processedTo.ElapsedSeconds))
                return CommandResult.Refused("That aircraft is in a routine check.");
            if (aircraft.CheckDue)
                return CommandResult.Refused("Routine check is due before the next service.");
            if (!DestinationCatalogue.TryFind(aircraft.BaseCode, out var origin)
                || !DestinationCatalogue.TryFind(destinationCode, out var destination))
                return CommandResult.Refused("Unknown network destination.");
            if (destinationCode == aircraft.BaseCode) return CommandResult.Refused("Choose another destination.");
            // Any Adelaide movement must use the rendered airport's real runway and stand
            // reservations. Network aircraft therefore work only routes outside Adelaide.
            if (destinationCode == Home.Code)
                return CommandResult.Refused("Adelaide services must use an aircraft based at Adelaide.");
            var km = origin.DistanceKmTo(destination);
            if (!aircraft.Type.CanReach(km) || !RouteAccess.Allows(aircraft.Type, destination))
                return CommandResult.Refused("That aircraft cannot operate this route.");
            if (RouteAccess.BandOf(destination) >= RouteBand.Tasman
                && CareerState.Tier < OperatingTier.International)
                return CommandResult.Refused("International network service requires International tier.");
            if (departAt.CompareTo(_processedTo) < 0)
                return CommandResult.Refused("Departure time is in the past.");
            var cost = FlightEconomics.DispatchCost(aircraft.Type, km);
            if (!CareerState.TryChargeDispatch(cost))
                return CommandResult.Refused($"This service costs ${cost:N0}; you have ${CareerState.Funds:N0}.");
            var duration = 2 * LegTiming.AirborneSeconds(km, aircraft.Type) + 45 * 60;
            aircraft.Plan(destinationCode, departAt.ElapsedSeconds, departAt.ElapsedSeconds + duration, automated);
            return CommandResult.Ok;
        }

        public CommandResult StartOutstationCheck(string registration)
        {
            var aircraft = _outstationFleet.Find(a => a.Registration == registration);
            if (aircraft == null) return CommandResult.Refused("Unknown outstation aircraft.");
            if (aircraft.HasFlight) return CommandResult.Refused("Finish the service before a check.");
            if (aircraft.InCheck(_processedTo.ElapsedSeconds))
                return CommandResult.Refused("A check is already under way.");
            if (!aircraft.CheckDue) return CommandResult.Refused("This aircraft is not due for a check.");
            var cost = Maintenance.CheckCost(aircraft.Type, PlayerBaseLevel.ExpandedRegional);
            if (!CareerState.TryChargePurchase(cost))
                return CommandResult.Refused($"Routine check costs ${cost:N0}; you have ${CareerState.Funds:N0}.");
            aircraft.StartCheck(_processedTo.ElapsedSeconds
                + Maintenance.CheckSeconds(aircraft.Type, PlayerBaseLevel.ExpandedRegional));
            return CommandResult.Ok;
        }

        public CommandResult SetRepeatSchedule(string registration, string destinationCode, int intervalHours)
        {
            if (!DelegationUnlocked)
                return CommandResult.Refused("Complete 12 manually planned services to unlock delegation.");
            if (intervalHours != 6 && intervalHours != 12 && intervalHours != 24)
                return CommandResult.Refused("Choose a 6, 12 or 24 hour repeat interval.");
            var local = _fleet.Find(a => a.Registration == registration && a.Airline.IsPlayer);
            var remote = _outstationFleet.Find(a => a.Registration == registration);
            if (local == null && remote == null) return CommandResult.Refused("Unknown player aircraft.");
            if (!DestinationCatalogue.TryFind(destinationCode, out var destination))
                return CommandResult.Refused("Unknown destination.");
            if (local != null && !CanOperate(local, destination))
                return CommandResult.Refused("That Adelaide aircraft cannot operate this route.");
            if (remote != null)
            {
                if (destinationCode == Home.Code || destinationCode == remote.BaseCode
                    || !DestinationCatalogue.TryFind(remote.BaseCode, out var origin)
                    || !remote.Type.CanReach(origin.DistanceKmTo(destination))
                    || !RouteAccess.Allows(remote.Type, destination)
                    || (RouteAccess.BandOf(destination) >= RouteBand.Tasman
                        && CareerState.Tier < OperatingTier.International))
                    return CommandResult.Refused("That outstation aircraft cannot operate this route.");
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
                var forecast = RouteForecast.For(origin, destination, aircraft.Type);
                aircraft.Complete();
                RouteContractDefinition matching = null;
                var active = CareerState.ActiveContract;
                if (active != null && CareerState.TryFindDefinition(active.DefinitionId, out var definition)
                    && definition.EligibleType.Id == aircraft.Type.Id
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
                CareerState.EvaluateTier(PlayerOwnedTypes(), PlayerFleetCount());
                CheckCareerFinale();
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
                    plan.Exception = "Routine check due; complete a check before resuming.";
                    continue;
                }
                if (local == null && remote == null)
                {
                    plan.Paused = true;
                    plan.Exception = "Aircraft is no longer available.";
                    continue;
                }
                if (!DestinationCatalogue.TryFind(plan.DestinationCode, out var destination))
                {
                    plan.Paused = true;
                    plan.Exception = "Destination is unavailable.";
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

        internal void RestoreNetworkState(IEnumerable<OutstationAircraft> aircraft,
            IEnumerable<RepeatSchedule> schedules)
        {
            _outstationFleet.Clear();
            _repeatSchedules.Clear();
            if (aircraft != null) _outstationFleet.AddRange(aircraft);
            if (schedules != null) _repeatSchedules.AddRange(schedules);
            _repeatSchedulesLive = false;
        }

        /// <summary>Expand the player's leased Adelaide operating footprint (ADR 0091).</summary>
        public CommandResult UpgradePlayerBase()
        {
            if (CareerState == null)
                return CommandResult.Refused("No career to expand.");
            if (!PlayerBase.TryNext(CareerState.BaseLevel, out var next))
                return CommandResult.Refused("Your Adelaide base is already fully developed.");
            if (CareerState.Tier < next.RequiredTier)
                return CommandResult.Refused($"{next.Title} needs {next.RequiredTier} tier.");
            if (CareerState.CompletedPlayerRotations < next.RequiredRotations)
                return CommandResult.Refused($"{next.Title} needs {next.RequiredRotations} completed rotations.");
            if (!CareerState.TryChargePurchase(next.UpgradeCost))
                return CommandResult.Refused(next.Title + " costs $" + next.UpgradeCost.ToString("N0")
                                             + "; you have $" + CareerState.Funds.ToString("N0") + ".");

            CareerState.BaseLevel = next.Level;
            CareerState.EvaluateTier(PlayerOwnedTypes(), PlayerFleetCount());
            CheckCareerFinale();
            return CommandResult.Ok;
        }

        public IReadOnlyList<CareerGoalStatus> CareerGoals() =>
            CareerRoadmap.Evaluate(CareerState, PlayerOwnedTypes(), PlayerFleetCount());

        public CareerGoalStatus PinnedCareerGoal() =>
            CareerRoadmap.Pinned(CareerState, PlayerOwnedTypes(), PlayerFleetCount());

        public CommandResult PinCareerGoal(string id)
        {
            foreach (var goal in CareerGoals())
            {
                if (goal.Id != id || goal.Stage > CareerState.Tier) continue;
                CareerState.PinGoal(id);
                return CommandResult.Ok;
            }
            return CommandResult.Refused("That career goal is not available yet.");
        }

        private void CheckCareerFinale()
        {
            if (CareerRoadmap.FinaleReady(CareerState, PlayerOwnedTypes(), PlayerFleetCount())
                && CareerState.TryAward(CareerRoadmap.FinaleKey, 0))
                CareerState.MarkFinale();
        }

        /// <summary>The career campaign as the airline stands now (ADR 0083).</summary>
        public IReadOnlyList<CampaignChapter> CampaignChapters() =>
            Campaign.Evaluate(CareerState, PlayerAirline == null ? null : PlayerOwnedTypes());

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
        /// Takes a parked player aircraft out of service for its routine check (ADR 0085): pays
        /// for it, grounds it for <see cref="Maintenance.CheckSeconds"/>, and resets its wear.
        /// </summary>
        public CommandResult StartCheck(FleetAircraft aircraft)
        {
            if (aircraft == null || !_fleet.Contains(aircraft))
                return CommandResult.Refused("No such aircraft.");
            if (!aircraft.Airline.IsPlayer)
                return CommandResult.Refused("Only your own aircraft can be sent for a check.");
            if (aircraft.State != FleetState.AtStand)
                return CommandResult.Refused($"{aircraft.Registration} must be parked at its stand for a check.");
            if (aircraft.Scheduled.HasValue)
                return CommandResult.Refused($"{aircraft.Registration} has a flight booked. Cancel it first.");
            if (Maintenance.InCheck(aircraft, _processedTo))
                return CommandResult.Refused($"{aircraft.Registration} is already in its check.");
            if (CareerState == null)
                return CommandResult.Refused("No career to charge the check against.");
            var cost = Maintenance.CheckCost(aircraft.Type, CareerState.BaseLevel);
            if (!CareerState.TryChargePurchase(cost))
                return CommandResult.Refused($"A check costs ${cost:N0}; you have ${CareerState.Funds:N0}.");

            aircraft.RotationsSinceCheck = 0;
            aircraft.CheckUntil = _processedTo.Advance(Maintenance.CheckSeconds(aircraft.Type, CareerState.BaseLevel));
            return CommandResult.Ok;
        }

        internal void RestoreMaintenance(string registration, int rotationsSinceCheck, long checkUntilSeconds)
        {
            var aircraft = _fleet.Find(a => string.Equals(a.Registration, registration, StringComparison.OrdinalIgnoreCase));
            if (aircraft == null)
                return;
            aircraft.RotationsSinceCheck = Math.Max(0, rotationsSinceCheck);
            aircraft.CheckUntil = checkUntilSeconds > 0 ? new SimulationTime(checkUntilSeconds) : null;
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
            if (Maintenance.InCheck(aircraft, _processedTo))
                return CommandResult.Refused($"{aircraft.Registration} is in its check until {Clock.TimeText(aircraft.CheckUntil.Value)}.");
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
            var forecast = RouteForecast.For(Home, justFlown.Value, aircraft.Type);
            var pay = forecast.Revenue;
            if (aircraft.Airline.IsPlayer && aircraft.PushbackLatenessSeconds.HasValue)
            {
                CareerState.ApplyPunctuality(
                    FlightEconomics.PunctualityReliabilityDelta(aircraft.PushbackLatenessSeconds.Value));
                aircraft.PushbackLatenessSeconds = null;
            }

            var settlement = CareerState.RecordCompletedRotation(
                settlementId, pay, matching, PlayerOwnedTypes(), now, PlayerFleetCount());
            if (settlement == null)
                return;

            if (aircraft.Airline.IsPlayer)
            {
                var dispatchCost = forecast.Cost;
                var completionBonus = settlement.Value.ContractFulfilled ? matching.CompletionReward : 0;
                CareerState.RecordService(justFlown.Value.Code,
                    settlement.Value.Payment - completionBonus - dispatchCost,
                    manual: !aircraft.AutomatedTrip);
                aircraft.AutomatedTrip = false;
                CareerState.EvaluateTier(PlayerOwnedTypes(), PlayerFleetCount());
                CheckCareerFinale();
            }

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
            if (aircraft.Airline.IsPlayer && CareerState != null
                && !PlayerBase.CanUseStand(CareerState.BaseLevel, aircraft.Type, stand))
                return CommandResult.Refused(
                    $"{AdelaideGround.StandLabel(stand)} is outside your {CareerState.Base.Title} allocation.");
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
            ProcessOutstationServices(target);
            if (_repeatSchedulesLive) ProcessRepeatSchedules(target);
            ProcessDue(_processedTo);
            // A Cathay that flew its last rotation home during this step is retired off-map.
            if (!IsCathaySeason(target))
                RetireOutOfSeasonOperators(at: target);
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

                    if (aircraft.Airline.IsEmergency && !aircraft.Scheduled.HasValue
                        && AirportCurfew.IsClosed(now, Clock))
                    {
                        ScheduleAiDeparture(aircraft, now);
                        return aircraft.Scheduled.HasValue;
                    }

                    if (!aircraft.Scheduled.HasValue || aircraft.Scheduled.Value.DepartAt.CompareTo(now) > 0)
                        return false;
                    if (!ExemptFromCurfew(aircraft) && AirportCurfew.IsClosed(now, Clock))
                        return false;
                    if (aircraft.Airline.IsPlayer && !aircraft.PrepStartedAt.HasValue)
                    {
                        var inferred = aircraft.Scheduled.Value.DepartAt.ElapsedSeconds
                            - DeparturePrep.TotalSeconds(aircraft.Type, CareerState.BaseLevel);
                        aircraft.PrepStartedAt = new SimulationTime(inferred < 0 ? 0 : inferred);
                    }
                    if (!DeparturePrep.IsReady(aircraft, now, CareerState.BaseLevel))
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
                    // Out of season Cathay stays in Hong Kong; it is retired while away.
                    if (aircraft.Airline.Id.Value == "CPA" && !IsCathaySeason(now))
                        return false;
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
                    if (!ExemptFromCurfew(aircraft) && AirportCurfew.IsClosed(now, Clock))
                    {
                        // Kept out overnight: fly in with the morning arrivals, not all at 05:00.
                        aircraft.ExtendUntil(MorningArrivalAt(aircraft, now));
                        return false;
                    }
                    // Do not land an aircraft that has nowhere to park. Before this guard a
                    // late arrival could touch down onto a full apron just before curfew and
                    // sit across the runway-exit taxiway until the 05:00 departure wave. Keep
                    // it in flow control and recheck at a useful interval instead.
                    var arrivalStand = SuggestStand(aircraft);
                    if (!aircraft.Airline.IsPlayer && arrivalStand == null)
                    {
                        aircraft.ExtendUntil(now.Advance(5 * 60));
                        return false;
                    }
                    // AI arrivals reserve the chosen position before joining final. Without
                    // this, simultaneous runway queues could both see the same free gate and
                    // the second aircraft would land with nowhere to go.
                    if (!aircraft.Airline.IsPlayer)
                        aircraft.Stand = arrivalStand.Value;
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
                    // Player: wait for AssignStand, or auto-park after PlayerStandAutoSeconds
                    // so a missed toast never strands them (ADR 0056). The deadline is fixed
                    // from StateStartedAt — any step size / skip lands on the same moment.
                    if (aircraft.Airline.IsPlayer
                        && now.ElapsedSeconds - aircraft.StateStartedAt.ElapsedSeconds < PlayerStandAutoSeconds)
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
                    aircraft.RotationsSinceCheck++;
                    // A rotation begun with the check already overdue (ADR 0085).
                    if (aircraft.Airline.IsPlayer && aircraft.RotationsSinceCheck > Maintenance.IntervalRotations)
                        CareerState?.ApplyPunctuality(-Maintenance.OverduePenalty);
                    aircraft.WentAroundThisTrip = false;
                    // Transition before clearing the destination so the AtStand event still
                    // carries the route for the Operations board history strip.
                    Transition(aircraft, FleetState.AtStand, now, null);
                    aircraft.CurrentDestination = null;
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

            // An AI arrival reserves its stand before joining final. Keep that reservation
            // through a go-around and the runway roll, then taxi to the same position.
            if (!string.IsNullOrEmpty(aircraft.Stand.Value)
                && _stands.Contains(aircraft.Stand)
                && StandFits(aircraft.Type, aircraft.Stand)
                && IsStandFree(aircraft.Stand, aircraft))
                return aircraft.Stand;

            // Scheduled terminal operators return to their own gate when it is available.
            // This keeps the Air New Zealand and Virgin streams operationally legible while
            // still allowing an alternate compatible gate if the home position is occupied.
            if (NeedsTerminalGate(aircraft.Type)
                && !string.IsNullOrEmpty(aircraft.DepartureStand.Value)
                && _stands.Contains(aircraft.DepartureStand)
                && StandFits(aircraft.Type, aircraft.DepartureStand)
                && IsStandFree(aircraft.DepartureStand)
                && IsLeadInFree(aircraft.DepartureStand, aircraft)
                && (!aircraft.Airline.IsPlayer || CareerState == null
                    || PlayerBase.CanUseStand(CareerState.BaseLevel, aircraft.Type, aircraft.DepartureStand)))
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

            if (aircraft.Airline.IsPlayer && CareerState != null)
                return SuggestPlayerStandFor(aircraft.Type, aircraft);

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

        private StableId? SuggestPlayerStandFor(AircraftType type, FleetAircraft except = null)
        {
            if (type == null || CareerState == null || !PlayerBase.Supports(CareerState.BaseLevel, type))
                return null;

            // First use the player's actual leased positions. This makes the base visible in
            // day-to-day operations and keeps jet access tied to gates 27/29 (plus pier 28 at
            // International) instead of silently using any terminal gate.
            foreach (var stand in PlayerBase.DedicatedStands(CareerState.BaseLevel, type))
            {
                if (!_stands.Contains(stand) || !StandFits(type, stand) || !IsStandFree(stand))
                    continue;
                if (AdelaideGround.IsTerminalGate(stand) && !IsLeadInFree(stand, except))
                    continue;
                return stand;
            }

            // Expanded regional operations lease capacity across the shared regional apron:
            // if the authored dedicated bay does not fit (e.g. ATR/Dash on walk-out 10A),
            // use another free regional bay. Existing away-aircraft reservation logic still
            // keeps enough shared capacity available for the player's fleet.
            if (!NeedsTerminalGate(type) && CareerState.BaseLevel >= PlayerBaseLevel.ExpandedRegional)
                return SuggestStandFor(type, except, allowPlayerDedicated: true);

            return null;
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
        public StableId? SuggestStandFor(AircraftType type, FleetAircraft except = null,
            bool allowPlayerDedicated = false)
        {
            if (type == null)
                return null;

            StableId? best = null;
            var bestCrowds = true;
            var bestOversized = true;
            var bestSeconds = long.MaxValue;
            foreach (var stand in _stands)
            {
                if (!StandFits(type, stand) || !IsStandFree(stand))
                    continue;
                if (!allowPlayerDedicated && PlayerAirline != null && CareerState != null
                    && PlayerBase.IsDedicatedStand(CareerState.BaseLevel, stand))
                    continue;
                if (AdelaideGround.IsTerminalGate(stand) && !IsLeadInFree(stand, except))
                    continue;
                var crowds = CrowdsNeighbour(type, stand);
                // Keep code E gates for the widebodies that need them (ADR 0110), and the
                // 50-series for the ATR / Q400s that cannot use a Saab walk-out (ADR 0111).
                var oversized = WastesStand(type, stand);
                var seconds = TaxiInSecondsTo(stand, type);
                // Wasting a stand another type needs outranks a tight neighbour: crowding is
                // cosmetic, but a Q400 with every 50-series bay full of Saabs cannot park.
                if (best != null)
                {
                    if (oversized != bestOversized)
                    {
                        if (oversized)
                            continue;
                    }
                    else if (crowds != bestCrowds)
                    {
                        if (crowds)
                            continue;
                    }
                    else if (seconds >= bestSeconds)
                        continue;
                }

                best = stand;
                bestCrowds = crowds;
                bestOversized = oversized;
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
            if (arrival != null && !MayUseRunwayDuringCurfew(arrival, now))
            {
                if (!ExemptFromCurfew(arrival))
                    arrival.ExtendUntil(AirportCurfew.OpensAt(now, Clock));
                arrival = null;
            }
            if (departure != null && !MayUseRunwayDuringCurfew(departure, now))
                departure = null;
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
                var prepEnd = aircraft.PrepStartedAt.Value.Advance(DeparturePrep.TotalSeconds(aircraft.Type, CareerState.BaseLevel));
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
                    return GroundClearanceAt(aircraft, at);

                var ahead = arrivals[0];
                arrivals.RemoveAt(0);
                var landing = AircraftPerformance.For(ahead.Type);
                at = at.Advance(ApproachHold.RemainingFinalSeconds(landing.ApproachSeconds, ahead.Registration)
                                + landing.LandingSeconds
                                + AdelaideGround.ClearOfRunwaySeconds(ahead.Type, ahead.AssignedRunway)
                                + WakeSeparationSeconds(ahead.Type));
            }

            return GroundClearanceAt(aircraft, at);
        }

        /// <summary>Apply the tower's taxi/vacate check to a displayed landing estimate.</summary>
        private SimulationTime GroundClearanceAt(FleetAircraft aircraft, SimulationTime at)
        {
            // Ground releases are checked on the five-second grid. Taxi legs are short, so
            // twelve minutes is a generous bounded horizon while keeping this HUD estimate cheap.
            for (var i = 0; i < 144 && !VacateClearOfTaxiing(aircraft, at); i++)
                at = GroundTraffic.NextGrid(at);
            return at;
        }

        /// <summary>Its vacate, flown from now, stays clear of every aircraft already moving on the ground.</summary>
        private bool VacateClearOfTaxiing(FleetAircraft arrival, SimulationTime now)
        {
            var profile = AircraftPerformance.For(arrival.Type);
            var touchdownIn = ApproachHold.RemainingFinalSeconds(profile.ApproachSeconds, arrival.Registration)
                              + profile.LandingSeconds;
            var vacate = AdelaideGround.VacateFor(arrival.Type, arrival.AssignedRunway);
            if (!GroundTraffic.PathClear(_fleet, arrival, vacate, arrival.AssignedRunway, taxiOut: false,
                    now.Advance(touchdownIn), includeStationary: false))
                return false;

            // A reserved stand lets the tower validate the route beyond the runway exit too.
            // Checking only the vacate allowed an outbound aircraft to cross that waiting point
            // a few seconds after the arrival stopped there.
            if (string.IsNullOrEmpty(arrival.Stand.Value))
                return true;
            var taxiIn = AdelaideGround.TaxiIn(arrival.Stand, arrival.Type, arrival.AssignedRunway);
            return GroundTraffic.PathClear(_fleet, arrival, taxiIn, arrival.AssignedRunway, taxiOut: false,
                now.Advance(touchdownIn + vacate.WholeSeconds), includeStationary: false);
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

        /// <summary>Player and RFDS may use the runway during the 23:00–05:00 curfew.</summary>
        public static bool ExemptFromCurfew(FleetAircraft aircraft) =>
            aircraft?.Airline != null && (aircraft.Airline.IsPlayer || aircraft.Airline.IsEmergency);

        private bool MayUseRunwayDuringCurfew(FleetAircraft aircraft, SimulationTime now)
        {
            if (ExemptFromCurfew(aircraft) || !AirportCurfew.IsClosed(now, Clock))
                return true;
            // Already moving: taxi-before-23:00 may take off; an arrival already on
            // short final may land. New commercial inbounds wait until 05:00.
            if (aircraft.State is FleetState.HoldingShort or FleetState.TaxiOut or FleetState.TakingOff)
                return true;
            return aircraft.State == FleetState.HoldingForLanding
                && !AirportCurfew.IsClosed(aircraft.StateStartedAt, Clock);
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
            !string.IsNullOrEmpty(aircraft.Stand.Value)
            && (aircraft.State is FleetState.AtStand or FleetState.HoldingForLanding or FleetState.Landing
                or FleetState.GoAround or FleetState.AwaitingStand or FleetState.TaxiIn);

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
        /// First commercial AI push hour: the 05:00 first wave (ADR 0110). The player and
        /// RFDS may go earlier.
        /// </summary>
        public const int AiFirstDepartureHour = AirportCurfew.OpensAtHour;

        /// <summary>
        /// Last commercial AI push hour (22:00–23:00; the 23:00 mark is the last flight).
        /// a taxi that started before then is allowed to take off. Regionals skip
        /// the late-international hole via the hour profile.
        /// </summary>
        public const int AiLastDepartureHour = 22;

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

        /// <summary>RFDS emergency legs from Adelaide — SA regionals, any hour.</summary>
        public static readonly IReadOnlyList<(string Code, int Weight)> RfdsNetwork = new[]
        {
            ("PLO", 2), ("MGB", 2), ("CED", 2), ("CPD", 2), ("WYA", 1), ("KGC", 1), ("BHQ", 1)
        };

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
            "RFDS" => RfdsNetwork,
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
                var code = VirginRotation[RotationIndex(aircraft, VirginRotation.Count)];
                if (DestinationCatalogue.TryFind(code, out var next) && CanReach(aircraft, next))
                    BookAiDeparture(aircraft, next, now);
                return;
            }

            if (aircraft.Airline.Id.Value == "QFA")
            {
                var code = QantasRotation[RotationIndex(aircraft, QantasRotation.Count)];
                if (DestinationCatalogue.TryFind(code, out var next) && CanReach(aircraft, next))
                    BookAiDeparture(aircraft, next, now);
                return;
            }

            if (aircraft.Airline.Id.Value == "JST")
            {
                var code = JetstarRotation[RotationIndex(aircraft, JetstarRotation.Count)];
                if (DestinationCatalogue.TryFind(code, out var next) && CanReach(aircraft, next))
                    BookAiDeparture(aircraft, next, now);
                return;
            }

            if (aircraft.Airline.Id.Value == "ANZ")
            {
                // Rotation, not a random draw, so international traffic is stable too.
                var code = AirNewZealandRotation[RotationIndex(aircraft, AirNewZealandRotation.Count)];
                if (DestinationCatalogue.TryFind(code, out var next) && CanReach(aircraft, next))
                    BookAiDeparture(aircraft, next, now);
                return;
            }
            var longHaulHome = LongHaulHomeOf(aircraft.Airline.Id.Value);
            if (longHaulHome != null)
            {
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

        /// <summary>
        /// Where this airframe is in its airline's rotation. The registration offsets the start
        /// so six Qantas 737s do not all fly the same first city (ADR 0111).
        /// </summary>
        private static int RotationIndex(FleetAircraft aircraft, int count) =>
            (aircraft.CompletedTrips + FirstWaveKey(aircraft) % count) % count;

        private void BookAiDeparture(FleetAircraft aircraft, Destination destination, SimulationTime now)
        {
            var departAt = AiDepartureWithinHours(now.Advance(AiTurnaroundSeconds(aircraft)), aircraft);
            var disruption = FlightDisruption.For(
                $"{aircraft.Registration}:{aircraft.CompletedTrips}:{destination.Code}",
                departAt, Clock);
            if (disruption.Cancelled)
            {
                aircraft.Scheduled = new ScheduledDeparture(destination, departAt, 0, cancelled: true);
                return;
            }

            if (disruption.Delayed)
                departAt = AiDepartureWithinHours(departAt.Advance(disruption.DelayMinutes * 60L), aircraft);
            departAt = SnapCommercialDeparture(aircraft, departAt);
            departAt = PinLongHaulEvening(aircraft, departAt);
            var delayMinutes = disruption.DelayMinutes;
            if (ShouldNightStopHere(aircraft, destination, departAt))
            {
                departAt = FirstWaveAfter(aircraft, departAt);
                delayMinutes = 0;
            }

            aircraft.Scheduled = new ScheduledDeparture(destination, departAt, delayMinutes);
        }

        /// <summary>
        /// Most Adelaide-based domestic and regional frames stay the night on the apron
        /// rather than fly an evening (18:00+) hop they cannot get back from before 23:00 — that is
        /// what fills the 05:00 first wave (ADR 0111). One in three still flies out and
        /// night-stops at the other end, so the late board is not empty. Long-haul,
        /// player and RFDS are never held.
        /// </summary>
        private bool ShouldNightStopHere(FleetAircraft aircraft, Destination destination, SimulationTime departAt)
        {
            if (ExemptFromCurfew(aircraft) || LongHaulHomeOf(aircraft.Airline.Id.Value) != null
                || FirstWaveKey(aircraft) % 3 == 0)
                return false;
            var local = Clock.LocalAt(departAt);
            if (local.Hour < 18 || AirportCurfew.IsClosed(local))
                return false;
            // Only short domestic / regional hops: a trans-Tasman or Bali leg is a
            // different operation, not an Adelaide-based evening shuttle.
            var leg = AirborneSeconds(aircraft, destination);
            if (leg > 150 * 60)
                return false;
            var backAt = departAt.Advance(2 * leg + DestinationTurnaroundSeconds + 15 * 60);
            var closes = Clock.AtLocal(local.Date.AddMinutes(AirportCurfew.LastMovementMinute));
            return backAt.CompareTo(closes) > 0;
        }

        private SimulationTime FirstWaveAfter(FleetAircraft aircraft, SimulationTime departAt)
        {
            var local = Clock.LocalAt(departAt);
            var day = local.Hour < AirportCurfew.OpensAtHour ? local.Date : local.Date.AddDays(1);
            var at = Clock.AtLocal(AdelaideHourProfile.FirstWaveLocal(day, FirstWaveKey(aircraft)));
            return at.CompareTo(departAt) > 0 ? at : departAt;
        }

        /// <summary>
        /// Cluster commercial AI onto 5-minute ADL bank marks so turns that finish a
        /// minute apart can share a departure time (ADR 0100). Player and RFDS untouched.
        /// </summary>
        private SimulationTime SnapCommercialDeparture(FleetAircraft aircraft, SimulationTime departAt)
        {
            if (aircraft == null || aircraft.Airline.IsPlayer || aircraft.Airline.IsEmergency)
                return departAt;
            var local = Clock.LocalAt(departAt);
            var snapped = AdelaideHourProfile.SnapToBankLocal(local, AiFirstDepartureHour, AiLastDepartureHour,
                FirstWaveKey(aircraft));
            var at = Clock.AtLocal(snapped);
            return at.CompareTo(departAt) > 0 ? at : departAt;
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
        /// Player and RFDS skip the window entirely.
        /// </summary>
        internal SimulationTime AiDepartureWithinHours(SimulationTime readyAt, FleetAircraft aircraft)
        {
            if (aircraft != null && aircraft.Airline.IsEmergency)
            {
                var local = Clock.LocalAt(readyAt);
                if (AirportCurfew.IsClosed(local))
                    return readyAt;
                var tonight = local.Date.AddHours(23).AddMinutes(30);
                if (local >= tonight)
                    tonight = tonight.AddDays(1);
                var at = Clock.AtLocal(tonight);
                return at.CompareTo(readyAt) > 0 ? at : readyAt;
            }

            if (aircraft != null && ExemptFromCurfew(aircraft))
                return readyAt;
            return PinLongHaulEvening(aircraft,
                AiDepartureWithinHours(readyAt, aircraft?.Type, aircraft == null ? 0 : FirstWaveKey(aircraft)));
        }

        /// <summary>
        /// Stable per-airframe key that spreads night-stopped aircraft across the 05:00–06:30
        /// first wave instead of stacking every one on 05:00 (ADR 0110).
        /// </summary>
        internal static int FirstWaveKey(FleetAircraft aircraft)
        {
            unchecked
            {
                var hash = 23;
                foreach (var ch in aircraft.Registration)
                    hash = hash * 31 + ch;
                return hash & int.MaxValue;
            }
        }

        /// <summary>
        /// Qatar and Emirates keep the real ~22:00 Adelaide departure when they
        /// are already in the evening window. A morning-ready jet still turns
        /// normally so it does not occupy a gate until night.
        /// </summary>
        private SimulationTime PinLongHaulEvening(FleetAircraft aircraft, SimulationTime departAt)
        {
            if (aircraft == null)
                return departAt;
            var id = aircraft.Airline.Id.Value;
            if (id != "UAE" && id != "QTR")
                return departAt;
            var local = Clock.LocalAt(departAt);
            if (local.Hour < 18 || local.Hour >= 22 || AirportCurfew.IsClosed(local))
                return departAt;
            var at = Clock.AtLocal(local.Date.AddHours(22));
            return at.CompareTo(departAt) > 0 ? at : departAt;
        }

        internal SimulationTime AiDepartureWithinHours(SimulationTime readyAt, AircraftType type = null,
            int firstWaveKey = 0)
        {
            var local = Clock.LocalAt(readyAt);
            DateTime useful;
            if (type != null && NeedsTerminalGate(type))
            {
                if (!AirportCurfew.IsClosed(local))
                    return readyAt;
                // Night-stop: the jet stays on its gate and leads tomorrow's first wave.
                var day = local.Hour < AirportCurfew.OpensAtHour ? local.Date : local.Date.AddDays(1);
                useful = AdelaideHourProfile.FirstWaveLocal(day, firstWaveKey);
            }
            else
            {
                useful = AdelaideHourProfile.NextUsefulLocal(local, AiFirstDepartureHour, AiLastDepartureHour);
                if (useful == local)
                    return readyAt;
                // Rolled past the evening: night-stop on the bay, out with the first wave.
                if (useful.Date != local.Date || local.Hour < AiFirstDepartureHour)
                    useful = AdelaideHourProfile.FirstWaveLocal(useful.Date, firstWaveKey);
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
