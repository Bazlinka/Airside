using System;
using System.Collections.Generic;
using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>ADR 0138 — every contract on offer can be flown in time, and the challenges count fairly.</summary>
    public sealed class ContractFeasibilityTests
    {
        private static AirlineOperations Start(out ManualSimulationClock clock)
        {
            clock = new ManualSimulationClock(new SimulationTime(0));
            return AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(5), Airline.Player("Fair Air", "#1F3A93"),
                firstFlightCoaching: false);
        }

        private static bool IsTurboprop(AircraftType type) =>
            AircraftCatalogue.For(type).StandClass == StandClass.RegionalBay;

        [Test]
        public void EveryMarketOffer_CanBeFlownInTime_ForEveryTypeTierAndBase()
        {
            var types = AircraftAcquisition.All.Select(o => o.Type).ToList();
            var fleets = types.Select(t => (IReadOnlyList<AircraftType>)new[] { t }).ToList();
            fleets.Add(types);
            var checkedOffers = 0;
            foreach (var fleet in fleets)
            foreach (OperatingTier tier in Enum.GetValues(typeof(OperatingTier)))
            foreach (PlayerBaseLevel baseLevel in Enum.GetValues(typeof(PlayerBaseLevel)))
            foreach (var reliability in new[] { 50, 100 })
                for (long window = 0; window < 120; window++)
                {
                    var now = new SimulationTime(window * ContractMarket.WindowSeconds);
                    foreach (var offer in ContractMarket.At(now, fleet, reliability, tier, baseLevel))
                    {
                        checkedOffers++;
                        Assert.That(ContractFeasibility.IsFair(offer, baseLevel), Is.True,
                            $"{offer.Id} at {baseLevel}: deadline {offer.DeadlineSeconds / 3600.0:F1} h");
                        if (offer.Kind == ContractKind.Medical)
                        {
                            Assert.That(offer.DeadlineSeconds, Is.EqualTo(ContractMarket.MedicalDeadlineSeconds));
                            Assert.That(IsTurboprop(offer.EligibleType), Is.True, offer.Id);
                        }
                    }
                }

            Assert.That(checkedOffers, Is.GreaterThan(10_000));
        }

        [Test]
        public void ATurbopropOwner_IsOfferedAMedicalCallEveryDay()
        {
            foreach (var type in AircraftAcquisition.All.Select(o => o.Type).Where(IsTurboprop))
                for (long window = 0; window < 400; window += ContractMarket.MedicalEveryWindows)
                {
                    var offers = ContractMarket.At(new SimulationTime(window * ContractMarket.WindowSeconds),
                        new[] { type }, 100, OperatingTier.Regional, PlayerBaseLevel.Starter);
                    Assert.That(offers.Any(o => o.Kind == ContractKind.Medical), Is.True, $"{type.Name} window {window}");
                    Assert.That(offers.Select(o => o.Id).Distinct().Count(), Is.EqualTo(offers.Count));
                }
        }

        [Test]
        public void FeaturedContracts_OnlyForAircraftYouOwnOrCouldBuyNow()
        {
            var ops = Start(out _);
            foreach (var reliability in new[] { 40, 70, 100 })
            {
                ops.RestoreCareerState(1_000_000, reliability, nameof(OperatingTier.Regional), null, 0, 0,
                    Array.Empty<string>(), Array.Empty<string>(), 0);
                var owned = ops.Fleet.Where(a => a.Airline.IsPlayer).Select(a => a.Type.Id).ToHashSet();
                foreach (var offer in ops.MarketOffers())
                    Assert.That(owned.Contains(offer.EligibleType.Id) || ops.CouldBuyNow(offer.EligibleType), Is.True,
                        $"{offer.Id} needs a {offer.EligibleType.Name} the airline can't get at {reliability}%");
            }
        }

        [Test]
        public void Accept_RefusesWorkThatCanNoLongerBeFlownInTime()
        {
            var ops = Start(out _);
            var type = ops.Fleet.First(a => a.Airline.IsPlayer).Type;
            var tooTight = new RouteContractDefinition("TEST-TIGHT", "ADL", "KGC", type, 5, 500, 800, 1,
                OperatingTier.Provisional, kind: ContractKind.Charter, deadlineSeconds: 3 * 3600);
            var result = ops.AcceptContract(tooTight);
            Assert.That(result.Accepted, Is.False);
            Assert.That(result.Reason, Is.EqualTo("You can't fly this in time with your aircraft."));

            var fair = new RouteContractDefinition("TEST-FAIR", "ADL", "KGC", type, 1, 500, 800, 1,
                OperatingTier.Provisional, kind: ContractKind.Charter, deadlineSeconds: 6 * 3600);
            Assert.That(ops.AcceptContract(fair).Accepted, Is.True);
        }

        [Test]
        public void AFlightParkingExactlyOnTheDeadline_StillCounts()
        {
            long FlyOnce(long deadline, out AirlineOperations ops)
            {
                ops = Start(out var clock);
                var plane = ops.Fleet.First(a => a.Airline.IsPlayer);
                var charter = new RouteContractDefinition("TEST-TIE", "ADL", "KGC", plane.Type, 1, 500, 800, 1,
                    OperatingTier.Provisional, kind: ContractKind.Charter, deadlineSeconds: deadline);
                // Straight in, past the Accept check, so the deadline can be set to the second.
                ops.CareerState.Remember(charter);
                ops.CareerState.ActiveContract = new ActiveRouteContract(charter.Id, new SimulationTime(0));
                Assert.That(DestinationCatalogue.TryFind("KGC", out var kgc), Is.True);
                var lead = DeparturePrep.LeadSeconds(plane.Type, ops.CareerState.BaseLevel);
                Assert.That(ops.ScheduleDeparture(plane, kgc, new SimulationTime(lead)).Accepted, Is.True);
                for (var t = 0L; t < 8 * 3600 && ops.CareerState.ContractHistory.Count == 0; t += 30)
                {
                    clock.Set(new SimulationTime(t));
                    ops.Update();
                }

                return ops.CareerState.ContractHistory.Count == 0 ? -1 : ops.CareerState.ContractHistory[0].CompletedAt.ElapsedSeconds;
            }

            var parkedAt = FlyOnce(8 * 3600, out _);
            Assert.That(parkedAt, Is.GreaterThan(0), "the flight should finish well inside 8 hours");
            var tie = FlyOnce(parkedAt, out var tight);
            Assert.That(tie, Is.EqualTo(parkedAt), "a flight parking on the deadline completes the contract");
            Assert.That(tight.CareerState.CompletedContractIds, Does.Contain("TEST-TIE"));
        }

        [Test]
        public void TheReliabilityChallenge_IsAStreak()
        {
            var challenge = CareerChallenges.All.Single(c => c.Id == "reliability-95");
            var facts = new ChallengeFacts(0, false, 0, 0, true, 0, 1, 96, 500, highReliabilityStreak: 12);
            Assert.That(challenge.Progress(facts), Is.EqualTo((12, 300)),
                "500 flights ever, but only the last 12 were at 95%");
            var dipped = new ChallengeFacts(0, false, 0, 0, true, 0, 1, 94, 500, highReliabilityStreak: 0);
            Assert.That(challenge.Progress(dipped).Item1, Is.Zero);
        }

        [Test]
        public void ReliabilityHistory_CountsHeldFlightsAndCapsItsLength()
        {
            var career = new AirlineCareerState();
            career.RestoreReliabilityHistory(3, new[] { 70, 90, 96, 97, 95 });
            Assert.That(career.FlightsHeldAtOrAbove(95), Is.EqualTo(3));
            Assert.That(career.FlightsHeldAtOrAbove(90), Is.EqualTo(4));
            Assert.That(career.FlightsHeldAtOrAbove(98), Is.Zero);
            career.RestoreReliabilityHistory(0, Enumerable.Repeat(80, 50));
            Assert.That(career.RecentReliability.Count, Is.EqualTo(AirlineCareerState.ReliabilityHistoryLength));
        }

        [Test]
        public void TheDaySoFar_SurvivesASave()
        {
            var ops = Start(out var clock);
            var plane = ops.Fleet.First(a => a.Airline.IsPlayer);
            Assert.That(DestinationCatalogue.TryFind("KGC", out var kgc), Is.True);
            var lead = DeparturePrep.LeadSeconds(plane.Type, ops.CareerState.BaseLevel);
            Assert.That(ops.ScheduleDeparture(plane, kgc, new SimulationTime(lead)).Accepted, Is.True);
            for (var t = 0L; t < 5 * 3600 && ops.TodaySoFar.Flights == 0; t += 30)
            {
                clock.Set(new SimulationTime(t));
                ops.Update();
            }

            Assert.That(ops.TodaySoFar.Flights, Is.EqualTo(1));
            var data = AirlineSave.Capture(ops);
            Assert.That(data.Version, Is.EqualTo(17));
            var restored = AirlineSave.Restore(data, new ManualSimulationClock(clock.Now));
            Assert.That(restored.TodaySoFar, Is.EqualTo(ops.TodaySoFar));
            Assert.That(restored.CareerState.HighReliabilityStreak, Is.EqualTo(ops.CareerState.HighReliabilityStreak));
            Assert.That(restored.CareerState.RecentReliability, Is.EqualTo(ops.CareerState.RecentReliability));

            // A v16 save has none of this: a fresh day, and readings seeded from today's reliability
            // (ADR 0139) so goals it had met stay met.
            data.Version = 16;
            var old = AirlineSave.Restore(data, new ManualSimulationClock(clock.Now));
            Assert.That(old.TodaySoFar.Flights, Is.Zero);
            Assert.That(old.CareerState.RecentReliability,
                Is.EqualTo(Enumerable.Repeat(ops.CareerState.Reliability, ops.CareerState.CompletedPlayerRotations)));
        }
    }
}
