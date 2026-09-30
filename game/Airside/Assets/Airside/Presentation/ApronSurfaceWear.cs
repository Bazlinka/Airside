using System;
using System.Collections.Generic;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>A rectangular concrete patch or drainage pit on an apron.</summary>
    public readonly struct ApronWearMark
    {
        public ApronWearMark(float centreX, float centreZ, float halfX, float halfZ,
            float yawDegrees, bool drainage)
        {
            CentreX = centreX;
            CentreZ = centreZ;
            HalfX = halfX;
            HalfZ = halfZ;
            YawDegrees = yawDegrees;
            Drainage = drainage;
        }

        public float CentreX { get; }
        public float CentreZ { get; }
        public float HalfX { get; }
        public float HalfZ { get; }
        public float YawDegrees { get; }
        /// <summary>True for a small drainage grate; false for a patch repair.</summary>
        public bool Drainage { get; }
    }

    /// <summary>
    /// Seeded apron patch repairs and drainage pits (visual overhaul Phase 1).
    /// Presentation only — clipped inside real apron outlines with an edge inset.
    /// </summary>
    public static class ApronSurfaceWear
    {
        public const int Seed = 8813;
        public const float EdgeInsetMetres = 4f;
        public const int PatchesPerApron = 6;
        public const int PitsPerApron = 4;

        private static ApronWearMark[] _cached;

        public static IReadOnlyList<ApronWearMark> All() => _cached ??= Generate();

        public static ApronWearMark[] Generate()
        {
            var random = new Random(Seed);
            var list = new List<ApronWearMark>();
            foreach (var apron in AdelaideLayout.Aprons)
            {
                AddMarks(list, random, apron.Xz, PatchesPerApron, drainage: false);
                AddMarks(list, random, apron.Xz, PitsPerApron, drainage: true);
            }

            return list.ToArray();
        }

        public static float[] Corners(ApronWearMark mark)
        {
            var yaw = mark.YawDegrees * Math.PI / 180d;
            var cos = (float)Math.Cos(yaw);
            var sin = (float)Math.Sin(yaw);
            var local = new[]
            {
                -mark.HalfX, -mark.HalfZ,
                mark.HalfX, -mark.HalfZ,
                mark.HalfX, mark.HalfZ,
                -mark.HalfX, mark.HalfZ
            };
            var world = new float[8];
            for (var i = 0; i < 4; i++)
            {
                var lx = local[i * 2];
                var lz = local[i * 2 + 1];
                world[i * 2] = mark.CentreX + lx * cos - lz * sin;
                world[i * 2 + 1] = mark.CentreZ + lx * sin + lz * cos;
            }

            return world;
        }

        private static void AddMarks(List<ApronWearMark> list, Random random, float[] xz,
            int count, bool drainage)
        {
            if (xz == null || xz.Length < 6)
                return;

            Bounds(xz, out var minX, out var maxX, out var minZ, out var maxZ);
            var attempts = count * 40;
            var placed = 0;
            for (var i = 0; i < attempts && placed < count; i++)
            {
                var x = Lerp(minX, maxX, (float)random.NextDouble());
                var z = Lerp(minZ, maxZ, (float)random.NextDouble());
                if (!Contains(xz, x, z) || DistanceToEdge(xz, x, z) < EdgeInsetMetres)
                    continue;

                float halfX;
                float halfZ;
                float yaw;
                if (drainage)
                {
                    halfX = Lerp(0.35f, 0.55f, (float)random.NextDouble());
                    halfZ = halfX;
                    yaw = 0f;
                }
                else
                {
                    halfX = Lerp(2.2f, 6.5f, (float)random.NextDouble());
                    halfZ = Lerp(1.4f, 4.0f, (float)random.NextDouble());
                    yaw = Lerp(-12f, 12f, (float)random.NextDouble());
                }

                list.Add(new ApronWearMark(x, z, halfX, halfZ, yaw, drainage));
                placed++;
            }
        }

        private static void Bounds(float[] xz, out float minX, out float maxX, out float minZ, out float maxZ)
        {
            minX = float.MaxValue;
            maxX = float.MinValue;
            minZ = float.MaxValue;
            maxZ = float.MinValue;
            for (var i = 0; i < xz.Length; i += 2)
            {
                minX = Math.Min(minX, xz[i]);
                maxX = Math.Max(maxX, xz[i]);
                minZ = Math.Min(minZ, xz[i + 1]);
                maxZ = Math.Max(maxZ, xz[i + 1]);
            }
        }

        /// <summary>Ray-crossing point-in-polygon (even-odd).</summary>
        public static bool Contains(float[] xz, float x, float z)
        {
            var inside = false;
            var count = xz.Length / 2;
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                var xi = xz[i * 2];
                var zi = xz[i * 2 + 1];
                var xj = xz[j * 2];
                var zj = xz[j * 2 + 1];
                var intersect = ((zi > z) != (zj > z))
                    && (x < (xj - xi) * (z - zi) / (zj - zi + 1e-12f) + xi);
                if (intersect)
                    inside = !inside;
            }

            return inside;
        }

        public static float DistanceToEdge(float[] xz, float x, float z)
        {
            var best = float.MaxValue;
            var count = xz.Length / 2;
            for (var i = 0; i < count; i++)
            {
                var j = (i + 1) % count;
                best = Math.Min(best, DistanceToSegment(
                    x, z, xz[i * 2], xz[i * 2 + 1], xz[j * 2], xz[j * 2 + 1]));
            }

            return best;
        }

        private static float DistanceToSegment(float px, float pz, float ax, float az, float bx, float bz)
        {
            var dx = bx - ax;
            var dz = bz - az;
            var len2 = dx * dx + dz * dz;
            if (len2 < 1e-8f)
            {
                var ex = px - ax;
                var ez = pz - az;
                return (float)Math.Sqrt(ex * ex + ez * ez);
            }

            var t = Math.Max(0f, Math.Min(1f, ((px - ax) * dx + (pz - az) * dz) / len2));
            var qx = ax + t * dx - px;
            var qz = az + t * dz - pz;
            return (float)Math.Sqrt(qx * qx + qz * qz);
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
