using System;
using System.IO;
using System.Linq;
using Airside.Presentation;
using Airside.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>
/// Batch-mode lineup of every ramp job and passenger luggage style, posed with the same
/// clips, scale and <see cref="HandTools"/> the game uses. Run by scripts/review-people.sh.
/// </summary>
public static class CharacterEquipmentReview
{
    public static void Render()
    {
        var args = Environment.GetCommandLineArgs();
        var index = Array.IndexOf(args, "-peopleReviewOutput");
        var output = Path.GetFullPath(index >= 0 && index + 1 < args.Length ? args[index + 1] : "../../work/people-review");
        Directory.CreateDirectory(output);
        ShaderUtil.allowAsyncCompilation = false;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.70f, 0.77f, 0.85f);
        RenderSettings.ambientEquatorColor = new Color(0.52f, 0.58f, 0.64f);
        RenderSettings.ambientGroundColor = new Color(0.27f, 0.29f, 0.30f);
        var sun = new GameObject("Review sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.transform.rotation = Quaternion.Euler(48f, -30f, 0f);
        sun.intensity = 1.25f;
        sun.shadows = LightShadows.Soft;
        var apron = GameObject.CreatePrimitive(PrimitiveType.Plane);
        apron.transform.localScale = new Vector3(6f, 1f, 6f);
        apron.GetComponent<Renderer>().sharedMaterial =
            AirsideMaterialLibrary.CreateShared(new Color(0.46f, 0.47f, 0.48f), AirsideMaterialLibrary.SurfaceKind.Concrete);
        var parent = new GameObject("Equipment").transform;

        var tasks = (RampTask[])Enum.GetValues(typeof(RampTask));
        for (var i = 0; i < tasks.Length; i++)
        {
            var id = i % 2 == 0 ? "chr_ramp_m_worker" : "chr_ramp_f_worker";
            var figure = Figure(id, new Vector3((i - tasks.Length / 2f) * 1.6f, 0f, 2f), 1.75f);
            var clip = Clip(id, tasks[i] switch
            {
                RampTask.MarshalArrival or RampTask.WingWalk => "Wave",
                RampTask.PlaceSafetyEquipment or RampTask.BoardingSupervision or RampTask.PushbackHeadset => "Idle_Neutral",
                _ => "Interact"
            });
            clip.SampleAnimation(figure, 0.35f * clip.length);
            HandTools.Pose(HandTools.BuildRampKit(tasks[i], parent), FigureRig.Find(figure));
        }

        var passengers = new[] { "chr_passenger_m_casual", "chr_passenger_f_formal", "chr_passenger_m_suit", "chr_passenger_f_casual", "chr_passenger_m_holiday" };
        for (var look = 0; look < passengers.Length; look++)
        {
            var figure = Figure(passengers[look], new Vector3((look - 2) * 1.7f, 0f, -1.6f), 1.62f + look * 0.04f);
            var walk = Clip(passengers[look], "Walk");
            walk.SampleAnimation(figure, (0.2f + look * 0.15f) * walk.length);
            HandTools.Pose(HandTools.BuildPassengerBag(look, parent), FigureRig.Find(figure));
        }

        var lifted = Figure("chr_passenger_f_suit", new Vector3(5.6f, 0f, -1.6f), 1.66f);
        var liftedWalk = Clip("chr_passenger_f_suit", "Walk");
        liftedWalk.SampleAnimation(lifted, 0.5f * liftedWalk.length);
        HandTools.Pose(HandTools.BuildPassengerBag(0, parent), FigureRig.Find(lifted), lifted: true);

        var camera = new GameObject("Review camera").AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.62f, 0.72f, 0.82f);
        camera.fieldOfView = 34f;
        camera.nearClipPlane = 0.05f;
        camera.allowHDR = false;
        var target = new RenderTexture(1800, 900, 24) { antiAliasing = 4 };
        camera.targetTexture = target;
        Shot(camera, target, output, "front", new Vector3(0f, 2.6f, -15f), new Vector3(0f, 0.9f, 0.4f));
        Shot(camera, target, output, "crew-close-left", new Vector3(-6.3f, 1.9f, -1.0f), new Vector3(-6.3f, 1.0f, 2f));
        Shot(camera, target, output, "crew-close-middle", new Vector3(-1.5f, 1.9f, -1.0f), new Vector3(-1.5f, 1.0f, 2f));
        Shot(camera, target, output, "crew-close-right", new Vector3(3.3f, 1.9f, -1.0f), new Vector3(3.3f, 1.0f, 2f));
        Shot(camera, target, output, "passengers-close", new Vector3(1.2f, 1.7f, -7.2f), new Vector3(1.2f, 0.8f, -1.6f));
        Shot(camera, target, output, "back", new Vector3(3f, 3.5f, 14f), new Vector3(0f, 0.8f, 0f));
        camera.fieldOfView = 20f;
        Shot(camera, target, output, "play-distance", new Vector3(0f, 32f, -48f), new Vector3(0f, 0.8f, 0f));
        camera.targetTexture = null;
        Object.DestroyImmediate(target);
        Debug.Log("People review: " + output);
    }

    private static GameObject Figure(string id, Vector3 position, float height)
    {
        var figure = Object.Instantiate(Resources.Load<GameObject>("Airside/Characters/" + id));
        var animator = figure.GetComponent<Animator>();
        if (animator != null)
            animator.enabled = false;
        figure.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 180f, 0f));
        figure.transform.localScale = Vector3.one * (height / AirsidePrototype.MeasureFigureHeight(figure));
        return figure;
    }

    private static AnimationClip Clip(string id, string suffix) =>
        Resources.LoadAll<AnimationClip>("Airside/Characters/" + id).First(c => c.name.EndsWith(suffix, StringComparison.Ordinal));

    private static void Shot(Camera camera, RenderTexture target, string output, string name, Vector3 from, Vector3 at)
    {
        camera.transform.position = from;
        camera.transform.LookAt(at);
        camera.Render();
        camera.Render();
        RenderTexture.active = target;
        var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        image.Apply();
        File.WriteAllBytes(Path.Combine(output, name + ".png"), image.EncodeToPNG());
        Object.DestroyImmediate(image);
        RenderTexture.active = null;
    }
}
