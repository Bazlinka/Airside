using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class CockpitWeatherEnvelopeTests
    {
        [TestCase(0f)] [TestCase(800f)] [TestCase(1600f)] [TestCase(4000f)]
        public void OutsideDeckHasNoCloudFog(float height)
            => Assert.That(CockpitWeatherEnvelope.InCloud(height, 1f), Is.Zero);
        [Test] public void ClearSkyHasNoCloudFog()
            => Assert.That(CockpitWeatherEnvelope.InCloud(1200f, 0.2f), Is.Zero);
        [Test] public void DenseDeckEnvelopsViewer()
            => Assert.That(CockpitWeatherEnvelope.InCloud(1200f, 0.9f), Is.EqualTo(1f));
        [Test] public void StormSkyAndVisibilityClearTogetherAboveClouds()
        {
            Assert.That(CockpitWeatherEnvelope.AboveDeck(1200f, 1f), Is.Zero);
            Assert.That(CockpitWeatherEnvelope.SkyCover(1200f, 1f), Is.EqualTo(1f));
            Assert.That(CockpitWeatherEnvelope.AboveDeck(1600f, 1f), Is.EqualTo(1f));
            Assert.That(CockpitWeatherEnvelope.SkyCover(1600f, 1f), Is.Zero);
            var previous = 0f;
            for (var height = 1350; height <= 1600; height++)
            {
                var above = CockpitWeatherEnvelope.AboveDeck(height, 1f);
                Assert.That(above, Is.GreaterThanOrEqualTo(previous));
                Assert.That(above - previous, Is.LessThan(0.01f), "No sharp visibility pop crossing the top");
                previous = above;
            }
        }
        [Test] public void ClearWeatherHasNoArtificialCloudBreakout()
        {
            Assert.That(CockpitWeatherEnvelope.AboveDeck(5000f, 0.2f), Is.Zero);
            Assert.That(CockpitWeatherEnvelope.SkyCover(5000f, 0.2f), Is.EqualTo(0.2f));
        }
        [Test] public void BrokenCloudRetainsSomeSkyOcclusionAboveTheDeck()
        {
            Assert.That(CockpitWeatherEnvelope.AboveDeck(1600f, 0.5f), Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(CockpitWeatherEnvelope.SkyCover(1600f, 0.5f), Is.EqualTo(0.25f).Within(0.001f));
        }
        [Test] public void RainStopsAboveDeck()
        {
            Assert.That(CockpitWeatherEnvelope.RainAtHeight(800f), Is.EqualTo(1f));
            Assert.That(CockpitWeatherEnvelope.RainAtHeight(1450f), Is.InRange(0f, 1f));
            Assert.That(CockpitWeatherEnvelope.RainAtHeight(1600f), Is.Zero);
        }
    }
}
