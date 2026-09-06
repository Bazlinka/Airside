using System;

namespace Airside.Simulation
{
    /// <summary>
    /// How much airlines trust the airport. Derived entirely from completed
    /// departures — on-time flights raise it, delayed flights lower it in
    /// proportion to the delay — so it is reconstructed exactly by replay and needs
    /// no persisted field of its own. Airlines gate their route proposals on it and
    /// pay more when it is high.
    /// </summary>
    public sealed class AirportReputation
    {
        public const int Starting = 50;
        public const int Minimum = 0;
        public const int Maximum = 100;

        public int Score { get; private set; } = Starting;
        public int OnTimeDepartures { get; private set; }
        public int DelayedDepartures { get; private set; }

        public void RecordDeparture(long delaySeconds)
        {
            if (delaySeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(delaySeconds));

            if (delaySeconds == 0)
            {
                Score = Math.Min(Maximum, Score + 2);
                OnTimeDepartures++;
            }
            else
            {
                var drop = (int)Math.Min(8, 2 + delaySeconds / 4);
                Score = Math.Max(Minimum, Score - drop);
                DelayedDepartures++;
            }
        }

        /// <summary>Extra income per flight a route pays when accepted at this reputation.</summary>
        public long IncomeBonus => Math.Max(0, Score - Starting) * 3L;

        public string Band =>
            Score >= 78 ? "Trusted" :
            Score >= 45 ? "Established" :
            Score >= 20 ? "Provisional" :
            "At risk";
    }
}
