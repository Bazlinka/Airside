using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// GoAroundRejoin.Blend01 (ADR 0068): the eased 0..1 factor that smooths the cut from a
    /// go-around's racetrack back onto the pinned approach queue position.
    /// </summary>
    public sealed class GoAroundRejoinTests
    {
        [Test]
        public void StartsAtZeroAndEndsAtOne()
        {
            Assert.That(GoAroundRejoin.Blend01(0.0), Is.EqualTo(0.0));
            Assert.That(GoAroundRejoin.Blend01(GoAroundRejoin.BlendSeconds), Is.EqualTo(1.0));
        }

        [Test]
        public void ClampsOutsideTheWindow()
        {
            Assert.That(GoAroundRejoin.Blend01(-5.0), Is.EqualTo(0.0));
            Assert.That(GoAroundRejoin.Blend01(GoAroundRejoin.BlendSeconds + 30.0), Is.EqualTo(1.0));
        }

        [Test]
        public void IsMonotonicallyIncreasingThroughTheWindow()
        {
            var previous = -1.0;
            for (var t = 0.0; t <= GoAroundRejoin.BlendSeconds; t += 0.25)
            {
                var value = GoAroundRejoin.Blend01(t);
                Assert.That(value, Is.GreaterThanOrEqualTo(previous), $"at t={t}");
                previous = value;
            }
        }

        [Test]
        public void EasesRatherThanMovingLinearly()
        {
            // Smoothstep's derivative is 0 at both ends, so progress near the very start of the
            // window is slower than a straight line would give — the aircraft eases in, not cuts.
            var quarter = GoAroundRejoin.Blend01(GoAroundRejoin.BlendSeconds * 0.25);
            Assert.That(quarter, Is.LessThan(0.25));
        }
    }
}
