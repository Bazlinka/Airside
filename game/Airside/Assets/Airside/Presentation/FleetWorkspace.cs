using System;
using System.Collections.Generic;
using System.Globalization;
using Airside.Domain;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>One aircraft row in the Fleet roster.</summary>
    public readonly struct FleetRosterRow
    {
        public FleetRosterRow(string registration, string typeName, string status, string stand,
            string operatorName, string liveryHex, StatusSeverity severity, bool isPlayer)
        {
            Registration = registration ?? string.Empty;
            TypeName = typeName ?? string.Empty;
            Status = status ?? string.Empty;
            Stand = stand ?? string.Empty;
            OperatorName = operatorName ?? string.Empty;
            LiveryHex = liveryHex ?? AirsidePalette.ConcreteHex;
            Severity = severity;
            IsPlayer = isPlayer;
        }

        public string Registration { get; }
        public string TypeName { get; }
        public string Status { get; }
        public string Stand { get; }
        public string OperatorName { get; }
        public string LiveryHex { get; }
        public StatusSeverity Severity { get; }
        public bool IsPlayer { get; }

        public HudTone StatusTone => Status.IndexOf("delay", StringComparison.OrdinalIgnoreCase) >= 0
                                     || Status.IndexOf("late", StringComparison.OrdinalIgnoreCase) >= 0
            ? HudTone.Negative
            : Status.StartsWith("Ready", StringComparison.OrdinalIgnoreCase)
              || Status.StartsWith("Completed", StringComparison.OrdinalIgnoreCase)
                ? HudTone.Positive
                : Severity >= StatusSeverity.Attention ? HudTone.Caution : HudTone.Default;
    }

    /// <summary>
    /// One aircraft the player could buy. Every value is read from
    /// <see cref="AircraftAcquisition"/> and current career state — no invented
    /// upgrade economy on the hangar list. Routine checks are a fleet action (ADR 0085).
    /// </summary>
    public readonly struct FleetMarketOffer
    {
        public FleetMarketOffer(AircraftType type, string typeName, string bandLabel, long price,
            string requirementLine, string standLine, bool affordable, bool unlocked, bool fleetFull,
            string cashWarning = null)
        {
            CashWarning = cashWarning ?? string.Empty;
            Type = type;
            TypeName = typeName ?? string.Empty;
            BandLabel = bandLabel ?? string.Empty;
            Price = price;
            RequirementLine = requirementLine ?? string.Empty;
            StandLine = standLine ?? string.Empty;
            Affordable = affordable;
            Unlocked = unlocked;
            FleetFull = fleetFull;
        }

        public AircraftType Type { get; }
        public string TypeName { get; }
        public string BandLabel { get; }
        public long Price { get; }

        /// <summary>The first unmet purchase gate, or what it is cleared for when all are met.</summary>
        public string RequirementLine { get; }

        /// <summary>
        /// ADR 0134: set when buying would leave less than the aircraft's usual flight costs, so it would sit
        /// until more money came in. Empty otherwise.
        /// </summary>
        public string CashWarning { get; }

        /// <summary>What happens to the airframe on delivery: a free stand, or a short ferry in.</summary>
        public string StandLine { get; }

        public bool Affordable { get; }
        public bool Unlocked { get; }
        public bool FleetFull { get; }

        /// <summary>Only a purchase the simulation would actually accept is offered as one.</summary>
        public bool CanBuy => Unlocked && Affordable && !FleetFull;
    }

    /// <summary>
    /// The Fleet workspace as data (ADR 0057): the player's own aircraft first, then the
    /// other operators on the field, the real status/assignment/preparation of whichever
    /// is selected, and the aircraft market under <see cref="AircraftAcquisition"/> rules.
    /// </summary>
    public sealed class FleetWorkspaceModel
    {
        private readonly List<FleetRosterRow> _mine = new();
        private readonly List<FleetRosterRow> _others = new();
        private readonly List<FleetMarketOffer> _market = new();
        private readonly List<OperationsPrepCheck> _prep = new();
        private readonly List<string> _capability = new();
        private readonly List<string> _capabilityIcons = new();

        public string Title => "FLEET";
        public string Subtitle { get; private set; } = string.Empty;

        public IReadOnlyList<FleetRosterRow> Mine => _mine;
        public IReadOnlyList<FleetRosterRow> Others => _others;
        public IReadOnlyList<FleetMarketOffer> Market => _market;

        public bool HasSelection { get; private set; }
        public string SelectedRegistration { get; private set; } = string.Empty;
        public string SelectedTypeName { get; private set; } = string.Empty;
        public bool SelectedIsPlayer { get; private set; }

        /// <summary>Real capability facts: route band, rotations flown, planning range.</summary>
        public IReadOnlyList<string> SelectedCapability => _capability;

        /// <summary>ADR 0130: one "category/name" icon per <see cref="SelectedCapability"/> line.</summary>
        public IReadOnlyList<string> SelectedCapabilityIcons => _capabilityIcons;

        /// <summary>The selected type's picture (Art-relative), and the livery it wears.</summary>
        public string SelectedThumbnail { get; private set; } = string.Empty;
        public string SelectedLiveryHex { get; private set; } = string.Empty;

        public string AssignmentLine { get; private set; } = string.Empty;
        public string AssignmentDetail { get; private set; } = string.Empty;
        public IReadOnlyList<OperationsPrepCheck> SelectedPrep => _prep;
        public AircraftHudAction PrimaryAction { get; private set; }
        public string PrimaryActionLabel { get; private set; } = string.Empty;
        public bool CanTrack { get; private set; }
        public bool CanStartCheck { get; private set; }
        public string StartCheckLabel { get; private set; } = string.Empty;

        /// <summary>ADR 0194: the aircraft's role, and the refit that changes it.</summary>
        public string RoleLine { get; private set; } = string.Empty;
        public bool CanChangeRole { get; private set; }
        public string ChangeRoleLabel { get; private set; } = string.Empty;

        public void Rebuild(AirlineOperations operations, SimulationTime now, string selectedRegistration)
        {
            _mine.Clear();
            _others.Clear();
            _market.Clear();
            _prep.Clear();
            _capability.Clear();
            _capabilityIcons.Clear();
            SelectedThumbnail = string.Empty;
            SelectedLiveryHex = string.Empty;
            HasSelection = false;
            SelectedRegistration = string.Empty;
            SelectedTypeName = string.Empty;
            SelectedIsPlayer = false;
            AssignmentLine = string.Empty;
            AssignmentDetail = string.Empty;
            PrimaryAction = AircraftHudAction.None;
            PrimaryActionLabel = string.Empty;
            CanTrack = false;
            CanStartCheck = false;
            StartCheckLabel = string.Empty;
            RoleLine = string.Empty;
            CanChangeRole = false;
            ChangeRoleLabel = string.Empty;
            if (operations == null)
                return;

            var clock = operations.Clock ?? AirlineClock.Default;
            var player = operations.PlayerAirline;
            FleetAircraft selected = null;

            foreach (var aircraft in operations.Fleet)
            {
                var row = Row(aircraft, now, operations.CareerState.BaseLevel);
                if (player != null && ReferenceEquals(aircraft.Airline, player))
                    _mine.Add(row);
                else
                    _others.Add(row);
                if (aircraft.Registration == selectedRegistration)
                    selected = aircraft;
            }

            var freeBays = 0;
            foreach (var _ in operations.FreeStands())
                freeBays++;
            var playerBase = operations.CareerState.Base;
            Subtitle = $"{_mine.Count} of {playerBase.FleetCapacity} aircraft"
                       + $"  ·  {Plural(freeBays, "regional stand")} free"
                       + $"  ·  {playerBase.Title}";

            FillMarket(operations, freeBays);

            if (selected != null)
                FillSelection(operations, selected, now, clock);
        }


        private static FleetRosterRow Row(FleetAircraft aircraft, SimulationTime now, PlayerBaseLevel baseLevel)
        {
            var stand = string.IsNullOrEmpty(aircraft.Stand.Value)
                ? "—"
                : AdelaideGround.StandLabel(aircraft.Stand);
            return new FleetRosterRow(
                aircraft.Registration,
                aircraft.Type.Name,
                FleetStatus(aircraft, now, baseLevel),
                stand,
                aircraft.Airline.Name,
                aircraft.Airline.LiveryHex,
                AircraftStatus.Severity(aircraft, now),
                aircraft.Airline.IsPlayer);
        }

        /// <summary>Roster wording: the prep stage while a booked departure is turning around.</summary>
        private static string FleetStatus(FleetAircraft aircraft, SimulationTime now, PlayerBaseLevel baseLevel)
        {
            if (aircraft.Airline.IsPlayer && Maintenance.InCheck(aircraft, now))
                return Maintenance.Status(aircraft, now, AirlineClock.Default);
            if (aircraft.Airline.IsPlayer && aircraft.State == FleetState.AtStand && aircraft.Scheduled.HasValue)
            {
                var prep = DeparturePrep.For(aircraft, now, baseLevel);
                return prep.Ready ? "Ready for pushback" : prep.Label;
            }

            if (aircraft.Airline.IsPlayer && aircraft.State == FleetState.AtStand
                && (Maintenance.IsOverdue(aircraft) || Maintenance.IsDueSoon(aircraft)))
                return Maintenance.Status(aircraft, now, AirlineClock.Default);

            return aircraft.State switch
            {
                FleetState.AtStand => "Available",
                FleetState.Outbound when aircraft.CurrentDestination.HasValue =>
                    $"Departed for {aircraft.CurrentDestination.Value.Name}",
                FleetState.Inbound when aircraft.CurrentDestination.HasValue =>
                    $"Inbound from {aircraft.CurrentDestination.Value.Name}",
                FleetState.AtDestination when aircraft.CurrentDestination.HasValue =>
                    $"On the ground at {aircraft.CurrentDestination.Value.Name}",
                _ => OperationsSummary.CompactState(aircraft, now)
            };
        }

        private void FillMarket(AirlineOperations operations, int freeBays)
        {
            var career = operations.CareerState;
            var owned = _mine.Count;
            var fleetFull = operations.PlayerFleetCount() >= AircraftAcquisition.MaxPlayerAircraft;
            var baseFull = owned >= career.Base.FleetCapacity;

            foreach (var offer in AircraftAcquisition.All)
            {
                var spec = AircraftCatalogue.For(offer.Type);
                var baseSupports = PlayerBase.Supports(career.BaseLevel, offer.Type);
                var unlocked = career.Tier >= offer.RequiredTier
                               && career.Reliability >= offer.RequiredReliability
                               && career.CompletedPlayerRotations >= offer.RequiredRotations
                               && baseSupports && !baseFull;
                var affordable = career.CanAfford(offer.Price);

                // "Requires Regional tier" (a career milestone, OperatingTier) and "flies
                // Regional routes" (a RouteBand) share the word "Regional" for two unrelated
                // systems — the tier line is spelled out as "career tier" and the route line
                // names real destinations instead of leaving the band as a bare label, so
                // buying a plane answers "where can it fly" concretely, not abstractly.
                string requirement;
                if (fleetFull)
                    requirement = $"Your fleet is full at {AircraftAcquisition.MaxPlayerAircraft} aircraft";
                else if (baseFull)
                    requirement = career.BaseLevel == PlayerBaseLevel.International
                        ? "Adelaide is full. Buy for an outstation in Network"
                        : $"The {career.Base.Title} holds {career.Base.FleetCapacity}. Expand your base";
                else if (!baseSupports)
                    requirement = $"Needs the {PlayerBase.For(PlayerBase.RequiredLevel(offer.Type)).Title}. Expand your base";
                else if (career.Tier < offer.RequiredTier)
                {
                    var requiredBase = CareerProgress.BaseCapabilityFor(offer.RequiredTier);
                    requirement = $"Needs the {requiredBase.Title} and the {offer.RequiredTier} tier";
                }
                else if (career.Reliability < offer.RequiredReliability)
                    requirement = $"Needs {offer.RequiredReliability}% reliability. You have {career.Reliability}%";
                else if (career.CompletedPlayerRotations < offer.RequiredRotations)
                    requirement = $"Needs {offer.RequiredRotations} flights. You have flown {career.CompletedPlayerRotations}";
                else if (!affordable)
                    requirement = $"Costs ${offer.Price:N0}. You have ${career.Funds:N0}";
                else
                {
                    var reach = RouteAccess.ExampleDestinations(offer.Operates);
                    var suffix = offer.Operates == RouteBand.Regional ? "" : ", +closer";
                    requirement = $"Ready to buy · flies {RouteMapWorkspaceModel.BandLabel(offer.Operates).ToLowerInvariant()} "
                                  + $"routes ({reach}{suffix})";
                }

                var needsGate = AirlineOperations.NeedsTerminalGate(offer.Type);
                string standLine;
                if (needsGate)
                    standLine = "Arrives at a free gate, or is flown in";
                else if (freeBays > 1)
                    standLine = "Arrives on a free regional bay";
                else
                    standLine = "No bay free, so it is flown in";

                var cashWarning = string.Empty;
                if (affordable)
                {
                    var left = career.Funds - offer.Price;
                    var usualFlight = operations.DispatchCost(offer.Type, FlightEconomics.TypicalLegKm(offer.Type));
                    if (left < usualFlight)
                        cashWarning = $"Leaves ${left:N0}. Its usual flight costs about ${usualFlight:N0}";
                }

                _market.Add(new FleetMarketOffer(offer.Type, spec.Name,
                    RouteMapWorkspaceModel.BandLabel(offer.Operates), offer.Price,
                    requirement, standLine, affordable, unlocked, fleetFull || baseFull, cashWarning));
            }

            // ADR 0131: twelve types for sale, three cards at a time — what you can buy leads, then what
            // is unlocked but short of money, then the rest in career order (AircraftAcquisition.All).
            var ordered = new List<(int Rank, int Index, FleetMarketOffer Offer)>();
            for (var i = 0; i < _market.Count; i++)
            {
                var offer = _market[i];
                var rank = offer.CanBuy ? 0 : offer.Unlocked && !offer.FleetFull ? 1 : 2;
                ordered.Add((rank, i, offer));
            }
            ordered.Sort((a, b) => a.Rank != b.Rank ? a.Rank.CompareTo(b.Rank) : a.Index.CompareTo(b.Index));
            _market.Clear();
            foreach (var entry in ordered)
                _market.Add(entry.Offer);
        }

        private void AddFact(string icon, string text)
        {
            _capability.Add(text);
            _capabilityIcons.Add(icon);
        }

        private void FillSelection(AirlineOperations operations, FleetAircraft aircraft,
            SimulationTime now, AirlineClock clock)
        {
            HasSelection = true;
            SelectedRegistration = aircraft.Registration;
            SelectedTypeName = aircraft.Type.Name;
            SelectedIsPlayer = aircraft.Airline.IsPlayer;

            SelectedThumbnail = FleetWorkspacePainter.Thumbnail(aircraft.Type);
            SelectedLiveryHex = aircraft.Airline.LiveryHex;

            var ceiling = RouteAccess.Ceiling(aircraft.Type);
            var ceilingSuffix = ceiling == RouteBand.Regional ? "" : ", +closer";
            AddFact("operation/departure", $"Flies {RouteMapWorkspaceModel.BandLabel(ceiling).ToLowerInvariant()} routes "
                                           + $"({RouteAccess.ExampleDestinations(ceiling)}{ceilingSuffix})");
            var distinction = AircraftDistinctions.Current(aircraft.CompletedTrips);
            var historyTitle = distinction.Flights > 0 ? distinction.Title : "New to the fleet";
            AddFact("operation/completed", $"{historyTitle} · {Plural(aircraft.CompletedTrips, "flight")} flown");
            AddFact("economy/route", $"{aircraft.Type.PracticalRangeKm:#,0} km range");
            if (aircraft.Airline.IsPlayer)
            {
                var joined = clock.LocalAt(aircraft.JoinedAirlineAt)
                    .ToString("d MMM yyyy", CultureInfo.InvariantCulture);
                AddFact("economy/reputation", aircraft.IsFoundingAircraft
                    ? $"Founding aircraft · with the airline since {joined}"
                    : $"Joined the airline {joined}");
                if (aircraft.HistoryFlights > 0)
                {
                    var recorded = aircraft.HistoryFlights == aircraft.CompletedTrips
                        ? $"${aircraft.LifetimeRevenue:N0} earned"
                        : $"${aircraft.LifetimeRevenue:N0} recorded since the logbook began";
                    var favourite = aircraft.FavouriteRoute;
                    if (!string.IsNullOrEmpty(favourite.DestinationCode)
                        && DestinationCatalogue.TryFind(favourite.DestinationCode, out var destination))
                        recorded += $" · {destination.Name} {favourite.Flights}×";
                    AddFact("economy/cash", recorded);
                }
                var next = AircraftDistinctions.Next(aircraft.CompletedTrips);
                if (next.Flights > 0)
                    AddFact("operation/completed",
                        $"Next distinction: {next.Title} · {next.Flights - aircraft.CompletedTrips} flights to go");
                var baseLevel = operations.CareerState.BaseLevel;
                AddFact("service/inspection", PlayerBase.MaintenanceLine(baseLevel, aircraft.Type)
                                              + " · $" + Maintenance.CheckCost(aircraft.Type, baseLevel).ToString("N0")
                                              + " · " + (Maintenance.CheckSeconds(aircraft.Type, baseLevel) / 3600.0).ToString("0.#") + " h");
                var check = Maintenance.Status(aircraft, now, clock);
                if (!string.IsNullOrEmpty(check))
                    AddFact("operation/turnaround", check);
            }
            if (!aircraft.Airline.IsPlayer)
                AddFact("economy/reputation", $"Operated by {aircraft.Airline.Name}");
            // The starter aircraft was never bought (AircraftAcquisition's own doc comment),
            // so it has no purchase price to base a resale figure on — line omitted for it
            // rather than showing a made-up number.
            else if (operations.CanResell(aircraft) && AircraftAcquisition.TryFor(aircraft.Type, out var ownedOffer))
                AddFact("economy/cash", $"Sells for ${(long)Math.Round(ownedOffer.Price * AirlineOperations.ResaleFraction):N0}");

            if (aircraft.Scheduled.HasValue)
            {
                var booked = aircraft.Scheduled.Value;
                AssignmentLine = $"Adelaide → {booked.Destination.Name}";
                AssignmentDetail = $"Departs {clock.TimeText(booked.DepartAt)}"
                                   + $"  ·  {AdelaideGround.StandLabel(aircraft.Stand)}";
            }
            else if (Maintenance.InCheck(aircraft, now))
            {
                AssignmentLine = Maintenance.Status(aircraft, now, clock);
                AssignmentDetail = AdelaideGround.StandLabel(aircraft.Stand);
            }
            else if (aircraft.CurrentDestination.HasValue)
            {
                var inbound = aircraft.State is FleetState.Inbound or FleetState.HoldingForLanding
                    or FleetState.Landing or FleetState.AwaitingStand or FleetState.TaxiIn;
                AssignmentLine = inbound
                    ? $"{aircraft.CurrentDestination.Value.Name} → Adelaide"
                    : $"Adelaide → {aircraft.CurrentDestination.Value.Name}";
                AssignmentDetail = aircraft.StateEndsAt.HasValue
                    ? $"{FlightBoard.TimeMeaning(aircraft, now).ToLowerInvariant()} {clock.TimeText(aircraft.StateEndsAt.Value)}"
                    : FlightBoard.PhaseLabel(aircraft, now);
            }
            else
            {
                AssignmentLine = "No flight planned";
                AssignmentDetail = AdelaideGround.StandLabel(aircraft.Stand);
            }

            if (aircraft.Airline.IsPlayer && aircraft.State == FleetState.AtStand && aircraft.Scheduled.HasValue)
            {
                var prep = DeparturePrep.For(aircraft, now, operations.CareerState.BaseLevel);
                _prep.Add(PrepCheck("Fuel", prep.FuelProgress, prep.Stage == DeparturePrepStage.Fuel));
                _prep.Add(PrepCheck("Catering", prep.CateringProgress, prep.Stage == DeparturePrepStage.Catering));
                _prep.Add(PrepCheck("Baggage", prep.BaggageProgress, prep.Stage == DeparturePrepStage.Baggage));
                _prep.Add(PrepCheck("Boarding", prep.BoardingProgress, prep.Stage == DeparturePrepStage.Boarding));
            }

            PrimaryAction = OperationsSummary.PrimaryAction(aircraft, now);
            PrimaryActionLabel = OperationsSummary.ActionLabel(PrimaryAction).ToUpperInvariant();
            // Track is the primary action for an aircraft that is already flying; offering it
            // twice on the same card just reads as a mistake. Parked idle aircraft offer a
            // check instead (ADR 0085) — following a parked airframe is a click on the field.
            CanStartCheck = Maintenance.CanStart(aircraft, now) && PrimaryAction != AircraftHudAction.StartCheck;
            StartCheckLabel = CanStartCheck
                ? "CHECK $" + Maintenance.CheckCost(aircraft.Type, operations.CareerState.BaseLevel).ToString("N0")
                : string.Empty;
            CanTrack = PrimaryAction != AircraftHudAction.TrackFlight && !CanStartCheck
                       && !Maintenance.InCheck(aircraft, now);

            if (aircraft.Airline.IsPlayer)
            {
                var refit = FreightRates.ConversionCost(aircraft.Type);
                RoleLine = aircraft.IsFreighter
                    ? $"Freighter · {FreightRates.CapacityTonnes(aircraft.Type):0.#} t payload, paid on the freight a route offers"
                    : $"Passengers · a freighter would carry {FreightRates.CapacityTonnes(aircraft.Type):0.#} t";
                CanChangeRole = aircraft.State == FleetState.AtStand && !aircraft.Scheduled.HasValue
                                && !Maintenance.InCheck(aircraft, now);
                ChangeRoleLabel = (aircraft.IsFreighter ? "TO PASSENGERS $" : "TO FREIGHTER $")
                                  + refit.ToString("N0");
            }
        }

        private static OperationsPrepCheck PrepCheck(string name, double progress, bool active)
        {
            var done = progress >= 1;
            var label = done ? name : active ? $"{name} {DeparturePrep.Percent(progress)}%" : name;
            return new OperationsPrepCheck(label, done, active);
        }

        private static string Plural(int count, string noun) =>
            count == 1 ? $"1 {noun}" : $"{count} {noun}s";
    }

    /// <summary>Where the Fleet workspace draws its roster, detail pane and market strip.</summary>
    public readonly struct FleetWorkspaceLayout
    {
        public const float RosterRowHeight = 44f;
        public const float SectionCaptionHeight = 18f;
        public const float DetailGap = 24f;
        public const float MarketRowHeight = 92f;
        public const float MarketCaptionHeight = 20f;
        public const float MinRosterWidth = 260f;
        public const float MinDetailWidth = 300f;

        private FleetWorkspaceLayout(HudBox surface, HudBox header, HudBox roster, HudBox detail,
            HudBox market, HudBox divider, int marketRows)
        {
            Surface = surface;
            Header = header;
            Roster = roster;
            Detail = detail;
            Market = market;
            Divider = divider;
            MarketRows = marketRows;
        }

        public HudBox Surface { get; }
        public HudBox Header { get; }
        public HudBox Roster { get; }
        public HudBox Detail { get; }

        /// <summary>The aircraft-market strip along the bottom. Empty when there is no room.</summary>
        public HudBox Market { get; }

        /// <summary>Hairline between the roster and the detail pane.</summary>
        public HudBox Divider { get; }

        public int MarketRows { get; }

        public HudBox TitleBox => new(Header.X + HudShell.SurfacePadding, Header.Y + 12f,
            Header.Width - HudShell.SurfacePadding * 2f, 30f);

        public HudBox SubtitleBox => new(Header.X + HudShell.SurfacePadding, Header.Y + 40f,
            Header.Width - HudShell.SurfacePadding * 2f, 18f);

        public HudBox RosterToolbar => new(Roster.X, Roster.Y, Roster.Width, 30f);

        public HudBox RosterRow(int index) =>
            new(Roster.X, Roster.Y + 30f + index * RosterRowHeight, Roster.Width, RosterRowHeight - 2f);

        public int VisibleRosterRows => Roster.Height <= 30f ? 0 : (int)((Roster.Height - 30f) / RosterRowHeight);

        public HudBox MarketCaption => Market.IsEmpty
            ? HudBox.Empty
            : Market.WithHeight(MarketCaptionHeight);

        public HudBox MarketRow(int index) => Market.IsEmpty || MarketRows <= 0
            ? HudBox.Empty
            : new HudBox(Market.X + index * (Market.Width / MarketRows), Market.Y + MarketCaptionHeight + 6f,
                Market.Width / MarketRows - 10f, MarketRowHeight);

        public static FleetWorkspaceLayout Create(HudBox surface, int marketOffers)
        {
            var header = HudShell.Header(surface);
            var body = HudShell.Body(surface, hasFooter: false);

            var rows = marketOffers < 0 ? 0 : marketOffers > 3 ? 3 : marketOffers;
            var marketHeight = rows == 0
                ? 0f
                : MarketCaptionHeight + 6f + MarketRowHeight + 8f;
            if (marketHeight > body.Height * 0.45f)
            {
                rows = 0;
                marketHeight = 0f;
            }

            var market = rows == 0
                ? HudBox.Empty
                : new HudBox(body.X, body.Bottom - marketHeight, body.Width, marketHeight);
            var upperHeight = rows == 0 ? body.Height : market.Y - body.Y - 16f;
            var upper = new HudBox(body.X, body.Y, body.Width, upperHeight < 0f ? 0f : upperHeight);

            var rosterWidth = upper.Width * 0.44f;
            if (rosterWidth < MinRosterWidth)
                rosterWidth = MinRosterWidth;
            if (upper.Width - rosterWidth - DetailGap < MinDetailWidth)
                rosterWidth = upper.Width - MinDetailWidth - DetailGap;
            if (rosterWidth < 0f)
                rosterWidth = upper.Width;

            var roster = new HudBox(upper.X, upper.Y, rosterWidth, upper.Height);
            var detailWidth = upper.Width - rosterWidth - DetailGap;
            var detail = detailWidth > 0f
                ? new HudBox(upper.Right - detailWidth, upper.Y, detailWidth, upper.Height)
                : HudBox.Empty;
            var divider = detail.IsEmpty
                ? HudBox.Empty
                : new HudBox(roster.Right + DetailGap * 0.5f, upper.Y, 1f, upper.Height);

            return new FleetWorkspaceLayout(surface, header, roster, detail, market, divider, rows);
        }
    }

    /// <summary>Paints the Fleet workspace into the shared draw list.</summary>
    public static class FleetWorkspacePainter
    {
        public const string MarketPrevious = "market:previous";
        public const string MarketNext = "market:next";

        public static void Paint(HudDrawList into, FleetWorkspaceModel model, FleetWorkspaceLayout layout,
            string selectedRegistration, int scrollRow, bool showOtherOperators = false, int marketStart = 0)
        {
            if (into == null || model == null)
                return;

            into.Clear();
            into.Surface(layout.Surface);
            HudShellPainter.PaintSheetHeader(into, layout.Surface, model.Title, model.Subtitle,
                layout.TitleBox, layout.SubtitleBox);

            PaintRoster(into, model, layout, selectedRegistration, scrollRow, showOtherOperators);
            if (!layout.Divider.IsEmpty)
                into.Hairline(layout.Divider);
            PaintDetail(into, model, layout);
            PaintMarket(into, model, layout, marketStart);
        }

        /// <summary>Clamps a market page start so the last page is full where it can be.</summary>
        public static int ClampMarketStart(int start, int offers, int rows) =>
            rows <= 0 || offers <= rows ? 0 : Math.Max(0, Math.Min(start, offers - rows));

        private static void PaintRoster(HudDrawList into, FleetWorkspaceModel model,
            FleetWorkspaceLayout layout, string selectedRegistration, int scrollRow, bool showOtherOperators)
        {
            var toolbar = layout.RosterToolbar;
            if (toolbar.Width >= 285f)
                into.Caption(new HudBox(toolbar.X, toolbar.Y + 8f, toolbar.Width - 175f, 18f), "YOUR AIRCRAFT");
            if (model.Others.Count > 0)
            {
                var buttonWidth = toolbar.Width >= 285f ? 170f : toolbar.Width;
                into.Button(new HudBox(toolbar.Right - buttonWidth, toolbar.Y, buttonWidth, 26f),
                    showOtherOperators ? "HIDE OTHER AIRLINES" : $"OTHER AIRLINES · {model.Others.Count}",
                    HudAction.ToggleOtherOperators, HudButtonStyle.Secondary);
            }
            var index = 0;
            var drawn = 0;
            var capacity = layout.VisibleRosterRows;
            var skip = scrollRow < 0 ? 0 : scrollRow;

            // Your own aircraft always come first; other operators follow, quieter.
            for (var i = 0; i < model.Mine.Count; i++)
            {
                if (index++ < skip)
                    continue;
                if (drawn >= capacity)
                    return;
                PaintRosterRow(into, layout, model.Mine[i], drawn++, selectedRegistration, quiet: false);
            }

            if (!showOtherOperators || model.Others.Count == 0 || drawn >= capacity)
                return;

            if (index++ >= skip)
            {
                into.Caption(layout.RosterRow(drawn).Inset(0f, 8f, 0f, 0f), "OTHER AIRLINES");
                drawn++;
            }

            for (var i = 0; i < model.Others.Count; i++)
            {
                if (index++ < skip)
                    continue;
                if (drawn >= capacity)
                    return;
                PaintRosterRow(into, layout, model.Others[i], drawn++, selectedRegistration, quiet: true);
            }
        }

        private static void PaintRosterRow(HudDrawList into, FleetWorkspaceLayout layout, FleetRosterRow row,
            int slot, string selectedRegistration, bool quiet)
        {
            var box = layout.RosterRow(slot);
            var selected = row.Registration == selectedRegistration;
            var alpha = quiet ? OperationsWorkspacePainter.SubordinateAlpha : 1f;

            if (selected)
                into.Fill(box, HudTone.Accent, 0.28f);
            else if ((slot & 1) == 1)
                into.Fill(box, HudTone.Default, 0.025f);
            into.Fill(new HudBox(box.X, box.Y, 3f, box.Height), HudTone.Default, quiet ? 0.5f : 1f,
                row.LiveryHex);

            var standWidth = box.Width >= 240f ? 74f : 0f;
            var body = new HudBox(box.X + 12f, box.Y + 7f, box.Width - 12f - standWidth, 18f);
            const float regWidth = 70f;
            // Status is the column worth reading; on a narrow roster the type gives way to it
            // rather than squeezing it to nothing.
            var typeWidth = body.Width >= 290f ? 104f : 0f;

            into.Text(body.WithWidth(regWidth), row.Registration, 13f, HudTone.Default, HudTextStyle.Bold,
                alpha: alpha);
            if (typeWidth > 0f)
                into.Text(body.Offset(regWidth, 0f).WithWidth(typeWidth - 8f), row.TypeName, 11f,
                    HudTone.Muted, alpha: alpha);
            into.Text(body.Offset(regWidth + typeWidth, 0f).WithWidth(body.Width - regWidth - typeWidth),
                row.Status, 12f, row.StatusTone, alpha: alpha);
            if (standWidth > 0f)
                into.Text(new HudBox(box.Right - standWidth, box.Y + 7f, standWidth - 6f, 18f), row.Stand,
                    12f, HudTone.Muted, HudTextStyle.Regular, HudAlign.Right, alpha: alpha);
            into.Hotspot(box, HudAction.Select(row.Registration));
        }

        private static void PaintDetail(HudDrawList into, FleetWorkspaceModel model, FleetWorkspaceLayout layout)
        {
            var pane = layout.Detail;
            if (pane.IsEmpty)
                return;

            if (!model.HasSelection)
            {
                into.Text(new HudBox(pane.X, pane.Y + 8f, pane.Width, 40f),
                    "Select an aircraft to see what it can fly, where it is going and its turnaround.", 13f,
                    HudTone.Muted, HudTextStyle.Wrap);
                return;
            }

            var top = pane.Y;
            // ADR 0130: the aircraft itself, on its livery, when the pane has the height for it.
            if (pane.Height >= 560f && model.SelectedThumbnail.Length > 0)
            {
                var stage = new HudBox(pane.X, pane.Y, pane.Width, 118f);
                PaintAircraftStage(into, stage, model.SelectedThumbnail, model.SelectedLiveryHex);
                top = stage.Bottom + 10f;
            }

            into.Text(new HudBox(pane.X, top, pane.Width, 28f),
                $"{model.SelectedRegistration}  ·  {model.SelectedTypeName}", 20f, HudTone.Default,
                HudTextStyle.Bold);

            var y = top + 38f;
            for (var i = 0; i < model.SelectedCapability.Count; i++)
            {
                var icon = i < model.SelectedCapabilityIcons.Count ? model.SelectedCapabilityIcons[i] : string.Empty;
                var slash = icon.IndexOf('/');
                if (slash > 0)
                    into.Icon(new HudBox(pane.X, y, 16f, 16f), icon.Substring(0, slash), icon.Substring(slash + 1),
                        HudTone.Accent);
                into.Text(new HudBox(pane.X + 24f, y, pane.Width - 24f, 18f), model.SelectedCapability[i], 13f);
                y += 21f;
            }

            y += 12f;
            into.Hairline(new HudBox(pane.X, y, pane.Width, 1f));
            y += 12f;
            into.Caption(new HudBox(pane.X, y, pane.Width, 16f), "NOW");
            y += 20f;
            into.Text(new HudBox(pane.X, y, pane.Width, 20f), model.AssignmentLine, 14f, HudTone.Default,
                HudTextStyle.Bold);
            y += 22f;
            into.Text(new HudBox(pane.X, y, pane.Width, 18f), model.AssignmentDetail, 12f, HudTone.Muted);
            y += 26f;

            if (model.SelectedPrep.Count > 0)
            {
                into.Hairline(new HudBox(pane.X, y, pane.Width, 1f));
                y += 12f;
                into.Caption(new HudBox(pane.X, y, pane.Width, 16f), "PREPARATION");
                y += 20f;
                var slot = pane.Width / model.SelectedPrep.Count;
                var centreY = y + 10f;
                into.Line(pane.X + slot * 0.5f, centreY,
                    pane.Right - slot * 0.5f, centreY, HudTone.Muted, 2f);
                for (var i = 0; i < model.SelectedPrep.Count; i++)
                {
                    var check = model.SelectedPrep[i];
                    var centreX = pane.X + slot * (i + 0.5f);
                    into.Dot(centreX, centreY, check.Active ? 18f : 15f, check.Tone);
                    into.Text(new HudBox(pane.X + i * slot, centreY + 15f, slot - 4f, 30f),
                        check.Label, 11f, check.Tone,
                        check.Active ? HudTextStyle.Bold : HudTextStyle.Regular, HudAlign.Center);
                }

                y += 56f;
            }

            if (!model.SelectedIsPlayer)
            {
                into.Text(new HudBox(pane.X, y, pane.Width, 36f),
                    "Another airline's aircraft. You can see it but not give it orders.", 12f,
                    HudTone.Muted, HudTextStyle.Wrap);
                return;
            }

            if (model.RoleLine.Length > 0)
            {
                into.Text(new HudBox(pane.X, y - 6f, pane.Width, 18f), model.RoleLine, 12f, HudTone.Muted);
                y += 20f;
            }

            var buttonY = y + 8f;
            var roleRow = model.RoleLine.Length > 0 ? 40f : 0f;
            if (buttonY + 34f + roleRow > pane.Bottom)
                buttonY = pane.Bottom - 38f - roleRow;
            var half = (pane.Width - 10f) * 0.5f;
            if (model.PrimaryAction != AircraftHudAction.None)
                into.Button(new HudBox(pane.X, buttonY, half, 34f), model.PrimaryActionLabel,
                    HudAction.Primary, HudButtonStyle.Primary);
            if (model.CanStartCheck)
                into.Button(new HudBox(pane.X + half + 10f, buttonY, half, 34f), model.StartCheckLabel,
                    HudAction.StartCheck, HudButtonStyle.Secondary);
            else if (model.CanTrack)
                into.Button(new HudBox(pane.X + half + 10f, buttonY, half, 34f), "TRACK", HudAction.Track,
                    HudButtonStyle.Secondary);
            if (model.RoleLine.Length > 0)
                into.Button(new HudBox(pane.X, buttonY + 40f, pane.Width, 30f), model.ChangeRoleLabel,
                    HudAction.ToggleFreighter, HudButtonStyle.Secondary, model.CanChangeRole);
        }

        /// <summary>
        /// An aircraft picture on a stage: a raised glass backdrop with the airline's livery as the lower
        /// band, the way the setup preview shows the starter Saab (ADR 0123/0130).
        /// </summary>
        public static void PaintAircraftStage(HudDrawList into, HudBox stage, string thumbnail, string liveryHex,
            float alpha = 1f)
        {
            into.Fill(stage, HudTone.Default, 0.9f * alpha, AirsidePalette.GlassRaisedHex);
            if (!string.IsNullOrEmpty(liveryHex))
                into.Fill(new HudBox(stage.X, stage.Y + stage.Height * 0.62f, stage.Width, stage.Height * 0.38f),
                    HudTone.Default, 0.8f * alpha, liveryHex);
            var height = stage.Height - 8f;
            var width = Math.Min(stage.Width - 12f, height * 1.5f);
            into.Image(new HudBox(stage.X + (stage.Width - width) * 0.5f, stage.Y + 4f, width, height), thumbnail, alpha);
        }

        private static IReadOnlyList<FleetMarketOffer> Page(IReadOnlyList<FleetMarketOffer> offers, int start, int rows)
        {
            var page = new List<FleetMarketOffer>(rows);
            for (var i = start; i < offers.Count && page.Count < rows; i++)
                page.Add(offers[i]);
            return page;
        }

        /// <summary>The type's picture, Art-relative, or empty for a type without one.</summary>
        public static string Thumbnail(AircraftType type) =>
            type != null && AircraftCatalogue.TryFor(type, out var spec) ? spec.ThumbnailPath ?? string.Empty : string.Empty;

        /// <summary>The one reason every locked offer shares (a full base, say), or null when they differ.</summary>
        public static string SharedLockReason(IReadOnlyList<FleetMarketOffer> offers, int shown)
        {
            string shared = null;
            var count = 0;
            for (var i = 0; i < offers.Count && i < shown; i++)
            {
                if (offers[i].CanBuy)
                    return null;
                if (shared == null)
                    shared = offers[i].RequirementLine;
                else if (shared != offers[i].RequirementLine)
                    return null;
                count++;
            }

            return count >= 2 ? shared : null;
        }

        private static void PaintMarket(HudDrawList into, FleetWorkspaceModel model, FleetWorkspaceLayout layout,
            int marketStart)
        {
            if (layout.Market.IsEmpty)
                return;

            into.Hairline(new HudBox(layout.Market.X, layout.Market.Y - 10f, layout.Market.Width, 1f));
            into.Caption(layout.MarketCaption, "AIRCRAFT MARKET");
            // One reason for every card (a full base) is said once, beside the caption, not three times.
            var start = ClampMarketStart(marketStart, model.Market.Count, layout.MarketRows);
            if (model.Market.Count > layout.MarketRows)
            {
                // ‹ 4–6 of 12 › — page through the whole market (ADR 0131).
                var pager = new HudBox(layout.Market.Right - 150f, layout.MarketCaption.Y - 5f, 150f, 22f);
                into.Button(pager.WithWidth(30f), "‹", MarketPrevious, HudButtonStyle.Secondary, start > 0);
                into.Text(new HudBox(pager.X + 34f, pager.Y + 3f, 82f, 16f),
                    $"{start + 1}–{Math.Min(start + layout.MarketRows, model.Market.Count)} of {model.Market.Count}", 11f,
                    HudTone.Muted, HudTextStyle.Regular, HudAlign.Center);
                into.Button(new HudBox(pager.Right - 30f, pager.Y, 30f, 22f), "›", MarketNext, HudButtonStyle.Secondary,
                    start + layout.MarketRows < model.Market.Count);
            }

            var shared = SharedLockReason(Page(model.Market, start, layout.MarketRows), layout.MarketRows);
            if (shared != null)
                into.Text(new HudBox(layout.MarketCaption.X + 170f, layout.MarketCaption.Y - 1f,
                        layout.Market.Width - 170f - 160f, 16f), shared, 11f, HudTone.Caution, HudTextStyle.Bold);

            var shown = 0;
            foreach (var offer in Page(model.Market, start, layout.MarketRows))
            {
                var box = layout.MarketRow(shown++);
                into.Fill(box, HudTone.Default, offer.CanBuy ? 0.06f : 0.03f);
                if (offer.CanBuy)
                    into.Outline(box, HudTone.Accent, 0.6f);

                // The aircraft on the left when the card is wide enough; the words beside it.
                var picture = box.Width >= 300f && Thumbnail(offer.Type).Length > 0 ? 108f : 0f;
                if (picture > 0f)
                    into.Image(new HudBox(box.X + 6f, box.Y + 10f, picture, picture / 1.5f), Thumbnail(offer.Type),
                        offer.CanBuy ? 1f : 0.55f);
                var textX = box.X + 12f + picture;
                var textWidth = box.Width - 24f - picture;
                into.Text(new HudBox(textX, box.Y + 8f, textWidth, 18f), offer.TypeName, 14f, HudTone.Default,
                    HudTextStyle.Bold);
                var warn = offer.CanBuy && offer.CashWarning.Length > 0;
                var line = warn ? offer.CashWarning
                    : offer.CanBuy ? offer.StandLine : shared != null ? offer.BandLabel + " routes" : offer.RequirementLine;
                into.Text(new HudBox(textX, box.Y + 29f, textWidth, 30f), line, 11f,
                    warn ? HudTone.Caution : offer.CanBuy || shared != null ? HudTone.Muted : HudTone.Caution,
                    HudTextStyle.Wrap);
                into.Text(new HudBox(textX, box.Bottom - 25f, textWidth - 82f, 18f), $"${offer.Price:N0}", 14f,
                    offer.Affordable ? HudTone.Default : HudTone.Muted, HudTextStyle.Bold);
                into.Button(new HudBox(box.Right - 76f, box.Bottom - 30f, 68f, 26f), "BUY",
                    HudAction.Buy(offer.Type.Id), HudButtonStyle.Primary, offer.CanBuy);
            }
        }
    }
}
