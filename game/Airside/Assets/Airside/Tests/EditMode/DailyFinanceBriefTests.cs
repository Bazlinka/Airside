using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class DailyFinanceBriefTests
    {
        [Test]
        public void BaselineAirport_ProjectsPositiveNetFromTurnaroundCadence()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(11), new ReservationTable());
            var brief = simulation.DailyFinance;

            var expectedCost = AirportSimulation.BaseDailyOperatingCost
                + Weather.DailyOperatingCost(simulation.CurrentWeather)
                + simulation.Staffing.DailyWage;
            var expectedIncome = AirportEconomy.TurnaroundRevenue
                * DayCycle.DaySeconds
                / AirportSimulation.CycleLengthSeconds;

            Assert.That(brief.ExpectedOperatingCost, Is.EqualTo(expectedCost));
            Assert.That(brief.ExpectedFlightIncome, Is.EqualTo(expectedIncome));
            Assert.That(brief.ExpectedNet, Is.EqualTo(expectedIncome - expectedCost));
            Assert.That(brief.ExpectedNet, Is.GreaterThan(0), "baseline ops should cash-flow positive before routes");
            Assert.That(brief.CashRunwayDays, Is.Null);
            Assert.That(brief.CashOnHand, Is.EqualTo(simulation.Economy.Cash));
        }

        [Test]
        public void AcceptedRouteIncome_RaisesExpectedFlightIncome()
        {
            var without = Project(acceptRoute: false);
            var with = Project(acceptRoute: true);

            Assert.That(with.ExpectedFlightIncome, Is.GreaterThan(without.ExpectedFlightIncome));
            Assert.That(with.ExpectedOperatingCost, Is.EqualTo(without.ExpectedOperatingCost));
        }

        [Test]
        public void NegativeNet_ReportsCashRunwayInWholeDays()
        {
            var brief = new DailyFinanceBrief(
                expectedOperatingCost: 5000,
                expectedFlightIncome: 1000,
                cashOnHand: 9000);

            Assert.That(brief.ExpectedNet, Is.EqualTo(-4000));
            Assert.That(brief.CashRunwayDays, Is.EqualTo(2));
        }

        [Test]
        public void DailyFinance_IsIdenticalAcrossLargeAndSmallSteps()
        {
            var smallClock = new ManualSimulationClock(new SimulationTime(0));
            var small = new AirportSimulation(smallClock, new SeededRandomSource(42), new ReservationTable());
            for (var i = 0; i < 400; i++)
            {
                smallClock.Advance(1);
                small.Update();
            }

            var largeClock = new ManualSimulationClock(new SimulationTime(0));
            var large = new AirportSimulation(largeClock, new SeededRandomSource(42), new ReservationTable());
            largeClock.Advance(400);
            large.Update();

            Assert.That(large.DailyFinance.ExpectedOperatingCost, Is.EqualTo(small.DailyFinance.ExpectedOperatingCost));
            Assert.That(large.DailyFinance.ExpectedFlightIncome, Is.EqualTo(small.DailyFinance.ExpectedFlightIncome));
            Assert.That(large.DailyFinance.CashOnHand, Is.EqualTo(small.DailyFinance.CashOnHand));
        }

        private static DailyFinanceBrief Project(bool acceptRoute)
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(99), new ReservationTable());
            var accepted = false;

            for (var second = 1; second <= 5000 && (!acceptRoute || !accepted); second++)
            {
                clock.Advance(1);
                simulation.Update();
                if (!acceptRoute || accepted || simulation.Routes.Pending == null)
                    continue;
                if (simulation.Reputation.Score < simulation.Routes.Pending.ReputationRequired)
                    continue;
                accepted = simulation.AcceptPendingRoute();
            }

            if (acceptRoute)
                Assert.That(accepted, Is.True);
            return simulation.DailyFinance;
        }
    }
}
