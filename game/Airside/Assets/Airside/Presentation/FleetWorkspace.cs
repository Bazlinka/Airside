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
            string operatorName, string liveryHex, StatusSeverity severity, bool isPlayer,
            string baseCode = "ADL", bool isOutstation = false)
        {
            Registration = registration ?? string.Empty;
            TypeName = typeName ?? string.Empty;
            Status = status ?? string.Empty;
            Stand = stand ?? string.Empty;
            OperatorName = operatorName ?? string.Empty;
            LiveryHex = liveryHex ?? AirsidePalette.ConcreteHex;
            Severity = severity;
            IsPlayer = isPlayer;
            BaseCode = baseCode ?? string.Empty;
            IsOutstation = isOutstation;
        }

        public string Registration { get; }
        public string TypeName { get; }
        public string Status { get; }

        /// <summary>The right-hand column: the stand at Adelaide, or the base code for an outstation aircraft.</summary>
        public string Stand { get; }

        public string OperatorName { get; }
        public string LiveryHex { get; }
        public StatusSeverity Severity { get; }
        public bool IsPlayer { get; }
        public string BaseCode { get; }
        public bool IsOutstation { get; }

        public HudTone StatusTone => Status.IndexOf("delay", StringComparison.OrdinalIgnoreCase) >= 0
                                     || Status.IndexOf("late", StringComparison.OrdinalIgnoreCase) >= 0
            ? HudTone.Negative
            : Status.StartsWith("Ready", StringComparison.OrdinalIgnoreCase)
              || Status.StartsWith("Completed", StringComparison.OrdinalIgnoreCase)
                ? HudTone.Positive
                : Severity >= StatusSeverity.Attention ? HudTone.Caution : HudTone.Default;
    }

    /// <summary>
    /// One line of the roster as drawn: a base heading or an aircraft row. Headings appear only when the
    /// shown aircraft span more than one base.
    /// </summary>
    public readonly struct FleetRosterSlot
    {
        public FleetRosterSlot(bool isHeader, int row, string text)
        {
            IsHeader = isHeader;
            Row = row;
            Text = text ?? string.Empty;
        }

        public bool IsHeader { get; }

        /// <summary>The index into <see cref="FleetWorkspaceModel.Mine"/>; unused for a heading.</summary>
        public int Row { get; }

        public string Text { get; }
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
    /// The Fleet workspace as data (ADR 0057, 0239): every aircraft the player owns at every base, grouped by
    /// base and filtered or sorted as asked, the profile of whichever is selected, the other operators on the
    /// field, and the aircraft market under <see cref="AircraftAcquisition"/> rules for the chosen base.
    /// </summary>
    public sealed class FleetWorkspaceModel
    {
        public const int RoutesPerPage = 4;

        private readonly List<FleetRosterRow> _mine = new();
        private readonly List<FleetRosterSlot> _mineSlots = new();
        private readonly List<FleetRosterRow> _others = new();
        private readonly List<FleetMarketOffer> _market = new();
        private readonly List<OperationsPrepCheck> _prep = new();
        private readonly List<string> _capability = new();
        private readonly List<string> _capabilityIcons = new();
        private readonly List<PlayerFleetEntry> _entries = new();
        private readonly List<PlayerFleetEntry> _shown = new();
        private readonly List<FleetBaseChip> _bases = new();
        private readonly List<FleetRouteRow> _routes = new();

        public string Title => "FLEET";
        public string Subtitle { get; private set; } = string.Empty;

        /// <summary>The player's aircraft that pass the board's filters, grouped by base and sorted.</summary>
        public IReadOnlyList<FleetRosterRow> Mine => _mine;

        /// <summary>The lines the roster draws for <see cref="Mine"/>: base headings between the rows when needed.</summary>
        public IReadOnlyList<FleetRosterSlot> MineSlots => _mineSlots;

        public IReadOnlyList<FleetRosterRow> Others => _others;
        public IReadOnlyList<FleetMarketOffer> Market => _market;

        /// <summary>Adelaide and each outstation candidate, with what is based there.</summary>
        public IReadOnlyList<FleetBaseChip> Bases => _bases;

        /// <summary>The next base's unmet requirement, said once beside the bases caption; empty when none.</summary>
        public string OpenHint { get; private set; } = string.Empty;

        public string StatusFilterLabel { get; private set; } = "ALL";
        public string SortLabel { get; private set; } = "FLEET";

        /// <summary>How many aircraft the player owns in all, before any filter.</summary>
        public int OwnedCount { get; private set; }

        /// <summary>Where BUY delivers: the filtered base when it is open, otherwise Adelaide.</summary>
        public string BuyBase { get; private set; } = "ADL";

        public string MarketCaption { get; private set; } = "AIRCRAFT MARKET";

        public bool HasSelection { get; private set; }
        public string SelectedRegistration { get; private set; } = string.Empty;
        public string SelectedTypeName { get; private set; } = string.Empty;
        public bool SelectedIsPlayer { get; private set; }
        public bool SelectedIsOutstation { get; private set; }
        public string SelectedBaseCode { get; private set; } = string.Empty;

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

        /// <summary>The action the check button dispatches: the Adelaide check or an outstation's.</summary>
        public string StartCheckAction { get; private set; } = HudAction.StartCheck;

        /// <summary>ADR 0194: the aircraft's role, and the refit that changes it.</summary>
        public string RoleLine { get; private set; } = string.Empty;
        public bool CanChangeRole { get; private set; }
        public string ChangeRoleLabel { get; private set; } = string.Empty;

        public bool CanSell { get; private set; }
        public string SellLabel { get; private set; } = string.Empty;

        /// <summary>Why the selected aircraft cannot be sold right now; empty when it can or has no resale value.</summary>
        public string SellHint { get; private set; } = string.Empty;

        public bool HasMoveAction { get; private set; }
        public bool CanMoveBase { get; private set; }
        public string MoveBaseLabel { get; private set; } = string.Empty;
        public string MoveHint { get; private set; } = string.Empty;

        /// <summary>The destinations an idle outstation aircraft could be sent to, best profit first.</summary>
        public IReadOnlyList<FleetRouteRow> OutstationRoutes => _routes;

        public int RoutePage { get; private set; }
        public int RoutePageCount { get; private set; }
        public bool RepeatUnlocked { get; private set; }
        public string RepeatNote { get; private set; } = string.Empty;
        public bool HasRepeat { get; private set; }

        /// <summary>Why an outstation aircraft shows no routes (planned flight, check, due); empty when it lists them.</summary>
        public string RouteBlockedNote { get; private set; } = string.Empty;

        /// <summary>The cameras; filled by the HUD after <see cref="Rebuild(AirlineOperations, SimulationTime, string, FleetBoardState)"/>.</summary>
        public FleetCameraAvailability Camera { get; } = new();

        /// <summary>Lines the roster draws, with the other-operators section when shown.</summary>
        public int RosterSlotCount(bool showOtherOperators) =>
            _mineSlots.Count + (showOtherOperators && _others.Count > 0 ? _others.Count + 1 : 0);

        public void Rebuild(AirlineOperations operations, SimulationTime now, string selectedRegistration) =>
            Rebuild(operations, now, selectedRegistration, null);

        public void Rebuild(AirlineOperations operations, SimulationTime now, string selectedRegistration,
            FleetBoardState board)
        {
            board ??= new FleetBoardState();
            _mine.Clear();
            _mineSlots.Clear();
            _others.Clear();
            _market.Clear();
            _prep.Clear();
            _capability.Clear();
            _capabilityIcons.Clear();
            _bases.Clear();
            _routes.Clear();
            _entries.Clear();
            _shown.Clear();
            Camera.Clear();
            SelectedThumbnail = string.Empty;
            SelectedLiveryHex = string.Empty;
            HasSelection = false;
            SelectedRegistration = string.Empty;
            SelectedTypeName = string.Empty;
            SelectedIsPlayer = false;
            SelectedIsOutstation = false;
            SelectedBaseCode = string.Empty;
            AssignmentLine = string.Empty;
            AssignmentDetail = string.Empty;
            PrimaryAction = AircraftHudAction.None;
            PrimaryActionLabel = string.Empty;
            CanTrack = false;
            CanStartCheck = false;
            StartCheckLabel = string.Empty;
            StartCheckAction = HudAction.StartCheck;
            RoleLine = string.Empty;
            CanChangeRole = false;
            ChangeRoleLabel = string.Empty;
            CanSell = false;
            SellLabel = string.Empty;
            SellHint = string.Empty;
            HasMoveAction = false;
            CanMoveBase = false;
            MoveBaseLabel = string.Empty;
            MoveHint = string.Empty;
            RoutePage = 0;
            RoutePageCount = 0;
            RepeatUnlocked = false;
            RepeatNote = string.Empty;
            HasRepeat = false;
            RouteBlockedNote = string.Empty;
            OpenHint = string.Empty;
            OwnedCount = 0;
            BuyBase = "ADL";
            MarketCaption = "AIRCRAFT MARKET";
            StatusFilterLabel = FleetBoardState.StatusLabel(board.Status);
            SortLabel = FleetBoardState.SortLabel(board.Sort);
            if (operations == null)
                return;

            var clock = operations.Clock ?? AirlineClock.Default;
            var player = operations.PlayerAirline;
            var baseLevel = operations.CareerState.BaseLevel;
            FleetAircraft selectedLive = null;
            PlayerFleetEntry selectedEntry = null;

            foreach (var aircraft in operations.Fleet)
            {
                if (player == null || !ReferenceEquals(aircraft.Airline, player))
                    _others.Add(OtherRow(aircraft, now, baseLevel));
                if (aircraft.Registration == selectedRegistration)
                    selectedLive = aircraft;
            }

            PlayerFleet.Entries(operations, now, _entries);
            OwnedCount = _entries.Count;
            var liveCount = 0;
            var outstationCount = 0;
            foreach (var entry in _entries)
            {
                if (entry.IsOutstation)
                    outstationCount++;
                else
                    liveCount++;
                if (entry.Registration == selectedRegistration)
                    selectedEntry = entry;
                if (board.Passes(entry))
                    _shown.Add(entry);
            }

            _shown.Sort(FleetEntryComparer.For(board.Sort));
            FillRoster(operations, now, clock, baseLevel);

            var freeBays = 0;
            foreach (var _ in operations.FreeStands())
                freeBays++;
            var playerBase = operations.CareerState.Base;
            Subtitle = $"{liveCount} of {playerBase.FleetCapacity} aircraft"
                       + $"  ·  {Plural(freeBays, "regional stand")} free"
                       + $"  ·  {playerBase.Title}";
            if (outstationCount > 0)
                Subtitle += $"  ·  {outstationCount} based away";

            FillBases(operations, board, liveCount);
            FillMarket(operations, freeBays, liveCount);

            if (selectedEntry != null && selectedEntry.IsOutstation)
                FillOutstationSelection(operations, selectedEntry, now, clock, board);
            else if (selectedLive != null)
                FillSelection(operations, selectedLive, now, clock);
        }

        private void FillRoster(AirlineOperations operations, SimulationTime now, AirlineClock clock,
            PlayerBaseLevel baseLevel)
        {
            var grouped = false;
            for (var i = 1; i < _shown.Count; i++)
                if (_shown[i].BaseCode != _shown[0].BaseCode)
                    grouped = true;

            var livery = operations.PlayerAirline?.LiveryHex ?? AirsidePalette.ConcreteHex;
            string lastBase = null;
            for (var i = 0; i < _shown.Count; i++)
            {
                var entry = _shown[i];
                if (grouped && entry.BaseCode != lastBase)
                {
                    lastBase = entry.BaseCode;
                    var inGroup = 0;
                    foreach (var other in _shown)
                        if (other.BaseCode == entry.BaseCode)
                            inGroup++;
                    _mineSlots.Add(new FleetRosterSlot(true, -1,
                        FleetStatusText.NameOf(entry.BaseCode).ToUpperInvariant() + "  ·  " + inGroup));
                }

                _mine.Add(PlayerRow(entry, now, clock, baseLevel, livery));
                _mineSlots.Add(new FleetRosterSlot(false, _mine.Count - 1, string.Empty));
            }
        }

        private static FleetRosterRow PlayerRow(PlayerFleetEntry entry, SimulationTime now, AirlineClock clock,
            PlayerBaseLevel baseLevel, string liveryHex)
        {
            var status = FleetStatusText.For(entry, now, clock, baseLevel);
            var severity = FleetStatusText.SeverityFor(entry, now);
            if (entry.IsOutstation)
                return new FleetRosterRow(entry.Registration, entry.Type.Name, status, entry.BaseCode, string.Empty,
                    liveryHex, severity, true, entry.BaseCode, true);
            var aircraft = entry.Live;
            var stand = string.IsNullOrEmpty(aircraft.Stand.Value) ? "—" : AdelaideGround.StandLabel(aircraft.Stand);
            return new FleetRosterRow(aircraft.Registration, aircraft.Type.Name, status, stand,
                aircraft.Airline.Name, aircraft.Airline.LiveryHex, severity, true, entry.BaseCode, false);
        }

        private static FleetRosterRow OtherRow(FleetAircraft aircraft, SimulationTime now, PlayerBaseLevel baseLevel)
        {
            var stand = string.IsNullOrEmpty(aircraft.Stand.Value)
                ? "—"
                : AdelaideGround.StandLabel(aircraft.Stand);
            return new FleetRosterRow(
                aircraft.Registration,
                aircraft.Type.Name,
                FleetStatusText.ForLive(aircraft, now, baseLevel),
                stand,
                aircraft.Airline.Name,
                aircraft.Airline.LiveryHex,
                AircraftStatus.Severity(aircraft, now),
                aircraft.Airline.IsPlayer);
        }

        private void FillBases(AirlineOperations operations, FleetBoardState board, int liveCount)
        {
            var career = operations.CareerState;
            _bases.Add(new FleetBaseChip("ADL", "Adelaide", true, liveCount, career.Base.FleetCapacity,
                board.BaseFilter == "ADL", 0, false, string.Empty));

            var requirement = operations.NextOutstationRequirement();
            var cost = operations.NextOutstationCost;
            var threeOpen = career.OutstationBases.Count >= 3;
            var anyClosed = false;
            foreach (var code in AirlineOperations.OutstationCandidates)
            {
                var open = career.HasOutstationBase(code);
                var based = 0;
                foreach (var entry in _entries)
                    if (entry.IsOutstation && entry.BaseCode == code)
                        based++;
                var reason = open ? string.Empty : threeOpen ? "Three outstations is the limit." : requirement;
                var canOpen = !open && reason.Length == 0 && career.CanAfford(cost);
                if (!open)
                    anyClosed = true;
                _bases.Add(new FleetBaseChip(code, FleetStatusText.NameOf(code), open, based,
                    AirlineOperations.OutstationCapacity, open && board.BaseFilter == code, cost, canOpen, reason));
            }

            if (anyClosed)
                OpenHint = threeOpen ? "Three outstations is the limit" : requirement.Length > 0
                    ? "Next base: " + requirement.TrimEnd('.')
                    : career.CanAfford(cost) ? string.Empty : $"Next base costs ${cost:N0}";
        }

        private void FillMarket(AirlineOperations operations, int freeBays, int liveCount)
        {
            var career = operations.CareerState;
            var buyBase = "ADL";
            foreach (var chip in _bases)
                if (chip.Selected && chip.IsOpen)
                    buyBase = chip.Code;
            BuyBase = buyBase;
            var atHome = buyBase == "ADL";
            if (!atHome)
                MarketCaption = "AIRCRAFT MARKET  ·  DELIVERED TO " + FleetStatusText.NameOf(buyBase).ToUpperInvariant();

            var fleetFull = operations.PlayerFleetCount() >= AircraftAcquisition.MaxPlayerAircraft;
            var baseFull = liveCount >= career.Base.FleetCapacity;

            foreach (var offer in AircraftAcquisition.All)
            {
                var spec = AircraftCatalogue.For(offer.Type);
                var affordable = career.CanAfford(offer.Price);
                bool unlocked;
                string requirement;
                string standLine;
                var reach = RouteAccess.ExampleDestinations(offer.Operates);
                var suffix = offer.Operates == RouteBand.Regional ? "" : ", +closer";
                var readyLine = $"Ready to buy · flies {RouteMapWorkspaceModel.BandLabel(offer.Operates).ToLowerInvariant()} "
                                + $"routes ({reach}{suffix})";

                if (atHome)
                {
                    var baseSupports = PlayerBase.Supports(career.BaseLevel, offer.Type);
                    unlocked = career.Tier >= offer.RequiredTier
                               && career.Reliability >= offer.RequiredReliability
                               && career.CompletedPlayerRotations >= offer.RequiredRotations
                               && baseSupports && !baseFull;

                    // "Requires Regional tier" (a career milestone, OperatingTier) and "flies
                    // Regional routes" (a RouteBand) share the word "Regional" for two unrelated
                    // systems — the tier line is spelled out as "career tier" and the route line
                    // names real destinations instead of leaving the band as a bare label, so
                    // buying a plane answers "where can it fly" concretely, not abstractly.
                    if (fleetFull)
                        requirement = $"Your fleet is full at {AircraftAcquisition.MaxPlayerAircraft} aircraft";
                    else if (baseFull)
                        requirement = career.BaseLevel == PlayerBaseLevel.International
                            ? "Adelaide is full. Pick an outstation base above"
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
                        requirement = readyLine;

                    var needsGate = AirlineOperations.NeedsTerminalGate(offer.Type);
                    if (needsGate)
                        standLine = "Arrives at a free gate, or is flown in";
                    else if (freeBays > 1)
                        standLine = "Arrives on a free regional bay";
                    else
                        standLine = "No bay free, so it is flown in";
                }
                else
                {
                    // Same gates the simulation applies to a purchase at an outstation, asked of the simulation
                    // itself so the card can never offer what the command would refuse.
                    var refusal = operations.PurchaseRefusal(offer.Type, buyBase, ignoreFunds: true);
                    unlocked = refusal == null;
                    requirement = refusal != null ? refusal
                        : !affordable ? $"Costs ${offer.Price:N0}. You have ${career.Funds:N0}"
                        : readyLine;
                    standLine = "Delivered to " + FleetStatusText.NameOf(buyBase) + " and flies from there";
                }

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
                    requirement, standLine, affordable, unlocked, fleetFull || (atHome && baseFull), cashWarning));
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
            SelectedBaseCode = aircraft.Airline.IsPlayer ? operations.Home.Code : string.Empty;

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
            else if (operations.CanResell(aircraft) && AircraftAcquisition.TryFor(aircraft.Type, out _))
                AddFact("economy/cash", $"Sells for ${AirlineOperations.ResaleValue(aircraft.Type):N0}");

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
                if (aircraft.IsFerry)
                    AssignmentLine += "  ·  ferry";
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

                if (operations.CanResell(aircraft))
                {
                    var value = AirlineOperations.ResaleValue(aircraft.Type);
                    CanSell = aircraft.State == FleetState.AtStand && !aircraft.Scheduled.HasValue
                              && !Maintenance.InCheck(aircraft, now);
                    SellLabel = "SELL $" + value.ToString("N0");
                    if (!CanSell)
                        SellHint = aircraft.Scheduled.HasValue ? "Cancel its flight to sell"
                            : Maintenance.InCheck(aircraft, now) ? "Sellable after its check"
                            : "Sellable once parked";
                }
            }
        }

        /// <summary>The profile of an aircraft based away from Adelaide: the same story, plus the routes it can fly.</summary>
        private void FillOutstationSelection(AirlineOperations operations, PlayerFleetEntry entry,
            SimulationTime now, AirlineClock clock, FleetBoardState board)
        {
            var aircraft = entry.Outstation;
            HasSelection = true;
            SelectedRegistration = entry.Registration;
            SelectedTypeName = entry.Type.Name;
            SelectedIsPlayer = true;
            SelectedIsOutstation = true;
            SelectedBaseCode = entry.BaseCode;
            SelectedThumbnail = FleetWorkspacePainter.Thumbnail(entry.Type);
            SelectedLiveryHex = operations.PlayerAirline?.LiveryHex ?? string.Empty;
            var baseName = FleetStatusText.NameOf(entry.BaseCode);

            AddFact("operation/stand", $"Based at {baseName} · off the Adelaide map");
            var ceiling = RouteAccess.Ceiling(entry.Type);
            var ceilingSuffix = ceiling == RouteBand.Regional ? "" : ", +closer";
            AddFact("operation/departure", $"Flies {RouteMapWorkspaceModel.BandLabel(ceiling).ToLowerInvariant()} routes "
                                           + $"({RouteAccess.ExampleDestinations(ceiling)}{ceilingSuffix})");
            var distinction = AircraftDistinctions.Current(aircraft.CompletedServices);
            var historyTitle = distinction.Flights > 0 ? distinction.Title : "New to the fleet";
            AddFact("operation/completed", $"{historyTitle} · {Plural(aircraft.CompletedServices, "flight")} flown");
            AddFact("economy/route", $"{entry.Type.PracticalRangeKm:#,0} km range");
            if (aircraft.JoinedAtSeconds > 0)
                AddFact("economy/reputation", "Joined the airline " + clock.LocalAt(new SimulationTime(aircraft.JoinedAtSeconds))
                    .ToString("d MMM yyyy", CultureInfo.InvariantCulture));
            if (aircraft.HistoryFlights > 0)
            {
                var recorded = $"${aircraft.LifetimeRevenue:N0} earned";
                var favourite = aircraft.FavouriteRoute;
                if (!string.IsNullOrEmpty(favourite.DestinationCode))
                    recorded += $" · {FleetStatusText.NameOf(favourite.DestinationCode)} {favourite.Flights}×";
                AddFact("economy/cash", recorded);
            }
            var next = AircraftDistinctions.Next(aircraft.CompletedServices);
            if (next.Flights > 0)
                AddFact("operation/completed",
                    $"Next distinction: {next.Title} · {next.Flights - aircraft.CompletedServices} flights to go");

            var capability = AirlineOperations.OutstationCheckCapability(entry.Type);
            var checkCost = Maintenance.CheckCost(entry.Type, capability);
            AddFact("service/inspection", PlayerBase.MaintenanceLine(capability, entry.Type)
                                          + " · $" + checkCost.ToString("N0")
                                          + " · " + (Maintenance.CheckSeconds(entry.Type, capability) / 3600.0).ToString("0.#") + " h");
            AddFact("operation/turnaround", entry.CheckDue
                ? "A check is due before its next flight"
                : $"{Plural(Math.Max(0, entry.RotationsUntilCheck), "flight")} until its next check");
            if (entry.ResaleValue > 0)
                AddFact("economy/cash", $"Sells for ${entry.ResaleValue:N0}");

            switch (entry.Kind)
            {
                case PlayerFleetKind.InCheck:
                    AssignmentLine = FleetStatusText.ForOutstation(entry, now, clock);
                    AssignmentDetail = baseName;
                    break;
                case PlayerFleetKind.Booked:
                case PlayerFleetKind.Airborne:
                    AssignmentLine = $"{baseName} → {FleetStatusText.NameOf(entry.DestinationCode)}";
                    AssignmentDetail = (entry.Kind == PlayerFleetKind.Booked ? "Departs " : "Back at base ")
                                       + clock.TimeText(new SimulationTime(entry.Kind == PlayerFleetKind.Booked
                                           ? entry.NextAtSeconds
                                           : aircraft.ReturnAtSeconds))
                                       + "  ·  " + FleetStatusText.ForOutstation(entry, now, clock);
                    break;
                default:
                    AssignmentLine = "No flight planned";
                    AssignmentDetail = baseName;
                    break;
            }

            CanStartCheck = entry.CheckDue && !aircraft.HasFlight && !entry.InCheck;
            StartCheckLabel = CanStartCheck ? "CHECK $" + checkCost.ToString("N0") : string.Empty;
            StartCheckAction = FleetActions.OutstationCheck;

            CanSell = entry.CanSell;
            if (entry.ResaleValue > 0)
            {
                SellLabel = "SELL $" + entry.ResaleValue.ToString("N0");
                if (!CanSell)
                    SellHint = aircraft.HasFlight ? "Sellable once its flight ends" : "Sellable after its check";
            }

            HasMoveAction = true;
            CanMoveBase = operations.CanRelocateToAdelaide(entry.Registration, out var moveReason);
            MoveBaseLabel = "TO ADELAIDE $" + operations.RelocationCost(aircraft).ToString("N0");
            MoveHint = CanMoveBase ? string.Empty : moveReason;

            RepeatUnlocked = operations.DelegationUnlocked;
            if (!RepeatUnlocked)
                RepeatNote = $"Repeat plans unlock after 12 flights you plan yourself ({operations.CareerState.ManualRotations}/12).";
            foreach (var plan in operations.RepeatSchedules)
                if (plan.Registration == entry.Registration)
                {
                    HasRepeat = true;
                    RepeatNote = plan.Exception.Length > 0
                        ? plan.Exception
                        : $"Repeats to {FleetStatusText.NameOf(plan.DestinationCode)} every {plan.IntervalHours} h while you play"
                          + (plan.Paused ? " (paused)" : string.Empty);
                }

            if (aircraft.HasFlight)
                RouteBlockedNote = "It is on a service. Routes open when it is back.";
            else if (entry.InCheck)
                RouteBlockedNote = "It is in its check.";
            else if (entry.CheckDue)
                RouteBlockedNote = "A check is due before its next flight.";
            else
                FillRoutes(operations, entry, board);
        }

        private void FillRoutes(AirlineOperations operations, PlayerFleetEntry entry, FleetBoardState board)
        {
            if (!DestinationCatalogue.TryFind(entry.BaseCode, out var origin))
                return;
            var scored = new List<(long Profit, FleetRouteRow Row)>();
            foreach (var destination in DestinationCatalogue.All)
            {
                // Adelaide movements belong to the live airport, so a network aircraft never works them.
                if (destination.Code == operations.Home.Code || destination.Code == entry.BaseCode)
                    continue;
                if (RouteAccess.BandOf(destination) >= RouteBand.Tasman
                    && operations.CareerState.Tier < OperatingTier.International)
                    continue;
                var km = origin.DistanceKmTo(destination);
                if (!entry.Type.CanReach(km) || !RouteAccess.Allows(entry.Type, destination))
                    continue;
                var forecast = operations.Forecast(origin, destination, entry.Type);
                var profit = forecast.Revenue - forecast.Cost;
                var repeats = false;
                var paused = false;
                foreach (var plan in operations.RepeatSchedules)
                    if (plan.Registration == entry.Registration && plan.DestinationCode == destination.Code)
                    {
                        repeats = true;
                        paused = plan.Paused;
                    }

                var sign = profit >= 0 ? "+" : "−";
                scored.Add((profit, new FleetRouteRow(destination.Code, destination.Name, (int)Math.Round(km),
                    $"{sign}${Math.Abs(profit):N0}", profit >= 0, repeats, paused)));
            }

            scored.Sort((a, b) => b.Profit.CompareTo(a.Profit));
            foreach (var item in scored)
                _routes.Add(item.Row);
            RoutePageCount = Math.Max(1, (_routes.Count + RoutesPerPage - 1) / RoutesPerPage);
            RoutePage = Math.Max(0, Math.Min(board.RoutePage, RoutePageCount - 1));
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

    /// <summary>Where the Fleet workspace draws its bases strip, roster, detail pane and market strip.</summary>
    public readonly struct FleetWorkspaceLayout
    {
        public const float RosterRowHeight = 44f;
        public const float SectionCaptionHeight = 18f;
        public const float DetailGap = 24f;
        public const float MarketRowHeight = 92f;
        public const float MarketCaptionHeight = 20f;
        public const float MinRosterWidth = 260f;
        public const float MinDetailWidth = 300f;

        /// <summary>The roster's two toolbar rows: the caption row, then the filter and sort buttons.</summary>
        public const float RosterHeaderHeight = 62f;

        public const float BasesCaptionHeight = 18f;
        public const float BaseChipHeight = 38f;
        public const float BasesStripHeight = BasesCaptionHeight + 4f + BaseChipHeight;

        /// <summary>The bases strip needs this much body height; a short window drops it for the roster.</summary>
        public const float BasesStripMinBodyHeight = 460f;

        private FleetWorkspaceLayout(HudBox surface, HudBox header, HudBox bases, HudBox roster, HudBox detail,
            HudBox market, HudBox divider, int marketRows)
        {
            Surface = surface;
            Header = header;
            Bases = bases;
            Roster = roster;
            Detail = detail;
            Market = market;
            Divider = divider;
            MarketRows = marketRows;
        }

        public HudBox Surface { get; }
        public HudBox Header { get; }

        /// <summary>The bases strip along the top of the body. Empty when there is no room.</summary>
        public HudBox Bases { get; }

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

        /// <summary>The status filter and sort buttons, under the caption row.</summary>
        public HudBox RosterFilters => new(Roster.X, Roster.Y + 32f, Roster.Width, 24f);

        public HudBox BasesCaption => Bases.IsEmpty ? HudBox.Empty : Bases.WithHeight(BasesCaptionHeight);

        public HudBox BaseChip(int index, int count) => Bases.IsEmpty || count <= 0
            ? HudBox.Empty
            : new HudBox(Bases.X + index * ((Bases.Width + 8f) / count), Bases.Y + BasesCaptionHeight + 4f,
                (Bases.Width + 8f) / count - 8f, BaseChipHeight);

        public HudBox RosterRow(int index) =>
            new(Roster.X, Roster.Y + RosterHeaderHeight + index * RosterRowHeight, Roster.Width, RosterRowHeight - 2f);

        public int VisibleRosterRows => Roster.Height <= RosterHeaderHeight
            ? 0
            : (int)((Roster.Height - RosterHeaderHeight) / RosterRowHeight);

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
            var wholeBody = HudShell.Body(surface, hasFooter: false);

            var stripHeight = wholeBody.Height >= BasesStripMinBodyHeight ? BasesStripHeight + 10f : 0f;
            var bases = stripHeight == 0f
                ? HudBox.Empty
                : new HudBox(wholeBody.X, wholeBody.Y, wholeBody.Width, BasesStripHeight);
            var body = new HudBox(wholeBody.X, wholeBody.Y + stripHeight, wholeBody.Width,
                wholeBody.Height - stripHeight);

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

            return new FleetWorkspaceLayout(surface, header, bases, roster, detail, market, divider, rows);
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

            PaintBases(into, model, layout);
            PaintRoster(into, model, layout, selectedRegistration, scrollRow, showOtherOperators);
            if (!layout.Divider.IsEmpty)
                into.Hairline(layout.Divider);
            PaintDetail(into, model, layout);
            PaintMarket(into, model, layout, marketStart);
        }

        /// <summary>Clamps a market page start so the last page is full where it can be.</summary>
        public static int ClampMarketStart(int start, int offers, int rows) =>
            rows <= 0 || offers <= rows ? 0 : Math.Max(0, Math.Min(start, offers - rows));

        private static void PaintBases(HudDrawList into, FleetWorkspaceModel model, FleetWorkspaceLayout layout)
        {
            if (layout.Bases.IsEmpty || model.Bases.Count == 0)
                return;

            var caption = layout.BasesCaption;
            into.Caption(caption.WithWidth(Math.Min(120f, caption.Width)), "BASES");
            if (model.OpenHint.Length > 0 && caption.Width > 260f)
                into.Text(new HudBox(caption.X + 90f, caption.Y, caption.Width - 90f, caption.Height), model.OpenHint,
                    11f, HudTone.Caution, HudTextStyle.Regular, HudAlign.Right);

            for (var i = 0; i < model.Bases.Count; i++)
            {
                var chip = model.Bases[i];
                var box = layout.BaseChip(i, model.Bases.Count);
                if (box.IsEmpty)
                    continue;
                var alpha = chip.IsOpen ? 1f : 0.55f;
                into.Fill(box, HudTone.Default, chip.Selected ? 0.12f : 0.05f);
                if (chip.Selected)
                    into.Outline(box, HudTone.Accent, 0.9f);
                else if (chip.CanOpen)
                    into.Outline(box, HudTone.Accent, 0.45f);

                into.Text(new HudBox(box.X + 8f, box.Y + 4f, box.Width * 0.5f, 16f), chip.Code, 13f,
                    chip.IsOpen ? HudTone.Default : HudTone.Muted, HudTextStyle.Bold, alpha: alpha);
                if (chip.IsOpen)
                    into.Text(new HudBox(box.X + box.Width * 0.45f, box.Y + 5f, box.Width * 0.55f - 8f, 16f),
                        $"{chip.Aircraft}/{chip.Capacity}", 12f,
                        chip.Aircraft >= chip.Capacity ? HudTone.Caution : HudTone.Muted, HudTextStyle.Regular,
                        HudAlign.Right);
                var second = chip.IsOpen
                    ? chip.Name
                    : chip.CanOpen ? $"Open · ${chip.OpenCost:N0}" : $"${chip.OpenCost:N0}";
                into.Text(new HudBox(box.X + 8f, box.Y + 21f, box.Width - 16f, 14f), second, 11f,
                    chip.CanOpen ? HudTone.Accent : HudTone.Muted, alpha: alpha);

                if (chip.IsOpen)
                    into.Hotspot(box, FleetActions.Base(chip.Code));
                else if (chip.CanOpen)
                    into.Hotspot(box, FleetActions.OpenBase(chip.Code));
            }
        }

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

            // Show and sort: two cycling buttons, so a long fleet is read the way the player asks for it.
            var filters = layout.RosterFilters;
            if (!filters.IsEmpty && layout.VisibleRosterRows > 0)
            {
                var half = (filters.Width - 8f) * 0.5f;
                into.Button(new HudBox(filters.X, filters.Y, half, 24f), "SHOW · " + model.StatusFilterLabel,
                    FleetActions.CycleStatus, HudButtonStyle.Secondary);
                into.Button(new HudBox(filters.X + half + 8f, filters.Y, half, 24f), "SORT · " + model.SortLabel,
                    FleetActions.CycleSort, HudButtonStyle.Secondary);
            }

            var index = 0;
            var drawn = 0;
            var capacity = layout.VisibleRosterRows;
            var skip = scrollRow < 0 ? 0 : scrollRow;

            if (model.Mine.Count == 0 && capacity > 0)
                into.Text(layout.RosterRow(0).Inset(4f, 8f, 4f, 0f),
                    model.OwnedCount == 0 ? "No aircraft yet." : "No aircraft match this filter.", 12f, HudTone.Muted);

            // Your own aircraft always come first, grouped by base; other operators follow, quieter.
            for (var i = 0; i < model.MineSlots.Count; i++)
            {
                if (index++ < skip)
                    continue;
                if (drawn >= capacity)
                    return;
                var slot = model.MineSlots[i];
                if (slot.IsHeader)
                    into.Caption(layout.RosterRow(drawn++).Inset(0f, 14f, 0f, 0f), slot.Text);
                else
                    PaintRosterRow(into, layout, model.Mine[slot.Row], drawn++, selectedRegistration, quiet: false);
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

        private const float ActionRowHeight = 30f;
        private const float ActionPrimaryHeight = 34f;
        private const float ActionGap = 6f;

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

            var chipWidth = model.SelectedBaseCode.Length > 0 && pane.Width >= 340f ? 60f : 0f;
            into.Text(new HudBox(pane.X, top, pane.Width - chipWidth, 28f),
                $"{model.SelectedRegistration}  ·  {model.SelectedTypeName}", 20f, HudTone.Default,
                HudTextStyle.Bold);
            if (chipWidth > 0f)
                into.Pill(new HudBox(pane.Right - chipWidth, top + 4f, chipWidth, 20f), model.SelectedBaseCode,
                    HudTone.Accent);

            if (!model.SelectedIsPlayer)
            {
                var y0 = PaintFacts(into, model, pane, top + 38f, pane.Bottom - 90f);
                y0 = PaintNow(into, model, pane, y0 + 4f);
                into.Text(new HudBox(pane.X, Math.Min(y0, pane.Bottom - 84f), pane.Width, 36f),
                    "Another airline's aircraft. You can see it but not give it orders.", 12f,
                    HudTone.Muted, HudTextStyle.Wrap);
                // Selecting a row stays in the sheet now, so following an aircraft is its own button.
                into.Button(new HudBox(pane.X, pane.Bottom - ActionPrimaryHeight, Math.Min(pane.Width, 160f),
                    ActionPrimaryHeight), "TRACK", HudAction.Track, HudButtonStyle.Secondary);
                return;
            }

            // Actions sit at the bottom of the pane in a fixed order of importance. A row that does not fit
            // is dropped, lowest priority first, rather than drawn over the text above it.
            var nowHeight = 68f + (model.SelectedPrep.Count > 0 ? 56f : 0f);
            var minimumContent = (top - pane.Y) + 38f + 3f * 21f + nowHeight;
            var budget = pane.Height - minimumContent;

            var showPrimary = model.PrimaryAction != AircraftHudAction.None || model.CanStartCheck || model.CanTrack;
            var showSell = model.SellLabel.Length > 0 || model.HasMoveAction;
            var showRole = model.RoleLine.Length > 0 && !model.SelectedIsOutstation;
            var showCamera = model.Camera.Visible;
            var used = 0f;
            if (showPrimary && used + ActionPrimaryHeight + ActionGap <= budget)
                used += ActionPrimaryHeight + ActionGap;
            else
                showPrimary = false;
            if (showSell && used + ActionRowHeight + ActionGap <= budget)
                used += ActionRowHeight + ActionGap;
            else
                showSell = false;
            if (showRole && used + ActionRowHeight + ActionGap <= budget)
                used += ActionRowHeight + ActionGap;
            else
                showRole = false;
            if (showCamera && used + ActionRowHeight + ActionGap <= budget)
                used += ActionRowHeight + ActionGap;
            else
                showCamera = false;

            var actionTop = pane.Bottom - used;
            var content = PaintFacts(into, model, pane, top + 38f, actionTop - nowHeight - 12f);
            content = PaintNow(into, model, pane, content + 6f);

            if (model.SelectedPrep.Count > 0)
                content = PaintPrep(into, model, pane, content);
            else if (model.SelectedIsOutstation)
                PaintRoutes(into, model, pane, content, actionTop - 6f);

            var y = actionTop;
            var half = (pane.Width - 10f) * 0.5f;
            if (showPrimary)
            {
                if (model.PrimaryAction != AircraftHudAction.None)
                    into.Button(new HudBox(pane.X, y, half, ActionPrimaryHeight), model.PrimaryActionLabel,
                        HudAction.Primary, HudButtonStyle.Primary);
                if (model.CanStartCheck)
                {
                    // Alone (an outstation aircraft has no primary action) the check takes the whole row.
                    var alone = model.PrimaryAction == AircraftHudAction.None;
                    into.Button(new HudBox(alone ? pane.X : pane.X + half + 10f, y, alone ? pane.Width : half,
                            ActionPrimaryHeight), model.StartCheckLabel, model.StartCheckAction,
                        alone ? HudButtonStyle.Primary : HudButtonStyle.Secondary);
                }
                else if (model.CanTrack)
                    into.Button(new HudBox(pane.X + half + 10f, y, half, ActionPrimaryHeight), "TRACK", HudAction.Track,
                        HudButtonStyle.Secondary);
                y += ActionPrimaryHeight + ActionGap;
            }

            if (showSell)
            {
                if (model.HasMoveAction && model.SellLabel.Length > 0)
                {
                    into.Button(new HudBox(pane.X, y, half, ActionRowHeight), model.MoveBaseLabel, FleetActions.MoveBase,
                        HudButtonStyle.Secondary, model.CanMoveBase);
                    into.Button(new HudBox(pane.X + half + 10f, y, half, ActionRowHeight), model.SellLabel,
                        FleetActions.Sell, HudButtonStyle.Secondary, model.CanSell);
                }
                else if (model.HasMoveAction)
                    into.Button(new HudBox(pane.X, y, pane.Width, ActionRowHeight), model.MoveBaseLabel,
                        FleetActions.MoveBase, HudButtonStyle.Secondary, model.CanMoveBase);
                else
                    into.Button(new HudBox(pane.X, y, pane.Width, ActionRowHeight), model.SellLabel, FleetActions.Sell,
                        HudButtonStyle.Secondary, model.CanSell);
                y += ActionRowHeight + ActionGap;
            }

            if (showRole)
            {
                into.Button(new HudBox(pane.X, y, pane.Width, ActionRowHeight), model.ChangeRoleLabel,
                    HudAction.ToggleFreighter, HudButtonStyle.Secondary, model.CanChangeRole);
                y += ActionRowHeight + ActionGap;
            }

            if (showCamera)
            {
                var third = (pane.Width - 12f) / 3f;
                var camera = model.Camera;
                into.Button(new HudBox(pane.X, y, third, ActionRowHeight), "COCKPIT", HudAction.CameraCockpit,
                    HudButtonStyle.Secondary, camera.Cockpit);
                into.Button(new HudBox(pane.X + third + 6f, y, third, ActionRowHeight), "WINDOW",
                    HudAction.CameraPassenger, HudButtonStyle.Secondary, camera.Window);
                into.Button(new HudBox(pane.X + (third + 6f) * 2f, y, third, ActionRowHeight), "EXTERIOR",
                    HudAction.CameraExterior, HudButtonStyle.Secondary, camera.Exterior);
            }
        }

        /// <summary>The capability and logbook lines, as many as fit above <paramref name="limit"/>; returns the next y.</summary>
        private static float PaintFacts(HudDrawList into, FleetWorkspaceModel model, HudBox pane, float y, float limit)
        {
            for (var i = 0; i < model.SelectedCapability.Count; i++)
            {
                if (y + 18f > limit)
                    break;
                var icon = i < model.SelectedCapabilityIcons.Count ? model.SelectedCapabilityIcons[i] : string.Empty;
                var slash = icon.IndexOf('/');
                if (slash > 0)
                    into.Icon(new HudBox(pane.X, y, 16f, 16f), icon.Substring(0, slash), icon.Substring(slash + 1),
                        HudTone.Accent);
                into.Text(new HudBox(pane.X + 24f, y, pane.Width - 24f, 18f), model.SelectedCapability[i], 13f);
                y += 21f;
            }

            return y;
        }

        private static float PaintNow(HudDrawList into, FleetWorkspaceModel model, HudBox pane, float y)
        {
            into.Hairline(new HudBox(pane.X, y, pane.Width, 1f));
            y += 10f;
            into.Caption(new HudBox(pane.X, y, pane.Width, 16f), "NOW");
            y += 18f;
            into.Text(new HudBox(pane.X, y, pane.Width, 20f), model.AssignmentLine, 14f, HudTone.Default,
                HudTextStyle.Bold);
            y += 22f;
            into.Text(new HudBox(pane.X, y, pane.Width, 18f), model.AssignmentDetail, 12f, HudTone.Muted);
            return y + 24f;
        }

        private static float PaintPrep(HudDrawList into, FleetWorkspaceModel model, HudBox pane, float y)
        {
            into.Hairline(new HudBox(pane.X, y, pane.Width, 1f));
            y += 10f;
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

            return y + 56f;
        }

        private const float RouteRowHeight = 30f;

        /// <summary>
        /// The routes an idle outstation aircraft could fly, best profit first, one click to send it. The rows that
        /// fit between <paramref name="y"/> and <paramref name="limit"/> are drawn; a pager steps through the rest.
        /// </summary>
        private static void PaintRoutes(HudDrawList into, FleetWorkspaceModel model, HudBox pane, float y, float limit)
        {
            if (model.RouteBlockedNote.Length > 0)
            {
                if (limit - y < 46f)
                    return;
                into.Hairline(new HudBox(pane.X, y, pane.Width, 1f));
                into.Text(new HudBox(pane.X, y + 10f, pane.Width, 36f), model.RouteBlockedNote, 12f, HudTone.Muted,
                    HudTextStyle.Wrap);
                return;
            }

            if (model.OutstationRoutes.Count == 0 || limit - y < 24f + RouteRowHeight)
                return;

            into.Hairline(new HudBox(pane.X, y, pane.Width, 1f));
            y += 8f;
            into.Caption(new HudBox(pane.X, y, pane.Width * 0.6f, 16f), "SEND ON A ROUTE");
            if (model.RoutePageCount > 1 && pane.Width >= 300f)
            {
                var pager = new HudBox(pane.Right - 110f, y - 4f, 110f, 22f);
                into.Button(pager.WithWidth(28f), "‹", FleetActions.RoutesPrevious, HudButtonStyle.Secondary,
                    model.RoutePage > 0);
                into.Text(new HudBox(pager.X + 30f, pager.Y + 3f, 50f, 16f),
                    $"{model.RoutePage + 1}/{model.RoutePageCount}", 11f, HudTone.Muted, HudTextStyle.Regular,
                    HudAlign.Center);
                into.Button(new HudBox(pager.Right - 28f, pager.Y, 28f, 22f), "›", FleetActions.RoutesNext,
                    HudButtonStyle.Secondary, model.RoutePage + 1 < model.RoutePageCount);
            }

            y += 22f;
            var rows = Math.Min(FleetWorkspaceModel.RoutesPerPage, (int)((limit - y) / RouteRowHeight));
            var first = model.RoutePage * FleetWorkspaceModel.RoutesPerPage;
            var buttonWidth = 62f;
            var repeatWidth = model.RepeatUnlocked ? 70f : 0f;
            for (var i = 0; i < rows && first + i < model.OutstationRoutes.Count; i++)
            {
                var route = model.OutstationRoutes[first + i];
                var row = new HudBox(pane.X, y + i * RouteRowHeight, pane.Width, RouteRowHeight - 2f);
                if ((i & 1) == 0)
                    into.Fill(row, HudTone.Default, 0.03f);
                var textWidth = row.Width - buttonWidth - repeatWidth - 12f;
                into.Text(new HudBox(row.X + 6f, row.Y + 6f, textWidth * 0.55f, 16f), route.Name, 12f, HudTone.Default,
                    HudTextStyle.Bold);
                into.Text(new HudBox(row.X + 6f + textWidth * 0.55f, row.Y + 6f, textWidth * 0.45f, 16f),
                    route.Forecast, 12f, route.Profitable ? HudTone.Positive : HudTone.Caution, HudTextStyle.Regular,
                    HudAlign.Right);
                var x = row.Right - buttonWidth;
                into.Button(new HudBox(x, row.Y + 2f, buttonWidth, row.Height - 4f), "SEND",
                    FleetActions.Route(route.Code), HudButtonStyle.Primary);
                if (repeatWidth > 0f)
                    into.Button(new HudBox(x - repeatWidth - 4f, row.Y + 2f, repeatWidth, row.Height - 4f),
                        route.Repeats ? (route.RepeatPaused ? "RESUME" : "PAUSE") : "REPEAT",
                        FleetActions.Repeat(route.Code), HudButtonStyle.Secondary);
            }

            var noteY = y + rows * RouteRowHeight + 2f;
            if (model.RepeatNote.Length > 0 && noteY + 16f <= limit)
            {
                var removeWidth = model.HasRepeat ? 70f : 0f;
                into.Text(new HudBox(pane.X, noteY, pane.Width - removeWidth - 6f, 16f), model.RepeatNote, 11f,
                    HudTone.Muted);
                if (removeWidth > 0f)
                    into.Button(new HudBox(pane.Right - removeWidth, noteY - 3f, removeWidth, 22f), "REMOVE",
                        FleetActions.RemoveRepeat, HudButtonStyle.Secondary);
            }
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
            var pagerWidth = model.Market.Count > layout.MarketRows ? 160f : 0f;
            into.Caption(layout.MarketCaption.WithWidth(Math.Max(0f, layout.MarketCaption.Width - pagerWidth)),
                model.MarketCaption);
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
