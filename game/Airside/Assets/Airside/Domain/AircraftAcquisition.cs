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

    /// <summary>Authored purchase list. The starter ATR is owned, not bought.</summary>
    public static class AircraftAcquisition
    {
        public const int MaxPlayerAircraft = 4;

        public static readonly AircraftOffer Saab340 = new(
            AircraftType.Saab340, 6_500, OperatingTier.Provisional, 70, 6);

        public static readonly AircraftOffer Dash8Q400 = new(
            AircraftType.Dash8Q400, 12_000, OperatingTier.Regional, 75, 12);

        public static readonly AircraftOffer Boeing7378 = new(
            AircraftType.Boeing7378, 32_000, OperatingTier.Domestic, 82, 20);

        public static readonly AircraftOffer AirbusA321Neo = new(
            AircraftType.AirbusA321Neo, 48_000, OperatingTier.International, 88, 30);

        public static readonly AircraftOffer AirbusA350900 = new(
            AircraftType.AirbusA350900, 90_000, OperatingTier.International, 92, 40);

        public static readonly AircraftOffer Boeing78710 = new(
            AircraftType.Boeing78710, 88_000, OperatingTier.International, 92, 40);

        public static readonly IReadOnlyList<AircraftOffer> All = new[]
        {
            Saab340, Dash8Q400, Boeing7378, AirbusA321Neo, AirbusA350900, Boeing78710
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
