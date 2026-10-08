using System;
using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class ArrivalHoldingTrackTests
    {
        [TestCase(true, true, false, 0, true)]
        [TestCase(true, true, true, 32001, true)]
        [TestCase(true, true, true, 32000, false)]
        [TestCase(false, true, false, 0, false)]
        [TestCase(true, false, false, 0, false)]
        public void LostEstimateRetainsOnlyAnEstablishedDelayedArrival(bool established, bool inbound,
            bool hasEta, double metres, bool hold) =>
            Assert.That(ArrivalHoldingTrack.Required(established, inbound, hasEta, metres), Is.EqualTo(hold));

        [TestCase(1d, 0d)] [TestCase(0d, -1d)] [TestCase(-0.6d, 0.8d)]
        public void OrbitStartsAtActualPoseAndKeepsItsHeadingAndSpeed(double fx, double fz)
        {
            ArrivalHoldingTrack.Offset(0, 80, 400, fx, fz, out var x, out var y, out var z);
            Assert.That(x, Is.Zero); Assert.That(y, Is.Zero); Assert.That(z, Is.Zero);
            ArrivalHoldingTrack.Offset(0.01, 80, 400, fx, fz, out x, out y, out z);
            Assert.That((x * fx + z * fz) / 0.01, Is.EqualTo(80).Within(0.01));
            Assert.That(y / 0.01, Is.EqualTo(ArrivalHoldingTrack.ClimbMetresPerSecond).Within(0.001));
        }

        [TestCase(70d)] [TestCase(120d)]
        public void SeveralHoldingLapsKeepFlyingWithoutPositionOrAltitudeResets(double speed)
        {
            var px = 0d; var pz = 0d; var py = 0d;
            for (var second = 1; second <= 3600; second++)
            {
                ArrivalHoldingTrack.Offset(second, speed, 400, 1, 0, out var x, out var y, out var z);
                var distance = Math.Sqrt((x-px)*(x-px)+(z-pz)*(z-pz));
                Assert.That(distance, Is.InRange(speed * 0.999, speed * 1.001));
                Assert.That(y, Is.InRange(py, 1100d));
                Assert.That(y-py, Is.LessThanOrEqualTo(5d));
                px=x; pz=z; py=y;
            }
            Assert.That(py, Is.EqualTo(1100d));
        }
    }
}
