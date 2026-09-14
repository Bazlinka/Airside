using System.Collections.Generic;
using System.Linq;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class FlightPlannerTests
    {
        private static (ManualSimulationClock clock, AirlineOperations ops, List<FleetAircraft> fleet) PlayerFleet(int count)
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(7), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideRegionalBays);
            var player = Airline.Player("Southern Cross Regional", "#C8102E");
            ops.AddAirline(player);
            var fleet = new List<FleetAircraft>();
            for (var i = 0; i < count; i++)
                fleet.Add(ops.AddAircraft(player, $"VH-PA{(char)('A' + i)}", AircraftType.Atr42,
                    AirlineOperations.AdelaideRegionalBays[i]));
            return (clock, ops, fleet);
        }

        [Test]
        public void ChoosePlanningAircraft_PrefersSelectedThenUnplannedParked()
        {
            var (_, ops, fleet) = PlayerFleet(3);
            var melbourne = DestinationCatalogue.Australia.First(d => d.Code == "MEL");
            ops.ScheduleDeparture(fleet[0], melbourne, new SimulationTime(600));

            Assert.That(FlightPlanner.ChoosePlanningAircraft(fleet, "VH-PAC"), Is.SameAs(fleet[2]));
            Assert.That(FlightPlanner.ChoosePlanningAircraft(fleet, null), Is.SameAs(fleet[1]));
            Assert.That(FlightPlanner.ChoosePlanningAircraft(fleet, "EMU-1"), Is.SameAs(fleet[1]));
            Assert.That(FlightPlanner.ChoosePlanningAircraft(new List<FleetAircraft>(), null), Is.Null);
        }

        [Test]
        public void Cycle_WrapsBothWays()
        {
            var (_, _, fleet) = PlayerFleet(3);

            Assert.That(FlightPlanner.Cycle(fleet, "VH-PAC", 1), Is.SameAs(fleet[0]));
            Assert.That(FlightPlanner.Cycle(fleet, "VH-PAA", -1), Is.SameAs(fleet[2]));
            Assert.That(FlightPlanner.Cycle(fleet, "VH-PAA", 1), Is.SameAs(fleet[1]));
            Assert.That(FlightPlanner.Cycle(fleet, null, -1), Is.SameAs(fleet[2]));
        }

        [Test]
        public void DestinationsFor_ListsReachableFirstNearestFirst()
        {
            var (_, ops, fleet) = PlayerFleet(1);
            var list = FlightPlanner.DestinationsFor(ops, fleet[0]);

            Assert.That(list.Count, Is.EqualTo(ops.MapDestinations().Count()));
            var firstLocked = list.FindIndex(d => !d.Reachable);
            if (firstLocked >= 0)
                Assert.That(list.Skip(firstLocked).All(d => !d.Reachable), Is.True);
            var reachable = list.Where(d => d.Reachable).ToList();
            Assert.That(reachable, Is.Not.Empty);
            for (var i = 1; i < reachable.Count; i++)
                Assert.That(reachable[i].DistanceKm, Is.GreaterThanOrEqualTo(reachable[i - 1].DistanceKm));
            Assert.That(reachable.All(d => d.AirborneSeconds > 0), Is.True);
        }

        [Test]
        public void StepDelay_SnapsToFiveMinutesAndClamps()
        {
            var floor = EngineStartSequence.MinimumDepartureLeadSeconds;
            Assert.That(FlightPlanner.StepDelay(floor, 1), Is.EqualTo(300));
            Assert.That(FlightPlanner.StepDelay(300, -1), Is.EqualTo(floor));
            Assert.That(FlightPlanner.StepDelay(420, -1), Is.EqualTo(300));
            Assert.That(FlightPlanner.StepDelay(420, 1), Is.EqualTo(600));
            Assert.That(FlightPlanner.StepDelay(FlightPlanner.MaxDepartureDelaySeconds, 3),
                Is.EqualTo(FlightPlanner.MaxDepartureDelaySeconds));
        }

        [Test]
        public void Estimate_OrdersTheTripTimeline()
        {
            var (_, _, fleet) = PlayerFleet(1);
            var trip = FlightPlanner.Estimate(fleet[0], 3600, new SimulationTime(1000));

            Assert.That(trip.Airborne.ElapsedSeconds, Is.GreaterThan(1000));
            Assert.That(trip.ArriveDestination.ElapsedSeconds - trip.Airborne.ElapsedSeconds, Is.EqualTo(3600));
            Assert.That(trip.LeaveDestination.ElapsedSeconds - trip.ArriveDestination.ElapsedSeconds,
                Is.EqualTo(AirlineOperations.DestinationTurnaroundSeconds));
            Assert.That(trip.BackAtAdelaide.ElapsedSeconds - trip.LeaveDestination.ElapsedSeconds, Is.EqualTo(3600));
        }

        [Test]
        public void NearestWithin_PicksClosestDotInsideRadius()
        {
            var points = new List<(float x, float y)> { (100f, 100f), (110f, 100f), (300f, 300f) };

            Assert.That(FlightPlanner.NearestWithin(points, 108f, 101f), Is.EqualTo(1));
            Assert.That(FlightPlanner.NearestWithin(points, 101f, 99f), Is.EqualTo(0));
            Assert.That(FlightPlanner.NearestWithin(points, 200f, 200f), Is.EqualTo(-1));
        }
    }
}
