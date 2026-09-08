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
        private readonly Func<double> _staffingFactor;
        /// <summary>
        /// When priority crew is hired mid-turnaround, freeze completion so only the
        /// remaining unfinished work is scaled — never instantly complete elapsed work.
        /// </summary>
        private long? _forcedCompletionOffset;

        public TurnaroundWorkflow(SimulationTime startedAt, bool cleaningDisruption, double staffingFactor = 1.0)
            : this(startedAt, cleaningDisruption, () => staffingFactor)
        {
        }

        public TurnaroundWorkflow(SimulationTime startedAt, bool cleaningDisruption, Func<double> staffingFactor)
        {
            _startedAt = startedAt;
            _cleaningDisruptionSeconds = cleaningDisruption ? 10 : 0;
            _staffingFactor = staffingFactor ?? (() => 1.0);
        }

        public bool PriorityCrewEnabled { get; private set; }
        public bool HasCleaningDisruption => _cleaningDisruptionSeconds > 0;

        /// <summary>True when this turnaround is being run by fewer than the baseline crew.</summary>
        public bool IsUnderstaffed => CurrentStaffingFactor() > 1.0;

        /// <summary>
        /// Why this turnaround runs past its scheduled window, or empty when it does
        /// not. Every delay the airport reports has to name a cause the player can act
        /// on, so this covers understaffing as well as cabin-cleaning disruptions.
        /// Understaffing wins when both apply — the player can hire crew.
        /// </summary>
        public string OverrunCause
        {
            get
            {
                if (CompletionOffset() <= ScheduledWindowSeconds)
                    return string.Empty;
                if (IsUnderstaffed)
                    return "Understaffed ground crew";
                if (HasCleaningDisruption)
                    return "Cabin cleaning disruption";
                return "Extended turnaround";
            }
        }

        /// <summary>
        /// Enable priority crew. When hired mid-turnaround, scales only remaining
        /// unfinished work from <paramref name="now"/> so enable cannot instantly
        /// complete already-elapsed tasks. At turnaround start, uses the normal
        /// per-task 0.7 rescale.
        /// </summary>
        public void EnablePriorityCrew(SimulationTime now)
        {
            if (PriorityCrewEnabled)
                return;

            PriorityCrewEnabled = true;
            var elapsed = Elapsed(now);
            if (elapsed <= 0)
                return;

            // Mid-workflow: freeze completion from remaining work only.
            PriorityCrewEnabled = false;
            var withoutPriority = ComputeCompletionOffset(applyPriority: false);
            PriorityCrewEnabled = true;

            if (elapsed >= withoutPriority)
            {
                _forcedCompletionOffset = withoutPriority;
                return;
            }

            var remaining = withoutPriority - elapsed;
            var scaledRemaining = Math.Max(1L, (long)Math.Ceiling(remaining * 0.7));
            _forcedCompletionOffset = elapsed + scaledRemaining;
        }

        /// <summary>Legacy enable at turnaround start (t=elapsed 0); remaining = full window.</summary>
        public void EnablePriorityCrew() => EnablePriorityCrew(_startedAt);

        public bool IsComplete(SimulationTime now) => Elapsed(now) >= CompletionOffset();

        public double Progress01(SimulationTime now)
        {
            var total = Math.Max(1L, CompletionOffset());
            return Math.Max(0, Math.Min(1, Elapsed(now) / (double)total));
        }

        public long DelaySeconds(SimulationTime now) => Math.Max(0, Elapsed(now) - ScheduledWindowSeconds);

        public string DelayCause(SimulationTime now)
        {
            if (Elapsed(now) < ScheduledWindowSeconds || IsComplete(now))
                return string.Empty;

            if (IsUnderstaffed)
                return "Understaffed ground crew";
            if (HasCleaningDisruption)
                return "Cabin cleaning disruption";

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
            if (_forcedCompletionOffset.HasValue)
                return _forcedCompletionOffset.Value;
            return ComputeCompletionOffset(applyPriority: true);
        }

        private long ComputeCompletionOffset(bool applyPriority)
        {
            var deplaneEnd = Duration(8, applyPriority);
            var unloadEnd = Duration(12, applyPriority);
            var cleaningEnd = deplaneEnd + Duration(15 + _cleaningDisruptionSeconds, applyPriority);
            var loadEnd = unloadEnd + Duration(16, applyPriority);
            var boardingEnd = cleaningEnd + Duration(18, applyPriority);
            return Math.Max(Math.Max(Duration(18, applyPriority), loadEnd), boardingEnd);
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

        private long Duration(long normalSeconds) => Duration(normalSeconds, applyPriority: true);

        private long Duration(long normalSeconds, bool applyPriority)
        {
            var factor = CurrentStaffingFactor() * (applyPriority && PriorityCrewEnabled ? 0.7 : 1.0);
            if (factor == 1.0)
                return normalSeconds;
            return Math.Max(1, (long)Math.Ceiling(normalSeconds * factor));
        }

        private double CurrentStaffingFactor()
        {
            var factor = _staffingFactor();
            return factor > 0 ? factor : 1.0;
        }

        private long Elapsed(SimulationTime now) => Math.Max(0, now.ElapsedSeconds - _startedAt.ElapsedSeconds);
    }
}
