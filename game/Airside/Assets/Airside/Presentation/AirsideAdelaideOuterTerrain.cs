using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Airside.Presentation
{
    /// <summary>
    /// ADR 0185 — draws <see cref="AdelaideOuterTerrainGeometry"/>: the land and Gulf out to about 96 km, so the camera can
    /// zoom far out and watch an arrival join from the approach. Coloured by height and sea only (no satellite image that far
    /// out); the terrain shaders push their haze out with the camera's far clip. Fails soft: without the far DEM the view
    /// simply ends at the 30 km ring as before.
    /// </summary>
    public static class AirsideAdelaideOuterTerrain
    {
        public const string ObjectName = "Adelaide Outer Terrain";
        private static readonly Color Hills = new Color(0.43f, 0.41f, 0.27f);

        public static bool TryBuild(Transform root, Shader shader)
        {
            try
            {
                var path = ArtRuntimePaths.ResolveExisting(AdelaideTerrainHeights.FarArtPath);
                if (root == null || shader == null || path == null)
                    return false;
                var terrain = AdelaideTerrainHeights.Parse(System.IO.File.ReadAllBytes(path));
                if (terrain == null)
                    return false;

                // The land-cover map is optional: without it the ring is coloured by height alone, as in ADR 0185.
                var landCover = LoadLandCover();
                if (landCover != null && landCover.Count != terrain.Count)
                    landCover = null;

                var pavementY = AirsideAdelaideGround.PavementWorldY;
                var result = AdelaideOuterTerrainGeometry.Build(terrain.Count, terrain.Spacing, terrain.Origin,
                    (xi, zi) => terrain.Sample(xi, zi), AdelaideTerrainHeights.ReliefAbovePlain,
                    pavementY - AirsideAdelaideSurroundings.PlainBelowPavement,
                    pavementY - AirsideAdelaideSurroundings.SeaBelowPavement,
                    Linear(AirsideAdelaideSurroundings.Plain), Linear(Hills), Linear(AirsideAdelaideSurroundings.DeepWater), landCover);
                if (result.Triangles.Length == 0)
                    return false;

                var vertices = new Vector3[result.VertexCount];
                var colours = new Color[result.VertexCount];
                for (var i = 0; i < vertices.Length; i++)
                {
                    vertices[i] = new Vector3(result.Positions[i * 3], result.Positions[i * 3 + 1], result.Positions[i * 3 + 2]);
                    colours[i] = new Color(result.Colours[i * 4], result.Colours[i * 4 + 1], result.Colours[i * 4 + 2], result.Colours[i * 4 + 3]);
                }

                var mesh = new Mesh { name = ObjectName, indexFormat = IndexFormat.UInt32 };
                mesh.vertices = vertices;
                mesh.colors = colours;
                mesh.triangles = result.Triangles;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();

                var material = AirsideAdelaideSurroundings.BuildMaterial(shader, AirsideAdelaideFarTerrain.SatelliteTexturePath,
                    AirsideAdelaideFarTerrain.SatelliteExtentMetres, "mat_adelaide_outer_terrain_v01");
                if (material == null)
                    return false;
                // Colour only: the satellite image ends at 30.5 km.
                material.SetFloat("_SatelliteNearStrength", 0f);
                material.SetFloat("_SatelliteFarStrength", 0f);
                material.SetFloat("_HorizonFadeStart", AirsideAdelaideSurroundings.FarHorizonFadeStartMetres);
                material.SetFloat("_HorizonFadeEnd", AirsideAdelaideSurroundings.FarHorizonFadeEndMetres);

                var go = new GameObject(ObjectName);
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Airside] Outer terrain failed to build: {e.Message}");
                return false;
            }
        }

        /// <summary>The far land-cover map, or null when it is missing or unreadable.</summary>
        public static AdelaideFarLandCover LoadLandCover()
        {
            var coverPath = ArtRuntimePaths.ResolveExisting(AdelaideFarLandCover.ArtPath);
            return coverPath == null ? null : AdelaideFarLandCover.Parse(System.IO.File.ReadAllBytes(coverPath));
        }

        internal static float[] Linear(Color c)
        {
            var l = c.linear;
            return new[] { l.r, l.g, l.b };
        }
    }
}
