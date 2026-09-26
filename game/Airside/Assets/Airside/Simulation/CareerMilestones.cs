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

            // Achievements are keepsakes, not requirements: none repeats a CareerRoadmap goal or a
            // tier (those have their own track), and fleet size is the whole airline, outstations
            // included, so "fleet at capacity" is reachable.
            list.Add(new CareerMilestone("first-rotation", "First service flown",
                career.CompletedPlayerRotations >= 1));
            list.Add(new CareerMilestone("first-contract", "First contract fulfilled",
                career.CompletedContractIds.Count >= 1));
            list.Add(new CareerMilestone("second-aircraft", "Fleet grown past the starter aircraft",
                fleetSize >= 2));
            list.Add(new CareerMilestone("ten-contracts", "10 contracts fulfilled",
                career.CompletedContractIds.Count >= 10));
            list.Add(new CareerMilestone("jet-operator", "First jet in the fleet",
                AirlineCareerState.OwnsAnyJet(ownedTypes)));
            list.Add(new CareerMilestone("first-outstation", "First outstation base",
                career.OutstationBases.Count >= 1));
            list.Add(new CareerMilestone("century", "100 services flown",
                career.CompletedPlayerRotations >= 100));
            list.Add(new CareerMilestone("widebody-operator", "First widebody in the fleet",
                OwnsWidebody(ownedTypes)));
            list.Add(new CareerMilestone("elite-reliability", "Reliability at 95 or better",
                career.Reliability >= 95 && career.CompletedPlayerRotations >= 50));
            list.Add(new CareerMilestone("full-fleet", $"Fleet at capacity ({AircraftAcquisition.MaxPlayerAircraft})",
                fleetSize >= AircraftAcquisition.MaxPlayerAircraft));
            list.Add(new CareerMilestone("established", "Established airline",
                career.FinaleReached));
            return list;
        }

        private static bool OwnsWidebody(IReadOnlyList<AircraftType> owned)
        {
            if (owned == null) return false;
            foreach (var type in owned)
                if (type != null && AircraftCatalogue.IsWidebody(type)) return true;
            return false;
        }
    }
}
