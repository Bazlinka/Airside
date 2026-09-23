using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>How passengers reach a parked aircraft's door (ADR 0114).</summary>
    public enum BoardingMode
    {
        /// <summary>Not parked at home.</summary>
        None,
        /// <summary>Terminal 1 bridge: passengers board inside the tunnel, out of sight.</summary>
        Aerobridge,
        /// <summary>Saab 340 / Dash 8 / ATR: the forward door folds down into its own airstair.</summary>
        IntegralAirstair,
        /// <summary>A jet on a stand without a bridge: a stair truck drives up to its L1 door.</summary>
        StairTruck
    }

    /// <summary>One passenger walking between the terminal and the aircraft door.</summary>
    public readonly struct PassengerMove
    {
        public PassengerMove(int index, bool boarding, double startSeconds, float speedMetresPerSecond, int look)
        {
            Index = index;
            Boarding = boarding;
            StartSeconds = startSeconds;
            SpeedMetresPerSecond = speedMetresPerSecond;
            Look = look;
        }

        public int Index { get; }
        /// <summary>True: terminal → aircraft. False: deplaning, aircraft → terminal.</summary>
        public bool Boarding { get; }
        /// <summary>When this passenger leaves the terminal (boarding) or the door (deplaning).</summary>
        public double StartSeconds { get; }
        public float SpeedMetresPerSecond { get; }
        /// <summary>Stable pick of outfit / character for this passenger.</summary>
        public int Look { get; }
    }

    /// <summary>
    /// Passengers on and off an aircraft as a pure function of fleet state and time
    /// (ADR 0114) — presentation only, like <see cref="EngineStartSequence"/>; nothing in
    /// the simulation waits for them. Arrivals deplane once the door opens; departures board
    /// so the last passenger is aboard before the door closes. Numbers come from the type's
    /// seats and a stable per-flight load factor; each passenger walks at their own pace.
    /// </summary>
    public static class BoardingFlow
    {
        public const double TurbopropIntervalSeconds = 2.6;
        public const double JetIntervalSeconds = 2.0;
        /// <summary>Allowance for the longest apron walk, so the last boarder makes the door.</summary>
        public const double WalkBudgetSeconds = 110;
        public const double DeplaneAfterDoorsSeconds = 5;

        // Stair truck: drives up after the engines are off, leaves before the push (ADR 0114).
        public const double StairTruckDockAfterParkSeconds = 40;
        public const double StairTruckMoveSeconds = 40;
        public const double StairTruckDoorsCloseBeforePushSeconds = 240;
        public const double StairTruckLeaveBeforePushSeconds = 230;

        public static BoardingMode ModeFor(FleetAircraft aircraft)
        {
            if (aircraft == null || aircraft.State != FleetState.AtStand)
                return BoardingMode.None;
            if (AdelaideAerobridges.Serves(aircraft.Stand))
                return BoardingMode.Aerobridge;
            return AirlineOperations.NeedsTerminalGate(aircraft.Type)
                ? BoardingMode.StairTruck
                : BoardingMode.IntegralAirstair;
        }

        /// <summary>Passengers carried this rotation: seats × a stable 62–94 % load factor.</summary>
        public static int PassengerCount(FleetAircraft aircraft)
        {
            if (aircraft == null)
                return 0;
            var seats = AircraftCatalogue.TypicalSeats(aircraft.Type);
            var load = 0.62 + Hash(aircraft.Registration, aircraft.CompletedTrips, 7) % 33 / 100.0;
            return Math.Max(1, (int)Math.Round(seats * load));
        }

        /// <summary>0 = stair truck away, 1 = at the L1 door. Only for <see cref="BoardingMode.StairTruck"/>.</summary>
        public static float StairTruckFraction(FleetAircraft aircraft, double nowSeconds)
        {
            if (ModeFor(aircraft) != BoardingMode.StairTruck)
                return 0f;
            var parked = nowSeconds - aircraft.StateStartedAt.ElapsedSeconds;
            var docked = Ramp((parked - StairTruckDockAfterParkSeconds) / StairTruckMoveSeconds);
            if (aircraft.Scheduled is { Cancelled: false } departure)
            {
                var leave = departure.DepartAt.ElapsedSeconds - StairTruckLeaveBeforePushSeconds;
                docked = Math.Min(docked, 1f - Ramp((nowSeconds - leave) / StairTruckMoveSeconds));
            }

            return docked;
        }

        /// <summary>
        /// Door state on a stair-truck stand: open once the truck is on, shut before it
        /// leaves. Null for any other stand (bridge and integral-stair timing apply there).
        /// </summary>
        public static bool? StairTruckDoorsOpen(FleetAircraft aircraft, double nowSeconds)
        {
            if (ModeFor(aircraft) != BoardingMode.StairTruck)
                return null;
            var parked = nowSeconds - aircraft.StateStartedAt.ElapsedSeconds;
            if (parked < EngineStartSequence.DoorsOpenAfterSeconds)
                return false;
            if (aircraft.Scheduled is { Cancelled: false } departure)
                return nowSeconds < departure.DepartAt.ElapsedSeconds - StairTruckDoorsCloseBeforePushSeconds;
            return true;
        }

        /// <summary>
        /// Every passenger movement that has started within <paramref name="lookBackSeconds"/>
        /// of <paramref name="nowSeconds"/>. Presentation places each along its walk and drops it
        /// once it has arrived. Empty on a bridged gate (boarding is inside the tunnel).
        /// </summary>
        public static void Moves(FleetAircraft aircraft, double nowSeconds, List<PassengerMove> into,
            double lookBackSeconds = 180, PlayerBaseLevel baseLevel = PlayerBaseLevel.Starter)
        {
            into?.Clear();
            var mode = ModeFor(aircraft);
            if (into == null || mode is BoardingMode.None or BoardingMode.Aerobridge)
                return;

            var interval = AirlineOperations.NeedsTerminalGate(aircraft.Type) ? JetIntervalSeconds : TurbopropIntervalSeconds;
            var parkedAt = aircraft.StateStartedAt.ElapsedSeconds;
            var doorsOpen = parkedAt + EngineStartSequence.DoorsOpenAfterSeconds;

            // Deplaning: everyone who flew in, once the door is open.
            var deplaneEnd = doorsOpen;
            if (aircraft.CompletedTrips > 0)
            {
                var arrived = PassengerCount(aircraft.CompletedTrips - 1, aircraft);
                var start = doorsOpen + DeplaneAfterDoorsSeconds;
                deplaneEnd = start + arrived * interval;
                AddWindow(aircraft, into, false, start, interval, arrived, nowSeconds, lookBackSeconds);
            }

            // Boarding: finish before the door closes for the push.
            if (aircraft.Scheduled is not { Cancelled: false } departure)
                return;
            var passengers = PassengerCount(aircraft);
            var doorsClose = departure.DepartAt.ElapsedSeconds - (mode == BoardingMode.StairTruck
                ? StairTruckDoorsCloseBeforePushSeconds
                : EngineStartSequence.DoorsCloseBeforeSeconds);
            double boardStart;
            var boardInterval = interval;
            if (aircraft.Airline.IsPlayer)
            {
                // The player's Boarding prep stage is the window.
                var total = DeparturePrep.TotalSeconds(aircraft.Type, baseLevel);
                var prepStart = aircraft.PrepStartedAt?.ElapsedSeconds ?? departure.DepartAt.ElapsedSeconds - total;
                var boardingSeconds = DeparturePrep.BoardingSecondsFor(aircraft.Type, baseLevel);
                boardStart = prepStart + total - boardingSeconds;
                boardInterval = Math.Max(0.6, (boardingSeconds - WalkBudgetSeconds * 0.5) / Math.Max(1, passengers));
            }
            else
            {
                boardStart = doorsClose - WalkBudgetSeconds - passengers * interval;
            }

            boardStart = Math.Max(boardStart, deplaneEnd + 20);
            AddWindow(aircraft, into, true, boardStart, boardInterval, passengers, nowSeconds, lookBackSeconds);
        }

        private static int PassengerCount(int trips, FleetAircraft aircraft)
        {
            var seats = AircraftCatalogue.TypicalSeats(aircraft.Type);
            var load = 0.62 + Hash(aircraft.Registration, trips, 7) % 33 / 100.0;
            return Math.Max(1, (int)Math.Round(seats * load));
        }

        private static void AddWindow(FleetAircraft aircraft, List<PassengerMove> into, bool boarding,
            double start, double interval, int count, double now, double lookBack)
        {
            if (now < start)
                return;
            var first = Math.Max(0, (int)Math.Floor((now - lookBack - start) / interval));
            var last = Math.Min(count - 1, (int)Math.Floor((now - start) / interval));
            for (var i = first; i <= last; i++)
            {
                var h = Hash(aircraft.Registration, aircraft.CompletedTrips * 2 + (boarding ? 1 : 0), i);
                // Small stagger so the file is not metronomic; never earlier than the window.
                var t = start + i * interval + (h % 7) * interval * 0.08;
                if (t > now || t < now - lookBack)
                    continue;
                var speed = 1.15f + (h / 7 % 30) / 100f;
                into.Add(new PassengerMove(i, boarding, t, speed, h / 211));
            }
        }

        private static int Hash(string registration, int salt, int index)
        {
            unchecked
            {
                var h = 17;
                foreach (var ch in registration ?? string.Empty)
                    h = h * 31 + ch;
                h = h * 31 + salt;
                h = h * 31 + index;
                h ^= h >> 13;
                h *= 0x5bd1e995;
                return h & int.MaxValue;
            }
        }

        private static float Ramp(double t)
        {
            if (t <= 0) return 0f;
            if (t >= 1) return 1f;
            return (float)(t * t * (3 - 2 * t));
        }
    }
}
