using System.Collections.Generic;

namespace Airside.Presentation
{
    /// <summary>One value in the persistent top bar, with the divider that precedes it.</summary>
    public readonly struct HudTopBarSegment
    {
        public HudTopBarSegment(HudBox box, string text, HudTone tone, bool leadingDivider)
        {
            Box = box;
            Text = text ?? string.Empty;
            Tone = tone;
            LeadingDivider = leadingDivider;
        }

        public HudBox Box { get; }
        public string Text { get; }
        public HudTone Tone { get; }

        /// <summary>True when a hairline rule is drawn immediately left of this segment.</summary>
        public bool LeadingDivider { get; }

        /// <summary>The hairline rule left of this segment, or empty when it has none.</summary>
        public HudBox Divider => LeadingDivider
            ? new HudBox(Box.X - HudShell.SegmentGap * 0.5f, Box.Y + HudShell.DividerInset,
                1f, Box.Height - HudShell.DividerInset * 2f)
            : HudBox.Empty;
    }

    /// <summary>One workspace tab in the top bar's navigation strip.</summary>
    public readonly struct HudNavTab
    {
        public HudNavTab(HudWorkspace workspace, string label, HudBox box, bool selected)
        {
            Workspace = workspace;
            Label = label ?? string.Empty;
            Box = box;
            Selected = selected;
        }

        public HudWorkspace Workspace { get; }
        public string Label { get; }
        public HudBox Box { get; }
        public bool Selected { get; }

        /// <summary>The 3 pt accent rule under a selected tab.</summary>
        public HudBox Underline => new(Box.X, Box.Bottom - 3f, Box.Width, 3f);
    }

    /// <summary>
    /// The persistent shell every page shares (ADR 0057): the slim top bar with the
    /// airline mark, live career values and the five workspace tabs, plus the standard
    /// title/body/footer split every workspace surface uses.
    ///
    /// Pure layout and text placement — no UnityEngine, no simulation decisions — so the
    /// same numbers drive the runtime HUD, the headless layout tests and the offline
    /// mockup renderer.
    /// </summary>
    public static class HudShell
    {
        public const float MarkSize = 22f;
        public const float EdgePadding = 14f;
        public const float SegmentGap = 22f;
        public const float DividerInset = 10f;
        public const float WordmarkMinWidth = 96f;
        public const float WordmarkMaxWidth = 210f;

        /// <summary>Workspace surface: the title band above a hairline, then the body.</summary>
        public const float HeaderHeight = 62f;
        public const float FooterHeight = 40f;
        public const float SurfacePadding = 22f;

        /// <summary>Outer margin the whole HUD keeps clear of the window edge.</summary>
        public const float Margin = 16f;

        public const float TopBarHeight = 44f;
        public const float ObjectiveWidth = 360f;
        public const float ObjectiveHeight = 122f;
        public const float GuideHeight = 104f;
        public const float NavStripMaxWidth = 460f;
        public const float NavTabMinWidth = 86f;

        /// <summary>
        /// Below this, a workspace beside the objective card would be unusable, so it takes
        /// the full width instead and the card gives way.
        /// </summary>
        public const float MinimumWorkspaceWidth = 560f;

        /// <summary>Gap between the top bar and whatever sits under it.</summary>
        public const float ContentGap = 10f;

        public static readonly (HudWorkspace workspace, string label)[] Tabs =
        {
            (HudWorkspace.Operations, "OPERATIONS"),
            (HudWorkspace.Map, "MAP"),
            (HudWorkspace.Fleet, "FLEET"),
            (HudWorkspace.Contracts, "CONTRACTS"),
            (HudWorkspace.Stats, "STATS")
        };

        /// <summary>The persistent full-width status and navigation strip.</summary>
        public static HudBox TopBar(float viewportWidth, float viewportHeight) =>
            new(0f, 0f, viewportWidth, Min(TopBarHeight, Max(1f, viewportHeight)));

