using Airside.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Airside.Tests
{
    /// <summary>Unity-side regressions for the 2026-09 presentation bug sweep.</summary>
    public sealed class PresentationBugSweepTests
    {
        [Test]
        public void WetConcreteAlbedo_OnlyAcceptsConcreteDryMaps()
        {
            var concrete = new Texture2D(2, 2) { name = "tx_concrete_apron_basecolor_v03" };
            var asphalt = new Texture2D(2, 2) { name = "tx_asphalt_runway_basecolor_v03" };
            try
            {
                Assert.That(AirsideMaterialLibrary.AcceptsWetConcreteAlbedo(concrete), Is.True);
                Assert.That(AirsideMaterialLibrary.AcceptsWetConcreteAlbedo(asphalt), Is.False);
                Assert.That(AirsideMaterialLibrary.AcceptsWetConcreteAlbedo(null), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(concrete);
                Object.DestroyImmediate(asphalt);
            }
        }

        [Test]
        public void WetConcreteAlbedo_ClearWeatherDampDoesNotSwap()
        {
            // UpdateWeatherPresentation applies 0.14 to paved slabs in clear weather and
            // 0.52 / 0.72 in rain / storm.
            Assert.That(0.14f, Is.LessThan(AirsideMaterialLibrary.WetConcreteAlbedoThreshold));
            Assert.That(0.52f, Is.GreaterThanOrEqualTo(AirsideMaterialLibrary.WetConcreteAlbedoThreshold));
        }

        [Test]
        public void ApplyWetness_RestoresDryAlbedoWhenRainClears()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader);
            var dry = new Texture2D(2, 2) { name = "tx_concrete_apron_basecolor_v03" };
            var other = new Texture2D(2, 2) { name = "stand_in_wet" };
            try
            {
                material.mainTexture = other;
                AirsideMaterialLibrary.ApplyWetness(material, 0.14f, Color.grey, 0.18f,
                    preferWetConcreteAlbedo: true, dryAlbedo: dry);
                Assert.That(material.mainTexture, Is.SameAs(dry));
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(dry);
                Object.DestroyImmediate(other);
            }
        }

        [TestCase("flood_cable_tray", AirsideMaterialLibrary.SurfaceKind.Rubber)]
        [TestCase("mudflap_l", AirsideMaterialLibrary.SurfaceKind.Rubber)]
        [TestCase("taillight_r", AirsideMaterialLibrary.SurfaceKind.Plastic)]
        [TestCase("pump_cabinet", AirsideMaterialLibrary.SurfaceKind.PaintedMetal)]
        [TestCase("service_wing_roof", AirsideMaterialLibrary.SurfaceKind.Metal)]
        [TestCase("shed_body", AirsideMaterialLibrary.SurfaceKind.Metal)]
        [TestCase("terminal_body", AirsideMaterialLibrary.SurfaceKind.Concrete)]
        [TestCase("tug_cab", AirsideMaterialLibrary.SurfaceKind.AircraftSkin)]
        [TestCase("Fuselage", AirsideMaterialLibrary.SurfaceKind.AircraftSkin)]
        [TestCase("tree_a_trunk", AirsideMaterialLibrary.SurfaceKind.Default)]
        [TestCase("rock_b", AirsideMaterialLibrary.SurfaceKind.Default)]
        [TestCase("tree_b_canopy", AirsideMaterialLibrary.SurfaceKind.Grass)]
        public void InferFromMeshName_AvoidsSkinSubstringTraps(string mesh, AirsideMaterialLibrary.SurfaceKind expected)
        {
            Assert.That(AirsideMaterialLibrary.InferFromMeshName(mesh), Is.EqualTo(expected));
        }

        [Test]
        public void FleetAircraft_OnTheGround_KeepLandingLightsOffAndFlapsUpOnStand()
        {
            Assert.That(AirsideReusableMotion.LandingLightsOn(Airside.Simulation.AircraftPhase.AtStand, 1f, drawnOnGround: true), Is.False);
            Assert.That(AirsideReusableMotion.LandingLightsOn(Airside.Simulation.AircraftPhase.TaxiIn, 0.5f, drawnOnGround: true), Is.False);
            Assert.That(AirsideReusableMotion.LandingLightsOn(Airside.Simulation.AircraftPhase.Approach, 0.5f, drawnOnGround: true), Is.True);
            Assert.That(AirsideReusableMotion.FlapDegrees(Airside.Simulation.AircraftPhase.AtStand, 1f, drawnOnGround: true), Is.EqualTo(0f));
            Assert.That(AirsideReusableMotion.FlapDegrees(Airside.Simulation.AircraftPhase.TaxiOut, 0.5f, drawnOnGround: true), Is.EqualTo(12f));
        }

        [Test]
        public void WaterMaterial_IsTranslucentNotWetAsphalt()
        {
            var material = AirsideMaterialLibrary.Create(new Color(0.2f, 0.4f, 0.5f), AirsideMaterialLibrary.SurfaceKind.Water);
            try
            {
                Assert.That(material.renderQueue, Is.GreaterThanOrEqualTo(3000), "water is transparent");
                var map = material.mainTexture;
                if (map != null)
                    Assert.That(map.name, Does.Not.Contain("asphalt"));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void AuthoredTemplate_HonoursATranslucentColour()
        {
            var material = AirsideMaterialLibrary.Create(new Color(0.46f, 0.47f, 0.48f, 0.4f), AirsideMaterialLibrary.SurfaceKind.Concrete);
            try
            {
                Assert.That(material.renderQueue, Is.GreaterThanOrEqualTo(3000));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }
    }
}
