using System;
using System.Collections.Generic;
using Airside.Simulation;

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
                "You run a small airline out of Adelaide Airport, on Adelaide time.",
                new[]
                {
                    ("The clock is real",
                        "One second in the game is one real second, and the clock is Adelaide's. You can't pause or "
                        + "speed up. The airport keeps going while the menu is open, and your booked flights still land "
                        + "and get paid while the game is closed."),
                    ("You start small",
                        "You have one Saab 340B on the regional bays and a little cash. A second Saab is for sale "
                        + "in Fleet from that cash. More aircraft, jets, interstate "
                        + "and overseas routes and other bases all have to be earned."),
                    ("The screen",
                        "The rail on the left opens each page. Across the top: the time, your money, reliability and "
                        + "tier. Bottom left is the career ring. Top right, your flights. Bottom right, the airfield "
                        + "radar. Bottom centre, the aircraft you have selected.")
                }),
            new FlightManualPage("first-flight", "Your first flight",
                "Plan it, turn it round, watch it go and bring it home.",
                new[]
                {
                    ("1 · Plan",
                        "Select your aircraft (click it or its flight tile, or press [ or ]) and press PLAN FLIGHT, or "
                        + "open the Map with Tab. Pick a green destination and a time, then press PLAN FLIGHT again. "
                        + "Kingscote is a short first hop."),
                    ("2 · Turnaround",
                        "Fuel, catering, bags and boarding happen on their own before it leaves. The aircraft's card "
                        + "shows each one. Press F to follow it as it pushes back, taxis and takes off."),
                    ("3 · Away and back",
                        "Watch it on the Map while it is away. When it lands, pick a stand on its card. BEST is the "
                        + "shortest taxi. After 90 seconds the tower picks one for you."),
                    ("4 · Paid",
                        "The flight is paid the moment it parks. The money goes straight into your funds.")
                }),
            new FlightManualPage("money", "Money and contracts",
                "Every flight pays. Contracts pay extra, and that buys the next aircraft.",
                new[]
                {
                    ("What a flight earns",
                        "A flight costs money when you plan it and earns its fares when it comes home. The Map shows "
                        + "what it should earn first. A big aircraft on a quiet route can lose money."),
                    ("Contracts",
                        "A contract pays a bonus for each flight on its route and a reward when it is done. One at a "
                        + "time. ABANDON drops it for a little reliability."),
                    ("Charters, medical calls and freight",
                        "Charters are one well-paid flight within 6 hours. Medical calls go to country towns within 3 "
                        + "hours and lift reliability. Freight is for turboprops. Miss a deadline and you lose reliability."),
                    ("Busy days",
                        "Most days a festival, the school holidays or the footy brings extra passengers somewhere. The "
                        + "news says where, and the Map's forecast shows it."),
                    ("Out of money",
                        "If you can't afford a flight, a rescue contract to Kingscote pays for its own first flight.")
                }),
            new FlightManualPage("reliability", "Reliability",
                "Airlines that run on time get the good work. You start at 100%.",
                new[]
                {
                    ("Leaving on time",
                        "Push back within 2 minutes of the booked time for +1. Up to 5 minutes late costs nothing. Up "
                        + "to 15 minutes late costs 1, and any later costs 2. Contract flights can add their own bonus."),
                    ("Why was I late?",
                        "When a flight is paid, the message says how late it left and why. The reasons are a "
                        + "turnaround booked too tight, traffic on the apron or taxiway, someone on the lead-in, or a "
                        + "runway crossing. A waiting aircraft's card says what it is waiting for. Tap that line to "
                        + "jump to the aircraft in the way, and ‹ to come back."),
                    ("Checks",
                        $"Every aircraft needs a check every {Maintenance.IntervalRotations} flights. Use SEND FOR "
                        + "CHECK on its card or in Fleet. Every flight it makes while overdue costs reliability."),
                    ("Why it matters",
                        "Below 70% you only get regional offers, and flights earn less. Each tier needs a certain "
                        + "reliability.")
                }),
            new FlightManualPage("career", "Career",
                "Four tiers, then you become an established airline.",
                new[]
                {
                    ("The ring",
                        "The career ring (bottom left) shows how many of this tier's goals you have done and what the "
                        + "next tier unlocks. Click it to open the Career page."),
                    ("Goals",
                        "Each tier has a few goals: finish a contract, fly a number of flights, serve new places, "
                        + "grow your base, keep your reliability up. Do them in any order. PIN one to show it on the "
                        + "HUD."),
                    ("Tiers",
                        "Provisional, then Regional, Domestic and International. You earn them in order and never lose "
                        + "them. Each one opens new aircraft, base upgrades and routes."),
                    ("Challenges",
                        "Challenges pay cash for on-time streaks, a profitable day, charters and medical calls. Bigger "
                        + "ones open after the finale. See them on Stats. At 23:00 you get the day's report."),
                    ("The finale",
                        $"Finish every International goal to become an established airline: {CareerRoadmap.FinalFleet} "
                        + $"aircraft, three bases, {CareerRoadmap.FinalDestinations} destinations, a widebody and a "
                        + "profit. Then keep flying.")
                }),
            new FlightManualPage("growing", "Growing the airline",
                "More aircraft, a bigger base, other cities, and flights that repeat on their own.",
                new[]
                {
                    ("Aircraft",
                        "Buy them in Fleet: the ATR 42 and Dash 8-400, then jets from the E190 to the 737-8 and "
                        + "A321neo, then widebodies from the A330 to the A350. Older jets are cheaper to buy and "
                        + "dearer to fly. Each needs a tier, a reliability level and a number of flights. Selling one "
                        + "gets back "
                        + $"{(int)Math.Round(AirlineOperations.ResaleFraction * 100)}% of the price."),
                    ("Your Adelaide base",
                        "EXPAND BASE (Career › Airline) adds room for more aircraft, jet gates, your own maintenance "
                        + "and quicker turnarounds."),
                    ("Outstations",
                        "From the Domestic tier you can open bases in Melbourne, Sydney, Brisbane or Perth (Fleet › "
                        + "Network). Aircraft based there fly between other cities off the map."),
                    ("Repeat schedules",
                        "After 12 hand-planned flights, REPEAT keeps an aircraft flying a route every 6, 12 or 24 "
                        + "hours while you play.")
                }),
            new FlightManualPage(ControlsPageId, "Controls",
                "The mouse does everything. The keys are shortcuts.",
                Controls()),
            new FlightManualPage(CreditsPageId, "Map and data credits",
                "Adelaide is built from open data. These are its sources.",
                new[]
                {
                    ("Map", MapAttribution.OpenStreetMap + " (ODbL). The airport layout, coast, land use, "
                            + "every road and footpath, car parks and bays, buildings, masts, tanks, solar arrays "
                            + "and bus stops."),
                    ("Satellite imagery", MapAttribution.Sentinel + ". Nine clear summer passes, 2024 to 2026."),
                    ("Terrain", "Copernicus DEM GLO-30. © DLR e.V. 2010-2014 and © Airbus Defence and Space GmbH "
                                + "2014-2018 provided under COPERNICUS by the European Union and ESA. All rights "
                                + "reserved."),
                    ("Land cover", "ESA WorldCover 2021 (CC BY 4.0). © ESA WorldCover project 2021 / Contains modified "
                                   + "Copernicus Sentinel data (2021) processed by the ESA WorldCover consortium."),
                    ("Weather and live traffic", "Open-Meteo (CC BY 4.0) and adsb.lol (ODbL), credited on screen "
                                                 + "while they are in use.")
                })
        };

        public const string CreditsPageId = "credits";

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
                    lines.Add(binding.Key + "    " + binding.Action);
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
