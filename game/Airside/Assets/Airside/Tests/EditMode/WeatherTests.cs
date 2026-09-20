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
        public void WeatherLook_ThickensFromClearToStormWithoutChangingTiming()
        {
            var clear = WeatherLook.For(WeatherKind.Clear);
            var cloudy = WeatherLook.For(WeatherKind.Cloudy);
            var overcast = WeatherLook.For(WeatherKind.Overcast);
            var rain = WeatherLook.For(WeatherKind.Rain);
            var storm = WeatherLook.For(WeatherKind.Storm);

            Assert.That(clear.IsRaining, Is.False);
            Assert.That(cloudy.CloudCover, Is.GreaterThan(clear.CloudCover));
            Assert.That(overcast.CloudCover, Is.GreaterThan(cloudy.CloudCover));
            Assert.That(overcast.Gloom, Is.GreaterThan(cloudy.Gloom));
            Assert.That(rain.IsRaining, Is.True);
            Assert.That(rain.Wetness, Is.GreaterThan(0.4f));
            Assert.That(storm.Precipitation, Is.GreaterThan(rain.Precipitation));
            Assert.That(storm.Gloom, Is.GreaterThan(rain.Gloom));
            Assert.That(storm.Visibility, Is.LessThan(clear.Visibility));
            Assert.That(Weather.IsAdverse(WeatherKind.Cloudy), Is.False);
            Assert.That(Weather.Look(WeatherKind.Storm).Gloom, Is.EqualTo(storm.Gloom));
        }
    }
}
