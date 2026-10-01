using System;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class AdelaideAvenuePlacementTests
    {
        [SetUp]
        public void ClearCache()
        {
            AdelaideAvenuePlacement.ClearCache();
            AdelaideWindbreakPlacement.ClearCache();
        }

        [Test]
        public void ResolveNameIndices_MatchApproachRoads()
        {
            var idx = AdelaideAvenuePlacement.ResolveNameIndices();
            Assert.That(idx.Length, Is.EqualTo(AdelaideAvenuePlacement.RoadNames.Length));
            for (var i = 0; i < idx.Length; i++)
            {
                Assert.That(idx[i], Is.GreaterThanOrEqualTo(0), AdelaideAvenuePlacement.RoadNames[i]);
                Assert.That(AdelaideRoadNetwork.Names[idx[i]], Is.EqualTo(AdelaideAvenuePlacement.RoadNames[i]));
            }
        }

        [Test]
        public void Sites_NonEmptyAndUnderCap()
        {
            var sites = AdelaideAvenuePlacement.Sites();
            Assert.That(sites.Length, Is.GreaterThan(0));
            Assert.That(sites.Length, Is.LessThanOrEqualTo(AdelaideAvenuePlacement.MaxTrees));
        }

        [Test]
        public void Sites_AreDeterministicAndSpaced()
        {
            var a = AdelaideAvenuePlacement.Sites();
            AdelaideAvenuePlacement.ClearCache();
            AdelaideWindbreakPlacement.ClearCache();
            var b = AdelaideAvenuePlacement.Sites();
            Assert.That(a.Length, Is.EqualTo(b.Length));
            for (var i = 0; i < a.Length; i++)
            {
                Assert.That(a[i].X, Is.EqualTo(b[i].X));
                Assert.That(a[i].Z, Is.EqualTo(b[i].Z));
            }

            var minDist = AdelaideAvenuePlacement.SpacingMetres * 0.5f;
            for (var i = 0; i < a.Length; i++)
            for (var j = i + 1; j < a.Length; j++)
            {
                var dx = a[i].X - a[j].X;
                var dz = a[i].Z - a[j].Z;
                Assert.That(Math.Sqrt(dx * dx + dz * dz), Is.GreaterThanOrEqualTo(minDist));
            }
        }

        [Test]
        public void Sites_StayOutsideBoundaryAndRunwayStrip()
        {
            var outline = AdelaideBoundary.Outline;
            foreach (var s in AdelaideAvenuePlacement.Sites())
            {
                Assert.That(AdelaideWindbreakPlacement.InsideOutline(outline, s.X, s.Z), Is.False);
                Assert.That(AdelaideAvenuePlacement.OnRunwayStrip(s.X, s.Z), Is.False);
                Assert.That(AdelaideAvenuePlacement.InCorridor(s.X, s.Z), Is.True);
            }
        }

        [Test]
        public void Build_TriangleCountWithinBudget()
        {
            var sink = new RoadMeshSink();
            var drawn = AdelaideAvenueGeometry.Build(sink, new RoadBuildOptions());
            var sites = AdelaideAvenuePlacement.Sites();
            Assert.That(drawn, Is.EqualTo(sites.Length));
            Assert.That(sink.TriangleCount,
                Is.LessThanOrEqualTo(sites.Length * AdelaideAvenueGeometry.MaxTreeTriangles));
            Assert.That(sink.TriangleCount, Is.GreaterThan(0));
        }
    }
}
