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

        public OperationalEventLog(int capacity = 40)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
        }

        public IReadOnlyList<OperationalEvent> Events => _events.ToArray();

        public void Add(OperationalEvent entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            _events.Add(entry);
            if (_events.Count > _capacity)
                _events.RemoveRange(0, _events.Count - _capacity);
        }
    }
}
