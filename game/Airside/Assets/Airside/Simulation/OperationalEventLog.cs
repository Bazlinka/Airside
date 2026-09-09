using System;
using System.Collections.Generic;
using System.Linq;
using Airside.Domain;

namespace Airside.Simulation
{
    public sealed class OperationalEvent
    {
        public OperationalEvent(SimulationTime occurredAt, string flightId, string title, string detail)
        {
            OccurredAt = occurredAt;
            FlightId = flightId;
            Title = title;
            Detail = detail;
        }

        public SimulationTime OccurredAt { get; }
        public string FlightId { get; }
        public string Title { get; }
        public string Detail { get; }
    }

    public sealed class OperationalEventLog
    {
        private readonly int _capacity;
        private readonly List<OperationalEvent> _events = new();

        public OperationalEventLog(int capacity = 80)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
        }

        public IReadOnlyList<OperationalEvent> Events => _events.ToArray();

        public void Add(OperationalEvent entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            _events.Add(entry);
            TrimToCapacity();
        }

        private void TrimToCapacity()
        {
            while (_events.Count > _capacity)
            {
                var removeAt = IndexOfFirstRemovable();
                // If the log is only Insolvent rows, still drop the oldest so capacity
                // cannot grow without bound.
                if (removeAt < 0)
                    removeAt = 0;
                _events.RemoveAt(removeAt);
            }
        }

        /// <summary>
        /// Prefer dropping ordinary events; keep titles that contain "Insolvent"
        /// so the terminal outcome stays visible in the log.
        /// </summary>
        private int IndexOfFirstRemovable()
        {
            for (var i = 0; i < _events.Count; i++)
            {
                var title = _events[i].Title ?? string.Empty;
                if (title.IndexOf("Insolvent", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                return i;
            }

            return -1;
        }
    }
}
