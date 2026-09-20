using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
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

    /// <summary>Career-tier progress, derived only — never stored (ADR 0053/0056's tiers already are).</summary>
    public static class CareerProgress
    {
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
