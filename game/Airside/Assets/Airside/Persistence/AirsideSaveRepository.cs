using System;
using System.IO;
using UnityEngine;

namespace Airside.Persistence
{
    public sealed class AirsideSaveRepository
    {
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
            var json = JsonUtility.ToJson(save, true);
            using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(json);
                writer.Flush();
                stream.Flush(true);
            }

            if (File.Exists(_path))
            {
                try
                {
                    File.Replace(temporaryPath, _path, PreviousPath, true);
                }
                catch (PlatformNotSupportedException)
                {
                    File.Copy(_path, PreviousPath, true);
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
                save = JsonUtility.FromJson<AirsideSaveData>(File.ReadAllText(path));
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
