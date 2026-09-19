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
            var a = ApproachHold.HoldingFinalProgress("VH-HLD");
            var b = ApproachHold.HoldingFinalProgress("VH-HLE");
            Assert.That(a, Is.InRange(0.62, 0.74));
            Assert.That(b, Is.InRange(0.62, 0.74));
            Assert.That(ApproachHold.RemainingFinalSeconds(100, "VH-HLD"), Is.InRange(8, 40));
        }
    }
}
