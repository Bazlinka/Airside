using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>The result of settling one eligible completed rotation (ADR 0053).</summary>
    public readonly struct FlightSettlement
    {
        public FlightSettlement(SettlementId settlementId, string contractDefinitionId, long payment,
            int reliabilityDelta, int rotationsCompleted, bool contractFulfilled)
        {
            SettlementId = settlementId;
            ContractDefinitionId = contractDefinitionId;
            Payment = payment;
            ReliabilityDelta = reliabilityDelta;
            RotationsCompleted = rotationsCompleted;
            ContractFulfilled = contractFulfilled;
        }

        public SettlementId SettlementId { get; }
        public string ContractDefinitionId { get; }
        public long Payment { get; }
        public int ReliabilityDelta { get; }

        /// <summary>Rotations completed against the contract, including this one.</summary>
        public int RotationsCompleted { get; }

        /// <summary>True when this settlement completed the contract's required rotation count.</summary>
        public bool ContractFulfilled { get; }
    }
}
