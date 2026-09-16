using System;
using System.Collections.Generic;

namespace Airside.Presentation
{
    /// <summary>A clipped concrete expansion joint in world-space x/z metres.</summary>
    public readonly struct ApronJoint
    {
        public ApronJoint(float startX, float startZ, float endX, float endZ)
        {
            StartX = startX;
            StartZ = startZ;
            EndX = endX;
            EndZ = endZ;
        }

        public float StartX { get; }
        public float StartZ { get; }
        public float EndX { get; }
        public float EndZ { get; }
        public float Length => (float)Math.Sqrt((EndX - StartX) * (EndX - StartX) + (EndZ - StartZ) * (EndZ - StartZ));
    }

    /// <summary>
    /// Generates a quiet, world-aligned slab grid clipped to an apron outline. Keeping this
    /// independent of the pavement mesh makes the visual detail deterministic and testable;
    /// it never participates in taxi routing, collision or simulation state.
    /// </summary>
    public static class ApronSlabJoints
    {
        public const float SpacingMetres = 18f;
        public const float EdgeInsetMetres = 0.45f;
        public const float WidthMetres = 0.11f;

        public static IReadOnlyList<ApronJoint> Generate(
            float[] xz,
            float spacing = SpacingMetres,
            float edgeInset = EdgeInsetMetres)
        {
            var result = new List<ApronJoint>();
            if (xz == null || xz.Length < 6 || xz.Length % 2 != 0 || spacing <= 0f)
                return result;

            var minX = float.MaxValue;
            var maxX = float.MinValue;
            var minZ = float.MaxValue;
            var maxZ = float.MinValue;
            for (var i = 0; i < xz.Length; i += 2)
            {
                minX = Math.Min(minX, xz[i]);
                maxX = Math.Max(maxX, xz[i]);
                minZ = Math.Min(minZ, xz[i + 1]);
                maxZ = Math.Max(maxZ, xz[i + 1]);
            }

            var firstX = (float)Math.Ceiling(minX / spacing) * spacing;
            for (var x = firstX; x < maxX; x += spacing)
            {
                if (x <= minX + edgeInset)
                    continue;
                AddVerticalSegments(result, xz, x, edgeInset);
            }

            var firstZ = (float)Math.Ceiling(minZ / spacing) * spacing;
            for (var z = firstZ; z < maxZ; z += spacing)
            {
                if (z <= minZ + edgeInset)
                    continue;
                AddHorizontalSegments(result, xz, z, edgeInset);
            }

            return result;
        }

        private static void AddVerticalSegments(List<ApronJoint> result, float[] xz, float x, float inset)
        {
            var crossings = new List<float>();
            var count = xz.Length / 2;
            for (var i = 0; i < count; i++)
            {
                var j = (i + 1) % count;
                var ax = xz[i * 2];
                var az = xz[i * 2 + 1];
                var bx = xz[j * 2];
                var bz = xz[j * 2 + 1];
                if ((ax <= x && bx > x) || (bx <= x && ax > x))
                    crossings.Add(az + (bz - az) * ((x - ax) / (bx - ax)));
            }

            crossings.Sort();
            for (var i = 0; i + 1 < crossings.Count; i += 2)
            {
                var start = crossings[i] + inset;
                var end = crossings[i + 1] - inset;
                if (end - start > WidthMetres * 2f)
                    result.Add(new ApronJoint(x, start, x, end));
            }
        }

        private static void AddHorizontalSegments(List<ApronJoint> result, float[] xz, float z, float inset)
        {
            var crossings = new List<float>();
            var count = xz.Length / 2;
            for (var i = 0; i < count; i++)
            {
                var j = (i + 1) % count;
                var ax = xz[i * 2];
                var az = xz[i * 2 + 1];
                var bx = xz[j * 2];
                var bz = xz[j * 2 + 1];
                if ((az <= z && bz > z) || (bz <= z && az > z))
                    crossings.Add(ax + (bx - ax) * ((z - az) / (bz - az)));
            }

            crossings.Sort();
            for (var i = 0; i + 1 < crossings.Count; i += 2)
            {
                var start = crossings[i] + inset;
                var end = crossings[i + 1] - inset;
                if (end - start > WidthMetres * 2f)
                    result.Add(new ApronJoint(start, z, end, z));
            }
        }
    }
}
