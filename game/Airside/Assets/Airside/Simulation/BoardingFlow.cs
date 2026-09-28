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
        StairTruck,
        /// <summary>
        /// A remote jet stand: passengers ride an apron bus, then use a stair truck at L1.
        /// The distinct mode lets presentation draw the bus without changing turnaround timing.
        /// </summary>
        RemoteBus
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

        // Remote bus: one trip receives the arriving load, a second returns for departure.
        // Both are pure timelines so loading a save or changing time scale recreates the same scene.
        public const double RemoteBusArriveAfterParkSeconds = 25;
        public const double RemoteBusMoveSeconds = 35;
        public const double RemoteBusArrivalLeaveAfterParkSeconds = 10 * 60;
        public const double RemoteBusDepartureArriveBeforePushSeconds = 25 * 60;
        public const double RemoteBusDepartureLeaveBeforePushSeconds = 190;

        public static BoardingMode ModeFor(FleetAircraft aircraft)
        {
            if (aircraft == null || aircraft.State != FleetState.AtStand)
                return BoardingMode.None;
            if (AdelaideAerobridges.Serves(aircraft.Stand))
                return BoardingMode.Aerobridge;
            return AirlineOperations.NeedsTerminalGate(aircraft.Type)
                ? BoardingMode.RemoteBus
                : BoardingMode.IntegralAirstair;
        }

        public static bool UsesStairTruck(BoardingMode mode) =>
            mode is BoardingMode.StairTruck or BoardingMode.RemoteBus;

        public static bool UsesRemoteBus(BoardingMode mode) => mode == BoardingMode.RemoteBus;

        /// <summary>
        /// 0 = at the terminal/depot, 1 = alongside the remote aircraft. The arrival bus clears
        /// after deplaning, then a departure bus returns before boarding and leaves before pushback.
        /// </summary>
        public static float RemoteBusFraction(FleetAircraft aircraft, double nowSeconds)
        {
            if (aircraft == null || !UsesRemoteBus(ModeFor(aircraft)))
                return 0f;
            var parked = nowSeconds - aircraft.StateStartedAt.ElapsedSeconds;
            var arrival = Ramp((parked - RemoteBusArriveAfterParkSeconds) / RemoteBusMoveSeconds);
            arrival = Math.Min(arrival, 1f - Ramp((parked - RemoteBusArrivalLeaveAfterParkSeconds) / RemoteBusMoveSeconds));

            var departure = 0f;
            if (aircraft.Scheduled is { Cancelled: false } booked)
            {
                var arrive = booked.DepartAt.ElapsedSeconds - RemoteBusDepartureArriveBeforePushSeconds;
                var leave = booked.DepartAt.ElapsedSeconds - RemoteBusDepartureLeaveBeforePushSeconds;
                departure = Ramp((nowSeconds - arrive) / RemoteBusMoveSeconds);
                departure = Math.Min(departure, 1f - Ramp((nowSeconds - leave) / RemoteBusMoveSeconds));
            }

            return Math.Max(arrival, departure);
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

        /// <summary>0 = stair truck away, 1 = at the L1 door.</summary>
        public static float StairTruckFraction(FleetAircraft aircraft, double nowSeconds)
        {
            if (!UsesStairTruck(ModeFor(aircraft)))
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
            if (!UsesStairTruck(ModeFor(aircraft)))
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
        /// once it has arrived. At a bridged gate the same moves are drawn inside the tunnel; at
        /// a remote stand their terminal end is the apron bus rather than the terminal wall.
        /// </summary>
        public static void Moves(FleetAircraft aircraft, double nowSeconds, List<PassengerMove> into,
            double lookBackSeconds = 180, PlayerBaseLevel baseLevel = PlayerBaseLevel.Starter)
        {
            into?.Clear();
            var mode = ModeFor(aircraft);
            if (into == null || mode == BoardingMode.None)
                return;

            var w = WindowsFor(aircraft, mode, baseLevel);
            if (w.Arrived > 0)
                AddWindow(aircraft, into, false, w.DeplaneStart, w.Interval, w.Arrived, nowSeconds, lookBackSeconds);
            if (w.Boarding > 0)
                AddWindow(aircraft, into, true, w.BoardStart, w.BoardInterval, w.Boarding, nowSeconds, lookBackSeconds);
        }

        /// <summary>Extra time the door stays open after the last passenger off, for cleaning crew.</summary>
        public const double DoorOpenAfterDeplaningSeconds = 60;
        /// <summary>The crew opens the door a little before the first boarder reaches it.</summary>
        public const double DoorOpenBeforeBoardingSeconds = 30;

        /// <summary>
        /// Whether a parked aircraft's passenger door is open because people are using it: from
        /// the door opening until shortly after the last arrival is off, and from shortly before
        /// boarding until the door closes for the push. Between rotations, overnight, and on an
        /// aircraft with no passengers to move, the door stays shut, as on a real apron.
        /// </summary>
        public static bool PassengersAtDoor(FleetAircraft aircraft, double nowSeconds,
            PlayerBaseLevel baseLevel = PlayerBaseLevel.Starter)
        {
            if (aircraft == null || aircraft.State != FleetState.AtStand)
                return false;
            var w = WindowsFor(aircraft, ModeFor(aircraft), baseLevel);
            if (w.Arrived > 0 && nowSeconds >= w.DoorsOpen
                              && nowSeconds < w.DeplaneEnd + DoorOpenAfterDeplaningSeconds)
                return true;
            return w.Boarding > 0 && nowSeconds >= w.BoardStart - DoorOpenBeforeBoardingSeconds;
        }

        private readonly struct Windows
        {
            public Windows(double interval, double doorsOpen, int arrived, double deplaneStart, double deplaneEnd,
                int boarding, double boardStart, double boardInterval)
            {
                Interval = interval;
                DoorsOpen = doorsOpen;
                Arrived = arrived;
                DeplaneStart = deplaneStart;
                DeplaneEnd = deplaneEnd;
                Boarding = boarding;
                BoardStart = boardStart;
                BoardInterval = boardInterval;
            }

            public double Interval { get; }
            public double DoorsOpen { get; }
            public int Arrived { get; }
            public double DeplaneStart { get; }
            public double DeplaneEnd { get; }
            public int Boarding { get; }
            public double BoardStart { get; }
            public double BoardInterval { get; }
        }

        private static Windows WindowsFor(FleetAircraft aircraft, BoardingMode mode, PlayerBaseLevel baseLevel)
        {
            var interval = AirlineOperations.NeedsTerminalGate(aircraft.Type) ? JetIntervalSeconds : TurbopropIntervalSeconds;
            var parkedAt = aircraft.StateStartedAt.ElapsedSeconds;
            var doorsOpen = parkedAt + (mode == BoardingMode.Aerobridge
                ? AerobridgeTimeline.DoorsOpenAfterParkSeconds
                : EngineStartSequence.DoorsOpenAfterSeconds);

            // Deplaning: everyone who flew in, once the door is open.
            var deplaneStart = doorsOpen + DeplaneAfterDoorsSeconds;
            var deplaneEnd = doorsOpen;
            var arrived = 0;
            if (aircraft.CompletedTrips > 0)
            {
                arrived = PassengerCount(aircraft.CompletedTrips - 1, aircraft);
                deplaneEnd = deplaneStart + arrived * interval;
            }

            // Boarding: finish before the door closes for the push.
            if (aircraft.Scheduled is not { Cancelled: false } departure)
                return new Windows(interval, doorsOpen, arrived, deplaneStart, deplaneEnd, 0, 0, interval);
            var passengers = PassengerCount(aircraft);
            var doorsClose = departure.DepartAt.ElapsedSeconds - (UsesStairTruck(mode)
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
            return new Windows(interval, doorsOpen, arrived, deplaneStart, deplaneEnd, passengers, boardStart, boardInterval);
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
