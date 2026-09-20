using System;
using System.Collections.Generic;
using System.Linq;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// The Fleet workspace (ADR 0057): your aircraft first, their real detail, and an
    /// aircraft market that only offers purchases the simulation would accept.
    /// </summary>
    public sealed class FleetWorkspaceTests
    {
        [Test]
        public void Fleet_ListsYourAircraftBeforeEveryOtherOperator()
        {
            var (clock, ops, _) = HudTestAirline.Create();
            var rival = Airline.Rex();
            ops.AddAirline(rival);
            ops.AddAircraft(rival, "VH-ZRC", AircraftType.Saab340, AirlineOperations.AdelaideRegionalBays[1]);
            ops.AddAircraft(ops.PlayerAirline, "VH-SUN", AircraftType.Atr42,
                AirlineOperations.AdelaideRegionalBays[2]);

            var model = new FleetWorkspaceModel();
            model.Rebuild(ops, clock.Now, null);

            Assert.That(model.Mine.Select(r => r.Registration), Is.EqualTo(new[] { "VH-PAX", "VH-SUN" }));
            Assert.That(model.Mine.All(r => r.IsPlayer), Is.True);
            Assert.That(model.Others.Select(r => r.Registration), Is.EqualTo(new[] { "VH-ZRC" }));
            Assert.That(model.Subtitle, Does.StartWith($"2 of {AircraftAcquisition.MaxPlayerAircraft} aircraft"));
        }

        [Test]
        public void Fleet_MarketUsesTheRealAcquisitionGatesAndPrices()
        {
            var (clock, ops, _) = HudTestAirline.Create();
            var model = new FleetWorkspaceModel();
            model.Rebuild(ops, clock.Now, null);

            Assert.That(model.Market.Select(o => o.Type.Id),
                Is.EqualTo(AircraftAcquisition.All.Select(o => o.Type.Id)));
            foreach (var offer in model.Market)
            {
                Assert.That(AircraftAcquisition.TryFor(offer.Type, out var authored), Is.True);
                Assert.That(offer.Price, Is.EqualTo(authored.Price));
                // A brand new Provisional airline has flown nothing, so nothing is buyable yet.
                Assert.That(offer.CanBuy, Is.False, offer.TypeName);
                Assert.That(offer.RequirementLine, Is.Not.Empty, offer.TypeName);
            }

            var dash = model.Market.Single(o => o.Type.Id == AircraftType.Dash8Q400.Id);
            Assert.That(dash.RequirementLine, Does.Contain(OperatingTier.Regional.ToString()));
        }

        [Test]
        public void Fleet_NeverOffersAPurchaseTheSimulationWouldRefuse()
        {
            var (clock, ops, _) = HudTestAirline.Create();
            var model = new FleetWorkspaceModel();
            model.Rebuild(ops, clock.Now, null);

            foreach (var offer in model.Market)
            {
                if (offer.CanBuy)
                    continue;
                Assert.That(ops.BuyAircraft(offer.Type).Accepted, Is.False,
                    $"{offer.TypeName} is drawn as locked, so buying it must be refused");
            }
        }

        [Test]
        public void Fleet_SelectionReportsRealCapabilityAssignmentAndPreparation()
        {
            var (clock, ops, plane) = HudTestAirline.Create();
            Assert.That(ops.ScheduleDeparture(plane, HudTestAirline.Code("KGC"), new SimulationTime(3600)).Accepted, Is.True);
            clock.Set(new SimulationTime(plane.PrepStartedAt!.Value.ElapsedSeconds + DeparturePrep.FuelSeconds + 10));
            ops.Update();

            var model = new FleetWorkspaceModel();
            model.Rebuild(ops, clock.Now, "VH-PAX");

            Assert.That(model.HasSelection, Is.True);
            // Names real destinations now, not just the bare band label — "Requires Regional
            // tier" and "flies Regional routes" used to share the word "Regional" for two
            // unrelated systems with nothing to tell them apart.
            Assert.That(model.SelectedCapability,
                Does.Contain("Regional capability (Kingscote, Port Lincoln)"));
            Assert.That(model.SelectedCapability, Does.Contain("0 completed rotations"));
            Assert.That(model.AssignmentLine, Is.EqualTo("Adelaide → Kingscote"));
            Assert.That(model.SelectedPrep.Select(p => p.Done), Is.EqualTo(new[] { true, false, false }));
            Assert.That(model.SelectedPrep[1].Active, Is.True);
        }

        [Test]
        public void Fleet_SelectionShowsResaleValueForABoughtAircraftButNotTheStarter()
        {
            var (clock, ops, plane) = HudTestAirline.Create();
            ops.RestoreCareerState(50_000, 100, nameof(OperatingTier.Regional), null, 0, 0,
                Array.Empty<string>(), Array.Empty<string>(), 12);
            Assert.That(ops.BuyAircraft(AircraftType.Dash8Q400).Accepted, Is.True);
            var bought = ops.Fleet.Single(a => a.Type.Id == AircraftType.Dash8Q400.Id);

            var model = new FleetWorkspaceModel();
            model.Rebuild(ops, clock.Now, plane.Registration);
            Assert.That(model.SelectedCapability.Any(line => line.StartsWith("Resale value")), Is.False,
                "the starter ATR was never bought, so it has no resale line");

            model.Rebuild(ops, clock.Now, bought.Registration);
            var expected = (long)Math.Round(AircraftAcquisition.Dash8Q400.Price * AirlineOperations.ResaleFraction);
            Assert.That(model.SelectedCapability, Does.Contain($"Resale value ${expected:N0}"));
        }

        [Test]
        public void Fleet_LayoutKeepsTheMarketInsideTheSurface()
        {
            foreach (var (width, height) in HudTestAirline.Viewports)
            {
                var surface = HudShell.WorkspaceSurface(width, height);
                var layout = FleetWorkspaceLayout.Create(surface, AircraftAcquisition.All.Count);
                var label = $"{width}x{height}";

                Assert.That(layout.Roster.Overlaps(layout.Market), Is.False, label);
                Assert.That(layout.Detail.Overlaps(layout.Market), Is.False, label);
                Assert.That(layout.Roster.Overlaps(layout.Detail), Is.False, label);
                if (layout.MarketRows > 0)
                    Assert.That(layout.MarketRow(layout.MarketRows - 1).Bottom,
                        Is.LessThanOrEqualTo(surface.Bottom + 0.01f), label);
                Assert.That(layout.Roster.Bottom, Is.LessThanOrEqualTo(surface.Bottom + 0.01f), label);
            }
        }

    }
}
