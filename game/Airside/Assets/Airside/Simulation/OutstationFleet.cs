using System;
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
