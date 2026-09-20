using System;
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
    }
}
