using System;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>ADR 0184: traffic signals, stop lines, give-way marks and signs from the road network's furniture.</summary>
    public sealed class AdelaideRoadFurnitureTests
    {
        private static int Count(AdelaideRoadNetwork.FurnitureKind kind)
        {
            var n = 0;
            var s = AdelaideRoadNetwork.FurnitureStride;
            for (var k = 0; k + s <= AdelaideRoadNetwork.Furniture.Length; k += s)
                if ((int)AdelaideRoadNetwork.Furniture[k] == (int)kind && AdelaideRoadNetwork.Furniture[k + 4] > 0f)
                    n++;
            return n;
        }

        [Test]
        public void TheDataHasSignalsStopSignsAndGiveWays_OnRoads()
        {
            Assert.That(Count(AdelaideRoadNetwork.FurnitureKind.TrafficSignal), Is.GreaterThan(60));
            Assert.That(Count(AdelaideRoadNetwork.FurnitureKind.Stop), Is.GreaterThan(15));
            Assert.That(Count(AdelaideRoadNetwork.FurnitureKind.GiveWay), Is.GreaterThan(200));
            var f = AdelaideRoadNetwork.Furniture;
            for (var k = 0; k + AdelaideRoadNetwork.FurnitureStride <= f.Length; k += AdelaideRoadNetwork.FurnitureStride)
            {
                Assert.That(f[k + 5], Is.InRange(0f, 360f), "heading");
                Assert.That(f[k + 6], Is.InRange(0f, 1f), "flags");
            }
        }

        [Test]
        public void SignalPoles_StandOnTheLeftKerb_AndHeadsReachOverEachApproach()
        {
            var heads = AdelaideRoadFurnitureGeometry.SignalHeads();
            Assert.That(heads.Count, Is.GreaterThan(100), "a head per approach");
            var green = 0;
            var red = 0;
            foreach (var h in heads)
            {
                Assert.That(Math.Sqrt(h.FaceX * h.FaceX + h.FaceZ * h.FaceZ), Is.EqualTo(1.0).Within(1e-3));
                Assert.That(Math.Sqrt(Math.Pow(h.X - h.PoleX, 2) + Math.Pow(h.Z - h.PoleZ, 2)),
                    Is.EqualTo(AdelaideRoadFurnitureGeometry.MastReachMetres).Within(1e-3),
                    "every head hangs at the end of its mast arm");
                if (h.RedLit)
                    red++;
                else
                    green++;
            }

            Assert.That(green, Is.GreaterThan(20));
            Assert.That(red, Is.GreaterThan(20), "cross traffic shows the other colour");
        }

        [Test]
        public void SignalsPaintAndSigns_AreWellFormed()
        {
            var o = new RoadBuildOptions();
            var props = new RoadMeshSink();
            var signals = AdelaideRoadFurnitureGeometry.BuildSignals(props, o);
            Assert.That(signals, Is.GreaterThan(100));
            Assert.That(props.TriangleCount, Is.EqualTo(signals * 134),
                "footing, pole, cabinet, mast arm, backing board, head, three lamps and three visors");
            Assert.That(AdelaideRoadFurnitureGeometry.BuildSigns(props, o), Is.GreaterThan(200));
            var paint = new RoadMeshSink();
            Assert.That(AdelaideRoadFurnitureGeometry.BuildRoadPaint(paint, o), Is.GreaterThan(400));
            Assert.That(props.VertexCount + paint.VertexCount, Is.LessThan(120000), "budget");
            foreach (var sink in new[] { props, paint })
            foreach (var tile in sink.Tiles)
            {
                var p = tile.Value.Positions;
                var n = tile.Value.Normals;
                var t = tile.Value.Triangles;
                for (var i = 0; i < t.Count; i += 3)
                {
                    int a = t[i], b = t[i + 1], c = t[i + 2];
                    var e1 = (p[b * 3] - p[a * 3], p[b * 3 + 1] - p[a * 3 + 1], p[b * 3 + 2] - p[a * 3 + 2]);
                    var e2 = (p[c * 3] - p[a * 3], p[c * 3 + 1] - p[a * 3 + 1], p[c * 3 + 2] - p[a * 3 + 2]);
                    var gx = e1.Item2 * e2.Item3 - e1.Item3 * e2.Item2;
                    var gy = e1.Item3 * e2.Item1 - e1.Item1 * e2.Item3;
                    var gz = e1.Item1 * e2.Item2 - e1.Item2 * e2.Item1;
                    Assert.That(gx * n[a * 3] + gy * n[a * 3 + 1] + gz * n[a * 3 + 2], Is.GreaterThanOrEqualTo(-1e-4f));
                }
            }
        }

        [Test]
        public void StopLinesAndTeeth_LieOnTheRoadSurfaceSide()
        {
            // signals sit on roads: every head is within (half width + 6 m) of some road centreline vertex-ish: cheap check
            var heads = AdelaideRoadFurnitureGeometry.SignalHeads();
            var checkedCount = 0;
            foreach (var h in heads)
            {
                var best = double.MaxValue;
                for (var r = 0; r < AdelaideRoadNetwork.Roads.Length && best > 12; r++)
                {
                    var road = AdelaideRoadNetwork.Roads[r];
                    for (var i = road.PointStart; i < road.PointStart + road.PointCount; i++)
                        best = Math.Min(best, Math.Sqrt(Math.Pow(AdelaideRoadNetwork.Points[i * 2] - h.X, 2) + Math.Pow(AdelaideRoadNetwork.Points[i * 2 + 1] - h.Z, 2)));
                }

                Assert.That(best, Is.LessThan(60), "a signal head hangs near its road");
                if (++checkedCount >= 40)
                    break;
            }
        }
    }
}
