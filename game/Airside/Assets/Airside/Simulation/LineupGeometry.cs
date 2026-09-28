using System;
using System.Collections.Generic;

namespace Airside.Simulation
{
    /// <summary>
    /// ADR 0147 — onto the runway the way an aircraft steers. The baked lineups turned onto the
    /// centreline on 3–30 m radii (the 12/30 ones as tight as 3 m) and finished up to 26° off the
    /// runway heading, so the takeoff roll began with a snap. Here a lineup is: straight on from the
    /// holding point, one turn on the aircraft's own radius, then straight along the centreline to the
    /// takeoff position, aligned. Falls back to the baked line where the geometry does not suit. Pure.
    /// </summary>
    public static class LineupGeometry
    {
        private const float Step = 2.5f;
        private const float MinCentreline = 8f;
        private const float MinLeadOut = 2f;

        /// <summary>
        /// Bend the first <paramref name="blendMetres"/> of <paramref name="baked"/> so it leaves its
        /// first point heading along (<paramref name="inX"/>, <paramref name="inZ"/>) and rejoins the
        /// authored line with the same tangent: each point is eased from the straight continuation of
        /// the arrival heading onto the authored point by smoothstep of the distance travelled. The
        /// start point and everything past the blend are unchanged.
        /// </summary>
        public static float[] AlignEntry(float[] baked, float inX, float inZ, float blendMetres)
        {
            if (baked == null || baked.Length < 4 || blendMetres <= 0f)
                return baked;
            float hx = baked[0], hz = baked[1];
            var result = (float[])baked.Clone();
            var along = 0f;
            for (var i = 2; i < baked.Length; i += 2)
            {
                along += (float)Math.Sqrt((baked[i] - baked[i - 2]) * (baked[i] - baked[i - 2])
                                          + (baked[i + 1] - baked[i - 1]) * (baked[i + 1] - baked[i - 1]));
                if (along >= blendMetres)
                    break;
                var u = along / blendMetres;
                var w = u * u * (3f - 2f * u);
                result[i] = hx + inX * along + (baked[i] - hx - inX * along) * w;
                result[i + 1] = hz + inZ * along + (baked[i + 1] - hz - inZ * along) * w;
            }

            return result;
        }

        /// <param name="baked">Holding point to takeoff position, as authored.</param>
        /// <param name="forwardX">Runway takeoff direction (unit, world XZ).</param>
        public static bool TryBuild(float[] baked, float forwardX, float forwardZ, float radius, out float[] path) =>
            TryBuild(baked, forwardX, forwardZ, radius, MinCentreline, out path, out _);

        /// <summary>
        /// The best compromise for an aircraft: the longest centreline straight (up to
        /// <paramref name="wheelbase"/> × 2.2) that still turns on at least three quarters of its radius,
        /// else whichever build turns widest.
        /// </summary>
        public static bool TryBest(float[] baked, float forwardX, float forwardZ, float radius, float wheelbase,
            out float[] path)
        {
            path = null;
            // The trailed main gear is still turning until it has had about two wheelbases
            // of centreline. A wider arc that spends that straight on the corner leaves the
            // nose cocked at the start of the roll. Take the longest straight that still
            // turns on a radius the aircraft can drive; if none does, keep the widest.
            float[] widest = null;
            var widestRadius = 0f;
            foreach (var factor in new[] { 2.6f, 2.2f, 1.9f, 1.6f, 1.3f, 1.0f, 0.6f, 0.3f, 0f })
            {
                if (!TryBuild(baked, forwardX, forwardZ, radius, wheelbase * factor, out var candidate, out var achieved))
                    continue;
                if (achieved > widestRadius)
                {
                    widestRadius = achieved;
                    widest = candidate;
                }

                if (factor >= 1.9f && achieved >= Math.Max(14f, radius * 0.55f))
                {
                    path = candidate;
                    return true;
                }
            }

            path = widest;
            return path != null;
        }

