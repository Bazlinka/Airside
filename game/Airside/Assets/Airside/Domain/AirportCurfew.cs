using System;

namespace Airside.Domain
{
    /// <summary>
    /// Adelaide's commercial operating day: first flight 05:00, last flight 23:00 local
    /// (ADR 0110 — Bailey's call, an hour earlier than the Curfew Act 2000's 06:00 so the
    /// first wave of night-stopped aircraft goes out at dawn). A movement scheduled for
    /// exactly 23:00 still goes; the field is closed from 23:00:01 to 04:59:59.
    /// Commercial AI follows it; the player and emergency RFDS flights do not.
    /// </summary>
    public static class AirportCurfew
    {
        public const int ClosedFromHour = 23;
        public const int OpensAtHour = 5;

        /// <summary>Minutes past midnight of the last commercial movement (23:00).</summary>
        public const int LastMovementMinute = ClosedFromHour * 60;

        public static bool IsClosed(DateTime local) =>
            local.TimeOfDay > TimeSpan.FromMinutes(LastMovementMinute) || local.Hour < OpensAtHour;

        public static bool IsClosed(SimulationTime now, AirlineClock clock) =>
            IsClosed((clock ?? AirlineClock.Default).LocalAt(now));

        public static DateTime NextOpenLocal(DateTime local)
        {
            if (!IsClosed(local))
                return local;
            if (local.Hour >= OpensAtHour)
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
