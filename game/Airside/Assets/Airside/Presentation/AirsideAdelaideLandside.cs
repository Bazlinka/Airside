using System.Collections.Generic;
using Airside.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Airside.Presentation
{
    /// <summary>
    /// T1 traffic-side pavement: drop-off boulevard, return loop, connectors and
    /// the short-stay pad. Presentation only; coordinates live in
    /// <see cref="AdelaideLandside"/>.
    /// </summary>
    public static class AirsideAdelaideLandside
    {
        public const string ObjectName = "Adelaide Landside";
        public const string ShaderName = "Airside/Surroundings";

        private static readonly Color Asphalt = new(0.30f, 0.32f, 0.33f);
        private static readonly Color Pad = new(0.27f, 0.29f, 0.30f);

        public static bool TryBuild(Transform root, float pavementWorldY)
        {
            try
            {
                var shader = Shader.Find(ShaderName);
                if (shader == null)
                    return false;

                var mesh = BuildMesh(pavementWorldY);
                if (mesh == null || mesh.vertexCount < 3)
                    return false;

                var go = new GameObject(ObjectName);
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = new Material(shader)
                {
                    name = "mat_adelaide_landside_v01",
                    enableInstancing = true
                };
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = true;
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Airside] Landside failed to build: {e.Message}");
                return false;
            }
        }

        public static Mesh BuildMesh(float pavementWorldY)
        {
            var vertices = new List<Vector3>(512);
            var colors = new List<Color>(512);
            var triangles = new List<int>(1024);
            var y = pavementWorldY + 0.08f;
            var asphalt = Asphalt.linear;
            asphalt.a = 0f;
            var pad = Pad.linear;
            pad.a = 0f;

            foreach (var (width, xz) in AdelaideLandside.Ribbons)
                AppendRibbon(vertices, colors, triangles, xz, width * 0.5f, y, asphalt);

            AppendPad(vertices, colors, triangles,
                AdelaideLandside.CarParkCentreX, AdelaideLandside.CarParkCentreZ,
                AdelaideLandside.CarParkHalfX, AdelaideLandside.CarParkHalfZ, y, pad);

            if (vertices.Count < 3)
                return null;

            var mesh = new Mesh
            {
                name = ObjectName,
                indexFormat = IndexFormat.UInt32
            };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AppendPad(List<Vector3> vertices, List<Color> colors, List<int> triangles,
            float cx, float cz, float hx, float hz, float y, Color color)
        {
            var a = vertices.Count;
            vertices.Add(new Vector3(cx - hx, y, cz - hz));
            vertices.Add(new Vector3(cx + hx, y, cz - hz));
            vertices.Add(new Vector3(cx - hx, y, cz + hz));
            vertices.Add(new Vector3(cx + hx, y, cz + hz));
            colors.Add(color);
            colors.Add(color);
            colors.Add(color);
            colors.Add(color);
            triangles.Add(a);
            triangles.Add(a + 2);
            triangles.Add(a + 1);
            triangles.Add(a + 1);
            triangles.Add(a + 2);
            triangles.Add(a + 3);
        }

        private static void AppendRibbon(List<Vector3> vertices, List<Color> colors, List<int> triangles,
            float[] xz, float half, float y, Color color)
        {
            if (xz == null || xz.Length < 4)
                return;
            var count = xz.Length / 2;
            Vector3? prevLeft = null;
            Vector3? prevRight = null;
            for (var p = 0; p < count; p++)
            {
                var x = xz[p * 2];
                var z = xz[p * 2 + 1];
                float dx, dz;
                if (p + 1 < count)
                {
                    dx = xz[(p + 1) * 2] - x;
                    dz = xz[(p + 1) * 2 + 1] - z;
                }
                else
                {
                    dx = x - xz[(p - 1) * 2];
                    dz = z - xz[(p - 1) * 2 + 1];
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
                var px = -dz * half;
                var pz = dx * half;
                var left = new Vector3(x + px, y, z + pz);
                var right = new Vector3(x - px, y, z - pz);
                if (prevLeft.HasValue)
                {
                    var a = vertices.Count;
                    vertices.Add(prevLeft.Value);
                    vertices.Add(prevRight.Value);
                    vertices.Add(left);
                    vertices.Add(right);
                    colors.Add(color);
                    colors.Add(color);
                    colors.Add(color);
                    colors.Add(color);
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
        }
    }
}
