using System;
using System.IO;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Airside.Presentation
{
    /// <summary>
    /// Decision 0025 item 1–2 — production-facing art entry point.
    /// Prefers Unity-imported prefabs under <c>Resources/Airside/Prefabs/</c>,
    /// then Addressables (when a catalog + key exist), then StreamingAssets glTF
    /// via <see cref="ArtGltfLoader"/>. Procedural cuboids remain the caller's
    /// last resort.
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
            if (Resources.Load<GameObject>($"{ResourcesPrefabRoot}/{prefabKey}") != null)
                return true;
            return AddressablesKeyExists(prefabKey);
        }

        public static bool TryInstantiatePrefab(string prefabKey, out Transform root)
        {
            root = null;
            if (string.IsNullOrEmpty(prefabKey))
                return false;

            var prefab = Resources.Load<GameObject>($"{ResourcesPrefabRoot}/{prefabKey}");
            if (prefab != null)
            {
                var instance = UnityEngine.Object.Instantiate(prefab);
                instance.name = prefabKey;
                root = instance.transform;
                return true;
            }

            return TryInstantiateAddressable(prefabKey, out root);
        }

        /// <summary>
        /// Prefab / Addressables first, then glTF kit. Returns false when neither source exists.
        /// </summary>
        public static bool TryInstantiate(
            string artRelativePath,
            Transform parent,
            out Transform root,
            Func<string, string> rename = null,
            Func<string, Color?> colorFor = null,
            Vector3 localPosition = default)
        {
            var key = PrefabKeyFromArtPath(artRelativePath);
            if (TryInstantiatePrefab(key, out root))
            {
                if (parent != null)
                    root.SetParent(parent, false);
                root.localPosition = localPosition;
                return true;
            }

            return ArtGltfLoader.TryInstantiate(
                artRelativePath, parent, out root, rename, colorFor, localPosition);
        }

        public static bool HasPresentation(string artRelativePath) =>
            HasPrefab(PrefabKeyFromArtPath(artRelativePath)) || ArtGltfLoader.HasKit(artRelativePath);

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
                // Keep the handle alive for the loaded asset; release with the instance if needed later.
                return true;
            }
            catch (Exception)
            {
                // No catalog / key — Addressables is optional until Bailey builds groups.
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
