using System;
using System.IO;
using System.Reflection;
using Airside.Domain;
using Airside.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Native type-specific interior stills. Geometry evidence only, not a packaged journey.</summary>
public static class CockpitAppearanceReview
{
    private static string _typeOverride;
    public static void RunAllJets()
    {
        try
        {
            foreach (var profile in JetCockpitProfile.All)
            { _typeOverride = profile.TypeId; Run(); }
        }
        finally { _typeOverride = null; }
    }

    public static void Run()
    {
        var args = Environment.GetCommandLineArgs();
        var index = Array.IndexOf(args, "-cockpitReviewOutput");
        var output = Path.GetFullPath(index >= 0 ? args[index + 1] : "../../work/cockpit-review");
        Directory.CreateDirectory(output);
        ShaderUtil.allowAsyncCompilation = false;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.7f, 0.77f, 0.85f);
        RenderSettings.ambientEquatorColor = new Color(0.52f, 0.58f, 0.64f);
        RenderSettings.ambientGroundColor = new Color(0.27f, 0.29f, 0.3f);
        RenderSettings.fog = false;
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
        sun.intensity = 1.2f;
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.transform.position = new Vector3(0f, -0.15f, 0f);
        ground.transform.localScale = new Vector3(1200f, 0.2f, 1800f);
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.color = new Color(0.28f, 0.32f, 0.22f);
        ground.GetComponent<Renderer>().sharedMaterial = material;
        var runway = GameObject.CreatePrimitive(PrimitiveType.Cube);
        runway.transform.position = new Vector3(0f, -0.04f, 300f);
        runway.transform.localScale = new Vector3(23f, 0.06f, 1000f);
        var asphalt = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        asphalt.color = new Color(0.13f, 0.15f, 0.16f);
        runway.GetComponent<Renderer>().sharedMaterial = asphalt;
        for (var z = 15f; z < 500f; z += 40f)
        {
            var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.transform.position = new Vector3(0f, 0f, z);
            line.transform.localScale = new Vector3(0.2f, 0.02f, 20f);
        }
        var typeIndex = Array.IndexOf(args, "-cockpitReviewType");
        var typeId = _typeOverride ?? (typeIndex >= 0 ? args[typeIndex + 1] : "SF34");
        if (!AircraftType.TryFromId(typeId, out var type) || !CockpitAvailability.Supported(type))
            throw new ArgumentException("Unsupported cockpit review type: " + typeId);
        var root = (Transform)typeof(AirsidePrototype).GetMethod("BuildAircraftForType", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { typeId + " review", type, Color.blue, null });
        root.position = new Vector3(0f, 0.7f, 0f);
        foreach (var lod in root.GetComponentsInChildren<LODGroup>()) lod.ForceLOD(0);
        CockpitInterior rig = type.Id == AircraftType.Saab340.Id
            ? SaabCockpitInterior.Build(root) : JetCockpitInterior.Build(root, type);
        rig.Enter();
        rig.SetReadout("GS 12 kt\nHEIGHT 0 ft\nHDG 050°");
        if (rig is JetCockpitInterior jet) jet.SetFlightState(root, Airside.Simulation.EngineState.Running, "REVIEW");
        var camera = new GameObject("Review camera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.57f, 0.74f, 0.85f);
        camera.fieldOfView = 65f;
        camera.nearClipPlane = 0.035f;
        camera.farClipPlane = 5000f;
        var target = new RenderTexture(1440, 900, 24) { antiAliasing = 4 };
        camera.targetTexture = target;
        foreach (var shot in new[] { ("forward", 0f, 0f), ("left", 0f, -65f), ("panel", 25f, 0f), ("right", 0f, 65f), ("overhead", -65f, 0f), ("layout", 5f, 0f), ("bank", 0f, 0f), ("footwell", 48f, 0f), ("left-down", 35f, -80f), ("right-down", 35f, 80f) })
        {
            root.rotation = shot.Item1 == "bank" ? Quaternion.Euler(-8f, 0f, 15f) : Quaternion.identity;
            camera.transform.SetPositionAndRotation(rig.Seat.position, rig.Seat.rotation * Quaternion.Euler(shot.Item2, shot.Item3, 0f));
            if (shot.Item1 == "layout")
                camera.transform.position = root.TransformPoint(rig.Seat.localPosition + rig.transform.localPosition + new Vector3(0.43f, 0f, -0.50f));
            camera.Render(); camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(output, typeId + "_" + shot.Item1 + ".png"), image.EncodeToPNG());
            Object.DestroyImmediate(image);
        }
        RenderTexture.active = null;
        camera.targetTexture = null;
        Object.DestroyImmediate(target);
        rig.Leave();
        Object.DestroyImmediate(root.gameObject);
        Object.DestroyImmediate(material);
        Object.DestroyImmediate(asphalt);
        Debug.Log("Cockpit native review: " + output);
    }
}
