using System.Collections.Generic;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class DailyServiceTests
    {
        private static readonly AircraftType[] Starter = { AircraftType.Saab340 };
        private static readonly AirlineClock Clock = AirlineClock.Default;
        private static readonly SimulationTime Morning = new(0); // Mon 14 Sep 2026 08:00 Adelaide

        [Test]
        public void ChapterOne_FreshDayShowsZeroOfTwo()
        {
            var pattern = DailyService.Evaluate(new AirlineCareerState(), Starter, Clock, Morning);
            Assert.That(pattern.Active, Is.True);
            Assert.That(pattern.Chapter, Is.EqualTo(1));
            Assert.That(pattern.Required, Is.EqualTo(2));
            Assert.That(pattern.Done, Is.EqualTo(0));
            Assert.That(pattern.Line, Is.EqualTo("TODAY · Kingscote 0/2"));
            Assert.That(pattern.DateKey, Is.EqualTo("2026-09-14"));
        }

        [Test]
        public void ChapterOne_TwoKingscoteHopsPayBonusOnce()
        {
            // Start below the reliability cap so the day-pattern +1 is observable.
            var career = new AirlineCareerState(reliability: 90);
            var kingscote = Code("KGC");
            var melbourne = Code("MEL");

            Assert.That(DailyService.TryRecordAndAward(career, Starter, melbourne, Clock, Morning), Is.False,
                "Melbourne does not count on chapter 1");
            Assert.That(DailyService.TryRecordAndAward(career, Starter, kingscote, Clock, Morning), Is.True);
            Assert.That(DailyService.Evaluate(career, Starter, Clock, Morning).Done, Is.EqualTo(1));
            Assert.That(career.Funds, Is.EqualTo(AirlineCareerState.StartingFunds),
                "hop keys pay nothing until the pattern completes");

            var reliabilityBefore = career.Reliability;
            Assert.That(DailyService.TryRecordAndAward(career, Starter, kingscote, Clock, Morning), Is.True);
            var after = DailyService.Evaluate(career, Starter, Clock, Morning);
            Assert.That(after.Complete, Is.True);
            Assert.That(after.BonusPaid, Is.True);
            Assert.That(career.Funds, Is.EqualTo(AirlineCareerState.StartingFunds + after.Bonus));
            Assert.That(career.Reliability, Is.EqualTo(reliabilityBefore + DailyService.ReliabilityBonus));

            var fundsAfterBonus = career.Funds;
            Assert.That(DailyService.TryRecordAndAward(career, Starter, kingscote, Clock, Morning), Is.False,
                "a third hop the same day must not double-pay");
            Assert.That(career.Funds, Is.EqualTo(fundsAfterBonus));
        }

        [Test]
        public void ReloadSameLocalDay_StillShowsBonusPaid()
        {
            var keys = new[]
            {
                DailyService.HopKey("2026-09-14", 1, 1),
                DailyService.HopKey("2026-09-14", 1, 2),
                DailyService.BonusKey("2026-09-14", 1)
            };
            var career = new AirlineCareerState(processedSettlementKeys: keys);
            var pattern = DailyService.Evaluate(career, Starter, Clock, Morning);
            Assert.That(pattern.Done, Is.EqualTo(2));
            Assert.That(pattern.BonusPaid, Is.True);
            Assert.That(pattern.Complete, Is.True);
        }

        [Test]
        public void NextLocalDay_ResetsTheCount()
        {
            var keys = new[]
            {
                DailyService.HopKey("2026-09-14", 1, 1),
                DailyService.HopKey("2026-09-14", 1, 2),
                DailyService.BonusKey("2026-09-14", 1)
            };
            var career = new AirlineCareerState(processedSettlementKeys: keys);
            var tomorrow = Clock.AtLocal(new System.DateTime(2026, 9, 15, 8, 0, 0));
            var pattern = DailyService.Evaluate(career, Starter, Clock, tomorrow);
            Assert.That(pattern.DateKey, Is.EqualTo("2026-09-15"));
            Assert.That(pattern.Done, Is.EqualTo(0));
            Assert.That(pattern.BonusPaid, Is.False);
            Assert.That(pattern.Complete, Is.False);
        }

        [Test]
        public void ChapterTwo_CountsANewRegionalTown()
        {
            var career = ChapterTwoCareer();
            var pattern = DailyService.Evaluate(career, new[] { AircraftType.Saab340, AircraftType.Atr42 },
                Clock, Morning);
            Assert.That(pattern.Active, Is.True);
            Assert.That(pattern.Label, Is.EqualTo("new regional town"));
            Assert.That(pattern.Required, Is.EqualTo(1));

            Assert.That(DailyService.Qualifies(2, Code("KGC"), career), Is.False,
                "Kingscote is already fulfilled");
            Assert.That(DailyService.Qualifies(2, Code("PLO"), career), Is.True);
            Assert.That(DailyService.TryRecordAndAward(career, new[] { AircraftType.Saab340, AircraftType.Atr42 },
                Code("PLO"), Clock, Morning), Is.True);
            Assert.That(DailyService.Evaluate(career, new[] { AircraftType.Saab340, AircraftType.Atr42 },
                Clock, Morning).Complete, Is.True);
        }

        [Test]
        public void ChapterFour_InactiveUntilAJetIsOwned()
        {
            var career = ChapterFourCareer();
            // Saab + Dash 8 clears chapters 1–3; without a jet the day pattern stays off.
            var regionalFleet = new[] { AircraftType.Saab340, AircraftType.Dash8Q400 };
            Assert.That(Campaign.Current(Campaign.Evaluate(career, regionalFleet)).Number, Is.EqualTo(4));
            Assert.That(DailyService.Evaluate(career, regionalFleet, Clock, Morning).Active, Is.False);

            var withJet = new[] { AircraftType.Saab340, AircraftType.Dash8Q400, AircraftType.Boeing7378 };
            var pattern = DailyService.Evaluate(career, withJet, Clock, Morning);
            Assert.That(pattern.Active, Is.True);
            Assert.That(pattern.Label, Is.EqualTo("interstate"));
            Assert.That(DailyService.Qualifies(4, Code("MEL"), career), Is.True);
            Assert.That(DailyService.Qualifies(4, Code("PLO"), career), Is.False);
        }

        [Test]
        public void Objective_PrefacesProgressWithTodayLine()
        {
            var (_, ops, _) = HudTestAirline.Create("Daily Air");
            var objective = OperationsSummary.Objective(
                ops.FleetOf(ops.PlayerAirline), Morning, Clock, ops.CareerState, ops.MarketOffers());
            Assert.That(objective.ProgressText, Does.StartWith("TODAY · Kingscote 0/2"));
        }

        private static AirlineCareerState ChapterTwoCareer() =>
            new(completedPlayerRotations: 3, tier: OperatingTier.Provisional,
                contractHistory: new[] { Record("KGC") }, completedContractIds: new[] { "REG-KGC-INTRO" },
                baseLevel: PlayerBaseLevel.Starter);

        private static AirlineCareerState ChapterFourCareer() =>
            new(completedPlayerRotations: 20, tier: OperatingTier.Domestic, reliability: 85,
                contractHistory: new[]
                {
                    Record("KGC"), Record("PLO"), Record("WYA"), Record("MGB")
                },
                completedContractIds: new[] { "REG-KGC-INTRO", "REG-PLO-INTRO", "C-WYA", "C-MGB", "C-5", "C-6" },
                baseLevel: PlayerBaseLevel.JetGate);

        private static CompletedContractRecord Record(string destination) =>
            new("C-" + destination, DestinationCatalogue.Adelaide.Code, destination, 1_000, default);

        private static Destination Code(string code)
        {
            Assert.That(DestinationCatalogue.TryFind(code, out var destination), Is.True, code);
            return destination;
        }
    }
}
