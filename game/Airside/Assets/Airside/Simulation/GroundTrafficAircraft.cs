using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// A second aircraft that repositions back and forth along the shared taxiway
    /// while the primary flight runs its cycle. It reserves the same named segments
    /// through the <see cref="ReservationTable"/>, so the two aircraft genuinely
    /// contend for A1 and A2.
    ///
    /// Commercial arrivals and departures have priority: this aircraft yields any
    /// segment the primary operation needs and holds position until the segment is
    /// free again. A prolonged hold is explained by the <see cref="TrafficWaitMonitor"/>.
    /// Movement is a pure function of the simulated seconds it has actually spent
    /// moving, so it stays deterministic across frame rates and offline catch-up.
    /// </summary>
    public sealed class GroundTrafficAircraft
    {
        public static readonly StableId Id = new("GT-201");
        public const long SecondsPerSegment = 16;

        private readonly struct Leg
        {
            public Leg(StableId segment, TaxiPoint from, TaxiPoint to)
            {
                Segment = segment;
                From = from;
                To = to;
            }

            public StableId Segment { get; }
            public TaxiPoint From { get; }
            public TaxiPoint To { get; }
        }

        // The two shared segments span points (-24,0)–(-12,9)–(8,9) in the taxi
        // network. This aircraft shuttles east→west along them and back.
        private static readonly Leg[] Circuit =
        {
            new(AirportTaxiNetwork.AlphaTwo, new TaxiPoint(8f, 9f), new TaxiPoint(-12f, 9f)),
            new(AirportTaxiNetwork.AlphaOne, new TaxiPoint(-12f, 9f), new TaxiPoint(-24f, 0f)),
            new(AirportTaxiNetwork.AlphaOne, new TaxiPoint(-24f, 0f), new TaxiPoint(-12f, 9f)),
            new(AirportTaxiNetwork.AlphaTwo, new TaxiPoint(-12f, 9f), new TaxiPoint(8f, 9f))
        };

        private readonly ReservationTable _reservations;
        private int _legIndex;
        private long _secondsOnSegment;
        private StableId _heldSegment;

        public GroundTrafficAircraft(ReservationTable reservations)
        {
            _reservations = reservations ?? throw new ArgumentNullException(nameof(reservations));
        }

        /// <summary>The segment this aircraft currently occupies, or <c>default</c> while it has none.</summary>
        public StableId CurrentSegment => _heldSegment;

        /// <summary>The segment it is trying to move onto next.</summary>
        public StableId DesiredSegment => Circuit[_legIndex].Segment;

        /// <summary>True while it is blocked waiting for a segment the primary holds.</summary>
        public bool IsHolding { get; private set; }

        /// <summary>0..1 progress across the current segment.</summary>
        public double SegmentProgress => _heldSegment.Equals(Circuit[_legIndex].Segment)
            ? Math.Max(0d, Math.Min(1d, _secondsOnSegment / (double)SecondsPerSegment))
            : 0d;

        /// <summary>World position along the taxiway for the presentation layer.</summary>
        public TaxiPoint Position
        {
            get
            {
                var leg = Circuit[_legIndex];
                var t = (float)SegmentProgress;
                return new TaxiPoint(
                    leg.From.X + (leg.To.X - leg.From.X) * t,
                    leg.From.Z + (leg.To.Z - leg.From.Z) * t);
            }
        }

        /// <summary>
        /// Release any segment the primary operation needs this tick. Called before the
        /// primary synchronises its own reservations so it never has to wait.
        /// </summary>
        public void Yield(IEnumerable<StableId> primaryResources)
        {
            if (_heldSegment.Equals(default))
                return;

            foreach (var resource in primaryResources)
            {
                if (resource.Equals(_heldSegment))
                {
                    _reservations.Release(Id);
                    _heldSegment = default;
                    _secondsOnSegment = 0;
                    IsHolding = true;
                    return;
                }
            }
        }

        /// <summary>
        /// Advance one simulated second. Called after the primary operation has taken
        /// its reservations for this tick.
        /// </summary>
        public void Reposition(SimulationTime now, TrafficWaitMonitor monitor)
        {
            var desired = Circuit[_legIndex].Segment;

            if (!_heldSegment.Equals(desired))
            {
                if (_reservations.TryReplace(Id, new[] { desired }, out var blocked))
                {
                    _heldSegment = desired;
                    _secondsOnSegment = 0;
                    IsHolding = false;
                    monitor.Clear(Id);
                }
                else
                {
                    IsHolding = true;
                    monitor.SetWaiting(Id, blocked, now);
                    return;
                }
            }

            _secondsOnSegment++;
            if (_secondsOnSegment >= SecondsPerSegment)
                _legIndex = (_legIndex + 1) % Circuit.Length;
        }
    }
}
