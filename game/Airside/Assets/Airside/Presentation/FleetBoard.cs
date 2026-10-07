using System;
using System.Collections.Generic;
using Airside.Domain;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>Which aircraft the Fleet roster lists by what they are doing (ADR 0239).</summary>
    public enum FleetStatusFilter { All, Flying, Parked, Check, Available }

    /// <summary>How the roster orders the aircraft inside each base.</summary>
    public enum FleetSortMode { Fleet, Status, Type, Earnings }

    /// <summary>Action ids owned by the unified Fleet workspace (ADR 0239).</summary>
    public static class FleetActions
    {
        public const string ToggleDetails = "fleet:details";
        public const string CancelRoute = "fleet:cancel-route";
        public const string ConfirmCancel = "fleet:confirm-cancel";
        public const string ToggleMarket = "fleet:market";
        public const string Available = "fleet:available";
        public const string All = "fleet:all";
        public const string ConfirmRoute = "fleet:confirm-route";
        public const string BackRoutes = "fleet:back-routes";
        public const string CycleStatus = "fleet:status";
        public const string CycleSort = "fleet:sort";
        public const string BasePrefix = "fleet:base:";
        public const string OpenBasePrefix = "fleet:open:";
        public const string RoutePrefix = "fleet:route:";
        public const string RepeatPrefix = "fleet:repeat:";
        public const string RemoveRepeat = "fleet:unrepeat";
        public const string RoutesPrevious = "fleet:routes-previous";
        public const string RoutesNext = "fleet:routes-next";
        public const string Sell = "fleet:sell";
        public const string MoveBase = "fleet:move";
        public const string OutstationCheck = "fleet:check";

        public static string Base(string code) => BasePrefix + code;
        public static string OpenBase(string code) => OpenBasePrefix + code;
        public static string Route(string code) => RoutePrefix + code;
        public static string Repeat(string code) => RepeatPrefix + code;
    }

    /// <summary>What the player has asked the Fleet roster to show. Presentation state only.</summary>
    public sealed class FleetBoardState
    {
        /// <summary>A base code to list, or empty for every base. It is also where BUY delivers.</summary>
        public string BaseFilter { get; set; } = string.Empty;

        public bool ShowDetails { get; set; }
        public bool CancelReview { get; set; }
        public bool ShowMarket { get; set; }
        public FleetStatusFilter Status { get; set; }
        public FleetSortMode Sort { get; set; }

        /// <summary>Which page of route rows an outstation aircraft shows.</summary>
        public int RoutePage { get; set; }
        public string ReviewRoute { get; set; } = string.Empty;

        /// <summary>Open an owned aircraft from Operations without stale filters hiding it.</summary>
        public void Focus(PlayerFleetEntry entry)
        {
            if (entry == null) return;
            BaseFilter = entry.BaseCode;
            Status = FleetStatusFilter.All;
            ShowMarket = false;
            RoutePage = 0;
            ReviewRoute = string.Empty;
            CancelReview = false;
        }

        public void ToggleBase(string code)
        {
            BaseFilter = BaseFilter == code ? string.Empty : code;
            RoutePage = 0;
            ReviewRoute = string.Empty;
            CancelReview = false;
        }

        public void CycleStatus() => Status = (FleetStatusFilter)(((int)Status + 1) % 5);

        public void CycleSort() => Sort = (FleetSortMode)(((int)Sort + 1) % 4);

        public static string StatusLabel(FleetStatusFilter status) => status switch
        {
            FleetStatusFilter.Available => "AVAILABLE",
            FleetStatusFilter.Flying => "FLYING",
            FleetStatusFilter.Parked => "PARKED",
            FleetStatusFilter.Check => "CHECKS",
            _ => "ALL"
        };

        public static string SortLabel(FleetSortMode sort) => sort switch
        {
            FleetSortMode.Status => "STATUS",
            FleetSortMode.Type => "TYPE",
            FleetSortMode.Earnings => "EARNINGS",
            _ => "FLEET"
        };

        public bool Passes(PlayerFleetEntry entry)
        {
            if (entry == null)
                return false;
            if (BaseFilter.Length > 0 && entry.BaseCode != BaseFilter)
                return false;
            return Status switch
            {
                FleetStatusFilter.Available => entry.Kind == PlayerFleetKind.Parked && !entry.CheckDue && !entry.InCheck,
                FleetStatusFilter.Flying => entry.IsFlying,
                FleetStatusFilter.Parked => entry.Kind == PlayerFleetKind.Parked || entry.Kind == PlayerFleetKind.Booked,
                FleetStatusFilter.Check => entry.InCheck || entry.CheckDue,
                _ => true
            };
        }

        /// <summary>Base order for grouping: Adelaide first, then the outstations in their fixed order.</summary>
        public static int BaseOrder(string code)
        {
            if (code == "ADL")
                return 0;
            for (var i = 0; i < AirlineOperations.OutstationCandidates.Count; i++)
                if (AirlineOperations.OutstationCandidates[i] == code)
                    return i + 1;
            return AirlineOperations.OutstationCandidates.Count + 1;
        }

        /// <summary>Lower sorts first: what needs the player, then what is flying, then what is idle.</summary>
        public static int StatusRank(PlayerFleetEntry entry)
        {
            if (entry.CheckDue || entry.InCheck)
                return 0;
            if (entry.IsFlying)
                return 1;
            return entry.Kind == PlayerFleetKind.Booked ? 2 : 3;
        }
    }

    /// <summary>One base on the Fleet workspace's bases strip.</summary>
    public readonly struct FleetBaseChip
    {
        public FleetBaseChip(string code, string name, bool isOpen, int aircraft, int capacity, bool selected,
            long openCost, bool canOpen, string openReason)
        {
            Code = code ?? string.Empty;
            Name = name ?? string.Empty;
            IsOpen = isOpen;
            Aircraft = aircraft;
            Capacity = capacity;
            Selected = selected;
            OpenCost = openCost;
            CanOpen = canOpen;
            OpenReason = openReason ?? string.Empty;
        }

        public string Code { get; }
        public string Name { get; }
        public bool IsOpen { get; }
        public int Aircraft { get; }
        public int Capacity { get; }
        public bool Selected { get; }
        public long OpenCost { get; }
        public bool CanOpen { get; }

        /// <summary>The first unmet requirement for opening it, or empty when only money is missing or all is met.</summary>
        public string OpenReason { get; }
    }

    /// <summary>One destination an outstation aircraft could be sent to, with what it would earn.</summary>
    public readonly struct FleetRouteRow
    {
        public FleetRouteRow(string code, string name, int km, string forecast, bool profitable, bool repeats,
            bool repeatPaused)
        {
            Code = code ?? string.Empty;
            Name = name ?? string.Empty;
            Km = km;
            Forecast = forecast ?? string.Empty;
            Profitable = profitable;
            Repeats = repeats;
            RepeatPaused = repeatPaused;
        }

        public string Code { get; }
        public string Name { get; }
        public int Km { get; }
        public string Forecast { get; }
        public bool Profitable { get; }

        /// <summary>A repeat plan to this destination exists for the aircraft.</summary>
        public bool Repeats { get; }

        public bool RepeatPaused { get; }
    }

    /// <summary>
    /// The three camera views for the selected aircraft. Availability depends on live Unity state (engines,
    /// the view area), so the HUD fills it in after the model is built; the model itself never decides it.
    /// </summary>
    public sealed class FleetCameraAvailability
    {
        public bool Visible { get; set; }
        public bool Cockpit { get; set; }
        public bool Window { get; set; }
        public bool Exterior { get; set; }
        public string Hint { get; set; } = string.Empty;

        public void Clear()
        {
            Visible = false;
            Cockpit = false;
            Window = false;
            Exterior = false;
            Hint = string.Empty;
        }
    }

    /// <summary>
    /// The one status vocabulary for the player's fleet (ADR 0239). Roster rows, the profile pane and the
    /// map all read it, so one aircraft never reads differently on two screens.
    /// </summary>
    public static class FleetStatusText
    {
        public static string For(PlayerFleetEntry entry, SimulationTime now, AirlineClock clock, PlayerBaseLevel baseLevel)
        {
            if (entry == null)
                return string.Empty;
            return entry.IsOutstation
                ? ForOutstation(entry, now, clock)
                : ForLive(entry.Live, now, baseLevel);
        }

        public static StatusSeverity SeverityFor(PlayerFleetEntry entry, SimulationTime now)
        {
            if (entry == null)
                return StatusSeverity.Normal;
            if (!entry.IsOutstation)
                return AircraftStatus.Severity(entry.Live, now);
            return entry.CheckDue && !entry.InCheck ? StatusSeverity.Attention : StatusSeverity.Normal;
        }

        /// <summary>Roster wording for an aircraft at Adelaide: the prep stage while a booked departure turns round.</summary>
        public static string ForLive(FleetAircraft aircraft, SimulationTime now, PlayerBaseLevel baseLevel)
        {
            if (aircraft.IsFerry && aircraft.CurrentDestination.HasValue)
                return $"Ferry in from {aircraft.CurrentDestination.Value.Name}";
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

        public static string ForOutstation(PlayerFleetEntry entry, SimulationTime now, AirlineClock clock)
        {
            clock ??= AirlineClock.Default;
            var aircraft = entry.Outstation;
            if (aircraft == null)
                return string.Empty;
            if (entry.InCheck)
                return $"In check until {clock.TimeText(new SimulationTime(entry.CheckUntilSeconds))}";
            if (entry.Kind == PlayerFleetKind.Booked)
                return $"Departs {clock.TimeText(new SimulationTime(entry.NextAtSeconds))} for {NameOf(entry.DestinationCode)}";
            if (entry.Kind == PlayerFleetKind.Airborne)
            {
                PlayerFleet.TryPosition(aircraft, now, out _, out _, out var phase, out _);
                return phase switch
                {
                    OutstationPhase.Outbound => $"Flying to {NameOf(entry.DestinationCode)}",
                    OutstationPhase.Turnaround => $"On the ground at {NameOf(entry.DestinationCode)}",
                    OutstationPhase.Inbound => $"Returning to {NameOf(entry.BaseCode)}",
                    _ => $"On service to {NameOf(entry.DestinationCode)}"
                };
            }

            return entry.CheckDue ? "Check due" : "Available";
        }

        public static string NameOf(string code) =>
            !string.IsNullOrEmpty(code) && DestinationCatalogue.TryFind(code, out var destination)
                ? destination.Name
                : code ?? string.Empty;
    }

    /// <summary>Orders the roster: grouped by base, then by the chosen mode inside each base.</summary>
    public sealed class FleetEntryComparer : IComparer<PlayerFleetEntry>
    {
        private static readonly FleetEntryComparer[] Cache =
        {
            new FleetEntryComparer(FleetSortMode.Fleet), new FleetEntryComparer(FleetSortMode.Status),
            new FleetEntryComparer(FleetSortMode.Type), new FleetEntryComparer(FleetSortMode.Earnings)
        };

        private readonly FleetSortMode _mode;

        private FleetEntryComparer(FleetSortMode mode) => _mode = mode;

        public static FleetEntryComparer For(FleetSortMode mode) => Cache[(int)mode];

        public int Compare(PlayerFleetEntry a, PlayerFleetEntry b)
        {
            var byBase = FleetBoardState.BaseOrder(a.BaseCode).CompareTo(FleetBoardState.BaseOrder(b.BaseCode));
            if (byBase != 0)
                return byBase;
            var byMode = 0;
            switch (_mode)
            {
                case FleetSortMode.Status:
                    byMode = FleetBoardState.StatusRank(a).CompareTo(FleetBoardState.StatusRank(b));
                    break;
                case FleetSortMode.Type:
                    byMode = string.CompareOrdinal(a.Type.Name, b.Type.Name);
                    break;
                case FleetSortMode.Earnings:
                    byMode = b.LifetimeRevenue.CompareTo(a.LifetimeRevenue);
                    break;
            }

            // List.Sort is not stable; the original position keeps equal aircraft in fleet order.
            return byMode != 0 ? byMode : a.Order.CompareTo(b.Order);
        }
    }
}
