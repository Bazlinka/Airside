using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// A second aircraft that runs its own repeating arrival and departure schedule
    /// while the primary flight runs its cycle. It taxis the shared segments A1 and
    /// A2 and reserves every segment and stand it uses through the same
    /// <see cref="ReservationTable"/>, so the two aircraft genuinely contend for the
    /// airfield.
    ///
    /// At the start of each arrival it parks on whichever stand the primary flight
    /// is not assigned. The primary flight still has absolute priority: on every
    /// simulated second this aircraft releases any resource the flight needs, the
    /// flight takes its reservations, and this aircraft then moves into whatever is
    /// left. It holds its current position until it can reserve the next leg, and a
    /// hold beyond ten seconds is explained by the <see cref="TrafficWaitMonitor"/>.
    ///
    /// Motion is a pure function of the simulated seconds it has actually spent
    /// moving, of the reservation table, and of the primary flight's stand
    /// assignment, so it is deterministic across frame rates and reconstructed
    /// exactly on load.
    /// </summary>
    public sealed class GroundTrafficAircraft
    {
        public static readonly StableId Id = new("GT-201");

        private readonly struct Leg
        {
            public Leg(string name, StableId[] resources, TaxiPoint from, TaxiPoint to, long seconds, bool parks = false)
            {
                Name = name;
                Resources = resources;
                From = from;
                To = to;
                Seconds = seconds;
                Parks = parks;
            }

            public string Name { get; }
            public StableId[] Resources { get; }
            public TaxiPoint From { get; }
            public TaxiPoint To { get; }
            public long Seconds { get; }
            public bool Parks { get; }
        }

        private static readonly StableId[] None = Array.Empty<StableId>();

        private readonly ReservationTable _reservations;
        private Leg[] _circuit;
        private StableId _targetStand;
        private int _legIndex;
        private long _secondsOnLeg;
        private bool _onLeg;
        private StableId[] _held = None;

        public GroundTrafficAircraft(ReservationTable reservations)
        {
            _reservations = reservations ?? throw new ArgumentNullException(nameof(reservations));
            _targetStand = AirportSimulation.StandTwo;
            _circuit = BuildCircuit(_targetStand);
        }

        /// <summary>Human-readable phase, e.g. "Taxi in on A1" or "At Stand 1".</summary>
        public string CurrentPhase => _circuit[_legIndex].Name;

        /// <summary>The stand this aircraft is arriving at this circuit.</summary>
        public StableId TargetStand => _targetStand;

        /// <summary>The first resource it currently holds, or <c>default</c> when it holds none.</summary>
        public StableId CurrentSegment => _held.Length > 0 ? _held[0] : default;

        /// <summary>The first resource the current leg needs.</summary>
        public StableId DesiredSegment => _circuit[_legIndex].Resources.Length > 0
            ? _circuit[_legIndex].Resources[0]
            : default;

        /// <summary>True while it is blocked waiting for a resource the flight holds.</summary>
        public bool IsHolding { get; private set; }

        /// <summary>True while it is parked on its stand.</summary>
        public bool IsAtStand => _onLeg && _circuit[_legIndex].Parks;

        /// <summary>0..1 progress along the current leg.</summary>
        public double Progress => _onLeg
            ? Math.Max(0d, Math.Min(1d, _secondsOnLeg / (double)_circuit[_legIndex].Seconds))
            : 0d;

        /// <summary>World position along the taxiway for the presentation layer.</summary>
        public TaxiPoint Position
        {
            get
            {
                var leg = _circuit[_legIndex];
                var t = (float)Progress;
                return new TaxiPoint(
                    leg.From.X + (leg.To.X - leg.From.X) * t,
                    leg.From.Z + (leg.To.Z - leg.From.Z) * t);
            }
        }

        /// <summary>
        /// Release any resource the primary flight needs this tick. Called before the
        /// flight synchronises its own reservations so it never has to wait.
        /// </summary>
        public void Yield(IEnumerable<StableId> primaryResources)
        {
            if (_held.Length == 0)
                return;

            foreach (var required in primaryResources)
            {
                foreach (var owned in _held)
                {
                    if (required.Equals(owned))
                    {
                        _reservations.Release(Id);
                        _held = None;
                        _onLeg = false;
                        _secondsOnLeg = 0;
                        IsHolding = true;
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Advance one simulated second. Called after the primary flight has taken
        /// its reservations for this tick. <paramref name="primaryStand"/> is the
        /// stand the primary flight is currently assigned; a fresh arrival parks on
        /// the other one.
        /// </summary>
        public void Reposition(SimulationTime now, TrafficWaitMonitor monitor, StableId primaryStand)
        {
            if (_legIndex == 0 && !_onLeg)
            {
                var away = primaryStand.Equals(AirportSimulation.StandOne)
                    ? AirportSimulation.StandTwo
                    : AirportSimulation.StandOne;
                if (!away.Equals(_targetStand))
                {
                    _targetStand = away;
                    _circuit = BuildCircuit(away);
                }
            }

            var leg = _circuit[_legIndex];

            if (!_onLeg)
            {
                if (leg.Resources.Length == 0)
                {
                    _reservations.Release(Id);
                    _held = None;
                }
                else if (_reservations.TryReplace(Id, leg.Resources, out var blocked))
                {
                    _held = leg.Resources;
                }
                else
                {
                    IsHolding = true;
                    monitor.SetWaiting(Id, blocked, now);
                    return;
                }

                _onLeg = true;
                _secondsOnLeg = 0;
                IsHolding = false;
                monitor.Clear(Id);
            }

            _secondsOnLeg++;
            if (_secondsOnLeg >= leg.Seconds)
            {
                _legIndex = (_legIndex + 1) % _circuit.Length;
                _onLeg = false;
            }
        }

        private static Leg[] BuildCircuit(StableId stand)
        {
            var isStandOne = stand.Equals(AirportSimulation.StandOne);
            var leadIn = isStandOne ? AirportTaxiNetwork.StandOneLeadIn : AirportTaxiNetwork.StandTwoLeadIn;
            var standPoint = new TaxiPoint(17f, isStandOne ? 14f : 20f);
            var label = isStandOne ? "Stand 1" : "Stand 2";

            // Runway end (-24,0) -> A1/A2 junction (-12,9) -> A2 end (8,9) -> stand,
            // then back out and away for a gap before the next arrival.
            return new[]
            {
                new Leg("Taxi in on A1", new[] { AirportTaxiNetwork.AlphaOne }, new TaxiPoint(-24f, 0f), new TaxiPoint(-12f, 9f), 14),
                new Leg("Taxi in on A2", new[] { AirportTaxiNetwork.AlphaTwo }, new TaxiPoint(-12f, 9f), new TaxiPoint(8f, 9f), 14),
                new Leg($"Taxi to {label}", new[] { leadIn, stand }, new TaxiPoint(8f, 9f), standPoint, 10),
                new Leg($"At {label}", new[] { stand }, standPoint, standPoint, 40, parks: true),
                new Leg("Taxi out on A2", new[] { AirportTaxiNetwork.AlphaTwo }, standPoint, new TaxiPoint(-12f, 9f), 16),
                new Leg("Taxi out on A1", new[] { AirportTaxiNetwork.AlphaOne }, new TaxiPoint(-12f, 9f), new TaxiPoint(-24f, 0f), 14),
                new Leg("Departing", None, new TaxiPoint(-24f, 0f), new TaxiPoint(-36f, -4f), 8),
                new Leg("Away", None, new TaxiPoint(-60f, -30f), new TaxiPoint(-60f, -30f), 30)
            };
        }
    }
}
