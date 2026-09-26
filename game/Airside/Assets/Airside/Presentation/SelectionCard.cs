using System.Collections.Generic;

namespace Airside.Presentation
{
    /// <summary>One turnaround stage on the selected-aircraft card.</summary>
    public readonly struct SelectionPrepStage
    {
        public SelectionPrepStage(string name, float progress01, bool active)
        {
            Name = name ?? string.Empty;
            Progress01 = progress01 < 0f ? 0f : progress01 > 1f ? 1f : progress01;
            Active = active;
        }

        public string Name { get; }
        public float Progress01 { get; }
        public bool Active { get; }
        public bool Done => Progress01 >= 1f;
    }

    /// <summary>One stand the landed aircraft can be sent to.</summary>
    public readonly struct SelectionStandChoice
    {
        public SelectionStandChoice(string standId, string label, bool best)
        {
            StandId = standId ?? string.Empty;
            Label = label ?? string.Empty;
            Best = best;
        }

        public string StandId { get; }
        public string Label { get; }
        public bool Best { get; }
    }

    /// <summary>Everything the selected-aircraft card shows, already worded by the runtime.</summary>
    public sealed class SelectionCardData
    {
        public string Registration = string.Empty;
        public string TypeName = string.Empty;
        public string LiveryHex;
        public string RouteLine = string.Empty;
        public string LiveLine = string.Empty;
        public string PhaseLabel = string.Empty;
        public HudTone PhaseTone = HudTone.Accent;
        public bool IsPlayer;
        public bool AwaitingStand;
        public string PrimaryLabel = string.Empty;
        public bool CanCancel;
        public bool GuidePrimary;
        public bool GuideBestStand;
        public readonly List<SelectionPrepStage> Prep = new();
        public readonly List<SelectionStandChoice> Stands = new();
    }

    /// <summary>
    /// The bottom-centre selected-aircraft card (ADR 0122): identity with a livery tick and a phase
    /// chip, route and live readout, a four-node turnaround timeline, then one amber action — or
    /// the stand choices after landing. Pure painter; the runtime reacts to the returned action ids.
    /// </summary>
    public static class SelectionCardPainter
    {
        public const string GuideHighlight = "guide";
        public const float StandRowHeight = 34f;

        /// <summary>Preferred card height for this state.</summary>
        public static float HeightFor(SelectionCardData data)
        {
            if (data == null) return 0f;
            if (data.AwaitingStand && data.IsPlayer)
                return 96f + ((System.Math.Max(1, data.Stands.Count) + 1) / 2) * StandRowHeight + 14f;
            if (data.Prep.Count > 0)
                return 186f;
            return data.IsPlayer ? 142f : 92f;
        }

        public static void Paint(HudDrawList into, HudBox box, SelectionCardData data)
        {
            if (into == null || data == null || box.IsEmpty)
                return;
            into.Surface(box, 0.9f);
            var x = box.X + 20f;
            var inner = box.Width - 40f;
            into.Fill(new HudBox(box.X + 8f, box.Y + 16f, 4f, 30f), HudTone.Default, 1f, data.LiveryHex);
            into.Text(new HudBox(x, box.Y + 12f, inner - 120f, 22f), data.Registration, 18f, HudTone.Default,
                HudTextStyle.Bold);
            into.Text(new HudBox(x + HudShell.Measure(data.Registration, 18f) + 6f, box.Y + 17f,
                inner - 200f, 18f), data.TypeName, 12f, HudTone.Muted);
            if (!string.IsNullOrEmpty(data.PhaseLabel))
                into.Pill(new HudBox(box.Right - 20f - 108f, box.Y + 14f, 108f, 20f), data.PhaseLabel.ToUpperInvariant(),
                    data.PhaseTone, fontSize: 9f);
            into.Text(new HudBox(x, box.Y + 40f, inner, 16f), data.RouteLine, 12f, HudTone.Default);
            into.Text(new HudBox(x, box.Y + 58f, inner, 16f), data.LiveLine, 11f, HudTone.Muted);

            if (data.Prep.Count > 0)
                PaintPrep(into, new HudBox(x, box.Y + 86f, inner, 44f), data.Prep);

            if (!data.IsPlayer)
                return;

            if (data.AwaitingStand)
            {
                PaintStands(into, new HudBox(x, box.Y + 84f, inner, box.Bottom - box.Y - 96f), data);
                return;
            }

            var cancelWidth = data.CanCancel ? 110f : 0f;
            var primary = new HudBox(x, box.Bottom - 50f, inner - cancelWidth - (data.CanCancel ? 10f : 0f), 36f);
            if (data.GuidePrimary)
                into.Outline(new HudBox(primary.X - 4f, primary.Y - 4f, primary.Width + 8f, primary.Height + 8f),
                    HudTone.Caution, 1f);
            into.Button(primary, data.PrimaryLabel.ToUpperInvariant(), HudAction.Primary, HudButtonStyle.Primary);
            if (data.CanCancel)
                into.Button(new HudBox(primary.Right + 10f, primary.Y, cancelWidth, 36f), "CANCEL",
                    HudAction.Cancel, HudButtonStyle.Destructive);
        }

