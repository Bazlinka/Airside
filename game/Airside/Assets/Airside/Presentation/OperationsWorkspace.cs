using System;
using System.Collections.Generic;
using Airside.Domain;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>Which half of the movement board is showing.</summary>
    public enum OperationsBoardTab
    {
        Departures,
        Arrivals
    }

    /// <summary>One movement on the Operations board. Every value is read from live state.</summary>
    public readonly struct OperationsFlightRow
    {
        public OperationsFlightRow(string registration, string scheduledTime, string estimatedTime,
            string flightNumber, string route, string stand, string status, string typeName,
            string operatorName, string liveryHex, StatusSeverity severity, bool isPlayer,
            bool hasProgress, float progress01, bool onField = true, bool isPast = false)
        {
            Registration = registration ?? string.Empty;
            ScheduledTime = scheduledTime ?? string.Empty;
            EstimatedTime = estimatedTime ?? string.Empty;
            FlightNumber = flightNumber ?? string.Empty;
            Route = route ?? string.Empty;
            Stand = stand ?? string.Empty;
            Status = status ?? string.Empty;
            TypeName = typeName ?? string.Empty;
            OperatorName = operatorName ?? string.Empty;
            LiveryHex = liveryHex ?? AirsidePalette.ConcreteHex;
            Severity = severity;
            IsPlayer = isPlayer;
            HasProgress = hasProgress;
            Progress01 = progress01;
            OnField = onField;
            IsPast = isPast;
        }

        public string Registration { get; }
        public string ScheduledTime { get; }
        public string EstimatedTime { get; }
        public string FlightNumber { get; }
        public string Route { get; }
        public string Stand { get; }
        public string Status { get; }
        public string TypeName { get; }
        public string OperatorName { get; }
        public string LiveryHex { get; }
        public StatusSeverity Severity { get; }

        /// <summary>Your airline reads at full contrast; other operators stay visible but subordinate.</summary>
        public bool IsPlayer { get; }

        public bool HasProgress { get; }
        public float Progress01 { get; }

        /// <summary>
        /// True when a live fleet aircraft is actually flying this slot (parked, taxiing,
        /// airborne on the field). False for the published day-plan overlay — those rows
        /// must not claim a stand, because nothing is parked there.
        /// </summary>
        public bool OnField { get; }

        /// <summary>Scheduled time is earlier than now and the movement is not live — mute on the board.</summary>
        public bool IsPast { get; }

        /// <summary>The estimate is only worth printing when it differs from the scheduled time.</summary>
        public bool ShowsEstimate =>
            EstimatedTime.Length > 0 && EstimatedTime != "—" && EstimatedTime != ScheduledTime;

        public HudTone StatusTone => Severity switch
        {
            StatusSeverity.Warning => HudTone.Negative,
            StatusSeverity.Attention => HudTone.Caution,
            _ => HudTone.Default
        };
    }

    /// <summary>One player exception or imminent commitment, pinned above the board.</summary>
    public readonly struct OperationsAttentionRow
    {
        public OperationsAttentionRow(string registration, string text, StatusSeverity severity)
        {
            Registration = registration ?? string.Empty;
            Text = text ?? string.Empty;
            Severity = severity;
        }

        public string Registration { get; }
        public string Text { get; }
        public StatusSeverity Severity { get; }

        public HudTone Tone => Severity == StatusSeverity.Warning ? HudTone.Negative : HudTone.Caution;
    }

    /// <summary>One turnaround stage on the selected-flight pane.</summary>
    public readonly struct OperationsPrepCheck
    {
        public OperationsPrepCheck(string label, bool done, bool active)
        {
            Label = label ?? string.Empty;
            Done = done;
            Active = active;
        }

        public string Label { get; }
        public bool Done { get; }
        public bool Active { get; }

        public HudTone Tone => Done ? HudTone.Positive : Active ? HudTone.Caution : HudTone.Muted;
    }

    /// <summary>One line of the event history strip along the bottom of the workspace.</summary>
    public readonly struct OperationsEventLine
    {
        public OperationsEventLine(string time, string text)
        {
            Time = time ?? string.Empty;
            Text = text ?? string.Empty;
        }

        public string Time { get; }
        public string Text { get; }
    }

    /// <summary>
    /// The Operations workspace as data (ADR 0057): a detailed movement board for the whole
    /// airport, player exceptions pinned above it, and the actionable detail of whichever
    /// flight is selected.
    ///
    /// Every string comes from live simulation and career state — this projects, it never
    /// decides. UnityEngine-free so the headless harness and the offline mockup renderer
    /// build exactly what the runtime HUD draws.
    /// </summary>
    public sealed class OperationsWorkspaceModel
    {
        private readonly List<OperationsFlightRow> _rows = new();
        private readonly List<OperationsAttentionRow> _attention = new();
        private readonly List<OperationsPrepCheck> _prep = new();
        private readonly List<OperationsEventLine> _events = new();
        private readonly List<FleetAircraft> _scratch = new();
        private readonly List<float> _dayDensity = new();
        private readonly List<float> _dayMarks = new();

        public string Title { get; private set; } = "OPERATIONS";
        public string Subtitle { get; private set; } = string.Empty;

        /// <summary>True while a storm holds every new landing/takeoff clearance (ADR 0058).</summary>
        public bool GroundStopped { get; private set; }

        public OperationsBoardTab Tab { get; private set; }

        /// <summary>0..1 through the operating day (05:00–23:00 Adelaide).</summary>
        public float DayProgress01 { get; private set; }

        /// <summary>"14:32 · evening bank · 3 on field · 8 listed ahead"</summary>
        public string DayCaption { get; private set; } = string.Empty;

        public int DayDoneCount { get; private set; }
        public int DayActiveCount { get; private set; }
        public int DayUpcomingCount { get; private set; }

        /// <summary>Live aircraft currently at Adelaide (parked, taxiing, holding, landing).</summary>
        public int DayOnFieldCount { get; private set; }

        /// <summary>Published day-plan rows still ahead that have no live aircraft covering them.</summary>
        public int DayListedAheadCount { get; private set; }

        /// <summary>
        /// First board row that is not a muted past movement — used to open the list
        /// near "now" instead of at 05:00 Landed/Departed.
        /// </summary>
        public int FirstActiveRowIndex
        {
            get
            {
                for (var i = 0; i < _rows.Count; i++)
                    if (!_rows[i].IsPast)
                        return i;
                return 0;
            }
        }

        /// <summary>
        /// Index of the first non-past row — same as <see cref="FirstActiveRowIndex"/>, exposed
        /// so the painter can draw a persistent NOW divider between past and upcoming.
        /// </summary>
        public int NowDividerRowIndex => FirstActiveRowIndex;

        /// <summary>Local-hour density samples across the strip (one per operating hour).</summary>
        public IReadOnlyList<float> DayDensity => _dayDensity;

        /// <summary>0..1 marks on the day strip for on-field movements (where metal actually is).</summary>
        public IReadOnlyList<float> DayMarks => _dayMarks;

        public IReadOnlyList<OperationsFlightRow> Rows => _rows;
        public IReadOnlyList<OperationsAttentionRow> Attention => _attention;
        public IReadOnlyList<OperationsEventLine> Events => _events;

        /// <summary>The registration the board and the detail pane agree on, or empty.</summary>
        public string SelectedRegistration { get; private set; } = string.Empty;

        public bool HasSelection => SelectedRegistration.Length > 0;
        public string SelectedTypeName { get; private set; } = string.Empty;
        public string SelectedRouteLine { get; private set; } = string.Empty;
        public string SelectedStatusLine { get; private set; } = string.Empty;
        public bool SelectedIsPlayer { get; private set; }
        public IReadOnlyList<OperationsPrepCheck> SelectedPrep => _prep;
        public AircraftHudAction PrimaryAction { get; private set; }
        public string PrimaryActionLabel { get; private set; } = string.Empty;
        public bool CanCancel { get; private set; }

        /// <summary>
        /// Rebuild from live state. <paramref name="selectedRegistration"/> may name any
        /// aircraft on the field; an unknown one simply leaves the detail pane empty.
        /// </summary>
        public void Rebuild(AirlineOperations operations, SimulationTime now, OperationsBoardTab tab,
            string selectedRegistration, IReadOnlyList<OperationsEventLine> events)
        {
            _rows.Clear();
            _attention.Clear();
            _prep.Clear();
            _events.Clear();
            _dayDensity.Clear();
            _dayMarks.Clear();
            Tab = tab;
            SelectedRegistration = string.Empty;
            SelectedTypeName = string.Empty;
            SelectedRouteLine = string.Empty;
            SelectedStatusLine = string.Empty;
            SelectedIsPlayer = false;
            PrimaryAction = AircraftHudAction.None;
            PrimaryActionLabel = string.Empty;
            CanCancel = false;
            GroundStopped = false;
            DayProgress01 = 0f;
            DayCaption = string.Empty;
            DayDoneCount = 0;
            DayActiveCount = 0;
            DayUpcomingCount = 0;
            DayOnFieldCount = 0;
            DayListedAheadCount = 0;
            if (operations == null)
                return;

            var clock = operations.Clock ?? AirlineClock.Default;
            GroundStopped = operations.IsGroundStopped;
            Subtitle = $"Adelaide  ·  {RunwayWeather.Label(operations.ActiveRunway)}"
                       + $"/{RunwayWeather.Label(operations.ActiveCrossRunway)}"
                       + $"  ·  {operations.Wind.Text}"
                       + $"  ·  {Weather.Describe(operations.CurrentWeather)}"
                       + (GroundStopped ? "  ·  GROUND STOP" : string.Empty);

            FillDayProgress(operations, now, clock);
            FillBoard(operations, now, tab, clock);
            FillAttention(operations, now, clock);

            if (events != null)
                foreach (var line in events)
                    _events.Add(line);

            var selected = Find(operations, selectedRegistration);
            if (selected != null)
                FillSelection(selected, now, clock);
        }

        private void FillDayProgress(AirlineOperations operations, SimulationTime now, AirlineClock clock)
        {
            var local = clock.LocalAt(now);
            var first = AirlineOperations.AiFirstDepartureHour;
            var last = AirlineOperations.AiLastDepartureHour;
            var startMin = first * 60;
            var endMin = last * 60;
            var span = Math.Max(1, endMin - startMin);
            var nowMin = local.Hour * 60 + local.Minute;
            DayProgress01 = Clamp01((nowMin - startMin) / (float)span);

            for (var hour = first; hour <= last; hour++)
                _dayDensity.Add(AdelaideHourProfile.Density(hour));

            var done = 0;
            var active = 0;
            var upcoming = 0;
            var listedAhead = 0;
            var claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var planned in AdelaideDayPlan.ForLocalDay(operations, now))
            {
                if (planned.Disruption.Cancelled)
                {
                    done++;
                    continue;
                }

                var live = AdelaideDayPlan.CoveringAircraft(planned, operations.Fleet, claimed);
                if (live != null && IsLiveMovement(live))
                {
                    active++;
                    continue;
                }

                var slotLocal = clock.LocalAt(planned.EstimatedAt);
                var slotMin = slotLocal.Hour * 60 + slotLocal.Minute;
                if (slotMin + 2 < nowMin)
                    done++;
                else
                {
                    upcoming++;
                    if (live == null)
                        listedAhead++;
                }
            }

            var onField = 0;
            foreach (var aircraft in operations.Fleet)
            {
                if (!IsOnFieldNow(aircraft))
                    continue;
                onField++;
                var markMin = MarkMinutes(aircraft, clock, nowMin);
                if (markMin >= startMin && markMin <= endMin)
                    _dayMarks.Add(Clamp01((markMin - startMin) / (float)span));
            }

            DayDoneCount = done;
            DayActiveCount = active;
            DayUpcomingCount = upcoming;
            DayOnFieldCount = onField;
            DayListedAheadCount = listedAhead;
            var bank = BankLabel(local.Hour);
            // Caption separates metal on the field from the published timetable overlay so
            // "12 to go" no longer reads as twelve empty gates waiting for pushback.
            DayCaption = $"{clock.TimeText(now)}  ·  {bank}  ·  {onField} on field  ·  {listedAhead} listed ahead";
        }

        private static bool IsOnFieldNow(FleetAircraft aircraft) => aircraft.State is
            FleetState.AtStand or FleetState.TaxiOut or FleetState.HoldingShort or FleetState.TakingOff
            or FleetState.Inbound or FleetState.HoldingForLanding or FleetState.GoAround
            or FleetState.Landing or FleetState.AwaitingStand or FleetState.TaxiIn;

        private static int MarkMinutes(FleetAircraft aircraft, AirlineClock clock, int fallback)
        {
            if (aircraft.Scheduled.HasValue)
            {
                var local = clock.LocalAt(aircraft.Scheduled.Value.DepartAt);
                return local.Hour * 60 + local.Minute;
            }

            if (aircraft.StateEndsAt.HasValue
                && aircraft.State is FleetState.Inbound or FleetState.HoldingForLanding
                    or FleetState.Landing or FleetState.TaxiIn or FleetState.AwaitingStand)
            {
                var local = clock.LocalAt(aircraft.StateEndsAt.Value);
                return local.Hour * 60 + local.Minute;
            }

            return fallback;
        }

        private static bool IsLiveMovement(FleetAircraft aircraft) => aircraft.State is
            FleetState.TaxiOut or FleetState.HoldingShort or FleetState.TakingOff or FleetState.Outbound
            or FleetState.Inbound or FleetState.HoldingForLanding or FleetState.GoAround
            or FleetState.Landing or FleetState.AwaitingStand or FleetState.TaxiIn
            or FleetState.AtDestination;

        private static string BankLabel(int hour) => hour switch
        {
            >= 6 and <= 8 => "morning bank",
            >= 11 and <= 12 => "midday bank",
            >= 16 and <= 18 => "evening bank",
            >= 19 and <= 21 => "evening wind-down",
            >= 9 and <= 15 => "afternoon",
            _ => "overnight"
        };

        private static float Clamp01(float value) =>
            value < 0f ? 0f : value > 1f ? 1f : value;

        private void FillBoard(AirlineOperations operations, SimulationTime now, OperationsBoardTab tab,
            AirlineClock clock)
        {
            _scratch.Clear();
            var arrivals = tab == OperationsBoardTab.Arrivals;
            var nowMin = BoardClockMinutes(clock.TimeText(now));
            foreach (var aircraft in operations.Fleet)
                if (arrivals ? FlightBoard.IsArrival(aircraft) : FlightBoard.IsDeparture(aircraft))
                    _scratch.Add(aircraft);
            FlightBoard.SortForBoard(_scratch, arrivals);

            var claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var aircraft in _scratch)
            {
                var severity = AircraftStatus.Severity(aircraft, now);
                var hasProgress = aircraft.StateEndsAt.HasValue || AircraftStatus.IsWaiting(aircraft);
                var progress = aircraft.StateEndsAt.HasValue
                    ? (float)aircraft.StateProgress(now)
                    : AircraftStatus.WaitProgress(aircraft, now);
                var time = FlightBoard.BoardTime(aircraft, arrivals, clock.TimeText);
                // Live Outbound rows used to stay IsPast=false forever, so a 09:55 departure
                // still airborne at 11:55 pinned the NOW divider (and the one-shot scroll snap)
                // two hours behind the clock. Once they have left the field, treat them like
                // any other past movement.
                var livePast = !arrivals
                    && aircraft.State == FleetState.Outbound
                    && BoardClockMinutes(time) + 2 < nowMin;
                _rows.Add(new OperationsFlightRow(
                    aircraft.Registration,
                    time,
                    FlightBoard.EstimatedTime(aircraft, clock.TimeText),
                    FlightNumber.OrRegistration(aircraft),
                    FlightBoard.RouteText(aircraft),
                    StandColumn(aircraft),
                    FlightBoard.PhaseLabel(aircraft, now),
                    aircraft.Type.Name,
                    aircraft.Airline.Name,
                    aircraft.Airline.LiveryHex,
                    severity,
                    aircraft.Airline.IsPlayer,
                    hasProgress,
                    progress,
                    onField: !livePast,
                    isPast: livePast));
            }

            foreach (var planned in AdelaideDayPlan.ForLocalDay(operations, now))
            {
                if (planned.Arrival != arrivals)
                    continue;
                if (AdelaideDayPlan.CoveringAircraft(planned, _scratch, claimed) != null)
                    continue;
                var plannedStatus = planned.Disruption.Cancelled
                    ? "Cancelled"
                    : planned.Disruption.Delayed
                        ? planned.Disruption.BoardLabel
                        : arrivals ? "Expected" : "Listed";
                var plannedSeverity = planned.Disruption.Cancelled
                    ? StatusSeverity.Warning
                    : planned.Disruption.Delayed ? StatusSeverity.Attention : StatusSeverity.Normal;
                var scheduled = clock.TimeText(planned.ScheduledAt);
                var etaText = clock.TimeText(planned.EstimatedAt);
                // Cancelled slots used to stay IsPast=false forever, which parked the NOW
                // divider on an 08:00 cancellation while the clock read 12:30.
                var slotTime = planned.Disruption.Cancelled ? scheduled : etaText;
                var past = BoardClockMinutes(slotTime) + 2 < nowMin;
                if (past && !planned.Disruption.Cancelled)
                    plannedStatus = arrivals ? "Landed" : "Departed";
                // Day-plan overlays never own a parked aircraft — claiming Gate 13 / Bay 50C
                // here is what made "due to depart" look like empty pavement. Stand stays "—"
                // until a live fleet row covers the slot (ADR 0071).
                _rows.Add(new OperationsFlightRow(
                    planned.Registration.Length > 0 ? planned.Registration : planned.FlightNumber,
                    scheduled,
                    planned.Disruption.Cancelled ? "—"
                        : planned.Disruption.Delayed ? etaText : "—",
                    planned.FlightNumber,
                    planned.RouteText,
                    "—",
                    plannedStatus,
                    planned.Type?.Name ?? string.Empty,
                    planned.AirlineName,
                    planned.LiveryHex,
                    plannedSeverity,
                    isPlayer: false,
                    hasProgress: false,
                    progress01: 0f,
                    onField: false,
                    isPast: past));
            }

            _rows.Sort((a, b) =>
            {
                var byTime = BoardClockMinutes(a.ScheduledTime).CompareTo(BoardClockMinutes(b.ScheduledTime));
                return byTime != 0 ? byTime : string.CompareOrdinal(a.FlightNumber, b.FlightNumber);
            });
        }

        private static int BoardClockMinutes(string time)
        {
            if (string.IsNullOrEmpty(time) || time == "—")
                return int.MaxValue;
            if (time.Length < 5 || time[2] != ':')
                return int.MaxValue;
            if (!int.TryParse(time.Substring(0, 2), out var hours)
                || !int.TryParse(time.Substring(3, 2), out var minutes))
                return int.MaxValue;
            return hours * 60 + minutes;
        }

        /// <summary>
        /// Player exceptions first, worst severity first; when nothing is wrong, the one
        /// commitment coming up next so the band is never an empty promise.
        /// </summary>
        private void FillAttention(AirlineOperations operations, SimulationTime now, AirlineClock clock)
        {
            var player = operations.PlayerAirline;
            if (player == null)
                return;

            foreach (var aircraft in operations.FleetOf(player))
            {
                var severity = AircraftStatus.Severity(aircraft, now);
                if (severity < StatusSeverity.Attention)
                    continue;
                _attention.Add(new OperationsAttentionRow(aircraft.Registration,
                    $"{aircraft.Registration}  ·  {ExceptionText(aircraft, now, clock)}", severity));
            }

            if (_attention.Count > 0)
            {
                _attention.Sort((a, b) => b.Severity.CompareTo(a.Severity));
                return;
            }

            _scratch.Clear();
            foreach (var aircraft in operations.FleetOf(player))
                _scratch.Add(aircraft);
            var priority = OperationsSummary.PriorityAircraft(_scratch, now);
            if (priority == null)
                return;
            _attention.Add(new OperationsAttentionRow(priority.Registration,
                $"{priority.Registration}  ·  {ExceptionText(priority, now, clock)}", StatusSeverity.Normal));
        }

        private static string ExceptionText(FleetAircraft aircraft, SimulationTime now, AirlineClock clock)
        {
            if (aircraft.State == FleetState.AwaitingStand)
                return $"Landed · parking{AircraftStatus.WaitSuffix(aircraft, now)}";

            if (aircraft.State == FleetState.AtStand && aircraft.Scheduled.HasValue)
            {
                var prep = DeparturePrep.For(aircraft, now);
                var late = FlightBoard.DepartureDelayMinutes(aircraft, now);
                var when = clock.TimeText(aircraft.Scheduled.Value.DepartAt);
                if (late > 0)
                    return $"{(prep.Ready ? "Ready" : prep.Label)}  ·  {late} min late for {when}";
                return $"{(prep.Ready ? "Ready" : prep.Label)}  ·  Departs {when}";
            }

            if (aircraft.State == FleetState.AtStand)
                return $"Available on {StandNames.Display(aircraft.Stand)} · no flight planned";

            var suffix = AircraftStatus.WaitSuffix(aircraft, now);
            var status = OperationsSummary.CompactState(aircraft, now);
            var destination = aircraft.CurrentDestination;
            return destination.HasValue
                ? $"{status} · {destination.Value.Name}{suffix}"
                : $"{status}{suffix}";
        }

        private void FillSelection(FleetAircraft aircraft, SimulationTime now, AirlineClock clock)
        {
            SelectedRegistration = aircraft.Registration;
            SelectedTypeName = aircraft.Type.Name;
            SelectedIsPlayer = aircraft.Airline.IsPlayer;
            SelectedStatusLine = FlightBoard.PhaseLabel(aircraft, now);

            if (aircraft.Scheduled.HasValue)
                SelectedRouteLine =
                    $"Adelaide → {aircraft.Scheduled.Value.Destination.Name}"
                    + $"  ·  Departs {clock.TimeText(aircraft.Scheduled.Value.DepartAt)}";
            else if (aircraft.CurrentDestination.HasValue)
                SelectedRouteLine = aircraft.State is FleetState.Inbound or FleetState.HoldingForLanding
                    or FleetState.Landing or FleetState.AwaitingStand or FleetState.TaxiIn
                    ? $"{aircraft.CurrentDestination.Value.Name} → Adelaide"
                    : $"Adelaide → {aircraft.CurrentDestination.Value.Name}";
            else
                SelectedRouteLine = $"{StandNames.Display(aircraft.Stand)}  ·  {aircraft.Airline.Name}";

            if (aircraft.Airline.IsPlayer && aircraft.State == FleetState.AtStand && aircraft.Scheduled.HasValue)
            {
                var prep = DeparturePrep.For(aircraft, now);
                _prep.Add(Check("Fuel", prep.FuelProgress, prep.Stage == DeparturePrepStage.Fuel));
                _prep.Add(Check("Catering", prep.CateringProgress, prep.Stage == DeparturePrepStage.Catering));
                _prep.Add(Check("Boarding", prep.BoardingProgress, prep.Stage == DeparturePrepStage.Boarding));
            }

            PrimaryAction = OperationsSummary.PrimaryAction(aircraft);
            PrimaryActionLabel = OperationsSummary.ActionLabel(PrimaryAction).ToUpperInvariant();
            CanCancel = aircraft.Airline.IsPlayer && aircraft.State == FleetState.AtStand
                        && aircraft.Scheduled.HasValue;
        }


        private static OperationsPrepCheck Check(string name, double progress, bool active)
        {
            var done = progress >= 1;
            var label = done ? name : active ? $"{name} {DeparturePrep.Percent(progress)}%" : name;
            return new OperationsPrepCheck(label, done, active);
        }

        private static string StandColumn(FleetAircraft aircraft)
        {
            var stand = !string.IsNullOrEmpty(aircraft.Stand.Value) ? aircraft.Stand : aircraft.DepartureStand;
            return string.IsNullOrEmpty(stand.Value) ? "—" : StandNames.Short(stand);
        }

        private static FleetAircraft Find(AirlineOperations operations, string registration)
        {
            if (string.IsNullOrEmpty(registration))
                return null;
            foreach (var aircraft in operations.Fleet)
                if (aircraft.Registration == registration)
                    return aircraft;
            return null;
        }
    }

    /// <summary>
    /// Where every part of the Operations workspace is drawn. Pure arithmetic so the
    /// no-overlap and fits-on-screen contract is testable without an editor.
    /// </summary>
    public readonly struct OperationsWorkspaceLayout
    {
        public const float DayStripHeight = 58f;
        public const float AttentionRowHeight = 26f;
        public const float AttentionCaptionHeight = 18f;
        public const float TabHeight = 30f;
        public const float TabWidth = 124f;
        public const float ColumnHeaderHeight = 22f;
        public const float FlightRowHeight = 34f;
        public const float DetailWidth = 248f;
        public const float DetailGap = 16f;
        public const float MinBoardWidth = 360f;

        private OperationsWorkspaceLayout(HudBox surface, HudBox header, HudBox dayStrip, HudBox attention,
            HudBox tabs, HudBox board, HudBox detail, HudBox footer, float[] columns)
        {
            Surface = surface;
            Header = header;
            DayStrip = dayStrip;
            Attention = attention;
            Tabs = tabs;
            Board = board;
            Detail = detail;
            Footer = footer;
            _columns = columns;
        }

        private readonly float[] _columns;

        public HudBox Surface { get; }
        public HudBox Header { get; }

        /// <summary>Operating-day progress under the header: where we are in Adelaide's day.</summary>
        public HudBox DayStrip { get; }

        /// <summary>The pinned player-exception band. Empty when there is nothing to pin.</summary>
        public HudBox Attention { get; }

        public HudBox Tabs { get; }

        /// <summary>The scrolling movement board, excluding the column header.</summary>
        public HudBox Board { get; }

        /// <summary>The selected-flight pane, or empty when the surface is too narrow for it.</summary>
        public HudBox Detail { get; }

        public HudBox Footer { get; }

        public HudBox TitleBox => new(Header.X + HudShell.SurfacePadding, Header.Y + 12f,
            Header.Width - HudShell.SurfacePadding * 2f, 30f);

        public HudBox SubtitleBox => new(Header.X + HudShell.SurfacePadding, Header.Y + 40f,
            Header.Width - HudShell.SurfacePadding * 2f, 18f);

        public HudBox DayCaptionBox => DayStrip.Inset(10f, 6f, 10f, 0f).WithHeight(16f);

        public HudBox DayTrackBox => new(DayStrip.X + 10f, DayStrip.Y + 34f, DayStrip.Width - 20f, 10f);

        public HudBox AttentionCaption => Attention.IsEmpty
            ? HudBox.Empty
            : Attention.Inset(10f, 6f, 10f, 0f).WithHeight(AttentionCaptionHeight);


        public HudBox AttentionRow(int index) => Attention.IsEmpty
            ? HudBox.Empty
            : new HudBox(Attention.X + 8f, Attention.Y + AttentionCaptionHeight + 6f + index * AttentionRowHeight,
                Attention.Width - 16f, AttentionRowHeight);

        public HudBox TabBox(int index) =>
            new(Tabs.X + index * (TabWidth + 8f), Tabs.Y, TabWidth, TabHeight);

        /// <summary>Column header strip, immediately above <see cref="Board"/>.</summary>
        public HudBox ColumnHeader => new(Board.X, Board.Y - ColumnHeaderHeight, Board.Width, ColumnHeaderHeight);

        public HudBox FlightRow(int index) =>
            new(Board.X, Board.Y + index * FlightRowHeight, Board.Width, FlightRowHeight - 2f);

        /// <summary>How many rows fit before the board needs to scroll.</summary>
        public int VisibleRows => Board.Height <= 0f ? 0 : (int)(Board.Height / FlightRowHeight);

        /// <summary>Left edge of board column <paramref name="index"/>, relative to the surface.</summary>
        public float ColumnX(int index) => _columns[index < 0 ? 0 : index >= _columns.Length ? _columns.Length - 1 : index];


        public float ColumnWidth(int index) =>
            index + 1 < _columns.Length ? _columns[index + 1] - _columns[index] - 8f : Board.Right - _columns[index];

        public static readonly string[] ColumnLabels = { "TIME", "FLIGHT", "ROUTE", "STAND", "STATUS" };

        public static OperationsWorkspaceLayout Create(HudBox surface, int attentionRows)
        {
            var header = HudShell.Header(surface);
            var footer = HudShell.Footer(surface);
            var body = HudShell.Body(surface, hasFooter: true);

            var y = body.Y;
            var dayStrip = new HudBox(body.X, y, body.Width, DayStripHeight);
            y = dayStrip.Bottom + 12f;

            var attention = HudBox.Empty;
            if (attentionRows > 0)
            {
                var height = AttentionCaptionHeight + 10f + attentionRows * AttentionRowHeight;
                attention = new HudBox(body.X, y, body.Width, height);
                y = attention.Bottom + 16f;
            }

            var tabs = new HudBox(body.X, y, body.Width, TabHeight);
            y = tabs.Bottom + 10f;

            var detailWidth = body.Width - MinBoardWidth - DetailGap >= DetailWidth ? DetailWidth : 0f;
            var boardWidth = detailWidth > 0f ? body.Width - detailWidth - DetailGap : body.Width;
            var boardTop = y + ColumnHeaderHeight;
            var boardHeight = body.Bottom - boardTop;
            var board = new HudBox(body.X, boardTop, boardWidth, boardHeight < 0f ? 0f : boardHeight);
            var detail = detailWidth > 0f
                ? new HudBox(body.Right - detailWidth, y, detailWidth, body.Bottom - y)
                : HudBox.Empty;

            // Widths are proportional so the board stays readable from a narrow laptop
            // window to a wide desktop one; STATUS absorbs the slack.
            var columns = new float[ColumnLabels.Length];
            var w = board.Width;
            columns[0] = board.X;
            columns[1] = board.X + Fit(w, 0.11f, 56f, 80f);
            columns[2] = columns[1] + Fit(w, 0.16f, 78f, 120f);
            columns[3] = columns[2] + Fit(w, 0.22f, 108f, 168f);
            columns[4] = columns[3] + Fit(w, 0.10f, 52f, 72f);

            return new OperationsWorkspaceLayout(surface, header, dayStrip, attention, tabs, board, detail,
                footer, columns);
        }

        private static float Fit(float total, float fraction, float min, float max)
        {
            var value = total * fraction;
            return value < min ? min : value > max ? max : value;
        }
    }

    /// <summary>
    /// Turns the Operations model and layout into the shared draw list. Pure, so the
    /// runtime HUD and the offline mockup renderer paint byte-for-byte the same surface.
    /// </summary>
    public static class OperationsWorkspacePainter
    {
        /// <summary>Other operators stay on the board but read below your own airline.</summary>
        public const float SubordinateAlpha = 0.62f;

        public static void Paint(HudDrawList into, OperationsWorkspaceModel model,
            OperationsWorkspaceLayout layout, string selectedRegistration, int scrollRow)
        {
            if (into == null || model == null)
                return;

            into.Clear();
            into.Surface(layout.Surface);
            PaintHeader(into, model, layout);
            PaintDayStrip(into, model, layout);
            PaintAttention(into, model, layout);
            PaintTabs(into, model, layout);
            PaintBoard(into, model, layout, selectedRegistration, scrollRow);
            PaintDetail(into, model, layout);
            PaintFooter(into, model, layout);
        }

        private static void PaintHeader(HudDrawList into, OperationsWorkspaceModel model,
            OperationsWorkspaceLayout layout)
        {
            into.Text(layout.TitleBox, model.Title, 26f, HudTone.Default, HudTextStyle.Bold | HudTextStyle.Caption);
            into.Text(layout.SubtitleBox, model.Subtitle, 12f, HudTone.Muted);
            into.Button(CloseBox(layout.Surface), "CLOSE", HudAction.Close, HudButtonStyle.Secondary);
            into.Hairline(HudShell.HeaderRule(layout.Surface));
        }

        private static void PaintDayStrip(HudDrawList into, OperationsWorkspaceModel model,
            OperationsWorkspaceLayout layout)
        {
            var strip = layout.DayStrip;
            into.Fill(strip, HudTone.Default, 0.04f);
            into.Outline(strip, HudTone.Muted, 0.45f);
            into.Caption(layout.DayCaptionBox, "TODAY", HudTone.Muted);
            into.Text(new HudBox(strip.X + 64f, strip.Y + 5f, strip.Width - 74f, 16f),
                model.DayCaption, 12f, HudTone.Default);

            var track = layout.DayTrackBox;
            into.Fill(track, HudTone.Muted, 0.22f);

            // Density underlay so morning / midday / evening banks read as thicker stretches.
            if (model.DayDensity.Count > 0)
            {
                var hourWidth = track.Width / model.DayDensity.Count;
                for (var i = 0; i < model.DayDensity.Count; i++)
                {
                    var density = model.DayDensity[i];
                    if (density < 0.45f)
                        continue;
                    var h = track.Height * (0.35f + 0.65f * density);
                    into.Fill(new HudBox(track.X + i * hourWidth, track.Bottom - h, hourWidth - 1f, h),
                        HudTone.Accent, 0.18f + 0.22f * density);
                }
            }

            // On-field movement ticks — metal that is actually at Adelaide, not the published list.
            foreach (var mark in model.DayMarks)
            {
                var x = track.X + track.Width * mark;
                into.Fill(new HudBox(x - 0.75f, track.Y + 1f, 1.5f, track.Height - 2f), HudTone.Default, 0.55f);
            }

            into.Bar(track, model.DayProgress01, HudTone.Accent);
            var caretX = track.X + track.Width * model.DayProgress01;
            into.Fill(new HudBox(caretX - 1f, track.Y - 3f, 2f, track.Height + 6f), HudTone.Default, 0.95f);
            // Persistent NOW label so the caret is not just a thin line to decode.
            var nowLabel = "NOW";
            var nowBox = new HudBox(caretX - 14f, track.Y - 14f, 28f, 12f);
            if (nowBox.X < track.X)
                nowBox = new HudBox(track.X, nowBox.Y, nowBox.Width, nowBox.Height);
            if (nowBox.Right > track.Right)
                nowBox = new HudBox(track.Right - nowBox.Width, nowBox.Y, nowBox.Width, nowBox.Height);
            into.Caption(nowBox, nowLabel, HudTone.Accent);
        }

        public static HudBox CloseBox(HudBox surface) =>
            new(surface.Right - HudShell.SurfacePadding - 78f, surface.Y + 18f, 78f, 26f);

        private static void PaintAttention(HudDrawList into, OperationsWorkspaceModel model,
            OperationsWorkspaceLayout layout)
        {
            if (layout.Attention.IsEmpty || model.Attention.Count == 0)
                return;

            var worst = model.Attention[0].Severity;
            var tone = worst >= StatusSeverity.Attention ? HudTone.Caution : HudTone.Muted;
            into.Fill(layout.Attention, tone, 0.07f);
            into.Outline(layout.Attention, tone, 0.75f);
            into.Caption(layout.AttentionCaption.Inset(10f, 0f, 10f, 0f),
                worst >= StatusSeverity.Attention ? "NEEDS ATTENTION" : "COMING UP", tone);

            for (var i = 0; i < model.Attention.Count; i++)
            {
                var row = model.Attention[i];
                var box = layout.AttentionRow(i);
                into.Fill(box, HudTone.Default, 0.05f);
                into.Fill(new HudBox(box.X + 6f, box.Y + 6f, 3f, box.Height - 12f), row.Tone, 1f);
                into.Text(new HudBox(box.X + 16f, box.Y + 4f, box.Width - 24f, 18f), row.Text, 13f, row.Tone,
                    HudTextStyle.Bold);
                into.Hotspot(box, HudAction.Select(row.Registration));
            }
        }

        private static void PaintTabs(HudDrawList into, OperationsWorkspaceModel model,
            OperationsWorkspaceLayout layout)
        {
            into.Button(layout.TabBox(0), "DEPARTURES", HudAction.TabDepartures,
                model.Tab == OperationsBoardTab.Departures ? HudButtonStyle.Primary : HudButtonStyle.Secondary);
            into.Button(layout.TabBox(1), "ARRIVALS", HudAction.TabArrivals,
                model.Tab == OperationsBoardTab.Arrivals ? HudButtonStyle.Primary : HudButtonStyle.Secondary);
        }

        private static void PaintBoard(HudDrawList into, OperationsWorkspaceModel model,
            OperationsWorkspaceLayout layout, string selectedRegistration, int scrollRow)
        {
            var header = layout.ColumnHeader;
            for (var c = 0; c < OperationsWorkspaceLayout.ColumnLabels.Length; c++)
                into.Caption(new HudBox(layout.ColumnX(c), header.Y + 4f, layout.ColumnWidth(c), 16f),
                    OperationsWorkspaceLayout.ColumnLabels[c]);
            into.Hairline(new HudBox(layout.Board.X, header.Bottom - 1f, layout.Board.Width, 1f));

            if (model.Rows.Count == 0)
            {
                into.Text(new HudBox(layout.Board.X, layout.Board.Y + 14f, layout.Board.Width, 20f),
                    model.Tab == OperationsBoardTab.Arrivals
                        ? "Nothing inbound to Adelaide right now."
                        : "Nothing outbound from Adelaide right now.", 13f, HudTone.Muted);
                return;
            }

            var first = scrollRow < 0 ? 0 : scrollRow;
            var last = first + layout.VisibleRows;
            if (last > model.Rows.Count)
                last = model.Rows.Count;

            for (var i = first; i < last; i++)
            {
                var row = model.Rows[i];
                var box = layout.FlightRow(i - first);
                var selected = row.Registration == selectedRegistration;
                var alpha = row.IsPlayer ? 1f : SubordinateAlpha;
                if (row.IsPast)
                    alpha *= 0.55f;
                else if (!row.OnField)
                    alpha *= 0.78f;

                // Persistent NOW divider: first upcoming row after the muted past block.
                if (i == model.NowDividerRowIndex && i > 0 && !row.IsPast)
                {
                    into.Fill(new HudBox(box.X + 4f, box.Y - 1f, box.Width - 8f, 2f), HudTone.Accent, 0.85f);
                    into.Caption(new HudBox(box.X + 8f, box.Y - 12f, 40f, 11f), "NOW", HudTone.Accent);
                }

                if (selected)
                    into.Fill(box, HudTone.Accent, 0.26f);
                else if (((i - first) & 1) == 1)
                    into.Fill(box, HudTone.Default, 0.025f);
                if (row.IsPlayer)
                    into.Fill(new HudBox(box.X, box.Y, 3f, box.Height), HudTone.Default, 1f, row.LiveryHex);

                var textY = box.Y + 5f;
                into.Text(Cell(layout, 0, textY), row.ScheduledTime, 13f,
                    row.IsPast ? HudTone.Muted : HudTone.Default,
                    HudTextStyle.Bold, alpha: alpha);
                if (row.ShowsEstimate)
                    into.Text(Cell(layout, 0, box.Y + 20f), "est " + row.EstimatedTime, 10f, HudTone.Muted,
                        alpha: alpha);

                into.Text(Cell(layout, 1, textY), row.FlightNumber, 13f, HudTone.Default, alpha: alpha);
                var flightSub = row.OperatorName.Length > 0 ? row.OperatorName : row.Registration;
                if (flightSub.Length > 0 && flightSub != row.FlightNumber)
                    into.Text(Cell(layout, 1, box.Y + 18f), flightSub, 10f, HudTone.Muted, alpha: alpha);

                into.Text(Cell(layout, 2, textY), row.Route, 13f, HudTone.Default, alpha: alpha);
                into.Text(Cell(layout, 3, textY), row.Stand, 13f,
                    row.OnField ? HudTone.Default : HudTone.Muted, alpha: alpha);
                into.Text(Cell(layout, 4, textY), row.Status, 13f, row.StatusTone,
                    row.Severity == StatusSeverity.Normal ? HudTextStyle.Regular : HudTextStyle.Bold,
                    alpha: alpha);

                if (row.HasProgress)
                    into.Bar(new HudBox(layout.ColumnX(0), box.Bottom - 3f, layout.Board.Right - layout.ColumnX(0), 2f),
                        row.Progress01, row.IsPlayer ? HudTone.Accent : HudTone.Muted);

                into.Hotspot(box, HudAction.Select(row.Registration));
            }

            if (last < model.Rows.Count)
                into.Text(new HudBox(layout.Board.X, layout.Board.Bottom - 16f, layout.Board.Width, 16f),
                    $"{model.Rows.Count - last} more below", 11f, HudTone.Muted, HudTextStyle.Caption);
        }

        private static HudBox Cell(OperationsWorkspaceLayout layout, int column, float y) =>
            new(layout.ColumnX(column) + (column == 0 ? 10f : 0f), y,
                layout.ColumnWidth(column) - (column == 0 ? 10f : 0f), 18f);

        private static void PaintDetail(HudDrawList into, OperationsWorkspaceModel model,
            OperationsWorkspaceLayout layout)
        {
            var pane = layout.Detail;
            if (pane.IsEmpty)
                return;

            into.Hairline(new HudBox(pane.X - OperationsWorkspaceLayout.DetailGap * 0.5f, pane.Y, 1f, pane.Height));

            if (!model.HasSelection)
            {
                into.Text(new HudBox(pane.X, pane.Y + 8f, pane.Width, 44f),
                    "Select a flight to see what you can do with it.", 13f, HudTone.Muted,
                    HudTextStyle.Wrap);
                return;
            }

            into.Fill(new HudBox(pane.X, pane.Y + 4f, 3f, 28f), HudTone.Accent, 1f);
            into.Text(new HudBox(pane.X + 12f, pane.Y + 4f, pane.Width - 12f, 26f), model.SelectedRegistration,
                20f, HudTone.Default, HudTextStyle.Bold);
            var typeLine = string.IsNullOrEmpty(model.SelectedTypeName)
                ? model.SelectedRouteLine
                : $"{model.SelectedTypeName}  ·  {model.SelectedRouteLine}";
            into.Text(new HudBox(pane.X + 12f, pane.Y + 30f, pane.Width - 12f, 18f), typeLine,
                12f, HudTone.Muted);

            var y = pane.Y + 60f;
            if (model.SelectedPrep.Count > 0)
            {
                foreach (var check in model.SelectedPrep)
                {
                    into.Text(new HudBox(pane.X, y, 18f, 18f), check.Done ? "✓" : check.Active ? "●" : "○",
                        13f, check.Tone, HudTextStyle.Bold);
                    into.Text(new HudBox(pane.X + 20f, y, pane.Width - 20f, 18f), check.Label, 13f, check.Tone,
                        check.Active ? HudTextStyle.Bold : HudTextStyle.Regular);
                    y += 21f;
                }

                y += 8f;
            }
            else
            {
                into.Text(new HudBox(pane.X, y, pane.Width, 18f), model.SelectedStatusLine, 13f, HudTone.Default);
                y += 28f;
            }

            if (!model.SelectedIsPlayer)
            {
                into.Text(new HudBox(pane.X, y, pane.Width, 36f),
                    "Another operator's flight — you can watch it, but not command it.", 12f, HudTone.Muted,
                    HudTextStyle.Wrap);
                return;
            }

            // Under the content it belongs to, not pinned to the bottom of a tall pane —
            // an action marooned half a screen below its flight reads as unrelated to it.
            var stack = model.CanCancel ? 76f : 40f;
            var buttonY = y + 8f;
            if (buttonY + stack > pane.Bottom)
                buttonY = pane.Bottom - stack;
            if (model.PrimaryAction != AircraftHudAction.None)
                into.Button(new HudBox(pane.X, buttonY, pane.Width, 34f), model.PrimaryActionLabel,
                    HudAction.Primary, HudButtonStyle.Primary);
            if (model.CanCancel)
                into.Button(new HudBox(pane.X, buttonY + 42f, pane.Width, 30f), "CANCEL", HudAction.Cancel,
                    HudButtonStyle.Destructive);
        }

        private static void PaintFooter(HudDrawList into, OperationsWorkspaceModel model,
            OperationsWorkspaceLayout layout)
        {
            into.Hairline(HudShell.FooterRule(layout.Surface));
            var footer = layout.Footer.Inset(HudShell.SurfacePadding, 6f, HudShell.SurfacePadding, 5f);
            into.Caption(footer.WithHeight(13f), "EVENT HISTORY");
            var lineBox = new HudBox(footer.X, footer.Y + 14f, footer.Width, 15f);
            if (model.Events.Count == 0)
            {
                into.Text(lineBox, "Nothing yet today.", 11f, HudTone.Muted);
                return;
            }

            var line = model.Events[0];
            into.Text(lineBox.WithWidth(52f), line.Time, 11f, HudTone.Muted);
            into.Text(lineBox.Offset(58f, 0f).WithWidth(footer.Width - 58f), line.Text, 11f);
        }
    }
}
