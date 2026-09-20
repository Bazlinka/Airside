using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class ApproachHoldTests
    {
        [Test]
        public void FleetCentrelineDrift_IsNotCompressedOnShortFinal()
        {
            Assert.That(ApproachHold.ApproachVisualProgress(0.95f, 0.14f), Is.EqualTo(0.95f).Within(1e-5));
            Assert.That(ApproachHold.ApproachVisualProgress(0.95f, -0.14f), Is.EqualTo(0.95f).Within(1e-5));
        }

        [Test]
        public void ParallelNumberTwo_CreepsInsteadOfStopping()
        {
            var compressed = ApproachHold.ApproachVisualProgress(0.95f, -4.5f);
            Assert.That(compressed, Is.GreaterThan(0.82f));
            Assert.That(compressed, Is.LessThan(0.85f));
        }

        [Test]
        public void HoldingFinal_StaysOnShortFinalNotTheCircuit()
        {
            Assert.That(ApproachHold.HoldingFinalProgress(0), Is.EqualTo(ApproachHold.FirstHoldProgress).Within(1e-5));
            Assert.That(ApproachHold.HoldingFinalProgress("VH-HLD"), Is.EqualTo(ApproachHold.FirstHoldProgress).Within(1e-5));
            Assert.That(ApproachHold.RemainingFinalSeconds(100, "VH-HLD"), Is.InRange(8, 40));
        }

        [Test]
        public void HoldingFinal_QueuesLaterArrivalsFurtherOut()
        {
            var first = ApproachHold.HoldingFinalProgress(0);
            var second = ApproachHold.HoldingFinalProgress(1);
            var third = ApproachHold.HoldingFinalProgress(2);
            Assert.That(first, Is.GreaterThan(second));
            Assert.That(second, Is.GreaterThan(third));
            Assert.That(third, Is.GreaterThanOrEqualTo(ApproachHold.LastHoldProgress));
            Assert.That(ApproachHold.RemainingFinalSeconds(100, 2),
                Is.GreaterThan(ApproachHold.RemainingFinalSeconds(100, 0)));
        }
    }
}
