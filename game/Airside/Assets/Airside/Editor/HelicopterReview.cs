using System;
using System.IO;
using System.Reflection;
using Airside.Domain;
using Airside.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>
/// Repeatable photographs of the Bell 412 rig (ADR 0207): the runtime builder's output at points along a
/// take-off and landing, with the rotor spun the way the game spins it.
///   -executeMethod HelicopterReview.Render [-helicopterOutput dir] [-helicopterViews side,front,top]
/// </summary>
public static class HelicopterReview
{
    private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;

    public static void Render()
    {
        var args = Environment.GetCommandLineArgs();
        string Arg(string key, string fallback) { var i = Array.IndexOf(args, key); return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback; }
        var output = Path.GetFullPath(Arg("-helicopterOutput", "../../work/helicopter-review"));
        var views = Arg("-helicopterViews", "side,front,top").Split(',');
        Directory.CreateDirectory(output);
        ShaderUtil.allowAsyncCompilation = false;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.70f, 0.77f, 0.85f);
        RenderSettings.ambientEquatorColor = new Color(0.52f, 0.58f, 0.64f);
        RenderSettings.ambientGroundColor = new Color(0.27f, 0.29f, 0.30f);
        var sun = new GameObject("Review sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
        sun.intensity = 0.9f;
        var camera = new GameObject("Review camera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.30f, 0.38f, 0.46f);
        camera.orthographic = true;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 300f;
        var target = new RenderTexture(1200, 800, 24) { antiAliasing = 4 };
        camera.targetTexture = target;

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.transform.localScale = new Vector3(6f, 1f, 6f);
        ground.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            { color = new Color(0.45f, 0.47f, 0.47f) };

        var proto = typeof(Airside.Presentation.AirsidePrototype);
        ColorUtility.TryParseHtmlString("#C4161C", out var accent);
        var root = (Transform)proto.GetMethod("BuildAircraftForType", PrivateStatic)
            .Invoke(null, new object[] { "Review B412", AircraftType.Bell412, accent, null });
        foreach (var lod in root.GetComponentsInChildren<LODGroup>()) lod.ForceLOD(0);
        var main = root.Find("Main rotor");
        var tail = root.Find("Tail rotor");
        Debug.Log("Bell rig: main rotor " + (main != null ? main.childCount + " children" : "MISSING")
                  + ", tail rotor " + (tail != null ? tail.childCount + " children" : "MISSING")
                  + ", disc " + (main != null && main.Find("Rotor disc") != null));
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
            if (r.name.Contains("shadow", StringComparison.OrdinalIgnoreCase)) r.enabled = false;

        var profile = RotorcraftPerformance.Bell412;
        var poses = new (string name, HelicopterPose pose, float parkedToFlight)[]
        {
            ("1-parked", HelicopterFlight.OnPad(0f), 0f),
            ("2-hover", HelicopterFlight.TakeoffPose(profile, profile.LiftSeconds + 1f), 0f),
            ("3-climb", HelicopterFlight.TakeoffPose(profile, profile.TakeoffSeconds - 8f), 0f),
            ("4-approach", HelicopterFlight.LandingPose(profile, 8f), 0f),
            ("5-flare", HelicopterFlight.LandingPose(profile, profile.DecelSeconds - 4f), 0f),
        };
        foreach (var (name, pose, _) in poses)
        {
            root.position = new Vector3(0f, pose.HeightMetres, 0f);
            root.rotation = Quaternion.Euler(pose.PitchDegrees, 0f, 0f);
            // Spin as the game does: stopped when parked, 330 rpm otherwise, with the blur disc fading in.
            var speed = name == "1-parked" ? 0f : 1f;
            if (main != null) main.localRotation = Quaternion.Euler(0f, 37f * speed, 0f);
            if (tail != null) tail.localRotation = Quaternion.Euler(61f * speed, 0f, 0f);
            SetDisc(main, "Rotor disc", speed * 0.34f);
            SetDisc(tail, "Tail rotor disc", speed * 0.4f);

            var bounds = new Bounds(root.position + Vector3.up * 2.4f, new Vector3(16f, 6.5f, 22f));
            foreach (var view in views)
            {
                var (yaw, pitch) = view switch { "side" => (90f, 4f), "front" => (0f, 8f), _ => (30f, 80f) };
                var rotation = Quaternion.Euler(-pitch, -yaw, 0f);
                camera.transform.position = bounds.center + rotation * Vector3.forward * 60f;
                camera.transform.LookAt(bounds.center);
                camera.orthographicSize = view == "front" ? 6.5f : 11.5f;
                camera.Render();
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                File.WriteAllBytes(Path.Combine(output, "B412_" + name + "_" + view + ".png"), image.EncodeToPNG());
                Object.DestroyImmediate(image);
            }
        }

        RenderTexture.active = null;
        camera.targetTexture = null;
        Object.DestroyImmediate(target);
        Debug.Log("Helicopter review: " + output);
    }

    private static void SetDisc(Transform rotor, string name, float alpha)
    {
        var disc = rotor != null ? rotor.Find(name) : null;
        if (disc == null) return;
        disc.gameObject.SetActive(alpha > 0.01f);
        var renderer = disc.GetComponent<Renderer>();
        var block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        block.SetColor("_BaseColor", new Color(0.72f, 0.74f, 0.78f, alpha));
        renderer.SetPropertyBlock(block);
    }
}
