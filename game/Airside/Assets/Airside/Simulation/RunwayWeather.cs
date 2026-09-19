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

        /// <summary>Use the runway end with the larger headwind component; calm defaults to 05.</summary>
        public static RunwayDirection Select(SurfaceWind wind) =>
            Select(wind, null, null, null);

        /// <summary>
        /// Tower runway on 05/23. Jets take the dest-aligned end through a wider
        /// wind tie; regionals follow the wind more tightly. 12/30 is named on the
        /// field; taxi-out still only reaches the 05/23 holds.
        /// </summary>
        public static RunwayDirection Select(SurfaceWind wind, AircraftType type,
            Destination? destination, Destination? home)
        {
            var h05 = Headwind(wind, Heading05);
            var h23 = Headwind(wind, Heading23);
            // Jets stay on 05/23 (the 3 100 m strip). They will take the dest-aligned
            // end through a wider wind tie so a Perth 787 does not climb out inland.
            // Regionals follow the wind more tightly. 12/30 is named on the field
            // but taxi-out still only reaches the 05/23 holds.
            var tieKnots = AllowsCrossRunway(type) ? 3.0 : 8.0;

            if (wind.Knots < 3)
                return PreferDestination(destination, home, RunwayDirection.Runway05);
            if (Math.Abs(h23 - h05) < tieKnots)
                return PreferDestination(destination, home, h23 > h05
                    ? RunwayDirection.Runway23
                    : RunwayDirection.Runway05);
            return h23 > h05 ? RunwayDirection.Runway23 : RunwayDirection.Runway05;
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

        private static RunwayDirection PreferDestination(Destination? destination, Destination? home,
            RunwayDirection fallback)
        {
            if (!destination.HasValue || !home.HasValue)
                return fallback;
            var heading = HeadingTo(home.Value, destination.Value);
            var to05 = Math.Abs(Wrap180(heading - Heading05));
            var to23 = Math.Abs(Wrap180(heading - Heading23));
            return to23 < to05 ? RunwayDirection.Runway23 : RunwayDirection.Runway05;
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
