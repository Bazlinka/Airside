using NUnit.Framework;
using Airside.Presentation;
using UnityEngine;

namespace Airside.Tests
{
    public sealed class AircraftAssetTests
    {
        [Test]
        public void Air001V06Prefab_HasFlightScaleBoundsAndRenderableMeshes()
        {
            var prefab = Resources.Load<GameObject>(
                "Airside/Prefabs/mdl_regional_turboprop_01_v06");

            Assert.That(prefab, Is.Not.Null, "v06 must be packaged as a Resources prefab");
            var renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
            Assert.That(renderers.Length, Is.GreaterThan(100), "v06 should retain its named presentation parts");

            var filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            Assert.That(filters.Length, Is.EqualTo(renderers.Length));
            var bounds = filters[0].sharedMesh.bounds;
            for (var i = 1; i < filters.Length; i++)
                bounds.Encapsulate(filters[i].sharedMesh.bounds);

            Assert.That(bounds.size.x, Is.InRange(14.5f, 15.5f), "wingspan must remain metre scale");
            Assert.That(bounds.size.y, Is.InRange(3.5f, 4.3f), "height must remain metre scale");
            Assert.That(bounds.size.z, Is.InRange(10.0f, 11.2f), "length must remain metre scale");
            Assert.That(bounds.min.y, Is.GreaterThan(-0.15f), "gear should meet the ground near local y=0");
        }

        [Test]
        public void Air001V06Presentation_NoUvMeshUsesBrightFlatAircraftMaterial()
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
