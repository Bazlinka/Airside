using System;

namespace Airside.Presentation
{
    /// <summary>
    /// Thin painted lines (a 0.9 m runway edge or centreline) are under a pixel wide from a kilometre or two out, so they
    /// break into shimmering dashes as the camera moves. The line is widened with distance instead, to a little over a
    /// pixel, so it stays a continuous line from every zoom. Pure maths; no UnityEngine types.
    /// </summary>
    public static class AirsidePaintWidening
    {
        /// <summary>Width the line is held to on screen, in pixels.</summary>
        public const float TargetPixels = 1.6f;

        /// <summary>Never wider than this many times the real marking, so a far zoom does not paint a motorway.</summary>
        public const float MaxFactor = 9f;

        /// <summary>World metres covered by one screen pixel at <paramref name="distance"/>.</summary>
        public static float PixelMetres(float distance, float verticalFovDegrees, float screenHeightPixels)
        {
            if (screenHeightPixels < 1f)
                return 0f;
            var half = (float)Math.Tan(Math.Max(1f, Math.Min(170f, verticalFovDegrees)) * 0.5f * Math.PI / 180.0);
            return 2f * Math.Max(0f, distance) * half / screenHeightPixels;
        }

        /// <summary>The width to draw a line of real width <paramref name="baseWidth"/> at <paramref name="distance"/>.</summary>
        public static float Width(float baseWidth, float distance, float verticalFovDegrees, float screenHeightPixels)
        {
            var wanted = TargetPixels * PixelMetres(distance, verticalFovDegrees, screenHeightPixels);
            return Math.Max(baseWidth, Math.Min(baseWidth * MaxFactor, wanted));
        }
    }
}
