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
        /// Opening operating cash for a new Standard airline (Economy v2, real Australian dollars). Covers an ATR 42 lease deposit
        /// and several weeks of Saab hops, but not a jet or widebody deposit: the player has to fly to earn each step up.
        /// </summary>
        public const long StartingFunds = FlightCostModel.StartingCash;

        /// <summary>
        /// Paid when the player books the rotation (refunded if they cancel before pushback). Covers the whole out-and-back
        /// (two legs), so the planner can show one number. Built from <see cref="FlightCostModel"/>: fuel, crew, maintenance,
        /// airport and navigation charges, handling and overhead. The maintenance reserve is billed separately, by checks.
        /// </summary>
        public static long DispatchCost(AircraftType type, double oneWayKm)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            var km = Math.Max(0, oneWayKm);
            return Math.Max(1L, (long)Math.Round(2.0 * FlightCostModel.Leg(type, km, FlightCostModel.BandForDistance(km)).DispatchCost));
        }

        /// <summary>
        /// ADR 0134: how far a type's usual flight is, by the routes it may fly: what a player should keep
        /// cash for after buying it. Capped by the type's own range.
        /// </summary>
        public static double TypicalLegKm(AircraftType type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            var km = RouteAccess.Ceiling(type) switch
            {
                RouteBand.Regional => 300.0,
                RouteBand.Domestic => 1000.0,
                RouteBand.National => 2000.0,
                RouteBand.Tasman or RouteBand.Pacific => 3200.0,
                _ => 6000.0
            };
            return Math.Min(km, type.PracticalRangeKm);
        }

        /// <summary>Paid once when a player aircraft returns to stand, with or without a contract.</summary>
        public static long FlightPay(AircraftType type, double oneWayKm) =>
            FlightPay(type, oneWayKm, RouteBand.Regional);

        public static long FlightPay(AircraftType type, double oneWayKm, RouteBand band)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            return Math.Max(1L, (long)Math.Round(2.0 * FlightCostModel.Leg(type, Math.Max(0, oneWayKm), band).Revenue));
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

        /// <summary>Lateness left once the seconds attributed to weather are taken off (ADR: weather is not scored).</summary>
        public static int ControllableLateness(int latenessSeconds, DelayBreakdown delay)
        {
            var weather = 0;
            foreach (var part in delay.Parts)
                if (part.Cause == DelayCause.Weather)
                    weather += part.Seconds;
            return Math.Max(0, latenessSeconds - weather);
        }

        public static int PunctualityReliabilityDelta(int latenessSeconds)
        {
            if (latenessSeconds <= OnTimeGraceSeconds) return 1;
            if (latenessSeconds <= SoftLateSeconds) return 0;
            if (latenessSeconds <= HardLateSeconds) return -1;
            return -2;
        }
    }
}
