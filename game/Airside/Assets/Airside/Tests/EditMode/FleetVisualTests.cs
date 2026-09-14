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
            Assert.That(departedAt.Value.ElapsedSeconds - takeoffPhaseAt.Value.ElapsedSeconds, Is.EqualTo(CircuitProfile.TakeoffSeconds));
            Assert.That(landingAt.Value.ElapsedSeconds - approachAt.Value.ElapsedSeconds, Is.EqualTo(CircuitProfile.ApproachSeconds));
            Assert.That(vacateAt.Value.ElapsedSeconds - landingAt.Value.ElapsedSeconds, Is.EqualTo(CircuitProfile.LandingSeconds));
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

            // Both push back at 0 from neighbouring bays; run until both have reached the hold.
            var both = System.Math.Max(AirlineOperations.TaxiOutSecondsFrom(first.Stand), AirlineOperations.TaxiOutSecondsFrom(second.Stand));
            clock.Set(new SimulationTime(both + 5));
            ops.Update();

            Assert.That(FleetVisual.For(first, clock.Now).Leg, Is.EqualTo(FleetGroundLeg.Lineup));
            var waiting = FleetVisual.For(second, clock.Now);
            Assert.That(waiting.Leg, Is.EqualTo(FleetGroundLeg.HoldingShort));
            Assert.That(waiting.Phase, Is.EqualTo(AircraftPhase.TaxiOut), "engines stay running at the hold");
        }
    }
}
