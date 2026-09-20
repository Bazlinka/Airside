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
            // Z now tracks the real wall's own gentle curve (ADR 0068) instead of one constant
            // — the near end sits at ~437.7, the far end at ~435.8, not a single fixed value.
            Assert.That(bays.All(b => b.Z is > 435.7f and < 437.8f && b.Height > 7f), Is.True);
            Assert.That(bays.First().Z, Is.GreaterThan(bays.Last().Z),
                "the wall's real Z drifts down across the glazing run, near end higher than far end");
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
        public void AirsideWallZAt_MatchesTheRealFootprintAndClampsPastEitherEnd()
        {
            // Exact vertices from AdelaideLayout.Terminals's own OSM footprint (ADR 0068) —
            // not re-measured, so this proves the two stay in lockstep.
            Assert.That(AdelaideTerminalArchitecture.AirsideWallZAt(993.3f), Is.EqualTo(437.7f).Within(0.01f));
            Assert.That(AdelaideTerminalArchitecture.AirsideWallZAt(1604.0f), Is.EqualTo(435.8f).Within(0.01f));
            Assert.That(AdelaideTerminalArchitecture.AirsideWallZAt(1036.5f), Is.EqualTo(437.6f).Within(0.01f));

            // Interpolated, not snapped, at a point strictly between two authored vertices.
            var mid = AdelaideTerminalArchitecture.AirsideWallZAt(1015f);
            Assert.That(mid, Is.InRange(437.6f, 437.7f));

            // Past either end, clamps to that end rather than extrapolating off the building.
            Assert.That(AdelaideTerminalArchitecture.AirsideWallZAt(500f), Is.EqualTo(437.7f).Within(0.01f));
            Assert.That(AdelaideTerminalArchitecture.AirsideWallZAt(2000f), Is.EqualTo(435.8f).Within(0.01f));
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

            Assert.That(floods.Length, Is.EqualTo(10));
            var terminal = floods.Where(f => f.Z < 450f).ToArray();
            var regional = floods.Where(f => f.Z >= 450f).ToArray();
            Assert.That(terminal.Length, Is.EqualTo(7));
            Assert.That(terminal.All(f => f.X > 975f && f.X < 1616f), Is.True);
            Assert.That(terminal.All(f => f.Z < 436f && f.TargetZ < f.Z - 40f), Is.True);
            Assert.That(regional.Length, Is.EqualTo(3));
            Assert.That(regional.All(f => f.TargetZ < f.Z), Is.True);
            Assert.That(AdelaideTerminalArchitecture.FloodHeightMetres, Is.GreaterThan(AdelaideTerminalArchitecture.ShellHeightMetres));
            Assert.That(AdelaideTerminalArchitecture.FloodRangeMetres, Is.GreaterThan(100f));
        }
    }
}
