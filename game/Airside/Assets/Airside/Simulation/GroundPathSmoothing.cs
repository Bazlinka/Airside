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

        /// <summary>
        /// Rounds off every turn tighter than <paramref name="minRadiusMetres"/> and collapses the
        /// out-and-back spurs a graph router leaves where it snaps a route's ends to the nearest
        /// node. The route is resampled every <paramref name="spacingMetres"/>, then each too-sharp
        /// vertex is pulled halfway toward the midpoint of its neighbours until none is. The first
        /// and last points never move, and straight runs are left exactly as they were.
        /// </summary>
        public static float[] RelaxTightTurns(float[] xz, float minRadiusMetres = 24f,
            float spacingMetres = 4f, int maxIterations = 400)
        {
            if (xz == null || xz.Length < 8 || spacingMetres < 1f || minRadiusMetres <= spacingMetres)
                return xz ?? Array.Empty<float>();

            var pts = Resample(xz, spacingMetres);
            var count = pts.Length / 2;
            if (count < 3)
                return xz;

            // Largest turn one vertex may make and still be a curve of at least minRadius.
            var allowed = 2f * (float)Math.Asin(Math.Min(1f, spacingMetres / (2f * minRadiusMetres)));
            var cosAllowed = (float)Math.Cos(allowed);
            for (var iteration = 0; iteration < maxIterations; iteration++)
            {
                var moved = false;
                for (var i = 1; i < count - 1; i++)
                {
                    float px = pts[(i - 1) * 2], pz = pts[(i - 1) * 2 + 1];
                    float cx = pts[i * 2], cz = pts[i * 2 + 1];
                    float nx = pts[(i + 1) * 2], nz = pts[(i + 1) * 2 + 1];
                    var d1 = Hypot(cx - px, cz - pz);
                    var d2 = Hypot(nx - cx, nz - cz);
                    var cos = d1 < 1e-4f || d2 < 1e-4f
                        ? -1f
                        : ((cx - px) * (nx - cx) + (cz - pz) * (nz - cz)) / (d1 * d2);
                    if (cos >= cosAllowed)
                        continue;
                    pts[i * 2] = cx + ((px + nx) * 0.5f - cx) * 0.5f;
                    pts[i * 2 + 1] = cz + ((pz + nz) * 0.5f - cz) * 0.5f;
                    moved = true;
                }

                if (!moved)
                    break;
            }

            return pts;
        }

        private static float[] Resample(float[] xz, float stepMetres)
        {
            var points = new List<float>(xz.Length) { xz[0], xz[1] };
            var carried = 0f;
            for (var i = 2; i + 1 < xz.Length; i += 2)
            {
                float ax = xz[i - 2], az = xz[i - 1], bx = xz[i], bz = xz[i + 1];
                var length = Hypot(bx - ax, bz - az);
                if (length < 1e-4f)
                    continue;
                var at = stepMetres - carried;
                while (at < length)
                {
                    var t = at / length;
                    points.Add(ax + (bx - ax) * t);
                    points.Add(az + (bz - az) * t);
                    at += stepMetres;
                }

                carried = length - (at - stepMetres);
            }

            var last = xz.Length - 2;
            if (Hypot(points[points.Count - 2] - xz[last], points[points.Count - 1] - xz[last + 1]) > 0.5f)
            {
                points.Add(xz[last]);
                points.Add(xz[last + 1]);
            }
            else
            {
                points[points.Count - 2] = xz[last];
                points[points.Count - 1] = xz[last + 1];
            }

            return points.ToArray();
        }

        private static float Hypot(float x, float z) => (float)Math.Sqrt(x * x + z * z);
    }
}
