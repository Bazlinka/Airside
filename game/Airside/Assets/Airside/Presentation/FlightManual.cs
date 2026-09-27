using System;
using System.Collections.Generic;

namespace Airside.Presentation
{
    /// <summary>One page of the in-game Flight Manual: a title, a one-line lead and short sections.</summary>
    public sealed class FlightManualPage
    {
        public FlightManualPage(string id, string title, string lead, IReadOnlyList<(string Heading, string Body)> sections)
        {
            Id = id;
            Title = title;
            Lead = lead;
            Sections = sections ?? Array.Empty<(string, string)>();
        }

        public string Id { get; }
        public string Title { get; }
        public string Lead { get; }
        public IReadOnlyList<(string Heading, string Body)> Sections { get; }
    }

    /// <summary>
    /// How to play Airside (ADR 0123): the rules the simulation actually runs, in plain language,
    /// plus the controls. Reachable from the title screen, the airline setup briefing, the rail's
    /// help button and F1. Copy only — it reads nothing and decides nothing, so it is kept honest by
    /// tests that pin the numbers it quotes to the constants the game uses.
    /// </summary>
    public static class FlightManual
    {
        public const string ControlsPageId = "controls";

        public static IReadOnlyList<FlightManualPage> Pages { get; } = new[]
        {
            new FlightManualPage("welcome", "Welcome to Airside",
                "You run a small airline from Adelaide Airport, in live Adelaide time.",
                new[]
                {
                    ("The airport is real and it is live",
                        "One second in the game is one real second, and the clock is Adelaide's own. There is no pause "
                        + "or fast-forward: the airport keeps running while the menu is open, and while the game is "
                        + "closed your booked flights still land and settle."),
                    ("You start small",
                        "One Saab 340B on the regional bays and a small float of cash. Everything else — more aircraft, "
                        + "jets, interstate and international routes, other bases — is earned."),
                    ("The screen",
                        "Left: the navigation rail. Top: your status (time, funds, reliability, tier). Bottom-left: the "
                        + "career ring. Top-right: your flights. Bottom-right: the airfield radar. Bottom-centre: the "
                        + "aircraft you have selected.")
                }),
            new FlightManualPage("first-flight", "Your first flight",
                "Plan it, turn it round, watch it go and bring it home.",
                new[]
                {
                    ("1 · Plan",
                        "Select your aircraft (click it, a flight tile, or press [ / ]) and press PLAN FLIGHT, or open "
                        + "Map with Tab. Choose a green destination and a departure time, then press PLAN FLIGHT on the "
                        + "map. Kingscote is a short first hop."),
                    ("2 · Turnaround",
                        "Fuel, catering, baggage and boarding run on their own before pushback. The selected-aircraft "
                        + "card shows each one. Follow the aircraft (F) to watch it push back, taxi and take off."),
                    ("3 · Away and back",
                        "Track it on the Map while it is away. When it lands, choose a stand on its card — BEST is the "
                        + "shortest taxi — or the tower parks it after 90 seconds."),
                    ("4 · Paid",
                        "Parking completes the trip and settles it: the revenue lands in your funds straight away.")
                }),
            new FlightManualPage("money", "Money and contracts",
                "Every flight pays. Contracts add a bonus that buys the next aircraft.",
                new[]
                {
                    ("Revenue and cost",
                        "Booking a flight charges its dispatch cost; landing it pays revenue from the seats you fill. The "
                        + "Map shows the forecast before you book. A big aircraft on a thin route can lose money."),
                    ("Contracts",
                        "Contracts (Contracts page) pay a bonus per rotation on their route and a completion reward. "
                        + "One at a time. ABANDON drops one for a small reliability cost if you can no longer fly it."),
                    ("Out of cash",
                        "If you cannot afford a flight, a recovery contract to Kingscote appears and pays for its own "
                        + "first dispatch, so an airline is never stranded."),
                    ("Difficulty",
                        "Relaxed, Standard or Demanding is chosen when the airline is founded. It sets your float, fares, "
                        + "flight costs and how hard lateness is punished. Contract terms are never scaled.")
                }),
            new FlightManualPage("reliability", "Reliability",
                "Partners trust punctual airlines. Reliability starts at 100% — keep it there.",
                new[]
                {
                    ("Punctuality",
                        "Pushing back within 2 minutes of the booked time earns +1. Up to 5 minutes late is neutral; up to "
                        + "15 costs 1; later costs 2. Contract rotations add their own bonus."),
                    ("Maintenance",
                        "Every aircraft needs a routine check every 8 rotations (SEND FOR CHECK on its card, or Fleet). "
                        + "Flying overdue costs reliability on every rotation."),
                    ("Why it matters",
                        "Below 70% only regional offers appear and flight revenue is cut. Each tier asks you to hold a "
                        + "reliability level.")
                }),
            new FlightManualPage("career", "Career",
                "Four operating tiers, then the established-airline ending.",
                new[]
                {
                    ("The ring",
                        "The career ring (bottom-left) shows how many of this tier's goals are done and what finishing "
                        + "them earns: TOWARD REGIONAL, TOWARD DOMESTIC … Click it for the Career track."),
                    ("Goals",
                        "Each tier has a handful of goals — fulfil a contract, fly services, serve destinations, expand "
                        + "the base, hold reliability. Do them in any order; PIN one to lead the HUD."),
                    ("Tiers",
                        "Provisional → Regional → Domestic → International. Tiers are earned in order and never lost. "
                        + "They unlock aircraft, base upgrades and international routes."),
                    ("The ending",
                        "Finish every International goal — 18 aircraft, three bases, 12 destinations, a widebody and a "
                        + "profitable network — to become an established airline. Then keep flying.")
                }),
            new FlightManualPage("growing", "Growing the airline",
                "More aircraft, a bigger base, other cities, and schedules that run themselves.",
                new[]
                {
                    ("Aircraft",
                        "Buy in Fleet: ATR 42, then Dash 8-400, 737-8 and A321neo, then the A350 and 787. Each asks for a "
                        + "tier, a reliability level and a number of flights. Selling returns 55%."),
                    ("Adelaide base",
                        "EXPAND BASE (Career › Airline) adds aircraft slots, jet gates, local maintenance and faster "
                        + "turnarounds."),
                    ("Outstations",
                        "From Domestic, open bases in Melbourne, Sydney, Brisbane or Perth (Fleet › Network). Aircraft "
                        + "based there fly between other cities off-map."),
                    ("Repeat schedules",
                        "After 12 hand-planned flights, REPEAT keeps an aircraft flying a route every 6, 12 or 24 hours "
                        + "while you play.")
                }),
            new FlightManualPage(ControlsPageId, "Controls",
                "Mouse for everything; keys for speed.",
                Controls())
        };

