using System;
using System.Collections.Generic;
using System.Linq;
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
        /// completed player-rotation count (ADR 0056). 9 adds lifetime revenue (never reduced
        /// by spending, unlike <see cref="CareerFunds"/>) and a capped history of fulfilled
        /// contracts, for a real career-stats view. A pre-9 save loads with 0 lifetime revenue
        /// and no history — same "no retroactive credit" tolerance as every earlier version.
        /// 10 adds per-aircraft pushback lateness for on-time reliability (ADR 0078). A pre-10
        /// mid-trip aircraft restores with no lateness recorded, so that one settlement skips
        /// the punctuality delta rather than inventing one. 11 adds routine-check wear and an
        /// in-progress check end time (ADR 0085). Older saves load every aircraft as freshly
        /// checked, so a reload does not invent overdue penalties. 12 adds the player's Adelaide
        /// base level (ADR 0091); older saves infer enough capacity for their existing fleet.
        /// 13 adds selectable career goals, route proof, outstation fleet/bases, earned repeat
        /// schedules, recent service margins and active-play timing (ADR 0120).
        /// 17 (ADR 0138) adds the day so far (so a reload keeps the day's report and the
        /// profitable-day challenge), the 95% reliability streak and the recent post-flight
        /// reliability readings. Older saves start a fresh day and an empty streak.
        /// 18 (ADR 0178) gives each local aircraft a persistent logbook: join time,
        /// founding-aircraft identity, recorded revenue and routes. Older aircraft keep
        /// their completed-flight count and begin the detailed logbook on migration.
        /// 19 (ADR 0194) records which player aircraft are freighters. Older saves are all passenger.
        /// 20 (ADR 0239) marks aircraft on a ferry between bases (they earn nothing on arrival) and gives
        /// each outstation aircraft a logbook: join time, recorded revenue and routes. Older saves have no
        /// ferries and outstation aircraft begin their logbook on migration.
        /// 21 (ADR 0243) preserves preceding runway movements for follower-specific wake minima.
        /// Older saves keep their already-booked conservative strip deadlines.
        /// </summary>
        // v22 (ADR 0245): saved maintenance startup/taxi/position/repair/return phases.
        // Older timed checks keep their original completion and charge semantics.
        // v23: recorded live weather, final commitment latch and flat arrival-view records.
        // Older saves use deterministic weather and rebuild presentation.
        // v24 (ADR 2026-10-09-economy-v2-real-dollar-scale): money is in real Australian dollars. Older saves have every
        // money amount multiplied once on load by FlightCostModel.LegacySaveMoneyScale (see AirlineSave.ScaleLegacyMoney).
        // v25: the last Adelaide day whose lease and insurance was paid. Older saves start paying from the day they load.
        // v26: the bank loan balance. Older saves have no loan.
        public const int CurrentVersion = 26;

        public int Version = CurrentVersion;

        /// <summary>Real-world UTC time of the save, as <see cref="DateTime.Ticks"/>; 0 when unknown.</summary>
        public long SavedAtUtcTicks;

        /// <summary>Real UTC instant of simulation time zero (live airline clock).</summary>
        public long EpochUtcTicks;
        public string HomeCode;
        public long ClockSeconds;
        public List<WeatherObservationRecord> WeatherObservations = new();
        public List<ArrivalViewRecord> ArrivalViews = new();
        public long RunwayFreeAtSeconds;
        /// <summary>
        /// When the 12/30 strip is free. 0 on pre-dual-strip saves means "same as
        /// <see cref="RunwayFreeAtSeconds"/> was historically" — load treats 0 as
        /// immediately free so the cross strip is not stuck behind the old mutex.
        /// </summary>
        public long CrossRunwayFreeAtSeconds;
        /// <summary>v21: preceding movements for follower-specific wake separation.</summary>
        public RunwayWakeRecord MainWake;
        public RunwayWakeRecord CrossWake;
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

        // ---- Contract variety (v15 / ADR 0127) -------------------------------------
        /// <summary>Scheduled / Charter / Medical / Freight. Older saves are Scheduled.</summary>
        public string ContractKind;
        /// <summary>Seconds from acceptance to fly the contract; 0 for none (every pre-v15 contract).</summary>
        public long ContractDeadlineSeconds;
        /// <summary>Consecutive on-time pushbacks, for the challenges.</summary>
        public int OnTimeStreak;

        // ---- Player base (v12 / ADR 0091) ----------------------------------------
        public string PlayerBaseLevel;

        // ---- Career roadmap (v13) -------------------------------------------------
        public string PinnedCareerGoalId;

        /// <summary>v14 (ADR 0123): Relaxed / Standard / Demanding. Older saves play as Standard.</summary>
        public string Difficulty;

        /// <summary>v14: true when the player turned the first-flight coaching off at setup.</summary>
        public bool CoachingOff;
        public List<string> ServedDestinationCodes = new();
        public List<string> OutstationBaseCodes = new();
        public List<long> RecentServiceMargins = new();
        public int ManualRotations;
        public long ActivePlaySeconds;
        public long RegionalAtSeconds;
        public long DomesticAtSeconds;
        public long InternationalAtSeconds;
        public long FinaleAtSeconds;
        public List<OutstationAircraftSaveRecord> OutstationFleet = new();
        public List<RepeatScheduleSaveRecord> RepeatSchedules = new();

        // ---- Day so far and reliability history (v17 / ADR 0138) -------------------
        public int DayFlights;
        public long DayRevenue;
        public long DayCost;
        public string DayBestCode;
        public long DayBestMargin;
        public bool HasDayStartReliability;
        public int DayStartReliability;
        public int DayLateFlights;
        public int DayLateSeconds;
        public bool HasReportedDay;
        public long ReportedDay;
        public int HighReliabilityStreak;
        public List<int> RecentReliability = new();

        // ---- Bank loan (v26) -------------------------------------------------------
        public long CareerLoan;

        // ---- Standing costs (v25) --------------------------------------------------
        public bool HasStandingPaidDay;
        public long StandingPaidThroughDay;

        // ---- Career stats (v9) -----------------------------------------------------
        public long CareerLifetimeRevenue;
        public List<CompletedContractSaveRecord> ContractHistory = new();
    }

    /// <summary>One fulfilled contract, JsonUtility-friendly mirror of <see cref="CompletedContractRecord"/>.</summary>
    [Serializable]
    public sealed class CompletedContractSaveRecord
    {
        public string DefinitionId;
        public string OriginCode;
        public string DestinationCode;
        public long TotalPaid;
        public long CompletedAtSeconds;
    }

    [Serializable]
    public sealed class OutstationAircraftSaveRecord
    {
        public string Registration;
        public string TypeId;
        public string BaseCode;
        public string DestinationCode;
        public long DepartAtSeconds;
        public long ReturnAtSeconds;
        public int CompletedServices;
        public bool Automated;
        public int RotationsSinceCheck;
        public long CheckUntilSeconds;

        /// <summary>v20 (ADR 0239): the outstation aircraft's logbook. Older saves load it empty.</summary>
        public long JoinedAtSeconds;
        public long LifetimeRevenue;
        public int HistoryFlights;
        public List<AircraftRouteSaveRecord> RouteHistory = new();
    }

    [Serializable]
    public sealed class RepeatScheduleSaveRecord
    {
        public string Registration;
        public string DestinationCode;
        public int IntervalHours;
        public long NextEligibleAtSeconds;
        public bool Paused;
        public string Exception;
    }

    [Serializable]
    public sealed class AirlineRecord
    {
        public string Id;
        public string Name;
        public string LiveryHex;
        public bool IsPlayer;

        /// <summary>v14: the player's chosen flight code, or empty to derive one from the name.</summary>
        public string Code;
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
        public bool AutomatedTrip;
        public string AssignedRunway;
        public bool WentAroundThisTrip;
        public bool ArrivalCommittedBeforeStorm;
        public bool HasPrepStart;
        public long PrepStartedAt;
        public bool HasPushbackLateness;
        public int PushbackLatenessSeconds;

        /// <summary>v16 (ADR 0128): what made that pushback late, "Cause:seconds;…". Empty before v16.</summary>
        public string PushbackDelay;

        /// <summary>v11 (ADR 0085). Older saves load as 0: every aircraft starts fresh.</summary>
        public int RotationsSinceCheck;
        public long CheckUntilSeconds;

        /// <summary>v22: actual ground maintenance journey. Null preserves legacy timed checks.</summary>
        public MaintenanceJob MaintenanceJob;

        /// <summary>v18 (ADR 0178): persistent identity and individual-aircraft logbook.</summary>
        public long JoinedAirlineAtSeconds;
        public bool IsFoundingAircraft;
        public long LifetimeRevenue;
        public int HistoryFlights;
        public List<AircraftRouteSaveRecord> RouteHistory = new();

        /// <summary>v19 (ADR 0194): converted to a package freighter.</summary>
        public bool IsFreighter;

        /// <summary>v20 (ADR 0239): being ferried between bases; arrives without paying or counting a flight.</summary>
        public bool IsFerry;
    }

    [Serializable]
    public sealed class AircraftRouteSaveRecord
    {
        public string DestinationCode;
        public int Flights;
    }

    [Serializable]
    public sealed class ArrivalViewRecord
    {
        public string Registration, DestinationCode, TypeId;
        public int FleetState, Runway;
        public long StateStartedAt;
        public double LastTime, HoldingStartedAt;
        public bool Holding, Active;
        public float Metres, Speed, Lateral, X, Y, Z, EntryX, EntryY, EntryZ, ForwardX, ForwardZ;
    }

    public static class AirlineSave
    {
        // Unity serializes inline reference fields by value: null may round-trip as an
        // all-default object. Only that exact placeholder is absence; partial jobs stay invalid.
        private static bool EmptyMaintenanceJob(MaintenanceJob job) =>
            string.IsNullOrEmpty(job.OriginStand) && string.IsNullOrEmpty(job.HangarId)
            && string.IsNullOrEmpty(job.ReturnStand) && job.Phase == MaintenancePhase.Preparing
            && job.RequestedAt == 0 && job.PhaseStartedAt == 0 && job.PhaseEndsAt == 0
            && job.RepairSeconds == 0 && !job.RepairCompleted;

        private static RunwayWakeRecord CopyWake(RunwayWakeRecord record) => record == null ? null
            : new RunwayWakeRecord { TypeId = record.TypeId, Departure = record.Departure,
                EventAtSeconds = record.EventAtSeconds };

        public static AirlineSaveData Capture(AirlineOperations operations, DateTime? savedAtUtc = null)
        {
            if (operations == null) throw new ArgumentNullException(nameof(operations));

            var data = new AirlineSaveData
            {
                SavedAtUtcTicks = savedAtUtc?.ToUniversalTime().Ticks ?? 0,
                EpochUtcTicks = operations.Clock.EpochUtcTicks,
                HomeCode = operations.Home.Code,
                ClockSeconds = operations.ProcessedTo.ElapsedSeconds,
                WeatherObservations = operations.WeatherTimeline.Records.Select(r => r.Copy()).ToList(),
                RunwayFreeAtSeconds = operations.RunwayFreeAt.ElapsedSeconds,
                CrossRunwayFreeAtSeconds = operations.CrossRunwayFreeAt.ElapsedSeconds,
                MainWake = CopyWake(operations.MainWake),
                CrossWake = CopyWake(operations.CrossWake),
                TotalEvents = operations.TotalEvents,
                RandomState = operations.RandomState,
                CareerFunds = operations.CareerState.Funds,
                CareerReliability = operations.CareerState.Reliability,
                CareerTier = operations.CareerState.Tier.ToString(),
                HasActiveContract = operations.CareerState.ActiveContract != null,
                ContractDefinitionId = operations.CareerState.ActiveContract?.DefinitionId ?? string.Empty,
                ContractAcceptedAtSeconds = operations.CareerState.ActiveContract?.AcceptedAt.ElapsedSeconds ?? 0,
                ContractCompletedRotations = operations.CareerState.ActiveContract?.CompletedRotations ?? 0,
                CompletedPlayerRotations = operations.CareerState.CompletedPlayerRotations,
                CareerLifetimeRevenue = operations.CareerState.LifetimeRevenue,
                PlayerBaseLevel = operations.CareerState.BaseLevel.ToString(),
                PinnedCareerGoalId = operations.CareerState.PinnedGoalId,
                ManualRotations = operations.CareerState.ManualRotations,
                Difficulty = operations.CareerState.Difficulty.ToString(),
                CoachingOff = !operations.FirstFlightCoaching,
                OnTimeStreak = operations.CareerState.OnTimeStreak,
                ActivePlaySeconds = operations.CareerState.ActivePlaySeconds,
                RegionalAtSeconds = operations.CareerState.RegionalAtSeconds,
                DomesticAtSeconds = operations.CareerState.DomesticAtSeconds,
                InternationalAtSeconds = operations.CareerState.InternationalAtSeconds,
                FinaleAtSeconds = operations.CareerState.FinaleAtSeconds
            };
            data.ProcessedSettlementKeys.AddRange(operations.CareerState.ProcessedSettlementKeys);
            data.ServedDestinationCodes.AddRange(operations.CareerState.ServedDestinations);
            data.OutstationBaseCodes.AddRange(operations.CareerState.OutstationBases);
            data.RecentServiceMargins.AddRange(operations.CareerState.RecentServiceMargins);
            data.CareerLoan = operations.CareerState.Loan;
            var standingDay = operations.SaveStandingPaidDay();
            data.HasStandingPaidDay = standingDay.HasValue;
            data.StandingPaidThroughDay = standingDay ?? 0;
            var day = operations.SaveToday();
            data.DayFlights = day.Flights;
            data.DayRevenue = day.Revenue;
            data.DayCost = day.Cost;
            data.DayBestCode = day.BestCode ?? string.Empty;
            data.DayBestMargin = day.BestMargin;
            data.HasDayStartReliability = day.StartReliability.HasValue;
            data.DayStartReliability = day.StartReliability ?? 0;
            data.DayLateFlights = day.LateFlights;
            data.DayLateSeconds = day.LateSeconds;
            data.HasReportedDay = day.ReportedDay.HasValue;
            data.ReportedDay = day.ReportedDay ?? 0;
            data.HighReliabilityStreak = operations.CareerState.HighReliabilityStreak;
            data.RecentReliability.AddRange(operations.CareerState.RecentReliability);
            foreach (var aircraft in operations.OutstationFleet)
            {
                var outstationRecord = new OutstationAircraftSaveRecord
                {
                    Registration = aircraft.Registration,
                    TypeId = aircraft.Type.Id,
                    BaseCode = aircraft.BaseCode,
                    DestinationCode = aircraft.DestinationCode,
                    DepartAtSeconds = aircraft.DepartAtSeconds,
                    ReturnAtSeconds = aircraft.ReturnAtSeconds,
                    CompletedServices = aircraft.CompletedServices,
                    Automated = aircraft.Automated,
                    RotationsSinceCheck = aircraft.RotationsSinceCheck,
                    CheckUntilSeconds = aircraft.CheckUntilSeconds,
                    JoinedAtSeconds = aircraft.JoinedAtSeconds,
                    LifetimeRevenue = aircraft.LifetimeRevenue,
                    HistoryFlights = aircraft.HistoryFlights
                };
                foreach (var route in aircraft.RouteHistory.OrderBy(r => r.DestinationCode, StringComparer.Ordinal))
                    outstationRecord.RouteHistory.Add(new AircraftRouteSaveRecord
                    {
                        DestinationCode = route.DestinationCode,
                        Flights = route.Flights
                    });
                data.OutstationFleet.Add(outstationRecord);
            }
            foreach (var plan in operations.RepeatSchedules)
                data.RepeatSchedules.Add(new RepeatScheduleSaveRecord
                {
                    Registration = plan.Registration,
                    DestinationCode = plan.DestinationCode,
                    IntervalHours = plan.IntervalHours,
                    NextEligibleAtSeconds = plan.NextEligibleAtSeconds,
                    Paused = plan.Paused,
                    Exception = plan.Exception
                });
            data.CompletedContractIds.AddRange(operations.CareerState.CompletedContractIds);
            foreach (var record in operations.CareerState.ContractHistory)
            {
                data.ContractHistory.Add(new CompletedContractSaveRecord
                {
                    DefinitionId = record.DefinitionId,
                    OriginCode = record.OriginCode,
                    DestinationCode = record.DestinationCode,
                    TotalPaid = record.TotalPaid,
                    CompletedAtSeconds = record.CompletedAt.ElapsedSeconds
                });
            }
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
                data.ContractKind = definition.Kind.ToString();
                data.ContractDeadlineSeconds = definition.DeadlineSeconds;
            }

            foreach (var airline in operations.Airlines)
            {
                data.Airlines.Add(new AirlineRecord
                {
                    Id = airline.Id.Value, Name = airline.Name, LiveryHex = airline.LiveryHex, IsPlayer = airline.IsPlayer,
                    Code = airline.Code ?? string.Empty
                });
            }

            foreach (var a in operations.Fleet)
            {
                var record = new AircraftRecord
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
                    AutomatedTrip = a.AutomatedTrip,
                    AssignedRunway = a.AssignedRunway.ToString(),
                    WentAroundThisTrip = a.WentAroundThisTrip,
                    ArrivalCommittedBeforeStorm = a.ArrivalCommittedBeforeStorm,
                    HasPrepStart = a.PrepStartedAt.HasValue,
                    PrepStartedAt = a.PrepStartedAt?.ElapsedSeconds ?? 0,
                    HasPushbackLateness = a.PushbackLatenessSeconds.HasValue,
                    PushbackLatenessSeconds = a.PushbackLatenessSeconds ?? 0,
                    PushbackDelay = a.PushbackDelay?.Serialize() ?? string.Empty,
                    RotationsSinceCheck = a.RotationsSinceCheck,
                    CheckUntilSeconds = a.CheckUntil?.ElapsedSeconds ?? 0,
                    MaintenanceJob = a.MaintenanceJob?.Copy(),
                    JoinedAirlineAtSeconds = a.JoinedAirlineAt.ElapsedSeconds,
                    IsFoundingAircraft = a.IsFoundingAircraft,
                    LifetimeRevenue = a.LifetimeRevenue,
                    HistoryFlights = a.HistoryFlights,
                    IsFreighter = a.IsFreighter,
                    IsFerry = a.IsFerry
                };
                foreach (var route in a.RouteHistory.OrderBy(r => r.DestinationCode, StringComparer.Ordinal))
                    record.RouteHistory.Add(new AircraftRouteSaveRecord
                    {
                        DestinationCode = route.DestinationCode,
                        Flights = route.Flights
                    });
                data.Fleet.Add(record);
            }

            return data;
        }

        /// <summary>
        /// One-time conversion of a pre-version-24 save to the Economy v2 money scale: funds, the accepted contract's pay, the
        /// day's takings and every lifetime-revenue tally. Dispatch costs and fares are computed, not saved. Run on the freshly
        /// deserialised data inside <see cref="Restore"/>, so it cannot apply twice to the same save. A flight booked before the
        /// conversion and cancelled after it refunds at the new cost; that small difference is accepted.
        /// </summary>
        private static void ScaleLegacyMoney(AirlineSaveData data)
        {
            if (data == null || data.Version >= 24) return;
            const long scale = FlightCostModel.LegacySaveMoneyScale;
            data.CareerFunds *= scale;
            data.ContractPaymentPerRotation *= scale;
            data.ContractCompletionReward *= scale;
            data.DayRevenue *= scale;
            data.DayCost *= scale;
            data.DayBestMargin *= scale;
            data.CareerLifetimeRevenue *= scale;
            foreach (var contract in data.ContractHistory ?? new List<CompletedContractSaveRecord>())
                contract.TotalPaid *= scale;
            foreach (var aircraft in data.Fleet ?? new List<AircraftRecord>())
                aircraft.LifetimeRevenue *= scale;
            foreach (var aircraft in data.OutstationFleet ?? new List<OutstationAircraftSaveRecord>())
                aircraft.LifetimeRevenue *= scale;
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
            if (data.Version < 24)
                ScaleLegacyMoney(data);
            if (!DestinationCatalogue.TryFind(data.HomeCode, out var home) || !home.Equals(DestinationCatalogue.Adelaide))
                throw new FormatException($"Unknown home airport '{data.HomeCode}'.");
            if (data.RandomState == 0)
                throw new FormatException("The save has no random state.");

            var operations = new AirlineOperations(clock, new SeededRandomSource(data.RandomState), home,
                AirlineOperations.AdelaideStands);

            if (data.Version >= 23) operations.WeatherTimeline.Restore(data.WeatherObservations);
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
                        : new Airline(record.Id, record.Name, record.LiveryHex, record.IsPlayer,
                            data.Version >= 14 && record.IsPlayer ? record.Code : null);
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
                var restoredRegistration = data.Version <= 3 && record.AirlineId == "WTB"
                    ? "VH-8IA"
                    : registration;
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
                    restoredRegistration,
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
                {
                    if (string.IsNullOrEmpty(record.AssignedRunway)
                        || !Enum.TryParse(record.AssignedRunway, out RunwayDirection runway))
                        throw new FormatException(
                            $"Unknown assigned runway '{record.AssignedRunway}' for {registration}.");
                    operations.RestoreMovementData(restoredRegistration, runway, record.WentAroundThisTrip,
                        data.Version >= 23 && record.ArrivalCommittedBeforeStorm);
                }
                if (data.Version >= 8 && record.HasPrepStart)
                    operations.RestorePrepData(restoredRegistration, new SimulationTime(record.PrepStartedAt));
                if (data.Version >= 10 && record.HasPushbackLateness)
                    operations.RestorePushbackLateness(restoredRegistration, record.PushbackLatenessSeconds,
                        data.Version >= 16 ? record.PushbackDelay : null);
                if (data.Version >= 11)
                    operations.RestoreMaintenance(restoredRegistration, record.RotationsSinceCheck, record.CheckUntilSeconds);
                if (data.Version >= 22 && record.MaintenanceJob != null && !EmptyMaintenanceJob(record.MaintenanceJob))
                    operations.RestoreMaintenanceJob(restoredRegistration, record.MaintenanceJob);
                else if (state == FleetState.Maintenance)
                    throw new FormatException("Maintenance aircraft has no saved job: " + registration);
                if (data.Version >= 18)
                {
                    if (record.JoinedAirlineAtSeconds < 0 || record.JoinedAirlineAtSeconds > data.ClockSeconds
                        || record.LifetimeRevenue < 0 || record.HistoryFlights < 0)
                        throw new FormatException($"Invalid aircraft logbook for {registration}.");
                    var routes = new List<AircraftRouteTally>();
                    var routeTotal = 0;
                    var seenRoutes = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var route in record.RouteHistory ?? new List<AircraftRouteSaveRecord>())
                    {
                        if (route == null || route.Flights <= 0
                            || !seenRoutes.Add(route.DestinationCode ?? string.Empty)
                            || !DestinationCatalogue.TryFind(route.DestinationCode, out _))
                            throw new FormatException($"Invalid route history for {registration}.");
                        routeTotal += route.Flights;
                        routes.Add(new AircraftRouteTally(route.DestinationCode, route.Flights));
                    }
                    if (routeTotal != record.HistoryFlights || record.HistoryFlights > record.CompletedTrips)
                        throw new FormatException($"Aircraft logbook totals do not match for {registration}.");
                    operations.RestoreAircraftHistory(restoredRegistration,
                        new SimulationTime(record.JoinedAirlineAtSeconds), record.IsFoundingAircraft,
                        record.LifetimeRevenue, record.HistoryFlights, routes);
                }
                else
                {
                    // VH-PAX is the authored founding airframe in every pre-v18 player save.
                    operations.RestoreAircraftHistory(restoredRegistration, new SimulationTime(0),
                        airline.IsPlayer && string.Equals(restoredRegistration, "VH-PAX", StringComparison.OrdinalIgnoreCase),
                        0, 0, Array.Empty<AircraftRouteTally>());
                }
                if (data.Version >= 19 && record.IsFreighter && airline.IsPlayer)
                    operations.RestoreFreighter(restoredRegistration);
                if (data.Version >= 20 && record.IsFerry)
                {
                    // A ferry is a player aircraft on its way in; it is never parked, booked or on the ground
                    // at its far end, because the first thing it does on arrival is clear the flag.
                    if (!airline.IsPlayer
                        || state is not (FleetState.Inbound or FleetState.HoldingForLanding or FleetState.GoAround
                            or FleetState.Landing or FleetState.AwaitingStand or FleetState.TaxiIn))
                        throw new FormatException($"{registration} is marked as a ferry but is {record.State}.");
                    operations.RestoreFerry(restoredRegistration);
                }
                if (data.Version >= 13 && airline.IsPlayer)
                    operations.RestoreAutomatedTrip(restoredRegistration, record.AutomatedTrip);
            }

            operations.RestoreTower(
                new SimulationTime(data.RunwayFreeAtSeconds),
                new SimulationTime(data.CrossRunwayFreeAtSeconds),
                data.TotalEvents);
            if (data.Version >= 21)
                operations.RestoreWake(CopyWake(data.MainWake), CopyWake(data.CrossWake));
            operations.ReconcileRunwayFreeAt();
            operations.Clock = ClockFor(data);
            if (data.Version <= 4)
            {
                operations.AddMissingRegionalCarriers();
                operations.AddMissingEmergencyOperators();
                operations.AddMissingTerminalOperators();
            }
            else
            {
                // ADR 0207: every save gains the rescue helicopter on the helipad if it is not already there.
                operations.AddMissingEmergencyOperators(rotorcraftOnly: true);
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
                // v15: the contract's kind and deadline. Older saves had neither: Scheduled, no deadline.
                var kind = ContractKind.Scheduled;
                if (data.Version >= 15 && !string.IsNullOrEmpty(data.ContractKind)
                    && (!Enum.TryParse(data.ContractKind, out kind) || !Enum.IsDefined(typeof(ContractKind), kind)))
                    throw new FormatException($"Unknown contract kind '{data.ContractKind}'.");
                snapshot = new RouteContractDefinition(
                    data.ContractDefinitionId, data.ContractOriginCode, data.ContractDestinationCode, contractType,
                    Math.Max(1, data.ContractRequiredRotations), Math.Max(0, data.ContractPaymentPerRotation),
                    Math.Max(0, data.ContractCompletionReward), data.ContractReliabilityGain, requiredTier,
                    data.ContractReliabilityLoss, unlocksTier, kind,
                    data.Version >= 15 ? Math.Max(0, data.ContractDeadlineSeconds) : 0);
            }

            var processedKeys = data.Version >= 6 ? data.ProcessedSettlementKeys ?? new List<string>() : new List<string>();
            var rotationCount = data.Version >= 8
                ? data.CompletedPlayerRotations
                : processedKeys.Count;

            var contractHistory = new List<CompletedContractRecord>();
            if (data.Version >= 9 && data.ContractHistory != null)
            {
                foreach (var record in data.ContractHistory)
                {
                    if (record == null)
                        continue;
                    contractHistory.Add(new CompletedContractRecord(record.DefinitionId, record.OriginCode,
                        record.DestinationCode, record.TotalPaid, new SimulationTime(record.CompletedAtSeconds)));
                }
            }

            PlayerBaseLevel? savedBaseLevel = null;
            if (data.Version >= 12)
            {
                if (string.IsNullOrWhiteSpace(data.PlayerBaseLevel)
                    || !Enum.TryParse(data.PlayerBaseLevel, out PlayerBaseLevel parsedBase)
                    || !Enum.IsDefined(typeof(PlayerBaseLevel), parsedBase)
                    || !string.Equals(parsedBase.ToString(), data.PlayerBaseLevel.Trim(), StringComparison.Ordinal))
                    throw new FormatException($"Unknown player base level '{data.PlayerBaseLevel}'.");
                savedBaseLevel = parsedBase;
            }

            if (data.Version >= 13)
            {
                var bases = new HashSet<string>(StringComparer.Ordinal);
                foreach (var code in data.OutstationBaseCodes ?? new List<string>())
                    if (!AirlineOperations.IsOutstationCandidate(code)
                        || !bases.Add(code))
                        throw new FormatException("Invalid outstation base in save.");
                if (bases.Count > 3)
                    throw new FormatException("Too many outstation bases in save.");
                foreach (var code in data.ServedDestinationCodes ?? new List<string>())
                    if (code == data.HomeCode || !DestinationCatalogue.TryFind(code, out _))
                        throw new FormatException("Invalid served destination in save.");
            }

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
                snapshot,
                data.Version >= 9 ? data.CareerLifetimeRevenue : 0,
                contractHistory,
                savedBaseLevel,
                data.Version >= 13 ? data.PinnedCareerGoalId : null,
                data.Version >= 13 ? data.ServedDestinationCodes : ProvenHistoricalDestinations(data, contractHistory),
                data.Version >= 13 ? data.OutstationBaseCodes : null,
                data.Version >= 13 ? data.RecentServiceMargins : null,
                // Before v13 every service was planned by hand, so the delegation count is the
                // rotation count; starting it at zero re-locked repeat schedules for veterans.
                data.Version >= 13 ? data.ManualRotations : rotationCount,
                data.Version >= 13 ? data.ActivePlaySeconds : 0,
                data.Version >= 13 ? data.RegionalAtSeconds : 0,
                data.Version >= 13 ? data.DomesticAtSeconds : 0,
                data.Version >= 13 ? data.InternationalAtSeconds : 0,
                data.Version >= 13 ? data.FinaleAtSeconds : 0,
                ParseDifficulty(data));
            operations.FirstFlightCoaching = data.Version < 14 || !data.CoachingOff;
            if (data.Version >= 15)
                operations.CareerState.RestoreOnTimeStreak(data.OnTimeStreak);
            if (data.Version >= 17)
            {
                operations.CareerState.RestoreReliabilityHistory(data.HighReliabilityStreak, data.RecentReliability);
                operations.RestoreToday(new AirlineOperations.DaySnapshot(data.DayFlights, data.DayRevenue, data.DayCost,
                    data.DayBestCode, data.DayBestMargin,
                    data.HasDayStartReliability ? data.DayStartReliability : null,
                    data.DayLateFlights, data.DayLateSeconds,
                    data.HasReportedDay ? data.ReportedDay : null));
            }

            // v25: resume the lease and insurance ledger where it stopped. Older saves leave it null, so nothing is charged
            // retroactively; the first day observed after loading becomes the starting point.
            if (data.Version >= 25)
                operations.RestoreStandingPaidDay(data.HasStandingPaidDay ? data.StandingPaidThroughDay : null);
            if (data.Version >= 26)
                operations.CareerState.RestoreLoan(data.CareerLoan);

            if (data.Version >= 13)
            {
                var networkFleet = new List<OutstationAircraft>();
                var seenRegistrations = new HashSet<string>(StringComparer.Ordinal);
                var playerRegistrations = new HashSet<string>(StringComparer.Ordinal);
                foreach (var local in operations.Fleet)
                {
                    seenRegistrations.Add(local.Registration);
                    if (local.Airline.IsPlayer) playerRegistrations.Add(local.Registration);
                }
                var basedCounts = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var record in data.OutstationFleet ?? new List<OutstationAircraftSaveRecord>())
                {
                    if (record == null || string.IsNullOrWhiteSpace(record.Registration)
                        || !AircraftType.TryFromId(record.TypeId, out var type)
                        || !DestinationCatalogue.TryFind(record.BaseCode, out var origin)
                        || !operations.CareerState.HasOutstationBase(record.BaseCode)
                        || !seenRegistrations.Add(record.Registration)
                        || record.CompletedServices < 0 || record.RotationsSinceCheck < 0
                        || record.CheckUntilSeconds < 0)
                        throw new FormatException("Invalid outstation aircraft in save.");
                    basedCounts.TryGetValue(record.BaseCode, out var based);
                    if (++based > AirlineOperations.OutstationCapacity
                        || operations.PlayerFleetCount() + networkFleet.Count + 1 > AircraftAcquisition.MaxPlayerAircraft)
                        throw new FormatException("Outstation fleet exceeds capacity.");
                    basedCounts[record.BaseCode] = based;
                    var hasFlight = !string.IsNullOrEmpty(record.DestinationCode);
                    if (hasFlight)
                    {
                        if (!DestinationCatalogue.TryFind(record.DestinationCode, out var destination)
                            || destination.Code == record.BaseCode || destination.Code == operations.Home.Code
                            || !type.CanReach(origin.DistanceKmTo(destination))
                            || !RouteAccess.Allows(type, destination)
                            || record.DepartAtSeconds < 0 || record.ReturnAtSeconds <= record.DepartAtSeconds)
                            throw new FormatException("Invalid outstation service in save.");
                    }
                    else if (record.DepartAtSeconds != 0 || record.ReturnAtSeconds != 0 || record.Automated)
                        throw new FormatException("Inactive outstation aircraft has service state.");
                    playerRegistrations.Add(record.Registration);
                    var restoredOutstation = new OutstationAircraft(record.Registration, type, record.BaseCode,
                        record.DestinationCode, record.DepartAtSeconds, record.ReturnAtSeconds,
                        record.CompletedServices, record.Automated, record.RotationsSinceCheck,
                        record.CheckUntilSeconds);
                    if (data.Version >= 20)
                    {
                        if (record.JoinedAtSeconds < 0 || record.JoinedAtSeconds > data.ClockSeconds
                            || record.LifetimeRevenue < 0 || record.HistoryFlights < 0)
                            throw new FormatException($"Invalid aircraft logbook for {record.Registration}.");
                        var outstationRoutes = new List<AircraftRouteTally>();
                        var outstationRouteTotal = 0;
                        var outstationSeen = new HashSet<string>(StringComparer.Ordinal);
                        foreach (var route in record.RouteHistory ?? new List<AircraftRouteSaveRecord>())
                        {
                            if (route == null || route.Flights <= 0
                                || !outstationSeen.Add(route.DestinationCode ?? string.Empty)
                                || !DestinationCatalogue.TryFind(route.DestinationCode, out _))
                                throw new FormatException($"Invalid route history for {record.Registration}.");
                            outstationRouteTotal += route.Flights;
                            outstationRoutes.Add(new AircraftRouteTally(route.DestinationCode, route.Flights));
                        }
                        if (outstationRouteTotal != record.HistoryFlights
                            || record.HistoryFlights > record.CompletedServices)
                            throw new FormatException($"Aircraft logbook totals do not match for {record.Registration}.");
                        restoredOutstation.RestoreHistory(record.JoinedAtSeconds, record.LifetimeRevenue,
                            record.HistoryFlights, outstationRoutes);
                    }
                    networkFleet.Add(restoredOutstation);
                }
                var plans = new List<RepeatSchedule>();
                var seenPlans = new HashSet<string>(StringComparer.Ordinal);
                foreach (var record in data.RepeatSchedules ?? new List<RepeatScheduleSaveRecord>())
                {
                    if (record == null || string.IsNullOrWhiteSpace(record.Registration)
                        || !seenPlans.Add(record.Registration)
                        || !playerRegistrations.Contains(record.Registration)
                        || !DestinationCatalogue.TryFind(record.DestinationCode, out _)
                        || record.NextEligibleAtSeconds < 0
                        || (record.IntervalHours != 6 && record.IntervalHours != 12 && record.IntervalHours != 24))
                        throw new FormatException("Invalid repeat schedule in save.");
                    plans.Add(new RepeatSchedule(record.Registration, record.DestinationCode,
                        record.IntervalHours, record.NextEligibleAtSeconds, record.Paused, record.Exception));
                }
                operations.RestoreNetworkState(networkFleet, plans);
            }

            return operations;
        }

        /// <summary>Only destinations proven by the old contract ledger are credited during migration.</summary>
        /// <summary>
        /// There is one career balance (ADR 0127): v14 saves founded on Relaxed or Demanding continue on
        /// Standard. The field is still written so older builds can read newer saves.
        /// </summary>
        private static CareerDifficulty ParseDifficulty(AirlineSaveData data) => CareerDifficulty.Standard;

        private static IReadOnlyList<string> ProvenHistoricalDestinations(AirlineSaveData data,
            IReadOnlyList<CompletedContractRecord> history)
        {
            var codes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var record in history)
                if (!string.IsNullOrEmpty(record.DestinationCode)) codes.Add(record.DestinationCode);
            foreach (var id in data.CompletedContractIds ?? new List<string>())
            {
                if (RouteContractCatalogue.TryFind(id, out var definition))
                    codes.Add(definition.DestinationCode);
                else if (id != null)
                {
                    var parts = id.Split('-');
                    if (parts.Length >= 5 && parts[0] == "MKT"
                        && DestinationCatalogue.TryFind(parts[3], out _))
                        codes.Add(parts[3]);
                }
            }
            return new List<string>(codes);
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
