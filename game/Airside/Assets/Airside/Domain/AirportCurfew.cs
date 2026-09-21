using System;

namespace Airside.Domain
{
    /// <summary>
    /// Adelaide Airport Curfew Act 2000: 23:00–06:00 local. Commercial AI follows it;
    /// the player and emergency RFDS flights do not.
    /// </summary>
    public static class AirportCurfew
    {
        public const int ClosedFromHour = 23;
        public const int OpensAtHour = 6;

        public static bool IsClosed(DateTime local) =>
            local.Hour >= ClosedFromHour || local.Hour < OpensAtHour;

        public static bool IsClosed(SimulationTime now, AirlineClock clock) =>
            IsClosed((clock ?? AirlineClock.Default).LocalAt(now));

        public static DateTime NextOpenLocal(DateTime local)
        {
            if (!IsClosed(local))
                return local;
            if (local.Hour >= ClosedFromHour)
                return local.Date.AddDays(1).AddHours(OpensAtHour);
            return local.Date.AddHours(OpensAtHour);
        }

        public static SimulationTime OpensAt(SimulationTime now, AirlineClock clock)
        {
            clock ??= AirlineClock.Default;
            var at = clock.AtLocal(NextOpenLocal(clock.LocalAt(now)));
            return at.CompareTo(now) > 0 ? at : now;
        }
    }
}
