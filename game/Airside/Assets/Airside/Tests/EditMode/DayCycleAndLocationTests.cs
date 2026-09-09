using System;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class DayCycleAndLocationTests
    {
        // Seconds from the start of the game (08:00, day one) to the first time it is `localHour`.
        private static SimulationTime At(double localHour)
        {
            var fromStart = ((localHour - 8.0 + 24.0) % 24.0) / 24.0 * DayCycle.DaySeconds;
            return new SimulationTime((long)System.Math.Round(fromStart));
        }

        [Test]
        public void DayCycle_StartsAtEightInTheMorningOnDayOne()
        {
            var cycle = new DayCycle(new SimulationTime(0));

            Assert.That(cycle.Hour, Is.EqualTo(8));
            Assert.That(cycle.Minute, Is.EqualTo(0));
            Assert.That(cycle.DaysElapsed, Is.EqualTo(0));
            Assert.That(cycle.Phase, Is.EqualTo(DayPhase.Day));
        }

        [Test]
        public void DayCycle_ReachesMiddayMiddayAndMidnightAcrossOneSimulatedDay()
        {
            var midday = new DayCycle(At(12.0));
            var midnight = new DayCycle(At(0.0));

            Assert.That(midday.Hour, Is.EqualTo(12));
            Assert.That(midday.Daylight, Is.EqualTo(1.0).Within(0.001));
            Assert.That(midnight.Hour, Is.EqualTo(0));
            Assert.That(midnight.Daylight, Is.EqualTo(0.0).Within(0.001));
            Assert.That(midnight.Phase, Is.EqualTo(DayPhase.Night));
        }

        [Test]
        public void DayCycle_WrapsAndCountsDays()
        {
            var afterTwoDays = new DayCycle(new SimulationTime(DayCycle.DaySeconds * 2));

            Assert.That(afterTwoDays.DaysElapsed, Is.EqualTo(2));
            Assert.That(afterTwoDays.Hour, Is.EqualTo(8), "same local time as the start, two days on");
        }

        [Test]
        public void DayCycle_MarksDawnAndDusk()
        {
            var dawn = new DayCycle(At(6.0));
            var dusk = new DayCycle(At(19.0));

            Assert.That(dawn.Phase, Is.EqualTo(DayPhase.Dawn));
            Assert.That(dusk.Phase, Is.EqualTo(DayPhase.Dusk));
            Assert.That(dawn.Daylight, Is.GreaterThan(0.0).And.LessThan(1.0));
            Assert.That(dusk.Daylight, Is.GreaterThan(0.0).And.LessThan(1.0));
        }

        [Test]
        public void AirportLocation_ResolvesKnownIdsAndRejectsUnknown()
        {
            Assert.That(AirportLocation.FromId("PLO").Name, Is.EqualTo("Port Lincoln"));
            Assert.That(AirportLocation.FromId("kgc"), Is.EqualTo(AirportLocation.Kingscote));
            Assert.That(AirportLocation.FromId("ADL"), Is.EqualTo(AirportLocation.Adelaide));
            Assert.That(AirportLocation.Default, Is.EqualTo(AirportLocation.Adelaide));
            Assert.That(AirportLocation.TryFromId("nonsense", out _), Is.False);
            Assert.Throws<ArgumentException>(() => AirportLocation.FromId("nonsense"));
            Assert.Throws<ArgumentException>(() => AirportLocation.FromId(""));
            Assert.That(AirportLocation.Presets.Length, Is.GreaterThanOrEqualTo(4));
        }

        [Test]
        public void Simulation_ExposesItsLocationAndLocalTime()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(
                clock, new SeededRandomSource(1), new ReservationTable(), AirportLocation.PortLincoln);

            Assert.That(simulation.Location, Is.EqualTo(AirportLocation.PortLincoln));
            Assert.That(simulation.TimeOfDay.Hour, Is.EqualTo(8));

            clock.Advance(DayCycle.DaySeconds / 2);
            simulation.Update();
            Assert.That(simulation.TimeOfDay.Hour, Is.EqualTo(20));
        }
    }
}
