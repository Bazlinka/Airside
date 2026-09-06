using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class DailyReportTests
    {
        [Test]
        public void CrossingMidnight_PublishesADailyReport()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(1), new ReservationTable());

            Assert.That(simulation.DailyReports.Latest, Is.Null);

            clock.Advance(DayCycle.DaySeconds);
            simulation.Update();

            var report = simulation.DailyReports.Latest;
            Assert.That(report, Is.Not.Null);
            Assert.That(report.DayNumber, Is.EqualTo(1));
            Assert.That(report.OperatingCost, Is.GreaterThan(0));
            Assert.That(report.GroundCrew, Is.EqualTo(AirportStaffing.BaselineGroundCrew));
            Assert.That(report.SummaryLine, Does.Contain("Day 1"));
        }

        [Test]
        public void DailyReport_CountsFlightsAndCashDuringTheDay()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(42), new ReservationTable());

            // Run through the first midnight with whatever flights completed.
            clock.Advance(DayCycle.DaySeconds);
            simulation.Update();
            var first = simulation.DailyReports.Latest;
            Assert.That(first, Is.Not.Null);
            Assert.That(first.FlightsCompleted, Is.LessThanOrEqualTo(simulation.CompletedCycles));
            Assert.That(first.FlightIncome, Is.EqualTo(first.TurnaroundRevenue + first.RouteIncome));
            Assert.That(first.OperatingCost, Is.GreaterThan(0));
            Assert.That(first.NetCashChange,
                Is.EqualTo(first.FlightIncome - first.DelayCost - first.OperatingCost));
        }

        [Test]
        public void MultipleDays_KeepOnlyTheLatestSevenReports()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(7), new ReservationTable());

            clock.Advance(DayCycle.DaySeconds * 10);
            simulation.Update();

            Assert.That(simulation.DailyReports.All.Count, Is.EqualTo(AirportDailyReports.MaximumReports));
            Assert.That(simulation.DailyReports.Latest.DayNumber, Is.EqualTo(10));
            Assert.That(simulation.DailyReports.All.First().DayNumber, Is.EqualTo(10 - AirportDailyReports.MaximumReports + 1));
        }

        [Test]
        public void DailyReports_MatchUnderLargeAndSmallTimeSteps()
        {
            AirportSimulation Run(bool large)
            {
                var clock = new ManualSimulationClock(new SimulationTime(0));
                var simulation = new AirportSimulation(clock, new SeededRandomSource(99), new ReservationTable());
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
            Assert.That(large.DailyReports.All.Count, Is.EqualTo(small.DailyReports.All.Count));
            for (var i = 0; i < small.DailyReports.All.Count; i++)
            {
                var a = small.DailyReports.All[i];
                var b = large.DailyReports.All[i];
                Assert.That(b.DayNumber, Is.EqualTo(a.DayNumber));
                Assert.That(b.FlightsCompleted, Is.EqualTo(a.FlightsCompleted));
                Assert.That(b.OperatingCost, Is.EqualTo(a.OperatingCost));
                Assert.That(b.NetCashChange, Is.EqualTo(a.NetCashChange));
                Assert.That(b.ReputationChange, Is.EqualTo(a.ReputationChange));
                Assert.That(b.ClosingWeather, Is.EqualTo(a.ClosingWeather));
            }
        }
    }
}
