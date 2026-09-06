using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    public sealed class AirportSimulation
    {
        public const long CycleLengthSeconds = 160;

        public static readonly StableId Runway = new("RUNWAY-09-27");
        public static readonly StableId Taxiway = new("TAXI-A");
        public static readonly StableId ApronLane = new("APRON-LANE");
        public static readonly StableId StandOne = new("STAND-1");
        public static readonly StableId StandTwo = new("STAND-2");

        private readonly ISimulationClock _clock;
        private readonly IRandomSource _random;
        private readonly ReservationTable _reservations;
        private SimulationTime _cycleStartedAt;

        public AirportSimulation(ISimulationClock clock, IRandomSource random, ReservationTable reservations)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _reservations = reservations ?? throw new ArgumentNullException(nameof(reservations));
            _cycleStartedAt = clock.Now;
            StartCycle();
            SynchronizeReservations();
        }

        public AircraftOperation ActiveAircraft { get; private set; }
        public StableId AssignedStand { get; private set; }
        public int CompletedCycles { get; private set; }
        public int ReservationConflicts { get; private set; }
        public SimulationTime CycleStartedAt => _cycleStartedAt;
        public ReservationTable Reservations => _reservations;

        public void Update()
        {
            while (_clock.Now.CompareTo(_cycleStartedAt.Advance(CycleLengthSeconds)) >= 0)
            {
                _reservations.Release(new StableId(ActiveAircraft.AircraftId));
                CompletedCycles++;
                _cycleStartedAt = _cycleStartedAt.Advance(CycleLengthSeconds);
                StartCycle();
            }

            ActiveAircraft.AdvanceTo(_clock.Now);
            SynchronizeReservations();
        }

        private void StartCycle()
        {
            var aircraftId = new StableId($"AS-{CompletedCycles + 101:000}");
            AssignedStand = _random.NextInt(0, 2) == 0 ? StandOne : StandTwo;
            ActiveAircraft = new AircraftOperation(aircraftId.Value, _cycleStartedAt);
        }

        private void SynchronizeReservations()
        {
            var owner = new StableId(ActiveAircraft.AircraftId);
            if (!_reservations.TryReplace(owner, RequiredResources(), out _))
                ReservationConflicts++;
        }

        private IEnumerable<StableId> RequiredResources()
        {
            switch (ActiveAircraft.Phase)
            {
                case AircraftPhase.Landing:
                case AircraftPhase.Takeoff:
                    yield return Runway;
                    break;
                case AircraftPhase.TaxiIn:
                    yield return Taxiway;
                    yield return AssignedStand;
                    break;
                case AircraftPhase.AtStand:
                    yield return AssignedStand;
                    break;
                case AircraftPhase.Pushback:
                    yield return AssignedStand;
                    yield return ApronLane;
                    break;
                case AircraftPhase.TaxiOut:
                    yield return Taxiway;
                    break;
            }
        }
    }
}
