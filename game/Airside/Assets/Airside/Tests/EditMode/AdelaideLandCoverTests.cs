using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class AdelaideLandCoverTests
    {
        [Test]
        public void Grid_HasExpectedCoverageBands()
        {
            Assert.That(AdelaideLandCover.CoverageResidentialPercent, Is.InRange(15f, 45f));
            Assert.That(AdelaideLandCover.CoverageParkPercent, Is.InRange(3f, 20f));
            Assert.That(AdelaideLandCover.CoverageWaterPercent, Is.InRange(0.2f, 5f));
            Assert.That(AdelaideLandCover.CoverageParkingPercent, Is.InRange(0.5f, 8f));
            Assert.That(AdelaideLandCover.CoverageCommercialPercent, Is.InRange(1f, 12f));
        }

        [Test]
        public void Sample_RoyalAdelaideGolf_IsPark()
        {
            // Centroid of the Royal Adelaide Golf Course polygon in the runway frame.
            Assert.That(AdelaideLandCover.Sample(2433.2f, 5789.2f), Is.EqualTo(AdelaideLandCover.Kind.Park));
        }

        [Test]
        public void Sample_Patawalonga_IsWater()
        {
            Assert.That(AdelaideLandCover.Sample(-2450.2f, -543.5f), Is.EqualTo(AdelaideLandCover.Kind.Water));
        }

        [Test]
        public void Sample_HarbourTown_IsBuiltSurface()
        {
            var kind = AdelaideLandCover.Sample(-549.7f, 883.0f);
            Assert.That(kind == AdelaideLandCover.Kind.Commercial || kind == AdelaideLandCover.Kind.Parking, Is.True,
                $"Harbour Town sampled as {kind}");
        }

        [Test]
        public void Sample_OperationalCore_IsEmpty()
        {
            Assert.That(AdelaideLandCover.InOperationalCore(0f, 0f), Is.True);
            Assert.That(AdelaideLandCover.Sample(0f, 0f), Is.EqualTo(AdelaideLandCover.Kind.None));
            Assert.That(AdelaideLandCover.Sample(200f, 100f), Is.EqualTo(AdelaideLandCover.Kind.None));
        }

        [Test]
        public void Roads_ContainArterialsWithPositiveWidth()
        {
            Assert.That(AdelaideLandCover.Roads, Is.Not.Null);
            Assert.That(AdelaideLandCover.Roads.Length, Is.GreaterThan(100));
            var width = AdelaideLandCover.Roads[0];
            Assert.That(width, Is.InRange(7f, 20f));
            var points = (int)AdelaideLandCover.Roads[1];
            Assert.That(points, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void Attribution_NamesOpenStreetMap()
        {
            Assert.That(AdelaideLandCover.Attribution, Does.Contain("OpenStreetMap"));
        }
    }
}
