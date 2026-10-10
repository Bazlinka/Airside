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
    public enum CareerEventKind
    {
        GoalComplete,
        TierReached,
        Finale,
        /// <summary>A contract's deadline passed before it was flown (ADR 0127).</summary>
        ContractExpired,
        /// <summary>A demand event starts: somewhere is busier today.</summary>
        News,
        /// <summary>The day's results at curfew.</summary>
        DailyReport,
        /// <summary>A challenge completed and paid.</summary>
        Challenge,
        /// <summary>A milestone (achievement) unlocked.</summary>
        Milestone,
        /// <summary>An individual player aircraft reached a logbook distinction (ADR 0178).</summary>
        AircraftMilestone
    }

    /// <summary>Career news for the HUD: announced once, never stored in the save.</summary>
    public readonly struct CareerEvent
    {
        public CareerEvent(CareerEventKind kind, OperatingTier tier, string text,
            string registration = null, string aircraftTypeId = null, int flights = 0, string title = null)
        {
            Kind = kind;
            Tier = tier;
            Text = text ?? string.Empty;
            Registration = registration ?? string.Empty;
            AircraftTypeId = aircraftTypeId ?? string.Empty;
            Flights = Math.Max(0, flights);
            Title = title ?? string.Empty;
        }

        public CareerEventKind Kind { get; }
        public OperatingTier Tier { get; }
        public string Text { get; }
        public string Registration { get; }
        public string AircraftTypeId { get; }
        public int Flights { get; }
        public string Title { get; }
    }

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
    /// The airport runs itself. The tower owns separate queues for 05/23 and 12/30,
    /// with shared intersection occupancy and applicable cross-flight wake separation.
    /// Arrivals normally precede departures. Owners only choose where and when an aircraft
    /// goes, and which stand it parks on. AI airlines make both choices automatically.
    /// </summary>
    public sealed partial class AirlineOperations
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
        public static readonly IReadOnlyList<StableId> AdelaideRegionalBays = StandIds(AdelaideLayout.Bays);

        /// <summary>Terminal gates (ADR 0047): jets only, a separate stand system from the regional bays.</summary>
        public static readonly IReadOnlyList<StableId> AdelaideTerminalGates = StandIds(AdelaideLayout.TerminalGates);

        /// <summary>The helipad spots (ADR 0207): helicopters only, no taxiway.</summary>
        public static readonly IReadOnlyList<StableId> AdelaideHelipadStands = HelipadStandIds();

        /// <summary>Every stand at Adelaide: the regional bays, the terminal gates, then the helipad spots.</summary>
        public static readonly IReadOnlyList<StableId> AdelaideStands = CombinedStands();

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

        /// <summary>
        /// T1 contact positions that take a code E widebody (A330, 787, A350): the MARS
        /// centre lines of the 18 / 20 / 22 / 28 piers and the 25 / 26 international gates,
        /// which is where Cathay, Singapore, Malaysia, Emirates, Qatar and the player's
        /// International widebody lease already park. Every other gate is code C
        /// (737 / A320 family and smaller). ADR 0110.
        /// </summary>
        public static readonly IReadOnlyList<StableId> CodeFGates = new[]
        {
            new StableId("GATE-16R"), new StableId("GATE-18R"),
            new StableId("GATE-20R"), new StableId("GATE-22R")
        };

        public static readonly IReadOnlyList<StableId> CodeEGates = new[]
        {
            new StableId("GATE-18"), new StableId("GATE-20"), new StableId("GATE-22L"),
            new StableId("GATE-25"), new StableId("GATE-26L"), new StableId("GATE-28L")
        };

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
            (Airline.Rfds, new[] { ("VH-FDA", AircraftType.Saab340) }),
            // SA Ambulance rescue helicopter on Helipad West (ADR 0207); the Bell 412EP Babcock flies for SA.
            (Airline.SaAmbulance, new[] { ("VH-SAR", AircraftType.Bell412) })
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
            AirlineClock airlineClock = null, CareerDifficulty difficulty = CareerDifficulty.Standard,
            bool firstFlightCoaching = true)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (!player.IsPlayer) throw new ArgumentException("The starting airline must be the player's.", nameof(player));

            var operations = new AirlineOperations(clock, random, DestinationCatalogue.Adelaide, AdelaideStands);
            // Book against the real Adelaide clock first. Assigning it after scheduling
            // used to stamp an 08:00 morning peak onto whatever live hour you launched.
            operations.Clock = airlineClock ?? AirlineClock.Default;
            operations.CareerState = new AirlineCareerState(difficulty: difficulty);
            operations.FirstFlightCoaching = firstFlightCoaching;
            operations.AddAirline(player);
            var foundingAircraft = operations.AddAircraft(player, "VH-PAX", AircraftType.Saab340, AdelaideRegionalBays[0]);
            foundingAircraft.IsFoundingAircraft = true;
            var aiFleet = new List<FleetAircraft>();
            // RFDS first so the emergency aircraft always has a bay; the larger regional
            // fleets overflow to night-stops away rather than squeezing it out (ADR 0111).
            operations.AddMissingEmergencyOperators(aiFleet);
            operations.AddMissingRegionalCarriers(aiFleet);
            var terminalFleet = new List<FleetAircraft>();
            operations.AddMissingTerminalOperators(terminalFleet);
            operations.SeedOpeningTraffic(aiFleet, terminalFleet);
            // Cargo keeps its own night bank; never rewrite it onto the passenger opening ladder.
            operations.AddMissingFreightOperators();
            return operations;
        }

        /// <summary>
        /// Last commercial arrival of the opening bank: 22:50, ten minutes before curfew.
        /// </summary>
        public const int LastOpeningArrivalMinute = AirportCurfew.ClosedFromHour * 60 - 10;

        public Destination Home { get; }
        public IReadOnlyList<OutstationAircraft> OutstationFleet => _outstationFleet;
        public IReadOnlyList<RepeatSchedule> RepeatSchedules => _repeatSchedules;
        public bool DelegationUnlocked => CareerState != null && CareerState.ManualRotations >= 12;

        public void RecordActivePlaySecond() => CareerState?.AddActivePlaySecond();


        /// <summary>Maps simulation time to real Adelaide time. Live: one simulated second per real second.</summary>
        public AirlineClock Clock
        {
            get => _airlineClock;
            internal set
            {
                _airlineClock = value ?? AirlineClock.Default;
                // The weather's fog hours are local-time rules: keep them on this clock (a different epoch
                // otherwise left fog, and the helicopters it grounds, hours out of step with the HUD).
                Weather.UseClock(_airlineClock);
            }
        }

        private AirlineClock _airlineClock = AirlineClock.Default;

        /// <summary>Simulation time everything has been resolved up to.</summary>
        public SimulationTime ProcessedTo => _processedTo;

        /// <summary>Physical-strip scheduling deadline for 05/23; follower wake/crossings may hold longer.</summary>
        public SimulationTime RunwayFreeAt => _mainRunwayFreeAt;

        /// <summary>Physical-strip scheduling deadline for 12/30; follower wake/crossings may hold longer.</summary>
        public SimulationTime CrossRunwayFreeAt => _crossRunwayFreeAt;

        public AirportWeatherTimeline WeatherTimeline { get; } = new();
        public void ObserveWeather(LiveWeatherSnapshot sample, long validSeconds = LiveWeather.StaleSeconds)
        {
            // Preserve already-entered finals before replacing weather at the same
            // whole-second timestamp (including the opening arrival bank at time zero).
            if (WeatherAt(_processedTo) != WeatherKind.Storm && sample.Kind == WeatherKind.Storm)
                foreach (var aircraft in _fleet)
                    if (ApproachRules.EnteredFinalBeforeStorm(aircraft, _processedTo, WeatherAt))
                        aircraft.ArrivalCommittedBeforeStorm = true;
            WeatherTimeline.Observe(_processedTo, sample, validSeconds);
        }
        public WeatherKind WeatherAt(SimulationTime at) => WeatherTimeline.At(at);
        public SurfaceWind WindAt(SimulationTime at) => WeatherTimeline.WindAt(Clock, at);
        public SurfaceWind Wind => WindAt(_processedTo);
        public RunwayDirection ActiveRunway => RunwayWeather.Select(Wind);

        /// <summary>Current forecast, for HUD and tower gating alike.</summary>
        public WeatherKind CurrentWeather => WeatherAt(_processedTo);

        /// <summary>
        /// True while a storm holds new gate releases and arrivals not yet on final.
        /// Movements already underway continue through normal tower separation checks.
        /// </summary>
        public bool IsGroundStopped => CurrentWeather == WeatherKind.Storm;

        /// <summary>Wind-selected end of the cross strip (12/30), for HUD and planners.</summary>
        public RunwayDirection ActiveCrossRunway =>
            RunwayWeather.Select(Wind, AircraftType.Atr42, null, Home);

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
            aircraft.Owner = this;
            _fleet.Add(aircraft);
            if (!airline.IsPlayer && (!airline.IsEmergency || type.IsRotorcraft))
                ScheduleAiDeparture(aircraft, _processedTo);
            return aircraft;
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

        /// <summary>What dispatching this leg costs the player, under the airline's difficulty (ADR 0123).</summary>
        public long DispatchCost(AircraftType type, double km)
        {
            var cost = FlightEconomics.DispatchCost(type, km);
            return CareerState == null ? cost : CareerState.DifficultyProfile.ScaleCost(cost);
        }

        /// <summary>
        /// The first-flight coaching card (ADR 0123). Chosen at setup; experienced players can turn it
        /// off. Saved with the airline.
        /// </summary>
        public bool FirstFlightCoaching { get; set; } = true;

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

        // ---- Runway crossings (ADR 0126) ------------------------------------------------------

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

            if (WeatherTimeline.NextBoundary(now) is { } weatherBoundary) Consider(weatherBoundary);
            if (ContractExpiresAt() is { } expiry)
                Consider(expiry);

            var runwayWanted = false;
            var taxiReleaseWantedBay = false;
            var taxiReleaseWantedGate = false;
            foreach (var aircraft in _fleet)
            {
                if (aircraft.MaintenanceJob is { } job)
                {
                    Consider(job.Waiting ? GroundTraffic.NextGrid(now) : new SimulationTime(job.PhaseEndsAt));
                    if (job.Phase == MaintenancePhase.Taxiing && !string.IsNullOrEmpty(aircraft.Stand.Value))
                        Consider(new SimulationTime(job.PhaseStartedAt + (long)Math.Ceiling(job.Paths(aircraft.Type).PushSeconds) + 60));
                    continue;
                }
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
                        if (readyAt.CompareTo(now) <= 0 && aircraft.Type.IsRotorcraft)
                        {
                            // Ready but held by the pad, the weather or the curfew: a helicopter has no taxiway
                            // to wait on, so the next change is one of those (ADR 0207).
                            Consider(HelipadFreeAt());
                            Consider(Weather.NextBlock(now));
                            if (!ExemptFromCurfew(aircraft) && AirportCurfew.IsClosed(now, Clock))
                                Consider(AirportCurfew.OpensAt(now, Clock));
                        }
                        else if (readyAt.CompareTo(now) <= 0)
                        {
                            // Ready but still at the stand: it may be waiting for the ground to
                            // clear, which is re-checked on the grid. Curfew is a wall-clock wait.
                            if (!ExemptFromCurfew(aircraft) && AirportCurfew.IsClosed(now, Clock))
                                Consider(AirportCurfew.OpensAt(now, Clock));
                            else if (WeatherAt(now) == WeatherKind.Storm)
                            {
                                Consider(Weather.NextBlock(now));
                                continue;
                            }
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
                if (aircraft.State == FleetState.HoldingForLanding)
                    Consider(new SimulationTime(ApproachRules.DecisionPointAt(aircraft.StateStartedAt.ElapsedSeconds)));
                // Waiting at the exit with a stand to go to: the taxi-in may be held for traffic.
                // Player aircraft first get a fixed decision window to pick a stand themselves.
                if (aircraft.State == FleetState.AwaitingStand)
                {
                    if (aircraft.Airline.IsPlayer && string.IsNullOrEmpty(aircraft.Stand.Value))
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
                foreach (var waiting in _fleet)
                    if (waiting.State is FleetState.HoldingShort or FleetState.HoldingForLanding)
                    {
                        Consider(MovementFreeAt(waiting, waiting.State == FleetState.HoldingForLanding));
                        var occupier = IntersectingRunwayOccupier(RunwayWeather.IsMainRunway(waiting.AssignedRunway), now);
                        if (occupier != null && IntersectionBusyUntil(occupier).HasValue)
                            Consider(IntersectionBusyUntil(occupier).Value);
                    }
                // An arrival still holding with its strip already free is being held for taxiing
                // traffic: re-check it on the ground-control grid.
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
                // A departure held short for crossing traffic (ADR 0126) is re-checked on the grid.
                foreach (var aircraft in _fleet)
                {
                    if (aircraft.State != FleetState.HoldingShort)
                        continue;
                    var main = RunwayWeather.IsMainRunway(aircraft.AssignedRunway);
                    if ((main ? _mainRunwayFreeAt : _crossRunwayFreeAt).CompareTo(now) <= 0
                        && CrossingDue(main, GridBefore(now),
                            now.Advance(RunwayBusySeconds(aircraft, landing: false))) != null)
                    {
                        Consider(GroundTraffic.NextGrid(now));
                        break;
                    }
                }
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

        public const int OutstationCapacity = 8;
        private static readonly string[] OutstationCodes = { "MEL", "SYD", "BNE", "PER" };

        /// <summary>The only cities that can host an outstation base — the one list save, rules and HUD share.</summary>
        public static IReadOnlyList<string> OutstationCandidates => OutstationCodes;

        public static bool IsOutstationCandidate(string code)
        {
            foreach (var candidate in OutstationCodes)
                if (candidate == code)
                    return true;
            return false;
        }

        public long NextOutstationCost => CareerState.OutstationBases.Count == 0 ? 15_000 : 40_000;

        /// <summary>ADR 0139: each outstation is earned, not just bought: (reliability, flights) for the 1st, 2nd, 3rd.</summary>
        public static readonly (int Reliability, int Flights)[] OutstationGates = { (85, 40), (88, 70), (90, 110) };

        /// <summary>The gate for the next outstation, or null once all three are open.</summary>
        public (int Reliability, int Flights)? NextOutstationGate =>
            CareerState.OutstationBases.Count < OutstationGates.Length
                ? OutstationGates[CareerState.OutstationBases.Count]
                : null;

        // What has already been announced, so each tier, goal and the finale is reported once.
        // Seeded silently from the state the first time it is read (a fresh start or a restored
        // save), so loading never replays old achievements. Presentation state only: not saved.
        private OperatingTier? _announcedTier;
        private readonly HashSet<string> _announcedGoals = new(StringComparer.Ordinal);
        private readonly List<CareerEvent> _careerEvents = new();

        // ---- Challenges, milestones, news and the daily report (ADR 0127) --------------------

        private readonly HashSet<string> _announcedMilestones = new(StringComparer.Ordinal);

        /// <summary>The player's flying since the last daily report. Saved from v17 (ADR 0138), so a reload
        /// mid-day keeps the day's flights for the report and the profitable-day challenge.</summary>
        private sealed class DayLedger
        {
            public int Flights;
            public long Revenue;
            public long Cost;
            public string BestCode = string.Empty;
            public long BestMargin = long.MinValue;
            public int? StartReliability;

            public long Margin => Revenue - Cost;

            /// <summary>ADR 0128: late pushbacks today, their minutes, and which cause cost the most.</summary>
            public int LateFlights;
            public int LateSeconds;
            public readonly Dictionary<DelayCause, int> DelayByCause = new();

            public void RecordDelay(DelayBreakdown delay)
            {
                if (!delay.IsLate)
                    return;
                LateFlights++;
                LateSeconds += delay.LatenessSeconds;
                foreach (var part in delay.Parts)
                {
                    DelayByCause.TryGetValue(part.Cause, out var sum);
                    DelayByCause[part.Cause] = sum + part.Seconds;
                }
            }

            /// <summary>The named cause that cost the most today, or null.</summary>
            public DelayCause? WorstCause()
            {
                DelayCause? worst = null;
                var most = 0;
                foreach (DelayCause cause in Enum.GetValues(typeof(DelayCause)))
                {
                    if (cause == DelayCause.Other || !DelayByCause.TryGetValue(cause, out var seconds) || seconds <= most)
                        continue;
                    most = seconds;
                    worst = cause;
                }

                return worst;
            }

            public void Record(Destination destination, long payment, long cost)
            {
                Flights++;
                Revenue += payment;
                Cost += cost;
                var margin = payment - cost;
                if (margin > BestMargin)
                {
                    BestMargin = margin;
                    BestCode = destination.Code;
                }
            }

            public void Reset(int reliability)
            {
                Flights = 0;
                Revenue = 0;
                Cost = 0;
                BestCode = string.Empty;
                BestMargin = long.MinValue;
                StartReliability = reliability;
                LateFlights = 0;
                LateSeconds = 0;
                DelayByCause.Clear();
            }
        }

        private readonly DayLedger _today = new();
        private long? _newsDay;
        private long? _reportedDay;

        internal readonly struct DaySnapshot
        {
            public DaySnapshot(int flights, long revenue, long cost, string bestCode, long bestMargin,
                int? startReliability, int lateFlights, int lateSeconds, long? reportedDay)
            {
                Flights = flights;
                Revenue = revenue;
                Cost = cost;
                BestCode = bestCode;
                BestMargin = bestMargin;
                StartReliability = startReliability;
                LateFlights = lateFlights;
                LateSeconds = lateSeconds;
                ReportedDay = reportedDay;
            }

            public int Flights { get; }
            public long Revenue { get; }
            public long Cost { get; }
            public string BestCode { get; }
            public long BestMargin { get; }
            public int? StartReliability { get; }
            public int LateFlights { get; }
            public int LateSeconds { get; }
            public long? ReportedDay { get; }
        }

        /// <summary>The day so far, for the HUD: flights, revenue, margin.</summary>
        public (int Flights, long Revenue, long Margin) TodaySoFar => (_today.Flights, _today.Revenue, _today.Margin);

        /// <summary>Today's demand event, if there is one.</summary>
        public DemandEvent? TodaysDemandEvent => DemandEvents.At(_processedTo, Clock);

        /// <summary>
        /// ADR 0138: could the airline's eligible aircraft at Adelaide fly every flight of
        /// <paramref name="definition"/> before its deadline, starting now? An aircraft away on a flight
        /// counts from when it should be back.
        /// </summary>
        public bool CanStillFinish(RouteContractDefinition definition)
        {
            if (definition == null || !definition.HasDeadline)
                return true;
            if (!DestinationCatalogue.TryFind(definition.DestinationCode, out var destination))
                return false;
            var count = 0;
            var firstFree = long.MaxValue;
            foreach (var aircraft in _fleet)
            {
                if (!aircraft.Airline.IsPlayer
                    || !definition.MatchesAircraft(aircraft.Type, aircraft.IsFreighter))
                    continue;
                count++;
                firstFree = Math.Min(firstFree, SecondsUntilHome(aircraft));
            }

            return ContractFeasibility.CanStillFinish(definition.EligibleType, destination,
                definition.RequiredRotations, CareerState.BaseLevel, count, firstFree == long.MaxValue ? 0 : firstFree,
                definition.DeadlineSeconds);
        }

        /// <summary>Roughly how long until an aircraft is parked at Adelaide and free to fly again.</summary>
        private long SecondsUntilHome(FleetAircraft aircraft)
        {
            var now = _processedTo.ElapsedSeconds;
            var phaseLeft = aircraft.StateEndsAt.HasValue ? Math.Max(0, aircraft.StateEndsAt.Value.ElapsedSeconds - now) : 0;
            var leg = aircraft.CurrentDestination is { } away ? AirborneSeconds(aircraft, away) : 0;
            return aircraft.State switch
            {
                // A parked aircraft in its check is not free until the check ends.
                FleetState.AtStand => aircraft.CheckUntil is { } checkEnds
                    ? Math.Max(0, checkEnds.ElapsedSeconds - now) : 0,
                FleetState.TaxiOut or FleetState.HoldingShort or FleetState.TakingOff =>
                    phaseLeft + 2 * leg + DestinationTurnaroundSeconds + ContractFeasibility.GroundSeconds,
                FleetState.Outbound => phaseLeft + DestinationTurnaroundSeconds + leg + ContractFeasibility.GroundSeconds / 2,
                FleetState.AtDestination => phaseLeft + leg + ContractFeasibility.GroundSeconds / 2,
                _ => phaseLeft + ContractFeasibility.GroundSeconds / 2
            };
        }

        private static string DestinationName(string code) =>
            DestinationCatalogue.TryFind(code, out var destination) ? destination.Name : code;

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
                && definition.MatchesAircraft(aircraft.Type, aircraft.IsFreighter)
                && definition.MatchesRoute(Home.Code, justFlown.Value.Code))
                matching = definition;

            var settlementId = new SettlementId(aircraft.Registration, aircraft.CompletedTrips);
            var forecast = Forecast(Home, justFlown.Value, aircraft);
            var pay = forecast.Revenue;
            DelayBreakdown? delay = null;
            if (aircraft.Airline.IsPlayer && aircraft.PushbackLatenessSeconds.HasValue)
            {
                delay = aircraft.PushbackDelay
                        ?? DelayBreakdown.Parse(aircraft.PushbackLatenessSeconds.Value, null);
                aircraft.PushbackDelay = null;
                // Delay the weather caused is not the airline's doing: it neither costs reliability nor breaks the streak.
                var controllable = FlightEconomics.ControllableLateness(aircraft.PushbackLatenessSeconds.Value, delay.Value);
                CareerState.ApplyPunctuality(FlightEconomics.PunctualityReliabilityDelta(controllable));
                CareerState.RecordPushback(controllable <= FlightEconomics.OnTimeGraceSeconds);
                aircraft.PushbackLatenessSeconds = null;
            }

            var settlement = CareerState.RecordCompletedRotation(
                settlementId, pay, matching, PlayerOwnedTypes(), now, PlayerFleetCount());
            if (settlement == null)
                return;
            if (delay.HasValue)
                settlement = settlement.Value.WithDelay(delay);

            if (aircraft.Airline.IsPlayer)
            {
                var dispatchCost = forecast.Cost;
                var completionBonus = settlement.Value.ContractFulfilled ? matching.CompletionReward : 0;
                CareerState.RecordService(justFlown.Value.Code,
                    settlement.Value.Payment - completionBonus - dispatchCost,
                    manual: !aircraft.AutomatedTrip);
                _today.Record(justFlown.Value, settlement.Value.Payment, dispatchCost);
                aircraft.RecordHistory(justFlown.Value, settlement.Value.Payment);
                if (AircraftDistinctions.TryReached(aircraft.CompletedTrips, out var distinction))
                    _careerEvents.Add(new CareerEvent(CareerEventKind.AircraftMilestone, CareerState.Tier,
                        $"{aircraft.Registration}: {distinction.Title.ToLowerInvariant()} reached.",
                        aircraft.Registration, aircraft.Type.Id, distinction.Flights, distinction.Title));
                if (delay.HasValue)
                    _today.RecordDelay(delay.Value);
                aircraft.AutomatedTrip = false;
                AdvanceCareer();
            }

            _recentSettlements.Add(settlement.Value);
            TotalSettlements++;
            if (_recentSettlements.Count > MaxRecentEvents)
                _recentSettlements.RemoveAt(0);
        }

        // ---- Time ----------------------------------------------------------------

        public void Update()
        {
            // Seed before this frame can settle anything, so the first real change is announced.
            if (CareerState != null) SeedCareerAnnouncements();
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
            AnnounceTheDay(target);
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
                // ADR 0138: a contract lapses only once everything else due at this instant has
                // happened, so a flight that parks exactly on the deadline still counts.
                if (!changed)
                    changed |= ExpireContract(now);
            } while (changed);
        }

        private bool AdvanceAircraft(FleetAircraft aircraft, SimulationTime now)
        {
            if (aircraft.MaintenanceJob != null) return AdvanceMaintenance(aircraft, now);
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
                            - DeparturePrep.TotalSeconds(aircraft.Type, CareerState.BaseLevel)
                            - (long)DepartureCountdown.PrepEndsBeforeSeconds;
                        aircraft.PrepStartedAt = new SimulationTime(inferred < 0 ? 0 : inferred);
                    }
                    if (!DeparturePrep.IsReady(aircraft, now, CareerState.BaseLevel))
                        return false;
                    if (aircraft.Type.IsRotorcraft)
                        return DepartRotorcraft(aircraft, now);
                    var pushingBackFromGate = AdelaideGround.IsTerminalGate(aircraft.Stand);
                    var readyAt = DepartureReadyAt(aircraft);
                    // Hold at the stand, before releasing any taxi route. Once released the
                    // departure is committed and tower clearance follows normal traffic rules.
                    if (WeatherAt(now) == WeatherKind.Storm)
                        return NoteDelay(aircraft, now, readyAt, DelayCause.Weather);
                    if (NextTaxiReleaseAt(now, pushingBackFromGate).HasValue)
                        return NoteDelay(aircraft, now, readyAt, DelayCause.ApronBusy);
                    // A gate pushback needs its lead-in clear before the tug moves; it is re-checked
                    // whenever anything else finishes, since that is the only way it frees.
                    if (pushingBackFromGate && !IsLeadInFree(aircraft.Stand, aircraft))
                        return NoteDelay(aircraft, now, readyAt, DelayCause.LeadIn);
                    // Ground control: push only when the whole route to the runway is clear of
                    // the traffic already moving. A departure that has to wait for it goes on the
                    // grid, so the moment it moves does not depend on how the clock is stepped.
                    var departureRunway = RunwayFor(aircraft);
                    if (!now.Equals(readyAt) && !GroundTraffic.OnGrid(now))
                        return false;
                    var taxiOutLeg = AdelaideGround.TaxiOut(aircraft.Stand, aircraft.Type, departureRunway);
                    if (!AdelaideGroundPolicy.RouteAvailable(aircraft.Stand, aircraft.Type, departureRunway, outbound: true))
                        return false;
                    if (!GroundTraffic.PathClear(_fleet, aircraft, taxiOutLeg,
                            departureRunway, taxiOut: true, now,
                            includeStationary: true))
                        return NoteDelay(aircraft, now, readyAt, DelayCause.Taxiway);
                    // ADR 0126: not onto a route that crosses a runway while that runway is busy.
                    if (CrossingIntoBusyStrip(taxiOutLeg, departureRunway, now, aircraft.Type).HasValue)
                        return NoteDelay(aircraft, now, readyAt, DelayCause.RunwayCrossing);
                    if (aircraft.Airline.IsPlayer)
                    {
                        var departAt = aircraft.Scheduled.Value.DepartAt.ElapsedSeconds;
                        var lateness = (int)(now.ElapsedSeconds - departAt);
                        aircraft.PushbackLatenessSeconds = lateness;
                        aircraft.PushbackDelay = DelayLedger.Close(aircraft.DelayLedger, departAt,
                            readyAt.ElapsedSeconds, now.ElapsedSeconds);
                    }

                    aircraft.DelayLedger = null;

                    aircraft.CurrentDestination = aircraft.Scheduled.Value.Destination;
                    aircraft.PublishedDepartureAt = aircraft.Scheduled.Value.PublishedAt;
                    aircraft.PushedBackAt = now;
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
                    Transition(aircraft, FleetState.AtDestination, now, aircraft.Type.IsRotorcraft
                        ? RescueTurnaroundSeconds(aircraft)
                        : AwayTurnaroundSeconds(aircraft, now));
                    return true;

                case FleetState.AtDestination:
                    // Out of season Cathay stays in Hong Kong; it is retired while away.
                    if (aircraft.Airline.Id.Value == "CPA" && !IsCathaySeason(now))
                        return false;
                    var inboundSeconds = LegAirborne(aircraft);
                    // A rescue call-out is not a timetabled flight: no random delay or cancellation.
                    if (!aircraft.Airline.IsPlayer && !aircraft.Type.IsRotorcraft)
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
                    // The extended final is visible before HoldingForLanding. Do not postpone
                    // a committed inbound's timer: that used to remove it from the drawn final.
                    if (WeatherAt(now) == WeatherKind.Storm
                        && !ApproachRules.EnteredFinalBeforeStorm(aircraft, now, WeatherAt))
                    {
                        aircraft.ExtendUntil(Weather.NextBlock(now));
                        return false;
                    }
                    // Do not land an aircraft that has nowhere to park. Before this guard a
                    // late arrival could touch down onto a full apron just before curfew and
                    // sit across the runway-exit taxiway until the 05:00 departure wave. Keep
                    // it in flow control and recheck at a useful interval instead.
                    if (aircraft.Type.IsRotorcraft)
                        return ArriveRotorcraft(aircraft, now);
                    // Metered in the circuit until its landing is near, never stacked on short final.
                    if (!(WeatherAt(now) == WeatherKind.Storm
                            && ApproachRules.EnteredFinalBeforeStorm(aircraft, now, WeatherAt))
                        && FinalJoinTime(aircraft, now) is { } joinAt)
                    {
                        aircraft.ExtendUntil(joinAt);
                        return false;
                    }
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
                    if (aircraft.Type.IsRotorcraft)
                        return FinishArrival(aircraft, now);
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
                    if (aircraft.Airline.IsPlayer && string.IsNullOrEmpty(aircraft.Stand.Value)
                        && now.ElapsedSeconds - aircraft.StateStartedAt.ElapsedSeconds < PlayerStandAutoSeconds)
                        return false;
                    // Ground control, as for a pushback: the taxi-in waits at the exit until its
                    // route is clear, moving only on the grid once it has had to wait.
                    if (!now.Equals(aircraft.StateStartedAt) && !GroundTraffic.OnGrid(now))
                        return false;
                    var taxiInLeg = AdelaideGround.TaxiIn(chosen.Value, aircraft.Type, aircraft.AssignedRunway);
                    if (!AdelaideGroundPolicy.RouteAvailable(chosen.Value, aircraft.Type, aircraft.AssignedRunway, outbound: false))
                        return false;
                    if (!GroundTraffic.PathClear(_fleet, aircraft, taxiInLeg,
                            aircraft.AssignedRunway, taxiOut: false, now,
                            includeStationary: true))
                        return false;
                    if (CrossingIntoBusyStrip(taxiInLeg, aircraft.AssignedRunway, now, aircraft.Type).HasValue)
                        return false;
                    aircraft.Stand = chosen.Value;
                    Transition(aircraft, FleetState.TaxiIn, now, TaxiInSecondsTo(chosen.Value, aircraft.Type, aircraft.AssignedRunway));
                    return true;

                case FleetState.TaxiIn:
                    return FinishArrival(aircraft, now);

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

        private int PlayerTurbopropsNeedingABay()
        {
            var count = 0;
            foreach (var aircraft in _fleet)
                if (aircraft.Airline.IsPlayer && !NeedsTerminalGate(aircraft.Type) && !aircraft.Type.IsRotorcraft
                    && !HoldsStand(aircraft))
                    count++;
            return count;
        }

        /// <summary>
        /// A departure holding short this long gets the runway ahead of arrivals that have
        /// waited less, so a steady arrival stream can no longer hold it forever.
        /// </summary>
        public const long DepartureMaxHoldSeconds = 6 * 60;

        /// <summary>
        /// When the tower is expected to clear <paramref name="aircraft"/> off short final,
        /// for an arrival still inbound or holding. Replays the tower's own rule for its strip:
        /// the strip's free time, arrivals ahead in wait order, a departure that has held past
        /// <see cref="DepartureMaxHoldSeconds"/> going first, and weather metering before final.
        /// Presentation flies the arrival in down an extended final to reach the
        /// hold point just as it is cleared, instead of parking it motionless in mid-air there
        /// for the whole wait. Null for anything else. An estimate: traffic that has not yet
        /// reached the queue can still change it.
        /// </summary>
        /// <summary>
        /// ADR 0128: a ready player departure was held by <paramref name="cause"/>. Sampled only at the
        /// moment it became ready and on the ground-control grid — the times every step size visits —
        /// so the breakdown does not depend on how the clock is stepped. Always returns false (the
        /// pushback did not happen), so a gate can return it directly.
        /// </summary>
        private bool NoteDelay(FleetAircraft aircraft, SimulationTime now, SimulationTime readyAt, DelayCause cause)
        {
            if (!aircraft.Airline.IsPlayer || !aircraft.Scheduled.HasValue)
                return false;
            if (!now.Equals(readyAt) && !GroundTraffic.OnGrid(now))
                return false;
            var departAt = aircraft.Scheduled.Value.DepartAt.ElapsedSeconds;
            var ledger = aircraft.DelayLedger;
            if (ledger == null || ledger.DepartAtSeconds != departAt)
                aircraft.DelayLedger = new DelayLedger(departAt, now.ElapsedSeconds, cause);
            else
                ledger.Sample(now.ElapsedSeconds, cause);
            return false;
        }

        /// <summary>Steps allowed by <see cref="ExpectedLandingClearance"/>'s ground check.</summary>
        public const int LandingGroundCheckSteps = 144;

        private static bool Before(FleetAircraft a, SimulationTime aJoined, FleetAircraft b, SimulationTime bJoined)
        {
            var order = aJoined.CompareTo(bJoined);
            return order < 0 || order == 0 && string.CompareOrdinal(a.Registration, b.Registration) < 0;
        }

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

        /// <summary>
        /// These states are mid-trip. A save that omits <see cref="FleetAircraft.CurrentDestination"/>
        /// would collapse every airborne leg to zero seconds on the next tick.
        /// </summary>
        internal static bool RequiresTripDestination(FleetState state) => state is
            FleetState.TaxiOut or FleetState.HoldingShort or FleetState.TakingOff
            or FleetState.Outbound or FleetState.AtDestination or FleetState.Inbound
            or FleetState.HoldingForLanding or FleetState.GoAround or FleetState.Landing;

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

        /// <summary>
        /// Local time (minutes after midnight) Emirates and Qatar land at Adelaide: 20:30, leaving
        /// their ~80 min widebody turn plus a go-around or a wait for a gate before the 22:00 slot.
        /// </summary>
        public const int EveningLongHaulArrivalMinute = 20 * 60 + 30;

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
