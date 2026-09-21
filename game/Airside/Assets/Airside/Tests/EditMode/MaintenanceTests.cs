using System;
using System.Collections.Generic;
using System.Linq;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>Routine aircraft checks (ADR 0085): wear, cost, grounding, HUD.</summary>
    public sealed class MaintenanceTests
    {
        private static Destination Code(string code)
        {
            Assert.That(DestinationCatalogue.TryFind(code, out var destination), Is.True, code);
            return destination;
        }

        private static (ManualSimulationClock clock, AirlineOperations ops, FleetAircraft plane) PlayerOnly()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(7), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideRegionalBays);
            var player = Airline.Player("Soak Air", "#1F3A93");
            ops.AddAirline(player);
            var plane = ops.AddAircraft(player, "VH-PAX", AircraftType.Saab340,
                AirlineOperations.AdelaideRegionalBays[0]);
            return (clock, ops, plane);
        }

        private static void RunTo(ManualSimulationClock clock, AirlineOperations ops, long seconds)
        {
            clock.Set(new SimulationTime(seconds));
            ops.Update();
        }

        private static void FlyRoundTrip(ManualSimulationClock clock, AirlineOperations ops, FleetAircraft plane,
            Destination destination, long departAtSeconds)
        {
            Assert.That(ops.ScheduleDeparture(plane, destination, new SimulationTime(departAtSeconds)).Accepted,
                Is.True);
            RunTo(clock, ops, departAtSeconds);
            while (plane.State is FleetState.TaxiOut or FleetState.HoldingShort)
                RunTo(clock, ops, (plane.StateEndsAt ?? clock.Now.Advance(1)).ElapsedSeconds);
            Assert.That(plane.StateEndsAt, Is.Not.Null);
            RunTo(clock, ops, plane.StateEndsAt.Value.ElapsedSeconds + 1);
            while (plane.State is FleetState.Outbound or FleetState.AtDestination or FleetState.Inbound)
                RunTo(clock, ops, plane.StateEndsAt.Value.ElapsedSeconds);
            var parkedBy = clock.Now.ElapsedSeconds + AirlineOperations.LandingRunwaySecondsFor(plane.Type) + 3600;
            while (plane.State != FleetState.AtStand && clock.Now.ElapsedSeconds < parkedBy)
                RunTo(clock, ops, (ops.NextEventAt() ?? clock.Now.Advance(60)).ElapsedSeconds);
            Assert.That(plane.State, Is.EqualTo(FleetState.AtStand));
        }

        [Test]
        public void SaabCheck_CostsFourHundredAndLastsTwoHours()
        {
            Assert.That(Maintenance.CheckCost(AircraftType.Saab340), Is.EqualTo(400L));
            Assert.That(Maintenance.CheckSeconds(AircraftType.Saab340), Is.EqualTo(2 * 3600L));
            Assert.That(Maintenance.CheckSeconds(AircraftType.Boeing7378), Is.EqualTo(4 * 3600L));
            Assert.That(Maintenance.CheckCost(AircraftType.Atr42),
                Is.EqualTo(Math.Max(300L, (long)Math.Round(AircraftAcquisition.Atr42.Price * Maintenance.CostFraction))));
        }

        [Test]
        public void StartCheck_ChargesGroundsAndResetsWear()
        {
            var (clock, ops, plane) = PlayerOnly();
            ops.RestoreMaintenance(plane.Registration, 7, 0);
            var funds = ops.CareerState.Funds;

            var result = ops.StartCheck(plane);

            Assert.That(result.Accepted, Is.True);
            Assert.That(plane.RotationsSinceCheck, Is.EqualTo(0));
            Assert.That(Maintenance.InCheck(plane, clock.Now), Is.True);
            Assert.That(plane.CheckUntil.Value.ElapsedSeconds, Is.EqualTo(Maintenance.CheckSeconds(plane.Type)));
            Assert.That(ops.CareerState.Funds, Is.EqualTo(funds - Maintenance.CheckCost(plane.Type)));
            Assert.That(ops.ScheduleDeparture(plane, Code("KGC"), new SimulationTime(600)).Accepted, Is.False);
            Assert.That(ops.ScheduleDeparture(plane, Code("KGC"), plane.CheckUntil.Value).Accepted, Is.True);
        }

        [Test]
        public void StartCheck_RefusesFlyingBookedAlreadyInCheckAndPoverty()
        {
            var (clock, ops, plane) = PlayerOnly();
            Assert.That(ops.ScheduleDeparture(plane, Code("KGC"), new SimulationTime(3600)).Accepted, Is.True);
            Assert.That(ops.StartCheck(plane).Accepted, Is.False, "booked");

            var (clock2, ops2, flying) = PlayerOnly();
            Assert.That(ops2.ScheduleDeparture(flying, Code("KGC"), new SimulationTime(600)).Accepted, Is.True);
            RunTo(clock2, ops2, 600);
            Assert.That(flying.State, Is.Not.EqualTo(FleetState.AtStand));
            Assert.That(ops2.StartCheck(flying).Accepted, Is.False, "not parked");

            var (clock3, ops3, parked) = PlayerOnly();
            Assert.That(ops3.StartCheck(parked).Accepted, Is.True);
            Assert.That(ops3.StartCheck(parked).Accepted, Is.False, "already in check");
            _ = clock;
            _ = clock3;

            var (clock4, ops4, broke) = PlayerOnly();
            ops4.RestoreCareerState(10, 100, nameof(OperatingTier.Provisional), null, 0, 0,
                Array.Empty<string>(), Array.Empty<string>(), 0);
            Assert.That(ops4.StartCheck(broke).Accepted, Is.False, "cannot afford");
            _ = clock4;
        }

        [Test]
        public void CheckEnds_AircraftIsFreeAgainWithoutAnEvent()
        {
            var (clock, ops, plane) = PlayerOnly();
            Assert.That(ops.StartCheck(plane).Accepted, Is.True);
            RunTo(clock, ops, plane.CheckUntil.Value.ElapsedSeconds);
            Assert.That(Maintenance.InCheck(plane, clock.Now), Is.False);
            Assert.That(ops.ScheduleDeparture(plane, Code("KGC"), clock.Now.Advance(DeparturePrep.LeadSeconds(plane.Type)))
                .Accepted, Is.True);
        }

        [Test]
        public void OverdueRotation_DropsReliability()
        {
            var (clock, ops, plane) = PlayerOnly();
            ops.RestoreCareerState(AirlineCareerState.StartingFunds, 90, nameof(OperatingTier.Provisional),
                null, 0, 0, Array.Empty<string>(), Array.Empty<string>(), 0);
            ops.RestoreMaintenance(plane.Registration, Maintenance.IntervalRotations, 0);
            Assert.That(Maintenance.IsOverdue(plane), Is.True);

            FlyRoundTrip(clock, ops, plane, Code("KGC"), DeparturePrep.LeadSeconds(plane.Type));

            Assert.That(plane.RotationsSinceCheck, Is.EqualTo(Maintenance.IntervalRotations + 1));
            // Overdue −2, then on-time pushback +1.
            Assert.That(ops.CareerState.Reliability, Is.EqualTo(90 - Maintenance.OverduePenalty + 1));
        }

        [Test]
        public void SaveV11_RemembersWearAndAnInProgressCheck()
        {
            var (clock, ops, plane) = PlayerOnly();
            Assert.That(ops.StartCheck(plane).Accepted, Is.True);
            plane.RotationsSinceCheck = 0;
            var json = AirlineSave.Capture(ops);
            Assert.That(json.Version, Is.EqualTo(11));
            Assert.That(json.Fleet[0].CheckUntilSeconds, Is.EqualTo(plane.CheckUntil.Value.ElapsedSeconds));

            var restored = AirlineSave.Restore(json, new ManualSimulationClock(clock.Now));
            var mine = restored.FleetOf(restored.PlayerAirline).Single();
            Assert.That(Maintenance.InCheck(mine, clock.Now), Is.True);
            Assert.That(mine.CheckUntil.Value.ElapsedSeconds, Is.EqualTo(plane.CheckUntil.Value.ElapsedSeconds));
        }

        [Test]
        public void SaveV10_LoadsAsFreshlyChecked()
        {
            var (clock, ops, _) = PlayerOnly();
            var data = AirlineSave.Capture(ops);
            data.Version = 10;
            data.Fleet[0].RotationsSinceCheck = 99;
            data.Fleet[0].CheckUntilSeconds = 12_000;

            var restored = AirlineSave.Restore(data, new ManualSimulationClock(clock.Now));
            var mine = restored.FleetOf(restored.PlayerAirline).Single();
            Assert.That(mine.RotationsSinceCheck, Is.EqualTo(0));
            Assert.That(mine.CheckUntil, Is.Null);
        }

        [Test]
        public void Hud_OverdueParkedAircraftOffersSendForCheck()
        {
            var (clock, ops, plane) = PlayerOnly();
            ops.RestoreMaintenance(plane.Registration, Maintenance.IntervalRotations, 0);

            Assert.That(OperationsSummary.PrimaryAction(plane, clock.Now),
                Is.EqualTo(AircraftHudAction.StartCheck));
            Assert.That(OperationsSummary.ActionLabel(AircraftHudAction.StartCheck), Is.EqualTo("Send for check"));
            Assert.That(OperationsSummary.AvailableCount(new[] { plane }, clock.Now), Is.EqualTo(1),
                "overdue is still free to fly — the player chooses when");
            Assert.That(OperationsSummary.CompactState(plane, clock.Now), Is.EqualTo("Check overdue"));
            Assert.That(AircraftStatus.Severity(plane, clock.Now), Is.EqualTo(StatusSeverity.Warning));
            Assert.That(OperationsSummary.Objective(new[] { plane }, clock.Now, ops.Clock, ops.CareerState).NextLine,
                Does.Contain("send").IgnoreCase.And.Contain("VH-PAX"));
        }

        [Test]
        public void Hud_InCheckHidesAvailabilityAndAsksThePlayerToWait()
        {
            var (clock, ops, plane) = PlayerOnly();
            Assert.That(ops.StartCheck(plane).Accepted, Is.True);

            Assert.That(OperationsSummary.AvailableCount(new[] { plane }, clock.Now), Is.EqualTo(0));
            Assert.That(OperationsSummary.PrimaryAction(plane, clock.Now), Is.EqualTo(AircraftHudAction.PlanFlight),
                "planning a departure after the check is still allowed");
            Assert.That(OperationsSummary.CompactState(plane, clock.Now, ops.Clock), Does.StartWith("In check until"));
            var next = OperationsSummary.Objective(new[] { plane }, clock.Now, ops.Clock, ops.CareerState).NextLine;
            Assert.That(next.ToLowerInvariant(), Does.Contain("wait").And.Contain("check"));
        }

        [Test]
        public void Fleet_ParkedAircraftGetsACheckButton()
        {
            var (clock, ops, plane) = HudTestAirline.Create();
            var model = new FleetWorkspaceModel();
            model.Rebuild(ops, clock.Now, plane.Registration);

            Assert.That(model.CanStartCheck, Is.True);
            Assert.That(model.StartCheckLabel, Does.Contain("$"));
            Assert.That(model.SelectedCapability, Does.Contain("Check in 8 rotations"));

            var list = new HudDrawList();
            var surface = HudShell.WorkspaceSurface(1440f, 900f);
            FleetWorkspacePainter.Paint(list, model, FleetWorkspaceLayout.Create(surface, model.Market.Count),
                plane.Registration, 0);
            Assert.That(list.Commands.Any(c => c.ActionId == HudAction.StartCheck), Is.True);
        }
    }
}
