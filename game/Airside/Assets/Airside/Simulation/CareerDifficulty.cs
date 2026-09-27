using System;
using System.Collections.Generic;

namespace Airside.Simulation
{
    /// <summary>How forgiving the airline business is, chosen once when the airline is founded (ADR 0123).</summary>
    public enum CareerDifficulty
    {
        Relaxed,
        Standard,
        Demanding
    }

    /// <summary>
    /// The economic dials a difficulty turns. Contract terms are never scaled — a contract pays
    /// exactly what it advertises — only the airline's own float, route revenue, dispatch cost
    /// and how hard lateness and overdue checks hit reliability.
    /// </summary>
    public readonly struct DifficultyProfile
    {
        public DifficultyProfile(CareerDifficulty difficulty, string title, string summary, long startingFunds,
            double revenueMultiplier, double costMultiplier, double penaltyMultiplier)
        {
            Difficulty = difficulty;
            Title = title;
            Summary = summary;
            StartingFunds = startingFunds;
            RevenueMultiplier = revenueMultiplier;
            CostMultiplier = costMultiplier;
            PenaltyMultiplier = penaltyMultiplier;
        }

        public CareerDifficulty Difficulty { get; }
        public string Title { get; }
        public string Summary { get; }
        public long StartingFunds { get; }

        /// <summary>Scales every route's ticket revenue (not contract bonuses).</summary>
        public double RevenueMultiplier { get; }

        /// <summary>Scales every dispatch cost.</summary>
        public double CostMultiplier { get; }

        /// <summary>Scales reliability losses from late pushbacks and overdue checks (never gains).</summary>
        public double PenaltyMultiplier { get; }

        public long ScaleRevenue(long revenue) => Math.Max(0, (long)Math.Round(revenue * RevenueMultiplier));

        public long ScaleCost(long cost) => cost <= 0 ? cost : Math.Max(1, (long)Math.Round(cost * CostMultiplier));

        /// <summary>
        /// A negative reliability change after scaling. Relaxed halves toward zero (a −1 is forgiven);
        /// Demanding rounds away from zero (−1 becomes −2). Gains are never scaled.
        /// </summary>
        public int ScalePenalty(int delta)
        {
            if (delta >= 0)
                return delta;
            var scaled = delta * PenaltyMultiplier;
            return PenaltyMultiplier < 1.0 ? (int)Math.Ceiling(scaled) : (int)Math.Floor(scaled);
        }

        /// <summary>Plain-language effects for the setup card.</summary>
        public IReadOnlyList<string> Effects => new[]
        {
            $"${StartingFunds:N0} starting float",
            RevenueMultiplier > 1.0 ? $"Fares +{Math.Round((RevenueMultiplier - 1) * 100)}%"
                : RevenueMultiplier < 1.0 ? $"Fares −{Math.Round((1 - RevenueMultiplier) * 100)}%" : "Standard fares",
            CostMultiplier < 1.0 ? $"Flight costs −{Math.Round((1 - CostMultiplier) * 100)}%"
                : CostMultiplier > 1.0 ? $"Flight costs +{Math.Round((CostMultiplier - 1) * 100)}%" : "Standard flight costs",
            PenaltyMultiplier < 1.0 ? "Lateness is forgiven more easily"
                : PenaltyMultiplier > 1.0 ? "Lateness and overdue checks hurt more" : "Standard reliability penalties"
        };
    }

    public static class Difficulty
    {
        public static readonly DifficultyProfile Relaxed = new(CareerDifficulty.Relaxed, "Relaxed",
            "Room to learn. A bigger float and kinder margins.", 6_000, 1.20, 0.85, 0.5);

        public static readonly DifficultyProfile Standard = new(CareerDifficulty.Standard, "Standard",
            "The intended airline business. Every flight counts.", FlightEconomics.StartingFunds, 1.0, 1.0, 1.0);

        public static readonly DifficultyProfile Demanding = new(CareerDifficulty.Demanding, "Demanding",
            "Thin margins and unforgiving punctuality.", 2_000, 0.90, 1.10, 1.5);

        public static IReadOnlyList<DifficultyProfile> All { get; } = new[] { Relaxed, Standard, Demanding };

        public static DifficultyProfile For(CareerDifficulty difficulty) => difficulty switch
        {
            CareerDifficulty.Relaxed => Relaxed,
            CareerDifficulty.Demanding => Demanding,
            _ => Standard
        };
    }
}
