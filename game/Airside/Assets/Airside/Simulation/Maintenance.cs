using System;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// Routine checks (ADR 0085): something to plan besides contracts. Every aircraft wears a
    /// little each rotation and needs a check every <see cref="IntervalRotations"/>. The player
    /// chooses when: a check grounds the aircraft for a few hours and costs money, so it fits
    /// best overnight or between contracts. Flying on past the interval costs reliability on
    /// every overdue rotation.
    /// </summary>
    public static class Maintenance
    {
        public const int IntervalRotations = 8;

        /// <summary>Reliability lost for each rotation flown with the check overdue.</summary>
        public const int OverduePenalty = 2;

        /// <summary>Check cost as a share of the aircraft's list price.</summary>
        public const double CostFraction = 0.06;

        public static int RotationsUntilDue(FleetAircraft aircraft) =>
            aircraft == null ? IntervalRotations : IntervalRotations - aircraft.RotationsSinceCheck;

        /// <summary>One rotation left, or none: worth planning the check now.</summary>
        public static bool IsDueSoon(FleetAircraft aircraft) => RotationsUntilDue(aircraft) <= 1;

        public static bool IsOverdue(FleetAircraft aircraft) => RotationsUntilDue(aircraft) <= 0;

        public static bool InCheck(FleetAircraft aircraft, SimulationTime now) =>
            aircraft?.CheckUntil is { } until && until.CompareTo(now) > 0;

        /// <summary>
        /// Parked, idle, not already in a check — the HUD can offer the button. Funds and
        /// ownership are still the command's to refuse.
        /// </summary>
        public static bool CanStart(FleetAircraft aircraft, SimulationTime now) =>
            aircraft != null
            && aircraft.Airline.IsPlayer
            && aircraft.State == FleetState.AtStand
            && !aircraft.Scheduled.HasValue
            && !InCheck(aircraft, now);

        public static long CheckSeconds(AircraftType type) =>
            AircraftCatalogue.TryFor(type, out var spec) && spec.StandClass == StandClass.TerminalGate
                ? 4 * 3600L
                : 2 * 3600L;

        public static long CheckCost(AircraftType type) =>
            AircraftAcquisition.TryFor(type, out var offer)
                ? Math.Max(300L, (long)Math.Round(offer.Price * CostFraction))
                : 400L;

        /// <summary>A short status for cards and lists, or empty when nothing is worth saying.</summary>
        public static string Status(FleetAircraft aircraft, SimulationTime now, AirlineClock clock)
        {
            if (aircraft == null || !aircraft.Airline.IsPlayer)
                return string.Empty;
            if (InCheck(aircraft, now))
                return $"In check until {(clock ?? AirlineClock.Default).TimeText(aircraft.CheckUntil.Value)}";
            if (IsOverdue(aircraft))
                return "Check overdue";
            if (IsDueSoon(aircraft))
                return "Check due next rotation";
            return $"Check in {RotationsUntilDue(aircraft)} rotations";
        }
    }
}
