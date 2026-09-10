using NUnit.Framework;
using Airside.Presentation;
using UnityEngine;

namespace Airside.Tests
{
    public sealed class AircraftAssetTests
    {
        [Test]
        public void Atr42StarterPrefab_MatchesOfficialDimensionsAndHasRenderableMeshes()
        {
            var prefab = Resources.Load<GameObject>(
                "Airside/Prefabs/mdl_atr42_starter_v01");

            Assert.That(prefab, Is.Not.Null, "the final ATR 42 starter must be packaged as a Resources prefab");
            var renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
            Assert.That(renderers.Length, Is.GreaterThan(155), "the final asset should retain its named presentation parts");

            var filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            Assert.That(filters.Length, Is.EqualTo(renderers.Length));
            var bounds = filters[0].sharedMesh.bounds;
            for (var i = 1; i < filters.Length; i++)
                bounds.Encapsulate(filters[i].sharedMesh.bounds);

            Assert.That(bounds.size.x, Is.EqualTo(24.57f).Within(0.03f), "wingspan must match the ATR 42-600");
            Assert.That(bounds.size.y, Is.EqualTo(7.59f).Within(0.03f), "height must match the ATR 42-600");
            Assert.That(bounds.size.z, Is.EqualTo(22.67f).Within(0.03f), "length must match the ATR 42-600");
            Assert.That(bounds.min.y, Is.InRange(-0.03f, 0.04f), "all six tires should meet local ground y=0");
        }

        [Test]
        public void Atr42StarterPrefab_HasRestrainedCompleteArticulationSet()
        {
            var prefab = Resources.Load<GameObject>(
                "Airside/Prefabs/mdl_atr42_starter_v01");
            Assert.That(prefab, Is.Not.Null);

            var transforms = prefab.GetComponentsInChildren<Transform>(true);
            Assert.That(System.Array.Exists(transforms, t => t.name == "flap_left"), Is.True);
            Assert.That(System.Array.Exists(transforms, t => t.name == "flap_right"), Is.True);
            Assert.That(System.Array.Exists(transforms, t => t.name == "aileron_left"), Is.True);
            Assert.That(System.Array.Exists(transforms, t => t.name == "aileron_right"), Is.True);
            Assert.That(System.Array.Exists(transforms, t => t.name == "elevator_left"), Is.True);
            Assert.That(System.Array.Exists(transforms, t => t.name == "elevator_right"), Is.True);
            Assert.That(System.Array.Exists(transforms, t => t.name == "rudder"), Is.True);
            Assert.That(System.Array.Exists(transforms, t => t.name == "spoiler_left"), Is.True);
            Assert.That(System.Array.Exists(transforms, t => t.name == "spoiler_right"), Is.True);
            Assert.That(System.Array.Exists(transforms, t => t.name == "door_fwd"), Is.True);
            Assert.That(System.Array.Exists(transforms, t => t.name == "cargo_door"), Is.True);
            Assert.That(System.Array.Exists(transforms, t => t.name == "gear_nose"), Is.True);
            Assert.That(System.Array.Exists(transforms, t => t.name == "gear_left"), Is.True);
            Assert.That(System.Array.Exists(transforms, t => t.name == "gear_right"), Is.True);

            Assert.That(System.Array.FindAll(transforms, t => t.name.StartsWith("tire_")).Length,
                Is.EqualTo(6), "ATR undercarriage should have twin nose and tandem main wheels");
            Assert.That(System.Array.FindAll(transforms,
                    t => t.name.StartsWith("propeller_left") && !t.name.Contains("tip")).Length,
                Is.EqualTo(6), "left propeller should have six authored blades");
            Assert.That(System.Array.FindAll(transforms,
                    t => t.name.StartsWith("propeller_right") && !t.name.Contains("tip")).Length,
                Is.EqualTo(6), "right propeller should have six authored blades");
        }

        [Test]
        public void Atr42StarterPresentation_NoUvMeshUsesBrightFlatAircraftMaterial()
        {
            var material = AirsideMaterialLibrary.CreateShared(
                new Color(0.93f, 0.95f, 0.97f),
                AirsideMaterialLibrary.SurfaceKind.AircraftSkin,
                useTextures: false);
            try
            {
                Assert.That(material.mainTexture, Is.Null,
                    "a mesh without UVs must not sample an authored texture at UV 0,0");
                var color = material.HasProperty("_BaseColor")
                    ? material.GetColor("_BaseColor")
                    : material.color;
                Assert.That(color.grayscale, Is.GreaterThan(0.85f));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }
    }
}
