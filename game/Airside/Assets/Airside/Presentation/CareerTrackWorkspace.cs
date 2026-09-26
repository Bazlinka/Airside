using System;
using System.Collections.Generic;
using Airside.Domain;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>Where the airline stands on one node of the career track.</summary>
    public enum CareerTrackNodeState
    {
        Done,
        Current,
        Ahead
    }

    /// <summary>One node on the tier track: Provisional … International, then Established.</summary>
    public readonly struct CareerTrackNode
    {
        public CareerTrackNode(string label, CareerTrackNodeState state)
        {
            Label = label ?? string.Empty;
            State = state;
        }

        public string Label { get; }
        public CareerTrackNodeState State { get; }
    }

    /// <summary>
    /// The Career track (ADR 0121/0122): one horizontal tier track, the current stage's goals as
    /// cards that say what finishing them earns, and a quiet preview of the next stage. Read-only
    /// projection of <see cref="CareerRoadmap"/> — the single progression authority.
    /// </summary>
    public sealed class CareerTrackModel
    {
        private readonly List<CareerTrackNode> _nodes = new();
        private readonly List<CareerGoalStatus> _current = new();
        private readonly List<CareerGoalStatus> _next = new();

        public string Title => "Career";
        public string Subtitle { get; private set; } = string.Empty;
        public IReadOnlyList<CareerTrackNode> Nodes => _nodes;
        public IReadOnlyList<CareerGoalStatus> CurrentGoals => _current;
        public IReadOnlyList<CareerGoalStatus> NextGoals => _next;
        public string CurrentCaption { get; private set; } = string.Empty;
        public string NextCaption { get; private set; } = string.Empty;
        public string PinnedGoalId { get; private set; } = string.Empty;
        public string FooterLine { get; private set; } = string.Empty;
        public bool FinaleReached { get; private set; }

        public void Rebuild(AirlineOperations operations)
        {
            _nodes.Clear();
            _current.Clear();
            _next.Clear();
            var career = operations?.CareerState;
            if (career == null)
                return;

            FinaleReached = career.FinaleReached;
            PinnedGoalId = operations.PinnedCareerGoal().Id ?? string.Empty;
            foreach (OperatingTier tier in Enum.GetValues(typeof(OperatingTier)))
                _nodes.Add(new CareerTrackNode(tier.ToString(),
                    tier < career.Tier || (FinaleReached && tier == career.Tier) ? CareerTrackNodeState.Done
                    : tier == career.Tier ? CareerTrackNodeState.Current : CareerTrackNodeState.Ahead));
            _nodes.Add(new CareerTrackNode("Established",
                FinaleReached ? CareerTrackNodeState.Done : CareerTrackNodeState.Ahead));

            foreach (var goal in operations.CareerGoals())
            {
                if (goal.Stage == career.Tier)
                    _current.Add(goal);
                else if (career.Tier < OperatingTier.International && goal.Stage == career.Tier + 1)
                    _next.Add(goal);
            }

            var stage = operations.CareerStage();
            CurrentCaption = FinaleReached
                ? "CAREER COMPLETE · SANDBOX"
                : $"TOWARD {stage.TargetLabel.ToUpperInvariant()} · {stage.Done} OF {stage.Total} DONE";
            NextCaption = _next.Count == 0 ? string.Empty
                : $"THEN, TOWARD {(career.Tier + 1 == OperatingTier.International ? "ESTABLISHED AIRLINE" : (career.Tier + 2).ToString().ToUpperInvariant())}";
            Subtitle = FinaleReached
                ? "Established airline — every career goal met. Keep flying."
                : $"{career.Tier} operator · finish every step below to reach {stage.TargetLabel}. Pin one to lead the HUD.";
            FooterLine = $"{career.BaseCount} base{(career.BaseCount == 1 ? "" : "s")} · {operations.PlayerFleetCount()} aircraft · "
                         + $"{career.ServedDestinations.Count} destinations · {career.CompletedPlayerRotations} services · "
                         + $"{career.ActivePlaySeconds / 3600} h played";
        }
    }

    public readonly struct CareerTrackLayout
    {
        public const float TrackHeight = 92f;
        public const float CardHeight = 76f;
        public const float CardGap = 12f;
        public const float PreviewRowHeight = 22f;

        private CareerTrackLayout(HudBox surface, HudBox track, HudBox goals, HudBox preview, HudBox footer, int columns)
        {
            Surface = surface;
            Track = track;
            Goals = goals;
            Preview = preview;
            Footer = footer;
            Columns = columns;
        }

        public HudBox Surface { get; }
        public HudBox Track { get; }
        public HudBox Goals { get; }
        public HudBox Preview { get; }
        public HudBox Footer { get; }
        public int Columns { get; }

        public HudBox TitleBox => new(Surface.X + HudShell.SurfacePadding + 12f, Surface.Y + 14f, Surface.Width * 0.5f, 30f);
        public HudBox SubtitleBox => new(Surface.X + HudShell.SurfacePadding, Surface.Y + 44f,
            Surface.Width - HudShell.SurfacePadding * 2f - 160f, 16f);

        public static CareerTrackLayout Create(HudBox surface, int currentGoals)
        {
            var body = HudShell.Body(surface, hasFooter: true);
            var track = body.WithHeight(TrackHeight);
            var columns = body.Width >= 620f ? 2 : 1;
            var rows = (Math.Max(1, currentGoals) + columns - 1) / columns;
            var goalsTop = track.Bottom + 26f;
            var goalsHeight = Math.Min(rows * (CardHeight + CardGap), Math.Max(0f, body.Bottom - goalsTop));
            var goals = new HudBox(body.X, goalsTop, body.Width, goalsHeight);
            var previewTop = goals.Bottom + 18f;
            var preview = new HudBox(body.X, previewTop, body.Width, Math.Max(0f, body.Bottom - previewTop));
            var footer = HudShell.Footer(surface).Inset(HudShell.SurfacePadding, 12f, HudShell.SurfacePadding, 0f)
                .WithHeight(18f);
            return new CareerTrackLayout(surface, track, goals, preview, footer, columns);
        }

        public HudBox Card(int index)
        {
            var width = (Goals.Width - CardGap * (Columns - 1)) / Columns;
            var column = index % Columns;
            var row = index / Columns;
            return new HudBox(Goals.X + column * (width + CardGap), Goals.Y + row * (CardHeight + CardGap),
                width, CardHeight);
        }
    }

    public static class CareerTrackPainter
    {
        public static void Paint(HudDrawList into, CareerTrackModel model, CareerTrackLayout layout)
        {
            if (into == null || model == null)
                return;
            into.Clear();
            into.Surface(layout.Surface);
            HudShellPainter.PaintSheetHeader(into, layout.Surface, model.Title, model.Subtitle,
                layout.TitleBox, layout.SubtitleBox);
            PaintTrack(into, model, layout.Track);
            PaintGoals(into, model, layout);
            PaintPreview(into, model, layout.Preview);
            into.Hairline(HudShell.FooterRule(layout.Surface), HudTone.Muted, 0.16f);
            into.Text(layout.Footer, model.FooterLine, 11f, HudTone.Muted);
        }

        private static void PaintTrack(HudDrawList into, CareerTrackModel model, HudBox track)
        {
            if (model.Nodes.Count == 0 || track.IsEmpty)
                return;
            into.Card(track, 0.7f);
            var inset = 70f;
            var y = track.Y + 34f;
            var step = (track.Width - inset * 2f) / Math.Max(1, model.Nodes.Count - 1);
            for (var i = 0; i < model.Nodes.Count - 1; i++)
            {
                var done = model.Nodes[i + 1].State != CareerTrackNodeState.Ahead;
                into.Fill(new HudBox(track.X + inset + i * step, y - 2f, step, 4f),
                    done ? HudTone.Accent : HudTone.Muted, done ? 0.9f : 0.25f);
            }
            for (var i = 0; i < model.Nodes.Count; i++)
            {
                var node = model.Nodes[i];
                var cx = track.X + inset + i * step;
                switch (node.State)
                {
                    case CareerTrackNodeState.Done:
                        into.Dot(cx, y, 20f, HudTone.Accent);
                        into.Text(new HudBox(cx - 10f, y - 8f, 20f, 16f), "✓", 12f, HudTone.Default,
                            HudTextStyle.Bold, HudAlign.Center, AirsidePalette.OnAccentHex);
                        break;
                    case CareerTrackNodeState.Current:
                        into.Ring(cx, y, 34f, 1f, HudTone.Caution, 3f);
                        into.Dot(cx, y, 16f, HudTone.Caution);
                        break;
                    default:
                        into.Dot(cx, y, 14f, HudTone.Muted);
                        into.Dot(cx, y, 9f, HudTone.Default, AirsidePalette.GlassRaisedHex);
                        break;
                }
                var labelWidth = Math.Min(step, inset * 2f - 4f);
                into.Text(new HudBox(cx - labelWidth * 0.5f, y + 22f, labelWidth, 16f), node.Label.ToUpperInvariant(), 10f,
                    node.State == CareerTrackNodeState.Current ? HudTone.Caution
                    : node.State == CareerTrackNodeState.Done ? HudTone.Default : HudTone.Muted,
                    HudTextStyle.Bold | HudTextStyle.Caption, HudAlign.Center);
            }
        }

        private static void PaintGoals(HudDrawList into, CareerTrackModel model, CareerTrackLayout layout)
        {
            into.Caption(new HudBox(layout.Goals.X, layout.Goals.Y - 20f, layout.Goals.Width, 14f),
                model.CurrentCaption, model.FinaleReached ? HudTone.Positive : HudTone.Accent);
            for (var i = 0; i < model.CurrentGoals.Count; i++)
            {
                var card = layout.Card(i);
                if (card.Bottom > layout.Goals.Bottom + 0.5f)
                    break;
                var goal = model.CurrentGoals[i];
                var pinned = goal.Id == model.PinnedGoalId && !goal.Complete;
                into.Card(card);
                if (pinned)
                    into.Outline(card, HudTone.Caution, 0.9f);
                var tone = goal.Complete ? HudTone.Positive : pinned ? HudTone.Caution : HudTone.Accent;
                into.Dot(card.X + 20f, card.Y + 22f, 12f, goal.Complete ? HudTone.Positive : HudTone.Muted);
                if (goal.Complete)
                    into.Text(new HudBox(card.X + 14f, card.Y + 15f, 12f, 14f), "✓", 10f, HudTone.Default,
                        HudTextStyle.Bold, HudAlign.Center, AirsidePalette.OnAccentHex);
                into.Text(new HudBox(card.X + 36f, card.Y + 12f, card.Width - 130f, 20f), goal.Title, 14f,
                    goal.Complete ? HudTone.Muted : HudTone.Default, HudTextStyle.Bold);
                into.Bar(new HudBox(card.X + 36f, card.Y + 42f, card.Width - 52f, 5f),
                    goal.Target <= 0 ? 1f : goal.Progress / (float)goal.Target, tone);
                into.Text(new HudBox(card.X + 36f, card.Y + 52f, card.Width - 52f, 16f),
                    goal.Complete ? "Done" : goal.ProgressText, 11f, goal.Complete ? HudTone.Positive : HudTone.Muted);
                var chip = new HudBox(card.Right - 86f, card.Y + 11f, 72f, 22f);
                if (pinned)
                    into.Pill(chip, "PINNED", HudTone.Caution, filled: true, fontSize: 9f);
                else if (!goal.Complete)
                    into.Button(chip, "PIN", HudAction.PinGoal(goal.Id), HudButtonStyle.Secondary);
            }
        }

        private static void PaintPreview(HudDrawList into, CareerTrackModel model, HudBox preview)
        {
            if (model.NextGoals.Count == 0 || preview.Height < 40f)
                return;
            into.Caption(preview.WithHeight(14f), model.NextCaption, HudTone.Muted);
            var y = preview.Y + 22f;
            var columns = preview.Width >= 620f ? 2 : 1;
            var width = preview.Width / columns;
            for (var i = 0; i < model.NextGoals.Count; i++)
            {
                var column = i % columns;
                if (column == 0 && i > 0)
                    y += CareerTrackLayout.PreviewRowHeight;
                if (y + CareerTrackLayout.PreviewRowHeight > preview.Bottom)
                    break;
                var goal = model.NextGoals[i];
                var x = preview.X + column * width;
                into.Dot(x + 5f, y + 8f, 5f, HudTone.Muted);
                into.Text(new HudBox(x + 16f, y, width - 24f, 18f),
                    goal.Title + (goal.Complete ? "  ·  already done" : "  ·  " + goal.ProgressText), 12f,
                    goal.Complete ? HudTone.Positive : HudTone.Muted);
            }
        }
    }
}
