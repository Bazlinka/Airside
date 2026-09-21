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
        /// Opening float for a new Provisional airline (ADR 0077). Covers several Saab hops
        /// before the first aircraft returns, but not an ATR on day one — the player has to
        /// fly to earn the first step up the fleet ladder.
        /// </summary>
        public const long StartingFunds = 2_800;

        /// <summary>
        /// Paid when the player books the rotation (refunded if they cancel before pushback).
        /// Covers the whole out-and-back, so the planner can show one number.
        /// </summary>
        public static long DispatchCost(AircraftType type, double oneWayKm)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            return Math.Max(90, (long)Math.Round(70 + Math.Max(0, oneWayKm) * 1.28 * Weight(type)));
        }

        /// <summary>Paid once when a player aircraft returns to stand, with or without a contract.</summary>
        public static long FlightPay(AircraftType type, double oneWayKm) =>
            FlightPay(type, oneWayKm, RouteBand.Regional);

        public static long FlightPay(AircraftType type, double oneWayKm, RouteBand band)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            var raw = Math.Max(120, (long)Math.Round(140 + Math.Max(0, oneWayKm) * 2.0 * Weight(type)
                * RouteAccess.PayMultiplier(band)));
            return raw;
        }

        /// <summary>
        /// Scales a rotation's base flight pay by how reliably the airline has been operating —
        /// ties "work harder for things" to the flat per-flight rate, not just the tier gate at
        /// <see cref="AircraftOffer.RequiredReliability"/>. Neutral (1.0) at and above
        /// <see cref="ContractMarket.DomesticReliabilityFloor"/> (70) — the same
        /// threshold the contract market already uses to gate Domestic-and-up destinations —
        /// so a career in good standing (100 to start) is completely unaffected; only a
        /// reliability that has actually slipped costs something on every flight, not just at
        /// the next purchase. Contract-specific pay (<see cref="RouteContractDefinition.PaymentPerRotation"/>,
        /// <see cref="RouteContractDefinition.CompletionReward"/>) is applied separately and
        /// never scaled — a contract's advertised numbers always pay exactly what it advertised.
        /// </summary>
        public static double ReliabilityMultiplier(int reliability)
        {
            if (reliability >= 70) return 1.0;
            if (reliability >= 50) return 0.92;
            return 0.8;
        }

        /// <summary>
        /// How many reliability points a player pushback earns or loses vs its booked time
        /// (ADR 0078). Grace of two minutes counts as on time; AI traffic is not scored.
        /// </summary>
        public const int OnTimeGraceSeconds = 2 * 60;
        public const int SoftLateSeconds = 5 * 60;
        public const int HardLateSeconds = 15 * 60;

        public static int PunctualityReliabilityDelta(int latenessSeconds)
        {
            if (latenessSeconds <= OnTimeGraceSeconds) return 1;
            if (latenessSeconds <= SoftLateSeconds) return 0;
            if (latenessSeconds <= HardLateSeconds) return -1;
            return -2;
        }

        /// <summary>
        /// Regional turboprops (bay types) are the starter airline's tool; jets cost more to
        /// dispatch. ATR/Saab/Dash 8 cruise above 500 km/h, so a cruise-speed cutoff would
        /// price a turboprop as a 787.
        /// </summary>
        public static double Weight(AircraftType type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            return AircraftCatalogue.For(type).StandClass == StandClass.RegionalBay ? 1.0 : 2.2;
        }
    }
}
