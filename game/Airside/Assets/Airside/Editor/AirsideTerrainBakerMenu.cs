using System;
using System.IO;
using System.Text;
using Airside.Presentation;
using UnityEditor;
using UnityEngine;

namespace Airside.Editor
{
    /// <summary>
    /// One-shot bake of the Kingscote airfield terrain.
    ///
    /// The packaged player must never build a 257×257 heightmap or a 256×256×4
    /// splatmap during startup, so all of that work happens here, in the Editor, and
    /// the result is committed as a TerrainData asset plus a Resources prefab the
    /// runtime simply instantiates. Until this has been run the runtime falls back to
    /// the procedural <c>Airfield terrain base</c> slab, so the scene is never broken
    /// by an un-baked terrain.
    ///
    /// Every number comes from <see cref="AirsideTerrainField"/>. Nothing is authored
    /// twice, so the baked asset and the EditMode tests cannot disagree.
    ///
    /// Run from the menu, or headlessly:
    /// <code>
    /// Unity -batchmode -quit -projectPath game/Airside \
    ///       -executeMethod Airside.Editor.AirsideTerrainBakerMenu.BakeFromCommandLine
    /// </code>
    /// </summary>
    public static class AirsideTerrainBakerMenu
    {
        private const string MenuPath = "Airside/Art/Bake Kingscote Terrain";

        private const string TerrainDir = "Assets/Airside/Art/Terrain";
        private const string TextureDir = "Assets/Airside/Art/Textures/Terrain";
        private const string PrefabDir = "Assets/Resources/Airside/Prefabs";

        private const string TerrainDataPath = TerrainDir + "/terrain_kingscote_first_playable_v01.asset";
        private const string MaterialPath = TerrainDir + "/mat_kingscote_terrain_v01.mat";
        private const string PrefabPath = PrefabDir + "/mdl_kingscote_terrain_v01.prefab";

        /// <summary>Name the runtime and the static-batching exclusion both key off.</summary>
        public const string TerrainObjectName = AirsideTerrainGround.TerrainObjectName;

        [MenuItem(MenuPath)]
        public static void Bake()
        {
            var report = BakeInternal(out var ok);
            if (ok)
                EditorUtility.DisplayDialog("Airside terrain", report, "OK");
            else
                EditorUtility.DisplayDialog("Airside terrain — failed", report, "OK");
        }

        /// <summary>Batchmode entry point. Exits non-zero when the bake could not complete.</summary>
        public static void BakeFromCommandLine()
        {
            string report;
            bool ok;
            try
            {
                report = BakeInternal(out ok);
            }
            catch (Exception ex)
            {
                Debug.LogError("Airside terrain bake threw: " + ex);
                EditorApplication.Exit(2);
                return;
            }

            Debug.Log(report);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        private static string BakeInternal(out bool ok)
        {
            ok = false;
            var log = new StringBuilder();

            EnsureFolder(TerrainDir);
            EnsureFolder(PrefabDir);

            var layers = new TerrainLayer[AirsideTerrainField.LayerCount];
            for (var i = 0; i < AirsideTerrainField.LayerCount; i++)
            {
                layers[i] = BakeLayer(i, log, out var layerOk);
                if (!layerOk)
                    return log.ToString();
            }

            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);
            var fresh = data == null;
            if (fresh)
                data = new TerrainData();

            // Resolution first: assigning heightmapResolution resets size, so setting
            // size before it silently throws the authored extents away.
            data.heightmapResolution = AirsideTerrainField.HeightmapResolution;
            data.size = new Vector3(
                AirsideTerrainField.SizeX,
                AirsideTerrainField.SizeY,
                AirsideTerrainField.SizeZ);
            data.alphamapResolution = AirsideTerrainField.AlphamapResolution;
            data.baseMapResolution = AirsideTerrainField.BasemapResolution;

            // No detail meshes or trees on this terrain. The existing VEG/scrub kits
            // place vegetation as ordinary prefabs, and terrain detail instancing would
            // be a second, competing source of the same thing.
            data.SetDetailResolution(0, 8);
            data.treePrototypes = Array.Empty<TreePrototype>();
            data.treeInstances = Array.Empty<TreeInstance>();

            data.terrainLayers = layers;

            if (fresh)
            {
                data.name = Path.GetFileNameWithoutExtension(TerrainDataPath);
                AssetDatabase.CreateAsset(data, TerrainDataPath);
            }

            data.SetHeights(0, 0, AirsideTerrainField.BuildHeights());
            data.SetAlphamaps(0, 0, AirsideTerrainField.BuildAlphamaps());

            log.AppendLine($"TerrainData {AirsideTerrainField.SizeX}×{AirsideTerrainField.SizeZ}×{AirsideTerrainField.SizeY} m, "
                           + $"heightmap {AirsideTerrainField.HeightmapResolution}, alphamap {AirsideTerrainField.AlphamapResolution}");

            var material = BakeMaterial(log);
            if (material == null)
                return log.ToString();

            var prefabOk = BakePrefab(data, material, log);
            if (!prefabOk)
                return log.ToString();

            var coverage = AirsideTerrainField.Coverage();
            for (var i = 0; i < AirsideTerrainField.LayerCount; i++)
                log.AppendLine($"  {AirsideTerrainField.LayerName(i)} {coverage[i] * 100f:F1}%");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            ok = true;
            return log.ToString();
        }

