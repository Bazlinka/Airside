using System.Linq;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class AdelaideTaxiwayLabelsTests
    {
        private const string Alphabet = "0123456789ABCDEFGHJKLRT";

        [Test]
        public void EveryRealTaxiwayReferenceCanBePainted()
        {
            var refs = AdelaideLayout.Taxiways.Select(t => t.Reference).Where(r => !string.IsNullOrEmpty(r)).Distinct();
            foreach (var reference in refs)
            foreach (var c in reference)
                Assert.That(Alphabet.Contains(c), $"taxiway '{reference}' uses unpainted glyph '{c}'");
        }

        [Test]
        public void NamedTaxiwaysAreLabelledUnnamedOnesAreNot()
        {
            var labels = AdelaideTaxiwayLabels.All();
            Assert.That(labels, Is.Not.Empty);
            var named = AdelaideLayout.Taxiways.Where(t => !string.IsNullOrEmpty(t.Reference))
                .Select(t => t.Reference).ToHashSet();
            Assert.That(labels.All(l => named.Contains(l.Reference)));
            // The long A6 (≈ 400 m) and the D2 loop must carry a label.
            Assert.That(labels.Any(l => l.Reference == "A6"));
            Assert.That(labels.Any(l => l.Reference == "D2"));
        }

        [Test]
        public void LabelsSitBesideTheirCentrelineNotOnIt()
        {
            var taxiway = new AdelaideTaxiway("A", 23f, new[] { 0f, 0f, 200f, 0f });
            var label = AdelaideTaxiwayLabels.Build(new[] { taxiway }).Single();
            Assert.That(label.X, Is.EqualTo(100f).Within(0.01f));
            Assert.That(System.Math.Abs(label.Z), Is.EqualTo(AdelaideTaxiwayLabels.SideOffsetMetres).Within(0.01f));
            Assert.That(label.YawDegrees, Is.EqualTo(90f).Within(0.01f));
        }

        [Test]
        public void LongTaxiwaysGetRepeatedLabelsAndStubsNone()
        {
            var longWay = new AdelaideTaxiway("B", 23f, new[] { 0f, 0f, 1400f, 0f });
            var stub = new AdelaideTaxiway("C", 23f, new[] { 0f, 0f, 30f, 0f });
            var labels = AdelaideTaxiwayLabels.Build(new[] { longWay, stub });
            Assert.That(labels.Count(l => l.Reference == "B"), Is.GreaterThanOrEqualTo(3));
            Assert.That(labels.Any(l => l.Reference == "C"), Is.False);
        }
    }
}
