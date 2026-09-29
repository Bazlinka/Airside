using System.Collections.Generic;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// Service vehicles, buses and walkers moved in straight lines and passed through parked
    /// aircraft, buildings and the terminal. <see cref="GroundRouter"/> plans round them.
    /// </summary>
    public sealed class GroundRouterTests
    {
        private readonly List<float> _route = new();

        [Test]
        public void NothingInTheWayIsAStraightLine()
        {
            GroundRouter.Plan(0f, 0f, 50f, 20f, new List<RouteObstacle>(), 2f, _route);
            Assert.That(_route, Is.EqualTo(new[] { 0f, 0f, 50f, 20f }));
        }

        [Test]
        public void GoesRoundABuildingInTheWay()
        {
            var building = new RouteObstacle(new[] { 20f, -10f, 30f, -10f, 30f, 10f, 20f, 10f }, true);
            GroundRouter.Plan(0f, 0f, 50f, 0f, new[] { building }, 2f, _route);
            AssertClearOf(building, 1.9f);
            Assert.That(_route[^2], Is.EqualTo(50f));
            Assert.That(_route[^1], Is.EqualTo(0f));
            Assert.That(GroundRouter.Length(_route), Is.LessThan(80f), "round the corner, not the long way");
        }

        [Test]
        public void DoesNotCrossAWallLine()
        {
            var wall = new RouteObstacle(new[] { 25f, -30f, 25f, 12f }, false);
            GroundRouter.Plan(0f, 0f, 50f, 0f, new[] { wall }, 2f, _route);
            AssertClearOf(wall, 1.9f);
        }

        [Test]
        public void DrivesRoundAnAtrToItsFuelStopWithoutCrossingItsNacellesOrPropellers()
        {
            var layout = AircraftLayout.For(AircraftType.Atr42);
            var rects = new List<LayoutRect>();
            layout.Footprint(rects, forVehicles: true);
            var obstacles = new List<RouteObstacle>();
            foreach (var r in rects)
                obstacles.Add(RouteObstacle.Quad(r.MinX, r.MinZ, r.MaxX, r.MinZ, r.MaxX, r.MaxZ, r.MinX, r.MaxZ));
            var fuel = layout.FuelTruck;
            // From the left side, ahead of the nose: straight across would go through the nose.
            GroundRouter.Plan(-15f, 18f, fuel.X, fuel.Z, obstacles, 2.2f, _route);
            foreach (var o in obstacles)
                AssertClearOf(o, 1.5f);
        }

        [Test]
        public void AGoalAgainstAnObstacleIsStillReached()
        {
            // A stair truck docking at a door ends inside the aircraft's grown outline.
            var fuselage = new RouteObstacle(new[] { -2f, -20f, 2f, -20f, 2f, 20f, -2f, 20f }, true);
            GroundRouter.Plan(-30f, 0f, -2.5f, 0f, new[] { fuselage }, 2.2f, _route);
            Assert.That(_route[^2], Is.EqualTo(-2.5f));
            Assert.That(_route[^1], Is.EqualTo(0f));
        }

        [Test]
        public void AlongFollowsThePolyline()
        {
            var line = new List<float> { 0f, 0f, 10f, 0f, 10f, 10f };
            Assert.That(GroundRouter.Length(line), Is.EqualTo(20f).Within(1e-4f));
            var mid = GroundRouter.Along(line, 15f);
            Assert.That(mid.X, Is.EqualTo(10f).Within(1e-4f));
            Assert.That(mid.Z, Is.EqualTo(5f).Within(1e-4f));
            Assert.That(mid.DirZ, Is.EqualTo(1f).Within(1e-4f));
        }

        /// <summary>No sampled point on the route (away from its ends) comes within <paramref name="clearance"/>.</summary>
        private void AssertClearOf(RouteObstacle obstacle, float clearance)
        {
            var total = GroundRouter.Length(_route);
            for (var d = 3f; d < total - 3f; d += 0.25f)
            {
                var p = GroundRouter.Along(_route, d);
                Assert.That(obstacle.Blocks(p.X, p.Z, clearance), Is.False,
                    $"route passes ({p.X:0.0}, {p.Z:0.0}), inside or against the obstacle");
            }
        }
    }
}
