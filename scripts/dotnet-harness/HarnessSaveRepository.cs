using System;
using System.IO;
using System.Text.Json;
using Airside.Persistence;

namespace Airside.Persistence
{
    // Headless-harness stand-in for the real Unity-facing AirsideSaveRepository
    // (which serialises via UnityEngine.JsonUtility). Same public surface,
    // System.Text.Json instead, so Domain/Simulation/Persistence tests can run
    // under `dotnet test` without a Unity install. Not part of the Unity project.
    public sealed class AirsideSaveRepository
    {
        private static readonly JsonSerializerOptions SerializerOptions = new() { IncludeFields = true };

        private readonly string _path;

        public AirsideSaveRepository(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A save path is required.", nameof(path));
            _path = path;
        }

        public string PreviousPath => _path + ".previous";

        public bool TryLoad(out AirsideSaveData save, out bool recoveredPrevious)
        {
            if (TryRead(_path, out save))
            {
                recoveredPrevious = false;
                return true;
            }

            if (TryRead(PreviousPath, out save))
            {
                recoveredPrevious = true;
                return true;
            }

            recoveredPrevious = false;
            save = null;
            return false;
        }

        public void Write(AirsideSaveData save)
        {
            save.Validate();
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var temporaryPath = _path + ".temporary";
            var json = JsonSerializer.Serialize(save, SerializerOptions);
            using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(json);
                writer.Flush();
                stream.Flush(true);
            }

            if (File.Exists(_path))
            {
                // A recovery write must not rotate a corrupt primary over the last
                // known-good backup. Validate the actual file before replacing it.
                var backupPath = TryRead(_path, out _) ? PreviousPath : null;
                try
                {
                    File.Replace(temporaryPath, _path, backupPath, true);
                }
                catch (PlatformNotSupportedException)
                {
                    if (backupPath != null)
                        File.Copy(_path, backupPath, true);
                    File.Copy(temporaryPath, _path, true);
                    File.Delete(temporaryPath);
                }
            }
            else
            {
                File.Move(temporaryPath, _path);
            }
        }

        private static bool TryRead(string path, out AirsideSaveData save)
        {
            save = null;
            if (!File.Exists(path))
                return false;

            try
            {
                save = JsonSerializer.Deserialize<AirsideSaveData>(File.ReadAllText(path), SerializerOptions);
                save?.Migrate();
                save?.Validate();
                return save != null;
            }
            catch (Exception)
            {
                save = null;
                return false;
            }
        }
    }
}
