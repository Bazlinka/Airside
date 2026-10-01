using System.Collections.Generic;
using Airside.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace Airside.Presentation
{
    /// <summary>Shared spectator geometry and lifetime; every supported type supplies its own layout.</summary>
    public abstract class TurbopropCockpitInterior : MonoBehaviour
    {
        public static TurbopropCockpitInterior Create(Transform aircraft, AircraftType type)
        {
            if (aircraft == null || !CockpitAvailability.Supported(type)) return null;
            return type.Id == AircraftType.Saab340.Id
                ? SaabCockpitInterior.Build(aircraft)
                : GlassTurbopropCockpitInterior.Build(aircraft, type);
        }

        public Transform Seat { get; protected set; }
        protected readonly List<Material> _materials = new();
        protected readonly List<(Renderer renderer, bool hidden)> _exterior = new();
        protected TextMesh _readout;
        private string _lastReadout;
        public void SetReadout(string value)
        {
            if (_readout == null || value == _lastReadout) return;
            _lastReadout = value;
            _readout.text = value;
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

        protected void Beam(string name, Vector3 a, Vector3 b, float width, Material material)
        {
            var beam = Box(name, (a + b) * 0.5f, new Vector3(width, width, Vector3.Distance(a, b)), material);
            beam.localRotation = Quaternion.LookRotation(b - a);
        }

        protected readonly List<Mesh> _meshes = new();
        protected Material _black, _panel, _white, _green, _metal, _dialMarks, _compassMarks;
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

        protected Transform Disc(string name, Vector3 centre, float radius, Material material)
        {
            var vertices = new Vector3[32];
            for (var i = 0; i < vertices.Length; i++)
            {
                var angle = i * Mathf.PI * 2f / vertices.Length;
                vertices[i] = centre + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
            }
            return Face(name, vertices, material);
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

        protected void Stroke(string name, Vector3 a, Vector3 b, float width, Material material)
        {
            var delta = b - a;
            var cross = new Vector3(-delta.y, delta.x, 0f).normalized * (width * 0.5f);
            Face(name, new[] { a - cross, b - cross, b + cross, a + cross }, material);
        }

        protected Material DialArtwork(string name, int ticks, float startDegrees, float stepDegrees)
        {
            // Original code-drawn markings share the dial's depth surface. Separate
            // millimetre-thin tick geometry vanished in the large-coordinate player scene.
            const int size = 128;
            var pixels = new Color32[size * size];
            var dark = new Color32(6, 9, 9, 255);
            var light = new Color32(201, 211, 193, 255);
            for (var i = 0; i < pixels.Length; i++) pixels[i] = dark;
            var centre = new Vector2(63.5f, 63.5f);
            for (var tick = 0; tick < ticks; tick++)
            {
                var angle = (startDegrees + tick * stepDegrees) * Mathf.Deg2Rad;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var a = centre + direction * 48f;
                var b = centre + direction * 57f;
                for (var y = 0; y < size; y++)
                    for (var x = 0; x < size; x++)
                    {
                        var point = new Vector2(x, y);
                        var t = Mathf.Clamp01(Vector2.Dot(point - a, b - a) / (b - a).sqrMagnitude);
                        if ((point - Vector2.Lerp(a, b, t)).sqrMagnitude <= 3.1f)
                            pixels[y * size + x] = light;
                    }
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            texture.SetPixels32(pixels); texture.Apply(true, true); _textures.Add(texture);
            var material = Surface(name, Color.white, false);
            material.SetTexture("_BaseMap", texture);
            return material;
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
