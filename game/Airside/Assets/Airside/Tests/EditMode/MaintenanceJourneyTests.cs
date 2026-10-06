using System;
using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class MaintenanceJourneyTests
    {
        private static (ManualSimulationClock Clock, AirlineOperations Ops, FleetAircraft Aircraft) Create(bool jet = false)
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(7), DestinationCatalogue.Adelaide, AirlineOperations.AdelaideStands);
            var player = Airline.Player("Coastline", "#39708A"); ops.AddAirline(player);
            ops.CareerState.BaseLevel = jet ? PlayerBaseLevel.JetGate : PlayerBaseLevel.ExpandedRegional;
            var aircraft = ops.AddAircraft(player, "VH-PAX", jet ? AircraftType.Boeing7378 : AircraftType.Saab340,
                jet ? AirlineOperations.AdelaideTerminalGates[0] : AirlineOperations.AdelaideRegionalBays[0]);
            return (clock, ops, aircraft);
        }
        private static void Advance(ManualSimulationClock clock, AirlineOperations ops, long at)
        {
            clock.Set(new SimulationTime(at)); ops.Update();
        }
        private static void Until(ManualSimulationClock clock, AirlineOperations ops, FleetAircraft aircraft, MaintenancePhase phase)
        {
            for (var i = 0; i < 1000 && aircraft.MaintenanceJob?.Phase != phase; i++)
                Advance(clock, ops, (ops.NextEventAt() ?? clock.Now.Advance(5)).ElapsedSeconds);
            Assert.That(aircraft.MaintenanceJob?.Phase, Is.EqualTo(phase));
        }

        [Test]
        public void Request_ChargesOnceAndWearIsResetOnlyAfterActualRepair()
        {
            var (clock, ops, aircraft) = Create(); aircraft.RotationsSinceCheck = 7;
            var funds = ops.CareerState.Funds;
            Assert.That(ops.StartCheck(aircraft).Accepted, Is.True);
            Assert.That(ops.StartCheck(aircraft).Accepted, Is.False);
            Assert.That(ops.CareerState.Funds, Is.EqualTo(funds - Maintenance.CheckCost(aircraft.Type, ops.CareerState.BaseLevel)));
            Assert.That(aircraft.RotationsSinceCheck, Is.EqualTo(7));
            Until(clock, ops, aircraft, MaintenancePhase.Repairing);
            Assert.That(aircraft.MaintenanceJob.PhaseEndsAt - aircraft.MaintenanceJob.PhaseStartedAt,
                Is.EqualTo(Maintenance.CheckSeconds(aircraft.Type, ops.CareerState.BaseLevel)));
            Assert.That(aircraft.RotationsSinceCheck, Is.EqualTo(7));
            Advance(clock, ops, aircraft.MaintenanceJob.PhaseEndsAt);
            Assert.That(aircraft.RotationsSinceCheck, Is.Zero);
            Assert.That(aircraft.CompletedTrips, Is.Zero);
            Assert.That(aircraft.LifetimeRevenue, Is.Zero);
            Assert.That(Maintenance.InCheck(aircraft, clock.Now), Is.True, "not dispatchable until returned");
        }

        [TestCase(false)] [TestCase(true)]
        public void Startup_UsesRightThenLeftAndDoorsStayShut(bool jet)
        {
            var (clock, ops, aircraft) = Create(jet);
            Assert.That(ops.StartCheck(aircraft).Accepted, Is.True);
            Until(clock, ops, aircraft, jet ? MaintenancePhase.Taxiing : MaintenancePhase.Starting);
            var start = aircraft.MaintenanceJob.PhaseStartedAt;
            var early = EngineStartSequence.For(aircraft, start + (jet ? DepartureCountdown.JetRightStartAfterPushSeconds : 10) + 15);
            Assert.That(early.Right, Is.GreaterThan(early.Left));
            Assert.That(early.Beacon, Is.True); Assert.That(early.DoorsOpen, Is.False);
            Assert.That(early.CargoDoor, Is.Zero);
            Assert.That(EngineStartSequence.For(aircraft, start + 120).Left, Is.EqualTo(1f));
        }

        [TestCase(false)] [TestCase(true)]
        public void Job_ReleasesOriginAndReturnsToAnotherCompatibleStandWithoutAJump(bool jet)
        {
            var (clock, ops, aircraft) = Create(jet); var origin = aircraft.Stand;
            Assert.That(ops.StartCheck(aircraft).Accepted, Is.True);
            Until(clock, ops, aircraft, MaintenancePhase.Repairing);
            Assert.That(ops.IsStandFree(origin), Is.True);
            ops.AddAircraft(ops.PlayerAirline, "VH-OTHER", aircraft.Type, origin);
            Until(clock, ops, aircraft, MaintenancePhase.Returning);
            Assert.That(aircraft.Stand, Is.Not.EqualTo(origin));
            var job = aircraft.MaintenanceJob; var paths = job.Paths(aircraft.Type);
            var inside = paths.Entry.PoseAt(paths.Entry.Seconds);
            var exiting = job.Pose(aircraft.Type, job.PhaseStartedAt);
            Assert.That(exiting.X, Is.EqualTo(inside.X).Within(.01));
            Assert.That(exiting.Z, Is.EqualTo(inside.Z).Within(.01));
            Assert.That(exiting.NoseX * inside.NoseX + exiting.NoseZ * inside.NoseZ, Is.GreaterThan(.999));
            var stillInside = paths.Return.PoseAt(Math.Min(10, paths.ExitSeconds * .1));
            Assert.That(stillInside.NoseX * inside.NoseX + stillInside.NoseZ * inside.NoseZ, Is.GreaterThan(.999), "reverse straight before turning outside the shed");
            var end = paths.Return.PoseAt(paths.Return.Seconds);
            Advance(clock, ops, job.PhaseEndsAt + 75);
            Assert.That(aircraft.MaintenanceJob, Is.Null);
            var parked = AdelaideGround.StandPose(aircraft.Stand);
            Assert.That(end.X, Is.EqualTo(parked.X).Within(.1));
            Assert.That(end.Z, Is.EqualTo(parked.Z).Within(.1));
            Assert.That(end.NoseX * parked.NoseX + end.NoseZ * parked.NoseZ, Is.GreaterThan(.99));
        }

        [TestCase(MaintenancePhase.Preparing)] [TestCase(MaintenancePhase.Taxiing)]
        [TestCase(MaintenancePhase.Positioning)] [TestCase(MaintenancePhase.Repairing)]
        [TestCase(MaintenancePhase.Returning)]
        public void SaveRestore_PreservesPoseAndCompletesExactlyOnce(MaintenancePhase phase)
        {
            var (clock, ops, aircraft) = Create(); Assert.That(ops.StartCheck(aircraft).Accepted, Is.True);
            Until(clock, ops, aircraft, phase);
            var pose = aircraft.MaintenanceJob.Pose(aircraft.Type, clock.Now.ElapsedSeconds);
            var data = AirlineSave.Capture(ops); var restoredClock = new ManualSimulationClock(clock.Now);
            var restored = AirlineSave.Restore(data, restoredClock);
            var mine = restored.FleetOf(restored.PlayerAirline).Single();
            Assert.That(mine.MaintenanceJob.Phase, Is.EqualTo(phase));
            var after = mine.MaintenanceJob.Pose(mine.Type, restoredClock.Now.ElapsedSeconds);
            Assert.That(after.X, Is.EqualTo(pose.X).Within(.01)); Assert.That(after.Z, Is.EqualTo(pose.Z).Within(.01));
            Advance(clock, ops, 86400); Advance(restoredClock, restored, 86400);
            Assert.That(mine.MaintenanceJob, Is.Null); Assert.That(aircraft.MaintenanceJob, Is.Null);
            Assert.That(mine.Stand, Is.EqualTo(aircraft.Stand));
            Assert.That(restored.CareerState.Funds, Is.EqualTo(ops.CareerState.Funds));
            Assert.That(mine.CompletedTrips, Is.Zero);
        }

        [Test]
        public void TimeStepping_AndLargeCatchupFinishIdentically()
        {
            var (clock, ops, aircraft) = Create(); var (clock2, ops2, aircraft2) = Create();
            ops.StartCheck(aircraft); ops2.StartCheck(aircraft2);
            Advance(clock, ops, 12000);
            for (var t = 3; t <= 12000; t += 3) Advance(clock2, ops2, t);
            Assert.That(aircraft.MaintenanceJob, Is.Null); Assert.That(aircraft2.MaintenanceJob, Is.Null);
            Assert.That(aircraft2.StateStartedAt, Is.EqualTo(aircraft.StateStartedAt));
            Assert.That(aircraft2.Stand, Is.EqualTo(aircraft.Stand));
            Assert.That(ops2.CareerState.Funds, Is.EqualTo(ops.CareerState.Funds));
        }

        [TestCase(20)] [TestCase(21)]
        public void LegacyCheck_RestoresWithoutNewStartupOrCharge(int version)
        {
            var (clock, ops, aircraft) = Create(); var data = AirlineSave.Capture(ops); data.Version = version;
            data.Fleet[0].CheckUntilSeconds = 7200;
            var restored = AirlineSave.Restore(data, clock); var mine = restored.FleetOf(restored.PlayerAirline).Single();
            Assert.That(mine.MaintenanceJob, Is.Null); Assert.That(Maintenance.InCheck(mine, clock.Now), Is.True);
            var funds = restored.CareerState.Funds; Advance(clock, restored, 7200);
            Assert.That(Maintenance.InCheck(mine, clock.Now), Is.False); Assert.That(restored.CareerState.Funds, Is.EqualTo(funds));
        }

        [Test]
        public void GroundController_SeesAndBlocksAnActiveMaintenanceMovement()
        {
            var (clock, ops, aircraft) = Create(); ops.StartCheck(aircraft);
            Until(clock, ops, aircraft, MaintenancePhase.Taxiing);
            var mine = aircraft.MaintenanceJob.Paths(aircraft.Type).Outbound;
            var candidate = ops.AddAircraft(ops.PlayerAirline, "VH-OTHER", AircraftType.Saab340, AirlineOperations.AdelaideRegionalBays[1]);
            Assert.That(GroundTraffic.TryPose(ops.Fleet, aircraft, clock.Now.ElapsedSeconds, out _, out _), Is.True);
            Assert.That(GroundTraffic.PathClear(ops.Fleet, candidate, mine, RunwayDirection.Runway05, false,
                clock.Now, true, out var blocker), Is.False);
            Assert.That(blocker, Is.SameAs(aircraft));
        }

        [Test]
        public void Return_WaitsWithoutStealingAnOccupiedStandOrChargingAgain()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var stand = AirlineOperations.AdelaideRegionalBays[0];
            var ops = new AirlineOperations(clock, new SeededRandomSource(7), DestinationCatalogue.Adelaide, new[] { stand });
            var player = Airline.Player("Coastline", "#39708A"); ops.AddAirline(player);
            ops.CareerState.BaseLevel = PlayerBaseLevel.ExpandedRegional;
            var aircraft = ops.AddAircraft(player, "VH-PAX", AircraftType.Saab340, stand);
            Assert.That(ops.StartCheck(aircraft).Accepted, Is.True);
            var funds = ops.CareerState.Funds;
            Until(clock, ops, aircraft, MaintenancePhase.Repairing);
            var other = ops.AddAircraft(player, "VH-OTHER", AircraftType.Saab340, stand);
            Advance(clock, ops, 18000);
            Assert.That(aircraft.MaintenanceJob.Phase, Is.EqualTo(MaintenancePhase.WaitingReturn));
            Assert.That(aircraft.MaintenanceJob.WaitReason, Does.Contain("free stand"));
            Assert.That(aircraft.Stand.Value, Is.Null.Or.Empty);
            Assert.That(other.Stand, Is.EqualTo(stand));
            Assert.That(ops.CareerState.Funds, Is.EqualTo(funds));
            var restored = AirlineSave.Restore(AirlineSave.Capture(ops), clock);
            Assert.That(restored.Fleet.Single(a => a.Registration == "VH-PAX").MaintenanceJob.Phase,
                Is.EqualTo(MaintenancePhase.WaitingReturn));
        }

        [Test]
        public void Tower_ProtectsBothStripsForMaintenanceInsteadOfIgnoringAssignedRunway()
        {
            var (clock, ops, aircraft) = Create(); ops.StartCheck(aircraft);
            Until(clock, ops, aircraft, MaintenancePhase.Taxiing);
            var job = aircraft.MaintenanceJob;
            var leg = new GroundLeg(new GroundLegPart(new GroundPath(new[] { -500f, 180f, -500f, -180f },
                new GroundSpeedLimits(4f, .4f, .6f, .7f)), false));
            job.Paths(aircraft.Type).Outbound = leg;
            var crossing = RunwayCrossings.For(leg, RunwayDirection.Runway05, aircraft.Type, includeOwnRunway: true)
                .First(c => c.MainStrip);
            var from = new SimulationTime(job.PhaseStartedAt + (long)Math.Floor(crossing.EnterSeconds));
            var until = new SimulationTime(job.PhaseStartedAt + (long)Math.Ceiling(crossing.ExitSeconds));
            Assert.That(ops.CrossingDue(true, from, until), Is.SameAs(aircraft));
            ops.RestoreTower(clock.Now.Advance(leg.WholeSeconds + 100), new SimulationTime(0), 0);
            Assert.That(ops.CrossingIntoBusyStrip(leg, RunwayDirection.Runway05, clock.Now, aircraft.Type,
                includeOwnRunway: true).HasValue, Is.True);
        }

        [Test]
        public void MalformedJob_IsRejectedBeforeResumingMovement()
        {
            var (clock, ops, aircraft) = Create(); ops.StartCheck(aircraft);
            var data = AirlineSave.Capture(ops); data.Fleet[0].MaintenanceJob.HangarId = "missing-shed";
            Assert.Throws<FormatException>(() => AirlineSave.Restore(data, clock));
        }
    }
}
