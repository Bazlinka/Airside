using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Airside.Presentation
{
    /// <summary>Fits articulation to the loaded kit's metal; never changes simulation datums.</summary>
    public static class AircraftLandingGearGeometry
    {
        public static Vector3 TopAttachment(Transform leg, Bounds fallback)
        {
            var mesh = leg.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh == null || !mesh.isReadable)
                return new Vector3(fallback.center.x, fallback.max.y, fallback.center.z);
            var vertices = mesh.vertices;
            var highest = float.MinValue;
            foreach (var v in vertices) highest = Mathf.Max(highest, leg.TransformPoint(v).y);
            var sum = Vector3.zero;
            var count = 0;
            foreach (var v in vertices)
            {
                var world = leg.TransformPoint(v);
                if (world.y < highest - 0.025f) continue;
                sum += world;
                count++;
            }
            return count > 0 ? sum / count : fallback.center;
        }

        public static void SplitTruckBeam(Transform strut, Transform truck, Vector3 axleCentre)
        {
            var filter = strut.GetComponent<MeshFilter>();
            var source = filter != null ? filter.sharedMesh : null;
            if (source == null || !source.isReadable) return;
            var vertices = source.vertices;
            var leg = new List<int>();
            var beam = new List<int>();
            var triangles = source.triangles;
            for (var i = 0; i < triangles.Length; i += 3)
            {
                var isBeam = true;
                for (var c = 0; c < 3; c++)
                    isBeam &= strut.TransformPoint(vertices[triangles[i + c]]).y <= axleCentre.y + 0.22f;
                var target = isBeam ? beam : leg;
                for (var c = 0; c < 3; c++) target.Add(triangles[i + c]);
            }
            if (beam.Count == 0 || leg.Count == 0) return;
            filter.sharedMesh = Extract(source, leg, v => v, source.name + " leg");
            var metal = new GameObject("Bogie beam " + truck.name).transform;
            metal.SetParent(truck, false);
            metal.gameObject.AddComponent<MeshFilter>().sharedMesh = Extract(source, beam,
                v => metal.InverseTransformPoint(strut.TransformPoint(v)), source.name + " bogie");
            metal.gameObject.AddComponent<MeshRenderer>().sharedMaterials = strut.GetComponent<Renderer>().sharedMaterials;
        }

        private static Mesh Extract(Mesh source, List<int> selected, Func<Vector3, Vector3> convert, string name)
        {
            var vertices = source.vertices;
            var uv = source.uv;
            var colours = source.colors;
            var resultVertices = new List<Vector3>();
            var resultUv = new List<Vector2>();
            var resultColours = new List<Color>();
            var indices = new List<int>();
            var map = new Dictionary<int, int>();
            foreach (var index in selected)
            {
                if (!map.TryGetValue(index, out var compact))
                {
                    compact = resultVertices.Count;
                    map.Add(index, compact);
                    resultVertices.Add(convert(vertices[index]));
                    if (uv.Length == vertices.Length) resultUv.Add(uv[index]);
                    if (colours.Length == vertices.Length) resultColours.Add(colours[index]);
                }
                indices.Add(compact);
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(resultVertices);
            mesh.SetTriangles(indices, 0);
            if (resultUv.Count > 0) mesh.SetUVs(0, resultUv);
            if (resultColours.Count > 0) mesh.SetColors(resultColours);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>The widebody kits have solid bay fairings but no animated leaves. Fit leaves to their skin.</summary>
        public static void AddMissingBayDoors(Transform aircraft)
        {
            var children = aircraft.GetComponentsInChildren<Transform>();
            Transform nose = null, fuselage = null;
            var hasDoors = false;
            var hasFairings = false;
            foreach (var child in children)
            {
                hasDoors |= AirsideAircraftParts.IsGearDoor(child.name);
                hasFairings |= child.name == "Gear fairing L";
                if (child.name == "Gear nose") nose = child;
                if (child.name == "Fuselage") fuselage = child;
            }
            // Existing kits keep their authored leaves, particularly the fitted Dash 8 nacelle doors.
            if (hasDoors || !hasFairings || nose == null || fuselage == null) return;
            var noseBounds = nose.GetComponent<Renderer>().bounds;
            var localNose = aircraft.InverseTransformPoint(noseBounds.center);
            var length = noseBounds.size.y + 0.9f;
            for (var side = -1; side <= 1; side += 2)
            {
                var minX = side < 0 ? -0.65f : 0.015f;
                var maxX = side < 0 ? -0.015f : 0.65f;
                AddSkinLeaf(aircraft, fuselage, "gear_door_nose_" + (side < 0 ? "l" : "r"),
                    minX, maxX, localNose.z + 0.28f, localNose.z + length);
                var fairingName = "Gear fairing " + (side < 0 ? "L" : "R");
                foreach (var child in children)
                {
                    if (child.name != fairingName) continue;
                    var b = child.GetComponent<MeshFilter>().sharedMesh.bounds;
                    // Fairing meshes still have kit-space vertices. Transform each bound into aircraft space.
                    var a = aircraft.InverseTransformPoint(child.TransformPoint(b.min));
                    var c = aircraft.InverseTransformPoint(child.TransformPoint(b.max));
                    var inner = side < 0 ? c.x - 0.03f : a.x + 0.03f;
                    var outer = side < 0 ? a.x + 0.6f : c.x - 0.6f;
                    AddSkinLeaf(aircraft, child, "Gear door " + (side < 0 ? "L" : "R"),
                        Mathf.Min(inner, outer), Mathf.Max(inner, outer), a.z + 0.1f, c.z - 0.1f);
                }
            }
        }

        private static void AddSkinLeaf(Transform aircraft, Transform skin, string name,
            float minX, float maxX, float minZ, float maxZ)
        {
            var filter = skin.GetComponent<MeshFilter>();
            if (filter == null || !filter.sharedMesh.isReadable) return;
            var mesh = filter.sharedMesh;
            var points = mesh.vertices;
            var triangles = mesh.triangles;
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            for (var i = 0; i < triangles.Length; i += 3)
            {
                var a = aircraft.InverseTransformPoint(skin.TransformPoint(points[triangles[i]]));
                var b = aircraft.InverseTransformPoint(skin.TransformPoint(points[triangles[i + 1]]));
                var c = aircraft.InverseTransformPoint(skin.TransformPoint(points[triangles[i + 2]]));
                var normal = Vector3.Cross(b - a, c - a).normalized;
                if (normal.y > -0.2f) continue;
                var polygon = new List<Vector3> { a, b, c };
                polygon = Clip(polygon, 0, minX, true);
                polygon = Clip(polygon, 0, maxX, false);
                polygon = Clip(polygon, 2, minZ, true);
                polygon = Clip(polygon, 2, maxZ, false);
                if (polygon.Count < 3) continue;
                var first = vertices.Count;
                foreach (var v in polygon) vertices.Add(v + normal * 0.008f);
                for (var j = 1; j < polygon.Count - 1; j++)
                {
                    indices.AddRange(new[] { first, first + j, first + j + 1 });
                    indices.AddRange(new[] { first, first + j + 1, first + j });
                }
            }
            if (vertices.Count == 0) return;
            var door = new GameObject(name).transform;
            door.SetParent(aircraft, false);
            var leaf = new Mesh { name = name + " fitted skin" };
            leaf.SetVertices(vertices);
            leaf.SetTriangles(indices, 0);
            // Double-sided triangles share normals; set the outward-facing underside explicitly.
            var normals = new Vector3[vertices.Count];
            for (var i = 0; i < normals.Length; i++) normals[i] = Vector3.down;
            leaf.normals = normals;
            leaf.RecalculateBounds();
            door.gameObject.AddComponent<MeshFilter>().sharedMesh = leaf;
            var material = Object.Instantiate(skin.GetComponent<Renderer>().sharedMaterial);
            material.name = "Original fitted gear bay leaf";
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(0.82f, 0.84f, 0.85f));
            door.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static List<Vector3> Clip(List<Vector3> input, int axis, float edge, bool greater)
        {
            var output = new List<Vector3>();
            if (input.Count == 0) return output;
            var previous = input[input.Count - 1];
            var previousInside = greater ? previous[axis] >= edge : previous[axis] <= edge;
            foreach (var current in input)
            {
                var inside = greater ? current[axis] >= edge : current[axis] <= edge;
                if (inside != previousInside)
                    output.Add(Vector3.LerpUnclamped(previous, current, (edge - previous[axis]) / (current[axis] - previous[axis])));
                if (inside) output.Add(current);
                previous = current;
                previousInside = inside;
            }
            return output;
        }
    }
}
