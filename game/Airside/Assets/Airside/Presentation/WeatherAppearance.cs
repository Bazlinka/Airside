using System;
using Airside.Domain;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>Visual weather varieties share the existing operational weather identity.</summary>
    public static class WeatherAppearance
    {
        private static float Smooth(float value) { var t = Math.Clamp(value, 0f, 1f); return t * t * (3f - 2f * t); }

        // A quarter of the bodies form a thin high layer on fair days. Continuous
        // weights ensure that a weather transition changes shape without replacing models.
        public static float Cirrus(int index, float cover, float storm) => index % 4 == 0
            ? (1f - Smooth((cover - 0.25f) / 0.4f)) * (1f - storm) : 0f;
        public static float Stratus(float cover, float storm) => Smooth((cover - 0.55f) / 0.3f) * (1f - storm);

        public static WeatherLook Variant(WeatherKind kind, long block)
        {
            var choice = (int)(((ulong)block * 1664525UL + 1013904223UL) % 3UL);
            // Keep weather categories, RNG sequence and airport safety decisions unchanged.
            return kind switch
            {
                WeatherKind.Cloudy when choice == 0 => new WeatherLook(0.28f, 0f, 0.10f, 0.97f, 0f),
                WeatherKind.Cloudy when choice == 1 => new WeatherLook(0.62f, 0f, 0.19f, 0.90f, 0f),
                WeatherKind.Rain when choice == 0 => new WeatherLook(0.90f, 0.22f, 0.25f, 0.72f, 0.40f),
                WeatherKind.Rain when choice == 1 => new WeatherLook(0.64f, 0.68f, 0.25f, 0.74f, 0.55f),
                _ => WeatherLook.For(kind)
            };
        }

        public static WeatherLook Forecast(SimulationTime now)
        {
            var block = now.ElapsedSeconds / Weather.BlockSeconds;
            var current = Variant(Weather.At(now), block);
            var into = now.ElapsedSeconds % Weather.BlockSeconds;
            if (block == 0 || into >= Weather.BlendSeconds) return current;
            var previous = Variant(Weather.At(new SimulationTime(block * Weather.BlockSeconds - 1)), block - 1);
            return WeatherLook.Lerp(previous, current, Smooth(into / (float)Weather.BlendSeconds));
        }

        public static string Describe(WeatherKind kind, WeatherLook look) => kind switch
        {
            WeatherKind.Storm => "Thunderstorm",
            WeatherKind.Fog => look.VisibilityMetres > 1200f ? "Mist" : "Fog",
            WeatherKind.Rain when look.Precipitation < 0.35f => "Drizzle",
            WeatherKind.Rain when look.CloudCover < 0.8f => "Showers",
            WeatherKind.Rain => "Rain",
            WeatherKind.Overcast => "Overcast",
            WeatherKind.Cloudy when look.CloudCover >= 0.55f => "Broken cloud",
            WeatherKind.Cloudy => "Scattered cloud",
            _ => look.VisibilityMetres < 10000f ? "Haze" : "Clear · high wisps"
        };
    }
}
