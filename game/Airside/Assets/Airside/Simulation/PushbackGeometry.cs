using System;
using System.Collections.Generic;

namespace Airside.Simulation
{
    /// <summary>
    /// ADR 0146 — a pushback the way a tug does it. The baked OSM pushbacks were one path per stand,
    /// whichever runway was in use, so for one of the two runway ends the tail swung toward the way the
    /// aircraft would taxi. The nose was then turned by blending its heading while the airframe slid
    /// along the push, which read as sideways skating. Here each push is built for its taxi route:
    /// <list type="bullet">
    /// <item>straight back from the stop along the stand line;</item>
    /// <item>a tail swing of the aircraft's own turning radius, away from the taxi direction;</item>
    /// <item>a short straight along the taxilane, so the aircraft stops nose-first down it.</item>
    /// </list>
    /// The taxi-out then starts where the push ends and runs forward along the taxilane to rejoin the
    /// baked route. Returns false (keep the baked paths) where the geometry does not suit a push onto
    /// a straight taxilane. Pure.
    /// </summary>
    public static class PushbackGeometry
    {
        private const float Step = 2.5f;
        private const float ExtraAlongTaxilane = 6f;
        private const float MinStraightBack = 4f;
        private const float MaxPushMetres = 180f;

        /// <summary>
        /// As <see cref="TryBuild(float,float,float,float[],float,out float[],out float[])"/>, but when this
        /// route leaves the push point almost along the push line (the 23 routes that head straight out to
        /// a parallel taxiway), take the taxilane's line from <paramref name="taxilaneHint"/> (the stand's
        /// other route) and face whichever way this route actually travels along it.
        /// </summary>
        public static bool TryBuild(float stopX, float stopZ, float headingDegrees, float[] taxiRoute,
            float[] taxilaneHint, float radius, out float[] push, out float[] taxi)
        {
            if (TryBuild(stopX, stopZ, headingDegrees, taxiRoute, radius, out push, out taxi))
                return true;
            if (taxilaneHint == null || taxilaneHint.Length < 8 || taxiRoute == null || taxiRoute.Length < 8
                || !DirectionAlong(taxilaneHint, 15f, out var hx, out var hz))
                return false;
            // Which way along the taxilane this route goes, judged well down the route.
            var far = Math.Min(taxiRoute.Length - 2, 60);
            var progress = (taxiRoute[far] - taxilaneHint[0]) * hx + (taxiRoute[far + 1] - taxilaneHint[1]) * hz;
            if (progress < 0f)
            {
                hx = -hx;
                hz = -hz;
            }

            // A synthetic start: the taxilane at the hint's start, heading that way, then the real route.
            var seed = new float[taxiRoute.Length + 4];
            seed[0] = taxilaneHint[0];
            seed[1] = taxilaneHint[1];
            seed[2] = taxilaneHint[0] + hx * 20f;
            seed[3] = taxilaneHint[1] + hz * 20f;
            Array.Copy(taxiRoute, 0, seed, 4, taxiRoute.Length);
            return TryBuild(stopX, stopZ, headingDegrees, seed, radius, out push, out taxi);
        }

