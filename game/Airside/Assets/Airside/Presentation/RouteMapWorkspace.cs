using System;
using System.Collections.Generic;
using Airside.Domain;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>Which half of the network the route map is showing.</summary>
    public enum RouteMapFilter
    {
        Available,
        Locked
    }

    /// <summary>
    /// The Route Map workspace as data (ADR 0057): which destinations the selected aircraft
    /// may actually operate, and the real cost and return of the one being looked at.
    ///
    /// Reachability comes from <see cref="AirlineOperations.CanOperate"/> (range plus the
    /// type's <see cref="RouteAccess"/> band) and money from <see cref="FlightEconomics"/> —
    /// nothing here is authored per-destination.
    /// </summary>
    public sealed class RouteMapWorkspaceModel
    {
        private readonly List<PlannerDestination> _all = new();
        private readonly List<PlannerDestination> _shown = new();
        private readonly List<FleetAircraft> _fleet = new();
        private readonly List<AircraftType> _ownedTypes = new();
        private readonly HashSet<string> _careerTargets = new(StringComparer.Ordinal);

        public string Title => "ROUTE MAP";
        public RouteMapFilter Filter { get; private set; }

        /// <summary>Destinations this aircraft may operate today.</summary>
        public int AvailableCount { get; private set; }

        /// <summary>Destinations out of range or above this type's route band.</summary>
        public int LockedCount { get; private set; }

        /// <summary>Everything on the map, reachable first — the map draws all of it.</summary>
        public IReadOnlyList<PlannerDestination> AllDestinations => _all;

        /// <summary>Just the half the filter selects — the side list shows this.</summary>
        public IReadOnlyList<PlannerDestination> ShownDestinations => _shown;
        public IReadOnlyCollection<string> CareerTargetCodes => _careerTargets;

        public bool IsCareerTarget(Destination destination) =>
            !string.IsNullOrEmpty(destination.Code) && _careerTargets.Contains(destination.Code);

        public bool HasAircraft { get; private set; }
        public string AircraftLabel { get; private set; } = "No aircraft";
        public double AircraftRangeKm { get; private set; }
        public string AircraftRangeLabel { get; private set; } = string.Empty;
        public bool CanCycleAircraft { get; private set; }

        public bool HasDestination { get; private set; }
        public string DestinationTitle { get; private set; } = string.Empty;
        public string BandAndDistance { get; private set; } = string.Empty;
        public string CompatibilityLine { get; private set; } = string.Empty;
        public string DispatchLine { get; private set; } = string.Empty;
        public string ReturnLine { get; private set; } = string.Empty;
        public string OperatingNote { get; private set; } = string.Empty;
        public string AvailabilityLine { get; private set; } = string.Empty;
        public HudTone AvailabilityTone { get; private set; } = HudTone.Muted;
        public string CareerLine { get; private set; } = string.Empty;
        public HudTone CareerTone { get; private set; } = HudTone.Muted;

        public string DepartureLabel { get; private set; } = string.Empty;
        public string DepartureDetail { get; private set; } = string.Empty;

        /// <summary>True only when the simulation would actually accept the booking.</summary>
        public bool CanPlan { get; private set; }

        public string PlanLabel { get; private set; } = "PLAN FLIGHT";

        /// <summary>Why the plan button is disabled, or empty when it is not.</summary>
        public string PlanBlockedReason { get; private set; } = string.Empty;

        public bool HasBooking { get; private set; }
        public string BookingLine { get; private set; } = string.Empty;

        public void Rebuild(AirlineOperations operations, FleetAircraft aircraft, Destination? selected,
            long departureDelaySeconds, SimulationTime now, RouteMapFilter filter)
        {
            Filter = filter;
            _all.Clear();
            _shown.Clear();
            _careerTargets.Clear();
            AvailableCount = 0;
            LockedCount = 0;
            HasAircraft = aircraft != null;
            AircraftRangeKm = aircraft?.Type?.PracticalRangeKm ?? 0.0;
            AircraftRangeLabel = aircraft == null
                ? string.Empty
                : $"{aircraft.Type.Name}  ·  {aircraft.Type.PracticalRangeKm:N0} km practical range";
            HasDestination = false;
            HasBooking = false;
            BookingLine = string.Empty;
            CanPlan = false;
            PlanBlockedReason = string.Empty;
            PlanLabel = "PLAN FLIGHT";
            DestinationTitle = string.Empty;
            BandAndDistance = string.Empty;
            CompatibilityLine = string.Empty;
            DispatchLine = string.Empty;
            ReturnLine = string.Empty;
            OperatingNote = string.Empty;
            AvailabilityLine = string.Empty;
            AvailabilityTone = HudTone.Muted;
            CareerLine = string.Empty;
            CareerTone = HudTone.Muted;
            DepartureLabel = string.Empty;
            DepartureDetail = string.Empty;
            if (operations == null)
                return;

            var clock = operations.Clock ?? AirlineClock.Default;
            FlightPlanner.DestinationsFor(operations, aircraft, _all);
            foreach (var row in _all)
            {
                if (row.Reachable)
                    AvailableCount++;
                else
                    LockedCount++;
                if (row.Reachable == (filter == RouteMapFilter.Available))
                    _shown.Add(row);
            }

            _fleet.Clear();
            _ownedTypes.Clear();
            var player = operations.PlayerAirline;
            if (player != null)
                foreach (var owned in operations.FleetOf(player))
                {
                    _fleet.Add(owned);
                    _ownedTypes.Add(owned.Type);
                }
            CanCycleAircraft = _fleet.Count > 1;
            foreach (var row in _all)
                if (Campaign.RouteGuidance(operations.CareerState, _ownedTypes, row.Destination).AdvancesCurrentChapter)
                    _careerTargets.Add(row.Destination.Code);

            AircraftLabel = aircraft == null
                ? "No aircraft"
                : $"{aircraft.Registration}  ·  {aircraft.Type.Name}";

            if (aircraft != null)
            {
                HasBooking = aircraft.Scheduled.HasValue;
                PlanLabel = HasBooking ? "UPDATE PLAN" : "PLAN FLIGHT";
                if (HasBooking)
                {
                    var booked = aircraft.Scheduled.Value;
                    BookingLine = $"Booked: {booked.Destination.Name} at {clock.TimeText(booked.DepartAt)}";
                }
            }

            if (!selected.HasValue)
                return;

            FillDestination(operations, aircraft, selected.Value, departureDelaySeconds, now, clock);
        }

        private void FillDestination(AirlineOperations operations, FleetAircraft aircraft,
            Destination destination, long departureDelaySeconds, SimulationTime now, AirlineClock clock)
        {
            HasDestination = true;
            DestinationTitle = (destination.Name ?? destination.Code).ToUpperInvariant();
            var km = operations.DistanceKm(destination);
            var band = RouteAccess.BandOf(destination);
            BandAndDistance = $"{BandLabel(band)}  ·  {km:0} km";

            var career = Campaign.RouteGuidance(operations.CareerState, _ownedTypes, destination);
            CareerLine = career.Text;
            CareerTone = career.AdvancesCurrentChapter ? HudTone.Caution : HudTone.Muted;

            if (aircraft == null)
            {
                CompatibilityLine = "No aircraft selected";
                AvailabilityLine = "Choose an aircraft to price this route";
                AvailabilityTone = HudTone.Muted;
                PlanBlockedReason = AvailabilityLine;
                return;
            }

            var type = aircraft.Type;
            var inRange = operations.CanReach(aircraft, destination);
            var inBand = RouteAccess.Allows(type, destination);
            CompatibilityLine = inRange && inBand
                ? $"{type.Name} compatible"
                : $"{type.Name} not cleared for this route";

            var dispatch = FlightEconomics.DispatchCost(type, km);
            var alreadyPaid = aircraft.Scheduled.HasValue
                ? FlightEconomics.DispatchCost(type, operations.DistanceKm(aircraft.Scheduled.Value.Destination))
                : 0;
            var changeCost = dispatch - alreadyPaid;
            var basePay = FlightEconomics.FlightPay(type, km, band);
            var pay = (long)Math.Round(basePay *
                FlightEconomics.ReliabilityMultiplier(operations.CareerState.Reliability));
            var active = operations.CareerState.ActiveContract;
            if (active != null
                && operations.CareerState.TryFindDefinition(active.DefinitionId, out var contract)
                && contract.EligibleType == type
                && contract.MatchesRoute(operations.Home.Code, destination.Code))
            {
                var contractPay = contract.PaymentPerRotation;
                if (active.CompletedRotations + 1 >= contract.RequiredRotations)
                    contractPay += contract.CompletionReward;
                pay += contractPay;
                OperatingNote = contract.ReliabilityLossOnCancel > 0
                    ? $"Contract +${contractPay:N0} · cancel −{contract.ReliabilityLossOnCancel} reliability"
                    : $"Contract +${contractPay:N0} on return";
            }
            DispatchLine = alreadyPaid > 0
                ? $"Change  {(changeCost >= 0 ? "+" : "−")}${Math.Abs(changeCost):N0}"
                : $"Dispatch  ${dispatch:N0}";
            ReturnLine = $"Est. return  ${pay:N0}  ·  net {(pay - dispatch >= 0 ? "+" : "−")}${Math.Abs(pay - dispatch):N0}";
            if (Maintenance.IsDueSoon(aircraft))
                OperatingNote = Maintenance.IsOverdue(aircraft)
                    ? $"Check overdue: next rotation costs {Maintenance.OverduePenalty} reliability"
                    : "Check due after this rotation";

            if (!inRange)
            {
                AvailabilityLine =
                    $"Locked — beyond the {type.Name}'s {type.PracticalRangeKm:0} km range";
                AvailabilityTone = HudTone.Muted;
                PlanBlockedReason = AvailabilityLine;
                return;
            }

            if (!inBand)
            {
                AvailabilityLine =
                    $"Locked — {Article.A(type.Name)} flies {BandLabel(RouteAccess.Ceiling(type))} routes; "
                    + $"{destination.Name} is {BandLabel(band)}";
                AvailabilityTone = HudTone.Muted;
                PlanBlockedReason = AvailabilityLine;
                return;
            }

            // The band the route needs, not the ceiling of the aircraft looking at it: a
            // Dash 8 on a Kingscote hop is flying a Regional route, not a Domestic one.
            AvailabilityLine = $"Available with {BandLabel(band)} capability";
            AvailabilityTone = HudTone.Caution;
            if (OperatingNote.Length > 0)
                AvailabilityLine = OperatingNote;

            var delay = FlightPlanner.ClampDelay(departureDelaySeconds, type);
            var departAt = now.Advance(delay);
            DepartureLabel = clock.TimeText(departAt);
            DepartureDetail = $"in {Duration(delay)}  ·  "
                              + $"{Duration(operations.AirborneSeconds(aircraft, destination))} each way";

            if (aircraft.State != FleetState.AtStand)
            {
                PlanBlockedReason = $"{aircraft.Registration} must be parked at Adelaide to be planned";
                return;
            }
            if (aircraft.CheckUntil is { } checkEnds && departAt.CompareTo(checkEnds) < 0)
            {
                PlanBlockedReason = $"{aircraft.Registration} is in its check until {clock.TimeText(checkEnds)}";
                return;
            }

            if (operations.CareerState.Funds + alreadyPaid < dispatch)
            {
                PlanBlockedReason =
                    $"{(alreadyPaid > 0 ? "Change" : "Dispatch")} costs ${changeCost:N0}; you have ${operations.CareerState.Funds:N0}";
                return;
            }

            CanPlan = true;
            PlanLabel = aircraft.Scheduled.HasValue ? "UPDATE PLAN" : "PLAN FLIGHT";
        }

        public static string BandLabel(RouteBand band) => band switch
        {
            RouteBand.Domestic => "Domestic",
            RouteBand.National => "National",
            RouteBand.Tasman => "Tasman",
            RouteBand.LongHaul => "Long-haul",
            _ => "Regional"
        };

        /// <summary>"1 h 25 min" / "40 min" — the same wording the planner timeline uses.</summary>
        public static string Duration(long seconds)
        {
            if (seconds < 0)
                seconds = 0;
            var minutes = (seconds + 30) / 60;
            if (minutes < 60)
                return $"{minutes} min";
            return minutes % 60 == 0 ? $"{minutes / 60} h" : $"{minutes / 60} h {minutes % 60} min";
        }
    }

    /// <summary>Where the Route Map workspace draws its map, filter pills and detail pane.</summary>
    public readonly struct RouteMapWorkspaceLayout
    {
        public const float FilterHeight = 28f;
        public const float FilterWidth = 118f;
        public const float DetailWidth = 320f;
        public const float DetailGap = 20f;

        /// <summary>
        /// Below this the map is not worth looking at, and the detail pane gives way. Kept
        /// low enough that a 1024-point window still gets both.
        /// </summary>
        public const float MinMapWidth = 320f;

        private RouteMapWorkspaceLayout(HudBox surface, HudBox header, HudBox filters, HudBox map, HudBox detail)
        {
            Surface = surface;
            Header = header;
            Filters = filters;
            Map = map;
            Detail = detail;
        }

        public HudBox Surface { get; }
        public HudBox Header { get; }

        /// <summary>The AVAILABLE / LOCKED pills, floating over the top-left of the map.</summary>
        public HudBox Filters { get; }

        public HudBox Map { get; }
        public HudBox Detail { get; }

        public HudBox TitleBox => new(Header.X + HudShell.SurfacePadding, Header.Y + 16f,
            Header.Width - HudShell.SurfacePadding * 2f, 30f);

        public HudBox FilterBox(int index) =>
            new(Filters.X + index * (FilterWidth + 8f), Filters.Y, FilterWidth, FilterHeight);

        public const float RivalsWidth = 142f;
        public const float RivalsHeight = 24f;

        /// <summary>
        /// The RIVALS ON/OFF toggle: top-right of the map, level with the pills when the map
        /// is wide enough for both, otherwise just under them. Pinned to the top-right on its
        /// own, it sat on the LOCKED pill on any map narrower than about 440 points.
        /// </summary>
        public HudBox RivalsToggle
        {
            get
            {
                var right = Map.Right - 12f - RivalsWidth;
                if (right >= Filters.Right + 12f)
                    return new HudBox(right, Map.Y + 12f, RivalsWidth, RivalsHeight);
                return new HudBox(Filters.X, Filters.Bottom + 8f, Math.Min(RivalsWidth, Math.Max(1f, Map.Width - 24f)),
                    RivalsHeight);
            }
        }

        public static RouteMapWorkspaceLayout Create(HudBox surface)
        {
            var header = HudShell.Header(surface);
            var body = HudShell.Body(surface, hasFooter: false);

            var detailWidth = body.Width - MinMapWidth - DetailGap >= DetailWidth ? DetailWidth : 0f;
            var mapWidth = detailWidth > 0f ? body.Width - detailWidth - DetailGap : body.Width;
            var map = new HudBox(body.X, body.Y, mapWidth, body.Height);
            var detail = detailWidth > 0f
                ? new HudBox(body.Right - detailWidth, body.Y, detailWidth, body.Height)
                : HudBox.Empty;
            var filters = new HudBox(map.X + 12f, map.Y + 12f, FilterWidth * 2f + 8f, FilterHeight);

            return new RouteMapWorkspaceLayout(surface, header, filters, map, detail);
        }
    }

    /// <summary>
    /// Paints the Route Map workspace: the chrome and destination detail through the shared
    /// draw list, and the static network layer (coastline, borders, routes, destination
    /// dots) that both the runtime map and the offline mockup rasterise. Live aircraft
    /// icons, tracking and the pointer stay with the runtime map — this changes how the
    /// network reads, not how it behaves.
    /// </summary>
    public static class RouteMapWorkspacePainter
    {
        public static void Paint(HudDrawList into, RouteMapWorkspaceModel model, RouteMapWorkspaceLayout layout)
        {
            if (into == null || model == null)
                return;

            into.Clear();
            into.Surface(layout.Surface);
            into.Text(layout.TitleBox, model.Title, 26f, HudTone.Default, HudTextStyle.Bold | HudTextStyle.Caption);
            into.Button(OperationsWorkspacePainter.CloseBox(layout.Surface), "CLOSE", HudAction.Close,
                HudButtonStyle.Secondary);
            into.Hairline(HudShell.HeaderRule(layout.Surface));

            // The map well is darker than the surface so the coastline and routes read.
            into.Fill(layout.Map, HudTone.Default, 0.55f, AirsidePalette.CoastalBlueDeepHex);
            PaintDetail(into, model, layout);
        }

        /// <summary>
        /// The AVAILABLE / LOCKED pills. Painted after the network so they float over the
        /// map rather than under it.
        /// </summary>
        public static void PaintFilters(HudDrawList into, RouteMapWorkspaceModel model,
            RouteMapWorkspaceLayout layout)
        {
            if (into == null || model == null)
                return;
            into.Button(layout.FilterBox(0), $"AVAILABLE {model.AvailableCount}", HudAction.FilterAvailable,
                model.Filter == RouteMapFilter.Available ? HudButtonStyle.Primary : HudButtonStyle.Secondary);
            into.Button(layout.FilterBox(1), $"LOCKED {model.LockedCount}", HudAction.FilterLocked,
                model.Filter == RouteMapFilter.Locked ? HudButtonStyle.Primary : HudButtonStyle.Secondary);
        }

        private static void PaintDetail(HudDrawList into, RouteMapWorkspaceModel model,
            RouteMapWorkspaceLayout layout)
        {
            var pane = layout.Detail;
            if (pane.IsEmpty)
                return;

            into.Hairline(new HudBox(pane.X - RouteMapWorkspaceLayout.DetailGap * 0.5f, pane.Y, 1f, pane.Height));

            if (!model.HasDestination)
            {
                PaintDestinationList(into, model, pane);
                return;
            }

            into.Text(new HudBox(pane.X, pane.Y, pane.Width, 30f), model.DestinationTitle, 22f,
                HudTone.Default, HudTextStyle.Bold | HudTextStyle.Caption);
            into.Caption(new HudBox(pane.X, pane.Y + 27f, pane.Width, 14f), "DESTINATION DOSSIER",
                model.CareerLine.Length > 0 ? HudTone.Caution : HudTone.Muted);
            into.Hairline(new HudBox(pane.X, pane.Y + 46f, pane.Width, 1f));

            var y = pane.Y + 58f;
            PaintFact(into, pane, ref y, "ROUTE", model.BandAndDistance, HudTone.Default);
            PaintFact(into, pane, ref y, "AIRCRAFT", model.CompatibilityLine, HudTone.Default);
            if (model.DispatchLine.Length > 0)
            {
                PaintFact(into, pane, ref y, "OUTBOUND", model.DispatchLine, HudTone.Default);
                PaintFact(into, pane, ref y, "RETURN", model.ReturnLine, HudTone.Positive);
            }

            into.Hairline(new HudBox(pane.X, y, pane.Width, 1f));
            y += 12f;
            into.Text(new HudBox(pane.X, y, pane.Width, 36f), model.AvailabilityLine, 13f,
                model.AvailabilityTone, HudTextStyle.Wrap);
            y += 44f;

            if (model.CareerLine.Length > 0)
            {
                into.Hairline(new HudBox(pane.X, y, pane.Width, 1f));
                y += 10f;
                into.Caption(new HudBox(pane.X, y, pane.Width, 16f), "CAREER");
                y += 19f;
                into.Text(new HudBox(pane.X, y, pane.Width, 34f), model.CareerLine, 12f,
                    model.CareerTone, HudTextStyle.Bold | HudTextStyle.Wrap);
                y += 38f;
                if (pane.Height >= 520f)
                {
                    into.Button(new HudBox(pane.X, y, 132f, 28f), "VIEW CONTRACTS",
                        HudAction.ViewContracts, HudButtonStyle.Secondary);
                    y += 36f;
                }
            }

            if (model.DepartureLabel.Length > 0)
            {
                into.Hairline(new HudBox(pane.X, y, pane.Width, 1f));
                y += 12f;
                into.Caption(new HudBox(pane.X, y, pane.Width, 16f), "DEPARTURE");
                y += 22f;

                into.Text(new HudBox(pane.X, y + 5f, 74f, 18f), "Aircraft", 12f, HudTone.Muted);
                into.Button(new HudBox(pane.X + 80f, y, pane.Width - 80f, 28f), model.AircraftLabel + "   ▾",
                    HudAction.NextAircraft, HudButtonStyle.Secondary, model.CanCycleAircraft);
                y += 34f;

                into.Text(new HudBox(pane.X, y + 5f, 74f, 18f), "Departure", 12f, HudTone.Muted);
                into.Button(new HudBox(pane.X + 80f, y, 34f, 28f), "−", HudAction.PreviousDeparture,
                    HudButtonStyle.Secondary);
                into.Button(new HudBox(pane.X + 118f, y, pane.Width - 156f, 28f), model.DepartureLabel,
                    HudAction.NextDeparture, HudButtonStyle.Secondary);
                into.Button(new HudBox(pane.Right - 34f, y, 34f, 28f), "+", HudAction.NextDeparture,
                    HudButtonStyle.Secondary);
                y += 32f;
                into.Text(new HudBox(pane.X + 80f, y, pane.Width - 80f, 16f), model.DepartureDetail, 11f,
                    HudTone.Muted);
            }

            var buttonY = pane.Bottom - 84f;
            into.Button(new HudBox(pane.X, buttonY, pane.Width, 44f), model.PlanLabel, HudAction.PlanFlight,
                HudButtonStyle.Primary, model.CanPlan);
            into.Button(new HudBox(pane.X, buttonY + 50f, pane.Width, 30f), "RESET MAP", HudAction.ResetMap,
                HudButtonStyle.Secondary);
            if (!model.CanPlan && model.PlanBlockedReason.Length > 0)
                into.Text(new HudBox(pane.X, buttonY - 34f, pane.Width, 32f), model.PlanBlockedReason, 11f,
                    HudTone.Muted, HudTextStyle.Wrap);
            else if (model.HasBooking)
                into.Text(new HudBox(pane.X, buttonY - 34f, pane.Width, 32f), model.BookingLine, 11f,
                    HudTone.Muted, HudTextStyle.Wrap);
        }

        private static void PaintFact(HudDrawList into, HudBox pane, ref float y, string caption,
            string value, HudTone tone)
        {
            var row = new HudBox(pane.X, y, pane.Width, 26f);
            into.Fill(row, HudTone.Default, 0.035f);
            into.Caption(new HudBox(row.X + 8f, row.Y + 6f, 78f, 15f), caption);
            into.Text(new HudBox(row.X + 88f, row.Y + 5f, row.Width - 96f, 17f), value, 12f, tone,
                HudTextStyle.Bold, HudAlign.Right);
            y += 32f;
        }

        private static void PaintDestinationList(HudDrawList into, RouteMapWorkspaceModel model, HudBox pane)
        {
            into.Text(new HudBox(pane.X, pane.Y, pane.Width, 26f),
                model.Filter == RouteMapFilter.Available ? "WHERE TO?" : "LOCKED ROUTES", 18f,
                HudTone.Default, HudTextStyle.Bold | HudTextStyle.Caption);
            into.Text(new HudBox(pane.X, pane.Y + 28f, pane.Width, 18f),
                model.HasAircraft
                    ? "Pick a dot on the map, or a destination here."
                    : "Choose an aircraft to see what it can fly.", 12f, HudTone.Muted);

            var y = pane.Y + 54f;
            foreach (var row in model.ShownDestinations)
            {
                if (y + 34f > pane.Bottom)
                    break;
                var box = new HudBox(pane.X, y, pane.Width, 32f);
                into.Fill(new HudBox(box.X, box.Y + 5f, 3f, 22f),
                    row.Reachable ? HudTone.Positive : HudTone.Muted, 1f);
                into.Text(new HudBox(box.X + 10f, box.Y, box.Width - 100f, 17f),
                    $"{row.Destination.Code}  {row.Destination.Name}", 13f,
                    row.Reachable ? HudTone.Default : HudTone.Muted);
                var careerTarget = model.IsCareerTarget(row.Destination);
                into.Text(new HudBox(box.X + 10f, box.Y + 16f, box.Width - 100f, 15f),
                    careerTarget ? $"CAREER TARGET · {row.Destination.State}" : row.Destination.State,
                    11f, careerTarget ? HudTone.Caution : HudTone.Muted,
                    careerTarget ? HudTextStyle.Bold : HudTextStyle.Regular);
                into.Text(new HudBox(box.Right - 96f, box.Y, 96f, 17f), $"{row.DistanceKm:0} km", 12f,
                    HudTone.Muted, HudTextStyle.Regular, HudAlign.Right);
                into.Text(new HudBox(box.Right - 96f, box.Y + 16f, 96f, 15f),
                    row.Reachable ? RouteMapWorkspaceModel.Duration(row.AirborneSeconds) : "locked", 11f,
                    row.Reachable ? HudTone.Positive : HudTone.Muted, HudTextStyle.Regular, HudAlign.Right);
                into.Hotspot(box, HudAction.Destination(row.Destination.Code));
                y += 34f;
            }
        }

        /// <summary>
        /// The static network layer inside <paramref name="map"/>: coastline, state borders,
        /// the selected route from home, and destination dots with zoom-dependent labels. The
        /// previous all-spokes view made route comparison harder, not easier.
        /// </summary>
        public static void PaintNetwork(HudDrawList into, HudBox map, AustraliaMapLens lens,
            IReadOnlyList<PlannerDestination> destinations, Destination home, Destination? selected,
            string playerLiveryHex, double aircraftRangeKm = 0.0, string aircraftRangeLabel = null,
            IReadOnlyCollection<string> careerTargetCodes = null)
        {
            if (into == null || lens == null)
                return;

            // One real aircraft limit is useful. Three generic circles looked technical but did
            // not answer the player's actual question: "how far can this selected aircraft go?"
            if (aircraftRangeKm > 0.0)
                RangeRing(into, map, lens, home, aircraftRangeKm, aircraftRangeLabel);
            Polyline(into, map, lens, AustraliaMapGeometry.MainlandCoastLonLat, HudTone.Muted, 1.6f);
            Polyline(into, map, lens, AustraliaMapGeometry.TasmaniaCoastLonLat, HudTone.Muted, 1.6f);
            foreach (var border in AustraliaMapGeometry.StateBorderLonLats)
                Polyline(into, map, lens, border, HudTone.Muted, 0.9f);

            if (destinations != null)
            {
                if (selected.HasValue)
                {
                    foreach (var row in destinations)
                    {
                        if (!selected.Value.Equals(row.Destination))
                            continue;
                        GreatCircle(into, map, lens, home, row.Destination, HudTone.Caution, 2.2f);
                        break;
                    }
                }

                foreach (var row in destinations)
                {
                    Project(lens, map, row.Destination.Longitude, row.Destination.Latitude, out var x, out var y);
                    if (!map.Contains(x, y))
                        continue;
                    var isSelected = selected.HasValue && selected.Value.Equals(row.Destination);
                    var careerTarget = ContainsCode(careerTargetCodes, row.Destination.Code);
                    var tone = isSelected || careerTarget
                        ? HudTone.Caution
                        : row.Reachable ? HudTone.Accent : HudTone.Muted;
                    into.Dot(x, y, isSelected ? 13f : careerTarget ? 11f : 9f, tone);
                    var label = isSelected || lens.Zoom >= 4f
                        ? row.Destination.Name
                        : lens.Zoom >= 2f ? row.Destination.Code : string.Empty;
                    if (label.Length > 0)
                        into.Text(new HudBox(x + 9f, y - 9f, 128f, 17f), label, 12f,
                            row.Reachable ? HudTone.Default : HudTone.Muted,
                            isSelected ? HudTextStyle.Bold : HudTextStyle.Regular);
                    // No hotspot: the map's own pointer handler picks the nearest dot, so it
                    // can tell a click from the start of a pan. An IMGUI control here would
                    // also come and go as zoom culls dots, which is how control ids drift.
                }
            }

            Project(lens, map, home.Longitude, home.Latitude, out var hx, out var hy);
            if (!map.Contains(hx, hy))
                return;
            into.Dot(hx, hy, 12f, HudTone.Caution, playerLiveryHex);
            into.Text(new HudBox(hx + 10f, hy - 9f, 120f, 17f), home.Name, 13f, HudTone.Default,
                HudTextStyle.Bold);
        }

        private static bool ContainsCode(IReadOnlyCollection<string> codes, string code)
        {
            if (codes == null || string.IsNullOrEmpty(code))
                return false;
            foreach (var candidate in codes)
                if (string.Equals(candidate, code, StringComparison.Ordinal))
                    return true;
            return false;
        }

        private static void RangeRing(HudDrawList into, HudBox map, AustraliaMapLens lens,
            Destination home, double distanceKm, string label)
        {
            const int segments = 72;
            RouteMap.DestinationPoint(home.Latitude, home.Longitude, distanceKm, 0.0,
                out var lat, out var lon);
            Project(lens, map, lon, lat, out var px, out var py);
            for (var i = 1; i <= segments; i++)
            {
                RouteMap.DestinationPoint(home.Latitude, home.Longitude, distanceKm,
                    i * 360.0 / segments, out lat, out lon);
                Project(lens, map, lon, lat, out var nx, out var ny);
                if ((i & 1) == 0)
                    Clipped(into, map, px, py, nx, ny, HudTone.Muted, 0.65f);
                px = nx;
                py = ny;
            }

            RouteMap.DestinationPoint(home.Latitude, home.Longitude, distanceKm, 90.0,
                out lat, out lon);
            Project(lens, map, lon, lat, out var lx, out var ly);
            if (map.Contains(lx, ly))
                into.Text(new HudBox(lx + 4f, ly - 15f, 190f, 15f),
                    string.IsNullOrEmpty(label) ? $"{distanceKm:N0} km range" : label, 9f,
                    HudTone.Muted);
        }

        private static void Polyline(HudDrawList into, HudBox map, AustraliaMapLens lens, float[] lonLat,
            HudTone tone, float thickness)
        {
            var count = AustraliaMapGeometry.PointCount(lonLat);
            for (var i = 1; i < count; i++)
            {
                Project(lens, map, lonLat[(i - 1) * 2], lonLat[(i - 1) * 2 + 1], out var x0, out var y0);
                Project(lens, map, lonLat[i * 2], lonLat[i * 2 + 1], out var x1, out var y1);
                Clipped(into, map, x0, y0, x1, y1, tone, thickness);
            }
        }

        private static void GreatCircle(HudDrawList into, HudBox map, AustraliaMapLens lens, Destination from,
            Destination to, HudTone tone, float thickness)
        {
            const int segments = 24;
            RouteMap.GreatCirclePoint(from.Latitude, from.Longitude, to.Latitude, to.Longitude, 0.0,
                out var lat, out var lon);
            Project(lens, map, lon, lat, out var px, out var py);
            for (var i = 1; i <= segments; i++)
            {
                RouteMap.GreatCirclePoint(from.Latitude, from.Longitude, to.Latitude, to.Longitude,
                    i / (double)segments, out lat, out lon);
                Project(lens, map, lon, lat, out var nx, out var ny);
                Clipped(into, map, px, py, nx, ny, tone, thickness);
                px = nx;
                py = ny;
            }
        }

        private static void Clipped(HudDrawList into, HudBox map, float x0, float y0, float x1, float y1,
            HudTone tone, float thickness)
        {
            if (!RouteMap.ClipSegment(ref x0, ref y0, ref x1, ref y1, map.X, map.Y, map.Right, map.Bottom))
                return;
            into.Line(x0, y0, x1, y1, tone, thickness);
        }

        private static void Project(AustraliaMapLens lens, HudBox map, double longitude, double latitude,
            out float x, out float y) =>
            lens.Project(map.X, map.Y, map.Width, map.Height, longitude, latitude, out x, out y);
    }
}
