using System;

namespace Airside.Presentation
{
    public sealed class FlightViewHudData
    {
        public string Registration, Aircraft, Phase, Route, Speed, VerticalSpeed, Distance;
        public string Airspeed;
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
            Identity = new HudBox((width - w) * 0.5f, 20f, w, narrow ? 262f : 214f);
            var controlsHeight = narrow ? 140f : 108f;
            Controls = new HudBox((width - w) * 0.5f, height - controlsHeight - 20f, w, controlsHeight);
            Toast = new HudBox(20f, Identity.Bottom + 12f, Math.Min(480f, Math.Max(0f, width - 40f)), HudShell.ToastHeight);
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

        /// <summary>One readout: a small caption over its value.</summary>
        private static void Instrument(HudDrawList into, float x, float y, float width, string caption, string value, float size)
        {
            into.Caption(new HudBox(x, y, width, 12f), caption, HudTone.Muted, HudAlign.Left, 9f);
            var fitted = FitLine(string.IsNullOrEmpty(value) ? "—" : value, width, 10f);
            into.Text(new HudBox(x, y + 13f, width, size + 8f), fitted, HudShell.FitFontSize(fitted, size, width, 10f),
                HudTone.Default, HudTextStyle.Bold);
        }

        public static void Paint(HudDrawList into, FlightViewHudLayout layout, FlightViewHudData data)
        {
            var box = layout.Identity;
            var x = box.X + 20f;
            var inner = box.Width - 40f;
            into.Surface(box);
            var narrow = box.Width < 520f;
            var identityWidth = box.Width - 164f;

            // Identity: registration large, airframe beside it, Overview top right.
            into.Text(new HudBox(x, box.Y + 14f, narrow ? identityWidth : 130f, 27f), data.Registration,
                HudShell.FitFontSize(data.Registration, 22f, narrow ? identityWidth : 130f, 12f), style: HudTextStyle.Bold);
            into.Text(new HudBox(narrow ? x : x + 136f, box.Y + (narrow ? 44f : 20f),
                narrow ? inner : box.Width - 310f, 18f), data.Aircraft, 12f, HudTone.Muted);
            into.Button(new HudBox(box.Right - 132f, box.Y + 14f, 112f, 30f), "Overview · Esc", Overview, HudButtonStyle.Secondary);

            // Route and phase as chips, with where the aircraft is beside them.
            var chipY = box.Y + (narrow ? 66f : 50f);
            var routeText = FitLine(data.Route, inner * 0.6f, 10f);
            var routeWidth = Math.Min(inner * 0.6f, HudShell.Measure(routeText, 10f, 1f) + 24f);
            into.Pill(new HudBox(x, chipY, routeWidth, 20f), routeText, HudTone.Accent, filled: false, fontSize: 10f);
            var phaseText = FitLine(data.Phase, inner * 0.3f, 10f);
            var phaseWidth = Math.Min(inner * 0.3f, HudShell.Measure(phaseText, 10f, 1f) + 24f);
            var phaseX = x + routeWidth + 8f;
            if (phaseX + phaseWidth <= x + inner)
                into.Pill(new HudBox(phaseX, chipY, phaseWidth, 20f), phaseText, HudTone.Caution, filled: false, fontSize: 10f);
            var locationX = phaseX + phaseWidth + 12f;
            var locationRoom = x + inner - locationX;
            if (!narrow && locationRoom > 80f)
            {
                var place = FitLine(data.Location, locationRoom);
                into.Text(new HudBox(locationX, chipY + 2f, locationRoom, 18f), place,
                    HudShell.FitFontSize(place, 11f, locationRoom, 10f), HudTone.Muted);
            }
            else
            {
                var place = FitLine(data.Location, inner);
                into.Text(new HudBox(x, chipY + 24f, inner, 18f), place,
                    HudShell.FitFontSize(place, 11f, inner, 10f), HudTone.Muted);
            }

            // Instruments: caption over value, the four flying numbers large, the rest smaller.
            var primary = new[] { ("GS", data.Speed), ("FIELD HT", data.Altitude), ("HDG", data.Heading), ("V/S", data.VerticalSpeed) };
            var secondary = new[] { ("IAS", data.Airspeed), ("TO GO", data.Remaining), ("TO AREA", data.Arrival) };
            var columns = narrow ? 2 : 4;
            var cell = inner / columns;
            var tilesY = box.Y + (narrow ? 114f : 84f);
            for (var i = 0; i < primary.Length; i++)
                Instrument(into, x + i % columns * cell, tilesY + i / columns * (narrow ? 38f : 40f), cell - 8f, primary[i].Item1, primary[i].Item2, narrow ? 15f : 17f);
            var secondaryY = tilesY + (narrow ? 2 * 38f : 40f) + 4f;
            const int secondaryColumns = 3;
            var secondaryCell = inner / secondaryColumns;
            for (var i = 0; i < secondary.Length; i++)
                Instrument(into, x + i % secondaryColumns * secondaryCell, secondaryY,
                    secondaryCell - 8f, secondary[i].Item1, secondary[i].Item2, narrow ? 12f : 13f);

            var journey = FitLine(data.Journey, inner);
            into.Text(new HudBox(x, box.Bottom - 32f, inner, 18f), journey,
                HudShell.FitFontSize(journey, 11f, inner, 10f), HudTone.Muted);
            if (data.JourneyProgress >= 0f)
            {
                var track = new HudBox(x, box.Bottom - 10f, inner, 4f);
                into.Fill(track, HudTone.Muted, .2f);
                into.Fill(new HudBox(track.X, track.Y, Math.Max(4f, track.Width * Math.Clamp(data.JourneyProgress, 0f, 1f)), track.Height), HudTone.Accent, 1f);
            }

            box = layout.Controls;
            x = box.X + 16f;
            into.Surface(box);
            var segment = (box.Width - 32f) / 4f;
            for (var i = 0; i < 4; i++)
            {
                var tab = new HudBox(x + i * segment, box.Y + 12f, segment - 4f, 34f);
                if (data.SelectedView == i) into.Fill(tab, HudTone.Accent, 0.24f);
                into.Button(tab, narrow && (i is 1 or 2) ? (i == 1 ? "Left" : "Right") : Views[i], ViewPrefix + i, HudButtonStyle.Secondary, data.Available[i]);
                if (data.SelectedView == i)
                    into.Hairline(new HudBox(tab.X + 12f, tab.Bottom + 2f, tab.Width - 24f, 2f), HudTone.Accent, 0.8f);
            }
            var actionWidth = narrow ? (box.Width - 40f) * .5f : 124f;
            into.Button(new HudBox(x, box.Y + 62f, actionWidth, 28f), "Recenter · Home", Recenter, HudButtonStyle.Secondary);
            into.Button(new HudBox(x + actionWidth + 8f, box.Y + 62f, narrow ? actionWidth : 126f, 28f),
                data.MotionEnabled ? "Vibration on" : "Vibration off", Motion, HudButtonStyle.Secondary, data.SelectedView == 0);
            var hint = data.PassengerIsCargo ? "Cargo aircraft / no passenger seats"
                : CockpitControlHints.Dock(data.SelectedView, narrow);
            into.Text(new HudBox(narrow ? x : x + 272f, box.Y + (narrow ? 102f : 65f), narrow ? box.Width - 32f : box.Width - 304f, 28f), hint, 11f, HudTone.Muted,
                HudTextStyle.Wrap, HudAlign.Right);
        }
    }
}
