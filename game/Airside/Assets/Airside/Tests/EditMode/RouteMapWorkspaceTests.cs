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

            model.Rebuild(ops, plane, null, 900, clock.Now, RouteMapFilter.Locked);
            Assert.That(model.ShownDestinations.All(d => !d.Reachable), Is.True);
            Assert.That(model.ShownDestinations.Any(d => d.Destination.Code == "MEL"), Is.True,
                "Melbourne is Domestic — an ATR may not file it whatever its range");
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
            Assert.That(model.CompatibilityLine, Is.EqualTo("ATR 42-600 compatible"));
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

    }
}
