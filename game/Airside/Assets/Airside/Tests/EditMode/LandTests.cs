using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class LandTests
    {
        [Test]
        public void BaselineLand_IsNotReserved()
        {
            var land = new AirportLand();
            Assert.That(land.SecondRunwayLandReserved, Is.False);
        }

        [Test]
        public void ReserveSecondRunwayLand_CostsMoneyAndIsOneOff()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(31), new ReservationTable());
            var cashBefore = simulation.Economy.Cash;

            Assert.That(simulation.ReserveSecondRunwayLand(), Is.True);
            Assert.That(simulation.Land.SecondRunwayLandReserved, Is.True);
            Assert.That(simulation.Economy.Cash, Is.EqualTo(cashBefore - AirportLand.SecondRunwayLandCost));
            Assert.That(simulation.ReserveSecondRunwayLand(), Is.False);
        }

        [Test]
        public void ReserveSecondRunwayLand_RefusesWithoutEnoughCash()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(37), new ReservationTable());

            while (simulation.Economy.Cash >= AirportLand.SecondRunwayLandCost)
                Assert.That(simulation.Economy.TrySpend(AirportLand.SecondRunwayLandCost - 1), Is.True);

            Assert.That(simulation.ReserveSecondRunwayLand(), Is.False);
            Assert.That(simulation.Land.SecondRunwayLandReserved, Is.False);
        }
    }
}
