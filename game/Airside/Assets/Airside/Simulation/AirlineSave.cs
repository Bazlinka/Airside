using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// Everything needed to resume an airline game (ADR 0045). Plain public fields and
    /// no nullables so Unity's JsonUtility can write it; <see cref="AirlineSave"/> is
    /// the only thing that reads or writes the simulation from it.
    /// </summary>
    [Serializable]
    public sealed class AirlineSaveData
    {
        /// <summary>
        /// 2 added <see cref="SavedAtUtcTicks"/> for away catch-up. 3 added
        /// <see cref="EpochUtcTicks"/> for live real time. 4 replaces the two fictional
        /// AI operators with real Adelaide airlines. Older saves still load: a v2
        /// clock is aligned so the save moment reads as when it was saved, a v1 clock
        /// uses <see cref="AirlineClock.DefaultEpochUtc"/>. 6 adds the ADR 0053 airline
        /// career: funds, reliability, tier, an accepted contract and every settlement
        /// already applied. A pre-6 save gets a fresh Provisional career on load — it
        /// never retroactively pays trips completed before the career existed. 7 adds
        /// fulfilled-contract ids (so a completed contract cannot be farmed) and the
        /// opening funds / per-flight pay / dispatch-cost loop from ADR 0055. 8 adds
        /// departure-prep start time, a snapshot of a generated market contract, and
        /// completed player-rotation count (ADR 0056).
        /// </summary>
        public const int CurrentVersion = 8;

        public int Version = CurrentVersion;

        /// <summary>Real-world UTC time of the save, as <see cref="DateTime.Ticks"/>; 0 when unknown.</summary>
        public long SavedAtUtcTicks;

        /// <summary>Real UTC instant of simulation time zero (live airline clock).</summary>
        public long EpochUtcTicks;
        public string HomeCode;
        public long ClockSeconds;
        public long RunwayFreeAtSeconds;
        /// <summary>
        /// When the 12/30 strip is free. 0 on pre-dual-strip saves means "same as
        /// <see cref="RunwayFreeAtSeconds"/> was historically" — load treats 0 as
        /// immediately free so the cross strip is not stuck behind the old mutex.
        /// </summary>
        public long CrossRunwayFreeAtSeconds;
        public long TotalEvents;
        public uint RandomState;
        public List<AirlineRecord> Airlines = new();
        public List<AircraftRecord> Fleet = new();

        // ---- Career (ADR 0053, v6) ------------------------------------------------
        public long CareerFunds;
        public int CareerReliability;
        public string CareerTier;
        public bool HasActiveContract;
        public string ContractDefinitionId;
        public long ContractAcceptedAtSeconds;
        public int ContractCompletedRotations;
        public List<string> ProcessedSettlementKeys = new();
        public List<string> CompletedContractIds = new();
        public int CompletedPlayerRotations;
        public bool HasContractSnapshot;
        public string ContractOriginCode;
        public string ContractDestinationCode;
        public string ContractTypeId;
        public int ContractRequiredRotations;
        public long ContractPaymentPerRotation;
        public long ContractCompletionReward;
        public int ContractReliabilityGain;
        public string ContractRequiredTier;
        public int ContractReliabilityLoss;
        public string ContractUnlocksTier;
    }

    [Serializable]
    public sealed class AirlineRecord
    {
        public string Id;
        public string Name;
        public string LiveryHex;
        public bool IsPlayer;
    }

    [Serializable]
    public sealed class AircraftRecord
    {
        public string Registration;
        public string AirlineId;
        public string TypeId;
        public string State;
        public long StateStartedAt;
        public bool HasStateEnd;
        public long StateEndsAt;
        public string Stand;
        public string DepartureStand;
        public string Destination;
        public bool HasScheduled;
        public string ScheduledDestination;
        public long ScheduledDepartAt;
        public int ScheduledDelayMinutes;
        public bool ScheduledCancelled;
        public int CompletedTrips;
        public string AssignedRunway;
        public bool WentAroundThisTrip;
        public bool HasPrepStart;
        public long PrepStartedAt;
    }

    public static class AirlineSave
    {
        public static AirlineSaveData Capture(AirlineOperations operations, DateTime? savedAtUtc = null)
        {
            if (operations == null) throw new ArgumentNullException(nameof(operations));

            var data = new AirlineSaveData
            {
                SavedAtUtcTicks = savedAtUtc?.ToUniversalTime().Ticks ?? 0,
                EpochUtcTicks = operations.Clock.EpochUtcTicks,
                HomeCode = operations.Home.Code,
                ClockSeconds = operations.ProcessedTo.ElapsedSeconds,
                RunwayFreeAtSeconds = operations.RunwayFreeAt.ElapsedSeconds,
                CrossRunwayFreeAtSeconds = operations.CrossRunwayFreeAt.ElapsedSeconds,
                TotalEvents = operations.TotalEvents,
                RandomState = operations.RandomState,
                CareerFunds = operations.CareerState.Funds,
                CareerReliability = operations.CareerState.Reliability,
                CareerTier = operations.CareerState.Tier.ToString(),
                HasActiveContract = operations.CareerState.ActiveContract != null,
                ContractDefinitionId = operations.CareerState.ActiveContract?.DefinitionId ?? string.Empty,
                ContractAcceptedAtSeconds = operations.CareerState.ActiveContract?.AcceptedAt.ElapsedSeconds ?? 0,
                ContractCompletedRotations = operations.CareerState.ActiveContract?.CompletedRotations ?? 0,
                CompletedPlayerRotations = operations.CareerState.CompletedPlayerRotations
            };
            data.ProcessedSettlementKeys.AddRange(operations.CareerState.ProcessedSettlementKeys);
            data.CompletedContractIds.AddRange(operations.CareerState.CompletedContractIds);
            if (operations.CareerState.ActiveContract != null
                && operations.CareerState.TryFindDefinition(operations.CareerState.ActiveContract.DefinitionId,
                    out var definition))
            {
                data.HasContractSnapshot = true;
                data.ContractOriginCode = definition.OriginCode;
                data.ContractDestinationCode = definition.DestinationCode;
                data.ContractTypeId = definition.EligibleType.Id;
                data.ContractRequiredRotations = definition.RequiredRotations;
                data.ContractPaymentPerRotation = definition.PaymentPerRotation;
                data.ContractCompletionReward = definition.CompletionReward;
                data.ContractReliabilityGain = definition.ReliabilityGainPerRotation;
                data.ContractRequiredTier = definition.RequiredTier.ToString();
                data.ContractReliabilityLoss = definition.ReliabilityLossOnCancel;
                data.ContractUnlocksTier = definition.UnlocksTier.ToString();
            }

            foreach (var airline in operations.Airlines)
            {
                data.Airlines.Add(new AirlineRecord
                {
                    Id = airline.Id.Value, Name = airline.Name, LiveryHex = airline.LiveryHex, IsPlayer = airline.IsPlayer
                });
            }

            foreach (var a in operations.Fleet)
            {
                data.Fleet.Add(new AircraftRecord
                {
                    Registration = a.Registration,
                    AirlineId = a.Airline.Id.Value,
                    TypeId = a.Type.Id,
                    State = a.State.ToString(),
                    StateStartedAt = a.StateStartedAt.ElapsedSeconds,
                    HasStateEnd = a.StateEndsAt.HasValue,
                    StateEndsAt = a.StateEndsAt?.ElapsedSeconds ?? 0,
                    Stand = a.Stand.Value ?? string.Empty,
                    DepartureStand = a.DepartureStand.Value ?? string.Empty,
                    Destination = a.CurrentDestination?.Code ?? string.Empty,
                    HasScheduled = a.Scheduled.HasValue,
                    ScheduledDestination = a.Scheduled?.Destination.Code ?? string.Empty,
                    ScheduledDepartAt = a.Scheduled?.DepartAt.ElapsedSeconds ?? 0,
                    ScheduledDelayMinutes = a.Scheduled?.DelayMinutes ?? 0,
                    ScheduledCancelled = a.Scheduled?.Cancelled ?? false,
                    CompletedTrips = a.CompletedTrips,
                    AssignedRunway = a.AssignedRunway.ToString(),
                    WentAroundThisTrip = a.WentAroundThisTrip,
                    HasPrepStart = a.PrepStartedAt.HasValue,
                    PrepStartedAt = a.PrepStartedAt?.ElapsedSeconds ?? 0
                });
            }

            return data;
        }

        /// <summary>
        /// Rebuild an airline game from a save, with <paramref name="clock"/> already set to
        /// <see cref="AirlineSaveData.ClockSeconds"/>. Throws <see cref="FormatException"/>
        /// on anything it does not recognise, rather than resuming a half-right game.
        /// </summary>
        public static AirlineOperations Restore(AirlineSaveData data, ISimulationClock clock)
        {
            if (data == null) throw new FormatException("The save is empty.");
            if (clock == null) throw new ArgumentNullException(nameof(clock));
            if (data.Version < 1 || data.Version > AirlineSaveData.CurrentVersion)
                throw new FormatException($"Save version {data.Version} is not supported (expected 1 to {AirlineSaveData.CurrentVersion}).");
            if (clock.Now.ElapsedSeconds != data.ClockSeconds)
                throw new ArgumentException("Set the clock to the saved time before restoring.", nameof(clock));
            if (!DestinationCatalogue.TryFind(data.HomeCode, out var home) || !home.Equals(DestinationCatalogue.Adelaide))
                throw new FormatException($"Unknown home airport '{data.HomeCode}'.");
            if (data.RandomState == 0)
                throw new FormatException("The save has no random state.");

            var operations = new AirlineOperations(clock, new SeededRandomSource(data.RandomState), home,
                AirlineOperations.AdelaideStands);

            var airlines = new Dictionary<string, Airline>(StringComparer.Ordinal);
            foreach (var record in data.Airlines ?? new List<AirlineRecord>())
            {
                if (data.Version <= 3 && record.Id == "EMU")
                    continue;
                Airline airline;
                try
                {
                    airline = data.Version <= 3 && record.Id == "WTB"
                        ? Airline.VirginAustralia()
                        : new Airline(record.Id, record.Name, record.LiveryHex, record.IsPlayer);
                }
                catch (ArgumentException e)
                {
                    throw new FormatException($"Airline '{record.Id}' is invalid: {e.Message}");
                }

                if (!airlines.ContainsKey(airline.Id.Value))
                    operations.AddAirline(airline);
                airlines[record.Id] = airline;
                airlines[airline.Id.Value] = airline;
            }

            if (operations.PlayerAirline == null)
                throw new FormatException("The save has no player airline.");

            foreach (var record in data.Fleet ?? new List<AircraftRecord>())
            {
                if (data.Version <= 3 && record.AirlineId == "EMU")
                    continue;
                if (!airlines.TryGetValue(record.AirlineId ?? string.Empty, out var airline))
                    throw new FormatException($"{record.Registration} belongs to unknown airline '{record.AirlineId}'.");
                // AIR-010 replaces the short-lived Singapore A350 placeholder that shipped in
                // v5 saves. Keep the rotation's exact state, timing and stand while moving only
                // that known registration/type pair onto the real 787-10 fleet entry.
                var migrateSingapore787 = record.AirlineId == "SIA"
                    && record.Registration == "9V-SMA"
                    && record.TypeId == "A359";
                var registration = migrateSingapore787 ? "9V-SCA" : record.Registration;
                var typeId = migrateSingapore787 ? "B78X" : record.TypeId;
                if (!AircraftType.TryFromId(typeId, out var type))
                    throw new FormatException($"{record.Registration} has unknown aircraft type '{record.TypeId}'.");
                // Enum.TryParse also accepts numbers ("99") and comma lists, which yield values
                // no state machine branch handles; only a declared state name is a state.
                if (string.IsNullOrWhiteSpace(record.State)
                    || !Enum.TryParse(record.State, out FleetState state)
                    || !Enum.IsDefined(typeof(FleetState), state)
                    || !string.Equals(state.ToString(), record.State.Trim(), StringComparison.Ordinal))
                    throw new FormatException($"{record.Registration} has unknown state '{record.State}'.");

                operations.RestoreAircraft(
                    data.Version <= 3 && record.AirlineId == "WTB" ? "VH-8IA" : registration,
                    airline,
                    type,
                    state,
                    new SimulationTime(record.StateStartedAt),
                    record.HasStateEnd ? new SimulationTime(record.StateEndsAt) : null,
                    Stand(record.Stand),
                    Stand(record.DepartureStand),
                    OptionalDestination(record.Destination, record.Registration),
                    record.HasScheduled
                        ? new ScheduledDeparture(RequiredDestination(record.ScheduledDestination, record.Registration),
                            new SimulationTime(record.ScheduledDepartAt),
                            record.ScheduledDelayMinutes, record.ScheduledCancelled)
                        : null,
                    record.CompletedTrips);
                if (data.Version >= 5)
                    operations.RestoreMovementData(registration,
                        Enum.TryParse(record.AssignedRunway, out RunwayDirection runway)
                            ? runway : RunwayDirection.Runway05,
                        record.WentAroundThisTrip);
                if (data.Version >= 8 && record.HasPrepStart)
                    operations.RestorePrepData(registration, new SimulationTime(record.PrepStartedAt));
            }

            operations.RestoreTower(
                new SimulationTime(data.RunwayFreeAtSeconds),
                new SimulationTime(data.CrossRunwayFreeAtSeconds),
                data.TotalEvents);
            operations.Clock = ClockFor(data);
            if (data.Version <= 4)
            {
                operations.AddMissingRegionalCarriers();
                operations.AddMissingTerminalOperators();
            }

            // A pre-6 save never had a career: start fresh Provisional with the same
            // opening float a new airline gets, rather than back-computing rewards for
            // trips flown before contracts existed.
            RouteContractDefinition snapshot = null;
            if (data.Version >= 8 && data.HasContractSnapshot && data.HasActiveContract
                && !string.IsNullOrEmpty(data.ContractDefinitionId)
                && !RouteContractCatalogue.TryFind(data.ContractDefinitionId, out _))
            {
                if (!AircraftType.TryFromId(data.ContractTypeId, out var contractType))
                    throw new FormatException($"Unknown contract aircraft type '{data.ContractTypeId}'.");
                if (string.IsNullOrWhiteSpace(data.ContractRequiredTier)
                    || !Enum.TryParse(data.ContractRequiredTier, out OperatingTier requiredTier)
                    || !Enum.IsDefined(typeof(OperatingTier), requiredTier))
                    throw new FormatException($"Unknown contract tier '{data.ContractRequiredTier}'.");
                var unlocksTier = OperatingTier.Provisional;
                if (!string.IsNullOrEmpty(data.ContractUnlocksTier)
                    && Enum.TryParse(data.ContractUnlocksTier, out OperatingTier parsedUnlock)
                    && Enum.IsDefined(typeof(OperatingTier), parsedUnlock))
                    unlocksTier = parsedUnlock;
                snapshot = new RouteContractDefinition(
                    data.ContractDefinitionId, data.ContractOriginCode, data.ContractDestinationCode, contractType,
                    Math.Max(1, data.ContractRequiredRotations), Math.Max(0, data.ContractPaymentPerRotation),
                    Math.Max(0, data.ContractCompletionReward), data.ContractReliabilityGain, requiredTier,
                    data.ContractReliabilityLoss, unlocksTier);
            }

            var processedKeys = data.Version >= 6 ? data.ProcessedSettlementKeys ?? new List<string>() : new List<string>();
            var rotationCount = data.Version >= 8
                ? data.CompletedPlayerRotations
                : processedKeys.Count;

            operations.RestoreCareerState(
                data.Version >= 6 ? data.CareerFunds : AirlineCareerState.StartingFunds,
                data.Version >= 6 ? data.CareerReliability : AirlineCareerState.StartingReliability,
                data.Version >= 6 && !string.IsNullOrEmpty(data.CareerTier)
                    ? data.CareerTier : OperatingTier.Provisional.ToString(),
                data.Version >= 6 && data.HasActiveContract ? data.ContractDefinitionId : null,
                data.Version >= 6 ? data.ContractAcceptedAtSeconds : 0,
                data.Version >= 6 ? data.ContractCompletedRotations : 0,
                processedKeys,
                data.Version >= 7 ? data.CompletedContractIds ?? new List<string>() : new List<string>(),
                rotationCount,
                snapshot);

            return operations;
        }

        /// <summary>
        /// The live clock a save runs on. v3 stores its epoch; a v2 save is aligned so its
        /// save moment reads as the real time it was saved; v1 falls back to the default.
        /// </summary>
        public static AirlineClock ClockFor(AirlineSaveData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            // A corrupt or truncated tick count used to throw straight out of here. Restore
            // caught that, but the menu's saved-game summary calls this from OnGUI, so a bad
            // save threw every frame behind the panel instead of offering a new airline.
            if (IsRealDate(data.EpochUtcTicks))
                return new AirlineClock(data.EpochUtcTicks);
            return IsRealDate(data.SavedAtUtcTicks)
                ? AirlineClock.Aligned(new SimulationTime(data.ClockSeconds), new DateTime(data.SavedAtUtcTicks, DateTimeKind.Utc))
                : AirlineClock.Default;
        }

        /// <summary>True for a tick count DateTime accepts and a save could plausibly hold.</summary>
        public static bool IsRealDate(long utcTicks) =>
            utcTicks > 0 && utcTicks <= DateTime.MaxValue.Ticks;

        private static StableId Stand(string value) =>
            string.IsNullOrWhiteSpace(value) ? default : new StableId(value);

        private static Destination? OptionalDestination(string code, string registration) =>
            string.IsNullOrEmpty(code) ? null : RequiredDestination(code, registration);

        private static Destination RequiredDestination(string code, string registration)
        {
            if (!DestinationCatalogue.TryFind(code, out var destination))
                throw new FormatException($"{registration} refers to unknown destination '{code}'.");
            return destination;
        }
    }
}
