using System;
using System.Collections.Generic;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>
    /// Deterministic Norfolk Island pine sites along Henley Beach Road (ADR 0219).
    /// Distinctive coastal avenue species for Glenelg/Henley. Pure maths.
    /// </summary>
    public static class AdelaideNorfolkPinePlacement
    {
        public const string RoadName = "Henley Beach Road";
        public const int MaxTrees = 120;
        public const float SpacingMetres = 22f;
        public const float EdgeInsetMetres = 5.5f;
        public const float CorridorMetres = 3200f;
        public const float MinHeightMetres = 12f;
        public const float MaxHeightMetres = 18f;
        public const float MinBoundaryDistMetres = 5f;

        private static Site[] _cached;
        private static short _nameIndex = short.MinValue;

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

        public static short ResolveNameIndex()
        {
            if (_nameIndex != short.MinValue)
                return _nameIndex;
            for (short i = 0; i < AdelaideRoadNetwork.Names.Length; i++)
            {
                if (AdelaideRoadNetwork.Names[i] == RoadName)
                {
                    _nameIndex = i;
                    return i;
                }
            }

            _nameIndex = -1;
            return -1;
        }

        public static Site[] Sites()
        {
            if (_cached != null)
                return _cached;
            _cached = ComputeSites();
            return _cached;
        }

        internal static void ClearCache()
        {
            _cached = null;
            _nameIndex = short.MinValue;
        }

        private static Site[] ComputeSites()
        {
            var nameIndex = ResolveNameIndex();
            var raw = new List<Site>(MaxTrees * 2);
            if (nameIndex < 0)
                return Array.Empty<Site>();

            var outline = AdelaideBoundary.Outline;
            foreach (var road in AdelaideRoadNetwork.Roads)
            {
                if (road.NameIndex != nameIndex || road.IsAirside || road.PointCount < 2)
                    continue;

                for (var i = 0; i + 1 < road.PointCount; i++)
                {
                    var i0 = road.PointStart + i;
                    var i1 = road.PointStart + i + 1;
                    var ax = AdelaideRoadNetwork.Points[i0 * 2];
                    var az = AdelaideRoadNetwork.Points[i0 * 2 + 1];
                    var bx = AdelaideRoadNetwork.Points[i1 * 2];
                    var bz = AdelaideRoadNetwork.Points[i1 * 2 + 1];
                    var ex = bx - ax;
                    var ez = bz - az;
                    var len = (float)Math.Sqrt(ex * ex + ez * ez);
                    if (len < SpacingMetres * 0.5f)
                        continue;
                    var ux = ex / len;
                    var uz = ez / len;
                    var leftX = -uz;
                    var leftZ = ux;
                    var offset = road.Width * 0.5f + EdgeInsetMetres;
                    var steps = (int)(len / SpacingMetres);
                    for (var s = 1; s <= steps; s++)
                    {
                        var t = s / (float)(steps + 1);
                        var cx = ax + ex * t;
                        var cz = az + ez * t;
                        if (!InCorridor(cx, cz))
                            continue;
                        TryAdd(raw, outline, cx + leftX * offset, cz + leftZ * offset, ux, uz);
                        TryAdd(raw, outline, cx - leftX * offset, cz - leftZ * offset, ux, uz);
                    }
                }
            }

            raw.Sort((a, b) =>
            {
                var c = a.X.CompareTo(b.X);
                return c != 0 ? c : a.Z.CompareTo(b.Z);
            });

            var thinned = new List<Site>(Math.Min(raw.Count, MaxTrees));
            var minDist2 = (SpacingMetres * 0.55f) * (SpacingMetres * 0.55f);
            for (var i = 0; i < raw.Count && thinned.Count < MaxTrees; i++)
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

        private static void TryAdd(List<Site> raw, float[] outline, float x, float z, float ux, float uz)
        {
            if (!InCorridor(x, z) || OnRunwayStrip(x, z))
                return;
            if (AdelaideWindbreakPlacement.InsideOutline(outline, x, z))
                return;
            if (AdelaideWindbreakPlacement.DistanceToOutline(outline, x, z) < MinBoundaryDistMetres)
                return;
            var h = MinHeightMetres + Frac(x * 0.09f + z * 0.15f) * (MaxHeightMetres - MinHeightMetres);
            raw.Add(new Site(x, z, (float)Math.Atan2(uz, ux), h));
        }

        public static bool InCorridor(float x, float z) =>
            Math.Abs(x) <= CorridorMetres && Math.Abs(z) <= CorridorMetres;

        public static bool OnRunwayStrip(float x, float z) =>
            Math.Abs(x) < 1550f && Math.Abs(z) < 150f;

        private static float Frac(float v)
        {
            v -= (float)Math.Floor(v);
            return v < 0f ? v + 1f : v;
        }
    }
}
