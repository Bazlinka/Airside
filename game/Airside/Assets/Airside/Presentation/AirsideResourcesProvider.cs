using System;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace Airside.Presentation
{
    /// <summary>
    /// Addressables provider that loads GameObjects from Unity Resources.
    /// Replaces the missing <c>LegacyResourcesProvider</c> so
    /// <see cref="AirsidePrefabAddressables"/> can expose
    /// <c>airside-prefab/&lt;key&gt;</c> without Editor catalog groups.
    /// </summary>
    public sealed class AirsideResourcesProvider : ResourceProviderBase
    {
        public const string Id = "Airside.ResourcesGameObjectProvider";

        public override string ProviderId => Id;

        public override void Provide(ProvideHandle provideHandle)
        {
            try
            {
                var path = provideHandle.Location.InternalId;
                var type = provideHandle.Type ?? typeof(GameObject);
                var asset = Resources.Load(path, type);
                if (asset != null)
                {
                    provideHandle.Complete(asset, true, null);
                    return;
                }

                provideHandle.Complete<object>(
                    null,
                    false,
                    new InvalidOperationException($"Resources asset missing at '{path}'."));
            }
            catch (Exception ex)
            {
                provideHandle.Complete<object>(null, false, ex);
            }
        }
    }
}