        /// <summary>The five workspace tabs, right-aligned inside <see cref="TopBar"/>.</summary>
        public static HudBox NavStrip(float viewportWidth, float viewportHeight)
        {
            var bar = TopBar(viewportWidth, viewportHeight);
            var inner = Max(1f, viewportWidth - Margin * 2f);
            var width = Min(NavStripMaxWidth, Max(NavTabMinWidth * Tabs.Length, inner * 0.38f));
            width = Min(width, Max(1f, viewportWidth * 0.5f));
            return new HudBox(viewportWidth - width, 0f, width, bar.Height);
        }

        /// <summary>
        /// The current-objective card, top-left under the bar. It is part of the persistent
        /// shell: the same card is in the same place on the overview and on every workspace,
        /// which is what makes the five pages read as one screen rather than five separate ones.
        /// </summary>
        public static HudBox Objective(float viewportWidth, float viewportHeight, bool showGuide)
        {
            var top = TopBar(viewportWidth, viewportHeight).Bottom + ContentGap;
            var floor = viewportHeight - Margin;
            var width = Min(ObjectiveWidth, Max(1f, viewportWidth - Margin * 2f));
            var height = showGuide ? GuideHeight : ObjectiveHeight;
            if (top >= floor - 0.01f)
                return new HudBox(Margin, Max(0f, floor - 1f), width, 1f);
            return new HudBox(Margin, top, width, Max(1f, Min(height, floor - top)));
        }

        /// <summary>
        /// The one open workspace surface. It sits beside the objective card so the shell
        /// never moves; on a window too narrow for both, the workspace takes the full width.
        /// </summary>
        public static HudBox WorkspaceSurface(float viewportWidth, float viewportHeight)
        {
            var top = TopBar(viewportWidth, viewportHeight).Bottom + ContentGap;
            var floor = viewportHeight - Margin;
            var beside = Margin + ObjectiveWidth + Margin;
            var width = viewportWidth - beside - Margin;
            var x = beside;
            if (width < MinimumWorkspaceWidth)
            {
                x = Margin;
                width = Max(1f, viewportWidth - Margin * 2f);
            }

            return new HudBox(x, top, width, Max(1f, floor - top));
        }

        /// <summary>True when the objective card stays visible beside an open workspace.</summary>
        public static bool ObjectiveSurvivesWorkspace(float viewportWidth, float viewportHeight) =>
            WorkspaceSurface(viewportWidth, viewportHeight).X > Margin + 1f;

        /// <summary>The airline mark, left-aligned and vertically centred in the bar.</summary>
        public static HudBox MarkBox(HudBox bar) =>
            new(bar.X + EdgePadding, bar.Y + (bar.Height - MarkSize) * 0.5f, MarkSize, MarkSize);

        /// <summary>The airline wordmark, sized to the name but clamped so the values always fit.</summary>
        public static HudBox WordmarkBox(HudBox bar, string airlineName)
        {
            var mark = MarkBox(bar);
            var measured = Measure(airlineName, 13f, letterSpacing: 2f);
            var width = Clamp(measured, WordmarkMinWidth, WordmarkMaxWidth);
            return new HudBox(mark.Right + 12f, bar.Y, width, bar.Height);
        }

        /// <summary>
        /// Live career values between the wordmark and the navigation strip: Adelaide time,
        /// funds, reliability and operating tier, each preceded by a hairline divider.
        /// Segments that cannot fit are dropped from the right rather than overlapping the tabs.
        /// </summary>
        public static void FillSegments(HudBox bar, string airlineName, HudBox navStrip,
            IReadOnlyList<string> values, List<HudTopBarSegment> into)
        {
            into.Clear();
            if (values == null || values.Count == 0)
                return;

            var x = WordmarkBox(bar, airlineName).Right + SegmentGap;
            var limit = navStrip.IsEmpty ? bar.Right - EdgePadding : navStrip.X - SegmentGap;
            for (var i = 0; i < values.Count; i++)
            {
                // Caption tracking is a tenth of the size; measuring without it is how a
                // value ended up touching the first workspace tab on a narrow window.
                var width = Measure(values[i], 12f, letterSpacing: 1.2f) + 6f;
                if (x + width > limit)
                    break;
                into.Add(new HudTopBarSegment(new HudBox(x, bar.Y, width, bar.Height), values[i],
                    i == 0 ? HudTone.Default : HudTone.Muted, leadingDivider: true));
                x += width + SegmentGap;
            }
        }

