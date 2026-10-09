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
        /// <summary>Flat part of every dispatch: landing, handling and crew call-out, whatever the distance.</summary>
        public const long DispatchBaseCost = 120;
        public const long MinimumDispatchCost = 180;

        public static long DispatchCost(AircraftType type, double oneWayKm)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            return Math.Max(MinimumDispatchCost, (long)Math.Round(DispatchBaseCost + Math.Max(0, oneWayKm) * CostPerKm(type)));
        }

        /// <summary>
        /// Dispatch cost per one-way km. Turboprops were tuned down from 1.28 to 1.12 (ADR 0125):
        /// the balance runs showed the regional fleet could not fund the jet, gate and outstation
        /// the Domestic stage asks for inside ADR 0120's play-time window.
        /// </summary>
        public static double CostPerKm(AircraftType type) =>
            type != null && type.IsRotorcraft ? RotorcraftCostPerKm
            : Weight(type) <= 1.0 ? TurbopropCostPerKm : JetCostPerKm * Weight(type) * RunningCostFactor(type);

        /// <summary>
        /// ADR 0131: how thirsty a jet is against the modern types the economy was tuned on (1.0). Only
        /// the running cost moves, never the pay, so older jets are cheaper to buy and dearer to fly.
        /// </summary>
        public static double RunningCostFactor(AircraftType type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (type.Id == AircraftType.EmbraerE190.Id) return 0.92;
            if (type.Id == AircraftType.AirbusA220300.Id) return 0.95;
            if (type.Id == AircraftType.Boeing737800.Id) return 1.12;
            if (type.Id == AircraftType.AirbusA320200.Id) return 1.10;
            if (type.Id == AircraftType.AirbusA330900.Id) return 1.02;
            if (type.Id == AircraftType.Boeing7879.Id) return 0.98;
            return 1.0;
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

        // Rebalanced up (game dollars, 8 Oct 2026): a flight now costs real money. A starter Saab hop keeps about
        // a fifth of its pay, a jet on a good domestic or longer leg 20-30%, so aircraft prices are earned over
        // many flights instead of a handful. See docs/plans/economy_realism_plan.md.
        public const double TurbopropCostPerKm = 1.45;
        public const double JetCostPerKm = 1.65;
        public const double RotorcraftCostPerKm = 1.0;

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

        /// <summary>
        /// Regional turboprops (bay types) are the starter airline's tool; jets cost more to
        /// dispatch. ATR/Saab/Dash 8 cruise above 500 km/h, so a cruise-speed cutoff would
        /// price a turboprop as a 787.
        /// </summary>
        public static double Weight(AircraftType type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            // Thirteen seats and a short-hop helicopter: a lighter load than a turboprop, priced like one to run.
            if (type.IsRotorcraft)
                return 0.7;
            if (AircraftCatalogue.For(type).StandClass == StandClass.RegionalBay)
                return 1.0;
            // A jet's pay and running cost step up with its size class, or a 337-seat 787-10 would earn less than a
            // 170-seat 737 on the same route (revenue only reflects load factor). Classes, not exact seats, so
            // sister types (737-8 / 737-800) still pay the same.
            var seats = AircraftCatalogue.TypicalSeats(type);
            return seats <= 120 ? 1.6 : seats <= 220 ? 2.2 : seats <= 300 ? 3.0 : 3.4;
        }
    }
}
