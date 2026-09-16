using System;
using Airside.Domain;

namespace Airside.Simulation
{
    public enum RunwayDirection
    {
        Runway05,
        Runway23
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

        /// <summary>Use the runway end with the larger headwind component; calm defaults to 05.</summary>
        public static RunwayDirection Select(SurfaceWind wind)
        {
            if (wind.Knots < 3)
                return RunwayDirection.Runway05;
            return Headwind(wind, 230) > Headwind(wind, 50)
                ? RunwayDirection.Runway23
                : RunwayDirection.Runway05;
        }

        private static double Headwind(SurfaceWind wind, int runwayHeading) =>
            wind.Knots * Math.Cos((wind.DirectionDegrees - runwayHeading) * Math.PI / 180.0);

        public static string Label(RunwayDirection direction) =>
            direction == RunwayDirection.Runway23 ? "23" : "05";
    }
}
