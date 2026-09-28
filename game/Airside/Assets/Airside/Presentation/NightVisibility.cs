using System;

namespace Airside.Presentation
{
    /// <summary>
    /// The player's night brightness option (ADR 0167). Natural is the ADR 0063/0064 night; the
    /// higher levels lift exposure and ambient light at night only, the way the eye adapts on a
    /// dark apron. Daylight is untouched at every level.
    /// </summary>
    public static class NightVisibility
    {
        public static readonly string[] Labels = { "Natural", "Brighter", "Brightest" };
        public const int DefaultLevel = 1;

        private static readonly float[] ExposureEv = { 0f, 0.35f, 0.7f };
        private static readonly float[] AmbientLift = { 0f, 0.3f, 0.65f };

        public static int Clamp(int level) => Math.Max(0, Math.Min(Labels.Length - 1, level));

        /// <summary>How much of the lift applies: all of it once daylight is below 0.1, none above 0.45.</summary>
        public static float NightWeight(float daylight)
        {
            var x = Math.Max(0f, Math.Min(1f, (0.45f - daylight) / 0.35f));
            return x * x * (3f - 2f * x);
        }

        /// <summary>Extra post-exposure (EV) at this level and daylight.</summary>
        public static float ExposureLift(int level, float daylight) => ExposureEv[Clamp(level)] * NightWeight(daylight);

        /// <summary>Multiplier on the ambient intensity at this level and daylight.</summary>
        public static float AmbientGain(int level, float daylight) => 1f + AmbientLift[Clamp(level)] * NightWeight(daylight);
    }
}
