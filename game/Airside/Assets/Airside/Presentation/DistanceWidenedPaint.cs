using System.Collections.Generic;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// A painted line (runway edge, centreline dashes) drawn as flat ribbons along the local X axis whose width follows
    /// <see cref="AirsidePaintWidening"/> node by node, so a 3 km edge line is wider where it is far and exact where it
    /// is near, with no steps. Rebuilds its vertices only when the camera has moved or zoomed.
    /// </summary>
    internal sealed class DistanceWidenedPaint : MonoBehaviour
    {
        /// <summary>Longest ribbon segment: sets how smoothly the width follows distance along a long line.</summary>
        public const float NodeSpacingMetres = 40f;

        private struct Ribbon
        {
            public int FirstNode;
            public int NodeCount;
            public float CentreZ;
            public float BaseWidth;
        }

        private readonly List<Ribbon> _ribbons = new();
        private readonly List<Vector2> _nodes = new();
        private Vector3[] _vertices;
        private Mesh _mesh;
        private float _y;
        private Camera _camera;
        private Vector3 _lastCamera = new(float.MaxValue, 0f, 0f);
        private float _lastFov;
        private float _lastHeight;

        public static void Create(Transform parent, string name, IEnumerable<(float CentreX, float CentreZ, float Length, float Width)> marks,
            float topY, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var paint = go.AddComponent<DistanceWidenedPaint>();
            paint._y = topY;
            foreach (var m in marks)
                paint.AddRibbon(m.CentreX, m.CentreZ, m.Length, m.Width);
            if (paint._ribbons.Count == 0)
            {
                Destroy(go);
                return;
            }

            paint.BuildMesh();
            go.AddComponent<MeshFilter>().sharedMesh = paint._mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            AirsideSceneIndex.Remember(go);
        }

        private void AddRibbon(float centreX, float centreZ, float length, float width)
        {
            var segments = Mathf.Max(1, Mathf.CeilToInt(length / NodeSpacingMetres));
            _ribbons.Add(new Ribbon { FirstNode = _nodes.Count, NodeCount = segments + 1, CentreZ = centreZ, BaseWidth = width });
            for (var i = 0; i <= segments; i++)
                _nodes.Add(new Vector2(centreX - length * 0.5f + length * i / segments, centreZ));
        }

        private void BuildMesh()
        {
            _vertices = new Vector3[_nodes.Count * 2];
            var normals = new Vector3[_vertices.Length];
            var triangles = new List<int>(_nodes.Count * 6);
            foreach (var ribbon in _ribbons)
            {
                for (var i = 0; i < ribbon.NodeCount; i++)
                {
                    var n = ribbon.FirstNode + i;
                    _vertices[n * 2] = new Vector3(_nodes[n].x, _y, _nodes[n].y - ribbon.BaseWidth * 0.5f);
                    _vertices[n * 2 + 1] = new Vector3(_nodes[n].x, _y, _nodes[n].y + ribbon.BaseWidth * 0.5f);
                    normals[n * 2] = normals[n * 2 + 1] = Vector3.up;
                    if (i + 1 == ribbon.NodeCount)
                        continue;
                    var v0 = n * 2;
                    // Clockwise seen from above, which is the front face.
                    triangles.Add(v0); triangles.Add(v0 + 1); triangles.Add(v0 + 3);
                    triangles.Add(v0); triangles.Add(v0 + 3); triangles.Add(v0 + 2);
                }
            }

            _mesh = new Mesh { name = name };
            _mesh.MarkDynamic();
            _mesh.vertices = _vertices;
            _mesh.normals = normals;
            _mesh.triangles = triangles.ToArray();
            _mesh.RecalculateBounds();
        }

        private void LateUpdate()
        {
            if (_mesh == null)
                return;
            if (_camera == null || !_camera.isActiveAndEnabled)
                _camera = Camera.main;
            if (_camera == null)
                return;

            var position = transform.InverseTransformPoint(_camera.transform.position);
            var fov = _camera.fieldOfView;
            var height = _camera.pixelHeight;
            // A metre of camera travel changes a width by well under a pixel's worth; skip the rebuild until it matters.
            if ((position - _lastCamera).sqrMagnitude < 1f && Mathf.Abs(fov - _lastFov) < 0.05f
                && Mathf.Abs(height - _lastHeight) < 0.5f)
                return;
            _lastCamera = position;
            _lastFov = fov;
            _lastHeight = height;

            foreach (var ribbon in _ribbons)
            {
                for (var i = 0; i < ribbon.NodeCount; i++)
                {
                    var n = ribbon.FirstNode + i;
                    var distance = Vector3.Distance(position, new Vector3(_nodes[n].x, _y, _nodes[n].y));
                    var half = AirsidePaintWidening.Width(ribbon.BaseWidth, distance, fov, height) * 0.5f;
                    _vertices[n * 2].z = ribbon.CentreZ - half;
                    _vertices[n * 2 + 1].z = ribbon.CentreZ + half;
                }
            }

            _mesh.vertices = _vertices;
            _mesh.RecalculateBounds();
        }

        private void OnDestroy()
        {
            if (_mesh != null)
                Destroy(_mesh);
        }
    }
}
