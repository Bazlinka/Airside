using System;
using System.Collections.Generic;

namespace Airside.Presentation
{
    public enum OptionsSection { General, Camera, Display, World, Notifications }

    public readonly struct OptionsRow
    {
        public OptionsRow(string label, string value, string detail, string action)
        { Label = label; Value = value; Detail = detail; Action = action; }
        public string Label { get; }
        public string Value { get; }
        public string Detail { get; }
        public string Action { get; }
    }

    public sealed class OptionsMenuModel
    {
        public OptionsSection Section;
        public bool FromTitle;
        public readonly List<OptionsRow> Rows = new();
    }

    public static class OptionsMenuPainter
    {
        public const string Back = "options:back";
        public static readonly string[] Sections = { "General", "Camera", "Display", "World", "Notifications" };

        public static HudBox Panel(float width, float height)
        {
            var w = Math.Min(680f, Math.Max(1f, width - 48f));
            var h = Math.Min(560f, Math.Max(1f, height - 48f));
            return new HudBox((width - w) * 0.5f, (height - h) * 0.5f, w, h);
        }

        public static void Paint(HudDrawList into, HudBox panel, OptionsMenuModel model)
        {
            into.Clear();
            into.Surface(panel, 0.98f);
            var x = panel.X + 24f;
            var width = panel.Width - 48f;
            into.Text(new HudBox(x, panel.Y + 20f, width, 28f), "Options", 24f, HudTone.Default, HudTextStyle.Bold);
            into.Text(new HudBox(x, panel.Y + 54f, width, 18f), "Make Airside comfortable for you.", 12f, HudTone.Muted);
            var tabWidth = (width - 6f * (Sections.Length - 1)) / Sections.Length;
            for (var i = 0; i < Sections.Length; i++)
                into.Button(new HudBox(x + i * (tabWidth + 6f), panel.Y + 84f, tabWidth, 34f),
                    Sections[i], "options:section:" + i,
                    (int)model.Section == i ? HudButtonStyle.Primary : HudButtonStyle.Secondary);

            var rowHeight = Math.Min(68f, (panel.Height - 204f) / Math.Max(1, model.Rows.Count));
            var buttonWidth = Math.Min(160f, width * 0.28f);
            for (var i = 0; i < model.Rows.Count; i++)
            {
                var row = model.Rows[i];
                var y = panel.Y + 134f + i * rowHeight;
                var labelWidth = width - buttonWidth - 18f;
                into.Text(new HudBox(x, y, labelWidth, 20f), row.Label, 15f, HudTone.Default, HudTextStyle.Bold);
                into.Text(new HudBox(x, y + 23f, labelWidth, 28f), row.Detail, 11f, HudTone.Muted, HudTextStyle.Wrap);
                into.Button(new HudBox(panel.Right - 24f - buttonWidth, y + 3f, buttonWidth, 34f),
                    row.Value, row.Action, HudButtonStyle.Secondary);
                if (i + 1 < model.Rows.Count)
                    into.Line(x, y + rowHeight - 8f, panel.Right - 24f, y + rowHeight - 8f, HudTone.Muted, 1f);
            }
            into.Text(new HudBox(x, panel.Bottom - 52f, width - 174f, 26f),
                "Changes save automatically.", 11f, HudTone.Muted, HudTextStyle.Wrap);
            into.Button(new HudBox(panel.Right - 184f, panel.Bottom - 58f, 160f, 36f),
                model.FromTitle ? "BACK TO TITLE" : "BACK", Back, HudButtonStyle.Primary);
        }
    }
}
