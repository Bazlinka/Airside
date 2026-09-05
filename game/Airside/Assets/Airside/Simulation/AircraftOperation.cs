using System;
using Airside.Domain;

namespace Airside.Simulation
{
    public enum AircraftPhase
    {
        Approach,
        Landing,
        TaxiIn,
        AtStand,
        Pushback,
        TaxiOut,
        Takeoff,
        Departed
    }

    public sealed class AircraftOperation
    {
        private static readonly long[] PhaseDurationsSeconds =
        {
            20,
            12,
            25,
            45,
            12,
            25,
            15,
            long.MaxValue
        };

        public AircraftOperation(string aircraftId, SimulationTime startedAt)
        {
            if (string.IsNullOrWhiteSpace(aircraftId))
                throw new ArgumentException("Aircraft identifier is required.", nameof(aircraftId));

            AircraftId = aircraftId;
            Phase = AircraftPhase.Approach;
            PhaseStartedAt = startedAt;
        }

        public string AircraftId { get; }
        public AircraftPhase Phase { get; private set; }
        public SimulationTime PhaseStartedAt { get; private set; }
        public bool IsComplete => Phase == AircraftPhase.Departed;

        public bool AdvanceTo(SimulationTime now)
        {
            if (now.CompareTo(PhaseStartedAt) < 0)
                throw new ArgumentOutOfRangeException(nameof(now), "Simulation time cannot move backwards.");

            var changed = false;
            while (!IsComplete)
            {
                var duration = PhaseDurationsSeconds[(int)Phase];
                var nextTransition = PhaseStartedAt.Advance(duration);
                if (now.CompareTo(nextTransition) < 0)
                    break;

                Phase = (AircraftPhase)((int)Phase + 1);
                PhaseStartedAt = nextTransition;
                changed = true;
            }

            return changed;
        }
    }
}
