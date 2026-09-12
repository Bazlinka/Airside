using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Resolution-independent placement for the circuit HUD. All rectangles are
    /// expressed in virtual GUI points after <see cref="ScaleFor"/> is applied.
    ///
    /// The HUD is two things only (ADR 0041): a control bar carrying pause, follow
    /// and the speed buttons, and a centred pause menu. Keeping the arithmetic here
    /// makes the fits-on-screen contract testable without an editor.
    /// </summary>
    public readonly struct HudLayout
    {
        private const float Margin = 22f;

        /// <summary>Control bar height, and the size of the square buttons inside it.</summary>
        public const float BarHeight = 44f;
        public const float ButtonWidth = 62f;
        public const float ButtonGap = 8f;

        /// <summary>pause · follow · 1× · 2× · 4×</summary>
        public const int ButtonCount = 5;

        public const float MenuWidth = 340f;
        public const float MenuHeight = 232f;

        private HudLayout(Rect controlBar, Rect pauseMenu)
        {
            ControlBar = controlBar;
            PauseMenu = pauseMenu;
        }

        /// <summary>Bottom-centre strip holding the five controls.</summary>
        public Rect ControlBar { get; }

        /// <summary>Centred pause-menu panel.</summary>
        public Rect PauseMenu { get; }

        public static float ScaleFor(int screenWidth, int screenHeight)
        {
            var widthScale = screenWidth / 1440f;
            var heightScale = screenHeight / 900f;
            // Allow smaller scales on short laptop windows so virtual panels fit.
            return Mathf.Clamp(Mathf.Min(widthScale, heightScale), 0.55f, 2.25f);
        }

        public static HudLayout Create(float viewportWidth, float viewportHeight)
        {
            var barWidth = ButtonCount * ButtonWidth + (ButtonCount - 1) * ButtonGap;
            // Never let the bar run under the margins on a narrow window.
            barWidth = Mathf.Min(barWidth, Mathf.Max(1f, viewportWidth - Margin * 2f));

            var bar = new Rect(
                (viewportWidth - barWidth) * 0.5f,
                Mathf.Max(Margin, viewportHeight - Margin - BarHeight),
                barWidth,
                BarHeight);

            var menuWidth = Mathf.Min(MenuWidth, Mathf.Max(1f, viewportWidth - Margin * 2f));
            var menuHeight = Mathf.Min(MenuHeight, Mathf.Max(1f, viewportHeight - Margin * 2f));
            var menu = new Rect(
                (viewportWidth - menuWidth) * 0.5f,
                (viewportHeight - menuHeight) * 0.5f,
                menuWidth,
                menuHeight);

            return new HudLayout(bar, menu);
        }

        /// <summary>
        /// Rect for control <paramref name="index"/> (0-based, left to right) inside
        /// <see cref="ControlBar"/>. Buttons share the bar's width evenly so they stay
        /// aligned when the bar is clamped on a narrow window.
        /// </summary>
        public Rect ButtonAt(int index)
        {
            var slot = (ControlBar.width - (ButtonCount - 1) * ButtonGap) / ButtonCount;
            return new Rect(
                ControlBar.x + index * (slot + ButtonGap),
                ControlBar.y,
                slot,
                ControlBar.height);
        }
    }
}
