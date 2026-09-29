using System;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// The last minutes before a departure, in the order a real ramp runs them (ADR 0177).
    /// Every stand type — aerobridge, stair truck, integral airstair — and the engines, beacon,
    /// boarding and doors take their times from here, so nothing can happen out of order:
    ///
    /// <code>
    ///   T-5:00  hold door closed, bags and train done (a player's: once the Baggage stage ends)
    ///   T-4:00  a player's prep, and so boarding, is complete; the last walkers reach the door
    ///   T-2:30  headcount done, passenger door closed
    ///   T-2:10  aerobridge or stair truck pulls back (clear by T-1:10)
    ///   T-1:50  turboprop: beacon on, No.2 then No.1 start (running by T-0:25)
    ///   T-1:00  jet: beacon on once the bridge or stairs are clear, tug connected
    ///   T-0     push; a jet starts No.2 then No.1 during the push
    /// </code>
    ///
    /// When a player's prep finishes late the steps close up behind it rather than overlap:
    /// the door never shuts on a passenger, and no engine starts with a door open or a bridge
    /// on. Presentation only — it never changes when the aircraft actually pushes.
    /// </summary>
    public static class DepartureCountdown
    {
        /// <summary>
        /// A player's whole prep, boarding included, is done this long before the push: time for
        /// the last walkers, the headcount, the door and the bridge before the beacon.
        /// </summary>
        public const double PrepEndsBeforeSeconds = 240;

        public const double CargoClosedBeforeSeconds = 300;
        public const double DoorsClosedBeforeSeconds = 150;
        public const double EquipmentAwayBeforeSeconds = 130;
        public const double TurbopropBeaconBeforeSeconds = 110;
        public const double TurbopropRightStartBeforeSeconds = 95;
        public const double TurbopropLeftStartBeforeSeconds = 55;
        public const double JetBeaconBeforeSeconds = 60;
        public const double JetRightStartAfterPushSeconds = 15;
        public const double JetLeftStartAfterPushSeconds = 45;

        /// <summary>Headcount and paperwork between the last passenger aboard and the door closing.</summary>
        public const double HeadcountSeconds = 20;

        /// <summary>How long a door takes to move, open or shut, seconds.</summary>
        public const double PassengerDoorSeconds = 8;
        public const double AirstairSeconds = 10;
        public const double CargoDoorSeconds = 6;

        public readonly struct Times
        {
            public Times(double push, double cargoClosed, double doorsClosed, double equipmentAway, double beaconOn,
                double rightStart, double leftStart)
            {
                Push = push;
                CargoClosed = cargoClosed;
                DoorsClosed = doorsClosed;
                EquipmentAway = equipmentAway;
                BeaconOn = beaconOn;
                RightStart = rightStart;
                LeftStart = leftStart;
            }

            /// <summary>The booked push, in game seconds.</summary>
            public double Push { get; }

            /// <summary>The hold door is fully shut.</summary>
            public double CargoClosed { get; }

            /// <summary>The passenger door is fully shut (it starts closing a door-time earlier).</summary>
            public double DoorsClosed { get; }

            /// <summary>The aerobridge or stair truck starts to pull back.</summary>
            public double EquipmentAway { get; }

            public double BeaconOn { get; }

            /// <summary>No.2 (right) engine start; after the push for a jet.</summary>
            public double RightStart { get; }

            public double LeftStart { get; }
        }

        /// <summary>The countdown for a booked departure; null when there is none (or it was cancelled).</summary>
        public static Times? For(FleetAircraft aircraft)
        {
            if (aircraft?.Scheduled is not { Cancelled: false } departure)
                return null;
            var push = (double)departure.DepartAt.ElapsedSeconds;
            var jet = AirlineOperations.NeedsTerminalGate(aircraft.Type);

            // A player's boarding ends when prep does, but its last passengers are still walking
            // out (their starts are spread to half a walk before the stage ends): the door waits
            // for them, and everything after it keeps its order.
            var lastAboard = aircraft.Airline.IsPlayer
                ? DeparturePrep.ReadyAtSeconds(aircraft) + BoardingFlow.WalkBudgetSeconds * 0.5
                : 0.0;
            var doors = Math.Max(push - DoorsClosedBeforeSeconds, lastAboard + HeadcountSeconds + DoorSeconds(aircraft));
            var away = Math.Max(push - EquipmentAwayBeforeSeconds, doors + 15);
            // The beacon waits until the bridge or stair truck is actually clear of the aircraft.
            var mode = BoardingFlow.ModeFor(aircraft);
            var clear = mode == BoardingMode.Aerobridge ? away + AerobridgeTimeline.RetractSeconds
                : BoardingFlow.UsesStairTruck(mode) ? away + BoardingFlow.StairTruckMoveSeconds
                : doors;
            var beacon = Math.Max(push - (jet ? JetBeaconBeforeSeconds : TurbopropBeaconBeforeSeconds), Math.Max(doors + 10, clear));
            double right, left;
            if (jet)
            {
                right = push + JetRightStartAfterPushSeconds;
                left = push + JetLeftStartAfterPushSeconds;
            }
            else
            {
                right = Math.Max(push - TurbopropRightStartBeforeSeconds, Math.Max(beacon, away) + 10);
                left = Math.Max(push - TurbopropLeftStartBeforeSeconds, right + 30);
            }

            var cargo = Math.Min(push - CargoClosedBeforeSeconds, doors);
            if (aircraft.Airline.IsPlayer && aircraft.Scheduled.HasValue)
            {
                // The hold shuts once the baggage stage is done, not before.
                var baggageEnd = BaggageStageEnd(aircraft);
                cargo = Math.Max(Math.Min(cargo, baggageEnd + 20), baggageEnd + 5);
            }

            return new Times(push, cargo, doors, away, beacon, right, left);
        }

        /// <summary>How long this type's passenger door takes to open or shut.</summary>
        public static double DoorSeconds(FleetAircraft aircraft) =>
            aircraft != null && !AirlineOperations.NeedsTerminalGate(aircraft.Type) ? AirstairSeconds : PassengerDoorSeconds;

        /// <summary>When the player's Baggage stage ends, in game seconds.</summary>
        public static double BaggageStageEnd(FleetAircraft aircraft)
        {
            var level = aircraft.BaseLevel;
            var ready = DeparturePrep.ReadyAtSeconds(aircraft);
            return ready - DeparturePrep.StageSecondsFor(aircraft.Type, DeparturePrepStage.Boarding, level);
        }

        /// <summary>When the player's Baggage stage starts, in game seconds.</summary>
        public static double BaggageStageStart(FleetAircraft aircraft) =>
            BaggageStageEnd(aircraft) - DeparturePrep.StageSecondsFor(aircraft.Type, DeparturePrepStage.Baggage, aircraft.BaseLevel);

        /// <summary>
        /// 0 shut … 1 open for a door that is open from <paramref name="openAt"/> (starting to
        /// move then) until it is fully shut at <paramref name="closedAt"/>, taking
        /// <paramref name="seconds"/> each way. Eased at both ends of each movement.
        /// </summary>
        public static float Open(double now, double openAt, double closedAt, double seconds)
        {
            if (closedAt <= openAt)
                return 0f;
            var opening = Smooth((now - openAt) / seconds);
            var closing = Smooth((now - (closedAt - seconds)) / seconds);
            return Math.Max(0f, Math.Min(opening, 1f - closing));
        }

        private static float Smooth(double t) => t <= 0 ? 0f : t >= 1 ? 1f : (float)(t * t * (3 - 2 * t));
    }
}
