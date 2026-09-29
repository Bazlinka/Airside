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
        public void EveryTriangle_IsWoundTheWayItsNormalSays_AndStaysInsideTheMap()
        {
            var sink = Asphalt(null, out _);
            var bad = 0;
            var upFacingRibbons = 0;
            foreach (var tile in sink.Tiles)
            {
                var p = tile.Value.Positions;
                var n = tile.Value.Normals;
                var t = tile.Value.Triangles;
                Assert.That(n.Count, Is.EqualTo(p.Count));
                for (var i = 0; i < t.Count; i += 3)
                {
                    int a = t[i], b = t[i + 1], c = t[i + 2];
                    Assert.That(Math.Abs(p[a * 3]), Is.LessThan(20000f));
                    Assert.That(float.IsNaN(p[a * 3 + 1]) || float.IsNaN(p[b * 3]) || float.IsNaN(p[c * 3 + 2]), Is.False);
                    var e1 = (p[b * 3] - p[a * 3], p[b * 3 + 1] - p[a * 3 + 1], p[b * 3 + 2] - p[a * 3 + 2]);
                    var e2 = (p[c * 3] - p[a * 3], p[c * 3 + 1] - p[a * 3 + 1], p[c * 3 + 2] - p[a * 3 + 2]);
                    var gx = e1.Item2 * e2.Item3 - e1.Item3 * e2.Item2;
                    var gy = e1.Item3 * e2.Item1 - e1.Item1 * e2.Item3;
                    var gz = e1.Item1 * e2.Item2 - e1.Item2 * e2.Item1;
                    // geometric normal (b-a)x(c-a) against the vertex normal: flat ribbons face up, parapets face out
                    if (gx * n[a * 3] + gy * n[a * 3 + 1] + gz * n[a * 3 + 2] < -1e-3f)
                        bad++;
                    if (n[a * 3 + 1] > 0.99f)
                        upFacingRibbons++;
                }
            }

            Assert.That(bad, Is.EqualTo(0), "downward or inside-out road triangles");
            Assert.That(upFacingRibbons, Is.GreaterThan(30000));
        }

        [Test]
        public void Bridges_RiseFromTheGroundAtBothEnds_AndHaveParapets()
        {
            var bridges = 0;
            foreach (var road in AdelaideRoadNetwork.Roads)
            {
                var profile = AdelaideRoadGeometry.BridgeProfile.For(road);
                if (profile == null)
                    continue;
                bridges++;
                var p = AdelaideRoadNetwork.Points;
                var first = road.PointStart * 2;
                var last = (road.PointStart + road.PointCount - 1) * 2;
                Assert.That(profile.Lift(p[first], p[first + 1]), Is.LessThan(0.05f), $"way {road.OsmId} starts on the ground");
                Assert.That(profile.Lift(p[last], p[last + 1]), Is.LessThan(0.05f), $"way {road.OsmId} ends on the ground");
                Assert.That(profile.Peak, Is.InRange(0.6f, 3f));
                var mid = (road.PointStart + road.PointCount / 2) * 2;
                Assert.That(profile.Lift(p[mid], p[mid + 1]), Is.GreaterThan(0f).And.LessThanOrEqualTo(profile.Peak + 1e-3f));
            }

            Assert.That(bridges, Is.GreaterThan(10), "the drivable bridges in the data");
            var sink = Asphalt(null, out _);
            var high = 0;
            foreach (var tile in sink.Tiles)
                for (var i = 1; i < tile.Value.Positions.Count; i += 3)
                    if (tile.Value.Positions[i] > 0.6f)
                        high++;
            Assert.That(high, Is.GreaterThan(300), "raised decks and parapets exist");
        }

        [Test]
        public void AdaptiveDensify_SplitsOnlyWhereTheGroundBends()
        {
            // the longest road in the network, so there is something to split
            var road = AdelaideRoadNetwork.Roads[0];
            foreach (var r in AdelaideRoadNetwork.Roads)
                if (r.PointCount > road.PointCount)
                    road = r;
            var original = road.PointCount;
            var flat = AdelaideRoadGeometry.AdaptiveDensify(road.PointStart, road.PointCount,
                new RoadBuildOptions { GroundHeight = (x, z) => 3f });
            var slope = AdelaideRoadGeometry.AdaptiveDensify(road.PointStart, road.PointCount,
                new RoadBuildOptions { GroundHeight = (x, z) => 0.05f * x - 0.02f * z });
            var hills = AdelaideRoadGeometry.AdaptiveDensify(road.PointStart, road.PointCount,
                new RoadBuildOptions { GroundHeight = (x, z) => 3f * (float)Math.Sin(x / 30f) });
            Assert.That(slope.Count / 2, Is.EqualTo(flat.Count / 2), "a straight slope is not a bend");
            Assert.That(flat.Count / 2, Is.LessThanOrEqualTo(original + original / 2 + 4),
                "flat ground adds few points (only over-long segments split)");
            Assert.That(hills.Count, Is.GreaterThan(flat.Count), "hills add points");
            var uniform = AdelaideRoadGeometry.Densify(road.PointStart, road.PointCount, 12f);
            Assert.That(flat.Count, Is.LessThan(uniform.Count), "far fewer than a 12 m cut");
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
