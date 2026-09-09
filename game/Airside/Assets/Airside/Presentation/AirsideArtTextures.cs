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

        public static Texture2D Load(
            string artRelativePath,
            bool linear = false,
            TextureWrapMode wrap = TextureWrapMode.Repeat)
        {
            if (string.IsNullOrEmpty(artRelativePath))
                return null;

            var key = CacheKey(artRelativePath, linear, wrap);
            if (Cache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var fullPath = ArtRuntimePaths.ResolveExisting(artRelativePath);
            if (fullPath == null)
            {
                Cache[key] = null;
                return null;
            }

            try
            {
                var bytes = File.ReadAllBytes(fullPath);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: true, linear: linear);
                if (!texture.LoadImage(bytes))
                {
                    Cache[key] = null;
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
                Cache[key] = null;
                return null;
            }
        }

        public static void ApplyRuntimeFilter(Texture2D texture)
        {
            if (texture == null)
                return;
            texture.filterMode = FilterMode.Trilinear;
            texture.anisoLevel = AirsideRuntimeQuality.AnisoLevel;
        }

        private static string CacheKey(string artRelativePath, bool linear, TextureWrapMode wrap) =>
            (linear ? "L|" : "S|") + (int)wrap + "|" + artRelativePath;
    }
}
