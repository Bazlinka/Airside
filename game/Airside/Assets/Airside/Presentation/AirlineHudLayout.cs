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

        /// <summary>
        /// Minimum band left under the left column on a stacked (narrow) window so the
        /// fleet/map rects stay above the toast and speed readout instead of collapsing
        /// onto them — the known 320×240 failure mode.
        /// </summary>
        public const float MinStackedContentHeight = 28f;

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

            var clockWidth = Mathf.Min(ClockWidth, inner);
            var fleetWidth = Mathf.Min(FleetWidth, inner);
            var fleetBeside = clockWidth + fleetWidth + Margin * 3f <= width;
            var fleetLeft = width - Margin - fleetWidth;

            var toastWidth = Mathf.Min(ToastWidth, inner);
            var toastBetween = fleetBeside && clockWidth + Margin + toastWidth + Margin <= fleetLeft - Margin;
            // Bottom-band toast: keep every other panel out of that strip so toast cannot
            // sit on the map/fleet/nav (320×240 and other short stacked windows).
            var contentFloor = toastBetween
                ? floor
                : Mathf.Max(Margin + 1f, floor - ToastHeight - Margin);

            // On a stacked window, leave a real band under the left column for fleet/map.
            var leftLimit = fleetBeside
                ? contentFloor
                : Mathf.Max(Margin + 8f, contentFloor - MinStackedContentHeight);

            var clock = new Rect(Margin, Margin, clockWidth,
                Mathf.Max(1f, Mathf.Min(ClockHeight, leftLimit - Margin)));

            // Always leave a one-pixel nav strip (+ gap) under the guide so ClampBelow
            // cannot park the nav on top of a guide that already filled leftLimit.
            var guideHeight = showGuide ? GuideHeight : StatusLineHeight;
            var guideTop = clock.yMax + Margin;
            var guideLimit = Mathf.Max(guideTop + 1f, leftLimit - Margin - 1f);
            var guide = ClampBelow(Margin, guideTop, clock.width, guideHeight, guideLimit);

            var navAvailable = (fleetBeside ? fleetLeft - Margin : width - Margin) - Margin;
            var navWidth = Mathf.Min(Mathf.Max(clock.width, Mathf.Max(1f, navAvailable)), NavStripMaxWidth);
            var navTop = guide.yMax + Margin;
            var navStrip = ClampBelow(Margin, navTop, Mathf.Min(navWidth, inner), NavStripHeight, leftLimit);

            var leftColumnBottom = navStrip.yMax;
            var fleetTop = fleetBeside ? Margin : leftColumnBottom + Margin;
            if (!fleetBeside)
                fleetTop = Mathf.Min(fleetTop, contentFloor - 1f);
            var fleet = new Rect(fleetLeft, fleetTop, fleetWidth, Mathf.Max(1f, contentFloor - fleetTop));

            var toast = toastBetween
                ? new Rect((clock.xMax + fleet.x - toastWidth) * 0.5f, Margin, toastWidth, ToastHeight)
                : new Rect((width - toastWidth) * 0.5f, Mathf.Max(Margin, floor - ToastHeight), toastWidth, ToastHeight);

            var mapBeside = fleetBeside && fleet.x - Margin - Margin >= MinimumMapWidth;
            var mapRight = mapBeside ? fleet.x - Margin : width - Margin;
            var mapTop = leftColumnBottom + Margin;
            if (!mapBeside)
                mapTop = Mathf.Min(mapTop, contentFloor - 1f);
            // When the map covers the fleet they share the same vertical band on purpose.
            if (!mapBeside && !fleetBeside)
                mapTop = fleet.y;
            var map = new Rect(Margin, mapTop, Mathf.Max(1f, mapRight - Margin), Mathf.Max(1f, contentFloor - mapTop));

            var setup = new Rect(Margin, Margin, inner, Mathf.Max(1f, contentFloor - Margin));

            return new AirlineHudLayout(clock, guide, navStrip, fleet, toast, map, !mapBeside, setup);
        }

        /// <summary>
        /// A panel that starts at <paramref name="y"/> and never crosses <paramref name="limit"/>.
        /// Degenerates to a 1 px strip just under the limit when the window has already run out.
        /// </summary>
        private static Rect ClampBelow(float x, float y, float width, float preferredHeight, float limit)
        {
            if (y >= limit - 0.01f)
                return new Rect(x, Mathf.Max(Margin, limit - 1f), width, 1f);
            return new Rect(x, y, width, Mathf.Max(1f, Mathf.Min(preferredHeight, limit - y)));
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
