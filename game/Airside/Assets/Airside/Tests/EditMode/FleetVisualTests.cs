using System.Collections.Generic;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>Fleet state → what the 3D field draws (ADR 0045).</summary>
    public sealed class FleetVisualTests
    {
        [Test]
        public void RoundTrip_DrawsEveryHomeMovementAndHidesTheLegsAway()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(3), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideRegionalBays);
            var player = Airline.Player("Test Air", "#123456");
            ops.AddAirline(player);
            var plane = ops.AddAircraft(player, "VH-TST", AircraftType.Atr42, AirlineOperations.AdelaideRegionalBays[0]);
            DestinationCatalogue.TryFind("KGC", out var kingscote);
            ops.ScheduleDeparture(plane, kingscote, new SimulationTime(10));

            var seenLegs = new List<FleetGroundLeg>();
            var seenPhases = new List<AircraftPhase>();
            var hiddenSeconds = 0;
            SimulationTime? takeoffPhaseAt = null, departedAt = null, approachAt = null, landingAt = null, vacateAt = null;

            for (var t = 0L; t < 4 * 3600; t++)
            {
                clock.Set(new SimulationTime(t));
                ops.Update();

                var visual = FleetVisual.For(plane, clock.Now);
                if (!visual.Visible)
                {
                    hiddenSeconds++;
                    Assert.That(new[] { FleetState.Outbound, FleetState.AtDestination, FleetState.Inbound }, Does.Contain(plane.State));
                    continue;
                }

                if (seenLegs.Count == 0 || seenLegs[^1] != visual.Leg) seenLegs.Add(visual.Leg);
                if (seenPhases.Count == 0 || seenPhases[^1] != visual.Phase) seenPhases.Add(visual.Phase);

                if (visual.Phase == AircraftPhase.Takeoff) takeoffPhaseAt ??= visual.PhaseStartedAt;
                if (visual.Phase == AircraftPhase.Departed) departedAt ??= visual.PhaseStartedAt;
                if (visual.Phase == AircraftPhase.Approach) approachAt ??= visual.PhaseStartedAt;
                if (visual.Phase == AircraftPhase.Landing) landingAt ??= visual.PhaseStartedAt;
                if (visual.Leg == FleetGroundLeg.Vacate) vacateAt ??= visual.LegStartedAt;

                if (plane.State == FleetState.AtStand && plane.CompletedTrips == 1)
                    break;
            }

            Assert.That(seenLegs, Is.EqualTo(new[]
            {
                FleetGroundLeg.Parked, FleetGroundLeg.TaxiOut, FleetGroundLeg.Lineup, FleetGroundLeg.None,
                FleetGroundLeg.Vacate, FleetGroundLeg.TaxiIn, FleetGroundLeg.Parked
            }), "the empty runway means no hold at the holding point; auto-stand skips the wait");
            Assert.That(seenPhases, Is.EqualTo(new[]
            {
                AircraftPhase.AtStand, AircraftPhase.TaxiOut, AircraftPhase.Takeoff, AircraftPhase.Departed,
                AircraftPhase.Approach, AircraftPhase.Landing, AircraftPhase.TaxiIn, AircraftPhase.AtStand
            }));

            // Each drawn airborne phase lasts exactly the circuit's duration, so the flown
            // curves hand over seamlessly.
            var performance = AircraftPerformance.For(plane.Type);
            Assert.That(departedAt.Value.ElapsedSeconds - takeoffPhaseAt.Value.ElapsedSeconds, Is.EqualTo(performance.TakeoffSeconds));
            Assert.That(landingAt.Value.ElapsedSeconds - approachAt.Value.ElapsedSeconds, Is.EqualTo(performance.ApproachSeconds));
            Assert.That(vacateAt.Value.ElapsedSeconds - landingAt.Value.ElapsedSeconds, Is.EqualTo(performance.LandingSeconds));
            Assert.That(hiddenSeconds, Is.GreaterThan(3600), "the Kingscote legs are flown off the map");
        }

        [Test]
        public void HoldingShort_IsDrawnWaitingAtTheHoldingPoint()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(3), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideRegionalBays);
            var player = Airline.Player("Test Air", "#123456");
            ops.AddAirline(player);
            var first = ops.AddAircraft(player, "VH-TSA", AircraftType.Atr42, AirlineOperations.AdelaideRegionalBays[0]);
            var second = ops.AddAircraft(player, "VH-TSB", AircraftType.Atr42, AirlineOperations.AdelaideRegionalBays[1]);
            DestinationCatalogue.TryFind("KGC", out var kingscote);
            ops.ScheduleDeparture(first, kingscote, new SimulationTime(600));
            ops.ScheduleDeparture(second, kingscote, new SimulationTime(600));

            // Ground releases the neighbour once the first has cleared the stands.
            clock.Set(new SimulationTime(600));
            ops.Update();
            var secondStart = 600 + AirlineOperations.TaxiClearSecondsFrom(
                first.DepartureStand, first.Type, first.AssignedRunway);
            clock.Set(new SimulationTime(secondStart));
            ops.Update();
            var secondAtHold = second.StateEndsAt.Value.ElapsedSeconds;
            clock.Set(new SimulationTime(secondAtHold + 5));
            ops.Update();

            var waiting = FleetVisual.For(second, clock.Now);
            Assert.That(waiting.Leg, Is.EqualTo(FleetGroundLeg.HoldingShort));
            Assert.That(waiting.Phase, Is.EqualTo(AircraftPhase.TaxiOut), "engines stay running at the hold");
        }

        [Test]
        public void WaitingAircraft_AreQueuedApartInsteadOfDrawnOnOneSpot()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(3), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideRegionalBays);
            var player = Airline.Player("Test Air", "#123456");
            ops.AddAirline(player);
            var bays = AirlineOperations.AdelaideRegionalBays;
            var a = ops.AddAircraft(player, "VH-TSA", AircraftType.Atr42, bays[0]);
            var b = ops.AddAircraft(player, "VH-TSB", AircraftType.Atr42, bays[1]);
            var c = ops.AddAircraft(player, "VH-TSC", AircraftType.Atr42, bays[2]);
            DestinationCatalogue.TryFind("KGC", out var kingscote);
            foreach (var plane in new[] { a, b, c })
                ops.ScheduleDeparture(plane, kingscote, new SimulationTime(600));

            // Slot poses must stay apart even when two aircraft do meet at the hold.
            // Longer apron clearance means they often will not bunch, so the geometry
            // is checked directly rather than waiting for a three-ship queue.
            var p0 = AdelaideGround.HoldingShortPose(bays[0], 0);
            var p1 = AdelaideGround.HoldingShortPose(bays[1], 1);
            var gap = System.Math.Sqrt((p0.X - p1.X) * (p0.X - p1.X) + (p0.Z - p1.Z) * (p0.Z - p1.Z));
            Assert.That(gap, Is.GreaterThan(30.0), "hold-short slots are not drawn on one spot");
            Assert.That(FleetVisual.QueueSlot(new[] { a, b }, b), Is.EqualTo(1));

            var w0 = AdelaideGround.AwaitingPose(0);
            var w1 = AdelaideGround.AwaitingPose(1);
            Assert.That(System.Math.Abs(w0.X - w1.X) + System.Math.Abs(w0.Z - w1.Z), Is.GreaterThan(30f));

            var vacate12 = AdelaideGround.VacateFor(AircraftType.Atr42, RunwayDirection.Runway12);
            var end12 = vacate12.PoseAt(vacate12.Seconds);
            var wait12 = AdelaideGround.AwaitingPose(0, AircraftType.Atr42, RunwayDirection.Runway12);
            var q12 = AdelaideGround.AwaitingPose(1, AircraftType.Atr42, RunwayDirection.Runway12);
            var q05 = AdelaideGround.AwaitingPose(1, AircraftType.Atr42, RunwayDirection.Runway05);
            Assert.That(System.Math.Abs(wait12.X - end12.X) + System.Math.Abs(wait12.Z - end12.Z), Is.LessThan(1f),
                "12/30 arrivals wait at the end of their own vacate (no snap back to the hold)");
            Assert.That(System.Math.Abs(q12.X - q05.X) + System.Math.Abs(q12.Z - q05.Z),
                Is.GreaterThan(20f), "the second 12 arrival queues on its exit, not the 05 exit");
            Assert.That(AdelaideGround.ClearOfRunwaySeconds(AircraftType.Atr42, RunwayDirection.Runway12),
                Is.LessThan(AdelaideGround.VacateFor(AircraftType.Atr42, RunwayDirection.Runway12).WholeSeconds / 2),
                "the strip frees well before that long vacate finishes");
        }

        [Test]
        public void HoldingForLanding_IsDrawnOnShortFinal()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(3), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideRegionalBays);
            var player = Airline.Player("Test Air", "#123456");
            ops.AddAirline(player);
            DestinationCatalogue.TryFind("KGC", out var kingscote);

            var method = typeof(AirlineOperations).GetMethod("RestoreAircraft",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(ops, new object[]
            {
                "VH-HLD", player, AircraftType.Atr42, FleetState.HoldingForLanding, new SimulationTime(0),
                null, default(StableId), default(StableId), kingscote, null, 0
            });
            var tower = typeof(AirlineOperations).GetMethod("RestoreTower",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(SimulationTime), typeof(SimulationTime), typeof(long) },
                modifiers: null);
            var busy = new SimulationTime(600);
            tower.Invoke(ops, new object[] { busy, busy, 0L });
            clock.Set(new SimulationTime(10));
            ops.Update();

            var plane = ops.Fleet[0];
            Assert.That(plane.State, Is.EqualTo(FleetState.HoldingForLanding));
            var visual = FleetVisual.For(plane, clock.Now);
            Assert.That(visual.Visible, Is.True, "final traffic must be drawn, not hidden off the field");
            Assert.That(visual.Phase, Is.EqualTo(AircraftPhase.Approach));
            Assert.That(visual.Leg, Is.EqualTo(FleetGroundLeg.None));
        }

        [Test]
        public void GoAround_FliesTheApproachThenTheMissedApproach()
        {
            long goAroundAt = -1;
            for (var hour = 0; hour < 24 && goAroundAt < 0; hour++)
            {
                var t = hour * 3600L;
                if (System.Math.Abs(AirlineOperations.GoAroundSeed(0, "VH-GA1", t) % 11) != 0)
                    continue;
                goAroundAt = t;
            }
            Assert.That(goAroundAt, Is.GreaterThanOrEqualTo(0), "a seed that actually goes around");

            var clock = new ManualSimulationClock(new SimulationTime(goAroundAt));
            var ops = new AirlineOperations(clock, new SeededRandomSource(3), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideRegionalBays);
            var player = Airline.Player("Test Air", "#123456");
            ops.AddAirline(player);
            DestinationCatalogue.TryFind("KGC", out var kingscote);
            var restore = typeof(AirlineOperations).GetMethod("RestoreAircraft",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            restore.Invoke(ops, new object[]
            {
                "VH-GA1", player, AircraftType.Atr42, FleetState.HoldingForLanding, new SimulationTime(goAroundAt),
                null, default(StableId), default(StableId), kingscote, null, 0
            });
            restore.Invoke(ops, new object[]
            {
                "VH-GA2", player, AircraftType.Atr42, FleetState.HoldingForLanding, new SimulationTime(goAroundAt),
                null, default(StableId), default(StableId), kingscote, null, 0
            });
            var first = ops.Fleet[0];

            ops.Update();
            Assert.That(first.WentAroundThisTrip, Is.True);
            Assert.That(first.State, Is.EqualTo(FleetState.Landing), "the missed approach starts as a visible final");
            var onFinal = FleetVisual.For(first, clock.Now);
            Assert.That(onFinal.Visible, Is.True);
            Assert.That(onFinal.Phase, Is.EqualTo(AircraftPhase.Approach));

            var abort = goAroundAt + ApproachHold.RemainingFinalSeconds(
                AircraftPerformance.For(first.Type).ApproachSeconds, first.Registration);
            clock.Set(new SimulationTime(abort));
            ops.Update();
            Assert.That(first.State, Is.EqualTo(FleetState.GoAround));
            var missed = FleetVisual.For(first, clock.Now);
            Assert.That(missed.Visible, Is.True);
            Assert.That(missed.Phase, Is.EqualTo(AircraftPhase.GoAround));
            Assert.That(missed.Leg, Is.EqualTo(FleetGroundLeg.None));

            var deadline = abort + AirlineOperations.GoAroundCircuitSeconds + 30 * 60;
            while (first.State == FleetState.GoAround && clock.Now.ElapsedSeconds < deadline)
            {
                var next = ops.NextEventAt() ?? clock.Now.Advance(1);
                if (next.ElapsedSeconds > deadline)
                    break;
                clock.Set(next);
                ops.Update();
            }

            Assert.That(first.State, Is.AnyOf(FleetState.HoldingForLanding, FleetState.Landing),
                "rejoin final after the circuit (tower may clear in the same tick)");
            Assert.That(first.AssignedRunway, Is.EqualTo(ops.RunwayFor(first)),
                "rejoin picks the live runway end, not a stale assignment from before the circuit");

            while (first.State != FleetState.AtStand && clock.Now.ElapsedSeconds < deadline)
            {
                var next = ops.NextEventAt() ?? clock.Now.Advance(1);
                if (next.ElapsedSeconds > deadline)
                    break;
                clock.Set(next);
                ops.Update();
            }

            Assert.That(first.State, Is.EqualTo(FleetState.AtStand),
                "a go-around must still reach a stand instead of looping the circuit");
        }
    }
}
