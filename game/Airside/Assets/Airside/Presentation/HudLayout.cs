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
            // Allow smaller scales on short laptop windows so virtual panels fit.
            return Mathf.Clamp(Mathf.Min(widthScale, heightScale), 0.55f, 2.25f);
        }

        public static HudLayout Create(float viewportWidth, float viewportHeight, float operationsHeight,
            bool showDailyReport, bool showRouteOffer)
        {
            var compact = viewportWidth < 1150f;
            var leftWidth = compact ? 390f : 430f;
            var rightWidth = compact ? 320f : 360f;
            var leftHeight = Mathf.Min(760f, viewportHeight - Margin * 2f);
            var rightX = viewportWidth - Margin - rightWidth;

            var reportHeight = showDailyReport ? 136f : 0f;
            var offerHeight = showRouteOffer ? 172f : 0f;
            var stackedExtras = (showDailyReport ? reportHeight + Gap : 0f)
                + (showRouteOffer ? offerHeight + Gap : 0f);
            var maxOperations = Mathf.Max(120f, viewportHeight - Margin * 2f - stackedExtras);
            if (operationsHeight > maxOperations)
                operationsHeight = maxOperations;

            var operations = new Rect(rightX, Margin, rightWidth, operationsHeight);
            var nextY = operations.yMax + Gap;
            var report = showDailyReport
                ? new Rect(rightX, nextY, rightWidth, reportHeight)
                : Rect.zero;
            if (showDailyReport)
                nextY = report.yMax + Gap;

            var offer = showRouteOffer
                ? new Rect(rightX, nextY, rightWidth, offerHeight)
                : Rect.zero;

            if (showRouteOffer && offer.yMax > viewportHeight - Margin)
            {
                var overflow = offer.yMax - (viewportHeight - Margin);
                operationsHeight = Mathf.Max(120f, operationsHeight - overflow);
                operations = new Rect(rightX, Margin, rightWidth, operationsHeight);
                nextY = operations.yMax + Gap;
                report = showDailyReport
                    ? new Rect(rightX, nextY, rightWidth, reportHeight)
                    : Rect.zero;
                if (showDailyReport)
                    nextY = report.yMax + Gap;
                offer = new Rect(rightX, nextY, rightWidth, offerHeight);
            }

            return new HudLayout(
                new Rect(Margin, Margin, leftWidth, leftHeight),
                operations,
                report,
                offer);
        }
    }
}
