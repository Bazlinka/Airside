using System;
using System.Collections.Generic;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>ADR 0184 P1c: the real car parks, their bays, cars and lamps.</summary>
    public sealed class AdelaideCarParkTests
    {
        private static bool InsideAnyPark(float x, float z)
        {
            for (var p = 0; p < AdelaideCarParks.PolygonCount; p++)
            {
                var a = AdelaideCarParks.PolygonStarts[p];
                var b = AdelaideCarParks.PolygonStarts[p + 1];
                var inside = false;
                for (int i = a, j = b - 1; i < b; j = i++)
                {
                    float xi = AdelaideCarParks.Points[i * 2], zi = AdelaideCarParks.Points[i * 2 + 1];
                    float xj = AdelaideCarParks.Points[j * 2], zj = AdelaideCarParks.Points[j * 2 + 1];
                    if ((zi > z) != (zj > z) && x < (xj - xi) * (z - zi) / (zj - zi) + xi)
                        inside = !inside;
                }

                if (inside)
                    return true;
            }

            return false;
        }

        [Test]
        public void Data_HasRealCarParksBaysAndLamps()
        {
            Assert.That(AdelaideCarParks.PolygonCount, Is.GreaterThan(300));
            Assert.That(AdelaideCarParks.RunCount, Is.GreaterThan(1000));
            Assert.That(AdelaideCarParks.LampCount, Is.GreaterThan(300));
            Assert.That(AdelaideCarParks.PolygonStarts[AdelaideCarParks.PolygonCount], Is.EqualTo(AdelaideCarParks.Points.Length / 2));
        }

        [Test]
        public void AisleBays_SitInsideTheirCarPark()
        {
            var checkedRuns = 0;
            var inside = 0;
            var r = AdelaideCarParks.Runs;
            for (var k = 0; k + AdelaideCarParks.RunStride <= r.Length; k += AdelaideCarParks.RunStride)
            {
                if (r[k + 6] < 0.5f)
                    continue;
                checkedRuns++;
                if (InsideAnyPark(r[k], r[k + 1]))
                    inside++;
            }

            Assert.That(checkedRuns, Is.GreaterThan(800));
            Assert.That(inside, Is.GreaterThanOrEqualTo(checkedRuns * 0.99),
                "bays are generated only where the car park polygon is (a few sit on the outline within rounding)");
        }

        [Test]
        public void Triangulate_CoversConcaveAndClockwiseOutlines()
        {
            // an L, counter-clockwise: area 3
            var l = new List<float> { 0, 0, 2, 0, 2, 1, 1, 1, 1, 2, 0, 2 };
            Assert.That(Area(l, AdelaideCarParkGeometry.Triangulate(l)), Is.EqualTo(3f).Within(1e-3));
            // the same L, clockwise
            var cw = new List<float> { 0, 2, 1, 2, 1, 1, 2, 1, 2, 0, 0, 0 };
            Assert.That(Area(cw, AdelaideCarParkGeometry.Triangulate(cw)), Is.EqualTo(3f).Within(1e-3));
        }

        [Test]
        public void Surfaces_CoverTheCarParkOutlines()
        {
            var sink = new RoadMeshSink();
            var drawn = AdelaideCarParkGeometry.BuildSurfaces(sink, new RoadBuildOptions());
            Assert.That(drawn, Is.EqualTo(AdelaideCarParks.PolygonCount));
            double outline = 0;
            for (var p = 0; p < AdelaideCarParks.PolygonCount; p++)
            {
                var xz = new List<float>();
                for (var i = AdelaideCarParks.PolygonStarts[p]; i < AdelaideCarParks.PolygonStarts[p + 1]; i++)
                {
                    xz.Add(AdelaideCarParks.Points[i * 2]);
                    xz.Add(AdelaideCarParks.Points[i * 2 + 1]);
                }

                outline += Math.Abs(AdelaideCarParkGeometry.SignedArea(xz));
            }

            double covered = 0;
            foreach (var tile in sink.Tiles)
            {
                var pos = tile.Value.Positions;
                var t = tile.Value.Triangles;
                for (var i = 0; i < t.Count; i += 3)
                {
                    var ax = pos[t[i] * 3];
                    var az = pos[t[i] * 3 + 2];
                    // y component of (b - a) x (c - a): positive when the triangle faces up
                    var cross = (pos[t[i + 1] * 3 + 2] - az) * (pos[t[i + 2] * 3] - ax)
                                - (pos[t[i + 1] * 3] - ax) * (pos[t[i + 2] * 3 + 2] - az);
                    Assert.That(cross, Is.GreaterThanOrEqualTo(-1e-3f), "every surface triangle faces up");
                    covered += Math.Abs(cross) * 0.5;
                }
            }

            Assert.That(covered / outline, Is.InRange(0.97, 1.03), "the triangulation fills the outlines");
        }

        [Test]
        public void Cars_ArePlacedWithinBudgetAndNormalsAgreeWithWinding()
        {
            var sink = new RoadMeshSink();
            var classes = new int[5];
            var cars = AdelaideCarParkGeometry.BuildCars(sink, new RoadBuildOptions(), classes);
            Assert.That(cars, Is.InRange(1500, AdelaideCarParkGeometry.MaxCars));
            Assert.That(sink.TriangleCount, Is.EqualTo(cars * 30), "three five-face boxes per car");
            Assert.That(sink.TriangleCount, Is.LessThanOrEqualTo(AdelaideCarParkGeometry.MaxCars * 30));
            Assert.That(classes, Has.All.GreaterThan(200), "all five body classes appear throughout Adelaide's built car parks");
            foreach (var tile in sink.Tiles)
            {
                var p = tile.Value.Positions;
                var n = tile.Value.Normals;
                var t = tile.Value.Triangles;
                Assert.That(n.Count, Is.EqualTo(p.Count));
                for (var i = 0; i < t.Count; i += 3)
                {
                    int a = t[i], b = t[i + 1], c = t[i + 2];
                    var e1x = p[b * 3] - p[a * 3];
                    var e1y = p[b * 3 + 1] - p[a * 3 + 1];
                    var e1z = p[b * 3 + 2] - p[a * 3 + 2];
                    var e2x = p[c * 3] - p[a * 3];
                    var e2y = p[c * 3 + 1] - p[a * 3 + 1];
                    var e2z = p[c * 3 + 2] - p[a * 3 + 2];
                    var gx = e1y * e2z - e1z * e2y;
                    var gy = e1z * e2x - e1x * e2z;
                    var gz = e1x * e2y - e1y * e2x;
                    Assert.That(gx * n[a * 3] + gy * n[a * 3 + 1] + gz * n[a * 3 + 2], Is.GreaterThan(0f),
                        "a car face is wound to face the way its normal says");
                }
            }
        }

        [Test]
        public void Cars_AreDeterministic()
        {
            var a = new RoadMeshSink();
            var b = new RoadMeshSink();
            Assert.That(AdelaideCarParkGeometry.BuildCars(a, new RoadBuildOptions()),
                Is.EqualTo(AdelaideCarParkGeometry.BuildCars(b, new RoadBuildOptions())));
            Assert.That(a.VertexCount, Is.EqualTo(b.VertexCount));
        }

        [Test]
        public void VehicleClasses_AreStableAndDistinct()
        {
            var seen = new HashSet<ParkedVehicleClass>();
            for (var run = 0; run < 70; run += AdelaideCarParks.RunStride)
            for (var bay = 0; bay < 40; bay++)
                seen.Add(AdelaideCarParkGeometry.VehicleClassFor(run, bay));

            Assert.That(seen, Is.EquivalentTo(new[]
            {
                ParkedVehicleClass.Sedan,
                ParkedVehicleClass.Hatchback,
                ParkedVehicleClass.Suv,
                ParkedVehicleClass.Ute,
                ParkedVehicleClass.Van
            }));
            Assert.That(AdelaideCarParkGeometry.VehicleClassFor(21, 9), Is.EqualTo(ParkedVehicleClass.Van),
                "the class hash remains stable across builds");
        }

        [Test]
        public void BayLinesAndLamps_Exist()
        {
            var lines = new RoadMeshSink();
            Assert.That(AdelaideCarParkGeometry.BuildBayLines(lines, new RoadBuildOptions()), Is.GreaterThan(3000));
            var lamps = new RoadMeshSink();
            Assert.That(AdelaideCarParkGeometry.BuildLamps(lamps, new RoadBuildOptions()), Is.EqualTo(AdelaideCarParks.LampCount));
            Assert.That(lamps.TriangleCount, Is.EqualTo(AdelaideCarParks.LampCount * 20));
        }

        private static double Area(List<float> xz, List<int[]> tris)
        {
            double a = 0;
            foreach (var t in tris)
            {
                var cross = (xz[t[1] * 2] - xz[t[0] * 2]) * (xz[t[2] * 2 + 1] - xz[t[0] * 2 + 1])
                            - (xz[t[1] * 2 + 1] - xz[t[0] * 2 + 1]) * (xz[t[2] * 2] - xz[t[0] * 2]);
                a += Math.Abs(cross) * 0.5;
            }

            return a;
        }
    }
}
