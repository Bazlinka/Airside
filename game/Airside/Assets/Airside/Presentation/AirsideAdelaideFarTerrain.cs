using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Airside.Presentation
{
    /// <summary>
    /// ADR 0158 (plan P6) — the land and Gulf beyond the ±12 km surroundings, out to the 30 km far
    /// clip: the rest of the Adelaide plain, the CBD rise and the Adelaide Hills as the eastern
    /// skyline, on real Copernicus heights and draped with the far Sentinel-2 image toned like the
    /// near one. One mesh, one material, 250 m cells; the square the surroundings own is left out
    /// but for a narrow band tucked just under their edge so no sky shows through the join.
    /// </summary>
    public static class AirsideAdelaideFarTerrain
    {
        public const string ObjectName = "Adelaide Far Terrain";
        public const string SatelliteTexturePath = "Textures/Environment/tx_adelaide_sentinel2_l2a_far_v01.jpg";
        public const float SatelliteExtentMetres = 30500f;
        public const float RadiusMetres = 30000f;

        /// <summary>
        /// Every DEM sample: 125 m cells (ADR 0247). Stride 2 threw away half the Hills' relief, so
        /// ridges and gullies read as a faceted lump when the camera pulled out over them.
        /// </summary>
        public const int Stride = 1;

        /// <summary>Cells wholly inside this half-size belong to the surroundings mesh.</summary>
        public const float InnerHalfMetres = CoastGrid.ExtentMetres - 300f;

        /// <summary>The overlap band sits this far under the surroundings so the two never fight.</summary>
        public const float TuckMetres = 3f;

        public static bool TryBuild(Transform root, AdelaideTerrainHeights terrain, Shader shader)
        {
            if (root == null || terrain == null || shader == null)
                return false;
            try
            {
                var material = AirsideAdelaideSurroundings.BuildMaterial(shader, SatelliteTexturePath,
                    SatelliteExtentMetres, "mat_adelaide_far_terrain_v01");
                if (material == null || material.GetTexture("_SatelliteAlbedo") == null)
                    return false;
                // Beyond 12 km the image is all there is: the same strength the surroundings reach
                // at their own far edge, and fade to fog just before the far clip.
                material.SetFloat("_SatelliteNearStrength", AirsideAdelaideSurroundings.SatelliteFarStrength);
                material.SetFloat("_SatelliteFarStrength", AirsideAdelaideSurroundings.SatelliteFarStrength);
                material.SetFloat("_HorizonFadeStart", AirsideAdelaideSurroundings.FarHorizonFadeStartMetres);
                material.SetFloat("_HorizonFadeEnd", AirsideAdelaideSurroundings.FarHorizonFadeEndMetres);

                var mesh = BuildMesh(terrain, AirsideAdelaideGround.PavementWorldY);
                if (mesh == null)
                    return false;
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
                Debug.LogWarning($"[Airside] Far terrain failed to build: {e.Message}");
                return false;
            }
        }

        public static Mesh BuildMesh(AdelaideTerrainHeights terrain, float pavementWorldY)
        {
            if (terrain == null)
                return null;
            var n = (terrain.Count - 1) / Stride + 1;
            var vertices = new Vector3[n * n];
            var colors = new Color[n * n];
            var sea = AirsideAdelaideSurroundings.DeepWater.linear;
            sea.a = 1f;
            var land = AirsideAdelaideSurroundings.Plain.linear;
            land.a = 0f;
            var plainY = pavementWorldY - AirsideAdelaideSurroundings.PlainBelowPavement;
            var seaY = pavementWorldY - AirsideAdelaideSurroundings.SeaBelowPavement;
            for (var zi = 0; zi < n; zi++)
            for (var xi = 0; xi < n; xi++)
            {
                var x = terrain.Origin + xi * Stride * terrain.Spacing;
                var z = terrain.Origin + zi * Stride * terrain.Spacing;
                var h = terrain.Sample(xi * Stride, zi * Stride);
                var i = zi * n + xi;
                var inside = Mathf.Abs(x) < InnerHalfMetres + 1f && Mathf.Abs(z) < InnerHalfMetres + 1f;
                // Sea is stored as 0 m. It is the stylised Gulf, as the surroundings' open water is
                // past its shallows, so the two meet at their edge in the same colour.
                if (h <= 0.01f)
                {
                    vertices[i] = new Vector3(x, seaY - (inside ? TuckMetres : 0f), z);
                    colors[i] = sea;
                    continue;
                }

                var y = plainY + AdelaideTerrainHeights.ReliefAbovePlain(h);
                vertices[i] = new Vector3(x, y - (inside ? TuckMetres : 0f), z);
                colors[i] = land;
            }

            var triangles = new List<int>(n * n * 3);
            var radiusSq = RadiusMetres * RadiusMetres;
            for (var zi = 0; zi < n - 1; zi++)
            for (var xi = 0; xi < n - 1; xi++)
            {
                var a = zi * n + xi;
                var b = a + 1;
                var c = a + n;
                var d = c + 1;
                if (!Keep(vertices[a], vertices[d], radiusSq))
                    continue;
                triangles.Add(a); triangles.Add(c); triangles.Add(b);
                triangles.Add(b); triangles.Add(c); triangles.Add(d);
            }

            if (triangles.Count == 0)
                return null;
            var mesh = new Mesh { name = ObjectName, indexFormat = IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// A cell (corners <paramref name="lo"/>, <paramref name="hi"/>) is drawn when it reaches into
        /// the disc and is not wholly inside the square the surroundings mesh owns.
        /// </summary>
        public static bool Keep(Vector3 lo, Vector3 hi, float radiusSq)
        {
            var nearestX = Mathf.Clamp(0f, Mathf.Min(lo.x, hi.x), Mathf.Max(lo.x, hi.x));
            var nearestZ = Mathf.Clamp(0f, Mathf.Min(lo.z, hi.z), Mathf.Max(lo.z, hi.z));
            if (nearestX * nearestX + nearestZ * nearestZ > radiusSq)
                return false;
            var insideSquare = Mathf.Max(Mathf.Abs(lo.x), Mathf.Abs(hi.x)) <= InnerHalfMetres
                               && Mathf.Max(Mathf.Abs(lo.z), Mathf.Abs(hi.z)) <= InnerHalfMetres;
            return !insideSquare;
        }
    }
}
