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

        /// <summary>Shared with the Stats workspace's post-game-start livery swatches (ADR 0067).</summary>
        private static readonly (string label, string hex)[] LiveryChoices = StatsWorkspaceModel.LiveryPalette;

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
        private readonly List<OperationsRow> _compactOpsRows = new();
        // Departures first: the board opens on what you are about to send, not what is coming.
        private bool _flightsShowArrivals;
        private Vector2 _devToolsScroll;
        private readonly SeededRandomSource _devToolsRandom = new(4242);

        // ---- Workspace shell (ADR 0057) -------------------------------------------------
        // One painter and one draw list per surface, reused every frame: the layout and copy
        // are decided by the UnityEngine-free painters in HudShell / *Workspace.cs.
        private readonly HudPainter _hudPainter = new();
        private readonly HudDrawList _shellDrawList = new();
        private readonly HudDrawList _workspaceDrawList = new();
        private readonly HudDrawList _mapNetworkDrawList = new();
        private readonly HudDrawList _mapFilterDrawList = new();
        private readonly List<HudTopBarSegment> _topBarSegments = new();
        private readonly List<HudNavTab> _navTabs = new();
        private readonly string[] _topBarValues = new string[4];
        private readonly OperationsWorkspaceModel _operationsWorkspace = new();
        private readonly RouteMapWorkspaceModel _routeMapWorkspace = new();
        private readonly FleetWorkspaceModel _fleetWorkspace = new();
        private readonly ContractsWorkspaceModel _contractsWorkspace = new();
        private readonly StatsWorkspaceModel _statsWorkspace = new();
        /// <summary>Draft text for the Stats workspace's rename field, reset each time it opens (ADR 0068).</summary>
        private string _statsRenameDraft = string.Empty;
        private readonly List<OperationsEventLine> _eventHistory = new();
        private RouteMapFilter _mapFilter = RouteMapFilter.Available;
        private int _boardScrollRow;
        private bool _boardScrollSnapToDay = true;
        /// <summary>
        /// When non-negative, the board is still auto-following NOW at this scroll row.
        /// Cleared as soon as the player scrolls the list themselves.
        /// </summary>
        private int _boardScrollFollowRow = -1;
        private int _rosterScrollRow;
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
            if (keyboard.cKey.wasPressedThisFrame)
                SetWorkspace(HudWorkspace.Contracts);
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

            var overview = _activeWorkspace == HudWorkspace.None && !_devToolsOpen;

            // A first click on a contract card arms it, the second commits (AcceptContractFromHud)
            // so an accidental click never signs a service commitment — but that arm used to
            // survive leaving the Contracts workspace entirely (switch pages, Esc, open dev
            // tools, follow a flight onto the map). Coming back later, one exploratory click on
            // a card the player had forgotten was armed would silently accept it. Checked here,
            // every frame, rather than at each of the several places `_activeWorkspace` changes,
            // so no future navigation path can reintroduce the same gap.
            if (_activeWorkspace != HudWorkspace.Contracts)
                _highlightedContractId = null;

            // Under every panel, so a tag never sits on top of a button.
            DrawFieldTags(small);
            DrawTopBar(placement);
            // The objective card is part of the persistent shell: it stays in the same place
            // whichever workspace is open, so the five pages read as one screen. It only gives
            // way on a window too narrow to hold a usable workspace beside it.
            if (showGuide)
                DrawGuide(placement.Objective, panel, label, small);
            else if (overview || ObjectiveSurvivesWorkspace(layout))
                DrawObjectiveCard(placement.Objective);
            if (overview && placement.Operations.width > 0f)
                DrawCompactOperations(placement.Operations, panel, label, small);
            if (_devToolsOpen)
                DrawDevToolsPanel(placement.Workspace, panel, title, label, small, smallButton);
            else switch (_activeWorkspace)
            {
                case HudWorkspace.Operations:
                    DrawOperationsWorkspace(placement.Workspace);
                    break;
                case HudWorkspace.Fleet:
                    DrawFleetWorkspace(placement.Workspace);
                    break;
                case HudWorkspace.Map:
                    DrawDestinationsMap(placement.Workspace, panel, title, label, small, smallButton);
                    break;
                case HudWorkspace.Contracts:
                    DrawContractsWorkspace(placement.Workspace);
                    break;
                case HudWorkspace.Stats:
                    DrawStatsWorkspace(placement.Workspace);
                    break;
            }
            DrawMiniMap(FieldMiniMap.PanelFor(layout, placement), panel, small);
            if (overview)
                DrawSelectionHudCard(layout, placement, panel, label, small, smallButton);
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
            var panelHeight = 392f + saveBlock;
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

            var eyebrow = Styled(label, "setup-eyebrow", s => AirsideTheme.TextStyle(new GUIStyle(s)
                { fontSize = 11, fontStyle = FontStyle.Bold }, AirsideTheme.SafetyYellow));
            GUI.Label(new Rect(x, rect.y + 14f, inner, 16f), "ADELAIDE  ·  PROVISIONAL AIRLINE", eyebrow);
            GUI.Label(new Rect(x, rect.y + 34f, inner, 28f),
                hasSave ? "Or start a new airline" : "Start small. Grow the airline.", title);

            GUI.Label(new Rect(x, rect.y + 72f, inner, 18f), "Airline name", label);
            _airlineNameDraft = GUI.TextField(new Rect(x, rect.y + 94f, inner, 30f), _airlineNameDraft ?? string.Empty, 32);

            GUI.Label(new Rect(x, rect.y + 136f, inner, 18f), "Livery colour", label);
            var swatch = (inner - 5 * 8f) / LiveryChoices.Length;
            for (var i = 0; i < LiveryChoices.Length; i++)
            {
                var cell = new Rect(x + i * (swatch + 8f), rect.y + 158f, swatch, 36f);
                DrawSolid(cell, AirsideTheme.FromHex(LiveryChoices[i].hex));
                if (i == _liveryChoice)
                    AirsideTheme.DrawPanelFrame(cell, AirsideTheme.SafetyYellow);
                if (GUI.Button(cell, GUIContent.none, GUIStyle.none))
                    _liveryChoice = i;
            }

            var ladder = Styled(label, "setup-ladder", s => AirsideTheme.TextStyle(new GUIStyle(s)
                { fontSize = 12, fontStyle = FontStyle.Bold }, AirsideTheme.OpenSky));
            var blurb = Styled(label, "setup-blurb", s => AirsideTheme.TextStyle(new GUIStyle(s)
                { fontSize = 13, wordWrap = true }, AirsideTheme.OpenSky));
            if (!hasSave)
            {
                GUI.Label(new Rect(x, rect.y + 208f, inner, 18f),
                    $"Saab 340B  →  ATR 42  →  Dash 8  →  jets   ·   ${AirlineCareerState.StartingFunds:N0} float",
                    ladder);
                GUI.Label(new Rect(x, rect.y + 230f, inner, 56f),
                    "One Saab on the regional bays. Take contracts, earn enough for the ATR, "
                    + "then unlock bigger metal step by step. Dispatch comes out of your float; "
                    + "every flight pays, contracts add the bonus that buys the next aircraft.",
                    blurb);
            }
            else
            {
                GUI.Label(new Rect(x, rect.y + 208f, inner, 40f),
                    "A new airline replaces your saved one.", blurb);
            }

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
            if (!FleetMode)
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

            var overview = _activeWorkspace == HudWorkspace.None && !_devToolsOpen;
            _hudPanels.Add(placement.TopBar);
            // The objective card is persistent: it is still on screen, and still swallowing
            // clicks, while a workspace is open beside it.
            if (overview || showGuide || ObjectiveSurvivesWorkspace(layout))
                _hudPanels.Add(placement.Objective);
            if (overview && placement.Operations.width > 0f)
                _hudPanels.Add(placement.Operations);
            if (_activeWorkspace != HudWorkspace.None || _devToolsOpen)
                _hudPanels.Add(placement.Workspace);
            if (MiniMapShows)
            {
                var miniMap = FieldMiniMap.PanelFor(layout, placement);
                if (miniMap.width > 0f)
                    _hudPanels.Add(miniMap);
            }
            if (overview && TrySelectionHudCardRect(layout, placement, out var selectionCard))
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
        /// The current-objective card (ADR 0053): one contract or career action, a progress
        /// bar, and a single Next line. Safety yellow is reserved for that current priority.
        /// </summary>
        private void DrawObjectiveCard(Rect rect)
        {
            if (rect.height < 8f || rect.width < 8f)
                return;

            var objective = OperationsSummary.Objective(PlayerFleet(), _clock.Now, _operations.Clock,
                _operations.CareerState, _operations.MarketOffers());
            var chapters = _operations.CampaignChapters();
            AnnounceCampaignProgress(chapters);
            _shellDrawList.Clear();
            HudShellPainter.PaintObjective(_shellDrawList, Box(rect), objective, Campaign.Current(chapters)?.Caption);
            _hudPainter.Draw(_shellDrawList);
        }

        private int _campaignChaptersAnnounced = -1;

        /// <summary>A toast the moment a chapter's reward lands; quiet about chapters done before this session.</summary>
        private void AnnounceCampaignProgress(IReadOnlyList<CampaignChapter> chapters)
        {
            var rewarded = 0;
            CampaignChapter latest = null;
            foreach (var chapter in chapters)
            {
                if (!chapter.Rewarded)
                    break;
                rewarded++;
                latest = chapter;
            }

            if (_campaignChaptersAnnounced >= 0 && rewarded > _campaignChaptersAnnounced && latest != null)
            {
                var next = Campaign.Current(chapters);
                var nextLine = next != null && !next.Complete ? $" Next: Chapter {next.Number}, {next.Title}." : " Campaign complete!";
                ShowToast($"Chapter {latest.Number} complete: {latest.Title}. +${latest.Reward:N0}.{nextLine}");
                PlayUiClick();
            }

            _campaignChaptersAnnounced = rewarded;
        }

        /// <summary>True when this window is wide enough to keep the objective card beside a workspace.</summary>
        private static bool ObjectiveSurvivesWorkspace(HudLayout layout) =>
            HudShell.ObjectiveSurvivesWorkspace(layout.Viewport.x, layout.Viewport.y);

        private static HudBox Box(Rect rect) => new(rect.x, rect.y, rect.width, rect.height);

        // ---- Top bar and workspaces ---------------------------------------------------------

        private GUIStyle _hudPrimaryButton;
        private GUIStyle _hudDestructiveButton;

        /// <summary>
        /// Slim persistent strip: airline, Adelaide time, funds, reliability, tier and the
        /// five workspaces. Operations stays visually selected on the default overview.
        /// </summary>
        private void DrawTopBar(AirlineHudLayout placement)
        {
            var bar = Box(placement.TopBar);
            var nav = Box(placement.NavStrip);
            var airline = _operations.PlayerAirline;
            var career = _operations.CareerState;

            _topBarValues[0] = $"ADELAIDE  {StampText(_clock.Now)}";
            _topBarValues[1] = $"${career.Funds:N0}";
            _topBarValues[2] = $"RELIABILITY {career.Reliability}%";
            _topBarValues[3] = career.Tier.ToString().ToUpperInvariant();
            HudShell.FillSegments(bar, airline.Name, nav, _topBarValues, _topBarSegments);
            HudShell.FillTabs(nav, _activeWorkspace, _navTabs);

            _shellDrawList.Clear();
            HudShellPainter.PaintTopBar(_shellDrawList, bar, nav, airline.Name, airline.LiveryHex,
                _topBarSegments, _navTabs);
            var clicked = _hudPainter.Draw(_shellDrawList);

            // The approved brand mark, over the livery bar the painter lays down for it.
            var mark = AirsideTheme.AppMarkLight;
            if (mark != null)
            {
                var markBox = HudShell.MarkBox(bar);
                GUI.DrawTexture(HudPainter.ToRect(markBox), mark, ScaleMode.ScaleToFit, true);
            }

            if (clicked == null)
                return;

            foreach (var (workspace, _) in HudShell.Tabs)
            {
                if (clicked != HudShellPainter.WorkspaceAction(workspace))
                    continue;
                if (workspace == HudWorkspace.Map)
                    TogglePlanner();
                else
                    SetWorkspace(workspace);
                return;
            }
        }

        /// <summary>Compact player-only Operations list. AI traffic stays on the full board.</summary>
        private void DrawCompactOperations(Rect area, GUIStyle panel, GUIStyle label, GUIStyle small)
        {
            if (area.width < 8f || area.height < 8f)
                return;

            var fleet = PlayerFleet();
            OperationsSummary.FillPlayerRows(fleet, _clock.Now, _compactOpsRows);
            var available = OperationsSummary.AvailableCount(fleet, _clock.Now);
            var height = Mathf.Min(area.height, 36f + _compactOpsRows.Count * 28f + 28f);
            var rect = new Rect(area.x, area.y, area.width, height);
            AirsideTheme.DrawOpaquePanel(rect);
            GUI.Box(rect, GUIContent.none, panel);

            var mute = Styled(small, "ops-mute", s => AirsideTheme.TextStyle(
                new GUIStyle(s) { fontSize = 11, fontStyle = FontStyle.Bold }, AirsideTheme.Concrete));
            var bold = Styled(label, "ops-reg", s => new GUIStyle(s) { fontSize = 13, fontStyle = FontStyle.Bold });
            GUI.Label(new Rect(rect.x + 14f, rect.y + 8f, rect.width - 28f, 16f), "OPERATIONS", mute);

            var y = rect.y + 28f;
            var inner = rect.width - 28f;
            foreach (var row in _compactOpsRows)
            {
                var rowRect = new Rect(rect.x + 10f, y, inner + 8f, 26f);
                if (row.IsPriority)
                    DrawSolid(rowRect, new Color(AirsideTheme.CoastalBlue.r, AirsideTheme.CoastalBlue.g, AirsideTheme.CoastalBlue.b, 0.28f));
                else if (_selectedAircraftId == row.Registration)
                    DrawSolid(rowRect, new Color(AirsideTheme.CoastalBlue.r, AirsideTheme.CoastalBlue.g, AirsideTheme.CoastalBlue.b, 0.18f));

                DrawSolid(new Rect(rowRect.x + 4f, rowRect.y + 7f, 4f, 12f),
                    row.IsPriority ? AirsideTheme.CoastalBlue : AirsideTheme.ClearGreen);
                GUI.Label(new Rect(rowRect.x + 14f, rowRect.y + 4f, 72f, 18f), row.Registration, bold);
                GUI.Label(new Rect(rowRect.x + 90f, rowRect.y + 4f, 48f, 18f), row.Route.ToUpperInvariant(), small);
                var stateStyle = row.IsPriority
                    ? Styled(small, "ops-priority", s => AirsideTheme.TextStyle(new GUIStyle(s) { fontStyle = FontStyle.Bold }, AirsideTheme.SafetyYellow))
                    : small;
                GUI.Label(new Rect(rowRect.x + 140f, rowRect.y + 4f, rowRect.width - 148f, 18f), row.State, stateStyle);
                if (GUI.Button(rowRect, GUIContent.none, GUIStyle.none))
                {
                    foreach (var aircraft in fleet)
                    {
                        if (aircraft.Registration == row.Registration)
                        {
                            SelectAircraft(aircraft);
                            break;
                        }
                    }
                }

                y += 28f;
            }

            GUI.Label(new Rect(rect.x + 14f, rect.yMax - 22f, inner, 16f),
                available == 1 ? "1 aircraft available" : $"{available} aircraft available", mute);
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
                GuideStep.WaitForDeparture => ("2 · Getting ready",
                    $"{reg} is fuelling, catering and boarding, then leaves at {ClockText(aircraft.Scheduled.Value.DepartAt)} Adelaide time. The airport runs in real time — click the aircraft on the field, or press Follow (F)."),
                GuideStep.Departing => ("3 · Departing",
                    $"{reg} is heading out. Click it on the field (or press Follow) to ride along through the taxi and takeoff."),
                GuideStep.Away => ("4 · Away to " + dest,
                    "Flights take real time. Track it on the route map (Tab), or close the game — the airport keeps running and tells you what happened."),
                GuideStep.Landing => ("5 · Coming home",
                    $"The tower is bringing {reg} in to land. Click the aircraft on final (or Follow) to watch the touchdown."),
                GuideStep.ChooseStand => ("6 · Parking",
                    $"{reg} has landed and is taking a stand on its own."),
                GuideStep.TaxiingIn => ("6 · Taxiing in",
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

        // ---- Fleet and contracts ---------------------------------------------------------

        private void AssignStandFromHud(FleetAircraft aircraft, StableId stand)
        {
            var result = _operations.AssignStand(aircraft, stand);
            if (result.Accepted)
                SaveAirline();
            else
                ShowToast(result.Reason);
        }

        /// <summary>
        /// Contextual selected-aircraft card: one dominant action, prep state for booked
        /// departures, and Cancel as a smaller red secondary.
        /// </summary>
        private void DrawSelectionHudCard(HudLayout hud, AirlineHudLayout placement, GUIStyle panel, GUIStyle label, GUIStyle small,
            GUIStyle smallButton)
        {
            if (!TrySelectionHudCardRect(hud, placement, out var rect, out var aircraft))
                return;

            AirsideTheme.DrawOpaquePanel(rect);
            GUI.Box(rect, GUIContent.none, panel);
            var accent = AirsideTheme.FromHex(aircraft.Airline.LiveryHex);
            DrawSolid(new Rect(rect.x, rect.y, 4f, rect.height), accent);

            var bold = Styled(label, "sel-title", s => new GUIStyle(s) { fontSize = 16, fontStyle = FontStyle.Bold });
            var mute = Styled(small, "sel-mute", s => AirsideTheme.TextStyle(new GUIStyle(s) { wordWrap = false }, AirsideTheme.Concrete));
            var x = rect.x + 16f;
            var inner = rect.width - 32f;
            GUI.Label(new Rect(x, rect.y + 10f, inner, 22f),
                $"{aircraft.Registration}  ·  {aircraft.Type.Name}", bold);
            GUI.Label(new Rect(x, rect.y + 32f, inner, 18f), SelectionRouteLine(aircraft), mute);
            GUI.Label(new Rect(x, rect.y + 50f, inner, 18f), SelectionLiveStats(aircraft), mute);

            var y = rect.y + 72f;
            if (ShowsDeparturePrep(aircraft))
                DrawDeparturePrepChecks(aircraft, x, y, inner, small);

            if (!aircraft.Airline.IsPlayer)
                return;

            var action = OperationsSummary.PrimaryAction(aircraft, _clock.Now);
            var primary = _hudPrimaryButton ??= AirsideTheme.PrimaryButtonStyle(
                new GUIStyle(smallButton) { fontSize = 14, fontStyle = FontStyle.Bold });
            var destructive = _hudDestructiveButton ??= AirsideTheme.DestructiveButtonStyle(
                new GUIStyle(smallButton) { fontSize = 13 });

            var canCancel = aircraft.State == FleetState.AtStand && aircraft.Scheduled.HasValue;
            var primaryWidth = canCancel ? inner - 118f : inner;
            var primaryRect = new Rect(x, rect.yMax - 52f, primaryWidth, 40f);
            if (IsGuided(aircraft, GuideStep.PlanFirstFlight) && action == AircraftHudAction.PlanFlight)
                DrawGuideHighlight(primaryRect);
            if (GUI.Button(primaryRect, OperationsSummary.ActionLabel(action).ToUpperInvariant(), primary))
                RunSelectionAction(aircraft, action);

            if (canCancel)
            {
                var cancelRect = new Rect(x + primaryWidth + 10f, rect.yMax - 52f, 108f, 40f);
                AirsideTheme.DrawPanelFrame(cancelRect, AirsideTheme.SignalRed);
                if (GUI.Button(cancelRect, "CANCEL", destructive))
                    CancelPlannedFlight(aircraft);
            }
        }

        private string SelectionRouteLine(FleetAircraft aircraft)
        {
            if (aircraft.Scheduled.HasValue)
            {
                var booked = aircraft.Scheduled.Value;
                return $"Adelaide → {booked.Destination.Name}  ·  Departs {ClockText(booked.DepartAt)}";
            }

            if (aircraft.CurrentDestination.HasValue)
                return $"Adelaide → {aircraft.CurrentDestination.Value.Name}";
            return StandNames.Display(aircraft.Stand);
        }

        private float DrawDeparturePrepChecks(FleetAircraft aircraft, float x, float y, float width, GUIStyle small)
        {
            var prep = DeparturePrep.For(aircraft, _clock.Now);
            var slot = width / 3f;
            DrawPrepCheck(x, y, slot, "Fuel", prep.FuelProgress, prep.Stage == DeparturePrepStage.Fuel, small);
            DrawPrepCheck(x + slot, y, slot, "Catering", prep.CateringProgress, prep.Stage == DeparturePrepStage.Catering, small);
            DrawPrepCheck(x + slot * 2f, y, slot, "Boarding", prep.BoardingProgress, prep.Stage == DeparturePrepStage.Boarding, small);
            return y + 22f;
        }

        private void DrawPrepCheck(float x, float y, float width, string name, double progress, bool active, GUIStyle small)
        {
            var done = progress >= 1;
            var colour = done ? AirsideTheme.ClearGreen : active ? AirsideTheme.SafetyYellow : AirsideTheme.Concrete;
            var mark = done ? "✓" : active ? "●" : "○";
            var text = active ? $"{name} {DeparturePrep.Percent(progress)}%" : name;
            var style = Styled(small, done ? "prep-done" : active ? "prep-active" : "prep-wait",
                s => AirsideTheme.TextStyle(new GUIStyle(s) { fontStyle = FontStyle.Bold }, colour));
            GUI.Label(new Rect(x, y, width, 18f), $"{mark}  {text}", style);
        }

        private void RunSelectionAction(FleetAircraft aircraft, AircraftHudAction action)
        {
            switch (action)
            {
                case AircraftHudAction.PlanFlight:
                case AircraftHudAction.ViewPlan:
                    OpenPlanner(aircraft);
                    break;
                case AircraftHudAction.TrackFlight:
                    SelectAircraft(aircraft);
                    break;
                case AircraftHudAction.AssignStand:
                    var stand = _operations.SuggestStand(aircraft);
                    if (stand.HasValue)
                        AssignStandFromHud(aircraft, stand.Value);
                    else
                        ShowToast("All stands occupied — wait for one to clear.");
                    break;
                case AircraftHudAction.StartCheck:
                    StartCheckFromHud(aircraft);
                    break;
            }
        }

        private static bool ShowsDeparturePrep(FleetAircraft aircraft) =>
            aircraft != null && aircraft.Airline.IsPlayer
            && aircraft.State == FleetState.AtStand && aircraft.Scheduled.HasValue;

        /// <summary>
        /// Followed aircraft, else the one the player clicked. Nothing when the
        /// field is just being watched — used by the speed readout so a parked
        /// Rex does not keep a strip on screen after deselect.
        /// </summary>
        private FleetAircraft WatchedAircraft()
        {
            if (_operations == null)
                return null;
            if (_cameraController != null && _cameraController.IsFollowing)
            {
                var target = _cameraController.FollowTarget;
                foreach (var pair in _fleetViewById)
                {
                    if (pair.Value == target && _fleetAircraftById.TryGetValue(pair.Key, out var followed))
                        return followed;
                }
            }

            if (!string.IsNullOrEmpty(_selectedAircraftId)
                && _fleetAircraftById.TryGetValue(_selectedAircraftId, out var selected))
                return selected;

            return null;
        }

        private FleetAircraft SelectionCardAircraft()
        {
            var watched = WatchedAircraft();
            if (watched != null)
                return watched;

            var fleet = PlayerFleet();
            var priority = OperationsSummary.PriorityAircraft(fleet, _clock.Now);
            if (priority != null && AircraftStatus.Severity(priority, _clock.Now) >= StatusSeverity.Attention)
                return priority;
            return null;
        }

        private bool TrySelectionHudCardRect(HudLayout hud, AirlineHudLayout placement, out Rect rect)
        {
            return TrySelectionHudCardRect(hud, placement, out rect, out _);
        }

        private bool TrySelectionHudCardRect(HudLayout hud, AirlineHudLayout placement, out Rect rect, out FleetAircraft aircraft)
        {
            rect = default;
            aircraft = SelectionCardAircraft();
            if (aircraft == null || placement.SelectedCard.height < 8f)
                return false;
            if (_activeWorkspace != HudWorkspace.None || _devToolsOpen)
                return false;
            var height = ShowsDeparturePrep(aircraft) ? 168f
                : aircraft.Airline.IsPlayer ? 134f
                : 88f;
            height = Mathf.Min(height, placement.SelectedCard.height);
            var bottom = placement.SelectedCard.yMax;
            if (FleetMode)
                bottom = Mathf.Min(bottom, hud.SpeedReadout.y - HudLayout.ReadoutGap);
            rect = new Rect(placement.SelectedCard.x, bottom - height,
                placement.SelectedCard.width, height);
            return true;
        }

        private string SelectionLiveStats(FleetAircraft aircraft)
        {
            if (!_fleetViewById.TryGetValue(aircraft.Registration, out var view)
                || view == null || !view.gameObject.activeSelf)
                return StatusText(aircraft);

            CommercialFlight flight = null;
            for (var i = 0; i < VisualFlights.Count; i++)
            {
                if (VisualFlights[i].AircraftId == aircraft.Registration)
                {
                    flight = VisualFlights[i];
                    break;
                }
            }

            var type = aircraft.Type;
            var knots = flight != null && FleetGroundSpeed(flight) is { } groundSpeed
                ? CircuitProfile.ToKnots(groundSpeed)
                : flight != null
                    ? AirsideFlightPath.AirspeedKnots(flight.Operation.Phase, VisualPhaseProgress(flight, 0f), type)
                    : 0f;
            return ReadoutText(knots, view);
        }

        private void SelectAircraft(FleetAircraft aircraft)
        {
            _selectedAircraftId = aircraft.Registration;
            var plannerStaysOpen = _activeWorkspace == HudWorkspace.Map && aircraft.Airline.IsPlayer;
            if (aircraft.Airline.IsPlayer)
                SetPlanningAircraft(aircraft);

            var onField = _fleetViewById.ContainsKey(aircraft.Registration);
            var following = AirsideSettings.Current.FollowOnSelect
                && TryFollowFleetAircraft(aircraft.Registration);
            _devToolsOpen = false;
            if (plannerStaysOpen)
            {
                // Switching aircraft inside the planner keeps planning; the camera still follows.
            }
            else if (following || onField)
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
                    ? StandDepartureStatus(aircraft)
                    : Maintenance.InCheck(aircraft, _clock.Now)
                        ? $"{StandNames.Display(aircraft.Stand)} · {Maintenance.Status(aircraft, _clock.Now, _operations.Clock)}"
                        : $"On {StandNames.Display(aircraft.Stand)} · no flight planned",
                FleetState.TaxiOut => $"Taxiing to runway {RunwayWeather.Label(aircraft.AssignedRunway)} · {dest}",
                FleetState.HoldingShort => $"Holding {RunwayWeather.Label(aircraft.AssignedRunway)} · {dest}{wait}",
                FleetState.TakingOff => FlightBoard.PhaseLabel(aircraft, _clock.Now) == "Lining up"
                    ? $"Lining up {RunwayWeather.Label(aircraft.AssignedRunway)} for {dest}"
                    : $"Departing {RunwayWeather.Label(aircraft.AssignedRunway)} for {dest}",
                FleetState.Outbound => $"Departed for {dest}{EnrouteAltitudeText(aircraft)} · lands {ends}",
                FleetState.AtDestination => $"Away at {dest} · departs {ends}",
                FleetState.Inbound => $"Inbound from {dest}{EnrouteAltitudeText(aircraft)} · {ends}",
                FleetState.HoldingForLanding =>
                    $"On final {ApproachSide(aircraft.AssignedRunway)} for runway {RunwayWeather.Label(aircraft.AssignedRunway)}{wait}",
                FleetState.GoAround => $"Going around, runway {RunwayWeather.Label(aircraft.AssignedRunway)}",
                FleetState.Landing => FlightBoard.PhaseLabel(aircraft, _clock.Now) switch
                {
                    "Go-around" => $"Going around, runway {RunwayWeather.Label(aircraft.AssignedRunway)}",
                    "Vacating" => $"Vacating runway {RunwayWeather.Label(aircraft.AssignedRunway)}",
                    "On final" => $"On final for runway {RunwayWeather.Label(aircraft.AssignedRunway)}",
                    _ => $"Landing runway {RunwayWeather.Label(aircraft.AssignedRunway)}"
                },
                FleetState.AwaitingStand => $"Landed · parking{wait}",
                FleetState.TaxiIn => $"Taxiing to {StandNames.Display(aircraft.Stand)}",
                _ => aircraft.State.ToString()
            };
        }

        private static string ApproachSide(RunwayDirection runway) => runway switch
        {
            RunwayDirection.Runway23 => "from the north-east",
            RunwayDirection.Runway12 => "from the north-east",
            RunwayDirection.Runway30 => "from the south-west",
            _ => "over the gulf"
        };

        private string StandDepartureStatus(FleetAircraft aircraft)
        {
            var dest = aircraft.Scheduled.Value.Destination.Name;
            var when = ClockText(aircraft.Scheduled.Value.DepartAt);
            var prep = DeparturePrep.For(aircraft, _clock.Now);
            if (!prep.Ready)
                return $"On {StandNames.Display(aircraft.Stand)} · {prep.Label} · {AirlineClock.DurationText(prep.RemainingSeconds)} left · departs {when} for {dest}";
            return $"On {StandNames.Display(aircraft.Stand)} · ready · departs {when} for {dest}";
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
                _departureDelaySeconds = FlightPlanner.ClampDelay(
                    aircraft.Scheduled.Value.DepartAt.ElapsedSeconds - _clock.Now.ElapsedSeconds, aircraft.Type);
            }
            else if (_mapSelection.HasValue && aircraft != null && !_operations.CanOperate(aircraft, _mapSelection.Value))
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
            if (_activeWorkspace == HudWorkspace.Operations)
                _boardScrollSnapToDay = true;
            if (_activeWorkspace != HudWorkspace.None)
                _devToolsOpen = false;
            // Reset to the live name each time Stats opens, so a draft left over from a
            // previous visit (typed, then closed without renaming) never resurfaces stale.
            if (_activeWorkspace == HudWorkspace.Stats)
                _statsRenameDraft = _operations?.PlayerAirline?.Name ?? string.Empty;
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
        private bool _mapRivalsVisible = true;
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
                RouteMap.FlightPoint(flight.From.Latitude, flight.From.Longitude, flight.To.Latitude, flight.To.Longitude,
                    flight.Progress, flight.Aircraft.Registration, out flight.Latitude, out flight.Longitude);
                _mapFlights.Add(flight);
            }
        }

        private void DrawDestinationsMap(Rect rect, GUIStyle panel, GUIStyle title, GUIStyle label, GUIStyle small, GUIStyle smallButton)
        {
            var aircraft = _mapAircraft;
            var home = _operations.Home;
            var workspaceLayout = RouteMapWorkspaceLayout.Create(Box(rect));
            var mapRect = HudPainter.ToRect(workspaceLayout.Map);
            var ink = AirsideTheme.RunwayInk;

            // Surface, title and the destination detail pane come from the shared painter;
            // the live map inside it keeps its own zoom, pan, tracking and pointer handling.
            _routeMapWorkspace.Rebuild(_operations, aircraft, _mapSelection, _departureDelaySeconds,
                _clock.Now, _mapFilter);
            RouteMapWorkspacePainter.Paint(_workspaceDrawList, _routeMapWorkspace, workspaceLayout);
            var chromeAction = _hudPainter.Draw(_workspaceDrawList);

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
                RouteMap.FlightPoint(flight.From.Latitude, flight.From.Longitude, flight.To.Latitude, flight.To.Longitude,
                    Math.Max(0.0, Math.Min(1.0, flight.Progress + step)), flight.Aircraft.Registration,
                    out var aheadLat, out var aheadLon);
                var ahead = Project(mapRect, aheadLon, aheadLat);
                var delta = step > 0 ? ahead - flight.Point : flight.Point - ahead;
                if (flight.Aircraft.State == FleetState.AtDestination)
                {
                    // Parked at the far end, nose pointing home.
                    delta = homePoint - flight.Point;
                }
                flight.HeadingDegrees = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg + 90f;
                _mapFlights[i] = flight;
                if (flight.Aircraft.Airline.IsPlayer || _mapRivalsVisible)
                {
                    _mapAircraftRows.Add(flight.Aircraft);
                    _mapAircraftPoints.Add((flight.Point.x, flight.Point.y));
                }
            }

            // Map controls in the top-left corner; the pointer handler leaves them alone.
            _mapControlRects.Clear();
            // Bottom-left: the AVAILABLE / LOCKED pills own the top-left corner now.
            var trackRect = new Rect(mapRect.x + 12f, mapRect.yMax - 58f, 170f, 24f);
            var zoomOutRect = new Rect(trackRect.xMax + 6f, trackRect.y, 90f, 24f);
            var rivalRect = HudPainter.ToRect(workspaceLayout.RivalsToggle);
            var rivalFlights = 0;
            for (var i = 0; i < _mapFlights.Count; i++)
                if (!_mapFlights[i].Aircraft.Airline.IsPlayer)
                    rivalFlights++;
            var selectedFlight = -1;
            for (var i = 0; i < _mapFlights.Count; i++)
                if (_mapFlights[i].Aircraft.Registration == _selectedAircraftId
                    && (_mapFlights[i].Aircraft.Airline.IsPlayer || _mapRivalsVisible))
                    selectedFlight = i;
            var showTrack = tracked >= 0 || selectedFlight >= 0;
            if (showTrack)
                _mapControlRects.Add(trackRect);
            if (_mapLens.Zoom > AustraliaMapLens.MinZoom + 0.01f)
                _mapControlRects.Add(zoomOutRect);
            _mapControlRects.Add(rivalRect);
            _mapControlRects.Add(HudPainter.ToRect(workspaceLayout.FilterBox(0)));
            _mapControlRects.Add(HudPainter.ToRect(workspaceLayout.FilterBox(1)));

            HandleMapPointer(mapRect);
            ApplyMapZoomEasing(mapRect);

            // Coastline, borders, every route from Adelaide and the destination dots, from the
            // same painter the offline mockups render, so what is compared is what is drawn.
            _mapNetworkDrawList.Clear();
            RouteMapWorkspacePainter.PaintNetwork(_mapNetworkDrawList, workspaceLayout.Map, _mapLens,
                _mapDestinationRows, home, _mapSelection, _operations.PlayerAirline.LiveryHex,
                _routeMapWorkspace.AircraftRangeKm, _routeMapWorkspace.AircraftRangeLabel);
            _hudPainter.Draw(_mapNetworkDrawList);
            DrawMapLabels(mapRect);

            var mouse = Event.current.mousePosition;
            var hovered = mapRect.Contains(mouse) && !_mapPanning
                ? FlightPlanner.NearestWithin(_mapDestinationPoints, mouse.x, mouse.y)
                : -1;
            if (hovered >= 0 && !mapRect.Contains(new Vector2(_mapDestinationPoints[hovered].x, _mapDestinationPoints[hovered].y)))
                hovered = -1;

            // Routes flown right now: flown part solid, the rest faint, along the great circle.
            foreach (var flight in _mapFlights)
            {
                if (!flight.Aircraft.Airline.IsPlayer && !_mapRivalsVisible)
                    continue;
                var colour = AirsideTheme.FromHex(flight.Aircraft.Airline.LiveryHex);
                DrawGreatCircle(mapRect, flight.From, flight.To, 0.0, flight.Progress,
                    new Color(colour.r, colour.g, colour.b, 0.8f), 2f, flight.Aircraft.Registration);
                DrawGreatCircle(mapRect, flight.From, flight.To, flight.Progress, 1.0,
                    new Color(colour.r, colour.g, colour.b, 0.3f), 1.5f, flight.Aircraft.Registration);
            }

            // The hovered dot gets a ring and a faint route; the network painter has already
            // drawn every other route, dot and label.
            if (hovered >= 0)
            {
                var hoverPoint = new Vector2(_mapDestinationPoints[hovered].x, _mapDestinationPoints[hovered].y);
                if (!(_mapSelection.HasValue && _mapSelection.Value.Equals(_mapDestinationRows[hovered].Destination)))
                    DrawGreatCircle(mapRect, home, _mapDestinationRows[hovered].Destination, 0.0, 1.0,
                        new Color(AirsideTheme.Cloud.r, AirsideTheme.Cloud.g, AirsideTheme.Cloud.b, 0.35f), 1.5f);
                AirsideTheme.DrawPanelFrame(new Rect(hoverPoint.x - 9f, hoverPoint.y - 9f, 18f, 18f),
                    AirsideTheme.Cloud);
            }

            DrawLiveMapTraffic(mapRect, small);

            // Aircraft icons on top of everything else on the map.
            for (var i = 0; i < _mapFlights.Count; i++)
            {
                var flight = _mapFlights[i];
                if (!flight.Aircraft.Airline.IsPlayer && !_mapRivalsVisible)
                    continue;
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

            if (GUI.Button(rivalRect, $"RIVALS {rivalFlights} · {(_mapRivalsVisible ? "ON" : "OFF")}", smallButton))
            {
                _mapRivalsVisible = !_mapRivalsVisible;
                if (!_mapRivalsVisible && tracked >= 0 && !_mapFlights[tracked].Aircraft.Airline.IsPlayer)
                    _mapTrackId = null;
                PlayUiClick();
            }

            // Floating over the finished map, so the pills are never buried under a route.
            _mapFilterDrawList.Clear();
            RouteMapWorkspacePainter.PaintFilters(_mapFilterDrawList, _routeMapWorkspace, workspaceLayout);
            var filterAction = _hudPainter.Draw(_mapFilterDrawList);

            DispatchWorkspaceAction(chromeAction ?? filterAction);
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

        private void DrawGreatCircle(Rect clip, Destination from, Destination to, double t0, double t1, Color colour,
            float thickness, string routeKey = null)
        {
            if (t1 <= t0)
                return;
            const int segments = 32;
            var steps = Math.Max(1, (int)Math.Ceiling(segments * (t1 - t0)));
            PointOnRoute(from, to, t0, routeKey, out var lat, out var lon);
            var previous = Project(clip, lon, lat);
            for (var i = 1; i <= steps; i++)
            {
                var t = t0 + (t1 - t0) * i / steps;
                PointOnRoute(from, to, t, routeKey, out lat, out lon);
                var next = Project(clip, lon, lat);
                DrawClippedLine(clip, previous, next, colour, thickness);
                previous = next;
            }
        }

        private static void PointOnRoute(Destination from, Destination to, double t, string routeKey,
            out double lat, out double lon)
        {
            if (string.IsNullOrEmpty(routeKey))
                RouteMap.GreatCirclePoint(from.Latitude, from.Longitude, to.Latitude, to.Longitude, t, out lat, out lon);
            else
                RouteMap.FlightPoint(from.Latitude, from.Longitude, to.Latitude, to.Longitude, t, routeKey, out lat, out lon);
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
        /// <summary>
        /// Real airliners around South Australia from the live feed (ADR 0082): pale, beneath
        /// the game's own flights, callsign and height once zoomed in. Not selectable.
        /// </summary>
        private void DrawLiveMapTraffic(Rect mapRect, GUIStyle small)
        {
            if (!LiveTrafficHealthy)
                return;
            var since = Time.realtimeSinceStartup - _liveFetchedAt;
            var size = Mathf.Lerp(12f, 20f, Mathf.InverseLerp(1f, 12f, _mapLens.Zoom));
            var labelled = _mapLens.Zoom >= 4f;
            var cloud = AirsideTheme.Cloud;
            foreach (var aircraft in _liveAircraft)
            {
                if (LiveTraffic.ModelFor(aircraft.TypeCode) == null
                    || aircraft.PositionAgeSeconds + since > LiveTraffic.StaleSeconds)
                    continue;
                LiveTraffic.PositionAfter(aircraft, aircraft.PositionAgeSeconds + since, out var lat, out var lon);
                var point = Project(mapRect, lon, lat);
                if (!mapRect.Contains(point))
                    continue;
                LiveTraffic.PositionAfter(aircraft, aircraft.PositionAgeSeconds + since + 60.0, out var aheadLat, out var aheadLon);
                var ahead = Project(mapRect, aheadLon, aheadLat);
                var delta = ahead - point;
                var heading = delta.sqrMagnitude > 0.01f
                    ? Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg + 90f
                    : (float)aircraft.TrackDegrees;
                DrawPlaneIcon(point, size + 2f, heading, new Color(0f, 0f, 0f, 0.5f));
                DrawPlaneIcon(point, size, heading, new Color(cloud.r, cloud.g, cloud.b, 0.7f));
                if (!labelled)
                    continue;
                var height = aircraft.OnGround ? "ground"
                    : aircraft.AltitudeFeet.HasValue ? $"{aircraft.AltitudeFeet.Value:N0} ft" : string.Empty;
                var previous = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 0.75f);
                GUI.Label(new Rect(point.x + size * 0.6f, point.y - 9f, 220f, 18f),
                    $"LIVE {aircraft.Label} · {aircraft.TypeCode} · {height}", small);
                GUI.color = previous;
            }
        }

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

        private void PickDestination(Destination destination)
        {
            _mapSelection = destination;
            _mapMessage = null;
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

        /// <summary>
        /// State and region names over the network the shared painter has already drawn, plus
        /// the pointer hint along the bottom.
        /// </summary>
        private void DrawMapLabels(Rect mapRect)
        {
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

        private void DrawOperationsWorkspace(Rect rect)
        {
            var surface = Box(rect);
            FillEventHistory();
            _operationsWorkspace.Rebuild(_operations, _clock.Now,
                _flightsShowArrivals ? OperationsBoardTab.Arrivals : OperationsBoardTab.Departures,
                _selectedAircraftId, _eventHistory);

            var layout = OperationsWorkspaceLayout.Create(surface, _operationsWorkspace.Attention.Count);
            var nowRow = _operationsWorkspace.FirstActiveRowIndex;
            if (_boardScrollSnapToDay)
            {
                _boardScrollRow = nowRow;
                _boardScrollFollowRow = nowRow;
                _boardScrollSnapToDay = false;
            }
            else if (_boardScrollFollowRow >= 0 && _boardScrollRow == _boardScrollFollowRow)
            {
                // Still parked on the previous NOW — walk forward with the clock so a board
                // left open from 09:55 is not still showing 09:55 at 11:55.
                _boardScrollRow = nowRow;
                _boardScrollFollowRow = nowRow;
            }

            var beforeScroll = _boardScrollRow;
            _boardScrollRow = ScrollRows(_boardScrollRow, layout.Board,
                _operationsWorkspace.Rows.Count - layout.VisibleRows);
            if (_boardScrollRow != beforeScroll)
                _boardScrollFollowRow = -1;
            OperationsWorkspacePainter.Paint(_workspaceDrawList, _operationsWorkspace, layout,
                _selectedAircraftId, _boardScrollRow);
            DispatchWorkspaceAction(_hudPainter.Draw(_workspaceDrawList));
        }

        /// <summary>The player's own recent movements, newest first, for the event-history strip.</summary>
        private void FillEventHistory()
        {
            _eventHistory.Clear();
            var events = _operations.RecentEvents;
            for (var i = events.Count - 1; i >= 0 && _eventHistory.Count < 6; i--)
            {
                var e = events[i];
                if (!e.Aircraft.Airline.IsPlayer)
                    continue;
                _eventHistory.Add(new OperationsEventLine(ClockText(e.At),
                    $"{e.Aircraft.Registration} · {EventPhrase(e)}"));
            }
        }

        /// <summary>What the aircraft did, from the state the event recorded — not from now.</summary>
        private static string EventPhrase(FleetEvent e) => e.State switch
        {
            FleetState.TaxiOut => $"pushed back for {Where(e)}",
            FleetState.HoldingShort => "holding short for the runway",
            FleetState.TakingOff => $"departed for {Where(e)}",
            FleetState.Outbound => $"airborne for {Where(e)}",
            FleetState.AtDestination => $"landed at {Where(e)}",
            FleetState.Inbound => $"left {Where(e)} for Adelaide",
            FleetState.HoldingForLanding => "on final at Adelaide",
            FleetState.GoAround => "went around at Adelaide",
            FleetState.Landing => "landed at Adelaide",
            FleetState.AwaitingStand => "landed, waiting for a stand",
            FleetState.TaxiIn => $"taxiing to {StandNames.Display(e.Aircraft.Stand)}",
            FleetState.AtStand => $"on {StandNames.Display(e.Aircraft.Stand)}",
            _ => e.State.ToString()
        };

        private static string Where(FleetEvent e) =>
            e.Aircraft.CurrentDestination?.Name ?? e.Aircraft.Scheduled?.Destination.Name ?? "its route";

        /// <summary>The Fleet workspace (ADR 0057): your aircraft, their detail, the real market.</summary>
        private void DrawFleetWorkspace(Rect rect)
        {
            var surface = Box(rect);
            _fleetWorkspace.Rebuild(_operations, _clock.Now, _selectedAircraftId);
            var layout = FleetWorkspaceLayout.Create(surface, _fleetWorkspace.Market.Count);
            var rows = _fleetWorkspace.Mine.Count + _fleetWorkspace.Others.Count
                       + (_fleetWorkspace.Others.Count > 0 ? 1 : 0);
            _rosterScrollRow = ScrollRows(_rosterScrollRow, layout.Roster, rows - layout.VisibleRosterRows);
            FleetWorkspacePainter.Paint(_workspaceDrawList, _fleetWorkspace, layout, _selectedAircraftId,
                _rosterScrollRow);
            DispatchWorkspaceAction(_hudPainter.Draw(_workspaceDrawList));
        }

        /// <summary>The Contracts workspace (ADR 0057): the active commitment beside the market.</summary>
        private void DrawContractsWorkspace(Rect rect)
        {
            _contractsWorkspace.Rebuild(_operations, _clock.Now);
            var layout = ContractsWorkspaceLayout.Create(Box(rect), _contractsWorkspace.ActiveTerms.Count);
            ContractsWorkspacePainter.Paint(_workspaceDrawList, _contractsWorkspace, layout,
                _highlightedContractId);
            DispatchWorkspaceAction(_hudPainter.Draw(_workspaceDrawList));
        }

        /// <summary>The Stats workspace (ADR 0066): career overview, next-tier progress, milestones, contract history.</summary>
        private void DrawStatsWorkspace(Rect rect)
        {
            _statsWorkspace.Rebuild(_operations, _clock.Now);
            var layout = StatsWorkspaceLayout.Create(Box(rect));
            StatsWorkspacePainter.Paint(_workspaceDrawList, _statsWorkspace, layout);
            DispatchWorkspaceAction(_hudPainter.Draw(_workspaceDrawList));

            // Raw Unity IMGUI, not through the draw list: no primitive in the shared,
            // UnityEngine-free HudDrawList renders editable text, so the rename control is
            // drawn directly here — the one exception in an otherwise painter-driven page.
            _statsRenameDraft = GUI.TextField(HudPainter.ToRect(layout.RenameFieldBox), _statsRenameDraft ?? string.Empty, 24);
            if (GUI.Button(HudPainter.ToRect(layout.RenameButtonBox), "RENAME"))
            {
                var result = _operations.RenameAirline(_statsRenameDraft);
                if (result.Accepted)
                {
                    PlayUiClick();
                    SaveAirline();
                }
                else
                {
                    ShowToast(result.Reason);
                    _statsRenameDraft = _operations.PlayerAirline?.Name ?? string.Empty;
                }
            }
        }

        private string _highlightedContractId;

        /// <summary>
        /// Mouse-wheel scrolling for a list drawn as fixed rows. Returns the clamped first
        /// visible row; <paramref name="maxRow"/> is how far it may scroll before the last
        /// row is on screen.
        /// </summary>
        private static int ScrollRows(int current, HudBox area, int maxRow)
        {
            if (maxRow < 0)
                maxRow = 0;
            var ev = Event.current;
            if (ev != null && ev.type == EventType.ScrollWheel
                && area.Contains(ev.mousePosition.x, ev.mousePosition.y))
            {
                current += ev.delta.y > 0f ? 1 : -1;
                ev.Use();
            }

            return current < 0 ? 0 : current > maxRow ? maxRow : current;
        }

        /// <summary>
        /// Turns a click on the shared draw list into the one command it stands for. Every
        /// command still goes through AirlineOperations, which remains free to refuse it.
        /// </summary>
        private void DispatchWorkspaceAction(string action)
        {
            if (string.IsNullOrEmpty(action))
                return;

            switch (action)
            {
                case HudAction.Close:
                    TryCloseAirlineOverlay();
                    return;
                case HudAction.TabDepartures:
                    _flightsShowArrivals = false;
                    _boardScrollSnapToDay = true;
                    PlayUiClick();
                    return;
                case HudAction.TabArrivals:
                    _flightsShowArrivals = true;
                    _boardScrollSnapToDay = true;
                    PlayUiClick();
                    return;
                case HudAction.FilterAvailable:
                    _mapFilter = RouteMapFilter.Available;
                    PlayUiClick();
                    return;
                case HudAction.FilterLocked:
                    _mapFilter = RouteMapFilter.Locked;
                    PlayUiClick();
                    return;
                case HudAction.Primary:
                    RunPrimaryActionOnSelection();
                    return;
                case HudAction.Cancel:
                    if (TryFindFleetAircraft(_selectedAircraftId, out var cancelling))
                        CancelPlannedFlight(cancelling);
                    return;
                case HudAction.Track:
                    if (TryFindFleetAircraft(_selectedAircraftId, out var tracking))
                        SelectAircraft(tracking);
                    return;
                case HudAction.StartCheck:
                    if (TryFindFleetAircraft(_selectedAircraftId, out var checking))
                        StartCheckFromHud(checking);
                    return;
                case HudAction.ViewEligibleAircraft:
                    ShowEligibleContractAircraft();
                    return;
                case HudAction.PlanFlight:
                    ScheduleFromPlanner();
                    return;
                case HudAction.ResetMap:
                    _mapTrackId = null;
                    _mapSelection = null;
                    _mapLens.Reset();
                    PlayUiClick();
                    return;
                case HudAction.NextAircraft:
                    CycleSelection(1);
                    return;
                case HudAction.PreviousAircraft:
                    CycleSelection(-1);
                    return;
                case HudAction.NextDeparture:
                    _departureDelaySeconds = FlightPlanner.StepDelay(_departureDelaySeconds, 1);
                    PlayUiClick();
                    return;
                case HudAction.PreviousDeparture:
                    _departureDelaySeconds = FlightPlanner.StepDelay(_departureDelaySeconds, -1);
                    PlayUiClick();
                    return;
                case HudAction.ClearDestination:
                    _mapSelection = null;
                    _mapMessage = null;
                    PlayUiClick();
                    return;
            }

            var registration = HudAction.Payload(action, HudAction.SelectPrefix);
            if (registration.Length > 0)
            {
                if (TryFindFleetAircraft(registration, out var picked))
                    SelectAircraft(picked);
                return;
            }

            var typeId = HudAction.Payload(action, HudAction.BuyPrefix);
            if (typeId.Length > 0)
            {
                BuyAircraftFromHud(typeId);
                return;
            }

            var contractId = HudAction.Payload(action, HudAction.AcceptPrefix);
            if (contractId.Length > 0)
            {
                AcceptContractFromHud(contractId);
                return;
            }

            var code = HudAction.Payload(action, HudAction.DestinationPrefix);
            if (code.Length > 0 && DestinationCatalogue.TryFind(code, out var destination))
            {
                PickDestination(destination);
                return;
            }

            var liveryHex = HudAction.Payload(action, HudAction.LiveryPrefix);
            if (liveryHex.Length > 0 && _operations.SetLivery(liveryHex).Accepted)
                PlayUiClick();
        }

        private bool TryFindFleetAircraft(string registration, out FleetAircraft aircraft)
        {
            aircraft = null;
            if (string.IsNullOrEmpty(registration))
                return false;
            return _fleetAircraftById.TryGetValue(registration, out aircraft);
        }

        private void RunPrimaryActionOnSelection()
        {
            if (!TryFindFleetAircraft(_selectedAircraftId, out var aircraft))
                return;
            RunSelectionAction(aircraft, OperationsSummary.PrimaryAction(aircraft, _clock.Now));
        }

        /// <summary>Select the first aircraft that can actually progress the active contract.</summary>
        private void ShowEligibleContractAircraft()
        {
            var career = _operations.CareerState;
            if (career.ActiveContract == null
                || !career.TryFindDefinition(career.ActiveContract.DefinitionId, out var definition))
                return;
            foreach (var aircraft in _operations.FleetOf(_operations.PlayerAirline))
            {
                if (aircraft.Type.Id != definition.EligibleType.Id)
                    continue;
                SetWorkspace(HudWorkspace.Fleet);
                SelectAircraft(aircraft);
                return;
            }

            ShowToast($"You have no {definition.EligibleType.Name} to fly {definition.Id}.");
        }

        private void BuyAircraftFromHud(string typeId)
        {
            if (!AircraftType.TryFromId(typeId, out var type))
                return;
            var result = _operations.BuyAircraft(type);
            if (result.Accepted)
            {
                ShowToast(AircraftAcquisition.TryFor(type, out var offer)
                    ? $"Bought {Article.A(type.Name)} for ${offer.Price:N0}."
                    : $"Bought {Article.A(type.Name)}.");
                SaveAirline();
            }
            else
            {
                ShowToast(result.Reason);
            }

            PlayUiClick();
        }

        private void StartCheckFromHud(FleetAircraft aircraft)
        {
            var cost = Maintenance.CheckCost(aircraft.Type);
            var result = _operations.StartCheck(aircraft);
            if (result.Accepted)
            {
                ShowToast($"{aircraft.Registration} is in check until {ClockText(aircraft.CheckUntil.Value)} · ${cost:N0}.");
                SaveAirline();
            }
            else
            {
                ShowToast(result.Reason);
            }

            PlayUiClick();
        }

        private void AcceptContractFromHud(string contractId)
        {
            RouteContractDefinition definition = null;
            foreach (var offer in _operations.MarketOffers())
                if (offer.Id == contractId)
                    definition = offer;
            if (definition == null && !RouteContractCatalogue.TryFind(contractId, out definition))
                return;

            // A first click highlights the offer; the second commits, so an accidental click
            // on a card never signs a service commitment.
            if (_highlightedContractId != contractId)
            {
                _highlightedContractId = contractId;
                PlayUiClick();
                return;
            }

            var result = _operations.AcceptContract(definition);
            if (result.Accepted)
            {
                _highlightedContractId = null;
                ShowToast($"Accepted {definition.OriginCode} ↔ {definition.DestinationCode}: "
                          + $"{definition.RequiredRotations} rotations.");
                SaveAirline();
            }
            else
            {
                ShowToast(result.Reason);
            }

            PlayUiClick();
        }

        /// <summary>Book the trip the planner is showing, through the simulation's own rules.</summary>
        private void ScheduleFromPlanner()
        {
            if (_mapAircraft == null || !_mapSelection.HasValue)
                return;
            var departAt = _clock.Now.Advance(
                FlightPlanner.ClampDelay(_departureDelaySeconds, _mapAircraft.Type));
            var result = _operations.ScheduleDeparture(_mapAircraft, _mapSelection.Value, departAt);
            if (result.Accepted)
            {
                ShowToast($"{_mapAircraft.Registration} pushes back {ClockText(departAt)} "
                          + $"for {_mapSelection.Value.Name}.");
                _activeWorkspace = HudWorkspace.None;
                _mapSelection = null;
                SaveAirline();
            }
            else
            {
                _mapMessage = result.Reason;
                ShowToast(result.Reason);
            }

            PlayUiClick();
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
                    if (_operations.CanOperate(aircraft, destination))
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
                        ShowToast($"{reg} is on final at Adelaide.");
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
                else if (!string.IsNullOrEmpty(s.ContractDefinitionId))
                    ShowToast($"{reg} earned ${s.Payment:N0} on {s.ContractDefinitionId} ({s.RotationsCompleted} rotations so far).");
                else
                    ShowToast($"{reg} earned ${s.Payment:N0} from that rotation.");
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

        private string StampText(SimulationTime time) => (_operations?.Clock ?? AirlineClock.Default).StampText(time);

        private static string DurationText(long seconds) => AirlineClock.DurationText(seconds);
    }
}
