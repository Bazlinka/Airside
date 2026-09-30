using System;
using System.Collections.Generic;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>One foam ribbon segment along the real OSM coastline.</summary>
    public readonly struct CoastFoamSegment
    {
        public CoastFoamSegment(float ax, float az, float bx, float bz,
            float outwardX, float outwardZ)
        {
            Ax = ax;
            Az = az;
            Bx = bx;
            Bz = bz;
            OutwardX = outwardX;
            OutwardZ = outwardZ;
        }

        public float Ax { get; }
        public float Az { get; }
        public float Bx { get; }
        public float Bz { get; }
        /// <summary>Unit normal pointing seaward.</summary>
        public float OutwardX { get; }
        public float OutwardZ { get; }
        public float Length
        {
            get
            {
                var dx = Bx - Ax;
                var dz = Bz - Az;
                return (float)Math.Sqrt(dx * dx + dz * dz);
            }
        }
    }

    /// <summary>
    /// West Beach dune berms, shore-foam ribbons and a Patawalonga/Barcoo outlet
    /// deepen (visual overhaul Phase 1 landform/coast). Pure maths — no UnityEngine —
    /// so the surroundings mesh and headless tests share one table.
    /// </summary>
    public static class AdelaideCoastLandform
    {
        public const float DuneBandStartMetres = 28f;
        public const float DuneBandPeakMetres = 70f;
        public const float DuneBandEndMetres = 150f;
        public const float DunePeakBoostMetres = 2.4f;
        public const float FoamStepMetres = 90f;
        public const float FoamMinSegmentMetres = 25f;
        public const float OutletBlendStartMetres = 420f;
        public const float OutletBlendEndMetres = 70f;

        /// <summary>
        /// Extra height for a land vertex near the coast: a soft berm peaking ~70 m inland,
        /// stronger on OSM sand/scrub (West Beach dunes).
        /// </summary>
        public static float DuneBoostMetres(float coastDistance, float worldX, float worldZ,
            bool sandOrScrub)
        {
            if (coastDistance < DuneBandStartMetres || coastDistance > DuneBandEndMetres)
                return 0f;

            var intoBand = (coastDistance - DuneBandStartMetres)
                / (DuneBandPeakMetres - DuneBandStartMetres);
            var outOfBand = (DuneBandEndMetres - coastDistance)
                / (DuneBandEndMetres - DuneBandPeakMetres);
            var envelope = coastDistance <= DuneBandPeakMetres
                ? SmoothStep(0f, 1f, intoBand)
                : SmoothStep(0f, 1f, outOfBand);
            var variation = 0.55f + 0.9f * Warped(worldX, worldZ, 95f, 6103);
            var sand = sandOrScrub ? 1.35f : 0.75f;
            return DunePeakBoostMetres * envelope * variation * sand;
        }

        /// <summary>
        /// 0 at inland lakes, 1 when the Patawalonga corridor meets the gulf —
        /// used to drop the water surface and shift colour toward shallows.
        /// </summary>
        public static float OutletBlend(float coastDistance) =>
            1f - SmoothStep(OutletBlendEndMetres, OutletBlendStartMetres, coastDistance);

        public static IReadOnlyList<CoastFoamSegment> FoamSegments(float[] coastline = null)
        {
            coastline ??= AdelaideCoast.Coastline;
            var list = new List<CoastFoamSegment>();
            if (coastline == null || coastline.Length < 4)
                return list;

            var carry = 0f;
            for (var i = 0; i + 3 < coastline.Length; i += 2)
            {
                var ax = coastline[i];
                var az = coastline[i + 1];
                var bx = coastline[i + 2];
                var bz = coastline[i + 3];
                var dx = bx - ax;
                var dz = bz - az;
                var len = (float)Math.Sqrt(dx * dx + dz * dz);
                if (len < 1f)
                    continue;
                carry += len;
                if (carry < FoamStepMetres && i + 5 < coastline.Length)
                    continue;
                if (len < FoamMinSegmentMetres)
                    continue;
                carry = 0f;

                var nx = -dz / len;
                var nz = dx / len;
                // Prefer the normal that points into the sea polygon.
                var mx = (ax + bx) * 0.5f;
                var mz = (az + bz) * 0.5f;
                if (!PointInSea(mx + nx * 30f, mz + nz * 30f))
                {
                    nx = -nx;
                    nz = -nz;
                }

                list.Add(new CoastFoamSegment(ax, az, bx, bz, nx, nz));
            }

            return list;
        }

        /// <summary>Four x/z corners of a foam quad for one segment.</summary>
        public static float[] FoamQuad(CoastFoamSegment segment, float landwardMetres, float seawardMetres)
        {
            var ox = segment.OutwardX;
            var oz = segment.OutwardZ;
            return new[]
            {
                segment.Ax - ox * landwardMetres, segment.Az - oz * landwardMetres,
                segment.Bx - ox * landwardMetres, segment.Bz - oz * landwardMetres,
                segment.Bx + ox * seawardMetres, segment.Bz + oz * seawardMetres,
                segment.Ax + ox * seawardMetres, segment.Az + oz * seawardMetres
            };
        }

        private static bool PointInSea(float x, float z)
        {
            var poly = AdelaideCoast.SeaPolygon;
            if (poly == null || poly.Length < 6)
                return x < -2000f; // west fallback
            var inside = false;
            var count = poly.Length / 2;
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                var xi = poly[i * 2];
                var zi = poly[i * 2 + 1];
                var xj = poly[j * 2];
                var zj = poly[j * 2 + 1];
                var intersect = ((zi > z) != (zj > z))
                    && (x < (xj - xi) * (z - zi) / (zj - zi + 1e-12f) + xi);
                if (intersect)
                    inside = !inside;
            }

            return inside;
        }

        private static float SmoothStep(float edge0, float edge1, float v)
        {
            if (Math.Abs(edge1 - edge0) < 1e-6f)
                return v < edge0 ? 0f : 1f;
            var t = (v - edge0) / (edge1 - edge0);
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            return t * t * (3f - 2f * t);
        }

        private static float Warped(float x, float z, float featureMetres, int seed)
        {
            var f = 1f / Math.Max(1f, featureMetres);
            return ValueNoise(x * f, z * f, seed) * 0.6f
                + ValueNoise(x * f * 2.3f + 3.1f, z * f * 2.3f - 1.7f, seed + 17) * 0.4f;
        }

        private static float ValueNoise(float x, float z, int seed)
        {
            var xi = x >= 0f ? (int)x : (int)x - 1;
            var zi = z >= 0f ? (int)z : (int)z - 1;
            var fx = x - xi;
            var fz = z - zi;
            var ux = fx * fx * (3f - 2f * fx);
            var uz = fz * fz * (3f - 2f * fz);
            var a = Hash(xi, zi, seed);
            var b = Hash(xi + 1, zi, seed);
            var c = Hash(xi, zi + 1, seed);
            var d = Hash(xi + 1, zi + 1, seed);
            return Lerp(Lerp(a, b, ux), Lerp(c, d, ux), uz);
        }

        private static float Hash(int x, int z, int seed)
        {
            unchecked
            {
                var h = x * 374761393 + z * 668265263 + seed * 1274126177;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0x7fffffff) / 2147483647f;
            }
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
