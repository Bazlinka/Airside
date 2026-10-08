using System;

namespace Airside.Presentation
{
    /// <summary>Bounded rendering around the watched area, not an airport-sized weather region.</summary>
    public static class WeatherCoverage
    {
        // Keep sixteen volumes; widen the footprint for storm bodies and soft recycling edges.
        public const float CloudHalfWidth = 12000f;
        public const float CloudHalfDepth = 12000f;
        public const float CloudFadeWidth = 2500f;
        public const float CloudFadeDepth = 2500f;
        public const float AtmosphereFadeStart = 20000f;
        public const float AtmosphereDistance = 30000f;

        /// <summary>Closest repeated world position. Interior clouds stay still as the watched area moves.</summary>
        public static float WrapNearView(float world, float focus, float halfSpan)
        {
            var span = halfSpan * 2f;
            return (float)(world - Math.Floor((world - focus + halfSpan) / span) * span);
        }

        public static float CloudEdge(float x, float z, float focusX, float focusZ) => Math.Min(
            Fade(Math.Abs(x - focusX), CloudHalfWidth - CloudFadeWidth, CloudHalfWidth),
            Fade(Math.Abs(z - focusZ), CloudHalfDepth - CloudFadeDepth, CloudHalfDepth));

        public static float Fade(float distance, float start, float end)
        {
            var t = Math.Max(0f, Math.Min(1f, (distance - start) / (end - start)));
            return 1f - t * t * (3f - 2f * t);
        }
    }
}
