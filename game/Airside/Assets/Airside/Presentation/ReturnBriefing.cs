using System;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>Structured return report; the fleet body scrolls independently of its header and actions.</summary>
    public readonly struct ReturnBriefingLayout
    {
        public ReturnBriefingLayout(HudBox area)
        {
            Surface = HudShell.CentredPanel(area, 820f, 610f);
            Fleet = new HudBox(Surface.X + 28f, Surface.Y + 246f, Surface.Width - 56f,
                Math.Max(50f, Surface.Height - 332f));
            Continue = new HudBox(Surface.Right - 232f, Surface.Bottom - 64f, 204f, 40f);
        }
        public HudBox Surface { get; }
        public HudBox Fleet { get; }
        public HudBox Continue { get; }
    }

    public static class ReturnBriefingPainter
    {
        public const string ContinueAction = "briefing:continue";
        public const float FleetRowHeight = 72f;

        public static void Paint(HudDrawList into, ReturnBriefingLayout layout, AwaySummary summary, string airline)
        {
            var box = layout.Surface;
            into.Surface(box);
            var x = box.X + 28f;
            var inner = box.Width - 56f;
            into.Caption(new HudBox(x, box.Y + 26f, inner, 18f), "YOUR AIRLINE / RETURN BRIEFING", HudTone.Accent);
            into.Text(new HudBox(x, box.Y + 56f, inner, 42f), "Welcome back.", 32f, style: HudTextStyle.Bold);
            into.Text(new HudBox(x, box.Y + 106f, inner, 24f),
                airline + "  ·  " + summary.Title, 14f, HudTone.Muted);
            var width = (inner - 24f) / 3f;
            var money = (summary.NetFunds < 0 ? "−" : summary.NetFunds > 0 ? "+" : "")
                + "$" + Math.Abs(summary.NetFunds).ToString("N0");
            var values = new[] { summary.PlayerFlights.ToString(), money, summary.Reliability + "%" };
            var change = summary.ReliabilityChange;
            var captions = new[] { "FLIGHTS COMPLETED", "NET FUNDS",
                "RELIABILITY" + (change == 0 ? "" : " / " + change.ToString("+0;-0;0")) };
            for (var i = 0; i < 3; i++)
            {
                var cell = new HudBox(x + i * (width + 12f), box.Y + 148f, width, 70f);
                into.Card(cell);
                into.Caption(new HudBox(cell.X + 14f, cell.Y + 10f, width - 28f, 16f), captions[i], fontSize: 10f);
                var tone = i == 1 && summary.NetFunds < 0 ? HudTone.Negative : HudTone.Default;
                into.Text(new HudBox(cell.X + 14f, cell.Y + 31f, width - 28f, 30f), values[i],
                    HudShell.FitFontSize(values[i], 24f, width - 28f, 14f), tone, HudTextStyle.Bold);
            }
            into.Caption(new HudBox(x, box.Y + 226f, inner, 18f), "YOUR FLEET / CURRENT STATUS");
            into.Hairline(new HudBox(x, box.Bottom - 82f, inner, 1f), alpha: 0.2f);
            into.Text(new HudBox(x, box.Bottom - 63f, inner - 222f, 42f),
                summary.AwaySeconds >= AwayCatchUp.MaxSeconds ? "Catch-up is limited to one week."
                    : summary.OtherFlights + " flights by other operators while you were away.",
                12f, HudTone.Muted, HudTextStyle.Wrap);
            into.Button(layout.Continue, "Return to the airport", ContinueAction, HudButtonStyle.Primary);
        }

        public static void PaintFleet(HudDrawList into, HudBox area, AwaySummary summary)
        {
            if (summary.FleetLines.Count == 0)
                into.Text(area, "Your fleet report will appear here after your first flight.", 14f, HudTone.Muted,
                    HudTextStyle.Wrap);
            for (var i = 0; i < summary.FleetLines.Count; i++)
            {
                var y = area.Y + i * FleetRowHeight;
                into.Caption(new HudBox(area.X, y + 4f, 40f, 18f), (i + 1).ToString("00"), HudTone.Accent);
                into.Text(new HudBox(area.X + 48f, y + 2f, area.Width - 54f, 54f), summary.FleetLines[i], 13f,
                    style: HudTextStyle.Wrap);
                into.Hairline(new HudBox(area.X + 48f, y + 64f, area.Width - 54f, 1f), alpha: 0.15f);
            }
        }
    }
}
