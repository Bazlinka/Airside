using System;
using System.Linq;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class FleetFlightClarityTests
    {
        private static (ManualSimulationClock Clock, AirlineOperations Ops, FleetAircraft Plane) Setup()
        {
            var (clock, ops, plane) = HudTestAirline.Create();
            ops.RestoreCareerState(500_000, 95, nameof(OperatingTier.Domestic), null, 0, 0,
                Array.Empty<string>(), Array.Empty<string>(), 100, baseLevel: PlayerBaseLevel.ExpandedRegional, manualRotations: 12);
            Assert.That(ops.OpenOutstationBase("MEL").Accepted, Is.True);
            Assert.That(ops.BuyAircraftAtOutstation(AircraftType.Dash8Q400, "MEL").Accepted, Is.True);
            return (clock, ops, plane);
        }

        [Test]
        public void AirportChange_SelectsVisibleAircraftAndEmptyBaseClearsDetails()
        {
            var (clock, ops, plane) = Setup();
            var model = new FleetWorkspaceModel();
            model.Rebuild(ops, clock.Now, plane.Registration, new FleetBoardState { BaseFilter = "MEL" });
            Assert.That(model.SelectedRegistration, Is.EqualTo("VH-O01"));
            Assert.That(model.SelectedBaseCode, Is.EqualTo("MEL"));
            model.Rebuild(ops, clock.Now, "VH-O01", new FleetBoardState { BaseFilter = "SYD" });
            Assert.That(model.HasSelection, Is.False);
        }

        [Test]
        public void Available_ExcludesBookingsAndDueChecksAcrossBothBases()
        {
            var (clock, ops, plane) = Setup();
            var board = new FleetBoardState { Status = FleetStatusFilter.Available };
            var model = new FleetWorkspaceModel();
            model.Rebuild(ops, clock.Now, null, board);
            Assert.That(model.AvailableCount, Is.EqualTo(2));
            Assert.That(ops.ScheduleDeparture(plane, HudTestAirline.Code("KGC"), clock.Now.Advance(1800)).Accepted, Is.True);
            model.Rebuild(ops, clock.Now, plane.Registration, board);
            Assert.That(model.Mine.Select(r => r.Registration), Is.EqualTo(new[] { "VH-O01" }));
            Assert.That(model.AvailableCount, Is.EqualTo(1));
            Assert.That(model.SelectedRegistration, Is.EqualTo("VH-O01"));
            Assert.That(ops.ScheduleOutstationService("VH-O01", "SYD", clock.Now.Advance(1800)).Accepted, Is.True);
            model.Rebuild(ops, clock.Now, null, board);
            Assert.That(model.AvailableCount, Is.Zero);
            Assert.That(model.Mine, Is.Empty);
            Assert.That(model.HasSelection, Is.False);
        }

        [Test]
        public void RemoteReview_DoesNotChargeAndRefusesExistingService()
        {
            var (clock, ops, _) = Setup();
            var board = new FleetBoardState { BaseFilter = "MEL", ReviewRoute = "SYD" };
            var model = new FleetWorkspaceModel();
            var funds = ops.CareerState.Funds;
            model.Rebuild(ops, clock.Now, "VH-O01", board);
            Assert.That(model.RouteReview, Is.EqualTo("Sydney"));
            Assert.That(model.CanConfirmRoute, Is.True);
            Assert.That(ops.CareerState.Funds, Is.EqualTo(funds));
            Assert.That(ops.OutstationFleet[0].HasFlight, Is.False);
            Assert.That(ops.ScheduleOutstationService("VH-O01", "SYD", clock.Now.Advance(1800)).Accepted, Is.True);
            model.Rebuild(ops, clock.Now, "VH-O01", board);
            Assert.That(model.CanConfirmRoute, Is.False);
        }

        [Test]
        public void RemoteQuote_UsesTheSameReliabilityAdjustedPaymentAsSettlement()
        {
            var (clock, ops, _) = Setup();
            var aircraft = ops.OutstationFleet[0];
            var origin = HudTestAirline.Code("MEL");
            var destination = HudTestAirline.Code("SYD");
            var expected = FlightPlanner.ExpectedRevenue(ops, origin, destination, aircraft.Type);
            Assert.That(ops.ScheduleOutstationService(aircraft.Registration, "SYD", clock.Now.Advance(1800)).Accepted, Is.True);
            clock.Set(new SimulationTime(aircraft.ReturnAtSeconds));
            ops.Update();
            Assert.That(aircraft.LifetimeRevenue, Is.EqualTo(expected));
        }

        [Test]
        public void RemoteCancellation_RefundsOnceAndPausesRepeatWithoutAddingAFlight()
        {
            var (clock, ops, _) = Setup();
            var aircraft = ops.OutstationFleet[0];
            Assert.That(ops.SetRepeatSchedule(aircraft.Registration, "SYD", 12).Accepted, Is.True);
            var before = ops.CareerState.Funds;
            Assert.That(ops.ScheduleOutstationService(aircraft.Registration, "SYD", clock.Now.Advance(1800)).Accepted, Is.True);
            Assert.That(ops.CareerState.Funds, Is.LessThan(before));
            Assert.That(ops.CancelOutstationService(aircraft.Registration).Accepted, Is.True);
            Assert.That(ops.CareerState.Funds, Is.EqualTo(before));
            Assert.That(aircraft.HasFlight, Is.False);
            Assert.That(aircraft.CompletedServices, Is.Zero);
            Assert.That(ops.RepeatSchedules.Single().Paused, Is.True);
            Assert.That(ops.CancelOutstationService(aircraft.Registration).Accepted, Is.False);
            Assert.That(ops.CareerState.Funds, Is.EqualTo(before));
        }

        [Test]
        public void RemoteCancellation_RefusesAfterDepartureAndKeepsPaidService()
        {
            var (clock, ops, _) = Setup();
            var aircraft = ops.OutstationFleet[0];
            Assert.That(ops.ScheduleOutstationService(aircraft.Registration, "SYD", clock.Now.Advance(1800)).Accepted, Is.True);
            var after = ops.CareerState.Funds;
            clock.Set(new SimulationTime(aircraft.DepartAtSeconds));
            ops.Update();
            Assert.That(ops.CancelOutstationService(aircraft.Registration).Accepted, Is.False);
            Assert.That(ops.CareerState.Funds, Is.EqualTo(after));
            Assert.That(aircraft.HasFlight, Is.True);
        }

        [Test]
        public void RemoteJourney_FollowsBothLegsAndEndsViewsOnGroundWithoutChangingBooking()
        {
            var (clock, ops, _) = Setup();
            var aircraft = ops.OutstationFleet[0];
            Assert.That(ops.ScheduleOutstationService(aircraft.Registration, "SYD", clock.Now.Advance(1800)).Accepted, Is.True);
            var ends = aircraft.ReturnAtSeconds;
            var leg = (ends - aircraft.DepartAtSeconds - PlayerFleet.TurnaroundSeconds) / 2;
            Assert.That(OutstationJourney.TryFor(aircraft, clock.Now, out var parked), Is.True);
            Assert.That(parked.Airborne, Is.False);
            Assert.That(OutstationJourney.TryFor(aircraft, aircraft.DepartAtSeconds + leg * .5, out var outbound), Is.True);
            Assert.That(outbound.Phase, Is.EqualTo(OutstationPhase.Outbound));
            Assert.That(outbound.Progress, Is.EqualTo(.5).Within(.0001));
            Assert.That(outbound.AltitudeFeet, Is.GreaterThan(0));
            Assert.That(outbound.SpeedKnots, Is.GreaterThan(0));
            Assert.That(OutstationJourney.TryFor(aircraft, aircraft.DepartAtSeconds + leg + 100, out var turnaround), Is.True);
            Assert.That(turnaround.Airborne, Is.False);
            Assert.That(OutstationJourney.TryFor(aircraft, ends - leg * .5, out var inbound), Is.True);
            Assert.That(inbound.Phase, Is.EqualTo(OutstationPhase.Inbound));
            Assert.That(aircraft.ReturnAtSeconds, Is.EqualTo(ends));
            Assert.That(aircraft.CompletedServices, Is.Zero);
        }

        [Test]
        public void RemoteReview_HasBackAndCommitControlsInEveryDesktopSize()
        {
            var (clock, ops, _) = Setup();
            foreach (var size in new[] { (1440f, 900f), (1280f, 720f), (900f, 720f), (800f, 600f) })
            {
                var model = new FleetWorkspaceModel();
                model.Rebuild(ops, clock.Now, "VH-O01", new FleetBoardState { ReviewRoute = "SYD" });
                var surface = HudShell.SideSheet(HudShell.WorkspaceSurface(size.Item1, size.Item2));
                var layout = FleetWorkspaceLayout.Create(surface, model.Market.Count, false);
                var list = new HudDrawList();
                FleetWorkspacePainter.Paint(list, model, layout, model.SelectedRegistration, 0);
                Assert.That(list.Commands.Any(c => c.ActionId == FleetActions.ConfirmRoute && c.Enabled), Is.True, size.ToString());
                Assert.That(list.Commands.Any(c => c.ActionId == FleetActions.BackRoutes), Is.True, size.ToString());
                Assert.That(list.Commands.Where(c => !c.Box.IsEmpty).All(c => c.Box.Bottom <= surface.Bottom + .01f), Is.True);
                Assert.That(layout.Market.IsEmpty, Is.True);
            }
        }
    }
}
