using System.Collections.Generic;
using System.Linq;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// The Contracts workspace (ADR 0057): the active commitment apart from the rotating
    /// market, with real progress, payments, penalties and refresh time.
    /// </summary>
    public sealed class ContractsWorkspaceTests
    {
        [Test]
        public void Contracts_KeepTheActiveCommitmentApartFromTheMarket()
        {
            var (clock, ops, _) = HudTestAirline.Create();
            Assert.That(ops.AcceptContract(RouteContractCatalogue.RegionalKingscoteIntro).Accepted, Is.True);

            var model = new ContractsWorkspaceModel();
            model.Rebuild(ops, clock.Now);

            Assert.That(model.HasActive, Is.True);
            Assert.That(model.ActiveTitle, Is.EqualTo("Prove the Kingscote service"));
            Assert.That(model.ActiveRoute, Is.EqualTo("Adelaide ↔ Kingscote"));
            Assert.That(model.ActiveProgressText, Is.EqualTo(
                $"0 of {RouteContractCatalogue.RegionalKingscoteIntro.RequiredRotations} rotations complete"));
            Assert.That(model.ActiveTerms, Does.Contain("Eligible: ATR 42-600"));
            Assert.That(model.ActiveTerms, Does.Contain(
                $"Cancellation: −{RouteContractCatalogue.RegionalKingscoteIntro.ReliabilityLossOnCancel} reliability"));
            Assert.That(model.EligibleAircraftLine, Is.EqualTo("VH-PAX"));
            Assert.That(model.Offers.All(o => !o.CanAccept), Is.True,
                "one contract at a time — every offer is explained, none is actionable");
            Assert.That(model.Offers.All(o => o.LockReason.Length > 0), Is.True);
        }

        [Test]
        public void Contracts_CountDownToTheRealMarketRefresh()
        {
            var (clock, ops, _) = HudTestAirline.Create();
            clock.Set(new SimulationTime(ContractMarket.WindowSeconds - 15 * 60));
            ops.Update();

            var model = new ContractsWorkspaceModel();
            model.Rebuild(ops, clock.Now);
            Assert.That(model.RefreshLine, Is.EqualTo("New offers in 15 min"));
        }

        [Test]
        public void Contracts_NeverShowACompletedContractAsActionable()
        {
            var (clock, ops, _) = HudTestAirline.Create();
            var model = new ContractsWorkspaceModel();
            model.Rebuild(ops, clock.Now);
            Assume.That(model.Offers, Is.Not.Empty, "this window should issue offers");

            var first = model.Offers[0].Definition;
            Assert.That(ops.AcceptContract(first).Accepted, Is.True);
            HudTestAirline.CompleteActiveContract(clock, ops, first);

            model.Rebuild(ops, clock.Now);
            Assert.That(model.Offers.Any(o => o.Definition.Id == first.Id), Is.False,
                "a fulfilled contract is not drawn at all, so it cannot be clicked");
            foreach (var offer in model.Offers)
                Assert.That(offer.CanAccept, Is.EqualTo(ops.CareerState.Tier >= offer.Definition.RequiredTier
                                                        && HudTestAirline.OwnsType(ops, offer.Definition.EligibleType)));
        }

        [Test]
        public void Contracts_OfferTotalsAddUpToWhatTheContractActuallyPays()
        {
            var (clock, ops, _) = HudTestAirline.Create();
            var model = new ContractsWorkspaceModel();
            model.Rebuild(ops, clock.Now);

            foreach (var offer in model.Offers)
            {
                var definition = offer.Definition;
                var total = definition.PaymentPerRotation * definition.RequiredRotations
                            + definition.CompletionReward;
                Assert.That(offer.Terms, Does.Contain($"${total:N0} total"));
                Assert.That(offer.Terms, Does.StartWith($"{definition.RequiredRotations} rotations"));
            }
        }

        [Test]
        public void Contracts_LayoutKeepsBothColumnsInsideTheSurface()
        {
            foreach (var (width, height) in HudTestAirline.Viewports)
            {
                var surface = HudShell.WorkspaceSurface(width, height);
                var layout = ContractsWorkspaceLayout.Create(surface);
                var label = $"{width}x{height}";

                Assert.That(layout.ActiveColumn.Overlaps(layout.OffersColumn), Is.False, label);
                Assert.That(layout.OffersColumn.Right, Is.LessThanOrEqualTo(surface.Right + 0.01f), label);
                Assert.That(layout.ActiveColumn.Overlaps(layout.Footer), Is.False, label);
                Assert.That(layout.OffersColumn.Overlaps(layout.Footer), Is.False, label);
                Assert.That(layout.VisibleOffers, Is.GreaterThanOrEqualTo(1), label);
            }
        }

    }
}
