using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>The stretch of ground movement a fleet aircraft is drawn on, if any.</summary>
    public enum FleetGroundLeg
    {
        /// <summary>Not on the ground at home: airborne path, or not drawn.</summary>
        None,
        Parked,
        /// <summary>Stand to the runway holding point, starting with the pushback.</summary>
        TaxiOut,
        HoldingShort,
        /// <summary>Holding point onto the runway and back to the takeoff position.</summary>
        Lineup,
        /// <summary>Rollout end back to the exit and clear of the runway.</summary>
        Vacate,
        AwaitingStand,
        /// <summary>Runway exit to the assigned stand.</summary>
        TaxiIn
    }

    /// <summary>
    /// How one fleet aircraft appears at the home airport right now, expressed in the
    /// circuit's <see cref="AircraftPhase"/> vocabulary so the existing presentation —
    /// flight paths, props, gear, lights, smoke, audio — draws it unchanged (ADR 0045).
    ///
    /// Airborne phases carry a <see cref="PhaseStartedAt"/> whose duration matches the
    /// circuit's, so the flown curves line up exactly. Ground movement has no circuit
    /// equivalent at Adelaide, so it is described by <see cref="Leg"/> instead.
    /// </summary>
    public readonly struct FleetVisual
    {
        private FleetVisual(bool visible, AircraftPhase phase, SimulationTime phaseStartedAt,
            FleetGroundLeg leg, SimulationTime legStartedAt, long legSeconds)
        {
            Visible = visible;
            Phase = phase;
            PhaseStartedAt = phaseStartedAt;
            Leg = leg;
            LegStartedAt = legStartedAt;
            LegSeconds = legSeconds;
        }

        public bool Visible { get; }
        public AircraftPhase Phase { get; }
        public SimulationTime PhaseStartedAt { get; }
        public FleetGroundLeg Leg { get; }
        public SimulationTime LegStartedAt { get; }
        public long LegSeconds { get; }

        public static FleetVisual For(FleetAircraft aircraft, SimulationTime now)
        {
            var performance = AircraftPerformance.For(aircraft.Type);
            var start = aircraft.StateStartedAt;
            var elapsed = now.ElapsedSeconds - start.ElapsedSeconds;
            // Taxi legs last as long as the state the simulation gave them.
            var stateSeconds = aircraft.StateEndsAt.HasValue ? aircraft.StateEndsAt.Value.ElapsedSeconds - start.ElapsedSeconds : 0;

            switch (aircraft.State)
            {
                case FleetState.AtStand:
                    return Ground(AircraftPhase.AtStand, start, FleetGroundLeg.Parked, start, 0);

                case FleetState.TaxiOut:
                    return Ground(AircraftPhase.TaxiOut, start, FleetGroundLeg.TaxiOut, start, stateSeconds);

                case FleetState.HoldingShort:
                    return Ground(AircraftPhase.TaxiOut, start, FleetGroundLeg.HoldingShort, start, 0);

                case FleetState.TakingOff:
                    var lineupSeconds = AdelaideGround.LineupFor(aircraft.AssignedRunway).WholeSeconds;
                    if (elapsed < lineupSeconds)
                        return Ground(AircraftPhase.TaxiOut, start, FleetGroundLeg.Lineup, start, lineupSeconds);
                    return Air(AircraftPhase.Takeoff, start.Advance(lineupSeconds));

                case FleetState.Outbound:
                    return elapsed < performance.DepartedSeconds
                        ? Air(AircraftPhase.Departed, start)
                        : Hidden(start);

                case FleetState.Landing:
                {
                    var approach = performance.ApproachSeconds;
                    var landing = performance.LandingSeconds;
                    // Missed approach: keep the aircraft on the short final it was already
                    // holding, then hand off to GoAround — do not restart a 4 km inbound.
                    if (aircraft.WentAroundThisTrip && AirlineOperations.IsMissedApproachLanding(aircraft))
                    {
                        var missedRemaining = ApproachHold.RemainingFinalSeconds(approach, aircraft.Registration);
                        var missedBackdate = approach - missedRemaining;
                        var missedStarted = start.ElapsedSeconds >= missedBackdate
                            ? start.Advance(-missedBackdate)
                            : new SimulationTime(0);
                        return Air(AircraftPhase.Approach, missedStarted);
                    }

                    var remaining = ApproachHold.RemainingFinalSeconds(approach, aircraft.Registration);
                    var backdate = approach - remaining;
                    var approachStarted = start.ElapsedSeconds >= backdate
                        ? start.Advance(-backdate)
                        : new SimulationTime(0);
                    if (elapsed < remaining)
                        return Air(AircraftPhase.Approach, approachStarted);
                    if (elapsed < remaining + landing)
                        return Air(AircraftPhase.Landing, start.Advance(remaining));
                    var vacateAt = start.Advance(remaining + landing);
                    return Ground(AircraftPhase.TaxiIn, vacateAt, FleetGroundLeg.Vacate, vacateAt,
                        AdelaideGround.VacateFor(aircraft.Type, aircraft.AssignedRunway).WholeSeconds);
                }

                case FleetState.GoAround:
                    return Air(AircraftPhase.GoAround, start);

                case FleetState.HoldingForLanding:
                    // Short final on the assigned runway (gulf for 05, NE for 23/12, SW for 30).
                    // Progress is pinned by ApproachHold, not this start time.
                    return Air(AircraftPhase.Approach, start);

                case FleetState.AwaitingStand:
                    return Ground(AircraftPhase.TaxiIn, start, FleetGroundLeg.AwaitingStand, start, 0);

                case FleetState.TaxiIn:
                    return Ground(AircraftPhase.TaxiIn, start, FleetGroundLeg.TaxiIn, start, stateSeconds);

                default:
                    // Away or turning around at the destination: not drawn at the field.
                    return Hidden(start);
            }
        }

        /// <summary>
        /// Place in the queue for aircraft sharing <paramref name="aircraft"/>'s waiting state
        /// and assigned runway (holding short, or waiting for a stand): 0 for whoever got
        /// there first. Ties break on registration so every frame and every load draws the
        /// same order. Different strips do not share a queue.
        /// </summary>
        public static int QueueSlot(IReadOnlyList<FleetAircraft> fleet, FleetAircraft aircraft, SimulationTime? now = null)
        {
            if (fleet == null || aircraft == null)
                return 0;
            var slot = 0;
            var shareStrip = aircraft.State != FleetState.AwaitingStand;
            for (var i = 0; i < fleet.Count; i++)
            {
                var other = fleet[i];
                // The aircraft cleared off the front of the queue starts its lineup from the
                // holding point itself; the next one waits a slot back until it has gone, or
                // it was drawn standing on top of it.
                if (aircraft.State == FleetState.HoldingShort && IsLiningUp(other, aircraft.AssignedRunway, now))
                {
                    slot++;
                    continue;
                }

                // Likewise the arrival that has just set off for its stand from the exit.
                if (aircraft.State == FleetState.AwaitingStand && IsLeavingExit(other, aircraft.AssignedRunway, now))
                {
                    slot++;
                    continue;
                }

                if (ReferenceEquals(other, aircraft) || other.State != aircraft.State)
                    continue;
                // Holding short queues per runway end. Waiting for a stand queues per runway exit:
                // 05 and 23 both vacate to E2, while 12 and 30 each join the bay corridor.
                if (shareStrip ? other.AssignedRunway != aircraft.AssignedRunway
                               : !AdelaideGround.SameArrivalExit(other.AssignedRunway, aircraft.AssignedRunway))
                    continue;
                var order = other.StateStartedAt.CompareTo(aircraft.StateStartedAt);
                if (order < 0 || order == 0
                    && string.CompareOrdinal(other.Registration, aircraft.Registration) < 0)
                    slot++;
            }

            return slot;
        }

        /// <summary>
        /// Aircraft ahead of <paramref name="aircraft"/> at its runway's holding point: those holding
        /// short and one still lining up. A taxi-out stops that many queue places back.
        /// </summary>
        public static int QueueAhead(IReadOnlyList<FleetAircraft> fleet, FleetAircraft aircraft, SimulationTime now)
        {
            if (fleet == null || aircraft == null)
                return 0;
            var ahead = 0;
            foreach (var other in fleet)
            {
                if (ReferenceEquals(other, aircraft) || other.AssignedRunway != aircraft.AssignedRunway)
                    continue;
                if (other.State == FleetState.HoldingShort || IsLiningUp(other, aircraft.AssignedRunway, now))
                    ahead++;
            }

            return ahead;
        }

        /// <summary>
        /// Arrivals already waiting for a stand at the exit a <paramref name="landing"/> aircraft will
        /// vacate to. Its vacate stops that many places back, where it will then wait.
        /// </summary>
        public static int ExitQueueAhead(IReadOnlyList<FleetAircraft> fleet, FleetAircraft landing, SimulationTime? now = null)
        {
            if (fleet == null || landing == null)
                return 0;
            var ahead = 0;
            foreach (var other in fleet)
            {
                if (ReferenceEquals(other, landing))
                    continue;
                if (other.State == FleetState.AwaitingStand
                    && AdelaideGround.SameArrivalExit(other.AssignedRunway, landing.AssignedRunway)
                    || IsLeavingExit(other, landing.AssignedRunway, now))
                    ahead++;
            }

            return ahead;
        }

        /// <summary>Seconds a taxi-in takes to move a queue place clear of the runway exit.</summary>
        public const long LeavingExitSeconds = 20;

        /// <summary>Taxiing in from the same exit and not yet a queue place clear of it.</summary>
        private static bool IsLeavingExit(FleetAircraft other, RunwayDirection runway, SimulationTime? now) =>
            now.HasValue && other.State == FleetState.TaxiIn
            && AdelaideGround.SameArrivalExit(other.AssignedRunway, runway)
            && now.Value.ElapsedSeconds - other.StateStartedAt.ElapsedSeconds < LeavingExitSeconds;

        /// <summary>Taking off from <paramref name="runway"/> and still on the lineup leg at <paramref name="now"/>.</summary>
        private static bool IsLiningUp(FleetAircraft other, RunwayDirection runway, SimulationTime? now) =>
            now.HasValue && other.State == FleetState.TakingOff && other.AssignedRunway == runway
            && now.Value.ElapsedSeconds - other.StateStartedAt.ElapsedSeconds
            < AdelaideGround.LineupFor(runway).WholeSeconds;

        private static FleetVisual Air(AircraftPhase phase, SimulationTime startedAt) =>
            new(true, phase, startedAt, FleetGroundLeg.None, startedAt, 0);

        private static FleetVisual Ground(AircraftPhase phase, SimulationTime phaseStartedAt,
            FleetGroundLeg leg, SimulationTime legStartedAt, long legSeconds) =>
            new(true, phase, phaseStartedAt, leg, legStartedAt, legSeconds);

        private static FleetVisual Hidden(SimulationTime at) =>
            new(false, AircraftPhase.Departed, at, FleetGroundLeg.None, at, 0);
    }
}
