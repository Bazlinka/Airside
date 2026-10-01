using System.Collections.Generic;

namespace Airside.Simulation
{
    /// <summary>
    /// Packaged-build review helper for <c>-airsideReviewFreighter</c>.
    /// Picks a parked, unbooked player aircraft to refit for ADR 0194 cargo-livery stills.
    /// Kept free of UnityEngine so EditMode / harness tests can lock soak seed coverage.
    /// </summary>
    public static class ReviewFreighterPick
    {
        /// <summary>
        /// Best parked player aircraft for a freighter still. Prefers jets (terminal-gate types).
        /// Returns null when none are AtStand without a booked flight or existing freighter flag.
        /// </summary>
        public static FleetAircraft PickBest(IEnumerable<FleetAircraft> playerFleet)
        {
            if (playerFleet == null)
                return null;

            FleetAircraft best = null;
            foreach (var aircraft in playerFleet)
            {
                if (aircraft == null
                    || aircraft.State != FleetState.AtStand
                    || aircraft.Scheduled.HasValue
                    || aircraft.IsFreighter)
                    continue;

                if (best == null)
                {
                    best = aircraft;
                    continue;
                }

                var jet = AirlineOperations.NeedsTerminalGate(aircraft.Type);
                var bestJet = AirlineOperations.NeedsTerminalGate(best.Type);
                if (jet && !bestJet)
                    best = aircraft;
            }

            return best;
        }
    }
}
