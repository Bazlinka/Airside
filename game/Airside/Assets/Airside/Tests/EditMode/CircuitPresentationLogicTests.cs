using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// Headless presentation-logic checks for the bare circuit polish pass.
    /// Kept out of PresentationLayoutTests so scripts/test-domain.sh can run them.
    /// </summary>
    public sealed class CircuitPresentationLogicTests
    {
        [SetUp]
        public void SetUp() => TaxiLoopFixture.RestoreCircuit();

        [Test]
        public void GearDoors_CloseWhenLockedUpOrDown_OpenInTransit()
        {
            Assert.That(AirsideReusableMotion.GearDoorOpenBias(AircraftPhase.Landing, 0.5f), Is.EqualTo(0f));
            Assert.That(AirsideReusableMotion.GearDoorOpenBias(AircraftPhase.Approach, 1f), Is.EqualTo(0f));
            Assert.That(AirsideReusableMotion.GearDoorOpenBias(AircraftPhase.Departed, 0.5f), Is.EqualTo(0f));

            var start = AirsideReusableMotion.GearRetractProgress;
            var mid = start + AirsideReusableMotion.GearTransitionProgress * 0.5f;
            Assert.That(AirsideReusableMotion.GearDoorOpenBias(AircraftPhase.Takeoff, mid),
                Is.GreaterThan(0.4f));
            Assert.That(AirsideReusableMotion.GearDoorOpenBias(AircraftPhase.Takeoff, mid),
                Is.LessThan(1.01f));
        }

        [Test]
        public void LandingFollow_KeepsLookAheadThroughRollout()
        {
            var early = AirsideCameraController.TestLookAheadMetres(AircraftPhase.Landing, 0.2f, 0f);
            var mid = AirsideCameraController.TestLookAheadMetres(AircraftPhase.Landing, 0.55f, 0f);
            var late = AirsideCameraController.TestLookAheadMetres(AircraftPhase.Landing, 1f, 0f);
            Assert.That(early, Is.EqualTo(32f).Within(0.05f));
            Assert.That(mid, Is.EqualTo(32f).Within(0.05f));
            Assert.That(late, Is.EqualTo(20f).Within(0.05f));
            Assert.That(late, Is.GreaterThan(12f));

            var earlyDist = AirsideCameraController.TestFollowDistance(AircraftPhase.Landing, 0f, 0.2f);
            var lateDist = AirsideCameraController.TestFollowDistance(AircraftPhase.Landing, 0f, 1f);
            Assert.That(earlyDist, Is.EqualTo(54f).Within(0.05f));
            Assert.That(lateDist, Is.EqualTo(42f).Within(0.05f));
            Assert.That(lateDist, Is.GreaterThan(36f));
        }
    }
}
