using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// One player airline's career progress (ADR 0053 / 0055 / 0056): funds, reliability,
    /// operating tier, the accepted route contract, issued market definitions and every
    /// settlement already applied. Mutated only by <see cref="AirlineOperations"/>.
    /// </summary>
    public sealed class AirlineCareerState
    {
        public const int StartingReliability = 100;
        public const long StartingFunds = FlightEconomics.StartingFunds;

        /// <summary>Most recent fulfilled contracts kept for history — oldest drops first past this.</summary>
        public const int MaxContractHistory = 10;

        private readonly HashSet<string> _processedSettlements;
        private readonly HashSet<string> _completedContracts;
        private readonly Dictionary<string, RouteContractDefinition> _issued;
        private readonly List<CompletedContractRecord> _contractHistory;

        public AirlineCareerState(
            long? funds = null, int reliability = StartingReliability, OperatingTier tier = OperatingTier.Provisional,
            ActiveRouteContract activeContract = null, IEnumerable<string> processedSettlementKeys = null,
            IEnumerable<string> completedContractIds = null, int completedPlayerRotations = 0,
            IEnumerable<RouteContractDefinition> issuedDefinitions = null, long lifetimeRevenue = 0,
            IEnumerable<CompletedContractRecord> contractHistory = null)
        {
            Funds = funds ?? StartingFunds;
            Reliability = Clamp(reliability);
            Tier = tier;
            ActiveContract = activeContract;
            CompletedPlayerRotations = Math.Max(0, completedPlayerRotations);
            LifetimeRevenue = Math.Max(0, lifetimeRevenue);
            _processedSettlements = processedSettlementKeys == null
                ? new HashSet<string>(StringComparer.Ordinal)
                : new HashSet<string>(processedSettlementKeys, StringComparer.Ordinal);
            _completedContracts = completedContractIds == null
                ? new HashSet<string>(StringComparer.Ordinal)
                : new HashSet<string>(completedContractIds, StringComparer.Ordinal);
            _issued = new Dictionary<string, RouteContractDefinition>(StringComparer.Ordinal);
            _contractHistory = contractHistory == null
                ? new List<CompletedContractRecord>()
                : new List<CompletedContractRecord>(contractHistory);
            if (issuedDefinitions == null)
                return;
            foreach (var definition in issuedDefinitions)
                Remember(definition);
        }

        public long Funds { get; private set; }
        public int Reliability { get; private set; }
        public OperatingTier Tier { get; internal set; }
        public ActiveRouteContract ActiveContract { get; internal set; }
        public int CompletedPlayerRotations { get; private set; }

        /// <summary>
        /// Every rotation payment ever settled, including contract bonuses — unlike
        /// <see cref="Funds"/>, spending (buying an aircraft, dispatch cost) never reduces it.
        /// A real answer to "how much have I actually earned", not just the current balance.
        /// </summary>
        public long LifetimeRevenue { get; private set; }

        /// <summary>Every settlement key already applied — read by save capture only.</summary>
        public IReadOnlyCollection<string> ProcessedSettlementKeys => _processedSettlements;

        /// <summary>Contract ids the airline has already fulfilled — they cannot be accepted again.</summary>
        public IReadOnlyCollection<string> CompletedContractIds => _completedContracts;

        /// <summary>Most recently fulfilled contracts first, capped at <see cref="MaxContractHistory"/>.</summary>
        public IReadOnlyList<CompletedContractRecord> ContractHistory => _contractHistory;

        public IEnumerable<RouteContractDefinition> IssuedDefinitions => _issued.Values;

        public bool HasSettled(SettlementId id) => _processedSettlements.Contains(id.Key);

        public bool HasCompleted(string definitionId) =>
            !string.IsNullOrEmpty(definitionId) && _completedContracts.Contains(definitionId);

        public bool CanAfford(long cost) => cost <= 0 || Funds >= cost;

        public bool TryFindDefinition(string id, out RouteContractDefinition definition)
        {
            definition = null;
            if (string.IsNullOrEmpty(id))
                return false;
            if (_issued.TryGetValue(id, out definition))
                return true;
            return RouteContractCatalogue.TryFind(id, out definition);
        }

        internal void Remember(RouteContractDefinition definition)
        {
            if (definition == null || _issued.ContainsKey(definition.Id))
                return;
            _issued[definition.Id] = definition;
        }

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

        internal bool TryChargePurchase(long cost) => TryChargeDispatch(cost);

        /// <summary>
        /// Applies a settlement exactly once: a repeat <paramref name="id"/> is refused rather
        /// than paid twice. Every player rotation pays <paramref name="baseRevenue"/>, scaled by
        /// <see cref="FlightEconomics.ReliabilityMultiplier"/> — a matching active contract's own
        /// per-rotation bonus and completion reward are untouched by that multiplier, so a
        /// contract's advertised numbers always pay exactly what it advertised.
        /// </summary>
        internal FlightSettlement? RecordCompletedRotation(
            SettlementId id, long baseRevenue, RouteContractDefinition matchingContract,
            IReadOnlyList<AircraftType> ownedTypes = null, SimulationTime now = default)
        {
            if (!_processedSettlements.Add(id.Key))
                return null;

            CompletedPlayerRotations++;
            var payment = Math.Max(0,
                (long)Math.Round(Math.Max(0, baseRevenue) * FlightEconomics.ReliabilityMultiplier(Reliability)));
            var reliability = 0;
            var rotations = 0;
            var fulfilled = false;
            var contractId = string.Empty;

            if (matchingContract != null && ActiveContract != null
                && ActiveContract.DefinitionId == matchingContract.Id)
            {
                // Fulfilment only depends on the rotation count, so it can be known before the
                // contract's own payment for this rotation is folded into ActiveContract's
                // running total (needed below for the history record's TotalPaid).
                rotations = ActiveContract.CompletedRotations + 1;
                fulfilled = rotations >= matchingContract.RequiredRotations;
                var contractPortion = matchingContract.PaymentPerRotation
                                       + (fulfilled ? matchingContract.CompletionReward : 0);
                ActiveContract.RecordRotation(contractPortion);
                payment += contractPortion;
                reliability = matchingContract.ReliabilityGainPerRotation;
                Reliability = Clamp(Reliability + reliability);
                contractId = matchingContract.Id;
                if (fulfilled)
                {
                    _completedContracts.Add(matchingContract.Id);
                    if (matchingContract.UnlocksTier > Tier)
                        Tier = matchingContract.UnlocksTier;
                    AddContractHistory(new CompletedContractRecord(matchingContract.Id, matchingContract.OriginCode,
                        matchingContract.DestinationCode, ActiveContract.TotalPaid, now));
                    ActiveContract = null;
                }
            }

            Funds += payment;
            LifetimeRevenue += payment;
            EvaluateTier(ownedTypes);
            return new FlightSettlement(id, contractId, payment, reliability, rotations, fulfilled);
        }

        /// <summary>Newest first, capped at <see cref="MaxContractHistory"/> — oldest drops off.</summary>
        private void AddContractHistory(CompletedContractRecord record)
        {
            _contractHistory.Insert(0, record);
            if (_contractHistory.Count > MaxContractHistory)
                _contractHistory.RemoveAt(_contractHistory.Count - 1);
        }

        // Tier thresholds — named so CareerProgress (the "what's left for the next tier" HUD
        // helper) can read the exact same numbers instead of a second, driftable copy.
        public const int RegionalRotations = 8;
        public const int RegionalReliability = 70;
        public const int DomesticRotations = 18;
        public const int DomesticReliability = 80;
        public const int InternationalRotations = 28;
        public const int InternationalReliability = 88;

        /// <summary>
        /// Capability milestone from rotations, reliability and owned types — not a named
        /// contract ladder (ADR 0056). Never drops a tier.
        /// </summary>
        internal void EvaluateTier(IReadOnlyList<AircraftType> ownedTypes)
        {
            var ownsDash = Owns(ownedTypes, AircraftType.Dash8Q400);
            var ownsJet = Owns(ownedTypes, AircraftType.Boeing7378)
                          || Owns(ownedTypes, AircraftType.AirbusA321Neo)
                          || Owns(ownedTypes, AircraftType.AirbusA350900)
                          || Owns(ownedTypes, AircraftType.Boeing78710);

            if (Tier < OperatingTier.Regional
                && CompletedPlayerRotations >= RegionalRotations && Reliability >= RegionalReliability)
                Tier = OperatingTier.Regional;
            if (Tier < OperatingTier.Domestic
                && CompletedPlayerRotations >= DomesticRotations && Reliability >= DomesticReliability
                && (ownsDash || ownsJet))
                Tier = OperatingTier.Domestic;
            // International unlocks on jet ops — not on already owning a widebody (A350/787
            // still require International to buy, so requiring ownsWide was a deadlock).
            if (Tier < OperatingTier.International
                && CompletedPlayerRotations >= InternationalRotations && Reliability >= InternationalReliability
                && ownsJet)
                Tier = OperatingTier.International;
        }

        /// <summary>True if any listed type is one of the jet types Domestic/International accept.</summary>
        internal static bool OwnsAnyJet(IReadOnlyList<AircraftType> ownedTypes) =>
            Owns(ownedTypes, AircraftType.Boeing7378) || Owns(ownedTypes, AircraftType.AirbusA321Neo)
            || Owns(ownedTypes, AircraftType.AirbusA350900) || Owns(ownedTypes, AircraftType.Boeing78710);

        /// <summary>True if any listed type is the Dash 8 (Domestic's other qualifying type).</summary>
        internal static bool OwnsDash8(IReadOnlyList<AircraftType> ownedTypes) =>
            Owns(ownedTypes, AircraftType.Dash8Q400);

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

        private static bool Owns(IReadOnlyList<AircraftType> owned, AircraftType type)
        {
            if (owned == null || type == null)
                return false;
            foreach (var candidate in owned)
                if (candidate != null && candidate.Id == type.Id)
                    return true;
            return false;
        }

        private static int Clamp(int reliability) => Math.Max(0, Math.Min(100, reliability));
    }
}
