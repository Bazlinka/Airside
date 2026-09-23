using System;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>What a parked or departing aircraft's engines, beacon and doors are doing.</summary>
    public readonly struct EngineState
    {
        public EngineState(float left, float right, bool beacon, bool doorsOpen)
        {
            Left = left;
            Right = right;
            Beacon = beacon;
            DoorsOpen = doorsOpen;
        }

        /// <summary>0 stopped … 1 running, for engine No.1 (left).</summary>
        public float Left { get; }

        /// <summary>0 stopped … 1 running, for engine No.2 (right).</summary>
        public float Right { get; }

        public bool Beacon { get; }
        public bool DoorsOpen { get; }

        public bool AnyRunning => Left > 0.02f || Right > 0.02f;

        public static EngineState Running => new(1f, 1f, beacon: true, doorsOpen: false);
        public static EngineState ColdAndOpen => new(0f, 0f, beacon: false, doorsOpen: true);
    }

    /// <summary>
    /// Turboprop start before departure and shutdown after parking, as a pure function of
    /// fleet state and time (presentation only; it never changes when anything happens).
    ///
    /// Before pushback: beacon on, doors close, No.2 (right) starts, then No.1 (left) —
    /// the usual ATR order — each spooling up over <see cref="SpoolSeconds"/>. After
    /// parking: No.1 then No.2 wind down, the beacon goes off, then the doors open.
    /// </summary>
    public static class EngineStartSequence
    {
        public const double BeaconOnBeforeSeconds = 180;
        /// <summary>
        /// Close doors after boarding would finish for a jet (180 s) and shortly
        /// before the right engine start. Kept below <see cref="RightStartBeforeSeconds"/>.
        /// Player prep still forces doors open through Boarding (see <see cref="For"/>).
        /// </summary>
        public const double DoorsCloseBeforeSeconds = 25;
        public const double RightStartBeforeSeconds = 120;
        public const double LeftStartBeforeSeconds = 70;
        public const double SpoolSeconds = 30;

        public const double LeftStopAfterSeconds = 15;
        public const double RightStopAfterSeconds = 35;
        public const double BeaconOffAfterSeconds = 75;
        public const double DoorsOpenAfterSeconds = 90;

        /// <summary>The earliest sensible departure: time enough to run the whole start.</summary>
        public const long MinimumDepartureLeadSeconds = 180;

        public static EngineState For(FleetAircraft aircraft, double nowSeconds)
        {
            var state = ForStairs(aircraft, nowSeconds);
            // On a bridged Terminal 1 gate the L1 door follows the aerobridge instead: it opens
            // once the cab is docked and closes before the bridge pulls back (ADR 0113).
            var bridged = AerobridgeTimeline.DoorsOpen(aircraft, nowSeconds);
            return bridged.HasValue
                ? new EngineState(state.Left, state.Right, state.Beacon, bridged.Value)
                : state;
        }

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

            if (aircraft.Scheduled is { } departure)
            {
                var until = departure.DepartAt.ElapsedSeconds - nowSeconds;
                // Keep doors open while the player is still fueling / catering / boarding.
                var boardingOpen = false;
                if (aircraft.Airline.IsPlayer)
                {
                    var prep = DeparturePrep.For(aircraft, new SimulationTime((long)Math.Max(0, nowSeconds)));
                    boardingOpen = prep.Stage is DeparturePrepStage.Fuel
                        or DeparturePrepStage.Catering or DeparturePrepStage.Boarding;
                }

                if (until <= BeaconOnBeforeSeconds)
                {
                    return new EngineState(
                        Ramp(LeftStartBeforeSeconds - until),
                        Ramp(RightStartBeforeSeconds - until),
                        beacon: true,
                        doorsOpen: boardingOpen || until > DoorsCloseBeforeSeconds);
                }
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
