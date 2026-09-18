using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// One player airline's career progress (ADR 0053 / 0055): funds, reliability, operating
    /// tier, the accepted route contract and every contract already fulfilled. Mutated only
    /// by <see cref="AirlineOperations"/>; every settlement is applied at most once.
    /// </summary>
    public sealed class AirlineCareerState
    {
        public const int StartingReliability = 100;
        public const long StartingFunds = FlightEconomics.StartingFunds;

        private readonly HashSet<string> _processedSettlements;
        private readonly HashSet<string> _completedContracts;

        public AirlineCareerState(
            long? funds = null, int reliability = StartingReliability, OperatingTier tier = OperatingTier.Provisional,
            ActiveRouteContract activeContract = null, IEnumerable<string> processedSettlementKeys = null,
            IEnumerable<string> completedContractIds = null)
        {
            Funds = funds ?? StartingFunds;
            Reliability = Clamp(reliability);
            Tier = tier;
            ActiveContract = activeContract;
            _processedSettlements = processedSettlementKeys == null
                ? new HashSet<string>(StringComparer.Ordinal)
                : new HashSet<string>(processedSettlementKeys, StringComparer.Ordinal);
            _completedContracts = completedContractIds == null
                ? new HashSet<string>(StringComparer.Ordinal)
                : new HashSet<string>(completedContractIds, StringComparer.Ordinal);
        }

        public long Funds { get; private set; }
        public int Reliability { get; private set; }
        public OperatingTier Tier { get; internal set; }
        public ActiveRouteContract ActiveContract { get; internal set; }

        /// <summary>Every settlement key already applied — read by save capture only.</summary>
        public IReadOnlyCollection<string> ProcessedSettlementKeys => _processedSettlements;

        /// <summary>Contract ids the airline has already fulfilled — they cannot be accepted again.</summary>
        public IReadOnlyCollection<string> CompletedContractIds => _completedContracts;

        public bool HasSettled(SettlementId id) => _processedSettlements.Contains(id.Key);

        public bool HasCompleted(string definitionId) =>
            !string.IsNullOrEmpty(definitionId) && _completedContracts.Contains(definitionId);

        public bool CanAfford(long cost) => cost <= 0 || Funds >= cost;

        internal bool TryChargeDispatch(long cost)
        {
            if (cost < 0)
                throw new ArgumentOutOfRangeException(nameof(cost));
            if (cost == 0)
                return true;
            if (Funds < cost)
                return false;
            Funds -= cost;
            return true;
        }

        internal void RefundDispatch(long cost)
        {
            if (cost > 0)
                Funds += cost;
        }

        /// <summary>
        /// Applies a settlement exactly once: a repeat <paramref name="id"/> is refused rather
        /// than paid twice. Every player rotation pays <paramref name="baseRevenue"/>; a matching
        /// active contract adds its per-rotation bonus (and completion reward on the last).
        /// </summary>
        internal FlightSettlement? RecordCompletedRotation(
            SettlementId id, long baseRevenue, RouteContractDefinition matchingContract)
        {
            if (!_processedSettlements.Add(id.Key))
                return null;

            var payment = Math.Max(0, baseRevenue);
            var reliability = 0;
            var rotations = 0;
            var fulfilled = false;
            var contractId = string.Empty;

            if (matchingContract != null && ActiveContract != null
                && ActiveContract.DefinitionId == matchingContract.Id)
            {
                ActiveContract.RecordRotation();
                rotations = ActiveContract.CompletedRotations;
                fulfilled = rotations >= matchingContract.RequiredRotations;
                payment += matchingContract.PaymentPerRotation
                           + (fulfilled ? matchingContract.CompletionReward : 0);
                reliability = matchingContract.ReliabilityGainPerRotation;
                Reliability = Clamp(Reliability + reliability);
                contractId = matchingContract.Id;
                if (fulfilled)
                {
                    _completedContracts.Add(matchingContract.Id);
                    if (matchingContract.UnlocksTier > Tier)
                        Tier = matchingContract.UnlocksTier;
                    ActiveContract = null;
                }
            }

            Funds += payment;
            return new FlightSettlement(id, contractId, payment, reliability, rotations, fulfilled);
        }

        /// <summary>
        /// Applies a settlement exactly once: a repeat <paramref name="id"/> is refused rather
        /// than paid twice — the "safe to apply exactly once" guarantee this whole feature
        /// exists to prove. Fulfilling the contract's required rotations clears
        /// <see cref="ActiveContract"/> and pays <see cref="RouteContractDefinition.CompletionReward"/>
        /// on top of this rotation's own payment.
        /// </summary>
        internal FlightSettlement? TryApplySettlement(SettlementId id, RouteContractDefinition definition) =>
            RecordCompletedRotation(id, 0, definition);

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
