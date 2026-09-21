using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// What's still needed to buy the next hangar aircraft (ADR 0078) — funds, rotations,
    /// reliability and tier, reading the same gates <see cref="AirlineOperations.BuyAircraft"/>
    /// enforces so the objective card cannot lie.
    /// </summary>
    public readonly struct NextAircraftRequirement
    {
        internal NextAircraftRequirement(AircraftOffer offer, long fundsShort, int rotationsShort,
            int reliabilityShort, bool needsTier, bool fleetFull, string baseRequirementLine, bool readyToBuy)
        {
            Offer = offer;
            FundsShort = fundsShort;
            RotationsShort = rotationsShort;
            ReliabilityShort = reliabilityShort;
            NeedsTier = needsTier;
            FleetFull = fleetFull;
            BaseRequirementLine = baseRequirementLine ?? string.Empty;
            ReadyToBuy = readyToBuy;
        }

        public AircraftOffer Offer { get; }
        public long FundsShort { get; }
        public int RotationsShort { get; }
        public int ReliabilityShort { get; }
        public bool NeedsTier { get; }
        public bool FleetFull { get; }
        public string BaseRequirementLine { get; }
        public bool ReadyToBuy { get; }
        public bool HasOffer => Offer != null;
    }

    /// <summary>
    /// What's actually still needed for the next <see cref="OperatingTier"/> — a real answer
    /// to "what is Regional?" / "how do I unlock the next one?" instead of an opaque label,
    /// reading the exact thresholds <see cref="AirlineCareerState.EvaluateTier"/> checks so the
    /// two can never drift apart.
    /// </summary>
    public readonly struct NextTierRequirement
    {
        internal NextTierRequirement(OperatingTier tier, int rotationsRemaining, int reliabilityRemaining,
            string missingAircraftLine, bool isMaxTier = false)
        {
            Tier = tier;
            RotationsRemaining = rotationsRemaining;
            ReliabilityRemaining = reliabilityRemaining;
            MissingAircraftLine = missingAircraftLine ?? string.Empty;
            IsMaxTier = isMaxTier;
        }

        /// <summary>The tier this requirement leads to. Meaningless when <see cref="IsMaxTier"/>.</summary>
        public OperatingTier Tier { get; }

        /// <summary>0 when the rotation count is already met.</summary>
        public int RotationsRemaining { get; }

        /// <summary>0 when the reliability floor is already met.</summary>
        public int ReliabilityRemaining { get; }

        /// <summary>Non-empty only when a specific aircraft type (not just rotations/reliability) is still missing.</summary>
        public string MissingAircraftLine { get; }

        /// <summary>True once every requirement below is satisfied — the tier is one settlement away.</summary>
        public bool AllMet => RotationsRemaining <= 0 && ReliabilityRemaining <= 0
                               && string.IsNullOrEmpty(MissingAircraftLine);

        /// <summary>True at International — there is nothing further to unlock.</summary>
        public bool IsMaxTier { get; }

        public static NextTierRequirement MaxTierReached =>
            new(OperatingTier.International, 0, 0, string.Empty, isMaxTier: true);
    }

    /// <summary>
    /// What the player's Adelaide base is capable of at a career tier. This is descriptive
    /// progression, not a second upgrade currency: every line maps to systems the career
    /// already gates today (regional operation, fleet growth, jet access and international
    /// widebody operation).
    /// </summary>
    public readonly struct BaseCapability
    {
        internal BaseCapability(OperatingTier tier, string title, string detail, string nextUnlock)
        {
            Tier = tier;
            Title = title ?? string.Empty;
            Detail = detail ?? string.Empty;
            NextUnlock = nextUnlock ?? string.Empty;
        }

        public OperatingTier Tier { get; }
        public string Title { get; }
        public string Detail { get; }
        public string NextUnlock { get; }
    }

    /// <summary>Career-tier, base-capability and hangar progress, derived only — never stored (ADR 0090).</summary>
    public static class CareerProgress
    {
        public static BaseCapability BaseCapabilityFor(OperatingTier tier) => tier switch
        {
            OperatingTier.Regional => new BaseCapability(tier,
                "Expanded regional base",
                "Multiple regional aircraft · Dash 8 growth",
                "Jet-gate operations"),
            OperatingTier.Domestic => new BaseCapability(tier,
                "Jet-gate operation",
                "Domestic jets · terminal-gate fleet",
                "International handling"),
            OperatingTier.International => new BaseCapability(tier,
                "International base",
                "Widebody fleet · long-haul handling",
                string.Empty),
            _ => new BaseCapability(OperatingTier.Provisional,
                "Regional starter base",
                "Regional apron · one-aircraft operation",
                "Expanded regional base")
        };

        /// <summary>
        /// The cheapest acquisition offer the career has not yet cleared every gate for, or the
        /// first affordable offer when every gate is clear. Null offer when the fleet is full or
        /// the catalogue is empty.
        /// </summary>
        public static NextAircraftRequirement NextAircraft(AirlineCareerState career, int ownedCount)
        {
            if (career == null || ownedCount >= AircraftAcquisition.MaxPlayerAircraft)
                return new NextAircraftRequirement(null, 0, 0, 0, false, ownedCount >= AircraftAcquisition.MaxPlayerAircraft, string.Empty, false);

            AircraftOffer first = null;
            foreach (var offer in AircraftAcquisition.All)
            {
                first ??= offer;
                var fundsShort = career.Funds >= offer.Price ? 0 : offer.Price - career.Funds;
                var rotationsShort = career.CompletedPlayerRotations >= offer.RequiredRotations
                    ? 0 : offer.RequiredRotations - career.CompletedPlayerRotations;
                var reliabilityShort = career.Reliability >= offer.RequiredReliability
                    ? 0 : offer.RequiredReliability - career.Reliability;
                var needsTier = career.Tier < offer.RequiredTier;
                string baseRequirement = string.Empty;
                if (ownedCount >= career.Base.FleetCapacity)
                    baseRequirement = $"{career.Base.Title} is full — expand your Adelaide base";
                else if (!PlayerBase.Supports(career.BaseLevel, offer.Type))
                {
                    var needed = AircraftCatalogue.IsWidebody(offer.Type)
                        ? PlayerBaseLevel.International : PlayerBaseLevel.JetGate;
                    baseRequirement = $"Requires {PlayerBase.For(needed).Title}";
                }
                var ready = fundsShort == 0 && rotationsShort == 0 && reliabilityShort == 0
                            && !needsTier && string.IsNullOrEmpty(baseRequirement);
                if (!ready)
                    return new NextAircraftRequirement(offer, fundsShort, rotationsShort, reliabilityShort,
                        needsTier, false, baseRequirement, false);
                return new NextAircraftRequirement(offer, 0, 0, 0, false, false, string.Empty, true);
            }

            return new NextAircraftRequirement(first, 0, 0, 0, false, false, string.Empty, false);
        }

        public static NextTierRequirement NextTier(AirlineCareerState career, IReadOnlyList<AircraftType> ownedTypes)
        {
            if (career == null)
                return NextTierRequirement.MaxTierReached;

            switch (career.Tier)
            {
                case OperatingTier.Provisional:
                    return new NextTierRequirement(OperatingTier.Regional,
                        Remaining(AirlineCareerState.RegionalRotations, career.CompletedPlayerRotations),
                        Remaining(AirlineCareerState.RegionalReliability, career.Reliability),
                        string.Empty);

                case OperatingTier.Regional:
                {
                    var hasQualifying = AirlineCareerState.OwnsDash8(ownedTypes) || AirlineCareerState.OwnsAnyJet(ownedTypes);
                    return new NextTierRequirement(OperatingTier.Domestic,
                        Remaining(AirlineCareerState.DomesticRotations, career.CompletedPlayerRotations),
                        Remaining(AirlineCareerState.DomesticReliability, career.Reliability),
                        hasQualifying ? string.Empty : "a Dash 8-400 or a jet");
                }

                case OperatingTier.Domestic:
                {
                    var hasJet = AirlineCareerState.OwnsAnyJet(ownedTypes);
                    return new NextTierRequirement(OperatingTier.International,
                        Remaining(AirlineCareerState.InternationalRotations, career.CompletedPlayerRotations),
                        Remaining(AirlineCareerState.InternationalReliability, career.Reliability),
                        hasJet ? string.Empty : "a jet");
                }

                default:
                    return NextTierRequirement.MaxTierReached;
            }
        }

        private static int Remaining(int required, int current) =>
            current >= required ? 0 : required - current;
    }
}
