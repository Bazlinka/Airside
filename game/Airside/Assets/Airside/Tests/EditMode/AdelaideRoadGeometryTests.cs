using System;
using System.Collections.Generic;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>ADR 0184: the pure geometry behind the complete road network (no UnityEngine).</summary>
    public sealed class AdelaideRoadGeometryTests
    {
        private static RoadMeshSink Asphalt(Func<float, float, RoadSurfaceUse> rule, out int drawn)
        {
            var sink = new RoadMeshSink();
            drawn = AdelaideRoadGeometry.BuildAsphalt(sink, new RoadBuildOptions { BaseY = 0f, AirsideRule = rule });
            return sink;
        }

        private static IEnumerable<(float ax, float ay, float az, float bx, float by, float bz, float cx, float cy, float cz)>
            Triangles(RoadMeshSink sink)
        {
            foreach (var tile in sink.Tiles)
            {
                var p = tile.Value.Positions;
                var t = tile.Value.Triangles;
                for (var i = 0; i < t.Count; i += 3)
                    yield return (p[t[i] * 3], p[t[i] * 3 + 1], p[t[i] * 3 + 2],
                        p[t[i + 1] * 3], p[t[i + 1] * 3 + 1], p[t[i + 1] * 3 + 2],
                        p[t[i + 2] * 3], p[t[i + 2] * 3 + 1], p[t[i + 2] * 3 + 2]);
            }
        }

        [Test]
        public void Asphalt_DrawsEveryRoad_NotACappedSubset()
        {
            var sink = Asphalt(null, out var drawn);
            var tunnels = 0;
            foreach (var road in AdelaideRoadNetwork.Roads)
                if (road.Layer < 0 || (road.Flags & AdelaideRoadNetwork.RoadFlags.Tunnel) != 0)
                    tunnels++;
            Assert.That(drawn, Is.EqualTo(AdelaideRoadNetwork.Roads.Length - tunnels));
            Assert.That(sink.TriangleCount, Is.GreaterThan(30000));
        }

        [Test]
        public void EveryTriangle_FacesUp_AndStaysInsideTheMap()
        {
            var sink = Asphalt(null, out _);
            var bad = 0;
            var total = 0;
            foreach (var (ax, ay, az, bx, by, bz, cx, cy, cz) in Triangles(sink))
            {
                total++;
                if ((bz - az) * (cx - ax) - (bx - ax) * (cz - az) < -1e-3f && bad++ < 8)
                    TestContext.Out.WriteLine($"DOWN a=({ax:F1},{az:F1}) b=({bx:F1},{bz:F1}) c=({cx:F1},{cz:F1})");
                Assert.That(Math.Abs(ax), Is.LessThan(8000f));
                Assert.That(float.IsNaN(ay) || float.IsNaN(bx) || float.IsNaN(cz), Is.False);
                // Cross product y of (b-a) x (c-a) = (b.z-a.z)*(c.x-a.x) - (b.x-a.x)*(c.z-a.z) is Unity's up-facing sign.
            }

            TestContext.Out.WriteLine($"DOWN total {bad} of {total}");
            Assert.That(bad, Is.EqualTo(0), "downward-facing road triangles");
        }

        [Test]
        public void SkippingAirsideRoads_RemovesGeometry()
        {
            var all = Asphalt(null, out _);
            var noAirside = Asphalt((x, z) => RoadSurfaceUse.Skip, out _);
            Assert.That(noAirside.TriangleCount, Is.LessThan(all.TriangleCount));
        }

        [Test]
        public void Geometry_SplitsIntoTilesAndStaysWithinTheVertexBudget()
        {
            var asphalt = Asphalt(null, out _);
            var paint = new RoadMeshSink();
            AdelaideRoadGeometry.BuildMarkings(paint, new RoadBuildOptions());
            Assert.That(asphalt.TileCount, Is.GreaterThan(4));
            Assert.That(asphalt.VertexCount + paint.VertexCount, Is.LessThan(1200000), "startup and memory budget");
            Assert.That(paint.VertexCount, Is.GreaterThan(5000), "lane, edge and zebra paint exists");
        }

        [Test]
        public void Crossings_AreDrawnOnRoadsThatCanCarryThem()
        {
            var sink = new RoadMeshSink();
            var count = AdelaideRoadGeometry.BuildCrossings(sink, new RoadBuildOptions());
            Assert.That(count, Is.GreaterThan(100));
            Assert.That(sink.TriangleCount, Is.EqualTo(count * 6 * 2));
        }

        [Test]
        public void Trim_CutsBothEndsAlongTheRun()
        {
            var run = new List<float> { 0f, 0f, 10f, 0f, 20f, 0f };
            var trimmed = AdelaideRoadGeometry.Trim(run, 3f, 4f);
            Assert.That(trimmed[0], Is.EqualTo(3f).Within(1e-4));
            Assert.That(trimmed[trimmed.Count - 2], Is.EqualTo(16f).Within(1e-4));
            Assert.That(AdelaideRoadGeometry.Trim(run, 15f, 15f), Is.Empty, "a run shorter than its trims vanishes");
        }

        [Test]
        public void OffsetRun_StaysTheRightDistanceOffAStraightAndABend()
        {
            var straight = AdelaideRoadGeometry.OffsetRun(new List<float> { 0f, 0f, 100f, 0f }, 2f);
            Assert.That(straight[1], Is.EqualTo(2f).Within(1e-4), "left of +x is +z");
            Assert.That(straight[3], Is.EqualTo(2f).Within(1e-4));

            var bend = AdelaideRoadGeometry.OffsetRun(new List<float> { 0f, 0f, 50f, 0f, 50f, 50f }, 2f);
            // The corner keeps the line a true 2 m off both legs.
            Assert.That(bend[2], Is.EqualTo(48f).Within(1e-3));
            Assert.That(bend[3], Is.EqualTo(2f).Within(1e-3));
        }

        [Test]
        public void Densify_LimitsSegmentLengthKeepingTheEnds()
        {
            var road = AdelaideRoadNetwork.Roads[0];
            var dense = AdelaideRoadGeometry.Densify(road.PointStart, road.PointCount, 5f);
            var src = AdelaideRoadNetwork.Points;
            Assert.That(dense[0], Is.EqualTo(src[road.PointStart * 2]));
            Assert.That(dense[dense.Count - 2], Is.EqualTo(src[(road.PointStart + road.PointCount - 1) * 2]));
            for (var i = 2; i + 1 < dense.Count; i += 2)
                Assert.That(Math.Sqrt(Math.Pow(dense[i] - dense[i - 2], 2) + Math.Pow(dense[i + 1] - dense[i - 1], 2)),
                    Is.LessThanOrEqualTo(5.001));
        }

        [Test]
        public void MarkingPlan_UsesTaggedLanesAndOneWay()
        {
            Assert.That(RoadMarkingPlan.ForRoad(7.5f, 1, true, false).Length, Is.EqualTo(2), "one lane one way: edges only");
            var twoLane = RoadMarkingPlan.ForRoad(12f, 2, false, false);
            Assert.That(Array.Exists(twoLane, l => l.Dashed && l.Offset == 0f), Is.True, "tagged two-lane keeps a dashed centre");
            Assert.That(Array.Exists(twoLane, l => !l.Dashed && Math.Abs(l.Offset) < 0.5f), Is.False, "no double line");
            var wide = RoadMarkingPlan.ForRoad(14f, 0, false, false);
            Assert.That(wide.Length, Is.EqualTo(RoadMarkingPlan.For(14f).Length), "untagged wide road falls back");
            var oneWay3 = RoadMarkingPlan.ForRoad(10.2f, 3, true, false);
            Assert.That(Array.FindAll(oneWay3, l => l.Dashed).Length, Is.EqualTo(2), "two lane lines between three lanes");
            foreach (var line in RoadMarkingPlan.ForRoad(6f, 0, false, true))
                Assert.That(Math.Abs(line.Offset) + line.Width * 0.5f, Is.LessThan(3f));
        }
    }
}
