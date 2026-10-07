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

        public string Title => "PLAN A FLIGHT";
        public string ProfitLine { get; private set; } = string.Empty;
        public HudTone ProfitTone { get; private set; } = HudTone.Positive;
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
        public bool CanDelegate { get; private set; }
        public string RepeatLabel { get; private set; } = "REPEAT 12H";

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
                : $"{aircraft.Type.Name}  ·  {aircraft.Type.PracticalRangeKm:N0} km range";
            HasDestination = false;
            HasBooking = false;
            BookingLine = string.Empty;
            CanPlan = false;
            PlanBlockedReason = string.Empty;
            PlanLabel = "PLAN FLIGHT";
            CanDelegate = false;
            RepeatLabel = "REPEAT 12H";
            DestinationTitle = string.Empty;
            BandAndDistance = string.Empty;
            CompatibilityLine = string.Empty;
            DispatchLine = string.Empty;
            ProfitLine = string.Empty;
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
                if (operations.CareerRouteGuidance(row.Destination).Length > 0)
                    _careerTargets.Add(row.Destination.Code);
            AircraftLabel = aircraft == null
                ? "No aircraft"
                : FlightNumber.ForAircraft(aircraft) is { } flight
                    ? $"{flight}  ·  {FlightNumber.PlaceName(aircraft)}"
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
            CanDelegate = operations.DelegationUnlocked && aircraft != null
                          && operations.CanOperate(aircraft, selected.Value);
            if (aircraft != null)
                foreach (var repeat in operations.RepeatSchedules)
                    if (repeat.Registration == aircraft.Registration
                        && repeat.DestinationCode == selected.Value.Code)
                        RepeatLabel = repeat.Paused ? "RESUME REPEAT" : "PAUSE REPEAT";
        }

        private void FillDestination(AirlineOperations operations, FleetAircraft aircraft,
            Destination destination, long departureDelaySeconds, SimulationTime now, AirlineClock clock)
        {
            HasDestination = true;
            DestinationTitle = (destination.Name ?? destination.Code).ToUpperInvariant();
            var km = operations.DistanceKm(destination);
            var band = RouteAccess.BandOf(destination);
            BandAndDistance = $"{BandLabel(band)}  ·  {km:0} km";

            CareerLine = operations.CareerRouteGuidance(destination);
            CareerTone = CareerLine.Length > 0 ? HudTone.Caution : HudTone.Muted;

            if (aircraft == null)
            {
                CompatibilityLine = "No aircraft selected";
                AvailabilityLine = "Choose an aircraft to see what this route pays";
                AvailabilityTone = HudTone.Muted;
                PlanBlockedReason = AvailabilityLine;
                return;
            }

            var type = aircraft.Type;
            var inRange = operations.CanReach(aircraft, destination);
            var inBand = RouteAccess.Allows(type, destination);
            CompatibilityLine = inRange && inBand
                ? $"{type.Name} can fly this"
                : $"{type.Name} can't fly this route";

            var dispatch = operations.DispatchCost(type, km);
            var alreadyPaid = aircraft.Scheduled.HasValue
                ? operations.DispatchCost(type, operations.DistanceKm(aircraft.Scheduled.Value.Destination))
                : 0;
            var changeCost = dispatch - alreadyPaid;
            var forecast = operations.Forecast(operations.Home, destination, aircraft);
            BandAndDistance += $" · {forecast.LoadText}";
            var pay = FlightPlanner.ExpectedRevenue(operations, operations.Home, destination, type, aircraft);
            var active = operations.CareerState.ActiveContract;
            if (active != null
                && operations.CareerState.TryFindDefinition(active.DefinitionId, out var contract)
                && contract.MatchesAircraft(type, aircraft.IsFreighter)
                && contract.MatchesRoute(operations.Home.Code, destination.Code))
            {
                var contractPay = contract.PaymentPerRotation;
                if (active.CompletedRotations + 1 >= contract.RequiredRotations)
                    contractPay += contract.CompletionReward;

                OperatingNote = contract.ReliabilityLossOnCancel > 0
                    ? $"Contract +${contractPay:N0} · abandoning costs {contract.ReliabilityLossOnCancel} reliability"
                    : $"Contract +${contractPay:N0} on return";
            }
            ProfitLine = $"Expected profit {(pay - dispatch >= 0 ? "+" : "−")}${Math.Abs(pay - dispatch):N0}";
            ProfitTone = pay >= dispatch ? HudTone.Positive : HudTone.Caution;
            DispatchLine = alreadyPaid > 0
                ? $"Change  {(changeCost >= 0 ? "+" : "−")}${Math.Abs(changeCost):N0}"
                : $"Pay now ${dispatch:N0}";
            ReturnLine = $"Expected return ${pay:N0}";
            if (Maintenance.IsDueSoon(aircraft))
            {
                var checkNote = Maintenance.IsOverdue(aircraft)
                    ? $"Check overdue · this flight costs {Maintenance.OverduePenalty} reliability"
                    : "Check due after this flight";
                OperatingNote = OperatingNote.Length > 0
                    ? OperatingNote + "\n" + checkNote
                    : checkNote;
            }

            if (!inRange)
            {
                AvailabilityLine =
                    $"Out of range. The {type.Name} reaches {type.PracticalRangeKm:N0} km";
                AvailabilityTone = HudTone.Muted;
                PlanBlockedReason = AvailabilityLine;
                return;
            }

            if (!inBand)
            {
                AvailabilityLine =
                    $"{Article.CapitalA(type.Name)} only flies {RouteAccess.Label(RouteAccess.Ceiling(type))} routes. "
                    + $"{destination.Name} is {RouteAccess.Label(band)}";
                AvailabilityTone = HudTone.Muted;
                PlanBlockedReason = AvailabilityLine;
                return;
            }

            // The band the route needs, not the ceiling of the aircraft looking at it: a
            // Dash 8 on a Kingscote hop is flying a Regional route, not a Domestic one.
            AvailabilityLine = $"{BandLabel(band)} route. You can fly it";
            AvailabilityTone = HudTone.Caution;
            if (OperatingNote.Length > 0)
                AvailabilityLine = OperatingNote;

            var delay = FlightPlanner.ClampDelay(departureDelaySeconds, aircraft, now);
            var departAt = AirlineOperations.WholeMinute(now.Advance(delay));
            DepartureLabel = clock.TimeText(departAt);
            DepartureDetail = $"in {Duration(delay)}  ·  "
                              + $"{Duration(operations.AirborneSeconds(aircraft, destination))} each way";

            if (aircraft.MaintenanceJob != null)
            {
                PlanBlockedReason = $"{aircraft.Registration} is in its check: {Maintenance.Status(aircraft, now, clock)}";
                return;
            }
            if (aircraft.State != FleetState.AtStand)
            {
                PlanBlockedReason = $"{aircraft.Registration} has to be parked at Adelaide first";
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
                    $"{(alreadyPaid > 0 ? "The change" : "This flight")} costs ${changeCost:N0}. You have ${operations.CareerState.Funds:N0}";
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
            RouteBand.Pacific => "Pacific",
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
        public static void Paint(HudDrawList into, RouteMapWorkspaceModel model, RouteMapWorkspaceLayout layout, bool showDetail = true)
        {
            if (into == null || model == null)
                return;

            into.Clear();
            into.Surface(layout.Surface);

            // The map well is darker than the surface so the coastline and routes read.
            into.Fill(layout.Surface, HudTone.Default, 1f, "#17242C");
            HudShellPainter.PaintSheetHeader(into, layout.Surface, model.Title, string.Empty, layout.TitleBox, HudBox.Empty);
            into.Fill(layout.Map, HudTone.Default, 1f, "#101C24");
            if (showDetail) PaintDetail(into, model, layout);
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

            if (pane.Height < 620f)
            {
                PaintCompactDetail(into, model, pane);
                return;
            }

            into.Text(new HudBox(pane.X, pane.Y, pane.Width, 30f), model.DestinationTitle, 22f,
                HudTone.Default, HudTextStyle.Bold | HudTextStyle.Caption);
            into.Caption(new HudBox(pane.X, pane.Y + 27f, pane.Width, 14f), "DESTINATION",
                model.CareerLine.Length > 0 ? HudTone.Caution : HudTone.Muted);
            into.Hairline(new HudBox(pane.X, pane.Y + 46f, pane.Width, 1f));

            var y = pane.Y + 58f;
            PaintFact(into, pane, ref y, "ROUTE", model.BandAndDistance, HudTone.Default);
            PaintFact(into, pane, ref y, "AIRCRAFT", model.CompatibilityLine, HudTone.Default);
            if (model.DispatchLine.Length > 0)
            {
                PaintFact(into, pane, ref y, "PAY NOW", model.DispatchLine, HudTone.Default);
                PaintFact(into, pane, ref y, "RETURN", model.ReturnLine, HudTone.Default);
            }

            into.Text(new HudBox(pane.X, y, pane.Width, 24f), model.ProfitLine, 17f, model.ProfitTone, HudTextStyle.Bold);
            y += 30f;
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
            if (model.CanDelegate)
            {
                into.Button(new HudBox(pane.X, buttonY + 50f, (pane.Width - 8f) * 0.5f, 30f),
                    model.RepeatLabel, HudAction.RepeatFlight, HudButtonStyle.Secondary);
                into.Button(new HudBox(pane.X + (pane.Width + 8f) * 0.5f, buttonY + 50f,
                    (pane.Width - 8f) * 0.5f, 30f), "COMPARE ROUTES", HudAction.ResetMap,
                    HudButtonStyle.Secondary);
            }
            else
                into.Button(new HudBox(pane.X, buttonY + 50f, pane.Width, 30f), "COMPARE ROUTES",
                    HudAction.ResetMap, HudButtonStyle.Secondary);
            if (!model.CanPlan && model.PlanBlockedReason.Length > 0)
                into.Text(new HudBox(pane.X, buttonY - 34f, pane.Width, 32f), model.PlanBlockedReason, 11f,
                    HudTone.Muted, HudTextStyle.Wrap);
            else if (model.HasBooking)
                into.Text(new HudBox(pane.X, buttonY - 34f, pane.Width, 32f), model.BookingLine, 11f,
                    HudTone.Muted, HudTextStyle.Wrap);
        }

        /// <summary>Keep booking controls fixed and reachable in a short desktop window.</summary>
        private static void PaintCompactDetail(HudDrawList into, RouteMapWorkspaceModel model, HudBox pane)
        {
            into.Text(new HudBox(pane.X, pane.Y, pane.Width, 28f), model.DestinationTitle, 20f, HudTone.Default, HudTextStyle.Bold);
            var buttonY = pane.Bottom - 84f;
            var controlsY = buttonY - 116f;
            var y = pane.Y + 36f;
            void Line(string text, HudTone tone, float height = 20f)
            {
                if (string.IsNullOrEmpty(text) || y + height > controlsY - 8f) return;
                into.Text(new HudBox(pane.X, y, pane.Width, height), text, 11f, tone, HudTextStyle.Wrap);
                y += height + 2f;
            }
            Line(model.ProfitLine, model.ProfitTone, 24f);
            Line(model.BandAndDistance, HudTone.Muted);
            Line(model.DispatchLine, HudTone.Default);
            Line(model.ReturnLine, HudTone.Default);
            Line(model.AvailabilityLine, model.AvailabilityTone, 28f);
            Line(model.CareerLine, model.CareerTone, 28f);
            if (model.CareerLine.Length > 0 && y + 28f <= controlsY - 8f)
            {
                into.Button(new HudBox(pane.X, y, Math.Min(pane.Width, 132f), 28f), "VIEW CONTRACTS",
                    HudAction.ViewContracts, HudButtonStyle.Secondary);
            }
            if (model.DepartureLabel.Length > 0)
            {
                into.Button(new HudBox(pane.X, controlsY, pane.Width, 28f), model.AircraftLabel + "   ▾",
                    HudAction.NextAircraft, HudButtonStyle.Secondary, model.CanCycleAircraft);
                var rowY = controlsY + 34f;
                into.Button(new HudBox(pane.X, rowY, 34f, 28f), "−", HudAction.PreviousDeparture, HudButtonStyle.Secondary);
                into.Button(new HudBox(pane.X + 40f, rowY, pane.Width - 80f, 28f), model.DepartureLabel,
                    HudAction.NextDeparture, HudButtonStyle.Secondary);
                into.Button(new HudBox(pane.Right - 34f, rowY, 34f, 28f), "+", HudAction.NextDeparture, HudButtonStyle.Secondary);
                into.Text(new HudBox(pane.X, rowY + 32f, pane.Width, 16f), model.DepartureDetail, 11f, HudTone.Muted);
            }
            var notice = !model.CanPlan ? model.PlanBlockedReason : model.HasBooking ? model.BookingLine : string.Empty;
            if (!string.IsNullOrEmpty(notice))
                into.Text(new HudBox(pane.X, buttonY - 32f, pane.Width, 30f), notice, 11f, HudTone.Muted, HudTextStyle.Wrap);
            into.Button(new HudBox(pane.X, buttonY, pane.Width, 44f), model.PlanLabel, HudAction.PlanFlight,
                HudButtonStyle.Primary, model.CanPlan);
            var reset = new HudBox(pane.X, buttonY + 50f, pane.Width, 30f);
            if (model.CanDelegate)
            {
                var width = (pane.Width - 8f) * .5f;
                into.Button(reset.WithWidth(width), model.RepeatLabel, HudAction.RepeatFlight, HudButtonStyle.Secondary);
                reset = new HudBox(pane.X + width + 8f, reset.Y, width, reset.Height);
            }
            into.Button(reset, "COMPARE ROUTES", HudAction.ResetMap, HudButtonStyle.Secondary);
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
                    ? "Expected profit per round trip · best first."
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
                    careerTarget ? $"CAREER TARGET · {row.Destination.Region}" : row.Destination.Region,
                    11f, careerTarget ? HudTone.Caution : HudTone.Muted,
                    careerTarget ? HudTextStyle.Bold : HudTextStyle.Regular);
                into.Text(new HudBox(box.Right - 96f, box.Y, 96f, 17f), $"{(row.Profit >= 0 ? "+" : "−")}${Math.Abs(row.Profit):N0}", 12f,
                    row.Reachable && row.Profit >= 0 ? HudTone.Positive : HudTone.Muted, HudTextStyle.Regular, HudAlign.Right);
                into.Text(new HudBox(box.Right - 96f, box.Y + 16f, 96f, 15f),
                    row.Reachable ? RouteMapWorkspaceModel.Duration(row.AirborneSeconds * 2 + AirlineOperations.DestinationTurnaroundSeconds) : "locked", 11f,
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
            PaintGeography(into, map, lens);

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

                // Labels never pile up: home and the selected city claim their space first, then
                // each other city is labelled only where it fits (its dot is always drawn).
                var taken = new List<HudBox>();
                Project(lens, map, home.Longitude, home.Latitude, out var homeX, out var homeY);
                taken.Add(new HudBox(homeX + 10f, homeY - 9f, HudShell.Measure(home.Name, 13f), 17f));
                if (selected.HasValue)
                {
                    Project(lens, map, selected.Value.Longitude, selected.Value.Latitude, out var sx, out var sy);
                    taken.Add(new HudBox(sx + 9f, sy - 9f, HudShell.Measure(selected.Value.Name, 12f) + 26f, 17f));
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
                    var overseas = !row.Destination.IsAustralian;
                    // Overseas cities are few and far apart, so they keep their names further out.
                    var label = isSelected || lens.Zoom >= 4f || (overseas && lens.Zoom >= 0.45f)
                        ? row.Destination.Name
                        : lens.Zoom >= 2f || overseas ? row.Destination.Code : string.Empty;
                    var labelBox = new HudBox(x + 9f, y - 9f, HudShell.Measure(label, 12f) + (overseas ? 26f : 0f), 17f);
                    // The selected city always shows its name; beside home it drops below instead of over it.
                    var labelDy = isSelected && Collides(labelBox, taken.GetRange(0, 1)) ? 16f : 0f;
                    if (label.Length > 0 && !isSelected && Collides(labelBox, taken))
                        label = string.Empty;
                    else if (label.Length > 0 && !isSelected)
                        taken.Add(labelBox);
                    if (label.Length > 0)
                    {
                        var style = isSelected ? HudTextStyle.Bold : HudTextStyle.Regular;
                        into.Text(new HudBox(x + 9f, y - 9f + labelDy, 128f, 17f), label, 12f,
                            row.Reachable ? HudTone.Default : HudTone.Muted, style);
                        if (overseas)
                            CountryChip(into, x + 11f + HudShell.Measure(label, 12f), y - 7f + labelDy,
                                row.Destination.Country, row.Reachable);
                    }
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

        /// <summary>Runway detail for home and every destination in view (ADR 0140).</summary>
        public static void PaintAirports(HudDrawList into, HudBox map, AustraliaMapLens lens,
            IReadOnlyList<PlannerDestination> destinations, Destination home)
        {
            if (into == null || lens == null || lens.Zoom < AustraliaMapLens.AirportDetailZoom)
                return;
            PaintAirportDetail(into, map, lens, home);
            if (destinations != null)
                foreach (var row in destinations)
                    PaintAirportDetail(into, map, lens, row.Destination);
        }

        /// <summary>
        /// ADR 0140: coastline at the zoom's level of detail, Australian state borders, towns once
        /// zoomed in, and each airport's runways once zoomed right in. Points closer than a few pixels
        /// are skipped, so a detailed coast costs no more than the view can show.
        /// </summary>
        public static void PaintGeography(HudDrawList into, HudBox map, AustraliaMapLens lens)
        {
            lens.GuiToLonLat(map.X, map.Y, map.Width, map.Height, map.X, map.Y, out var west, out var north);
            lens.GuiToLonLat(map.X, map.Y, map.Width, map.Height, map.Right, map.Bottom, out var east, out var south);
            var view = (W: west, E: east, S: south, N: north);
            var coastWidth = lens.Detail == MapDetail.Detail ? 1.4f : 1.6f;
            // Zoomed right in, the fine coast drawn around each airport takes over there, and the
            // detail coast is skipped inside those boxes so the shore is not drawn twice.
            var fine = lens.Zoom >= MapGeography.AirportCoastZoom;
            foreach (var ring in MapGeography.Coasts(lens.Detail))
                if (Overlaps(MapGeography.BoundsOf(ring), view))
                    Polyline(into, map, lens, ring, HudTone.Muted, coastWidth, fine);
            if (fine)
                foreach (var piece in MapGeographyData.AirportCoasts)
                    if (Overlaps(MapGeography.BoundsOf(piece), view))
                        Polyline(into, map, lens, piece, HudTone.Muted, coastWidth);
            if (lens.Zoom >= 0.6f)
                foreach (var border in MapGeographyData.StateBorders)
                    if (Overlaps(MapGeography.BoundsOf(border), view))
                        Polyline(into, map, lens, border, HudTone.Muted, 0.8f);
            PaintTowns(into, map, lens, view);
        }

        private static bool Collides(HudBox box, List<HudBox> taken)
        {
            foreach (var other in taken)
                if (box.X < other.Right && other.X < box.Right && box.Y < other.Bottom && other.Y < box.Bottom)
                    return true;
            return false;
        }

        private static bool Overlaps((float W, float E, float S, float N) b, (float W, float E, float S, float N) view) =>
            !(b.E < view.W || b.W > view.E || b.N < view.S || b.S > view.N);

        /// <summary>Towns as small grey dots, the biggest first, dropping labels that would overlap.</summary>
        private static void PaintTowns(HudDrawList into, HudBox map, AustraliaMapLens lens,
            (float W, float E, float S, float N) view)
        {
            var rank = lens.TownRank;
            if (rank < 0)
                return;
            var taken = new List<HudBox>();
            foreach (var town in MapGeographyData.Towns)
            {
                if (town.Rank > rank || town.Lon < view.W || town.Lon > view.E || town.Lat < view.S || town.Lat > view.N
                    || MapGeography.IsAirportTown(town.Name, town.Lon, town.Lat))
                    continue;
                Project(lens, map, town.Lon, town.Lat, out var x, out var y);
                if (!map.Contains(x, y))
                    continue;
                var size = town.Rank <= 1 ? 10f : 9f;
                var box = new HudBox(x + 5f, y - 7f, HudShell.Measure(town.Name, size) + 2f, 13f);
                var clear = true;
                foreach (var other in taken)
                    if (box.X < other.Right && other.X < box.Right && box.Y < other.Bottom && other.Y < box.Bottom)
                        clear = false;
                into.Dot(x, y, town.Rank <= 1 ? 4f : 3f, HudTone.Muted);
                if (!clear || taken.Count > 60)
                    continue;
                taken.Add(box);
                into.Text(box, town.Name, size, HudTone.Muted);
            }
        }

        /// <summary>A small pill with the country's ISO code beside an overseas city (ADR 0140).</summary>
        private static void CountryChip(HudDrawList into, float x, float y, string iso, bool reachable)
        {
            var box = new HudBox(x, y, 22f, 13f);
            into.Fill(box, reachable ? HudTone.Accent : HudTone.Muted, 0.28f);
            into.Text(box, iso, 8.5f, reachable ? HudTone.Default : HudTone.Muted, HudTextStyle.Bold, HudAlign.Center);
        }

        /// <summary>
        /// Zoomed right in on an airport (ADR 0140): its runways to scale with their numbers, and
        /// its name, code and elevation.
        /// </summary>
        public static void PaintAirportDetail(HudDrawList into, HudBox map, AustraliaMapLens lens, Destination airport)
        {
            if (lens.Zoom < AustraliaMapLens.AirportDetailZoom)
                return;
            Project(lens, map, airport.Longitude, airport.Latitude, out var ax, out var ay);
            var margin = 400f;
            if (ax < map.X - margin || ax > map.Right + margin || ay < map.Y - margin || ay > map.Bottom + margin)
                return;
            var metresPerPixel = 111_320f / lens.PixelsPerDegree(map.Width, map.Height);
            var any = false;
            foreach (var runway in MapGeography.RunwaysAt(airport.Code))
            {
                any = true;
                Project(lens, map, runway.Lon1, runway.Lat1, out var x1, out var y1);
                Project(lens, map, runway.Lon2, runway.Lat2, out var x2, out var y2);
                var width = Math.Max(2.5f, runway.WidthM / metresPerPixel);
                Clipped(into, map, x1, y1, x2, y2, HudTone.Default, width);
                if (map.Contains(x1, y1))
                    into.Text(new HudBox(x1 - 14f, y1 - 7f, 28f, 14f), runway.End1, 9f, HudTone.Caution,
                        HudTextStyle.Bold, HudAlign.Center);
                if (map.Contains(x2, y2))
                    into.Text(new HudBox(x2 - 14f, y2 - 7f, 28f, 14f), runway.End2, 9f, HudTone.Caution,
                        HudTextStyle.Bold, HudAlign.Center);
            }

            if (!any || !map.Contains(ax, ay))
                return;
            var elevation = MapGeography.ElevationFt(airport.Code);
            var line = $"{MapGeography.AirportName(airport.Code)} · {airport.Code}"
                       + (elevation.HasValue ? $" · {elevation.Value:N0} ft" : string.Empty);
            into.Text(new HudBox(ax + 14f, ay + 24f, 320f, 16f), line, 11f, HudTone.Default, HudTextStyle.Bold);
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
            var prevLon = lon;
            for (var i = 1; i <= segments; i++)
            {
                RouteMap.DestinationPoint(home.Latitude, home.Longitude, distanceKm,
                    i * 360.0 / segments, out lat, out lon);
                Project(lens, map, lon, lat, out var nx, out var ny);
                if ((i & 1) == 0 && !Wraps(prevLon, lon))
                    Clipped(into, map, px, py, nx, ny, HudTone.Muted, 0.65f);
                px = nx;
                py = ny;
                prevLon = lon;
            }

            RouteMap.DestinationPoint(home.Latitude, home.Longitude, distanceKm, 90.0,
                out lat, out lon);
            Project(lens, map, lon, lat, out var lx, out var ly);
            if (map.Contains(lx, ly))
                into.Text(new HudBox(lx + 4f, ly - 15f, 190f, 15f),
                    string.IsNullOrEmpty(label) ? $"{distanceKm:N0} km range" : label, 9f,
                    HudTone.Muted);
        }

        /// <summary>Screen pixels a coast vertex must move before it is drawn (ADR 0140).</summary>
        public const float MinSegmentPixels = 2.5f;

        private static void Polyline(HudDrawList into, HudBox map, AustraliaMapLens lens, float[] lonLat,
            HudTone tone, float thickness, bool skipNearAirports = false)
        {
            var count = lonLat.Length / 2;
            if (count < 2)
                return;
            Project(lens, map, lonLat[0], lonLat[1], out var x0, out var y0);
            var nearBefore = skipNearAirports && MapGeography.NearAirport(lonLat[0], lonLat[1]);
            for (var i = 1; i < count; i++)
            {
                Project(lens, map, lonLat[i * 2], lonLat[i * 2 + 1], out var x1, out var y1);
                var last = i == count - 1;
                if (!last && Math.Abs(x1 - x0) + Math.Abs(y1 - y0) < MinSegmentPixels)
                    continue;
                var near = skipNearAirports && MapGeography.NearAirport(lonLat[i * 2], lonLat[i * 2 + 1]);
                if (!(near && nearBefore))
                    Clipped(into, map, x0, y0, x1, y1, tone, thickness);
                x0 = x1;
                y0 = y1;
                nearBefore = near;
            }
        }

        /// <summary>Two points more than half the world apart in longitude never join (antimeridian guard).</summary>
        private static bool Wraps(double lonA, double lonB) =>
            Math.Abs(AustraliaMapLens.Unwrap(lonA) - AustraliaMapLens.Unwrap(lonB)) > 180.0;

        private static void GreatCircle(HudDrawList into, HudBox map, AustraliaMapLens lens, Destination from,
            Destination to, HudTone tone, float thickness)
        {
            const int segments = 24;
            RouteMap.GreatCirclePoint(from.Latitude, from.Longitude, to.Latitude, to.Longitude, 0.0,
                out var lat, out var lon);
            Project(lens, map, lon, lat, out var px, out var py);
            var prevLon = lon;
            for (var i = 1; i <= segments; i++)
            {
                RouteMap.GreatCirclePoint(from.Latitude, from.Longitude, to.Latitude, to.Longitude,
                    i / (double)segments, out lat, out lon);
                Project(lens, map, lon, lat, out var nx, out var ny);
                if (!Wraps(prevLon, lon))
                    Clipped(into, map, px, py, nx, ny, tone, thickness);
                px = nx;
                py = ny;
                prevLon = lon;
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
