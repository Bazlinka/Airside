using System.Collections.Generic;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class WeatherTests
    {
        [Test]
        public void Weather_IsDeterministicForAGivenTime()
        {
            for (long t = 0; t < 5000; t += 137)
                Assert.That(Weather.At(new SimulationTime(t)), Is.EqualTo(Weather.At(new SimulationTime(t))));
        }

        [Test]
        public void Weather_HoldsForABlockThenCanChange()
        {
            var start = Weather.At(new SimulationTime(0));
            Assert.That(Weather.At(new SimulationTime(Weather.BlockSeconds - 1)), Is.EqualTo(start));

            var seen = new HashSet<WeatherKind>();
            for (long block = 0; block < 60; block++)
                seen.Add(Weather.At(new SimulationTime(block * Weather.BlockSeconds)));
            Assert.That(seen.Count, Is.GreaterThan(1), "weather should vary across the day");
        }

        [Test]
        public void Weather_FavoursMildConditions()
        {
            var mild = 0;
            const int samples = 400;
            for (long block = 0; block < samples; block++)
            {
                var kind = Weather.At(new SimulationTime(block * Weather.BlockSeconds));
                if (kind == WeatherKind.Clear || kind == WeatherKind.Cloudy)
                    mild++;
            }

            Assert.That(mild, Is.GreaterThan(samples / 2), "most of the time the weather is fine");
        }

        [Test]
        public void Simulation_ChargesDailyRunningCostsAtEachMidnight()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(24031996), new ReservationTable());

            // Before the first simulated midnight: no running cost yet.
            clock.Advance(DayCycle.DaySeconds / 4);
            simulation.Update();
            Assert.That(simulation.Economy.TotalOperatingCost, Is.Zero);

            // Cross the first two midnights (at 800s and 2000s for an 08:00 start).
            clock.Advance(DayCycle.DaySeconds * 2);
            simulation.Update();

            var expected = 2 * (AirportSimulation.BaseDailyOperatingCost + simulation.Staffing.DailyWage)
                + Weather.DailyOperatingCost(Weather.At(new SimulationTime(800)))
                + Weather.DailyOperatingCost(Weather.At(new SimulationTime(2000)));
            Assert.That(simulation.Economy.TotalOperatingCost, Is.EqualTo(expected));
        }

        [Test]
        public void DailySettlement_IsIdenticalUnderLargeAndSmallTimeSteps()
        {
            var smallClock = new ManualSimulationClock(new SimulationTime(0));
            var small = new AirportSimulation(smallClock, new SeededRandomSource(99), new ReservationTable());
            for (var second = 1; second <= 3500; second++)
            {
                smallClock.Advance(1);
                small.Update();
            }

            var largeClock = new ManualSimulationClock(new SimulationTime(0));
            var large = new AirportSimulation(largeClock, new SeededRandomSource(99), new ReservationTable());
            largeClock.Advance(3500);
            large.Update();

            Assert.That(large.Economy.TotalOperatingCost, Is.EqualTo(small.Economy.TotalOperatingCost));
            Assert.That(large.Economy.Cash, Is.EqualTo(small.Economy.Cash));
        }
    }
}
