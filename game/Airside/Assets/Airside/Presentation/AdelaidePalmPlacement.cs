using System;
using System.Collections.Generic;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>
    /// Deterministic date-palm sites along OSM car-park edges near Terminal 1 (ADR 0215).
    /// Pure maths — fills the landside hole left by NDVI tree exclusion.
    /// </summary>
    public static class AdelaidePalmPlacement
    {
        public const int MaxPalms = 180;
        public const float SpacingMetres = 14f;
        public const float EdgeInsetMetres = 2.2f;

        /// <summary>Tighter than car draw radius — palms only around the terminal landside ring.</summary>
        public const float PalmRadiusMetres = 900f;

        public const float PrecinctMinX = 750f;
        public const float PrecinctMaxX = 1750f;
        public const float PrecinctMinZ = 200f;
        public const float PrecinctMaxZ = 1000f;

        public const float MinHeightMetres = 7f;
        public const float MaxHeightMetres = 10f;

        private static Site[] _cached;

        public readonly struct Site
        {
            public readonly float X;
            public readonly float Z;
            public readonly float YawRad;
            public readonly float HeightMetres;

            public Site(float x, float z, float yawRad, float heightMetres)
            {
                X = x;
                Z = z;
                YawRad = yawRad;
                HeightMetres = heightMetres;
            }
        }

        /// <summary>Sorted, deterministic sites. Cached after first call.</summary>
        public static Site[] Sites()
        {
            if (_cached != null)
                return _cached;
            _cached = ComputeSites();
            return _cached;
        }

        /// <summary>Test hook — clears the cache so layout rule changes are re-evaluated.</summary>
        internal static void ClearCache() => _cached = null;

        private static Site[] ComputeSites()
        {
            var raw = new List<Site>(MaxPalms * 2);
            var cx = AdelaideCarParkGeometry.CentreX;
            var cz = AdelaideCarParkGeometry.CentreZ;
            var r2 = PalmRadiusMetres * PalmRadiusMetres;

            for (var p = 0; p < AdelaideCarParks.PolygonCount; p++)
            {
                var start = AdelaideCarParks.PolygonStarts[p];
                var end = AdelaideCarParks.PolygonStarts[p + 1];
                var n = end - start;
                if (n < 3)
                    continue;

                var sumX = 0f;
                var sumZ = 0f;
                for (var i = start; i < end; i++)
                {
                    sumX += AdelaideCarParks.Points[i * 2];
                    sumZ += AdelaideCarParks.Points[i * 2 + 1];
                }

                var midX = sumX / n;
                var midZ = sumZ / n;
                var dx = midX - cx;
                var dz = midZ - cz;
                if (dx * dx + dz * dz > r2)
                    continue;
                if (midX < PrecinctMinX || midX > PrecinctMaxX || midZ < PrecinctMinZ || midZ > PrecinctMaxZ)
                    continue;

                for (var i = 0; i < n; i++)
                {
                    var i0 = start + i;
                    var i1 = start + (i + 1) % n;
                    var ax = AdelaideCarParks.Points[i0 * 2];
                    var az = AdelaideCarParks.Points[i0 * 2 + 1];
                    var bx = AdelaideCarParks.Points[i1 * 2];
                    var bz = AdelaideCarParks.Points[i1 * 2 + 1];
                    var ex = bx - ax;
                    var ez = bz - az;
                    var len = (float)Math.Sqrt(ex * ex + ez * ez);
                    if (len < SpacingMetres * 0.5f)
                        continue;
                    var ux = ex / len;
                    var uz = ez / len;
                    // Inward normal (toward polygon centroid).
                    var nx = -uz;
                    var nz = ux;
                    var toMidX = midX - (ax + bx) * 0.5f;
                    var toMidZ = midZ - (az + bz) * 0.5f;
                    if (nx * toMidX + nz * toMidZ < 0f)
                    {
                        nx = -nx;
                        nz = -nz;
                    }

                    var steps = (int)(len / SpacingMetres);
                    for (var s = 1; s <= steps; s++)
                    {
                        var t = s / (float)(steps + 1);
                        var x = ax + ex * t + nx * EdgeInsetMetres;
                        var z = az + ez * t + nz * EdgeInsetMetres;
                        if (!InPrecinct(x, z) || FarFromCentre(x, z, r2) || OnRunwayStrip(x, z))
                            continue;
                        if (!InsidePolygon(start, end, x, z))
                            continue;
                        var yaw = (float)Math.Atan2(uz, ux);
                        var h = MinHeightMetres + Frac(x * 0.19f + z * 0.11f) * (MaxHeightMetres - MinHeightMetres);
                        raw.Add(new Site(x, z, yaw, h));
                    }
                }
            }

            // Stable order, then thin to MaxPalms by keeping every Nth after sort.
            raw.Sort((a, b) =>
            {
                var c = a.X.CompareTo(b.X);
                return c != 0 ? c : a.Z.CompareTo(b.Z);
            });

            // Drop sites that crowd closer than ~0.6× spacing.
            var thinned = new List<Site>(Math.Min(raw.Count, MaxPalms));
            var minDist2 = (SpacingMetres * 0.6f) * (SpacingMetres * 0.6f);
            for (var i = 0; i < raw.Count && thinned.Count < MaxPalms; i++)
            {
                var s = raw[i];
                var ok = true;
                for (var j = 0; j < thinned.Count; j++)
                {
                    var ddx = s.X - thinned[j].X;
                    var ddz = s.Z - thinned[j].Z;
                    if (ddx * ddx + ddz * ddz < minDist2)
                    {
                        ok = false;
                        break;
                    }
                }

                if (ok)
                    thinned.Add(s);
            }

            return thinned.ToArray();
        }

        public static bool InPrecinct(float x, float z) =>
            x >= PrecinctMinX && x <= PrecinctMaxX && z >= PrecinctMinZ && z <= PrecinctMaxZ;

        public static bool OnRunwayStrip(float x, float z) =>
            Math.Abs(x) < 1550f && Math.Abs(z) < 150f;

        private static bool FarFromCentre(float x, float z, float r2)
        {
            var dx = x - AdelaideCarParkGeometry.CentreX;
            var dz = z - AdelaideCarParkGeometry.CentreZ;
            return dx * dx + dz * dz > r2;
        }

        private static bool InsidePolygon(int start, int end, float x, float z)
        {
            var inside = false;
            for (int i = start, j = end - 1; i < end; j = i++)
            {
                var xi = AdelaideCarParks.Points[i * 2];
                var zi = AdelaideCarParks.Points[i * 2 + 1];
                var xj = AdelaideCarParks.Points[j * 2];
                var zj = AdelaideCarParks.Points[j * 2 + 1];
                if ((zi > z) != (zj > z) && x < (xj - xi) * (z - zi) / (zj - zi + 1e-12f) + xi)
                    inside = !inside;
            }

            return inside;
        }

        private static float Frac(float x)
        {
            x -= (float)Math.Floor(x);
            return x < 0f ? x + 1f : x;
        }
    }
}
