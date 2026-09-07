using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// "Every delay should have an understandable cause" is a stated goal of the
    /// game, so no delay may be reported with an empty explanation.
    /// </summary>
    public sealed class DelayCauseTests
    {
        [Test]
        public void UnderstaffedTurnaround_OverrunsAndNamesUnderstaffingAsTheCause()
        {
            var understaffed = new AirportStaffing();
            Assert.That(understaffed.Release(), Is.True);
            Assert.That(understaffed.Release(), Is.True);
            Assert.That(understaffed.IsUnderstaffed, Is.True);

            var workflow = new TurnaroundWorkflow(
                new SimulationTime(0), cleaningDisruption: false, understaffed.TurnaroundSpeedFactor);

            Assert.That(workflow.HasCleaningDisruption, Is.False);
            Assert.That(workflow.IsComplete(new SimulationTime(TurnaroundWorkflow.ScheduledWindowSeconds)), Is.False,
                "fewer crew than the baseline should stretch the turnaround past its window");
            Assert.That(workflow.OverrunCause, Is.EqualTo("Understaffed ground crew"));
            Assert.That(workflow.DelayCause(new SimulationTime(50)), Is.EqualTo("Understaffed ground crew"));
        }

        [Test]
        public void OnScheduleTurnaround_ReportsNoOverrunCause()
        {
            var workflow = new TurnaroundWorkflow(new SimulationTime(0), cleaningDisruption: false);

            Assert.That(workflow.IsComplete(new SimulationTime(TurnaroundWorkflow.ScheduledWindowSeconds)), Is.True);
            Assert.That(workflow.OverrunCause, Is.Empty);
        }

        [Test]
        public void EveryRecordedDelay_InAnUnderstaffedAirport_HasACause()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(5), new ReservationTable());
            Assert.That(simulation.ReleaseGroundCrew(), Is.True);
            Assert.That(simulation.ReleaseGroundCrew(), Is.True);

            var sawDelay = false;
            for (var second = 1; second <= 3000; second++)
            {
                clock.Advance(1);
                simulation.Update();
                if (simulation.LastDelaySeconds > 0)
                {
                    sawDelay = true;
                    Assert.That(simulation.LastDelayCause, Is.Not.Empty,
                        "a reported delay must name a cause the player can act on");
                }
            }

            Assert.That(sawDelay, Is.True, "an understaffed airport should run late");
        }

        [Test]
        public void UnderstaffedDelayEvents_AreLoggedWithTheirCause()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(5), new ReservationTable());
            Assert.That(simulation.ReleaseGroundCrew(), Is.True);
            Assert.That(simulation.ReleaseGroundCrew(), Is.True);

            for (var second = 1; second <= 3000; second++)
            {
                clock.Advance(1);
                simulation.Update();
            }

            var delayEvents = 0;
            foreach (var entry in simulation.EventLog.Events)
            {
                if (!entry.Title.StartsWith("Delayed", System.StringComparison.Ordinal))
                    continue;
                delayEvents++;
                Assert.That(entry.Detail, Is.Not.Empty, $"'{entry.Title}' was logged without a cause");
            }

            Assert.That(delayEvents, Is.GreaterThan(0));
        }
    }
}
