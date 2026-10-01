using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Airside.Presentation
{
    public abstract class CockpitInterior : MonoBehaviour
    {
        public Transform Seat { get; protected set; }
        protected readonly List<Material> _materials = new();
        protected readonly List<(Renderer renderer, bool hidden)> _exterior = new();
        protected TextMesh _readout;
        protected string _lastReadout;
        private bool _entered;
        public void SetReadout(string value)
        {
            if (_readout == null || value == _lastReadout) return;
            _lastReadout = value;
            _readout.text = value;
        }

        /// <summary>Live pitch/bank for the primary flight displays. Types without a live ADI ignore it.</summary>
        public virtual void SetAttitude(float pitchUpDegrees, float bankLeftDegrees) { }

        public void Enter()
        {
            if (_entered) return;
            _entered = true;
            _exterior.Clear();
            foreach (var renderer in transform.parent.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.transform.IsChildOf(transform)) continue;
                // Keep the whole wing, engines and animated propellers visible through the windows.
                if (KeepExteriorPart(renderer.transform)) continue;
                _exterior.Add((renderer, renderer.forceRenderingOff));
                renderer.forceRenderingOff = true;
            }
            gameObject.SetActive(true);
        }

        protected bool KeepExteriorPart(Transform part)
        {
            while (part != null && part != transform.parent)
            {
                if (CockpitExteriorVisibility.KeepsDuringCockpit(part.name)) return true;
                part = part.parent;
            }
            return false;
        }

        public void Leave()
        {
            foreach (var entry in _exterior)
                if (entry.renderer != null) entry.renderer.forceRenderingOff = entry.hidden;
            _exterior.Clear();
            _entered = false;
            gameObject.SetActive(false);
        }

        protected Material Surface(string name, Color color, bool lit = true)
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

        protected Transform Box(string name, Vector3 pos, Vector3 size, Material mat)
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

        protected Transform Beam(string name, Vector3 a, Vector3 b, float width, Material material)
        {
            var beam = Box(name, (a + b) * 0.5f, new Vector3(width, width, Vector3.Distance(a, b)), material);
            beam.localRotation = Quaternion.LookRotation(b - a);
            return beam;
        }

        private readonly List<(Transform part, Vector3 pivot, Vector3 mid, Quaternion rest, float side)> _wipers = new();
        private Light _panelLight;

        /// <summary>A windscreen wiper that is parked until it rains, then sweeps up about its inner pivot.</summary>
        protected void Wiper(Vector3 inner, Vector3 outer, float width, Material material, float side)
        {
            var part = Beam("Windscreen wiper", inner, outer, width, material);
            _wipers.Add((part, inner, (inner + outer) * 0.5f, part.localRotation, side));
        }

        /// <summary>Night panel glow and rain wipers. Presentation only; called every frame by the runtime.</summary>
        public void SetEnvironment(float daylight, float precipitation, float seconds)
        {
            var night = 1f - Mathf.SmoothStep(0.15f, 0.55f, daylight);
            if (night > 0.02f && _panelLight == null && Seat != null)
            {
                var host = new GameObject("Panel glow");
                host.transform.SetParent(transform, false);
                host.transform.localPosition = Seat.localPosition + new Vector3(0.25f, 0.35f, 0.45f);
                _panelLight = host.AddComponent<Light>();
                _panelLight.type = LightType.Point; _panelLight.range = 3.4f;
                _panelLight.color = new Color(1f, 0.80f, 0.58f); _panelLight.shadows = LightShadows.None;
            }
            if (_panelLight != null)
            {
                _panelLight.intensity = 1.1f * night;
                _panelLight.enabled = night > 0.02f;
            }
            var sweep = 0f;
            if (precipitation > 0.04f)
            {
                var period = Mathf.Lerp(3.2f, 1.1f, Mathf.Clamp01(precipitation));
                var phase = seconds / period % 1f;
                var active = Mathf.Lerp(0.45f, 1f, Mathf.Clamp01(precipitation));   // light rain wipes intermittently
                sweep = phase < active ? Mathf.Sin(Mathf.PI * phase / active) : 0f;
            }
            foreach (var wiper in _wipers)
            {
                if (wiper.part == null) continue;
                var turn = Quaternion.AngleAxis(wiper.side * 72f * sweep, Vector3.forward);
                wiper.part.localRotation = turn * wiper.rest;
                wiper.part.localPosition = wiper.pivot + turn * (wiper.mid - wiper.pivot);
            }
        }

        protected readonly List<Mesh> _meshes = new();
        protected readonly List<Texture2D> _textures = new();
        protected Transform Face(string name, Vector3[] vertices, Material material)
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
            var uv = new Vector2[bothSides.Length];
            var bounds = mesh.bounds;
            for (var i = 0; i < bothSides.Length; i++)
                uv[i] = new Vector2((bothSides[i].x - bounds.min.x) / Mathf.Max(0.0001f, bounds.size.x),
                    (bothSides[i].y - bounds.min.y) / Mathf.Max(0.0001f, bounds.size.y));
            mesh.uv = uv;
            _meshes.Add(mesh);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.AddComponent<MeshRenderer>().sharedMaterial = material;
            return part.transform;
        }

        protected TextMesh Label(string name, string text, Vector3 position, float size, Color color)
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

        /// <summary>A round attitude indicator: a sky/ground/pitch-ladder texture whose UVs are driven by
        /// the aircraft's real pitch and bank, clipped by a fixed disc like a real PFD ADI.</summary>
        protected sealed class AttitudeDisc
        {
            private const float DegreesPerRadius = 18f;   // vertical field: +/-18 degrees at the rim
            private const float TextureDegrees = 90f;     // texture spans +/-45 degrees of pitch
            private readonly Mesh _mesh;
            private readonly Vector2[] _local;
            private readonly Vector2[] _uv;
            public AttitudeDisc(Mesh mesh, Vector2[] local) { _mesh = mesh; _local = local; _uv = new Vector2[local.Length]; Set(0f, 0f); }

            private float _lastPitch = float.NaN, _lastBank = float.NaN;

            public void Set(float pitchUpDegrees, float bankLeftDegrees)
            {
                if (pitchUpDegrees == _lastPitch && bankLeftDegrees == _lastBank) return;
                _lastPitch = pitchUpDegrees; _lastBank = bankLeftDegrees;
                var angle = bankLeftDegrees * Mathf.Deg2Rad;
                var cos = Mathf.Cos(angle); var sin = Mathf.Sin(angle);
                var pitch = Mathf.Clamp(pitchUpDegrees, -30f, 30f);
                for (var i = 0; i < _local.Length; i++)
                {
                    var p = _local[i];
                    var x = p.x * cos - p.y * sin;
                    var y = p.x * sin + p.y * cos;
                    _uv[i] = new Vector2(0.5f + x * 0.5f, 0.5f + (pitch + y * DegreesPerRadius) / TextureDegrees);
                }
                _mesh.uv = _uv;
            }
        }

        private Texture2D _attitudeTexture;
        protected AttitudeDisc MakeAttitudeDisc(string name, Vector3 centre, float radius)
        {
            if (_attitudeTexture == null) _attitudeTexture = MakeAttitudeTexture();
            var material = Surface(name, Color.white, false);
            material.SetTexture("_BaseMap", _attitudeTexture);
            const int rim = 40;
            var local = new Vector2[(rim + 1) * 2];
            var vertices = new Vector3[local.Length];
            for (var side = 0; side < 2; side++)
            {
                var offset = side * (rim + 1);
                local[offset] = Vector2.zero; vertices[offset] = Vector3.zero;
                for (var i = 0; i < rim; i++)
                {
                    var a = i * Mathf.PI * 2f / rim;
                    local[offset + 1 + i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    vertices[offset + 1 + i] = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
                }
            }
            var triangles = new int[rim * 6];
            for (var i = 0; i < rim; i++)
            {
                var a = 1 + i; var b = 1 + (i + 1) % rim;
                triangles[i * 6] = 0; triangles[i * 6 + 1] = b; triangles[i * 6 + 2] = a;
                triangles[i * 6 + 3] = rim + 1; triangles[i * 6 + 4] = rim + 1 + a; triangles[i * 6 + 5] = rim + 1 + b;
            }
            var mesh = new Mesh { name = name, vertices = vertices, triangles = triangles };
            mesh.RecalculateBounds(); _meshes.Add(mesh);
            var host = new GameObject(name);
            host.transform.SetParent(transform, false); host.transform.localPosition = centre;
            host.AddComponent<MeshFilter>().sharedMesh = mesh;
            host.AddComponent<MeshRenderer>().sharedMaterial = material;
            return new AttitudeDisc(mesh, local);
        }

        private Texture2D MakeAttitudeTexture()
        {
            const int width = 128, height = 512;           // 512 rows = 90 degrees of pitch
            var pixels = new Color32[width * height];
            var skyTop = new Color32(22, 78, 150, 255); var skyHorizon = new Color32(70, 140, 205, 255);
            var groundHorizon = new Color32(150, 98, 52, 255); var groundBottom = new Color32(88, 54, 26, 255);
            var white = new Color32(235, 240, 235, 255);
            for (var y = 0; y < height; y++)
            {
                var degrees = (y + 0.5f) / height * 90f - 45f;
                Color32 c = degrees >= 0f
                    ? Color32.Lerp(skyHorizon, skyTop, Mathf.Clamp01(degrees / 45f))
                    : Color32.Lerp(groundHorizon, groundBottom, Mathf.Clamp01(-degrees / 45f));
                for (var x = 0; x < width; x++) pixels[y * width + x] = c;
            }
            void Line(float degrees, int halfWidth, int thickness)
            {
                var row = Mathf.RoundToInt((degrees + 45f) / 90f * height);
                for (var y = row - thickness / 2; y <= row + thickness / 2; y++)
                {
                    if (y < 0 || y >= height) continue;
                    for (var x = width / 2 - halfWidth; x <= width / 2 + halfWidth; x++) pixels[y * width + x] = white;
                }
            }
            Line(0f, width / 2, 3);                          // horizon
            for (var d = -40; d <= 40; d += 5)
            {
                if (d == 0) continue;
                Line(d, d % 10 == 0 ? 22 : 11, 2);          // 10 degree bars long, 5 degree bars short
            }
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, true)
            { name = "Attitude indicator", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            texture.SetPixels32(pixels); texture.Apply(true, true); _textures.Add(texture);
            return texture;
        }

        protected static void Dispose(Object item)
        {
            if (Application.isPlaying) Destroy(item);
            else DestroyImmediate(item);
        }

        protected virtual void OnDestroy()
        {
            foreach (var entry in _exterior)
                if (entry.renderer != null) entry.renderer.forceRenderingOff = entry.hidden;
            foreach (var texture in _textures)
                if (texture != null) Dispose(texture);
            foreach (var mesh in _meshes)
                if (mesh != null) Dispose(mesh);
            foreach (var material in _materials)
                if (material != null) Dispose(material);
        }
    }
}
