using System;
using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class FullCareerTests
    {
        private static (ManualSimulationClock Clock, AirlineOperations Operations) NewAirline()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var operations = new AirlineOperations(clock, new SeededRandomSource(91),
                DestinationCatalogue.Adelaide, AirlineOperations.AdelaideStands);
            var player = Airline.Player("Test Regional", "#245AA6");
            operations.AddAirline(player);
            operations.AddAircraft(player, "VH-TST", AircraftType.Saab340,
                AirlineOperations.AdelaideRegionalBays[0]);
            return (clock, operations);
        }

        [Test]
        public void Roadmap_PinSurvivesSaveAndOldTierSurvivesMigration()
        {
            var (clock, operations) = NewAirline();
            Assert.That(operations.PinCareerGoal("first-rotations").Accepted, Is.True);
            Assert.That(operations.PinCareerGoal("domestic-network").Accepted, Is.False);
            var saved = AirlineSave.Capture(operations);
            var restored = AirlineSave.Restore(saved, clock);
            Assert.That(restored.PinnedCareerGoal().Id, Is.EqualTo("first-rotations"));

            saved.Version = 12;
            saved.CareerTier = nameof(OperatingTier.Domestic);
            saved.CompletedContractIds.Add(RouteContractCatalogue.RegionalKingscoteIntro.Id);
            saved.PinnedCareerGoalId = null;
            saved.ServedDestinationCodes = null;
            var legacy = AirlineSave.Restore(saved, clock);
            Assert.That(legacy.CareerState.Tier, Is.EqualTo(OperatingTier.Domestic));
            Assert.That(legacy.CareerState.ServedDestinations, Does.Contain("KGC"));
            Assert.That(legacy.CareerState.Funds, Is.EqualTo(saved.CareerFunds));
        }

        [Test]
        public void FinalMilestone_RequiresFleetBasesNetworkQualityAndThirtyProfitableServices()
        {
            var codes = new[] { "KGC", "PLO", "MGB", "CED", "MEL", "SYD", "CBR", "BNE", "PER",
                "AKL", "SIN", "HKG" };
            var career = new AirlineCareerState(500_000, 90, OperatingTier.International,
                servedDestinations: codes, outstationBases: new[] { "MEL", "SYD" },
                recentServiceMargins: Enumerable.Repeat(100L, 30));
            var fleet = Enumerable.Repeat(AircraftType.Boeing7378, 17).Append(AircraftType.AirbusA350900).ToArray();
            Assert.That(CareerRoadmap.FinaleReady(career, fleet, fleet.Length), Is.True);
            var noWidebody = Enumerable.Repeat(AircraftType.Boeing7378, 18).ToArray();
            Assert.That(CareerRoadmap.FinaleReady(career, noWidebody, noWidebody.Length), Is.False,
                "the finale needs every International-stage goal, the widebody included");
            var loss = new AirlineCareerState(500_000, 90, OperatingTier.International,
                servedDestinations: codes, outstationBases: new[] { "MEL", "SYD" },
                recentServiceMargins: Enumerable.Repeat(-100L, 30));
            Assert.That(CareerRoadmap.FinaleReady(loss, fleet, fleet.Length), Is.False);
        }

        [Test]
        public void RouteForecast_PenalisesAnOversizedAircraftOnThinService()
        {
            Assert.That(DestinationCatalogue.TryFind("KGC", out var island), Is.True);
            var saab = RouteForecast.For(DestinationCatalogue.Adelaide, island, AircraftType.Saab340);
            var widebody = RouteForecast.For(DestinationCatalogue.Adelaide, island, AircraftType.AirbusA350900);
            Assert.That(saab.ExpectedPassengers, Is.EqualTo(25));
            Assert.That(saab.Seats, Is.LessThan(widebody.Seats));
            Assert.That(saab.Margin, Is.GreaterThan(widebody.Margin));
        }

        [Test]
        public void OutstationFlight_UsesSeparateCapacitySettlesOnceAndSurvivesSave()
        {
            var (clock, operations) = NewAirline();
            operations.RestoreCareerState(500_000, 95, nameof(OperatingTier.Domestic), null, 0, 0,
                Array.Empty<string>(), Array.Empty<string>(), 100,
                baseLevel: PlayerBaseLevel.JetGate, manualRotations: 12);
            Assert.That(operations.OpenOutstationBase("MEL").Accepted, Is.True);
            Assert.That(operations.BuyAircraftAtOutstation(AircraftType.Dash8Q400, "MEL").Accepted, Is.True);
            Assert.That(operations.PlayerFleetCount(), Is.EqualTo(2));
            Assert.That(operations.FleetOf(operations.PlayerAirline).Count(), Is.EqualTo(1),
                "outstation ownership never creates an Adelaide aircraft or stand reservation");
            var aircraft = operations.OutstationFleet.Single();
            Assert.That(operations.ScheduleOutstationService(aircraft.Registration, "SYD",
                new SimulationTime(1800)).Accepted, Is.True);
            var saved = AirlineSave.Capture(operations);
            var restored = AirlineSave.Restore(saved, clock);
            var returnAt = restored.OutstationFleet.Single().ReturnAtSeconds;
            clock.Set(new SimulationTime(returnAt));
            restored.Update();
            Assert.That(restored.OutstationFleet.Single().CompletedServices, Is.EqualTo(1));
            Assert.That(restored.CareerState.ServedDestinations, Does.Contain("SYD"));
            var funds = restored.CareerState.Funds;
            restored.Update();
            Assert.That(restored.CareerState.Funds, Is.EqualTo(funds));
            Assert.That(restored.CareerState.CompletedPlayerRotations, Is.EqualTo(101));
        }

        [Test]
        public void CompletedOfferWindow_StillProvidesAReachableRecoveryContract()
        {
            var (_, operations) = NewAirline();
            var completed = operations.MarketOffers().Select(o => o.Id).ToArray();
            operations.RestoreCareerState(200, 45, nameof(OperatingTier.Provisional), null, 0, 0,
                Array.Empty<string>(), completed, 0);
            var offers = operations.MarketOffers();
            Assert.That(offers.Any(o => o.Id.StartsWith("REC-", StringComparison.Ordinal)), Is.True);
            var recovery = offers.First(o => o.Id.StartsWith("REC-", StringComparison.Ordinal));
            Assert.That(recovery.EligibleType.Id, Is.EqualTo(AircraftType.Saab340.Id));
            Assert.That(operations.AcceptContract(recovery).Accepted, Is.True);
        }

        [Test]
        public void RecoveryContract_CanDispatchWithEmptyCashAndRepaysOnSettlement()
        {
            var (clock, operations) = NewAirline();
            operations.RestoreCareerState(0, 45, nameof(OperatingTier.Provisional), null, 0, 0,
                Array.Empty<string>(), Array.Empty<string>(), 0);
            var recovery = operations.MarketOffers().First(o => o.Id.StartsWith("REC-", StringComparison.Ordinal));
            Assert.That(operations.AcceptContract(recovery).Accepted, Is.True);
            var aircraft = operations.FleetOf(operations.PlayerAirline).Single();
            Assert.That(DestinationCatalogue.TryFind("KGC", out var destination), Is.True);
            Assert.That(operations.ScheduleDeparture(aircraft, destination,
                new SimulationTime(3600)).Accepted, Is.True);
            Assert.That(operations.CareerState.Funds, Is.LessThan(0));
            clock.Set(new SimulationTime(24 * 3600));
            operations.Update();
            Assert.That(operations.CareerState.CompletedPlayerRotations, Is.EqualTo(1));
            Assert.That(operations.CareerState.Funds, Is.GreaterThan(0));
        }

        [Test]
        public void BaseUpgrade_ImmediatelyEvaluatesTheNextTier()
        {
            var (_, operations) = NewAirline();
            operations.AddAircraft(operations.PlayerAirline, "VH-T02", AircraftType.Atr42,
                AirlineOperations.AdelaideRegionalBays[1]);
            operations.AddAircraft(operations.PlayerAirline, "VH-T03", AircraftType.Atr42,
                AirlineOperations.AdelaideRegionalBays[2]);
            operations.RestoreCareerState(20_000, 85, nameof(OperatingTier.Regional), null, 0, 0,
                Array.Empty<string>(), Array.Empty<string>(), 30,
                baseLevel: PlayerBaseLevel.Starter,
                servedDestinations: new[] { "KGC", "PLO", "MGB", "CED" });
            Assert.That(operations.UpgradePlayerBase().Accepted, Is.True);
            Assert.That(operations.CareerState.Tier, Is.EqualTo(OperatingTier.Domestic));
        }

        [Test]
        public void NetworkGrowth_ReachesTwentyFiveWithoutUsingAdelaideStands()
        {
            var (_, operations) = NewAirline();
            operations.RestoreCareerState(5_000_000, 100, nameof(OperatingTier.International), null, 0, 0,
                Array.Empty<string>(), Array.Empty<string>(), 100,
                baseLevel: PlayerBaseLevel.JetGate);
            foreach (var code in new[] { "MEL", "SYD", "BNE" })
            {
                Assert.That(operations.OpenOutstationBase(code).Accepted, Is.True);
                for (var i = 0; i < AirlineOperations.OutstationCapacity; i++)
                    Assert.That(operations.BuyAircraftAtOutstation(AircraftType.Dash8Q400, code).Accepted,
                        Is.True, code + " aircraft " + i);
            }
            Assert.That(operations.PlayerFleetCount(), Is.EqualTo(25));
            Assert.That(operations.FleetOf(operations.PlayerAirline).Count(), Is.EqualTo(1));
            Assert.That(operations.BuyAircraftAtOutstation(AircraftType.Dash8Q400, "MEL").Accepted,
                Is.False);
        }

        [Test]
        public void InvalidOutstationSave_RejectsDuplicateRegistrationAndImpossibleFlight()
        {
            var (clock, operations) = NewAirline();
            operations.RestoreCareerState(500_000, 95, nameof(OperatingTier.Domestic), null, 0, 0,
                Array.Empty<string>(), Array.Empty<string>(), 100,
                baseLevel: PlayerBaseLevel.JetGate);
            Assert.That(operations.OpenOutstationBase("MEL").Accepted, Is.True);
            Assert.That(operations.BuyAircraftAtOutstation(AircraftType.Dash8Q400, "MEL").Accepted, Is.True);
            var saved = AirlineSave.Capture(operations);
            saved.OutstationFleet[0].Registration = "VH-TST";
            Assert.Throws<FormatException>(() => AirlineSave.Restore(saved, clock));
            saved.OutstationFleet[0].Registration = "VH-O01";
            saved.OutstationFleet[0].DestinationCode = "ADL";
            saved.OutstationFleet[0].DepartAtSeconds = 100;
            saved.OutstationFleet[0].ReturnAtSeconds = 1000;
            Assert.Throws<FormatException>(() => AirlineSave.Restore(saved, clock));
        }

        [Test]
        public void InvalidCareerProof_CannotFabricateBasesOrDestinations()
        {
            var (clock, operations) = NewAirline();
            var saved = AirlineSave.Capture(operations);
            saved.OutstationBaseCodes.Add("ADL");
            Assert.Throws<FormatException>(() => AirlineSave.Restore(saved, clock));
            saved.OutstationBaseCodes.Clear();
            saved.ServedDestinationCodes.Add("FAKE");
            Assert.Throws<FormatException>(() => AirlineSave.Restore(saved, clock));
        }

        [Test]
        public void RepeatSchedule_DoesNotGenerateFlightsDuringAwayCatchUp()
        {
            var (clock, operations) = NewAirline();
            operations.RestoreCareerState(10_000, 95, nameof(OperatingTier.Regional), null, 0, 0,
                Array.Empty<string>(), Array.Empty<string>(), 12, manualRotations: 12);
            Assert.That(operations.SetRepeatSchedule("VH-TST", "KGC", 12).Accepted, Is.True);
            var saved = AirlineSave.Capture(operations);
            var restored = AirlineSave.Restore(saved, clock);
            clock.Set(new SimulationTime(24 * 3600));
            restored.Update();
            Assert.That(restored.FleetOf(restored.PlayerAirline).Single().Scheduled, Is.Null);
            Assert.That(restored.CareerState.Funds, Is.EqualTo(saved.CareerFunds));
            restored.ResumeRepeatSchedules();
            restored.Update();
            Assert.That(restored.FleetOf(restored.PlayerAirline).Single().Scheduled, Is.Not.Null);
        }
    }
}
