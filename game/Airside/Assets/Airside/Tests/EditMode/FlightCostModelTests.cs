using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// Economy v2 cost model (ADR 2026-10-09-economy-v2-real-dollar-scale). The first group pins sourced tier A arithmetic;
    /// the second pins behaviour (ordering, viability of leasing) without fixing the tier D design constants.
    /// </summary>
    public sealed class FlightCostModelTests
    {
        private static readonly AircraftType[] AllTypes =
        {
            AircraftType.Saab340, AircraftType.Atr42, AircraftType.Dash8Q400, AircraftType.Bell412,
            AircraftType.EmbraerE190, AircraftType.AirbusA220300, AircraftType.AirbusA320200, AircraftType.Boeing737800,
            AircraftType.Boeing7378, AircraftType.AirbusA321Neo, AircraftType.AirbusA330900, AircraftType.Boeing7879,
            AircraftType.Boeing78710, AircraftType.AirbusA350900
        };

        [Test]
        public void TerminalNavigation_MatchesAirservicesAdelaideRate()
        {
            Assert.That(FlightCostModel.TerminalNavigation(18.60), Is.EqualTo(237.7).Within(0.5));   // ATR 42-500
            Assert.That(FlightCostModel.TerminalNavigation(28.15), Is.EqualTo(359.8).Within(0.5));   // Dash 8-400
            Assert.That(FlightCostModel.TerminalNavigation(73.50), Is.EqualTo(939.3).Within(0.5));   // A320
            Assert.That(FlightCostModel.TerminalNavigation(229.55), Is.EqualTo(2933.6).Within(0.5)); // A330-300
            Assert.That(FlightCostModel.TerminalNavigation(0.5), Is.EqualTo(21.00), "minimum charge applies");
        }

        [Test]
        public void PassengerFees_MatchAdelaideSchedule()
        {
            Assert.That(FlightCostModel.PassengerFee(RouteBand.Regional), Is.EqualTo(6.13));
            Assert.That(FlightCostModel.PassengerFee(RouteBand.Domestic), Is.EqualTo(14.71));
            Assert.That(FlightCostModel.PassengerFee(RouteBand.LongHaul), Is.EqualTo(38.85));
            Assert.That(30 * FlightCostModel.PassengerFee(RouteBand.Regional), Is.EqualTo(183.9).Within(0.01));
        }

        [Test]
        public void Arff_AppliesOnlyFromTheWeightThreshold()
        {
            Assert.That(FlightCostModel.Arff(5.4), Is.EqualTo(0), "Bell 412 is below 5.7 t");
            Assert.That(FlightCostModel.Arff(13.2), Is.GreaterThan(0));
        }

        [Test]
        public void EnRoute_UsesWeightUnder20TonnesAndSquareRootAbove()
        {
            Assert.That(FlightCostModel.EnRoute(18.6, 300), Is.EqualTo(0.90 * 3 * 18.6).Within(0.001));
            Assert.That(FlightCostModel.EnRoute(73.5, 1000), Is.EqualTo(4.04 * 10 * System.Math.Sqrt(73.5)).Within(0.001));
        }

        [Test]
        public void EveryCatalogueTypeHasAProfile()
        {
            foreach (var type in AllTypes)
                Assert.That(FlightCostModel.ProfileFor(type).ValueAud, Is.GreaterThan(0), type.Id);
        }

        [Test]
        public void StarterSaab_ProfitsOnTheKingscoteHop()
        {
            Assert.That(DestinationCatalogue.TryFind("KGC", out var kgc), Is.True);
            var km = DestinationCatalogue.Adelaide.DistanceKmTo(kgc);
            var leg = FlightCostModel.Leg(AircraftType.Saab340, km, RouteBand.Regional);
            Assert.That(leg.Profit, Is.GreaterThan(0));
            Assert.That(leg.Margin, Is.InRange(0.05, 0.60), "a real regional margin, not a money printer or a loss");
        }

        [Test]
        public void CostsRiseWithFuelPriceAndDistance()
        {
            var cheap = FlightCostModel.Leg(AircraftType.Boeing737800, 650, RouteBand.Domestic, 0.60);
            var dear = FlightCostModel.Leg(AircraftType.Boeing737800, 650, RouteBand.Domestic, 1.20);
            Assert.That(dear.Fuel, Is.GreaterThan(cheap.Fuel * 1.9));
            Assert.That(dear.Revenue, Is.EqualTo(cheap.Revenue), "fuel never changes revenue");
            var longer = FlightCostModel.Leg(AircraftType.Boeing737800, 1100, RouteBand.Domestic);
            var shorter = FlightCostModel.Leg(AircraftType.Boeing737800, 650, RouteBand.Domestic);
            Assert.That(longer.Cost, Is.GreaterThan(shorter.Cost));
            Assert.That(longer.Revenue, Is.GreaterThan(shorter.Revenue));
        }

        [Test]
        public void LargerTypesCostMoreToRunButCarryMore()
        {
            var atr = FlightCostModel.Leg(AircraftType.Atr42, 300, RouteBand.Regional);
            var q400 = FlightCostModel.Leg(AircraftType.Dash8Q400, 300, RouteBand.Regional);
            Assert.That(q400.Cost, Is.GreaterThan(atr.Cost));
            Assert.That(q400.Revenue, Is.GreaterThan(atr.Revenue));
        }

        [Test]
        public void LeasingEveryTypeIsViableOnItsTypicalLegsButNotAMoneyPrinter()
        {
            foreach (var type in AllTypes)
            {
                var band = RouteAccess.Ceiling(type);
                var km = FlightEconomics.TypicalLegKm(type);
                var legs = FlightCostModel.LegsPerWorkingDay(type, km);
                var day = FlightCostModel.Leg(type, km, band).Profit * legs;
                var standing = FlightCostModel.StandingPerDay(type);
                Assert.That(day, Is.GreaterThan(standing), $"{type.Id} must cover its lease and insurance on a working day");
                // The starter Saab is deliberately generous (cheap lease, short legs) so the first hours work.
                Assert.That(day, Is.LessThan(standing * 25), $"{type.Id} must not repay a lease 25 times over in a day");
            }
        }

        [Test]
        public void OpeningCash_CoversTheFirstStepUpDeposit()
        {
            Assert.That(FlightCostModel.LeaseDeposit(AircraftType.Atr42), Is.LessThan(FlightCostModel.StartingCash));
            Assert.That(FlightCostModel.LeaseDeposit(AircraftType.AirbusA330900), Is.GreaterThan(FlightCostModel.StartingCash),
                "a widebody is out of reach on opening cash alone; tier and reliability gates hold jets back separately");
        }

        [Test]
        public void LoanCapsGrowWithTierAndInterestIsDaily()
        {
            var previous = 0L;
            foreach (var tier in new[] { OperatingTier.Provisional, OperatingTier.Regional, OperatingTier.Domestic, OperatingTier.International })
            {
                Assert.That(FlightCostModel.LoanCap(tier), Is.GreaterThan(previous));
                previous = FlightCostModel.LoanCap(tier);
            }
            Assert.That(FlightCostModel.LoanInterestPerDay(1_000_000), Is.EqualTo(1_000_000 * 0.085 / 365.0).Within(0.01));
            Assert.That(FlightCostModel.LoanInterestPerDay(-5), Is.EqualTo(0));
        }

        [Test]
        public void Leg_IsDeterministic()
        {
            var a = FlightCostModel.Leg(AircraftType.AirbusA330900, 3900, RouteBand.LongHaul);
            var b = FlightCostModel.Leg(AircraftType.AirbusA330900, 3900, RouteBand.LongHaul);
            Assert.That(a.Profit, Is.EqualTo(b.Profit));
            Assert.That(AllTypes.Select(t => FlightCostModel.LeasePerDay(t)).All(v => v > 0), Is.True);
        }
    }
}
