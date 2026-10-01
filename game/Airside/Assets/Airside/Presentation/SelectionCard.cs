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

        /// <summary>Why the aircraft is waiting (ADR 0124), or empty.</summary>
        public string HoldLine = string.Empty;

        /// <summary>ADR 0128: what tapping the hold line does (e.g. select the blocker), or empty for no link.</summary>
        public string HoldAction = string.Empty;

        /// <summary>Registration to go back to after following a hold link, or empty.</summary>
        public string BackRegistration = string.Empty;
        public string PhaseLabel = string.Empty;
        public HudTone PhaseTone = HudTone.Accent;
        public bool IsPlayer;
        public bool AwaitingStand;
        public string PrimaryLabel = string.Empty;
        public bool CanCancel;
        public bool GuidePrimary;
        public bool GuideBestStand;

        /// <summary>The camera can follow this aircraft (it is drawn on the field).</summary>
        public bool CanFollow;
        /// <summary>The camera is following it now.</summary>
        public bool Following;
        public bool ShowCameraActions;
        public bool CanCockpit;
        public string CockpitHint = string.Empty;

        /// <summary>Live readout columns (empty when the aircraft is not drawn on the field).</summary>
        public string Speed = string.Empty;
        public string Altitude = string.Empty;
        public string Heading = string.Empty;

        /// <summary>Where it is in its current leg: "Lands 14:32" left, "12 min" right, bar 0..1 (-1 for no bar).</summary>
        public string JourneyLeft = string.Empty;
        public string JourneyRight = string.Empty;
        public float JourneyProgress = -1f;

        public bool HasTelemetry => !string.IsNullOrEmpty(Speed);
        public bool HasJourney => !string.IsNullOrEmpty(JourneyLeft);
        public readonly List<SelectionPrepStage> Prep = new();
        public readonly List<SelectionStandChoice> Stands = new();
    }

    /// <summary>Wording helpers for the selected-aircraft card, kept pure so they are tested.</summary>
    public static class SelectionCardText
    {
        /// <summary>"under a minute", "12 min", "1 h 05 min" for a countdown in seconds.</summary>
        public static string Remaining(double seconds)
        {
            if (seconds < 0) seconds = 0;
            var minutes = (int)System.Math.Ceiling(seconds / 60.0);
            if (minutes <= 0) return "now";
            if (seconds < 60) return "under a minute";
            if (minutes < 60) return minutes + " min";
            return $"{minutes / 60} h {minutes % 60:00} min";
        }

        /// <summary>
        /// Splits the field readout ("142 kt  ·  1,200 ft ▲  ·  HDG 230") into the card's columns.
        /// Missing parts come back empty rather than throwing.
        /// </summary>
        public static void SplitReadout(string readout, out string speed, out string altitude, out string heading)
        {
            speed = altitude = heading = string.Empty;
            if (string.IsNullOrWhiteSpace(readout))
                return;
            foreach (var raw in readout.Split(new[] { "·" }, System.StringSplitOptions.RemoveEmptyEntries))
            {
                var part = raw.Trim();
                if (part.StartsWith("HDG ", System.StringComparison.Ordinal))
                    heading = part.Substring(4).Trim() + "°";
                else if (part.Contains(" ft"))
                    altitude = part;
                else if (part.Contains(" kt"))
                    speed = part;
            }
        }
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

        /// <summary>Vertical positions of each section, shared by sizing and painting so they cannot drift.</summary>
        private struct Layout
        {
            public float HoldY, LiveY, TelemetryY, JourneyY, PrepY, BodyY, Bottom;
        }

        private static Layout Measure(SelectionCardData data)
        {
            var l = new Layout { HoldY = -1f, LiveY = -1f, TelemetryY = -1f, JourneyY = -1f, PrepY = -1f };
            var y = 58f;
            if (!string.IsNullOrEmpty(data.HoldLine))
            {
                l.HoldY = y;
                y += 20f;
            }

            if (data.HasTelemetry)
            {
                l.TelemetryY = y + 2f;
                y += 38f;
            }
            else if (string.IsNullOrEmpty(data.HoldLine))
            {
                l.LiveY = y;
                y += 20f;
            }

            if (data.HasJourney)
            {
                l.JourneyY = y + 2f;
                y += 30f;
            }

            if (data.Prep.Count > 0)
            {
                l.PrepY = y + 2f;
                y += 50f;
            }

            l.BodyY = y + 4f;
            if (!data.IsPlayer)
                l.Bottom = y + 12f;
            else if (data.AwaitingStand)
                l.Bottom = l.BodyY + 18f + ((System.Math.Max(1, data.Stands.Count) + 1) / 2) * StandRowHeight + 14f;
            else
                l.Bottom = l.BodyY + 54f;
            if (data.ShowCameraActions) l.Bottom += 38f;
            return l;
        }

        /// <summary>Preferred card height for this state.</summary>
        public static float HeightFor(SelectionCardData data) => data == null ? 0f : Measure(data).Bottom;

        public static void Paint(HudDrawList into, HudBox box, SelectionCardData data)
        {
            if (into == null || data == null || box.IsEmpty)
                return;
            var layout = Measure(data);
            into.Surface(box, 0.9f);
            if (data.ShowCameraActions)
            {
                into.Button(new HudBox(box.X + 20f, box.Bottom - 34f, box.Width - 40f, 26f),
                    data.CanCockpit ? "COCKPIT" : data.CockpitHint.ToUpperInvariant(), "camera-cockpit",
                    HudButtonStyle.Secondary, data.CanCockpit);
                box = new HudBox(box.X, box.Y, box.Width, box.Height - 38f);
            }
            var x = box.X + 20f;
            var inner = box.Width - 40f;
            into.Fill(new HudBox(box.X + 8f, box.Y + 16f, 4f, 30f), HudTone.Default, 1f, data.LiveryHex);
            into.Text(new HudBox(x, box.Y + 12f, inner - 150f, 22f), data.Registration, 18f, HudTone.Default,
                HudTextStyle.Bold);
            into.Text(new HudBox(x + HudShell.Measure(data.Registration, 18f) + 6f, box.Y + 17f,
                inner - 230f, 18f), data.TypeName, 12f, HudTone.Muted);

            // Close: lets go of the aircraft and, with it, the camera follow.
            into.Button(new HudBox(box.Right - 20f - 22f, box.Y + 13f, 22f, 22f), "×", HudAction.CardClose,
                HudButtonStyle.Secondary);
            var pillRight = box.Right - 20f - 22f - 6f;
            if (!string.IsNullOrEmpty(data.PhaseLabel))
                into.Pill(new HudBox(pillRight - 108f, box.Y + 14f, 108f, 20f), data.PhaseLabel.ToUpperInvariant(),
                    data.PhaseTone, fontSize: 9f);

            var followWidth = data.CanFollow ? 98f : 0f;
            into.Text(new HudBox(x, box.Y + 40f, inner - followWidth - 6f, 16f), data.RouteLine, 12f, HudTone.Default);
            if (data.CanFollow)
                into.Button(new HudBox(box.Right - 20f - 92f, box.Y + 38f, 92f, 20f),
                    data.Following ? "FOLLOWING" : "FOLLOW", HudAction.CardFollow,
                    data.Following ? HudButtonStyle.Primary : HudButtonStyle.Secondary);

            if (layout.HoldY >= 0f)
            {
                var hy = box.Y + layout.HoldY;
                into.Dot(x + 4f, hy + 8f, 7f, HudTone.Caution);
                var linked = !string.IsNullOrEmpty(data.HoldAction);
                into.Text(new HudBox(x + 14f, hy, inner - (linked ? 28f : 14f), 16f), data.HoldLine, 11f,
                    HudTone.Caution, HudTextStyle.Bold);
                if (linked)
                {
                    // ADR 0128: the hold line is a link to what is holding it.
                    into.Text(new HudBox(x + inner - 12f, hy - 1f, 12f, 16f), "›", 14f, HudTone.Caution, HudTextStyle.Bold);
                    into.Hotspot(new HudBox(x, hy - 3f, inner, 22f), data.HoldAction);
                }
            }

            if (layout.LiveY >= 0f)
                into.Text(new HudBox(x, box.Y + layout.LiveY, inner, 16f), data.LiveLine, 11f, HudTone.Muted);

            if (layout.TelemetryY >= 0f)
                PaintTelemetry(into, new HudBox(x, box.Y + layout.TelemetryY, inner, 32f), data);

            if (layout.JourneyY >= 0f)
                PaintJourney(into, new HudBox(x, box.Y + layout.JourneyY, inner, 26f), data);

            if (!string.IsNullOrEmpty(data.BackRegistration))
                into.Button(new HudBox(pillRight - 108f - 86f, box.Y + 14f, 78f, 20f), "‹ " + data.BackRegistration,
                    HudAction.SelectPrefix + data.BackRegistration, HudButtonStyle.Secondary);

            if (layout.PrepY >= 0f)
                PaintPrep(into, new HudBox(x, box.Y + layout.PrepY, inner, 44f), data.Prep);

            if (!data.IsPlayer)
                return;

            if (data.AwaitingStand)
            {
                PaintStands(into, new HudBox(x, box.Y + layout.BodyY, inner, box.Bottom - box.Y - layout.BodyY - 12f), data);
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

        /// <summary>Speed, altitude and heading as three labelled columns instead of one dotted string.</summary>
        private static void PaintTelemetry(HudDrawList into, HudBox area, SelectionCardData data)
        {
            var column = area.Width / 3f;
            var values = new[] { data.Speed, string.IsNullOrEmpty(data.Altitude) ? "On ground" : data.Altitude, data.Heading };
            var captions = new[] { "SPEED", "ALTITUDE", "HEADING" };
            for (var i = 0; i < 3; i++)
            {
                var cell = new HudBox(area.X + column * i, area.Y, column - 6f, area.Height);
                into.Caption(new HudBox(cell.X, cell.Y, cell.Width, 12f), captions[i], HudTone.Muted, HudAlign.Left, 9f);
                into.Text(new HudBox(cell.X, cell.Y + 12f, cell.Width, 20f), values[i], 15f, HudTone.Default,
                    HudTextStyle.Bold);
            }
        }

        /// <summary>Where the aircraft is in its leg: what happens next and when, over a progress bar.</summary>
        private static void PaintJourney(HudDrawList into, HudBox area, SelectionCardData data)
        {
            into.Text(new HudBox(area.X, area.Y, area.Width * 0.62f, 16f), data.JourneyLeft, 11f, HudTone.Default,
                HudTextStyle.Bold);
            if (!string.IsNullOrEmpty(data.JourneyRight))
                into.Text(new HudBox(area.X + area.Width * 0.62f, area.Y, area.Width * 0.38f, 16f), data.JourneyRight,
                    11f, HudTone.Accent, HudTextStyle.Bold, HudAlign.Right);
            if (data.JourneyProgress >= 0f)
                into.Bar(new HudBox(area.X, area.Y + 19f, area.Width, 4f), data.JourneyProgress, HudTone.Accent);
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
                into.Pill(new HudBox(area.X, y, area.Width, 28f), "EVERY STAND IS TAKEN. WAIT FOR ONE",
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
