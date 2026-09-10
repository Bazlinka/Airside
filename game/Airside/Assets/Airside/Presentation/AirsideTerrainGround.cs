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
        /// Instantiates the baked terrain under <paramref name="root"/>.
        /// Returns false when the asset has not been baked, leaving the caller to build
        /// the procedural fallback slab.
        /// </summary>
        public static bool TryBuild(Transform root)
        {
            Active = false;
            if (root == null)
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

            terrain.heightmapPixelError = AirsideTerrainField.HeightmapPixelError;
            terrain.detailObjectDistance = 0f;
            terrain.treeDistance = 0f;

            Active = true;
            return true;
        }
    }
}
