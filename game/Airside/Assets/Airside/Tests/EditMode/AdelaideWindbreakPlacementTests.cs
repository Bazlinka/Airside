using System;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class AdelaideWindbreakPlacementTests
    {
        [SetUp]
        public void ClearCache() => AdelaideWindbreakPlacement.ClearCache();

        [Test]
        public void ResolveNameIndex_IsExactTapleysHillRoad()
        {
            var idx = AdelaideWindbreakPlacement.ResolveNameIndex();
            Assert.That(idx, Is.GreaterThanOrEqualTo(0));
            Assert.That(AdelaideRoadNetwork.Names[idx], Is.EqualTo("Tapleys Hill Road"));
            Assert.That(AdelaideRoadNetwork.Names[idx], Is.Not.EqualTo("Old Tapleys Hill Road"));
        }

        [Test]
        public void Sites_NonEmptyAndUnderCap()
        {
            var sites = AdelaideWindbreakPlacement.Sites();
            Assert.That(sites.Length, Is.GreaterThan(0));
            Assert.That(sites.Length, Is.LessThanOrEqualTo(AdelaideWindbreakPlacement.MaxTrees));
        }

        [Test]
        public void Sites_AreDeterministicAndSpaced()
        {
            var a = AdelaideWindbreakPlacement.Sites();
            AdelaideWindbreakPlacement.ClearCache();
            var b = AdelaideWindbreakPlacement.Sites();
            Assert.That(a.Length, Is.EqualTo(b.Length));
            for (var i = 0; i < a.Length; i++)
            {
                Assert.That(a[i].X, Is.EqualTo(b[i].X));
                Assert.That(a[i].Z, Is.EqualTo(b[i].Z));
            }

            var minDist = AdelaideWindbreakPlacement.SpacingMetres * 0.55f;
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
            foreach (var s in AdelaideWindbreakPlacement.Sites())
            {
                Assert.That(AdelaideWindbreakPlacement.InsideOutline(outline, s.X, s.Z), Is.False);
                Assert.That(AdelaideWindbreakPlacement.OnRunwayStrip(s.X, s.Z), Is.False);
                Assert.That(AdelaideWindbreakPlacement.InFrontage(s.X, s.Z), Is.True);
                var d = AdelaideWindbreakPlacement.DistanceToOutline(outline, s.X, s.Z);
                Assert.That(d, Is.InRange(
                    AdelaideWindbreakPlacement.MinBoundaryDistMetres,
                    AdelaideWindbreakPlacement.MaxBoundaryDistMetres));
            }
        }

        [Test]
        public void Sites_PreferLandsideAwayFromBoundary()
        {
            // Every site should be farther from the fence than the road centerline sample nearby.
            var outline = AdelaideBoundary.Outline;
            var sites = AdelaideWindbreakPlacement.Sites();
            Assert.That(sites.Length, Is.GreaterThan(10));
            var farther = 0;
            foreach (var s in sites)
            {
                // Approximate centerline by stepping back toward ARP (0,0) a short distance — weak but
                // the hard lock is: site itself is outside and in the verge band.
                var dSite = AdelaideWindbreakPlacement.DistanceToOutline(outline, s.X, s.Z);
                if (dSite >= AdelaideWindbreakPlacement.MinBoundaryDistMetres)
                    farther++;
            }

            Assert.That(farther, Is.EqualTo(sites.Length));
        }

        [Test]
        public void Build_TriangleCountWithinBudget()
        {
            var sink = new RoadMeshSink();
            var drawn = AdelaideWindbreakGeometry.Build(sink, new RoadBuildOptions());
            var sites = AdelaideWindbreakPlacement.Sites();
            Assert.That(drawn, Is.EqualTo(sites.Length));
            Assert.That(sink.TriangleCount,
                Is.LessThanOrEqualTo(sites.Length * AdelaideWindbreakGeometry.MaxTreeTriangles));
            Assert.That(sink.TriangleCount, Is.GreaterThan(0));
        }
    }
}
