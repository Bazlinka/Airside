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

        [Test]
        public void MediumJetsGetTheMiddleWakeBand()
        {
            // WakeSeparationSeconds used to classify by a hand-picked list of type IDs - a
            // second, disconnected source of truth from the catalogue's own wingspan data
            // that would silently give any newly added heavy jet only the smallest 90 s
            // separation if its ID were never added to match. It is now derived from
            // AircraftCatalogue.WingspanMetres directly.
            Assert.That(AirlineOperations.WakeSeparationSeconds(Airside.Domain.AircraftType.Boeing7378), Is.EqualTo(120));
            Assert.That(AirlineOperations.WakeSeparationSeconds(Airside.Domain.AircraftType.AirbusA321Neo), Is.EqualTo(120));
            Assert.That(AirlineOperations.WakeSeparationSeconds(Airside.Domain.AircraftType.Saab340), Is.EqualTo(90));
            Assert.That(AirlineOperations.WakeSeparationSeconds(Airside.Domain.AircraftType.Dash8Q400), Is.EqualTo(90));
        }
    }
}
