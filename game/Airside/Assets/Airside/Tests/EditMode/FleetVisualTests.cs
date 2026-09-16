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
                    Assert.That(new[] { FleetState.Outbound, FleetState.AtDestination, FleetState.Inbound, FleetState.HoldingForLanding }, Does.Contain(plane.State));
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
                if (plane.State == FleetState.AwaitingStand)
                    ops.AssignStand(plane, AirlineOperations.AdelaideRegionalBays[2]);
            }

            Assert.That(seenLegs, Is.EqualTo(new[]
            {
                FleetGroundLeg.Parked, FleetGroundLeg.TaxiOut, FleetGroundLeg.Lineup, FleetGroundLeg.None,
                FleetGroundLeg.Vacate, FleetGroundLeg.AwaitingStand, FleetGroundLeg.TaxiIn, FleetGroundLeg.Parked
            }), "the empty runway means no hold at the holding point");
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
            ops.ScheduleDeparture(first, kingscote, new SimulationTime(0));
            ops.ScheduleDeparture(second, kingscote, new SimulationTime(0));

            // Ground releases the neighbour one minute later; inspect once it reaches the queue.
            var secondAtHold = AirlineOperations.TaxiReleaseSeparationSeconds
                               + AirlineOperations.TaxiOutSecondsFrom(second.Stand);
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
                ops.ScheduleDeparture(plane, kingscote, new SimulationTime(0));

            long longest = 0;
            var releaseIndex = 0;
            foreach (var plane in new[] { a, b, c })
            {
                longest = System.Math.Max(longest, releaseIndex * AirlineOperations.TaxiReleaseSeparationSeconds
                                                   + AirlineOperations.TaxiOutSecondsFrom(plane.Stand));
                releaseIndex++;
            }
            clock.Set(new SimulationTime(longest + 5));
            ops.Update();

            var holding = new List<FleetAircraft>();
            foreach (var plane in ops.Fleet)
                if (plane.State == FleetState.HoldingShort)
                    holding.Add(plane);
            Assert.That(holding.Count, Is.EqualTo(2), "one lines up, two hold");

            var slots = new HashSet<int>();
            foreach (var plane in holding)
                slots.Add(FleetVisual.QueueSlot(ops.Fleet, plane));
            Assert.That(slots, Is.EquivalentTo(new[] { 0, 1 }));

            var p0 = AdelaideGround.HoldingShortPose(holding[0].DepartureStand, FleetVisual.QueueSlot(ops.Fleet, holding[0]));
            var p1 = AdelaideGround.HoldingShortPose(holding[1].DepartureStand, FleetVisual.QueueSlot(ops.Fleet, holding[1]));
            var gap = System.Math.Sqrt((p0.X - p1.X) * (p0.X - p1.X) + (p0.Z - p1.Z) * (p0.Z - p1.Z));
            Assert.That(gap, Is.GreaterThan(30.0));

            var w0 = AdelaideGround.AwaitingPose(0);
            var w1 = AdelaideGround.AwaitingPose(1);
            Assert.That(System.Math.Abs(w0.X - w1.X) + System.Math.Abs(w0.Z - w1.Z), Is.GreaterThan(30f));
        }
    }
}
