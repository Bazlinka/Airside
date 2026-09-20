using System;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// 12/30 lineup and vacate must meet the remapped takeoff/landing stations so
    /// regionals do not teleport when the phase changes.
    /// </summary>
    public sealed class CrossStripContinuityTests
    {
        [Test]
        public void HoldingShortPose_EmptyStandUsesTheAssignedEndNotBay50D()
        {
            var hold05 = AdelaideGround.HoldingShortPose(default, 0, RunwayDirection.Runway05);
            Assert.That(System.Math.Abs(hold05.X - AdelaideLayout.Runway05Hold[0])
                        + System.Math.Abs(hold05.Z - AdelaideLayout.Runway05Hold[1]), Is.LessThan(1f));

            var hold23 = AdelaideGround.HoldingShortPose(default, 0, RunwayDirection.Runway23);
            Assert.That(System.Math.Abs(hold23.X - AdelaideLayout.Lineup23[0])
                        + System.Math.Abs(hold23.Z - AdelaideLayout.Lineup23[1]), Is.LessThan(1f),
                "23 must not fall back to the 05 hold");
            Assert.That(System.Math.Abs(hold23.X - hold05.X) + System.Math.Abs(hold23.Z - hold05.Z),
                Is.GreaterThan(100f));

            var hold12 = AdelaideGround.HoldingShortPose(default, 0, RunwayDirection.Runway12);
            Assert.That(System.Math.Abs(hold12.X - AdelaideCrossRoutes.Hold12[0])
                        + System.Math.Abs(hold12.Z - AdelaideCrossRoutes.Hold12[1]), Is.LessThan(1f));
        }

        [Test]
        public void VacateStartsWhereLandingRolloutEnds()
        {
            foreach (var runway in new[] { RunwayDirection.Runway12, RunwayDirection.Runway30 })
            {
                RunwayFrame.ToWorld(runway, CircuitProfile.RolloutEndX, 0f, 0f, out var rollX, out _, out var rollZ);
                var vacate = AdelaideCrossRoutes.Vacate(runway);
                var gap = Math.Sqrt((vacate[0] - rollX) * (vacate[0] - rollX)
                                    + (vacate[1] - rollZ) * (vacate[1] - rollZ));
                Assert.That(gap, Is.LessThan(2.5),
                    $"{runway} vacate must start where landing rollout ends");
            }
        }

        [Test]
        public void LineupEndsWhereTakeoffRollStarts()
        {
            foreach (var runway in new[] { RunwayDirection.Runway12, RunwayDirection.Runway30 })
            {
                RunwayFrame.ToWorld(runway, CircuitProfile.TakeoffStartX, 0f, 0f, out var takeX, out _, out var takeZ);
                var lineup = AdelaideCrossRoutes.Lineup(runway);
                var gap = Math.Sqrt(
                    (lineup[lineup.Length - 2] - takeX) * (lineup[lineup.Length - 2] - takeX)
                    + (lineup[lineup.Length - 1] - takeZ) * (lineup[lineup.Length - 1] - takeZ));
                Assert.That(gap, Is.LessThan(2.5),
                    $"{runway} lineup must end where takeoff roll starts");
            }
        }

        [Test]
        public void RemapAlong_KeepsOneMetreSoFiftyKnotsLooksLikeFiftyKnots()
        {
            Assert.That(RunwayFrame.RemapAlong(CircuitProfile.WestThresholdX),
                Is.EqualTo(-AdelaideCrossRoutes.HalfLength).Within(0.01f),
                "05 arrival threshold lines up with the 12 threshold");

            var from = RunwayFrame.RemapAlong(CircuitProfile.TouchdownX);
            var to = RunwayFrame.RemapAlong(CircuitProfile.TouchdownX + 200f);
            Assert.That(to - from, Is.EqualTo(200f).Within(0.01f),
                "200 m of 05 rollout must stay 200 m on 12/30");

            var roll = RunwayFrame.RemapAlong(CircuitProfile.TakeoffStartX + 900f)
                       - RunwayFrame.RemapAlong(CircuitProfile.TakeoffStartX);
            Assert.That(roll, Is.EqualTo(900f).Within(0.01f),
                "the ATR takeoff roll must not be squeezed onto the short strip");
        }

        [Test]
        public void ClearOfRunway_LeavesTheTwelveThirtyPavement()
        {
            var vacate = AdelaideGround.VacateFor(AircraftType.Atr42, RunwayDirection.Runway12);
            var clear = AdelaideGround.ClearOfRunwaySeconds(AircraftType.Atr42, RunwayDirection.Runway12);
            var pose = vacate.PoseAt(clear);
            WorldToCrossLocal(pose.X, pose.Z, out var along, out var across);
            var offPavement = Math.Abs(across) > 28f
                              || Math.Abs(along) > AdelaideCrossRoutes.HalfLength + 20f;
            Assert.That(offPavement, Is.True,
                $"clear-of-runway still on 12/30 at along={along:0} across={across:0}");
        }

        private static void WorldToCrossLocal(float x, float z, out float along, out float across)
        {
            var yaw = AdelaideLayout.CrossRunwayYawDegrees * Math.PI / 180.0;
            var cos = Math.Cos(yaw);
            var sin = Math.Sin(yaw);
            var dx = x - AdelaideLayout.CrossRunwayCenterX;
            var dz = z - AdelaideLayout.CrossRunwayCenterZ;
            along = (float)(cos * dx - sin * dz);
            across = (float)(sin * dx + cos * dz);
        }

        [Test]
        public void ToWorld_TwelveThirtyPreservesTakeoffRollMetres()
        {
            foreach (var runway in new[] { RunwayDirection.Runway12, RunwayDirection.Runway30 })
            {
                RunwayFrame.ToWorld(runway, CircuitProfile.TakeoffStartX, 0f, 0f,
                    out var x0, out _, out var z0);
                RunwayFrame.ToWorld(runway, CircuitProfile.TakeoffStartX + 200f, 0f, 0f,
                    out var x1, out _, out var z1);
                var travelled = Math.Sqrt((x1 - x0) * (x1 - x0) + (z1 - z0) * (z1 - z0));
                Assert.That(travelled, Is.EqualTo(200.0).Within(0.5),
                    $"{runway} must move 200 m when the 05 frame does");
            }
        }
    }
}
