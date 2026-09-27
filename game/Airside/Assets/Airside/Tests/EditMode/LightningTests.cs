using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class LightningTests
    {
        // Block 249 (896400-899999s) is a one-hour storm on the ADR 0143 drifting chain (see
        // Weather.At); blocks 248 (Cloudy) and 250 (Rain) bracket it.
        private const long StormBlockStart = 896400;
        private const long StormBlockEnd = 900000;

        [Test]
        public void Lightning_NeverStrikesOutsideAStorm()
        {
            for (long t = 0; t < StormBlockStart; t += 61)
                if (Weather.At(new SimulationTime(t)) != WeatherKind.Storm)
                    Assert.That(Lightning.StrikesAt(new SimulationTime(t)), Is.False);
            for (long t = StormBlockEnd; t < StormBlockEnd + 3600; t += 61)
                Assert.That(Lightning.StrikesAt(new SimulationTime(t)), Is.False);
        }

        [Test]
        public void Lightning_AlwaysOpensAStormOnItsFirstSecond()
        {
            Assert.That(Lightning.StrikesAt(new SimulationTime(StormBlockStart)), Is.True,
                "a storm should announce itself immediately, not build up silently");
        }

        [Test]
        public void Lightning_IsDeterministic()
        {
            for (long t = StormBlockStart; t < StormBlockEnd; t += 37)
                Assert.That(Lightning.StrikesAt(new SimulationTime(t)), Is.EqualTo(Lightning.StrikesAt(new SimulationTime(t))));
        }

        [Test]
        public void Lightning_GapsBetweenStrikesStayInTheAuthoredRange()
        {
            long? previous = null;
            for (var t = StormBlockStart; t < StormBlockEnd; t++)
            {
                if (!Lightning.StrikesAt(new SimulationTime(t)))
                    continue;
                if (previous.HasValue)
                    Assert.That(t - previous.Value, Is.InRange(5, 13), "gaps should read as an active storm, not a strobe");
                previous = t;
            }

            Assert.That(previous, Is.Not.Null, "the block should have produced at least one strike");
        }

        [TestCase(896400, true)]
        [TestCase(896412, true)]
        [TestCase(896420, true)]
        [TestCase(896405, false)]
        [TestCase(899999, false)]
        public void Lightning_MatchesTheComputedLadderForAKnownStormBlock(long second, bool expectedStrike)
        {
            Assert.That(Lightning.StrikesAt(new SimulationTime(second)), Is.EqualTo(expectedStrike));
        }

        [Test]
        public void Lightning_DistanceAndThunderDelayStayInBounds()
        {
            for (var t = StormBlockStart; t < StormBlockEnd; t += 7)
            {
                if (!Lightning.StrikesAt(new SimulationTime(t)))
                    continue;
                var distance = Lightning.DistanceFor(new SimulationTime(t));
                Assert.That(distance, Is.InRange(0f, 0.99f));
                var delay = Lightning.ThunderDelaySeconds(distance);
                Assert.That(delay, Is.InRange(0.3f, 7.3f), "thunder should never arrive before its own flash");
            }
        }
    }
}
