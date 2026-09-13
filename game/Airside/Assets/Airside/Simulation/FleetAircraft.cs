using System;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// Where an airline aircraft is in its round trip from the home airport (ADR 0045).
    /// Order is the order a trip passes through them.
    /// </summary>
    public enum FleetState
    {
        /// <summary>Parked on a stand at home. May have a departure scheduled.</summary>
        AtStand,
        /// <summary>Pushed back and taxiing to the runway.</summary>
        TaxiOut,
        /// <summary>At the holding point, waiting for the tower to release the runway.</summary>
        HoldingShort,
        /// <summary>On the runway for the takeoff roll.</summary>
        TakingOff,
        /// <summary>Off the map, flying to the destination.</summary>
        Outbound,
        /// <summary>On the ground at the destination (turnaround).</summary>
        AtDestination,
        /// <summary>Off the map, flying home.</summary>
        Inbound,
        /// <summary>Back in the home circuit, holding for a landing slot.</summary>
        HoldingForLanding,
        /// <summary>On the runway for the approach and landing roll.</summary>
        Landing,
        /// <summary>Vacated the runway; waiting for a stand to be chosen.</summary>
        AwaitingStand,
        /// <summary>Taxiing to the assigned stand.</summary>
        TaxiIn
    }

    /// <summary>A departure the owner has asked for but that has not started yet.</summary>
    public readonly struct ScheduledDeparture
    {
        public ScheduledDeparture(Destination destination, SimulationTime departAt)
        {
            Destination = destination;
            DepartAt = departAt;
        }

        public Destination Destination { get; }
        public SimulationTime DepartAt { get; }
    }

    /// <summary>
    /// One aircraft owned by an airline. State is changed only by
    /// <see cref="AirlineOperations"/>; everything here is read-only to callers.
    /// </summary>
    public sealed class FleetAircraft
    {
        internal FleetAircraft(string registration, Airline airline, AircraftType type, StableId stand, SimulationTime now)
        {
            if (string.IsNullOrWhiteSpace(registration))
                throw new ArgumentException("A registration is required.", nameof(registration));

            Registration = registration;
            Airline = airline ?? throw new ArgumentNullException(nameof(airline));
            Type = type ?? throw new ArgumentNullException(nameof(type));
            Stand = stand;
            State = FleetState.AtStand;
            StateStartedAt = now;
            StateEndsAt = null;
        }

        public string Registration { get; }
        public Airline Airline { get; }
        public AircraftType Type { get; }
        public StableId Id => new(Registration);

        public FleetState State { get; private set; }
        public SimulationTime StateStartedAt { get; private set; }

        /// <summary>
        /// When the current state ends on its own. Null for states that wait on something
        /// else: a schedule, the tower, or the player's stand choice.
        /// </summary>
        public SimulationTime? StateEndsAt { get; private set; }

        /// <summary>The stand held or being taxied to. Default while away from one.</summary>
        public StableId Stand { get; internal set; }

        /// <summary>The trip in progress, from takeoff until back on a stand.</summary>
        public Destination? CurrentDestination { get; internal set; }

        public ScheduledDeparture? Scheduled { get; internal set; }

        /// <summary>The stand the aircraft last pushed back from, so its taxi-out can be drawn from there.</summary>
        public StableId DepartureStand { get; internal set; }

        public int CompletedTrips { get; internal set; }

        public bool IsOffMap => State is FleetState.Outbound or FleetState.AtDestination or FleetState.Inbound;

        /// <summary>0..1 through a timed state; 0 for waiting states.</summary>
        public double StateProgress(SimulationTime now)
        {
            if (StateEndsAt == null)
                return 0;
            var total = StateEndsAt.Value.ElapsedSeconds - StateStartedAt.ElapsedSeconds;
            if (total <= 0)
                return 1;
            var done = now.ElapsedSeconds - StateStartedAt.ElapsedSeconds;
            return Math.Clamp(done / (double)total, 0, 1);
        }

        /// <summary>Put a restored aircraft back exactly where a save left it.</summary>
        internal void Restore(FleetState state, SimulationTime startedAt, SimulationTime? endsAt)
        {
            State = state;
            StateStartedAt = startedAt;
            StateEndsAt = endsAt;
        }

        internal void Enter(FleetState state, SimulationTime now, long? durationSeconds)
        {
            State = state;
            StateStartedAt = now;
            StateEndsAt = durationSeconds.HasValue ? now.Advance(Math.Max(0, durationSeconds.Value)) : null;
        }

        public override string ToString() => $"{Registration} ({Airline.Name}) {State}";
    }
}