        /// <param name="centreline">
        /// Straight run along the centreline before the takeoff position: long enough (about twice the
        /// wheelbase) that the main gear, trailing the nose, is lined up too.
        /// </param>
        public static bool TryBuild(float[] baked, float forwardX, float forwardZ, float radius, float centreline,
            out float[] path, out float achievedRadius)
        {
            path = null;
            achievedRadius = 0f;
            if (baked == null || baked.Length < 8)
                return false;
            float hx = baked[0], hz = baked[1];
            float ex = baked[baked.Length - 2], ez = baked[baked.Length - 1];
            if (!StartDirection(baked, 12f, out var sx, out var sz))
                return false;

            // Corner C where the line from the hold meets the centreline through the takeoff position.
            var cross = sx * forwardZ - sz * forwardX;
            if (Math.Abs(cross) < 0.2f)
                return false;
            float rx = ex - hx, rz = ez - hz;
            var a = (rx * forwardZ - rz * forwardX) / cross;  // along the hold's line
            var b = (rx * sz - rz * sx) / cross;              // along the centreline from the takeoff position
            if (a <= MinLeadOut || b >= -MinCentreline)
                return false;
            centreline = Math.Max(MinCentreline, centreline);
            float cx = hx + sx * a, cz = hz + sz * a;

            var cos = Math.Max(-1f, Math.Min(1f, sx * forwardX + sz * forwardZ));
            var theta = (float)Math.Acos(cos);
            if (theta < 0.05f || theta > 2.8f)
                return false;
            var tangent = (float)(radius * Math.Tan(theta / 2f));
            tangent = Math.Min(tangent, Math.Min(a - MinLeadOut, -b - centreline));
            if (tangent < 3f)
                return false;
            achievedRadius = (float)(tangent / Math.Tan(theta / 2f));

            float ax = cx - sx * tangent, az = cz - sz * tangent;
            float bx = cx + forwardX * tangent, bz = cz + forwardZ * tangent;
            // A quadratic through the corner cuts inside a circular fillet, so the middle of
            // the turn was tighter than the radius above and the tail had to slide to stay
            // with it. This is the circular fillet of that same radius. `cross` is the
            // turn sign already computed above.
            var nx = cross >= 0f ? -sz : sz;
            var nz = cross >= 0f ? sx : -sx;
            float ox = ax + nx * achievedRadius, oz = az + nz * achievedRadius;
            var a0 = Math.Atan2(ax - ox, az - oz);
            var a1 = Math.Atan2(bx - ox, bz - oz);
            var sweep = WrapPi(a1 - a0);
            var arc = Math.Abs(sweep) * achievedRadius;
            var steps = Math.Max(8, (int)Math.Ceiling(arc / Step));
            var points = new List<float>();
            Line(points, hx, hz, ax, az, true);
            for (var i = 1; i <= steps; i++)
            {
                if (i == steps)
                {
                    points.Add(bx);
                    points.Add(bz);
                    break;
                }

                var ang = a0 + sweep * (i / (double)steps);
                points.Add(ox + (float)(Math.Sin(ang) * achievedRadius));
                points.Add(oz + (float)(Math.Cos(ang) * achievedRadius));
            }

            Line(points, bx, bz, ex, ez, false);
            path = points.ToArray();
            return true;
        }

        private static bool StartDirection(float[] xz, float metres, out float dx, out float dz)
        {
            float walked = 0f, px = xz[0], pz = xz[1];
            for (var i = 2; i + 1 < xz.Length; i += 2)
            {
                walked += (float)Math.Sqrt((xz[i] - px) * (xz[i] - px) + (xz[i + 1] - pz) * (xz[i + 1] - pz));
                px = xz[i];
                pz = xz[i + 1];
                if (walked >= metres)
                    break;
            }

            dx = px - xz[0];
            dz = pz - xz[1];
            var length = (float)Math.Sqrt(dx * dx + dz * dz);
            if (length < 2f)
                return false;
            dx /= length;
            dz /= length;
            return true;
        }

        private static double WrapPi(double angle)
        {
            while (angle > Math.PI)
                angle -= 2.0 * Math.PI;
            while (angle < -Math.PI)
                angle += 2.0 * Math.PI;
            return angle;
        }

        private static void Line(List<float> points, float x0, float z0, float x1, float z1, bool includeStart)
        {
            var length = (float)Math.Sqrt((x1 - x0) * (x1 - x0) + (z1 - z0) * (z1 - z0));
            var steps = Math.Max(1, (int)Math.Ceiling(length / Step));
            for (var i = includeStart ? 0 : 1; i <= steps; i++)
            {
                var t = i / (float)steps;
                points.Add(x0 + (x1 - x0) * t);
                points.Add(z0 + (z1 - z0) * t);
            }
        }
    }
}
