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
            var kingscote = DestinationCatalogue.Australia.First(d => d.Code == "KGC");
            ops.ScheduleDeparture(fleet[0], kingscote, new SimulationTime(600));

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
        public void Estimate_UsesTheAircraftsOwnTaxiAndTakeoffTimeNotAlwaysTheAtr42()
        {
            // Estimate used to call the untyped AirlineOperations overloads, which default
            // to an ATR 42 regardless of what is actually parked - so a 787's departure
            // preview showed exactly the same taxi/takeoff time as a turboprop's.
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(7), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideStands);
            var player = Airline.Player("Southern Cross Regional", "#C8102E");
            ops.AddAirline(player);
            var atr = ops.AddAircraft(player, "VH-PAA", AircraftType.Atr42, AirlineOperations.AdelaideRegionalBays[0]);
            var jet = ops.AddAircraft(player, "VH-PAJ", AircraftType.Boeing78710, AirlineOperations.AdelaideTerminalGates[0]);

            var depart = new SimulationTime(1000);
            var jetTrip = FlightPlanner.Estimate(jet, 3600, depart);

            // Pin the exact expected value using the aircraft's own type-aware timings,
            // so a regression back to the untyped (always-ATR-42) overloads fails this
            // test even when a stand-class heuristic happens to partly compensate.
            var expectedAirborne = depart.Advance(
                AirlineOperations.TaxiOutSecondsFrom(jet.Stand, jet.Type) + AirlineOperations.TakeoffRunwaySecondsFor(jet.Type));
            Assert.That(jetTrip.Airborne.ElapsedSeconds, Is.EqualTo(expectedAirborne.ElapsedSeconds));

            var atrTrip = FlightPlanner.Estimate(atr, 3600, depart);
            Assert.That(jetTrip.Airborne.ElapsedSeconds, Is.Not.EqualTo(atrTrip.Airborne.ElapsedSeconds),
                "a 787's taxi-out + takeoff roll is not an ATR 42's");
        }

        [Test]
        public void ExpectedBackAt_UsesTheAircraftsOwnTakeoffTimeNotAlwaysTheAtr42()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(7), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideStands);
            var player = Airline.Player("Southern Cross Regional", "#C8102E");
            ops.AddAirline(player);
            var atr = ops.AddAircraft(player, "VH-PAA", AircraftType.Atr42, AirlineOperations.AdelaideRegionalBays[0]);
            var jet = ops.AddAircraft(player, "VH-PAJ", AircraftType.Boeing78710, AirlineOperations.AdelaideTerminalGates[0]);
            var kingscote = DestinationCatalogue.Australia.First(d => d.Code == "KGC");
            var melbourne = DestinationCatalogue.Australia.First(d => d.Code == "MEL");
            ops.ScheduleDeparture(atr, kingscote, new SimulationTime(600));
            ops.ScheduleDeparture(jet, melbourne, new SimulationTime(600));
            clock.Set(new SimulationTime(650));
            ops.Update();

            var atrBack = FlightPlanner.ExpectedBackAt(atr, 3600, clock.Now);
            var jetBack = FlightPlanner.ExpectedBackAt(jet, 3600, clock.Now);
            Assert.That(atrBack.HasValue && jetBack.HasValue, Is.True);

            Assert.That(jet.State, Is.EqualTo(FleetState.TaxiOut), "still taxiing out when the estimate is taken");
            var expectedJetBack = jet.StateEndsAt!.Value.Advance(
                AirlineOperations.TakeoffRunwaySecondsFor(jet.Type) + 3600 * 2 + AirlineOperations.DestinationTurnaroundSeconds);
            Assert.That(jetBack.Value.ElapsedSeconds, Is.EqualTo(expectedJetBack.ElapsedSeconds));
            Assert.That(jetBack.Value.ElapsedSeconds, Is.Not.EqualTo(atrBack.Value.ElapsedSeconds),
                "a 787's takeoff runway time is not an ATR 42's");
        }

        [Test]
        public void NearestWithin_PicksClosestDotInsideRadius()
        {
            var points = new List<(float x, float y)> { (100f, 100f), (110f, 100f), (300f, 300f) };

            Assert.That(FlightPlanner.NearestWithin(points, 108f, 101f), Is.EqualTo(1));
            Assert.That(FlightPlanner.NearestWithin(points, 101f, 99f), Is.EqualTo(0));
            Assert.That(FlightPlanner.NearestWithin(points, 200f, 200f), Is.EqualTo(-1));
        }

        [Test]
        public void NextFreeAircraft_SkipsBookedAndExcluded()
        {
            var (_, ops, fleet) = PlayerFleet(3);
            var kingscote = DestinationCatalogue.Australia.First(d => d.Code == "KGC");
            ops.ScheduleDeparture(fleet[1], kingscote, new SimulationTime(600));

            Assert.That(FlightPlanner.NextFreeAircraft(fleet, "VH-PAA"), Is.SameAs(fleet[2]));
            Assert.That(FlightPlanner.NextFreeAircraft(fleet, null), Is.SameAs(fleet[0]));
        }

        [Test]
        public void ExpectedBackAt_IsNullWhenParkedAndAfterTheTripWhenAway()
        {
            var (clock, ops, fleet) = PlayerFleet(1);
            var plane = fleet[0];
            var kingscote = DestinationCatalogue.Australia.First(d => d.Code == "KGC");
            Assert.That(FlightPlanner.ExpectedBackAt(plane, 3600, clock.Now), Is.Null);

            ops.ScheduleDeparture(plane, kingscote, new SimulationTime(600));
            var airborne = ops.AirborneSeconds(plane, kingscote);
            while (plane.State != FleetState.Outbound && clock.Now.ElapsedSeconds < 7200)
            {
                clock.Advance(5);
                ops.Update();
            }

            Assert.That(plane.State, Is.EqualTo(FleetState.Outbound));
            var back = FlightPlanner.ExpectedBackAt(plane, airborne, clock.Now);
            Assert.That(back.HasValue, Is.True);
            Assert.That(back.Value.ElapsedSeconds, Is.EqualTo(
                plane.StateEndsAt.Value.ElapsedSeconds + AirlineOperations.DestinationTurnaroundSeconds + airborne));
        }
    }
}
