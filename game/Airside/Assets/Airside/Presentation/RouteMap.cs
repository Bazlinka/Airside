using System;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>
    /// Geometry for drawing live flights on the Australia map and tags over the field.
    /// No UnityEngine, so the EditMode suite covers it.
    /// </summary>
    public static class RouteMap
    {
        /// <summary>Field tags hide once the camera is this close — the aircraft itself is plain to see.</summary>
        public const float FieldTagHideDistanceMetres = 140f;

        /// <summary>Zoom the map jumps to when it starts tracking a flight.</summary>
        public const float TrackingZoom = 14f;

        /// <summary>Fraction of a state elapsed at a sub-second instant, so map icons glide instead of ticking.</summary>
        public static double Progress(long startedAtSeconds, long endsAtSeconds, double nowSeconds)
        {
            var total = endsAtSeconds - startedAtSeconds;
            if (total <= 0)
                return 1.0;
            var done = (nowSeconds - startedAtSeconds) / total;
            return done < 0 ? 0 : done > 1 ? 1 : done;
        }

        /// <summary>Point a fraction <paramref name="t"/> along one flight's displaced track.</summary>
        public static void FlightPoint(double lat1, double lon1, double lat2, double lon2, double t,
            string key, out double lat, out double lon) =>
            FlightRoute.Point(lat1, lon1, lat2, lon2, t, key, out lat, out lon);

        /// <summary>Point a fraction <paramref name="t"/> along the great circle between two lat/lon points (degrees).</summary>
        public static void GreatCirclePoint(double lat1, double lon1, double lat2, double lon2, double t,
            out double lat, out double lon)
        {
            var phi1 = lat1 * Math.PI / 180.0;
            var lam1 = lon1 * Math.PI / 180.0;
            var phi2 = lat2 * Math.PI / 180.0;
            var lam2 = lon2 * Math.PI / 180.0;

            var x1 = Math.Cos(phi1) * Math.Cos(lam1);
            var y1 = Math.Cos(phi1) * Math.Sin(lam1);
            var z1 = Math.Sin(phi1);
            var x2 = Math.Cos(phi2) * Math.Cos(lam2);
            var y2 = Math.Cos(phi2) * Math.Sin(lam2);
            var z2 = Math.Sin(phi2);

            var dot = Math.Max(-1.0, Math.Min(1.0, x1 * x2 + y1 * y2 + z1 * z2));
            var omega = Math.Acos(dot);
            double x, y, z;
            if (omega < 1e-9)
            {
                x = x1 + (x2 - x1) * t;
                y = y1 + (y2 - y1) * t;
                z = z1 + (z2 - z1) * t;
            }
            else
            {
                var s = Math.Sin(omega);
                var a = Math.Sin((1 - t) * omega) / s;
                var b = Math.Sin(t * omega) / s;
                x = a * x1 + b * x2;
                y = a * y1 + b * y2;
                z = a * z1 + b * z2;
            }

            lat = Math.Atan2(z, Math.Sqrt(x * x + y * y)) * 180.0 / Math.PI;
            lon = Math.Atan2(y, x) * 180.0 / Math.PI;
        }

        /// <summary>
        /// Liang–Barsky clip of a segment to a rectangle. False when nothing of it is inside;
        /// otherwise the endpoints are trimmed to the rectangle.
        /// </summary>
        public static bool ClipSegment(ref float x0, ref float y0, ref float x1, ref float y1,
            float minX, float minY, float maxX, float maxY)
        {
            var dx = x1 - x0;
            var dy = y1 - y0;
            var t0 = 0f;
            var t1 = 1f;

            bool Edge(float p, float q)
            {
                if (Math.Abs(p) < 1e-6f)
                    return q >= 0f;
                var r = q / p;
                if (p < 0f)
                {
                    if (r > t1) return false;
                    if (r > t0) t0 = r;
                }
                else
                {
                    if (r < t0) return false;
                    if (r < t1) t1 = r;
                }
                return true;
            }

            if (!Edge(-dx, x0 - minX) || !Edge(dx, maxX - x0) || !Edge(-dy, y0 - minY) || !Edge(dy, maxY - y0))
                return false;

            var sx = x0;
            var sy = y0;
            x0 = sx + t0 * dx;
            y0 = sy + t0 * dy;
            x1 = sx + t1 * dx;
            y1 = sy + t1 * dy;
            return true;
        }
    }

    /// <summary>
    /// Scroll-to-zoom feel shared by the route map and the field camera: steps in log space
    /// sized by how far the wheel moved, capped per event, eased in over ~0.25 s.
    /// </summary>
    public static class MapZoom
    {
        /// <summary>Log-zoom per unit of scroll delta — one mouse-wheel notch (~3) is about 12 %.</summary>
        public const float LogPerScrollUnit = 0.04f;

        /// <summary>Largest single scroll event honoured (a flung trackpad is not a teleport).</summary>
        public const float MaxScrollPerEvent = 6f;

        /// <summary>Most log-zoom that can be queued at once, ~2x.</summary>
        public const float MaxPending = 0.7f;

        /// <summary>Easing rate: ~95 % of a queued zoom lands in 0.25 s.</summary>
        public const float EaseRate = 12f;

        /// <summary>Scroll down (positive) zooms out; returns log-zoom to queue.</summary>
        public static float StepFor(float scrollDelta)
        {
            var clamped = scrollDelta < -MaxScrollPerEvent ? -MaxScrollPerEvent : scrollDelta > MaxScrollPerEvent ? MaxScrollPerEvent : scrollDelta;
            return -clamped * LogPerScrollUnit;
        }

        /// <summary>Portion of the pending log-zoom to apply this frame.</summary>
        public static float EaseStep(float pending, float deltaSeconds)
        {
            if (deltaSeconds <= 0f)
                return 0f;
            return pending * (1f - (float)System.Math.Exp(-deltaSeconds * EaseRate));
        }
    }
}
