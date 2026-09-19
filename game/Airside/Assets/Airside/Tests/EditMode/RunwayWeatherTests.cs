using Airside.Domain;
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
        public void JetToPerth_Takes23WhenTheWindIsClose()
        {
            Assert.That(DestinationCatalogue.TryFind("PER", out var perth), Is.True);
            var wind = new SurfaceWind(140, 8);
            Assert.That(RunwayWeather.Select(wind, AircraftType.Boeing78710, perth, DestinationCatalogue.Adelaide),
                Is.EqualTo(RunwayDirection.Runway23));
            Assert.That(RunwayWeather.AllowsCrossRunway(AircraftType.Boeing78710), Is.False);
            Assert.That(RunwayWeather.AllowsCrossRunway(AircraftType.Atr42), Is.True);
            Assert.That(RunwayWeather.Label(RunwayDirection.Runway12), Is.EqualTo("12"));
            Assert.That(RunwayWeather.Label(RunwayDirection.Runway30), Is.EqualTo("30"));
        }

        [Test]
        public void Regional_UsesTheCrossStrip_JetStaysOnTheLongStrip()
        {
            var wind12 = new SurfaceWind(123, 10);
            var wind30 = new SurfaceWind(303, 10);
            Assert.That(RunwayWeather.Select(wind12, AircraftType.Atr42, null, null),
                Is.EqualTo(RunwayDirection.Runway12));
            Assert.That(RunwayWeather.Select(wind30, AircraftType.Dash8Q400, null, null),
                Is.EqualTo(RunwayDirection.Runway30));
            Assert.That(RunwayWeather.Select(wind12, AircraftType.Boeing7378, null, null),
                Is.EqualTo(RunwayDirection.Runway05));
            Assert.That(RunwayWeather.Select(wind30, AircraftType.AirbusA321Neo, null, null),
                Is.EqualTo(RunwayDirection.Runway23));
            Assert.That(RunwayWeather.IsMainRunway(RunwayDirection.Runway05), Is.True);
            Assert.That(RunwayWeather.IsMainRunway(RunwayDirection.Runway12), Is.False);
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
