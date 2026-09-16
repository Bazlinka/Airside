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
            int reliabilityGainPerRotation, OperatingTier requiredTier)
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

        /// <summary>True when <paramref name="fromCode"/>/<paramref name="toCode"/> match this contract's route, either direction.</summary>
        public bool MatchesRoute(string fromCode, string toCode) =>
            (string.Equals(OriginCode, fromCode, StringComparison.OrdinalIgnoreCase)
                && string.Equals(DestinationCode, toCode, StringComparison.OrdinalIgnoreCase))
            || (string.Equals(OriginCode, toCode, StringComparison.OrdinalIgnoreCase)
                && string.Equals(DestinationCode, fromCode, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The authored contract set (ADR 0053). One entry today: the starter Adelaide-Kingscote
    /// regional contract from the plan doc. Bailey tunes the exact payment/reliability
    /// numbers before this reaches players — they are placeholders here, not balanced values.
    /// </summary>
    public static class RouteContractCatalogue
    {
        public static readonly RouteContractDefinition RegionalKingscoteIntro = new(
            id: "REG-KGC-INTRO",
            originCode: "ADL",
            destinationCode: "KGC",
            eligibleType: AircraftType.Atr42,
            requiredRotations: 5,
            paymentPerRotation: 400,
            completionReward: 1000,
            reliabilityGainPerRotation: 2,
            requiredTier: OperatingTier.Provisional);

        public static readonly IReadOnlyList<RouteContractDefinition> All = new[]
        {
            RegionalKingscoteIntro
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
