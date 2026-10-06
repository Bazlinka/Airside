using System;
using Airside.Simulation;

namespace Airside.Presentation
{
    public readonly struct WeatherFlow
    {
        public WeatherFlow(float x, float z) { X = x; Z = z; }
        public float X { get; }
        public float Z { get; }
    }

    /// <summary>World drift travels away from the meteorological wind-from bearing.</summary>
    public static class WeatherWindFlow
    {
        public static WeatherFlow FromBearing(float fromDegrees, float magnitude)
        {
            var radians = RunwayWeather.UnityYawFromTrue(fromDegrees + 180f) * Math.PI / 180.0;
            return new WeatherFlow((float)Math.Sin(radians) * magnitude, (float)Math.Cos(radians) * magnitude);
        }

        // Keep existing presentation speeds, including gentle movement at calm.
        public static WeatherFlow Cloud(SurfaceWind wind, bool bareField)
            => FromBearing(wind.DirectionDegrees, bareField ? Math.Max(1.5f, wind.Knots * 0.5144f) * 1.6f : 0.35f);
        public static WeatherFlow Rain(SurfaceWind wind, bool storm)
            => FromBearing(wind.DirectionDegrees, storm ? 6f : 2.5f);
        public static WeatherFlow ShaderGlobal(SurfaceWind wind)
            => FromBearing(wind.DirectionDegrees, 7f);
    }
}
