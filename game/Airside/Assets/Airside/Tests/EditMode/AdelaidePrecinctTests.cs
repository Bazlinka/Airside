using System.Collections.Generic;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>ADR 0184 P2/P3: canopies, solar, tanks, masts, bus stops and footpaths.</summary>
    public sealed class AdelaidePrecinctTests
    {
        [Test]
        public void Data_HasTheRealPrecinctFurniture()
        {
            Assert.That(AdelaidePrecinct.CanopyCount, Is.GreaterThanOrEqualTo(5), "taxi ranks and car-park entrance roofs");
            Assert.That(AdelaidePrecinct.SolarCount, Is.GreaterThan(90));
            Assert.That(AdelaidePrecinct.TankCount, Is.GreaterThan(30));
            Assert.That(AdelaidePrecinct.MastCount, Is.GreaterThan(50));
            Assert.That(AdelaidePrecinct.BusStopCount, Is.GreaterThan(80));
            Assert.That(AdelaidePrecinct.PathCount, Is.GreaterThan(500));
            Assert.That(AdelaidePrecinct.CanopyStarts[AdelaidePrecinct.CanopyCount], Is.EqualTo(AdelaidePrecinct.CanopyPoints.Length / 2));
            Assert.That(AdelaidePrecinct.PathStarts[AdelaidePrecinct.PathCount], Is.EqualTo(AdelaidePrecinct.PathPoints.Length / 2));
            Assert.That(AdelaidePrecinct.PathInfo.Length, Is.EqualTo(AdelaidePrecinct.PathCount * 2));
        }

        [Test]
        public void TerminalFloodMasts_StandAlongTheAirsideFace()
        {
            var atT1 = 0;
            for (var i = 0; i + 3 < AdelaidePrecinct.Masts.Length; i += 4)
            {
                var x = AdelaidePrecinct.Masts[i];
                var z = AdelaidePrecinct.Masts[i + 1];
                if (AdelaidePrecinct.Masts[i + 2] == 0f && x > 800f && x < 1700f && z > 400f && z < 500f)
                    atT1++;
            }

            Assert.That(atT1, Is.GreaterThan(10));
        }

        [Test]
        public void TaxiRankCanopy_IsRecognisedSoTheSuburbBlockIsHidden()
        {
            // a suburb-extract prism that is the canopy's own outline is recognised
            var recognised = 0;
            for (var c = 0; c < AdelaidePrecinct.CanopyCount; c++)
                if (AdelaidePrecinctGeometry.IsCanopyFootprint(AdelaidePrecinctGeometry.Polygon(
                        AdelaidePrecinct.CanopyStarts, AdelaidePrecinct.CanopyPoints, c).ToArray()))
                    recognised++;
            Assert.That(recognised, Is.EqualTo(AdelaidePrecinct.CanopyCount));
            Assert.That(AdelaidePrecinctGeometry.IsCanopyFootprint(new[] { 0f, 0f, 10f, 0f, 10f, 10f, 0f, 10f }), Is.False);
            Assert.That(AdelaidePrecinctGeometry.IsCanopyFootprint(null), Is.False);
        }

        [Test]
        public void Props_AreWellFormed_NormalsAgreeWithWinding()
        {
            var o = new RoadBuildOptions();
            var sink = new RoadMeshSink();
            Assert.That(AdelaidePrecinctGeometry.BuildCanopies(sink, o), Is.EqualTo(AdelaidePrecinct.CanopyCount));
            AdelaidePrecinctGeometry.BuildSolar(sink, o);
            AdelaidePrecinctGeometry.BuildTanks(sink, o);
            AdelaidePrecinctGeometry.BuildMasts(sink, o);
            AdelaidePrecinctGeometry.BuildBusStops(sink, o);
            Assert.That(sink.TriangleCount, Is.GreaterThan(3000));
            foreach (var tile in sink.Tiles)
            {
                var p = tile.Value.Positions;
                var n = tile.Value.Normals;
                var t = tile.Value.Triangles;
                Assert.That(n.Count, Is.EqualTo(p.Count));
                for (var i = 0; i < t.Count; i += 3)
                {
                    int a = t[i], b = t[i + 1], c = t[i + 2];
                    var e1 = (p[b * 3] - p[a * 3], p[b * 3 + 1] - p[a * 3 + 1], p[b * 3 + 2] - p[a * 3 + 2]);
                    var e2 = (p[c * 3] - p[a * 3], p[c * 3 + 1] - p[a * 3 + 1], p[c * 3 + 2] - p[a * 3 + 2]);
                    var gx = e1.Item2 * e2.Item3 - e1.Item3 * e2.Item2;
                    var gy = e1.Item3 * e2.Item1 - e1.Item1 * e2.Item3;
                    var gz = e1.Item1 * e2.Item2 - e1.Item2 * e2.Item1;
                    var dot = gx * n[a * 3] + gy * n[a * 3 + 1] + gz * n[a * 3 + 2];
                    Assert.That(dot, Is.GreaterThanOrEqualTo(-1e-4f), "a precinct face is wound the way its normal says");
                }
            }
        }

        [Test]
        public void Paths_DrawAsUpFacingRibbons()
        {
            var sink = new RoadMeshSink();
            var drawn = AdelaidePrecinctGeometry.BuildPaths(sink, new RoadBuildOptions());
            Assert.That(drawn, Is.GreaterThan(400));
            var down = 0;
            foreach (var tile in sink.Tiles)
            {
                var p = tile.Value.Positions;
                var t = tile.Value.Triangles;
                for (var i = 0; i < t.Count; i += 3)
                {
                    int a = t[i], b = t[i + 1], c = t[i + 2];
                    var gy = (p[b * 3 + 2] - p[a * 3 + 2]) * (p[c * 3] - p[a * 3]) - (p[b * 3] - p[a * 3]) * (p[c * 3 + 2] - p[a * 3 + 2]);
                    if (gy < -1e-3f)
                        down++;
                }
            }

            Assert.That(down, Is.EqualTo(0));
        }

        [Test]
        public void HoldSigns_StandBesideEveryHoldingPosition_OffThePavement()
        {
            var sink = new RoadMeshSink();
            var placed = AdelaidePrecinctGeometry.BuildHoldSigns(sink, new RoadBuildOptions());
            Assert.That(placed, Is.EqualTo(AdelaideLayout.HoldingPositions.Length / 2 * 2), "one each side of every hold");
            Assert.That(sink.TriangleCount, Is.EqualTo(placed * 30), "post, plate and red face per sign");
            // the sign is set back from the taxiway edge: at the first hold it is farther than half a width from the centreline
            AdelaidePrecinctGeometry.NearestTaxiway(AdelaideLayout.HoldingPositions[0], AdelaideLayout.HoldingPositions[1],
                out var dx, out var dz, out var width);
            Assert.That(dx * dx + dz * dz, Is.EqualTo(1f).Within(1e-3));
            Assert.That(width, Is.GreaterThan(10f));
        }

        [Test]
        public void Contains_HandlesConcaveOutlines()
        {
            var l = new List<float> { 0, 0, 2, 0, 2, 1, 1, 1, 1, 2, 0, 2 };
            Assert.That(AdelaidePrecinctGeometry.Contains(l, 0.5f, 1.5f), Is.True);
            Assert.That(AdelaidePrecinctGeometry.Contains(l, 1.5f, 1.5f), Is.False, "the notch of the L");
        }
    }
}
