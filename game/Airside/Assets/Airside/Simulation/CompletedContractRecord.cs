using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// One fulfilled route contract, kept for the player's career history (a real answer to
    /// "what have I actually done" beyond the single currently-active contract). Immutable,
    /// captured at the moment <see cref="RouteContractDefinition.RequiredRotations"/> is met.
    /// </summary>
    public readonly struct CompletedContractRecord
    {
        public CompletedContractRecord(string definitionId, string originCode, string destinationCode,
            long totalPaid, SimulationTime completedAt)
        {
            DefinitionId = definitionId ?? string.Empty;
            OriginCode = originCode ?? string.Empty;
            DestinationCode = destinationCode ?? string.Empty;
            TotalPaid = totalPaid;
            CompletedAt = completedAt;
        }

        public string DefinitionId { get; }
        public string OriginCode { get; }
        public string DestinationCode { get; }

        /// <summary>Every rotation payment plus the completion reward, summed across the contract's life.</summary>
        public long TotalPaid { get; }
        public SimulationTime CompletedAt { get; }
    }
}
