using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Airside.Presentation
{
    /// <summary>Shared fitted detail pass for all authored fleet kits, before their existing rigs are assembled.</summary>
    public static class AircraftSurfaceDetails
    {
        public const string DetailName = "Aircraft surface details";
        private static readonly Color Seam = new(0.31f, 0.36f, 0.39f);
        private static readonly Color Hardware = new(0.61f, 0.65f, 0.67f);

        public static void Build(Transform aircraft)
        {
            if (aircraft == null) return;
            // Snapshot: detail children must never become source panels during this pass.
            foreach (var filter in aircraft.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || !filter.sharedMesh.isReadable
                    || filter.transform.Find(DetailName) != null)
                    continue;
                var name = filter.name.Replace('_', ' ').ToLowerInvariant();
                var cargo = name == "cargo door";
                var sliding = name.Contains("sliding door");
                var door = cargo || sliding || name.StartsWith("cabindoor", StringComparison.Ordinal)
                    || name == "door fwd" || name == "door service aft"
                    || name.StartsWith("door left ", StringComparison.Ordinal)
                    || name.StartsWith("door right ", StringComparison.Ordinal);
                var hull = name == "fuselage" || name.EndsWith(" fuselage", StringComparison.Ordinal);
                if (!door && !hull) continue;

                var matrix = aircraft.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var original = filter.sharedMesh.vertices;
                if (original.Length == 0) continue;
                var skin = new float[original.Length * 3];
                var bounds = new Bounds(matrix.MultiplyPoint3x4(original[0]), Vector3.zero);
                for (var i = 0; i < original.Length; i++)
                {
                    var v = matrix.MultiplyPoint3x4(original[i]);
                    bounds.Encapsulate(v);
                    skin[i * 3] = v.x; skin[i * 3 + 1] = v.y; skin[i * 3 + 2] = v.z;
                }
                var triangles = filter.sharedMesh.triangles;
                var details = AircraftSurfaceDetailGeometry.Build(skin, triangles, door, cargo, sliding,
                    bounds.center.x, bounds.min.y, bounds.max.y, bounds.min.z, bounds.max.z);
                Add(filter.transform, aircraft, details.Trim, DetailName, Seam);
                Add(filter.transform, aircraft, details.Hardware, DetailName + " hardware", Hardware);
            }
            AirsideNamedChildren.Forget(aircraft);
        }

        private static void Add(Transform panel, Transform aircraft, List<AircraftSurfaceDetailGeometry.Patch> patches,
            string name, Color colour)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var matrix = panel.worldToLocalMatrix * aircraft.localToWorldMatrix;
            foreach (var patch in patches)
            {
                var start = vertices.Count;
                for (var i = 0; i < patch.Positions.Length; i += 3)
                    vertices.Add(matrix.MultiplyPoint3x4(new Vector3(patch.Positions[i], patch.Positions[i + 1], patch.Positions[i + 2])));
                foreach (var index in patch.Triangles) triangles.Add(start + index);
            }
            if (triangles.Count == 0) return;
            var go = new GameObject(name);
            go.transform.SetParent(panel, false);
            var mesh = new Mesh { name = panel.name + " " + name,
                indexFormat = vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            // Door hinge rebakes read these children later, so retain the CPU copy.
            go.AddComponent<AirsideGeneratedMeshOwner>().Mesh = mesh;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = AircraftLiveryPaint.MaterialFor("door_handle_detail", colour);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            AirsideSceneIndex.Remember(go.transform);
        }
    }
}
