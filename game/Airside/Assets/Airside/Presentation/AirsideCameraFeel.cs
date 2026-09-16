using System;

namespace Airside.Presentation
{
    /// <summary>
    /// Zoom and drag-pan rates for the overview camera. No UnityEngine types so the
    /// headless harness can lock the feel without an editor. The MonoBehaviour
    /// <see cref="AirsideCameraController"/> applies these values to the live camera.
    /// </summary>
    public static class AirsideCameraFeel
    {
        // One mouse-wheel notch (~120 units on macOS) is ~25 % closer / ~33 % further —
        // the previous 0.001 rate needed ~27 notches to leave the 2.4 km overview, which
        // felt stuck on both mouse wheels and trackpads. Keep the log-space queue so a
        // fast trackpad flick can still cover a wide band without a hard jump.
        public const float ZoomLogPerScrollUnit = 0.0024f;
        public const float MaxScrollPerFrame = 480f;
        public const float MaxZoomPendingLog = 2.4f;
        public const float ZoomEaseRate = 14f;
        public const float OrbitYawDegreesPerPixel = 0.26f;
        public const float OrbitPitchDegreesPerPixel = 0.2f;
        /// <summary>Drag pan scale: metres moved per screen pixel at the current orbit distance.</summary>
        public const float PanMetresPerPixelAtUnitDistance = 0.0028f;

        /// <summary>
        /// How much one scroll sample grows the pending log-zoom queue. Positive scroll
        /// zooms in (distance shrinks). Caps a single frame so a huge trackpad delta cannot
        /// skip the whole field in one tick.
        /// </summary>
        public static float QueueScrollZoom(float pendingLog, float scrollUnits)
        {
            var clamped = Clamp(scrollUnits, -MaxScrollPerFrame, MaxScrollPerFrame);
            return Clamp(pendingLog - clamped * ZoomLogPerScrollUnit, -MaxZoomPendingLog, MaxZoomPendingLog);
        }

        /// <summary>Distance multiplier for a fully applied scroll sample (before easing).</summary>
        public static float ZoomFactorForScroll(float scrollUnits)
        {
            var clamped = Clamp(scrollUnits, -MaxScrollPerFrame, MaxScrollPerFrame);
            return (float)Math.Exp(-clamped * ZoomLogPerScrollUnit);
        }

        /// <summary>Ground metres moved per drag pixel at the given orbit distance.</summary>
        public static float PanMetresPerPixel(float distance) =>
            Math.Max(0f, distance) * PanMetresPerPixelAtUnitDistance;

        private static float Clamp(float value, float min, float max) =>
            value < min ? min : value > max ? max : value;
    }
}
