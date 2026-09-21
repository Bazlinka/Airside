using System.Collections.Generic;
using System.Linq;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// The Route Map workspace (ADR 0057): what the selected aircraft may actually fly,
    /// and what the route really costs and pays.
    /// </summary>
    public sealed class RouteMapWorkspaceTests
    {
        [Test]
        public void Map_SplitsAvailableFromLockedByRealCapability()
        {
            var (clock, ops, plane) = HudTestAirline.Create();
            var model = new RouteMapWorkspaceModel();
            model.Rebuild(ops, plane, null, 900, clock.Now, RouteMapFilter.Available);

            Assert.That(model.AvailableCount, Is.GreaterThan(0));
            Assert.That(model.LockedCount, Is.GreaterThan(0));
            Assert.That(model.ShownDestinations.All(d => d.Reachable), Is.True);
            Assert.That(model.ShownDestinations.Any(d => d.Destination.Code == "KGC"), Is.True);
            Assert.That(model.IsCareerTarget(HudTestAirline.Code("KGC")), Is.True,
                "the current campaign target should be visible before opening its detail");

            model.Rebuild(ops, plane, null, 900, clock.Now, RouteMapFilter.Locked);
            Assert.That(model.ShownDestinations.All(d => !d.Reachable), Is.True);
            Assert.That(model.ShownDestinations.Any(d => d.Destination.Code == "MEL"), Is.True,
                "Melbourne is Domestic — a Saab may not file it whatever its range");
        }

        [Test]
        public void Map_ExplainsWhenASelectedDestinationAdvancesTheCareer()
        {
            var (clock, ops, plane) = HudTestAirline.Create();
            var model = new RouteMapWorkspaceModel();

            model.Rebuild(ops, plane, HudTestAirline.Code("KGC"), 900, clock.Now, RouteMapFilter.Available);
            Assert.That(model.CareerLine, Does.Contain("Chapter 1 target"));
            Assert.That(model.CareerTone, Is.EqualTo(HudTone.Caution));
            Assert.That(model.IsCareerTarget(HudTestAirline.Code("KGC")), Is.True);

            model.Rebuild(ops, plane, HudTestAirline.Code("MEL"), 900, clock.Now, RouteMapFilter.Locked);
            Assert.That(model.CareerLine, Is.Empty);
        }

        [Test]
        public void Map_PricesADestinationFromTheSameEconomicsTheBookingCharges()
        {
            var (clock, ops, plane) = HudTestAirline.Create();
            var kingscote = HudTestAirline.Code("KGC");
            var model = new RouteMapWorkspaceModel();
            model.Rebuild(ops, plane, kingscote, 900, clock.Now, RouteMapFilter.Available);

            var km = ops.DistanceKm(kingscote);
            Assert.That(model.DispatchLine,
                Is.EqualTo($"Dispatch  ${FlightEconomics.DispatchCost(plane.Type, km):N0}"));
            Assert.That(model.ReturnLine, Is.EqualTo(
                $"Estimated return  ${FlightEconomics.FlightPay(plane.Type, km, RouteBand.Regional):N0}"));
            Assert.That(model.CompatibilityLine, Is.EqualTo("Saab 340B compatible"));
            Assert.That(model.AvailabilityLine, Is.EqualTo("Available with Regional capability"));
            Assert.That(model.CanPlan, Is.True);
        }

        [Test]
        public void Map_ExplainsWhyALockedDestinationCannotBeFlown()
        {
            var (clock, ops, plane) = HudTestAirline.Create();
            var model = new RouteMapWorkspaceModel();

            model.Rebuild(ops, plane, HudTestAirline.Code("MEL"), 900, clock.Now, RouteMapFilter.Available);
            Assert.That(model.CanPlan, Is.False);
            Assert.That(model.AvailabilityLine, Does.Contain("Domestic"));
            Assert.That(model.AvailabilityLine, Does.StartWith("Locked"));

            model.Rebuild(ops, plane, HudTestAirline.Code("PER"), 900, clock.Now, RouteMapFilter.Available);
            Assert.That(model.CanPlan, Is.False);
            Assert.That(model.AvailabilityLine, Does.Contain("range"));
        }

        [Test]
        public void Map_WillNotOfferAPlanTheSimulationWouldRefuse()
        {
            var (clock, ops, plane) = HudTestAirline.Create();
            Assert.That(ops.ScheduleDeparture(plane, HudTestAirline.Code("KGC"), new SimulationTime(600)).Accepted, Is.True);
            clock.Set(new SimulationTime(3600));
            ops.Update();
            Assert.That(plane.State, Is.Not.EqualTo(FleetState.AtStand), "it should be away by now");

            var model = new RouteMapWorkspaceModel();
            model.Rebuild(ops, plane, HudTestAirline.Code("PLO"), 900, clock.Now, RouteMapFilter.Available);
            Assert.That(model.CanPlan, Is.False);
            Assert.That(model.PlanBlockedReason, Does.Contain("parked"));
        }

        [Test]
        public void Map_ExposesTheSelectedAircraftsActualPracticalRange()
        {
            var (clock, ops, plane) = HudTestAirline.Create();
            var model = new RouteMapWorkspaceModel();

            model.Rebuild(ops, plane, null, 900, clock.Now, RouteMapFilter.Available);

            Assert.That(model.AircraftRangeKm, Is.EqualTo(plane.Type.PracticalRangeKm));
            Assert.That(model.AircraftRangeLabel, Does.Contain(plane.Type.Name));
            Assert.That(model.AircraftRangeLabel, Does.Contain($"{plane.Type.PracticalRangeKm:N0} km"));
        }

        [Test]
        public void Network_DrawsOnlyTheSelectedRouteAndDecluttersLabelsUntilZoomed()
        {
            var (clock, ops, plane) = HudTestAirline.Create();
            var model = new RouteMapWorkspaceModel();
            var selected = HudTestAirline.Code("KGC");
            model.Rebuild(ops, plane, selected, 900, clock.Now, RouteMapFilter.Available);
            var map = new HudBox(0f, 0f, 900f, 620f);
            var lens = new AustraliaMapLens();

            var unselected = new HudDrawList();
            RouteMapWorkspacePainter.PaintNetwork(unselected, map, lens, model.AllDestinations,
                ops.Home, null, ops.PlayerAirline.LiveryHex, model.AircraftRangeKm, model.AircraftRangeLabel);
            var selectedList = new HudDrawList();
            RouteMapWorkspacePainter.PaintNetwork(selectedList, map, lens, model.AllDestinations,
                ops.Home, selected, ops.PlayerAirline.LiveryHex, model.AircraftRangeKm, model.AircraftRangeLabel);

            Assert.That(selectedList.Commands.Count(c => c.Kind == HudDrawKind.Line),
                Is.GreaterThan(unselected.Commands.Count(c => c.Kind == HudDrawKind.Line)));
            Assert.That(unselected.Commands.Any(c => c.Text == selected.Name), Is.False,
                "default zoom should show dots rather than a wall of destination names");
            Assert.That(selectedList.Commands.Any(c => c.Text == selected.Name), Is.True,
                "the selected destination remains named at every zoom");
            Assert.That(selectedList.Commands.Any(c => c.Text == model.AircraftRangeLabel), Is.True);
        }

    
        [TestCase(1440f, 900f)]
        [TestCase(1600f, 900f)]
        [TestCase(1280f, 800f)]
        [TestCase(1024f, 640f)]
        [TestCase(800f, 600f)]
        [TestCase(1164f, 872f)]
        public void Controls_RivalsTogglePillsAndDetailNeverOverlap(float width, float height)
        {
            var layout = RouteMapWorkspaceLayout.Create(HudShell.WorkspaceSurface(width, height));
            var rivals = layout.RivalsToggle;
            var at = $"{width}x{height}";
            Assert.That(rivals.Width, Is.GreaterThan(80f), "rivals toggle collapsed " + at);
            for (var i = 0; i < 2; i++)
                Assert.That(Overlaps(rivals, layout.FilterBox(i)), Is.False, $"rivals toggle on filter pill {i} " + at);
            if (!layout.Detail.IsEmpty)
                Assert.That(Overlaps(rivals, layout.Detail), Is.False, "rivals toggle on the detail pane " + at);
            Assert.That(rivals.X, Is.GreaterThanOrEqualTo(layout.Map.X - 0.01f), at);
            Assert.That(rivals.Right, Is.LessThanOrEqualTo(layout.Map.Right + 0.01f), at);
            Assert.That(rivals.Bottom, Is.LessThanOrEqualTo(layout.Map.Bottom + 0.01f), at);
        }

        private static bool Overlaps(HudBox a, HudBox b) =>
            a.X < b.Right && b.X < a.Right && a.Y < b.Bottom && b.Y < a.Bottom;
}
}
