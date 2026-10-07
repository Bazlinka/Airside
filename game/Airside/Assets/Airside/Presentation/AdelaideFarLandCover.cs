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

        /// <summary>The grid index nearest runway-local coordinate <paramref name="metres"/> (same on both axes).</summary>
        public int CellOf(float metres) => (int)Math.Round((metres - Origin) / Spacing);

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

        // Albedo as the shader sees it (the project renders in Gamma space): the far ring's drape as rendered (Sentinel-2 times tint at the far strength, over the old plain
        // vertex colour), averaged per class 15-30 km from the field by scripts/calibrate-landcover-palette.py. The far ring
        // hands its drape over to these before 30 km, so they must match the image in game or the hand-over shows as a ring.
        private static readonly float[][] Base =
        {
            new[] { 0.065f, 0.103f, 0.109f },   // water (sea colour is passed in by the caller; this is inland)
            new[] { 0.140f, 0.130f, 0.088f },   // tree
            new[] { 0.156f, 0.150f, 0.110f },   // shrub
            new[] { 0.204f, 0.177f, 0.128f },   // grass
            new[] { 0.263f, 0.214f, 0.158f },   // crop
            new[] { 0.226f, 0.211f, 0.164f },   // built
            new[] { 0.357f, 0.339f, 0.276f },   // bare
            new[] { 0.107f, 0.119f, 0.075f }    // wetland
        };

        // Paddock variants of the measured crop colour: a green crop and a paler fallow.
        private static readonly float[] GreenCrop = { 0.115f, 0.150f, 0.085f };
        private static readonly float[] FallowCrop = { 0.320f, 0.255f, 0.180f };

        /// <summary>
        /// One channel of the far ring's vertex colour while its drape hands over to land cover. The shader draws
        /// <c>strength * (1 - t) * image + (1 - strength * (1 - t)) * vertex</c>; this vertex makes that an exact crossfade from
        /// the old look (<paramref name="plain"/> under the image) at t = 0 to <paramref name="land"/> at t = 1, whatever the image.
        /// </summary>
        public static float DrapeHandover(float plain, float land, float t, float strength)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            var weight = 1f - strength * (1f - t);
            return weight <= 1e-5f ? plain : ((1f - t) * (1f - strength) * plain + t * land) / weight;
        }

        /// <summary>HLSL smoothstep, so C# can predict the shader's hand-over weight at a vertex.</summary>
        public static float Smoothstep(float edge0, float edge1, float x)
        {
            var t = Math.Max(0f, Math.Min(1f, (x - edge0) / Math.Max(1e-5f, edge1 - edge0)));
            return t * t * (3f - 2f * t);
        }

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
            // Light touch: the palette is measured from an image that already holds the Hills' shading, and the mesh is lit.
            var shade = (1f - 0.1f * Math.Max(0f, Math.Min(1f, slope))) * v;
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
