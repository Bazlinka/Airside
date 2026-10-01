using System;

namespace Airside.Presentation
{
    /// <summary>Bounded rendering around the view, not an airport-sized weather region.</summary>
    public static class WeatherCoverage
    {
        // Keep the existing sixteen-volume budget/density; recycle only beyond soft edges.
        public const float CloudHalfWidth = 4500f;
        public const float CloudHalfDepth = 2800f;
        public const float CloudFadeWidth = 700f;
        public const float CloudFadeDepth = 500f;
        public const float AtmosphereFadeStart = 20000f;
        public const float AtmosphereDistance = 30000f;

        /// <summary>Closest repeated world position. Interior clouds stay still as the view moves.</summary>
        public static float WrapNearView(float world, float camera, float halfSpan)
        {
            var span = halfSpan * 2f;
            return (float)(world - Math.Floor((world - camera + halfSpan) / span) * span);
        }

        public static float CloudEdge(float x, float z, float cameraX, float cameraZ) => Math.Min(
            Fade(Math.Abs(x - cameraX), CloudHalfWidth - CloudFadeWidth, CloudHalfWidth),
            Fade(Math.Abs(z - cameraZ), CloudHalfDepth - CloudFadeDepth, CloudHalfDepth));

        public static float Fade(float distance, float start, float end)
        {
            var t = Math.Max(0f, Math.Min(1f, (distance - start) / (end - start)));
            return 1f - t * t * (3f - 2f * t);
        }
    }
}
