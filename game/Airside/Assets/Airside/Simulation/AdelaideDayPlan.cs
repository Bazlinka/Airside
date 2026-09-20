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
    /// arrivals and departures, from the first pushback hour to the last. Planned
    /// movements that are currently airborne are drawn as sky traffic so the
    /// whole day is visible without parking extra aircraft on the stands.
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
                        var arriveMinutes = Math.Min(banks[slot],
                            AirlineOperations.AiLastDepartureHour * 60 - 55);
                        var departMinutes = Math.Min(arriveMinutes + 50,
                            AirlineOperations.AiLastDepartureHour * 60 - 5);
                        var number = 210 + slot;
                        var stand = StandFor(type, slot);
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
        /// Planned Adelaide movements that are in the air right now and are not
        /// already a live fleet aircraft. Overflights stay in <see cref="SkyTraffic"/>.
        /// </summary>
        public static IReadOnlyList<SkyFlight> AirborneAt(AirlineOperations operations, SimulationTime now)
        {
            if (operations == null)
                return Array.Empty<SkyFlight>();

            var home = operations.Home;
            var list = new List<SkyFlight>();
            var claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var planned in ForLocalDay(operations, now))
            {
                if (planned.Type == null)
                    continue;
                var awayCode = planned.Arrival ? planned.Origin : planned.Destination;
                if (!DestinationCatalogue.TryFind(awayCode, out var away))
                    continue;

                if (CoveringAircraft(planned, operations.Fleet, claimed) != null
                    || planned.Disruption.Cancelled)
                    continue;

                var from = planned.Arrival ? away : home;
                var to = planned.Arrival ? home : away;
                if (SkyTraffic.TryEnroute(planned.FlightNumber, planned.Type, from, to,
                        now.ElapsedSeconds, StartSeconds(planned, from, to), out var flight))
                    list.Add(flight);
            }

            return list;
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
            var liveArrival = aircraft.State is FleetState.AtDestination or FleetState.Inbound
                or FleetState.HoldingForLanding or FleetState.GoAround or FleetState.Landing
                or FleetState.AwaitingStand or FleetState.TaxiIn;
            return planned.Arrival == liveArrival;
        }

        public static AircraftType TypeFor(Airline airline) => airline?.Id.Value switch
        {
            "REX" => AircraftType.Saab340,
            "QLK" => AircraftType.Dash8Q400,
            "VOZ" => AircraftType.Boeing7378,
            "ANZ" => AircraftType.AirbusA321Neo,
            "SIA" => AircraftType.Boeing78710,
            "CPA" => AircraftType.AirbusA350900,
            _ => null
        };

        private static double StartSeconds(PlannedMovement planned, Destination from, Destination to)
        {
            var duration = LegTiming.AirborneSeconds(from.DistanceKmTo(to), planned.Type);
            return planned.Arrival
                ? planned.EstimatedAt.ElapsedSeconds - duration
                : planned.EstimatedAt.ElapsedSeconds;
        }

        private static string StandFor(AircraftType type, int slot)
        {
            if (AirlineOperations.NeedsTerminalGate(type))
            {
                var gates = FittingStands(AirlineOperations.AdelaideTerminalGates, type);
                return AdelaideGround.StandLabel(gates[slot % gates.Count]);
            }

            var bays = FittingStands(AirlineOperations.AdelaideRegionalBays, type);
            return AdelaideGround.StandLabel(bays[slot % bays.Count]);
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
