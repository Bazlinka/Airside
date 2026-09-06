using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Resolution-independent placement for the prototype HUD. All rectangles are
    /// expressed in virtual GUI points after <see cref="ScaleFor"/> is applied.
    /// Keeping the calculation separate makes the no-overlap contract testable.
    /// </summary>
    public readonly struct HudLayout
    {
        private const float Margin = 22f;
        private const float Gap = 12f;

        private HudLayout(Rect leftPanel, Rect operationsPanel, Rect dailyReportPanel, Rect routeOfferPanel)
        {
            LeftPanel = leftPanel;
            OperationsPanel = operationsPanel;
            DailyReportPanel = dailyReportPanel;
            RouteOfferPanel = routeOfferPanel;
        }

        public Rect LeftPanel { get; }
        public Rect OperationsPanel { get; }
        public Rect DailyReportPanel { get; }
        public Rect RouteOfferPanel { get; }

        public static float ScaleFor(int screenWidth, int screenHeight)
        {
            var widthScale = screenWidth / 1440f;
            var heightScale = screenHeight / 900f;
            return Mathf.Clamp(Mathf.Min(widthScale, heightScale), 0.8f, 2.25f);
        }

        public static HudLayout Create(float viewportWidth, float viewportHeight, float operationsHeight,
            bool showDailyReport, bool showRouteOffer)
        {
            var compact = viewportWidth < 1150f;
            var leftWidth = compact ? 390f : 430f;
            var rightWidth = compact ? 320f : 360f;
            var leftHeight = Mathf.Min(760f, viewportHeight - Margin * 2f);
            var rightX = viewportWidth - Margin - rightWidth;

            var operations = new Rect(rightX, Margin, rightWidth, operationsHeight);
            var nextY = operations.yMax + Gap;
            var report = showDailyReport
                ? new Rect(rightX, nextY, rightWidth, 136f)
                : Rect.zero;
            if (showDailyReport)
                nextY = report.yMax + Gap;

            var offer = showRouteOffer
                ? new Rect(rightX, nextY, rightWidth, 172f)
                : Rect.zero;

            return new HudLayout(
                new Rect(Margin, Margin, leftWidth, leftHeight),
                operations,
                report,
                offer);
        }
    }
}
