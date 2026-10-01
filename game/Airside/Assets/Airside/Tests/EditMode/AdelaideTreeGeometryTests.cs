using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class AdelaideTreeGeometryTests
    {
        [Test]
        public void Lobes_AreAtLeastTwoAndUnderTriangleBudget()
        {
            var lobes = AdelaideTreeGeometry.LobesForSeed(120f, -80f);
            Assert.That(lobes.Length, Is.GreaterThanOrEqualTo(AdelaideTreeGeometry.MinLobeCount));
            Assert.That(AdelaideTreeGeometry.CrownTriangleCount(lobes),
                Is.LessThanOrEqualTo(AdelaideTreeGeometry.MaxCrownTriangles));
            Assert.That(AdelaideTreeGeometry.CrownTriangleCount(lobes), Is.EqualTo(28));
        }

        [Test]
        public void WidestRing_SitsInUpperHalfOnEveryLobe()
        {
            var lobes = AdelaideTreeGeometry.LobesForSeed(10f, 20f);
            Assert.That(AdelaideTreeGeometry.WidestRingsInUpperHalf(lobes), Is.True);
            foreach (var lobe in lobes)
            {
                Assert.That(lobe.RingHeightFrac, Is.GreaterThan(0.5f));
                Assert.That(lobe.RingHeightFrac, Is.LessThan(lobe.ApexHeightFrac));
                Assert.That(lobe.BottomHeightFrac, Is.LessThan(lobe.RingHeightFrac));
            }
        }

        [Test]
        public void Layout_IsDeterministicForSameSeed()
        {
            var a = AdelaideTreeGeometry.LobesForSeed(400f, -1200f);
            var b = AdelaideTreeGeometry.LobesForSeed(400f, -1200f);
            Assert.That(a.Length, Is.EqualTo(b.Length));
            for (var i = 0; i < a.Length; i++)
            {
                Assert.That(a[i].OffsetXFrac, Is.EqualTo(b[i].OffsetXFrac));
                Assert.That(a[i].OffsetZFrac, Is.EqualTo(b[i].OffsetZFrac));
                Assert.That(a[i].RadiusScale, Is.EqualTo(b[i].RadiusScale));
                Assert.That(a[i].SideCount, Is.EqualTo(b[i].SideCount));
            }
        }

        [Test]
        public void Layout_VariesWithSeedSoCrownsDoNotClone()
        {
            var a = AdelaideTreeGeometry.LobesForSeed(0f, 0f);
            var b = AdelaideTreeGeometry.LobesForSeed(500f, 300f);
            var same =
                System.Math.Abs(a[1].OffsetXFrac - b[1].OffsetXFrac) < 1e-4f &&
                System.Math.Abs(a[1].OffsetZFrac - b[1].OffsetZFrac) < 1e-4f;
            Assert.That(same, Is.False);
        }

        [Test]
        public void PrimaryLobe_IsFullRadiusAtOrigin()
        {
            var lobes = AdelaideTreeGeometry.LobesForSeed(1f, 1f);
            Assert.That(lobes[0].OffsetXFrac, Is.EqualTo(0f));
            Assert.That(lobes[0].OffsetZFrac, Is.EqualTo(0f));
            Assert.That(lobes[0].RadiusScale, Is.EqualTo(1f));
            Assert.That(lobes[0].SideCount, Is.EqualTo(6));
        }
    }
}
