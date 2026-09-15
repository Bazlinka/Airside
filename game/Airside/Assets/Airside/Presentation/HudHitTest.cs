using System.Collections.Generic;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Whether a pointer is over something the HUD owns — a panel, a field tag, the toast —
    /// so the camera neither pans, zooms nor 3D-picks through it. Pure maths, EditMode tested.
    /// </summary>
    public static class HudHitTest
    {
        /// <summary>Input System position (pixels, origin bottom-left) to IMGUI points (origin top-left, scaled).</summary>
        public static Vector2 ToGui(Vector2 inputSystemPosition, float screenHeight, float hudScale)
        {
            var scale = hudScale > 0f ? hudScale : 1f;
            return new Vector2(inputSystemPosition.x, screenHeight - inputSystemPosition.y) / scale;
        }

        /// <summary>True when <paramref name="gui"/> falls inside any panel or overlay rect.</summary>
        public static bool Contains(Vector2 gui, IReadOnlyList<Rect> panels, IReadOnlyList<Rect> overlays = null) =>
            AnyContains(panels, gui) || AnyContains(overlays, gui);

        private static bool AnyContains(IReadOnlyList<Rect> rects, Vector2 gui)
        {
            if (rects == null)
                return false;
            for (var i = 0; i < rects.Count; i++)
                if (rects[i].Contains(gui))
                    return true;
            return false;
        }

        public static bool IsOverHud(Vector2 inputSystemPosition, float screenHeight, float hudScale,
            IReadOnlyList<Rect> panels, IReadOnlyList<Rect> overlays) =>
            Contains(ToGui(inputSystemPosition, screenHeight, hudScale), panels, overlays);
    }
}
