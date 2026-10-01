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
        [Test] public void RainStopsAboveDeck()
        {
            Assert.That(CockpitWeatherEnvelope.RainAtHeight(800f), Is.EqualTo(1f));
            Assert.That(CockpitWeatherEnvelope.RainAtHeight(1450f), Is.InRange(0f, 1f));
            Assert.That(CockpitWeatherEnvelope.RainAtHeight(1600f), Is.Zero);
        }
    }
}
