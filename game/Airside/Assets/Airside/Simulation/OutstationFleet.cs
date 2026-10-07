using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// A player aircraft based away from the rendered Adelaide airport. Its service is a
    /// deterministic timed round trip; it never occupies or fabricates an Adelaide stand.
    /// </summary>
    public sealed class OutstationAircraft
    {
        public OutstationAircraft(string registration, AircraftType type, string baseCode,
            string destinationCode = null, long departAtSeconds = 0, long returnAtSeconds = 0,
            int completedServices = 0, bool automated = false,
            int rotationsSinceCheck = 0, long checkUntilSeconds = 0)
        {
            if (string.IsNullOrWhiteSpace(registration)) throw new ArgumentException("Registration required.", nameof(registration));
            Registration = registration;
            Type = type ?? throw new ArgumentNullException(nameof(type));
            BaseCode = baseCode ?? throw new ArgumentNullException(nameof(baseCode));
            DestinationCode = destinationCode ?? string.Empty;
            DepartAtSeconds = departAtSeconds;
            ReturnAtSeconds = returnAtSeconds;
            CompletedServices = Math.Max(0, completedServices);
            Automated = automated;
            RotationsSinceCheck = Math.Max(0, rotationsSinceCheck);
            CheckUntilSeconds = Math.Max(0, checkUntilSeconds);
        }

        public string Registration { get; }
        public AircraftType Type { get; }
        public string BaseCode { get; }
        public string DestinationCode { get; private set; }
        public long DepartAtSeconds { get; private set; }
        public long ReturnAtSeconds { get; private set; }
        public int CompletedServices { get; private set; }
        public bool HasFlight => DestinationCode.Length > 0;
        public bool Automated { get; private set; }
        public int RotationsSinceCheck { get; private set; }
        public long CheckUntilSeconds { get; private set; }
        public bool CheckDue => RotationsSinceCheck >= Maintenance.IntervalRotations;
        public bool InCheck(long nowSeconds) => CheckUntilSeconds > nowSeconds;

        /// <summary>When this airframe joined the airline; 0 for aircraft bought before the logbook (save v20).</summary>
        public long JoinedAtSeconds { get; internal set; }

        /// <summary>Revenue credited to this airframe since its logbook began (save v20).</summary>
        public long LifetimeRevenue { get; private set; }

        /// <summary>Services with route and revenue entries in the logbook; never above <see cref="CompletedServices"/>.</summary>
        public int HistoryFlights { get; private set; }

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

        internal void RecordHistory(string destinationCode, long revenue)
        {
            HistoryFlights++;
            LifetimeRevenue += Math.Max(0, revenue);
            _routeFlights.TryGetValue(destinationCode, out var flights);
            _routeFlights[destinationCode] = flights + 1;
        }

        internal void RestoreHistory(long joinedAtSeconds, long lifetimeRevenue, int historyFlights,
            IEnumerable<AircraftRouteTally> routes)
        {
            JoinedAtSeconds = Math.Max(0, joinedAtSeconds);
            LifetimeRevenue = Math.Max(0, lifetimeRevenue);
            HistoryFlights = Math.Max(0, historyFlights);
            _routeFlights.Clear();
            if (routes == null)
                return;
            foreach (var route in routes)
                if (!string.IsNullOrWhiteSpace(route.DestinationCode) && route.Flights > 0)
                    _routeFlights[route.DestinationCode] = route.Flights;
        }

        internal void StartCheck(long untilSeconds)
        {
            RotationsSinceCheck = 0;
            CheckUntilSeconds = untilSeconds;
        }

        internal void Plan(string destinationCode, long departAtSeconds, long returnAtSeconds, bool automated)
        {
            if (HasFlight) throw new InvalidOperationException("Aircraft already has a service.");
            DestinationCode = destinationCode;
            DepartAtSeconds = departAtSeconds;
            ReturnAtSeconds = returnAtSeconds;
            Automated = automated;
        }

        internal void Cancel()
        {
            DestinationCode = string.Empty;
            DepartAtSeconds = ReturnAtSeconds = 0;
            Automated = false;
        }

        internal void Complete()
        {
            if (!HasFlight) throw new InvalidOperationException("No service to complete.");
            CompletedServices++;
            RotationsSinceCheck++;
            DestinationCode = string.Empty;
            DepartAtSeconds = 0;
            ReturnAtSeconds = 0;
            Automated = false;
        }
    }

    /// <summary>A repeat plan is issued only during an active play session.</summary>
    public sealed class RepeatSchedule
    {
        public RepeatSchedule(string registration, string destinationCode, int intervalHours,
            long nextEligibleAtSeconds = 0, bool paused = false, string exception = null)
        {
            Registration = registration ?? string.Empty;
            DestinationCode = destinationCode ?? string.Empty;
            IntervalHours = intervalHours;
            NextEligibleAtSeconds = Math.Max(0, nextEligibleAtSeconds);
            Paused = paused;
            Exception = exception ?? string.Empty;
        }

        public string Registration { get; }
        public string DestinationCode { get; }
        public int IntervalHours { get; }
        public long NextEligibleAtSeconds { get; internal set; }
        public bool Paused { get; internal set; }
        public string Exception { get; internal set; }
    }
}
