using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class StaffingTests
    {
        [Test]
        public void BaselineCrew_LeavesTurnaroundTimingUnchanged()
        {
            var baseline = new AirportStaffing();
            Assert.That(baseline.TurnaroundSpeedFactor, Is.EqualTo(1.0));

            var withFactor = new TurnaroundWorkflow(new SimulationTime(0), false, baseline.TurnaroundSpeedFactor);
            var without = new TurnaroundWorkflow(new SimulationTime(0), false);

            for (long t = 0; t <= 120; t++)
                Assert.That(withFactor.IsComplete(new SimulationTime(t)),
                    Is.EqualTo(without.IsComplete(new SimulationTime(t))), $"at t={t}");
        }

        [Test]
        public void Understaffing_StretchesTheTurnaround_ExtraCrewShortensIt()
        {
            var staffing = new AirportStaffing();
            staffing.Release();
            staffing.Release(); // 2 crew — understaffed
            Assert.That(staffing.IsUnderstaffed, Is.True);
            Assert.That(staffing.TurnaroundSpeedFactor, Is.GreaterThan(1.0));

            var slow = new TurnaroundWorkflow(new SimulationTime(0), false, staffing.TurnaroundSpeedFactor);
            var normal = new TurnaroundWorkflow(new SimulationTime(0), false);
            Assert.That(CompletionSecond(slow), Is.GreaterThan(CompletionSecond(normal)));

            var over = new AirportStaffing();
            over.Hire();
            over.Hire();
            Assert.That(over.TurnaroundSpeedFactor, Is.LessThan(1.0));
            var fast = new TurnaroundWorkflow(new SimulationTime(0), false, over.TurnaroundSpeedFactor);
            Assert.That(CompletionSecond(fast), Is.LessThan(CompletionSecond(normal)));
        }

        [Test]
        public void HireAndRelease_StayWithinBoundsAndCostMoney()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(1), new ReservationTable());
            var cashBefore = simulation.Economy.Cash;

            Assert.That(simulation.HireGroundCrew(), Is.True);
            Assert.That(simulation.Staffing.GroundCrew, Is.EqualTo(AirportStaffing.BaselineGroundCrew + 1));
            Assert.That(simulation.Economy.Cash, Is.EqualTo(cashBefore - AirportStaffing.HireCost));

            while (simulation.HireGroundCrew()) { }
            Assert.That(simulation.Staffing.GroundCrew, Is.EqualTo(AirportStaffing.MaximumGroundCrew));

            while (simulation.ReleaseGroundCrew()) { }
            Assert.That(simulation.Staffing.GroundCrew, Is.EqualTo(AirportStaffing.MinimumGroundCrew));
        }

        [Test]
        public void Payroll_IsChargedWithTheDailyRunningCosts()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(24031996), new ReservationTable());

            clock.Advance(DayCycle.DaySeconds * 2); // cross two midnights
            simulation.Update();

            Assert.That(simulation.Economy.TotalOperatingCost,
                Is.GreaterThanOrEqualTo(2 * (AirportSimulation.BaseDailyOperatingCost + simulation.Staffing.DailyWage)));
        }

        private static long CompletionSecond(TurnaroundWorkflow workflow)
        {
            for (long t = 0; t <= 400; t++)
                if (workflow.IsComplete(new SimulationTime(t)))
                    return t;
            return -1;
        }
    }
}
