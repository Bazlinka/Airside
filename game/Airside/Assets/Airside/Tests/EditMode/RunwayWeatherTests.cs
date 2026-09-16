using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class RunwayWeatherTests
    {
        [TestCase(50, 12, RunwayDirection.Runway05)]
        [TestCase(230, 12, RunwayDirection.Runway23)]
        [TestCase(230, 2, RunwayDirection.Runway05)]
        public void RunwayUsesTheBestHeadwindAndAStableCalmDefault(int direction, int knots, RunwayDirection expected)
        {
            Assert.That(RunwayWeather.Select(new SurfaceWind(direction, knots)), Is.EqualTo(expected));
        }

        [Test]
        public void HeavyAircraftReceiveLongerWakeSpacing()
        {
            Assert.That(AirlineOperations.WakeSeparationSeconds(Airside.Domain.AircraftType.AirbusA350900), Is.EqualTo(180));
            Assert.That(AirlineOperations.WakeSeparationSeconds(Airside.Domain.AircraftType.Boeing78710), Is.EqualTo(180));
            Assert.That(AirlineOperations.WakeSeparationSeconds(Airside.Domain.AircraftType.Atr42), Is.EqualTo(90));
        }
    }
}
