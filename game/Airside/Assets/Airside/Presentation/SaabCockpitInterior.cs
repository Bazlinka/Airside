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

        private void MakeInterior()
        {
            var trim = Surface("trim", new Color(0.20f, 0.23f, 0.24f));
            var lining = Surface("lining", new Color(0.42f, 0.44f, 0.41f));
            var ink = Surface("panel", new Color(0.06f, 0.09f, 0.10f));
            var fabric = Surface("seat fabric", new Color(0.21f, 0.28f, 0.30f));
            var display = Surface("display", new Color(0.015f, 0.035f, 0.04f), false);
            Seat = new GameObject("Left pilot eye").transform;
            Seat.SetParent(transform, false);
            Seat.localPosition = new Vector3(-0.38f, 2.35f, 7.10f);
            Seat.localRotation = Quaternion.Euler(12f, 0f, 0f);
            Box("Flight deck floor", new Vector3(0f, 1.20f, 7.05f), new Vector3(1.65f, 0.08f, 2.40f), trim);
            Box("Flight deck roof", new Vector3(0f, 2.73f, 7.05f), new Vector3(1.65f, 0.10f, 2.35f), lining);
            Box("Rear bulkhead", new Vector3(0f, 1.95f, 5.9f), new Vector3(1.65f, 1.5f, 0.08f), lining);
            foreach (var side in new[] { -1f, 1f })
            {
                Box("Lower side lining", new Vector3(side * 0.80f, 1.62f, 7.12f), new Vector3(0.09f, 0.75f, 2.25f), lining);
                Beam("Side window sill", new Vector3(side * 0.80f, 2.02f, 6.1f), new Vector3(side * 0.68f, 2.10f, 8.15f), 0.055f, trim);
                Beam("Side roof rail", new Vector3(side * 0.80f, 2.68f, 6.1f), new Vector3(side * 0.58f, 2.57f, 8.15f), 0.07f, lining);
                Beam("Windscreen outer pillar", new Vector3(side * 0.68f, 2.10f, 8.15f), new Vector3(side * 0.58f, 2.57f, 8.15f), 0.06f, lining);
                Beam("Side window pillar", new Vector3(side * 0.80f, 2.02f, 6.45f), new Vector3(side * 0.80f, 2.68f, 6.45f), 0.055f, lining);
                Box("Pilot cushion", new Vector3(side * 0.40f, 1.52f, 6.83f), new Vector3(0.52f, 0.13f, 0.52f), fabric);
                Box("Pilot seat back", new Vector3(side * 0.40f, 1.91f, 6.52f), new Vector3(0.52f, 0.75f, 0.12f), fabric);
                Beam("Yoke column", new Vector3(side * 0.40f, 1.30f, 7.32f), new Vector3(side * 0.40f, 1.69f, 7.48f), 0.04f, trim);
                Box("Yoke crossbar", new Vector3(side * 0.40f, 1.71f, 7.48f), new Vector3(0.26f, 0.035f, 0.045f), ink);
                foreach (var grip in new[] { -1f, 1f })
                    Box("Yoke grip", new Vector3(side * 0.40f + grip * 0.12f, 1.75f, 7.48f), new Vector3(0.035f, 0.11f, 0.045f), ink);
            }
            Beam("Centre windscreen pillar", new Vector3(0f, 2.06f, 8.27f), new Vector3(0f, 2.60f, 8.27f), 0.04f, trim);
            Box("Upper windscreen lining", new Vector3(0f, 2.655f, 8.15f), new Vector3(1.22f, 0.17f, 0.07f), lining);
            Beam("Windscreen brow", new Vector3(-0.59f, 2.57f, 8.15f), new Vector3(0.59f, 2.57f, 8.15f), 0.055f, lining);
            Box("Glare shield", new Vector3(0f, 2.02f, 7.94f), new Vector3(1.42f, 0.07f, 0.46f), ink);
            Box("Instrument panel", new Vector3(0f, 1.79f, 7.91f), new Vector3(1.36f, 0.40f, 0.16f), trim);
            Box("Centre pedestal", new Vector3(0f, 1.47f, 7.18f), new Vector3(0.25f, 0.46f, 0.95f), trim);
            foreach (var side in new[] { -1f, 1f })
            {
                Box("Power lever", new Vector3(side * 0.045f, 1.75f, 7.28f), new Vector3(0.025f, 0.18f, 0.035f), ink);
                Box("Power lever knob", new Vector3(side * 0.045f, 1.84f, 7.28f), new Vector3(0.06f, 0.035f, 0.055f), ink);
            }
            Box("Live readout screen", new Vector3(-0.37f, 1.82f, 7.82f), new Vector3(0.54f, 0.25f, 0.015f), display);
            Box("Right panel inset", new Vector3(0.37f, 1.82f, 7.82f), new Vector3(0.54f, 0.25f, 0.015f), ink);
            var label = new GameObject("Live readout");
            label.transform.SetParent(transform, false);
            label.transform.localPosition = new Vector3(-0.61f, 1.89f, 7.805f);
            _readout = label.AddComponent<TextMesh>();
            _readout.anchor = TextAnchor.UpperLeft;
            _readout.characterSize = 0.014f;
            _readout.fontSize = 42;
            _readout.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _readout.color = new Color(0.65f, 0.9f, 0.78f);
            var renderer = label.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _readout.font.material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            SetReadout("LOCAL FLIGHT VIEW");
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
            foreach (var material in _materials)
                if (material != null) Dispose(material);
        }
    }
}
