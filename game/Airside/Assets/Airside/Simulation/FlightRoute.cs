using System;

namespace Airside.Simulation
{
    /// <summary>
    /// A flown track between two airports. The published city-pair is still a
    /// great circle; each flight is displaced onto a parallel or dog-legged
    /// airway so two Melbourne services do not sit on the same line.
    /// </summary>
    public static class FlightRoute
    {
        public static void Point(double lat1, double lon1, double lat2, double lon2, double t,
            string key, out double lat, out double lon)
        {
            GreatCircle(lat1, lon1, lat2, lon2, t, out lat, out lon);
            if (string.IsNullOrEmpty(key))
                return;

            GreatCircle(lat1, lon1, lat2, lon2, Math.Min(1.0, t + 0.004), out var latAhead, out var lonAhead);
            var heading = HeadingDegrees(lat, lon, latAhead, lonAhead);
            var offsetKm = OffsetKm(key, DistanceKm(lat1, lon1, lat2, lon2));
            Displace(lat, lon, heading, offsetKm, t, key, out lat, out lon);
        }

        public static double OffsetKm(string key, double distanceKm)
        {
            var hash = Hash(key);
            var max = Math.Min(48.0, Math.Max(6.0, Math.Abs(distanceKm) * 0.07));
            return ((hash % 2001) / 2000.0 * 2.0 - 1.0) * max;
        }

        public static void GreatCircle(double lat1, double lon1, double lat2, double lon2, double t,
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

        public static double HeadingDegrees(double lat1, double lon1, double lat2, double lon2)
        {
            var phi1 = lat1 * Math.PI / 180.0;
            var phi2 = lat2 * Math.PI / 180.0;
            var dLon = (lon2 - lon1) * Math.PI / 180.0;
            var y = Math.Sin(dLon) * Math.Cos(phi2);
            var x = Math.Cos(phi1) * Math.Sin(phi2) - Math.Sin(phi1) * Math.Cos(phi2) * Math.Cos(dLon);
            var deg = Math.Atan2(y, x) * 180.0 / Math.PI;
            return deg < 0 ? deg + 360 : deg;
        }

        public static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double earthKm = 6371.0;
            var p1 = lat1 * Math.PI / 180.0;
            var p2 = lat2 * Math.PI / 180.0;
            var dPhi = p2 - p1;
            var dLam = (lon2 - lon1) * Math.PI / 180.0;
            var a = Math.Sin(dPhi * 0.5) * Math.Sin(dPhi * 0.5)
                    + Math.Cos(p1) * Math.Cos(p2) * Math.Sin(dLam * 0.5) * Math.Sin(dLam * 0.5);
            return 2.0 * earthKm * Math.Asin(Math.Min(1.0, Math.Sqrt(a)));
        }

        private static void Displace(double lat, double lon, double headingDegrees, double offsetKm,
            double t, string key, out double outLat, out double outLon)
        {
            var envelope = Math.Sin(Math.PI * Clamp01(t));
            var dogleg = (Hash(key + "|w") % 3) - 1;
            var shape = envelope * (1.0 + 0.55 * dogleg * Math.Sin(2.0 * Math.PI * Clamp01(t)));
            var metres = offsetKm * 1000.0 * shape;
            var rad = (headingDegrees + 90.0) * Math.PI / 180.0;
            var north = metres * Math.Cos(rad);
            var east = metres * Math.Sin(rad);
            const double metresPerDegree = 110_946.0;
            outLat = lat + north / metresPerDegree;
            var denom = metresPerDegree * Math.Cos(lat * Math.PI / 180.0);
            outLon = Math.Abs(denom) < 1e-6 ? lon : lon + east / denom;
        }

        private static double Clamp01(double value) => value < 0 ? 0 : value > 1 ? 1 : value;

        private static int Hash(string key)
        {
            unchecked
            {
                var hash = 17;
                if (key == null)
                    return hash;
                foreach (var ch in key)
                    hash = hash * 31 + ch;
                return hash & int.MaxValue;
            }
        }
    }
}
