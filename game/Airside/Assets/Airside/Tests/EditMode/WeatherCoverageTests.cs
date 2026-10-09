using System;
using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class WeatherCoverageTests
    {
        [TestCase(0f, 0f)]
        [TestCase(18000f, -14000f)]
        [TestCase(-30000f, 25000f)]
        public void CloudWindowHasCoverageEvenFarOutsideTheAirport(float x, float z)
        {
            var visible = 0;
            for (var i = 0; i < 16; i++)
            {
                var worldX = ((i % 4 + 0.5f) / 4f * 2f - 1f) * WeatherCoverage.CloudHalfWidth;
                var worldZ = ((i / 4 + 0.5f) / 4f * 2f - 1f) * WeatherCoverage.CloudHalfDepth;
                var cloudX = WeatherCoverage.WrapNearView(worldX, x, WeatherCoverage.CloudHalfWidth);
                var cloudZ = WeatherCoverage.WrapNearView(worldZ, z, WeatherCoverage.CloudHalfDepth);
                Assert.That(Math.Abs(cloudX - x), Is.LessThanOrEqualTo(WeatherCoverage.CloudHalfWidth));
                Assert.That(Math.Abs(cloudZ - z), Is.LessThanOrEqualTo(WeatherCoverage.CloudHalfDepth));
                if (WeatherCoverage.CloudEdge(cloudX, cloudZ, x, z) > 0.5f) visible++;
            }
            Assert.That(visible, Is.GreaterThanOrEqualTo(8), "moving outside airport bounds must not empty the sky");
        }

        [Test] public void MovingCameraDoesNotDragInteriorCloudsAlongWithIt()
        {
            for (var view = -500f; view <= 500f; view += 25f)
                Assert.That(WeatherCoverage.WrapNearView(125f, view, WeatherCoverage.CloudHalfWidth), Is.EqualTo(125f));
        }

        [Test] public void RecyclingCloudIsInvisibleOnBothSidesOfItsJump()
        {
            var before = WeatherCoverage.WrapNearView(WeatherCoverage.CloudHalfWidth - 1f, 0f, WeatherCoverage.CloudHalfWidth);
            var after = WeatherCoverage.WrapNearView(WeatherCoverage.CloudHalfWidth + 1f, 0f, WeatherCoverage.CloudHalfWidth);
            Assert.That(before, Is.GreaterThan(0f));
            Assert.That(after, Is.LessThan(0f));
            Assert.That(WeatherCoverage.CloudEdge(before, 0f, 0f, 0f), Is.LessThan(0.0001f));
            Assert.That(WeatherCoverage.CloudEdge(after, 0f, 0f, 0f), Is.LessThan(0.0001f));
        }

        [Test] public void CameraBoundaryCrossingDoesNotPopAVisibleCloud()
        {
            var previousAlpha = -1f;
            for (var camera = -WeatherCoverage.CloudHalfWidth - 200f; camera <= -WeatherCoverage.CloudHalfWidth + WeatherCoverage.CloudFadeWidth; camera += 10f)
            {
                var cloud = WeatherCoverage.WrapNearView(0f, camera, WeatherCoverage.CloudHalfWidth);
                var alpha = WeatherCoverage.CloudEdge(cloud, 0f, camera, 0f);
                if (previousAlpha >= 0f) Assert.That(Math.Abs(alpha - previousAlpha), Is.LessThan(0.025f));
                previousAlpha = alpha;
            }
        }

        [Test] public void DistantAtmosphereFadesGraduallyOnlyAtTheRenderingLimit()
        {
            Assert.That(WeatherCoverage.Fade(15000f, WeatherCoverage.AtmosphereFadeStart, WeatherCoverage.AtmosphereDistance), Is.EqualTo(1f));
            Assert.That(WeatherCoverage.Fade(25000f, WeatherCoverage.AtmosphereFadeStart, WeatherCoverage.AtmosphereDistance), Is.EqualTo(0.5f));
            var previous = 1f;
            for (var distance = 20000f; distance <= 31000f; distance += 100f)
            {
                var alpha = WeatherCoverage.Fade(distance, WeatherCoverage.AtmosphereFadeStart, WeatherCoverage.AtmosphereDistance);
                Assert.That(alpha, Is.InRange(0f, previous));
                Assert.That(previous - alpha, Is.LessThan(0.016f));
                previous = alpha;
            }
            Assert.That(previous, Is.Zero);
        }
    }
}
