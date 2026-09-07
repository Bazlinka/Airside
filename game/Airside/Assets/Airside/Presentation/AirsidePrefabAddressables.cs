using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace Airside.Presentation
{
    /// <summary>
    /// Decision 0025 item 1 / ADR 0026 — runtime Addressables locator that exposes
    /// every <c>Resources/Airside/Prefabs</c> asset under key
    /// <c>airside-prefab/&lt;key&gt;</c> until Bailey builds Editor Addressables groups.
    /// Uses the built-in LegacyResourcesProvider so no custom download path is needed.
    /// </summary>
    public static class AirsidePrefabAddressables
    {
        public const string LocatorId = "Airside.Prefabs";
        private static bool _registered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap() => EnsureRegistered();

        public static void EnsureRegistered()
        {
            if (_registered)
                return;

            try
            {
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
                        typeof(LegacyResourcesProvider).FullName,
                        typeof(GameObject));
                    locations[key] = new List<IResourceLocation> { location };
                }

                if (locations.Count == 0)
                {
                    // Still register an empty locator so callers can probe safely.
                    Addressables.AddResourceLocator(new Locator(locations));
                    _registered = true;
                    return;
                }

                Addressables.AddResourceLocator(new Locator(locations));
                _registered = true;
            }
            catch (Exception)
            {
                // Addressables / ResourceManager unavailable in some batch contexts.
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
