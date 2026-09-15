using System.Collections.Generic;
using Airside.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Airside.Presentation
{
    /// <summary>
    /// Arterial road ribbons around YPAD from <see cref="AdelaideLandCover.Roads"/> —
    /// motorway / trunk / primary / secondary centreline strips in the Airside palette.
    /// Presentation only; fails soft when the shader or road data is missing.
    /// </summary>
    public static class AirsideAdelaideRoads
    {
        public const string ObjectName = "Adelaide Roads";
        public const string ShaderName = "Airside/Surroundings";

        private const int MaxRoads = 480;
        private const float MinWidthMetres = 9f;
        private const float YOffsetMetres = 0.35f;

        private static readonly Color Asphalt = new(0.28f, 0.30f, 0.31f);

        public static bool TryBuild(Transform root, float pavementWorldY, System.Func<float, float, float> groundHeight = null)
        {
            try
            {
                var shader = Shader.Find(ShaderName);
                if (shader == null || AdelaideLandCover.Roads == null || AdelaideLandCover.Roads.Length < 3)
                    return false;

                var mesh = BuildMesh(pavementWorldY, groundHeight);
                if (mesh == null || mesh.vertexCount < 3)
                    return false;

                var go = new GameObject(ObjectName);
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = new Material(shader)
                {
                    name = "mat_adelaide_roads_v01",
                    enableInstancing = true
                };
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Airside] Roads failed to build: {e.Message}");
                return false;
            }
        }

        /// <param name="groundHeight">
        /// Surface height at a world x,z. Without it roads lie on one fixed plane, which put
        /// them metres above the beach and inland water and under parts of the airfield lip.
        /// </param>
        public static Mesh BuildMesh(float pavementWorldY, System.Func<float, float, float> groundHeight = null)
        {
            var roads = AdelaideLandCover.Roads;
            var vertices = new List<Vector3>(4096);
            var colors = new List<Color>(4096);
            var triangles = new List<int>(8192);
            var y = pavementWorldY - 1.45f + YOffsetMetres; // sit just above the plain
            var asphalt = Asphalt.linear;
            asphalt.a = 0f;

            var i = 0;
            var drawn = 0;
            while (i < roads.Length)
            {
                var width = roads[i++];
                if (width <= 0f)
                    break;
                if (i >= roads.Length)
                    break;
                var count = (int)roads[i++];
                if (count < 2 || i + count * 2 > roads.Length)
                    break;

                if (width >= MinWidthMetres && drawn < MaxRoads)
                {
                    var half = width * 0.5f;
                    var start = vertices.Count;
                    Vector3? prevLeft = null;
                    Vector3? prevRight = null;
                    for (var p = 0; p < count; p++)
                    {
                        var x = roads[i + p * 2];
                        var z = roads[i + p * 2 + 1];
                        // Skip the operational core — pavement owns that.
                        if (AdelaideLandCover.InOperationalCore(x, z))
                        {
                            prevLeft = prevRight = null;
                            continue;
                        }

                        float dx, dz;
                        if (p + 1 < count)
                        {
                            dx = roads[i + (p + 1) * 2] - x;
                            dz = roads[i + (p + 1) * 2 + 1] - z;
                        }
                        else
                        {
                            dx = x - roads[i + (p - 1) * 2];
                            dz = z - roads[i + (p - 1) * 2 + 1];
                        }
                        var len = Mathf.Sqrt(dx * dx + dz * dz);
                        if (len < 1e-3f)
                        {
                            dx = 1f;
                            dz = 0f;
                            len = 1f;
                        }
                        dx /= len;
                        dz /= len;
                        // Perpendicular in XZ.
                        var px = -dz * half;
                        var pz = dx * half;
                        var ly = groundHeight != null
                            ? Mathf.Max(groundHeight(x + px, z + pz), groundHeight(x, z)) + YOffsetMetres
                            : y;
                        var ry = groundHeight != null
                            ? Mathf.Max(groundHeight(x - px, z - pz), groundHeight(x, z)) + YOffsetMetres
                            : y;
                        var left = new Vector3(x + px, ly, z + pz);
                        var right = new Vector3(x - px, ry, z - pz);
                        if (prevLeft.HasValue)
                        {
                            var a = vertices.Count;
                            vertices.Add(prevLeft.Value);
                            vertices.Add(prevRight.Value);
                            vertices.Add(left);
                            vertices.Add(right);
                            colors.Add(asphalt);
                            colors.Add(asphalt);
                            colors.Add(asphalt);
                            colors.Add(asphalt);
                            triangles.Add(a);
                            triangles.Add(a + 2);
                            triangles.Add(a + 1);
                            triangles.Add(a + 1);
                            triangles.Add(a + 2);
                            triangles.Add(a + 3);
                        }
                        prevLeft = left;
                        prevRight = right;
                    }
                    if (vertices.Count > start)
                        drawn++;
                }

                i += count * 2;
            }

            if (vertices.Count < 3)
                return null;

            var mesh = new Mesh
            {
                name = "Adelaide Roads",
                indexFormat = IndexFormat.UInt32
            };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
