using System;
using System.IO;
using Airside.Domain;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Reads the git stamp Unity copies from StreamingAssets. Missing or unreadable
    /// files stay playable and report <see cref="BuildIdentity.Unstamped"/>.
    /// </summary>
    public static class BuildIdentityReader
    {
        public const string FileName = "build-identity.txt";

        private static BuildIdentity _current;

        public static BuildIdentity Current => _current ??= Load();

        public static BuildIdentity Load()
        {
            try
            {
                var path = Path.Combine(Application.streamingAssetsPath, FileName);
                if (!File.Exists(path))
                    return BuildIdentity.Unstamped;
                return BuildIdentity.Parse(File.ReadAllText(path));
            }
            catch (Exception)
            {
                return BuildIdentity.Unstamped;
            }
        }
    }
}
