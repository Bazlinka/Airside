using System;

namespace Airside.Presentation
{
    /// <summary>
    /// Which latitude/longitude window the route map's baked land texture covers, and when it still
    /// serves the view. Pure maths (no UnityEngine) so the headless harness covers it. The window is
    /// the visible map plus a margin, so a short pan or a small zoom reuses the same texture.
    /// </summary>
    public static class RouteMapLandWindow
    {
        /// <summary>Extra map either side of the view that the texture covers, as a fraction of the view.</summary>
        public const float Margin = 0.25f;

        /// <summary>The texture is reused while the current scale is within this band of the baked scale.</summary>
        public const float MinScaleRatio = 0.62f, MaxScaleRatio = 1.6f;

        public static void Bounds(AustraliaMapLens lens, float width, float height,
            out double west, out double east, out double south, out double north)
        {
            lens.GuiToLonLat(0f, 0f, width, height, -width * Margin, -height * Margin, out var w, out var n);
            lens.GuiToLonLat(0f, 0f, width, height, width * (1f + Margin), height * (1f + Margin), out var e, out var s);
            west = w; east = e; south = s; north = n;
        }

        /// <summary>True when the baked window still holds the whole view at a similar enough scale.</summary>
        public static bool Serves(AustraliaMapLens lens, float width, float height, double west, double east,
            double south, double north, float bakedPixelsPerDegree)
        {
            if (bakedPixelsPerDegree <= 0f)
                return false;
            var ratio = lens.PixelsPerDegree(width, height) / bakedPixelsPerDegree;
            if (ratio < MinScaleRatio || ratio > MaxScaleRatio)
                return false;
            lens.GuiToLonLat(0f, 0f, width, height, 0f, 0f, out var viewWest, out var viewNorth);
            lens.GuiToLonLat(0f, 0f, width, height, width, height, out var viewEast, out var viewSouth);
            return viewWest >= west && viewEast <= east && viewSouth >= south && viewNorth <= north;
        }

        /// <summary>Texture size for a map panel: about 1.2 texels per screen pixel, aspect matching the window.</summary>
        public static void TextureSize(float width, float height, out int pixelsWide, out int pixelsHigh)
        {
            pixelsWide = Math.Max(256, Math.Min(2048, (int)Math.Round(width * (1f + 2f * Margin) * 1.2f)));
            pixelsHigh = Math.Max(128, (int)Math.Round(pixelsWide * height / Math.Max(1f, width)));
        }
    }
}
