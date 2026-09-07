using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace Airside.Presentation
{
    /// <summary>
    /// Decision 0025 item 1 / ADR 0026 — runtime Addressables locator that exposes
    /// every <c>Resources/Airside/Prefabs</c> asset under key
    /// <c>airside-prefab/&lt;key&gt;</c> until Bailey builds Editor Addressables groups.
    /// Loads via <see cref="AirsideResourcesProvider"/> (Resources.Load), keeping
    /// StreamingAssets glTF and direct Resources fallbacks intact.
    /// </summary>
    public static class AirsidePrefabAddressables
    {
        public const string LocatorId = "Airside.Prefabs";

        private static bool _registered;
        private static bool _providerRegistered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap() => EnsureRegistered();

        public static void EnsureRegistered()
        {
            if (_registered)
                return;

            try
            {
                EnsureProvider();

                var locations = new Dictionary<object, IList<IResourceLocation>>();
                var prefabs = Resources.LoadAll<GameObject>(ArtPresentationLoader.ResourcesPrefabRoot);
                for (var i = 0; i < prefabs.Length; i++)
                {
                    var prefab = prefabs[i];
                    if (prefab == null || string.IsNullOrEmpty(prefab.name))
                        continue;

                    var key = ArtPresentationLoader.AddressablesKeyPrefix + prefab.name;
                    var internalId = $"{ArtPresentationLoader.ResourcesPrefabRoot}/{prefab.name}";
                    IResourceLocation location = new ResourceLocationBase(
                        key,
                        internalId,
                        AirsideResourcesProvider.Id,
                        typeof(GameObject));
                    locations[key] = new List<IResourceLocation> { location };
                }

                Addressables.AddResourceLocator(new Locator(locations));
                _registered = true;
            }
            catch (Exception)
            {
                // Addressables / ResourceManager unavailable in some batch contexts.
                // ArtPresentationLoader still falls through to Resources → glTF.
                _registered = true;
            }
        }

        private static void EnsureProvider()
        {
            if (_providerRegistered)
                return;

            try
            {
                // Initialize so ResourceManager exists before we add a provider/locator.
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
                // Leave unregistered; locator registration may still no-op safely.
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

        private sealed class Locator : IResourceLocator
        {
            private readonly Dictionary<object, IList<IResourceLocation>> _locations;

            public Locator(Dictionary<object, IList<IResourceLocation>> locations) =>
                _locations = locations ?? new Dictionary<object, IList<IResourceLocation>>();

            public string LocatorId => AirsidePrefabAddressables.LocatorId;

            public IEnumerable<object> Keys => _locations.Keys;

#if !ENABLE_JSON_CATALOG
            /// <summary>
            /// Required by IResourceLocator when Addressables is built against the binary
            /// catalog (the default in this Unity version). Guarded the same way the
            /// interface declares it, so a JSON-catalog build still compiles.
            /// </summary>
            public IEnumerable<IResourceLocation> AllLocations
            {
                get
                {
                    foreach (var entry in _locations.Values)
                    {
                        if (entry == null)
                            continue;
                        foreach (var location in entry)
                            yield return location;
                    }
                }
            }
#endif

            public bool Locate(object key, Type type, out IList<IResourceLocation> locations)
            {
                if (key != null && _locations.TryGetValue(key, out locations))
                {
                    if (type == null || type == typeof(GameObject) || type == typeof(UnityEngine.Object))
                        return true;
                }

                locations = null;
                return false;
            }
        }
    }
}
