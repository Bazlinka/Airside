using System;

namespace Airside.Simulation
{
    /// <summary>
    /// Latitude/longitude → the field's own world frame, exactly as
    /// scripts/generate-ypad-layout.py builds every taxiway, stand, coastline and the
    /// satellite ground: origin at the midpoint of the 05 and 23 thresholds, +x along
    /// 05 → 23, +z to the left of runway 05 (the terminal side). Anything placed from real
    /// coordinates — live traffic — must use this so it lands on the drawn pavement.
    /// </summary>
    public static class YpadFrame
    {
        // Same constants as the layout generator.
        private const double Lat0 = -34.95;
        private const double Lon0 = 138.53;
        private const double MetresPerDegreeLat = 110574.0;
        private static readonly double MetresPerDegreeLon = 111320.0 * Math.Cos(Lat0 * Math.PI / 180.0);

        private const double Runway05Lat = -34.9585244;
        private const double Runway05Lon = 138.5172171;
        private const double Runway23Lat = -34.9406962;
        private const double Runway23Lon = 138.5431392;

        private static readonly double MidEast;
        private static readonly double MidNorth;
        private static readonly double AlongEast;
        private static readonly double AlongNorth;

        static YpadFrame()
        {
            EastNorth(Runway05Lat, Runway05Lon, out var ae, out var an);
            EastNorth(Runway23Lat, Runway23Lon, out var be, out var bn);
            MidEast = (ae + be) * 0.5;
            MidNorth = (an + bn) * 0.5;
            var length = Math.Sqrt((be - ae) * (be - ae) + (bn - an) * (bn - an));
            AlongEast = (be - ae) / length;
            AlongNorth = (bn - an) / length;
        }

        /// <summary>World x/z metres for a real position.</summary>
        public static void ToWorld(double latitude, double longitude, out double x, out double z)
        {
            EastNorth(latitude, longitude, out var east, out var north);
            FromEastNorth(east - MidEast, north - MidNorth, out x, out z);
        }

        /// <summary>Rotates a true east/north vector into world x/z (left normal is +z).</summary>
        public static void FromEastNorth(double east, double north, out double x, out double z)
        {
            x = east * AlongEast + north * AlongNorth;
            z = -east * AlongNorth + north * AlongEast;
        }

        /// <summary>Unity yaw (degrees from +z toward +x) for a true track.</summary>
        public static float UnityYawFromTrue(double trueDegrees)
        {
            var radians = trueDegrees * Math.PI / 180.0;
            FromEastNorth(Math.Sin(radians), Math.Cos(radians), out var x, out var z);
            return (float)(Math.Atan2(x, z) * 180.0 / Math.PI);
        }

        private static void EastNorth(double latitude, double longitude, out double east, out double north)
        {
            east = (longitude - Lon0) * MetresPerDegreeLon;
            north = (latitude - Lat0) * MetresPerDegreeLat;
        }
    }
}
