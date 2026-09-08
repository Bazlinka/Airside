using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// One commercial aircraft loop: approach through departure, stand assignment,
    /// turnaround and settlement. <see cref="AirportSimulation"/> owns the list and
    /// shared reservation / fleet orchestration.
    /// </summary>
    public sealed class CommercialFlight
    {
        public CommercialFlight(string aircraftId, SimulationTime spawnedAt, StableId assignedStand, TaxiRoute taxiRoute)
        {
            if (string.IsNullOrWhiteSpace(aircraftId))
                throw new ArgumentException("Aircraft identifier is required.", nameof(aircraftId));

            AircraftId = aircraftId;
            SpawnedAt = spawnedAt;
            CycleStartedAt = spawnedAt;
            AssignedStand = assignedStand;
            TaxiRoute = taxiRoute ?? throw new ArgumentNullException(nameof(taxiRoute));
            Operation = new AircraftOperation(aircraftId, spawnedAt);
        }

        public string AircraftId { get; }
        public SimulationTime SpawnedAt { get; }
        public SimulationTime CycleStartedAt { get; set; }
        public AircraftOperation Operation { get; set; }
        public StableId AssignedStand { get; set; }
        public TaxiRoute TaxiRoute { get; set; }
        public TurnaroundWorkflow Turnaround { get; set; }
        public bool FlightSettled { get; set; }
        public long LastDelaySeconds { get; set; }
        public string LastDelayCause { get; set; } = string.Empty;

        public StableId OwnerId => new(AircraftId);

        public StableId SegmentFor(SimulationTime at)
        {
            var phase = Operation.Phase;
            if (phase != AircraftPhase.TaxiIn && phase != AircraftPhase.TaxiOut)
                return default;

            // Holding short after taxi-out: no longer occupying a taxi segment.
            if (phase == AircraftPhase.TaxiOut && Operation.SecondsRemaining(at) <= 0)
                return default;

            var progress = Operation.PhaseProgress(at);
            var count = TaxiRoute.SegmentIds.Count;
            var index = Math.Min(count - 1, (int)(Math.Max(0, Math.Min(0.999999, progress)) * count));
            if (phase == AircraftPhase.TaxiOut)
                index = count - 1 - index;
            return TaxiRoute.SegmentIds[index];
        }

        public IEnumerable<StableId> RequiredResources(SimulationTime at) =>
            ResourcesForPhase(Operation.Phase, at);

        /// <summary>
        /// Resources a commercial must hold while in <paramref name="phase"/>. Used both for
        /// the live phase and to gate the next transition so taxiways/runways are reserved
        /// before use.
        /// </summary>
        public IEnumerable<StableId> ResourcesForPhase(AircraftPhase phase, SimulationTime at)
        {
            switch (phase)
            {
                case AircraftPhase.Landing:
                case AircraftPhase.Takeoff:
                    yield return AirportSimulation.Runway;
                    break;
                case AircraftPhase.TaxiIn:
                    // Single-file A1/A2 corridor — dual commercials must not meet head-on.
                    yield return AirportTaxiNetwork.Corridor;
                    yield return SegmentForPhase(AircraftPhase.TaxiIn, at);
                    yield return AssignedStand;
                    break;
                case AircraftPhase.AtStand:
                    yield return AssignedStand;
                    break;
                case AircraftPhase.Pushback:
                    yield return AssignedStand;
                    yield return AirportSimulation.ApronLane;
                    break;
                case AircraftPhase.TaxiOut:
                    // Finished taxi, holding short: release the corridor so a landing
                    // aircraft can vacate the runway without deadlocking against takeoff.
                    if (Operation.Phase == AircraftPhase.TaxiOut
                        && Operation.SecondsRemaining(at) <= 0)
                        yield break;
                    yield return AirportTaxiNetwork.Corridor;
                    yield return SegmentForPhase(AircraftPhase.TaxiOut, at);
                    break;
            }
        }

        private StableId SegmentForPhase(AircraftPhase phase, SimulationTime at)
        {
            if (phase != AircraftPhase.TaxiIn && phase != AircraftPhase.TaxiOut)
                return default;

            // When asking about a future taxi phase before we have entered it, use the
            // entry end of the route (first segment inbound / last outbound).
            if (Operation.Phase != phase)
            {
                var count = TaxiRoute.SegmentIds.Count;
                return phase == AircraftPhase.TaxiOut
                    ? TaxiRoute.SegmentIds[count - 1]
                    : TaxiRoute.SegmentIds[0];
            }

            return SegmentFor(at);
        }
    }
}
