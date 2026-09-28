using System;
using System.Linq;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// ADR 0173 — the drawn final no longer re-runs the tower's landing estimate every frame,
    /// arrivals ease their speed along it, lineups leave the holding point on the taxi heading,
    /// and a shut-down propeller or fan stands still.
    /// </summary>
    public sealed class ParkedAircraftAndMotionTests
    {
        [Test]
        public void Lineup_LeavesTheHoldingPointOnTheTaxiHeading()
        {
            foreach (var runway in new[]
                     {
                         RunwayDirection.Runway05, RunwayDirection.Runway23,
                         RunwayDirection.Runway12, RunwayDirection.Runway30
                     })
            foreach (var spec in AircraftCatalogue.All)
            {
                if (!RunwayWeather.IsMainRunway(runway) && AirlineOperations.NeedsTerminalGate(spec.Type))
                    continue;
                Assert.That(AdelaideGround.TryHoldApproachDirection(runway, out var inX, out var inZ), Is.True);
                var lineup = AdelaideGround.LineupFor(runway, spec.Type);
                var start = lineup.PoseAt(0);
                var early = lineup.PoseAt(3);
                var dx = early.X - start.X;
                var dz = early.Z - start.Z;
                var length = Math.Sqrt(dx * dx + dz * dz);
                Assert.That(length, Is.GreaterThan(0.05), $"{spec.Id} {runway} moves off the hold");
                var dot = (dx * inX + dz * inZ) / length;
                var degrees = Math.Acos(Math.Min(1.0, dot)) * 180.0 / Math.PI;
                Assert.That(degrees, Is.LessThan(2.0),
                    $"{spec.Id} {runway} sets off {degrees:0.0}° off the taxi heading (was 8.5° on 05)");
            }
        }

        [Test]
        public void AlignEntry_KeepsTheStartAndTheLineBeyondTheBlend()
        {
            var baked = new[] { 0f, 0f, 3f, -10f, 6f, -20f, 9f, -30f, 12f, -40f, 15f, -50f, 18f, -60f };
            var aligned = LineupGeometry.AlignEntry(baked, 0f, -1f, 30f);
            Assert.That(aligned[0], Is.EqualTo(0f));
            Assert.That(aligned[1], Is.EqualTo(0f));
            Assert.That(aligned[2], Is.LessThan(baked[2]), "early points pulled toward the arrival heading");
            for (var i = 8; i < baked.Length; i++)
                Assert.That(aligned[i], Is.EqualTo(baked[i]), "beyond the blend the authored line is unchanged");
        }

        [Test]
        public void FlyFinal_EasesSpeedInsteadOfLurchingToTheEstimate()
        {
            const float approach = 70f;
            var speed = approach;
            var metres = 10_000f;
            // The estimate slips 60 s later in one step: the old clamp dropped straight to 55 %.
            var target = metres + approach * 60f;
            var last = speed;
            for (var i = 0; i < 600; i++)
            {
                metres = ArrivalApproach.FlyFinal(metres, ref speed, target, approach, 1f / 60f);
                target -= approach / 60f;
                Assert.That(Math.Abs(speed - last), Is.LessThanOrEqualTo(approach * ArrivalApproach.SpeedChangePerSecond / 60f + 1e-4f),
                    "speed changes gradually");
                last = speed;
            }

            Assert.That(speed, Is.LessThan(approach), "it slowed to lose the gap");
            Assert.That(speed, Is.GreaterThanOrEqualTo(approach * ArrivalApproach.MinSpeedFactor));

            // Converges: fly long enough and it is back on the estimate at approach speed.
            for (var i = 0; i < 60 * 240; i++)
            {
                metres = ArrivalApproach.FlyFinal(metres, ref speed, target, approach, 1f / 60f);
                target = Math.Max(0f, target - approach / 60f);
            }

            Assert.That(Math.Abs(metres - target), Is.LessThan(approach * 3f), "caught up with the estimate");
            Assert.That(ArrivalApproach.FlyFinal(0f, ref speed, 0f, approach, 0.1f), Is.EqualTo(0f), "never passes the hold point");
        }

        [Test]
        public void LandingEstimate_StepwiseEqualsTheWholeEstimate()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(2026),
                Airline.Player("Probe Air", "#1F3A93"));
            var compared = 0;
            for (var t = 0L; t < 14 * 3600 && compared < 60; t += 2)
            {
                clock.Set(new SimulationTime(t));
                ops.Update();
                if (t % 120 != 0)
                    continue;
                foreach (var aircraft in ops.Fleet.Where(a => a.State is FleetState.Inbound or FleetState.HoldingForLanding))
                {
                    var whole = ops.ExpectedLandingClearance(aircraft, out var runway);
                    var queued = ops.ExpectedLandingQueueTime(aircraft, out var queuedRunway);
                    Assert.That(queued.HasValue, Is.EqualTo(whole.HasValue));
                    if (!whole.HasValue)
                        continue;
                    Assert.That(queuedRunway, Is.EqualTo(runway));
                    var at = queued.Value;
                    for (var i = 0; i < AirlineOperations.LandingGroundCheckSteps && !ops.LandingGroundClear(aircraft, at); i++)
                        at = GroundTraffic.NextGrid(at);
                    Assert.That(at, Is.EqualTo(whole.Value), aircraft.Registration);
                    compared++;
                }
            }

            Assert.That(compared, Is.GreaterThan(20), "plenty of arrivals compared");
        }

        [Test]
        public void ShutDownEngines_StandStill()
        {
            Assert.That(AirsidePropellerDynamics.ParkedRpm, Is.EqualTo(0f),
                "a feathered, braked propeller and a shut-down fan do not creep round at the stand");
        }
    }
}
