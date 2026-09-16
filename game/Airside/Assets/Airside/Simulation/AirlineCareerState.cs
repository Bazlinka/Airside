using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// One player airline's career progress (ADR 0053): funds, reliability, operating tier
    /// and whichever route contract is currently accepted. Mutated only by
    /// <see cref="AirlineOperations"/>; every settlement is applied at most once.
    /// </summary>
    public sealed class AirlineCareerState
    {
        public const int StartingReliability = 100;

        private readonly HashSet<string> _processedSettlements;

        public AirlineCareerState(
            long funds = 0, int reliability = StartingReliability, OperatingTier tier = OperatingTier.Provisional,
            ActiveRouteContract activeContract = null, IEnumerable<string> processedSettlementKeys = null)
        {
            Funds = funds;
            Reliability = Clamp(reliability);
            Tier = tier;
            ActiveContract = activeContract;
            _processedSettlements = processedSettlementKeys == null
                ? new HashSet<string>(StringComparer.Ordinal)
                : new HashSet<string>(processedSettlementKeys, StringComparer.Ordinal);
        }

        public long Funds { get; private set; }
        public int Reliability { get; private set; }
        public OperatingTier Tier { get; internal set; }
        public ActiveRouteContract ActiveContract { get; internal set; }

        /// <summary>Every settlement key already applied — read by save capture only.</summary>
        public IReadOnlyCollection<string> ProcessedSettlementKeys => _processedSettlements;

        public bool HasSettled(SettlementId id) => _processedSettlements.Contains(id.Key);

        /// <summary>
        /// Applies a settlement exactly once: a repeat <paramref name="id"/> is refused rather
        /// than paid twice — the "safe to apply exactly once" guarantee this whole feature
        /// exists to prove. Fulfilling the contract's required rotations clears
        /// <see cref="ActiveContract"/> and pays <see cref="RouteContractDefinition.CompletionReward"/>
        /// on top of this rotation's own payment.
        /// </summary>
        internal FlightSettlement? TryApplySettlement(SettlementId id, RouteContractDefinition definition)
        {
            if (definition == null || ActiveContract == null || ActiveContract.DefinitionId != definition.Id)
                return null;
            if (!_processedSettlements.Add(id.Key))
                return null;

            ActiveContract.RecordRotation();
            var rotations = ActiveContract.CompletedRotations;
            var fulfilled = rotations >= definition.RequiredRotations;
            var payment = definition.PaymentPerRotation + (fulfilled ? definition.CompletionReward : 0);

            Funds += payment;
            Reliability = Clamp(Reliability + definition.ReliabilityGainPerRotation);
            if (fulfilled)
                ActiveContract = null;

            return new FlightSettlement(id, definition.Id, payment, definition.ReliabilityGainPerRotation, rotations, fulfilled);
        }

        /// <summary>
        /// Docks reliability for breaking a commitment against the active contract (ADR 0053:
        /// "a broken commitment ... should cost reliability"). A no-op unless
        /// <paramref name="definitionId"/> is the contract currently active, so cancelling a
        /// flight unrelated to any contract never costs anything.
        /// </summary>
        internal void PenalizeCancellation(string definitionId, int amount)
        {
            if (amount <= 0 || ActiveContract == null || ActiveContract.DefinitionId != definitionId)
                return;
            Reliability = Clamp(Reliability - amount);
        }

        private static int Clamp(int reliability) => Math.Max(0, Math.Min(100, reliability));
    }
}
