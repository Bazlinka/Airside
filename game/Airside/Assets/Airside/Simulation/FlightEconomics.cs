using System;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// What it costs to dispatch a player rotation and what that rotation pays when it
    /// returns. Pure functions of type and one-way distance — presentation never awards
    /// money itself (ADR 0053 / 0055). AI traffic is not charged.
    /// </summary>
    public static class FlightEconomics
    {
        /// <summary>
        /// Opening float for a new Provisional airline. Covers several ATR hops (including a
        /// long SA leg) before the first aircraft returns, and still leaves room to dispatch
        /// two parked player aircraft at once.
        /// </summary>
        public const long StartingFunds = 4_000;

        /// <summary>
        /// Paid when the player books the rotation (refunded if they cancel before pushback).
        /// Covers the whole out-and-back, so the planner can show one number.
        /// </summary>
        public static long DispatchCost(AircraftType type, double oneWayKm)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            return Math.Max(80, (long)Math.Round(60 + Math.Max(0, oneWayKm) * 1.1 * Weight(type)));
        }

        /// <summary>Paid once when a player aircraft returns to stand, with or without a contract.</summary>
        public static long FlightPay(AircraftType type, double oneWayKm)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            return Math.Max(120, (long)Math.Round(140 + Math.Max(0, oneWayKm) * 2.0 * Weight(type)));
        }

        /// <summary>
        /// Regional turboprops (bay types) are the starter airline's tool; jets cost more to
        /// dispatch. ATR/Saab/Dash 8 cruise above 500 km/h, so a cruise-speed cutoff would
        /// price the starter ATR as a 787.
        /// </summary>
        public static double Weight(AircraftType type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            return AircraftCatalogue.For(type).StandClass == StandClass.RegionalBay ? 1.0 : 2.2;
        }
    }
}
