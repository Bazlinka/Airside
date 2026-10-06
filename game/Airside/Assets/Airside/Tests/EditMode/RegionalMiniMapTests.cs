using Airside.Domain;
using Airside.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Airside.Tests
{
    public sealed class RegionalMiniMapTests
    {
        [Test] public void ScopeIncludesEveryServedSouthAustralianDestination()
        {
            var map = RegionalMiniMap.Fit(new Rect(10, 20, 240, 130));
            foreach (var destination in DestinationCatalogue.All)
                if (destination.Region == "SA")
                {
                    Assert.That(RegionalMiniMap.Contains(destination.Latitude, destination.Longitude), Is.True, destination.Name);
                    Assert.That(map.Contains(RegionalMiniMap.Point(map, destination.Latitude, destination.Longitude)), Is.True, destination.Name);
                }
            Assert.That(RegionalMiniMap.Contains(-37.67, 144.84), Is.False);
            Assert.That(RegionalMiniMap.Contains(double.NaN, 138), Is.False);
        }
        [Test] public void BakedLandMaskAgreesWithExistingCoastGeography()
        {
            const int width = 48, height = 64;
            var pixels = RegionalMiniMap.Bake(width, height);
            for (var y = 0; y < height; y += 7)
            for (var x = 0; x < width; x += 5)
            {
                var latitude = RegionalMiniMap.South + (y + .5) / height * (RegionalMiniMap.North - RegionalMiniMap.South);
                var longitude = RegionalMiniMap.West + (x + .5) / width * (RegionalMiniMap.East - RegionalMiniMap.West);
                Assert.That(pixels[y * width + x].Equals(FieldMiniMap.Water), Is.EqualTo(!MapGeography.OnLand(longitude, latitude)), $"{latitude}, {longitude}");
            }
        }
        [Test] public void OverlappingFlightsCycleRegardlessOfSelectedDrawOrder()
        {
            var points = new[] { new Vector2(50, 50), new Vector2(50, 50), new Vector2(90, 50) };
            var ids = new[] { "VH-B", "VH-A", "VH-C" };
            Assert.That(RegionalMiniMap.Pick(points, ids, new Vector2(50, 50), "VH-A"), Is.EqualTo(0));
            Assert.That(RegionalMiniMap.Pick(points, ids, new Vector2(50, 50), "VH-B"), Is.EqualTo(1));
            Assert.That(RegionalMiniMap.Pick(points, ids, new Vector2(70, 80), null), Is.EqualTo(-1));
        }
        [Test] public void FullMapHitRadiusCyclesNearbyFlightsOutsideMiniMapRadius()
        {
            var points = new[] { new Vector2(50, 50), new Vector2(65, 50) };
            var ids = new[] { "VH-A", "VH-B" };
            Assert.That(RegionalMiniMap.Pick(points, ids, new Vector2(50, 50), "VH-A"), Is.EqualTo(0));
            Assert.That(RegionalMiniMap.Pick(points, ids, new Vector2(50, 50), "VH-A", 18f), Is.EqualTo(1));
        }
        [Test] public void NorthAndEastProjectUpAndRightAndFitStaysInsidePanel()
        {
            var area = new Rect(25, 30, 250, 140); var map = RegionalMiniMap.Fit(area);
            Assert.That(area.Contains(map.min), Is.True);
            Assert.That(area.Contains(map.max - Vector2.one), Is.True);
            var adelaide = RegionalMiniMap.Point(map, -34.95, 138.53);
            Assert.That(RegionalMiniMap.Point(map, -30, 140).x, Is.GreaterThan(adelaide.x));
            Assert.That(RegionalMiniMap.Point(map, -30, 140).y, Is.LessThan(adelaide.y));
        }
    }
}
