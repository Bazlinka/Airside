using Airside.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using System.Reflection;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Airside.Tests
{
    public sealed class WorldLightingTests
    {
        [Test]
        public void EqualAlbedoGroundRoadAndPavementHaveMatchingRenderedBrightness()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Offscreen brightness requires native graphics; run Unity without -nographics.");
            var previousProbe = RenderSettings.ambientProbe;
            var previousFog = RenderSettings.fog;
            var probe = new SphericalHarmonicsL2();
            probe.AddAmbientLight(new Color(.3f, .3f, .3f));
            RenderSettings.ambientProbe = probe;
            RenderSettings.fog = false;
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            var cameraObject = new GameObject("World material regression camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 300, 0);
            camera.transform.rotation = Quaternion.Euler(90, 0, 0);
            camera.orthographic = true;
            camera.orthographicSize = 2;
            camera.farClipPlane = 500;
            var target = new RenderTexture(32, 32, 24);
            camera.targetTexture = target;
            var image = new Texture2D(32, 32, TextureFormat.RGB24, false);
            var materials = new[] { new Material(Shader.Find("Airside/AdelaideGround")),
                new Material(Shader.Find("Airside/Surroundings")), new Material(Shader.Find("Airside/Pavement")) };
            var map = new Texture2D(1, 1);
            var colour = new Color(.3f, .3f, .3f);
            map.SetPixel(0, 0, colour); map.Apply();
            var mesh = Object.Instantiate(plane.GetComponent<MeshFilter>().sharedMesh);
            plane.GetComponent<MeshFilter>().sharedMesh = mesh;
            try
            {
                materials[0].SetTexture("_DryAlbedo", map);
                materials[0].SetColor("_Tint", Color.white);
                materials[0].SetFloat("_SatelliteStrength", 0);
                materials[0].SetFloat("_MacroStrength", 0);
                materials[0].SetFloat("_MownStripeStrength", 0);
                materials[0].SetFloat("_BumpScale", 0);
                materials[1].SetFloat("_VertexSurface", 1);
                materials[2].SetColor("_BaseColor", colour);
                materials[2].SetFloat("_ScanContrast", 0);
                materials[2].SetFloat("_MacroStrength", 0);
                materials[2].SetFloat("_PatchStrength", 0);
                materials[2].SetFloat("_BumpScale", 0);
                float? reference = null;
                for (var index = 0; index < materials.Length; index++)
                {
                    materials[index].SetFloat("_Smoothness", .08f);
                    var colors = new Color[mesh.vertexCount];
                    for (var vertex = 0; vertex < colors.Length; vertex++)
                        colors[vertex] = index == 0 ? new Color(1, 0, 0, 1) : colour.linear;
                    mesh.colors = colors;
                    plane.GetComponent<Renderer>().sharedMaterial = materials[index];
                    camera.Render(); camera.Render();
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, 32, 32), 0, 0); image.Apply();
                    var brightness = image.GetPixel(16, 16).grayscale;
                    Assert.That(brightness, Is.GreaterThan(.01f), "Black/error output is not a lighting match");
                    if (reference.HasValue)
                        Assert.That(brightness, Is.EqualTo(reference.Value).Within(.035f), materials[index].shader.name);
                    else reference = brightness;
                }
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = null;
                foreach (var material in materials) Object.DestroyImmediate(material);
                Object.DestroyImmediate(map); Object.DestroyImmediate(image); Object.DestroyImmediate(target);
                Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(plane); Object.DestroyImmediate(mesh);
                RenderSettings.ambientProbe = previousProbe; RenderSettings.fog = previousFog;
            }
        }

        [Test]
        public void DayGradeIsNeutralAndNightExposureRemainsReadable()
        {
            var root = new GameObject("World grading regression");
            try
            {
                var day = AirsideDayVolume.Ensure(root.transform);
                var grade = typeof(AirsideDayVolume).GetField("_color", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(day);
                day.Apply(1, 0);
                Assert.That(GradeValue(grade, "postExposure"), Is.EqualTo(0).Within(.001f));
                Assert.That(GradeValue(grade, "saturation"), Is.EqualTo(0).Within(.001f));
                day.Apply(0, 0);
                Assert.That(GradeValue(grade, "postExposure"), Is.EqualTo(.06f).Within(.001f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static float GradeValue(object grade, string field)
        {
            var parameter = grade.GetType().GetField(field).GetValue(grade);
            return (float)parameter.GetType().GetProperty("value").GetValue(parameter);
        }

#if UNITY_EDITOR
        [Test]
        public void WorldShadersCompileInNativeUnity()
        {
            foreach (var name in new[] { "Airside/Pavement", "Airside/AdelaideGround", "Airside/Surroundings" })
            {
                var shader = Shader.Find(name);
                Assert.That(shader, Is.Not.Null, name);
                Assert.That(ShaderUtil.ShaderHasError(shader), Is.False, name);
            }
        }
#endif
    }
}
