using System;
using System.Collections.Generic;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>
    /// Deterministic coastal tea-tree/scrub sites on West Beach dunes (ADR 0218).
    /// OSM <see cref="AdelaideLandCover.Kind.Sand"/> cells in the coast-landform dune band.
    /// Pure maths — drawn into the road props sink.
    /// </summary>
    public static class AdelaideDuneScrubPlacement
    {
        public const int MaxBushes = 250;
        public const float SpacingMetres = 12f;
        public const float WestMaxX = -800f;
        public const float WestMinX = -3600f;
        public const float CorridorMinZ = -1800f;
        public const float CorridorMaxZ = 2200f;
        public const float MinHeightMetres = 1.2f;
        public const float MaxHeightMetres = 2.2f;
        public const float MinRadiusMetres = 1.0f;
        public const float MaxRadiusMetres = 1.8f;

        private static Site[] _cached;

        public readonly struct Site
        {
            public readonly float X;
            public readonly float Z;
            public readonly float YawRad;
            public readonly float HeightMetres;
            public readonly float RadiusMetres;

            public Site(float x, float z, float yawRad, float heightMetres, float radiusMetres)
            {
                X = x;
                Z = z;
                YawRad = yawRad;
                HeightMetres = heightMetres;
                RadiusMetres = radiusMetres;
            }
        }

        public static Site[] Sites()
        {
            if (_cached != null)
                return _cached;
            _cached = ComputeSites();
            return _cached;
        }

        public static void ClearCache() => _cached = null;

        private static Site[] ComputeSites()
        {
            var raw = new List<Site>(MaxBushes * 2);
            var outline = AdelaideBoundary.Outline;
            var step = SpacingMetres;
            for (var x = WestMinX; x <= WestMaxX; x += step)
            for (var z = CorridorMinZ; z <= CorridorMaxZ; z += step)
            {
                // Jitter inside the cell so the lattice does not read as a grid.
                var jx = x + (Frac(x * 0.31f + z * 0.17f) - 0.5f) * step * 0.7f;
                var jz = z + (Frac(x * 0.19f - z * 0.23f) - 0.5f) * step * 0.7f;
                if (jx < WestMinX || jx > WestMaxX || jz < CorridorMinZ || jz > CorridorMaxZ)
                    continue;
                if (AdelaideLandCover.Sample(jx, jz) != AdelaideLandCover.Kind.Sand)
                    continue;
                var coast = AdelaideCoastLandform.CoastDistanceMetres(jx, jz);
                if (coast < AdelaideCoastLandform.DuneBandStartMetres
                    || coast > AdelaideCoastLandform.DuneBandEndMetres)
                    continue;
                if (AdelaideWindbreakPlacement.InsideOutline(outline, jx, jz))
                    continue;
                if (OnRunwayStrip(jx, jz))
                    continue;

                var h = MinHeightMetres + Frac(jx * 0.11f + jz * 0.07f) * (MaxHeightMetres - MinHeightMetres);
                var r = MinRadiusMetres + Frac(jx * 0.05f - jz * 0.09f) * (MaxRadiusMetres - MinRadiusMetres);
                var yaw = Frac(jx * 0.41f + jz * 0.13f) * (float)(Math.PI * 2.0);
                raw.Add(new Site(jx, jz, yaw, h, r));
            }

            raw.Sort((a, b) =>
            {
                var c = a.X.CompareTo(b.X);
                return c != 0 ? c : a.Z.CompareTo(b.Z);
            });

            var thinned = new List<Site>(Math.Min(raw.Count, MaxBushes));
            var minDist2 = (SpacingMetres * 0.55f) * (SpacingMetres * 0.55f);
            for (var i = 0; i < raw.Count && thinned.Count < MaxBushes; i++)
            {
                var s = raw[i];
                var ok = true;
                for (var j = 0; j < thinned.Count; j++)
                {
                    var dx = s.X - thinned[j].X;
                    var dz = s.Z - thinned[j].Z;
                    if (dx * dx + dz * dz < minDist2)
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

        public static bool OnRunwayStrip(float x, float z) =>
            Math.Abs(x) < 1550f && Math.Abs(z) < 150f;

        private static float Frac(float x)
        {
            x -= (float)Math.Floor(x);
            return x < 0f ? x + 1f : x;
        }
    }
}
