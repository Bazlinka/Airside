using System;
using System.Globalization;

namespace Airside.Domain
{
    /// <summary>
    /// Live airline time (ADR 0045, Bailey 2026-09-14): simulation time is seconds since
    /// <see cref="EpochUtcTicks"/>, and one simulated second is one real second, so the
    /// game clock is the real time in Adelaide — daylight saving included.
    /// </summary>
    public sealed class AirlineClock
    {
        /// <summary>A fixed epoch for tests and saves that predate live time: 08:00 ACST, 14 Sep 2026.</summary>
        public static readonly DateTime DefaultEpochUtc = new(2026, 9, 13, 22, 30, 0, DateTimeKind.Utc);

        private static TimeZoneInfo _adelaide;

        public AirlineClock(long epochUtcTicks)
        {
            if (epochUtcTicks <= 0)
                throw new ArgumentOutOfRangeException(nameof(epochUtcTicks));
            EpochUtcTicks = epochUtcTicks;
        }

        /// <summary>Real UTC instant of simulation time zero.</summary>
        public long EpochUtcTicks { get; }

        public static AirlineClock Default => new(DefaultEpochUtc.Ticks);

        /// <summary>A clock on which <paramref name="time"/> is the real instant <paramref name="utc"/>.</summary>
        public static AirlineClock Aligned(SimulationTime time, DateTime utc) =>
            new(utc.ToUniversalTime().Ticks - time.ElapsedSeconds * TimeSpan.TicksPerSecond);

        /// <summary>Adelaide's zone (ACST +9:30 / ACDT +10:30), with a fixed +9:30 fallback.</summary>
        public static TimeZoneInfo Adelaide
        {
            get
            {
                if (_adelaide != null)
                    return _adelaide;
                foreach (var id in new[] { "Australia/Adelaide", "Cen. Australia Standard Time" })
                {
                    try
                    {
                        return _adelaide = TimeZoneInfo.FindSystemTimeZoneById(id);
                    }
                    catch (Exception)
                    {
                        // Try the next id; zone databases differ by platform.
                    }
                }

                return _adelaide = TimeZoneInfo.CreateCustomTimeZone("ACST", TimeSpan.FromMinutes(570), "Adelaide", "ACST");
            }
        }

        /// <summary>Precise seconds since the epoch at a real UTC instant; never negative.</summary>
        public double SecondsAt(DateTime utc) =>
            Math.Max(0.0, (utc.ToUniversalTime().Ticks - EpochUtcTicks) / (double)TimeSpan.TicksPerSecond);

        /// <summary>Whole simulation seconds at a real UTC instant.</summary>
        public SimulationTime At(DateTime utc) => new((long)Math.Floor(SecondsAt(utc)));

        /// <summary>Whole simulation seconds at an Adelaide wall-clock time (DST-aware), never before the epoch.</summary>
        public SimulationTime AtLocal(DateTime adelaideLocal)
        {
            var utc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(adelaideLocal, DateTimeKind.Unspecified), Adelaide);
            return new SimulationTime((long)Math.Ceiling(SecondsAt(utc)));
        }

        public DateTime LocalAt(SimulationTime time) =>
            TimeZoneInfo.ConvertTimeFromUtc(
                new DateTime(EpochUtcTicks + time.ElapsedSeconds * TimeSpan.TicksPerSecond, DateTimeKind.Utc), Adelaide);

        public string TimeText(SimulationTime time) => LocalAt(time).ToString("HH:mm", CultureInfo.InvariantCulture);

        public string DateText(SimulationTime time) => LocalAt(time).ToString("ddd d MMM", CultureInfo.InvariantCulture);

        public static string DurationText(long seconds)
        {
            var minutes = (long)Math.Round(Math.Max(0, seconds) / 60.0);
            if (minutes >= 24 * 60)
                return $"{minutes / (24 * 60)} d {minutes / 60 % 24} h";
            return minutes >= 60 ? $"{minutes / 60} h {minutes % 60:00} min" : $"{minutes} min";
        }
    }
}