        /// <summary>The five workspace tabs, right-aligned in <paramref name="navStrip"/>.</summary>
        public static void FillTabs(HudBox navStrip, HudWorkspace active, List<HudNavTab> into)
        {
            into.Clear();
            if (navStrip.IsEmpty)
                return;
            var slot = navStrip.Width / Tabs.Length;
            for (var i = 0; i < Tabs.Length; i++)
            {
                var (workspace, label) = Tabs[i];
                // The default overview is Operations without its board open, so the tab still
                // reads as the page you are on rather than leaving all four unselected.
                var selected = active == workspace
                               || (workspace == HudWorkspace.Operations && active == HudWorkspace.None);
                into.Add(new HudNavTab(workspace, label,
                    new HudBox(navStrip.X + i * slot, navStrip.Y, slot, navStrip.Height), selected));
            }
        }

        /// <summary>Title band of a workspace surface.</summary>
        public static HudBox Header(HudBox surface) => surface.SliceTop(HeaderHeight);

        /// <summary>Hairline under the title band.</summary>
        public static HudBox HeaderRule(HudBox surface) =>
            new(surface.X + SurfacePadding, surface.Y + HeaderHeight, surface.Width - SurfacePadding * 2f, 1f);

        /// <summary>Everything between the title band and the footer.</summary>
        public static HudBox Body(HudBox surface, bool hasFooter)
        {
            var top = surface.Y + HeaderHeight + 1f;
            var bottom = hasFooter ? surface.Bottom - FooterHeight : surface.Bottom;
            return new HudBox(surface.X + SurfacePadding, top + 12f,
                surface.Width - SurfacePadding * 2f, bottom - top - 24f);
        }


        /// <summary>Footer strip and the hairline above it.</summary>
        public static HudBox Footer(HudBox surface) => surface.SliceBottom(FooterHeight);

        public static HudBox FooterRule(HudBox surface) =>
            new(surface.X + SurfacePadding, surface.Bottom - FooterHeight,
                surface.Width - SurfacePadding * 2f, 1f);

        /// <summary>
        /// Approximate rendered width of HUD text, in points. The runtime uses Unity's own
        /// glyph metrics for hit testing; this is only used to size bars and columns, where
        /// a consistent, editor-free estimate matters more than exactness — and it is the
        /// same estimate the offline mockup renderer checks against.
        /// </summary>
        public static float Measure(string text, float fontSize, float letterSpacing = 0f)
        {
            if (string.IsNullOrEmpty(text))
                return 0f;
            // 0.70 em per glyph: generous enough to cover bold capitals in both Unity's
            // default sans face and the one the offline renderer has, because a box that
            // measures short clips its own text rather than merely looking loose.
            return text.Length * (fontSize * 0.70f + letterSpacing);
        }

        private static float Clamp(float value, float min, float max) =>
            value < min ? min : value > max ? max : value;

        private static float Min(float a, float b) => a < b ? a : b;
        private static float Max(float a, float b) => a > b ? a : b;
    }

    /// <summary>
    /// Paints the persistent shell — identical on every page — into the shared draw list:
    /// the top bar and the current-objective card.
    /// </summary>
    public static class HudShellPainter
    {
        public const string WorkspacePrefix = "workspace:";

        public static string WorkspaceAction(HudWorkspace workspace) => WorkspacePrefix + workspace;

