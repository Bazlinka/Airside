using System;
using Airside.Domain;
using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>Presentation daylight pin vs live day cycle (no Unity types).</summary>
    public sealed class DaylightPresentationTests
    {
        [Test]
        public void Resolve_PinForcesNoon()
        {
            Assert.That(DaylightPresentation.Resolve(pinToNoon: true, simulationDaylight: 0.0), Is.EqualTo(1f));
            Assert.That(DaylightPresentation.Resolve(pinToNoon: true, simulationDaylight: 0.4), Is.EqualTo(1f));
        }

        [Test]
        public void Resolve_FollowsSimulationWhenUnpinned()
        {
            Assert.That(DaylightPresentation.Resolve(false, 0.0), Is.EqualTo(0f));
            Assert.That(DaylightPresentation.Resolve(false, 1.0), Is.EqualTo(1f));
            Assert.That(DaylightPresentation.Resolve(false, 0.5), Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void LiveAdelaideNight_IsDarkWhenUnpinned()
        {
            // Day cycle starts day 1 at 08:00; 16 hours later is local midnight.
            var midnight = new DayCycle(DayCycle.MidnightOfDay(1));
            Assert.That(midnight.Phase, Is.EqualTo(DayPhase.Night));
            Assert.That(DaylightPresentation.Resolve(false, midnight.Daylight), Is.EqualTo(0f));
        }

        [Test]
        public void ReviewLocalTime_ReadsStrictTwentyFourHourTime()
        {
            Assert.That(DaylightPresentation.ReviewLocalTime(new[] { "Airside", "-airsideReviewTime", "18:45" }),
                Is.EqualTo(new TimeSpan(18, 45, 0)));
            Assert.That(DaylightPresentation.ReviewLocalTime(new[] { "Airside", "-airsideReviewTime", "6:45" }),
                Is.Null);
            Assert.That(DaylightPresentation.ReviewLocalTime(new[] { "Airside", "-airsideReviewTime", "24:00" }),
                Is.Null);
            Assert.That(DaylightPresentation.ReviewLocalTime(new[] { "Airside" }), Is.Null);
        }
    }
}
