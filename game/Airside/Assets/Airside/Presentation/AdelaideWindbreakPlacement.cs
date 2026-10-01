using System;
using System.Collections.Generic;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>
    /// Deterministic eucalypt windbreak sites along Tapleys Hill Road (ADR 0216).
    /// Pure maths — landside verge only, outside the aerodrome boundary.
    /// </summary>
    public static class AdelaideWindbreakPlacement
    {
        public const string RoadName = "Tapleys Hill Road";
        public const int MaxTrees = 180;
        public const float SpacingMetres = 16f;
        public const float EdgeInsetMetres = 4f;
        public const float MinBoundaryDistMetres = 20f;
        public const float MaxBoundaryDistMetres = 220f;
        public const float FrontageMinX = -2600f;
        public const float FrontageMaxX = 400f;
        public const float FrontageMinZ = -900f;
        public const float FrontageMaxZ = 1500f;
        public const float MinHeightMetres = 7f;
        public const float MaxHeightMetres = 11f;

        private static Site[] _cached;
        private static short _nameIndex = short.MinValue;

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

        public static void ClearCache()
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
                    // Left-of-travel and right-of-travel normals.
                    var leftX = -uz;
                    var leftZ = ux;
                    var offset = road.Width * 0.5f + EdgeInsetMetres;

                    var steps = (int)(len / SpacingMetres);
                    for (var s = 1; s <= steps; s++)
                    {
                        var t = s / (float)(steps + 1);
                        var cx = ax + ex * t;
                        var cz = az + ez * t;
                        if (!InFrontage(cx, cz))
                            continue;

                        // Pick the side farther from the aerodrome (landside / west of fence).
                        var lx = cx + leftX * offset;
                        var lz = cz + leftZ * offset;
                        var rx = cx - leftX * offset;
                        var rz = cz - leftZ * offset;
                        var dL = DistanceToOutline(outline, lx, lz);
                        var dR = DistanceToOutline(outline, rx, rz);
                        var insideL = InsideOutline(outline, lx, lz);
                        var insideR = InsideOutline(outline, rx, rz);
                        float x, z, dist;
                        if (insideL && !insideR)
                        {
                            x = rx;
                            z = rz;
                            dist = dR;
                        }
                        else if (insideR && !insideL)
                        {
                            x = lx;
                            z = lz;
                            dist = dL;
                        }
                        else if (dL >= dR)
                        {
                            x = lx;
                            z = lz;
                            dist = dL;
                        }
                        else
                        {
                            x = rx;
                            z = rz;
                            dist = dR;
                        }

                        if (InsideOutline(outline, x, z))
                            continue;
                        if (dist < MinBoundaryDistMetres || dist > MaxBoundaryDistMetres)
                            continue;
                        if (!InFrontage(x, z) || OnRunwayStrip(x, z))
                            continue;

                        var h = MinHeightMetres + Frac(x * 0.17f + z * 0.09f) * (MaxHeightMetres - MinHeightMetres);
                        var crown = h * (0.32f + Frac(x * 0.05f - z * 0.03f) * 0.08f);
                        raw.Add(new Site(x, z, (float)Math.Atan2(uz, ux), h, crown));
                    }
                }
            }

            raw.Sort((a, b) =>
            {
                var c = a.X.CompareTo(b.X);
                return c != 0 ? c : a.Z.CompareTo(b.Z);
            });

            var thinned = new List<Site>(Math.Min(raw.Count, MaxTrees));
            var minDist2 = (SpacingMetres * 0.6f) * (SpacingMetres * 0.6f);
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

        public static bool InFrontage(float x, float z) =>
            x >= FrontageMinX && x <= FrontageMaxX && z >= FrontageMinZ && z <= FrontageMaxZ;

        public static bool OnRunwayStrip(float x, float z) =>
            Math.Abs(x) < 1550f && Math.Abs(z) < 150f;

        public static bool InsideOutline(float[] poly, float x, float z)
        {
            var inside = false;
            var n = poly.Length / 2;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                var xi = poly[i * 2];
                var zi = poly[i * 2 + 1];
                var xj = poly[j * 2];
                var zj = poly[j * 2 + 1];
                if ((zi > z) != (zj > z) && x < (xj - xi) * (z - zi) / (zj - zi + 1e-12f) + xi)
                    inside = !inside;
            }

            return inside;
        }

        public static float DistanceToOutline(float[] poly, float x, float z)
        {
            var best = float.MaxValue;
            var n = poly.Length / 2;
            for (var i = 0; i < n; i++)
            {
                var j = (i + 1) % n;
                var ax = poly[i * 2];
                var az = poly[i * 2 + 1];
                var dx = poly[j * 2] - ax;
                var dz = poly[j * 2 + 1] - az;
                var len2 = dx * dx + dz * dz;
                var t = len2 < 1e-9f ? 0f : Math.Max(0f, Math.Min(1f, ((x - ax) * dx + (z - az) * dz) / len2));
                var px = x - ax - dx * t;
                var pz = z - az - dz * t;
                var d = (float)Math.Sqrt(px * px + pz * pz);
                if (d < best)
                    best = d;
            }

            return best;
        }

        private static float Frac(float x)
        {
            x -= (float)Math.Floor(x);
            return x < 0f ? x + 1f : x;
        }
    }
}
