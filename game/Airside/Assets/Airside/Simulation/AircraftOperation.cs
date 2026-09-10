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
        private Func<SimulationTime, double> _atStandProgress;

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
        public long PhaseDurationSeconds => AirportCircuit.DurationSeconds(Phase);

        /// <summary>
        /// When set, <see cref="PhaseProgress"/> for <see cref="AircraftPhase.AtStand"/>
        /// is driven by turnaround progress instead of the fixed 45s phase window.
        /// </summary>
        public void BindAtStandProgress(Func<SimulationTime, double> progress) =>
            _atStandProgress = progress;

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

            if (Phase == AircraftPhase.AtStand && _atStandProgress != null)
                return Math.Max(0, Math.Min(1, _atStandProgress(now)));

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

        /// <summary>
        /// Restart the approach phase clock (go-around / missed approach). Does not
        /// change the phase enum — presentation and saves stay compatible.
        /// </summary>
        public void RestartApproach(SimulationTime now)
        {
            if (Phase != AircraftPhase.Approach)
                throw new InvalidOperationException("Only an approach can be restarted as a go-around.");
            PhaseStartedAt = now;
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
                var duration = AirportCircuit.DurationSeconds(Phase);
                var nextTransition = PhaseStartedAt.Advance(duration);
                var durationElapsed = now.CompareTo(nextTransition) >= 0;

                if (!durationElapsed)
                {
                    // Extra crew can finish turnaround before the scheduled 45s window.
                    // Only the gated AdvanceTo path (AirportSimulation) may leave early —
                    // ungated AdvanceTo must still wait out the full AtStand duration.
                    if (preserveSchedule || Phase != AircraftPhase.AtStand || !canLeavePhase(Phase))
                        break;

                    Phase = (AircraftPhase)((int)Phase + 1);
                    PhaseStartedAt = now;
                    changed = true;
                    continue;
                }

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
