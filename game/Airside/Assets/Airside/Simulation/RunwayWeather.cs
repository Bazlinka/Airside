using System;
using Airside.Domain;

namespace Airside.Simulation
{
    public enum RunwayDirection
    {
        Runway05,
        Runway23,
        Runway12,
        Runway30
    }

    /// <summary>Deterministic Adelaide surface wind used by the tower and HUD.</summary>
    public readonly struct SurfaceWind
    {
        public SurfaceWind(int directionDegrees, int knots)
        {
            DirectionDegrees = ((directionDegrees % 360) + 360) % 360;
            Knots = Math.Max(0, knots);
        }

        public int DirectionDegrees { get; }
        public int Knots { get; }
        public string Text => $"{DirectionDegrees:000}° / {Knots} kt";
    }

    public static class RunwayWeather
    {
        /// <summary>
        /// A slowly changing, repeatable local wind. It avoids live-network dependency while
        /// making runway use change with the weather instead of being hard-coded to 05.
        /// </summary>
        public static SurfaceWind At(AirlineClock clock, SimulationTime time)
        {
            var local = clock.LocalAt(time);
            var day = local.DayOfYear;
            var hour = local.TimeOfDay.TotalHours;
            var direction = 140.0
                + 105.0 * Math.Sin((day + hour / 24.0) * Math.PI * 2.0 / 9.0)
                + 35.0 * Math.Sin(hour * Math.PI * 2.0 / 24.0);
            var speed = 6.0 + 5.0 * (0.5 + 0.5 * Math.Sin((day * 3.0 + hour) * Math.PI * 2.0 / 17.0));
            return new SurfaceWind((int)Math.Round(direction), (int)Math.Round(speed));
        }

        /// <summary>Published 05/23 true headings, degrees clockwise from north.</summary>
        public const int Heading05 = 50;
        public const int Heading23 = 230;
        /// <summary>12/30 true headings. Turboprops may use this 1 650 m strip; jets may not.</summary>
        public const int Heading12 = 123;
        public const int Heading30 = 303;

        /// <summary>
        /// World +X is 05 (50° true); Unity yaw 0 faces +Z, which is 320° true.
        /// </summary>
        public static float TrueFromUnityYaw(float unityYawDegrees)
        {
            var heading = Heading05 + 270f + unityYawDegrees;
            heading %= 360f;
            if (heading < 0f)
                heading += 360f;
            return heading;
        }

        /// <summary>Use the runway end with the larger headwind component; calm defaults to 05.</summary>
        public static RunwayDirection Select(SurfaceWind wind) =>
            Select(wind, null, null, null);

        /// <summary>
        /// Tower runway. Jets stay on the 3 100 m 05/23 strip and take the
        /// dest-aligned end through a wider wind tie. Regionals use 12/30 —
        /// the 1 650 m cross strip — and follow the wind more tightly.
        /// </summary>
        public static RunwayDirection Select(SurfaceWind wind, AircraftType type,
            Destination? destination, Destination? home)
        {
            if (AllowsCrossRunway(type))
                return SelectCross(wind, destination, home);

            var h05 = Headwind(wind, Heading05);
            var h23 = Headwind(wind, Heading23);
            // Jets stay on 05/23. They will take the dest-aligned end through
            // an 8 kt wind tie so a Perth 787 does not climb out inland.
            if (wind.Knots < 3)
                return PreferPair(destination, home, RunwayDirection.Runway05, RunwayDirection.Runway23,
                    Heading05, Heading23, RunwayDirection.Runway05);
            if (Math.Abs(h23 - h05) < 8.0)
                return PreferPair(destination, home, RunwayDirection.Runway05, RunwayDirection.Runway23,
                    Heading05, Heading23, h23 > h05 ? RunwayDirection.Runway23 : RunwayDirection.Runway05);
            return h23 > h05 ? RunwayDirection.Runway23 : RunwayDirection.Runway05;
        }

        private static RunwayDirection SelectCross(SurfaceWind wind, Destination? destination, Destination? home)
        {
            var h12 = Headwind(wind, Heading12);
            var h30 = Headwind(wind, Heading30);
            if (wind.Knots < 3)
                return PreferPair(destination, home, RunwayDirection.Runway12, RunwayDirection.Runway30,
                    Heading12, Heading30, RunwayDirection.Runway12);
            if (Math.Abs(h30 - h12) < 3.0)
                return PreferPair(destination, home, RunwayDirection.Runway12, RunwayDirection.Runway30,
                    Heading12, Heading30, h30 > h12 ? RunwayDirection.Runway30 : RunwayDirection.Runway12);
            return h30 > h12 ? RunwayDirection.Runway30 : RunwayDirection.Runway12;
        }

        /// <summary>Jets need the 3 100 m 05/23. Turboprops may use 12/30.</summary>
        public static bool AllowsCrossRunway(AircraftType type) =>
            type != null && !AirlineOperations.NeedsTerminalGate(type);

        public static bool IsMainRunway(RunwayDirection direction) =>
            direction is RunwayDirection.Runway05 or RunwayDirection.Runway23;

        public static int HeadingDegrees(RunwayDirection direction) => direction switch
        {
            RunwayDirection.Runway23 => Heading23,
            RunwayDirection.Runway12 => Heading12,
            RunwayDirection.Runway30 => Heading30,
            _ => Heading05
        };

        private static RunwayDirection PreferPair(Destination? destination, Destination? home,
            RunwayDirection first, RunwayDirection second, int firstHeading, int secondHeading,
            RunwayDirection fallback)
        {
            if (!destination.HasValue || !home.HasValue)
                return fallback;
            var heading = HeadingTo(home.Value, destination.Value);
            var toFirst = Math.Abs(Wrap180(heading - firstHeading));
            var toSecond = Math.Abs(Wrap180(heading - secondHeading));
            return toSecond < toFirst ? second : first;
        }

        private static double HeadingTo(Destination from, Destination to)
        {
            var phi1 = from.Latitude * Math.PI / 180.0;
            var phi2 = to.Latitude * Math.PI / 180.0;
            var dLon = (to.Longitude - from.Longitude) * Math.PI / 180.0;
            var y = Math.Sin(dLon) * Math.Cos(phi2);
            var x = Math.Cos(phi1) * Math.Sin(phi2) - Math.Sin(phi1) * Math.Cos(phi2) * Math.Cos(dLon);
            var deg = Math.Atan2(y, x) * 180.0 / Math.PI;
            return deg < 0 ? deg + 360 : deg;
        }

        private static double Wrap180(double degrees)
        {
            while (degrees > 180)
                degrees -= 360;
            while (degrees < -180)
                degrees += 360;
            return degrees;
        }

        private static double Headwind(SurfaceWind wind, int runwayHeading) =>
            wind.Knots * Math.Cos((wind.DirectionDegrees - runwayHeading) * Math.PI / 180.0);

        public static string Label(RunwayDirection direction) => direction switch
        {
            RunwayDirection.Runway23 => "23",
            RunwayDirection.Runway12 => "12",
            RunwayDirection.Runway30 => "30",
            _ => "05"
        };
    }
}
