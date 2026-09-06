using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class TurnaroundAndEconomyTests
    {
        [Test]
        public void CleaningDisruption_CreatesAnExplainableSixSecondDelay()
        {
            var workflow = new TurnaroundWorkflow(new SimulationTime(100), true);

            Assert.That(workflow.IsComplete(new SimulationTime(145)), Is.False);
            Assert.That(workflow.DelaySeconds(new SimulationTime(146)), Is.EqualTo(1));
            Assert.That(workflow.DelayCause(new SimulationTime(146)), Is.EqualTo("Cabin cleaning disruption"));
            Assert.That(workflow.IsComplete(new SimulationTime(151)), Is.True);
            Assert.That(workflow.DelaySeconds(new SimulationTime(151)), Is.EqualTo(6));
        }

        [Test]
        public void PriorityCrew_CompletesADisruptedTurnaroundInsideItsSchedule()
        {
            var workflow = new TurnaroundWorkflow(new SimulationTime(0), true);
            workflow.EnablePriorityCrew();

            Assert.That(workflow.IsComplete(new SimulationTime(37)), Is.True);
            Assert.That(workflow.DelaySeconds(new SimulationTime(37)), Is.Zero);
        }

        [Test]
        public void Economy_AppliesRevenueAndDelayCost()
        {
            var economy = new AirportEconomy();
            economy.CompleteFlight(6);

            Assert.That(economy.Cash, Is.EqualTo(AirportEconomy.StartingCash + 1200 - 240));
            Assert.That(economy.TotalRevenue, Is.EqualTo(1200));
            Assert.That(economy.TotalDelayCost, Is.EqualTo(240));
        }
    }
}
