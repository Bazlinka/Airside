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

        public static bool IsAutoLandingToken(string value) =>
            !string.IsNullOrEmpty(value)
            && string.Equals(value, AutoLandingToken, StringComparison.OrdinalIgnoreCase);

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
    }
}
