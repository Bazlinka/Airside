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
        public const float Margin = 20f;

        /// <summary>Gap between neighbouring floating panels.</summary>
        public const float Gap = 16f;

        public const float RailWidth = 72f;
        public const float RailMarkHeight = 8f;
        public const float RailItemHeight = 58f;
        public const float RailItemMinHeight = 46f;
        public const float RailFootHeight = 48f;

        public const float CapsuleHeight = 64f;
        public const float CapsuleMaxWidth = 700f;
        public const float CapsuleMinWidth = 300f;

        /// <summary>A long airline name is clipped rather than pushing every other readout out.</summary>
        public const float MaxReadoutWidth = 210f;

        public const float CareerWidth = 400f;
        public const float CareerHeight = 68f;
        public const float GuideHeight = 144f;

        public const float OperationsWidth = 300f;
        public const float OperationsMaxHeight = 440f;
        public const float OperationsTileHeight = 78f;
        public const float OperationsHeaderHeight = 34f;

        public const float MiniMapWidth = 236f;
        public const float MiniMapHeight = 150f;

        public const float SelectedCardWidth = 328f;
        public const float SelectedCardHeight = 700f;
        public const float SelectedCardMinWidth = 300f;

        public const float ToastWidth = 420f;
        public const float ToastHeight = 68f;
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
            (HudWorkspace.Operations, "Operations"),
            (HudWorkspace.Map, "Map"),
            (HudWorkspace.Fleet, "Fleet"),
            (HudWorkspace.Contracts, "Contracts"),
            (HudWorkspace.Stats, "Career")
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
            var top = Margin + CapsuleHeight + Gap;
            var available = Max(1f, viewportHeight - top - Margin);
            var height = Min(available, RailMarkHeight + RailItemHeightFor(viewportHeight) * Tabs.Length + RailFootHeight);
            return new HudBox(Margin, top, Min(RailWidth, Max(1f, viewportWidth - Margin * 2f)), height);
        }

        /// <summary>Item height after shrinking to fit a short window (never below a clickable size).</summary>
        public static float RailItemHeightFor(float viewportHeight)
        {
            var available = viewportHeight - Margin * 2f - CapsuleHeight - Gap - RailMarkHeight - RailFootHeight;
            return Clamp(available / Tabs.Length, RailItemMinHeight, RailItemHeight);
        }

        /// <summary>The brand-mark slot at the top of the rail.</summary>
        public static HudBox RailMark(HudBox rail) => new(rail.X, rail.Y, 0f, 0f);

        /// <summary>The round "?" at the foot of the rail that opens the Flight Manual.</summary>
        public static HudBox RailHelp(HudBox rail) =>
            new(rail.X + (rail.Width - 34f) * 0.5f, rail.Bottom - 46f, 34f, 34f);

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
            return new HudBox(Margin, Margin, Max(1f, viewportWidth - Margin * 2f),
                Min(CapsuleHeight, Max(1f, viewportHeight - Margin * 2f)));
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
            var limit = capsule.Right - (capsule.Width >= 800f ? 358f : 70f);
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
            bool workspaceOpen = false, bool showMiniMap = false, float trackerHeight = 0f,
            bool showCareerCard = true)
        {
            var width = viewportWidth;
            var height = viewportHeight;
            var floor = height - Margin;
            var rail = Rail(width, height);
            var capsule = Capsule(width, height);
            var left = rail.Right + Gap;
            var available = Max(1f, width - Margin - left);
            var top = capsule.Bottom + Gap;
            var careerHeight = showGuide ? GuideHeight : CareerHeight;
            var career = new HudBox(left, floor - careerHeight, Min(CareerWidth, available), careerHeight);
            if (career.Y < top || career.Width < 220f || !showCareerCard && !showGuide) career = Hidden(career);
            var selectedWidth = Min(SelectedCardWidth, available);
            var selectedHeight = Min(SelectedCardHeight, Max(0f, floor - top));
            var selected = new HudBox(width - Margin - selectedWidth, top, selectedWidth, selectedHeight);
            if (selectedWidth < SelectedCardMinWidth || selectedHeight < 340f
                || !career.IsEmpty && selected.Overlaps(career)) selected = Hidden(selected);
            // Bottom-left stack, from the floor up: career card, flight tracker, airport map.
            var stackTop = career.IsEmpty ? floor : career.Y - Gap;
            var tracker = new HudBox(0, 0, 0, 0);
            if (trackerHeight > 0f)
            {
                var candidate = new HudBox(left, stackTop - trackerHeight, Min(FlightTrackerPainter.Width, available), trackerHeight);
                // Clear of the toast strip along the top-left and of the selected card on the right.
                if (candidate.Y >= top + ToastHeight + ToastGap && candidate.Width >= 280f
                    && (selected.IsEmpty || candidate.Right + Gap <= selected.X))
                {
                    tracker = candidate;
                    stackTop = candidate.Y - Gap;
                }
            }

            var mini = new HudBox(left, stackTop - MiniMapHeight, MiniMapWidth, MiniMapHeight);
            if (!showMiniMap || mini.Y < top || mini.Right > width - Margin
                || !selected.IsEmpty && mini.Overlaps(selected)) mini = Hidden(mini);
            var operations = tracker;
            var toastRoom = (selected.IsEmpty ? width - Margin : selected.X - Gap) - left;
            var toast = new HudBox(left, top, Min(ToastWidth, Max(0f, toastRoom)), ToastHeight);
            if (toast.Width < 200f || toast.Bottom > floor) toast = Hidden(toast);
            var setup = new HudBox(Margin, Margin, Max(1f, width - Margin * 2f), Max(1f, height - Margin * 2f));
            var workspace = WorkspaceSurface(width, height);
            if (!workspaceOpen) workspace = Hidden(workspace);
            else
            {
                career = Hidden(career); mini = Hidden(mini); selected = Hidden(selected); operations = Hidden(operations);
                // Keep command feedback below the sheet, clear of its scrollable body and actions.
                toast = new HudBox(left, workspace.Bottom + ToastGap, Math.Min(ToastWidth, available), ToastHeight);
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
            var floor = viewportHeight - Margin - ToastHeight - ToastGap;
            return new HudBox(x, top, Max(1f, viewportWidth - Margin - x), Max(1f, floor - top));
        }

        /// <summary>
        /// ADR 0135: a side sheet for workspaces that don't need the full width (Fleet, Contracts, Airline):
        /// anchored right, about two-thirds of the workspace, so the live airport stays in view on the left.
        /// Small windows keep the full width, where every layout was tuned.
        /// </summary>
        public static HudBox SideSheet(HudBox workspace)
        {
            if (workspace.Width < SideSheetMinWorkspace)
                return workspace;
            var width = Max(SideSheetMinWidth, workspace.Width * 0.68f);
            return new HudBox(workspace.Right - width, workspace.Y, width, workspace.Height);
        }

        /// <summary>
        /// The largest size up to <paramref name="size"/> at which <paramref name="text"/> fits
        /// <paramref name="width"/>, never below <paramref name="minimum"/> (ADR 0135: side sheets are narrower).
        /// </summary>
        public static float FitFontSize(string text, float size, float width, float minimum)
        {
            var measured = Measure(text, size);
            if (measured <= width || measured <= 0f)
                return size;
            return Max(minimum, size * width / measured);
        }

        public const float SideSheetMinWorkspace = 1150f;
        public const float SideSheetMinWidth = 900f;

        /// <summary>How far a sheet sits to the right while it slides in, and its opacity (ADR 0135).</summary>
        public static (float OffsetX, float Alpha) SheetEntrance(float secondsOpen)
        {
            const float duration = 0.18f;
            var t = secondsOpen <= 0f ? 0f : secondsOpen >= duration ? 1f : secondsOpen / duration;
            var eased = 1f - (1f - t) * (1f - t) * (1f - t);
            return ((1f - eased) * 28f, eased);
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
        public static void CapsuleValues(AirlineOperations operations, string adelaideTime, List<HudCapsuleValue> into,
            long? fundsShown = null, int fundsDirection = 0)
        {
            into.Clear();
            var airline = operations?.PlayerAirline;
            var career = operations?.CareerState;
            if (airline == null || career == null)
                return;
            into.Add(new HudCapsuleValue("AIRLINE", airline.Name));
            into.Add(new HudCapsuleValue("ADELAIDE", adelaideTime));
            // ADR 0132: money counts to its new value, green while rising and red while falling.
            var funds = fundsShown ?? career.Funds;
            into.Add(new HudCapsuleValue("FUNDS", "$" + funds.ToString("N0"),
                fundsDirection > 0 ? HudTone.Positive
                : fundsDirection < 0 || funds < 0 ? HudTone.Negative : HudTone.Default));
            into.Add(new HudCapsuleValue("RELIABILITY", career.Reliability + "%", HudTone.Default,
                HudCapsuleKind.Gauge, career.Reliability / 100f));
            // Tier progress lives in the Career workspace, keeping the overview calm.
        }

        public const string OverviewAction = "shell:overview";
        public const string MiniMapAction = "shell:minimap";
        public const string TowerAction = "shell:tower";
        public const string MenuAction = "shell:menu";

        /// <summary>Width of the floating action group at the top right (Overview, Radar, Tower, Menu).</summary>
        public const float ControlsWidth = 330f;
        public const float ControlsHeight = 48f;

        /// <summary>Where the action group sits: top right, vertically centred on the capsule row.</summary>
        public static HudBox ControlsBox(HudBox capsule)
        {
            if (capsule.IsEmpty)
                return capsule;
            if (capsule.Width < 800f)
                return new HudBox(capsule.Right - 74f, capsule.Y + (capsule.Height - ControlsHeight) * 0.5f, 74f, ControlsHeight);
            return new HudBox(capsule.Right - ControlsWidth, capsule.Y + (capsule.Height - ControlsHeight) * 0.5f,
                ControlsWidth, ControlsHeight);
        }

        /// <summary>The status capsule's real extent: it hugs its readouts instead of spanning the window.</summary>
        public static HudBox CapsuleContent(HudBox capsule, IReadOnlyList<HudCapsuleSegment> segments)
        {
            if (capsule.IsEmpty || segments == null || segments.Count == 0)
                return capsule;
            var right = segments[segments.Count - 1].Box.Right + 22f;
            return new HudBox(capsule.X, capsule.Y, Math.Min(capsule.Width, Math.Max(HudShell.CapsuleMinWidth, right - capsule.X)),
                capsule.Height);
        }

        public static void PaintControls(HudDrawList into, HudBox capsule, bool miniMapVisible)
        {
            var group = ControlsBox(capsule);
            if (group.IsEmpty)
                return;
            into.Surface(group, 0.97f);
            if (capsule.Width < 800f)
            {
                into.Button(new HudBox(group.X + 8f, group.Y + 8f, group.Width - 16f, 32f), "Menu", MenuAction, HudButtonStyle.Secondary);
                return;
            }
            var x = group.X + 8f;
            var y = group.Y + 8f;
            into.Button(new HudBox(x, y, 82f, 32f), "Overview", OverviewAction, HudButtonStyle.Secondary);
            into.Button(new HudBox(x + 86f, y, 92f, 32f), miniMapVisible ? "Hide radar" : "Radar (N)", MiniMapAction, HudButtonStyle.Secondary);
            into.Button(new HudBox(x + 182f, y, 62f, 32f), "Tower", TowerAction, HudButtonStyle.Secondary);
            into.Button(new HudBox(x + 248f, y, 66f, 32f), "Menu", MenuAction, HudButtonStyle.Secondary);
        }

        /// <summary>The rail: livery-ringed brand slot, then icon-over-label workspace items.</summary>
        public static void PaintRail(HudDrawList into, HudBox rail, string liveryHex, IReadOnlyList<HudNavTab> tabs)
        {
            if (into == null || rail.IsEmpty)
                return;
            into.Surface(rail, 0.97f);
            var mark = HudShell.RailMark(rail);
            if (tabs == null)
                return;
            foreach (var tab in tabs)
            {
                var tone = tab.Selected ? HudTone.Accent : HudTone.Muted;
                if (tab.Selected)
                {
                    // A filled aqua pill and edge bar: unmistakably "you are here".
                    into.Fill(tab.Highlight, HudTone.Accent, 0.16f);
                    into.Fill(new HudBox(tab.Box.X + 2f, tab.Box.Y + tab.Box.Height * 0.22f, 3f,
                        tab.Box.Height * 0.56f), HudTone.Accent, 1f);
                }
                var iconSize = tab.Box.Height >= 56f ? 28f : 20f;
                into.Icon(new HudBox(tab.Box.X + (tab.Box.Width - iconSize) * 0.5f,
                        tab.Box.Y + (tab.Box.Height - iconSize - 14f) * 0.5f, iconSize, iconSize),
                    tab.IconCategory, tab.IconName, tab.Selected ? HudTone.Accent : HudTone.Default,
                    tab.Selected ? 1f : 0.62f);
                into.Text(new HudBox(tab.Box.X, tab.Box.Bottom - 6f - (tab.Box.Height - iconSize - 14f) * 0.5f - 13f,
                        tab.Box.Width, 13f), tab.Label, tab.Label.Length > 7 ? 10f : 11f, tone,
                    tab.Selected ? HudTextStyle.Bold : HudTextStyle.Regular, HudAlign.Center);
                into.Hotspot(tab.Box, WorkspaceAction(tab.Workspace));
            }
            var help = HudShell.RailHelp(rail);
            if (tabs.Count > 0 && help.Y >= tabs[tabs.Count - 1].Box.Bottom)
                into.Button(help, "?", HelpAction, HudButtonStyle.Secondary);
        }

        /// <summary>The status capsule: caption-over-value instruments with a gauge and a tier chip.</summary>
        public static void PaintCapsule(HudDrawList into, HudBox capsule, IReadOnlyList<HudCapsuleSegment> segments)
        {
            if (into == null || capsule.IsEmpty)
                return;
            into.Surface(CapsuleContent(capsule, segments), 0.97f);
            if (segments == null)
                return;
            for (var i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                var box = segment.Box;
                if (i > 0)
                    into.Hairline(new HudBox(box.X - 13f, box.Y + 14f, 1f, box.Height - 28f), alpha: 0.14f);
                switch (segment.Kind)
                {
                    case HudCapsuleKind.Chip:
                        into.Pill(new HudBox(box.X, box.Y + (box.Height - 22f) * 0.5f, box.Width, 22f),
                            segment.Value, segment.Tone, filled: true, fontSize: 10f);
                        break;
                    default:
                        into.Caption(new HudBox(box.X, box.Y + 10f, box.Width, 11f), segment.Caption,
                            HudTone.Muted, HudAlign.Left, 9f);
                        into.Text(new HudBox(box.X, box.Y + 24f, box.Width, 22f), segment.Value, 17f,
                            segment.Tone, HudTextStyle.Bold);
                        if (segment.Kind == HudCapsuleKind.Gauge)
                            into.Bar(new HudBox(box.X, box.Bottom - 9f, box.Width, 3f), segment.Gauge01,
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
            into.Surface(box, 0.97f);
            // The stage count sits in an aqua chip, the milestone is the headline, and a bar shows how far through the stage the airline is.
            var chip = new HudBox(box.X + 14f, box.Y + (box.Height - 40f) * 0.5f, 44f, 40f);
            into.Fill(chip, HudTone.Accent, 0.16f);
            into.Text(new HudBox(chip.X, chip.Y + 11f, chip.Width, 18f), stageText ?? string.Empty, 13f, HudTone.Accent,
                HudTextStyle.Bold, HudAlign.Center);
            var x = box.X + 70f;
            var width = box.Width - 86f;
            into.Caption(new HudBox(x, box.Y + 11f, width, 12f), "NEXT MILESTONE", fontSize: 9f);
            into.Text(new HudBox(x, box.Y + 27f, width, 20f), objective.Title, 14f, HudTone.Default, HudTextStyle.Bold);
            into.Bar(new HudBox(x, box.Bottom - 12f, width, 3f), stage01, HudTone.Accent);
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
                // Line 1: flight number, airframe, destination code.
                into.Dot(tile.X + 14f, tile.Y + 17f, 8f, tone);
                into.Text(new HudBox(tile.X + 26f, tile.Y + 8f, 90f, 18f), row.FlightLabel, 13f, HudTone.Default,
                    HudTextStyle.Bold);
                var typeX = tile.X + 26f + HudShell.Measure(row.FlightLabel, 13f) + 8f;
                var pillX = tile.Right - 70f;
                if (row.TypeName.Length > 0 && typeX < pillX - 24f)
                    into.Text(new HudBox(typeX, tile.Y + 10f, pillX - typeX - 6f, 14f),
                        row.TypeName, 10f, HudTone.Muted);
                into.Pill(new HudBox(pillX, tile.Y + 8f, 60f, 18f),
                    string.IsNullOrEmpty(row.Route) ? "—" : row.Route.ToUpperInvariant(), HudTone.Route);
                // Line 2: where it is going, and the registration it flies under.
                var line2 = row.RouteText;
                if (row.FlightLabel != row.Registration)
                    line2 = line2.Length == 0 ? row.Registration : line2 + "  ·  " + row.Registration;
                into.Text(new HudBox(tile.X + 14f, tile.Y + 29f, tile.Width - 24f, 16f), line2, 11f, HudTone.Default);
                // Line 3: what it is doing, and the time to watch.
                var timeText = row.TimeText.Length > 0 ? row.TimeText : row.ProgressText;
                var timeWidth = timeText.Length > 0 ? Math.Min(HudShell.Measure(timeText, 10f) + 4f, tile.Width * 0.5f) : 0f;
                into.Text(new HudBox(tile.X + 14f, tile.Y + 47f, tile.Width - 28f - timeWidth, 16f), row.State, 11f,
                    row.IsPriority ? HudTone.Caution : tone == HudTone.Positive ? HudTone.Muted : tone,
                    HudTextStyle.Bold);
                if (timeWidth > 0f)
                    into.Text(new HudBox(tile.Right - 14f - timeWidth, tile.Y + 48f, timeWidth, 16f), timeText, 10f,
                        HudTone.Muted, HudTextStyle.Regular, HudAlign.Right);
                // Line 4: progress through the turnaround, the flight leg or the check.
                if (row.HasProgress)
                {
                    var bar = new HudBox(tile.X + 14f, tile.Bottom - 10f, tile.Width - 28f, 3f);
                    into.Bar(bar, row.Progress01,
                        row.Progress01 >= 1f ? HudTone.Positive : tone == HudTone.Positive ? HudTone.Accent : tone);
                }
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
            into.Text(titleBox, TitleCase(title), 26f, HudTone.Default, HudTextStyle.Regular);
            into.Text(subtitleBox, subtitle, HudShell.FitFontSize(subtitle, 12f, subtitleBox.Width, 10f), HudTone.Muted);
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
    /// <summary>A departure-style notice: readable severity, wrapped message and optional lifetime/repeat indicators.</summary>
    public static class ToastPainter
    {
        public static void Paint(HudDrawList into, HudBox box, string text, HudTone tone, float alpha,
            float remaining01 = -1f, int repeats = 1)
        {
            if (into == null || box.IsEmpty || string.IsNullOrEmpty(text) || alpha <= 0.01f)
                return;
            alpha = Math.Clamp(alpha, 0f, 1f);
            into.Fill(box, HudTone.Default, 0.96f * alpha, AirsidePalette.GlassHex);
            into.Outline(box, HudTone.Muted, 0.22f * alpha);
            into.Hairline(new HudBox(box.X, box.Y + 10f, 2f, box.Height - 20f), tone, alpha);
            var centreY = box.Y + box.Height * 0.5f;
            into.Fill(new HudBox(box.X + 12f, centreY - 12f, 24f, 24f), tone, 0.14f * alpha);
            into.Text(new HudBox(box.X + 12f, centreY - 10f, 24f, 20f),
                tone == HudTone.Positive ? "+" : tone is HudTone.Caution or HudTone.Negative ? "!" : "i",
                14f, tone, HudTextStyle.Bold, HudAlign.Center, alpha: alpha);
            var x = box.X + 46f;
            var width = box.Width - 60f;
            var expanded = box.Height >= 60f;
            if (expanded)
            {
                var label = tone switch
                {
                    HudTone.Positive => "SUCCESS",
                    HudTone.Caution => "ATTENTION",
                    HudTone.Negative => "ACTION NEEDED",
                    HudTone.Accent => "AIRPORT UPDATE",
                    _ => "NOTICE"
                };
                into.Text(new HudBox(x, box.Y + 9f, Math.Max(0f, width - (repeats > 1 ? 46f : 0f)), 12f),
                    label, 9f, tone, HudTextStyle.Bold | HudTextStyle.Caption, alpha: alpha);
            }
            if (repeats > 1)
            {
                into.Text(new HudBox(box.Right - 58f, box.Y + 8f, 44f, 14f), "×" + repeats, 10f,
                    HudTone.Muted, HudTextStyle.Bold, HudAlign.Right, alpha: alpha);
                if (!expanded) width -= 46f;
            }
            into.Text(new HudBox(x, expanded ? box.Y + 25f : centreY - 15f, Math.Max(0f, width), 30f),
                text, HudShell.FitFontSize(text, 12f, Math.Max(1f, width) * 2f, 10f),
                HudTone.Default, HudTextStyle.Wrap, alpha: alpha);
            if (remaining01 >= 0f)
            {
                var track = new HudBox(x, box.Bottom - 6f, Math.Max(0f, width), 1f);
                into.Hairline(track, HudTone.Muted, 0.2f * alpha);
                var remaining = Math.Clamp(remaining01, 0f, 1f);
                if (remaining > 0f)
                    into.Hairline(new HudBox(track.X, track.Y, track.Width * remaining, 1f), tone, 0.55f * alpha);
            }
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
            into.Surface(box, 0.97f);
            into.Dot(box.X + 16f, box.Y + 12f, 6f, HudTone.Accent);
            into.Caption(new HudBox(box.X + 26f, box.Y + 6f, box.Width - 36f, 12f), header, HudTone.Muted,
                HudAlign.Left, 9f);
            into.Card(Inner(box), 0.9f);
        }
    }
}
