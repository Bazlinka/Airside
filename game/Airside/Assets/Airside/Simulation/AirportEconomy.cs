using System;

namespace Airside.Simulation
{
    public sealed class AirportEconomy
    {
        public const long StartingCash = 25000;
        public const long PriorityCrewCost = 300;
        public const long TurnaroundRevenue = 1200;
        public const long DelayCostPerSecond = 40;
        public const int InsolvencyConsecutiveDays = 3;

        public AirportEconomy()
        {
            Cash = StartingCash;
        }

        public long Cash { get; private set; }
        public long TotalRevenue { get; private set; }
        public long TotalDelayCost { get; private set; }
        public long TotalRouteIncome { get; private set; }
        public long TotalGeneralAviationIncome { get; private set; }
        public long TotalOperatingCost { get; private set; }
        public int ConsecutiveNegativeDays { get; private set; }
        public bool IsInsolvent { get; private set; }

        public bool PurchasePriorityCrew() => TrySpend(PriorityCrewCost);

        public bool TrySpend(long amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
            if (Cash < amount)
                return false;

            Cash -= amount;
            return true;
        }

        public void PayOperatingCosts(long amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount));

            Cash -= amount;
            TotalOperatingCost += amount;
        }

        public void AddRouteIncome(long amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount));

            Cash += amount;
            TotalRevenue += amount;
            TotalRouteIncome += amount;
        }

        public void AddGeneralAviationIncome(long amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount));

            Cash += amount;
            TotalRevenue += amount;
            TotalGeneralAviationIncome += amount;
        }

        public void CompleteFlight(long delaySeconds)
        {
            if (delaySeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(delaySeconds));

            var delayCost = delaySeconds * DelayCostPerSecond;
            Cash += TurnaroundRevenue - delayCost;
            TotalRevenue += TurnaroundRevenue;
            TotalDelayCost += delayCost;
        }

        /// <summary>
        /// Called once per simulated day close. Three consecutive closes with a
        /// negative cash balance declare the airport insolvent.
        /// </summary>
        public void EvaluateDayEndSolvency()
        {
            if (IsInsolvent)
                return;

            if (Cash < 0)
            {
                ConsecutiveNegativeDays++;
                if (ConsecutiveNegativeDays >= InsolvencyConsecutiveDays)
                    IsInsolvent = true;
            }
            else
            {
                ConsecutiveNegativeDays = 0;
            }
        }
    }
}
