using System;
using System.Collections.Generic;

namespace Airside.Domain
{
    /// <summary>
    /// What it costs, and what you must already be, to take one more aircraft of a type (ADR 0056). Since Economy v2 the
    /// <see cref="Price"/> is the lease deposit (<see cref="LeaseTerms.Deposit"/>), not a purchase price.
    /// </summary>
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

    /// <summary>
    /// Authored purchase list. The first Saab is given at the start (ADR 0077). A second one
    /// is for sale immediately, paid from the opening cash (ADR 0164).
    /// </summary>
    public static class AircraftAcquisition
    {
        public const int MaxPlayerAircraft = 25;

        /// <summary>A second Saab, bought from the opening float. No flights required.</summary>
        public static readonly AircraftOffer Saab340 = new(
            AircraftType.Saab340, LeaseTerms.Deposit(AircraftType.Saab340), OperatingTier.Provisional, 70, 0);

        /// <summary>First step up from the Saab — still Provisional, still bay-based, after the base expands.</summary>
        public static readonly AircraftOffer Atr42 = new(
            AircraftType.Atr42, LeaseTerms.Deposit(AircraftType.Atr42), OperatingTier.Provisional, 70, 5);

        /// <summary>
        /// The Bell 412EP (ADR 0207): a helicopter that lifts from the helipad beside the rescue base and flies the
        /// regional band. Thirteen seats pay less per flight than a turboprop's, and it needs the expanded base's pad.
        /// </summary>
        public static readonly AircraftOffer Bell412 = new(
            AircraftType.Bell412, LeaseTerms.Deposit(AircraftType.Bell412), OperatingTier.Provisional, 75, 8);

        public static readonly AircraftOffer Dash8Q400 = new(
            AircraftType.Dash8Q400, LeaseTerms.Deposit(AircraftType.Dash8Q400), OperatingTier.Regional, 75, 12);

        public static readonly AircraftOffer Boeing7378 = new(
            AircraftType.Boeing7378, LeaseTerms.Deposit(AircraftType.Boeing7378), OperatingTier.Domestic, 82, 42);

        public static readonly AircraftOffer AirbusA321Neo = new(
            AircraftType.AirbusA321Neo, LeaseTerms.Deposit(AircraftType.AirbusA321Neo), OperatingTier.Domestic, 88, 60);

        public static readonly AircraftOffer AirbusA350900 = new(
            AircraftType.AirbusA350900, LeaseTerms.Deposit(AircraftType.AirbusA350900), OperatingTier.International, 92, 100);

        public static readonly AircraftOffer Boeing78710 = new(
            AircraftType.Boeing78710, LeaseTerms.Deposit(AircraftType.Boeing78710), OperatingTier.International, 92, 100);

        // ADR 0131: every type Airside already models is for sale. Older narrowbodies are cheaper to buy
        // and dearer to run (fuel burn in FlightCostModel); regional jets are the first jet step.

        /// <summary>The first jet: cheaper than a 737, flies the domestic band.</summary>
        public static readonly AircraftOffer EmbraerE190 = new(
            AircraftType.EmbraerE190, LeaseTerms.Deposit(AircraftType.EmbraerE190), OperatingTier.Domestic, 80, 32);

        /// <summary>A small modern jet with the range for the Tasman.</summary>
        public static readonly AircraftOffer AirbusA220300 = new(
            AircraftType.AirbusA220300, LeaseTerms.Deposit(AircraftType.AirbusA220300), OperatingTier.Domestic, 82, 36);

        /// <summary>Older narrowbodies: a cheaper way onto national routes, dearer to fly.</summary>
        public static readonly AircraftOffer Boeing737800 = new(
            AircraftType.Boeing737800, LeaseTerms.Deposit(AircraftType.Boeing737800), OperatingTier.Domestic, 80, 38);

        public static readonly AircraftOffer AirbusA320200 = new(
            AircraftType.AirbusA320200, LeaseTerms.Deposit(AircraftType.AirbusA320200), OperatingTier.Domestic, 80, 38);

        /// <summary>A cheaper first widebody.</summary>
        public static readonly AircraftOffer AirbusA330900 = new(
            AircraftType.AirbusA330900, LeaseTerms.Deposit(AircraftType.AirbusA330900), OperatingTier.International, 90, 90);

        /// <summary>The long-range widebody for thinner long-haul routes.</summary>
        public static readonly AircraftOffer Boeing7879 = new(
            AircraftType.Boeing7879, LeaseTerms.Deposit(AircraftType.Boeing7879), OperatingTier.International, 91, 95);

        /// <summary>Every offer, in the order a career meets them (tier, then price).</summary>
        public static readonly IReadOnlyList<AircraftOffer> All = new[]
        {
            Saab340, Atr42, Bell412, Dash8Q400,
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
