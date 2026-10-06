using System;
using Airside.Domain;

namespace Airside.Simulation
{
    public enum WakeCategory { Light, Medium, Heavy, Super }

    /// <summary>Australian MOS 172 §10.12 time minima, in seconds. Independent of runway occupancy.</summary>
    public static class WakeSeparation
    {
        // Classification is MTOW, never wingspan. The current fleet's published weight bands
        // are recorded in docs/data/ADELAIDE_GROUND_PROTOCOLS.md. Unknown types use a conservative heavy band.
        public static WakeCategory Category(AircraftType type) =>
            AircraftCatalogue.TryFor(type, out var spec) ? spec.WeightBand switch
            {
                AircraftWeightBand.Light => WakeCategory.Light,
                AircraftWeightBand.MediumBelow25Tonnes or AircraftWeightBand.Medium => WakeCategory.Medium,
                AircraftWeightBand.Super => WakeCategory.Super,
                _ => WakeCategory.Heavy
            } : WakeCategory.Heavy;

        public static bool MediumAtLeast25Tonnes(AircraftType type) =>
            AircraftCatalogue.TryFor(type, out var spec) && spec.WeightBand == AircraftWeightBand.Medium;

        public static long Seconds(AircraftType leader, AircraftType follower, bool landing,
            bool intermediateDeparture = false)
        {
            var lead = Category(leader);
            var follow = Category(follower);
            if (intermediateDeparture && !landing)
                return lead == WakeCategory.Super ? 240
                    : lead == WakeCategory.Heavy && follow is WakeCategory.Medium or WakeCategory.Light ? 180
                    : MediumAtLeast25Tonnes(leader) && follow == WakeCategory.Light ? 180 : 0;
            if (lead == WakeCategory.Super)
                return follow switch { WakeCategory.Heavy => landing ? 180 : 120,
                    WakeCategory.Medium => 180, WakeCategory.Light => landing ? 240 : 180, _ => 0 };
            if (lead == WakeCategory.Heavy)
                return follow switch { WakeCategory.Medium => 120, WakeCategory.Light => landing ? 180 : 120, _ => 0 };
            return lead == WakeCategory.Medium && (MediumAtLeast25Tonnes(leader) || leader?.IsRotorcraft == true)
                   && follow == WakeCategory.Light ? landing ? 180 : 120 : 0;
        }
    }

    /// <summary>Persistent preceding movement, anchored at airborne time or touchdown.</summary>
    [Serializable]
    public sealed class RunwayWakeRecord
    {
        public string TypeId;
        public bool Departure;
        public long EventAtSeconds;
    }

    public sealed partial class AirlineOperations
    {
        internal RunwayWakeRecord MainWake { get; private set; }
        internal RunwayWakeRecord CrossWake { get; private set; }

        internal void RestoreWake(RunwayWakeRecord main, RunwayWakeRecord cross)
        {
            Validate(main);
            Validate(cross);
            MainWake = main;
            CrossWake = cross;
            static void Validate(RunwayWakeRecord record)
            {
                if (record != null && (!AircraftType.TryFromId(record.TypeId, out _) || record.EventAtSeconds < 0))
                    throw new FormatException("Invalid saved runway wake movement.");
            }
        }

        private static long MovementEventOffset(FleetAircraft aircraft, bool landing) => landing
            ? ApproachHold.RemainingFinalSeconds(AircraftPerformance.For(aircraft.Type).ApproachSeconds, aircraft.Registration)
              + (long)Math.Round(AircraftPerformance.For(aircraft.Type).FinalGlideExactSeconds
                                 + AircraftPerformance.For(aircraft.Type).FlareExactSeconds)
            : AdelaideGround.LineupFor(aircraft.AssignedRunway, aircraft.Type).WholeSeconds
              + (long)Math.Round(AircraftPerformance.For(aircraft.Type).TakeoffRollExactSeconds);

        internal SimulationTime MovementFreeAt(FleetAircraft aircraft, bool landing)
        {
            var main = RunwayWeather.IsMainRunway(aircraft.AssignedRunway);
            var at = main ? _mainRunwayFreeAt.ElapsedSeconds : _crossRunwayFreeAt.ElapsedSeconds;
            Apply(main ? MainWake : CrossWake, sameStrip: true);
            // Adelaide's two physical runways intersect. Cross-runway flight paths use the same
            // wake minima; conservatively reserve both until a movement has physically cleared.
            Apply(main ? CrossWake : MainWake, sameStrip: false);
            return new SimulationTime(at);
            void Apply(RunwayWakeRecord record, bool sameStrip)
            {
                if (record == null || !AircraftType.TryFromId(record.TypeId, out var leader))
                    return;
                // MOS 172 10.12.3.3(b): landing behind a departure on the same runway is exempt.
                var seconds = landing && record.Departure && sameStrip ? 0
                    : WakeSeparation.Seconds(leader, aircraft.Type, landing);
                if (seconds > 0)
                    at = Math.Max(at, record.EventAtSeconds + seconds - MovementEventOffset(aircraft, landing));
            }
        }

        private void RecordWake(FleetAircraft aircraft, bool landing, SimulationTime now)
        {
            var record = new RunwayWakeRecord { TypeId = aircraft.Type.Id, Departure = !landing,
                EventAtSeconds = now.ElapsedSeconds + MovementEventOffset(aircraft, landing) };
            if (RunwayWeather.IsMainRunway(aircraft.AssignedRunway)) MainWake = record;
            else CrossWake = record;
        }

        internal static SimulationTime? IntersectionBusyUntil(FleetAircraft aircraft) =>
            aircraft.State == FleetState.TakingOff ? aircraft.StateEndsAt : StripBusyUntil(aircraft);

        internal FleetAircraft IntersectingRunwayOccupier(bool mainStrip, SimulationTime now)
        {
            foreach (var aircraft in _fleet)
                if (!aircraft.Type.IsRotorcraft && aircraft.State is FleetState.TakingOff or FleetState.Landing
                    && RunwayWeather.IsMainRunway(aircraft.AssignedRunway) != mainStrip
                    && (IntersectionBusyUntil(aircraft)?.CompareTo(now) ?? -1) > 0)
                    return aircraft;
            return null;
        }
    }
}
