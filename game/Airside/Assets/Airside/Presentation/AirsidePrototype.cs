using System;
using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Airside.Presentation
{
    public sealed class AirsidePrototype : MonoBehaviour
    {
        private ManualSimulationClock _clock;
        private AirportSimulation _simulation;
        private Transform _aircraft;
        private AirsideCameraController _cameraController;
        private double _preciseTime;
        private bool _paused;
        private int _speed = 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartPrototype()
        {
            if (FindFirstObjectByType<AirsidePrototype>() == null)
                new GameObject("Airside Prototype").AddComponent<AirsidePrototype>();
        }

        private void Awake()
        {
            _clock = new ManualSimulationClock(new SimulationTime(0));
            _simulation = new AirportSimulation(_clock, new SeededRandomSource(24031996), new ReservationTable());

            BuildLightingAndCamera();
            BuildAirfield();
            _aircraft = BuildAircraft();
            _cameraController.SetFollowTarget(_aircraft);
        }

        private void Update()
        {
            ReadSimulationControls();
            if (!_paused)
                _preciseTime += Time.unscaledDeltaTime * _speed;

            var wholeSeconds = (long)Math.Floor(_preciseTime);
            if (wholeSeconds > _clock.Now.ElapsedSeconds)
            {
                _clock.Set(new SimulationTime(wholeSeconds));
                _simulation.Update();
            }

            UpdateAircraftVisual();
        }

        private void ReadSimulationControls()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.spaceKey.wasPressedThisFrame)
                _paused = !_paused;
            if (keyboard.tabKey.wasPressedThisFrame)
                _speed = _speed == 1 ? 4 : 1;
        }

        private void UpdateAircraftVisual()
        {
            var cycleSeconds = (float)(_preciseTime - _simulation.CycleStartedAt.ElapsedSeconds);
            var standZ = _simulation.AssignedStand.Equals(AirportSimulation.StandOne) ? 14f : 20f;
            var position = PositionFor(cycleSeconds, standZ);
            var next = PositionFor(cycleSeconds + 0.15f, standZ);
            _aircraft.position = position;

            var direction = next - position;
            if (direction.sqrMagnitude > 0.001f)
                _aircraft.rotation = Quaternion.Slerp(_aircraft.rotation, Quaternion.LookRotation(direction.normalized), Time.unscaledDeltaTime * 5f);
        }

        private void OnGUI()
        {
            var scale = Mathf.Clamp(Screen.height / 720f, 0.8f, 1.35f);
            var previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            var panel = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(18, 18, 14, 14)
            };
            var title = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
            var detail = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            var small = new GUIStyle(GUI.skin.label) { fontSize = 13 };

            GUI.Box(new Rect(22, 22, 370, 205), string.Empty, panel);
            GUI.Label(new Rect(42, 36, 320, 34), "AIRSIDE", title);
            GUI.Label(new Rect(42, 76, 320, 25), $"Flight {_simulation.ActiveAircraft.AircraftId}  ·  {_simulation.AssignedStand}", detail);
            GUI.Label(new Rect(42, 104, 320, 25), $"{FormatPhase(_simulation.ActiveAircraft.Phase)}  ·  {_simulation.ActiveAircraft.SecondsRemaining(_clock.Now)}s", detail);
            GUI.Label(new Rect(42, 132, 320, 22), $"{(_paused ? "PAUSED" : $"{_speed}× time")}  ·  Cycles {_simulation.CompletedCycles}", small);
            GUI.Label(new Rect(42, 156, 320, 22), $"Reserved: {ReservationSummary()}", small);
            GUI.Label(new Rect(42, 184, 330, 25), "Space pause  ·  Tab speed  ·  F follow  ·  O overview", small);

            GUI.Box(new Rect(Screen.width / scale - 258, 22, 236, 78), string.Empty, panel);
            GUI.Label(new Rect(Screen.width / scale - 238, 38, 200, 22), "Right-drag orbit", small);
            GUI.Label(new Rect(Screen.width / scale - 238, 62, 200, 22), "Scroll zoom  ·  WASD pan", small);
            GUI.matrix = previousMatrix;
        }

        private string ReservationSummary()
        {
            var resources = _simulation.Reservations.OccupiedResources.Select(resource => resource.Value).ToArray();
            return resources.Length == 0 ? "none" : string.Join(", ", resources);
        }

        private void BuildLightingAndCamera()
        {
            var camera = Camera.main;
            if (camera == null)
                camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.fieldOfView = 48f;
            camera.clearFlags = CameraClearFlags.Skybox;

            _cameraController = camera.GetComponent<AirsideCameraController>();
            if (_cameraController == null)
                _cameraController = camera.gameObject.AddComponent<AirsideCameraController>();

            if (camera.GetComponent<AudioListener>() == null)
                camera.gameObject.AddComponent<AudioListener>();

            var light = FindFirstObjectByType<Light>();
            if (light == null)
                light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.color = new Color(1f, 0.93f, 0.82f);
            light.transform.rotation = Quaternion.Euler(48f, -28f, 0f);
            RenderSettings.ambientLight = new Color(0.46f, 0.53f, 0.61f);
        }

        private static void BuildAirfield()
        {
            CreateBlock("Grass", new Vector3(0f, -0.65f, 4f), new Vector3(94f, 1f, 66f), new Color(0.16f, 0.34f, 0.21f));
            CreateBlock("Runway", new Vector3(0f, -0.08f, 0f), new Vector3(78f, 0.15f, 7f), new Color(0.105f, 0.12f, 0.14f));
            CreateBlock("Taxiway A", new Vector3(8f, -0.02f, 9f), new Vector3(48f, 0.12f, 4f), new Color(0.22f, 0.24f, 0.26f));
            CreateBlock("Apron", new Vector3(20f, 0f, 17f), new Vector3(28f, 0.12f, 14f), new Color(0.34f, 0.36f, 0.37f));
            CreateBlock("Terminal", new Vector3(26f, 2.2f, 27f), new Vector3(22f, 4.5f, 5f), new Color(0.68f, 0.72f, 0.75f));
            CreateBlock("Terminal glass", new Vector3(26f, 2.4f, 24.45f), new Vector3(17f, 2.2f, 0.12f), new Color(0.16f, 0.38f, 0.5f));
            CreateBlock("Hangar", new Vector3(-20f, 2.5f, 20f), new Vector3(14f, 5f, 9f), new Color(0.45f, 0.5f, 0.54f));

            for (var x = -34; x <= 34; x += 8)
                CreateBlock("Runway marking", new Vector3(x, 0.02f, 0f), new Vector3(3.5f, 0.03f, 0.28f), Color.white);

            BuildStandMarking(17f, 14f, "Stand 1");
            BuildStandMarking(17f, 20f, "Stand 2");
        }

        private static void BuildStandMarking(float x, float z, string name)
        {
            CreateBlock(name, new Vector3(x, 0.08f, z), new Vector3(0.18f, 0.03f, 4.2f), new Color(0.96f, 0.77f, 0.12f));
            CreateBlock($"{name} stop", new Vector3(x, 0.08f, z + 1.9f), new Vector3(3.4f, 0.03f, 0.18f), new Color(0.96f, 0.77f, 0.12f));
        }

        private static Transform BuildAircraft()
        {
            var root = new GameObject("Active flight").transform;
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Fuselage";
            body.transform.SetParent(root, false);
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            body.transform.localScale = new Vector3(0.72f, 2.8f, 0.72f);
            body.GetComponent<Renderer>().material = CreateMaterial(new Color(0.93f, 0.95f, 0.97f));

            var wings = CreateBlock("Wings", Vector3.zero, new Vector3(7f, 0.12f, 2.2f), new Color(0.12f, 0.43f, 0.76f));
            wings.transform.SetParent(root, false);
            var tail = CreateBlock("Tail", new Vector3(0f, 0.65f, -2f), new Vector3(0.16f, 1.6f, 1.1f), new Color(0.12f, 0.43f, 0.76f));
            tail.transform.SetParent(root, false);

            var source = root.gameObject.AddComponent<AudioSource>();
            source.clip = CreateEngineClip();
            source.loop = true;
            source.volume = 0.11f;
            source.spatialBlend = 0.75f;
            source.minDistance = 8f;
            source.maxDistance = 75f;
            source.Play();
            return root;
        }

        private static AudioClip CreateEngineClip()
        {
            const int sampleRate = 22050;
            var samples = new float[sampleRate];
            for (var i = 0; i < samples.Length; i++)
            {
                var time = i / (float)sampleRate;
                samples[i] = (Mathf.Sin(time * 2f * Mathf.PI * 82f) * 0.12f) +
                             (Mathf.Sin(time * 2f * Mathf.PI * 164f) * 0.035f);
            }

            var clip = AudioClip.Create("Prototype engine", samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static Vector3 PositionFor(float second, float standZ)
        {
            second = Mathf.Clamp(second, 0f, AirportSimulation.CycleLengthSeconds);
            if (second < 20f) return Smooth(new Vector3(-52f, 14f, 0f), new Vector3(-35f, 2f, 0f), second / 20f);
            if (second < 32f) return Smooth(new Vector3(-35f, 2f, 0f), new Vector3(-24f, 0.7f, 0f), (second - 20f) / 12f);
            if (second < 57f) return Smooth(new Vector3(-24f, 0.7f, 0f), new Vector3(17f, 0.7f, standZ), (second - 32f) / 25f);
            if (second < 102f) return new Vector3(17f, 0.7f, standZ);
            if (second < 114f) return Smooth(new Vector3(17f, 0.7f, standZ), new Vector3(12f, 0.7f, standZ - 2f), (second - 102f) / 12f);
            if (second < 139f) return Smooth(new Vector3(12f, 0.7f, standZ - 2f), new Vector3(28f, 0.7f, 0f), (second - 114f) / 25f);
            if (second < 154f) return Smooth(new Vector3(28f, 0.7f, 0f), new Vector3(48f, 12f, 0f), (second - 139f) / 15f);
            return new Vector3(52f, 15f, 0f);
        }

        private static Vector3 Smooth(Vector3 from, Vector3 to, float progress) => Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, progress));

        private static GameObject CreateBlock(string name, Vector3 position, Vector3 scale, Color color)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.position = position;
            block.transform.localScale = scale;
            block.GetComponent<Renderer>().material = CreateMaterial(color);
            return block;
        }

        private static Material CreateMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            return new Material(shader) { color = color };
        }

        private static string FormatPhase(AircraftPhase phase) => phase switch
        {
            AircraftPhase.TaxiIn => "Taxiing to stand",
            AircraftPhase.AtStand => "Turnaround at stand",
            AircraftPhase.TaxiOut => "Taxiing to runway",
            _ => phase.ToString()
        };
    }
}
