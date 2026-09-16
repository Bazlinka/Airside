using System;
using System.Globalization;

namespace Airside.Presentation
{
    /// <summary>
    /// Pure mapping from simulation daylight to what the field should show.
    /// Presentation only — the pin is a debug override, not a sim rule.
    /// </summary>
    public static class DaylightPresentation
    {
        public const string ReviewTimeFlag = "-airsideReviewTime";

        /// <summary>
        /// When <paramref name="pinToNoon"/> is true, always return full daylight (1).
        /// Otherwise pass through the simulation's 0..1 daylight factor.
        /// </summary>
        public static float Resolve(bool pinToNoon, double simulationDaylight)
        {
            if (pinToNoon)
                return 1f;
            if (simulationDaylight <= 0.0)
                return 0f;
            if (simulationDaylight >= 1.0)
                return 1f;
            return (float)simulationDaylight;
        }

        /// <summary>
        /// Reads the presentation-only local-time override used by packaged review shots.
        /// It never changes the simulation clock, schedules, weather, or save data.
        /// </summary>
        public static TimeSpan? ReviewLocalTime(string[] args)
        {
            if (args == null)
                return null;

            var index = Array.IndexOf(args, ReviewTimeFlag);
            if (index < 0 || index + 1 >= args.Length)
                return null;

            return TimeSpan.TryParseExact(args[index + 1], @"hh\:mm", CultureInfo.InvariantCulture,
                out var localTime)
                ? localTime
                : null;
        }
    }
}
