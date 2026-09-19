using System;
using System.Collections.Generic;

namespace Airside.Simulation
{
    /// <summary>
    /// Turns a sparse waypoint list into a taxiable polyline: corners become
    /// real fillets and long straights get enough samples for the speed
    /// profiler. Used for the 12/30 routes that are not OSM-traced.
    /// </summary>
    public static class GroundPathSmoothing
    {
        public const float DefaultFilletMetres = 22f;
        public const float DefaultStepMetres = 10f;

        public static float[] FilletAndDensify(float[] xz, float filletMetres = DefaultFilletMetres,
            float stepMetres = DefaultStepMetres)
        {
            if (xz == null || xz.Length < 4)
                return xz ?? Array.Empty<float>();
            return Densify(Fillet(xz, filletMetres), stepMetres);
        }

        public static float[] Join(float[] first, float[] second)
        {
            if (first == null || first.Length < 2)
                return second ?? Array.Empty<float>();
            if (second == null || second.Length < 2)
                return first;
            var skip = Hypot(first[first.Length - 2] - second[0], first[first.Length - 1] - second[1]) < 1.5f;
            var extra = skip ? second.Length - 2 : second.Length;
            if (extra <= 0)
                return first;
            var joined = new float[first.Length + extra];
            Array.Copy(first, joined, first.Length);
            Array.Copy(second, skip ? 2 : 0, joined, first.Length, extra);
            return joined;
        }

        public static float[] Fillet(float[] xz, float filletMetres)
        {
            if (xz == null || xz.Length < 6 || filletMetres <= 0.5f)
                return xz ?? Array.Empty<float>();

            var points = new List<float>(xz.Length + 32) { xz[0], xz[1] };
            var last = xz.Length / 2 - 1;
            for (var i = 1; i < last; i++)
            {
                float ax = xz[(i - 1) * 2], az = xz[(i - 1) * 2 + 1];
                float bx = xz[i * 2], bz = xz[i * 2 + 1];
                float cx = xz[(i + 1) * 2], cz = xz[(i + 1) * 2 + 1];
                var ab = Hypot(bx - ax, bz - az);
                var bc = Hypot(cx - bx, cz - bz);
                if (ab < 1.5f || bc < 1.5f)
                {
                    points.Add(bx);
                    points.Add(bz);
                    continue;
                }

                var inX = (ax - bx) / ab;
                var inZ = (az - bz) / ab;
                var outX = (cx - bx) / bc;
                var outZ = (cz - bz) / bc;
                var dot = inX * outX + inZ * outZ;
                if (dot > 0.97f)
                {
                    points.Add(bx);
                    points.Add(bz);
                    continue;
                }

                var radius = Math.Min(filletMetres, Math.Min(ab, bc) * 0.4f);
                var entryX = bx + inX * radius;
                var entryZ = bz + inZ * radius;
                var exitX = bx + outX * radius;
                var exitZ = bz + outZ * radius;
                const int steps = 6;
                for (var s = 0; s <= steps; s++)
                {
                    var t = s / (float)steps;
                    var omt = 1f - t;
                    points.Add(omt * omt * entryX + 2f * omt * t * bx + t * t * exitX);
                    points.Add(omt * omt * entryZ + 2f * omt * t * bz + t * t * exitZ);
                }
            }

            points.Add(xz[xz.Length - 2]);
            points.Add(xz[xz.Length - 1]);
            return points.ToArray();
        }

        public static float[] Densify(float[] xz, float stepMetres)
        {
            if (xz == null || xz.Length < 4 || stepMetres < 1f)
                return xz ?? Array.Empty<float>();

            var points = new List<float>(xz.Length * 2) { xz[0], xz[1] };
            for (var i = 2; i + 1 < xz.Length; i += 2)
            {
                float ax = xz[i - 2], az = xz[i - 1], bx = xz[i], bz = xz[i + 1];
                var length = Hypot(bx - ax, bz - az);
                var count = Math.Max(1, (int)Math.Floor(length / stepMetres));
                for (var s = 1; s <= count; s++)
                {
                    var t = s / (float)count;
                    if (s == count)
                    {
                        points.Add(bx);
                        points.Add(bz);
                    }
                    else
                    {
                        points.Add(ax + (bx - ax) * t);
                        points.Add(az + (bz - az) * t);
                    }
                }
            }

            return points.ToArray();
        }

        private static float Hypot(float x, float z) => (float)Math.Sqrt(x * x + z * z);
    }
}
