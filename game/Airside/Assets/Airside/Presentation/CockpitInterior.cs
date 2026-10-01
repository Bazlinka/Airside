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

        public void Enter()
        {
            if (_entered) return;
            _entered = true;
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

        protected bool KeepExteriorPart(Transform part)
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

        protected Transform Box(string name, Vector3 pos, Vector3 size, Material mat, bool bevelled = true)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(transform, false);
            part.transform.localPosition = pos;
            part.transform.localScale = size;
            var bevel = bevelled ? AirsidePrototype.CockpitBoxMesh(size) : null;
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
