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

            var progress = Operation.PhaseProgress(at);
            var count = TaxiRoute.SegmentIds.Count;
            var index = Math.Min(count - 1, (int)(Math.Max(0, Math.Min(0.999999, progress)) * count));
            if (phase == AircraftPhase.TaxiOut)
                index = count - 1 - index;
            return TaxiRoute.SegmentIds[index];
        }

        public IEnumerable<StableId> RequiredResources(SimulationTime at)
        {
            switch (Operation.Phase)
            {
                case AircraftPhase.Landing:
                case AircraftPhase.Takeoff:
                    yield return AirportSimulation.Runway;
                    break;
                case AircraftPhase.TaxiIn:
                    yield return SegmentFor(at);
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
                    yield return SegmentFor(at);
                    break;
            }
        }
    }
}
