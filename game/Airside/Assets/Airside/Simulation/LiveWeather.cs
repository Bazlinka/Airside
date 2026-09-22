using System;
using System.Globalization;

namespace Airside.Simulation
{
    /// <summary>
    /// One presentation-only Adelaide forecast sample. Nothing in the operational
    /// simulation reads this type: runway choice, ground stops, saves and replay keep
    /// using the deterministic <see cref="Weather"/> and <see cref="RunwayWeather"/>.
    /// </summary>
    public readonly struct LiveWeatherSnapshot
    {
        public LiveWeatherSnapshot(WeatherKind kind, WeatherLook look, SurfaceWind wind,
            float temperatureCelsius, float humidityPercent, float precipitationMillimetres,
            float visibilityMetres, float cloudCover)
        {
            Kind = kind;
            Look = look;
            Wind = wind;
            TemperatureCelsius = temperatureCelsius;
            HumidityPercent = humidityPercent;
            PrecipitationMillimetres = precipitationMillimetres;
            VisibilityMetres = visibilityMetres;
            CloudCover = cloudCover;
        }

        public WeatherKind Kind { get; }
        public WeatherLook Look { get; }
        public SurfaceWind Wind { get; }
        public float TemperatureCelsius { get; }
        public float HumidityPercent { get; }
        public float PrecipitationMillimetres { get; }
        public float VisibilityMetres { get; }
        public float CloudCover { get; }
    }

    /// <summary>
    /// Open-Meteo forecast request and a deliberately small parser for its flat
    /// <c>current</c> object. The fixed airport coordinate is the only location sent.
    /// </summary>
    public static class LiveWeather
    {
        public const int PollSeconds = 15 * 60;
        public const int StaleSeconds = 2 * 60 * 60;
        public const string Credit = "Weather · Open-Meteo (CC BY 4.0)";

        private const double Latitude = -34.945;
        private const double Longitude = 138.531;

        public static string RequestUrl() =>
            "https://api.open-meteo.com/v1/forecast" +
            $"?latitude={Latitude.ToString(CultureInfo.InvariantCulture)}" +
            $"&longitude={Longitude.ToString(CultureInfo.InvariantCulture)}" +
            "&current=temperature_2m,relative_humidity_2m,precipitation,rain,showers," +
            "weather_code,cloud_cover,visibility,wind_speed_10m,wind_direction_10m,wind_gusts_10m" +
            "&wind_speed_unit=kn&timezone=Australia%2FAdelaide&forecast_days=1";

        public static bool TryParse(string json, out LiveWeatherSnapshot snapshot)
        {
            snapshot = default;
            if (string.IsNullOrWhiteSpace(json))
                return false;

            var currentAt = json.IndexOf("\"current\"", StringComparison.Ordinal);
            if (currentAt < 0)
                return false;
            var currentStart = json.IndexOf('{', currentAt);
            if (currentStart < 0)
                return false;
            var currentEnd = MatchingBrace(json, currentStart);
            if (currentEnd <= currentStart)
                return false;
            var current = json.Substring(currentStart, currentEnd - currentStart + 1);

            if (!TryNumber(current, "weather_code", out var code)
                || !TryNumber(current, "cloud_cover", out var cloudPercent)
                || !TryNumber(current, "precipitation", out var precipitation)
                || !TryNumber(current, "visibility", out var visibility)
                || !TryNumber(current, "wind_speed_10m", out var windSpeed)
                || !TryNumber(current, "wind_direction_10m", out var windDirection)
                || !TryNumber(current, "temperature_2m", out var temperature))
                return false;

            TryNumber(current, "relative_humidity_2m", out var humidity);
            if (!Finite(code) || !Finite(cloudPercent) || !Finite(precipitation)
                || !Finite(visibility) || !Finite(windSpeed) || !Finite(windDirection)
                || !Finite(temperature))
                return false;

            var cloud = Clamp01((float)cloudPercent / 100f);
            var rain = Math.Max(0f, (float)precipitation);
            var metres = Math.Max(50f, (float)visibility);
            var kind = Classify((int)Math.Round(code), cloud, rain, metres);
            var look = LookFor(kind, cloud, rain, metres);
            var wind = new SurfaceWind((int)Math.Round(windDirection), (int)Math.Round(Math.Max(0d, windSpeed)));
            snapshot = new LiveWeatherSnapshot(
                kind,
                look,
                wind,
                (float)temperature,
                (float)Math.Max(0d, Math.Min(100d, humidity)),
                rain,
                metres,
                cloud);
            return true;
        }

