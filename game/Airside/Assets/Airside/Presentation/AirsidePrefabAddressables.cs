using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
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

        /// <summary>
        /// This locator was written against <c>LegacyResourcesProvider</c>, which the
        /// Addressables version in this project does not ship — so the file never
        /// compiled and this path has never run. The provider id is named rather than
        /// resolved via typeof so the build is green; until a real provider is
        /// registered, <see cref="Register"/> deliberately does nothing and
        /// <see cref="ArtPresentationLoader"/> keeps using its Resources → glTF
        /// fallbacks, which is what has actually been serving prefabs all along.
        /// Bailey's Editor Addressables groups are the intended replacement.
        /// </summary>
        private const string ResourcesProviderId =
            "UnityEngine.ResourceManagement.ResourceProviders.LegacyResourcesProvider";

        /// <summary>True once a real provider exists and this locator can be trusted.</summary>
        public static bool Enabled { get; set; }
        private static bool _registered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap() => EnsureRegistered();

        public static void EnsureRegistered()
        {
            if (_registered)
                return;

            // Off until a provider that exists in this Addressables version is wired up
            // (see ResourcesProviderId). Registering a locator whose provider cannot load
            // would turn a working Resources fallback into a runtime failure.
            if (!Enabled)
            {
                _registered = true;
                return;
            }

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
                        ResourcesProviderId,
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
