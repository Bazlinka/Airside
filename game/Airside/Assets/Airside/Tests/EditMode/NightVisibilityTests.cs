using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class NightVisibilityTests
    {
        [Test]
        public void DaylightIsTheSameAtEveryLevel()
        {
            for (var level = 0; level < NightVisibility.Labels.Length; level++)
            {
                Assert.That(NightVisibility.ExposureLift(level, 0.6f), Is.Zero);
                Assert.That(NightVisibility.AmbientGain(level, 1f), Is.EqualTo(1f));
            }
        }

        [Test]
        public void HigherLevelsLiftTheNight()
        {
            Assert.That(NightVisibility.ExposureLift(0, 0f), Is.Zero, "Natural is the old night");
            Assert.That(NightVisibility.AmbientGain(0, 0f), Is.EqualTo(1f));
            Assert.That(NightVisibility.ExposureLift(1, 0f), Is.GreaterThan(0f));
            Assert.That(NightVisibility.ExposureLift(2, 0f), Is.GreaterThan(NightVisibility.ExposureLift(1, 0f)));
            Assert.That(NightVisibility.AmbientGain(2, 0f), Is.GreaterThan(NightVisibility.AmbientGain(1, 0f)));
            Assert.That(NightVisibility.ExposureLift(2, 0f), Is.LessThanOrEqualTo(1f), "brighter, not daylight");
            Assert.That(NightVisibility.ExposureLift(1, 0.25f), Is.InRange(0f, NightVisibility.ExposureLift(1, 0f)),
                "eases in through dusk");
            Assert.That(NightVisibility.Clamp(7), Is.EqualTo(NightVisibility.Labels.Length - 1));
            Assert.That(NightVisibility.Clamp(-1), Is.Zero);
        }
    }
}
