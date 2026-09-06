using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    public sealed class TrafficWait
    {
        public TrafficWait(StableId aircraft, StableId resource, SimulationTime startedAt)
        {
            Aircraft = aircraft;
            Resource = resource;
            StartedAt = startedAt;
        }

        public StableId Aircraft { get; }
        public StableId Resource { get; }
        public SimulationTime StartedAt { get; }
    }

    public sealed class TrafficWaitMonitor
    {
        public const long WarningAfterSeconds = 10;
        private readonly Dictionary<StableId, TrafficWait> _waits = new();

        public IReadOnlyCollection<TrafficWait> ActiveWaits => _waits.Values;

        public void SetWaiting(StableId aircraft, StableId resource, SimulationTime now)
        {
            if (!_waits.ContainsKey(aircraft))
                _waits[aircraft] = new TrafficWait(aircraft, resource, now);
        }

        public void Clear(StableId aircraft) => _waits.Remove(aircraft);

        /// <summary>When the given aircraft started waiting, if it is waiting now.</summary>
        public bool TryGetWaitStart(StableId aircraft, out SimulationTime startedAt)
        {
            if (_waits.TryGetValue(aircraft, out var wait))
            {
                startedAt = wait.StartedAt;
                return true;
            }

            startedAt = default;
            return false;
        }

        public bool HasWarning(SimulationTime now)
        {
            foreach (var wait in _waits.Values)
            {
                if (now.ElapsedSeconds - wait.StartedAt.ElapsedSeconds >= WarningAfterSeconds)
                    return true;
            }
            return false;
        }

        public string Describe(SimulationTime now)
        {
            foreach (var wait in _waits.Values)
            {
                var elapsed = Math.Max(0, now.ElapsedSeconds - wait.StartedAt.ElapsedSeconds);
                if (elapsed >= WarningAfterSeconds)
                    return $"{wait.Aircraft.Value} waiting {elapsed}s for {wait.Resource.Value}";
            }
            return string.Empty;
        }
    }
}
