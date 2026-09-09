using System.Collections.Generic;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace Airside.Tests
{
    public sealed class PresentationLayoutTests
    {
        [TestCase(1440, 900, 1f)]
        [TestCase(1280, 720, 0.8f)]
        public void HudScale_NormalWindowsUseReadableVirtualSize(int width, int height, float expected)
        {
            Assert.That(HudLayout.ScaleFor(width, height), Is.EqualTo(expected).Within(0.001f));
        }

        [Test]
        public void HudScale_RetinaDisplayIsNotCappedAtTheOldTinyScale()
        {
            Assert.That(HudLayout.ScaleFor(3456, 2168), Is.EqualTo(2.25f).Within(0.001f));
        }

        [TestCase(1280f, 800f)]
        [TestCase(1536f, 964f)]
        public void HudLayout_DailyReportAndOfferHaveSeparateSpace(float width, float height)
        {
            var layout = HudLayout.Create(width, height, 330f, showDailyReport: true, showRouteOffer: true);

            Assert.That(layout.OperationsPanel.Overlaps(layout.DailyReportPanel), Is.False);
            Assert.That(layout.OperationsPanel.Overlaps(layout.RouteOfferPanel), Is.False);
            Assert.That(layout.DailyReportPanel.Overlaps(layout.RouteOfferPanel), Is.False);
            Assert.That(layout.RouteOfferPanel.yMax, Is.LessThanOrEqualTo(height - 22f));
            Assert.That(layout.LeftPanel.Overlaps(layout.OperationsPanel), Is.False);
        }

        [TestCase(1280, 720)]
        [TestCase(1440, 900)]
        [TestCase(3456, 2168)]
        public void HudLayout_PhysicalLaptopAndRetinaSizesRemainInsideViewport(int screenWidth, int screenHeight)
        {
            var scale = HudLayout.ScaleFor(screenWidth, screenHeight);
            var width = screenWidth / scale;
            var height = screenHeight / scale;
            var layout = HudLayout.Create(width, height, 354f, showDailyReport: true, showRouteOffer: true);

            Assert.That(layout.LeftPanel.xMin, Is.GreaterThanOrEqualTo(0f));
            Assert.That(layout.LeftPanel.yMax, Is.LessThanOrEqualTo(height));
            Assert.That(layout.RouteOfferPanel.xMax, Is.LessThanOrEqualTo(width));
            Assert.That(layout.RouteOfferPanel.yMax, Is.LessThanOrEqualTo(height));
        }

        [Test]
        public void TaxiVisualPath_ChangesSegmentsByChordLengthMatchingReservations()
        {
            var route = new TaxiRoute(
                "Unequal test route",
                new[] { new StableId("A"), new StableId("B"), new StableId("C") },
                new[] { new TaxiPoint(0f, 0f), new TaxiPoint(1f, 0f), new TaxiPoint(101f, 0f), new TaxiPoint(102f, 0f) });

            // Lengths 1 / 100 / 1 — progress 0.02 is still on A; 0.50 mid B; 0.995 on C.
            var first = TaxiVisualPath.PositionAt(route, 0.005f, reverse: false);
            var second = TaxiVisualPath.PositionAt(route, 0.50f, reverse: false);
            var third = TaxiVisualPath.PositionAt(route, 0.995f, reverse: false);
            var reverseEarly = TaxiVisualPath.PositionAt(route, 0.005f, reverse: true);

            Assert.That(first.x, Is.InRange(0f, 1f), "early progress stays on short A");
            Assert.That(second.x, Is.InRange(1f, 101f), "mid progress stays on long B");
            Assert.That(third.x, Is.InRange(101f, 102f), "late progress reaches C");
            Assert.That(reverseEarly.x, Is.InRange(101f, 102f), "reverse early stays on C");
            Assert.That(route.SegmentIndexAt(0.50, reverse: false), Is.EqualTo(1));
        }

        [Test]
        public void HudScale_SmallWindowsCanDropBelowOldMin()
        {
            Assert.That(HudLayout.ScaleFor(800, 500), Is.LessThan(0.8f));
            Assert.That(HudLayout.ScaleFor(800, 500), Is.GreaterThanOrEqualTo(0.55f));
        }

        [Test]
        public void GroundTrafficVisual_WhenYieldedDoesNotInterpolateThroughReleasedSpace()
        {
            var previous = new Vector3(20f, 0.7f, 9f);
            var resetBySimulation = new Vector3(8f, 0.7f, 9f);

            var visible = TaxiVisualPath.MoveGroundTraffic(previous, resetBySimulation, isHolding: true, maxDistanceDelta: 0.1f);

            Assert.That(visible, Is.EqualTo(resetBySimulation));
        }
    }
}
