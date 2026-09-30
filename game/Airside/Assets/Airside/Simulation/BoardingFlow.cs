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
        /// <summary>First passenger steps out once the door (or airstair) has finished opening.</summary>
        public const double DeplaneAfterDoorsSeconds = 12;

        // Stair truck: drives up after the engines are off, leaves before the push (ADR 0114).
        public const double StairTruckDockAfterParkSeconds = 40;
        public const double StairTruckMoveSeconds = 40;
        // Door shut, then the truck pulls back, on the departure countdown (ADR 0177).
        public const double StairTruckDoorsCloseBeforePushSeconds = DepartureCountdown.DoorsClosedBeforeSeconds;
        public const double StairTruckLeaveBeforePushSeconds = DepartureCountdown.EquipmentAwayBeforeSeconds;

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
            // A freighter carries cargo, not people (ADR 0194).
            if (aircraft == null || aircraft.IsFreighter)
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
            if (DepartureCountdown.For(aircraft) is { } countdown)
                docked = Math.Min(docked, 1f - Ramp((nowSeconds - countdown.EquipmentAway) / StairTruckMoveSeconds));

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
            if (DepartureCountdown.For(aircraft) is { } countdown)
                return nowSeconds < countdown.DoorsClosed;
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
        /// How far open (0 shut … 1 open) the passenger door is, moving over the type's door
        /// time: it opens once the aircraft is ready to deplane, shuts once the cleaners are done,
        /// reopens shortly before boarding and is shut on the departure countdown (ADR 0177).
        /// </summary>
        public static float PassengerDoorOpen(FleetAircraft aircraft, double nowSeconds,
            PlayerBaseLevel baseLevel = PlayerBaseLevel.Starter)
        {
            if (aircraft == null || aircraft.State != FleetState.AtStand)
                return 0f;
            var mode = ModeFor(aircraft);
            if (mode == BoardingMode.None)
                return 0f;
            var w = WindowsFor(aircraft, mode, baseLevel);
            var seconds = DepartureCountdown.DoorSeconds(aircraft);
            var open = 0f;
            if (w.Arrived > 0)
                open = DepartureCountdown.Open(nowSeconds, w.DoorsOpen,
                    w.DeplaneEnd + DoorOpenAfterDeplaningSeconds + seconds, seconds);
            if (w.Boarding > 0 && DepartureCountdown.For(aircraft) is { } countdown)
                open = Math.Max(open, DepartureCountdown.Open(nowSeconds, w.BoardStart - DoorOpenBeforeBoardingSeconds,
                    countdown.DoorsClosed, seconds));
            return open;
        }

        /// <summary>
        /// How far open the hold doors are: for unloading after arrival, and for loading until
        /// the hold is shut on the departure countdown (after the player's Baggage stage).
        /// </summary>
        public static float CargoDoorOpen(FleetAircraft aircraft, double nowSeconds)
        {
            if (aircraft == null || aircraft.State != FleetState.AtStand)
                return 0f;
            var parked = (double)aircraft.StateStartedAt.ElapsedSeconds;
            var seconds = DepartureCountdown.CargoDoorSeconds;
            var open = 0f;
            if (aircraft.CompletedTrips > 0)
                open = DepartureCountdown.Open(nowSeconds, parked + CargoOpenAfterParkSeconds,
                    parked + CargoOpenAfterParkSeconds + UnloadSeconds, seconds);
            if (DepartureCountdown.For(aircraft) is { } countdown)
            {
                var loadFrom = aircraft.Airline.IsPlayer
                    ? DepartureCountdown.BaggageStageStart(aircraft) - 20
                    : countdown.CargoClosed - LoadSeconds;
                open = Math.Max(open, DepartureCountdown.Open(nowSeconds, loadFrom, countdown.CargoClosed, seconds));
            }

            return open;
        }

        /// <summary>Hold doors open once the engines are off, for this long to unload.</summary>
        public const double CargoOpenAfterParkSeconds = 60;
        public const double UnloadSeconds = 6 * 60;
        /// <summary>An AI aircraft's hold is open this long for loading before it is shut.</summary>
        public const double LoadSeconds = 9 * 60;

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
            // The last boarder is aboard before the headcount and the door starting to close.
            var countdown = DepartureCountdown.For(aircraft).Value;
            var doorsClose = countdown.DoorsClosed - DepartureCountdown.DoorSeconds(aircraft) - DepartureCountdown.HeadcountSeconds;
            double boardStart;
            var boardInterval = interval;
            if (aircraft.Airline.IsPlayer)
            {
                // The player's Boarding prep stage is the window.
                var boardingSeconds = DeparturePrep.BoardingSecondsFor(aircraft.Type, baseLevel);
                boardStart = DeparturePrep.ReadyAtSeconds(aircraft) - boardingSeconds;
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
            if (aircraft.IsFreighter)
                return 0;
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
