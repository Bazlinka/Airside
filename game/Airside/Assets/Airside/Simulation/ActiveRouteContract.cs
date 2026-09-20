using System;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// Live progress against an accepted <see cref="RouteContractDefinition"/> (ADR 0053).
    /// Mutated only by <see cref="AirlineOperations"/>.
    /// </summary>
    public sealed class ActiveRouteContract
    {
        public ActiveRouteContract(string definitionId, SimulationTime acceptedAt, int completedRotations = 0)
        {
            DefinitionId = definitionId;
            AcceptedAt = acceptedAt;
            CompletedRotations = completedRotations;
        }

        public string DefinitionId { get; }
        public SimulationTime AcceptedAt { get; }
        public int CompletedRotations { get; private set; }

        /// <summary>
        /// Every per-rotation payment this contract has paid so far, including this one — not
        /// restored across a save taken mid-contract (a save has no record of rotations paid
        /// before it), so a contract history entry for one completed after a reload undercounts
        /// by whatever it paid before that save. Acceptable: the same "no retroactive credit"
        /// tolerance this career state already applies to pre-career trips.
        /// </summary>
        public long TotalPaid { get; private set; }

        internal void RecordRotation(long paid)
        {
            CompletedRotations++;
            TotalPaid += Math.Max(0, paid);
        }
    }
}
