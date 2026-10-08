using System;
using System.IO;
using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// The single airline save on disk (ADR 0045): JSON in the player's persistent data
    /// folder, written to a temporary file and moved into place so a crash mid-write
    /// never leaves a half-written save. One previous readable save is retained for recovery.
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
            {
                // Never rotate an unreadable primary over a usable recovery copy. A primary this session wrote
                // itself is known good, so only the first save re-reads (and parses) it: autosave stays cheap.
                var keepBackup = (_verifiedPrimary == path) || TryReadFile(path, out _, out _);
                File.Replace(temporary, path, destinationBackupFileName: keepBackup ? path + ".bak" : null);
            }
            else
                File.Move(temporary, path);
            _verifiedPrimary = path;
        }

        // The path whose current primary file this process wrote, and so knows is readable.
        private static string _verifiedPrimary;

        /// <summary>Read the current save, or its previous copy if the current JSON is unreadable.</summary>
        public static bool TryRead(string path, out AirlineSaveData data, out string error)
            => TryRead(path, out data, out error, out _);

        public static bool TryRead(string path, out AirlineSaveData data, out string error,
            out bool recoveredFromBackup)
        {
            recoveredFromBackup = false;
            if (TryReadFile(path, out data, out error))
                return true;

            var primaryError = error;
            if (TryReadFile(path + ".bak", out data, out _))
            {
                recoveredFromBackup = true;
                error = string.Empty;
                return true;
            }

            error = primaryError;
            return false;
        }

        private static bool TryReadFile(string path, out AirlineSaveData data, out string error)
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
