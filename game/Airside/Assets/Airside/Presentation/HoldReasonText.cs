using System;
using System.Collections.Generic;
using Airside.Domain;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>
    /// Plain-language wording for <see cref="HoldReason"/> (ADR 0124): a sentence for cards and
    /// boards ("Holding short 23 — QFA 737-8 landing, clear in about 1 min") and a two-word tag
    /// for field labels ("hold · 737 landing"). Presentation only.
    /// </summary>
    public static class HoldReasonText
    {
        /// <summary>The sentence, or empty when the aircraft is not held by anything.</summary>
        public static string Long(FleetAircraft aircraft, HoldReason reason, SimulationTime now, AirlineClock clock = null)
        {
            clock ??= AirlineClock.Default;
            var rwy = reason.Runway.HasValue ? RunwayWeather.Label(reason.Runway.Value) : "the runway";
            var who = Name(reason.Blocker);
            var soon = Soon(reason.Until, now);
            var departing = aircraft != null && aircraft.State is FleetState.HoldingShort or FleetState.AtStand;
            switch (reason.Kind)
            {
                case HoldKind.Cancelled:
                    return "Cancelled. The airline will rebook it";
                case HoldKind.Curfew:
                    return $"Curfew: Adelaide opens again at {Time(reason.Until, clock)}";
                case HoldKind.Turnaround:
                    return $"Turnaround: {Lower(reason.Detail)}";
                case HoldKind.ApronBusy:
                    return $"Waiting to push: {Names(reason.Others)} already taxiing out{soon}";
                case HoldKind.LeadInBlocked:
                    return reason.Blocker != null
                        ? $"Waiting to push: {who} is on the lead-in"
                        : "Waiting to push: the lead-in is in use";
                case HoldKind.TaxiwayBlocked:
                    if (aircraft?.State == FleetState.AtStand)
                        return reason.Blocker != null
                            ? $"Waiting to push: {who} is on the taxi route"
                            : "Waiting to push: traffic on the taxi route";
                    if (aircraft?.State == FleetState.AwaitingStand)
                        return reason.Blocker != null
                            ? $"Holding at the exit: {who} is on the taxi route"
                            : "Holding at the exit: traffic on the taxi route";
                    return "Holding: traffic at the runway exit";
                case HoldKind.RunwayOccupied:
                    return $"{Where(departing, rwy)}: {who} {Movement(reason.Blocker)}{soon}";
                case HoldKind.WakeSeparation:
                    return reason.Blocker != null
                        ? $"{Where(departing, rwy)}: spacing behind {who}{soon}"
                        : $"{Where(departing, rwy)}: runway spacing{soon}";
                case HoldKind.GroundStop:
                    return "Ground stop: storm over the field";
                case HoldKind.Queued:
                    return departing
                        ? $"Number {reason.Position} for {rwy}, behind {who}"
                        : $"Number {reason.Position} to land on {rwy}, behind {who}";
                case HoldKind.ArrivalFirst:
                    return $"Holding short {rwy}: {who} lands first";
                case HoldKind.DepartureFirst:
                    return $"Holding for {rwy}: {who} goes first";
                case HoldKind.NoStandFree:
                    return $"Waiting for a stand: {reason.Detail}";
                case HoldKind.ChooseStand:
                    return $"Choose a stand, or the tower picks one at {Time(reason.Until, clock)}";
                case HoldKind.HeldAirborne:
                    return $"Holding in the air: {reason.Detail} until {Time(reason.Until, clock)}";
                case HoldKind.InCheck:
                    return $"In maintenance until {Time(reason.Until, clock)}";
                case HoldKind.CrossingRunway:
                    return reason.Blocker != null
                        ? $"{Where(departing, rwy)}: {who} is crossing"
                        : $"Waiting to taxi: runway {(string.IsNullOrEmpty(reason.Detail) ? "crossing" : reason.Detail)} is busy where you cross";
                default:
                    return string.Empty;
            }
        }

        /// <summary>A two-word tag for field labels and tight rows, or empty.</summary>
        public static string Short(HoldReason reason) => reason.Kind switch
        {
            HoldKind.Cancelled => "cancelled",
            HoldKind.Curfew => "curfew",
            HoldKind.Turnaround => Lower(reason.Detail),
            HoldKind.ApronBusy => "wait · apron busy",
            HoldKind.LeadInBlocked => "wait · lead-in",
            HoldKind.TaxiwayBlocked => "wait · traffic",
            HoldKind.RunwayOccupied => $"hold · {ShortType(reason.Blocker)} {Movement(reason.Blocker)}",
            HoldKind.WakeSeparation => "hold · spacing",
            HoldKind.GroundStop => "hold · storm",
            HoldKind.Queued => $"hold · no. {reason.Position}",
            HoldKind.ArrivalFirst => $"hold · {ShortType(reason.Blocker)} landing",
            HoldKind.DepartureFirst => "hold · departure",
            HoldKind.NoStandFree => "no stand free",
            HoldKind.ChooseStand => "choose stand",
            HoldKind.HeldAirborne => "holding in air",
            HoldKind.InCheck => "in check",
            HoldKind.CrossingRunway => reason.Blocker != null ? "hold · crossing" : "wait · crossing",
            _ => string.Empty
        };

        private static string Where(bool departing, string rwy) => departing ? $"Holding short {rwy}" : $"Holding for {rwy}";

        private static string Movement(FleetAircraft blocker) => blocker?.State switch
        {
            FleetState.Landing => "landing",
            FleetState.TakingOff => "departing",
            _ => "on the runway"
        };

        /// <summary>"QFA412 (737-8)": the flight number the boards show, plus the type.</summary>
        public static string Name(FleetAircraft aircraft) =>
            aircraft == null ? "traffic" : $"{FlightNumber.OrRegistration(aircraft)} ({ShortType(aircraft)})";

        private static string Names(IReadOnlyList<FleetAircraft> aircraft)
        {
            if (aircraft == null || aircraft.Count == 0)
                return "departures";
            var names = new List<string>();
            foreach (var one in aircraft)
                names.Add(FlightNumber.OrRegistration(one));
            return string.Join(" and ", names);
        }

        public static string ShortType(FleetAircraft aircraft)
        {
            var name = aircraft?.Type?.Name ?? string.Empty;
            // "Boeing 737-8" → "737-8", "Saab 340B" → "340B", "Dash 8-400" → "Dash 8".
            if (name.StartsWith("Dash", StringComparison.Ordinal))
                return "Dash 8";
            var space = name.LastIndexOf(' ');
            return space > 0 ? name.Substring(space + 1) : name;
        }

        private static string Soon(SimulationTime? until, SimulationTime now)
        {
            if (!until.HasValue)
                return string.Empty;
            var seconds = until.Value.ElapsedSeconds - now.ElapsedSeconds;
            if (seconds <= 0)
                return string.Empty;
            return seconds < 60 ? $", clear in {seconds} s" : $", clear in about {(seconds + 30) / 60} min";
        }

        private static string Time(SimulationTime? at, AirlineClock clock) =>
            at.HasValue ? clock.TimeText(at.Value) : "soon";

        private static string Lower(string text) =>
            string.IsNullOrEmpty(text) ? string.Empty : char.ToLowerInvariant(text[0]) + text.Substring(1);
    }
}
