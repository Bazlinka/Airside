using System;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class WeatherAuditPolicyTests
    {
        [TestCase(50f, -1f, 0f)]
        [TestCase(230f, 1f, 0f)]
        [TestCase(320f, 0f, -1f)]
        [TestCase(140f, 0f, 1f)]
        [TestCase(410f, -1f, 0f)]
        [TestCase(-130f, 1f, 0f)]
        public void FromBearingTravelsAwayFromReportedWind(float bearing, float x, float z)
        {
            var flow = WeatherWindFlow.FromBearing(bearing, 1f);
            Assert.That(flow.X, Is.EqualTo(x).Within(0.00001f));
            Assert.That(flow.Z, Is.EqualTo(z).Within(0.00001f));
        }

        [TestCase(0)] [TestCase(90)] [TestCase(180)] [TestCase(270)]
        public void CardinalMeteorologicalBearingsRemainOppositeFlow(int bearing)
        {
            var flow = WeatherWindFlow.FromBearing(bearing, 1f);
            var yaw = RunwayWeather.UnityYawFromTrue(bearing) * Math.PI / 180;
            Assert.That(flow.X * Math.Sin(yaw) + flow.Z * Math.Cos(yaw), Is.EqualTo(-1f).Within(0.00001f));
        }

        [TestCase(0)] [TestCase(50)] [TestCase(230)] [TestCase(359)]
        public void AllActualDriftProducersShareDownwindDirection(int bearing)
        {
            var wind = new SurfaceWind(bearing, 10);
            var unit = WeatherWindFlow.FromBearing(bearing, 1f);
            var cloud = WeatherWindFlow.Cloud(wind, true);
            var rain = WeatherWindFlow.Rain(wind, false);
            var storm = WeatherWindFlow.Rain(wind, true);
            var shader = WeatherWindFlow.ShaderGlobal(wind);
            foreach (var sample in new[] { cloud, rain, storm, shader })
            {
                var length = (float)Math.Sqrt(sample.X * sample.X + sample.Z * sample.Z);
                Assert.That(sample.X / length, Is.EqualTo(unit.X).Within(0.00001f));
                Assert.That(sample.Z / length, Is.EqualTo(unit.Z).Within(0.00001f));
            }
            Assert.That(Math.Sqrt(cloud.X * cloud.X + cloud.Z * cloud.Z), Is.EqualTo(10f * 0.5144f * 1.6f).Within(0.00001f));
        }

        [Test]
        public void CalmRetainsExistingPresentationSpeedStyling()
        {
            var calm = new SurfaceWind(50, 0);
            Assert.That(WeatherWindFlow.Cloud(calm, true).X, Is.EqualTo(-2.4f).Within(0.00001f));
            Assert.That(WeatherWindFlow.Cloud(calm, false).X, Is.EqualTo(-0.35f).Within(0.00001f));
            Assert.That(WeatherWindFlow.Rain(calm, false).X, Is.EqualTo(-2.5f).Within(0.00001f));
            Assert.That(WeatherWindFlow.Rain(calm, true).X, Is.EqualTo(-6f).Within(0.00001f));
            Assert.That(WeatherWindFlow.ShaderGlobal(calm).X, Is.EqualTo(-7f).Within(0.00001f));
            var stopped = WeatherWindFlow.FromBearing(50f, 0f);
            Assert.That(stopped.X, Is.Zero);
            Assert.That(stopped.Z, Is.Zero);
        }

        [TestCase(800f, true)] [TestCase(1450f, true)]
        [TestCase(1600f, false)] [TestCase(5000f, false)]
        public void WiperActivationTracksObservedRainAtAltitude(float height, bool active)
        {
            var precipitation = CockpitObserverWeather.Rain(1f, height, true);
            Assert.That(precipitation, Is.EqualTo(CockpitWeatherEnvelope.RainAtHeight(height)));
            Assert.That(CockpitObserverWeather.WipersActive(precipitation), Is.EqualTo(active));
        }

        [TestCase(0f, false)] [TestCase(0.04f, false)] [TestCase(0.041f, true)]
        public void WiperParkingThresholdStaysUnchanged(float rain, bool active)
            => Assert.That(CockpitObserverWeather.WipersActive(rain), Is.EqualTo(active));

        [Test]
        public void ExteriorKeepsAirportRainAndDryWeatherParksWipers()
        {
            Assert.That(CockpitObserverWeather.Rain(0.75f, 5000f, false), Is.EqualTo(0.75f));
            Assert.That(CockpitObserverWeather.WipersActive(CockpitObserverWeather.Rain(0f, 800f, true)), Is.False);
        }

        [Test]
        public void GeneratedStarTemperaturesAndBrightnessRemainDistinct()
        {
            var cool = CelestialStarColour.For(1f, 0.1f);
            var warm = CelestialStarColour.For(1f, 0.9f);
            var dim = CelestialStarColour.For(0.65f, 0.9f);
            Assert.That(cool.B, Is.GreaterThan(cool.R));
            Assert.That(warm.R, Is.GreaterThan(warm.B));
            Assert.That(dim.R, Is.EqualTo(warm.R * 0.65f).Within(0.00001f));
            Assert.That(dim.G, Is.EqualTo(warm.G * 0.65f).Within(0.00001f));
            Assert.That(dim.B, Is.EqualTo(warm.B * 0.65f).Within(0.00001f));
        }

        [Test]
        public void NavigationLightsFollowPilotRelativeNaming()
        {
            var left = AircraftNavigationPalette.For(AirsideAircraftParts.NavigationLightFor("nav_light_left"));
            var right = AircraftNavigationPalette.For(AirsideAircraftParts.NavigationLightFor("NavLight R"));
            var tail = AircraftNavigationPalette.For(AirsideAircraftParts.NavigationLightFor("tail_nav_light"));
            Assert.That(left.R, Is.GreaterThan(left.G));
            Assert.That(left.R, Is.GreaterThan(left.B));
            Assert.That(right.G, Is.GreaterThan(right.R));
            Assert.That(right.G, Is.GreaterThan(right.B));
            Assert.That(tail.R, Is.EqualTo(tail.G));
            Assert.That(tail.B, Is.EqualTo(tail.R).Within(0.06f));
        }
    }
}
