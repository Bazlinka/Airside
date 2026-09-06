using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class BaggageTests
    {
        [Test]
        public void BaselineBaggage_HasNoMishandlingCostAtOrUnderCapacity()
        {
            var baggage = new AirportBaggage();
            Assert.That(baggage.HandlingCapacityPerDay, Is.EqualTo(AirportBaggage.BaselineHandlingCapacityPerDay));
            Assert.That(baggage.MishandlingCostFor(0), Is.Zero);
            Assert.That(baggage.MishandlingCostFor(AirportBaggage.BaselineHandlingCapacityPerDay), Is.Zero);
        }

        [Test]
        public void MishandlingCost_ChargesOnlyTheExcessOverCapacity()
        {
            var baggage = new AirportBaggage();
            var over = AirportBaggage.BaselineHandlingCapacityPerDay + 3;
            Assert.That(baggage.MishandlingCostFor(over), Is.EqualTo(3 * AirportBaggage.MishandlingCostPerExcessFlight));
        }

        [Test]
        public void ExpandSortation_CostsMoneyAndStaysWithinBounds()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(41), new ReservationTable());
            var cashBefore = simulation.Economy.Cash;

            Assert.That(simulation.ExpandBaggageSortation(), Is.True);
            Assert.That(simulation.Baggage.HandlingCapacityPerDay,
                Is.EqualTo(AirportBaggage.BaselineHandlingCapacityPerDay + AirportBaggage.CapacityPerExpansion));
            Assert.That(simulation.Economy.Cash, Is.EqualTo(cashBefore - AirportBaggage.SortationExpansionCost));

            while (simulation.ExpandBaggageSortation()) { }
            Assert.That(simulation.Baggage.HandlingCapacityPerDay, Is.EqualTo(AirportBaggage.MaximumHandlingCapacityPerDay));
            Assert.That(simulation.ExpandBaggageSortation(), Is.False);
        }

        [Test]
        public void FullyBookedScheduleWithoutTerminalExpansion_NeverExceedsBaggageCapacity()
        {
            // The terminal caps scheduled flights at 12/day (AirportTerminal
            // baseline) unless expanded, matching baggage's own baseline
            // capacity exactly, even after a third stand raises the
            // stand-based cap to 18/day. So under ordinary play — accepting
            // every offer reputation allows, with a third stand built but the
            // terminal not expanded — the mishandling cost never triggers.
            // This is the same safety argument decision 0023 (terminal) and
            // this baggage foundation both rely on.
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(43), new ReservationTable());
            simulation.BuildThirdStand();

            for (var second = 1; second <= 20000; second++)
            {
                clock.Advance(1);
                simulation.Update();
                if (simulation.Routes.Pending == null)
                    continue;
                if (simulation.Reputation.Score < simulation.Routes.Pending.ReputationRequired)
                    continue;
                simulation.AcceptPendingRoute();
            }

            Assert.That(simulation.Routes.ScheduledFlightsPerDay, Is.GreaterThan(0));
            Assert.That(simulation.Routes.ScheduledFlightsPerDay,
                Is.LessThanOrEqualTo(AirportBaggage.BaselineHandlingCapacityPerDay));
            Assert.That(simulation.Baggage.MishandlingCostFor(simulation.Routes.ScheduledFlightsPerDay), Is.Zero);
        }
    }
}
