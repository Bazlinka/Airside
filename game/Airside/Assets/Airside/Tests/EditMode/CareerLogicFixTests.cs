using System;
using System.Collections.Generic;
using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// ADR 0121 — one coherent career. Each test pins a defect found in the September 2026 career
    /// audit: lost pay after resale, the contract soft-lock, tier skipping, three meanings of
    /// "international", stuck hints and silent tier-ups.
    /// </summary>
    public sealed class CareerLogicFixTests
    {
        private static (ManualSimulationClock Clock, AirlineOperations Operations) NewAirline()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var operations = new AirlineOperations(clock, new SeededRandomSource(91),
                DestinationCatalogue.Adelaide, AirlineOperations.AdelaideStands);
            var player = Airline.Player("Test Regional", "#245AA6");
            operations.AddAirline(player);
            operations.AddAircraft(player, "VH-PAX", AircraftType.Saab340,
                AirlineOperations.AdelaideRegionalBays[0]);
            return (clock, operations);
        }

        private static void Restore(AirlineOperations operations, long funds = 100_000, int reliability = 100,
            OperatingTier tier = OperatingTier.Provisional, IEnumerable<string> processed = null,
            IEnumerable<string> completed = null, int rotations = 0,
            PlayerBaseLevel baseLevel = PlayerBaseLevel.Starter, IEnumerable<string> served = null,
            IEnumerable<string> outstations = null) =>
            operations.RestoreCareerState(funds, reliability, tier.ToString(), null, 0, 0,
                processed ?? Array.Empty<string>(), completed ?? Array.Empty<string>(), rotations,
                baseLevel: baseLevel, servedDestinations: served, outstationBases: outstations);

        [Test]
        public void BoughtAircraft_NeverReusesARegistrationThatAlreadySettledFlights()
        {
            var (_, operations) = NewAirline();
            // VH-PAA flew two services, then was sold: its keys are spent.
            Restore(operations, rotations: 10, baseLevel: PlayerBaseLevel.ExpandedRegional,
                processed: new[] { "VH-PAA#1", "VH-PAA#2" });
            Assert.That(operations.BuyAircraft(AircraftType.Atr42).Accepted, Is.True);
            var registrations = operations.FleetOf(operations.PlayerAirline).Select(a => a.Registration).ToArray();
            Assert.That(registrations, Does.Not.Contain("VH-PAA"),
                "a reissued mark restarts at trip #1, whose settlement key is already spent, so it would never pay");
            Assert.That(registrations, Does.Contain("VH-PAB"));
        }

        [Test]
        public void ActiveContract_CanBeAbandonedForItsReliabilityCostAndAcceptedAgain()
        {
            var (_, operations) = NewAirline();
            var intro = RouteContractCatalogue.RegionalKingscoteIntro;
            Assert.That(operations.AcceptContract(intro).Accepted, Is.True);
            var before = operations.CareerState.Reliability;

            Assert.That(operations.AbandonContract().Accepted, Is.True);
            Assert.That(operations.CareerState.ActiveContract, Is.Null);
            Assert.That(operations.CareerState.Reliability, Is.EqualTo(before - intro.ReliabilityLossOnCancel));
            Assert.That(operations.AbandonContract().Accepted, Is.False, "nothing left to abandon");
            Assert.That(operations.AcceptContract(intro).Accepted, Is.True, "an abandoned contract is not completed");
        }

        [Test]
        public void BrokeWithAStuckContract_AbandonOpensTheRecoveryPath()
        {
            var (_, operations) = NewAirline();
            Assert.That(operations.AcceptContract(RouteContractCatalogue.RegionalKingscoteIntro).Accepted, Is.True);
            Restore(operations, funds: 0, reliability: 60);
            Assert.That(operations.AcceptContract(RouteContractCatalogue.RegionalKingscoteIntro).Accepted, Is.True);

            var recovery = operations.MarketOffers().First(o => o.Id.StartsWith("REC-", StringComparison.Ordinal));
            Assert.That(operations.AcceptContract(recovery).Accepted, Is.False, "one contract at a time");
            Assert.That(operations.AbandonContract().Accepted, Is.True);
            Assert.That(operations.AcceptContract(recovery).Accepted, Is.True);
            var aircraft = operations.FleetOf(operations.PlayerAirline).Single();
            Assert.That(operations.ScheduleDeparture(aircraft, HudTestAirline.Code("KGC"),
                new SimulationTime(3600)).Accepted, Is.True, "the recovery contract underwrites its dispatch");
        }

        [Test]
        public void Contract_NeedsItsAircraftTypeBasedAtAdelaide()
        {
            var (_, operations) = NewAirline();
            Restore(operations, tier: OperatingTier.Domestic);
            var melbourne = RouteContractCatalogue.DomesticMelbourneIntro;
            var accepted = operations.AcceptContract(melbourne);
            Assert.That(accepted.Accepted, Is.False);
            Assert.That(accepted.Reason, Does.Contain(melbourne.EligibleType.Name));
        }

        [Test]
        public void FeaturedCareerContracts_OnlyFeatureTypesTheAirlineFliesOrCanBuy()
        {
            var (_, operations) = NewAirline();
            Restore(operations, tier: OperatingTier.Provisional);
            foreach (var offer in operations.MarketOffers())
            {
                var owned = operations.PlayerOwnedTypes().Any(t => t.Id == offer.EligibleType.Id);
                var buyable = AircraftAcquisition.TryFor(offer.EligibleType, out var sale)
                              && sale.RequiredTier <= OperatingTier.Provisional;
                Assert.That(owned || buyable, Is.True, offer.Id);
            }
        }

        [Test]
        public void Tiers_AreEarnedInOrderAndCannotBeSkipped()
        {
            var fleet = new[] { AircraftType.Saab340, AircraftType.Atr42, AircraftType.Atr42 };
            // Every Regional-stage goal done while still Provisional — but no contract fulfilled.
            var career = new AirlineCareerState(50_000, 95, OperatingTier.Provisional,
                completedPlayerRotations: 40, baseLevel: PlayerBaseLevel.ExpandedRegional,
                servedDestinations: new[] { "KGC", "PLO", "WYA", "MGB" });
            career.EvaluateTier(fleet, fleet.Length);
            Assert.That(career.Tier, Is.EqualTo(OperatingTier.Provisional),
                "Domestic must not be reachable before Regional");

            var proved = new AirlineCareerState(50_000, 95, OperatingTier.Provisional,
                completedContractIds: new[] { RouteContractCatalogue.RegionalKingscoteIntro.Id },
                completedPlayerRotations: 40, baseLevel: PlayerBaseLevel.ExpandedRegional,
                servedDestinations: new[] { "KGC", "PLO", "WYA", "MGB" });
            Assert.That(proved.EvaluateTier(fleet, fleet.Length), Is.EqualTo(2));
            Assert.That(proved.Tier, Is.EqualTo(OperatingTier.Domestic), "then each tier is taken in turn");
        }

        [Test]
        public void CairnsAndDarwin_AreAustralianNotInternational()
        {
            Assert.That(RouteAccess.IsInternational("CNS"), Is.False);
            Assert.That(RouteAccess.IsInternational("DRW"), Is.False);
            Assert.That(RouteAccess.BandOf("CNS"), Is.EqualTo(RouteBand.National));
            Assert.That(RouteAccess.IsInternational("AKL"), Is.True);

            var career = new AirlineCareerState(tier: OperatingTier.International,
                servedDestinations: new[] { "CNS", "DRW", "AKL" });
            var goal = CareerRoadmap.Evaluate(career, new[] { AircraftType.Boeing78710 })
                .Single(g => g.Id == "international-network");
            Assert.That(goal.Progress, Is.EqualTo(1));
        }

        [Test]
        public void InternationalRoutes_NeedTheInternationalTierFromAdelaideAndInTheMarket()
        {
            var (_, operations) = NewAirline();
            Restore(operations, tier: OperatingTier.Domestic, rotations: 40, reliability: 95,
                baseLevel: PlayerBaseLevel.JetGate, funds: 500_000);
            Assert.That(operations.BuyAircraft(AircraftType.AirbusA321Neo).Accepted, Is.True);
            var jet = operations.FleetOf(operations.PlayerAirline).First(a => a.Type.Id == AircraftType.AirbusA321Neo.Id);
            if (jet.State == FleetState.AtStand)
            {
                var result = operations.ScheduleDeparture(jet, HudTestAirline.Code("AKL"), new SimulationTime(7200));
                Assert.That(result.Accepted, Is.False);
                Assert.That(result.Reason, Does.Contain("International"));
            }

            for (var window = 0; window < 40; window++)
            {
                var offers = ContractMarket.At(new SimulationTime(window * ContractMarket.WindowSeconds),
                    new[] { AircraftType.AirbusA321Neo, AircraftType.AirbusA350900 }, 95, OperatingTier.Domestic);
                foreach (var offer in offers)
                    Assert.That(RouteAccess.IsInternational(offer.DestinationCode), Is.False, offer.Id);
            }
        }

        [Test]
        public void GoalsCountTheWholeAirlineFleetAndOutstationGoalCountsOutstationsOnly()
        {
            var career = new AirlineCareerState(tier: OperatingTier.Domestic);
            var goals = CareerRoadmap.Evaluate(career, new[] { AircraftType.Saab340 }, fleetCount: 7);
            Assert.That(goals.Single(g => g.Id == "regional-fleet").Progress, Is.EqualTo(3));
            var outstation = goals.Single(g => g.Id == "domestic-base");
            Assert.That(outstation.Progress, Is.EqualTo(0));
            Assert.That(outstation.Target, Is.EqualTo(1), "Adelaide is not an outstation");
        }

        [Test]
        public void GoalsSayWhatTheirStageEarns()
        {
            var goals = CareerRoadmap.Evaluate(new AirlineCareerState(), new[] { AircraftType.Saab340 });
            Assert.That(goals.First(g => g.Stage == OperatingTier.Provisional).UnlocksLabel, Is.EqualTo("Regional"));
            Assert.That(goals.First(g => g.Stage == OperatingTier.International).UnlocksLabel,
                Is.EqualTo("Established airline"));
            Assert.That(goals.Where(g => g.Id.EndsWith("reliability", StringComparison.Ordinal))
                .All(g => g.Title.StartsWith("Hold reliability", StringComparison.Ordinal)), Is.True,
                "reliability starts at 100%, so the goal is to hold it, not to reach it");
        }

        [Test]
        public void CompletedPin_ReleasesToTheNextOpenGoal()
        {
            var career = new AirlineCareerState(completedPlayerRotations: 5);
            career.PinGoal("first-rotations");
            var pinned = CareerRoadmap.Pinned(career, new[] { AircraftType.Saab340 }, 1);
            Assert.That(pinned.Id, Is.Not.EqualTo("first-rotations"));
            Assert.That(pinned.Complete, Is.False);
        }

        [Test]
        public void StageProgress_CountsTheCurrentTiersGoals()
        {
            var career = new AirlineCareerState(completedPlayerRotations: 5);
            var stage = CareerRoadmap.StageProgress(career, new[] { AircraftType.Saab340 }, 1);
            Assert.That(stage.Stage, Is.EqualTo(OperatingTier.Provisional));
            Assert.That(stage.Total, Is.EqualTo(3));
            Assert.That(stage.Done, Is.EqualTo(2), "five services and held reliability; no contract yet");
            Assert.That(stage.TargetLabel, Is.EqualTo("Regional"));
        }

        [Test]
        public void NetworkMarginGoal_ShowsServicesCountedAndRunningMargin()
        {
            var career = new AirlineCareerState(tier: OperatingTier.International,
                recentServiceMargins: Enumerable.Repeat(-50L, 12));
            var goal = CareerRoadmap.Evaluate(career, new[] { AircraftType.Saab340 })
                .Single(g => g.Id == "established-margin");
            Assert.That(goal.Target, Is.EqualTo(CareerRoadmap.FinalMarginServices));
            Assert.That(goal.Progress, Is.EqualTo(12));
            Assert.That(goal.ProgressText, Does.Contain("−$600"));
        }

        [Test]
        public void Outstation_RefusesATypeWithNoRouteItCanFly()
        {
            var (_, operations) = NewAirline();
            Restore(operations, tier: OperatingTier.Domestic, rotations: 40, reliability: 95,
                baseLevel: PlayerBaseLevel.JetGate, funds: 500_000);
            Assert.That(operations.OpenOutstationBase("PER").Accepted, Is.True);
            Assert.That(operations.HasOutstationRoute(AircraftType.Atr42, "PER"), Is.False);
            var refused = operations.BuyAircraftAtOutstation(AircraftType.Atr42, "PER");
            Assert.That(refused.Accepted, Is.False);
            Assert.That(refused.Reason, Does.Contain("no route"));
            Assert.That(operations.BuyAircraftAtOutstation(AircraftType.Boeing7378, "PER").Accepted, Is.True);
        }

        [Test]
        public void TierUp_IsAnnouncedOnceAndLoadingAnnouncesNothing()
        {
            var (clock, operations) = NewAirline();
            Restore(operations, rotations: 5, completed: new[] { RouteContractCatalogue.RegionalKingscoteIntro.Id },
                baseLevel: PlayerBaseLevel.Starter, funds: 50_000);
            operations.Update();
            Assert.That(operations.TryTakeCareerEvent(out _), Is.False, "a restored state is not news");

            // Any command that re-evaluates the career promotes and announces.
            Assert.That(operations.UpgradePlayerBase().Accepted, Is.True);
            var events = new List<CareerEvent>();
            while (operations.TryTakeCareerEvent(out var e)) events.Add(e);
            Assert.That(operations.CareerState.Tier, Is.EqualTo(OperatingTier.Regional));
            Assert.That(events.Count(e => e.Kind == CareerEventKind.TierReached), Is.EqualTo(1));
            Assert.That(events.Single(e => e.Kind == CareerEventKind.TierReached).Tier, Is.EqualTo(OperatingTier.Regional));
            Assert.That(events.Any(e => e.Kind == CareerEventKind.GoalComplete && e.Text.Contains("Expand the Adelaide base")),
                Is.True);

            Assert.That(operations.UpgradePlayerBase().Accepted, Is.False);
            Assert.That(operations.TryTakeCareerEvent(out _), Is.False, "nothing is announced twice");
        }

        [Test]
        public void MigratedSave_CountsEarlierServicesTowardDelegation()
        {
            var (clock, operations) = NewAirline();
            Restore(operations, rotations: 20);
            var saved = AirlineSave.Capture(operations);
            saved.Version = 12;
            var legacy = AirlineSave.Restore(saved, clock);
            Assert.That(legacy.CareerState.ManualRotations, Is.EqualTo(20));
            Assert.That(legacy.DelegationUnlocked, Is.True);
        }
    }
}
