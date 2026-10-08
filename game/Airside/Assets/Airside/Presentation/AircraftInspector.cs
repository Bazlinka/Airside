using System;
using Airside.Domain;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>Pure inspector geometry. Identity and commands never scroll with aircraft details.</summary>
    public readonly struct AircraftInspectorLayout
    {
        public AircraftInspectorLayout(HudBox panel)
        {
            Panel = panel;
            // The painter uses fixed text and button sizes. Shrinking these bands
            // on a short desktop makes details cover identity and camera buttons
            // overlap Cancel / Follow. The body alone absorbs the smaller height.
            var header = Math.Min(128f, panel.Height);
            var footer = Math.Min(150f, Math.Max(0f, panel.Height - header));
            Header = new HudBox(panel.X, panel.Y, panel.Width, header);
            Body = new HudBox(panel.X + 20f, panel.Y + header, Math.Max(1f, panel.Width - 40f), Math.Max(1f, panel.Height - header - footer));
            Footer = new HudBox(panel.X + 20f, panel.Bottom - footer + 12f, Math.Max(1f, panel.Width - 40f), footer - 24f);
        }
        /// <summary>Shortest panel that keeps the full header and footer.</summary>
        public const float MinFittedHeight = 430f;

        /// <summary>
        /// Panel height for this much body content: the full header and footer plus the content,
        /// never taller than the space the shell allows. A parked aircraft no longer gets a
        /// mostly empty full-height panel.
        /// </summary>
        public static float FittedHeight(float available, float contentHeight) =>
            Math.Min(available, Math.Max(MinFittedHeight, 128f + 150f + contentHeight + 8f));

        public HudBox Panel { get; }
        public HudBox Header { get; }
        public HudBox Body { get; }
        public HudBox Footer { get; }
    }

    public static class MaintenanceInspectorProjection
    {
        private static readonly string[] Steps = { "Prepare aircraft", "Start engines", "Taxi to maintenance", "Position inside shed", "Maintenance check", "Return to stand" };
        public static void Fill(SelectionCardData card, FleetAircraft aircraft, SimulationTime now)
        {
            var job = aircraft.MaintenanceJob;
            if (job == null) return;
            card.IsMaintenance = true;
            card.RouteLine = job.Hangar(aircraft.Type).HangarName + " · Maintenance bay";
            card.LiveLine = "No passengers or cargo on board";
            card.HoldLine = job.WaitReason ?? string.Empty;
            card.HoldAction = string.Empty;
            card.PhaseLabel = job.Label;
            card.Prep.Clear();
            var step = job.Phase switch
            {
                MaintenancePhase.Preparing => 0,
                MaintenancePhase.Starting => 1,
                MaintenancePhase.WaitingOutbound or MaintenancePhase.Taxiing => 2,
                MaintenancePhase.ShuttingDown or MaintenancePhase.Positioning => 3,
                MaintenancePhase.Repairing => 4,
                _ => 5
            };
            for (var i = 0; i < Steps.Length; i++)
                card.Prep.Add(new SelectionPrepStage(Steps[i], i < step ? 1f : 0f, i == step));
            card.JourneyLeft = job.Phase == MaintenancePhase.WaitingOutbound ? "Taxi clearance pending"
                : job.Phase == MaintenancePhase.WaitingReturn ? "Return time pending" : "Ground movement in progress";
            card.JourneyRight = string.Empty;
            card.JourneyProgress = -1f;
            if (job.Phase == MaintenancePhase.Repairing)
            {
                card.JourneyLeft = "Repair time remaining";
                card.JourneyRight = SelectionCardText.Remaining(job.PhaseEndsAt - now.ElapsedSeconds);
                card.JourneyProgress = (float)Math.Clamp((now.ElapsedSeconds - job.PhaseStartedAt) / (double)job.RepairSeconds, 0, 1);
            }
        }
    }

    public static class AircraftInspectorPainter
    {
        public static float ContentHeight(SelectionCardData data) => 130f + data.Prep.Count * 36f
            + (data.AwaitingStand ? 28f + Math.Max(1, data.Stands.Count) * 36f : 0f)
            + (data.HasJourney ? 44f : 0f);

        public static void Header(HudDrawList into, AircraftInspectorLayout layout, SelectionCardData data)
        {
            var box = layout.Panel;
            into.Surface(box, .97f);
            var x = box.X + 20f;
            var width = box.Width - 40f;
            if (string.IsNullOrEmpty(data.BackRegistration))
                into.Caption(new HudBox(x, box.Y + 18f, width - 32f, 12f), "SELECTED AIRCRAFT", fontSize: 9f);
            else into.Button(new HudBox(x, box.Y + 12f, width - 40f, 24f), "‹ Back to " + data.BackRegistration,
                HudAction.SelectPrefix + data.BackRegistration, HudButtonStyle.Secondary);
            into.Button(new HudBox(box.Right - 46f, box.Y + 12f, 30f, 30f), "×", HudAction.CardClose, HudButtonStyle.Secondary);
            into.Text(new HudBox(x, box.Y + 40f, width, 30f), data.Registration, 26f);
            into.Text(new HudBox(x, box.Y + 76f, width, 18f), data.TypeName, 12f, HudTone.Muted);
            into.Dot(x + 3f, box.Y + 111f, 6f, data.PhaseTone);
            into.Text(new HudBox(x + 14f, box.Y + 100f, width - 14f, 22f), data.PhaseLabel, 14f, data.PhaseTone);
        }

        /// <summary>Local scroll coordinates; runtime clips this independently from header/footer.</summary>
        public static void Body(HudDrawList into, HudBox area, SelectionCardData data)
        {
            var y = area.Y + 6f;
            into.Text(new HudBox(area.X, y, area.Width, 34f), data.RouteLine, 12f, HudTone.Default, HudTextStyle.Wrap);
            y += 42f;
            if (!string.IsNullOrEmpty(data.HoldLine))
            {
                into.Text(new HudBox(area.X, y, area.Width, 40f), data.HoldLine, 12f, HudTone.Caution, HudTextStyle.Wrap);
                if (!string.IsNullOrEmpty(data.HoldAction)) into.Hotspot(new HudBox(area.X, y, area.Width, 40f), data.HoldAction);
            }
            else into.Text(new HudBox(area.X, y, area.Width, 40f), data.LiveLine, 12f, HudTone.Muted, HudTextStyle.Wrap);
            y += 48f;
            if (data.HasJourney)
            {
                into.Text(new HudBox(area.X, y, area.Width, 16f), data.JourneyLeft, 12f);
                into.Text(new HudBox(area.X, y + 19f, area.Width, 14f), data.JourneyRight, 11f, HudTone.Muted);
                if (data.JourneyProgress >= 0) into.Bar(new HudBox(area.X, y + 36f, area.Width, 3f), data.JourneyProgress, HudTone.Accent);
                y += 48f;
            }
            if (data.Prep.Count > 0)
            {
                into.Caption(new HudBox(area.X, y, area.Width, 12f), data.IsMaintenance ? "MAINTENANCE JOURNEY" : "PREPARATION", fontSize: 9f);
                y += 24f;
                foreach (var step in data.Prep)
                {
                    if (y + 24f > area.Bottom) break;
                    var tone = step.Done ? HudTone.Positive : step.Active ? HudTone.Accent : HudTone.Muted;
                    into.Dot(area.X + 4f, y + 7f, step.Active ? 8f : 6f, tone);
                    into.Text(new HudBox(area.X + 20f, y - 2f, area.Width - 20f, 24f), step.Name, 12f, tone);
                    y += 36f;
                }
            }
            if (data.AwaitingStand)
            {
                into.Caption(new HudBox(area.X, y, area.Width, 12f), "CHOOSE A STAND", fontSize: 9f);
                y += 24f;
                if (data.Stands.Count == 0) into.Text(new HudBox(area.X, y, area.Width, 32f), "Waiting for a compatible free stand", 12f, HudTone.Caution, HudTextStyle.Wrap);
                foreach (var stand in data.Stands)
                {
                    if (y + 30f > area.Bottom) break;
                    into.Button(new HudBox(area.X, y, area.Width, 30f), stand.Label + (stand.Best ? " · Recommended" : ""),
                        HudAction.StandPrefix + stand.StandId, HudButtonStyle.Secondary);
                    y += 36f;
                }
            }
        }

        public static void Footer(HudDrawList into, AircraftInspectorLayout layout, SelectionCardData data)
        {
            var box = layout.Footer;
            into.Hairline(new HudBox(box.X, box.Y - 10f, box.Width, 1f), alpha: .15f);
            var useFollow = data.IsMaintenance || !data.IsPlayer || string.IsNullOrEmpty(data.PrimaryLabel);
            into.Button(new HudBox(box.X, box.Y, box.Width, 38f), useFollow
                    ? data.Following ? "Release follow" : "Follow aircraft" : data.PrimaryLabel,
                useFollow ? HudAction.CardFollow : HudAction.Primary, HudButtonStyle.Primary,
                !useFollow || data.CanFollow);
            var y = box.Y + 44f;
            if (data.CanCancel)
                into.Button(new HudBox(box.X, y, box.Width, 28f), "Cancel planned flight", HudAction.Cancel, HudButtonStyle.Secondary);
            else if (!useFollow && data.CanFollow)
                into.Button(new HudBox(box.X, y, box.Width, 28f), data.Following ? "Release follow" : "Follow aircraft",
                    HudAction.CardFollow, HudButtonStyle.Secondary);
            if (!data.ShowCameraActions) return;
            y = box.Bottom - 32f;
            var width = (box.Width - 12f) / 3f;
            into.Button(new HudBox(box.X, y, width, 32f), "Cockpit", "camera-cockpit", HudButtonStyle.Secondary, data.CanCockpit);
            into.Button(new HudBox(box.X + width + 6f, y, width, 32f), "Window", "camera-passenger", HudButtonStyle.Secondary, data.CanPassenger);
            into.Button(new HudBox(box.X + width * 2f + 12f, y, width, 32f), "Exterior", "camera-exterior", HudButtonStyle.Secondary, data.CanExterior);
        }

        /// <summary>Offline preview of the actual runtime painter, with details clipped to their visible area.</summary>
        public static void Paint(HudDrawList into, HudBox box, SelectionCardData data)
        {
            var layout = new AircraftInspectorLayout(box);
            Header(into, layout, data);
            var body = new HudDrawList();
            Body(body, layout.Body, data);
            into.AppendClipped(body, layout.Body);
            Footer(into, layout, data);
        }
    }
}
