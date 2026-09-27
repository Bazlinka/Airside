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
            ("The Adelaide Fringe is on", new[] { "MEL", "SYD", "BNE" }, 1.35),
            ("School holidays have started", new[] { "KGC", "PLO", "OOL" }, 1.5),
            ("The mines up north need fly-in crews", new[] { "CPD", "WYA", "BHQ", "PER" }, 1.45),
            ("It's AFL finals week", new[] { "MEL", "PER" }, 1.5),
            ("Kangaroo Island has its food and wine festival", new[] { "KGC" }, 1.9),
            ("It's show weekend in Mount Gambier", new[] { "MGB", "MQL" }, 1.7),
            ("A rugby tour is crossing the Tasman", new[] { "AKL", "CHC" }, 1.45),
            ("It's airshow week in Singapore", new[] { "SIN", "KUL" }, 1.4),
            ("An outback music festival is on", new[] { "CPD", "ASP", "CED" }, 1.6),
            ("It's budget week in Canberra", new[] { "CBR", "SYD" }, 1.4),
            ("Families are heading to Bali", new[] { "DPS", "PER" }, 1.45),
            ("Tasmania's winter feast is on", new[] { "HBA", "MEL" }, 1.45)
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
