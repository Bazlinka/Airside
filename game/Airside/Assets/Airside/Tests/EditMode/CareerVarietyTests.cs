using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>ADR 0127 — contract kinds and deadlines, demand events, the daily report and challenges.</summary>
    public sealed class CareerVarietyTests
    {
        private static AirlineOperations Start(out ManualSimulationClock clock)
        {
            clock = new ManualSimulationClock(new SimulationTime(0));
            return AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(5), Airline.Player("Variety Air", "#1F3A93"),
                firstFlightCoaching: false);
        }

        private static List<CareerEvent> Drain(AirlineOperations ops)
        {
            var list = new List<CareerEvent>();
            while (ops.TryTakeCareerEvent(out var e))
                list.Add(e);
            return list;
        }

        [Test]
        public void Market_OffersEveryKindWithTheirOwnTerms()
        {
            var types = new[] { AircraftType.Saab340, AircraftType.Atr42, AircraftType.Boeing7378 };
            var seen = new HashSet<ContractKind>();
            for (long window = 0; window < 200; window++)
            {
                foreach (var offer in ContractMarket.At(new SimulationTime(window * ContractMarket.WindowSeconds), types, 100,
                             OperatingTier.Domestic))
                {
                    seen.Add(offer.Kind);
                    Assert.That(offer.HasDeadline, Is.True, $"{offer.Id} market work is a commitment");
                    switch (offer.Kind)
                    {
                        case ContractKind.Charter:
                            Assert.That(offer.RequiredRotations, Is.EqualTo(1));
                            Assert.That(offer.DeadlineSeconds, Is.LessThanOrEqualTo(6 * 3600));
                            break;
                        case ContractKind.Medical:
                            Assert.That(RouteAccess.BandOf(offer.DestinationCode), Is.EqualTo(RouteBand.Regional));
                            Assert.That(AircraftCatalogue.For(offer.EligibleType).StandClass, Is.EqualTo(StandClass.RegionalBay));
                            Assert.That(offer.ReliabilityGainPerRotation, Is.GreaterThan(1));
                            break;
                        case ContractKind.Freight:
                            Assert.That(AircraftCatalogue.For(offer.EligibleType).StandClass, Is.EqualTo(StandClass.RegionalBay));
                            break;
                    }
                }
            }

            Assert.That(seen, Is.EquivalentTo(Enum.GetValues(typeof(ContractKind)).Cast<ContractKind>()));
        }

        [Test]
        public void ContractDeadline_LapsesAtTheExactMomentWhateverTheStepSize()
        {
            (long at, int reliability, bool active) Run(int stepSeconds)
            {
                var ops = Start(out var clock);
                var charter = new RouteContractDefinition("TEST-CH", "ADL", "KGC", AircraftType.Saab340, 3, 500, 800, 1,
                    OperatingTier.Provisional, reliabilityLossOnCancel: 4, kind: ContractKind.Charter, deadlineSeconds: 2 * 3600);
                Assert.That(ops.AcceptContract(charter).Accepted, Is.True);
                var due = ops.ContractExpiresAt().Value.ElapsedSeconds;
                long lapsedAt = -1;
                for (var t = 0L; t < 3 * 3600; t += stepSeconds)
                {
                    clock.Set(new SimulationTime(t));
                    ops.Update();
                    if (lapsedAt < 0 && ops.CareerState.ActiveContract == null)
                    {
                        lapsedAt = t;
                        Assert.That(Drain(ops).Any(e => e.Kind == CareerEventKind.ContractExpired), Is.True);
                    }
                }

                Assert.That(due, Is.EqualTo(2 * 3600));
                return (lapsedAt, ops.CareerState.Reliability, ops.CareerState.ActiveContract != null);
            }

            var fine = Run(1);
            var coarse = Run(1800);
            Assert.That(fine.active, Is.False);
            Assert.That(fine.reliability, Is.EqualTo(coarse.reliability), "same outcome for any step size");
            Assert.That(fine.reliability, Is.EqualTo(AirlineCareerState.StartingReliability - 4));
            Assert.That(fine.at, Is.EqualTo(2 * 3600));
        }

        [Test]
        public void Save_V15KeepsTheContractKindDeadlineAndStreak()
        {
            var ops = Start(out var clock);
            var freight = new RouteContractDefinition("MKT-9-0-KGC-SF34-FRT", "ADL", "KGC", AircraftType.Saab340, 2, 300, 400, 1,
                OperatingTier.Provisional, 2, kind: ContractKind.Freight, deadlineSeconds: 24 * 3600);
            Assert.That(ops.AcceptContract(freight).Accepted, Is.True);
            for (var i = 0; i < 7; i++)
                ops.CareerState.RecordPushback(true);
            var saved = AirlineSave.Capture(ops);
            Assert.That(saved.Version, Is.GreaterThanOrEqualTo(15));
            var restored = AirlineSave.Restore(saved, clock);
            Assert.That(restored.CareerState.TryFindDefinition(freight.Id, out var back), Is.True);
            Assert.That(back.Kind, Is.EqualTo(ContractKind.Freight));
            Assert.That(back.DeadlineSeconds, Is.EqualTo(24 * 3600));
            Assert.That(restored.CareerState.OnTimeStreak, Is.EqualTo(7));

            saved.Version = 14;
            var legacy = AirlineSave.Restore(saved, clock);
            Assert.That(legacy.CareerState.TryFindDefinition(freight.Id, out var old), Is.True);
            Assert.That(old.Kind, Is.EqualTo(ContractKind.Scheduled), "a v14 contract had no kind");
            Assert.That(old.HasDeadline, Is.False, "and no deadline");
            Assert.That(legacy.CareerState.OnTimeStreak, Is.EqualTo(0));
        }

        [Test]
        public void DemandEvents_AreDeterministicAndRaiseTheForecastWhereTheyApply()
        {
            var days = Enumerable.Range(0, 400).Select(d => DemandEvents.OnDay(d)).ToList();
            var share = days.Count(d => d.HasValue) / 400.0;
            Assert.That(share, Is.InRange(0.45, 0.65));
            Assert.That(DemandEvents.OnDay(123)?.Headline, Is.EqualTo(DemandEvents.OnDay(123)?.Headline));

            // Find a day that boosts Kingscote and check the live forecast (and so settlement) carries it.
            var ops = Start(out var clock);
            var kgc = DestinationCatalogue.All.First(d => d.Code == "KGC");
            var plain = RouteForecast.For(DestinationCatalogue.Adelaide, kgc, AircraftType.Saab340);
            for (var t = 0L; t < 200L * 86400; t += 86400)
            {
                if (DemandEvents.MultiplierFor("KGC", new SimulationTime(t), ops.Clock) <= 1.0)
                    continue;
                clock.Set(new SimulationTime(t));
                ops.Update();
                Assert.That(ops.Forecast(DestinationCatalogue.Adelaide, kgc, AircraftType.Saab340).Revenue,
                    Is.GreaterThan(plain.Revenue));
                Assert.That(ops.TodaysDemandEvent?.Affects("KGC"), Is.True);
                return;
            }

            Assert.Fail("no Kingscote demand day in 200 days");
        }

        [Test]
        public void OnTimeStreak_PaysItsChallengeOnce()
        {
            var ops = Start(out _);
            var advance = typeof(AirlineOperations).GetMethod("AdvanceCareer", BindingFlags.NonPublic | BindingFlags.Instance);
            Drain(ops);
            var funds = ops.CareerState.Funds;
            for (var i = 0; i < 10; i++)
                ops.CareerState.RecordPushback(true);
            advance.Invoke(ops, null);
            Assert.That(ops.CareerState.Funds, Is.EqualTo(funds + 1_500));
            Assert.That(ops.CareerState.HasAward("challenge:on-time-10"), Is.True);
            Assert.That(Drain(ops).Any(e => e.Kind == CareerEventKind.Challenge), Is.True);
            advance.Invoke(ops, null);
            Assert.That(ops.CareerState.Funds, Is.EqualTo(funds + 1_500), "paid once");
            ops.CareerState.RecordPushback(false);
            Assert.That(ops.CareerState.OnTimeStreak, Is.EqualTo(0), "a late pushback breaks the streak");
            Assert.That(ops.CareerChallengeStatus().Where(c => c.Challenge.Prestige).All(c => !c.Open),
                Is.True, "prestige challenges wait for the finale");
        }

        [Test]
        public void AFlownDay_EndsWithAReportAndTheDaysNews()
        {
            var ops = Start(out var clock);
            var bot = new CareerBot(ops, CareerPlayStyle.Competent, 1, new CareerRunResult());
            var events = new List<CareerEvent>();
            while (clock.Now.ElapsedSeconds < 3 * 86400)
            {
                bot.Tick(clock.Now);
                var now = clock.Now.ElapsedSeconds;
                var next = ops.NextEventAt()?.ElapsedSeconds ?? now + 60;
                clock.Set(new SimulationTime(Math.Max(now + 1, Math.Min(next, now + 60))));
                ops.Update();
                events.AddRange(Drain(ops));
            }

            var reports = events.Where(e => e.Kind == CareerEventKind.DailyReport).ToList();
            Assert.That(reports.Count, Is.InRange(2, 3), "one report per flown day");
            Assert.That(reports[0].Text, Does.StartWith("Today:"));
            Assert.That(events.Any(e => e.Kind == CareerEventKind.News)
                        || Enumerable.Range(0, 3).All(d => DemandEvents.At(new SimulationTime(d * 86400L), ops.Clock) == null),
                Is.True, "a demand day is announced");
        }
    }
}
