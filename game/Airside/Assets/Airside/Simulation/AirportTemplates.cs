using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// A generic runway, terminal and gate template for each Australian airport in
    /// <see cref="DestinationCatalogue.Australia"/> (ADR 2026-10-08-airport-templates), so every flight has a runway to land on
    /// and a gate to park at. Figures come from docs/data/AUSTRALIAN_AIRPORTS_RUNWAYS_TERMINALS_GATES.md. Where an airport
    /// publishes no gate count the template uses a small generic count marked <see cref="FactBasis.Generic"/>. Adelaide's
    /// gates are its real stands; the detailed Adelaide scene still decides what happens there.
    /// </summary>
    public static class AirportTemplates
    {
        private static readonly Dictionary<string, AirportTemplate> ByIata = Build();

        public static IReadOnlyCollection<AirportTemplate> All => ByIata.Values;

        public static bool TryFor(string iata, out AirportTemplate template)
        {
            template = null;
            return iata != null && ByIata.TryGetValue(iata, out template);
        }

        // ---- builders -----------------------------------------------------------------------------------------

        private static RunwayTemplate Rwy(string id, double metres, bool sealedSurface = true, char softMax = 'F') =>
            new(id, metres, sealedSurface, softMax);

        private static List<GateTemplate> Gates(string terminal, int first, int count, char code, bool aerobridge,
            GateUse use, FactBasis basis, string prefix = null)
        {
            var list = new List<GateTemplate>(count);
            for (var i = 0; i < count; i++)
                list.Add(new GateTemplate($"{prefix ?? terminal}-{first + i:00}", terminal, code, aerobridge, use, basis));
            return list;
        }

        private static TerminalTemplate Terminal(string id, string name, params List<GateTemplate>[] groups)
        {
            var all = new List<GateTemplate>();
            foreach (var group in groups) all.AddRange(group);
            return new TerminalTemplate(id, name, all);
        }

        /// <summary>A small regional airport: one terminal, a few bays, nothing the source contradicts.</summary>
        private static AirportTemplate Regional(string iata, string icao, string name, string state, string calmEnd,
            int bays, params RunwayTemplate[] runways) =>
            new(iata, icao, name, state, runways, calmEnd,
                new[] { Terminal("T1", "Terminal", Gates("T1", 1, bays, 'C', false, GateUse.Domestic, FactBasis.Generic)) },
                "Runways from Wikipedia; bay count is a generic placeholder (no figure published).");

        private static Dictionary<string, AirportTemplate> Build()
        {
            const FactBasis S = FactBasis.Sourced, I = FactBasis.Inferred, G = FactBasis.Generic;
            const GateUse Dom = GateUse.Domestic, Intl = GateUse.International, Swing = GateUse.Swing;
            var all = new List<AirportTemplate>
            {
                Adelaide(),

                new("MEL", "YMML", "Melbourne", "VIC",
                    new[] { Rwy("16/34", 3657), Rwy("09/27", 2286) }, "16",
                    new[]
                    {
                        Terminal("T1", "Terminal 1 (Qantas)", Gates("T1", 1, 16, 'C', true, Dom, S), Gates("T1", 17, 5, 'C', false, Dom, S)),
                        Terminal("T2", "Terminal 2 (International)", Gates("T2", 1, 20, 'E', true, Intl, S)),
                        Terminal("T3", "Terminal 3 (Virgin Australia)", Gates("T3", 1, 11, 'C', true, Dom, S), Gates("T3", 12, 8, 'C', false, Dom, S)),
                        Terminal("T4", "Terminal 4 (Low-cost)", Gates("T4", 41, 12, 'C', false, Dom, I, "T4"))
                    },
                    "Terminal gate counts from Wikipedia (its 68-gate total conflicts with them); T4 is gates 41-52 from a photo caption."),

                new("SYD", "YSSY", "Sydney", "NSW",
                    new[] { Rwy("16R/34L", 3963), Rwy("07/25", 2530, true, 'D'), Rwy("16L/34R", 2438, true, 'D') }, "16",
                    new[]
                    {
                        Terminal("T1", "Terminal 1 (International)", Gates("T1", 1, 25, 'E', true, Intl, S)),
                        Terminal("T2", "Terminal 2 (Domestic)", Gates("T2", 1, 20, 'C', true, Dom, S), Gates("T2", 21, 6, 'B', false, Dom, G)),
                        Terminal("T3", "Terminal 3 (Domestic)", Gates("T3", 1, 14, 'C', true, Dom, S))
                    },
                    "Runway usage from Wikipedia (07/25 and 16L/34R steer widebodies away); T2 regional bays are generic."),

                new("BNE", "YBBN", "Brisbane", "QLD",
                    new[] { Rwy("01R/19L", 3560), Rwy("01L/19R", 3300) }, "01",
                    new[]
                    {
                        Terminal("INT", "International Terminal", Gates("INT", 1, 4, 'F', true, Intl, S), Gates("INT", 5, 10, 'E', true, Intl, S)),
                        Terminal("DOM", "Domestic Terminal", Gates("DOM", 1, 9, 'C', true, Dom, S),
                            Gates("DOM", 10, 9, 'C', true, Dom, S), Gates("DOM", 19, 2, 'C', false, Dom, S))
                    },
                    "Bay counts from Wikipedia; which international bays take an A380 is given (4)."),

                new("PER", "YPPH", "Perth", "WA",
                    new[] { Rwy("03/21", 3444), Rwy("06/24", 2163) }, "03",
                    new[]
                    {
                        Terminal("T1", "Terminal 1 (International and Virgin)", Gates("T1", 1, 5, 'E', true, Intl, S),
                            Gates("T1", 6, 2, 'E', false, Intl, S), Gates("T1", 11, 8, 'C', false, Dom, S)),
                        Terminal("T2", "Terminal 2 (Regional)", Gates("T2", 1, 14, 'B', false, Dom, S)),
                        Terminal("T3", "Terminal 3 (Qantas)", Gates("T3", 1, 5, 'C', true, Dom, S), Gates("T3", 6, 4, 'C', false, Dom, S)),
                        Terminal("T4", "Terminal 4 (Qantas)", Gates("T4", 1, 4, 'C', true, Dom, S), Gates("T4", 5, 5, 'C', false, Dom, S))
                    },
                    "Gate and jetway counts from Wikipedia."),

                new("CBR", "YSCB", "Canberra", "ACT",
                    new[] { Rwy("17/35", 3283), Rwy("12/30", 1679) }, "17",
                    new[]
                    {
                        Terminal("SOUTH", "Southern Concourse (Qantas)", Gates("SOUTH", 1, 5, 'C', true, Dom, G)),
                        Terminal("WEST", "Western Concourse (Virgin)", Gates("WEST", 1, 4, 'C', true, Dom, G), Gates("WEST", 5, 1, 'C', true, Swing, S))
                    },
                    "Concourses and the international gate 5 from Wikipedia; other gate counts are generic."),

                new("HBA", "YMHB", "Hobart", "TAS",
                    new[] { Rwy("12/30", 2727) }, "12",
                    new[] { Terminal("T1", "Terminal", Gates("T1", 1, 5, 'C', false, Dom, I), Gates("T1", 6, 1, 'C', false, Swing, S), Gates("T1", 7, 3, 'C', false, Dom, I)) },
                    "Six narrow-body bays plus three; gate 6 is the international swing gate."),

                new("ASP", "YBAS", "Alice Springs", "NT",
                    new[] { Rwy("12/30", 2438), Rwy("17/35", 1133) }, "12",
                    new[] { Terminal("T1", "Terminal", Gates("T1", 1, 9, 'C', false, Dom, S)) },
                    "Nine commercial parking positions from Wikipedia; passengers walk."),

                new("OOL", "YBCG", "Gold Coast", "QLD",
                    new[] { Rwy("14/32", 2492), Rwy("17/35", 582) }, "14",
                    new[]
                    {
                        Terminal("T1", "Terminal 1", Gates("T1", 1, 4, 'C', true, Dom, S), Gates("T1", 5, 2, 'E', false, Intl, S),
                            Gates("T1", 7, 13, 'C', false, Dom, I))
                    },
                    "4 aerobridges and 2 wide-body stands from Wikipedia; the rest of its 19-aircraft capacity is inferred."),

                new("CNS", "YBCS", "Cairns", "QLD",
                    new[] { Rwy("15/33", 3156) }, "15",
                    new[]
                    {
                        Terminal("INT", "International Terminal", Gates("INT", 1, 6, 'E', true, Intl, S), Gates("INT", 7, 4, 'E', false, Intl, S)),
                        Terminal("DOM", "Domestic Terminal", Gates("DOM", 1, 5, 'C', true, Dom, S), Gates("DOM", 6, 12, 'C', false, Dom, S))
                    },
                    "Gate and jet-bridge counts from Wikipedia."),

                new("DRW", "YPDN", "Darwin", "NT",
                    new[] { Rwy("11/29", 3354), Rwy("18/36", 1524) }, "11",
                    new[]
                    {
                        Terminal("INT", "International", Gates("INT", 1, 6, 'E', true, Intl, G)),
                        Terminal("DOM", "Domestic", Gates("DOM", 1, 8, 'C', true, Dom, G))
                    },
                    "Runways from Wikipedia; its sources disagree on terminal layout and give no counts, so gates are generic."),

                Regional("MQL", "YMIA", "Mildura", "VIC", "09", 3, Rwy("09/27", 1830), Rwy("18/36", 1139)),
                Regional("KGC", "YKSC", "Kingscote", "SA", "01", 2, Rwy("01/19", 1402), Rwy("06/24", 1134, false), Rwy("15/33", 1164, false)),
                Regional("PLO", "YPLC", "Port Lincoln", "SA", "01", 3, Rwy("01/19", 1499), Rwy("15/33", 1450, false), Rwy("05/23", 1275, false)),
                Regional("MGB", "YMTG", "Mount Gambier", "SA", "18", 3, Rwy("18/36", 1644), Rwy("11/29", 922), Rwy("06/24", 846)),
                Regional("CED", "YCDU", "Ceduna", "SA", "11", 2, Rwy("11/29", 1740), Rwy("17/35", 1014, false)),
                Regional("WYA", "YWHA", "Whyalla", "SA", "17", 2, Rwy("17/35", 1686), Rwy("05/23", 1500)),
                Regional("CPD", "YCBP", "Coober Pedy", "SA", "04", 2, Rwy("04/22", 1428), Rwy("14/32", 829, false)),
                Regional("BHQ", "YBHI", "Broken Hill", "NSW", "05", 2, Rwy("05/23", 2515), Rwy("14/32", 1000, false))
            };

            var map = new Dictionary<string, AirportTemplate>(StringComparer.Ordinal);
            foreach (var template in all)
            {
                // Real gate numbers where OpenStreetMap has them (scripts/generate-airport-gates.py).
                if (template.Iata != "ADL" && AirportGateData.TryFor(template.Icao, out var mapped))
                    map[template.Iata] = new AirportTemplate(template.Iata, template.Icao, template.Name, template.State,
                        template.Runways, template.CalmWindRunwayEnd, mapped,
                        "Runways from Wikipedia; gate numbers and terminal membership from OpenStreetMap (ODbL). Aerobridge and size are inferred from the terminal's role.");
                else
                    map[template.Iata] = template;
            }

            return map;
        }

        /// <summary>Adelaide: the real runways and the same bays and gates the detailed scene reserves.</summary>
        private static AirportTemplate Adelaide()
        {
            var gates = new List<GateTemplate>();
            foreach (var bay in AdelaideLayout.Bays)
                gates.Add(new GateTemplate(bay.Id, "T1", 'C', false, GateUse.Domestic, FactBasis.Sourced));
            foreach (var gate in AdelaideLayout.TerminalGates)
            {
                var codeE = false;
                foreach (var id in AirlineOperations.CodeEGates)
                    if (id.Value == gate.Id) { codeE = true; break; }
                gates.Add(new GateTemplate(gate.Id, "T1", codeE ? 'E' : 'C', true, GateUse.Swing, FactBasis.Sourced));
            }

            return new AirportTemplate("ADL", "YPAD", "Adelaide", "SA",
                new[] { new RunwayTemplate("05/23", 3100, true), new RunwayTemplate("12/30", 1652, true, 'B') }, "05",
                new[] { new TerminalTemplate("T1", "Terminal", gates) },
                "Real stands from AdelaideLayout (OSM); 12/30 stays a turboprop strip in the detailed scene.");
        }
    }
}
