using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class GeneralAviationTests
    {
        [Test]
        public void BaselineGeneralAviation_HasThreeMovementsPerDay()
        {
            var ga = new AirportGeneralAviation();
            Assert.That(ga.MovementsPerDay, Is.EqualTo(AirportGeneralAviation.BaselineMovementsPerDay));
            Assert.That(ga.DailyIncome,
                Is.EqualTo(AirportGeneralAviation.BaselineMovementsPerDay * AirportGeneralAviation.LandingFeePerMovement));
            Assert.That(ga.CanExpand, Is.True);
        }

        [Test]
        public void ExpandApron_CostsMoneyAndStaysWithinBounds()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(9), new ReservationTable());
            var cashBefore = simulation.Economy.Cash;

            Assert.That(simulation.ExpandGeneralAviationApron(), Is.True);
            Assert.That(simulation.GeneralAviation.MovementsPerDay,
                Is.EqualTo(AirportGeneralAviation.MaximumMovementsPerDay));
            Assert.That(simulation.Economy.Cash, Is.EqualTo(cashBefore - AirportGeneralAviation.ApronExpansionCost));
            Assert.That(simulation.ExpandGeneralAviationApron(), Is.False);
        }

        [Test]
        public void DailyIncome_IsSettledAtMidnightAndReportedSeparately()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(13), new ReservationTable());
            var cashBefore = simulation.Economy.Cash;

            clock.Advance(DayCycle.DaySeconds);
            simulation.Update();

            var report = simulation.DailyReports.Latest;
            Assert.That(report, Is.Not.Null);
            Assert.That(report.GeneralAviationIncome, Is.EqualTo(AirportGeneralAviation.BaselineMovementsPerDay
                * AirportGeneralAviation.LandingFeePerMovement));
            Assert.That(simulation.Economy.TotalGeneralAviationIncome, Is.EqualTo(report.GeneralAviationIncome));
            Assert.That(simulation.Economy.Cash, Is.GreaterThanOrEqualTo(cashBefore - AirportSimulation.BaseDailyOperatingCost
                - simulation.Staffing.DailyWage + report.GeneralAviationIncome));
        }

        [Test]
        public void GeneralAviationIncome_IsIdenticalAcrossLargeAndSmallTimeSteps()
        {
            AirportSimulation Run(bool large)
            {
                var clock = new ManualSimulationClock(new SimulationTime(0));
                var simulation = new AirportSimulation(clock, new SeededRandomSource(21), new ReservationTable());
                var total = DayCycle.DaySeconds * 3;
                if (large)
                {
                    clock.Advance(total);
                    simulation.Update();
                }
                else
                {
                    for (var s = 1; s <= total; s++)
                    {
                        clock.Advance(1);
                        simulation.Update();
                    }
                }

                return simulation;
            }

            var small = Run(false);
            var large = Run(true);
            Assert.That(large.Economy.TotalGeneralAviationIncome, Is.EqualTo(small.Economy.TotalGeneralAviationIncome));
        }
    }
}
