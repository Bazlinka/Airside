using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    public enum TurnaroundTaskState
    {
        Waiting,
        Active,
        Complete
    }

    public readonly struct TurnaroundTaskView
    {
        public TurnaroundTaskView(string name, TurnaroundTaskState state, long secondsRemaining, float progress01 = 0f)
        {
            Name = name;
            State = state;
            SecondsRemaining = secondsRemaining;
            Progress01 = progress01 < 0f ? 0f : progress01 > 1f ? 1f : progress01;
        }

        public string Name { get; }
        public TurnaroundTaskState State { get; }
        public long SecondsRemaining { get; }
        /// <summary>0 waiting, 0–1 active, 1 complete. Presentation HUD bars only.</summary>
        public float Progress01 { get; }
    }

    public sealed class TurnaroundWorkflow
    {
        public const long ScheduledWindowSeconds = 45;

        private readonly SimulationTime _startedAt;
        private readonly int _cleaningDisruptionSeconds;
        private readonly double _staffingFactor;

        public TurnaroundWorkflow(SimulationTime startedAt, bool cleaningDisruption, double staffingFactor = 1.0)
        {
            _startedAt = startedAt;
            _cleaningDisruptionSeconds = cleaningDisruption ? 10 : 0;
            _staffingFactor = staffingFactor > 0 ? staffingFactor : 1.0;
        }

        public bool PriorityCrewEnabled { get; private set; }
        public bool HasCleaningDisruption => _cleaningDisruptionSeconds > 0;

        /// <summary>True when this turnaround is being run by fewer than the baseline crew.</summary>
        public bool IsUnderstaffed => _staffingFactor > 1.0;

        /// <summary>
        /// Why this turnaround runs past its scheduled window, or empty when it does
        /// not. Every delay the airport reports has to name a cause the player can act
        /// on, so this covers understaffing as well as cabin-cleaning disruptions.
        /// </summary>
        public string OverrunCause
        {
            get
            {
                if (CompletionOffset() <= ScheduledWindowSeconds)
                    return string.Empty;
                if (HasCleaningDisruption)
                    return "Cabin cleaning disruption";
                if (IsUnderstaffed)
                    return "Understaffed ground crew";
                return "Extended turnaround";
            }
        }

        public void EnablePriorityCrew()
        {
            PriorityCrewEnabled = true;
        }

        public bool IsComplete(SimulationTime now) => Elapsed(now) >= CompletionOffset();

        public long DelaySeconds(SimulationTime now) => Math.Max(0, Elapsed(now) - ScheduledWindowSeconds);

        public string DelayCause(SimulationTime now)
        {
            if (Elapsed(now) < ScheduledWindowSeconds || IsComplete(now))
                return string.Empty;

            if (HasCleaningDisruption)
                return "Cabin cleaning disruption";
            if (IsUnderstaffed)
                return "Understaffed ground crew";

            foreach (var task in Tasks(now))
            {
                if (task.State != TurnaroundTaskState.Complete)
                    return task.Name;
            }

            return string.Empty;
        }

        public IReadOnlyList<TurnaroundTaskView> Tasks(SimulationTime now)
        {
            var deplaneEnd = Duration(8);
            var unloadEnd = Duration(12);
            var refuelEnd = Duration(18);
            var cleaningEnd = deplaneEnd + Duration(15 + _cleaningDisruptionSeconds);
            var loadEnd = unloadEnd + Duration(16);
            var boardingStart = Math.Max(cleaningEnd, deplaneEnd);
            var boardingEnd = boardingStart + Duration(18);

            return new[]
            {
                View("Passengers off", 0, deplaneEnd, now),
                View("Unload bags", 0, unloadEnd, now),
                View("Refuel", 0, refuelEnd, now),
                View("Clean cabin", deplaneEnd, cleaningEnd, now),
                View("Load bags", unloadEnd, loadEnd, now),
                View("Board passengers", boardingStart, boardingEnd, now)
            };
        }

        private long CompletionOffset()
        {
            var deplaneEnd = Duration(8);
            var unloadEnd = Duration(12);
            var cleaningEnd = deplaneEnd + Duration(15 + _cleaningDisruptionSeconds);
            var loadEnd = unloadEnd + Duration(16);
            var boardingEnd = cleaningEnd + Duration(18);
            return Math.Max(Math.Max(Duration(18), loadEnd), boardingEnd);
        }

        private TurnaroundTaskView View(string name, long start, long end, SimulationTime now)
        {
            var elapsed = Elapsed(now);
            var duration = Math.Max(1L, end - start);
            if (elapsed < start)
                return new TurnaroundTaskView(name, TurnaroundTaskState.Waiting, duration, 0f);
            if (elapsed >= end)
                return new TurnaroundTaskView(name, TurnaroundTaskState.Complete, 0, 1f);
            var remaining = end - elapsed;
            var progress = 1f - (float)remaining / duration;
            return new TurnaroundTaskView(name, TurnaroundTaskState.Active, remaining, progress);
        }

        private long Duration(long normalSeconds)
        {
            var factor = _staffingFactor * (PriorityCrewEnabled ? 0.7 : 1.0);
            if (factor == 1.0)
                return normalSeconds;
            return Math.Max(1, (long)Math.Ceiling(normalSeconds * factor));
        }

        private long Elapsed(SimulationTime now) => Math.Max(0, now.ElapsedSeconds - _startedAt.ElapsedSeconds);
    }
}
