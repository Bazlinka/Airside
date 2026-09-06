using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class TerminalTests
    {
        [Test]
        public void BaselineTerminal_MatchesTwoStandScheduleCap()
        {
            var terminal = new AirportTerminal();
            Assert.That(terminal.CheckInDesks, Is.EqualTo(AirportTerminal.BaselineCheckInDesks));
            Assert.That(terminal.PassengerCapacityPerDay,
                Is.EqualTo(AirportRoutes.MaxScheduledFlightsPerDay(AirportRoutes.BaselineStandCount)));
            Assert.That(terminal.HasExpandedCheckIn, Is.False);
            Assert.That(terminal.CanExpand, Is.True);
        }

        [Test]
        public void ExpandCheckIn_CostsMoneyAndRaisesCapacityToThreeStandLevel()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(3), new ReservationTable());
            var cashBefore = simulation.Economy.Cash;

            Assert.That(simulation.ExpandCheckInHall(), Is.True);
            Assert.That(simulation.Terminal.HasExpandedCheckIn, Is.True);
            Assert.That(simulation.Terminal.PassengerCapacityPerDay,
                Is.EqualTo(AirportRoutes.MaxScheduledFlightsPerDay(AirportCapacity.MaximumStands)));
            Assert.That(simulation.Economy.Cash, Is.EqualTo(cashBefore - AirportTerminal.CheckInHallExpansionCost));

            // Only one expansion exists in this first slice.
            Assert.That(simulation.ExpandCheckInHall(), Is.False);
        }

        [Test]
        public void ThirdStand_ExposesTerminalAsTheBindingConstraintUntilExpanded()
        {
            // Baseline terminal capacity (12/day) equals the baseline two-stand
            // schedule cap, so building a third stand (raising the stand-based
            // cap to 18/day) exposes the terminal as the binding constraint
            // until check-in is also expanded.
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(5), new ReservationTable());
            Assert.That(simulation.BuildThirdStand(), Is.True);

            Assert.That(simulation.MaxScheduledFlightsPerDay,
                Is.EqualTo(AirportTerminal.BaselineCheckInDesks * AirportTerminal.FlightsPerDeskPerDayCap));

            Assert.That(simulation.ExpandCheckInHall(), Is.True);
            Assert.That(simulation.MaxScheduledFlightsPerDay,
                Is.EqualTo(AirportRoutes.MaxScheduledFlightsPerDay(AirportCapacity.MaximumStands)));
        }
    }
}
