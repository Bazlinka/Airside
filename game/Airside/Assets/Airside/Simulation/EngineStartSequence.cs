using System;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>What a parked or departing aircraft's engines, beacon and doors are doing.</summary>
    public readonly struct EngineState
    {
        public EngineState(float left, float right, bool beacon, bool doorsOpen)
            : this(left, right, beacon, doorsOpen ? 1f : 0f, 0f)
        {
        }

        public EngineState(float left, float right, bool beacon, float passengerDoor, float cargoDoor)
        {
            Left = left;
            Right = right;
            Beacon = beacon;
            PassengerDoor = Math.Max(0f, Math.Min(1f, passengerDoor));
            CargoDoor = Math.Max(0f, Math.Min(1f, cargoDoor));
        }

        /// <summary>0 stopped … 1 running, for engine No.1 (left).</summary>
        public float Left { get; }

        /// <summary>0 stopped … 1 running, for engine No.2 (right).</summary>
        public float Right { get; }

        public bool Beacon { get; }

        /// <summary>0 shut … 1 open: the passenger door (or airstair), part-way while it moves.</summary>
        public float PassengerDoor { get; }

        /// <summary>0 shut … 1 open: the hold doors.</summary>
        public float CargoDoor { get; }

        public bool DoorsOpen => PassengerDoor > 0.5f;

        public bool AnyRunning => Left > 0.02f || Right > 0.02f;

        public static EngineState Running => new(1f, 1f, beacon: true, doorsOpen: false);
        public static EngineState ColdAndOpen => new(0f, 0f, beacon: false, doorsOpen: true);
    }

    /// <summary>
    /// Engine start before departure and shutdown after parking, as a pure function of fleet
    /// state and time (presentation only; it never changes when anything happens).
    ///
    /// Before pushback everything follows <see cref="DepartureCountdown"/>: doors shut, the
    /// bridge or stairs clear, beacon on; a turboprop then starts No.2 (right) and No.1 (left)
    /// on the stand — the usual ATR order — and a jet starts them during the push. Each spools
    /// over <see cref="SpoolSeconds"/>. After parking: No.1 then No.2 wind down, the beacon
    /// goes off, then the doors open.
    /// </summary>
    public static class EngineStartSequence
    {
        public const double BeaconOnBeforeSeconds = DepartureCountdown.TurbopropBeaconBeforeSeconds;
        public const double DoorsCloseBeforeSeconds = DepartureCountdown.DoorsClosedBeforeSeconds;
        public const double RightStartBeforeSeconds = DepartureCountdown.TurbopropRightStartBeforeSeconds;
        public const double LeftStartBeforeSeconds = DepartureCountdown.TurbopropLeftStartBeforeSeconds;
        public const double SpoolSeconds = 30;

        public const double LeftStopAfterSeconds = 15;
        public const double RightStopAfterSeconds = 35;
        public const double BeaconOffAfterSeconds = 75;
        public const double DoorsOpenAfterSeconds = 90;

        /// <summary>The earliest sensible departure: time enough to run the whole start.</summary>
        public const long MinimumDepartureLeadSeconds = 180;

        public static EngineState For(FleetAircraft aircraft, double nowSeconds)
        {
            // A helicopter spools its rotor on the pad, with no pushback, stairs or doors (ADR 0207).
            if (aircraft != null && aircraft.Type.IsRotorcraft)
                return RotorcraftEngines.StateFor(aircraft, nowSeconds);

            // A jet starts its engines during the push (ADR 0177): No.2, then No.1.
            if (aircraft != null && aircraft.State == FleetState.TaxiOut && AirlineOperations.NeedsTerminalGate(aircraft.Type))
            {
                var pushing = nowSeconds - aircraft.StateStartedAt.ElapsedSeconds;
                return new EngineState(Ramp(pushing - DepartureCountdown.JetLeftStartAfterPushSeconds),
                    Ramp(pushing - DepartureCountdown.JetRightStartAfterPushSeconds), beacon: true, 0f, 0f);
            }

            // In a check the aircraft is towed to a hangar (ADR 0186): engines off, beacon off, doors shut.
            if (aircraft != null && aircraft.State == FleetState.AtStand && BoardingFlow.InCheck(aircraft, nowSeconds))
                return new EngineState(0f, 0f, beacon: false, 0f, 0f);

            var state = ForStairs(aircraft, nowSeconds);
            if (aircraft == null || aircraft.State != FleetState.AtStand)
                return state;
            // The doors follow the people and bags using them, on every kind of stand: open to
            // deplane and to board, shut between rotations and overnight, and shut on the
            // departure countdown before the bridge or stairs move and any engine starts.
            var passengerDoor = BoardingFlow.PassengerDoorOpen(aircraft, nowSeconds);
            if (AerobridgeTimeline.DoorsOpen(aircraft, nowSeconds) == false
                || BoardingFlow.StairTruckDoorsOpen(aircraft, nowSeconds) == false)
                passengerDoor = Math.Min(passengerDoor, ClosingTail(aircraft, nowSeconds));
            return new EngineState(state.Left, state.Right, state.Beacon, passengerDoor,
                BoardingFlow.CargoDoorOpen(aircraft, nowSeconds));
        }

        /// <summary>A door already moving shut when the bridge or truck says shut finishes its swing.</summary>
        private static float ClosingTail(FleetAircraft aircraft, double nowSeconds) =>
            DepartureCountdown.For(aircraft) is { } countdown
                ? DepartureCountdown.Open(nowSeconds, double.MinValue / 4, countdown.DoorsClosed,
                    DepartureCountdown.DoorSeconds(aircraft))
                : 0f;

        private static EngineState ForStairs(FleetAircraft aircraft, double nowSeconds)
        {
            if (aircraft == null || aircraft.State != FleetState.AtStand)
                return EngineState.Running;

            if (aircraft.Scheduled is { Cancelled: true })
            {
                // A cancelled booking must not spool up for a push that will never happen.
                if (aircraft.CompletedTrips == 0)
                    return EngineState.ColdAndOpen;
                var parkedCancelled = nowSeconds - aircraft.StateStartedAt.ElapsedSeconds;
                return new EngineState(
                    1f - Ramp(parkedCancelled - LeftStopAfterSeconds),
                    1f - Ramp(parkedCancelled - RightStopAfterSeconds),
                    beacon: parkedCancelled < BeaconOffAfterSeconds,
                    doorsOpen: parkedCancelled >= DoorsOpenAfterSeconds);
            }

            if (DepartureCountdown.For(aircraft) is { } countdown && nowSeconds >= countdown.BeaconOn)
            {
                // A jet's engines wait for the push; a turboprop starts on the stand.
                var jet = AirlineOperations.NeedsTerminalGate(aircraft.Type);
                return new EngineState(
                    jet ? 0f : Ramp(nowSeconds - countdown.LeftStart),
                    jet ? 0f : Ramp(nowSeconds - countdown.RightStart),
                    beacon: true,
                    doorsOpen: false);
            }

            // A brand-new aircraft has never arrived, so it has nothing to shut down.
            if (aircraft.CompletedTrips == 0)
                return EngineState.ColdAndOpen;

            var parked = nowSeconds - aircraft.StateStartedAt.ElapsedSeconds;
            return new EngineState(
                1f - Ramp(parked - LeftStopAfterSeconds),
                1f - Ramp(parked - RightStopAfterSeconds),
                beacon: parked < BeaconOffAfterSeconds,
                doorsOpen: parked >= DoorsOpenAfterSeconds);
        }

        private static float Ramp(double secondsSinceStart)
        {
            var t = secondsSinceStart / SpoolSeconds;
            if (t <= 0) return 0f;
            if (t >= 1) return 1f;
            return (float)(t * t * (3 - 2 * t));
        }
    }
}
