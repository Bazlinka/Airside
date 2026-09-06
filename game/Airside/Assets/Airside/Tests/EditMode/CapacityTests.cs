using System.Collections.Generic;
using System.IO;
using Airside.Domain;
using Airside.Persistence;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class CapacityTests
    {
        [Test]
        public void BaselineCapacity_KeepsTwoStands()
        {
            var capacity = new AirportCapacity();
            Assert.That(capacity.StandCount, Is.EqualTo(AirportCapacity.BaselineStands));
            Assert.That(capacity.HasThirdStand, Is.False);
            Assert.That(capacity.CanExpand, Is.True);
        }

        [Test]
        public void BuildThirdStand_CostsMoneyAndConnectsStandThree()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(7), new ReservationTable());
            var cashBefore = simulation.Economy.Cash;

            Assert.That(simulation.BuildThirdStand(), Is.True);
            Assert.That(simulation.Capacity.HasThirdStand, Is.True);
            Assert.That(simulation.Capacity.StandCount, Is.EqualTo(3));
            Assert.That(simulation.Economy.Cash, Is.EqualTo(cashBefore - AirportCapacity.ThirdStandCost));
            Assert.That(simulation.BuildThirdStand(), Is.False);

            var route = simulation.TaxiNetwork.RouteTo(AirportSimulation.StandThree);
            Assert.That(route.SegmentIds[2], Is.EqualTo(AirportTaxiNetwork.StandThreeLeadIn));
            Assert.That(route.Points[route.Points.Count - 1].Z, Is.EqualTo(26f));
        }

        [Test]
        public void AfterExpansion_PrimaryFlightCanUseStandThree()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(99), new ReservationTable());
            Assert.That(simulation.BuildThirdStand(), Is.True);

            var seen = new HashSet<string>();
            for (var second = 1; second <= 12000; second++)
            {
                clock.Advance(1);
                simulation.Update();
                seen.Add(simulation.AssignedStand.Value);
                if (seen.Count == 3)
                    break;
            }

            Assert.That(seen, Does.Contain(AirportSimulation.StandThree.Value));
            Assert.That(seen.Count, Is.EqualTo(3));
        }

        [Test]
        public void AlternateStand_PreservesTwoStandRule_AndPrefersLowestFreeStand()
        {
            Assert.That(AirportSimulation.AlternateStand(AirportSimulation.StandOne, 2),
                Is.EqualTo(AirportSimulation.StandTwo));
            Assert.That(AirportSimulation.AlternateStand(AirportSimulation.StandTwo, 2),
                Is.EqualTo(AirportSimulation.StandOne));

            Assert.That(AirportSimulation.AlternateStand(AirportSimulation.StandOne, 3),
                Is.EqualTo(AirportSimulation.StandTwo));
            Assert.That(AirportSimulation.AlternateStand(AirportSimulation.StandTwo, 3),
                Is.EqualTo(AirportSimulation.StandOne));
            Assert.That(AirportSimulation.AlternateStand(AirportSimulation.StandThree, 3),
                Is.EqualTo(AirportSimulation.StandOne));
        }

        [Test]
        public void BuildStandCommand_SurvivesReload()
        {
            var directory = Path.Combine(Path.GetTempPath(), "airside-capacity-" + System.Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, "save.json");
            Directory.CreateDirectory(directory);
            try
            {
                var session = PersistentAirportSession.LoadOrCreate(path, 1_000_000, 42);
                session.AdvanceTo(1);
                Assert.That(session.BuildThirdStand(), Is.True);
                session.Save(1_000_010);

                var restored = PersistentAirportSession.LoadOrCreate(path, 1_000_020, 42);
                Assert.That(restored.Simulation.Capacity.HasThirdStand, Is.True);
                Assert.That(restored.Simulation.Economy.Cash,
                    Is.EqualTo(AirportEconomy.StartingCash - AirportCapacity.ThirdStandCost));
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }
    }
}
