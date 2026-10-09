using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Keeps the player rendering at the display's real resolution. Unity remembers the last window
    /// size in the user's preferences, and a stale one (a maximised 1600x900 window on a 3456x2168 Retina
    /// screen) made the whole game render small and get stretched, with black bars: soft text and a soft
    /// world. A full-screen or maximised player is always asked for the display's native size.
    /// </summary>
    public static class AirsideDisplay
    {
        /// <summary>Below this share of the display's width the render size is treated as stale.</summary>
        public const float StaleShare = 0.9f;

        /// <summary>True when a non-windowed player renders clearly below the display's native size.</summary>
        public static bool IsBelowNative(FullScreenMode mode, int renderWidth, int renderHeight, int nativeWidth, int nativeHeight) =>
            mode != FullScreenMode.Windowed && nativeWidth > 0 && nativeHeight > 0
            && (renderWidth < nativeWidth * StaleShare || renderHeight < nativeHeight * StaleShare);

        public static void EnsureNativeResolution()
        {
            if (Application.isEditor)
                return;
            var native = Screen.currentResolution;
            if (!IsBelowNative(Screen.fullScreenMode, Screen.width, Screen.height, native.width, native.height))
                return;
            Debug.Log($"[Airside display] rendering {Screen.width}x{Screen.height} on a {native.width}x{native.height} display; switching to native");
            Screen.SetResolution(native.width, native.height, FullScreenMode.FullScreenWindow);
        }
    }
}
