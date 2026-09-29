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
/// Batch-mode check of a whole turnaround's layout on one type: the real aircraft, every ramp
/// crew member for every job (with their equipment), where each service vehicle stops, and
/// the routes vehicles drive to get there. Run by scripts/review-turnaround.sh.
/// </summary>
public static class TurnaroundLayoutReview
{
    private static readonly RampActivity[] Jobs =
    {
        RampActivity.Arrival, RampActivity.Fuel, RampActivity.Catering, RampActivity.Baggage, RampActivity.Boarding
    };

    public static void Render()
    {
        var args = Environment.GetCommandLineArgs();
        var index = Array.IndexOf(args, "-turnaroundReviewOutput");
        var output = Path.GetFullPath(index >= 0 && index + 1 < args.Length ? args[index + 1] : "../../work/turnaround-review");
        Directory.CreateDirectory(output);
        ShaderUtil.allowAsyncCompilation = false;

        foreach (var id in new[] { "ATR42", "SF34", "DH8D", "A320" })
        {
            AircraftType.TryFromId(id, out var type);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.72f, 0.78f, 0.86f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.6f, 0.65f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.31f, 0.32f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
            var apron = GameObject.CreatePrimitive(PrimitiveType.Plane);
            apron.transform.localScale = new Vector3(20f, 1f, 20f);
            apron.GetComponent<Renderer>().sharedMaterial =
                AirsideMaterialLibrary.CreateShared(new Color(0.5f, 0.51f, 0.52f), AirsideMaterialLibrary.SurfaceKind.Concrete);

            var aircraft = (Transform)typeof(AirsidePrototype).GetMethod("BuildAircraftForType", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, BuildArgs(type));
            aircraft.position = Vector3.zero;
            OpenAirstairs(aircraft);
            var layout = AircraftLayout.For(type);
            var parent = new GameObject("Crew").transform;
            var crew = new List<RampCrewMember>();
            var slot = 0;
            foreach (var job in Jobs)
            {
                RampCrew.ForActivity(job, 0.4, layout, crew);
                foreach (var member in crew)
                    Worker(member, parent, slot++);
            }

            Marker("Fuel truck", layout.FuelTruck, new Vector3(2.5f, 2.6f, 7f), new Color(0.95f, 0.76f, 0.12f));
            Marker("Baggage train", layout.BaggageTrain, new Vector3(1.6f, 1.4f, 3.2f), new Color(0.91f, 0.38f, 0.12f));
            if (layout.CateringTruck is { } hiLoader)
                Marker("Catering truck", hiLoader, new Vector3(2.5f, 3.2f, 7f), new Color(0.9f, 0.9f, 0.92f));

            // Routes a vehicle would drive from a park bay behind the aircraft.
            var rects = new List<LayoutRect>();
            layout.Footprint(rects, forVehicles: true);
            var obstacles = rects.Select(r => RouteObstacle.Quad(r.MinX, r.MinZ, r.MaxX, r.MinZ, r.MaxX, r.MaxZ, r.MinX, r.MaxZ)).ToList();
            var from = new Vector2(-(layout.HalfSpan + 10f), layout.NoseZ + 10f);
            DrawRoute(from, layout.FuelTruck, obstacles, new Color(0.95f, 0.76f, 0.12f));
            DrawRoute(from, layout.BaggageTrain, obstacles, new Color(0.91f, 0.38f, 0.12f));

            var centre = new Vector3(0f, 0f, (layout.NoseZ + layout.TailZ) * 0.5f);
            var reach = Mathf.Max(layout.HalfSpan, layout.Length * 0.5f) + 12f;
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.72f, 0.82f);
            camera.allowHDR = false;
            var target = new RenderTexture(1600, 1200, 24) { antiAliasing = 4 };
            camera.targetTexture = target;
            camera.orthographic = true;
            camera.orthographicSize = reach;
            camera.transform.SetPositionAndRotation(centre + Vector3.up * 120f, Quaternion.Euler(90f, 0f, 0f));
            Shot(camera, target, Path.Combine(output, id + "_top.png"));
            camera.orthographic = false;
            camera.fieldOfView = 38f;
            camera.transform.position = centre + new Vector3(-reach * 1.05f, reach * 0.55f, reach * 0.35f);
            camera.transform.LookAt(centre);
            Shot(camera, target, Path.Combine(output, id + "_oblique.png"));
            camera.targetTexture = null;
            Object.DestroyImmediate(target);
            Debug.Log("Turnaround review " + id);
        }
    }

    private static object[] BuildArgs(AircraftType type)
    {
        var method = typeof(AirsidePrototype).GetMethod("BuildAircraftForType", BindingFlags.NonPublic | BindingFlags.Static);
        var args = method.GetParameters().Select(p => p.HasDefaultValue ? p.DefaultValue : null).ToArray();
        args[0] = "Review " + type.Id;
        args[1] = type;
        args[2] = new Color(0.12f, 0.35f, 0.6f);
        return args;
    }

    private static void OpenAirstairs(Transform aircraft)
    {
        foreach (var door in aircraft.GetComponentsInChildren<Component>(true).Where(c => c.GetType().Name == "AirstairDoor"))
        {
            var degrees = (float)door.GetType().GetField("OpenDegrees").GetValue(door);
            door.transform.localRotation *= Quaternion.Euler(0f, 0f, degrees);
        }
    }

    private static void Worker(RampCrewMember member, Transform parent, int slot)
    {
        var id = slot % 2 == 0 ? "chr_ramp_m_worker" : "chr_ramp_f_worker";
        var figure = Object.Instantiate(Resources.Load<GameObject>("Airside/Characters/" + id));
        var animator = figure.GetComponent<Animator>();
        if (animator != null)
            animator.enabled = false;
        figure.transform.SetPositionAndRotation(new Vector3(member.AcrossMetres, 0f, member.AlongMetres),
            Quaternion.Euler(0f, member.FacingDegrees, 0f));
        figure.transform.localScale = Vector3.one * (1.75f / AirsidePrototype.MeasureFigureHeight(figure));
        var clip = Resources.LoadAll<AnimationClip>("Airside/Characters/" + id)
            .First(c => c.name.EndsWith(member.Task switch
            {
                RampTask.MarshalArrival or RampTask.WingWalk => "Wave",
                RampTask.PlaceSafetyEquipment or RampTask.BoardingSupervision or RampTask.PushbackHeadset => "Idle_Neutral",
                _ => "Interact"
            }, StringComparison.Ordinal));
        clip.SampleAnimation(figure, 0.3f * clip.length);
        HandTools.Pose(HandTools.BuildRampKit(member.Task, parent), FigureRig.Find(figure));
    }

    private static void Marker(string name, (float X, float Z) at, Vector3 size, Color colour)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.position = new Vector3(at.X, size.y * 0.5f, at.Z);
        box.transform.localScale = size;
        box.GetComponent<Renderer>().sharedMaterial = AirsideMaterialLibrary.CreateShared(colour, AirsideMaterialLibrary.SurfaceKind.PaintedMetal);
    }

    private static void DrawRoute(Vector2 from, (float X, float Z) to, List<RouteObstacle> obstacles, Color colour)
    {
        var route = new List<float>();
        GroundRouter.Plan(from.x, from.y, to.X, to.Z, obstacles, 2.2f, route);
        var material = AirsideMaterialLibrary.CreateShared(colour, AirsideMaterialLibrary.SurfaceKind.PaintedLine);
        for (var i = 2; i + 1 < route.Count; i += 2)
        {
            var a = new Vector3(route[i - 2], 0.05f, route[i - 1]);
            var b = new Vector3(route[i], 0.05f, route[i + 1]);
            var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.transform.position = (a + b) * 0.5f;
            line.transform.rotation = Quaternion.LookRotation(b - a, Vector3.up);
            line.transform.localScale = new Vector3(0.35f, 0.04f, Vector3.Distance(a, b));
            line.GetComponent<Renderer>().sharedMaterial = material;
        }
    }

    private static void Shot(Camera camera, RenderTexture target, string path)
    {
        camera.Render();
        camera.Render();
        RenderTexture.active = target;
        var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        image.Apply();
        File.WriteAllBytes(path, image.EncodeToPNG());
        Object.DestroyImmediate(image);
        RenderTexture.active = null;
    }
}
