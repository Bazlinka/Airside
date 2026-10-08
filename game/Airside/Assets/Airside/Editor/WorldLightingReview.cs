using System;
using System.IO;
using System.Reflection;
using System.Threading;
using Airside.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Controlled native views of real airport meshes, not a packaged player soak.</summary>
public static class WorldLightingReview
{
    public static void Render()
    {
        ShaderUtil.allowAsyncCompilation = false;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var args = Environment.GetCommandLineArgs();
        var index = Array.IndexOf(args, "-worldLightingOutput");
        var output = index >= 0 ? args[index + 1] : "../../work/world-lighting";
        Directory.CreateDirectory(output);
        var root = new GameObject("World lighting reference").transform;
        typeof(AirsidePrototype).GetField("_airfieldRoot", BindingFlags.Static | BindingFlags.NonPublic)
            .SetValue(null, root);
        AirsideAdelaideGroundMesh.TryBuild(root);
        AirsideAdelaideSurroundings.TryBuild(root);
        typeof(AirsidePrototype).GetMethod("BuildBareAdelaidePavement", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, null);
        var roads = AirsideAdelaideRoadNetworkMesh.BuildAsync(root, AirsideAdelaideGround.PavementWorldY,
            AirsideAdelaideSurroundings.RoadHeight(AirsideAdelaideGround.PavementWorldY));
        while (roads.MoveNext()) Thread.Sleep(1);
        var sun = new GameObject("World sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.shadows = LightShadows.Soft;
        RenderSettings.sun = sun;
        var camera = new GameObject("World camera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.orthographic = true;
        camera.nearClipPlane = .5f;
        camera.farClipPlane = 30000;
        camera.allowHDR = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        var volume = AirsideDayVolume.Ensure(root);
        var target = new RenderTexture(1600, 1000, 24) { antiAliasing = 4 };
        camera.targetTexture = target;
        foreach (var phase in new[] { ("day", 1f, 0f), ("dusk", .3f, .75f), ("night", 0f, 0f) })
        {
            sun.intensity = Mathf.Lerp(.30f, 1.25f, phase.Item2);
            sun.color = Color.Lerp(new Color(.32f, .38f, .55f), Color.white, phase.Item2);
            sun.transform.rotation = Quaternion.Euler(Mathf.Lerp(8, 52, phase.Item2), -35, 0);
            var ambient = Color.Lerp(new Color(.28f, .32f, .42f), new Color(.42f, .43f, .44f), phase.Item2);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambient;
            var probe = new SphericalHarmonicsL2();
            probe.AddAmbientLight(ambient);
            RenderSettings.ambientProbe = probe;
            RenderSettings.fog = false;
            camera.backgroundColor = ambient * .65f;
            volume.Apply(phase.Item2, phase.Item3);
            foreach (var view in new[] { ("overview", new Vector3(2600, 3200, -3400), Vector3.zero, 2900f),
                ("apron", new Vector3(1600, 520, 2050), new Vector3(1100, 0, 750), 700f) })
            {
                camera.transform.position = view.Item2;
                camera.transform.LookAt(view.Item3);
                camera.orthographicSize = view.Item4;
                camera.Render(); camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                File.WriteAllBytes(Path.Combine(output, phase.Item1 + "-" + view.Item1 + ".png"), image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
            }
        }
        camera.targetTexture = null;
        RenderTexture.active = null;
        UnityEngine.Object.DestroyImmediate(target);
        Debug.Log("World lighting native views: " + Path.GetFullPath(output));
    }
}
