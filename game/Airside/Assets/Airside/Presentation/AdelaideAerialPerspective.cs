using System;

namespace Airside.Presentation
{
    /// <summary>
    /// Distance + height aerial haze for far land (ADR 0210). Pure maths — Outer
    /// terrain bakes cooler hill colours so the Mount Lofty ring reads as haze,
    /// not a hard brown silhouette. Baked by range from the airfield, not the camera, so it
    /// must stay a tint: the zoomed-out overview looks straight down on this ring, and the
    /// camera-distance fade in Airside/Surroundings already hazes it from the ground.
    /// </summary>
    public static class AdelaideAerialPerspective
    {
        /// <summary>
        /// Haze starts easing in past this horizontal range (metres): the edge of the satellite
        /// disc, which carries no baked haze, so the two meet without a seam.
        /// </summary>
        public const float FadeStartMetres = 30000f;

        /// <summary>Full haze by this range (metres): the outer edge of the far DEM.</summary>
        public const float FadeEndMetres = 96000f;

        /// <summary>Strongest pull toward <see cref="HazeLinear"/>; beyond this land reads as cloud from above.</summary>
        public const float MaxStrength = 0.3f;

        /// <summary>Cool blue-grey haze target (linear).</summary>
        public static readonly float[] HazeLinear = { 0.52f, 0.58f, 0.68f };

        /// <summary>
        /// 0 inside the satellite disc, up to <see cref="MaxStrength"/> at the edge of the far DEM.
        /// Height above the plain strengthens haze (ridges dissolve first).
        /// </summary>
        public static float Strength01(float distanceMetres, float heightAboveSea)
        {
            var t = (distanceMetres - FadeStartMetres) / (FadeEndMetres - FadeStartMetres);
            t = Math.Max(0f, Math.Min(1f, t));
            t = t * t * (3f - 2f * t);
            var heightBoost = Math.Max(0f, Math.Min(1f, (heightAboveSea - 80f) / 400f));
            var s = Math.Max(0f, Math.Min(1f, t * (0.75f + 0.25f * heightBoost) + heightBoost * 0.15f * t));
            return s * MaxStrength;
        }

        public static void ApplyRgb(ref float r, ref float g, ref float b,
            float distanceMetres, float heightAboveSea)
        {
            var s = Strength01(distanceMetres, heightAboveSea);
            if (s <= 0f)
                return;
            r = r + (HazeLinear[0] - r) * s;
            g = g + (HazeLinear[1] - g) * s;
            b = b + (HazeLinear[2] - b) * s;
        }
    }
}
