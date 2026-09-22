using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// One local day's career service pattern for the current campaign chapter (ADR 0102).
    /// Derived from settlement keys — no save schema of its own.
    /// </summary>
    public readonly struct DailyServicePattern
    {
        internal DailyServicePattern(bool active, int chapter, string label, int required, int done,
            long bonus, bool bonusPaid, string dateKey)
        {
            Active = active;
            Chapter = chapter;
            Label = label ?? string.Empty;
            Required = Math.Max(0, required);
            Done = Math.Max(0, done);
            Bonus = Math.Max(0, bonus);
            BonusPaid = bonusPaid;
            DateKey = dateKey ?? string.Empty;
        }

        public bool Active { get; }
        public int Chapter { get; }
        public string Label { get; }
        public int Required { get; }
        public int Done { get; }
        public long Bonus { get; }
        public bool BonusPaid { get; }
        public string DateKey { get; }

        public bool Complete => Active && Required > 0 && Done >= Required;

        /// <summary>"TODAY · Kingscote 1/2" — the objective card's session line.</summary>
        public string Line => Active
            ? $"TODAY · {Label} {Math.Min(Done, Required)}/{Required}"
            : string.Empty;

        public static DailyServicePattern Inactive => new(false, 0, string.Empty, 0, 0, 0, false, string.Empty);
    }

    /// <summary>
    /// Session-level "fly this today" pattern per campaign chapter (ADR 0102). Counts
    /// qualifying player rotations against once-per-local-day settlement keys; completing
    /// the pattern pays a small bonus (and +1 reliability) at most once that day.
    /// Incomplete at the end of the day pays nothing — the next local date resets the count.
    /// </summary>
    public static class DailyService
    {
        public const int ReliabilityBonus = 1;

        public static string HopKey(string dateKey, int chapter, int index) =>
            $"dailyservice:{dateKey}:{chapter}:{index}";

        public static string BonusKey(string dateKey, int chapter) =>
            $"dailyservice-bonus:{dateKey}:{chapter}";

        public static string DateKey(AirlineClock clock, SimulationTime now)
        {
            clock ??= AirlineClock.Default;
            return clock.LocalAt(now).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        public static DailyServicePattern Evaluate(AirlineCareerState career,
            IReadOnlyList<AircraftType> ownedTypes, AirlineClock clock, SimulationTime now)
        {
            if (career == null)
                return DailyServicePattern.Inactive;

            ownedTypes ??= Array.Empty<AircraftType>();
            var chapter = Campaign.Current(Campaign.Evaluate(career, ownedTypes));
            if (chapter == null)
                return DailyServicePattern.Inactive;

            if (!TrySpec(chapter.Number, ownedTypes, out var label, out var required, out var bonus))
                return DailyServicePattern.Inactive;

            var date = DateKey(clock, now);
            var done = CountHops(career, date, chapter.Number);
            var paid = career.ProcessedSettlementKeys.Contains(BonusKey(date, chapter.Number));
            return new DailyServicePattern(true, chapter.Number, label, required, done, bonus, paid, date);
        }

        /// <summary>
        /// Records a qualifying hop and, when the day's pattern is complete, pays the bonus
        /// once. Returns true when a key or funds change.
        /// </summary>
        public static bool TryRecordAndAward(AirlineCareerState career, IReadOnlyList<AircraftType> ownedTypes,
            Destination destination, AirlineClock clock, SimulationTime now)
        {
            if (career == null)
                return false;

            var pattern = Evaluate(career, ownedTypes, clock, now);
            if (!pattern.Active || pattern.Complete)
                return false;
            if (!Qualifies(pattern.Chapter, destination, career))
                return false;

            var next = pattern.Done + 1;
            if (!career.TryAward(HopKey(pattern.DateKey, pattern.Chapter, next), 0))
                return false;

            if (next < pattern.Required)
                return true;

            var paid = career.TryAward(BonusKey(pattern.DateKey, pattern.Chapter), pattern.Bonus);
            if (paid)
                career.ApplyPunctuality(ReliabilityBonus);
            return true;
        }

        public static bool Qualifies(int chapter, Destination destination, AirlineCareerState career)
        {
            if (career == null || string.IsNullOrEmpty(destination.Code))
                return false;

            var code = destination.Code;
            switch (chapter)
            {
                case 1:
                    return string.Equals(code, "KGC", StringComparison.OrdinalIgnoreCase);
                case 2:
                {
                    if (RouteAccess.BandOf(destination) != RouteBand.Regional)
                        return false;
                    var fulfilled = Campaign.FulfilledDestinationCodes(career);
                    // Prefer a town the chapter has not counted yet; once two towns are done,
                    // any regional hop still keeps the day pattern reachable.
                    return !fulfilled.Contains(code) || fulfilled.Count >= 2;
                }
                case 3:
                    return RouteAccess.BandOf(destination) == RouteBand.Regional;
                case 4:
                    return Campaign.IsInterstate(code);
                case 5:
                    return Campaign.IsInternational(code);
                default:
                    return false;
            }
        }

        private static bool TrySpec(int chapter, IReadOnlyList<AircraftType> ownedTypes,
            out string label, out int required, out long bonus)
        {
            label = string.Empty;
            required = 0;
            bonus = 0;
            switch (chapter)
            {
                case 1:
                    label = "Kingscote";
                    required = 2;
                    bonus = 200;
                    return true;
                case 2:
                    label = "new regional town";
                    required = 1;
                    bonus = 350;
                    return true;
                case 3:
                    label = "regional";
                    required = 3;
                    bonus = 500;
                    return true;
                case 4:
                    if (!AirlineCareerState.OwnsAnyJet(ownedTypes))
                        return false;
                    label = "interstate";
                    required = 1;
                    bonus = 800;
                    return true;
                case 5:
                    if (!OwnsWidebody(ownedTypes))
                        return false;
                    label = "international";
                    required = 1;
                    bonus = 1_200;
                    return true;
                default:
                    return false;
            }
        }

        private static int CountHops(AirlineCareerState career, string dateKey, int chapter)
        {
            var prefix = $"dailyservice:{dateKey}:{chapter}:";
            var count = 0;
            foreach (var key in career.ProcessedSettlementKeys)
            {
                if (key == null || !key.StartsWith(prefix, StringComparison.Ordinal))
                    continue;
                // Hop keys only — never the bonus key (different prefix).
                count++;
            }

            return count;
        }

        private static bool OwnsWidebody(IReadOnlyList<AircraftType> ownedTypes)
        {
            if (ownedTypes == null)
                return false;
            foreach (var type in ownedTypes)
                if (Campaign.IsWidebody(type))
                    return true;
            return false;
        }
    }
}
