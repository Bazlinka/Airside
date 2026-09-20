using Airside.Domain;

namespace Airside.Simulation
{
    public enum WeatherKind
    {
        Clear,
        Cloudy,
        Overcast,
        Rain,
        Fog,
        Storm
    }

    /// <summary>
    /// Deterministic weather. It changes every <see cref="BlockSeconds"/> and is a
    /// pure function of the simulated timeline (no random source), so it is
    /// identical under live play and offline catch-up and does not disturb the
    /// gameplay random sequence. Weather is informational plus a daily operating
    /// cost — it does not change flight timing.
    /// </summary>
    public static class Weather
    {
        // A new sky every simulated hour. Five minutes suited the old 20-minute day; on a
        // real 24-hour day at 60x it flickered rain on and off every five seconds.
        public const long BlockSeconds = 3600;

        public static WeatherKind At(SimulationTime now)
        {
            var block = (ulong)(now.ElapsedSeconds / BlockSeconds);
            // xorshift-style hash of the block index — stable, well spread.
            var h = (uint)(block * 2654435761UL);
            h ^= h >> 15;
            h *= 2246822519u;
            h ^= h >> 13;

            var r = h % 100u;
            if (r < 34) return WeatherKind.Clear;
            if (r < 60) return WeatherKind.Cloudy;
            if (r < 78) return WeatherKind.Overcast;
            if (r < 90) return WeatherKind.Rain;
            if (r < 97) return WeatherKind.Fog;
            return WeatherKind.Storm;
        }

        public static bool IsAdverse(WeatherKind kind) =>
            kind == WeatherKind.Rain || kind == WeatherKind.Fog || kind == WeatherKind.Storm;

        public static string Describe(WeatherKind kind) => kind switch
        {
            WeatherKind.Clear => "Clear",
            WeatherKind.Cloudy => "Cloudy",
            WeatherKind.Overcast => "Overcast",
            WeatherKind.Rain => "Rain",
            WeatherKind.Fog => "Fog",
            WeatherKind.Storm => "Storm",
            _ => "Clear"
        };

        /// <summary>Presentation knobs for a forecast. Does not change flight timing (ADR 0013).</summary>
        public static WeatherLook Look(WeatherKind kind) => WeatherLook.For(kind);
    }

    /// <summary>
    /// Shared look for cloudy / overcast / rain / fog / storm. Presentation reads
    /// these instead of scattering magic numbers. Timing stays informational.
    /// </summary>
    public readonly struct WeatherLook
    {
        public WeatherLook(float cloudCover, float precipitation, float gloom, float visibility, float wetness)
        {
            CloudCover = cloudCover;
            Precipitation = precipitation;
            Gloom = gloom;
            Visibility = visibility;
            Wetness = wetness;
        }

        /// <summary>0 clear sky … 1 a solid overcast sheet.</summary>
        public float CloudCover { get; }

        /// <summary>0 dry … 1 storm rain.</summary>
        public float Precipitation { get; }

        /// <summary>0 bright … 1 storm gloom (sun and ambient dim).</summary>
        public float Gloom { get; }

        /// <summary>1 unlimited … 0 socked-in fog.</summary>
        public float Visibility { get; }

        /// <summary>0 dry pavement … 1 soaked.</summary>
        public float Wetness { get; }

        public bool IsRaining => Precipitation > 0.05f;

        public static WeatherLook For(WeatherKind kind) => kind switch
        {
            WeatherKind.Cloudy => new WeatherLook(0.45f, 0f, 0.16f, 0.92f, 0f),
            WeatherKind.Overcast => new WeatherLook(0.78f, 0f, 0.22f, 0.82f, 0f),
            WeatherKind.Rain => new WeatherLook(0.88f, 0.55f, 0.28f, 0.68f, 0.52f),
            WeatherKind.Fog => new WeatherLook(0.70f, 0f, 0.42f, 0.38f, 0.14f),
            WeatherKind.Storm => new WeatherLook(0.95f, 1f, 0.55f, 0.48f, 0.72f),
            _ => new WeatherLook(0.12f, 0f, 0f, 1f, 0f)
        };
    }
}
