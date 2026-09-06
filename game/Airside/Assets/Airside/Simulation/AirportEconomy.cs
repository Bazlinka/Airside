using System;

namespace Airside.Simulation
{
    public sealed class AirportEconomy
    {
        public const long StartingCash = 25000;
        public const long PriorityCrewCost = 300;
        public const long TurnaroundRevenue = 1200;
        public const long DelayCostPerSecond = 40;

        public AirportEconomy()
        {
            Cash = StartingCash;
        }

        public long Cash { get; private set; }
        public long TotalRevenue { get; private set; }
        public long TotalDelayCost { get; private set; }
        public long TotalRouteIncome { get; private set; }
        public long TotalOperatingCost { get; private set; }

        public bool PurchasePriorityCrew()
        {
            if (Cash < PriorityCrewCost)
                return false;

            Cash -= PriorityCrewCost;
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

        public void CompleteFlight(long delaySeconds)
        {
            if (delaySeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(delaySeconds));

            var delayCost = delaySeconds * DelayCostPerSecond;
            Cash += TurnaroundRevenue - delayCost;
            TotalRevenue += TurnaroundRevenue;
            TotalDelayCost += delayCost;
        }
    }
}
