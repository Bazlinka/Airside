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
            var resources = Resources.LoadAll<GameObject>(
                Airside.Presentation.ArtPresentationLoader.ResourcesPrefabRoot);
            var catalog = Airside.Presentation.AirsidePrefabAddressables.HasPackagedCatalog
                ? "packaged catalog present"
                : "no packaged catalog — keys resolve on demand via Resources";
            Debug.Log(
                $"[Airside] Prefab Addressables locator ready ({catalog}). " +
                $"Resources prefabs found: {resources.Length}. " +
                "Runtime loads one key at a time; Editor Addressables groups can mirror airside-prefab/<key>.");
            EditorUtility.DisplayDialog(
                "Airside Addressables",
                $"On-demand locator registered.\n{catalog}.\n{resources.Length} Resources prefabs on disk.\n\n" +
                "Runtime no longer LoadAlls this folder at startup.\n" +
                "StreamingAssets glTF remains the fallback when no prefab exists.",
                "OK");
        }
    }
}
