using System.Collections.Generic;
using Airside.Domain;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>
    /// Player-facing stand names. The simulation keys stands as BAY-1..BAY-4; at Adelaide
    /// they are real regional bays 50D..50A, and that is what the player should read.
    /// </summary>
    public static class StandNames
    {
        /// <summary>"50C" for a known Adelaide bay, the raw id otherwise, "—" when empty.</summary>
        public static string Short(StableId stand)
        {
            if (string.IsNullOrEmpty(stand.Value))
                return "—";
            foreach (var bay in AdelaideLayout.Bays)
                if (bay.Id == stand.Value)
                    return bay.Reference;
            return stand.Value;
        }

        /// <summary>"Bay 50C" — the simulation's own label, so summaries and HUD agree.</summary>
        public static string Display(StableId stand) => AdelaideGround.StandLabel(stand);

        /// <summary>The free stand with the shortest taxi in, or null when none is free.</summary>
        public static StableId? QuickestToTaxiIn(IEnumerable<StableId> freeStands)
        {
            StableId? best = null;
            var bestSeconds = long.MaxValue;
            foreach (var stand in freeStands)
            {
                var seconds = AirlineOperations.TaxiInSecondsTo(stand);
                if (seconds >= bestSeconds)
                    continue;
                bestSeconds = seconds;
                best = stand;
            }

            return best;
        }
    }
}
