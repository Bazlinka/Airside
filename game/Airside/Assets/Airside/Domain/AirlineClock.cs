using System;

namespace Airside.Domain
{
    /// <summary>
    /// How airline time reads to the player (ADR 0045): the same real 24-hour day as
    /// <see cref="DayCycle"/>, starting at 08:00 on day 1.
    /// </summary>
    public static class AirlineClock
    {
        public const long DayLengthSeconds = DayCycle.DaySeconds;
        public const long StartSeconds = 8 * 3600;

        public static string TimeText(SimulationTime time)
        {
            var local = (StartSeconds + time.ElapsedSeconds) % DayLengthSeconds;
            return $"{local / 3600:00}:{local % 3600 / 60:00}";
        }

        public static long DayNumber(SimulationTime time) => (StartSeconds + time.ElapsedSeconds) / DayLengthSeconds + 1;

        public static string DurationText(long seconds)
        {
            var minutes = (long)Math.Round(Math.Max(0, seconds) / 60.0);
            if (minutes >= 24 * 60)
                return $"{minutes / (24 * 60)} d {minutes / 60 % 24} h";
            return minutes >= 60 ? $"{minutes / 60} h {minutes % 60:00} min" : $"{minutes} min";
        }
    }
}