        public static int IndexOf(string id)
        {
            for (var i = 0; i < Pages.Count; i++)
                if (Pages[i].Id == id)
                    return i;
            return 0;
        }

        private static IReadOnlyList<(string, string)> Controls()
        {
            var sections = new List<(string, string)>();
            foreach (var section in ControlsHelp.Sections)
            {
                var lines = new List<string>();
                foreach (var binding in section.Bindings)
                    lines.Add(binding.Key + "  —  " + binding.Action);
                sections.Add((section.Title, string.Join("\n", lines)));
            }
            return sections;
        }
    }

    /// <summary>A centred glass manual: page list on the left, the page on the right, Prev / Next.</summary>
    public static class FlightManualPainter
    {
        public const string PagePrefix = "manual:page:";
        public const string Next = "manual:next";
        public const string Previous = "manual:previous";

        public static HudBox Panel(float viewportWidth, float viewportHeight) =>
            HudShell.CentredPanel(new HudBox(HudShell.Margin, HudShell.Margin, viewportWidth - HudShell.Margin * 2f,
                viewportHeight - HudShell.Margin * 2f), 860f, 600f);

        public static void Paint(HudDrawList into, HudBox panel, int pageIndex)
        {
            if (into == null || panel.IsEmpty)
                return;
            var pages = FlightManual.Pages;
            pageIndex = Math.Clamp(pageIndex, 0, pages.Count - 1);
            var page = pages[pageIndex];
            into.Clear();
            into.Surface(panel, 0.96f);
            into.Caption(new HudBox(panel.X + 24f, panel.Y + 22f, 200f, 12f), "FLIGHT MANUAL", HudTone.Accent,
                HudAlign.Left, 10f);
            into.Button(HudShellPainter.CloseBox(panel), "×", HudAction.Close, HudButtonStyle.Secondary);

            var listWidth = panel.Width >= 700f ? 200f : 0f;
            var y = panel.Y + 50f;
            if (listWidth > 0f)
            {
                for (var i = 0; i < pages.Count; i++)
                {
                    var row = new HudBox(panel.X + 14f, y, listWidth, 34f);
                    if (i == pageIndex)
                    {
                        into.Fill(row, HudTone.Accent, 0.16f);
                        into.Fill(new HudBox(row.X, row.Y + 8f, 3f, 18f), HudTone.Accent, 1f);
                    }
                    into.Text(new HudBox(row.X + 14f, row.Y + 9f, row.Width - 20f, 16f), pages[i].Title, 12f,
                        i == pageIndex ? HudTone.Default : HudTone.Muted, i == pageIndex ? HudTextStyle.Bold : HudTextStyle.Regular);
                    into.Hotspot(row, PagePrefix + i);
                    y += 38f;
                }
                into.Hairline(new HudBox(panel.X + listWidth + 26f, panel.Y + 50f, 1f, panel.Height - 110f),
                    HudTone.Muted, 0.18f);
            }

            var x = panel.X + (listWidth > 0f ? listWidth + 50f : 24f);
            var width = panel.Right - 24f - x;
            var top = panel.Y + 46f;
            into.Text(new HudBox(x, top, width - 40f, 30f), page.Title, 22f, HudTone.Default, HudTextStyle.Bold);
            into.Text(new HudBox(x, top + 34f, width, 18f), page.Lead, 12f, HudTone.Accent);
            y = top + 66f;
            var floor = panel.Bottom - 64f;
            foreach (var (heading, body) in page.Sections)
            {
                var lines = EstimateLines(body, width, 11.5f);
                var height = 18f + lines * 16f;
                if (y + height > floor)
                    break;
                into.Text(new HudBox(x, y, width, 18f), heading, 13f, HudTone.Default, HudTextStyle.Bold);
                into.Text(new HudBox(x, y + 18f, width, lines * 16f + 2f), body, 11.5f, HudTone.Muted, HudTextStyle.Wrap);
                y += height + 10f;
            }

            var footer = panel.Bottom - 50f;
            into.Text(new HudBox(x, footer + 10f, 120f, 16f), $"{pageIndex + 1} / {pages.Count}", 11f, HudTone.Muted);
            into.Button(new HudBox(panel.Right - 24f - 230f, footer, 110f, 34f), "PREVIOUS", Previous,
                HudButtonStyle.Secondary, pageIndex > 0);
            into.Button(new HudBox(panel.Right - 24f - 110f, footer, 110f, 34f),
                pageIndex < pages.Count - 1 ? "NEXT" : "DONE", pageIndex < pages.Count - 1 ? Next : HudAction.Close,
                HudButtonStyle.Primary);
        }

        /// <summary>Wrapped line count at a width, using the shared glyph estimate plus explicit line breaks.</summary>
        public static int EstimateLines(string text, float width, float fontSize)
        {
            if (string.IsNullOrEmpty(text))
                return 0;
            var perLine = Math.Max(10, (int)(width / (fontSize * 0.52f)));
            var lines = 0;
            foreach (var paragraph in text.Split('\n'))
                lines += Math.Max(1, (paragraph.Length + perLine - 1) / perLine);
            return lines;
        }

        /// <summary>Applies a manual action; returns the new page index, or -1 to close.</summary>
        public static int Apply(string action, int pageIndex)
        {
            if (action == HudAction.Close)
                return -1;
            if (action == Next)
                return Math.Min(pageIndex + 1, FlightManual.Pages.Count - 1);
            if (action == Previous)
                return Math.Max(pageIndex - 1, 0);
            var payload = HudAction.Payload(action, PagePrefix);
            return int.TryParse(payload, out var page) && page >= 0 && page < FlightManual.Pages.Count ? page : pageIndex;
        }
    }
}
