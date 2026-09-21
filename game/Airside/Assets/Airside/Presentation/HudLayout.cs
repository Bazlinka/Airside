using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Resolution-independent placement for the circuit HUD. All rectangles are
    /// expressed in virtual GUI points after <see cref="ScaleFor"/> is applied.
    ///
    /// The HUD is a centred pause menu. Live airline time has no pause, rates or skip
    /// (ADR 0045). Follow / Overview sit on the circuit HUD only; the airline overview
    /// uses the selected-aircraft card and Esc/R instead (ADR 0053). The airline panels
    /// place themselves around the pause menu. Keeping the arithmetic here makes the
    /// fits-on-screen contract testable without an editor.
    /// </summary>
    public readonly struct HudLayout
    {
        private const float Margin = 22f;

        /// <summary>Control bar height, and the size of the square buttons inside it.</summary>
        public const float BarHeight = 44f;
        public const float ButtonWidth = 104f;
        public const float ButtonGap = 8f;

        /// <summary>follow · overview. Live real time has no pause, rates or skip (ADR 0045).</summary>
        public const int ButtonCount = 2;

        public const float MenuWidth = 340f;
        /// <summary>Taller than the four buttons so the build-identity line fits under Quit.</summary>
        public const float MenuHeight = 360f;
        public const float OptionsWidth = 420f;
        public const float OptionsHeight = 460f;

        /// <summary>Live speed / altitude / heading, centred just above the control bar.</summary>
        public const float ReadoutWidth = 420f;
        public const float ReadoutHeight = 38f;
        public const float ReadoutGap = 8f;

        /// <summary>
        /// Map credit and build stamp share one line in the strip under the airline HUD's
        /// floor (<see cref="HudShell.Margin"/>), so they never draw across a panel's edge.
        /// </summary>
        public const float FooterHeight = 15f;
        /// <summary>Wide enough for the OSM credit plus the live-traffic (adsb.lol) credit.</summary>
        public const float CreditWidth = 470f;
        public const float StampMaxWidth = 420f;
        private const float FooterInset = 8f;

        private HudLayout(Vector2 viewport, Rect controlBar, Rect pauseMenu, Rect optionsMenu, Rect speedReadout)
        {
            Viewport = viewport;
            ControlBar = controlBar;
            PauseMenu = pauseMenu;
            OptionsMenu = optionsMenu;
            SpeedReadout = speedReadout;
        }

        /// <summary>Virtual viewport size the layout was made for.</summary>
        public Vector2 Viewport { get; }

        /// <summary>Bottom-centre strip holding the controls.</summary>
        public Rect ControlBar { get; }

        /// <summary>Centred pause-menu panel.</summary>
        public Rect PauseMenu { get; }

        /// <summary>Centred options panel, opened from the pause menu.</summary>
        public Rect OptionsMenu { get; }

        /// <summary>Live airspeed, directly above the control bar.</summary>
        public Rect SpeedReadout { get; }

        /// <summary>ODbL map credit, bottom-right in the footer strip.</summary>
        public Rect MapCredit => new(
            Mathf.Max(FooterInset, Viewport.x - FooterInset - CreditWidth),
            Viewport.y - FooterHeight,
            Mathf.Min(CreditWidth, Mathf.Max(1f, Viewport.x - FooterInset * 2f)),
            FooterHeight);

        /// <summary>
        /// Git build stamp, right-aligned just left of <see cref="MapCredit"/> in the same
        /// footer strip. Zero-width when the window is too narrow to fit both.
        /// </summary>
        public Rect BuildStamp
        {
            get
            {
                var credit = MapCredit;
                var right = credit.x - FooterInset;
                var width = Mathf.Min(StampMaxWidth, right - FooterInset);
                return width < 80f
                    ? new Rect(credit.x, credit.y, 0f, 0f)
                    : new Rect(right - width, credit.y, width, FooterHeight);
            }
        }

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

            var optionsWidth = Mathf.Min(OptionsWidth, Mathf.Max(1f, viewportWidth - Margin * 2f));
            var optionsHeight = Mathf.Min(OptionsHeight, Mathf.Max(1f, viewportHeight - Margin * 2f));
            var options = new Rect(
                (viewportWidth - optionsWidth) * 0.5f,
                (viewportHeight - optionsHeight) * 0.5f,
                optionsWidth,
                optionsHeight);

            var readoutWidth = Mathf.Min(ReadoutWidth, Mathf.Max(1f, viewportWidth - Margin * 2f));
            var readout = new Rect(
                (viewportWidth - readoutWidth) * 0.5f,
                // Sits on the bar rather than off the top of a very short window.
                Mathf.Max(0f, bar.y - ReadoutHeight - ReadoutGap),
                readoutWidth,
                Mathf.Min(ReadoutHeight, bar.y));

            return new HudLayout(new Vector2(viewportWidth, viewportHeight), bar, menu, options, readout);
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
