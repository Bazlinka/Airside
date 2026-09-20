using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>One named career achievement and the test that decides whether it's been reached.</summary>
    public readonly struct CareerMilestone
    {
        internal CareerMilestone(string id, string title, bool reached)
        {
            Id = id;
            Title = title;
            Reached = reached;
        }

        public string Id { get; }
        public string Title { get; }
        public bool Reached { get; }
    }

    /// <summary>
    /// A short, fixed list of career achievements, evaluated fresh every time from state
    /// already tracked elsewhere (rotations, tier, fleet size, contract history) — nothing new
    /// to persist, nothing that can fall out of sync with a save.
    /// </summary>
    public static class CareerMilestones
    {
        public static IReadOnlyList<CareerMilestone> Reached(
            AirlineCareerState career, int fleetSize, IReadOnlyList<AircraftType> ownedTypes)
        {
            var list = new List<CareerMilestone>();
            if (career == null)
                return list;

            list.Add(new CareerMilestone("first-rotation", "First rotation completed",
                career.CompletedPlayerRotations >= 1));
            list.Add(new CareerMilestone("ten-rotations", "10 rotations completed",
                career.CompletedPlayerRotations >= 10));
            list.Add(new CareerMilestone("twenty-five-rotations", "25 rotations completed",
                career.CompletedPlayerRotations >= 25));
            list.Add(new CareerMilestone("first-contract", "First contract fulfilled",
                career.ContractHistory.Count >= 1));
            list.Add(new CareerMilestone("regional-tier", "Reached Regional",
                career.Tier >= OperatingTier.Regional));
            list.Add(new CareerMilestone("domestic-tier", "Reached Domestic",
                career.Tier >= OperatingTier.Domestic));
            list.Add(new CareerMilestone("international-tier", "Reached International",
                career.Tier >= OperatingTier.International));
            list.Add(new CareerMilestone("second-aircraft", "Fleet grown past the starter aircraft",
                fleetSize >= 2));
            list.Add(new CareerMilestone("full-fleet", $"Fleet at capacity ({AircraftAcquisition.MaxPlayerAircraft})",
                fleetSize >= AircraftAcquisition.MaxPlayerAircraft));
            list.Add(new CareerMilestone("jet-operator", "First jet in the fleet",
                AirlineCareerState.OwnsAnyJet(ownedTypes)));
            list.Add(new CareerMilestone("elite-reliability", "Reliability at 95 or better",
                career.Reliability >= 95));
            return list;
        }
    }
}
