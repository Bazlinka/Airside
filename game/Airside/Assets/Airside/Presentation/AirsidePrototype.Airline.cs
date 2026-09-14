using System;
using System.Collections.Generic;
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

        /// <summary>
        /// Coarse mainland and Tasmania outlines (lon, lat) — a reading aid for the map,
        /// not survey data.
        /// </summary>
        private static readonly Vector2[] MainlandOutline =
        {
            new(113.6f, -22.0f), new(113.5f, -26.5f), new(114.6f, -28.8f), new(115.7f, -31.8f),
            new(115.0f, -33.6f), new(115.1f, -34.4f), new(117.9f, -35.1f), new(121.9f, -33.9f),
            new(126.0f, -32.3f), new(129.0f, -31.7f), new(131.2f, -31.5f), new(133.7f, -32.1f),
            new(135.9f, -34.7f), new(137.8f, -32.5f), new(137.0f, -35.2f), new(138.4f, -34.4f),
            new(138.5f, -34.8f), new(138.1f, -35.6f), new(139.4f, -36.0f), new(140.5f, -37.9f),
            new(141.6f, -38.4f), new(143.5f, -38.8f), new(144.9f, -38.3f), new(146.4f, -39.1f),
            new(148.0f, -37.9f), new(150.0f, -37.5f), new(150.2f, -35.7f), new(151.3f, -33.9f),
            new(151.8f, -32.9f), new(153.1f, -30.3f), new(153.6f, -28.6f), new(153.4f, -27.2f),
            new(153.2f, -25.0f), new(150.8f, -23.3f), new(149.2f, -21.1f), new(146.8f, -19.3f),
            new(145.8f, -16.9f), new(145.3f, -15.5f), new(143.5f, -12.8f), new(142.5f, -10.7f),
            new(141.9f, -12.6f), new(141.6f, -15.0f), new(140.8f, -17.5f), new(139.5f, -17.4f),
            new(136.8f, -15.9f), new(136.8f, -12.2f), new(135.5f, -12.0f), new(132.5f, -11.4f),
            new(130.8f, -12.4f), new(129.5f, -14.9f), new(128.1f, -15.5f), new(125.3f, -14.4f),
            new(123.6f, -16.3f), new(122.2f, -18.0f), new(118.6f, -20.3f), new(116.8f, -20.6f),
            new(113.6f, -22.0f)
        };

        private static readonly Vector2[] TasmaniaOutline =
        {
            new(144.6f, -40.7f), new(148.3f, -40.9f), new(148.0f, -43.2f), new(146.9f, -43.6f),
            new(145.2f, -42.2f), new(144.6f, -40.7f)
        };

        private AirlineOperations _operations;
        private string _airlineNameDraft = "Southern Cross Regional";
        private int _liveryChoice;
        private bool _mapOpen;
        private FleetAircraft _mapAircraft;
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
            if (!(_mapOpen && placement.MapCoversFleet))
                DrawFleetPanel(placement.FleetArea, panel, label, small, smallButton);
            if (_mapOpen)
                DrawDestinationsMap(placement.Map, panel, title, label, small, smallButton);
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
            if (!(_mapOpen && placement.MapCoversFleet))
                _hudPanels.Add(placement.FleetArea);
            if (_mapOpen)
                _hudPanels.Add(placement.Map);
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
                    $"{reg} leaves at {ClockText(aircraft.Scheduled.Value.DepartAt)} Adelaide time. The airport runs in real time — look around, or press Follow (F)."),
                GuideStep.Departing => ("3 · Departing",
                    $"{reg} is heading out. Press Follow (F) to ride along through the taxi and takeoff."),
                GuideStep.Away => ("4 · Away to " + dest,
                    "Flights take real time. Track it on the Map (Tab), or close the game — the airport keeps running and tells you what happened."),
                GuideStep.Landing => ("5 · Coming home",
                    $"The tower is bringing {reg} in to land. Follow (F) to watch the touchdown."),
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
            if (GUI.Button(new Rect(rect.x + 14f, rect.y + 60f, 130f, 24f), _mapOpen ? "Close map" : "Map (Tab)", smallButton))
                ToggleMap(_mapAircraft);
        }

        private void DrawFleetPanel(Rect area, GUIStyle panel, GUIStyle label, GUIStyle small, GUIStyle smallButton)
        {
            var rect = new Rect(area.x, area.y, area.width, Mathf.Min(Mathf.Max(FleetPanelContentHeight(), 120f), area.height));
            GUI.Box(rect, GUIContent.none, panel);
            var x = rect.x + 16f;
            var inner = rect.width - 32f;
            var y = rect.y + 12f;

            GUI.Label(new Rect(x, y, inner, 22f), "YOUR FLEET", label);
            y += 26f;
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
                    GUI.Label(new Rect(x, y, inner, 34f), $"{aircraft.Registration}  {StatusText(aircraft)}", small);
                    y += 34f;
                }
            }
        }

        /// <summary>Mirrors the row heights drawn below so the panel hugs its content.</summary>
        private float FleetPanelContentHeight()
        {
            var height = 12f + 26f;
            foreach (var aircraft in _operations.FleetOf(_operations.PlayerAirline))
            {
                height += 22f + 34f + 10f;
                if (aircraft.StateEndsAt.HasValue) height += 12f;
                if (aircraft.State == FleetState.AtStand) height += 32f;
                if (aircraft.State == FleetState.AwaitingStand) height += 54f;
            }

            foreach (var airline in _operations.Airlines)
            {
                if (airline.IsPlayer)
                    continue;
                height += 30f;
                foreach (var _ in _operations.FleetOf(airline))
                    height += 34f;
            }

            return height + 4f;
        }

        private float DrawPlayerAircraftRow(FleetAircraft aircraft, float x, float y, float width, GUIStyle label, GUIStyle small, GUIStyle smallButton)
        {
            GUI.Label(new Rect(x, y, width, 20f), $"{aircraft.Registration}  ·  {aircraft.Type.Name}", label);
            y += 22f;
            GUI.Label(new Rect(x, y, width, 34f), StatusText(aircraft), small);
            y += 34f;

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
            _mapOpen = forceOpen || !_mapOpen;
            _mapAircraft = aircraft ?? FirstPlayerAircraft();
            _mapSelection = null;
            _mapMessage = null;
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

            DrawOutline(mapRect, MainlandOutline);
            DrawOutline(mapRect, TasmaniaOutline);

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

        private static Vector2 Project(Rect area, double longitude, double latitude)
        {
            // Equirectangular with the width shrunk by cos(mid-latitude) so Australia
            // keeps its shape, then fitted and centred in the panel.
            const float minLon = 112f, maxLon = 155f, minLat = -44.5f, maxLat = -9.5f;
            var aspect = Mathf.Cos(27f * Mathf.Deg2Rad);
            var mapWidth = (maxLon - minLon) * aspect;
            var mapHeight = maxLat - minLat;
            var scale = Mathf.Min(area.width / mapWidth, area.height / mapHeight);
            var offsetX = area.x + (area.width - mapWidth * scale) * 0.5f;
            var offsetY = area.y + (area.height - mapHeight * scale) * 0.5f;
            return new Vector2(
                offsetX + ((float)longitude - minLon) * aspect * scale,
                offsetY + (maxLat - (float)latitude) * scale);
        }

        private static void DrawOutline(Rect area, Vector2[] lonLat)
        {
            var colour = new Color(AirsideTheme.Sand.r, AirsideTheme.Sand.g, AirsideTheme.Sand.b, 0.7f);
            for (var i = 1; i < lonLat.Length; i++)
                DrawLine(Project(area, lonLat[i - 1].x, lonLat[i - 1].y), Project(area, lonLat[i].x, lonLat[i].y), colour, 1.5f);
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
