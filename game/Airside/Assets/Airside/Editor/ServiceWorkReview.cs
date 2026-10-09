using System;
using System.Collections.Generic;
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

/// <summary>
/// Batch-mode frames from the middle of each hands-on job (ADR 0176), posed by the game's own
/// <see cref="ServiceChoreography"/>, crew rig and equipment: bags walked to a turboprop hold
/// and up a jet's belt loader, the fuel nozzle out on its hose, galley boxes carried up an
/// airstair, a hi-loader raised to a jet's service door, and bags left planeside.
/// Run by scripts/review-service-work.sh.
/// </summary>
public static class ServiceWorkReview
{
    private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;

    public static void Render()
    {
        var args = Environment.GetCommandLineArgs();
        var index = Array.IndexOf(args, "-serviceReviewOutput");
        var output = Path.GetFullPath(index >= 0 && index + 1 < args.Length ? args[index + 1] : "../../work/service-review");
        Directory.CreateDirectory(output);
        ShaderUtil.allowAsyncCompilation = false;
        // One scene for every frame: aircraft art is cached in the scene, and a fresh scene per
        // frame left a second build of the same type empty.
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.72f, 0.78f, 0.86f);
        RenderSettings.ambientEquatorColor = new Color(0.55f, 0.6f, 0.65f);
        RenderSettings.ambientGroundColor = new Color(0.3f, 0.31f, 0.32f);
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.transform.rotation = Quaternion.Euler(50f, 40f, 0f);
        sun.intensity = 1.2f;
        sun.shadows = LightShadows.Soft;
        var apron = GameObject.CreatePrimitive(PrimitiveType.Plane);
        apron.transform.localScale = new Vector3(20f, 1f, 20f);
        apron.GetComponent<Renderer>().sharedMaterial =
            AirsideMaterialLibrary.CreateShared(new Color(0.5f, 0.51f, 0.52f), AirsideMaterialLibrary.SurfaceKind.Concrete);

