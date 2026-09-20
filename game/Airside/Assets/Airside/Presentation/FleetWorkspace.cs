using System;
using System.Collections.Generic;
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

        public HudTone StatusTone => Severity switch
        {
            StatusSeverity.Warning => HudTone.Negative,
            StatusSeverity.Attention => HudTone.Caution,
            _ => HudTone.Default
        };
    }

    /// <summary>
    /// One aircraft the player could buy. Every value is read from
    /// <see cref="AircraftAcquisition"/> and current career state — no invented
    /// maintenance, wear or upgrade economy.
    /// </summary>
    public readonly struct FleetMarketOffer
    {
        public FleetMarketOffer(AircraftType type, string typeName, string bandLabel, long price,
            string requirementLine, string standLine, bool affordable, bool unlocked, bool fleetFull)
        {
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

        public string AssignmentLine { get; private set; } = string.Empty;
        public string AssignmentDetail { get; private set; } = string.Empty;
        public IReadOnlyList<OperationsPrepCheck> SelectedPrep => _prep;
        public AircraftHudAction PrimaryAction { get; private set; }
        public string PrimaryActionLabel { get; private set; } = string.Empty;
        public bool CanTrack { get; private set; }

        public void Rebuild(AirlineOperations operations, SimulationTime now, string selectedRegistration)
        {
            _mine.Clear();
            _others.Clear();
            _market.Clear();
            _prep.Clear();
            _capability.Clear();
            HasSelection = false;
            SelectedRegistration = string.Empty;
            SelectedTypeName = string.Empty;
            SelectedIsPlayer = false;
            AssignmentLine = string.Empty;
            AssignmentDetail = string.Empty;
            PrimaryAction = AircraftHudAction.None;
            PrimaryActionLabel = string.Empty;
            CanTrack = false;
            if (operations == null)
                return;

            var clock = operations.Clock ?? AirlineClock.Default;
            var player = operations.PlayerAirline;
            FleetAircraft selected = null;

            foreach (var aircraft in operations.Fleet)
            {
                var row = Row(aircraft, now);
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
            Subtitle = $"{_mine.Count} of {AircraftAcquisition.MaxPlayerAircraft} aircraft"
                       + $"  ·  {Plural(freeBays, "regional stand")} available";

            FillMarket(operations, freeBays);

            if (selected != null)
                FillSelection(operations, selected, now, clock);
        }


        private static FleetRosterRow Row(FleetAircraft aircraft, SimulationTime now)
        {
            var stand = string.IsNullOrEmpty(aircraft.Stand.Value)
                ? "—"
                : AdelaideGround.StandLabel(aircraft.Stand);
            return new FleetRosterRow(
                aircraft.Registration,
                aircraft.Type.Name,
                FleetStatus(aircraft, now),
                stand,
                aircraft.Airline.Name,
                aircraft.Airline.LiveryHex,
                AircraftStatus.Severity(aircraft, now),
                aircraft.Airline.IsPlayer);
        }

        /// <summary>Roster wording: the prep stage while a booked departure is turning around.</summary>
        private static string FleetStatus(FleetAircraft aircraft, SimulationTime now)
        {
            if (aircraft.Airline.IsPlayer && aircraft.State == FleetState.AtStand && aircraft.Scheduled.HasValue)
            {
                var prep = DeparturePrep.For(aircraft, now);
                return prep.Ready ? "Ready for pushback" : prep.Label;
            }

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
            var fleetFull = owned >= AircraftAcquisition.MaxPlayerAircraft;

            foreach (var offer in AircraftAcquisition.All)
            {
                var spec = AircraftCatalogue.For(offer.Type);
                var unlocked = career.Tier >= offer.RequiredTier
                               && career.Reliability >= offer.RequiredReliability
                               && career.CompletedPlayerRotations >= offer.RequiredRotations;
                var affordable = career.CanAfford(offer.Price);

                // "Requires Regional tier" (a career milestone, OperatingTier) and "flies
                // Regional routes" (a RouteBand) share the word "Regional" for two unrelated
                // systems — the tier line is spelled out as "career tier" and the route line
                // names real destinations instead of leaving the band as a bare label, so
                // buying a plane answers "where can it fly" concretely, not abstractly.
                string requirement;
                if (fleetFull)
                    requirement = $"Fleet is full ({AircraftAcquisition.MaxPlayerAircraft} aircraft)";
                else if (career.Tier < offer.RequiredTier)
                    requirement = $"Requires {offer.RequiredTier} career tier";
                else if (career.Reliability < offer.RequiredReliability)
                    requirement = $"Requires {offer.RequiredReliability}% reliability"
                                  + $" — you are at {career.Reliability}%";
                else if (career.CompletedPlayerRotations < offer.RequiredRotations)
                    requirement = $"Requires {offer.RequiredRotations} completed rotations"
                                  + $" — you have {career.CompletedPlayerRotations}";
                else if (!affordable)
                    requirement = $"Costs ${offer.Price:N0} — you have ${career.Funds:N0}";
                else
                {
                    var reach = RouteAccess.ExampleDestinations(offer.Operates);
                    var suffix = offer.Operates == RouteBand.Regional ? "" : ", +closer";
                    requirement = $"Cleared to buy · flies {RouteMapWorkspaceModel.BandLabel(offer.Operates)} "
                                  + $"routes ({reach}{suffix})";
                }

                var needsGate = AirlineOperations.NeedsTerminalGate(offer.Type);
                string standLine;
                if (needsGate)
                    standLine = "Parks at a free terminal gate, or ferries in";
                else if (freeBays > 1)
                    standLine = "Parks on a free regional bay on delivery";
                else
                    standLine = "No spare bay — delivered on a short ferry in";

                _market.Add(new FleetMarketOffer(offer.Type, spec.Name,
                    RouteMapWorkspaceModel.BandLabel(offer.Operates), offer.Price,
                    requirement, standLine, affordable, unlocked, fleetFull));
            }
        }

        private void FillSelection(AirlineOperations operations, FleetAircraft aircraft,
            SimulationTime now, AirlineClock clock)
        {
            HasSelection = true;
            SelectedRegistration = aircraft.Registration;
            SelectedTypeName = aircraft.Type.Name;
            SelectedIsPlayer = aircraft.Airline.IsPlayer;

            var ceiling = RouteAccess.Ceiling(aircraft.Type);
            var ceilingSuffix = ceiling == RouteBand.Regional ? "" : ", +closer";
            _capability.Add($"{RouteMapWorkspaceModel.BandLabel(ceiling)} capability "
                             + $"({RouteAccess.ExampleDestinations(ceiling)}{ceilingSuffix})");
            _capability.Add(Plural(aircraft.CompletedTrips, "completed rotation"));
            _capability.Add($"{aircraft.Type.PracticalRangeKm:#,0} km planning range");
            if (!aircraft.Airline.IsPlayer)
                _capability.Add($"Operated by {aircraft.Airline.Name}");
            // The starter aircraft was never bought (AircraftAcquisition's own doc comment),
            // so it has no purchase price to base a resale figure on — line omitted for it
            // rather than showing a made-up number.
            else if (AircraftAcquisition.TryFor(aircraft.Type, out var ownedOffer))
                _capability.Add($"Resale value ${(long)Math.Round(ownedOffer.Price * AirlineOperations.ResaleFraction):N0}");

            if (aircraft.Scheduled.HasValue)
            {
                var booked = aircraft.Scheduled.Value;
                AssignmentLine = $"Adelaide → {booked.Destination.Name}";
                AssignmentDetail = $"Departs {clock.TimeText(booked.DepartAt)}"
                                   + $"  ·  {AdelaideGround.StandLabel(aircraft.Stand)}";
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
                var prep = DeparturePrep.For(aircraft, now);
                _prep.Add(PrepCheck("Fuel", prep.FuelProgress, prep.Stage == DeparturePrepStage.Fuel));
                _prep.Add(PrepCheck("Catering", prep.CateringProgress, prep.Stage == DeparturePrepStage.Catering));
                _prep.Add(PrepCheck("Boarding", prep.BoardingProgress, prep.Stage == DeparturePrepStage.Boarding));
            }

            PrimaryAction = OperationsSummary.PrimaryAction(aircraft);
            PrimaryActionLabel = OperationsSummary.ActionLabel(PrimaryAction).ToUpperInvariant();
            // Track is the primary action for an aircraft that is already flying; offering it
            // twice on the same card just reads as a mistake.
            CanTrack = PrimaryAction != AircraftHudAction.TrackFlight;
            _ = operations;
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
        public const float RosterRowHeight = 34f;
        public const float SectionCaptionHeight = 18f;
        public const float DetailGap = 24f;
        public const float MarketRowHeight = 52f;
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

        public HudBox RosterRow(int index) =>
            new(Roster.X, Roster.Y + index * RosterRowHeight, Roster.Width, RosterRowHeight - 2f);

        public int VisibleRosterRows => Roster.Height <= 0f ? 0 : (int)(Roster.Height / RosterRowHeight);

        public HudBox MarketCaption => Market.IsEmpty
            ? HudBox.Empty
            : Market.WithHeight(MarketCaptionHeight);

        public HudBox MarketRow(int index) => Market.IsEmpty
            ? HudBox.Empty
            : new HudBox(Market.X, Market.Y + MarketCaptionHeight + 6f + index * MarketRowHeight,
                Market.Width, MarketRowHeight - 6f);

        public static FleetWorkspaceLayout Create(HudBox surface, int marketOffers)
        {
            var header = HudShell.Header(surface);
            var body = HudShell.Body(surface, hasFooter: false);

            var rows = marketOffers < 0 ? 0 : marketOffers > 3 ? 3 : marketOffers;
            var marketHeight = rows == 0
                ? 0f
                : MarketCaptionHeight + 6f + rows * MarketRowHeight + 8f;
            if (marketHeight > body.Height * 0.45f)
            {
                rows = (int)((body.Height * 0.45f - MarketCaptionHeight - 14f) / MarketRowHeight);
                rows = rows < 0 ? 0 : rows;
                marketHeight = rows == 0 ? 0f : MarketCaptionHeight + 6f + rows * MarketRowHeight + 8f;
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
        public static void Paint(HudDrawList into, FleetWorkspaceModel model, FleetWorkspaceLayout layout,
            string selectedRegistration, int scrollRow)
        {
            if (into == null || model == null)
                return;

            into.Clear();
            into.Surface(layout.Surface);
            into.Text(layout.TitleBox, model.Title, 26f, HudTone.Default, HudTextStyle.Bold | HudTextStyle.Caption);
            into.Text(layout.SubtitleBox, model.Subtitle, 12f, HudTone.Muted);
            into.Button(OperationsWorkspacePainter.CloseBox(layout.Surface), "CLOSE", HudAction.Close,
                HudButtonStyle.Secondary);
            into.Hairline(HudShell.HeaderRule(layout.Surface));

            PaintRoster(into, model, layout, selectedRegistration, scrollRow);
            if (!layout.Divider.IsEmpty)
                into.Hairline(layout.Divider);
            PaintDetail(into, model, layout);
            PaintMarket(into, model, layout);
        }

        private static void PaintRoster(HudDrawList into, FleetWorkspaceModel model,
            FleetWorkspaceLayout layout, string selectedRegistration, int scrollRow)
        {
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

            if (model.Others.Count == 0 || drawn >= capacity)
                return;

            if (index++ >= skip)
            {
                into.Caption(layout.RosterRow(drawn).Inset(0f, 8f, 0f, 0f), "OTHER OPERATORS");
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
                    "Select an aircraft to see its capability, assignment and turnaround.", 13f,
                    HudTone.Muted, HudTextStyle.Wrap);
                return;
            }

            into.Text(new HudBox(pane.X, pane.Y, pane.Width, 28f),
                $"{model.SelectedRegistration}  ·  {model.SelectedTypeName}", 20f, HudTone.Default,
                HudTextStyle.Bold);

            var y = pane.Y + 38f;
            into.Fill(new HudBox(pane.X, y, 3f, model.SelectedCapability.Count * 21f), HudTone.Accent, 1f);
            foreach (var fact in model.SelectedCapability)
            {
                into.Text(new HudBox(pane.X + 14f, y, pane.Width - 14f, 18f), fact, 13f);
                y += 21f;
            }

            y += 12f;
            into.Hairline(new HudBox(pane.X, y, pane.Width, 1f));
            y += 12f;
            into.Caption(new HudBox(pane.X, y, pane.Width, 16f), "CURRENT ASSIGNMENT");
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
                for (var i = 0; i < model.SelectedPrep.Count; i++)
                {
                    var check = model.SelectedPrep[i];
                    into.Text(new HudBox(pane.X + i * slot, y, slot - 8f, 18f),
                        (check.Done ? "✓ " : check.Active ? "● " : "○ ") + check.Label, 12f, check.Tone,
                        check.Active ? HudTextStyle.Bold : HudTextStyle.Regular);
                }

                y += 26f;
            }

            if (!model.SelectedIsPlayer)
            {
                into.Text(new HudBox(pane.X, y, pane.Width, 36f),
                    "Another operator's aircraft — visible on the field, not yours to command.", 12f,
                    HudTone.Muted, HudTextStyle.Wrap);
                return;
            }

            var buttonY = y + 8f;
            if (buttonY + 34f > pane.Bottom)
                buttonY = pane.Bottom - 38f;
            var half = (pane.Width - 10f) * 0.5f;
            if (model.PrimaryAction != AircraftHudAction.None)
                into.Button(new HudBox(pane.X, buttonY, half, 34f), model.PrimaryActionLabel,
                    HudAction.Primary, HudButtonStyle.Primary);
            if (model.CanTrack)
                into.Button(new HudBox(pane.X + half + 10f, buttonY, half, 34f), "TRACK", HudAction.Track,
                    HudButtonStyle.Secondary);
        }

        private static void PaintMarket(HudDrawList into, FleetWorkspaceModel model, FleetWorkspaceLayout layout)
        {
            if (layout.Market.IsEmpty)
                return;

            into.Hairline(new HudBox(layout.Market.X, layout.Market.Y - 10f, layout.Market.Width, 1f));
            into.Caption(layout.MarketCaption, "AIRCRAFT MARKET");

            var shown = 0;
            foreach (var offer in model.Market)
            {
                if (shown >= layout.MarketRows)
                    break;
                var box = layout.MarketRow(shown++);
                into.Fill(box, HudTone.Default, offer.CanBuy ? 0.06f : 0.03f);
                if (offer.CanBuy)
                    into.Outline(box, HudTone.Accent, 0.6f);

                into.Text(new HudBox(box.X + 14f, box.Y + 8f, box.Width - 260f, 18f),
                    $"{offer.TypeName}  ·  {offer.BandLabel}", 14f, HudTone.Default, HudTextStyle.Bold);
                into.Text(new HudBox(box.X + 14f, box.Y + 26f, box.Width - 260f, 16f),
                    offer.CanBuy ? offer.StandLine : offer.RequirementLine, 11f,
                    offer.CanBuy ? HudTone.Muted : HudTone.Caution);
                into.Text(new HudBox(box.Right - 240f, box.Y + 12f, 110f, 20f), $"${offer.Price:N0}", 14f,
                    offer.Affordable ? HudTone.Default : HudTone.Muted, HudTextStyle.Bold, HudAlign.Right);
                into.Button(new HudBox(box.Right - 118f, box.Y + 9f, 108f, 28f), "BUY",
                    HudAction.Buy(offer.Type.Id), HudButtonStyle.Primary, offer.CanBuy);
            }
        }
    }
}
