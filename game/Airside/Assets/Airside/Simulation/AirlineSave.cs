using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// Everything needed to resume an airline game (ADR 0045). Plain public fields and
    /// no nullables so Unity's JsonUtility can write it; <see cref="AirlineSave"/> is
    /// the only thing that reads or writes the simulation from it.
    /// </summary>
    [Serializable]
    public sealed class AirlineSaveData
    {
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;
        public string HomeCode;
        public long ClockSeconds;
        public long RunwayFreeAtSeconds;
        public long TotalEvents;
        public uint RandomState;
        public List<AirlineRecord> Airlines = new();
        public List<AircraftRecord> Fleet = new();
    }

    [Serializable]
    public sealed class AirlineRecord
    {
        public string Id;
        public string Name;
        public string LiveryHex;
        public bool IsPlayer;
    }

    [Serializable]
    public sealed class AircraftRecord
    {
        public string Registration;
        public string AirlineId;
        public string TypeId;
        public string State;
        public long StateStartedAt;
        public bool HasStateEnd;
        public long StateEndsAt;
        public string Stand;
        public string DepartureStand;
        public string Destination;
        public bool HasScheduled;
        public string ScheduledDestination;
        public long ScheduledDepartAt;
        public int CompletedTrips;
    }

    public static class AirlineSave
    {
        public static AirlineSaveData Capture(AirlineOperations operations)
        {
            if (operations == null) throw new ArgumentNullException(nameof(operations));

            var data = new AirlineSaveData
            {
                HomeCode = operations.Home.Code,
                ClockSeconds = operations.ProcessedTo.ElapsedSeconds,
                RunwayFreeAtSeconds = operations.RunwayFreeAt.ElapsedSeconds,
                TotalEvents = operations.TotalEvents,
                RandomState = operations.RandomState
            };

            foreach (var airline in operations.Airlines)
            {
                data.Airlines.Add(new AirlineRecord
                {
                    Id = airline.Id.Value, Name = airline.Name, LiveryHex = airline.LiveryHex, IsPlayer = airline.IsPlayer
                });
            }

            foreach (var a in operations.Fleet)
            {
                data.Fleet.Add(new AircraftRecord
                {
                    Registration = a.Registration,
                    AirlineId = a.Airline.Id.Value,
                    TypeId = a.Type.Id,
                    State = a.State.ToString(),
                    StateStartedAt = a.StateStartedAt.ElapsedSeconds,
                    HasStateEnd = a.StateEndsAt.HasValue,
                    StateEndsAt = a.StateEndsAt?.ElapsedSeconds ?? 0,
                    Stand = a.Stand.Value ?? string.Empty,
                    DepartureStand = a.DepartureStand.Value ?? string.Empty,
                    Destination = a.CurrentDestination?.Code ?? string.Empty,
                    HasScheduled = a.Scheduled.HasValue,
                    ScheduledDestination = a.Scheduled?.Destination.Code ?? string.Empty,
                    ScheduledDepartAt = a.Scheduled?.DepartAt.ElapsedSeconds ?? 0,
                    CompletedTrips = a.CompletedTrips
                });
            }

            return data;
        }

        /// <summary>
        /// Rebuild an airline game from a save, with <paramref name="clock"/> already set to
        /// <see cref="AirlineSaveData.ClockSeconds"/>. Throws <see cref="FormatException"/>
        /// on anything it does not recognise, rather than resuming a half-right game.
        /// </summary>
        public static AirlineOperations Restore(AirlineSaveData data, ISimulationClock clock)
        {
            if (data == null) throw new FormatException("The save is empty.");
            if (clock == null) throw new ArgumentNullException(nameof(clock));
            if (data.Version != AirlineSaveData.CurrentVersion)
                throw new FormatException($"Save version {data.Version} is not supported (expected {AirlineSaveData.CurrentVersion}).");
            if (clock.Now.ElapsedSeconds != data.ClockSeconds)
                throw new ArgumentException("Set the clock to the saved time before restoring.", nameof(clock));
            if (!DestinationCatalogue.TryFind(data.HomeCode, out var home) || !home.Equals(DestinationCatalogue.Adelaide))
                throw new FormatException($"Unknown home airport '{data.HomeCode}'.");
            if (data.RandomState == 0)
                throw new FormatException("The save has no random state.");

            var operations = new AirlineOperations(clock, new SeededRandomSource(data.RandomState), home,
                AirlineOperations.AdelaideRegionalBays);

            var airlines = new Dictionary<string, Airline>(StringComparer.Ordinal);
            foreach (var record in data.Airlines ?? new List<AirlineRecord>())
            {
                Airline airline;
                try
                {
                    airline = new Airline(record.Id, record.Name, record.LiveryHex, record.IsPlayer);
                }
                catch (ArgumentException e)
                {
                    throw new FormatException($"Airline '{record.Id}' is invalid: {e.Message}");
                }

                operations.AddAirline(airline);
                airlines[record.Id] = airline;
            }

            if (operations.PlayerAirline == null)
                throw new FormatException("The save has no player airline.");

            foreach (var record in data.Fleet ?? new List<AircraftRecord>())
            {
                if (!airlines.TryGetValue(record.AirlineId ?? string.Empty, out var airline))
                    throw new FormatException($"{record.Registration} belongs to unknown airline '{record.AirlineId}'.");
                if (!AircraftType.TryFromId(record.TypeId, out var type))
                    throw new FormatException($"{record.Registration} has unknown aircraft type '{record.TypeId}'.");
                if (!Enum.TryParse(record.State, out FleetState state))
                    throw new FormatException($"{record.Registration} has unknown state '{record.State}'.");

                operations.RestoreAircraft(
                    record.Registration,
                    airline,
                    type,
                    state,
                    new SimulationTime(record.StateStartedAt),
                    record.HasStateEnd ? new SimulationTime(record.StateEndsAt) : null,
                    Stand(record.Stand),
                    Stand(record.DepartureStand),
                    OptionalDestination(record.Destination, record.Registration),
                    record.HasScheduled
                        ? new ScheduledDeparture(RequiredDestination(record.ScheduledDestination, record.Registration),
                            new SimulationTime(record.ScheduledDepartAt))
                        : null,
                    record.CompletedTrips);
            }

            operations.RestoreTower(new SimulationTime(data.RunwayFreeAtSeconds), data.TotalEvents);
            return operations;
        }

        private static StableId Stand(string value) =>
            string.IsNullOrWhiteSpace(value) ? default : new StableId(value);

        private static Destination? OptionalDestination(string code, string registration) =>
            string.IsNullOrEmpty(code) ? null : RequiredDestination(code, registration);

        private static Destination RequiredDestination(string code, string registration)
        {
            if (!DestinationCatalogue.TryFind(code, out var destination))
                throw new FormatException($"{registration} refers to unknown destination '{code}'.");
            return destination;
        }
    }
}
