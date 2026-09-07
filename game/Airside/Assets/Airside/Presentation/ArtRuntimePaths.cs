using System.IO;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Resolves runtime art files for both the Editor and packaged players.
    ///
    /// Packaged macOS/Windows/Linux builds do <b>not</b> ship the Unity
    /// <c>Assets/</c> tree as loose files under <see cref="Application.dataPath"/>.
    /// Runtime filesystem loaders must therefore read from
    /// <see cref="Application.streamingAssetsPath"/> (populated by
    /// <c>scripts/sync-art-streaming-assets.sh</c>).
    ///
    /// Editor play mode still falls back to <c>Assets/Airside/Art</c> so artists
    /// can iterate before re-syncing StreamingAssets.
    ///
    /// Long-term production path: Unity-imported meshes/prefabs or Addressables.
    /// This helper keeps the interim glTF/PNG filesystem pipeline honest in builds.
    /// </summary>
    public static class ArtRuntimePaths
    {
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

            var streaming = Path.Combine(StreamingArtRoot, artRelativePath);
            if (File.Exists(streaming))
                return streaming;

            var editor = Path.Combine(EditorArtRoot, artRelativePath);
            if (File.Exists(editor))
                return editor;

            return null;
        }
    }
}
