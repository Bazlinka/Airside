using System.Collections.Generic;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace Airside.Tests
{
    /// <summary>Airfield mini-map projection, placement and baking (presentation helpers only).</summary>
    public sealed class FieldMiniMapTests
    {
        [Test]
        public void Bounds_HoldEveryBayAndBothRunwayEnds()
        {
            FieldMiniMap.WorldBounds(out var minX, out var maxX, out var minZ, out var maxZ);
            foreach (var bay in AdelaideLayout.Bays)
            {
                Assert.That(bay.StopX, Is.InRange(minX, maxX), bay.Id);
                Assert.That(bay.StopZ, Is.InRange(minZ, maxZ), bay.Id);
            }

            var half = AdelaideLayout.MainRunwayLengthMetres * 0.5f;
            Assert.That(minX, Is.LessThan(-half));
            Assert.That(maxX, Is.GreaterThan(half));
        }

        [Test]
        public void WorldToMap_AndBack_RoundTripsAndPutsTheTerminalSideUp()
        {
            var map = FieldMiniMap.FitMap(new Rect(22f, 700f, 288f, 138f));
            var bay = AdelaideLayout.Bays[0];
            var point = FieldMiniMap.WorldToMap(map, bay.StopX, bay.StopZ);
            Assert.That(map.Contains(point), Is.True);

            var back = FieldMiniMap.MapToWorld(map, point);
            Assert.That(back.x, Is.EqualTo(bay.StopX).Within(0.5f));
            Assert.That(back.y, Is.EqualTo(bay.StopZ).Within(0.5f));

            // Terminal side (positive z) is higher on screen (smaller GUI y) than the runway.
            Assert.That(point.y, Is.LessThan(FieldMiniMap.WorldToMap(map, bay.StopX, 0f).y));
        }

        [Test]
        public void MapToWorld_ClampsClicksOutsideTheMap()
        {
            var map = new Rect(0f, 0f, 200f, 100f);
            FieldMiniMap.WorldBounds(out var minX, out _, out _, out var maxZ);
            var corner = FieldMiniMap.MapToWorld(map, new Vector2(-50f, -50f));
            Assert.That(corner.x, Is.EqualTo(minX).Within(0.01f));
            Assert.That(corner.y, Is.EqualTo(maxZ).Within(0.01f));
        }

        [Test]
        public void FitMap_KeepsTheFieldAspectInsideTheArea()
        {
            var area = new Rect(10f, 10f, 300f, 300f);
            var map = FieldMiniMap.FitMap(area);
            FieldMiniMap.WorldBounds(out var minX, out var maxX, out var minZ, out var maxZ);
            Assert.That(map.width / map.height, Is.EqualTo((maxX - minX) / (maxZ - minZ)).Within(0.01f));
            Assert.That(area.Contains(map.min) && area.Contains(map.max - new Vector2(0.01f, 0.01f)), Is.True);
        }

        [TestCase(1280, 720)]
        [TestCase(1440, 900)]
        [TestCase(1920, 1080)]
        [TestCase(3456, 2168)]
        [TestCase(800, 500)]
        [TestCase(400, 780)]
        public void PanelFor_NeverOverlapsTheHud(int screenWidth, int screenHeight)
        {
            foreach (var guide in new[] { false, true })
            {
                var scale = HudLayout.ScaleFor(screenWidth, screenHeight);
                var hud = HudLayout.Create(screenWidth / scale, screenHeight / scale);
                var airline = AirlineHudLayout.Create(hud, guide);
                var panel = FieldMiniMap.PanelFor(hud, airline);
                if (panel.width <= 0f)
                    continue;
                foreach (var other in new[] { airline.TopBar, airline.Objective, airline.Operations, airline.Toast, airline.SelectedCard })
                {
                    if (other.width <= 0f || other.height <= 0f)
                        continue;
                    Assert.That(panel.Overlaps(other), Is.False, $"{screenWidth}x{screenHeight} guide={guide} overlaps {other}");
                }
                Assert.That(panel.yMax, Is.LessThanOrEqualTo(hud.Viewport.y));
            }
        }

        [Test]
        public void PanelFor_ShowsOnTheDesktopWindow()
        {
            var hud = HudLayout.Create(1440f, 900f);
            Assert.That(FieldMiniMap.PanelFor(hud, AirlineHudLayout.Create(hud)).width, Is.EqualTo(FieldMiniMap.PanelWidth));
        }

        [Test]
        public void GroundPoint_HitsThePlaneOrStopsAtTheFarLimit()
        {
            var down = FieldMiniMap.GroundPoint(new Vector3(10f, 100f, 20f), new Vector3(0f, -1f, 1f).normalized, 5000f);
            Assert.That(down.x, Is.EqualTo(10f).Within(0.01f));
            Assert.That(down.y, Is.EqualTo(120f).Within(0.01f));

            var up = FieldMiniMap.GroundPoint(new Vector3(0f, 100f, 0f), new Vector3(1f, 0.2f, 0f), 800f);
            Assert.That(up.x, Is.EqualTo(800f).Within(0.01f));
        }

        [Test]
        public void ViewChevron_IsCompactAndPointsInTheCameraDirection()
        {
            var map = new Rect(10f, 20f, 280f, 120f);
            var centre = new Vector2(100f, 80f);
            FieldMiniMap.ViewChevron(map, centre, Vector2.right, out var tip, out var left, out var right);

            Assert.That(tip.x, Is.GreaterThan(centre.x));
            Assert.That(left.x, Is.LessThan(centre.x));
            Assert.That(right.x, Is.LessThan(centre.x));
            Assert.That(Vector2.Distance(centre, tip), Is.EqualTo(18f).Within(0.01f));
            Assert.That(Vector2.Distance(left, right), Is.EqualTo(14f).Within(0.01f));
            Assert.That(map.Contains(tip) && map.Contains(left) && map.Contains(right), Is.True);
        }

        [Test]
        public void NearestDot_PicksTheClosestWithinRadius()
        {
            var dots = new List<Vector2> { new(0f, 0f), new(6f, 0f), new(40f, 0f) };
            Assert.That(FieldMiniMap.NearestDot(dots, new Vector2(5f, 0f)), Is.EqualTo(1));
            Assert.That(FieldMiniMap.NearestDot(dots, new Vector2(25f, 0f)), Is.EqualTo(-1));
        }

        [Test]
        public void Bake_PaintsRunwaysAndAprons()
        {
            const int w = 600, h = 280;
            var pixels = FieldMiniMap.Bake(w, h);
            Assert.That(pixels.Length, Is.EqualTo(w * h));

            var map = new Rect(0f, 0f, w, h);
            // Runway midpoint, in the bottom-up pixel buffer.
            var runway = FieldMiniMap.WorldToMap(map, 0f, 0f);
            var runwayPixel = pixels[(int)(h - runway.y) * w + (int)runway.x];
            Assert.That(runwayPixel, Is.EqualTo(FieldMiniMap.Runway));

            var counts = new Dictionary<Color32, int>();
            foreach (var p in pixels)
                counts[p] = counts.TryGetValue(p, out var n) ? n + 1 : 1;
            Assert.That(counts.ContainsKey(FieldMiniMap.Apron), Is.True);
            Assert.That(counts.ContainsKey(FieldMiniMap.Taxiway), Is.True);
            Assert.That(counts.ContainsKey(FieldMiniMap.Building), Is.True);
            Assert.That(counts[FieldMiniMap.Grass], Is.GreaterThan(w * h / 4));
            Assert.That(counts.ContainsKey(FieldMiniMap.Water), Is.True, "the gulf is painted, not grass");
        }

        [Test]
        public void Bake_WestOfThe05ThresholdIsWaterNotGrass()
        {
            const int w = 600, h = 280;
            var pixels = FieldMiniMap.Bake(w, h);
            var map = new Rect(0f, 0f, w, h);
            var west = FieldMiniMap.WorldToMap(map, -AdelaideLayout.MainRunwayLengthMetres * 0.5f - 1180f, 0f);
            var x = Mathf.Clamp((int)west.x, 0, w - 1);
            var y = Mathf.Clamp((int)(h - west.y), 0, h - 1);
            Assert.That(pixels[y * w + x], Is.EqualTo(FieldMiniMap.Water));
        }

        [Test]
        public void RunwayLabels_NameBothStrips()
        {
            var labels = new System.Collections.Generic.List<string>();
            foreach (var (label, _, _) in FieldMiniMap.RunwayLabels())
                labels.Add(label);
            Assert.That(labels, Is.EquivalentTo(new[] { "05", "23", "12", "30" }));
        }
    }
}
