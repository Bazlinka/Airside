using System;
using System.Collections.Generic;

namespace Airside.Domain
{
    /// <summary>
    /// An authored airline service contract (ADR 0053): a route, an eligible aircraft, how
    /// many rotations it takes and what each one — and completing the whole contract — pays.
    /// Pure data; <see cref="Simulation.AirlineOperations"/> decides when one is accepted,
    /// tracks progress against it and settles each eligible flight.
    /// </summary>
    public sealed class RouteContractDefinition
    {
        public RouteContractDefinition(
            string id, string originCode, string destinationCode, AircraftType eligibleType,
            int requiredRotations, long paymentPerRotation, long completionReward,
            int reliabilityGainPerRotation, OperatingTier requiredTier, int reliabilityLossOnCancel = 0,
            OperatingTier unlocksTier = OperatingTier.Provisional)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A contract id is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(originCode))
                throw new ArgumentException("An origin is required.", nameof(originCode));
            if (string.IsNullOrWhiteSpace(destinationCode))
                throw new ArgumentException("A destination is required.", nameof(destinationCode));
            if (eligibleType == null)
                throw new ArgumentNullException(nameof(eligibleType));
            if (requiredRotations <= 0)
                throw new ArgumentOutOfRangeException(nameof(requiredRotations));
            if (paymentPerRotation < 0)
                throw new ArgumentOutOfRangeException(nameof(paymentPerRotation));
            if (completionReward < 0)
                throw new ArgumentOutOfRangeException(nameof(completionReward));

