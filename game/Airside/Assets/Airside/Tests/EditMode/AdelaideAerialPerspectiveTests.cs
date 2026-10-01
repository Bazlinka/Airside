using System;
using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class AdelaideAerialPerspectiveTests
    {
        [Test]
        public void Strength_IsZeroNearFieldAndRisesWithDistance()
        {
            Assert.That(AdelaideAerialPerspective.Strength01(5000f, 50f), Is.EqualTo(0f));
            Assert.That(AdelaideAerialPerspective.Strength01(30000f, 50f),
                Is.GreaterThan(AdelaideAerialPerspective.Strength01(15000f, 50f)));
            Assert.That(AdelaideAerialPerspective.Strength01(50000f, 50f), Is.GreaterThan(0.7f));
        }

        [Test]
        public void Strength_IsStrongerOnHighGroundAtSameDistance()
        {
            var plain = AdelaideAerialPerspective.Strength01(25000f, 40f);
            var hills = AdelaideAerialPerspective.Strength01(25000f, 400f);
            Assert.That(hills, Is.GreaterThan(plain));
        }

        [Test]
        public void Apply_PushesLandTowardCoolHaze()
        {
            float r = 0.43f, g = 0.41f, b = 0.27f;
            AdelaideAerialPerspective.ApplyRgb(ref r, ref g, ref b, 45000f, 300f);
            Assert.That(b, Is.GreaterThan(0.27f));
            Assert.That(b / Math.Max(1e-4f, r), Is.GreaterThan(0.27f / 0.43f));
        }

        [Test]
        public void Apply_IsIdentityAtZeroStrength()
        {
            float r = 0.5f, g = 0.45f, b = 0.3f;
            AdelaideAerialPerspective.ApplyRgb(ref r, ref g, ref b, 1000f, 20f);
            Assert.That(r, Is.EqualTo(0.5f));
            Assert.That(g, Is.EqualTo(0.45f));
            Assert.That(b, Is.EqualTo(0.3f));
        }
    }
}
