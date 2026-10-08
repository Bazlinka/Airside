using System;
using System.IO;
using System.Reflection;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Repeatable photographs of the runtime aircraft builder, materials and paint.</summary>
public static class AircraftAppearanceReview
{
    private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;

    public static void Render()
    {
        var args = Environment.GetCommandLineArgs();
        string Arg(string key, string fallback) { var i = Array.IndexOf(args, key); return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback; }
        var output = Path.GetFullPath(Arg("-aircraftReviewOutput", "../../work/aircraft-review"));
        var only = Arg("-aircraftReviewTypes", "ATR42,SF34,DH8D,E190,A223,A320,B738,B38M,A21N,A359,A339,B789,B78X,B412").Split(',');
        Directory.CreateDirectory(output);
        ShaderUtil.allowAsyncCompilation = false;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.70f, 0.77f, 0.85f);
        RenderSettings.ambientEquatorColor = new Color(0.52f, 0.58f, 0.64f);
        RenderSettings.ambientGroundColor = new Color(0.27f, 0.29f, 0.30f);
        RenderSettings.fog = false;
        var sun = new GameObject("Review sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
        sun.intensity = 1.2f;
        sun.shadows = LightShadows.Soft;
        var camera = new GameObject("Review camera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.18f, 0.23f, 0.28f);
        camera.orthographic = true;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 500f;
        camera.allowHDR = false;
        var target = new RenderTexture(1440, 900, 24) { antiAliasing = 4 };
        camera.targetTexture = target;
        foreach (var id in only)
        {
            if (!AircraftType.TryFromId(id, out var type)) throw new InvalidOperationException("Unknown aircraft " + id);
            var hex = Arg("-aircraftReviewColour", "#0F8B8D");
            var title = Arg("-aircraftReviewAirline", "Coastline Regional");
            if (Arg("-aircraftReviewPalette", "custom") == "fleet")
            {
                if (id is "SF34" or "A223" or "B738" or "A339") { hex = "#B8742A"; title = "Emu Air"; }
                else if (id is "E190" or "B38M" or "A359" or "B78X") { hex = "#1F3A93"; title = "Southern Cross Link"; }
            }
            ColorUtility.TryParseHtmlString(hex, out var accent);
            var root = (Transform)typeof(AirsidePrototype).GetMethod("BuildAircraftForType", PrivateStatic)
                .Invoke(null, new object[] { "Review " + id, type, accent, null });
            var airline = Airline.Player(title, hex);
            var fleet = (FleetAircraft)Activator.CreateInstance(typeof(FleetAircraft), BindingFlags.Instance | BindingFlags.NonPublic,
                null, new object[] { "VH-ASH", airline, type, new StableId("REVIEW"), new SimulationTime(0) }, null);
            if (!type.IsRotorcraft)
                typeof(AirsidePrototype).GetMethod("EnsureAircraftIdentityMarkings", PrivateStatic, null,
                    new[]{typeof(Transform),typeof(FleetAircraft),typeof(Color)},null)
                    .Invoke(null, new object[] { root, fleet, accent });
            foreach (var lod in root.GetComponentsInChildren<LODGroup>()) lod.ForceLOD(0);
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
                if (r.name is "Fuselage" or "Wing L" or "Tail" or "Livery emblem")
                    Debug.Log(id + " finish " + r.name + ": " + r.sharedMaterial.name + " " + r.sharedMaterial.GetColor("_BaseColor"));
            var bounds = new Bounds(); var first = true;
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                if (renderer.name.Contains("shadow", StringComparison.OrdinalIgnoreCase)) { renderer.enabled = false; continue; }
                if (first) { bounds = renderer.bounds; first = false; } else bounds.Encapsulate(renderer.bounds);
            }
            // -aircraftReviewDoors open: swing every door as the boarding timeline does, so the hollow behind it shows.
            var doorBounds = Arg("-aircraftReviewDoors", "shut") == "open" ? OpenDoors(root) : (Bounds?)null;
            var viewList = new System.Collections.Generic.List<(string, float, float)>
                { ("front", 38f, 22f), ("side", 90f, 5f), ("opposite", -90f, 5f), ("rear", 145f, 28f), ("overview", 40f, 55f) };
            if (doorBounds.HasValue)
                viewList.Add(("door", doorBounds.Value.center.x < 0f ? 90f : -90f, 6f));
            foreach (var angle in viewList)
            {
                if (!Arg("-aircraftReviewViews", "front,side,opposite,rear,overview").Contains(angle.Item1)) continue;
                var rotation = Quaternion.Euler(-angle.Item3, -angle.Item2, 0f);
                var direction = rotation * Vector3.forward;
                var framed = angle.Item1 == "door" ? doorBounds.Value : bounds;
                camera.transform.position = framed.center + direction * (angle.Item1 == "door" ? 30f : 150f);
                camera.transform.LookAt(framed.center);
                var ext = framed.extents;
                var right = camera.transform.right; var up = camera.transform.up;
                float Extent(Vector3 axis) => Mathf.Abs(axis.x) * ext.x + Mathf.Abs(axis.y) * ext.y + Mathf.Abs(axis.z) * ext.z;
                camera.orthographicSize = Mathf.Max(Extent(up), Extent(right) / camera.aspect) * (angle.Item1 == "door" ? 1.6f : 1.14f);
                // Labels use the same side visibility tracker in normal play; invoke it here
                // because editor executeMethod does not run MonoBehaviour.LateUpdate.
                foreach (var behaviour in root.GetComponents<MonoBehaviour>())
                    behaviour.GetType().GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(behaviour, null);
                // First submission uploads new material constants on Metal. Discard it:
                // reading that warm-up frame made the first aircraft appear one colour.
                camera.Render();
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                File.WriteAllBytes(Path.Combine(output, id + "_" + angle.Item1 + ".png"), image.EncodeToPNG());
                Object.DestroyImmediate(image);
            }
            Object.DestroyImmediate(root.gameObject);
            Debug.Log("Aircraft review captured " + id);
        }
        RenderTexture.active = null;
        camera.targetTexture = null;
        Object.DestroyImmediate(target);
        Debug.Log("Aircraft appearance review: " + output);
    }

    /// <summary>Open the passenger and cargo doors the way UpdateCabinDoor does; returns the passenger door's bounds.</summary>
    private static Bounds? OpenDoors(Transform root)
    {
        Bounds? passenger = null;
        var show = typeof(AirsidePrototype).GetMethod("ShowDoorway", PrivateStatic);
        foreach (var door in root.GetComponentsInChildren<Transform>(true))
        {
            var isCabin = door.name.StartsWith("CabinDoor", StringComparison.Ordinal) && door.GetComponent<MeshFilter>() != null;
            var isCargo = door.name.StartsWith("Cargo door", StringComparison.Ordinal);
            if (!isCabin && !isCargo) continue;
            if (isCabin) passenger = door.GetComponent<Renderer>().bounds;
            var euler = door.localEulerAngles;
            var airstair = door.GetComponent("Airside.Presentation.AirstairDoor");
            if (airstair != null)
                euler.z = (float)airstair.GetType().GetField("OpenDegrees").GetValue(airstair);
            else
                euler.y = isCabin ? -85f : 70f;
            door.localEulerAngles = euler;
            var doorway = door.GetComponent<AircraftDoorway>();
            if (doorway != null) show.Invoke(null, new object[] { doorway.Shell, 1f });
        }
        return passenger;
    }
}
