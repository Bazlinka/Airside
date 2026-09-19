using System.Collections.Generic;
using System.Linq;
using Airside.Domain;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>The one primary action on the selected-aircraft card (ADR 0053).</summary>
    public enum AircraftHudAction
    {
        None,
        PlanFlight,
        ViewPlan,
        TrackFlight,
        AssignStand
    }

    /// <summary>
    /// One active career objective for the persistent HUD card. Presentation only —
    /// values are derived from simulation/career state, never stored.
    /// </summary>
    public readonly struct CareerObjective
    {
        public CareerObjective(string title, string progressText, float progress01, string nextLine,
            StatusSeverity nextSeverity)
        {
            Title = title ?? string.Empty;
            ProgressText = progressText ?? string.Empty;
            Progress01 = progress01;
            NextLine = nextLine ?? string.Empty;
            NextSeverity = nextSeverity;
        }

        public string Title { get; }
        public string ProgressText { get; }
        public float Progress01 { get; }
        public string NextLine { get; }
        public StatusSeverity NextSeverity { get; }
    }

    /// <summary>One player aircraft in the compact Operations list.</summary>
    public readonly struct OperationsRow
    {
        public OperationsRow(string registration, string route, string state, StatusSeverity severity, bool isPriority)
        {
            Registration = registration ?? string.Empty;
            Route = route ?? string.Empty;
            State = state ?? string.Empty;
            Severity = severity;
            IsPriority = isPriority;
        }

        public string Registration { get; }
        public string Route { get; }
        public string State { get; }
        public StatusSeverity Severity { get; }
        public bool IsPriority { get; }
    }

    /// <summary>
    /// Read-only HUD projections (ADR 0053): the current objective, compact player
    /// Operations rows and the single primary aircraft action. Decides nothing and stores
    /// nothing. Player aircraft only — AI traffic belongs on the full flights board.
    /// </summary>
    public static class OperationsSummary
    {
        /// <summary>
        /// The most urgent aircraft's situation; failing that, the active contract's progress;
        /// failing that, a quiet fleet-wide line. <paramref name="playerFleet"/> in any order —
        /// the worst severity wins.
        /// </summary>
        public static (string Text, StatusSeverity Severity) Line(
            IEnumerable<FleetAircraft> playerFleet, SimulationTime now, AirlineCareerState career = null,
            IReadOnlyList<RouteContractDefinition> marketOffers = null)
        {
            var objective = Objective(playerFleet, now, null, career, marketOffers);
            return (string.IsNullOrEmpty(objective.NextLine) ? objective.Title : objective.NextLine,
                objective.NextSeverity);
        }

        public static CareerObjective Objective(
            IEnumerable<FleetAircraft> playerFleet, SimulationTime now, AirlineClock clock = null,
            AirlineCareerState career = null, IReadOnlyList<RouteContractDefinition> marketOffers = null)
        {
            clock ??= AirlineClock.Default;
            var (next, nextSeverity) = NextAction(playerFleet, now, clock);

            if (career?.ActiveContract != null
                && career.TryFindDefinition(career.ActiveContract.DefinitionId, out var definition))
            {
                var done = career.ActiveContract.CompletedRotations;
                var required = definition.RequiredRotations;
                return new CareerObjective(
                    ProveTitle(definition),
                    $"{done} of {required} rotations complete",
                    required <= 0 ? 0f : done / (float)required,
                    next,
                    nextSeverity);
            }

            var offer = NextOffer(career, marketOffers);
            if (offer != null)
            {
                return new CareerObjective(
                    $"Accept a {PlaceName(offer.DestinationCode)} contract",
                    "No contract accepted yet",
                    0f,
                    $"Next: open Contracts for {offer.OriginCode} ↔ {offer.DestinationCode}",
                    StatusSeverity.Attention);
            }

            if (career != null)
            {
                return new CareerObjective(
                    "Keep the airline flying",
                    $"${career.Funds:N0} on hand · {career.Reliability}% reliability",
                    0f,
                    next,
                    nextSeverity);
            }

            return new CareerObjective("Keep the airline flying", string.Empty, 0f, next, nextSeverity);
        }

        public static void FillPlayerRows(IEnumerable<FleetAircraft> playerFleet, SimulationTime now,
            List<OperationsRow> into)
        {
            into.Clear();
            if (playerFleet == null)
                return;

            var priority = PriorityAircraft(playerFleet, now);
            foreach (var aircraft in playerFleet)
            {
                into.Add(new OperationsRow(
                    aircraft.Registration,
                    RouteLabel(aircraft),
                    CompactState(aircraft, now),
                    AircraftStatus.Severity(aircraft, now),
                    priority != null && ReferenceEquals(priority, aircraft)));
            }
        }

        public static int AvailableCount(IEnumerable<FleetAircraft> playerFleet)
        {
            if (playerFleet == null)
                return 0;
            var count = 0;
            foreach (var aircraft in playerFleet)
            {
                if (aircraft.State == FleetState.AtStand && !aircraft.Scheduled.HasValue)
                    count++;
            }

            return count;
        }

        public static AircraftHudAction PrimaryAction(FleetAircraft aircraft)
        {
            if (aircraft == null || !aircraft.Airline.IsPlayer)
                return AircraftHudAction.None;
            if (aircraft.State == FleetState.AwaitingStand)
                return AircraftHudAction.AssignStand;
            if (aircraft.State == FleetState.AtStand && !aircraft.Scheduled.HasValue)
                return AircraftHudAction.PlanFlight;
            if (aircraft.State == FleetState.AtStand && aircraft.Scheduled.HasValue)
                return AircraftHudAction.ViewPlan;
            return AircraftHudAction.TrackFlight;
        }

        public static string ActionLabel(AircraftHudAction action) => action switch
        {
            AircraftHudAction.PlanFlight => "Plan flight",
            AircraftHudAction.ViewPlan => "View plan",
            AircraftHudAction.TrackFlight => "Track flight",
            AircraftHudAction.AssignStand => "Assign stand",
            _ => string.Empty
        };

        public static string CompactState(FleetAircraft aircraft, SimulationTime now)
        {
            if (aircraft == null)
                return string.Empty;
            if (aircraft.State == FleetState.AtStand && aircraft.Scheduled.HasValue && aircraft.Airline.IsPlayer)
            {
                var prep = DeparturePrep.For(aircraft, now);
                return prep.Ready ? "Ready" : prep.Label;
            }

            return aircraft.State switch
            {
                FleetState.AtStand => "Available",
                FleetState.TaxiOut => "Taxiing",
                FleetState.HoldingShort => "Holding",
                FleetState.TakingOff => "Departing",
                FleetState.Outbound => "Departed",
                FleetState.AtDestination => "Away",
                FleetState.Inbound => "Inbound",
                FleetState.HoldingForLanding => "On final",
                FleetState.GoAround => "Go-around",
                FleetState.Landing => "Landing",
                FleetState.AwaitingStand => "Landed",
                FleetState.TaxiIn => "Landed",
                _ => aircraft.State.ToString()
            };
        }

        public static string ProveTitle(RouteContractDefinition definition)
        {
            if (definition == null)
                return "Current contract";
            return $"Prove the {PlaceName(definition.DestinationCode)} service";
        }

        public static string PlaceName(string code)
        {
            if (DestinationCatalogue.TryFind(code, out var destination) && !string.IsNullOrEmpty(destination.Name))
                return destination.Name;
            return string.IsNullOrEmpty(code) ? "route" : code;
        }

        private static string RouteLabel(FleetAircraft aircraft)
        {
            if (aircraft.Scheduled.HasValue)
                return aircraft.Scheduled.Value.Destination.Code;
            if (aircraft.CurrentDestination.HasValue)
                return aircraft.CurrentDestination.Value.Code;
            return "available";
        }

        private static (string Text, StatusSeverity Severity) NextAction(
            IEnumerable<FleetAircraft> playerFleet, SimulationTime now, AirlineClock clock)
        {
            if (playerFleet == null || !playerFleet.Any())
                return ("Next: add an aircraft in Fleet", StatusSeverity.Normal);

            var priority = PriorityAircraft(playerFleet, now);
            if (priority == null)
                return ("Next: plan a flight when an aircraft is free", StatusSeverity.Normal);

            var severity = AircraftStatus.Severity(priority, now);
            if (priority.State == FleetState.AwaitingStand)
                return ($"Next: {priority.Registration} needs a stand", StatusSeverity.Warning);
            if (priority.State == FleetState.AtStand && !priority.Scheduled.HasValue)
                return ($"Next: plan a flight for {priority.Registration}", StatusSeverity.Normal);
            if (priority.State == FleetState.AtStand && priority.Scheduled.HasValue)
            {
                var when = clock.TimeText(priority.Scheduled.Value.DepartAt);
                var prep = DeparturePrep.For(priority, now);
                var stage = prep.Ready ? "ready" : prep.Label;
                return ($"Next: {priority.Registration} departs {when} · {stage}",
                    severity == StatusSeverity.Normal ? StatusSeverity.Attention : severity);
            }

            var dest = RouteLabel(priority);
            return ($"Next: {priority.Registration} · {CompactState(priority, now)}" +
                    (dest == "available" ? string.Empty : $" · {dest}"),
                severity);
        }

        /// <summary>Worst severity, then soonest booked departure, then first idle aircraft.</summary>
        public static FleetAircraft PriorityAircraft(IEnumerable<FleetAircraft> playerFleet, SimulationTime now)
        {
            if (playerFleet == null)
                return null;

            FleetAircraft worst = null;
            var worstSeverity = StatusSeverity.Normal;
            foreach (var aircraft in playerFleet)
            {
                var severity = AircraftStatus.Severity(aircraft, now);
                if (severity > worstSeverity)
                {
                    worstSeverity = severity;
                    worst = aircraft;
                }
            }

            if (worst != null)
                return worst;

            FleetAircraft soonest = null;
            var soonestAt = long.MaxValue;
            foreach (var aircraft in playerFleet)
            {
                if (!aircraft.Scheduled.HasValue)
                    continue;
                var at = aircraft.Scheduled.Value.DepartAt.ElapsedSeconds;
                if (at < soonestAt)
                {
                    soonestAt = at;
                    soonest = aircraft;
                }
            }

            if (soonest != null)
                return soonest;

            foreach (var aircraft in playerFleet)
            {
                if (aircraft.State == FleetState.AtStand)
                    return aircraft;
            }

            return playerFleet.FirstOrDefault();
        }

        private static RouteContractDefinition NextOffer(AirlineCareerState career,
            IReadOnlyList<RouteContractDefinition> marketOffers)
        {
            if (career == null || career.ActiveContract != null)
                return null;
            if (marketOffers != null)
            {
                foreach (var offer in marketOffers)
                    if (!career.HasCompleted(offer.Id))
                        return offer;
            }

            foreach (var definition in RouteContractCatalogue.All)
            {
                if (career.HasCompleted(definition.Id) || career.Tier < definition.RequiredTier)
                    continue;
                return definition;
            }

            return null;
        }
    }
}
