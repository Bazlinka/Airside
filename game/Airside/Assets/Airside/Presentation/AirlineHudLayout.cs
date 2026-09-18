using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Placement of the airline HUD shell (ADR 0053): a slim full-width top bar, the
    /// current-objective card, compact player Operations, a contextual selected-aircraft
    /// card and a smaller mini-map. Kept pure so the fits-on-screen and no-overlap
    /// contract is tested at every target display size.
    ///
    /// Wide windows put the objective left and compact Operations right under the top
    /// bar. Narrow windows stack Operations under the objective. The mini-map and
    /// selected card sit on the bottom band; either hides when that band has no room.
    /// </summary>
    public readonly struct AirlineHudLayout
    {
        // The persistent shell's own measurements live in HudShell, which is UnityEngine-free
        // so the headless harness and the offline mockup renderer place it identically.
        public const float Margin = HudShell.Margin;
        public const float TopBarHeight = HudShell.TopBarHeight;
        public const float ObjectiveWidth = HudShell.ObjectiveWidth;
        public const float ObjectiveHeight = HudShell.ObjectiveHeight;
        public const float GuideHeight = HudShell.GuideHeight;
        public const float NavStripMaxWidth = HudShell.NavStripMaxWidth;
        public const float NavTabMinWidth = HudShell.NavTabMinWidth;
        public const float MinimumWorkspaceWidth = HudShell.MinimumWorkspaceWidth;

        public const float OperationsWidth = 340f;
        public const float OperationsMaxHeight = 184f;
        public const float ToastWidth = 460f;
        public const float ToastHeight = 40f;
        public const float SetupWidth = 420f;
        public const float SelectedCardWidth = 440f;
        public const float SelectedCardHeight = 148f;
        public const float MiniMapWidth = 220f;
        public const float MiniMapHeight = 132f;

        private AirlineHudLayout(
            Rect topBar, Rect navStrip, Rect objective, Rect operations, Rect workspace,
            Rect toast, Rect miniMap, Rect selectedCard, Rect setupArea, bool workspaceCoversOverview)
        {
            TopBar = topBar;
            NavStrip = navStrip;
            Objective = objective;
            Operations = operations;
            Workspace = workspace;
            Toast = toast;
            MiniMap = miniMap;
            SelectedCard = selectedCard;
            SetupArea = setupArea;
            WorkspaceCoversOverview = workspaceCoversOverview;
        }

        /// <summary>Full-width persistent status and navigation strip.</summary>
        public Rect TopBar { get; }

        /// <summary>The four workspace tabs, right-aligned inside <see cref="TopBar"/>.</summary>
        public Rect NavStrip { get; }

        /// <summary>
        /// Upper-left current-objective card, or the first-flight guide while it runs.
        /// </summary>
        public Rect Objective { get; }

        /// <summary>Upper-right compact player Operations list. Zero-sized when stacked windows run out of room.</summary>
        public Rect Operations { get; }

        /// <summary>The one major workspace surface (Map, Fleet, Contracts, full flights board).</summary>
        public Rect Workspace { get; }

        public Rect Toast { get; }

        /// <summary>Lower-left airfield mini-map. Zero-sized when it would collide or not fit.</summary>
        public Rect MiniMap { get; }

        /// <summary>Bottom-centre selected-aircraft card reservation. Zero-sized on very short windows.</summary>
        public Rect SelectedCard { get; }

        /// <summary>The centred region the start-your-airline panel is fitted into.</summary>
        public Rect SetupArea { get; }

        /// <summary>True when the window is too narrow for Operations beside the objective.</summary>
        public bool WorkspaceCoversOverview { get; }

        public static AirlineHudLayout Create(HudLayout hud, bool showGuide = false)
        {
            var width = hud.Viewport.x;
            var height = hud.Viewport.y;
            var inner = Mathf.Max(1f, width - Margin * 2f);
            var floor = height - Margin;

            var topBar = ToRect(HudShell.TopBar(width, height));
            var navStrip = ToRect(HudShell.NavStrip(width, height));

            var contentTop = topBar.yMax + HudShell.ContentGap;
            var objective = ToRect(HudShell.Objective(width, height, showGuide));
            var cardWidth = objective.width;

            var opsBeside = cardWidth + OperationsWidth + Margin * 3f <= width;
            Rect operations;
            if (opsBeside)
            {
                var opsLeft = width - Margin - Mathf.Min(OperationsWidth, inner);
                operations = ClampBelow(opsLeft, contentTop, Mathf.Min(OperationsWidth, inner), OperationsMaxHeight, floor);
            }
            else
            {
                var stackedTop = objective.yMax + Margin;
                operations = ClampBelow(Margin, stackedTop, cardWidth, OperationsMaxHeight, floor);
                if (operations.height < 64f)
                    operations = new Rect(operations.x, operations.y, 0f, 0f);
            }

            var selectedWidth = Mathf.Min(SelectedCardWidth, inner);
            var selectedHeight = Mathf.Min(SelectedCardHeight, Mathf.Max(1f, floor - contentTop));
            var selectedX = (width - selectedWidth) * 0.5f;
            var selectedY = floor - selectedHeight;
            var selectedCard = selectedY >= contentTop
                ? new Rect(selectedX, selectedY, selectedWidth, selectedHeight)
                : new Rect(selectedX, floor, selectedWidth, 0f);

            if (selectedCard.height > 0f && selectedCard.Overlaps(operations) && operations.width > 0f)
            {
                if (!opsBeside)
                    operations = new Rect(operations.x, operations.y, 0f, 0f);
                else
                    operations = ShrinkAbove(operations, selectedCard.y - Margin);
            }

            if (selectedCard.height > 0f && selectedCard.Overlaps(objective))
                selectedCard = new Rect(selectedCard.x, selectedCard.y, selectedCard.width, 0f);

            var miniWidth = Mathf.Min(MiniMapWidth, inner);
            var miniHeight = MiniMapHeight;
            var mini = new Rect(Margin, floor - miniHeight, miniWidth, miniHeight);
            var leftColumnBottom = Mathf.Max(objective.yMax, operations.width > 0f && !opsBeside ? operations.yMax : objective.yMax);
            if (miniWidth < MiniMapWidth * 0.7f
                || mini.y < leftColumnBottom + 8f
                || (selectedCard.height > 0f && mini.Overlaps(selectedCard))
                || (operations.width > 0f && mini.Overlaps(operations))
                || mini.Overlaps(objective))
                mini = new Rect(Margin, floor, 0f, 0f);

            var toastWidth = Mathf.Min(ToastWidth, inner);
            var toastY = contentTop;
            var toast = new Rect((width - toastWidth) * 0.5f, toastY, toastWidth, ToastHeight);
            if (toast.Overlaps(objective) || (operations.width > 0f && toast.Overlaps(operations)))
            {
                var toastBottom = selectedCard.height > 0f ? selectedCard.y - 8f : floor - ToastHeight;
                toast = new Rect((width - toastWidth) * 0.5f, Mathf.Max(contentTop, toastBottom - ToastHeight), toastWidth, ToastHeight);
                if (toast.Overlaps(objective) || (operations.width > 0f && toast.Overlaps(operations))
                    || (selectedCard.height > 0f && toast.Overlaps(selectedCard))
                    || (mini.width > 0f && toast.Overlaps(mini)))
                    toast = new Rect(toast.x, topBar.yMax + 4f, toastWidth, Mathf.Min(ToastHeight, 20f));
            }

            var workspaceTop = topBar.yMax + HudShell.ContentGap;
            var workspace = ToRect(HudShell.WorkspaceSurface(width, height));
            var setup = new Rect(Margin, workspaceTop, inner, Mathf.Max(1f, floor - workspaceTop));

            return new AirlineHudLayout(
                topBar, navStrip, objective, operations, workspace, toast, mini, selectedCard, setup, !opsBeside);
        }

        private static Rect ToRect(HudBox box) => new(box.X, box.Y, box.Width, box.Height);

        private static Rect ClampBelow(float x, float y, float width, float preferredHeight, float limit)
        {
            if (width <= 0f)
                return new Rect(x, y, 0f, 0f);
            if (y >= limit - 0.01f)
                return new Rect(x, Mathf.Max(0f, limit - 1f), width, 1f);
            return new Rect(x, y, width, Mathf.Max(1f, Mathf.Min(preferredHeight, limit - y)));
        }

        private static Rect ShrinkAbove(Rect rect, float limit)
        {
            if (rect.y >= limit - 1f)
                return new Rect(rect.x, rect.y, 0f, 0f);
            return new Rect(rect.x, rect.y, rect.width, Mathf.Max(0f, limit - rect.y));
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
