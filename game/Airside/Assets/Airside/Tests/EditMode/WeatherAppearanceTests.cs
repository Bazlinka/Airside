using System;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class WeatherAppearanceTests
    {
        [Test]
        public void RainVarietiesRemainRainOperationallyButHaveDistinctLooks()
        {
            var drizzle = false; var showers = false; var rain = false;
            for (long block = 0; block < 12; block++)
            {
                var look = WeatherAppearance.Variant(WeatherKind.Rain, block);
                var name = WeatherAppearance.Describe(WeatherKind.Rain, look);
                drizzle |= name == "Drizzle"; showers |= name == "Showers"; rain |= name == "Rain";
                Assert.That(look.IsRaining, Is.True);
                Assert.That(look.Wetness, Is.GreaterThan(0f));
            }
            Assert.That(drizzle && showers && rain, Is.True);
        }

        [Test]
        public void ForecastDoesNotSnapAtHourBoundaries()
        {
            for (long block = 2; block < 72; block++)
            {
                var before = WeatherAppearance.Forecast(new SimulationTime(block * Weather.BlockSeconds - 1));
                var after = WeatherAppearance.Forecast(new SimulationTime(block * Weather.BlockSeconds));
                Assert.That(after.CloudCover, Is.EqualTo(before.CloudCover).Within(0.00001f));
                Assert.That(after.Precipitation, Is.EqualTo(before.Precipitation).Within(0.00001f));
                Assert.That(after.Visibility, Is.EqualTo(before.Visibility).Within(0.00001f));
            }
        }

        [Test]
        public void HighWispsAndStratiformBanksYieldToDevelopedStorms()
        {
            Assert.That(WeatherAppearance.Cirrus(0, 0.12f, 0f), Is.EqualTo(1f));
            Assert.That(WeatherAppearance.Cirrus(1, 0.12f, 0f), Is.Zero);
            Assert.That(WeatherAppearance.Stratus(0.95f, 0f), Is.EqualTo(1f));
            for (var index = 0; index < 16; index++)
            {
                Assert.That(WeatherAppearance.Cirrus(index, 0.95f, 1f), Is.Zero);
                Assert.That(WeatherAppearance.Stratus(0.95f, 1f), Is.Zero);
            }
        }

        [Test]
        public void CoverageKeepsWholeStormBodiesAwayFromRecyclingSeams()
        {
            Assert.That(WeatherCoverage.CloudFadeWidth, Is.GreaterThanOrEqualTo(2500f));
            Assert.That(WeatherCoverage.CloudFadeDepth, Is.GreaterThanOrEqualTo(2500f));
            Assert.That(WeatherCoverage.CloudEdge(5000f, 5000f, 0f, 0f), Is.EqualTo(1f));
        }
    }
}
