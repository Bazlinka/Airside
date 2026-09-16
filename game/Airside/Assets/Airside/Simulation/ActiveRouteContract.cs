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

        internal void RecordRotation() => CompletedRotations++;
    }
}
