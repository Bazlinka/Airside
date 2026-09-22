using System.Collections.Generic;
using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class MapLabelLayoutTests
    {
        [Test]
        public void DenseCluster_FansLabelsWithoutOverlapsOrEscapingTheMap()
        {
            var bounds = new HudBox(0f, 0f, 480f, 360f);
            var occupied = new List<HudBox>();

            for (var i = 0; i < 16; i++)
            {
                Assert.That(MapLabelLayout.TryPlace(bounds, 240f + i % 3, 180f + i % 2, 10f,
                    130f, 18f, occupied, out var placement), Is.True, "label " + i);
                Assert.That(placement.X, Is.GreaterThanOrEqualTo(bounds.X));
                Assert.That(placement.Right, Is.LessThanOrEqualTo(bounds.Right));
                Assert.That(placement.Y, Is.GreaterThanOrEqualTo(bounds.Y));
                Assert.That(placement.Bottom, Is.LessThanOrEqualTo(bounds.Bottom));
                for (var prior = 0; prior < occupied.Count; prior++)
                    Assert.That(placement.Overlaps(occupied[prior]), Is.False, $"label {i} overlaps {prior}");
                occupied.Add(placement);
            }
        }

        [Test]
        public void SaturatedCluster_LeavesTheNextLowPriorityLabelOut()
        {
            var bounds = new HudBox(0f, 0f, 160f, 40f);
            var occupied = new List<HudBox>();

            Assert.That(MapLabelLayout.TryPlace(bounds, 72f, 20f, 8f, 70f, 18f, occupied, out var first), Is.True);
            occupied.Add(first);
            Assert.That(MapLabelLayout.TryPlace(bounds, 72f, 20f, 8f, 70f, 18f, occupied, out _), Is.False);
        }
    }
}
