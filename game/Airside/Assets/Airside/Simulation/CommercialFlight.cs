using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// One commercial aircraft loop: approach through departure, stand assignment,
    /// turnaround and settlement. <see cref="AirportSimulation"/> owns the list and
    /// shared reservation / traffic orchestration.
    /// </summary>
    public sealed class CommercialFlight
    {
        public CommercialFlight(string aircraftId, SimulationTime spawnedAt, StableId assignedStand, StandTaxiRoutes routes)
        {
            if (string.IsNullOrWhiteSpace(aircraftId))
                throw new ArgumentException("Aircraft identifier is required.", nameof(aircraftId));

            AircraftId = aircraftId;
            SpawnedAt = spawnedAt;
            CycleStartedAt = spawnedAt;
            AssignedStand = assignedStand;
            if (routes.Arrival == null)
                throw new ArgumentNullException(nameof(routes));
            if (routes.Departure == null)
                throw new ArgumentNullException(nameof(routes));
            ArrivalRoute = routes.Arrival;
            DepartureRoute = routes.Departure;
            TaxiRoute = ArrivalRoute;
            Operation = new AircraftOperation(aircraftId, spawnedAt);
        }

        public string AircraftId { get; }
        public SimulationTime SpawnedAt { get; }
        public SimulationTime CycleStartedAt { get; set; }
        public AircraftOperation Operation { get; set; }
        public StableId AssignedStand { get; set; }
        public TaxiRoute ArrivalRoute { get; }
        public TaxiRoute DepartureRoute { get; }
        /// <summary>Arrival path — kept for callers that only need taxi-in geometry.</summary>
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
            var reverse = phase == AircraftPhase.TaxiOut;
            var route = RouteFor(Operation.Phase);
            return route.SegmentIds[route.SegmentIndexAt(progress, reverse)];
        }

        public TaxiRoute RouteFor(AircraftPhase phase) =>
            phase == AircraftPhase.TaxiOut ? DepartureRoute : ArrivalRoute;

        public IEnumerable<StableId> RequiredResources(SimulationTime at) =>
            ResourcesForPhase(Operation.Phase, at);

        /// <summary>
        /// Resources a commercial must hold while in <paramref name="phase"/>. Used both for
        /// the live phase and to gate the next transition so taxiways/runways are reserved
        /// before use.
        /// </summary>
        public IEnumerable<StableId> ResourcesForPhase(AircraftPhase phase, SimulationTime at)
        {
            if (AirportCircuit.IsSkippedGroundPhase(phase))
            {
                yield return AirportSimulation.Runway;
                yield break;
            }

            switch (phase)
            {
                case AircraftPhase.Approach:
                case AircraftPhase.Landing:
                    // Claim the assigned stand early so GT cannot park on an inbound stand
                    // and so dual commercials never double-book before taxi-in.
                    if (phase == AircraftPhase.Landing)
                        yield return AirportSimulation.Runway;
                    yield return AssignedStand;
                    break;
                case AircraftPhase.Takeoff:
                    yield return AirportSimulation.Runway;
                    break;
                case AircraftPhase.TaxiIn:
                    // An arrival still inside the runway strip has not vacated. Releasing
                    // the runway the instant the rollout ended let a departure be cleared
                    // and start its roll while the arrival was still on the centreline.
                    if (!HasVacatedRunway(at))
                        yield return AirportSimulation.Runway;
                    // Single-file A1/A2 corridor — dual commercials must not meet head-on.
                    yield return AirportTaxiNetwork.Corridor;
                    yield return SegmentForPhase(AircraftPhase.TaxiIn, at);
                    if (OnApronThroat(AircraftPhase.TaxiIn, at))
                        yield return AirportTaxiNetwork.ApronThroat;
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
                    if (OnApronThroat(AircraftPhase.TaxiOut, at))
                        yield return AirportTaxiNetwork.ApronThroat;
                    // Keep the stand reserved until the lead-in / throat is clear so GT
                    // cannot inbound while this airframe is still on the bay chord.
                    if (StillOccupyingStandOnTaxiOut(at))
                        yield return AssignedStand;
                    break;
            }
        }

        /// <summary>
        /// True once an inbound aircraft is past the A1 holding position. Only meaningful
        /// while taxiing in; every other phase is either on the runway by right or well
        /// clear of it.
        /// </summary>
        public bool HasVacatedRunway(SimulationTime at)
        {
            if (Operation.Phase != AircraftPhase.TaxiIn)
                return true;

            return Operation.PhaseProgress(at) >= AirportTaxiNetwork.RunwayHoldingProgress(ArrivalRoute);
        }

        private bool StillOccupyingStandOnTaxiOut(SimulationTime at)
        {
            if (Operation.Phase != AircraftPhase.TaxiOut)
                return true;

            var segment = SegmentFor(at);
            return segment.Equals(AirportTaxiNetwork.LeadInFor(AssignedStand))
                || segment.Equals(AirportTaxiNetwork.ApronThroat)
                || segment.Equals(default(StableId));
        }

        private bool OnApronThroat(AircraftPhase phase, SimulationTime at)
        {
            var route = RouteFor(phase);
            var segment = Operation.Phase == phase
                ? SegmentFor(at)
                : (phase == AircraftPhase.TaxiOut
                    ? route.SegmentIds[route.SegmentIds.Count - 1]
                    : route.SegmentIds[0]);
            return segment.Equals(AirportTaxiNetwork.ApronThroat)
                || segment.Equals(AirportTaxiNetwork.LeadInFor(AssignedStand));
        }

        private StableId SegmentForPhase(AircraftPhase phase, SimulationTime at)
        {
            if (phase != AircraftPhase.TaxiIn && phase != AircraftPhase.TaxiOut)
                return default;

            // When asking about a future taxi phase before we have entered it, use the
            // entry end of the route (first segment inbound / last outbound).
            if (Operation.Phase != phase)
            {
                var route = RouteFor(phase);
                var count = route.SegmentIds.Count;
                return phase == AircraftPhase.TaxiOut
                    ? route.SegmentIds[count - 1]
                    : route.SegmentIds[0];
            }

            return SegmentFor(at);
        }
    }
}
