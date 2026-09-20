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
