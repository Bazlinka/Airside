using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Persistence
{
    [Serializable]
    public sealed class AirsideCommandRecord
    {
        public string commandId;
        public string commandType;
        public long simulationSecond;
    }

    [Serializable]
    public sealed class AirsideSaveData
    {
        public const int CurrentSchemaVersion = 2;
        public const int MinimumSupportedSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public long revision;
        public long simulatedSeconds;
        public long savedUnixSeconds;
        public uint randomSeed;

        // Added in schema 2. Old saves are migrated to the default location on load.
        public string locationId = AirportLocation.Default.Id;

        public List<AirsideCommandRecord> commands = new();

        /// <summary>Bring an older-schema save up to the current schema. Safe to call on any save.</summary>
        public void Migrate()
        {
            // Reject unsupported input before migration can erase its original version.
            if (schemaVersion < MinimumSupportedSchemaVersion || schemaVersion > CurrentSchemaVersion)
                throw new InvalidOperationException($"Unsupported save schema {schemaVersion}.");
            if (schemaVersion < 2)
            {
                if (string.IsNullOrWhiteSpace(locationId))
                    locationId = AirportLocation.Default.Id;
                schemaVersion = 2;
            }
        }

        public void Validate()
        {
            if (schemaVersion < MinimumSupportedSchemaVersion || schemaVersion > CurrentSchemaVersion)
                throw new InvalidOperationException($"Unsupported save schema {schemaVersion}.");
            if (revision < 0 || simulatedSeconds < 0 || savedUnixSeconds < 0)
                throw new InvalidOperationException("Save contains an invalid negative value.");
            if (randomSeed == 0)
                throw new InvalidOperationException("Save random seed is missing.");
            if (string.IsNullOrWhiteSpace(locationId))
                throw new InvalidOperationException("Save location is missing.");

            commands ??= new List<AirsideCommandRecord>();
            foreach (var command in commands)
            {
                if (command == null || string.IsNullOrWhiteSpace(command.commandId) ||
                    string.IsNullOrWhiteSpace(command.commandType) || command.simulationSecond < 0)
                    throw new InvalidOperationException("Save contains an invalid command record.");
            }
        }
    }
}
