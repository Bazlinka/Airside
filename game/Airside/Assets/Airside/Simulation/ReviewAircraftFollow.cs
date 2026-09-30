using System;

namespace Airside.Simulation
{
    /// <summary>
    /// Packaged-build review helpers for <c>-airsideReviewAircraft</c>.
    /// Keeps auto-pick ranking free of UnityEngine so EditMode / harness tests can lock it.
    /// </summary>
    public static class ReviewAircraftFollow
    {
        public const string AutoLandingToken = "auto-landing";
        public const string AutoTakeoffToken = "auto-takeoff";

        public static bool IsAutoLandingToken(string value) =>
            MatchesToken(value, AutoLandingToken);

        public static bool IsAutoTakeoffToken(string value) =>
            MatchesToken(value, AutoTakeoffToken);

        public static bool IsAutoFollowToken(string value) =>
            IsAutoLandingToken(value) || IsAutoTakeoffToken(value);

        private static bool MatchesToken(string value, string token) =>
            !string.IsNullOrEmpty(value)
            && string.Equals(value, token, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Rank a candidate for an arrival follow shot. Lower is better.
        /// Returns a negative value when the aircraft is not a candidate.
        /// Within the same phase band, jets (terminal-gate types) beat turboprops
        /// so tyre-pivot stills prefer the ADR 0194 jet case.
        /// </summary>
        public static int AutoLandingRank(FleetState state, bool hasView, bool preferJet)
        {
            if (!hasView)
                return -1;

            var phaseRank = state switch
            {
                FleetState.Landing => 0,
                FleetState.HoldingForLanding => 1,
                FleetState.Inbound => 2,
                FleetState.GoAround => 3,
                _ => -1
            };
            if (phaseRank < 0)
                return -1;

            return phaseRank * 2 + (preferJet ? 0 : 1);
        }

        /// <summary>
        /// Rank a candidate for a departure / rotation follow shot. Lower is better.
        /// Jets preferred within the same phase band (ADR 0194 tyre pivot).
        /// </summary>
        public static int AutoTakeoffRank(FleetState state, bool hasView, bool preferJet)
        {
            if (!hasView)
                return -1;

            var phaseRank = state switch
            {
                FleetState.TakingOff => 0,
                FleetState.HoldingShort => 1,
                FleetState.TaxiOut => 2,
                FleetState.Outbound => 3,
                _ => -1
            };
            if (phaseRank < 0)
                return -1;

            return phaseRank * 2 + (preferJet ? 0 : 1);
        }
    }
}
