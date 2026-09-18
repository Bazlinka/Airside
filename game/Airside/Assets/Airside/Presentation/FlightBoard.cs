using System;
using System.Collections.Generic;
using Airside.Domain;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>
    /// Pure helpers for the Flights board: sort key, route label and phase chip.
    /// Presentation only — never decides schedules or reservations.
    /// </summary>
    public static class FlightBoard
    {
        public const string HomeCode = "ADL";

        /// <summary>
        /// Next interesting clock time for sorting (depart / phase end / phase start).
        /// Lower values appear first so the board reads as a live timetable.
        /// </summary>
        public static long SortKeySeconds(FleetAircraft aircraft)
        {
            if (aircraft == null)
                return long.MaxValue;

            // Idle parked aircraft sit at the bottom of a timetable.
            if (aircraft.State == FleetState.AtStand && !aircraft.Scheduled.HasValue)
                return long.MaxValue;

            if (aircraft.State == FleetState.AtStand && aircraft.Scheduled.HasValue)
                return aircraft.Scheduled.Value.DepartAt.ElapsedSeconds;

            if (aircraft.StateEndsAt.HasValue)
                return aircraft.StateEndsAt.Value.ElapsedSeconds;

            return aircraft.StateStartedAt.ElapsedSeconds;
        }

        public static string RouteText(FleetAircraft aircraft)
        {
            if (aircraft == null)
                return string.Empty;

            var dest = aircraft.CurrentDestination ?? aircraft.Scheduled?.Destination;
            var code = dest?.Code ?? "—";

            return aircraft.State switch
            {
                FleetState.AtStand when aircraft.Scheduled.HasValue => $"{HomeCode} → {code}",
                FleetState.TaxiOut or FleetState.HoldingShort or FleetState.TakingOff or FleetState.Outbound
                    => $"{HomeCode} → {code}",
                FleetState.AtDestination => $"At {code}",
                FleetState.Inbound or FleetState.HoldingForLanding or FleetState.Landing
                    or FleetState.GoAround or FleetState.AwaitingStand or FleetState.TaxiIn
                    => $"{code} → {HomeCode}",
                _ => aircraft.Stand.Value.Length > 0 ? StandNames.Display(aircraft.Stand) : HomeCode
            };
        }

        public static string PhaseLabel(FleetAircraft aircraft)
        {
            if (aircraft == null)
                return string.Empty;

            return aircraft.State switch
            {
                FleetState.AtStand when aircraft.Scheduled.HasValue => "Scheduled",
                FleetState.AtStand => "On stand",
                FleetState.TaxiOut => "Taxi out",
                FleetState.HoldingShort => "Holding short",
                FleetState.TakingOff => "Taking off",
                FleetState.Outbound => "En route",
                FleetState.AtDestination => "Turnaround",
                FleetState.Inbound => "Returning",
                FleetState.HoldingForLanding => "In circuit",
                FleetState.GoAround => "Go-around",
                FleetState.Landing => "Landing",
                FleetState.AwaitingStand => "Needs stand",
                FleetState.TaxiIn => "Taxi in",
                _ => aircraft.State.ToString()
            };
        }

        /// <summary>Clock-aware status: prep percent while turning around, else a gate-hold or phase chip.</summary>
        public static string PhaseLabel(FleetAircraft aircraft, SimulationTime now)
        {
            if (DepartureDelayMinutes(aircraft, now) > 0)
                return "Gate hold";
            if (aircraft != null && aircraft.Airline.IsPlayer && aircraft.State == FleetState.AtStand
                && aircraft.Scheduled.HasValue)
            {
                var prep = DeparturePrep.For(aircraft, now);
                return prep.Ready ? "Ready" : prep.Label;
            }

            return PhaseLabel(aircraft);
        }

        /// <summary>Whole minutes late, once a scheduled aircraft is at least a minute overdue.</summary>
        public static long DepartureDelayMinutes(FleetAircraft aircraft, SimulationTime now)
        {
            if (aircraft == null || aircraft.State != FleetState.AtStand || !aircraft.Scheduled.HasValue)
                return 0;
            var seconds = now.ElapsedSeconds - aircraft.Scheduled.Value.DepartAt.ElapsedSeconds;
            return seconds >= 60 ? seconds / 60 : 0;
        }

        public static string TimeLabel(FleetAircraft aircraft, Func<SimulationTime, string> clockText)
        {
            if (aircraft == null || clockText == null)
                return string.Empty;

            if (aircraft.State == FleetState.AtStand && aircraft.Scheduled.HasValue)
                return clockText(aircraft.Scheduled.Value.DepartAt);

            if (aircraft.StateEndsAt.HasValue)
                return clockText(aircraft.StateEndsAt.Value);

            return clockText(aircraft.StateStartedAt);
        }

        /// <summary>What the board's NEXT time represents, so a clock value is never ambiguous.</summary>
        public static string TimeMeaning(FleetAircraft aircraft)
        {
            if (aircraft == null)
                return string.Empty;
            return TimeMeaning(aircraft.State, aircraft.Scheduled.HasValue);
        }

        /// <summary>Pure milestone label used by the board and its layout tests.</summary>
        public static string TimeMeaning(FleetState state, bool hasScheduledDeparture = false)
        {
            return state switch
            {
                FleetState.AtStand when hasScheduledDeparture => "DEPARTS",
                FleetState.AtStand => "SINCE",
                FleetState.TaxiOut => "AT HOLD",
                FleetState.HoldingShort => "HOLD SINCE",
                FleetState.TakingOff => "AIRBORNE",
                FleetState.Outbound => "ARRIVES",
                FleetState.AtDestination => "RETURNS",
                FleetState.Inbound => "IN CIRCUIT",
                FleetState.HoldingForLanding => "HOLD SINCE",
                FleetState.GoAround => "RE-SEQUENCE",
                FleetState.Landing => "CLEAR RWY",
                FleetState.AwaitingStand => "WAIT SINCE",
                FleetState.TaxiIn => "ON STAND",
                _ => "NEXT"
            };
        }

        public static string TimeMeaning(FleetAircraft aircraft, SimulationTime now)
        {
            var delay = DepartureDelayMinutes(aircraft, now);
            return delay > 0 ? $"LATE +{delay} MIN" : TimeMeaning(aircraft);
        }

        public static bool IsArrival(FleetAircraft aircraft) => aircraft != null && aircraft.State is
            FleetState.AtDestination or FleetState.Inbound or FleetState.HoldingForLanding or FleetState.GoAround
            or FleetState.Landing or FleetState.AwaitingStand or FleetState.TaxiIn;

        public static bool IsDeparture(FleetAircraft aircraft) => aircraft != null && !IsArrival(aircraft);

        public static string GateText(FleetAircraft aircraft)
        {
            if (aircraft == null)
                return "—";
            var stand = !string.IsNullOrEmpty(aircraft.Stand.Value) ? aircraft.Stand : aircraft.DepartureStand;
            return string.IsNullOrEmpty(stand.Value) ? "—" : AdelaideGround.StandLabel(stand).Replace("Gate ", "");
        }

        public static string ScheduledTime(FleetAircraft aircraft, Func<SimulationTime, string> clockText)
        {
            if (aircraft == null || clockText == null)
                return "—";
            if (aircraft.Scheduled.HasValue)
                return clockText(aircraft.Scheduled.Value.DepartAt);
            return clockText(aircraft.StateStartedAt);
        }

        public static string EstimatedTime(FleetAircraft aircraft, Func<SimulationTime, string> clockText)
        {
            if (aircraft == null || clockText == null)
                return "—";
            return aircraft.StateEndsAt.HasValue ? clockText(aircraft.StateEndsAt.Value) : "—";
        }

        /// <summary>Stable sort: next event time, then registration.</summary>
        public static void Sort(List<FleetAircraft> aircraft)
        {
            if (aircraft == null)
                return;
            aircraft.Sort((a, b) =>
            {
                var byTime = SortKeySeconds(a).CompareTo(SortKeySeconds(b));
                return byTime != 0
                    ? byTime
                    : string.CompareOrdinal(a?.Registration, b?.Registration);
            });
        }

        /// <summary>
        /// Stable split into YOUR AIRLINE then OTHER OPERATORS (<see cref="Ownership"/>), keeping
        /// each section in the order <see cref="Sort"/> gave it.
        /// </summary>
        public static void GroupByOwnership(List<FleetAircraft> aircraft)
        {
            var mine = new List<FleetAircraft>();
            var others = new List<FleetAircraft>();
            foreach (var a in aircraft)
                (a.Airline.IsPlayer ? mine : others).Add(a);
            aircraft.Clear();
            aircraft.AddRange(mine);
            aircraft.AddRange(others);
        }
    }
}
