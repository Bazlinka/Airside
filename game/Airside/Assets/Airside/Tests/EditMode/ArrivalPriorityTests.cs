using System;
using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>An arrival on final is never left waiting: it lands or goes around (ADR 2026-10-07).</summary>
    public sealed class ArrivalPriorityTests
    {
        [Test]
        public void DecisionPoint_IsTheFinalHoldLimitAfterJoining()
        {
            Assert.That(ApproachRules.DecisionPointAt(1000), Is.EqualTo(1000 + ApproachRules.FinalHoldLimitSeconds));
            Assert.That(ApproachRules.MeterHorizonSeconds, Is.LessThan(ApproachRules.FinalHoldLimitSeconds));
        }

        [TestCase(2026u)]
        [TestCase(7u)]
        [TestCase(99u)]
        public void NoArrivalStaysOnFinalPastItsDecisionPoint(uint seed)
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(seed),
                Airline.Player("Priority Test Air", "#1F3A93"));
            var landed = 0;
            var wentAround = 0;

            for (var steps = 0; steps < 80000 && clock.Now.ElapsedSeconds < 2 * 24 * 3600; steps++)
            {
                var next = ops.NextEventAt();
                if (next == null)
                    break;
                clock.Set(next.Value);
                ops.Update();
                foreach (var aircraft in ops.Fleet)
                {
                    if (aircraft.State == FleetState.GoAround)
                        wentAround++;
                    if (aircraft.State == FleetState.Landing && !AirlineOperations.IsMissedApproachLanding(aircraft))
                        landed++;
                    if (aircraft.State != FleetState.HoldingForLanding || aircraft.Type.IsRotorcraft
                        || !AirlineOperations.ExemptFromCurfew(aircraft) && AirportCurfew.IsClosed(clock.Now, ops.Clock))
                        continue;
                    var waited = clock.Now.ElapsedSeconds - aircraft.StateStartedAt.ElapsedSeconds;
                    Assert.That(waited, Is.LessThanOrEqualTo(ApproachRules.FinalHoldLimitSeconds),
                        $"{aircraft.Registration} has sat on final {waited} s at {clock.Now.ElapsedSeconds}");
                }
            }

            TestContext.WriteLine($"seed {seed}: landings {landed}, go-around state entries {wentAround}");
            Assert.That(landed, Is.GreaterThan(20), "the run should land plenty of arrivals");
        }
    }
}
