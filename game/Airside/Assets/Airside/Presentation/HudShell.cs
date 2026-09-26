using System;
using System.Collections.Generic;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>One workspace button on the vertical navigation rail.</summary>
    public readonly struct HudNavTab
    {
        public HudNavTab(HudWorkspace workspace, string label, HudBox box, bool selected,
            string iconCategory = "", string iconName = "")
        {
            Workspace = workspace;
            Label = label ?? string.Empty;
            Box = box;
            Selected = selected;
            IconCategory = iconCategory ?? string.Empty;
            IconName = iconName ?? string.Empty;
        }

        public HudWorkspace Workspace { get; }
        public string Label { get; }
        public HudBox Box { get; }
        public bool Selected { get; }
        public string IconCategory { get; }
        public string IconName { get; }

        /// <summary>The aqua pill behind a selected rail item.</summary>
        public HudBox Highlight => Box.Inset(6f, 4f, 6f, 4f);
    }

    /// <summary>What a status-capsule segment shows.</summary>
    public enum HudCapsuleKind
    {
        /// <summary>A small caption over a large value.</summary>
        Readout,
        /// <summary>A readout with a thin gauge under the value (reliability).</summary>
        Gauge,
        /// <summary>A rounded chip (operating tier).</summary>
        Chip
    }

    /// <summary>One instrument in the top status capsule.</summary>
    public readonly struct HudCapsuleSegment
    {
        public HudCapsuleSegment(HudBox box, string caption, string value, HudTone tone,
            HudCapsuleKind kind = HudCapsuleKind.Readout, float gauge01 = 0f)
        {
            Box = box;
            Caption = caption ?? string.Empty;
            Value = value ?? string.Empty;
            Tone = tone;
            Kind = kind;
            Gauge01 = gauge01;
        }

        public HudBox Box { get; }
        public string Caption { get; }
        public string Value { get; }
        public HudTone Tone { get; }
        public HudCapsuleKind Kind { get; }
        public float Gauge01 { get; }
    }

    /// <summary>A capsule value before placement.</summary>
    public readonly struct HudCapsuleValue
    {
        public HudCapsuleValue(string caption, string value, HudTone tone = HudTone.Default,
            HudCapsuleKind kind = HudCapsuleKind.Readout, float gauge01 = 0f)
        {
            Caption = caption ?? string.Empty;
            Value = value ?? string.Empty;
            Tone = tone;
            Kind = kind;
            Gauge01 = gauge01;
        }

        public string Caption { get; }
        public string Value { get; }
        public HudTone Tone { get; }
        public HudCapsuleKind Kind { get; }
        public float Gauge01 { get; }
    }

    /// <summary>
    /// Where every persistent Glass Cockpit panel sits (ADR 0122), in virtual points. Pure data so
    /// the fits-and-never-overlaps contract is tested headlessly at every supported window.
    /// A zero-size box means "not shown at this window size".
    /// </summary>
    public readonly struct HudShellLayout
    {
        public HudShellLayout(HudBox rail, HudBox capsule, HudBox career, HudBox operations, HudBox workspace,
            HudBox toast, HudBox miniMap, HudBox selectedCard, HudBox setupArea)
        {
            Rail = rail;
            Capsule = capsule;
            Career = career;
            Operations = operations;
            Workspace = workspace;
            Toast = toast;
            MiniMap = miniMap;
            SelectedCard = selectedCard;
            SetupArea = setupArea;
        }

        /// <summary>Left vertical navigation rail: brand mark, five workspaces, help.</summary>
        public HudBox Rail { get; }

        /// <summary>Top status capsule: airline, Adelaide time, funds, reliability, tier.</summary>
        public HudBox Capsule { get; }

        /// <summary>Bottom-left career ring card (or the first-flight guide while it runs).</summary>
        public HudBox Career { get; }

        /// <summary>Top-right live flight tiles for the player's fleet.</summary>
        public HudBox Operations { get; }

        /// <summary>The one open workspace sheet, right of the rail and under the capsule.</summary>
        public HudBox Workspace { get; }

        /// <summary>Where the newest toast sits; older ones stack below it.</summary>
        public HudBox Toast { get; }

        /// <summary>Bottom-right airfield radar.</summary>
        public HudBox MiniMap { get; }

        /// <summary>Bottom-centre selected-aircraft reservation (the card is usually shorter).</summary>
        public HudBox SelectedCard { get; }

        /// <summary>The centred region modal cards (setup, away summary) are fitted into.</summary>
        public HudBox SetupArea { get; }

        /// <summary>Every persistent panel shown at this size, for overlap checks and hit testing.</summary>
        public IEnumerable<(string Name, HudBox Box)> Panels
        {
            get
            {
                yield return ("rail", Rail);
                yield return ("capsule", Capsule);
                yield return ("career", Career);
                yield return ("operations", Operations);
                yield return ("workspace", Workspace);
                yield return ("toast", Toast);
                yield return ("minimap", MiniMap);
                yield return ("selected", SelectedCard);
            }
        }
    }

    /// <summary>
    /// The Glass Cockpit shell (ADR 0122) every page shares: a vertical navigation rail on the left,
    /// a floating status capsule at the top, the career ring card bottom-left, live flight tiles
    /// top-right, the airfield radar bottom-right and the selected-aircraft card bottom-centre. An
    /// open workspace is one large glass sheet right of the rail. Pure layout — no UnityEngine and
    /// no simulation decisions — so the runtime HUD, the headless tests and the offline mockup
    /// renderer all place it identically.
    /// </summary>
    public static class HudShell
    {
        /// <summary>Outer margin the whole HUD keeps clear of the window edge.</summary>
        public const float Margin = 16f;

        /// <summary>Gap between neighbouring floating panels.</summary>
        public const float Gap = 10f;

        public const float RailWidth = 76f;
        public const float RailMarkHeight = 70f;
        public const float RailItemHeight = 64f;
        public const float RailItemMinHeight = 46f;
        public const float RailFootHeight = 12f;

        public const float CapsuleHeight = 52f;
        public const float CapsuleMaxWidth = 700f;
        public const float CapsuleMinWidth = 300f;

        /// <summary>A long airline name is clipped rather than pushing every other readout out.</summary>
        public const float MaxReadoutWidth = 150f;

        public const float CareerWidth = 340f;
        public const float CareerHeight = 150f;
        public const float GuideHeight = CareerHeight;

        public const float OperationsWidth = 300f;
        public const float OperationsMaxHeight = 320f;
        public const float OperationsTileHeight = 52f;
        public const float OperationsHeaderHeight = 34f;

        public const float MiniMapWidth = 236f;
        public const float MiniMapHeight = 150f;

        public const float SelectedCardWidth = 460f;
        public const float SelectedCardHeight = 220f;
        public const float SelectedCardMinWidth = 300f;

        public const float ToastWidth = 420f;
        public const float ToastHeight = 42f;
        public const float ToastGap = 6f;

        public const float SetupWidth = 460f;

        /// <summary>Workspace sheet: the title band, then the body, then an optional footer.</summary>
        public const float HeaderHeight = 64f;
        public const float FooterHeight = 44f;
        public const float SurfacePadding = 24f;

        /// <summary>The narrowest sheet a two-column page is laid out for.</summary>
        public const float MinimumWorkspaceWidth = 560f;

        public static readonly (HudWorkspace workspace, string label)[] Tabs =
        {
            (HudWorkspace.Operations, "OPS"),
            (HudWorkspace.Map, "MAP"),
            (HudWorkspace.Fleet, "FLEET"),
            (HudWorkspace.Contracts, "CONTRACTS"),
            (HudWorkspace.Stats, "CAREER")
        };

        /// <summary>The approved UI line icon for each rail item (Art/UI/Icons).</summary>
        public static (string Category, string Name) TabIcon(HudWorkspace workspace) => workspace switch
        {
            HudWorkspace.Operations => ("operation", "departure"),
            HudWorkspace.Map => ("economy", "route"),
            HudWorkspace.Fleet => ("operation", "stand"),
            HudWorkspace.Contracts => ("economy", "income"),
            HudWorkspace.Stats => ("economy", "reputation"),
            _ => ("system", "overview")
        };

        /// <summary>The navigation rail, full height when there is room for every item.</summary>
        public static HudBox Rail(float viewportWidth, float viewportHeight)
        {
            var available = Max(1f, viewportHeight - Margin * 2f);
            var itemHeight = RailItemHeightFor(viewportHeight);
            var height = Min(available, RailMarkHeight + itemHeight * Tabs.Length + RailFootHeight);
            return new HudBox(Margin, Margin, Min(RailWidth, Max(1f, viewportWidth - Margin * 2f)), height);
        }

        /// <summary>Item height after shrinking to fit a short window (never below a clickable size).</summary>
        public static float RailItemHeightFor(float viewportHeight)
        {
            var available = viewportHeight - Margin * 2f - RailMarkHeight - RailFootHeight;
            return Clamp(available / Tabs.Length, RailItemMinHeight, RailItemHeight);
        }

        /// <summary>The brand-mark slot at the top of the rail.</summary>
        public static HudBox RailMark(HudBox rail) =>
            new(rail.X + (rail.Width - 40f) * 0.5f, rail.Y + 14f, 40f, 40f);

        /// <summary>The five workspace items, stacked under the brand mark.</summary>
        public static void FillTabs(HudBox rail, float viewportHeight, HudWorkspace active, List<HudNavTab> into)
        {
            into.Clear();
            if (rail.IsEmpty)
                return;
            var itemHeight = RailItemHeightFor(viewportHeight);
            var y = rail.Y + RailMarkHeight;
            for (var i = 0; i < Tabs.Length; i++)
            {
                var (workspace, label) = Tabs[i];
                // The overview is Operations without its board open, so exactly one item reads as
                // the current page even when no sheet is open.
                var selected = active == workspace
                               || (workspace == HudWorkspace.Operations && active == HudWorkspace.None);
                var (category, name) = TabIcon(workspace);
                var box = new HudBox(rail.X, y, rail.Width, itemHeight);
                if (box.Bottom > rail.Bottom + 0.01f)
                    break;
                into.Add(new HudNavTab(workspace, label, box, selected, category, name));
                y += itemHeight;
            }
        }

        /// <summary>The top status capsule: centred when it fits, otherwise beside the rail.</summary>
        public static HudBox Capsule(float viewportWidth, float viewportHeight)
        {
            var rail = Rail(viewportWidth, viewportHeight);
            var left = rail.Right + Gap;
            var right = viewportWidth - Margin - OperationsWidth - Gap;
            var room = right - left;
            if (room < CapsuleMinWidth)
                right = viewportWidth - Margin;
            room = Max(1f, right - left);
            var width = Min(CapsuleMaxWidth, room);
            var centred = (viewportWidth - width) * 0.5f;
            var x = centred >= left && centred + width <= right ? centred : left;
            return new HudBox(x, Margin, width, Min(CapsuleHeight, Max(1f, viewportHeight - Margin * 2f)));
        }

        /// <summary>
        /// Places the capsule's instruments left to right: airline, time, funds, reliability, tier.
        /// Readouts that cannot fit are dropped from the right rather than overlapping.
        /// </summary>
        public static void FillCapsule(HudBox capsule, IReadOnlyList<HudCapsuleValue> values,
            List<HudCapsuleSegment> into)
        {
            into.Clear();
            if (values == null || capsule.IsEmpty)
                return;
            var x = capsule.X + 20f;
            var limit = capsule.Right - 16f;
            foreach (var value in values)
            {
                var width = value.Kind == HudCapsuleKind.Chip
                    ? Measure(value.Value, 10f, 1f) + 26f
                    : Min(MaxReadoutWidth, Max(Measure(value.Caption, 9f, 0.9f), Measure(value.Value, 15f)) + 4f);
                if (x + width > limit)
                    break;
                into.Add(new HudCapsuleSegment(new HudBox(x, capsule.Y, width, capsule.Height),
                    value.Caption, value.Value, value.Tone, value.Kind, value.Gauge01));
                x += width + 26f;
            }
        }

        /// <summary>The whole persistent shell for one window size and HUD state.</summary>
        public static HudShellLayout Layout(float viewportWidth, float viewportHeight, bool showGuide = false,
            bool workspaceOpen = false)
        {
            var width = viewportWidth;
            var height = viewportHeight;
            var floor = height - Margin;
            var rail = Rail(width, height);
            var capsule = Capsule(width, height);
            var workspace = WorkspaceSurface(width, height);

            // Bottom-left career card; if the rail reaches down that far, it sits beside the rail.
            var careerWidth = Min(CareerWidth, Max(1f, width - Margin * 2f));
            var careerHeight = showGuide ? GuideHeight : CareerHeight;
            var career = new HudBox(Margin, floor - careerHeight, careerWidth, careerHeight);
            if (career.Y < rail.Bottom + Gap)
            {
                careerWidth = Min(CareerWidth, Max(1f, width - rail.Right - Gap - Margin));
                career = new HudBox(rail.Right + Gap, floor - careerHeight, careerWidth, careerHeight);
            }
            if (career.Y < capsule.Bottom + Gap || career.Width < 220f)
                career = Hidden(career);

            // Bottom-right radar.
            var mini = new HudBox(width - Margin - MiniMapWidth, floor - MiniMapHeight, MiniMapWidth, MiniMapHeight);
            if (mini.X < career.Right + Gap + SelectedCardMinWidth + Gap || mini.Y < capsule.Bottom + Gap)
                mini = Hidden(mini);

            // Top-right live flight tiles, never reaching the radar.
            var opsBottom = mini.IsEmpty ? floor - (career.IsEmpty ? 0f : 0f) : mini.Y - Gap;
            var opsX = width - Margin - OperationsWidth;
            var operations = new HudBox(opsX, Margin, OperationsWidth,
                Min(OperationsMaxHeight, Max(0f, opsBottom - Margin)));
            if (operations.X < capsule.Right + Gap - 0.01f || operations.Height < OperationsHeaderHeight + OperationsTileHeight)
                operations = Hidden(operations);

            // Bottom-centre selected aircraft, between the career card and the radar.
            var leftEdge = career.IsEmpty ? rail.Right + Gap : career.Right + Gap;
            if (rail.Bottom + Gap < floor - SelectedCardHeight && career.IsEmpty)
                leftEdge = Margin;
            var rightEdge = mini.IsEmpty ? width - Margin : mini.X - Gap;
            var selectedWidth = Min(SelectedCardWidth, rightEdge - leftEdge);
            var selectedHeight = Min(SelectedCardHeight, Max(0f, floor - capsule.Bottom - Gap));
            var selected = new HudBox(leftEdge + (rightEdge - leftEdge - selectedWidth) * 0.5f,
                floor - selectedHeight, selectedWidth, selectedHeight);
            if (selectedWidth < SelectedCardMinWidth || selectedHeight < 88f)
                selected = Hidden(selected);
            if (!selected.IsEmpty && !operations.IsEmpty && selected.Overlaps(operations))
                operations = operations.Bottom > selected.Y - Gap && selected.Y - Gap - operations.Y
                             >= OperationsHeaderHeight + OperationsTileHeight
                    ? operations.WithHeight(selected.Y - Gap - operations.Y)
                    : Hidden(operations);

            // Toasts: under the capsule on the overview; in the tiles' corner over an open sheet.
            HudBox toast;
            if (workspaceOpen)
            {
                var toastWidth = Min(ToastWidth, width - capsule.Right - Gap - Margin);
                toast = new HudBox(width - Margin - toastWidth, Margin, toastWidth, ToastHeight);
                if (toastWidth < 200f)
                    toast = Hidden(toast);
            }
            else
            {
                var toastWidth = Min(ToastWidth, capsule.Width);
                toast = new HudBox(capsule.X + (capsule.Width - toastWidth) * 0.5f, capsule.Bottom + Gap,
                    toastWidth, ToastHeight);
                if (toastWidth < 200f)
                    toast = Hidden(toast);
            }

            var setup = new HudBox(Margin, Margin, Max(1f, width - Margin * 2f), Max(1f, height - Margin * 2f));

            if (!workspaceOpen)
                workspace = Hidden(workspace);
            else
            {
                // The sheet replaces the overview panels; only the rail, capsule and toast stay.
                career = Hidden(career);
                operations = Hidden(operations);
                mini = Hidden(mini);
                selected = Hidden(selected);
            }

            return new HudShellLayout(rail, capsule, career, operations, workspace, toast, mini, selected, setup);
        }

        /// <summary>The one open workspace sheet, right of the rail and under the capsule.</summary>
        public static HudBox WorkspaceSurface(float viewportWidth, float viewportHeight)
        {
            var rail = Rail(viewportWidth, viewportHeight);
            var capsule = Capsule(viewportWidth, viewportHeight);
            var x = rail.Right + Gap;
            var top = capsule.Bottom + Gap;
            var floor = viewportHeight - Margin;
            return new HudBox(x, top, Max(1f, viewportWidth - Margin - x), Max(1f, floor - top));
        }

        /// <summary>A modal card centred in <paramref name="area"/>, never larger than it.</summary>
        public static HudBox CentredPanel(HudBox area, float preferredWidth, float preferredHeight)
        {
            var w = Min(preferredWidth, area.Width);
            var h = Min(preferredHeight, area.Height);
            return new HudBox(area.X + (area.Width - w) * 0.5f, area.Y + (area.Height - h) * 0.5f, w, h);
        }

        /// <summary>Title band of a workspace sheet.</summary>
        public static HudBox Header(HudBox surface) => surface.SliceTop(HeaderHeight);

        /// <summary>Rule under the title band.</summary>
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

        /// <summary>Footer strip and the rule above it.</summary>
        public static HudBox Footer(HudBox surface) => surface.SliceBottom(FooterHeight);

        public static HudBox FooterRule(HudBox surface) =>
            new(surface.X + SurfacePadding, surface.Bottom - FooterHeight,
                surface.Width - SurfacePadding * 2f, 1f);

        /// <summary>
        /// Approximate rendered width of HUD text, in points: 0.70 em per glyph, generous enough for
        /// bold capitals in both Unity's default face and the offline renderer's, because a box that
        /// measures short clips its own text rather than merely looking loose.
        /// </summary>
        public static float Measure(string text, float fontSize, float letterSpacing = 0f)
        {
            if (string.IsNullOrEmpty(text))
                return 0f;
            return text.Length * (fontSize * 0.70f + letterSpacing);
        }

        private static HudBox Hidden(HudBox box) => new(box.X, box.Y, 0f, 0f);

        private static float Clamp(float value, float min, float max) =>
            value < min ? min : value > max ? max : value;

        private static float Min(float a, float b) => a < b ? a : b;
        private static float Max(float a, float b) => a > b ? a : b;
    }

    /// <summary>
    /// Paints the persistent Glass Cockpit shell into a draw list: the navigation rail, the status
    /// capsule, the career ring card, the live flight tiles and the shared workspace-sheet header.
    /// </summary>
    public static class HudShellPainter
    {
        public const string WorkspacePrefix = "workspace:";
        public const string HelpAction = "shell:help";

        public static string WorkspaceAction(HudWorkspace workspace) => WorkspacePrefix + workspace;

        /// <summary>The capsule's instruments for the live airline, shared by the runtime and the mockups.</summary>
        public static void CapsuleValues(AirlineOperations operations, string adelaideTime, List<HudCapsuleValue> into)
        {
            into.Clear();
            var airline = operations?.PlayerAirline;
            var career = operations?.CareerState;
            if (airline == null || career == null)
                return;
            into.Add(new HudCapsuleValue("AIRLINE", airline.Name));
            into.Add(new HudCapsuleValue("ADELAIDE", adelaideTime));
            into.Add(new HudCapsuleValue("FUNDS", "$" + career.Funds.ToString("N0"),
                career.Funds < 0 ? HudTone.Negative : HudTone.Default));
            into.Add(new HudCapsuleValue("RELIABILITY", career.Reliability + "%", HudTone.Default,
                HudCapsuleKind.Gauge, career.Reliability / 100f));
            into.Add(new HudCapsuleValue("TIER", career.FinaleReached ? "ESTABLISHED" : career.Tier.ToString().ToUpperInvariant(),
                HudTone.Accent, HudCapsuleKind.Chip));
        }

        /// <summary>The rail: livery-ringed brand slot, then icon-over-label workspace items.</summary>
        public static void PaintRail(HudDrawList into, HudBox rail, string liveryHex, IReadOnlyList<HudNavTab> tabs)
        {
            if (into == null || rail.IsEmpty)
                return;
            into.Surface(rail, 0.88f);
            var mark = HudShell.RailMark(rail);
            // The airline's livery rings the brand mark; the runtime draws the mark texture inside.
            into.Ring(mark.X + mark.Width * 0.5f, mark.Y + mark.Height * 0.5f, mark.Width + 10f, 1f,
                HudTone.Default, 3f);
            into.Dot(mark.X + mark.Width * 0.5f, mark.Y + mark.Height * 0.5f, mark.Width + 5f, HudTone.Default,
                liveryHex);
            into.Hairline(new HudBox(rail.X + 16f, rail.Y + HudShell.RailMarkHeight - 6f, rail.Width - 32f, 1f),
                HudTone.Muted, 0.25f);
            if (tabs == null)
                return;
            foreach (var tab in tabs)
            {
                var tone = tab.Selected ? HudTone.Accent : HudTone.Muted;
                if (tab.Selected)
                {
                    into.Fill(tab.Highlight, HudTone.Accent, 0.16f);
                    into.Fill(new HudBox(tab.Box.X + 2f, tab.Box.Y + tab.Box.Height * 0.25f, 3f,
                        tab.Box.Height * 0.5f), HudTone.Accent, 1f);
                }
                var iconSize = tab.Box.Height >= 56f ? 24f : 18f;
                into.Icon(new HudBox(tab.Box.X + (tab.Box.Width - iconSize) * 0.5f,
                        tab.Box.Y + (tab.Box.Height - iconSize - 14f) * 0.5f, iconSize, iconSize),
                    tab.IconCategory, tab.IconName, tab.Selected ? HudTone.Accent : HudTone.Default,
                    tab.Selected ? 1f : 0.72f);
                into.Text(new HudBox(tab.Box.X, tab.Box.Bottom - 6f - (tab.Box.Height - iconSize - 14f) * 0.5f - 12f,
                        tab.Box.Width, 12f), tab.Label, tab.Label.Length > 7 ? 7.5f : 9f, tone, HudTextStyle.Bold | HudTextStyle.Caption,
                    HudAlign.Center);
                into.Hotspot(tab.Box, WorkspaceAction(tab.Workspace));
            }
        }

        /// <summary>The status capsule: caption-over-value instruments with a gauge and a tier chip.</summary>
        public static void PaintCapsule(HudDrawList into, HudBox capsule, IReadOnlyList<HudCapsuleSegment> segments)
        {
            if (into == null || capsule.IsEmpty)
                return;
            into.Surface(capsule, 0.86f);
            if (segments == null)
                return;
            for (var i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                var box = segment.Box;
                if (i > 0)
                    into.Dot(box.X - 13f, box.Y + box.Height * 0.5f, 3f, HudTone.Muted);
                switch (segment.Kind)
                {
                    case HudCapsuleKind.Chip:
                        into.Pill(new HudBox(box.X, box.Y + (box.Height - 22f) * 0.5f, box.Width, 22f),
                            segment.Value, segment.Tone, filled: true, fontSize: 10f);
                        break;
                    default:
                        into.Caption(new HudBox(box.X, box.Y + 9f, box.Width, 11f), segment.Caption,
                            HudTone.Muted, HudAlign.Left, 9f);
                        into.Text(new HudBox(box.X, box.Y + 21f, box.Width, 20f), segment.Value, 15f,
                            segment.Tone, HudTextStyle.Bold);
                        if (segment.Kind == HudCapsuleKind.Gauge)
                            into.Bar(new HudBox(box.X, box.Bottom - 8f, box.Width, 3f), segment.Gauge01,
                                segment.Gauge01 >= 0.8f ? HudTone.Positive
                                : segment.Gauge01 >= 0.6f ? HudTone.Caution : HudTone.Negative);
                        break;
                }
            }
        }

        /// <summary>
        /// The career ring card: a ring showing how much of the current stage is done, the stage's
        /// target ("TOWARD DOMESTIC"), the pinned goal with its own progress, and the next action.
        /// </summary>
        public static void PaintCareer(HudDrawList into, HudBox box, CareerObjective objective,
            float stage01, string stageText)
        {
            if (into == null || box.IsEmpty)
                return;
            into.Surface(box, 0.88f);
            const float ring = 74f;
            var cx = box.X + 18f + ring * 0.5f;
            var cy = box.Y + 18f + ring * 0.5f;
            into.Ring(cx, cy, ring, 1f, HudTone.Muted, 7f);
            into.Ring(cx, cy, ring, stage01, HudTone.Accent, 7f);
            into.Text(new HudBox(cx - ring * 0.5f, cy - 12f, ring, 20f), stageText ?? string.Empty, 16f,
                HudTone.Default, HudTextStyle.Bold, HudAlign.Center);
            into.Caption(new HudBox(cx - ring * 0.5f, cy + 7f, ring, 10f), "STEPS", HudTone.Muted, HudAlign.Center, 8f);

            var x = box.X + 18f + ring + 16f;
            var width = box.Right - 16f - x;
            into.Caption(new HudBox(x, box.Y + 16f, width, 12f), objective.Caption, HudTone.Accent, HudAlign.Left, 9f);
            into.Text(new HudBox(x, box.Y + 31f, width, 40f), objective.Title, 15f, HudTone.Default,
                HudTextStyle.Bold | HudTextStyle.Wrap);
            into.Bar(new HudBox(x, box.Y + 76f, width, 4f), objective.Progress01, HudTone.Accent);
            into.Text(new HudBox(x, box.Y + 84f, width, 14f), objective.ProgressText, 10f, HudTone.Muted);
            var next = objective.NextLine ?? string.Empty;
            var late = next.IndexOf("delay", StringComparison.OrdinalIgnoreCase) >= 0
                       || next.IndexOf("late", StringComparison.OrdinalIgnoreCase) >= 0;
            into.Fill(new HudBox(box.X + 12f, box.Bottom - 44f, box.Width - 24f, 32f),
                late ? HudTone.Negative : HudTone.Caution, 0.12f);
            into.Dot(box.X + 26f, box.Bottom - 28f, 7f, late ? HudTone.Negative : HudTone.Caution);
            into.Text(new HudBox(box.X + 38f, box.Bottom - 37f, box.Width - 56f, 20f),
                next.StartsWith("Next: ", StringComparison.Ordinal) ? next.Substring(6) : next, 11f,
                late ? HudTone.Negative : HudTone.Caution, HudTextStyle.Bold);
        }

        /// <summary>The first-flight guide in the career card's place: a numbered coaching card.</summary>
        public static void PaintGuide(HudDrawList into, HudBox box, int step, int steps, string heading, string hint)
        {
            if (into == null || box.IsEmpty)
                return;
            into.Surface(box, 0.9f);
            into.Pill(new HudBox(box.X + 16f, box.Y + 14f, 118f, 20f), $"FIRST FLIGHT {step}/{steps}",
                HudTone.Caution, filled: true, fontSize: 9f);
            into.Text(new HudBox(box.X + 16f, box.Y + 42f, box.Width - 32f, 22f), heading, 15f, HudTone.Default,
                HudTextStyle.Bold);
            into.Text(new HudBox(box.X + 16f, box.Y + 66f, box.Width - 32f, box.Height - 76f), hint, 11f,
                HudTone.Muted, HudTextStyle.Wrap);
        }

        /// <summary>
        /// Live flight tiles: one glass tile per player aircraft with a phase chip and a status line,
        /// the priority aircraft outlined in amber. Clicking a tile selects the aircraft.
        /// </summary>
        public static void PaintOperations(HudDrawList into, HudBox area, IReadOnlyList<OperationsRow> rows,
            string selectedRegistration, string footer)
        {
            if (into == null || area.IsEmpty || rows == null)
                return;
            var visible = VisibleTiles(area, rows.Count);
            var height = HudShell.OperationsHeaderHeight + visible * (HudShell.OperationsTileHeight + 6f) + 22f;
            var box = area.WithHeight(Math.Min(area.Height, height));
            into.Surface(box, 0.86f);
            into.Caption(new HudBox(box.X + 16f, box.Y + 13f, box.Width - 32f, 12f), "MY FLIGHTS", HudTone.Muted,
                HudAlign.Left, 9f);
            into.Text(new HudBox(box.X + 16f, box.Y + 12f, box.Width - 32f, 12f), rows.Count + " AIRCRAFT", 9f,
                HudTone.Muted, HudTextStyle.Bold | HudTextStyle.Caption, HudAlign.Right);
            var y = box.Y + HudShell.OperationsHeaderHeight;
            for (var i = 0; i < visible; i++)
            {
                var row = rows[i];
                var tile = new HudBox(box.X + 10f, y, box.Width - 20f, HudShell.OperationsTileHeight);
                into.Card(tile, row.Registration == selectedRegistration ? 1f : 0.8f);
                if (row.IsPriority)
                    into.Outline(tile, HudTone.Caution, 0.85f);
                else if (row.Registration == selectedRegistration)
                    into.Outline(tile, HudTone.Accent, 0.7f);
                var tone = SeverityTone(row.Severity, row.IsPriority);
                into.Dot(tile.X + 14f, tile.Y + 17f, 8f, tone);
                into.Text(new HudBox(tile.X + 26f, tile.Y + 8f, 90f, 18f), row.Registration, 13f, HudTone.Default,
                    HudTextStyle.Bold);
                into.Pill(new HudBox(tile.Right - 70f, tile.Y + 8f, 60f, 18f),
                    string.IsNullOrEmpty(row.Route) ? "—" : row.Route.ToUpperInvariant(), HudTone.Route);
                into.Text(new HudBox(tile.X + 26f, tile.Y + 29f, tile.Width - 36f, 16f), row.State, 11f,
                    row.IsPriority ? HudTone.Caution : HudTone.Muted, row.IsPriority ? HudTextStyle.Bold : HudTextStyle.Regular);
                into.Hotspot(tile, HudAction.Select(row.Registration));
                y += HudShell.OperationsTileHeight + 6f;
            }
            into.Text(new HudBox(box.X + 16f, box.Bottom - 20f, box.Width - 32f, 14f), footer, 10f, HudTone.Muted);
        }

        /// <summary>How many flight tiles fit in <paramref name="area"/>.</summary>
        public static int VisibleTiles(HudBox area, int count)
        {
            var fit = (int)((area.Height - HudShell.OperationsHeaderHeight - 22f) / (HudShell.OperationsTileHeight + 6f));
            return Math.Max(0, Math.Min(count, fit));
        }

        /// <summary>
        /// The shared header of every workspace sheet: an aqua tick, the page title in title case, its
        /// subtitle, and a round close button top-right.
        /// </summary>
        public static void PaintSheetHeader(HudDrawList into, HudBox surface, string title, string subtitle,
            HudBox titleBox, HudBox subtitleBox)
        {
            into.Fill(new HudBox(titleBox.X - 12f, titleBox.Y + 5f, 4f, 22f), HudTone.Accent, 1f);
            into.Text(titleBox, TitleCase(title), 24f, HudTone.Default, HudTextStyle.Bold);
            into.Text(subtitleBox, subtitle, 12f, HudTone.Muted);
            into.Button(CloseBox(surface), "×", HudAction.Close, HudButtonStyle.Secondary);
            into.Hairline(HudShell.HeaderRule(surface), HudTone.Muted, 0.16f);
        }

        /// <summary>The round close button at the sheet's top-right corner.</summary>
        public static HudBox CloseBox(HudBox surface) =>
            new(surface.Right - HudShell.SurfacePadding - 32f, surface.Y + 16f, 32f, 32f);

        /// <summary>The one secondary header action a sheet may carry, just left of the close button.</summary>
        public static HudBox HeaderActionBox(HudBox surface)
        {
            var close = CloseBox(surface);
            return new HudBox(close.X - 118f, close.Y, 106f, close.Height);
        }

        /// <summary>"CAREER ROADMAP" → "Career Roadmap"; already mixed-case titles are left alone.</summary>
        public static string TitleCase(string title)
        {
            if (string.IsNullOrEmpty(title) || title != title.ToUpperInvariant())
                return title ?? string.Empty;
            var chars = title.ToLowerInvariant().ToCharArray();
            var start = true;
            for (var i = 0; i < chars.Length; i++)
            {
                if (start && char.IsLetter(chars[i]))
                    chars[i] = char.ToUpperInvariant(chars[i]);
                start = chars[i] == ' ' || chars[i] == '-' || chars[i] == '·';
            }
            return new string(chars);
        }

        public static HudTone SeverityTone(StatusSeverity severity, bool priority) => severity switch
        {
            StatusSeverity.Warning => HudTone.Negative,
            StatusSeverity.Attention => HudTone.Caution,
            _ => priority ? HudTone.Caution : HudTone.Positive
        };
    }
}

