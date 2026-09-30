using System.Linq;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class AdelaideCoastLandformTests
    {
        [Test]
        public void DuneBoost_PeaksInTheBandAndIsZeroOnTheWaterline()
        {
            Assert.That(AdelaideCoastLandform.DuneBoostMetres(10f, -2000f, 0f, true), Is.EqualTo(0f));
            Assert.That(AdelaideCoastLandform.DuneBoostMetres(400f, -2000f, 0f, true), Is.EqualTo(0f));
            var peak = AdelaideCoastLandform.DuneBoostMetres(
                AdelaideCoastLandform.DuneBandPeakMetres, -2200f, 200f, true);
            Assert.That(peak, Is.GreaterThan(1f).And.LessThan(5f));
            var plain = AdelaideCoastLandform.DuneBoostMetres(
                AdelaideCoastLandform.DuneBandPeakMetres, -2200f, 200f, false);
            Assert.That(peak, Is.GreaterThan(plain));
        }

        [Test]
        public void DuneBoost_IsDeterministic()
        {
            var a = AdelaideCoastLandform.DuneBoostMetres(70f, -2100f, 100f, true);
            var b = AdelaideCoastLandform.DuneBoostMetres(70f, -2100f, 100f, true);
            Assert.That(a, Is.EqualTo(b));
        }

        [Test]
        public void OutletBlend_IsStrongNearTheGulf()
        {
            Assert.That(AdelaideCoastLandform.OutletBlend(50f), Is.GreaterThan(0.85f));
            Assert.That(AdelaideCoastLandform.OutletBlend(500f), Is.LessThan(0.15f));
        }

        [Test]
        public void FoamSegments_FollowTheRealCoastlineSeaward()
        {
            var segments = AdelaideCoastLandform.FoamSegments();
            Assert.That(segments.Count, Is.GreaterThan(20));
            Assert.That(segments.All(s => s.Length >= AdelaideCoastLandform.FoamMinSegmentMetres), Is.True);
            // West Beach is west of the field — foam should sit well west of the runway.
            Assert.That(segments.Average(s => (s.Ax + s.Bx) * 0.5f), Is.LessThan(-500f));
        }

        [Test]
        public void FoamQuad_SpansLandwardAndSeawardOfTheSegment()
        {
            var segment = AdelaideCoastLandform.FoamSegments().First();
            var quad = AdelaideCoastLandform.FoamQuad(segment, 3f, 10f);
            Assert.That(quad.Length, Is.EqualTo(8));
            Assert.That(quad[0], Is.EqualTo(segment.Ax - segment.OutwardX * 3f).Within(0.01f));
            Assert.That(quad[1], Is.EqualTo(segment.Az - segment.OutwardZ * 3f).Within(0.01f));
            Assert.That(quad[4], Is.EqualTo(segment.Bx + segment.OutwardX * 10f).Within(0.01f));
            Assert.That(quad[5], Is.EqualTo(segment.Bz + segment.OutwardZ * 10f).Within(0.01f));
            // Landward and seaward edges are a full ribbon width apart.
            var midLandX = (quad[0] + quad[2]) * 0.5f;
            var midLandZ = (quad[1] + quad[3]) * 0.5f;
            var midSeaX = (quad[4] + quad[6]) * 0.5f;
            var midSeaZ = (quad[5] + quad[7]) * 0.5f;
            var span = System.Math.Sqrt(
                (midSeaX - midLandX) * (midSeaX - midLandX)
                + (midSeaZ - midLandZ) * (midSeaZ - midLandZ));
            Assert.That(span, Is.EqualTo(13.0).Within(0.05));
        }

        [Test]
        public void DuneBoost_RaisesTheSandBandAboveAFlatBeachDip()
        {
            // Same composition the surroundings mesh applies: beach lerp then dune boost.
            const float beachY = -4.2f;
            var dune = AdelaideCoastLandform.DuneBoostMetres(70f, -2300f, 150f, true);
            Assert.That(beachY + dune, Is.GreaterThan(beachY + 1f));
        }
    }
}
