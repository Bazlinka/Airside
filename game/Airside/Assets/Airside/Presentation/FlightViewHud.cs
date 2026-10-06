using System;

namespace Airside.Presentation
{
    public sealed class FlightViewHudData
    {
        public string Registration, Aircraft, Phase, Route, Speed, VerticalSpeed, Distance;
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
            var w = Math.Min(720f, Math.Max(320f, width - 40f));
            Identity = new HudBox((width - w) * 0.5f, 20f, w, 100f);
            Controls = new HudBox((width - w) * 0.5f, height - 128f, w, 108f);
        }
        public HudBox Identity { get; }
        public HudBox Controls { get; }
    }

    public static class FlightViewHudPainter
    {
        public const string ViewPrefix = "flight-view:";
        public const string Recenter = "flight:recenter";
        public const string Overview = "flight:overview";
        public const string Motion = "flight:motion";
        private static readonly string[] Views = { "Cockpit", "Left window", "Right window", "Exterior" };

        public static void Paint(HudDrawList into, FlightViewHudLayout layout, FlightViewHudData data)
        {
            var box = layout.Identity;
            var x = box.X + 20f;
            into.Surface(box);
            into.Text(new HudBox(x, box.Y + 14f, 130f, 27f), data.Registration, 21f, style: HudTextStyle.Bold);
            into.Text(new HudBox(x + 136f, box.Y + 19f, box.Width - 310f, 22f), data.Aircraft, 12f, HudTone.Muted);
            into.Button(new HudBox(box.Right - 132f, box.Y + 14f, 112f, 30f), "Overview · Esc", Overview, HudButtonStyle.Secondary);
            into.Text(new HudBox(x, box.Y + 44f, box.Width - 40f, 18f), data.Route + "  /  " + data.Phase, 12f, HudTone.Accent);
            var metrics = new[] { "GS  " + data.Speed, "V/S  " + data.VerticalSpeed, "ADELAIDE  " + data.Distance };
            var cell = (box.Width - 40f) / 3f;
            for (var i = 0; i < 3; i++)
                into.Text(new HudBox(x + i * cell, box.Y + 72f, cell - 8f, 18f), metrics[i], 12f, HudTone.Muted);

            box = layout.Controls;
            x = box.X + 16f;
            into.Surface(box);
            var segment = (box.Width - 32f) / 4f;
            for (var i = 0; i < 4; i++)
            {
                var tab = new HudBox(x + i * segment, box.Y + 12f, segment - 4f, 34f);
                if (data.SelectedView == i) into.Fill(tab, HudTone.Accent, 0.12f);
                into.Button(tab, Views[i], ViewPrefix + i, HudButtonStyle.Secondary, data.Available[i]);
                if (data.SelectedView == i)
                    into.Hairline(new HudBox(tab.X + 12f, tab.Bottom + 2f, tab.Width - 24f, 2f), HudTone.Accent, 0.8f);
            }
            into.Button(new HudBox(x, box.Y + 62f, 124f, 28f), "Recenter · Home", Recenter, HudButtonStyle.Secondary);
            into.Button(new HudBox(x + 132f, box.Y + 62f, 126f, 28f),
                data.MotionEnabled ? "Vibration on" : "Vibration off", Motion, HudButtonStyle.Secondary, data.SelectedView == 0);
            var hint = data.PassengerIsCargo ? "Cargo aircraft / no passenger seats"
                : data.SelectedView == 3 ? "Drag to orbit · scroll to zoom" : "Drag to look · scroll to zoom";
            into.Text(new HudBox(x + 272f, box.Y + 65f, box.Width - 304f, 28f), hint, 11f, HudTone.Muted,
                HudTextStyle.Wrap, HudAlign.Right);
        }
    }
}
