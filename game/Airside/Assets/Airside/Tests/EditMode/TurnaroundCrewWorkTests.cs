using System;
using System.Collections.Generic;
using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class TurnaroundCrewWorkTests
    {
        [Test]
        public void EveryFleetTypeCompletesItsWholeLoadBeforeClearingAndNeverDuplicatesBags()
        {
            foreach (var spec in AircraftCatalogue.All.Where(s => !s.Type.IsRotorcraft))
            {
                var type = spec.Type;
                var crew = new List<RampCrewMember>();
                var actions = new List<CrewAction>();
                var scene = new ServiceScene();
                RampCrew.ForActivity(RampActivity.Baggage, 0, AircraftLayout.For(type), crew);
                Assert.That(crew.Count(m => m.Task == RampTask.BaggageCart), Is.EqualTo(TurnaroundCrewWork.BagCarriers(type)), type.Id);
                var sum = Enumerable.Range(0, TurnaroundCrewWork.BagCarriers(type)).Sum(l => TurnaroundCrewWork.CarrierBagCount(type, l));
                Assert.That(sum, Is.EqualTo(TurnaroundCrewWork.BagCount(type)), type.Id);
                var seconds = DeparturePrep.StageSecondsFor(type, DeparturePrepStage.Baggage, PlayerBaseLevel.Starter);
                ServiceChoreography.Act(RampActivity.Baggage, type, seconds - TurnaroundCrewWork.ClearSeconds,
                    seconds, crew, actions, scene);
                Assert.That(scene.TrainLoad, Is.EqualTo(0).Within(0.0001), type.Id);
                Assert.That(actions.All(a => a.Item != CarriedItem.Bag), Is.True, type.Id);
                // Sampling other times, then replaying the same instant, cannot alter the load.
                ServiceChoreography.Act(RampActivity.Baggage, type, 20, seconds, crew, actions, scene);
                var at20 = scene.TrainLoad;
                ServiceChoreography.Act(RampActivity.Baggage, type, seconds, seconds, crew, actions, scene);
                ServiceChoreography.Act(RampActivity.Baggage, type, 20, seconds, crew, actions, scene);
                Assert.That(scene.TrainLoad, Is.EqualTo(at20), type.Id);
            }
        }

        [Test]
        public void AnEarlyBookingCannotPushBackBeforeWorkersFinishAndClear()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(7), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideStands);
            var player = Airline.Player("Crew check", "#334455");
            ops.AddAirline(player);
            var plane = ops.AddAircraft(player, "VH-CREW", AircraftType.Saab340, AirlineOperations.AdelaideRegionalBays[0]);
            DestinationCatalogue.TryFind("KGC", out var destination);
            Assert.That(ops.ScheduleDeparture(plane, destination, new SimulationTime(300)).Accepted, Is.True);
            var oldTimer = DeparturePrep.FuelSeconds + DeparturePrep.CateringSeconds + DeparturePrep.BaggageSeconds + DeparturePrep.BoardingSeconds;
            clock.Set(new SimulationTime(oldTimer));
            ops.Update();
            Assert.That(plane.State, Is.EqualTo(FleetState.AtStand), "the old timer is insufficient for the actual baggage load");
            var ready = DeparturePrep.ReadyAtSeconds(plane);
            Assert.That(ready, Is.GreaterThan(oldTimer));
            Assert.That(DeparturePrep.IsReady(plane, new SimulationTime(ready - 1)), Is.False);
            clock.Set(new SimulationTime(ready));
            ops.Update();
            Assert.That(plane.State, Is.EqualTo(FleetState.TaxiOut));
        }
    }
}
