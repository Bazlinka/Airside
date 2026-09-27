using System;
using System.Collections.Generic;

namespace Airside.Domain
{
    /// <summary>What it costs, and what you must already be, to buy one more aircraft of a type (ADR 0056).</summary>
    public sealed class AircraftOffer
    {
        public AircraftOffer(AircraftType type, long price, OperatingTier requiredTier, int requiredReliability,
            int requiredRotations)
        {
            Type = type ?? throw new ArgumentNullException(nameof(type));
            Price = price;
            RequiredTier = requiredTier;
            RequiredReliability = requiredReliability;
            RequiredRotations = requiredRotations;
        }

        public AircraftType Type { get; }
        public long Price { get; }
        public OperatingTier RequiredTier { get; }
        public int RequiredReliability { get; }
        public int RequiredRotations { get; }
        public RouteBand Operates => RouteAccess.Ceiling(Type);
    }

    /// <summary>Authored purchase list. The starter Saab is owned, not bought (ADR 0077).</summary>
    public static class AircraftAcquisition
    {
        public const int MaxPlayerAircraft = 25;

        /// <summary>First step up from the starter Saab — still Provisional, still bay-based.</summary>
        public static readonly AircraftOffer Atr42 = new(
            AircraftType.Atr42, 5_200, OperatingTier.Provisional, 70, 5);

        public static readonly AircraftOffer Dash8Q400 = new(
            AircraftType.Dash8Q400, 14_500, OperatingTier.Regional, 75, 12);

        public static readonly AircraftOffer Boeing7378 = new(
            AircraftType.Boeing7378, 28_000, OperatingTier.Domestic, 82, 42);

        public static readonly AircraftOffer AirbusA321Neo = new(
            AircraftType.AirbusA321Neo, 45_000, OperatingTier.Domestic, 88, 60);

        public static readonly AircraftOffer AirbusA350900 = new(
            AircraftType.AirbusA350900, 85_000, OperatingTier.International, 92, 100);

        public static readonly AircraftOffer Boeing78710 = new(
            AircraftType.Boeing78710, 82_000, OperatingTier.International, 92, 100);

        // ADR 0131: every type Airside already models is for sale. Older narrowbodies are cheaper to buy
        // and dearer to run (FlightEconomics.RunningCostFactor); regional jets are the first jet step.

        /// <summary>The first jet: cheaper than a 737, flies the domestic band.</summary>
        public static readonly AircraftOffer EmbraerE190 = new(
            AircraftType.EmbraerE190, 20_000, OperatingTier.Domestic, 80, 32);

        /// <summary>A small modern jet with the range for the Tasman.</summary>
        public static readonly AircraftOffer AirbusA220300 = new(
            AircraftType.AirbusA220300, 24_000, OperatingTier.Domestic, 82, 36);

        /// <summary>Older narrowbodies: a cheaper way onto national routes, dearer to fly.</summary>
        public static readonly AircraftOffer Boeing737800 = new(
            AircraftType.Boeing737800, 24_500, OperatingTier.Domestic, 80, 38);

        public static readonly AircraftOffer AirbusA320200 = new(
            AircraftType.AirbusA320200, 25_500, OperatingTier.Domestic, 80, 38);

        /// <summary>A cheaper first widebody.</summary>
        public static readonly AircraftOffer AirbusA330900 = new(
            AircraftType.AirbusA330900, 66_000, OperatingTier.International, 90, 90);

        /// <summary>The long-range widebody for thinner long-haul routes.</summary>
        public static readonly AircraftOffer Boeing7879 = new(
            AircraftType.Boeing7879, 74_000, OperatingTier.International, 91, 95);

        /// <summary>Every offer, in the order a career meets them (tier, then price).</summary>
        public static readonly IReadOnlyList<AircraftOffer> All = new[]
        {
            Atr42, Dash8Q400,
            EmbraerE190, AirbusA220300, Boeing737800, AirbusA320200, Boeing7378, AirbusA321Neo,
            AirbusA330900, Boeing7879, Boeing78710, AirbusA350900
        };

        public static bool TryFor(AircraftType type, out AircraftOffer offer)
        {
            offer = null;
            if (type == null)
                return false;
            foreach (var candidate in All)
            {
                if (candidate.Type.Id != type.Id)
                    continue;
                offer = candidate;
                return true;
            }

            return false;
        }
    }
}
