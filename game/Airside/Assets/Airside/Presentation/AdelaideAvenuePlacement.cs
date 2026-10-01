using System;
using System.Collections.Generic;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>
    /// Deterministic avenue eucalypts along major landside approaches (ADR 0217):
    /// Sir Donald Bradman Drive, Burbridge Road, Sir Richard Williams Avenue.
    /// Both verges; outside the aerodrome boundary. Pure maths.
    /// </summary>
    public static class AdelaideAvenuePlacement
    {
        public static readonly string[] RoadNames =
        {
            "Sir Donald Bradman Drive",
            "Burbridge Road",
            "Sir Richard Williams Avenue"
        };

        public const int MaxTrees = 220;
        public const float SpacingMetres = 18f;
        public const float EdgeInsetMetres = 5f;
        public const float MinBoundaryDistMetres = 5f;
        public const float CorridorMetres = 2800f;
        public const float MinHeightMetres = 8f;
        public const float MaxHeightMetres = 13f;

        private static Site[] _cached;
        private static short[] _nameIndices;

        public readonly struct Site
        {
            public readonly float X;
            public readonly float Z;
            public readonly float YawRad;
            public readonly float HeightMetres;
            public readonly float CrownRadius;

            public Site(float x, float z, float yawRad, float heightMetres, float crownRadius)
            {
                X = x;
                Z = z;
                YawRad = yawRad;
                HeightMetres = heightMetres;
                CrownRadius = crownRadius;
            }
        }

        public static short[] ResolveNameIndices()
        {
            if (_nameIndices != null)
                return _nameIndices;
            var found = new short[RoadNames.Length];
            for (var r = 0; r < RoadNames.Length; r++)
            {
                found[r] = -1;
                for (short i = 0; i < AdelaideRoadNetwork.Names.Length; i++)
                {
                    if (AdelaideRoadNetwork.Names[i] == RoadNames[r])
                    {
                        found[r] = i;
                        break;
                    }
                }
            }

            _nameIndices = found;
            return found;
        }

        public static Site[] Sites()
        {
            if (_cached != null)
                return _cached;
            _cached = ComputeSites();
            return _cached;
        }

        public static void ClearCache()
        {
            _cached = null;
            _nameIndices = null;
        }

        private static bool IsTargetName(short nameIndex, short[] indices)
        {
            for (var i = 0; i < indices.Length; i++)
                if (indices[i] == nameIndex)
                    return true;
            return false;
        }

        private static Site[] ComputeSites()
        {
            var indices = ResolveNameIndices();
            var raw = new List<Site>(MaxTrees * 2);
            var outline = AdelaideBoundary.Outline;
            var windbreak = new HashSet<long>();
            // Soft de-dupe vs Tapleys windbreak (~half spacing).
            foreach (var w in AdelaideWindbreakPlacement.Sites())
                windbreak.Add(CellKey(w.X, w.Z));

            foreach (var road in AdelaideRoadNetwork.Roads)
            {
                if (road.NameIndex < 0 || !IsTargetName(road.NameIndex, indices) || road.IsAirside || road.PointCount < 2)
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

                        // Both verges; keep only landside (outside boundary).
                        TryAdd(raw, outline, windbreak, cx + leftX * offset, cz + leftZ * offset, ux, uz);
                        TryAdd(raw, outline, windbreak, cx - leftX * offset, cz - leftZ * offset, ux, uz);
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

        private static void TryAdd(List<Site> raw, float[] outline, HashSet<long> windbreak,
            float x, float z, float ux, float uz)
        {
            if (!InCorridor(x, z) || OnRunwayStrip(x, z))
                return;
            if (AdelaideWindbreakPlacement.InsideOutline(outline, x, z))
                return;
            var d = AdelaideWindbreakPlacement.DistanceToOutline(outline, x, z);
            if (d < MinBoundaryDistMetres)
                return;
            if (windbreak.Contains(CellKey(x, z)))
                return;
            var h = MinHeightMetres + Frac(x * 0.13f + z * 0.07f) * (MaxHeightMetres - MinHeightMetres);
            var crown = h * (0.30f + Frac(x * 0.04f - z * 0.02f) * 0.08f);
            raw.Add(new Site(x, z, (float)Math.Atan2(uz, ux), h, crown));
        }

        public static bool InCorridor(float x, float z) =>
            Math.Abs(x) <= CorridorMetres && Math.Abs(z) <= CorridorMetres;

        public static bool OnRunwayStrip(float x, float z) =>
            Math.Abs(x) < 1550f && Math.Abs(z) < 150f;

        private static long CellKey(float x, float z)
        {
            var ix = (int)Math.Floor(x / 8f);
            var iz = (int)Math.Floor(z / 8f);
            return ((long)ix << 32) ^ (uint)iz;
        }

        private static float Frac(float x)
        {
            x -= (float)Math.Floor(x);
            return x < 0f ? x + 1f : x;
        }
    }
}
