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
        public void OffsetRun_StaysTheRightDistanceOffAStraightAndABend()
        {
            var straight = new System.Collections.Generic.List<UnityEngine.Vector2> { new(0, 0), new(100, 0) };
            var moved = AirsideAdelaideRoads.OffsetRun(straight, 3f);
            Assert.That(moved[0].y, Is.EqualTo(3f).Within(1e-3f), "left of +x travel is +z");
            var bend = new System.Collections.Generic.List<UnityEngine.Vector2> { new(0, 0), new(50, 0), new(100, 50) };
            foreach (var p in AirsideAdelaideRoads.OffsetRun(bend, 4f))
            {
                var nearest = float.MaxValue;
                for (var t = 0f; t <= 1f; t += 0.01f)
                    nearest = System.Math.Min(nearest, UnityEngine.Vector2.Distance(p,
                        t < 0.5f ? UnityEngine.Vector2.Lerp(bend[0], bend[1], t * 2f) : UnityEngine.Vector2.Lerp(bend[1], bend[2], t * 2f - 1f)));
                Assert.That(nearest, Is.InRange(3.2f, 4.1f));
            }
        }

        [Test]
        public void RoadMarkingMesh_BuildsFromTheRealRoads()
        {
            var mesh = AirsideAdelaideRoads.BuildLaneMarkingMesh(0f);
            Assert.That(mesh, Is.Not.Null);
            Assert.That(mesh.vertexCount, Is.GreaterThan(1000));
        }

        [Test]
        public void Laneways_GetOnlyACentreLine()
        {
            var lines = RoadMarkingPlan.For(6f);
            Assert.That(lines.Length, Is.EqualTo(1));
        }
    }
}
