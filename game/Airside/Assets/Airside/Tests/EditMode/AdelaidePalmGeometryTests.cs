using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class AdelaidePalmGeometryTests
    {
        [Test]
        public void Fronds_AtLeastFiveAndUnderTriangleBudget()
        {
            var fronds = AdelaidePalmGeometry.FrondsForSeed(1300f, 500f);
            Assert.That(fronds.Length, Is.InRange(AdelaidePalmGeometry.MinFrondCount, AdelaidePalmGeometry.MaxFrondCount));
            Assert.That(AdelaidePalmGeometry.TriangleCount(fronds),
                Is.LessThanOrEqualTo(AdelaidePalmGeometry.MaxPalmTriangles));
        }

        [Test]
        public void Layout_IsDeterministicForSameSeed()
        {
            var a = AdelaidePalmGeometry.FrondsForSeed(1200f, 600f);
            var b = AdelaidePalmGeometry.FrondsForSeed(1200f, 600f);
            Assert.That(a.Length, Is.EqualTo(b.Length));
            for (var i = 0; i < a.Length; i++)
            {
                Assert.That(a[i].YawRad, Is.EqualTo(b[i].YawRad));
                Assert.That(a[i].PitchRad, Is.EqualTo(b[i].PitchRad));
                Assert.That(a[i].LengthFrac, Is.EqualTo(b[i].LengthFrac));
            }
        }

        [Test]
        public void Layout_VariesWithSeed()
        {
            var a = AdelaidePalmGeometry.FrondsForSeed(1000f, 400f);
            var b = AdelaidePalmGeometry.FrondsForSeed(1400f, 700f);
            var same =
                a.Length == b.Length &&
                System.Math.Abs(a[0].YawRad - b[0].YawRad) < 1e-4f &&
                System.Math.Abs(a[0].PitchRad - b[0].PitchRad) < 1e-4f;
            Assert.That(same, Is.False);
        }

        [Test]
        public void Fronds_DroopBelowHorizontal()
        {
            var fronds = AdelaidePalmGeometry.FrondsForSeed(1100f, 550f);
            foreach (var f in fronds)
                Assert.That(f.PitchRad, Is.LessThan(0f));
        }

        [Test]
        public void TrunkSides_FixedAtFive()
        {
            Assert.That(AdelaidePalmGeometry.TrunkSides, Is.EqualTo(5));
        }
    }
}
