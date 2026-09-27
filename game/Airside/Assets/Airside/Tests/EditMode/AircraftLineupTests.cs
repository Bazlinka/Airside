using System.Linq;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>ADR 0131 — every modelled type is for sale, each with a reason to buy it.</summary>
    public sealed class AircraftLineupTests
    {
        [Test]
        public void EveryModelledTypeExceptTheStarterIsForSale_WithItsArt()
        {
            var forSale = AircraftAcquisition.All.Select(o => o.Type.Id).ToList();
            Assert.That(forSale, Is.Unique);
            foreach (var spec in AircraftCatalogue.All)
            {
                if (spec.Type.Id == AircraftType.Saab340.Id)
                    continue;
                Assert.That(forSale, Does.Contain(spec.Type.Id), spec.Name);
                Assert.That(spec.RuntimeModelPath, Is.Not.Null.And.Not.Empty, spec.Name);
                Assert.That(FleetWorkspacePainter.Thumbnail(spec.Type), Does.StartWith("UI/Aircraft/"), spec.Name);
            }
        }

        [Test]
        public void EveryTypeCanReachSomewhereInItsOwnBand()
        {
            foreach (var offer in AircraftAcquisition.All)
            {
                var ceiling = RouteAccess.Ceiling(offer.Type);
                var reachable = DestinationCatalogue.All.Any(d => RouteAccess.BandOf(d) == ceiling
                    && offer.Type.CanReach(DestinationCatalogue.Adelaide.DistanceKmTo(d)));
                Assert.That(reachable, Is.True, $"{offer.Type.Name} is cleared for {ceiling} but reaches none of it");
            }
        }

        [Test]
        public void EachNewTypeIsARealTradeOff()
        {
            // The first jet is the cheapest jet, and opens the Domestic tier's "fly a jet" goal.
            var jets = AircraftAcquisition.All.Where(o => AirlineOperations.NeedsTerminalGate(o.Type)).ToList();
            Assert.That(jets.OrderBy(o => o.Price).First().Type, Is.EqualTo(AircraftType.EmbraerE190));
            Assert.That(AirlineCareerState.OwnsAnyJet(new[] { AircraftType.EmbraerE190 }), Is.True);

            // Older narrowbodies cost less to buy than the 737-8 and more to fly.
            foreach (var older in new[] { AircraftAcquisition.Boeing737800, AircraftAcquisition.AirbusA320200 })
            {
                Assert.That(older.Price, Is.LessThan(AircraftAcquisition.Boeing7378.Price), older.Type.Name);
                Assert.That(FlightEconomics.CostPerKm(older.Type),
                    Is.GreaterThan(FlightEconomics.CostPerKm(AircraftType.Boeing7378)), older.Type.Name);
            }

            // The new widebodies are cheaper ways into long-haul, and count for the widebody goal.
            Assert.That(AircraftAcquisition.AirbusA330900.Price, Is.LessThan(AircraftAcquisition.Boeing78710.Price));
            Assert.That(AircraftAcquisition.Boeing7879.Price, Is.LessThan(AircraftAcquisition.Boeing78710.Price));
            var goals = CareerRoadmap.Evaluate(new AirlineCareerState(), new[] { AircraftType.Saab340, AircraftType.AirbusA330900 });
            Assert.That(goals.Single(g => g.Id == "international-widebody").Complete, Is.True);

            // Pay never depends on the running-cost factor.
            Assert.That(FlightEconomics.FlightPay(AircraftType.Boeing737800, 1200),
                Is.EqualTo(FlightEconomics.FlightPay(AircraftType.Boeing7378, 1200)));
        }

        [Test]
        public void FleetMarket_WarnsWhenABuyLeavesTooLittleToFlyIt()
        {
            var (clock, ops, _) = HudTestAirline.Create();
            var a330 = AircraftAcquisition.AirbusA330900;
            // Just enough for the A330, and a little over: not enough for a long-haul flight.
            ops.RestoreCareerState(a330.Price + 500, 100, nameof(OperatingTier.International), null, 0, 0,
                System.Array.Empty<string>(), System.Array.Empty<string>(), 60, null, 0, null,
                baseLevel: PlayerBaseLevel.International);
            var model = new FleetWorkspaceModel();
            model.Rebuild(ops, clock.Now, null);
            var offer = model.Market.Single(o => o.Type == AircraftType.AirbusA330900);
            Assert.That(offer.CashWarning, Does.StartWith("Leaves $500."));
            Assert.That(FlightEconomics.TypicalLegKm(AircraftType.AirbusA330900), Is.EqualTo(6000.0));
            Assert.That(FlightEconomics.TypicalLegKm(AircraftType.Saab340), Is.EqualTo(300.0));

            ops.RestoreCareerState(a330.Price + 50_000, 100, nameof(OperatingTier.International), null, 0, 0,
                System.Array.Empty<string>(), System.Array.Empty<string>(), 60, null, 0, null,
                baseLevel: PlayerBaseLevel.International);
            model.Rebuild(ops, clock.Now, null);
            Assert.That(model.Market.Single(o => o.Type == AircraftType.AirbusA330900).CashWarning, Is.Empty);
        }

        [Test]
        public void FleetMarket_LeadsWithWhatYouCanBuyAndPages()
        {
            var (clock, ops, _) = HudTestAirline.Create();
            ops.RestoreCareerState(500_000, 100, nameof(OperatingTier.International), null, 0, 0,
                System.Array.Empty<string>(), System.Array.Empty<string>(), 60, null, 0, null,
                baseLevel: PlayerBaseLevel.International);
            var model = new FleetWorkspaceModel();
            model.Rebuild(ops, clock.Now, null);
            Assert.That(model.Market.Count, Is.EqualTo(AircraftAcquisition.All.Count));
            var firstLocked = model.Market.ToList().FindIndex(o => !o.CanBuy);
            if (firstLocked >= 0)
                Assert.That(model.Market.Skip(firstLocked).Any(o => o.CanBuy), Is.False, "buyable offers come first");

            Assert.That(FleetWorkspacePainter.ClampMarketStart(99, 12, 3), Is.EqualTo(9));
            Assert.That(FleetWorkspacePainter.ClampMarketStart(-4, 12, 3), Is.EqualTo(0));
            Assert.That(FleetWorkspacePainter.ClampMarketStart(5, 2, 3), Is.EqualTo(0));

            var layout = FleetWorkspaceLayout.Create(HudShell.WorkspaceSurface(1440f, 900f), model.Market.Count);
            var draw = new HudDrawList();
            FleetWorkspacePainter.Paint(draw, model, layout, null, 0, marketStart: 3);
            Assert.That(draw.Commands.Any(c => c.ActionId == FleetWorkspacePainter.MarketNext), Is.True);
            Assert.That(draw.Commands.Any(c => c.Kind == HudDrawKind.Text && c.Text == model.Market[3].TypeName), Is.True,
                "the second page shows the fourth offer");
        }
    }
}
