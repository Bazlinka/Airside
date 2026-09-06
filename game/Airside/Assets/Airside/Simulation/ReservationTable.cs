using System.Collections.Generic;
using System.Linq;
using Airside.Domain;

namespace Airside.Simulation
{
    public sealed class ReservationTable
    {
        private readonly Dictionary<StableId, StableId> _ownersByResource = new();

        public IReadOnlyCollection<StableId> OccupiedResources => _ownersByResource.Keys.ToArray();

        public bool TryReplace(StableId owner, IEnumerable<StableId> requestedResources, out StableId blockedResource)
        {
            var requested = requestedResources.Distinct().ToArray();
            foreach (var resource in requested)
            {
                if (_ownersByResource.TryGetValue(resource, out var existingOwner) && !existingOwner.Equals(owner))
                {
                    blockedResource = resource;
                    return false;
                }
            }

            Release(owner);
            foreach (var resource in requested)
                _ownersByResource[resource] = owner;

            blockedResource = default;
            return true;
        }

        public bool IsReserved(StableId resource) => _ownersByResource.ContainsKey(resource);

        public void Release(StableId owner)
        {
            var owned = _ownersByResource
                .Where(pair => pair.Value.Equals(owner))
                .Select(pair => pair.Key)
                .ToArray();

            foreach (var resource in owned)
                _ownersByResource.Remove(resource);
        }
    }
}
