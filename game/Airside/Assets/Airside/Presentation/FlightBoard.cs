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
                    or FleetState.AwaitingStand or FleetState.TaxiIn
                    => $"{code} → {HomeCode}",
                _ => aircraft.Stand.Value.Length > 0 ? $"Stand {aircraft.Stand.Value}" : HomeCode
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
                FleetState.Landing => "Landing",
                FleetState.AwaitingStand => "Needs stand",
                FleetState.TaxiIn => "Taxi in",
                _ => aircraft.State.ToString()
            };
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
    }
}
