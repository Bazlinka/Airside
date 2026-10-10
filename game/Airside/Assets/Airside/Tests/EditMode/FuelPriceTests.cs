using System;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>Economy v2 seeded fuel price walk: pure function of the day, wider on Demanding.</summary>
    public sealed class FuelPriceTests
    {
        [Test]
        public void PriceIsDeterministicBoundedAndMoves()
        {
            double min = double.MaxValue, max = double.MinValue;
            for (long day = -20; day < 800; day++)
            {
                var price = FuelPrice.Ratio(day, CareerDifficulty.Standard);
                Assert.That(price, Is.EqualTo(FuelPrice.Ratio(day, CareerDifficulty.Standard)));
                Assert.That(price, Is.InRange(0.85, 1.15));
                min = Math.Min(min, price);
                max = Math.Max(max, price);
            }

            Assert.That(max - min, Is.GreaterThan(0.1), "the price actually walks");
            Assert.That(Math.Abs(FuelPrice.Ratio(101, CareerDifficulty.Standard) - FuelPrice.Ratio(100, CareerDifficulty.Standard)),
                Is.LessThan(0.05), "and walks smoothly day to day");
        }

        [Test]
        public void DemandingSwingsWiderAroundTheSameWalk()
        {
            var wider = false;
            for (long day = 0; day < 400; day++)
            {
                var standard = FuelPrice.Ratio(day, CareerDifficulty.Standard) - 1;
                var hard = FuelPrice.Ratio(day, CareerDifficulty.Demanding) - 1;
                Assert.That(Math.Abs(hard), Is.LessThanOrEqualTo(0.40 + 1e-9));
                if (Math.Abs(hard) > 0.2) wider = true;
                Assert.That(Math.Sign(hard), Is.EqualTo(Math.Sign(standard)));
            }

            Assert.That(wider, Is.True);
        }

        [Test]
        public void CancellingARebookedFlightRefundsExactlyWhatWasPaidOnAnyDay()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(7), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideRegionalBays);
            var player = Airline.Player("Fuel Air", "#245AA6");
            ops.AddAirline(player);
            var aircraft = ops.AddAircraft(player, "VH-FUE", AircraftType.Saab340, AirlineOperations.AdelaideRegionalBays[0]);
            Assert.That(DestinationCatalogue.TryFind("KGC", out var destination), Is.True);
            var funds = ops.CareerState.Funds;
            var departAt = new SimulationTime(30 * 24 * 3600L + 3600);

            Assert.That(ops.ScheduleDeparture(aircraft, destination, departAt).Accepted, Is.True);
            Assert.That(ops.CareerState.Funds, Is.LessThan(funds));
            Assert.That(ops.CancelDeparture(aircraft).Accepted, Is.True);
            Assert.That(ops.CareerState.Funds, Is.EqualTo(funds), "paid at the departure day's fuel price, refunded at the same");
        }
    }
}
