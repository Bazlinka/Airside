using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>Thin runway lines widen with distance so they do not shimmer into dashes.</summary>
    public sealed class AirsidePaintWideningTests
    {
        [Test]
        public void NearLine_KeepsItsRealWidth()
        {
            Assert.That(AirsidePaintWidening.Width(0.9f, 60f, 60f, 900f), Is.EqualTo(0.9f));
            Assert.That(AirsidePaintWidening.Width(0.9f, 250f, 60f, 900f), Is.EqualTo(0.9f));
        }

        [Test]
        public void FarLine_IsHeldToAboutAPixelAndAHalf()
        {
            var distance = 2000f;
            var width = AirsidePaintWidening.Width(0.9f, distance, 60f, 900f);
            var pixels = width / AirsidePaintWidening.PixelMetres(distance, 60f, 900f);
            Assert.That(pixels, Is.EqualTo(AirsidePaintWidening.TargetPixels).Within(0.01f));
        }

        [Test]
        public void Width_GrowsWithDistance_AndIsCapped()
        {
            var last = 0f;
            foreach (var d in new[] { 100f, 500f, 1000f, 2000f, 4000f, 20000f })
            {
                var w = AirsidePaintWidening.Width(0.9f, d, 60f, 900f);
                Assert.That(w, Is.GreaterThanOrEqualTo(last));
                Assert.That(w, Is.LessThanOrEqualTo(0.9f * AirsidePaintWidening.MaxFactor + 1e-4f));
                last = w;
            }
        }

        [Test]
        public void BadScreenOrFov_DoesNotThrowOrShrinkTheLine()
        {
            Assert.That(AirsidePaintWidening.Width(0.9f, 1000f, 60f, 0f), Is.EqualTo(0.9f));
            Assert.That(AirsidePaintWidening.Width(0.9f, 1000f, 0f, 900f), Is.GreaterThanOrEqualTo(0.9f));
        }
    }
}
