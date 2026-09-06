using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    public sealed class AirportSimulation
    {
        public const long CycleLengthSeconds = 160;
        public const long DepartureResetSeconds = 6;

        public static readonly StableId Runway = new("RUNWAY-09-27");
        public static readonly StableId Taxiway = new("TAXI-A");
        public static readonly StableId ApronLane = new("APRON-LANE");
        public static readonly StableId StandOne = new("STAND-1");
        public static readonly StableId StandTwo = new("STAND-2");

        private readonly ISimulationClock _clock;
        private readonly IRandomSource _random;
        private readonly ReservationTable _reservations;
        private SimulationTime _cycleStartedAt;
        private SimulationTime _lastUpdatedAt;
        private bool _flightSettled;

        public AirportSimulation(ISimulationClock clock, IRandomSource random, ReservationTable reservations)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _reservations = reservations ?? throw new ArgumentNullException(nameof(reservations));
            _cycleStartedAt = clock.Now;
            _lastUpdatedAt = clock.Now;
            Economy = new AirportEconomy();
            StartCycle();
            SynchronizeReservations();
        }

        public AircraftOperation ActiveAircraft { get; private set; }
        public StableId AssignedStand { get; private set; }
        public int CompletedCycles { get; private set; }
        public int ReservationConflicts { get; private set; }
        public TurnaroundWorkflow ActiveTurnaround { get; private set; }
        public AirportEconomy Economy { get; }
        public long LastDelaySeconds { get; private set; }
        public string LastDelayCause { get; private set; } = string.Empty;
        public long CurrentDelaySeconds => ActiveAircraft.Phase == AircraftPhase.AtStand && ActiveTurnaround != null
            ? ActiveTurnaround.DelaySeconds(_clock.Now)
            : 0;
        public string CurrentDelayCause => ActiveAircraft.Phase == AircraftPhase.AtStand && ActiveTurnaround != null
            ? ActiveTurnaround.DelayCause(_clock.Now)
            : string.Empty;
        public SimulationTime CycleStartedAt => _cycleStartedAt;
        public ReservationTable Reservations => _reservations;

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

        public bool EnablePriorityCrew()
        {
            if (ActiveAircraft.Phase != AircraftPhase.AtStand || ActiveTurnaround == null || ActiveTurnaround.PriorityCrewEnabled)
                return false;
            if (!Economy.PurchasePriorityCrew())
                return false;

            ActiveTurnaround.EnablePriorityCrew();
            return true;
        }

        private void AdvanceOneSecond(SimulationTime now)
        {
            if (ActiveAircraft.IsComplete)
            {
                if (now.CompareTo(ActiveAircraft.PhaseStartedAt.Advance(DepartureResetSeconds)) >= 0)
                {
                    _reservations.Release(new StableId(ActiveAircraft.AircraftId));
                    CompletedCycles++;
                    _cycleStartedAt = now;
                    StartCycle();
                }

                SynchronizeReservations();
                return;
            }

            var previousPhase = ActiveAircraft.Phase;
            ActiveAircraft.AdvanceTo(now, CanLeavePhase);

            if (previousPhase != ActiveAircraft.Phase)
            {
                if (ActiveAircraft.Phase == AircraftPhase.AtStand)
                    ActiveTurnaround = new TurnaroundWorkflow(ActiveAircraft.PhaseStartedAt, _random.NextInt(0, 3) == 0);

                if (previousPhase == AircraftPhase.AtStand && ActiveTurnaround != null)
                {
                    LastDelaySeconds = ActiveTurnaround.DelaySeconds(now);
                    LastDelayCause = LastDelaySeconds > 0 && ActiveTurnaround.HasCleaningDisruption
                        ? "Cabin cleaning disruption"
                        : string.Empty;
                }

                if (ActiveAircraft.Phase == AircraftPhase.Departed && !_flightSettled)
                {
                    Economy.CompleteFlight(LastDelaySeconds);
                    _flightSettled = true;
                }
            }

            SynchronizeReservations();
        }

        private bool CanLeavePhase(AircraftPhase phase)
        {
            return phase != AircraftPhase.AtStand || ActiveTurnaround == null || ActiveTurnaround.IsComplete(_lastUpdatedAt);
        }

        private void StartCycle()
        {
            var aircraftId = new StableId($"AS-{CompletedCycles + 101:000}");
            AssignedStand = _random.NextInt(0, 2) == 0 ? StandOne : StandTwo;
            ActiveAircraft = new AircraftOperation(aircraftId.Value, _cycleStartedAt);
            ActiveTurnaround = null;
            _flightSettled = false;
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
