using System;
using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class AdelaidePalmPlacementTests
    {
        [SetUp]
        public void ClearCache() => AdelaidePalmPlacement.ClearCache();

        [Test]
        public void Sites_NonEmptyAndUnderCap()
        {
            var sites = AdelaidePalmPlacement.Sites();
            Assert.That(sites.Length, Is.GreaterThan(0));
            Assert.That(sites.Length, Is.LessThanOrEqualTo(AdelaidePalmPlacement.MaxPalms));
        }

        [Test]
        public void Sites_LieInsideLandsidePrecinctAndCarRadius()
        {
            var sites = AdelaidePalmPlacement.Sites();
            var r2 = AdelaidePalmPlacement.PalmRadiusMetres * AdelaidePalmPlacement.PalmRadiusMetres;
            foreach (var s in sites)
            {
                Assert.That(AdelaidePalmPlacement.InPrecinct(s.X, s.Z), Is.True);
                var dx = s.X - AdelaideCarParkGeometry.CentreX;
                var dz = s.Z - AdelaideCarParkGeometry.CentreZ;
                Assert.That(dx * dx + dz * dz, Is.LessThanOrEqualTo(r2));
                Assert.That(s.HeightMetres, Is.InRange(
                    AdelaidePalmPlacement.MinHeightMetres, AdelaidePalmPlacement.MaxHeightMetres));
            }
        }

        [Test]
        public void Sites_KeepClearOfRunwayStrip()
        {
            foreach (var s in AdelaidePalmPlacement.Sites())
                Assert.That(AdelaidePalmPlacement.OnRunwayStrip(s.X, s.Z), Is.False);
        }

        [Test]
        public void Sites_AreDeterministicAndSpaced()
        {
            var a = AdelaidePalmPlacement.Sites();
            AdelaidePalmPlacement.ClearCache();
            var b = AdelaidePalmPlacement.Sites();
            Assert.That(a.Length, Is.EqualTo(b.Length));
            for (var i = 0; i < a.Length; i++)
            {
                Assert.That(a[i].X, Is.EqualTo(b[i].X));
                Assert.That(a[i].Z, Is.EqualTo(b[i].Z));
                Assert.That(a[i].HeightMetres, Is.EqualTo(b[i].HeightMetres));
            }

            var minDist = AdelaidePalmPlacement.SpacingMetres * 0.55f;
            for (var i = 0; i < a.Length; i++)
            {
                for (var j = i + 1; j < a.Length; j++)
                {
                    var dx = a[i].X - a[j].X;
                    var dz = a[i].Z - a[j].Z;
                    Assert.That(Math.Sqrt(dx * dx + dz * dz), Is.GreaterThanOrEqualTo(minDist));
                }
            }
        }

        [Test]
        public void BuildPalms_TriangleCountWithinBudget()
        {
            var before = new RoadMeshSink();
            var after = new RoadMeshSink();
            var options = new RoadBuildOptions();
            var drawn = AdelaideCarParkGeometry.BuildPalms(after, options);
            var sites = AdelaidePalmPlacement.Sites();
            Assert.That(drawn, Is.EqualTo(sites.Length));
            Assert.That(after.TriangleCount - before.TriangleCount,
                Is.LessThanOrEqualTo(sites.Length * AdelaidePalmGeometry.MaxPalmTriangles));
            Assert.That(after.TriangleCount, Is.GreaterThan(0));
        }
    }
}
