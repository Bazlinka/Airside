using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests.EditMode
{
    public sealed class ReviewOverviewFramingTests
    {
        [Test]
        public void TryReadExpected_ReadsNightSkyCliPose()
        {
            var args = new[]
            {
                "Airside",
                "-airsideReviewView", "overview",
                "-airsideOverviewDistance", "11000",
                "-airsideOverviewPitch", "8",
                "-airsideOverviewYaw", "270",
            };

            Assert.That(ReviewOverviewFraming.TryReadExpected(args, out var expected), Is.True);
            Assert.That(expected.PitchDegrees, Is.EqualTo(8f));
            Assert.That(expected.YawDegrees, Is.EqualTo(270f));
            Assert.That(expected.DistanceMetres, Is.EqualTo(11000f));
        }

        [Test]
        public void TryReadExpected_WithoutFramingFlags_ReturnsFalse()
        {
            Assert.That(
                ReviewOverviewFraming.TryReadExpected(
                    new[] { "Airside", "-airsideSoak", "-airsideReviewAircraft", "auto-landing" },
                    out _),
                Is.False);
        }

        [Test]
        public void Matches_AcceptsNightSkyPoseWithinTolerance()
        {
            var expected = new ReviewOverviewFraming.Pose(8f, 270f, 11000f);
            var actual = new ReviewOverviewFraming.Pose(9.5f, 268f, 10500f);
            Assert.That(ReviewOverviewFraming.Matches(expected, actual), Is.True);
        }

        [Test]
        public void Matches_RejectsNoseDownDefaultOverview()
        {
            // #490 keep PNG used default overview pitch ~50 / short range — must fail closed.
            var expected = new ReviewOverviewFraming.Pose(8f, 270f, 11000f);
            var actual = new ReviewOverviewFraming.Pose(50f, 200f, 2400f);
            Assert.That(ReviewOverviewFraming.Matches(expected, actual), Is.False);
        }

        [Test]
        public void Matches_IgnoresUnspecifiedAxes()
        {
            var expected = new ReviewOverviewFraming.Pose(8f, float.NaN, float.NaN);
            var actual = new ReviewOverviewFraming.Pose(7f, 12f, 99f);
            Assert.That(ReviewOverviewFraming.Matches(expected, actual), Is.True);
        }
    }
}
