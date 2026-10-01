using System;
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
        AssignStand,
        StartCheck
    }

    /// <summary>
    /// One active career objective for the persistent HUD card. Presentation only —
    /// values are derived from simulation/career state, never stored.
    /// </summary>
    public readonly struct CareerObjective
    {
        public CareerObjective(string title, string progressText, float progress01, string nextLine,
            StatusSeverity nextSeverity, string caption = null)
        {
            Caption = caption ?? string.Empty;
            Title = title ?? string.Empty;
            ProgressText = progressText ?? string.Empty;
            Progress01 = progress01;
            NextLine = nextLine ?? string.Empty;
            NextSeverity = nextSeverity;
        }

        /// <summary>"TOWARD DOMESTIC" — what the goal's stage earns. Empty without a career.</summary>
        public string Caption { get; }
        public string Title { get; }
        public string ProgressText { get; }
        public float Progress01 { get; }
        public string NextLine { get; }
        public StatusSeverity NextSeverity { get; }
    }

    /// <summary>One player aircraft in the compact Operations list.</summary>
    public readonly struct OperationsRow
    {
        public OperationsRow(string registration, string route, string state, StatusSeverity severity, bool isPriority,
            string flightLabel = null, string typeName = null, string routeText = null, string timeText = null,
            float progress01 = -1f, string progressText = null)
        {
            Registration = registration ?? string.Empty;
            Route = route ?? string.Empty;
            State = state ?? string.Empty;
            Severity = severity;
            IsPriority = isPriority;
            FlightLabel = string.IsNullOrEmpty(flightLabel) ? Registration : flightLabel;
            TypeName = typeName ?? string.Empty;
            RouteText = routeText ?? string.Empty;
            TimeText = timeText ?? string.Empty;
            Progress01 = progress01 < 0f ? -1f : progress01 > 1f ? 1f : progress01;
            ProgressText = progressText ?? string.Empty;
        }

        public string Registration { get; }
        public string Route { get; }
        public string State { get; }
        public StatusSeverity Severity { get; }
        public bool IsPriority { get; }

        /// <summary>The board's flight number (ZL3482-style), or the registration when nothing is booked.</summary>
        public string FlightLabel { get; }

        /// <summary>"ATR 42", "Boeing 737"… the airframe, so the tile says what is flying.</summary>
        public string TypeName { get; }

        /// <summary>"Adelaide → Kingscote", or the stand while parked with nothing booked.</summary>
        public string RouteText { get; }

        /// <summary>The time that matters now: "Departs 14:05", "ETA 15:20", "Check ends 18:00". Empty if none.</summary>
        public string TimeText { get; }

        /// <summary>How far through the current step (turnaround, flight leg, check); negative for none.</summary>
        public float Progress01 { get; }

        /// <summary>The progress bar's word, e.g. "Fuel 40%" or "62% of flight".</summary>
        public string ProgressText { get; }

        public bool HasProgress => Progress01 >= 0f;
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

        /// <summary>
        /// The career card: the pinned (or first open) roadmap goal — the single source of career
        /// direction (ADR 0121) — plus one concrete next action. Pass <paramref name="goal"/> from
        /// <see cref="AirlineOperations.PinnedCareerGoal"/> so outstation aircraft count too; without
        /// it the goal is derived from <paramref name="playerFleet"/> alone.
        /// </summary>
        public static CareerObjective Objective(
            IEnumerable<FleetAircraft> playerFleet, SimulationTime now, AirlineClock clock = null,
            AirlineCareerState career = null, IReadOnlyList<RouteContractDefinition> marketOffers = null,
            CareerGoalStatus? goal = null)
        {
            clock ??= AirlineClock.Default;
            var (next, nextSeverity) = NextAction(playerFleet, now, clock, career, marketOffers);
            if (career == null)
                return new CareerObjective("Keep the airline flying", string.Empty, 0f, next, nextSeverity);

            if (!goal.HasValue)
            {
                var owned = new List<AircraftType>();
                if (playerFleet != null)
                    foreach (var aircraft in playerFleet)
                        if (aircraft?.Type != null)
                            owned.Add(aircraft.Type);
                goal = CareerRoadmap.Pinned(career, owned, owned.Count);
            }

            var g = goal.Value;
            if (string.IsNullOrEmpty(g.Id))
                return new CareerObjective("Keep the airline flying",
                    $"${career.Funds:N0} on hand · {career.Reliability}% reliability", 0f, next, nextSeverity);
            var finale = career.FinaleReached;
            return new CareerObjective(
                finale ? "Established airline" : g.Title,
                finale ? "Every goal done. Keep flying" : g.ProgressText,
                finale ? 1f : g.Target <= 0 ? 0f : g.Progress / (float)g.Target,
                next, nextSeverity,
                finale ? "CAREER COMPLETE" : "TOWARD " + g.UnlocksLabel.ToUpperInvariant());
        }

        /// <summary>
        /// One row per player aircraft. With <paramref name="operations"/>, a held aircraft's state
        /// line says why it is waiting (ADR 0124) instead of only "Holding".
        /// </summary>
        public static void FillPlayerRows(IEnumerable<FleetAircraft> playerFleet, SimulationTime now,
            List<OperationsRow> into, AirlineOperations operations = null)
        {
            into.Clear();
            if (playerFleet == null)
                return;

            var priority = PriorityAircraft(playerFleet, now);
            foreach (var aircraft in playerFleet)
            {
                var state = CompactState(aircraft, now);
                if (operations != null)
                {
                    var reason = operations.Why(aircraft);
                    if (reason.IsHolding && reason.Kind != HoldKind.Turnaround)
                        state = HoldReasonText.Long(aircraft, reason, now, operations.Clock);
                }
                var detail = Detail(aircraft, now, operations?.Clock ?? AirlineClock.Default,
                    operations?.CareerState?.BaseLevel);
                into.Add(new OperationsRow(
                    aircraft.Registration,
                    RouteLabel(aircraft),
                    state,
                    AircraftStatus.Severity(aircraft, now),
                    priority != null && ReferenceEquals(priority, aircraft),
                    FlightNumber.ForAircraft(aircraft),
                    aircraft.Type.Name,
                    detail.Route, detail.Time, detail.Progress, detail.ProgressText));
            }
        }

        /// <summary>
        /// The extra lines on a flight tile: where it is going, the time to watch, and a progress bar
        /// for whatever it is doing now (turnaround, flight leg or check). Read-only.
        /// </summary>
        private static (string Route, string Time, float Progress, string ProgressText) Detail(
            FleetAircraft aircraft, SimulationTime now, AirlineClock clock, PlayerBaseLevel? baseLevel)
        {
            var place = FlightNumber.PlaceName(aircraft);
            var route = string.IsNullOrEmpty(place)
                ? StandNames.Display(aircraft.Stand)
                : FlightNumber.IsReturning(aircraft) ? place + " → Adelaide" : "Adelaide → " + place;

            var time = string.Empty;
            var progress = -1f;
            var progressText = string.Empty;

            if (aircraft.Airline.IsPlayer && Maintenance.InCheck(aircraft, now) && aircraft.CheckUntil.HasValue)
            {
                time = "Check ends " + clock.TimeText(aircraft.CheckUntil.Value);
                return (route, time, progress, progressText);
            }

            switch (aircraft.State)
            {
                case FleetState.AtStand when aircraft.Scheduled.HasValue:
                    var booked = aircraft.Scheduled.Value;
                    time = (booked.Cancelled ? "Cancelled " : "Departs ") + clock.TimeText(booked.DepartAt);
                    if (!booked.Cancelled && aircraft.Airline.IsPlayer)
                    {
                        var prep = DeparturePrep.For(aircraft, now, baseLevel ?? PlayerBaseLevel.Starter);
                        progress = prep.Ready ? 1f : (float)(prep.FuelProgress + prep.CateringProgress
                            + prep.BaggageProgress + prep.BoardingProgress) / 4f;
                        progressText = prep.Ready ? "Ready to push" : prep.Label;
                    }
                    break;
                case FleetState.Outbound:
                case FleetState.Inbound:
                    if (aircraft.StateEndsAt.HasValue)
                    {
                        time = "ETA " + clock.TimeText(aircraft.StateEndsAt.Value);
                        progress = (float)aircraft.StateProgress(now);
                        progressText = (int)(progress * 100f) + "% of flight";
                    }
                    break;
                case FleetState.AtDestination:
                    if (aircraft.StateEndsAt.HasValue)
                    {
                        time = "Leaves " + clock.TimeText(aircraft.StateEndsAt.Value);
                        progress = (float)aircraft.StateProgress(now);
                        progressText = "Turnaround";
                    }
                    break;
                case FleetState.AwaitingStand:
                    time = "Needs a stand";
                    break;
            }

            return (route, time, progress, progressText);
        }

        public static int AvailableCount(IEnumerable<FleetAircraft> playerFleet, SimulationTime now = default)
        {
            if (playerFleet == null)
                return 0;
            var count = 0;
            foreach (var aircraft in playerFleet)
            {
                if (aircraft.State == FleetState.AtStand && !aircraft.Scheduled.HasValue
                    && !Maintenance.InCheck(aircraft, now))
                    count++;
            }

            return count;
        }

        public static AircraftHudAction PrimaryAction(FleetAircraft aircraft, SimulationTime now = default)
        {
            if (aircraft == null || !aircraft.Airline.IsPlayer)
                return AircraftHudAction.None;
            if (aircraft.State == FleetState.AwaitingStand)
                return AircraftHudAction.AssignStand;
            if (aircraft.State == FleetState.AtStand && !aircraft.Scheduled.HasValue
                && Maintenance.IsOverdue(aircraft) && !Maintenance.InCheck(aircraft, now))
                return AircraftHudAction.StartCheck;
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
            AircraftHudAction.StartCheck => "Send for check",
            _ => string.Empty
        };

        public static string CompactState(FleetAircraft aircraft, SimulationTime now, AirlineClock clock = null)
        {
            if (aircraft == null)
                return string.Empty;
            if (aircraft.Airline.IsPlayer && Maintenance.InCheck(aircraft, now))
                return Maintenance.Status(aircraft, now, clock);
            if (aircraft.State == FleetState.AtStand && aircraft.Scheduled.HasValue && aircraft.Airline.IsPlayer)
            {
                var prep = DeparturePrep.For(aircraft, now);
                return prep.Ready ? "Ready" : prep.Label;
            }

            if (aircraft.State == FleetState.AtStand && aircraft.Airline.IsPlayer
                && (Maintenance.IsOverdue(aircraft) || Maintenance.IsDueSoon(aircraft)))
                return Maintenance.Status(aircraft, now, clock);

            return aircraft.State switch
            {
                FleetState.AtStand => "Available",
                FleetState.TaxiOut => "Taxiing",
                FleetState.HoldingShort => "Holding",
                FleetState.TakingOff => FlightBoard.PhaseLabel(aircraft, now),
                FleetState.Outbound => "Departed",
                FleetState.AtDestination => "Away",
                FleetState.Inbound => "Inbound",
                FleetState.HoldingForLanding => "On final",
                FleetState.GoAround => "Go-around",
                FleetState.Landing => FlightBoard.PhaseLabel(aircraft, now),
                FleetState.AwaitingStand => "Landed",
                FleetState.TaxiIn => "Taxiing",
                _ => aircraft.State.ToString()
            };
        }

        public static string ProveTitle(RouteContractDefinition definition)
        {
            if (definition == null)
                return "Current contract";
            return $"Fly the {PlaceName(definition.DestinationCode)} contract";
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

        /// <summary>
        /// One concrete next action for the objective card — a verb the player can do now,
        /// not a status readout. Priority: parking → prep → schedule/accept → follow/track → buy.
        /// </summary>
        private static (string Text, StatusSeverity Severity) NextAction(
            IEnumerable<FleetAircraft> playerFleet, SimulationTime now, AirlineClock clock,
            AirlineCareerState career = null, IReadOnlyList<RouteContractDefinition> marketOffers = null)
        {
            if (playerFleet == null || !playerFleet.Any())
                return ("Next: add an aircraft in Fleet", StatusSeverity.Normal);

            var fleet = playerFleet as IList<FleetAircraft> ?? playerFleet.ToList();
            var priority = PriorityAircraft(fleet, now);
            if (priority == null)
                return ("Next: plan a flight when an aircraft is free", StatusSeverity.Normal);

            var severity = AircraftStatus.Severity(priority, now);
            if (priority.State == FleetState.AwaitingStand)
                return ($"Next: assign a stand to {priority.Registration}", StatusSeverity.Warning);

            // Delivery inbound (purchase with no free stand) — do not say "track" like a line flight.
            if (priority.State == FleetState.Inbound && priority.CompletedTrips == 0
                && !priority.Scheduled.HasValue)
                return ($"Next: let {priority.Registration} park, then plan its first flight",
                    StatusSeverity.Attention);

            if (Maintenance.InCheck(priority, now))
                return ($"Next: {priority.Registration} is in its check until {clock.TimeText(priority.CheckUntil.Value)}",
                    StatusSeverity.Attention);

            if (priority.State == FleetState.AtStand && !priority.Scheduled.HasValue && Maintenance.IsOverdue(priority))
                return ($"Next: send {priority.Registration} for a check", StatusSeverity.Warning);

            if (priority.State == FleetState.AtStand && priority.Scheduled.HasValue)
            {
                var prep = DeparturePrep.For(priority, now, career?.BaseLevel ?? PlayerBaseLevel.Starter);
                if (!prep.Ready)
                {
                    var finish = prep.Stage switch
                    {
                        DeparturePrepStage.Fuel => "Finish fuelling",
                        DeparturePrepStage.Catering => "Finish catering",
                        DeparturePrepStage.Baggage => "Finish baggage",
                        DeparturePrepStage.Boarding => "Finish boarding",
                        _ => "Finish turnaround"
                    };
                    return ($"Next: {finish} on {priority.Registration}",
                        severity == StatusSeverity.Normal ? StatusSeverity.Attention : severity);
                }

                var when = clock.TimeText(priority.Scheduled.Value.DepartAt);
                return ($"Next: watch {priority.Registration} push back at {when}",
                    severity == StatusSeverity.Normal ? StatusSeverity.Attention : severity);
            }

            if (priority.State == FleetState.AtStand && !priority.Scheduled.HasValue)
            {
                if (career?.ActiveContract != null
                    && career.TryFindDefinition(career.ActiveContract.DefinitionId, out var active))
                {
                    return ($"Next: plan {priority.Registration} to {PlaceName(active.DestinationCode)}",
                        StatusSeverity.Attention);
                }

                var offer = NextOffer(career, marketOffers);
                if (offer != null)
                    return ($"Next: accept {Article.A(PlaceName(offer.DestinationCode))} contract",
                        StatusSeverity.Attention);

                if (TryBuyHint(career, fleet.Count, out var buyLine))
                    return (buyLine, StatusSeverity.Attention);

                var hangar = CareerProgress.NextAircraft(career, fleet.Count, fleet.Select(a => a.Type).ToList());
                if (hangar.HasOffer && !hangar.ReadyToBuy && !hangar.FleetFull)
                    return (!string.IsNullOrEmpty(hangar.BaseRequirementLine)
                            ? $"Next: expand your Adelaide base for the {hangar.Offer.Type.Name}"
                            : $"Next: keep flying {priority.Registration} to earn the {hangar.Offer.Type.Name}",
                        StatusSeverity.Attention);

                return ($"Next: plan a flight for {priority.Registration}", StatusSeverity.Normal);
            }

            if (priority.State is FleetState.TaxiOut or FleetState.HoldingShort or FleetState.TakingOff
                or FleetState.Outbound or FleetState.AtDestination or FleetState.Inbound
                or FleetState.HoldingForLanding or FleetState.GoAround or FleetState.Landing
                or FleetState.TaxiIn)
            {
                return ($"Next: track {priority.Registration}",
                    severity == StatusSeverity.Normal ? StatusSeverity.Attention : severity);
            }

            if (TryBuyHint(career, fleet.Count, out var hint))
                return (hint, StatusSeverity.Attention);

            return ($"Next: plan a flight for {priority.Registration}", StatusSeverity.Normal);
        }

        private static bool TryBuyHint(AirlineCareerState career, int ownedCount, out string line)
        {
            line = null;
            if (career == null || ownedCount >= AircraftAcquisition.MaxPlayerAircraft)
                return false;
            foreach (var offer in AircraftAcquisition.All)
            {
                if (ownedCount >= career.Base.FleetCapacity
                    || !PlayerBase.Supports(career.BaseLevel, offer.Type)
                    || career.Tier < offer.RequiredTier
                    || career.Reliability < offer.RequiredReliability
                    || career.CompletedPlayerRotations < offer.RequiredRotations
                    || !career.CanAfford(offer.Price))
                    continue;
                line = $"Next: buy {Article.A(offer.Type.Name)} in Fleet";
                return true;
            }

            return false;
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
