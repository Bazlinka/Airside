using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>ADR 0143 — one sky, fog that matches it, and layers that follow the weather.</summary>
    public sealed class AtmosphereLookTests
    {
        private static AtmosphereLook Day(WeatherKind kind, float cameraHeight = 50f) =>
            AtmosphereLook.For(WeatherLook.For(kind), 1f, 0f, false, cameraHeight);

        [Test]
        public void TheSkyCarriesTheWeather()
        {
            var clear = Day(WeatherKind.Clear).Sky;
            var overcast = Day(WeatherKind.Overcast).Sky;
            var storm = Day(WeatherKind.Storm).Sky;
            var fog = Day(WeatherKind.Fog).Sky;
            Assert.That(overcast.Saturation, Is.LessThan(clear.Saturation), "overcast is greyer than a clear sky");
            Assert.That(storm.Luma, Is.LessThan(overcast.Luma * 0.75f), "a storm sky is dark slate");
            Assert.That(fog.Luma, Is.GreaterThan(overcast.Luma), "fog is pale");
            Assert.That(fog.Saturation, Is.LessThan(0.05f));
            var night = AtmosphereLook.For(WeatherLook.For(WeatherKind.Clear), 0f, 0f, false, 50f).Sky;
            Assert.That(night.Luma, Is.LessThan(0.08f));
        }

        [Test]
        public void TheFogMatchesTheSky()
        {
            foreach (WeatherKind kind in System.Enum.GetValues(typeof(WeatherKind)))
            {
                var look = Day(kind);
                Assert.That(System.Math.Abs(look.Fog.Luma - look.Sky.Luma), Is.LessThan(0.08f), kind.ToString());
            }
        }

        [Test]
        public void FogDensity_FollowsVisibility_AndEasesForAHighCamera()
        {
            var clear = Day(WeatherKind.Clear);
            var fog = Day(WeatherKind.Fog);
            Assert.That(fog.FogDensity, Is.GreaterThan(clear.FogDensity * 20f));
            Assert.That(clear.FogDensity, Is.EqualTo(WeatherLook.For(WeatherKind.Clear).FogDensity).Within(1e-7f));

            var fromOverview = Day(WeatherKind.Fog, 1_840f);
            Assert.That(fromOverview.FogDensity, Is.EqualTo(fog.FogDensity * AtmosphereLook.HighCameraFogShare).Within(1e-6f));
            // From the overview in fog the field (≈2.4 km away) still shows through.
            var d = 2_400f * fromOverview.FogDensity;
            Assert.That(System.Math.Exp(-d * d), Is.GreaterThan(0.3));
            // A clear day's arrivals 20 km out are not fogged away.
            var far = 20_000f * Day(WeatherKind.Clear, 1_840f).FogDensity;
            Assert.That(System.Math.Exp(-far * far), Is.GreaterThan(0.9));
        }

        [Test]
        public void Layers_FollowTheWeather()
        {
            Assert.That(Day(WeatherKind.Clear).Stratus, Is.Zero);
            Assert.That(Day(WeatherKind.Overcast).Stratus, Is.GreaterThan(0.3f));
            Assert.That(Day(WeatherKind.Overcast, 1_840f).Stratus, Is.EqualTo(Day(WeatherKind.Overcast).Stratus),
                "the deck must remain visible beneath a camera above cloud height");
            Assert.That(Day(WeatherKind.Fog).Mist, Is.GreaterThan(0.6f));
            Assert.That(Day(WeatherKind.Clear).Mist, Is.Zero);
            var dawn = AtmosphereLook.For(WeatherLook.For(WeatherKind.Clear), 0.5f, 1f, true, 50f);
            var dusk = AtmosphereLook.For(WeatherLook.For(WeatherKind.Clear), 0.5f, 1f, false, 50f);
            Assert.That(dawn.Mist, Is.GreaterThan(0.1f), "a clear dawn has a little ground mist");
            Assert.That(dusk.Mist, Is.Zero);
            Assert.That(Day(WeatherKind.Storm).CloudShade, Is.LessThan(0.6f));
            Assert.That(Day(WeatherKind.Clear).CloudShade, Is.EqualTo(1f));
            Assert.That(Day(WeatherKind.Overcast).HorizonBand, Is.GreaterThan(Day(WeatherKind.Clear).HorizonBand));
        }

        [Test] public void ClimbingThroughCloudHeightDoesNotClearTheEntireOvercastDeck()
        {
            var deck = Day(WeatherKind.Overcast).Stratus;
            for (var height = 650f; height <= 1800f; height += 25f)
                Assert.That(Day(WeatherKind.Overcast, height).Stratus, Is.EqualTo(deck));
            Assert.That(Day(WeatherKind.Clear, 1800f).Stratus, Is.Zero);
        }

        [Test]
        public void TheLook_IsContinuousBetweenWeathers()
        {
            var a = WeatherLook.For(WeatherKind.Cloudy);
            var b = WeatherLook.For(WeatherKind.Rain);
            var previous = AtmosphereLook.For(a, 1f, 0f, false, 50f);
            for (var t = 0.02f; t <= 1f; t += 0.02f)
            {
                var now = AtmosphereLook.For(WeatherLook.Lerp(a, b, t), 1f, 0f, false, 50f);
                Assert.That(System.Math.Abs(now.Sky.Luma - previous.Sky.Luma), Is.LessThan(0.03f));
                Assert.That(System.Math.Abs(now.FogDensity - previous.FogDensity), Is.LessThan(previous.FogDensity * 0.3f + 1e-5f));
                previous = now;
            }
        }
    }
}
