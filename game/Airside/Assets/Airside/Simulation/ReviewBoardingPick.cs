using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// Packaged-build review helper for <c>-airsideReviewBoarding</c>.
    /// Picks a parked regional player aircraft so walkway tape / boarding can run (ADR 0187).
    /// Kept free of UnityEngine so EditMode / harness tests can lock soak seed coverage.
    /// </summary>
    public static class ReviewBoardingPick
    {
        /// <summary>
        /// First parked regional suitable for a boarding still. Jets (terminal-gate types),
        /// freighters, and aircraft already in hangar check are skipped. Scheduled departures
        /// are still returned — the soak caller cancels and rebooks with minimum lead.
        /// </summary>
        public static FleetAircraft PickBest(IEnumerable<FleetAircraft> playerFleet, SimulationTime now)
        {
            if (playerFleet == null)
                return null;

            foreach (var aircraft in playerFleet)
            {
                if (aircraft == null
                    || aircraft.State != FleetState.AtStand
                    || aircraft.IsFreighter
                    || Maintenance.InCheck(aircraft, now)
                    || AirlineOperations.NeedsTerminalGate(aircraft.Type))
                    continue;

                return aircraft;
            }

            return null;
        }
    }
}
