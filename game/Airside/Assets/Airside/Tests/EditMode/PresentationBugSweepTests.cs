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

        [Test]
        public void DayVolumeGrading_FadesInWithoutAStep()
        {
            Assert.That(AirsideDayVolume.NoonPunchWeight(0.749f, 0f), Is.EqualTo(AirsideDayVolume.NoonPunchWeight(0.751f, 0f)).Within(0.05f));
            Assert.That(AirsideDayVolume.NoonPunchWeight(0.9f, 0f), Is.EqualTo(1f));
            Assert.That(AirsideDayVolume.NoonPunchWeight(0.5f, 0f), Is.EqualTo(0f));
            Assert.That(AirsideDayVolume.GoldenBloomWeight(0.349f), Is.EqualTo(AirsideDayVolume.GoldenBloomWeight(0.351f)).Within(0.05f));
        }
    
        [Test]
        public void CameraKeyboardPan_ScalesWithZoomAndLiftIsBounded()
        {
            Assert.That(AirsideCameraController.KeyboardPanMetresPerSecond(AirsideBareField.MinOrbitDistance),
                Is.LessThan(AirsideBareField.OverviewPanMetresPerSecond * 0.25f));
            Assert.That(AirsideCameraController.KeyboardPanMetresPerSecond(AirsideBareField.MaxOrbitDistance),
                Is.GreaterThan(AirsideBareField.OverviewPanMetresPerSecond));
            Assert.That(AirsideCameraController.ClampCentreHeight(-400f), Is.EqualTo(AirsideCameraController.MinCentreHeightMetres));
            Assert.That(AirsideCameraController.ClampCentreHeight(9000f), Is.EqualTo(AirsideCameraController.MaxCentreHeightMetres));
        }
    
        [Test]
        public void MiniMapDots_DrawSelectionAndOwnAircraftLast()
        {
            Assert.That(AirsidePrototype.MiniMapDotPass(mine: false, selected: false), Is.LessThan(AirsidePrototype.MiniMapDotPass(mine: true, selected: false)));
            Assert.That(AirsidePrototype.MiniMapDotPass(mine: true, selected: false), Is.LessThan(AirsidePrototype.MiniMapDotPass(mine: false, selected: true)));
        }
    
        [Test]
        public void WeatherGloom_EasesBetweenForecastKinds()
        {
            var storm = AirsidePrototype.WeatherGloomTarget(Airside.Simulation.WeatherKind.Storm);
            Assert.That(AirsidePrototype.EaseWeatherGloom(0f, storm, 1f / 60f), Is.LessThan(0.01f));
            Assert.That(AirsidePrototype.EaseWeatherGloom(0f, storm, float.PositiveInfinity), Is.EqualTo(storm));
        }
    
        [Test]
        public void StarField_FadesOutRatherThanBlinkingOff()
        {
            Assert.That(AirsidePrototype.StarFieldFade(0f), Is.GreaterThan(1f));
            Assert.That(AirsidePrototype.StarFieldFade(0.42f), Is.EqualTo(0f));
            Assert.That(AirsidePrototype.StarFieldFade(0.34f), Is.LessThan(0.2f));
            Assert.That(AirsidePrototype.StarFieldFade(0.2f), Is.GreaterThan(AirsidePrototype.StarFieldFade(0.3f)));
        }
    
        [Test]
        public void CombinedKitKey_SeparatesColoursInEveryCulture()
        {
            var previous = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
                var a = ArtGltfLoader.TestCombinedKey("kit", new[] { ("body", new Color(0.5f, 0.5f, 0.5f, 1f)) }, null);
                var b = ArtGltfLoader.TestCombinedKey("kit", new[] { ("body", new Color(0.5f, 0.50001f, 0.5f, 1f)) }, null);
                Assert.That(a, Does.Not.Contain("0,5"));
                Assert.That(a, Is.Not.EqualTo(b));
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = previous;
            }
        }
    
        [Test]
        public void SavedClock_FallsBackWhenTheTicksAreCorrupt()
        {
            var data = new Airside.Simulation.AirlineSaveData { ClockSeconds = 120, SavedAtUtcTicks = long.MaxValue };
            Assert.DoesNotThrow(() => Airside.Simulation.AirlineSave.ClockFor(data));
            Assert.That(Airside.Simulation.AirlineSave.ClockFor(data).EpochUtcTicks,
                Is.EqualTo(Airside.Domain.AirlineClock.Default.EpochUtcTicks));
            Assert.That(Airside.Simulation.AirlineSave.IsRealDate(-5), Is.False);
        }
    
        [Test]
        public void SceneIndex_ForgetsDestroyedObjects()
        {
            var go = new GameObject("bug-sweep-probe");
            AirsideSceneIndex.Remember(go);
            Object.DestroyImmediate(go);
            Assert.That(AirsideSceneIndex.Find("bug-sweep-probe"), Is.Null);
            Assert.DoesNotThrow(() => AirsideSceneIndex.FindGameObject("bug-sweep-probe"));
            Assert.That(AirsideSceneIndex.FindGameObject("bug-sweep-probe"), Is.Null);
        }
    
        [Test]
        public void AwaySummary_SurvivesASaveWithNoFleetArray()
        {
            var clock = new Airside.Simulation.ManualSimulationClock(new Airside.Domain.SimulationTime(0));
            var ops = Airside.Simulation.AirlineOperations.StartAtAdelaide(
                clock, new Airside.Simulation.SeededRandomSource(31),
                Airside.Domain.Airline.Player("Bight Air", "#1F3A93"));

            // A save with no fleet array at all (an older build, or hand-edited) used to throw.
            var summary = Airside.Simulation.AwaySummary.Build(
                new Airside.Simulation.AirlineSaveData(), ops, 3600);
            Assert.That(summary.Lines, Is.Not.Empty);
        }
    }
}
