using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Native fixture captures of runtime aircraft lights. Not a packaged gameplay playtest.</summary>
public static class AircraftLightingReview
{
    public static void Render()
    {
        var output = Path.GetFullPath("../../work/aircraft-lighting-review");
        Directory.CreateDirectory(output);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        ShaderUtil.allowAsyncCompilation = false;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.10f, 0.13f, 0.20f);
        var key = new GameObject("Night fill").AddComponent<Light>();
        key.type = LightType.Directional; key.intensity = 0.18f;
        key.color = new Color(0.6f, 0.7f, 1f); key.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.transform.localScale = Vector3.one * 60f;
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.color = new Color(0.16f, 0.18f, 0.21f);
        ground.GetComponent<Renderer>().sharedMaterial = material;
        var camera = new GameObject("Lighting camera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.015f, 0.024f, 0.045f);
        camera.fieldOfView = 42f;
        camera.farClipPlane = 10000f;
        var target = new RenderTexture(1400, 900, 24);
        camera.targetTexture = target;
        var proto = typeof(AirsidePrototype);
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        var partsType = proto.GetNestedType("AircraftViewParts", BindingFlags.NonPublic);
        foreach (var spec in AircraftCatalogue.All.Concat(AircraftCatalogue.Rotorcraft))
        {
            var root = (Transform)proto.GetMethod("BuildAircraftForType", flags)
                .Invoke(null, new object[] { "Review " + spec.Id, spec.Type, new Color(0.1f, 0.25f, 0.7f), null });
            root.position = Vector3.up * AirsideFlightPath.GroundY;
            foreach (var lod in root.GetComponentsInChildren<LODGroup>()) lod.ForceLOD(0);
            var args = new object[] { AirsideNamedChildren.Get(root), AirsideNamedChildren.Names(root), Activator.CreateInstance(partsType) };
            proto.GetMethod("ClassifyAnimatedParts", flags).Invoke(null, args);
            var lamps = partsType.GetField("LightsAndGear").GetValue(args[2]);
            var bounds = new Bounds(root.position, Vector3.zero);
            foreach (var r in root.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            foreach (var shot in new[] { "taxi", "takeoff", "rear", "parked", "beam" })
            {
                var distance = Mathf.Max(32f, bounds.size.magnitude * 1.2f);
                if (shot == "beam") distance = AircraftLightingProfile.For(spec.Type).LandingRange * 1.2f;
                var offset = shot == "beam" ? new Vector3(-0.3f, 0.5f, 0.85f) : shot == "rear" ? new Vector3(-0.5f, 0.23f, -0.85f) : new Vector3(-0.5f, 0.23f, 0.85f);
                camera.transform.position = bounds.center + offset.normalized * distance;
                camera.transform.LookAt(bounds.center + Vector3.forward * (shot == "beam" ? AircraftLightingProfile.For(spec.Type).LandingRange * 0.3f : shot == "rear" ? 0f : 8f));
                var phase = shot == "taxi" ? AircraftPhase.TaxiOut : shot == "parked" ? AircraftPhase.AtStand : AircraftPhase.Takeoff;
                proto.GetMethod("UpdateAircraftLightsAndGear", flags).Invoke(null,
                    new object[] { lamps, phase, 0.1f, 0f, 0f, (shot == "taxi" ? 0.25f / AircraftLightingProfile.For(spec.Type).BeaconHz : shot == "rear" || shot == "beam" ? 0.10f : 0.02f) - AircraftLightingProfile.ClockOffsetSeconds(root.name), shot == "parked" ? EngineState.ColdAndOpen : EngineState.Running, null, spec.Type });
                camera.Render(); camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(output, spec.Id + "_" + shot + ".png"), image.EncodeToPNG());
                Object.DestroyImmediate(image);
            }
            Object.DestroyImmediate(root.gameObject);
            Debug.Log("Lighting review captured " + spec.Id);
        }
        RenderTexture.active = null;
        camera.targetTexture = null;
        Object.DestroyImmediate(target);
        Debug.Log("Aircraft lighting review: " + output);
    }
}
