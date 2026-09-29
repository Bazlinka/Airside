using System;
using System.Collections.Generic;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>One straight run of fence between two posts, world x,z metres.</summary>
    public readonly struct FenceRun
    {
        public FenceRun(float x0, float z0, float x1, float z1) { X0 = x0; Z0 = z0; X1 = x1; Z1 = z1; }
        public float X0 { get; }
        public float Z0 { get; }
        public float X1 { get; }
        public float Z1 { get; }
        public float Length => (float)Math.Sqrt((X1 - X0) * (X1 - X0) + (Z1 - Z0) * (Z1 - Z0));
    }

    /// <summary>
    /// The security fence along the real aerodrome boundary (<see cref="AdelaideBoundary"/>), cut into
    /// runs of at most <see cref="PostSpacingMetres"/> with an opening at each vehicle gate. It replaces
    /// the plain 3.4 × 2.3 km rectangle (<see cref="AirsideAdelaidePerimeter"/>). Pure geometry.
    /// </summary>
    public static class AdelaideBoundaryFence
    {
        public const float PostSpacingMetres = 40f;

        /// <summary>Gate opening along the fence: the vehicle gate width plus a post either side.</summary>
        public static float GateGapMetres => AirsideAdelaidePerimeter.VehicleGateWidthMetres + 2f * AirsideAdelaidePerimeter.PostSizeMetres;

        private static FenceRun[] _runs;

        public static FenceRun[] Runs() => _runs ??= Plan(AdelaideBoundary.Outline, AdelaideBoundary.Gates, PostSpacingMetres, GateGapMetres);

        public static FenceRun[] Plan(float[] outline, float[] gates, float spacing, float gateGap)
        {
            var runs = new List<FenceRun>();
            var count = outline.Length / 2;
            for (var i = 0; i < count; i++)
            {
                var j = (i + 1) % count;
                var ax = outline[i * 2];
                var az = outline[i * 2 + 1];
                var bx = outline[j * 2];
                var bz = outline[j * 2 + 1];
                var length = (float)Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
                if (length < 0.01f)
                    continue;
                var pieces = Math.Max(1, (int)Math.Ceiling(length / spacing));
                for (var p = 0; p < pieces; p++)
                {
                    var t0 = p / (float)pieces;
                    var t1 = (p + 1) / (float)pieces;
                    var x0 = ax + (bx - ax) * t0;
                    var z0 = az + (bz - az) * t0;
                    var x1 = ax + (bx - ax) * t1;
                    var z1 = az + (bz - az) * t1;
                    AddCutAtGates(runs, x0, z0, x1, z1, gates, gateGap);
                }
            }

            return runs.ToArray();
        }

        private static void AddCutAtGates(List<FenceRun> runs, float x0, float z0, float x1, float z1, float[] gates, float gap)
        {
            var dx = x1 - x0;
            var dz = z1 - z0;
            var length = (float)Math.Sqrt(dx * dx + dz * dz);
            var from = 0f;
            var to = length;
            for (var g = 0; gates != null && g + 1 < gates.Length; g += 2)
            {
                // Distance of the gate along this run, if it lies on it.
                var along = ((gates[g] - x0) * dx + (gates[g + 1] - z0) * dz) / length;
                var off = Math.Abs((gates[g] - x0) * dz - (gates[g + 1] - z0) * dx) / length;
                if (off > 2f || along < -gap || along > length + gap)
                    continue;
                var openFrom = along - gap * 0.5f;
                var openTo = along + gap * 0.5f;
                if (openTo <= from || openFrom >= to)
                    continue;
                if (openFrom > from + 0.01f)
                {
                    runs.Add(Sub(x0, z0, dx, dz, length, from, openFrom));
                }

                from = Math.Max(from, openTo);
            }

            if (to - from > 0.01f)
                runs.Add(Sub(x0, z0, dx, dz, length, from, to));
        }

        private static FenceRun Sub(float x0, float z0, float dx, float dz, float length, float from, float to) =>
            new(x0 + dx * from / length, z0 + dz * from / length, x0 + dx * to / length, z0 + dz * to / length);
    }
}
