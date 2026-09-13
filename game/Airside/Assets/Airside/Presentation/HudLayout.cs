using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Resolution-independent placement for the circuit HUD. All rectangles are
    /// expressed in virtual GUI points after <see cref="ScaleFor"/> is applied.
    ///
    /// The HUD is a control bar carrying pause, follow, the speed buttons and
    /// skip-to-next-event (ADR 0041), a centred pause menu, and a live airspeed
    /// readout above the bar (ADR 0044). The airline panels (ADR 0045) place
    /// themselves around it. Keeping the arithmetic here makes the fits-on-screen
    /// contract testable without an editor.
    /// </summary>
    public readonly struct HudLayout
    {
        private const float Margin = 22f;

        /// <summary>Control bar height, and the size of the square buttons inside it.</summary>
        public const float BarHeight = 44f;
        public const float ButtonWidth = 62f;
        public const float ButtonGap = 8f;

        /// <summary>pause · follow · 1× · 2× · 4× · 10× · 30× · 60× · skip</summary>
        public const int ButtonCount = 9;

        public const float MenuWidth = 340f;
        public const float MenuHeight = 232f;

        /// <summary>Airspeed readout, centred just above the control bar.</summary>
        public const float ReadoutWidth = 132f;
        public const float ReadoutHeight = 34f;
        public const float ReadoutGap = 8f;

        private HudLayout(Rect controlBar, Rect pauseMenu, Rect speedReadout)
        {
            ControlBar = controlBar;
            PauseMenu = pauseMenu;
            SpeedReadout = speedReadout;
        }

        /// <summary>Bottom-centre strip holding the controls.</summary>
        public Rect ControlBar { get; }

        /// <summary>Centred pause-menu panel.</summary>
        public Rect PauseMenu { get; }

        /// <summary>Live airspeed, directly above the control bar.</summary>
        public Rect SpeedReadout { get; }

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

            var readoutWidth = Mathf.Min(ReadoutWidth, Mathf.Max(1f, viewportWidth - Margin * 2f));
            var readout = new Rect(
                (viewportWidth - readoutWidth) * 0.5f,
                // Sits on the bar rather than off the top of a very short window.
                Mathf.Max(0f, bar.y - ReadoutHeight - ReadoutGap),
                readoutWidth,
                Mathf.Min(ReadoutHeight, bar.y));

            return new HudLayout(bar, menu, readout);
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