        public static WeatherKind Classify(int wmoCode, float cloudCover, float precipitationMillimetres,
            float visibilityMetres)
        {
            if (wmoCode is 95 or 96 or 99)
                return WeatherKind.Storm;
            if (wmoCode is 45 or 48 || visibilityMetres < 1200f)
                return WeatherKind.Fog;
            if ((wmoCode >= 51 && wmoCode <= 67) || (wmoCode >= 80 && wmoCode <= 82)
                || precipitationMillimetres > 0.05f)
                return WeatherKind.Rain;
            if (wmoCode == 3 || cloudCover >= 0.82f)
                return WeatherKind.Overcast;
            if (wmoCode is 1 or 2 || cloudCover >= 0.24f)
                return WeatherKind.Cloudy;
            return WeatherKind.Clear;
        }

        /// <summary>
        /// Preserve the authored weather identity while using the feed's continuous cloud,
        /// precipitation and visibility values for less step-like presentation.
        /// </summary>
        public static WeatherLook LookFor(WeatherKind kind, float cloudCover,
            float precipitationMillimetres, float visibilityMetres)
        {
            cloudCover = Clamp01(cloudCover);
            var rain = Clamp01(PrecipitationScale(precipitationMillimetres));
            var visibility = Clamp01((visibilityMetres - 350f) / 19650f);
            var authored = WeatherLook.For(kind);

            var precipitation = Math.Max(authored.Precipitation * 0.55f, rain);
            var gloom = Math.Max(authored.Gloom * 0.65f,
                cloudCover * 0.28f + precipitation * 0.22f + (1f - visibility) * 0.34f);
            var wetness = Math.Max(authored.Wetness * 0.55f, precipitation * 0.78f);
            if (kind == WeatherKind.Fog)
                wetness = Math.Max(wetness, 0.10f);

            return new WeatherLook(
                Math.Max(cloudCover, authored.CloudCover * 0.55f),
                Clamp01(precipitation),
                Clamp01(gloom),
                Math.Min(visibility, authored.Visibility + 0.12f),
                Clamp01(wetness));
        }

        private static float PrecipitationScale(float millimetres)
        {
            if (millimetres <= 0f)
                return 0f;
            // Current precipitation is a 15-minute model amount. A square-root response
            // lets drizzle read without making ordinary rain look like a permanent storm.
            return (float)Math.Sqrt(Math.Min(1f, millimetres / 2.5f));
        }

        private static bool TryNumber(string json, string key, out double value)
        {
            value = 0d;
            var token = "\"" + key + "\"";
            var at = json.IndexOf(token, StringComparison.Ordinal);
            if (at < 0)
                return false;
            at = json.IndexOf(':', at + token.Length);
            if (at < 0)
                return false;
            at++;
            while (at < json.Length && char.IsWhiteSpace(json[at])) at++;
            var end = at;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] is '-' or '+' or '.' or 'e' or 'E')) end++;
            return end > at && double.TryParse(json.Substring(at, end - at), NumberStyles.Float,
                CultureInfo.InvariantCulture, out value);
        }

        private static int MatchingBrace(string text, int start)
        {
            var depth = 0;
            var quoted = false;
            for (var i = start; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '"' && (i == 0 || text[i - 1] != '\\'))
                    quoted = !quoted;
                if (quoted)
                    continue;
                if (c == '{') depth++;
                if (c == '}' && --depth == 0) return i;
            }
            return -1;
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    }
}
