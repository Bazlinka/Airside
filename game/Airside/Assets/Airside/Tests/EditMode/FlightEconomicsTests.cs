using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class FlightEconomicsTests
    {
        [Test]
        public void KingscoteRoundTrip_IsProfitableOnTheStarterSaab()
        {
            Assert.That(DestinationCatalogue.TryFind("KGC", out var kgc), Is.True);
            var km = DestinationCatalogue.Adelaide.DistanceKmTo(kgc);
            var cost = FlightEconomics.DispatchCost(AircraftType.Saab340, km);
            var pay = FlightEconomics.FlightPay(AircraftType.Saab340, km);
            Assert.That(cost, Is.GreaterThan(1_000), "real dollars: a Kingscote rotation costs thousands");
            Assert.That(pay, Is.GreaterThan(cost), "a starter hop must not go broke on the operating loop alone");
            Assert.That(AirlineCareerState.StartingFunds, Is.GreaterThan(cost * 4),
                "opening float covers several hops before the first return");
            Assert.That(AirlineCareerState.StartingFunds, Is.GreaterThan(AircraftAcquisition.Atr42.Price),
                "opening cash covers the first step-up aircraft's lease deposit");
        }

        [Test]
        public void TurbopropDispatch_IsCheaperThanAJetOnTheSameLeg()
        {
            const double km = 650;
            Assert.That(FlightEconomics.DispatchCost(AircraftType.Atr42, km),
                Is.LessThan(FlightEconomics.DispatchCost(AircraftType.Boeing78710, km)));
            Assert.That(FlightEconomics.FlightPay(AircraftType.Atr42, km),
                Is.LessThan(FlightEconomics.FlightPay(AircraftType.Boeing78710, km)));
        }

        [Test]
        public void LongerBands_PayMoreForTheSameAircraft()
        {
            Assert.That(DestinationCatalogue.TryFind("PLO", out var plo), Is.True);
            Assert.That(DestinationCatalogue.TryFind("MEL", out var mel), Is.True);
            Assert.That(DestinationCatalogue.TryFind("PER", out var per), Is.True);
            long Pay(Destination d) => FlightEconomics.FlightPay(AircraftType.Boeing737800,
                DestinationCatalogue.Adelaide.DistanceKmTo(d), RouteAccess.BandOf(d));
            Assert.That(Pay(mel), Is.GreaterThan(Pay(plo)), "Melbourne pays more than Port Lincoln");
            Assert.That(Pay(per), Is.GreaterThan(Pay(mel)), "Perth pays more than Melbourne");
        }

        [Test]
        public void OpeningFloat_CoversSeveralSaabHopsAndTheFirstStepUpButNotAWidebody()
        {
            Assert.That(DestinationCatalogue.TryFind("KGC", out var kgc), Is.True);
            var kgcKm = DestinationCatalogue.Adelaide.DistanceKmTo(kgc);
            var saabHop = FlightEconomics.DispatchCost(AircraftType.Saab340, kgcKm);
            Assert.That(FlightEconomics.StartingFunds, Is.GreaterThan(saabHop * 4),
                "opening float still covers a short bank of starter hops");
            Assert.That(FlightEconomics.StartingFunds, Is.GreaterThan(AircraftAcquisition.Atr42.Price),
                "the opening cash covers the ATR 42 lease deposit");
            Assert.That(FlightEconomics.StartingFunds, Is.LessThan(AircraftAcquisition.AirbusA330900.Price),
                "but not a widebody deposit: the player has to fly to earn each step up");
        }

        [Test]
        public void LongerHops_CostAndPayMore()
        {
            Assert.That(DestinationCatalogue.TryFind("KGC", out var kgc), Is.True);
            Assert.That(DestinationCatalogue.TryFind("PLO", out var plo), Is.True);
            var shortKm = DestinationCatalogue.Adelaide.DistanceKmTo(kgc);
            var longKm = DestinationCatalogue.Adelaide.DistanceKmTo(plo);
            Assert.That(longKm, Is.GreaterThan(shortKm));
            Assert.That(FlightEconomics.DispatchCost(AircraftType.Atr42, longKm),
                Is.GreaterThan(FlightEconomics.DispatchCost(AircraftType.Atr42, shortKm)));
            Assert.That(FlightEconomics.FlightPay(AircraftType.Atr42, longKm),
                Is.GreaterThan(FlightEconomics.FlightPay(AircraftType.Atr42, shortKm)));
        }

        [Test]
        public void ReliabilityMultiplier_IsNeutralAtAndAboveTheDomesticFloorOnlyPenalisesBelowIt()
        {
            Assert.That(FlightEconomics.ReliabilityMultiplier(100), Is.EqualTo(1.0),
                "a fresh career (100) must be completely unaffected");
            Assert.That(FlightEconomics.ReliabilityMultiplier(ContractMarket.DomesticReliabilityFloor),
                Is.EqualTo(1.0), "neutral right down to the same floor the contract market already uses");
            Assert.That(FlightEconomics.ReliabilityMultiplier(ContractMarket.DomesticReliabilityFloor - 1),
                Is.LessThan(1.0), "one point under the floor already costs something");
            Assert.That(FlightEconomics.ReliabilityMultiplier(49), Is.LessThan(FlightEconomics.ReliabilityMultiplier(50)),
                "the penalty deepens the worse reliability gets");
            Assert.That(FlightEconomics.ReliabilityMultiplier(0), Is.GreaterThan(0),
                "even a ruined reputation still pays something for the flight");
        }

        [Test]
        public void PunctualityReliabilityDelta_RewardsOnTimeAndPenalisesLatePushback()
        {
            Assert.That(FlightEconomics.PunctualityReliabilityDelta(0), Is.EqualTo(1));
            Assert.That(FlightEconomics.PunctualityReliabilityDelta(FlightEconomics.OnTimeGraceSeconds), Is.EqualTo(1));
            Assert.That(FlightEconomics.PunctualityReliabilityDelta(FlightEconomics.OnTimeGraceSeconds + 1), Is.EqualTo(0));
            Assert.That(FlightEconomics.PunctualityReliabilityDelta(FlightEconomics.SoftLateSeconds), Is.EqualTo(0));
            Assert.That(FlightEconomics.PunctualityReliabilityDelta(FlightEconomics.SoftLateSeconds + 1), Is.EqualTo(-1));
            Assert.That(FlightEconomics.PunctualityReliabilityDelta(FlightEconomics.HardLateSeconds), Is.EqualTo(-1));
            Assert.That(FlightEconomics.PunctualityReliabilityDelta(FlightEconomics.HardLateSeconds + 1), Is.EqualTo(-2));
        }
    }
}
