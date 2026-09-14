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
        private const float ToastSeconds = 6f;

        private static readonly (string label, string hex)[] LiveryChoices =
        {
            ("Crimson", "#C8102E"), ("Navy", "#1F3A93"), ("Forest", "#2E7D32"),
            ("Sunset", "#E8772E"), ("Violet", "#6A3FA0"), ("Gold", "#D4A017")
        };

        private static readonly (string label, long seconds)[] DepartureOffsets =
        {
            // The soonest option still leaves time to board, close up and start both engines.
            ("In 3 min", EngineStartSequence.MinimumDepartureLeadSeconds), ("+15 min", 15 * 60), ("+30 min", 30 * 60),
            ("+1 h", 3600), ("+2 h", 7200)
        };

        private AirlineOperations _operations;
        private string _airlineNameDraft = "Southern Cross Regional";
        private int _liveryChoice;
        private bool _mapOpen;
        private bool _hangarOpen;
        private bool _flightsOpen;
        private bool _devToolsOpen;
        private readonly AustraliaMapLens _mapLens = new();
        private Vector2 _hangarScroll;
        private Vector2 _flightsScroll;
        private Vector2 _devToolsScroll;
        private readonly List<FleetAircraft> _flightsBoardRows = new();
        private readonly SeededRandomSource _devToolsRandom = new(4242);
        private FleetAircraft _mapAircraft;
        private string _selectedAircraftId;
        private Destination? _mapSelection;
        private int _departureOffsetChoice;
        private string _mapMessage;
        private long _seenEvents;
        private string _toast;
        private float _toastUntil;

        private bool AirlineSetupOpen => _operations == null;

        // ---- Frame hooks called from AirsidePrototype ----------------------------

        private void UpdateAirlineOperations()
        {
            if (_operations == null)
                return;

            _operations.Update();
            RefreshFleetFlights();
            AnnounceNewEvents();
            AutosaveIfDue();
        }

        /// <summary>True when the airline layer owns the keyboard this frame.</summary>
        private bool ReadAirlineControls(Keyboard keyboard)
        {
            // The start and away-summary panels own the keyboard until dismissed.
            if (AirlineSetupOpen || _awaySummary != null)
                return true;

            if (keyboard.tabKey.wasPressedThisFrame)
                ToggleMap(_mapAircraft);
            if (keyboard.hKey.wasPressedThisFrame)
                ToggleHangar();
            if (keyboard.tKey.wasPressedThisFrame)
                ToggleFlights();
            if (keyboard.f8Key.wasPressedThisFrame)
                ToggleDevTools();
            return false;
        }

        private void DrawAirlineHud(HudLayout layout, GUIStyle panel, GUIStyle title, GUIStyle button)
        {
            // A focused text field or a modal airline panel owns the keyboard.
            if (_cameraController != null)
                _cameraController.KeyboardCaptured =
                    AirlineSetupOpen || _awaySummary != null || GUIUtility.keyboardControl != 0;
            _guideStep = FirstFlightGuide.For(_operations, out _guideAircraft);
            if (_lastGuideStep == GuideStep.TaxiingIn && _guideStep == GuideStep.Complete)
                ShowToast("First trip complete. Keep your aircraft flying — plan the next one any time.");
            _lastGuideStep = _guideStep;
            var showGuide = !AirlineSetupOpen && _awaySummary == null && _guideStep != GuideStep.Complete;
            var placement = AirlineHudLayout.Create(layout, showGuide);
            RememberHudPanels(layout, placement, showGuide);

            var label = AirsideTheme.TextStyle(new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true });
            var small = AirsideTheme.TextStyle(new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true }, AirsideTheme.OpenSky);
            var smallButton = AirsideTheme.TextStyle(new GUIStyle(GUI.skin.button) { fontSize = 13, fontStyle = FontStyle.Bold }, AirsideTheme.Cloud);

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

            DrawClockPanel(placement.Clock, panel, label, small, smallButton);
            if (showGuide)
                DrawGuide(placement.Guide, panel, label, small);
            if (!((_mapOpen || _hangarOpen || _flightsOpen || _devToolsOpen) && placement.MapCoversFleet))
                DrawFleetPanel(placement.FleetArea, panel, label, small, smallButton);
            if (_devToolsOpen)
                DrawDevToolsPanel(placement.Map, panel, title, label, small, smallButton);
            else if (_flightsOpen)
                DrawFlightsPanel(placement.Map, panel, title, label, small, smallButton);
            else if (_hangarOpen)
                DrawHangarPanel(placement.Map, panel, title, label, small, smallButton);
            else if (_mapOpen)
                DrawDestinationsMap(placement.Map, panel, title, label, small, smallButton);
            DrawSelectionHudCard(layout, panel, label, small);
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
                var small = AirsideTheme.TextStyle(new GUIStyle(label) { fontSize = 12 }, AirsideTheme.OpenSky);
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
                    : "You start with one ATR 42-600. Emu Air flies two from the same airport.", label);

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
            RefreshFleetFlights();
            ShowToast($"{name} is open for business. Plan a flight for {FirstPlayerAircraft()?.Registration}.");
            SaveAirline();
            PlayUiClick();
        }

        // ---- Pointer over HUD ------------------------------------------------------------------

        private readonly List<Rect> _hudPanels = new();
        private float _hudScale = 1f;

        /// <summary>Record where HUD panels are this frame, in virtual GUI points.</summary>
        private void RememberHudPanels(HudLayout layout, AirlineHudLayout placement, bool showGuide)
        {
            _hudScale = HudLayout.ScaleFor(Screen.width, Screen.height);
            _hudPanels.Clear();
            _hudPanels.Add(layout.ControlBar);
            _hudPanels.Add(layout.SpeedReadout);
            if (_menuOpen)
                _hudPanels.Add(layout.PauseMenu);
            if (AirlineSetupOpen || _awaySummary != null)
            {
                // Modal panels: the whole screen belongs to the HUD until dismissed.
                _hudPanels.Add(new Rect(0f, 0f, layout.Viewport.x, layout.Viewport.y));
                return;
            }

            _hudPanels.Add(placement.Clock);
            if (showGuide)
                _hudPanels.Add(placement.Guide);
            if (!((_mapOpen || _hangarOpen || _flightsOpen || _devToolsOpen) && placement.MapCoversFleet))
                _hudPanels.Add(placement.FleetArea);
            if (_mapOpen || _hangarOpen || _flightsOpen || _devToolsOpen)
                _hudPanels.Add(placement.Map);
            if (TrySelectionHudCardRect(layout, out var selectionCard))
                _hudPanels.Add(selectionCard);
        }

        private bool IsPointerOverHud(Vector2 inputSystemPosition)
        {
            // Input System: origin bottom-left in pixels. IMGUI: origin top-left, scaled.
            var gui = new Vector2(inputSystemPosition.x, Screen.height - inputSystemPosition.y) / _hudScale;
            foreach (var rect in _hudPanels)
                if (rect.Contains(gui))
                    return true;
            return false;
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
            var bold = new GUIStyle(label) { fontStyle = FontStyle.Bold };
            GUI.Label(new Rect(rect.x + 14f, rect.y + 10f, rect.width - 28f, 22f), heading, bold);
            GUI.Label(new Rect(rect.x + 14f, rect.y + 34f, rect.width - 28f, rect.height - 40f), hint, small);
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
                    "Flights take real time. Track it on the Map (Tab), or close the game — the airport keeps running and tells you what happened."),
                GuideStep.Landing => ("5 · Coming home",
                    $"The tower is bringing {reg} in to land. Click the aircraft on final (or Follow) to watch the touchdown."),
                GuideStep.ChooseStand => ("6 · Choose a stand",
                    $"{reg} has landed. Pick a free bay in Your Fleet so it can taxi in."),
                GuideStep.TaxiingIn => ("7 · Taxiing in",
                    $"{reg} is taxiing to {aircraft.Stand}. That completes your first trip."),
                _ => (string.Empty, string.Empty)
            };
        }

        // ---- Away summary -----------------------------------------------------------------

        private void DrawAwaySummary(AirlineHudLayout placement, GUIStyle panel, GUIStyle title, GUIStyle label, GUIStyle button)
        {
            var summary = _awaySummary;
            var lineHeight = 40f;
            var rect = placement.SetupPanel(70f + summary.Lines.Count * lineHeight + 80f);
            GUI.Box(rect, GUIContent.none, panel);
            var x = rect.x + 20f;
            var inner = rect.width - 40f;

            GUI.Label(new Rect(x, rect.y + 16f, inner, 30f), summary.Title, title);
            var y = rect.y + 58f;
            foreach (var line in summary.Lines)
            {
                GUI.Label(new Rect(x, y, inner, lineHeight), line, label);
                y += lineHeight;
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

            _clock = clock;
            _simulation = new AirportSimulation(_clock, new SeededRandomSource(24031996), new ReservationTable());
            _preciseTime = _clock.Now.ElapsedSeconds;
            _operations = restored;
            _seenEvents = _operations.TotalEvents;
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
            if (GUI.Button(new Rect(rect.x + 10f, rect.y + 60f, 86f, 24f), _mapOpen ? "Close map" : "Map (Tab)", smallButton))
                ToggleMap(_mapAircraft);
            if (GUI.Button(new Rect(rect.x + 102f, rect.y + 60f, 90f, 24f), _hangarOpen ? "Close hangar" : "Hangar (H)", smallButton))
                ToggleHangar();
            if (GUI.Button(new Rect(rect.x + 198f, rect.y + 60f, 90f, 24f), _flightsOpen ? "Close board" : "Flights (T)", smallButton))
                ToggleFlights();
        }

        private void DrawFleetPanel(Rect area, GUIStyle panel, GUIStyle label, GUIStyle small, GUIStyle smallButton)
        {
            var rect = new Rect(area.x, area.y, area.width, Mathf.Min(Mathf.Max(FleetPanelContentHeight(), 120f), area.height));
            GUI.Box(rect, GUIContent.none, panel);
            var x = rect.x + 16f;
            var inner = rect.width - 32f;
            var y = rect.y + 12f;

            GUI.Label(new Rect(x, y, inner, 22f), "YOUR FLEET", label);
            y += 22f;
            GUI.Label(new Rect(x, y, inner, 18f), "Click an aircraft on the field — or a registration here.", small);
            y += 22f;
            foreach (var aircraft in _operations.FleetOf(_operations.PlayerAirline))
            {
                y = DrawPlayerAircraftRow(aircraft, x, y, inner, label, small, smallButton);
                y += 10f;
            }

            foreach (var airline in _operations.Airlines)
            {
                if (airline.IsPlayer)
                    continue;
                y += 6f;
                DrawSolid(new Rect(x, y + 4f, 8f, 14f), AirsideTheme.FromHex(airline.LiveryHex));
                GUI.Label(new Rect(x + 14f, y, inner - 14f, 22f), airline.Name.ToUpperInvariant(), label);
                y += 24f;
                foreach (var aircraft in _operations.FleetOf(airline))
                {
                    if (y + 20f > rect.yMax)
                        break;
                    y = DrawTrafficAircraftRow(aircraft, x, y, inner, label, small);
                }
            }
        }

        /// <summary>Mirrors the row heights drawn below so the panel hugs its content.</summary>
        private float FleetPanelContentHeight()
        {
            var height = 12f + 22f + 22f;
            foreach (var aircraft in _operations.FleetOf(_operations.PlayerAirline))
            {
                height += 56f + 10f;
                if (_selectedAircraftId == aircraft.Registration) height += 42f;
                if (aircraft.StateEndsAt.HasValue) height += 12f;
                if (aircraft.State == FleetState.AtStand) height += 32f;
                if (aircraft.State == FleetState.AwaitingStand) height += 54f;
            }

            foreach (var airline in _operations.Airlines)
            {
                if (airline.IsPlayer)
                    continue;
                height += 30f;
                foreach (var aircraft in _operations.FleetOf(airline))
                    height += 54f + (_selectedAircraftId == aircraft.Registration ? 42f : 0f);
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

            switch (aircraft.State)
            {
                case FleetState.AtStand:
                    var planRect = new Rect(x, y, 140f, 26f);
                    if (IsGuided(aircraft, GuideStep.PlanFirstFlight))
                        DrawGuideHighlight(planRect);
                    if (GUI.Button(planRect, "Plan flight", smallButton))
                        ToggleMap(aircraft, forceOpen: true);
                    if (aircraft.Scheduled.HasValue
                        && GUI.Button(new Rect(x + 150f, y, 120f, 26f), "Cancel", smallButton))
                        if (_operations.CancelDeparture(aircraft).Accepted)
                            SaveAirline();
                    y += 32f;
                    break;

                case FleetState.AwaitingStand:
                    GUI.Label(new Rect(x, y, width, 20f), "Choose a stand:", label);
                    y += 22f;
                    var bx = x;
                    var any = false;
                    foreach (var stand in _operations.FreeStands())
                    {
                        any = true;
                        var standRect = new Rect(bx, y, 76f, 26f);
                        if (IsGuided(aircraft, GuideStep.ChooseStand))
                            DrawGuideHighlight(standRect);
                        if (GUI.Button(standRect, stand.Value, smallButton))
                        {
                            var result = _operations.AssignStand(aircraft, stand);
                            if (result.Accepted)
                                SaveAirline();
                            else
                                ShowToast(result.Reason);
                        }
                        bx += 82f;
                    }

                    if (!any)
                        GUI.Label(new Rect(x, y, width, 26f), "All stands occupied — wait for one to clear.", small);
                    y += 32f;
                    break;
            }

            return y;
        }

        private float DrawTrafficAircraftRow(FleetAircraft aircraft, float x, float y, float width, GUIStyle label, GUIStyle small)
        {
            var row = new Rect(x, y, width, 52f);
            DrawAircraftSelection(aircraft, row, aircraft.Registration, StatusText(aircraft), label, small);
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

            GUI.Label(new Rect(rect.x, rect.y, rect.width - 70f, 20f), heading, headingStyle);
            GUI.Label(new Rect(rect.x, rect.y + 20f, rect.width, rect.height - 20f), status, statusStyle);
            var action = selected ? "Selected" : "Select";
            var actionStyle = AirsideTheme.TextStyle(new GUIStyle(statusStyle) { alignment = TextAnchor.UpperRight, fontStyle = FontStyle.Bold },
                selected ? AirsideTheme.SafetyYellow : AirsideTheme.CoastalBlue);
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
            var card = new Rect(x, y + 2f, width, 36f);
            DrawSolid(card, new Color(AirsideTheme.RunwayInk.r, AirsideTheme.RunwayInk.g, AirsideTheme.RunwayInk.b, 0.9f));
            DrawSolid(new Rect(card.x, card.y, 4f, card.height), accent);
            var mode = onField ? "FOLLOWING AT ADELAIDE" : "TRACKING ON ROUTE MAP";
            GUI.Label(new Rect(card.x + 10f, card.y + 3f, card.width - 20f, 15f), mode, small);
            GUI.Label(new Rect(card.x + 10f, card.y + 18f, card.width - 20f, 15f),
                $"{aircraft.Airline.Name}  ·  {aircraft.Type.Name}", small);
            return y + 42f;
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
            var bold = new GUIStyle(label) { fontStyle = FontStyle.Bold };
            GUI.Label(new Rect(rect.x + 14f, rect.y + 8f, rect.width - 28f, 20f),
                $"{aircraft.Registration}  ·  {aircraft.Airline.Name}", bold);
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
            _mapAircraft = aircraft;
            if (TryFollowFleetAircraft(aircraft.Registration))
            {
                _mapOpen = false;
                _hangarOpen = false;
                _flightsOpen = false;
                _devToolsOpen = false;
            }
            else
            {
                _hangarOpen = false;
                _flightsOpen = false;
                _devToolsOpen = false;
                _mapOpen = true;
                _mapLens.Reset();
                _mapSelection = aircraft.CurrentDestination;
            }
            PlayUiClick();
        }

        private bool ClearAircraftSelection()
        {
            if (string.IsNullOrEmpty(_selectedAircraftId))
                return false;
            _selectedAircraftId = null;
            _mapAircraft = null;
            _mapOpen = false;
            _hangarOpen = false;
            _flightsOpen = false;
            _devToolsOpen = false;
            return true;
        }

        private string StatusText(FleetAircraft aircraft)
        {
            var to = aircraft.CurrentDestination;
            var dest = to.HasValue ? to.Value.Name : string.Empty;
            var ends = aircraft.StateEndsAt.HasValue ? ClockText(aircraft.StateEndsAt.Value) : string.Empty;
            return aircraft.State switch
            {
                FleetState.AtStand => aircraft.Scheduled.HasValue
                    ? $"On {aircraft.Stand} · departs {ClockText(aircraft.Scheduled.Value.DepartAt)} for {aircraft.Scheduled.Value.Destination.Name}"
                    : $"On {aircraft.Stand} · no flight planned",
                FleetState.TaxiOut => $"Taxiing to the runway · {dest}",
                FleetState.HoldingShort => $"Holding short, waiting for the runway · {dest}",
                FleetState.TakingOff => $"Taking off for {dest}",
                FleetState.Outbound => $"En route to {dest} · lands {ends}",
                FleetState.AtDestination => $"On the ground at {dest} · departs {ends}",
                FleetState.Inbound => $"Returning from {dest} · back {ends}",
                FleetState.HoldingForLanding => "In the Adelaide circuit, waiting to land",
                FleetState.Landing => "Landing at Adelaide",
                FleetState.AwaitingStand => "Landed · waiting for a stand",
                FleetState.TaxiIn => $"Taxiing to {aircraft.Stand}",
                _ => aircraft.State.ToString()
            };
        }

        // ---- Destinations map ---------------------------------------------------------

        private void ToggleMap(FleetAircraft aircraft, bool forceOpen = false)
        {
            var open = forceOpen || !_mapOpen;
            _mapOpen = open;
            if (open)
            {
                _hangarOpen = false;
                _flightsOpen = false;
                _devToolsOpen = false;
                _mapLens.Reset();
            }
            _mapAircraft = aircraft ?? FirstPlayerAircraft();
            _mapSelection = null;
            _mapMessage = null;
            PlayUiClick();
        }

        private void ToggleHangar()
        {
            _hangarOpen = !_hangarOpen;
            if (_hangarOpen)
            {
                _mapOpen = false;
                _flightsOpen = false;
                _devToolsOpen = false;
            }
            PlayUiClick();
        }

        private void ToggleFlights()
        {
            _flightsOpen = !_flightsOpen;
            if (_flightsOpen)
            {
                _mapOpen = false;
                _hangarOpen = false;
                _devToolsOpen = false;
            }
            PlayUiClick();
        }

        private void ToggleDevTools()
        {
            _devToolsOpen = !_devToolsOpen;
            if (_devToolsOpen)
            {
                _mapOpen = false;
                _hangarOpen = false;
                _flightsOpen = false;
            }
            PlayUiClick();
        }

        private void DrawDestinationsMap(Rect rect, GUIStyle panel, GUIStyle title, GUIStyle label, GUIStyle small, GUIStyle smallButton)
        {
            // Near-opaque: the translucent HUD panel let runways and taxiways read
            // through the coastline and route lines.
            var ink = AirsideTheme.RunwayInk;
            DrawSolid(rect, new Color(ink.r, ink.g, ink.b, 0.96f));
            GUI.Box(rect, GUIContent.none, panel);

            var detailWidth = Mathf.Min(260f, rect.width * 0.38f);
            var mapRect = new Rect(rect.x + 12f, rect.y + 12f, rect.width - detailWidth - 36f, rect.height - 24f);
            var detail = new Rect(mapRect.xMax + 12f, rect.y + 12f, detailWidth, rect.height - 24f);

            HandleMapLensGui(mapRect);
            DrawAustraliaBase(mapRect);

            var aircraft = _mapAircraft;
            var home = _operations.Home;
            var homePoint = Project(mapRect, home.Longitude, home.Latitude);

            // Routes flown right now, so the map doubles as the off-map flight tracker.
            foreach (var flying in _operations.Fleet)
            {
                if (!flying.IsOffMap || !flying.CurrentDestination.HasValue)
                    continue;
                var d = flying.CurrentDestination.Value;
                var destPoint = Project(mapRect, d.Longitude, d.Latitude);
                var colour = AirsideTheme.FromHex(flying.Airline.LiveryHex);
                DrawLine(homePoint, destPoint, new Color(colour.r, colour.g, colour.b, 0.55f), 2f);
                var p = (float)flying.StateProgress(_clock.Now);
                var at = flying.State switch
                {
                    FleetState.Outbound => Vector2.Lerp(homePoint, destPoint, p),
                    FleetState.Inbound => Vector2.Lerp(destPoint, homePoint, p),
                    _ => destPoint
                };
                DrawSolid(new Rect(at.x - 5f, at.y - 5f, 10f, 10f), colour);
                GUI.Label(new Rect(at.x + 7f, at.y - 9f, 80f, 18f), flying.Registration, small);
            }

            foreach (var destination in _operations.MapDestinations())
            {
                var point = Project(mapRect, destination.Longitude, destination.Latitude);
                var reachable = aircraft != null && _operations.CanReach(aircraft, destination);
                var selected = _mapSelection.HasValue && _mapSelection.Value.Equals(destination);
                if (selected)
                    DrawLine(homePoint, point, AirsideTheme.SafetyYellow, 2f);

                var colour = selected ? AirsideTheme.SafetyYellow : reachable ? AirsideTheme.ClearGreen : AirsideTheme.Concrete;
                var size = selected ? 12f : 9f;
                DrawSolid(new Rect(point.x - size * 0.5f, point.y - size * 0.5f, size, size), colour);
                GUI.Label(new Rect(point.x + 7f, point.y - 9f, 60f, 18f), destination.Code, small);
                if (GUI.Button(new Rect(point.x - 12f, point.y - 12f, 24f, 24f), GUIContent.none, GUIStyle.none))
                {
                    _mapSelection = destination;
                    _mapMessage = null;
                }
            }

            DrawSolid(new Rect(homePoint.x - 7f, homePoint.y - 7f, 14f, 14f), AirsideTheme.FromHex(_operations.PlayerAirline.LiveryHex));
            // Left of the dot: Kingscote and Port Lincoln sit just to its right and below.
            var adlStyle = new GUIStyle(label) { alignment = TextAnchor.MiddleRight };
            GUI.Label(new Rect(homePoint.x - 89f, homePoint.y - 9f, 80f, 18f), "ADL", adlStyle);

            DrawMapDetail(detail, aircraft, title, label, small, smallButton);
        }

        private void DrawMapDetail(Rect rect, FleetAircraft aircraft, GUIStyle title, GUIStyle label, GUIStyle small, GUIStyle smallButton)
        {
            var y = rect.y;
            GUI.Label(new Rect(rect.x, y, rect.width, 28f), "Destinations", title);
            y += 34f;
            GUI.Label(new Rect(rect.x, y, rect.width, 36f),
                aircraft != null ? $"Planning {aircraft.Registration} · range {aircraft.Type.PracticalRangeKm:0} km" : "No aircraft selected", small);
            y += 38f;

            if (!_mapSelection.HasValue)
            {
                GUI.Label(new Rect(rect.x, y, rect.width, 60f),
                    "Green: in range. Grey: beyond this aircraft's range. Click a destination.", small);
                if (GUI.Button(new Rect(rect.x, rect.yMax - 30f, rect.width, 28f), "Close", smallButton))
                    ToggleMap(null);
                return;
            }

            var destination = _mapSelection.Value;
            var km = _operations.DistanceKm(destination);
            GUI.Label(new Rect(rect.x, y, rect.width, 22f), $"{destination.Name}, {destination.State}", label);
            y += 24f;
            GUI.Label(new Rect(rect.x, y, rect.width, 20f), $"{km:0} km from Adelaide", small);
            y += 22f;

            if (aircraft == null)
                return;

            if (!_operations.CanReach(aircraft, destination))
            {
                GUI.Label(new Rect(rect.x, y, rect.width, 54f),
                    $"Locked — beyond the {aircraft.Type.Name}'s range. A longer-range aircraft will open it.", small);
            }
            else
            {
                var airborne = _operations.AirborneSeconds(aircraft, destination);
                GUI.Label(new Rect(rect.x, y, rect.width, 20f), $"{DurationText(airborne)} each way", small);
                y += 26f;

                if (aircraft.State != FleetState.AtStand)
                {
                    GUI.Label(new Rect(rect.x, y, rect.width, 40f), $"{aircraft.Registration} must be on a stand to plan a flight.", small);
                }
                else
                {
                    GUI.Label(new Rect(rect.x, y, rect.width, 20f), "Depart", label);
                    y += 22f;
                    for (var i = 0; i < DepartureOffsets.Length; i++)
                    {
                        var col = i % 3;
                        var row = i / 3;
                        var cell = new Rect(rect.x + col * (rect.width / 3f), y + row * 30f, rect.width / 3f - 4f, 26f);
                        var text = i == _departureOffsetChoice ? $"[{DepartureOffsets[i].label}]" : DepartureOffsets[i].label;
                        if (GUI.Button(cell, text, smallButton))
                            _departureOffsetChoice = i;
                    }

                    y += 64f;
                    var departAt = _clock.Now.Advance(DepartureOffsets[_departureOffsetChoice].seconds);
                    var back = departAt.ElapsedSeconds + AirlineOperations.TaxiOutSecondsFrom(aircraft.Stand) + AirlineOperations.TakeoffRunwaySeconds
                               + airborne * 2 + AirlineOperations.DestinationTurnaroundSeconds;
                    GUI.Label(new Rect(rect.x, y, rect.width, 40f),
                        $"Departs {ClockText(departAt)} · back about {ClockText(new SimulationTime(back))}", small);
                    y += 42f;

                    if (GUI.Button(new Rect(rect.x, y, rect.width, 32f), $"Schedule to {destination.Code}", smallButton))
                    {
                        var result = _operations.ScheduleDeparture(aircraft, destination, departAt);
                        if (result.Accepted)
                        {
                            ShowToast($"{aircraft.Registration} departs {ClockText(departAt)} for {destination.Name}.");
                            _mapOpen = false;
                            SaveAirline();
                        }
                        else
                        {
                            _mapMessage = result.Reason;
                        }
                    }

                    y += 38f;
                }
            }

            if (!string.IsNullOrEmpty(_mapMessage))
                GUI.Label(new Rect(rect.x, y, rect.width, 40f), _mapMessage, small);

            if (GUI.Button(new Rect(rect.x, rect.yMax - 30f, rect.width, 28f), "Close", smallButton))
                ToggleMap(null);
        }

        private Vector2 Project(Rect area, double longitude, double latitude)
        {
            _mapLens.Project(area.x, area.y, area.width, area.height, longitude, latitude, out var x, out var y);
            return new Vector2(x, y);
        }

        private void HandleMapLensGui(Rect mapRect)
        {
            var ev = Event.current;
            if (ev == null)
                return;
            var over = mapRect.Contains(ev.mousePosition);
            if (over && ev.type == EventType.ScrollWheel)
            {
                var factor = ev.delta.y > 0f ? 0.9f : 1.12f;
                _mapLens.ZoomAtGui(mapRect.width, mapRect.height,
                    ev.mousePosition.x - mapRect.x, ev.mousePosition.y - mapRect.y, factor);
                ev.Use();
            }

            if (over && ev.type == EventType.MouseDown && ev.button == 0)
            {
                _mapPanning = true;
                _mapPanGui = ev.mousePosition;
                ev.Use();
            }

            if (_mapPanning && ev.type == EventType.MouseDrag && ev.button == 0)
            {
                _mapLens.PanByGuiDelta(mapRect.width, mapRect.height,
                    _mapPanGui.x - mapRect.x, _mapPanGui.y - mapRect.y,
                    ev.mousePosition.x - mapRect.x, ev.mousePosition.y - mapRect.y);
                _mapPanGui = ev.mousePosition;
                ev.Use();
            }

            if (ev.type == EventType.MouseUp && ev.button == 0)
                _mapPanning = false;
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
                var stateStyle = AirsideTheme.TextStyle(new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                }, AirsideTheme.OpenSky);
                foreach (var (code, lon, lat) in AustraliaMapGeometry.StateLabels)
                {
                    var p = Project(mapRect, lon, lat);
                    GUI.Label(new Rect(p.x - 18f, p.y - 9f, 36f, 18f), code, stateStyle);
                }
            }

            if (_mapLens.ShowCountyDetail)
            {
                var regionStyle = AirsideTheme.TextStyle(new GUIStyle(GUI.skin.label)
                {
                    fontSize = 10,
                    alignment = TextAnchor.MiddleCenter
                }, AirsideTheme.Cloud);
                foreach (var (name, lon, lat) in AustraliaMapGeometry.RegionLabels)
                {
                    var p = Project(mapRect, lon, lat);
                    GUI.Label(new Rect(p.x - 54f, p.y - 8f, 108f, 16f), name, regionStyle);
                }
            }

            var hint = AirsideTheme.TextStyle(new GUIStyle(GUI.skin.label) { fontSize = 11 }, AirsideTheme.Concrete);
            GUI.Label(new Rect(mapRect.x + 6f, mapRect.yMax - 20f, mapRect.width - 12f, 18f),
                "Scroll to zoom · drag to pan · click a destination", hint);
        }

        private void DrawLonLatPolyline(Rect area, float[] lonLat, Color colour, float thickness)
        {
            var count = AustraliaMapGeometry.PointCount(lonLat);
            for (var i = 1; i < count; i++)
            {
                var i0 = (i - 1) * 2;
                var i1 = i * 2;
                DrawLine(
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
            GUI.Label(new Rect(x, rect.y + 10f, inner - 120f, 26f), "Flights", title);
            if (GUI.Button(new Rect(rect.xMax - 108f, rect.y + 10f, 92f, 26f), "Close", smallButton))
                ToggleFlights();

            GUI.Label(new Rect(x, rect.y + 40f, inner, 18f),
                "Every movement at Adelaide — yours and the other airlines — ordered by the next time that matters.", small);

            _flightsBoardRows.Clear();
            foreach (var aircraft in _operations.Fleet)
                _flightsBoardRows.Add(aircraft);
            FlightBoard.Sort(_flightsBoardRows);

            var headerY = rect.y + 64f;
            var timeW = 72f;
            var phaseW = 112f;
            var routeW = Mathf.Min(160f, inner * 0.28f);
            GUI.Label(new Rect(x, headerY, timeW, 18f), "TIME", small);
            GUI.Label(new Rect(x + timeW, headerY, phaseW, 18f), "PHASE", small);
            GUI.Label(new Rect(x + timeW + phaseW, headerY, routeW, 18f), "ROUTE", small);
            GUI.Label(new Rect(x + timeW + phaseW + routeW, headerY, inner - timeW - phaseW - routeW, 18f), "AIRCRAFT", small);

            var view = new Rect(x, headerY + 22f, inner, rect.height - (headerY - rect.y) - 36f);
            var rowHeight = 44f;
            var contentHeight = 8f + _flightsBoardRows.Count * rowHeight;
            _flightsScroll = GUI.BeginScrollView(view, _flightsScroll, new Rect(0f, 0f, inner - 18f, contentHeight));
            var y = 4f;
            var rowWidth = inner - 22f;
            foreach (var aircraft in _flightsBoardRows)
            {
                var row = new Rect(0f, y, rowWidth, rowHeight - 4f);
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

                GUI.Label(new Rect(8f, y + 6f, timeW - 4f, 18f), FlightBoard.TimeLabel(aircraft, ClockText), small);
                GUI.Label(new Rect(timeW, y + 6f, phaseW - 4f, 18f), FlightBoard.PhaseLabel(aircraft), label);
                GUI.Label(new Rect(timeW + phaseW, y + 6f, routeW - 4f, 18f), FlightBoard.RouteText(aircraft), small);
                GUI.Label(new Rect(timeW + phaseW + routeW, y + 6f, rowWidth - timeW - phaseW - routeW - 8f, 18f),
                    $"{aircraft.Registration}  ·  {aircraft.Airline.Name}", small);

                if (aircraft.StateEndsAt.HasValue)
                {
                    AirsideTheme.DrawProgressBar(new Rect(timeW, y + 28f, rowWidth - timeW - 12f, 5f),
                        (float)aircraft.StateProgress(_clock.Now),
                        AirsideTheme.CoastalBlue, AirsideTheme.Tarmac);
                }

                if (GUI.Button(row, GUIContent.none, GUIStyle.none))
                {
                    SelectAircraft(aircraft);
                    if (aircraft.IsOffMap)
                    {
                        _flightsOpen = false;
                        _mapOpen = true;
                        _mapLens.Reset();
                    }
                }

                y += rowHeight;
            }

            GUI.EndScrollView();
        }


        private void DrawDevToolsPanel(Rect rect, GUIStyle panel, GUIStyle title, GUIStyle label, GUIStyle small, GUIStyle smallButton)
        {
            var ink = AirsideTheme.RunwayInk;
            DrawSolid(rect, new Color(ink.r, ink.g, ink.b, 0.96f));
            GUI.Box(rect, GUIContent.none, panel);

            var x = rect.x + 16f;
            var inner = rect.width - 32f;
            GUI.Label(new Rect(x, rect.y + 10f, inner - 120f, 26f), "Dev tools", title);
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


        private void DrawHangarPanel(Rect rect, GUIStyle panel, GUIStyle title, GUIStyle label, GUIStyle small, GUIStyle smallButton)
        {
            var ink = AirsideTheme.RunwayInk;
            DrawSolid(rect, new Color(ink.r, ink.g, ink.b, 0.96f));
            GUI.Box(rect, GUIContent.none, panel);

            var x = rect.x + 16f;
            var inner = rect.width - 32f;
            GUI.Label(new Rect(x, rect.y + 10f, inner - 120f, 26f), "Hangar", title);
            if (GUI.Button(new Rect(rect.xMax - 108f, rect.y + 10f, 92f, 26f), "Close", smallButton))
                ToggleHangar();

            GUI.Label(new Rect(x, rect.y + 40f, inner, 18f),
                "Your aircraft and every other flight in the sky right now.", small);

            var view = new Rect(x, rect.y + 64f, inner, rect.height - 78f);
            var contentHeight = 8f;
            foreach (var airline in _operations.Airlines)
            {
                contentHeight += 28f;
                foreach (var _ in _operations.FleetOf(airline))
                    contentHeight += 72f;
            }

            _hangarScroll = GUI.BeginScrollView(view, _hangarScroll, new Rect(0f, 0f, inner - 18f, contentHeight));
            var y = 4f;
            var rowWidth = inner - 22f;
            foreach (var airline in _operations.Airlines)
            {
                DrawSolid(new Rect(0f, y + 4f, 8f, 14f), AirsideTheme.FromHex(airline.LiveryHex));
                GUI.Label(new Rect(14f, y, rowWidth - 14f, 22f),
                    airline.IsPlayer ? $"{airline.Name.ToUpperInvariant()}  ·  YOUR AIRLINE" : airline.Name.ToUpperInvariant(),
                    label);
                y += 26f;

                foreach (var aircraft in _operations.FleetOf(airline))
                {
                    var row = new Rect(0f, y, rowWidth, 66f);
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

                    GUI.Label(new Rect(10f, y + 6f, rowWidth - 20f, 20f),
                        $"{aircraft.Registration}  ·  {aircraft.Type.Name}", label);
                    GUI.Label(new Rect(10f, y + 28f, rowWidth - 20f, 18f), StatusText(aircraft), small);

                    if (aircraft.StateEndsAt.HasValue)
                    {
                        AirsideTheme.DrawProgressBar(new Rect(10f, y + 50f, rowWidth - 20f, 6f),
                            (float)aircraft.StateProgress(_clock.Now),
                            AirsideTheme.CoastalBlue, AirsideTheme.Tarmac);
                    }
                    else
                    {
                        var where = aircraft.IsOffMap ? "Away from Adelaide" : "At Adelaide";
                        GUI.Label(new Rect(10f, y + 48f, rowWidth - 20f, 16f), where, small);
                    }

                    if (GUI.Button(row, GUIContent.none, GUIStyle.none))
                    {
                        SelectAircraft(aircraft);
                        if (aircraft.IsOffMap)
                        {
                            _hangarOpen = false;
                            _mapOpen = true;
                            _mapLens.Reset();
                        }
                    }

                    y += 72f;
                }
            }

            GUI.EndScrollView();
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
                        ShowToast($"{reg} is parked on {e.Aircraft.Stand}.");
                        break;
                }
            }
        }

        private void DrawToast(Rect rect, GUIStyle label)
        {
            if (string.IsNullOrEmpty(_toast) || Time.unscaledTime > _toastUntil)
                return;

            DrawSolid(rect, new Color(AirsideTheme.RunwayInk.r, AirsideTheme.RunwayInk.g, AirsideTheme.RunwayInk.b, 0.9f));
            AirsideTheme.DrawPanelFrame(rect, AirsideTheme.SafetyYellow);
            var centred = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
            GUI.Label(rect, _toast, centred);
        }

        private void ShowToast(string message)
        {
            _toast = message;
            _toastUntil = Time.unscaledTime + ToastSeconds;
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
