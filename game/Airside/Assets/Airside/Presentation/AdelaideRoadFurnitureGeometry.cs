using System;
using System.Collections.Generic;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>
    /// ADR 0184/0186 — intersection furniture from <see cref="AdelaideRoadNetwork.Furniture"/>: traffic signals (a
    /// footed pole, mast arm, backed three-lamp head and visors for each approach, with the pole on the left kerb as
    /// Australian traffic keeps left), stop lines at signals and stop signs, give-way "shark teeth", and stop and
    /// give-way signs. Pure (no UnityEngine), so the headless harness checks it. A road's heading is the direction of
    /// travel the node applies to (<c>direction=backward</c> flips it).
    /// </summary>
    public static class AdelaideRoadFurnitureGeometry
    {
        public const float StopLineSetbackMetres = 1.5f;
        public const float PoleHeightMetres = 3.4f;
        public const float SignalSetbackMetres = 3.0f;
        public const float KerbOffsetMetres = 0.9f;
        public const float MastReachMetres = 1.6f;

        private static readonly RoadColor Pole = RoadColor.Srgb(0.30f, 0.31f, 0.33f, 1f);
        private static readonly RoadColor Concrete = RoadColor.Srgb(0.54f, 0.55f, 0.55f, 1f);
        private static readonly RoadColor HeadBody = RoadColor.Srgb(0.10f, 0.10f, 0.11f, 1f);
        private static readonly RoadColor RedLit = RoadColor.Srgb(1.00f, 0.12f, 0.06f, 1f);
        private static readonly RoadColor RedDim = RoadColor.Srgb(0.30f, 0.05f, 0.04f, 1f);
        private static readonly RoadColor AmberDim = RoadColor.Srgb(0.32f, 0.22f, 0.04f, 1f);
        private static readonly RoadColor GreenLit = RoadColor.Srgb(0.10f, 1.00f, 0.32f, 1f);
        private static readonly RoadColor GreenDim = RoadColor.Srgb(0.04f, 0.24f, 0.10f, 1f);
        private static readonly RoadColor White = RoadColor.Srgb(0.92f, 0.92f, 0.90f, 1f);
        private static readonly RoadColor SignRed = RoadColor.Srgb(0.72f, 0.06f, 0.06f, 1f);

        /// <summary>One signal assembly: kerbside pole, hanging head, facing direction and lit state.</summary>
        public readonly struct Head
        {
            public Head(float poleX, float poleZ, float x, float z, float faceX, float faceZ, bool redLit)
            {
                PoleX = poleX;
                PoleZ = poleZ;
                X = x;
                Z = z;
                FaceX = faceX;
                FaceZ = faceZ;
                RedLit = redLit;
            }

            public float PoleX { get; }
            public float PoleZ { get; }
            public float X { get; }
            public float Z { get; }
            public float FaceX { get; }
            public float FaceZ { get; }
            public bool RedLit { get; }
        }

        private static IEnumerable<int> Nodes(AdelaideRoadNetwork.FurnitureKind kind)
        {
            var f = AdelaideRoadNetwork.Furniture;
            var s = AdelaideRoadNetwork.FurnitureStride;
            for (var k = 0; k + s <= f.Length; k += s)
                if ((int)f[k] == (int)kind && f[k + 4] > 0f)
                    yield return k;
        }

        private static void Frame(int k, out float x, out float z, out float tx, out float tz, out float width, out bool oneWay,
            out float axisDegrees)
        {
            var f = AdelaideRoadNetwork.Furniture;
            x = f[k + 1];
            z = f[k + 2];
            axisDegrees = f[k + 3];
            width = f[k + 4];
            var h = f[k + 5] * (float)Math.PI / 180f;
            tx = (float)Math.Cos(h);
            tz = (float)Math.Sin(h);
            oneWay = ((int)f[k + 6] & 1) != 0;
        }

        /// <summary>Every signal head (for the night glows): two-way roads get one per direction, one-way roads one.</summary>
        public static List<Head> SignalHeads()
        {
            var heads = new List<Head>();
            foreach (var k in Nodes(AdelaideRoadNetwork.FurnitureKind.TrafficSignal))
            {
                Frame(k, out var x, out var z, out var tx, out var tz, out var width, out var oneWay, out var axis);
                // Approaches on one axis show green while the cross axis shows red.
                var red = ((int)(axis / 90f) & 1) == 1;
                for (var dir = 0; dir < (oneWay ? 1 : 2); dir++)
                {
                    var d = dir == 0 ? 1f : -1f;
                    var ax = tx * d;
                    var az = tz * d;
                    var lx = -az;
                    var lz = ax;
                    var poleX = x - ax * SignalSetbackMetres + lx * (width * 0.5f + KerbOffsetMetres);
                    var poleZ = z - az * SignalSetbackMetres + lz * (width * 0.5f + KerbOffsetMetres);
                    // From the left kerb, the arm reaches right toward the approach lane. The head coordinates are
                    // also consumed by the night-glow builder, keeping the emissive glow on the physical lamps.
                    var faceX = -ax;
                    var faceZ = -az;
                    var armX = -faceZ;
                    var armZ = faceX;
                    heads.Add(new Head(poleX, poleZ, poleX + armX * MastReachMetres,
                        poleZ + armZ * MastReachMetres, faceX, faceZ, red));
                }
            }

            return heads;
        }

        /// <summary>A footed pole, mast arm, backed head, three lamps and visors per approach.</summary>
        public static int BuildSignals(RoadMeshSink sink, RoadBuildOptions o)
        {
            var heads = SignalHeads();
            foreach (var h in heads)
            {
                var y = o.Height(h.PoleX, h.PoleZ) + o.YOffset;
                sink.Cylinder(h.PoleX, y, h.PoleZ, 0.22f, 0.16f, 8, Concrete);
                sink.Box(h.PoleX, y + 0.16f, h.PoleZ, 1f, 0f, 0.07f, 0.07f, PoleHeightMetres - 0.16f, Pole);
                sink.Box(h.PoleX, y + 0.55f, h.PoleZ, h.FaceX, h.FaceZ, 0.12f, 0.18f, 0.42f, HeadBody);
                var armX = -h.FaceZ;
                var armZ = h.FaceX;
                sink.Box((h.PoleX + h.X) * 0.5f, y + PoleHeightMetres - 0.12f,
                    (h.PoleZ + h.Z) * 0.5f, armX, armZ, MastReachMetres * 0.5f, 0.065f, 0.12f, Pole);
                var headBase = y + PoleHeightMetres - 1.05f;
                // A slightly oversized backing board makes the head readable against road and foliage.
                sink.Box(h.X - h.FaceX * 0.08f, headBase - 0.08f, h.Z - h.FaceZ * 0.08f,
                    h.FaceX, h.FaceZ, 0.08f, 0.27f, 1.16f, Pole);
                sink.Box(h.X, headBase, h.Z, h.FaceX, h.FaceZ, 0.16f, 0.2f, 1.0f, HeadBody);
                var lx = h.X + h.FaceX * 0.17f;
                var lz = h.Z + h.FaceZ * 0.17f;
                SignalLamp(sink, lx, headBase + 0.06f, lz, h, h.RedLit ? GreenDim : GreenLit);
                SignalLamp(sink, lx, headBase + 0.39f, lz, h, AmberDim);
                SignalLamp(sink, lx, headBase + 0.72f, lz, h, h.RedLit ? RedLit : RedDim);
            }

            return heads.Count;
        }

        private static void SignalLamp(RoadMeshSink sink, float x, float y, float z, Head h, RoadColor colour)
        {
            sink.Box(x, y, z, h.FaceX, h.FaceZ, 0.03f, 0.09f, 0.22f, colour);
            sink.Box(x + h.FaceX * 0.08f, y + 0.22f, z + h.FaceZ * 0.08f,
                h.FaceX, h.FaceZ, 0.12f, 0.11f, 0.05f, HeadBody);
        }

        /// <summary>Stop lines at signals (each direction) and stop signs, and give-way teeth. Returns marks drawn.</summary>
        public static int BuildRoadPaint(RoadMeshSink sink, RoadBuildOptions o)
        {
            var count = 0;
            foreach (var k in Nodes(AdelaideRoadNetwork.FurnitureKind.TrafficSignal))
            {
                Frame(k, out var x, out var z, out var tx, out var tz, out var width, out var oneWay, out _);
                for (var dir = 0; dir < (oneWay ? 1 : 2); dir++)
                {
                    var d = dir == 0 ? 1f : -1f;
                    StopLine(sink, o, x - tx * d * StopLineSetbackMetres, z - tz * d * StopLineSetbackMetres, tx * d, tz * d, width);
                    count++;
                }
            }

            foreach (var k in Nodes(AdelaideRoadNetwork.FurnitureKind.Stop))
            {
                Frame(k, out var x, out var z, out var tx, out var tz, out var width, out _, out _);
                StopLine(sink, o, x, z, tx, tz, width);
                count++;
            }

            foreach (var k in Nodes(AdelaideRoadNetwork.FurnitureKind.GiveWay))
            {
                Frame(k, out var x, out var z, out var tx, out var tz, out var width, out _, out _);
                GiveWayTeeth(sink, o, x, z, tx, tz, width);
                count++;
            }

            return count;
        }

        /// <summary>A stop line across the left half of the approach (travel direction t), 0.45 m thick.</summary>
        private static void StopLine(RoadMeshSink sink, RoadBuildOptions o, float x, float z, float tx, float tz, float width)
        {
            var lx = -tz;
            var lz = tx;
            var inner = 0.3f;
            var outer = Math.Max(inner + 0.5f, width * 0.5f - 0.3f);
            Strip(sink, o, x, z, tx, tz, lx, lz, inner, outer, 0.225f);
        }

        private static void Strip(RoadMeshSink sink, RoadBuildOptions o, float x, float z, float tx, float tz, float lx, float lz,
            float from, float to, float half)
        {
            var lift = o.YOffset + AdelaideRoadGeometry.PaintLift;
            float H(float px, float pz) => o.Height(px, pz) + lift;
            float ax = x + lx * from - tx * half, az = z + lz * from - tz * half;
            float bx = x + lx * to - tx * half, bz = z + lz * to - tz * half;
            float cx = x + lx * to + tx * half, cz = z + lz * to + tz * half;
            float dx = x + lx * from + tx * half, dz = z + lz * from + tz * half;
            sink.Tri(ax, H(ax, az), az, bx, H(bx, bz), bz, cx, H(cx, cz), cz, White);
            sink.Tri(ax, H(ax, az), az, cx, H(cx, cz), cz, dx, H(dx, dz), dz, White);
        }

        /// <summary>Give-way "shark teeth": triangles across the left half whose points face the approaching traffic.</summary>
        private static void GiveWayTeeth(RoadMeshSink sink, RoadBuildOptions o, float x, float z, float tx, float tz, float width)
        {
            var lx = -tz;
            var lz = tx;
            var lift = o.YOffset + AdelaideRoadGeometry.PaintLift;
            float H(float px, float pz) => o.Height(px, pz) + lift;
            var reach = Math.Max(1.5f, width * 0.5f - 0.3f);
            const float tooth = 0.6f;
            for (var s = 0.3f; s + tooth <= reach + 1e-3f; s += tooth * 1.5f)
            {
                float ax = x + lx * s, az = z + lz * s;
                float bx = x + lx * (s + tooth), bz = z + lz * (s + tooth);
                var px = x + lx * (s + tooth * 0.5f) - tx * tooth;
                var pz = z + lz * (s + tooth * 0.5f) - tz * tooth;
                sink.Tri(ax, H(ax, az), az, bx, H(bx, bz), bz, px, H(px, pz), pz, White);
            }
        }

        /// <summary>Stop signs (red plate on a pole) and give-way signs (white plate with a red face) on the left kerb.</summary>
        public static int BuildSigns(RoadMeshSink sink, RoadBuildOptions o)
        {
            var count = 0;
            foreach (var k in Nodes(AdelaideRoadNetwork.FurnitureKind.Stop))
            {
                Sign(k, sink, o, true);
                count++;
            }

            foreach (var k in Nodes(AdelaideRoadNetwork.FurnitureKind.GiveWay))
            {
                Sign(k, sink, o, false);
                count++;
            }

            return count;
        }

        private static void Sign(int k, RoadMeshSink sink, RoadBuildOptions o, bool stop)
        {
            Frame(k, out var x, out var z, out var tx, out var tz, out var width, out _, out _);
            var lx = -tz;
            var lz = tx;
            var px = x - tx * 0.6f + lx * (width * 0.5f + KerbOffsetMetres);
            var pz = z - tz * 0.6f + lz * (width * 0.5f + KerbOffsetMetres);
            var y = o.Height(px, pz) + o.YOffset;
            sink.Cylinder(px, y, pz, 0.15f, 0.12f, 8, Concrete);
            sink.Box(px, y + 0.12f, pz, 1f, 0f, 0.04f, 0.04f, 2.18f, Pole);
            // the plate faces the approaching traffic: its long axis lies across the road
            sink.Box(px + tx * 0.025f, y + 1.65f, pz + tz * 0.025f, lx, lz, 0.40f, 0.025f, 0.80f, HeadBody);
            sink.Box(px, y + 1.7f, pz, lx, lz, 0.36f, 0.02f, 0.72f, stop ? SignRed : White);
            if (!stop)
                sink.Box(px - tx * 0.021f, y + 1.79f, pz - tz * 0.021f, lx, lz, 0.28f, 0.02f, 0.5f, SignRed);
        }
    }
}
