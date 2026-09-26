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
        /// The next step up the fleet ladder: the first acquisition offer whose type the airline does
        /// not own yet (so the card moves past the ATR once one is bought), with every gate still
        /// short. When every type is owned, the cheapest offer. Null offer when the fleet is full.
        /// </summary>
        public static NextAircraftRequirement NextAircraft(AirlineCareerState career, int ownedCount,
            IReadOnlyList<AircraftType> ownedTypes = null)
        {
            if (career == null || ownedCount >= AircraftAcquisition.MaxPlayerAircraft)
                return new NextAircraftRequirement(null, 0, 0, 0, false, ownedCount >= AircraftAcquisition.MaxPlayerAircraft, string.Empty, false);

            AircraftOffer first = null;
            foreach (var offer in AircraftAcquisition.All)
            {
                first ??= offer;
                if (Owns(ownedTypes, offer.Type))
                    continue;
                var fundsShort = career.Funds >= offer.Price ? 0 : offer.Price - career.Funds;
                var rotationsShort = career.CompletedPlayerRotations >= offer.RequiredRotations
                    ? 0 : offer.RequiredRotations - career.CompletedPlayerRotations;
                var reliabilityShort = career.Reliability >= offer.RequiredReliability
                    ? 0 : offer.RequiredReliability - career.Reliability;
                var needsTier = career.Tier < offer.RequiredTier;
                string baseRequirement = string.Empty;
                if (ownedCount >= career.Base.FleetCapacity)
                    baseRequirement = career.BaseLevel == PlayerBaseLevel.International
                        ? "Adelaide is full — add aircraft at an outstation in Fleet → Network"
                        : $"{career.Base.Title} is full — expand your Adelaide base";
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

        private static bool Owns(IReadOnlyList<AircraftType> owned, AircraftType type)
        {
            if (owned == null) return false;
            foreach (var candidate in owned)
                if (candidate != null && candidate.Id == type.Id) return true;
            return false;
        }
    }
}
