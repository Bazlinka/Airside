using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Placement of the airline panels (ADR 0045) around the circuit HUD, in the same
    /// virtual GUI points as <see cref="HudLayout"/>. Kept pure so the fits-on-screen
    /// and no-overlap contract is tested at every target display size.
    ///
    /// Wide windows put the clock top-left, the fleet top-right and the toast between
    /// them. Narrow windows stack the fleet under the clock, move the toast down
    /// above the speed readout, and let the open map cover the fleet panel.
    /// </summary>
    public readonly struct AirlineHudLayout
    {
        public const float Margin = 22f;
        public const float ClockWidth = 300f;
        public const float ClockHeight = 92f;
        public const float FleetWidth = 360f;
        public const float ToastWidth = 460f;
        public const float ToastHeight = 40f;
        public const float SetupWidth = 420f;
        public const float MinimumMapWidth = 520f;
        public const float GuideHeight = 104f;
        public const float NavStripHeight = 44f;

        /// <summary>The nav strip never grows wider than this, even with a lot of spare room.</summary>
        public const float NavStripMaxWidth = 420f;

        /// <summary>Height of the persistent objective line once the first-flight guide is done.</summary>
        public const float StatusLineHeight = 26f;

        private AirlineHudLayout(Rect clock, Rect guide, Rect navStrip, Rect fleetArea, Rect toast, Rect map, bool mapCoversFleet, Rect setupArea)
        {
            Clock = clock;
            Guide = guide;
            NavStrip = navStrip;
            FleetArea = fleetArea;
            Toast = toast;
            Map = map;
            MapCoversFleet = mapCoversFleet;
            SetupArea = setupArea;
        }

        public Rect Clock { get; }

        /// <summary>
        /// Under the clock: the first-session guide card while it runs, or — once it's done —
        /// the persistent one-line objective/status (ADR 0053). Always sized; never zero.
        /// </summary>
        public Rect Guide { get; }

        /// <summary>
        /// The four-workspace nav strip (ADR 0053), directly under the clock/guide column.
        /// Wider than the clock itself — up to <see cref="NavStripMaxWidth"/> — since four tab
        /// labels need more room than the narrow clock column alone provides.
        /// </summary>
        public Rect NavStrip { get; }

        /// <summary>The most room the fleet panel may take; it shrinks to its content.</summary>
        public Rect FleetArea { get; }

        public Rect Toast { get; }
        public Rect Map { get; }

        /// <summary>True when the window is too narrow for the map beside the fleet, so the fleet hides while it is open.</summary>
        public bool MapCoversFleet { get; }

        /// <summary>The centred region the start-your-airline panel is fitted into.</summary>
        public Rect SetupArea { get; }

        public static AirlineHudLayout Create(HudLayout hud, bool showGuide = false)
        {
            var width = hud.Viewport.x;
            var height = hud.Viewport.y;
            var inner = Mathf.Max(1f, width - Margin * 2f);
            // Everything above the speed readout (itself above the control bar).
            var floor = Mathf.Max(Margin + 1f, Mathf.Min(hud.SpeedReadout.y, hud.ControlBar.y) - Margin);

            var clock = new Rect(Margin, Margin, Mathf.Min(ClockWidth, inner), Mathf.Min(ClockHeight, floor - Margin));
            var guideHeight = showGuide ? GuideHeight : StatusLineHeight;
            var guide = new Rect(Margin, clock.yMax + Margin, clock.width,
                Mathf.Max(1f, Mathf.Min(guideHeight, floor - clock.yMax - Margin)));
            // Whatever sits below the left column starts under the guide/status line.
            var leftColumnBottom = guide.yMax;

            // The fleet panel's horizontal placement depends only on width, not on anything
            // below the clock, so it is worked out now and used to size the nav strip too —
            // four tab labels ("Operations", "Contracts", ...) need far more room than the
            // 300 px clock column alone ever gives them.
            var fleetWidth = Mathf.Min(FleetWidth, inner);
            var fleetBeside = clock.width + fleetWidth + Margin * 3f <= width;
            var fleetLeft = width - Margin - fleetWidth;

            var navAvailable = (fleetBeside ? fleetLeft - Margin : width - Margin) - Margin;
            var navWidth = Mathf.Min(Mathf.Max(clock.width, navAvailable), NavStripMaxWidth);
            var navStrip = new Rect(Margin, leftColumnBottom + Margin, Mathf.Min(navWidth, inner),
                Mathf.Max(1f, Mathf.Min(NavStripHeight, floor - leftColumnBottom - Margin)));
            // Everything below the left column now starts under the nav strip.
            leftColumnBottom = navStrip.yMax;

            var fleetTop = fleetBeside ? Margin : leftColumnBottom + Margin;
            var fleet = new Rect(fleetLeft, fleetTop, fleetWidth, Mathf.Max(1f, floor - fleetTop));

            var toastWidth = Mathf.Min(ToastWidth, inner);
            var toastBetween = fleetBeside && clock.xMax + Margin + toastWidth + Margin <= fleet.x;
            var toast = toastBetween
                ? new Rect((clock.xMax + fleet.x - toastWidth) * 0.5f, Margin, toastWidth, ToastHeight)
                : new Rect((width - toastWidth) * 0.5f, Mathf.Max(Margin, floor - ToastHeight), toastWidth, ToastHeight);

            var mapTop = leftColumnBottom + Margin;
            var mapBeside = fleetBeside && fleet.x - Margin - Margin >= MinimumMapWidth;
            var mapRight = mapBeside ? fleet.x - Margin : width - Margin;
            var map = new Rect(Margin, mapTop, Mathf.Max(1f, mapRight - Margin), Mathf.Max(1f, floor - mapTop));

            var setup = new Rect(Margin, Margin, inner, Mathf.Max(1f, floor - Margin));

            return new AirlineHudLayout(clock, guide, navStrip, fleet, toast, map, !mapBeside, setup);
        }

        /// <summary>A panel of the preferred size, centred in <see cref="SetupArea"/> and never larger than it.</summary>
        public Rect SetupPanel(float preferredHeight)
        {
            var w = Mathf.Min(SetupWidth, SetupArea.width);
            var h = Mathf.Min(preferredHeight, SetupArea.height);
            return new Rect(SetupArea.x + (SetupArea.width - w) * 0.5f, SetupArea.y + (SetupArea.height - h) * 0.5f, w, h);
        }
    }
}
