using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Cached StreamingAssets / Editor PNG loads. The airfield stamps the same
    /// handful of surface maps onto thousands of tiles; loading each copy from
    /// disk was both a startup stall and a VRAM leak.
    /// </summary>
    public static class AirsideArtTextures
    {
        private static readonly Dictionary<string, Texture2D> Cache = new(System.StringComparer.Ordinal);

        // A file that is absent or unreadable was recorded as a null cache entry, which the
        // lookup below could not tell from "not cached", so every request for a missing map
        // went back to the disk — once per material built, for the life of the run.
        private static readonly HashSet<string> Misses = new(System.StringComparer.Ordinal);

        /// <param name="keepReadable">
        /// Keep the CPU-side pixel copy. Only callers that read pixels back need it; everyone
        /// else lets LoadImage release it, halving each surface map's memory footprint.
        /// </param>
        public static Texture2D Load(
            string artRelativePath,
            bool linear = false,
            TextureWrapMode wrap = TextureWrapMode.Repeat,
            bool keepReadable = false)
        {
            if (string.IsNullOrEmpty(artRelativePath))
                return null;

            var key = CacheKey(artRelativePath, linear, wrap, keepReadable);
            if (Cache.TryGetValue(key, out var cached) && cached != null)
                return cached;
            if (Misses.Contains(key))
                return null;

            var fullPath = ArtRuntimePaths.ResolveExisting(artRelativePath);
            if (fullPath == null)
            {
                Misses.Add(key);
                return null;
            }

            try
            {
                var bytes = File.ReadAllBytes(fullPath);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: true, linear: linear);
                if (!texture.LoadImage(bytes, markNonReadable: !keepReadable))
                {
                    // The 2x2 placeholder is already on the GPU; dropping the reference
                    // without destroying it leaked one texture per unreadable file.
                    Object.Destroy(texture);
                    Misses.Add(key);
                    return null;
                }

                texture.name = Path.GetFileNameWithoutExtension(artRelativePath);
                texture.wrapMode = wrap;
                ApplyRuntimeFilter(texture);
                Cache[key] = texture;
                return texture;
            }
            catch
            {
                Misses.Add(key);
                return null;
            }
        }

        /// <summary>Test seam: how many paths are remembered as unavailable.</summary>
        public static int MissCount => Misses.Count;

        public static void ApplyRuntimeFilter(Texture2D texture)
        {
            if (texture == null)
                return;
            texture.filterMode = FilterMode.Trilinear;
            texture.anisoLevel = AirsideRuntimeQuality.AnisoLevel;
        }

        private static string CacheKey(string artRelativePath, bool linear, TextureWrapMode wrap, bool keepReadable) =>
            (linear ? "L|" : "S|") + (keepReadable ? "R|" : "N|") + (int)wrap + "|" + artRelativePath;
    }
}
