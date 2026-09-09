using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace Airside.Presentation
{
    /// <summary>
    /// Decision 0026 / performance P1 — Addressables locator for
    /// <c>airside-prefab/&lt;key&gt;</c>. Keys resolve on demand via
    /// <see cref="AirsideResourcesProvider"/>; startup no longer
    /// <c>Resources.LoadAll</c>s every prefab. When a packaged Addressables
    /// catalog is present it is initialised first and this locator only fills
    /// missing keys.
    /// </summary>
    public static class AirsidePrefabAddressables
    {
        public const string LocatorId = "Airside.Prefabs";

        private static bool _registered;
        private static bool _providerRegistered;

        public static bool HasPackagedCatalog
        {
            get
            {
                try
                {
                    return File.Exists(Path.Combine(Application.streamingAssetsPath, "aa", "settings.json"));
                }
                catch
                {
                    return false;
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap() => EnsureRegistered();

        public static void EnsureRegistered()
        {
            if (_registered)
                return;

            try
            {
                EnsureProvider();
                Addressables.AddResourceLocator(new OnDemandLocator());
                _registered = true;
            }
            catch (Exception)
            {
                _registered = true;
            }
        }

        private static void EnsureProvider()
        {
            if (_providerRegistered)
                return;

            try
            {
                var catalogSettings = Path.Combine(
                    Application.streamingAssetsPath, "aa", "settings.json");
                if (File.Exists(catalogSettings))
                    Addressables.InitializeAsync().WaitForCompletion();

                var providers = Addressables.ResourceManager.ResourceProviders;
                for (var i = 0; i < providers.Count; i++)
                {
                    if (providers[i] != null && providers[i].ProviderId == AirsideResourcesProvider.Id)
                    {
                        _providerRegistered = true;
                        return;
                    }
                }

                providers.Add(new AirsideResourcesProvider());
                _providerRegistered = true;
            }
            catch (Exception)
            {
            }
        }

        public static int RegisteredKeyCount
        {
            get
            {
                EnsureRegistered();
                var count = 0;
                try
                {
                    foreach (var locator in Addressables.ResourceLocators)
                    {
                        if (locator.LocatorId != LocatorId)
                            continue;
                        foreach (var _ in locator.Keys)
                            count++;
                    }
                }
                catch (Exception)
                {
                    return 0;
                }

                return count;
            }
        }

        /// <summary>
        /// On-demand locator: a key exists when the Resources prefab file can be
        /// named. The provider loads that single asset; nothing else is touched.
        /// </summary>
        private sealed class OnDemandLocator : IResourceLocator
        {
            public string LocatorId => AirsidePrefabAddressables.LocatorId;

            public IEnumerable<object> Keys
            {
                get { yield break; }
            }

#if !ENABLE_JSON_CATALOG
            public IEnumerable<IResourceLocation> AllLocations
            {
                get { yield break; }
            }
#endif

            public bool Locate(object key, Type type, out IList<IResourceLocation> locations)
            {
                locations = null;
                if (key is not string s
                    || !s.StartsWith(ArtPresentationLoader.AddressablesKeyPrefix, StringComparison.Ordinal))
                    return false;
                if (type != null && type != typeof(GameObject) && type != typeof(UnityEngine.Object))
                    return false;

                var prefabKey = s.Substring(ArtPresentationLoader.AddressablesKeyPrefix.Length);
                if (string.IsNullOrEmpty(prefabKey))
                    return false;

                var internalId = $"{ArtPresentationLoader.ResourcesPrefabRoot}/{prefabKey}";
                locations = new List<IResourceLocation>
                {
                    new ResourceLocationBase(
                        s,
                        internalId,
                        AirsideResourcesProvider.Id,
                        typeof(GameObject))
                };
                return true;
            }
        }
    }
}
