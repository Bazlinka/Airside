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
        public long PhaseDurationSeconds => PhaseDurationsSeconds[(int)Phase];

        public long SecondsRemaining(SimulationTime now)
        {
            if (IsComplete)
                return 0;

            var elapsed = now.ElapsedSeconds - PhaseStartedAt.ElapsedSeconds;
            return Math.Max(0, PhaseDurationSeconds - elapsed);
        }

        public double PhaseProgress(SimulationTime now)
        {
            if (IsComplete)
                return 1;

            var elapsed = now.ElapsedSeconds - PhaseStartedAt.ElapsedSeconds;
            return Math.Max(0, Math.Min(1, elapsed / (double)PhaseDurationSeconds));
        }

        public bool AdvanceTo(SimulationTime now)
        {
            return AdvanceToInternal(now, _ => true, true);
        }

        public bool AdvanceTo(SimulationTime now, Func<AircraftPhase, bool> canLeavePhase)
        {
            return AdvanceToInternal(now, canLeavePhase, false);
        }

        /// <summary>
        /// Hold phase progress for one simulated second while waiting on a resource.
        /// Pushes <see cref="PhaseStartedAt"/> forward so remaining duration is preserved.
        /// </summary>
        public void StallOneSecond()
        {
            PhaseStartedAt = PhaseStartedAt.Advance(1);
        }

        private bool AdvanceToInternal(SimulationTime now, Func<AircraftPhase, bool> canLeavePhase, bool preserveSchedule)
        {
            if (now.CompareTo(PhaseStartedAt) < 0)
                throw new ArgumentOutOfRangeException(nameof(now), "Simulation time cannot move backwards.");
            if (canLeavePhase == null)
                throw new ArgumentNullException(nameof(canLeavePhase));

            var changed = false;
            while (!IsComplete)
            {
                var duration = PhaseDurationsSeconds[(int)Phase];
                var nextTransition = PhaseStartedAt.Advance(duration);
                if (now.CompareTo(nextTransition) < 0)
                    break;
                if (!canLeavePhase(Phase))
                    break;

                Phase = (AircraftPhase)((int)Phase + 1);
                PhaseStartedAt = preserveSchedule || now.CompareTo(nextTransition) == 0 ? nextTransition : now;
                changed = true;
            }

            return changed;
        }
    }
}
