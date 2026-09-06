using System;

namespace Airside.Simulation
{
    /// <summary>
    /// Forward-looking daily cash picture: expected operating cost under the
    /// current weather and staffing, versus expected commercial income if the
    /// airport keeps today's flight cadence with no delays. Pure function of
    /// live simulation inputs — identical under live play and offline catch-up.
    /// </summary>
    public readonly struct DailyFinanceBrief
    {
        public DailyFinanceBrief(long expectedOperatingCost, long expectedFlightIncome, long cashOnHand)
        {
            if (expectedOperatingCost < 0)
                throw new ArgumentOutOfRangeException(nameof(expectedOperatingCost));
            if (expectedFlightIncome < 0)
                throw new ArgumentOutOfRangeException(nameof(expectedFlightIncome));

            ExpectedOperatingCost = expectedOperatingCost;
            ExpectedFlightIncome = expectedFlightIncome;
            CashOnHand = cashOnHand;
        }

        public long ExpectedOperatingCost { get; }
        public long ExpectedFlightIncome { get; }
        public long ExpectedNet => ExpectedFlightIncome - ExpectedOperatingCost;
        public long CashOnHand { get; }

        /// <summary>
        /// Whole days of cash left at the current expected net burn. Null when
        /// the expected net is non-negative (runway is not meaningful).
        /// </summary>
        public int? CashRunwayDays
        {
            get
            {
                if (ExpectedNet >= 0)
                    return null;
                if (CashOnHand <= 0)
                    return 0;
                return (int)(CashOnHand / -ExpectedNet);
            }
        }
    }
}
