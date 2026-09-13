using System;
using System.IO;
using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// The single airline save on disk (ADR 0045): JSON in the player's persistent data
    /// folder, written to a temporary file and moved into place so a crash mid-write
    /// never leaves a half-written save.
    /// </summary>
    public static class AirlineSaveFile
    {
        public const string FileName = "airline-save.json";

        public static string DefaultPath => Path.Combine(Application.persistentDataPath, FileName);

        public static void Write(string path, AirlineSaveData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(data, prettyPrint: true));
            if (File.Exists(path))
                File.Replace(temporary, path, destinationBackupFileName: null);
            else
                File.Move(temporary, path);
        }

        /// <summary>False with a reason when there is no save or it cannot be parsed.</summary>
        public static bool TryRead(string path, out AirlineSaveData data, out string error)
        {
            data = null;
            error = string.Empty;
            if (!File.Exists(path))
            {
                error = "No saved airline.";
                return false;
            }

            try
            {
                data = JsonUtility.FromJson<AirlineSaveData>(File.ReadAllText(path));
                if (data != null && data.Airlines != null && data.Airlines.Count > 0)
                    return true;
                error = "The save file is empty.";
            }
            catch (Exception e)
            {
                error = $"The save file could not be read: {e.Message}";
            }

            data = null;
            return false;
        }
    }
}
