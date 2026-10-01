using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class GolfBunkerMarksTests
    {
        [Test]
        public void Bunkers_MatchOsmCountAndRadiusClamp()
        {
            Assert.That(AdelaideGolfBunkers.Count, Is.EqualTo(213));
            for (var i = 0; i < AdelaideGolfBunkers.Count; i++)
            {
                AdelaideGolfBunkers.Get(i, out var x, out var z, out var r);
                Assert.That(r, Is.InRange(2f, 22f), $"bunker {i} radius {r}");
                Assert.That(AdelaideLandCover.InOperationalCore(x, z), Is.False,
                    $"bunker {i} at ({x:0},{z:0}) is on the airfield");
            }
        }

        [Test]
        public void Bunkers_IncludeGlenelgCourse()
        {
            Assert.That(GolfBunkerMarks.CountNearGlenelg(), Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void DiscCorners_RingAroundCentre()
        {
            var corners = GolfBunkerMarks.DiscCorners(10f, -20f, 5f, sides: 8);
            Assert.That(corners.Length, Is.EqualTo(16));
            for (var i = 0; i < 8; i++)
            {
                var dx = corners[i * 2] - 10f;
                var dz = corners[i * 2 + 1] - (-20f);
                Assert.That(System.Math.Sqrt(dx * dx + dz * dz), Is.EqualTo(5.0).Within(1e-4));
            }
        }

        [Test]
        public void Attribution_NamesOpenStreetMap()
        {
            Assert.That(AdelaideGolfBunkers.Attribution, Does.Contain("OpenStreetMap"));
        }
    }
}
