using System;
using System.Collections.Generic;
using Airside.Domain;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>
    /// Pure helpers for the Dev Tools panel (Bailey playtest list): fleet lines,
    /// next-event label and auto-schedule delay. Presentation only — callers still
    /// issue commands through <see cref="AirlineOperations"/>.
    /// </summary>
    public static class DevTools
    {
        /// <summary>
        /// First departure lead used by soak / auto-schedule so every session covers
        /// a full engine start early.
        /// </summary>
        public const long FirstAutoDepartureLeadSeconds = 4 * 60;

        /// <summary>Later auto-schedule upper bound (exclusive), matching soak.</summary>
        public const int LaterAutoDepartureLeadMaxSeconds = 1800;

        public static string FleetLine(FleetAircraft aircraft)
        {
            if (aircraft == null)
                return string.Empty;

            var dest = aircraft.CurrentDestination ?? aircraft.Scheduled?.Destination;
            var destText = dest.HasValue ? dest.Value.Code : "—";
            // Stand is default (null Value) whenever the aircraft is off its stand, so reading
            // Value.Length threw and the Dev Tools list broke as soon as anything departed.
            var stand = string.IsNullOrEmpty(aircraft.Stand.Value) ? "—" : aircraft.Stand.Value;
            return $"{aircraft.Registration}  ·  {aircraft.State}  ·  {destText}  ·  stand {stand}";
        }

        public static string NextEventLabel(SimulationTime? next, Func<SimulationTime, string> clockText)
        {
            if (clockText == null)
                return string.Empty;
            if (!next.HasValue)
                return "Next event: waiting on you (stand / schedule)";
            return $"Next event: {clockText(next.Value)}";
        }

        /// <summary>
        /// Delay before an auto-scheduled departure. First trip = 4 min; later trips
        /// use <paramref name="rollInclusive"/> clamped into
        /// [MinimumDepartureLeadSeconds, LaterAutoDepartureLeadMaxSeconds).
        /// </summary>
        public static long AutoScheduleDelaySeconds(int completedTrips, int rollInclusive)
        {
            if (completedTrips <= 0)
                return FirstAutoDepartureLeadSeconds;

            var min = (int)EngineStartSequence.MinimumDepartureLeadSeconds;
            var maxExclusive = LaterAutoDepartureLeadMaxSeconds;
            if (maxExclusive <= min)
                return min;
            var span = maxExclusive - min;
            var offset = rollInclusive % span;
            if (offset < 0)
                offset += span;
            return min + offset;
        }

        public static int CountIdleAtStand(IEnumerable<FleetAircraft> fleet)
        {
            if (fleet == null)
                return 0;
            var count = 0;
            foreach (var aircraft in fleet)
            {
                if (aircraft != null && aircraft.State == FleetState.AtStand && !aircraft.Scheduled.HasValue)
                    count++;
            }

            return count;
        }

        public static int CountAwaitingStand(IEnumerable<FleetAircraft> fleet)
        {
            if (fleet == null)
                return 0;
            var count = 0;
            foreach (var aircraft in fleet)
            {
                if (aircraft != null && aircraft.State == FleetState.AwaitingStand)
                    count++;
            }

            return count;
        }
    }
}
