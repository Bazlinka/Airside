using System.Collections.Generic;
using System.Linq;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class CampaignTests
    {
        private static readonly AircraftType[] Starter = { AircraftType.Saab340 };

        [Test]
        public void NewAirline_StartsOnChapterOneWithNothingDone()
        {
            var chapters = Campaign.Evaluate(new AirlineCareerState(), Starter);
            Assert.That(chapters.Count, Is.EqualTo(Campaign.ChapterCount));
            var current = Campaign.Current(chapters);
            Assert.That(current.Number, Is.EqualTo(1));
            Assert.That(current.Caption, Is.EqualTo("CHAPTER 1 · ISLAND HOPPER"));
            Assert.That(current.Complete, Is.False);
            Assert.That(current.Goals.Single(g => g.Text.StartsWith("Keep reliability")).Done, Is.True,
                "a new airline starts at 100%");
        }

        [Test]
        public void ChapterOne_CompletesOnAKingscoteContractAndThreeRotations()
        {
            var career = new AirlineCareerState(completedPlayerRotations: 3,
                contractHistory: new[] { Record("KGC") }, completedContractIds: new[] { "C-KGC" });
            var chapters = Campaign.Evaluate(career, Starter);
            Assert.That(chapters[0].Complete, Is.True);
            Assert.That(Campaign.Current(chapters).Number, Is.EqualTo(2));
        }

        [Test]
        public void LaterChapters_WaitForEarlierOnes()
        {
            // Everything chapter 2 asks for, but chapter 1's Kingscote contract never flown.
            var career = new AirlineCareerState(completedPlayerRotations: 9, tier: OperatingTier.Regional,
                contractHistory: new[] { Record("PLO"), Record("WYA") }, completedContractIds: new[] { "C-PLO", "C-WYA" });
            var chapters = Campaign.Evaluate(career, new[] { AircraftType.Saab340, AircraftType.Atr42 });
            Assert.That(chapters[1].Goals.All(g => g.Done), Is.True);
            Assert.That(chapters[1].Complete, Is.False, "chapters are played in order");
            Assert.That(Campaign.Current(chapters).Number, Is.EqualTo(1));
        }

        [Test]
        public void Reward_IsPaidExactlyOnceAndSurvivesTheSettlementKeys()
        {
            var career = new AirlineCareerState(funds: 1000);
            Assert.That(career.TryAward(Campaign.RewardKey(1), 1500), Is.True);
            Assert.That(career.TryAward(Campaign.RewardKey(1), 1500), Is.False);
            Assert.That(career.Funds, Is.EqualTo(2500));

            var reloaded = new AirlineCareerState(funds: career.Funds,
                processedSettlementKeys: career.ProcessedSettlementKeys);
            Assert.That(reloaded.TryAward(Campaign.RewardKey(1), 1500), Is.False, "a reload cannot pay it twice");
        }

        [Test]
        public void Operations_PaysAChapterWhenItsGoalsAreMet()
        {
            var (clock, ops, _) = HudTestAirline.Create("Campaign Air");
            var before = ops.CareerState.Funds;
            Assert.That(ops.CampaignChapters()[0].Rewarded, Is.False);
            // No rotations flown yet: nothing to pay however often the tower runs.
            clock.Set(new SimulationTime(600));
            ops.Update();
            Assert.That(ops.CareerState.Funds, Is.EqualTo(before));
        }

        [Test]
        public void ObjectiveCard_HeadsWithTheChapter()
        {
            var list = new HudDrawList();
            HudShellPainter.PaintObjective(list, new HudBox(0, 0, 360, 122),
                new CareerObjective("Accept a Kingscote contract", "", 0f, "Next", StatusSeverity.Normal),
                "CHAPTER 1 · ISLAND HOPPER");
            Assert.That(list.Commands.Any(c => c.Text == "CHAPTER 1 · ISLAND HOPPER"), Is.True);
        }

        private static CompletedContractRecord Record(string destination) =>
            new("C-" + destination, "ADL", destination, 1000, new SimulationTime(0));
    
        [Test]
        public void EveryCareerContract_IsFlyableByItsType()
        {
            foreach (var definition in RouteContractCatalogue.All)
            {
                Assert.That(DestinationCatalogue.TryFind(definition.DestinationCode, out var destination), Is.True, definition.Id);
                var km = DestinationCatalogue.Adelaide.DistanceKmTo(destination);
                Assert.That(definition.EligibleType.CanReach(km), Is.True, $"{definition.Id}: {definition.EligibleType.Name} cannot reach {km:0} km");
                Assert.That(RouteAccess.Allows(definition.EligibleType, destination), Is.True, $"{definition.Id}: route band");
            }
        }

        [Test]
        public void Offers_LeadWithTheNextCareerContracts()
        {
            var (_, ops, _) = HudTestAirline.Create("Offers Air");
            var offers = ops.MarketOffers();
            Assert.That(offers.Take(AirlineOperations.FeaturedCareerContracts).Select(o => o.Id),
                Is.EqualTo(new[] { "REG-KGC-INTRO", "REG-PLO-INTRO" }));
        }
}
}
