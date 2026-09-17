using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Airside.Domain;
using Airside.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Airside.Presentation
{
    /// <summary>
    /// The player-airline layer (ADR 0045): start-your-airline panel, fleet panel,
    /// Australia-wide destinations map, stand choice and skip-to-next-event.
    /// IMGUI, drawn over the circuit HUD. Once the airline starts, the 3D field draws
    /// the fleets instead of the demo circuit (AirsidePrototype.FleetVisuals.cs).
    /// </summary>
    public sealed partial class AirsidePrototype
    {

        private static readonly (string label, string hex)[] LiveryChoices =
        {
            ("Crimson", "#C8102E"), ("Navy", "#1F3A93"), ("Forest", "#2E7D32"),
            ("Sunset", "#E8772E"), ("Violet", "#6A3FA0"), ("Gold", "#D4A017")
        };

        private AirlineOperations _operations;
        private string _airlineNameDraft = "Southern Cross Regional";
        private int _liveryChoice;
        /// <summary>The single player workspace open at a time (ADR 0053).</summary>
        private HudWorkspace _activeWorkspace;
        private bool _devToolsOpen;
        private bool _controlsHelpOpen;
        private readonly AustraliaMapLens _mapLens = new();

        // Destinations map pointer: a press only becomes a pan once it moves past the drag
        // threshold, so a click still picks the destination or aircraft under it.
        private bool _mapPressed;
        private bool _mapPanning;
        private Vector2 _mapPressGui;
        private Vector2 _mapPanGui;
        private readonly List<(float x, float y)> _mapDestinationPoints = new();
        private readonly List<PlannerDestination> _mapDestinationRows = new();
        private readonly List<(float x, float y)> _mapAircraftPoints = new();
        private readonly List<FleetAircraft> _mapAircraftRows = new();
        private readonly List<FleetAircraft> _playerFleetRows = new();
        private Vector2 _plannerScroll;
        private float _plannerContentHeight = 600f;
        private Vector2 _hangarScroll;
        private Vector2 _fleetScroll;
        private Vector2 _flightsScroll;
        private bool _flightsShowArrivals = true;
        private Vector2 _devToolsScroll;
        private readonly List<FleetAircraft> _flightsBoardRows = new();
        // IMGUI calls OnGUI several times per real frame (Layout, Repaint, every mouse-move);
        // without this the whole fleet was rescanned and re-sorted on every one of those
        // passes while the Flights tab was open, not just once per frame.
        private int _flightsBoardRowsFrame = -1;
        private bool _flightsBoardRowsForArrivals;
        private readonly SeededRandomSource _devToolsRandom = new(4242);
        /// <summary>The player aircraft the flight planner is planning.</summary>
        private FleetAircraft _mapAircraft;
        private string _selectedAircraftId;
        private Destination? _mapSelection;
        private long _departureDelaySeconds = 15 * 60;
        private string _mapMessage;
        private long _seenEvents;
        private long _seenSettlements;
        private readonly ToastQueue _toasts = new();
        private readonly List<ToastEntry> _visibleToasts = new();

        private bool AirlineSetupOpen => _operations == null;
        private bool AirlineModalOpen => AirlineSetupOpen || _awaySummary != null;

        // IMGUI runs several times a frame (layout, repaint, input). Styles built inline were
        // allocated on every pass — dozens a frame, one per fleet row — and fed the garbage
        // collector for nothing, so they are made once and reused.
        private GUIStyle _hudLabel;
        private GUIStyle _hudSmall;
        private GUIStyle _hudSmallButton;
        private readonly Dictionary<(GUIStyle basis, string variant), GUIStyle> _styleCache = new();

        /// <summary>A style derived from <paramref name="basis"/>, built on first use and cached.</summary>
        private GUIStyle Styled(GUIStyle basis, string variant, Func<GUIStyle, GUIStyle> make)
        {
            var key = (basis, variant);
            if (!_styleCache.TryGetValue(key, out var style))
                _styleCache[key] = style = make(basis);
            return style;
        }

        // ---- Frame hooks called from AirsidePrototype ----------------------------

        private void UpdateAirlineOperations()
        {
            if (_operations == null)
                return;

            _operations.Update();
            RefreshFleetFlights();
            AnnounceNewEvents();
            AnnounceNewSettlements();
            AutosaveIfDue();
        }

        /// <summary>True when the airline layer owns the keyboard this frame.</summary>
        private bool ReadAirlineControls(Keyboard keyboard)
        {
            // The start and away-summary panels own the keyboard until dismissed.
            if (AirlineModalOpen)
                return true;

            // Help owns the keyboard while open (except F1 here and Esc in Prototype). The
            // other hotkeys used to run first, opening the hangar or planner behind it.
            if (_controlsHelpOpen)
            {
                if (keyboard.f1Key.wasPressedThisFrame)
                    ToggleControlsHelp();
                return true;
            }

            if (keyboard.tabKey.wasPressedThisFrame)
                TogglePlanner();
            if (keyboard.leftBracketKey.wasPressedThisFrame)
                CycleSelection(-1);
            if (keyboard.rightBracketKey.wasPressedThisFrame)
                CycleSelection(1);
            if (keyboard.hKey.wasPressedThisFrame)
                SetWorkspace(HudWorkspace.Fleet);
            if (keyboard.tKey.wasPressedThisFrame)
                SetWorkspace(HudWorkspace.Operations);
            if (keyboard.f8Key.wasPressedThisFrame)
                ToggleDevTools();
            if (keyboard.f1Key.wasPressedThisFrame)
                ToggleControlsHelp();
            if (keyboard.lKey.wasPressedThisFrame)
                ToggleFieldTags();
            if (keyboard.nKey.wasPressedThisFrame)
                ToggleMiniMap();
            // F1 above may just have opened help; it owns the keyboard from this frame.
            return _controlsHelpOpen;
        }

        private void DrawAirlineHud(HudLayout layout, GUIStyle panel, GUIStyle title, GUIStyle button)
        {
            // A focused text field or a modal airline panel owns the keyboard.
            if (_cameraController != null)
                // The Esc menu owns the keyboard too: the camera reads WASD/QE/ZX itself, so without
                // this the view kept panning and orbiting behind the open menu.
                _cameraController.KeyboardCaptured =
                    _menuOpen || AirlineModalOpen || _controlsHelpOpen
                    || GUIUtility.keyboardControl != 0;
            // The pause menu is itself modal. Drawing the airline setup, away summary or
            // workspace panels behind it produced overlapping labels and live buttons.
            if (_menuOpen)
            {
                // Keep the full-screen pointer capture that prevents camera drags and
                // scrolls behind the menu even though the underlying HUD is not drawn.
                RememberHudPanels(layout, AirlineHudLayout.Create(layout, false), false);
                return;
            }
            _guideStep = FirstFlightGuide.For(_operations, out _guideAircraft);
            if (_lastGuideStep == GuideStep.TaxiingIn && _guideStep == GuideStep.Complete)
                ShowToast("First trip complete. Keep your aircraft flying — plan the next one any time.");
            _lastGuideStep = _guideStep;
            var showGuide = !AirlineModalOpen && _guideStep != GuideStep.Complete;
            var placement = AirlineHudLayout.Create(layout, showGuide);
            RememberHudPanels(layout, placement, showGuide);

            var label = _hudLabel ??= AirsideTheme.TextStyle(new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true });
            var small = _hudSmall ??= AirsideTheme.TextStyle(new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true }, AirsideTheme.OpenSky);
            var smallButton = _hudSmallButton ??= AirsideTheme.ButtonStyle(new GUIStyle(GUI.skin.button) { fontSize = 13, fontStyle = FontStyle.Bold }, AirsideTheme.Cloud);

            if (AirlineSetupOpen)
            {
                DrawAirlineSetup(placement, panel, title, label, button);
                return;
            }

            if (_awaySummary != null)
            {
                DrawAwaySummary(placement, panel, title, label, button);
                return;
            }

            // Under every panel, so a tag never sits on top of a button.
            DrawFieldTags(small);
            DrawClockPanel(placement.Clock, panel, label, small, smallButton);
            if (showGuide)
                DrawGuide(placement.Guide, panel, label, small);
            else
                DrawStatusLine(placement.Guide, panel, small);
            DrawWorkspaceNav(placement.NavStrip, smallButton);
            if (!((_activeWorkspace != HudWorkspace.None || _devToolsOpen) && placement.MapCoversFleet))
                DrawFleetPanel(placement.FleetArea, panel, label, small, smallButton);
            if (_devToolsOpen)
                DrawDevToolsPanel(placement.Map, panel, title, label, small, smallButton);
            else switch (_activeWorkspace)
            {
                case HudWorkspace.Operations:
                    DrawFlightsPanel(placement.Map, panel, title, label, small, smallButton);
                    break;
                case HudWorkspace.Fleet:
                    DrawHangarPanel(placement.Map, panel, title, label, small, smallButton);
                    break;
                case HudWorkspace.Map:
                    DrawDestinationsMap(placement.Map, panel, title, label, small, smallButton);
                    break;
                case HudWorkspace.Contracts:
                    DrawContractsPanel(placement.Map, panel, title, label, small, smallButton);
                    break;
            }
            DrawMiniMap(FieldMiniMap.PanelFor(layout, placement), panel, small);
            DrawSelectionHudCard(layout, panel, label, small);
            if (_controlsHelpOpen)
                DrawControlsHelp(layout, panel, title, label, small, smallButton);
            DrawToast(placement.Toast, label);
        }

        // ---- Start your airline ---------------------------------------------------

        private void DrawAirlineSetup(AirlineHudLayout placement, GUIStyle panel, GUIStyle title, GUIStyle label, GUIStyle button)
        {
            ProbeSavedAirline();
            var hasSave = _savedAirline != null;
            var saveBlock = hasSave || !string.IsNullOrEmpty(_saveError) ? 96f : 0f;
            var panelHeight = 300f + saveBlock;
            var rect = placement.SetupPanel(panelHeight);
            GUI.Box(rect, GUIContent.none, panel);
            var x = rect.x + 20f;
            var inner = rect.width - 40f;

            if (saveBlock > 0f)
            {
                var small = Styled(label, "setup-small", s => AirsideTheme.TextStyle(new GUIStyle(s) { fontSize = 12 }, AirsideTheme.OpenSky));
                if (hasSave)
                {
                    if (GUI.Button(new Rect(x, rect.y + 16f, inner, 40f), $"Continue {SavedAirlineName()}", button))
                        ContinueAirline();
                    GUI.Label(new Rect(x, rect.y + 58f, inner, 20f), SavedAirlineSummary(), small);
                }
                else
                {
                    GUI.Label(new Rect(x, rect.y + 16f, inner, 60f), _saveError, small);
                }

                rect.y += saveBlock;
                rect.height -= saveBlock;
            }

            GUI.Label(new Rect(x, rect.y + 16f, inner, 30f), hasSave ? "Or start a new airline" : "Start your airline at Adelaide", title);
            GUI.Label(new Rect(x, rect.y + 56f, inner, 20f), "Airline name", label);
            _airlineNameDraft = GUI.TextField(new Rect(x, rect.y + 80f, inner, 30f), _airlineNameDraft ?? string.Empty, 32);

            GUI.Label(new Rect(x, rect.y + 122f, inner, 20f), "Livery colour", label);
            var swatch = (inner - 5 * 8f) / LiveryChoices.Length;
            for (var i = 0; i < LiveryChoices.Length; i++)
            {
                var cell = new Rect(x + i * (swatch + 8f), rect.y + 146f, swatch, 36f);
                DrawSolid(cell, AirsideTheme.FromHex(LiveryChoices[i].hex));
                if (i == _liveryChoice)
                    AirsideTheme.DrawPanelFrame(cell, AirsideTheme.SafetyYellow);
                if (GUI.Button(cell, GUIContent.none, GUIStyle.none))
                    _liveryChoice = i;
            }

            GUI.Label(new Rect(x, rect.y + 190f, inner, 36f),
                hasSave
                    ? "A new airline replaces your saved one."
                    : "You start with one ATR 42-600, sharing the regional apron with Rex and QantasLink.", label);

            var name = (_airlineNameDraft ?? string.Empty).Trim();
            GUI.enabled = name.Length > 0;
            if (GUI.Button(new Rect(x, rect.y + rect.height - 58f, inner, 40f), "Start airline", button))
                StartAirline(name);
            GUI.enabled = true;
        }

        private void StartAirline(string name)
        {
            var player = Airline.Player(name, LiveryChoices[_liveryChoice].hex);
            // Live time: whatever the demo circuit's clock reads now is this real instant.
            _operations = AirlineOperations.StartAtAdelaide(_clock, new SeededRandomSource(20260913), player,
                AirlineClock.Aligned(_clock.Now, DateTime.UtcNow));
            _seenEvents = _operations.TotalEvents;
            _seenSettlements = _operations.TotalSettlements;
            RefreshFleetFlights();
            ShowToast($"{name} is open for business. Plan a flight for {FirstPlayerAircraft()?.Registration}.");
            SaveAirline();
            PlayUiClick();
        }

        // ---- Pointer over HUD ------------------------------------------------------------------

        private readonly List<Rect> _hudPanels = new();
        // Small click targets drawn over the field (tags, the toast). Kept apart from
        // _hudPanels because field tags hide themselves behind panels, not behind each other.
        private readonly List<Rect> _hudOverlays = new();
        private float _hudScale = 1f;

        /// <summary>Record where HUD panels are this frame, in virtual GUI points.</summary>
        private void RememberHudPanels(HudLayout layout, AirlineHudLayout placement, bool showGuide)
        {
            _hudScale = HudLayout.ScaleFor(Screen.width, Screen.height);
            _hudPanels.Clear();
            _hudOverlays.Clear();
            _hudPanels.Add(layout.ControlBar);
            _hudPanels.Add(layout.SpeedReadout);
            // The menu is modal: the whole screen is HUD while it is up, so dragging or scrolling
            // beside it no longer orbits, pans or zooms the camera behind it.
            if (_menuOpen)
                _hudPanels.Add(new Rect(0f, 0f, layout.Viewport.x, layout.Viewport.y));
            if (AirlineModalOpen)
            {
                // Modal panels: the whole screen belongs to the HUD until dismissed.
                _hudPanels.Add(new Rect(0f, 0f, layout.Viewport.x, layout.Viewport.y));
                return;
            }

            _hudPanels.Add(placement.Clock);
            // Always meaningful now: the tutorial card while it runs, the status line after.
            _hudPanels.Add(placement.Guide);
            _hudPanels.Add(placement.NavStrip);
            if (!((_activeWorkspace != HudWorkspace.None || _devToolsOpen) && placement.MapCoversFleet))
                _hudPanels.Add(placement.FleetArea);
            if (_activeWorkspace != HudWorkspace.None || _devToolsOpen)
                _hudPanels.Add(placement.Map);
            if (MiniMapShows)
            {
                var miniMap = FieldMiniMap.PanelFor(layout, placement);
                if (miniMap.width > 0f)
                    _hudPanels.Add(miniMap);
            }
            if (TrySelectionHudCardRect(layout, out var selectionCard))
                _hudPanels.Add(selectionCard);
            if (_controlsHelpOpen)
                _hudPanels.Add(new Rect(0f, 0f, layout.Viewport.x, layout.Viewport.y));
        }

        private bool IsPointerOverHud(Vector2 inputSystemPosition)
        {
            return HudHitTest.IsOverHud(inputSystemPosition, Screen.height, _hudScale, _hudPanels, _hudOverlays);
        }

        // ---- First-flight guide ------------------------------------------------------------

        private GuideStep _guideStep = GuideStep.Complete;
        private GuideStep _lastGuideStep = GuideStep.Complete;
        private FleetAircraft _guideAircraft;

        private bool IsGuided(FleetAircraft aircraft, GuideStep step) =>
            _guideStep == step && ReferenceEquals(aircraft, _guideAircraft);

        private void DrawGuide(Rect rect, GUIStyle panel, GUIStyle label, GUIStyle small)
        {
            var (heading, hint) = GuideText(_guideStep, _guideAircraft);
            GUI.Box(rect, GUIContent.none, panel);
            AirsideTheme.DrawPanelFrame(rect, AirsideTheme.SafetyYellow);
            var bold = Styled(label, "bold", s => new GUIStyle(s) { fontStyle = FontStyle.Bold });
            GUI.Label(new Rect(rect.x + 14f, rect.y + 10f, rect.width - 28f, 22f), heading, bold);
            GUI.Label(new Rect(rect.x + 14f, rect.y + 34f, rect.width - 28f, rect.height - 40f), hint, small);
        }

        /// <summary>
        /// The persistent one-line objective (ADR 0053) that replaces the guide card once it's
        /// done: the player fleet's most urgent aircraft, or a quiet fleet-wide line.
        /// </summary>
        private void DrawStatusLine(Rect rect, GUIStyle panel, GUIStyle small)
        {
            if (rect.height < 4f)
                return;
            // The guide card it replaces has a panel behind it; bare floating text here read
            // as unfinished next to it, so this gets the same quiet chrome, not a border colour.
            GUI.Box(rect, GUIContent.none, panel);
            var (text, severity) = OperationsSummary.Line(PlayerFleet(), _clock.Now, _operations.CareerState);
            var style = Styled(small, "status-line", s => AirsideTheme.TextStyle(
                new GUIStyle(s) { fontStyle = FontStyle.Bold, wordWrap = false }, AirsideTheme.Cloud));
            var previousContent = GUI.contentColor;
            if (severity != StatusSeverity.Normal)
                GUI.contentColor = SeverityColour(severity, previousContent);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 4f, rect.width - 16f, rect.height - 4f), text, style);
            GUI.contentColor = previousContent;
        }

        /// <summary>A gentle yellow pulse around the control the guide is pointing at.</summary>
        private static void DrawGuideHighlight(Rect rect)
        {
            var pulse = 0.45f + 0.55f * Mathf.PingPong(Time.unscaledTime * 1.6f, 1f);
            var colour = AirsideTheme.SafetyYellow;
            colour.a = pulse;
            AirsideTheme.DrawPanelFrame(new Rect(rect.x - 3f, rect.y - 3f, rect.width + 6f, rect.height + 6f), colour);
        }

        private (string heading, string hint) GuideText(GuideStep step, FleetAircraft aircraft)
        {
            var reg = aircraft?.Registration ?? "Your aircraft";
            var dest = aircraft?.CurrentDestination?.Name ?? aircraft?.Scheduled?.Destination.Name ?? "its destination";
            return step switch
            {
                GuideStep.PlanFirstFlight => ("1 · Plan your first flight",
                    $"Click Plan flight for {reg}, pick a green destination and when it leaves. Kingscote is a short hop."),
                GuideStep.WaitForDeparture => ("2 · Flight planned",
                    $"{reg} leaves at {ClockText(aircraft.Scheduled.Value.DepartAt)} Adelaide time. The airport runs in real time — click the aircraft on the field, or press Follow (F)."),
                GuideStep.Departing => ("3 · Departing",
                    $"{reg} is heading out. Click it on the field (or press Follow) to ride along through the taxi and takeoff."),
                GuideStep.Away => ("4 · Away to " + dest,
                    "Flights take real time. Track it on the route map (Tab), or close the game — the airport keeps running and tells you what happened."),
                GuideStep.Landing => ("5 · Coming home",
                    $"The tower is bringing {reg} in to land. Click the aircraft on final (or Follow) to watch the touchdown."),
                GuideStep.ChooseStand => ("6 · Choose a stand",
                    $"{reg} has landed. Pick a free bay in Your Fleet so it can taxi in."),
                GuideStep.TaxiingIn => ("7 · Taxiing in",
                    $"{reg} is taxiing to {StandNames.Display(aircraft.Stand)}. That completes your first trip."),
                _ => (string.Empty, string.Empty)
            };
        }

        // ---- Away summary -----------------------------------------------------------------

        private void DrawAwaySummary(AirlineHudLayout placement, GUIStyle panel, GUIStyle title, GUIStyle label, GUIStyle button)
        {
            var summary = _awaySummary;
            // Measure each sentence at the panel's text width: a fixed 40 px row clipped the
            // third line of a long status ("…departing 14:05 for Mount Gambier").
            var inner = placement.SetupPanel(100f).width - 40f;
            var linesHeight = 0f;
            foreach (var line in summary.Lines)
                linesHeight += label.CalcHeight(new GUIContent(line), inner) + 8f;
            var rect = placement.SetupPanel(70f + linesHeight + 80f);
            GUI.Box(rect, GUIContent.none, panel);
            var x = rect.x + 20f;

            GUI.Label(new Rect(x, rect.y + 16f, inner, 30f), summary.Title, title);
            var y = rect.y + 58f;
            foreach (var line in summary.Lines)
            {
                var height = label.CalcHeight(new GUIContent(line), inner);
                GUI.Label(new Rect(x, y, inner, height), line, label);
                y += height + 8f;
            }

            if (GUI.Button(new Rect(x, rect.yMax - 58f, inner, 40f), "Back to the airport", button))
            {
                _awaySummary = null;
                PlayUiClick();
            }
        }

        // ---- Saving ---------------------------------------------------------------------

        private const float AutosaveIntervalSeconds = 20f;

        private AwaySummary _awaySummary;
        private bool _saveProbed;
        private AirlineSaveData _savedAirline;
        private string _saveError;
        private float _nextAutosaveAt;
        private bool _saveFailureShown;

        private void ProbeSavedAirline()
        {
            if (_saveProbed)
                return;
            _saveProbed = true;

            if (AirlineSaveFile.TryRead(SavePath, out var data, out var error))
                _savedAirline = data;
            else if (File.Exists(SavePath))
                _saveError = $"{error} Starting a new airline will replace it.";
        }

        private string SavedAirlineName()
        {
            foreach (var airline in _savedAirline.Airlines)
                if (airline.IsPlayer)
                    return airline.Name;
            return "your airline";
        }

        private string SavedAirlineSummary()
        {
            var at = new SimulationTime(_savedAirline.ClockSeconds);
            var savedClock = AirlineSave.ClockFor(_savedAirline);
            var trips = 0;
            foreach (var aircraft in _savedAirline.Fleet)
                foreach (var airline in _savedAirline.Airlines)
                    if (airline.IsPlayer && airline.Id == aircraft.AirlineId)
                        trips += aircraft.CompletedTrips;
            return $"Saved {savedClock.DateText(at)} {savedClock.TimeText(at)}  ·  {trips} trip{(trips == 1 ? "" : "s")} flown";
        }

        /// <summary>
        /// Resume the saved airline. The circuit simulation and clock are rebuilt at the
        /// saved time rather than stepped there second by second from zero.
        /// </summary>
        private void ContinueAirline()
        {
            var data = _savedAirline;
            var clock = new ManualSimulationClock(new SimulationTime(data.ClockSeconds));
            AirlineOperations restored;
            try
            {
                restored = AirlineSave.Restore(data, clock);
            }
            catch (Exception e) when (e is FormatException or ArgumentException or InvalidOperationException)
            {
                _savedAirline = null;
                _saveError = $"Your saved airline could not be loaded ({e.Message}). Starting a new airline will replace it.";
                return;
            }

            // The airport kept running while the game was closed: bring it to the real
            // time now through the same event-driven update live play uses, then report
            // what happened if the gap was worth a summary.
            var target = AwayCatchUp.LiveTarget(restored, DateTime.UtcNow);
            var away = target.ElapsedSeconds - clock.Now.ElapsedSeconds;
            clock.Set(target);
            restored.Update();
            if (away >= AwayCatchUp.MinimumSeconds)
                _awaySummary = AwaySummary.Build(data, restored, away);

            // Saves from before the extra regional carriers or the terminal jet gain them now,
            // parked and booked — once only, never a duplicate.
            var joined = restored.AddMissingRegionalCarriers();
            var jetJoined = restored.AddMissingTerminalOperators();

            _clock = clock;
            _simulation = new AirportSimulation(_clock, new SeededRandomSource(24031996), new ReservationTable());
            _preciseTime = _clock.Now.ElapsedSeconds;
            _operations = restored;
            _seenEvents = _operations.TotalEvents;
            _seenSettlements = _operations.TotalSettlements;
            // An old enough save gains both; the else-if used to swallow the jet's news.
            if (joined > 0)
                ShowToast("Rex and QantasLink now fly from Adelaide's regional apron too.");
            if (jetJoined > 0)
                ShowToast("Virgin Australia's 737-8 now operates from Gate 13.");
            RefreshFleetFlights();
            if (_awaySummary == null)
                ShowToast($"Welcome back to {_operations.PlayerAirline.Name}.");
            SaveAirline();
            PlayUiClick();
        }

        private void RequestAutosave() => _nextAutosaveAt = Mathf.Min(_nextAutosaveAt, Time.unscaledTime + 2f);

        private void AutosaveIfDue()
        {
            if (Time.unscaledTime < _nextAutosaveAt)
                return;
            SaveAirline();
        }

        private void SaveAirline()
        {
            if (_operations == null)
                return;

            _nextAutosaveAt = Time.unscaledTime + AutosaveIntervalSeconds;
            try
            {
                AirlineSaveFile.Write(SavePath, AirlineSave.Capture(_operations, DateTime.UtcNow));
                _saveFailureShown = false;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                if (_saveFailureShown)
                    return;
                _saveFailureShown = true;
                ShowToast($"Could not save: {e.Message}");
            }
        }

        private void OnApplicationQuit() => SaveAirline();

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
                SaveAirline();
        }

        // ---- Clock and fleet ---------------------------------------------------------

        private void DrawClockPanel(Rect rect, GUIStyle panel, GUIStyle label, GUIStyle small, GUIStyle smallButton)
        {
            GUI.Box(rect, GUIContent.none, panel);
            var airline = _operations.PlayerAirline;
            DrawSolid(new Rect(rect.x + 14f, rect.y + 16f, 10f, 22f), AirsideTheme.FromHex(airline.LiveryHex));
            GUI.Label(new Rect(rect.x + 32f, rect.y + 12f, rect.width - 46f, 24f), airline.Name, label);
            GUI.Label(new Rect(rect.x + 32f, rect.y + 36f, rect.width - 46f, 20f),
                $"Adelaide  {ClockText(_clock.Now)}  ·  {_operations.Clock.DateText(_clock.Now)}", small);
            // ADR 0053's persistent status strip: funds and reliability, quiet, always visible.
            GUI.Label(new Rect(rect.x + 32f, rect.y + 58f, rect.width - 46f, 20f),
                $"${_operations.CareerState.Funds:N0}  ·  {_operations.CareerState.Reliability}% reliability", small);
        }

        // The hotkeys (T/Tab/H) used to print in each label ("Operations (T)") but four of
        // those never fit the strip without overflowing it — the hotkeys still work, Controls
        // Help (F1) still lists them (from its own ControlsHelp.Sections data), the button
        // just doesn't spell it out any more, so there is nothing left for this table to carry
        // beyond the workspace and its label.
        private static readonly (HudWorkspace workspace, string label)[] WorkspaceTabs =
        {
            (HudWorkspace.Operations, "Operations"),
            (HudWorkspace.Map, "Map"),
            (HudWorkspace.Fleet, "Fleet"),
            (HudWorkspace.Contracts, "Contracts")
        };

        private GUIStyle _navActiveButtonStyle;

        /// <summary>
        /// The four player workspaces (ADR 0053), one open at a time. Replaces the previous
        /// three ad hoc clock-panel buttons; the active tab reads distinctly from the rest.
        /// </summary>
        private void DrawWorkspaceNav(Rect rect, GUIStyle smallButton)
        {
            var active = _navActiveButtonStyle ??= AirsideTheme.TextStyle(
                new GUIStyle(smallButton) { fontStyle = FontStyle.Bold }, AirsideTheme.SafetyYellow);

            var slot = rect.width / WorkspaceTabs.Length;
            for (var i = 0; i < WorkspaceTabs.Length; i++)
            {
                var (workspace, label) = WorkspaceTabs[i];
                var tabRect = new Rect(rect.x + i * slot, rect.y, slot - 4f, rect.height);
                // The hotkey used to print in the label ("Operations (T)") but four of those
                // never fit the strip without overflowing it — the hotkeys still work, Controls
                // Help (F1) still lists them, the button just doesn't spell it out any more.
                var style = _activeWorkspace == workspace ? active : smallButton;
                if (GUI.Button(tabRect, label, style))
                {
                    // The Map workspace needs a planning aircraft chosen and the lens reset —
                    // TogglePlanner/OpenPlanner already do that (it's what Tab and field-tag
                    // selection use); a plain SetWorkspace would open an empty "No aircraft to
                    // plan" planner the first time a player clicks this tab before selecting
                    // any aircraft.
                    if (workspace == HudWorkspace.Map)
                        TogglePlanner();
                    else
                        SetWorkspace(workspace);
                }
            }
        }

        /// <summary>The Contracts workspace (ADR 0053): career summary, the active contract's
        /// progress, or the authored contracts on offer when none is active.</summary>
        private void DrawContractsPanel(Rect rect, GUIStyle panel, GUIStyle title, GUIStyle label, GUIStyle small, GUIStyle smallButton)
        {
            var ink = AirsideTheme.RunwayInk;
            DrawSolid(rect, new Color(ink.r, ink.g, ink.b, 0.96f));
            GUI.Box(rect, GUIContent.none, panel);

            var x = rect.x + 16f;
            var inner = rect.width - 32f;
            GUI.Label(new Rect(x, rect.y + 10f, inner - 120f, 26f), "Contracts", title);
            if (GUI.Button(new Rect(rect.xMax - 108f, rect.y + 10f, 92f, 26f), "Close", smallButton))
                SetWorkspace(HudWorkspace.Contracts);

            var career = _operations.CareerState;
            var bold = Styled(label, "bold", st => new GUIStyle(st) { fontStyle = FontStyle.Bold });
            GUI.Label(new Rect(x, rect.y + 46f, inner, 20f),
                $"{career.Tier} tier  ·  ${career.Funds:N0}  ·  {career.Reliability}% reliability", bold);

            var y = rect.y + 78f;
            if (career.ActiveContract != null && RouteContractCatalogue.TryFind(career.ActiveContract.DefinitionId, out var active))
            {
                DrawActiveContractCard(x, y, inner, active, career.ActiveContract, label, small);
            }
            else
            {
                foreach (var definition in RouteContractCatalogue.All)
                    y = DrawContractOffer(x, y, inner, definition, career, label, small, smallButton) + 12f;
            }
        }

        private void DrawActiveContractCard(float x, float y, float width, RouteContractDefinition definition,
            ActiveRouteContract active, GUIStyle label, GUIStyle small)
        {
            var card = new Rect(x, y, width, 112f);
            DrawSolid(card, new Color(1f, 1f, 1f, 0.05f));
            AirsideTheme.DrawPanelFrame(card, AirsideTheme.SafetyYellow);
            var bold = Styled(label, "bold", st => new GUIStyle(st) { fontStyle = FontStyle.Bold });
            GUI.Label(new Rect(card.x + 12f, card.y + 10f, card.width - 24f, 22f),
                $"{definition.Id}  ·  {definition.OriginCode} ↔ {definition.DestinationCode}", bold);
            GUI.Label(new Rect(card.x + 12f, card.y + 32f, card.width - 24f, 20f),
                $"{active.CompletedRotations} of {definition.RequiredRotations} rotations complete", small);
            AirsideTheme.DrawProgressBar(new Rect(card.x + 12f, card.y + 54f, card.width - 24f, 8f),
                Mathf.Clamp01((float)active.CompletedRotations / definition.RequiredRotations),
                AirsideTheme.ClearGreen, AirsideTheme.Tarmac);
            GUI.Label(new Rect(card.x + 12f, card.y + 70f, card.width - 24f, 20f),
                $"${definition.PaymentPerRotation:N0} per rotation  ·  ${definition.CompletionReward:N0} on completion", small);
            GUI.Label(new Rect(card.x + 12f, card.y + 90f, card.width - 24f, 20f),
                $"Fly a {definition.EligibleType.Name} between {definition.OriginCode} and {definition.DestinationCode} to progress.", small);
        }

        /// <summary>One authored contract not yet accepted; returns the y just past it.</summary>
        private float DrawContractOffer(float x, float y, float width, RouteContractDefinition definition,
            AirlineCareerState career, GUIStyle label, GUIStyle small, GUIStyle smallButton)
        {
            var card = new Rect(x, y, width, 132f);
            DrawSolid(card, new Color(1f, 1f, 1f, 0.04f));
            AirsideTheme.DrawPanelFrame(card, AirsideTheme.Concrete);
            var bold = Styled(label, "bold", st => new GUIStyle(st) { fontStyle = FontStyle.Bold });
            GUI.Label(new Rect(card.x + 12f, card.y + 10f, card.width - 24f, 22f),
                $"{definition.Id}  ·  {definition.OriginCode} ↔ {definition.DestinationCode}", bold);
            GUI.Label(new Rect(card.x + 12f, card.y + 32f, card.width - 24f, 20f),
                $"{definition.RequiredRotations} rotations  ·  {definition.EligibleType.Name}  ·  {definition.RequiredTier} tier", small);
            GUI.Label(new Rect(card.x + 12f, card.y + 52f, card.width - 24f, 20f),
                $"${definition.PaymentPerRotation:N0} per rotation, +${definition.CompletionReward:N0} on completion", small);
            GUI.Label(new Rect(card.x + 12f, card.y + 72f, card.width - 24f, 20f),
                $"+{definition.ReliabilityGainPerRotation} reliability per rotation", small);

            var eligible = career.Tier >= definition.RequiredTier;
            GUI.enabled = eligible;
            if (GUI.Button(new Rect(card.x + 12f, card.y + 96f, 160f, 26f), "Accept contract", smallButton))
            {
                var result = _operations.AcceptContract(definition);
                if (result.Accepted)
                {
                    ShowToast($"Accepted {definition.Id}: {definition.OriginCode} ↔ {definition.DestinationCode}.");
                    SaveAirline();
                }
                else
                {
                    ShowToast(result.Reason);
                }
            }
            GUI.enabled = true;
            if (!eligible)
                GUI.Label(new Rect(card.x + 180f, card.y + 100f, card.width - 192f, 20f),
                    $"Needs {definition.RequiredTier} tier.", small);

            return card.yMax;
        }

        /// <summary>
        /// The always-visible roster sidebar — distinct from the <see cref="HudWorkspace.Fleet"/>
        /// nav tab, which opens the Hangar panel (<see cref="DrawHangarPanel"/>).
        /// </summary>
        private readonly List<FleetAircraft> _fleetPanelMine = new();
        private readonly Dictionary<Airline, List<FleetAircraft>> _fleetPanelOtherFleets = new();

        /// <summary>One pass over the whole fleet, reused by both the height pass and the draw
        /// pass below — each used to call FleetOf(airline) separately (itself a full fleet
        /// scan) for the height total and again to draw the rows, doubling the fleet scans
        /// on this always-visible sidebar every single OnGUI invocation.</summary>
        private void GroupFleetPanelAircraft()
        {
            var player = _operations.PlayerAirline;
            var mine = _fleetPanelMine;
            mine.Clear();
            foreach (var pair in _fleetPanelOtherFleets)
                pair.Value.Clear();
            foreach (var aircraft in _operations.Fleet)
            {
                if (ReferenceEquals(aircraft.Airline, player))
                {
                    mine.Add(aircraft);
                    continue;
                }
                if (!_fleetPanelOtherFleets.TryGetValue(aircraft.Airline, out var list))
                {
                    list = new List<FleetAircraft>();
                    _fleetPanelOtherFleets[aircraft.Airline] = list;
                }
                list.Add(aircraft);
            }
        }

        private void DrawFleetPanel(Rect area, GUIStyle panel, GUIStyle label, GUIStyle small, GUIStyle smallButton)
        {
            GroupFleetPanelAircraft();
            var estimatedInner = area.width - 48f;
            var contentHeight = FleetPanelContentHeight(estimatedInner);
            var rect = new Rect(area.x, area.y, area.width, Mathf.Min(Mathf.Max(contentHeight + 12f, 120f), area.height));
            GUI.Box(rect, GUIContent.none, panel);
            var view = new Rect(rect.x + 6f, rect.y + 6f, rect.width - 12f, rect.height - 12f);
            var scrollWidth = view.width - (contentHeight > view.height ? 18f : 0f);
            _fleetScroll = GUI.BeginScrollView(view, _fleetScroll,
                new Rect(0f, 0f, scrollWidth, Mathf.Max(view.height, contentHeight)));
            var x = 10f;
            var inner = scrollWidth - 20f;
            var y = 6f;

            var bold = Styled(label, "bold", st => new GUIStyle(st) { fontStyle = FontStyle.Bold });
            DrawOwnershipHeader(x, y, inner, Ownership.PlayerSection, _operations.PlayerAirline.LiveryHex, bold);
            y += 26f;
            GUI.Label(new Rect(x, y, inner, 18f), "Click an aircraft on the field — or a registration here.", small);
            y += 22f;
            foreach (var aircraft in _fleetPanelMine)
            {
                y = DrawPlayerAircraftRow(aircraft, x, y, inner, label, small, smallButton);
                y += 10f;
            }

            y += 6f;
            DrawOwnershipHeader(x, y, inner, Ownership.OtherSection, null, bold);
            y += 28f;
            foreach (var airline in _operations.Airlines)
            {
                if (airline.IsPlayer)
                    continue;
                // Other operators are quieter: a small livery dot and name, not a heading.
                var previous = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, Ownership.OtherAlpha);
                DrawSolid(new Rect(x, y + 5f, 6f, 10f), AirsideTheme.FromHex(airline.LiveryHex));
                GUI.Label(new Rect(x + 12f, y, inner - 12f, 20f), airline.Name, small);
                GUI.color = previous;
                y += 22f;
                if (_fleetPanelOtherFleets.TryGetValue(airline, out var fleet))
                {
                    foreach (var aircraft in fleet)
                    {
                        y = DrawTrafficAircraftRow(aircraft, x, y, inner, label, small);
                    }
                }
            }
            GUI.EndScrollView();
        }

        /// <summary>Mirrors the row heights drawn below so the panel hugs its content.</summary>
        private float FleetPanelContentHeight(float inner)
        {
            var height = 12f + 26f + 22f + 34f;
            foreach (var aircraft in _fleetPanelMine)
            {
                height += 56f + 10f;
                if (_selectedAircraftId == aircraft.Registration) height += 52f;
                if (aircraft.StateEndsAt.HasValue || AircraftStatus.IsWaiting(aircraft)) height += 12f;
                if (aircraft.State == FleetState.AtStand) height += 32f;
                if (aircraft.State == FleetState.AwaitingStand)
                    height += 84f + 32f * (StandButtonRows(CountFreeStands(), inner) - 1);
            }

            foreach (var airline in _operations.Airlines)
            {
                if (airline.IsPlayer)
                    continue;
                height += 22f;
                if (_fleetPanelOtherFleets.TryGetValue(airline, out var fleet))
                    foreach (var aircraft in fleet)
                        height += 54f + (_selectedAircraftId == aircraft.Registration ? 52f : 0f);
            }

            return height + 4f;
        }

        private float DrawPlayerAircraftRow(FleetAircraft aircraft, float x, float y, float width, GUIStyle label, GUIStyle small, GUIStyle smallButton)
        {
            var row = new Rect(x, y, width, 54f);
            DrawAircraftSelection(aircraft, row, $"{aircraft.Registration}  ·  {aircraft.Type.Name}", StatusText(aircraft), label, small);
            y += 56f;

            y = DrawSelectedAircraftDetail(aircraft, x, y, width, small);

            if (aircraft.StateEndsAt.HasValue)
            {
                AirsideTheme.DrawProgressBar(new Rect(x, y, width, 6f), (float)aircraft.StateProgress(_clock.Now),
                    AirsideTheme.CoastalBlue, AirsideTheme.Tarmac);
                y += 12f;
            }
            else if (AircraftStatus.IsWaiting(aircraft))
            {
                // No end time to count down to: fill towards the "this is too long" mark instead.
                AirsideTheme.DrawProgressBar(new Rect(x, y, width, 6f), AircraftStatus.WaitProgress(aircraft, _clock.Now),
                    SeverityColour(AircraftStatus.Severity(aircraft, _clock.Now), AirsideTheme.CoastalBlue), AirsideTheme.Tarmac);
                y += 12f;
            }

            switch (aircraft.State)
            {
                case FleetState.AtStand:
                    var planRect = new Rect(x, y, 140f, 26f);
                    if (IsGuided(aircraft, GuideStep.PlanFirstFlight))
                        DrawGuideHighlight(planRect);
                    if (GUI.Button(planRect, aircraft.Scheduled.HasValue ? "Change plan" : "Plan flight", smallButton))
                        OpenPlanner(aircraft);
                    if (aircraft.Scheduled.HasValue
                        && GUI.Button(new Rect(x + 150f, y, 120f, 26f), "Cancel flight", smallButton))
                        CancelPlannedFlight(aircraft);
                    y += 32f;
                    break;

                case FleetState.AwaitingStand:
                    GUI.Label(new Rect(x, y, width, 20f), "Choose a stand:", label);
                    y += 22f;
                    // Shortest taxi in, avoiding a bay too tight beside a Dash 8-400 when another is free.
                    var quickest = _operations.SuggestStand(aircraft);
                    if (quickest.HasValue)
                    {
                        // One click for the usual choice: the free bay with the shortest taxi in.
                        var quickRect = new Rect(x, y, width, 26f);
                        if (IsGuided(aircraft, GuideStep.ChooseStand))
                            DrawGuideHighlight(quickRect);
                        var minutes = Mathf.Max(1, Mathf.RoundToInt(AirlineOperations.TaxiInSecondsTo(quickest.Value) / 60f));
                        if (GUI.Button(quickRect, $"Best stand: {StandNames.Display(quickest.Value)} · {minutes} min taxi", smallButton))
                            AssignStandFromHud(aircraft, quickest.Value);
                        y += 30f;
                    }

                    // Wrap the bay buttons: six free bays at 82 px ran ~160 px past the 360 px
                    // fleet panel and off its right edge.
                    var bx = x;
                    var any = false;
                    foreach (var stand in _operations.FreeStands())
                    {
                        if (any && bx + StandButtonWidth > x + width)
                        {
                            bx = x;
                            y += 32f;
                        }

                        any = true;
                        if (GUI.Button(new Rect(bx, y, StandButtonWidth, 26f), StandNames.Short(stand), smallButton))
                            AssignStandFromHud(aircraft, stand);
                        bx += StandButtonWidth + StandButtonGap;
                    }

                    if (!any)
                        GUI.Label(new Rect(x, y, width, 26f), "All stands occupied — wait for one to clear.", small);
                    y += 32f;
                    break;
            }

            return y;
        }

        private const float StandButtonWidth = 76f;
        private const float StandButtonGap = 6f;

        private int CountFreeStands()
        {
            var count = 0;
            foreach (var _ in _operations.FreeStands())
                count++;
            return count;
        }

        /// <summary>Rows the wrapped bay buttons take in <paramref name="width"/> (at least one).</summary>
        private static int StandButtonRows(int buttons, float width)
        {
            var perRow = Mathf.Max(1, Mathf.FloorToInt((width + StandButtonGap) / (StandButtonWidth + StandButtonGap)));
            return Mathf.Max(1, (buttons + perRow - 1) / perRow);
        }

        private void AssignStandFromHud(FleetAircraft aircraft, StableId stand)
        {
            var result = _operations.AssignStand(aircraft, stand);
            if (result.Accepted)
                SaveAirline();
            else
                ShowToast(result.Reason);
        }

        private float DrawTrafficAircraftRow(FleetAircraft aircraft, float x, float y, float width, GUIStyle label, GUIStyle small)
        {
            var row = new Rect(x, y, width, 52f);
            DrawAircraftSelection(aircraft, row, $"{aircraft.Registration}  ·  {aircraft.Type.Name}", StatusText(aircraft), small, small);
            return DrawSelectedAircraftDetail(aircraft, x, y + 54f, width, small);
        }

        private void DrawAircraftSelection(FleetAircraft aircraft, Rect rect, string heading, string status, GUIStyle headingStyle, GUIStyle statusStyle)
        {
            var selected = _selectedAircraftId == aircraft.Registration;
            var mouse = Event.current != null ? Event.current.mousePosition : Vector2.negativeInfinity;
            var hovered = rect.Contains(mouse);
            if (selected || hovered)
            {
                var fill = selected
                    ? new Color(AirsideTheme.CoastalBlue.r, AirsideTheme.CoastalBlue.g, AirsideTheme.CoastalBlue.b, 0.42f)
                    : new Color(AirsideTheme.CoastalBlue.r, AirsideTheme.CoastalBlue.g, AirsideTheme.CoastalBlue.b, 0.22f);
                DrawSolid(new Rect(rect.x - 5f, rect.y - 2f, rect.width + 10f, rect.height + 4f), fill);
                if (selected)
                    AirsideTheme.DrawPanelFrame(new Rect(rect.x - 5f, rect.y - 2f, rect.width + 10f, rect.height + 4f),
                        AirsideTheme.SafetyYellow);
            }

            var mine = aircraft.Airline.IsPlayer;
            var previousColour = GUI.color;
            if (mine)
                DrawSolid(new Rect(rect.x - 5f, rect.y - 2f, 3f, rect.height + 4f), AirsideTheme.FromHex(aircraft.Airline.LiveryHex));
            else if (!selected)
                GUI.color = new Color(1f, 1f, 1f, Ownership.AlphaFor(aircraft.Airline));
            GUI.Label(new Rect(rect.x, rect.y, rect.width - (mine ? 132f : 70f), 20f), heading, headingStyle);
            var severity = AircraftStatus.Severity(aircraft, _clock.Now);
            var previousContent = GUI.contentColor;
            if (severity != StatusSeverity.Normal)
                GUI.contentColor = SeverityColour(severity, previousContent);
            GUI.Label(new Rect(rect.x, rect.y + 20f, rect.width, rect.height - 20f), status, statusStyle);
            GUI.contentColor = previousContent;
            GUI.color = previousColour;
            if (mine)
                DrawOwnershipBadge(new Rect(rect.x, rect.y - 4f, rect.width - 72f, rect.height), aircraft.Airline, statusStyle);
            var action = selected ? "Selected" : "Select";
            var actionStyle = selected
                ? Styled(statusStyle, "action-selected", s => AirsideTheme.TextStyle(
                    new GUIStyle(s) { alignment = TextAnchor.UpperRight, fontStyle = FontStyle.Bold }, AirsideTheme.SafetyYellow))
                : Styled(statusStyle, "action", s => AirsideTheme.TextStyle(
                    new GUIStyle(s) { alignment = TextAnchor.UpperRight, fontStyle = FontStyle.Bold }, AirsideTheme.CoastalBlue));
            GUI.Label(new Rect(rect.xMax - 70f, rect.y, 70f, 18f), action, actionStyle);

            // Whole row is the hit target — the previous 20 px registration-only target was
            // effectively invisible in packaged play, which is why Bailey could not use it.
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                SelectAircraft(aircraft);
        }

        private float DrawSelectedAircraftDetail(FleetAircraft aircraft, float x, float y, float width, GUIStyle small)
        {
            if (_selectedAircraftId != aircraft.Registration)
                return y;

            var onField = _fleetViewById.ContainsKey(aircraft.Registration);
            var accent = AirsideTheme.FromHex(aircraft.Airline.LiveryHex);
            var card = new Rect(x, y + 2f, width, 42f);
            DrawSolid(card, new Color(AirsideTheme.RunwayInk.r, AirsideTheme.RunwayInk.g, AirsideTheme.RunwayInk.b, 0.9f));
            DrawSolid(new Rect(card.x, card.y, 4f, card.height), accent);
            var mode = onField ? "FOLLOWING AT ADELAIDE" : "TRACKING ON ROUTE MAP";
            GUI.Label(new Rect(card.x + 10f, card.y + 3f, card.width - 20f, 18f), mode, small);
            GUI.Label(new Rect(card.x + 10f, card.y + 21f, card.width - 20f, 18f),
                $"{aircraft.Airline.Name}  ·  {aircraft.Type.Name}", small);
            return y + 52f;
        }

        /// <summary>
        /// Always-visible selection card above the control bar so selection stays readable
        /// even when the fleet panel is covered by the destinations map.
        /// </summary>
        private void DrawSelectionHudCard(HudLayout layout, GUIStyle panel, GUIStyle label, GUIStyle small)
        {
            if (!TrySelectionHudCardRect(layout, out var rect))
                return;
            if (!_fleetAircraftById.TryGetValue(_selectedAircraftId, out var aircraft))
                return;

            GUI.Box(rect, GUIContent.none, panel);
            AirsideTheme.DrawPanelFrame(rect, AirsideTheme.SafetyYellow);
            var onField = _fleetViewById.ContainsKey(aircraft.Registration);
            var accent = AirsideTheme.FromHex(aircraft.Airline.LiveryHex);
            DrawSolid(new Rect(rect.x, rect.y, 5f, rect.height), accent);
            var bold = Styled(label, "bold", s => new GUIStyle(s) { fontStyle = FontStyle.Bold });
            GUI.Label(new Rect(rect.x + 14f, rect.y + 8f, rect.width - 90f, 20f),
                $"{aircraft.Registration}  ·  {aircraft.Airline.Name}  ·  {aircraft.Type.Name}", bold);
            if (aircraft.Airline.IsPlayer)
            {
                DrawOwnershipBadge(new Rect(rect.x, rect.y + 6f, rect.width - 8f, rect.height), aircraft.Airline, small);
            }
            else
            {
                var quiet = Styled(small, "other-operator", s => new GUIStyle(s) { alignment = TextAnchor.UpperRight, fontSize = 10, wordWrap = false });
                var previous = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, Ownership.OtherAlpha);
                GUI.Label(new Rect(rect.xMax - 130f, rect.y + 10f, 120f, 16f), "OTHER OPERATOR", quiet);
                GUI.color = previous;
            }
            GUI.Label(new Rect(rect.x + 14f, rect.y + 30f, rect.width - 28f, 34f),
                onField
                    ? $"{StatusText(aircraft)}\nCamera following — Overview / R / Esc clears."
                    : $"{StatusText(aircraft)}\nAway from Adelaide — tracked on the map.",
                small);
        }

        private bool TrySelectionHudCardRect(HudLayout layout, out Rect rect)
        {
            rect = default;
            if (string.IsNullOrEmpty(_selectedAircraftId) || _operations == null)
                return false;
            // Every overlay already shows the selection, and the card would sit over its buttons.
            if (_activeWorkspace != HudWorkspace.None || _devToolsOpen)
                return false;
            var width = Mathf.Min(420f, layout.Viewport.x - AirlineHudLayout.Margin * 2f);
            var height = 72f;
            var x = (layout.Viewport.x - width) * 0.5f;
            var y = Mathf.Max(AirlineHudLayout.Margin, layout.SpeedReadout.y - height - 12f);
            rect = new Rect(x, y, width, height);
            return true;
        }

        private void SelectAircraft(FleetAircraft aircraft)
        {
            _selectedAircraftId = aircraft.Registration;
            var plannerStaysOpen = _activeWorkspace == HudWorkspace.Map && aircraft.Airline.IsPlayer;
            if (aircraft.Airline.IsPlayer)
                SetPlanningAircraft(aircraft);

            var following = TryFollowFleetAircraft(aircraft.Registration);
            _devToolsOpen = false;
            if (plannerStaysOpen)
            {
                // Switching aircraft inside the planner keeps planning; the camera still follows.
            }
            else if (following)
            {
                _activeWorkspace = HudWorkspace.None;
            }
            else
            {
                // Away from Adelaide: the route map is where it can be seen, flying live.
                if (_activeWorkspace != HudWorkspace.Map)
                    _mapLens.Reset();
                _activeWorkspace = HudWorkspace.Map;
            }

            if (!following && aircraft.IsOffMap)
                StartMapTracking(aircraft.Registration);
            else if (_mapTrackId != null && _mapTrackId != aircraft.Registration)
                _mapTrackId = null;

            PlayUiClick();
        }

        /// <summary>Step the selection through aircraft: the player's fleet while planning, everyone otherwise.</summary>
        private void CycleSelection(int delta)
        {
            if (_operations == null)
                return;
            IReadOnlyList<FleetAircraft> pool;
            if (_activeWorkspace == HudWorkspace.Map)
            {
                pool = PlayerFleet();
                var next = FlightPlanner.Cycle(pool, _mapAircraft?.Registration, delta);
                if (next != null)
                    SelectAircraft(next);
                return;
            }

            pool = _operations.Fleet;
            var picked = FlightPlanner.Cycle(pool, _selectedAircraftId, delta);
            if (picked != null)
                SelectAircraft(picked);
        }

        private IReadOnlyList<FleetAircraft> PlayerFleet()
        {
            _playerFleetRows.Clear();
            if (_operations != null)
                _playerFleetRows.AddRange(_operations.FleetOf(_operations.PlayerAirline));
            return _playerFleetRows;
        }

        private bool ClearAircraftSelection()
        {
            if (string.IsNullOrEmpty(_selectedAircraftId))
                return false;
            _selectedAircraftId = null;
            _activeWorkspace = HudWorkspace.None;
            _devToolsOpen = false;
            return true;
        }

        /// <summary>Esc closes whichever overlay is open before it touches the selection.</summary>
        private bool TryCloseAirlineOverlay()
        {
            if (_activeWorkspace == HudWorkspace.None && !_devToolsOpen)
                return false;
            _activeWorkspace = HudWorkspace.None;
            _devToolsOpen = false;
            _mapPressed = false;
            _mapPanning = false;
            PlayUiClick();
            return true;
        }

        private string StatusText(FleetAircraft aircraft)
        {
            var to = aircraft.CurrentDestination;
            var dest = to.HasValue ? to.Value.Name : string.Empty;
            var ends = aircraft.StateEndsAt.HasValue ? ClockText(aircraft.StateEndsAt.Value) : string.Empty;
            var wait = AircraftStatus.WaitSuffix(aircraft, _clock.Now);
            return aircraft.State switch
            {
                FleetState.AtStand => aircraft.Scheduled.HasValue
                    ? $"On {StandNames.Display(aircraft.Stand)} · departs {ClockText(aircraft.Scheduled.Value.DepartAt)} for {aircraft.Scheduled.Value.Destination.Name}"
                    : $"On {StandNames.Display(aircraft.Stand)} · no flight planned",
                FleetState.TaxiOut => $"Taxiing to the runway · {dest}",
                FleetState.HoldingShort => $"Holding short for the runway · {dest}{wait}",
                FleetState.TakingOff => $"Taking off for {dest}",
                FleetState.Outbound => $"En route to {dest}{EnrouteAltitudeText(aircraft)} · lands {ends}",
                FleetState.AtDestination => $"On the ground at {dest} · departs {ends}",
                FleetState.Inbound => $"Returning from {dest}{EnrouteAltitudeText(aircraft)} · back {ends}",
                FleetState.HoldingForLanding => $"In the Adelaide circuit, waiting to land{wait}",
                FleetState.Landing => "Landing at Adelaide",
                FleetState.AwaitingStand => $"Landed · needs a stand{wait}",
                FleetState.TaxiIn => $"Taxiing to {StandNames.Display(aircraft.Stand)}",
                _ => aircraft.State.ToString()
            };
        }

        // ---- Destinations map ---------------------------------------------------------

        private void TogglePlanner()
        {
            if (_activeWorkspace == HudWorkspace.Map)
            {
                _activeWorkspace = HudWorkspace.None;
                PlayUiClick();
                return;
            }

            OpenPlanner(null);
        }

        /// <summary>
        /// Open the flight planner on an aircraft — or, given none, the selected player
        /// aircraft, else the first parked one with nothing planned.
        /// </summary>
        private void OpenPlanner(FleetAircraft aircraft)
        {
            _activeWorkspace = HudWorkspace.Map;
            _devToolsOpen = false;
            _mapLens.Reset();
            _mapTrackId = null;
            _plannerScroll = Vector2.zero;
            var chosen = aircraft ?? FlightPlanner.ChoosePlanningAircraft(PlayerFleet(), _selectedAircraftId ?? _mapAircraft?.Registration);
            if (chosen != null)
            {
                _selectedAircraftId = chosen.Registration;
                TryFollowFleetAircraft(chosen.Registration);
            }
            SetPlanningAircraft(chosen, force: true);
            PlayUiClick();
        }

        private void SetPlanningAircraft(FleetAircraft aircraft, bool force = false)
        {
            if (!force && ReferenceEquals(aircraft, _mapAircraft))
                return;
            _mapAircraft = aircraft;
            _mapMessage = null;
            // An aircraft with a plan reopens on it, so "Change plan" starts from what is booked.
            if (aircraft != null && aircraft.Scheduled.HasValue)
            {
                _mapSelection = aircraft.Scheduled.Value.Destination;
                _departureDelaySeconds = FlightPlanner.ClampDelay(aircraft.Scheduled.Value.DepartAt.ElapsedSeconds - _clock.Now.ElapsedSeconds);
            }
            else if (_mapSelection.HasValue && aircraft != null && !_operations.CanReach(aircraft, _mapSelection.Value))
            {
                _mapSelection = null;
            }
        }

        private void CancelPlannedFlight(FleetAircraft aircraft)
        {
            var reliabilityBefore = _operations.CareerState.Reliability;
            var result = _operations.CancelDeparture(aircraft);
            if (result.Accepted)
            {
                var lost = reliabilityBefore - _operations.CareerState.Reliability;
                ShowToast(lost > 0
                    ? $"{aircraft.Registration}'s flight is cancelled — reliability down {lost}."
                    : $"{aircraft.Registration}'s flight is cancelled.");
                SaveAirline();
            }
            else
            {
                ShowToast(result.Reason);
            }
        }

        /// <summary>
        /// Opens <paramref name="target"/> as the one active workspace (ADR 0053), or closes it
        /// if it is already open. Shared by the workspace nav strip and its hotkeys (H/T).
        /// </summary>
        private void SetWorkspace(HudWorkspace target)
        {
            _activeWorkspace = _activeWorkspace == target ? HudWorkspace.None : target;
            if (_activeWorkspace != HudWorkspace.None)
                _devToolsOpen = false;
            PlayUiClick();
        }

        private void ToggleDevTools()
        {
            _devToolsOpen = !_devToolsOpen;
            if (_devToolsOpen)
                _activeWorkspace = HudWorkspace.None;
            PlayUiClick();
        }

        private struct MapFlight
        {
            public FleetAircraft Aircraft;
            public double Latitude;
            public double Longitude;
            public double Progress;
            public Destination From;
            public Destination To;
            public Vector2 Point;
            public float HeadingDegrees;
            public EnrouteProfile Profile;
            public double ElapsedSeconds;
        }

        private readonly List<MapFlight> _mapFlights = new();
        private readonly List<Rect> _mapControlRects = new();
        private string _mapTrackId;
        private static Texture2D _planeIcon;

        /// <summary>Where each off-map flight is right now, to the sub-second, along its great circle.</summary>
        private void LocateMapFlights()
        {
            _mapFlights.Clear();
            var home = _operations.Home;
            foreach (var flying in _operations.Fleet)
            {
                if (!flying.IsOffMap || !flying.CurrentDestination.HasValue)
                    continue;
                var destination = flying.CurrentDestination.Value;
                var inbound = flying.State == FleetState.Inbound;
                var flight = new MapFlight
                {
                    Aircraft = flying,
                    From = inbound ? destination : home,
                    To = inbound ? home : destination
                };
                if (flying.State == FleetState.AtDestination || !TryEnroute(flying, out flight.Profile, out flight.ElapsedSeconds))
                    flight.Progress = 1.0;
                else
                    // Distance flown, not time elapsed: slower in the climb and descent.
                    flight.Progress = flight.Profile.DistanceFractionAt(flight.ElapsedSeconds);
                RouteMap.GreatCirclePoint(flight.From.Latitude, flight.From.Longitude, flight.To.Latitude, flight.To.Longitude,
                    flight.Progress, out flight.Latitude, out flight.Longitude);
                _mapFlights.Add(flight);
            }
        }

        private void DrawDestinationsMap(Rect rect, GUIStyle panel, GUIStyle title, GUIStyle label, GUIStyle small, GUIStyle smallButton)
        {
            // Near-opaque: the translucent HUD panel let runways and taxiways read
            // through the coastline and route lines.
            var ink = AirsideTheme.RunwayInk;
            DrawSolid(rect, new Color(ink.r, ink.g, ink.b, 0.96f));
            GUI.Box(rect, GUIContent.none, panel);

            var detailWidth = Mathf.Clamp(rect.width * 0.4f, 240f, 320f);
            var mapRect = new Rect(rect.x + 12f, rect.y + 12f, rect.width - detailWidth - 36f, rect.height - 24f);
            var detail = new Rect(mapRect.xMax + 12f, rect.y + 12f, detailWidth, rect.height - 24f);

            var aircraft = _mapAircraft;
            var home = _operations.Home;

            LocateMapFlights();
            var tracked = -1;
            for (var i = 0; i < _mapFlights.Count; i++)
                if (_mapFlights[i].Aircraft.Registration == _mapTrackId)
                    tracked = i;
            if (!string.IsNullOrEmpty(_mapTrackId) && tracked < 0)
            {
                // It landed (or was never away): hand the view back rather than stare at nothing.
                _mapTrackId = null;
            }
            if (tracked >= 0 && !_mapPanning)
                _mapLens.CenterOn(mapRect.width, mapRect.height, _mapFlights[tracked].Longitude, _mapFlights[tracked].Latitude);

            // Project everything clickable first, so the pointer handler can hit-test it.
            FlightPlanner.DestinationsFor(_operations, aircraft, _mapDestinationRows);
            _mapDestinationPoints.Clear();
            foreach (var row in _mapDestinationRows)
            {
                var p = Project(mapRect, row.Destination.Longitude, row.Destination.Latitude);
                _mapDestinationPoints.Add((p.x, p.y));
            }

            var homePoint = Project(mapRect, home.Longitude, home.Latitude);
            _mapAircraftRows.Clear();
            _mapAircraftPoints.Clear();
            for (var i = 0; i < _mapFlights.Count; i++)
            {
                var flight = _mapFlights[i];
                flight.Point = Project(mapRect, flight.Longitude, flight.Latitude);
                // Heading from a point a little further along (or behind, at the very end).
                var step = flight.Progress < 0.995 ? 0.004 : -0.004;
                RouteMap.GreatCirclePoint(flight.From.Latitude, flight.From.Longitude, flight.To.Latitude, flight.To.Longitude,
                    Math.Max(0.0, Math.Min(1.0, flight.Progress + step)), out var aheadLat, out var aheadLon);
                var ahead = Project(mapRect, aheadLon, aheadLat);
                var delta = step > 0 ? ahead - flight.Point : flight.Point - ahead;
                if (flight.Aircraft.State == FleetState.AtDestination)
                {
                    // Parked at the far end, nose pointing home.
                    delta = homePoint - flight.Point;
                }
                flight.HeadingDegrees = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg + 90f;
                _mapFlights[i] = flight;
                _mapAircraftRows.Add(flight.Aircraft);
                _mapAircraftPoints.Add((flight.Point.x, flight.Point.y));
            }

            // Map controls in the top-left corner; the pointer handler leaves them alone.
            _mapControlRects.Clear();
            var trackRect = new Rect(mapRect.x + 6f, mapRect.y + 6f, 170f, 24f);
            var zoomOutRect = new Rect(trackRect.xMax + 6f, trackRect.y, 90f, 24f);
            var selectedFlight = -1;
            for (var i = 0; i < _mapFlights.Count; i++)
                if (_mapFlights[i].Aircraft.Registration == _selectedAircraftId)
                    selectedFlight = i;
            var showTrack = tracked >= 0 || selectedFlight >= 0;
            if (showTrack)
                _mapControlRects.Add(trackRect);
            if (_mapLens.Zoom > AustraliaMapLens.MinZoom + 0.01f)
                _mapControlRects.Add(zoomOutRect);

            HandleMapPointer(mapRect);
            ApplyMapZoomEasing(mapRect);
            DrawAustraliaBase(mapRect);

            var mouse = Event.current.mousePosition;
            var hovered = mapRect.Contains(mouse) && !_mapPanning
                ? FlightPlanner.NearestWithin(_mapDestinationPoints, mouse.x, mouse.y)
                : -1;
            if (hovered >= 0 && !mapRect.Contains(new Vector2(_mapDestinationPoints[hovered].x, _mapDestinationPoints[hovered].y)))
                hovered = -1;

            // Routes flown right now: flown part solid, the rest faint, along the great circle.
            foreach (var flight in _mapFlights)
            {
                var colour = AirsideTheme.FromHex(flight.Aircraft.Airline.LiveryHex);
                DrawGreatCircle(mapRect, flight.From, flight.To, 0.0, flight.Progress, new Color(colour.r, colour.g, colour.b, 0.8f), 2f);
                DrawGreatCircle(mapRect, flight.From, flight.To, flight.Progress, 1.0, new Color(colour.r, colour.g, colour.b, 0.3f), 1.5f);
            }

            if (hovered >= 0 && !(_mapSelection.HasValue && _mapSelection.Value.Equals(_mapDestinationRows[hovered].Destination)))
                DrawGreatCircle(mapRect, home, _mapDestinationRows[hovered].Destination, 0.0, 1.0,
                    new Color(AirsideTheme.Cloud.r, AirsideTheme.Cloud.g, AirsideTheme.Cloud.b, 0.35f), 1.5f);

            for (var i = 0; i < _mapDestinationRows.Count; i++)
            {
                var row = _mapDestinationRows[i];
                var point = new Vector2(_mapDestinationPoints[i].x, _mapDestinationPoints[i].y);
                var selected = _mapSelection.HasValue && _mapSelection.Value.Equals(row.Destination);
                if (selected)
                    DrawGreatCircle(mapRect, home, row.Destination, 0.0, 1.0, AirsideTheme.SafetyYellow, 2.5f);
                if (!mapRect.Contains(point))
                    continue;

                // A clean dot marks the destination — this is where parked aircraft sit.
                var colour = selected ? AirsideTheme.SafetyYellow : row.Reachable ? AirsideTheme.ClearGreen : AirsideTheme.Concrete;
                var zoomBoost = Mathf.Lerp(1f, 1.5f, Mathf.InverseLerp(1f, 10f, _mapLens.Zoom));
                var size = (selected ? 20f : i == hovered ? 18f : 14f) * zoomBoost;
                DrawAirportIcon(point, size, colour);
                if (i == hovered)
                    AirsideTheme.DrawPanelFrame(new Rect(point.x - size * 0.5f, point.y - size * 0.5f, size, size), AirsideTheme.Cloud);
                GUI.Label(new Rect(point.x + size * 0.5f + 2f, point.y - 9f, _mapLens.Zoom >= 6f ? 140f : 60f, 18f),
                    _mapLens.Zoom >= 6f ? $"{row.Destination.Code} {row.Destination.Name}" : row.Destination.Code, small);
            }

            if (mapRect.Contains(homePoint))
            {
                var homeSize = 22f * Mathf.Lerp(1f, 1.5f, Mathf.InverseLerp(1f, 10f, _mapLens.Zoom));
                DrawAirportIcon(homePoint, homeSize, AirsideTheme.FromHex(_operations.PlayerAirline.LiveryHex));
                // Left of the dot: Kingscote and Port Lincoln sit just to its right and below.
                var adlStyle = Styled(label, "middle-right", s => new GUIStyle(s) { alignment = TextAnchor.MiddleRight });
                GUI.Label(new Rect(homePoint.x - 89f - homeSize * 0.5f, homePoint.y - 9f, 80f, 18f), "ADL", adlStyle);
            }

            // Aircraft icons on top of everything else on the map.
            for (var i = 0; i < _mapFlights.Count; i++)
            {
                var flight = _mapFlights[i];
                if (!mapRect.Contains(flight.Point))
                    continue;
                var mine = flight.Aircraft.Airline.IsPlayer;
                var isSelected = flight.Aircraft.Registration == _selectedAircraftId;
                // Other operators fly smaller and fainter on the map, so your aircraft read first.
                var iconSize = Mathf.Lerp(18f, 30f, Mathf.InverseLerp(1f, 12f, _mapLens.Zoom)) * (mine ? 1.15f : 0.85f);
                var iconAlpha = isSelected || i == tracked ? 1f : Ownership.AlphaFor(flight.Aircraft.Airline);
                if (isSelected || i == tracked)
                    DrawPlaneIcon(flight.Point, iconSize + 8f, flight.HeadingDegrees, AirsideTheme.SafetyYellow);
                else if (mine)
                    DrawPlaneIcon(flight.Point, iconSize + 6f, flight.HeadingDegrees, new Color(1f, 1f, 1f, 0.85f));
                DrawPlaneIcon(flight.Point, iconSize + 3f, flight.HeadingDegrees, new Color(0f, 0f, 0f, 0.75f * iconAlpha));
                var iconColour = AirsideTheme.FromHex(flight.Aircraft.Airline.LiveryHex);
                iconColour.a = iconAlpha;
                DrawPlaneIcon(flight.Point, iconSize, flight.HeadingDegrees, iconColour);

                var labelColour = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, iconAlpha);
                var detailed = isSelected || i == tracked || _mapLens.Zoom >= 4f;
                var labelRect = new Rect(flight.Point.x + iconSize * 0.6f, flight.Point.y - 10f, 300f, detailed ? 36f : 18f);
                if (detailed)
                    DrawSolid(labelRect, new Color(ink.r, ink.g, ink.b, 0.75f));
                GUI.Label(new Rect(labelRect.x + 4f, labelRect.y + 1f, labelRect.width - 8f, 18f),
                    $"{FlightNumber.OrRegistration(flight.Aircraft)} → {flight.To.Code}", small);
                if (detailed)
                    GUI.Label(new Rect(labelRect.x + 4f, labelRect.y + 17f, labelRect.width - 8f, 18f),
                        $"{MapFlightDetail(flight)} · {flight.Aircraft.Registration} {flight.Aircraft.Type.Name}", small);
                GUI.color = labelColour;
            }

            if (hovered >= 0)
            {
                var row = _mapDestinationRows[hovered];
                var h = _mapDestinationPoints[hovered];
                var tip = row.Reachable
                    ? $"{row.Destination.Name} · {row.DistanceKm:0} km · {DurationText(row.AirborneSeconds)}"
                    : $"{row.Destination.Name} · {row.DistanceKm:0} km · out of range";
                // Keep the tip inside the map: near the bottom edge (Hobart, Launceston) it used
                // to hang below the panel over the 3D field, and it never clamped on the left.
                var tipRect = new Rect(
                    Mathf.Max(mapRect.x, Mathf.Min(h.x + 12f, mapRect.xMax - 250f)),
                    h.y + 12f + 22f > mapRect.yMax ? h.y - 12f - 22f : h.y + 12f,
                    250f, 22f);
                DrawSolid(tipRect, new Color(ink.r, ink.g, ink.b, 0.92f));
                AirsideTheme.DrawPanelFrame(tipRect, row.Reachable ? AirsideTheme.ClearGreen : AirsideTheme.Concrete);
                GUI.Label(new Rect(tipRect.x + 6f, tipRect.y + 2f, tipRect.width - 12f, 18f), tip, small);
            }

            if (showTrack)
            {
                var trackText = tracked >= 0
                    ? $"Stop tracking {_mapTrackId}"
                    : $"Track {_selectedAircraftId} live";
                if (GUI.Button(trackRect, trackText, smallButton))
                {
                    if (tracked >= 0)
                        _mapTrackId = null;
                    else
                        StartMapTracking(_selectedAircraftId);
                }
            }

            if (_mapLens.Zoom > AustraliaMapLens.MinZoom + 0.01f && GUI.Button(zoomOutRect, "Zoom out", smallButton))
            {
                _mapTrackId = null;
                _mapLens.Reset();
            }

            DrawPlanner(detail, aircraft, title, label, small, smallButton);
        }

        private float _mapZoomPending;
        private Vector2 _mapZoomAnchor;
        private float _mapZoomLastTime;

        private void ApplyMapZoomEasing(Rect mapRect)
        {
            if (Event.current.type != EventType.Repaint)
                return;
            var now = Time.unscaledTime;
            var dt = Mathf.Clamp(now - _mapZoomLastTime, 0f, 0.1f);
            _mapZoomLastTime = now;
            if (Mathf.Abs(_mapZoomPending) < 0.0005f)
            {
                _mapZoomPending = 0f;
                return;
            }

            var applied = MapZoom.EaseStep(_mapZoomPending, dt);
            _mapZoomPending -= applied;
            // Tracking keeps the flight centred, so zoom about the centre rather than the cursor.
            var anchor = string.IsNullOrEmpty(_mapTrackId) ? _mapZoomAnchor : mapRect.size * 0.5f;
            _mapLens.ZoomAtGui(mapRect.width, mapRect.height, anchor.x, anchor.y, Mathf.Exp(applied));
        }

        private string MapFlightDetail(MapFlight flight)
        {
            var aircraft = flight.Aircraft;
            var ends = aircraft.StateEndsAt.HasValue ? ClockText(aircraft.StateEndsAt.Value) : "";
            if (aircraft.State == FleetState.AtDestination)
                return $"On the ground at {flight.To.Code} · leaves {ends}";
            var legKm = flight.From.DistanceKmTo(flight.To);
            var toGo = legKm * (1.0 - flight.Progress);
            var profile = flight.Profile;
            var t = flight.ElapsedSeconds;
            var trend = profile.PhaseAt(t) switch
            {
                EnroutePhase.Climb => " ▲",
                EnroutePhase.Descent => " ▼",
                _ => string.Empty
            };
            return $"{EnrouteProfile.AltitudeText(profile.AltitudeFeetAt(t))}{trend} · {profile.GroundSpeedKnotsAt(t):0} kt · {toGo:0} km · lands {ends}";
        }

        /// <summary>The away leg an aircraft is flying and how far into it, at sub-second time.</summary>
        private bool TryEnroute(FleetAircraft aircraft, out EnrouteProfile profile, out double elapsedSeconds)
        {
            profile = default;
            elapsedSeconds = 0;
            if (aircraft.State is not (FleetState.Outbound or FleetState.Inbound)
                || !aircraft.StateEndsAt.HasValue || !aircraft.CurrentDestination.HasValue)
                return false;
            var started = aircraft.StateStartedAt.ElapsedSeconds;
            profile = new EnrouteProfile(_operations.DistanceKm(aircraft.CurrentDestination.Value),
                aircraft.StateEndsAt.Value.ElapsedSeconds - started, aircraft.Type);
            elapsedSeconds = Math.Max(0.0, _preciseTime - started);
            return true;
        }

        private string EnrouteAltitudeText(FleetAircraft aircraft) =>
            TryEnroute(aircraft, out var profile, out var elapsed)
                ? " · " + EnrouteProfile.AltitudeText(profile.AltitudeFeetAt(elapsed))
                : string.Empty;

        private void StartMapTracking(string aircraftId)
        {
            _mapTrackId = aircraftId;
            if (_mapLens.Zoom < RouteMap.TrackingZoom)
                _mapLens.SetZoom(RouteMap.TrackingZoom);
            PlayUiClick();
        }

        private void DrawGreatCircle(Rect clip, Destination from, Destination to, double t0, double t1, Color colour, float thickness)
        {
            if (t1 <= t0)
                return;
            const int segments = 32;
            var steps = Math.Max(1, (int)Math.Ceiling(segments * (t1 - t0)));
            RouteMap.GreatCirclePoint(from.Latitude, from.Longitude, to.Latitude, to.Longitude, t0, out var lat, out var lon);
            var previous = Project(clip, lon, lat);
            for (var i = 1; i <= steps; i++)
            {
                var t = t0 + (t1 - t0) * i / steps;
                RouteMap.GreatCirclePoint(from.Latitude, from.Longitude, to.Latitude, to.Longitude, t, out lat, out lon);
                var next = Project(clip, lon, lat);
                DrawClippedLine(clip, previous, next, colour, thickness);
                previous = next;
            }
        }

        /// <summary>A line trimmed to the map, so zoomed-in coastlines and routes never spill over the HUD.</summary>
        private static void DrawClippedLine(Rect clip, Vector2 from, Vector2 to, Color colour, float thickness)
        {
            float x0 = from.x, y0 = from.y, x1 = to.x, y1 = to.y;
            if (!RouteMap.ClipSegment(ref x0, ref y0, ref x1, ref y1, clip.xMin, clip.yMin, clip.xMax, clip.yMax))
                return;
            DrawLine(new Vector2(x0, y0), new Vector2(x1, y1), colour, thickness);
        }

        /// <summary>Top-down aircraft silhouette, nose up, drawn rotated to a heading.</summary>
        private static void DrawPlaneIcon(Vector2 centre, float size, float headingDegrees, Color colour)
        {
            var matrix = GUI.matrix;
            GUI.matrix = matrix
                         * Matrix4x4.Translate(centre)
                         * Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, headingDegrees))
                         * Matrix4x4.Translate(-centre);
            var previous = GUI.color;
            GUI.color = colour;
            GUI.DrawTexture(new Rect(centre.x - size * 0.5f, centre.y - size * 0.5f, size, size), PlaneIcon());
            GUI.color = previous;
            GUI.matrix = matrix;
        }

        private static Texture2D PlaneIcon()
        {
            if (_planeIcon != null)
                return _planeIcon;

            const int n = 64;
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[n * n];
            for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
            {
                // u across (-1..1), v along with +1 at the nose (texture top draws at the top in IMGUI).
                var u = (x + 0.5f) / n * 2f - 1f;
                var v = (y + 0.5f) / n * 2f - 1f;
                var au = Mathf.Abs(u);
                var noseTaper = v > 0.7f ? Mathf.Lerp(0.1f, 0.03f, (v - 0.7f) / 0.25f) : 0.1f;
                var fuselage = au < noseTaper && v > -0.9f && v < 0.95f;
                var wing = au < 0.96f && v > 0.08f && v < 0.3f - 0.06f * au;
                var tail = au < 0.38f && v > -0.86f && v < -0.7f + 0.04f * (1f - au);
                pixels[y * n + x] = fuselage || wing || tail ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            _planeIcon = texture;
            return _planeIcon;
        }

        private static Texture2D _airportIcon;

        /// <summary>A clean antialiased dot marking a destination on the map.</summary>
        private static void DrawAirportIcon(Vector2 centre, float size, Color colour)
        {
            var previous = GUI.color;
            GUI.color = colour;
            GUI.DrawTexture(new Rect(centre.x - size * 0.5f, centre.y - size * 0.5f, size, size), AirportIcon());
            GUI.color = previous;
        }

        private static Texture2D AirportIcon()
        {
            if (_airportIcon != null)
                return _airportIcon;

            // A plain crossed-runway glyph read as a target/"no entry" mark at the 14-30 px
            // this actually draws at (Bailey screenshot, 2026-09-16) — a clean antialiased dot
            // is unambiguous at any size and still reads as a place, not a plain flat square.
            const int n = 32;
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[n * n];
            var centre = (n - 1) * 0.5f;
            for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
            {
                var dx = (x - centre) / centre;
                var dy = (y - centre) / centre;
                var r = Mathf.Sqrt(dx * dx + dy * dy);
                // A soft-edged disc: one pixel of antialiasing at the rim, solid inside.
                var alpha = Mathf.Clamp01((0.94f - r) / 0.16f);
                pixels[y * n + x] = new Color32(255, 255, 255, (byte)(255f * alpha));
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            _airportIcon = texture;
            return _airportIcon;
        }

        /// <summary>
        /// The planner pane beside the map: which aircraft, where to, when, and the trip it
        /// makes. Scrolls when the HUD is short so Schedule is never pushed off-screen.
        /// </summary>
        private void DrawPlanner(Rect rect, FleetAircraft aircraft, GUIStyle title, GUIStyle label, GUIStyle small, GUIStyle smallButton)
        {
            var closeRect = new Rect(rect.x, rect.yMax - 30f, rect.width, 28f);
            var view = new Rect(rect.x, rect.y, rect.width, rect.height - 38f);
            var needsScroll = _plannerContentHeight > view.height;
            var width = needsScroll ? rect.width - 16f : rect.width;
            _plannerScroll = GUI.BeginScrollView(view, _plannerScroll, new Rect(0f, 0f, width, Mathf.Max(_plannerContentHeight, view.height)));
            var x = 0f;
            var y = 0f;
            var bold = Styled(label, "bold", s => new GUIStyle(s) { fontStyle = FontStyle.Bold });

            GUI.Label(new Rect(x, y, width, 28f), "Flight planner", title);
            y += 32f;

            // ---- Which aircraft
            var fleet = PlayerFleet();
            if (aircraft == null)
            {
                GUI.Label(new Rect(x, y, width, 20f), "No aircraft to plan.", small);
                y += 24f;
            }
            else
            {
                var arrows = fleet.Count > 1;
                var nameX = arrows ? x + 34f : x;
                var nameW = arrows ? width - 68f : width;
                if (arrows && GUI.Button(new Rect(x, y, 28f, 26f), "<", smallButton))
                    SelectAircraft(FlightPlanner.Cycle(fleet, aircraft.Registration, -1));
                DrawSolid(new Rect(nameX, y, 4f, 26f), AirsideTheme.FromHex(aircraft.Airline.LiveryHex));
                var centred = Styled(bold, "middle-center", s => new GUIStyle(s) { alignment = TextAnchor.MiddleCenter });
                GUI.Label(new Rect(nameX, y, nameW, 26f), $"{aircraft.Registration}  ·  {aircraft.Type.Name}", centred);
                if (arrows && GUI.Button(new Rect(x + width - 28f, y, 28f, 26f), ">", smallButton))
                    SelectAircraft(FlightPlanner.Cycle(fleet, aircraft.Registration, 1));
                y += 28f;
                GUI.Label(new Rect(x, y, width, 34f), StatusText(aircraft), small);
                y += 34f;
                y = DrawPlannerAvailabilityHint(x, y, width, aircraft, fleet, small, smallButton);
                if (arrows)
                {
                    GUI.Label(new Rect(x, y, width, 18f), $"[ ] switch aircraft · {fleet.Count} in your fleet", small);
                    y += 20f;
                }
            }

            DrawSolid(new Rect(x, y + 2f, width, 1f), new Color(AirsideTheme.Concrete.r, AirsideTheme.Concrete.g, AirsideTheme.Concrete.b, 0.5f));
            y += 8f;

            if (!_mapSelection.HasValue)
            {
                y = DrawDestinationList(x, y, width, aircraft, label, small);
            }
            else
            {
                y = DrawPlannedTrip(x, y, width, aircraft, _mapSelection.Value, label, bold, small, smallButton);
            }

            if (!string.IsNullOrEmpty(_mapMessage))
            {
                GUI.Label(new Rect(x, y, width, 40f), _mapMessage, small);
                y += 42f;
            }

            if (Event.current.type == EventType.Repaint)
                _plannerContentHeight = y + 8f;
            GUI.EndScrollView();

            if (GUI.Button(closeRect, "Close (Tab)", smallButton))
                TogglePlanner();
        }

        /// <summary>
        /// When the aircraft being planned is busy: point at a free one, or say when this one
        /// is expected back, so the planner never just says "not now".
        /// </summary>
        private float DrawPlannerAvailabilityHint(float x, float y, float width, FleetAircraft aircraft,
            IReadOnlyList<FleetAircraft> fleet, GUIStyle small, GUIStyle smallButton)
        {
            if (aircraft.State == FleetState.AtStand)
                return y;

            var hint = new Rect(x, y, width, 0f);
            var free = FlightPlanner.NextFreeAircraft(fleet, aircraft.Registration);
            if (free != null)
            {
                hint.height = 58f;
                DrawSolid(hint, new Color(AirsideTheme.ClearGreen.r, AirsideTheme.ClearGreen.g, AirsideTheme.ClearGreen.b, 0.16f));
                GUI.Label(new Rect(x + 8f, y + 4f, width - 16f, 18f), $"{free.Registration} is free on {StandNames.Display(free.Stand)}.", small);
                // Copy before the button: SelectAircraft rebuilds the shared fleet list.
                var target = free;
                if (GUI.Button(new Rect(x + 8f, y + 26f, width - 16f, 26f), $"Plan {target.Registration} instead", smallButton))
                    SelectAircraft(target);
                return y + hint.height + 6f;
            }

            var leg = aircraft.CurrentDestination.HasValue ? _operations.AirborneSeconds(aircraft, aircraft.CurrentDestination.Value) : 0L;
            var back = FlightPlanner.ExpectedBackAt(aircraft, leg, _clock.Now);
            var text = back.HasValue && back.Value.CompareTo(_clock.Now) > 0
                ? $"Back over Adelaide about {ClockText(back.Value)} — then choose a stand and plan its next trip."
                : "Nearly home — choose a stand when it lands, then plan its next trip.";
            if (fleet.Count > 1)
                text = "All your aircraft are busy. " + text;
            hint.height = 40f;
            DrawSolid(hint, new Color(AirsideTheme.CoastalBlue.r, AirsideTheme.CoastalBlue.g, AirsideTheme.CoastalBlue.b, 0.18f));
            GUI.Label(new Rect(x + 8f, y + 3f, width - 16f, 36f), text, small);
            return y + hint.height + 6f;
        }

        private float DrawDestinationList(float x, float y, float width, FleetAircraft aircraft, GUIStyle label, GUIStyle small)
        {
            GUI.Label(new Rect(x, y, width, 20f), "Where to?", label);
            y += 20f;
            GUI.Label(new Rect(x, y, width, 18f), "Pick from the list or click a dot on the map.", small);
            y += 22f;

            var right = Styled(small, "upper-right", s => new GUIStyle(s) { alignment = TextAnchor.UpperRight });
            var mouse = Event.current.mousePosition;
            var shownLocked = false;
            foreach (var row in _mapDestinationRows)
            {
                if (!row.Reachable && !shownLocked)
                {
                    shownLocked = true;
                    y += 6f;
                    GUI.Label(new Rect(x, y, width, 18f), "Beyond this aircraft's range", small);
                    y += 20f;
                }

                var cell = new Rect(x, y, width, 34f);
                if (cell.Contains(mouse))
                    DrawSolid(cell, new Color(AirsideTheme.CoastalBlue.r, AirsideTheme.CoastalBlue.g, AirsideTheme.CoastalBlue.b, 0.22f));
                DrawSolid(new Rect(x, y + 6f, 4f, 22f), row.Reachable ? AirsideTheme.ClearGreen : AirsideTheme.Concrete);
                var nameStyle = row.Reachable ? label : small;
                GUI.Label(new Rect(x + 10f, y + 1f, width - 110f, 18f), $"{row.Destination.Code}  {row.Destination.Name}", nameStyle);
                GUI.Label(new Rect(x + 10f, y + 17f, width - 110f, 16f), row.Destination.State, small);
                GUI.Label(new Rect(x + width - 104f, y + 1f, 100f, 16f), $"{row.DistanceKm:0} km", right);
                GUI.Label(new Rect(x + width - 104f, y + 17f, 100f, 16f),
                    row.Reachable ? DurationText(row.AirborneSeconds) : "locked", right);
                if (GUI.Button(cell, GUIContent.none, GUIStyle.none))
                    PickDestination(row.Destination);
                y += 36f;
            }

            return y;
        }

        private float DrawPlannedTrip(float x, float y, float width, FleetAircraft aircraft, Destination destination,
            GUIStyle label, GUIStyle bold, GUIStyle small, GUIStyle smallButton)
        {
            if (GUI.Button(new Rect(x, y, 150f, 24f), "< All destinations", smallButton))
            {
                _mapSelection = null;
                _mapMessage = null;
            }
            y += 30f;

            var km = _operations.DistanceKm(destination);
            GUI.Label(new Rect(x, y, width, 22f), $"{destination.Name}, {destination.State}", bold);
            y += 22f;

            if (aircraft == null)
                return y;

            if (!_operations.CanReach(aircraft, destination))
            {
                GUI.Label(new Rect(x, y, width, 20f), $"{km:0} km from Adelaide", small);
                y += 22f;
                GUI.Label(new Rect(x, y, width, 54f),
                    $"Locked — beyond the {aircraft.Type.Name}'s {aircraft.Type.PracticalRangeKm:0} km range. A longer-range aircraft will open it.", small);
                return y + 56f;
            }

            var airborne = _operations.AirborneSeconds(aircraft, destination);
            GUI.Label(new Rect(x, y, width, 20f), $"{km:0} km · {DurationText(airborne)} each way", small);
            y += 26f;

            if (aircraft.State != FleetState.AtStand)
            {
                GUI.Label(new Rect(x, y, width, 48f),
                    $"{aircraft.Registration} can be planned once it is parked on a stand at Adelaide.", small);
                return y + 50f;
            }

            // ---- When
            _departureDelaySeconds = FlightPlanner.ClampDelay(_departureDelaySeconds);
            var departAt = _clock.Now.Advance(_departureDelaySeconds);
            GUI.Label(new Rect(x, y, width, 20f), $"Pushback {ClockText(departAt)}  ·  in {DurationText(_departureDelaySeconds)}", label);
            y += 24f;

            var chips = FlightPlanner.QuickDepartures;
            var perRow = 3;
            var chipW = (width - (perRow - 1) * 4f) / perRow;
            for (var i = 0; i < chips.Length; i++)
            {
                var cell = new Rect(x + i % perRow * (chipW + 4f), y + i / perRow * 30f, chipW, 26f);
                if (chips[i].seconds == _departureDelaySeconds)
                    AirsideTheme.DrawPanelFrame(new Rect(cell.x - 2f, cell.y - 2f, cell.width + 4f, cell.height + 4f), AirsideTheme.SafetyYellow);
                if (GUI.Button(cell, chips[i].label, smallButton))
                    _departureDelaySeconds = chips[i].seconds;
            }
            y += (chips.Length + perRow - 1) / perRow * 30f + 2f;

            var stepW = (width - 4f) / 2f;
            if (GUI.Button(new Rect(x, y, stepW, 24f), "- 5 min", smallButton))
                _departureDelaySeconds = FlightPlanner.StepDelay(_departureDelaySeconds, -1);
            if (GUI.Button(new Rect(x + stepW + 4f, y, stepW, 24f), "+ 5 min", smallButton))
                _departureDelaySeconds = FlightPlanner.StepDelay(_departureDelaySeconds, 1);
            y += 32f;

            // ---- The trip it makes
            var trip = FlightPlanner.Estimate(aircraft, airborne, departAt);
            var timeStyle = Styled(small, "upper-right", s => new GUIStyle(s) { alignment = TextAnchor.UpperRight });
            var legs = new (string what, SimulationTime at)[]
            {
                ($"Pushback from {StandNames.Display(aircraft.Stand)}", trip.DepartStand),
                ("Airborne from Adelaide", trip.Airborne),
                ($"Lands {destination.Code}", trip.ArriveDestination),
                ($"Departs {destination.Code}", trip.LeaveDestination),
                ("Back at Adelaide (about)", trip.BackAtAdelaide)
            };
            var box = new Rect(x, y, width, legs.Length * 19f + 10f);
            DrawSolid(box, new Color(AirsideTheme.Tarmac.r, AirsideTheme.Tarmac.g, AirsideTheme.Tarmac.b, 0.6f));
            for (var i = 0; i < legs.Length; i++)
            {
                GUI.Label(new Rect(x + 8f, y + 5f + i * 19f, width - 70f, 18f), legs[i].what, small);
                GUI.Label(new Rect(x + width - 68f, y + 5f + i * 19f, 60f, 18f), ClockText(legs[i].at), timeStyle);
            }
            y += box.height + 8f;

            var booked = aircraft.Scheduled;
            var scheduleRect = new Rect(x, y, width, 34f);
            if (IsGuided(aircraft, GuideStep.PlanFirstFlight))
                DrawGuideHighlight(scheduleRect);
            var verb = booked.HasValue ? "Update plan" : "Schedule";
            if (GUI.Button(scheduleRect, $"{verb}: {aircraft.Registration} to {destination.Code}", smallButton))
            {
                var result = _operations.ScheduleDeparture(aircraft, destination, departAt);
                if (result.Accepted)
                {
                    ShowToast($"{aircraft.Registration} pushes back {ClockText(departAt)} for {destination.Name}.");
                    _activeWorkspace = HudWorkspace.None;
                    _mapSelection = null;
                    SaveAirline();
                }
                else
                {
                    _mapMessage = result.Reason;
                }
            }
            y += 40f;

            if (booked.HasValue)
            {
                GUI.Label(new Rect(x, y, width, 34f),
                    $"Booked now: {booked.Value.Destination.Name} at {ClockText(booked.Value.DepartAt)}.", small);
                y += 34f;
                if (GUI.Button(new Rect(x, y, width, 26f), "Cancel booked flight", smallButton))
                    CancelPlannedFlight(aircraft);
                y += 32f;
            }

            return y;
        }

        private void PickDestination(Destination destination)
        {
            _mapSelection = destination;
            _mapMessage = null;
            _plannerScroll = Vector2.zero;
            PlayUiClick();
        }

        private Vector2 Project(Rect area, double longitude, double latitude)
        {
            _mapLens.Project(area.x, area.y, area.width, area.height, longitude, latitude, out var x, out var y);
            return new Vector2(x, y);
        }

        /// <summary>
        /// Scroll zooms. A left press becomes a pan only after it moves past the drag
        /// threshold; released before that it is a click, which picks the nearest
        /// aircraft or destination dot. (Consuming every press to start panning is what
        /// made destinations so hard to click.)
        /// </summary>
        private void HandleMapPointer(Rect mapRect)
        {
            var ev = Event.current;
            if (ev == null)
                return;
            var over = mapRect.Contains(ev.mousePosition);
            foreach (var control in _mapControlRects)
                if (control.Contains(ev.mousePosition))
                    over = false;
            if (over && ev.type == EventType.ScrollWheel)
            {
                // Queue the zoom in log space and ease it in over a few frames: a trackpad sends
                // dozens of scroll events a second, and applying each one outright made the map
                // leap. The step scales with how far the wheel actually moved.
                _mapZoomPending = Mathf.Clamp(_mapZoomPending + MapZoom.StepFor(ev.delta.y), -MapZoom.MaxPending, MapZoom.MaxPending);
                _mapZoomAnchor = ev.mousePosition - mapRect.position;
                ev.Use();
                return;
            }

            if (ev.button != 0)
                return;

            switch (ev.type)
            {
                case EventType.MouseDown when over:
                    _mapPressed = true;
                    _mapPanning = false;
                    _mapPressGui = ev.mousePosition;
                    _mapPanGui = ev.mousePosition;
                    ev.Use();
                    break;

                case EventType.MouseDrag when _mapPressed:
                    if (!_mapPanning && !AircraftPickRouting.CountsAsClick(_mapPressGui.x, _mapPressGui.y, ev.mousePosition.x, ev.mousePosition.y))
                    {
                        _mapPanning = true;
                        _mapTrackId = null;
                    }
                    if (_mapPanning)
                    {
                        _mapLens.PanByGuiDelta(mapRect.width, mapRect.height,
                            _mapPanGui.x - mapRect.x, _mapPanGui.y - mapRect.y,
                            ev.mousePosition.x - mapRect.x, ev.mousePosition.y - mapRect.y);
                        _mapPanGui = ev.mousePosition;
                    }
                    ev.Use();
                    break;

                case EventType.MouseUp when _mapPressed:
                    var wasClick = !_mapPanning;
                    _mapPressed = false;
                    _mapPanning = false;
                    if (!wasClick)
                    {
                        ev.Use();
                        break;
                    }

                    // Only what is drawn can be clicked: zoomed in, a dot or plane just past the
                    // map edge is culled from the drawing but was still inside the hit radius.
                    var aircraftHit = FlightPlanner.NearestWithin(_mapAircraftPoints, ev.mousePosition.x, ev.mousePosition.y, 18f);
                    if (aircraftHit >= 0 && mapRect.Contains(new Vector2(_mapAircraftPoints[aircraftHit].x, _mapAircraftPoints[aircraftHit].y)))
                    {
                        SelectAircraft(_mapAircraftRows[aircraftHit]);
                        ev.Use();
                        break;
                    }

                    var destinationHit = FlightPlanner.NearestWithin(_mapDestinationPoints, ev.mousePosition.x, ev.mousePosition.y);
                    if (destinationHit >= 0
                        && mapRect.Contains(new Vector2(_mapDestinationPoints[destinationHit].x, _mapDestinationPoints[destinationHit].y)))
                        PickDestination(_mapDestinationRows[destinationHit].Destination);
                    ev.Use();
                    break;
            }
        }

        private void DrawAustraliaBase(Rect mapRect)
        {
            var coast = new Color(AirsideTheme.Sand.r, AirsideTheme.Sand.g, AirsideTheme.Sand.b, 0.85f);
            var border = new Color(AirsideTheme.Concrete.r, AirsideTheme.Concrete.g, AirsideTheme.Concrete.b, 0.55f);
            DrawLonLatPolyline(mapRect, AustraliaMapGeometry.MainlandCoastLonLat, coast, 2f);
            DrawLonLatPolyline(mapRect, AustraliaMapGeometry.TasmaniaCoastLonLat, coast, 2f);
            foreach (var borderLine in AustraliaMapGeometry.StateBorderLonLats)
                DrawLonLatPolyline(mapRect, borderLine, border, 1.2f);

            if (_mapLens.ShowStateLabels)
            {
                var stateStyle = Styled(GUI.skin.label, "map-state", s => AirsideTheme.TextStyle(new GUIStyle(s)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                }, AirsideTheme.OpenSky));
                foreach (var (code, lon, lat) in AustraliaMapGeometry.StateLabels)
                {
                    var p = Project(mapRect, lon, lat);
                    if (mapRect.Contains(p))
                        GUI.Label(new Rect(p.x - 18f, p.y - 9f, 36f, 18f), code, stateStyle);
                }
            }

            if (_mapLens.ShowCountyDetail)
            {
                var regionStyle = Styled(GUI.skin.label, "map-region", s => AirsideTheme.TextStyle(new GUIStyle(s)
                {
                    fontSize = 10,
                    alignment = TextAnchor.MiddleCenter
                }, AirsideTheme.Cloud));
                foreach (var (name, lon, lat) in AustraliaMapGeometry.RegionLabels)
                {
                    var p = Project(mapRect, lon, lat);
                    if (mapRect.Contains(p))
                        GUI.Label(new Rect(p.x - 54f, p.y - 8f, 108f, 16f), name, regionStyle);
                }
            }

            var hint = Styled(GUI.skin.label, "map-hint", s => AirsideTheme.TextStyle(new GUIStyle(s) { fontSize = 11 }, AirsideTheme.Concrete));
            GUI.Label(new Rect(mapRect.x + 6f, mapRect.yMax - 20f, mapRect.width - 12f, 18f),
                "Scroll to zoom · drag to pan · click a destination, or a plane to track it", hint);
        }

        private void DrawLonLatPolyline(Rect area, float[] lonLat, Color colour, float thickness)
        {
            var count = AustraliaMapGeometry.PointCount(lonLat);
            for (var i = 1; i < count; i++)
            {
                var i0 = (i - 1) * 2;
                var i1 = i * 2;
                DrawClippedLine(area,
                    Project(area, lonLat[i0], lonLat[i0 + 1]),
                    Project(area, lonLat[i1], lonLat[i1 + 1]),
                    colour, thickness);
            }
        }


        private void DrawFlightsPanel(Rect rect, GUIStyle panel, GUIStyle title, GUIStyle label, GUIStyle small, GUIStyle smallButton)
        {
            var ink = AirsideTheme.RunwayInk;
            DrawSolid(rect, new Color(ink.r, ink.g, ink.b, 0.96f));
            GUI.Box(rect, GUIContent.none, panel);

            var x = rect.x + 16f;
            var inner = rect.width - 32f;
            GUI.Label(new Rect(x, rect.y + 10f, inner - 120f, 26f), "Adelaide flights", title);
            if (GUI.Button(new Rect(rect.xMax - 108f, rect.y + 10f, 92f, 26f), "Close", smallButton))
                SetWorkspace(HudWorkspace.Operations);

            GUI.Label(new Rect(x, rect.y + 40f, inner, 18f),
                $"RUNWAY {RunwayWeather.Label(_operations.ActiveRunway)}  ·  WIND {_operations.Wind.Text}", small);

            var tabY = rect.y + 62f;
            GUI.color = _flightsShowArrivals ? AirsideTheme.SafetyYellow : Color.white;
            if (GUI.Button(new Rect(x, tabY, 132f, 27f), "ARRIVALS", smallButton))
                _flightsShowArrivals = true;
            GUI.color = !_flightsShowArrivals ? AirsideTheme.SafetyYellow : Color.white;
            if (GUI.Button(new Rect(x + 140f, tabY, 132f, 27f), "DEPARTURES", smallButton))
                _flightsShowArrivals = false;
            GUI.color = Color.white;

            if (_flightsBoardRowsFrame != Time.frameCount || _flightsBoardRowsForArrivals != _flightsShowArrivals)
            {
                _flightsBoardRows.Clear();
                foreach (var aircraft in _operations.Fleet)
                    if (_flightsShowArrivals ? FlightBoard.IsArrival(aircraft) : FlightBoard.IsDeparture(aircraft))
                        _flightsBoardRows.Add(aircraft);
                FlightBoard.Sort(_flightsBoardRows);
                _flightsBoardRowsFrame = Time.frameCount;
                _flightsBoardRowsForArrivals = _flightsShowArrivals;
            }

            var headerY = rect.y + 98f;
            var schedW = 58f;
            var estimateW = 58f;
            var gateW = 48f;
            var routeW = Mathf.Min(118f, inner * 0.20f);
            var phaseW = 112f;
            GUI.Label(new Rect(x, headerY, schedW, 18f), "SCHED", small);
            GUI.Label(new Rect(x + schedW, headerY, estimateW, 18f), "EST", small);
            GUI.Label(new Rect(x + schedW + estimateW, headerY, gateW, 18f), "GATE", small);
            GUI.Label(new Rect(x + schedW + estimateW + gateW, headerY, routeW, 18f), "FLIGHT", small);
            GUI.Label(new Rect(x + schedW + estimateW + gateW + routeW, headerY, phaseW, 18f), "STATUS", small);
            GUI.Label(new Rect(x + schedW + estimateW + gateW + routeW + phaseW, headerY,
                inner - schedW - estimateW - gateW - routeW - phaseW, 18f), "AIRCRAFT / OPERATOR", small);

            var view = new Rect(x, headerY + 22f, inner, rect.height - (headerY - rect.y) - 36f);
            var rowHeight = 52f;
            var contentHeight = 8f + _flightsBoardRows.Count * rowHeight;
            if (_toasts.History.Count > 0)
                contentHeight += 34f + _toasts.History.Count * 20f;
            _flightsScroll = GUI.BeginScrollView(view, _flightsScroll, new Rect(0f, 0f, inner - 18f, contentHeight));
            var y = 4f;
            var rowWidth = inner - 22f;
            var boardBold = Styled(label, "bold", st => new GUIStyle(st) { fontStyle = FontStyle.Bold });
            var boardTiny = Styled(small, "board-tiny", st => AirsideTheme.TextStyle(new GUIStyle(st) { fontSize = 10, wordWrap = false }, AirsideTheme.Concrete));
            var rowIndex = 0;
            foreach (var aircraft in _flightsBoardRows)
            {
                var row = new Rect(0f, y, rowWidth, rowHeight - 4f);
                var mine = aircraft.Airline.IsPlayer;
                var previousColour = GUI.color;
                var selected = _selectedAircraftId == aircraft.Registration;
                if (selected)
                {
                    DrawSolid(row, new Color(AirsideTheme.CoastalBlue.r, AirsideTheme.CoastalBlue.g, AirsideTheme.CoastalBlue.b, 0.28f));
                    AirsideTheme.DrawPanelFrame(row, AirsideTheme.SafetyYellow);
                }
                else if (row.Contains(Event.current.mousePosition))
                {
                    DrawSolid(row, new Color(AirsideTheme.CoastalBlue.r, AirsideTheme.CoastalBlue.g, AirsideTheme.CoastalBlue.b, 0.14f));
                }
                else if ((rowIndex & 1) == 1)
                {
                    DrawSolid(row, new Color(1f, 1f, 1f, 0.025f));
                }

                DrawSolid(new Rect(row.x, row.y, 4f, row.height), AirsideTheme.FromHex(aircraft.Airline.LiveryHex));
                if (!mine && !selected)
                    GUI.color = new Color(1f, 1f, 1f, Ownership.AlphaFor(aircraft.Airline));
                GUI.Label(new Rect(10f, y + 5f, schedW - 8f, 18f), FlightBoard.ScheduledTime(aircraft, ClockText), boardBold);
                GUI.Label(new Rect(schedW, y + 5f, estimateW - 4f, 18f), FlightBoard.EstimatedTime(aircraft, ClockText), label);
                GUI.Label(new Rect(schedW + estimateW, y + 5f, gateW - 4f, 18f), FlightBoard.GateText(aircraft), label);
                var routeX = schedW + estimateW + gateW;
                GUI.Label(new Rect(routeX, y + 5f, routeW - 4f, 20f), FlightBoard.RouteText(aircraft), boardBold);
                var boardSeverity = AircraftStatus.Severity(aircraft, _clock.Now);
                var boardContent = GUI.contentColor;
                if (boardSeverity != StatusSeverity.Normal)
                    GUI.contentColor = SeverityColour(boardSeverity, boardContent);
                var boardFlightNumber = FlightNumber.ForAircraft(aircraft);
                var boardIdentity = boardFlightNumber != null
                    ? $"{boardFlightNumber} · {aircraft.Registration}"
                    : aircraft.Registration;
                GUI.Label(new Rect(routeX, y + 25f, routeW - 4f, 16f), boardIdentity, boardTiny);
                GUI.Label(new Rect(routeX + routeW, y + 6f, phaseW - 4f, 20f), FlightBoard.PhaseLabel(aircraft, _clock.Now), label);
                GUI.contentColor = boardContent;
                var aircraftX = routeX + routeW + phaseW;
                GUI.Label(new Rect(aircraftX, y + 5f, rowWidth - aircraftX - 8f, 18f),
                    $"{aircraft.Type.Name}{EnrouteAltitudeText(aircraft)}", small);
                GUI.Label(new Rect(aircraftX, y + 24f, rowWidth - aircraftX - 8f, 16f), aircraft.Airline.Name, boardTiny);

                if (aircraft.StateEndsAt.HasValue)
                {
                    AirsideTheme.DrawProgressBar(new Rect(schedW, y + 40f, rowWidth - schedW - 12f, 4f),
                        (float)aircraft.StateProgress(_clock.Now),
                        mine ? AirsideTheme.FromHex(aircraft.Airline.LiveryHex) : AirsideTheme.Concrete, AirsideTheme.Tarmac);
                }
                else if (AircraftStatus.IsWaiting(aircraft))
                {
                    AirsideTheme.DrawProgressBar(new Rect(schedW, y + 40f, rowWidth - schedW - 12f, 4f),
                        AircraftStatus.WaitProgress(aircraft, _clock.Now),
                        SeverityColour(boardSeverity, AirsideTheme.Concrete), AirsideTheme.Tarmac);
                }

                GUI.color = previousColour;
                if (GUI.Button(row, GUIContent.none, GUIStyle.none))
                {
                    SelectAircraft(aircraft);
                }

                y += rowHeight;
                rowIndex++;
            }

            if (_toasts.History.Count > 0)
            {
                // Messages scroll past in a few seconds; the latest few stay readable here.
                y += 8f;
                GUI.Label(new Rect(0f, y, rowWidth, 22f), "RECENT MESSAGES", boardBold);
                y += 26f;
                foreach (var entry in _toasts.History)
                {
                    var ago = Mathf.Max(0, Mathf.RoundToInt((Time.unscaledTime - entry.ShownAt) / 60f));
                    GUI.Label(new Rect(0f, y, 64f, 20f), ago == 0 ? "now" : $"{ago} min", small);
                    GUI.Label(new Rect(64f, y, rowWidth - 64f, 20f), entry.Message, small);
                    y += 20f;
                }
            }

            GUI.EndScrollView();
        }


        private void DrawDevToolsPanel(Rect rect, GUIStyle panel, GUIStyle title, GUIStyle label, GUIStyle small, GUIStyle smallButton)
        {
            var ink = AirsideTheme.RunwayInk;
            DrawSolid(rect, new Color(ink.r, ink.g, ink.b, 0.96f));
            GUI.Box(rect, GUIContent.none, panel);
            // Diagnostic overlay, not a player workspace (ADR 0053): a distinct accent and
            // badge keep it from reading as one more tab beside Operations/Map/Fleet/Contracts.
            AirsideTheme.DrawPanelFrame(rect, AirsideTheme.SignalRed);

            var x = rect.x + 16f;
            var inner = rect.width - 32f;
            GUI.Label(new Rect(x, rect.y + 10f, inner - 174f, 26f), "Dev tools", title);
            GUI.Label(new Rect(rect.xMax - 172f, rect.y + 16f, 50f, 20f), "DEV", Styled(small, "devBadge", st =>
                AirsideTheme.TextStyle(new GUIStyle(st) { fontStyle = FontStyle.Bold }, AirsideTheme.SignalRed)));
            if (GUI.Button(new Rect(rect.xMax - 108f, rect.y + 10f, 92f, 26f), "Close", smallButton))
                ToggleDevTools();

            GUI.Label(new Rect(x, rect.y + 40f, inner, 18f),
                "Playtest helpers — inspect the fleet, auto-schedule idle aircraft, park arrivals. Live time stays on.", small);

            var next = _operations.NextEventAt();
            GUI.Label(new Rect(x, rect.y + 62f, inner, 18f), DevTools.NextEventLabel(next, ClockText), label);

            var idle = DevTools.CountIdleAtStand(_operations.Fleet);
            var waiting = DevTools.CountAwaitingStand(_operations.Fleet);
            var free = 0;
            foreach (var _ in _operations.FreeStands())
                free++;
            GUI.Label(new Rect(x, rect.y + 84f, inner, 18f),
                $"Idle at stand: {idle}  ·  Awaiting stand: {waiting}  ·  Free stands: {free}", small);

            var buttonY = rect.y + 108f;
            if (GUI.Button(new Rect(x, buttonY, 210f, 28f), "Auto-schedule idle player", smallButton))
                DevToolsAutoSchedulePlayer();
            if (GUI.Button(new Rect(x + 220f, buttonY, 180f, 28f), "Assign free stands", smallButton))
                DevToolsAssignStands();

            var view = new Rect(x, buttonY + 40f, inner, rect.height - (buttonY + 40f - rect.y) - 14f);
            var contentHeight = 8f + _operations.Fleet.Count * 28f;
            _devToolsScroll = GUI.BeginScrollView(view, _devToolsScroll, new Rect(0f, 0f, inner - 18f, contentHeight));
            var y = 4f;
            foreach (var aircraft in _operations.Fleet)
            {
                var row = new Rect(0f, y, inner - 22f, 24f);
                var selected = _selectedAircraftId == aircraft.Registration;
                if (selected)
                    DrawSolid(row, new Color(AirsideTheme.CoastalBlue.r, AirsideTheme.CoastalBlue.g, AirsideTheme.CoastalBlue.b, 0.28f));
                else if (row.Contains(Event.current.mousePosition))
                    DrawSolid(row, new Color(AirsideTheme.CoastalBlue.r, AirsideTheme.CoastalBlue.g, AirsideTheme.CoastalBlue.b, 0.14f));

                GUI.Label(new Rect(8f, y + 2f, inner - 36f, 20f), DevTools.FleetLine(aircraft), small);
                if (GUI.Button(row, GUIContent.none, GUIStyle.none))
                    SelectAircraft(aircraft);
                y += 28f;
            }

            GUI.EndScrollView();
        }

        private void DevToolsAutoSchedulePlayer()
        {
            var player = _operations.PlayerAirline;
            if (player == null)
                return;
            var scheduled = 0;
            foreach (var aircraft in _operations.FleetOf(player))
            {
                if (aircraft.State != FleetState.AtStand || aircraft.Scheduled.HasValue)
                    continue;
                var reachable = new List<Destination>();
                foreach (var destination in _operations.MapDestinations())
                    if (_operations.CanReach(aircraft, destination))
                        reachable.Add(destination);
                if (reachable.Count == 0)
                    continue;
                var pick = reachable[_devToolsRandom.NextInt(0, reachable.Count)];
                var delay = DevTools.AutoScheduleDelaySeconds(aircraft.CompletedTrips,
                    _devToolsRandom.NextInt(0, DevTools.LaterAutoDepartureLeadMaxSeconds));
                var result = _operations.ScheduleDeparture(aircraft, pick, _clock.Now.Advance(delay));
                if (result.Accepted)
                    scheduled++;
            }

            ShowToast(scheduled > 0
                ? $"Auto-scheduled {scheduled} player aircraft."
                : "No idle player aircraft to schedule.");
            PlayUiClick();
        }

        private void DevToolsAssignStands()
        {
            var assigned = 0;
            foreach (var aircraft in _operations.Fleet)
            {
                if (aircraft.State != FleetState.AwaitingStand)
                    continue;
                foreach (var stand in _operations.FreeStands())
                {
                    var result = _operations.AssignStand(aircraft, stand);
                    if (result.Accepted)
                    {
                        assigned++;
                        break;
                    }
                }
            }

            ShowToast(assigned > 0
                ? $"Assigned {assigned} aircraft to free stands."
                : "No aircraft waiting for a stand.");
            PlayUiClick();
        }


        private int _hangarTab;

        private void DrawHangarPanel(Rect rect, GUIStyle panel, GUIStyle title, GUIStyle label, GUIStyle small, GUIStyle smallButton)
        {
            var ink = AirsideTheme.RunwayInk;
            DrawSolid(rect, new Color(ink.r, ink.g, ink.b, 0.96f));
            GUI.Box(rect, GUIContent.none, panel);

            var x = rect.x + 16f;
            var inner = rect.width - 32f;
            GUI.Label(new Rect(x, rect.y + 10f, inner - 120f, 26f), "Hangar", title);
            if (GUI.Button(new Rect(rect.xMax - 108f, rect.y + 10f, 92f, 26f), "Close", smallButton))
                SetWorkspace(HudWorkspace.Fleet);

            var tabs = new[] { "Fleet", "Aircraft types" };
            for (var i = 0; i < tabs.Length; i++)
            {
                var tab = new Rect(x + i * 134f, rect.y + 42f, 128f, 24f);
                if (i == _hangarTab)
                    AirsideTheme.DrawPanelFrame(new Rect(tab.x - 2f, tab.y - 2f, tab.width + 4f, tab.height + 4f), AirsideTheme.SafetyYellow);
                if (GUI.Button(tab, tabs[i], smallButton) && _hangarTab != i)
                {
                    _hangarTab = i;
                    _hangarScroll = Vector2.zero;
                    PlayUiClick();
                }
            }

            var view = new Rect(x, rect.y + 76f, inner, rect.height - 90f);
            if (_hangarTab == 1)
                DrawHangarTypes(view, label, small);
            else
                DrawHangarFleet(view, label, small);
        }

        private readonly List<FleetAircraft> _hangarMine = new();
        private readonly List<Airline> _hangarOthers = new();
        private readonly Dictionary<Airline, List<FleetAircraft>> _hangarOtherFleets = new();

        private void DrawHangarFleet(Rect view, GUIStyle label, GUIStyle small)
        {
            var player = _operations.PlayerAirline;
            // Reused: OnGUI runs this several times a frame while the hangar is open.
            // One pass over the whole fleet here, instead of calling FleetOf(airline) once
            // per airline for the content-height total and again to draw the rows — each
            // FleetOf call itself scans the entire fleet, so that used to be two full
            // fleet scans per airline, every OnGUI invocation.
            var mine = _hangarMine;
            var others = _hangarOthers;
            mine.Clear();
            others.Clear();
            foreach (var pair in _hangarOtherFleets)
                pair.Value.Clear();
            foreach (var aircraft in _operations.Fleet)
            {
                if (ReferenceEquals(aircraft.Airline, player))
                {
                    mine.Add(aircraft);
                    continue;
                }
                if (!_hangarOtherFleets.TryGetValue(aircraft.Airline, out var list))
                {
                    list = new List<FleetAircraft>();
                    _hangarOtherFleets[aircraft.Airline] = list;
                }
                list.Add(aircraft);
            }
            foreach (var airline in _operations.Airlines)
                if (!airline.IsPlayer)
                    others.Add(airline);

            const float rowHeight = 72f;
            var contentHeight = 8f + 30f + mine.Count * rowHeight + 34f;
            foreach (var airline in others)
            {
                contentHeight += 22f;
                if (_hangarOtherFleets.TryGetValue(airline, out var fleet))
                    contentHeight += fleet.Count * 60f;
            }

            var inner = view.width;
            _hangarScroll = GUI.BeginScrollView(view, _hangarScroll, new Rect(0f, 0f, inner - 18f, contentHeight));
            var rowWidth = inner - 22f;
            var y = 4f;
            var bold = Styled(label, "bold", s => new GUIStyle(s) { fontStyle = FontStyle.Bold });

            DrawOwnershipHeader(0f, y, rowWidth, Ownership.PlayerSection, player.LiveryHex, bold);
            y += 30f;
            foreach (var aircraft in mine)
            {
                DrawHangarRow(aircraft, new Rect(0f, y, rowWidth, rowHeight - 6f), label, small, quiet: false);
                y += rowHeight;
            }

            y += 8f;
            DrawOwnershipHeader(0f, y, rowWidth, Ownership.OtherSection, null, bold);
            y += 26f;
            foreach (var airline in others)
            {
                var previous = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, Ownership.OtherAlpha);
                DrawSolid(new Rect(0f, y + 5f, 6f, 10f), AirsideTheme.FromHex(airline.LiveryHex));
                GUI.Label(new Rect(12f, y, rowWidth - 12f, 20f), airline.Name, small);
                GUI.color = previous;
                y += 22f;
                if (_hangarOtherFleets.TryGetValue(airline, out var fleet))
                {
                    foreach (var aircraft in fleet)
                    {
                        DrawHangarRow(aircraft, new Rect(0f, y, rowWidth, 54f), label, small, quiet: true);
                        y += 60f;
                    }
                }
            }

            GUI.EndScrollView();
        }

        /// <summary>Section heading shared by every player/AI split (<see cref="Ownership"/>).</summary>
        private static void DrawOwnershipHeader(float x, float y, float width, string text, string liveryHex, GUIStyle style)
        {
            var colour = liveryHex != null ? AirsideTheme.FromHex(liveryHex) : AirsideTheme.Concrete;
            DrawSolid(new Rect(x, y + 3f, 4f, 16f), colour);
            GUI.Label(new Rect(x + 10f, y, width - 10f, 22f), text, style);
            DrawSolid(new Rect(x, y + 23f, width, 1f), new Color(colour.r, colour.g, colour.b, 0.45f));
        }

        /// <summary>The player's livery pill with the ownership badge, right-aligned in a row.</summary>
        private void DrawOwnershipBadge(Rect row, Airline airline, GUIStyle small)
        {
            var badge = Ownership.BadgeFor(airline);
            if (badge == null)
                return;
            var livery = AirsideTheme.FromHex(airline.LiveryHex);
            var pill = new Rect(row.xMax - 58f, row.y + 4f, 52f, 16f);
            DrawSolid(pill, livery);
            var style = Styled(small, "badge", s => new GUIStyle(s) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 10, wordWrap = false });
            GUI.Label(pill, badge, style);
        }

        private void DrawHangarRow(FleetAircraft aircraft, Rect row, GUIStyle label, GUIStyle small, bool quiet)
        {
            var selected = _selectedAircraftId == aircraft.Registration;
            var livery = AirsideTheme.FromHex(aircraft.Airline.LiveryHex);
            if (selected)
            {
                DrawSolid(row, new Color(AirsideTheme.CoastalBlue.r, AirsideTheme.CoastalBlue.g, AirsideTheme.CoastalBlue.b, 0.28f));
                AirsideTheme.DrawPanelFrame(row, AirsideTheme.SafetyYellow);
            }
            else if (row.Contains(Event.current.mousePosition))
            {
                DrawSolid(row, new Color(AirsideTheme.CoastalBlue.r, AirsideTheme.CoastalBlue.g, AirsideTheme.CoastalBlue.b, 0.14f));
            }

            var previous = GUI.color;
            if (quiet && !selected)
                GUI.color = new Color(1f, 1f, 1f, Ownership.AlphaFor(aircraft.Airline));
            if (!quiet)
                DrawSolid(new Rect(row.x, row.y, 5f, row.height), livery);

            var textX = row.x + (quiet ? 10f : 14f);
            var textWidth = row.width - (textX - row.x) - 66f;
            GUI.Label(new Rect(textX, row.y + 4f, textWidth, 20f), $"{aircraft.Registration}  ·  {aircraft.Type.Name}", quiet ? small : label);
            GUI.Label(new Rect(textX, row.y + (quiet ? 20f : 26f), row.width - (textX - row.x) - 10f, 18f), StatusText(aircraft), small);
            if (aircraft.StateEndsAt.HasValue)
                AirsideTheme.DrawProgressBar(new Rect(textX, row.yMax - 10f, row.width - (textX - row.x) - 10f, 5f),
                    (float)aircraft.StateProgress(_clock.Now), quiet ? AirsideTheme.Concrete : livery, AirsideTheme.Tarmac);
            GUI.color = previous;
            DrawOwnershipBadge(row, aircraft.Airline, small);

            if (GUI.Button(row, GUIContent.none, GUIStyle.none))
                SelectAircraft(aircraft);
        }

        private void DrawHangarTypes(Rect view, GUIStyle label, GUIStyle small)
        {
            const float cardHeight = 148f;
            var types = AircraftCatalogue.All;
            var inner = view.width;
            _hangarScroll = GUI.BeginScrollView(view, _hangarScroll, new Rect(0f, 0f, inner - 18f, 8f + types.Count * (cardHeight + 10f)));
            var width = inner - 22f;
            var bold = Styled(label, "bold", s => new GUIStyle(s) { fontStyle = FontStyle.Bold });
            var y = 4f;
            foreach (var spec in types)
            {
                DrawAircraftTypeCard(spec, new Rect(0f, y, width, cardHeight), bold, small);
                y += cardHeight + 10f;
            }

            GUI.EndScrollView();
        }

        /// <summary>One catalogue card: a thumbnail rendered from the runtime model, or a clearly labelled placeholder.</summary>
        private void DrawAircraftTypeCard(AircraftSpec spec, Rect card, GUIStyle bold, GUIStyle small)
        {
            DrawSolid(card, new Color(AirsideTheme.Tarmac.r, AirsideTheme.Tarmac.g, AirsideTheme.Tarmac.b, 0.55f));
            var thumbRect = new Rect(card.x + 8f, card.y + 8f, 186f, 124f);
            var thumb = spec.ModelStatus == ModelStatus.Genuine
                ? AirsideArtTextures.Load(spec.ThumbnailPath, wrap: TextureWrapMode.Clamp)
                : null;
            if (thumb != null)
            {
                GUI.DrawTexture(thumbRect, thumb, ScaleMode.ScaleToFit, alphaBlend: true);
            }
            else
            {
                DrawSolid(thumbRect, new Color(0f, 0f, 0f, 0.25f));
                AirsideTheme.DrawPanelFrame(thumbRect, AirsideTheme.Concrete);
                var centred = Styled(small, "placeholder", s => new GUIStyle(s) { alignment = TextAnchor.MiddleCenter, wordWrap = true });
                GUI.Label(new Rect(thumbRect.x + 8f, thumbRect.y, thumbRect.width - 16f, thumbRect.height),
                    spec.ModelStatus == ModelStatus.Placeholder
                        ? "PLACEHOLDER\nNo genuine model yet — flies with a stand-in"
                        : "Thumbnail unavailable", centred);
            }

            var tx = thumbRect.xMax + 12f;
            var tw = card.xMax - tx - 8f;
            GUI.Label(new Rect(tx, card.y + 6f, tw, 20f), spec.Name, bold);
            GUI.Label(new Rect(tx, card.y + 26f, tw, 18f), spec.Role, small);
            GUI.Label(new Rect(tx, card.y + 46f, tw, 18f),
                $"Length {spec.LengthMetres:0.0} m  ·  Span {spec.WingspanMetres:0.0} m  ·  Height {spec.HeightMetres:0.0} m", small);
            GUI.Label(new Rect(tx, card.y + 64f, tw, 18f),
                $"Cruise {spec.PlanningCruiseKmh:0} km/h  ·  Planning range {spec.PracticalRangeKm:#,0} km", small);
            GUI.Label(new Rect(tx, card.y + 82f, tw, 18f), $"Uses: {spec.StandClassLabel}", small);

            var here = 0;
            var yours = 0;
            foreach (var aircraft in _operations.Fleet)
            {
                if (aircraft.Type.Id != spec.Id)
                    continue;
                here++;
                if (aircraft.Airline.IsPlayer)
                    yours++;
            }

            GUI.Label(new Rect(tx, card.y + 100f, tw, 18f),
                here == 0 ? "Not flying at Adelaide" : $"At Adelaide: {here}  ·  {Ownership.PlayerSection.ToLowerInvariant()}: {yours}", small);
            var status = spec.ModelStatus == ModelStatus.Genuine ? "Model: genuine, true scale" : "Model: PLACEHOLDER";
            var statusStyle = spec.ModelStatus == ModelStatus.Genuine
                ? small
                : Styled(small, "placeholder-status", s => AirsideTheme.TextStyle(new GUIStyle(s) { fontStyle = FontStyle.Bold }, AirsideTheme.SafetyYellow));
            GUI.Label(new Rect(tx, card.y + 120f, tw, 18f), status, statusStyle);
        }


        private static void DrawLine(Vector2 from, Vector2 to, Color colour, float thickness)
        {
            var delta = to - from;
            var length = delta.magnitude;
            if (length < 0.5f)
                return;

            // Rotate in GUI space, after the HUD scale. GUIUtility.RotateAroundPivot takes
            // its pivot in screen space, so under any HUD scale other than 1 every line
            // swung about the wrong point and scattered across the screen.
            var matrix = GUI.matrix;
            GUI.matrix = matrix
                         * Matrix4x4.Translate(from)
                         * Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg))
                         * Matrix4x4.Translate(-from);
            DrawSolid(new Rect(from.x, from.y - thickness * 0.5f, length, thickness), colour);
            GUI.matrix = matrix;
        }

        private static void DrawSolid(Rect rect, Color colour)
        {
            var previous = GUI.color;
            GUI.color = colour;
            GUI.DrawTexture(rect, AirsideTheme.SolidWhite);
            GUI.color = previous;
        }

        // ---- Notifications ------------------------------------------------------------

        private void AnnounceNewEvents()
        {
            var fresh = _operations.TotalEvents - _seenEvents;
            _seenEvents = _operations.TotalEvents;
            if (fresh <= 0)
                return;

            // Anything that changed state is worth keeping; the throttle stops 60x from
            // writing the file every frame.
            RequestAutosave();

            var events = _operations.RecentEvents;
            var start = Math.Max(0, events.Count - (int)Math.Min(fresh, events.Count));
            for (var i = start; i < events.Count; i++)
            {
                var e = events[i];
                if (!e.Aircraft.Airline.IsPlayer)
                    continue;

                var reg = e.Aircraft.Registration;
                var dest = e.Aircraft.CurrentDestination?.Name;
                switch (e.State)
                {
                    case FleetState.Outbound:
                        ShowToast($"{reg} departed for {dest}.");
                        break;
                    case FleetState.HoldingForLanding:
                        ShowToast($"{reg} is back in the Adelaide circuit.");
                        break;
                    case FleetState.AwaitingStand:
                        ShowToast($"{reg} has landed — choose a stand.");
                        break;
                    case FleetState.AtStand:
                        ShowToast($"{reg} is parked on {StandNames.Display(e.Aircraft.Stand)}.");
                        break;
                }
            }
        }

        /// <summary>One toast per newly-applied career settlement (ADR 0053) — the result the
        /// plan doc asks the player to see after every eligible flight.</summary>
        private void AnnounceNewSettlements()
        {
            var fresh = _operations.TotalSettlements - _seenSettlements;
            _seenSettlements = _operations.TotalSettlements;
            if (fresh <= 0)
                return;

            var settlements = _operations.RecentSettlements;
            var start = Math.Max(0, settlements.Count - (int)Math.Min(fresh, settlements.Count));
            for (var i = start; i < settlements.Count; i++)
            {
                var s = settlements[i];
                var reg = s.SettlementId.Registration;
                if (s.ContractFulfilled)
                    ShowToast($"{reg} earned ${s.Payment:N0} — {s.ContractDefinitionId} complete!");
                else
                    ShowToast($"{reg} earned ${s.Payment:N0} on {s.ContractDefinitionId} ({s.RotationsCompleted} rotations so far).");
            }
        }

        private void DrawToast(Rect rect, GUIStyle label)
        {
            var now = Time.unscaledTime;
            _toasts.Visible(now, _visibleToasts);
            if (_visibleToasts.Count == 0)
                return;

            // Newest sits in the toast slot; older ones step away from the screen edge it hugs.
            var step = (rect.height + 6f) * (rect.center.y < Screen.height / HudLayout.ScaleFor(Screen.width, Screen.height) * 0.5f ? 1f : -1f);
            var centred = Styled(label, "middle-center", s => new GUIStyle(s) { alignment = TextAnchor.MiddleCenter });
            var previous = GUI.color;
            for (var i = 0; i < _visibleToasts.Count; i++)
            {
                var entry = _visibleToasts[i];
                var alpha = ToastQueue.Alpha(entry, now) * (i == 0 ? 1f : 0.78f);
                var slot = new Rect(rect.x, rect.y + step * i, rect.width, rect.height);
                _hudOverlays.Add(slot);
                DrawSolid(slot, new Color(AirsideTheme.RunwayInk.r, AirsideTheme.RunwayInk.g, AirsideTheme.RunwayInk.b, 0.9f * alpha));
                var frame = i == 0 ? AirsideTheme.SafetyYellow : AirsideTheme.Concrete;
                AirsideTheme.DrawPanelFrame(slot, new Color(frame.r, frame.g, frame.b, alpha));
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.Label(slot, entry.Repeats > 1 ? $"{entry.Message}  ×{entry.Repeats}" : entry.Message, centred);
                GUI.color = previous;
            }
        }

        private void ShowToast(string message) => _toasts.Push(message, Time.unscaledTime);

        /// <summary>Text/bar colour for a status severity; <paramref name="normal"/> when nothing is wrong.</summary>
        private static Color SeverityColour(StatusSeverity severity, Color normal) => severity switch
        {
            StatusSeverity.Warning => AirsideTheme.SignalRed,
            StatusSeverity.Attention => AirsideTheme.SafetyYellow,
            _ => normal
        };

        private void ToggleControlsHelp()
        {
            _controlsHelpOpen = !_controlsHelpOpen;
            PlayUiClick();
        }

        /// <summary>Esc closes help before selection / menu.</summary>
        private bool TryCloseControlsHelp()
        {
            if (!_controlsHelpOpen)
                return false;
            _controlsHelpOpen = false;
            PlayUiClick();
            return true;
        }

        private void DrawControlsHelp(HudLayout layout, GUIStyle panel, GUIStyle title, GUIStyle label, GUIStyle small, GUIStyle smallButton)
        {
            var viewport = layout.Viewport;
            var width = Mathf.Min(520f, viewport.x - AirlineHudLayout.Margin * 2f);
            var height = Mathf.Min(520f, viewport.y - AirlineHudLayout.Margin * 2f);
            var rect = new Rect((viewport.x - width) * 0.5f, (viewport.y - height) * 0.5f, width, height);

            var ink = AirsideTheme.RunwayInk;
            DrawSolid(rect, new Color(ink.r, ink.g, ink.b, 0.96f));
            GUI.Box(rect, GUIContent.none, panel);
            AirsideTheme.DrawPanelFrame(rect, AirsideTheme.SafetyYellow);

            var x = rect.x + 18f;
            var inner = rect.width - 36f;
            GUI.Label(new Rect(x, rect.y + 12f, inner - 100f, 26f), "Controls", title);
            if (GUI.Button(new Rect(rect.xMax - 110f, rect.y + 12f, 92f, 26f), "Close", smallButton))
                ToggleControlsHelp();

            GUI.Label(new Rect(x, rect.y + 42f, inner, 18f),
                "Hotkeys for camera, airline panels and playtest tools. Press F1 again to close.", small);

            var y = rect.y + 70f;
            foreach (var section in ControlsHelp.Sections)
            {
                GUI.Label(new Rect(x, y, inner, 20f), section.Title.ToUpperInvariant(), label);
                y += 24f;
                foreach (var binding in section.Bindings)
                {
                    GUI.Label(new Rect(x, y, 110f, 18f), binding.Key, smallButton);
                    GUI.Label(new Rect(x + 120f, y, inner - 120f, 18f), binding.Action, small);
                    y += 22f;
                }

                y += 10f;
            }
        }


        // ---- Helpers ------------------------------------------------------------------

        private FleetAircraft FirstPlayerAircraft()
        {
            if (_operations == null)
                return null;
            foreach (var aircraft in _operations.FleetOf(_operations.PlayerAirline))
                return aircraft;
            return null;
        }

        private string PlayerNeedsStand()
        {
            foreach (var aircraft in _operations.FleetOf(_operations.PlayerAirline))
                if (aircraft.State == FleetState.AwaitingStand)
                    return $"{aircraft.Registration} is waiting for you to choose a stand.";
            return null;
        }

        private string ClockText(SimulationTime time) => (_operations?.Clock ?? AirlineClock.Default).TimeText(time);

        private static string DurationText(long seconds) => AirlineClock.DurationText(seconds);
    }
}
