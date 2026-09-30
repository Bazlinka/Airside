using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests.EditMode
{
    public sealed class ReviewShotScheduleTests
    {
        [Test]
        public void SingleShot_MatchesLegacyCaptureGameArgs()
        {
            var args = new[]
            {
                "Airside",
                "-airsideSoak",
                "-airsideReviewShot", "/tmp/day.png",
                "-airsideReviewDelay", "780",
                "-airsideReviewFollowZoom", "0.55",
                "-airsideReviewWeather", "clear",
            };

            Assert.That(ReviewShotSchedule.TryParse(args, out var schedule), Is.True);
            Assert.That(schedule.Count, Is.EqualTo(1));
            Assert.That(schedule[0].Path, Is.EqualTo("/tmp/day.png"));
            Assert.That(schedule[0].DelaySeconds, Is.EqualTo(780f));
            Assert.That(schedule[0].FollowZoom, Is.EqualTo(0.55f));
            Assert.That(schedule[0].WeatherToken, Is.EqualTo("clear"));
        }

        [Test]
        public void MultiShot_KeepsPerShotDelayZoomAndWeather()
        {
            var args = new[]
            {
                "Airside",
                "-airsideReviewShot", "/tmp/day.png",
                "-airsideReviewDelay", "780",
                "-airsideReviewFollowZoom", "0.55",
                "-airsideReviewWeather", "clear",
                "-airsideReviewShot", "/tmp/close.png",
                "-airsideReviewDelay", "783",
                "-airsideReviewFollowZoom", "0.35",
                "-airsideReviewShot", "/tmp/storm.png",
                "-airsideReviewDelay", "786",
                "-airsideReviewFollowZoom", "0.55",
                "-airsideReviewWeather", "storm",
            };

            Assert.That(ReviewShotSchedule.TryParse(args, out var schedule), Is.True);
            Assert.That(schedule.Count, Is.EqualTo(3));
            Assert.That(schedule[0].Path, Does.EndWith("day.png"));
            Assert.That(schedule[0].DelaySeconds, Is.EqualTo(780f));
            Assert.That(schedule[0].WeatherToken, Is.EqualTo("clear"));
            Assert.That(schedule[1].Path, Does.EndWith("close.png"));
            Assert.That(schedule[1].DelaySeconds, Is.EqualTo(783f));
            Assert.That(schedule[1].FollowZoom, Is.EqualTo(0.35f));
            Assert.That(schedule[1].WeatherToken, Is.Null);
            Assert.That(schedule[2].Path, Does.EndWith("storm.png"));
            Assert.That(schedule[2].DelaySeconds, Is.EqualTo(786f));
            Assert.That(schedule[2].WeatherToken, Is.EqualTo("storm"));
        }

        [Test]
        public void MissingShotFlag_ReturnsFalse()
        {
            Assert.That(ReviewShotSchedule.TryParse(new[] { "Airside", "-airsideSoak" }, out _), Is.False);
        }
    }
}
