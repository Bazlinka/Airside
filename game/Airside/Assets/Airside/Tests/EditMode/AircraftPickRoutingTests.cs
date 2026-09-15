using System.Collections.Generic;
using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class AircraftPickRoutingTests
    {
        [Test]
        public void CountsAsClick_AcceptsAStationaryPress()
        {
            Assert.That(AircraftPickRouting.CountsAsClick(100f, 200f, 100f, 200f), Is.True);
            Assert.That(AircraftPickRouting.CountsAsClick(100f, 200f, 103f, 201f), Is.True);
        }

        [Test]
        public void CountsAsClick_RejectsADragPastThePanThreshold()
        {
            Assert.That(AircraftPickRouting.CountsAsClick(100f, 200f, 105f, 200f), Is.False);
            Assert.That(AircraftPickRouting.CountsAsClick(0f, 0f, 10f, 10f), Is.False);
        }

        [Test]
        public void TryRegistrationFromViewName_ReadsCommercialPrefix()
        {
            Assert.That(AircraftPickRouting.TryRegistrationFromViewName("Commercial VH-PAX", out var id), Is.True);
            Assert.That(id, Is.EqualTo("VH-PAX"));
            Assert.That(AircraftPickRouting.TryRegistrationFromViewName("VH-PAX", out _), Is.False);
            Assert.That(AircraftPickRouting.TryRegistrationFromViewName("Commercial ", out _), Is.False);
        }

        [Test]
        public void ResolveNearest_PicksTheClosestSelectableAircraft()
        {
            var hits = new List<AircraftPickHit>
            {
                new("VH-FAR", 120f, true),
                new("VH-NEAR", 40f, true),
                new("VH-CLOSER-BUT-AWAY", 10f, false)
            };

            Assert.That(AircraftPickRouting.ResolveNearest(hits), Is.EqualTo("VH-NEAR"));
        }

        [Test]
        public void ResolveNearest_ReturnsNullWhenNothingOnFieldIsHit()
        {
            var hits = new List<AircraftPickHit>
            {
                new("VH-AWAY", 5f, false),
                new(null, 1f, true)
            };

            Assert.That(AircraftPickRouting.ResolveNearest(hits), Is.Null);
        }

        [Test]
        public void IsOnFieldSelectable_RequiresAnActiveOnFieldEntry()
        {
            var onField = new Dictionary<string, bool>
            {
                ["VH-PAX"] = true,
                ["VH-EMU"] = false
            };

            Assert.That(AircraftPickRouting.IsOnFieldSelectable("VH-PAX", onField), Is.True);
            Assert.That(AircraftPickRouting.IsOnFieldSelectable("VH-EMU", onField), Is.False);
            Assert.That(AircraftPickRouting.IsOnFieldSelectable("VH-MISSING", onField), Is.False);
            Assert.That(AircraftPickRouting.IsOnFieldSelectable(null, onField), Is.False);
        }

        [Test]
        public void IndexOfSame_KeepsTheCurrentAircraftOrReportsItLeft()
        {
            var first = new object();
            var selected = new object();
            var next = new[] { first, selected };

            Assert.That(AircraftPickRouting.IndexOfSame(next, selected), Is.EqualTo(1));
            Assert.That(AircraftPickRouting.IndexOfSame(new[] { first }, selected), Is.EqualTo(-1));
            Assert.That(AircraftPickRouting.IndexOfSame(System.Array.Empty<object>(), selected), Is.EqualTo(-1));
            Assert.That(AircraftPickRouting.IndexOfSame(next, null), Is.EqualTo(-1));
        }
    }
}
