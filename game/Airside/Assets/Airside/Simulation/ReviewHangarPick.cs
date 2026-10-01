using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// Packaged-build review helper for <c>-airsideReviewHangarCheck</c>.
    /// Picks a parked player aircraft to start a hangar check for ADR 0186–0188 tow stills.
    /// Kept free of UnityEngine so EditMode / harness tests can lock soak seed coverage.
    /// </summary>
    public static class ReviewHangarPick
    {
        /// <summary>
        /// Best parked player aircraft for a hangar-tow still. Prefers a non-founding type
        /// so the founding Saab stays available for soak auto-dispatch / boarding.
        /// Returns null when none are AtStand without a booked flight, freighter flag, or active check.
        /// </summary>
        public static FleetAircraft PickBest(IEnumerable<FleetAircraft> playerFleet, SimulationTime now)
        {
            if (playerFleet == null)
                return null;

            FleetAircraft best = null;
            foreach (var aircraft in playerFleet)
            {
                if (aircraft == null
                    || aircraft.State != FleetState.AtStand
                    || aircraft.Scheduled.HasValue
                    || aircraft.IsFreighter
                    || Maintenance.InCheck(aircraft, now))
                    continue;

                if (best == null || (!aircraft.IsFoundingAircraft && best.IsFoundingAircraft))
                    best = aircraft;
            }

            return best;
        }
    }
}
