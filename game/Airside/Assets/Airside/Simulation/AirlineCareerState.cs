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
        private readonly HashSet<string> _servedDestinations;
        private readonly HashSet<string> _outstationBases;
        private readonly Queue<long> _recentServiceMargins;

        public AirlineCareerState(
            long? funds = null, int reliability = StartingReliability, OperatingTier tier = OperatingTier.Provisional,
            ActiveRouteContract activeContract = null, IEnumerable<string> processedSettlementKeys = null,
            IEnumerable<string> completedContractIds = null, int completedPlayerRotations = 0,
            IEnumerable<RouteContractDefinition> issuedDefinitions = null, long lifetimeRevenue = 0,
            IEnumerable<CompletedContractRecord> contractHistory = null,
            PlayerBaseLevel baseLevel = PlayerBaseLevel.Starter,
            string pinnedGoalId = null, IEnumerable<string> servedDestinations = null,
            IEnumerable<string> outstationBases = null, IEnumerable<long> recentServiceMargins = null,
            int manualRotations = 0, long activePlaySeconds = 0,
            long regionalAtSeconds = 0, long domesticAtSeconds = 0,
            long internationalAtSeconds = 0, long finaleAtSeconds = 0)
        {
            Funds = funds ?? StartingFunds;
            Reliability = Clamp(reliability);
            Tier = tier;
            ActiveContract = activeContract;
            CompletedPlayerRotations = Math.Max(0, completedPlayerRotations);
            LifetimeRevenue = Math.Max(0, lifetimeRevenue);
            BaseLevel = baseLevel;
            PinnedGoalId = pinnedGoalId ?? string.Empty;
            ManualRotations = Math.Max(0, manualRotations);
            ActivePlaySeconds = Math.Max(0, activePlaySeconds);
            RegionalAtSeconds = Math.Max(0, regionalAtSeconds);
            DomesticAtSeconds = Math.Max(0, domesticAtSeconds);
            InternationalAtSeconds = Math.Max(0, internationalAtSeconds);
            FinaleAtSeconds = Math.Max(0, finaleAtSeconds);
            _servedDestinations = new HashSet<string>(servedDestinations ?? Array.Empty<string>(), StringComparer.Ordinal);
            _outstationBases = new HashSet<string>(outstationBases ?? Array.Empty<string>(), StringComparer.Ordinal);
            _recentServiceMargins = new Queue<long>();
            if (recentServiceMargins != null)
                foreach (var margin in recentServiceMargins)
                    AppendMargin(margin);
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
        public PlayerBaseLevel BaseLevel { get; internal set; }
        public PlayerBaseSpec Base => PlayerBase.For(BaseLevel);
        public string PinnedGoalId { get; private set; }
        public int ManualRotations { get; private set; }
        public long ActivePlaySeconds { get; private set; }
        public long RegionalAtSeconds { get; private set; }
        public long DomesticAtSeconds { get; private set; }
        public long InternationalAtSeconds { get; private set; }
        public long FinaleAtSeconds { get; private set; }
        internal void AddActivePlaySecond() => ActivePlaySeconds++;
        /// <summary>The established-airline finale has been recorded (once, ever; setbacks never remove it).</summary>
        public bool FinaleReached => _processedSettlements.Contains(CareerRoadmap.FinaleKey);
        internal void MarkFinale() { if (FinaleAtSeconds == 0) FinaleAtSeconds = ActivePlaySeconds; }
        public IReadOnlyCollection<string> ServedDestinations => _servedDestinations;
        public IReadOnlyCollection<string> OutstationBases => _outstationBases;

        public bool HasOutstationBase(string code) => code != null && _outstationBases.Contains(code);
        public IReadOnlyCollection<long> RecentServiceMargins => _recentServiceMargins;
        public int BaseCount => 1 + _outstationBases.Count;
        public long RecentOperatingMargin
        {
            get { long total = 0; foreach (var value in _recentServiceMargins) total += value; return total; }
        }

        internal void PinGoal(string id) => PinnedGoalId = id ?? string.Empty;

        internal void RecordService(string destinationCode, long margin, bool manual)
        {
            if (!string.IsNullOrWhiteSpace(destinationCode))
                _servedDestinations.Add(destinationCode);
            AppendMargin(margin);
            if (manual) ManualRotations++;
        }

        internal bool AddOutstationBase(string code) =>
            !string.IsNullOrWhiteSpace(code) && code != "ADL" && _outstationBases.Add(code);

        private void AppendMargin(long margin)
        {
            _recentServiceMargins.Enqueue(margin);
            while (_recentServiceMargins.Count > 30)
                _recentServiceMargins.Dequeue();
        }

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

        /// <summary>True when any flight has already settled under <paramref name="registration"/>.</summary>
        public bool HasSettlementHistory(string registration)
        {
            if (string.IsNullOrWhiteSpace(registration)) return false;
            var prefix = registration.Trim() + "#";
            foreach (var key in _processedSettlements)
                if (key.StartsWith(prefix, StringComparison.Ordinal))
                    return true;
            return false;
        }

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

        // A recovery contract underwrites its own first dispatch when cash has run out.
        // The cost still leaves the balance, and the scheduled service must settle to repay it.
        internal bool TryChargeRecoveryDispatch(long cost, string destinationCode)
        {
            if (cost < 0 || ActiveContract == null
                || !ActiveContract.DefinitionId.StartsWith("REC-", StringComparison.Ordinal)
                || !TryFindDefinition(ActiveContract.DefinitionId, out var definition)
                || definition.DestinationCode != destinationCode
                || Funds < 0 || Funds >= cost)
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
        /// Pays a one-off reward (a campaign chapter) at most once, ever: the key joins the
        /// settlement keys saves already carry, so a reload cannot pay it twice.
        /// </summary>
        internal bool TryAward(string key, long amount)
        {
            if (string.IsNullOrEmpty(key) || !_processedSettlements.Add(key))
                return false;
            Funds += Math.Max(0, amount);
            return true;
        }

        /// <summary>
        /// Applies a settlement exactly once: a repeat <paramref name="id"/> is refused rather
        /// than paid twice. Every player rotation pays <paramref name="baseRevenue"/>, scaled by
        /// <see cref="FlightEconomics.ReliabilityMultiplier"/> — a matching active contract's own
        /// per-rotation bonus and completion reward are untouched by that multiplier, so a
        /// contract's advertised numbers always pay exactly what it advertised.
        /// </summary>
        internal FlightSettlement? RecordCompletedRotation(
            SettlementId id, long baseRevenue, RouteContractDefinition matchingContract,
            IReadOnlyList<AircraftType> ownedTypes = null, SimulationTime now = default, int fleetCount = 0)
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
                    AddContractHistory(new CompletedContractRecord(matchingContract.Id, matchingContract.OriginCode,
                        matchingContract.DestinationCode, ActiveContract.TotalPaid, now));
                    ActiveContract = null;
                }
            }

            Funds += payment;
            LifetimeRevenue += payment;
            EvaluateTier(ownedTypes, fleetCount);
            return new FlightSettlement(id, contractId, payment, reliability, rotations, fulfilled);
        }

        /// <summary>Newest first, capped at <see cref="MaxContractHistory"/> — oldest drops off.</summary>
        private void AddContractHistory(CompletedContractRecord record)
        {
            _contractHistory.Insert(0, record);
            if (_contractHistory.Count > MaxContractHistory)
                _contractHistory.RemoveAt(_contractHistory.Count - 1);
        }

        /// <summary>
        /// Promotes one operating tier at a time, in order: a tier is reached only after the one
        /// below it, so a player can never skip Regional by finishing Regional-stage goals while
        /// still Provisional. Returns how many tiers were gained (0 almost always).
        /// </summary>
        internal int EvaluateTier(IReadOnlyList<AircraftType> ownedTypes, int fleetCount = 0)
        {
            var gained = 0;
            while (Tier < OperatingTier.International)
            {
                var next = Tier + 1;
                if (!CareerRoadmap.CanReach(this, ownedTypes, next, fleetCount))
                    break;
                Tier = next;
                gained++;
                switch (next)
                {
                    case OperatingTier.Regional when RegionalAtSeconds == 0: RegionalAtSeconds = ActivePlaySeconds; break;
                    case OperatingTier.Domestic when DomesticAtSeconds == 0: DomesticAtSeconds = ActivePlaySeconds; break;
                    case OperatingTier.International when InternationalAtSeconds == 0: InternationalAtSeconds = ActivePlaySeconds; break;
                }
            }
            return gained;
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

        /// <summary>
        /// Walks away from the active contract: it pays nothing more, costs
        /// <paramref name="reliabilityLoss"/>, and can be accepted again later. The only way out of
        /// a contract the airline can no longer fly (sold type, no cash), so it must always exist.
        /// </summary>
        internal bool AbandonContract(int reliabilityLoss)
        {
            if (ActiveContract == null)
                return false;
            Reliability = Clamp(Reliability - Math.Max(0, reliabilityLoss));
            ActiveContract = null;
            return true;
        }

        /// <summary>
        /// Applies an on-time / late reliability delta from pushback punctuality (ADR 0078).
        /// Zero is a no-op; the result is always clamped to 0..100.
        /// </summary>
        internal void ApplyPunctuality(int reliabilityDelta)
        {
            if (reliabilityDelta == 0)
                return;
            Reliability = Clamp(Reliability + reliabilityDelta);
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
