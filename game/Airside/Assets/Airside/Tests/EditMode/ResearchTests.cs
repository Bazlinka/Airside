using System;
using System.IO;
using Airside.Domain;
using Airside.Persistence;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class ResearchTests
    {
        [Test]
        public void StartOperationsResearch_CostsMoneyAndBeginsProgress()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(1), new ReservationTable());
            var cashBefore = simulation.Economy.Cash;

            Assert.That(simulation.StartOperationsResearch(), Is.True);
            Assert.That(simulation.Research.IsResearching, Is.True);
            Assert.That(simulation.Research.OperationsEfficiencyComplete, Is.False);
            Assert.That(simulation.Economy.Cash,
                Is.EqualTo(cashBefore - AirportResearch.OperationsEfficiencyCost));
            Assert.That(simulation.StartOperationsResearch(), Is.False, "cannot start twice");
        }

        [Test]
        public void OperationsResearch_CompletesAfterOneSimulatedDay()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(2), new ReservationTable());
            Assert.That(simulation.StartOperationsResearch(), Is.True);

            clock.Advance(AirportResearch.OperationsEfficiencyDurationSeconds - 1);
            simulation.Update();
            Assert.That(simulation.Research.OperationsEfficiencyComplete, Is.False);
            Assert.That(simulation.Research.IsResearching, Is.True);

            clock.Advance(1);
            simulation.Update();
            Assert.That(simulation.Research.OperationsEfficiencyComplete, Is.True);
            Assert.That(simulation.Research.IsResearching, Is.False);
            Assert.That(simulation.Research.DailyOperatingDiscount,
                Is.EqualTo(AirportResearch.OperationsEfficiencyDailyDiscount));
        }

        [Test]
        public void CompletedResearch_ReducesDailyOperatingCost()
        {
            var withResearchClock = new ManualSimulationClock(new SimulationTime(0));
            var withResearch = new AirportSimulation(withResearchClock, new SeededRandomSource(3), new ReservationTable());
            Assert.That(withResearch.StartOperationsResearch(), Is.True);
            withResearchClock.Advance(AirportResearch.OperationsEfficiencyDurationSeconds);
            withResearch.Update();
            Assert.That(withResearch.Research.OperationsEfficiencyComplete, Is.True);

            var controlClock = new ManualSimulationClock(new SimulationTime(0));
            var control = new AirportSimulation(controlClock, new SeededRandomSource(3), new ReservationTable());

            // Cross the first midnight after research is already complete for one save.
            withResearchClock.Advance(DayCycle.DaySeconds);
            withResearch.Update();
            controlClock.Advance(AirportResearch.OperationsEfficiencyDurationSeconds + DayCycle.DaySeconds);
            control.Update();

            Assert.That(withResearch.Economy.TotalOperatingCost,
                Is.EqualTo(control.Economy.TotalOperatingCost - AirportResearch.OperationsEfficiencyDailyDiscount));
        }

        [Test]
        public void ResearchProgress_IsIdenticalUnderLargeAndSmallSteps()
        {
            AirportSimulation Run(bool largeSteps)
            {
                var clock = new ManualSimulationClock(new SimulationTime(0));
                var simulation = new AirportSimulation(clock, new SeededRandomSource(11), new ReservationTable());
                simulation.StartOperationsResearch();
                var total = AirportResearch.OperationsEfficiencyDurationSeconds + 50;
                if (largeSteps)
                {
                    clock.Advance(total);
                    simulation.Update();
                }
                else
                {
                    for (var second = 1; second <= total; second++)
                    {
                        clock.Advance(1);
                        simulation.Update();
                    }
                }

                return simulation;
            }

            var small = Run(false);
            var large = Run(true);
            Assert.That(large.Research.OperationsEfficiencyComplete, Is.EqualTo(small.Research.OperationsEfficiencyComplete));
            Assert.That(large.Economy.Cash, Is.EqualTo(small.Economy.Cash));
            Assert.That(large.Economy.TotalOperatingCost, Is.EqualTo(small.Economy.TotalOperatingCost));
        }

        [Test]
        public void StartResearchCommand_SurvivesReloadAndCompletesOffline()
        {
            var directory = Path.Combine(Path.GetTempPath(), "airside-research-" + System.Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, "save.json");
            Directory.CreateDirectory(directory);
            try
            {
                var session = PersistentAirportSession.LoadOrCreate(path, 1_000_000, 42);
                session.AdvanceTo(1);
                Assert.That(session.StartOperationsResearch(), Is.True);
                session.Save(1_000_010);

                var restored = PersistentAirportSession.LoadOrCreate(
                    path,
                    1_000_010 + AirportResearch.OperationsEfficiencyDurationSeconds,
                    42);
                Assert.That(restored.Simulation.Research.OperationsEfficiencyComplete, Is.True);
                Assert.That(restored.Simulation.Research.DailyOperatingDiscount,
                    Is.EqualTo(AirportResearch.OperationsEfficiencyDailyDiscount));
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }
        [Test]
        public void PassengerServices_RequiresOperationsEfficiencyFirst()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(5), new ReservationTable());
            Assert.That(simulation.StartPassengerServicesResearch(), Is.False);
            Assert.That(simulation.StartOperationsResearch(), Is.True);
            clock.Advance(AirportResearch.OperationsEfficiencyDurationSeconds);
            simulation.Update();
            Assert.That(simulation.Research.OperationsEfficiencyComplete, Is.True);
            Assert.That(simulation.StartPassengerServicesResearch(), Is.True);
            Assert.That(simulation.Research.ActiveProjectId, Is.EqualTo(AirportResearch.PassengerServicesId));
        }

        [Test]
        public void PassengerServices_AddsRouteIncomeBonusAfterCompletion()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(6), new ReservationTable());
            Assert.That(simulation.StartOperationsResearch(), Is.True);
            clock.Advance(AirportResearch.OperationsEfficiencyDurationSeconds);
            simulation.Update();
            Assert.That(simulation.StartPassengerServicesResearch(), Is.True);
            clock.Advance(AirportResearch.PassengerServicesDurationSeconds);
            simulation.Update();
            Assert.That(simulation.Research.PassengerServicesComplete, Is.True);
            Assert.That(simulation.Research.RouteIncomeBonus,
                Is.EqualTo(AirportResearch.PassengerServicesRouteBonus));
            Assert.That(simulation.DailyFinance.ExpectedFlightIncome,
                Is.EqualTo(
                    (AirportEconomy.TurnaroundRevenue + simulation.Routes.IncomePerFlight + AirportResearch.PassengerServicesRouteBonus)
                    * DayCycle.DaySeconds / AirportSimulation.CycleLengthSeconds));
            Assert.That(simulation.DailyFinance.ExpectedOperatingCost,
                Is.EqualTo(
                    Math.Max(0, AirportSimulation.BaseDailyOperatingCost - AirportResearch.OperationsEfficiencyDailyDiscount)
                    + Weather.DailyOperatingCost(simulation.CurrentWeather)
                    + simulation.Staffing.DailyWage));
        }

        [Test]
        public void PassengerServicesCommand_SurvivesReload()
        {
            var path = Path.Combine(Path.GetTempPath(), $"airside-research-pax-{Guid.NewGuid():N}.json");
            try
            {
                var session = PersistentAirportSession.LoadOrCreate(path, 1_000_000, 42);
                session.AdvanceTo(1);
                Assert.That(session.StartOperationsResearch(), Is.True);
                session.Save(1_000_001);
                session = PersistentAirportSession.LoadOrCreate(
                    path,
                    1_000_001 + AirportResearch.OperationsEfficiencyDurationSeconds,
                    42);
                Assert.That(session.Simulation.Research.OperationsEfficiencyComplete, Is.True);
                Assert.That(session.StartPassengerServicesResearch(), Is.True);
                var afterOpsUnix = 1_000_001 + AirportResearch.OperationsEfficiencyDurationSeconds;
                session.Save(afterOpsUnix);
                var restored = PersistentAirportSession.LoadOrCreate(
                    path,
                    afterOpsUnix + AirportResearch.PassengerServicesDurationSeconds,
                    42);
                Assert.That(restored.Simulation.Research.PassengerServicesComplete, Is.True);
                Assert.That(restored.Simulation.Research.RouteIncomeBonus,
                    Is.EqualTo(AirportResearch.PassengerServicesRouteBonus));
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

    }
}
