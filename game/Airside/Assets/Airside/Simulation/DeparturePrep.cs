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
        public DeparturePrepStatus(DeparturePrepStage stage, double stageProgress, bool ready, string label)
        {
            Stage = stage;
            StageProgress = stageProgress;
            Ready = ready;
            Label = label;
        }

        public DeparturePrepStage Stage { get; }
        public double StageProgress { get; }
        public bool Ready { get; }
        public string Label { get; }
    }

    /// <summary>
    /// Pure function of type, prep-start time and now. Simulation will not release pushback
    /// until <see cref="IsReady"/>; presentation only draws the stage.
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

        public static DeparturePrepStatus For(FleetAircraft aircraft, SimulationTime now)
        {
            if (aircraft == null || !aircraft.Scheduled.HasValue)
                return new DeparturePrepStatus(DeparturePrepStage.Idle, 0, true, "No departure planned");
            if (!aircraft.Airline.IsPlayer)
                return new DeparturePrepStatus(DeparturePrepStage.Ready, 1, true, "Ready");

            var start = aircraft.PrepStartedAt?.ElapsedSeconds ?? now.ElapsedSeconds;
            var elapsed = now.ElapsedSeconds - start;
            if (elapsed < 0)
                elapsed = 0;

            var fuel = Scale(aircraft.Type, FuelSeconds);
            var catering = Scale(aircraft.Type, CateringSeconds);
            var boarding = Scale(aircraft.Type, BoardingSeconds);

            if (elapsed < fuel)
                return new DeparturePrepStatus(DeparturePrepStage.Fuel, Progress(elapsed, fuel), false, "Fuelling");
            elapsed -= fuel;
            if (elapsed < catering)
                return new DeparturePrepStatus(DeparturePrepStage.Catering, Progress(elapsed, catering), false, "Catering");
            elapsed -= catering;
            if (elapsed < boarding)
                return new DeparturePrepStatus(DeparturePrepStage.Boarding, Progress(elapsed, boarding), false, "Boarding");

            return new DeparturePrepStatus(DeparturePrepStage.Ready, 1, true, "Ready for pushback");
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
