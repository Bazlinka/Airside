using System;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>ADR 0187: fenced passenger walkways with barrier tape.</summary>
    public sealed class AdelaideWalkwayTests
    {
        [Test]
        public void SomeBaysHaveAFencedWalk_AndTheyAreShortEnoughToWalk()
        {
            Assert.That(AdelaideWalkwayGeometry.Corridors.Count, Is.GreaterThan(0));
            foreach (var pair in AdelaideWalkwayGeometry.Corridors)
            {
                var c = pair.Value;
                var length = Math.Sqrt((c[2] - c[0]) * (c[2] - c[0]) + (c[3] - c[1]) * (c[3] - c[1]));
                Assert.That(length, Is.InRange(4, AdelaideWalkwayGeometry.MaxLengthMetres), pair.Key);
            }
        }

        [Test]
        public void NoWalkwayEntersARunway()
        {
            var half = AdelaideLayout.MainRunwayLengthMetres * 0.5f;
            foreach (var pair in AdelaideWalkwayGeometry.Corridors)
            {
                var c = pair.Value;
                for (var t = 0f; t <= 1f; t += 0.02f)
                {
                    var x = c[0] + (c[2] - c[0]) * t;
                    var z = c[1] + (c[3] - c[1]) * t;
                    Assert.That(Math.Abs(x) < half && Math.Abs(z) < 30f, Is.False, $"{pair.Key} at {x:F0},{z:F0}");
                }
            }
        }

        [Test]
        public void TheTapeIsDrawnOnBothSides_WithinABudget()
        {
            var sink = new RoadMeshSink();
            var drawn = AdelaideWalkwayGeometry.Build(sink, new RoadBuildOptions());
            Assert.That(drawn, Is.EqualTo(AdelaideWalkwayGeometry.Corridors.Count));
            Assert.That(sink.VertexCount, Is.InRange(100, 60000));
            TestContext.WriteLine($"{drawn} walkways, {sink.VertexCount} vertices");
        }

        [Test]
        public void ACorridorIsFoundByStandId()
        {
            foreach (var pair in AdelaideWalkwayGeometry.Corridors)
                Assert.That(AdelaideWalkwayGeometry.TryCorridor(new StableId(pair.Key), out var xz) && xz.Length == 4, Is.True);
            Assert.That(AdelaideWalkwayGeometry.TryCorridor(new StableId("nowhere"), out _), Is.False);
        }
    }
}
