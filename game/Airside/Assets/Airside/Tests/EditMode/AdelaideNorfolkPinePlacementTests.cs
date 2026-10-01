using System;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class AdelaideNorfolkPinePlacementTests
    {
        [SetUp]
        public void ClearCache() => AdelaideNorfolkPinePlacement.ClearCache();

        [Test]
        public void ResolveNameIndex_IsHenleyBeachRoad()
        {
            var idx = AdelaideNorfolkPinePlacement.ResolveNameIndex();
            Assert.That(idx, Is.GreaterThanOrEqualTo(0));
            Assert.That(AdelaideRoadNetwork.Names[idx], Is.EqualTo("Henley Beach Road"));
        }

        [Test]
        public void Sites_NonEmptyAndUnderCap()
        {
            var sites = AdelaideNorfolkPinePlacement.Sites();
            Assert.That(sites.Length, Is.GreaterThan(0));
            Assert.That(sites.Length, Is.LessThanOrEqualTo(AdelaideNorfolkPinePlacement.MaxTrees));
        }

        [Test]
        public void Sites_StayOutsideBoundaryAndAreTallerThanEucalyptAvenues()
        {
            var outline = AdelaideBoundary.Outline;
            foreach (var s in AdelaideNorfolkPinePlacement.Sites())
            {
                Assert.That(AdelaideWindbreakPlacement.InsideOutline(outline, s.X, s.Z), Is.False);
                Assert.That(AdelaideNorfolkPinePlacement.OnRunwayStrip(s.X, s.Z), Is.False);
                Assert.That(s.HeightMetres, Is.GreaterThanOrEqualTo(AdelaideNorfolkPinePlacement.MinHeightMetres));
            }
        }

        [Test]
        public void Sites_AreDeterministicAndSpaced()
        {
            var a = AdelaideNorfolkPinePlacement.Sites();
            AdelaideNorfolkPinePlacement.ClearCache();
            var b = AdelaideNorfolkPinePlacement.Sites();
            Assert.That(a.Length, Is.EqualTo(b.Length));
            var minDist = AdelaideNorfolkPinePlacement.SpacingMetres * 0.5f;
            for (var i = 0; i < a.Length; i++)
            {
                Assert.That(a[i].X, Is.EqualTo(b[i].X));
                for (var j = i + 1; j < a.Length; j++)
                {
                    var dx = a[i].X - a[j].X;
                    var dz = a[i].Z - a[j].Z;
                    Assert.That(Math.Sqrt(dx * dx + dz * dz), Is.GreaterThanOrEqualTo(minDist));
                }
            }
        }

        [Test]
        public void Build_TriangleCountWithinBudget()
        {
            var sink = new RoadMeshSink();
            var drawn = AdelaideNorfolkPineGeometry.Build(sink, new RoadBuildOptions());
            var sites = AdelaideNorfolkPinePlacement.Sites();
            Assert.That(drawn, Is.EqualTo(sites.Length));
            Assert.That(sink.TriangleCount,
                Is.LessThanOrEqualTo(sites.Length * AdelaideNorfolkPineGeometry.MaxTreeTriangles));
            Assert.That(sink.TriangleCount, Is.GreaterThan(0));
        }
    }
}
