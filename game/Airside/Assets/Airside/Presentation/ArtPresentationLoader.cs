using System;
using System.IO;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Airside.Presentation
{
    /// <summary>
    /// Decision 0025 item 1–2 — production-facing art entry point.
    /// Prefers Addressables/Resources prefabs, then StreamingAssets glTF via
    /// <see cref="ArtGltfLoader"/>. Builtin Cube/Cylinder pipeline-proof Resources
    /// prefabs yield to a companion glTF when present (lathed fidelity before Mac
    /// FBX bake). Procedural cuboids remain the caller's last resort.
    /// </summary>
    public static class ArtPresentationLoader
    {
        public const string ResourcesPrefabRoot = "Airside/Prefabs";
        public const string AddressablesKeyPrefix = "airside-prefab/";

        /// <summary>
        /// Stable prefab key from a kit path, e.g.
        /// <c>Models/Buildings/mdl_hangar_small_v02.gltf</c> → <c>mdl_hangar_small_v02</c>.
        /// </summary>
        public static string PrefabKeyFromArtPath(string artRelativePath)
        {
            if (string.IsNullOrEmpty(artRelativePath))
                return null;
            return Path.GetFileNameWithoutExtension(artRelativePath.Replace('\\', '/'));
        }

        public static bool HasPrefab(string prefabKey)
        {
            if (string.IsNullOrEmpty(prefabKey))
                return false;
            AirsidePrefabAddressables.EnsureRegistered();
            if (AddressablesKeyExists(prefabKey))
                return true;
            return Resources.Load<GameObject>($"{ResourcesPrefabRoot}/{prefabKey}") != null;
        }

        public static bool TryInstantiatePrefab(string prefabKey, out Transform root)
        {
            root = null;
            if (string.IsNullOrEmpty(prefabKey))
                return false;

            AirsidePrefabAddressables.EnsureRegistered();
            if (TryInstantiateAddressable(prefabKey, out root))
                return true;

            var prefab = Resources.Load<GameObject>($"{ResourcesPrefabRoot}/{prefabKey}");
            if (prefab != null)
            {
                var instance = UnityEngine.Object.Instantiate(prefab);
                instance.name = prefabKey;
                root = instance.transform;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Prefab / Addressables first, then glTF kit. Returns false when neither source exists.
        /// Pipeline-proof Resources prefabs (Unity builtin Cube/Cylinder meshes only) yield to a
        /// StreamingAssets glTF companion when present, so AIR-001 v05 / authored kits show
        /// lathed mesh fidelity before Mac FBX bake replaces the Resources prefab.
        /// </summary>
        public static bool TryInstantiate(
            string artRelativePath,
            Transform parent,
            out Transform root,
            Func<string, string> rename = null,
            Func<string, Color?> colorFor = null,
            Vector3 localPosition = default,
            bool preferProceduralMaterials = false)
        {
            var key = PrefabKeyFromArtPath(artRelativePath);
            if (!string.IsNullOrEmpty(key) && TryLoadPrefabAsset(key, out var prefab))
            {
                var gltfAvailable = ArtGltfLoader.HasKit(artRelativePath);
                if (!gltfAvailable || !IsPipelineProofPrefab(prefab))
                {
                    var instance = UnityEngine.Object.Instantiate(prefab);
                    instance.name = key;
                    root = instance.transform;
                    if (parent != null)
                        root.SetParent(parent, false);
                    root.localPosition = localPosition;
                    ApplyPresentationMaterials(root, rename, colorFor, preferProceduralMaterials);
                    return true;
                }
            }

            return ArtGltfLoader.TryInstantiate(
                artRelativePath, parent, out root, rename, colorFor, localPosition,
                preferProceduralMaterials);
        }

        /// <summary>
        /// After a Mac FBX bake, Resources prefabs keep ModelImporter default materials.
        /// When <paramref name="colorFor"/> names a mesh colour (aircraft kits), always
        /// rebind to the presentation palette so REF-005 white wing / teal nacelle reads
        /// win over Unity's toy-blue defaults. Otherwise keep authored albedo maps, but
        /// still force glass/rubber kinds (opaque Lit on windscreens/tires).
        /// </summary>
        private static void ApplyPresentationMaterials(
            Transform root,
            Func<string, string> rename,
            Func<string, Color?> colorFor,
            bool preferProceduralMaterials = false)
        {
            if (root == null)
                return;

            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                    continue;
                var originalName = renderer.gameObject.name;
                if (rename != null)
                {
                    var renamed = rename(originalName);
                    if (!string.IsNullOrEmpty(renamed) && renamed != originalName)
                        renderer.gameObject.name = renamed;
                }

                var kind = AirsideMaterialLibrary.InferFromMeshName(originalName);
                var namedColor = colorFor?.Invoke(originalName);
                if (namedColor.HasValue)
                {
                    renderer.sharedMaterial = AirsideMaterialLibrary.Create(
                        namedColor.Value, kind, preferProcedural: preferProceduralMaterials);
                    continue;
                }

                // No palette override — preserve mapped FBX materials except glass/rubber.
                var existing = renderer.sharedMaterial;
                if (existing != null)
                {
                    var hasMap = existing.mainTexture != null
                        || (existing.HasProperty("_BaseMap") && existing.GetTexture("_BaseMap") != null)
                        || (existing.HasProperty("_MainTex") && existing.GetTexture("_MainTex") != null);
                    if (hasMap && kind is not (AirsideMaterialLibrary.SurfaceKind.Glass
                        or AirsideMaterialLibrary.SurfaceKind.Rubber))
                        continue;
                }

                var color = new Color(0.61f, 0.64f, 0.63f);
                renderer.sharedMaterial = AirsideMaterialLibrary.CreateShared(color, kind);
            }
        }

        public static bool HasPresentation(string artRelativePath) =>
            HasPrefab(PrefabKeyFromArtPath(artRelativePath)) || ArtGltfLoader.HasKit(artRelativePath);

        private static bool TryLoadPrefabAsset(string prefabKey, out GameObject prefab)
        {
            prefab = null;
            if (string.IsNullOrEmpty(prefabKey))
                return false;

            AirsidePrefabAddressables.EnsureRegistered();
            try
            {
                var key = AddressablesKeyPrefix + prefabKey;
                if (AddressablesKeyExists(prefabKey))
                {
                    var handle = Addressables.LoadAssetAsync<GameObject>(key);
                    prefab = handle.WaitForCompletion();
                    if (handle.Status == AsyncOperationStatus.Succeeded && prefab != null)
                        return true;
                    if (handle.IsValid())
                        Addressables.Release(handle);
                    prefab = null;
                }
            }
            catch (Exception)
            {
                prefab = null;
            }

            prefab = Resources.Load<GameObject>($"{ResourcesPrefabRoot}/{prefabKey}");
            return prefab != null;
        }

        /// <summary>
        /// True when every MeshFilter uses a Unity builtin Cube/Cylinder (pipeline proof),
        /// or when no mesh filters exist. Mac FBX bake replaces these with imported meshes.
        /// </summary>
        private static bool IsPipelineProofPrefab(GameObject prefab)
        {
            if (prefab == null)
                return true;
            var filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            if (filters == null || filters.Length == 0)
                return true;
            for (var i = 0; i < filters.Length; i++)
            {
                var mesh = filters[i].sharedMesh;
                if (mesh == null)
                    continue;
                var n = mesh.name;
                if (n != "Cube" && n != "Cylinder" && n != "Sphere" && n != "Capsule"
                    && n != "Plane" && n != "Quad")
                    return false;
            }

            return true;
        }

        private static bool TryInstantiateAddressable(string prefabKey, out Transform root)
        {
            root = null;
            try
            {
                var key = AddressablesKeyPrefix + prefabKey;
                if (!AddressablesKeyExists(prefabKey))
                    return false;

                var handle = Addressables.LoadAssetAsync<GameObject>(key);
                var prefab = handle.WaitForCompletion();
                if (handle.Status != AsyncOperationStatus.Succeeded || prefab == null)
                {
                    if (handle.IsValid())
                        Addressables.Release(handle);
                    return false;
                }

                var instance = UnityEngine.Object.Instantiate(prefab);
                instance.name = prefabKey;
                root = instance.transform;
                return true;
            }
            catch (Exception)
            {
                // No catalog / key / ResourceManager — fall through to Resources / glTF.
                return false;
            }
        }

        private static bool AddressablesKeyExists(string prefabKey)
        {
            try
            {
                var key = AddressablesKeyPrefix + prefabKey;
                foreach (var locator in Addressables.ResourceLocators)
                {
                    if (locator.Locate(key, typeof(GameObject), out _))
                        return true;
                }
            }
            catch (Exception)
            {
                return false;
            }

            return false;
        }
    }
}
