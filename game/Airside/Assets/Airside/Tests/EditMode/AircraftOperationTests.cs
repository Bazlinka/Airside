using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class AircraftOperationTests
    {
        [Test]
        public void CompleteCycle_IsDeterministicAcrossLargeAndSmallTimeSteps()
        {
            var oneStep = new AircraftOperation("AS001", new SimulationTime(0));
            oneStep.AdvanceTo(new SimulationTime(154));

            var manySteps = new AircraftOperation("AS001", new SimulationTime(0));
            for (var second = 1; second <= 154; second++)
                manySteps.AdvanceTo(new SimulationTime(second));

            Assert.That(oneStep.Phase, Is.EqualTo(AircraftPhase.Departed));
            Assert.That(manySteps.Phase, Is.EqualTo(oneStep.Phase));
            Assert.That(manySteps.PhaseStartedAt, Is.EqualTo(oneStep.PhaseStartedAt));
        }

        [Test]
        public void AdvanceTo_RejectsTimeMovingBackwards()
        {
            var operation = new AircraftOperation("AS001", new SimulationTime(10));

            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => operation.AdvanceTo(new SimulationTime(9)));
        }
    }
}
