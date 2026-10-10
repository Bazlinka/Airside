using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    public sealed partial class AirlineOperations
    {
        /// <summary>The route forecast the player sees and is settled on, under the airline's difficulty.</summary>
        public RouteForecast Forecast(Destination origin, Destination destination, AircraftType type)
        {
            // Today's demand event (ADR 0127) is in both the forecast and the settlement.
            var forecast = RouteForecast.For(origin, destination, type,
                DemandEvents.MultiplierFor(destination.Code, _processedTo, Clock));
            return CareerState == null ? forecast : forecast.Under(CareerState.DifficultyProfile);
        }

        /// <summary>The forecast for this aircraft: tonnes if it is a freighter (ADR 0194), else seats.</summary>
        public RouteForecast Forecast(Destination origin, Destination destination, FleetAircraft aircraft)
        {
            if (aircraft == null || !aircraft.IsFreighter)
                return Forecast(origin, destination, aircraft?.Type);
            var forecast = RouteForecast.ForFreight(origin, destination, aircraft.Type);
            return CareerState == null ? forecast : forecast.Under(CareerState.DifficultyProfile);
        }

        /// <summary>
        /// Converts a parked player aircraft to a freighter, or back to passengers (ADR 0194). A fixed refit
        /// fee; the aircraft must be on its stand with no flight booked.
        /// </summary>
        public CommandResult SetFreighter(FleetAircraft aircraft, bool freighter)
        {
            if (aircraft == null || !_fleet.Contains(aircraft))
                return CommandResult.Refused("Unknown aircraft.");
            if (!aircraft.Airline.IsPlayer || CareerState == null)
                return CommandResult.Refused("Only your own aircraft can change role.");
            if (aircraft.IsFreighter == freighter)
                return CommandResult.Refused(freighter
                    ? $"{aircraft.Registration} is already a freighter."
                    : $"{aircraft.Registration} already carries passengers.");
            if (aircraft.State != FleetState.AtStand)
                return CommandResult.Refused($"{aircraft.Registration} must be parked on a stand to be refitted.");
            if (aircraft.Scheduled.HasValue)
                return CommandResult.Refused($"Cancel {aircraft.Registration}'s booked flight before the refit.");
            if (Maintenance.InCheck(aircraft, _processedTo))
                return CommandResult.Refused($"{aircraft.Registration} is in its check: {Maintenance.Status(aircraft, _processedTo, Clock)}.");
            var cost = FreightRates.ConversionCost(aircraft.Type);
            if (!CareerState.TryChargeDispatch(cost))
                return CommandResult.Refused($"The refit costs ${cost:N0}. You have ${CareerState.Funds:N0}.");
            aircraft.IsFreighter = freighter;
            return CommandResult.Ok;
        }

        /// <summary>
        /// The contracts on offer: the next authored career contracts the airline's tier allows
        /// and it has not yet fulfilled (ADR 0084), then the rotating market. The authored ones used
        /// to appear nowhere but the objective card's fallback.
        /// </summary>
        public IReadOnlyList<RouteContractDefinition> MarketOffers()
        {
            var offers = new List<RouteContractDefinition>();
            var localTypes = AdelaideOwnedTypes();
            if (CareerState != null)
            {
                foreach (var definition in RouteContractCatalogue.All)
                {
                    if (offers.Count >= FeaturedCareerContracts)
                        break;
                    if (CareerState.HasCompleted(definition.Id) || CareerState.Tier < definition.RequiredTier)
                        continue;
                    if (CareerState.ActiveContract != null && CareerState.ActiveContract.DefinitionId == definition.Id)
                        continue;
                    // Feature only work the airline can fly now or buy into at its tier: an
                    // authored Dash 8 contract must not hold a featured slot for a player who
                    // never buys one, hiding every later authored offer.
                    // ADR 0138: "buy into" means every purchase gate is met now (tier, reliability,
                    // flights and base), not just the tier, or the featured card is a dead end.
                    if (!localTypes.Exists(t => t.Id == definition.EligibleType.Id)
                        && !CouldBuyNow(definition.EligibleType))
                        continue;
                    offers.Add(definition);
                }
            }

            offers.AddRange(ContractMarket.At(_processedTo, localTypes, CareerState?.Reliability ?? 0,
                CareerState?.Tier ?? OperatingTier.Provisional, CareerState?.BaseLevel ?? PlayerBaseLevel.Starter));
            if (CareerState == null)
                return offers;
            var usable = false;
            foreach (var offer in offers)
                if (!CareerState.HasCompleted(offer.Id) && HasContractAircraft(offer))
                    usable = true;
            // A usable-looking offer is still a dead end when the airline cannot afford
            // even one dispatch. Keep a funded recovery path visible in that case too.
            var hasRecoveryRoute = DestinationCatalogue.TryFind("KGC", out var recovery);
            var strandedForCash = hasRecoveryRoute && localTypes.Count > 0 && CareerState.Funds <
                DispatchCost(localTypes[0], DistanceKm(recovery));
            if ((!usable || strandedForCash) && localTypes.Count > 0 && hasRecoveryRoute)
            {
                var type = localTypes[0];
                if (RouteAccess.Allows(type, recovery) && type.CanReach(DistanceKm(recovery)))
                {
                    var id = $"REC-{CareerState.CompletedPlayerRotations}-{type.Id}";
                    var basePay = FlightEconomics.FlightPay(type, DistanceKm(recovery));
                    offers.Insert(0, new RouteContractDefinition(id, Home.Code, recovery.Code, type, 2,
                        Math.Max(200, basePay / 2), Math.Max(200, basePay), 1,
                        OperatingTier.Provisional, reliabilityLossOnCancel: 2));
                }
            }
            return offers;
        }

        /// <summary>Every catalogue destination except home, reachable or not, for the map.</summary>
        public IEnumerable<Destination> MapDestinations()
        {
            foreach (var destination in DestinationCatalogue.Australia)
                if (!destination.Equals(Home))
                    yield return destination;
        }

        /// <summary>
        /// Everywhere an Adelaide aircraft can be planned to (ADR 0125): Australia plus the
        /// international catalogue. The planner used <see cref="MapDestinations"/>, which is
        /// Australia only, so no Adelaide aircraft could ever be sent to Auckland or Singapore —
        /// the authored international contracts and the finale's international goal were unreachable
        /// from the planner.
        /// </summary>
        public IEnumerable<Destination> PlannableDestinations()
        {
            foreach (var destination in DestinationCatalogue.All)
                if (!destination.Equals(Home))
                    yield return destination;
        }

        /// <summary>
        /// Accepts a route contract (authored or a live market offer) as the player's one
        /// active career contract. Market definitions are remembered so settlement still
        /// works after the offer window rolls (ADR 0056).
        /// </summary>
        public CommandResult AcceptContract(RouteContractDefinition definition)
        {
            if (definition == null)
                return CommandResult.Refused("Unknown contract.");
            if (CareerState == null)
                return CommandResult.Refused("No player airline.");
            if (CareerState.ActiveContract != null)
                return CommandResult.Refused("You already have a contract. Finish or abandon it first.");
            if (CareerState.HasCompleted(definition.Id))
                return CommandResult.Refused("You have already completed this contract.");
            if (CareerState.Tier < definition.RequiredTier)
                return CommandResult.Refused($"This contract needs the {definition.RequiredTier} tier.");
            if (!HasContractAircraft(definition))
                return CommandResult.Refused(
                    definition.RequiresFreighter
                        ? $"Refit {Article.A(definition.EligibleType.Name)} at Adelaide as a freighter for this contract."
                        : $"This contract needs {Article.A(definition.EligibleType.Name)} at Adelaide.");
            if (definition.HasDeadline && !CanStillFinish(definition))
                return CommandResult.Refused("You can't fly this in time with your aircraft.");

            CareerState.Remember(definition);
            CareerState.ActiveContract = new ActiveRouteContract(definition.Id, _processedTo);
            return CommandResult.Ok;
        }

        /// <summary>Shared acceptance/HUD eligibility; aircraft at outstations cannot service Adelaide work.</summary>
        public bool HasContractAircraft(RouteContractDefinition definition)
        {
            if (definition == null) return false;
            foreach (var aircraft in _fleet)
                if (aircraft.Airline.IsPlayer
                    && definition.MatchesAircraft(aircraft.Type, aircraft.IsFreighter))
                    return true;
            return false;
        }

        /// <summary>
        /// Drops the active contract for its advertised cancellation cost in reliability. Flights
        /// already booked still fly and pay their ordinary route revenue, just no contract bonus.
        /// </summary>
        public CommandResult AbandonContract()
        {
            if (CareerState?.ActiveContract == null)
                return CommandResult.Refused("You have no contract.");
            var loss = CareerState.TryFindDefinition(CareerState.ActiveContract.DefinitionId, out var definition)
                ? definition.ReliabilityLossOnCancel
                : 3;
            CareerState.AbandonContract(loss);
            return CommandResult.Ok;
        }

        public IReadOnlyList<CareerGoalStatus> CareerGoals() =>
            CareerRoadmap.Evaluate(CareerState, PlayerOwnedTypes(), PlayerFleetCount());

        public CareerGoalStatus PinnedCareerGoal() =>
            CareerRoadmap.Pinned(CareerState, PlayerOwnedTypes(), PlayerFleetCount());

        public CommandResult PinCareerGoal(string id)
        {
            foreach (var goal in CareerGoals())
            {
                if (goal.Id != id || goal.Stage > CareerState.Tier) continue;
                CareerState.PinGoal(id);
                return CommandResult.Ok;
            }
            return CommandResult.Refused("That goal isn't open yet.");
        }

        private void CheckCareerFinale()
        {
            if (CareerRoadmap.FinaleReady(CareerState, PlayerOwnedTypes(), PlayerFleetCount())
                && CareerState.TryAward(CareerRoadmap.FinaleKey, 0))
            {
                CareerState.MarkFinale();
                _careerEvents.Add(new CareerEvent(CareerEventKind.Finale, CareerState.Tier,
                    "You are an established airline. Every career goal is done, and the airport is yours to keep running."));
            }
        }

        /// <summary>Re-evaluates tier, goals and the finale after anything that can move them.</summary>
        private void AdvanceCareer()
        {
            if (CareerState == null) return;
            SeedCareerAnnouncements();
            CareerState.EvaluateTier(PlayerOwnedTypes(), PlayerFleetCount());
            foreach (var goal in CareerGoals())
                if (goal.Complete && goal.Stage <= CareerState.Tier && _announcedGoals.Add(goal.Id))
                    _careerEvents.Add(new CareerEvent(CareerEventKind.GoalComplete, goal.Stage,
                        $"Career goal complete: {goal.Title}."));
            while (_announcedTier.Value < CareerState.Tier)
            {
                var reached = _announcedTier.Value + 1;
                _announcedTier = reached;
                _careerEvents.Add(new CareerEvent(CareerEventKind.TierReached, reached,
                    $"{reached} tier reached! New aircraft, routes and goals are open."));
            }
            CheckCareerFinale();
            CheckChallenges(profitableDay: false);
            AnnounceMilestones();
        }

        /// <summary>The optional challenges and how far along each is.</summary>
        public IReadOnlyList<CareerChallengeStatus> CareerChallengeStatus() =>
            CareerChallenges.Status(CareerState, CareerChallenges.FactsFor(CareerState, PlayerFleetCount(), false));

        private void CheckChallenges(bool profitableDay)
        {
            if (CareerState == null)
                return;
            var facts = CareerChallenges.FactsFor(CareerState, PlayerFleetCount(), profitableDay);
            foreach (var challenge in CareerChallenges.All)
            {
                if (challenge.Prestige && !CareerState.FinaleReached)
                    continue;
                var (progress, target) = challenge.Progress(facts);
                if (progress < target || !CareerState.TryAward(challenge.Key, challenge.Reward))
                    continue;
                _careerEvents.Add(new CareerEvent(CareerEventKind.Challenge, CareerState.Tier,
                    $"Challenge done: {challenge.Title}. ${challenge.Reward:N0} paid."));
            }
        }

        private void AnnounceMilestones()
        {
            foreach (var milestone in CareerMilestones.Reached(CareerState, PlayerFleetCount(), PlayerOwnedTypes()))
                if (milestone.Reached && _announcedMilestones.Add(milestone.Id))
                    _careerEvents.Add(new CareerEvent(CareerEventKind.Milestone, CareerState.Tier,
                        $"Achievement: {milestone.Title}."));
        }

        // ---- Standing costs (Economy v2) ---------------------------------------------------------------------------------------

        /// <summary>The last Adelaide day whose lease and insurance has been paid. Null until the first day is observed.</summary>
        private long? _standingPaidThroughDay;

        internal long? SaveStandingPaidDay() => _standingPaidThroughDay;
        internal void RestoreStandingPaidDay(long? day) => _standingPaidThroughDay = day;

        /// <summary>
        /// Lease and insurance across the whole player fleet for one day, flying or parked. The founding aircraft is owned and
        /// pays insurance only. Everything else is leased (<see cref="LeaseTerms"/>).
        /// </summary>
        public long DailyStandingCost()
        {
            double total = 0;
            foreach (var aircraft in _fleet)
                if (aircraft.Airline.IsPlayer)
                    total += FlightCostModel.StandingPerDay(aircraft.Type, aircraft.IsFoundingAircraft);
            foreach (var aircraft in _outstationFleet)
                total += FlightCostModel.StandingPerDay(aircraft.Type, false);
            return (long)Math.Round(total);
        }

        /// <summary>
        /// Pays lease and insurance once per Adelaide day. Charged by day index, so a long away catch-up pays every missed day
        /// exactly once and the result does not depend on how the clock was stepped. Cash never goes below zero here: a
        /// shortfall is reported, and the bank loan and recovery contract are what carry a cash crunch.
        /// </summary>
        private void ChargeStandingCosts(long day)
        {
            if (CareerState == null)
                return;
            if (!_standingPaidThroughDay.HasValue)
            {
                _standingPaidThroughDay = day;
                return;
            }

            var days = day - _standingPaidThroughDay.Value;
            if (days <= 0)
                return;
            _standingPaidThroughDay = day;
            var due = DailyStandingCost() * days;
            if (due <= 0)
                return;
            var paid = CareerState.PayStanding(due);
            _today.Cost += paid;
            var text = paid >= due
                ? $"Leases and insurance: ${paid:N0} paid for {days} day{(days == 1 ? "" : "s")}."
                : $"Leases and insurance came to ${due:N0}; only ${paid:N0} was available. Fly or borrow to catch up.";
            _careerEvents.Add(new CareerEvent(CareerEventKind.News, CareerState.Tier, text));
        }

        /// <summary>The day ledger as plain values, for the save (ADR 0138).</summary>
        internal DaySnapshot SaveToday() => new(_today.Flights, _today.Revenue, _today.Cost, _today.BestCode,
            _today.BestMargin, _today.StartReliability, _today.LateFlights, _today.LateSeconds, _reportedDay);

        /// <summary>
        /// Once per Adelaide day: announce the day's demand event, and at curfew (23:00) the day's
        /// results, paying the profitable-day challenge. Presentation news only — nothing here changes
        /// the timeline, so it is checked once per update rather than as an event.
        /// </summary>
        private void AnnounceTheDay(SimulationTime now)
        {
            if (CareerState == null || PlayerAirline == null)
                return;
            _today.StartReliability ??= CareerState.Reliability;
            var day = DemandEvents.DayOf(now, Clock);
            ChargeStandingCosts(day);
            if (_newsDay != day)
            {
                _newsDay = day;
                if (DemandEvents.OnDay(day) is { } today)
                {
                    var names = new List<string>(today.Codes.Length);
                    foreach (var code in today.Codes)
                        names.Add(DestinationCatalogue.TryFind(code, out var d) ? d.Name : code);
                    var places = string.Join(", ", names);
                    _careerEvents.Add(new CareerEvent(CareerEventKind.News, CareerState.Tier,
                        $"{today.Headline}. Demand is up {(int)Math.Round((today.Multiplier - 1) * 100)}% today to {places}."));
                }
            }

            var local = Clock.LocalAt(now);
            // The report is due after the curfew falls, but a player who was away then still gets the one they
            // missed (otherwise two days of flights merge into the next "Today").
            var missedReport = _reportedDay.HasValue && _reportedDay.Value < day - 1;
            if (!missedReport && (local.Hour < AirportCurfew.ClosedFromHour || _reportedDay == day))
                return;
            _reportedDay = missedReport ? day - 1 : day;
            if (_today.Flights == 0)
            {
                _today.Reset(CareerState.Reliability);
                return;
            }

            var change = CareerState.Reliability - (_today.StartReliability ?? CareerState.Reliability);
            var best = DestinationCatalogue.TryFind(_today.BestCode, out var bestPlace) ? bestPlace.Name : _today.BestCode;
            _careerEvents.Add(new CareerEvent(CareerEventKind.DailyReport, CareerState.Tier,
                $"{(missedReport ? "Last report" : "Today")}: {_today.Flights} flight{(_today.Flights == 1 ? "" : "s")} · ${_today.Revenue:N0} in · "
                + $"{(_today.Margin >= 0 ? "+" : "−")}${Math.Abs(_today.Margin):N0} margin · reliability {CareerState.Reliability}% "
                + $"({(change >= 0 ? "+" : "")}{change}) · best route {best} · {DelaySummary(_today)}."));
            CheckChallenges(profitableDay: _today.Flights >= 4 && _today.Margin > 0);
            _today.Reset(CareerState.Reliability);
        }

        private static string DelaySummary(DayLedger day)
        {
            if (day.LateFlights == 0)
                return "every pushback on time";
            var minutes = Math.Max(1, (int)Math.Round(day.LateSeconds / 60.0));
            var text = $"{day.LateFlights} late ({minutes} min)";
            return day.WorstCause() is { } worst ? $"{text}, mostly {DelayCauses.Label(worst)}" : text;
        }

        /// <summary>When the active contract's deadline passes, or null (ADR 0127).</summary>
        public SimulationTime? ContractExpiresAt()
        {
            var active = CareerState?.ActiveContract;
            if (active == null || !CareerState.TryFindDefinition(active.DefinitionId, out var definition)
                || !definition.HasDeadline)
                return null;
            return active.AcceptedAt.Advance(definition.DeadlineSeconds);
        }

        /// <summary>
        /// A contract not flown by its deadline lapses (ADR 0127): the reliability it promised is lost,
        /// flights already paid stay paid, and anything still in the air flies on at the ordinary rate.
        /// Processed at the exact deadline, so the result never depends on how the clock was stepped.
        /// </summary>
        private bool ExpireContract(SimulationTime now)
        {
            var expiry = ContractExpiresAt();
            if (!expiry.HasValue || expiry.Value.CompareTo(now) > 0)
                return false;
            var active = CareerState.ActiveContract;
            CareerState.TryFindDefinition(active.DefinitionId, out var definition);
            CareerState.AbandonContract(definition.ReliabilityLossOnCancel);
            _careerEvents.Add(new CareerEvent(CareerEventKind.ContractExpired, CareerState.Tier,
                $"{ContractKindLabel(definition.Kind)} to {DestinationName(definition.DestinationCode)} ran out of time. "
                + $"You flew {active.CompletedRotations} of {definition.RequiredRotations}. Reliability −{definition.ReliabilityLossOnCancel}."));
            return true;
        }

        public static string ContractKindLabel(ContractKind kind) => kind switch
        {
            ContractKind.Charter => "Charter",
            ContractKind.Medical => "Medical flight",
            ContractKind.Freight => "Freight run",
            _ => "Contract"
        };

        /// <summary>Oldest-first career news since the last call — tier-ups, goals, the finale.</summary>
        public bool TryTakeCareerEvent(out CareerEvent careerEvent)
        {
            if (_careerEvents.Count == 0)
            {
                careerEvent = default;
                return false;
            }
            careerEvent = _careerEvents[0];
            _careerEvents.RemoveAt(0);
            return true;
        }

        /// <summary>Goals done in the current stage and what the stage earns — the career ring.</summary>
        public CareerStageProgress CareerStage() =>
            CareerRoadmap.StageProgress(CareerState, PlayerOwnedTypes(), PlayerFleetCount());

        /// <summary>A short "this destination would count" line for the pinned career goal, or empty.</summary>
        public string CareerRouteGuidance(Destination destination) =>
            CareerRoadmap.RouteGuidance(CareerState, PlayerOwnedTypes(), destination, PlayerFleetCount());

        /// <summary>Renames the player's own airline. AI operators use real airline names and cannot be renamed.</summary>
        public CommandResult RenameAirline(string name)
        {
            if (PlayerAirline == null)
                return CommandResult.Refused("No player airline.");
            try
            {
                PlayerAirline.Rename(name);
            }
            catch (ArgumentException e)
            {
                return CommandResult.Refused(e.Message);
            }

            return CommandResult.Ok;
        }

        /// <summary>Repaints the player's own airline's livery. AI liveries are authored and fixed.</summary>
        public CommandResult SetLivery(string liveryHex)
        {
            if (PlayerAirline == null)
                return CommandResult.Refused("No player airline.");
            try
            {
                PlayerAirline.Repaint(liveryHex);
            }
            catch (ArgumentException e)
            {
                return CommandResult.Refused(e.Message);
            }

            return CommandResult.Ok;
        }
    }
}