        public static void PaintTopBar(HudDrawList into, HudBox bar, HudBox navStrip, string airlineName,
            string liveryHex, IReadOnlyList<HudTopBarSegment> segments, IReadOnlyList<HudNavTab> tabs)
        {
            if (into == null)
                return;

            into.Fill(bar, HudTone.Default, 1f, AirsidePalette.RunwayInkHex);
            into.Hairline(new HudBox(bar.X, bar.Bottom - 1f, bar.Width, 1f), HudTone.Accent, 0.5f);

            var mark = HudShell.MarkBox(bar);
            into.Fill(new HudBox(mark.X, mark.Y + 4f, 5f, mark.Height - 8f), HudTone.Default, 1f, liveryHex);
            var wordmark = HudShell.WordmarkBox(bar, airlineName);
            into.Text(wordmark.Inset(0f, (bar.Height - 18f) * 0.5f, 0f, 0f).WithHeight(18f),
                (airlineName ?? string.Empty).ToUpperInvariant(), 13f, HudTone.Default,
                HudTextStyle.Bold | HudTextStyle.Caption);

            if (segments != null)
            {
                foreach (var segment in segments)
                {
                    if (segment.LeadingDivider)
                        into.Hairline(segment.Divider, HudTone.Muted, 0.28f);
                    into.Text(segment.Box.Inset(0f, (bar.Height - 16f) * 0.5f, 0f, 0f).WithHeight(16f),
                        segment.Text, 12f, segment.Tone, HudTextStyle.Bold | HudTextStyle.Caption);
                }
            }

            if (tabs == null || tabs.Count == 0)
                return;

            // "CONTRACTS" is the longest label; a narrow window shrinks every tab together
            // rather than clipping one of them off the right edge.
            var slot = tabs[0].Box.Width;
            var fontSize = 12f;
            while (fontSize > 9f && HudShell.Measure("CONTRACTS", fontSize, fontSize * 0.1f) > slot - 10f)
                fontSize -= 0.5f;

            foreach (var tab in tabs)
            {
                if (tab.Selected)
                {
                    into.Fill(tab.Box, HudTone.Accent, 0.9f);
                    into.Fill(tab.Underline, HudTone.Default, 0.9f, AirsidePalette.OpenSkyHex);
                }

                into.Text(tab.Box.Inset(0f, (tab.Box.Height - 16f) * 0.5f, 0f, 0f).WithHeight(16f),
                    tab.Label, fontSize, tab.Selected ? HudTone.Default : HudTone.Muted,
                    HudTextStyle.Bold | HudTextStyle.Caption, HudAlign.Center);
                into.Hotspot(tab.Box, WorkspaceAction(tab.Workspace));
            }
        }

        /// <summary>The current-objective card: one career goal, its progress, and the next step.</summary>
        public static void PaintObjective(HudDrawList into, HudBox box, CareerObjective objective)
        {
            if (into == null || box.IsEmpty)
                return;

            into.Fill(box, HudTone.Default, 0.94f, AirsidePalette.RunwayInkHex);
            into.Outline(box, HudTone.Accent, 0.4f);

            var x = box.X + 14f;
            var width = box.Width - 28f;
            into.Caption(new HudBox(x, box.Y + 8f, width, 14f), "CURRENT OBJECTIVE");
            into.Text(new HudBox(x, box.Y + 24f, width, 24f), objective.Title, 16f, HudTone.Default,
                HudTextStyle.Bold | HudTextStyle.Wrap);
            into.Text(new HudBox(x, box.Y + 50f, width, 16f), objective.ProgressText, 11f, HudTone.Muted);
            into.Bar(new HudBox(x, box.Y + 70f, width - 34f, 8f), objective.Progress01, HudTone.Caution);
            into.Text(new HudBox(x + width - 30f, box.Y + 64f, 30f, 16f),
                $"{(int)(objective.Progress01 * 100f)}%", 11f, HudTone.Muted, HudTextStyle.Regular,
                HudAlign.Right);
            into.Text(new HudBox(x, box.Y + 84f, width, 30f), objective.NextLine, 12f,
                objective.NextSeverity == StatusSeverity.Warning ? HudTone.Negative : HudTone.Caution,
                HudTextStyle.Bold | HudTextStyle.Wrap);
        }
    }
}