        public static bool TryBuild(float stopX, float stopZ, float headingDegrees, float[] taxiRoute, float radius,
            out float[] push, out float[] taxi)
        {
            push = null;
            taxi = null;
            if (taxiRoute == null || taxiRoute.Length < 8)
                return false;

            // Nose direction at the stop, and the taxilane's direction where the route begins.
            var h = headingDegrees * Math.PI / 180.0;
            float nx = (float)Math.Sin(h), nz = (float)Math.Cos(h);
            float p0x = taxiRoute[0], p0z = taxiRoute[1];
            if (!DirectionAlong(taxiRoute, 15f, out var dx, out var dz))
                return false;

            // Push line: stop + s·(−n). Taxilane line: P0 + u·d. Corner C where they meet.
            float bx = -nx, bz = -nz;
            var cross = bx * dz - bz * dx;
            if (Math.Abs(cross) < 0.35f)
                return false; // push line nearly along the taxilane: no clean corner
            var s = ((p0x - stopX) * dz - (p0z - stopZ) * dx) / cross;
            if (s < MinStraightBack || s > MaxPushMetres)
                return false;
            float cx = stopX + bx * s, cz = stopZ + bz * s;

            // After the swing the aircraft travels tail-first along −d, so it faces +d (down the taxilane).
            float ox = -dx, oz = -dz;
            var cos = Math.Max(-1f, Math.Min(1f, bx * ox + bz * oz));
            var theta = (float)Math.Acos(cos);
            if (theta < 0.3f || theta > 2.7f)
                return false;
            var tangent = (float)(radius * Math.Tan(theta / 2f));
            if (tangent > s - MinStraightBack)
            {
                // Not enough room behind the stand for this radius: tighten, within reason.
                tangent = s - MinStraightBack;
                if (tangent < 3f)
                    return false;
            }

            float ax = cx - bx * tangent, az = cz - bz * tangent;   // where the swing starts
            float ex = cx + ox * tangent, ez = cz + oz * tangent;   // where it ends, on the taxilane
            float fx = ex + ox * ExtraAlongTaxilane, fz = ez + oz * ExtraAlongTaxilane;

            var path = new List<float>();
            AddLine(path, stopX, stopZ, ax, az, includeStart: true);
            AddBezier(path, ax, az, cx, cz, ex, ez);
            AddLine(path, ex, ez, fx, fz, includeStart: false);
            push = path.ToArray();

            // Taxi forward from the push end along +d, rejoining the route past the corner.
            var rejoin = -1;
            for (var i = 0; i + 1 < taxiRoute.Length; i += 2)
            {
                var along = (taxiRoute[i] - fx) * dx + (taxiRoute[i + 1] - fz) * dz;
                if (along >= tangent + ExtraAlongTaxilane + 30f)
                {
                    rejoin = i;
                    break;
                }
            }

            if (rejoin < 0)
                return false;
            var route = new List<float>();
            AddLine(route, fx, fz, taxiRoute[rejoin], taxiRoute[rejoin + 1], includeStart: true);
            for (var i = rejoin + 2; i + 1 < taxiRoute.Length; i += 2)
            {
                route.Add(taxiRoute[i]);
                route.Add(taxiRoute[i + 1]);
            }

            taxi = route.ToArray();
            return true;
        }

        /// <summary>Unit direction from a route's start to the point <paramref name="metres"/> along it.</summary>
        private static bool DirectionAlong(float[] xz, float metres, out float dx, out float dz)
        {
            dx = dz = 0f;
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
            if (length < 3f)
                return false;
            dx /= length;
            dz /= length;
            return true;
        }

        private static void AddLine(List<float> path, float x0, float z0, float x1, float z1, bool includeStart)
        {
            var length = (float)Math.Sqrt((x1 - x0) * (x1 - x0) + (z1 - z0) * (z1 - z0));
            var steps = Math.Max(1, (int)Math.Ceiling(length / Step));
            for (var i = includeStart ? 0 : 1; i <= steps; i++)
            {
                var t = i / (float)steps;
                path.Add(x0 + (x1 - x0) * t);
                path.Add(z0 + (z1 - z0) * t);
            }
        }

        /// <summary>A quadratic Bézier through the corner: tangent to both straights, close to a circular arc.</summary>
        private static void AddBezier(List<float> path, float ax, float az, float cx, float cz, float ex, float ez)
        {
            var length = (float)(Math.Sqrt((cx - ax) * (cx - ax) + (cz - az) * (cz - az))
                                 + Math.Sqrt((ex - cx) * (ex - cx) + (ez - cz) * (ez - cz)));
            var steps = Math.Max(4, (int)Math.Ceiling(length / Step));
            for (var i = 1; i <= steps; i++)
            {
                var t = i / (float)steps;
                var u = 1f - t;
                path.Add(u * u * ax + 2f * u * t * cx + t * t * ex);
                path.Add(u * u * az + 2f * u * t * cz + t * t * ez);
            }
        }
    }
}
