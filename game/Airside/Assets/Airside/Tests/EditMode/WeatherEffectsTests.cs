using System.Reflection;
using Airside.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

namespace Airside.Tests
{
    public sealed class WeatherEffectsTests
    {
        [Test]
        public void Rain_UsesOneColliderFreeMeshInsteadOfHundredsOfObjects()
        {
            var build = typeof(AirsidePrototype).GetMethod("BuildRainRoot", BindingFlags.NonPublic | BindingFlags.Static);
            var root = (Transform)build.Invoke(null, null);
            var material = root.GetComponent<Renderer>().sharedMaterial;
            var mesh = root.GetComponent<MeshFilter>().sharedMesh;
            try
            {
                Assert.That(root.childCount, Is.Zero);
                Assert.That(root.GetComponentsInChildren<Collider>(), Is.Empty);
                Assert.That(root.gameObject.activeSelf, Is.False, "dry startup must not show rain");
                Assert.That(mesh.vertexCount, Is.EqualTo(768 * 4));
                Assert.That(mesh.triangles.Length, Is.EqualTo(768 * 6));
                Assert.That(root.GetComponent<Renderer>().shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
                Assert.That(material.shader.name, Is.EqualTo("Airside/WeatherRain"));
            }
            finally
            {
                Object.DestroyImmediate(root.gameObject);
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(material);
            }
        }

        [TestCase("Airside/WeatherVolume")]
        [TestCase("Airside/WeatherCeiling")]
        [TestCase("Airside/WeatherRain")]
        public void WeatherShaders_AreAvailableInThePlayerBuild(string name)
        {
            var shader = Shader.Find(name);
            Assert.That(shader, Is.Not.Null);
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            var shaders = settings.FindProperty("m_AlwaysIncludedShaders");
            var included = false;
            for (var i = 0; i < shaders.arraySize; i++)
                included |= shaders.GetArrayElementAtIndex(i).objectReferenceValue == shader;
            Assert.That(included, Is.True, "runtime Shader.Find needs inclusion or a build can silently lose the effect");
        }
    }
}
