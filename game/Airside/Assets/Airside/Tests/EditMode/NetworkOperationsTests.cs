using System;
using System.Collections.Generic;
using System.Linq;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class NetworkOperationsTests
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
        public void Overview_IncludesEveryBaseOnceAndCountsOnlyDispatchableAircraft()
        {
            var (clock, ops, plane) = Setup();
            var other = Airline.Rex();
            ops.AddAirline(other);
            ops.AddAircraft(other, "VH-AI", AircraftType.Saab340,
                AirlineOperations.AdelaideRegionalBays[1]);
            var model = new OperationsWorkspaceModel();
            model.Rebuild(ops, clock.Now, OperationsBoardTab.Departures, null, null);
            Assert.That(model.AirlineRows.Select(r => r.Registration), Is.EquivalentTo(new[] { plane.Registration, "VH-O01" }));
            Assert.That(model.AvailableAircraft, Is.EqualTo(2));
            Assert.That(ops.ScheduleOutstationService("VH-O01", "SYD", clock.Now.Advance(1800)).Accepted, Is.True);
            model.Rebuild(ops, clock.Now, OperationsBoardTab.Departures, null, null);
            Assert.That(model.AvailableAircraft, Is.EqualTo(1));
            Assert.That(model.AirlineRows.First().Registration, Is.EqualTo("VH-O01"), "booked work is above idle aircraft");
        }

        [Test]
        public void NetworkRow_ChangesDirectionAndNextEventForEachRealServicePhase()
        {
            var (clock, ops, _) = Setup();
            var aircraft = ops.OutstationFleet[0];
            Assert.That(ops.ScheduleOutstationService(aircraft.Registration, "SYD", clock.Now.Advance(1800)).Accepted, Is.True);
            var leg = (aircraft.ReturnAtSeconds - aircraft.DepartAtSeconds - PlayerFleet.TurnaroundSeconds) / 2;
            var rows = new List<OperationsRow>();
            OperationsRow Row(long at)
            {
                OperationsSummary.FillAirlineRows(ops, new SimulationTime(at), rows);
                return rows.Single(r => r.Registration == aircraft.Registration);
            }
            Assert.That(Row(clock.Now.ElapsedSeconds).TimeText, Is.EqualTo("Departs " + ops.Clock.TimeText(new SimulationTime(aircraft.DepartAtSeconds))));
            var outbound = Row(aircraft.DepartAtSeconds + leg / 2);
            Assert.That(outbound.State, Is.EqualTo("Outbound"));
            Assert.That(outbound.RouteText, Is.EqualTo("Melbourne → Sydney"));
            Assert.That(outbound.Progress01, Is.EqualTo(.5f).Within(.001));
            Assert.That(outbound.TimeText, Is.EqualTo("Arrives " + ops.Clock.TimeText(new SimulationTime(aircraft.DepartAtSeconds + leg))));
            var turnaround = Row(aircraft.DepartAtSeconds + leg + PlayerFleet.TurnaroundSeconds / 2);
            Assert.That(turnaround.State, Is.EqualTo("Turnaround"));
            Assert.That(turnaround.RouteText, Is.EqualTo("At Sydney · returns to Melbourne"));
            Assert.That(turnaround.TimeText, Is.EqualTo("Leaves " + ops.Clock.TimeText(new SimulationTime(aircraft.DepartAtSeconds + leg + PlayerFleet.TurnaroundSeconds))));
            var inbound = Row(aircraft.ReturnAtSeconds - leg / 2);
            Assert.That(inbound.State, Is.EqualTo("Inbound"));
            Assert.That(inbound.RouteText, Is.EqualTo("Sydney → Melbourne"));
            Assert.That(inbound.TimeText, Is.EqualTo("Returns " + ops.Clock.TimeText(new SimulationTime(aircraft.ReturnAtSeconds))));
            Assert.That(aircraft.CompletedServices, Is.Zero, "reading the overview does not settle a flight");
        }

        [Test]
        public void NetworkCheck_TakesPriorityAndDoesNotPretendAircraftIsAvailable()
        {
            var (clock, ops, _) = Setup();
            ops.RestoreNetworkState(new[] { new OutstationAircraft("VH-O01", AircraftType.Dash8Q400, "MEL",
                rotationsSinceCheck: Maintenance.IntervalRotations) }, Array.Empty<RepeatSchedule>());
            var model = new OperationsWorkspaceModel();
            model.Rebuild(ops, clock.Now, OperationsBoardTab.Departures, null, null);
            Assert.That(model.AirlineRows[0].State, Is.EqualTo("Check overdue"));
            Assert.That(model.AvailableAircraft, Is.EqualTo(1));
            Assert.That(ops.StartOutstationCheck("VH-O01").Accepted, Is.True);
            model.Rebuild(ops, clock.Now, OperationsBoardTab.Departures, null, null);
            Assert.That(model.AirlineRows.Single(r => r.Registration == "VH-O01").State, Is.EqualTo("Routine check"));
            Assert.That(model.AvailableAircraft, Is.EqualTo(1));
        }

        [Test]
        public void OpenFromOperations_ClearsFiltersAndShowsCorrectAircraftControls()
        {
            var (clock, ops, plane) = Setup();
            var board = new FleetBoardState { BaseFilter = "ADL", Status = FleetStatusFilter.Flying,
                ShowMarket = true, ReviewRoute = "KGC", CancelReview = true, RoutePage = 4 };
            board.Focus(PlayerFleet.Find(ops, clock.Now, "VH-O01"));
            var model = new FleetWorkspaceModel();
            model.Rebuild(ops, clock.Now, "VH-O01", board);
            Assert.That(model.SelectedRegistration, Is.EqualTo("VH-O01"));
            Assert.That(model.SelectedBaseCode, Is.EqualTo("MEL"));
            Assert.That(board.ShowMarket || board.CancelReview, Is.False);
            Assert.That(board.ReviewRoute, Is.Empty);
            board.Focus(PlayerFleet.Find(ops, clock.Now, plane.Registration));
            model.Rebuild(ops, clock.Now, plane.Registration, board);
            Assert.That(model.SelectedRegistration, Is.EqualTo(plane.Registration));
            Assert.That(model.SelectedBaseCode, Is.EqualTo("ADL"));
        }

        [Test]
        public void GrowingOverview_LastAircraftRemainsReachableInCompactWindows()
        {
            var (clock, ops, _) = Setup();
            ops.RestoreNetworkState(Enumerable.Range(1, 12).Select(i => new OutstationAircraft("VH-N" + i.ToString("00"),
                AircraftType.Dash8Q400, "MEL")), Array.Empty<RepeatSchedule>());
            var model = new OperationsWorkspaceModel();
            model.Rebuild(ops, clock.Now, OperationsBoardTab.Departures, null, null);
            foreach (var size in new[] { (1440f, 900f), (1280f, 720f), (900f, 720f), (800f, 600f) })
            {
                var surface = HudShell.WorkspaceSurface(size.Item1, size.Item2);
                var layout = OperationsWorkspaceLayout.Create(surface, 0, airlineView: true);
                var list = new HudDrawList();
                OperationsWorkspacePainter.Paint(list, model, layout, null, model.AirlineRows.Count);
                Assert.That(list.Commands.Any(c => c.ActionId == HudAction.Select(model.AirlineRows.Last().Registration)), Is.True, size.ToString());
                Assert.That(list.Commands.Any(c => c.Text == "AIRPORT MOVEMENTS" && c.ActionId == HudAction.ToggleMovements), Is.True);
                Assert.That(list.Commands.Where(c => c.Kind == HudDrawKind.Hotspot).All(c => c.Box.Bottom <= surface.Bottom), Is.True);
                Assert.That(layout.Detail.IsEmpty, Is.True);
                Assert.That(layout.VisibleRows, Is.GreaterThanOrEqualTo(3), "compact airline view keeps room for multiple aircraft");
            }
        }
    }
}
