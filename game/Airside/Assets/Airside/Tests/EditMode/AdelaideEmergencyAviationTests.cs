using System;
using System.Linq;
using Airside.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Airside.Tests
{
    /// <summary>ADR 0186: AIR-017 and the mapped Helipad West presentation.</summary>
    public sealed class AdelaideEmergencyAviationTests
    {
        [Test]
        public void BellPaintUsesOperatorPrimaryAndPresetAccentWithoutRecolouringGlassOrRotors()
        {
            ColorUtility.TryParseHtmlString("#0F8B8D", out var primary);
            Assert.That(AirsideAdelaideEmergencyAviation.PartColour("rescue_red_belly", primary), Is.EqualTo(primary));
            Assert.That(AirsideAdelaideEmergencyAviation.PartColour("livery_secondary", primary),
                Is.EqualTo(AircraftLiveryPaint.Secondary(primary)));
            Assert.That(AirsideAdelaideEmergencyAviation.PartColour("livery_emblem", primary),
                Is.EqualTo(AircraftLiveryPaint.Emblem(primary)));
            Assert.That(AirsideAdelaideEmergencyAviation.PartColour("cockpit_glass_left", primary),
                Is.EqualTo(AirsideAdelaideEmergencyAviation.PartColour("cockpit_glass_left")));
            Assert.That(AirsideAdelaideEmergencyAviation.PartColour("main_rotor_blade_1", primary),
                Is.EqualTo(AirsideAdelaideEmergencyAviation.PartColour("main_rotor_blade_1")));
        }

        [Test]
        public void HelipadWest_UsesTheCommittedOsmOutlineAndCentre()
        {
            Assert.That(AdelaideEmergencyAviationGeometry.OsmWayId, Is.EqualTo(1229789628));
            Assert.That(AdelaideEmergencyAviationGeometry.Outline.Length / 2, Is.EqualTo(19));
            var min = float.MaxValue;
            var max = float.MinValue;
            var sum = 0f;
            for (var i = 0; i < AdelaideEmergencyAviationGeometry.Outline.Length; i += 2)
            {
                var dx = AdelaideEmergencyAviationGeometry.Outline[i] - AdelaideEmergencyAviationGeometry.PadCentreX;
                var dz = AdelaideEmergencyAviationGeometry.Outline[i + 1] - AdelaideEmergencyAviationGeometry.PadCentreZ;
                var radius = (float)Math.Sqrt(dx * dx + dz * dz);
                min = Math.Min(min, radius);
                max = Math.Max(max, radius);
                sum += radius;
            }

            var mean = sum / (AdelaideEmergencyAviationGeometry.Outline.Length / 2);
            Assert.That(mean, Is.EqualTo(AdelaideEmergencyAviationGeometry.PadRadiusMetres).Within(0.08f));
            Assert.That(max - min, Is.LessThan(0.25f), "the committed OSM way is a circular pad");
        }

        [Test]
        public void Helipad_HasSurfaceMarkingsAndTwelveEdgeLights()
        {
            var surface = new RoadMeshSink();
            var paint = new RoadMeshSink();
            var fixtures = new RoadMeshSink();
            var options = new RoadBuildOptions();
            Assert.That(AdelaideEmergencyAviationGeometry.BuildSurface(surface, options), Is.EqualTo(19));
            Assert.That(AdelaideEmergencyAviationGeometry.BuildPaint(paint, options), Is.EqualTo(3));
            Assert.That(AdelaideEmergencyAviationGeometry.BuildFixtures(fixtures, options),
                Is.EqualTo(AdelaideEmergencyAviationGeometry.EdgeLightCount));
            Assert.That(surface.TriangleCount, Is.EqualTo(19));
            Assert.That(paint.TriangleCount, Is.GreaterThan(160), "perimeter, touchdown ring and H");
            Assert.That(fixtures.TriangleCount, Is.GreaterThan(300));
        }

        [Test]
        public void Air017_LoadsAtBell412ClassDimensions_WithRequiredSilhouetteParts()
        {
            var parent = new GameObject("AIR-017 test").transform;
            try
            {
                Assert.That(ArtPresentationLoader.TryInstantiate(
                    AirsideAdelaideEmergencyAviation.ArtPath, parent, out var root), Is.True);
                Assert.That(root, Is.Not.Null);
                Assert.That(root.Find("fuselage"), Is.Not.Null);
                Assert.That(root.Find("engine_left"), Is.Not.Null);
                Assert.That(root.Find("engine_right"), Is.Not.Null);
                Assert.That(root.Find("landing_skid_left"), Is.Not.Null);
                Assert.That(root.Find("landing_skid_right"), Is.Not.Null);
                for (var i = 1; i <= 4; i++)
                    Assert.That(root.Find($"main_rotor_blade_{i}"), Is.Not.Null);

                var renderers = root.GetComponentsInChildren<Renderer>(true);
                // Surface details are added by the common loader after the source kit.
                // Keep this silhouette guard about the 45 original model parts.
                var sourceParts = renderers.Count(r => !r.name.StartsWith(AircraftSurfaceDetails.DetailName, StringComparison.Ordinal));
                Assert.That(sourceParts, Is.EqualTo(45));
                var bounds = renderers[0].bounds;
                for (var i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);
                Assert.That(bounds.size.x, Is.InRange(13.8f, 14.4f));
                Assert.That(bounds.size.z, Is.InRange(16.5f, 17.3f));
                Assert.That(bounds.size.y, Is.InRange(4.4f, 4.9f));
                Assert.That(bounds.size.x * 0.5f, Is.LessThan(AdelaideEmergencyAviationGeometry.PadRadiusMetres - 10f),
                    "the parked rotor stays well inside the 37.9 m pad");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent.gameObject);
            }
        }
    }
}
