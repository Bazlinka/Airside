using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Airside.Presentation
{
    /// <summary>Original, editable SF34 spectator interior, authored in the exterior kit's metre coordinates.</summary>
    public sealed class SaabCockpitInterior : MonoBehaviour
    {
        public Transform Seat { get; private set; }
        private readonly List<Material> _materials = new();
        private readonly List<(Renderer renderer, bool hidden)> _exterior = new();
        private TextMesh _readout;
        private string _lastReadout;
        public void SetReadout(string value)
        {
            if (_readout == null || value == _lastReadout) return;
            _lastReadout = value;
            _readout.text = value;
        }

        public static SaabCockpitInterior Build(Transform aircraft)
        {
            var host = new GameObject("SF34 cockpit interior");
            host.transform.SetParent(aircraft, false);
            host.transform.localPosition = new Vector3(0f, AircraftVisualProfiles.Saab340.ModelGroundOffsetMetres, 0f);
            var rig = host.AddComponent<SaabCockpitInterior>();
            rig.MakeInterior();
            return rig;
        }

        public void Enter()
        {
            _exterior.Clear();
            foreach (var renderer in transform.parent.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.transform.IsChildOf(transform)) continue;
                // Keep wings, nacelles and animated propellers visible through side windows.
                if (KeepExteriorPart(renderer.transform)) continue;
                _exterior.Add((renderer, renderer.forceRenderingOff));
                renderer.forceRenderingOff = true;
            }
            gameObject.SetActive(true);
        }

        private bool KeepExteriorPart(Transform part)
        {
            while (part != null && part != transform.parent)
            {
                var name = part.name;
                if (name.StartsWith("Wing") || name.StartsWith("Engine") || name.StartsWith("Nacelle")
                    || name.StartsWith("Prop") || name.StartsWith("Spinner")) return true;
                part = part.parent;
            }
            return false;
        }

        public void Leave()
        {
            foreach (var entry in _exterior)
                if (entry.renderer != null) entry.renderer.forceRenderingOff = entry.hidden;
            _exterior.Clear();
            gameObject.SetActive(false);
        }

        private Material Surface(string name, Color color, bool lit = true)
        {
            var shader = Shader.Find(lit ? "Universal Render Pipeline/Lit" : "Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            var mat = new Material(shader) { name = "Cockpit " + name };
            mat.SetColor("_BaseColor", color);
            mat.color = color;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.2f);
            _materials.Add(mat);
            return mat;
        }

        private Transform Box(string name, Vector3 pos, Vector3 size, Material mat)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(transform, false);
            part.transform.localPosition = pos;
            part.transform.localScale = size;
            var bevel = AirsidePrototype.CockpitBoxMesh(size);
            if (bevel != null) part.GetComponent<MeshFilter>().sharedMesh = bevel;
            var collider = part.GetComponent<Collider>();
            collider.enabled = false;
            Dispose(collider);
            part.GetComponent<Renderer>().sharedMaterial = mat;
            return part.transform;
        }

        private void Beam(string name, Vector3 a, Vector3 b, float width, Material material)
        {
            var beam = Box(name, (a + b) * 0.5f, new Vector3(width, width, Vector3.Distance(a, b)), material);
            beam.localRotation = Quaternion.LookRotation(b - a);
        }

        // Reference-driven SF34B layout. All geometry and markings are original;
        // reference photographs are not textures or shipped assets.
        private readonly List<Mesh> _meshes = new();
        private Material _black, _panel, _white, _green, _metal;

        private Transform Face(string name, Vector3[] vertices, Material material)
        {
            var part = new GameObject(name);
            part.transform.SetParent(transform, false);
            var mesh = new Mesh { name = name };
            var triangles = new List<int>();
            var bothSides = new Vector3[vertices.Length * 2];
            vertices.CopyTo(bothSides, 0); vertices.CopyTo(bothSides, vertices.Length);
            for (var i = 1; i < vertices.Length - 1; i++)
            {
                triangles.Add(0); triangles.Add(i + 1); triangles.Add(i);
                triangles.Add(vertices.Length); triangles.Add(vertices.Length + i); triangles.Add(vertices.Length + i + 1);
            }
            mesh.vertices = bothSides; mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            _meshes.Add(mesh);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.AddComponent<MeshRenderer>().sharedMaterial = material;
            return part.transform;
        }

        private Transform Disc(string name, Vector3 centre, float radius, Material material)
        {
            var vertices = new Vector3[32];
            for (var i = 0; i < vertices.Length; i++)
            {
                var angle = i * Mathf.PI * 2f / vertices.Length;
                vertices[i] = centre + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
            }
            return Face(name, vertices, material);
        }

        private TextMesh Label(string name, string text, Vector3 position, float size, Color color)
        {
            var host = new GameObject(name);
            host.transform.SetParent(transform, false); host.transform.localPosition = position;
            var label = host.AddComponent<TextMesh>();
            label.text = text; label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center;
            label.characterSize = size; label.fontSize = 48;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.color = color;
            var renderer = host.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = label.font.material; renderer.shadowCastingMode = ShadowCastingMode.Off;
            return label;
        }

        private void Stroke(string name, Vector3 a, Vector3 b, float width, Material material)
        {
            var delta = b - a;
            var cross = new Vector3(-delta.y, delta.x, 0f).normalized * (width * 0.5f);
            Face(name, new[] { a - cross, b - cross, b + cross, a + cross }, material);
        }

        private void Dial(string name, float x, float y, float radius, float needleDegrees = 30f)
        {
            var z = 7.985f;
            Disc(name + " rim", new Vector3(x, y, z), radius, _metal);
            Disc(name + " face", new Vector3(x, y, z - 0.002f), radius * 0.88f, _black);
            for (var tick = 0; tick < 10; tick++)
            {
                var angle = (tick * 27f + 150f) * Mathf.Deg2Rad;
                var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                Stroke(name + " tick", new Vector3(x, y, z - 0.004f) + direction * radius * 0.68f,
                    new Vector3(x, y, z - 0.004f) + direction * radius * 0.84f, Mathf.Max(0.0035f, radius * 0.075f), _white);
            }
            var needle = new Vector3(Mathf.Sin(needleDegrees * Mathf.Deg2Rad), Mathf.Cos(needleDegrees * Mathf.Deg2Rad), 0f);
            Stroke(name + " needle", new Vector3(x, y, z - 0.006f),
                new Vector3(x, y, z - 0.006f) + needle * radius * 0.65f, Mathf.Max(0.003f, radius * 0.05f), _white);
            Disc(name + " hub", new Vector3(x, y, z - 0.007f), radius * 0.1f, _metal);
        }

        private void FlightDisplays(float x)
        {
            var sky = Surface("EFIS sky", new Color(0.18f, 0.36f, 0.46f), false);
            var earth = Surface("EFIS earth", new Color(0.39f, 0.27f, 0.15f), false);
            foreach (var y in new[] { 1.91f, 1.665f })
            {
                Box("CRT bezel", new Vector3(x, y, 8.0f), new Vector3(0.23f, 0.215f, 0.055f), _black);
                Box("CRT glass", new Vector3(x, y, 7.967f), new Vector3(0.185f, 0.17f, 0.009f), _black);
                for (var side = -1; side <= 1; side += 2)
                    Disc("CRT adjustment knob", new Vector3(x + side * 0.094f, y - 0.087f, 7.96f), 0.012f, _metal);
            }
            Box("EFIS blue sky", new Vector3(x, 1.945f, 7.958f), new Vector3(0.145f, 0.07f, 0.003f), sky);
            Box("EFIS brown ground", new Vector3(x, 1.875f, 7.957f), new Vector3(0.145f, 0.07f, 0.003f), earth);
            Box("EFIS horizon", new Vector3(x, 1.91f, 7.953f), new Vector3(0.145f, 0.002f, 0.002f), _white);
            for (var row = -2; row <= 2; row++)
                Box("Pitch ladder", new Vector3(x, 1.91f + row * 0.02f, 7.95f), new Vector3(row == 0 ? 0.09f : 0.035f, 0.0018f, 0.002f), _white);
            Box("Flight director", new Vector3(x, 1.91f, 7.947f), new Vector3(0.07f, 0.004f, 0.002f), _green);
            Disc("Navigation compass", new Vector3(x, 1.665f, 7.955f), 0.072f, _metal);
            Disc("Navigation background", new Vector3(x, 1.665f, 7.953f), 0.066f, _black);
            for (var tick = 0; tick < 12; tick++)
            {
                var angle = tick * Mathf.PI / 6f;
                var d = new Vector3(Mathf.Sin(angle), Mathf.Cos(angle), 0f);
                Stroke("Compass tick", new Vector3(x, 1.665f, 7.95f) + d * 0.053f,
                    new Vector3(x, 1.665f, 7.95f) + d * 0.062f, 0.0035f, _white);
            }
            Beam("Course pointer", new Vector3(x, 1.615f, 7.945f), new Vector3(x, 1.715f, 7.945f), 0.003f, _green);
            Label("Compass north", "N", new Vector3(x, 1.712f, 7.94f), 0.0028f, Color.white);
        }

        private void MakeInterior()
        {
            var lining = Surface("warm ivory lining", new Color(0.64f, 0.63f, 0.57f));
            var trim = Surface("window seals", new Color(0.18f, 0.20f, 0.19f));
            _panel = Surface("grey instrument panel", new Color(0.46f, 0.49f, 0.47f));
            _black = Surface("black instrument faces", new Color(0.025f, 0.035f, 0.035f), false);
            _white = Surface("instrument markings", new Color(0.78f, 0.82f, 0.74f), false);
            _green = Surface("instrument green", new Color(0.45f, 0.78f, 0.51f), false);
            _metal = Surface("instrument rims", new Color(0.26f, 0.29f, 0.28f));
            var fabric = Surface("seat fabric", new Color(0.36f, 0.38f, 0.35f));
            Seat = new GameObject("Left pilot eye").transform;
            Seat.SetParent(transform, false);
            Seat.localPosition = new Vector3(-0.43f, 2.38f, 7.13f);
            Seat.localRotation = Quaternion.Euler(16f, 0f, 0f);
            Box("Flight deck floor", new Vector3(0f, 1.20f, 7.0f), new Vector3(1.72f, 0.07f, 2.45f), trim);
            Box("Flight deck roof", new Vector3(0f, 2.79f, 6.94f), new Vector3(1.7f, 0.08f, 2.20f), lining);
            Box("Rear bulkhead", new Vector3(0f, 1.98f, 5.9f), new Vector3(1.72f, 1.55f, 0.06f), lining);
            // Raked forward panes, angled corner panes and side panes; no flat bus windscreen.
            var centreLow = new Vector3(0f, 2.16f, 8.35f);
            var centreHigh = new Vector3(0f, 2.70f, 8.08f);
            Beam("Centre windscreen seal", centreLow, centreHigh, 0.027f, trim);
            foreach (var side in new[] { -1f, 1f })
            {
                var frontLow = new Vector3(side * 0.69f, 2.12f, 8.18f);
                var frontHigh = new Vector3(side * 0.60f, 2.66f, 7.99f);
                var cornerLow = new Vector3(side * 0.86f, 2.04f, 7.69f);
                var cornerHigh = new Vector3(side * 0.80f, 2.68f, 7.43f);
                Beam("Forward brow seal", centreHigh, frontHigh, 0.035f, trim);
                Beam("Forward sill seal", centreLow, frontLow, 0.032f, trim);
                Beam("Angled windscreen post", frontLow, frontHigh, 0.042f, lining);
                Beam("Quarter window sill", frontLow, cornerLow, 0.035f, trim);
                Beam("Quarter window brow", frontHigh, cornerHigh, 0.041f, lining);
                Beam("Side window front post", cornerLow, cornerHigh, 0.046f, lining);
                Beam("Sliding window sill", cornerLow, new Vector3(side * 0.86f, 2.04f, 6.38f), 0.039f, trim);
                Beam("Sliding window rail", cornerHigh, new Vector3(side * 0.81f, 2.72f, 6.38f), 0.042f, lining);
                Beam("Sliding window rear post", new Vector3(side * 0.86f, 2.04f, 6.38f), new Vector3(side * 0.81f, 2.72f, 6.38f), 0.043f, lining);
                Face("Forward roof taper", new[] { centreHigh, frontHigh, cornerHigh,
                    new Vector3(side * 0.85f, 2.79f, 6.8f), new Vector3(0f, 2.79f, 6.8f) }, lining);
                Box("Lower side lining", new Vector3(side * 0.86f, 1.66f, 7.03f), new Vector3(0.075f, 0.72f, 1.90f), lining);
                Beam("Window latch", new Vector3(side * 0.83f, 2.07f, 7.12f), new Vector3(side * 0.83f, 2.07f, 7.29f), 0.018f, _metal);
                Box("Side console", new Vector3(side * 0.74f, 1.49f, 7.18f), new Vector3(0.16f, 0.1f, 0.65f), _panel);
                Box("Pilot cushion", new Vector3(side * 0.43f, 1.55f, 6.91f), new Vector3(0.50f, 0.13f, 0.48f), fabric);
                Box("Pilot seat back", new Vector3(side * 0.43f, 1.90f, 6.64f), new Vector3(0.50f, 0.70f, 0.12f), fabric);
                Box("Pilot headrest", new Vector3(side * 0.43f, 2.31f, 6.62f), new Vector3(0.27f, 0.21f, 0.13f), fabric);
                Box("Yoke column", new Vector3(side * 0.43f, 1.50f, 7.57f), new Vector3(0.09f, 0.57f, 0.10f), lining);
                var x = side * 0.43f;
                Beam("Yoke lower crossbar", new Vector3(x - 0.15f, 1.77f, 7.52f), new Vector3(x + 0.15f, 1.77f, 7.52f), 0.027f, trim);
                foreach (var grip in new[] { -1f, 1f })
                {
                    Beam("Yoke squared grip", new Vector3(x + grip * 0.15f, 1.77f, 7.52f), new Vector3(x + grip * 0.15f, 1.94f, 7.56f), 0.032f, trim);
                    Beam("Yoke inward shoulder", new Vector3(x + grip * 0.15f, 1.94f, 7.56f), new Vector3(x + grip * 0.045f, 1.94f, 7.56f), 0.028f, trim);
                }
                Box("Yoke centre hub", new Vector3(x, 1.85f, 7.54f), new Vector3(0.085f, 0.07f, 0.05f), trim);
                Box("Yoke checklist clip", new Vector3(x, 1.81f, 7.49f), new Vector3(0.075f, 0.13f, 0.004f), _white);
                Label("Yoke identification", "SF34", new Vector3(x, 1.85f, 7.509f), 0.0033f, Color.white);
                // Separate vertical CRT pair with adjacent round instruments.
                FlightDisplays(x);
                Dial("Outboard upper flight gauge", x + side * 0.18f, 1.93f, 0.066f);
                Dial("Outboard lower flight gauge", x + side * 0.18f, 1.74f, 0.055f, -35f);
                Dial("Inboard upper flight gauge", x - side * 0.17f, 1.94f, 0.049f, 45f);
                Dial("Inboard lower flight gauge", x - side * 0.17f, 1.77f, 0.049f, -15f);
            }
            // Chamfered panel silhouette and shallow glare shield leave the instruments visible.
            Face("Instrument panel face", new[] { new Vector3(-0.78f, 1.53f, 8.015f), new Vector3(0.78f, 1.53f, 8.015f),
                new Vector3(0.78f, 1.96f, 8.015f), new Vector3(0.61f, 2.14f, 8.015f),
                new Vector3(-0.61f, 2.14f, 8.015f), new Vector3(-0.78f, 1.96f, 8.015f) }, _panel);
            foreach (var side in new[] { -1f, 1f })
            {
                Beam("Windscreen wiper arm", new Vector3(side * 0.06f, 2.18f, 8.32f), new Vector3(side * 0.40f, 2.20f, 8.23f), 0.009f, trim);
                Beam("Parked windscreen wiper blade", new Vector3(side * 0.22f, 2.205f, 8.275f), new Vector3(side * 0.58f, 2.17f, 8.205f), 0.012f, trim);
                for (var row = 0; row < 3; row++)
                    Box("Panel outboard switch", new Vector3(side * 0.725f, 1.88f - row * 0.085f, 7.987f), new Vector3(0.022f, 0.017f, 0.017f), trim);
            }
            Box("Shallow glareshield", new Vector3(0f, 2.17f, 8.12f), new Vector3(1.45f, 0.045f, 0.32f), trim);
            Box("Autopilot control rail", new Vector3(0f, 2.075f, 7.994f), new Vector3(0.70f, 0.083f, 0.028f), _metal);
            for (var i = 0; i < 12; i++)
            {
                Box("Flight guidance button", new Vector3(-0.29f + i * 0.052f, 2.075f, 7.975f), new Vector3(0.025f, 0.021f, 0.008f), _black);
                Box("Guidance indicator", new Vector3(-0.29f + i * 0.052f, 2.09f, 7.968f), new Vector3(0.018f, 0.003f, 0.002f), _green);
            }
            Label("Guidance legends", "HDG   NAV   AP   ALT   VS", new Vector3(0f, 2.11f, 7.969f), 0.0035f, Color.white);
            Box("Engine instrument inset", new Vector3(0f, 1.835f, 8.0f), new Vector3(0.27f, 0.36f, 0.025f), _metal);
            var titles = new[] { "TRQ", "ITT", "NP", "NG" };
            for (var row = 0; row < 4; row++)
            {
                var y = 1.965f - row * 0.082f;
                Dial("Left engine " + titles[row], -0.074f, y, 0.035f, 35f - row * 12f);
                Dial("Right engine " + titles[row], 0.074f, y, 0.035f, 32f - row * 10f);
                Label("Engine gauge legend", titles[row], new Vector3(0f, y, 7.97f), 0.0023f, Color.white);
            }
            Box("Local telemetry inset", new Vector3(0f, 1.575f, 7.995f), new Vector3(0.26f, 0.11f, 0.02f), _black);
            _readout = Label("Local telemetry", "", new Vector3(0f, 1.575f, 7.98f), 0.0038f, new Color(0.65f, 0.85f, 0.69f));
            Label("Panel identity", "SF34 • LOCAL VIEW", new Vector3(-0.43f, 1.535f, 7.978f), 0.0034f, Color.white);
            // Narrow radio pedestal with three paired turboprop lever groups.
            Box("Centre pedestal", new Vector3(0f, 1.48f, 7.19f), new Vector3(0.31f, 0.43f, 1.30f), _panel);
            for (var row = 0; row < 3; row++)
            {
                Box("Radio control unit", new Vector3(0f, 1.702f, 7.66f - row * 0.12f), new Vector3(0.26f, 0.018f, 0.095f), _metal);
                for (var col = 0; col < 5; col++)
                    Box("Radio key", new Vector3(-0.1f + col * 0.05f, 1.716f, 7.66f - row * 0.12f), new Vector3(0.018f, 0.014f, 0.035f), _black);
            }
            for (var group = 0; group < 3; group++)
                foreach (var side in new[] { -1f, 1f })
                {
                    var x = -0.10f + group * 0.10f + side * 0.022f;
                    Box("Lever slot", new Vector3(x, 1.708f, 7.16f), new Vector3(0.009f, 0.006f, 0.30f), _black);
                    Beam("Turboprop lever", new Vector3(x, 1.71f, 7.13f), new Vector3(x, 1.86f - group * 0.025f, 7.23f), 0.013f, _metal);
                    Box(group == 0 ? "Power handle" : group == 1 ? "Condition handle" : "Propeller handle",
                        new Vector3(x, 1.86f - group * 0.025f, 7.23f), new Vector3(0.039f, 0.027f, 0.06f), group == 1 ? _white : trim);
                }
            Box("Overhead electrical panel", new Vector3(0f, 2.742f, 7.31f), new Vector3(0.48f, 0.025f, 1.04f), _panel);
            for (var row = 0; row < 6; row++)
                for (var col = 0; col < 6; col++)
                    Box("Overhead switch", new Vector3(-0.19f + col * 0.076f, 2.719f, 6.88f + row * 0.155f), new Vector3(0.013f, 0.033f, 0.018f), trim);
            SetReadout("GS 0 kt\nHEIGHT 0 ft\nHDG 000°");
        }

        private static void Dispose(Object item)
        {
            if (Application.isPlaying) Destroy(item);
            else DestroyImmediate(item);
        }

        private void OnDestroy()
        {
            foreach (var entry in _exterior)
                if (entry.renderer != null) entry.renderer.forceRenderingOff = entry.hidden;
            foreach (var mesh in _meshes)
                if (mesh != null) Dispose(mesh);
            foreach (var material in _materials)
                if (material != null) Dispose(material);
        }
    }
}
