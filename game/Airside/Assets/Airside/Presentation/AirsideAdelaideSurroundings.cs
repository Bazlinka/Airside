using System;
using Airside.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Airside.Presentation
{
    /// <summary>
    /// Land, beach and the Gulf St Vincent around the airfield, from the real OSM coastline
    /// (<see cref="AdelaideCoast"/>). One vertex-coloured heightfield built from
    /// <see cref="CoastGrid"/>: it meets the airfield ground mesh exactly on its edge (and
    /// tucks under it), eases down to the coastal plain, slopes over a beach and lies flat
    /// as sea. Stylised to the Airside palette rather than photographic — it is there so
    /// the overview reads as Adelaide. Presentation only; fails soft to no surroundings.
    /// </summary>
    public static class AirsideAdelaideSurroundings
    {
        public const string ObjectName = "Adelaide Surroundings";
        public const string ShaderName = "Airside/Surroundings";

        // Heights relative to the pavement: the airport sits ~5 m above the gulf.
        private const float PlainBelowPavement = 1.6f;
        private const float BeachBelowPavement = 4.2f;
        private const float SeaBelowPavement = 5.2f;
        private const float TuckUnderMetres = 4f;
        private const float EdgeBlendMetres = 700f;
        // Wider than the real sand so the 60 m grid draws a continuous strip, not dashes.
        private const float BeachWidthMetres = 150f;

        // Tuned against the airfield ground as rendered in a packaged build (measured pixel
        // values; the tonemapper makes these sensitive), so the field edge disappears.
        private static readonly Color AirfieldEdge = new(0.575f, 0.595f, 0.43f);
        private static readonly Color Plain = new(0.555f, 0.57f, 0.42f);
        private static readonly Color Suburb = new(0.585f, 0.575f, 0.53f);
        private static readonly Color Park = new(0.46f, 0.53f, 0.39f);
        private static readonly Color Beach = new(0.74f, 0.69f, 0.55f);
        private static readonly Color Shallows = new(0.30f, 0.53f, 0.58f);
        private static readonly Color DeepWater = new(0.14f, 0.32f, 0.44f);

        public static bool TryBuild(Transform root)
        {
            try
            {
                var shader = Shader.Find(ShaderName);
                if (shader == null)
                {
                    Debug.LogWarning($"[Airside] {ShaderName} not in build; surroundings skipped.");
                    return false;
                }

                var grid = new CoastGrid(AdelaideCoast.SeaPolygon, AdelaideCoast.Coastline,
                    AirsideAdelaideGround.SizeX * 0.5f, AirsideAdelaideGround.SizeZ * 0.5f);
                var mesh = BuildMesh(grid);

                var go = new GameObject(ObjectName);
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = new Material(shader) { name = "mat_adelaide_surroundings_v01", enableInstancing = true };
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Airside] Surroundings failed to build: {e.Message}");
                return false;
            }
        }

        public static Mesh BuildMesh(CoastGrid grid)
        {
            var nx = grid.CountX;
            var nz = grid.CountZ;
            var vertices = new Vector3[nx * nz];
            var colors = new Color[nx * nz];
            var pavement = AirsideAdelaideGround.PavementWorldY;

            for (var zi = 0; zi < nz; zi++)
            for (var xi = 0; xi < nx; xi++)
            {
                var x = grid.X(xi);
                var z = grid.Z(zi);
                var i = zi * nx + xi;
                var coast = grid.CoastDistance(xi, zi);

                if (grid.IsSea(xi, zi))
                {
                    vertices[i] = new Vector3(x, pavement - SeaBelowPavement, z);
                    var c = Color.Lerp(Shallows, DeepWater, Mathf.SmoothStep(0f, 1f, coast / CoastGrid.MaxDistanceMetres));
                    c = c.linear;
                    c.a = 1f;
                    colors[i] = c;
                    continue;
                }

                // Land: meet the airfield edge height, ease to the plain, dip over the beach.
                var edgeX = Mathf.Clamp(x, -grid.HoleHalfX, grid.HoleHalfX);
                var edgeZ = Mathf.Clamp(z, -grid.HoleHalfZ, grid.HoleHalfZ);
                var edgeHeight = AirsideAdelaideGround.WorldHeight(edgeX, edgeZ);
                var outside = grid.DistanceOutsideHole(x, z);
                var height = Mathf.Lerp(edgeHeight, pavement - PlainBelowPavement,
                    Mathf.SmoothStep(0f, 1f, outside / EdgeBlendMetres));
                var beach = 1f - Mathf.SmoothStep(0f, 1f, coast / BeachWidthMetres);
                height = Mathf.Lerp(height, pavement - BeachBelowPavement, beach);
                if (grid.IsTuckedUnder(xi, zi))
                    height = edgeHeight - TuckUnderMetres;

                vertices[i] = new Vector3(x, height, z);
                colors[i] = LandColour(x, z, beach, outside);
            }

            var triangles = new System.Collections.Generic.List<int>((nx - 1) * (nz - 1) * 6);
            for (var zi = 0; zi < nz - 1; zi++)
            for (var xi = 0; xi < nx - 1; xi++)
            {
                if (grid.IsCellCovered(xi, zi))
                    continue;
                var a = zi * nx + xi;
                var b = a + 1;
                var c = a + nx;
                var d = c + 1;
                triangles.Add(a); triangles.Add(c); triangles.Add(b);
                triangles.Add(b); triangles.Add(c); triangles.Add(d);
            }

            var mesh = new Mesh { name = "Adelaide Surroundings", indexFormat = IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Coastal plain in broad, soft patches — suburbs, parks, open ground — so it is not
        /// one flat card from the overview, then sand along the beach.
        /// </summary>
        private static Color LandColour(float x, float z, float beach, float outsideAirfield)
        {
            var patches = Noise(x / 900f, z / 900f) * 0.65f + Noise(x / 260f + 11.3f, z / 260f - 4.1f) * 0.35f;
            var land = Color.Lerp(Plain, Suburb, Mathf.SmoothStep(0.35f, 0.75f, patches));
            land = Color.Lerp(land, Park, Mathf.SmoothStep(0.72f, 0.9f, Noise(x / 500f - 7.7f, z / 500f + 3.2f)) * 0.8f);
            // Match the airfield's dry grass for the first few hundred metres out.
            land = Color.Lerp(AirfieldEdge, land, Mathf.SmoothStep(0f, 1f, outsideAirfield / 900f));
            // Palette is authored in sRGB; vertex colours are read as linear in this project.
            var colour = Color.Lerp(land, Beach, Mathf.SmoothStep(0f, 1f, beach)).linear;
            colour.a = 0f;
            return colour;
        }

        /// <summary>Smooth value noise in [0, 1].</summary>
        private static float Noise(float x, float z)
        {
            var ix = Mathf.FloorToInt(x);
            var iz = Mathf.FloorToInt(z);
            var fx = x - ix;
            var fz = z - iz;
            var ux = fx * fx * (3f - 2f * fx);
            var uz = fz * fz * (3f - 2f * fz);
            var a = Hash(ix, iz);
            var b = Hash(ix + 1, iz);
            var c = Hash(ix, iz + 1);
            var d = Hash(ix + 1, iz + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, ux), Mathf.Lerp(c, d, ux), uz);
        }

        private static float Hash(int x, int z)
        {
            unchecked
            {
                var h = (uint)(x * 374761393 + z * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
            }
        }
    }
}
