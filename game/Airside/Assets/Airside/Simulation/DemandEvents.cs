using System;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>Somewhere busier than usual for one Adelaide day (ADR 0127).</summary>
    public readonly struct DemandEvent
    {
        public DemandEvent(long day, string headline, string[] codes, double multiplier)
        {
            Day = day;
            Headline = headline;
            Codes = codes;
            Multiplier = multiplier;
        }

        /// <summary>Adelaide local day number the event runs on.</summary>
        public long Day { get; }
        public string Headline { get; }
        public string[] Codes { get; }
        public double Multiplier { get; }

        public bool Affects(string code) => Array.IndexOf(Codes, code) >= 0;
    }

    /// <summary>
    /// ADR 0127 — the market moves. On a little over half of Adelaide days something draws extra
    /// passengers to a few destinations: festivals, school holidays, finals, a mining boom. Demand
    /// there is multiplied for that local day, the forecast shows it, and settlement pays it. A pure
    /// function of the date, so nothing is saved and every replay agrees.
    /// </summary>
    public static class DemandEvents
    {
        public const int ChancePercent = 55;

        private static readonly (string Headline, string[] Codes, double Multiplier)[] Templates =
        {
            ("Adelaide Fringe — interstate visitors pour in", new[] { "MEL", "SYD", "BNE" }, 1.35),
            ("School holidays — families head for the coast", new[] { "KGC", "PLO", "OOL" }, 1.5),
            ("Mining boom — fly-in crews needed up north", new[] { "CPD", "WYA", "BHQ", "PER" }, 1.45),
            ("AFL finals — footy fans on the move", new[] { "MEL", "PER" }, 1.5),
            ("Kangaroo Island food and wine festival", new[] { "KGC" }, 1.9),
            ("Mount Gambier show weekend", new[] { "MGB", "MQL" }, 1.7),
            ("Tasman rugby tour", new[] { "AKL", "CHC" }, 1.45),
            ("Singapore airshow week", new[] { "SIN", "KUL" }, 1.4),
            ("Outback music festival", new[] { "CPD", "ASP", "CED" }, 1.6),
            ("Canberra budget week", new[] { "CBR", "SYD" }, 1.4),
            ("Bali school-holiday rush", new[] { "DPS", "PER" }, 1.45),
            ("Tasmania winter feast", new[] { "HBA", "MEL" }, 1.45)
        };

        private static readonly DateTime Epoch = new(2020, 1, 1);

        public static long DayOf(SimulationTime now, AirlineClock clock) =>
            (long)((clock ?? AirlineClock.Default).LocalAt(now).Date - Epoch).TotalDays;

        public static DemandEvent? OnDay(long day)
        {
            var rng = new SeededRandomSource((uint)(day * 2_654_435_761L % 4_294_967_291L + 11));
            if (rng.NextInt(0, 100) >= ChancePercent)
                return null;
            var template = Templates[rng.NextInt(0, Templates.Length)];
            return new DemandEvent(day, template.Headline, template.Codes, template.Multiplier);
        }

        public static DemandEvent? At(SimulationTime now, AirlineClock clock) => OnDay(DayOf(now, clock));

        public static double MultiplierFor(string code, SimulationTime now, AirlineClock clock)
        {
            var today = At(now, clock);
            return today.HasValue && today.Value.Affects(code) ? today.Value.Multiplier : 1.0;
        }
    }
}
