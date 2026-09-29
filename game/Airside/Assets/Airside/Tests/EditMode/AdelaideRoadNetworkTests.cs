using System;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>ADR 0184: the complete OSM road network that replaces the capped arterial ribbons.</summary>
    public sealed class AdelaideRoadNetworkTests
    {
        private static float X(int i) => AdelaideRoadNetwork.Points[i * 2];
        private static float Z(int i) => AdelaideRoadNetwork.Points[i * 2 + 1];

        [Test]
        public void Network_IsComplete_NotTheCappedArterialSubset()
        {
            Assert.That(AdelaideRoadNetwork.Roads.Length, Is.GreaterThan(3000), "every drivable way, not 720");
            var classes = new bool[10];
            foreach (var road in AdelaideRoadNetwork.Roads)
                classes[(int)road.Class] = true;
            Assert.That(classes[(int)AdelaideRoadNetwork.RoadClass.Residential], Is.True, "local streets are present");
            Assert.That(classes[(int)AdelaideRoadNetwork.RoadClass.Service], Is.True, "service roads are present");
            Assert.That(classes[(int)AdelaideRoadNetwork.RoadClass.Primary], Is.True);
        }

        [Test]
        public void Roads_OwnValidPointRanges_AndSaneWidths()
        {
            var pointCount = AdelaideRoadNetwork.Points.Length / 2;
            var expectedStart = 0;
            foreach (var road in AdelaideRoadNetwork.Roads)
            {
                Assert.That(road.PointCount, Is.GreaterThanOrEqualTo(2), $"way {road.OsmId}");
                Assert.That(road.PointStart, Is.EqualTo(expectedStart), $"way {road.OsmId} ranges are contiguous");
                expectedStart += road.PointCount;
                Assert.That(expectedStart, Is.LessThanOrEqualTo(pointCount));
                Assert.That(road.Width, Is.InRange(2.5f, 40f), $"way {road.OsmId}");
                if (road.NameIndex >= 0)
                    Assert.That(road.NameIndex, Is.LessThan(AdelaideRoadNetwork.Names.Length));
            }
            Assert.That(expectedStart, Is.EqualTo(pointCount), "every point belongs to a road");
        }

        [Test]
        public void Roads_ExistInsideTheOperationalCore_WhichTheOldMesherSkipped()
        {
            var inCore = 0;
            foreach (var road in AdelaideRoadNetwork.Roads)
            {
                for (var i = road.PointStart; i < road.PointStart + road.PointCount; i++)
                {
                    if (AdelaideLandCover.InOperationalCore(X(i), Z(i)))
                    {
                        inCore++;
                        break;
                    }
                }
            }
            Assert.That(inCore, Is.GreaterThan(80));
        }

        [Test]
        public void AirsideRoads_AreFlagged_AndCoverTheExistingServiceRoads()
        {
            var airside = 0;
            foreach (var road in AdelaideRoadNetwork.Roads)
                if (road.IsAirside)
                    airside++;
            Assert.That(airside, Is.GreaterThan(50));

            foreach (var (name, xz) in new[]
            {
                ("ApronFrontage", AdelaideServiceRoads.ApronFrontage),
                ("AirsideAccessRoad", AdelaideServiceRoads.AirsideAccessRoad),
                ("SecurityRoad", AdelaideServiceRoads.SecurityRoad),
                ("LocaliserRoad", AdelaideServiceRoads.LocaliserRoad)
            })
            {
                for (var i = 0; i + 1 < xz.Length; i += 8)
                    Assert.That(DistanceToAirsideRoad(xz[i], xz[i + 1]), Is.LessThan(4f),
                        $"{name} point {i / 2} is drawn by the network");
            }
        }

        [Test]
        public void Junctions_SitOnRoadVertices()
        {
            Assert.That(AdelaideRoadNetwork.JunctionCount, Is.GreaterThan(1000));
            for (var j = 0; j < AdelaideRoadNetwork.JunctionCount; j += 25)
            {
                var jx = AdelaideRoadNetwork.Junctions[j * 4];
                var jz = AdelaideRoadNetwork.Junctions[j * 4 + 1];
                var best = float.MaxValue;
                for (var i = 0; i < AdelaideRoadNetwork.Points.Length / 2; i++)
                    best = Math.Min(best, (float)Math.Sqrt((X(i) - jx) * (X(i) - jx) + (Z(i) - jz) * (Z(i) - jz)));
                Assert.That(best, Is.LessThan(0.3f), $"junction {j}");
                Assert.That(AdelaideRoadNetwork.Junctions[j * 4 + 3], Is.GreaterThanOrEqualTo(2f));
            }
        }

        [Test]
        public void Furniture_HasCrossingsAndValidHeadings()
        {
            var crossings = 0;
            for (var i = 0; i < AdelaideRoadNetwork.FurnitureCount; i++)
            {
                var kind = (int)AdelaideRoadNetwork.Furniture[i * AdelaideRoadNetwork.FurnitureStride];
                var yaw = AdelaideRoadNetwork.Furniture[i * AdelaideRoadNetwork.FurnitureStride + 3];
                Assert.That(kind, Is.InRange(0, 6));
                Assert.That(yaw, Is.InRange(0f, 180f));
                if (kind == (int)AdelaideRoadNetwork.FurnitureKind.Crossing)
                    crossings++;
            }
            Assert.That(crossings, Is.GreaterThan(500));
        }

        private static float DistanceToAirsideRoad(float x, float z)
        {
            var best = float.MaxValue;
            foreach (var road in AdelaideRoadNetwork.Roads)
            {
                if (!road.IsAirside)
                    continue;
                for (var i = road.PointStart; i + 1 < road.PointStart + road.PointCount; i++)
                    best = Math.Min(best, SegmentDistance(x, z, X(i), Z(i), X(i + 1), Z(i + 1)));
            }
            return best;
        }

        private static float SegmentDistance(float px, float pz, float ax, float az, float bx, float bz)
        {
            var vx = bx - ax;
            var vz = bz - az;
            var l2 = vx * vx + vz * vz;
            var t = l2 <= 0f ? 0f : Math.Max(0f, Math.Min(1f, ((px - ax) * vx + (pz - az) * vz) / l2));
            var dx = px - (ax + vx * t);
            var dz = pz - (az + vz * t);
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }
    }
}
