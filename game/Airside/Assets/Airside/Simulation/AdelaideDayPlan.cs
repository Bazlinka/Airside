using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// One published Adelaide movement for the local operating day. Live fleet
    /// aircraft overlay these rows when they are already flying the same slot.
    /// </summary>
    public readonly struct PlannedMovement
    {
        public PlannedMovement(string flightNumber, string airlineId, string airlineName, string liveryHex,
            string registration, AircraftType type, string origin, string destination,
            SimulationTime scheduledAt, bool arrival, string standLabel,
            FlightDisruption disruption = default)
        {
            FlightNumber = flightNumber ?? string.Empty;
            AirlineId = airlineId ?? string.Empty;
            AirlineName = airlineName ?? string.Empty;
            LiveryHex = liveryHex ?? string.Empty;
            Registration = registration ?? string.Empty;
            Type = type;
            Origin = origin ?? string.Empty;
            Destination = destination ?? string.Empty;
            ScheduledAt = scheduledAt;
            Arrival = arrival;
            StandLabel = standLabel ?? "—";
            Disruption = disruption.Cancelled || disruption.Delayed ? disruption : FlightDisruption.None;
        }

        public string FlightNumber { get; }
        public string AirlineId { get; }
        public string AirlineName { get; }
        public string LiveryHex { get; }
        public string Registration { get; }
        public AircraftType Type { get; }
        public string Origin { get; }
        public string Destination { get; }
        public SimulationTime ScheduledAt { get; }
        public bool Arrival { get; }
        public string StandLabel { get; }
        public FlightDisruption Disruption { get; }

        public SimulationTime EstimatedAt =>
            Disruption.Delayed ? ScheduledAt.Advance(Disruption.DelayMinutes * 60L) : ScheduledAt;

        public string RouteText => $"{Origin} → {Destination}";
    }

    /// <summary>
    /// A representative Adelaide day: every official AI operator's network, both
    /// arrivals and departures, 05:00–23:00, each turn on a stand that fits the type and
    /// is not already taken. The sky draws only the live fleet's legs (ADR 0110).
    /// </summary>
    public static class AdelaideDayPlan
    {
        public static IReadOnlyList<PlannedMovement> ForLocalDay(AirlineOperations operations, SimulationTime now)
        {
            if (operations == null)
                return Array.Empty<PlannedMovement>();

            var clock = operations.Clock ?? AirlineClock.Default;
            var day = clock.LocalAt(now).Date;
            var list = new List<PlannedMovement>();
            // Minute each stand is next free, so two overlapping turns never share a gate.
            var standFreeAt = new Dictionary<StableId, int>();

            foreach (var airline in operations.Airlines)
            {
                if (airline.IsPlayer)
                    continue;
                var type = TypeFor(airline);
                if (type == null)
                    continue;

                var tripCount = 0;
                foreach (var (code, weight) in AirlineOperations.AiNetworkFor(airline))
                {
                    if (!DestinationCatalogue.TryFind(code, out _))
                        continue;
                    tripCount += Math.Max(2, weight + 1);
                }

                var banks = AdelaideHourProfile.BankMinutes(tripCount,
                    AirlineOperations.AiFirstDepartureHour, AirlineOperations.AiLastDepartureHour);
                var slot = 0;
                foreach (var (code, weight) in AirlineOperations.AiNetworkFor(airline))
                {
                    if (!DestinationCatalogue.TryFind(code, out _))
                        continue;
                    var trips = Math.Max(2, weight + 1);
                    for (var trip = 0; trip < trips; trip++)
                    {
                        if (slot >= banks.Length)
                            break;
                        // Gate dwell matches the live AI turnaround bands (ADR 0071):
                        // turboprop ~35, narrowbody ~50, widebody ~75 — not a flat 50 for all.
                        var dwell = TurnaroundMinutes(type);
                        var arriveMinutes = Math.Min(banks[slot], AirportCurfew.LastMovementMinute - dwell);
                        var departMinutes = Math.Min(arriveMinutes + dwell, AirportCurfew.LastMovementMinute);
                        var number = 210 + slot;
                        var stand = StandFor(type, slot, arriveMinutes, departMinutes, standFreeAt);
                        var arriveAt = clock.AtLocal(day.AddMinutes(arriveMinutes));
                        var departAt = clock.AtLocal(day.AddMinutes(departMinutes));
                        var arriveNumber = $"{airline.Id.Value}{number}";
                        var departNumber = $"{airline.Id.Value}{number + 1}";
                        list.Add(new PlannedMovement(
                            arriveNumber, airline.Id.Value, airline.Name, airline.LiveryHex,
                            string.Empty, type, code, "ADL",
                            arriveAt, arrival: true, stand,
                            FlightDisruption.For(arriveNumber, arriveAt, clock)));
                        list.Add(new PlannedMovement(
                            departNumber, airline.Id.Value, airline.Name, airline.LiveryHex,
                            string.Empty, type, "ADL", code,
                            departAt, arrival: false, stand,
                            FlightDisruption.For(departNumber, departAt, clock)));
                        slot++;
                    }
                }
            }

            return list;
        }

        /// <summary>
        /// Adelaide flights in the air right now, drawn from the live fleet's own off-map
        /// legs (ADR 0110): an aircraft that left one of our gates climbing away, or one
        /// flying home to take a stand. Timetable-only "phantom" flights are no longer
        /// drawn, so nothing departs Adelaide without first having been parked at a gate.
        /// Overflights stay in <see cref="SkyTraffic"/>.
        /// </summary>
        public static IReadOnlyList<SkyFlight> AirborneAt(AirlineOperations operations, SimulationTime now)
        {
            if (operations == null)
                return Array.Empty<SkyFlight>();

            var flights = new List<SkyFlight>();
            FillAirborneAt(operations, now, flights);
            return flights;
        }

        /// <summary>Refill a caller-owned buffer without allocating a new flight list each frame.</summary>
        public static void FillAirborneAt(AirlineOperations operations, SimulationTime now, List<SkyFlight> flights)
        {
            if (flights == null)
                throw new ArgumentNullException(nameof(flights));
            flights.Clear();
            if (operations == null)
                return;

            var home = operations.Home;
            foreach (var aircraft in operations.Fleet)
            {
                if (aircraft?.Type == null || !aircraft.CurrentDestination.HasValue)
                    continue;
                var away = aircraft.CurrentDestination.Value;
                var duration = LegTiming.AirborneSeconds(home.DistanceKmTo(away), aircraft.Type);
                double start;
                Destination from;
                Destination to;
                if (aircraft.State == FleetState.Outbound)
                {
                    // The fleet draws the climb-out itself for DepartedSeconds; hand over after.
                    var departed = AircraftPerformance.For(aircraft.Type).DepartedSeconds;
                    if (now.ElapsedSeconds - aircraft.StateStartedAt.ElapsedSeconds < departed)
                        continue;
                    start = aircraft.StateStartedAt.ElapsedSeconds;
                    from = home;
                    to = away;
                }
                else if (aircraft.State == FleetState.Inbound && aircraft.StateEndsAt.HasValue)
                {
                    // Inbound may include a ground delay at the outstation: fly the last
                    // leg-length of it, arriving as the aircraft joins the circuit.
                    start = aircraft.StateEndsAt.Value.ElapsedSeconds - duration;
                    from = away;
                    to = home;
                }
                else
                {
                    continue;
                }

                var callsign = aircraft.Airline.Id.Value + aircraft.Registration;
                if (SkyTraffic.TryEnroute(callsign, aircraft.Type, from, to, now.ElapsedSeconds, start,
                        out var flight))
                    flights.Add(flight);
            }
        }

        /// <summary>
        /// True when a live aircraft is already the movement this planned slot describes,
        /// so the board shows the live row instead of a duplicate planned one.
        /// Arrival slots only match inbound/landing states; departure slots only match
        /// outbound/scheduled ones — otherwise an outbound Rex to MEL would suppress the
        /// planned arrival from MEL.
        /// </summary>
        public static bool CoveredBy(PlannedMovement planned, FleetAircraft aircraft)
        {
            return MatchDeltaSeconds(planned, aircraft) >= 0;
        }

        /// <summary>
        /// Nearest live aircraft that covers <paramref name="planned"/>, skipping registrations
        /// already claimed so one inbound cannot suppress every same-route day-plan slot.
        /// </summary>
        public static FleetAircraft CoveringAircraft(PlannedMovement planned,
            IEnumerable<FleetAircraft> fleet, HashSet<string> claimed = null)
        {
            if (fleet == null)
                return null;
            FleetAircraft best = null;
            var bestDelta = long.MaxValue;
            foreach (var aircraft in fleet)
            {
                if (aircraft == null)
                    continue;
                if (claimed != null && claimed.Contains(aircraft.Registration))
                    continue;
                var delta = MatchDeltaSeconds(planned, aircraft);
                if (delta < 0 || delta >= bestDelta)
                    continue;
                best = aircraft;
                bestDelta = delta;
            }

            if (best != null)
                claimed?.Add(best.Registration);
            return best;
        }

        /// <summary>
        /// Absolute seconds between live and planned times when the aircraft covers the
        /// slot; otherwise -1.
        /// </summary>
        public static long MatchDeltaSeconds(PlannedMovement planned, FleetAircraft aircraft)
        {
            if (aircraft == null || aircraft.Airline.Id.Value != planned.AirlineId)
                return -1;
            if (planned.Registration.Length > 0
                && string.Equals(aircraft.Registration, planned.Registration, StringComparison.OrdinalIgnoreCase))
                return MatchesHalf(planned, aircraft) ? 0 : -1;

            if (!MatchesHalf(planned, aircraft))
                return -1;

            var dest = aircraft.CurrentDestination ?? aircraft.Scheduled?.Destination;
            if (!dest.HasValue)
                return -1;
            var code = dest.Value.Code;
            var plannedAway = planned.Arrival ? planned.Origin : planned.Destination;
            if (code != plannedAway)
                return -1;

            var liveSeconds = planned.Arrival
                ? ArrivalMatchSeconds(planned, aircraft)
                : aircraft.Scheduled?.DepartAt.ElapsedSeconds ?? aircraft.StateStartedAt.ElapsedSeconds;
            var delta = Math.Abs(liveSeconds - planned.ScheduledAt.ElapsedSeconds);
            return delta < 25 * 60 ? delta : -1;
        }

        /// <summary>
        /// Holders, go-arounds and stand waits have no useful StateEndsAt for ETA matching.
        /// Prefer the inbound ETA when present; otherwise keep covering via the planned slot
        /// so a long final does not resurrect a ghost day-plan row in the sky.
        /// </summary>
        private static long ArrivalMatchSeconds(PlannedMovement planned, FleetAircraft aircraft)
        {
            if (aircraft.StateEndsAt.HasValue
                && aircraft.State is FleetState.Inbound or FleetState.Landing)
                return aircraft.StateEndsAt.Value.ElapsedSeconds;

            if (aircraft.State == FleetState.HoldingForLanding)
            {
                var remaining = ApproachHold.RemainingFinalSeconds(
                    AircraftPerformance.For(aircraft.Type).ApproachSeconds, aircraft.Registration);
                return aircraft.StateStartedAt.ElapsedSeconds + remaining;
            }

            if (aircraft.State is FleetState.GoAround or FleetState.AwaitingStand or FleetState.TaxiIn
                or FleetState.Landing)
                return planned.EstimatedAt.ElapsedSeconds;

            return aircraft.StateStartedAt.ElapsedSeconds;
        }

        private static bool MatchesHalf(PlannedMovement planned, FleetAircraft aircraft)
        {
            var liveArrival = aircraft.State is FleetState.Inbound
                or FleetState.HoldingForLanding or FleetState.GoAround or FleetState.Landing
                or FleetState.AwaitingStand or FleetState.TaxiIn;
            return planned.Arrival == liveArrival;
        }

        public static AircraftType TypeFor(Airline airline) => airline?.Id.Value switch
        {
            "REX" => AircraftType.Saab340,
            "QLK" => AircraftType.Dash8Q400,
            "VOZ" => AircraftType.Boeing7378,
            "QFA" => AircraftType.Boeing737800,
            "JST" => AircraftType.AirbusA320200,
            "ANZ" => AircraftType.AirbusA321Neo,
            "FJI" => AircraftType.Boeing7378,
            "SIA" => AircraftType.Boeing78710,
            "UAE" => AircraftType.AirbusA350900,
            "CPA" => AircraftType.AirbusA350900,
            "MAS" => AircraftType.AirbusA330900,
            "QTR" => AircraftType.AirbusA350900,
            _ => null
        };

        private static int TurnaroundMinutes(AircraftType type)
        {
            if (type == null)
                return 50;
            if (AircraftCatalogue.IsWidebody(type))
                return 75;
            if (AirlineOperations.NeedsTerminalGate(type))
                return 50;
            return 35;
        }

        /// <summary>
        /// A stand that fits the type (class and ICAO code letter) and is free for the whole
        /// turn, preferring one that is not bigger than needed. Falls back to the old
        /// round-robin only when every fitting stand is busy.
        /// </summary>
        private static string StandFor(AircraftType type, int slot, int arriveMinutes, int departMinutes,
            Dictionary<StableId, int> standFreeAt)
        {
            var pool = AirlineOperations.NeedsTerminalGate(type)
                ? AirlineOperations.AdelaideTerminalGates
                : AirlineOperations.AdelaideRegionalBays;
            var fitted = FittingStands(pool, type);
            StableId? pick = null;
            for (var pass = 0; pass < 2 && pick == null; pass++)
            {
                for (var i = 0; i < fitted.Count; i++)
                {
                    var stand = fitted[(slot + i) % fitted.Count];
                    if (pass == 0 && AirlineOperations.IsOversized(type, stand))
                        continue;
                    if (standFreeAt.TryGetValue(stand, out var freeAt) && freeAt > arriveMinutes)
                        continue;
                    pick = stand;
                    break;
                }
            }

            var chosen = pick ?? fitted[slot % fitted.Count];
            standFreeAt[chosen] = departMinutes;
            return AdelaideGround.StandLabel(chosen);
        }

        private static IReadOnlyList<StableId> FittingStands(IReadOnlyList<StableId> stands, AircraftType type)
        {
            var fitted = new List<StableId>();
            foreach (var stand in stands)
                if (AirlineOperations.StandFits(type, stand))
                    fitted.Add(stand);
            return fitted.Count > 0 ? fitted : stands;
        }
    }
}
