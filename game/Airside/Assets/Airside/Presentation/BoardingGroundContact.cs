using System;

namespace Airside.Presentation
{
    /// <summary>Physical boarding pavement, independent of the aircraft motion-root datum.</summary>
    public static class BoardingGroundContact
    {
        public static float AdelaideApronY => AirsideBareField.RunwayCenterY
            + AirsideBareField.RunwayHeightMetres * 0.5f - 0.011f;
        public const float MiniatureApronY = 0.06f;
        public const float MiniatureStandThreeY = 0.095f;
        public static float StairRise(float doorSillY, float pavementY) => Math.Max(0.6f, doorSillY - pavementY);
        public static float StairRun(float rise) => Math.Max(1.2f, rise * 1.45f);
    }
}
