using System;
using System.Linq;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>ADR 0126 — braking into a queue place, and runway crossings the tower respects.</summary>
    public sealed class TaxiCrossingAndQueueTests
    {
        [Test]
        public void HelicopterLiftingFromPad_NeverMovesTheRunwayQueueBack()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(4), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideStands);
            var other = new Airline("OTH", "Other Air", "#C95D50", isPlayer: false);
            ops.AddAirline(other);
            ops.RestoreAircraft("VH-ROT", other, AircraftType.Bell412, FleetState.TakingOff,
                clock.Now, clock.Now.Advance((long)Math.Ceiling(RotorcraftPerformance.Bell412.TakeoffSeconds)), default,
                AirlineOperations.AdelaideHelipadStands[0], HudTestAirline.Code("KGC"), null, 0);
            var rotor = ops.Fleet.Last();
            ops.RestoreAircraft("VH-DEP", other, AircraftType.Atr42, FleetState.HoldingShort,
                clock.Now, null, default, AirlineOperations.AdelaideRegionalBays[0], HudTestAirline.Code("KGC"), null, 0);
            var departure = ops.Fleet.Last();
            Assert.That(FleetVisual.QueueSlot(ops.Fleet, departure, clock.Now), Is.Zero);
            Assert.That(FleetVisual.QueueAhead(ops.Fleet, departure, clock.Now), Is.Zero);
            Assert.That(GroundTraffic.TryPose(ops.Fleet, rotor, 0, out _, out _), Is.False,
                "the pad liftoff must not be reconstructed as a runway roll");
        }

        [Test]
        public void BusyOpeningBank_DoesNotTrapTheBayFourArrivalAgainstBayTwoDeparture()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(2026),
                Airline.Player("Probe Air", "#123456"));
            var firstArrival = ops.Fleet.Single(a => a.Registration == "VH-QOM");
            var parkedBlocker = ops.Fleet.Single(a => a.Registration == "VH-QOK");
            clock.Set(new SimulationTime(3600)); ops.Update();
            Assert.That(parkedBlocker.State, Is.EqualTo(FleetState.Outbound),
                "the booked departure must get past the exit and fly its actual route");
            Assert.That(firstArrival.CompletedTrips, Is.GreaterThan(0),
                "the inbound aircraft must reach its bay and resume its rotation");
        }

        [Test]
        public void TaxiIn_HoldsForAnActualParkedObstacleAndReleasesAfterItLeaves()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(4), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideStands);
            var other = new Airline("OTH", "Other Air", "#C95D50", isPlayer: false);
            ops.AddAirline(other);
            var parked = ops.AddAircraft(other, "VH-PKD", AircraftType.Dash8Q400, new StableId("BAY-2"));
            ops.RestoreAircraft("VH-ARR", other, AircraftType.Dash8Q400, FleetState.AwaitingStand,
                clock.Now, null, new StableId("BAY-4"), default, HudTestAirline.Code("KGC"), null, 0);
            var arrival = ops.Fleet.Last(); arrival.AssignedRunway = RunwayDirection.Runway30;
            // A deliberately conflicting transit route tests the guard independently
            // of the now-corrected BAY-4 apron route, which clears this parked neighbour.
            var obstaclePose = AdelaideGround.StandPose(parked.Stand);
            var route = new GroundLeg(new GroundLegPart(new GroundPath(new[]
            {
                obstaclePose.X - 40f, obstaclePose.Z, obstaclePose.X + 40f, obstaclePose.Z
            }, GroundSpeedLimits.TaxiFor(arrival.Type)), false));
            Assert.That(GroundTraffic.PathClear(ops.Fleet, arrival, route, arrival.AssignedRunway,
                taxiOut: false, clock.Now, includeStationary: true, out var blocker), Is.False);
            Assert.That(blocker, Is.SameAs(parked));
            // After the blocker departs, the exact same route can be released.
            parked.Restore(FleetState.AtDestination, clock.Now, clock.Now.Advance(3600));
            Assert.That(GroundTraffic.PathClear(ops.Fleet, arrival, route, arrival.AssignedRunway,
                taxiOut: false, clock.Now), Is.True);
        }

        [Test]
        public void QueuedTaxiOut_BrakesEvenlyToAStandstillAtItsPlace()
        {
            var stand = AirlineOperations.AdelaideRegionalBays[0];
            var leg = AdelaideGround.TaxiOut(stand, AircraftType.Atr42, RunwayDirection.Runway05);
            var stop = GroundTraffic.QueuedSeconds(leg, 2);
            var (sx, sz) = leg.PositionAt(stop);

            float? lastSpeed = null;
            var braking = false;
            for (var t = 0.0; t < stop + 90; t += 0.1)
            {
                var s = leg.BrakedSeconds(t, stop, out var speed);
                Assert.That(s, Is.LessThanOrEqualTo(stop + 1e-6), "never past its place");
                if (speed < 0f)
                {
                    Assert.That(braking, Is.False, "once braking it never resumes the plan");
                    continue;
                }

                braking = true;
                Assert.That(speed, Is.GreaterThanOrEqualTo(0f));
                if (lastSpeed.HasValue)
                {
                    var decel = (lastSpeed.Value - speed) / 0.1f;
                    Assert.That(decel, Is.LessThanOrEqualTo(GroundLeg.QueueBrakingMetresPerSecondSquared + 0.2f), $"hard stop at {t:0.0}s");
                    Assert.That(speed, Is.LessThanOrEqualTo(lastSpeed.Value + 1e-4f), "no speeding up while braking");
                }

                lastSpeed = speed;
            }

            Assert.That(braking, Is.True);
            Assert.That(lastSpeed, Is.EqualTo(0f), "stood still at the end");
            var (ex, ez) = leg.PositionAt(leg.BrakedSeconds(stop + 90, stop, out _));
            Assert.That(Math.Sqrt((ex - sx) * (ex - sx) + (ez - sz) * (ez - sz)), Is.LessThan(0.5), "stops exactly at its queue place");
        }

        [Test]
        public void AdelaideRoutes_CrossTheRunwaysTheyReallyCross()
        {
            var bay = AirlineOperations.AdelaideRegionalBays[0];
            Assert.That(RunwayCrossings.For(AdelaideGround.TaxiIn(bay, AircraftType.Atr42, RunwayDirection.Runway05), RunwayDirection.Runway05)
                .Any(c => !c.MainStrip), Is.True, "a 05 arrival taxis across 12/30");
            Assert.That(RunwayCrossings.For(AdelaideGround.TaxiOut(bay, AircraftType.Atr42, RunwayDirection.Runway30), RunwayDirection.Runway30)
                .Any(c => c.MainStrip), Is.True, "a 30 departure taxis across 05/23");
            foreach (var crossing in RunwayCrossings.For(AdelaideGround.TaxiOut(bay, AircraftType.Atr42, RunwayDirection.Runway30), RunwayDirection.Runway30))
                Assert.That(crossing.ExitSeconds, Is.GreaterThan(crossing.EnterSeconds));
            Assert.That(RunwayCrossings.For(AdelaideGround.TaxiOut(bay, AircraftType.Atr42, RunwayDirection.Runway12), RunwayDirection.Runway12),
                Is.Empty, "its own runway's holding point is not a crossing");
        }

        [Test]
        public void Tower_HoldsATakeoffWhileTaxiingTrafficCrossesTheRunway()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(4), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideStands);
            var other = new Airline("OTH", "Other Air", "#C95D50", isPlayer: false);
            ops.AddAirline(other);
            var bays = AirlineOperations.AdelaideRegionalBays;
            var taxiIn = AdelaideGround.TaxiIn(bays[1], AircraftType.Atr42, RunwayDirection.Runway05);
            ops.RestoreAircraft("VH-TXI", other, AircraftType.Atr42, FleetState.TaxiIn, new SimulationTime(0),
                new SimulationTime(taxiIn.WholeSeconds), bays[1], default, HudTestAirline.Code("KGC"), null, 0);
            var crosser = ops.Fleet.Last();
            crosser.AssignedRunway = RunwayDirection.Runway05;
            ops.RestoreAircraft("VH-DEP", other, AircraftType.Atr42, FleetState.HoldingShort, new SimulationTime(0), null,
                default, bays[2], HudTestAirline.Code("KGC"), null, 0);
            var departure = ops.Fleet.Last();
            departure.AssignedRunway = RunwayDirection.Runway12;
            var crossing = RunwayCrossings.For(taxiIn, RunwayDirection.Runway05).First(c => !c.MainStrip);
            if (Weather.At(new SimulationTime(0)) == WeatherKind.Storm)
                Assert.Inconclusive("storm at t=0");

            var why = ops.Why(departure);
            Assert.That(why.Kind, Is.EqualTo(HoldKind.CrossingRunway), why.Kind.ToString());
            Assert.That(why.Blocker, Is.SameAs(crosser));
            Assert.That(HoldReasonText.Long(departure, why, clock.Now), Does.Contain("crossing"));

            for (var t = 1L; t < 3600 && departure.State == FleetState.HoldingShort; t++)
            {
                clock.Set(new SimulationTime(t));
                ops.Update();
            }

            Assert.That(departure.State, Is.Not.EqualTo(FleetState.HoldingShort), "departs once the crossing is done");
            var rollStart = departure.StateStartedAt.ElapsedSeconds;
            var rollEnd = rollStart + ops.RunwayBusySeconds(departure, landing: false);
            var crossStart = (long)Math.Floor(crossing.EnterSeconds);
            var crossEnd = (long)Math.Ceiling(crossing.ExitSeconds);
            Assert.That(rollEnd <= crossStart || rollStart >= crossEnd, Is.True,
                $"takeoff {rollStart}–{rollEnd}s overlapped the crossing {crossStart}–{crossEnd}s");
        }
    }
}
