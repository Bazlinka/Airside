using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class AdelaideSeasonGrassTintTests
    {
        [Test]
        public void Dryness_PeaksInJanuaryAndGreensInJuly()
        {
            Assert.That(AdelaideSeasonGrassTint.Dryness01(15), Is.EqualTo(1f).Within(0.02f));
            Assert.That(AdelaideSeasonGrassTint.Dryness01(196), Is.EqualTo(0f).Within(0.02f));
        }

        [Test]
        public void Dryness_IsSymmetricAroundPeakDry()
        {
            var a = AdelaideSeasonGrassTint.Dryness01(15 + 40);
            var b = AdelaideSeasonGrassTint.Dryness01(15 - 40);
            Assert.That(a, Is.EqualTo(b).Within(0.02f));
        }

        [Test]
        public void Apply_MakesParkStrawierInSummerNotGreener()
        {
            float r0 = 0.46f, g0 = 0.53f, b0 = 0.39f;
            float r1 = r0, g1 = g0, b1 = b0;
            AdelaideSeasonGrassTint.ApplyRgb(ref r0, ref g0, ref b0, dryness01: 0f);
            AdelaideSeasonGrassTint.ApplyRgb(ref r1, ref g1, ref b1, dryness01: 1f);
            Assert.That(r0, Is.EqualTo(0.46f));
            Assert.That(g0, Is.EqualTo(0.53f));
            Assert.That(r1 / g1, Is.GreaterThan(r0 / g0));
            Assert.That(g1, Is.LessThanOrEqualTo(g0));
        }

        [Test]
        public void AppliesTo_GrassKindsOnly()
        {
            Assert.That(AdelaideSeasonGrassTint.AppliesTo(AdelaideLandCover.Kind.Park), Is.True);
            Assert.That(AdelaideSeasonGrassTint.AppliesTo(AdelaideLandCover.Kind.Scrub), Is.True);
            Assert.That(AdelaideSeasonGrassTint.AppliesTo(AdelaideLandCover.Kind.None), Is.True);
            Assert.That(AdelaideSeasonGrassTint.AppliesTo(AdelaideLandCover.Kind.Golf), Is.False);
            Assert.That(AdelaideSeasonGrassTint.AppliesTo(AdelaideLandCover.Kind.Residential), Is.False);
            Assert.That(AdelaideSeasonGrassTint.AppliesTo(AdelaideLandCover.Kind.Parking), Is.False);
            Assert.That(AdelaideSeasonGrassTint.AppliesTo(AdelaideLandCover.Kind.Sand), Is.False);
        }

        [Test]
        public void Apply_IsDeterministic()
        {
            float r1 = 0.5f, g1 = 0.55f, b1 = 0.4f;
            float r2 = 0.5f, g2 = 0.55f, b2 = 0.4f;
            AdelaideSeasonGrassTint.ApplyRgb(ref r1, ref g1, ref b1, 15);
            AdelaideSeasonGrassTint.ApplyRgb(ref r2, ref g2, ref b2, 15);
            Assert.That(r1, Is.EqualTo(r2));
            Assert.That(g1, Is.EqualTo(g2));
            Assert.That(b1, Is.EqualTo(b2));
        }
    }
}
