using System;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>Fuel → catering → boarding before a player pushback (ADR 0056). No vehicles.</summary>
    public enum DeparturePrepStage
    {
        Idle,
        Fuel,
        Catering,
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
            double boardingProgress,
            long remainingSeconds)
        {
            Stage = stage;
            StageProgress = stageProgress;
            Ready = ready;
            Label = label;
            FuelProgress = fuelProgress;
            CateringProgress = cateringProgress;
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

        /// <summary>0..1 through boarding.</summary>
        public double BoardingProgress { get; }

        /// <summary>Seconds left in the current stage, or 0 when idle/ready.</summary>
        public long RemainingSeconds { get; }

        public int StagePercent => DeparturePrep.Percent(StageProgress);
        public int FuelPercent => DeparturePrep.Percent(FuelProgress);
        public int CateringPercent => DeparturePrep.Percent(CateringProgress);
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
        public const long BoardingSeconds = 120;

        public static long TotalSeconds(AircraftType type) =>
            Scale(type, FuelSeconds) + Scale(type, CateringSeconds) + Scale(type, BoardingSeconds);

        /// <summary>Planner lead: prep plus the engine-start window, whichever is longer.</summary>
        public static long LeadSeconds(AircraftType type) =>
            Math.Max(EngineStartSequence.MinimumDepartureLeadSeconds, TotalSeconds(type));

        public static bool IsReady(FleetAircraft aircraft, SimulationTime now)
        {
            if (aircraft == null || !aircraft.Airline.IsPlayer || !aircraft.Scheduled.HasValue)
                return true;
            return For(aircraft, now).Ready;
        }

        public static int Percent(double progress01) =>
            (int)Math.Round(Math.Max(0, Math.Min(1, progress01)) * 100);

        public static DeparturePrepStatus For(FleetAircraft aircraft, SimulationTime now)
        {
            if (aircraft == null || !aircraft.Scheduled.HasValue)
                return new DeparturePrepStatus(DeparturePrepStage.Idle, 0, true, "No departure planned",
                    0, 0, 0, 0);
            if (!aircraft.Airline.IsPlayer)
                return new DeparturePrepStatus(DeparturePrepStage.Ready, 1, true, "Ready",
                    1, 1, 1, 0);

            var start = aircraft.PrepStartedAt?.ElapsedSeconds ?? now.ElapsedSeconds;
            var elapsed = now.ElapsedSeconds - start;
            if (elapsed < 0)
                elapsed = 0;

            var fuel = Scale(aircraft.Type, FuelSeconds);
            var catering = Scale(aircraft.Type, CateringSeconds);
            var boarding = Scale(aircraft.Type, BoardingSeconds);

            var fuelProgress = Progress(elapsed, fuel);
            var afterFuel = elapsed - fuel;
            var cateringProgress = afterFuel <= 0 ? 0 : Progress(afterFuel, catering);
            var afterCatering = afterFuel - catering;
            var boardingProgress = afterCatering <= 0 ? 0 : Progress(afterCatering, boarding);

            if (elapsed < fuel)
                return new DeparturePrepStatus(DeparturePrepStage.Fuel, fuelProgress, false,
                    StageLabel("Fuelling", fuelProgress),
                    fuelProgress, 0, 0, Remaining(elapsed, fuel));

            if (afterFuel < catering)
                return new DeparturePrepStatus(DeparturePrepStage.Catering, cateringProgress, false,
                    StageLabel("Catering", cateringProgress),
                    1, cateringProgress, 0, Remaining(afterFuel, catering));

            if (afterCatering < boarding)
                return new DeparturePrepStatus(DeparturePrepStage.Boarding, boardingProgress, false,
                    StageLabel("Boarding", boardingProgress),
                    1, 1, boardingProgress, Remaining(afterCatering, boarding));

            return new DeparturePrepStatus(DeparturePrepStage.Ready, 1, true, "Ready for pushback",
                1, 1, 1, 0);
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

        private static long Scale(AircraftType type, long seconds)
        {
            if (type != null && AircraftCatalogue.TryFor(type, out var spec)
                && spec.StandClass == StandClass.TerminalGate)
                return (long)Math.Round(seconds * 1.5);
            return seconds;
        }

        private static double Progress(double elapsed, long duration) =>
            duration <= 0 ? 1 : Math.Max(0, Math.Min(1, elapsed / duration));
    }
}