        var regional = TurnaroundCrewWork.BaggageSeconds(AircraftType.Saab340);
        var jet = TurnaroundCrewWork.BaggageSeconds(AircraftType.AirbusA320200);
        Frame(output, "SF34", RampActivity.Baggage, 6, regional);
        Frame(output, "SF34", RampActivity.Baggage, 38, regional);
        Frame(output, "SF34", RampActivity.Baggage, regional - 6, regional);
        Frame(output, "A320", RampActivity.Baggage, 44, jet);
        Frame(output, "A320", RampActivity.Baggage, jet - 6, jet);
        Frame(output, "ATR42", RampActivity.Fuel, 45, 90);
        Frame(output, "ATR42", RampActivity.Catering, 44, 75);
        Frame(output, "A320", RampActivity.Catering, 60, 112);
        Frame(output, "ATR42", RampActivity.Boarding, 60, 120, new List<double> { 8, 14, 19, 26, 33, 41, 47 });
    }

    private static void Frame(string output, string id, RampActivity activity, double elapsed, double seconds,
        List<double> drops = null)
    {
        AircraftType.TryFromId(id, out var type);
        var layout = AircraftLayout.For(type);
        var frame = new GameObject("Frame").transform;
        var aircraft = BuildAircraft(type);
        aircraft.SetParent(frame, true);
        OpenAirstairs(aircraft);
        var pose = new GroundPose(0f, 0f, 0f, 1f, 0f, false);
        (float X, float Z)? cart = null;
        if (drops != null)
        {
            var door = layout.PassengerDoor;
            var side = AircraftLayout.SideOf(door);
            cart = (door.X + side * 6.5f, door.Z + 1.5f);
        }

        var crew = new List<RampCrewMember>();
        var actions = new List<CrewAction>();
        var scene = new ServiceScene();
        RampCrew.ForActivity(activity, elapsed / seconds, layout, crew);
        ServiceChoreography.Act(activity, type, elapsed, seconds, crew, actions, scene, drops, cart);
        var parent = new GameObject("Work").transform;
        parent.SetParent(frame, false);

        for (var i = 0; i < crew.Count; i++)
            Worker(crew[i], actions[i], parent, i, scene, activity);

        foreach (var item in scene.Transit)
        {
            var loose = HandTools.BuildItem(item.Kind, parent, 2);
            loose.position = new Vector3(item.X, item.Height - (item.Kind == CarriedItem.Bag ? 0.35f : 0f), item.Z);
        }

        if (scene.BeltLoader)
        {
            var foot = new Vector3(scene.BeltFoot.X, 0.75f, scene.BeltFoot.Z);
            var top = new Vector3(scene.BeltTop.X, scene.BeltTopHeight, scene.BeltTop.Z);
            var belt = GameObject.CreatePrimitive(PrimitiveType.Cube);
            belt.transform.SetParent(frame, false);
            belt.transform.SetPositionAndRotation((foot + top) * 0.5f, Quaternion.LookRotation((top - foot).normalized));
            belt.transform.localScale = new Vector3(0.8f, 0.12f, (top - foot).magnitude + 0.4f);
            belt.GetComponent<Renderer>().sharedMaterial = AirsideMaterialLibrary.CreateShared(new Color(0.2f, 0.21f, 0.22f));
            var chassis = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chassis.transform.SetParent(frame, false);
            var mid = Vector3.Lerp(foot, top, 0.35f);
            chassis.transform.SetPositionAndRotation(new Vector3(mid.x, 0.35f, mid.z),
                Quaternion.LookRotation(new Vector3(top.x - foot.x, 0f, top.z - foot.z).normalized));
            chassis.transform.localScale = new Vector3(1.8f, 0.5f, 4.2f);
            chassis.GetComponent<Renderer>().sharedMaterial = AirsideMaterialLibrary.CreateShared(new Color(0.85f, 0.7f, 0.2f));
        }

        if (activity == RampActivity.Baggage)
            Vehicle(frame, "Baggage cart", layout.BaggageTrain, Quaternion.LookRotation(Vector3.forward), new Color(0.91f, 0.38f, 0.12f),
                "Models/Vehicles/mdl_baggage_tug_train_v06.gltf", scene);
        if (activity == RampActivity.Fuel)
            Vehicle(frame, "Fuel truck", layout.FuelTruck, Quaternion.LookRotation(Vector3.forward), new Color(0.95f, 0.76f, 0.12f),
                "Models/Vehicles/mdl_fuel_truck_small_v06.gltf", scene);
        if (activity == RampActivity.Catering && layout.CateringTruck is { } dock && layout.CateringDoor is { } service)
            Vehicle(frame, "Catering truck", dock, Quaternion.LookRotation(Vector3.right * -AircraftLayout.SideOf(service)),
                new Color(0.82f, 0.86f, 0.88f), "Models/Vehicles/mdl_catering_truck_v01.gltf", scene);
        if (scene.PlanesideCart)
        {
            var bed = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bed.transform.SetParent(frame, false);
            bed.transform.position = new Vector3(scene.PlanesideAt.X, 0.35f, scene.PlanesideAt.Z);
            bed.transform.localScale = new Vector3(1.2f, 0.08f, 2.2f);
            for (var i = 0; i < scene.PlanesideBags; i++)
            {
                var bag = HandTools.BuildItem(CarriedItem.Bag, parent, i);
                bag.position = new Vector3(scene.PlanesideAt.X + (i % 2 == 0 ? 0.02f : 0.58f), 0.39f + 0.085f,
                    scene.PlanesideAt.Z - 0.8f + i / 2 * 0.52f);
                bag.rotation = Quaternion.Euler(0f, 0f, 90f);
                bag.localScale = Vector3.one * 0.8f;
            }
        }

        // Frame the workers.
        var focus = crew.Count == 0 ? Vector3.zero
            : actions.Aggregate(Vector3.zero, (sum, a) => sum + new Vector3(a.X, a.Height, a.Z)) / actions.Count;
        var sideSign = actions.Count > 0 && actions.Average(a => a.X) < 0f ? -1f : 1f;
        var camera = new GameObject("Camera").AddComponent<Camera>();
        camera.transform.SetParent(frame, false);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.62f, 0.72f, 0.82f);
        camera.allowHDR = false;
        camera.fieldOfView = 40f;
        camera.transform.position = focus + new Vector3(sideSign * 13f, 7f, -9f);
        camera.transform.LookAt(focus + Vector3.up * 1.2f);
        var target = new RenderTexture(1600, 1000, 24) { antiAliasing = 4 };
        camera.targetTexture = target;
        camera.Render();
        camera.Render();
        RenderTexture.active = target;
        var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        image.Apply();
        File.WriteAllBytes(Path.Combine(output, $"{id}_{activity}_t{elapsed:0}.png"), image.EncodeToPNG());
        RenderTexture.active = null;
        camera.targetTexture = null;
        Object.DestroyImmediate(target);
        Object.DestroyImmediate(image);
        Debug.Log($"Service review {id} {activity}");
        Object.DestroyImmediate(frame.gameObject);
    }

    private static Transform BuildAircraft(AircraftType type)
    {
        var method = typeof(AirsidePrototype).GetMethod("BuildAircraftForType", PrivateStatic);
        var args = method.GetParameters().Select(p => p.HasDefaultValue ? p.DefaultValue : null).ToArray();
        args[0] = "Review " + type.Id;
        args[1] = type;
        args[2] = new Color(0.12f, 0.35f, 0.6f);
        return (Transform)method.Invoke(null, args);
    }

    private static void OpenAirstairs(Transform aircraft)
    {
        foreach (var door in aircraft.GetComponentsInChildren<Component>(true).Where(c => c.GetType().Name == "AirstairDoor"))
            door.transform.localRotation *= Quaternion.Euler(0f, 0f, (float)door.GetType().GetField("OpenDegrees").GetValue(door));
    }

    private static void Vehicle(Transform frame, string name, (float X, float Z) at, Quaternion rotation, Color colour, string art, ServiceScene scene)
    {
        var build = typeof(AirsidePrototype).GetMethod("BuildServiceVehicle", PrivateStatic);
        var args = build.GetParameters().Select(p => p.HasDefaultValue ? p.DefaultValue : null).ToArray();
        args[0] = name;
        args[1] = colour;
        args[2] = new Vector3(3f, 1.4f, 1.3f);
        args[3] = art;
        var vehicle = (Transform)build.Invoke(null, args);
        vehicle.SetParent(frame, true);
        vehicle.gameObject.SetActive(true);
        Debug.Log($"Review vehicle {name}: {vehicle.GetComponentsInChildren<Renderer>(true).Length} renderers, "
            + $"{vehicle.GetComponentsInChildren<Renderer>(true).Count(r => r.enabled && r.gameObject.activeInHierarchy)} shown");
        typeof(AirsidePrototype).GetMethod("OrientPlusXKitToForward", PrivateStatic)?.Invoke(null, new object[] { vehicle });
        vehicle.SetPositionAndRotation(new Vector3(at.X, 0f, at.Z), rotation);
        var parts = vehicle.GetComponentsInChildren<Transform>(true);
        var bags = parts.Where(p => p.name == "Cargo bag").ToArray();
        for (var i = 0; i < bags.Length; i++)
            bags[i].gameObject.SetActive(i < Mathf.CeilToInt(scene.TrainLoad * bags.Length));
        foreach (var nozzle in parts.Where(p => p.name == "Hose nozzle"))
            nozzle.gameObject.SetActive(!scene.NozzleOut);
        foreach (var part in parts)
        {
            if (part == vehicle)
                continue;
            if (part.name.Contains("box_") || part.name.Contains("platform"))
                part.position += Vector3.up * scene.LiftHeight;
            else if (part.name.Contains("scissor"))
                part.localScale = new Vector3(part.localScale.x, part.localScale.y * (1f + scene.LiftHeight / 2.09f), part.localScale.z);
        }
    }

    private static void Worker(RampCrewMember member, CrewAction action, Transform parent, int slot, ServiceScene scene,
        RampActivity activity)
    {
        var id = slot % 2 == 0 ? "chr_ramp_m_worker" : "chr_ramp_f_worker";
        var figure = Object.Instantiate(Resources.Load<GameObject>("Airside/Characters/" + id), parent);
        var animator = figure.GetComponent<Animator>();
        if (animator != null)
            animator.enabled = false;
        figure.transform.SetPositionAndRotation(new Vector3(action.X, action.Height, action.Z),
            Quaternion.Euler(0f, action.FacingDegrees, 0f));
        figure.transform.localScale = Vector3.one * (1.75f / AirsidePrototype.MeasureFigureHeight(figure));
        var clips = Resources.LoadAll<AnimationClip>("Airside/Characters/" + id);
        var suffix = action.Walking ? "Walk" : member.Task switch
        {
            RampTask.MarshalArrival or RampTask.WingWalk => "Wave",
            RampTask.PlaceSafetyEquipment or RampTask.EquipmentRunner or RampTask.BoardingSupervision or RampTask.PushbackHeadset => "Idle_Neutral",
            _ => "Interact"
        };
        var clip = clips.First(c => c.name.EndsWith(suffix, StringComparison.Ordinal));
        clip.SampleAnimation(figure, 0.3f * clip.length);
        var kit = HandTools.BuildRampKit(member.Task, parent);
        HandTools.SetCarried(kit, action.Item);
        if (scene.NozzleOut && member.Task == RampTask.FuelCoupling)
            kit.HoseAnchor = new Vector3(scene.HoseReel.X, 1.0f, scene.HoseReel.Z);
        HandTools.Pose(kit, FigureRig.Find(figure));
    }
}
