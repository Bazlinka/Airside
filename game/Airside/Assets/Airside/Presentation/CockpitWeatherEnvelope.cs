using System;

namespace Airside.Presentation
{
    /// <summary>Presentation cloud deck tied to the low stratus deck and developed thunderstorm tops.</summary>
    public static class CockpitWeatherEnvelope
    {
        public static float InCloud(float height, float cover)
        {
            var coverage = Clamp((cover - 0.3f) / 0.4f);
            return coverage * Smooth((height - 900f) / 200f) * (1f - Smooth((height - 1350f) / 250f));
        }

        public const float StormTopMetres = 10000f;

        public static float AboveDeck(float height, float cover, float stormDepth = 0f)
        {
            var top = 1600f + (StormTopMetres - 1600f) * Clamp(stormDepth);
            var transition = 250f + 550f * Clamp(stormDepth);
            return Clamp((cover - 0.3f) / 0.4f) * Smooth((height - top + transition) / transition);
        }

        public static float SkyCover(float height, float cover, float stormDepth = 0f)
            => Clamp(cover) * (1f - AboveDeck(height, cover, stormDepth));

        public static float RainAtHeight(float height, float stormDepth = 0f)
        {
            // Liquid rain fades in the cold upper storm; cloud tops may extend much higher.
            var top = 1600f + 2900f * Clamp(stormDepth);
            var transition = 250f + 500f * Clamp(stormDepth);
            return 1f - Smooth((height - top + transition) / transition);
        }

        /// <summary>Approximate occupied storm body in the same normalised space as WeatherVolume.</summary>
        public static float StormBody(float x, float y, float z)
        {
            float Ellipsoid(float cx, float cy, float sx, float sy, float sz)
                => 1f - (float)Math.Sqrt((x-cx)*(x-cx)*sx*sx + (y-cy)*(y-cy)*sy*sy + z*z*sz*sz);
            var tower = Ellipsoid(0f, -0.06f, 3.4f, 2.4f, 3.5f);
            var anvil = Ellipsoid(0.08f, 0.32f, 2.1f, 6.5f, 2.2f);
            return Smooth(Math.Max(tower, anvil) * 4f);
        }
        private static float Clamp(float value) => Math.Max(0f, Math.Min(1f, value));
        private static float Smooth(float value) { var t = Clamp(value); return t * t * (3f - 2f * t); }
    }
}
