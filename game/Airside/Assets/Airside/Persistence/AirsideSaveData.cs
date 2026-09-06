using System;
using System.Collections.Generic;

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
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public long revision;
        public long simulatedSeconds;
        public long savedUnixSeconds;
        public uint randomSeed;
        public List<AirsideCommandRecord> commands = new();

        public void Validate()
        {
            if (schemaVersion != CurrentSchemaVersion)
                throw new InvalidOperationException($"Unsupported save schema {schemaVersion}.");
            if (revision < 0 || simulatedSeconds < 0 || savedUnixSeconds < 0)
                throw new InvalidOperationException("Save contains an invalid negative value.");
            if (randomSeed == 0)
                throw new InvalidOperationException("Save random seed is missing.");

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
