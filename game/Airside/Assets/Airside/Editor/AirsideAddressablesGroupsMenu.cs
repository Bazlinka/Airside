using UnityEditor;
using UnityEngine;

namespace Airside.Editor
{
    /// <summary>
    /// Decision 0025 item 1 — Mac Editor helper that documents and verifies the
    /// runtime Addressables key contract for Resources prefabs. Does not mutate
    /// AddressableAssetSettings (those groups stay Bailey-owned); it only probes
    /// that <see cref="Airside.Presentation.AirsidePrefabAddressables"/> registers
    /// every Resources prefab under <c>airside-prefab/&lt;key&gt;</c>.
    /// </summary>
    public static class AirsideAddressablesGroupsMenu
    {
        private const string MenuPath = "Airside/Art/Verify Prefab Addressables Keys";

        [MenuItem(MenuPath)]
        private static void VerifyPrefabKeys()
        {
            Airside.Presentation.AirsidePrefabAddressables.EnsureRegistered();
            var count = Airside.Presentation.AirsidePrefabAddressables.RegisteredKeyCount;
            var resources = Resources.LoadAll<GameObject>(
                Airside.Presentation.ArtPresentationLoader.ResourcesPrefabRoot);
            Debug.Log(
                $"[Airside] Addressables prefab keys registered: {count} " +
                $"(Resources prefabs found: {resources.Length}). " +
                "Editor Addressables groups can mirror the same airside-prefab/<key> contract.");
            EditorUtility.DisplayDialog(
                "Airside Addressables",
                $"Registered {count} airside-prefab keys from {resources.Length} Resources prefabs.\n\n" +
                "Runtime uses AirsideResourcesProvider → Resources.Load.\n" +
                "StreamingAssets glTF remains the fallback when no prefab exists.",
                "OK");
        }
    }
}
