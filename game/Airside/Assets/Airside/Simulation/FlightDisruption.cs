using System;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// A published movement that does not run on time: a delay or a cancellation.
    /// Deterministic in the flight number, the scheduled second and the local hour,
    /// so the board, the sky and a reloaded save all agree.
    /// </summary>
    public readonly struct FlightDisruption
    {
        public static FlightDisruption None { get; } = new(false, 0, string.Empty);

        public FlightDisruption(bool cancelled, int delayMinutes, string reason)
        {
            Cancelled = cancelled;
            DelayMinutes = cancelled ? 0 : Math.Max(0, delayMinutes);
            Reason = reason ?? string.Empty;
        }

        public bool Cancelled { get; }
        public int DelayMinutes { get; }
        public string Reason { get; }
        public bool Delayed => !Cancelled && DelayMinutes > 0;

        public string BoardLabel =>
            Cancelled ? "Cancelled" :
            Delayed ? $"Delayed +{DelayMinutes}" :
            string.Empty;

        public static FlightDisruption For(string flightNumber, SimulationTime scheduled, AirlineClock clock)
        {
            clock ??= AirlineClock.Default;
            var hour = clock.LocalAt(scheduled).Hour;
            var density = AdelaideHourProfile.Density(hour);
            var hash = Hash(flightNumber, scheduled.ElapsedSeconds);
            var unit = (hash % 10000) / 10000.0;
            var cancelChance = 0.012 + density * 0.028;
            var delayChance = 0.05 + density * 0.09;
            var reason = ReasonFrom(hash);
            if (unit < cancelChance)
                return new FlightDisruption(true, 0, reason);
            if (unit < cancelChance + delayChance)
                return new FlightDisruption(false, 8 + (int)((hash / 13) % 38), reason);
            return None;
        }

        public static string ReasonFrom(int hash) => (Math.Abs(hash) % 4) switch
        {
            0 => "weather",
            1 => "traffic",
            2 => "crew",
            _ => "technical"
        };

        private static int Hash(string flightNumber, long scheduledSeconds)
        {
            unchecked
            {
                var hash = 17;
                if (flightNumber != null)
                    foreach (var ch in flightNumber)
                        hash = hash * 31 + ch;
                hash = hash * 31 + (int)scheduledSeconds;
                return hash & int.MaxValue;
            }
        }
    }
}
