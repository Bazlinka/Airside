using System.Collections.Generic;
using System.Linq;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// Every workspace painter draws inside its own surface and gives each control an
    /// action the HUD can dispatch (ADR 0057).
    /// </summary>
    public sealed class HudWorkspacePainterTests
    {
        [Test]
        public void Painters_DrawEveryWorkspaceInsideItsOwnSurface()
        {
            var (clock, ops, plane) = HudTestAirline.Create();
            Assert.That(ops.ScheduleDeparture(plane, HudTestAirline.Code("KGC"), new SimulationTime(3600)).Accepted, Is.True);
            Assert.That(ops.AcceptContract(RouteContractCatalogue.RegionalKingscoteIntro).Accepted, Is.True);
            clock.Set(new SimulationTime(120));
            ops.Update();

            foreach (var (width, height) in HudTestAirline.Viewports)
            {
                var surface = HudShell.WorkspaceSurface(width, height);
                var list = new HudDrawList();

                var operations = new OperationsWorkspaceModel();
                operations.Rebuild(ops, clock.Now, OperationsBoardTab.Departures, "VH-PAX", null);
                OperationsWorkspacePainter.Paint(list, operations,
                    OperationsWorkspaceLayout.Create(surface, operations.Attention.Count), "VH-PAX", 0);
                AssertInside(list, surface, $"operations {width}x{height}");

                var map = new RouteMapWorkspaceModel();
                map.Rebuild(ops, plane, HudTestAirline.Code("KGC"), 900, clock.Now, RouteMapFilter.Available);
                RouteMapWorkspacePainter.Paint(list, map, RouteMapWorkspaceLayout.Create(surface));
                AssertInside(list, surface, $"map {width}x{height}");

                var fleet = new FleetWorkspaceModel();
                fleet.Rebuild(ops, clock.Now, "VH-PAX");
                FleetWorkspacePainter.Paint(list, fleet,
                    FleetWorkspaceLayout.Create(surface, fleet.Market.Count), "VH-PAX", 0);
                AssertInside(list, surface, $"fleet {width}x{height}");

                var contracts = new ContractsWorkspaceModel();
                contracts.Rebuild(ops, clock.Now);
                ContractsWorkspacePainter.Paint(list, contracts,
                    ContractsWorkspaceLayout.Create(surface), null);
                AssertInside(list, surface, $"contracts {width}x{height}");
            }
        }

        [Test]
        public void Painters_GiveEveryButtonAnActionTheHudCanDispatch()
        {
            var (clock, ops, plane) = HudTestAirline.Create();
            clock.Set(new SimulationTime(60));
            ops.Update();

            var surface = HudShell.WorkspaceSurface(1440f, 900f);
            var list = new HudDrawList();
            var fleet = new FleetWorkspaceModel();
            fleet.Rebuild(ops, clock.Now, "VH-PAX");
            FleetWorkspacePainter.Paint(list, fleet, FleetWorkspaceLayout.Create(surface, fleet.Market.Count),
                "VH-PAX", 0);

            foreach (var command in list.Commands)
            {
                if (command.Kind != HudDrawKind.Button && command.Kind != HudDrawKind.Hotspot)
                    continue;
                Assert.That(command.ActionId, Is.Not.Empty, command.Text);
            }

            Assert.That(list.Commands.Any(c => c.ActionId == HudAction.Select("VH-PAX")), Is.True);
            Assert.That(list.Commands.Any(c => c.ActionId == HudAction.Buy(AircraftType.Saab340.Id)), Is.True);
            Assert.That(HudAction.Payload(HudAction.Select("VH-PAX"), HudAction.SelectPrefix),
                Is.EqualTo("VH-PAX"));
            Assert.That(HudAction.Payload("nonsense", HudAction.SelectPrefix), Is.Empty);
            _ = plane;
        }

        private static void AssertInside(HudDrawList list, HudBox surface, string label)
        {
            foreach (var command in list.Commands)
            {
                if (command.Kind == HudDrawKind.Line || command.Box.IsEmpty)
                    continue;
                Assert.That(command.Box.X, Is.GreaterThanOrEqualTo(surface.X - 0.01f),
                    $"{label}: {command.Kind} '{command.Text}' left of the surface");
                Assert.That(command.Box.Right, Is.LessThanOrEqualTo(surface.Right + 0.01f),
                    $"{label}: {command.Kind} '{command.Text}' right of the surface");
                Assert.That(command.Box.Y, Is.GreaterThanOrEqualTo(surface.Y - 0.01f),
                    $"{label}: {command.Kind} '{command.Text}' above the surface");
                Assert.That(command.Box.Bottom, Is.LessThanOrEqualTo(surface.Bottom + 0.01f),
                    $"{label}: {command.Kind} '{command.Text}' below the surface");
            }
        }

        /// <summary>Park an AI aircraft and cancel the departure <c>AddAircraft</c> books for it.</summary>
        private static void Park(AirlineOperations ops, Domain.Airline airline, string registration,
            StableId stand)
        {
            var aircraft = ops.AddAircraft(airline, registration, AircraftType.Saab340, stand);
            ops.CancelDeparture(aircraft);
        }

        private static void HoldEveryBay(AirlineOperations ops, Domain.Airline airline)
        {
            foreach (var aircraft in ops.FleetOf(airline))
                if (aircraft.State == FleetState.AtStand && aircraft.Scheduled.HasValue)
                    ops.CancelDeparture(aircraft);
        }

    }
}
