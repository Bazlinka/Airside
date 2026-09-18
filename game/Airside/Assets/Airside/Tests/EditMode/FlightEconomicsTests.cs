using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class FlightEconomicsTests
    {
        [Test]
        public void KingscoteRoundTrip_IsProfitableOnTheATR()
        {
            Assert.That(DestinationCatalogue.TryFind("KGC", out var kgc), Is.True);
            var km = DestinationCatalogue.Adelaide.DistanceKmTo(kgc);
            var cost = FlightEconomics.DispatchCost(AircraftType.Atr42, km);
            var pay = FlightEconomics.FlightPay(AircraftType.Atr42, km);
            Assert.That(cost, Is.GreaterThan(80));
            Assert.That(pay, Is.GreaterThan(cost), "a starter hop must not go broke on the operating loop alone");
            Assert.That(AirlineCareerState.StartingFunds, Is.GreaterThan(cost * 4),
                "opening float covers several hops before the first return");
        }

        [Test]
        public void TurbopropDispatch_IsCheaperThanAJetOnTheSameLeg()
        {
            const double km = 650;
            Assert.That(FlightEconomics.Weight(AircraftType.Atr42), Is.EqualTo(1.0));
            Assert.That(FlightEconomics.Weight(AircraftType.Boeing78710), Is.EqualTo(2.2));
            Assert.That(FlightEconomics.DispatchCost(AircraftType.Atr42, km),
                Is.LessThan(FlightEconomics.DispatchCost(AircraftType.Boeing78710, km)));
            Assert.That(FlightEconomics.FlightPay(AircraftType.Atr42, km),
                Is.LessThan(FlightEconomics.FlightPay(AircraftType.Boeing78710, km)));
            Assert.That(FlightEconomics.FlightPay(AircraftType.Atr42, km, RouteBand.Domestic),
                Is.GreaterThan(FlightEconomics.FlightPay(AircraftType.Atr42, km, RouteBand.Regional)));
        }

        [Test]
        public void DomesticBand_PaysMoreThanRegionalOnTheSameLeg()
        {
            const double km = 650;
            Assert.That(FlightEconomics.FlightPay(AircraftType.Dash8Q400, km, RouteBand.Domestic),
                Is.GreaterThan(FlightEconomics.FlightPay(AircraftType.Dash8Q400, km, RouteBand.Regional)));
            Assert.That(RouteAccess.PayMultiplier(RouteBand.LongHaul), Is.GreaterThan(RouteAccess.PayMultiplier(RouteBand.National)));
        }

        [Test]
        public void OpeningFloat_CoversAnATRMelbourneAndAJetMelbourneTogether()
        {
            Assert.That(DestinationCatalogue.TryFind("MEL", out var mel), Is.True);
            var km = DestinationCatalogue.Adelaide.DistanceKmTo(mel);
            var pair = FlightEconomics.DispatchCost(AircraftType.Atr42, km)
                       + FlightEconomics.DispatchCost(AircraftType.Boeing78710, km);
            Assert.That(FlightEconomics.StartingFunds, Is.GreaterThan(pair),
                "tests and a two-ship dispatch must not go broke on the opening float");
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
    }
}
