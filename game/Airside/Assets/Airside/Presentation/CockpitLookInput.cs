using System;

namespace Airside.Presentation
{
    /// <summary>
    /// Pointer and scroll handling for looking round the cockpit, pure so a Mac trackpad and a mouse can
    /// share one set of rules. A trackpad delivers large, uneven deltas (a fast flick, or the first frame
    /// after a click), so each frame's movement is bounded and sensitivity follows the zoom, which keeps a
    /// narrow field of view from feeling twitchy. Presentation only.
    /// </summary>
    public static class CockpitLookInput
    {
        public const float CockpitDegreesPerPixel = 0.15f;
        public const float OrbitDegreesPerPixel = 0.2f;
        public const float MaxPixelsPerFrame = 90f;
        public const float MaxScrollPerFrame = 240f;
        public const float ReferenceFov = 65f;
        /// <summary>Seconds to glide from the outside camera to the seat, or back out again.</summary>
        public const float TransitionSeconds = 0.9f;

        /// <summary>Pixels of pointer travel this frame, bounded so a spike cannot whip the view round.</summary>
        public static float ClampPixels(float pixels)
        {
            if (float.IsNaN(pixels)) return 0f;
            return Math.Min(Math.Max(pixels, -MaxPixelsPerFrame), MaxPixelsPerFrame);
        }

        /// <summary>Scroll units this frame for zoom, bounded for the same reason.</summary>
        public static float ClampScroll(float scroll)
        {
            if (float.IsNaN(scroll)) return 0f;
            return Math.Min(Math.Max(scroll, -MaxScrollPerFrame), MaxScrollPerFrame);
        }

        /// <summary>
        /// Degrees per pixel of drag. Scales with the field of view (zoomed in turns finer) and with the
        /// camera-speed setting, 1 being the old fixed rate at the default zoom.
        /// </summary>
        public static float DegreesPerPixel(float baseDegrees, float fieldOfView, float cameraSpeed)
        {
            var zoom = Math.Min(Math.Max(fieldOfView / ReferenceFov, 0.45f), 1.3f);
            var speed = 0.5f + 0.5f * Math.Min(Math.Max(cameraSpeed, 0f), 1f);
            return baseDegrees * zoom * speed;
        }

        /// <summary>Smooth 0..1 progress through a transition that has run for <paramref name="seconds"/>.</summary>
        public static float Ease(float seconds)
        {
            var t = Math.Min(Math.Max(seconds / TransitionSeconds, 0f), 1f);
            return t * t * (3f - 2f * t);
        }
    }
}
