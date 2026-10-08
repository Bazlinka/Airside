using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// Where an airline aircraft is in its round trip from the home airport (ADR 0045).
    /// Order is the order a trip passes through them.
    /// </summary>
    public enum FleetState
    {
        /// <summary>Parked on a stand at home. May have a departure scheduled.</summary>
        AtStand,
        /// <summary>Pushed back and taxiing to the runway.</summary>
        TaxiOut,
        /// <summary>At the holding point, waiting for the tower to release the runway.</summary>
        HoldingShort,
        /// <summary>On the runway for the takeoff roll.</summary>
        TakingOff,
        /// <summary>Off the map, flying to the destination.</summary>
        Outbound,
        /// <summary>On the ground at the destination (turnaround).</summary>
        AtDestination,
        /// <summary>Off the map, flying home.</summary>
        Inbound,
        /// <summary>Back in the home circuit, holding for a landing slot.</summary>
        HoldingForLanding,
        /// <summary>Climbing away after an unstable approach, before rejoining the arrival sequence.</summary>
        GoAround,
        /// <summary>On the runway for the approach and landing roll.</summary>
        Landing,
        /// <summary>Vacated the runway; waiting for a stand to be chosen.</summary>
        AwaitingStand,
        /// <summary>Taxiing to the assigned stand.</summary>
        TaxiIn,
        /// <summary>A ground maintenance job, independent of commercial trips.</summary>
        Maintenance
    }

    /// <summary>A destination and the number of completed return flights an aircraft has made there.</summary>
    public readonly struct AircraftRouteTally
    {
        public AircraftRouteTally(string destinationCode, int flights)
        {
            DestinationCode = destinationCode ?? string.Empty;
            Flights = Math.Max(0, flights);
        }

        public string DestinationCode { get; }
        public int Flights { get; }
    }

    /// <summary>Named distinctions that turn an aircraft's flight count into a small personal story.</summary>
    public readonly struct AircraftDistinction
    {
        public AircraftDistinction(int flights, string title)
        {
            Flights = flights;
            Title = title ?? string.Empty;
        }

        public int Flights { get; }
        public string Title { get; }
    }

    public static class AircraftDistinctions
    {
        public static readonly IReadOnlyList<AircraftDistinction> All = new[]
        {
            new AircraftDistinction(1, "First flight"),
            new AircraftDistinction(10, "Familiar face"),
            new AircraftDistinction(25, "Route regular"),
            new AircraftDistinction(50, "Workhorse"),
            new AircraftDistinction(100, "Veteran"),
            new AircraftDistinction(250, "Airline icon")
        };

        public static bool TryReached(int completedFlights, out AircraftDistinction distinction)
        {
            foreach (var candidate in All)
                if (candidate.Flights == completedFlights)
                {
                    distinction = candidate;
                    return true;
                }
            distinction = default;
            return false;
        }

        public static AircraftDistinction Current(int completedFlights)
        {
            var current = default(AircraftDistinction);
            foreach (var candidate in All)
            {
                if (candidate.Flights > completedFlights)
                    break;
                current = candidate;
            }
            return current;
        }

        public static AircraftDistinction Next(int completedFlights)
        {
            foreach (var candidate in All)
                if (candidate.Flights > completedFlights)
                    return candidate;
            return default;
        }
    }

    /// <summary>A departure the owner has asked for but that has not started yet.</summary>
    public readonly struct ScheduledDeparture
    {
        /// <param name="departAt">When the aircraft will actually push back.</param>
        /// <param name="delayMinutes">Legacy form: minutes <paramref name="departAt"/> is behind the
        /// published time. Ignored when <paramref name="publishedAt"/> is given.</param>
        /// <param name="publishedAt">The time printed on the board (ADR 0137). Defaults to
        /// <paramref name="departAt"/> less any delay.</param>
        public ScheduledDeparture(Destination destination, SimulationTime departAt,
            int delayMinutes = 0, bool cancelled = false, SimulationTime? publishedAt = null)
        {
            Destination = destination;
            DepartAt = departAt;
            Cancelled = cancelled;
            if (cancelled)
                PublishedAt = publishedAt ?? departAt;
            else if (publishedAt.HasValue)
                PublishedAt = publishedAt.Value.CompareTo(departAt) < 0 ? publishedAt.Value : departAt;
            else
                PublishedAt = new SimulationTime(Math.Max(0,
                    departAt.ElapsedSeconds - (delayMinutes < 0 ? 0 : delayMinutes) * 60L));
        }

        public Destination Destination { get; }

        /// <summary>When the aircraft will push back (the published time plus any delay).</summary>
        public SimulationTime DepartAt { get; }

        /// <summary>The scheduled time on the board, which never moves when a delay is added.</summary>
        public SimulationTime PublishedAt { get; }

        /// <summary>Whole minutes behind the published time, worked out from the two times.</summary>
        public int DelayMinutes => Cancelled ? 0 : (int)((DepartAt.ElapsedSeconds - PublishedAt.ElapsedSeconds) / 60);

        public bool Cancelled { get; }
    }

    /// <summary>
    /// One aircraft owned by an airline. State is changed only by
    /// <see cref="AirlineOperations"/>; everything here is read-only to callers.
    /// </summary>
    public sealed class FleetAircraft
    {
        internal FleetAircraft(string registration, Airline airline, AircraftType type, StableId stand, SimulationTime now)
        {
            if (string.IsNullOrWhiteSpace(registration))
                throw new ArgumentException("A registration is required.", nameof(registration));

            Registration = registration;
            Airline = airline ?? throw new ArgumentNullException(nameof(airline));
            // Dedicated operators are cargo on both creation and restore; player role remains saved/refittable.
            IsFreighter = airline.IsFreightCarrier;
            Type = type ?? throw new ArgumentNullException(nameof(type));
            Stand = stand;
            State = FleetState.AtStand;
            StateStartedAt = now;
            StateEndsAt = null;
            JoinedAirlineAt = now;
        }

        public string Registration { get; }
        public Airline Airline { get; }
        public AircraftType Type { get; }
        public StableId Id => new(Registration);

        /// <summary>The operations that own this aircraft; set when it joins the fleet.</summary>
        internal AirlineOperations Owner { get; set; }

        /// <summary>
        /// The player's base level, which sets how long departure prep takes (ADR 0137). Every
        /// status line reads it from here so the board, the card and the simulation agree.
        /// </summary>
        public PlayerBaseLevel BaseLevel => Owner?.CareerState?.BaseLevel ?? PlayerBaseLevel.Starter;

        public FleetState State { get; private set; }
        public SimulationTime StateStartedAt { get; private set; }

        /// <summary>
        /// When the current state ends on its own. Null for states that wait on something
        /// else: a schedule, the tower, or the player's stand choice.
        /// </summary>
        public SimulationTime? StateEndsAt { get; private set; }

        /// <summary>The stand held or being taxied to. Default while away from one.</summary>
        public StableId Stand { get; internal set; }

        /// <summary>The trip in progress, from takeoff until back on a stand.</summary>
        public Destination? CurrentDestination { get; internal set; }

        public ScheduledDeparture? Scheduled { get; internal set; }

        /// <summary>
        /// The published time of the flight now under way, kept after pushback so the departures
        /// board's TIME never moves once the aircraft leaves (ADR 0137). Not saved: after a reload
        /// mid-taxi the board falls back to the phase start.
        /// </summary>
        public SimulationTime? PublishedDepartureAt { get; internal set; }

        /// <summary>When the flight now under way actually pushed back. Not saved.</summary>
        public SimulationTime? PushedBackAt { get; internal set; }

        /// <summary>The stand the aircraft last pushed back from, so its taxi-out can be drawn from there.</summary>
        public StableId DepartureStand { get; internal set; }

        public int CompletedTrips { get; internal set; }

        /// <summary>When this individual airframe joined its operator. Persisted from save v18.</summary>
        public SimulationTime JoinedAirlineAt { get; internal set; }

        /// <summary>
        /// Converted to a package freighter (ADR 0194): flies with no passengers, is paid from freight demand
        /// and wears a cargo livery. Player aircraft only; persisted from save v19.
        /// </summary>
        public bool IsFreighter { get; internal set; }

        /// <summary>The original player aircraft, kept distinct from later Saabs with the same type.</summary>
        public bool IsFoundingAircraft { get; internal set; }

        /// <summary>
        /// Being moved between bases (ADR 0239): it flies in like any arrival but earns nothing and counts no flight
        /// when it parks. Persisted from save v20, because a long ferry is likely to be saved mid-air.
        /// </summary>
        public bool IsFerry { get; internal set; }

        /// <summary>Revenue credited to this airframe since its logbook began (save v18).</summary>
        public long LifetimeRevenue { get; internal set; }

        /// <summary>Flights with route and revenue entries in the v18 logbook.</summary>
        public int HistoryFlights { get; internal set; }

        private readonly Dictionary<string, int> _routeFlights = new(StringComparer.Ordinal);

        public IEnumerable<AircraftRouteTally> RouteHistory
        {
            get
            {
                foreach (var pair in _routeFlights)
                    yield return new AircraftRouteTally(pair.Key, pair.Value);
            }
        }

        public AircraftRouteTally FavouriteRoute
        {
            get
            {
                var bestCode = string.Empty;
                var bestFlights = 0;
                foreach (var pair in _routeFlights)
                    if (pair.Value > bestFlights || pair.Value == bestFlights
                        && string.CompareOrdinal(pair.Key, bestCode) < 0)
                    {
                        bestCode = pair.Key;
                        bestFlights = pair.Value;
                    }
                return new AircraftRouteTally(bestCode, bestFlights);
            }
        }

        internal void RecordHistory(Destination destination, long revenue)
        {
            HistoryFlights++;
            LifetimeRevenue += Math.Max(0, revenue);
            _routeFlights.TryGetValue(destination.Code, out var flights);
            _routeFlights[destination.Code] = flights + 1;
        }

        internal void RestoreHistory(SimulationTime joinedAt, bool founding, long lifetimeRevenue,
            int historyFlights, IEnumerable<AircraftRouteTally> routes)
        {
            JoinedAirlineAt = joinedAt;
            IsFoundingAircraft = founding;
            LifetimeRevenue = Math.Max(0, lifetimeRevenue);
            HistoryFlights = Math.Max(0, historyFlights);
            _routeFlights.Clear();
            if (routes == null)
                return;
            foreach (var route in routes)
                if (!string.IsNullOrWhiteSpace(route.DestinationCode) && route.Flights > 0)
                    _routeFlights[route.DestinationCode] = route.Flights;
        }

        /// <summary>Current trip was booked by an earned repeat schedule.</summary>
        public bool AutomatedTrip { get; internal set; }

        /// <summary>Rotations flown since the last routine check (ADR 0085).</summary>
        public int RotationsSinceCheck { get; internal set; }

        /// <summary>When the check under way finishes; null when none is.</summary>
        public SimulationTime? CheckUntil { get; internal set; }

        public MaintenanceJob MaintenanceJob { get; internal set; }

        /// <summary>Runway fixed when the movement enters the airport sequence.</summary>
        public RunwayDirection AssignedRunway { get; internal set; } = RunwayDirection.Runway05;

        /// <summary>Prevents repeated go-arounds on the same round trip.</summary>
        public bool WentAroundThisTrip { get; internal set; }
        public bool ArrivalCommittedBeforeStorm { get; internal set; }

        /// <summary>When player departure prep (fuel → catering → boarding) started. Null if none.</summary>
        public SimulationTime? PrepStartedAt { get; internal set; }

        /// <summary>
        /// Seconds late (or early as negative) when this trip pushed back vs the booked
        /// <see cref="ScheduledDeparture.DepartAt"/>. Null until pushback, or for AI / pre-v10
        /// restores that never recorded it (ADR 0078).
        /// </summary>
        public int? PushbackLatenessSeconds { get; internal set; }

        /// <summary>What made that pushback late (ADR 0128), held with the lateness until the rotation settles.</summary>
        public DelayBreakdown? PushbackDelay { get; internal set; }

        /// <summary>The current wait at the stand, sampled on the ground-control grid. Not saved.</summary>
        internal DelayLedger DelayLedger { get; set; }

        public bool IsOffMap => State is FleetState.Outbound or FleetState.AtDestination or FleetState.Inbound;

        /// <summary>0..1 through a timed state; 0 for waiting states.</summary>
        public double StateProgress(SimulationTime now)
        {
            if (StateEndsAt == null)
                return 0;
            var total = StateEndsAt.Value.ElapsedSeconds - StateStartedAt.ElapsedSeconds;
            if (total <= 0)
                return 1;
            var done = now.ElapsedSeconds - StateStartedAt.ElapsedSeconds;
            return Math.Clamp(done / (double)total, 0, 1);
        }

        /// <summary>Put a restored aircraft back exactly where a save left it.</summary>
        internal void Restore(FleetState state, SimulationTime startedAt, SimulationTime? endsAt)
        {
            State = state;
            StateStartedAt = startedAt;
            StateEndsAt = endsAt;
        }

        /// <summary>Stretch a timed state (e.g. inbound waiting out the curfew) without restarting it.</summary>
        internal void ExtendUntil(SimulationTime endsAt) => StateEndsAt = endsAt;

        internal void Enter(FleetState state, SimulationTime now, long? durationSeconds)
        {
            if (state is FleetState.Inbound or FleetState.AtStand) ArrivalCommittedBeforeStorm = false;
            State = state;
            StateStartedAt = now;
            StateEndsAt = durationSeconds.HasValue ? now.Advance(Math.Max(0, durationSeconds.Value)) : null;
        }

        public override string ToString() => $"{Registration} ({Airline.Name}) {State}";
    }
}
