using System;
using System.Collections.Generic;
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

/// <summary>
/// Repeatable photographs of the aircraft physical-animation pass. It builds each type with the real runtime
/// builder, classifies it with the real part classifier, then drives the real control-surface and gear passes
/// into a pose and renders it:
///   -articulationPose geardown wheels down and locked (the authored pose, for comparison)
///   -articulationPose gearup   wheels up and locked (circuit)
///   -articulationPose gearmid  gear half-way through its retraction
///   -articulationPose roll     rolling left with flaps set (control surfaces, from above and behind)
///   -articulationPose landing  flaps landing, ground spoilers up, gear down
/// Output goes to -articulationOutput (default work/articulation-review).
/// </summary>
public static class AircraftArticulationReview
{
    private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void Render()
    {
        var args = Environment.GetCommandLineArgs();
        string Arg(string key, string fallback) { var i = Array.IndexOf(args, key); return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback; }
        var output = Path.GetFullPath(Arg("-articulationOutput", "../../work/articulation-review"));
        var only = Arg("-aircraftReviewTypes", "ATR42,SF34,DH8D,E190,A223,A320,B738,B38M,A21N,A359,A339,B789,B78X").Split(',');
        var pose = Arg("-articulationPose", "gearup");
        var views = Arg("-aircraftReviewViews", pose is "roll" or "landing" ? "ahead,behind,top" : "side,front,under").Split(',');
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
        sun.intensity = 0.85f;
        var camera = new GameObject("Review camera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.18f, 0.23f, 0.28f);
        camera.orthographic = true;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 500f;
        var target = new RenderTexture(1440, 900, 24) { antiAliasing = 4 };
        camera.targetTexture = target;

        var proto = typeof(AirsidePrototype);
        var viewPartsType = proto.GetNestedType("AircraftViewParts", BindingFlags.NonPublic);
        var stateType = proto.GetNestedType("AircraftArticulationState", BindingFlags.NonPublic);
        foreach (var id in only)
        {
            if (!AircraftType.TryFromId(id, out var type)) throw new InvalidOperationException("Unknown aircraft " + id);
            ColorUtility.TryParseHtmlString("#1F3A93", out var accent);
            var root = (Transform)proto.GetMethod("BuildAircraftForType", PrivateStatic)
                .Invoke(null, new object[] { "Review " + id, type, accent, null });
            foreach (var lod in root.GetComponentsInChildren<LODGroup>()) lod.ForceLOD(0);

            // Classify exactly as the runtime does.
            var children = AirsideNamedChildren.Get(root);
            var names = AirsideNamedChildren.Names(root);
            var parts = Activator.CreateInstance(viewPartsType);
            viewPartsType.GetField("Owner").SetValue(parts, root);
            // PartsFor flags separate elevator meshes before classifying; mirror that so the tailplane stays still.
            foreach (var name in names)
                if (name.StartsWith("Elevator", StringComparison.Ordinal))
                    viewPartsType.GetField("HasSeparateElevators").SetValue(parts, true);
            var classifyArgs = new object[] { children, names, parts };
            proto.GetMethod("ClassifyAnimatedParts", PrivateStatic).Invoke(null, classifyArgs);
            parts = classifyArgs[2];
            var control = viewPartsType.GetField("ControlSurfaces").GetValue(parts);
            var gear = viewPartsType.GetField("LightsAndGear").GetValue(parts);
            var state = Activator.CreateInstance(stateType, true);
            // Frame the gear shots on the deployed legs, wheels and doors, whatever pose is photographed.
            var updateGear = proto.GetMethod("UpdateAircraftLightsAndGear", PrivateStatic);
            for (var settle = 0; settle < 3; settle++)
                updateGear.Invoke(null, new object[] { gear, AircraftPhase.Landing, 1f, 0.5f, 1f, 0f, null, null, type });
            Bounds? gearBounds = null;
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                var n = renderer.name;
                if (!(n.StartsWith("Gear", StringComparison.Ordinal) || n.StartsWith("Tire", StringComparison.Ordinal)
                      || n.StartsWith("Wheel", StringComparison.Ordinal) || n.StartsWith("gear_door", StringComparison.Ordinal)))
                    continue;
                if (gearBounds.HasValue) { var b = gearBounds.Value; b.Encapsulate(renderer.bounds); gearBounds = b; }
                else gearBounds = renderer.bounds;
            }
            ApplyPose(proto, pose, type, control, gear, state, id);
            LogSurfaces(control, id, pose);

            foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
                if (r.name.Contains("shadow", StringComparison.OrdinalIgnoreCase)) r.enabled = false;
            var bounds = new Bounds(); var first = true;
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                if (!renderer.enabled) continue;
                if (first) { bounds = renderer.bounds; first = false; } else bounds.Encapsulate(renderer.bounds);
            }

            var viewList = new Dictionary<string, (float yaw, float pitch)>
            {
                { "side", (90f, 4f) }, { "front", (20f, 14f) }, { "under", (60f, -38f) },
                { "top", (0f, 89f) }, { "rear", (165f, 22f) }, { "ahead", (0f, 0f) }, { "behind", (180f, 0f) }
            };
            foreach (var name in views)
            {
                var (yaw, pitch) = viewList[name];
                var rotation = Quaternion.Euler(-pitch, -yaw, 0f);
                var framed = bounds;
                // Gear shots frame the lower fuselage and the wheels, not the whole airframe.
                if (pose is "gearup" or "gearmid" or "geardown" && gearBounds.HasValue)
                {
                    framed = gearBounds.Value;
                    framed.Expand(2.5f);
                }
                camera.transform.position = framed.center + rotation * Vector3.forward * 150f;
                camera.transform.LookAt(framed.center);
                var ext = framed.extents;
                var right = camera.transform.right; var up = camera.transform.up;
                float Extent(Vector3 axis) => Mathf.Abs(axis.x) * ext.x + Mathf.Abs(axis.y) * ext.y + Mathf.Abs(axis.z) * ext.z;
                camera.orthographicSize = Mathf.Max(Extent(up), Extent(right) / camera.aspect) * 1.1f;
                camera.Render();
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                File.WriteAllBytes(Path.Combine(output, id + "_" + pose + "_" + name + ".png"), image.EncodeToPNG());
                Object.DestroyImmediate(image);
            }

            LogRig(root, id);
            Object.DestroyImmediate(root.gameObject);
        }

