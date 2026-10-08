using System;
using System.Collections.Generic;
using Airside.Domain;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>
    /// "Where is my flight?" once it is planned. Every booked or moving player flight gets one row with a
    /// six-step progress bar, so the player always knows how far it has got and what comes next.
    /// Pure state projection: it reads the simulation and never changes it.
    /// </summary>
    public readonly struct FlightTrackerRow
    {
        public FlightTrackerRow(string registration, string flight, string route, int step, float stepProgress01,
            string stepLabel, string status, string time, StatusSeverity severity, bool isSelected)
        {
            Registration = registration ?? string.Empty;
            Flight = flight ?? string.Empty;
            Route = route ?? string.Empty;
            Step = step < 0 ? 0 : step >= FlightTracker.StepCount ? FlightTracker.StepCount - 1 : step;
            StepProgress01 = stepProgress01 < 0f ? 0f : stepProgress01 > 1f ? 1f : stepProgress01;
            StepLabel = stepLabel ?? string.Empty;
            Status = status ?? string.Empty;
            Time = time ?? string.Empty;
            Severity = severity;
            IsSelected = isSelected;
        }

        public string Registration { get; }

        /// <summary>The board's flight number, or the registration when none is shown.</summary>
        public string Flight { get; }

        /// <summary>"Adelaide → Kingscote".</summary>
        public string Route { get; }

        /// <summary>Zero-based index into <see cref="FlightTracker.StepLabels"/>: the step happening now.</summary>
        public int Step { get; }

        /// <summary>How far through the current step (preparation, the flight leg), 0..1.</summary>
        public float StepProgress01 { get; }

        public string StepLabel { get; }

        /// <summary>The plain-words state: "Boarding", "Taxiing", "Departed".</summary>
        public string Status { get; }

        /// <summary>The time that matters now: "Departs 14:05", "ETA 15:20".</summary>
        public string Time { get; }

        public StatusSeverity Severity { get; }
        public bool IsSelected { get; }
    }

    public static class FlightTracker
    {
        public const int StepCount = 6;
        public const int MaxRows = 3;

        public static readonly string[] StepLabels = { "BOOKED", "READY", "TAXI", "FLYING", "LANDING", "ARRIVED" };

        /// <summary>The step a local aircraft is on, or -1 when it has no flight to track.</summary>
        public static int StepOf(FleetAircraft aircraft, SimulationTime now, PlayerBaseLevel baseLevel)
        {
            if (aircraft == null)
                return -1;
            switch (aircraft.State)
            {
                case FleetState.AtStand:
                    if (!aircraft.Scheduled.HasValue || aircraft.Scheduled.Value.Cancelled)
                        return -1;
                    return DeparturePrep.For(aircraft, now, baseLevel).Ready ? 1 : 0;
                case FleetState.TaxiOut:
                case FleetState.HoldingShort:
                    return 2;
                case FleetState.TakingOff:
                case FleetState.Outbound:
                case FleetState.AtDestination:
                case FleetState.Inbound:
                    return 3;
                case FleetState.HoldingForLanding:
                case FleetState.GoAround:
                case FleetState.Landing:
                    return 4;
                case FleetState.AwaitingStand:
                case FleetState.TaxiIn:
                    return 5;
                default:
                    return -1;
            }
        }

        /// <summary>
        /// Fills <paramref name="into"/> with at most <paramref name="max"/> rows from the airline board, the
        /// selected aircraft first. Aircraft with nothing booked and nothing moving are not tracked.
        /// </summary>
        public static void Fill(AirlineOperations operations, SimulationTime now, string selectedRegistration,
            List<FlightTrackerRow> into, List<OperationsRow> scratch, int max = MaxRows)
        {
            into.Clear();
            if (operations?.PlayerAirline == null)
                return;
            OperationsSummary.FillAirlineRows(operations, now, scratch);
            var baseLevel = operations.CareerState?.BaseLevel ?? PlayerBaseLevel.Starter;
            var local = new Dictionary<string, FleetAircraft>(StringComparer.Ordinal);
            foreach (var aircraft in operations.FleetOf(operations.PlayerAirline))
                local[aircraft.Registration] = aircraft;

            var tracked = new List<FlightTrackerRow>();
            foreach (var row in scratch)
            {
                var step = -1;
                var progress = 0f;
                if (local.TryGetValue(row.Registration, out var aircraft))
                {
                    step = StepOf(aircraft, now, baseLevel);
                    if (step == 0 || step == 3 || step == 4)
                        progress = row.Progress01 < 0f ? 0f : row.Progress01;
                }
                else
                {
                    // Based away from Adelaide: the board only knows booked / out / back.
                    switch (row.State)
                    {
                        case "Booked": step = 0; break;
                        case "Outbound":
                        case "Turnaround":
                        case "Inbound":
                            step = 3;
                            progress = row.Progress01 < 0f ? 0f : row.Progress01;
                            break;
                    }
                }

                if (step < 0)
                    continue;
                tracked.Add(new FlightTrackerRow(row.Registration, row.FlightLabel, row.RouteText, step, progress,
                    StepLabels[step], row.State, row.TimeText, row.Severity,
                    string.Equals(row.Registration, selectedRegistration, StringComparison.Ordinal)));
            }

            // The aircraft being looked at leads; the rest keep the board's attention-then-next-event order.
            tracked.Sort((a, b) => b.IsSelected.CompareTo(a.IsSelected));
            for (var i = 0; i < tracked.Count && into.Count < max; i++)
                into.Add(tracked[i]);
            TotalTracked = tracked.Count;
        }

        /// <summary>How many flights the last <see cref="Fill"/> found, including any beyond the shown rows.</summary>
        public static int TotalTracked { get; private set; }
    }

    /// <summary>Draws the tracker card into the shared draw list (pure, so it is checked headlessly).</summary>
    public static class FlightTrackerPainter
    {
        public const float Width = 400f;
        public const float HeaderHeight = 30f;
        public const float RowHeight = 54f;
        public const float FooterHeight = 18f;
        public const float Padding = 14f;

        public static float HeightFor(int rows, bool footer) =>
            rows <= 0 ? 0f : HeaderHeight + rows * RowHeight + (footer ? FooterHeight : 0f) + 6f;

        public static void Paint(HudDrawList into, HudBox box, IReadOnlyList<FlightTrackerRow> rows, int total)
        {
            if (into == null || box.IsEmpty || rows == null || rows.Count == 0)
                return;
            into.Surface(box, 0.88f);
            into.Caption(new HudBox(box.X + Padding, box.Y + 10f, box.Width - Padding * 2f, 14f), "YOUR FLIGHTS",
                HudTone.Accent);
            var more = total - rows.Count;
            if (more > 0)
                into.Text(new HudBox(box.Right - Padding - 120f, box.Y + 9f, 120f, 14f), $"+{more} more in Ops", 10f,
                    HudTone.Muted, HudTextStyle.Regular, HudAlign.Right);

            var y = box.Y + HeaderHeight;
            var width = box.Width - Padding * 2f;
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var rowBox = new HudBox(box.X + 6f, y - 2f, box.Width - 12f, RowHeight - 2f);
                if (row.IsSelected)
                    into.Fill(rowBox, HudTone.Accent, 0.12f);
                var tone = row.Severity >= StatusSeverity.Warning ? HudTone.Negative
                    : row.Severity >= StatusSeverity.Attention ? HudTone.Caution : HudTone.Accent;
                var x = box.X + Padding;
                var timeWidth = Math.Min(140f, width * 0.38f);
                var title = row.Flight + (row.Route.Length > 0 ? "  ·  " + row.Route : string.Empty);
                into.Text(new HudBox(x, y, width - timeWidth - 6f, 16f), title,
                    HudShell.FitFontSize(title, 12f, width - timeWidth - 6f, 10f), HudTone.Default, HudTextStyle.Bold);
                into.Text(new HudBox(x + width - timeWidth, y, timeWidth, 16f), row.Time, 11f, HudTone.Muted,
                    HudTextStyle.Regular, HudAlign.Right);

                // Six segments: done ones solid, the current one fills with its own progress.
                var gap = 3f;
                var segment = (width - gap * (FlightTracker.StepCount - 1)) / FlightTracker.StepCount;
                for (var s = 0; s < FlightTracker.StepCount; s++)
                {
                    var seg = new HudBox(x + s * (segment + gap), y + 22f, segment, 5f);
                    into.Fill(seg, HudTone.Muted, 0.25f);
                    if (s < row.Step)
                        into.Fill(seg, tone, 0.9f);
                    else if (s == row.Step)
                        into.Fill(seg.WithWidth(Math.Max(3f, segment * row.StepProgress01)), tone, 0.95f);
                }

                var caption = $"{row.Step + 1}/{FlightTracker.StepCount}  {row.StepLabel}";
                into.Text(new HudBox(x, y + 31f, 110f, 14f), caption, 10f, tone, HudTextStyle.Bold);
                into.Text(new HudBox(x + 112f, y + 31f, width - 112f, 14f), row.Status, 11f, HudTone.Muted,
                    HudTextStyle.Regular, HudAlign.Right);
                into.Hotspot(rowBox, HudAction.Select(row.Registration));
                y += RowHeight;
            }
        }
    }
}
