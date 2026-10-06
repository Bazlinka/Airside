using System;

namespace Airside.Presentation
{
    public sealed class FlightViewHudData
    {
        public string Registration, Aircraft, Phase, Route, Speed, VerticalSpeed, Distance;
        public string Location, Altitude, Heading, Remaining, Arrival, Journey;
        public float JourneyProgress = -1f;
        public int SelectedView;
        public bool MotionEnabled;
        public readonly bool[] Available = new bool[4];
        public bool PassengerIsCargo;
    }

    /// <summary>Flight identity above, compact viewing dock below; the central sightline stays clear.</summary>
    public readonly struct FlightViewHudLayout
    {
        public FlightViewHudLayout(float width, float height)
        {
            var w = Math.Min(720f, Math.Max(0f, width - 32f));
            var narrow = w < 520f;
            Identity = new HudBox((width - w) * 0.5f, 20f, w, narrow ? 220f : 182f);
            var controlsHeight = narrow ? 140f : 108f;
            Controls = new HudBox((width - w) * 0.5f, height - controlsHeight - 20f, w, controlsHeight);
            Toast = new HudBox(20f, Identity.Bottom + 12f, Math.Min(480f, Math.Max(0f, width - 40f)), 54f);
        }
        public HudBox Identity { get; }
        public HudBox Controls { get; }
        public HudBox Toast { get; }
    }

    public static class FlightViewHudPainter
    {
        public const string ViewPrefix = "flight-view:";
        public const string Recenter = "flight:recenter";
        public const string Overview = "flight:overview";
        public const string Motion = "flight:motion";
        private static readonly string[] Views = { "Cockpit", "Left window", "Right window", "Exterior" };

        private static string FitLine(string text, float width, float minimum = 10f)
        {
            if (string.IsNullOrEmpty(text) || HudShell.Measure(text, minimum) <= width) return text;
            var count = text.Length;
            while (count > 0 && HudShell.Measure(text.Substring(0, count) + "…", minimum) > width) count--;
            return text.Substring(0, count) + "…";
        }

        public static void Paint(HudDrawList into, FlightViewHudLayout layout, FlightViewHudData data)
        {
            var box = layout.Identity;
            var x = box.X + 20f;
            into.Surface(box);
            var narrow = box.Width < 520f;
            var identityWidth = box.Width - 164f;
            into.Text(new HudBox(x, box.Y + 14f, narrow ? identityWidth : 130f, 27f), data.Registration,
                HudShell.FitFontSize(data.Registration, 21f, narrow ? identityWidth : 130f, 12f), style: HudTextStyle.Bold);
            into.Text(new HudBox(narrow ? x : x + 136f, box.Y + (narrow ? 46f : 19f),
                narrow ? box.Width - 40f : box.Width - 310f, 18f), data.Aircraft, 12f, HudTone.Muted);
            into.Button(new HudBox(box.Right - 132f, box.Y + 14f, 112f, 30f), "Overview · Esc", Overview, HudButtonStyle.Secondary);
            var routeY = box.Y + (narrow ? 68f : 46f);
            var route = FitLine(data.Route + "  /  " + data.Phase, box.Width - 40f);
            into.Text(new HudBox(x, routeY, box.Width - 40f, 18f), route,
                HudShell.FitFontSize(route, 12f, box.Width - 40f, 10f), HudTone.Accent);
            var location = FitLine(data.Location, box.Width - 40f);
            into.Text(new HudBox(x, routeY + 21f, box.Width - 40f, 18f), location,
                HudShell.FitFontSize(location, 11f, box.Width - 40f, 10f), HudTone.Muted);
            var metrics = new[] { "GS  " + data.Speed, "FIELD HT  " + data.Altitude, "HDG  " + data.Heading,
                "V/S  " + data.VerticalSpeed, "TO GO  " + data.Remaining, "TO AREA  " + data.Arrival };
            var columns = narrow ? 2 : 3;
            var cell = (box.Width - 40f) / columns;
            for (var i = 0; i < metrics.Length; i++)
            {
                metrics[i] = FitLine(metrics[i], cell - 8f);
                into.Text(new HudBox(x + i % columns * cell, routeY + 50f + i / columns * 23f, cell - 8f, 18f), metrics[i],
                    HudShell.FitFontSize(metrics[i], 12f, cell - 8f, 10f), HudTone.Muted);
            }
            var journey = FitLine(data.Journey, box.Width - 40f);
            into.Text(new HudBox(x, box.Bottom - 32f, box.Width - 40f, 18f), journey,
                HudShell.FitFontSize(journey, 11f, box.Width - 40f, 10f), HudTone.Muted);
            if (data.JourneyProgress >= 0f)
            {
                var track = new HudBox(x, box.Bottom - 9f, box.Width - 40f, 3f);
                into.Fill(track, HudTone.Muted, .18f);
                into.Fill(new HudBox(track.X, track.Y, track.Width * Math.Clamp(data.JourneyProgress, 0f, 1f), track.Height), HudTone.Accent, .8f);
            }

            box = layout.Controls;
            x = box.X + 16f;
            into.Surface(box);
            var segment = (box.Width - 32f) / 4f;
            for (var i = 0; i < 4; i++)
            {
                var tab = new HudBox(x + i * segment, box.Y + 12f, segment - 4f, 34f);
                if (data.SelectedView == i) into.Fill(tab, HudTone.Accent, 0.12f);
                into.Button(tab, narrow && (i is 1 or 2) ? (i == 1 ? "Left" : "Right") : Views[i], ViewPrefix + i, HudButtonStyle.Secondary, data.Available[i]);
                if (data.SelectedView == i)
                    into.Hairline(new HudBox(tab.X + 12f, tab.Bottom + 2f, tab.Width - 24f, 2f), HudTone.Accent, 0.8f);
            }
            var actionWidth = narrow ? (box.Width - 40f) * .5f : 124f;
            into.Button(new HudBox(x, box.Y + 62f, actionWidth, 28f), "Recenter · Home", Recenter, HudButtonStyle.Secondary);
            into.Button(new HudBox(x + actionWidth + 8f, box.Y + 62f, narrow ? actionWidth : 126f, 28f),
                data.MotionEnabled ? "Vibration on" : "Vibration off", Motion, HudButtonStyle.Secondary, data.SelectedView == 0);
            var hint = data.PassengerIsCargo ? "Cargo aircraft / no passenger seats"
                : data.SelectedView == 3 ? "Drag to orbit · scroll to zoom" : "Drag to look · scroll to zoom";
            into.Text(new HudBox(narrow ? x : x + 272f, box.Y + (narrow ? 102f : 65f), narrow ? box.Width - 32f : box.Width - 304f, 28f), hint, 11f, HudTone.Muted,
                HudTextStyle.Wrap, HudAlign.Right);
        }
    }
}
