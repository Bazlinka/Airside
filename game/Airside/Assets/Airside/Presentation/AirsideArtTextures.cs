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
                // A very large colour image (the 4096 px satellite) stays readable just long
                // enough to block-compress it: uncompressed it would hold ~85 MB of VRAM with
                // mips, compressed about a sixth of that.
                var compress = !keepReadable && !linear && IsLargeImage(bytes);
                if (!texture.LoadImage(bytes, markNonReadable: !keepReadable && !compress))
                {
                    // The 2x2 placeholder is already on the GPU; dropping the reference
                    // without destroying it leaked one texture per unreadable file.
                    if (Application.isPlaying)
                        Object.Destroy(texture);
                    else
                        Object.DestroyImmediate(texture);
                    Misses.Add(key);
                    return null;
                }

                if (compress)
                {
                    texture.Compress(highQuality: false);
                    texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
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

        /// <summary>Images at or above this many pixels are block-compressed on load.</summary>
        public const int CompressAtPixels = 4096 * 4096;

        /// <summary>
        /// True for a PNG or JPEG whose header says it is at least <see cref="CompressAtPixels"/>.
        /// Read from the header so the decision is made before decoding.
        /// </summary>
        public static bool IsLargeImage(byte[] bytes)
        {
            return TryReadImageSize(bytes, out var width, out var height)
                   && (long)width * height >= CompressAtPixels;
        }

        public static bool TryReadImageSize(byte[] bytes, out int width, out int height)
        {
            width = height = 0;
            if (bytes == null || bytes.Length < 24)
                return false;
            // PNG: signature, then the IHDR chunk's big-endian width and height.
            if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
            {
                width = BigEndian(bytes, 16);
                height = BigEndian(bytes, 20);
                return width > 0 && height > 0;
            }

            // JPEG: walk the markers to the first start-of-frame.
            if (bytes[0] != 0xFF || bytes[1] != 0xD8)
                return false;
            var i = 2;
            while (i + 9 < bytes.Length)
            {
                if (bytes[i] != 0xFF)
                    return false;
                var marker = bytes[i + 1];
                var length = (bytes[i + 2] << 8) | bytes[i + 3];
                var isFrame = marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC;
                if (isFrame)
                {
                    height = (bytes[i + 5] << 8) | bytes[i + 6];
                    width = (bytes[i + 7] << 8) | bytes[i + 8];
                    return width > 0 && height > 0;
                }

                i += 2 + length;
            }

            return false;
        }

        private static int BigEndian(byte[] bytes, int at) =>
            (bytes[at] << 24) | (bytes[at + 1] << 16) | (bytes[at + 2] << 8) | bytes[at + 3];

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
