using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class AirportSimulationTests
    {
        [Test]
        public void FiftyCycles_CompleteWithoutReservationConflicts()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(42), new ReservationTable());

            for (var second = 1; second <= 10000 && simulation.CompletedCycles < 50; second++)
            {
                clock.Advance(1);
                simulation.Update();
            }

            Assert.That(simulation.CompletedCycles, Is.EqualTo(50));
            Assert.That(simulation.ReservationConflicts, Is.Zero);
            Assert.That(simulation.ActiveAircraft.AircraftId, Is.EqualTo("AS-151"));
        }

        [Test]
        public void PriorityCrew_CostsOnceAndIsAppliedToTheActiveTurnaround()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(42), new ReservationTable());
            clock.Advance(57);
            simulation.Update();

            Assert.That(simulation.ActiveAircraft.Phase, Is.EqualTo(AircraftPhase.AtStand));
            Assert.That(simulation.EnablePriorityCrew(), Is.True);
            Assert.That(simulation.EnablePriorityCrew(), Is.False);
            Assert.That(simulation.ActiveTurnaround.PriorityCrewEnabled, Is.True);
            Assert.That(simulation.Economy.Cash, Is.EqualTo(AirportEconomy.StartingCash - AirportEconomy.PriorityCrewCost));
        }

        [Test]
        public void DelayedFlight_ReconcilesRevenueAndDelayInTheLiveSimulation()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(24031996), new ReservationTable());
            clock.Advance(160);
            simulation.Update();

            Assert.That(simulation.ActiveAircraft.Phase, Is.EqualTo(AircraftPhase.Departed));
            Assert.That(simulation.LastDelaySeconds, Is.EqualTo(6));
            Assert.That(simulation.LastDelayCause, Is.EqualTo("Cabin cleaning disruption"));
            Assert.That(simulation.Economy.Cash, Is.EqualTo(25960));
        }

        [Test]
        public void LargeAndSmallTimeSteps_ProduceTheSameSimulationState()
        {
            var smallClock = new ManualSimulationClock(new SimulationTime(0));
            var small = new AirportSimulation(smallClock, new SeededRandomSource(99), new ReservationTable());
            for (var second = 1; second <= 3217; second++)
            {
                smallClock.Advance(1);
                small.Update();
            }

            var largeClock = new ManualSimulationClock(new SimulationTime(0));
            var large = new AirportSimulation(largeClock, new SeededRandomSource(99), new ReservationTable());
            largeClock.Advance(3217);
            large.Update();

            Assert.That(large.CompletedCycles, Is.EqualTo(small.CompletedCycles));
            Assert.That(large.ActiveAircraft.Phase, Is.EqualTo(small.ActiveAircraft.Phase));
            Assert.That(large.AssignedStand, Is.EqualTo(small.AssignedStand));
            Assert.That(large.Reservations.OccupiedResources, Is.EquivalentTo(small.Reservations.OccupiedResources));
        }

        [Test]
        public void ReservationTable_BlocksASecondOwnerAtomically()
        {
            var table = new ReservationTable();
            var first = new StableId("AS-101");
            var second = new StableId("AS-102");

            Assert.That(table.TryReplace(first, new[] { AirportSimulation.Runway }, out _), Is.True);
            Assert.That(table.TryReplace(second, new[] { AirportSimulation.Runway, AirportSimulation.Taxiway }, out var blocked), Is.False);
            Assert.That(blocked, Is.EqualTo(AirportSimulation.Runway));
            Assert.That(table.IsReserved(AirportSimulation.Taxiway), Is.False);
        }

        [Test]
        public void SeededRandomSource_RepeatsItsSequence()
        {
            var first = new SeededRandomSource(1234);
            var second = new SeededRandomSource(1234);

            for (var index = 0; index < 100; index++)
                Assert.That(first.NextInt(0, 10000), Is.EqualTo(second.NextInt(0, 10000)));
        }
    }
}
