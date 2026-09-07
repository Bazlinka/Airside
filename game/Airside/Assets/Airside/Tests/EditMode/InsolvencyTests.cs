using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class InsolvencyTests
    {
        [Test]
        public void InsolvencyAtMidnight_DoesNotAdvanceTrafficAfterTheFinalReport()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(11), new ReservationTable());
            simulation.Economy.PayOperatingCosts(AirportEconomy.StartingCash + 80000);
            clock.Advance(3199);
            simulation.Update();
            // Arrange an approach whose landing clearance falls exactly on the third close.
            var flight = simulation.Flights[0];
            flight.Operation = new AircraftOperation(flight.AircraftId, new SimulationTime(3180));

            clock.Advance(1);
            simulation.Update();

            Assert.That(simulation.IsInsolvent, Is.True);
            Assert.That(flight.Operation.Phase, Is.EqualTo(AircraftPhase.Approach));
            Assert.That(simulation.EventLog.Events.Last().Title, Is.EqualTo("Insolvent"));
        }

        [Test]
        public void Economy_ThreeConsecutiveNegativeDayEnds_DeclaresInsolvency()
        {
            var economy = new AirportEconomy();
            economy.PayOperatingCosts(AirportEconomy.StartingCash + 1);
            Assert.That(economy.Cash, Is.LessThan(0));

            economy.EvaluateDayEndSolvency();
            Assert.That(economy.IsInsolvent, Is.False);
            Assert.That(economy.ConsecutiveNegativeDays, Is.EqualTo(1));

            economy.EvaluateDayEndSolvency();
            Assert.That(economy.IsInsolvent, Is.False);
            Assert.That(economy.ConsecutiveNegativeDays, Is.EqualTo(2));

            economy.EvaluateDayEndSolvency();
            Assert.That(economy.IsInsolvent, Is.True);
            Assert.That(economy.ConsecutiveNegativeDays, Is.EqualTo(AirportEconomy.InsolvencyConsecutiveDays));
        }

        [Test]
        public void Economy_APositiveDayEnd_ResetsTheConsecutiveCount()
        {
            var economy = new AirportEconomy();
            economy.PayOperatingCosts(AirportEconomy.StartingCash + 1);
            economy.EvaluateDayEndSolvency();
            economy.EvaluateDayEndSolvency();
            Assert.That(economy.ConsecutiveNegativeDays, Is.EqualTo(2));

            economy.AddRouteIncome(AirportEconomy.StartingCash + 1000);
            Assert.That(economy.Cash, Is.GreaterThanOrEqualTo(0));
            economy.EvaluateDayEndSolvency();
            Assert.That(economy.ConsecutiveNegativeDays, Is.Zero);
            Assert.That(economy.IsInsolvent, Is.False);

            economy.PayOperatingCosts(economy.Cash + 1);
            economy.EvaluateDayEndSolvency();
            Assert.That(economy.ConsecutiveNegativeDays, Is.EqualTo(1));
            Assert.That(economy.IsInsolvent, Is.False);
        }

        [Test]
        public void Simulation_ThreeNegativeDayCloses_EndsWithInsolventOutcome()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(7), new ReservationTable());

            // Drain far enough that three days of flight income cannot recover.
            simulation.Economy.PayOperatingCosts(AirportEconomy.StartingCash + 80000);
            Assert.That(simulation.IsInsolvent, Is.False);

            // First midnight is at t=800; the third day close is when DaysElapsed reaches 3 (t=3200).
            clock.Advance(3200);
            simulation.Update();

            Assert.That(simulation.IsInsolvent, Is.True);
            Assert.That(simulation.Economy.ConsecutiveNegativeDays,
                Is.EqualTo(AirportEconomy.InsolvencyConsecutiveDays));
            Assert.That(simulation.EventLog.Events.Any(e => e.Title == "Insolvent"), Is.True);
        }

        [Test]
        public void Simulation_StopsAdvancingOnceInsolvent()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(11), new ReservationTable());
            simulation.Economy.PayOperatingCosts(AirportEconomy.StartingCash + 80000);

            clock.Advance(3200);
            simulation.Update();
            Assert.That(simulation.IsInsolvent, Is.True);

            var cash = simulation.Economy.Cash;
            var operatingCost = simulation.Economy.TotalOperatingCost;
            var cycles = simulation.CompletedCycles;

            clock.Advance(DayCycle.DaySeconds * 2);
            simulation.Update();

            Assert.That(simulation.Economy.Cash, Is.EqualTo(cash));
            Assert.That(simulation.Economy.TotalOperatingCost, Is.EqualTo(operatingCost));
            Assert.That(simulation.CompletedCycles, Is.EqualTo(cycles));
            Assert.That(simulation.HireGroundCrew(), Is.False);
            Assert.That(simulation.AcceptPendingRoute(), Is.False);
        }

        [Test]
        public void Insolvency_IsIdenticalUnderLargeAndSmallTimeSteps()
        {
            AirportSimulation Run(bool largeSteps)
            {
                var clock = new ManualSimulationClock(new SimulationTime(0));
                var simulation = new AirportSimulation(clock, new SeededRandomSource(99), new ReservationTable());
                simulation.Economy.PayOperatingCosts(AirportEconomy.StartingCash + 80000);

                if (largeSteps)
                {
                    clock.Advance(4000);
                    simulation.Update();
                }
                else
                {
                    for (var second = 1; second <= 4000; second++)
                    {
                        clock.Advance(1);
                        simulation.Update();
                    }
                }

                return simulation;
            }

            var small = Run(false);
            var large = Run(true);

            Assert.That(large.IsInsolvent, Is.EqualTo(small.IsInsolvent));
            Assert.That(large.Economy.Cash, Is.EqualTo(small.Economy.Cash));
            Assert.That(large.Economy.ConsecutiveNegativeDays, Is.EqualTo(small.Economy.ConsecutiveNegativeDays));
            Assert.That(large.Economy.TotalOperatingCost, Is.EqualTo(small.Economy.TotalOperatingCost));
        }
    }
}
