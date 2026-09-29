using System;
using System.Collections.Generic;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>
    /// ADR 0184 — Adelaide Airport's precinct furniture as flat geometry from <see cref="AdelaidePrecinct"/>: open
    /// canopies on posts, solar arrays, storage tanks, floodlight and navaid masts, bus stops and footpaths. Pure (no
    /// UnityEngine) so the headless harness checks it.
    /// </summary>
    public static class AdelaidePrecinctGeometry
    {
        public const float PostSize = 0.22f;
        public const float MaxPostSpacing = 14f;

        private static readonly RoadColor RoofSlab = RoadColor.Srgb(0.74f, 0.75f, 0.73f, 1f);
        private static readonly RoadColor Post = RoadColor.Srgb(0.46f, 0.47f, 0.49f, 1f);
        private static readonly RoadColor Panel = RoadColor.Srgb(0.09f, 0.13f, 0.25f, 1f);
        private static readonly RoadColor TankPaint = RoadColor.Srgb(0.86f, 0.87f, 0.85f, 1f);
        private static readonly RoadColor MastPaint = RoadColor.Srgb(0.58f, 0.59f, 0.60f, 1f);
        private static readonly RoadColor MastHead = RoadColor.Srgb(0.22f, 0.23f, 0.25f, 1f);
        private static readonly RoadColor Shelter = RoadColor.Srgb(0.30f, 0.44f, 0.36f, 1f);
        private static readonly RoadColor ShelterGlass = RoadColor.Srgb(0.42f, 0.52f, 0.58f, 1f);
        private static readonly RoadColor SignPlate = RoadColor.Srgb(0.85f, 0.72f, 0.16f, 1f);

        // --- canopies and solar ---

        /// <summary>Flat roofs on posts (taxi and bus ranks, car-park entrances). Returns canopies drawn.</summary>
        public static int BuildCanopies(RoadMeshSink sink, RoadBuildOptions o)
        {
            for (var c = 0; c < AdelaidePrecinct.CanopyCount; c++)
            {
                var xz = Polygon(AdelaidePrecinct.CanopyStarts, AdelaidePrecinct.CanopyPoints, c);
                var h = AdelaidePrecinct.CanopyHeights[c];
                Slab(sink, o, xz, h, RoofSlab);
                var n = xz.Count / 2;
                for (var i = 0; i < n; i++)
                {
                    var j = (i + 1) % n;
                    var ax = xz[i * 2];
                    var az = xz[i * 2 + 1];
                    var bx = xz[j * 2];
                    var bz = xz[j * 2 + 1];
                    var len = (float)Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
                    var pieces = Math.Max(1, (int)Math.Ceiling(len / MaxPostSpacing));
                    for (var k = 0; k < pieces; k++)
                    {
                        var t = (float)k / pieces;
                        var px = ax + (bx - ax) * t;
                        var pz = az + (bz - az) * t;
                        sink.Box(px, o.Height(px, pz) + o.YOffset, pz, 1f, 0f, PostSize, PostSize, h, Post);
                    }
                }
            }

            return AdelaidePrecinct.CanopyCount;
        }

        /// <summary>Photovoltaic arrays: dark blue panels, raised a little on the ground or set on the canopy roof.</summary>
        public static int BuildSolar(RoadMeshSink sink, RoadBuildOptions o)
        {
            for (var s = 0; s < AdelaidePrecinct.SolarCount; s++)
            {
                var xz = Polygon(AdelaidePrecinct.SolarStarts, AdelaidePrecinct.SolarPoints, s);
                var onRoof = AdelaidePrecinct.SolarOnRoof[s] > 0.5f;
                // Roof arrays sit on their canopy; ground arrays stand about a metre up on frames.
                var height = onRoof ? RoofHeightUnder(xz) + 0.14f : 1.0f;
                Slab(sink, o, xz, height, Panel);
            }

            return AdelaidePrecinct.SolarCount;
        }

        private static float RoofHeightUnder(List<float> xz)
        {
            var cx = 0f;
            var cz = 0f;
            var n = xz.Count / 2;
            for (var i = 0; i < n; i++)
            {
                cx += xz[i * 2];
                cz += xz[i * 2 + 1];
            }

            cx /= n;
            cz /= n;
            for (var c = 0; c < AdelaidePrecinct.CanopyCount; c++)
                if (Contains(Polygon(AdelaidePrecinct.CanopyStarts, AdelaidePrecinct.CanopyPoints, c), cx, cz))
                    return AdelaidePrecinct.CanopyHeights[c];
            return 4.6f;
        }

        private static void Slab(RoadMeshSink sink, RoadBuildOptions o, List<float> xz, float height, RoadColor colour)
        {
            foreach (var t in AdelaideCarParkGeometry.Triangulate(xz))
            {
                float Y(int k) => o.Height(xz[t[k] * 2], xz[t[k] * 2 + 1]) + o.YOffset + height;
                sink.Tri(xz[t[0] * 2], Y(0), xz[t[0] * 2 + 1], xz[t[1] * 2], Y(1), xz[t[1] * 2 + 1],
                    xz[t[2] * 2], Y(2), xz[t[2] * 2 + 1], colour);
            }
        }

        /// <summary>
        /// True when a suburb-extract prism is really one of the open canopies (it must not draw as a solid block):
        /// most of its corners lie inside a canopy outline or within 1.5 m of its edge.
        /// </summary>
        public static bool IsCanopyFootprint(float[] footprintXz)
        {
            if (footprintXz == null || footprintXz.Length < 6)
                return false;
            var n = footprintXz.Length / 2;
            for (var c = 0; c < AdelaidePrecinct.CanopyCount; c++)
            {
                var poly = Polygon(AdelaidePrecinct.CanopyStarts, AdelaidePrecinct.CanopyPoints, c);
                var near = 0;
                for (var i = 0; i < n; i++)
                {
                    var x = footprintXz[i * 2];
                    var z = footprintXz[i * 2 + 1];
                    if (Contains(poly, x, z) || EdgeDistance(poly, x, z) < 1.5f)
                        near++;
                }

                if (near * 2 > n)
                    return true;
            }

            return false;
        }

        private static float EdgeDistance(List<float> xz, float x, float z)
        {
            var n = xz.Count / 2;
            var best = float.MaxValue;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float ax = xz[j * 2], az = xz[j * 2 + 1], bx = xz[i * 2], bz = xz[i * 2 + 1];
                var vx = bx - ax;
                var vz = bz - az;
                var l2 = vx * vx + vz * vz;
                var t = l2 <= 0f ? 0f : Math.Max(0f, Math.Min(1f, ((x - ax) * vx + (z - az) * vz) / l2));
                var dx = x - (ax + vx * t);
                var dz = z - (az + vz * t);
                best = Math.Min(best, (float)Math.Sqrt(dx * dx + dz * dz));
            }

            return best;
        }

        // --- tanks and masts ---

        public static int BuildTanks(RoadMeshSink sink, RoadBuildOptions o)
        {
            var t = AdelaidePrecinct.Tanks;
            for (var i = 0; i + 3 < t.Length; i += 4)
                sink.Cylinder(t[i], o.Height(t[i], t[i + 1]) + o.YOffset, t[i + 1], t[i + 2], t[i + 3], 14, TankPaint);
            return AdelaidePrecinct.TankCount;
        }

        /// <summary>Floodlight masts with a lamp head, communication masts, and low navaid masts on a cabinet.</summary>
        public static int BuildMasts(RoadMeshSink sink, RoadBuildOptions o)
        {
            var m = AdelaidePrecinct.Masts;
            for (var i = 0; i + 3 < m.Length; i += 4)
            {
                var x = m[i];
                var z = m[i + 1];
                var kind = (int)m[i + 2];
                var h = m[i + 3];
                var y = o.Height(x, z) + o.YOffset;
                switch (kind)
                {
                    case 0:
                        sink.Box(x, y, z, 1f, 0f, 0.28f, 0.28f, h, MastPaint);
                        sink.Box(x, y + h, z, 1f, 0f, 1.6f, 0.5f, 0.9f, MastHead);
                        break;
                    case 1:
                        sink.Box(x, y, z, 1f, 0f, 0.4f, 0.4f, h, MastPaint);
                        sink.Box(x, y + h, z, 1f, 0f, 0.9f, 0.9f, 1.2f, MastHead);
                        break;
                    default:
                        sink.Box(x, y, z, 1f, 0f, 1.2f, 1.2f, 2.4f, MastPaint);
                        sink.Box(x, y + 2.4f, z, 1f, 0f, 0.12f, 0.12f, h, MastHead);
                        break;
                }
            }

            return AdelaidePrecinct.MastCount;
        }

        // --- bus stops ---

        public static int BuildBusStops(RoadMeshSink sink, RoadBuildOptions o)
        {
            var b = AdelaidePrecinct.BusStops;
            for (var i = 0; i + 3 < b.Length; i += 4)
            {
                var x = b[i];
                var z = b[i + 1];
                var yaw = b[i + 2] * (float)Math.PI / 180f;
                var ux = (float)Math.Cos(yaw);
                var uz = (float)Math.Sin(yaw);
                var y = o.Height(x, z) + o.YOffset;
                if (b[i + 3] > 0.5f)
                {
                    // a shelter: two posts, a roof, a glass back panel on the far side from the road
                    var nx = -uz;
                    var nz = ux;
                    sink.Box(x, y + 2.4f, z, ux, uz, 1.6f, 0.75f, 0.12f, Shelter);
                    sink.Box(x - ux * 1.4f, y, z - uz * 1.4f, ux, uz, 0.06f, 0.06f, 2.4f, Post);
                    sink.Box(x + ux * 1.4f, y, z + uz * 1.4f, ux, uz, 0.06f, 0.06f, 2.4f, Post);
                    sink.Box(x + nx * 0.7f, y + 0.1f, z + nz * 0.7f, ux, uz, 1.5f, 0.03f, 1.9f, ShelterGlass);
                }
                else
                {
                    sink.Box(x, y, z, ux, uz, 0.04f, 0.04f, 2.6f, Post);
                    sink.Box(x, y + 2.1f, z, ux, uz, 0.02f, 0.28f, 0.5f, SignPlate);
                }
            }

            return AdelaidePrecinct.BusStopCount;
        }

        // --- footpaths ---

        /// <summary>Footpaths, sidewalks, cycleways and steps as light ribbons under the road paint.</summary>
        public static int BuildPaths(RoadMeshSink sink, RoadBuildOptions o)
        {
            var drawn = 0;
            var lowered = new RoadBuildOptions
            {
                GroundHeight = o.GroundHeight,
                BaseY = o.BaseY,
                YOffset = o.YOffset * 0.75f,
                AirsideRule = null,
                MaxSegmentMetres = o.MaxSegmentMetres
            };
            for (var p = 0; p < AdelaidePrecinct.PathCount; p++)
            {
                var start = AdelaidePrecinct.PathStarts[p];
                var end = AdelaidePrecinct.PathStarts[p + 1];
                var kind = (int)AdelaidePrecinct.PathInfo[p * 2];
                var width = AdelaidePrecinct.PathInfo[p * 2 + 1];
                var raw = new List<float>((end - start) * 2);
                for (var i = start; i < end; i++)
                {
                    raw.Add(AdelaidePrecinct.PathPoints[i * 2]);
                    raw.Add(AdelaidePrecinct.PathPoints[i * 2 + 1]);
                }

                var pts = AdelaideRoadGeometry.DropTiny(DensifyList(raw, o.MaxSegmentMetres), AdelaideRoadGeometry.MinSegmentMetres);
                if (pts.Count < 4)
                    continue;
                var mid = pts.Count / 4 * 2;
                if (AdelaideRoadGeometry.RibbonFromPoints(sink, lowered, pts, width, PathColour(kind, pts[mid], pts[mid + 1]), false))
                    drawn++;
            }

            return drawn;
        }

        private static RoadColor PathColour(int kind, float x, float z)
        {
            var a = AdelaideRoadGeometry.AlphaAt(x, z);
            switch (kind)
            {
                case 1: return RoadColor.Srgb(0.50f, 0.36f, 0.32f, a);
                case 2: return RoadColor.Srgb(0.56f, 0.51f, 0.42f, a);
                case 3: return RoadColor.Srgb(0.68f, 0.66f, 0.62f, a);
                case 4: return RoadColor.Srgb(0.62f, 0.62f, 0.61f, a);
                default: return RoadColor.Srgb(0.64f, 0.63f, 0.60f, a);
            }
        }

        // --- holding-position signs ---

        private static readonly RoadColor MandatoryRed = RoadColor.Srgb(0.72f, 0.07f, 0.07f, 1f);
        private static readonly RoadColor SignWhite = RoadColor.Srgb(0.93f, 0.93f, 0.9f, 1f);
        public const float HoldSignSetbackMetres = 4f;

        /// <summary>
        /// A mandatory red sign each side of every holding position, facing along the taxiway: what a pilot reads
        /// at the hold bars. Returns signs placed.
        /// </summary>
        public static int BuildHoldSigns(RoadMeshSink sink, RoadBuildOptions o)
        {
            var h = AdelaideLayout.HoldingPositions;
            var placed = 0;
            for (var i = 0; i + 1 < h.Length; i += 2)
            {
                NearestTaxiway(h[i], h[i + 1], out var dx, out var dz, out var width);
                var ax = -dz;
                var az = dx;
                foreach (var side in new[] { -1f, 1f })
                {
                    var reach = width * 0.5f + HoldSignSetbackMetres;
                    var x = h[i] + ax * reach * side;
                    var z = h[i + 1] + az * reach * side;
                    var y = o.Height(x, z) + o.YOffset;
                    sink.Box(x, y, z, ax, az, 0.05f, 0.05f, 1.0f, Post);
                    sink.Box(x, y + 1.0f, z, ax, az, 1.2f, 0.05f, 0.9f, SignWhite);
                    sink.Box(x, y + 1.05f, z, ax, az, 1.1f, 0.06f, 0.8f, MandatoryRed);
                    placed++;
                }
            }

            return placed;
        }

        /// <summary>Direction (unit) and width of the taxiway segment nearest (x, z).</summary>
        public static void NearestTaxiway(float x, float z, out float dx, out float dz, out float width)
        {
            dx = 1f;
            dz = 0f;
            width = 23f;
            var best = float.MaxValue;
            foreach (var taxiway in AdelaideLayout.Taxiways)
            {
                var xz = taxiway.Xz;
                for (var i = 0; i + 3 < xz.Length; i += 2)
                {
                    var vx = xz[i + 2] - xz[i];
                    var vz = xz[i + 3] - xz[i + 1];
                    var l2 = vx * vx + vz * vz;
                    if (l2 < 1e-4f)
                        continue;
                    var t = Math.Max(0f, Math.Min(1f, ((x - xz[i]) * vx + (z - xz[i + 1]) * vz) / l2));
                    var ex = x - (xz[i] + vx * t);
                    var ez = z - (xz[i + 1] + vz * t);
                    var d = ex * ex + ez * ez;
                    if (d >= best)
                        continue;
                    best = d;
                    var l = (float)Math.Sqrt(l2);
                    dx = vx / l;
                    dz = vz / l;
                    width = taxiway.Width;
                }
            }
        }

        // --- helpers ---

        public static List<float> Polygon(int[] starts, float[] points, int index)
        {
            var list = new List<float>((starts[index + 1] - starts[index]) * 2);
            for (var i = starts[index]; i < starts[index + 1]; i++)
            {
                list.Add(points[i * 2]);
                list.Add(points[i * 2 + 1]);
            }

            return list;
        }

        public static bool Contains(List<float> xz, float x, float z)
        {
            var n = xz.Count / 2;
            var inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float xi = xz[i * 2], zi = xz[i * 2 + 1], xj = xz[j * 2], zj = xz[j * 2 + 1];
                if ((zi > z) != (zj > z) && x < (xj - xi) * (z - zi) / (zj - zi) + xi)
                    inside = !inside;
            }

            return inside;
        }

        private static List<float> DensifyList(List<float> xz, float maxSegment)
        {
            var result = new List<float>(xz.Count) { xz[0], xz[1] };
            for (var i = 1; i < xz.Count / 2; i++)
            {
                var px = result[result.Count - 2];
                var pz = result[result.Count - 1];
                var x = xz[i * 2];
                var z = xz[i * 2 + 1];
                var len = (float)Math.Sqrt((x - px) * (x - px) + (z - pz) * (z - pz));
                var pieces = maxSegment < len ? (int)Math.Ceiling(len / maxSegment) : 1;
                for (var k = 1; k < pieces; k++)
                {
                    var t = (float)k / pieces;
                    result.Add(px + (x - px) * t);
                    result.Add(pz + (z - pz) * t);
                }

                result.Add(x);
                result.Add(z);
            }

            return result;
        }
    }
}
