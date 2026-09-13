using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// Circuit-only simulation: one aircraft flies approach → landing → takeoff →
    /// departed, then recycles onto a fresh approach. Nothing is scored, costed,
    /// scheduled or tracked (ADR 0041).
    ///
    /// The runway reservation and the taxi network are retained because the
    /// visible pavement and the aircraft's ground path are built from them, not
    /// because anything competes for them — a single aircraft never blocks itself.
    /// </summary>
    public sealed class AirportSimulation
    {
        /// <summary>How long a departed aircraft stays off-field before the next arrival.</summary>
        public static long DepartureResetSeconds => AirportCircuit.SkipGroundTaxi
            ? AirportCircuit.DepartureFlyOutSeconds
            : 6;

        public static readonly StableId Runway = new("RUNWAY-09-27");
        public static readonly StableId ApronLane = new("APRON-LANE");
        public static readonly StableId StandOne = new("STAND-1");
        public static readonly StableId StandTwo = new("STAND-2");
        public static readonly StableId StandThree = new("STAND-3");

        private readonly ISimulationClock _clock;
        private readonly ReservationTable _reservations;
        private readonly List<CommercialFlight> _flights = new();
        private SimulationTime _lastUpdatedAt;
        private int _nextAircraftNumber = 101;

        public AirportSimulation(ISimulationClock clock, IRandomSource random, ReservationTable reservations)
            : this(clock, random, reservations, AirportLocation.Default)
        {
        }

        /// <param name="random">
        /// Validated but not retained: the circuit is fully deterministic, so nothing
        /// draws from it. Kept in the signature because callers still supply a seed and
        /// a future variation (wind, traffic) will want it back.
        /// </param>
        public AirportSimulation(ISimulationClock clock, IRandomSource random, ReservationTable reservations, AirportLocation location)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            if (random == null) throw new ArgumentNullException(nameof(random));
            _reservations = reservations ?? throw new ArgumentNullException(nameof(reservations));
            Location = location;
            _lastUpdatedAt = clock.Now;
            TaxiNetwork = new AirportTaxiNetwork();
            Spawn(_clock.Now, StandOne);
            SynchronizeReservations();
        }

        public AirportLocation Location { get; }
        public DayCycle TimeOfDay => new(_clock.Now);
        public WeatherKind CurrentWeather => Weather.At(_lastUpdatedAt);
        public AirportTaxiNetwork TaxiNetwork { get; }
        public ReservationTable Reservations => _reservations;

        /// <summary>The single aircraft on the circuit, as a one-element list.</summary>
        public IReadOnlyList<CommercialFlight> Flights => _flights;

        /// <summary>Completed circuits since launch. Presentation-only counter.</summary>
        public int CompletedCycles { get; private set; }

        /// <summary>
        /// Times the aircraft could not claim what its phase needs. A single airframe
        /// has nothing to contend with, so this must stay zero — it is an invariant
        /// check on the reservation wiring, not a gameplay statistic.
        /// </summary>
        public int ReservationConflicts { get; private set; }

        public AircraftOperation ActiveAircraft => Primary.Operation;
        public StableId AssignedStand => Primary.AssignedStand;
        public StableId CurrentTaxiSegment => Primary.SegmentFor(_clock.Now);
        public SimulationTime CycleStartedAt => Primary.CycleStartedAt;

        private CommercialFlight Primary => _flights[0];

        public void Update()
        {
            if (_clock.Now.CompareTo(_lastUpdatedAt) < 0)
                throw new InvalidOperationException("Simulation time cannot move backwards.");

            while (_lastUpdatedAt.CompareTo(_clock.Now) < 0)
            {
                _lastUpdatedAt = _lastUpdatedAt.Advance(1);
                AdvanceOneSecond(_lastUpdatedAt);
            }
        }

        /// <summary>
        /// Put a fresh aircraft on approach immediately, discarding the one in the
        /// air. Drives the pause menu's "Restart circuit".
        /// </summary>
        public void RestartCircuit()
        {
            var now = _clock.Now;
            _reservations.Release(Primary.OwnerId);
            _flights.Clear();
            CompletedCycles = 0;
            Spawn(now, StandOne);
            SynchronizeReservations();
        }

        private void AdvanceOneSecond(SimulationTime now)
        {
            var flight = Primary;

            if (flight.Operation.IsComplete)
            {
                // A departed aircraft must not hold the runway while it flies out.
                _reservations.Release(flight.OwnerId);
                if (now.CompareTo(flight.Operation.PhaseStartedAt.Advance(DepartureResetSeconds)) >= 0)
                    Respawn(flight, now);
                return;
            }

            var phaseBefore = flight.Operation.Phase;
            if (!_reservations.TryReplace(flight.OwnerId, flight.RequiredResources(now), out _))
            {
                // Unreachable with one airframe; counted rather than swallowed so the
                // invariant test can catch a regression in the reservation wiring.
                ReservationConflicts++;
                flight.Operation.StallOneSecond();
                return;
            }

            flight.Operation.AdvanceTo(now);

            if (phaseBefore != AircraftPhase.Departed && flight.Operation.Phase == AircraftPhase.Departed)
                CompletedCycles++;

            SynchronizeReservations();
        }

        private void Spawn(SimulationTime at, StableId stand)
        {
            var id = $"AS-{_nextAircraftNumber++:000}";
            _flights.Add(new CommercialFlight(id, at, stand, TaxiNetwork.RoutesTo(stand)));
        }

        private void Respawn(CommercialFlight flight, SimulationTime now)
        {
            _reservations.Release(flight.OwnerId);
            var stand = flight.AssignedStand;
            var id = $"AS-{_nextAircraftNumber++:000}";
            _flights[_flights.IndexOf(flight)] =
                new CommercialFlight(id, now, stand, TaxiNetwork.RoutesTo(stand));
            SynchronizeReservations();
        }

        private void SynchronizeReservations()
        {
            var flight = Primary;
            if (flight.Operation.IsComplete)
            {
                _reservations.Release(flight.OwnerId);
                return;
            }

            _reservations.TryReplace(flight.OwnerId, flight.RequiredResources(_clock.Now), out _);
        }
    }
}
