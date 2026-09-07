using System;
using System.IO;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Decision 0025 item 1–2 scaffold — production-facing art entry point.
    /// Prefers Unity-imported prefabs under <c>Resources/Airside/Prefabs/</c>
    /// (drop targets for Addressables / Prefab workflow), then falls back to the
    /// interim StreamingAssets glTF kits via <see cref="ArtGltfLoader"/>.
    /// Procedural cuboids remain the caller's last resort.
    /// </summary>
    public static class ArtPresentationLoader
    {
        public const string ResourcesPrefabRoot = "Airside/Prefabs";

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
            return Resources.Load<GameObject>($"{ResourcesPrefabRoot}/{prefabKey}") != null;
        }

        public static bool TryInstantiatePrefab(string prefabKey, out Transform root)
        {
            root = null;
            if (string.IsNullOrEmpty(prefabKey))
                return false;

            var prefab = Resources.Load<GameObject>($"{ResourcesPrefabRoot}/{prefabKey}");
            if (prefab == null)
                return false;

            var instance = UnityEngine.Object.Instantiate(prefab);
            instance.name = prefabKey;
            root = instance.transform;
            return true;
        }

        /// <summary>
        /// Prefab first, then glTF kit. Returns false when neither source exists.
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
    }
}
