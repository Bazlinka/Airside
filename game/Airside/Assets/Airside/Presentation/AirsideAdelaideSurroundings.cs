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
    /// as sea, then tinted by real OSM land cover (<see cref="AdelaideLandCover"/>) so
    /// parks, suburbs, car parks and the Patawalonga read in the right place. Stylised
    /// to the Airside palette rather than photographic — it is there so the overview
    /// reads as Adelaide. A runway-aligned ESA Sentinel-2 composite supplies the broad
    /// albedo while the mesh still supplies playable heights and water response.
    /// Presentation only; fails soft to no surroundings.
    /// </summary>
    public static class AirsideAdelaideSurroundings
    {
        public const string ObjectName = "Adelaide Surroundings";
        public const string ShaderName = "Airside/Surroundings";

        // Heights relative to the pavement: the airport sits ~5 m above the gulf.
        internal const float PlainBelowPavement = 1.6f;
        private const float BeachBelowPavement = 4.2f;
        internal const float SeaBelowPavement = 5.2f;
        private const float InlandWaterBelowPavement = 4.8f;
        private const float TuckUnderMetres = 4f;
        private const float EdgeBlendMetres = 700f;
        /// <summary>
        /// How far the satellite/grass mix eases across the airfield rectangle.
        /// Must match the ground shader's <c>_SatelliteEdgeBlend</c> so both
        /// meshes paint the same colour on the join — 2 m left a grass ring
        /// against the field's satellite edge and read as a bright hairline.
        /// </summary>
        public const float EdgeTextureBlendMetres = 1050f;

        /// <summary>Satellite is colour context, not near-field detail: 10 m source pixels blur the airport edge.</summary>
        public const float SatelliteNearStrength = 0.78f;
        public const float SatelliteFarStrength = 0.92f;
        public const float SatelliteFarBlendStartMetres = 1800f;
        public const float SatelliteFarBlendEndMetres = 5200f;
        private const float BeachWidthMetres = 55f;
        public const float SatelliteExtentMetres = 12000f;
        /// <summary>
        /// A median of nine cloud-free summer Sentinel-2 L2A scenes (2024-2026) at native 10 m,
        /// 4096 px over the ±12 km square (<c>scripts/generate-adelaide-satellite-s2.py</c>).
        /// Replaced the 2021 WorldCover WMS bake, whose Gulf carried a smeared tile gap.
        /// </summary>
        public const string SatelliteTexturePath =
            "Textures/Environment/tx_adelaide_sentinel2_l2a_v02.jpg";

        // Tuned against the airfield ground as rendered in a packaged build (measured pixel
        // values; the tonemapper makes these sensitive), so the field edge disappears.
        private static readonly Color AirfieldEdge = new(0.575f, 0.595f, 0.43f);
        internal static readonly Color Plain = new(0.555f, 0.57f, 0.42f);
        private static readonly Color Suburb = new(0.585f, 0.575f, 0.53f);
        private static readonly Color Park = new(0.46f, 0.53f, 0.39f);
        private static readonly Color Commercial = new(0.62f, 0.60f, 0.56f);
        private static readonly Color Parking = new(0.42f, 0.43f, 0.41f);
        private static readonly Color Scrub = new(0.52f, 0.55f, 0.40f);
        private static readonly Color Beach = new(0.74f, 0.69f, 0.55f);
        // Slightly greener shallows / deeper gulf blue — closer to WLD-004 / Coastal Blue.
        private static readonly Color Shallows = new(0.28f, 0.55f, 0.58f);
        internal static readonly Color DeepWater = new(0.12f, 0.30f, 0.42f);
        private static readonly Color InlandWater = new(0.26f, 0.50f, 0.54f);

        public static bool TryBuild(Transform root) => TryBuild(root, out _);

        /// <summary>
        /// Real ground heights (ADR 0158), loaded once. Null when the baked DEM is missing: the
        /// surroundings then stay the flat plain they were, and no far ring is built.
        /// </summary>
        public static AdelaideTerrainHeights Terrain
        {
            get
            {
                if (_terrainLoaded)
                    return _terrain;
                _terrainLoaded = true;
                try
                {
                    var path = ArtRuntimePaths.ResolveExisting(AdelaideTerrainHeights.ArtPath);
                    _terrain = path != null ? AdelaideTerrainHeights.Parse(System.IO.File.ReadAllBytes(path)) : null;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Airside] Adelaide terrain unreadable: {e.Message}");
                    _terrain = null;
                }

                return _terrain;
            }
        }

        private static AdelaideTerrainHeights _terrain;
        private static bool _terrainLoaded;

        /// <summary>
        /// Height the landside roads should follow: the pavement plane they have always used,
        /// raised by the same relief the surroundings get, so an arterial never sinks into a rise.
        /// </summary>
        public static Func<float, float, float> RoadHeight(float pavementWorldY)
        {
            var terrain = Terrain;
            if (terrain == null)
                return null;
            var halfX = AirsideAdelaideGround.SizeX * 0.5f;
            var halfZ = AirsideAdelaideGround.SizeZ * 0.5f;
            return (x, z) =>
            {
                var dx = Mathf.Max(0f, Mathf.Abs(x) - halfX);
                var dz = Mathf.Max(0f, Mathf.Abs(z) - halfZ);
                return pavementWorldY + terrain.Relief(x, z, Mathf.Sqrt(dx * dx + dz * dz));
            };
        }

        /// <summary>
        /// With the far ring in place the land runs on to the far clip, so the surroundings fade
        /// to the fog colour just before it instead of at a fixed ~10 km wall; the weather's own
        /// fog now decides how far you can see (60 km on a clear day, a few hundred metres in fog).
        /// </summary>
        public const float FarHorizonFadeStartMetres = 25500f;
        public const float FarHorizonFadeEndMetres = 29500f;

        public static bool TryBuild(Transform root, out Material material)
        {
            material = null;
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
                var mesh = BuildMesh(grid, out var heights, Terrain);

                var go = new GameObject(ObjectName);
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                material = BuildMaterial(shader);
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                // The Hills and the rest of the plain out to the far clip (plan P6).
                if (Terrain != null && material != null && AirsideAdelaideFarTerrain.TryBuild(root, Terrain, shader))
                {
                    material.SetFloat("_HorizonFadeStart", FarHorizonFadeStartMetres);
                    material.SetFloat("_HorizonFadeEnd", FarHorizonFadeEndMetres);
                }

                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Airside] Surroundings failed to build: {e.Message}");
                return false;
            }
        }

        public static Material BuildMaterial(Shader shader = null) =>
            BuildMaterial(shader, SatelliteTexturePath, SatelliteExtentMetres, "mat_adelaide_surroundings_v01");

        public static Material BuildMaterial(Shader shader, string satellitePath, float satelliteExtent, string name)
        {
            shader ??= Shader.Find(ShaderName);
            if (shader == null)
                return null;

            var material = new Material(shader) { name = name, enableInstancing = true };
            var dry = AirsideArtTextures.Load(
                AirsideAdelaideGround.LayerBasecolorPath(AirsideAdelaideGround.LayerDryGrass));
            if (dry != null)
                material.SetTexture("_AirfieldAlbedo", dry);
            var satellite = AirsideArtTextures.Load(satellitePath, wrap: TextureWrapMode.Clamp);
            if (satellite != null)
                material.SetTexture("_SatelliteAlbedo", satellite);
            material.SetFloat("_SatelliteExtent", satelliteExtent);
            material.SetFloat("_SatelliteNearStrength", satellite != null ? SatelliteNearStrength : 0f);
            material.SetFloat("_SatelliteFarStrength", satellite != null ? SatelliteFarStrength : 0f);
            material.SetFloat("_SatelliteFarBlendStart", SatelliteFarBlendStartMetres);
            material.SetFloat("_SatelliteFarBlendEnd", SatelliteFarBlendEndMetres);
            material.SetColor("_SatelliteTint", new Color(0.56f, 0.58f, 0.56f, 1f));
            material.SetColor("_AirfieldTint", new Color(0.59f, 0.61f, 0.55f, 1f));
            material.SetFloat("_AirfieldHalfX", AirsideAdelaideGround.SizeX * 0.5f);
            material.SetFloat("_AirfieldHalfZ", AirsideAdelaideGround.SizeZ * 0.5f);
            material.SetFloat("_EdgeTextureBlend", EdgeTextureBlendMetres);
            material.SetFloat("_DryTile", AirsideAdelaideGround.TileSize(AirsideAdelaideGround.LayerDryGrass));
            material.SetFloat("_MacroScale", AirsideAdelaideGroundMesh.MacroScaleMetres);
            material.SetFloat("_MacroStrength", AirsideAdelaideGroundMesh.MacroStrength);
            material.SetFloat("_FarBlendStart", AirsideAdelaideGroundMesh.FarBlendStartMetres);
            material.SetFloat("_FarBlendEnd", AirsideAdelaideGroundMesh.FarBlendEndMetres);
            return material;
        }

        public static Mesh BuildMesh(CoastGrid grid) => BuildMesh(grid, out _);

        /// <summary>
        /// Bilinear height of the surroundings surface at a world x,z, from the vertices
        /// <see cref="BuildMesh(CoastGrid, out Vector3[])"/> produced. Roads sit on this so they
        /// follow the beach and the airfield lip instead of floating on a fixed plane.
        /// </summary>
        public static Func<float, float, float> HeightSampler(CoastGrid grid, Vector3[] vertices)
        {
            if (grid == null || vertices == null || vertices.Length != grid.CountX * grid.CountZ)
                return null;

            int Cell(int count, Func<int, float> axis, float value)
            {
                var lo = 0;
                var hi = count - 2;
                while (lo < hi)
                {
                    var mid = (lo + hi + 1) / 2;
                    if (axis(mid) <= value)
                        lo = mid;
                    else
                        hi = mid - 1;
                }

                return Mathf.Clamp(lo, 0, count - 2);
            }

            return (x, z) =>
            {
                var nx = grid.CountX;
                var xi = Cell(nx, grid.X, x);
                var zi = Cell(grid.CountZ, grid.Z, z);
                var tx = Mathf.InverseLerp(grid.X(xi), grid.X(xi + 1), x);
                var tz = Mathf.InverseLerp(grid.Z(zi), grid.Z(zi + 1), z);
                var h00 = vertices[zi * nx + xi].y;
                var h10 = vertices[zi * nx + xi + 1].y;
                var h01 = vertices[(zi + 1) * nx + xi].y;
                var h11 = vertices[(zi + 1) * nx + xi + 1].y;
                return Mathf.Lerp(Mathf.Lerp(h00, h10, tx), Mathf.Lerp(h01, h11, tx), tz);
            };
        }

        public static Mesh BuildMesh(CoastGrid grid, out Vector3[] vertices) => BuildMesh(grid, out vertices, null);

        /// <param name="terrain">
        /// Real heights (ADR 0158): land higher than the plain rises by that much, eased in from
        /// 700 m outside the airfield so its edge, the coast and the beach keep their shape.
        /// </param>
        public static Mesh BuildMesh(CoastGrid grid, out Vector3[] vertices, AdelaideTerrainHeights terrain)
        {
            var nx = grid.CountX;
            var nz = grid.CountZ;
            vertices = new Vector3[nx * nz];
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
                    // Satellite water carries the exact shoreline and shallows; dissolve it
                    // into the stylised Gulf over distance, where WMS tile gaps can occur.
                    c.a = Mathf.SmoothStep(0f, 1f, coast / 450f);
                    colors[i] = c;
                    continue;
                }

                var cover = AdelaideLandCover.Sample(x, z);
                if (cover == AdelaideLandCover.Kind.Water)
                {
                    // Patawalonga / West Lakes — real inland water, not the gulf.
                    vertices[i] = new Vector3(x, pavement - InlandWaterBelowPavement, z);
                    var c = InlandWater.linear;
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
                if (terrain != null)
                    height += terrain.Relief(x, z, outside);
                var beach = 1f - Mathf.SmoothStep(0f, 1f, coast / BeachWidthMetres);
                height = Mathf.Lerp(height, pavement - BeachBelowPavement, beach);
                if (grid.IsTuckedUnder(xi, zi))
                    height = edgeHeight - TuckUnderMetres;

                vertices[i] = new Vector3(x, height, z);
                colors[i] = LandColour(x, z, beach, outside, cover);
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
            RepairJoinNormals(grid, vertices, mesh);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// The 4 m tuck-under cliff shares vertices with the hole edge.
        /// <see cref="Mesh.RecalculateNormals"/> pulls those verts down the cliff
        /// and lights a bright rectangle around the field. Rebuild the join from
        /// the outward land only.
        /// </summary>
        public static void RepairJoinNormals(CoastGrid grid, Vector3[] vertices, Mesh mesh)
        {
            if (grid == null || vertices == null || mesh == null)
                return;
            var nx = grid.CountX;
            var nz = grid.CountZ;
            if (vertices.Length != nx * nz)
                return;

            var normals = mesh.normals;
            if (normals == null || normals.Length != vertices.Length)
                return;

            for (var zi = 0; zi < nz; zi++)
            for (var xi = 0; xi < nx; xi++)
            {
                if (!IsHoleEdge(grid, xi, zi))
                    continue;

                var ox = xi;
                var oz = zi;
                if (Mathf.Abs(Mathf.Abs(grid.X(xi)) - grid.HoleHalfX) < 0.05f)
                    ox = grid.X(xi) >= 0f ? Mathf.Min(xi + 1, nx - 1) : Mathf.Max(xi - 1, 0);
                if (Mathf.Abs(Mathf.Abs(grid.Z(zi)) - grid.HoleHalfZ) < 0.05f)
                    oz = grid.Z(zi) >= 0f ? Mathf.Min(zi + 1, nz - 1) : Mathf.Max(zi - 1, 0);

                var edge = vertices[zi * nx + xi];
                var outward = vertices[oz * nx + ox] - edge;
                Vector3 along;
                if (ox != xi)
                {
                    var z1 = Mathf.Min(zi + 1, nz - 1);
                    var z0 = Mathf.Max(zi - 1, 0);
                    along = vertices[z1 * nx + xi] - vertices[z0 * nx + xi];
                }
                else
                {
                    var x1 = Mathf.Min(xi + 1, nx - 1);
                    var x0 = Mathf.Max(xi - 1, 0);
                    along = vertices[zi * nx + x1] - vertices[zi * nx + x0];
                }

                var n = Vector3.Cross(along, outward);
                if (n.y < 0f)
                    n = -n;
                if (n.sqrMagnitude > 1e-6f)
                    normals[zi * nx + xi] = n.normalized;
            }

            mesh.normals = normals;
        }

        public static bool IsHoleEdge(CoastGrid grid, int xi, int zi)
        {
            if (grid == null)
                return false;
            var onX = Mathf.Abs(Mathf.Abs(grid.X(xi)) - grid.HoleHalfX) < 0.05f
                      && Mathf.Abs(grid.Z(zi)) <= grid.HoleHalfZ + 0.05f;
            var onZ = Mathf.Abs(Mathf.Abs(grid.Z(zi)) - grid.HoleHalfZ) < 0.05f
                      && Mathf.Abs(grid.X(xi)) <= grid.HoleHalfX + 0.05f;
            return onX || onZ;
        }

        /// <summary>
        /// Coastal plain in broad, soft patches — suburbs, parks, open ground — so it is not
        /// one flat card from the overview, then sand along the beach.
        /// </summary>
        private static Color LandColour(float x, float z, float beach, float outsideAirfield,
            AdelaideLandCover.Kind cover)
        {
            // Soft noise plain as the fallback / blend base.
            var patches = Noise(x / 900f, z / 900f) * 0.65f + Noise(x / 260f + 11.3f, z / 260f - 4.1f) * 0.35f;
            var land = Color.Lerp(Plain, Suburb, Mathf.SmoothStep(0.35f, 0.75f, patches));
            land = Color.Lerp(land, Park, Mathf.SmoothStep(0.72f, 0.9f, Noise(x / 500f - 7.7f, z / 500f + 3.2f)) * 0.8f);

            // Real OSM land cover overrides the noise where we have it (WLD-004 palette).
            var mapped = CoverColour(cover);
            if (mapped.HasValue)
                land = Color.Lerp(land, mapped.Value, 0.82f);

            // Match the airfield's dry grass for the first few hundred metres out.
            land = Color.Lerp(AirfieldEdge, land, Mathf.SmoothStep(0f, 1f, outsideAirfield / 900f));
            // Palette is authored in sRGB; vertex colours are read as linear in this project.
            var colour = Color.Lerp(land, Beach, Mathf.SmoothStep(0f, 1f, beach)).linear;
            colour.a = 0f;
            return colour;
        }

        private static Color? CoverColour(AdelaideLandCover.Kind cover) => cover switch
        {
            AdelaideLandCover.Kind.Residential => Suburb,
            AdelaideLandCover.Kind.Commercial => Commercial,
            AdelaideLandCover.Kind.Park => Park,
            AdelaideLandCover.Kind.Parking => Parking,
            AdelaideLandCover.Kind.Sand => Beach,
            AdelaideLandCover.Kind.Scrub => Scrub,
            _ => null,
        };

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