        RenderTexture.active = null;
        camera.targetTexture = null;
        Object.DestroyImmediate(target);
        Debug.Log("Aircraft articulation review: " + output);
    }

    private static void ApplyPose(Type proto, string pose, AircraftType type, object control, object gear,
        object state, string id)
    {
        var updateGear = proto.GetMethod("UpdateAircraftLightsAndGear", PrivateStatic);
        var updateSurfaces = proto.GetMethod("UpdateControlSurfaces", PrivateStatic);
        object[] GearArgs(AircraftPhase phase, float progress) => new object[]
        {
            gear, phase, 1f, progress, 0f, 0f, null, null, type
        };
        switch (pose)
        {
            case "geardown":
                updateGear.Invoke(null, GearArgs(AircraftPhase.Landing, 0.5f));
                break;
            case "gearup":
                for (var i = 0; i < 12; i++)
                    updateGear.Invoke(null, new object[] { gear, AircraftPhase.Circuit, 1f, 0.5f, 1f, 0f, null, null, type });
                break;
            case "gearmid":
            {
                // Seed fully down, then step to the progress that puts the gear half-way through its cycle.
                updateGear.Invoke(null, GearArgs(AircraftPhase.Landing, 0.5f));
                var best = 0f; var bestError = 9f;
                for (var p = 0f; p <= 1f; p += 0.0005f)
                {
                    var error = Mathf.Abs(AirsideReusableMotion.GearBias(AircraftPhase.Takeoff, p, type) - 0.5f);
                    if (error < bestError) { bestError = error; best = p; }
                }
                for (var i = 0; i < 80; i++)
                    updateGear.Invoke(null, new object[] { gear, AircraftPhase.Takeoff, 1f, best, 0.1f, 0f, null, null, type });
                break;
            }
            case var approach when approach.StartsWith("approach"):
            {
                // approachNN: seed gear up in the circuit, then fly the first NN% of final at 20 fps.
                var end = int.TryParse(approach.Substring(8), out var percent) ? percent / 100f : 0.1f;
                for (var i = 0; i < 12; i++)
                    updateGear.Invoke(null, new object[] { gear, AircraftPhase.Circuit, 1f, 0.5f, 1f, 0f, null, null, type });
                const int frames = 100;
                for (var i = 1; i <= frames; i++)
                    updateGear.Invoke(null, new object[]
                        { gear, AircraftPhase.Approach, 1f, end * i / frames, 0.05f, 0f, null, null, type });
                break;
            }
            case "roll":
            case "landing":
            {
                updateGear.Invoke(null, GearArgs(AircraftPhase.Landing, 0.5f));
                var phase = pose == "roll" ? AircraftPhase.Approach : AircraftPhase.Landing;
                var progress = pose == "roll" ? 1f : 0.8f;
                // A steady roll to the left: 10 degrees a second for two seconds, flaps running out meanwhile.
                for (var i = 0; i < 60; i++)
                {
                    var bank = pose == "roll" ? i * 1f : 0f;
                    updateSurfaces.Invoke(null, new object[]
                        { control, state, phase, progress, bank, 0.1f, null, true });
                }
                break;
            }
        }
    }

    /// <summary>Where each surface's trailing edge sits against its rest pose, in aircraft axes (+X right, +Y up).</summary>
    private static void LogSurfaces(object control, string id, string pose)
    {
        var builder = new System.Text.StringBuilder(id + " " + pose + " surfaces:");
        foreach (var part in (Array)control)
        {
            var type = part.GetType();
            var transform = (Transform)type.GetField("Transform").GetValue(part);
            var kind = type.GetField("Kind").GetValue(part).ToString();
            if (kind == "Wing") continue;
            var deflection = (float)type.GetField("Deflection").GetValue(part);
            var axis = (Vector3)type.GetField("HingeAxis").GetValue(part);
            var restRotation = (Quaternion)type.GetField("RestRotation").GetValue(part);
            var restPosition = (Vector3)type.GetField("RestPosition").GetValue(part);
            var filter = transform.GetComponent<MeshFilter>();
            if (filter == null) continue;
            var bounds = filter.sharedMesh.bounds;
            var edge = new Vector3(bounds.center.x, bounds.center.y, bounds.min.z);
            var now = transform.TransformPoint(edge);
            var rotation = transform.localRotation; var position = transform.localPosition;
            transform.localRotation = restRotation; transform.localPosition = restPosition;
            var rest = transform.TransformPoint(edge);
            transform.localRotation = rotation; transform.localPosition = position;
            var move = now - rest;
            builder.Append("\n  ").Append(transform.name).Append(' ').Append(kind)
                .Append(" TEdown=").Append(deflection.ToString("F1"))
                .Append(" axis=").Append(axis.ToString("F2"))
                .Append(" TE moves x=").Append(move.x.ToString("F2"))
                .Append(" y=").Append(move.y.ToString("F2"))
                .Append(" z=").Append(move.z.ToString("F2"));
        }
        Debug.Log(builder.ToString());
    }

    private static void LogRig(Transform root, string id)
    {
        var builder = new System.Text.StringBuilder(id + " gear rig:");
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name is "Gear nose" or "Gear L" or "Gear R" or "Truck L" or "Truck R")
            {
                builder.Append("\n  ").Append(t.name).Append(" <- ").Append(t.parent != null ? t.parent.name : "-")
                    .Append(" [").Append(t.childCount).Append(" children:");
                for (var c = 0; c < t.childCount; c++) builder.Append(' ').Append(t.GetChild(c).name).Append(',');
                builder.Append(']');
            }
        Debug.Log(builder.ToString());
    }
}
