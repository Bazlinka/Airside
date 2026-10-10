using System;

namespace Airside.Domain
{
    /// <summary>
    /// Economy v2 lease terms (ADR 2026-10-09-economy-v2-real-dollar-scale): what an aircraft is worth, and so what a lease
    /// deposit and a monthly lease cost. Market values are tier D design values (docs/data/AIRLINE_OPERATING_COSTS.md §4);
    /// they live in Domain so the aircraft price list can use them without depending on Simulation.
    /// </summary>
    public static class LeaseTerms
    {
        /// <summary>Monthly lease as a share of market value, and the deposit in months of lease.</summary>
        public const double RatePerMonth = 0.009;
        public const int DepositMonths = 3;

        /// <summary>Approximate market value in Australian dollars (design).</summary>
        public static long ValueAud(AircraftType type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            return type.Id switch
            {
                "SF34" => 2_000_000,
                "ATR42" => 10_000_000,
                "DH8D" => 18_000_000,
                "B412" => 7_000_000,
                "E190" => 25_000_000,
                "A223" => 45_000_000,
                "A320" => 28_000_000,
                "B738" => 32_000_000,
                "B38M" => 75_000_000,
                "A21N" => 85_000_000,
                "A339" => 140_000_000,
                "B789" => 170_000_000,
                "B78X" => 190_000_000,
                "A359" => 200_000_000,
                _ => throw new ArgumentException($"{type.Id} has no market value.", nameof(type))
            };
        }

        /// <summary>The upfront deposit to lease one aircraft of the type: <see cref="DepositMonths"/> months of lease.</summary>
        public static long Deposit(AircraftType type) =>
            (long)Math.Round(ValueAud(type) * RatePerMonth * DepositMonths);
    }
}
