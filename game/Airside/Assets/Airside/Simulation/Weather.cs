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
        public const long BlockSeconds = 300; // a new sky every five simulated minutes

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
    }
}
