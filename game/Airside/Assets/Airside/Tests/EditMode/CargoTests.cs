using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class CargoTests
    {
        [Test]
        public void BaselineCargo_HasTwoContractsPerDay()
        {
            var cargo = new AirportCargo();
            Assert.That(cargo.ContractsPerDay, Is.EqualTo(AirportCargo.BaselineContractsPerDay));
            Assert.That(cargo.DailyIncome,
                Is.EqualTo(AirportCargo.BaselineContractsPerDay * AirportCargo.IncomePerContract));
            Assert.That(cargo.CanExpand, Is.True);
        }

        [Test]
        public void ExpandWarehouse_CostsMoneyAndStaysWithinBounds()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(17), new ReservationTable());
            var cashBefore = simulation.Economy.Cash;

            Assert.That(simulation.ExpandCargoWarehouse(), Is.True);
            Assert.That(simulation.Cargo.ContractsPerDay,
                Is.EqualTo(AirportCargo.BaselineContractsPerDay + AirportCargo.ContractsPerExpansion));
            Assert.That(simulation.Economy.Cash, Is.EqualTo(cashBefore - AirportCargo.WarehouseExpansionCost));

            while (simulation.ExpandCargoWarehouse()) { }
            Assert.That(simulation.Cargo.ContractsPerDay, Is.EqualTo(AirportCargo.MaximumContractsPerDay));
            Assert.That(simulation.ExpandCargoWarehouse(), Is.False);
        }

        [Test]
        public void DailyIncome_IsSettledAtMidnightAndFoldedIntoFlightIncome()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(23), new ReservationTable());

            clock.Advance(DayCycle.DaySeconds);
            simulation.Update();

            var report = simulation.DailyReports.Latest;
            Assert.That(report, Is.Not.Null);
            Assert.That(report.CargoIncome, Is.EqualTo(AirportCargo.BaselineContractsPerDay * AirportCargo.IncomePerContract));
            Assert.That(simulation.Economy.TotalCargoIncome, Is.EqualTo(report.CargoIncome));
            Assert.That(report.FlightIncome,
                Is.EqualTo(report.TurnaroundRevenue + report.RouteIncome + report.GeneralAviationIncome + report.CargoIncome));
            Assert.That(report.NetCashChange, Is.EqualTo(report.FlightIncome - report.DelayCost - report.OperatingCost));
        }
    }
}
