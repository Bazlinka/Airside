using System;
using System.Collections.Generic;
using System.Linq;
using Airside.Domain;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>One career achievement row, reached or not.</summary>
    public readonly struct MilestoneRow
    {
        public MilestoneRow(string title, bool reached)
        {
            Title = title;
            Reached = reached;
        }

        public string Title { get; }
        public bool Reached { get; }
    }

    /// <summary>One fulfilled contract, most recent first.</summary>
    public readonly struct ContractHistoryRow
    {
        public ContractHistoryRow(string routeText, string paidText)
        {
            RouteText = routeText ?? string.Empty;
            PaidText = paidText ?? string.Empty;
        }

        public string RouteText { get; }
        public string PaidText { get; }
    }

    /// <summary>
    /// The Stats/career workspace (ADR 0066): a real answer to "how am I actually doing" —
    /// funds, lifetime revenue, reliability, tier and exactly what the next one still needs,
    /// fleet size, milestones and recent contract history. Everything here already exists
    /// elsewhere (Funds/Reliability on the objective card fallback line, Tier gating buys);
    /// this is the first place they are all shown together, accurately, without digging.
    /// </summary>
    public sealed class StatsWorkspaceModel
    {
        private readonly List<MilestoneRow> _milestones = new();
        private readonly List<ContractHistoryRow> _history = new();

        /// <summary>
        /// The livery choices offered at airline creation (ADR 0045), reused here so a player
        /// who wants to repaint later picks from the same authored set rather than a free
        /// colour picker — one shared source of truth with <c>AirsidePrototype.LiveryChoices</c>.
        /// </summary>
        public static readonly (string Label, string Hex)[] LiveryPalette =
        {
            ("Crimson", "#C8102E"), ("Navy", "#1F3A93"), ("Forest", "#2E7D32"),
            ("Sunset", "#E8772E"), ("Violet", "#6A3FA0"), ("Gold", "#D4A017")
        };

        public string Title => "CAREER";

        public string AirlineName { get; private set; } = string.Empty;
        public string CurrentLiveryHex { get; private set; } = string.Empty;
        public string FundsLine { get; private set; } = string.Empty;
        public string LifetimeRevenueLine { get; private set; } = string.Empty;
        public string ReliabilityLine { get; private set; } = string.Empty;
        public string TierLine { get; private set; } = string.Empty;
        public string FleetLine { get; private set; } = string.Empty;

        /// <summary>False only at International — there is nothing further to work toward.</summary>
        public bool HasNextTier { get; private set; }
        public string NextTierTitle { get; private set; } = string.Empty;
        public string NextTierRequirementLine { get; private set; } = string.Empty;
        public float NextTierProgress01 { get; private set; }

        public IReadOnlyList<MilestoneRow> Milestones => _milestones;
        public IReadOnlyList<ContractHistoryRow> ContractHistory => _history;
        public string EmptyHistoryLine => "No contracts fulfilled yet — accept one from Contracts.";

        /// <summary>
        /// Lifetime count, not the length of <see cref="ContractHistory"/> — that list is
        /// capped at <see cref="MaxHistoryShown"/> recent entries, so a long career needs its
        /// own true total (<c>AirlineCareerState.CompletedContractIds</c>, which never drops
        /// an id) to answer "how many have I actually fulfilled" once it outgrows the list.
        /// </summary>
        public string ContractsFulfilledLine { get; private set; } = string.Empty;

        public string MilestonesReachedLine { get; private set; } = string.Empty;

        public const int MaxHistoryShown = 5;

        public void Rebuild(AirlineOperations operations, SimulationTime now)
        {
            _milestones.Clear();
            _history.Clear();
            AirlineName = string.Empty;
            CurrentLiveryHex = string.Empty;
            FundsLine = string.Empty;
            LifetimeRevenueLine = string.Empty;
            ReliabilityLine = string.Empty;
            TierLine = string.Empty;
            FleetLine = string.Empty;
            HasNextTier = false;
            NextTierTitle = string.Empty;
            NextTierRequirementLine = string.Empty;
            NextTierProgress01 = 0f;
            ContractsFulfilledLine = string.Empty;
            MilestonesReachedLine = string.Empty;
            if (operations?.PlayerAirline == null)
                return;

            var career = operations.CareerState;
            var ownedTypes = operations.Fleet
                .Where(a => a.Airline.IsPlayer)
                .Select(a => a.Type)
                .ToList();
            var fleetSize = ownedTypes.Count;

            AirlineName = operations.PlayerAirline.Name;
            CurrentLiveryHex = operations.PlayerAirline.LiveryHex;
            FundsLine = $"${career.Funds:N0} on hand";
            LifetimeRevenueLine = $"${career.LifetimeRevenue:N0} lifetime revenue";
            ReliabilityLine = $"{career.Reliability}% reliability";
            TierLine = $"{career.Tier} tier";
            FleetLine = $"{fleetSize} of {AircraftAcquisition.MaxPlayerAircraft} aircraft";

            FillNextTier(career, ownedTypes);

            var milestones = CareerMilestones.Reached(career, fleetSize, ownedTypes);
            foreach (var milestone in milestones)
                _milestones.Add(new MilestoneRow(milestone.Title, milestone.Reached));
            var reachedCount = milestones.Count(m => m.Reached);
            MilestonesReachedLine = $"{reachedCount} of {milestones.Count} milestones reached";

            foreach (var record in career.ContractHistory.Take(MaxHistoryShown))
                _history.Add(new ContractHistoryRow(
                    $"{OperationsSummary.PlaceName(record.OriginCode)} → {OperationsSummary.PlaceName(record.DestinationCode)}",
                    $"${record.TotalPaid:N0}"));
            // Lifetime, not the capped list above — the real answer once a long career has
            // fulfilled more contracts than ContractHistory keeps.
            ContractsFulfilledLine = $"{career.CompletedContractIds.Count} contracts fulfilled all-time";
        }

        private void FillNextTier(AirlineCareerState career, IReadOnlyList<AircraftType> ownedTypes)
        {
            var next = CareerProgress.NextTier(career, ownedTypes);
            if (next.IsMaxTier)
            {
                NextTierTitle = "International reached";
                NextTierRequirementLine = "No further tier to unlock.";
                NextTierProgress01 = 1f;
                return;
            }

            HasNextTier = true;
            NextTierTitle = $"Next: {next.Tier}";

            var required = next.Tier switch
            {
                OperatingTier.Regional => AirlineCareerState.RegionalRotations,
                OperatingTier.Domestic => AirlineCareerState.DomesticRotations,
                OperatingTier.International => AirlineCareerState.InternationalRotations,
                _ => 1
            };
            var done = required - next.RotationsRemaining;
            NextTierProgress01 = required <= 0 ? 1f : Clamp01(done / (float)required);

            var needs = new List<string>();
            if (next.RotationsRemaining > 0)
                needs.Add($"{next.RotationsRemaining} more rotation" + (next.RotationsRemaining == 1 ? "" : "s"));
            if (next.ReliabilityRemaining > 0)
                needs.Add($"{next.ReliabilityRemaining} more reliability");
            if (!string.IsNullOrEmpty(next.MissingAircraftLine))
                needs.Add(next.MissingAircraftLine);
            NextTierRequirementLine = needs.Count == 0
                ? "Requirements met — clears on the next settlement."
                : "Needs " + string.Join(", ", needs);
        }

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    }

    public readonly struct StatsWorkspaceLayout
    {
        public const float ColumnGap = 28f;
        public const float CaptionHeight = 20f;
        public const float StatRowHeight = 26f;
        public const float MilestoneRowHeight = 22f;
        public const float HistoryRowHeight = 22f;
        public const float MinColumnWidth = 300f;

        private StatsWorkspaceLayout(HudBox surface, HudBox header, HudBox leftColumn, HudBox rightColumn,
            HudBox divider, HudBox footer)
        {
            Surface = surface;
            Header = header;
            LeftColumn = leftColumn;
            RightColumn = rightColumn;
            Divider = divider;
            Footer = footer;
        }

        public HudBox Surface { get; }
        public HudBox Header { get; }
        public HudBox LeftColumn { get; }
        public HudBox RightColumn { get; }
        public HudBox Divider { get; }
        public HudBox Footer { get; }

        public HudBox TitleBox => new(Header.X + HudShell.SurfacePadding, Header.Y + 16f, 220f, 30f);

        public const float RenameFieldWidth = 150f;
        public const float RenameButtonWidth = 64f;
        private const float RenameGap = 6f;

        /// <summary>
        /// Right-aligned before the CLOSE button (ADR 0068) — raw Unity `GUI.TextField` drawn
        /// directly by the caller, not through the draw list: no primitive in the shared,
        /// UnityEngine-free `HudDrawList` renders editable text, and adding one just for this
        /// single control was a bigger, riskier change than drawing it as one exception.
        /// </summary>
        public HudBox RenameFieldBox => new(RenameButtonBox.X - RenameGap - RenameFieldWidth, Header.Y + 18f,
            RenameFieldWidth, 26f);

        public HudBox RenameButtonBox => new(
            OperationsWorkspacePainter.CloseBox(Surface).X - RenameGap - RenameButtonWidth,
            Header.Y + 18f, RenameButtonWidth, 26f);

        public HudBox OverviewCaption => LeftColumn.WithHeight(CaptionHeight);

        public HudBox StatRow(int index) =>
            new(LeftColumn.X, LeftColumn.Y + CaptionHeight + 8f + index * StatRowHeight, LeftColumn.Width, StatRowHeight);

        /// <summary>Where the "next tier" block starts, below the five overview stat rows.</summary>
        public float NextTierY => LeftColumn.Y + CaptionHeight + 8f + 5 * StatRowHeight + 16f;

        public HudBox NextTierCaption => new(LeftColumn.X, NextTierY, LeftColumn.Width, CaptionHeight);
        public HudBox NextTierTitleBox => new(LeftColumn.X, NextTierY + CaptionHeight + 6f, LeftColumn.Width, 20f);
        public HudBox NextTierBar => new(LeftColumn.X, NextTierY + CaptionHeight + 30f, LeftColumn.Width - 46f, 10f);
        public HudBox NextTierPercent =>
            new(LeftColumn.Right - 40f, NextTierY + CaptionHeight + 26f, 40f, 18f);
        public HudBox NextTierRequirementBox =>
            new(LeftColumn.X, NextTierY + CaptionHeight + 48f, LeftColumn.Width, 36f);

        public const float SwatchSize = 28f;
        public const float SwatchGap = 8f;

        /// <summary>Where the livery-repaint swatches start, below the next-tier requirement text.</summary>
        public float ProfileY => NextTierY + CaptionHeight + 48f + 36f + 16f;

        public HudBox ProfileCaption => new(LeftColumn.X, ProfileY, LeftColumn.Width, CaptionHeight);

        public HudBox LiverySwatch(int index) =>
            new(LeftColumn.X + index * (SwatchSize + SwatchGap), ProfileY + CaptionHeight + 8f,
                SwatchSize, SwatchSize);

        public HudBox MilestonesCaption => RightColumn.WithHeight(CaptionHeight);

        public const float SummaryRowHeight = 20f;

        /// <summary>"N of M milestones reached", right under the caption.</summary>
        public HudBox MilestonesSummaryBox =>
            new(RightColumn.X, RightColumn.Y + CaptionHeight + 2f, RightColumn.Width, SummaryRowHeight);

        /// <summary>Where the milestone rows start, below the caption and its summary line.</summary>
        private float MilestonesListY => RightColumn.Y + CaptionHeight + SummaryRowHeight + 8f;

        public HudBox MilestoneRow(int index) =>
            new(RightColumn.X, MilestonesListY + index * MilestoneRowHeight, RightColumn.Width, MilestoneRowHeight);

        /// <summary>
        /// How many milestone rows fit before the history block and the lifetime-fulfilled
        /// line below it need room. Mirrors the exact gaps <see cref="HistoryY"/> and
        /// <see cref="ContractsFulfilledBox"/> use, sized for the worst case (a full history
        /// list) so this never reserves less room than those could actually need.
        /// </summary>
        public int VisibleMilestones(int totalMilestones)
        {
            var beforeList = CaptionHeight + SummaryRowHeight + 8f;
            const float historyGap = 12f;
            const float rowsGap = 6f;
            const float fulfilledGap = 10f;
            const float fulfilledHeight = 18f;
            var afterList = historyGap + CaptionHeight + rowsGap + MaxHistoryRows * HistoryRowHeight
                             + fulfilledGap + fulfilledHeight;
            var available = RightColumn.Height - beforeList - afterList;
            var fit = available <= 0f ? 0 : (int)(available / MilestoneRowHeight);
            return fit < 0 ? 0 : fit > totalMilestones ? totalMilestones : fit;
        }

        public const int MaxHistoryRows = StatsWorkspaceModel.MaxHistoryShown;

        private float HistoryY(int shownMilestones) =>
            MilestonesListY + shownMilestones * MilestoneRowHeight + 12f;

        public HudBox HistoryCaption(int shownMilestones) =>
            new(RightColumn.X, HistoryY(shownMilestones), RightColumn.Width, CaptionHeight);

        public HudBox HistoryRow(int shownMilestones, int index) =>
            new(RightColumn.X, HistoryY(shownMilestones) + CaptionHeight + 6f + index * HistoryRowHeight,
                RightColumn.Width, HistoryRowHeight);

        /// <summary>
        /// The lifetime "N contracts fulfilled all-time" line, below whatever the history
        /// block actually shows (rows, or its one-line empty state) — the real content that
        /// used to leave the rest of this column blank once Milestones and Recent Contracts
        /// ran out of things to say (ADR 0068).
        /// </summary>
        public HudBox ContractsFulfilledBox(int shownMilestones, int shownHistory)
        {
            var rows = Math.Max(shownHistory, 1); // the empty-state line takes one row's worth of height
            var y = HistoryY(shownMilestones) + CaptionHeight + 6f + rows * HistoryRowHeight + 10f;
            return new HudBox(RightColumn.X, y, RightColumn.Width, 18f);
        }

        public static StatsWorkspaceLayout Create(HudBox surface)
        {
            var header = HudShell.Header(surface);
            var footer = HudShell.Footer(surface);
            var body = HudShell.Body(surface, hasFooter: true);

            var columnWidth = (body.Width - ColumnGap) * 0.46f;
            if (columnWidth < MinColumnWidth)
                columnWidth = MinColumnWidth;
            if (columnWidth > body.Width - ColumnGap - MinColumnWidth)
                columnWidth = body.Width - ColumnGap - MinColumnWidth;
            if (columnWidth < 0f || body.Width < MinColumnWidth * 2f + ColumnGap)
            {
                var top = body.Height * 0.55f;
                var stackedLeft = new HudBox(body.X, body.Y, body.Width, top);
                var stackedRight = new HudBox(body.X, body.Y + top + 12f, body.Width, body.Height - top - 12f);
                return new StatsWorkspaceLayout(surface, header, stackedLeft, stackedRight, HudBox.Empty, footer);
            }

            var left = new HudBox(body.X, body.Y, columnWidth, body.Height);
            var right = new HudBox(body.X + columnWidth + ColumnGap, body.Y,
                body.Width - columnWidth - ColumnGap, body.Height);
            var divider = new HudBox(body.X + columnWidth + ColumnGap * 0.5f, body.Y, 1f, body.Height);
            return new StatsWorkspaceLayout(surface, header, left, right, divider, footer);
        }
    }

    /// <summary>Paints the Stats workspace into the shared draw list.</summary>
    public static class StatsWorkspacePainter
    {
        public static void Paint(HudDrawList into, StatsWorkspaceModel model, StatsWorkspaceLayout layout)
        {
            if (into == null || model == null)
                return;

            into.Clear();
            into.Surface(layout.Surface);
            into.Text(layout.TitleBox, model.Title, 26f, HudTone.Default, HudTextStyle.Bold | HudTextStyle.Caption);
            into.Button(OperationsWorkspacePainter.CloseBox(layout.Surface), "CLOSE", HudAction.Close,
                HudButtonStyle.Secondary);
            into.Hairline(HudShell.HeaderRule(layout.Surface));

            PaintOverview(into, model, layout);
            if (!layout.Divider.IsEmpty)
                into.Hairline(layout.Divider);
            PaintMilestonesAndHistory(into, model, layout);

            into.Hairline(HudShell.FooterRule(layout.Surface));
            into.Text(layout.Footer.Inset(HudShell.SurfacePadding, 10f, HudShell.SurfacePadding, 0f)
                    .WithHeight(16f), model.AirlineName, 11f, HudTone.Muted);
        }

        private static void PaintOverview(HudDrawList into, StatsWorkspaceModel model, StatsWorkspaceLayout layout)
        {
            into.Caption(layout.OverviewCaption, "OVERVIEW");
            var stats = new[]
            {
                model.FundsLine, model.LifetimeRevenueLine, model.ReliabilityLine, model.TierLine, model.FleetLine
            };
            for (var i = 0; i < stats.Length; i++)
                into.Text(layout.StatRow(i).Inset(0f, 2f, 0f, 0f), stats[i], 14f);

            into.Caption(layout.NextTierCaption, "NEXT TIER");
            into.Text(layout.NextTierTitleBox, model.NextTierTitle, 15f, HudTone.Default, HudTextStyle.Bold);
            if (model.HasNextTier)
            {
                into.Bar(layout.NextTierBar, model.NextTierProgress01, HudTone.Accent);
                into.Text(layout.NextTierPercent, $"{(int)(model.NextTierProgress01 * 100f)}%", 12f, HudTone.Muted);
            }

            into.Text(layout.NextTierRequirementBox, model.NextTierRequirementLine, 12f, HudTone.Muted,
                HudTextStyle.Wrap);

            PaintProfile(into, model, layout);
        }

        /// <summary>
        /// Livery repaint (ADR 0067): the same authored swatches offered at airline creation,
        /// clickable here too — a real HUD entry point for AirlineOperations.SetLivery, which
        /// otherwise had no way to be reached once past the setup screen. Rename gets its own
        /// control in the header (ADR 0068) — a raw `GUI.TextField`, drawn by the caller rather
        /// than through this painter, since the shared draw list has no editable-text primitive.
        /// </summary>
        private static void PaintProfile(HudDrawList into, StatsWorkspaceModel model, StatsWorkspaceLayout layout)
        {
            into.Caption(layout.ProfileCaption, "LIVERY");
            for (var i = 0; i < StatsWorkspaceModel.LiveryPalette.Length; i++)
            {
                var (_, hex) = StatsWorkspaceModel.LiveryPalette[i];
                var swatch = layout.LiverySwatch(i);
                var current = string.Equals(hex, model.CurrentLiveryHex, StringComparison.OrdinalIgnoreCase);
                into.Fill(swatch, HudTone.Default, 1f, hex);
                into.Outline(swatch, current ? HudTone.Default : HudTone.Muted, current ? 1f : 0.4f);
                into.Hotspot(swatch, HudAction.Livery(hex));
            }
        }

        private static void PaintMilestonesAndHistory(HudDrawList into, StatsWorkspaceModel model,
            StatsWorkspaceLayout layout)
        {
            into.Caption(layout.MilestonesCaption, "MILESTONES");
            into.Text(layout.MilestonesSummaryBox, model.MilestonesReachedLine, 12f, HudTone.Muted);
            var shown = layout.VisibleMilestones(model.Milestones.Count);
            for (var i = 0; i < shown; i++)
            {
                var milestone = model.Milestones[i];
                var row = layout.MilestoneRow(i);
                into.Text(row.SliceLeft(20f), milestone.Reached ? "✓" : "•",
                    14f, milestone.Reached ? HudTone.Positive : HudTone.Muted, HudTextStyle.Bold);
                into.Text(row.Inset(24f, 2f, 0f, 0f), milestone.Title, 13f,
                    milestone.Reached ? HudTone.Default : HudTone.Muted);
            }

            into.Caption(layout.HistoryCaption(shown), "RECENT CONTRACTS");
            // A cramped stacked layout (a narrow viewport gives this column under half the
            // body's height) can legitimately run out of room for a full history list plus the
            // lifetime line below it — VisibleMilestones' own reservation assumes the common
            // case, not every combination of viewport and data. Same defensive floor check
            // ContractsWorkspacePainter.PaintActive already uses for its terms list: stop
            // drawing rather than run text into or past the footer.
            var floor = layout.RightColumn.Bottom;
            var shownHistory = model.ContractHistory.Count;
            var drawnHistoryRows = 0;
            if (shownHistory == 0)
            {
                var row = layout.HistoryRow(shown, 0);
                if (row.Bottom <= floor)
                {
                    into.Text(row, model.EmptyHistoryLine, 12f, HudTone.Muted, HudTextStyle.Wrap);
                    drawnHistoryRows = 1;
                }
            }
            else
            {
                for (var i = 0; i < shownHistory; i++)
                {
                    var row = layout.HistoryRow(shown, i);
                    if (row.Bottom > floor)
                        break;
                    var record = model.ContractHistory[i];
                    into.Text(row.SliceLeft(row.Width - 80f), record.RouteText, 12f);
                    into.Text(new HudBox(row.Right - 80f, row.Y, 80f, row.Height), record.PaidText, 12f,
                        HudTone.Positive, HudTextStyle.Regular, HudAlign.Right);
                    drawnHistoryRows = i + 1;
                }
            }

            // Lifetime total, below whatever Recent Contracts actually showed — real content
            // filling what used to be blank space once Milestones and history ran out of rows
            // to draw (ADR 0068). Skipped, not squeezed in, if it would not fit either.
            var fulfilledBox = layout.ContractsFulfilledBox(shown, drawnHistoryRows);
            if (fulfilledBox.Bottom <= floor)
                into.Text(fulfilledBox, model.ContractsFulfilledLine, 12f, HudTone.Muted);
        }
    }
}
