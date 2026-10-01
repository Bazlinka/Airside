using System;

namespace Airside.Presentation
{
    /// <summary>Presentation cloud deck tied to the existing 1100 m stratus layer.</summary>
    public static class CockpitWeatherEnvelope
    {
        public static float InCloud(float height, float cover)
        {
            var coverage = Clamp((cover - 0.3f) / 0.4f);
            return coverage * Smooth((height - 900f) / 200f) * (1f - Smooth((height - 1350f) / 250f));
        }

        public static float RainAtHeight(float height) => 1f - Smooth((height - 1350f) / 250f);
        private static float Clamp(float value) => Math.Max(0f, Math.Min(1f, value));
        private static float Smooth(float value) { var t = Clamp(value); return t * t * (3f - 2f * t); }
    }
}
