using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Airside.Domain;
using Airside.Persistence;
using Airside.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace Airside.Presentation
{
    public sealed class AirsidePrototype : MonoBehaviour
    {
        private ManualSimulationClock _clock;
        private AirportSimulation _simulation;
        private PersistentAirportSession _session;
        private Transform[] _commercialAircraft;
        private Transform[] _groundTraffic;
        private Light _sun;
        private Light _fillLight;
        private Light[] _apronLights;
        private Light[] _landsideLights;
        private Light[] _thresholdLights;
        private Light[] _alsLights;
        private Light[] _runwayEdgeLights;
        private Light _aerodromeBeacon;
        private ReflectionProbe _apronProbe;
        private ReflectionProbe _terminalProbe;
        private Transform _rainRoot;
        private Transform _touchdownSmoke;
        private Light _fuelFarmLight;
        private Light _arffBayLight;
        private Renderer _arffLightbarRenderer;
        private Transform _skidMarkRoot;
        private Transform _taxiSprayRoot;
        private Light[] _windowLights;
        private Transform _horizonDome;
        private Transform _sunDisc;
        private Transform _moonDisc;
        private Transform _cloudRoot;
        private Transform _cloudUmbraRoot;
        private Transform _birdFlockRoot;
        private Transform _apronLifeRoot;
        private readonly List<(Transform Person, Vector3 BasePos, bool Walker)> _apronPeople =
            new List<(Transform, Vector3, bool)>();
        private Transform _hangarDoor;
        private float _hangarDoorClosedX = -20f;
        private readonly List<(Transform Panel, float ClosedX, float OpenDelta)> _hangarDoorPanels =
            new List<(Transform, float, float)>();
        private Light _hangarBayLight;
        private AirsideDayVolume _dayVolume;
        private float _touchdownSmokeRemaining;
        private AirsideCanvasHud _canvasHud;
        private AirsideToolkitHud _toolkitHud;
        private bool _canvasHudActive;
        private AudioSource _touchdownAudio;
        private AudioClip _touchdownClip;
        private AudioSource _ambientWindAudio;
        private AudioSource _ambientRainAudio;
        private AudioSource _ambientCoastAudio;
        private readonly Dictionary<string, AircraftPhase> _previousPhases = new Dictionary<string, AircraftPhase>();
        private readonly HashSet<string> _touchdownFired = new HashSet<string>();
        private readonly List<(Renderer Renderer, Color DryColor, float DrySmoothness, float DryMetallic, float DryBumpScale, bool Paved)> _wetSurfaces =
            new List<(Renderer, Color, float, float, float, bool)>();

        // Last wetness pushed into the wet-surface materials; NaN forces the next pass to
        // re-apply (set on collect, so newly built surfaces such as Stand 3 pick up rain).
        private float _lastAppliedWetness = float.NaN;
        private readonly List<Renderer> _holdShortRenderers = new List<Renderer>();
        private readonly List<Renderer> _airfieldLightRenderers = new List<Renderer>();
        private readonly List<Renderer> _nightGlowRenderers = new List<Renderer>();
        private Transform _fuelTruck;
        private Transform _baggageCart;
        private Transform _passengerBus;
        private Transform _stairs;
        private Transform _chocks;
        private Transform _wetPuddleRoot;
        private Transform _gpuCart;
        private Transform _pushbackTug;
        private Transform _windsockSock;
        private Transform _terminalFlag;
        private Transform _coastFoam;
        private readonly List<Transform> _coastFoamLayers = new List<Transform>();
        private readonly List<(Transform Boat, Vector3 BasePos, float BaseYaw)> _coastBoats =
            new List<(Transform, Vector3, float)>();
        private readonly List<Renderer> _coastWaterRenderers = new List<Renderer>();
        private Transform _jettyDeck;
        private Transform _opsAntennaDish;
        private Transform _starFieldRoot;
        private bool _standThreeVisualBuilt;
        private Camera _mainCamera;
        private AirsideCameraController _cameraController;
        private double _preciseTime;
        private bool _paused;
        private bool _audioMuted;
        private int _speed = 1;
        private long _nextAutosaveSecond;
        private bool _showAwaySummary;
        private bool _showOpeningBriefing;
        private bool _routeOfferToastShown;
        private bool _firstRouteIncomeToastShown;
        private long _routeIncomeSeen;
        private int _acceptedRouteCountSeen;
        private const float EngineVolumeRunning = 0.11f;
        private const float EngineVolumeIdle = 0.02f;
        private const float EngineVolumePausedScale = 0.28f;
        private const float AmbientWindVolume = 0.045f;
        private const float AmbientRainVolume = 0.07f;
        private const float AmbientStormVolume = 0.11f;
        private const float AmbientCoastVolume = 0.035f;
        private float _apronProbeRefreshAt;
        private string _researchToast = string.Empty;
        private float _researchToastUntil;
        private float _saveIndicatorUntil;
        private int _seenEventCount;
        private string _opsToast = string.Empty;
        private float _opsToastUntil;
        private string _SavePath =>
            Path.Combine(Application.persistentDataPath, "airside-save-v1.json");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartPrototype()
        {
            if (FindFirstObjectByType<AirsidePrototype>() == null)
                new GameObject("Airside Prototype").AddComponent<AirsidePrototype>();
        }

        private void Awake()
        {
            _session = PersistentAirportSession.LoadOrCreate(_SavePath, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), 24031996);
            _clock = _session.Clock;
            _simulation = _session.Simulation;
            _preciseTime = _clock.Now.ElapsedSeconds;
            _nextAutosaveSecond = _clock.Now.ElapsedSeconds + 15;
            _showAwaySummary = _session.LastAwaySummary.HasReport;
            // New-game / early session: no away report and no accepted routes yet.
            _showOpeningBriefing = !_showAwaySummary && _simulation.Routes.Accepted.Count == 0;
            if (_showOpeningBriefing)
                _paused = true;
            _acceptedRouteCountSeen = _simulation.Routes.Accepted.Count;
            _routeIncomeSeen = _simulation.Economy.TotalRouteIncome;
            _firstRouteIncomeToastShown = _routeIncomeSeen > 0;
            _seenEventCount = _simulation.EventLog.Events.Count;

            BuildLightingAndCamera();
            _dayVolume = AirsideDayVolume.Ensure(transform);
            BuildAirfield();
            CollectNightGlowWindows();
            var fuelLamp = GameObject.Find("Fuel farm light");
            if (fuelLamp != null)
                _fuelFarmLight = fuelLamp.GetComponent<Light>();
            var arffLamp = GameObject.Find("ARFF bay light");
            if (arffLamp != null)
                _arffBayLight = arffLamp.GetComponent<Light>();
            _apronLights = BuildApronLights();
            _landsideLights = BuildLandsideStreetlights();
            _thresholdLights = BuildThresholdApproachLights();
            _alsLights = CollectAlsLights();
            _runwayEdgeLights = BuildRunwayEdgePointLights();
            _apronProbe = BuildApronReflectionProbe();
            _terminalProbe = BuildTerminalReflectionProbe();
            _aerodromeBeacon = BuildAerodromeBeacon();
            _rainRoot = BuildRainRoot();
            _touchdownSmoke = BuildTouchdownSmoke();
            _skidMarkRoot = BuildSkidMarkRoot();
            _taxiSprayRoot = BuildTaxiSprayRoot();
            _touchdownClip = CreateTouchdownClip();
            _touchdownAudio = gameObject.AddComponent<AudioSource>();
            _touchdownAudio.playOnAwake = false;
            _touchdownAudio.spatialBlend = 0.55f;
            _touchdownAudio.volume = 0.22f;
            _ambientWindAudio = gameObject.AddComponent<AudioSource>();
            _ambientWindAudio.clip = CreateWindClip();
            _ambientWindAudio.loop = true;
            _ambientWindAudio.playOnAwake = false;
            _ambientWindAudio.spatialBlend = 0f;
            _ambientWindAudio.volume = 0f;
            _ambientWindAudio.Play();
            _ambientRainAudio = gameObject.AddComponent<AudioSource>();
            _ambientRainAudio.clip = CreateRainClip();
            _ambientRainAudio.loop = true;
            _ambientRainAudio.playOnAwake = false;
            _ambientRainAudio.spatialBlend = 0f;
            _ambientRainAudio.volume = 0f;
            _ambientRainAudio.Play();
            _ambientCoastAudio = gameObject.AddComponent<AudioSource>();
            _ambientCoastAudio.clip = CreateCoastClip();
            _ambientCoastAudio.loop = true;
            _ambientCoastAudio.playOnAwake = false;
            _ambientCoastAudio.spatialBlend = 0f;
            _ambientCoastAudio.volume = 0f;
            _ambientCoastAudio.Play();
            CollectWetSurfaces();
            BuildWetPuddles();
            CollectHoldShortMarkings();
            CollectAirfieldLights();
            var hangarDoor = GameObject.Find("Hangar door");
            if (hangarDoor != null)
            {
                _hangarDoor = hangarDoor.transform;
                _hangarDoorClosedX = _hangarDoor.position.x;
            }

            CollectHangarDoorPanels();
            CollectCoastalMotionTargets();
            _opsAntennaDish = GameObject.Find("antenna_dish")?.transform;
            _terminalFlag = GameObject.Find("flag_cloth")?.transform;

            var hangarBayLightGo = GameObject.Find("Hangar bay light");
            if (hangarBayLightGo == null)
            {
                hangarBayLightGo = new GameObject("Hangar bay light");
                hangarBayLightGo.transform.position = new Vector3(-20f, 3.2f, 20.5f);
                var bay = hangarBayLightGo.AddComponent<Light>();
                bay.type = LightType.Point;
                bay.color = new Color(1f, 0.88f, 0.62f);
                bay.range = 14f;
                bay.intensity = 0.2f;
            }
            _hangarBayLight = hangarBayLightGo.GetComponent<Light>();
            var dome = GameObject.Find("Horizon dome");
            if (dome != null)
                _horizonDome = dome.transform;
            var clouds = GameObject.Find("Cloud bands");
            if (clouds != null)
                _cloudRoot = clouds.transform;
            var umbras = GameObject.Find("Cloud umbras");
            if (umbras != null)
                _cloudUmbraRoot = umbras.transform;
            _commercialAircraft = Array.Empty<Transform>();
            SyncCommercialAircraftViews();
            _groundTraffic = new Transform[_simulation.GroundTraffic.Count];
            for (var index = 0; index < _groundTraffic.Length; index++)
                _groundTraffic[index] = BuildGroundTrafficAircraft(_simulation.GroundTraffic[index].Id.Value);
            _fuelTruck = BuildServiceVehicle("Fuel truck", new Color(0.95f, 0.76f, 0.12f), new Vector3(3.1f, 1.25f, 1.35f),
                PreferArtKit(
                    "Models/Vehicles/mdl_fuel_truck_small_v06.gltf",
                    "Models/Vehicles/mdl_fuel_truck_small_v05.gltf",
                    "Models/Vehicles/mdl_fuel_truck_small_authored_v01.gltf",
                    "Models/Vehicles/mdl_fuel_truck_small_v04.gltf",
                    "Models/Vehicles/mdl_fuel_truck_small_v03.gltf",
                    "Models/Vehicles/mdl_fuel_truck_small_v02.gltf",
                    "Models/Vehicles/mdl_fuel_truck_small_v01.gltf"));
            _baggageCart = BuildServiceVehicle("Baggage cart", new Color(0.91f, 0.38f, 0.12f), new Vector3(2.3f, 0.8f, 1.15f),
                PreferArtKit(
                    "Models/Vehicles/mdl_baggage_tug_train_v06.gltf",
                    "Models/Vehicles/mdl_baggage_tug_train_v05.gltf",
                    "Models/Vehicles/mdl_baggage_tug_train_authored_v01.gltf",
                    "Models/Vehicles/mdl_baggage_tug_train_v04.gltf",
                    "Models/Vehicles/mdl_baggage_tug_train_v03.gltf",
                    "Models/Vehicles/mdl_baggage_tug_train_v02.gltf",
                    "Models/Vehicles/mdl_baggage_tug_train_v01.gltf"));
            _passengerBus = BuildServiceVehicle("Passenger bus", new Color(0.22f, 0.44f, 0.55f), new Vector3(3.8f, 1.5f, 1.45f),
                PreferArtKit(
                    "Models/Vehicles/mdl_passenger_bus_apron_v06.gltf",
                    "Models/Vehicles/mdl_passenger_bus_apron_v05.gltf",
                    "Models/Vehicles/mdl_passenger_bus_apron_authored_v01.gltf",
                    "Models/Vehicles/mdl_passenger_bus_apron_v04.gltf",
                    "Models/Vehicles/mdl_passenger_bus_apron_v03.gltf",
                    "Models/Vehicles/mdl_passenger_bus_apron_v02.gltf",
                    "Models/Vehicles/mdl_passenger_bus_apron_v01.gltf"));
            _stairs = BuildStairs();
            _chocks = BuildChocks();
            _gpuCart = BuildGpuCart();
            _pushbackTug = BuildPushbackTug();
            _windsockSock = BuildWindsock();
            EnsureStandThreeVisual();
            if (_commercialAircraft.Length > 0)
                _cameraController.SetFollowTargets(_commercialAircraft);

            _canvasHud = AirsideCanvasHud.Create(transform);
            _toolkitHud = AirsideToolkitHud.Create(transform);
            Action onAccept = () => TryAcceptPendingRouteFromHotkey();
            Action onDecline = () =>
            {
                if (_simulation.Routes.Pending != null && !_simulation.IsInsolvent)
                    _session.DeclineRoute();
            };
            _canvasHud.BindActions(
                onAccept: onAccept,
                onDecline: onDecline,
                onPriorityCrew: () =>
                {
                    if (!_simulation.IsInsolvent)
                        _session.EnablePriorityCrew();
                },
                onHireCrew: () =>
                {
                    if (!_simulation.IsInsolvent)
                        _session.HireGroundCrew();
                },
                onReleaseCrew: () =>
                {
                    if (!_simulation.IsInsolvent)
                        _session.ReleaseGroundCrew();
                },
                onBuildStand: () =>
                {
                    if (!_simulation.IsInsolvent)
                        _session.BuildThirdStand();
                },
                onStartResearch: () =>
                {
                    if (_simulation.IsInsolvent)
                        return;
                    var research = _simulation.Research;
                    if (research.CanStartOperationsEfficiency)
                        _session.StartOperationsResearch();
                    else if (research.CanStartPassengerServices)
                        _session.StartPassengerServicesResearch();
                },
                onBeginOperations: () => DismissOpeningBriefing(),
                onResetAirport: () => ResetToNewAirport(),
                onContinueAway: () => { _showAwaySummary = false; });
            if (_toolkitHud != null)
            {
                _toolkitHud.BindActions(
                    onAccept: onAccept,
                    onDecline: onDecline,
                    onPriorityCrew: () =>
                    {
                        if (!_simulation.IsInsolvent)
                            _session.EnablePriorityCrew();
                    },
                    onHireCrew: () =>
                    {
                        if (!_simulation.IsInsolvent)
                            _session.HireGroundCrew();
                    },
                    onReleaseCrew: () =>
                    {
                        if (!_simulation.IsInsolvent)
                            _session.ReleaseGroundCrew();
                    },
                    onBuildStand: () =>
                    {
                        if (!_simulation.IsInsolvent)
                            _session.BuildThirdStand();
                    },
                    onStartResearch: () =>
                    {
                        if (_simulation.IsInsolvent)
                            return;
                        var research = _simulation.Research;
                        if (research.CanStartOperationsEfficiency)
                            _session.StartOperationsResearch();
                        else if (research.CanStartPassengerServices)
                            _session.StartPassengerServicesResearch();
                    },
                    onBeginOperations: () => DismissOpeningBriefing(),
                    onResetAirport: () => ResetToNewAirport(),
                    onContinueAway: () => { _showAwaySummary = false; },
                    onTogglePause: () => { _paused = !_paused; },
                    onSpeed1: () => { _speed = 1; },
                    onSpeed4: () => { _speed = 4; },
                    onFollow: () => _cameraController?.CycleOrStartFollow(),
                    onOverview: () => _cameraController?.ReturnToOverview(),
                    onToggleMute: () => { _audioMuted = !_audioMuted; });
            }
            _canvasHudActive = _canvasHud.IsActive;
        }

        private void Update()
        {
            ReadSimulationControls();
            if (!_paused)
                _preciseTime += Time.unscaledDeltaTime * _speed;

            var wholeSeconds = (long)Math.Floor(_preciseTime);
            if (wholeSeconds > _clock.Now.ElapsedSeconds)
            {
                _clock.Set(new SimulationTime(wholeSeconds));
                _simulation.Update();
                MaybeShowResearchToast();
                MaybeShowOpsToast();
                MaybeShowFirstSessionDecisionToasts();
            }

            if (_clock.Now.ElapsedSeconds >= _nextAutosaveSecond)
            {
                SaveSession();
                _nextAutosaveSecond = _clock.Now.ElapsedSeconds + 15;
            }

            ApplyDayCycle();
            UpdateAircraftVisual();
            UpdateGroundTrafficVisual();
            UpdateServiceVehicles();
            UpdateStandEquipment();
            UpdateWindsock();
            UpdateTerminalFlag();
            EnsureStandThreeVisual();
            UpdateEngineAudio();
            UpdateAmbientAudio();
            UpdateWeatherPresentation();
            UpdateTouchdownSmoke();
            UpdateTrafficWaitPresentation();
            UpdateCloudDrift();
            UpdateBirdFlock();
            UpdateHangarDoor();
            UpdateCoastalMotion();
            UpdateOpsAntenna();
            UpdateStarField();
            UpdateApronLife();
            SyncCanvasHud();
        }

        private void SyncCanvasHud()
        {
            if (!_canvasHudActive || _canvasHud == null)
                return;

            var toolkitActive = _toolkitHud != null && _toolkitHud.IsActive;
            // Toolkit owns product chrome — keep Canvas as hotkey/fallback only (0025 item 6).
            _canvasHud.SetVisible(!toolkitActive);

            string awayBody = null;
            if (_showAwaySummary)
            {
                var summary = _session.LastAwaySummary;
                var note = summary.RecoveredPreviousSave
                    ? "Recovered the previous safe copy."
                    : summary.ClockMovedBackwards
                        ? "Device clock moved backwards; no time was added."
                        : $"{_simulation.Location.Name} · {_simulation.Location.Region}";
                awayBody =
                    $"Airport operated for {FormatDuration(summary.AwaySeconds)}\n" +
                    $"Flights completed: {summary.FlightsCompleted}\n" +
                    $"Cash change: {summary.CashChange:+$#,0;-$#,0;$0}\n" +
                    $"Route income: ${summary.RouteIncome:N0}\n" +
                    $"Delay + running costs: ${summary.DelayCost + summary.OperatingCost:N0}\n" +
                    $"Reputation: {summary.ReputationChange:+0;-0;0}  (now {_simulation.Reputation.Score})\n\n" +
                    note;
            }

            string insolvencyBody = null;
            if (_simulation.IsInsolvent)
            {
                insolvencyBody =
                    $"Cash stayed negative across {AirportEconomy.InsolvencyConsecutiveDays} consecutive day closes. Operations have stopped; commands are refused.\n\n" +
                    $"Final cash: ${_simulation.Economy.Cash:N0}  ·  Reputation {_simulation.Reputation.Score}\n" +
                    $"{_simulation.Location.Name} · {_simulation.Location.Region}";
            }

            var showBriefing = _showOpeningBriefing && !_showAwaySummary && !_simulation.IsInsolvent;
            var showPause = _paused && !_showOpeningBriefing && !_showAwaySummary && !_simulation.IsInsolvent;
            var showAway = _showAwaySummary && !_simulation.IsInsolvent;
            var showInsolvency = _simulation.IsInsolvent;

            if (toolkitActive)
            {
                _toolkitHud.SyncOverlays(
                    showBriefing: showBriefing,
                    showPause: showPause,
                    showAway: showAway,
                    showInsolvency: showInsolvency,
                    locationName: _simulation.Location.Name,
                    firstOfferAfterSeconds: (int)AirportRoutes.FirstOfferAfterSeconds,
                    awayBody: awayBody,
                    insolvencyBody: insolvencyBody);
                _canvasHud.SetOverlaysVisible(false);
                _canvasHud.SetLeftPanelVisible(false);
                _canvasHud.SetRightPanelsVisible(false);
            }
            else
            {
                _canvasHud.SyncOverlays(
                    showBriefing: showBriefing,
                    showPause: showPause,
                    showAway: showAway,
                    showInsolvency: showInsolvency,
                    locationName: _simulation.Location.Name,
                    firstOfferAfterSeconds: (int)AirportRoutes.FirstOfferAfterSeconds,
                    awayBody: awayBody,
                    insolvencyBody: insolvencyBody);
            }

            var overlayOwnsScreen = showBriefing || showAway || showInsolvency;
            if (overlayOwnsScreen)
                return;

            SyncCanvasLeftPanel();
            var earlySession = _simulation.Routes.Accepted.Count == 0;
            var proposal = _simulation.Routes.Pending;
            if (proposal == null)
            {
                if (toolkitActive)
                    _toolkitHud.SyncOffer(false, false, string.Empty, string.Empty, string.Empty, false, false, string.Empty);
                else
                    _canvasHud.SyncOffer(false, false, string.Empty, string.Empty, string.Empty, false, false, string.Empty);
            }
            else
            {
                var firstDecision = earlySession;
                var meetsReputation = _simulation.Reputation.Score >= proposal.ReputationRequired;
                var fitsCapacity = _simulation.Routes.FitsScheduleCapacity(_simulation.Capacity.StandCount);
                var blocked = !meetsReputation || !fitsCapacity;
                string status;
                if (!meetsReputation)
                    status = $"Needs reputation {proposal.ReputationRequired} (have {_simulation.Reputation.Score})";
                else if (!fitsCapacity)
                    status = $"Schedule full ({_simulation.Routes.ScheduledFlightsPerDay}/{_simulation.MaxScheduledFlightsPerDay} flights/day)";
                else
                    status = $"Expires in {proposal.SecondsRemaining(_clock.Now)}s";
                var payout = proposal.IncomePerFlight + _simulation.Reputation.IncomeBonus;
                var body = firstDecision
                    ? $"Accept to earn cash on every completed flight.\n{proposal.Airline}\n{proposal.FlightsPerDay}/day to {proposal.Destination}\n+${payout:N0} per completed flight"
                    : $"{proposal.Airline}\n{proposal.FlightsPerDay}/day to {proposal.Destination}\n+${payout:N0} per completed flight";
                var title = firstDecision ? "FIRST DECISION — route offer" : "ROUTE OFFER — decide now";
                var acceptLabel = firstDecision ? "Accept route  (Enter)" : "Accept route";
                var canAccept = !_simulation.IsInsolvent && meetsReputation && fitsCapacity;
                if (toolkitActive)
                {
                    _toolkitHud.SyncOffer(true, firstDecision, title, body, status, blocked, canAccept, acceptLabel);
                    _canvasHud.SyncOffer(false, false, string.Empty, string.Empty, string.Empty, false, false, string.Empty);
                }
                else
                {
                    _canvasHud.SyncOffer(true, firstDecision, title, body, status, blocked, canAccept, acceptLabel);
                }
            }

            var toastVisible = !string.IsNullOrEmpty(_opsToast) && Time.unscaledTime <= _opsToastUntil;
            var researchVisible = !string.IsNullOrEmpty(_researchToast) && Time.unscaledTime <= _researchToastUntil;
            var saveVisible = Time.unscaledTime <= _saveIndicatorUntil;

            // Decision 0025 item 6 — Toolkit owns toasts + right column when active.
            if (toolkitActive)
            {
                _toolkitHud.SyncToast(_opsToast, toastVisible);
                _toolkitHud.SyncResearchToast(_researchToast, researchVisible);
                _toolkitHud.SyncSaveIndicator(saveVisible);
                _canvasHud.SyncToast(string.Empty, false);
                _canvasHud.SyncResearchToast(string.Empty, false);
                _canvasHud.SyncSaveIndicator(false);
            }
            else
            {
                _canvasHud.SyncToast(_opsToast, toastVisible);
                _canvasHud.SyncResearchToast(_researchToast, researchVisible);
                _canvasHud.SyncSaveIndicator(saveVisible);
            }

            SyncCanvasOpsPanel(toolkitActive);
            if (toolkitActive)
            {
                _canvasHud.SetRightPanelsVisible(false);
                _canvasHud.SetLeftPanelVisible(false);
            }
        }

        private void SyncCanvasOpsPanel(bool toolkitOwnsOps = false)
        {
            var accepted = _simulation.Routes.Accepted;
            var pendingOffer = _simulation.Routes.Pending;
            var summary =
                $"Routes {accepted.Count}  ·  {_simulation.Routes.ScheduledFlightsPerDay}/{_simulation.MaxScheduledFlightsPerDay} scheduled flights/day  ·  ${_simulation.Routes.IncomePerFlight + _simulation.Research.RouteIncomeBonus:N0}/flight";

            var lines = new System.Collections.Generic.List<string>();
            if (accepted.Count == 0)
            {
                if (pendingOffer != null)
                    lines.Add("Offer waiting above — Accept to start income");
                else
                {
                    var secondsToOffer = Math.Max(0, AirportRoutes.FirstOfferAfterSeconds - _clock.Now.ElapsedSeconds);
                    lines.Add(secondsToOffer > 0
                        ? $"No routes yet — first offer in {secondsToOffer}s"
                        : "No routes yet — offer arriving…");
                }
            }
            else
            {
                var start = Math.Max(0, accepted.Count - 4);
                for (var i = start; i < accepted.Count; i++)
                {
                    var route = accepted[i];
                    lines.Add($"{route.Airline} · {route.FlightsPerDay}/d → {route.Destination} · ${route.IncomePerFlight:N0}");
                }

                if (start > 0)
                    lines.Add($"+{start} earlier route(s)");
            }

            foreach (var aircraft in _simulation.GroundTraffic)
                lines.Add($"{aircraft.Id.Value}: {GroundTrafficSummary(aircraft)}");

            foreach (var entry in _simulation.EventLog.Events.Reverse().Take(4))
                lines.Add($"T+{entry.OccurredAt.ElapsedSeconds}s  {entry.FlightId}  ·  {entry.Title}");

            string report = null;
            var latest = _simulation.DailyReports.Latest;
            if (latest != null)
            {
                var rep = latest.ReputationChange == 0 ? "reputation flat"
                    : latest.ReputationChange > 0 ? $"reputation +{latest.ReputationChange}"
                    : $"reputation {latest.ReputationChange}";
                report =
                    $"{latest.SummaryLine}\nIncome ${latest.FlightIncome:N0}  ·  delays -${latest.DelayCost:N0}  ·  running -${latest.OperatingCost:N0}\n{rep}  ·  {latest.GroundCrew} crew";
            }

            var body = string.Join("\n", lines);
            var metar = MetarLine(_simulation.CurrentWeather);
            if (toolkitOwnsOps && _toolkitHud != null)
            {
                _toolkitHud.SyncOps(summary, body, report, metar);
                _toolkitHud.SyncReputationBar(_simulation.Reputation.Score / 100f);
            }
            else
                _canvasHud.SyncOps(summary, body, report);
        }

        private static string MetarLine(WeatherKind weather)
        {
            // Presentation-only METAR-style readout for REF-004 ops chrome.
            return weather switch
            {
                WeatherKind.Clear => "METAR  ·  TEMP 18°  ·  WIND 240/08  ·  VIS 10km",
                WeatherKind.Cloudy => "METAR  ·  TEMP 17°  ·  WIND 230/10  ·  VIS 9km",
                WeatherKind.Overcast => "METAR  ·  TEMP 16°  ·  WIND 220/12  ·  VIS 8km",
                WeatherKind.Rain => "METAR  ·  TEMP 14°  ·  WIND 200/14  ·  VIS 4km  ·  RA",
                WeatherKind.Storm => "METAR  ·  TEMP 13°  ·  WIND 190/22G32  ·  VIS 2km  ·  TSRA",
                WeatherKind.Fog => "METAR  ·  TEMP 12°  ·  WIND 250/04  ·  VIS 800m  ·  FG",
                _ => "METAR  ·  TEMP 17°  ·  WIND 230/10  ·  VIS 9km"
            };
        }

        private void SyncCanvasLeftPanel()
        {
            var timeOfDay = _simulation.TimeOfDay;
            var earlySession = _simulation.Routes.Accepted.Count == 0;
            var atStand = _simulation.ActiveAircraft.Phase == AircraftPhase.AtStand && _simulation.ActiveTurnaround != null;

            var weatherLabel = Weather.Describe(_simulation.CurrentWeather);
            if (Weather.IsAdverse(_simulation.CurrentWeather))
                weatherLabel += " · wet apron";
            var clockLine =
                $"{(_paused ? "PAUSED" : $"{_speed}× time")}{(_audioMuted ? "  ·  MUTED" : string.Empty)}  ·  Day {timeOfDay.DaysElapsed + 1} {timeOfDay.Clock} {timeOfDay.Phase}  ·  {weatherLabel}";
            var clockColor = _paused || _speed > 1 ? AirsideTheme.SafetyYellow : AirsideTheme.Cloud;

            var cashColor = _simulation.Economy.Cash < 0 ? AirsideTheme.SignalRed : AirsideTheme.Cloud;
            var cashLine =
                $"${_simulation.Economy.Cash:N0}  ·  Rep {_simulation.Reputation.Score}";
            if (_simulation.Reputation.Band == "Trusted")
                cashColor = _simulation.Economy.Cash < 0 ? AirsideTheme.SignalRed : AirsideTheme.ClearGreen;
            else if (_simulation.Reputation.Band == "Provisional" || _simulation.Reputation.Band == "At Risk")
                cashColor = _simulation.Economy.Cash < 0 ? AirsideTheme.SignalRed : AirsideTheme.SafetyYellow;

            var finance = _simulation.DailyFinance;
            var financeColor = finance.ExpectedNet < 0 ? AirsideTheme.SignalRed
                : finance.CashRunwayDays is int runwayDays && runwayDays <= 3 ? AirsideTheme.SafetyYellow
                : AirsideTheme.ClearGreen;
            // Slim economy strip: short day-net only (full breakdown stays in ops).
            var financeLine = $"Day {finance.ExpectedNet:+$#,0;-$#,0;$0}";

            string warningLine = null;
            var warningColor = AirsideTheme.SafetyYellow;
            if (_simulation.Economy.ConsecutiveNegativeDays > 0)
            {
                var left = AirportEconomy.InsolvencyConsecutiveDays - _simulation.Economy.ConsecutiveNegativeDays;
                warningLine =
                    $"Cash warning: {_simulation.Economy.ConsecutiveNegativeDays} negative day close(s) · {left} more → insolvent";
            }
            else if (_simulation.TrafficWaits.HasWarning(_clock.Now))
            {
                warningLine = $"TRAFFIC: {_simulation.TrafficWaits.Describe(_clock.Now)}";
            }

            var turnaroundLines = string.Empty;
            var priorityVisible = false;
            var priorityInteractable = false;
            var priorityLabel = "Hire priority crew · $300";
            string scheduleLine;
            var scheduleColor = AirsideTheme.Cloud;
            if (atStand)
            {
                var parts = new System.Collections.Generic.List<string>();
                foreach (var task in _simulation.ActiveTurnaround.Tasks(_clock.Now))
                {
                    var mark = task.State == TurnaroundTaskState.Complete ? "✓"
                        : task.State == TurnaroundTaskState.Active ? "●" : "○";
                    var time = task.State == TurnaroundTaskState.Complete ? string.Empty : $"  {task.SecondsRemaining}s";
                    parts.Add($"{mark} {task.Name}{time}");
                }

                if (_simulation.CurrentDelaySeconds > 0)
                {
                    parts.Add($"DELAY +{_simulation.CurrentDelaySeconds}s · {_simulation.CurrentDelayCause}");
                    scheduleColor = AirsideTheme.SignalRed;
                }

                turnaroundLines = string.Join("\n", parts);
                var alreadyAssigned = _simulation.ActiveTurnaround.PriorityCrewEnabled;
                priorityVisible = true;
                priorityInteractable = !alreadyAssigned && _simulation.Economy.Cash >= AirportEconomy.PriorityCrewCost;
                priorityLabel = alreadyAssigned ? "Priority crew active" : "Hire priority crew · $300";
                scheduleLine = "Turnaround in progress";
            }
            else
            {
                var onSchedule = _simulation.LastDelaySeconds <= 0;
                scheduleLine = onSchedule
                    ? "Operations running to schedule"
                    : $"Last flight delay: {_simulation.LastDelaySeconds}s · {_simulation.LastDelayCause}";
                scheduleColor = onSchedule ? AirsideTheme.ClearGreen : AirsideTheme.SignalRed;
            }

            var staffing = _simulation.Staffing;
            var staffingLine =
                $"Crew {staffing.GroundCrew}  ·  ${staffing.DailyWage:N0}/day{(staffing.IsUnderstaffed ? "  ·  SHORT" : string.Empty)}";
            var staffingColor = staffing.IsUnderstaffed ? AirsideTheme.SafetyYellow : AirsideTheme.Cloud;

            var capacity = _simulation.Capacity;
            var research = _simulation.Research;
            var researchLine = string.Empty;
            var researchProgressVisible = false;
            var researchProgress01 = 0f;
            var researchButtonVisible = false;
            var researchButtonInteractable = false;
            var researchButtonLabel = "Start research";
            if (!earlySession)
            {
                if (research.IsResearching)
                {
                    researchProgress01 = (float)research.Progress01(_clock.Now);
                    var pct = (int)(researchProgress01 * 100);
                    researchLine =
                        $"Research: {research.ActiveProjectName} {pct}% · {research.SecondsRemaining(_clock.Now)}s left";
                    researchProgressVisible = true;
                }
                else if (research.CanStartOperationsEfficiency)
                {
                    researchLine =
                        $"Research: {AirportResearch.OperationsEfficiencyName} · -${AirportResearch.OperationsEfficiencyDailyDiscount}/day when done";
                    researchButtonVisible = true;
                    researchButtonInteractable = _simulation.Economy.Cash >= AirportResearch.OperationsEfficiencyCost;
                    researchButtonLabel = $"Start research · ${AirportResearch.OperationsEfficiencyCost:N0}";
                }
                else if (research.CanStartPassengerServices)
                {
                    researchLine =
                        $"Research: {AirportResearch.PassengerServicesName} · +${AirportResearch.PassengerServicesRouteBonus}/flight when done";
                    researchButtonVisible = true;
                    researchButtonInteractable = _simulation.Economy.Cash >= AirportResearch.PassengerServicesCost;
                    researchButtonLabel = $"Start research · ${AirportResearch.PassengerServicesCost:N0}";
                }
                else
                {
                    var ops = research.OperationsEfficiencyComplete
                        ? $"{AirportResearch.OperationsEfficiencyName} ✓"
                        : string.Empty;
                    var pax = research.PassengerServicesComplete
                        ? $"{AirportResearch.PassengerServicesName} ✓ (+${AirportResearch.PassengerServicesRouteBonus}/flt)"
                        : string.Empty;
                    researchLine =
                        $"Research: {ops}{(ops.Length > 0 && pax.Length > 0 ? " · " : string.Empty)}{pax}";
                }
            }

            var coachUrgent = _simulation.Routes.Pending != null && _simulation.Routes.Accepted.Count == 0;
            var showWaitMeter = earlySession && _simulation.Routes.Pending == null && !_showOpeningBriefing;
            var waitLabel = string.Empty;
            var waitProgress = 0f;
            if (showWaitMeter)
            {
                var secondsToOffer = Math.Max(0, AirportRoutes.FirstOfferAfterSeconds - _clock.Now.ElapsedSeconds);
                waitProgress = 1f - Mathf.Clamp01(secondsToOffer / (float)AirportRoutes.FirstOfferAfterSeconds);
                waitLabel = secondsToOffer > 0
                    ? $"Waiting for first airline offer… {secondsToOffer}s"
                    : "Airline offer arriving…";
            }

            var toolkitOwnsLeft = _toolkitHud != null && _toolkitHud.IsActive;
            void SyncLeft(
                string locationLine,
                string flightLine,
                string phaseLine,
                string clock,
                Color clockCol,
                string cash,
                Color cashCol,
                string finance,
                Color financeCol,
                string warning,
                Color warningCol,
                bool showTurnaround,
                string turnaround,
                bool priorityVis,
                bool priorityInt,
                string priorityLbl,
                string schedule,
                Color scheduleCol,
                string staffing,
                Color staffingCol,
                bool early,
                string earlyHint,
                bool hireInt,
                string hireLbl,
                bool releaseInt,
                bool buildVis,
                bool buildInt,
                string buildLbl,
                string stands,
                string research,
                bool researchProgressVis,
                float researchProgress,
                bool researchButtonVis,
                bool researchButtonInt,
                string researchButtonLbl,
                string coach,
                bool coachUrgentFlag,
                string controls,
                bool waitMeter,
                string waitLbl,
                float waitProgress)
            {
                if (toolkitOwnsLeft)
                {
                    _toolkitHud.SyncLeftPanel(
                        locationLine, flightLine, phaseLine, clock, clockCol, cash, cashCol,
                        finance, financeCol, warning, warningCol, showTurnaround, turnaround,
                        priorityVis, priorityInt, priorityLbl, schedule, scheduleCol, staffing, staffingCol,
                        early, earlyHint, hireInt, hireLbl, releaseInt, buildVis, buildInt, buildLbl,
                        stands, research, researchProgressVis, researchProgress, researchButtonVis,
                        researchButtonInt, researchButtonLbl, coach, coachUrgentFlag, controls,
                        waitMeter, waitLbl, waitProgress);
                }
                else
                {
                    _canvasHud.SyncLeftPanel(
                        locationLine, flightLine, phaseLine, clock, clockCol, cash, cashCol,
                        finance, financeCol, warning, warningCol, showTurnaround, turnaround,
                        priorityVis, priorityInt, priorityLbl, schedule, scheduleCol, staffing, staffingCol,
                        early, earlyHint, hireInt, hireLbl, releaseInt, buildVis, buildInt, buildLbl,
                        stands, research, researchProgressVis, researchProgress, researchButtonVis,
                        researchButtonInt, researchButtonLbl, coach, coachUrgentFlag, controls,
                        waitMeter, waitLbl, waitProgress);
                }
            }

            SyncLeft(
                locationLine: $"{_simulation.Location.Name}  ·  {_simulation.Location.Region}",
                flightLine: CommercialFlightHudLine(),
                phaseLine: CommercialPhaseHudLine(),
                clock: clockLine,
                clockCol: clockColor,
                cash: cashLine,
                cashCol: cashColor,
                finance: financeLine,
                financeCol: financeColor,
                warning: warningLine,
                warningCol: warningColor,
                showTurnaround: atStand,
                turnaround: turnaroundLines,
                priorityVis: priorityVisible,
                priorityInt: priorityInteractable,
                priorityLbl: priorityLabel,
                schedule: scheduleLine,
                scheduleCol: scheduleColor,
                staffing: staffingLine,
                staffingCol: staffingColor,
                early: earlySession,
                earlyHint: "Crew / stand / research unlock after you accept a route",
                hireInt: staffing.GroundCrew < AirportStaffing.MaximumGroundCrew
                         && _simulation.Economy.Cash >= AirportStaffing.HireCost,
                hireLbl: $"Hire crew · ${AirportStaffing.HireCost}",
                releaseInt: staffing.GroundCrew > AirportStaffing.MinimumGroundCrew,
                buildVis: true,
                buildInt: capacity.CanExpand && _simulation.Economy.Cash >= AirportCapacity.ThirdStandCost,
                buildLbl: capacity.HasThirdStand
                    ? "Stand 3 built"
                    : $"Build stand 3 · ${AirportCapacity.ThirdStandCost:N0}",
                stands: $"Stands: {capacity.StandCount} / {AirportCapacity.MaximumStands}",
                research: researchLine,
                researchProgressVis: researchProgressVisible,
                researchProgress: researchProgress01,
                researchButtonVis: researchButtonVisible,
                researchButtonInt: researchButtonInteractable,
                researchButtonLbl: researchButtonLabel,
                coach: FirstSessionCoachLine(),
                coachUrgentFlag: coachUrgent,
                controls: earlySession
                    ? "Space pause · Tab speed · Enter accept offer · F follow · O overview"
                    : "Space pause · Tab speed · P priority · M mute · F follow/cycle · O overview",
                waitMeter: showWaitMeter,
                waitLbl: waitLabel,
                waitProgress: waitProgress);

            if (toolkitOwnsLeft)
            {
                AircraftPhase? phase = _simulation.Flights.Count > 0
                    ? _simulation.Flights[0].Operation.Phase
                    : null;
                _toolkitHud.SyncChromeIcons(
                    phase,
                    _simulation.CurrentWeather,
                    _speed,
                    _paused,
                    _audioMuted,
                    _cameraController != null && _cameraController.IsFollowing);
                if (atStand && _simulation.ActiveTurnaround != null)
                {
                    var tasks = _simulation.ActiveTurnaround.Tasks(_clock.Now);
                    var names = new string[tasks.Count];
                    var progress = new float[tasks.Count];
                    var icons = new Texture2D[tasks.Count];
                    for (var i = 0; i < tasks.Count; i++)
                    {
                        names[i] = tasks[i].State == TurnaroundTaskState.Complete
                            ? $"✓ {tasks[i].Name}"
                            : tasks[i].State == TurnaroundTaskState.Active
                                ? $"● {tasks[i].Name}  {tasks[i].SecondsRemaining}s"
                                : $"○ {tasks[i].Name}";
                        progress[i] = tasks[i].Progress01;
                        icons[i] = AirsideTheme.ServiceIconForTask(tasks[i].Name);
                    }

                    _toolkitHud.SyncTurnaroundBars(true, names, progress, icons);
                }
                else
                {
                    _toolkitHud.SyncTurnaroundBars(false, null, null, null);
                }
            }
        }

        private void ReadSimulationControls()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (_showAwaySummary)
            {
                if (keyboard.enterKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)
                    _showAwaySummary = false;
                return;
            }

            if (_showOpeningBriefing)
            {
                if (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame
                    || keyboard.escapeKey.wasPressedThisFrame)
                    DismissOpeningBriefing();
                return;
            }

            if (_simulation.IsInsolvent)
            {
                // Simulation is frozen; keep presentation paused and ignore ops hotkeys.
                _paused = true;
                return;
            }

            if (keyboard.spaceKey.wasPressedThisFrame)
                _paused = !_paused;
            if (keyboard.tabKey.wasPressedThisFrame)
                _speed = _speed == 1 ? 4 : 1;
            if (keyboard.pKey.wasPressedThisFrame)
                _session.EnablePriorityCrew();
            if (keyboard.mKey.wasPressedThisFrame)
                _audioMuted = !_audioMuted;

            // First-session: Enter accepts a ready route offer without hunting the mouse.
            if (keyboard.enterKey.wasPressedThisFrame)
                TryAcceptPendingRouteFromHotkey();
        }

        private void TryAcceptPendingRouteFromHotkey()
        {
            var proposal = _simulation.Routes.Pending;
            if (proposal == null || _simulation.IsInsolvent)
                return;
            if (_simulation.Reputation.Score < proposal.ReputationRequired)
                return;
            if (!_simulation.Routes.FitsScheduleCapacity(_simulation.Capacity.StandCount))
                return;
            _session.AcceptRoute();
        }

        private void UpdateAircraftVisual()
        {
            SyncCommercialAircraftViews();
            for (var index = 0; index < _simulation.Flights.Count; index++)
            {
                var flight = _simulation.Flights[index];
                var view = _commercialAircraft[index];
                var standZ = AirportTaxiNetwork.StandZ(flight.AssignedStand);
                var phase = flight.Operation.Phase;
                var progress = VisualPhaseProgress(flight, 0f);
                var position = PositionFor(phase, progress, standZ, flight.TaxiRoute);
                // Short look-ahead during takeoff lineup / early taxi so we do not skip the
                // whole curve and snap yaw onto the next straight.
                var lookAhead = phase == AircraftPhase.Takeoff && progress < 0.2f ? 0.04f
                    : phase is AircraftPhase.TaxiOut or AircraftPhase.TaxiIn or AircraftPhase.Pushback ? 0.08f
                    : 0.15f;
                var next = PositionFor(phase, VisualPhaseProgress(flight, lookAhead), standZ, flight.TaxiRoute);
                view.position = position;

                var direction = next - position;
                var targetRotation = direction.sqrMagnitude > 0.001f
                    ? Quaternion.LookRotation(direction.normalized)
                    : view.rotation;
                var pitch = PhasePitchDegrees(phase, progress);
                var bank = TurnBankDegrees(view, targetRotation, phase);
                targetRotation *= Quaternion.Euler(pitch, 0f, bank);
                var turnRate = phase == AircraftPhase.Takeoff && progress < 0.2f ? 8f : 5f;
                view.rotation = Quaternion.Slerp(view.rotation, targetRotation, Time.unscaledDeltaTime * turnRate);

                SpinPropellers(view, phase);
                RollLandingGearTires(view, phase);
                UpdateControlSurfaces(view, phase, progress, bank);
                UpdateGroundShadow(view);
                UpdateAircraftLightsAndGear(view, phase, (float)_simulation.TimeOfDay.Daylight);
                UpdateCabinDoor(view, phase);
                UpdateCabinWindowGlow(view, phase, (float)_simulation.TimeOfDay.Daylight);
                UpdateEngineHeat(view, phase);

                if (_cameraController != null
                    && _cameraController.IsFollowing
                    && _cameraController.FollowTarget == view)
                    _cameraController.SetFollowPhase(phase, progress);
            }
        }

        private static float PhasePitchDegrees(AircraftPhase phase, float progress)
        {
            // Presentation-only attitude: nose-up takeoff, shallow approach, landing flare.
            // Negative X euler = nose up with LookRotation-forward posing.
            var t = Mathf.Clamp01(progress);
            return phase switch
            {
                AircraftPhase.Takeoff => t < 0.48f
                    ? 0f
                    : Mathf.Lerp(0f, -10f, Mathf.SmoothStep(0f, 1f, (t - 0.48f) / 0.52f)),
                AircraftPhase.Approach => Mathf.Lerp(-2.5f, -3.5f, t),
                AircraftPhase.Landing => t < 0.28f
                    ? Mathf.Lerp(-3.5f, -5f, t / 0.28f)
                    : Mathf.Lerp(-5f, 0f, Mathf.SmoothStep(0f, 1f, (t - 0.28f) / 0.72f)),
                AircraftPhase.Departed => -8f,
                _ => 0f
            };
        }

        private static float TurnBankDegrees(Transform view, Quaternion targetRotation, AircraftPhase phase)
        {
            if (phase is AircraftPhase.AtStand or AircraftPhase.Departed)
                return 0f;

            var yawDelta = Mathf.DeltaAngle(view.eulerAngles.y, targetRotation.eulerAngles.y);
            var limit = phase is AircraftPhase.TaxiIn or AircraftPhase.TaxiOut or AircraftPhase.Pushback
                ? 8f
                : 16f;
            return Mathf.Clamp(-yawDelta * 2.2f, -limit, limit);
        }

        private static void UpdateControlSurfaces(Transform aircraft, AircraftPhase phase, float progress, float bankDegrees)
        {
            // Presentation-only: rudder/elevator deflect with attitude (Batch D life).
            var pitch = PhasePitchDegrees(phase, progress);
            var elevator = Mathf.Clamp(-pitch * 1.4f, -22f, 22f);
            var rudder = Mathf.Clamp(-bankDegrees * 0.9f, -18f, 18f);
            foreach (var child in aircraft.GetComponentsInChildren<Transform>(true))
            {
                if (child == aircraft)
                    continue;
                if (child.name.StartsWith("Rudder", StringComparison.Ordinal))
                {
                    var euler = child.localEulerAngles;
                    var current = euler.y > 180f ? euler.y - 360f : euler.y;
                    euler.y = Mathf.MoveTowards(current, rudder, Time.unscaledDeltaTime * 90f);
                    child.localEulerAngles = euler;
                }
                else if (child.name.IndexOf("elevator", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         child.name.StartsWith("Tailplane", StringComparison.Ordinal))
                {
                    // Soft elevator cue on the whole tailplane when no separate elevator mesh.
                    var euler = child.localEulerAngles;
                    var current = euler.x > 180f ? euler.x - 360f : euler.x;
                    var target = child.name.StartsWith("Tailplane", StringComparison.Ordinal) ? elevator * 0.35f : elevator;
                    euler.x = Mathf.MoveTowards(current, target, Time.unscaledDeltaTime * 80f);
                    child.localEulerAngles = euler;
                }
                else if (child.name.StartsWith("Aileron", StringComparison.Ordinal))
                {
                    var euler = child.localEulerAngles;
                    var current = euler.x > 180f ? euler.x - 360f : euler.x;
                    var side = child.name.IndexOf(" L", StringComparison.Ordinal) >= 0 ? 1f : -1f;
                    var target = Mathf.Clamp(bankDegrees * 0.8f * side, -18f, 18f);
                    euler.x = Mathf.MoveTowards(current, target, Time.unscaledDeltaTime * 90f);
                    child.localEulerAngles = euler;
                }
                else if (child.name is "Flap L" or "Flap R")
                {
                    var deploy = phase is AircraftPhase.Approach or AircraftPhase.Landing or AircraftPhase.Takeoff
                        ? Mathf.Lerp(0f, 22f, Mathf.Clamp01(progress + 0.25f))
                        : 0f;
                    var euler = child.localEulerAngles;
                    var current = euler.x > 180f ? euler.x - 360f : euler.x;
                    euler.x = Mathf.MoveTowards(current, deploy, Time.unscaledDeltaTime * 40f);
                    child.localEulerAngles = euler;
                }
                else if (child.name.StartsWith("Spoiler", StringComparison.Ordinal))
                {
                    var raise = phase is AircraftPhase.Landing
                        ? Mathf.Lerp(0f, 35f, Mathf.Clamp01(progress))
                        : 0f;
                    var euler = child.localEulerAngles;
                    var current = euler.x > 180f ? euler.x - 360f : euler.x;
                    euler.x = Mathf.MoveTowards(current, -raise, Time.unscaledDeltaTime * 55f);
                    child.localEulerAngles = euler;
                }
            }
        }

        private void UpdateEngineAudio()
        {
            for (var index = 0; index < _simulation.Flights.Count && index < _commercialAircraft.Length; index++)
            {
                var phase = _simulation.Flights[index].Operation.Phase;
                var enginesOn = phase != AircraftPhase.AtStand && phase != AircraftPhase.Departed;
                ApplyEngineAudio(_commercialAircraft[index], enginesOn);
            }

            for (var index = 0; index < _groundTraffic.Length; index++)
            {
                var traffic = _simulation.GroundTraffic[index];
                // Ground traffic keeps props turning while on the field; quieter when holding.
                ApplyEngineAudio(_groundTraffic[index], enginesOn: !traffic.IsHolding);
            }
        }

        private void ApplyEngineAudio(Transform aircraft, bool enginesOn)
        {
            if (aircraft == null)
                return;

            var source = aircraft.GetComponent<AudioSource>();
            if (source == null)
                return;

            if (_audioMuted)
            {
                source.volume = 0f;
                return;
            }

            var target = enginesOn ? EngineVolumeRunning : EngineVolumeIdle;
            if (_paused)
                target *= EngineVolumePausedScale;
            source.volume = Mathf.MoveTowards(source.volume, target, Time.unscaledDeltaTime * 0.4f);
        }

        private void UpdateAmbientAudio()
        {
            if (_ambientWindAudio == null || _ambientRainAudio == null)
                return;

            var weather = _simulation.CurrentWeather;
            var raining = weather == WeatherKind.Rain || weather == WeatherKind.Storm;
            var storm = weather == WeatherKind.Storm;
            var windTarget = _audioMuted ? 0f : AmbientWindVolume;
            var rainTarget = _audioMuted || !raining ? 0f : (storm ? AmbientStormVolume : AmbientRainVolume);
            var coastTarget = _audioMuted ? 0f : AmbientCoastVolume * (storm ? 1.45f : raining ? 1.2f : 1f);
            if (_paused)
            {
                windTarget *= EngineVolumePausedScale;
                rainTarget *= EngineVolumePausedScale;
                coastTarget *= EngineVolumePausedScale;
            }

            // Slight day/night wind variation (presentation only).
            if (!_audioMuted)
                windTarget *= Mathf.Lerp(0.75f, 1.1f, 1f - (float)_simulation.TimeOfDay.Daylight);

            _ambientWindAudio.volume = Mathf.MoveTowards(_ambientWindAudio.volume, windTarget, Time.unscaledDeltaTime * 0.2f);
            _ambientRainAudio.volume = Mathf.MoveTowards(_ambientRainAudio.volume, rainTarget, Time.unscaledDeltaTime * 0.25f);
            _ambientRainAudio.pitch = storm ? 1.08f : 1f;
            if (_ambientCoastAudio != null)
            {
                _ambientCoastAudio.volume = Mathf.MoveTowards(
                    _ambientCoastAudio.volume, coastTarget, Time.unscaledDeltaTime * 0.15f);
                _ambientCoastAudio.pitch = 0.92f + 0.08f * Mathf.PerlinNoise(Time.unscaledTime * 0.05f, 1.7f);
            }
        }

        private static void UpdateAircraftLightsAndGear(Transform aircraft, AircraftPhase phase, float daylight)
        {
            var airborne = phase is AircraftPhase.Approach or AircraftPhase.Takeoff or AircraftPhase.Departed;
            var enginesOn = phase != AircraftPhase.AtStand && phase != AircraftPhase.Departed;
            var night = daylight < 0.35f;
            var landingLights = phase is AircraftPhase.Approach or AircraftPhase.Landing or AircraftPhase.Takeoff;
            var taxiLights = !airborne && (night || phase is AircraftPhase.TaxiIn or AircraftPhase.TaxiOut or AircraftPhase.Pushback);

            foreach (var child in aircraft.GetComponentsInChildren<Transform>(true))
            {
                if (child == aircraft)
                    continue;
                if (child.name.StartsWith("Gear door", StringComparison.Ordinal))
                {
                    // Doors open when gear is down; close when retracted (presentation only).
                    child.gameObject.SetActive(true);
                    var euler = child.localEulerAngles;
                    var current = euler.x > 180f ? euler.x - 360f : euler.x;
                    var target = airborne ? 0f : 78f;
                    euler.x = Mathf.MoveTowards(current, target, Time.unscaledDeltaTime * 160f);
                    child.localEulerAngles = euler;
                }
                else if (child.name is "Gear nose" or "Gear L" or "Gear R")
                {
                    // Soft retract/deploy instead of a hard pop (Batch D ANM-AIR-002 language).
                    // Exact strut names only — densified "Gear scissors *" must not pitch with legs.
                    child.gameObject.SetActive(true);
                    var euler = child.localEulerAngles;
                    var current = euler.x > 180f ? euler.x - 360f : euler.x;
                    var gearBias = AirsideReusableMotion.GearBias(phase);
                    var target = Mathf.Lerp(0f, -80f, 1f - gearBias);
                    euler.x = Mathf.MoveTowards(current, target, Time.unscaledDeltaTime * 140f);
                    child.localEulerAngles = euler;
                }
                else if (child.name.StartsWith("NavLight", StringComparison.Ordinal))
                {
                    var navOn = enginesOn || night;
                    child.gameObject.SetActive(navOn);
                    EnsureNavPointLight(child, navOn, child.name.EndsWith("R", StringComparison.Ordinal)
                        || child.name.IndexOf(" R", StringComparison.Ordinal) >= 0
                        || child.name.IndexOf("right", StringComparison.OrdinalIgnoreCase) >= 0);
                }
                else if (child.name.StartsWith("Beacon", StringComparison.Ordinal))
                {
                    // ANM-AIR-004 — pulse rate from AirsideReusableMotion (not a hard-coded 2 Hz).
                    var beaconOn = enginesOn &&
                        (Mathf.FloorToInt(Time.unscaledTime * AirsideReusableMotion.BeaconHz * 2f) % 2 == 0);
                    child.gameObject.SetActive(beaconOn);
                    EnsureBeaconPointLight(child, beaconOn);
                }
                else if (child.name.StartsWith("LandingLight", StringComparison.Ordinal))
                {
                    child.gameObject.SetActive(landingLights);
                    EnsureLandingSpotLight(child, landingLights, night);
                }
                else if (child.name.StartsWith("TaxiLight", StringComparison.Ordinal))
                {
                    child.gameObject.SetActive(taxiLights);
                    EnsureTaxiSpotLight(child, taxiLights);
                }
            }
        }

        /// <summary>
        /// Decision 0025 items 5+7 — wingtip nav lights cast real coloured PointLights.
        /// </summary>
        private static void EnsureNavPointLight(Transform lamp, bool on, bool isRight)
        {
            var light = lamp.GetComponent<Light>();
            if (light == null)
            {
                light = lamp.gameObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = isRight
                    ? new Color(0.95f, 0.15f, 0.12f)
                    : new Color(0.12f, 0.95f, 0.28f);
                light.range = 8f;
                light.shadows = LightShadows.None;
            }

            light.enabled = on;
            if (on)
                light.intensity = 1.8f * AirsideReusableMotion.NavSteady;
        }

        private static void EnsureBeaconPointLight(Transform lamp, bool on)
        {
            var light = lamp.GetComponent<Light>();
            if (light == null)
            {
                light = lamp.gameObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.25f, 0.12f);
                light.range = 10f;
                light.shadows = LightShadows.None;
            }

            light.enabled = on;
            if (on)
                light.intensity = 2.6f;
        }

        /// <summary>
        /// Decision 0025 items 5+7 — real SpotLights on landing / taxi lamp meshes so
        /// approach and night taxi cast light on the runway and apron.
        /// </summary>
        private static void EnsureLandingSpotLight(Transform lamp, bool on, bool night)
        {
            var light = lamp.GetComponent<Light>();
            if (light == null)
            {
                light = lamp.gameObject.AddComponent<Light>();
                light.type = LightType.Spot;
                light.color = new Color(1f, 0.97f, 0.88f);
                light.range = 42f;
                light.spotAngle = 48f;
                light.innerSpotAngle = 22f;
                light.shadows = LightShadows.Soft;
            }

            light.enabled = on;
            if (!on)
                return;
            light.intensity = night ? 6.5f : 3.2f;
            // Lamp mesh faces +Z (aircraft forward); SpotLights aim along local +Z.
            light.transform.localRotation = Quaternion.identity;
        }

        private static void EnsureTaxiSpotLight(Transform lamp, bool on)
        {
            var light = lamp.GetComponent<Light>();
            if (light == null)
            {
                light = lamp.gameObject.AddComponent<Light>();
                light.type = LightType.Spot;
                light.color = new Color(1f, 0.94f, 0.78f);
                light.range = 18f;
                light.spotAngle = 55f;
                light.innerSpotAngle = 28f;
                light.shadows = LightShadows.None;
                light.intensity = 2.4f;
            }

            light.enabled = on;
        }

        private static void UpdateCabinDoor(Transform aircraft, AircraftPhase phase)
        {
            // Presentation-only: cabin + cargo doors swing open at stand, close before pushback.
            // ANM-AIR-003 — open bias from AirsideReusableMotion.
            var doorBias = AirsideReusableMotion.CabinDoorBias(phase);
            var cabinTargetY = Mathf.Lerp(0f, -85f, doorBias);
            var cargoTargetY = Mathf.Lerp(0f, 70f, doorBias);
            foreach (var child in aircraft.GetComponentsInChildren<Transform>(true))
            {
                if (child == aircraft)
                    continue;
                if (child.name.StartsWith("CabinDoor", StringComparison.Ordinal))
                {
                    var euler = child.localEulerAngles;
                    var current = euler.y > 180f ? euler.y - 360f : euler.y;
                    euler.y = Mathf.MoveTowards(current, cabinTargetY, Time.unscaledDeltaTime * 120f);
                    child.localEulerAngles = euler;
                }
                else if (child.name.StartsWith("Cargo door", StringComparison.OrdinalIgnoreCase)
                         || child.name.Equals("CargoDoor", StringComparison.OrdinalIgnoreCase))
                {
                    var euler = child.localEulerAngles;
                    var current = euler.y > 180f ? euler.y - 360f : euler.y;
                    euler.y = Mathf.MoveTowards(current, cargoTargetY, Time.unscaledDeltaTime * 100f);
                    child.localEulerAngles = euler;
                }
            }
        }

        /// <summary>
        /// Decision 0025 items 5+7 — cabin / cockpit glass picks up warm emissive glow
        /// at night and a softer stand dwell glow so the airframe reads alive.
        /// </summary>
        private static void UpdateCabinWindowGlow(Transform aircraft, AircraftPhase phase, float daylight)
        {
            var night = daylight < 0.4f;
            var atStand = phase == AircraftPhase.AtStand;
            var enginesOn = phase != AircraftPhase.AtStand && phase != AircraftPhase.Departed;
            var intensity = 0f;
            if (night)
                intensity = atStand ? 1.35f : enginesOn ? 1.05f : 0.55f;
            else if (atStand)
                intensity = 0.22f;

            var glow = new Color(1f, 0.82f, 0.55f) * intensity;
            foreach (var child in aircraft.GetComponentsInChildren<Transform>(true))
            {
                if (child == aircraft)
                    continue;
                var n = child.name;
                // Exact glass only — densified Cockpit frame / pillars / cabin window
                // frames must not emit (InferFromMeshName treats those as Metal).
                if (!(n == "Cockpit"
                      || n == "Cockpit glare"
                      || ((n.StartsWith("Cabin window", StringComparison.OrdinalIgnoreCase)
                           || n.StartsWith("Cabin windows", StringComparison.OrdinalIgnoreCase)
                           || n.IndexOf("cabin_window", StringComparison.OrdinalIgnoreCase) >= 0)
                          && n.IndexOf("frame", StringComparison.OrdinalIgnoreCase) < 0)))
                    continue;

                var renderer = child.GetComponent<Renderer>();
                if (renderer == null || renderer.material == null)
                    continue;
                var mat = renderer.material;
                if (intensity > 0.01f)
                {
                    mat.EnableKeyword("_EMISSION");
                    if (mat.HasProperty("_EmissionColor"))
                        mat.SetColor("_EmissionColor", glow);
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                else if (mat.HasProperty("_EmissionColor"))
                {
                    mat.SetColor("_EmissionColor", Color.black);
                    mat.DisableKeyword("_EMISSION");
                }
            }
        }

        private static void UpdateEngineHeat(Transform aircraft, AircraftPhase phase)
        {
            // Presentation-only: subtle heat shimmer behind running engines.
            var enginesOn = phase != AircraftPhase.AtStand && phase != AircraftPhase.Departed;
            var intensity = phase is AircraftPhase.Takeoff or AircraftPhase.Approach ? 1.25f : 1f;
            foreach (var child in aircraft.GetComponentsInChildren<Transform>(true))
            {
                if (child == aircraft || !child.name.StartsWith("EngineHeat", StringComparison.Ordinal))
                    continue;

                child.gameObject.SetActive(enginesOn);
                if (!enginesOn)
                    continue;

                var pulse = 0.85f + 0.15f * Mathf.Sin(
                    Time.unscaledTime * AirsideReusableMotion.HeatPulseHz * Mathf.PI * 2f
                    + child.GetInstanceID() * 0.01f);
                child.localScale = new Vector3(0.35f * pulse * intensity, 0.35f * pulse * intensity, 0.7f);
                var renderer = child.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var color = renderer.material.color;
                    color.a = (0.12f + 0.1f * pulse) * intensity;
                    SetRendererColor(renderer, color);
                }
            }
        }

        private static void SpinPropellers(Transform aircraft, AircraftPhase phase)
        {
            // Presentation-only: RPM follows phase (Batch F4 ANM-AIR-001 via AirsideReusableMotion).
            if (!AirsideReusableMotion.PropellersSpinning(phase))
            {
                ApplyPropBlur(aircraft, highRpm: false);
                return;
            }

            var rpm = AirsideReusableMotion.PropRpmForPhase(phase);
            // Constants are true RPM — convert to degrees/sec (×6) so blades read as spinning.
            var degrees = Time.unscaledDeltaTime * rpm * 6f;
            var highRpm = rpm >= AirsideReusableMotion.PropHighRpmThreshold;
            foreach (var child in aircraft.GetComponentsInChildren<Transform>(true))
            {
                if (child == aircraft)
                    continue;
                if (!child.name.StartsWith("Propeller", StringComparison.Ordinal))
                    continue;
                child.Rotate(Vector3.forward, degrees, Space.Self);
                ApplyPropBlurToHub(child, highRpm);
            }
        }

        private static void SpinGroundTrafficPropellers(Transform aircraft, bool enginesOn)
        {
            if (!enginesOn)
            {
                ApplyPropBlur(aircraft, highRpm: false);
                return;
            }

            // Match ANM-AIR taxi RPM so ground traffic props read with the fleet.
            var rpm = AirsideReusableMotion.PropRpmTaxi;
            var degrees = Time.unscaledDeltaTime * rpm * 6f;
            var highRpm = rpm >= AirsideReusableMotion.PropHighRpmThreshold;
            foreach (var child in aircraft.GetComponentsInChildren<Transform>(true))
            {
                if (child == aircraft)
                    continue;
                if (!child.name.StartsWith("Propeller", StringComparison.Ordinal))
                    continue;
                child.Rotate(Vector3.forward, degrees, Space.Self);
                ApplyPropBlurToHub(child, highRpm);
            }
        }

        /// <summary>
        /// At high RPM hide individual blades and show a translucent disc (Batch D life).
        /// </summary>
        private static void ApplyPropBlur(Transform aircraft, bool highRpm)
        {
            foreach (var child in aircraft.GetComponentsInChildren<Transform>(true))
            {
                if (child == aircraft || !child.name.StartsWith("Propeller", StringComparison.Ordinal))
                    continue;
                ApplyPropBlurToHub(child, highRpm);
            }
        }

        private static void ApplyPropBlurToHub(Transform propeller, bool highRpm)
        {
            var selfRenderer = propeller.GetComponent<Renderer>();
            if (selfRenderer != null)
                selfRenderer.enabled = !highRpm;

            for (var i = 0; i < propeller.childCount; i++)
            {
                var child = propeller.GetChild(i);
                if (child.name == "PropDisc")
                {
                    child.gameObject.SetActive(highRpm);
                    continue;
                }

                var renderer = child.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.enabled = !highRpm;
            }
        }

        private static void RollLandingGearTires(Transform aircraft, AircraftPhase phase)
        {
            // Presentation-only: tires roll on the ground (Batch D motion life).
            var rolling = phase is AircraftPhase.TaxiIn or AircraftPhase.TaxiOut
                or AircraftPhase.Pushback or AircraftPhase.Landing or AircraftPhase.Takeoff;
            if (!rolling)
                return;

            var speed = phase switch
            {
                AircraftPhase.Takeoff => 2.4f,
                AircraftPhase.Landing => 1.9f,
                AircraftPhase.Pushback => 0.55f,
                _ => 1f
            };
            var degrees = Time.unscaledDeltaTime * AirsideReusableMotion.AircraftTireRpmTaxi * speed;
            foreach (var child in aircraft.GetComponentsInChildren<Transform>(true))
            {
                if (child == aircraft)
                    continue;
                if (child.name.StartsWith("Tire", StringComparison.Ordinal) ||
                    (child.name.IndexOf("wheel", StringComparison.OrdinalIgnoreCase) >= 0
                     && child.name.IndexOf("arch", StringComparison.OrdinalIgnoreCase) < 0
                     && child.name.IndexOf("hub", StringComparison.OrdinalIgnoreCase) < 0))
                    child.Rotate(Vector3.right, degrees, Space.Self);
            }
        }

        private void SyncCommercialAircraftViews()
        {
            var flights = _simulation.Flights;
            var needed = flights.Count;

            // Keep each visual glued to its AircraftId across respawn reordering.
            // Count-only rebuild left transforms at stale list indices after Sort.
            var byId = new Dictionary<string, Transform>(needed);
            if (_commercialAircraft != null)
            {
                foreach (var existing in _commercialAircraft)
                {
                    if (existing == null)
                        continue;
                    const string prefix = "Commercial ";
                    if (existing.name.StartsWith(prefix, StringComparison.Ordinal))
                        byId[existing.name.Substring(prefix.Length)] = existing;
                }
            }

            var next = new Transform[needed];
            var kept = new HashSet<Transform>();
            for (var index = 0; index < needed; index++)
            {
                var flight = flights[index];
                if (byId.TryGetValue(flight.AircraftId, out var existing))
                {
                    next[index] = existing;
                    kept.Add(existing);
                    continue;
                }

                var color = index == 0
                    ? new Color(0.12f, 0.43f, 0.76f)
                    : new Color(0.18f, 0.55f, 0.48f);
                var livery = index == 0
                    ? "Textures/Decals/dc_livery_coastline_regional_v01.png"
                    : "Textures/Decals/dc_livery_emu_air_v01.png";
                next[index] = BuildAircraft($"Commercial {flight.AircraftId}", color, livery);
            }

            foreach (var pair in byId)
            {
                if (!kept.Contains(pair.Value) && pair.Value != null)
                    Destroy(pair.Value.gameObject);
            }

            var changed = _commercialAircraft == null || _commercialAircraft.Length != next.Length;
            if (!changed)
            {
                for (var i = 0; i < next.Length; i++)
                {
                    if (_commercialAircraft[i] != next[i])
                    {
                        changed = true;
                        break;
                    }
                }
            }

            _commercialAircraft = next;
            if (changed && needed > 0 && _cameraController != null)
                _cameraController.SetFollowTargets(_commercialAircraft);
        }

        private void UpdateGroundTrafficVisual()
        {
            for (var index = 0; index < _groundTraffic.Length; index++)
            {
                var view = _groundTraffic[index];
                var traffic = _simulation.GroundTraffic[index];
                var point = traffic.Position;
                var target = new Vector3(point.X, 0.7f, point.Z);
                var previous = view.position;

                // A yield can snap the sim point back; do not lerp through released space.
                view.position = TaxiVisualPath.MoveGroundTraffic(
                    previous,
                    target,
                    traffic.IsHolding,
                    Time.unscaledDeltaTime * 10f);

                var direction = target - previous;
                var targetRotation = direction.sqrMagnitude > 0.0004f
                    ? Quaternion.LookRotation(direction.normalized)
                    : view.rotation;
                if (!traffic.IsHolding)
                    targetRotation *= Quaternion.Euler(0f, 0f, TurnBankDegrees(view, targetRotation, AircraftPhase.TaxiIn));
                view.rotation = Quaternion.Slerp(view.rotation, targetRotation, Time.unscaledDeltaTime * 4f);

                SpinGroundTrafficPropellers(view, enginesOn: !traffic.IsHolding);
                RollLandingGearTires(
                    view,
                    traffic.IsHolding ? AircraftPhase.AtStand : AircraftPhase.TaxiIn);
                UpdateGroundShadow(view);
                UpdateAircraftLightsAndGear(
                    view,
                    traffic.IsHolding ? AircraftPhase.AtStand : AircraftPhase.TaxiIn,
                    (float)_simulation.TimeOfDay.Daylight);
                UpdateCabinWindowGlow(
                    view,
                    traffic.IsHolding ? AircraftPhase.AtStand : AircraftPhase.TaxiIn,
                    (float)_simulation.TimeOfDay.Daylight);
                UpdateEngineHeat(view, traffic.IsHolding ? AircraftPhase.AtStand : AircraftPhase.TaxiIn);
            }
        }

        private float VisualPhaseProgress(CommercialFlight flight, float lookAheadSeconds)
        {
            if (flight.Operation.IsComplete)
                return 1f;

            var elapsed = _preciseTime + lookAheadSeconds - flight.Operation.PhaseStartedAt.ElapsedSeconds;
            return Mathf.Clamp01((float)(elapsed / flight.Operation.PhaseDurationSeconds));
        }

        private void UpdateServiceVehicles()
        {
            // Prefer any commercial currently in turnaround (supports dual flights).
            CommercialFlight servicing = null;
            foreach (var flight in _simulation.Flights)
            {
                if (flight.Operation.Phase == AircraftPhase.AtStand && flight.Turnaround != null)
                {
                    servicing = flight;
                    break;
                }
            }

            // Parked GSE stays visible on the apron edge so the field feels staffed.
            var fuelPark = new Vector3(-4.5f, 0.55f, 12.5f);
            var bagPark = new Vector3(-1.5f, 0.42f, 11.8f);
            var busPark = new Vector3(2f, 0.68f, 11.2f);

            if (servicing == null)
            {
                UpdateVehicle(_fuelTruck, false, fuelPark, fuelPark);
                UpdateVehicle(_baggageCart, false, bagPark, bagPark);
                UpdateVehicle(_passengerBus, false, busPark, busPark);
                SyncVehicleHeadlights(_fuelTruck, (float)_simulation.TimeOfDay.Daylight < 0.38f, (float)_simulation.TimeOfDay.Daylight);
                SyncVehicleHeadlights(_baggageCart, (float)_simulation.TimeOfDay.Daylight < 0.38f, (float)_simulation.TimeOfDay.Daylight);
                SyncVehicleHeadlights(_passengerBus, (float)_simulation.TimeOfDay.Daylight < 0.38f, (float)_simulation.TimeOfDay.Daylight);
                return;
            }

            var standZ = AirportTaxiNetwork.StandZ(servicing.AssignedStand);
            var fuelActive = TaskActive(servicing, "Refuel");
            var bagActive = TaskActive(servicing, "Unload bags") || TaskActive(servicing, "Load bags");
            var paxActive = TaskActive(servicing, "Passengers off") || TaskActive(servicing, "Board passengers");
            UpdateVehicle(_fuelTruck, fuelActive, new Vector3(13.3f, 0.55f, standZ + 1.8f), fuelPark);
            UpdateVehicle(_baggageCart, bagActive, new Vector3(20.2f, 0.42f, standZ - 1.8f), bagPark);
            UpdateVehicle(_passengerBus, paxActive, new Vector3(13f, 0.68f, standZ - 2.2f), busPark);
            var daylight = (float)_simulation.TimeOfDay.Daylight;
            SyncVehicleHeadlights(_fuelTruck, fuelActive || daylight < 0.38f, daylight);
            SyncVehicleHeadlights(_baggageCart, bagActive || daylight < 0.38f, daylight);
            SyncVehicleHeadlights(_passengerBus, paxActive || daylight < 0.38f, daylight);
            AnimateServiceLoops(_fuelTruck, fuelActive, "Hose");
            AnimateServiceLoops(_baggageCart, bagActive, "Cargo");
            AnimateServiceLoops(_passengerBus, paxActive, "Door");
            PulseServiceBeacon(_fuelTruck, fuelActive);
            PulseServiceBeacon(_baggageCart, bagActive);
            PulseServiceBeacon(_passengerBus, paxActive);
        }

        private void UpdateStandEquipment()
        {
            // Presentation-only stand props (Batch C PRP / Batch A REF scale language).
            CommercialFlight atStand = null;
            CommercialFlight pushing = null;
            foreach (var flight in _simulation.Flights)
            {
                if (flight.Operation.Phase == AircraftPhase.AtStand)
                    atStand ??= flight;
                if (flight.Operation.Phase == AircraftPhase.Pushback)
                    pushing ??= flight;
            }

            if (atStand != null)
            {
                var z = AirportTaxiNetwork.StandZ(atStand.AssignedStand);
                var progress = VisualPhaseProgress(atStand, 0f);
                // Stairs roll in from apron edge, then pitch up against the cabin.
                var arrive = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress * 5f));
                var stairsX = Mathf.Lerp(20.5f, 17.9f, arrive);
                var stairsPitch = Mathf.Lerp(-42f, -6f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress * 4f)));
                PlaceProp(_stairs, true, new Vector3(stairsX, 0.55f, z + 0.15f), Quaternion.Euler(stairsPitch, -8f, 0f));
                // Chocks drop and settle with a slight roll into the tire.
                var chockArrive = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress * 6f));
                var chockY = Mathf.Lerp(0.42f, 0.12f, chockArrive);
                var chockRoll = Mathf.Lerp(35f, 0f, chockArrive);
                PlaceProp(_chocks, true, new Vector3(17f, chockY, z + 1.55f), Quaternion.Euler(0f, 0f, chockRoll));
                PlaceProp(_gpuCart, true, new Vector3(15.2f, 0.35f, z + 2.4f), Quaternion.Euler(0f, 90f, 0f));
                PulseGpuCart(_gpuCart, true);
            }
            else
            {
                PlaceProp(_stairs, false, Vector3.zero, Quaternion.identity);
                PlaceProp(_chocks, false, Vector3.zero, Quaternion.identity);
                PlaceProp(_gpuCart, false, Vector3.zero, Quaternion.identity);
                PulseGpuCart(_gpuCart, false);
            }

            if (pushing != null)
            {
                var z = AirportTaxiNetwork.StandZ(pushing.AssignedStand);
                var progress = VisualPhaseProgress(pushing, 0f);
                var tugPos = Vector3.Lerp(
                    new Vector3(15.2f, 0.4f, z),
                    new Vector3(11.2f, 0.4f, z - 2f),
                    Mathf.SmoothStep(0f, 1f, progress));
                PlaceProp(_pushbackTug, true, tugPos, Quaternion.LookRotation(new Vector3(-1f, 0f, -0.35f)));
                PulseServiceBeacon(_pushbackTug, true);
                SyncVehicleHeadlights(_pushbackTug, true, (float)_simulation.TimeOfDay.Daylight);
            }
            else
            {
                PlaceProp(_pushbackTug, false, Vector3.zero, Quaternion.identity);
            }
        }

        private static void PlaceProp(Transform prop, bool active, Vector3 position, Quaternion rotation)
        {
            if (prop == null)
                return;
            prop.gameObject.SetActive(active);
            if (!active)
                return;
            prop.position = position;
            prop.rotation = rotation;
        }

        private void UpdateWindsock()
        {
            if (_windsockSock == null)
                return;

            // Presentation-only: sock streams with a soft wind sway (not sim weather).
            var wind = 12f + Mathf.Sin(Time.unscaledTime * AirsideReusableMotion.WindsockSwayHz * Mathf.PI * 2f) * 8f;
            var sway = Mathf.Sin(Time.unscaledTime * AirsideReusableMotion.WindsockRippleHz * Mathf.PI * 2f) * 6f;
            _windsockSock.localRotation = Quaternion.Euler(0f, wind, sway);
            // Keep parent scale stable; ripple fabric segments so authored children keep shape.
            _windsockSock.localScale = Vector3.one;
            for (var i = 0; i < _windsockSock.childCount; i++)
            {
                var seg = _windsockSock.GetChild(i);
                var ripple = Mathf.Sin(
                    Time.unscaledTime * AirsideReusableMotion.WindsockRippleHz * Mathf.PI * 2f * 1.4f
                    + i * 1.35f) * 5f;
                seg.localRotation = Quaternion.Euler(ripple * 0.25f, 0f, ripple);
            }
        }

        private void UpdateTerminalFlag()
        {
            if (_terminalFlag == null)
            {
                _terminalFlag = GameObject.Find("flag_cloth")?.transform;
                if (_terminalFlag == null)
                    return;
            }

            // Soft flap on the terminal flag cloth (0025 item 7) — presentation only.
            var flap = Mathf.Sin(Time.unscaledTime * AirsideReusableMotion.FlagFlapHz * Mathf.PI * 2f) * 12f;
            var ripple = Mathf.Sin(Time.unscaledTime * AirsideReusableMotion.FlagRippleHz * Mathf.PI * 2f) * 4f;
            _terminalFlag.localRotation = Quaternion.Euler(flap * 0.15f, 0f, flap + ripple);
        }

        private void EnsureStandThreeVisual()
        {
            if (_standThreeVisualBuilt || !_simulation.Capacity.HasThirdStand)
                return;

            BuildStandMarking(17f, 26f, "Stand 3");
            CreateBlock("Stand 3 apron pad", new Vector3(20f, 0.01f, 26f), new Vector3(16f, 0.08f, 6f),
                new Color(0.34f, 0.36f, 0.37f),
                "Textures/Surfaces/tx_concrete_apron_basecolor_v01.png", new Vector2(2f, 1f));
            CreateTaxiLeadPad("Taxi lead Stand 3", standZ: 26f);
            // Extend apron north so Stand 3 is not an island past the concrete edge.
            CreateBlock("Apron north extension", new Vector3(20f, 0.005f, 24.5f), new Vector3(26f, 0.08f, 5f),
                new Color(0.36f, 0.38f, 0.39f),
                "Textures/Surfaces/tx_concrete_apron_basecolor_v01.png", new Vector2(3f, 1f));
            var propsKit = PreferArtKit(
                "Models/Props/mdl_airfield_props_kit_authored_v01.gltf",
                "Models/Props/mdl_airfield_props_kit_v02.gltf",
                "Models/Props/mdl_airfield_props_kit_v01.gltf");
            // One lead-in cone when props kit is heavy; pair only as greybox fallback.
            CreateCone(new Vector3(14.5f, 0.25f, 24.2f));
            if (string.IsNullOrEmpty(propsKit) || !ArtGltfLoader.HasKit(propsKit))
                CreateCone(new Vector3(14.5f, 0.25f, 27.8f));
            // Digit paint comes from PlaceWorldMarkings / PlaceRunwayDigit (kit-prefer).
            _standThreeVisualBuilt = true;
            // Pad is created after the Awake wet collect — refresh so Stand 3 rains too.
            CollectWetSurfaces();
        }

        private bool TaskActive(CommercialFlight flight, string name)
        {
            return flight.Turnaround != null &&
                   flight.Turnaround.Tasks(_clock.Now).Any(task => task.Name == name && task.State == TurnaroundTaskState.Active);
        }

        private static void UpdateVehicle(Transform vehicle, bool active, Vector3 servicePosition, Vector3 parkPosition)
        {
            if (vehicle == null)
                return;

            vehicle.gameObject.SetActive(true);
            var target = active ? servicePosition : parkPosition;
            // First show may still be at origin — start from the park bay.
            if (vehicle.position.sqrMagnitude < 0.01f)
                vehicle.position = parkPosition;

            var previous = vehicle.position;
            var speed = active ? 7.5f : 5.5f;
            vehicle.position = Vector3.MoveTowards(previous, target, Time.unscaledDeltaTime * speed);
            var travel = Vector3.Distance(previous, vehicle.position);
            if (travel > 0.001f)
            {
                var flat = target - previous;
                flat.y = 0f;
                if (flat.sqrMagnitude > 0.0001f)
                {
                    var look = Quaternion.LookRotation(flat.normalized, Vector3.up);
                    vehicle.rotation = Quaternion.Slerp(vehicle.rotation, look, Time.unscaledDeltaTime * 4f);
                }
            }

            if (!active && Vector3.Distance(vehicle.position, parkPosition) < 0.05f)
                ResetServiceLoopParts(vehicle);

            // Presentation-only: wheels roll while the vehicle is moving (ANM-VEH rates).
            var spinRpm = active
                ? AirsideReusableMotion.VehicleWheelRpmTaxi
                : AirsideReusableMotion.VehicleWheelRpmService;
            var degrees = travel * 120f + (travel > 0.001f ? Time.unscaledDeltaTime * spinRpm : 0f);
            if (degrees <= 0f)
                return;
            foreach (var child in vehicle.GetComponentsInChildren<Transform>(true))
            {
                if (child == vehicle)
                    continue;
                if (child.name.IndexOf("wheel", StringComparison.OrdinalIgnoreCase) >= 0
                    && child.name.IndexOf("arch", StringComparison.OrdinalIgnoreCase) < 0
                    && child.name.IndexOf("hub", StringComparison.OrdinalIgnoreCase) < 0)
                    child.Rotate(Vector3.right, degrees, Space.Self);
            }
        }

        private static void PulseServiceBeacon(Transform vehicle, bool active)
        {
            if (vehicle == null)
                return;
            foreach (var child in vehicle.GetComponentsInChildren<Transform>(true))
            {
                if (child == vehicle)
                    continue;
                if (child.name.IndexOf("beacon", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                child.gameObject.SetActive(active);
                if (!active)
                    continue;
                var on = Mathf.FloorToInt(Time.unscaledTime * AirsideReusableMotion.BeaconHz * 2f) % 2 == 0;
                var renderer = child.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var color = on ? new Color(1f, 0.35f, 0.08f) : new Color(0.35f, 0.12f, 0.05f);
                    SetRendererColor(renderer, color);
                    if (renderer.material.HasProperty("_EmissionColor"))
                    {
                        renderer.material.EnableKeyword("_EMISSION");
                        renderer.material.SetColor("_EmissionColor", color * (on ? 2.2f : 0.2f));
                    }
                }
            }
        }

        /// <summary>
        /// Decision 0025 items 5+7 — GSE headlamp SpotLights so night apron servicing reads lit.
        /// </summary>
        private static void SyncVehicleHeadlights(Transform vehicle, bool on, float daylight)
        {
            if (vehicle == null || !vehicle.gameObject.activeInHierarchy)
                return;

            EnsureVehicleHeadlightMeshes(vehicle);
            var night = daylight < 0.4f;
            foreach (var child in vehicle.GetComponentsInChildren<Transform>(true))
            {
                if (child == vehicle)
                    continue;
                if (child.name.IndexOf("Headlight", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                child.gameObject.SetActive(on);
                var light = child.GetComponent<Light>();
                if (light == null)
                {
                    light = child.gameObject.AddComponent<Light>();
                    light.type = LightType.Spot;
                    light.color = new Color(1f, 0.95f, 0.8f);
                    light.range = 14f;
                    light.spotAngle = 58f;
                    light.innerSpotAngle = 28f;
                    light.shadows = LightShadows.None;
                }

                // Authored GSE kits face along +X (cab/nose at +X); Unity SpotLights aim +Z.
                light.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                light.enabled = on;
                if (on)
                    light.intensity = night ? 2.8f : 1.1f;
            }
        }

        private static void EnsureVehicleHeadlightMeshes(Transform vehicle)
        {
            var has = false;
            foreach (var child in vehicle.GetComponentsInChildren<Transform>(true))
            {
                if (child != vehicle && child.name.IndexOf("Headlight", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    has = true;
                    break;
                }
            }

            if (has)
                return;

            // Fallback lamps sit on the forward bumper (+X cab end), not mid-body.
            ParentBlock(vehicle, "Headlight L", new Vector3(1.85f, 0.55f, 0.4f),
                new Vector3(0.12f, 0.1f, 0.12f), new Color(0.95f, 0.92f, 0.75f));
            ParentBlock(vehicle, "Headlight R", new Vector3(1.85f, 0.55f, -0.4f),
                new Vector3(0.12f, 0.1f, 0.12f), new Color(0.95f, 0.92f, 0.75f));
        }

        private static void PulseGpuCart(Transform gpu, bool active)
        {
            if (gpu == null)
                return;
            var light = gpu.GetComponentInChildren<Light>();
            if (light == null && active)
            {
                var go = new GameObject("GPU glow");
                go.transform.SetParent(gpu, false);
                go.transform.localPosition = new Vector3(0f, 0.6f, 0f);
                light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = 4.5f;
                light.color = new Color(0.55f, 0.85f, 1f);
            }

            if (light == null)
                return;
            light.enabled = active;
            if (active)
                light.intensity = 0.35f + 0.2f * (0.5f + 0.5f * Mathf.Sin(
                    Time.unscaledTime * AirsideReusableMotion.ServicePulseHz * Mathf.PI * 2f));
        }

        private static void ResetServiceLoopParts(Transform vehicle)
        {
            if (vehicle == null)
                return;

            foreach (var child in vehicle.GetComponentsInChildren<Transform>(true))
            {
                if (child == vehicle)
                    continue;
                // Exact names only — densified Hose reel/guard/tray and Cargo tags must not reset.
                if (child.name == "Hose")
                {
                    child.localScale = new Vector3(0.12f, 0.12f, 0.4f);
                    child.localPosition = new Vector3(child.localPosition.x, child.localPosition.y, 0.4f);
                }
                else if (child.name == "Cargo")
                {
                    var pos = child.localPosition;
                    pos.y = 0.35f;
                    child.localPosition = pos;
                }
                else if (child.name == "Door")
                {
                    child.localEulerAngles = Vector3.zero;
                }
            }
        }

        private static void AnimateServiceLoops(Transform vehicle, bool active, string partPrefix)
        {
            if (!active || vehicle == null || !vehicle.gameObject.activeSelf)
                return;

            foreach (var child in vehicle.GetComponentsInChildren<Transform>(true))
            {
                // Exact part names only so densified accessories (Hose reel, Cargo tag) stay put.
                // Cargo bags nest under Cargo so they bob with the crate (0025 item 7).
                if (child == vehicle || child.name != partPrefix)
                    continue;

                if (partPrefix == "Hose")
                {
                    var scale = child.localScale;
                    scale.z = Mathf.MoveTowards(scale.z, 2.4f, Time.unscaledDeltaTime * 1.8f);
                    child.localScale = scale;
                    child.localPosition = new Vector3(child.localPosition.x, child.localPosition.y, 0.2f + scale.z * 0.5f);
                }
                else if (partPrefix == "Cargo")
                {
                    var pos = child.localPosition;
                    pos.y = 0.35f + Mathf.Sin(Time.unscaledTime * 6f + child.GetInstanceID() * 0.01f) * 0.08f;
                    child.localPosition = pos;
                }
                else if (partPrefix == "Door")
                {
                    var euler = child.localEulerAngles;
                    var current = euler.y > 180f ? euler.y - 360f : euler.y;
                    euler.y = Mathf.MoveTowards(current, -70f, Time.unscaledDeltaTime * 100f);
                    child.localEulerAngles = euler;
                }
            }
        }

        private void UpdateWeatherPresentation()
        {
            var weather = _simulation.CurrentWeather;
            var raining = weather == WeatherKind.Rain || weather == WeatherKind.Storm;
            var wet = Weather.IsAdverse(weather);
            var storm = weather == WeatherKind.Storm;

            if (_rainRoot != null)
                _rainRoot.gameObject.SetActive(raining);

            if (raining && _rainRoot != null)
            {
                var fallBase = storm ? 20f : 12f;
                var drift = storm ? -3.2f : -1.5f;
                for (var i = 0; i < _rainRoot.childCount; i++)
                {
                    var drop = _rainRoot.GetChild(i);
                    var pos = drop.localPosition;
                    pos.y -= Time.unscaledDeltaTime * (fallBase + (i % 5));
                    if (pos.y < 0.5f)
                        pos.y = 18f + (i % 7);
                    pos.x += Time.unscaledDeltaTime * drift;
                    if (pos.x < -40f)
                        pos.x += 80f;
                    drop.localPosition = pos;
                    var thickness = storm ? 0.07f : 0.04f;
                    var length = storm ? 0.85f : 0.55f;
                    drop.localScale = new Vector3(thickness, length, thickness);
                }
            }

            if (wet)
            {
                // Cooler, denser atmosphere in adverse weather — stacks on base day fog.
                var fogDay = new Color(0.55f, 0.6f, 0.66f);
                var fogNight = new Color(0.18f, 0.22f, 0.3f);
                var daylight = (float)_simulation.TimeOfDay.Daylight;
                RenderSettings.ambientLight *= 0.9f;
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = Color.Lerp(fogNight, fogDay, Mathf.Max(daylight, 0.25f));
                var baseDensity = Mathf.Lerp(0.0065f, 0.0032f, daylight);
                // Adverse fog kept readable on the apron — thick enough to read FG/TSRA, not opaque.
                RenderSettings.fogDensity = weather == WeatherKind.Storm
                    ? Mathf.Max(baseDensity, 0.014f)
                    : weather == WeatherKind.Fog
                        ? Mathf.Max(baseDensity, 0.011f)
                        : raining
                            ? Mathf.Max(baseDensity, 0.0075f)
                            : baseDensity;
            }
            // Clear weather keeps the soft day fog applied in ApplyDayCycle.

            // Darken + gloss paved surfaces when wet (VFX-004 / material wet variants).
            // Clear weather keeps a soft residual damp on paved slabs (REF day apron).
            var rainWetness = wet
                ? (weather == WeatherKind.Storm ? 0.72f : raining ? 0.52f : 0.34f)
                : 0f;
            // Wetness only changes when the weather changes (four discrete values), and
            // ApplyWetness toggles shader keywords — which invalidates the SRP Batcher
            // batch for that material. Re-applying every frame tore the batcher down
            // continuously, so only walk the surfaces when the target actually moves.
            if (!Mathf.Approximately(rainWetness, _lastAppliedWetness))
            {
                _lastAppliedWetness = rainWetness;
                for (var i = 0; i < _wetSurfaces.Count; i++)
                {
                    var (renderer, dry, drySmooth, dryMetallic, dryBump, paved) = _wetSurfaces[i];
                    if (renderer == null)
                        continue;
                    var apply = wet ? rainWetness : (paved ? 0.06f : 0f);
                    AirsideMaterialLibrary.ApplyWetness(
                        renderer.material, apply, dry, drySmooth, dryMetallic, dryBump);
                }
            }

            UpdateWetPuddles(rainWetness, storm);
            UpdateTaxiSpray(rainWetness, raining || storm);

            // Refresh apron probe when wetness or dusk shifts so Lit pavement picks up floods.
            if (_apronProbe != null && Time.unscaledTime >= _apronProbeRefreshAt)
            {
                _apronProbe.intensity = Mathf.Lerp(0.75f, 1.15f, rainWetness);
                _apronProbe.RenderProbe();
                _apronProbeRefreshAt = Time.unscaledTime + (rainWetness > 0.05f ? 4.5f : 12f);
            }
        }

        private void UpdateTaxiSpray(float wetness, bool raining)
        {
            if (_taxiSprayRoot == null)
                return;

            var anyTaxi = false;
            foreach (var flight in _simulation.Flights)
            {
                if (flight.Operation.Phase is AircraftPhase.TaxiIn or AircraftPhase.TaxiOut
                    or AircraftPhase.Landing or AircraftPhase.Takeoff)
                {
                    anyTaxi = true;
                    break;
                }
            }

            var show = wetness > 0.12f && anyTaxi;
            _taxiSprayRoot.gameObject.SetActive(show);
            if (!show)
                return;

            // Follow the lead commercial gear so wet taxi throws mist.
            Transform lead = null;
            if (_commercialAircraft != null && _commercialAircraft.Length > 0)
                lead = _commercialAircraft[0];
            if (lead == null)
                return;

            _taxiSprayRoot.position = lead.position + Vector3.up * 0.2f;
            _taxiSprayRoot.rotation = lead.rotation;
            for (var i = 0; i < _taxiSprayRoot.childCount; i++)
            {
                var puff = _taxiSprayRoot.GetChild(i);
                var pulse = 0.7f + 0.3f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * (6f + i) + i));
                var side = i % 2 == 0 ? -0.65f : 0.65f;
                puff.localPosition = new Vector3(side, 0.08f + pulse * 0.12f, -0.4f - i * 0.15f);
                puff.localScale = new Vector3(0.55f, 0.25f, 0.55f) * pulse * (raining ? 1.25f : 1f);
                var renderer = puff.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var color = renderer.material.color;
                    color.a = (0.18f + wetness * 0.28f) * pulse;
                    SetRendererColor(renderer, color);
                }
            }
        }

        private void UpdateWetPuddles(float wetness, bool storm)
        {
            if (_wetPuddleRoot == null)
                return;
            var show = wetness > 0.05f;
            _wetPuddleRoot.gameObject.SetActive(show);
            if (!show)
                return;
            var alpha = Mathf.Lerp(0.12f, storm ? 0.42f : 0.32f, wetness);
            for (var i = 0; i < _wetPuddleRoot.childCount; i++)
            {
                var cluster = _wetPuddleRoot.GetChild(i);
                for (var b = 0; b < cluster.childCount; b++)
                {
                    var puddle = cluster.GetChild(b);
                    var renderer = puddle.GetComponent<Renderer>();
                    if (renderer == null)
                        continue;
                    var color = renderer.material.color;
                    color.a = alpha * (0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 0.7f + i + b * 0.4f));
                    SetRendererColor(renderer, color);
                }
            }
        }

        private void UpdateTouchdownSmoke()
        {
            if (_touchdownSmoke == null)
                return;

            for (var index = 0; index < _simulation.Flights.Count; index++)
            {
                var flight = _simulation.Flights[index];
                var phase = flight.Operation.Phase;
                var id = flight.AircraftId;

                // Fire once when the visual path actually meets the runway — not at the
                // Approach→Landing phase change (that is still ~1.5 m AGL after the path fix).
                if (phase == AircraftPhase.Landing
                    && index < _commercialAircraft.Length
                    && !_touchdownFired.Contains(id)
                    && VisualPhaseProgress(flight, 0f) >= 0.28f)
                {
                    _touchdownFired.Add(id);
                    _touchdownSmoke.position = _commercialAircraft[index].position + Vector3.up * 0.15f;
                    _touchdownSmoke.rotation = _commercialAircraft[index].rotation;
                    _touchdownSmoke.localScale = Vector3.one;
                    for (var p = 0; p < _touchdownSmoke.childCount; p++)
                    {
                        var puff = _touchdownSmoke.GetChild(p);
                        var side = p % 2 == 0 ? -0.75f : 0.75f;
                        var aft = -0.15f * (p / 2);
                        puff.localPosition = new Vector3(side, 0.12f, aft);
                    }

                    _touchdownSmoke.gameObject.SetActive(true);
                    _touchdownSmokeRemaining = 1.35f;
                    SpawnSkidMarks(_commercialAircraft[index]);
                    if (_touchdownAudio != null && _touchdownClip != null && !_audioMuted)
                    {
                        _touchdownAudio.transform.position = _touchdownSmoke.position;
                        _touchdownAudio.PlayOneShot(_touchdownClip, 0.35f);
                    }

                    if (_cameraController != null)
                        _cameraController.PulseTouchdown();
                }
                else if (phase != AircraftPhase.Landing)
                {
                    _touchdownFired.Remove(id);
                }

                _previousPhases[id] = phase;
            }

            if (_touchdownSmokeRemaining <= 0f)
            {
                _touchdownSmoke.gameObject.SetActive(false);
            }
            else
            {
                _touchdownSmokeRemaining -= Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(_touchdownSmokeRemaining / 1.35f);
                for (var i = 0; i < _touchdownSmoke.childCount; i++)
                {
                    var puff = _touchdownSmoke.GetChild(i);
                    puff.localScale = Vector3.Lerp(new Vector3(2.8f, 0.25f, 2.8f), new Vector3(1.0f, 0.35f, 1.0f), t);
                    puff.localPosition += Vector3.up * (Time.unscaledDeltaTime * 0.35f);
                    var renderer = puff.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        var color = renderer.material.color;
                        color.a = t * 0.5f;
                        SetRendererColor(renderer, color);
                    }
                }
            }

            UpdateSkidMarks();
        }

        private void SpawnSkidMarks(Transform aircraft)
        {
            if (_skidMarkRoot == null || aircraft == null)
                return;

            // Two dark rubber streaks under main gear — fade over ~22s (presentation only).
            for (var i = 0; i < 2; i++)
            {
                var mark = GameObject.CreatePrimitive(PrimitiveType.Cube);
                mark.name = "Skid mark";
                Object.Destroy(mark.GetComponent<Collider>());
                mark.transform.SetParent(_skidMarkRoot, false);
                var side = i == 0 ? -0.75f : 0.75f;
                mark.transform.position = aircraft.position
                    + aircraft.right * side
                    + aircraft.forward * -0.4f
                    + Vector3.up * 0.04f;
                var fwd = Vector3.ProjectOnPlane(aircraft.forward, Vector3.up);
                if (fwd.sqrMagnitude < 0.0001f)
                    fwd = Vector3.forward;
                mark.transform.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);
                mark.transform.localScale = new Vector3(0.22f, 0.02f, 3.6f);
                mark.GetComponent<Renderer>().material = CreateMaterial(new Color(0.12f, 0.11f, 0.1f, 0.7f));
            }
        }

        private void UpdateSkidMarks()
        {
            if (_skidMarkRoot == null)
                return;

            for (var i = _skidMarkRoot.childCount - 1; i >= 0; i--)
            {
                var mark = _skidMarkRoot.GetChild(i);
                var renderer = mark.GetComponent<Renderer>();
                if (renderer == null)
                {
                    Object.Destroy(mark.gameObject);
                    continue;
                }

                var color = renderer.material.color;
                color.a -= Time.unscaledDeltaTime / 22f;
                if (color.a <= 0.02f)
                {
                    Object.Destroy(mark.gameObject);
                    continue;
                }

                SetRendererColor(renderer, color);
                // Stretch slightly as the mark ages so it reads as a rollout streak.
                var scale = mark.localScale;
                scale.z = Mathf.MoveTowards(scale.z, 5.2f, Time.unscaledDeltaTime * 0.08f);
                mark.localScale = scale;
            }
        }

        private void CollectHoldShortMarkings()
        {
            _holdShortRenderers.Clear();
            foreach (var name in new[]
                     {
                         "Hold short A", "Hold short B", "Hold short C", "Hold short D",
                         "Hold short E", "Hold short F",
                         "hold_short_a", "hold_short_b", "hold_short_c", "hold_short_d",
                         "hold_short_e", "hold_short_f"
                     })
            {
                var go = GameObject.Find(name);
                if (go == null)
                    continue;
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null && !_holdShortRenderers.Contains(renderer))
                    _holdShortRenderers.Add(renderer);
            }
        }

        private void UpdateTrafficWaitPresentation()
        {
            // Presentation-only: pulse hold-short bars when a traffic wait is active.
            var warning = _simulation.TrafficWaits.HasWarning(_clock.Now);
            var pulse = warning
                ? 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(
                    Time.unscaledTime * AirsideReusableMotion.ServicePulseHz * Mathf.PI * 2f))
                : 1f;
            var baseColor = new Color(0.95f, 0.82f, 0.12f);
            var hot = new Color(1f, 0.45f, 0.12f);
            var color = warning ? Color.Lerp(baseColor, hot, pulse) : baseColor;
            for (var i = 0; i < _holdShortRenderers.Count; i++)
            {
                var renderer = _holdShortRenderers[i];
                if (renderer == null)
                    continue;
                SetRendererColor(renderer, color);
                if (renderer.material.HasProperty("_EmissionColor"))
                {
                    renderer.material.EnableKeyword("_EMISSION");
                    var emit = warning
                        ? new Color(1f, 0.4f, 0.08f) * (0.35f + pulse * 1.4f)
                        : Color.black;
                    renderer.material.SetColor("_EmissionColor", emit);
                }
            }
        }


        /// <summary>
        /// True for paved / painted surfaces that keep a residual damp sheen in clear
        /// weather. Constant for the lifetime of a renderer, so it is resolved once in
        /// <see cref="CollectWetSurfaces"/> rather than re-tested every frame.
        /// </summary>
        private static bool IsPavedSurfaceName(string name)
        {
            return (name is "Runway" or "Taxiway A" or "Apron" or "Stand 3 apron pad"
                    or "Access road" or "Access road turn" or "Car park" or "Service lane" or "Fuel pad")
                || name.StartsWith("Apron joint", StringComparison.Ordinal)
                || name.StartsWith("Apron slab", StringComparison.Ordinal)
                || name.StartsWith("Apron fringe", StringComparison.Ordinal)
                || name.StartsWith("Runway marking", StringComparison.Ordinal)
                || name.StartsWith("Runway edge", StringComparison.Ordinal)
                || name.StartsWith("Threshold", StringComparison.Ordinal)
                || name.StartsWith("Hold short", StringComparison.Ordinal)
                || name.StartsWith("Taxi edge", StringComparison.Ordinal)
                || name.StartsWith("Stand stop", StringComparison.Ordinal)
                || name.StartsWith("Stand number", StringComparison.Ordinal)
                || name.StartsWith("Access turn", StringComparison.Ordinal)
                || name.StartsWith("Drop-off zebra", StringComparison.Ordinal)
                || name.StartsWith("Overflow bay", StringComparison.Ordinal)
                || name.StartsWith("Bay line", StringComparison.Ordinal)
                || name.StartsWith("Taxi arrow", StringComparison.Ordinal)
                || name.StartsWith("Runway digit", StringComparison.Ordinal)
                || name.StartsWith("Stand lead", StringComparison.Ordinal)
                || name.StartsWith("Apron chevron", StringComparison.Ordinal)
                || name.StartsWith("Hold short", StringComparison.Ordinal)
                || name.StartsWith("Taxi centre", StringComparison.Ordinal)
                || name.StartsWith("Aiming point", StringComparison.Ordinal)
                || name.StartsWith("TDZ ", StringComparison.Ordinal)
                || name.StartsWith("Threshold stripe", StringComparison.Ordinal)
                || name.StartsWith("Jetty ", StringComparison.Ordinal)
                || name.StartsWith("ARFF apron", StringComparison.Ordinal)
                || name.StartsWith("Fuel ", StringComparison.Ordinal)
                || name.StartsWith("Terminal canopy", StringComparison.Ordinal)
                || name.StartsWith("Stand box", StringComparison.Ordinal)
                || name.StartsWith("Access road shoulder", StringComparison.Ordinal)
                || name.StartsWith("Runway shoulder", StringComparison.Ordinal)
                || name.StartsWith("Access turn shoulder", StringComparison.Ordinal)
                || name.StartsWith("Car park kerb", StringComparison.Ordinal)
                || name.StartsWith("Relief berm", StringComparison.Ordinal)
                || name.StartsWith("Relief mound", StringComparison.Ordinal)
                || name.StartsWith("runway_centre", StringComparison.Ordinal)
                || name.StartsWith("runway_edge_left", StringComparison.Ordinal)
                || name.StartsWith("runway_edge_right", StringComparison.Ordinal)
                || name.StartsWith("runway_threshold", StringComparison.Ordinal)
                || name.StartsWith("taxi_centreline", StringComparison.Ordinal)
                || name.StartsWith("taxi_edge_", StringComparison.Ordinal)
                || name.StartsWith("taxi_arrow_", StringComparison.Ordinal)
                || name.StartsWith("hold_short_", StringComparison.Ordinal)
                || name.StartsWith("threshold_", StringComparison.Ordinal)
                || name.StartsWith("stand_stop_", StringComparison.Ordinal)
                || name.StartsWith("aiming_", StringComparison.Ordinal)
                || name.StartsWith("tdz_", StringComparison.Ordinal)
                || name.StartsWith("chevron_", StringComparison.Ordinal)
                || name.StartsWith("digit_", StringComparison.Ordinal)
                || name.StartsWith("apron_arrow_", StringComparison.Ordinal);
        }

        private void CollectWetSurfaces()
        {
            _wetSurfaces.Clear();
            // New surfaces have never been wetted — force the next weather pass to apply.
            _lastAppliedWetness = float.NaN;
            foreach (var renderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (renderer == null)
                    continue;
                var n = renderer.gameObject.name;
                if (!(n is "Runway" or "Taxiway A" or "Apron" or "Stand 3 apron pad"
                        or "Access road" or "Access road turn" or "Car park" or "Service lane"
                        or "Fuel pad" or "Grass"
                        or "Access road shoulder L" or "Access road shoulder R"
                        or "Runway shoulder N" or "Runway shoulder S"
                        or "Relief berm N" or "Relief berm S")
                    && !n.StartsWith("Coast ", StringComparison.Ordinal)
                    && !n.StartsWith("Outer paddock", StringComparison.Ordinal)
                    && !n.StartsWith("Coast dune", StringComparison.Ordinal)
                    && !n.StartsWith("Hill far", StringComparison.Ordinal)
                    && !n.StartsWith("Car park kerb", StringComparison.Ordinal)
                    && !n.StartsWith("Coast scrub", StringComparison.Ordinal)
                    && !n.StartsWith("Apron joint", StringComparison.Ordinal)
                    && !n.StartsWith("Apron fringe", StringComparison.Ordinal)
                    && !n.StartsWith("Apron slab", StringComparison.Ordinal)
                    && !n.StartsWith("Runway marking", StringComparison.Ordinal)
                    && !n.StartsWith("Runway edge", StringComparison.Ordinal)
                    && !n.StartsWith("Threshold", StringComparison.Ordinal)
                    && !n.StartsWith("Hold short", StringComparison.Ordinal)
                    && !n.StartsWith("Taxi edge", StringComparison.Ordinal)
                    && !n.StartsWith("Stand stop", StringComparison.Ordinal)
                    && !n.StartsWith("Stand number", StringComparison.Ordinal)
                    && !n.StartsWith("Access turn", StringComparison.Ordinal)
                    && !n.StartsWith("Drop-off zebra", StringComparison.Ordinal)
                    && !n.StartsWith("Overflow bay", StringComparison.Ordinal)
                    && !n.StartsWith("Bay line", StringComparison.Ordinal)
                    && !n.StartsWith("Taxi arrow", StringComparison.Ordinal)
                    && !n.StartsWith("Runway digit", StringComparison.Ordinal)
                    && !n.StartsWith("Stand lead", StringComparison.Ordinal)
                    && !n.StartsWith("Apron chevron", StringComparison.Ordinal)
                    && !n.StartsWith("Hold short", StringComparison.Ordinal)
                    && !n.StartsWith("Taxi centre", StringComparison.Ordinal)
                    && !n.StartsWith("Aiming point", StringComparison.Ordinal)
                    && !n.StartsWith("TDZ ", StringComparison.Ordinal)
                    && !n.StartsWith("Threshold stripe", StringComparison.Ordinal)
                    && !n.StartsWith("Jetty ", StringComparison.Ordinal)
                    && !n.StartsWith("ARFF apron", StringComparison.Ordinal)
                    && !n.StartsWith("Fuel ", StringComparison.Ordinal)
                    && !n.StartsWith("Terminal canopy", StringComparison.Ordinal)
                    && !n.StartsWith("Stand box", StringComparison.Ordinal)
                    && !n.StartsWith("Access road shoulder", StringComparison.Ordinal)
                    && !n.StartsWith("Runway shoulder", StringComparison.Ordinal)
                    && !n.StartsWith("Access turn shoulder", StringComparison.Ordinal)
                    // Authored markings kit mesh names (glTF nodes), not CreateBlock titles.
                    // Keep prefixes tight so lighting kit taxi_/edge_/runway_edge_light stay dry.
                    && !n.StartsWith("runway_centre", StringComparison.Ordinal)
                    && !n.StartsWith("runway_edge_left", StringComparison.Ordinal)
                    && !n.StartsWith("runway_edge_right", StringComparison.Ordinal)
                    && !n.StartsWith("runway_threshold", StringComparison.Ordinal)
                    && !n.StartsWith("taxi_centreline", StringComparison.Ordinal)
                    && !n.StartsWith("taxi_edge_", StringComparison.Ordinal)
                    && !n.StartsWith("taxi_arrow_", StringComparison.Ordinal)
                    && !n.StartsWith("hold_short_", StringComparison.Ordinal)
                    && !n.StartsWith("threshold_", StringComparison.Ordinal)
                    && !n.StartsWith("stand_stop_", StringComparison.Ordinal)
                    && !n.StartsWith("aiming_", StringComparison.Ordinal)
                    && !n.StartsWith("tdz_", StringComparison.Ordinal)
                    && !n.StartsWith("chevron_", StringComparison.Ordinal)
                    && !n.StartsWith("digit_", StringComparison.Ordinal)
                    && !n.StartsWith("apron_arrow_", StringComparison.Ordinal)
                    && !n.StartsWith("Relief mound", StringComparison.Ordinal)
                    && !n.StartsWith("Grass ribbon", StringComparison.Ordinal)
                    && !n.StartsWith("Coast dune", StringComparison.Ordinal))
                    continue;

                var mat = renderer.material;
                var drySmooth = 0.28f;
                if (mat.HasProperty("_Smoothness"))
                    drySmooth = mat.GetFloat("_Smoothness");
                else if (mat.HasProperty("_Glossiness"))
                    drySmooth = mat.GetFloat("_Glossiness");
                var dryMetallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0.02f;
                var dryBump = mat.HasProperty("_BumpScale") ? mat.GetFloat("_BumpScale") : 0.5f;
                _wetSurfaces.Add((renderer, mat.color, drySmooth, dryMetallic, dryBump,
                    IsPavedSurfaceName(n)));
            }
        }

        /// <summary>
        /// Soft reflective puddle discs on the apron / taxi — visible wet response beyond
        /// material darken (0025 items 4+7). Presentation only.
        /// </summary>
        private void BuildWetPuddles()
        {
            var root = new GameObject("Wet puddles").transform;
            _wetPuddleRoot = root;
            var spots = new[]
            {
                new Vector3(18f, 0.07f, 15f),
                new Vector3(24f, 0.07f, 19f),
                new Vector3(14f, 0.07f, 20.5f),
                new Vector3(28f, 0.07f, 14.5f),
                new Vector3(8f, 0.07f, 10f),
                new Vector3(4f, 0.07f, 9f),
                new Vector3(20f, 0.07f, 12f),
                new Vector3(-18f, 0.07f, 16f),
                new Vector3(32f, 0.07f, 18f),
                new Vector3(22f, 0.07f, 22f),
                new Vector3(16f, 0.07f, 17.5f),
                new Vector3(26f, 0.07f, 16f),
                new Vector3(10f, 0.07f, 14f),
                new Vector3(30f, 0.07f, 21f),
                new Vector3(-14f, 0.07f, 18f),
                new Vector3(12f, 0.07f, 18.5f),
                new Vector3(34f, 0.07f, 15f),
                new Vector3(6f, 0.07f, 12.5f),
                new Vector3(48f, 0.07f, 46f),
                new Vector3(44f, 0.07f, 44f),
                new Vector3(26f, 0.07f, 38f),
                new Vector3(-20f, 0.07f, 28.5f),
                new Vector3(-34f, 0.07f, 22f),
                new Vector3(0f, 0.07f, 2f),
                new Vector3(-6f, 0.07f, 0.5f),
                new Vector3(6f, 0.07f, -0.5f),
                new Vector3(18f, 0.07f, 9f),
                new Vector3(2f, 0.07f, 9f),
                new Vector3(40f, 0.07f, 46f),
                new Vector3(52f, 0.07f, 48f),
                new Vector3(-24f, 0.07f, 28f),
                new Vector3(22f, 0.07f, 14f)
            };

            // Batch F4 VFX-004 — reusable wet accent kit first (presentation only).
            var hasWetKit = ArtPresentationLoader.TryInstantiatePrefab("vfx_wet_surface_response_v01", out var wetKit);
            if (hasWetKit)
            {
                wetKit.SetParent(root, false);
                wetKit.localPosition = new Vector3(20f, 0f, 16f);
                wetKit.name = "Wet surface kit";
            }

            // Kit owns the wet read — keep a short hero apron/taxi set; full carpet is fallback.
            var spotCount = hasWetKit ? 10 : spots.Length;
            for (var i = 0; i < spotCount; i++)
            {
                // Irregular multi-blob puddles (REF soft damp patches, not toy discs).
                var cluster = new GameObject($"Puddle {i}").transform;
                cluster.SetParent(root, false);
                cluster.position = spots[i];
                var blobs = hasWetKit ? 1 + (i % 2) : 2 + (i % 3);
                for (var b = 0; b < blobs; b++)
                {
                    var puddle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    puddle.name = $"Puddle {i} blob {b}";
                    Object.Destroy(puddle.GetComponent<Collider>());
                    puddle.transform.SetParent(cluster, false);
                    var ox = ((b * 37 + i * 13) % 17) * 0.06f - 0.4f;
                    var oz = ((b * 29 + i * 11) % 15) * 0.07f - 0.35f;
                    puddle.transform.localPosition = new Vector3(ox, 0f, oz);
                    var rx = 0.7f + (i % 3) * 0.35f + b * 0.15f;
                    var rz = rx * (0.45f + (b % 3) * 0.22f);
                    puddle.transform.localScale = new Vector3(rx, 0.012f, rz);
                    puddle.transform.localRotation = Quaternion.Euler(0f, (i * 23 + b * 41) % 360, 0f);
                    var material = AirsideMaterialLibrary.Create(
                        new Color(0.2f, 0.28f, 0.34f, 0.28f),
                        AirsideMaterialLibrary.SurfaceKind.Water);
                    if (material.HasProperty("_Smoothness"))
                        material.SetFloat("_Smoothness", 0.96f);
                    if (material.HasProperty("_Metallic"))
                        material.SetFloat("_Metallic", 0.35f);
                    puddle.GetComponent<Renderer>().material = material;
                    puddle.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }

            root.gameObject.SetActive(false);
        }

        private void CollectAirfieldLights()
        {
            _airfieldLightRenderers.Clear();
            foreach (var renderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (renderer == null)
                    continue;
                var n = renderer.gameObject.name;
                if (n.StartsWith("Runway edge", StringComparison.Ordinal) ||
                    n.StartsWith("Taxi light", StringComparison.Ordinal) ||
                    n.StartsWith("ALS", StringComparison.Ordinal) ||
                    n.StartsWith("REIL", StringComparison.Ordinal) ||
                    n.StartsWith("Apron flood", StringComparison.Ordinal) ||
                    n.StartsWith("edge_", StringComparison.Ordinal) ||
                    n.StartsWith("taxi_", StringComparison.Ordinal) ||
                    n.StartsWith("flood_", StringComparison.Ordinal) ||
                    n.StartsWith("obst_", StringComparison.Ordinal) ||
                    n == "runway_edge_light" ||
                    n == "taxiway_light" ||
                    n == "apron_floodlight" ||
                    n == "obstruction_light")
                    _airfieldLightRenderers.Add(renderer);
            }
        }

        private static Transform BuildRainRoot()
        {
            var root = new GameObject("Rain").transform;
            root.position = new Vector3(0f, 0f, 8f);

            // Batch F4 VFX-003 — seed from reusable kit when present, then stamp a dense field.
            Transform seed = null;
            if (ArtPresentationLoader.TryInstantiatePrefab("vfx_rain_airfield_v01", out var kit))
            {
                kit.SetParent(root, false);
                kit.localPosition = Vector3.zero;
                kit.name = "Rain kit seed";
                seed = kit;
            }

            var rng = new System.Random(42);
            // Kit drops already read; keep a calmer field so rain is weather, not soup.
            var dropCount = seed != null ? 40 : 96;
            for (var i = 0; i < dropCount; i++)
            {
                GameObject drop;
                if (seed != null && seed.childCount > 0)
                {
                    var src = seed.GetChild(i % seed.childCount);
                    drop = Object.Instantiate(src.gameObject);
                    drop.name = $"Rain {i}";
                    drop.transform.SetParent(root, false);
                }
                else
                {
                    drop = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    drop.name = $"Rain {i}";
                    drop.transform.SetParent(root, false);
                    drop.transform.localScale = new Vector3(0.04f, 0.55f, 0.04f);
                    drop.transform.localRotation = Quaternion.Euler(12f, 0f, 8f);
                    drop.GetComponent<Renderer>().material = AirsideMaterialLibrary.Create(
                        new Color(0.7f, 0.78f, 0.88f, 0.35f),
                        AirsideMaterialLibrary.SurfaceKind.Default);
                    var collider = drop.GetComponent<Collider>();
                    if (collider != null)
                        Object.Destroy(collider);
                }

                drop.transform.localPosition = new Vector3(
                    (float)(rng.NextDouble() * 80f - 40f),
                    (float)(rng.NextDouble() * 16f + 2f),
                    (float)(rng.NextDouble() * 50f - 10f));
                drop.transform.localRotation = Quaternion.Euler(12f, 0f, 8f);
            }

            if (seed != null)
                Object.Destroy(seed.gameObject);

            root.gameObject.SetActive(false);
            return root;
        }

        private static Transform BuildTouchdownSmoke()
        {
            if (ArtPresentationLoader.TryInstantiatePrefab("vfx_touchdown_smoke_v01", out var kit))
            {
                kit.name = "Touchdown smoke";
                kit.gameObject.SetActive(false);
                return kit;
            }

            var root = new GameObject("Touchdown smoke").transform;
            for (var i = 0; i < 4; i++)
            {
                var smoke = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                smoke.name = i % 2 == 0 ? "Smoke L" : "Smoke R";
                smoke.transform.SetParent(root, false);
                var side = i % 2 == 0 ? -0.75f : 0.75f;
                smoke.transform.localPosition = new Vector3(side, 0.12f, -0.15f * (i / 2));
                smoke.transform.localScale = new Vector3(1.1f, 0.35f, 1.1f);
                smoke.GetComponent<Renderer>().material = CreateMaterial(new Color(0.85f, 0.85f, 0.88f, 0.4f));
                var collider = smoke.GetComponent<Collider>();
                if (collider != null)
                    Object.Destroy(collider);
            }

            root.gameObject.SetActive(false);
            return root;
        }

        private static Transform BuildSkidMarkRoot()
        {
            var root = new GameObject("Skid marks").transform;
            return root;
        }

        private static Transform BuildTaxiSprayRoot()
        {
            var root = new GameObject("Taxi spray").transform;
            for (var i = 0; i < 4; i++)
            {
                var puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                puff.name = $"Spray {i}";
                Object.Destroy(puff.GetComponent<Collider>());
                puff.transform.SetParent(root, false);
                puff.transform.localScale = new Vector3(0.5f, 0.22f, 0.5f);
                puff.GetComponent<Renderer>().material = CreateMaterial(new Color(0.75f, 0.8f, 0.85f, 0.25f));
            }

            root.gameObject.SetActive(false);
            return root;
        }

        private void OnGUI()
        {
            // Toolkit owns the full gameplay HUD + overlays when active — skip IMGUI
            // entirely so first-session density is Toolkit/uGUI only (0025 item 6).
            if (_toolkitHud != null && _toolkitHud.IsActive)
                return;

            var scale = HudLayout.ScaleFor(Screen.width, Screen.height);
            var previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            // Approved Airside HUD direction (docs/art/ART_DIRECTION_AND_ASSET_SPEC.md,
            // REF-004): translucent Runway Ink panels, Cloud text, Coastal Blue for
            // buttons, Safety Yellow for caution, Clear Green for on-time, Signal Red
            // reserved for delay.
            var panel = AirsideTheme.PanelStyle(new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(18, 18, 14, 14)
            });
            var title = AirsideTheme.TextStyle(new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold });
            var detail = AirsideTheme.TextStyle(new GUIStyle(GUI.skin.label) { fontSize = 17 });
            var small = AirsideTheme.TextStyle(new GUIStyle(GUI.skin.label) { fontSize = 14 });
            var caution = AirsideTheme.CautionStyle(small);
            var onTime = AirsideTheme.TextStyle(new GUIStyle(small), AirsideTheme.ClearGreen);
            var delayed = AirsideTheme.TextStyle(new GUIStyle(small), AirsideTheme.SignalRed);
            var button = AirsideTheme.TextStyle(new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold }, AirsideTheme.Cloud);

            // Canvas HUD owns the left status / hire / research panel when active.
            if (!_canvasHudActive)
            {
            var timeOfDay = _simulation.TimeOfDay;
            var earlySession = _simulation.Routes.Accepted.Count == 0;
            var atStand = _simulation.ActiveAircraft.Phase == AircraftPhase.AtStand && _simulation.ActiveTurnaround != null;
            var turnaroundTaskCount = atStand ? _simulation.ActiveTurnaround.Tasks(_clock.Now).Count() : 0;
            // Dynamic left panel: shorter in the first session so the world stays visible.
            var leftPanelHeight = earlySession
                ? 420f
                : Mathf.Clamp(360f + turnaroundTaskCount * 19f + (atStand ? 70f : 0f) + 140f, 420f, 580f);
            var leftPanelRect = new Rect(22, 22, 410, leftPanelHeight);
            GUI.Box(leftPanelRect, string.Empty, panel);
            AirsideTheme.DrawPanelFrame(leftPanelRect);

            var y = 36f;
            var wordmark = AirsideTheme.WordmarkLight;
            if (wordmark != null)
            {
                GUI.DrawTexture(new Rect(42, 28, 240, 40), wordmark, ScaleMode.ScaleToFit, alphaBlend: true);
                y = 70f;
                GUI.Label(new Rect(42, y, 380, 18), $"{_simulation.Location.Name}  ·  {_simulation.Location.Region}", small);
                y += 20f;
            }
            else
            {
                GUI.Label(new Rect(42, y, 320, 34), "AIRSIDE", title);
                y += 28f;
                GUI.Label(new Rect(42, y, 380, 18), $"{_simulation.Location.Name}  ·  {_simulation.Location.Region}", small);
                y += 20f;
            }

            GUI.Label(new Rect(42, y, 380, 22), CommercialFlightHudLine(), detail);
            y += 24f;

            var phaseLineX = 42f;
            var phaseIcon = _simulation.Flights.Count > 0
                ? AirsideTheme.OperationIcon(_simulation.Flights[0].Operation.Phase)
                : null;
            if (phaseIcon != null)
            {
                GUI.DrawTexture(new Rect(42, y, 20, 20), phaseIcon, ScaleMode.ScaleToFit, alphaBlend: true);
                phaseLineX = 68f;
            }
            GUI.Label(new Rect(phaseLineX, y, 380 - (phaseLineX - 42), 22), CommercialPhaseHudLine(), detail);
            y += 24f;

            var weatherLabel = Weather.Describe(_simulation.CurrentWeather);
            if (Weather.IsAdverse(_simulation.CurrentWeather))
                weatherLabel += " · wet apron";
            var clockStyle = _paused || _speed > 1 ? caution : small;
            var weatherLineX = 42f;
            var weatherIcon = AirsideTheme.WeatherIcon(_simulation.CurrentWeather);
            if (weatherIcon != null)
            {
                GUI.DrawTexture(new Rect(42, y, 20, 20), weatherIcon, ScaleMode.ScaleToFit, alphaBlend: true);
                weatherLineX = 68f;
            }
            GUI.Label(new Rect(weatherLineX, y, 380 - (weatherLineX - 42), 22),
                $"{(_paused ? "PAUSED" : $"{_speed}× time")}{(_audioMuted ? "  ·  MUTED" : string.Empty)}  ·  Day {timeOfDay.DaysElapsed + 1} {timeOfDay.Clock} {timeOfDay.Phase}  ·  {weatherLabel}", clockStyle);
            y += 24f;

            var cashStyle = _simulation.Economy.Cash < 0 ? delayed : small;
            var reputationStyle = ReputationBandStyle(small, onTime, caution, delayed);
            var cashIcon = AirsideTheme.Icon("economy", "cash");
            var cashX = 42f;
            if (cashIcon != null)
            {
                GUI.DrawTexture(new Rect(42, y, 18, 18), cashIcon, ScaleMode.ScaleToFit, alphaBlend: true);
                cashX = 64f;
            }
            GUI.Label(new Rect(cashX, y, 200 - (cashX - 42), 22), $"Cash: ${_simulation.Economy.Cash:N0}  ·  Cycles {_simulation.CompletedCycles}", cashStyle);
            var repIcon = AirsideTheme.Icon("economy", "reputation");
            var repX = 242f;
            if (repIcon != null)
            {
                GUI.DrawTexture(new Rect(242, y, 18, 18), repIcon, ScaleMode.ScaleToFit, alphaBlend: true);
                repX = 264f;
            }
            GUI.Label(new Rect(repX, y, 190 - (repX - 242), 22),
                $"Rep {_simulation.Reputation.Score} ({_simulation.Reputation.Band})", reputationStyle);
            y += 22f;

            var finance = _simulation.DailyFinance;
            var runway = finance.CashRunwayDays is int days
                ? $"  ·  ~{days}d runway"
                : "  ·  cash building";
            var financeStyle = finance.ExpectedNet < 0 ? delayed
                : finance.CashRunwayDays is int runwayDays && runwayDays <= 3 ? caution
                : onTime;
            var incomeIcon = AirsideTheme.Icon("economy", "income");
            var financeX = 42f;
            if (incomeIcon != null)
            {
                GUI.DrawTexture(new Rect(42, y, 18, 18), incomeIcon, ScaleMode.ScaleToFit, alphaBlend: true);
                financeX = 64f;
            }
            GUI.Label(new Rect(financeX, y, 390 - (financeX - 42), 22),
                $"Day est. {finance.ExpectedNet:+$#,0;-$#,0;$0} (in ${finance.ExpectedFlightIncome:N0} / out ${finance.ExpectedOperatingCost:N0}){runway}", financeStyle);
            y += 22f;

            if (_simulation.IsInsolvent)
            {
                GUI.Label(new Rect(42, y, 360, 22), "INSOLVENT — operations frozen", delayed);
                y += 22f;
            }
            else if (_simulation.Economy.ConsecutiveNegativeDays > 0)
            {
                var left = AirportEconomy.InsolvencyConsecutiveDays - _simulation.Economy.ConsecutiveNegativeDays;
                GUI.Label(new Rect(42, y, 360, 22),
                    $"Cash warning: {_simulation.Economy.ConsecutiveNegativeDays} negative day close(s) · {left} more → insolvent", caution);
                y += 22f;
            }
            else if (_simulation.TrafficWaits.HasWarning(_clock.Now))
            {
                GUI.Label(new Rect(42, y, 360, 22), $"TRAFFIC: {_simulation.TrafficWaits.Describe(_clock.Now)}", caution);
                y += 22f;
            }

            if (atStand)
            {
                foreach (var task in _simulation.ActiveTurnaround.Tasks(_clock.Now))
                {
                    var mark = task.State == TurnaroundTaskState.Complete ? "✓" : task.State == TurnaroundTaskState.Active ? "●" : "○";
                    var time = task.State == TurnaroundTaskState.Complete ? string.Empty : $"  {task.SecondsRemaining}s";
                    var taskIcon = AirsideTheme.ServiceIconForTask(task.Name);
                    var taskX = 42f;
                    if (taskIcon != null)
                    {
                        GUI.DrawTexture(new Rect(42, y, 16, 16), taskIcon, ScaleMode.ScaleToFit, alphaBlend: true);
                        taskX = 62f;
                    }
                    GUI.Label(new Rect(taskX, y, 350 - (taskX - 42), 20), $"{mark} {task.Name}{time}", small);
                    y += 19f;
                }

                if (_simulation.CurrentDelaySeconds > 0)
                {
                    GUI.Label(new Rect(42, y, 360, 22), $"DELAY +{_simulation.CurrentDelaySeconds}s · {_simulation.CurrentDelayCause}", delayed);
                    y += 22f;
                }

                var alreadyAssigned = _simulation.ActiveTurnaround != null && _simulation.ActiveTurnaround.PriorityCrewEnabled;
                GUI.enabled = !_simulation.IsInsolvent && !alreadyAssigned && _simulation.Economy.Cash >= AirportEconomy.PriorityCrewCost;
                if (GUI.Button(new Rect(42, y, 190, 27), alreadyAssigned ? "Priority crew active" : "Hire priority crew · $300", button))
                    _session.EnablePriorityCrew();
                GUI.enabled = true;
                y += 34f;
            }
            else
            {
                var onSchedule = _simulation.LastDelaySeconds <= 0;
                GUI.Label(new Rect(42, y, 350, 22), onSchedule
                    ? "Operations running to schedule"
                    : $"Last flight delay: {_simulation.LastDelaySeconds}s · {_simulation.LastDelayCause}", onSchedule ? onTime : delayed);
                y += 24f;
            }

            var staffing = _simulation.Staffing;
            GUI.Label(new Rect(42, y, 380, 20),
                $"Ground crew: {staffing.GroundCrew}  ·  payroll ${staffing.DailyWage:N0}/day{(staffing.IsUnderstaffed ? "  ·  UNDERSTAFFED" : string.Empty)}",
                staffing.IsUnderstaffed ? caution : small);
            y += 22f;

            if (earlySession)
            {
                GUI.Label(new Rect(42, y, 360, 22), "Crew / stand / research unlock after you accept a route", small);
                y += 24f;
            }
            else
            {
                GUI.enabled = !_simulation.IsInsolvent && staffing.GroundCrew < AirportStaffing.MaximumGroundCrew && _simulation.Economy.Cash >= AirportStaffing.HireCost;
                if (GUI.Button(new Rect(42, y, 150, 24), $"Hire crew · ${AirportStaffing.HireCost}", button))
                    _session.HireGroundCrew();
                GUI.enabled = !_simulation.IsInsolvent && staffing.GroundCrew > AirportStaffing.MinimumGroundCrew;
                if (GUI.Button(new Rect(198, y, 110, 24), "Release crew", button))
                    _session.ReleaseGroundCrew();
                GUI.enabled = true;
                y += 28f;

                var capacity = _simulation.Capacity;
                GUI.Label(new Rect(42, y, 380, 20),
                    $"Stands: {capacity.StandCount} / {AirportCapacity.MaximumStands}", small);
                y += 22f;
                GUI.enabled = !_simulation.IsInsolvent && capacity.CanExpand && _simulation.Economy.Cash >= AirportCapacity.ThirdStandCost;
                if (GUI.Button(new Rect(42, y, 220, 24),
                        capacity.HasThirdStand ? "Stand 3 built" : $"Build stand 3 · ${AirportCapacity.ThirdStandCost:N0}", button))
                    _session.BuildThirdStand();
                GUI.enabled = true;
                y += 28f;

                var research = _simulation.Research;
                var researchIcon = AirsideTheme.Icon("economy", "research");
                var researchLabelX = 42f;
                if (researchIcon != null)
                {
                    GUI.DrawTexture(new Rect(42, y, 18, 18), researchIcon, ScaleMode.ScaleToFit, alphaBlend: true);
                    researchLabelX = 64f;
                }
                if (research.IsResearching)
                {
                    var progress = (float)research.Progress01(_clock.Now);
                    var pct = (int)(progress * 100);
                    GUI.Label(new Rect(researchLabelX, y, 380 - (researchLabelX - 42), 20),
                        $"Research: {research.ActiveProjectName} {pct}% · {research.SecondsRemaining(_clock.Now)}s left", small);
                    y += 22f;
                    AirsideTheme.DrawProgressBar(
                        new Rect(42, y, 280, 8),
                        progress,
                        AirsideTheme.CoastalBlue,
                        new Color(AirsideTheme.Tarmac.r, AirsideTheme.Tarmac.g, AirsideTheme.Tarmac.b, 0.85f));
                    y += 16f;
                }
                else if (research.CanStartOperationsEfficiency)
                {
                    GUI.Label(new Rect(researchLabelX, y, 380 - (researchLabelX - 42), 20),
                        $"Research: {AirportResearch.OperationsEfficiencyName} · -${AirportResearch.OperationsEfficiencyDailyDiscount}/day when done", small);
                    y += 22f;
                    GUI.enabled = !_simulation.IsInsolvent && _simulation.Economy.Cash >= AirportResearch.OperationsEfficiencyCost;
                    if (GUI.Button(new Rect(42, y, 260, 24), $"Start research · ${AirportResearch.OperationsEfficiencyCost:N0}", button))
                        _session.StartOperationsResearch();
                    GUI.enabled = true;
                    y += 28f;
                }
                else if (research.CanStartPassengerServices)
                {
                    GUI.Label(new Rect(researchLabelX, y, 380 - (researchLabelX - 42), 20),
                        $"Research: {AirportResearch.PassengerServicesName} · +${AirportResearch.PassengerServicesRouteBonus}/flight when done", small);
                    y += 22f;
                    GUI.enabled = !_simulation.IsInsolvent && _simulation.Economy.Cash >= AirportResearch.PassengerServicesCost;
                    if (GUI.Button(new Rect(42, y, 280, 24), $"Start research · ${AirportResearch.PassengerServicesCost:N0}", button))
                        _session.StartPassengerServicesResearch();
                    GUI.enabled = true;
                    y += 28f;
                }
                else
                {
                    var ops = research.OperationsEfficiencyComplete
                        ? $"{AirportResearch.OperationsEfficiencyName} ✓"
                        : string.Empty;
                    var pax = research.PassengerServicesComplete
                        ? $"{AirportResearch.PassengerServicesName} ✓ (+${AirportResearch.PassengerServicesRouteBonus}/flt)"
                        : string.Empty;
                    GUI.Label(new Rect(researchLabelX, y, 380 - (researchLabelX - 42), 20),
                        $"Research: {ops}{(ops.Length > 0 && pax.Length > 0 ? " · " : string.Empty)}{pax}", small);
                    y += 22f;
                }
            }

            // Coach tip — Safety Yellow when the first decision is live.
            var coachUrgent = _simulation.Routes.Pending != null && _simulation.Routes.Accepted.Count == 0;
            var coachStyle = coachUrgent
                ? AirsideTheme.TextStyle(new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold }, AirsideTheme.SafetyYellow)
                : detail;
            if (coachUrgent)
            {
                var stripe = AirsideTheme.AlertStripeBackground;
                if (stripe != null)
                    GUI.DrawTexture(new Rect(36, y - 2, 382, 28), stripe, ScaleMode.StretchToFill, alphaBlend: true);
            }
            GUI.Label(new Rect(42, y, 380, 24), FirstSessionCoachLine(), coachStyle);
            y += 26f;
            GUI.Label(new Rect(42, y, 380, 22),
                earlySession
                    ? "Space pause · Tab speed · Enter accept offer · F follow · O overview"
                    : "Space pause · Tab speed · P priority · M mute · F follow/cycle · O overview",
                small);

            // First-session waiting meter under the coach tip.
            if (earlySession && _simulation.Routes.Pending == null && !_showOpeningBriefing)
            {
                y += 24f;
                var secondsToOffer = Math.Max(0, AirportRoutes.FirstOfferAfterSeconds - _clock.Now.ElapsedSeconds);
                var progress = 1f - Mathf.Clamp01(secondsToOffer / (float)AirportRoutes.FirstOfferAfterSeconds);
                GUI.Label(new Rect(42, y, 380, 18),
                    secondsToOffer > 0
                        ? $"Waiting for first airline offer… {secondsToOffer}s"
                        : "Airline offer arriving…",
                    small);
                y += 20f;
                AirsideTheme.DrawProgressBar(
                    new Rect(42, y, 360, 10),
                    progress,
                    AirsideTheme.CoastalBlue,
                    new Color(AirsideTheme.Tarmac.r, AirsideTheme.Tarmac.g, AirsideTheme.Tarmac.b, 0.9f));
            }
            } // end !_canvasHudActive left panel

            var historyLeft = Screen.width / scale - 362;
            var accepted = _simulation.Routes.Accepted;
            var pendingOffer = _simulation.Routes.Pending;
            var firstDecisionOffer = pendingOffer != null && accepted.Count == 0;
            // Canvas HUD owns the offer panel when active; IMGUI ops sits at the top-right.
            var offerHeight = (_canvasHudActive || pendingOffer == null) ? 0f : (firstDecisionOffer ? 196f : 156f);
            var opsTop = 22f + (offerHeight > 0f ? offerHeight + 12f : 0f);
            // Pin the actionable offer above operations so status detail never buries it.
            if (pendingOffer != null && !_canvasHudActive)
                DrawRouteOffer(scale, panel, detail, small, caution, button, offerTop: 22f);

            if (!_canvasHudActive)
            {
            var listedRoutes = accepted.Count == 0
                ? 1
                : Math.Min(4, accepted.Count) + (accepted.Count > 4 ? 1 : 0);
            // Header through routes summary (~80), schedule lines, fleet (2), event tail (4).
            var opsHeight = 80f + listedRoutes * 18f + 4f + 2 * 18f + 6f + 4 * 20f + 16f;
            var opsRect = new Rect(historyLeft, opsTop, 340, opsHeight);
            GUI.Box(opsRect, string.Empty, panel);
            AirsideTheme.DrawPanelFrame(opsRect);
            GUI.Label(new Rect(historyLeft + 20, opsTop + 14, 300, 26), "OPERATIONS", detail);
            GUI.Label(new Rect(historyLeft + 20, opsTop + 40, 320, 20),
                $"Routes {_simulation.Routes.Accepted.Count}  ·  {_simulation.Routes.ScheduledFlightsPerDay}/{_simulation.MaxScheduledFlightsPerDay} scheduled flights/day  ·  ${_simulation.Routes.IncomePerFlight + _simulation.Research.RouteIncomeBonus:N0}/flight", small);

            var trafficY = opsTop + 58f;
            if (accepted.Count == 0)
            {
                if (pendingOffer != null)
                    GUI.Label(new Rect(historyLeft + 20, trafficY, 310, 20), "Offer waiting above — Accept to start income", caution);
                else
                {
                    var secondsToOffer = Math.Max(0, AirportRoutes.FirstOfferAfterSeconds - _clock.Now.ElapsedSeconds);
                    GUI.Label(new Rect(historyLeft + 20, trafficY, 310, 20),
                        secondsToOffer > 0
                            ? $"No routes yet — first offer in {secondsToOffer}s"
                            : "No routes yet — offer arriving…",
                        small);
                }

                trafficY += 18f;
            }
            else
            {
                var start = Math.Max(0, accepted.Count - 4);
                for (var i = start; i < accepted.Count; i++)
                {
                    var route = accepted[i];
                    GUI.Label(new Rect(historyLeft + 20, trafficY, 310, 20),
                        $"{route.Airline} · {route.FlightsPerDay}/d → {route.Destination} · ${route.IncomePerFlight:N0}", small);
                    trafficY += 18f;
                }

                if (start > 0)
                {
                    GUI.Label(new Rect(historyLeft + 20, trafficY, 310, 20), $"+{start} earlier route(s)", small);
                    trafficY += 18f;
                }
            }

            trafficY += 4f;
            foreach (var aircraft in _simulation.GroundTraffic)
            {
                GUI.Label(new Rect(historyLeft + 20, trafficY, 310, 20), $"{aircraft.Id.Value}: {GroundTrafficSummary(aircraft)}", small);
                trafficY += 18f;
            }

            var historyY = trafficY + 6f;
            foreach (var entry in _simulation.EventLog.Events.Reverse().Take(4))
            {
                GUI.Label(new Rect(historyLeft + 20, historyY, 300, 19), $"T+{entry.OccurredAt.ElapsedSeconds}s  {entry.FlightId}  ·  {entry.Title}", small);
                historyY += 20f;
            }

            var latest = _simulation.DailyReports.Latest;
            if (latest != null)
            {
                var reportTop = historyY + 10f;
                GUI.Box(new Rect(historyLeft, reportTop, 340, 118), string.Empty, panel);
                GUI.Label(new Rect(historyLeft + 20, reportTop + 12, 300, 24), "DAILY REPORT", detail);
                GUI.Label(new Rect(historyLeft + 20, reportTop + 40, 310, 20), latest.SummaryLine, small);
                GUI.Label(new Rect(historyLeft + 20, reportTop + 60, 310, 20),
                    $"Income ${latest.FlightIncome:N0}  ·  delays -${latest.DelayCost:N0}  ·  running -${latest.OperatingCost:N0}", small);
                var rep = latest.ReputationChange == 0 ? "reputation flat"
                    : latest.ReputationChange > 0 ? $"reputation +{latest.ReputationChange}"
                    : $"reputation {latest.ReputationChange}";
                GUI.Label(new Rect(historyLeft + 20, reportTop + 80, 310, 20),
                    $"{rep}  ·  {latest.GroundCrew} crew", small);
            }
            } // end !_canvasHudActive ops panel

            if (!_canvasHudActive)
            {
                DrawResearchToast(scale, panel, onTime);
                DrawSaveIndicator(scale, panel, small, onTime);
                DrawOpsToast(scale, panel, detail, onTime);
            }
            if (!_canvasHudActive)
            {
                if (_paused && !_showAwaySummary && !_showOpeningBriefing)
                    DrawPauseOverlay(scale, panel, title, caution, small, button);
                if (_showOpeningBriefing)
                    DrawOpeningBriefing(scale, panel, title, detail, small, button);
                if (_showAwaySummary)
                    DrawAwaySummary(scale, panel, title, detail, small, button);
                if (_simulation.IsInsolvent)
                    DrawInsolvencyOverlay(scale, panel, title, detail, small, delayed, button);
            }
            GUI.matrix = previousMatrix;
        }

        private void DrawSaveIndicator(float scale, GUIStyle panel, GUIStyle small, GUIStyle onTime)
        {
            if (Time.unscaledTime > _saveIndicatorUntil)
                return;

            var width = 110f;
            var height = 36f;
            var left = Screen.width / scale - width - 24f;
            var top = Screen.height / scale - height - 24f;
            GUI.Box(new Rect(left, top, width, height), string.Empty, panel);
            GUI.Label(new Rect(left + 16f, top + 8f, width - 24f, 22f), "Saved", onTime);
        }

        private void DrawInsolvencyOverlay(float scale, GUIStyle panel, GUIStyle title, GUIStyle detail, GUIStyle small, GUIStyle delayed, GUIStyle button)
        {
            var width = 460f;
            var height = 290f;
            var left = (Screen.width / scale - width) * 0.5f;
            var top = (Screen.height / scale - height) * 0.5f;
            GUI.Box(new Rect(left, top, width, height), string.Empty, panel);
            GUI.Label(new Rect(left + 24, top + 22, width - 48, 34), "AIRSIDE", title);
            GUI.Label(new Rect(left + 24, top + 58, width - 48, 28), "Airport declared insolvent", delayed);
            GUI.Label(new Rect(left + 24, top + 96, width - 48, 44),
                $"Cash stayed negative across {AirportEconomy.InsolvencyConsecutiveDays} consecutive day closes. Operations have stopped; commands are refused.", detail);
            GUI.Label(new Rect(left + 24, top + 150, width - 48, 22),
                $"Final cash: ${_simulation.Economy.Cash:N0}  ·  Reputation {_simulation.Reputation.Score}", detail);
            GUI.Label(new Rect(left + 24, top + 180, width - 48, 22),
                $"{_simulation.Location.Name} · {_simulation.Location.Region}", small);
            if (GUI.Button(new Rect(left + 100, top + 220, 260, 36), "Start a new airport", button))
                ResetToNewAirport();
        }

        private void DrawRouteOffer(float scale, GUIStyle panel, GUIStyle detail, GUIStyle small, GUIStyle caution, GUIStyle button, float offerTop = 244f)
        {
            var proposal = _simulation.Routes.Pending;
            if (proposal == null)
                return;

            var left = Screen.width / scale - 362;
            var top = offerTop;
            var firstDecision = _simulation.Routes.Accepted.Count == 0;
            var height = firstDecision ? 196f : 156f;
            var offerRect = new Rect(left, top, 340, height);
            GUI.Box(offerRect, string.Empty, panel);
            AirsideTheme.DrawPanelFrame(offerRect, firstDecision
                ? new Color(AirsideTheme.SafetyYellow.r, AirsideTheme.SafetyYellow.g, AirsideTheme.SafetyYellow.b, 0.7f)
                : null);
            if (firstDecision)
            {
                var stripe = AirsideTheme.AlertStripeBackground;
                if (stripe != null)
                    GUI.DrawTexture(new Rect(left, top, 340, 6f), stripe, ScaleMode.StretchToFill, alphaBlend: true);
                // Soft pulse so the first decision panel reads as live.
                var pulse = 0.35f + 0.25f * (0.5f + 0.5f * Mathf.Sin(
                    Time.unscaledTime * AirsideReusableMotion.UiPulseHz * Mathf.PI * 2f));
                var prev = GUI.color;
                GUI.color = new Color(AirsideTheme.SafetyYellow.r, AirsideTheme.SafetyYellow.g, AirsideTheme.SafetyYellow.b, pulse);
                GUI.DrawTexture(new Rect(left, top, 4f, height), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(left + 336f, top, 4f, height), Texture2D.whiteTexture);
                GUI.color = prev;
            }

            var routeIcon = AirsideTheme.Icon("economy", "route");
            var titleX = left + 20f;
            if (routeIcon != null)
            {
                GUI.DrawTexture(new Rect(left + 20, top + 14, 20, 20), routeIcon, ScaleMode.ScaleToFit, alphaBlend: true);
                titleX = left + 46f;
            }

            var offerTitle = firstDecision ? "FIRST DECISION — route offer" : "ROUTE OFFER — decide now";
            GUI.Label(new Rect(titleX, top + 14, 300, 24), offerTitle, firstDecision ? caution : detail);
            if (firstDecision)
            {
                GUI.Label(new Rect(left + 20, top + 40, 310, 18),
                    "Accept to earn cash on every completed flight.", small);
            }

            var bodyTop = firstDecision ? top + 60f : top + 42f;
            GUI.Label(new Rect(left + 20, bodyTop, 310, 20), $"{proposal.Airline}", small);
            GUI.Label(new Rect(left + 20, bodyTop + 20, 310, 20),
                $"{proposal.FlightsPerDay}/day to {proposal.Destination}", small);
            var payout = proposal.IncomePerFlight + _simulation.Reputation.IncomeBonus;
            GUI.Label(new Rect(left + 20, bodyTop + 40, 310, 20),
                $"+${payout:N0} per completed flight", small);
            var meetsReputation = _simulation.Reputation.Score >= proposal.ReputationRequired;
            var fitsCapacity = _simulation.Routes.FitsScheduleCapacity(_simulation.Capacity.StandCount);
            string status;
            var blocked = !meetsReputation || !fitsCapacity;
            if (!meetsReputation)
                status = $"Needs reputation {proposal.ReputationRequired} (have {_simulation.Reputation.Score})";
            else if (!fitsCapacity)
                status = $"Schedule full ({_simulation.Routes.ScheduledFlightsPerDay}/{_simulation.MaxScheduledFlightsPerDay} flights/day)";
            else
                status = $"Expires in {proposal.SecondsRemaining(_clock.Now)}s";
            GUI.Label(new Rect(left + 20, bodyTop + 60, 310, 20), status, blocked ? caution : small);

            var buttonTop = bodyTop + 82f;
            GUI.enabled = !_simulation.IsInsolvent && meetsReputation && fitsCapacity;
            var acceptLabel = firstDecision ? "Accept route  (Enter)" : "Accept route";
            var acceptWidth = firstDecision ? 190f : 150f;
            if (GUI.Button(new Rect(left + 20, buttonTop, acceptWidth, firstDecision ? 32f : 24f), acceptLabel, button))
                _session.AcceptRoute();
            GUI.enabled = true;
            var declineTop = firstDecision ? buttonTop : buttonTop;
            var declineLeft = firstDecision ? left + 220f : left + 178f;
            var declineW = firstDecision ? 100f : 130f;
            var declineH = firstDecision ? 32f : 24f;
            if (GUI.Button(new Rect(declineLeft, declineTop, declineW, declineH), "Decline", button))
                _session.DeclineRoute();
        }


        private void MaybeShowResearchToast()
        {
            var completedId = _simulation.Research.LastCompletedProjectId;
            if (string.IsNullOrEmpty(completedId))
                return;

            if (completedId == AirportResearch.PassengerServicesId)
            {
                _researchToast =
                    $"Research complete — {AirportResearch.PassengerServicesName} (+${AirportResearch.PassengerServicesRouteBonus}/flight)";
            }
            else
            {
                _researchToast =
                    $"Research complete — {AirportResearch.OperationsEfficiencyName} (-${AirportResearch.OperationsEfficiencyDailyDiscount}/day)";
            }

            _researchToastUntil = Time.unscaledTime + 8f;
        }

        private void DrawResearchToast(float scale, GUIStyle panel, GUIStyle onTime)
        {
            if (string.IsNullOrEmpty(_researchToast) || Time.unscaledTime > _researchToastUntil)
                return;

            var width = 520f;
            var height = 64f;
            var left = (Screen.width / scale - width) * 0.5f;
            GUI.Box(new Rect(left, 18f, width, height), string.Empty, panel);
            GUI.Label(new Rect(left + 20f, 34f, width - 40f, 28f), _researchToast, onTime);
        }

        private void MaybeShowOpsToast()
        {
            var events = _simulation.EventLog.Events;
            if (events.Count <= _seenEventCount)
                return;

            var latest = events[events.Count - 1];
            _seenEventCount = events.Count;
            var flight = string.IsNullOrEmpty(latest.FlightId) ? string.Empty : $"{latest.FlightId} · ";
            _opsToast = $"{flight}{latest.Title}";
            _opsToastUntil = Time.unscaledTime + 4.5f;
        }

        private void DrawOpsToast(float scale, GUIStyle panel, GUIStyle detail, GUIStyle onTime)
        {
            if (string.IsNullOrEmpty(_opsToast) || Time.unscaledTime > _opsToastUntil)
                return;

            var width = 440f;
            var height = 52f;
            var left = (Screen.width / scale - width) * 0.5f;
            var top = Screen.height / scale - height - 28f;
            GUI.Box(new Rect(left, top, width, height), string.Empty, panel);
            GUI.Label(new Rect(left + 18f, top + 14f, width - 36f, 26f), _opsToast, onTime);
        }


        private void DrawPauseOverlay(float scale, GUIStyle panel, GUIStyle title, GUIStyle caution, GUIStyle small, GUIStyle button)
        {
            // Presentation-only dimmer while simulation time is paused.
            var width = Screen.width / scale;
            var height = Screen.height / scale;
            var prev = GUI.color;
            GUI.color = new Color(0.05f, 0.07f, 0.09f, 0.35f);
            GUI.DrawTexture(new Rect(0f, 0f, width, height), Texture2D.whiteTexture);
            GUI.color = prev;

            var boxW = 320f;
            var boxH = 140f;
            var left = (width - boxW) * 0.5f;
            var top = (height - boxH) * 0.5f;
            GUI.Box(new Rect(left, top, boxW, boxH), string.Empty, panel);
            GUI.Label(new Rect(left + 24f, top + 18f, boxW - 48f, 36f), "PAUSED", title);
            GUI.Label(new Rect(left + 24f, top + 52f, boxW - 48f, 22f), "Space to resume", caution);
            if (GUI.Button(new Rect(left + 50f, top + 88f, 220f, 30f), "Start new airport", button))
                ResetToNewAirport();
        }

        private void DrawOpeningBriefing(float scale, GUIStyle panel, GUIStyle title, GUIStyle detail, GUIStyle small, GUIStyle button)
        {
            var screenW = Screen.width / scale;
            var screenH = Screen.height / scale;
            var splash = AirsideTheme.SplashDawn;
            if (splash != null)
            {
                // Full-bleed dawn splash; briefing card sits in the quiet left/centre area.
                GUI.DrawTexture(new Rect(0f, 0f, screenW, screenH), splash, ScaleMode.ScaleAndCrop, alphaBlend: false);
                var prev = GUI.color;
                GUI.color = new Color(0.05f, 0.07f, 0.09f, 0.28f);
                GUI.DrawTexture(new Rect(0f, 0f, screenW, screenH), Texture2D.whiteTexture);
                GUI.color = prev;
            }

            var width = 500f;
            var height = 360f;
            var left = (screenW - width) * 0.5f;
            var top = (screenH - height) * 0.5f;
            GUI.Box(new Rect(left, top, width, height), string.Empty, panel);
            var wordmark = AirsideTheme.WordmarkLight;
            if (wordmark != null)
                GUI.DrawTexture(new Rect(left + 24, top + 14, 280, 70), wordmark, ScaleMode.ScaleToFit, alphaBlend: true);
            else
                GUI.Label(new Rect(left + 24, top + 18, width - 48, 34), "AIRSIDE", title);
            GUI.Label(new Rect(left + 24, top + 88, width - 48, 24), "You run this regional airport", detail);
            GUI.Label(new Rect(left + 24, top + 118, width - 48, 44),
                $"Aircraft move on their own. Your job is cash, reputation and capacity at {_simulation.Location.Name}.", detail);
            GUI.Label(new Rect(left + 24, top + 170, width - 48, 22), "First useful decision", detail);
            GUI.Label(new Rect(left + 24, top + 196, width - 48, 44),
                $"In about {AirportRoutes.FirstOfferAfterSeconds} seconds an airline will offer a scheduled route. Accept it to earn money on every completed flight.", small);
            GUI.Label(new Rect(left + 24, top + 248, width - 48, 40),
                "Watch the right-hand OPERATIONS panel. Watch cash and delays on the left. Press Enter to Accept the first offer.", small);
            GUI.Label(new Rect(left + 24, top + 292, width - 48, 20),
                "Space / Enter to begin  ·  Tab = 4× speed", small);
            if (GUI.Button(new Rect(left + 140, top + 318, 220, 30), "Begin operations", button))
                DismissOpeningBriefing();
        }

        private void DismissOpeningBriefing()
        {
            _showOpeningBriefing = false;
            _paused = false;
            if (_commercialAircraft != null && _commercialAircraft.Length > 0)
            {
                _cameraController.SetFollowTargets(_commercialAircraft);
                _cameraController.StartFollowFirst();
            }
        }

        private string FirstSessionCoachLine()
        {
            if (_simulation.IsInsolvent)
                return "Airport insolvent — operations frozen.";
            if (_showOpeningBriefing)
                return "Read the briefing, then begin — first route offer arrives soon.";
            if (_simulation.Routes.Pending != null && _simulation.Routes.Accepted.Count == 0)
                return "Tip: First useful decision — Accept the route offer (Enter).";
            if (_simulation.Routes.Pending != null)
                return "Tip: Accept a route offer (Enter) for recurring flight income.";
            if (_simulation.Routes.Accepted.Count == 0)
            {
                var secondsToOffer = Math.Max(0, AirportRoutes.FirstOfferAfterSeconds - _clock.Now.ElapsedSeconds);
                if (secondsToOffer > 0)
                    return $"Tip: First route offer in {secondsToOffer}s — watch the top-right panel.";
                return "Tip: Airlines will offer routes soon — watch the right panel.";
            }
            if (_simulation.Flights.Count == 0)
                return "Tip: Accepted routes spawn commercial flights automatically.";
            if (_simulation.ActiveAircraft.Phase == AircraftPhase.AtStand)
                return "Tip: Hire crew or priority crew to speed turnarounds.";
            return "Tip: Watch delays — reputation and cash follow on-time ops.";
        }

        private void MaybeShowFirstSessionDecisionToasts()
        {
            if (_showOpeningBriefing || _showAwaySummary)
                return;

            if (!_routeOfferToastShown && _simulation.Routes.Pending != null && _simulation.Routes.Accepted.Count == 0)
            {
                _opsToast = "Route offer ready — Accept on the right for recurring income";
                _opsToastUntil = Time.unscaledTime + 6f;
                _routeOfferToastShown = true;
            }

            var accepted = _simulation.Routes.Accepted.Count;
            if (accepted > _acceptedRouteCountSeen)
            {
                var latest = _simulation.Routes.Accepted[accepted - 1];
                var bonus = _simulation.Research.RouteIncomeBonus;
                _opsToast =
                    $"Route accepted — +${latest.IncomePerFlight + bonus:N0} per completed flight";
                _opsToastUntil = Time.unscaledTime + 6f;
                _acceptedRouteCountSeen = accepted;
            }
            else if (accepted < _acceptedRouteCountSeen)
            {
                _acceptedRouteCountSeen = accepted;
            }

            var routeIncome = _simulation.Economy.TotalRouteIncome;
            if (!_firstRouteIncomeToastShown && routeIncome > _routeIncomeSeen)
            {
                var gained = routeIncome - _routeIncomeSeen;
                _opsToast = $"First route payout +${gained:N0} — completed flights now pay you";
                _opsToastUntil = Time.unscaledTime + 7f;
                _firstRouteIncomeToastShown = true;
            }

            _routeIncomeSeen = routeIncome;
        }

        private void ResetToNewAirport()
        {
            try
            {
                var path = _SavePath;
                if (File.Exists(path))
                    File.Delete(path);
                if (File.Exists(path + ".previous"))
                    File.Delete(path + ".previous");
                if (File.Exists(path + ".temporary"))
                    File.Delete(path + ".temporary");
            }
            catch (IOException)
            {
                // Best-effort wipe; reload still attempts a clean create.
            }

            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }

        private void DrawAwaySummary(float scale, GUIStyle panel, GUIStyle title, GUIStyle detail, GUIStyle small, GUIStyle button)
        {
            var summary = _session.LastAwaySummary;
            var width = 460f;
            var height = 348f;
            var left = (Screen.width / scale - width) * 0.5f;
            var top = (Screen.height / scale - height) * 0.5f;
            GUI.Box(new Rect(left, top, width, height), string.Empty, panel);
            GUI.Label(new Rect(left + 24, top + 18, width - 48, 34), "AIRSIDE", title);
            GUI.Label(new Rect(left + 24, top + 50, width - 48, 22), "Welcome back to operations", detail);
            GUI.Label(new Rect(left + 24, top + 82, width - 48, 22),
                $"Airport operated for {FormatDuration(summary.AwaySeconds)}", detail);
            GUI.Label(new Rect(left + 24, top + 112, width - 48, 22),
                $"Flights completed: {summary.FlightsCompleted}", detail);

            var cashStyle = summary.CashChange >= 0
                ? AirsideTheme.TextStyle(new GUIStyle(detail), AirsideTheme.ClearGreen)
                : AirsideTheme.TextStyle(new GUIStyle(detail), AirsideTheme.SignalRed);
            GUI.Label(new Rect(left + 24, top + 140, width - 48, 22),
                $"Cash change: {summary.CashChange:+$#,0;-$#,0;$0}", cashStyle);
            GUI.Label(new Rect(left + 24, top + 168, width - 48, 22),
                $"Route income: ${summary.RouteIncome:N0}", detail);
            GUI.Label(new Rect(left + 24, top + 196, width - 48, 22),
                $"Delay + running costs: ${summary.DelayCost + summary.OperatingCost:N0}", detail);
            GUI.Label(new Rect(left + 24, top + 224, width - 48, 22),
                $"Reputation: {summary.ReputationChange:+0;-0;0}  (now {_simulation.Reputation.Score})", detail);
            if (summary.RecoveredPreviousSave)
                GUI.Label(new Rect(left + 24, top + 252, width - 48, 20), "Recovered the previous safe copy.", small);
            else if (summary.ClockMovedBackwards)
                GUI.Label(new Rect(left + 24, top + 252, width - 48, 20), "Device clock moved backwards; no time was added.", small);
            else
                GUI.Label(new Rect(left + 24, top + 252, width - 48, 20),
                    $"{_simulation.Location.Name} · {_simulation.Location.Region}", small);
            if (GUI.Button(new Rect(left + 130, top + 292, 200, 32), "Continue operations", button))
                _showAwaySummary = false;
        }

        private static string FormatDuration(long seconds)
        {
            var span = TimeSpan.FromSeconds(seconds);
            if (span.TotalDays >= 1)
                return $"{(int)span.TotalDays}d {span.Hours}h";
            if (span.TotalHours >= 1)
                return $"{(int)span.TotalHours}h {span.Minutes}m";
            if (span.TotalMinutes >= 1)
                return $"{(int)span.TotalMinutes}m {span.Seconds}s";
            return $"{span.Seconds}s";
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
                SaveSession();
        }

        private void OnApplicationQuit()
        {
            SaveSession();
        }

        private void SaveSession()
        {
            _session?.Save(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            _saveIndicatorUntil = Time.unscaledTime + 1.6f;
        }

        private static string GroundTrafficSummary(GroundTrafficAircraft traffic)
        {
            if (traffic.IsHolding)
            {
                var waitingFor = string.IsNullOrEmpty(traffic.DesiredSegment.Value) ? "clearance" : traffic.DesiredSegment.Value;
                return $"{traffic.CurrentPhase} — holding for {waitingFor}";
            }

            return traffic.CurrentPhase;
        }

        private void BuildLightingAndCamera()
        {
            var camera = Camera.main;
            if (camera == null)
                camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            // Match overview framing (architectural miniature, decision 0022 / post-F polish).
            camera.fieldOfView = 48f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            _mainCamera = camera;

            _cameraController = camera.GetComponent<AirsideCameraController>();
            if (_cameraController == null)
                _cameraController = camera.gameObject.AddComponent<AirsideCameraController>();

            if (camera.GetComponent<AudioListener>() == null)
                camera.gameObject.AddComponent<AudioListener>();

            _sun = FindFirstObjectByType<Light>();
            if (_sun == null)
                _sun = new GameObject("Sun").AddComponent<Light>();
            _sun.type = LightType.Directional;
            _sun.shadows = LightShadows.Soft;
            _sun.shadowStrength = 0.78f;
            _sun.shadowBias = 0.035f;
            _sun.shadowNormalBias = 0.4f;

            // Cool fill opposite the key — softens night and dawn without a full probe bake.
            var fillGo = GameObject.Find("Fill light");
            _fillLight = fillGo != null ? fillGo.GetComponent<Light>() : null;
            if (_fillLight == null)
                _fillLight = new GameObject("Fill light").AddComponent<Light>();
            _fillLight.type = LightType.Directional;
            _fillLight.shadows = LightShadows.None;
            _fillLight.intensity = 0.25f;
            _fillLight.color = new Color(0.45f, 0.55f, 0.75f);

            ApplyDayCycle();
        }

        private void ApplyDayCycle()
        {
            var cycle = _simulation.TimeOfDay;
            var daylight = (float)cycle.Daylight;

            var elevation = (float)cycle.SunElevationDegrees;
            _sun.transform.rotation = Quaternion.Euler(Mathf.Max(-6f, elevation), -28f - (float)cycle.Fraction * 90f, 0f);

            // Warm key light, cooler fill — closer to REF dawn/day without a full URP stack.
            var day = new Color(1f, 0.94f, 0.82f);
            var goldenHour = new Color(1f, 0.62f, 0.38f);
            var night = new Color(0.28f, 0.36f, 0.58f);
            var warm = Mathf.Clamp01(Mathf.Min(daylight, 1f - daylight) * 3.2f); // strong near dawn/dusk
            _sun.color = Color.Lerp(Color.Lerp(night, day, daylight), goldenHour, warm * Mathf.Max(daylight, 0.15f));
            // Noon punch + readable night key so REF overview separation holds (post-F polish).
            _sun.intensity = Mathf.Lerp(0.32f, 1.98f, daylight);
            _sun.shadowStrength = Mathf.Lerp(0.38f, 0.82f, daylight);

            // Weather gloom cools the post stack (rain/fog/storm) without fighting day fog.
            var weather = _simulation.CurrentWeather;
            var weatherGloom = weather == WeatherKind.Storm ? 0.55f
                : weather == WeatherKind.Fog ? 0.42f
                : weather == WeatherKind.Rain ? 0.28f
                : weather == WeatherKind.Cloudy ? 0.16f
                : weather == WeatherKind.Overcast ? 0.22f
                : 0f;
            if (weatherGloom > 0f)
                _sun.intensity *= Mathf.Lerp(1f, 0.72f, weatherGloom);
            _dayVolume?.Apply(daylight, warm, weatherGloom);

            if (_fillLight != null)
            {
                _fillLight.transform.rotation = Quaternion.Euler(25f, 140f - (float)cycle.Fraction * 40f, 0f);
                _fillLight.color = Color.Lerp(
                    new Color(0.25f, 0.32f, 0.55f),
                    Color.Lerp(new Color(0.55f, 0.65f, 0.85f), new Color(1f, 0.78f, 0.62f), warm * 0.55f),
                    daylight);
                _fillLight.intensity = Mathf.Lerp(0.55f, 0.18f, daylight) + warm * 0.06f;
            }

            var ambientDay = new Color(0.52f, 0.58f, 0.64f);
            var ambientDusk = new Color(0.58f, 0.38f, 0.32f);
            var ambientNight = new Color(0.14f, 0.16f, 0.26f);
            var ambientSky = Color.Lerp(Color.Lerp(ambientNight, ambientDay, daylight), ambientDusk, warm * 0.85f);
            var ambientEquator = Color.Lerp(
                new Color(0.2f, 0.22f, 0.32f),
                Color.Lerp(new Color(0.42f, 0.46f, 0.48f), new Color(0.53f, 0.40f, 0.34f), warm),
                daylight);
            var ambientGround = Color.Lerp(
                new Color(0.1f, 0.11f, 0.14f),
                Color.Lerp(new Color(0.22f, 0.24f, 0.2f), new Color(0.28f, 0.18f, 0.14f), warm),
                daylight);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ambientSky;
            RenderSettings.ambientEquatorColor = ambientEquator;
            RenderSettings.ambientGroundColor = ambientGround;
            RenderSettings.ambientIntensity = Mathf.Lerp(1.05f, 1.05f, daylight) + warm * 0.08f;
            RenderSettings.subtractiveShadowColor = Color.Lerp(
                new Color(0.22f, 0.28f, 0.4f),
                new Color(0.4f, 0.28f, 0.28f),
                warm);

            var skyDay = new Color(0.50f, 0.55f, 0.56f); // REF-001 muted day sky (not OpenSky cyan)
            var skyDusk = new Color(0.72f, 0.42f, 0.32f);
            var skyNight = new Color(0.05f, 0.07f, 0.12f);
            var sky = Color.Lerp(Color.Lerp(skyNight, skyDay, daylight), skyDusk, warm * 0.78f);
            if (_mainCamera != null)
                _mainCamera.backgroundColor = sky;
            if (_horizonDome != null)
            {
                var domeRenderer = _horizonDome.GetComponent<Renderer>();
                if (domeRenderer != null)
                {
                    SetRendererColor(domeRenderer, sky);
                    if (domeRenderer.material.HasProperty("_EmissionColor"))
                        domeRenderer.material.SetColor("_EmissionColor", sky);
                }
            }

            UpdateSunAndMoonDiscs(daylight, warm, elevation);

            // Soft exponential fog for depth on clear days; weather can thicken it later.
            if (!Weather.IsAdverse(_simulation.CurrentWeather))
            {
                var cloudy = _simulation.CurrentWeather == WeatherKind.Cloudy;
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                var clearFog = Color.Lerp(
                    new Color(0.08f, 0.1f, 0.16f),
                    Color.Lerp(skyDay * 0.92f, skyDusk * 0.85f, warm),
                    Mathf.Clamp01(daylight + warm * 0.25f));
                if (cloudy)
                    clearFog = Color.Lerp(clearFog, new Color(0.55f, 0.6f, 0.66f), 0.28f);
                RenderSettings.fogColor = clearFog;
                var density = Mathf.Lerp(0.0058f, 0.0028f, daylight);
                if (cloudy)
                    density = Mathf.Max(density, Mathf.Lerp(0.0072f, 0.0042f, daylight));
                // Soft dusk thicken so warm horizon haze reads without washing ALS.
                density += warm * 0.0009f;
                RenderSettings.fogDensity = density;
            }

            // Apron floods come up as daylight falls (presentation only).
            if (_apronLights != null)
            {
                // Stronger night punch so REF-002 warm pools read against scrub.
                var flood = Mathf.Lerp(4.2f, 0.05f, daylight);
                for (var i = 0; i < _apronLights.Length; i++)
                {
                    var light = _apronLights[i];
                    if (light == null)
                        continue;
                    // Tiny phase offset flicker so floods don't feel static at night.
                    var flicker = daylight < 0.4f
                        ? 1f + 0.04f * Mathf.Sin(
                            Time.unscaledTime * AirsideReusableMotion.FloodFlickerHz * Mathf.PI * 2f + i * 1.7f)
                        : 1f;
                    // Corner masts (0–3) get a bit more punch than fill floods.
                    var boost = i < 4 ? 1.15f : 1f;
                    light.intensity = flood * flicker * boost;
                    light.enabled = flood > 0.06f;
                }
            }

            // Landside streetlights along the access road / car park.
            if (_landsideLights != null)
            {
                var street = Mathf.Lerp(1.15f, 0.02f, daylight);
                for (var i = 0; i < _landsideLights.Length; i++)
                {
                    var light = _landsideLights[i];
                    if (light == null)
                        continue;
                    var flicker = daylight < 0.4f
                        ? 1f + 0.035f * Mathf.Sin(
                            Time.unscaledTime * AirsideReusableMotion.FloodFlickerHz * Mathf.PI * 2f + i * 2.1f + 0.8f)
                        : 1f;
                    light.intensity = street * flicker;
                }
            }

            // Threshold / approach point lights punch up at dusk for runway ends.
            if (_thresholdLights != null)
            {
                var approach = Mathf.Lerp(1.85f, 0.04f, daylight);
                for (var i = 0; i < _thresholdLights.Length; i++)
                {
                    var light = _thresholdLights[i];
                    if (light == null)
                        continue;
                    light.intensity = approach;
                }
            }

            // ALS centreline / bar lamps — steady dusk base, sequential chase at night.
            if (_alsLights != null)
            {
                var alsBase = Mathf.Lerp(2.1f, 0.03f, daylight);
                var nightChase = daylight < 0.42f;
                var chase = Time.unscaledTime * AirsideReusableMotion.AlsChaseHz;
                for (var i = 0; i < _alsLights.Length; i++)
                {
                    var light = _alsLights[i];
                    if (light == null)
                        continue;

                    // Far ALS REIL spots — sharp night flash, not centreline chase.
                    if (light.name.StartsWith("REIL", StringComparison.Ordinal))
                    {
                        var flash = daylight < 0.42f
                            && Mathf.Repeat(
                                Time.unscaledTime * AirsideReusableMotion.ReilFlashHz
                                + (light.name.EndsWith("R") ? 0.5f : 0f), 1f) < 0.18f;
                        light.intensity = flash ? 4.2f : alsBase * 0.25f;
                        light.enabled = daylight < 0.55f;
                        continue;
                    }

                    if (!nightChase)
                    {
                        light.intensity = alsBase;
                        light.enabled = alsBase > 0.05f;
                        continue;
                    }

                    // Chase from far approach (high index) toward the threshold (index 0).
                    var step = (Mathf.Min(_alsLights.Length, 8) - 1 - i) * 0.42f;
                    var wave = Mathf.Repeat(chase - step, 2.4f);
                    var pulse = wave < 0.4f
                        ? Mathf.SmoothStep(0f, 1f, 1f - Mathf.Abs(wave / 0.2f - 1f))
                        : 0f;
                    light.intensity = alsBase * (0.4f + 1.8f * pulse);
                    light.enabled = true;
                }
            }

            UpdateArffLightbar(daylight);

            // Sparse runway-edge point lights so the strip reads as a lit ribbon at night.
            if (_runwayEdgeLights != null)
            {
                var edge = Mathf.Lerp(0.95f, 0.02f, daylight);
                var reilPulse = daylight < 0.42f
                    ? (Mathf.Repeat(Time.unscaledTime * 1.8f, 1f) < 0.22f ? 2.6f : 0.15f)
                    : 0f;
                for (var i = 0; i < _runwayEdgeLights.Length; i++)
                {
                    var light = _runwayEdgeLights[i];
                    if (light == null)
                        continue;
                    if (light.name.StartsWith("REIL", StringComparison.Ordinal))
                    {
                        light.intensity = edge * 0.35f + reilPulse;
                        light.enabled = daylight < 0.55f;
                        continue;
                    }

                    light.intensity = edge;
                }
            }

            if (_apronProbe != null)
            {
                // Brighter probe intensity at night so wet Lit surfaces pick up floods.
                _apronProbe.intensity = Mathf.Lerp(1.15f, 0.85f, daylight);
                if (daylight < 0.45f && Time.frameCount % 45 == 0)
                    _apronProbe.RenderProbe();
            }

            if (_terminalProbe != null)
            {
                _terminalProbe.intensity = Mathf.Lerp(1.05f, 0.8f, daylight);
                if (daylight < 0.45f && Time.frameCount % 60 == 0)
                    _terminalProbe.RenderProbe();
            }

            UpdateAirfieldNavLights(daylight);
            UpdateNightGlow(daylight);
            UpdateAerodromeBeacon(daylight);
        }

        private void UpdateAirfieldNavLights(float daylight)
        {
            // Edge / taxi lights punch up at dusk/night so the airfield stays readable.
            var night = 1f - daylight;
            var intensity = Mathf.Lerp(0.35f, 1.35f, night);
            var warmWhite = Color.Lerp(new Color(0.85f, 0.88f, 0.7f), new Color(1f, 0.95f, 0.75f), night);
            for (var i = 0; i < _airfieldLightRenderers.Count; i++)
            {
                var renderer = _airfieldLightRenderers[i];
                if (renderer == null)
                    continue;
                var baseColor = renderer.gameObject.name.IndexOf("taxi", StringComparison.OrdinalIgnoreCase) >= 0
                    ? new Color(0.25f, 0.55f, 1f)
                    : warmWhite;
                var color = baseColor * intensity;
                color.a = 1f;
                SetRendererColor(renderer, color);
                if (renderer.material.HasProperty("_EmissionColor"))
                {
                    renderer.material.EnableKeyword("_EMISSION");
                    renderer.material.SetColor("_EmissionColor", baseColor * (0.2f + night * 1.4f));
                }
            }
        }

        private void CollectNightGlowWindows()
        {
            _nightGlowRenderers.Clear();
            var lights = new List<Light>();
            foreach (var name in new[]
                     {
                         "Terminal window glow L",
                         "Terminal window glow R",
                         "Terminal landside glow",
                         "Terminal canopy glow",
                         "Hangar window glow",
                         "Ops shed window glow",
                         "interior_glow_l",
                         "interior_glow_r",
                         "interior_glow_mid",
                         "interior_glow_desk",
                         "interior_glow",
                         "canopy_light_l",
                         "canopy_light_r",
                         "canopy_light_mid",
                         // Authored glass when greybox glow cubes were gated off.
                         "side_window",
                         "side_window_b",
                         "office_window",
                         "window_l",
                         "window_r",
                         "window_side",
                         "window_side_b",
                         "glass_pane",
                         "glass_pane_l",
                         "glass_pane_r",
                         "glass_front",
                         "landside_glass"
                     })
            {
                var go = GameObject.Find(name);
                if (go == null)
                    continue;
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null && !_nightGlowRenderers.Contains(renderer))
                    _nightGlowRenderers.Add(renderer);

                // Real PointLight spill so dusk buildings light the apron (0025 item 5).
                // Skip glass_pane* here — prefix collect below attaches emission only.
                var wantsPoint = !(name.StartsWith("glass_pane", StringComparison.Ordinal)
                                   || name is "glass_front" or "landside_glass"
                                   || name.StartsWith("side_window", StringComparison.Ordinal)
                                   || name.StartsWith("window_", StringComparison.Ordinal)
                                   || name == "office_window");
                if (!wantsPoint)
                    continue;

                var light = go.GetComponent<Light>();
                if (light == null)
                {
                    light = go.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.color = new Color(1f, 0.78f, 0.45f);
                    light.range = name.StartsWith("Hangar", StringComparison.Ordinal) ? 14f : 11f;
                    light.shadows = LightShadows.None;
                    light.intensity = 0f;
                }

                if (!lights.Contains(light))
                    lights.Add(light);
            }

            _windowLights = lights.ToArray();

            // Authored hangar/terminal glass often uses numbered pane names — emission glow only.
            // Do not stamp a PointLight on every glass_pane* (dusk wash / overlapping soup).
            var paneLights = 0;
            const int maxPaneLights = 6;
            foreach (var renderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (renderer == null || _nightGlowRenderers.Contains(renderer))
                    continue;
                var n = renderer.gameObject.name;
                if (!(n.StartsWith("glass_pane", StringComparison.Ordinal)
                      || n.StartsWith("skylight_l", StringComparison.Ordinal)
                      || n.StartsWith("skylight_r", StringComparison.Ordinal)
                      || n.StartsWith("skylight_mid", StringComparison.Ordinal)
                      || n is "skylight_l" or "skylight_r" or "skylight_mid"))
                    continue;
                if (n.StartsWith("skylight_frame", StringComparison.Ordinal))
                    continue;

                _nightGlowRenderers.Add(renderer);
                // Sparse hero PointLights only — every ~7th pane, max six.
                if (paneLights >= maxPaneLights || (_nightGlowRenderers.Count % 7) != 0)
                    continue;

                var light = renderer.GetComponent<Light>();
                if (light == null)
                {
                    light = renderer.gameObject.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.color = new Color(1f, 0.78f, 0.45f);
                    light.range = 10f;
                    light.shadows = LightShadows.None;
                    light.intensity = 0f;
                }

                if (!lights.Contains(light))
                {
                    lights.Add(light);
                    paneLights++;
                }
            }

            _windowLights = lights.ToArray();
            UpdateNightGlow((float)_simulation.TimeOfDay.Daylight);
        }

        private void UpdateNightGlow(float daylight)
        {
            // Presentation-only: terminal/hangar windows warm up as daylight falls,
            // with a soft per-window flicker so night interiors feel occupied (0025 item 5).
            var glow = Mathf.Lerp(1.15f, 0.05f, daylight);
            var night = 1f - daylight;
            for (var i = 0; i < _nightGlowRenderers.Count; i++)
            {
                var renderer = _nightGlowRenderers[i];
                if (renderer == null)
                    continue;
                var flicker = night > 0.35f
                    ? 1f + 0.06f * Mathf.Sin(
                        Time.unscaledTime * (AirsideReusableMotion.WindowFlickerHz * Mathf.PI * 2f + i * 0.37f) + i)
                    : 1f;
                var color = new Color(1f, 0.82f, 0.45f, 1f) * (0.28f + glow * 0.85f) * flicker;
                color.a = 1f;
                var emission = new Color(1f, 0.72f, 0.32f) * (0.2f + glow * 2.4f) * flicker;
                SetRendererColor(renderer, color);
                if (renderer.material.HasProperty("_EmissionColor"))
                {
                    renderer.material.EnableKeyword("_EMISSION");
                    renderer.material.SetColor("_EmissionColor", emission);
                    renderer.material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
            }

            if (_windowLights != null)
            {
                var intensity = Mathf.Lerp(2.4f, 0.02f, daylight);
                for (var i = 0; i < _windowLights.Length; i++)
                {
                    var light = _windowLights[i];
                    if (light == null)
                        continue;
                    var flicker = night > 0.35f
                        ? 1f + 0.05f * Mathf.Sin(
                            Time.unscaledTime * (AirsideReusableMotion.WindowFlickerHz * Mathf.PI * 2f + i * 0.41f) + i * 0.7f)
                        : 1f;
                    light.intensity = intensity * flicker;
                    light.enabled = intensity > 0.05f;
                }
            }

            if (_fuelFarmLight != null)
            {
                var farm = Mathf.Lerp(1.6f, 0.02f, daylight);
                _fuelFarmLight.intensity = farm;
                _fuelFarmLight.enabled = farm > 0.05f;
            }

            if (_arffBayLight != null)
            {
                var bay = Mathf.Lerp(1.8f, 0.02f, daylight);
                _arffBayLight.intensity = bay;
                _arffBayLight.enabled = bay > 0.05f;
            }
        }

        /// <summary>
        /// Decision 0025 items 5+7 — ARFF lightbar blinks amber/red at dusk so the
        /// rescue truck reads as active equipment, not a static prop.
        /// </summary>
        private void UpdateArffLightbar(float daylight)
        {
            if (_arffLightbarRenderer == null)
            {
                var truck = GameObject.Find("ARFF truck");
                if (truck != null)
                {
                    foreach (var t in truck.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name.IndexOf("lightbar", StringComparison.OrdinalIgnoreCase) < 0)
                            continue;
                        _arffLightbarRenderer = t.GetComponent<Renderer>();
                        break;
                    }
                }
            }

            if (_arffLightbarRenderer == null)
                return;

            var night = 1f - daylight;
            var blink = 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(
                Time.unscaledTime * AirsideReusableMotion.ArffLightbarHz * Mathf.PI * 2f));
            var amber = new Color(1f, 0.35f, 0.12f) * (0.15f + night * 1.8f * blink);
            SetRendererColor(_arffLightbarRenderer, Color.Lerp(new Color(0.95f, 0.85f, 0.2f), amber, night));
            if (_arffLightbarRenderer.material.HasProperty("_EmissionColor"))
            {
                _arffLightbarRenderer.material.EnableKeyword("_EMISSION");
                _arffLightbarRenderer.material.SetColor("_EmissionColor", amber);
                _arffLightbarRenderer.material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
        }

        private static Light[] CollectAlsLights()
        {
            var lights = new List<Light>();
            for (var i = 0; i < 8; i++)
            {
                var go = GameObject.Find($"ALS lamp {i}");
                if (go == null)
                    continue;
                var light = go.GetComponent<Light>();
                if (light != null)
                    lights.Add(light);
            }

            foreach (var name in new[] { "REIL lamp L", "REIL lamp R" })
            {
                var go = GameObject.Find(name);
                if (go == null)
                    continue;
                var light = go.GetComponent<Light>();
                if (light != null)
                    lights.Add(light);
            }

            return lights.ToArray();
        }

        private static Light[] BuildApronLights()
        {
            // Spot floods aimed at stand / hangar apron so authored metal picks up
            // directional wash at dusk (0025 item 5) — fewer omnidirectional spills.
            var specs = new[]
            {
                // Four corner masts — SpotLight height matches ~9 m authored flood heads.
                (new Vector3(8f, 9.2f, 12f), new Vector3(17f, 0.2f, 14f)),
                (new Vector3(32f, 9.2f, 12f), new Vector3(17f, 0.2f, 20f)),
                (new Vector3(8f, 9.2f, 22f), new Vector3(26f, 0.2f, 24f)),
                (new Vector3(32f, 9.2f, 22f), new Vector3(20f, 0.2f, 17f)),
                (new Vector3(-18f, 6.5f, 16f), new Vector3(-20f, 0.2f, 20f)),
                (new Vector3(17f, 6.8f, 26f), new Vector3(26f, 0.5f, 27f)),
                (new Vector3(-8f, 5.8f, 22f), new Vector3(-8f, 0.2f, 26f)),
                (new Vector3(20f, 6.5f, 10f), new Vector3(20f, 0.2f, 17f))
            };
            var lights = new Light[specs.Length];
            for (var i = 0; i < specs.Length; i++)
            {
                var (pos, lookAt) = specs[i];
                var go = new GameObject($"Apron flood {i + 1}");
                go.transform.position = pos;
                go.transform.LookAt(lookAt);
                var light = go.AddComponent<Light>();
                light.type = LightType.Spot;
                light.color = new Color(1f, 0.88f, 0.55f);
                light.range = 36f;
                light.spotAngle = 78f;
                light.innerSpotAngle = 42f;
                light.intensity = 0.05f;
                // Soft shadows on the four corner mast floods (hero REF-002 pools).
                light.shadows = i < 4 ? LightShadows.Soft : LightShadows.None;
                lights[i] = light;
            }

            return lights;
        }

        /// <summary>
        /// Decision 0025 item 5 — PointLights along runway edges (every 8 m) plus taxi
        /// centreline hints and REIL pairs at both thresholds so the strip reads as a
        /// lit ribbon at dusk. Presentation only.
        /// </summary>
        private static Light[] BuildRunwayEdgePointLights()
        {
            var lights = new System.Collections.Generic.List<Light>();
            var lightingKit = PreferArtKit(
                "Models/Props/mdl_airfield_lighting_kit_authored_v01.gltf",
                "Models/Props/mdl_airfield_lighting_kit_v02.gltf",
                "Models/Props/mdl_airfield_lighting_kit_v01.gltf");
            var hasLightingKit = !string.IsNullOrEmpty(lightingKit) && ArtGltfLoader.HasKit(lightingKit);
            // Keep PointLights denser than kit fixture spacing — silhouette meshes ≠ illumination.
            var edgeStep = hasLightingKit ? 10 : 8;
            for (var x = -36; x <= 36; x += edgeStep)
            {
                lights.Add(CreateEdgePointLight($"Runway edge point L {x}", new Vector3(x, 0.55f, -3.4f)));
                lights.Add(CreateEdgePointLight($"Runway edge point R {x}", new Vector3(x, 0.55f, 3.4f)));
            }

            // Blue taxi centreline hints — skip z=9 densify when PlaceWorldLighting owns taxi edges.
            if (!hasLightingKit)
            {
                var taxiStem = new Color(0.35f, 0.36f, 0.38f);
                var taxiLens = new Color(0.3f, 0.55f, 1f);
                for (var x = -12; x <= 28; x += 8)
                {
                    lights.Add(CreateEdgePointLight($"Taxi point {x}", new Vector3(x, 0.45f, 9f),
                        new Color(0.3f, 0.55f, 1f), range: 7.5f));
                    var origin = new Vector3(x, 0f, 9f);
                    if (!ArtGltfLoader.TryPlaceNamedMesh(lightingKit, "taxi_stem", origin, Quaternion.identity, taxiStem, out _)
                        && !ArtGltfLoader.TryPlaceNamedMesh(lightingKit, "taxiway_light", origin, Quaternion.identity, taxiLens, out _))
                    {
                        CreateBlock($"Taxi fixture {x}", new Vector3(x, 0.2f, 9f), new Vector3(0.18f, 0.35f, 0.18f), taxiStem);
                    }

                    ArtGltfLoader.TryPlaceNamedMesh(lightingKit, "taxi_lens", origin, Quaternion.identity, taxiLens, out _);
                    ArtGltfLoader.TryPlaceNamedMesh(lightingKit, "taxi_base", origin, Quaternion.identity, taxiStem, out _);
                }
            }
            else
            {
                // Sparse taxi spill along Taxiway A so night taxi still reads without fixture glitter.
                for (var x = -8; x <= 24; x += 16)
                {
                    lights.Add(CreateEdgePointLight($"Taxi point {x}", new Vector3(x, 0.45f, 9f),
                        new Color(0.3f, 0.55f, 1f), range: 9f));
                }
            }

            // Always light the A1 runway exit fillet — kit thinning used to leave it dark.
            lights.Add(CreateEdgePointLight("Taxi A1 point W", new Vector3(-22f, 0.45f, 2.2f),
                new Color(0.3f, 0.55f, 1f), range: 8f));
            lights.Add(CreateEdgePointLight("Taxi A1 point M", new Vector3(-18f, 0.45f, 4.5f),
                new Color(0.3f, 0.55f, 1f), range: 8f));
            lights.Add(CreateEdgePointLight("Taxi A1 point E", new Vector3(-14f, 0.45f, 7f),
                new Color(0.3f, 0.55f, 1f), range: 8f));

            // REIL-style white flashers just beyond each threshold (blinked later).
            lights.Add(CreateEdgePointLight("REIL W L", new Vector3(-44f, 1.6f, -2.8f),
                new Color(1f, 1f, 0.95f), range: 16f));
            lights.Add(CreateEdgePointLight("REIL W R", new Vector3(-44f, 1.6f, 2.8f),
                new Color(1f, 1f, 0.95f), range: 16f));
            lights.Add(CreateEdgePointLight("REIL E L", new Vector3(44f, 1.6f, -2.8f),
                new Color(1f, 1f, 0.95f), range: 16f));
            lights.Add(CreateEdgePointLight("REIL E R", new Vector3(44f, 1.6f, 2.8f),
                new Color(1f, 1f, 0.95f), range: 16f));
            var taxiStemColor = new Color(0.35f, 0.36f, 0.38f);
            void PlaceReilPost(string name, Vector3 origin)
            {
                var kit = ArtGltfLoader.TryPlaceNamedMesh(lightingKit, "obst_stem", origin, Quaternion.identity, taxiStemColor, out _)
                    | ArtGltfLoader.TryPlaceNamedMesh(lightingKit, "obst_base", origin, Quaternion.identity, taxiStemColor, out _)
                    | ArtGltfLoader.TryPlaceNamedMesh(lightingKit, "obst_lens", origin, Quaternion.identity, new Color(1f, 1f, 0.9f), out _);
                if (!kit)
                    CreateBlock(name, origin + new Vector3(0f, 0.8f, 0f), new Vector3(0.18f, 1.6f, 0.18f), new Color(0.4f, 0.42f, 0.44f));
            }

            PlaceReilPost("REIL post W L", new Vector3(-44f, 0f, -2.8f));
            PlaceReilPost("REIL post W R", new Vector3(-44f, 0f, 2.8f));
            PlaceReilPost("REIL post E L", new Vector3(44f, 0f, -2.8f));
            PlaceReilPost("REIL post E R", new Vector3(44f, 0f, 2.8f));
            return lights.ToArray();
        }

        private static Light CreateEdgePointLight(string name, Vector3 position, Color? color = null, float range = 11f)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color ?? new Color(1f, 0.96f, 0.78f);
            light.range = range;
            light.intensity = 0.02f;
            return light;
        }

        /// <summary>
        /// Decision 0025 item 5 — threshold / short approach point lights so runway
        /// ends read at dusk without a full nav-aid system. Presentation only.
        /// </summary>
        private static Light[] BuildThresholdApproachLights()
        {
            var specs = new (Vector3 Pos, Color Color, float Range)[]
            {
                // West threshold (09) — warm white bars + green wing-bar hint.
                (new Vector3(-38f, 1.1f, -2.2f), new Color(1f, 0.96f, 0.82f), 14f),
                (new Vector3(-38f, 1.1f, 2.2f), new Color(1f, 0.96f, 0.82f), 14f),
                (new Vector3(-42f, 0.9f, -1.1f), new Color(0.35f, 0.95f, 0.55f), 10f),
                (new Vector3(-42f, 0.9f, 1.1f), new Color(0.35f, 0.95f, 0.55f), 10f),
                // East threshold (27).
                (new Vector3(38f, 1.1f, -2.2f), new Color(1f, 0.96f, 0.82f), 14f),
                (new Vector3(38f, 1.1f, 2.2f), new Color(1f, 0.96f, 0.82f), 14f),
                (new Vector3(42f, 0.9f, -1.1f), new Color(0.35f, 0.95f, 0.55f), 10f),
                (new Vector3(42f, 0.9f, 1.1f), new Color(0.35f, 0.95f, 0.55f), 10f),
                // Compact PAPI-style ladder south of west approach path.
                (new Vector3(-34f, 1.4f, -5.2f), new Color(1f, 0.35f, 0.28f), 9f),
                (new Vector3(-32.5f, 1.4f, -5.2f), new Color(1f, 0.35f, 0.28f), 9f),
                (new Vector3(-31f, 1.4f, -5.2f), new Color(1f, 0.95f, 0.75f), 9f),
                (new Vector3(-29.5f, 1.4f, -5.2f), new Color(1f, 0.95f, 0.75f), 9f)
            };

            var lights = new Light[specs.Length];
            var lightingKit = PreferArtKit(
                "Models/Props/mdl_airfield_lighting_kit_authored_v01.gltf",
                "Models/Props/mdl_airfield_lighting_kit_v02.gltf",
                "Models/Props/mdl_airfield_lighting_kit_v01.gltf");
            var stem = new Color(0.35f, 0.36f, 0.38f);
            for (var i = 0; i < specs.Length; i++)
            {
                var spec = specs[i];
                var origin = new Vector3(spec.Pos.x, 0f, spec.Pos.z);
                var kitLamp = ArtGltfLoader.TryPlaceNamedMesh(
                    lightingKit, "edge_stem", origin, Quaternion.identity, stem, out _)
                    | ArtGltfLoader.TryPlaceNamedMesh(
                        lightingKit, "edge_lens", origin, Quaternion.identity, spec.Color, out _)
                    | ArtGltfLoader.TryPlaceNamedMesh(
                        lightingKit, "taxi_lens", origin, Quaternion.identity, spec.Color, out _);
                if (!kitLamp)
                    CreateBlock($"Threshold lamp {i}", spec.Pos, new Vector3(0.22f, 0.18f, 0.22f), spec.Color);
                var go = new GameObject($"Threshold approach light {i + 1}");
                go.transform.position = spec.Pos + new Vector3(0f, 0.15f, 0f);
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = spec.Color;
                light.range = spec.Range;
                light.intensity = 0.04f;
                lights[i] = light;
            }

            return lights;
        }

        /// <summary>
        /// Decision 0025 item 5 — realtime apron ReflectionProbe so Lit wet asphalt /
        /// metal pick up local floods at night without a baked probe set.
        /// </summary>
        private static ReflectionProbe BuildApronReflectionProbe()
        {
            var go = new GameObject("Apron reflection probe");
            go.transform.position = new Vector3(20f, 3.5f, 17f);
            var probe = go.AddComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.IndividualFaces;
            probe.resolution = 128;
            // Cover stand apron + hangar face so authored metal/glass get local floods.
            probe.size = new Vector3(56f, 22f, 42f);
            probe.center = Vector3.zero;
            probe.intensity = 1f;
            probe.boxProjection = true;
            probe.shadowDistance = 28f;
            probe.nearClipPlane = 0.3f;
            probe.farClipPlane = 90f;
            probe.RenderProbe();
            return probe;
        }

        /// <summary>
        /// Decision 0025 item 5 — second realtime probe on the terminal landside so
        /// authored glass / canopy posts catch window spill at dusk.
        /// </summary>
        private static ReflectionProbe BuildTerminalReflectionProbe()
        {
            var go = new GameObject("Terminal reflection probe");
            go.transform.position = new Vector3(26f, 3.2f, 27f);
            var probe = go.AddComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.IndividualFaces;
            probe.resolution = 64;
            probe.size = new Vector3(32f, 16f, 22f);
            probe.center = Vector3.zero;
            probe.intensity = 0.95f;
            probe.boxProjection = true;
            probe.shadowDistance = 18f;
            probe.nearClipPlane = 0.3f;
            probe.farClipPlane = 60f;
            probe.RenderProbe();
            return probe;
        }

        private static Light[] BuildLandsideStreetlights()
        {
            // Poles + warm point lights along access road and car park edge.
            var positions = new[]
            {
                new Vector3(23.5f, 0f, 34f),
                new Vector3(23.5f, 0f, 40f),
                new Vector3(29f, 0f, 46f),
                new Vector3(40f, 0f, 46f),
                new Vector3(52f, 0f, 46f),
                new Vector3(48f, 0f, 40f)
            };
            var lightingKit = PreferArtKit(
                "Models/Props/mdl_airfield_lighting_kit_authored_v01.gltf",
                "Models/Props/mdl_airfield_lighting_kit_v02.gltf",
                "Models/Props/mdl_airfield_lighting_kit_v01.gltf");
            var steel = new Color(0.35f, 0.36f, 0.38f);
            var head = new Color(0.25f, 0.26f, 0.28f);
            var lampColor = new Color(1f, 0.92f, 0.7f);
            // Apron flood masts are oversized for drop-off — shrink landside kit stems.
            var landsideMastScale = Vector3.one * 0.62f;
            var lights = new Light[positions.Length];
            for (var i = 0; i < positions.Length; i++)
            {
                var pos = positions[i];
                var kitPole = ArtGltfLoader.TryPlaceNamedMesh(
                    lightingKit, "flood_pole", pos, Quaternion.identity, steel, out _,
                    localScale: landsideMastScale);
                var kitHead = ArtGltfLoader.TryPlaceNamedMesh(
                    lightingKit, "flood_head", pos, Quaternion.identity, head, out _,
                    localScale: landsideMastScale);
                ArtGltfLoader.TryPlaceNamedMesh(
                    lightingKit, "flood_lamp", pos, Quaternion.identity, lampColor, out _,
                    localScale: landsideMastScale);
                if (!kitPole)
                {
                    CreateBlock($"Streetlight pole {i}", pos + new Vector3(0f, 2.2f, 0f), new Vector3(0.14f, 4.4f, 0.14f), steel);
                }

                if (!kitHead)
                {
                    CreateBlock($"Streetlight head {i}", pos + new Vector3(0.35f, 4.35f, 0f), new Vector3(0.7f, 0.18f, 0.35f), head);
                    CreateBlock($"Streetlight lamp {i}", pos + new Vector3(0.55f, 4.2f, 0f), new Vector3(0.28f, 0.16f, 0.28f), lampColor);
                }

                var go = new GameObject($"Landside streetlight {i + 1}");
                // Kit masts are scaled ~0.62 — keep the point light near the shorter head.
                var lightHeight = kitPole || kitHead ? 2.55f : 4.1f;
                go.transform.position = pos + new Vector3(0.35f, lightHeight, 0f);
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.9f, 0.7f);
                light.range = 16f;
                light.intensity = 0.02f;
                lights[i] = light;
            }

            return lights;
        }

        private static Light BuildAerodromeBeacon()
        {
            // Presentation-only aerodrome beacon — prefer lighting-kit obst mast.
            var mast = new GameObject("Aerodrome beacon").transform;
            mast.position = new Vector3(38f, 0f, 18f);
            var lightingKit = PreferArtKit(
                "Models/Props/mdl_airfield_lighting_kit_authored_v01.gltf",
                "Models/Props/mdl_airfield_lighting_kit_v02.gltf",
                "Models/Props/mdl_airfield_lighting_kit_v01.gltf");
            var steel = new Color(0.55f, 0.56f, 0.58f);
            var origin = mast.position;
            var kitMast = false;
            if (ArtGltfLoader.TryPlaceNamedMesh(lightingKit, "obst_base", origin, Quaternion.identity, steel, out var basePart))
            {
                basePart.SetParent(mast, true);
                kitMast = true;
            }

            if (ArtGltfLoader.TryPlaceNamedMesh(lightingKit, "obst_stem", origin, Quaternion.identity, steel, out var stemPart))
            {
                stemPart.SetParent(mast, true);
                kitMast = true;
            }

            if (ArtGltfLoader.TryPlaceNamedMesh(
                    lightingKit, "obst_lens", origin, Quaternion.identity,
                    new Color(0.95f, 0.95f, 0.9f), out var lensPart))
            {
                lensPart.SetParent(mast, true);
                kitMast = true;
            }

            ArtGltfLoader.TryPlaceNamedMesh(
                lightingKit, "obst_beacon_ring", origin, Quaternion.identity,
                new Color(1f, 0.9f, 0.5f), out var ringPart);
            if (ringPart != null)
                ringPart.SetParent(mast, true);

            if (!kitMast)
            {
                var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pole.name = "Beacon mast";
                Object.Destroy(pole.GetComponent<Collider>());
                pole.transform.SetParent(mast, false);
                pole.transform.localPosition = new Vector3(0f, 4.5f, 0f);
                pole.transform.localScale = new Vector3(0.18f, 4.5f, 0.18f);
                if (pole.GetComponent<Renderer>() != null)
                    SetRendererColor(pole.GetComponent<Renderer>(), steel);

                var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                head.name = "Beacon head";
                Object.Destroy(head.GetComponent<Collider>());
                head.transform.SetParent(mast, false);
                head.transform.localPosition = new Vector3(0f, 9.1f, 0f);
                head.transform.localScale = new Vector3(0.55f, 0.55f, 0.55f);
                SetRendererColor(head.GetComponent<Renderer>(), new Color(0.95f, 0.95f, 0.9f));
            }

            var lightGo = new GameObject("Beacon light");
            lightGo.transform.SetParent(mast, false);
            lightGo.transform.localPosition = new Vector3(0f, kitMast ? 6.5f : 9.1f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.85f, 1f, 0.9f);
            light.range = 42f;
            light.intensity = 0f;
            return light;
        }

        private void UpdateAerodromeBeacon(float daylight)
        {
            if (_aerodromeBeacon == null)
                return;

            // Night-only white/green pulse — presentation decoration, not navigational.
            if (daylight > 0.38f)
            {
                _aerodromeBeacon.intensity = 0f;
                return;
            }

            var pulse = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * (AirsideReusableMotion.BeaconHz * Mathf.PI)));
            _aerodromeBeacon.intensity = pulse * Mathf.Lerp(2.4f, 0.2f, daylight / 0.38f);
            _aerodromeBeacon.color = Mathf.FloorToInt(Time.unscaledTime * AirsideReusableMotion.BeaconHz) % 2 == 0
                ? new Color(0.95f, 0.98f, 1f)
                : new Color(0.35f, 0.95f, 0.55f);
        }

        /// <summary>
        /// Paved lead-in along the taxi chord from Taxiway A (8,9) to the stand bay.
        /// </summary>
        private static void CreateTaxiLeadPad(string name, float standZ)
        {
            var from = new Vector3(8f, -0.01f, 9f);
            var to = new Vector3(17f, -0.01f, standZ);
            var mid = (from + to) * 0.5f;
            var delta = to - from;
            var length = delta.magnitude + 1.6f;
            var yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            var pad = CreateBlock(name, mid, new Vector3(4.6f, 0.1f, length), new Color(0.28f, 0.3f, 0.32f),
                "Textures/Surfaces/tx_asphalt_runway_basecolor_v01.png", new Vector2(1.2f, 1.4f));
            pad.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        private static void BuildAirfield()
        {
            // Batch B surfaces (Approved): textured when Art PNGs load; solid colours remain fallback.
            CreateBlock("Grass", new Vector3(0f, -0.65f, 4f), new Vector3(94f, 1f, 66f), Shade(AirsideTheme.Eucalyptus, 0.55f),
                "Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png", new Vector2(12f, 8f));
            CreateBlock("Runway", new Vector3(0f, -0.08f, 0f), new Vector3(78f, 0.15f, 7f), new Color(0.16f, 0.18f, 0.2f),
                "Textures/Surfaces/tx_asphalt_runway_basecolor_v01.png", new Vector2(10f, 1.2f));
            CreateBlock("Runway shoulder N", new Vector3(0f, -0.1f, 4.2f), new Vector3(76f, 0.08f, 1.4f), new Color(0.28f, 0.3f, 0.28f),
                "Textures/Surfaces/tx_concrete_apron_basecolor_v01.png", new Vector2(8f, 0.3f));
            CreateBlock("Runway shoulder S", new Vector3(0f, -0.1f, -4.2f), new Vector3(76f, 0.08f, 1.4f), new Color(0.28f, 0.3f, 0.28f),
                "Textures/Surfaces/tx_concrete_apron_basecolor_v01.png", new Vector2(8f, 0.3f));
            CreateBlock("Taxiway A", new Vector3(8f, -0.02f, 9f), new Vector3(48f, 0.12f, 4f), new Color(0.22f, 0.24f, 0.26f),
                "Textures/Surfaces/tx_asphalt_runway_basecolor_v01.png", new Vector2(6f, 0.8f));
            // A1 runway exit / fillet — without this the taxi path (-24,0)→(-12,9) is grass.
            CreateBlock("Taxiway A exit", new Vector3(-18f, -0.02f, 4.5f), new Vector3(16f, 0.12f, 9.5f), new Color(0.22f, 0.24f, 0.26f),
                "Textures/Surfaces/tx_asphalt_runway_basecolor_v01.png", new Vector2(3f, 1.2f));
            // Lead-in pads follow the actual taxi chord (8,9)→(17, standZ).
            CreateTaxiLeadPad("Taxi lead Stand 1", standZ: 14f);
            CreateTaxiLeadPad("Taxi lead Stand 2", standZ: 20f);
            CreateBlock("Apron", new Vector3(20f, 0f, 17f), new Vector3(28f, 0.12f, 14f), new Color(0.38f, 0.4f, 0.41f),
                "Textures/Surfaces/tx_concrete_apron_basecolor_v01.png", new Vector2(4f, 2f));
            // Skip apron joint/slab densify — MAT concrete + soft wet residual carry the read;
            // greybox joints read as scattered blocks from landing/follow cameras.
            // Batch C buildings — prefer richer v03 kits (0025 item 2) with v02/v01 fallback.
            PlaceBuildingOrFallback(
                PreferArtKit(
                    "Models/Buildings/mdl_terminal_regional_small_v05.gltf",
                    "Models/Buildings/mdl_terminal_regional_small_authored_v01.gltf",
                    "Models/Buildings/mdl_terminal_regional_small_v04.gltf",
                    "Models/Buildings/mdl_terminal_regional_small_v03.gltf",
                    "Models/Buildings/mdl_terminal_regional_small_v02.gltf",
                    "Models/Buildings/mdl_terminal_regional_small_v01.gltf"),
                new Vector3(26f, 0f, 27f),
                name =>
                {
                    if (name.StartsWith("glass_pane", StringComparison.Ordinal)
                        || name is "glass_front" or "windows" or "cabin_windows" or "landside_glass"
                        or "door_glass" or "boarding_glass" or "service_window")
                        return new Color(0.16f, 0.38f, 0.5f, 0.42f);
                    if (name is "interior_glow_l" or "interior_glow_r" or "interior_glow_mid" or "interior_glow_desk")
                        return new Color(1f, 0.82f, 0.55f);
                    if (name is "interior_counter" or "interior_seat_row" or "interior_desk_a" or "interior_desk_b"
                        or "interior_table_1" or "interior_table_2")
                        return new Color(0.45f, 0.42f, 0.38f);
                    if (name is "interior_chair_1" or "interior_chair_2" or "interior_chair_3" or "interior_chair_4")
                        return new Color(0.35f, 0.4f, 0.48f);
                    if (name is "interior_figure_a" or "interior_figure_b" or "interior_figure_c")
                        return new Color(0.25f, 0.28f, 0.32f);
                    if (name is "entrance" or "entrance_door_l" or "entrance_door_r" or "boarding_gate")
                        return new Color(0.55f, 0.6f, 0.64f);
                    if (name.StartsWith("window_mullion", StringComparison.Ordinal)
                        || name.StartsWith("landside_mullion", StringComparison.Ordinal)
                        || name is "window_transom" or "window_midrail" or "window_sill" or "window_header"
                        or "landside_transom" or "landside_sill"
                        or "entrance_transom" or "entrance_frame" or "boarding_frame"
                        or "entrance_handle_l" or "entrance_handle_r"
                        or "service_window_frame")
                        return new Color(0.72f, 0.75f, 0.78f);
                    if (name.StartsWith("wall_rib", StringComparison.Ordinal)
                        || name.StartsWith("service_rib", StringComparison.Ordinal)
                        || name.StartsWith("girth_band", StringComparison.Ordinal)
                        || name is "service_wing" or "service_door" or "baggage_door" or "baggage_ramp"
                        or "service_door_frame" or "baggage_door_frame")
                        return new Color(0.58f, 0.62f, 0.64f);
                    if ((name is "end_cap_left" or "end_cap_right" or "column_l" or "column_r" or "column_ml" or "column_mr"
                        or "buttress_r" or "plinth" or "plinth_step" or "plinth_kerb_l" or "plinth_kerb_r")
                        || name.StartsWith("end_cap_soft", StringComparison.Ordinal))
                        return new Color(0.62f, 0.66f, 0.69f);
                    if (name is "canopy" or "canopy_post_l" or "canopy_post_r" or "canopy_post_ml" or "canopy_post_mr"
                        or "canopy_beam" or "canopy_edge" or "canopy_brace_l" or "canopy_brace_r"
                        or "canopy_brace_ml" or "canopy_brace_mr"
                        or "canopy_light_l" or "canopy_light_r" or "canopy_light_mid"
                        or "canopy_soffit" or "canopy_gutter" or "canopy_flash"
                        or "roof_slab" or "roof_plant" or "roof_plant_b"
                        or "roof_plant_c" or "roof_parapet" or "roof_parapet_back"
                        or "roof_vent_a" or "roof_vent_b" or "roof_vent_c"
                        or "roof_panel_l" or "roof_panel_r" or "roof_ridge"
                        or "roof_eave_front" or "roof_eave_back"
                        or "roof_flash_front" or "roof_flash_back"
                        or "fascia_front" or "fascia_back" or "soffit_front"
                        or "landside_awning" or "landside_awning_brace_l" or "landside_awning_brace_r"
                        or "signage_bar" or "signage_cap" or "signage_glyph_a" or "signage_glyph_b"
                        or "hvac_duct" or "hvac_duct_b" or "flag_pole" or "flag_cloth"
                        or "baggage_canopy" or "boarding_canopy" or "downpipe_l" or "downpipe_r"
                        or "service_wing_roof" or "service_wing_fascia"
                        or "corner_trim_l" or "corner_trim_r" or "corner_trim_bl" or "corner_trim_br")
                        return new Color(0.55f, 0.58f, 0.6f);
                    return new Color(0.68f, 0.72f, 0.75f);
                },
                () =>
                {
                    CreateBlock("Terminal", new Vector3(26f, 2.2f, 27f), new Vector3(22f, 4.5f, 5f), new Color(0.68f, 0.72f, 0.75f));
                    CreateBlock("Terminal glass", new Vector3(26f, 2.4f, 24.45f), new Vector3(17f, 2.2f, 0.12f), new Color(0.16f, 0.38f, 0.5f),
                        "Textures/Environment/tx_terminal_glass_mask_v01.png", new Vector2(3f, 1.5f));
                    CreateBlock("Terminal end L", new Vector3(14.8f, 2.0f, 27f), new Vector3(1.2f, 4.0f, 5.2f), new Color(0.62f, 0.66f, 0.69f));
                    CreateBlock("Terminal end R", new Vector3(37.2f, 2.0f, 27f), new Vector3(1.2f, 4.0f, 5.2f), new Color(0.62f, 0.66f, 0.69f));
                    CreateBlock("Terminal service", new Vector3(32f, 1.4f, 30.5f), new Vector3(8f, 2.8f, 3f), new Color(0.58f, 0.62f, 0.64f));
                },
                "Textures/Environment/tx_terminal_glass_mask_v01.png",
                surfaceTextureRelativePath: "Textures/Surfaces/tx_concrete_apron_basecolor_v01.png",
                surfaceTextureTiling: new Vector2(2.5f, 1.2f),
                surfaceMeshNames: new[]
                {
                    "terminal_body", "end_cap", "service_wing", "roof", "canopy", "buttress", "plinth",
                    "column", "signage", "fascia", "soffit", "wall_rib", "service_rib", "corner_trim", "girth",
                    "roof_panel", "roof_ridge", "roof_eave", "hvac"
                });
            // Warm interior spill at dusk/night — only when the terminal kit did not
            // already ship interior glow meshes (avoid stacking cubes on authored glass).
            if (GameObject.Find("interior_glow_l") == null
                && GameObject.Find("interior_glow_r") == null
                && GameObject.Find("interior_glow_mid") == null)
            {
                CreateBlock("Terminal window glow L", new Vector3(20f, 2.35f, 24.5f), new Vector3(5.5f, 1.6f, 0.08f), new Color(1f, 0.82f, 0.45f));
                CreateBlock("Terminal window glow R", new Vector3(32f, 2.35f, 24.5f), new Vector3(5.5f, 1.6f, 0.08f), new Color(1f, 0.82f, 0.45f));
            }

            if (GameObject.Find("landside_glass") == null && GameObject.Find("interior_glow_desk") == null)
                CreateBlock("Terminal landside glow", new Vector3(26f, 2.2f, 29.4f), new Vector3(10f, 1.4f, 0.08f), new Color(1f, 0.8f, 0.42f));
            BuildTerminalLandsideCanopy();
            PlaceBuildingOrFallback(
                PreferArtKit(
                    "Models/Buildings/mdl_hangar_small_v05.gltf",
                    "Models/Buildings/mdl_hangar_small_authored_v01.gltf",
                    "Models/Buildings/mdl_hangar_small_v04.gltf",
                    "Models/Buildings/mdl_hangar_small_v03.gltf",
                    "Models/Buildings/mdl_hangar_small_v02.gltf",
                    "Models/Buildings/mdl_hangar_small_v01.gltf"),
                new Vector3(-20f, 0f, 20f),
                name =>
                {
                    if (name.StartsWith("glass_pane", StringComparison.Ordinal)
                        || name is "side_window" or "side_window_b" or "office_window"
                        or "skylight_l" or "skylight_r" or "skylight_mid")
                        return new Color(0.18f, 0.36f, 0.48f, 0.42f);
                    if (name.StartsWith("office_mullion", StringComparison.Ordinal)
                        || name.StartsWith("skylight_frame", StringComparison.Ordinal)
                        || name is "side_mullion_l" or "side_mullion_r"
                        or "side_sill_l" or "side_sill_r" or "office_sill" or "office_header")
                        return new Color(0.4f, 0.44f, 0.48f);
                    return name switch
                    {
                        "door_opening" or "door_panel_l" or "door_panel_r" or "door_rib_l" or "door_rib_r"
                            or "door_bar_l1" or "door_bar_l2" or "door_bar_l3" or "door_bar_l4"
                            or "door_bar_r1" or "door_bar_r2" or "door_bar_r3" or "door_bar_r4"
                            or "door_handle_l" or "door_handle_r"
                            or "door_warning_l" or "door_warning_r"
                            or "personnel_door" or "personnel_frame" or "rear_door" => new Color(0.22f, 0.24f, 0.26f),
                        "roof_ridge" or "roof_ridge_cap" or "roof_vent_ridge"
                            or "roof_panel_l" or "roof_panel_r" or "crane_beam" or "crane_trolley"
                            or "crane_hook" or "gutter_front" or "gutter_back" or "gutter_end_l" or "gutter_end_r"
                            or "roof_rib_1" or "roof_rib_2" or "roof_rib_3" or "roof_rib_4"
                            or "roof_rib_5" or "roof_rib_6" or "roof_rib_7"
                            or "flood_can_l" or "flood_can_r" or "downpipe_l" or "downpipe_r"
                            or "sign_board" or "sign_glyph" or "rear_vent"
                            or "fascia_front" or "fascia_back" or "office_roof" or "office_fascia"
                            or "office_downpipe" or "crane_rail_l" or "crane_rail_r"
                            or "gable_front_l" or "gable_front_r" or "gable_back_l" or "gable_back_r"
                            or "gable_apex_front" or "gable_apex_back" or "door_header" or "door_threshold"
                            => new Color(0.4f, 0.44f, 0.48f),
                        "buttress_l" or "buttress_r" or "door_track_l" or "door_track_r" or "door_track_mid"
                            or "door_track_brace_l" or "door_track_brace_r"
                            or "plinth" or "side_louvre_l" or "side_louvre_r"
                            or "workbench" or "tool_cabinet" or "floor_drain" or "floor_mark_bay"
                            or "side_vent" or "side_vent_b" or "office_lean" or "office_door"
                            or "column_ml" or "column_mr"
                            or "cladding_face_l" or "cladding_face_r"
                            or "cladding_face_front" or "cladding_face_back"
                            or "girth_band_1" or "girth_band_2" or "girth_band_3"
                            or "corner_trim_fl" or "corner_trim_fr"
                            or "corner_trim_bl" or "corner_trim_br"
                            or "office_step" or "office_awning" or "roof_flash_front" or "roof_flash_back"
                            or "flood_mount_l" or "flood_mount_r" or "girth_band_4" or "service_door_step"
                            => new Color(0.42f, 0.46f, 0.5f),
                        "door_peek_l" or "door_peek_r" => new Color(0.18f, 0.36f, 0.48f, 0.42f),
                        _ => name.StartsWith("wall_rib_", StringComparison.Ordinal)
                            ? new Color(0.42f, 0.46f, 0.5f)
                            : new Color(0.45f, 0.5f, 0.54f)
                    };
                },
                () =>
                {
                    CreateBlock("Hangar", new Vector3(-20f, 2.5f, 20f), new Vector3(14f, 5f, 9f), new Color(0.45f, 0.5f, 0.54f),
                        "Textures/Surfaces/tx_corrugated_metal_basecolor_v01.png", new Vector2(2.5f, 1.5f));
                    CreateBlock("Hangar door", new Vector3(-20f, 2.0f, 24.6f), new Vector3(8f, 4f, 0.2f), new Color(0.22f, 0.24f, 0.26f));
                },
                "Textures/Environment/tx_terminal_glass_mask_v01.png",
                surfaceTextureRelativePath: "Textures/Surfaces/tx_corrugated_metal_basecolor_v01.png",
                surfaceTextureTiling: new Vector2(2.5f, 1.5f),
                surfaceMeshNames: new[] { "hangar_shell", "roof", "buttress", "door_track", "side_vent", "cladding", "wall_rib", "girth", "gable" });
            // Sliding door slab only when the hangar kit did not ship panel doors.
            if (GameObject.Find("Hangar door") == null
                && GameObject.Find("door_panel_l") == null
                && GameObject.Find("door_panel_r") == null)
                CreateBlock("Hangar door", new Vector3(-20f, 2.0f, 24.6f), new Vector3(8f, 4f, 0.2f), new Color(0.22f, 0.24f, 0.26f));
            // Prefer hangar-kit bay props (workbench / tool cabinet) over greybox densify.
            if (GameObject.Find("workbench") == null && GameObject.Find("tool_cabinet") == null)
                BuildHangarBayInterior();
            if (GameObject.Find("side_window") == null
                && GameObject.Find("side_window_b") == null
                && GameObject.Find("office_window") == null
                && GameObject.Find("glass_pane") == null
                && GameObject.Find("glass_pane_l") == null
                && GameObject.Find("glass_pane_r") == null)
                CreateBlock("Hangar window glow", new Vector3(-20f, 3.2f, 24.55f), new Vector3(4.5f, 1.8f, 0.08f), new Color(1f, 0.75f, 0.35f));
            PlaceBuildingOrFallback(
                PreferArtKit(
                    "Models/Buildings/mdl_operations_shed_v05.gltf",
                    "Models/Buildings/mdl_operations_shed_authored_v01.gltf",
                    "Models/Buildings/mdl_operations_shed_v04.gltf",
                    "Models/Buildings/mdl_operations_shed_v03.gltf",
                    "Models/Buildings/mdl_operations_shed_v02.gltf",
                    "Models/Buildings/mdl_operations_shed_v01.gltf"),
                new Vector3(-8f, 0f, 26f),
                name =>
                {
                    if (name.StartsWith("glass_pane", StringComparison.Ordinal)
                        || name is "window_l" or "window_r" or "window_side" or "window_side_b")
                        return new Color(0.2f, 0.4f, 0.5f, 0.42f);
                    if (name.StartsWith("window_mullion", StringComparison.Ordinal)
                        || name.StartsWith("wall_rib", StringComparison.Ordinal)
                        || name is "window_transom_l" or "window_transom_r"
                        or "window_header_l" or "window_header_r"
                        or "window_sill_l" or "window_sill_r"
                        or "girth_band_1" or "girth_band_2" or "girth_band_3"
                        or "cladding_face_l" or "cladding_face_r"
                        or "cladding_face_front" or "cladding_face_back")
                        return new Color(0.48f, 0.5f, 0.46f);
                    return name switch
                    {
                        "door" or "door_frame" or "door_knob" or "door_kick" => new Color(0.35f, 0.38f, 0.34f),
                        "interior_glow" => new Color(1f, 0.82f, 0.5f),
                        "interior_desk" => new Color(0.42f, 0.4f, 0.36f),
                        "signage" => AirsideTheme.SafetyYellow,
                        "signage_glyph" => new Color(0.12f, 0.18f, 0.28f),
                        "porch_roof" or "porch_beam" or "porch_light" or "porch_fascia" or "porch_soffit"
                            or "porch_riser" or "porch_post_l" or "porch_post_r"
                            or "porch_post_mid_l" or "porch_post_mid_r"
                            or "roof_ridge" or "roof_ridge_cap" or "roof_panel" or "roof_panel_l" or "roof_panel_r"
                            or "roof_gutter" or "roof_fascia" or "roof_downpipe_l" or "roof_downpipe_r"
                            or "roof_vent_a" or "roof_vent_b" or "roof_eave_back"
                            or "roof_flash_front" or "roof_flash_back"
                            or "gable_front_l" or "gable_front_r" or "gable_back_l" or "gable_back_r"
                            or "gable_apex_front" or "gable_apex_back"
                            or "antenna_mast" or "antenna_dish" or "antenna_boom" or "antenna_guy" or "antenna_guy_b"
                            or "radio_antenna_whip"
                            or "ac_unit" or "ac_unit_b" or "ac_grille" or "ac_pipe" or "ac_pipe_b" or "radio_rack"
                            or "vent_pipe" or "wall_vent" or "flood_can" or "flood_can_b"
                            or "flood_mount" or "flood_mount_b"
                            or "step_rail_l" or "step_rail_r"
                            or "side_louvre" or "side_louvre_b" or "mailbox" or "bench" or "plinth"
                            or "shed_corner_l" or "shed_corner_r"
                            or "window_ledge_l" or "window_ledge_r"
                            or "window_awning_l" or "window_awning_r"
                            or "power_box" or "hose_reel"
                            => new Color(0.48f, 0.5f, 0.46f),
                        _ => new Color(0.55f, 0.58f, 0.52f)
                    };
                },
                () => CreateBlock("Ops shed", new Vector3(-8f, 1.4f, 26f), new Vector3(6f, 2.8f, 4f), new Color(0.55f, 0.58f, 0.52f),
                    "Textures/Surfaces/tx_corrugated_metal_basecolor_v01.png", new Vector2(1.5f, 1.2f)),
                "Textures/Environment/tx_terminal_glass_mask_v01.png",
                surfaceTextureRelativePath: "Textures/Surfaces/tx_corrugated_metal_basecolor_v01.png",
                surfaceTextureTiling: new Vector2(1.5f, 1.2f),
                surfaceMeshNames: new[] { "shed_body", "porch", "roof", "cladding", "wall_rib", "girth" });
            if (GameObject.Find("interior_glow") == null
                && GameObject.Find("window_l") == null
                && GameObject.Find("window_r") == null)
                CreateBlock("Ops shed window glow", new Vector3(-8f, 1.5f, 24.1f), new Vector3(3.2f, 1.1f, 0.08f), new Color(1f, 0.78f, 0.4f));

            // Soft wear accent only — large stain sheets were opaque black patches (PNG alpha ignored).
            CreateDecalQuad("Runway wear", new Vector3(0f, 0.02f, 0f), new Vector3(36f, 1f, 1.2f),
                "Textures/Decals/dc_runway_wear_v01.png");
            // Soft fringe so the apron doesn't float as a hard cutout (REF densify).
            // N/S only — E/W fringe cubes read as blocks beside taxi/stand lead-ins.
            var fringe = Shade(AirsideTheme.DryGrass, 0.7f);
            CreateBlock("Apron fringe N", new Vector3(20f, -0.02f, 24.4f), new Vector3(29f, 0.06f, 1.2f), fringe,
                "Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png", new Vector2(4f, 0.4f));
            CreateBlock("Apron fringe S", new Vector3(20f, -0.02f, 9.6f), new Vector3(29f, 0.06f, 1.2f), fringe,
                "Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png", new Vector2(4f, 0.4f));
            // Planter strip between terminal glass and apron edge — prefer PRP-003 kit.
            if (!TryPlaceAirsidePlanterStrip())
            {
                CreateBlock("Terminal planter bed", new Vector3(26f, 0.12f, 24.4f), new Vector3(14f, 0.28f, 1.1f),
                    new Color(0.28f, 0.22f, 0.16f));
                PlaceShrub(new Vector3(20f, 0f, 24.4f), 0.55f);
                PlaceShrub(new Vector3(23f, 0f, 24.5f), 0.62f);
                PlaceShrub(new Vector3(26f, 0f, 24.35f), 0.58f);
                PlaceShrub(new Vector3(29f, 0f, 24.45f), 0.65f);
                PlaceShrub(new Vector3(32f, 0f, 24.4f), 0.5f);
            }

            PlaceWorldMarkings();
            PlaceWorldLighting();
            PlaceWorldProps();
            BuildEnvironmentContext();

            BuildStandMarking(17f, 14f, "Stand 1");
            BuildStandMarking(17f, 20f, "Stand 2");
            // Stand bay digits come from PlaceWorldMarkings / PlaceRunwayDigit (kit-prefer).

        }

        /// <summary>
        /// Decision 0025 item 3 — regional environment greybox around the operating
        /// airfield: coast, access road, car park, fencing, vegetation and a soft
        /// horizon dome. Presentation only; primitives + existing Batch B surfaces.
        /// </summary>
        private static void BuildEnvironmentContext()
        {
            // Outer paddock + dry-grass fringe so the airfield is not a floating island.
            // When WLD-004 terrain accents own paddock_* meshes, keep thin fringe only.
            var terrainKit = PreferArtKit("Models/Environment/mdl_kingscote_context_terrain_v01.gltf");
            var hasTerrainKit = !string.IsNullOrEmpty(terrainKit) && ArtGltfLoader.HasKit(terrainKit);
            if (hasTerrainKit)
            {
                CreateBlock("Outer paddock N", new Vector3(0f, -0.85f, 58f), new Vector3(90f, 0.55f, 14f), Shade(AirsideTheme.DryGrass, 0.7f),
                    "Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png", new Vector2(12f, 2f));
                CreateBlock("Outer paddock S", new Vector3(0f, -0.85f, -40f), new Vector3(90f, 0.55f, 12f), Shade(AirsideTheme.DryGrass, 0.65f),
                    "Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png", new Vector2(12f, 1.8f));
                CreateBlock("Outer paddock E", new Vector3(72f, -0.85f, 4f), new Vector3(14f, 0.55f, 60f), Shade(AirsideTheme.Eucalyptus, 0.45f),
                    "Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png", new Vector2(2f, 8f));
                CreateBlock("Outer paddock W", new Vector3(-72f, -0.85f, 4f), new Vector3(14f, 0.55f, 60f), Shade(AirsideTheme.Eucalyptus, 0.45f),
                    "Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png", new Vector2(2f, 8f));
            }
            else
            {
                CreateBlock("Outer paddock N", new Vector3(0f, -0.85f, 48f), new Vector3(140f, 0.8f, 40f), Shade(AirsideTheme.DryGrass, 0.7f),
                    "Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png", new Vector2(18f, 6f));
                CreateBlock("Outer paddock S", new Vector3(0f, -0.85f, -36f), new Vector3(140f, 0.8f, 36f), Shade(AirsideTheme.DryGrass, 0.65f),
                    "Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png", new Vector2(18f, 5f));
                CreateBlock("Outer paddock E", new Vector3(68f, -0.85f, 4f), new Vector3(36f, 0.8f, 90f), Shade(AirsideTheme.Eucalyptus, 0.45f),
                    "Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png", new Vector2(5f, 12f));
                CreateBlock("Outer paddock W", new Vector3(-68f, -0.85f, 4f), new Vector3(36f, 0.8f, 90f), Shade(AirsideTheme.Eucalyptus, 0.45f),
                    "Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png", new Vector2(5f, 12f));
            }

            // Kangaroo Island coastal strip south of the runway (sand, not water physics).
            CreateBlock("Coast sand", new Vector3(0f, -0.55f, -48f), new Vector3(160f, 0.35f, 14f), AirsideTheme.Sand,
                "Textures/Surfaces/tx_sand_coast_basecolor_v01.png", new Vector2(20f, 2f));
            // Surf foam ribbon so the sand/water join reads from overview (0025 item 3).
            CreateBlock("Coast foam", new Vector3(0f, -0.62f, -54.5f), new Vector3(165f, 0.08f, 2.2f),
                new Color(0.88f, 0.92f, 0.95f, 0.85f),
                "Textures/Surfaces/tx_water_coast_basecolor_v01.png", new Vector2(22f, 0.4f));
            CreateBlock("Coast foam inner", new Vector3(0f, -0.58f, -53.2f), new Vector3(150f, 0.05f, 1.1f),
                new Color(0.92f, 0.95f, 0.97f, 0.55f),
                "Textures/Surfaces/tx_water_coast_basecolor_v01.png", new Vector2(18f, 0.25f));
            CreateBlock("Coast foam outer", new Vector3(0f, -0.68f, -56.2f), new Vector3(170f, 0.04f, 1.4f),
                new Color(0.78f, 0.86f, 0.92f, 0.45f),
                "Textures/Surfaces/tx_water_coast_basecolor_v01.png", new Vector2(20f, 0.3f));
            CreateBlock("Coast shallows", new Vector3(0f, -0.9f, -58f), new Vector3(170f, 0.2f, 12f), new Color(0.45f, 0.68f, 0.78f),
                "Textures/Surfaces/tx_water_coast_basecolor_v01.png", new Vector2(16f, 1.5f));
            CreateBlock("Coast water", new Vector3(0f, -1.15f, -72f), new Vector3(180f, 0.15f, 20f), new Color(0.22f, 0.42f, 0.58f),
                "Textures/Surfaces/tx_water_coast_basecolor_v01.png", new Vector2(14f, 2f));
            BuildCoastalLife();

            // Landside access: terminal → car park road + bay.
            CreateBlock("Access road", new Vector3(26f, -0.02f, 38f), new Vector3(6f, 0.1f, 22f), new Color(0.2f, 0.22f, 0.24f),
                "Textures/Surfaces/tx_asphalt_runway_basecolor_v01.png", new Vector2(1f, 4f));
            CreateBlock("Access road turn", new Vector3(38f, -0.02f, 46f), new Vector3(28f, 0.1f, 5.5f), new Color(0.2f, 0.22f, 0.24f),
                "Textures/Surfaces/tx_asphalt_runway_basecolor_v01.png", new Vector2(4f, 1f));
            CreateBlock("Car park", new Vector3(48f, -0.01f, 46f), new Vector3(18f, 0.08f, 12f), new Color(0.28f, 0.3f, 0.32f),
                "Textures/Surfaces/tx_asphalt_runway_basecolor_v01.png", new Vector2(3f, 2f));
            // Bay/stall paint — thin when PRP-003 kerbs already frame the park.
            var hasForecourtKerbs = GameObject.Find("Car park kerb N") != null;
            var bayRows = hasForecourtKerbs ? 2 : 5;
            var stallCols = hasForecourtKerbs ? 3 : 5;
            for (var i = 0; i < bayRows; i++)
            {
                var z = hasForecourtKerbs ? 43f + i * 4.0f : 42f + i * 2.0f;
                CreateBlock($"Bay line {i}", new Vector3(48f, 0.05f, z), new Vector3(14f, 0.02f, 0.08f), Color.white);
            }

            for (var i = 0; i < stallCols; i++)
            {
                var x = hasForecourtKerbs ? 42.5f + i * 5.0f : 41.5f + i * 3.6f;
                CreateBlock($"Stall line {i}", new Vector3(x, 0.05f, 46f), new Vector3(0.08f, 0.02f, 10f), Color.white);
            }

            if (!hasForecourtKerbs)
            {
                CreateBlock("Car park kerb N", new Vector3(48f, 0.12f, 52.2f), new Vector3(18.5f, 0.2f, 0.35f), AirsideTheme.Concrete);
                CreateBlock("Car park kerb S", new Vector3(48f, 0.12f, 39.8f), new Vector3(18.5f, 0.2f, 0.35f), AirsideTheme.Concrete);
            }

            CreateBlock("Access centreline", new Vector3(26f, 0.05f, 38f), new Vector3(0.12f, 0.02f, 18f), new Color(0.95f, 0.85f, 0.2f));
            if (!hasForecourtKerbs)
            {
                CreateBlock("Access edge L", new Vector3(23.1f, 0.05f, 38f), new Vector3(0.1f, 0.02f, 18f), Color.white);
                CreateBlock("Access edge R", new Vector3(28.9f, 0.05f, 38f), new Vector3(0.1f, 0.02f, 18f), Color.white);
            }

            // Turn segment markings so the landside elbow reads as road, not empty asphalt.
            CreateBlock("Access turn centreline", new Vector3(38f, 0.05f, 46f), new Vector3(22f, 0.02f, 0.12f), new Color(0.95f, 0.85f, 0.2f));
            if (!hasForecourtKerbs)
            {
                CreateBlock("Access turn edge N", new Vector3(38f, 0.05f, 48.5f), new Vector3(24f, 0.02f, 0.1f), Color.white);
                CreateBlock("Access turn edge S", new Vector3(38f, 0.05f, 43.5f), new Vector3(24f, 0.02f, 0.1f), Color.white);
            }

            CreateBlock("Access turn shoulder N", new Vector3(38f, -0.01f, 49.4f), new Vector3(24f, 0.06f, 1.0f), Shade(AirsideTheme.Concrete, 0.85f),
                "Textures/Surfaces/tx_concrete_apron_basecolor_v01.png", new Vector2(3f, 0.3f));
            CreateBlock("Access turn shoulder S", new Vector3(38f, -0.01f, 42.6f), new Vector3(24f, 0.06f, 1.0f), Shade(AirsideTheme.Concrete, 0.85f),
                "Textures/Surfaces/tx_concrete_apron_basecolor_v01.png", new Vector2(3f, 0.3f));
            var turnDashes = hasForecourtKerbs ? 3 : 6;
            for (var i = 0; i < turnDashes; i++)
            {
                var x = hasForecourtKerbs ? 30f + i * 6f : 28f + i * 3.5f;
                CreateBlock($"Access turn dash {i}", new Vector3(x, 0.06f, 46f),
                    new Vector3(1.4f, 0.02f, 0.35f), new Color(0.95f, 0.9f, 0.35f));
            }

            CreateBlock("Access road shoulder L", new Vector3(22.2f, -0.01f, 38f), new Vector3(1.2f, 0.06f, 20f), Shade(AirsideTheme.Concrete, 0.85f),
                "Textures/Surfaces/tx_concrete_apron_basecolor_v01.png", new Vector2(0.4f, 3f));
            CreateBlock("Access road shoulder R", new Vector3(29.8f, -0.01f, 38f), new Vector3(1.2f, 0.06f, 20f), Shade(AirsideTheme.Concrete, 0.85f),
                "Textures/Surfaces/tx_concrete_apron_basecolor_v01.png", new Vector2(0.4f, 3f));
            // One zebra when PRP-003 drop-off kerb/bollards already mark the curb.
            CreateBlock("Drop-off zebra", new Vector3(26f, 0.05f, 34.5f), new Vector3(5.5f, 0.02f, 0.35f), Color.white);
            if (!hasForecourtKerbs)
                CreateBlock("Drop-off zebra 2", new Vector3(26f, 0.05f, 33.8f), new Vector3(5.5f, 0.02f, 0.28f), Color.white);
            if (GameObject.Find("Parking sign post") == null)
                CreateBlock("Parking sign post", new Vector3(39.5f, 1.1f, 40.5f), new Vector3(0.12f, 2.2f, 0.12f), new Color(0.45f, 0.46f, 0.48f));
            if (GameObject.Find("Parking sign face") == null)
                CreateBlock("Parking sign face", new Vector3(39.5f, 2.0f, 40.5f), new Vector3(0.08f, 0.7f, 0.9f), AirsideTheme.SafetyYellow);

            // Hangar service lane.
            CreateBlock("Service lane", new Vector3(-20f, -0.02f, 28.5f), new Vector3(18f, 0.08f, 3.2f), new Color(0.24f, 0.26f, 0.28f),
                "Textures/Surfaces/tx_asphalt_runway_basecolor_v01.png", new Vector2(3f, 0.6f));
            if (!hasForecourtKerbs)
            {
                CreateBlock("Service lane centreline", new Vector3(-20f, 0.04f, 28.5f), new Vector3(14f, 0.02f, 0.1f),
                    new Color(0.95f, 0.85f, 0.2f));
                CreateBlock("Service lane edge N", new Vector3(-20f, 0.04f, 29.9f), new Vector3(16f, 0.02f, 0.08f), Color.white);
                CreateBlock("Service lane edge S", new Vector3(-20f, 0.04f, 27.1f), new Vector3(16f, 0.02f, 0.08f), Color.white);
            }

            BuildPerimeterFence();
            BuildApproachLightBars();
            BuildArffRescueShed();
            BuildVegetation();
            BuildTerrainMicroRelief();
            BuildBuildingContactShadows();
            BuildDistantHills();
            BuildHorizonDome();
            BuildLandsideLife();
            BuildApronLife();
        }

        /// <summary>
        /// Soft grass/sand mounds around the airfield so the ground plane reads as
        /// terrain rather than a flat slab (0025 item 3). Presentation only.
        /// </summary>
        private static void BuildTerrainMicroRelief()
        {
            var grass = Shade(AirsideTheme.Eucalyptus, 0.62f);
            var dry = Shade(AirsideTheme.DryGrass, 0.9f);
            var sand = Shade(AirsideTheme.Sand, 0.95f);
            var terrainKit = PreferArtKit("Models/Environment/mdl_kingscote_context_terrain_v01.gltf");
            var hasTerrainKit = !string.IsNullOrEmpty(terrainKit) && ArtGltfLoader.HasKit(terrainKit);

            // North/south berms framing the runway strip — thinner when WLD accents land.
            if (hasTerrainKit)
            {
                CreateBlock("Relief berm N", new Vector3(0f, 0.12f, 30f), new Vector3(55f, 0.35f, 3.2f), grass,
                    "Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png", new Vector2(6f, 1f));
                CreateBlock("Relief berm S", new Vector3(0f, 0.1f, -22f), new Vector3(50f, 0.28f, 3.5f), dry,
                    "Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png", new Vector2(5.5f, 0.9f));
            }
            else
            {
                CreateBlock("Relief berm N", new Vector3(0f, 0.15f, 30f), new Vector3(70f, 0.55f, 4.5f), grass,
                    "Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png", new Vector2(8f, 1.2f));
                CreateBlock("Relief berm S", new Vector3(0f, 0.12f, -22f), new Vector3(64f, 0.45f, 5f), dry,
                    "Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png", new Vector2(7f, 1f));
            }

            // Scattered mounds — skip dense field when WLD-004 paddock/hill accents own the fringe.
            if (!hasTerrainKit)
            {
                var mounds = new[]
                {
                    new Vector3(-36f, 0.2f, 8f),
                    new Vector3(-34f, 0.18f, -8f),
                    new Vector3(36f, 0.22f, 6f),
                    new Vector3(38f, 0.16f, -10f),
                    new Vector3(-26f, 0.25f, 26f),
                    new Vector3(30f, 0.2f, 34f),
                    new Vector3(-12f, 0.14f, -28f),
                    new Vector3(14f, 0.15f, -30f),
                    new Vector3(-40f, 0.2f, 18f),
                    new Vector3(42f, 0.18f, 20f),
                    new Vector3(-22f, 0.12f, -34f),
                    new Vector3(8f, 0.1f, -36f),
                    new Vector3(-48f, 0.22f, 42f),
                    new Vector3(52f, 0.2f, 52f),
                    new Vector3(-55f, 0.18f, -20f),
                    new Vector3(60f, 0.16f, -18f),
                    new Vector3(0f, 0.14f, 56f),
                    new Vector3(-6f, 0.12f, -40f),
                    new Vector3(-50f, 0.18f, 8f),
                    new Vector3(48f, 0.16f, -6f),
                    new Vector3(-18f, 0.14f, 40f),
                    new Vector3(22f, 0.15f, 38f),
                    new Vector3(-32f, 0.12f, -24f),
                    new Vector3(28f, 0.13f, -26f),
                    new Vector3(10f, 0.11f, 48f),
                    new Vector3(-8f, 0.12f, 50f)
                };
                for (var i = 0; i < mounds.Length; i++)
                {
                    var pos = mounds[i];
                    var size = new Vector3(4.5f + (i % 3) * 1.2f, 0.35f + (i % 4) * 0.08f, 3.2f + (i % 2) * 1.1f);
                    var color = i % 3 == 0 ? sand : i % 3 == 1 ? dry : grass;
                    CreateBlock($"Relief mound {i}", pos, size, color,
                        "Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png", new Vector2(1.5f, 1.2f));
                }
            }

            // Coastal dune rise between apron grass and sand strip — skip when WLD-004
            // terrain kit will place authored dune accents (avoid double densify).
            if (!hasTerrainKit)
            {
                CreateBlock("Coast dune L", new Vector3(-28f, 0.35f, -42f), new Vector3(18f, 0.9f, 5f), sand,
                    "Textures/Surfaces/tx_sand_coast_basecolor_v01.png", new Vector2(3f, 1.2f));
                CreateBlock("Coast dune R", new Vector3(24f, 0.3f, -43f), new Vector3(16f, 0.75f, 4.5f), sand,
                    "Textures/Surfaces/tx_sand_coast_basecolor_v01.png", new Vector2(2.8f, 1.1f));
                CreateBlock("Coast dune mid", new Vector3(0f, 0.22f, -41f), new Vector3(22f, 0.55f, 3.5f), Shade(sand, 0.9f),
                    "Textures/Surfaces/tx_sand_coast_basecolor_v01.png", new Vector2(3.5f, 1f));
                CreateBlock("Coast dune L crest", new Vector3(-30f, 0.7f, -43.5f), new Vector3(10f, 0.45f, 2.2f), Shade(sand, 1.05f),
                    "Textures/Surfaces/tx_sand_coast_basecolor_v01.png", new Vector2(2f, 0.8f));
                CreateBlock("Coast dune R crest", new Vector3(26f, 0.55f, -44f), new Vector3(9f, 0.35f, 2f), Shade(sand, 1.02f),
                    "Textures/Surfaces/tx_sand_coast_basecolor_v01.png", new Vector2(1.8f, 0.7f));
                // Soft grass ribbons so the main Grass slab is not a single flat plane.
                CreateBlock("Grass ribbon N", new Vector3(0f, -0.4f, 26f), new Vector3(80f, 0.35f, 8f), Shade(grass, 0.95f),
                    "Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png", new Vector2(10f, 1.5f));
                CreateBlock("Grass ribbon S", new Vector3(0f, -0.42f, -16f), new Vector3(72f, 0.3f, 7f), Shade(dry, 0.92f),
                    "Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png", new Vector2(9f, 1.3f));
                CreateBlock("Grass ribbon W", new Vector3(-40f, -0.38f, 4f), new Vector3(10f, 0.32f, 40f), Shade(grass, 0.88f),
                    "Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png", new Vector2(2f, 6f));
                CreateBlock("Grass ribbon E", new Vector3(40f, -0.38f, 4f), new Vector3(10f, 0.32f, 40f), Shade(dry, 0.9f),
                    "Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png", new Vector2(2f, 6f));
            }
        }

        /// <summary>
        /// Stylised staff / passenger silhouettes — readable life, not characters.
        /// </summary>
        private static void BuildApronLife()
        {
            var root = new GameObject("Apron life").transform;
            var crewKit = PreferArtKit(
                "Models/Characters/mdl_ramp_crew_kit_v02.gltf",
                "Models/Characters/mdl_ramp_crew_kit_v01.gltf");
            var paxKit = PreferArtKit(
                "Models/Characters/mdl_passenger_kit_v02.gltf",
                "Models/Characters/mdl_passenger_kit_v01.gltf");
            var hasChrKits = (!string.IsNullOrEmpty(crewKit) && ArtGltfLoader.HasKit(crewKit))
                             || (!string.IsNullOrEmpty(paxKit) && ArtGltfLoader.HasKit(paxKit));

            // Hero set always — marshallers, fuel, baggage, hangar, gate, a few passengers.
            PlacePerson(root, "Marshaller", new Vector3(14.5f, 0f, 16.5f), 200f, new Color(0.85f, 0.55f, 0.12f),
                hiVis: true, marshallerWand: true);
            PlacePerson(root, "Fueler", new Vector3(-3.2f, 0f, 13.2f), 90f, new Color(0.2f, 0.35f, 0.55f), hiVis: true);
            PlacePerson(root, "Hangar tech", new Vector3(-16f, 0f, 18.5f), 270f, new Color(0.35f, 0.4f, 0.45f));
            PlacePerson(root, "Landside passenger A", new Vector3(26.5f, 0f, 31.8f), 180f, new Color(0.45f, 0.22f, 0.2f));
            PlacePerson(root, "Landside passenger B", new Vector3(27.8f, 0f, 31.6f), 175f, new Color(0.2f, 0.35f, 0.4f));
            PlacePerson(root, "Gate attendant", new Vector3(24.2f, 0f, 30.8f), 200f, new Color(0.55f, 0.58f, 0.62f));
            PlacePerson(root, "Stand 2 marshaller", new Vector3(22.5f, 0f, 16.8f), 185f, new Color(0.9f, 0.5f, 0.1f),
                hiVis: true, marshallerWand: true);
            PlacePerson(root, "Baggage handler", new Vector3(20.5f, 0f, 19.5f), 250f, new Color(0.3f, 0.45f, 0.55f), hiVis: true);
            PlacePerson(root, "Bench sitter", new Vector3(29.5f, 0.15f, 31.5f), 0f, new Color(0.35f, 0.3f, 0.28f), seated: true);

            if (hasChrKits)
            {
                // One calm walker so life still moves without a CHR carpet.
                PlacePerson(root, "Ramp walker", new Vector3(18.5f, 0f, 14.2f), 95f, new Color(0.55f, 0.35f, 0.18f), hiVis: true);
                return;
            }

            PlacePerson(root, "Ops walker", new Vector3(22f, 0f, 22.5f), 15f, new Color(0.25f, 0.28f, 0.32f));
            PlacePerson(root, "Ramp walker", new Vector3(18.5f, 0f, 14.2f), 95f, new Color(0.55f, 0.35f, 0.18f), hiVis: true);
            PlacePerson(root, "Hangar tech B", new Vector3(-18.5f, 0f, 17.2f), 200f, new Color(0.4f, 0.42f, 0.38f));
            PlacePerson(root, "Landside passenger C", new Vector3(25.6f, 0f, 31.4f), 190f, new Color(0.3f, 0.32f, 0.45f));
            PlacePerson(root, "Landside passenger D", new Vector3(28.5f, 0f, 32.2f), 160f, new Color(0.5f, 0.45f, 0.35f));
            PlacePerson(root, "Car park walker", new Vector3(34f, 0f, 34f), 220f, new Color(0.4f, 0.25f, 0.3f));
            PlacePerson(root, "Fuel pad walker", new Vector3(-32f, 0f, 20.5f), 110f, new Color(0.55f, 0.4f, 0.2f), hiVis: true);
            PlacePerson(root, "Stairs attendant", new Vector3(16.8f, 0f, 18.2f), 170f, new Color(0.6f, 0.35f, 0.25f), hiVis: true);
            PlacePerson(root, "Ops walker B", new Vector3(-6f, 0f, 24.5f), 40f, new Color(0.28f, 0.3f, 0.35f));
            PlacePerson(root, "Car park walker B", new Vector3(44f, 0f, 42f), 280f, new Color(0.35f, 0.4f, 0.45f));
            PlacePerson(root, "Landside passenger E", new Vector3(24.8f, 0f, 32.5f), 150f, new Color(0.55f, 0.3f, 0.35f));
            PlacePerson(root, "Bench sitter B", new Vector3(30.8f, 0.15f, 31.2f), 10f, new Color(0.25f, 0.35f, 0.4f), seated: true);
        }

        private static void PlacePerson(
            Transform parent,
            string name,
            Vector3 position,
            float yaw,
            Color clothes,
            bool seated = false,
            bool hiVis = false,
            bool marshallerWand = false)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.position = position;
            root.rotation = Quaternion.Euler(0f, yaw, 0f);

            // Batch F2 CHR-001/002 — prefer authored kit parts; keep torso/wand/arm/leg names
            // so UpdateApronLife idle/wave animation still finds them.
            if (TryPlaceCharacterFromKit(root, name, clothes, seated, hiVis, marshallerWand))
                return;

            var bodyH = seated ? 0.55f : 0.85f;
            var bodyY = seated ? 0.55f : 0.9f;
            var torsoColor = hiVis ? new Color(0.95f, 0.72f, 0.12f) : clothes;
            ParentBlock(root, $"{name} torso", new Vector3(0f, bodyY, 0f), new Vector3(0.38f, bodyH, 0.22f), torsoColor);
            if (hiVis)
            {
                ParentBlock(root, $"{name} vest stripe", new Vector3(0f, bodyY + 0.05f, 0.12f),
                    new Vector3(0.36f, 0.12f, 0.04f), new Color(0.95f, 0.95f, 0.9f));
                ParentBlock(root, $"{name} hard hat", new Vector3(0f, bodyY + bodyH * 0.55f + 0.32f, 0f),
                    new Vector3(0.26f, 0.12f, 0.28f), new Color(0.95f, 0.78f, 0.15f));
            }

            ParentBlock(root, $"{name} head", new Vector3(0f, bodyY + bodyH * 0.55f + 0.18f, 0f), new Vector3(0.22f, 0.22f, 0.22f),
                new Color(0.78f, 0.62f, 0.5f));
            if (!seated)
            {
                ParentBlock(root, $"{name} leg L", new Vector3(-0.1f, 0.35f, 0f), new Vector3(0.14f, 0.7f, 0.14f), Shade(clothes, 0.7f));
                ParentBlock(root, $"{name} leg R", new Vector3(0.1f, 0.35f, 0f), new Vector3(0.14f, 0.7f, 0.14f), Shade(clothes, 0.7f));
                ParentBlock(root, $"{name} arm L", new Vector3(-0.28f, bodyY + 0.05f, 0f), new Vector3(0.12f, 0.55f, 0.12f), Shade(torsoColor, 0.85f));
                ParentBlock(root, $"{name} arm R", new Vector3(0.28f, bodyY + 0.05f, 0f), new Vector3(0.12f, 0.55f, 0.12f), Shade(torsoColor, 0.85f));
                if (marshallerWand)
                {
                    ParentBlock(root, $"{name} wand", new Vector3(0.42f, bodyY + 0.35f, 0.05f),
                        new Vector3(0.05f, 0.55f, 0.05f), new Color(0.95f, 0.2f, 0.15f));
                    ParentBlock(root, $"{name} wand tip", new Vector3(0.42f, bodyY + 0.65f, 0.05f),
                        new Vector3(0.08f, 0.08f, 0.08f), new Color(1f, 0.85f, 0.2f));
                }
            }
            else
            {
                ParentBlock(root, $"{name} legs", new Vector3(0f, 0.28f, 0.2f), new Vector3(0.4f, 0.2f, 0.55f), Shade(clothes, 0.7f));
            }
        }

        /// <summary>
        /// Batch F2 — place CHR kit meshes renamed for UpdateApronLife heuristics.
        /// Crew roles map to marshaller/fueler/ramp; passengers cycle stand/walk/sit variants.
        /// </summary>
        private static bool TryPlaceCharacterFromKit(
            Transform root,
            string name,
            Color clothes,
            bool seated,
            bool hiVis,
            bool marshallerWand)
        {
            string prefix;
            string kitPath;
            var lower = name.ToLowerInvariant();
            if (lower.Contains("marshaller"))
            {
                prefix = "marshaller";
                kitPath = PreferArtKit(
                "Models/Characters/mdl_ramp_crew_kit_v02.gltf",
                "Models/Characters/mdl_ramp_crew_kit_v01.gltf");
            }
            else if (lower.Contains("fueler") || lower.Contains("baggage") || lower.Contains("stairs")
                     || lower.Contains("ramp") || (hiVis && !seated))
            {
                prefix = lower.Contains("fueler") ? "fueler" : "ramp";
                kitPath = PreferArtKit(
                "Models/Characters/mdl_ramp_crew_kit_v02.gltf",
                "Models/Characters/mdl_ramp_crew_kit_v01.gltf");
            }
            else
            {
                kitPath = PreferArtKit(
                "Models/Characters/mdl_passenger_kit_v02.gltf",
                "Models/Characters/mdl_passenger_kit_v01.gltf");
                if (seated)
                    prefix = lower.Contains("sitter b") || lower.GetHashCode() % 2 == 0 ? "sit_f" : "sit_e";
                else if (lower.Contains("walker"))
                    prefix = Math.Abs(name.GetHashCode()) % 2 == 0 ? "walk_c" : "walk_d";
                else
                    prefix = Math.Abs(name.GetHashCode()) % 2 == 0 ? "stand_a" : "stand_b";
            }

            if (string.IsNullOrEmpty(kitPath) || !ArtGltfLoader.HasKit(kitPath))
                return false;

            var torsoColor = hiVis ? new Color(0.95f, 0.72f, 0.12f) : clothes;
            var skin = new Color(0.78f, 0.62f, 0.5f);
            var placed = 0;

            void Place(string mesh, string displayName, Color color)
            {
                if (!ArtGltfLoader.TryPlaceNamedMesh(kitPath, mesh, Vector3.zero, Quaternion.identity, color, out var part))
                    return;
                part.SetParent(root, false);
                part.localPosition = Vector3.zero;
                part.name = displayName;
                placed++;
            }

            Place($"{prefix}_torso", $"{name} torso", torsoColor);
            Place($"{prefix}_head", $"{name} head", skin);
            if (hiVis || prefix is "marshaller" or "fueler" or "ramp")
            {
                Place($"{prefix}_vest", $"{name} vest stripe", new Color(0.95f, 0.95f, 0.9f));
                Place($"{prefix}_hat", $"{name} hard hat", new Color(0.95f, 0.78f, 0.15f));
            }

            if (seated || prefix.StartsWith("sit_", StringComparison.Ordinal))
            {
                Place($"{prefix}_legs", $"{name} legs", Shade(clothes, 0.7f));
                Place($"{prefix}_arm_l", $"{name} arm L", Shade(torsoColor, 0.85f));
                Place($"{prefix}_arm_r", $"{name} arm R", Shade(torsoColor, 0.85f));
            }
            else
            {
                Place($"{prefix}_leg_l", $"{name} leg L", Shade(clothes, 0.7f));
                Place($"{prefix}_leg_r", $"{name} leg R", Shade(clothes, 0.7f));
                Place($"{prefix}_arm_l", $"{name} arm L", Shade(torsoColor, 0.85f));
                Place($"{prefix}_arm_r", $"{name} arm R", Shade(torsoColor, 0.85f));
                Place($"{prefix}_shoe_l", $"{name} shoe L", new Color(0.15f, 0.15f, 0.16f));
                Place($"{prefix}_shoe_r", $"{name} shoe R", new Color(0.15f, 0.15f, 0.16f));
            }

            if (marshallerWand || prefix == "marshaller")
            {
                Place($"{prefix}_wand", $"{name} wand", new Color(0.95f, 0.2f, 0.15f));
                Place($"{prefix}_wand_tip", $"{name} wand tip", new Color(1f, 0.85f, 0.2f));
            }

            return placed >= 3;
        }

        private void UpdateApronLife()
        {
            if (_apronLifeRoot == null)
            {
                var found = GameObject.Find("Apron life");
                if (found != null)
                    _apronLifeRoot = found.transform;
            }

            if (_apronLifeRoot == null)
                return;

            if (_apronPeople.Count == 0)
            {
                for (var i = 0; i < _apronLifeRoot.childCount; i++)
                {
                    var person = _apronLifeRoot.GetChild(i);
                    var walker = person.name.IndexOf("walker", StringComparison.OrdinalIgnoreCase) >= 0;
                    _apronPeople.Add((person, person.position, walker));
                }
            }

            // Soft idle lean on torsos so figures don't read as frozen props.
            for (var i = 0; i < _apronPeople.Count; i++)
            {
                var (person, basePos, walker) = _apronPeople[i];
                if (person == null)
                    continue;
                if (person.name.IndexOf("sitter", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                var wave = false;
                if (person.name.IndexOf("marshaller", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    foreach (var flight in _simulation.Flights)
                    {
                        if (flight.Operation.Phase is AircraftPhase.Approach or AircraftPhase.Landing or AircraftPhase.TaxiIn)
                        {
                            wave = true;
                            break;
                        }
                    }
                }

                // Shuffle walkers around their spawn; other standing figures get a tiny idle sway.
                if (walker)
                {
                    var ox = Mathf.Sin(Time.unscaledTime * AirsideReusableMotion.ApronWalkerHz * Mathf.PI * 2f + i) * 1.6f;
                    var oz = Mathf.Cos(Time.unscaledTime * AirsideReusableMotion.ApronWalkerHz * Mathf.PI * 2f * 0.8f + i * 0.7f) * 1.0f;
                    person.position = new Vector3(basePos.x + ox, basePos.y, basePos.z + oz);
                    var look = new Vector3(-oz, 0f, ox);
                    if (look.sqrMagnitude > 0.0001f)
                        person.rotation = Quaternion.Slerp(person.rotation, Quaternion.LookRotation(look.normalized),
                            Time.unscaledDeltaTime * 2f);
                }
                else
                {
                    var sway = Mathf.Sin(Time.unscaledTime * AirsideReusableMotion.ApronIdleSwayHz * Mathf.PI * 2f + i * 0.9f) * 0.08f;
                    person.position = new Vector3(basePos.x + sway, basePos.y, basePos.z);
                }

                foreach (var child in person.GetComponentsInChildren<Transform>(true))
                {
                    if (child == person)
                        continue;
                    if (child.name.IndexOf("torso", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var lean = Mathf.Sin(Time.unscaledTime * AirsideReusableMotion.ApronIdleSwayHz * Mathf.PI * 2f * 1.6f + i * 1.3f) * 4f;
                        if (wave)
                            lean += Mathf.Sin(Time.unscaledTime * AirsideReusableMotion.ApronWaveHz * Mathf.PI * 2f) * 16f;
                        child.localEulerAngles = new Vector3(0f, 0f, lean);
                    }
                    else if (wave && child.name.IndexOf("wand", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var tip = Mathf.Sin(Time.unscaledTime * AirsideReusableMotion.ApronWaveHz * Mathf.PI * 2f * 1.5f) * 28f;
                        child.localEulerAngles = new Vector3(tip, 0f, 12f);
                    }
                    else if (wave && child.name.IndexOf("arm", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var swing = Mathf.Sin(Time.unscaledTime * AirsideReusableMotion.ApronWaveHz * Mathf.PI * 2f * 1.25f
                            + (child.name.Contains("L") ? 0f : 1.2f)) * 35f;
                        child.localEulerAngles = new Vector3(swing, 0f, child.name.Contains("L") ? -12f : 12f);
                    }
                    else if (walker && child.name.IndexOf("leg", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var stride = Mathf.Sin(Time.unscaledTime * AirsideReusableMotion.ApronStrideHz
                            + (child.name.Contains("L") ? 0f : 3.14f)) * 18f;
                        child.localEulerAngles = new Vector3(stride, 0f, 0f);
                    }
                }
            }
        }

        /// <summary>
        /// Jetty + fishing boat silhouettes on the KI coast so the southern edge
        /// reads as a shoreline with life (presentation only).
        /// </summary>
        private static void BuildCoastalLife()
        {
            // Timber jetty — restrained silhouette so kit coast/dunes stay the hero read.
            var timber = new Color(0.45f, 0.32f, 0.18f);
            var pile = new Color(0.35f, 0.26f, 0.16f);
            CreateBlock("Jetty deck", new Vector3(-18f, -0.15f, -52f), new Vector3(2.4f, 0.18f, 12f), timber);
            for (var plank = 0; plank < 4; plank++)
            {
                var z = -47f - plank * 2.2f;
                CreateBlock($"Jetty plank {plank}", new Vector3(-18f, -0.04f, z), new Vector3(2.2f, 0.05f, 1.6f), Shade(timber, 0.92f + (plank % 2) * 0.04f));
            }

            for (var i = 0; i < 3; i++)
            {
                var z = -47f - i * 3.5f;
                CreateBlock($"Jetty pile L {i}", new Vector3(-19f, -0.55f, z), new Vector3(0.28f, 0.9f, 0.28f), pile);
                CreateBlock($"Jetty pile R {i}", new Vector3(-17f, -0.55f, z), new Vector3(0.28f, 0.9f, 0.28f), pile);
            }

            CreateBlock("Jetty rail L", new Vector3(-19.1f, 0.35f, -52f), new Vector3(0.08f, 0.7f, 10f), Shade(timber, 0.85f));
            CreateBlock("Jetty rail R", new Vector3(-16.9f, 0.35f, -52f), new Vector3(0.08f, 0.7f, 10f), Shade(timber, 0.85f));
            CreateBlock("Jetty bollard A", new Vector3(-19.0f, 0.25f, -46.5f), new Vector3(0.22f, 0.55f, 0.22f), new Color(0.25f, 0.26f, 0.28f));
            CreateBlock("Jetty bollard B", new Vector3(-17.0f, 0.25f, -46.5f), new Vector3(0.22f, 0.55f, 0.22f), new Color(0.25f, 0.26f, 0.28f));
            CreateBlock("Jetty end cap", new Vector3(-18f, -0.05f, -58f), new Vector3(2.5f, 0.2f, 0.35f), Shade(timber, 0.8f));
            CreateBlock("Jetty approach", new Vector3(-18f, -0.12f, -44.5f), new Vector3(3.2f, 0.14f, 2.2f), Shade(AirsideTheme.Concrete, 0.9f));

            // Prefab boats are heavy silhouettes — three near the jetty; six only as greybox fallback.
            var hasBoatPrefab = ArtPresentationLoader.HasPrefab("mdl_coast_boat_v01");
            PlaceCoastBoat("Coast boat A", new Vector3(-12f, -0.55f, -62f), 12f, new Color(0.85f, 0.88f, 0.9f));
            PlaceCoastBoat("Coast boat B", new Vector3(22f, -0.5f, -68f), -20f, new Color(0.75f, 0.35f, 0.22f));
            PlaceCoastBoat("Coast boat C", new Vector3(8f, -0.48f, -78f), 28f, new Color(0.92f, 0.9f, 0.82f));
            if (!hasBoatPrefab)
            {
                PlaceCoastBoat("Coast boat D", new Vector3(55f, -0.45f, -74f), 5f, new Color(0.2f, 0.35f, 0.45f));
                PlaceCoastBoat("Coast boat E", new Vector3(-40f, -0.5f, -70f), -8f, new Color(0.55f, 0.2f, 0.18f));
                PlaceCoastBoat("Coast boat F", new Vector3(-58f, -0.52f, -66f), -15f, new Color(0.15f, 0.28f, 0.22f));
            }

            // Rock outcrops along the sand — prefer VEG-002 scrub kit rocks.
            var rock = new Color(0.42f, 0.4f, 0.38f);
            PlaceCoastRock("Coast rock A", new Vector3(-28f, -0.25f, -50f), "rock_a", rock, 4.2f, 18f);
            PlaceCoastRock("Coast rock B", new Vector3(18f, -0.2f, -49f), "rock_b", Shade(rock, 0.9f), 3.6f, -12f);
            PlaceCoastRock("Coast rock C", new Vector3(42f, -0.3f, -51.5f), "rock_a", Shade(rock, 1.1f), 5.0f, 40f);
        }

        /// <summary>VEG-002 rock accents on the KI shoreline; cube blocks remain fallback.</summary>
        private static void PlaceCoastRock(string name, Vector3 position, string mesh, Color color, float scale, float yawDegrees)
        {
            var kit = PreferArtKit("Models/Environment/mdl_kingscote_scrub_kit_v01.gltf");
            if (!string.IsNullOrEmpty(kit)
                && ArtGltfLoader.TryPlaceNamedMesh(
                    kit, mesh, position, Quaternion.Euler(0f, yawDegrees, 0f), color, out var part,
                    localScale: Vector3.one * scale))
            {
                part.name = name;
                return;
            }

            var size = mesh == "rock_b"
                ? new Vector3(2.2f, 0.7f, 1.8f)
                : new Vector3(2.8f, 0.9f, 2.2f);
            if (scale > 4.5f)
                size = new Vector3(3.4f, 1.1f, 2.6f);
            CreateBlock(name, position, size, color);
        }

        /// <summary>
        /// Decision 0025 items 1+3 — Resources coast boat with procedural fallback.
        /// </summary>
        private static void PlaceCoastBoat(string name, Vector3 position, float yawDegrees, Color hull)
        {
            Transform root;
            if (ArtPresentationLoader.TryInstantiatePrefab("mdl_coast_boat_v01", out var prefabRoot))
            {
                prefabRoot.name = name;
                root = prefabRoot;
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer == null)
                        continue;
                    var n = renderer.gameObject.name.ToLowerInvariant();
                    if (n.Contains("window"))
                        renderer.material = AirsideMaterialLibrary.Create(
                            new Color(0.35f, 0.55f, 0.7f, 0.55f), AirsideMaterialLibrary.SurfaceKind.Glass);
                    else if (n.Contains("stripe"))
                        SetRendererColor(renderer, new Color(0.2f, 0.45f, 0.65f));
                    else if (n.Contains("mast") || n.Contains("boom") || n.Contains("rail") || n.Contains("cleat"))
                        SetRendererColor(renderer, new Color(0.75f, 0.75f, 0.72f));
                    else if (n.Contains("outboard"))
                        SetRendererColor(renderer, new Color(0.2f, 0.22f, 0.25f));
                    else if (n.Contains("hull") || n.Contains("bow") || n.Contains("gunwale") || n.Contains("transom")
                             || n.Contains("roof") || n.Contains("cabin"))
                        SetRendererColor(renderer, n.Contains("roof") || n.Contains("cabin")
                            ? Shade(hull, 0.85f) : hull);
                }
            }
            else
            {
                root = new GameObject(name).transform;
                ParentBlock(root, $"{name} hull", Vector3.zero, new Vector3(1.4f, 0.55f, 4.2f), hull);
                ParentBlock(root, $"{name} gunwale", new Vector3(0f, 0.28f, 0.05f), new Vector3(1.48f, 0.08f, 3.9f), Shade(hull, 1.08f));
                ParentBlock(root, $"{name} bow", new Vector3(0f, 0.12f, 2.05f), new Vector3(1.05f, 0.4f, 0.7f), Shade(hull, 0.95f));
                ParentBlock(root, $"{name} transom", new Vector3(0f, 0.18f, -2.05f), new Vector3(1.25f, 0.45f, 0.18f), Shade(hull, 0.9f));
                ParentBlock(root, $"{name} roof", new Vector3(0f, 0.45f, -0.4f), new Vector3(1.1f, 0.7f, 1.6f), Shade(hull, 0.85f));
                ParentBlock(root, $"{name} window", new Vector3(0f, 0.55f, 0.15f), new Vector3(1.0f, 0.35f, 0.08f),
                    new Color(0.35f, 0.55f, 0.7f, 0.55f));
                ParentBlock(root, $"{name} mast", new Vector3(0f, 1.4f, 0.2f), new Vector3(0.1f, 2.2f, 0.1f), new Color(0.75f, 0.75f, 0.72f));
                ParentBlock(root, $"{name} boom", new Vector3(0f, 0.95f, -0.35f), new Vector3(0.08f, 0.08f, 1.6f), new Color(0.7f, 0.7f, 0.68f));
                ParentBlock(root, $"{name} cabin door", new Vector3(0.45f, 0.4f, -0.55f), new Vector3(0.08f, 0.45f, 0.35f), Shade(hull, 0.7f));
                ParentBlock(root, $"{name} rail L", new Vector3(-0.72f, 0.42f, 0.2f), new Vector3(0.05f, 0.08f, 3.2f), new Color(0.8f, 0.8f, 0.78f));
                ParentBlock(root, $"{name} rail R", new Vector3(0.72f, 0.42f, 0.2f), new Vector3(0.05f, 0.08f, 3.2f), new Color(0.8f, 0.8f, 0.78f));
                ParentBlock(root, $"{name} outboard", new Vector3(0f, 0.05f, -2.35f), new Vector3(0.35f, 0.45f, 0.55f), new Color(0.2f, 0.22f, 0.25f));
                ParentBlock(root, $"{name} stripe", new Vector3(0f, 0.15f, 0.0f), new Vector3(1.42f, 0.08f, 3.6f), new Color(0.2f, 0.45f, 0.65f));
            }

            root.position = position;
            root.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
        }

        /// <summary>
        /// Parked cars, kerbside drop-off and a few landside props so the terminal
        /// approach reads as a working regional airfield (presentation only).
        /// </summary>
        private static void BuildLandsideLife()
        {
            var carColors = new[]
            {
                new Color(0.75f, 0.22f, 0.18f),
                new Color(0.92f, 0.92f, 0.9f),
                new Color(0.15f, 0.18f, 0.22f),
                new Color(0.2f, 0.35f, 0.55f),
                new Color(0.85f, 0.7f, 0.25f),
                new Color(0.35f, 0.4f, 0.38f),
                new Color(0.55f, 0.55f, 0.58f),
                new Color(0.12f, 0.45f, 0.35f)
            };

            // Car park bays — thin when parked-car prefab/kit is a heavy silhouette.
            var hasCarPrefab = ArtPresentationLoader.HasPresentation("Models/Vehicles/mdl_parked_car_v02.gltf")
                               || ArtPresentationLoader.HasPrefab("mdl_parked_car_v02")
                               || ArtPresentationLoader.HasPrefab("mdl_parked_car_v01");
            var bayCarCount = hasCarPrefab ? 4 : 8;
            for (var i = 0; i < bayCarCount; i++)
            {
                if (hasCarPrefab)
                {
                    // Single calm row facing the terminal.
                    PlaceParkedCar($"Parked car {i}", new Vector3(42f + i * 3.6f, 0f, 43.2f), 90f, carColors[i]);
                }
                else
                {
                    var row = i < 4 ? 0 : 1;
                    var slot = i % 4;
                    PlaceParkedCar($"Parked car {i}", new Vector3(42f + slot * 3.6f, 0f, 43.2f + row * 4.2f), 90f, carColors[i]);
                }
            }

            // Kerbside drop-off on the access road.
            PlaceParkedCar("Drop-off car", new Vector3(23.5f, 0f, 36f), 0f, carColors[2]);
            PlaceParkedCar("Taxi wait", new Vector3(28.5f, 0f, 36.5f), 8f, new Color(0.92f, 0.78f, 0.15f));

            // Landside furniture: luggage trolley cluster + bench near terminal doors.
            PlaceLuggageTrolley("Luggage trolley A", new Vector3(24f, 0f, 31.5f), -15f);
            PlaceLuggageTrolley("Luggage trolley B", new Vector3(25.2f, 0f, 31.5f), 8f);
            if (!hasCarPrefab)
                PlaceLuggageTrolley("Luggage trolley C", new Vector3(24.6f, 0f, 30.6f), 175f);
            PlaceLandsideBench("Landside bench", new Vector3(29.5f, 0f, 31.2f), 0f);
            PlaceLandsideBench("Car park bench", new Vector3(40.5f, 0f, 40.2f), 90f);

            // Extra trees framing the car park.
            PlaceTree(new Vector3(58f, 0f, 48f), 1.1f);
            PlaceTree(new Vector3(44f, 0f, 54f), 0.95f);
            PlaceTree(new Vector3(20f, 0f, 44f), 0.85f);
            PlaceTree(new Vector3(56f, 0f, 40f), 0.9f);
            PlaceTree(new Vector3(34f, 0f, 52f), 1.05f);
            if (!hasCarPrefab)
            {
                PlaceTree(new Vector3(52f, 0f, 56f), 0.88f);
                PlaceTree(new Vector3(38f, 0f, 56f), 1.0f);
            }

            // Overflow bay row — one hero ute + visitor when prefab cars are present.
            if (hasCarPrefab)
            {
                PlaceParkedCar("Staff ute", new Vector3(49.2f, 0f, 51.2f), 90f, new Color(0.55f, 0.55f, 0.22f));
                PlaceParkedCar("Visitor car", new Vector3(38.5f, 0f, 47.5f), 0f, new Color(0.6f, 0.15f, 0.2f));
            }
            else
            {
                PlaceParkedCar("Overflow car A", new Vector3(42f, 0f, 51.2f), 90f, new Color(0.45f, 0.2f, 0.18f));
                PlaceParkedCar("Overflow car B", new Vector3(45.6f, 0f, 51.2f), 90f, new Color(0.7f, 0.72f, 0.75f));
                PlaceParkedCar("Staff ute", new Vector3(49.2f, 0f, 51.2f), 90f, new Color(0.55f, 0.55f, 0.22f));
                PlaceParkedCar("Overflow car C", new Vector3(52.8f, 0f, 51.2f), 90f, new Color(0.25f, 0.3f, 0.45f));
                PlaceParkedCar("Visitor car", new Vector3(38.5f, 0f, 47.5f), 0f, new Color(0.6f, 0.15f, 0.2f));
                PlaceLuggageTrolley("Luggage trolley D", new Vector3(23.4f, 0f, 30.2f), 40f);
            }

            PlaceLandsideBench("Access bench", new Vector3(22f, 0f, 40.5f), 90f);

            // Painted parking bay chevrons — thin when PRP-003 kerbs already frame the park.
            var hasForecourtKerbs = GameObject.Find("Car park kerb N") != null;
            var bayCount = hasForecourtKerbs ? 2 : 4;
            for (var bay = 0; bay < bayCount; bay++)
            {
                var x = hasForecourtKerbs ? 43.5f + bay * 5.5f : 42f + bay * 3.6f;
                CreateBlock($"Bay line L {bay}", new Vector3(x - 1.5f, 0.06f, 43.2f), new Vector3(0.08f, 0.02f, 3.4f),
                    new Color(0.92f, 0.92f, 0.88f));
                CreateBlock($"Bay line R {bay}", new Vector3(x + 1.5f, 0.06f, 43.2f), new Vector3(0.08f, 0.02f, 3.4f),
                    new Color(0.92f, 0.92f, 0.88f));
                CreateBlock($"Bay stop {bay}", new Vector3(x, 0.06f, 41.6f), new Vector3(2.8f, 0.02f, 0.08f),
                    new Color(0.92f, 0.92f, 0.88f));
                if (!hasForecourtKerbs)
                {
                    // Overflow row chevrons under the north kerb cars.
                    CreateBlock($"Overflow bay L {bay}", new Vector3(x - 1.5f, 0.06f, 51.2f), new Vector3(0.08f, 0.02f, 3.0f),
                        new Color(0.92f, 0.92f, 0.88f));
                    CreateBlock($"Overflow bay R {bay}", new Vector3(x + 1.5f, 0.06f, 51.2f), new Vector3(0.08f, 0.02f, 3.0f),
                        new Color(0.92f, 0.92f, 0.88f));
                    CreateBlock($"Overflow stop {bay}", new Vector3(x, 0.06f, 52.6f), new Vector3(2.8f, 0.02f, 0.08f),
                        new Color(0.92f, 0.92f, 0.88f));
                }
            }

            // Access-road centre dashes toward the terminal (0025 item 3).
            var dashCount = hasForecourtKerbs ? 4 : 8;
            for (var i = 0; i < dashCount; i++)
            {
                var z = hasForecourtKerbs ? 38.5f + i * 3.2f : 38f + i * 1.8f;
                CreateBlock($"Access dash {i}", new Vector3(26f, 0.06f, z),
                    new Vector3(0.35f, 0.02f, 0.9f), new Color(0.95f, 0.9f, 0.35f));
            }

            // Small general-aviation tie-down markers west of hangar (life, not sim).
            var tieCount = 3;
            for (var i = 0; i < tieCount; i++)
            {
                CreateBlock($"Tie-down {i}", new Vector3(-30f - i * 5.5f, 0.08f, 14f), new Vector3(0.35f, 0.08f, 0.35f),
                    new Color(0.55f, 0.55f, 0.5f));
                CreateBlock($"Tie-down rope {i}", new Vector3(-30f - i * 5.5f, 0.04f, 14.55f),
                    new Vector3(0.06f, 0.04f, 0.9f), new Color(0.35f, 0.35f, 0.32f));
            }
        }

        /// <summary>
        /// Prefer landside car v02 kit/prefab, then v01, then procedural cuboids.
        /// </summary>
        private static void PlaceParkedCar(string name, Vector3 position, float yawDegrees, Color body)
        {
            Transform root = null;
            var kit = PreferArtKit(
                "Models/Vehicles/mdl_parked_car_v02.gltf",
                "Models/Vehicles/mdl_parked_car_v01.gltf");
            if (!string.IsNullOrEmpty(kit)
                && ArtPresentationLoader.TryInstantiate(
                    kit,
                    null,
                    out root,
                    kitName => kitName switch
                    {
                        "car_body" => $"{name} body",
                        "car_roof" => $"{name} roof",
                        "car_hood" => $"{name} hood",
                        "car_boot" => $"{name} boot",
                        "glass_front" => $"{name} glass front",
                        "glass_rear" => $"{name} glass rear",
                        "glass_side_l" => $"{name} glass side L",
                        "glass_side_r" => $"{name} glass side R",
                        "wheel_fl" => $"{name} wheel FL",
                        "wheel_fr" => $"{name} wheel FR",
                        "wheel_rl" => $"{name} wheel RL",
                        "wheel_rr" => $"{name} wheel RR",
                        _ => $"{name} {kitName}"
                    },
                    kitName =>
                    {
                        if (kitName.StartsWith("glass", StringComparison.Ordinal)
                            || kitName.Contains("window", StringComparison.Ordinal))
                            return new Color(0.18f, 0.35f, 0.48f, 0.42f);
                        return kitName switch
                        {
                            "wheel_fl" or "wheel_fr" or "wheel_rl" or "wheel_rr" => new Color(0.12f, 0.12f, 0.13f),
                            "hub_fl" or "hub_fr" or "hub_rl" or "hub_rr" => new Color(0.45f, 0.46f, 0.48f),
                            "car_headlight_l" or "car_headlight_r" => new Color(0.95f, 0.95f, 0.85f),
                            "car_taillight_l" or "car_taillight_r" => new Color(0.85f, 0.15f, 0.12f),
                            "car_stripe" => new Color(0.85f, 0.85f, 0.88f),
                            "car_grille" or "car_grille_bar_1" or "car_grille_bar_2"
                                or "car_bumper_front" or "car_bumper_rear"
                                or "car_wheel_arch_fl" or "car_wheel_arch_fr"
                                or "car_wheel_arch_rl" or "car_wheel_arch_rr"
                                or "car_skirt_l" or "car_skirt_r" => Shade(body, 0.7f),
                            "car_roof" => Shade(body, 0.85f),
                            "car_mirror_l" or "car_mirror_r" or "car_number_plate"
                                or "wiper" or "antenna" or "door_handle_l" or "door_handle_r"
                                or "window_mullion_a" or "window_mullion_b"
                                or "window_sill_l" or "window_sill_r" => new Color(0.25f, 0.26f, 0.28f),
                            _ => body
                        };
                    }))
            {
                root.name = name;
            }
            else if (ArtPresentationLoader.TryInstantiatePrefab("mdl_parked_car_v02", out var prefabRoot)
                     || ArtPresentationLoader.TryInstantiatePrefab("mdl_parked_car_v01", out prefabRoot))
            {
                prefabRoot.name = name;
                root = prefabRoot;
                TintParkedCarBody(root, body);
            }
            else
            {
                root = new GameObject(name).transform;
                ParentBlock(root, $"{name} body", new Vector3(0f, 0.45f, 0f), new Vector3(1.7f, 0.55f, 3.6f), body);
                ParentBlock(root, $"{name} roof", new Vector3(0f, 0.95f, -0.15f), new Vector3(1.5f, 0.42f, 1.7f), Shade(body, 0.85f));
                ParentBlock(root, $"{name} hood", new Vector3(0f, 0.55f, 1.05f), new Vector3(1.55f, 0.22f, 1.1f), body);
                ParentBlock(root, $"{name} glass front", new Vector3(0f, 1.05f, 0.65f), new Vector3(1.35f, 0.32f, 0.08f), new Color(0.2f, 0.35f, 0.45f, 0.42f));
                ParentBlock(root, $"{name} glass side L", new Vector3(-0.78f, 1.0f, -0.1f), new Vector3(0.06f, 0.28f, 1.2f), new Color(0.2f, 0.35f, 0.45f, 0.42f));
                ParentBlock(root, $"{name} bumper front", new Vector3(0f, 0.32f, 1.88f), new Vector3(1.7f, 0.28f, 0.2f), Shade(body, 0.7f));
                var wheel = new Color(0.12f, 0.12f, 0.13f);
                ParentBlock(root, $"{name} wheel FL", new Vector3(-0.78f, 0.17f, 1.1f), new Vector3(0.28f, 0.28f, 0.22f), wheel);
                ParentBlock(root, $"{name} wheel FR", new Vector3(0.78f, 0.17f, 1.1f), new Vector3(0.28f, 0.28f, 0.22f), wheel);
                ParentBlock(root, $"{name} wheel RL", new Vector3(-0.78f, 0.17f, -1.1f), new Vector3(0.28f, 0.28f, 0.22f), wheel);
                ParentBlock(root, $"{name} wheel RR", new Vector3(0.78f, 0.17f, -1.1f), new Vector3(0.28f, 0.28f, 0.22f), wheel);
            }

            root.position = position;
            root.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
        }

        private static void TintParkedCarBody(Transform root, Color body)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                    continue;
                var n = renderer.gameObject.name.ToLowerInvariant();
                if (n.Contains("wheel") || n.Contains("window") || n.Contains("glass")
                    || n.Contains("headlight") || n.Contains("taillight") || n.Contains("stripe")
                    || n.Contains("grille") || n.Contains("mirror") || n.Contains("hub")
                    || n.Contains("number") || n.Contains("wiper") || n.Contains("antenna"))
                    continue;
                if (n.Contains("body") || n.Contains("roof") || n.Contains("bumper")
                    || n.Contains("hood") || n.Contains("boot") || n.Contains("door")
                    || n.Contains("arch") || n.Contains("skirt"))
                {
                    var color = n.Contains("roof") ? Shade(body, 0.85f)
                        : n.Contains("bumper") || n.Contains("arch") || n.Contains("skirt") ? Shade(body, 0.7f)
                        : body;
                    // Flat lit for UV-less prefab cubes — avoid black authored texels.
                    renderer.material = AirsideMaterialLibrary.Create(color,
                        AirsideMaterialLibrary.SurfaceKind.PaintedMetal, useTextures: false);
                }
            }
        }

        /// <summary>
        /// Decision 0025 / Batch F3 — prefer PRP-003 trolley parts, then Resources, then greybox.
        /// </summary>
        private static void PlaceLuggageTrolley(string name, Vector3 position, float yawDegrees)
        {
            Transform root = null;
            var kit = PreferArtKit("Models/Props/mdl_terminal_forecourt_kit_v01.gltf");
            if (!string.IsNullOrEmpty(kit) && ArtGltfLoader.HasKit(kit))
            {
                root = new GameObject(name).transform;
                var steel = new Color(0.7f, 0.72f, 0.75f);
                var placed = 0;
                void Place(string mesh, Color color)
                {
                    if (!ArtGltfLoader.TryPlaceNamedMesh(kit, mesh, Vector3.zero, Quaternion.identity, color, out var part))
                        return;
                    part.SetParent(root, false);
                    part.localPosition = Vector3.zero;
                    part.localRotation = Quaternion.identity;
                    placed++;
                }

                Place("trolley_rail", steel);
                Place("trolley_post_l", steel);
                Place("trolley_post_r", steel);
                if (placed == 0)
                {
                    Object.Destroy(root.gameObject);
                    root = null;
                }
            }

            if (root == null && ArtPresentationLoader.TryInstantiatePrefab("mdl_luggage_trolley_v01", out var prefabRoot))
            {
                prefabRoot.name = name;
                root = prefabRoot;
            }

            if (root == null)
            {
                root = new GameObject(name).transform;
                ParentBlock(root, $"{name} basket", new Vector3(0f, 0.45f, 0f), new Vector3(0.8f, 0.55f, 0.45f), new Color(0.7f, 0.72f, 0.75f));
                ParentBlock(root, $"{name} handle", new Vector3(0f, 0.85f, -0.35f), new Vector3(0.7f, 0.08f, 0.08f), new Color(0.7f, 0.72f, 0.75f));
            }

            root.position = position;
            root.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
        }

        /// <summary>
        /// Decision 0025 / Batch F3 — prefer PRP-003 bench parts, then Resources, then greybox.
        /// </summary>
        private static void PlaceLandsideBench(string name, Vector3 position, float yawDegrees)
        {
            Transform root = null;
            var kit = PreferArtKit("Models/Props/mdl_terminal_forecourt_kit_v01.gltf");
            if (!string.IsNullOrEmpty(kit) && ArtGltfLoader.HasKit(kit))
            {
                root = new GameObject(name).transform;
                var wood = new Color(0.4f, 0.32f, 0.22f);
                var steel = new Color(0.45f, 0.46f, 0.48f);
                var placed = 0;
                void Place(string mesh, Color color)
                {
                    if (!ArtGltfLoader.TryPlaceNamedMesh(kit, mesh, Vector3.zero, Quaternion.identity, color, out var part))
                        return;
                    part.SetParent(root, false);
                    part.localPosition = Vector3.zero;
                    part.localRotation = Quaternion.identity;
                    placed++;
                }

                Place("bench_seat", wood);
                Place("bench_back", Shade(wood, 0.9f));
                Place("bench_leg_l", steel);
                Place("bench_leg_r", steel);
                if (placed == 0)
                {
                    Object.Destroy(root.gameObject);
                    root = null;
                }
            }

            if (root == null && ArtPresentationLoader.TryInstantiatePrefab("mdl_landside_bench_v01", out var prefabRoot))
            {
                prefabRoot.name = name;
                root = prefabRoot;
            }

            if (root == null)
            {
                root = new GameObject(name).transform;
                var wood = new Color(0.45f, 0.32f, 0.18f);
                ParentBlock(root, $"{name} seat", new Vector3(0f, 0.35f, 0f), new Vector3(2.2f, 0.12f, 0.55f), wood);
                ParentBlock(root, $"{name} back", new Vector3(0f, 0.7f, -0.22f), new Vector3(2.2f, 0.55f, 0.1f), wood);
            }

            root.position = position;
            root.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
        }

        /// <summary>
        /// Batch F3 PRP-002 — place modular fence bay / corner / vehicle gate panels.
        /// Returns false when the kit is missing so the CreateBlock ribbon remains the fallback.
        /// </summary>
        private static bool TryBuildPerimeterFenceFromKit()
        {
            var kit = PreferArtKit(
                "Models/Props/mdl_airfield_fence_gate_kit_v02.gltf",
                "Models/Props/mdl_airfield_fence_gate_kit_v01.gltf");
            if (string.IsNullOrEmpty(kit) || !ArtGltfLoader.HasKit(kit))
                return false;

            var post = new Color(0.55f, 0.56f, 0.58f);
            var panel = new Color(0.62f, 0.64f, 0.66f);
            var yellow = Shade(AirsideTheme.SafetyYellow, 0.75f);
            var placed = 0;

            void PlacePart(string mesh, Vector3 pos, Quaternion rot, Color color, string name)
            {
                if (!ArtGltfLoader.TryPlaceNamedMesh(kit, mesh, pos, rot, color, out var part))
                    return;
                part.name = name;
                placed++;
            }

            void PlaceBay(Vector3 pos, float yawDeg, string tag)
            {
                var rot = Quaternion.Euler(0f, yawDeg, 0f);
                PlacePart("fence_bay", pos, rot, panel, $"Fence bay {tag}");
                PlacePart("fence_bay_rail_top", pos, rot, post, $"Fence bay rail top {tag}");
                PlacePart("fence_bay_rail_mid", pos, rot, post, $"Fence bay rail mid {tag}");
                PlacePart("fence_bay_rail_bot", pos, rot, post, $"Fence bay rail bot {tag}");
                PlacePart("fence_bay_cap_l", pos, rot, post, $"Fence bay cap L {tag}");
                PlacePart("fence_bay_cap_r", pos, rot, post, $"Fence bay cap R {tag}");
                PlacePart("fence_bay_post_l", pos, rot, post, $"Fence bay post L {tag}");
                PlacePart("fence_bay_post_r", pos, rot, post, $"Fence bay post R {tag}");
                PlacePart("fence_corner_brace", pos, rot, post, $"Fence bay brace {tag}");
            }

            // North landside (gap for vehicle gate at x≈22–30).
            for (var x = -40; x <= 56; x += 4)
            {
                if (x >= 22 && x <= 30)
                    continue;
                PlaceBay(new Vector3(x + 2f, 0f, 34f), 0f, $"N {x}");
            }

            // West / east airside.
            for (var z = -18; z <= 32; z += 4)
            {
                PlaceBay(new Vector3(-44f, 0f, z + 2f), 90f, $"W {z}");
                PlaceBay(new Vector3(44f, 0f, z + 2f), 90f, $"E {z}");
            }

            // South above dunes (gap at runway strip).
            for (var x = -40; x <= 40; x += 4)
            {
                if (x >= -12 && x <= 12)
                    continue;
                PlaceBay(new Vector3(x + 2f, 0f, -20f), 0f, $"S {x}");
            }

            PlacePart("fence_corner", new Vector3(-44f, 0f, 34f), Quaternion.identity, post, "Fence corner NW");
            PlacePart("fence_corner", new Vector3(44f, 0f, 34f), Quaternion.identity, post, "Fence corner NE");
            PlacePart("fence_corner", new Vector3(-44f, 0f, -20f), Quaternion.identity, post, "Fence corner SW");
            PlacePart("fence_corner", new Vector3(44f, 0f, -20f), Quaternion.identity, post, "Fence corner SE");

            // Vehicle gate at access road.
            PlacePart("gate_post", new Vector3(23f, 0f, 34f), Quaternion.identity, post, "Gate post L");
            PlacePart("gate_post", new Vector3(29f, 0f, 34f), Quaternion.identity, post, "Gate post R");
            PlacePart("gate_vehicle_leaf_l", new Vector3(24.2f, 0f, 35.6f), Quaternion.Euler(0f, 12f, 0f), yellow, "Gate leaf L");
            PlacePart("gate_vehicle_leaf_r", new Vector3(27.8f, 0f, 35.6f), Quaternion.Euler(0f, -12f, 0f), yellow, "Gate leaf R");
            PlacePart("gate_vehicle_rail", new Vector3(26f, 0f, 35.55f), Quaternion.identity, post, "Gate vehicle rail");
            PlacePart("gate_vehicle_chevron", new Vector3(24.2f, 0f, 35.5f), Quaternion.identity, new Color(0.15f, 0.15f, 0.16f), "Gate chevron L");
            PlacePart("gate_vehicle_chevron", new Vector3(27.8f, 0f, 35.5f), Quaternion.identity, new Color(0.15f, 0.15f, 0.16f), "Gate chevron R");
            PlacePart("gate_sign", new Vector3(26f, 0f, 34.2f), Quaternion.identity, AirsideTheme.SafetyYellow, "Gate sign");
            PlacePart("gate_sign_frame", new Vector3(26f, 0f, 34.15f), Quaternion.identity, post, "Gate sign frame");
            PlacePart("gate_post_light", new Vector3(23f, 0f, 34.15f), Quaternion.identity, new Color(0.95f, 0.35f, 0.12f), "Gate light L");
            PlacePart("gate_post_light", new Vector3(29f, 0f, 34.15f), Quaternion.identity, new Color(0.95f, 0.35f, 0.12f), "Gate light R");
            PlacePart("gate_latch", new Vector3(26f, 0f, 35.5f), Quaternion.identity, new Color(0.25f, 0.26f, 0.28f), "Gate latch");
            PlacePart("gate_stop", new Vector3(23.1f, 0f, 34.4f), Quaternion.identity, AirsideTheme.Concrete, "Gate stop L");
            PlacePart("gate_stop", new Vector3(28.9f, 0f, 34.4f), Quaternion.identity, AirsideTheme.Concrete, "Gate stop R");

            return placed >= 20;
        }

        private static void BuildPerimeterFence()
        {
            // Batch F3 PRP-002 — modular fence/gate kit; dense CreateBlock ribbon remains fallback.
            if (TryBuildPerimeterFenceFromKit())
                return;

            var post = new Color(0.55f, 0.56f, 0.58f);
            var rail = new Color(0.72f, 0.74f, 0.76f);
            var mesh = new Color(0.62f, 0.64f, 0.66f);
            // North landside fence (gap for vehicle gate at access road x≈23–29).
            for (var x = -40; x <= 56; x += 4)
            {
                if (x >= 22 && x <= 30)
                    continue;
                CreateBlock($"Fence post N {x}", new Vector3(x, 0.7f, 34f), new Vector3(0.12f, 1.4f, 0.12f), post);
                if (x < 56 && !(x >= 18 && x <= 30))
                {
                    CreateBlock($"Fence rail N top {x}", new Vector3(x + 2f, 1.25f, 34f), new Vector3(4f, 0.05f, 0.05f), rail);
                    CreateBlock($"Fence rail N mid {x}", new Vector3(x + 2f, 0.75f, 34f), new Vector3(4f, 0.05f, 0.05f), rail);
                    CreateBlock($"Fence rail N bot {x}", new Vector3(x + 2f, 0.28f, 34f), new Vector3(4f, 0.05f, 0.05f), rail);
                    // Diagonal bars read as chain-link from overview.
                    CreateBlock($"Fence mesh N a {x}", new Vector3(x + 1f, 0.75f, 34f), new Vector3(0.04f, 1.0f, 0.04f), mesh);
                    CreateBlock($"Fence mesh N b {x}", new Vector3(x + 3f, 0.75f, 34f), new Vector3(0.04f, 1.0f, 0.04f), mesh);
                }
            }

            // West and east airside boundaries (keep runway ends open).
            for (var z = -18; z <= 32; z += 4)
            {
                CreateBlock($"Fence post W {z}", new Vector3(-44f, 0.7f, z), new Vector3(0.12f, 1.4f, 0.12f), post);
                CreateBlock($"Fence post E {z}", new Vector3(44f, 0.7f, z), new Vector3(0.12f, 1.4f, 0.12f), post);
                if (z < 32)
                {
                    CreateBlock($"Fence rail W top {z}", new Vector3(-44f, 1.25f, z + 2f), new Vector3(0.05f, 0.05f, 4f), rail);
                    CreateBlock($"Fence rail W mid {z}", new Vector3(-44f, 0.75f, z + 2f), new Vector3(0.05f, 0.05f, 4f), rail);
                    CreateBlock($"Fence rail W bot {z}", new Vector3(-44f, 0.28f, z + 2f), new Vector3(0.05f, 0.05f, 4f), rail);
                    CreateBlock($"Fence rail E top {z}", new Vector3(44f, 1.25f, z + 2f), new Vector3(0.05f, 0.05f, 4f), rail);
                    CreateBlock($"Fence rail E mid {z}", new Vector3(44f, 0.75f, z + 2f), new Vector3(0.05f, 0.05f, 4f), rail);
                    CreateBlock($"Fence rail E bot {z}", new Vector3(44f, 0.28f, z + 2f), new Vector3(0.05f, 0.05f, 4f), rail);
                    CreateBlock($"Fence mesh W a {z}", new Vector3(-44f, 0.75f, z + 1f), new Vector3(0.04f, 1.0f, 0.04f), mesh);
                    CreateBlock($"Fence mesh W b {z}", new Vector3(-44f, 0.75f, z + 3f), new Vector3(0.04f, 1.0f, 0.04f), mesh);
                    CreateBlock($"Fence mesh E a {z}", new Vector3(44f, 0.75f, z + 1f), new Vector3(0.04f, 1.0f, 0.04f), mesh);
                    CreateBlock($"Fence mesh E b {z}", new Vector3(44f, 0.75f, z + 3f), new Vector3(0.04f, 1.0f, 0.04f), mesh);
                }
            }

            // Vehicle gate leaves at the access road (open inward to landside).
            CreateBlock("Gate post L", new Vector3(23f, 0.9f, 34f), new Vector3(0.22f, 1.8f, 0.22f), post);
            CreateBlock("Gate post R", new Vector3(29f, 0.9f, 34f), new Vector3(0.22f, 1.8f, 0.22f), post);
            CreateBlock("Gate leaf L", new Vector3(24.2f, 0.85f, 35.6f), new Vector3(2.2f, 1.5f, 0.08f), Shade(AirsideTheme.SafetyYellow, 0.75f));
            CreateBlock("Gate leaf R", new Vector3(27.8f, 0.85f, 35.6f), new Vector3(2.2f, 1.5f, 0.08f), Shade(AirsideTheme.SafetyYellow, 0.75f));
            CreateBlock("Gate rail L top", new Vector3(24.2f, 1.45f, 35.55f), new Vector3(2.0f, 0.06f, 0.06f), rail);
            CreateBlock("Gate rail L mid", new Vector3(24.2f, 0.85f, 35.55f), new Vector3(2.0f, 0.06f, 0.06f), rail);
            CreateBlock("Gate rail R top", new Vector3(27.8f, 1.45f, 35.55f), new Vector3(2.0f, 0.06f, 0.06f), rail);
            CreateBlock("Gate rail R mid", new Vector3(27.8f, 0.85f, 35.55f), new Vector3(2.0f, 0.06f, 0.06f), rail);
            CreateBlock("Gate hinge L", new Vector3(23.15f, 0.9f, 34.35f), new Vector3(0.12f, 0.35f, 0.12f), mesh);
            CreateBlock("Gate hinge R", new Vector3(28.85f, 0.9f, 34.35f), new Vector3(0.12f, 0.35f, 0.12f), mesh);
            CreateBlock("Gate latch", new Vector3(26f, 0.95f, 35.5f), new Vector3(0.35f, 0.18f, 0.12f), new Color(0.25f, 0.26f, 0.28f));
            CreateBlock("Gate stop L", new Vector3(23.1f, 0.08f, 34.4f), new Vector3(0.35f, 0.12f, 0.35f), AirsideTheme.Concrete);
            CreateBlock("Gate stop R", new Vector3(28.9f, 0.08f, 34.4f), new Vector3(0.35f, 0.12f, 0.35f), AirsideTheme.Concrete);
            CreateBlock("Gate sign", new Vector3(26f, 2.0f, 34.2f), new Vector3(1.6f, 0.55f, 0.06f), AirsideTheme.SafetyYellow);
            CreateBlock("Gate sign frame", new Vector3(26f, 2.0f, 34.15f), new Vector3(1.75f, 0.68f, 0.04f), post);
            CreateBlock("Gate light L", new Vector3(23f, 1.85f, 34.15f), new Vector3(0.18f, 0.18f, 0.18f), new Color(0.95f, 0.35f, 0.12f));
            CreateBlock("Gate light R", new Vector3(29f, 1.85f, 34.15f), new Vector3(0.18f, 0.18f, 0.18f), new Color(0.95f, 0.35f, 0.12f));

            // South airside fence above the dunes (gap kept clear of runway strip).
            for (var x = -40; x <= 40; x += 4)
            {
                if (x >= -12 && x <= 12)
                    continue;
                CreateBlock($"Fence post S {x}", new Vector3(x, 0.65f, -20f), new Vector3(0.12f, 1.3f, 0.12f), post);
                if (x < 40 && !(x >= -16 && x <= 12))
                {
                    CreateBlock($"Fence rail S top {x}", new Vector3(x + 2f, 1.15f, -20f), new Vector3(4f, 0.05f, 0.05f), rail);
                    CreateBlock($"Fence rail S mid {x}", new Vector3(x + 2f, 0.7f, -20f), new Vector3(4f, 0.05f, 0.05f), rail);
                    CreateBlock($"Fence rail S bot {x}", new Vector3(x + 2f, 0.28f, -20f), new Vector3(4f, 0.05f, 0.05f), rail);
                    CreateBlock($"Fence mesh S a {x}", new Vector3(x + 1f, 0.7f, -20f), new Vector3(0.04f, 0.9f, 0.04f), mesh);
                    CreateBlock($"Fence mesh S b {x}", new Vector3(x + 3f, 0.7f, -20f), new Vector3(0.04f, 0.9f, 0.04f), mesh);
                }
            }

            // Mid-span fence posts densify the north/south ribbons so overview reads as chain-link.
            for (var x = -38; x <= 54; x += 4)
            {
                if (x >= 22 && x <= 30)
                    continue;
                CreateBlock($"Fence post N mid {x}", new Vector3(x + 2f, 0.7f, 34f), new Vector3(0.1f, 1.35f, 0.1f), post);
            }

            for (var x = -38; x <= 38; x += 4)
            {
                if (x >= -12 && x <= 12)
                    continue;
                CreateBlock($"Fence post S mid {x}", new Vector3(x + 2f, 0.65f, -20f), new Vector3(0.1f, 1.25f, 0.1f), post);
            }

            // Warning chevrons on the airside face of the vehicle gate.
            CreateBlock("Gate chevron L", new Vector3(24.2f, 0.85f, 35.45f), new Vector3(1.8f, 0.35f, 0.04f),
                new Color(0.15f, 0.15f, 0.16f));
            CreateBlock("Gate chevron R", new Vector3(27.8f, 0.85f, 35.45f), new Vector3(1.8f, 0.35f, 0.04f),
                new Color(0.15f, 0.15f, 0.16f));

            // East/west mid posts + corner braces so airside boundary reads continuous.
            for (var z = -16; z <= 30; z += 4)
            {
                CreateBlock($"Fence post W mid {z}", new Vector3(-44f, 0.7f, z + 2f), new Vector3(0.1f, 1.35f, 0.1f), post);
                CreateBlock($"Fence post E mid {z}", new Vector3(44f, 0.7f, z + 2f), new Vector3(0.1f, 1.35f, 0.1f), post);
            }

            CreateBlock("Fence brace NW", new Vector3(-43.2f, 0.7f, 33.2f), new Vector3(1.4f, 0.08f, 0.08f), rail);
            CreateBlock("Fence brace NE", new Vector3(43.2f, 0.7f, 33.2f), new Vector3(1.4f, 0.08f, 0.08f), rail);
            CreateBlock("Fence brace SW", new Vector3(-43.2f, 0.65f, -19.2f), new Vector3(1.4f, 0.08f, 0.08f), rail);
            CreateBlock("Fence brace SE", new Vector3(43.2f, 0.65f, -19.2f), new Vector3(1.4f, 0.08f, 0.08f), rail);
            // Mid-span diagonal braces so the fence reads as stiffened chain-link, not toy rails.
            for (var x = -36; x <= 52; x += 12)
            {
                if (x >= 20 && x <= 32)
                    continue;
                CreateBlock($"Fence brace N diag {x}", new Vector3(x + 1f, 0.75f, 34f), new Vector3(2.2f, 0.06f, 0.06f), mesh);
            }

            for (var x = -36; x <= 36; x += 12)
            {
                if (x >= -14 && x <= 14)
                    continue;
                CreateBlock($"Fence brace S diag {x}", new Vector3(x + 1f, 0.7f, -20f), new Vector3(2.2f, 0.06f, 0.06f), mesh);
            }

            for (var z = -14; z <= 28; z += 12)
            {
                CreateBlock($"Fence brace W diag {z}", new Vector3(-44f, 0.75f, z + 1f), new Vector3(0.06f, 0.06f, 2.2f), mesh);
                CreateBlock($"Fence brace E diag {z}", new Vector3(44f, 0.75f, z + 1f), new Vector3(0.06f, 0.06f, 2.2f), mesh);
            }

            CreateBlock("Fence cap NW", new Vector3(-44f, 1.45f, 34f), new Vector3(0.28f, 0.12f, 0.28f), post);
            CreateBlock("Fence cap NE", new Vector3(44f, 1.45f, 34f), new Vector3(0.28f, 0.12f, 0.28f), post);
            CreateBlock("Fence cap SW", new Vector3(-44f, 1.35f, -20f), new Vector3(0.28f, 0.12f, 0.28f), post);
            CreateBlock("Fence cap SE", new Vector3(44f, 1.35f, -20f), new Vector3(0.28f, 0.12f, 0.28f), post);

            // Top tension wire + intermittent post caps so the fence reads as chain-link mesh.
            for (var x = -40; x <= 52; x += 8)
            {
                if (x >= 20 && x <= 32)
                    continue;
                CreateBlock($"Fence wire N {x}", new Vector3(x + 2f, 1.38f, 34f), new Vector3(8f, 0.03f, 0.03f), mesh);
                CreateBlock($"Fence cap N {x}", new Vector3(x, 1.42f, 34f), new Vector3(0.2f, 0.1f, 0.2f), post);
            }

            for (var x = -40; x <= 36; x += 8)
            {
                if (x >= -14 && x <= 14)
                    continue;
                CreateBlock($"Fence wire S {x}", new Vector3(x + 2f, 1.28f, -20f), new Vector3(8f, 0.03f, 0.03f), mesh);
                CreateBlock($"Fence cap S {x}", new Vector3(x, 1.32f, -20f), new Vector3(0.2f, 0.1f, 0.2f), post);
            }

            for (var z = -18; z <= 28; z += 8)
            {
                CreateBlock($"Fence wire W {z}", new Vector3(-44f, 1.38f, z + 2f), new Vector3(0.03f, 0.03f, 8f), mesh);
                CreateBlock($"Fence wire E {z}", new Vector3(44f, 1.38f, z + 2f), new Vector3(0.03f, 0.03f, 8f), mesh);
                CreateBlock($"Fence cap W {z}", new Vector3(-44f, 1.42f, z), new Vector3(0.2f, 0.1f, 0.2f), post);
                CreateBlock($"Fence cap E {z}", new Vector3(44f, 1.42f, z), new Vector3(0.2f, 0.1f, 0.2f), post);
            }
        }

        /// <summary>
        /// Decision 0025 items 3+5 — short approach light bars west of the threshold
        /// so night approaches read as a lit path, not a bare runway end.
        /// </summary>
        private static void BuildApproachLightBars()
        {
            var bar = new Color(0.85f, 0.88f, 0.9f);
            var stem = new Color(0.35f, 0.36f, 0.38f);
            var lightingKit = PreferArtKit(
                "Models/Props/mdl_airfield_lighting_kit_authored_v01.gltf",
                "Models/Props/mdl_airfield_lighting_kit_v02.gltf",
                "Models/Props/mdl_airfield_lighting_kit_v01.gltf");
            // Simple ALS centreline + bar pairs west of runway 09 threshold (~x=-36).
            // Reuse edge/taxi/obst lighting kit parts so stations read authored, not toy cubes.
            // Kit path: fewer stations + silhouette fixtures so approach reads lit, not mesh soup.
            var hasLightingKit = !string.IsNullOrEmpty(lightingKit) && ArtGltfLoader.HasKit(lightingKit);
            var stationCount = hasLightingKit ? 5 : 8;
            var stationStep = hasLightingKit ? 7f : 5f;
            var anyKitStation = false;
            for (var i = 0; i < stationCount; i++)
            {
                var x = -40f - i * stationStep;
                var origin = new Vector3(x, 0f, 0f);
                var kitStation = false;
                void AlsPart(string mesh, Color color)
                {
                    if (ArtGltfLoader.TryPlaceNamedMesh(lightingKit, mesh, origin, Quaternion.identity, color, out _))
                        kitStation = true;
                }

                AlsPart("edge_base", stem);
                AlsPart("edge_stem", stem);
                AlsPart("edge_lens", bar);
                if (!hasLightingKit)
                {
                    AlsPart("edge_collar", Shade(stem, 1.1f));
                    AlsPart("edge_gasket", new Color(0.2f, 0.21f, 0.22f));
                    AlsPart("edge_glare", new Color(1f, 0.97f, 0.88f));
                    AlsPart("edge_reflector", new Color(0.9f, 0.92f, 0.94f));
                }

                if (i % 2 == 0)
                {
                    AlsPart("taxi_base", stem);
                    AlsPart("taxi_stem", stem);
                    AlsPart("taxi_lens", bar);
                    if (!hasLightingKit)
                    {
                        AlsPart("taxi_collar", Shade(stem, 1.05f));
                        AlsPart("taxi_reflector", new Color(0.9f, 0.92f, 0.94f));
                    }
                }

                if (!kitStation)
                {
                    CreateBlock($"ALS stem {i}", new Vector3(x, 0.35f, 0f), new Vector3(0.12f, 0.7f, 0.12f), stem);
                    CreateBlock($"ALS centre {i}", new Vector3(x, 0.75f, 0f), new Vector3(0.35f, 0.18f, 0.35f), bar);
                    CreateBlock($"ALS bar L {i}", new Vector3(x, 0.7f, -1.4f - i * 0.12f), new Vector3(0.25f, 0.14f, 2.2f + i * 0.18f), bar);
                    CreateBlock($"ALS bar R {i}", new Vector3(x, 0.7f, 1.4f + i * 0.12f), new Vector3(0.25f, 0.14f, 2.2f + i * 0.18f), bar);
                    if (i % 2 == 0)
                        CreateBlock($"ALS cross {i}", new Vector3(x, 0.68f, 0f), new Vector3(0.18f, 0.12f, 3.6f + i * 0.15f), bar);
                }
                else
                {
                    // Lateral bars from lighting kit stems rotated across the approach axis
                    // (kit has no dedicated ALS wing meshes).
                    var barHalf = 1.4f + i * 0.12f;
                    var barLen = 2.2f + i * 0.18f;
                    ArtGltfLoader.TryPlaceNamedMesh(
                        lightingKit, "taxi_stem",
                        new Vector3(x, 0.55f, -barHalf),
                        Quaternion.Euler(0f, 90f, 0f), stem, out _,
                        new Vector3(0.35f, barLen * 0.35f, 0.35f));
                    ArtGltfLoader.TryPlaceNamedMesh(
                        lightingKit, "taxi_stem",
                        new Vector3(x, 0.55f, barHalf),
                        Quaternion.Euler(0f, 90f, 0f), stem, out _,
                        new Vector3(0.35f, barLen * 0.35f, 0.35f));
                    ArtGltfLoader.TryPlaceNamedMesh(
                        lightingKit, "taxi_lens",
                        new Vector3(x, 0.72f, -barHalf),
                        Quaternion.identity, bar, out _,
                        new Vector3(0.55f, 0.55f, 0.55f));
                    ArtGltfLoader.TryPlaceNamedMesh(
                        lightingKit, "taxi_lens",
                        new Vector3(x, 0.72f, barHalf),
                        Quaternion.identity, bar, out _,
                        new Vector3(0.55f, 0.55f, 0.55f));
                    if (i % 2 == 0)
                    {
                        ArtGltfLoader.TryPlaceNamedMesh(
                            lightingKit, "edge_stem",
                            new Vector3(x, 0.5f, 0f),
                            Quaternion.Euler(0f, 90f, 0f), stem, out _,
                            new Vector3(0.3f, (3.6f + i * 0.15f) * 0.28f, 0.3f));
                    }
                }

                if (kitStation)
                    anyKitStation = true;

                var lampGo = new GameObject($"ALS lamp {i}");
                lampGo.transform.position = new Vector3(x, 0.95f, 0f);
                // Aim SpotLights toward threshold (~x=-36) so approach washes asphalt (0025 item 5).
                lampGo.transform.rotation = Quaternion.LookRotation(new Vector3(-36f - x, -0.7f, 0f).normalized);
                var light = lampGo.AddComponent<Light>();
                light.type = LightType.Spot;
                light.color = new Color(1f, 0.95f, 0.85f);
                light.range = 14f + i * 0.6f;
                light.spotAngle = 42f;
                light.innerSpotAngle = 18f;
                light.intensity = 0f;
                light.shadows = LightShadows.None;

                // Emissive lens proxy only on greybox path — kit stations already ship edge_lens.
                if (!kitStation)
                {
                    var lens = CreateBlock($"ALS lens {i}", new Vector3(x, 0.78f, 0f), new Vector3(0.28f, 0.12f, 0.28f),
                        new Color(1f, 0.97f, 0.88f));
                    var lensRenderer = lens.GetComponent<Renderer>();
                    if (lensRenderer != null && lensRenderer.material.HasProperty("_EmissionColor"))
                    {
                        lensRenderer.material.EnableKeyword("_EMISSION");
                        lensRenderer.material.SetColor("_EmissionColor", new Color(1f, 0.95f, 0.8f) * 1.4f);
                    }
                }
            }

            // Far REIL pair — pulsed SpotLights at night (collected with runway edge REIL names).
            var reilOriginL = new Vector3(-78f, 0f, -2.8f);
            var reilOriginR = new Vector3(-78f, 0f, 2.8f);
            var reilKit = ArtGltfLoader.TryPlaceNamedMesh(lightingKit, "obst_base", reilOriginL, Quaternion.identity, stem, out _)
                | ArtGltfLoader.TryPlaceNamedMesh(lightingKit, "obst_stem", reilOriginL, Quaternion.identity, stem, out _)
                | ArtGltfLoader.TryPlaceNamedMesh(lightingKit, "obst_lens", reilOriginL, Quaternion.identity, new Color(1f, 1f, 0.9f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(lightingKit, "obst_guard", reilOriginL, Quaternion.identity, Shade(stem, 1.1f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(lightingKit, "obst_ring", reilOriginL, Quaternion.identity, new Color(0.95f, 0.35f, 0.12f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(lightingKit, "obst_cap", reilOriginL, Quaternion.identity, stem, out _)
                | ArtGltfLoader.TryPlaceNamedMesh(lightingKit, "obst_beacon_ring", reilOriginL, Quaternion.identity, new Color(1f, 0.9f, 0.5f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(lightingKit, "obst_base", reilOriginR, Quaternion.identity, stem, out _)
                | ArtGltfLoader.TryPlaceNamedMesh(lightingKit, "obst_stem", reilOriginR, Quaternion.identity, stem, out _)
                | ArtGltfLoader.TryPlaceNamedMesh(lightingKit, "obst_lens", reilOriginR, Quaternion.identity, new Color(1f, 1f, 0.9f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(lightingKit, "obst_guard", reilOriginR, Quaternion.identity, Shade(stem, 1.1f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(lightingKit, "obst_ring", reilOriginR, Quaternion.identity, new Color(0.95f, 0.35f, 0.12f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(lightingKit, "obst_cap", reilOriginR, Quaternion.identity, stem, out _)
                | ArtGltfLoader.TryPlaceNamedMesh(lightingKit, "obst_beacon_ring", reilOriginR, Quaternion.identity, new Color(1f, 0.9f, 0.5f), out _);
            if (!reilKit)
            {
                CreateBlock("ALS REIL L", new Vector3(-78f, 0.8f, -2.8f), new Vector3(0.4f, 0.4f, 0.4f), new Color(1f, 1f, 0.9f));
                CreateBlock("ALS REIL R", new Vector3(-78f, 0.8f, 2.8f), new Vector3(0.4f, 0.4f, 0.4f), new Color(1f, 1f, 0.9f));
                CreateBlock("ALS REIL mast L", new Vector3(-78f, 0.4f, -2.8f), new Vector3(0.14f, 0.75f, 0.14f), stem);
                CreateBlock("ALS REIL mast R", new Vector3(-78f, 0.4f, 2.8f), new Vector3(0.14f, 0.75f, 0.14f), stem);
                CreateBlock("ALS REIL base L", new Vector3(-78f, 0.06f, -2.8f), new Vector3(0.45f, 0.1f, 0.45f), AirsideTheme.Concrete);
                CreateBlock("ALS REIL base R", new Vector3(-78f, 0.06f, 2.8f), new Vector3(0.45f, 0.1f, 0.45f), AirsideTheme.Concrete);
            }

            if (!anyKitStation)
            {
                CreateBlock("ALS lead-in bar", new Vector3(-58f, 0.72f, 0f), new Vector3(0.2f, 0.12f, 4.8f), bar);
                CreateBlock("ALS wing bar L", new Vector3(-52f, 0.7f, -3.2f), new Vector3(0.22f, 0.12f, 2.4f), bar);
                CreateBlock("ALS wing bar R", new Vector3(-52f, 0.7f, 3.2f), new Vector3(0.22f, 0.12f, 2.4f), bar);
            }
            else
            {
                // Kit path — reuse taxi stems for the approach wing / lead-in read.
                ArtGltfLoader.TryPlaceNamedMesh(
                    lightingKit, "taxi_stem", new Vector3(-58f, 0.5f, 0f),
                    Quaternion.Euler(0f, 90f, 0f), stem, out _, new Vector3(0.35f, 1.2f, 0.35f));
                ArtGltfLoader.TryPlaceNamedMesh(
                    lightingKit, "taxi_stem", new Vector3(-52f, 0.5f, -3.2f),
                    Quaternion.Euler(0f, 90f, 0f), stem, out _, new Vector3(0.3f, 0.7f, 0.3f));
                ArtGltfLoader.TryPlaceNamedMesh(
                    lightingKit, "taxi_stem", new Vector3(-52f, 0.5f, 3.2f),
                    Quaternion.Euler(0f, 90f, 0f), stem, out _, new Vector3(0.3f, 0.7f, 0.3f));
            }

            for (var side = 0; side < 2; side++)
            {
                var z = side == 0 ? -2.8f : 2.8f;
                var reilGo = new GameObject(side == 0 ? "REIL lamp L" : "REIL lamp R");
                reilGo.transform.position = new Vector3(-78f, 1.1f, z);
                reilGo.transform.rotation = Quaternion.LookRotation(new Vector3(1f, -0.15f, 0f));
                var reil = reilGo.AddComponent<Light>();
                reil.type = LightType.Spot;
                reil.color = new Color(1f, 1f, 0.92f);
                reil.range = 22f;
                reil.spotAngle = 28f;
                reil.innerSpotAngle = 12f;
                reil.intensity = 0f;
                reil.shadows = LightShadows.None;
            }
        }

        /// <summary>
        /// Decision 0025 items 1+3 — small ARFF / rescue shed so landside reads as a
        /// working regional airfield, not only terminal + hangar.
        /// </summary>
        private static void BuildArffRescueShed()
        {
            if (ArtPresentationLoader.TryInstantiatePrefab("mdl_arff_shed_v01", out var shed))
            {
                shed.name = "ARFF rescue shed";
                shed.position = new Vector3(-28f, 0f, 30f);
                // Kit already carries side spill meshes — soft Point only as dusk fallback.
                var bay = new GameObject("ARFF bay light");
                bay.transform.position = new Vector3(-28f, 2.4f, 27.8f);
                var light = bay.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.85f, 0.55f);
                light.range = 8f;
                light.intensity = 0f;
                light.shadows = LightShadows.None;
            }
            else
            {
                var body = new Color(0.72f, 0.22f, 0.18f);
                var roof = new Color(0.35f, 0.36f, 0.38f);
                var door = new Color(0.55f, 0.56f, 0.58f);
                CreateBlock("ARFF shed", new Vector3(-28f, 1.4f, 30f), new Vector3(7f, 2.8f, 5.5f), body);
                CreateBlock("ARFF roof", new Vector3(-28f, 3.0f, 30f), new Vector3(7.6f, 0.35f, 6.0f), roof);
                CreateBlock("ARFF roof ridge", new Vector3(-28f, 3.25f, 30f), new Vector3(7.8f, 0.18f, 0.8f), Shade(roof, 0.85f));
                CreateBlock("ARFF door L", new Vector3(-29.4f, 1.2f, 27.2f), new Vector3(2.4f, 2.2f, 0.12f), door);
                CreateBlock("ARFF door R", new Vector3(-26.6f, 1.2f, 27.2f), new Vector3(2.4f, 2.2f, 0.12f), door);
                CreateBlock("ARFF door rib L", new Vector3(-29.4f, 1.2f, 27.28f), new Vector3(0.1f, 2.1f, 0.06f), Shade(door, 0.8f));
                CreateBlock("ARFF door rib R", new Vector3(-26.6f, 1.2f, 27.28f), new Vector3(0.1f, 2.1f, 0.06f), Shade(door, 0.8f));
                CreateBlock("ARFF window", new Vector3(-25.2f, 2.0f, 30f), new Vector3(0.08f, 0.9f, 1.4f), new Color(0.2f, 0.4f, 0.5f));
                CreateBlock("ARFF apron", new Vector3(-28f, 0.02f, 26.5f), new Vector3(9f, 0.06f, 4f), AirsideTheme.Concrete,
                    "Textures/Surfaces/tx_concrete_apron_basecolor_v01.png", new Vector2(2f, 1f));
                CreateBlock("ARFF sign", new Vector3(-28f, 2.6f, 27.15f), new Vector3(2.2f, 0.45f, 0.08f), AirsideTheme.SafetyYellow);
                CreateBlock("ARFF hose reel", new Vector3(-31.2f, 0.55f, 27.5f), new Vector3(0.7f, 0.9f, 0.7f), new Color(0.35f, 0.2f, 0.15f));
                CreateBlock("ARFF hydrant", new Vector3(-24.8f, 0.35f, 27.8f), new Vector3(0.35f, 0.55f, 0.35f), new Color(0.75f, 0.2f, 0.15f));

                // Soft bay spill at dusk (greybox shed has no kit side lamps).
                var bay = new GameObject("ARFF bay light");
                bay.transform.position = new Vector3(-28f, 2.4f, 27.8f);
                var light = bay.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.85f, 0.55f);
                light.range = 10f;
                light.intensity = 0f;
                light.shadows = LightShadows.None;
            }

            PlaceArffTruck();
        }

        /// <summary>
        /// Decision 0025 items 1+3 — Resources ARFF truck parked on the rescue apron.
        /// </summary>
        private static void PlaceArffTruck()
        {
            Transform root;
            if (ArtPresentationLoader.TryInstantiatePrefab("mdl_arff_truck_v01", out var prefabRoot))
            {
                prefabRoot.name = "ARFF truck";
                root = prefabRoot;
            }
            else
            {
                root = new GameObject("ARFF truck").transform;
                ParentBlock(root, "ARFF chassis", new Vector3(0f, 0.55f, 0f), new Vector3(1.8f, 0.55f, 4.2f), new Color(0.78f, 0.18f, 0.14f));
                ParentBlock(root, "ARFF cab", new Vector3(0f, 1.35f, 1.2f), new Vector3(1.7f, 1.0f, 1.6f), new Color(0.78f, 0.18f, 0.14f));
                ParentBlock(root, "ARFF tank", new Vector3(0f, 1.4f, -0.9f), new Vector3(1.55f, 1.1f, 2.4f), new Color(0.78f, 0.18f, 0.14f));
                ParentBlock(root, "ARFF stripe", new Vector3(0f, 0.85f, 0f), new Vector3(1.85f, 0.18f, 3.6f), AirsideTheme.SafetyYellow);
            }

            root.position = new Vector3(-28f, 0f, 25.2f);
            root.rotation = Quaternion.Euler(0f, 180f, 0f);
        }

        /// <summary>
        /// Decision 0025 item 3 — landside canopy, posts and glass so the terminal
        /// entrance reads as a building, not a flat box, from overview and landside.
        /// </summary>
        private static void BuildTerminalLandsideCanopy()
        {
            var steel = new Color(0.48f, 0.5f, 0.52f);
            var glass = new Color(0.18f, 0.42f, 0.55f);
            var soffit = new Color(0.62f, 0.64f, 0.66f);
            // Terminal kits already carry canopy / landside glass — skip greybox densify.
            var hasKitCanopy = GameObject.Find("canopy") != null
                || GameObject.Find("canopy_soffit") != null
                || GameObject.Find("canopy_beam") != null;
            if (!hasKitCanopy)
            {
                CreateBlock("Terminal canopy slab", new Vector3(26f, 3.55f, 31.2f), new Vector3(16f, 0.18f, 4.2f), soffit);
                CreateBlock("Terminal canopy edge", new Vector3(26f, 3.4f, 33.1f), new Vector3(16.2f, 0.22f, 0.25f), steel);
                for (var i = 0; i < 5; i++)
                {
                    var x = 18.5f + i * 3.75f;
                    CreateBlock($"Terminal canopy post {i}", new Vector3(x, 1.7f, 32.6f), new Vector3(0.22f, 3.4f, 0.22f), steel);
                }

                CreateBlock("Terminal landside glass", new Vector3(26f, 2.1f, 29.55f), new Vector3(14f, 2.6f, 0.1f), glass,
                    "Textures/Environment/tx_terminal_glass_mask_v01.png", new Vector2(2.5f, 1.2f));
                CreateBlock("Terminal entrance frame", new Vector3(26f, 1.6f, 29.5f), new Vector3(3.2f, 2.8f, 0.18f), steel);
                CreateBlock("Terminal doors", new Vector3(26f, 1.45f, 29.35f), new Vector3(2.6f, 2.4f, 0.08f), new Color(0.22f, 0.28f, 0.32f));
                CreateBlock("Terminal canopy glow", new Vector3(26f, 3.35f, 31.2f), new Vector3(12f, 0.06f, 3.2f), new Color(1f, 0.85f, 0.55f));
            }
            else if (GameObject.Find("canopy_light_l") == null
                     && GameObject.Find("canopy_light_r") == null
                     && GameObject.Find("canopy_light_mid") == null)
            {
                CreateBlock("Terminal canopy glow", new Vector3(26f, 3.35f, 31.2f), new Vector3(12f, 0.06f, 3.2f), new Color(1f, 0.85f, 0.55f));
            }

            // Batch F3 PRP-003 — prefer forecourt kit for bench/planter/bollards/sign; blocks remain fallback.
            if (!TryPlaceForecourtFromKit())
            {
                CreateBlock("Terminal bench", new Vector3(21f, 0.35f, 31.6f), new Vector3(2.4f, 0.35f, 0.55f), new Color(0.4f, 0.32f, 0.22f));
                CreateBlock("Terminal planter", new Vector3(31.5f, 0.35f, 31.8f), new Vector3(1.4f, 0.5f, 1.0f), AirsideTheme.Concrete);
                CreateBlock("Terminal planter scrub", new Vector3(31.5f, 0.85f, 31.8f), new Vector3(1.1f, 0.55f, 0.7f), Shade(AirsideTheme.Eucalyptus, 0.85f));
            }
        }

        /// <summary>Batch F3 PRP-003 — kerbs, bollards, planter, bench, parking sign.</summary>
        private static bool TryPlaceForecourtFromKit()
        {
            var kit = PreferArtKit("Models/Props/mdl_terminal_forecourt_kit_v01.gltf");
            if (string.IsNullOrEmpty(kit) || !ArtGltfLoader.HasKit(kit))
                return false;

            var wood = new Color(0.4f, 0.32f, 0.22f);
            var steel = new Color(0.45f, 0.46f, 0.48f);
            var placed = 0;
            void Place(string mesh, Vector3 pos, Color color, string name)
            {
                if (!ArtGltfLoader.TryPlaceNamedMesh(kit, mesh, pos, Quaternion.identity, color, out var part))
                    return;
                part.name = name;
                placed++;
            }

            Place("bench_seat", new Vector3(21f, 0f, 31.6f), wood, "Terminal bench");
            Place("bench_back", new Vector3(21f, 0f, 31.6f), Shade(wood, 0.9f), "Terminal bench back");
            Place("bench_leg_l", new Vector3(21f, 0f, 31.6f), steel, "Terminal bench leg L");
            Place("bench_leg_r", new Vector3(21f, 0f, 31.6f), steel, "Terminal bench leg R");
            Place("planter", new Vector3(31.5f, 0f, 31.8f), AirsideTheme.Concrete, "Terminal planter");
            Place("planter_soil", new Vector3(31.5f, 0f, 31.8f), new Color(0.28f, 0.22f, 0.14f), "Terminal planter soil");
            Place("planter_scrub", new Vector3(31.5f, 0f, 31.8f), Shade(AirsideTheme.Eucalyptus, 0.85f), "Terminal planter scrub");
            // Prefer authored dropoff bollard; fall back to generic bollard mesh.
            if (!ArtGltfLoader.TryPlaceNamedMesh(kit, "dropoff_bollard", new Vector3(23.5f, 0f, 33.2f), Quaternion.identity, steel, out var dropL))
                Place("bollard", new Vector3(23.5f, 0f, 33.2f), steel, "Drop-off bollard L");
            else
            {
                dropL.name = "Drop-off bollard L";
                placed++;
            }

            if (!ArtGltfLoader.TryPlaceNamedMesh(kit, "dropoff_bollard", new Vector3(28.5f, 0f, 33.2f), Quaternion.identity, steel, out var dropR))
                Place("bollard", new Vector3(28.5f, 0f, 33.2f), steel, "Drop-off bollard R");
            else
            {
                dropR.name = "Drop-off bollard R";
                placed++;
            }

            Place("bollard_cap", new Vector3(23.5f, 0f, 33.2f), AirsideTheme.SafetyYellow, "Drop-off bollard cap L");
            Place("bollard_cap", new Vector3(28.5f, 0f, 33.2f), AirsideTheme.SafetyYellow, "Drop-off bollard cap R");
            Place("kerb_straight", new Vector3(26f, 0f, 33.6f), AirsideTheme.Concrete, "Drop-off kerb");
            Place("trolley_rail", new Vector3(33.5f, 0f, 30.8f), steel, "Trolley rail");
            Place("trolley_post_l", new Vector3(33.5f, 0f, 30.8f), steel, "Trolley post L");
            Place("trolley_post_r", new Vector3(33.5f, 0f, 30.8f), steel, "Trolley post R");
            Place("sign_post", new Vector3(39.5f, 0f, 40.5f), steel, "Parking sign post");
            Place("sign_face", new Vector3(39.5f, 0f, 40.5f), AirsideTheme.SafetyYellow, "Parking sign face");
            Place("sign_frame", new Vector3(39.5f, 0f, 40.5f), Shade(steel, 0.85f), "Parking sign frame");
            Place("kerb_straight", new Vector3(48f, 0f, 52.2f), AirsideTheme.Concrete, "Car park kerb N");
            Place("kerb_straight", new Vector3(48f, 0f, 39.8f), AirsideTheme.Concrete, "Car park kerb S");
            return placed >= 6;
        }

        /// <summary>Airside planter strip in front of terminal glass — PRP-003 parts.</summary>
        private static bool TryPlaceAirsidePlanterStrip()
        {
            var kit = PreferArtKit("Models/Props/mdl_terminal_forecourt_kit_v01.gltf");
            if (string.IsNullOrEmpty(kit) || !ArtGltfLoader.HasKit(kit))
                return false;

            var soil = new Color(0.28f, 0.22f, 0.16f);
            var placed = 0;
            void Place(string mesh, Vector3 pos, Color color, string name)
            {
                if (!ArtGltfLoader.TryPlaceNamedMesh(kit, mesh, pos, Quaternion.identity, color, out var part))
                    return;
                part.name = name;
                placed++;
            }

            // Three planter clusters along the airside glass edge.
            foreach (var x in new[] { 20f, 26f, 32f })
            {
                Place("planter", new Vector3(x, 0f, 24.4f), AirsideTheme.Concrete, $"Airside planter {x:0}");
                Place("planter_soil", new Vector3(x, 0f, 24.4f), soil, $"Airside planter soil {x:0}");
                Place("planter_scrub", new Vector3(x, 0f, 24.4f), Shade(AirsideTheme.Eucalyptus, 0.85f), $"Airside planter scrub {x:0}");
            }

            return placed >= 3;
        }

        private static void BuildVegetation()
        {
            // Stylised eucalyptus clumps — denser belts so overview reads as KI bush, not
            // a handful of props (0025 item 3). When VEG kits land, skip far densify carpets.
            var eucKit = PreferArtKit("Models/Environment/mdl_eucalyptus_kit_v01.gltf");
            var scrubKit = PreferArtKit("Models/Environment/mdl_kingscote_scrub_kit_v01.gltf");
            var hasEucKit = !string.IsNullOrEmpty(eucKit) && ArtGltfLoader.HasKit(eucKit);
            var hasScrubKit = !string.IsNullOrEmpty(scrubKit) && ArtGltfLoader.HasKit(scrubKit);

            var trees = new (Vector3 Pos, float Scale)[]
            {
                (new Vector3(-32f, 0f, 30f), 1.1f),
                (new Vector3(-38f, 0f, 22f), 0.9f),
                (new Vector3(-28f, 0f, 36f), 1.25f),
                (new Vector3(-42f, 0f, 34f), 1.05f),
                (new Vector3(-46f, 0f, 26f), 0.88f),
                (new Vector3(-24f, 0f, 40f), 0.95f),
                (new Vector3(40f, 0f, 30f), 1.0f),
                (new Vector3(52f, 0f, 34f), 1.15f),
                (new Vector3(58f, 0f, 28f), 0.85f),
                (new Vector3(46f, 0f, 40f), 1.05f),
                (new Vector3(36f, 0f, 52f), 1.2f),
                (new Vector3(62f, 0f, 42f), 0.92f),
                (new Vector3(-52f, 0f, 8f), 1.3f),
                (new Vector3(-48f, 0f, -8f), 0.95f),
                (new Vector3(-56f, 0f, -2f), 1.1f),
                (new Vector3(-44f, 0f, 14f), 0.82f),
                (new Vector3(50f, 0f, -10f), 1.05f),
                (new Vector3(56f, 0f, 8f), 0.9f),
                (new Vector3(62f, 0f, -4f), 1.15f),
                (new Vector3(54f, 0f, 18f), 0.78f),
                (new Vector3(-18f, 0f, 42f), 0.8f),
                (new Vector3(-8f, 0f, 46f), 0.95f),
                (new Vector3(8f, 0f, 44f), 1.05f),
                (new Vector3(16f, 0f, 50f), 0.88f),
                // South fringe above the dunes (keep clear of runway strip).
                (new Vector3(-40f, 0f, -28f), 0.9f),
                (new Vector3(-28f, 0f, -32f), 1.0f),
                (new Vector3(28f, 0f, -30f), 0.95f),
                (new Vector3(40f, 0f, -26f), 1.1f),
                (new Vector3(-60f, 0f, 20f), 1.2f),
                (new Vector3(68f, 0f, 16f), 1.05f),
                // Extra belt density so overview reads as continuous KI bush (0025 item 3).
                (new Vector3(-34f, 0f, 48f), 1.0f),
                (new Vector3(-20f, 0f, 52f), 1.15f),
                (new Vector3(4f, 0f, 54f), 0.9f),
                (new Vector3(22f, 0f, 50f), 1.05f),
                (new Vector3(44f, 0f, 56f), 1.2f),
                (new Vector3(-58f, 0f, 32f), 0.95f),
                (new Vector3(70f, 0f, 30f), 1.1f),
                (new Vector3(-64f, 0f, -10f), 1.05f),
                (new Vector3(66f, 0f, -14f), 0.88f),
                (new Vector3(-50f, 0f, -30f), 1.0f),
                (new Vector3(48f, 0f, -32f), 1.12f),
                // Far paddock belt — denser KI fringe from overview (0025 item 3).
                (new Vector3(-70f, 0f, 40f), 1.25f),
                (new Vector3(-66f, 0f, 50f), 1.05f),
                (new Vector3(74f, 0f, 38f), 1.15f),
                (new Vector3(72f, 0f, 48f), 0.95f),
                (new Vector3(-72f, 0f, 8f), 1.1f),
                (new Vector3(76f, 0f, 6f), 1.0f),
                (new Vector3(-12f, 0f, 58f), 1.2f),
                (new Vector3(30f, 0f, 58f), 1.08f),
                // Inland paddock densify — close the gaps between belts (0025 item 3).
                (new Vector3(-38f, 0f, 56f), 1.1f),
                (new Vector3(12f, 0f, 60f), 0.95f),
                (new Vector3(52f, 0f, 60f), 1.15f),
                (new Vector3(-68f, 0f, -22f), 1.05f),
                (new Vector3(70f, 0f, -20f), 0.9f),
                (new Vector3(-55f, 0f, 55f), 1.2f),
                (new Vector3(58f, 0f, 54f), 1.0f),
                (new Vector3(-25f, 0f, -36f), 0.85f),
                (new Vector3(18f, 0f, -34f), 1.0f),
                // Close N/E paddock holes from overview (0025 item 3).
                (new Vector3(-42f, 0f, 62f), 1.15f),
                (new Vector3(-15f, 0f, 62f), 1.05f),
                (new Vector3(6f, 0f, 64f), 0.92f),
                (new Vector3(26f, 0f, 62f), 1.1f),
                (new Vector3(48f, 0f, 64f), 1.0f),
                (new Vector3(64f, 0f, 58f), 1.18f),
                (new Vector3(-75f, 0f, 28f), 1.08f),
                (new Vector3(78f, 0f, 24f), 0.95f),
                (new Vector3(-8f, 0f, -38f), 0.88f),
                (new Vector3(36f, 0f, -36f), 1.02f)
            };
            // Core + extra belt always; far densify only when eucalyptus kit is missing.
            var treeCount = hasEucKit ? 41 : trees.Length;
            for (var i = 0; i < treeCount; i++)
                PlaceTree(trees[i].Pos, trees[i].Scale);

            // Low shrub / scrub clusters along fence and car-park edges.
            var shrubs = new[]
            {
                new Vector3(-30f, 0f, 33f), new Vector3(-22f, 0f, 35f), new Vector3(-14f, 0f, 33.5f),
                new Vector3(12f, 0f, 34.5f), new Vector3(34f, 0f, 33f), new Vector3(40f, 0f, 36f),
                new Vector3(54f, 0f, 50f), new Vector3(50f, 0f, 54f), new Vector3(60f, 0f, 44f),
                new Vector3(-50f, 0f, 4f), new Vector3(-54f, 0f, -12f), new Vector3(48f, 0f, -16f),
                new Vector3(-36f, 0f, -24f), new Vector3(32f, 0f, -22f), new Vector3(0f, 0f, 38f)
            };
            for (var i = 0; i < shrubs.Length; i++)
                PlaceShrub(shrubs[i], 0.7f + (i % 4) * 0.12f);

            // Extra inland scrub — thin when VEG-002 already stamps authored clumps.
            var inlandScrub = new[]
            {
                new Vector3(-62f, 0f, 44f), new Vector3(-58f, 0f, 52f), new Vector3(-45f, 0f, 58f),
                new Vector3(-30f, 0f, 58f), new Vector3(-5f, 0f, 56f), new Vector3(18f, 0f, 58f),
                new Vector3(40f, 0f, 58f), new Vector3(62f, 0f, 52f), new Vector3(68f, 0f, 42f),
                new Vector3(72f, 0f, 22f), new Vector3(70f, 0f, -8f), new Vector3(-70f, 0f, -6f),
                new Vector3(-66f, 0f, 18f), new Vector3(8f, 0f, 40f), new Vector3(-4f, 0f, 36f)
            };
            var inlandCount = hasScrubKit ? 7 : inlandScrub.Length;
            for (var i = 0; i < inlandCount; i++)
                PlaceShrub(inlandScrub[i], 0.75f + (i % 5) * 0.1f);

            // Fence-line scrub carpet — wider step when kit scrub owns the silhouette.
            var fenceStep = hasScrubKit ? 9 : 4;
            for (var x = -70; x <= 70; x += fenceStep)
            {
                PlaceShrubClump(new Vector3(x, 0f, 36f + (x % 5) * 0.2f), 0.55f + (Mathf.Abs(x) % 4) * 0.08f);
                if (x % (fenceStep * 2) == 0)
                    PlaceShrubClump(new Vector3(x + 1.5f, 0f, 40f), 0.7f);
            }

            var sideStep = hasScrubKit ? 10 : 5;
            for (var z = -20; z <= 50; z += sideStep)
            {
                PlaceShrubClump(new Vector3(-48f - (z % 3) * 0.4f, 0f, z), 0.6f + (Mathf.Abs(z) % 3) * 0.1f);
                PlaceShrubClump(new Vector3(50f + (z % 3) * 0.4f, 0f, z), 0.6f + (Mathf.Abs(z) % 3) * 0.1f);
            }

            // Between apron fringe and N fence.
            var fringeStep = hasScrubKit ? 6 : 3;
            for (var x = 6; x <= 34; x += fringeStep)
                PlaceShrubClump(new Vector3(x, 0f, 28.5f + (x % 2) * 0.4f), 0.5f);

            // Dense coastal scrub belt — prefer VEG-002 clumps over greybox cubes.
            var coastStep = hasScrubKit ? 8 : 5;
            for (var x = -55; x <= 55; x += coastStep)
            {
                var zJitter = ((x * 13) % 7) * 0.15f;
                PlaceShrub(new Vector3(x, 0f, -39.5f + zJitter), 0.7f + (Mathf.Abs(x) % 4) * 0.06f);
                if (x % (coastStep * 2) == 0)
                    PlaceShrub(new Vector3(x + 1.5f, 0f, -37.5f), 0.65f);
            }
        }

        private static void PlaceShrub(Vector3 basePosition, float scale)
        {
            PlaceShrubClump(basePosition, scale);
        }

        private static void PlaceShrubClump(Vector3 basePosition, float scale)
        {
            if (TryPlaceScrubFromKit(basePosition, scale))
                return;

            // Multi-sphere scrub clump so fence belts read as bumpy KI olive, not props.
            var colorA = Shade(AirsideTheme.DryGrass, 0.85f);
            var colorB = Shade(AirsideTheme.Eucalyptus, 0.72f);
            var colorC = Shade(AirsideTheme.DryGrass, 0.95f);
            PlaceShrubSphere(basePosition + new Vector3(0f, 0.4f * scale, 0f),
                new Vector3(1.35f * scale, 0.8f * scale, 1.15f * scale), colorA, "Shrub");
            PlaceShrubSphere(basePosition + new Vector3(0.45f * scale, 0.35f * scale, -0.3f * scale),
                new Vector3(0.95f * scale, 0.6f * scale, 0.85f * scale), colorB, "Shrub B");
            PlaceShrubSphere(basePosition + new Vector3(-0.4f * scale, 0.32f * scale, 0.25f * scale),
                new Vector3(0.85f * scale, 0.55f * scale, 0.75f * scale), Shade(colorA, 0.9f), "Shrub C");
            PlaceShrubSphere(basePosition + new Vector3(0.15f * scale, 0.28f * scale, 0.45f * scale),
                new Vector3(0.7f * scale, 0.45f * scale, 0.65f * scale), colorC, "Shrub D");
            PlaceShrubSphere(basePosition + new Vector3(-0.25f * scale, 0.25f * scale, -0.4f * scale),
                new Vector3(0.65f * scale, 0.4f * scale, 0.6f * scale), Shade(colorB, 0.88f), "Shrub E");
        }

        /// <summary>Batch F3 VEG-002 — place authored scrub cluster; sphere clumps remain fallback.</summary>
        private static bool TryPlaceScrubFromKit(Vector3 basePosition, float scale)
        {
            var kit = PreferArtKit("Models/Environment/mdl_kingscote_scrub_kit_v01.gltf");
            if (string.IsNullOrEmpty(kit) || !ArtGltfLoader.HasKit(kit))
                return false;

            var variants = new[] { "scrub_a", "scrub_b", "scrub_c", "scrub_d", "scrub_e" };
            var prefix = variants[Math.Abs(basePosition.GetHashCode()) % variants.Length];
            var yaw = (basePosition.x * 23f + basePosition.z * 11f) % 360f;
            var root = new GameObject($"Scrub {prefix}").transform;
            root.position = basePosition;
            root.rotation = Quaternion.Euler(0f, yaw, 0f);
            root.localScale = Vector3.one * scale;

            var dry = Shade(AirsideTheme.DryGrass, 0.85f);
            var euc = Shade(AirsideTheme.Eucalyptus, 0.72f);
            var placed = 0;
            void Place(string mesh, Color color)
            {
                if (!ArtGltfLoader.TryPlaceNamedMesh(kit, mesh, Vector3.zero, Quaternion.identity, color, out var part))
                    return;
                part.SetParent(root, false);
                part.localPosition = Vector3.zero;
                part.localRotation = Quaternion.identity;
                part.name = mesh;
                placed++;
            }

            Place($"{prefix}_core", dry);
            Place($"{prefix}_side", euc);
            Place($"{prefix}_side_b", Shade(dry, 0.9f));
            Place($"{prefix}_tuft", Shade(euc, 0.88f));
            if (Math.Abs(basePosition.GetHashCode()) % 5 == 0)
                Place("rock_a", new Color(0.45f, 0.4f, 0.32f));

            if (placed < 2)
            {
                Object.Destroy(root.gameObject);
                return false;
            }

            return true;
        }

        private static void PlaceShrubSphere(Vector3 position, Vector3 scale, Color color, string name)
        {
            var bush = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bush.name = name;
            Object.Destroy(bush.GetComponent<Collider>());
            bush.transform.position = position;
            bush.transform.localScale = scale;
            bush.GetComponent<Renderer>().material = AirsideMaterialLibrary.Create(
                color, AirsideMaterialLibrary.SurfaceKind.Grass);
        }

        private static void PlaceTree(Vector3 basePosition, float scale)
        {
            if (TryPlaceTreeFromKit(basePosition, scale))
                return;

            // Eucalyptus clump: tall thin trunk + staggered canopies + bark rings (REF overview).
            var yaw = (basePosition.x * 17f + basePosition.z * 13f) % 360f;
            var lean = ((basePosition.x + basePosition.z) % 9f) - 4f;
            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Tree trunk";
            Object.Destroy(trunk.GetComponent<Collider>());
            trunk.transform.position = basePosition + new Vector3(0f, 1.55f * scale, 0f);
            trunk.transform.localScale = new Vector3(0.22f * scale, 1.55f * scale, 0.22f * scale);
            trunk.transform.rotation = Quaternion.Euler(lean * 0.6f, yaw, lean * 0.35f);
            trunk.GetComponent<Renderer>().material = AirsideMaterialLibrary.Create(
                new Color(0.32f, 0.24f, 0.15f), AirsideMaterialLibrary.SurfaceKind.PaintedMetal);

            // Root flare + bark rings so trunks do not read as perfect cylinders.
            var flare = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            flare.name = "Tree flare";
            Object.Destroy(flare.GetComponent<Collider>());
            flare.transform.position = basePosition + new Vector3(0f, 0.12f * scale, 0f);
            flare.transform.localScale = new Vector3(0.42f * scale, 0.12f * scale, 0.42f * scale);
            flare.GetComponent<Renderer>().material = AirsideMaterialLibrary.Create(
                new Color(0.28f, 0.2f, 0.12f), AirsideMaterialLibrary.SurfaceKind.PaintedMetal);
            CreateBlock("Tree bark low", basePosition + new Vector3(0f, 0.85f * scale, 0f),
                new Vector3(0.28f * scale, 0.08f * scale, 0.28f * scale), new Color(0.38f, 0.28f, 0.16f));
            CreateBlock("Tree bark mid", basePosition + new Vector3(0f, 1.7f * scale, 0f),
                new Vector3(0.26f * scale, 0.07f * scale, 0.26f * scale), new Color(0.36f, 0.26f, 0.15f));

            // Secondary lean branch for eucalyptus silhouette.
            var fork = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            fork.name = "Tree fork";
            Object.Destroy(fork.GetComponent<Collider>());
            fork.transform.position = basePosition + new Vector3(0.25f * scale, 2.4f * scale, -0.15f * scale);
            fork.transform.localScale = new Vector3(0.12f * scale, 0.55f * scale, 0.12f * scale);
            fork.transform.rotation = Quaternion.Euler(18f + lean, yaw + 35f, -12f);
            fork.GetComponent<Renderer>().material = AirsideMaterialLibrary.Create(
                new Color(0.3f, 0.22f, 0.14f), AirsideMaterialLibrary.SurfaceKind.PaintedMetal);

            var canopyColorA = Shade(AirsideTheme.Eucalyptus, 0.9f);
            var canopyColorB = Shade(AirsideTheme.Eucalyptus, 0.78f);
            var canopyColorC = Shade(AirsideTheme.Eucalyptus, 0.7f);
            var canopyColorD = Shade(AirsideTheme.Eucalyptus, 0.82f);
            PlaceTreeCanopy(basePosition + new Vector3(0f, 3.35f * scale, 0f),
                new Vector3(2.0f * scale, 1.55f * scale, 1.9f * scale), canopyColorA, "Tree canopy");
            PlaceTreeCanopy(basePosition + new Vector3(0.65f * scale, 2.85f * scale, -0.45f * scale),
                new Vector3(1.45f * scale, 1.15f * scale, 1.35f * scale), canopyColorB, "Tree canopy B");
            PlaceTreeCanopy(basePosition + new Vector3(-0.55f * scale, 2.95f * scale, 0.5f * scale),
                new Vector3(1.25f * scale, 1.05f * scale, 1.2f * scale), canopyColorC, "Tree canopy C");
            PlaceTreeCanopy(basePosition + new Vector3(0.35f * scale, 3.55f * scale, 0.35f * scale),
                new Vector3(1.05f * scale, 0.85f * scale, 1.0f * scale), canopyColorD, "Tree canopy D");
            PlaceTreeCanopy(basePosition + new Vector3(-0.2f * scale, 2.55f * scale, -0.55f * scale),
                new Vector3(0.95f * scale, 0.75f * scale, 0.9f * scale), Shade(canopyColorB, 0.92f), "Tree canopy E");
        }

        /// <summary>Batch F3 VEG-001 — place authored eucalyptus silhouette; primitives remain fallback.</summary>
        private static bool TryPlaceTreeFromKit(Vector3 basePosition, float scale)
        {
            var kit = PreferArtKit("Models/Environment/mdl_eucalyptus_kit_v01.gltf");
            if (string.IsNullOrEmpty(kit) || !ArtGltfLoader.HasKit(kit))
                return false;

            var variants = new[] { "tree_a", "tree_b", "tree_c" };
            var prefix = variants[Math.Abs(basePosition.GetHashCode()) % variants.Length];
            var yaw = (basePosition.x * 17f + basePosition.z * 13f) % 360f;
            var lean = ((basePosition.x + basePosition.z) % 9f) - 4f;
            var root = new GameObject($"Eucalyptus {prefix}").transform;
            root.position = basePosition;
            root.rotation = Quaternion.Euler(lean * 0.35f, yaw, lean * 0.2f);
            root.localScale = Vector3.one * scale;

            var bark = new Color(0.32f, 0.24f, 0.15f);
            var canopyA = Shade(AirsideTheme.Eucalyptus, 0.9f);
            var canopyB = Shade(AirsideTheme.Eucalyptus, 0.78f);
            var placed = 0;
            void Place(string mesh, Color color)
            {
                if (!ArtGltfLoader.TryPlaceNamedMesh(kit, mesh, Vector3.zero, Quaternion.identity, color, out var part))
                    return;
                part.SetParent(root, false);
                part.localPosition = Vector3.zero;
                part.localRotation = Quaternion.identity;
                part.name = mesh.Replace($"{prefix}_", "Tree ");
                placed++;
            }

            Place($"{prefix}_trunk", bark);
            Place($"{prefix}_flare", Shade(bark, 0.85f));
            Place($"{prefix}_bark_low", new Color(0.38f, 0.28f, 0.16f));
            Place($"{prefix}_bark_mid", new Color(0.36f, 0.26f, 0.15f));
            Place($"{prefix}_fork", Shade(bark, 0.9f));
            Place($"{prefix}_canopy", canopyA);
            Place($"{prefix}_canopy_b", canopyB);
            Place($"{prefix}_canopy_c", Shade(canopyA, 0.85f));
            Place($"{prefix}_canopy_d", Shade(canopyB, 0.92f));

            if (placed < 4)
            {
                Object.Destroy(root.gameObject);
                return false;
            }

            return true;
        }

        private static void PlaceTreeCanopy(Vector3 position, Vector3 scale, Color color, string name)
        {
            var canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            canopy.name = name;
            Object.Destroy(canopy.GetComponent<Collider>());
            canopy.transform.position = position;
            canopy.transform.localScale = scale;
            canopy.GetComponent<Renderer>().material = AirsideMaterialLibrary.Create(
                color, AirsideMaterialLibrary.SurfaceKind.Grass);
        }

        /// <summary>
        /// Batch F3 WLD-004 — soft hill/dune accents outside operational geometry.
        /// Does not replace runway/apron/stand code-owned surfaces.
        /// </summary>
        private static bool TryPlaceContextTerrainAccents()
        {
            var kit = PreferArtKit("Models/Environment/mdl_kingscote_context_terrain_v01.gltf");
            if (string.IsNullOrEmpty(kit) || !ArtGltfLoader.HasKit(kit))
                return false;

            var placed = 0;
            void Place(string mesh, Vector3 pos, Quaternion rot, Color color, string name, float scale = 1f)
            {
                if (!ArtGltfLoader.TryPlaceNamedMesh(kit, mesh, pos, rot, color, out var part, localScale: Vector3.one * scale))
                    return;
                part.name = name;
                placed++;
            }

            var euc = Shade(AirsideTheme.Eucalyptus, 0.45f);
            var dry = Shade(AirsideTheme.DryGrass, 0.55f);
            var sand = Shade(AirsideTheme.Sand, 0.75f);
            Place("hill_a", new Vector3(-85f, 0f, 68f), Quaternion.identity, euc, "Context hill NW", 2.8f);
            Place("hill_b", new Vector3(90f, 0f, 62f), Quaternion.Euler(0f, 25f, 0f), dry, "Context hill NE", 2.6f);
            Place("hill_c", new Vector3(-95f, 0f, 8f), Quaternion.Euler(0f, 40f, 0f), euc, "Context hill W", 2.4f);
            Place("hill_a", new Vector3(100f, 0f, 4f), Quaternion.Euler(0f, -30f, 0f), dry, "Context hill E", 2.3f);
            Place("dune_a", new Vector3(-40f, 0f, -52f), Quaternion.identity, sand, "Context dune SW", 2.0f);
            Place("dune_b", new Vector3(35f, 0f, -50f), Quaternion.Euler(0f, 15f, 0f), sand, "Context dune SE", 1.9f);
            Place("berm", new Vector3(0f, 0f, -42f), Quaternion.identity, Shade(sand, 0.9f), "Context coast berm", 2.8f);
            // Near-field coast / paddock accents from the same WLD-004 kit (textured slabs remain).
            Place("coast_sand", new Vector3(-55f, -0.2f, -48f), Quaternion.identity, sand, "Context coast sand W", 1.8f);
            Place("coast_sand", new Vector3(55f, -0.2f, -48f), Quaternion.Euler(0f, 180f, 0f), sand, "Context coast sand E", 1.8f);
            Place("coast_shallows", new Vector3(-30f, -0.5f, -58f), Quaternion.identity, new Color(0.45f, 0.68f, 0.78f), "Context shallows W", 1.4f);
            Place("coast_shallows", new Vector3(30f, -0.5f, -58f), Quaternion.Euler(0f, 180f, 0f), new Color(0.45f, 0.68f, 0.78f), "Context shallows E", 1.4f);
            Place("coast_water", new Vector3(0f, -0.8f, -70f), Quaternion.identity, new Color(0.22f, 0.42f, 0.58f), "Context coast water", 2.2f);
            Place("paddock_n", new Vector3(-50f, -0.3f, 42f), Quaternion.identity, dry, "Context paddock NW", 2.1f);
            Place("paddock_s", new Vector3(50f, -0.3f, 42f), Quaternion.Euler(0f, 180f, 0f), dry, "Context paddock NE", 2.0f);
            Place("paddock_e", new Vector3(62f, -0.3f, -20f), Quaternion.identity, euc, "Context paddock E", 1.75f);
            Place("paddock_w", new Vector3(-62f, -0.3f, -18f), Quaternion.Euler(0f, 20f, 0f), euc, "Context paddock W", 1.75f);
            return placed > 0;
        }

        private static void BuildDistantHills()
        {
            // Batch F3 WLD-004 — authored hill/dune accents; far slabs only when accents miss.
            var accents = TryPlaceContextTerrainAccents();

            if (!accents)
            {
                // Textured far masses so the horizon is not a void (0025 item 3).
                CreateBlock("Hill far NW", new Vector3(-90f, 2f, 70f), new Vector3(50f, 8f, 28f), Shade(AirsideTheme.Eucalyptus, 0.4f),
                    "Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png", new Vector2(6f, 3f));
                CreateBlock("Hill far NE", new Vector3(95f, 1.5f, 65f), new Vector3(44f, 6f, 24f), Shade(AirsideTheme.DryGrass, 0.55f),
                    "Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png", new Vector2(5f, 2.5f));
                CreateBlock("Hill far W", new Vector3(-100f, 1.2f, 10f), new Vector3(30f, 5f, 40f), Shade(AirsideTheme.Eucalyptus, 0.35f),
                    "Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png", new Vector2(4f, 5f));
                CreateBlock("Hill far E", new Vector3(105f, 1.0f, 5f), new Vector3(28f, 4.5f, 36f), Shade(AirsideTheme.DryGrass, 0.5f),
                    "Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png", new Vector2(3.5f, 4.5f));
                CreateBlock("Hill far S", new Vector3(0f, 0.8f, -95f), new Vector3(70f, 3.5f, 18f), Shade(AirsideTheme.Sand, 0.75f),
                    "Textures/Surfaces/tx_sand_coast_basecolor_v01.png", new Vector2(8f, 2f));
                CreateBlock("Hill far NW ridge", new Vector3(-78f, 5.2f, 72f), new Vector3(22f, 3.5f, 12f), Shade(AirsideTheme.Eucalyptus, 0.48f),
                    "Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png", new Vector2(3f, 1.5f));
                CreateBlock("Hill far NE spur", new Vector3(108f, 3.2f, 58f), new Vector3(18f, 3.2f, 14f), Shade(AirsideTheme.DryGrass, 0.62f),
                    "Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png", new Vector2(2.5f, 1.5f));
                CreateBlock("Hill far W shoulder", new Vector3(-88f, 2.8f, -8f), new Vector3(16f, 3.5f, 18f), Shade(AirsideTheme.Eucalyptus, 0.42f),
                    "Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png", new Vector2(2f, 2.2f));
                CreateBlock("Hill far E shoulder", new Vector3(92f, 2.4f, -12f), new Vector3(14f, 3.0f, 16f), Shade(AirsideTheme.DryGrass, 0.58f),
                    "Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png", new Vector2(2f, 2f));
                CreateBlock("Hill far SW headland", new Vector3(-55f, 1.4f, -88f), new Vector3(28f, 4.2f, 14f),
                    Shade(AirsideTheme.Sand, 0.68f),
                    "Textures/Surfaces/tx_sand_coast_basecolor_v01.png", new Vector2(4f, 2f));
                CreateBlock("Hill far SE headland", new Vector3(58f, 1.2f, -90f), new Vector3(26f, 3.8f, 12f),
                    Shade(AirsideTheme.Sand, 0.72f),
                    "Textures/Surfaces/tx_sand_coast_basecolor_v01.png", new Vector2(3.5f, 1.8f));
                CreateBlock("Hill far N spur", new Vector3(12f, 3.8f, 78f), new Vector3(24f, 4.5f, 16f),
                    Shade(AirsideTheme.Eucalyptus, 0.5f),
                    "Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png", new Vector2(3f, 2f));
                return;
            }

            // Kit accents own the near horizon — one thin far sand ring only so the south
            // skyline does not open into a void behind the dunes.
            CreateBlock("Hill far S", new Vector3(0f, 0.6f, -102f), new Vector3(90f, 2.4f, 12f), Shade(AirsideTheme.Sand, 0.7f),
                "Textures/Surfaces/tx_sand_coast_basecolor_v01.png", new Vector2(10f, 1.5f));
        }

        private static void BuildHorizonDome()
        {
            // Soft inverted dome so the sky is not a flat camera clear-colour void.
            // Unlit-ish pale Open Sky; day/dusk still tint via camera background underneath.
            var dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dome.name = "Horizon dome";
            Object.Destroy(dome.GetComponent<Collider>());
            dome.transform.position = new Vector3(0f, 0f, 0f);
            dome.transform.localScale = new Vector3(260f, 120f, 260f);
            var material = AirsideMaterialLibrary.Create(AirsideTheme.OpenSky, AirsideMaterialLibrary.SurfaceKind.UnlitSky);
            // Render inside of the sphere.
            material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Front);
            dome.GetComponent<Renderer>().material = material;
            dome.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            dome.GetComponent<Renderer>().receiveShadows = false;
            BuildCloudBands();
            BuildSunAndMoonDiscs();
            BuildStarField();
            BuildBirdFlock();
        }

        private static void BuildStarField()
        {
            // Sparse night stars on the sky dome — presentation only (0025 item 5).
            var root = new GameObject("Star field").transform;
            var rng = new System.Random(31415);
            for (var i = 0; i < 72; i++)
            {
                var star = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                star.name = $"Star {i}";
                Object.Destroy(star.GetComponent<Collider>());
                star.transform.SetParent(root, false);
                // Hemisphere above the horizon, inward-facing.
                var yaw = (float)rng.NextDouble() * 360f;
                var pitch = 10f + (float)rng.NextDouble() * 72f;
                var dir = Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward;
                star.transform.position = dir.normalized * 118f;
                var s = 0.22f + (float)rng.NextDouble() * 0.42f;
                star.transform.localScale = Vector3.one * s;
                var bright = 0.65f + (float)rng.NextDouble() * 0.35f;
                var mat = AirsideMaterialLibrary.Create(
                    new Color(bright, bright, 0.95f * bright, 1f),
                    AirsideMaterialLibrary.SurfaceKind.UnlitSky);
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", new Color(bright, bright, 1f) * 1.35f);
                }

                star.GetComponent<Renderer>().material = mat;
                star.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                star.GetComponent<Renderer>().receiveShadows = false;
            }

            root.gameObject.SetActive(false);
        }

        private void UpdateOpsAntenna()
        {
            if (_opsAntennaDish == null)
            {
                _opsAntennaDish = GameObject.Find("antenna_dish")?.transform;
                if (_opsAntennaDish == null)
                    return;
            }

            // Slow dish sweep so the ops roof reads alive (0025 item 7).
            _opsAntennaDish.Rotate(Vector3.up, Time.unscaledDeltaTime * 18f, Space.World);
        }

        private void UpdateStarField()
        {
            if (_starFieldRoot == null)
            {
                var found = GameObject.Find("Star field");
                if (found != null)
                    _starFieldRoot = found.transform;
            }

            if (_starFieldRoot == null)
                return;

            var daylight = (float)_simulation.TimeOfDay.Daylight;
            var show = daylight < 0.35f;
            _starFieldRoot.gameObject.SetActive(show);
            if (!show)
                return;

            // Soft twinkle — alpha via emission intensity.
            var twinkle = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * AirsideReusableMotion.StarTwinkleHz);
            for (var i = 0; i < _starFieldRoot.childCount; i++)
            {
                var star = _starFieldRoot.GetChild(i);
                var renderer = star.GetComponent<Renderer>();
                if (renderer == null || !renderer.material.HasProperty("_EmissionColor"))
                    continue;
                var phase = 0.7f + 0.3f * Mathf.Sin(
                    Time.unscaledTime * (AirsideReusableMotion.StarTwinkleHz * 0.7f + i * 0.11f) + i);
                var c = new Color(phase, phase, 1f) * (twinkle * (1.1f - daylight));
                renderer.material.SetColor("_EmissionColor", c);
            }
        }

        private static void BuildSunAndMoonDiscs()
        {
            // Visible sun/moon discs so day cycle reads from overview (0025 item 5).
            var sun = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sun.name = "Sun disc";
            Object.Destroy(sun.GetComponent<Collider>());
            sun.transform.localScale = new Vector3(6.5f, 6.5f, 6.5f);
            var sunMat = AirsideMaterialLibrary.Create(
                new Color(1f, 0.92f, 0.65f, 1f),
                AirsideMaterialLibrary.SurfaceKind.UnlitSky);
            if (sunMat.HasProperty("_EmissionColor"))
            {
                sunMat.EnableKeyword("_EMISSION");
                sunMat.SetColor("_EmissionColor", new Color(1.4f, 1.1f, 0.55f));
            }

            sun.GetComponent<Renderer>().material = sunMat;
            sun.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sun.GetComponent<Renderer>().receiveShadows = false;

            var moon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            moon.name = "Moon disc";
            Object.Destroy(moon.GetComponent<Collider>());
            moon.transform.localScale = new Vector3(4.2f, 4.2f, 4.2f);
            var moonMat = AirsideMaterialLibrary.Create(
                new Color(0.82f, 0.86f, 0.95f, 1f),
                AirsideMaterialLibrary.SurfaceKind.UnlitSky);
            if (moonMat.HasProperty("_EmissionColor"))
            {
                moonMat.EnableKeyword("_EMISSION");
                moonMat.SetColor("_EmissionColor", new Color(0.55f, 0.6f, 0.75f));
            }

            moon.GetComponent<Renderer>().material = moonMat;
            moon.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            moon.GetComponent<Renderer>().receiveShadows = false;
            moon.SetActive(false);
        }

        private void UpdateSunAndMoonDiscs(float daylight, float warm, float elevation)
        {
            if (_sunDisc == null)
            {
                var found = GameObject.Find("Sun disc");
                if (found != null)
                    _sunDisc = found.transform;
            }

            if (_moonDisc == null)
            {
                var foundMoon = GameObject.Find("Moon disc");
                if (foundMoon != null)
                    _moonDisc = foundMoon.transform;
            }

            // Place discs opposite the light direction on a large sky sphere.
            var sunDir = _sun != null ? -_sun.transform.forward : Vector3.up;
            if (_sunDisc != null)
            {
                var showSun = daylight > 0.02f || elevation > -4f;
                _sunDisc.gameObject.SetActive(showSun);
                if (showSun)
                {
                    _sunDisc.position = sunDir.normalized * 95f + Vector3.up * 8f;
                    var sunColor = Color.Lerp(
                        new Color(1f, 0.55f, 0.28f),
                        new Color(1f, 0.95f, 0.78f),
                        Mathf.Clamp01(daylight));
                    sunColor = Color.Lerp(sunColor, new Color(1f, 0.7f, 0.4f), warm * 0.55f);
                    var renderer = _sunDisc.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        SetRendererColor(renderer, sunColor);
                        if (renderer.material.HasProperty("_EmissionColor"))
                            renderer.material.SetColor("_EmissionColor", sunColor * (1.1f + warm * 0.6f));
                    }

                    var scale = Mathf.Lerp(9.5f, 6.2f, daylight);
                    _sunDisc.localScale = Vector3.one * scale;
                }
            }

            if (_moonDisc != null)
            {
                var showMoon = daylight < 0.45f;
                _moonDisc.gameObject.SetActive(showMoon);
                if (showMoon)
                {
                    // Opposite hemisphere from the sun path.
                    var moonDir = Quaternion.Euler(0f, 180f, 0f) * sunDir;
                    if (moonDir.y < 0.05f)
                        moonDir.y = 0.15f;
                    _moonDisc.position = moonDir.normalized * 90f + Vector3.up * 6f;
                    var alpha = Mathf.Lerp(1f, 0.15f, daylight / 0.45f);
                    var renderer = _moonDisc.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        var c = new Color(0.82f, 0.86f, 0.95f, 1f) * alpha;
                        SetRendererColor(renderer, c);
                        if (renderer.material.HasProperty("_EmissionColor"))
                            renderer.material.SetColor("_EmissionColor", c * 0.7f);
                    }
                }
            }
        }

        private static void BuildCloudBands()
        {
            // Soft translucent cloud clusters so the sky reads layered — presentation only.
            // Keep the count calm for a miniature sky; UpdateCloudDrift thickens tint for weather.
            var cloudRoot = new GameObject("Cloud bands").transform;
            var umbraRoot = new GameObject("Cloud umbras").transform;
            var rng = new System.Random(90210);
            const int clusterCount = 9;
            for (var i = 0; i < clusterCount; i++)
            {
                var cluster = new GameObject($"Cloud {i}").transform;
                cluster.SetParent(cloudRoot, false);
                var x = (float)(rng.NextDouble() * 220f - 110f);
                var z = (float)(rng.NextDouble() * 200f - 100f);
                var y = 24f + (float)rng.NextDouble() * 26f;
                cluster.position = new Vector3(x, y, z);

                var sx = 16f + (float)rng.NextDouble() * 30f;
                var sy = 3.4f + (float)rng.NextDouble() * 4.5f;
                var sz = 9f + (float)rng.NextDouble() * 18f;
                var alpha = 0.14f + (float)rng.NextDouble() * 0.14f;
                var blobs = 1 + (i % 2);
                for (var b = 0; b < blobs; b++)
                {
                    var cloud = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    cloud.name = $"Cloud {i} blob {b}";
                    Object.Destroy(cloud.GetComponent<Collider>());
                    cloud.transform.SetParent(cluster, false);
                    cloud.transform.localPosition = new Vector3(
                        (b - 0.5f) * sx * 0.22f,
                        (b % 2) * sy * 0.15f,
                        (b - 0.25f) * sz * 0.12f);
                    cloud.transform.localScale = new Vector3(
                        sx * (0.65f + b * 0.14f),
                        sy * (0.75f + (b % 2) * 0.2f),
                        sz * (0.65f + b * 0.12f));
                    cloud.GetComponent<Renderer>().material = AirsideMaterialLibrary.Create(
                        new Color(0.95f, 0.96f, 0.98f, alpha),
                        AirsideMaterialLibrary.SurfaceKind.Default);
                    cloud.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    cloud.GetComponent<Renderer>().receiveShadows = false;
                }

                // Soft ground umbra under each cloud cluster — drifts with UpdateCloudDrift.
                var umbra = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                umbra.name = $"Cloud umbra {i}";
                Object.Destroy(umbra.GetComponent<Collider>());
                umbra.transform.SetParent(umbraRoot, false);
                umbra.transform.position = new Vector3(x, 0.06f, z);
                umbra.transform.localScale = new Vector3(sx * 0.9f, 0.02f, sz * 0.9f);
                var umbraMat = AirsideMaterialLibrary.Create(
                    new Color(0.05f, 0.07f, 0.1f, 0.18f),
                    AirsideMaterialLibrary.SurfaceKind.Default);
                umbra.GetComponent<Renderer>().material = umbraMat;
                umbra.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                umbra.GetComponent<Renderer>().receiveShadows = false;
            }
        }

        /// <summary>
        /// Soft contact blobs under primary buildings so Lit surfaces read grounded
        /// without waiting for a full shadow-cascade bake (0025 items 3+5).
        /// </summary>
        private static void BuildBuildingContactShadows()
        {
            // Soft, tight discs — oversized near-black cylinders read as ground patches at night.
            PlaceContactShadow("Terminal contact", new Vector3(26f, 0.035f, 27f), new Vector3(14f, 0.02f, 4.5f), 0.14f);
            PlaceContactShadow("Hangar contact", new Vector3(-20f, 0.035f, 20f), new Vector3(10f, 0.02f, 7f), 0.14f);
            PlaceContactShadow("Ops contact", new Vector3(-8f, 0.035f, 26f), new Vector3(5f, 0.02f, 3.5f), 0.12f);
            PlaceContactShadow("Fuel farm contact", new Vector3(-34f, 0.035f, 22f), new Vector3(5.5f, 0.015f, 4.5f), 0.1f);
            PlaceContactShadow("ARFF contact", new Vector3(-28f, 0.035f, 30f), new Vector3(5.5f, 0.015f, 4.5f), 0.1f);
            PlaceContactShadow("Canopy contact", new Vector3(26f, 0.035f, 31.5f), new Vector3(10f, 0.015f, 3.2f), 0.08f);
        }

        private static void PlaceContactShadow(string name, Vector3 position, Vector3 scale, float alpha)
        {
            var shadow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shadow.name = name;
            Object.Destroy(shadow.GetComponent<Collider>());
            shadow.transform.position = position;
            shadow.transform.localScale = scale;
            var color = new Color(0.05f, 0.06f, 0.08f, Mathf.Clamp01(alpha));
            // Alpha < 1 routes through URP transparent so discs do not stamp opaque black.
            var material = AirsideMaterialLibrary.Create(color, AirsideMaterialLibrary.SurfaceKind.Default);
            var renderer = shadow.GetComponent<Renderer>();
            renderer.material = material;
            SetRendererColor(renderer, color);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void CollectHangarDoorPanels()
        {
            _hangarDoorPanels.Clear();
            // Authored / kit hangar doors — slide L/R panels instead of a single greybox slab.
            foreach (var name in new[]
                     {
                         "door_panel_l", "door_panel_r", "door_rib_l", "door_rib_r",
                         "door_bar_l1", "door_bar_l2", "door_bar_l3", "door_bar_l4",
                         "door_bar_r1", "door_bar_r2", "door_bar_r3", "door_bar_r4",
                         "door_handle_l", "door_handle_r",
                         "door_warning_l", "door_warning_r"
                     })
            {
                var go = GameObject.Find(name);
                if (go == null)
                    continue;
                var t = go.transform;
                var openDelta = name.Contains("_l", StringComparison.Ordinal) ? -3.6f : 3.6f;
                _hangarDoorPanels.Add((t, t.localPosition.x, openDelta));
            }

            // Avoid stacking the procedural slab on top of authored hangar doors.
            if (_hangarDoorPanels.Count > 0 && _hangarDoor != null)
            {
                _hangarDoor.gameObject.SetActive(false);
                _hangarDoor = null;
            }
        }

        private void CollectCoastalMotionTargets()
        {
            _coastBoats.Clear();
            _coastFoamLayers.Clear();
            _coastWaterRenderers.Clear();
            _coastFoam = GameObject.Find("Coast foam")?.transform;
            _jettyDeck = GameObject.Find("Jetty deck")?.transform;
            foreach (var name in new[] { "Coast foam inner", "Coast foam outer" })
            {
                var foam = GameObject.Find(name);
                if (foam != null)
                    _coastFoamLayers.Add(foam.transform);
            }
            foreach (var name in new[]
                     {
                         "Coast boat A", "Coast boat B", "Coast boat C", "Coast boat D",
                         "Coast boat E", "Coast boat F"
                     })
            {
                var go = GameObject.Find(name);
                if (go == null)
                    continue;
                _coastBoats.Add((go.transform, go.transform.position, go.transform.eulerAngles.y));
            }

            foreach (var name in new[] { "Coast water", "Coast shallows" })
            {
                var go = GameObject.Find(name);
                if (go == null)
                    continue;
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null)
                    _coastWaterRenderers.Add(renderer);
            }
        }

        private void UpdateHangarDoor()
        {
            if (_hangarDoor == null && _hangarBayLight == null && _hangarDoorPanels.Count == 0)
                return;

            // Presentation-only: hangar door slides open by day, closes at night.
            // Also opens wider when a commercial aircraft is near the hangar apron.
            var daylight = (float)_simulation.TimeOfDay.Daylight;
            var openAmount = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((daylight - 0.15f) / 0.35f));
            for (var i = 0; i < _simulation.Flights.Count; i++)
            {
                var flight = _simulation.Flights[i];
                if (flight.Operation.Phase is AircraftPhase.TaxiIn or AircraftPhase.AtStand
                    or AircraftPhase.TaxiOut or AircraftPhase.Pushback)
                {
                    openAmount = Mathf.Max(openAmount, 0.85f);
                    break;
                }
            }

            if (_hangarDoor != null)
            {
                var targetX = Mathf.Lerp(_hangarDoorClosedX, _hangarDoorClosedX - 7.2f, openAmount);
                var pos = _hangarDoor.position;
                pos.x = Mathf.MoveTowards(pos.x, targetX, Time.unscaledDeltaTime * 1.8f);
                _hangarDoor.position = pos;
            }

            for (var i = 0; i < _hangarDoorPanels.Count; i++)
            {
                var (panel, closedX, openDelta) = _hangarDoorPanels[i];
                if (panel == null)
                    continue;
                var local = panel.localPosition;
                var target = closedX + openDelta * openAmount;
                local.x = Mathf.MoveTowards(local.x, target, Time.unscaledDeltaTime * 1.6f);
                panel.localPosition = local;
            }

            // Warm bay spill: brighter when the door is open by day; soft night work-light when closed.
            if (_hangarBayLight != null)
            {
                var daySpill = openAmount * 1.55f;
                var nightGlow = (1f - daylight) * 0.72f;
                _hangarBayLight.intensity = Mathf.Max(0.1f, daySpill + nightGlow);
                _hangarBayLight.color = Color.Lerp(
                    new Color(1f, 0.82f, 0.55f),
                    new Color(1f, 0.92f, 0.7f),
                    daylight);
                _hangarBayLight.color = Color.Lerp(
                    new Color(1f, 0.78f, 0.48f),
                    new Color(1f, 0.92f, 0.72f),
                    openAmount);
            }
        }

        /// <summary>
        /// Soft boat bob + foam pulse on the KI coast (0025 items 3+7). Presentation only.
        /// </summary>
        private void UpdateCoastalMotion()
        {
            var t = Time.unscaledTime;
            for (var i = 0; i < _coastBoats.Count; i++)
            {
                var (boat, basePos, baseYaw) = _coastBoats[i];
                if (boat == null)
                    continue;
                var bob = Mathf.Sin(t * AirsideReusableMotion.CoastBobHz + i * 1.4f) * 0.08f;
                var yawSway = Mathf.Sin(t * AirsideReusableMotion.CoastYawHz + i) * 2.2f;
                boat.position = basePos + new Vector3(0f, bob, 0f);
                boat.rotation = Quaternion.Euler(
                    Mathf.Sin(t * AirsideReusableMotion.CoastPitchHz + i) * 2.5f,
                    baseYaw + yawSway,
                    Mathf.Cos(t * AirsideReusableMotion.CoastRollHz + i * 0.8f) * 3f);
            }

            if (_coastFoam != null)
            {
                var pulse = 0.92f + 0.08f * Mathf.Sin(t * AirsideReusableMotion.FoamPulseHz);
                var scale = _coastFoam.localScale;
                scale.z = 2.2f * pulse;
                _coastFoam.localScale = scale;
                var renderer = _coastFoam.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var c = renderer.material.color;
                    c.a = 0.55f + 0.3f * (0.5f + 0.5f * Mathf.Sin(t * AirsideReusableMotion.FoamAlphaHz));
                    SetRendererColor(renderer, c);
                }
            }

            // Secondary foam ribbons pulse out of phase so the surf edge reads layered.
            for (var i = 0; i < _coastFoamLayers.Count; i++)
            {
                var foam = _coastFoamLayers[i];
                if (foam == null)
                    continue;
                var renderer = foam.GetComponent<Renderer>();
                if (renderer == null)
                    continue;
                var wave = 0.5f + 0.5f * Mathf.Sin(t * 1.8f + i * 1.7f);
                var c = renderer.material.color;
                c.a = 0.3f + 0.35f * wave;
                SetRendererColor(renderer, c);
                // Soft Z pulse so the surf edge breathes toward shore.
                var scale = foam.localScale;
                scale.z = (i == 0 ? 1.1f : 1.4f) * (0.92f + 0.1f * wave);
                foam.localScale = scale;
            }

            if (_jettyDeck != null)
            {
                var pos = _jettyDeck.position;
                pos.y = -0.15f + Mathf.Sin(t * 0.9f) * 0.02f;
                _jettyDeck.position = pos;
            }

            // Slow UV scroll + shallow bob so the KI coast reads as living water (0025 items 3+7).
            for (var i = 0; i < _coastWaterRenderers.Count; i++)
            {
                var renderer = _coastWaterRenderers[i];
                if (renderer == null)
                    continue;
                var mat = renderer.material;
                var scroll = new Vector2(t * (0.012f + i * 0.004f), t * 0.008f);
                mat.mainTextureOffset = scroll;
                if (mat.HasProperty("_BaseMap"))
                    mat.SetTextureOffset("_BaseMap", scroll);
                if (renderer.gameObject.name.IndexOf("shallow", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var p = renderer.transform.position;
                    p.y = -0.35f + Mathf.Sin(t * 0.65f + i) * 0.03f;
                    renderer.transform.position = p;
                }
            }
        }

        private void UpdateCloudDrift()
        {
            if (_cloudRoot == null)
            {
                var found = GameObject.Find("Cloud bands");
                if (found != null)
                    _cloudRoot = found.transform;
            }

            if (_cloudUmbraRoot == null)
            {
                var foundUmbra = GameObject.Find("Cloud umbras");
                if (foundUmbra != null)
                    _cloudUmbraRoot = foundUmbra.transform;
            }

            if (_cloudRoot == null)
                return;

            // Slow eastward drift + day tint so clouds feel alive without sim coupling.
            var daylight = (float)_simulation.TimeOfDay.Daylight;
            var drift = Time.unscaledDeltaTime * 0.35f;
            var weather = _simulation.CurrentWeather;
            var overcast = weather is WeatherKind.Overcast or WeatherKind.Rain or WeatherKind.Storm or WeatherKind.Fog;
            var cloudy = weather == WeatherKind.Cloudy;
            var thickSky = overcast || cloudy;
            var umbraAlpha = overcast
                ? Mathf.Lerp(0.06f, 0.18f, daylight)
                : cloudy
                    ? Mathf.Lerp(0.05f, 0.22f, daylight)
                    : Mathf.Lerp(0.04f, 0.26f, daylight);
            for (var i = 0; i < _cloudRoot.childCount; i++)
            {
                var cloud = _cloudRoot.GetChild(i);
                var p = cloud.position;
                p.x += drift;
                if (p.x > 100f)
                    p.x = -100f;
                cloud.position = p;

                var dusk = Mathf.Clamp01(Mathf.Min(daylight, 1f - daylight) * 3f);
                var tint = Color.Lerp(new Color(0.55f, 0.6f, 0.75f), new Color(0.95f, 0.96f, 0.98f), daylight);
                tint = Color.Lerp(tint, new Color(0.95f, 0.7f, 0.55f), dusk * 0.55f);
                if (thickSky)
                    tint = Color.Lerp(tint, new Color(0.62f, 0.66f, 0.72f), overcast ? 0.55f : 0.32f);
                var baseAlpha = overcast ? 0.42f : cloudy ? 0.32f : 0.22f;
                tint.a = Mathf.Lerp(baseAlpha * 0.85f, baseAlpha, daylight);

                // Cluster roots have no renderer — tint each blob child.
                if (cloud.childCount > 0)
                {
                    for (var b = 0; b < cloud.childCount; b++)
                    {
                        var blobRenderer = cloud.GetChild(b).GetComponent<Renderer>();
                        if (blobRenderer == null)
                            continue;
                        var color = blobRenderer.material.color;
                        var blobTint = tint;
                        if (color.a > 0.01f)
                            blobTint.a = Mathf.Max(tint.a, color.a * (thickSky ? (overcast ? 1.35f : 1.15f) : 1f));
                        SetRendererColor(blobRenderer, blobTint);
                    }
                }
                else
                {
                    var renderer = cloud.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        var color = renderer.material.color;
                        if (color.a > 0.01f)
                            tint.a = Mathf.Max(tint.a, color.a * (thickSky ? (overcast ? 1.35f : 1.15f) : 1f));
                        SetRendererColor(renderer, tint);
                    }
                }

                if (_cloudUmbraRoot == null || i >= _cloudUmbraRoot.childCount)
                    continue;
                var umbra = _cloudUmbraRoot.GetChild(i);
                umbra.position = new Vector3(p.x, 0.06f, p.z);
                umbra.rotation = Quaternion.identity;
                // Keep authored umbra footprint; only drift with the cluster.
                var umbraRenderer = umbra.GetComponent<Renderer>();
                if (umbraRenderer == null)
                    continue;
                var umbraColor = umbraRenderer.material.color;
                umbraColor.a = umbraAlpha;
                SetRendererColor(umbraRenderer, umbraColor);
            }
        }

        private static void BuildBirdFlock()
        {
            // Stylised coastal flock — body + hinged wing quads so flaps read from overview
            // (0025 item 7). Presentation only.
            var root = new GameObject("Bird flock").transform;
            var rng = new System.Random(4242);
            for (var i = 0; i < 28; i++)
            {
                var bird = new GameObject($"Bird {i}").transform;
                bird.SetParent(root, false);
                // Seed orbit phase in unused euler z for UpdateBirdFlock.
                bird.localEulerAngles = new Vector3(0f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f);

                var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                body.name = "Body";
                Object.Destroy(body.GetComponent<Collider>());
                body.transform.SetParent(bird, false);
                body.transform.localScale = new Vector3(0.12f, 0.05f, 0.55f);
                body.GetComponent<Renderer>().material = AirsideMaterialLibrary.Create(
                    new Color(0.1f, 0.1f, 0.12f),
                    AirsideMaterialLibrary.SurfaceKind.Plastic);
                body.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                PlaceBirdWing(bird, "Wing L", new Vector3(-0.22f, 0.02f, 0.05f), true);
                PlaceBirdWing(bird, "Wing R", new Vector3(0.22f, 0.02f, 0.05f), false);
            }
        }

        private static void PlaceBirdWing(Transform bird, string name, Vector3 localPos, bool left)
        {
            var wing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wing.name = name;
            Object.Destroy(wing.GetComponent<Collider>());
            wing.transform.SetParent(bird, false);
            wing.transform.localPosition = localPos;
            wing.transform.localScale = new Vector3(0.55f, 0.02f, 0.14f);
            wing.GetComponent<Renderer>().material = AirsideMaterialLibrary.Create(
                new Color(0.18f, 0.18f, 0.2f),
                AirsideMaterialLibrary.SurfaceKind.Plastic);
            wing.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // Pivot hint stored as unused local euler y sign for flap direction.
            wing.transform.localEulerAngles = new Vector3(0f, left ? -8f : 8f, 0f);
        }

        private void UpdateBirdFlock()
        {
            if (_birdFlockRoot == null)
            {
                var found = GameObject.Find("Bird flock");
                if (found != null)
                    _birdFlockRoot = found.transform;
            }

            if (_birdFlockRoot == null)
                return;

            // Wide lazy orbit south of the runway — presentation flock, not wildlife sim.
            var t = Time.unscaledTime * AirsideReusableMotion.BirdOrbitHz * Mathf.PI * 2f;
            for (var i = 0; i < _birdFlockRoot.childCount; i++)
            {
                var bird = _birdFlockRoot.GetChild(i);
                var phase = bird.localEulerAngles.z * Mathf.Deg2Rad + t + i * 0.35f;
                var radius = 26f + (i % 5) * 3.2f;
                var x = Mathf.Cos(phase) * radius + (i % 3) * 1.5f;
                var z = -42f + Mathf.Sin(phase) * radius * 0.45f;
                var y = 8.5f + Mathf.Sin(phase * 2.1f + i) * 1.8f + (i % 3) * 0.8f;
                var next = new Vector3(x, y, z);
                var prev = bird.position;
                bird.position = next;
                var dir = next - prev;
                if (dir.sqrMagnitude > 0.0001f)
                    bird.rotation = Quaternion.Slerp(bird.rotation, Quaternion.LookRotation(dir.normalized), Time.unscaledDeltaTime * 4f);

                // Hinged wing flaps — readable silhouette from overview.
                var flap = Mathf.Sin(Time.unscaledTime * AirsideReusableMotion.BirdFlapHz * Mathf.PI * 2f + i * 0.7f) * 38f;
                for (var c = 0; c < bird.childCount; c++)
                {
                    var child = bird.GetChild(c);
                    if (child.name.StartsWith("Wing L", StringComparison.Ordinal))
                        child.localRotation = Quaternion.Euler(0f, -8f, flap);
                    else if (child.name.StartsWith("Wing R", StringComparison.Ordinal))
                        child.localRotation = Quaternion.Euler(0f, 8f, -flap);
                }
            }
        }

        /// <summary>Darken an opaque palette colour without dropping alpha into the transparent path.</summary>
        private static Color Shade(Color color, float factor) =>
            new Color(color.r * factor, color.g * factor, color.b * factor, 1f);

        private static void BuildStandMarking(float x, float z, string name)
        {
            // Markings kit already paints stand_stop + digits + chevrons — skip yellow densify.
            if (GameObject.Find("stand_stop_a") != null
                || GameObject.Find("stand_stop_b") != null
                || GameObject.Find("stand_stop_c") != null)
                return;

            CreateBlock(name, new Vector3(x, 0.08f, z), new Vector3(0.18f, 0.03f, 4.2f), new Color(0.96f, 0.77f, 0.12f));
            CreateBlock($"{name} stop", new Vector3(x, 0.08f, z + 1.9f), new Vector3(3.4f, 0.03f, 0.18f), new Color(0.96f, 0.77f, 0.12f));
        }

        private static Transform BuildAircraft(string name, Color accent, string liveryDecalRelativePath = null)
        {
            var root = new GameObject(name).transform;
            // Batch C AIR-001: metre-scale turboprop kit. Motion roots still use y=0.7, so
            // offset the kit by -0.7 so gear sits on the ground. Primitive fallback below.
            var usedArt = ArtPresentationLoader.TryInstantiate(
                PreferArtKit(
                    "Models/Aircraft/mdl_regional_turboprop_01_v06.gltf",
                    "Models/Aircraft/mdl_regional_turboprop_01_v05.gltf",
                    "Models/Aircraft/mdl_regional_turboprop_01_authored_v01.gltf",
                    "Models/Aircraft/mdl_regional_turboprop_01_lofted_v01.gltf",
                    "Models/Aircraft/mdl_regional_turboprop_01_v04.gltf",
                    "Models/Aircraft/mdl_regional_turboprop_01_v03.gltf",
                    "Models/Aircraft/mdl_regional_turboprop_01_v02.gltf",
                    "Models/Aircraft/mdl_regional_turboprop_01_v01.gltf"),
                root,
                out _,
                RenameAircraftPart,
                kitName => AircraftPartColor(kitName, accent),
                localPosition: new Vector3(0f, -0.7f, 0f));

            if (usedArt)
            {
                NestCrossPropellerBlades(root);
                // glTF kits author prop verts at nacelle world positions while the
                // Propeller transform sits at the kit origin — rebake so spin stays on-hub.
                RebakePropellerPivots(root);
                NestLandingGearParts(root);
                NestCabinDoorParts(root);
                NestFlapParts(root);
            }

            if (!usedArt)
            {
                // Batch C turboprop silhouette with separated props/engines/gear (primitive fallback).
                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "Fuselage";
                body.transform.SetParent(root, false);
                body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                body.transform.localScale = new Vector3(0.72f, 2.8f, 0.72f);
                body.GetComponent<Renderer>().material = CreateMaterial(new Color(0.93f, 0.95f, 0.97f));
                ParentBlock(root, "Livery stripe", new Vector3(0f, 0.12f, 0.05f), new Vector3(0.76f, 0.08f, 2.2f), accent);
                ParentBlock(root, "Wing L", new Vector3(-2.1f, 0.05f, 0.35f), new Vector3(3.6f, 0.12f, 1.5f), accent);
                ParentBlock(root, "Wing R", new Vector3(2.1f, 0.05f, 0.35f), new Vector3(3.6f, 0.12f, 1.5f), accent);
                ParentBlock(root, "Engine L", new Vector3(-1.35f, -0.05f, 0.85f), new Vector3(0.45f, 0.45f, 1.1f), accent * 0.85f);
                ParentBlock(root, "Engine R", new Vector3(1.35f, -0.05f, 0.85f), new Vector3(0.45f, 0.45f, 1.1f), accent * 0.85f);
                ParentBlock(root, "Propeller L", new Vector3(-1.35f, -0.05f, 1.45f), new Vector3(0.08f, 1.35f, 0.18f), new Color(0.2f, 0.2f, 0.22f));
                ParentBlock(root, "Propeller R", new Vector3(1.35f, -0.05f, 1.45f), new Vector3(0.08f, 1.35f, 0.18f), new Color(0.2f, 0.2f, 0.22f));
                ParentBlock(root, "Tail", new Vector3(0f, 0.85f, -2.15f), new Vector3(0.14f, 1.5f, 1.0f), accent);
                ParentBlock(root, "Tailplane", new Vector3(0f, 0.55f, -2.2f), new Vector3(2.2f, 0.1f, 0.7f), accent);
                ParentBlock(root, "Gear nose", new Vector3(0f, -0.55f, 1.5f), new Vector3(0.12f, 0.45f, 0.28f), new Color(0.25f, 0.25f, 0.28f));
                ParentBlock(root, "Gear L", new Vector3(-0.7f, -0.55f, -0.2f), new Vector3(0.12f, 0.45f, 0.32f), new Color(0.25f, 0.25f, 0.28f));
                ParentBlock(root, "Gear R", new Vector3(0.7f, -0.55f, -0.2f), new Vector3(0.12f, 0.45f, 0.32f), new Color(0.25f, 0.25f, 0.28f));
                ParentBlock(root, "CabinDoor", new Vector3(0.55f, 0.05f, 0.35f), new Vector3(0.08f, 0.85f, 0.55f), new Color(0.78f, 0.8f, 0.83f));
            }

            ApplyLiveryDecal(root, liveryDecalRelativePath);
            EnsurePropDiscs(root);
            EnsureGroundShadow(root);
            // Only inject lamp / heat proxies when the authored kit did not already ship them.
            if (!HasNamedChild(root, "NavLight L"))
                ParentBlock(root, "NavLight L", new Vector3(-3.7f, 0.08f, 0.2f), new Vector3(0.12f, 0.12f, 0.12f), new Color(0.1f, 0.9f, 0.2f));
            if (!HasNamedChild(root, "NavLight R"))
                ParentBlock(root, "NavLight R", new Vector3(3.7f, 0.08f, 0.2f), new Vector3(0.12f, 0.12f, 0.12f), new Color(0.9f, 0.12f, 0.12f));
            if (!HasNamedChild(root, "Beacon"))
                ParentBlock(root, "Beacon", new Vector3(0f, 0.85f, 0.2f), new Vector3(0.14f, 0.14f, 0.14f), new Color(0.95f, 0.2f, 0.15f));
            if (!HasNamedChild(root, "LandingLight") && !HasNamedChild(root, "LandingLight L"))
                ParentBlock(root, "LandingLight", new Vector3(0f, -0.15f, 2.5f), new Vector3(0.18f, 0.12f, 0.2f), new Color(0.95f, 0.95f, 0.85f));
            if (!HasNamedChild(root, "TaxiLight"))
                ParentBlock(root, "TaxiLight", new Vector3(0f, -0.2f, 2.2f), new Vector3(0.14f, 0.1f, 0.16f), new Color(0.95f, 0.92f, 0.7f));
            if (!HasNamedChild(root, "EngineHeat L") && !HasNamedChild(root, "EngineHeat R"))
            {
                // Batch F4 VFX-002 — prefer reusable heat kit; fall back to translucent quads.
                if (ArtPresentationLoader.TryInstantiatePrefab("vfx_engine_heat_v01", out var heatKit))
                {
                    while (heatKit.childCount > 0)
                    {
                        var child = heatKit.GetChild(0);
                        child.SetParent(root, false);
                    }

                    Object.Destroy(heatKit.gameObject);
                }
                else
                {
                    ParentBlock(root, "EngineHeat L", new Vector3(-1.35f, -0.05f, 0.15f), new Vector3(0.35f, 0.35f, 0.7f), new Color(0.95f, 0.55f, 0.2f, 0.15f));
                    ParentBlock(root, "EngineHeat R", new Vector3(1.35f, -0.05f, 0.15f), new Vector3(0.35f, 0.35f, 0.7f), new Color(0.95f, 0.55f, 0.2f, 0.15f));
                }
            }

            var source = root.gameObject.AddComponent<AudioSource>();
            source.clip = CreateEngineClip();
            source.loop = true;
            source.volume = 0.11f;
            source.spatialBlend = 0.75f;
            source.minDistance = 8f;
            source.maxDistance = 75f;
            source.Play();
            return root;
        }

        private static string RenameAircraftPart(string kitName) => kitName switch
        {
            "fuselage" => "Fuselage",
            "fuselage_mid" => "Fuselage mid",
            "fuselage_aft" => "Fuselage aft",
            "cabin_ring_fwd" => "Fuselage",
            "cabin_ring_mid" => "Fuselage mid",
            "cabin_ring_aft" => "Fuselage aft",
            "cabin_ring_tail" => "Fuselage aft",
            "tail_cone" => "Fuselage aft",
            "belly_fairing" => "Belly fairing",
            "nose" => "Nose",
            "nose_tip" => "Nose",
            "nose_ring_a" => "Nose",
            "nose_ring_b" => "Nose",
            "radome" => "Radome",
            "cockpit" => "Cockpit",
            "cockpit_loft" => "Cockpit",
            "cockpit_frame" => "Cockpit frame",
            "cabin_windows" => "Cabin windows",
            "cabin_window_band" => "Cabin window band",
            "cabin_window_1" => "Cabin window 1",
            "cabin_window_2" => "Cabin window 2",
            "cabin_window_3" => "Cabin window 3",
            "cabin_window_4" => "Cabin window 4",
            "cabin_window_5" => "Cabin window 5",
            "cabin_window_6" => "Cabin window 6",
            "cabin_window_7" => "Cabin window 7",
            "cabin_window_r1" => "Cabin window R1",
            "cabin_window_r2" => "Cabin window R2",
            "cabin_window_r3" => "Cabin window R3",
            "cabin_window_r4" => "Cabin window R4",
            "cabin_window_r5" => "Cabin window R5",
            "cabin_window_r6" => "Cabin window R6",
            "cabin_window_r7" => "Cabin window R7",
            "cabin_window_frame_1" => "Cabin window frame 1",
            "cabin_window_frame_3" => "Cabin window frame 3",
            "cabin_window_frame_5" => "Cabin window frame 5",
            "cabin_window_frame_7" => "Cabin window frame 7",
            "cabin_window_frame_r1" => "Cabin window frame R1",
            "cabin_window_frame_r2" => "Cabin window frame R2",
            "cabin_window_frame_r3" => "Cabin window frame R3",
            "cabin_window_frame_r4" => "Cabin window frame R4",
            "cabin_window_frame_r5" => "Cabin window frame R5",
            "cabin_window_frame_r7" => "Cabin window frame R7",
            "cockpit_glare" => "Cockpit glare",
            "windscreen_c" => "Windscreen C",
            "windscreen_l" => "Windscreen L",
            "windscreen_r" => "Windscreen R",
            "windscreen_pillar_l" => "Windscreen pillar L",
            "windscreen_pillar_r" => "Windscreen pillar R",
            "windscreen_pillar_c" => "Windscreen pillar C",
            "livery_stripe" => "Livery stripe",
            "livery_stripe_lower" => "Livery stripe lower",
            "livery_tail_sweep" => "Livery tail sweep",
            "door_frame_fwd" => "Door frame",
            "door_handle_fwd" => "Door handle",
            "inspection_panel_fwd" => "Inspection panel fwd",
            "inspection_panel_aft" => "Inspection panel aft",
            "cargo_sill" => "Cargo sill",
            "wing_fence_left" => "Wing fence L",
            "wing_fence_right" => "Wing fence R",
            "wing_fence_mid_l" => "Wing fence mid L",
            "wing_fence_mid_r" => "Wing fence mid R",
            "static_wick_left" => "Static wick L",
            "static_wick_right" => "Static wick R",
            "prop_hub_left" => "Prop hub L",
            "prop_hub_right" => "Prop hub R",
            "hub_cap_left" => "Hub cap L",
            "hub_cap_right" => "Hub cap R",
            "tailplane_tip_l" => "Tailplane tip L",
            "tailplane_tip_r" => "Tailplane tip R",
            "vor_antenna" => "VOR antenna",
            "wing_left" => "Wing L",
            "wing_right" => "Wing R",
            "wing_root_left" => "Wing root L",
            "wing_root_right" => "Wing root R",
            "wing_fairing_left" => "Wing fairing L",
            "wing_fairing_right" => "Wing fairing R",
            "flap_left" => "Flap L",
            "flap_right" => "Flap R",
            "flap_track_l1" => "Flap track L1",
            "flap_track_l2" => "Flap track L2",
            "flap_track_r1" => "Flap track R1",
            "flap_track_r2" => "Flap track R2",
            "flap_fairing_l" => "Flap fairing L",
            "flap_fairing_r" => "Flap fairing R",
            "spoiler_left" => "Spoiler L",
            "spoiler_right" => "Spoiler R",
            "aileron_left" => "Aileron L",
            "aileron_right" => "Aileron R",
            "wingtip_left" => "Wingtip L",
            "wingtip_right" => "Wingtip R",
            "winglet_left" => "Winglet L",
            "winglet_right" => "Winglet R",
            "engine_left" => "Engine L",
            "engine_right" => "Engine R",
            "pylon_left" => "Pylon L",
            "pylon_right" => "Pylon R",
            "nacelle_left" => "Nacelle L",
            "nacelle_right" => "Nacelle R",
            "intake_left" => "Intake L",
            "intake_right" => "Intake R",
            "exhaust_left" => "Exhaust L",
            "exhaust_right" => "Exhaust R",
            "exhaust_stack_l" => "Exhaust stack L",
            "exhaust_stack_r" => "Exhaust stack R",
            "oil_cooler_l" => "Oil cooler L",
            "oil_cooler_r" => "Oil cooler R",
            "cowl_flap_l" => "Cowl flap L",
            "cowl_flap_r" => "Cowl flap R",
            "propeller_left" => "Propeller L",
            "propeller_right" => "Propeller R",
            "propeller_left_b" => "PropBlade L",
            "propeller_right_b" => "PropBlade R",
            "propeller_left_c" => "PropBlade L2",
            "propeller_right_c" => "PropBlade R2",
            "propeller_left_d" => "PropBlade L3",
            "propeller_right_d" => "PropBlade R3",
            "propeller_left_e" => "PropBlade L4",
            "propeller_right_e" => "PropBlade R4",
            "propeller_left_f" => "PropBlade L5",
            "propeller_right_f" => "PropBlade R5",
            "propeller_left_tip" => "PropTip L",
            "propeller_right_tip" => "PropTip R",
            "propeller_left_tip_b" => "PropTip L2",
            "propeller_right_tip_b" => "PropTip R2",
            "propeller_left_tip_c" => "PropTip L3",
            "propeller_right_tip_c" => "PropTip R3",
            "propeller_left_tip_d" => "PropTip L4",
            "propeller_right_tip_d" => "PropTip R4",
            "propeller_left_tip_e" => "PropTip L5",
            "propeller_right_tip_e" => "PropTip R5",
            "propeller_left_tip_f" => "PropTip L6",
            "propeller_right_tip_f" => "PropTip R6",
            "spinner_left" => "Spinner L",
            "spinner_right" => "Spinner R",
            "spinner_stripe_l" => "Spinner stripe L",
            "spinner_stripe_r" => "Spinner stripe R",
            "tail_fin" => "Tail",
            "tail_fin_tip" => "Tail tip",
            "tailplane" => "Tailplane",
            "dorsal_fin" => "Dorsal fin",
            "hf_antenna" => "HF antenna",
            "tail_nav_light" => "Tail nav light",
            "elevator_left" => "Elevator L",
            "elevator_right" => "Elevator R",
            "rudder" => "Rudder",
            "gear_nose" => "Gear nose",
            "gear_left" => "Gear L",
            "gear_right" => "Gear R",
            "gear_oleo_nose" => "Gear oleo nose",
            "gear_oleo_left" => "Gear oleo L",
            "gear_oleo_right" => "Gear oleo R",
            "gear_scissors_nose" => "Gear scissors nose",
            "gear_scissors_left" => "Gear scissors L",
            "gear_scissors_right" => "Gear scissors R",
            "gear_door_nose" => "Gear door nose",
            "gear_door_left" => "Gear door L",
            "gear_door_right" => "Gear door R",
            "tire_nose" => "Tire nose",
            "tire_left" => "Tire L",
            "tire_right" => "Tire R",
            "wheel_nose" => "Wheel nose",
            "wheel_left" => "Wheel L",
            "wheel_right" => "Wheel R",
            "rim_nose" => "Rim nose",
            "rim_left" => "Rim L",
            "rim_right" => "Rim R",
            "door_fwd" => "CabinDoor",
            "cargo_door" => "Cargo door",
            "cargo_door_latch" => "Cargo door latch",
            "antenna" => "Antenna",
            "antenna_aft" => "Antenna aft",
            "pitot" => "Pitot",
            "pitot_b" => "Pitot B",
            "nav_light_left" => "NavLight L",
            "nav_light_right" => "NavLight R",
            "beacon_top" => "Beacon",
            "landing_light_l" => "LandingLight L",
            "landing_light_r" => "LandingLight R",
            "taxi_light" => "TaxiLight",
            _ => kitName
        };

        private static Color? AircraftPartColor(string kitName, Color accent) => kitName switch
        {
            "fuselage" or "fuselage_mid" or "fuselage_aft"
                or "cabin_ring_fwd" or "cabin_ring_mid" or "cabin_ring_aft" or "cabin_ring_tail" or "tail_cone"
                or "nose" or "nose_tip" or "nose_ring_a" or "nose_ring_b" or "radome"
                or "belly_fairing" or "cargo_door" or "door_frame_fwd" => new Color(0.93f, 0.95f, 0.97f),
            "cockpit" or "cockpit_loft" or "cabin_windows" or "cabin_window_band"
                or "cabin_window_1" or "cabin_window_2" or "cabin_window_3" or "cabin_window_4" or "cabin_window_5"
                or "cabin_window_6" or "cabin_window_7"
                or "cabin_window_r1" or "cabin_window_r2" or "cabin_window_r3" or "cabin_window_r4" or "cabin_window_r5"
                or "cabin_window_r6" or "cabin_window_r7" or "cockpit_glare"
                or "windscreen_c" or "windscreen_l" or "windscreen_r"
                => new Color(0.18f, 0.35f, 0.48f, 0.42f),
            "cabin_window_frame_1" or "cabin_window_frame_3" or "cabin_window_frame_5" or "cabin_window_frame_7"
                or "cabin_window_frame_r1" or "cabin_window_frame_r2" or "cabin_window_frame_r3"
                or "cabin_window_frame_r4" or "cabin_window_frame_r5" or "cabin_window_frame_r7"
                or "cockpit_frame" or "windscreen_pillar_l" or "windscreen_pillar_r" or "windscreen_pillar_c"
                => new Color(0.75f, 0.78f, 0.82f),
            "livery_stripe" or "livery_stripe_lower" or "livery_tail_sweep" => new Color(0.15f, 0.35f, 0.65f),
            "door_handle_fwd" or "cargo_door_latch" or "cargo_sill" => new Color(0.72f, 0.74f, 0.78f),
            "inspection_panel_fwd" or "inspection_panel_aft" => new Color(0.86f, 0.88f, 0.90f),
            "wing_left" or "wing_right" or "wing_root_left" or "wing_root_right"
                or "wing_fairing_left" or "wing_fairing_right"
                or "wingtip_left" or "wingtip_right" or "winglet_left" or "winglet_right"
                or "wing_fence_left" or "wing_fence_right" or "wing_fence_mid_l" or "wing_fence_mid_r"
                or "flap_left" or "flap_right" or "flap_fairing_l" or "flap_fairing_r"
                or "spoiler_left" or "spoiler_right"
                or "aileron_left" or "aileron_right"
                or "tail_fin" or "tail_fin_tip" or "tailplane" or "dorsal_fin"
                or "tailplane_tip_l" or "tailplane_tip_r"
                or "elevator_left" or "elevator_right" or "rudder" => accent,
            "flap_track_l1" or "flap_track_l2" or "flap_track_r1" or "flap_track_r2"
                => new Color(0.32f, 0.34f, 0.38f),
            "engine_left" or "engine_right" or "pylon_left" or "pylon_right"
                or "nacelle_left" or "nacelle_right"
                or "intake_left" or "intake_right"
                or "oil_cooler_l" or "oil_cooler_r" or "cowl_flap_l" or "cowl_flap_r"
                => new Color(0.15f, 0.38f, 0.55f),
            "exhaust_left" or "exhaust_right" or "exhaust_stack_l" or "exhaust_stack_r"
                => new Color(0.35f, 0.36f, 0.38f),
            "propeller_left" or "propeller_right" or "propeller_left_b" or "propeller_right_b"
                or "propeller_left_c" or "propeller_right_c"
                or "propeller_left_d" or "propeller_right_d"
                or "propeller_left_e" or "propeller_right_e"
                or "propeller_left_f" or "propeller_right_f"
                or "spinner_left" or "spinner_right" or "prop_hub_left" or "prop_hub_right"
                or "hub_cap_left" or "hub_cap_right"
                => new Color(0.2f, 0.2f, 0.22f),
            "propeller_left_tip" or "propeller_right_tip"
                or "propeller_left_tip_b" or "propeller_right_tip_b"
                or "propeller_left_tip_c" or "propeller_right_tip_c"
                or "propeller_left_tip_d" or "propeller_right_tip_d"
                or "propeller_left_tip_e" or "propeller_right_tip_e"
                or "propeller_left_tip_f" or "propeller_right_tip_f"
                => new Color(0.92f, 0.78f, 0.18f),
            "spinner_stripe_l" or "spinner_stripe_r" => new Color(0.92f, 0.55f, 0.12f),
            "gear_nose" or "gear_left" or "gear_right"
                or "gear_oleo_nose" or "gear_oleo_left" or "gear_oleo_right"
                or "gear_scissors_nose" or "gear_scissors_left" or "gear_scissors_right"
                or "gear_door_nose" or "gear_door_left" or "gear_door_right" => new Color(0.25f, 0.25f, 0.28f),
            "tire_nose" or "tire_left" or "tire_right" => new Color(0.12f, 0.12f, 0.13f),
            "rim_nose" or "rim_left" or "rim_right"
                or "wheel_nose" or "wheel_left" or "wheel_right" => new Color(0.55f, 0.56f, 0.58f),
            "door_fwd" => new Color(0.78f, 0.8f, 0.83f),
            "antenna" or "antenna_aft" or "pitot" or "pitot_b" or "vor_antenna"
                or "hf_antenna" or "static_wick_left" or "static_wick_right" => new Color(0.35f, 0.35f, 0.38f),
            "nav_light_left" => new Color(0.2f, 0.9f, 0.3f),
            "nav_light_right" => new Color(0.9f, 0.2f, 0.2f),
            "beacon_top" => new Color(0.95f, 0.35f, 0.12f),
            "tail_nav_light" => new Color(0.95f, 0.95f, 0.9f),
            "landing_light_l" or "landing_light_r" or "taxi_light" => new Color(0.95f, 0.95f, 0.85f),
            _ => null
        };

        /// <summary>
        /// Parent blades, hubs and spinners under each propeller so SpinPropellers
        /// rotates the whole assembly (0025 item 7).
        /// </summary>
        private static void NestCrossPropellerBlades(Transform aircraft)
        {
            Transform propL = null, propR = null;
            Transform hubL = null, hubR = null, spinnerL = null, spinnerR = null;
            Transform capL = null, capR = null;
            Transform stripeL = null, stripeR = null;
            var bladesL = new Transform[5];
            var bladesR = new Transform[5];
            var tipsL = new Transform[6];
            var tipsR = new Transform[6];
            foreach (var child in aircraft.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "Propeller L") propL = child;
                else if (child.name == "Propeller R") propR = child;
                else if (child.name == "PropBlade L") bladesL[0] = child;
                else if (child.name == "PropBlade R") bladesR[0] = child;
                else if (child.name == "PropBlade L2") bladesL[1] = child;
                else if (child.name == "PropBlade R2") bladesR[1] = child;
                else if (child.name == "PropBlade L3") bladesL[2] = child;
                else if (child.name == "PropBlade R3") bladesR[2] = child;
                else if (child.name == "PropBlade L4") bladesL[3] = child;
                else if (child.name == "PropBlade R4") bladesR[3] = child;
                else if (child.name == "PropBlade L5") bladesL[4] = child;
                else if (child.name == "PropBlade R5") bladesR[4] = child;
                else if (child.name == "PropTip L") tipsL[0] = child;
                else if (child.name == "PropTip R") tipsR[0] = child;
                else if (child.name == "PropTip L2") tipsL[1] = child;
                else if (child.name == "PropTip R2") tipsR[1] = child;
                else if (child.name == "PropTip L3") tipsL[2] = child;
                else if (child.name == "PropTip R3") tipsR[2] = child;
                else if (child.name == "PropTip L4") tipsL[3] = child;
                else if (child.name == "PropTip R4") tipsR[3] = child;
                else if (child.name == "PropTip L5") tipsL[4] = child;
                else if (child.name == "PropTip R5") tipsR[4] = child;
                else if (child.name == "PropTip L6") tipsL[5] = child;
                else if (child.name == "PropTip R6") tipsR[5] = child;
                else if (child.name == "Prop hub L") hubL = child;
                else if (child.name == "Prop hub R") hubR = child;
                else if (child.name == "Spinner L") spinnerL = child;
                else if (child.name == "Spinner R") spinnerR = child;
                else if (child.name == "Hub cap L") capL = child;
                else if (child.name == "Hub cap R") capR = child;
                else if (child.name == "Spinner stripe L") stripeL = child;
                else if (child.name == "Spinner stripe R") stripeR = child;
            }

            for (var i = 0; i < bladesL.Length; i++)
                NestUnderProp(propL, bladesL[i], i == 0 ? "Blade" : $"Blade {i + 1}");
            for (var i = 0; i < bladesR.Length; i++)
                NestUnderProp(propR, bladesR[i], i == 0 ? "Blade" : $"Blade {i + 1}");
            for (var i = 0; i < tipsL.Length; i++)
                NestUnderProp(propL, tipsL[i], i == 0 ? "Tip" : $"Tip {i + 1}");
            for (var i = 0; i < tipsR.Length; i++)
                NestUnderProp(propR, tipsR[i], i == 0 ? "Tip" : $"Tip {i + 1}");
            NestUnderProp(propL, hubL, "Hub");
            NestUnderProp(propR, hubR, "Hub");
            NestUnderProp(propL, spinnerL, "Spinner");
            NestUnderProp(propR, spinnerR, "Spinner");
            NestUnderProp(propL, capL, "Hub cap");
            NestUnderProp(propR, capR, "Hub cap");
            NestUnderProp(propL, stripeL, "Stripe");
            NestUnderProp(propR, stripeR, "Stripe");
        }

        /// <summary>
        /// Move each Propeller transform to its hub centre and rebake mesh verts so
        /// <see cref="SpinPropellers"/> rotates about the nacelle, not the airframe origin.
        /// No-ops when the prop node is already at the hub (Resources/prefab path).
        /// </summary>
        private static void RebakePropellerPivots(Transform aircraft)
        {
            foreach (var child in aircraft.GetComponentsInChildren<Transform>(true))
            {
                if (child == aircraft || !child.name.StartsWith("Propeller", StringComparison.Ordinal))
                    continue;
                RebakePropellerPivot(child);
            }
        }

        private static void RebakePropellerPivot(Transform prop)
        {
            if (!TryEstimatePropHubWorld(prop, out var hubWorld))
                return;

            // Already at the hub (Resources/prefab path with local blade verts).
            if ((prop.position - hubWorld).sqrMagnitude < 0.0025f)
                return;

            var filters = prop.GetComponentsInChildren<MeshFilter>(true);
            var worldVerts = new Vector3[filters.Length][];
            for (var i = 0; i < filters.Length; i++)
            {
                var filter = filters[i];
                if (filter == null || filter.sharedMesh == null)
                {
                    worldVerts[i] = null;
                    continue;
                }

                var source = filter.sharedMesh;
                var local = source.vertices;
                var world = new Vector3[local.Length];
                var xf = filter.transform;
                for (var v = 0; v < local.Length; v++)
                    world[v] = xf.TransformPoint(local[v]);
                worldVerts[i] = world;
            }

            prop.position = hubWorld;

            for (var i = 0; i < filters.Length; i++)
            {
                if (worldVerts[i] == null)
                    continue;
                var filter = filters[i];
                var mesh = Object.Instantiate(filter.sharedMesh);
                mesh.name = filter.sharedMesh.name + " hub-pivot";
                var local = new Vector3[worldVerts[i].Length];
                var xf = filter.transform;
                for (var v = 0; v < local.Length; v++)
                    local[v] = xf.InverseTransformPoint(worldVerts[i][v]);
                mesh.vertices = local;
                mesh.RecalculateBounds();
                mesh.RecalculateNormals();
                filter.sharedMesh = mesh;
            }
        }

        private static bool TryEstimatePropHubWorld(Transform prop, out Vector3 hubWorld)
        {
            hubWorld = default;
            var hub = prop.Find("Hub");
            if (hub != null)
            {
                var hubRenderer = hub.GetComponent<Renderer>();
                if (hubRenderer != null)
                {
                    hubWorld = hubRenderer.bounds.center;
                    return true;
                }
            }

            var spinner = prop.Find("Spinner");
            if (spinner != null)
            {
                var spinnerRenderer = spinner.GetComponent<Renderer>();
                if (spinnerRenderer != null)
                {
                    hubWorld = spinnerRenderer.bounds.center;
                    return true;
                }
            }

            var selfRenderer = prop.GetComponent<Renderer>();
            if (selfRenderer != null)
            {
                hubWorld = selfRenderer.bounds.center;
                return true;
            }

            var sum = Vector3.zero;
            var count = 0;
            foreach (var renderer in prop.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || renderer.name == "PropDisc")
                    continue;
                sum += renderer.bounds.center;
                count++;
            }

            if (count == 0)
                return false;
            hubWorld = sum / count;
            return true;
        }

        private static void NestUnderProp(Transform prop, Transform part, string rename)
        {
            if (prop == null || part == null || part.parent == prop)
                return;
            part.SetParent(prop, true);
            part.name = rename;
        }

        /// <summary>
        /// Parent scissors / tires under matching gear struts so retract takes the
        /// whole assembly (0025 item 7) — mirrors NestCrossPropellerBlades.
        /// </summary>
        private static void NestLandingGearParts(Transform aircraft)
        {
            Transform gearNose = null, gearL = null, gearR = null;
            Transform scissorsNose = null, scissorsL = null, scissorsR = null;
            Transform tireNose = null, tireL = null, tireR = null;
            Transform oleoNose = null, oleoL = null, oleoR = null;
            Transform rimNose = null, rimL = null, rimR = null;
            Transform wheelNose = null, wheelL = null, wheelR = null;
            foreach (var child in aircraft.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "Gear nose") gearNose = child;
                else if (child.name == "Gear L") gearL = child;
                else if (child.name == "Gear R") gearR = child;
                else if (child.name == "Gear scissors nose") scissorsNose = child;
                else if (child.name == "Gear scissors L") scissorsL = child;
                else if (child.name == "Gear scissors R") scissorsR = child;
                else if (child.name == "Tire nose") tireNose = child;
                else if (child.name == "Tire L") tireL = child;
                else if (child.name == "Tire R") tireR = child;
                else if (child.name == "Gear oleo nose") oleoNose = child;
                else if (child.name == "Gear oleo L") oleoL = child;
                else if (child.name == "Gear oleo R") oleoR = child;
                else if (child.name == "Rim nose") rimNose = child;
                else if (child.name == "Rim L") rimL = child;
                else if (child.name == "Rim R") rimR = child;
                else if (child.name == "Wheel nose") wheelNose = child;
                else if (child.name == "Wheel L") wheelL = child;
                else if (child.name == "Wheel R") wheelR = child;
            }

            NestUnderProp(gearNose, scissorsNose, "Scissors");
            NestUnderProp(gearL, scissorsL, "Scissors");
            NestUnderProp(gearR, scissorsR, "Scissors");
            NestUnderProp(gearNose, oleoNose, "Oleo");
            NestUnderProp(gearL, oleoL, "Oleo");
            NestUnderProp(gearR, oleoR, "Oleo");
            NestUnderProp(gearNose, tireNose, "Tire");
            NestUnderProp(gearL, tireL, "Tire");
            NestUnderProp(gearR, tireR, "Tire");
            NestUnderProp(gearNose, wheelNose, "Wheel");
            NestUnderProp(gearL, wheelL, "Wheel");
            NestUnderProp(gearR, wheelR, "Wheel");
            NestUnderProp(gearNose, rimNose, "Rim");
            NestUnderProp(gearL, rimL, "Rim");
            NestUnderProp(gearR, rimR, "Rim");
            // Gear doors stay siblings so UpdateAircraftLightsAndGear can animate them independently.
        }

        /// <summary>
        /// Nest flap tracks/fairings under Flap L/R so approach deploy carries them (0025 item 7).
        /// </summary>
        private static void NestFlapParts(Transform aircraft)
        {
            Transform flapL = null, flapR = null;
            var leftExtras = new List<Transform>();
            var rightExtras = new List<Transform>();
            foreach (var child in aircraft.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "Flap L")
                    flapL = child;
                else if (child.name == "Flap R")
                    flapR = child;
                else if (child.name is "Flap track L1" or "Flap track L2" or "Flap fairing L")
                    leftExtras.Add(child);
                else if (child.name is "Flap track R1" or "Flap track R2" or "Flap fairing R")
                    rightExtras.Add(child);
            }

            foreach (var extra in leftExtras)
                NestUnderProp(flapL, extra, extra.name);
            foreach (var extra in rightExtras)
                NestUnderProp(flapR, extra, extra.name);
        }

        /// <summary>
        /// Nest densified cargo bags under the first Cargo crate so bag unload bob carries them.
        /// </summary>
        private static void NestCargoBags(Transform vehicle)
        {
            Transform cargo = null;
            var bags = new List<Transform>();
            foreach (var child in vehicle.GetComponentsInChildren<Transform>(true))
            {
                if (child == vehicle)
                    continue;
                if (child.name == "Cargo" && cargo == null)
                    cargo = child;
                else if (child.name.StartsWith("Cargo bag", StringComparison.Ordinal))
                    bags.Add(child);
            }

            if (cargo == null)
                return;
            foreach (var bag in bags)
                NestUnderProp(cargo, bag, bag.name);
        }

        /// <summary>
        /// Nest cabin door handle under CabinDoor so UpdateCabinDoor swings both (0025 item 7).
        /// </summary>
        private static void NestCabinDoorParts(Transform aircraft)
        {
            Transform door = null;
            Transform handle = null;
            Transform frame = null;
            Transform latch = null;
            foreach (var child in aircraft.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.StartsWith("CabinDoor", StringComparison.Ordinal))
                    door = child;
                else if (child.name == "Door handle")
                    handle = child;
                else if (child.name == "Door frame")
                    frame = child;
                else if (child.name == "Cargo door latch")
                    latch = child;
            }

            NestUnderProp(door, handle, "Handle");
            NestUnderProp(door, frame, "Frame");
            // Cargo latch stays with cargo door if present.
            Transform cargo = null;
            foreach (var child in aircraft.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "Cargo door")
                {
                    cargo = child;
                    break;
                }
            }

            NestUnderProp(cargo, latch, "Latch");
        }

        /// <summary>
        /// Nest bus/GSE door glass and handles under Door so AnimateServiceLoops swings them.
        /// </summary>
        private static void NestServiceDoorParts(Transform vehicle)
        {
            Transform door = null;
            var extras = new List<Transform>();
            foreach (var child in vehicle.GetComponentsInChildren<Transform>(true))
            {
                if (child == vehicle)
                    continue;
                if (child.name == "Door")
                    door = child;
                else if (child.name is "Door glass" or "Door handle" or "Door frame")
                    extras.Add(child);
            }

            if (door == null)
                return;
            foreach (var extra in extras)
                NestUnderProp(door, extra, extra.name);
        }

        private static bool HasNamedChild(Transform root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child != root && child.name == name)
                    return true;
            }

            return false;
        }

        /// <summary>URP Lit uses _BaseColor; keep legacy .color in sync for Built-in fallbacks.</summary>
        private static void SetRendererColor(Renderer renderer, Color color)
        {
            if (renderer == null)
                return;
            renderer.material.color = color;
            if (renderer.material.HasProperty("_BaseColor"))
                renderer.material.SetColor("_BaseColor", color);
        }

        /// <summary>
        /// Translucent prop disc under each propeller hub — shown only at high RPM.
        /// </summary>
        private static void EnsurePropDiscs(Transform aircraft)
        {
            foreach (var child in aircraft.GetComponentsInChildren<Transform>(true))
            {
                if (child == aircraft || !child.name.StartsWith("Propeller", StringComparison.Ordinal))
                    continue;
                if (child.Find("PropDisc") != null)
                    continue;

                // Size the blur disc from blade/tip bounds (v06 radial ~1.27 m — fixed 1.2
                // diameter read as a hub pancake after pivot rebake).
                var radius = 0.6f;
                foreach (var renderer in child.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer == null)
                        continue;
                    var n = renderer.name;
                    if (n.IndexOf("blade", StringComparison.OrdinalIgnoreCase) < 0
                        && n.IndexOf("tip", StringComparison.OrdinalIgnoreCase) < 0
                        && !n.StartsWith("Propeller", StringComparison.Ordinal))
                        continue;
                    var extents = renderer.bounds.extents;
                    var planar = Mathf.Max(extents.x, extents.y, extents.z);
                    radius = Mathf.Max(radius, planar);
                }

                var diameter = Mathf.Clamp(radius * 2.05f, 1.2f, 2.8f);
                var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                disc.name = "PropDisc";
                Object.Destroy(disc.GetComponent<Collider>());
                disc.transform.SetParent(child, false);
                disc.transform.localPosition = Vector3.zero;
                // Cylinder axis → local Z so the face is perpendicular to the spin axis.
                disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                disc.transform.localScale = new Vector3(diameter, 0.012f, diameter);
                disc.GetComponent<Renderer>().material = CreateMaterial(new Color(0.55f, 0.56f, 0.6f, 0.32f));
                disc.SetActive(false);
            }
        }

        /// <summary>
        /// Soft elliptical ground shadow under each aircraft (presentation only).
        /// </summary>
        private static void EnsureGroundShadow(Transform aircraft)
        {
            if (aircraft.Find("GroundShadow") != null)
                return;

            var shadow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shadow.name = "GroundShadow";
            Object.Destroy(shadow.GetComponent<Collider>());
            shadow.transform.SetParent(aircraft, false);
            shadow.transform.localPosition = new Vector3(0f, -0.65f, 0f);
            shadow.transform.localRotation = Quaternion.identity;
            shadow.transform.localScale = new Vector3(2.8f, 0.012f, 1.5f);
            var material = AirsideMaterialLibrary.Create(new Color(0.05f, 0.06f, 0.08f, 0.16f),
                AirsideMaterialLibrary.SurfaceKind.Default);
            var renderer = shadow.GetComponent<Renderer>();
            renderer.material = material;
            SetRendererColor(renderer, new Color(0.05f, 0.06f, 0.08f, 0.16f));
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static void UpdateGroundShadow(Transform aircraft)
        {
            var shadow = aircraft.Find("GroundShadow");
            if (shadow == null)
                return;

            var ground = new Vector3(aircraft.position.x, 0.05f, aircraft.position.z);
            shadow.position = ground;
            shadow.rotation = Quaternion.identity;
            var altitude = Mathf.Max(0f, aircraft.position.y - 0.55f);
            var t = Mathf.Clamp01(altitude / 14f);
            var width = Mathf.Lerp(3.6f, 7.2f, t);
            var depth = width * 0.52f;
            var sx = aircraft.lossyScale.x > 0.001f ? width / aircraft.lossyScale.x : width;
            var sy = aircraft.lossyScale.y > 0.001f ? 0.03f / aircraft.lossyScale.y : 0.03f;
            var sz = aircraft.lossyScale.z > 0.001f ? depth / aircraft.lossyScale.z : depth;
            shadow.localScale = new Vector3(sx, sy, sz);

            var renderer = shadow.GetComponent<Renderer>();
            if (renderer == null)
                return;
            var color = renderer.material.color;
            // Softer contact so realtime URP shadows remain the primary read.
            color.a = Mathf.Lerp(0.28f, 0.04f, t);
            SetRendererColor(renderer, color);
            shadow.gameObject.SetActive(aircraft.gameObject.activeInHierarchy);
        }

        /// <summary>Prefer the richest present kit/prefab; last candidate is the Approved fallback.</summary>
        private static string PreferArtKit(params string[] candidates)
        {
            if (candidates == null || candidates.Length == 0)
                return null;
            for (var i = 0; i < candidates.Length; i++)
            {
                if (ArtPresentationLoader.HasPresentation(candidates[i]))
                    return candidates[i];
            }

            return candidates[candidates.Length - 1];
        }

        private static void ApplyLiveryDecal(Transform aircraft, string artRelativePath)
        {
            if (string.IsNullOrEmpty(artRelativePath))
                return;
            var texture = TryLoadArtTexture(artRelativePath);
            if (texture == null)
                return;

            foreach (var child in aircraft.GetComponentsInChildren<Transform>(true))
            {
                var n = child.name;
                // Cover segmented turboprop fuselage parts (v04 + lofted cabin rings / nose rings).
                if (n != "Fuselage" && n != "FuselageMid" && n != "Fuselage mid" && n != "FuselageAft" && n != "Fuselage aft"
                    && n != "Nose"
                    && n.IndexOf("fuselage", StringComparison.OrdinalIgnoreCase) < 0
                    && n.IndexOf("nose", StringComparison.OrdinalIgnoreCase) < 0
                    && n.IndexOf("cabin_ring", StringComparison.OrdinalIgnoreCase) < 0
                    && n.IndexOf("tail_cone", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                var renderer = child.GetComponent<Renderer>();
                if (renderer == null)
                    continue;
                renderer.material.mainTexture = texture;
                renderer.material.mainTextureScale = new Vector2(1f, 1f);
            }
        }

        private static void ParentBlock(Transform parent, string name, Vector3 localPosition, Vector3 scale, Color color)
        {
            var block = CreateBlock(name, localPosition, scale, color);
            block.transform.SetParent(parent, false);
            block.transform.localPosition = localPosition;
        }

        private static Transform BuildGroundTrafficAircraft(string label)
        {
            var root = BuildAircraft(
                $"Ground traffic {label}",
                new Color(0.82f, 0.55f, 0.16f),
                "Textures/Decals/dc_livery_airside_traffic_v01.png");
            root.localScale = new Vector3(0.82f, 0.82f, 0.82f);
            root.position = new Vector3(8f, 0.7f, 9f);
            return root;
        }

        private static Transform BuildServiceVehicle(string name, Color color, Vector3 scale, string artRelativePath = null)
        {
            var root = new GameObject(name).transform;
            var usedArt = !string.IsNullOrEmpty(artRelativePath) && ArtPresentationLoader.TryInstantiate(
                artRelativePath,
                root,
                out _,
                kitName => kitName switch
                {
                    "cab" => $"{name} cab",
                    "tank" or "bus_body" or "tug" => $"{name} body",
                    "hose" => "Hose",
                    "hose_mount" => "Hose mount",
                    "hose_nozzle" => "Hose nozzle",
                    "hose_reel" => "Hose reel",
                    "hose_guard" => "Hose guard",
                    "hose_tray" => "Hose tray",
                    "door" or "cab_door" or "cab_door_r" => "Door",
                    "door_glass" or "door_glass_r" => "Door glass",
                    "door_handle" or "door_handle_l" or "door_handle_r" or "door_hinge_t" or "door_hinge_b"
                        => "Door handle",
                    "door_frame" => "Door frame",
                    "cargo_1" or "cargo_2" or "cargo_3" => "Cargo",
                    "cargo_tag_1" or "cargo_tag_2" => "Cargo tag",
                    "cargo_bag_1a" or "cargo_bag_1b" or "cargo_bag_1c"
                        or "cargo_bag_2a" or "cargo_bag_2b" or "cargo_bag_2c"
                        or "cargo_bag_3a" or "cargo_bag_3b" or "cargo_bag_3c" => "Cargo bag",
                    "headlight_l" => "Headlight L",
                    "headlight_r" => "Headlight R",
                    "taillight_l" => "Taillight L",
                    "taillight_r" => "Taillight R",
                    // Keep cart_* / cart_wheel_* names so wheels roll and carts do not bob as Cargo.
                    _ => $"{name} {kitName}"
                },
                kitName =>
                {
                    if (kitName.StartsWith("glass_pane", StringComparison.Ordinal)
                        || kitName is "cab_window" or "windows" or "tug_window" or "door_glass" or "door_glass_r"
                        or "windshield" or "rear_window" or "belt_loader_cab_glass")
                        return new Color(0.2f, 0.4f, 0.55f, 0.42f);
                    if (kitName.StartsWith("window_mullion", StringComparison.Ordinal)
                        || kitName is "window_sill" or "window_sill_b" or "window_header" or "window_header_b"
                        or "destination_board" or "destination_board_hood" or "destination_digit")
                        return color * 0.7f;
                    return kitName switch
                    {
                        "wheel_fl" or "wheel_fr" or "wheel_rl" or "wheel_rr"
                            or "wheel_ml" or "wheel_mr"
                            or "cart_wheel_1l" or "cart_wheel_1r" or "cart_wheel_2l" or "cart_wheel_2r"
                            or "cart_wheel_3l" or "cart_wheel_3r"
                            or "hub_fl" or "hub_fr" or "hub_rl" or "hub_rr"
                            or "hub_ml" or "hub_mr"
                            or "wheel_hub_fl" or "wheel_hub_fr" or "wheel_hub_rl" or "wheel_hub_rr"
                            or "mudflap_l" or "mudflap_r" => new Color(0.15f, 0.15f, 0.16f),
                        "hose_mount" or "hose" or "hose_reel" or "hose_nozzle" or "hose_guard" or "hose_tray"
                            or "hose_coil_a" or "hose_coil_b" or "hose_coil_c" or "hose_pivot"
                            or "pump_cabinet" or "pump_cabinet_door"
                            or "pump_gauge" or "pump_valve"
                            or "exhaust" or "pump_hose_out" => new Color(0.25f, 0.25f, 0.28f),
                        "door" or "cab_door" or "cab_door_r" or "door_frame" or "door_handle"
                            or "door_handle_l" or "door_handle_r" or "door_hinge_t" or "door_hinge_b"
                            => new Color(0.2f, 0.22f, 0.25f),
                        "cab" or "tug_cab" or "cab_roof" or "cab_visor" or "cab_fairing"
                            or "tug_seat" or "tug_seat_back" or "tug_rollbar"
                            or "tug_rollbar_top" or "tug_rollbar_l" or "tug_rollbar_r"
                            or "tug_floor" or "tug_steering" or "tug_steering_wheel"
                            or "counterweight" => color * 0.82f,
                        "beacon" or "beacon_guard" => new Color(0.95f, 0.35f, 0.12f),
                        "headlight_l" or "headlight_r" => new Color(0.95f, 0.95f, 0.85f),
                        "taillight_l" or "taillight_r" => new Color(0.85f, 0.15f, 0.12f),
                        "mirror_l" or "mirror_r" or "bumper" or "bumper_front" or "bumper_rear"
                            or "tug_bumper" or "tank_band" or "tank_band_2" or "tank_band_3" or "tank_band_4"
                            or "tank_cap" or "tank_cap_b" or "tank_ladder" or "tank_walkway"
                            or "tank_end_f" or "tank_end_r" or "tank_rail_l" or "tank_rail_r"
                            or "grill" or "light_bar" or "fender_fl" or "fender_fr" or "fender_rl" or "fender_rr"
                            or "fender_ml" or "fender_mr"
                            or "wheel_arch_fl" or "wheel_arch_fr" or "wheel_arch_rl" or "wheel_arch_rr"
                            or "chassis" or "step" or "step_r" or "roof_rack" or "roof_vent"
                            or "number_plate" or "fuel_hazard" or "hazard_chevron_1" or "hazard_chevron_2"
                            or "wiper" or "wiper_b" or "nose_round" or "tail_round"
                            or "body_panel_l" or "body_panel_r" or "skirt_l" or "skirt_r"
                            => color * 0.7f,
                        "cargo_1" or "cargo_2" or "cargo_3" or "cargo_tag_1" or "cargo_tag_2"
                            or "cargo_bag_1a" or "cargo_bag_1b" or "cargo_bag_1c"
                            or "cargo_bag_2a" or "cargo_bag_2b" or "cargo_bag_2c"
                            or "cargo_bag_3a" or "cargo_bag_3b" or "cargo_bag_3c"
                            or "cargo_lid_1" or "cargo_lid_2" or "cargo_lid_3"
                            or "cargo_latch_1" or "cargo_latch_2" or "cargo_latch_3"
                            => new Color(0.22f, 0.44f, 0.55f),
                        "stripe" or "stripe_b" or "stripe_upper" or "cab_stripe" or "tank_stripe"
                            or "tug_stripe" => new Color(0.95f, 0.85f, 0.2f),
                        "bus_body_upper" or "cabin_roof" => new Color(0.94f, 0.95f, 0.96f),
                        "cart_rail_1" or "cart_rail_2" or "cart_rail_3"
                            or "cart_rail_1b" or "cart_rail_2b" or "cart_rail_3b"
                            or "cart_gate_1" or "cart_gate_2" or "cart_gate_3"
                            or "cart_bed_1" or "cart_bed_2" or "cart_bed_3"
                            or "cart_canopy_1" or "cart_canopy_2" or "cart_canopy_3"
                            or "cart_post_1l" or "cart_post_1r" or "cart_post_2l" or "cart_post_2r"
                            or "cart_post_3l" or "cart_post_3r"
                            or "cart_post_1fl" or "cart_post_1fr" or "cart_post_2fl" or "cart_post_2fr"
                            or "cart_post_3fl" or "cart_post_3fr"
                            or "hitch_1" or "hitch_2" or "hitch_3"
                            or "hitch_pin_1" or "hitch_pin_2" or "hitch_pin_3"
                            or "tow_pivot_1" or "tow_pivot_2" or "tow_pivot_3" => color * 0.6f,
                        "seat_row_1" or "seat_row_2" or "seat_row_3" or "seat_row_4"
                            or "seat_back_1" or "seat_back_2" => new Color(0.35f, 0.38f, 0.42f),
                        _ => color
                    };
                },
                localPosition: new Vector3(0f, -0.55f, 0f));

            if (!usedArt)
            {
                ParentBlock(root, $"{name} body", Vector3.zero, scale, color);
                ParentBlock(root, $"{name} cab", new Vector3(scale.x * 0.28f, scale.y * 0.42f, 0f),
                    new Vector3(scale.x * 0.34f, scale.y * 0.62f, scale.z * 0.86f), color * 0.82f);
                ParentBlock(root, $"{name} wheel FL", new Vector3(scale.x * 0.32f, -scale.y * 0.35f, scale.z * 0.42f),
                    new Vector3(0.28f, 0.35f, 0.18f), new Color(0.15f, 0.15f, 0.16f));
                ParentBlock(root, $"{name} wheel FR", new Vector3(scale.x * 0.32f, -scale.y * 0.35f, -scale.z * 0.42f),
                    new Vector3(0.28f, 0.35f, 0.18f), new Color(0.15f, 0.15f, 0.16f));
                ParentBlock(root, $"{name} wheel RL", new Vector3(-scale.x * 0.28f, -scale.y * 0.35f, scale.z * 0.42f),
                    new Vector3(0.28f, 0.35f, 0.18f), new Color(0.15f, 0.15f, 0.16f));
                ParentBlock(root, $"{name} wheel RR", new Vector3(-scale.x * 0.28f, -scale.y * 0.35f, -scale.z * 0.42f),
                    new Vector3(0.28f, 0.35f, 0.18f), new Color(0.15f, 0.15f, 0.16f));
                ParentBlock(root, "Hose", new Vector3(scale.x * 0.45f, 0.15f, 0.2f),
                    new Vector3(0.12f, 0.12f, 0.4f), new Color(0.25f, 0.25f, 0.28f));
                ParentBlock(root, "Cargo", new Vector3(0f, 0.35f, 0f),
                    new Vector3(0.55f, 0.35f, 0.45f), new Color(0.75f, 0.55f, 0.2f));
                ParentBlock(root, "Door", new Vector3(scale.x * 0.2f, 0.25f, scale.z * 0.45f),
                    new Vector3(0.08f, 0.7f, 0.45f), new Color(0.2f, 0.22f, 0.25f));
            }
            else
            {
                NestServiceDoorParts(root);
                NestCargoBags(root);
            }

            root.gameObject.SetActive(false);
            return root;
        }


        private static Transform BuildStairs()
        {
            // Prefer denser authored service kit over thin Resources prefab (0025 item 2).
            var root = new GameObject("Passenger stairs").transform;
            var kit = PreferArtKit(
                "Models/Props/mdl_service_equipment_kit_v03.gltf",
                "Models/Props/mdl_service_equipment_kit_authored_v01.gltf",
                "Models/Props/mdl_service_equipment_kit_v02.gltf",
                "Models/Props/mdl_service_equipment_kit_v01.gltf");
            var placed = false;
            Transform PlacePart(string mesh, Color color)
            {
                if (!ArtGltfLoader.TryPlaceNamedMesh(kit, mesh, Vector3.zero, Quaternion.identity, color, out var part))
                    return null;
                part.SetParent(root, false);
                part.localPosition = new Vector3(0f, -0.55f, 0f);
                placed = true;
                return part;
            }

            PlacePart("stairs_base", new Color(0.55f, 0.56f, 0.58f));
            PlacePart("stairs_rail_l", AirsideTheme.SafetyYellow);
            PlacePart("stairs_rail_r", AirsideTheme.SafetyYellow);
            PlacePart("stairs_rail_mid", AirsideTheme.SafetyYellow);
            PlacePart("stairs_tread_1", new Color(0.62f, 0.63f, 0.65f));
            PlacePart("stairs_tread_2", new Color(0.62f, 0.63f, 0.65f));
            PlacePart("stairs_tread_3", new Color(0.62f, 0.63f, 0.65f));
            PlacePart("stairs_tread_4", new Color(0.62f, 0.63f, 0.65f));
            PlacePart("stairs_tread_5", new Color(0.62f, 0.63f, 0.65f));
            PlacePart("stairs_tread_6", new Color(0.62f, 0.63f, 0.65f));
            PlacePart("stairs_rail_cross", AirsideTheme.SafetyYellow);
            PlacePart("stairs_post_1l", AirsideTheme.SafetyYellow);
            PlacePart("stairs_post_1r", AirsideTheme.SafetyYellow);
            PlacePart("stairs_post_2l", AirsideTheme.SafetyYellow);
            PlacePart("stairs_post_2r", AirsideTheme.SafetyYellow);
            PlacePart("stairs_post_3l", AirsideTheme.SafetyYellow);
            PlacePart("stairs_post_3r", AirsideTheme.SafetyYellow);
            PlacePart("stairs_nosing_1", AirsideTheme.SafetyYellow);
            PlacePart("stairs_nosing_2", AirsideTheme.SafetyYellow);
            PlacePart("stairs_nosing_3", AirsideTheme.SafetyYellow);
            PlacePart("stairs_nosing_4", AirsideTheme.SafetyYellow);
            PlacePart("stairs_nosing_5", AirsideTheme.SafetyYellow);
            PlacePart("stairs_side_panel_l", AirsideTheme.CoastalBlue);
            PlacePart("stairs_side_panel_r", AirsideTheme.CoastalBlue);
            PlacePart("stairs_platform", new Color(0.7f, 0.72f, 0.74f));
            PlacePart("stairs_handle", Shade(AirsideTheme.CoastalBlue, 0.9f));
            PlacePart("stairs_brace", new Color(0.5f, 0.5f, 0.52f));
            PlacePart("stairs_wheel_l", new Color(0.15f, 0.15f, 0.16f));
            PlacePart("stairs_wheel_r", new Color(0.15f, 0.15f, 0.16f));
            PlacePart("stairs_wheel_rl", new Color(0.15f, 0.15f, 0.16f));
            PlacePart("stairs_wheel_rr", new Color(0.15f, 0.15f, 0.16f));
            PlacePart("stairs_hub_fl", new Color(0.25f, 0.26f, 0.28f));
            PlacePart("stairs_hub_fr", new Color(0.25f, 0.26f, 0.28f));
            PlacePart("stairs_hub_rl", new Color(0.25f, 0.26f, 0.28f));
            PlacePart("stairs_hub_rr", new Color(0.25f, 0.26f, 0.28f));
            if (!placed && ArtGltfLoader.TryPlaceNamedMesh(kit, "stairs", Vector3.zero, Quaternion.identity,
                    new Color(0.7f, 0.72f, 0.74f), out var stairs))
            {
                stairs.SetParent(root, false);
                stairs.localPosition = new Vector3(0f, -0.55f, 0f);
                placed = true;
            }

            if (placed)
            {
                root.gameObject.SetActive(false);
                return root;
            }

            Object.Destroy(root.gameObject);
            if (ArtPresentationLoader.TryInstantiatePrefab("mdl_passenger_stairs_v02", out var prefabRoot)
                || ArtPresentationLoader.TryInstantiatePrefab("mdl_passenger_stairs_v01", out prefabRoot))
            {
                prefabRoot.name = "Passenger stairs";
                prefabRoot.gameObject.SetActive(false);
                return prefabRoot;
            }

            root = new GameObject("Passenger stairs").transform;
            ParentBlock(root, "Stairs base", Vector3.zero, new Vector3(1.1f, 0.2f, 2.4f), new Color(0.7f, 0.72f, 0.74f));
            ParentBlock(root, "Stairs rail L", new Vector3(-0.45f, 0.55f, 0f), new Vector3(0.08f, 1.0f, 2.2f), new Color(0.85f, 0.55f, 0.15f));
            ParentBlock(root, "Stairs rail R", new Vector3(0.45f, 0.55f, 0f), new Vector3(0.08f, 1.0f, 2.2f), new Color(0.85f, 0.55f, 0.15f));
            for (var i = 0; i < 5; i++)
                ParentBlock(root, $"Step {i}", new Vector3(0f, 0.15f + i * 0.18f, -0.9f + i * 0.35f),
                    new Vector3(0.95f, 0.08f, 0.32f), new Color(0.55f, 0.56f, 0.58f));

            root.gameObject.SetActive(false);
            return root;
        }

        private static Transform BuildChocks()
        {
            var root = new GameObject("Wheel chocks").transform;
            var kit = PreferArtKit(
                "Models/Props/mdl_service_equipment_kit_v03.gltf",
                "Models/Props/mdl_service_equipment_kit_authored_v01.gltf",
                "Models/Props/mdl_service_equipment_kit_v02.gltf",
                "Models/Props/mdl_service_equipment_kit_v01.gltf");
            var placed = ArtGltfLoader.TryPlaceNamedMesh(kit, "chock_a", new Vector3(-0.55f, 0f, 0f), Quaternion.identity,
                new Color(0.85f, 0.2f, 0.15f), out var a);
            placed = ArtGltfLoader.TryPlaceNamedMesh(kit, "chock_b", new Vector3(0.55f, 0f, 0f), Quaternion.identity,
                new Color(0.85f, 0.2f, 0.15f), out var b) || placed;
            placed = ArtGltfLoader.TryPlaceNamedMesh(kit, "chock_rope", Vector3.zero, Quaternion.identity,
                new Color(0.2f, 0.2f, 0.22f), out var rope) || placed;
            placed = ArtGltfLoader.TryPlaceNamedMesh(kit, "chock_handle", Vector3.zero, Quaternion.identity,
                new Color(0.25f, 0.26f, 0.28f), out var handle) || placed;
            // v02 kit may ship a single combined "chocks" mesh.
            if (!placed)
            {
                placed = ArtGltfLoader.TryPlaceNamedMesh(kit, "chocks", Vector3.zero, Quaternion.identity,
                    new Color(0.85f, 0.2f, 0.15f), out var combined);
                if (combined != null)
                {
                    combined.SetParent(root, false);
                    combined.localPosition = new Vector3(0f, -0.55f, 0f);
                }
            }

            if (placed)
            {
                if (a != null) { a.SetParent(root, false); a.localPosition = new Vector3(-0.55f, -0.55f, 0f); }
                if (b != null) { b.SetParent(root, false); b.localPosition = new Vector3(0.55f, -0.55f, 0f); }
                if (rope != null) { rope.SetParent(root, false); rope.localPosition = new Vector3(0f, -0.55f, 0f); }
                if (handle != null) { handle.SetParent(root, false); handle.localPosition = new Vector3(0f, -0.55f, 0f); }
                root.gameObject.SetActive(false);
                return root;
            }

            Object.Destroy(root.gameObject);
            if (ArtPresentationLoader.TryInstantiatePrefab("mdl_wheel_chocks_v01", out var prefabRoot))
            {
                prefabRoot.name = "Wheel chocks";
                prefabRoot.gameObject.SetActive(false);
                return prefabRoot;
            }

            root = new GameObject("Wheel chocks").transform;
            ParentBlock(root, "Chock L", new Vector3(-0.55f, 0f, 0f), new Vector3(0.35f, 0.22f, 0.45f), new Color(0.85f, 0.2f, 0.15f));
            ParentBlock(root, "Chock R", new Vector3(0.55f, 0f, 0f), new Vector3(0.35f, 0.22f, 0.45f), new Color(0.85f, 0.2f, 0.15f));
            root.gameObject.SetActive(false);
            return root;
        }

        private static Transform BuildGpuCart()
        {
            var root = new GameObject("GPU cart").transform;
            var kit = PreferArtKit(
                "Models/Props/mdl_service_equipment_kit_v03.gltf",
                "Models/Props/mdl_service_equipment_kit_authored_v01.gltf",
                "Models/Props/mdl_service_equipment_kit_v02.gltf",
                "Models/Props/mdl_service_equipment_kit_v01.gltf");
            var placed = false;
            void PlaceGpu(string mesh, Color color)
            {
                if (!ArtGltfLoader.TryPlaceNamedMesh(kit, mesh, Vector3.zero, Quaternion.identity, color, out var part))
                    return;
                part.SetParent(root, false);
                part.localPosition = new Vector3(0f, -0.55f, 0f);
                placed = true;
            }

            // REF-003 GSE palette — Safety Yellow chassis, dark metal vents/wheels.
            PlaceGpu("gpu_body", AirsideTheme.SafetyYellow);
            PlaceGpu("gpu_cab", Shade(AirsideTheme.SafetyYellow, 0.85f));
            PlaceGpu("gpu_vent", new Color(0.35f, 0.38f, 0.36f));
            PlaceGpu("gpu_panel", new Color(0.2f, 0.22f, 0.24f));
            PlaceGpu("gpu_panel_b", new Color(0.2f, 0.22f, 0.24f));
            PlaceGpu("gpu_grille", new Color(0.18f, 0.2f, 0.2f));
            PlaceGpu("gpu_grille_2", new Color(0.18f, 0.2f, 0.2f));
            PlaceGpu("gpu_slot_1", new Color(0.15f, 0.16f, 0.18f));
            PlaceGpu("gpu_slot_2", new Color(0.15f, 0.16f, 0.18f));
            PlaceGpu("gpu_cable", new Color(0.2f, 0.2f, 0.22f));
            PlaceGpu("gpu_cable_reel", new Color(0.22f, 0.22f, 0.24f));
            PlaceGpu("gpu_hitch", new Color(0.3f, 0.3f, 0.32f));
            PlaceGpu("gpu_beacon", new Color(0.95f, 0.35f, 0.12f));
            PlaceGpu("gpu_exhaust", new Color(0.3f, 0.32f, 0.3f));
            PlaceGpu("gpu_light", new Color(0.95f, 0.9f, 0.6f));
            PlaceGpu("gpu_handle", new Color(0.28f, 0.3f, 0.32f));
            PlaceGpu("gpu_stripe", new Color(0.15f, 0.16f, 0.18f));
            PlaceGpu("gpu_wheel_fl", new Color(0.15f, 0.15f, 0.16f));
            PlaceGpu("gpu_wheel_fr", new Color(0.15f, 0.15f, 0.16f));
            PlaceGpu("gpu_wheel_rl", new Color(0.15f, 0.15f, 0.16f));
            PlaceGpu("gpu_wheel_rr", new Color(0.15f, 0.15f, 0.16f));
            PlaceGpu("gpu_hub_fl", new Color(0.25f, 0.26f, 0.28f));
            PlaceGpu("gpu_hub_fr", new Color(0.25f, 0.26f, 0.28f));
            PlaceGpu("gpu_hub_rl", new Color(0.25f, 0.26f, 0.28f));
            PlaceGpu("gpu_hub_rr", new Color(0.25f, 0.26f, 0.28f));
            if (!placed && ArtGltfLoader.TryPlaceNamedMesh(kit, "gpu", Vector3.zero, Quaternion.identity,
                    AirsideTheme.SafetyYellow, out var gpu))
            {
                gpu.SetParent(root, false);
                gpu.localPosition = new Vector3(0f, -0.55f, 0f);
                placed = true;
            }

            if (placed)
            {
                root.gameObject.SetActive(false);
                return root;
            }

            Object.Destroy(root.gameObject);
            if (ArtPresentationLoader.TryInstantiatePrefab("mdl_gpu_cart_v01", out var prefabRoot))
            {
                prefabRoot.name = "GPU cart";
                prefabRoot.gameObject.SetActive(false);
                return prefabRoot;
            }

            root = new GameObject("GPU cart").transform;
            ParentBlock(root, "GPU body", Vector3.zero, new Vector3(1.4f, 0.7f, 0.9f), AirsideTheme.SafetyYellow);
            ParentBlock(root, "GPU cable", new Vector3(0.85f, 0.1f, 0f), new Vector3(0.7f, 0.08f, 0.08f), new Color(0.2f, 0.2f, 0.22f));
            ParentBlock(root, "GPU wheel L", new Vector3(0.4f, -0.28f, 0.35f), new Vector3(0.22f, 0.22f, 0.14f), new Color(0.15f, 0.15f, 0.16f));
            ParentBlock(root, "GPU wheel R", new Vector3(0.4f, -0.28f, -0.35f), new Vector3(0.22f, 0.22f, 0.14f), new Color(0.15f, 0.15f, 0.16f));
            root.gameObject.SetActive(false);
            return root;
        }

        private static Transform BuildPushbackTug()
        {
            // VEH-004 — prefer pushback tug v03, then v02, then pipeline-proof v01.
            var pushbackKit = PreferArtKit(
                "Models/Vehicles/mdl_pushback_tug_v03.gltf",
                "Models/Vehicles/mdl_pushback_tug_v02.gltf");
            if (!string.IsNullOrEmpty(pushbackKit)
                && ArtPresentationLoader.TryInstantiate(
                    pushbackKit,
                    null,
                    out var artRoot,
                    kitName => kitName switch
                    {
                        "towbar" => "Tug towbar",
                        "towbar_head" => "Tug towbar head",
                        "towbar_wheel" => "Tug towbar wheel",
                        "towbar_handle" => "Tug towbar handle",
                        "towbar_eye" => "Tug towbar eye",
                        "tow_pivot" or "towbar_pivot_mid" => "Tug tow pivot",
                        "tug_body" => "Tug body",
                        "tug_cab" => "Tug cab",
                        "beacon" => "Tug beacon",
                        "wheel_fl" => "Tug wheel FL",
                        "wheel_fr" => "Tug wheel FR",
                        "wheel_rl" => "Tug wheel RL",
                        "wheel_rr" => "Tug wheel RR",
                        _ => $"Tug {kitName}"
                    },
                    kitName =>
                    {
                        if (kitName.StartsWith("glass", StringComparison.Ordinal))
                            return new Color(0.2f, 0.4f, 0.55f, 0.42f);
                        return kitName switch
                        {
                            "wheel_fl" or "wheel_fr" or "wheel_rl" or "wheel_rr"
                                or "hub_fl" or "hub_fr" or "hub_rl" or "hub_rr"
                                or "towbar_wheel" => new Color(0.15f, 0.15f, 0.16f),
                            "towbar" or "towbar_head" or "towbar_handle" or "towbar_eye" or "tow_pivot"
                                or "towbar_pivot_mid"
                                => new Color(0.3f, 0.32f, 0.34f),
                            "beacon" => new Color(0.95f, 0.35f, 0.12f),
                            "headlight_l" or "headlight_r" => new Color(0.95f, 0.95f, 0.85f),
                            "taillight_l" or "taillight_r" => new Color(0.85f, 0.15f, 0.12f),
                            "stripe" => new Color(0.95f, 0.85f, 0.2f),
                            _ => new Color(0.82f, 0.62f, 0.18f)
                        };
                    },
                    localPosition: new Vector3(0f, -0.55f, 0f)))
            {
                artRoot.name = "Pushback tug";
                artRoot.gameObject.SetActive(false);
                return artRoot;
            }

            if (ArtPresentationLoader.TryInstantiatePrefab("mdl_pushback_tug_v03", out var prefabV03)
                || ArtPresentationLoader.TryInstantiatePrefab("mdl_pushback_tug_v02", out prefabV03)
                || ArtPresentationLoader.TryInstantiatePrefab("mdl_pushback_tug_v01", out prefabV03))
            {
                prefabV03.name = "Pushback tug";
                prefabV03.gameObject.SetActive(false);
                return prefabV03;
            }

            var root = new GameObject("Pushback tug").transform;
            var kit = PreferArtKit(
                "Models/Props/mdl_service_equipment_kit_v03.gltf",
                "Models/Props/mdl_service_equipment_kit_authored_v01.gltf",
                "Models/Props/mdl_service_equipment_kit_v02.gltf",
                "Models/Props/mdl_service_equipment_kit_v01.gltf");
            var placed = false;
            void PlaceTow(string mesh, Color color)
            {
                if (!ArtGltfLoader.TryPlaceNamedMesh(kit, mesh, Vector3.zero, Quaternion.identity, color, out var part))
                    return;
                part.SetParent(root, false);
                part.localPosition = new Vector3(0f, -0.55f, 0f);
                placed = true;
            }

            PlaceTow("towbar", new Color(0.82f, 0.62f, 0.18f));
            PlaceTow("towbar_head", new Color(0.3f, 0.32f, 0.34f));
            PlaceTow("towbar_wheel", new Color(0.15f, 0.15f, 0.16f));
            PlaceTow("towbar_handle", new Color(0.28f, 0.3f, 0.32f));
            PlaceTow("towbar_eye", new Color(0.35f, 0.36f, 0.38f));
            if (!placed)
            {
                ParentBlock(root, "Tug body", Vector3.zero, new Vector3(2.2f, 0.85f, 1.15f), new Color(0.82f, 0.62f, 0.18f));
                ParentBlock(root, "Tug cab", new Vector3(0.55f, 0.45f, 0f), new Vector3(0.9f, 0.7f, 1.0f), new Color(0.7f, 0.52f, 0.14f));
                ParentBlock(root, "Tug towbar", new Vector3(-1.4f, 0.05f, 0f), new Vector3(1.2f, 0.12f, 0.12f), new Color(0.3f, 0.3f, 0.32f));
                ParentBlock(root, "Tug wheel FL", new Vector3(0.6f, -0.35f, 0.45f), new Vector3(0.28f, 0.32f, 0.18f), new Color(0.15f, 0.15f, 0.16f));
                ParentBlock(root, "Tug wheel FR", new Vector3(0.6f, -0.35f, -0.45f), new Vector3(0.28f, 0.32f, 0.18f), new Color(0.15f, 0.15f, 0.16f));
                ParentBlock(root, "Tug wheel RL", new Vector3(-0.55f, -0.35f, 0.45f), new Vector3(0.28f, 0.32f, 0.18f), new Color(0.15f, 0.15f, 0.16f));
                ParentBlock(root, "Tug wheel RR", new Vector3(-0.55f, -0.35f, -0.45f), new Vector3(0.28f, 0.32f, 0.18f), new Color(0.15f, 0.15f, 0.16f));
                ParentBlock(root, "Tug beacon", new Vector3(0.55f, 0.9f, 0f), new Vector3(0.18f, 0.18f, 0.18f), new Color(0.95f, 0.35f, 0.12f));
            }

            root.gameObject.SetActive(false);
            return root;
        }

        private static Transform BuildWindsock()
        {
            // WLD-003 windsock — denser authored kit pole/fabric; animated sock parent stays procedural.
            var propsKit = PreferArtKit(
                "Models/Props/mdl_airfield_props_kit_authored_v01.gltf",
                "Models/Props/mdl_airfield_props_kit_v02.gltf",
                "Models/Props/mdl_airfield_props_kit_v01.gltf");
            var poleOrigin = new Vector3(-12f, 0f, 12f);
            var placedPole = ArtGltfLoader.TryPlaceNamedMesh(propsKit, "sock_base", poleOrigin, Quaternion.identity, new Color(0.35f, 0.36f, 0.38f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(propsKit, "sock_pole", poleOrigin, Quaternion.identity, new Color(0.75f, 0.75f, 0.72f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(propsKit, "windsock_pole", poleOrigin, Quaternion.identity, new Color(0.75f, 0.75f, 0.72f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(propsKit, "sock_frame", poleOrigin, Quaternion.identity, new Color(0.55f, 0.55f, 0.52f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(propsKit, "sock_swivel", poleOrigin, Quaternion.identity, new Color(0.45f, 0.46f, 0.48f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(propsKit, "sock_guy_l", poleOrigin, Quaternion.identity, new Color(0.4f, 0.4f, 0.42f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(propsKit, "sock_guy_r", poleOrigin, Quaternion.identity, new Color(0.4f, 0.4f, 0.42f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(propsKit, "sock_counterweight", poleOrigin, Quaternion.identity, new Color(0.3f, 0.32f, 0.34f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(propsKit, "sock_light", poleOrigin, Quaternion.identity, new Color(0.95f, 0.95f, 0.85f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(propsKit, "sock_ring", poleOrigin, Quaternion.identity, new Color(0.55f, 0.55f, 0.52f), out _);

            if (!placedPole && ArtPresentationLoader.TryInstantiatePrefab("mdl_windsock_pole_v01", out var polePrefab))
            {
                polePrefab.name = "Windsock pole";
                polePrefab.position = poleOrigin;
            }
            else if (!placedPole)
            {
                CreateBlock("Windsock pole", new Vector3(-12f, 1.6f, 12f), new Vector3(0.12f, 3.2f, 0.12f), new Color(0.75f, 0.75f, 0.72f));
                CreateBlock("Windsock hinge", new Vector3(-12f, 3.15f, 12f), new Vector3(0.22f, 0.22f, 0.22f), new Color(0.55f, 0.55f, 0.52f));
            }

            var sock = new GameObject("Windsock sock").transform;
            sock.position = new Vector3(-11.2f, 3.05f, 12f);
            sock.rotation = Quaternion.Euler(0f, 12f, 0f);
            var fabricPlaced = false;
            void PlaceFabric(string mesh, Color color)
            {
                if (!ArtGltfLoader.TryPlaceNamedMesh(propsKit, mesh, Vector3.zero, Quaternion.identity, color, out var part))
                    return;
                part.SetParent(sock, false);
                part.localPosition = Vector3.zero;
                part.localRotation = Quaternion.identity;
                fabricPlaced = true;
            }

            PlaceFabric("sock_fabric", new Color(0.92f, 0.55f, 0.12f));
            PlaceFabric("sock_fabric_mid", new Color(0.95f, 0.65f, 0.2f));
            PlaceFabric("sock_fabric_tip", new Color(0.95f, 0.95f, 0.92f));
            if (!fabricPlaced)
            {
                var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cylinder.name = "Windsock fabric";
                Object.Destroy(cylinder.GetComponent<Collider>());
                cylinder.transform.SetParent(sock, false);
                cylinder.transform.localPosition = Vector3.zero;
                cylinder.transform.localScale = new Vector3(0.55f, 0.55f, 1.35f);
                cylinder.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                cylinder.GetComponent<Renderer>().material = CreateMaterial(new Color(0.92f, 0.55f, 0.12f));
                var stripe = CreateBlock("Windsock stripe", new Vector3(-10.6f, 3.05f, 12f), new Vector3(0.35f, 0.52f, 0.52f),
                    new Color(0.95f, 0.95f, 0.92f));
                stripe.transform.SetParent(sock, true);
            }

            return sock;
        }

        private static void CreateCone(Vector3 position)
        {
            // Prefer denser authored props kit over thin Resources prefab (0025 item 2).
            var kit = PreferArtKit(
                "Models/Props/mdl_airfield_props_kit_authored_v01.gltf",
                "Models/Props/mdl_airfield_props_kit_v02.gltf",
                "Models/Props/mdl_airfield_props_kit_v01.gltf");
            var origin = position + new Vector3(0f, -0.25f, 0f);
            if (ArtGltfLoader.TryPlaceNamedMesh(kit, "cone_base", origin, Quaternion.identity, new Color(0.2f, 0.2f, 0.22f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "cone_body", origin, Quaternion.identity, new Color(0.95f, 0.45f, 0.08f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "cone_stripe", origin, Quaternion.identity, Color.white, out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "cone_tip", origin, Quaternion.identity, new Color(0.95f, 0.45f, 0.08f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "cone_collar", origin, Quaternion.identity, Color.white, out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "cone_handle", origin, Quaternion.identity, new Color(0.25f, 0.25f, 0.28f), out _))
                return;

            if (ArtGltfLoader.TryPlaceNamedMesh(kit, "cone", origin, Quaternion.identity, new Color(0.95f, 0.45f, 0.08f), out _))
                return;

            if (ArtPresentationLoader.TryInstantiatePrefab("mdl_safety_cone_v01", out var prefabRoot))
            {
                prefabRoot.name = "Safety cone";
                prefabRoot.position = position;
                return;
            }

            var cone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cone.name = "Safety cone";
            Object.Destroy(cone.GetComponent<Collider>());
            cone.transform.position = position;
            cone.transform.localScale = new Vector3(0.28f, 0.35f, 0.28f);
            cone.GetComponent<Renderer>().material = CreateMaterial(new Color(0.95f, 0.45f, 0.08f));
            CreateBlock("Cone collar", position + new Vector3(0f, 0.12f, 0f), new Vector3(0.32f, 0.06f, 0.32f), Color.white);
        }

        private static void CreateBarrier(Vector3 position, float yawDegrees)
        {
            var kit = PreferArtKit(
                "Models/Props/mdl_airfield_props_kit_authored_v01.gltf",
                "Models/Props/mdl_airfield_props_kit_v02.gltf",
                "Models/Props/mdl_airfield_props_kit_v01.gltf");
            var origin = position + new Vector3(0f, -0.45f, 0f);
            var rot = Quaternion.Euler(0f, yawDegrees, 0f);
            if (ArtGltfLoader.TryPlaceNamedMesh(kit, "barrier_rail", origin, rot, new Color(0.9f, 0.55f, 0.12f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "barrier_rail_low", origin, rot, new Color(0.9f, 0.55f, 0.12f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "barrier_stripe", origin, rot, Color.white, out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "barrier_stripe_b", origin, rot, Color.white, out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "barrier_brace", origin, rot, new Color(0.3f, 0.3f, 0.32f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "barrier_brace_b", origin, rot, new Color(0.3f, 0.3f, 0.32f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "barrier_top_cap", origin, rot, new Color(0.85f, 0.5f, 0.12f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "barrier_leg_l", origin, rot, new Color(0.25f, 0.25f, 0.28f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "barrier_leg_r", origin, rot, new Color(0.25f, 0.25f, 0.28f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "barrier_foot_l", origin, rot, new Color(0.3f, 0.3f, 0.32f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "barrier_foot_r", origin, rot, new Color(0.3f, 0.3f, 0.32f), out _))
                return;

            if (ArtGltfLoader.TryPlaceNamedMesh(kit, "barrier", origin, rot, new Color(0.9f, 0.55f, 0.12f), out _))
                return;

            if (ArtPresentationLoader.TryInstantiatePrefab("mdl_work_barrier_v01", out var prefabRoot))
            {
                prefabRoot.name = "Barrier";
                prefabRoot.position = position;
                prefabRoot.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
                return;
            }

            var root = new GameObject("Barrier").transform;
            root.position = position;
            root.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
            ParentBlock(root, "Barrier rail", Vector3.zero, new Vector3(2.4f, 0.12f, 0.12f), new Color(0.9f, 0.55f, 0.12f));
            ParentBlock(root, "Barrier leg L", new Vector3(-1.0f, -0.25f, 0f), new Vector3(0.12f, 0.55f, 0.12f), new Color(0.25f, 0.25f, 0.28f));
            ParentBlock(root, "Barrier leg R", new Vector3(1.0f, -0.25f, 0f), new Vector3(0.12f, 0.55f, 0.12f), new Color(0.25f, 0.25f, 0.28f));
        }

        private static void PlaceBuildingOrFallback(
            string artRelativePath,
            Vector3 worldPosition,
            System.Func<string, Color?> colorFor,
            System.Action fallback,
            string glassTextureRelativePath = null,
            string surfaceTextureRelativePath = null,
            Vector2? surfaceTextureTiling = null,
            string[] surfaceMeshNames = null)
        {
            if (ArtPresentationLoader.TryInstantiate(artRelativePath, null, out var root, rename: null, colorFor: colorFor))
            {
                root.position = worldPosition;
                if (!string.IsNullOrEmpty(glassTextureRelativePath))
                {
                    foreach (var child in root.GetComponentsInChildren<Transform>(true))
                    {
                        var n = child.name;
                        if (n is not ("glass_front" or "landside_glass" or "windows" or "cabin_windows"
                            or "door_glass" or "window_l" or "window_r" or "window_side" or "window_side_b"
                            or "windshield" or "rear_window" or "side_window" or "side_window_b"
                            or "office_window" or "skylight_l" or "skylight_r" or "skylight_mid"
                            or "door_peek_l" or "door_peek_r" or "boarding_glass" or "service_window")
                            && !n.StartsWith("glass_pane", StringComparison.Ordinal))
                            continue;
                        var renderer = child.GetComponent<Renderer>();
                        if (renderer == null)
                            continue;
                        var texture = TryLoadArtTexture(glassTextureRelativePath);
                        if (texture != null)
                        {
                            renderer.material.mainTexture = texture;
                            renderer.material.mainTextureScale = new Vector2(3f, 1.5f);
                        }
                    }
                }

                if (!string.IsNullOrEmpty(surfaceTextureRelativePath))
                {
                    var surface = TryLoadArtTexture(surfaceTextureRelativePath);
                    if (surface != null)
                    {
                        var tiling = surfaceTextureTiling ?? new Vector2(2f, 1.5f);
                        foreach (var child in root.GetComponentsInChildren<Transform>(true))
                        {
                            if (child.name is "glass_front" or "door_opening" or "entrance"
                                or "window_l" or "window_r" or "window_side" or "window_side_b"
                                or "cabin_windows" or "cockpit" or "landside_glass"
                                or "windshield" or "rear_window" or "side_window" or "side_window_b"
                                or "office_window" or "skylight_l" or "skylight_r" or "skylight_mid")
                                continue;
                            if (child.name.StartsWith("glass_pane", StringComparison.Ordinal))
                                continue;
                            if (surfaceMeshNames != null && surfaceMeshNames.Length > 0)
                            {
                                var match = false;
                                for (var i = 0; i < surfaceMeshNames.Length; i++)
                                {
                                    if (child.name == surfaceMeshNames[i] ||
                                        child.name.StartsWith(surfaceMeshNames[i], StringComparison.Ordinal))
                                    {
                                        match = true;
                                        break;
                                    }
                                }

                                if (!match)
                                    continue;
                            }

                            var renderer = child.GetComponent<Renderer>();
                            if (renderer == null)
                                continue;
                            renderer.material.mainTexture = surface;
                            renderer.material.mainTextureScale = tiling;
                            if (renderer.material.HasProperty("_Smoothness"))
                                renderer.material.SetFloat("_Smoothness", 0.28f);
                        }
                    }
                }

                return;
            }

            fallback();
        }

        private static void PlaceWorldMarkings()
        {
            // WLD-001 centreline: kit strip runs along Z; rotate onto the X runway axis.
            const string kit = "Models/Props/mdl_airfield_markings_kit_v01.gltf";
            var usedCentre = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "runway_centreline", Vector3.zero, Quaternion.Euler(0f, 90f, 0f), Color.white, out _);
            // Centreline dashes densify the strip when kit ships them (0025 item 3).
            ArtGltfLoader.TryPlaceNamedMesh(kit, "runway_centre_dash_a", new Vector3(-20f, 0.022f, 0f), Quaternion.Euler(0f, 90f, 0f), Color.white, out _);
            ArtGltfLoader.TryPlaceNamedMesh(kit, "runway_centre_dash_b", new Vector3(-8f, 0.022f, 0f), Quaternion.Euler(0f, 90f, 0f), Color.white, out _);
            ArtGltfLoader.TryPlaceNamedMesh(kit, "runway_centre_dash_c", new Vector3(8f, 0.022f, 0f), Quaternion.Euler(0f, 90f, 0f), Color.white, out _);
            ArtGltfLoader.TryPlaceNamedMesh(kit, "runway_centre_dash_d", new Vector3(20f, 0.022f, 0f), Quaternion.Euler(0f, 90f, 0f), Color.white, out _);

            if (!usedCentre)
            {
                for (var x = -34; x <= 34; x += 8)
                    CreateBlock("Runway marking", new Vector3(x, 0.02f, 0f), new Vector3(3.5f, 0.03f, 0.28f), Color.white);
            }

            // Kit edge / threshold strips when present; greybox fallbacks keep the 7 m strip readable.
            var usedEdgeL = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "runway_edge_left", new Vector3(0f, 0.025f, -3.35f), Quaternion.Euler(0f, 90f, 0f), Color.white, out _);
            var usedEdgeR = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "runway_edge_right", new Vector3(0f, 0.025f, 3.35f), Quaternion.Euler(0f, 90f, 0f), Color.white, out _);
            if (!usedEdgeL)
                CreateBlock("Runway edge L", new Vector3(0f, 0.025f, -3.35f), new Vector3(72f, 0.02f, 0.22f), Color.white);
            if (!usedEdgeR)
                CreateBlock("Runway edge R", new Vector3(0f, 0.025f, 3.35f), new Vector3(72f, 0.02f, 0.22f), Color.white);

            var usedThresholdW = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "runway_threshold", new Vector3(-36f, 0.03f, 0f), Quaternion.Euler(0f, 90f, 0f), Color.white, out _);
            var usedThresholdE = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "runway_threshold", new Vector3(36f, 0.03f, 0f), Quaternion.Euler(0f, -90f, 0f), Color.white, out _);
            // Threshold bar densify + side bars when kit present.
            foreach (var bar in new[] { "threshold_bar_a", "threshold_bar_b", "threshold_bar_c", "threshold_bar_d" })
            {
                ArtGltfLoader.TryPlaceNamedMesh(kit, bar, new Vector3(-36f, 0.032f, 0f), Quaternion.Euler(0f, 90f, 0f), Color.white, out _);
                ArtGltfLoader.TryPlaceNamedMesh(kit, bar, new Vector3(36f, 0.032f, 0f), Quaternion.Euler(0f, -90f, 0f), Color.white, out _);
            }

            var usedSideWL = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "threshold_side_l", new Vector3(-36f, 0.032f, -3.0f), Quaternion.Euler(0f, 90f, 0f), Color.white, out _);
            var usedSideWR = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "threshold_side_r", new Vector3(-36f, 0.032f, 3.0f), Quaternion.Euler(0f, 90f, 0f), Color.white, out _);
            var usedSideEL = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "threshold_side_l", new Vector3(36f, 0.032f, -3.0f), Quaternion.Euler(0f, -90f, 0f), Color.white, out _);
            var usedSideER = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "threshold_side_r", new Vector3(36f, 0.032f, 3.0f), Quaternion.Euler(0f, -90f, 0f), Color.white, out _);
            if (!usedThresholdW || !usedThresholdE)
            {
                for (var z = -2.4f; z <= 2.4f; z += 0.8f)
                {
                    if (!usedThresholdW)
                        CreateBlock("Threshold W", new Vector3(-36f, 0.03f, z), new Vector3(2.2f, 0.02f, 0.35f), Color.white);
                    if (!usedThresholdE)
                        CreateBlock("Threshold E", new Vector3(36f, 0.03f, z), new Vector3(2.2f, 0.02f, 0.35f), Color.white);
                }
            }

            var holdYellow = new Color(0.95f, 0.82f, 0.12f);
            var usedHoldA = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "hold_short_a", new Vector3(-12f, 0.05f, 6.6f), Quaternion.identity, holdYellow, out var holdA);
            var usedHoldB = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "hold_short_b", new Vector3(-12f, 0.05f, 7.1f), Quaternion.identity, holdYellow, out var holdB);
            var usedHoldC = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "hold_short_c", new Vector3(4f, 0.05f, 6.6f), Quaternion.identity, holdYellow, out var holdC);
            var usedHoldD = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "hold_short_d", new Vector3(4f, 0.05f, 7.1f), Quaternion.identity, holdYellow, out var holdD);
            if (holdA != null) holdA.name = "Hold short A";
            if (holdB != null) holdB.name = "Hold short B";
            if (holdC != null) holdC.name = "Hold short C";
            if (holdD != null) holdD.name = "Hold short D";
            if (!usedHoldA)
                CreateBlock("Hold short A", new Vector3(-12f, 0.05f, 6.6f), new Vector3(4.2f, 0.03f, 0.22f), holdYellow);
            if (!usedHoldB)
                CreateBlock("Hold short B", new Vector3(-12f, 0.05f, 7.1f), new Vector3(4.2f, 0.03f, 0.22f), holdYellow);
            if (!usedHoldC)
                CreateBlock("Hold short C", new Vector3(4f, 0.05f, 6.6f), new Vector3(3.6f, 0.03f, 0.2f), holdYellow);
            if (!usedHoldD)
                CreateBlock("Hold short D", new Vector3(4f, 0.05f, 7.1f), new Vector3(3.6f, 0.03f, 0.2f), holdYellow);
            // Readable block digits for 09 / 27 (facing inbound traffic).
            PlaceRunwayDigit('0', new Vector3(-34.6f, 0.04f, 0f), yaw: 90f);
            PlaceRunwayDigit('9', new Vector3(-32.6f, 0.04f, 0f), yaw: 90f);
            PlaceRunwayDigit('2', new Vector3(32.6f, 0.04f, 0f), yaw: -90f);
            PlaceRunwayDigit('7', new Vector3(34.6f, 0.04f, 0f), yaw: -90f);
            // Side stripes beside threshold bars — only when kit sides missed (avoid z-fight).
            if (!usedSideWL)
                CreateBlock("Threshold stripe W L", new Vector3(-36f, 0.03f, -3.05f), new Vector3(2.2f, 0.02f, 0.45f), Color.white);
            if (!usedSideWR)
                CreateBlock("Threshold stripe W R", new Vector3(-36f, 0.03f, 3.05f), new Vector3(2.2f, 0.02f, 0.45f), Color.white);
            if (!usedSideEL)
                CreateBlock("Threshold stripe E L", new Vector3(36f, 0.03f, -3.05f), new Vector3(2.2f, 0.02f, 0.45f), Color.white);
            if (!usedSideER)
                CreateBlock("Threshold stripe E R", new Vector3(36f, 0.03f, 3.05f), new Vector3(2.2f, 0.02f, 0.45f), Color.white);

            // Aiming-point pairs (WLD markings language) — kit first, greybox fallback.
            foreach (var x in new[] { -18f, 18f })
            {
                var placedL = ArtGltfLoader.TryPlaceNamedMesh(
                    kit, "aiming_point_l", new Vector3(x, 0.035f, -1.55f), Quaternion.identity, Color.white, out _);
                var placedR = ArtGltfLoader.TryPlaceNamedMesh(
                    kit, "aiming_point_r", new Vector3(x, 0.035f, 1.55f), Quaternion.identity, Color.white, out _);
                if (!placedL)
                    CreateBlock($"Aiming point {x} L", new Vector3(x, 0.035f, -1.55f), new Vector3(2.8f, 0.025f, 1.1f), Color.white);
                if (!placedR)
                    CreateBlock($"Aiming point {x} R", new Vector3(x, 0.035f, 1.55f), new Vector3(2.8f, 0.025f, 1.1f), Color.white);
            }

            // Touchdown zone marks between threshold and aiming points.
            foreach (var x in new[] { -30f, -28f, -26f, -24f, -22f, 22f, 24f, 26f, 28f, 30f })
            {
                var placedL = ArtGltfLoader.TryPlaceNamedMesh(
                    kit, "tdz_mark_l", new Vector3(x, 0.03f, -1.4f), Quaternion.identity, Color.white, out _);
                var placedR = ArtGltfLoader.TryPlaceNamedMesh(
                    kit, "tdz_mark_r", new Vector3(x, 0.03f, 1.4f), Quaternion.identity, Color.white, out _);
                if (!placedL)
                    CreateBlock($"TDZ {x} L", new Vector3(x, 0.03f, -1.4f), new Vector3(1.4f, 0.02f, 0.5f), Color.white);
                if (!placedR)
                    CreateBlock($"TDZ {x} R", new Vector3(x, 0.03f, 1.4f), new Vector3(1.4f, 0.02f, 0.5f), Color.white);
            }
            // Stand bay numbers on the apron (readable from overview) — kit digit bars preferred.
            PlaceRunwayDigit('1', new Vector3(14.2f, 0.04f, 14f), yaw: 0f);
            PlaceRunwayDigit('2', new Vector3(14.2f, 0.04f, 20f), yaw: 0f);
            PlaceRunwayDigit('3', new Vector3(14.2f, 0.04f, 26f), yaw: 0f);

            var usedStandA = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "stand_stop_a", new Vector3(14f, 0.04f, 16.2f), Quaternion.identity,
                new Color(0.95f, 0.85f, 0.2f), out _);
            var usedStandB = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "stand_stop_b", new Vector3(22f, 0.04f, 16.2f), Quaternion.identity,
                new Color(0.95f, 0.85f, 0.2f), out _);
            if (!usedStandA)
                CreateBlock("Stand stop 1", new Vector3(14f, 0.04f, 16.2f), new Vector3(2.8f, 0.02f, 0.18f), new Color(0.95f, 0.85f, 0.2f));
            if (!usedStandB)
                CreateBlock("Stand stop 2", new Vector3(22f, 0.04f, 16.2f), new Vector3(2.8f, 0.02f, 0.18f), new Color(0.95f, 0.85f, 0.2f));
            var usedStandC = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "stand_stop_c", new Vector3(30f, 0.04f, 16.2f), Quaternion.identity,
                new Color(0.95f, 0.85f, 0.2f), out _);
            if (!usedStandC)
                CreateBlock("Stand stop 3", new Vector3(30f, 0.04f, 16.2f), new Vector3(2.8f, 0.02f, 0.18f), new Color(0.95f, 0.85f, 0.2f));

            // Taxi markings are authored along local X (see generate-batch-b-surfaces).
            // Identity rotation keeps them on Taxiway A; Yaw 90 sent them across the apron
            // and gated off the greybox dashes. Centreline mesh is 20 m — place two copies.
            var taxiPaint = new Color(0.95f, 0.85f, 0.2f);
            var usedTaxiWest = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "taxi_centreline", new Vector3(8f, 0.035f, 9f), Quaternion.identity,
                taxiPaint, out _);
            var usedTaxiEast = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "taxi_centreline", new Vector3(28f, 0.035f, 9f), Quaternion.identity,
                taxiPaint, out _);
            if (!usedTaxiWest && !usedTaxiEast)
            {
                for (var x = -6; x <= 28; x += 5)
                    CreateBlock($"Taxi centre {x}", new Vector3(x, 0.035f, 9f), new Vector3(2.2f, 0.02f, 0.16f),
                        taxiPaint);
            }
            else
            {
                // Extend paint onto the A1 exit fillet when the kit only covers Taxiway A.
                for (var x = -22; x <= -14; x += 4)
                    CreateBlock($"Taxi exit centre {x}", new Vector3(x, 0.035f, 4.5f + (x + 22f) * 0.35f),
                        new Vector3(2.0f, 0.02f, 0.14f), taxiPaint);
            }

            // Edges: mesh already carries ±1.85 Z offset — place at taxi centre, identity yaw.
            // Two copies match the dual centreline coverage along Taxiway A.
            var usedTaxiEdgeN = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "taxi_edge_n", new Vector3(8f, 0.035f, 9f), Quaternion.identity, Color.white, out _);
            var usedTaxiEdgeS = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "taxi_edge_s", new Vector3(8f, 0.035f, 9f), Quaternion.identity, Color.white, out _);
            ArtGltfLoader.TryPlaceNamedMesh(
                kit, "taxi_edge_n", new Vector3(28f, 0.035f, 9f), Quaternion.identity, Color.white, out _);
            ArtGltfLoader.TryPlaceNamedMesh(
                kit, "taxi_edge_s", new Vector3(28f, 0.035f, 9f), Quaternion.identity, Color.white, out _);
            if (!usedTaxiEdgeN)
                CreateBlock("Taxi edge N", new Vector3(8f, 0.035f, 10.85f), new Vector3(44f, 0.02f, 0.14f), Color.white);
            if (!usedTaxiEdgeS)
                CreateBlock("Taxi edge S", new Vector3(8f, 0.035f, 7.15f), new Vector3(44f, 0.02f, 0.14f), Color.white);
            // Apron lead-in chevrons from taxi to stand lead — kit chevrons when present.
            for (var i = 0; i < 4; i++)
            {
                var z = 11.2f + i * 0.85f;
                var pos = new Vector3(14f + i * 0.4f, 0.04f, z);
                var mesh = i % 2 == 0 ? "chevron_lead_a" : "chevron_lead_b";
                if (i == 2) mesh = "chevron_lead_c";
                if (i == 3) mesh = "chevron_lead_d";
                if (!ArtGltfLoader.TryPlaceNamedMesh(
                        kit, mesh, pos, Quaternion.Euler(0f, 25f, 0f),
                        new Color(0.95f, 0.85f, 0.2f), out _))
                {
                    CreateBlock($"Apron chevron {i}", pos, new Vector3(1.1f, 0.02f, 0.16f),
                        new Color(0.95f, 0.85f, 0.2f));
                }
            }

            // Taxi direction arrows on Taxiway A (kit shaft+head or greybox).
            PlaceTaxiArrow(kit, new Vector3(-4f, 0.04f, 9f), 90f);
            PlaceTaxiArrow(kit, new Vector3(18f, 0.04f, 9f), 90f);
            PlaceTaxiArrow(kit, new Vector3(14f, 0.04f, 12.5f), 0f);
            // Apron entry arrows from densified kit when present.
            ArtGltfLoader.TryPlaceNamedMesh(kit, "apron_arrow_a", new Vector3(12f, 0.04f, 11.5f), Quaternion.identity,
                new Color(0.95f, 0.85f, 0.2f), out _);
            ArtGltfLoader.TryPlaceNamedMesh(kit, "apron_arrow_b", new Vector3(16f, 0.04f, 12.8f), Quaternion.identity,
                new Color(0.95f, 0.85f, 0.2f), out _);

            // Hold-short across the A1 fillet (path (-24,0)→(-12,9)), not beside it.
            var holdPos = new Vector3(-18f, 0.05f, 4.5f);
            var holdYaw = Mathf.Atan2(12f, 9f) * Mathf.Rad2Deg + 90f;
            var holdRot = Quaternion.Euler(0f, holdYaw, 0f);
            if (!ArtGltfLoader.TryPlaceNamedMesh(kit, "hold_short_e", holdPos, holdRot, holdYellow, out _))
            {
                var bar = CreateBlock("Hold short A1", holdPos, new Vector3(3.4f, 0.03f, 0.22f), holdYellow);
                bar.transform.rotation = holdRot;
            }

            if (!ArtGltfLoader.TryPlaceNamedMesh(
                    kit, "hold_short_f", holdPos + holdRot * new Vector3(0f, 0f, 0.45f), holdRot, holdYellow, out _))
            {
                var bar2 = CreateBlock("Hold short A1 b", holdPos + holdRot * new Vector3(0f, 0f, 0.45f),
                    new Vector3(3.4f, 0.03f, 0.22f), holdYellow);
                bar2.transform.rotation = holdRot;
            }

            // Stand lead-in dashes — skip when markings kit already placed stand stops
            // (otherwise landing/follow cameras see a carpet of yellow cubes).
            if (GameObject.Find("stand_stop_a") == null
                && GameObject.Find("stand_stop_b") == null
                && GameObject.Find("stand_stop_c") == null)
            {
                foreach (var standX in new[] { 14f, 22f })
                {
                    for (var step = 0; step < 3; step++)
                    {
                        var z = 12.4f + step * 1.1f;
                        CreateBlock($"Stand lead {standX} {step}", new Vector3(standX, 0.04f, z),
                            new Vector3(0.14f, 0.02f, 0.4f), new Color(0.95f, 0.85f, 0.2f));
                    }
                }
            }
        }

        private static void PlaceTaxiArrow(string kit, Vector3 position, float yaw)
        {
            var root = new GameObject("Taxi arrow").transform;
            root.position = position;
            root.rotation = Quaternion.Euler(0f, yaw, 0f);
            var yellow = new Color(0.95f, 0.85f, 0.2f);
            var used = false;
            void PlacePart(string mesh, Vector3 local)
            {
                if (!ArtGltfLoader.TryPlaceNamedMesh(kit, mesh, Vector3.zero, Quaternion.identity, yellow, out var part))
                    return;
                part.SetParent(root, false);
                part.localPosition = local;
                part.localRotation = Quaternion.identity;
                used = true;
            }

            PlacePart("taxi_arrow_shaft", Vector3.zero);
            PlacePart("taxi_arrow_head_l", Vector3.zero);
            PlacePart("taxi_arrow_head_r", Vector3.zero);
            PlacePart("taxi_arrow_head_cap", Vector3.zero);
            if (used)
                return;

            ParentBlock(root, "shaft", new Vector3(0f, 0f, -0.2f), new Vector3(0.28f, 0.03f, 1.6f), yellow);
            ParentBlock(root, "head L", new Vector3(-0.35f, 0f, 0.7f), new Vector3(0.55f, 0.03f, 0.35f), yellow);
            ParentBlock(root, "head R", new Vector3(0.35f, 0f, 0.7f), new Vector3(0.55f, 0.03f, 0.35f), yellow);
        }

        /// <summary>
        /// Decision 0025 item 3 — block runway digits readable from overview.
        /// Prefer WLD kit digit bars when present; greybox segments remain the fallback.
        /// Local +Z is digit height; yaw rotates onto the runway axis.
        /// </summary>
        private static void PlaceRunwayDigit(char digit, Vector3 centre, float yaw)
        {
            const string kit = "Models/Props/mdl_airfield_markings_kit_v01.gltf";
            var root = new GameObject($"Runway digit {digit}").transform;
            root.position = centre;
            root.rotation = Quaternion.Euler(0f, yaw, 0f);
            void Seg(string name, float x, float z, float sx, float sz)
            {
                // Prefer kit digit bars for micro-relief; scale locally to match segment size.
                string mesh;
                if (name.Contains("serif", StringComparison.Ordinal))
                    mesh = "digit_serif";
                else if (sx > sz * 1.2f)
                    mesh = name.Contains("mid", StringComparison.Ordinal) ? "digit_bar_h_short" : "digit_bar_h";
                else
                    mesh = "digit_bar_v";

                if (ArtGltfLoader.TryPlaceNamedMesh(kit, mesh, new Vector3(x, 0f, z), Quaternion.identity, Color.white, out var part))
                {
                    part.SetParent(root, false);
                    part.localPosition = new Vector3(x, 0f, z);
                    part.localScale = new Vector3(
                        Mathf.Max(0.35f, sx / 1.1f),
                        1f,
                        Mathf.Max(0.35f, sz / 1.9f));
                    return;
                }

                ParentBlock(root, name, new Vector3(x, 0f, z), new Vector3(sx, 0.03f, sz), Color.white);
            }

            switch (digit)
            {
                case '0':
                    Seg("top", 0f, 0.95f, 1.1f, 0.28f);
                    Seg("bot", 0f, -0.95f, 1.1f, 0.28f);
                    Seg("left", -0.55f, 0f, 0.28f, 1.9f);
                    Seg("right", 0.55f, 0f, 0.28f, 1.9f);
                    break;
                case '2':
                    Seg("top", 0f, 0.95f, 1.1f, 0.28f);
                    Seg("mid", 0f, 0f, 1.1f, 0.28f);
                    Seg("bot", 0f, -0.95f, 1.1f, 0.28f);
                    Seg("ur", 0.55f, 0.5f, 0.28f, 0.9f);
                    Seg("ll", -0.55f, -0.5f, 0.28f, 0.9f);
                    break;
                case '7':
                    Seg("top", 0f, 0.95f, 1.1f, 0.28f);
                    Seg("stem", 0.35f, -0.1f, 0.28f, 1.9f);
                    break;
                case '9':
                    Seg("top", 0f, 0.95f, 1.1f, 0.28f);
                    Seg("mid", 0f, 0.1f, 1.1f, 0.28f);
                    Seg("ul", -0.55f, 0.55f, 0.28f, 0.85f);
                    Seg("ur", 0.55f, 0.55f, 0.28f, 0.85f);
                    Seg("stem", 0.55f, -0.45f, 0.28f, 1.0f);
                    break;
                case '1':
                    Seg("stem", 0f, 0f, 0.32f, 1.9f);
                    Seg("base", 0f, -0.95f, 0.85f, 0.28f);
                    Seg("serif", -0.28f, 0.7f, 0.45f, 0.28f);
                    break;
                case '3':
                    Seg("top", 0f, 0.95f, 1.1f, 0.28f);
                    Seg("mid", 0f, 0f, 1.0f, 0.28f);
                    Seg("bot", 0f, -0.95f, 1.1f, 0.28f);
                    Seg("ur", 0.55f, 0.5f, 0.28f, 0.9f);
                    Seg("lr", 0.55f, -0.5f, 0.28f, 0.9f);
                    break;
            }
        }

        /// <summary>
        /// Decision 0025 items 1+3 — workbench / shelves / drums so the open hangar
        /// bay reads occupied instead of an empty shell.
        /// </summary>
        private static void BuildHangarBayInterior()
        {
            if (ArtPresentationLoader.TryInstantiatePrefab("mdl_hangar_bay_props_v01", out var props))
            {
                props.name = "Hangar bay props";
                props.position = new Vector3(-20f, 0f, 18.2f);
                props.rotation = Quaternion.Euler(0f, 8f, 0f);
                return;
            }

            var root = new GameObject("Hangar bay props").transform;
            root.position = new Vector3(-20f, 0f, 18.2f);
            ParentBlock(root, "Workbench top", new Vector3(0f, 0.85f, 0f), new Vector3(2.4f, 0.12f, 0.9f), new Color(0.45f, 0.42f, 0.38f));
            ParentBlock(root, "Workbench vise", new Vector3(0.85f, 0.98f, 0.15f), new Vector3(0.35f, 0.28f, 0.35f), new Color(0.35f, 0.36f, 0.38f));
            ParentBlock(root, "Workbench leg L", new Vector3(-1f, 0.4f, 0f), new Vector3(0.12f, 0.8f, 0.8f), new Color(0.25f, 0.25f, 0.28f));
            ParentBlock(root, "Workbench leg R", new Vector3(1f, 0.4f, 0f), new Vector3(0.12f, 0.8f, 0.8f), new Color(0.25f, 0.25f, 0.28f));
            ParentBlock(root, "Shelf frame", new Vector3(-2.2f, 1.1f, -0.1f), new Vector3(0.9f, 1.8f, 0.45f), new Color(0.4f, 0.42f, 0.4f));
            ParentBlock(root, "Shelf board mid", new Vector3(-2.2f, 1.0f, -0.1f), new Vector3(0.85f, 0.08f, 0.4f), new Color(0.5f, 0.45f, 0.35f));
            ParentBlock(root, "Oil drum", new Vector3(-1.6f, 0.55f, 0.9f), new Vector3(0.55f, 1.1f, 0.55f), new Color(0.85f, 0.55f, 0.18f));
            ParentBlock(root, "Oil drum B", new Vector3(-0.9f, 0.45f, 1.1f), new Vector3(0.45f, 0.9f, 0.45f), new Color(0.75f, 0.45f, 0.15f));
            ParentBlock(root, "Tool cart body", new Vector3(1.8f, 0.55f, 0.6f), new Vector3(0.9f, 0.7f, 0.7f), new Color(0.35f, 0.45f, 0.55f));
            ParentBlock(root, "Crate stack", new Vector3(2.3f, 0.45f, -0.5f), new Vector3(0.7f, 0.9f, 0.55f), new Color(0.55f, 0.4f, 0.22f));
            ParentBlock(root, "Tire rack", new Vector3(-0.2f, 0.7f, -1.0f), new Vector3(1.4f, 1.2f, 0.35f), new Color(0.3f, 0.32f, 0.34f));
            ParentBlock(root, "Fire extinguisher", new Vector3(0.9f, 0.55f, -0.9f), new Vector3(0.22f, 0.7f, 0.22f), new Color(0.85f, 0.15f, 0.12f));
            ParentBlock(root, "Parts bin", new Vector3(0.4f, 0.35f, 0.7f), new Vector3(0.55f, 0.4f, 0.4f), new Color(0.55f, 0.55f, 0.2f));
        }

        private static void PlaceWorldLighting()
        {
            var kit = PreferArtKit(
                "Models/Props/mdl_airfield_lighting_kit_authored_v01.gltf",
                "Models/Props/mdl_airfield_lighting_kit_v02.gltf",
                "Models/Props/mdl_airfield_lighting_kit_v01.gltf");
            var hasLightingKit = !string.IsNullOrEmpty(kit) && ArtGltfLoader.HasKit(kit);
            var edgeColor = new Color(1f, 1f, 0.85f);
            var taxiColor = new Color(0.25f, 0.55f, 1f);
            var obstruction = new Color(0.95f, 0.35f, 0.12f);

            var edgeStep = hasLightingKit ? 12 : 6;
            for (var x = -36; x <= 36; x += edgeStep)
            {
                PlaceEdgeLamp(kit, new Vector3(x, 0f, -3.4f), edgeColor);
                PlaceEdgeLamp(kit, new Vector3(x, 0f, 3.4f), edgeColor);
            }

            var taxiStep = hasLightingKit ? 12 : 8;
            for (var x = -8; x <= 28; x += taxiStep)
            {
                PlaceTaxiLamp(kit, new Vector3(x, 0f, 11.1f), taxiColor);
                PlaceTaxiLamp(kit, new Vector3(x, 0f, 6.9f), taxiColor);
            }

            // A1 exit fillet fixtures — path (-24,0)→(-12,9).
            PlaceTaxiLamp(kit, new Vector3(-22f, 0f, 2.2f), taxiColor);
            PlaceTaxiLamp(kit, new Vector3(-18f, 0f, 4.5f), taxiColor);
            PlaceTaxiLamp(kit, new Vector3(-14f, 0f, 7f), taxiColor);

            PlaceObstructionLamp(kit, new Vector3(-20f, 5.0f, 20f), obstruction, "Hangar obstruction");
            PlaceObstructionLamp(kit, new Vector3(26f, 4.5f, 27f), obstruction, "Terminal roof light");
            PlaceObstructionLamp(kit, new Vector3(-8f, 3.2f, 26f), obstruction, "Ops obstruction");

            // Apron flood poles — four corners so night turnarounds read lit.
            var flood = new Color(0.75f, 0.78f, 0.8f);
            PlaceFloodMast(kit, new Vector3(8f, 0f, 12f), flood);
            PlaceFloodMast(kit, new Vector3(32f, 0f, 12f), flood);
            PlaceFloodMast(kit, new Vector3(8f, 0f, 22f), flood);
            PlaceFloodMast(kit, new Vector3(32f, 0f, 22f), flood);
        }

        private static void PlaceEdgeLamp(string kit, Vector3 position, Color color)
        {
            // Kit silhouette: base + stem + lens only (skip collar/gasket/glare/reflector soup).
            if (ArtGltfLoader.TryPlaceNamedMesh(kit, "edge_base", position, Quaternion.identity, new Color(0.35f, 0.36f, 0.38f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "edge_stem", position, Quaternion.identity, new Color(0.45f, 0.46f, 0.48f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "edge_lens", position, Quaternion.identity, color, out _))
                return;
            if (!ArtGltfLoader.TryPlaceNamedMesh(kit, "runway_edge_light", position, Quaternion.identity, color, out _))
                CreateBlock("Runway edge", position + new Vector3(0f, 0.05f, 0f), new Vector3(0.25f, 0.1f, 0.25f), color);
        }

        private static void PlaceTaxiLamp(string kit, Vector3 position, Color color)
        {
            if (ArtGltfLoader.TryPlaceNamedMesh(kit, "taxi_base", position, Quaternion.identity, new Color(0.3f, 0.32f, 0.34f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "taxi_stem", position, Quaternion.identity, new Color(0.4f, 0.42f, 0.44f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "taxi_lens", position, Quaternion.identity, color, out _))
                return;
            if (!ArtGltfLoader.TryPlaceNamedMesh(kit, "taxiway_light", position, Quaternion.identity, color, out _))
                CreateBlock("Taxi light", position + new Vector3(0f, 0.18f, 0f), new Vector3(0.18f, 0.35f, 0.18f), color);
        }

        private static void PlaceObstructionLamp(string kit, Vector3 position, Color color, string fallbackName)
        {
            if (ArtGltfLoader.TryPlaceNamedMesh(kit, "obst_base", position, Quaternion.identity, new Color(0.35f, 0.36f, 0.38f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "obst_stem", position, Quaternion.identity, new Color(0.4f, 0.42f, 0.44f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "obst_lens", position, Quaternion.identity, color, out _))
                return;
            if (!ArtGltfLoader.TryPlaceNamedMesh(kit, "obstruction_light", position, Quaternion.identity, color, out _))
                CreateBlock(fallbackName, position + new Vector3(0f, 0.2f, 0f), new Vector3(0.22f, 0.22f, 0.22f), color);
        }

        private static void PlaceFloodMast(string kit, Vector3 position, Color color)
        {
            // Kit path: mast silhouette only — SpotLights in BuildApronLights still own night pools.
            if (ArtGltfLoader.TryPlaceNamedMesh(kit, "flood_base", position, Quaternion.identity, new Color(0.3f, 0.32f, 0.34f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "flood_pole", position, Quaternion.identity, color, out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "flood_crossarm", position, Quaternion.identity, Shade(color, 0.9f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "flood_arm", position, Quaternion.identity, Shade(color, 0.85f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "flood_head", position, Quaternion.identity, new Color(0.25f, 0.26f, 0.28f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "flood_lamp", position, Quaternion.identity, new Color(1f, 0.95f, 0.8f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "flood_visor", position, Quaternion.identity, new Color(0.2f, 0.21f, 0.22f), out _))
                return;

            if (!ArtGltfLoader.TryPlaceNamedMesh(kit, "apron_floodlight", position, Quaternion.identity, color, out _))
            {
                CreateBlock("Flood pole", position + new Vector3(0f, 4f, 0f), new Vector3(0.25f, 8f, 0.25f), color);
                CreateBlock("Flood head", position + new Vector3(0f, 8.1f, 0f), new Vector3(1.2f, 0.35f, 0.55f), new Color(0.25f, 0.26f, 0.28f));
            }
        }

        private static void PlaceWorldProps()
        {
            var kit = PreferArtKit(
                "Models/Props/mdl_airfield_props_kit_authored_v01.gltf",
                "Models/Props/mdl_airfield_props_kit_v02.gltf",
                "Models/Props/mdl_airfield_props_kit_v01.gltf");
            var hasPropsKit = !string.IsNullOrEmpty(kit) && ArtGltfLoader.HasKit(kit);

            // Stand lead-in cones — hero corners when props kit stamps multi-mesh cones.
            CreateCone(new Vector3(12.5f, 0.25f, 12.2f));
            CreateCone(new Vector3(12.5f, 0.25f, 21.8f));
            CreateCone(new Vector3(23.5f, 0.25f, 12.2f));
            CreateCone(new Vector3(23.5f, 0.25f, 21.8f));
            if (!hasPropsKit)
            {
                CreateCone(new Vector3(12.5f, 0.25f, 15.8f));
                CreateCone(new Vector3(12.5f, 0.25f, 18.2f));
                CreateCone(new Vector3(23.5f, 0.25f, 15.8f));
                CreateCone(new Vector3(23.5f, 0.25f, 18.2f));
                CreateCone(new Vector3(-6f, 0.25f, 11f));
                CreateCone(new Vector3(-10f, 0.25f, 11f));
                CreateCone(new Vector3(4f, 0.25f, 7.2f));
                CreateCone(new Vector3(4f, 0.25f, 10.8f));
                CreateCone(new Vector3(18f, 0.25f, 11.2f));
                CreateCone(new Vector3(28f, 0.25f, 11.2f));
                CreateCone(new Vector3(8f, 0.25f, 23.5f));
                CreateCone(new Vector3(30f, 0.25f, 23.5f));
            }
            else
            {
                // Taxi lead-in pair so the A1 entry still reads marked.
                CreateCone(new Vector3(4f, 0.25f, 7.2f));
                CreateCone(new Vector3(4f, 0.25f, 10.8f));
            }

            // Worksite / hangar barriers — thin when kit barriers are heavy silhouettes.
            CreateBarrier(new Vector3(-14f, 0.45f, 14f), 0f);
            CreateBarrier(new Vector3(-22f, 0.45f, 25.5f), 90f);
            if (!hasPropsKit)
            {
                if (!ArtPresentationLoader.HasPrefab("mdl_fuel_farm_v01"))
                    CreateBarrier(new Vector3(-28f, 0.45f, 18f), 0f);
                CreateBarrier(new Vector3(36f, 0.45f, 18f), 90f);
                CreateBarrier(new Vector3(6f, 0.45f, 24.5f), 0f);
                CreateBarrier(new Vector3(34f, 0.45f, 24.5f), 0f);
            }

            PlaceSignBoard(kit, new Vector3(10f, 0f, 22f), 90f);
            PlaceSignBoard(kit, new Vector3(-4f, 0f, 12f), 0f);
            if (!hasPropsKit)
            {
                PlaceSignBoard(kit, new Vector3(18f, 0f, 11.5f), 0f);
                PlaceSignBoard(kit, new Vector3(28f, 0f, 12f), 0f);
                PlaceSignBoard(kit, new Vector3(-18f, 0f, 16f), 90f);
            }

            // Hero dolly pair when props kit is dense; greybox keeps the fuller apron stack.
            PlaceBaggageDolly(kit, new Vector3(30f, 0f, 22f));
            PlaceBaggageDolly(kit, new Vector3(32.2f, 0f, 22f));
            if (!hasPropsKit)
            {
                PlaceBaggageDolly(kit, new Vector3(28f, 0f, 19.5f));
                PlaceBaggageDolly(kit, new Vector3(34f, 0f, 19.5f));
                PlaceBaggageDolly(kit, new Vector3(31f, 0f, 17.2f));
                PlaceBaggageDolly(kit, new Vector3(33.5f, 0f, 17.2f));
            }

            // Belt loaders live in the service kit, not the props kit (0025 wiring bug).
            // One authored hero loader when the kit is present; second only on greybox.
            var serviceKit = PreferArtKit(
                "Models/Props/mdl_service_equipment_kit_v03.gltf",
                "Models/Props/mdl_service_equipment_kit_authored_v01.gltf",
                "Models/Props/mdl_service_equipment_kit_v02.gltf",
                "Models/Props/mdl_service_equipment_kit_v01.gltf");
            var hasServiceKit = !string.IsNullOrEmpty(serviceKit) && ArtGltfLoader.HasKit(serviceKit);
            PlaceBeltLoader(serviceKit, new Vector3(12.5f, 0f, 21.5f), 200f, silhouetteOnly: hasServiceKit);
            if (!hasServiceKit)
                PlaceBeltLoader(serviceKit, new Vector3(29.5f, 0f, 15.5f), 110f, silhouetteOnly: false);

            BuildApronSafetyProps();
            BuildFuelFarm();
            BuildParkedGaAircraft();
        }

        private static void PlaceBeltLoader(string kit, Vector3 position, float yaw, bool silhouetteOnly = false)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var yellow = new Color(0.85f, 0.7f, 0.2f);
            var dark = new Color(0.2f, 0.22f, 0.24f);
            var placed = false;
            void Place(string mesh, Color color)
            {
                if (!ArtGltfLoader.TryPlaceNamedMesh(kit, mesh, position, rot, color, out _))
                    return;
                placed = true;
            }

            Place("belt_loader_chassis", yellow);
            Place("belt_loader_cab", Shade(yellow, 0.85f));
            Place("belt_loader_boom", new Color(0.55f, 0.56f, 0.58f));
            Place("belt_loader_belt", new Color(0.25f, 0.25f, 0.26f));
            Place("belt_loader_wheel_fl", dark);
            Place("belt_loader_wheel_fr", dark);
            Place("belt_loader_wheel_rl", dark);
            Place("belt_loader_wheel_rr", dark);
            if (!silhouetteOnly)
            {
                Place("belt_loader_cab_glass", new Color(0.35f, 0.55f, 0.65f));
                Place("belt_loader_stripe", new Color(0.15f, 0.16f, 0.18f));
                Place("belt_loader_rail_l", dark);
                Place("belt_loader_rail_r", dark);
                Place("belt_loader_hinge", dark);
                Place("belt_loader_support", dark);
                Place("belt_loader_roller_1", new Color(0.35f, 0.36f, 0.38f));
                Place("belt_loader_roller_2", new Color(0.35f, 0.36f, 0.38f));
                Place("belt_loader_roller_3", new Color(0.35f, 0.36f, 0.38f));
                Place("belt_loader_roller_4", new Color(0.35f, 0.36f, 0.38f));
                Place("belt_loader_bumper", Shade(yellow, 0.7f));
                Place("belt_loader_hub_fl", new Color(0.28f, 0.3f, 0.32f));
                Place("belt_loader_hub_fr", new Color(0.28f, 0.3f, 0.32f));
                Place("belt_loader_hitch", dark);
                Place("belt_loader_light", new Color(0.95f, 0.9f, 0.6f));
            }

            if (!placed)
            {
                CreateBlock("Belt loader body", position + new Vector3(0f, 0.4f, 0f), new Vector3(1.6f, 0.5f, 0.8f), yellow);
                CreateBlock("Belt loader boom", position + new Vector3(0.8f, 0.85f, 0f), new Vector3(2.2f, 0.2f, 0.35f),
                    new Color(0.55f, 0.56f, 0.58f));
            }
        }

        /// <summary>
        /// Decision 0025 items 1+3 — fire hydrants, extinguisher cabinets and FOD bins
        /// so the apron edge reads as a working safety-equipped airfield.
        /// </summary>
        private static void BuildApronSafetyProps()
        {
            var hasHydrant = ArtPresentationLoader.HasPrefab("mdl_fire_hydrant_v01");
            var hasCabinet = ArtPresentationLoader.HasPrefab("mdl_extinguisher_cabinet_v01");
            var serviceKit = PreferArtKit(
                "Models/Props/mdl_service_equipment_kit_v03.gltf",
                "Models/Props/mdl_service_equipment_kit_authored_v01.gltf",
                "Models/Props/mdl_service_equipment_kit_v02.gltf",
                "Models/Props/mdl_service_equipment_kit_v01.gltf");
            var hasBinKit = !string.IsNullOrEmpty(serviceKit) && ArtGltfLoader.HasKit(serviceKit);

            PlaceFireHydrant("Hydrant apron NE", new Vector3(34f, 0f, 23.5f), 0f);
            PlaceFireHydrant("Hydrant apron NW", new Vector3(8.5f, 0f, 23.5f), 0f);
            if (!hasHydrant)
            {
                PlaceFireHydrant("Hydrant taxi", new Vector3(-2f, 0f, 11.5f), 90f);
                PlaceFireHydrant("Hydrant hangar", new Vector3(-14f, 0f, 16f), 0f);
            }

            PlaceExtinguisherCabinet("Extinguisher terminal", new Vector3(20f, 0f, 24.2f), 180f);
            PlaceExtinguisherCabinet("Extinguisher hangar", new Vector3(-15.5f, 0f, 24.2f), 180f);
            if (!hasCabinet)
                PlaceExtinguisherCabinet("Extinguisher ops", new Vector3(-5f, 0f, 24.2f), 180f);

            PlaceFodBin("FOD bin A", new Vector3(36f, 0f, 20f), 270f);
            PlaceFodBin("FOD bin B", new Vector3(10f, 0f, 11.2f), 0f);
            if (!hasBinKit)
                PlaceFodBin("FOD bin C", new Vector3(-24f, 0f, 16.5f), 90f);

            // Stand lead-in / box paint — skip when markings kit already placed stand stops
            // (avoid double-painted bays next to authored threshold/TDZ).
            if (GameObject.Find("stand_stop_a") == null
                && GameObject.Find("stand_stop_b") == null
                && GameObject.Find("stand_stop_c") == null)
            {
                foreach (var z in new[] { 14f, 20f, 26f })
                {
                    CreateBlock($"Stand box front {z}", new Vector3(20f, 0.04f, z - 2.6f), new Vector3(10f, 0.02f, 0.12f), Color.white);
                    CreateBlock($"Stand box back {z}", new Vector3(20f, 0.04f, z + 2.6f), new Vector3(10f, 0.02f, 0.12f), Color.white);
                    CreateBlock($"Stand box L {z}", new Vector3(14.8f, 0.04f, z), new Vector3(0.12f, 0.02f, 5.2f), Color.white);
                    CreateBlock($"Stand box R {z}", new Vector3(25.2f, 0.04f, z), new Vector3(0.12f, 0.02f, 5.2f), Color.white);
                }
            }
        }

        private static void PlaceFireHydrant(string name, Vector3 position, float yawDegrees)
        {
            Transform root;
            if (ArtPresentationLoader.TryInstantiatePrefab("mdl_fire_hydrant_v01", out var prefabRoot))
            {
                prefabRoot.name = name;
                root = prefabRoot;
            }
            else
            {
                root = new GameObject(name).transform;
                ParentBlock(root, $"{name} barrel", new Vector3(0f, 0.55f, 0f), new Vector3(0.4f, 0.7f, 0.4f), new Color(0.78f, 0.18f, 0.14f));
                ParentBlock(root, $"{name} stripe", new Vector3(0f, 0.55f, 0f), new Vector3(0.42f, 0.12f, 0.42f), AirsideTheme.SafetyYellow);
            }

            root.position = position;
            root.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
        }

        private static void PlaceExtinguisherCabinet(string name, Vector3 position, float yawDegrees)
        {
            Transform root;
            if (ArtPresentationLoader.TryInstantiatePrefab("mdl_extinguisher_cabinet_v01", out var prefabRoot))
            {
                prefabRoot.name = name;
                root = prefabRoot;
            }
            else
            {
                root = new GameObject(name).transform;
                ParentBlock(root, $"{name} body", new Vector3(0f, 0.7f, 0f), new Vector3(0.55f, 1.2f, 0.35f), new Color(0.82f, 0.2f, 0.16f));
                ParentBlock(root, $"{name} stripe", new Vector3(0f, 1.15f, 0.2f), new Vector3(0.5f, 0.1f, 0.05f), AirsideTheme.SafetyYellow);
            }

            root.position = position;
            root.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
        }

        private static void PlaceFodBin(string name, Vector3 position, float yawDegrees)
        {
            var kit = PreferArtKit(
                "Models/Props/mdl_service_equipment_kit_v03.gltf",
                "Models/Props/mdl_service_equipment_kit_authored_v01.gltf",
                "Models/Props/mdl_service_equipment_kit_v02.gltf",
                "Models/Props/mdl_service_equipment_kit_v01.gltf");
            var rot = Quaternion.Euler(0f, yawDegrees, 0f);
            var yellow = new Color(0.95f, 0.75f, 0.15f);
            var dark = new Color(0.2f, 0.22f, 0.25f);
            if (ArtGltfLoader.TryPlaceNamedMesh(kit, "bin", position, rot, yellow, out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "bin_lid", position, rot, dark, out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "bin_handle", position, rot, Shade(dark, 1.15f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "bin_stripe", position, rot, new Color(0.15f, 0.16f, 0.18f), out _))
                return;

            Transform root;
            if (ArtPresentationLoader.TryInstantiatePrefab("mdl_fod_bin_v01", out var prefabRoot))
            {
                prefabRoot.name = name;
                root = prefabRoot;
            }
            else
            {
                root = new GameObject(name).transform;
                ParentBlock(root, $"{name} body", new Vector3(0f, 0.45f, 0f), new Vector3(0.7f, 0.75f, 0.55f), yellow);
                ParentBlock(root, $"{name} lid", new Vector3(0f, 0.88f, 0f), new Vector3(0.75f, 0.1f, 0.6f), dark);
            }

            root.position = position;
            root.rotation = rot;
        }

        private static void PlaceSignBoard(string kit, Vector3 position, float yawDegrees)
        {
            var rot = Quaternion.Euler(0f, yawDegrees, 0f);
            if (ArtGltfLoader.TryPlaceNamedMesh(kit, "sign_post", position, rot, new Color(0.35f, 0.36f, 0.38f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "sign_face", position, rot, new Color(0.95f, 0.95f, 0.92f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "sign_cap", position, rot, new Color(0.12f, 0.35f, 0.55f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "sign_brace", position, rot, new Color(0.4f, 0.42f, 0.44f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "sign_reflector", position, rot, new Color(0.85f, 0.88f, 0.9f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "sign_base", position, rot, new Color(0.3f, 0.32f, 0.34f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "sign_glyph_bar", position, rot, new Color(0.12f, 0.35f, 0.55f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "sign_glyph_bar_b", position, rot, new Color(0.12f, 0.35f, 0.55f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "sign_glyph_dot", position, rot, new Color(0.12f, 0.35f, 0.55f), out _))
                return;

            if (ArtGltfLoader.TryPlaceNamedMesh(kit, "sign_board", position, rot, new Color(0.12f, 0.35f, 0.55f), out _))
                return;

            if (ArtPresentationLoader.TryInstantiatePrefab("mdl_airside_sign_v01", out var prefabRoot))
            {
                prefabRoot.name = "Airside sign";
                prefabRoot.position = position;
                prefabRoot.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
                return;
            }

            var root = new GameObject("Airside sign").transform;
            root.position = position;
            root.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
            ParentBlock(root, "Airside sign post", new Vector3(0f, 1.1f, 0f), new Vector3(0.12f, 2.0f, 0.12f), new Color(0.35f, 0.36f, 0.38f));
            ParentBlock(root, "Airside sign face", new Vector3(0.08f, 1.35f, 0f), new Vector3(0.04f, 0.9f, 1.1f), new Color(0.95f, 0.95f, 0.92f));
            ParentBlock(root, "Airside sign back", new Vector3(0f, 1.35f, 0f), new Vector3(0.12f, 1.0f, 1.2f), new Color(0.12f, 0.35f, 0.55f));
        }

        private static void PlaceBaggageDolly(string kit, Vector3 position)
        {
            if (ArtGltfLoader.TryPlaceNamedMesh(kit, "dolly_bed", position, Quaternion.identity, new Color(0.55f, 0.35f, 0.18f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "dolly_rail_l", position, Quaternion.identity, new Color(0.45f, 0.3f, 0.16f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "dolly_rail_r", position, Quaternion.identity, new Color(0.45f, 0.3f, 0.16f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "dolly_rail_mid", position, Quaternion.identity, new Color(0.45f, 0.3f, 0.16f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "dolly_rail_end", position, Quaternion.identity, new Color(0.45f, 0.3f, 0.16f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "dolly_post_l", position, Quaternion.identity, new Color(0.45f, 0.3f, 0.16f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "dolly_post_r", position, Quaternion.identity, new Color(0.45f, 0.3f, 0.16f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "dolly_handle", position, Quaternion.identity, new Color(0.4f, 0.4f, 0.42f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "dolly_hitch", position, Quaternion.identity, new Color(0.35f, 0.35f, 0.38f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "dolly_hitch_pin", position, Quaternion.identity, new Color(0.3f, 0.3f, 0.32f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "dolly_cargo", position, Quaternion.identity, new Color(0.7f, 0.55f, 0.25f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "dolly_bag_a", position, Quaternion.identity, new Color(0.75f, 0.55f, 0.2f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "dolly_bag_b", position, Quaternion.identity, new Color(0.65f, 0.45f, 0.18f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "dolly_bag_c", position, Quaternion.identity, new Color(0.8f, 0.6f, 0.25f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "dolly_wheel_fl", position, Quaternion.identity, new Color(0.15f, 0.15f, 0.16f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "dolly_wheel_fr", position, Quaternion.identity, new Color(0.15f, 0.15f, 0.16f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "dolly_wheel_rl", position, Quaternion.identity, new Color(0.15f, 0.15f, 0.16f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "dolly_wheel_rr", position, Quaternion.identity, new Color(0.15f, 0.15f, 0.16f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "dolly_hub_fl", position, Quaternion.identity, new Color(0.45f, 0.45f, 0.48f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "dolly_hub_fr", position, Quaternion.identity, new Color(0.45f, 0.45f, 0.48f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "dolly_hub_rl", position, Quaternion.identity, new Color(0.45f, 0.45f, 0.48f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "dolly_hub_rr", position, Quaternion.identity, new Color(0.45f, 0.45f, 0.48f), out _))
                return;

            if (ArtGltfLoader.TryPlaceNamedMesh(kit, "baggage_dolly", position, Quaternion.identity,
                    new Color(0.55f, 0.35f, 0.18f), out _))
                return;

            if (ArtPresentationLoader.TryInstantiatePrefab("mdl_baggage_dolly_v01", out var prefabRoot))
            {
                prefabRoot.name = "Baggage dolly";
                prefabRoot.position = position;
                return;
            }

            CreateBlock("Dolly", position + new Vector3(0f, 0.35f, 0f), new Vector3(1.6f, 0.7f, 0.9f), new Color(0.55f, 0.35f, 0.18f));
        }

        /// <summary>
        /// Small fuel farm west of the hangar — readable silhouette, not a sim system.
        /// </summary>
        private static void BuildFuelFarm()
        {
            if (ArtPresentationLoader.TryInstantiatePrefab("mdl_fuel_farm_v01", out var farm))
            {
                farm.name = "Fuel farm";
                farm.position = new Vector3(-34f, 0f, 22f);
            }
            else
            {
                CreateBlock("Fuel pad", new Vector3(-34f, 0.02f, 22f), new Vector3(8f, 0.08f, 6f), new Color(0.28f, 0.3f, 0.32f),
                    "Textures/Surfaces/tx_concrete_apron_basecolor_v01.png", new Vector2(1.2f, 1f));
                // Cylindrical tanks read as storage vessels, not cargo cubes (0025 item 2/3).
                PlaceFuelTank("Fuel tank A", new Vector3(-35.5f, 1.15f, 22.5f), new Color(0.72f, 0.55f, 0.18f));
                PlaceFuelTank("Fuel tank B", new Vector3(-32.2f, 1.15f, 22.5f), new Color(0.72f, 0.55f, 0.18f));
                CreateBlock("Fuel bund", new Vector3(-34f, 0.25f, 22f), new Vector3(7.2f, 0.35f, 5.2f), new Color(0.4f, 0.42f, 0.4f));
                CreateBlock("Fuel pump", new Vector3(-34f, 0.7f, 19.6f), new Vector3(1.2f, 1.2f, 0.8f), new Color(0.25f, 0.28f, 0.3f));
                CreateBlock("Fuel hose reel", new Vector3(-33.1f, 0.45f, 19.8f), new Vector3(0.55f, 0.55f, 0.55f), new Color(0.35f, 0.2f, 0.12f));
                // Cones/barrier only when the farm is greybox — kit ships its own safety fringe.
                CreateCone(new Vector3(-30.5f, 0.25f, 19.2f));
                CreateCone(new Vector3(-37.5f, 0.25f, 19.2f));
                CreateBarrier(new Vector3(-34f, 0.45f, 18.6f), 0f);
            }

            // Amber safety flood over the fuel pad at night (presentation only).
            var lamp = new GameObject("Fuel farm light");
            lamp.transform.position = new Vector3(-34f, 4.2f, 22f);
            var light = lamp.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.72f, 0.28f);
            light.range = 16f;
            light.intensity = 0f;
            light.shadows = LightShadows.None;
        }

        private static void PlaceFuelTank(string name, Vector3 position, Color color)
        {
            var tank = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tank.name = name;
            Object.Destroy(tank.GetComponent<Collider>());
            tank.transform.position = position;
            tank.transform.localScale = new Vector3(2.0f, 1.15f, 2.0f);
            tank.GetComponent<Renderer>().material = AirsideMaterialLibrary.Create(
                color, AirsideMaterialLibrary.SurfaceKind.PaintedMetal);
            // Cap + ladder stub for silhouette.
            CreateBlock($"{name} cap", position + new Vector3(0f, 1.25f, 0f), new Vector3(0.9f, 0.18f, 0.9f), Shade(color, 0.85f));
            CreateBlock($"{name} ladder", position + new Vector3(1.05f, 0.2f, 0f), new Vector3(0.12f, 1.8f, 0.35f),
                new Color(0.45f, 0.46f, 0.48f));
        }

        /// <summary>
        /// Static GA aircraft west of the hangar so the GA apron reads occupied.
        /// Presentation-only; not in the simulation fleet.
        /// </summary>
        private static void BuildParkedGaAircraft()
        {
            var spots = new[]
            {
                (x: -30f, z: 14f, yaw: 90f),
                (x: -37f, z: 14f, yaw: 98f),
                (x: -33.5f, z: 10.5f, yaw: 105f),
                (x: -40.5f, z: 11.5f, yaw: 85f),
                (x: -27f, z: 11f, yaw: 110f)
            };
            // Prefab GA reads heavier than greybox — three airframes keep the bay calm.
            var hasGaPrefab = ArtPresentationLoader.HasPrefab("mdl_parked_ga_v01");
            var count = hasGaPrefab ? 3 : spots.Length;
            for (var i = 0; i < count; i++)
            {
                var spot = spots[i];
                Transform root;
                if (ArtPresentationLoader.TryInstantiatePrefab("mdl_parked_ga_v01", out var prefabRoot))
                {
                    prefabRoot.name = $"Parked GA {i}";
                    root = prefabRoot;
                }
                else
                {
                    root = new GameObject($"Parked GA {i}").transform;
                    ParentBlock(root, "GA fuselage", Vector3.zero, new Vector3(0.55f, 0.55f, 2.4f), new Color(0.9f, 0.91f, 0.93f));
                    ParentBlock(root, "GA nose", new Vector3(0f, 0f, 1.15f), new Vector3(0.42f, 0.42f, 0.55f), new Color(0.9f, 0.91f, 0.93f));
                    ParentBlock(root, "GA wing", new Vector3(0f, 0.08f, 0.15f), new Vector3(3.2f, 0.08f, 0.7f), new Color(0.85f, 0.55f, 0.2f));
                    ParentBlock(root, "GA wing strut L", new Vector3(-0.9f, -0.12f, 0.15f), new Vector3(0.06f, 0.42f, 0.06f), new Color(0.4f, 0.4f, 0.42f));
                    ParentBlock(root, "GA wing strut R", new Vector3(0.9f, -0.12f, 0.15f), new Vector3(0.06f, 0.42f, 0.06f), new Color(0.4f, 0.4f, 0.42f));
                    ParentBlock(root, "GA tail", new Vector3(0f, 0.55f, -1.0f), new Vector3(0.1f, 0.9f, 0.55f), new Color(0.85f, 0.55f, 0.2f));
                    ParentBlock(root, "GA tailplane", new Vector3(0f, 0.4f, -1.05f), new Vector3(1.4f, 0.06f, 0.4f), new Color(0.85f, 0.55f, 0.2f));
                    ParentBlock(root, "GA canopy glass", new Vector3(0f, 0.32f, 0.45f), new Vector3(0.42f, 0.22f, 0.7f), new Color(0.2f, 0.35f, 0.45f, 0.42f));
                    ParentBlock(root, "GA spinner", new Vector3(0f, 0f, 1.55f), new Vector3(0.22f, 0.22f, 0.28f), new Color(0.25f, 0.25f, 0.28f));
                    ParentBlock(root, "GA prop blade A", new Vector3(0f, 0f, 1.48f), new Vector3(0.06f, 0.95f, 0.1f), new Color(0.2f, 0.2f, 0.22f));
                    ParentBlock(root, "GA gear nose", new Vector3(0f, -0.35f, 0.85f), new Vector3(0.08f, 0.35f, 0.08f), new Color(0.3f, 0.3f, 0.32f));
                    ParentBlock(root, "GA gear L", new Vector3(-0.5f, -0.35f, -0.15f), new Vector3(0.08f, 0.35f, 0.08f), new Color(0.3f, 0.3f, 0.32f));
                    ParentBlock(root, "GA gear R", new Vector3(0.5f, -0.35f, -0.15f), new Vector3(0.08f, 0.35f, 0.08f), new Color(0.3f, 0.3f, 0.32f));
                    ParentBlock(root, "GA stripe", new Vector3(0f, 0.05f, 0.1f), new Vector3(0.58f, 0.08f, 1.6f), new Color(0.85f, 0.55f, 0.2f));
                }

                root.position = new Vector3(spot.x, 0.55f, spot.z);
                root.rotation = Quaternion.Euler(0f, spot.yaw, 0f);
                CreateBlock($"Tie rope {i}a", new Vector3(spot.x - 1.4f, 0.08f, spot.z), new Vector3(0.2f, 0.06f, 0.2f), new Color(0.55f, 0.55f, 0.5f));
                CreateBlock($"Tie rope {i}b", new Vector3(spot.x + 1.4f, 0.08f, spot.z), new Vector3(0.2f, 0.06f, 0.2f), new Color(0.55f, 0.55f, 0.5f));
                PlaceContactShadow($"GA contact {i}", new Vector3(spot.x, 0.04f, spot.z), new Vector3(3.4f, 0.02f, 2.6f), 0.14f);
            }
        }


        private static AudioClip CreateEngineClip()
        {
            const int sampleRate = 22050;
            var samples = new float[sampleRate];
            for (var i = 0; i < samples.Length; i++)
            {
                var time = i / (float)sampleRate;
                samples[i] = (Mathf.Sin(time * 2f * Mathf.PI * 82f) * 0.12f) +
                             (Mathf.Sin(time * 2f * Mathf.PI * 164f) * 0.035f);
            }

            var clip = AudioClip.Create("Prototype engine", samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateWindClip()
        {
            // Soft filtered noise bed for regional airfield air (presentation only).
            const int sampleRate = 22050;
            var samples = new float[sampleRate * 2];
            var state = 0f;
            for (var i = 0; i < samples.Length; i++)
            {
                var white = (UnityEngine.Random.value * 2f - 1f);
                state = state * 0.92f + white * 0.08f;
                var gust = Mathf.Sin(i / (float)sampleRate * 2f * Mathf.PI * 0.35f) * 0.15f;
                samples[i] = (state * 0.55f + white * 0.08f + gust * state) * 0.35f;
            }

            var clip = AudioClip.Create("Ambient wind", samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateRainClip()
        {
            const int sampleRate = 22050;
            var samples = new float[sampleRate];
            for (var i = 0; i < samples.Length; i++)
            {
                var crackle = UnityEngine.Random.value * 2f - 1f;
                var hush = Mathf.Sin(i * 0.015f) * 0.1f;
                samples[i] = crackle * 0.22f + hush * crackle;
            }

            var clip = AudioClip.Create("Ambient rain", samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>Soft coastal wave bed for Kangaroo Island ambience (presentation only).</summary>
        private static AudioClip CreateCoastClip()
        {
            const int sampleRate = 22050;
            var samples = new float[sampleRate * 3];
            var state = 0f;
            for (var i = 0; i < samples.Length; i++)
            {
                var t = i / (float)sampleRate;
                var white = UnityEngine.Random.value * 2f - 1f;
                state = state * 0.96f + white * 0.04f;
                var swell = Mathf.Sin(t * 2f * Mathf.PI * 0.22f) * 0.5f + 0.5f;
                var wash = Mathf.Sin(t * 2f * Mathf.PI * 0.55f + 1.3f) * 0.35f + 0.65f;
                samples[i] = state * 0.4f * swell * wash;
            }

            var clip = AudioClip.Create("Ambient coast", samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateTouchdownClip()
        {
            const int sampleRate = 22050;
            var samples = new float[sampleRate / 4];
            for (var i = 0; i < samples.Length; i++)
            {
                var time = i / (float)sampleRate;
                var envelope = Mathf.Exp(-time * 18f);
                var chirp = Mathf.Sin(time * 2f * Mathf.PI * (420f + time * 900f));
                var rumble = Mathf.Sin(time * 2f * Mathf.PI * 90f) * 0.35f;
                samples[i] = (chirp * 0.22f + rumble) * envelope;
            }

            var clip = AudioClip.Create("Touchdown chirp", samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private Vector3 PositionFor(AircraftPhase phase, float progress, float standZ, TaxiRoute taxiRoute)
        {
            // Air phases share the runway axis and meet the taxi network at (-24, 0)
            // so takeoff no longer teleports 52 m after taxi-out, and landing rolls out
            // to the same A1 entry TaxiIn uses.
            var t = Mathf.Clamp01(progress);
            return phase switch
            {
                AircraftPhase.Approach => Smooth(
                    new Vector3(-72f, 7.5f, 0f), new Vector3(-50f, 1.55f, 0f), t),
                AircraftPhase.Landing => LandingPosition(t),
                AircraftPhase.TaxiIn => PositionAlongTaxiRoute(taxiRoute, t, false),
                AircraftPhase.AtStand => new Vector3(17f, 0.7f, standZ),
                AircraftPhase.Pushback => Smooth(new Vector3(17f, 0.7f, standZ), new Vector3(12f, 0.7f, standZ - 2f), t),
                AircraftPhase.TaxiOut => TaxiOutPosition(taxiRoute, t, standZ),
                AircraftPhase.Takeoff => TakeoffPosition(t),
                _ => new Vector3(55f, 14f, 0f)
            };
        }

        /// <summary>
        /// Leave the pushback pad toward the lead-in — do not drive back onto the stand.
        /// </summary>
        private Vector3 TaxiOutPosition(TaxiRoute route, float t, float standZ)
        {
            var pushEnd = new Vector3(12f, 0.7f, standZ - 2f);
            // Reverse route: 0 = stand, 1 = runway exit. Join mid lead-in (~0.30).
            const float joinT = 0.30f;
            if (t < 0.18f)
            {
                var join = PositionAlongTaxiRoute(route, joinT, true);
                return Smooth(pushEnd, join, t / 0.18f);
            }

            var routeT = joinT + ((t - 0.18f) / 0.82f) * (1f - joinT);
            return PositionAlongTaxiRoute(route, routeT, true);
        }

        /// <summary>
        /// Flare then a real ground rollout (~22 m) to the west taxi exit (-24).
        /// </summary>
        private static Vector3 LandingPosition(float t)
        {
            const float touchdownT = 0.28f;
            if (t < touchdownT)
            {
                return Smooth(new Vector3(-50f, 1.55f, 0f), new Vector3(-46f, 0.7f, 0f), t / touchdownT);
            }

            var u = (t - touchdownT) / (1f - touchdownT);
            var eased = 1f - (1f - u) * (1f - u);
            return Vector3.Lerp(new Vector3(-46f, 0.7f, 0f), new Vector3(-24f, 0.7f, 0f), eased);
        }

        /// <summary>
        /// Line up from the A1 entry heading, ground-roll, then climb — continuous with taxi-out.
        /// </summary>
        private static Vector3 TakeoffPosition(float t)
        {
            // First ~14%: bezier lineup so LookRotation does not snap ~140° onto +X.
            if (t < 0.14f)
            {
                var u = Mathf.SmoothStep(0f, 1f, t / 0.14f);
                var start = new Vector3(-24f, 0.7f, 0f);
                var bend = new Vector3(-23.2f, 0.7f, -0.85f);
                var aligned = new Vector3(-20.5f, 0.7f, 0f);
                var omu = 1f - u;
                return omu * omu * start + 2f * omu * u * bend + u * u * aligned;
            }

            if (t < 0.48f)
            {
                var u = (t - 0.14f) / 0.34f;
                var eased = u * u;
                return Vector3.Lerp(new Vector3(-20.5f, 0.7f, 0f), new Vector3(10f, 0.7f, 0f), eased);
            }

            var climb = (t - 0.48f) / 0.52f;
            return Smooth(new Vector3(10f, 0.7f, 0f), new Vector3(52f, 12f, 0f), climb);
        }

        private Vector3 PositionAlongTaxiRoute(TaxiRoute route, float progress, bool reverse)
        {
            return TaxiVisualPath.PositionAt(route, progress, reverse);
        }

        private static Vector3 Smooth(Vector3 from, Vector3 to, float progress) => Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, progress));

        

        private static GameObject CreateBlock(
            string name,
            Vector3 position,
            Vector3 scale,
            Color color,
            string artTextureRelativePath = null,
            Vector2? textureTiling = null)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.position = position;
            block.transform.localScale = scale;
            block.GetComponent<Renderer>().material = CreateMaterial(color, artTextureRelativePath, textureTiling);
            return block;
        }

        private static void CreateDecalQuad(string name, Vector3 position, Vector3 scale, string artTextureRelativePath)
        {
            var texture = TryLoadArtTexture(artTextureRelativePath);
            if (texture == null)
                return;

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            UnityEngine.Object.Destroy(quad.GetComponent<Collider>());
            quad.transform.position = position;
            quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = scale;
            // True URP transparent — wear PNGs are mostly alpha; opaque Lit ignored that and
            // stamped dark RGB as black ground patches.
            var tint = new Color(1f, 1f, 1f, 0.42f);
            var material = AirsideMaterialLibrary.Create(
                tint, AirsideMaterialLibrary.SurfaceKind.Default, texture, Vector2.one);
            var renderer = quad.GetComponent<Renderer>();
            renderer.material = material;
            SetRendererColor(renderer, tint);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static Material CreateMaterial(Color color, string artTextureRelativePath = null, Vector2? textureTiling = null)
        {
            var kind = AirsideMaterialLibrary.InferFromTexturePath(artTextureRelativePath);
            if (kind == AirsideMaterialLibrary.SurfaceKind.Default)
                kind = InferSurfaceKindFromColor(color);
            var albedo = TryLoadArtTexture(artTextureRelativePath);
            return AirsideMaterialLibrary.Create(color, kind, albedo, textureTiling);
        }

        private static AirsideMaterialLibrary.SurfaceKind InferSurfaceKindFromColor(Color color)
        {
            // Heuristic for untextured primitives (cars, props, glow quads, painted lines).
            // Translucent rain/smoke/mist must NOT become Glass — MAT-001 mat_glass is a pane
            // material and reads as bright vertical shafts on thin Cube droplets.
            if (color.a < 0.99f)
            {
                var isVfxMist = color.a < 0.55f && color.r > 0.55f && color.g > 0.55f && color.b > 0.55f;
                if (isVfxMist)
                    return AirsideMaterialLibrary.SurfaceKind.Default;
                return AirsideMaterialLibrary.SurfaceKind.Glass;
            }
            // Near-white / cream → painted markings, not aircraft skin (MAT-001 / 0025 item 4).
            if (color.r > 0.85f && color.g > 0.85f && color.b > 0.85f)
                return AirsideMaterialLibrary.SurfaceKind.PaintedLine;
            // Safety-yellow / taxi paint.
            if (color.r > 0.85f && color.g > 0.75f && color.b < 0.45f)
                return AirsideMaterialLibrary.SurfaceKind.PaintedLine;
            if (color.b > color.r + 0.15f && color.b > color.g + 0.05f)
                return AirsideMaterialLibrary.SurfaceKind.Water;
            return AirsideMaterialLibrary.SurfaceKind.PaintedMetal;
        }

        /// <summary>
        /// Loads Batch B PNGs via <see cref="ArtRuntimePaths"/> (StreamingAssets in
        /// packaged builds; Editor Assets fallback). Returns null when missing so
        /// solid-colour primitives remain the fallback.
        /// </summary>
        private static Texture2D TryLoadArtTexture(string artRelativePath)
        {
            if (string.IsNullOrEmpty(artRelativePath))
                return null;

            var fullPath = ArtRuntimePaths.ResolveExisting(artRelativePath);
            if (fullPath == null)
                return null;

            var bytes = File.ReadAllBytes(fullPath);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: true);
            if (!texture.LoadImage(bytes))
                return null;

            texture.name = Path.GetFileNameWithoutExtension(artRelativePath);
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;
            return texture;
        }



        private GUIStyle ReputationBandStyle(GUIStyle small, GUIStyle onTime, GUIStyle caution, GUIStyle delayed)
        {
            // Presentation only — band names come from AirportReputation.Band.
            return _simulation.Reputation.Band switch
            {
                "Trusted" => onTime,
                "Established" => small,
                "Provisional" => caution,
                _ => delayed
            };
        }

        private string CommercialFlightHudLine()
        {
            if (_simulation.Flights.Count == 0)
                return "No commercial flights";

            var parts = new string[_simulation.Flights.Count];
            for (var index = 0; index < _simulation.Flights.Count; index++)
            {
                var flight = _simulation.Flights[index];
                parts[index] =
                    $"{flight.AircraftId} @ {flight.AssignedStand.Value}";
            }

            return _simulation.Flights.Count == 1
                ? $"Flight {parts[0]}"
                : $"Flights {string.Join(" · ", parts)}";
        }

        private string CommercialPhaseHudLine()
        {
            if (_simulation.Flights.Count == 0)
                return "No active phase";

            if (_simulation.Flights.Count == 1)
            {
                var op = _simulation.ActiveAircraft;
                return $"{FormatPhase(op.Phase)}  ·  {op.SecondsRemaining(_clock.Now)}s";
            }

            var parts = new string[_simulation.Flights.Count];
            for (var index = 0; index < _simulation.Flights.Count; index++)
            {
                var flight = _simulation.Flights[index];
                var op = flight.Operation;
                parts[index] =
                    $"{flight.AircraftId}: {FormatPhase(op.Phase)} {op.SecondsRemaining(_clock.Now)}s";
            }

            return string.Join("  ·  ", parts);
        }

        private static string FormatPhase(AircraftPhase phase) => phase switch
        {
            AircraftPhase.Approach => "On approach",
            AircraftPhase.Landing => "Landing",
            AircraftPhase.TaxiIn => "Taxiing to stand",
            AircraftPhase.AtStand => "Turnaround at stand",
            AircraftPhase.Pushback => "Pushback",
            AircraftPhase.TaxiOut => "Taxiing to runway",
            AircraftPhase.Takeoff => "Taking off",
            AircraftPhase.Departed => "Departed",
            _ => phase.ToString()
        };

    }
}
