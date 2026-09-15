using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Instantiates the terrain baked by <c>Airside.Editor.AirsideTerrainBakerMenu</c>.
    ///
    /// This is the whole runtime cost of the new ground: one <c>Resources.Load</c> and
    /// one <c>Instantiate</c>. The heightmap and splatmap are already in the asset, so
    /// nothing is generated at startup — the previous outer-ground tile field is what
    /// stopped the packaged player from reaching its first frame, and a startup
    /// splatmap bake would be the same mistake in a new shape.
    ///
    /// When the asset is absent the caller keeps the procedural
    /// <c>Airfield terrain base</c> slab, so a project that has not run the bake yet
    /// still renders correctly. That fallback stays until packaged visual QA passes.
    /// </summary>
    public static class AirsideTerrainGround
    {
        /// <summary>
        /// Scene name of the instantiated terrain. <see cref="AirsideStaticWorld"/>
        /// treats this name as dynamic so the terrain never reaches
        /// <c>StaticBatchingUtility.Combine</c>; combining it previously caused a
        /// packaged Mac memory spike.
        /// </summary>
        public const string TerrainObjectName = "Kingscote terrain";

        public const string PrefabKey = "mdl_kingscote_terrain_v01";

        /// <summary>True once a baked terrain has been instantiated this session.</summary>
        public static bool Active { get; private set; }

        /// <summary>
        /// Albedo scale for the CC0 ground layers under the release noon rig (sun 2.05, trilight
        /// ambient 1.12, +0.22 EV, contrast 8.5). The bare field's own ground shader tints the
        /// same textures; this legacy URP Terrain/Lit path used them at full strength and read
        /// as washed-out grey. Warmer and greener than the bare tint because URP Lit reads cooler
        /// under the same rig. Calibrated from packaged -airsideFullAirport captures (2026-09-16).
        /// </summary>
        public static readonly Vector4 LayerAlbedoScale = new(0.72f, 0.76f, 0.42f, 1f);

        /// <summary>
        /// A runtime copy of <paramref name="source"/> calibrated for the noon rig: albedo scaled
        /// by <see cref="LayerAlbedoScale"/>, and the mask map's smoothness (alpha) capped at
        /// the layer's authored smoothness. The generated mask alphas average 0.27–0.45 — three
        /// to four times the authored 0.05–0.18 — and URP Terrain/Lit reads mask alpha as
        /// smoothness, so glancing sky reflection clipped the whole terrain to white.
        /// The source asset is never modified.
        /// </summary>
        public static TerrainLayer CalibratedLayer(TerrainLayer source)
        {
            if (source == null)
                return null;

            var copy = Object.Instantiate(source);
            copy.name = source.name;
            var albedo = source.diffuseRemapMax;
            copy.diffuseRemapMax = new Vector4(
                albedo.x * LayerAlbedoScale.x,
                albedo.y * LayerAlbedoScale.y,
                albedo.z * LayerAlbedoScale.z,
                albedo.w);
            var mask = source.maskMapRemapMax;
            mask.w = Mathf.Min(mask.w, source.smoothness);
            copy.maskMapRemapMax = mask;
            return copy;
        }

        /// <summary>
        /// Gives <paramref name="terrain"/> its own copy of the baked TerrainData with calibrated
        /// layers, so play mode never writes into the shared asset.
        /// </summary>
        public static void Calibrate(Terrain terrain)
        {
            if (terrain == null || terrain.terrainData == null)
                return;

            var data = Object.Instantiate(terrain.terrainData);
            data.name = terrain.terrainData.name;
            var layers = data.terrainLayers;
            var calibrated = new TerrainLayer[layers.Length];
            for (var i = 0; i < layers.Length; i++)
                calibrated[i] = CalibratedLayer(layers[i]);
            data.terrainLayers = calibrated;
            terrain.terrainData = data;
        }

        /// <summary>
        /// Instantiates the baked terrain under <paramref name="root"/>.
        /// Returns false when the asset has not been baked, leaving the caller to build
        /// the procedural fallback slab.
        /// </summary>
        public static bool TryBuild(Transform root)
        {
            Active = false;
            // Legacy miniature world only: the release bare circuit has its own ground.
            if (root == null || AirsideBareField.Enabled)
                return false;

            var prefab = Resources.Load<GameObject>($"{ArtPresentationLoader.ResourcesPrefabRoot}/{PrefabKey}");
            if (prefab == null)
                return false;

            var go = Object.Instantiate(prefab, root);
            go.name = TerrainObjectName;

            // The prefab records its own world origin, but Instantiate under a parent
            // reparents rather than repositions, so set the position from the authored
            // field instead of trusting whatever local offset survived.
            go.transform.localPosition = new Vector3(
                AirsideTerrainField.OriginX,
                AirsideTerrainField.OriginY,
                AirsideTerrainField.OriginZ);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var terrain = go.GetComponent<Terrain>();
            if (terrain == null || terrain.terrainData == null)
            {
                Object.Destroy(go);
                return false;
            }

            Calibrate(terrain);
            terrain.heightmapPixelError = AirsideTerrainField.HeightmapPixelError;
            terrain.detailObjectDistance = 0f;
            terrain.treeDistance = 0f;

            Active = true;
            return true;
        }
    }
}
