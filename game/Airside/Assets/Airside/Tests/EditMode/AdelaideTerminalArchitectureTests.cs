using System.Linq;
using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class AdelaideTerminalArchitectureTests
    {
        [Test]
        public void AirsideGlazing_SpansTheRealApronFacadeInSeparatedBays()
        {
            var bays = AdelaideTerminalArchitecture.AirsideGlazing();

            Assert.That(bays.Length, Is.EqualTo(28));
            Assert.That(bays.First().X - bays.First().Width * 0.5f, Is.EqualTo(986f).Within(0.01f));
            Assert.That(bays.Last().X + bays.Last().Width * 0.5f, Is.LessThan(1616f));
            Assert.That(bays.All(b => b.Z < 436f && b.Height > 7f), Is.True);
            for (var i = 1; i < bays.Length; i++)
                Assert.That(bays[i].X - bays[i - 1].X, Is.GreaterThan(bays[i].Width), "mullion gaps must remain visible");
        }

        [Test]
        public void GlazingMullions_SitInEveryGapBetweenAdjacentBaysWithoutOverlappingEitherPane()
        {
            var bays = AdelaideTerminalArchitecture.AirsideGlazing();
            var mullions = AdelaideTerminalArchitecture.GlazingMullions();

            // One mullion per gap between adjacent bays, none before the first or after the last.
            Assert.That(mullions.Length, Is.EqualTo(bays.Length - 1));
            for (var i = 0; i < mullions.Length; i++)
            {
                var left = bays[i];
                var right = bays[i + 1];
                var mullion = mullions[i];
                var leftEdge = left.X + left.Width * 0.5f;
                var rightEdge = right.X - right.Width * 0.5f;
                Assert.That(mullion.X - mullion.Width * 0.5f, Is.GreaterThan(leftEdge),
                    $"mullion {i} overlaps bay {i}'s glass");
                Assert.That(mullion.X + mullion.Width * 0.5f, Is.LessThan(rightEdge),
                    $"mullion {i} overlaps bay {i + 1}'s glass");
                // Proud of the glass plane (larger Z than the panes) so it reads as framing
                // in front of the glass rather than flush or recessed behind it.
                Assert.That(mullion.Z, Is.GreaterThanOrEqualTo(left.Z));
            }
        }

        [Test]
        public void RoofDetails_StayAboveTheShellAndInsideItsMainAirsideBar()
        {
            var details = AdelaideTerminalArchitecture.RoofDetails();

            Assert.That(details.Length, Is.InRange(8, 16));
            Assert.That(details.All(d => d.Y - d.Height * 0.5f >= AdelaideTerminalArchitecture.ShellHeightMetres), Is.True);
            Assert.That(details.All(d => d.X - d.Width * 0.5f > 975f && d.X + d.Width * 0.5f < 1616f), Is.True);
            Assert.That(details.All(d => d.Z - d.Depth * 0.5f >= 436f && d.Z + d.Depth * 0.5f < 474f), Is.True);
        }

        [Test]
        public void ApronFloods_RunAlongTheRealTerminalAndAimSouthOntoTheStands()
        {
            var floods = AdelaideTerminalArchitecture.ApronFloods();

            Assert.That(floods.Length, Is.EqualTo(7));
            Assert.That(floods.All(f => f.X > 975f && f.X < 1616f), Is.True);
            Assert.That(floods.All(f => f.Z < 436f && f.TargetZ < f.Z - 40f), Is.True);
            Assert.That(AdelaideTerminalArchitecture.FloodHeightMetres, Is.GreaterThan(AdelaideTerminalArchitecture.ShellHeightMetres));
            Assert.That(AdelaideTerminalArchitecture.FloodRangeMetres, Is.GreaterThan(100f));
        }
    }
}
