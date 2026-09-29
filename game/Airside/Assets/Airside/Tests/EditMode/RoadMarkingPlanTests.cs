using System.Linq;
using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class RoadMarkingPlanTests
    {
        [Test]
        public void TwoLaneRoad_KeepsItsDashedCentreAndGainsEdgeLines()
        {
            var lines = RoadMarkingPlan.For(9f);
            Assert.That(lines.Count(l => l.Dashed && l.Offset == 0f), Is.EqualTo(1));
            Assert.That(lines.Count(l => !l.Dashed), Is.EqualTo(2));
            Assert.That(lines.Where(l => !l.Dashed).Select(l => System.Math.Abs(l.Offset)),
                Is.All.EqualTo(9f * 0.5f - RoadMarkingPlan.EdgeInsetMetres).Within(1e-4));
        }

        [Test]
        public void WideRoad_GetsADoubleCentreAndDashedLaneLines()
        {
            var lines = RoadMarkingPlan.For(21f); // six lanes
            Assert.That(RoadMarkingPlan.Lanes(21f), Is.EqualTo(6));
            Assert.That(lines.Count(l => !l.Dashed && System.Math.Abs(l.Offset) < 0.5f), Is.EqualTo(2), "double centre");
            Assert.That(lines.Count(l => l.Dashed), Is.EqualTo(4), "two lane lines each side");
            Assert.That(lines.Any(l => l.Dashed && l.Offset == 0f), Is.False, "no dashed line where you may not cross");
        }

        [Test]
        public void EveryLineSitsInsideTheRoad()
        {
            foreach (var width in new[] { 6f, 9f, 12f, 16f, 24f, 40f })
                foreach (var line in RoadMarkingPlan.For(width))
                    Assert.That(System.Math.Abs(line.Offset) + line.Width * 0.5f, Is.LessThan(width * 0.5f), $"width {width}");
        }

        [Test]
        public void Laneways_GetOnlyACentreLine()
        {
            var lines = RoadMarkingPlan.For(6f);
            Assert.That(lines.Length, Is.EqualTo(1));
        }
    }
}