namespace Airside.Presentation
{
    /// <summary>A transient notice: a glass pill with a coloured leading dot (ADR 0122).</summary>
    public static class ToastPainter
    {
        public static void Paint(HudDrawList into, HudBox box, string text, HudTone tone, float alpha)
        {
            if (into == null || box.IsEmpty || string.IsNullOrEmpty(text) || alpha <= 0.01f)
                return;
            into.Surface(box, 0.92f * alpha);
            into.Dot(box.X + 20f, box.Y + box.Height * 0.5f, 9f, tone);
            into.Text(new HudBox(box.X + 34f, box.Y + (box.Height - 16f) * 0.5f, box.Width - 48f, 18f), text, 12f,
                HudTone.Default, HudTextStyle.Bold, HudAlign.Left, null, alpha);
        }
    }

    /// <summary>The airfield radar's glass frame and header; the runtime paints the airfield inside.</summary>
    public static class MiniMapFrame
    {
        public const float HeaderHeight = 22f;

        public static HudBox Inner(HudBox box) => new(box.X + 8f, box.Y + HeaderHeight + 2f, box.Width - 16f,
            box.Height - HeaderHeight - 10f);

        public static void Paint(HudDrawList into, HudBox box, string header)
        {
            if (into == null || box.IsEmpty)
                return;
            into.Surface(box, 0.88f);
            into.Dot(box.X + 16f, box.Y + 12f, 6f, HudTone.Accent);
            into.Caption(new HudBox(box.X + 26f, box.Y + 6f, box.Width - 36f, 12f), header, HudTone.Muted,
                HudAlign.Left, 9f);
            into.Card(Inner(box), 0.9f);
        }
    }
}
