using System.Linq;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class TaxiwayEdgeWearTests
    {
        [Test]
        public void StraightTaxiway_PlacesSparseStripsOnBothInsideEdges()
        {
            var strips = TaxiwayEdgeWear.Generate(new[] { 0f, 0f, 100f, 0f }, 10f);

            Assert.That(strips.Count, Is.InRange(7, 12));
            Assert.That(strips.Any(s => s.StartZ > 0f), Is.True);
            Assert.That(strips.Any(s => s.StartZ < 0f), Is.True);
            Assert.That(strips.All(s => s.Length <= TaxiwayEdgeWear.PatchLengthMetres + 0.001f), Is.True);
            Assert.That(strips.All(s => s.StartX >= 0f && s.EndX <= 100f), Is.True);
        }

        [Test]
        public void Generate_IsDeterministicAndRejectsDegenerateInput()
        {
            var centreline = new[] { -20f, 4f, 40f, 18f, 70f, 60f };
            var first = TaxiwayEdgeWear.Generate(centreline, 9f);
            var second = TaxiwayEdgeWear.Generate(centreline, 9f);

            Assert.That(second.Count, Is.EqualTo(first.Count));
            for (var i = 0; i < first.Count; i++)
            {
                Assert.That(second[i].StartX, Is.EqualTo(first[i].StartX));
                Assert.That(second[i].StartZ, Is.EqualTo(first[i].StartZ));
                Assert.That(second[i].EndX, Is.EqualTo(first[i].EndX));
                Assert.That(second[i].EndZ, Is.EqualTo(first[i].EndZ));
            }

            Assert.That(TaxiwayEdgeWear.Generate(null, 9f), Is.Empty);
            Assert.That(TaxiwayEdgeWear.Generate(new[] { 0f, 0f, 0.2f, 0.2f }, 9f), Is.Empty);
        }

        [Test]
        public void AdelaideTaxiways_StayWithinAQuietDetailBudget()
        {
            var count = AdelaideLayout.Taxiways.Sum(t => TaxiwayEdgeWear.Generate(t.Xz, t.Width * 0.5f).Count);
            Assert.That(count, Is.InRange(100, 2500));
        }
    }
}
