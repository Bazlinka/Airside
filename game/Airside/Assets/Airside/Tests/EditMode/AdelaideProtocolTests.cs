using System;
using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class AdelaideProtocolTests
    {
        [TestCase("A359", "B38M", false, 120)]
        [TestCase("A359", "B412", true, 180)]
        [TestCase("B38M", "B412", false, 120)]
        [TestCase("DH8D", "B412", true, 180)]
        [TestCase("ATR42", "B412", true, 0)]
        [TestCase("SF34", "B412", false, 0)]
        [TestCase("B38M", "ATR42", false, 0)]
        [TestCase("A359", "B789", true, 0)]
        public void Wake_MinimaDependOnBothWeightCategories(string lead, string follow, bool landing, long expected)
        {
            AircraftType.TryFromId(lead, out var leader);
            AircraftType.TryFromId(follow, out var follower);
            Assert.That(WakeSeparation.Seconds(leader, follower, landing), Is.EqualTo(expected));
        }

        [Test]
        public void IntermediateDeparture_RequiresLongerWakeInterval()
        {
            Assert.That(WakeSeparation.Seconds(AircraftType.AirbusA350900, AircraftType.Boeing7378,
                landing: false, intermediateDeparture: true), Is.EqualTo(180));
        }

        [Test]
        public void WingSizeDoesNotClassifyWake_Q400AndAtrAreBothMedium()
        {
            Assert.That(WakeSeparation.Category(AircraftType.Dash8Q400), Is.EqualTo(WakeCategory.Medium));
            Assert.That(WakeSeparation.Category(AircraftType.Atr42), Is.EqualTo(WakeCategory.Medium));
            Assert.That(WakeSeparation.MediumAtLeast25Tonnes(AircraftType.Atr42), Is.False);
            Assert.That(WakeSeparation.MediumAtLeast25Tonnes(AircraftType.Dash8Q400), Is.True);
        }

        [Test]
        public void ErsaRestrictions_AreAircraftAndDepartureBaySpecific()
        {
            var gate25 = AdelaideGround.TerminalGates.First(g => g.Reference == "25");
            var stand = new StableId(gate25.Id);
            Assert.That(AdelaideTaxiPolicy.Allows("B1", AircraftType.Boeing7378, stand), Is.False);
            Assert.That(AdelaideTaxiPolicy.Allows("L", AircraftType.Boeing7378, stand), Is.False);
            Assert.That(AdelaideTaxiPolicy.Allows("L2", AircraftType.Boeing7378, stand), Is.True);
            Assert.That(AdelaideTaxiPolicy.Allows("B1", AircraftType.Boeing7378), Is.True, "restriction is on pushback departures");
            Assert.That(AdelaideTaxiPolicy.Allows("E2", AircraftType.AirbusA350900), Is.False);
            Assert.That(AdelaideTaxiPolicy.Allows("F4", AircraftType.AirbusA350900), Is.False);
            Assert.That(AdelaideTaxiPolicy.Allows("L2", AircraftType.AirbusA350900), Is.True);
            Assert.That(AdelaideTaxiPolicy.Allows("R", AircraftType.Saab340), Is.False);
        }

        [Test]
        public void RestrictedRouter_DoesNotInventConnectionAcrossGrass()
        {
            Assert.That(AdelaideTaxiRouter.TryRoute(10000, 10000, 20000, 20000,
                AircraftType.AirbusA350900, default, out var route), Is.False);
            Assert.That(route, Is.Null);
        }

        [Test]
        public void SupportedGates_HavePermittedMainRunwayRoutes()
        {
            var failures = new System.Collections.Generic.List<string>();
            foreach (var spec in AircraftCatalogue.All.Where(s => s.StandClass == StandClass.TerminalGate))
            foreach (var gate in AdelaideGround.TerminalGates)
            foreach (var runway in new[] { RunwayDirection.Runway05, RunwayDirection.Runway23 })
            {
                var stand = new StableId(gate.Id);
                // Large aircraft only on the gates the game's stand system allows.
                if (!AirlineOperations.StandFits(spec.Type, stand)) continue;
                if (!AdelaideGroundPolicy.RouteAvailable(stand, spec.Type, runway, true)) failures.Add($"{spec.Id} {gate.Reference} {runway} out");
                if (!AdelaideGroundPolicy.RouteAvailable(stand, spec.Type, runway, false)) failures.Add($"{spec.Id} {gate.Reference} {runway} in");
                var vacate = AdelaideGround.VacateFor(spec.Type, runway);
                var taxi = AdelaideGround.TaxiIn(stand, spec.Type, runway);
                Assert.That(Distance(vacate.PoseAt(vacate.Seconds), taxi.PoseAt(0)), Is.LessThan(0.5), $"{spec.Id} {gate.Reference} {runway} exit join");
            }
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        [Test]
        public void CrossingReservation_CoversTheAircraftTailBeforeReleasingRunway()
        {
            var leg = new GroundLeg(new GroundLegPart(new GroundPath(new[] { 0f, -200f, 0f, 200f }, GroundSpeedLimits.TaxiFor(AircraftType.AirbusA350900)), false));
            var centre = RunwayCrossings.For(leg, RunwayDirection.Runway12).Single(c => c.MainStrip);
            var envelope = RunwayCrossings.For(leg, RunwayDirection.Runway12, AircraftType.AirbusA350900).Single(c => c.MainStrip);
            Assert.That(envelope.EnterSeconds, Is.LessThan(centre.EnterSeconds));
            Assert.That(envelope.ExitSeconds, Is.GreaterThan(centre.ExitSeconds));
            var noseAtRelease = leg.PoseAt(envelope.ExitSeconds);
            Assert.That(noseAtRelease.Z - AircraftCatalogue.AirbusA350900.LengthMetres,
                Is.GreaterThan(RunwayCrossings.StripHalfWidthMetres));
        }

        [Test]
        public void WakeHistory_RoundTripsWithoutAliasingTheRunningSimulation()
        {
            var ops = NewOperations(out var clock);
            ops.AddAirline(Airline.Player("Wake Air", "#123456"));
            ops.RestoreWake(new RunwayWakeRecord { TypeId = "A359", Departure = true, EventAtSeconds = 120 }, null);
            var save = AirlineSave.Capture(ops);
            var restored = AirlineSave.Restore(save, new ManualSimulationClock(new SimulationTime(save.ClockSeconds)));
            Assert.That(restored.MainWake.TypeId, Is.EqualTo("A359"));
            Assert.That(restored.MainWake.EventAtSeconds, Is.EqualTo(120));
            save.MainWake.EventAtSeconds = 999;
            Assert.That(ops.MainWake.EventAtSeconds, Is.EqualTo(120));
            Assert.That(restored.MainWake.EventAtSeconds, Is.EqualTo(120));
        }

        [Test]
        public void CodeCPushbacks_MoveEastAtTheEndOnEitherRunway()
        {
            var eastX = Math.Sin(50.0 * Math.PI / 180.0);
            var eastZ = Math.Sin(320.0 * Math.PI / 180.0);
            foreach (var gate in AdelaideGround.TerminalGates)
            foreach (var runway in new[] { RunwayDirection.Runway05, RunwayDirection.Runway23 })
            {
                var leg = AdelaideGround.TaxiOut(new StableId(gate.Id), AircraftType.Boeing7378, runway);
                var push = leg.Parts[0].Path;
                var end = push.PointAtDistance(push.Length);
                var before = push.PointAtDistance(push.Length - 3);
                Assert.That((end.x - before.x) * eastX + (end.z - before.z) * eastZ, Is.GreaterThan(0),
                    $"gate {gate.Reference} {runway}");
            }
        }

        [Test]
        public void FollowerWake_ClearanceIsAnchoredAtAirborneTimeAndSurvivesLoad()
        {
            var ops = NewOperations(out var clock);
            var player = Airline.Player("Wake Air", "#123456");
            ops.AddAirline(player);
            ops.RestoreAircraft("VH-WAK", player, AircraftType.Boeing7378, FleetState.HoldingShort,
                clock.Now, null, default, AirlineOperations.AdelaideTerminalGates[0],
                HudTestAirline.Code("MEL"), null, 0);
            var follower = ops.Fleet.Single();
            follower.AssignedRunway = RunwayDirection.Runway05;
            ops.RestoreWake(new RunwayWakeRecord { TypeId = "A359", Departure = true, EventAtSeconds = 120 }, null);
            var rotationOffset = AdelaideGround.LineupFor(follower.AssignedRunway, follower.Type).WholeSeconds
                + (long)Math.Round(AircraftPerformance.For(follower.Type).TakeoffRollExactSeconds);
            var due = ops.MovementFreeAt(follower, landing: false);
            Assert.That(due.ElapsedSeconds + rotationOffset, Is.EqualTo(240));
            var save = AirlineSave.Capture(ops);
            var restored = AirlineSave.Restore(save, new ManualSimulationClock(clock.Now));
            Assert.That(restored.MovementFreeAt(restored.Fleet.Single(a => a.Registration == "VH-WAK"), false), Is.EqualTo(due));
        }

        [TestCase(179)]
        [TestCase(181)]
        [TestCase(600)]
        public void GroundClearance_NeverExpiresAStationaryBlocker(int seconds)
        {
            var ops = NewOperations(out var clock);
            var player = Airline.Player("Blocked Air", "#123456");
            ops.AddAirline(player);
            ops.RestoreAircraft("VH-BLK", player, AircraftType.Atr42, FleetState.AwaitingStand,
                new SimulationTime(0), null, default, default, HudTestAirline.Code("KGC"), null, 0);
            var blocker = ops.Fleet.Single();
            blocker.AssignedRunway = RunwayDirection.Runway05;
            var candidate = ops.AddAircraft(player, "VH-WAI", AircraftType.Atr42, AirlineOperations.AdelaideRegionalBays[0]);
            var waiting = AdelaideGround.AwaitingPose(0, blocker.Type, blocker.AssignedRunway);
            var leg = new GroundLeg(new GroundLegPart(new GroundPath(new[] {
                waiting.X - 80, waiting.Z, waiting.X + 80, waiting.Z }, GroundSpeedLimits.TaxiFor(candidate.Type)), false));
            Assert.That(GroundTraffic.PathClear(ops.Fleet, candidate, leg, RunwayDirection.Runway12,
                taxiOut: false, new SimulationTime(seconds), includeStationary: true, out var actualBlocker), Is.False);
            Assert.That(actualBlocker, Is.SameAs(blocker));
        }

        [Test]
        public void GroundController_ExplainsAStationaryRouteBlockAfterTenMinutes()
        {
            foreach (var stand in AirlineOperations.AdelaideRegionalBays)
            foreach (var runway in new[] { RunwayDirection.Runway05, RunwayDirection.Runway23 })
            foreach (var blockerRunway in new[] { RunwayDirection.Runway12, RunwayDirection.Runway30 })
            {
                var ops = NewOperations(out var clock);
                var other = new Airline("OTH", "Other Air", "#123456", isPlayer: false);
                ops.AddAirline(other);
                ops.RestoreAircraft("VH-CAN", other, AircraftType.Atr42, FleetState.AwaitingStand,
                    new SimulationTime(0), null, stand, default, HudTestAirline.Code("KGC"), null, 0);
                var candidate = ops.Fleet.Single();
                candidate.AssignedRunway = runway;
                ops.RestoreAircraft("VH-OBS", other, AircraftType.Atr42, FleetState.AwaitingStand,
                    new SimulationTime(1), null, default, default, HudTestAirline.Code("KGC"), null, 0);
                var blocker = ops.Fleet.Last();
                blocker.AssignedRunway = blockerRunway;
                clock.Set(new SimulationTime(600));
                var leg = AdelaideGround.TaxiIn(stand, candidate.Type, runway);
                if (GroundTraffic.PathClear(ops.Fleet, candidate, leg, runway, taxiOut: false, clock.Now)) continue;
                Assert.That(ops.Why(candidate).Kind, Is.EqualTo(HoldKind.TaxiwayBlocked));
                Assert.That(ops.Why(candidate).Blocker, Is.SameAs(blocker));
                Assert.That(ops.AssignStand(candidate, stand).Accepted, Is.True);
                Assert.That(candidate.State, Is.EqualTo(FleetState.AwaitingStand), "manual choice does not bypass traffic");
                Assert.That(candidate.Stand, Is.EqualTo(stand), "choice remains reserved during the hold");
                Assert.That(ops.IsStandFree(stand), Is.False);
                Assert.That(ops.AssignableStands(candidate), Does.Contain(stand), "its own reservation remains selectable");
                return;
            }
            Assert.Fail("Fixture must include an actual shared-corridor stationary conflict.");
        }

        [Test]
        public void LandingVacate_ReservesItsCrossingOfTheOtherRunway()
        {
            foreach (var runway in new[] { RunwayDirection.Runway05, RunwayDirection.Runway23,
                         RunwayDirection.Runway12, RunwayDirection.Runway30 })
            {
                var vacate = AdelaideGround.VacateFor(AircraftType.Atr42, runway);
                var crossings = RunwayCrossings.For(vacate, runway, AircraftType.Atr42);
                if (crossings.Count == 0) continue;
                var ops = NewOperations(out var clock);
                var other = new Airline("OTH", "Other Air", "#123456", isPlayer: false);
                ops.AddAirline(other);
                const long vacateAt = 120;
                ops.RestoreAircraft("VH-VAC", other, AircraftType.Atr42, FleetState.Landing,
                    new SimulationTime(0), new SimulationTime(vacateAt + vacate.WholeSeconds),
                    default, default, HudTestAirline.Code("KGC"), null, 0);
                var arrival = ops.Fleet.Single();
                arrival.AssignedRunway = runway;
                var crossing = crossings[0];
                Assert.That(ops.CrossingDue(crossing.MainStrip,
                    new SimulationTime(vacateAt + (long)Math.Floor(crossing.EnterSeconds)),
                    new SimulationTime(vacateAt + (long)Math.Ceiling(crossing.ExitSeconds))), Is.SameAs(arrival));
                return;
            }
            Assert.Fail("Fixture must include a real vacate crossing.");
        }

        [Test]
        public void Version20FleetSave_KeepsFerryAndDeadlinesWithoutInventingWakeHistory()
        {
            var ops = NewOperations(out var clock);
            var player = Airline.Player("Migration Air", "#123456");
            ops.AddAirline(player);
            ops.RestoreAircraft("VH-FER", player, AircraftType.Saab340, FleetState.Inbound,
                clock.Now, new SimulationTime(1200), default, default, HudTestAirline.Code("MEL"), null, 0);
            ops.Fleet.Single().IsFerry = true;
            ops.RestoreTower(new SimulationTime(600), new SimulationTime(300), 0);
            var save = AirlineSave.Capture(ops);
            Assert.That(save.Version, Is.EqualTo(21));
            save.Version = 20;
            // Version 20 predates these fields: even a stray field cannot become wake history.
            save.MainWake = new RunwayWakeRecord { TypeId = "UNKNOWN", EventAtSeconds = -1 };
            var restored = AirlineSave.Restore(save, new ManualSimulationClock(clock.Now));
            Assert.That(restored.Fleet.Single(a => a.Registration == "VH-FER").IsFerry, Is.True);
            Assert.That(restored.MainWake, Is.Null);
            Assert.That(restored.RunwayFreeAt.ElapsedSeconds, Is.EqualTo(600));
            Assert.That(restored.CrossRunwayFreeAt.ElapsedSeconds, Is.EqualTo(300));
        }

        private static AirlineOperations NewOperations(out ManualSimulationClock clock)
        {
            clock = new ManualSimulationClock(new SimulationTime(0));
            return new AirlineOperations(clock, new SeededRandomSource(3), DestinationCatalogue.Adelaide, AirlineOperations.AdelaideStands);
        }
        private static double Distance(GroundPose a, GroundPose b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));
    }
}
