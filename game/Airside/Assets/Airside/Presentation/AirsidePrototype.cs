using Airside.Domain;
using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    public sealed class AirsidePrototype : MonoBehaviour
    {
        private AircraftOperation _operation;
        private Transform _aircraft;
        private float _startedAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartPrototype()
        {
            if (FindFirstObjectByType<AirsidePrototype>() != null)
                return;

            new GameObject("Airside Prototype").AddComponent<AirsidePrototype>();
        }

        private void Awake()
        {
            _operation = new AircraftOperation("AS-101", new SimulationTime(0));
            _startedAt = Time.time;

            BuildLightingAndCamera();
            BuildAirfield();
            _aircraft = BuildAircraft();
        }

        private void Update()
        {
            var elapsed = Mathf.FloorToInt(Time.time - _startedAt) % 160;
            if (_operation.IsComplete && elapsed < 2)
            {
                _operation = new AircraftOperation("AS-101", new SimulationTime(0));
                _startedAt = Time.time;
                elapsed = 0;
            }

            _operation.AdvanceTo(new SimulationTime(elapsed));
            _aircraft.position = PositionFor(elapsed);
        }

        private void OnGUI()
        {
            var panel = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 18,
                padding = new RectOffset(18, 18, 14, 14)
            };
            var title = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
            var detail = new GUIStyle(GUI.skin.label) { fontSize = 17 };

            GUI.Box(new Rect(22, 22, 330, 142), string.Empty, panel);
            GUI.Label(new Rect(42, 36, 280, 34), "AIRSIDE", title);
            GUI.Label(new Rect(42, 76, 280, 28), $"Flight {_operation.AircraftId}", detail);
            GUI.Label(new Rect(42, 105, 280, 28), $"Status: {FormatPhase(_operation.Phase)}", detail);
            GUI.Label(new Rect(42, 134, 280, 22), "Prototype simulation · 1× time", GUI.skin.label);
        }

        private static void BuildLightingAndCamera()
        {
            var camera = Camera.main;
            if (camera == null)
                camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(32f, 35f, -44f);
            camera.transform.LookAt(new Vector3(5f, 0f, 0f));
            camera.fieldOfView = 48f;

            var light = FindFirstObjectByType<Light>();
            if (light == null)
                light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.transform.rotation = Quaternion.Euler(48f, -28f, 0f);
        }

        private static void BuildAirfield()
        {
            CreateBlock("Grass", new Vector3(0f, -0.65f, 0f), new Vector3(90f, 1f, 60f), new Color(0.18f, 0.34f, 0.22f));
            CreateBlock("Runway", new Vector3(0f, -0.08f, 0f), new Vector3(74f, 0.15f, 7f), new Color(0.12f, 0.14f, 0.16f));
            CreateBlock("Taxiway", new Vector3(8f, -0.02f, 9f), new Vector3(44f, 0.12f, 4f), new Color(0.23f, 0.25f, 0.27f));
            CreateBlock("Apron", new Vector3(19f, 0f, 15f), new Vector3(26f, 0.12f, 12f), new Color(0.34f, 0.36f, 0.37f));
            CreateBlock("Terminal", new Vector3(24f, 2.2f, 22f), new Vector3(20f, 4.5f, 5f), new Color(0.68f, 0.72f, 0.75f));
            CreateBlock("Hangar", new Vector3(-18f, 2.5f, 18f), new Vector3(13f, 5f, 8f), new Color(0.48f, 0.53f, 0.56f));

            for (var x = -32; x <= 32; x += 8)
                CreateBlock("Runway marking", new Vector3(x, 0.02f, 0f), new Vector3(3.5f, 0.03f, 0.28f), Color.white);
        }

        private static Transform BuildAircraft()
        {
            var root = new GameObject("Flight AS-101").transform;
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Fuselage";
            body.transform.SetParent(root, false);
            body.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            body.transform.localScale = new Vector3(0.7f, 2.8f, 0.7f);
            body.GetComponent<Renderer>().material = Material(new Color(0.93f, 0.95f, 0.97f));

            var wings = CreateBlock("Wings", Vector3.zero, new Vector3(2.2f, 0.12f, 7f), new Color(0.15f, 0.48f, 0.78f));
            wings.transform.SetParent(root, false);
            var tail = CreateBlock("Tail", new Vector3(-2f, 0.6f, 0f), new Vector3(1.1f, 1.6f, 0.16f), new Color(0.15f, 0.48f, 0.78f));
            tail.transform.SetParent(root, false);
            return root;
        }

        private static Vector3 PositionFor(int second)
        {
            if (second < 32) return Vector3.Lerp(new Vector3(-48f, 11f, 0f), new Vector3(-30f, 0.6f, 0f), second / 32f);
            if (second < 57) return Vector3.Lerp(new Vector3(-30f, 0.6f, 0f), new Vector3(17f, 0.7f, 15f), (second - 32f) / 25f);
            if (second < 102) return new Vector3(17f, 0.7f, 15f);
            if (second < 139) return Vector3.Lerp(new Vector3(17f, 0.7f, 15f), new Vector3(30f, 0.6f, 0f), (second - 102f) / 37f);
            return Vector3.Lerp(new Vector3(30f, 0.6f, 0f), new Vector3(48f, 11f, 0f), (second - 139f) / 21f);
        }

        private static GameObject CreateBlock(string name, Vector3 position, Vector3 scale, Color color)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.position = position;
            block.transform.localScale = scale;
            block.GetComponent<Renderer>().material = Material(color);
            return block;
        }

        private static Material Material(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { color = color };
            return material;
        }

        private static string FormatPhase(AircraftPhase phase) => phase switch
        {
            AircraftPhase.TaxiIn => "Taxiing to stand",
            AircraftPhase.AtStand => "At stand",
            AircraftPhase.TaxiOut => "Taxiing to runway",
            _ => phase.ToString()
        };
    }
}