            Id = id;
            OriginCode = originCode;
            DestinationCode = destinationCode;
            EligibleType = eligibleType;
            RequiredRotations = requiredRotations;
            PaymentPerRotation = paymentPerRotation;
            CompletionReward = completionReward;
            ReliabilityGainPerRotation = reliabilityGainPerRotation;
            RequiredTier = requiredTier;
            ReliabilityLossOnCancel = Math.Max(0, reliabilityLossOnCancel);
            UnlocksTier = unlocksTier;
        }

        public string Id { get; }
        public string OriginCode { get; }
        public string DestinationCode { get; }
        public AircraftType EligibleType { get; }
        public int RequiredRotations { get; }

        /// <summary>Paid once per eligible completed rotation. Tuning data, not final.</summary>
        public long PaymentPerRotation { get; }

        /// <summary>Paid once, on top of the last rotation's payment, when the contract fulfils.</summary>
        public long CompletionReward { get; }

        public int ReliabilityGainPerRotation { get; }
        public OperatingTier RequiredTier { get; }

        /// <summary>Reliability cost of cancelling a scheduled flight that would have counted
        /// towards this contract — a broken commitment, not a flight that was never planned.</summary>
        public int ReliabilityLossOnCancel { get; }

        /// <summary>If this is above the airline's current tier, completing the contract grants it.</summary>
        public OperatingTier UnlocksTier { get; }

        /// <summary>True when <paramref name="fromCode"/>/<paramref name="toCode"/> match this contract's route, either direction.</summary>
        public bool MatchesRoute(string fromCode, string toCode) =>
            (string.Equals(OriginCode, fromCode, StringComparison.OrdinalIgnoreCase)
                && string.Equals(DestinationCode, toCode, StringComparison.OrdinalIgnoreCase))
            || (string.Equals(OriginCode, toCode, StringComparison.OrdinalIgnoreCase)
                && string.Equals(DestinationCode, fromCode, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Authored intro contracts kept for save compatibility (ADR 0053 / 0055 / 0077). Live
    /// offers come from <c>ContractMarket</c> (ADR 0056). Regional intros use the starter
    /// Saab 340; Melbourne needs a Dash 8. Payment numbers are placeholders.
    /// </summary>
    public static class RouteContractCatalogue
    {
        public static readonly RouteContractDefinition RegionalKingscoteIntro = new(
            id: "REG-KGC-INTRO",
            originCode: "ADL",
            destinationCode: "KGC",
            eligibleType: AircraftType.Saab340,
            requiredRotations: 5,
            paymentPerRotation: 420,
            completionReward: 1_100,
            reliabilityGainPerRotation: 2,
            requiredTier: OperatingTier.Provisional,
            reliabilityLossOnCancel: 3,
            unlocksTier: OperatingTier.Regional);

        public static readonly RouteContractDefinition RegionalPortLincolnIntro = new(
            id: "REG-PLO-INTRO",
            originCode: "ADL",
            destinationCode: "PLO",
            eligibleType: AircraftType.Saab340,
            requiredRotations: 4,
            paymentPerRotation: 580,
            completionReward: 1_500,
            reliabilityGainPerRotation: 2,
            requiredTier: OperatingTier.Provisional,
            reliabilityLossOnCancel: 3,
            unlocksTier: OperatingTier.Regional);

        public static readonly RouteContractDefinition RegionalWhyallaIntro = new(
            id: "REG-WYA-INTRO",
            originCode: "ADL",
            destinationCode: "WYA",
            eligibleType: AircraftType.Saab340,
            requiredRotations: 4,
            paymentPerRotation: 540,
            completionReward: 1_300,
            reliabilityGainPerRotation: 2,
            requiredTier: OperatingTier.Regional,
            reliabilityLossOnCancel: 3);

        public static readonly RouteContractDefinition DomesticMelbourneIntro = new(
            id: "DOM-MEL-INTRO",
            originCode: "ADL",
            destinationCode: "MEL",
            eligibleType: AircraftType.Dash8Q400,
            requiredRotations: 3,
            paymentPerRotation: 900,
            completionReward: 2500,
            reliabilityGainPerRotation: 3,
            requiredTier: OperatingTier.Regional,
            reliabilityLossOnCancel: 4,
            unlocksTier: OperatingTier.Domestic);

        // ---- Career contracts (ADR 0084): a stepping stone per campaign chapter ----

        public static readonly RouteContractDefinition RegionalMountGambier = new(
            id: "REG-MGB",
            originCode: "ADL",
            destinationCode: "MGB",
            eligibleType: AircraftType.Atr42,
            requiredRotations: 5,
            paymentPerRotation: 650,
            completionReward: 1800,
            reliabilityGainPerRotation: 2,
            requiredTier: OperatingTier.Regional,
            reliabilityLossOnCancel: 3);

        public static readonly RouteContractDefinition RegionalCeduna = new(
            id: "REG-CED",
            originCode: "ADL",
            destinationCode: "CED",
            eligibleType: AircraftType.Saab340,
            requiredRotations: 4,
            paymentPerRotation: 700,
            completionReward: 1600,
            reliabilityGainPerRotation: 2,
            requiredTier: OperatingTier.Regional,
            reliabilityLossOnCancel: 3);

        public static readonly RouteContractDefinition RegionalCooberPedy = new(
            id: "REG-CPD",
            originCode: "ADL",
            destinationCode: "CPD",
            eligibleType: AircraftType.Atr42,
            requiredRotations: 4,
            paymentPerRotation: 950,
            completionReward: 2400,
            reliabilityGainPerRotation: 2,
            requiredTier: OperatingTier.Regional,
            reliabilityLossOnCancel: 3);

        public static readonly RouteContractDefinition RegionalMildura = new(
            id: "REG-MQL",
            originCode: "ADL",
            destinationCode: "MQL",
            eligibleType: AircraftType.Dash8Q400,
            requiredRotations: 4,
            paymentPerRotation: 800,
            completionReward: 2200,
            reliabilityGainPerRotation: 2,
            requiredTier: OperatingTier.Regional,
            reliabilityLossOnCancel: 3);

        public static readonly RouteContractDefinition RegionalBrokenHill = new(
            id: "REG-BHQ",
            originCode: "ADL",
            destinationCode: "BHQ",
            eligibleType: AircraftType.Dash8Q400,
            requiredRotations: 4,
            paymentPerRotation: 900,
            completionReward: 2400,
            reliabilityGainPerRotation: 2,
            requiredTier: OperatingTier.Regional,
            reliabilityLossOnCancel: 3);

        public static readonly RouteContractDefinition DomesticCanberra = new(
            id: "DOM-CBR",
            originCode: "ADL",
            destinationCode: "CBR",
            eligibleType: AircraftType.Dash8Q400,
            requiredRotations: 4,
            paymentPerRotation: 1300,
            completionReward: 3200,
            reliabilityGainPerRotation: 3,
            requiredTier: OperatingTier.Domestic,
            reliabilityLossOnCancel: 4);

        public static readonly RouteContractDefinition DomesticSydney = new(
            id: "DOM-SYD",
            originCode: "ADL",
            destinationCode: "SYD",
            eligibleType: AircraftType.Boeing7378,
            requiredRotations: 5,
            paymentPerRotation: 2400,
            completionReward: 6000,
            reliabilityGainPerRotation: 3,
            requiredTier: OperatingTier.Domestic,
            reliabilityLossOnCancel: 4);

        public static readonly RouteContractDefinition DomesticBrisbane = new(
            id: "DOM-BNE",
            originCode: "ADL",
            destinationCode: "BNE",
            eligibleType: AircraftType.Boeing7378,
            requiredRotations: 4,
            paymentPerRotation: 3000,
            completionReward: 7500,
            reliabilityGainPerRotation: 3,
            requiredTier: OperatingTier.Domestic,
            reliabilityLossOnCancel: 4);

        public static readonly RouteContractDefinition DomesticPerth = new(
            id: "DOM-PER",
            originCode: "ADL",
            destinationCode: "PER",
            eligibleType: AircraftType.AirbusA321Neo,
            requiredRotations: 4,
            paymentPerRotation: 3800,
            completionReward: 9000,
            reliabilityGainPerRotation: 3,
            requiredTier: OperatingTier.Domestic,
            reliabilityLossOnCancel: 5);

        public static readonly RouteContractDefinition InternationalAuckland = new(
            id: "INT-AKL",
            originCode: "ADL",
            destinationCode: "AKL",
            eligibleType: AircraftType.AirbusA321Neo,
            requiredRotations: 3,
            paymentPerRotation: 5500,
            completionReward: 12000,
            reliabilityGainPerRotation: 3,
            requiredTier: OperatingTier.International,
            reliabilityLossOnCancel: 5);

        public static readonly RouteContractDefinition InternationalSingapore = new(
            id: "INT-SIN",
            originCode: "ADL",
            destinationCode: "SIN",
            eligibleType: AircraftType.AirbusA350900,
            requiredRotations: 3,
            paymentPerRotation: 11000,
            completionReward: 25000,
            reliabilityGainPerRotation: 3,
            requiredTier: OperatingTier.International,
            reliabilityLossOnCancel: 5);

        public static readonly RouteContractDefinition InternationalHongKong = new(
            id: "INT-HKG",
            originCode: "ADL",
            destinationCode: "HKG",
            eligibleType: AircraftType.Boeing78710,
            requiredRotations: 3,
            paymentPerRotation: 13000,
            completionReward: 30000,
            reliabilityGainPerRotation: 3,
            requiredTier: OperatingTier.International,
            reliabilityLossOnCancel: 5);

        public static readonly IReadOnlyList<RouteContractDefinition> All = new[]
        {
            RegionalKingscoteIntro,
            RegionalPortLincolnIntro,
            RegionalWhyallaIntro,
            DomesticMelbourneIntro,
            RegionalMountGambier,
            RegionalCeduna,
            RegionalCooberPedy,
            RegionalMildura,
            RegionalBrokenHill,
            DomesticCanberra,
            DomesticSydney,
            DomesticBrisbane,
            DomesticPerth,
            InternationalAuckland,
            InternationalSingapore,
            InternationalHongKong
        };

        public static bool TryFind(string id, out RouteContractDefinition definition)
        {
            definition = null;
            foreach (var candidate in All)
            {
                if (string.Equals(candidate.Id, id, StringComparison.Ordinal))
                {
                    definition = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}
