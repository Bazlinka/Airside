using System;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class GroundClearanceRegressionTests
    {
        [TestCase(0.5, false)]
        [TestCase(1.5, true)]
        public void ParkedObstacle_RequiresOneMetrePositivePlanningGap(double gap, bool expected)
        {
            var airline = new Airline("OTH", "Other Air", "#123456", false);
            var at = new SimulationTime(0);
            var parked = new FleetAircraft("VH-PKD", airline, AircraftType.Dash8Q400, new StableId("BAY-2"), at);
            var candidate = new FleetAircraft("VH-ARR", airline, AircraftType.Dash8Q400, new StableId("BAY-7"), at);
            AssertClearance(candidate, parked, AdelaideGround.StandPose(parked.Stand), gap, expected);
        }

        [TestCase(2.5, false)]
        [TestCase(3.5, true)]
        public void HoldingQueue_RetainsThreeMetrePlanningGap(double gap, bool expected)
        {
            var airline = new Airline("OTH", "Other Air", "#123456", false);
            var at = new SimulationTime(0);
            var holding = new FleetAircraft("VH-HLD", airline, AircraftType.Dash8Q400, default, at);
            holding.DepartureStand = new StableId("BAY-2");
            holding.Restore(FleetState.HoldingShort, at, null);
            var candidate = new FleetAircraft("VH-ARR", airline, AircraftType.Dash8Q400, new StableId("BAY-7"), at);
            Assert.That(GroundTraffic.TryPose(new[] { holding }, holding, 0, out var pose, out _), Is.True);
            AssertClearance(candidate, holding, pose, gap, expected);
        }

        private static void AssertClearance(FleetAircraft candidate, FleetAircraft obstacle,
            GroundPose pose, double gap, bool expected)
        {
            var distance = (float)(0.85 * (GroundTraffic.HalfSpan(candidate.Type)
                + GroundTraffic.HalfSpan(obstacle.Type)) + 2.0 + gap);
            var path = new GroundPath(new[] { pose.X - 40f, pose.Z + distance,
                pose.X + 40f, pose.Z + distance }, GroundSpeedLimits.TaxiFor(candidate.Type));
            var leg = new GroundLeg(new GroundLegPart(path, false));
            Assert.That(GroundTraffic.PathClear(new[] { obstacle }, candidate, leg, RunwayDirection.Runway05,
                false, new SimulationTime(0)), Is.EqualTo(expected));
        }

        [TestCase(RunwayDirection.Runway05)]
        [TestCase(RunwayDirection.Runway23)]
        [TestCase(RunwayDirection.Runway12)]
        [TestCase(RunwayDirection.Runway30)]
        public void BayFourInbound_ClearsLargestParkedNeighboursWithoutChangingTheStop(RunwayDirection runway)
        {
            var stand = new StableId("BAY-4");
            var route = AdelaideGround.TaxiIn(stand, AircraftType.Dash8Q400, runway);
            foreach (var other in new[] { "BAY-1", "BAY-2", "BAY-3" })
            {
                var parked = AdelaideGround.StandPose(new StableId(other));
                var minimum = double.MaxValue;
                for (var t = 0.0; t <= route.Seconds; t += 0.1)
                {
                    var (x, z) = route.PositionAt(t);
                    minimum = Math.Min(minimum, Math.Sqrt((x - parked.X) * (x - parked.X)
                        + (z - parked.Z) * (z - parked.Z)));
                }
                var limit = 0.85 * (2 * GroundTraffic.HalfSpan(AircraftType.Dash8Q400)) + 2;
                Assert.That(minimum, Is.GreaterThan(limit + 3), other + " keeps even the moving-traffic planning allowance");
            }
            var stop = route.PoseAt(route.Seconds);
            var expected = AdelaideGround.StandPose(stand);
            Assert.That(stop.X, Is.EqualTo(expected.X).Within(0.05));
            Assert.That(stop.Z, Is.EqualTo(expected.Z).Within(0.05));
        }
    }
}
