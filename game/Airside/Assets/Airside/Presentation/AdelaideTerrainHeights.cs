using System;

namespace Airside.Presentation
{
    /// <summary>
    /// ADR 0158 — real ground heights around Adelaide in the runway frame, from the Copernicus
    /// GLO-30 DEM (<c>scripts/generate-adelaide-terrain.py</c>): the plain rising gently inland and
    /// the Adelaide Hills to the south-east. A square grid of metres above sea level; sea is 0.
    /// No UnityEngine, so the file format and the height rules are tested headlessly.
    /// </summary>
    public sealed class AdelaideTerrainHeights
    {
        public const string ArtPath = "Terrain/dem_adelaide_runway_v01.bin";
        public const string Attribution =
            "Elevation: Copernicus DEM GLO-30 © DLR e.V. 2010-2014 and © Airbus Defence and Space GmbH 2014-2018 provided under COPERNICUS by the European Union and ESA";

        /// <summary>
        /// Height of the surroundings' coastal plain above sea level. The mesh already puts the
        /// plain at <c>PlainBelowPavement</c> and the sea at <c>SeaBelowPavement</c>; only ground
        /// higher than this is added on top, so the coast, beach and airfield edge keep their shape.
        /// </summary>
        public const float PlainAboveSeaMetres = 3.6f;

        /// <summary>No relief next to the airfield: it eases in from here…</summary>
        public const float ReliefStartMetres = 700f;

        /// <summary>…and is at full height from here (metres outside the airfield rectangle).</summary>
        public const float ReliefFullMetres = 2500f;

        private readonly short[] _heights;
        private readonly float _heightScale;

        private AdelaideTerrainHeights(int count, float spacing, float origin, float heightScale, short[] heights)
        {
            Count = count;
            Spacing = spacing;
            Origin = origin;
            _heightScale = heightScale;
            _heights = heights;
        }

        public int Count { get; }
        public float Spacing { get; }

        /// <summary>x and z of sample [0, 0]; the grid is square.</summary>
        public float Origin { get; }

        public float HalfExtent => -Origin;

        /// <summary>Reads the generator's "ADEM" v1 file; null for anything else.</summary>
        public static AdelaideTerrainHeights Parse(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 24 || bytes[0] != 'A' || bytes[1] != 'D' || bytes[2] != 'E' || bytes[3] != 'M')
                return null;
            var version = BitConverter.ToInt32(bytes, 4);
            var count = BitConverter.ToInt32(bytes, 8);
            var spacing = BitConverter.ToSingle(bytes, 12);
            var origin = BitConverter.ToSingle(bytes, 16);
            var scale = BitConverter.ToSingle(bytes, 20);
            if (version != 1 || count < 2 || spacing <= 0f || scale <= 0f)
                return null;
            var samples = (long)count * count;
            if (bytes.Length < 24 + samples * 2)
                return null;
            var heights = new short[samples];
            Buffer.BlockCopy(bytes, 24, heights, 0, (int)(samples * 2));
            if (!BitConverter.IsLittleEndian)
                for (var i = 0; i < heights.Length; i++)
                    heights[i] = (short)((heights[i] << 8) | ((heights[i] >> 8) & 0xFF));
            return new AdelaideTerrainHeights(count, spacing, origin, scale, heights);
        }

        /// <summary>Stored sample, metres above sea level.</summary>
        public float Sample(int xi, int zi)
        {
            xi = Math.Max(0, Math.Min(Count - 1, xi));
            zi = Math.Max(0, Math.Min(Count - 1, zi));
            return _heights[zi * Count + xi] * _heightScale;
        }

        /// <summary>Bilinear height above sea level at a runway-frame x, z (clamped at the edge).</summary>
        public float Height(float x, float z)
        {
            var fx = (x - Origin) / Spacing;
            var fz = (z - Origin) / Spacing;
            var xi = (int)Math.Floor(fx);
            var zi = (int)Math.Floor(fz);
            var tx = Math.Max(0f, Math.Min(1f, fx - xi));
            var tz = Math.Max(0f, Math.Min(1f, fz - zi));
            var h00 = Sample(xi, zi);
            var h10 = Sample(xi + 1, zi);
            var h01 = Sample(xi, zi + 1);
            var h11 = Sample(xi + 1, zi + 1);
            var a = h00 + (h10 - h00) * tx;
            var b = h01 + (h11 - h01) * tx;
            return a + (b - a) * tz;
        }

        /// <summary>Metres the ground stands above the surroundings' flat plain; never negative.</summary>
        public static float ReliefAbovePlain(float heightAboveSea) =>
            Math.Max(0f, heightAboveSea - PlainAboveSeaMetres);

        /// <summary>0 beside the airfield, easing to 1 by <see cref="ReliefFullMetres"/> outside it.</summary>
        public static float ReliefWeight(float metresOutsideAirfield)
        {
            var t = (metresOutsideAirfield - ReliefStartMetres) / (ReliefFullMetres - ReliefStartMetres);
            t = Math.Max(0f, Math.Min(1f, t));
            return t * t * (3f - 2f * t);
        }

        /// <summary>The relief the surroundings add at a point.</summary>
        public float Relief(float x, float z, float metresOutsideAirfield) =>
            ReliefAbovePlain(Height(x, z)) * ReliefWeight(metresOutsideAirfield);
    }
}
