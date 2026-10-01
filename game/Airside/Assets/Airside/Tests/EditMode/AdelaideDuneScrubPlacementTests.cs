using System;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class AdelaideDuneScrubPlacementTests
    {
        [SetUp]
        public void ClearCache() => AdelaideDuneScrubPlacement.ClearCache();

        [Test]
        public void CoastDistance_IsNearZeroOnCoastlineAndLargerInland()
        {
            var coast = AdelaideCoast.Coastline;
            var on = AdelaideCoastLandform.CoastDistanceMetres(coast[40], coast[41]);
            var inland = AdelaideCoastLandform.CoastDistanceMetres(0f, 0f);
            Assert.That(on, Is.LessThan(5f));
            Assert.That(inland, Is.GreaterThan(AdelaideCoastLandform.DuneBandEndMetres));
        }

        [Test]
        public void Sites_NonEmptyAndUnderCap()
        {
            var sites = AdelaideDuneScrubPlacement.Sites();
            Assert.That(sites.Length, Is.GreaterThan(0));
            Assert.That(sites.Length, Is.LessThanOrEqualTo(AdelaideDuneScrubPlacement.MaxBushes));
        }

        [Test]
        public void Sites_AreSandInDuneBandOutsideBoundary()
        {
            var outline = AdelaideBoundary.Outline;
            foreach (var s in AdelaideDuneScrubPlacement.Sites())
            {
                Assert.That(AdelaideLandCover.Sample(s.X, s.Z), Is.EqualTo(AdelaideLandCover.Kind.Sand));
                var coast = AdelaideCoastLandform.CoastDistanceMetres(s.X, s.Z);
                Assert.That(coast, Is.InRange(
                    AdelaideCoastLandform.DuneBandStartMetres,
                    AdelaideCoastLandform.DuneBandEndMetres));
                Assert.That(AdelaideWindbreakPlacement.InsideOutline(outline, s.X, s.Z), Is.False);
                Assert.That(AdelaideDuneScrubPlacement.OnRunwayStrip(s.X, s.Z), Is.False);
                Assert.That(s.X, Is.LessThanOrEqualTo(AdelaideDuneScrubPlacement.WestMaxX));
            }
        }

        [Test]
        public void Sites_AreDeterministicAndSpaced()
        {
            var a = AdelaideDuneScrubPlacement.Sites();
            AdelaideDuneScrubPlacement.ClearCache();
            var b = AdelaideDuneScrubPlacement.Sites();
            Assert.That(a.Length, Is.EqualTo(b.Length));
            for (var i = 0; i < a.Length; i++)
            {
                Assert.That(a[i].X, Is.EqualTo(b[i].X));
                Assert.That(a[i].Z, Is.EqualTo(b[i].Z));
            }

            var minDist = AdelaideDuneScrubPlacement.SpacingMetres * 0.5f;
            for (var i = 0; i < a.Length; i++)
            for (var j = i + 1; j < a.Length; j++)
            {
                var dx = a[i].X - a[j].X;
                var dz = a[i].Z - a[j].Z;
                Assert.That(Math.Sqrt(dx * dx + dz * dz), Is.GreaterThanOrEqualTo(minDist));
            }
        }

        [Test]
        public void Build_TriangleCountWithinBudget()
        {
            var sink = new RoadMeshSink();
            var drawn = AdelaideDuneScrubGeometry.Build(sink, new RoadBuildOptions());
            var sites = AdelaideDuneScrubPlacement.Sites();
            Assert.That(drawn, Is.EqualTo(sites.Length));
            Assert.That(sink.TriangleCount,
                Is.LessThanOrEqualTo(sites.Length * AdelaideDuneScrubGeometry.MaxBushTriangles));
            Assert.That(sink.TriangleCount, Is.GreaterThan(0));
        }
    }
}
