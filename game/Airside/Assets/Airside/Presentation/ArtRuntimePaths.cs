using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Resolves runtime art files for both the Editor and packaged players.
    /// Existence checks are cached — the tile airfield used to hit disk for
    /// the same PNG thousands of times during Awake.
    /// </summary>
    public static class ArtRuntimePaths
    {
        private static readonly Dictionary<string, string> ExistingCache = new(System.StringComparer.Ordinal);

        public static string StreamingArtRoot =>
            Path.Combine(Application.streamingAssetsPath, "Airside", "Art");

        public static string EditorArtRoot =>
            Path.Combine(Application.dataPath, "Airside", "Art");

        /// <summary>
        /// Returns an absolute path to an art file when it exists, otherwise null.
        /// Prefers StreamingAssets (what the packaged player ships), then the
        /// Editor Assets tree.
        /// </summary>
        public static string ResolveExisting(string artRelativePath)
        {
            if (string.IsNullOrEmpty(artRelativePath))
                return null;

            if (ExistingCache.TryGetValue(artRelativePath, out var cached))
                return string.IsNullOrEmpty(cached) ? null : cached;

            var streaming = Path.Combine(StreamingArtRoot, artRelativePath);
            if (File.Exists(streaming))
            {
                ExistingCache[artRelativePath] = streaming;
                return streaming;
            }

            var editor = Path.Combine(EditorArtRoot, artRelativePath);
            if (File.Exists(editor))
            {
                ExistingCache[artRelativePath] = editor;
                return editor;
            }

            ExistingCache[artRelativePath] = string.Empty;
            return null;
        }
    }
}
