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
                    if (elapsed < AirlineOperations.LineupSeconds)
                        return Ground(AircraftPhase.TaxiOut, start, FleetGroundLeg.Lineup, start, AirlineOperations.LineupSeconds);
                    return Air(AircraftPhase.Takeoff, start.Advance(AirlineOperations.LineupSeconds));

                case FleetState.Outbound:
                    return elapsed < CircuitProfile.DepartedSeconds
                        ? Air(AircraftPhase.Departed, start)
                        : Hidden(start);

                case FleetState.Landing:
                {
                    var approach = CircuitProfile.ApproachSeconds;
                    var landing = CircuitProfile.LandingSeconds;
                    if (elapsed < approach)
                        return Air(AircraftPhase.Approach, start);
                    if (elapsed < approach + landing)
                        return Air(AircraftPhase.Landing, start.Advance(approach));
                    var vacateAt = start.Advance(approach + landing);
                    return Ground(AircraftPhase.TaxiIn, vacateAt, FleetGroundLeg.Vacate, vacateAt, AirlineOperations.VacateSeconds);
                }

                case FleetState.AwaitingStand:
                    return Ground(AircraftPhase.TaxiIn, start, FleetGroundLeg.AwaitingStand, start, 0);

                case FleetState.TaxiIn:
                    return Ground(AircraftPhase.TaxiIn, start, FleetGroundLeg.TaxiIn, start, stateSeconds);

                default:
                    // Away, turning around at the destination, or holding in the circuit
                    // for a landing slot: not drawn at the field.
                    return Hidden(start);
            }
        }

        /// <summary>
        /// Place in the queue for aircraft sharing <paramref name="aircraft"/>'s waiting state
        /// (holding short, or waiting for a stand): 0 for whoever got there first. Ties break on
        /// registration so every frame and every load draws the same order.
        /// </summary>
        public static int QueueSlot(IReadOnlyList<FleetAircraft> fleet, FleetAircraft aircraft)
        {
            if (fleet == null || aircraft == null)
                return 0;
            var slot = 0;
            for (var i = 0; i < fleet.Count; i++)
            {
                var other = fleet[i];
                if (ReferenceEquals(other, aircraft) || other.State != aircraft.State)
                    continue;
                var order = other.StateStartedAt.CompareTo(aircraft.StateStartedAt);
                if (order < 0 || order == 0
                    && string.CompareOrdinal(other.Registration, aircraft.Registration) < 0)
                    slot++;
            }

            return slot;
        }

        private static FleetVisual Air(AircraftPhase phase, SimulationTime startedAt) =>
            new(true, phase, startedAt, FleetGroundLeg.None, startedAt, 0);

        private static FleetVisual Ground(AircraftPhase phase, SimulationTime phaseStartedAt,
            FleetGroundLeg leg, SimulationTime legStartedAt, long legSeconds) =>
            new(true, phase, phaseStartedAt, leg, legStartedAt, legSeconds);

        private static FleetVisual Hidden(SimulationTime at) =>
            new(false, AircraftPhase.Departed, at, FleetGroundLeg.None, at, 0);
    }
}