        private static TerrainLayer BakeLayer(int layer, StringBuilder log, out bool ok)
        {
            ok = false;
            var name = AirsideTerrainField.LayerName(layer);
            var path = $"{TerrainDir}/trn_ground_{name}_v01.terrainlayer";

            var diffuse = RequireTexture($"{TextureDir}/tx_ground_{name}_basecolor_v01.png", log);
            var normal = RequireTexture($"{TextureDir}/tx_ground_{name}_normal_v01.png", log);
            var mask = RequireTexture($"{TextureDir}/tx_ground_{name}_maskmap_v01.png", log);
            if (diffuse == null || normal == null || mask == null)
                return null;

            var terrainLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            var fresh = terrainLayer == null;
            if (fresh)
                terrainLayer = new TerrainLayer { name = Path.GetFileNameWithoutExtension(path) };

            terrainLayer.diffuseTexture = diffuse;
            terrainLayer.normalMapTexture = normal;
            terrainLayer.maskMapTexture = mask;
            terrainLayer.normalScale = 1f;
            terrainLayer.metallic = AirsideTerrainField.Metallic(layer);
            terrainLayer.smoothness = AirsideTerrainField.Smoothness(layer);

            var tile = AirsideTerrainField.TileSize(layer);
            terrainLayer.tileSize = new Vector2(tile, tile);

            // Offset each layer so the four do not start their first repeat at the same
            // world position. Equal offsets on non-harmonic tile sizes still line up at
            // the origin, and that one shared seam is visible from the overview camera.
            terrainLayer.tileOffset = new Vector2(tile * 0.37f * layer, tile * 0.61f * layer);

            terrainLayer.diffuseRemapMin = Vector4.zero;
            terrainLayer.diffuseRemapMax = Vector4.one;
            terrainLayer.maskMapRemapMin = Vector4.zero;
            terrainLayer.maskMapRemapMax = Vector4.one;

            if (fresh)
                AssetDatabase.CreateAsset(terrainLayer, path);
            else
                EditorUtility.SetDirty(terrainLayer);

            log.AppendLine($"layer {layer} {name}: tile {tile} m, smoothness {terrainLayer.smoothness:F2}");
            ok = true;
            return terrainLayer;
        }

        private static Texture2D RequireTexture(string path, StringBuilder log)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
                log.AppendLine($"MISSING texture {path} — run scripts/generate-cc0-terrain-ground.py");
            return tex;
        }

        private static Material BakeMaterial(StringBuilder log)
        {
            var shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
            if (shader == null)
            {
                log.AppendLine("MISSING shader 'Universal Render Pipeline/Terrain/Lit' — is URP active?");
                return null;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = Path.GetFileNameWithoutExtension(MaterialPath) };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
            }

            // Blend layers by the height stored in the mask map's blue channel rather
            // than by splat weight alone. Straight weight blending cross-fades two
            // ground types into a flat average, which is what made the old slab edges
            // read as bands; height blending lets dirt sit down into the grass.
            //
            // _EnableHeightBlend is the material's own toggle and its keyword has to be
            // set alongside it, exactly as URP's TerrainLitShaderGUI does. _MASKMAP,
            // _NORMALMAP and _NumLayersCount are deliberately left alone: they are
            // per-renderer state the terrain sets from terrainData.terrainLayers, and
            // pinning them on the material would fight the engine.
            material.SetFloat("_EnableHeightBlend", 1f);
            material.EnableKeyword("_TERRAIN_BLEND_HEIGHT");
            material.SetFloat("_HeightTransition", 0.2f);

            log.AppendLine($"material {MaterialPath} (height blend on, transition 0.20)");
            return material;
        }

        private static bool BakePrefab(TerrainData data, Material material, StringBuilder log)
        {
            var go = new GameObject(TerrainObjectName);
            try
            {
                go.transform.position = new Vector3(
                    AirsideTerrainField.OriginX,
                    AirsideTerrainField.OriginY,
                    AirsideTerrainField.OriginZ);

                var terrain = go.AddComponent<Terrain>();
                terrain.terrainData = data;
                terrain.materialTemplate = material;
                terrain.heightmapPixelError = AirsideTerrainField.HeightmapPixelError;
                terrain.basemapDistance = 220f;

                // No terrain-authored vegetation, and no auto-connect: this is a single
                // standalone tile, and neighbour stitching would only cost time.
                terrain.detailObjectDistance = 0f;
                terrain.treeDistance = 0f;
                terrain.allowAutoConnect = false;
                terrain.groupingID = 0;

                // Aircraft and vehicles are placed by the simulation, not by physics, and
                // com.unity.modules.terrainphysics is not in the project, so this terrain
                // deliberately carries no TerrainCollider.

                PrefabUtility.SaveAsPrefabAsset(go, PrefabPath, out var success);
                if (!success)
                {
                    log.AppendLine($"FAILED to write prefab {PrefabPath}");
                    return false;
                }

                log.AppendLine($"prefab {PrefabPath} at "
                               + $"({AirsideTerrainField.OriginX:F1}, {AirsideTerrainField.OriginY:F3}, {AirsideTerrainField.OriginZ:F1})");
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var leaf = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf))
                return;
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
