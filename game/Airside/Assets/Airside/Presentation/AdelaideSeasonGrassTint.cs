using System;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>
    /// Adelaide seasonal dry-grass tint (ADR 0209). Pure maths — Surroundings
    /// bakes the dryness into vertex colours once at field build.
    /// </summary>
    public static class AdelaideSeasonGrassTint
    {
        /// <summary>Peak dry (straw) — mid January.</summary>
        public const int PeakDryDayOfYear = 15;

        /// <summary>Peak green — mid July (southern hemisphere winter).</summary>
        public const int PeakGreenDayOfYear = 196;

        /// <summary>
        /// 0 = winter green (identity), 1 = late-summer straw.
        /// </summary>
        public static float Dryness01(int dayOfYear)
        {
            var day = ((dayOfYear - 1) % 365 + 365) % 365 + 1;
            var angle = (day - PeakDryDayOfYear) * (Math.PI * 2.0 / 365.0);
            return (float)(0.5 + 0.5 * Math.Cos(angle));
        }

        public static bool AppliesTo(AdelaideLandCover.Kind cover) =>
            cover == AdelaideLandCover.Kind.None
            || cover == AdelaideLandCover.Kind.Park
            || cover == AdelaideLandCover.Kind.Scrub;

        /// <summary>
        /// Push sRGB grass toward straw as dryness → 1; identity at 0.
        /// </summary>
        public static void ApplyRgb(ref float r, ref float g, ref float b, float dryness01)
        {
            var t = Math.Max(0f, Math.Min(1f, dryness01));
            if (t <= 0f)
                return;
            // Straw: lift red/amber, soften green, slight brown in blue.
            var strawR = Math.Min(1f, r * 1.08f + 0.10f);
            var strawG = Math.Max(0f, g * 0.88f - 0.02f);
            var strawB = Math.Max(0f, b * 0.78f);
            r = r + (strawR - r) * t;
            g = g + (strawG - g) * t;
            b = b + (strawB - b) * t;
        }

        public static void ApplyRgb(ref float r, ref float g, ref float b, int dayOfYear) =>
            ApplyRgb(ref r, ref g, ref b, Dryness01(dayOfYear));
    }
}
