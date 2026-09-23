using System;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>Fuel → catering → baggage → boarding before a player pushback (ADR 0056 / 0093).</summary>
    public enum DeparturePrepStage
    {
        Idle,
        Fuel,
        Catering,
        Baggage,
        Boarding,
        Ready
    }

    public readonly struct DeparturePrepStatus
    {
        public DeparturePrepStatus(
            DeparturePrepStage stage,
            double stageProgress,
            bool ready,
            string label,
            double fuelProgress,
            double cateringProgress,
            double baggageProgress,
            double boardingProgress,
            long remainingSeconds)
        {
            Stage = stage;
            StageProgress = stageProgress;
            Ready = ready;
            Label = label;
            FuelProgress = fuelProgress;
            CateringProgress = cateringProgress;
            BaggageProgress = baggageProgress;
            BoardingProgress = boardingProgress;
            RemainingSeconds = remainingSeconds;
        }

        public DeparturePrepStage Stage { get; }
        public double StageProgress { get; }
        public bool Ready { get; }
        public string Label { get; }

        /// <summary>0..1 through fuelling. Completed stages stay at 1; later stages stay at 0.</summary>
        public double FuelProgress { get; }

        /// <summary>0..1 through catering.</summary>
        public double CateringProgress { get; }

        /// <summary>0..1 through baggage handling.</summary>
        public double BaggageProgress { get; }

        /// <summary>0..1 through boarding.</summary>
        public double BoardingProgress { get; }

        /// <summary>Seconds left in the current stage, or 0 when idle/ready.</summary>
        public long RemainingSeconds { get; }

        public int StagePercent => DeparturePrep.Percent(StageProgress);
        public int FuelPercent => DeparturePrep.Percent(FuelProgress);
        public int CateringPercent => DeparturePrep.Percent(CateringProgress);
        public int BaggagePercent => DeparturePrep.Percent(BaggageProgress);
        public int BoardingPercent => DeparturePrep.Percent(BoardingProgress);
    }

    /// <summary>
    /// Pure function of type, prep-start time and now. Simulation will not release pushback
    /// until <see cref="IsReady"/>; presentation only draws the stages.
    /// </summary>
    public static class DeparturePrep
    {
        public const long FuelSeconds = 90;
        public const long CateringSeconds = 75;
        public const long BaggageSeconds = 90;
        public const long BoardingSeconds = 120;

        public static long TotalSeconds(AircraftType type) =>
            Scale(type, FuelSeconds, PlayerBaseLevel.Starter)
            + Scale(type, CateringSeconds, PlayerBaseLevel.Starter)
            + Scale(type, BaggageSeconds, PlayerBaseLevel.Starter)
            + Scale(type, BoardingSeconds, PlayerBaseLevel.Starter);

        public static long TotalSeconds(AircraftType type, PlayerBaseLevel baseLevel) =>
            Scale(type, FuelSeconds, baseLevel)
            + Scale(type, CateringSeconds, baseLevel)
            + Scale(type, BaggageSeconds, baseLevel)
            + Scale(type, BoardingSeconds, baseLevel);

        /// <summary>Planner lead: prep plus the engine-start window, whichever is longer.</summary>
        public static long LeadSeconds(AircraftType type) =>
            Math.Max(EngineStartSequence.MinimumDepartureLeadSeconds, TotalSeconds(type));

        public static long LeadSeconds(AircraftType type, PlayerBaseLevel baseLevel) =>
            Math.Max(EngineStartSequence.MinimumDepartureLeadSeconds, TotalSeconds(type, baseLevel));

        public static bool IsReady(FleetAircraft aircraft, SimulationTime now) =>
            IsReady(aircraft, now, PlayerBaseLevel.Starter);

        public static bool IsReady(FleetAircraft aircraft, SimulationTime now, PlayerBaseLevel baseLevel)
        {
            if (aircraft == null || !aircraft.Airline.IsPlayer || !aircraft.Scheduled.HasValue)
                return true;
            return For(aircraft, now, baseLevel).Ready;
        }

        public static int Percent(double progress01) =>
            (int)Math.Round(Math.Max(0, Math.Min(1, progress01)) * 100);

        public static DeparturePrepStatus For(FleetAircraft aircraft, SimulationTime now) =>
            For(aircraft, now, PlayerBaseLevel.Starter);

        public static DeparturePrepStatus For(FleetAircraft aircraft, SimulationTime now, PlayerBaseLevel baseLevel)
        {
            if (aircraft == null || !aircraft.Scheduled.HasValue)
                return new DeparturePrepStatus(DeparturePrepStage.Idle, 0, true, "No departure planned",
                    0, 0, 0, 0, 0);
            if (aircraft.Scheduled.Value.Cancelled)
                return new DeparturePrepStatus(DeparturePrepStage.Idle, 0, true, "Cancelled",
                    0, 0, 0, 0, 0);
            if (!aircraft.Airline.IsPlayer)
                return new DeparturePrepStatus(DeparturePrepStage.Ready, 1, true, "Ready",
                    1, 1, 1, 1, 0);

            var start = StartSeconds(aircraft, now, baseLevel);
            var elapsed = now.ElapsedSeconds - start;
            if (elapsed < 0)
                elapsed = 0;

            var fuel = Scale(aircraft.Type, FuelSeconds, baseLevel);
            var catering = Scale(aircraft.Type, CateringSeconds, baseLevel);
            var baggage = Scale(aircraft.Type, BaggageSeconds, baseLevel);
            var boarding = Scale(aircraft.Type, BoardingSeconds, baseLevel);

            var fuelProgress = Progress(elapsed, fuel);
            var afterFuel = elapsed - fuel;
            var cateringProgress = afterFuel <= 0 ? 0 : Progress(afterFuel, catering);
            var afterCatering = afterFuel - catering;
            var baggageProgress = afterCatering <= 0 ? 0 : Progress(afterCatering, baggage);
            var afterBaggage = afterCatering - baggage;
            var boardingProgress = afterBaggage <= 0 ? 0 : Progress(afterBaggage, boarding);

            if (elapsed < fuel)
                return new DeparturePrepStatus(DeparturePrepStage.Fuel, fuelProgress, false,
                    StageLabel("Fuelling", fuelProgress),
                    fuelProgress, 0, 0, 0, Remaining(elapsed, fuel));

            if (afterFuel < catering)
                return new DeparturePrepStatus(DeparturePrepStage.Catering, cateringProgress, false,
                    StageLabel("Catering", cateringProgress),
                    1, cateringProgress, 0, 0, Remaining(afterFuel, catering));

            if (afterCatering < baggage)
                return new DeparturePrepStatus(DeparturePrepStage.Baggage, baggageProgress, false,
                    StageLabel("Baggage", baggageProgress),
                    1, 1, baggageProgress, 0, Remaining(afterCatering, baggage));

            if (afterBaggage < boarding)
                return new DeparturePrepStatus(DeparturePrepStage.Boarding, boardingProgress, false,
                    StageLabel("Boarding", boardingProgress),
                    1, 1, 1, boardingProgress, Remaining(afterBaggage, boarding));

            return new DeparturePrepStatus(DeparturePrepStage.Ready, 1, true, "Ready for pushback",
                1, 1, 1, 1, 0);
        }

        /// <summary>
        /// When prep-start was not saved, infer it from the booked pushback so fuelling
        /// cannot sit at 0% forever (elapsed would otherwise be <c>now - now</c> every call).
        /// </summary>
        /// <summary>
        /// Seconds until <paramref name="stage"/> begins; negative once it has ended, and 0
        /// while it is running. Ground vehicles use this to set off before their stage starts
        /// and to drive home after it finishes (<see cref="GroundServiceRun"/>).
        /// </summary>
        public static double SecondsUntilStage(FleetAircraft aircraft, SimulationTime now,
            PlayerBaseLevel baseLevel, DeparturePrepStage stage)
        {
            if (aircraft == null || !aircraft.Scheduled.HasValue || aircraft.Scheduled.Value.Cancelled)
                return double.MaxValue;

            var elapsed = (double)(now.ElapsedSeconds - StartSeconds(aircraft, now, baseLevel));
            var fuel = Scale(aircraft.Type, FuelSeconds, baseLevel);
            var catering = Scale(aircraft.Type, CateringSeconds, baseLevel);
            var baggage = Scale(aircraft.Type, BaggageSeconds, baseLevel);

            double startsAt, endsAt;
            switch (stage)
            {
                case DeparturePrepStage.Fuel:
                    startsAt = 0;
                    endsAt = fuel;
                    break;
                case DeparturePrepStage.Catering:
                    startsAt = fuel;
                    endsAt = fuel + catering;
                    break;
                case DeparturePrepStage.Baggage:
                    startsAt = fuel + catering;
                    endsAt = fuel + catering + baggage;
                    break;
                default:
                    return double.MaxValue;
            }

            if (elapsed < startsAt)
                return startsAt - elapsed;
            return elapsed <= endsAt ? 0.0 : endsAt - elapsed;
        }

        private static long StartSeconds(FleetAircraft aircraft, SimulationTime now, PlayerBaseLevel baseLevel)
        {
            if (aircraft.PrepStartedAt.HasValue)
                return aircraft.PrepStartedAt.Value.ElapsedSeconds;
            var total = TotalSeconds(aircraft.Type, baseLevel);
            var inferred = aircraft.Scheduled.Value.DepartAt.ElapsedSeconds - total;
            return inferred < 0 ? 0 : inferred;
        }

        private static string StageLabel(string name, double progress) =>
            $"{name} {Percent(progress)}%";

        private static long Remaining(double elapsed, long duration)
        {
            var left = duration - elapsed;
            if (left <= 0)
                return 0;
            return (long)Math.Ceiling(left);
        }

        /// <summary>How long the Boarding stage takes for this type at this base level.</summary>
        public static long BoardingSecondsFor(AircraftType type, PlayerBaseLevel baseLevel) =>
            Scale(type, BoardingSeconds, baseLevel);

        private static long Scale(AircraftType type, long seconds, PlayerBaseLevel baseLevel)
        {
            var typeScale = type != null && AircraftCatalogue.TryFor(type, out var spec)
                            && spec.StandClass == StandClass.TerminalGate ? 1.5 : 1.0;
            var baseScale = baseLevel switch
            {
                PlayerBaseLevel.ExpandedRegional => 0.90,
                PlayerBaseLevel.JetGate => 0.80,
                PlayerBaseLevel.International => 0.70,
                _ => 1.0
            };
            return (long)Math.Round(seconds * typeScale * baseScale);
        }

        private static double Progress(double elapsed, long duration) =>
            duration <= 0 ? 1 : Math.Max(0, Math.Min(1, elapsed / duration));
    }
}
