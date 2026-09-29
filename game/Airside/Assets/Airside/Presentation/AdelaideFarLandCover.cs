using System;
using System.IO;
using System.IO.Compression;

namespace Airside.Presentation
{
    /// <summary>
    /// ADR 0190 — a coarse land-cover map of greater Adelaide on the far DEM's grid (769 x 769 cells of 250 m, +-96 km), baked from
    /// ESA WorldCover 2021 by <c>scripts/generate-adelaide-landcover.py</c>. It colours the outer terrain seen when the camera is
    /// zoomed out: suburbs, crops, woodland, water. Pure (no UnityEngine).
    /// </summary>
    public sealed class AdelaideFarLandCover
    {
        public const string ArtPath = "Terrain/landcover_adelaide_far_v01.bin";

        public const int Water = 0, Tree = 1, Shrub = 2, Grass = 3, Crop = 4, Built = 5, Bare = 6, Wetland = 7;
        public const int ClassCount = 8;

        private readonly byte[] _cells;

        private AdelaideFarLandCover(int count, float spacing, float origin, byte[] cells)
        {
            Count = count;
            Spacing = spacing;
            Origin = origin;
            _cells = cells;
        }

        public int Count { get; }
        public float Spacing { get; }
        public float Origin { get; }

        /// <summary>The class of cell (xi, zi); cells outside the grid read as their nearest edge cell.</summary>
        public int ClassAt(int xi, int zi)
        {
            xi = Math.Max(0, Math.Min(Count - 1, xi));
            zi = Math.Max(0, Math.Min(Count - 1, zi));
            return _cells[zi * Count + xi];
        }

        /// <summary>Header (ALCV, version, count, spacing, origin) then a raw-deflate stream of one byte per cell; null if not that.</summary>
        public static AdelaideFarLandCover Parse(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 20 || bytes[0] != 'A' || bytes[1] != 'L' || bytes[2] != 'C' || bytes[3] != 'V')
                return null;
            var version = BitConverter.ToInt32(bytes, 4);
            var count = BitConverter.ToInt32(bytes, 8);
            var spacing = BitConverter.ToSingle(bytes, 12);
            var origin = BitConverter.ToSingle(bytes, 16);
            if (version != 1 || count < 2 || count > 4096 || spacing <= 0f)
                return null;
            var cells = new byte[count * count];
            using (var source = new MemoryStream(bytes, 20, bytes.Length - 20))
            using (var inflate = new DeflateStream(source, CompressionMode.Decompress))
            {
                var read = 0;
                while (read < cells.Length)
                {
                    var n = inflate.Read(cells, read, cells.Length - read);
                    if (n <= 0)
                        return null;
                    read += n;
                }
            }

            return new AdelaideFarLandCover(count, spacing, origin, cells);
        }

        // Linear-space base colours (sRGB in the comment). A late-summer Adelaide: dry grass and crops are straw, the plain is
        // pale, the Hills and the Fleurieu are darker green, suburbs read as grey-tan.
        private static readonly float[][] Base =
        {
            new[] { 0.020f, 0.085f, 0.135f },   // water (sea colour is passed in by the caller; this is inland)
            new[] { 0.014f, 0.052f, 0.020f },   // tree  (0.13, 0.25, 0.15)
            new[] { 0.070f, 0.085f, 0.030f },   // shrub (0.30, 0.33, 0.19)
            new[] { 0.190f, 0.170f, 0.070f },   // grass (0.47, 0.45, 0.30)
            new[] { 0.250f, 0.200f, 0.070f },   // crop  (0.54, 0.49, 0.30)
            new[] { 0.150f, 0.140f, 0.125f },   // built (0.42, 0.41, 0.39)
            new[] { 0.290f, 0.230f, 0.150f },   // bare  (0.58, 0.52, 0.42)
            new[] { 0.030f, 0.075f, 0.060f }    // wetland
        };

        private static readonly float[] GreenCrop = { 0.075f, 0.140f, 0.045f };
        private static readonly float[] FallowCrop = { 0.310f, 0.240f, 0.100f };

        /// <summary>
        /// The colour (linear rgb) of a cell: its class, with a stable patchwork of paddocks in crop land and a little variation
        /// everywhere so the plain does not read as one flat tone. <paramref name="slope"/> (0 flat .. 1 steep) darkens hillsides.
        /// </summary>
        public static void Colour(int cls, int xi, int zi, float slope, float[] into)
        {
            cls = Math.Max(0, Math.Min(ClassCount - 1, cls));
            var b = Base[cls];
            float r = b[0], g = b[1], bl = b[2];
            if (cls == Crop)
            {
                // Paddocks of about 1 km: green, straw or fallow, chosen by a stable hash.
                var h = Hash(xi / 4, zi / 4);
                var pick = h % 3u;
                var p = pick == 0 ? GreenCrop : pick == 1 ? b : FallowCrop;
                r = p[0]; g = p[1]; bl = p[2];
            }

            var v = 0.90f + 0.20f * (Hash(xi, zi) % 1000u) / 1000f;
            var shade = (1f - 0.35f * Math.Max(0f, Math.Min(1f, slope))) * v;
            into[0] = r * shade;
            into[1] = g * shade;
            into[2] = bl * shade;
        }

        private static uint Hash(int x, int z)
        {
            unchecked
            {
                var h = (uint)(x * 73856093) ^ (uint)(z * 19349663) ^ 0x9E3779B9u;
                h ^= h >> 15;
                h *= 0x2C1B3C6Du;
                h ^= h >> 12;
                return h;
            }
        }
    }
}
