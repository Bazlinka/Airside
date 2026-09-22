using System;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// macOS frame pacing (ADR 0101). Airside is a slow, real-time airport: nothing on screen
    /// needs more than 60 fps, but <c>vSyncCount = 1</c> on a ProMotion MacBook or a 120/144 Hz
    /// external display renders the full HDR + SSAO + MSAA stack at the panel's refresh
    /// rate. Because the player keeps running while another app has focus
    /// (<c>runInBackground</c>), it also kept rendering at full rate behind the user's other
    /// windows. Pacing uses a vsync divisor rather than <c>targetFrameRate</c>, so frames
    /// stay on vblank and never tear in native fullscreen.
    /// Simulation time is read off the wall clock, so frame rate never changes outcomes.
    /// </summary>
    public static class AirsideFramePacing
    {
        public const int FocusedTargetFps = 60;
        public const int BackgroundTargetFps = 30;
        public const int MaxVSyncCount = 4;
        public const float RecheckSeconds = 1f;

        /// <summary>
        /// Smallest vsync divisor that keeps the frame rate at or above <paramref name="targetFps"/>:
        /// 60 Hz → 1, 120 Hz → 2 (60 fps), 144 Hz → 2 (72 fps), 100 Hz → 1. A target of 0 or
        /// less means uncapped (every vblank). Unknown refresh rates fall back to 1.
        /// </summary>
        public static int VSyncCountFor(double refreshHz, int targetFps)
        {
            if (targetFps <= 0 || double.IsNaN(refreshHz) || double.IsInfinity(refreshHz) || refreshHz <= 0)
                return 1;
            // 59.94 Hz / 60 is 0.999, not 0: the slack stops a fractional NTSC-style rate
            // from flooring to zero, and a 119.88 Hz panel still halves.
            var divisor = (int)Math.Floor(refreshHz / targetFps + 0.01);
            return Mathf.Clamp(divisor, 1, MaxVSyncCount);
        }

        /// <summary>Frame-rate target for the current state; 0 means the display's own rate.</summary>
        public static int TargetFps(bool uncapped, bool focused, bool soak)
        {
            // Automated soak/review runs never own focus but still need full-rate capture.
            if (!focused && !soak)
                return BackgroundTargetFps;
            return uncapped ? 0 : FocusedTargetFps;
        }

        private static int _applied = -1;
        private static float _nextCheckAt;

        /// <summary>Re-reads the display rate at most once a second (the window can move screens).</summary>
        public static void Tick(bool uncapped, bool soak)
        {
            if (Time.unscaledTime < _nextCheckAt)
                return;
            Apply(uncapped, soak);
        }

        public static void Apply(bool uncapped, bool soak) => Apply(uncapped, soak, Application.isFocused);

        public static void Apply(bool uncapped, bool soak, bool focused)
        {
            _nextCheckAt = Time.unscaledTime + RecheckSeconds;
            var refresh = Screen.currentResolution.refreshRateRatio.value;
            var target = TargetFps(uncapped, focused, soak);
            var vsync = VSyncCountFor(refresh, target);
            if (vsync == _applied && QualitySettings.vSyncCount == vsync)
                return;
            _applied = vsync;
            QualitySettings.vSyncCount = vsync;
        }
    }
}