        private static void PaintPrep(HudDrawList into, HudBox area, IReadOnlyList<SelectionPrepStage> prep)
        {
            var step = area.Width / prep.Count;
            var y = area.Y + 8f;
            for (var i = 0; i < prep.Count - 1; i++)
            {
                var done = prep[i].Done;
                into.Fill(new HudBox(area.X + step * (i + 0.5f), y - 1.5f, step, 3f),
                    done ? HudTone.Positive : HudTone.Muted, done ? 0.9f : 0.25f);
            }
            for (var i = 0; i < prep.Count; i++)
            {
                var stage = prep[i];
                var cx = area.X + step * (i + 0.5f);
                if (stage.Done)
                    into.Dot(cx, y, 14f, HudTone.Positive);
                else if (stage.Active)
                {
                    into.Ring(cx, y, 20f, stage.Progress01, HudTone.Caution, 3f);
                    into.Dot(cx, y, 8f, HudTone.Caution);
                }
                else
                    into.Dot(cx, y, 10f, HudTone.Muted);
                var label = stage.Active ? $"{stage.Name} {(int)(stage.Progress01 * 100f)}%" : stage.Name;
                into.Text(new HudBox(cx - step * 0.5f, y + 14f, step, 14f), label, 10f,
                    stage.Done ? HudTone.Positive : stage.Active ? HudTone.Caution : HudTone.Muted,
                    stage.Active ? HudTextStyle.Bold : HudTextStyle.Regular, HudAlign.Center);
            }
        }

        private static void PaintStands(HudDrawList into, HudBox area, SelectionCardData data)
        {
            into.Caption(area.WithHeight(12f), "CHOOSE A STAND", HudTone.Caution, HudAlign.Left, 9f);
            var y = area.Y + 18f;
            var gap = 8f;
            var width = (area.Width - gap) * 0.5f;
            if (data.Stands.Count == 0)
            {
                into.Pill(new HudBox(area.X, y, area.Width, 28f), "ALL STANDS FULL — WAIT FOR ONE TO CLEAR",
                    HudTone.Negative, fontSize: 9f);
                return;
            }
            for (var i = 0; i < data.Stands.Count; i++)
            {
                var stand = data.Stands[i];
                var cell = new HudBox(area.X + (i % 2) * (width + gap), y + (i / 2) * StandRowHeight, width, 28f);
                if (stand.Best && data.GuideBestStand)
                    into.Outline(new HudBox(cell.X - 3f, cell.Y - 3f, cell.Width + 6f, cell.Height + 6f), HudTone.Caution, 1f);
                into.Button(cell, stand.Best ? "BEST · " + stand.Label : stand.Label, HudAction.Stand(stand.StandId),
                    stand.Best ? HudButtonStyle.Primary : HudButtonStyle.Secondary);
            }
        }
    }
}
