using System;
using Airside.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Airside.Presentation
{
    /// <summary>
    /// ADR 0208 — draws <see cref="AdelaideCbdSkylineGeometry"/> as one grey mesh
    /// ~7 km ENE of YPAD so overview / far zoom shows a CBD silhouette. Soft-fail
    /// when the table is empty or terrain is missing.
    /// </summary>
    public static class AirsideAdelaideCbdSkyline
    {
        public const string ObjectName = "Adelaide CBD Skyline";

        public static bool TryBuild(Transform root, AdelaideTerrainHeights terrain)
        {
            try
            {
                if (root == null || AdelaideCbdSkyline.Count == 0)
                    return false;

                var plainY = AirsideAdelaideGround.PavementWorldY
                    - AirsideAdelaideSurroundings.PlainBelowPavement;
                Func<float, float, float> heightAt = terrain != null
                    ? terrain.Height
                    : (_, __) => AdelaideTerrainHeights.PlainAboveSeaMetres;

                var result = AdelaideCbdSkylineGeometry.Build(plainY, heightAt);
                if (result.TriangleCount == 0)
                    return false;

                var vertices = new Vector3[result.VertexCount];
                var colours = new Color[result.VertexCount];
                for (var i = 0; i < vertices.Length; i++)
                {
                    vertices[i] = new Vector3(
                        result.Positions[i * 3],
                        result.Positions[i * 3 + 1],
                        result.Positions[i * 3 + 2]);
                    colours[i] = new Color(
                        result.Colours[i * 4],
                        result.Colours[i * 4 + 1],
                        result.Colours[i * 4 + 2],
                        result.Colours[i * 4 + 3]);
                }

                var mesh = new Mesh
                {
                    name = ObjectName,
                    indexFormat = IndexFormat.UInt16
                };
                mesh.vertices = vertices;
                mesh.colors = colours;
                mesh.triangles = result.Triangles;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();

                var go = new GameObject(ObjectName);
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = AirsideMaterialLibrary.CreateShared(
                    new Color(
                        AdelaideCbdSkylineGeometry.TowerLinear[0],
                        AdelaideCbdSkylineGeometry.TowerLinear[1],
                        AdelaideCbdSkylineGeometry.TowerLinear[2],
                        AdelaideCbdSkylineGeometry.TowerLinear[3]),
                    AirsideMaterialLibrary.SurfaceKind.Concrete);
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Airside] CBD skyline failed to build: {e.Message}");
                return false;
            }
        }
    }
}
