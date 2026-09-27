using System.Linq;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>ADR 0130 — the departures board, aircraft pictures, offer chips and the recent-flights list.</summary>
    public sealed class GameFeelHudTests
    {
        [Test]
        public void FlapField_DrawsOneTilePerCellAndCutsLongText()
        {
            var draw = new HudDrawList();
            var end = OperationsWorkspacePainter.FlapField(draw, 10f, 0f, 12f, 20f, 11f, "Kangaroo Island", 8,
                HudTone.Default, 1f);
            Assert.That(end, Is.EqualTo(10f + 8 * 12f));
            var tiles = draw.Commands.Count(c => c.Kind == HudDrawKind.Fill && c.ColourHex == OperationsWorkspacePainter.FlapTileHex);
            Assert.That(tiles, Is.EqualTo(8));
            var letters = string.Concat(draw.Commands.Where(c => c.Kind == HudDrawKind.Text).Select(c => c.Text));
            Assert.That(letters, Is.EqualTo("KANGAROO"), "upper-cased, cut to the cells, spaces left blank");
        }

        [Test]
        public void BoardPlace_PrintsTheFarEndOfTheRoute()
        {
            Assert.That(OperationsWorkspacePainter.BoardPlace("ADL → SYD"), Is.EqualTo("Sydney"));
            Assert.That(OperationsWorkspacePainter.BoardPlace("KGC → ADL"), Is.EqualTo("Kingscote"));
            Assert.That(OperationsWorkspacePainter.BoardPlace("ADL → —"), Is.Empty);
        }

        [Test]
        public void EveryBuyableTypeHasAPicture()
        {
            foreach (var offer in AircraftAcquisition.All)
                Assert.That(FleetWorkspacePainter.Thumbnail(offer.Type), Does.StartWith("UI/Aircraft/"), offer.Type.Name);
            Assert.That(FleetWorkspacePainter.Thumbnail(AircraftType.Saab340), Does.StartWith("UI/Aircraft/"));
        }

        [Test]
        public void ContractOffers_SayASharedLockOnceAndShowTheirTermsAsChips()
        {
            var (clock, ops, _) = HudTestAirline.Create();
            Assert.That(ops.AcceptContract(RouteContractCatalogue.RegionalKingscoteIntro).Accepted, Is.True);
            var model = new ContractsWorkspaceModel();
            model.Rebuild(ops, clock.Now);
            Assume.That(model.Offers.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(ContractsWorkspacePainter.SharedLockReason(model.Offers, model.Offers.Count),
                Does.StartWith("One contract at a time"));

            var layout = ContractsWorkspaceLayout.Create(HudShell.WorkspaceSurface(1440f, 900f));
            var draw = new HudDrawList();
            ContractsWorkspacePainter.Paint(draw, model, layout, null);
            Assert.That(draw.Commands.Count(c => c.Kind == HudDrawKind.Text && c.Text.StartsWith("One contract at a time")),
                Is.EqualTo(1), "said once, not on every card");

            var definition = model.Offers[0].Definition;
            var chips = ContractsWorkspacePainter.Chips(definition);
            Assert.That(chips[0], Does.EndWith(definition.RequiredRotations == 1 ? "flight" : "flights"));
            Assert.That(chips, Does.Contain(definition.EligibleType.Name));
        }

        [Test]
        public void FleetMarket_SaysASharedLockOnce()
        {
            var (clock, ops, _) = HudTestAirline.Create();
            var model = new FleetWorkspaceModel();
            model.Rebuild(ops, clock.Now, null);
            var layout = FleetWorkspaceLayout.Create(HudShell.WorkspaceSurface(1440f, 900f), model.Market.Count);
            var shared = FleetWorkspacePainter.SharedLockReason(model.Market, layout.MarketRows);
            Assume.That(shared, Is.Not.Null, "a fresh airline's starter base is full, so every offer shares that lock");
            var draw = new HudDrawList();
            FleetWorkspacePainter.Paint(draw, model, layout, null, 0);
            Assert.That(draw.Commands.Count(c => c.Kind == HudDrawKind.Text && c.Text == shared), Is.EqualTo(1));
            Assert.That(draw.Commands.Count(c => c.Kind == HudDrawKind.Image), Is.GreaterThanOrEqualTo(1), "market pictures");
        }

        [Test]
        public void AirlinePage_ListsYourRecentPaidFlights()
        {
            var (clock, ops, _) = HudTestAirline.Create();
            var definition = RouteContractCatalogue.RegionalKingscoteIntro;
            Assert.That(ops.AcceptContract(definition).Accepted, Is.True);
            HudTestAirline.CompleteActiveContract(clock, ops, definition);
            var model = new StatsWorkspaceModel();
            model.Rebuild(ops, clock.Now);
            Assert.That(model.RecentFlights, Is.Not.Empty);
            Assert.That(model.RecentFlights.Count, Is.LessThanOrEqualTo(StatsWorkspaceModel.MaxRecentFlights));
            Assert.That(model.RecentFlights[0].PaidText, Does.StartWith("$"));
            Assert.That(model.RecentFlights.Any(f => f.ContractDone), Is.True, "the flight that finished the contract");
        }
    }
}
