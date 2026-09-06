using System;

namespace Airside.Domain
{
    public enum DayPhase
    {
        Night,
        Dawn,
        Day,
        Dusk
    }

    /// <summary>
    /// The local time of day at the airport, derived purely from the simulation
    /// clock. In the prototype the simulation clock is treated as local time, one
    /// simulated day every <see cref="DaySeconds"/> seconds, starting at 08:00 on
    /// day one. Aligning this to real-world wall time comes with the companion app.
    /// </summary>
    public readonly struct DayCycle
    {
        public const long DaySeconds = 1200; // one simulated day every 20 minutes
        private const double StartHour = 8.0;

        public DayCycle(SimulationTime now, long daySeconds = DaySeconds)
        {
            if (daySeconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(daySeconds));

            DaysElapsed = (int)((now.ElapsedSeconds + StartHour / 24.0 * daySeconds) / daySeconds);

            var raw = (now.ElapsedSeconds + StartHour / 24.0 * daySeconds) / daySeconds;
            Fraction = raw - Math.Floor(raw);
        }

        /// <summary>0 at local midnight, 0.5 at midday, wrapping at 1.</summary>
        public double Fraction { get; }

        /// <summary>Whole simulated days since the game began.</summary>
        public int DaysElapsed { get; }

        public double LocalHour => Fraction * 24.0;
        public int Hour => (int)LocalHour;
        public int Minute => (int)((LocalHour - Hour) * 60.0);

        public DayPhase Phase
        {
            get
            {
                var h = LocalHour;
                if (h < 5.0 || h >= 20.0) return DayPhase.Night;
                if (h < 7.0) return DayPhase.Dawn;
                if (h < 18.0) return DayPhase.Day;
                return DayPhase.Dusk;
            }
        }

        /// <summary>
        /// 0 while the sun is down, rising through dawn to 1 at midday and back down
        /// through dusk — drives light intensity, colour and ambient.
        /// </summary>
        public double Daylight
        {
            get
            {
                var h = LocalHour;
                if (h <= 5.0 || h >= 20.0) return 0.0;
                if (h < 7.0) return (h - 5.0) / 2.0;   // dawn ramp
                if (h <= 17.0) return 1.0;             // full day
                if (h < 20.0) return (20.0 - h) / 3.0; // dusk ramp
                return 0.0;
            }
        }

        /// <summary>Sun elevation angle in degrees; negative before dawn / after dusk.</summary>
        public double SunElevationDegrees => -6.0 + 66.0 * Math.Sin(Math.PI * Math.Max(0.0, Math.Min(1.0, (LocalHour - 5.0) / 15.0)));

        public string Clock => $"{Hour:00}:{Minute:00}";
    }
}
