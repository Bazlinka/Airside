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
        // Each commercial keeps a stable livery slot (0 = Coastline Regional blue,
        // 1 = Emu Air teal) for its lifetime. Slots are keyed by AircraftId so a
        // respawn-driven re-sort of the flight list can no longer repaint a plane
        // or leave two aircraft in the same livery.
        private readonly Dictionary<string, int> _commercialLiverySlot = new();
        // Damped roll angle per airframe so banking eases in and out of a turn.
        private readonly Dictionary<string, float> _bankDegrees = new();
        private readonly Dictionary<int, float> _propRpm = new();

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
        private Renderer[] _touchdownSmokeRenderers;
        private Light _fuelFarmLight;
        private Light _arffBayLight;
        private Renderer _arffLightbarRenderer;
        private Transform _skidMarkRoot;
        private Transform _taxiSprayRoot;
        private Light[] _windowLights;
        private Transform _horizonDome;
        private Renderer _horizonDomeRenderer;
        private Transform _sunDisc;
        private Renderer _sunDiscRenderer;
        private Transform _moonDisc;
        private Renderer _moonDiscRenderer;
        private Renderer _starFieldRenderer;
        private Renderer _coastFoamRenderer;
        private Renderer[] _taxiSprayRenderers;
        private Renderer[] _puddleRenderers;
        private readonly Dictionary<int, AudioSource> _engineAudio = new Dictionary<int, AudioSource>();
        private Transform _cloudRoot;
        private Transform _cloudUmbraRoot;
        private int _cloudTintKey = int.MinValue;
        private Transform _birdFlockRoot;
        private Transform[] _birdWingL;
        private Transform[] _birdWingR;
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
        private AudioSource _uiAudio;
        private AudioClip _uiClickClip;
        private readonly Dictionary<string, AircraftPhase> _previousPhases = new Dictionary<string, AircraftPhase>();
        private readonly HashSet<string> _touchdownFired = new HashSet<string>();
        private readonly HashSet<string> _rotateFired = new HashSet<string>();
        private AudioClip _rotateClip;
        private readonly List<(Material Material, Color DryColor, float DrySmoothness, float DryMetallic, float DryBumpScale, bool Paved)> _wetSurfaces =
            new List<(Material, Color, float, float, float, bool)>();
        private static readonly MaterialPropertyBlock RendererTintBlock = new();
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
        private static readonly int MainTexStId = Shader.PropertyToID("_MainTex_ST");
        private static readonly Dictionary<string, string> SurfaceBasecolorCache = new();
        private static Transform _airfieldRoot;
        private static Mesh FallbackShrubMesh;
        private static Material FallbackShrubMaterial;
        private static Mesh FallbackTreeBarkMesh;
        private static Mesh FallbackTreeCanopyMesh;
        private static Material FallbackTreeBarkMaterial;
        private static Material FallbackTreeCanopyMaterial;
        private static Mesh BuiltinSphereMesh;
        private static Mesh BuiltinCylinderMesh;
        private static Mesh BuiltinCubeMesh;

        // True when the authored Adelaide ground mesh actually built, so world
        // heights come from the landform rather than the flat fallback slab. The
        // perimeter fence seats itself differently in each case.
        private static bool _bareGroundFollowsLandform;

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
        private readonly List<Renderer> _coastFoamRenderers = new List<Renderer>();
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
        private float _presentationClock;
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
        // Temporary — keep the field in daylight while night lighting is reworked.
        private const bool PinDaylightPresentation = true;

        private float PresentationDaylight =>
            PinDaylightPresentation ? 1f : (float)_simulation.TimeOfDay.Daylight;

        private float _apronProbeRefreshAt;
            private int _probeBand = int.MinValue;
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
            AirsideRuntimeQuality.Apply(_mainCamera);
            _dayVolume = AirsideDayVolume.Ensure(transform);
            BuildAirfield();
            if (AirsideFocusMode.ShowDecorativeLights)
            {
                _apronLights = BuildApronLights();
                _landsideLights = BuildLandsideStreetlights();
                _thresholdLights = BuildThresholdApproachLights();
                _alsLights = CollectAlsLights();
                _runwayEdgeLights = BuildRunwayEdgePointLights();
                _apronProbe = BuildApronReflectionProbe();
                _terminalProbe = BuildTerminalReflectionProbe();
                _aerodromeBeacon = BuildAerodromeBeacon();
            }
            else
            {
                _apronLights = Array.Empty<Light>();
                _landsideLights = Array.Empty<Light>();
                _thresholdLights = Array.Empty<Light>();
                _alsLights = Array.Empty<Light>();
                _runwayEdgeLights = Array.Empty<Light>();
            }
            _rainRoot = AirsideFocusMode.ShowEnvironment ? BuildRainRoot() : null;
            // Touchdown smoke is circuit presentation, independent of disabled world props.
            _touchdownSmoke = BuildTouchdownSmoke();
            _skidMarkRoot = null;
            _taxiSprayRoot = AirsideFocusMode.ShowEnvironment ? BuildTaxiSprayRoot() : null;
            _touchdownClip = CreateTouchdownClip();
            _rotateClip = CreateRotateClip();
            _touchdownAudio = gameObject.AddComponent<AudioSource>();
            _touchdownAudio.playOnAwake = false;
            _touchdownAudio.spatialBlend = 0.55f;
            _touchdownAudio.volume = 0.22f;
            _ambientWindAudio = gameObject.AddComponent<AudioSource>();
            _ambientWindAudio.loop = true;
            _ambientWindAudio.playOnAwake = false;
            _ambientWindAudio.spatialBlend = 0f;
            _ambientWindAudio.volume = 0f;
            _ambientRainAudio = gameObject.AddComponent<AudioSource>();
            _ambientRainAudio.loop = true;
            _ambientRainAudio.playOnAwake = false;
            _ambientRainAudio.spatialBlend = 0f;
            _ambientRainAudio.volume = 0f;
            _ambientCoastAudio = gameObject.AddComponent<AudioSource>();
            _ambientCoastAudio.loop = true;
            _ambientCoastAudio.playOnAwake = false;
            _ambientCoastAudio.spatialBlend = 0f;
            _ambientCoastAudio.volume = 0f;
            _uiAudio = gameObject.AddComponent<AudioSource>();
            _uiAudio.playOnAwake = false;
            _uiAudio.spatialBlend = 0f;
            _uiAudio.volume = 0.3f;
            AirsideSceneIndex.Capture();
            if (AirsideFocusMode.ShowBuildings)
                CollectNightGlowWindows();
            _fuelFarmLight = AirsideFocusMode.ShowDecorativeLights
                ? AirsideSceneIndex.FindLight("Fuel farm light") : null;
            _arffBayLight = AirsideFocusMode.ShowDecorativeLights
                ? AirsideSceneIndex.FindLight("ARFF bay light") : null;
            var worldRenderers = AirsideSceneIndex.Renderers;
            CollectWetSurfaces(worldRenderers);
            if (AirsideFocusMode.ShowEnvironment)
                BuildWetPuddles();
            CollectHoldShortMarkings(worldRenderers);
            CollectAirfieldLights(worldRenderers);
            if (AirsideFocusMode.ShowBuildings)
            {
                var hangarDoor = AirsideSceneIndex.Find("Hangar door");
                if (hangarDoor != null)
                {
                    _hangarDoor = hangarDoor;
                    _hangarDoorClosedX = _hangarDoor.position.x;
                }

                CollectHangarDoorPanels();
                _opsAntennaDish = AirsideSceneIndex.Find("antenna_dish");
                _terminalFlag = AirsideSceneIndex.Find("flag_cloth");

                var hangarBayLightGo = AirsideSceneIndex.FindGameObject("Hangar bay light");
                if (hangarBayLightGo == null)
                {
                    hangarBayLightGo = new GameObject("Hangar bay light");
                    hangarBayLightGo.transform.position = new Vector3(-20f, 3.2f, 20.5f);
                    var bay = hangarBayLightGo.AddComponent<Light>();
                    bay.type = LightType.Point;
                    bay.color = new Color(1f, 0.88f, 0.62f);
                    bay.range = 14f;
                    bay.intensity = 0.2f;
                    AirsideSceneIndex.Remember(hangarBayLightGo);
                }
                _hangarBayLight = hangarBayLightGo.GetComponent<Light>();
            }

            if (AirsideFocusMode.ShowEnvironment)
            {
                CollectCoastalMotionTargets();
                _horizonDome = AirsideSceneIndex.Find("Horizon dome");
                _cloudRoot = AirsideSceneIndex.Find("Cloud bands");
                _cloudUmbraRoot = AirsideSceneIndex.Find("Cloud umbras");
            }
            _commercialAircraft = Array.Empty<Transform>();
            SyncCommercialAircraftViews();
            if (AirsideFocusMode.ShowGroundTrafficAircraft)
            {
                _groundTraffic = new Transform[_simulation.GroundTraffic.Count];
                for (var index = 0; index < _groundTraffic.Length; index++)
                    _groundTraffic[index] = BuildGroundTrafficAircraft(_simulation.GroundTraffic[index].Id.Value);
            }
            else
            {
                _groundTraffic = Array.Empty<Transform>();
            }
            if (AirsideFocusMode.ShowGroundVehicles)
            {
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
                // Authored GSE kits face +X; LookRotation travel aims +Z — nest a -90° yaw so they match.
                OrientPlusXKitToForward(_fuelTruck);
                OrientPlusXKitToForward(_baggageCart);
                OrientPlusXKitToForward(_passengerBus);
            }

            if (AirsideFocusMode.ShowStandEquipment)
            {
                _stairs = BuildStairs();
                _chocks = BuildChocks();
                _gpuCart = BuildGpuCart();
                _pushbackTug = BuildPushbackTug();
                OrientPlusXKitToForward(_pushbackTug);
            }

            if (AirsideFocusMode.ShowWorldProps)
                _windsockSock = BuildWindsock();
            if (AirsideFocusMode.ShowBuildings)
                EnsureStandThreeVisual();
            if (_commercialAircraft.Length > 0)
                _cameraController.SetFollowTargets(_commercialAircraft);
            AirsideRuntimeQuality.AfterWorldBuilt();
            AirsideStaticWorld.Finalize(_airfieldRoot);
            if (_apronProbe != null)
                _apronProbe.RenderProbe();
            if (_terminalProbe != null)
                _terminalProbe.RenderProbe();
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
                onContinueAway: () => { _showAwaySummary = false; },
                onUiClick: PlayUiClick);
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
                    onToggleMute: () => { _audioMuted = !_audioMuted; },
                    onUiClick: PlayUiClick);
            }
            _canvasHudActive = _canvasHud.IsActive;
        }

        private void Update()
        {
            ReadSimulationControls();
            if (_cameraController != null)
                _cameraController.FreezePresentation = _paused;
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
            AdvancePresentationClock();
            UpdateAircraftVisual();
            UpdateGroundTrafficVisual();
            UpdateServiceVehicles();
            UpdateStandEquipment();
            UpdateWindsock();
            UpdateTerminalFlag();
            if (AirsideFocusMode.ShowBuildings)
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

            if (!string.IsNullOrEmpty(_simulation.Atc.LastInstruction))
                lines.Add($"ATC · {_simulation.Atc.LastInstruction}");

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

            var circuitHud = !AirsideFocusMode.ShowEconomyHud;
            SyncLeft(
                locationLine: $"{_simulation.Location.Name}  ·  {_simulation.Location.Region}",
                flightLine: CommercialFlightHudLine(),
                phaseLine: CommercialPhaseHudLine(),
                clock: clockLine,
                clockCol: clockColor,
                cash: circuitHud ? string.Empty : cashLine,
                cashCol: cashColor,
                finance: circuitHud ? string.Empty : financeLine,
                financeCol: financeColor,
                warning: circuitHud ? string.Empty : warningLine,
                warningCol: warningColor,
                showTurnaround: !circuitHud && atStand,
                turnaround: circuitHud ? string.Empty : turnaroundLines,
                priorityVis: !circuitHud && priorityVisible,
                priorityInt: priorityInteractable,
                priorityLbl: priorityLabel,
                schedule: circuitHud ? string.Empty : scheduleLine,
                scheduleCol: scheduleColor,
                staffing: circuitHud ? string.Empty : staffingLine,
                staffingCol: staffingColor,
                early: circuitHud || earlySession,
                earlyHint: circuitHud
                    ? string.Empty
                    : "Crew / stand / research unlock after you accept a route",
                hireInt: !circuitHud && staffing.GroundCrew < AirportStaffing.MaximumGroundCrew
                         && _simulation.Economy.Cash >= AirportStaffing.HireCost,
                hireLbl: $"Hire crew · ${AirportStaffing.HireCost}",
                releaseInt: !circuitHud && staffing.GroundCrew > AirportStaffing.MinimumGroundCrew,
                buildVis: !circuitHud,
                buildInt: capacity.CanExpand && _simulation.Economy.Cash >= AirportCapacity.ThirdStandCost,
                buildLbl: capacity.HasThirdStand
                    ? "Stand 3 built"
                    : $"Build stand 3 · ${AirportCapacity.ThirdStandCost:N0}",
                stands: circuitHud ? string.Empty : $"Stands: {capacity.StandCount} / {AirportCapacity.MaximumStands}",
                research: circuitHud ? string.Empty : researchLine,
                researchProgressVis: !circuitHud && researchProgressVisible,
                researchProgress: researchProgress01,
                researchButtonVis: !circuitHud && researchButtonVisible,
                researchButtonInt: researchButtonInteractable,
                researchButtonLbl: researchButtonLabel,
                coach: circuitHud ? string.Empty : FirstSessionCoachLine(),
                coachUrgentFlag: !circuitHud && coachUrgent,
                controls: circuitHud
                    ? "Space pause · Tab speed · F follow · O overview · M mute"
                    : earlySession
                    ? "Space pause · Tab speed · Enter accept offer · F follow · O overview"
                    : "Space pause · Tab speed · P priority · M mute · F follow/cycle · O overview",
                waitMeter: !circuitHud && showWaitMeter,
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
                if (index >= _commercialAircraft.Length)
                    break;
                var flight = _simulation.Flights[index];
                var view = _commercialAircraft[index];
                if (view == null)
                    continue;
                var phase = flight.Operation.Phase;
                var progress = VisualPhaseProgress(flight, 0f);
                var lane = ApproachLaneOffset(flight);
                var route = TaxiRouteFor(flight, phase);
                var position = PositionFor(phase, progress, route, lane);
                // Keep look-ahead inside the current taxi segment so yaw does not cut corners.
                var lookAhead = phase == AircraftPhase.Takeoff
                        && progress < AirsideFlightPath.LineupProgress ? 0.04f
                    : phase is AircraftPhase.TaxiOut or AircraftPhase.TaxiIn or AircraftPhase.Pushback ? 0.03f
                    : 0.15f;
                var next = PositionFor(phase, VisualPhaseProgress(flight, lookAhead), route, lane);
                // Fractional phase progress is exact — catch-up lag made some phases slide
                // while airborne phases snapped, which read as inconsistent smoothness.
                view.position = position;

                var direction = next - position;
                var heading = direction.sqrMagnitude > 0.001f
                    ? Quaternion.LookRotation(direction.normalized)
                    : view.rotation;
                var pitch = PhasePitchDegrees(phase, progress);
                var bank = SmoothedBankDegrees(flight.AircraftId, view, heading, phase);
                var targetRotation = heading * Quaternion.Euler(pitch, 0f, bank);
                // Exponential damping keeps the turn rate identical at 30 and 144 fps, and
                // freezes attitude while paused instead of drifting on unscaled time.
                var turnRate = phase == AircraftPhase.Takeoff && progress < AirsideFlightPath.RotateProgress * 0.4f ? 8f : 5f;
                view.rotation = Quaternion.Slerp(
                    view.rotation,
                    targetRotation,
                    AirsideFlightPath.DampFactor(turnRate, PresentationDeltaTime));

                SpinPropellers(view, phase);
                RollLandingGearTires(view, phase, progress);
                ApplyOleoSettling(view, phase, progress);
                UpdateControlSurfaces(view, phase, progress, bank, PresentationDeltaTime);
                UpdateGroundShadow(view);
                UpdateAircraftLightsAndGear(view, phase, PresentationDaylight, progress, PresentationDeltaTime);
                UpdateCabinDoor(view, phase);
                UpdateCabinWindowGlow(view, phase, PresentationDaylight);
                UpdateEngineHeat(view, phase);

                if (_cameraController != null
                    && _cameraController.IsFollowing
                    && _cameraController.FollowTarget == view)
                    _cameraController.SetFollowPhase(phase, progress);
            }
        }

        private static float PhasePitchDegrees(AircraftPhase phase, float progress) =>
            AirsideFlightPath.PitchDegrees(phase, progress);

        private static float TurnBankDegrees(Transform view, Quaternion targetRotation, AircraftPhase phase)
        {
            if (phase is AircraftPhase.AtStand or AircraftPhase.Departed)
                return 0f;

            // Ground phases stay wings-level so wingtips do not dig into the apron.
            if (phase is AircraftPhase.TaxiIn or AircraftPhase.TaxiOut or AircraftPhase.Pushback
                or AircraftPhase.Landing or AircraftPhase.AtStand)
                return 0f;

            var yawDelta = Mathf.DeltaAngle(view.eulerAngles.y, targetRotation.eulerAngles.y);
            var limit = 16f;
            return Mathf.Clamp(-yawDelta * 2.2f, -limit, limit);
        }

        /// <summary>
        /// Roll in and out of a turn instead of snapping to the instantaneous yaw error.
        /// The raw value jitters frame to frame because it is a difference of two poses
        /// that are themselves being damped, which made the wings twitch on every turn.
        /// </summary>
        private float SmoothedBankDegrees(string aircraftId, Transform view, Quaternion heading, AircraftPhase phase)
        {
            var target = TurnBankDegrees(view, heading, phase);
            if (!_bankDegrees.TryGetValue(aircraftId, out var current))
                current = target;

            var dt = PresentationDeltaTime;
            // Roll in a little slower than the aircraft rolls out — matches how a turn reads.
            var rate = Mathf.Abs(target) > Mathf.Abs(current) ? 2.2f : 3.2f;
            current = Mathf.Lerp(current, target, AirsideFlightPath.DampFactor(rate, dt));
            _bankDegrees[aircraftId] = current;
            return current;
        }

        private static void UpdateControlSurfaces(
            Transform aircraft, AircraftPhase phase, float progress, float bankDegrees, float deltaTime)
        {
            // Presentation-only: rudder/elevator deflect with attitude (Batch D life).
            // deltaTime is the presentation clock, so surfaces hold still while paused
            // and sweep 4x faster at 4x speed instead of running on their own timeline.
            if (deltaTime <= 0f)
                return;
            var pitch = PhasePitchDegrees(phase, progress);
            var elevator = Mathf.Clamp(-pitch * 1.4f, -22f, 22f);
            var rudder = Mathf.Clamp(-bankDegrees * 0.9f, -18f, 18f);
            var children = AirsideNamedChildren.Get(aircraft);
            var hasSeparateElevators = false;
            foreach (var part in children)
            {
                if (part != null && part.name.StartsWith("Elevator", StringComparison.Ordinal))
                {
                    hasSeparateElevators = true;
                    break;
                }
            }
            foreach (var child in children)
            {
                if (child == aircraft)
                    continue;
                if (child.name.StartsWith("Rudder", StringComparison.Ordinal))
                {
                    var euler = child.localEulerAngles;
                    var current = euler.y > 180f ? euler.y - 360f : euler.y;
                    euler.y = Mathf.MoveTowards(current, rudder, deltaTime * 90f);
                    child.localEulerAngles = euler;
                }
                else if (child.name.IndexOf("elevator", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         (!hasSeparateElevators && child.name.StartsWith("Tailplane", StringComparison.Ordinal)))
                {
                    // Soft elevator cue on the whole tailplane when no separate elevator mesh.
                    var euler = child.localEulerAngles;
                    var current = euler.x > 180f ? euler.x - 360f : euler.x;
                    var target = child.name.StartsWith("Tailplane", StringComparison.Ordinal) ? elevator * 0.35f : elevator;
                    euler.x = Mathf.MoveTowards(current, target, deltaTime * 80f);
                    child.localEulerAngles = euler;
                }
                else if (child.name.StartsWith("Aileron", StringComparison.Ordinal))
                {
                    var euler = child.localEulerAngles;
                    var current = euler.x > 180f ? euler.x - 360f : euler.x;
                    var side = child.name.IndexOf(" L", StringComparison.Ordinal) >= 0 ? 1f : -1f;
                    var target = Mathf.Clamp(bankDegrees * 0.8f * side, -18f, 18f);
                    euler.x = Mathf.MoveTowards(current, target, deltaTime * 90f);
                    child.localEulerAngles = euler;
                }
                else if (child.name is "Flap L" or "Flap R")
                {
                    // Takeoff flap is set for the roll and milked off after rotation —
                    // it used to keep extending all the way through the climb.
                    var deploy = AirsideReusableMotion.FlapDegrees(phase, progress);
                    var euler = child.localEulerAngles;
                    var current = euler.x > 180f ? euler.x - 360f : euler.x;
                    euler.x = Mathf.MoveTowards(current, deploy, deltaTime * 40f);
                    child.localEulerAngles = euler;
                }
                else if (child.name.StartsWith("Spoiler", StringComparison.Ordinal))
                {
                    // Spoilers pop on touchdown and stow as the rollout ends, rather
                    // than creeping up from zero through the whole flare.
                    var raise = phase == AircraftPhase.Landing
                        ? 35f * Mathf.Clamp01(Mathf.InverseLerp(
                              AirsideFlightPath.TouchdownProgress,
                              AirsideFlightPath.TouchdownProgress + 0.06f, progress)
                            - Mathf.InverseLerp(0.86f, 1f, progress))
                        : 0f;
                    var euler = child.localEulerAngles;
                    var current = euler.x > 180f ? euler.x - 360f : euler.x;
                    euler.x = Mathf.MoveTowards(current, -raise, deltaTime * 55f);
                    child.localEulerAngles = euler;
                }
            }
        }

        private void UpdateEngineAudio()
        {
            for (var index = 0; index < _simulation.Flights.Count && index < _commercialAircraft.Length; index++)
            {
                var phase = _simulation.Flights[index].Operation.Phase;
                ApplyEngineAudio(_commercialAircraft[index],
                    AirsideReusableMotion.PropellersSpinning(phase));
            }

            for (var index = 0; index < _groundTraffic.Length; index++)
            {
                var traffic = _simulation.GroundTraffic[index];
                // Ground traffic keeps props turning while on the field; quieter when holding.
                ApplyEngineAudio(_groundTraffic[index], enginesOn: !traffic.IsHolding);
            }
        }

        private void PlayUiClick()
        {
            if (_audioMuted || _uiAudio == null)
                return;
            if (_uiClickClip == null)
                _uiClickClip = Resources.Load<AudioClip>("Airside/Audio/ui_select_005");
            if (_uiClickClip == null)
                return;

            _uiAudio.PlayOneShot(_uiClickClip);
        }

        private void ApplyEngineAudio(Transform aircraft, bool enginesOn)
        {
            if (aircraft == null)
                return;

            var id = aircraft.GetInstanceID();
            if (!_engineAudio.TryGetValue(id, out var source) || source == null)
            {
                source = aircraft.GetComponent<AudioSource>();
                if (source == null)
                    return;
                _engineAudio[id] = source;
            }

            if (_audioMuted)
            {
                source.volume = 0f;
                return;
            }

            // Engine note follows the spooled RPM. It used to drone at one pitch and one
            // running volume, so a takeoff sounded exactly like a pushback.
            var power = _propRpm.TryGetValue(id, out var rpm)
                ? Mathf.InverseLerp(AirsideReusableMotion.PropRpmTaxi,
                    AirsideReusableMotion.PropRpmTakeoff, rpm)
                : 0f;
            source.pitch = Mathf.Lerp(0.85f, 1.2f, power);

            var target = enginesOn ? Mathf.Lerp(EngineVolumeRunning, EngineVolumeRunning * 1.5f, power) : EngineVolumeIdle;
            if (_paused)
                target *= EngineVolumePausedScale;
            source.volume = Mathf.MoveTowards(source.volume, target, Time.unscaledDeltaTime * 0.4f);
        }

        private void UpdateAmbientAudio()
        {
            if (_ambientWindAudio == null || _ambientRainAudio == null)
                return;

            EnsureAmbientClips();

            var weather = _simulation.CurrentWeather;
            var raining = weather == WeatherKind.Rain || weather == WeatherKind.Storm;
            var storm = weather == WeatherKind.Storm;
            var windTarget = _audioMuted ? 0f : AmbientWindVolume;
            var rainTarget = _audioMuted || !raining ? 0f : (storm ? AmbientStormVolume : AmbientRainVolume);
            var coastTarget = _audioMuted || AirsideFocusMode.BareWorld ? 0f : AmbientCoastVolume * (storm ? 1.45f : raining ? 1.2f : 1f);
            if (_paused)
            {
                windTarget *= EngineVolumePausedScale;
                rainTarget *= EngineVolumePausedScale;
                coastTarget *= EngineVolumePausedScale;
            }

            // Slight day/night wind variation (presentation only).
            if (!_audioMuted)
                windTarget *= Mathf.Lerp(0.75f, 1.1f, 1f - PresentationDaylight);

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

        private void EnsureAmbientClips()
        {
            if (_ambientWindAudio != null && _ambientWindAudio.clip == null)
            {
                var clip = Resources.Load<AudioClip>("Airside/Audio/wind_whoosh_loop") ?? CreateWindClip();
                _ambientWindAudio.clip = clip;
                if (clip != null && !_ambientWindAudio.isPlaying)
                    _ambientWindAudio.Play();
            }

            if (_ambientRainAudio != null && _ambientRainAudio.clip == null)
            {
                var clip = Resources.Load<AudioClip>("Airside/Audio/rain_loop_03") ?? CreateRainClip();
                _ambientRainAudio.clip = clip;
                if (clip != null && !_ambientRainAudio.isPlaying)
                    _ambientRainAudio.Play();
            }

            if (_ambientCoastAudio != null && _ambientCoastAudio.clip == null)
            {
                var clip = Resources.Load<AudioClip>("Airside/Audio/coast_wave_01") ?? CreateCoastClip();
                _ambientCoastAudio.clip = clip;
                if (clip != null && !_ambientCoastAudio.isPlaying)
                    _ambientCoastAudio.Play();
            }
        }

        private static void UpdateAircraftLightsAndGear(
            Transform aircraft, AircraftPhase phase, float daylight, float progress01 = 1f, float deltaTime = -1f)
        {
            if (deltaTime < 0f)
                deltaTime = Time.unscaledDeltaTime;
            // Pause freezes strut/door motion with the presentation clock.
            if (deltaTime <= 0f)
                deltaTime = 0f;
            var gearBias = AirsideReusableMotion.GearBias(phase, progress01);
            var airborne = phase == AircraftPhase.Departed
                || phase == AircraftPhase.Approach
                || (phase == AircraftPhase.Takeoff && gearBias < 0.5f);
            var enginesOn = AirsideReusableMotion.PropellersSpinning(phase);
            var night = daylight < 0.35f;
            var landingLights = AirsideReusableMotion.LandingLightsOn(phase, progress01);
            var taxiLights = !airborne && (night || phase is AircraftPhase.TaxiIn or AircraftPhase.TaxiOut or AircraftPhase.Pushback);

            foreach (var child in AirsideNamedChildren.Get(aircraft))
            {
                if (child == aircraft)
                    continue;
                if (child.name.StartsWith("Gear door", StringComparison.Ordinal))
                {
                    // Doors open only while the gear is in transit; closed when locked
                    // up or locked down so the wells read correctly on the rollout.
                    child.gameObject.SetActive(true);
                    var euler = child.localEulerAngles;
                    var current = euler.x > 180f ? euler.x - 360f : euler.x;
                    var doorOpen = AirsideReusableMotion.GearDoorOpenBias(phase, progress01);
                    var target = Mathf.Lerp(0f, 78f, doorOpen);
                    euler.x = Mathf.MoveTowards(current, target, deltaTime * 90f);
                    child.localEulerAngles = euler;
                }
                else if (child.name is "Gear nose" or "Gear L" or "Gear R")
                {
                    // Soft retract/deploy instead of a hard pop (Batch D ANM-AIR-002 language).
                    // Exact strut names only — densified "Gear scissors *" must not pitch with legs.
                    child.gameObject.SetActive(true);
                    var euler = child.localEulerAngles;
                    var current = euler.x > 180f ? euler.x - 360f : euler.x;
                    var target = Mathf.Lerp(0f, -80f, 1f - gearBias);
                    euler.x = Mathf.MoveTowards(current, target, deltaTime * 70f);
                    child.localEulerAngles = euler;
                }
                else if (child.name.StartsWith("NavLight", StringComparison.Ordinal))
                {
                    var navOn = enginesOn || night;
                    child.gameObject.SetActive(navOn);
                    EnsureNavPointLight(child, navOn, IsNavLightRight(child.name));
                }
                else if (child.name.StartsWith("Beacon", StringComparison.Ordinal))
                {
                    // ANM-AIR-004 — pulse from the presentation clock so pause freezes the blink.
                    var beaconOn = enginesOn;
                    if (beaconOn && deltaTime > 0f)
                        beaconOn = Mathf.FloorToInt(Time.unscaledTime * AirsideReusableMotion.BeaconHz * 2f) % 2 == 0;
                    else if (beaconOn)
                        beaconOn = child.gameObject.activeSelf;
                    child.gameObject.SetActive(beaconOn);
                    EnsureBeaconPointLight(child, beaconOn);
                }
                else if (child.name.StartsWith("LandingLight", StringComparison.Ordinal))
                {
                    child.gameObject.SetActive(landingLights);
                    EnsureLandingSpotLight(child, landingLights, night);
                    var lamp = child.GetComponent<Renderer>();
                    if (lamp != null)
                    {
                        lamp.GetPropertyBlock(RendererTintBlock);
                        var color = landingLights
                            ? new Color(1f, 0.97f, 0.88f)
                            : new Color(0.55f, 0.55f, 0.5f);
                        RendererTintBlock.SetColor("_Color", color);
                        RendererTintBlock.SetColor("_BaseColor", color);
                        RendererTintBlock.SetColor("_EmissionColor", landingLights
                            ? new Color(2.6f, 2.5f, 2.1f)
                            : Color.black);
                        lamp.SetPropertyBlock(RendererTintBlock);
                    }
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
        private static bool IsNavLightRight(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
            var lower = name.ToLowerInvariant();
            if (lower.Contains("right") || lower.Contains("_r") || lower.EndsWith(" r") || lower.EndsWith("-r"))
                return true;
            if (lower.Contains("left") || lower.Contains("_l") || lower.EndsWith(" l") || lower.EndsWith("-l"))
                return false;
            // Exact trailing R/L after a separator — avoid matching bare "NavLight".
            if (name.EndsWith(" R", StringComparison.Ordinal) || name.EndsWith("_R", StringComparison.Ordinal))
                return true;
            if (name.EndsWith(" L", StringComparison.Ordinal) || name.EndsWith("_L", StringComparison.Ordinal))
                return false;
            return false;
        }

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
            // Pinned daylight washes a night-tuned lamp. Keep the beam readable in follow.
            light.intensity = night ? 7.5f : 9.5f;
            light.range = 90f;
            light.spotAngle = 48f;
            light.innerSpotAngle = 22f;
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
            foreach (var child in AirsideNamedChildren.Get(aircraft))
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
            foreach (var child in AirsideNamedChildren.Get(aircraft))
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
                if (renderer == null)
                    continue;
                SetRendererColor(renderer, Color.white, intensity > 0.01f ? glow : Color.black);
            }
        }

        private static void UpdateEngineHeat(Transform aircraft, AircraftPhase phase)
        {
            // Presentation-only: subtle heat shimmer behind running engines.
            var enginesOn = phase != AircraftPhase.AtStand && phase != AircraftPhase.Departed;
            var intensity = phase is AircraftPhase.Takeoff or AircraftPhase.Approach ? 1.25f : 1f;
            foreach (var child in AirsideNamedChildren.Get(aircraft))
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
                    var color = GetRendererColor(renderer);
                    color.a = (0.12f + 0.1f * pulse) * intensity;
                    SetRendererColor(renderer, color);
                }
            }
        }

        /// <summary>Sim-rate presentation dt — freezes when paused, scales at 4×.</summary>
        private float PresentationDeltaTime => _paused ? 0f : Time.unscaledDeltaTime * _speed;

        private void SpinPropellers(Transform aircraft, AircraftPhase phase)
        {
            // Presentation-only: RPM follows phase (Batch F4 ANM-AIR-001 via AirsideReusableMotion).
            // RPM used to jump straight to the new phase value, so takeoff power arrived
            // in one frame and engines stopped dead at shutdown. Spool between them
            // instead — up faster than down, the way an engine accepts throttle.
            var targetRpm = AirsideReusableMotion.PropellersSpinning(phase)
                ? AirsideReusableMotion.PropRpmForPhase(phase)
                : 0f;
            var rpm = SpooledPropRpm(aircraft, targetRpm);
            if (rpm < 1f)
            {
                ApplyPropBlur(aircraft, highRpm: false);
                return;
            }

            // Constants are true RPM — convert to degrees/sec (×6) so blades read as spinning.
            var degrees = PresentationDeltaTime * rpm * 6f;
            if (degrees <= 0f)
                return;
            var highRpm = rpm >= AirsideReusableMotion.PropHighRpmThreshold;
            foreach (var child in AirsideNamedChildren.Get(aircraft))
            {
                if (child == aircraft)
                    continue;
                if (!child.name.StartsWith("Propeller", StringComparison.Ordinal))
                    continue;
                child.Rotate(Vector3.forward, degrees, Space.Self);
                ApplyPropBlurToHub(child, highRpm);
            }
        }

        private float SpooledPropRpm(Transform aircraft, float targetRpm)
        {
            var key = aircraft.GetInstanceID();
            if (!_propRpm.TryGetValue(key, out var current))
                current = targetRpm;
            var rate = targetRpm > current ? 1.6f : 0.8f;
            current = Mathf.Lerp(current, targetRpm, AirsideFlightPath.DampFactor(rate, PresentationDeltaTime));
            _propRpm[key] = current;
            return current;
        }

        private void SpinGroundTrafficPropellers(Transform aircraft, bool enginesOn)
        {
            // Match ANM-AIR taxi RPM so ground traffic props read with the fleet, and
            // spool through start-up / shutdown rather than snapping on and off.
            var rpm = SpooledPropRpm(aircraft, enginesOn ? AirsideReusableMotion.PropRpmTaxi : 0f);
            if (rpm < 1f)
            {
                ApplyPropBlur(aircraft, highRpm: false);
                return;
            }

            var degrees = PresentationDeltaTime * rpm * 6f;
            if (degrees <= 0f)
                return;
            var highRpm = rpm >= AirsideReusableMotion.PropHighRpmThreshold;
            foreach (var child in AirsideNamedChildren.Get(aircraft))
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
            foreach (var child in AirsideNamedChildren.Get(aircraft))
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

        private void RollLandingGearTires(Transform aircraft, AircraftPhase phase, float progress)
        {
            // Distance travelled / radius — stops naturally when ground speed is zero.
            var groundSpeed = AirsideFlightPath.GroundSpeedMetresPerSecond(phase, progress);
            if (groundSpeed <= 0.001f || PresentationDeltaTime <= 0f)
                return;

            foreach (var child in AirsideNamedChildren.Get(aircraft))
            {
                if (child == aircraft)
                    continue;
                if (!(child.name.StartsWith("Tire", StringComparison.Ordinal) ||
                      (child.name.IndexOf("wheel", StringComparison.OrdinalIgnoreCase) >= 0
                       && child.name.IndexOf("arch", StringComparison.OrdinalIgnoreCase) < 0
                       && child.name.IndexOf("hub", StringComparison.OrdinalIgnoreCase) < 0)))
                    continue;
                var radius = AirsideReusableMotion.TireRadiusMetres(child.name);
                var degrees = PresentationDeltaTime
                    * AirsideFlightPath.TireAngularDegreesPerSecond(groundSpeed, radius);
                if (degrees > 0f)
                    child.Rotate(Vector3.right, degrees, Space.Self);
            }
        }

        /// <summary>
        /// Brief body settle after touchdown. Applied on top of the path pose.
        /// Presentation only — never feeds simulation.
        /// </summary>
        private static void ApplyOleoSettling(Transform aircraft, AircraftPhase phase, float progress)
        {
            if (aircraft == null)
                return;
            var compression = AirsideReusableMotion.OleoCompressionMetres(phase, progress);
            if (compression <= 0f)
                return;
            var world = aircraft.position;
            aircraft.position = new Vector3(world.x, world.y - compression, world.z);
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

            // Drop slot reservations for aircraft that have left the schedule.
            var liveIds = new HashSet<string>(needed);
            foreach (var flight in flights)
                liveIds.Add(flight.AircraftId);
            var staleSlots = new List<string>();
            foreach (var pair in _commercialLiverySlot)
            {
                if (!liveIds.Contains(pair.Key))
                    staleSlots.Add(pair.Key);
            }
            foreach (var id in staleSlots)
                _commercialLiverySlot.Remove(id);

            var next = new Transform[needed];
            var kept = new HashSet<Transform>();
            var visibleLimit = AirsideFocusMode.VisibleCommercialFlights;
            for (var index = 0; index < needed; index++)
            {
                var flight = flights[index];
                var visible = index < visibleLimit;

                // Assign a stable livery slot: reuse this aircraft's slot, else take
                // the lowest slot no other current aircraft holds.
                if (!_commercialLiverySlot.TryGetValue(flight.AircraftId, out var slot))
                {
                    slot = 0;
                    while (true)
                    {
                        var taken = false;
                        foreach (var pair in _commercialLiverySlot)
                        {
                            if (pair.Value == slot) { taken = true; break; }
                        }
                        if (!taken)
                            break;
                        slot++;
                    }
                    _commercialLiverySlot[flight.AircraftId] = slot;
                }

                if (byId.TryGetValue(flight.AircraftId, out var existing))
                {
                    next[index] = existing;
                    kept.Add(existing);
                    existing.gameObject.SetActive(visible);
                    continue;
                }

                if (!visible)
                {
                    next[index] = null;
                    continue;
                }

                // Coastline Regional v06 turboprop — the smooth reference airframe.
                var color = new Color(0.12f, 0.43f, 0.76f);
                var livery = "Textures/Decals/dc_livery_coastline_regional_v01.png";
                next[index] = BuildAircraft($"Commercial {flight.AircraftId}", color, livery);
            }

            foreach (var pair in byId)
            {
                if (!kept.Contains(pair.Value) && pair.Value != null)
                {
                    AirsideNamedChildren.Forget(pair.Value);
                    Destroy(pair.Value.gameObject);
                }
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
            {
                var follow = next.Where(t => t != null && t.gameObject.activeSelf).ToArray();
                if (follow.Length > 0)
                    _cameraController.SetFollowTargets(follow);
            }
        }

        private void UpdateGroundTrafficVisual()
        {
            if (_groundTraffic == null || _groundTraffic.Length == 0)
                return;

            for (var index = 0; index < _groundTraffic.Length; index++)
            {
                var view = _groundTraffic[index];
                var traffic = _simulation.GroundTraffic[index];
                var point = traffic.Position;
                var target = new Vector3(point.X, 0.7f, point.Z);
                var previous = view.position;

                // A yield can snap the sim point back; do not lerp through released space.
                // Catch-up rate tracks pause / 1× / 4× so GT does not slide while frozen.
                var catchUp = PresentationDeltaTime * 10f;
                view.position = catchUp <= 0f
                    ? previous
                    : TaxiVisualPath.MoveGroundTraffic(
                        previous,
                        target,
                        traffic.IsHolding,
                        catchUp);

                var direction = target - previous;
                var targetRotation = direction.sqrMagnitude > 0.0004f
                    ? Quaternion.LookRotation(direction.normalized)
                    : view.rotation;
                var visualPhase = GroundTrafficVisualPhase(traffic);
                if (traffic.IsHolding)
                {
                    // Keep last travel heading on a yield snap (zero-length move).
                    targetRotation = view.rotation;
                }
                view.rotation = Quaternion.Slerp(
                    view.rotation,
                    targetRotation,
                    AirsideFlightPath.DampFactor(4f, PresentationDeltaTime));

                if (traffic.CurrentPhase is "Away" or "Away hold")
                {
                    view.gameObject.SetActive(false);
                    continue;
                }
                if (!view.gameObject.activeSelf)
                    view.gameObject.SetActive(true);

                var enginesOn = GroundTrafficEnginesOn(traffic);
                SpinGroundTrafficPropellers(view, enginesOn);
                RollLandingGearTires(
                    view,
                    traffic.IsHolding || traffic.IsAtStand ? AircraftPhase.AtStand : AircraftPhase.TaxiIn,
                    1f);
                UpdateGroundShadow(view);
                UpdateAircraftLightsAndGear(
                    view,
                    visualPhase,
                    PresentationDaylight);
                UpdateCabinWindowGlow(
                    view,
                    visualPhase,
                    PresentationDaylight);
                UpdateEngineHeat(view, visualPhase);
            }
        }

        private static AircraftPhase GroundTrafficVisualPhase(GroundTrafficAircraft traffic)
        {
            if (traffic.IsAtStand)
                return AircraftPhase.AtStand;
            var phase = traffic.CurrentPhase;
            if (phase is "Away" or "Away hold" or "Waiting for a slot")
                return AircraftPhase.Departed;
            if (phase == "Run-up hold")
                return AircraftPhase.Pushback;
            return AircraftPhase.TaxiIn;
        }

        private static bool GroundTrafficEnginesOn(GroundTrafficAircraft traffic)
        {
            if (traffic.IsAtStand)
                return false;
            var phase = traffic.CurrentPhase;
            return phase is not ("Away" or "Waiting for a slot");
        }

                private float PresentationClock
        {
            get
            {
                // Accumulates only while the sim is presenting motion (pauses freeze beacons/flags).
                return _presentationClock;
            }
        }

        private void AdvancePresentationClock()
        {
            _presentationClock += PresentationDeltaTime;
        }

        /// <summary>
        /// How far through its current phase an aircraft should be drawn, read at the
        /// fractional presentation clock rather than the simulated second. Sampling the
        /// simulation directly gave a 1 Hz staircase — 59 still frames then a jump, four
        /// times longer at 4x, which is what made the fast speed look so much worse.
        ///
        /// <paramref name="lookAheadSeconds"/> asks for the position a moment later, used
        /// to point the nose along the path.
        /// </summary>
        private float VisualPhaseProgress(CommercialFlight flight, float lookAheadSeconds)
        {
            var operation = flight.Operation;
            var phase = operation.Phase;
            var duration = AirsideFlightPath.PhaseSeconds(phase);
            var progress = PhaseProgressNow(flight, duration);

            if (lookAheadSeconds <= 0f || duration <= 0f)
                return progress;

            return Mathf.Clamp01(progress + lookAheadSeconds / duration);
        }

        private float PhaseProgressNow(CommercialFlight flight, float duration)
        {
            var operation = flight.Operation;

            // A flight waiting on a reservation has its phase clock pushed forward once
            // per stalled second, so a fractional read would creep forward and snap back
            // every second. Hold the simulated value: the aircraft is standing still,
            // which is exactly what it is doing.
            if (_simulation.TrafficWaits.TryGetWaitStart(flight.OwnerId, out _))
                return Mathf.Clamp01((float)operation.PhaseProgress(_clock.Now));

            return AirsideAircraftMotion.PhaseProgress(
                _preciseTime, operation.PhaseStartedAt.ElapsedSeconds, duration);
        }

        private void UpdateServiceVehicles()
        {
            if (!AirsideFocusMode.ShowGroundVehicles)
                return;

            // Prefer the watched/focused commercial at stand so dual-stand activity matches the player view.
            var servicing = PreferWatchedAtStandFlight(requireTurnaround: true);

            // Parked GSE stays visible on the apron edge so the field feels staffed.
            var fuelPark = new Vector3(-2.5f, 0.55f, 24.5f);
            var bagPark = new Vector3(0.5f, 0.42f, 25.2f);
            var busPark = new Vector3(2.5f, 0.68f, 26f);

            if (servicing == null)
            {
                UpdateVehicle(_fuelTruck, false, fuelPark, fuelPark);
                UpdateVehicle(_baggageCart, false, bagPark, bagPark);
                UpdateVehicle(_passengerBus, false, busPark, busPark);
                SyncVehicleHeadlights(_fuelTruck, PresentationDaylight < 0.38f, PresentationDaylight);
                SyncVehicleHeadlights(_baggageCart, PresentationDaylight < 0.38f, PresentationDaylight);
                SyncVehicleHeadlights(_passengerBus, PresentationDaylight < 0.38f, PresentationDaylight);
                return;
            }

            var standZ = AirportTaxiNetwork.StandZ(servicing.AssignedStand);
            var fuelActive = TaskActive(servicing, "Refuel");
            var bagActive = TaskActive(servicing, "Unload bags") || TaskActive(servicing, "Load bags");
            var paxActive = TaskActive(servicing, "Passengers off") || TaskActive(servicing, "Board passengers");
            // Approved turnaround day/dusk board: fuel at port wing, baggage port-forward,
            // bus starboard clear of props.
            UpdateVehicle(_fuelTruck, fuelActive, new Vector3(18.8f, 0.55f, standZ + 2.4f), fuelPark);
            UpdateVehicle(_baggageCart, bagActive, new Vector3(19.4f, 0.42f, standZ + 2.4f), bagPark);
            UpdateVehicle(_passengerBus, paxActive, new Vector3(15.6f, 0.68f, standZ + 4.2f), busPark);
            var daylight = PresentationDaylight;
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
            if (!AirsideFocusMode.ShowStandEquipment)
                return;

            // Presentation-only stand props — prefer the flight the player is watching.
            var atStand = PreferWatchedAtStandFlight(requireTurnaround: false);
            CommercialFlight pushing = PreferWatchedFlightInPhase(AircraftPhase.Pushback);
            if (pushing == null)
            {
                foreach (var flight in _simulation.Flights)
                {
                    if (flight.Operation.Phase == AircraftPhase.Pushback)
                    {
                        pushing = flight;
                        break;
                    }
                }
            }

            if (atStand != null)
            {
                var z = AirportTaxiNetwork.StandZ(atStand.AssignedStand);
                var progress = VisualPhaseProgress(atStand, 0f);
                // Stairs roll in from apron edge, then tip up toward the cabin along kit long axis.
                var arrive = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress * 5f));
                var stairsX = Mathf.Lerp(20.5f, 17.9f, arrive);
                var stairsPitch = Mathf.Lerp(-42f, -6f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress * 4f)));
                // Yaw ~90 so kit long axis (+Z) aims toward cabin (−X), then pitch about local X.
                PlaceProp(_stairs, true, new Vector3(stairsX, 0.55f, z + 0.15f),
                    Quaternion.Euler(0f, 90f, 0f) * Quaternion.Euler(stairsPitch, 0f, 0f));
                // Paired chocks at main-gear tracks (board inventory).
                var chockArrive = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress * 6f));
                var chockY = Mathf.Lerp(0.42f, 0.12f, chockArrive);
                var chockRoll = Mathf.Lerp(35f, 0f, chockArrive);
                PlaceProp(_chocks, true, new Vector3(16.6f, chockY, z + 1.55f), Quaternion.Euler(0f, 0f, chockRoll));
                // GPU at nose in Coastal Blue (board); cable toward aircraft.
                PlaceProp(_gpuCart, true, new Vector3(15.6f, 0.35f, z + 2.1f), Quaternion.Euler(0f, 90f, 0f));
                PulseGpuCart(_gpuCart, true);
                // Pushback tug waits at nose during AtStand so the service set matches the board.
                PlaceProp(_pushbackTug, true, new Vector3(13.4f, 0.4f, z - 0.2f),
                    Quaternion.LookRotation(new Vector3(-1, 0, 7.85f)));
                SyncVehicleHeadlights(_pushbackTug, PresentationDaylight < 0.38f,
                    PresentationDaylight);
            }
            else
            {
                PlaceProp(_stairs, false, Vector3.zero, Quaternion.identity);
                PlaceProp(_chocks, false, Vector3.zero, Quaternion.identity);
                PlaceProp(_gpuCart, false, Vector3.zero, Quaternion.identity);
                PulseGpuCart(_gpuCart, false);
                if (pushing == null)
                    PlaceProp(_pushbackTug, false, Vector3.zero, Quaternion.identity);
            }

            if (pushing != null)
            {
                var z = AirportTaxiNetwork.StandZ(pushing.AssignedStand);
                var progress = VisualPhaseProgress(pushing, 0f);
                // Match commercial Pushback endpoints so the tug does not lag a parallel path.
                var from = new Vector3(17f, 0.4f, z);
                var to = new Vector3(12f, 0.4f, z - 2f);
                var tugPos = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, progress));
                var travel = to - from;
                travel.y = 0f;
                var facing = travel.sqrMagnitude > 0.0001f
                    ? Quaternion.LookRotation(travel.normalized, Vector3.up)
                    : Quaternion.LookRotation(new Vector3(-1, 0, 7.65f));
                PlaceProp(_pushbackTug, true, tugPos, facing);
                PulseServiceBeacon(_pushbackTug, true);
                SyncVehicleHeadlights(_pushbackTug, true, PresentationDaylight);
            }
            else if (atStand == null)
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
                _terminalFlag = AirsideSceneIndex.Find("flag_cloth");
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
            if (!AirsideCombinedSurfaces.UseTileOperational)
            {
                CreateBlock(
                    "Stand 3 apron",
                    new Vector3(40f, 0.07f, 18.6f),
                    new Vector3(18f, 0.05f, 22f),
                    new Color(0.46f, 0.48f, 0.50f));
                CreateBlock(
                    "Stand 3 taxi lead",
                    new Vector3(18f, 0.065f, 18.6f),
                    new Vector3(26f, 0.04f, 8f),
                    new Color(0.50f, 0.50f, 0.52f));
                CreateCone(new Vector3(32.2f, 0.18f, 28.4f));
                CreateCone(new Vector3(32.2f, 0.18f, 8.8f));
                CreateCone(new Vector3(47.8f, 0.18f, 28.4f));
                CreateCone(new Vector3(47.8f, 0.18f, 8.8f));
            }
        }



        private bool TaskActive(CommercialFlight flight, string name)
        {
            return flight.Turnaround != null &&
                   flight.Turnaround.Tasks(_clock.Now).Any(task => task.Name == name && task.State == TurnaroundTaskState.Active);
        }

        /// <summary>
        /// Prefer the commercial the camera is following when it is at stand; otherwise
        /// the first at-stand flight (matches FocusFlight intent for dual-stand play).
        /// </summary>
        private CommercialFlight PreferWatchedAtStandFlight(bool requireTurnaround)
        {
            var followed = PreferWatchedFlightInPhase(AircraftPhase.AtStand);
            if (followed != null && (!requireTurnaround || followed.Turnaround != null))
                return followed;

            foreach (var flight in _simulation.Flights)
            {
                if (flight.Operation.Phase != AircraftPhase.AtStand)
                    continue;
                if (requireTurnaround && flight.Turnaround == null)
                    continue;
                return flight;
            }

            return null;
        }

        private CommercialFlight PreferWatchedFlightInPhase(AircraftPhase phase)
        {
            if (_cameraController == null || !_cameraController.IsFollowing || _cameraController.FollowTarget == null)
                return null;
            if (_commercialAircraft == null)
                return null;

            for (var i = 0; i < _commercialAircraft.Length && i < _simulation.Flights.Count; i++)
            {
                if (_commercialAircraft[i] != _cameraController.FollowTarget)
                    continue;
                var flight = _simulation.Flights[i];
                return flight.Operation.Phase == phase ? flight : null;
            }

            return null;
        }

        /// <summary>
        /// Authored vehicle kits face along +X; nest a -90° yaw so LookRotation(+Z) travel
        /// aligns the kit long axis / cab with the travel direction.
        /// </summary>
        private static void OrientPlusXKitToForward(Transform root)
        {
            if (root == null || root.childCount == 0)
                return;
            if (root.Find("FacingOffset") != null)
                return;

            var offset = new GameObject("FacingOffset").transform;
            offset.SetParent(root, false);
            offset.localPosition = Vector3.zero;
            offset.localRotation = Quaternion.Euler(0f, -90f, 0f);
            offset.localScale = Vector3.one;

            var toReparent = new List<Transform>();
            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child != offset)
                    toReparent.Add(child);
            }

            foreach (var child in toReparent)
                child.SetParent(offset, false);
        }

        private void UpdateVehicle(Transform vehicle, bool active, Vector3 servicePosition, Vector3 parkPosition)
        {
            if (vehicle == null)
                return;

            vehicle.gameObject.SetActive(true);
            var target = active ? servicePosition : parkPosition;
            // First show may still be at origin — start from the park bay.
            if (vehicle.position.sqrMagnitude < 0.01f)
                vehicle.position = parkPosition;

            var previous = vehicle.position;
            var speed = (active ? 7.5f : 5.5f) * (_paused ? 0f : _speed);
            vehicle.position = Vector3.MoveTowards(previous, target, Time.unscaledDeltaTime * speed);
            var travel = Vector3.Distance(previous, vehicle.position);
            if (travel > 0.001f)
            {
                var flat = target - previous;
                flat.y = 0f;
                if (flat.sqrMagnitude > 0.0001f)
                {
                    var look = Quaternion.LookRotation(flat.normalized, Vector3.up);
                    vehicle.rotation = Quaternion.Slerp(vehicle.rotation, look, Time.unscaledDeltaTime * 4f * Mathf.Max(1, _speed));
                }
            }

            if (!active && Vector3.Distance(vehicle.position, parkPosition) < 0.05f)
                ResetServiceLoopParts(vehicle);

            // Presentation-only: wheels roll while the vehicle is moving (ANM-VEH rates).
            var spinRpm = active
                ? AirsideReusableMotion.VehicleWheelRpmTaxi
                : AirsideReusableMotion.VehicleWheelRpmService;
            var degrees = travel * 120f + (travel > 0.001f ? Time.unscaledDeltaTime * spinRpm * Mathf.Max(1, _speed) : 0f);
            if (degrees <= 0f)
                return;
            foreach (var child in AirsideNamedChildren.Get(vehicle))
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
            foreach (var child in AirsideNamedChildren.Get(vehicle))
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
                    SetRendererColor(renderer, color, color * (on ? 2.2f : 0.2f));
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
            foreach (var child in AirsideNamedChildren.Get(vehicle))
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

                // Authored GSE kits are oriented at build (OrientPlusXKitToForward) so
                // SpotLights aim along vehicle +Z with the travel LookRotation — do not
                // re-force a +X kit offset every frame.
                light.transform.localRotation = Quaternion.identity;
                light.enabled = on;
                if (on)
                    light.intensity = night ? 2.8f : 1.1f;
            }
        }

        private static void EnsureVehicleHeadlightMeshes(Transform vehicle)
        {
            var has = false;
            foreach (var child in AirsideNamedChildren.Get(vehicle))
            {
                if (child != vehicle && child.name.IndexOf("Headlight", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    has = true;
                    break;
                }
            }

            if (has)
                return;

            // Fallback lamps sit on the forward bumper (+Z after OrientPlusXKitToForward).
            ParentBlock(vehicle, "Headlight L", new Vector3(-0.4f, 0.55f, 1.85f),
                new Vector3(0.12f, 0.1f, 0.12f), new Color(0.95f, 0.92f, 0.75f));
            ParentBlock(vehicle, "Headlight R", new Vector3(0.4f, 0.55f, 1.85f),
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

            foreach (var child in AirsideNamedChildren.Get(vehicle))
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

            foreach (var child in AirsideNamedChildren.Get(vehicle))
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
                var daylight = PresentationDaylight;
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = Color.Lerp(fogNight, fogDay, Mathf.Max(daylight, 0.25f));
                var baseDensity = AirsideBareField.Enabled
                    ? Mathf.Lerp(0.00032f, 0.0002f, daylight)
                    : Mathf.Lerp(0.0065f, 0.0032f, daylight);
                // Adverse fog kept readable — thick enough to read FG/TSRA, not opaque.
                RenderSettings.fogDensity = AirsideBareField.Enabled
                    ? (weather == WeatherKind.Storm
                        ? Mathf.Max(baseDensity, 0.00045f)
                        : weather == WeatherKind.Fog
                            ? Mathf.Max(baseDensity, 0.00038f)
                            : raining
                                ? Mathf.Max(baseDensity, 0.00028f)
                                : baseDensity)
                    : (weather == WeatherKind.Storm
                        ? Mathf.Max(baseDensity, 0.014f)
                        : weather == WeatherKind.Fog
                            ? Mathf.Max(baseDensity, 0.011f)
                            : raining
                                ? Mathf.Max(baseDensity, 0.0075f)
                                : baseDensity);
            }
            // Clear weather keeps the soft day fog applied in ApplyDayCycle.

            // Darken + gloss paved surfaces when wet (VFX-004 / material wet variants).
            // Fog alone thickens atmosphere — it does not soak the apron.
            // Clear weather keeps a soft residual damp on paved slabs (REF day apron).
            var rainWetness = raining
                ? (weather == WeatherKind.Storm ? 0.72f : 0.52f)
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
                    var (material, dry, drySmooth, dryMetallic, dryBump, paved) = _wetSurfaces[i];
                    if (material == null)
                        continue;
                    // Clear residual damp reads on overview like the turnaround dusk board.
                    var apply = raining ? rainWetness : (paved ? 0.14f : 0f);
                    AirsideMaterialLibrary.ApplyWetness(
                        material, apply, dry, drySmooth, dryMetallic, dryBump,
                        preferWetConcreteAlbedo: paved);
                }
            }

            UpdateWetPuddles(rainWetness, storm);
            UpdateTaxiSpray(rainWetness, raining || storm);

            // Refresh apron probe when wetness or dusk shifts so Lit pavement picks up floods.
            if (_apronProbe != null && Time.unscaledTime >= _apronProbeRefreshAt)
            {
                _apronProbe.intensity = Mathf.Lerp(0.75f, 1.15f, rainWetness);
                MaybeRefreshApronProbe(PresentationDaylight, rainWetness);
                _apronProbeRefreshAt = Time.unscaledTime + 30f;
            }
        }

        private void UpdateTaxiSpray(float wetness, bool raining)
        {
            if (_taxiSprayRoot == null)
                return;

            Transform lead = null;
            for (var i = 0; i < _simulation.Flights.Count; i++)
            {
                var flight = _simulation.Flights[i];
                var phase = flight.Operation.Phase;
                var progress = VisualPhaseProgress(flight, 0f);
                // Ground spray only — not climbing takeoff or airborne approach.
                var onGround = phase is AircraftPhase.TaxiIn or AircraftPhase.TaxiOut or AircraftPhase.Pushback
                    || phase == AircraftPhase.Landing
                    || (phase == AircraftPhase.Takeoff && progress < AirsideFlightPath.RotateProgress);
                if (!onGround)
                    continue;
                if (_commercialAircraft == null || i >= _commercialAircraft.Length)
                    continue;
                lead = _commercialAircraft[i];
                if (lead != null)
                    break;
            }

            var show = wetness > 0.12f && lead != null;
            _taxiSprayRoot.gameObject.SetActive(show);
            if (!show)
                return;

            _taxiSprayRoot.position = lead.position + Vector3.up * 0.2f;
            _taxiSprayRoot.rotation = lead.rotation;
            var n = _taxiSprayRoot.childCount;
            if (_taxiSprayRenderers == null || _taxiSprayRenderers.Length != n)
            {
                _taxiSprayRenderers = new Renderer[n];
                for (var i = 0; i < n; i++)
                    _taxiSprayRenderers[i] = _taxiSprayRoot.GetChild(i).GetComponent<Renderer>();
            }

            for (var i = 0; i < n; i++)
            {
                var puff = _taxiSprayRoot.GetChild(i);
                var pulse = 0.7f + 0.3f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * (6f + i) + i));
                var side = i % 2 == 0 ? -0.65f : 0.65f;
                puff.localPosition = new Vector3(side, 0.08f + pulse * 0.12f, -0.4f - i * 0.15f);
                puff.localScale = new Vector3(0.55f, 0.25f, 0.55f) * pulse * (raining ? 1.25f : 1f);
                var renderer = _taxiSprayRenderers[i];
                if (renderer != null)
                {
                    var color = GetRendererColor(renderer);
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
            if (_puddleRenderers == null)
            {
                var list = new List<Renderer>(16);
                for (var i = 0; i < _wetPuddleRoot.childCount; i++)
                {
                    var cluster = _wetPuddleRoot.GetChild(i);
                    for (var b = 0; b < cluster.childCount; b++)
                    {
                        var renderer = cluster.GetChild(b).GetComponent<Renderer>();
                        if (renderer != null)
                            list.Add(renderer);
                    }
                }

                _puddleRenderers = list.ToArray();
            }

            for (var i = 0; i < _puddleRenderers.Length; i++)
            {
                var renderer = _puddleRenderers[i];
                if (renderer == null)
                    continue;
                var color = GetRendererColor(renderer);
                color.a = alpha * (0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 0.7f + i * 0.4f));
                SetRendererColor(renderer, color);
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
                    && VisualPhaseProgress(flight, 0f) >= AirsideFlightPath.TouchdownProgress)
                {
                    _touchdownFired.Add(id);
                    _touchdownSmoke.position = _commercialAircraft[index].position + Vector3.up * 0.15f;
                    _touchdownSmoke.rotation = _commercialAircraft[index].rotation;
                    _touchdownSmoke.localScale = Vector3.one * 1.35f;
                    for (var p = 0; p < _touchdownSmoke.childCount; p++)
                    {
                        var puff = _touchdownSmoke.GetChild(p);
                        var side = p % 2 == 0
                            ? -AirsideReusableMotion.MainGearHalfTrackMetres
                            : AirsideReusableMotion.MainGearHalfTrackMetres;
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

                // Soft rotate cue once the visual path lifts — presentation only.
                if (phase == AircraftPhase.Takeoff
                    && index < _commercialAircraft.Length
                    && !_rotateFired.Contains(id)
                    && VisualPhaseProgress(flight, 0f) >= AirsideFlightPath.RotateProgress)
                {
                    _rotateFired.Add(id);
                    if (_touchdownAudio != null && _rotateClip != null && !_audioMuted)
                    {
                        _touchdownAudio.transform.position = _commercialAircraft[index].position;
                        _touchdownAudio.PlayOneShot(_rotateClip, 0.22f);
                    }
                }
                else if (phase != AircraftPhase.Takeoff)
                {
                    _rotateFired.Remove(id);
                }

                _previousPhases[id] = phase;
            }

            if (_touchdownSmokeRemaining <= 0f)
            {
                _touchdownSmoke.gameObject.SetActive(false);
            }
            else if (!_paused)
            {
                // Freeze the puff while paused; at 4× it still ages in real time so the
                // one-shot stays short rather than stretching across the whole rollout.
                _touchdownSmokeRemaining -= Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(_touchdownSmokeRemaining / 1.35f);
                var n = _touchdownSmoke.childCount;
                if (_touchdownSmokeRenderers == null || _touchdownSmokeRenderers.Length != n)
                {
                    _touchdownSmokeRenderers = new Renderer[n];
                    for (var i = 0; i < n; i++)
                        _touchdownSmokeRenderers[i] = _touchdownSmoke.GetChild(i).GetComponent<Renderer>();
                }

                for (var i = 0; i < n; i++)
                {
                    var puff = _touchdownSmoke.GetChild(i);
                    puff.localScale = Vector3.Lerp(new Vector3(2.8f, 0.25f, 2.8f), new Vector3(1.0f, 0.35f, 1.0f), t);
                    puff.localPosition += Vector3.up * (Time.unscaledDeltaTime * 0.35f);
                    var renderer = _touchdownSmokeRenderers[i];
                    if (renderer != null)
                    {
                        var color = GetRendererColor(renderer);
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
                var side = i == 0 ? -2.05f : 2.05f;
                mark.transform.position = aircraft.position
                    + aircraft.right * side
                    + aircraft.forward * -0.4f
                    + Vector3.up * 0.04f;
                var fwd = Vector3.ProjectOnPlane(aircraft.forward, Vector3.up);
                if (fwd.sqrMagnitude < 0.0001f)
                    fwd = Vector3.forward;
                mark.transform.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);
                mark.transform.localScale = new Vector3(0.22f, 0.02f, 3.6f);
                var markRenderer = mark.GetComponent<Renderer>();
                markRenderer.sharedMaterial = CreateMaterial(new Color(0.12f, 0.11f, 0.1f, 0.7f));
                SetRendererColor(markRenderer, new Color(0.12f, 0.11f, 0.1f, 0.7f));
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

                var color = GetRendererColor(renderer);
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

        private void CollectHoldShortMarkings(Renderer[] renderers = null)
        {
            _holdShortRenderers.Clear();
            // Prefix scan — A1/A2 fillet bars and kit meshes rename often; exact lists go stale.
            renderers ??= AirsideSceneIndex.Renderers;
            foreach (var renderer in renderers)
            {
                if (renderer == null)
                    continue;
                var n = renderer.gameObject.name;
                if (n.StartsWith("Hold short", StringComparison.Ordinal)
                    || n.StartsWith("hold_short", StringComparison.Ordinal))
                {
                    if (!_holdShortRenderers.Contains(renderer))
                        _holdShortRenderers.Add(renderer);
                }
            }
        }

        private void UpdateTrafficWaitPresentation()
        {
            // Presentation-only: pulse hold-short bars when a traffic wait is active
            // or tower has issued hold / line-up / number-two.
            var atcHot = _simulation.Atc.ActiveClearance is AtcClearance.HoldShortRunway
                or AtcClearance.LineUpAndWait
                or AtcClearance.NumberTwoLanding
                or AtcClearance.NumberTwoDeparture
                or AtcClearance.ReadyForDeparture
                or AtcClearance.GroundHold
                or AtcClearance.HoldApron
                or AtcClearance.TrafficAdvisory
                or AtcClearance.ConditionalLanding
                or AtcClearance.OrbitLeft
                or AtcClearance.GoAround
                or AtcClearance.HoldShortAlpha
                or AtcClearance.MinimumApproachSpeed
                or AtcClearance.GiveWayTaxiing;
            var warning = _simulation.TrafficWaits.HasWarning(_clock.Now) || atcHot;
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
                var emit = warning
                    ? new Color(1f, 0.4f, 0.08f) * (0.35f + pulse * 1.4f)
                    : Color.black;
                SetRendererColor(renderer, color, emit);
            }
        }


        /// <summary>
        /// True for paved / painted surfaces that keep a residual damp sheen in clear
        /// weather. Constant for the lifetime of a renderer, so it is resolved once in
        /// <see cref="CollectWetSurfaces"/> rather than re-tested every frame.
        /// </summary>
        private static bool IsPavedSurfaceName(string name)
        {
            return (name is "Hangar taxi link"
                    or "Service lane link W" or "Service lane link E" or "Fuel pad link"
                    or "ARFF apron link"
                    or "Access road stub" or "Access road elbow")
                || name.StartsWith("Service lane", StringComparison.Ordinal)
                || name.StartsWith("Fuel pad", StringComparison.Ordinal)
                || name.StartsWith("Access road", StringComparison.Ordinal)
                || name.StartsWith("Car park aisle", StringComparison.Ordinal)
                || name.StartsWith("Stand 3 apron", StringComparison.Ordinal)
                || name.StartsWith("Car park bay", StringComparison.Ordinal)
                || name.StartsWith("Runway 05", StringComparison.Ordinal) || name.StartsWith("Runway 12", StringComparison.Ordinal) || name.StartsWith("Taxiway ", StringComparison.Ordinal)
                || name.StartsWith("Pavement fillet", StringComparison.Ordinal)
                || name.StartsWith("Runway E", StringComparison.Ordinal)
                || name.StartsWith("Runway mid", StringComparison.Ordinal)
                || name.StartsWith("Runway blast", StringComparison.Ordinal)
                || name.StartsWith("Apron ", StringComparison.Ordinal)
                || name.StartsWith("Apron joint", StringComparison.Ordinal)
                || name.StartsWith("Apron slab", StringComparison.Ordinal)
                || name.StartsWith("Apron fringe", StringComparison.Ordinal)
                || name.StartsWith("Apron corner", StringComparison.Ordinal)
                || name.StartsWith("Runway marking", StringComparison.Ordinal)
                || name.StartsWith("Runway edge", StringComparison.Ordinal)
                || name.StartsWith("Threshold", StringComparison.Ordinal)
                || name.StartsWith("Hold short", StringComparison.Ordinal)
                || name.StartsWith("Taxi edge", StringComparison.Ordinal)
                || name.StartsWith("Taxi lead", StringComparison.Ordinal)
                || name.StartsWith("Taxiway A", StringComparison.Ordinal)
                || name.StartsWith("Hangar apron", StringComparison.Ordinal)
                || name.StartsWith("Stand stop", StringComparison.Ordinal)
                || name.StartsWith("Stand number", StringComparison.Ordinal)
                || name.StartsWith("Access turn", StringComparison.Ordinal)
                || name.StartsWith("Access centreline", StringComparison.Ordinal)
                || name.StartsWith("Access edge", StringComparison.Ordinal)
                || name.StartsWith("Taxi exit centre", StringComparison.Ordinal)
                || name.StartsWith("Drop-off zebra", StringComparison.Ordinal)
                || name.StartsWith("Overflow bay", StringComparison.Ordinal)
                || name.StartsWith("Bay line", StringComparison.Ordinal)
                || name.StartsWith("Stall line", StringComparison.Ordinal)
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

        private void CollectWetSurfaces(Renderer[] renderers = null)
        {
            _wetSurfaces.Clear();
            // New surfaces have never been wetted — force the next weather pass to apply.
            _lastAppliedWetness = float.NaN;
            var seenMaterials = new HashSet<int>();
            renderers ??= AirsideSceneIndex.Renderers;
            foreach (var renderer in renderers)
            {
                if (renderer == null)
                    continue;
                var n = renderer.gameObject.name;
                if (!(n is "Hangar taxi link"
                        or "Service lane link W" or "Service lane link E" or "Fuel pad link"
                        or "ARFF apron link"
                        or "Access road stub" or "Access road elbow")
                    && !n.StartsWith("Service lane", StringComparison.Ordinal)
                    && !n.StartsWith("Fuel pad", StringComparison.Ordinal)
                    && !n.StartsWith("Access road", StringComparison.Ordinal)
                    && !n.StartsWith("Car park aisle", StringComparison.Ordinal)
                    && !n.StartsWith("Stand 3 apron", StringComparison.Ordinal)
                    && !n.StartsWith("Car park bay", StringComparison.Ordinal)
                    && !n.StartsWith("Runway 05", StringComparison.Ordinal) && !n.StartsWith("Runway 12", StringComparison.Ordinal) && !n.StartsWith("Taxiway ", StringComparison.Ordinal)
                    && !n.StartsWith("Pavement fillet", StringComparison.Ordinal)
                    && !n.StartsWith("Runway E", StringComparison.Ordinal)
                    && !n.StartsWith("Runway mid", StringComparison.Ordinal)
                    && !n.StartsWith("Runway blast", StringComparison.Ordinal)
                    && !n.StartsWith("Apron ", StringComparison.Ordinal)
                    && !n.StartsWith("Grass", StringComparison.Ordinal)
                    && !n.StartsWith("Infield grass", StringComparison.Ordinal)
                    && !n.StartsWith("Relief berm", StringComparison.Ordinal)
                    && !n.StartsWith("Coast ", StringComparison.Ordinal)
                    && !n.StartsWith("Outer paddock", StringComparison.Ordinal)
                    && !n.StartsWith("Outer horizon", StringComparison.Ordinal)
                    && !n.StartsWith("Coast dune", StringComparison.Ordinal)
                    && !n.StartsWith("Hill far", StringComparison.Ordinal)
                    && !n.StartsWith("Car park kerb", StringComparison.Ordinal)
                    && !n.StartsWith("Coast scrub", StringComparison.Ordinal)
                    && !n.StartsWith("Apron joint", StringComparison.Ordinal)
                    && !n.StartsWith("Apron fringe", StringComparison.Ordinal)
                    && !n.StartsWith("Apron slab", StringComparison.Ordinal)
                    && !n.StartsWith("Taxi lead", StringComparison.Ordinal)
                    && !n.StartsWith("Taxiway A", StringComparison.Ordinal)
                    && !n.StartsWith("Hangar apron", StringComparison.Ordinal)
                    && !n.StartsWith("Runway marking", StringComparison.Ordinal)
                    && !n.StartsWith("Runway edge", StringComparison.Ordinal)
                    && !n.StartsWith("Threshold", StringComparison.Ordinal)
                    && !n.StartsWith("Hold short", StringComparison.Ordinal)
                    && !n.StartsWith("Taxi edge", StringComparison.Ordinal)
                    && !n.StartsWith("Stand stop", StringComparison.Ordinal)
                    && !n.StartsWith("Stand number", StringComparison.Ordinal)
                    && !n.StartsWith("Access turn", StringComparison.Ordinal)
                    && !n.StartsWith("Access centreline", StringComparison.Ordinal)
                    && !n.StartsWith("Access edge", StringComparison.Ordinal)
                    && !n.StartsWith("Taxi exit centre", StringComparison.Ordinal)
                    && !n.StartsWith("Drop-off zebra", StringComparison.Ordinal)
                    && !n.StartsWith("Overflow bay", StringComparison.Ordinal)
                    && !n.StartsWith("Bay line", StringComparison.Ordinal)
                    && !n.StartsWith("Stall line", StringComparison.Ordinal)
                    && !n.StartsWith("Taxi arrow", StringComparison.Ordinal)
                    && !n.StartsWith("Alpha edge", StringComparison.Ordinal)
                    && !n.StartsWith("Alpha centre", StringComparison.Ordinal)
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

                var mat = renderer.sharedMaterial;
                if (mat == null)
                    continue;
                var materialId = mat.GetInstanceID();
                if (!seenMaterials.Add(materialId))
                    continue;
                var drySmooth = 0.28f;
                if (mat.HasProperty("_Smoothness"))
                    drySmooth = mat.GetFloat("_Smoothness");
                else if (mat.HasProperty("_Glossiness"))
                    drySmooth = mat.GetFloat("_Glossiness");
                var dryMetallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0.02f;
                var dryBump = mat.HasProperty("_BumpScale") ? mat.GetFloat("_BumpScale") : 0.5f;
                _wetSurfaces.Add((mat, mat.color, drySmooth, dryMetallic, dryBump,
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
                    var puddleRenderer = puddle.GetComponent<Renderer>();
                    puddleRenderer.sharedMaterial = AirsideMaterialLibrary.CreateShared(
                        new Color(0.2f, 0.28f, 0.34f, 0.28f),
                        AirsideMaterialLibrary.SurfaceKind.Water);
                    SetRendererColor(puddleRenderer, new Color(0.2f, 0.28f, 0.34f, 0.28f));
                    puddleRenderer.GetPropertyBlock(RendererTintBlock);
                    RendererTintBlock.SetFloat("_Smoothness", 0.96f);
                    RendererTintBlock.SetFloat("_Metallic", 0.35f);
                    puddleRenderer.SetPropertyBlock(RendererTintBlock);
                    puddleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }

            root.gameObject.SetActive(false);
        }

        private void CollectAirfieldLights(Renderer[] renderers = null)
        {
            _airfieldLightRenderers.Clear();
            renderers ??= AirsideSceneIndex.Renderers;
            foreach (var renderer in renderers)
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
            var dropCount = AirsideRuntimeQuality.RainDropCount(seed != null);
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
                    drop.GetComponent<Renderer>().sharedMaterial = AirsideMaterialLibrary.CreateShared(
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
                var smokeRenderer = smoke.GetComponent<Renderer>();
                smokeRenderer.sharedMaterial = CreateMaterial(new Color(0.85f, 0.85f, 0.88f, 0.4f));
                SetRendererColor(smokeRenderer, new Color(0.85f, 0.85f, 0.88f, 0.4f));
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
                var puffRenderer = puff.GetComponent<Renderer>();
                puffRenderer.sharedMaterial = CreateMaterial(new Color(0.75f, 0.8f, 0.85f, 0.25f));
                SetRendererColor(puffRenderer, new Color(0.75f, 0.8f, 0.85f, 0.25f));
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
            var opsHeight = 80f + listedRoutes * 18f + 4f + 2 * 18f + 36f + 6f + 4 * 20f + 16f;
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

            if (!string.IsNullOrEmpty(_simulation.Atc.LastInstruction))
            {
                GUI.Label(new Rect(historyLeft + 20, trafficY, 310, 36), $"ATC · {_simulation.Atc.LastInstruction}", small);
                trafficY += 36f;
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
            camera.fieldOfView = AirsideBareField.OverviewFov;
            // The Adelaide ground is 3.4 km on the long axis; the far plane has to
            // cover the whole site from the high overview.
            camera.farClipPlane = Mathf.Max(camera.farClipPlane, AirsideBareField.CameraFarClip);
            camera.clearFlags = CameraClearFlags.SolidColor;
            _mainCamera = camera;

            _cameraController = camera.GetComponent<AirsideCameraController>();
            if (_cameraController == null)
                _cameraController = camera.gameObject.AddComponent<AirsideCameraController>();

            if (camera.GetComponent<AudioListener>() == null)
                camera.gameObject.AddComponent<AudioListener>();

            _sun = FindPreferredSunLight();
            if (_sun == null)
                _sun = new GameObject("Sun").AddComponent<Light>();
            _sun.type = LightType.Directional;
            _sun.shadows = LightShadows.Soft;
            _sun.shadowStrength = 0.78f;
            _sun.shadowBias = 0.035f;
            _sun.shadowNormalBias = 0.4f;

            // Cool fill opposite the key — softens night and dawn without a full probe bake.
            var fillGo = FindBuilt("Fill light");
            _fillLight = fillGo != null ? fillGo.GetComponent<Light>() : null;
            if (_fillLight == null)
                _fillLight = new GameObject("Fill light").AddComponent<Light>();
            _fillLight.type = LightType.Directional;
            _fillLight.shadows = LightShadows.None;
            _fillLight.intensity = 0.25f;
            _fillLight.color = new Color(0.45f, 0.55f, 0.75f);

            ApplyDayCycle();
        }

        /// <summary>Prefer a Light named "Sun", else an existing DirectionalLight — not a random Spot.</summary>
        private static Light FindPreferredSunLight()
        {
            var named = FindBuilt("Sun") ?? FindBuilt("Directional Light");
            return named != null ? named.GetComponent<Light>() : null;
        }

        private void ApplyDayCycle()
        {
            var cycle = _simulation.TimeOfDay;
            var daylight = PresentationDaylight;

            var elevation = PinDaylightPresentation ? 48f : (float)cycle.SunElevationDegrees;
            _sun.transform.rotation = PinDaylightPresentation
                ? Quaternion.Euler(48f, -28f, 0f)
                : Quaternion.Euler(Mathf.Max(-6f, elevation), -28f - (float)cycle.Fraction * 90f, 0f);

            // Warm key, cool fill — day must read bright coastal sun; night must yield to
            // apron floods so the airfield silhouette stays obvious from overview.
            var day = new Color(1f, 0.96f, 0.88f);
            var goldenHour = new Color(1f, 0.68f, 0.42f);
            var night = new Color(0.32f, 0.38f, 0.55f);
            var warm = PinDaylightPresentation
                ? 0f
                : Mathf.Clamp01(Mathf.Min(daylight, 1f - daylight) * 2.6f); // dawn/dusk only
            _sun.color = Color.Lerp(Color.Lerp(night, day, daylight), goldenHour, warm * Mathf.Max(daylight, 0.12f));
            // Noon punch; night key stays dim so flood pools (not a blue wash) light the apron.
            _sun.intensity = Mathf.Lerp(0.12f, 2.05f, Mathf.SmoothStep(0f, 1f, daylight));
            _sun.shadowStrength = Mathf.Lerp(0.28f, 0.78f, daylight);

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
                // Cool day fill opens shadows; night fill is soft blue-grey form light only.
                _fillLight.color = Color.Lerp(
                    new Color(0.28f, 0.34f, 0.52f),
                    Color.Lerp(new Color(0.62f, 0.72f, 0.9f), new Color(1f, 0.82f, 0.68f), warm * 0.45f),
                    daylight);
                _fillLight.intensity = Mathf.Lerp(0.38f, 0.22f, daylight) + warm * 0.05f;
            }

            // Trilight: day = bright cool sky / warm ground separation; night = deep blue-grey
            // that still lets hangar/terminal silhouettes read outside flood pools.
            var ambientDay = new Color(0.58f, 0.64f, 0.72f);
            var ambientDusk = new Color(0.52f, 0.36f, 0.3f);
            var ambientNight = new Color(0.12f, 0.14f, 0.22f);
            var ambientSky = Color.Lerp(Color.Lerp(ambientNight, ambientDay, daylight), ambientDusk, warm * 0.55f);
            var ambientEquator = Color.Lerp(
                new Color(0.16f, 0.18f, 0.26f),
                Color.Lerp(new Color(0.46f, 0.5f, 0.52f), new Color(0.5f, 0.38f, 0.32f), warm),
                daylight);
            var ambientGround = Color.Lerp(
                new Color(0.08f, 0.09f, 0.11f),
                Color.Lerp(new Color(0.26f, 0.28f, 0.22f), new Color(0.3f, 0.2f, 0.15f), warm),
                daylight);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ambientSky;
            RenderSettings.ambientEquatorColor = ambientEquator;
            RenderSettings.ambientGroundColor = ambientGround;
            RenderSettings.ambientIntensity = Mathf.Lerp(0.72f, 1.12f, daylight) + warm * 0.06f;
            if (weatherGloom > 0f)
            {
                // Dim trilight under fog/rain/storm — ambientLight is ignored in Trilight mode.
                ambientSky = Color.Lerp(ambientSky, ambientSky * 0.72f, weatherGloom);
                ambientEquator = Color.Lerp(ambientEquator, ambientEquator * 0.7f, weatherGloom);
                ambientGround = Color.Lerp(ambientGround, ambientGround * 0.65f, weatherGloom);
                RenderSettings.ambientSkyColor = ambientSky;
                RenderSettings.ambientEquatorColor = ambientEquator;
                RenderSettings.ambientGroundColor = ambientGround;
                RenderSettings.ambientIntensity *= Mathf.Lerp(1f, 0.78f, weatherGloom);
            }
            RenderSettings.subtractiveShadowColor = Color.Lerp(
                new Color(0.18f, 0.24f, 0.36f),
                new Color(0.38f, 0.28f, 0.26f),
                warm);

            // REF-001 coastal day sky (clear blue, not grey mush); dusk warmth stays on the
            // horizon without orange-fogging the whole overview (art direction).
            var skyDay = new Color(0.55f, 0.68f, 0.82f);
            var skyDusk = new Color(0.62f, 0.38f, 0.3f);
            var skyNight = new Color(0.04f, 0.055f, 0.1f);
            var sky = Color.Lerp(Color.Lerp(skyNight, skyDay, daylight), skyDusk, warm * 0.55f);
            if (_mainCamera != null)
                _mainCamera.backgroundColor = sky;
            if (_horizonDome != null)
            {
                if (_horizonDomeRenderer == null)
                    _horizonDomeRenderer = _horizonDome.GetComponent<Renderer>();
                if (_horizonDomeRenderer != null)
                    SetRendererColor(_horizonDomeRenderer, sky, sky * Mathf.Lerp(0.35f, 1f, daylight));
            }

            UpdateSunAndMoonDiscs(daylight, warm, elevation);

            // Soft depth fog only — thick enough for far hills, thin enough that runway,
            // apron and buildings stay obvious from the default overview.
            if (!Weather.IsAdverse(_simulation.CurrentWeather))
            {
                var cloudy = _simulation.CurrentWeather == WeatherKind.Cloudy;
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                var clearFog = Color.Lerp(
                    new Color(0.06f, 0.08f, 0.14f),
                    Color.Lerp(skyDay * 0.95f, new Color(0.7f, 0.55f, 0.48f), warm * 0.45f),
                    Mathf.Clamp01(daylight + warm * 0.15f));
                if (cloudy)
                    clearFog = Color.Lerp(clearFog, new Color(0.58f, 0.62f, 0.68f), 0.22f);
                RenderSettings.fogColor = clearFog;
                var density = AirsideBareField.Enabled
                    ? Mathf.Lerp(AirsideBareField.NightFogDensity, AirsideBareField.DayFogDensity, daylight)
                    : Mathf.Lerp(0.0036f, 0.0016f, daylight);
                if (cloudy && !AirsideBareField.Enabled)
                    density = Mathf.Max(density, Mathf.Lerp(0.005f, 0.0028f, daylight));
                // Tiny dusk haze only — do not orange-wash the whole scene.
                density += AirsideBareField.Enabled ? warm * 0.00002f : warm * 0.00035f;
                RenderSettings.fogDensity = density;
            }

            // Apron floods come up as daylight falls (presentation only).
            if (_apronLights != null)
            {
                // Warm night pools so REF-002 apron reads; day floods stay off.
                var flood = Mathf.Lerp(3.6f, 0.04f, Mathf.SmoothStep(0f, 1f, daylight));
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
                _apronProbe.intensity = Mathf.Lerp(1.15f, 0.85f, daylight);
                MaybeRefreshApronProbe(daylight, 0f);
            }

            if (_terminalProbe != null)
                _terminalProbe.intensity = Mathf.Lerp(1.05f, 0.8f, daylight);

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
                SetRendererColor(renderer, color, baseColor * (0.2f + night * 1.4f));
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
                         "Terminal canopy glow W",
                         "Terminal canopy glow E",
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
                var go = AirsideSceneIndex.FindGameObject(name);
                if (go == null)
                    continue;
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null && !_nightGlowRenderers.Contains(renderer))
                    _nightGlowRenderers.Add(renderer);

                var wantsPoint = AirsideRuntimeQuality.WindowPointLights
                    && !(name.StartsWith("glass_pane", StringComparison.Ordinal)
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

            var paneLights = 0;
            var maxPaneLights = AirsideRuntimeQuality.PanePointLights;
            foreach (var renderer in AirsideSceneIndex.Renderers)
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
            UpdateNightGlow(PresentationDaylight);
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
                SetRendererColor(renderer, color, emission);
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
                var truck = AirsideSceneIndex.Find("ARFF truck");
                if (truck != null)
                {
                    var bar = AirsideNamedChildren.FindContains(truck, "lightbar");
                    if (bar != null)
                        _arffLightbarRenderer = bar.GetComponent<Renderer>();
                }
            }

            if (_arffLightbarRenderer == null)
                return;

            var night = 1f - daylight;
            var blink = 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(
                Time.unscaledTime * AirsideReusableMotion.ArffLightbarHz * Mathf.PI * 2f));
            var amber = new Color(1f, 0.35f, 0.12f) * (0.15f + night * 1.8f * blink);
            SetRendererColor(_arffLightbarRenderer, Color.Lerp(new Color(0.95f, 0.85f, 0.2f), amber, night), amber);
        }

        private static Light[] CollectAlsLights()
        {
            var lights = new List<Light>();
            for (var i = 0; i < 8; i++)
            {
                var light = AirsideSceneIndex.FindLight($"ALS lamp {i}");
                if (light != null)
                    lights.Add(light);
            }

            foreach (var name in new[] { "REIL lamp L", "REIL lamp R" })
            {
                var light = AirsideSceneIndex.FindLight(name);
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
            var lights = new Light[AirsideRuntimeQuality.ApronFloodCount(specs.Length)];
            for (var i = 0; i < lights.Length; i++)
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
            var edgeStep = AirsideRuntimeQuality.EdgeLightStep;
            if (hasLightingKit)
                edgeStep = Mathf.Max(edgeStep, 10);
            var runwayEdge = (int)AirportLayout.RunwayHalfLength - 2;
            for (var x = -runwayEdge; x <= runwayEdge; x += edgeStep)
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
                    if (!ArtGltfLoader.TryPlaceCombined(
                            lightingKit,
                            new[]
                            {
                                ("taxi_base", taxiStem),
                                ("taxi_stem", taxiStem),
                                ("taxi_lens", taxiLens)
                            },
                            origin, Quaternion.identity, $"Taxi fixture {x}", out _))
                    {
                        CreateBlock($"Taxi fixture {x}", new Vector3(x, 0.2f, 9f), new Vector3(0.18f, 0.35f, 0.18f), taxiStem);
                    }
                }
            }
            else if (AirsideRuntimeQuality.Current == AirsideRuntimeQuality.Ladder.High)
            {
                // Sparse taxi spill along Taxiway A so night taxi still reads without fixture glitter.
                for (var x = -8; x <= 24; x += 16)
                {
                    lights.Add(CreateEdgePointLight($"Taxi point {x}", new Vector3(x, 0.45f, 9f),
                        new Color(0.3f, 0.55f, 1f), range: 9f));
                }
            }

            // Always light the A1 runway exit fillet — kit thinning used to leave it dark.
            var fillet = AirsideRuntimeQuality.FilletLightCount;
            if (fillet >= 3)
            {
                lights.Add(CreateEdgePointLight("Taxi A1 point W", new Vector3(-22f, 0.45f, 2.2f),
                    new Color(0.3f, 0.55f, 1f), range: 8f));
            }

            lights.Add(CreateEdgePointLight("Taxi A1 point M", new Vector3(-18f, 0.45f, 4.5f),
                new Color(0.3f, 0.55f, 1f), range: 8f));
            if (fillet >= 3)
            {
                lights.Add(CreateEdgePointLight("Taxi A1 point E", new Vector3(-14f, 0.45f, 7f),
                    new Color(0.3f, 0.55f, 1f), range: 8f));
                lights.Add(CreateEdgePointLight("Taxi A2 point E", new Vector3(26f, 0.45f, 2.2f),
                    new Color(0.3f, 0.55f, 1f), range: 8f));
            }

            lights.Add(CreateEdgePointLight("Taxi A2 point M", new Vector3(22f, 0.45f, 4.5f),
                new Color(0.3f, 0.55f, 1f), range: 8f));
            if (fillet >= 3)
            {
                lights.Add(CreateEdgePointLight("Taxi A2 point W", new Vector3(18f, 0.45f, 7f),
                    new Color(0.3f, 0.55f, 1f), range: 8f));
            }

            // REIL-style white flashers just beyond each blast pad (blinked later).
            lights.Add(CreateEdgePointLight("REIL W L", new Vector3(-54f, 1.6f, -2.8f),
                new Color(1f, 1f, 0.95f), range: 16f));
            lights.Add(CreateEdgePointLight("REIL W R", new Vector3(-54f, 1.6f, 2.8f),
                new Color(1f, 1f, 0.95f), range: 16f));
            lights.Add(CreateEdgePointLight("REIL E L", new Vector3(54f, 1.6f, -2.8f),
                new Color(1f, 1f, 0.95f), range: 16f));
            lights.Add(CreateEdgePointLight("REIL E R", new Vector3(54f, 1.6f, 2.8f),
                new Color(1f, 1f, 0.95f), range: 16f));
            var taxiStemColor = new Color(0.35f, 0.36f, 0.38f);
            void PlaceReilPost(string name, Vector3 origin)
            {
                if (ArtGltfLoader.TryPlaceCombined(
                        lightingKit,
                        new[]
                        {
                            ("obst_base", taxiStemColor),
                            ("obst_stem", taxiStemColor),
                            ("obst_lens", new Color(1f, 1f, 0.9f))
                        },
                        origin, Quaternion.identity, name, out _))
                    return;
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
            AirsideSceneIndex.Remember(go);
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

            var lights = new Light[AirsideRuntimeQuality.ThresholdLightCount(specs.Length)];
            var lightingKit = PreferArtKit(
                "Models/Props/mdl_airfield_lighting_kit_authored_v01.gltf",
                "Models/Props/mdl_airfield_lighting_kit_v02.gltf",
                "Models/Props/mdl_airfield_lighting_kit_v01.gltf");
            var stem = new Color(0.35f, 0.36f, 0.38f);
            for (var i = 0; i < lights.Length; i++)
            {
                var spec = specs[i];
                var origin = new Vector3(spec.Pos.x, 0f, spec.Pos.z);
                if (!ArtGltfLoader.TryPlaceCombined(
                        lightingKit,
                        new[]
                        {
                            ("edge_stem", stem),
                            ("edge_lens", spec.Color),
                            ("taxi_lens", spec.Color)
                        },
                        origin, Quaternion.identity, $"Threshold lamp {i}", out _))
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
        private void MaybeRefreshApronProbe(float daylight, float wetness)
        {
            if (_apronProbe == null)
                return;
            var band = AirsideRuntimeQuality.ProbeBand(daylight, wetness);
            if (band == _probeBand)
                return;
            _probeBand = band;
            _apronProbe.RenderProbe();
        }

        private static ReflectionProbe BuildApronReflectionProbe()
        {
            var go = new GameObject("Apron reflection probe");
            go.transform.position = new Vector3(20f, 3.5f, 17f);
            var probe = go.AddComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.IndividualFaces;
            probe.resolution = AirsideRuntimeQuality.Current == AirsideRuntimeQuality.Ladder.High ? 128 : 64;
            // Cover stand apron + hangar face so authored metal/glass get local floods.
            probe.size = new Vector3(56f, 22f, 42f);
            probe.center = Vector3.zero;
            probe.intensity = 1f;
            probe.boxProjection = true;
            probe.shadowDistance = 28f;
            probe.nearClipPlane = 0.3f;
            probe.farClipPlane = 90f;
            return probe;
        }

        /// <summary>
        /// Decision 0025 item 5 — second realtime probe on the terminal landside so
        /// authored glass / canopy posts catch window spill at dusk.
        /// </summary>
        private static ReflectionProbe BuildTerminalReflectionProbe()
        {
            if (!AirsideRuntimeQuality.UseTerminalProbe)
                return null;
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
            var count = AirsideRuntimeQuality.LandsideLightCount(positions.Length);
            var lights = new Light[count];
            for (var i = 0; i < count; i++)
            {
                var pos = positions[i];
                var kitMast = ArtGltfLoader.TryPlaceCombined(
                    lightingKit,
                    new[]
                    {
                        ("flood_pole", steel),
                        ("flood_head", head),
                        ("flood_lamp", lampColor)
                    },
                    pos, Quaternion.identity, $"Streetlight {i}", out _,
                    landsideMastScale);
                if (!kitMast)
                {
                    CreateBlock($"Streetlight pole {i}", pos + new Vector3(0f, 2.2f, 0f), new Vector3(0.14f, 4.4f, 0.14f), steel);
                    CreateBlock($"Streetlight head {i}", pos + new Vector3(0.35f, 4.35f, 0f), new Vector3(0.7f, 0.18f, 0.35f), head);
                    CreateBlock($"Streetlight lamp {i}", pos + new Vector3(0.55f, 4.2f, 0f), new Vector3(0.28f, 0.16f, 0.28f), lampColor);
                }

                var go = new GameObject($"Landside streetlight {i + 1}");
                // Kit masts are scaled ~0.62f — keep the point light near the shorter head.
                var lightHeight = kitMast ? 2.55f : 4.1f;
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
            var kitMast = ArtGltfLoader.TryPlaceCombined(
                lightingKit,
                new[]
                {
                    ("obst_base", steel),
                    ("obst_stem", steel),
                    ("obst_lens", new Color(0.95f, 0.95f, 0.9f)),
                    ("obst_beacon_ring", new Color(1f, 0.9f, 0.5f))
                },
                origin, Quaternion.identity, "Aerodrome beacon mast", out var kitRoot);
            if (kitRoot != null)
            {
                kitRoot.SetParent(mast, true);
                kitMast = true;
            }

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

            var pulse = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(PresentationClock * (AirsideReusableMotion.BeaconHz * Mathf.PI)));
            _aerodromeBeacon.intensity = pulse * Mathf.Lerp(2.4f, 0.2f, daylight / 0.38f);
            _aerodromeBeacon.color = Mathf.FloorToInt(PresentationClock * AirsideReusableMotion.BeaconHz) % 2 == 0
                ? new Color(0.95f, 0.98f, 1f)
                : new Color(0.35f, 0.95f, 0.55f);
        }

        /// <summary>
        /// Paved lead-in / chord pad following an actual taxi path so aircraft stay on asphalt.
        /// </summary>
        private static void CreateTaxiChordPad(string name, Vector3 from, Vector3 to, float width,
            string textureRelativePath, Vector2 tiling)
        {
            var mid = (from + to) * 0.5f;
            var delta = to - from;
            var length = delta.magnitude + 1.4f;
            var yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            var pad = CreateBlock(name, mid, new Vector3(width, 0.12f, length), new Color(0.22f, 0.24f, 0.26f),
                textureRelativePath, tiling);
            pad.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        /// <summary>
        /// One rotated paint slab instead of a cube-per-metre dash dump.
        /// </summary>
        private static void CreatePaintStrip(string name, Vector3 from, Vector3 to, float width, Color color)
        {
            var mid = (from + to) * 0.5f;
            var delta = to - from;
            var length = Mathf.Max(delta.magnitude, 0.5f);
            var yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            var strip = CreateBlock(name, mid, new Vector3(width, 0.02f, length), color);
            strip.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        /// <summary>
        /// Paved lead-in along the taxi chord from the parallel taxiways to the stand bay.
        /// </summary>
        private static void CreateTaxiLeadPad(string name, float standZ)
        {
            // Dogleg matching AirportTaxiNetwork: apron throat → stand.
            var alpha = new Vector3(AirportLayout.TaxiwayEastX, 0.01f, AirportLayout.TaxiwayAlphaZ);
            var throat = new Vector3(AirportLayout.ApronThroatX, 0.01f, standZ);
            var stand = new Vector3(AirportLayout.StandX, 0.01f, standZ);
            CreateTaxiChordPad($"{name} throat leg", alpha, throat, 3.2f,
                PreferSurfaceBasecolor("tx_asphalt_runway"), new Vector2(1.2f, 1.4f));
            CreateTaxiChordPad($"{name} stand leg", throat, stand, 3.4f,
                PreferSurfaceBasecolor("tx_asphalt_runway"), new Vector2(1.2f, 1.4f));
            CreateTaxiChordPad($"{name} throat", alpha, new Vector3(10f, 0.01f, AirportLayout.TaxiwayAlphaZ + (standZ - AirportLayout.TaxiwayAlphaZ) * 0.25f), 2.8f,
                PreferSurfaceBasecolor("tx_asphalt_runway"), new Vector2(1f, 1f));
            CreateTaxiChordPad($"{name} mouth", new Vector3(stand.x - 2f, 0.01f, standZ), stand, 3.2f,
                PreferSurfaceBasecolor("tx_asphalt_runway"), new Vector2(1f, 1f));
        }

        private static void BuildAirfield()
        {
            var root = new GameObject("Airfield");
            _airfieldRoot = root.transform;
            AirsideStaticWorld.WorldRoot = _airfieldRoot;
            if (AirsideBareField.Enabled)
            {
                BuildBareAdelaideField();
                return;
            }

            if (!AirsideTerrainGround.TryBuild(_airfieldRoot))
                BuildAirfieldTerrainBase();
            if (AirsideCombinedSurfaces.UseTileOperational)
                BuildAirfieldTerrain11Operational();
            else
                BuildCombinedOperationalSurfaces();
            if (AirsideFocusMode.ShowBuildings)
                BuildAirfieldTerrain12();
            if (AirsideFocusMode.ShowBuildings || AirsideFocusMode.ShowEnvironment
                || AirsideFocusMode.ShowWorldProps)
                BuildAirfieldApronAndBuildings();
        }

        /// <summary>
        /// Authored Adelaide ground mesh, one 3100 × 45 m runway with shoulders, and
        /// real-metre paint from <see cref="AirsideRunwayMarkings"/>. No taxiways,
        /// apron, buildings, signs or props. Falls back to a grass slab if the mesh
        /// builder cannot resolve the CC0 maps.
        /// </summary>
        private static void BuildBareAdelaideField()
        {
            _bareGroundFollowsLandform = AirsideAdelaideGroundMesh.TryBuild(_airfieldRoot);
            if (!_bareGroundFollowsLandform)
            {
                var grass = Shade(AirsideTheme.DryGrass, 0.62f);
                CreateBlock(
                    AirsideBareField.GroundObjectName,
                    new Vector3(0f, AirsideBareField.GroundCenterY, 0f),
                    new Vector3(
                        AirsideBareField.GroundLengthMetres,
                        AirsideBareField.GroundHeightMetres,
                        AirsideBareField.GroundWidthMetres),
                    grass,
                    PreferSurfaceBasecolor("tx_grass_kingscote"),
                    new Vector2(
                        AirsideBareField.GroundLengthMetres / 47f,
                        AirsideBareField.GroundWidthMetres / 37f));
            }

            BuildBareAdelaidePavement();
            BuildBareAdelaidePerimeterFence();
        }

        /// <summary>
        /// YPAD silhouette: 05/23 + 12/30 + Taxiway F + D/E exits, each to the same
        /// real-metre / combined-paint standard as the original single strip.
        /// </summary>
        private static void BuildBareAdelaidePavement()
        {
            var asphalt = new Color(0.16f, 0.18f, 0.2f);
            var taxiAsphalt = new Color(0.18f, 0.19f, 0.21f);
            var shoulderColor = new Color(0.42f, 0.36f, 0.28f);
            var paint = Color.white;
            var dirtAlbedo = AirsideAdelaideGround.LayerBasecolorPath(AirsideAdelaideGround.LayerWornDirt);
            if (ArtRuntimePaths.ResolveExisting(dirtAlbedo) == null)
                dirtAlbedo = PreferSurfaceBasecolor("tx_grass_kingscote");

            BuildBareMainRunway(asphalt, shoulderColor, dirtAlbedo, paint);
            BuildBareCrossRunway(asphalt, shoulderColor, dirtAlbedo, paint);
            BuildBareTaxiSkeleton(taxiAsphalt, paint);
        }

        private static void BuildBareMainRunway(
            Color asphalt, Color shoulderColor, string dirtAlbedo, Color paint)
        {
            var runway = CreateBlock(
                AirsideAdelaidePavement.MainRunwayName,
                new Vector3(0f, AirsideBareField.RunwayCenterY, 0f),
                new Vector3(
                    AirsideBareField.RunwayLengthMetres,
                    AirsideBareField.RunwayHeightMetres,
                    AirsideBareField.RunwayWidthMetres),
                asphalt,
                PreferSurfaceBasecolor("tx_asphalt_runway"),
                new Vector2(
                    AirsideBareField.RunwayLengthMetres / 42f,
                    AirsideBareField.RunwayWidthMetres / 11f));
            ApplyRunwayMultiScale(runway.transform);
            BuildBareRunwayRubberMarks();

            var shoulderWidth = AirsideAdelaidePavement.ShoulderWidthMetres;
            var shoulderZ = AirsideBareField.RunwayHalfWidth + shoulderWidth * 0.5f;
            CreateBlock(
                "Runway 05/23 shoulder N",
                new Vector3(0f, AirsideBareField.RunwayCenterY - 0.01f, shoulderZ),
                new Vector3(AirsideBareField.RunwayLengthMetres + 40f, 0.1f, shoulderWidth),
                shoulderColor,
                dirtAlbedo,
                new Vector2((AirsideBareField.RunwayLengthMetres + 40f) / 29f, shoulderWidth / 9f));
            CreateBlock(
                "Runway 05/23 shoulder S",
                new Vector3(0f, AirsideBareField.RunwayCenterY - 0.01f, -shoulderZ),
                new Vector3(AirsideBareField.RunwayLengthMetres + 40f, 0.1f, shoulderWidth),
                shoulderColor,
                dirtAlbedo,
                new Vector2((AirsideBareField.RunwayLengthMetres + 40f) / 29f, shoulderWidth / 9f));

            var markings = new GameObject("Runway 05/23 markings").transform;
            if (_airfieldRoot != null)
                markings.SetParent(_airfieldRoot, false);

            CreateCombinedStripPaint(markings, "Runway 05/23 edge left",
                new[] { AirsideRunwayMarkings.EdgeLeft }, paint);
            CreateCombinedStripPaint(markings, "Runway 05/23 edge right",
                new[] { AirsideRunwayMarkings.EdgeRight }, paint);
            CreateCombinedStripPaint(markings, "Runway 05/23 centre",
                AirsideRunwayMarkings.CentrelineDashes(), paint);
            CreateCombinedStripPaint(markings, "Runway 05/23 threshold",
                AirsideRunwayMarkings.ThresholdStripes(), paint);
            CreateCombinedStripPaint(markings, "Aiming point 05/23",
                AirsideRunwayMarkings.AimingPoints(), paint);
            CreateCombinedStripPaint(markings, "TDZ marks 05/23",
                AirsideRunwayMarkings.TouchdownZones(), paint);
        }

        private static void BuildBareCrossRunway(
            Color asphalt, Color shoulderColor, string dirtAlbedo, Color paint)
        {
            var root = new GameObject(AirsideAdelaidePavement.CrossRunwayName).transform;
            if (_airfieldRoot != null)
                root.SetParent(_airfieldRoot, false);
            root.position = new Vector3(
                AirsideAdelaidePavement.CrossCenterX,
                AirsideBareField.RunwayCenterY,
                AirsideAdelaidePavement.CrossCenterZ);
            root.rotation = Quaternion.Euler(0f, AirsideAdelaidePavement.CrossYawDegrees, 0f);

            var slab = CreateLocalBlock(
                root,
                "Runway 12/30 slab",
                Vector3.zero,
                new Vector3(
                    AirsideAdelaidePavement.CrossLengthMetres,
                    AirsideBareField.RunwayHeightMetres,
                    AirsideAdelaidePavement.CrossWidthMetres),
                asphalt,
                PreferSurfaceBasecolor("tx_asphalt_runway"),
                new Vector2(
                    AirsideAdelaidePavement.CrossLengthMetres / 42f,
                    AirsideAdelaidePavement.CrossWidthMetres / 11f));
            ApplyRunwayMultiScale(slab.transform);

            var shoulderWidth = AirsideAdelaidePavement.ShoulderWidthMetres;
            var shoulderZ = AirsideAdelaidePavement.CrossHalfWidth + shoulderWidth * 0.5f;
            CreateLocalBlock(
                root,
                "Runway 12/30 shoulder L",
                new Vector3(0f, -0.01f, shoulderZ),
                new Vector3(AirsideAdelaidePavement.CrossLengthMetres + 30f, 0.1f, shoulderWidth),
                shoulderColor,
                dirtAlbedo,
                new Vector2((AirsideAdelaidePavement.CrossLengthMetres + 30f) / 29f, shoulderWidth / 9f));
            CreateLocalBlock(
                root,
                "Runway 12/30 shoulder R",
                new Vector3(0f, -0.01f, -shoulderZ),
                new Vector3(AirsideAdelaidePavement.CrossLengthMetres + 30f, 0.1f, shoulderWidth),
                shoulderColor,
                dirtAlbedo,
                new Vector2((AirsideAdelaidePavement.CrossLengthMetres + 30f) / 29f, shoulderWidth / 9f));

            var markings = new GameObject("Runway 12/30 markings").transform;
            markings.SetParent(root, false);
            markings.localPosition = Vector3.zero;
            markings.localRotation = Quaternion.identity;

            // Strip marks are local to the rotated root so paint follows 12/30.
            var y = AirsideRunwayMarkings.PaintLiftMetres
                    + AirsideBareField.RunwayHeightMetres * 0.5f;
            SpawnLocalStripPaint(markings, "Runway 12/30 edge left",
                new[] { AirsideStripMarkings.EdgeLeft(
                    AirsideAdelaidePavement.CrossLengthMetres,
                    AirsideAdelaidePavement.CrossWidthMetres) }, paint, y);
            SpawnLocalStripPaint(markings, "Runway 12/30 edge right",
                new[] { AirsideStripMarkings.EdgeRight(
                    AirsideAdelaidePavement.CrossLengthMetres,
                    AirsideAdelaidePavement.CrossWidthMetres) }, paint, y);
            SpawnLocalStripPaint(markings, "Runway 12/30 centre",
                AirsideStripMarkings.CentrelineDashes(AirsideAdelaidePavement.CrossLengthMetres),
                paint, y);
            SpawnLocalStripPaint(markings, "Runway 12/30 threshold",
                AirsideStripMarkings.ThresholdStripes(AirsideAdelaidePavement.CrossLengthMetres),
                paint, y);
            SpawnLocalStripPaint(markings, "Runway 12/30 aiming",
                AirsideStripMarkings.AimingPoints(
                    AirsideAdelaidePavement.CrossLengthMetres,
                    AirsideStripMarkings.ShortStripAimingFromThreshold),
                paint, y);
            SpawnLocalStripPaint(markings, "Runway 12/30 tdz",
                AirsideStripMarkings.TouchdownZones(
                    AirsideAdelaidePavement.CrossLengthMetres,
                    AirsideStripMarkings.ShortStripTouchdownDistances),
                paint, y);
        }

        private static void BuildBareTaxiSkeleton(Color taxiAsphalt, Color paint)
        {
            var taxiYellow = new Color(0.92f, 0.78f, 0.12f);
            var sealedShoulder = new Color(0.24f, 0.25f, 0.27f);
            var apronConcrete = new Color(0.42f, 0.43f, 0.44f);
            var y = AirsideBareField.RunwayCenterY;
            var h = AirsideBareField.RunwayHeightMetres * 0.92f;
            var shoulder = AirsideAdelaidePavement.TaxiSealedShoulderMetres;
            var tw = AirsideAdelaidePavement.TaxiwayWidthMetres;
            var half = AirsideAdelaidePavement.TaxiwayHalfWidth;

            void ParallelTaxi(
                string name, float centerZ, float length,
                bool paintGuide, Transform markingsRoot)
            {
                CreateBlock(
                    $"{name} shoulder N",
                    new Vector3(0f, y - 0.005f, centerZ + half + shoulder * 0.5f),
                    new Vector3(length + 24f, h * 0.85f, shoulder),
                    sealedShoulder,
                    PreferSurfaceBasecolor("tx_asphalt_runway"),
                    new Vector2(length / 40f, shoulder / 4f));
                CreateBlock(
                    $"{name} shoulder S",
                    new Vector3(0f, y - 0.005f, centerZ - half - shoulder * 0.5f),
                    new Vector3(length + 24f, h * 0.85f, shoulder),
                    sealedShoulder,
                    PreferSurfaceBasecolor("tx_asphalt_runway"),
                    new Vector2(length / 40f, shoulder / 4f));

                var slab = CreateBlock(
                    name,
                    new Vector3(0f, y, centerZ),
                    new Vector3(length, h, tw),
                    taxiAsphalt,
                    PreferSurfaceBasecolor("tx_asphalt_runway"),
                    new Vector2(length / 36f, tw / 9f));
                ApplyRunwayMultiScale(slab.transform);

                if (paintGuide && markingsRoot != null)
                {
                    var marks = AirsideStripMarkings.TaxiwayGuide(length, tw);
                    var world = OffsetMarks(marks, 0f, centerZ);
                    CreateCombinedStripPaint(
                        markingsRoot, $"{name} guide", world, taxiYellow);
                }
            }

            void CrossLink(
                string name, float centerX, float centerZ, float lengthZ, bool holdShort,
                Transform markingsRoot)
            {
                CreateBlock(
                    $"{name} shoulder E",
                    new Vector3(centerX + half + shoulder * 0.5f, y - 0.005f, centerZ),
                    new Vector3(shoulder, h * 0.85f, lengthZ + 8f),
                    sealedShoulder,
                    PreferSurfaceBasecolor("tx_asphalt_runway"),
                    new Vector2(shoulder / 4f, lengthZ / 18f));
                CreateBlock(
                    $"{name} shoulder W",
                    new Vector3(centerX - half - shoulder * 0.5f, y - 0.005f, centerZ),
                    new Vector3(shoulder, h * 0.85f, lengthZ + 8f),
                    sealedShoulder,
                    PreferSurfaceBasecolor("tx_asphalt_runway"),
                    new Vector2(shoulder / 4f, lengthZ / 18f));

                var link = CreateBlock(
                    name,
                    new Vector3(centerX, y, centerZ),
                    new Vector3(tw, h, lengthZ),
                    taxiAsphalt,
                    PreferSurfaceBasecolor("tx_asphalt_runway"),
                    new Vector2(9f, lengthZ / 18f));
                ApplyRunwayMultiScale(link.transform);

                if (holdShort && markingsRoot != null)
                {
                    var hold = AirsideStripMarkings.HoldShortBars(
                        tw, AirsideAdelaidePavement.HoldShortFromRunwayEdgeMetres);
                    CreateCombinedStripPaint(
                        markingsRoot,
                        $"{name} hold",
                        HoldShortWorld(hold, centerX),
                        taxiYellow);
                }
            }

            var markings = new GameObject("Taxiway markings").transform;
            if (_airfieldRoot != null)
                markings.SetParent(_airfieldRoot, false);

            ParallelTaxi(
                AirsideAdelaidePavement.TaxiwayFName,
                AirsideAdelaidePavement.TaxiwayFCenterZ,
                AirsideAdelaidePavement.TaxiwayFLengthMetres,
                paintGuide: true,
                markings);
            ParallelTaxi(
                AirsideAdelaidePavement.TaxiwayAName,
                AirsideAdelaidePavement.TaxiwayACenterZ,
                AirsideAdelaidePavement.TaxiwayALengthMetres,
                paintGuide: true,
                markings);

            var exits = AirsideAdelaidePavement.RunwayExitCenterXs;
            var exitNames = AirsideAdelaidePavement.RunwayExitNames;
            for (var i = 0; i < exits.Length; i++)
            {
                CrossLink(
                    exitNames[i],
                    exits[i],
                    AirsideAdelaidePavement.TaxiLinkCenterZ,
                    AirsideAdelaidePavement.TaxiLinkLengthZ,
                    holdShort: true,
                    markings);
            }

            var af = AirsideAdelaidePavement.AfLinkCenterXs;
            for (var i = 0; i < af.Length; i++)
            {
                CrossLink(
                    $"Taxiway AF link {i}",
                    af[i],
                    AirsideAdelaidePavement.AfLinkCenterZ,
                    AirsideAdelaidePavement.AfLinkLengthZ,
                    holdShort: false,
                    markings);
            }

            var apronEntries = AirsideAdelaidePavement.ApronEntryCenterXs;
            for (var i = 0; i < apronEntries.Length; i++)
            {
                CrossLink(
                    $"Taxiway apron entry {i}",
                    apronEntries[i],
                    AirsideAdelaidePavement.ApronEntryCenterZ,
                    AirsideAdelaidePavement.ApronEntryLengthZ,
                    holdShort: false,
                    markings);
            }

            // Terminal apron pad (concrete) — empty, no buildings.
            var terminal = CreateBlock(
                AirsideAdelaidePavement.TerminalApronName,
                new Vector3(
                    AirsideAdelaidePavement.TerminalApronCenterX,
                    y - 0.01f,
                    AirsideAdelaidePavement.TerminalApronCenterZ),
                new Vector3(
                    AirsideAdelaidePavement.TerminalApronLengthX,
                    h * 0.88f,
                    AirsideAdelaidePavement.TerminalApronWidthZ),
                apronConcrete,
                PreferSurfaceBasecolor("tx_asphalt_runway"),
                new Vector2(
                    AirsideAdelaidePavement.TerminalApronLengthX / 28f,
                    AirsideAdelaidePavement.TerminalApronWidthZ / 18f));
            ApplyRunwayMultiScale(terminal.transform);

            // RFDS / south apron silhouette.
            var rfds = CreateBlock(
                AirsideAdelaidePavement.RfdsApronName,
                new Vector3(
                    AirsideAdelaidePavement.RfdsApronCenterX,
                    y - 0.01f,
                    AirsideAdelaidePavement.RfdsApronCenterZ),
                new Vector3(
                    AirsideAdelaidePavement.RfdsApronLengthX,
                    h * 0.88f,
                    AirsideAdelaidePavement.RfdsApronWidthZ),
                apronConcrete,
                PreferSurfaceBasecolor("tx_asphalt_runway"),
                new Vector2(
                    AirsideAdelaidePavement.RfdsApronLengthX / 18f,
                    AirsideAdelaidePavement.RfdsApronWidthZ / 14f));
            ApplyRunwayMultiScale(rfds.transform);

            BuildBareTaxiFillets(taxiAsphalt);
        }

        /// <summary>
        /// Quarter-disk / end-cap / crossing meshes so taxi and runway joins read as
        /// smooth Code C/E pavement, not hard cube corners.
        /// </summary>
        private static void BuildBareTaxiFillets(Color taxiAsphalt)
        {
            // Fillets are flat patches, so they sit on the taxiway *surface* — the
            // top of the slab, not its mid-height. (The old code placed them at the
            // slab centre and then scaled Y, which does nothing to a mesh whose
            // vertices are all at y = 0.)
            var y = AirsideBareField.RunwayCenterY
                    + AirsideBareField.RunwayHeightMetres * 0.92f * 0.5f;

            // One shared material for all of them, built with the texture and tiling
            // baked into the cache key. Never mutate what CreateSharedSurfaceMaterial
            // hands back — it is shared with every other caller of the same key.
            var albedo = PreferSurfaceBasecolor("tx_asphalt_runway");
            var material = CreateSharedSurfaceMaterial(
                taxiAsphalt, albedo, Vector2.one);

            var fillets = AirsideAdelaidePavement.AllFillets();
            for (var i = 0; i < fillets.Length; i++)
            {
                var f = fillets[i];
                var mesh = f.Kind == PavementArcKind.CornerFillet
                    ? CreateCornerFilletMesh(f, AirsideAdelaidePavement.FilletArcSegments)
                    : CreateSectorDiskMesh(
                        f.Radius,
                        f.StartRadians,
                        f.SweepRadians,
                        AirsideAdelaidePavement.FilletArcSegments,
                        f.CenterX,
                        f.CenterZ);
                if (mesh == null)
                    continue;
                var go = new GameObject($"Pavement fillet {i}");
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = true;
                go.transform.SetParent(_airfieldRoot, false);
                go.transform.position = new Vector3(f.CenterX, y, f.CenterZ);
                AirsideSceneIndex.Remember(go);
                ApplyRunwayMultiScale(go.transform);
            }
        }

        /// <summary>
        /// Flat concave fillet in a re-entrant pavement corner: the curved triangle
        /// bounded by the two edges and an arc of <paramref name="segments"/> steps
        /// tangent to both. Local space is centred on the corner, so the caller
        /// positions the object at the corner point.
        /// </summary>
        private static Mesh CreateCornerFilletMesh(
            AirsideAdelaidePavement.FilletSpec spec, int segments)
        {
            var r = spec.Radius;
            if (r <= 0.01f || segments < 2)
                return null;

            var steps = Mathf.Max(2, segments);
            var sx = spec.OutwardX;
            var sz = spec.OutwardZ;
            // Arc centre, in corner-local metres.
            var ox = sx * r;
            var oz = sz * r;

            // Arc runs from the tangent point on one edge to the tangent point on
            // the other: (ox, 0) -> (0, oz), swept around (ox, oz).
            var startAngle = Mathf.Atan2(-oz, 0f);
            var endAngle = Mathf.Atan2(0f, -ox);
            var sweep = Mathf.DeltaAngle(
                startAngle * Mathf.Rad2Deg, endAngle * Mathf.Rad2Deg) * Mathf.Deg2Rad;

            var verts = new Vector3[steps + 2];
            var uvs = new Vector2[steps + 2];
            verts[0] = Vector3.zero; // the corner itself
            for (var i = 0; i <= steps; i++)
            {
                var a = startAngle + sweep * (i / (float)steps);
                verts[i + 1] = new Vector3(ox + Mathf.Cos(a) * r, 0f, oz + Mathf.Sin(a) * r);
            }

            // World-metre UVs so the asphalt is continuous with the abutting slab.
            for (var i = 0; i < verts.Length; i++)
            {
                uvs[i] = new Vector2(
                    (spec.CenterX + verts[i].x) / 9f,
                    (spec.CenterZ + verts[i].z) / 9f);
            }

            return BuildFanMesh("PavementFillet", verts, uvs, closed: false);
        }

        /// <summary>
        /// Flat sector / disk in the XZ plane (Y up). <paramref name="worldCenterX"/>
        /// / <paramref name="worldCenterZ"/> are only used to keep the texture UVs
        /// aligned with the surrounding pavement.
        /// </summary>
        private static Mesh CreateSectorDiskMesh(
            float radius, float startRadians, float sweepRadians, int segments,
            float worldCenterX = 0f, float worldCenterZ = 0f)
        {
            if (radius <= 0.01f || segments < 3)
                return null;
            var steps = Mathf.Max(3, segments);
            var full = Mathf.Abs(sweepRadians) >= Mathf.PI * 2f - 0.01f;

            var count = full ? steps + 1 : steps + 2;
            var verts = new Vector3[count];
            var uvs = new Vector2[count];
            verts[0] = Vector3.zero;
            var arcCount = full ? steps : steps + 1;
            for (var i = 0; i < arcCount; i++)
            {
                var a = startRadians + sweepRadians * (i / (float)steps);
                verts[i + 1] = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            }

            for (var i = 0; i < verts.Length; i++)
            {
                uvs[i] = new Vector2(
                    (worldCenterX + verts[i].x) / 9f,
                    (worldCenterZ + verts[i].z) / 9f);
            }

            return BuildFanMesh(full ? "PavementDisk" : "PavementSector", verts, uvs, full);
        }

        /// <summary>
        /// Triangle fan from <paramref name="verts"/>[0] around the remaining
        /// vertices, wound so the face normal points +Y whichever way the arc runs.
        /// Getting this wrong renders the patch invisible from above.
        /// </summary>
        private static Mesh BuildFanMesh(string name, Vector3[] verts, Vector2[] uvs, bool closed)
        {
            var arc = verts.Length - 1;
            if (arc < 2)
                return null;
            var triCount = closed ? arc : arc - 1;
            if (triCount < 1)
                return null;

            // Signed area of the fan polygon in the XZ plane. Positive means the
            // boundary runs counter-clockwise in maths convention, which — viewed
            // from +Y in Unity's left-handed space — needs the reversed winding.
            //
            // For an open fan the apex is ON the boundary and must be included: a
            // concave corner fillet bulges away from its apex, so the arc alone
            // reports the opposite sign and the patch renders face-down (invisible).
            var first = closed ? 1 : 0;
            var area = 0f;
            for (var i = first; i <= arc; i++)
            {
                var a = verts[i];
                var b = verts[i < arc ? i + 1 : first];
                area += a.x * b.z - b.x * a.z;
            }

            var reversed = area > 0f;
            var tris = new int[triCount * 3];
            for (var i = 0; i < triCount; i++)
            {
                var next = i + 2 <= arc ? i + 2 : 1;
                tris[i * 3] = 0;
                tris[i * 3 + 1] = reversed ? next : i + 1;
                tris[i * 3 + 2] = reversed ? i + 1 : next;
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }


        private static AirsideRunwayMarkings.RunwayMark[] OffsetMarks(
            AirsideStripMarkings.Mark[] marks, float offsetX, float offsetZ)
        {
            var result = new AirsideRunwayMarkings.RunwayMark[marks.Length];
            for (var i = 0; i < marks.Length; i++)
            {
                var m = marks[i];
                result[i] = new AirsideRunwayMarkings.RunwayMark(
                    m.CenterX + offsetX, m.CenterZ + offsetZ, m.LengthX, m.WidthZ);
            }

            return result;
        }

        private static AirsideRunwayMarkings.RunwayMark[] HoldShortWorld(
            AirsideStripMarkings.Mark[] local, float centerX)
        {
            // Local hold marks: LengthX = across taxi, WidthZ = bar thickness along link.
            // World: X = across (same), Z = runway-edge + local Z.
            var runwayEdgeZ = AirsideBareField.RunwayHalfWidth;
            var result = new AirsideRunwayMarkings.RunwayMark[local.Length];
            for (var i = 0; i < local.Length; i++)
            {
                var m = local[i];
                result[i] = new AirsideRunwayMarkings.RunwayMark(
                    centerX + m.CenterX,
                    runwayEdgeZ + m.CenterZ,
                    m.LengthX,
                    m.WidthZ);
            }

            return result;
        }

        private static GameObject CreateLocalBlock(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Color color,
            string artTextureRelativePath = null,
            Vector2? textureTiling = null)
        {
            var block = CreateBlock(name, localPosition, localScale, color, artTextureRelativePath, textureTiling);
            block.transform.SetParent(parent, false);
            block.transform.localPosition = localPosition;
            block.transform.localRotation = Quaternion.identity;
            block.transform.localScale = localScale;
            return block;
        }

        private static void SpawnLocalStripPaint(
            Transform parent, string name, AirsideStripMarkings.Mark[] marks, Color color, float localY)
        {
            if (marks == null || marks.Length == 0)
                return;

            var h = AirsideRunwayMarkings.PaintHeight;
            var locals = new Matrix4x4[marks.Length];
            for (var i = 0; i < marks.Length; i++)
            {
                var mark = marks[i];
                locals[i] = Matrix4x4.TRS(
                    new Vector3(mark.CenterX, localY, mark.CenterZ),
                    Quaternion.identity,
                    new Vector3(mark.LengthX, h, mark.WidthZ));
            }

            var mesh = AirsideMeshUtil.CombineTransformed(BuiltinCube(), locals);
            if (mesh == null)
            {
                foreach (var mark in marks)
                {
                    ParentBlock(
                        parent,
                        name,
                        new Vector3(mark.CenterX, localY, mark.CenterZ),
                        new Vector3(mark.LengthX, h, mark.WidthZ),
                        color);
                }

                return;
            }

            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = CreateSharedSurfaceMaterial(color);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            go.transform.SetParent(parent, false);
            AirsideSceneIndex.Remember(go);
        }

        /// <summary>
        /// One combined paint mesh per marking family so a multi-kilometre strip is not
        /// hundreds of unbatched cubes. Accepts <see cref="AirsideRunwayMarkings.RunwayMark"/>.
        /// </summary>
        private static void CreateCombinedStripPaint(
            Transform parent, string name, AirsideRunwayMarkings.RunwayMark[] marks, Color color)
        {
            CreateCombinedRunwayPaint(parent, name, marks, color);
        }

        /// <summary>
        /// Dark rubber-deposit bands in the touchdown zone — presentation only,
        /// matching the darkened asphalt pilots see on a busy jet runway.
        /// </summary>
        private static void BuildBareRunwayRubberMarks()
        {
            var rubber = new Color(0.08f, 0.08f, 0.09f, 1f);
            var y = AirsideBareField.RunwayCenterY + AirsideBareField.RunwayHeightMetres * 0.52f;
            // Aiming-point / TDZ region each end (≈ 400–900 m from threshold).
            float[] centers = { -900f, -650f, 650f, 900f };
            for (var i = 0; i < centers.Length; i++)
            {
                CreateBlock(
                    $"Runway rubber {i}",
                    new Vector3(centers[i], y, 0f),
                    new Vector3(180f, 0.02f, AirsideBareField.RunwayWidthMetres - 4f),
                    rubber);
            }
        }

        /// <summary>
        /// Adelaide-scale airside security fence on the 785 ha site boundary —
        /// chain-link height + vehicle gates. No buildings.
        /// </summary>
        /// <summary>
        /// Airside security fence on the published site rectangle.
        ///
        /// Every panel, post, guard rail and gate leaf is seated on
        /// <see cref="AirsideAdelaidePerimeter.FenceBaseY"/> rather than world Y = 0:
        /// the authored ground drops roughly 2.4 m into its boundary lip exactly
        /// where the fence runs, so a fixed height leaves the whole ~11 km ribbon
        /// hanging in mid-air by about its own height.
        ///
        /// Panels are combined into one mesh per side, which turns ~900 renderers
        /// into a handful — at overview range the individual pickets are well under
        /// a pixel.
        /// </summary>
        private static void BuildBareAdelaidePerimeterFence()
        {
            var meshColor = new Color(0.42f, 0.44f, 0.46f);
            var postColor = new Color(0.32f, 0.33f, 0.35f);
            var guardColor = new Color(0.55f, 0.56f, 0.58f);
            var gateYellow = new Color(0.90f, 0.75f, 0.10f);
            var hx = AirsideAdelaidePerimeter.FenceHalfX;
            var hz = AirsideAdelaidePerimeter.FenceHalfZ;
            var h = AirsideAdelaidePerimeter.FenceHeightMetres;
            var guard = AirsideAdelaidePerimeter.TopGuardHeightMetres;
            var thick = AirsideAdelaidePerimeter.PanelThicknessMetres;
            var post = AirsideAdelaidePerimeter.PostSizeMetres;
            var spacing = AirsideAdelaidePerimeter.PostSpacingMetres;
            var root = new GameObject("Adelaide perimeter fence").transform;
            if (_airfieldRoot != null)
                root.SetParent(_airfieldRoot, false);

            // On the authored ground mesh the fence follows the landform. If that
            // mesh could not be built we are standing on the flat fallback slab, so
            // the fence seats on the slab top instead.
            float BaseY(float x, float z) =>
                _bareGroundFollowsLandform
                    ? AirsideAdelaidePerimeter.FenceBaseY(x, z)
                    : AirsideAdelaideGround.PavementWorldY - AirsideAdelaidePerimeter.FenceEmbedMetres;

            float BaseYRun(float x0, float z0, float x1, float z1)
            {
                if (!_bareGroundFollowsLandform)
                    return BaseY(x0, z0);
                return AirsideAdelaidePerimeter.FenceBaseYAlongSegment(x0, z0, x1, z1);
            }

            void Panel(string name, Vector3 pos, Vector3 scale, Color color)
            {
                var block = CreateBlock(name, pos, scale, color);
                block.transform.SetParent(root, true);
            }

            void SpawnBatch(string name, List<Matrix4x4> locals, Color color)
            {
                if (locals.Count == 0)
                    return;
                var mesh = AirsideMeshUtil.CombineTransformed(BuiltinCube(), locals.ToArray());
                if (mesh == null)
                {
                    // Same fallback the strip paint uses: one block per instance.
                    for (var i = 0; i < locals.Count; i++)
                    {
                        var m = locals[i];
                        Panel($"{name} {i}", m.GetColumn(3), m.lossyScale, color);
                    }

                    return;
                }

                var go = new GameObject(name);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = CreateSharedSurfaceMaterial(color);
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                go.transform.SetParent(root, false);
                AirsideSceneIndex.Remember(go);
            }

            void BuildSide(string side, bool alongX)
            {
                var panels = new List<Matrix4x4>(128);
                var guards = new List<Matrix4x4>(128);
                var posts = new List<Matrix4x4>(128);

                var length = alongX ? hx * 2f : hz * 2f;
                var cursor = -length * 0.5f;
                while (cursor < length * 0.5f - 0.01f)
                {
                    var remaining = length * 0.5f - cursor;
                    var seg = Mathf.Min(spacing, remaining);
                    var segMid = cursor + seg * 0.5f;

                    // Leave a clear opening where a vehicle gate stands.
                    if (AirsideAdelaidePerimeter.IsInGateGap(side, segMid))
                    {
                        cursor += AirsideAdelaidePerimeter.VehicleGateWidthMetres;
                        continue;
                    }

                    float x0, z0, x1, z1, midX, midZ;
                    if (alongX)
                    {
                        var z = side == "N" ? hz : -hz;
                        x0 = cursor; z0 = z;
                        x1 = cursor + seg; z1 = z;
                        midX = segMid; midZ = z;
                    }
                    else
                    {
                        var x = side == "E" ? hx : -hx;
                        x0 = x; z0 = cursor;
                        x1 = x; z1 = cursor + seg;
                        midX = x; midZ = segMid;
                    }

                    var baseY = BaseYRun(x0, z0, x1, z1);
                    var panelScale = alongX
                        ? new Vector3(seg, h, thick)
                        : new Vector3(thick, h, seg);
                    var guardScale = alongX
                        ? new Vector3(seg, guard, thick * 0.7f)
                        : new Vector3(thick * 0.7f, guard, seg);

                    panels.Add(Matrix4x4.TRS(
                        new Vector3(midX, baseY + h * 0.5f, midZ), Quaternion.identity, panelScale));
                    guards.Add(Matrix4x4.TRS(
                        new Vector3(midX, baseY + h + guard * 0.5f, midZ), Quaternion.identity, guardScale));

                    var postBase = BaseY(x0, z0);
                    posts.Add(Matrix4x4.TRS(
                        new Vector3(x0, postBase + (h + guard) * 0.5f, z0),
                        Quaternion.identity,
                        new Vector3(post, h + guard, post)));

                    cursor += seg;
                }

                SpawnBatch($"Fence {side}", panels, meshColor);
                SpawnBatch($"Fence guard {side}", guards, guardColor);
                SpawnBatch($"Fence post {side}", posts, postColor);
            }

            BuildSide("N", alongX: true);
            BuildSide("S", alongX: true);
            BuildSide("E", alongX: false);
            BuildSide("W", alongX: false);

            // Corner posts.
            float[] cxs = { -hx, hx };
            float[] czs = { -hz, hz };
            for (var ix = 0; ix < cxs.Length; ix++)
            for (var iz = 0; iz < czs.Length; iz++)
            {
                var cornerBase = BaseY(cxs[ix], czs[iz]);
                Panel(
                    $"Fence corner {ix}{iz}",
                    new Vector3(cxs[ix], cornerBase + (h + guard) * 0.5f, czs[iz]),
                    new Vector3(post * 1.4f, h + guard, post * 1.4f),
                    postColor);
            }

            // Vehicle gates.
            for (var g = 0; g < AirsideAdelaidePerimeter.VehicleGates.Length; g++)
            {
                var gate = AirsideAdelaidePerimeter.VehicleGates[g];
                var gw = AirsideAdelaidePerimeter.VehicleGateWidthMetres;
                var gh = AirsideAdelaidePerimeter.GateLeafHeightMetres;
                float gx, gz;
                Vector3 leafScale;
                Vector3 postOffset;
                if (gate.Side == "N" || gate.Side == "S")
                {
                    gx = gate.StationAlongSide;
                    gz = gate.Side == "N" ? hz : -hz;
                    leafScale = new Vector3(gw * 0.48f, gh, thick * 1.2f);
                    postOffset = new Vector3(gw * 0.5f, 0f, 0f);
                }
                else
                {
                    gx = gate.Side == "E" ? hx : -hx;
                    gz = gate.StationAlongSide;
                    leafScale = new Vector3(thick * 1.2f, gh, gw * 0.48f);
                    postOffset = new Vector3(0f, 0f, gw * 0.5f);
                }

                var gateBase = BaseY(gx, gz);
                var groundPos = new Vector3(gx, gateBase, gz);
                var postCentre = Vector3.up * ((h + guard) * 0.5f);
                var postScale = new Vector3(post * 1.6f, h + guard, post * 1.6f);
                Panel($"{gate.Name} post L", groundPos - postOffset + postCentre, postScale, postColor);
                Panel($"{gate.Name} post R", groundPos + postOffset + postCentre, postScale, postColor);

                // Leaves ajar slightly toward landside.
                var open = gate.Side == "N" || gate.Side == "E" ? 1.2f : -1.2f;
                var leafShift = gate.Side == "N" || gate.Side == "S"
                    ? new Vector3(0f, 0f, open)
                    : new Vector3(open, 0f, 0f);
                var leafCentre = Vector3.up * (gh * 0.5f);
                Panel($"{gate.Name} leaf L",
                    groundPos - postOffset * 0.5f + leafShift + leafCentre, leafScale, gateYellow);
                Panel($"{gate.Name} leaf R",
                    groundPos + postOffset * 0.5f + leafShift + leafCentre, leafScale, gateYellow);
            }
        }

        /// <summary>
        /// Second-scale asphalt detail on URP Lit so long-runway tiling does not read
        /// as stretched pixels from follow, without changing the 45 m footprint.
        /// </summary>
        private static void ApplyRunwayMultiScale(Transform runway)
        {
            if (runway == null)
                return;
            var renderer = runway.GetComponent<Renderer>();
            if (renderer == null || renderer.sharedMaterial == null)
                return;
            var material = renderer.material;
            if (material.mainTexture == null || !material.HasProperty("_DetailAlbedoMap"))
                return;
            // Second UV scale breaks the long-runway landmark repeat without new assets.
            material.SetTexture("_DetailAlbedoMap", material.mainTexture);
            material.SetTextureScale("_DetailAlbedoMap", new Vector2(3.7f, 1.9f));
            material.EnableKeyword("_DETAIL_MULX2");
            if (material.HasProperty("_DetailAlbedoMapScale"))
                material.SetFloat("_DetailAlbedoMapScale", 0.35f);
            var normalPath = PreferSurfaceMap("tx_asphalt_runway", "normal");
            if (normalPath != null && material.HasProperty("_DetailNormalMap"))
            {
                var normal = AirsideArtTextures.Load(normalPath, linear: true);
                if (normal != null)
                {
                    material.SetTexture("_DetailNormalMap", normal);
                    material.SetTextureScale("_DetailNormalMap", new Vector2(3.7f, 1.9f));
                }
            }
        }

        /// <summary>
        /// One combined paint mesh per marking family so a 3 100 m strip is not
        /// hundreds of unbatched cubes. Names keep the wet-surface collectors working.
        /// </summary>
        private static void CreateCombinedRunwayPaint(
            Transform parent, string name, AirsideRunwayMarkings.RunwayMark[] marks, Color color)
        {
            if (marks == null || marks.Length == 0)
                return;

            var y = AirsideRunwayMarkings.PaintCenterY;
            var h = AirsideRunwayMarkings.PaintHeight;
            var locals = new Matrix4x4[marks.Length];
            for (var i = 0; i < marks.Length; i++)
            {
                var mark = marks[i];
                locals[i] = Matrix4x4.TRS(
                    new Vector3(mark.CenterX, y, mark.CenterZ),
                    Quaternion.identity,
                    new Vector3(mark.LengthX, h, mark.WidthZ));
            }

            var mesh = AirsideMeshUtil.CombineTransformed(BuiltinCube(), locals);
            if (mesh == null)
            {
                foreach (var mark in marks)
                {
                    ParentBlock(
                        parent,
                        name,
                        new Vector3(mark.CenterX, y, mark.CenterZ),
                        new Vector3(mark.LengthX, h, mark.WidthZ),
                        color);
                }

                return;
            }

            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = CreateSharedSurfaceMaterial(color);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            go.transform.SetParent(parent, false);
            AirsideSceneIndex.Remember(go);
        }

        /// <summary>
        /// P0 — one slab per operational surface instead of the 745-tile Terrain11 dump.
        /// Names keep the wet-surface / hold-short collectors working.
        /// </summary>
        private static void BuildCombinedOperationalSurfaces()
        {
            var grass = Shade(AirsideTheme.Eucalyptus, 0.62f);
            var asphalt = new Color(0.16f, 0.18f, 0.2f);
            var concrete = new Color(0.34f, 0.36f, 0.37f);
            var taxiAsphalt = new Color(0.22f, 0.24f, 0.26f);
            var runwayLen = AirportLayout.RunwayLength;
            var runwayHalf = AirportLayout.RunwayHalfLength;
            var taxiSpan = AirportLayout.TaxiwayEastX - AirportLayout.AlphaJunctionX + 16f;
            var taxiCentreX = (AirportLayout.TaxiwayEastX + AirportLayout.AlphaJunctionX) * 0.5f;

            CreateBlock("Infield grass", new Vector3(4f, -0.55f, 4.6f), new Vector3(runwayHalf + 8f, 0.28f, 3.4f), grass,
                PreferSurfaceBasecolor("tx_grass_kingscote"), new Vector2(12f, 2.2f));
            CreateBlock("Runway W", new Vector3(0f, -0.08f, 0f), new Vector3(runwayLen, 0.144f, AirportLayout.RunwayWidth), asphalt,
                PreferSurfaceBasecolor("tx_asphalt_runway"), new Vector2(24f, 2.4f));
            CreateBlock("Runway blast W", new Vector3(-runwayHalf - 4f, -0.08f, 0f), new Vector3(8f, 0.14f, 6.4f), asphalt,
                PreferSurfaceBasecolor("tx_asphalt_runway"), new Vector2(2.2f, 2.2f));
            CreateBlock("Runway blast E", new Vector3(runwayHalf + 4f, -0.08f, 0f), new Vector3(8f, 0.14f, 6.4f), asphalt,
                PreferSurfaceBasecolor("tx_asphalt_runway"), new Vector2(2.2f, 2.2f));
            CreateBlock("Taxiway A", new Vector3(taxiCentreX, -0.02f, AirportLayout.TaxiwayAlphaZ),
                new Vector3(taxiSpan, 0.12f, 4.2f), taxiAsphalt,
                PreferSurfaceBasecolor("tx_asphalt_runway"), new Vector2(16f, 1.6f));
            CreateBlock("Taxiway B", new Vector3(taxiCentreX, -0.02f, AirportLayout.TaxiwayBravoZ),
                new Vector3(taxiSpan, 0.12f, 4.2f), taxiAsphalt,
                PreferSurfaceBasecolor("tx_asphalt_runway"), new Vector2(16f, 1.6f));
            CreateTaxiChordPad("Taxiway B exit", new Vector3(AirportLayout.ArrivalExitX, -0.02f, 0f),
                new Vector3(AirportLayout.ArrivalExitX, -0.02f, AirportLayout.TaxiwayBravoZ), 4.2f,
                PreferSurfaceBasecolor("tx_asphalt_runway"), new Vector2(1.2f, 1.4f));
            CreateTaxiChordPad("Taxiway A entry", new Vector3(AirportLayout.DepartureEntryX, -0.02f, 0f),
                new Vector3(AirportLayout.AlphaJunctionX, -0.02f, AirportLayout.TaxiwayAlphaZ), 4.2f,
                PreferSurfaceBasecolor("tx_asphalt_runway"), new Vector2(1.2f, 1.4f));
            CreateBlock("Apron ", new Vector3(20f, 0f, 18f), new Vector3(28f, 0.12f, 16f), concrete,
                PreferSurfaceBasecolor("tx_concrete_apron"), new Vector2(6f, 4f));
        }

        // Fallback ground, used only when the baked Kingscote TerrainData is absent.
        //
        // The former fine-grained outer-ground tile field created tens of thousands of
        // primitives during Awake, preventing the player from reaching its first frame.
        // One textured base keeps the operational airfield visible; the retained inner
        // runway, taxi, apron, buildings, props and context provide the visual detail.
        private static void BuildAirfieldTerrainBase()
        {
            CreateBlock("Airfield terrain base",
                new Vector3(AirsideTerrainField.CentreX, -0.76f, AirsideTerrainField.CentreZ),
                new Vector3(AirsideTerrainField.SizeX + 24f, 0.8f, AirsideTerrainField.SizeZ + 24f),
                Shade(AirsideTheme.DryGrass, 0.62f), PreferSurfaceBasecolor("tx_grass_kingscote"),
                new Vector2(AirsideTerrainField.SizeX * 0.11f, AirsideTerrainField.SizeZ * 0.12f));
        }

        private static void BuildAirfieldTerrain11Operational()
        {
            // Tile dump retired — combined pads plus the WLD-004 kit cover this ground.
            BuildCombinedOperationalSurfaces();
        }

        private static void BuildAirfieldTerrain12()
        {
            // Warm interior spill at dusk/night — only when the terminal kit did not
            // already ship interior glow meshes (avoid stacking cubes on authored glass).
            if (FindBuilt("interior_glow_l") == null
                && FindBuilt("interior_glow_r") == null
                && FindBuilt("interior_glow_mid") == null)
            {
                CreateBlock("Terminal window glow L", new Vector3(20f, 2.35f, 24.5f), new Vector3(5.5f, 1.6f, 0.08f), new Color(1f, 0.82f, 0.45f));
                CreateBlock("Terminal window glow R", new Vector3(32f, 2.35f, 24.5f), new Vector3(5.5f, 1.6f, 0.08f), new Color(1f, 0.82f, 0.45f));
            }

            if (FindBuilt("landside_glass") == null && FindBuilt("interior_glow_desk") == null)
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
            if (FindBuilt("Hangar door") == null
                && FindBuilt("door_panel_l") == null
                && FindBuilt("door_panel_r") == null)
                CreateBlock("Hangar door", new Vector3(-20f, 2.0f, 24.6f), new Vector3(8f, 4f, 0.2f), new Color(0.22f, 0.24f, 0.26f));
            // Prefer hangar-kit bay props (workbench / tool cabinet) over greybox densify.
            if (FindBuilt("workbench") == null && FindBuilt("tool_cabinet") == null)
                BuildHangarBayInterior();
            if (FindBuilt("side_window") == null
                && FindBuilt("side_window_b") == null
                && FindBuilt("office_window") == null
                && FindBuilt("glass_pane") == null
                && FindBuilt("glass_pane_l") == null
                && FindBuilt("glass_pane_r") == null)
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
            if (FindBuilt("interior_glow") == null
                && FindBuilt("window_l") == null
                && FindBuilt("window_r") == null)
                CreateBlock("Ops shed window glow", new Vector3(-8f, 1.5f, 24.1f), new Vector3(3.2f, 1.1f, 0.08f), new Color(1f, 0.78f, 0.4f));

            // Soft wear accent only — large stain sheets were opaque black patches (PNG alpha ignored).
            CreateDecalQuad("Runway wear W", new Vector3(-52f, 0.02f, 0f), new Vector3(18f, 1f, 1.2f),
                "Textures/Decals/dc_runway_wear_v01.png");
            CreateDecalQuad("Runway wear mid", new Vector3(0f, 0.02f, 0f), new Vector3(18f, 1f, 1.15f),
                "Textures/Decals/dc_runway_wear_v01.png");
            CreateDecalQuad("Runway wear E", new Vector3(52f, 0.02f, 0f), new Vector3(18f, 1f, 1.2f),
                "Textures/Decals/dc_runway_wear_v01.png");
            // Soft hangar apron so the hangar does not sit on raw grass (regional strip cue).
            CreateBlock("Hangar apron", new Vector3(-20.5f, -0.01f, 16.4f), new Vector3(16f, 0.09f, 8.4f), new Color(0.34f, 0.36f, 0.37f),
                PreferSurfaceBasecolor("tx_concrete_apron"), new Vector2(3.2f, 2.2f));
            CreateBlock("Hangar apron wing", new Vector3(-26.6f, -0.01f, 17.5f), new Vector3(8.2f, 0.08f, 5.2f), new Color(0.33f, 0.35f, 0.36f),
                PreferSurfaceBasecolor("tx_concrete_apron"), new Vector2(2.2f, 1.4f));
            CreateTaxiChordPad("Hangar taxi link", new Vector3(-12f, -0.02f, 9f), new Vector3(-18f, -0.02f, 14f), 4.2f,
                PreferSurfaceBasecolor("tx_asphalt_runway"), new Vector2(1.4f, 1.1f));
            // Hangar apron lead paint — yellow lead-in toward the door.
            CreateBlock("Hangar apron centre", new Vector3(-20f, 0.04f, 17.2f), new Vector3(0.12f, 0.02f, 4.2f), new Color(0.95f, 0.85f, 0.2f));
            CreateBlock("Hangar apron edge N", new Vector3(-20f, 0.04f, 20.2f), new Vector3(8.5f, 0.02f, 0.1f), Color.white);
            CreateBlock("Hangar apron edge S", new Vector3(-20f, 0.04f, 13.2f), new Vector3(8.5f, 0.02f, 0.1f), Color.white);
            CreateBlock("Hangar apron stop", new Vector3(-20f, 0.04f, 19.4f), new Vector3(3.2f, 0.02f, 0.12f), new Color(0.95f, 0.85f, 0.2f));

            // Soft fringe so the apron doesn't float as a hard cutout (REF densify).
            // N/S only — E/W fringe cubes read as blocks beside taxi/stand lead-ins.
        }
        private static void BuildAirfieldApronAndBuildings()
        {
            var fringe = Shade(AirsideTheme.DryGrass, 0.7f);
            CreateBlock("Apron fringe N", new Vector3(16f, -0.022f, 25.12f), new Vector3(28f, 0.055f, 1.2f), fringe,
                PreferSurfaceBasecolor("tx_grass_kingscote"), new Vector2(8f, 0.35f));
            CreateBlock("Apron fringe S", new Vector3(16f, -0.022f, 9.1f), new Vector3(28f, 0.055f, 1.2f), fringe,
                PreferSurfaceBasecolor("tx_grass_kingscote"), new Vector2(8f, 0.35f));
            // Planter strip between terminal glass and apron edge — prefer PRP-003 kit.
            if (!TryPlaceAirsidePlanterStrip())
            {
                CreateBlock("Terminal planter bed W", new Vector3(22.5f, 0.12f, 24.4f), new Vector3(7f, 0.28f, 1.1f),
                    new Color(0.28f, 0.22f, 0.16f));
                CreateBlock("Terminal planter bed E", new Vector3(29.5f, 0.12f, 24.45f), new Vector3(6.65f, 0.2688f, 1.067f),
                    new Color(0.3f, 0.24f, 0.17f));
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

            BuildStandMarking(AirportLayout.StandX, 14f, "Stand 1");
            BuildStandMarking(AirportLayout.StandX, 24f, "Stand 2");
            // Stand bay digits come from PlaceWorldMarkings / PlaceRunwayDigit (kit-prefer).
        }

        /// <summary>
        /// Decision 0025 item 3 — regional environment greybox around the operating
        /// airfield: coast, access road, car park, fencing, vegetation and a soft
        /// horizon dome. Presentation only; primitives + existing Batch B surfaces.
        /// </summary>
        private static void BuildEnvironmentContext()
        {
            var terrainKit = PreferArtKit(
                "Models/Environment/mdl_kingscote_context_terrain_v02.gltf",
                "Models/Environment/mdl_kingscote_context_terrain_v01.gltf");
            var hasTerrainKit = !string.IsNullOrEmpty(terrainKit) && ArtGltfLoader.HasKit(terrainKit);
            if (!hasTerrainKit)
            {
                var sand = AirsideTheme.Sand;
                var water = new Color(0.18f, 0.38f, 0.48f);
                var paddock = Shade(AirsideTheme.DryGrass, 0.7f);
                CreateBlock("Coast sand", new Vector3(-20f, -0.55f, -48f), new Vector3(180f, 0.12f, 28f), sand,
                    PreferSurfaceBasecolor("tx_sand_coast"), new Vector2(18f, 4f));
                CreateBlock("Coast water", new Vector3(0f, -1.12f, -72f), new Vector3(220f, 0.18f, 36f), water,
                    PreferSurfaceBasecolor("tx_water_coast"), new Vector2(12f, 3f));
                CreateBlock("Outer paddock W", new Vector3(-95f, -0.35f, 8f), new Vector3(70f, 0.1f, 140f), paddock,
                    PreferSurfaceBasecolor("tx_grass_kingscote"), new Vector2(16f, 28f));
                CreateBlock("Outer paddock E", new Vector3(95f, -0.35f, 8f), new Vector3(70f, 0.1f, 140f), paddock,
                    PreferSurfaceBasecolor("tx_grass_kingscote"), new Vector2(16f, 28f));
            }

            BuildCoastalLife();

            var asphalt = new Color(0.2f, 0.22f, 0.24f);
            CreateBlock("Access road", new Vector3(26f, -0.02f, 38f), new Vector3(6.4f, 0.1f, 22f), asphalt,
                PreferSurfaceBasecolor("tx_asphalt_runway"), new Vector2(2.2f, 8f));
            CreateBlock("Access road east", new Vector3(40f, -0.02f, 46.5f), new Vector3(24f, 0.1f, 5.2f), asphalt,
                PreferSurfaceBasecolor("tx_asphalt_runway"), new Vector2(8f, 1.8f));
            CreateTaxiChordPad("Access road elbow", new Vector3(26f, -0.02f, 46f), new Vector3(34f, -0.02f, 46f), 6.2f,
                PreferSurfaceBasecolor("tx_asphalt_runway"), new Vector2(2.2f, 1.8f));
            CreateBlock("Access road stub", new Vector3(26f, -0.02f, 28.5f), new Vector3(6.2f, 0.1f, 6f), asphalt,
                PreferSurfaceBasecolor("tx_asphalt_runway"), new Vector2(2f, 2f));
            CreateBlock("Car park aisle", new Vector3(48f, -0.02f, 46f), new Vector3(16f, 0.08f, 12f), asphalt,
                PreferSurfaceBasecolor("tx_asphalt_runway"), new Vector2(4f, 3f));
            var hasForecourtKerbs = FindBuilt("Car park kerb N") != null;
            CreateBlock("Drop-off zebra", new Vector3(26f, 0.05f, 34.5f), new Vector3(5.5f, 0.02f, 0.35f), Color.white);
            if (!hasForecourtKerbs)
                CreateBlock("Drop-off zebra 2", new Vector3(26f, 0.05f, 33.8f), new Vector3(5.5f, 0.02f, 0.28f), Color.white);
            if (FindBuilt("Parking sign post") == null)
                CreateBlock("Parking sign post", new Vector3(39.5f, 1.1f, 40.5f), new Vector3(0.12f, 2.2f, 0.12f), new Color(0.45f, 0.46f, 0.48f));
            if (FindBuilt("Parking sign face") == null)
                CreateBlock("Parking sign face", new Vector3(39.5f, 2.0f, 40.5f), new Vector3(0.08f, 0.7f, 0.9f), AirsideTheme.SafetyYellow);

            CreateBlock("Service lane", new Vector3(-22f, -0.02f, 28.5f), new Vector3(16f, 0.08f, 2.8f), new Color(0.24f, 0.26f, 0.28f),
                PreferSurfaceBasecolor("tx_asphalt_runway"), new Vector2(4f, 1f));
            CreateTaxiChordPad("Service lane link W", new Vector3(-28f, -0.02f, 28.5f), new Vector3(-22f, -0.02f, 26.5f), 3.2f,
                PreferSurfaceBasecolor("tx_asphalt_runway"), new Vector2(1.1f, 0.9f));
            CreateTaxiChordPad("Service lane link E", new Vector3(-16f, -0.02f, 28.5f), new Vector3(-12f, -0.02f, 20f), 3.4f,
                PreferSurfaceBasecolor("tx_asphalt_runway"), new Vector2(1.2f, 1f));
            CreateBlock("Service lane centreline", new Vector3(-22f, 0.04f, 28.5f), new Vector3(12f, 0.02f, 0.09f),
                new Color(0.95f, 0.85f, 0.2f));
            CreateBlock("Service lane edge N", new Vector3(-22f, 0.04f, 29.8f), new Vector3(12f, 0.02f, 0.07f), Color.white);
            CreateBlock("Service lane edge S", new Vector3(-22f, 0.04f, 27.2f), new Vector3(12f, 0.02f, 0.07f), Color.white);

            BuildPerimeterFence();
            BuildApproachLightBars();
            BuildArffRescueShed();
            BuildVegetation();
            BuildTerrainMicroRelief();
            BuildBuildingContactShadows();
            BuildDistantHills();
            BuildHorizonDome();
            if (AirsideFocusMode.ShowGroundVehicles)
                BuildLandsideLife();
            if (AirsideFocusMode.ShowPeople)
                BuildApronLife();
        }

        /// <summary>
        /// Soft grass/sand mounds around the airfield so the ground plane reads as
        /// terrain rather than a flat slab (0025 item 3). Presentation only.
        /// </summary>
        private static void BuildTerrainMicroRelief()
        {
            var terrainKit = PreferArtKit(
                "Models/Environment/mdl_kingscote_context_terrain_v02.gltf",
                "Models/Environment/mdl_kingscote_context_terrain_v01.gltf");
            if (!string.IsNullOrEmpty(terrainKit) && ArtGltfLoader.HasKit(terrainKit))
                return;

            var grass = Shade(AirsideTheme.Eucalyptus, 0.62f);
            var dry = Shade(AirsideTheme.DryGrass, 0.9f);
            var sand = Shade(AirsideTheme.Sand, 0.95f);
            CreateBlock("Relief berm N", new Vector3(-8f, 0.28f, 38f), new Vector3(36f, 0.7f, 6f), grass,
                PreferSurfaceBasecolor("tx_grass_kingscote"), new Vector2(8f, 2f));
            CreateBlock("Relief berm S", new Vector3(6f, 0.22f, -24f), new Vector3(40f, 0.55f, 8f), dry,
                PreferSurfaceBasecolor("tx_grass_kingscote"), new Vector2(8f, 2.2f));
            CreateBlock("Dune mound west", new Vector3(-95f, 0.35f, -175f), new Vector3(28f, 0.7f, 18f), sand,
                PreferSurfaceBasecolor("tx_sand_coast"), new Vector2(4f, 2f));
            CreateBlock("Dune mound east", new Vector3(155f, 0.28f, -168f), new Vector3(22f, 0.55f, 14f), sand,
                PreferSurfaceBasecolor("tx_sand_coast"), new Vector2(3.5f, 1.8f));
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
            PlacePerson(root, "Stand 2 marshaller", new Vector3(19.8f, 0f, 24f), 185f, new Color(0.9f, 0.5f, 0.1f),
                hiVis: true, marshallerWand: true);
            PlacePerson(root, "Baggage handler", new Vector3(20.5f, 0f, 19.5f), 250f, new Color(0.3f, 0.45f, 0.55f), hiVis: true);
            PlacePerson(root, "Bench sitter", new Vector3(29.5f, 0.15f, 31.5f), 0f, new Color(0.35f, 0.3f, 0.28f), seated: true);

            if (hasChrKits)
            {
                // Fidelity board CHR sheet — keep nine readable silhouettes, not a carpet:
                // hero set already covers three ramp roles + stand/sit passengers; add one
                // walker and one seated passenger so stand/walk/sit all read from overview.
                PlacePerson(root, "Ramp walker", new Vector3(18.5f, 0f, 14.2f), 95f, new Color(0.55f, 0.35f, 0.18f), hiVis: true);
                PlacePerson(root, "Bench sitter B", new Vector3(30.8f, 0.15f, 31.2f), 10f, new Color(0.25f, 0.35f, 0.4f), seated: true);
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
                    ParentBlock(root, $"{name} wand L", new Vector3(-0.42f, bodyY + 0.35f, 0.05f),
                        new Vector3(0.05f, 0.55f, 0.05f), new Color(0.95f, 0.2f, 0.15f));
                    ParentBlock(root, $"{name} wand tip L", new Vector3(-0.42f, bodyY + 0.65f, 0.05f),
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
                // Silhouette board shows two wands — mirror a second into the other hand.
                if (ArtGltfLoader.TryPlaceNamedMesh(kitPath, $"{prefix}_wand", Vector3.zero, Quaternion.identity,
                        new Color(0.95f, 0.2f, 0.15f), out var wandL))
                {
                    wandL.SetParent(root, false);
                    wandL.localPosition = new Vector3(-0.42f, 0f, 0.05f);
                    wandL.localRotation = Quaternion.Euler(0f, 0f, 12f);
                    wandL.name = $"{name} wand L";
                    placed++;
                }

                if (ArtGltfLoader.TryPlaceNamedMesh(kitPath, $"{prefix}_wand_tip", Vector3.zero, Quaternion.identity,
                        new Color(1f, 0.85f, 0.2f), out var tipL))
                {
                    tipL.SetParent(root, false);
                    tipL.localPosition = new Vector3(-0.42f, 0.3f, 0.05f);
                    tipL.name = $"{name} wand tip L";
                    placed++;
                }
            }

            return placed >= 3;
        }

        private void UpdateApronLife()
        {
            if (!AirsideFocusMode.ShowPeople)
                return;

            if (_apronLifeRoot == null)
                _apronLifeRoot = AirsideSceneIndex.Find("Apron life");

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
                // Freeze when the sim is paused so apron life matches aircraft/GSE.
                if (_paused)
                    continue;

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

                foreach (var child in AirsideNamedChildren.Get(person))
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
                        var left = IsLeftSideLimb(child.name);
                        var swing = Mathf.Sin(Time.unscaledTime * AirsideReusableMotion.ApronWaveHz * Mathf.PI * 2f * 1.25f
                            + (left ? 0f : 1.2f)) * 35f;
                        child.localEulerAngles = new Vector3(swing, 0f, left ? -12f : 12f);
                    }
                    else if (walker && child.name.IndexOf("leg", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var left = IsLeftSideLimb(child.name);
                        var stride = Mathf.Sin(Time.unscaledTime * AirsideReusableMotion.ApronStrideHz
                            + (left ? 0f : 3.14f)) * 18f;
                        child.localEulerAngles = new Vector3(stride, 0f, 0f);
                    }
                }
            }
        }

        /// <summary>
        /// Left arm/leg detection — do not use name.Contains("L") (matches "Marshaller").
        /// Prefer " arm L" / ends with " L" / "arm_l" / "leg_l".
        /// </summary>
        private static bool IsLeftSideLimb(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
            if (name.EndsWith(" L", StringComparison.Ordinal) || name.EndsWith("_L", StringComparison.Ordinal))
                return true;
            if (name.EndsWith(" R", StringComparison.Ordinal) || name.EndsWith("_R", StringComparison.Ordinal))
                return false;
            var lower = name.ToLowerInvariant();
            if (lower.Contains(" arm l") || lower.Contains(" leg l") || lower.Contains("arm_l") || lower.Contains("leg_l")
                || lower.EndsWith("_l") || lower.EndsWith(" l"))
                return true;
            return false;
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
            CreateBlock("Jetty deck", new Vector3(-18f, -0.15f, -52.2f), new Vector3(2.2f, 0.16f, 12.4f), timber);
            CreateBlock("Jetty approach", new Vector3(-18f, -0.12f, -44f), new Vector3(2.8f, 0.12f, 3.4f), Shade(AirsideTheme.Concrete, 0.9f));
            CreateBlock("Jetty rail L", new Vector3(-19.1f, 0.35f, -52f), new Vector3(0.08f, 0.7f, 10f), Shade(timber, 0.85f));
            CreateBlock("Jetty rail R", new Vector3(-16.9f, 0.35f, -52f), new Vector3(0.08f, 0.7f, 10f), Shade(timber, 0.85f));
            CreateBlock("Jetty pile L", new Vector3(-19f, -0.55f, -52f), new Vector3(0.28f, 0.9f, 0.28f), pile);
            CreateBlock("Jetty pile R", new Vector3(-17f, -0.55f, -52f), new Vector3(0.28f, 0.9f, 0.28f), pile);
            CreateBlock("Jetty bollard A", new Vector3(-19.0f, 0.25f, -46.5f), new Vector3(0.22f, 0.55f, 0.22f), new Color(0.25f, 0.26f, 0.28f));
            CreateBlock("Jetty bollard B", new Vector3(-17.0f, 0.25f, -46.5f), new Vector3(0.22f, 0.55f, 0.22f), new Color(0.25f, 0.26f, 0.28f));
            CreateBlock("Jetty end cap", new Vector3(-18f, -0.05f, -58f), new Vector3(2.5f, 0.2f, 0.35f), Shade(timber, 0.8f));

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

            // Rock outcrops along the sand — prefer VEG-002 scrub kit rocks (v02 adds rock_c).
            var rock = new Color(0.82f, 0.78f, 0.72f);
            PlaceCoastRock("Coast rock A", new Vector3(-28f, -0.25f, -50f), "rock_a", rock, 4.2f, 18f);
            PlaceCoastRock("Coast rock B", new Vector3(18f, -0.2f, -49f), "rock_b", new Color(0.62f, 0.42f, 0.32f), 3.6f, -12f);
            PlaceCoastRock("Coast rock C", new Vector3(42f, -0.3f, -51.5f), "rock_c", Shade(rock, 1.05f), 4.4f, 40f);
            PlaceCoastRock("Coast rock D", new Vector3(-8f, -0.22f, -50.5f), "rock_a", Shade(rock, 0.95f), 3.2f, -25f);
        }

        /// <summary>VEG-002 rock accents on the KI shoreline; cube blocks remain fallback.</summary>
        private static void PlaceCoastRock(string name, Vector3 position, string mesh, Color color, float scale, float yawDegrees)
        {
            var kit = PreferArtKit(
                "Models/Environment/mdl_kingscote_scrub_kit_v02.gltf",
                "Models/Environment/mdl_kingscote_scrub_kit_v01.gltf");
            if (!string.IsNullOrEmpty(kit)
                && ArtGltfLoader.TryPlaceNamedMesh(
                    kit, mesh, position, Quaternion.Euler(0f, yawDegrees, 0f), color, out var part,
                    localScale: Vector3.one * scale))
            {
                part.name = name;
                return;
            }

            var size = mesh == "rock_b" || mesh == "rock_c"
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
                        renderer.sharedMaterial = AirsideMaterialLibrary.CreateShared(
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

            // Car park bays — place into the same row/column layout as Bay line paint.
            var hasCarPrefab = ArtPresentationLoader.HasPresentation("Models/Vehicles/mdl_parked_car_v02.gltf")
                               || ArtPresentationLoader.HasPrefab("mdl_parked_car_v02")
                               || ArtPresentationLoader.HasPrefab("mdl_parked_car_v01");
            var hasForecourtKerbsEarly = FindBuilt("Car park kerb N") != null;
            var bayCarCount = hasCarPrefab ? (hasForecourtKerbsEarly ? 6 : 4) : 8;
            for (var i = 0; i < bayCarCount; i++)
            {
                if (hasCarPrefab)
                {
                    if (hasForecourtKerbsEarly)
                    {
                        var cols = 3;
                        var row = i / cols;
                        var col = i % cols;
                        var x = 42.5f + col * 5.0f;
                        var z = 43f + row * 4.0f;
                        PlaceParkedCar($"Parked car {i}", new Vector3(x, 0f, z), 180f, carColors[i % carColors.Length]);
                    }
                    else
                    {
                        PlaceParkedCar($"Parked car {i}", new Vector3(42f + i * 3.6f, 0f, 43.2f), 180f, carColors[i % carColors.Length]);
                    }
                }
                else
                {
                    var row = i < 4 ? 0 : 1;
                    var slot = i % 4;
                    PlaceParkedCar($"Parked car {i}", new Vector3(42f + slot * 3.6f, 0f, 43.2f + row * 4.2f), 180f, carColors[i % carColors.Length]);
                }
            }

            // Kerbside drop-off on the access road.
            PlaceParkedCar("Drop-off car", new Vector3(23.5f, 0f, 36f), 0f, carColors[2]);
            PlaceParkedCar("Taxi wait", new Vector3(28.5f, 0f, 36.5f), 8f, new Color(0.92f, 0.78f, 0.15f));

            // Landside furniture: skip near-terminal bench/trolley when PRP-003 forecourt already placed them.
            var forecourtPlaced = FindBuilt("Terminal bench") != null
                                  || FindBuilt("Trolley rail") != null;
            if (!forecourtPlaced)
            {
                PlaceLuggageTrolley("Luggage trolley A", new Vector3(24f, 0f, 31.5f), -15f);
                PlaceLuggageTrolley("Luggage trolley B", new Vector3(25.2f, 0f, 31.5f), 8f);
                if (!hasCarPrefab)
                    PlaceLuggageTrolley("Luggage trolley C", new Vector3(24.6f, 0f, 30.6f), 175f);
                PlaceLandsideBench("Landside bench", new Vector3(29.5f, 0f, 31.2f), 0f);
            }

            PlaceLandsideBench("Car park bench", new Vector3(40.5f, 0f, 40.2f), 90f);

            // Skip PlaceTree positions that overlap the BuildVegetation belt (already placed).
            // Remaining landside trees: only drop-off fringe not covered by veg belt.
            PlaceTree(new Vector3(20f, 0f, 44f), 0.85f);

            // Overflow bay row — one hero ute + visitor when prefab cars are present.
            if (hasCarPrefab)
            {
                PlaceParkedCar("Staff ute", new Vector3(49.2f, 0f, 51.2f), 180f, new Color(0.55f, 0.55f, 0.22f));
                PlaceParkedCar("Visitor car", new Vector3(38.5f, 0f, 47.5f), 0f, new Color(0.6f, 0.15f, 0.2f));
            }
            else
            {
                PlaceParkedCar("Overflow car A", new Vector3(42f, 0f, 51.2f), 180f, new Color(0.45f, 0.2f, 0.18f));
                PlaceParkedCar("Overflow car B", new Vector3(45.6f, 0f, 51.2f), 180f, new Color(0.7f, 0.72f, 0.75f));
                PlaceParkedCar("Staff ute", new Vector3(49.2f, 0f, 51.2f), 180f, new Color(0.55f, 0.55f, 0.22f));
                PlaceParkedCar("Overflow car C", new Vector3(52.8f, 0f, 51.2f), 180f, new Color(0.25f, 0.3f, 0.45f));
                PlaceParkedCar("Visitor car", new Vector3(38.5f, 0f, 47.5f), 0f, new Color(0.6f, 0.15f, 0.2f));
                if (!forecourtPlaced)
                    PlaceLuggageTrolley("Luggage trolley D", new Vector3(23.4f, 0f, 30.2f), 40f);
            }

            PlaceLandsideBench("Access bench", new Vector3(22f, 0f, 40.5f), 90f);

            // Painted parking bay chevrons — thin when PRP-003 kerbs already frame the park.
            var hasForecourtKerbs = FindBuilt("Car park kerb N") != null;
            var bayPaint = new Color(0.92f, 0.92f, 0.88f);
            if (hasForecourtKerbs)
            {
                CreateBlock("Bay line W", new Vector3(42.0f, 0.06f, 43.2f), new Vector3(0.08f, 0.02f, 3.4f), bayPaint);
                CreateBlock("Bay line E", new Vector3(50.5f, 0.06f, 43.2f), new Vector3(0.08f, 0.02f, 3.4f), bayPaint);
                CreateBlock("Bay stop", new Vector3(46.25f, 0.06f, 41.6f), new Vector3(8.5f, 0.02f, 0.08f), bayPaint);
            }
            else
            {
                CreateBlock("Bay line W", new Vector3(40.5f, 0.06f, 43.2f), new Vector3(0.08f, 0.02f, 3.4f), bayPaint);
                CreateBlock("Bay line E", new Vector3(54.3f, 0.06f, 43.2f), new Vector3(0.08f, 0.02f, 3.4f), bayPaint);
                CreateBlock("Bay stop", new Vector3(47.4f, 0.06f, 41.6f), new Vector3(14.4f, 0.02f, 0.08f), bayPaint);
                CreateBlock("Overflow bay W", new Vector3(40.5f, 0.06f, 51.2f), new Vector3(0.08f, 0.02f, 3.0f), bayPaint);
                CreateBlock("Overflow bay E", new Vector3(54.3f, 0.06f, 51.2f), new Vector3(0.08f, 0.02f, 3.0f), bayPaint);
                CreateBlock("Overflow stop", new Vector3(47.4f, 0.06f, 52.6f), new Vector3(14.4f, 0.02f, 0.08f), bayPaint);
            }

            CreateBlock("Access dash", new Vector3(26f, 0.06f, hasForecourtKerbs ? 43.3f : 44.3f),
                new Vector3(0.35f, 0.02f, hasForecourtKerbs ? 12.8f : 14.4f), new Color(0.95f, 0.9f, 0.35f));

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
        /// Prefer landside car v02 kit/prefab, then Resources prefab, then procedural cuboids.
        /// </summary>
        private static void PlaceParkedCar(string name, Vector3 position, float yawDegrees, Color body)
        {
            Transform root = null;
            var kit = PreferArtKit("Models/Vehicles/mdl_parked_car_v02.gltf");
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
                    renderer.sharedMaterial = AirsideMaterialLibrary.CreateShared(color,
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
            var kit = PreferArtKit(
                "Models/Props/mdl_terminal_forecourt_kit_v02.gltf",
                "Models/Props/mdl_terminal_forecourt_kit_v01.gltf");
            if (!string.IsNullOrEmpty(kit) && ArtGltfLoader.HasKit(kit))
            {
                var steel = new Color(0.7f, 0.72f, 0.75f);
                var rot = Quaternion.Euler(0f, yawDegrees, 0f);
                if (ArtGltfLoader.TryPlaceCombined(
                        kit,
                        new[]
                        {
                            ("trolley_rail", steel),
                            ("trolley_post_l", steel),
                            ("trolley_post_r", steel)
                        },
                        position, rot, name, out var combined))
                {
                    combined.position = position;
                    combined.rotation = rot;
                    return;
                }

                root = new GameObject(name).transform;
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
            var kit = PreferArtKit(
                "Models/Props/mdl_terminal_forecourt_kit_v02.gltf",
                "Models/Props/mdl_terminal_forecourt_kit_v01.gltf");
            if (!string.IsNullOrEmpty(kit) && ArtGltfLoader.HasKit(kit))
            {
                var wood = new Color(0.4f, 0.32f, 0.22f);
                var steel = new Color(0.45f, 0.46f, 0.48f);
                var rot = Quaternion.Euler(0f, yawDegrees, 0f);
                if (ArtGltfLoader.TryPlaceCombined(
                        kit,
                        new[]
                        {
                            ("bench_seat", wood),
                            ("bench_back", Shade(wood, 0.9f)),
                            ("bench_leg_l", steel),
                            ("bench_leg_r", steel)
                        },
                        position, rot, name, out var combined))
                {
                    combined.position = position;
                    combined.rotation = rot;
                    return;
                }

                root = new GameObject(name).transform;
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
                var parts = AirsideRuntimeQuality.PlaceFenceRails
                    ? new[]
                    {
                        ("fence_bay", panel),
                        ("fence_bay_post_l", post),
                        ("fence_bay_post_r", post),
                        ("fence_bay_rail_top", post),
                        ("fence_bay_rail_mid", post),
                        ("fence_bay_rail_bot", post),
                        ("fence_bay_cap_l", post),
                        ("fence_bay_cap_r", post)
                    }
                    : new[]
                    {
                        ("fence_bay", panel),
                        ("fence_bay_post_l", post),
                        ("fence_bay_post_r", post)
                    };
                if (ArtGltfLoader.TryPlaceCombined(kit, parts, pos, rot, $"Fence bay {tag}", out _))
                {
                    placed++;
                    return;
                }

                PlacePart("fence_bay", pos, rot, panel, $"Fence bay {tag}");
                PlacePart("fence_bay_post_l", pos, rot, post, $"Fence bay post L {tag}");
                PlacePart("fence_bay_post_r", pos, rot, post, $"Fence bay post R {tag}");
                if (!AirsideRuntimeQuality.PlaceFenceRails)
                    return;
                PlacePart("fence_bay_rail_top", pos, rot, post, $"Fence bay rail top {tag}");
                PlacePart("fence_bay_rail_mid", pos, rot, post, $"Fence bay rail mid {tag}");
                PlacePart("fence_bay_rail_bot", pos, rot, post, $"Fence bay rail bot {tag}");
                PlacePart("fence_bay_cap_l", pos, rot, post, $"Fence bay cap L {tag}");
                PlacePart("fence_bay_cap_r", pos, rot, post, $"Fence bay cap R {tag}");
            }

            // North landside (gap for vehicle gate at x≈22–30).
            for (var x = -40; x <= 56; x += 4)
            {
                if (x >= 22 && x <= 30)
                    continue;
                PlaceBay(new Vector3(x + 2f, 0f, 34f), 0f, $"N {x}");
            }

            // West / east airside — east faces inward with -90 yaw.
            for (var z = -18; z <= 32; z += 4)
            {
                PlaceBay(new Vector3(-44f, 0f, z + 2f), 90f, $"W {z}");
                PlaceBay(new Vector3(44f, 0f, z + 2f), -90f, $"E {z}");
            }

            // South above dunes (gap at runway strip).
            for (var x = -40; x <= 40; x += 4)
            {
                if (x >= -12 && x <= 12)
                    continue;
                PlaceBay(new Vector3(x + 2f, 0f, -20f), 0f, $"S {x}");
            }

            void PlaceCombo(string name, Vector3 pos, Quaternion rot, params (string Name, Color Color)[] parts)
            {
                if (ArtGltfLoader.TryPlaceCombined(kit, parts, pos, rot, name, out _))
                {
                    placed++;
                    return;
                }

                for (var i = 0; i < parts.Length; i++)
                    PlacePart(parts[i].Name, pos, rot, parts[i].Color, $"{name} {parts[i].Name}");
            }

            PlaceCombo("Fence corner NW", new Vector3(-44f, 0f, 34f), Quaternion.identity,
                ("fence_corner", post), ("fence_corner_brace", post));
            PlaceCombo("Fence corner NE", new Vector3(44f, 0f, 34f), Quaternion.identity,
                ("fence_corner", post), ("fence_corner_brace", post));
            PlaceCombo("Fence corner SW", new Vector3(-44f, 0f, -20f), Quaternion.identity,
                ("fence_corner", post), ("fence_corner_brace", post));
            PlaceCombo("Fence corner SE", new Vector3(44f, 0f, -20f), Quaternion.identity,
                ("fence_corner", post), ("fence_corner_brace", post));

            // Vehicle gate at access road. Leaves keep unique yaw; the rest share identity rotation.
            PlacePart("gate_vehicle_leaf_l", new Vector3(24.2f, 0f, 35.6f), Quaternion.Euler(0f, 12f, 0f), yellow, "Gate leaf L");
            PlacePart("gate_vehicle_leaf_r", new Vector3(27.8f, 0f, 35.6f), Quaternion.Euler(0f, -12f, 0f), yellow, "Gate leaf R");
            var gateOrigin = new Vector3(26f, 0f, 34f);
            var chevron = new Color(0.15f, 0.15f, 0.16f);
            var light = new Color(0.95f, 0.35f, 0.12f);
            if (ArtGltfLoader.TryPlaceCombined(
                    kit,
                    new[]
                    {
                        ("gate_post", post),
                        ("gate_post", post),
                        ("gate_vehicle_rail", post),
                        ("gate_vehicle_chevron", chevron),
                        ("gate_vehicle_chevron", chevron),
                        ("gate_sign", AirsideTheme.SafetyYellow),
                        ("gate_sign_frame", post),
                        ("gate_post_light", light),
                        ("gate_post_light", light),
                        ("gate_latch", new Color(0.25f, 0.26f, 0.28f)),
                        ("gate_stop", AirsideTheme.Concrete),
                        ("gate_stop", AirsideTheme.Concrete)
                    },
                    gateOrigin, Quaternion.identity, "Vehicle gate", out _,
                    localOffsets: new[]
                    {
                        new Vector3(-3f, 0f, 0f),
                        new Vector3(3f, 0f, 0f),
                        new Vector3(0f, 0f, 1.55f),
                        new Vector3(-1.8f, 0f, 1.5f),
                        new Vector3(1.8f, 0f, 1.5f),
                        new Vector3(0f, 0f, 0.2f),
                        new Vector3(0f, 0f, 0.15f),
                        new Vector3(-3f, 0f, 0.15f),
                        new Vector3(3f, 0f, 0.15f),
                        new Vector3(0f, 0f, 1.5f),
                        new Vector3(-2.9f, 0f, 0.4f),
                        new Vector3(2.9f, 0f, 0.4f)
                    }))
            {
                placed++;
            }
            else
            {
                PlacePart("gate_post", new Vector3(23f, 0f, 34f), Quaternion.identity, post, "Gate post L");
                PlacePart("gate_post", new Vector3(29f, 0f, 34f), Quaternion.identity, post, "Gate post R");
                PlacePart("gate_vehicle_rail", new Vector3(26f, 0f, 35.55f), Quaternion.identity, post, "Gate vehicle rail");
                PlacePart("gate_vehicle_chevron", new Vector3(24.2f, 0f, 35.5f), Quaternion.identity, chevron, "Gate chevron L");
                PlacePart("gate_vehicle_chevron", new Vector3(27.8f, 0f, 35.5f), Quaternion.identity, chevron, "Gate chevron R");
                PlaceCombo("Gate sign", new Vector3(26f, 0f, 34.2f), Quaternion.identity,
                    ("gate_sign", AirsideTheme.SafetyYellow), ("gate_sign_frame", post));
                PlacePart("gate_post_light", new Vector3(23f, 0f, 34.15f), Quaternion.identity, light, "Gate light L");
                PlacePart("gate_post_light", new Vector3(29f, 0f, 34.15f), Quaternion.identity, light, "Gate light R");
                PlacePart("gate_latch", new Vector3(26f, 0f, 35.5f), Quaternion.identity, new Color(0.25f, 0.26f, 0.28f), "Gate latch");
                PlacePart("gate_stop", new Vector3(23.1f, 0f, 34.4f), Quaternion.identity, AirsideTheme.Concrete, "Gate stop L");
                PlacePart("gate_stop", new Vector3(28.9f, 0f, 34.4f), Quaternion.identity, AirsideTheme.Concrete, "Gate stop R");
            }
            PlaceCombo("Pedestrian gate", new Vector3(20.6f, 0f, 34f), Quaternion.identity,
                ("gate_pedestrian", panel), ("gate_pedestrian_frame", post));
            PlaceCombo("Pedestrian gate E", new Vector3(31.4f, 0f, 34f), Quaternion.identity,
                ("gate_pedestrian", panel), ("gate_pedestrian_frame", post));

            return placed >= 20;
        }

        private static void BuildPerimeterFence()
        {
            // Perimeter fence removed — the kit ribbon blocked sight lines on the runway
            // and read as a cage around the airfield rather than a distant landside boundary.
            return;

            // Batch F3 PRP-002 — modular fence/gate kit; dense CreateBlock ribbon remains fallback.
            if (TryBuildPerimeterFenceFromKit())
                return;

            var post = new Color(0.55f, 0.56f, 0.58f);
            var rail = new Color(0.72f, 0.74f, 0.76f);
            var mesh = new Color(0.62f, 0.64f, 0.66f);
            CreateBlock("Fence N west", new Vector3(-9f, 0.75f, 34f), new Vector3(62f, 1.35f, 0.08f), mesh);
            CreateBlock("Fence N east", new Vector3(43f, 0.75f, 34f), new Vector3(26f, 1.35f, 0.08f), mesh);
            CreateBlock("Fence rail N west", new Vector3(-9f, 1.25f, 34f), new Vector3(62f, 0.05f, 0.05f), rail);
            CreateBlock("Fence rail N east", new Vector3(43f, 1.25f, 34f), new Vector3(26f, 0.05f, 0.05f), rail);
            CreateBlock("Fence W", new Vector3(-44f, 0.75f, 7f), new Vector3(0.08f, 1.35f, 50f), mesh);
            CreateBlock("Fence E", new Vector3(44f, 0.75f, 7f), new Vector3(0.08f, 1.35f, 50f), mesh);
            CreateBlock("Fence rail W", new Vector3(-44f, 1.25f, 7f), new Vector3(0.05f, 0.05f, 50f), rail);
            CreateBlock("Fence rail E", new Vector3(44f, 1.25f, 7f), new Vector3(0.05f, 0.05f, 50f), rail);

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

            CreateBlock("Fence S west", new Vector3(-26f, 0.7f, -20f), new Vector3(28f, 1.3f, 0.08f), mesh);
            CreateBlock("Fence S east", new Vector3(26f, 0.7f, -20f), new Vector3(28f, 1.3f, 0.08f), mesh);
            CreateBlock("Fence rail S west", new Vector3(-26f, 1.15f, -20f), new Vector3(28f, 0.05f, 0.05f), rail);
            CreateBlock("Fence rail S east", new Vector3(26f, 1.15f, -20f), new Vector3(28f, 0.05f, 0.05f), rail);
            CreateBlock("Gate chevron L", new Vector3(24.2f, 0.85f, 35.45f), new Vector3(1.8f, 0.35f, 0.04f),
                new Color(0.15f, 0.15f, 0.16f));
            CreateBlock("Gate chevron R", new Vector3(27.8f, 0.85f, 35.45f), new Vector3(1.8f, 0.35f, 0.04f),
                new Color(0.15f, 0.15f, 0.16f));
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
            var alsCentre = new[]
            {
                ("edge_base", stem),
                ("edge_stem", stem),
                ("edge_lens", bar),
                ("edge_collar", Shade(stem, 1.1f))
            };
            var alsBar = new[]
            {
                ("edge_base", stem),
                ("edge_stem", stem),
                ("edge_lens", bar),
                ("edge_collar", Shade(stem, 1.1f)),
                ("taxi_base", stem),
                ("taxi_stem", stem),
                ("taxi_lens", bar),
                ("taxi_collar", Shade(stem, 1.05f))
            };
            for (var i = 0; i < stationCount; i++)
            {
                var x = -40f - i * stationStep;
                var origin = new Vector3(x, 0f, 0f);
                var kitStation = ArtGltfLoader.TryPlaceCombined(
                    lightingKit, i % 2 == 0 ? alsBar : alsCentre,
                    origin, Quaternion.identity, $"ALS station {i}", out _);

                if (!kitStation)
                {
                    CreateBlock($"ALS stem {i}", new Vector3(x, 0.35f, 0f), new Vector3(0.12f, 0.7f, 0.12f), stem);
                    CreateBlock($"ALS centre {i}", new Vector3(x, 0.75f, 0f), new Vector3(0.35f, 0.18f, 0.35f), bar);
                    CreateBlock($"ALS bar L {i}", new Vector3(x, 0.7f, -1.4f - i * 0.12f), new Vector3(0.25f, 0.14f, 2.2f + i * 0.18f), bar);
                    CreateBlock($"ALS bar R {i}", new Vector3(x, 0.7f, 1.4f + i * 0.12f), new Vector3(0.25f, 0.14f, 2.2f + i * 0.18f), bar);
                    if (i % 2 == 0)
                        CreateBlock($"ALS cross {i}", new Vector3(x, 0.68f, 0f), new Vector3(0.18f, 0.12f, 3.6f + i * 0.15f), bar);
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
                    if (lensRenderer != null)
                        SetRendererColor(lensRenderer, new Color(1f, 0.97f, 0.88f), new Color(1f, 0.95f, 0.8f) * 1.4f);
                }
            }

            // Far REIL pair — pulsed SpotLights at night (collected with runway edge REIL names).
            var reilOriginL = new Vector3(-78f, 0f, -2.8f);
            var reilOriginR = new Vector3(-78f, 0f, 2.8f);
            var reilParts = new[]
            {
                ("obst_base", stem),
                ("obst_stem", stem),
                ("obst_lens", new Color(1f, 1f, 0.9f)),
                ("obst_guard", Shade(stem, 1.1f)),
                ("obst_ring", new Color(0.95f, 0.35f, 0.12f)),
                ("obst_cap", stem),
                ("obst_beacon_ring", new Color(1f, 0.9f, 0.5f))
            };
            var reilKit = ArtGltfLoader.TryPlaceCombined(
                    lightingKit, reilParts, reilOriginL, Quaternion.identity, "ALS REIL L", out _)
                | ArtGltfLoader.TryPlaceCombined(
                    lightingKit, reilParts, reilOriginR, Quaternion.identity, "ALS REIL R", out _);
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
            if (TryInstantiatePreferredPrefab(out var shed, "mdl_arff_shed_v02", "mdl_arff_shed_v01"))
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
                CreateBlock("ARFF apron WW", new Vector3(-30.025f, 0.018f, 26.78f), new Vector3(1.32f, 0.0576f, 2.716f), AirsideTheme.Concrete,
                    PreferSurfaceBasecolor("tx_concrete_apron"), new Vector2(1f, 1f));
                CreateBlock("ARFF apron WE", new Vector3(-28.725f, 0.02f, 26.82f), new Vector3(1.32f, 0.0576f, 2.716f), Shade(AirsideTheme.Concrete, 0.99f),
                    PreferSurfaceBasecolor("tx_concrete_apron"), new Vector2(1f, 1f));
                CreateBlock("ARFF apron EW", new Vector3(-27.275f, 0.02f, 26.83f), new Vector3(1.25f, 0.055f, 2.63f), Shade(AirsideTheme.Concrete, 0.97f),
                    PreferSurfaceBasecolor("tx_concrete_apron"), new Vector2(1f, 1f));
                CreateBlock("ARFF apron EE", new Vector3(-25.975f, 0.022f, 26.87f), new Vector3(1.25f, 0.055f, 2.63f), Shade(AirsideTheme.Concrete, 0.96f),
                    PreferSurfaceBasecolor("tx_concrete_apron"), new Vector2(1f, 1f));
                // ARFF bay paint — yellow keep-clear cue readable from overview.
                CreateBlock("ARFF bay centre", new Vector3(-28f, 0.055f, 26.6f), new Vector3(0.12f, 0.02f, 2.8f), AirsideTheme.SafetyYellow);
                CreateBlock("ARFF bay edge N", new Vector3(-28f, 0.055f, 28.1f), new Vector3(4.6f, 0.02f, 0.1f), Color.white);
                CreateBlock("ARFF bay edge S", new Vector3(-28f, 0.055f, 25.1f), new Vector3(4.6f, 0.02f, 0.1f), Color.white);
                CreateBlock("ARFF bay stop", new Vector3(-28f, 0.055f, 25.6f), new Vector3(2.2f, 0.02f, 0.12f), AirsideTheme.SafetyYellow);
                CreateTaxiChordPad("ARFF apron link", new Vector3(-28f, 0.02f, 26.5f), new Vector3(-22f, 0.02f, 28.5f), 3.2f,
                    PreferSurfaceBasecolor("tx_asphalt_runway"), new Vector2(1.1f, 0.9f));
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

            if (AirsideFocusMode.ShowGroundVehicles)
                PlaceArffTruck();
        }

        /// <summary>
        /// Decision 0025 items 1+3 — Resources ARFF truck parked on the rescue apron.
        /// </summary>
        private static void PlaceArffTruck()
        {
            Transform root;
            if (TryInstantiatePreferredPrefab(out var prefabRoot, "mdl_arff_truck_v02", "mdl_arff_truck_v01"))
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
            var hasKitCanopy = FindBuilt("canopy") != null
                || FindBuilt("canopy_soffit") != null
                || FindBuilt("canopy_beam") != null;
            if (!hasKitCanopy)
            {
                CreateBlock("Terminal canopy slab W", new Vector3(22f, 3.55f, 31.2f), new Vector3(8f, 0.18f, 4.2f), soffit);
                CreateBlock("Terminal canopy slab E", new Vector3(30f, 3.55f, 31.25f), new Vector3(7.6f, 0.1728f, 4.074f), Shade(soffit, 0.97f));
                CreateBlock("Terminal canopy edge W", new Vector3(22f, 3.4f, 33.1f), new Vector3(8.1f, 0.22f, 0.25f), steel);
                CreateBlock("Terminal canopy edge E", new Vector3(30.1f, 3.4f, 33.15f), new Vector3(7.7f, 0.2112f, 0.2425f), Shade(steel, 0.95f));
                for (var i = 0; i < 5; i++)
                {
                    var x = 18.5f + i * 3.75f;
                    CreateBlock($"Terminal canopy post {i}", new Vector3(x, 1.7f, 32.6f), new Vector3(0.22f, 3.4f, 0.22f), steel);
                }

                CreateBlock("Terminal landside glass W", new Vector3(22.5f, 2.1f, 29.55f), new Vector3(7f, 2.6f, 0.1f), glass,
                    "Textures/Environment/tx_terminal_glass_mask_v01.png", new Vector2(1.25f, 1.2f));
                CreateBlock("Terminal landside glass E", new Vector3(29.5f, 2.1f, 29.55f), new Vector3(6.65f, 2.496f, 0.097f), glass,
                    "Textures/Environment/tx_terminal_glass_mask_v01.png", new Vector2(1.25f, 1.2f));
                CreateBlock("Terminal entrance frame", new Vector3(26f, 1.6f, 29.5f), new Vector3(3.2f, 2.8f, 0.18f), steel);
                CreateBlock("Terminal doors", new Vector3(26f, 1.45f, 29.35f), new Vector3(2.6f, 2.4f, 0.08f), new Color(0.22f, 0.28f, 0.32f));
                CreateBlock("Terminal canopy glow W", new Vector3(22.5f, 3.35f, 31.2f), new Vector3(6f, 0.06f, 3.2f), new Color(1f, 0.85f, 0.55f));
                CreateBlock("Terminal canopy glow E", new Vector3(29.5f, 3.35f, 31.25f), new Vector3(5.7f, 0.0576f, 3.104f), new Color(1f, 0.85f, 0.55f));
            }
            else if (FindBuilt("canopy_light_l") == null
                     && FindBuilt("canopy_light_r") == null
                     && FindBuilt("canopy_light_mid") == null)
            {
                CreateBlock("Terminal canopy glow W", new Vector3(22.5f, 3.35f, 31.2f), new Vector3(6f, 0.06f, 3.2f), new Color(1f, 0.85f, 0.55f));
                CreateBlock("Terminal canopy glow E", new Vector3(29.5f, 3.35f, 31.25f), new Vector3(5.7f, 0.0576f, 3.104f), new Color(1f, 0.85f, 0.55f));
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
            var kit = PreferArtKit(
                "Models/Props/mdl_terminal_forecourt_kit_v02.gltf",
                "Models/Props/mdl_terminal_forecourt_kit_v01.gltf");
            if (string.IsNullOrEmpty(kit) || !ArtGltfLoader.HasKit(kit))
                return false;

            var wood = new Color(0.4f, 0.32f, 0.22f);
            var steel = new Color(0.45f, 0.46f, 0.48f);
            var soil = new Color(0.28f, 0.22f, 0.14f);
            var placed = 0;
            void Place(string mesh, Vector3 pos, Color color, string name, float yawDeg = 0f)
            {
                if (!ArtGltfLoader.TryPlaceNamedMesh(kit, mesh, pos, Quaternion.Euler(0f, yawDeg, 0f), color, out var part))
                    return;
                part.name = name;
                placed++;
            }

            void PlaceCluster(string name, Vector3 pos, float yaw, params (string Name, Color Color)[] parts)
            {
                if (ArtGltfLoader.TryPlaceCombined(kit, parts, pos, Quaternion.Euler(0f, yaw, 0f), name, out _))
                {
                    placed++;
                    return;
                }

                for (var i = 0; i < parts.Length; i++)
                    Place(parts[i].Name, pos, parts[i].Color, name, yaw);
            }

            PlaceCluster("Terminal bench", new Vector3(21f, 0f, 31.6f), 0f,
                ("bench_seat", wood), ("bench_back", Shade(wood, 0.9f)), ("bench_leg_l", steel), ("bench_leg_r", steel));
            PlaceCluster("Terminal planter", new Vector3(31.5f, 0f, 31.8f), 0f,
                ("planter", AirsideTheme.Concrete), ("planter_soil", soil),
                ("planter_scrub", Shade(AirsideTheme.Eucalyptus, 0.85f)));
            PlaceCluster("Terminal planter W", new Vector3(18.5f, 0f, 31.8f), 0f,
                ("planter", AirsideTheme.Concrete), ("planter_soil", soil),
                ("planter_scrub", Shade(AirsideTheme.Eucalyptus, 0.85f)));

            var bollardBody = ArtGltfLoader.HasMesh(kit, "dropoff_bollard") ? "dropoff_bollard" : "bollard";
            void PlaceBollard(string name, Vector3 pos)
            {
                PlaceCluster(name, pos, 0f, (bollardBody, steel), ("bollard_cap", AirsideTheme.SafetyYellow));
            }

            PlaceBollard("Drop-off bollard L", new Vector3(23.5f, 0f, 33.2f));
            PlaceBollard("Drop-off bollard M", new Vector3(26f, 0f, 33.2f));
            PlaceBollard("Drop-off bollard R", new Vector3(28.5f, 0f, 33.2f));
            Place("kerb_straight", new Vector3(26f, 0f, 33.6f), AirsideTheme.Concrete, "Drop-off kerb");
            Place("kerb_corner", new Vector3(23.2f, 0f, 33.6f), AirsideTheme.Concrete, "Drop-off kerb corner L", 0f);
            Place("kerb_corner", new Vector3(28.8f, 0f, 33.6f), AirsideTheme.Concrete, "Drop-off kerb corner R", 90f);
            PlaceCluster("Trolley bay", new Vector3(33.5f, 0f, 30.8f), 0f,
                ("trolley_rail", steel), ("trolley_post_l", steel), ("trolley_post_r", steel));
            PlaceCluster("Parking sign", new Vector3(39.5f, 0f, 40.5f), 0f,
                ("sign_post", steel), ("sign_face", AirsideTheme.SafetyYellow), ("sign_frame", Shade(steel, 0.85f)));
            PlaceCluster("Access sign", new Vector3(44f, 0f, 36f), 0f,
                ("sign_post", steel), ("sign_face", AirsideTheme.SafetyYellow), ("sign_frame", Shade(steel, 0.85f)));
            Place("kerb_straight", new Vector3(48f, 0f, 52.2f), AirsideTheme.Concrete, "Car park kerb N");
            Place("kerb_straight", new Vector3(48f, 0f, 39.8f), AirsideTheme.Concrete, "Car park kerb S");
            Place("kerb_corner", new Vector3(42f, 0f, 52.2f), AirsideTheme.Concrete, "Car park kerb corner NW", 180f);
            Place("kerb_corner", new Vector3(54f, 0f, 52.2f), AirsideTheme.Concrete, "Car park kerb corner NE", -90f);
            return placed >= 6;
        }

        /// <summary>Airside planter strip in front of terminal glass — PRP-003 parts.</summary>
        private static bool TryPlaceAirsidePlanterStrip()
        {
            var kit = PreferArtKit(
                "Models/Props/mdl_terminal_forecourt_kit_v02.gltf",
                "Models/Props/mdl_terminal_forecourt_kit_v01.gltf");
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
                var pos = new Vector3(x, 0f, 24.4f);
                var name = $"Airside planter {x:0}";
                if (ArtGltfLoader.TryPlaceCombined(
                        kit,
                        new[]
                        {
                            ("planter", AirsideTheme.Concrete),
                            ("planter_soil", soil),
                            ("planter_scrub", Shade(AirsideTheme.Eucalyptus, 0.85f))
                        },
                        pos, Quaternion.identity, name, out _))
                {
                    placed++;
                    continue;
                }

                Place("planter", pos, AirsideTheme.Concrete, name);
                Place("planter_soil", pos, soil, $"{name} soil");
                Place("planter_scrub", pos, Shade(AirsideTheme.Eucalyptus, 0.85f), $"{name} scrub");
            }

            return placed >= 3;
        }

        private static void BuildVegetation()
        {
            // Stylised eucalyptus clumps — denser belts so overview reads as KI bush, not
            // a handful of props (0025 item 3). PlaceTree prefers VEG-001 v02→v01.
            var scrubKit = PreferArtKit(
                "Models/Environment/mdl_kingscote_scrub_kit_v02.gltf",
                "Models/Environment/mdl_kingscote_scrub_kit_v01.gltf");
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
            // Place the full belt with authored VEG-001 silhouettes when the kit is
            // present (v02 densifies far paddock too). Primitive greybox still covers
            // every slot if the kit is missing.
            var treeCount = trees.Length;
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
            var inlandCount = inlandScrub.Length;
            for (var i = 0; i < inlandCount; i++)
                PlaceShrub(inlandScrub[i], 0.75f + (i % 5) * 0.1f);

            // Fence-line scrub carpet — denser when VEG-002 kit stamps authored clumps.
            var fenceStep = hasScrubKit ? 5 : 4;
            if (AirsideRuntimeQuality.Current != AirsideRuntimeQuality.Ladder.High)
                fenceStep += 3;
            for (var x = -70; x <= 70; x += fenceStep)
            {
                PlaceShrubClump(new Vector3(x, 0f, 36f + (x % 5) * 0.2f), 0.55f + (Mathf.Abs(x) % 4) * 0.08f);
                if (x % (fenceStep * 2) == 0)
                    PlaceShrubClump(new Vector3(x + 1.5f, 0f, 40f), 0.7f);
            }

            var sideStep = hasScrubKit ? 6 : 5;
            if (AirsideRuntimeQuality.Current != AirsideRuntimeQuality.Ladder.High)
                sideStep += 3;
            for (var z = -20; z <= 50; z += sideStep)
            {
                PlaceShrubClump(new Vector3(-48f - (z % 3) * 0.4f, 0f, z), 0.6f + (Mathf.Abs(z) % 3) * 0.1f);
                PlaceShrubClump(new Vector3(50f + (z % 3) * 0.4f, 0f, z), 0.6f + (Mathf.Abs(z) % 3) * 0.1f);
            }

            // Between apron fringe and N fence.
            var fringeStep = hasScrubKit ? 3 : 3;
            if (AirsideRuntimeQuality.Current != AirsideRuntimeQuality.Ladder.High)
                fringeStep += 3;
            for (var x = 6; x <= 34; x += fringeStep)
                PlaceShrubClump(new Vector3(x, 0f, 28.5f + (x % 2) * 0.4f), 0.5f);

            // Dense coastal scrub belt — prefer VEG-002 clumps over greybox cubes.
            var coastStep = hasScrubKit ? 4 : 5;
            if (AirsideRuntimeQuality.Current != AirsideRuntimeQuality.Ladder.High)
                coastStep += 3;
            for (var x = -55; x <= 55; x += coastStep)
            {
                var zJitter = ((x * 13) % 7) * 0.15f;
                PlaceShrub(new Vector3(x, 0f, -39.5f + zJitter), 0.7f + (Mathf.Abs(x) % 4) * 0.06f);
                if (x % (coastStep * 2) == 0)
                    PlaceShrub(new Vector3(x + 1.5f, 0f, -37.5f), 0.65f);
                // Extra dune-edge stamp so overview matches the scrub style sheet belt.
                if (hasScrubKit && AirsideRuntimeQuality.Current == AirsideRuntimeQuality.Ladder.High && x % 8 == 0)
                    PlaceShrub(new Vector3(x + 0.8f, 0f, -41.2f + zJitter * 0.5f), 0.85f);
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

            EnsureFallbackShrubMesh();
            var go = new GameObject("Shrub");
            go.transform.position = basePosition;
            go.transform.localScale = Vector3.one * scale;
            go.AddComponent<MeshFilter>().sharedMesh = FallbackShrubMesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = FallbackShrubMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (_airfieldRoot != null)
                go.transform.SetParent(_airfieldRoot, true);
            AirsideSceneIndex.Remember(go);
        }

        private static void EnsureFallbackShrubMesh()
        {
            if (FallbackShrubMesh != null)
                return;
            var locals = new[]
            {
                Matrix4x4.TRS(new Vector3(0f, 0.4f, 0f), Quaternion.identity, new Vector3(1.35f, 0.8f, 1.15f)),
                Matrix4x4.TRS(new Vector3(0.45f, 0.35f, -0.3f), Quaternion.identity, new Vector3(0.95f, 0.6f, 0.85f)),
                Matrix4x4.TRS(new Vector3(-0.4f, 0.32f, 0.25f), Quaternion.identity, new Vector3(0.85f, 0.55f, 0.75f)),
                Matrix4x4.TRS(new Vector3(0.15f, 0.28f, 0.45f), Quaternion.identity, new Vector3(0.7f, 0.45f, 0.65f)),
                Matrix4x4.TRS(new Vector3(-0.25f, 0.25f, -0.4f), Quaternion.identity, new Vector3(0.65f, 0.4f, 0.6f))
            };
            FallbackShrubMesh = AirsideMeshUtil.CombineTransformed(BuiltinSphere(), locals);
            FallbackShrubMaterial = AirsideMaterialLibrary.CreateShared(
                Shade(AirsideTheme.DryGrass, 0.85f), AirsideMaterialLibrary.SurfaceKind.Grass);
        }

        /// <summary>Batch F3 VEG-002 — place authored scrub cluster; sphere clumps remain fallback.</summary>
        private static bool TryPlaceScrubFromKit(Vector3 basePosition, float scale)
        {
            var kit = PreferArtKit(
                "Models/Environment/mdl_kingscote_scrub_kit_v02.gltf",
                "Models/Environment/mdl_kingscote_scrub_kit_v01.gltf");
            if (string.IsNullOrEmpty(kit) || !ArtGltfLoader.HasKit(kit))
                return false;

            var variants = new[] { "scrub_a", "scrub_b", "scrub_c", "scrub_d", "scrub_e" };
            var prefix = variants[Math.Abs(basePosition.GetHashCode()) % variants.Length];
            var yaw = (basePosition.x * 23f + basePosition.z * 11f) % 360f;
            var dry = Shade(AirsideTheme.DryGrass, 0.85f);
            var euc = Shade(AirsideTheme.Eucalyptus, 0.72f);
            var parts = new List<(string Name, Color Color)>(8)
            {
                ($"{prefix}_core", dry),
                ($"{prefix}_side", euc),
                ($"{prefix}_side_b", Shade(dry, 0.9f)),
                ($"{prefix}_tuft", Shade(euc, 0.88f))
            };
            var hash = Math.Abs(basePosition.GetHashCode());
            if (hash % 4 == 0)
            {
                var grass = hash % 3 == 0 ? "grass_tuft_c" : (hash % 3 == 1 ? "grass_tuft_a" : "grass_tuft_b");
                parts.Add((grass, Shade(AirsideTheme.DryGrass, 0.95f)));
            }

            if (hash % 5 == 0)
            {
                var rock = hash % 15 == 0 ? "rock_c" : (hash % 10 == 0 ? "rock_b" : "rock_a");
                var limestone = rock == "rock_b"
                    ? new Color(0.62f, 0.42f, 0.32f)
                    : new Color(0.82f, 0.78f, 0.72f);
                parts.Add((rock, limestone));
            }

            if (basePosition.z < -34f && hash % 2 == 0)
                parts.Add((hash % 2 == 0 ? "dune_mix_a" : "dune_mix_b", Shade(AirsideTheme.Sand, 0.85f)));

            return ArtGltfLoader.TryPlaceCombined(
                kit, parts.ToArray(), basePosition, Quaternion.Euler(0f, yaw, 0f),
                $"Scrub {prefix}", out _, Vector3.one * scale);
        }

        private static void PlaceTree(Vector3 basePosition, float scale)
        {
            if (TryPlaceTreeFromKit(basePosition, scale))
                return;

            EnsureFallbackTreeMeshes();
            var yaw = (basePosition.x * 17f + basePosition.z * 13f) % 360f;
            var lean = ((basePosition.x + basePosition.z) % 9f) - 4f;
            var root = new GameObject("Tree");
            root.transform.SetPositionAndRotation(
                basePosition, Quaternion.Euler(lean * 0.6f, yaw, lean * 0.35f));
            root.transform.localScale = Vector3.one * scale;

            var bark = new GameObject("Tree bark");
            bark.transform.SetParent(root.transform, false);
            bark.AddComponent<MeshFilter>().sharedMesh = FallbackTreeBarkMesh;
            bark.AddComponent<MeshRenderer>().sharedMaterial = FallbackTreeBarkMaterial;

            var canopy = new GameObject("Tree canopy");
            canopy.transform.SetParent(root.transform, false);
            canopy.AddComponent<MeshFilter>().sharedMesh = FallbackTreeCanopyMesh;
            canopy.AddComponent<MeshRenderer>().sharedMaterial = FallbackTreeCanopyMaterial;

            if (_airfieldRoot != null)
                root.transform.SetParent(_airfieldRoot, true);
            AirsideSceneIndex.Remember(root);
        }

        private static void EnsureFallbackTreeMeshes()
        {
            if (FallbackTreeBarkMesh != null)
                return;
            FallbackTreeBarkMesh = AirsideMeshUtil.CombineTransformed(
                BuiltinCylinder(),
                new[]
                {
                    Matrix4x4.TRS(new Vector3(0f, 1.55f, 0f), Quaternion.identity, new Vector3(0.22f, 1.55f, 0.22f)),
                    Matrix4x4.TRS(new Vector3(0f, 0.12f, 0f), Quaternion.identity, new Vector3(0.42f, 0.12f, 0.42f)),
                    Matrix4x4.TRS(new Vector3(0f, 0.85f, 0f), Quaternion.identity, new Vector3(0.28f, 0.08f, 0.28f)),
                    Matrix4x4.TRS(new Vector3(0f, 1.7f, 0f), Quaternion.identity, new Vector3(0.26f, 0.07f, 0.26f)),
                    Matrix4x4.TRS(new Vector3(0.25f, 2.4f, -0.15f), Quaternion.Euler(18f, 35f, -12f), new Vector3(0.12f, 0.55f, 0.12f))
                });
            FallbackTreeCanopyMesh = AirsideMeshUtil.CombineTransformed(
                BuiltinSphere(),
                new[]
                {
                    Matrix4x4.TRS(new Vector3(0f, 3.35f, 0f), Quaternion.identity, new Vector3(2.0f, 1.55f, 1.9f)),
                    Matrix4x4.TRS(new Vector3(0.65f, 2.85f, -0.45f), Quaternion.identity, new Vector3(1.45f, 1.15f, 1.35f)),
                    Matrix4x4.TRS(new Vector3(-0.55f, 2.95f, 0.5f), Quaternion.identity, new Vector3(1.25f, 1.05f, 1.2f)),
                    Matrix4x4.TRS(new Vector3(0.35f, 3.55f, 0.35f), Quaternion.identity, new Vector3(1.05f, 0.85f, 1.0f)),
                    Matrix4x4.TRS(new Vector3(-0.2f, 2.55f, -0.55f), Quaternion.identity, new Vector3(0.95f, 0.75f, 0.9f))
                });
            FallbackTreeBarkMaterial = AirsideMaterialLibrary.CreateShared(
                new Color(0.32f, 0.24f, 0.15f), AirsideMaterialLibrary.SurfaceKind.PaintedMetal);
            FallbackTreeCanopyMaterial = AirsideMaterialLibrary.CreateShared(
                Shade(AirsideTheme.Eucalyptus, 0.9f), AirsideMaterialLibrary.SurfaceKind.Grass);
        }

        private static Mesh BuiltinSphere()
        {
            if (BuiltinSphereMesh != null)
                return BuiltinSphereMesh;
            var temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            BuiltinSphereMesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(temp);
            return BuiltinSphereMesh;
        }

        private static Mesh BuiltinCube()
        {
            if (BuiltinCubeMesh != null)
                return BuiltinCubeMesh;
            var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            BuiltinCubeMesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(temp);
            return BuiltinCubeMesh;
        }

        private static Mesh BuiltinCylinder()
        {
            if (BuiltinCylinderMesh != null)
                return BuiltinCylinderMesh;
            var temp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            BuiltinCylinderMesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(temp);
            return BuiltinCylinderMesh;
        }

        /// <summary>Batch F3 VEG-001 — place authored eucalyptus silhouette; primitives remain fallback.</summary>
        private static bool TryPlaceTreeFromKit(Vector3 basePosition, float scale)
        {
            var kit = PreferArtKit(
                "Models/Environment/mdl_eucalyptus_kit_v02.gltf",
                "Models/Environment/mdl_eucalyptus_kit_v01.gltf");
            if (string.IsNullOrEmpty(kit) || !ArtGltfLoader.HasKit(kit))
                return false;

            var variants = new[] { "tree_a", "tree_b", "tree_c" };
            var prefix = variants[Math.Abs(basePosition.GetHashCode()) % variants.Length];
            var yaw = (basePosition.x * 17f + basePosition.z * 13f) % 360f;
            var lean = ((basePosition.x + basePosition.z) % 9f) - 4f;
            var bark = new Color(0.32f, 0.24f, 0.15f);
            var canopyA = Shade(AirsideTheme.Eucalyptus, 0.9f);
            var canopyB = Shade(AirsideTheme.Eucalyptus, 0.78f);
            var parts = new List<(string Name, Color Color)>(12)
            {
                ($"{prefix}_trunk", bark),
                ($"{prefix}_flare", Shade(bark, 0.85f)),
                ($"{prefix}_bark_low", new Color(0.38f, 0.28f, 0.16f)),
                ($"{prefix}_bark_mid", new Color(0.36f, 0.26f, 0.15f)),
                ($"{prefix}_fork", Shade(bark, 0.9f)),
                ($"{prefix}_canopy", canopyA),
                ($"{prefix}_canopy_b", canopyB),
                ($"{prefix}_canopy_c", Shade(canopyA, 0.85f)),
                ($"{prefix}_canopy_d", Shade(canopyB, 0.92f))
            };
            if (Mathf.Abs(basePosition.x) > 55f || Mathf.Abs(basePosition.z) > 50f)
                parts.Add(($"{prefix}_lod1", Shade(canopyA, 0.88f)));

            return ArtGltfLoader.TryPlaceCombined(
                kit, parts.ToArray(), basePosition,
                Quaternion.Euler(lean * 0.35f, yaw, lean * 0.2f),
                $"Eucalyptus {prefix}", out _, Vector3.one * scale);
        }

        /// <summary>
        /// Batch F3 WLD-004 — soft hill/dune accents outside operational geometry.
        /// Does not replace runway/apron/stand code-owned surfaces.
        /// </summary>
        private static bool TryPlaceContextTerrainAccents()
        {
            var kit = PreferArtKit(
                "Models/Environment/mdl_kingscote_context_terrain_v02.gltf",
                "Models/Environment/mdl_kingscote_context_terrain_v01.gltf");
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
            // WLD-004 v02 densify — extra mid-horizon hills kept clear of ops.
            Place("hill_b", new Vector3(-78f, 0f, 48f), Quaternion.Euler(0f, 12f, 0f), dry, "Context hill NW mid", 1.9f);
            Place("hill_c", new Vector3(82f, 0f, 46f), Quaternion.Euler(0f, -18f, 0f), euc, "Context hill NE mid", 1.85f);
            Place("dune_a", new Vector3(-40f, 0f, -52f), Quaternion.identity, sand, "Context dune SW", 2.0f);
            Place("dune_b", new Vector3(35f, 0f, -50f), Quaternion.Euler(0f, 15f, 0f), sand, "Context dune SE", 1.9f);
            Place("dune_a", new Vector3(-12f, 0f, -54f), Quaternion.Euler(0f, -8f, 0f), Shade(sand, 0.92f), "Context dune S mid", 1.55f);
            Place("dune_b", new Vector3(12f, 0f, -53f), Quaternion.Euler(0f, 22f, 0f), Shade(sand, 0.95f), "Context dune S mid E", 1.5f);
            Place("berm", new Vector3(0f, 0f, -42f), Quaternion.identity, Shade(sand, 0.9f), "Context coast berm", 2.8f);
            // Near-field coast / paddock accents from the same WLD-004 kit (textured slabs remain).
            Place("coast_sand", new Vector3(-55f, -0.2f, -48f), Quaternion.identity, sand, "Context coast sand W", 1.8f);
            Place("coast_sand", new Vector3(55f, -0.2f, -48f), Quaternion.Euler(0f, 180f, 0f), sand, "Context coast sand E", 1.8f);
            Place("coast_shallows", new Vector3(-30f, -0.5f, -58f), Quaternion.identity, new Color(0.32f, 0.62f, 0.72f), "Context shallows W", 1.4f);
            Place("coast_shallows", new Vector3(30f, -0.5f, -58f), Quaternion.Euler(0f, 180f, 0f), new Color(0.32f, 0.62f, 0.72f), "Context shallows E", 1.4f);
            Place("coast_shallows", new Vector3(0f, -0.45f, -56f), Quaternion.identity, new Color(0.35f, 0.66f, 0.74f), "Context shallows mid", 1.2f);
            Place("coast_water", new Vector3(0f, -0.8f, -70f), Quaternion.identity, new Color(0.18f, 0.38f, 0.52f), "Context coast water", 2.2f);
            Place("paddock_n", new Vector3(-50f, -0.3f, 42f), Quaternion.identity, dry, "Context paddock NW", 2.1f);
            Place("paddock_s", new Vector3(50f, -0.3f, 42f), Quaternion.Euler(0f, 180f, 0f), dry, "Context paddock NE", 2.0f);
            Place("paddock_e", new Vector3(62f, -0.3f, -20f), Quaternion.identity, euc, "Context paddock E", 1.75f);
            Place("paddock_w", new Vector3(-62f, -0.3f, -18f), Quaternion.Euler(0f, 20f, 0f), euc, "Context paddock W", 1.75f);
            return placed > 0;
        }

        private static void BuildDistantHills()
        {
            if (TryPlaceContextTerrainAccents())
                return;

            CreateBlock("Hill far NW", new Vector3(-110f, 4.2f, 70f), new Vector3(36f, 9f, 22f), Shade(AirsideTheme.Eucalyptus, 0.4f),
                PreferSurfaceBasecolor("tx_grass_kingscote"), new Vector2(6f, 3f));
            CreateBlock("Hill far NE", new Vector3(110f, 3.8f, 68f), new Vector3(32f, 8f, 20f), Shade(AirsideTheme.DryGrass, 0.5f),
                PreferSurfaceBasecolor("tx_grass_kingscote"), new Vector2(5.5f, 2.8f));
            CreateBlock("Hill far W", new Vector3(-265f, 5.5f, 40f), new Vector3(36f, 11f, 70f), Shade(AirsideTheme.Eucalyptus, 0.42f),
                PreferSurfaceBasecolor("tx_grass_kingscote"), new Vector2(6f, 8f));
            CreateBlock("Hill far SE", new Vector3(48f, 0.7f, -104f), new Vector3(22f, 2.2f, 16f), Shade(AirsideTheme.Sand, 0.67f),
                PreferSurfaceBasecolor("tx_sand_coast"), new Vector2(4f, 2.5f));
        }

        private static void BuildHorizonDome()
        {
            // Soft inverted dome so the sky is not a flat camera clear-colour void.
            // Unlit-ish pale Open Sky; day/dusk still tint via camera background underneath.
            var dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dome.name = "Horizon dome";
            Object.Destroy(dome.GetComponent<Collider>());
            dome.transform.position = new Vector3(0f, 0f, 0f);
            dome.transform.localScale = new Vector3(330f, 150f, 330f);
            var material = AirsideMaterialLibrary.Create(AirsideTheme.OpenSky, AirsideMaterialLibrary.SurfaceKind.UnlitSky);
            // Render inside of the sphere.
            material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Front);
            // Draw it as a backdrop rather than as geometry. An opaque depth-writing dome
            // hid everything beyond its 165 m radius, so an aircraft joining final from
            // the distance stayed invisible until it crossed the sky wall and popped into
            // existence. Background queue with no depth write can never occlude.
            material.SetInt("_ZWrite", 0);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Background;
            var domeRenderer = dome.GetComponent<Renderer>();
            domeRenderer.sharedMaterial = material;
            domeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            domeRenderer.receiveShadows = false;
            BuildCloudBands();
            BuildSunAndMoonDiscs();
            BuildStarField();
            BuildBirdFlock();
        }

        private static void BuildStarField()
        {
            // One mesh of inward quads — 72 spheres were 72 UnlitSky draw calls.
            var root = new GameObject("Star field").transform;
            var rng = new System.Random(31415);
            const int count = 72;
            var vertices = new Vector3[count * 4];
            var triangles = new int[count * 6];
            var colors = new Color[count * 4];
            for (var i = 0; i < count; i++)
            {
                var yaw = (float)rng.NextDouble() * 360f;
                var pitch = 10f + (float)rng.NextDouble() * 72f;
                var dir = (Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward).normalized;
                // Far enough out to sit beyond the whole flight envelope — an arrival
                // joining final 430 m out must not be occluded by a star. Radius and
                // quad size scale together, so the night sky looks unchanged.
                const float radius = 128f * StarDistanceScale;
                var pos = dir * radius;
                var s = (0.22f + (float)rng.NextDouble() * 0.42f) * StarDistanceScale;
                var bright = 0.65f + (float)rng.NextDouble() * 0.35f;
                var color = new Color(bright, bright, 0.95f * bright, 1f);
                var right = Vector3.Cross(dir, Vector3.up);
                if (right.sqrMagnitude < 0.001f)
                    right = Vector3.right;
                right.Normalize();
                var up = Vector3.Cross(right, dir).normalized;
                var v = i * 4;
                vertices[v] = pos + (-right - up) * s;
                vertices[v + 1] = pos + (right - up) * s;
                vertices[v + 2] = pos + (right + up) * s;
                vertices[v + 3] = pos + (-right + up) * s;
                var t = i * 6;
                triangles[t] = v;
                triangles[t + 1] = v + 1;
                triangles[t + 2] = v + 2;
                triangles[t + 3] = v;
                triangles[t + 4] = v + 2;
                triangles[t + 5] = v + 3;
                for (var k = 0; k < 4; k++)
                    colors[v + k] = color;
            }

            var mesh = new Mesh { name = "Star field" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetColors(colors);
            mesh.RecalculateBounds();
            AirsideMeshUtil.UploadStatic(mesh);

            var go = new GameObject("Star mesh");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = StarSharedMaterial();
            SetRendererColor(renderer, Color.white, new Color(1.2f, 1.2f, 1.35f));
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            root.gameObject.SetActive(false);
        }

        private const float StarDistanceScale = 4f;

        private static Material StarSharedMaterial()
        {
            var mat = AirsideMaterialLibrary.CreateShared(
                Color.white, AirsideMaterialLibrary.SurfaceKind.UnlitSky);
            if (mat.HasProperty("_EmissionColor"))
                mat.EnableKeyword("_EMISSION");
            return mat;
        }

        private void UpdateOpsAntenna()
        {
            if (_opsAntennaDish == null)
            {
                _opsAntennaDish = AirsideSceneIndex.Find("antenna_dish");
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
                _starFieldRoot = AirsideSceneIndex.Find("Star field");
            }

            if (_starFieldRoot == null)
                return;

            var daylight = PresentationDaylight;
            var show = daylight < 0.35f;
            _starFieldRoot.gameObject.SetActive(show);
            if (!show)
                return;

            var twinkle = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * AirsideReusableMotion.StarTwinkleHz);
            if (_starFieldRenderer == null)
            {
                _starFieldRenderer = _starFieldRoot.childCount > 0
                    ? _starFieldRoot.GetChild(0).GetComponent<Renderer>()
                    : _starFieldRoot.GetComponent<Renderer>();
            }

            if (_starFieldRenderer == null)
                return;
            var c = new Color(twinkle, twinkle, 1f) * (1.1f - daylight);
            SetRendererColor(_starFieldRenderer, c, c);
        }

        private static void BuildSunAndMoonDiscs()
        {
            // Visible sun/moon discs so day cycle reads from overview (0025 item 5).
            var sun = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sun.name = "Sun disc";
            Object.Destroy(sun.GetComponent<Collider>());
            sun.transform.localScale = new Vector3(6.5f, 6.5f, 6.5f);
            var sunMat = AirsideMaterialLibrary.CreateShared(
                new Color(1f, 0.92f, 0.65f, 1f),
                AirsideMaterialLibrary.SurfaceKind.UnlitSky);
            if (sunMat.HasProperty("_EmissionColor"))
                sunMat.EnableKeyword("_EMISSION");
            sunMat.SetInt("_ZWrite", 0);
            sunMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Background;
            var sunRenderer = sun.GetComponent<Renderer>();
            sunRenderer.sharedMaterial = sunMat;
            SetRendererColor(sunRenderer, new Color(1f, 0.92f, 0.65f, 1f), new Color(1.4f, 1.1f, 0.55f));
            sunRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sunRenderer.receiveShadows = false;

            var moon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            moon.name = "Moon disc";
            Object.Destroy(moon.GetComponent<Collider>());
            moon.transform.localScale = new Vector3(4.2f, 4.2f, 4.2f);
            var moonMat = AirsideMaterialLibrary.CreateShared(
                new Color(0.82f, 0.86f, 0.95f, 1f),
                AirsideMaterialLibrary.SurfaceKind.UnlitSky);
            if (moonMat.HasProperty("_EmissionColor"))
                moonMat.EnableKeyword("_EMISSION");
            var moonRenderer = moon.GetComponent<Renderer>();
            moonRenderer.sharedMaterial = moonMat;
            SetRendererColor(moonRenderer, new Color(0.82f, 0.86f, 0.95f, 1f), new Color(0.55f, 0.6f, 0.75f));
            moonRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            moonRenderer.receiveShadows = false;
            moon.SetActive(false);
        }

        private void UpdateSunAndMoonDiscs(float daylight, float warm, float elevation)
        {
            if (_sunDisc == null)
            {
                _sunDisc = AirsideSceneIndex.Find("Sun disc");
            }

            if (_moonDisc == null)
            {
                _moonDisc = AirsideSceneIndex.Find("Moon disc");
            }

            // Sun/moon billboards read wrong from overview and clip through terrain — off
            // while daylight is pinned; directional light carries the sky.
            if (PinDaylightPresentation)
            {
                if (_sunDisc != null)
                    _sunDisc.gameObject.SetActive(false);
                if (_moonDisc != null)
                    _moonDisc.gameObject.SetActive(false);
                return;
            }

            // Place discs on a camera-centred sky sphere so they cannot sit under the terrain.
            var sunDir = _sun != null ? -_sun.transform.forward : Vector3.up;
            var skyAnchor = _mainCamera != null ? _mainCamera.transform.position : Vector3.zero;
            if (_sunDisc != null)
            {
                var showSun = sunDir.y > 0.2f && daylight > 0.15f;
                _sunDisc.gameObject.SetActive(showSun);
                if (showSun)
                {
                    _sunDisc.position = skyAnchor + sunDir.normalized * 420f;
                    var sunColor = Color.Lerp(
                        new Color(1f, 0.55f, 0.28f),
                        new Color(1f, 0.95f, 0.78f),
                        Mathf.Clamp01(daylight));
                    sunColor = Color.Lerp(sunColor, new Color(1f, 0.7f, 0.4f), warm * 0.55f);
                    if (_sunDiscRenderer == null)
                        _sunDiscRenderer = _sunDisc.GetComponent<Renderer>();
                    if (_sunDiscRenderer != null)
                        SetRendererColor(_sunDiscRenderer, sunColor, sunColor * (1.1f + warm * 0.6f));

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
                    if (_moonDiscRenderer == null)
                        _moonDiscRenderer = _moonDisc.GetComponent<Renderer>();
                    if (_moonDiscRenderer != null)
                    {
                        var c = new Color(0.82f, 0.86f, 0.95f, 1f) * alpha;
                        SetRendererColor(_moonDiscRenderer, c, c * 0.7f);
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
                var blobLocals = new Matrix4x4[blobs];
                for (var b = 0; b < blobs; b++)
                {
                    blobLocals[b] = Matrix4x4.TRS(
                        new Vector3(
                            (b - 0.5f) * sx * 0.22f,
                            (b % 2) * sy * 0.15f,
                            (b - 0.25f) * sz * 0.12f),
                        Quaternion.identity,
                        new Vector3(
                            sx * (0.65f + b * 0.14f),
                            sy * (0.75f + (b % 2) * 0.2f),
                            sz * (0.65f + b * 0.12f)));
                }

                cluster.gameObject.AddComponent<MeshFilter>().sharedMesh =
                    AirsideMeshUtil.CombineTransformed(BuiltinSphere(), blobLocals);
                var cloudRenderer = cluster.gameObject.AddComponent<MeshRenderer>();
                cloudRenderer.sharedMaterial = AirsideMaterialLibrary.CreateShared(
                    new Color(0.95f, 0.96f, 0.98f, alpha),
                    AirsideMaterialLibrary.SurfaceKind.Default);
                SetRendererColor(cloudRenderer, new Color(0.95f, 0.96f, 0.98f, alpha));
                cloudRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                cloudRenderer.receiveShadows = false;

                // Soft ground umbra under each cloud cluster — drifts with UpdateCloudDrift.
                var umbra = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                umbra.name = $"Cloud umbra {i}";
                Object.Destroy(umbra.GetComponent<Collider>());
                umbra.transform.SetParent(umbraRoot, false);
                umbra.transform.position = new Vector3(x, 0.06f, z);
                umbra.transform.localScale = new Vector3(sx * 0.9f, 0.02f, sz * 0.9f);
                var umbraMat = AirsideMaterialLibrary.CreateShared(
                    new Color(0.05f, 0.07f, 0.1f, 0.18f),
                    AirsideMaterialLibrary.SurfaceKind.Default);
                var umbraRenderer = umbra.GetComponent<Renderer>();
                umbraRenderer.sharedMaterial = umbraMat;
                SetRendererColor(umbraRenderer, new Color(0.05f, 0.07f, 0.1f, 0.18f));
                umbraRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                umbraRenderer.receiveShadows = false;
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
            var material = AirsideMaterialLibrary.CreateShared(color, AirsideMaterialLibrary.SurfaceKind.Default);
            var renderer = shadow.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
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
                var t = AirsideSceneIndex.Find(name);
                if (t == null)
                    continue;
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
            _coastFoamRenderers.Clear();
            _coastWaterRenderers.Clear();
            _coastFoam = null;
            _coastFoamRenderer = null;
            // Prefix scan — foam/water pads are subdivided often; exact name lists go stale.
            foreach (var renderer in AirsideSceneIndex.Renderers)
            {
                if (renderer == null)
                    continue;
                var n = renderer.gameObject.name;
                if (n.StartsWith("Coast foam", StringComparison.Ordinal))
                {
                    _coastFoamLayers.Add(renderer.transform);
                    _coastFoamRenderers.Add(renderer);
                    if (_coastFoam == null)
                    {
                        _coastFoam = renderer.transform;
                        _coastFoamRenderer = renderer;
                    }
                }
                else if (n.StartsWith("Coast water", StringComparison.Ordinal)
                    || n.StartsWith("Coast shallows", StringComparison.Ordinal))
                {
                    _coastWaterRenderers.Add(renderer);
                }
            }

            _jettyDeck = AirsideSceneIndex.Find("Jetty deck");
            foreach (var name in new[]
                     {
                         "Coast boat A", "Coast boat B", "Coast boat C", "Coast boat D",
                         "Coast boat E", "Coast boat F"
                     })
            {
                var boat = AirsideSceneIndex.Find(name);
                if (boat == null)
                    continue;
                _coastBoats.Add((boat, boat.position, boat.eulerAngles.y));
            }
        }

        private void UpdateHangarDoor()
        {
            if (_hangarDoor == null && _hangarBayLight == null && _hangarDoorPanels.Count == 0)
                return;

            // Presentation-only: hangar door slides open by day, closes at night.
            // Also opens wider when a commercial aircraft is near the hangar apron.
            var daylight = PresentationDaylight;
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
                if (_coastFoamRenderer != null)
                {
                    var c = GetRendererColor(_coastFoamRenderer);
                    c.a = 0.55f + 0.3f * (0.5f + 0.5f * Mathf.Sin(t * AirsideReusableMotion.FoamAlphaHz));
                    SetRendererColor(_coastFoamRenderer, c);
                }
            }

            // Secondary foam ribbons pulse out of phase so the surf edge reads layered.
            for (var i = 0; i < _coastFoamLayers.Count; i++)
            {
                var foam = _coastFoamLayers[i];
                if (foam == null)
                    continue;
                var renderer = i < _coastFoamRenderers.Count ? _coastFoamRenderers[i] : null;
                if (renderer == null)
                    continue;
                var wave = 0.5f + 0.5f * Mathf.Sin(t * 1.8f + i * 1.7f);
                var c = GetRendererColor(renderer);
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
                ApplyRendererTextureOffset(renderer, new Vector2(t * (0.012f + i * 0.004f), t * 0.008f));
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
                _cloudRoot = AirsideSceneIndex.Find("Cloud bands");
            }

            if (_cloudUmbraRoot == null)
            {
                _cloudUmbraRoot = AirsideSceneIndex.Find("Cloud umbras");
            }

            if (_cloudRoot == null)
                return;

            // Slow eastward drift + day tint so clouds feel alive without sim coupling.
            var daylight = PresentationDaylight;
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
            var tintKey = ((int)weather << 4) ^ AirsideRuntimeQuality.ProbeBand(daylight, 0f);
            var tintChanged = tintKey != _cloudTintKey;
            if (tintChanged)
                _cloudTintKey = tintKey;
            for (var i = 0; i < _cloudRoot.childCount; i++)
            {
                var cloud = _cloudRoot.GetChild(i);
                var p = cloud.position;
                p.x += drift;
                if (p.x > 100f)
                    p.x = -100f;
                cloud.position = p;

                if (_cloudUmbraRoot != null && i < _cloudUmbraRoot.childCount)
                {
                    var umbraTransform = _cloudUmbraRoot.GetChild(i);
                    umbraTransform.position = new Vector3(p.x, 0.06f, p.z);
                    umbraTransform.rotation = Quaternion.identity;
                }

                if (!tintChanged)
                    continue;

                var dusk = Mathf.Clamp01(Mathf.Min(daylight, 1f - daylight) * 3f);
                var tint = Color.Lerp(new Color(0.55f, 0.6f, 0.75f), new Color(0.95f, 0.96f, 0.98f), daylight);
                tint = Color.Lerp(tint, new Color(0.95f, 0.7f, 0.55f), dusk * 0.55f);
                if (thickSky)
                    tint = Color.Lerp(tint, new Color(0.62f, 0.66f, 0.72f), overcast ? 0.55f : 0.32f);
                var baseAlpha = overcast ? 0.42f : cloudy ? 0.32f : 0.22f;
                tint.a = Mathf.Lerp(baseAlpha * 0.85f, baseAlpha, daylight);

                // Combined cluster mesh — one renderer, MPB tint only when the band changes.
                var renderer = cloud.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var color = GetRendererColor(renderer);
                    if (color.a > 0.01f)
                        tint.a = Mathf.Max(tint.a, color.a * (thickSky ? (overcast ? 1.35f : 1.15f) : 1f));
                    SetRendererColor(renderer, tint);
                }

                if (_cloudUmbraRoot == null || i >= _cloudUmbraRoot.childCount)
                    continue;
                var umbra = _cloudUmbraRoot.GetChild(i);
                // Keep authored umbra footprint; only the alpha follows the day/weather band.
                var umbraRenderer = umbra.GetComponent<Renderer>();
                if (umbraRenderer == null)
                    continue;
                var umbraColor = GetRendererColor(umbraRenderer);
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
            for (var i = 0; i < AirsideRuntimeQuality.BirdCount; i++)
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
                var bodyRenderer = body.GetComponent<Renderer>();
                bodyRenderer.sharedMaterial = AirsideMaterialLibrary.CreateShared(
                    new Color(0.1f, 0.1f, 0.12f),
                    AirsideMaterialLibrary.SurfaceKind.Plastic);
                bodyRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

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
            var wingRenderer = wing.GetComponent<Renderer>();
            wingRenderer.sharedMaterial = AirsideMaterialLibrary.CreateShared(
                new Color(0.18f, 0.18f, 0.2f),
                AirsideMaterialLibrary.SurfaceKind.Plastic);
            wingRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // Pivot hint stored as unused local euler y sign for flap direction.
            wing.transform.localEulerAngles = new Vector3(0f, left ? -8f : 8f, 0f);
        }

        private void UpdateBirdFlock()
        {
            if (_birdFlockRoot == null)
            {
                _birdFlockRoot = AirsideSceneIndex.Find("Bird flock");
            }

            if (_birdFlockRoot == null)
                return;

            var nBirds = _birdFlockRoot.childCount;
            if (_birdWingL == null || _birdWingL.Length != nBirds)
            {
                _birdWingL = new Transform[nBirds];
                _birdWingR = new Transform[nBirds];
                for (var i = 0; i < nBirds; i++)
                {
                    var bird = _birdFlockRoot.GetChild(i);
                    for (var c = 0; c < bird.childCount; c++)
                    {
                        var child = bird.GetChild(c);
                        if (child.name.StartsWith("Wing L", StringComparison.Ordinal))
                            _birdWingL[i] = child;
                        else if (child.name.StartsWith("Wing R", StringComparison.Ordinal))
                            _birdWingR[i] = child;
                    }
                }
            }

            // Wide lazy orbit south of the runway — presentation flock, not wildlife sim.
            var t = Time.unscaledTime * AirsideReusableMotion.BirdOrbitHz * Mathf.PI * 2f;
            for (var i = 0; i < nBirds; i++)
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
                if (_birdWingL[i] != null)
                    _birdWingL[i].localRotation = Quaternion.Euler(0f, -8f, flap);
                if (_birdWingR[i] != null)
                    _birdWingR[i].localRotation = Quaternion.Euler(0f, 8f, -flap);
            }
        }

        /// <summary>Darken an opaque palette colour without dropping alpha into the transparent path.</summary>
        private static Color Shade(Color color, float factor) =>
            new Color(color.r * factor, color.g * factor, color.b * factor, 1f);

        private static void BuildStandMarking(float x, float z, string name)
        {
            // Markings kit already paints stand_stop + digits + chevrons — skip yellow densify.
            if (FindBuilt("stand_stop_a") != null
                || FindBuilt("stand_stop_b") != null
                || FindBuilt("stand_stop_c") != null)
                return;

            CreateBlock(name, new Vector3(x, 0.08f, z), new Vector3(0.18f, 0.03f, 4.2f), new Color(0.96f, 0.77f, 0.12f));
            CreateBlock($"{name} stop", new Vector3(x, 0.08f, z + 1.9f), new Vector3(3.4f, 0.03f, 0.18f), new Color(0.96f, 0.77f, 0.12f));
        }

        private static Transform BuildAircraft(string name, Color accent, string liveryDecalRelativePath = null)
        {
            var root = new GameObject(name).transform;
            // Production AIR-001: true-size ATR 42-class starter aircraft. Motion roots
            // use y=0.7f, so offset the metre-authored kit to put its tires on the ground.
            // v06 remains a safe fallback for branches/builds that have not imported it yet.
            var aircraftArt = PreferArtKit(
                "Models/Aircraft/mdl_atr42_starter_v01.gltf",
                "Models/Aircraft/mdl_regional_turboprop_01_v06.gltf");
            var finalAtr42 = aircraftArt.EndsWith("mdl_atr42_starter_v01.gltf", StringComparison.Ordinal);
            var usedArt = ArtPresentationLoader.TryInstantiate(
                aircraftArt,
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
                RebakeAircraftArticulatedPivots(root);
                NestLandingGearParts(root);
                NestCabinDoorParts(root);
                NestFlapParts(root);
                if (finalAtr42)
                {
                    PolishFinalAtrMaterials(root);
                    EnsureAircraftLod(root);
                }
            }

            if (!usedArt)
            {
                // Batch C turboprop silhouette with separated props/engines/gear (primitive fallback).
                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "Fuselage";
                body.transform.SetParent(root, false);
                body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                body.transform.localScale = new Vector3(0.72f, 2.8f, 0.72f);
                body.GetComponent<Renderer>().sharedMaterial = CreateMaterial(new Color(0.93f, 0.95f, 0.97f));
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
            // The final starter keeps the silhouette clean; the legacy heat cubes
            // read as opaque blobs at its larger scale. Older kits retain their cue.
            if (!finalAtr42 && !HasNamedChild(root, "EngineHeat L") && !HasNamedChild(root, "EngineHeat R"))
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
            source.minDistance = 12f;
            source.maxDistance = 220f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.Play();
            return root;
        }

        private static string RenameAircraftPart(string kitName)
        {
            if (kitName.StartsWith("cabin_window_r", StringComparison.Ordinal))
                return "Cabin window R" + kitName.Substring("cabin_window_r".Length);
            if (kitName.StartsWith("cabin_window_", StringComparison.Ordinal))
                return "Cabin window " + kitName.Substring("cabin_window_".Length);
            if (kitName.StartsWith("tire_nose_", StringComparison.Ordinal))
                return "Tire nose " + FriendlyPartSuffix(kitName.Substring("tire_nose_".Length));
            if (kitName.StartsWith("wheel_nose_", StringComparison.Ordinal))
                return "Wheel nose " + FriendlyPartSuffix(kitName.Substring("wheel_nose_".Length));
            if (kitName.StartsWith("rim_nose_", StringComparison.Ordinal))
                return "Rim nose " + FriendlyPartSuffix(kitName.Substring("rim_nose_".Length));
            if (kitName.StartsWith("tire_left_", StringComparison.Ordinal))
                return "Tire L " + FriendlyPartSuffix(kitName.Substring("tire_left_".Length));
            if (kitName.StartsWith("wheel_left_", StringComparison.Ordinal))
                return "Wheel L " + FriendlyPartSuffix(kitName.Substring("wheel_left_".Length));
            if (kitName.StartsWith("rim_left_", StringComparison.Ordinal))
                return "Rim L " + FriendlyPartSuffix(kitName.Substring("rim_left_".Length));
            if (kitName.StartsWith("tire_right_", StringComparison.Ordinal))
                return "Tire R " + FriendlyPartSuffix(kitName.Substring("tire_right_".Length));
            if (kitName.StartsWith("wheel_right_", StringComparison.Ordinal))
                return "Wheel R " + FriendlyPartSuffix(kitName.Substring("wheel_right_".Length));
            if (kitName.StartsWith("rim_right_", StringComparison.Ordinal))
                return "Rim R " + FriendlyPartSuffix(kitName.Substring("rim_right_".Length));

            return kitName switch
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
            "gear_fairing_left" => "Gear fairing L",
            "gear_fairing_right" => "Gear fairing R",
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
            "engine_heat_left" => "EngineHeat L",
            "engine_heat_right" => "EngineHeat R",
            _ => kitName
            };
        }

        private static string FriendlyPartSuffix(string suffix) =>
            suffix.Replace('_', ' ');

        private static Color? AircraftPartColor(string kitName, Color accent)
        {
            if (kitName.StartsWith("cabin_window_", StringComparison.Ordinal))
                return new Color(0.18f, 0.35f, 0.48f, 0.42f);
            if (kitName.StartsWith("tire_", StringComparison.Ordinal))
                return new Color(0.12f, 0.12f, 0.13f);
            if (kitName.StartsWith("wheel_", StringComparison.Ordinal)
                || kitName.StartsWith("rim_", StringComparison.Ordinal))
                return new Color(0.55f, 0.56f, 0.58f);
            if (kitName.StartsWith("engine_heat_", StringComparison.Ordinal))
                return new Color(0.95f, 0.55f, 0.2f, 0.10f);

            return kitName switch
            {
            "fuselage" or "fuselage_mid" or "fuselage_aft"
                or "cabin_ring_fwd" or "cabin_ring_mid" or "cabin_ring_aft" or "cabin_ring_tail" or "tail_cone"
                or "nose" or "nose_tip" or "nose_ring_a" or "nose_ring_b" or "radome"
                or "belly_fairing" or "cargo_door" or "door_frame_fwd"
                or "gear_fairing_left" or "gear_fairing_right" => new Color(0.93f, 0.95f, 0.97f),
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
        }

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
            foreach (var child in AirsideNamedChildren.Get(aircraft))
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
            foreach (var child in AirsideNamedChildren.Get(aircraft))
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
        /// FBX/glTF fallback meshes arrive with aircraft-space vertices and zeroed
        /// transforms. Move gameplay parts to their actual hinges and rebake the
        /// vertices so their runtime rotations do not orbit around the fuselage.
        /// </summary>
        private static void RebakeAircraftArticulatedPivots(Transform aircraft)
        {
            foreach (var child in AirsideNamedChildren.Get(aircraft))
            {
                if (child == aircraft)
                    continue;
                var renderer = child.GetComponent<Renderer>();
                if (renderer == null)
                    continue;

                var bounds = renderer.bounds;
                var pivot = bounds.center;
                var articulated = true;
                if (child.name is "Gear nose" or "Gear L" or "Gear R")
                {
                    pivot.y = bounds.max.y;
                }
                else if (child.name.StartsWith("Gear door", StringComparison.Ordinal))
                {
                    pivot.y = bounds.max.y;
                }
                else if (child.name is "Flap L" or "Flap R"
                         || child.name.StartsWith("Aileron", StringComparison.Ordinal)
                         || child.name.StartsWith("Elevator", StringComparison.Ordinal)
                         || child.name.StartsWith("Spoiler", StringComparison.Ordinal))
                {
                    pivot.z = bounds.max.z;
                }
                else if (child.name.StartsWith("Rudder", StringComparison.Ordinal)
                         || child.name.StartsWith("CabinDoor", StringComparison.Ordinal)
                         || child.name.StartsWith("Cargo door", StringComparison.OrdinalIgnoreCase))
                {
                    pivot.z = bounds.max.z;
                }
                else
                {
                    articulated = false;
                }

                if (articulated)
                    RebakePartPivot(child, pivot);
            }
        }

        private static void RebakePartPivot(Transform part, Vector3 pivotWorld)
        {
            if ((part.position - pivotWorld).sqrMagnitude < 0.0025f)
                return;

            var filters = part.GetComponentsInChildren<MeshFilter>(true);
            var worldVertices = new Vector3[filters.Length][];
            for (var i = 0; i < filters.Length; i++)
            {
                var filter = filters[i];
                if (filter == null || filter.sharedMesh == null)
                    continue;
                var vertices = filter.sharedMesh.vertices;
                var world = new Vector3[vertices.Length];
                for (var v = 0; v < vertices.Length; v++)
                    world[v] = filter.transform.TransformPoint(vertices[v]);
                worldVertices[i] = world;
            }

            part.position = pivotWorld;
            for (var i = 0; i < filters.Length; i++)
            {
                if (worldVertices[i] == null)
                    continue;
                var filter = filters[i];
                var mesh = Object.Instantiate(filter.sharedMesh);
                mesh.name = filter.sharedMesh.name + " articulated-pivot";
                var local = new Vector3[worldVertices[i].Length];
                for (var v = 0; v < local.Length; v++)
                    local[v] = filter.transform.InverseTransformPoint(worldVertices[i][v]);
                mesh.vertices = local;
                mesh.RecalculateBounds();
                mesh.RecalculateNormals();
                filter.sharedMesh = mesh;
            }
        }

        /// <summary>
        /// Parent scissors / tires under matching gear struts so retract takes the
        /// whole assembly (0025 item 7) — mirrors NestCrossPropellerBlades.
        /// </summary>
        private static void NestLandingGearParts(Transform aircraft)
        {
            Transform gearNose = null, gearL = null, gearR = null;
            var movingParts = new List<Transform>();
            foreach (var child in AirsideNamedChildren.Get(aircraft))
            {
                if (child.name == "Gear nose") gearNose = child;
                else if (child.name == "Gear L") gearL = child;
                else if (child.name == "Gear R") gearR = child;
                else if (child.name.StartsWith("Gear scissors", StringComparison.Ordinal)
                         || child.name.StartsWith("Gear oleo", StringComparison.Ordinal)
                         || child.name.StartsWith("Tire", StringComparison.Ordinal)
                         || child.name.StartsWith("Wheel", StringComparison.Ordinal)
                         || child.name.StartsWith("Rim", StringComparison.Ordinal))
                    movingParts.Add(child);
            }

            foreach (var part in movingParts)
            {
                var lower = part.name.ToLowerInvariant();
                var gear = lower.Contains("nose") ? gearNose
                    : part.name.IndexOf(" L", StringComparison.Ordinal) >= 0 ? gearL
                    : part.name.IndexOf(" R", StringComparison.Ordinal) >= 0 ? gearR
                    : null;
                NestUnderProp(gear, part, part.name);
            }
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
            foreach (var child in AirsideNamedChildren.Get(aircraft))
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
            foreach (var child in AirsideNamedChildren.Get(vehicle))
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
            foreach (var child in AirsideNamedChildren.Get(aircraft))
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
            foreach (var child in AirsideNamedChildren.Get(aircraft))
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
            foreach (var child in AirsideNamedChildren.Get(vehicle))
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

        private static bool HasNamedChild(Transform root, string name) =>
            AirsideNamedChildren.HasName(root, name);

        private static GameObject FindBuilt(string name)
        {
            var found = AirsideSceneIndex.FindGameObject(name);
            if (found != null)
                return found;
            if (AirsideSceneIndex.IsKnownMissing(name))
                return null;
            found = UnityEngine.GameObject.Find(name);
            if (found != null)
                AirsideSceneIndex.Remember(found);
            else
                AirsideSceneIndex.RememberMiss(name);
            return found;
        }

        private static Color GetRendererColor(Renderer renderer)
        {
            if (renderer == null)
                return Color.white;

            renderer.GetPropertyBlock(RendererTintBlock);
            if (RendererTintBlock.HasProperty(BaseColorId))
                return RendererTintBlock.GetColor(BaseColorId);
            if (RendererTintBlock.HasProperty(ColorId))
                return RendererTintBlock.GetColor(ColorId);

            var shared = renderer.sharedMaterial;
            if (shared == null)
                return Color.white;
            if (shared.HasProperty(BaseColorId))
                return shared.GetColor(BaseColorId);
            return shared.color;
        }

        /// <summary>URP Lit uses _BaseColor; keep legacy .color in sync for Built-in fallbacks.
        /// Property blocks tint per renderer without cloning the shared material.</summary>
        private static void SetRendererColor(Renderer renderer, Color color, Color? emission = null)
        {
            if (renderer == null)
                return;

            renderer.GetPropertyBlock(RendererTintBlock);
            RendererTintBlock.SetColor("_Color", color);
            RendererTintBlock.SetColor("_BaseColor", color);
            if (emission.HasValue)
            {
                var shared = renderer.sharedMaterial;
                if (shared != null && shared.HasProperty("_EmissionColor"))
                {
                    shared.EnableKeyword("_EMISSION");
                    RendererTintBlock.SetColor("_EmissionColor", emission.Value);
                }
            }

            renderer.SetPropertyBlock(RendererTintBlock);
        }

        private static void ApplyRendererTextureOffset(Renderer renderer, Vector2 offset)
        {
            if (renderer == null)
                return;

            renderer.GetPropertyBlock(RendererTintBlock);
            var scale = Vector2.one;
            var shared = renderer.sharedMaterial;
            if (shared != null)
            {
                if (shared.HasProperty(BaseMapId))
                    scale = shared.GetTextureScale(BaseMapId);
                else if (shared.HasProperty(MainTexId))
                    scale = shared.GetTextureScale(MainTexId);
            }

            var st = new Vector4(scale.x, scale.y, offset.x, offset.y);
            RendererTintBlock.SetVector(BaseMapStId, st);
            RendererTintBlock.SetVector(MainTexStId, st);
            renderer.SetPropertyBlock(RendererTintBlock);
        }

        private static void ApplyRendererTexture(
            Renderer renderer,
            Texture texture,
            Vector2 tiling,
            float? smoothness = null)
        {
            if (renderer == null || texture == null)
                return;

            renderer.GetPropertyBlock(RendererTintBlock);
            RendererTintBlock.SetTexture("_BaseMap", texture);
            RendererTintBlock.SetTexture("_MainTex", texture);
            var st = new Vector4(tiling.x, tiling.y, 0f, 0f);
            RendererTintBlock.SetVector(BaseMapStId, st);
            RendererTintBlock.SetVector(MainTexStId, st);
            if (smoothness.HasValue)
                RendererTintBlock.SetFloat("_Smoothness", smoothness.Value);
            renderer.SetPropertyBlock(RendererTintBlock);
        }

        /// <summary>
        /// Restrained PBR response for the final ATR: skin, glass, rubber, metal and
        /// lights without changing the fictional Airside livery.
        /// </summary>
        private static void PolishFinalAtrMaterials(Transform aircraft)
        {
            foreach (var renderer in aircraft.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || renderer.name is "GroundShadow" or "PropDisc")
                    continue;
                var kind = AirsideMaterialLibrary.InferFromMeshName(renderer.name);
                var color = GetRendererColor(renderer);
                // Skip near-black UV-failure patches — lift them to a usable panel grey.
                if (color.r < 0.04f && color.g < 0.04f && color.b < 0.04f && color.a > 0.9f)
                    color = new Color(0.55f, 0.58f, 0.62f, 1f);
                renderer.sharedMaterial = AirsideMaterialLibrary.CreateShared(color, kind);
            }
        }

        /// <summary>
        /// Overview LOD: keep the full ATR close-up, drop small static detail far out.
        /// Moving parts stay in every LOD so gear/props never pop off.
        /// </summary>
        private static void EnsureAircraftLod(Transform aircraft)
        {
            if (aircraft.GetComponent<LODGroup>() != null)
                return;

            var all = new List<Renderer>(64);
            var nearOnly = new List<Renderer>(32);
            foreach (var renderer in aircraft.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || renderer.name is "GroundShadow" or "PropDisc")
                    continue;
                all.Add(renderer);
                var n = renderer.name;
                var moving = n.StartsWith("Propeller", StringComparison.Ordinal)
                    || n.StartsWith("Tire", StringComparison.Ordinal)
                    || n.StartsWith("Gear", StringComparison.Ordinal)
                    || n.StartsWith("Flap", StringComparison.Ordinal)
                    || n.StartsWith("Aileron", StringComparison.Ordinal)
                    || n.StartsWith("Elevator", StringComparison.Ordinal)
                    || n.StartsWith("Rudder", StringComparison.Ordinal)
                    || n.StartsWith("Spoiler", StringComparison.Ordinal)
                    || n.StartsWith("CabinDoor", StringComparison.Ordinal)
                    || n.StartsWith("LandingLight", StringComparison.Ordinal)
                    || n.StartsWith("NavLight", StringComparison.Ordinal)
                    || n.StartsWith("Beacon", StringComparison.Ordinal);
                var fine = n.IndexOf("rim", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("scissors", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("rivet", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("antenna", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("fairing", StringComparison.OrdinalIgnoreCase) >= 0;
                if (!moving && fine)
                    nearOnly.Add(renderer);
            }

            if (all.Count == 0)
                return;

            var far = new List<Renderer>(all.Count);
            foreach (var renderer in all)
            {
                if (!nearOnly.Contains(renderer))
                    far.Add(renderer);
            }

            // Medium drops fine detail immediately; High keeps it to ~12% screen height.
            var detailHeight = AirsideRuntimeQuality.Current == AirsideRuntimeQuality.Ladder.High
                ? 0.12f
                : 0.35f;
            var group = aircraft.gameObject.AddComponent<LODGroup>();
            group.SetLODs(new[]
            {
                new LOD(detailHeight, all.ToArray()),
                new LOD(0.02f, far.ToArray())
            });
            group.RecalculateBounds();
        }

        /// <summary>
        /// Translucent prop disc under each propeller hub — shown only at high RPM.
        /// </summary>
        private static void EnsurePropDiscs(Transform aircraft)
        {
            foreach (var child in AirsideNamedChildren.Get(aircraft))
            {
                if (child == aircraft || !child.name.StartsWith("Propeller", StringComparison.Ordinal))
                    continue;
                if (child.Find("PropDisc") != null)
                    continue;

                // Size the blur disc from blade/tip bounds (v06 radial ~1.27f m — fixed 1.2f
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

                var diameter = Mathf.Clamp(radius * 2.05f, 1.2f, 4.05f);
                var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                disc.name = "PropDisc";
                Object.Destroy(disc.GetComponent<Collider>());
                disc.transform.SetParent(child, false);
                disc.transform.localPosition = Vector3.zero;
                // Cylinder axis → local Z so the face is perpendicular to the spin axis.
                disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                // Thin glass disc — reads as motion blur, not a grey cylinder slab.
                disc.transform.localScale = new Vector3(diameter * 1.02f, 0.0035f, diameter * 1.02f);
                var discColor = new Color(0.72f, 0.74f, 0.78f, 0.11f);
                var discMat = AirsideMaterialLibrary.CreateShared(
                    discColor,
                    AirsideMaterialLibrary.SurfaceKind.Glass);
                var discRenderer = disc.GetComponent<Renderer>();
                discRenderer.sharedMaterial = discMat;
                discRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                discRenderer.receiveShadows = false;
                SetRendererColor(discRenderer, discColor);
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
            shadow.transform.localPosition = new Vector3(0f, -0.55f, 0f);
            shadow.transform.localRotation = Quaternion.identity;
            // ATR 42-class footprint (~24.6 m span × ~22.7 m length).
            shadow.transform.localScale = new Vector3(22.5f, 0.012f, 16.5f);
            var material = AirsideMaterialLibrary.CreateShared(new Color(0.05f, 0.06f, 0.08f, 0.16f),
                AirsideMaterialLibrary.SurfaceKind.Default);
            var renderer = shadow.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            SetRendererColor(renderer, new Color(0.05f, 0.06f, 0.08f, 0.16f));
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static void UpdateGroundShadow(Transform aircraft)
        {
            var shadow = aircraft.Find("GroundShadow");
            if (shadow == null)
                return;

            var groundY = AirsideBareField.RunwayCenterY + AirsideBareField.RunwayHeightMetres * 0.5f + 0.02f;
            var ground = new Vector3(aircraft.position.x, groundY, aircraft.position.z);
            shadow.position = ground;
            shadow.rotation = Quaternion.identity;
            var altitude = Mathf.Max(0f, aircraft.position.y - AirsideFlightPath.GroundY);
            var t = Mathf.Clamp01(altitude / 18f);
            var width = Mathf.Lerp(22.5f, 30f, t);
            var depth = Mathf.Lerp(16.5f, 22f, t);
            var sx = aircraft.lossyScale.x > 0.001f ? width / aircraft.lossyScale.x : width;
            var sy = aircraft.lossyScale.y > 0.001f ? 0.03f / aircraft.lossyScale.y : 0.03f;
            var sz = aircraft.lossyScale.z > 0.001f ? depth / aircraft.lossyScale.z : depth;
            shadow.localScale = new Vector3(sx, sy, sz);

            var renderer = shadow.GetComponent<Renderer>();
            if (renderer == null)
                return;
            var color = GetRendererColor(renderer);
            // Softer contact so realtime URP shadows remain the primary read.
            color.a = Mathf.Lerp(0.28f, 0.04f, t);
            SetRendererColor(renderer, color);
            shadow.gameObject.SetActive(aircraft.gameObject.activeInHierarchy);
        }

        /// <summary>Prefer the richest present kit/prefab; null when none are available.</summary>
        private static string PreferArtKit(params string[] candidates)
        {
            if (candidates == null || candidates.Length == 0)
                return null;
            for (var i = 0; i < candidates.Length; i++)
            {
                if (ArtPresentationLoader.HasPresentation(candidates[i]))
                    return candidates[i];
            }

            return null;
        }

        /// <summary>
        /// Prefer denser surface basecolours (v02 fidelity board) when present; keep v01 fallback.
        /// </summary>
        private static string PreferSurfaceBasecolor(string stem) => PreferSurfaceMap(stem, "basecolor");

        private static string PreferSurfaceMap(string stem, string map)
        {
            if (string.IsNullOrEmpty(stem) || string.IsNullOrEmpty(map))
                return null;
            var cacheKey = stem + "|" + map;
            if (SurfaceBasecolorCache.TryGetValue(cacheKey, out var cached))
                return cached;

            string chosen = null;
            var v03 = $"Textures/Surfaces/{stem}_{map}_v03.png";
            if (ArtRuntimePaths.ResolveExisting(v03) != null)
                chosen = v03;
            else
            {
                var v02 = $"Textures/Surfaces/{stem}_{map}_v02.png";
                if (ArtRuntimePaths.ResolveExisting(v02) != null)
                    chosen = v02;
                else
                    chosen = $"Textures/Surfaces/{stem}_{map}_v01.png";
            }

            SurfaceBasecolorCache[cacheKey] = chosen;
            return chosen;
        }

        /// <summary>Prefer denser Resources prefab keys when present.</summary>
        private static bool TryInstantiatePreferredPrefab(out Transform root, params string[] prefabKeys)
        {
            root = null;
            if (prefabKeys == null)
                return false;
            for (var i = 0; i < prefabKeys.Length; i++)
            {
                if (ArtPresentationLoader.TryInstantiatePrefab(prefabKeys[i], out root))
                    return true;
            }

            return false;
        }

        private static void ApplyLiveryDecal(Transform aircraft, string artRelativePath)
        {
            if (string.IsNullOrEmpty(artRelativePath))
                return;
            var texture = TryLoadArtTexture(artRelativePath);
            if (texture == null)
                return;

            foreach (var child in AirsideNamedChildren.Get(aircraft))
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
                ApplyRendererTexture(renderer, texture, Vector2.one);
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
            var yellow = AirsideTheme.SafetyYellow;
            var tread = new Color(0.62f, 0.63f, 0.65f);
            var placed = ArtGltfLoader.TryPlaceCombined(
                kit,
                new[]
                {
                    ("stairs_base", new Color(0.55f, 0.56f, 0.58f)),
                    ("stairs_rail_l", yellow),
                    ("stairs_rail_r", yellow),
                    ("stairs_rail_mid", yellow),
                    ("stairs_tread_1", tread),
                    ("stairs_tread_2", tread),
                    ("stairs_tread_3", tread),
                    ("stairs_tread_4", tread),
                    ("stairs_tread_5", tread),
                    ("stairs_tread_6", tread),
                    ("stairs_rail_cross", yellow),
                    ("stairs_post_1l", yellow),
                    ("stairs_post_1r", yellow),
                    ("stairs_post_2l", yellow),
                    ("stairs_post_2r", yellow),
                    ("stairs_post_3l", yellow),
                    ("stairs_post_3r", yellow),
                    ("stairs_nosing_1", yellow),
                    ("stairs_nosing_2", yellow),
                    ("stairs_nosing_3", yellow),
                    ("stairs_nosing_4", yellow),
                    ("stairs_nosing_5", yellow),
                    ("stairs_side_panel_l", AirsideTheme.CoastalBlue),
                    ("stairs_side_panel_r", AirsideTheme.CoastalBlue),
                    ("stairs_platform", new Color(0.7f, 0.72f, 0.74f)),
                    ("stairs_handle", Shade(AirsideTheme.CoastalBlue, 0.9f)),
                    ("stairs_brace", new Color(0.5f, 0.5f, 0.52f)),
                    ("stairs_wheel_l", new Color(0.15f, 0.15f, 0.16f)),
                    ("stairs_wheel_r", new Color(0.15f, 0.15f, 0.16f)),
                    ("stairs_wheel_rl", new Color(0.15f, 0.15f, 0.16f)),
                    ("stairs_wheel_rr", new Color(0.15f, 0.15f, 0.16f)),
                    ("stairs_hub_fl", new Color(0.25f, 0.26f, 0.28f)),
                    ("stairs_hub_fr", new Color(0.25f, 0.26f, 0.28f)),
                    ("stairs_hub_rl", new Color(0.25f, 0.26f, 0.28f)),
                    ("stairs_hub_rr", new Color(0.25f, 0.26f, 0.28f))
                },
                Vector3.zero, Quaternion.identity, "Passenger stairs kit", out var kitRoot);
            if (kitRoot != null)
            {
                kitRoot.SetParent(root, false);
                kitRoot.localPosition = Vector3.zero;
                kitRoot.localRotation = Quaternion.identity;
            }

            if (!placed && ArtGltfLoader.TryPlaceNamedMesh(kit, "stairs", Vector3.zero, Quaternion.identity,
                    new Color(0.7f, 0.72f, 0.74f), out var stairs))
            {
                stairs.SetParent(root, false);
                stairs.localPosition = Vector3.zero;
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
            var rubber = new Color(0.85f, 0.2f, 0.15f);
            var placed = ArtGltfLoader.TryPlaceCombined(
                kit,
                new[]
                {
                    ("chock_a", rubber),
                    ("chock_b", rubber),
                    ("chock_rope", new Color(0.2f, 0.2f, 0.22f)),
                    ("chock_handle", new Color(0.25f, 0.26f, 0.28f))
                },
                Vector3.zero, Quaternion.identity, "Wheel chocks kit", out var kitRoot,
                localOffsets: new[]
                {
                    new Vector3(-0.55f, 0f, 0f),
                    new Vector3(0.55f, 0f, 0f),
                    Vector3.zero,
                    Vector3.zero
                });
            if (kitRoot != null)
            {
                kitRoot.SetParent(root, false);
                kitRoot.localPosition = Vector3.zero;
                kitRoot.localRotation = Quaternion.identity;
            }

            // v02 kit may ship a single combined "chocks" mesh.
            if (!placed)
            {
                placed = ArtGltfLoader.TryPlaceNamedMesh(kit, "chocks", Vector3.zero, Quaternion.identity,
                    rubber, out var combined);
                if (combined != null)
                {
                    combined.SetParent(root, false);
                    combined.localPosition = Vector3.zero;
                }
            }

            if (placed)
            {
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
            var placed = ArtGltfLoader.TryPlaceCombined(
                kit,
                new[]
                {
                    ("gpu_body", AirsideTheme.CoastalBlue),
                    ("gpu_cab", Shade(AirsideTheme.CoastalBlue, 0.85f)),
                    ("gpu_vent", new Color(0.35f, 0.38f, 0.36f)),
                    ("gpu_panel", new Color(0.2f, 0.22f, 0.24f)),
                    ("gpu_panel_b", new Color(0.2f, 0.22f, 0.24f)),
                    ("gpu_grille", new Color(0.18f, 0.2f, 0.2f)),
                    ("gpu_grille_2", new Color(0.18f, 0.2f, 0.2f)),
                    ("gpu_slot_1", new Color(0.15f, 0.16f, 0.18f)),
                    ("gpu_slot_2", new Color(0.15f, 0.16f, 0.18f)),
                    ("gpu_cable", new Color(0.2f, 0.2f, 0.22f)),
                    ("gpu_cable_reel", new Color(0.22f, 0.22f, 0.24f)),
                    ("gpu_hitch", new Color(0.3f, 0.3f, 0.32f)),
                    ("gpu_beacon", new Color(0.95f, 0.35f, 0.12f)),
                    ("gpu_exhaust", new Color(0.3f, 0.32f, 0.3f)),
                    ("gpu_light", new Color(0.95f, 0.9f, 0.6f)),
                    ("gpu_handle", new Color(0.28f, 0.3f, 0.32f)),
                    ("gpu_stripe", new Color(0.15f, 0.16f, 0.18f)),
                    ("gpu_wheel_fl", new Color(0.15f, 0.15f, 0.16f)),
                    ("gpu_wheel_fr", new Color(0.15f, 0.15f, 0.16f)),
                    ("gpu_wheel_rl", new Color(0.15f, 0.15f, 0.16f)),
                    ("gpu_wheel_rr", new Color(0.15f, 0.15f, 0.16f)),
                    ("gpu_hub_fl", new Color(0.25f, 0.26f, 0.28f)),
                    ("gpu_hub_fr", new Color(0.25f, 0.26f, 0.28f)),
                    ("gpu_hub_rl", new Color(0.25f, 0.26f, 0.28f)),
                    ("gpu_hub_rr", new Color(0.25f, 0.26f, 0.28f))
                },
                Vector3.zero, Quaternion.identity, "GPU cart kit", out var kitRoot);
            if (kitRoot != null)
            {
                kitRoot.SetParent(root, false);
                kitRoot.localPosition = Vector3.zero;
                kitRoot.localRotation = Quaternion.identity;
            }

            if (!placed && ArtGltfLoader.TryPlaceNamedMesh(kit, "gpu", Vector3.zero, Quaternion.identity,
                    AirsideTheme.CoastalBlue, out var gpu))
            {
                gpu.SetParent(root, false);
                gpu.localPosition = Vector3.zero;
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
            ParentBlock(root, "GPU body", Vector3.zero, new Vector3(1.4f, 0.7f, 0.9f), AirsideTheme.CoastalBlue);
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
            var placed = ArtGltfLoader.TryPlaceCombined(
                kit,
                new[]
                {
                    ("towbar", new Color(0.82f, 0.62f, 0.18f)),
                    ("towbar_head", new Color(0.3f, 0.32f, 0.34f)),
                    ("towbar_wheel", new Color(0.15f, 0.15f, 0.16f)),
                    ("towbar_handle", new Color(0.28f, 0.3f, 0.32f)),
                    ("towbar_eye", new Color(0.35f, 0.36f, 0.38f))
                },
                Vector3.zero, Quaternion.identity, "Tug towbar kit", out var kitRoot);
            if (kitRoot != null)
            {
                kitRoot.SetParent(root, false);
                kitRoot.localPosition = new Vector3(0f, -0.55f, 0f);
                kitRoot.localRotation = Quaternion.identity;
            }

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
            var placedPole = ArtGltfLoader.TryPlaceCombined(
                    propsKit,
                    new[]
                    {
                        ("sock_base", new Color(0.35f, 0.36f, 0.38f)),
                        ("sock_pole", new Color(0.75f, 0.75f, 0.72f)),
                        ("windsock_pole", new Color(0.75f, 0.75f, 0.72f)),
                        ("sock_frame", new Color(0.55f, 0.55f, 0.52f)),
                        ("sock_swivel", new Color(0.45f, 0.46f, 0.48f)),
                        ("sock_guy_l", new Color(0.4f, 0.4f, 0.42f)),
                        ("sock_guy_r", new Color(0.4f, 0.4f, 0.42f)),
                        ("sock_counterweight", new Color(0.3f, 0.32f, 0.34f)),
                        ("sock_light", new Color(0.95f, 0.95f, 0.85f)),
                        ("sock_ring", new Color(0.55f, 0.55f, 0.52f))
                    },
                    poleOrigin, Quaternion.identity, "Windsock pole", out _);

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
            var fabricPlaced = ArtGltfLoader.TryPlaceCombined(
                propsKit,
                new[]
                {
                    ("sock_fabric", new Color(0.92f, 0.55f, 0.12f)),
                    ("sock_fabric_mid", new Color(0.95f, 0.65f, 0.2f)),
                    ("sock_fabric_tip", new Color(0.95f, 0.95f, 0.92f))
                },
                sock.position, sock.rotation, "Windsock fabric", out var fabric);
            if (fabric != null)
            {
                fabric.SetParent(sock, true);
                fabricPlaced = true;
            }

            if (!fabricPlaced)
            {
                var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cylinder.name = "Windsock fabric";
                Object.Destroy(cylinder.GetComponent<Collider>());
                cylinder.transform.SetParent(sock, false);
                cylinder.transform.localPosition = Vector3.zero;
                cylinder.transform.localScale = new Vector3(0.55f, 0.55f, 1.35f);
                cylinder.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                cylinder.GetComponent<Renderer>().sharedMaterial = CreateMaterial(new Color(0.92f, 0.55f, 0.12f));
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
            if (ArtGltfLoader.TryPlaceCombined(
                    kit,
                    new[]
                    {
                        ("cone_base", new Color(0.2f, 0.2f, 0.22f)),
                        ("cone_body", new Color(0.95f, 0.45f, 0.08f)),
                        ("cone_stripe", Color.white),
                        ("cone_tip", new Color(0.95f, 0.45f, 0.08f)),
                        ("cone_collar", Color.white),
                        ("cone_handle", new Color(0.25f, 0.25f, 0.28f))
                    },
                    origin, Quaternion.identity, "Safety cone", out _))
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
            cone.GetComponent<Renderer>().sharedMaterial = CreateMaterial(new Color(0.95f, 0.45f, 0.08f));
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
            if (ArtGltfLoader.TryPlaceCombined(
                    kit,
                    new[]
                    {
                        ("barrier_rail", new Color(0.9f, 0.55f, 0.12f)),
                        ("barrier_rail_low", new Color(0.9f, 0.55f, 0.12f)),
                        ("barrier_stripe", Color.white),
                        ("barrier_stripe_b", Color.white),
                        ("barrier_brace", new Color(0.3f, 0.3f, 0.32f)),
                        ("barrier_brace_b", new Color(0.3f, 0.3f, 0.32f)),
                        ("barrier_top_cap", new Color(0.85f, 0.5f, 0.12f)),
                        ("barrier_leg_l", new Color(0.25f, 0.25f, 0.28f)),
                        ("barrier_leg_r", new Color(0.25f, 0.25f, 0.28f)),
                        ("barrier_foot_l", new Color(0.3f, 0.3f, 0.32f)),
                        ("barrier_foot_r", new Color(0.3f, 0.3f, 0.32f))
                    },
                    origin, rot, "Barrier", out _))
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
                AirsideStaticWorld.Attach(root);
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
                            ApplyRendererTexture(renderer, texture, new Vector2(3f, 1.5f));
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
                            ApplyRendererTexture(renderer, surface, tiling, 0.28f);
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
            // Extra dash meshes only when the strip missed — otherwise they double the GPU.
            if (!usedCentre)
            {
                CreateBlock("Runway marking mid W", new Vector3(-20f, 0.022f, 0f), new Vector3(2.2f, 0.025f, 0.24f), Color.white);
                CreateBlock("Runway marking mid E", new Vector3(20f, 0.022f, 0f), new Vector3(2.2f, 0.025f, 0.24f), Color.white);
                CreateBlock("Runway marking mid 0", new Vector3(0f, 0.022f, 0f), new Vector3(2.2f, 0.025f, 0.24f), Color.white);
                CreateBlock("Runway marking mid W2", new Vector3(-34f, 0.022f, 0f), new Vector3(2.0f, 0.025f, 0.22f), Color.white);
                CreateBlock("Runway marking mid W3", new Vector3(-6f, 0.022f, 0f), new Vector3(2.0f, 0.025f, 0.22f), Color.white);
                CreateBlock("Runway marking mid E2", new Vector3(6f, 0.022f, 0f), new Vector3(2.0f, 0.025f, 0.22f), Color.white);
                CreateBlock("Runway marking mid E3", new Vector3(34f, 0.022f, 0f), new Vector3(2.0f, 0.025f, 0.22f), Color.white);
                CreateBlock("Runway marking", new Vector3(0f, 0.02f, 0f), new Vector3(86f, 0.03f, 0.26f), Color.white);
            }

            // Kit edge / threshold strips when present; greybox fallbacks keep the strip readable.
            var usedEdgeL = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "runway_edge_left", new Vector3(0f, 0.025f, -3.35f), Quaternion.Euler(0f, 90f, 0f), Color.white, out _);
            var usedEdgeR = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "runway_edge_right", new Vector3(0f, 0.025f, 3.35f), Quaternion.Euler(0f, 90f, 0f), Color.white, out _);
            if (!usedEdgeL)
                CreateBlock("Runway edge L", new Vector3(0f, 0.025f, -3.35f), new Vector3(86f, 0.02f, 0.16f), Color.white);

            if (!usedEdgeR)
                CreateBlock("Runway edge R", new Vector3(0f, 0.025f, 3.35f), new Vector3(86f, 0.02f, 0.16f), Color.white);

            var thresholdW = AirportLayout.WestThresholdX;
            var thresholdE = AirportLayout.EastThresholdX;
            var usedThresholdW = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "runway_threshold", new Vector3(thresholdW, 0.03f, 0f), Quaternion.Euler(0f, 90f, 0f), Color.white, out _);
            var usedThresholdE = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "runway_threshold", new Vector3(thresholdE, 0.03f, 0f), Quaternion.Euler(0f, -90f, 0f), Color.white, out _);
            // Extra bar meshes only when a threshold strip missed.
            if (!usedThresholdW || !usedThresholdE)
            {
                foreach (var bar in new[] { "threshold_bar_a", "threshold_bar_b", "threshold_bar_c", "threshold_bar_d" })
                {
                    if (!usedThresholdW)
                        ArtGltfLoader.TryPlaceNamedMesh(kit, bar, new Vector3(-44f, 0.032f, 0f), Quaternion.Euler(0f, 90f, 0f), Color.white, out _);
                    if (!usedThresholdE)
                        ArtGltfLoader.TryPlaceNamedMesh(kit, bar, new Vector3(44f, 0.032f, 0f), Quaternion.Euler(0f, -90f, 0f), Color.white, out _);
                }
            }

            var usedSideWL = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "threshold_side_l", new Vector3(-44f, 0.032f, -3.0f), Quaternion.Euler(0f, 90f, 0f), Color.white, out _);
            var usedSideWR = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "threshold_side_r", new Vector3(-44f, 0.032f, 3.0f), Quaternion.Euler(0f, 90f, 0f), Color.white, out _);
            var usedSideEL = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "threshold_side_l", new Vector3(44f, 0.032f, -3.0f), Quaternion.Euler(0f, -90f, 0f), Color.white, out _);
            var usedSideER = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "threshold_side_r", new Vector3(44f, 0.032f, 3.0f), Quaternion.Euler(0f, -90f, 0f), Color.white, out _);
            if (!usedThresholdW)
                CreateBlock("Threshold W", new Vector3(thresholdW, 0.03f, 0f), new Vector3(2.2f, 0.02f, 5.4f), Color.white);
            if (!usedThresholdE)
                CreateBlock("Threshold E", new Vector3(thresholdE, 0.03f, 0f), new Vector3(2.2f, 0.02f, 5.4f), Color.white);

            var holdYellow = new Color(0.95f, 0.82f, 0.12f);
            var holdX = AirsideFlightPath.HoldShortX;
            var holdZ = AirportTaxiNetwork.RunwayHoldingPositionZ;
            var usedHoldA = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "hold_short_a", new Vector3(holdX, 0.05f, holdZ + 0.1f), Quaternion.identity, holdYellow, out var holdA);
            var usedHoldB = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "hold_short_b", new Vector3(holdX, 0.05f, holdZ + 0.6f), Quaternion.identity, holdYellow, out var holdB);
            var usedHoldC = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "hold_short_c", new Vector3(4f, 0.05f, 6.6f), Quaternion.identity, holdYellow, out var holdC);
            var usedHoldD = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "hold_short_d", new Vector3(4f, 0.05f, 7.1f), Quaternion.identity, holdYellow, out var holdD);
            if (holdA != null) holdA.name = "Hold short A";
            if (holdB != null) holdB.name = "Hold short B";
            if (holdC != null) holdC.name = "Hold short C";
            if (holdD != null) holdD.name = "Hold short D";
            if (!usedHoldA)
                CreateBlock("Hold short A", new Vector3(holdX, 0.05f, holdZ + 0.1f), new Vector3(4.2f, 0.03f, 0.22f), holdYellow);
            if (!usedHoldB)
                CreateBlock("Hold short B", new Vector3(holdX, 0.05f, holdZ + 0.6f), new Vector3(4.2f, 0.03f, 0.22f), holdYellow);
            if (!usedHoldC)
                CreateBlock("Hold short C", new Vector3(4f, 0.05f, 6.6f), new Vector3(3.6f, 0.03f, 0.2f), holdYellow);
            if (!usedHoldD)
                CreateBlock("Hold short D", new Vector3(4f, 0.05f, 7.1f), new Vector3(3.6f, 0.03f, 0.2f), holdYellow);
            // Eastern Alpha hold bars (toward stands / runway 27 end) for denser taxi authenticity.
            var usedHoldE = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "hold_short_e", new Vector3(22f, 0.05f, 6.6f), Quaternion.identity, holdYellow, out var holdE);
            var usedHoldF = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "hold_short_f", new Vector3(22f, 0.05f, 7.1f), Quaternion.identity, holdYellow, out var holdF);
            if (holdE != null) holdE.name = "Hold short E";
            if (holdF != null) holdF.name = "Hold short F";
            if (!usedHoldE)
                CreateBlock("Hold short E", new Vector3(22f, 0.05f, 6.6f), new Vector3(3.6f, 0.03f, 0.2f), holdYellow);
            if (!usedHoldF)
                CreateBlock("Hold short F", new Vector3(22f, 0.05f, 7.1f), new Vector3(3.6f, 0.03f, 0.2f), holdYellow);
            // Mid-Alpha hold bars (between A1 west and eastern stand lead) for denser taxi authenticity.
            var usedHoldG = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "hold_short_g", new Vector3(12f, 0.05f, 6.6f), Quaternion.identity, holdYellow, out var holdG);
            var usedHoldH = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "hold_short_h", new Vector3(12f, 0.05f, 7.1f), Quaternion.identity, holdYellow, out var holdH);
            if (holdG != null) holdG.name = "Hold short G";
            if (holdH != null) holdH.name = "Hold short H";
            if (!usedHoldG)
                CreateBlock("Hold short G", new Vector3(12f, 0.05f, 6.6f), new Vector3(3.6f, 0.03f, 0.2f), holdYellow);
            if (!usedHoldH)
                CreateBlock("Hold short H", new Vector3(12f, 0.05f, 7.1f), new Vector3(3.6f, 0.03f, 0.2f), holdYellow);
            // West-mid Alpha hold bars (between A1 exit and mid-Alpha) for denser taxi authenticity.
            CreateBlock("Hold short I", new Vector3(-2f, 0.05f, 6.6f), new Vector3(3.6f, 0.03f, 0.2f), holdYellow);
            CreateBlock("Hold short J", new Vector3(-2f, 0.05f, 7.1f), new Vector3(3.6f, 0.03f, 0.2f), holdYellow);
            // East-mid Alpha hold bars near A2 exit for denser taxi authenticity.
            CreateBlock("Hold short K", new Vector3(28f, 0.05f, 6.6f), new Vector3(3.6f, 0.03f, 0.2f), holdYellow);
            CreateBlock("Hold short L", new Vector3(28f, 0.05f, 7.1f), new Vector3(3.6f, 0.03f, 0.2f), holdYellow);
            // Readable block digits for 09 / 27 (facing inbound traffic).
            PlaceRunwayDigit('0', new Vector3(-34.6f, 0.04f, 0f), yaw: 90f);
            PlaceRunwayDigit('9', new Vector3(-32.6f, 0.04f, 0f), yaw: 90f);
            PlaceRunwayDigit('2', new Vector3(32.6f, 0.04f, 0f), yaw: -90f);
            PlaceRunwayDigit('7', new Vector3(34.6f, 0.04f, 0f), yaw: -90f);
            // Side stripes beside threshold bars — only when kit sides missed (avoid z-fight).
            if (!usedSideWL)
            {
                CreateBlock("Threshold stripe W L", new Vector3(-36f, 0.03f, -3.05f), new Vector3(2.2f, 0.02f, 0.45f), Color.white);
                CreateBlock("Threshold stripe W L2", new Vector3(-38.2f, 0.03f, -3.05f), new Vector3(1.8f, 0.02f, 0.4f), Color.white);
            }
            if (!usedSideWR)
            {
                CreateBlock("Threshold stripe W R", new Vector3(-36f, 0.03f, 3.05f), new Vector3(2.2f, 0.02f, 0.45f), Color.white);
                CreateBlock("Threshold stripe W R2", new Vector3(-38.2f, 0.03f, 3.05f), new Vector3(1.8f, 0.02f, 0.4f), Color.white);
            }
            if (!usedSideEL)
            {
                CreateBlock("Threshold stripe E L", new Vector3(36f, 0.03f, -3.05f), new Vector3(2.2f, 0.02f, 0.45f), Color.white);
                CreateBlock("Threshold stripe E L2", new Vector3(38.2f, 0.03f, -3.05f), new Vector3(1.8f, 0.02f, 0.4f), Color.white);
            }
            if (!usedSideER)
            {
                CreateBlock("Threshold stripe E R", new Vector3(36f, 0.03f, 3.05f), new Vector3(2.2f, 0.02f, 0.45f), Color.white);
                CreateBlock("Threshold stripe E R2", new Vector3(38.2f, 0.03f, 3.05f), new Vector3(1.8f, 0.02f, 0.4f), Color.white);
            }

            // One aiming pair per end — six pairs were densify, not ICAO aiming points.
            foreach (var x in new[] { -12f, 12f })
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

            // Two TDZ pairs per end instead of a 14-pair carpet.
            foreach (var x in new[] { -30f, -24f, 24f, 30f })
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
            PlaceRunwayDigit('2', new Vector3(14.2f, 0.04f, 24f), yaw: 0f);
            PlaceRunwayDigit('3', new Vector3(14.2f, 0.04f, 34f), yaw: 0f);

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
            // and gated off the greybox dashes. Centreline mesh is 20 m — place three copies.
            // Skip 1 m cube densify when any kit segment landed; greybox is one Alpha strip.
            var taxiPaint = new Color(0.95f, 0.85f, 0.2f);
            var usedTaxiFarWest = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "taxi_centreline", new Vector3(-8f, 0.035f, 9f), Quaternion.identity,
                taxiPaint, out _);
            var usedTaxiWest = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "taxi_centreline", new Vector3(8f, 0.035f, 9f), Quaternion.identity,
                taxiPaint, out _);
            var usedTaxiEast = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "taxi_centreline", new Vector3(28f, 0.035f, 9f), Quaternion.identity,
                taxiPaint, out _);
            if (!usedTaxiFarWest && !usedTaxiWest && !usedTaxiEast)
            {
                CreateBlock("Taxi centre Alpha", new Vector3(8f, 0.035f, 9f),
                    new Vector3(60f, 0.02f, 0.11f), taxiPaint);
            }

            CreatePaintStrip("Taxi exit centre A1",
                new Vector3(AirportLayout.DepartureEntryX, 0.035f, 4.5f),
                new Vector3(AirportLayout.AlphaJunctionX, 0.035f, AirportLayout.TaxiwayAlphaZ - 0.66f), 0.12f, taxiPaint);
            CreatePaintStrip("Taxi exit centre B1",
                new Vector3(AirportLayout.ArrivalExitX, 0.035f, 4.5f),
                new Vector3(AirportLayout.ArrivalExitX, 0.035f, AirportLayout.TaxiwayBravoZ - 0.66f), 0.12f, taxiPaint);
            CreatePaintStrip("Taxi exit centre A2",
                new Vector3(AirportLayout.TaxiwayEastX, 0.035f, AirportLayout.TaxiwayAlphaZ - 0.66f),
                new Vector3(AirportLayout.TaxiwayEastX + 12f, 0.035f, 4.5f), 0.12f, taxiPaint);

            // Edges: mesh already carries ±1.85f Z offset — place at taxi centre, identity yaw.
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
            {
                CreateBlock("Taxi edge N", new Vector3(8f, 0.035f, 10.85f),
                    new Vector3(56f, 0.02f, 0.12f), Color.white);
            }

            if (!usedTaxiEdgeS)
            {
                CreateBlock("Taxi edge S", new Vector3(8f, 0.035f, 7.15f),
                    new Vector3(56f, 0.02f, 0.12f), Color.white);
            }
            // Apron lead-in chevrons from taxi to stand lead — kit chevrons when present.
            for (var i = 0; i < 3; i++)
            {
                var z = 11.0f + i * 0.7f;
                var pos = new Vector3(13.6f + i * 0.35f, 0.04f, z);
                var mesh = i % 2 == 0 ? "chevron_lead_a" : "chevron_lead_b";
                if (i == 2) mesh = "chevron_lead_c";
                if (!ArtGltfLoader.TryPlaceNamedMesh(
                        kit, mesh, pos, Quaternion.Euler(0f, 25f, 0f),
                        new Color(0.95f, 0.85f, 0.2f), out _))
                {
                    CreateBlock($"Apron chevron {i}", pos, new Vector3(1.0f, 0.02f, 0.15f),
                        new Color(0.95f, 0.85f, 0.2f));
                }
            }
            // Second lead path toward Stand 2 / 3 for denser apron authenticity.
            for (var i = 0; i < 2; i++)
            {
                var z = 11.2f + i * 0.85f;
                var pos = new Vector3(21.5f + i * 0.4f, 0.04f, z);
                if (!ArtGltfLoader.TryPlaceNamedMesh(
                        kit, i % 2 == 0 ? "chevron_lead_a" : "chevron_lead_b", pos,
                        Quaternion.Euler(0f, 20f, 0f), new Color(0.95f, 0.85f, 0.2f), out _))
                {
                    CreateBlock($"Apron chevron east {i}", pos, new Vector3(0.9f, 0.02f, 0.14f),
                        new Color(0.95f, 0.85f, 0.2f));
                }
            }

            // Taxi direction arrows on Taxiway A (west / mid / east + one apron lead).
            PlaceTaxiArrow(kit, new Vector3(-12f, 0.04f, 9f), 90f);
            PlaceTaxiArrow(kit, new Vector3(8f, 0.04f, 9f), 90f);
            PlaceTaxiArrow(kit, new Vector3(18f, 0.04f, 9f), 90f);
            PlaceTaxiArrow(kit, new Vector3(14f, 0.04f, 12.5f), 0f);
            // Mid-Alpha edge dashes flanking hold bars G/H.
            CreateBlock("Alpha edge mid N", new Vector3(12f, 0.045f, 10.85f), new Vector3(2.4f, 0.02f, 0.12f), new Color(1f, 0.92f, 0.2f));
            CreateBlock("Alpha edge mid S", new Vector3(12f, 0.045f, 7.35f), new Vector3(2.4f, 0.02f, 0.12f), new Color(1f, 0.92f, 0.2f));
            // East-mid Alpha edge cues flanking hold bars K/L near A2.
            CreateBlock("Alpha edge east N", new Vector3(28f, 0.045f, 10.85f), new Vector3(2.4f, 0.02f, 0.12f), new Color(1f, 0.92f, 0.2f));
            CreateBlock("Alpha edge east S", new Vector3(28f, 0.045f, 7.35f), new Vector3(2.4f, 0.02f, 0.12f), new Color(1f, 0.92f, 0.2f));
            CreateBlock("Alpha centre mid", new Vector3(12f, 0.042f, 8.85f), new Vector3(1.8f, 0.018f, 0.1f), new Color(1f, 0.92f, 0.2f));
            // Apron entry arrows from densified kit when present.
            ArtGltfLoader.TryPlaceNamedMesh(kit, "apron_arrow_a", new Vector3(12f, 0.04f, 11.5f), Quaternion.identity,
                new Color(0.95f, 0.85f, 0.2f), out _);
            ArtGltfLoader.TryPlaceNamedMesh(kit, "apron_arrow_b", new Vector3(16f, 0.04f, 12.8f), Quaternion.identity,
                new Color(0.95f, 0.85f, 0.2f), out _);
            // Bay-separation joints between Stand 1/2 and 2/3 for denser apron authenticity.
            CreateBlock("Apron joint 1-2", new Vector3(18f, 0.045f, 15.5f), new Vector3(0.12f, 0.02f, 5.2f), new Color(0.55f, 0.56f, 0.57f));
            CreateBlock("Apron joint 2-3", new Vector3(26f, 0.045f, 15.5f), new Vector3(0.12f, 0.02f, 5.2f), new Color(0.55f, 0.56f, 0.57f));
            CreateBlock("Apron joint lead", new Vector3(22f, 0.045f, 12.2f), new Vector3(8.5f, 0.018f, 0.1f), new Color(0.55f, 0.56f, 0.57f));

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

            // Hold-short across the A2 eastern fillet (path (28,0)→(16,9)).
            var holdPosA2 = new Vector3(22f, 0.05f, 4.5f);
            var holdYawA2 = Mathf.Atan2(-12f, 9f) * Mathf.Rad2Deg + 90f;
            var holdRotA2 = Quaternion.Euler(0f, holdYawA2, 0f);
            if (!ArtGltfLoader.TryPlaceNamedMesh(kit, "hold_short_c", holdPosA2, holdRotA2, holdYellow, out _))
            {
                var barA2 = CreateBlock("Hold short A2", holdPosA2, new Vector3(3.4f, 0.03f, 0.22f), holdYellow);
                barA2.transform.rotation = holdRotA2;
            }

            if (!ArtGltfLoader.TryPlaceNamedMesh(
                    kit, "hold_short_d", holdPosA2 + holdRotA2 * new Vector3(0f, 0f, 0.45f), holdRotA2, holdYellow, out _))
            {
                var barA2b = CreateBlock("Hold short A2 b", holdPosA2 + holdRotA2 * new Vector3(0f, 0f, 0.45f),
                    new Vector3(3.4f, 0.03f, 0.22f), holdYellow);
                barA2b.transform.rotation = holdRotA2;
            }

            // Stand lead-in dashes — skip when markings kit already placed stand stops
            // (otherwise landing/follow cameras see a carpet of yellow cubes).
            if (FindBuilt("stand_stop_a") == null
                && FindBuilt("stand_stop_b") == null
                && FindBuilt("stand_stop_c") == null)
            {
                foreach (var standX in new[] { 14f, 22f, 30f })
                {
                    CreateBlock($"Stand lead {standX}", new Vector3(standX, 0.04f, 13.7f),
                        new Vector3(0.12f, 0.02f, 5.1f), new Color(0.95f, 0.85f, 0.2f));
                }
            }
        }

        private static void PlaceTaxiArrow(string kit, Vector3 position, float yaw)
        {
            var yellow = new Color(0.95f, 0.85f, 0.2f);
            var rot = Quaternion.Euler(0f, yaw, 0f);
            if (ArtGltfLoader.TryPlaceCombined(
                    kit,
                    new[]
                    {
                        ("taxi_arrow_shaft", yellow),
                        ("taxi_arrow_head_l", yellow),
                        ("taxi_arrow_head_r", yellow)
                    },
                    position, rot, "Taxi arrow", out _))
                return;

            var root = new GameObject("Taxi arrow").transform;
            root.position = position;
            root.rotation = rot;
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

            var edgeStep = AirsideRuntimeQuality.LightingFixtureStep;
            if (!hasLightingKit)
                edgeStep = Mathf.Min(edgeStep, 10);
            for (var x = -44; x <= 44; x += edgeStep)
            {
                PlaceEdgeLamp(kit, new Vector3(x, 0f, -3.4f), edgeColor);
                PlaceEdgeLamp(kit, new Vector3(x, 0f, 3.4f), edgeColor);
            }

            var taxiStep = AirsideRuntimeQuality.LightingFixtureStep;
            for (var x = -8; x <= 28; x += taxiStep)
            {
                PlaceTaxiLamp(kit, new Vector3(x, 0f, 11.1f), taxiColor);
                PlaceTaxiLamp(kit, new Vector3(x, 0f, 6.9f), taxiColor);
            }

            // A1 exit fillet fixtures — path (-24,0)→(-12,9).
            PlaceTaxiLamp(kit, new Vector3(-18f, 0f, 4.5f), taxiColor);
            if (AirsideRuntimeQuality.FilletLightCount >= 3)
            {
                PlaceTaxiLamp(kit, new Vector3(-22f, 0f, 2.2f), taxiColor);
                PlaceTaxiLamp(kit, new Vector3(-14f, 0f, 7f), taxiColor);
            }

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
            if (ArtGltfLoader.TryPlaceCombined(
                    kit,
                    new[]
                    {
                        ("edge_base", new Color(0.35f, 0.36f, 0.38f)),
                        ("edge_stem", new Color(0.45f, 0.46f, 0.48f)),
                        ("edge_lens", color)
                    },
                    position, Quaternion.identity, "Runway edge", out _))
                return;
            if (!ArtGltfLoader.TryPlaceNamedMesh(kit, "runway_edge_light", position, Quaternion.identity, color, out _))
                CreateBlock("Runway edge", position + new Vector3(0f, 0.05f, 0f), new Vector3(0.25f, 0.1f, 0.25f), color);
        }

        private static void PlaceTaxiLamp(string kit, Vector3 position, Color color)
        {
            if (ArtGltfLoader.TryPlaceCombined(
                    kit,
                    new[]
                    {
                        ("taxi_base", new Color(0.3f, 0.32f, 0.34f)),
                        ("taxi_stem", new Color(0.4f, 0.42f, 0.44f)),
                        ("taxi_lens", color)
                    },
                    position, Quaternion.identity, "Taxi light", out _))
                return;
            if (!ArtGltfLoader.TryPlaceNamedMesh(kit, "taxiway_light", position, Quaternion.identity, color, out _))
                CreateBlock("Taxi light", position + new Vector3(0f, 0.18f, 0f), new Vector3(0.18f, 0.35f, 0.18f), color);
        }

        private static void PlaceObstructionLamp(string kit, Vector3 position, Color color, string fallbackName)
        {
            if (ArtGltfLoader.TryPlaceCombined(
                    kit,
                    new[]
                    {
                        ("obst_base", new Color(0.35f, 0.36f, 0.38f)),
                        ("obst_stem", new Color(0.4f, 0.42f, 0.44f)),
                        ("obst_lens", color)
                    },
                    position, Quaternion.identity, fallbackName, out _))
                return;
            if (!ArtGltfLoader.TryPlaceNamedMesh(kit, "obstruction_light", position, Quaternion.identity, color, out _))
                CreateBlock(fallbackName, position + new Vector3(0f, 0.2f, 0f), new Vector3(0.22f, 0.22f, 0.22f), color);
        }

        private static void PlaceFloodMast(string kit, Vector3 position, Color color)
        {
            // Kit path: mast silhouette only — SpotLights in BuildApronLights still own night pools.
            var floodParts = AirsideRuntimeQuality.Current == AirsideRuntimeQuality.Ladder.High
                ? new[]
                {
                    ("flood_base", new Color(0.3f, 0.32f, 0.34f)),
                    ("flood_pole", color),
                    ("flood_head", new Color(0.25f, 0.26f, 0.28f)),
                    ("flood_crossarm", Shade(color, 0.9f)),
                    ("flood_arm", Shade(color, 0.85f)),
                    ("flood_lamp", new Color(1f, 0.95f, 0.8f)),
                    ("flood_visor", new Color(0.2f, 0.21f, 0.22f))
                }
                : new[]
                {
                    ("flood_base", new Color(0.3f, 0.32f, 0.34f)),
                    ("flood_pole", color),
                    ("flood_head", new Color(0.25f, 0.26f, 0.28f))
                };
            if (ArtGltfLoader.TryPlaceCombined(kit, floodParts, position, Quaternion.identity, "Apron flood", out _))
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
            var extras = new Color(0.35f, 0.36f, 0.38f);
            // Combined mesh keeps High extras (glass/rails/rollers) without extra GameObjects.
            if (ArtGltfLoader.TryPlaceCombined(
                    kit,
                    new[]
                    {
                        ("belt_loader_chassis", yellow),
                        ("belt_loader_cab", Shade(yellow, 0.85f)),
                        ("belt_loader_boom", new Color(0.55f, 0.56f, 0.58f)),
                        ("belt_loader_belt", new Color(0.25f, 0.25f, 0.26f)),
                        ("belt_loader_wheel_fl", dark),
                        ("belt_loader_wheel_fr", dark),
                        ("belt_loader_wheel_rl", dark),
                        ("belt_loader_wheel_rr", dark),
                        ("belt_loader_cab_glass", new Color(0.35f, 0.55f, 0.65f)),
                        ("belt_loader_stripe", new Color(0.15f, 0.16f, 0.18f)),
                        ("belt_loader_rail_l", dark),
                        ("belt_loader_rail_r", dark),
                        ("belt_loader_hinge", dark),
                        ("belt_loader_support", dark),
                        ("belt_loader_roller_1", extras),
                        ("belt_loader_roller_2", extras),
                        ("belt_loader_roller_3", extras),
                        ("belt_loader_roller_4", extras),
                        ("belt_loader_bumper", Shade(yellow, 0.7f)),
                        ("belt_loader_hub_fl", new Color(0.28f, 0.3f, 0.32f)),
                        ("belt_loader_hub_fr", new Color(0.28f, 0.3f, 0.32f)),
                        ("belt_loader_hitch", dark),
                        ("belt_loader_light", new Color(0.95f, 0.9f, 0.6f))
                    },
                    position, rot, "Belt loader", out _))
                return;

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
                Place("belt_loader_roller_1", extras);
                Place("belt_loader_roller_2", extras);
                Place("belt_loader_roller_3", extras);
                Place("belt_loader_roller_4", extras);
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
            if (FindBuilt("stand_stop_a") == null
                && FindBuilt("stand_stop_b") == null
                && FindBuilt("stand_stop_c") == null)
            {
                foreach (var z in new[] { 14f, 20f, 26f })
                {
                    CreateBlock($"Stand box front {z}", new Vector3(20f, 0.04f, z - 2.6f),
                        new Vector3(11.2f, 0.02f, 0.12f), Color.white);
                    CreateBlock($"Stand box back {z}", new Vector3(20f, 0.04f, z + 2.6f),
                        new Vector3(11.2f, 0.02f, 0.12f), Color.white);
                    CreateBlock($"Stand box L {z}", new Vector3(14.8f, 0.04f, z),
                        new Vector3(0.12f, 0.02f, 5.2f), Color.white);
                    CreateBlock($"Stand box R {z}", new Vector3(25.2f, 0.04f, z),
                        new Vector3(0.12f, 0.02f, 5.2f), Color.white);
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
            if (ArtGltfLoader.TryPlaceCombined(
                    kit,
                    new[]
                    {
                        ("bin", yellow),
                        ("bin_lid", dark),
                        ("bin_handle", Shade(dark, 1.15f)),
                        ("bin_stripe", new Color(0.15f, 0.16f, 0.18f))
                    },
                    position, rot, name, out _))
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
            if (ArtGltfLoader.TryPlaceCombined(
                    kit,
                    new[]
                    {
                        ("sign_post", new Color(0.35f, 0.36f, 0.38f)),
                        ("sign_face", new Color(0.95f, 0.95f, 0.92f)),
                        ("sign_cap", new Color(0.12f, 0.35f, 0.55f)),
                        ("sign_brace", new Color(0.4f, 0.42f, 0.44f)),
                        ("sign_reflector", new Color(0.85f, 0.88f, 0.9f)),
                        ("sign_base", new Color(0.3f, 0.32f, 0.34f)),
                        ("sign_glyph_bar", new Color(0.12f, 0.35f, 0.55f)),
                        ("sign_glyph_bar_b", new Color(0.12f, 0.35f, 0.55f)),
                        ("sign_glyph_dot", new Color(0.12f, 0.35f, 0.55f))
                    },
                    position, rot, "Airside sign", out _))
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
            if (ArtGltfLoader.TryPlaceCombined(
                    kit,
                    new[]
                    {
                        ("dolly_bed", new Color(0.55f, 0.35f, 0.18f)),
                        ("dolly_rail_l", new Color(0.45f, 0.3f, 0.16f)),
                        ("dolly_rail_r", new Color(0.45f, 0.3f, 0.16f)),
                        ("dolly_rail_mid", new Color(0.45f, 0.3f, 0.16f)),
                        ("dolly_rail_end", new Color(0.45f, 0.3f, 0.16f)),
                        ("dolly_post_l", new Color(0.45f, 0.3f, 0.16f)),
                        ("dolly_post_r", new Color(0.45f, 0.3f, 0.16f)),
                        ("dolly_handle", new Color(0.4f, 0.4f, 0.42f)),
                        ("dolly_hitch", new Color(0.35f, 0.35f, 0.38f)),
                        ("dolly_hitch_pin", new Color(0.3f, 0.3f, 0.32f)),
                        ("dolly_cargo", new Color(0.7f, 0.55f, 0.25f)),
                        ("dolly_bag_a", new Color(0.75f, 0.55f, 0.2f)),
                        ("dolly_bag_b", new Color(0.65f, 0.45f, 0.18f)),
                        ("dolly_bag_c", new Color(0.8f, 0.6f, 0.25f)),
                        ("dolly_wheel_fl", new Color(0.15f, 0.15f, 0.16f)),
                        ("dolly_wheel_fr", new Color(0.15f, 0.15f, 0.16f)),
                        ("dolly_wheel_rl", new Color(0.15f, 0.15f, 0.16f)),
                        ("dolly_wheel_rr", new Color(0.15f, 0.15f, 0.16f)),
                        ("dolly_hub_fl", new Color(0.45f, 0.45f, 0.48f)),
                        ("dolly_hub_fr", new Color(0.45f, 0.45f, 0.48f)),
                        ("dolly_hub_rl", new Color(0.45f, 0.45f, 0.48f)),
                        ("dolly_hub_rr", new Color(0.45f, 0.45f, 0.48f))
                    },
                    position, Quaternion.identity, "Baggage dolly", out _))
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
                CreateBlock("Fuel pad", new Vector3(-34f, 0.02f, 21.5f), new Vector3(6.5f, 0.08f, 5.6f), new Color(0.29f, 0.31f, 0.33f),
                    PreferSurfaceBasecolor("tx_concrete_apron"), new Vector2(2.2f, 1.8f));
                CreateBlock("Fuel pad centre", new Vector3(-34f, 0.05f, 21.5f), new Vector3(0.12f, 0.02f, 3.6f), new Color(0.95f, 0.85f, 0.2f));
                CreateBlock("Fuel pad edge N", new Vector3(-34f, 0.05f, 24.2f), new Vector3(4.2f, 0.02f, 0.1f), Color.white);
                CreateBlock("Fuel pad edge S", new Vector3(-34f, 0.05f, 18.8f), new Vector3(4.2f, 0.02f, 0.1f), Color.white);
                CreateBlock("Fuel pad stop", new Vector3(-34f, 0.05f, 20.2f), new Vector3(2.4f, 0.02f, 0.12f), new Color(0.95f, 0.85f, 0.2f));
                CreateTaxiChordPad("Fuel pad link", new Vector3(-30f, 0.02f, 22f), new Vector3(-24f, 0.02f, 18f), 3.6f,
                    PreferSurfaceBasecolor("tx_asphalt_runway"), new Vector2(1.1f, 1f));
                PlaceFuelTank("Fuel tank A", new Vector3(-35.5f, 1.15f, 22.5f), new Color(0.72f, 0.55f, 0.18f));
                PlaceFuelTank("Fuel tank B", new Vector3(-32.2f, 1.15f, 22.5f), new Color(0.72f, 0.55f, 0.18f));
                var bund = new Color(0.4f, 0.42f, 0.4f);
                CreateBlock("Fuel bund N", new Vector3(-34f, 0.35f, 24.15f), new Vector3(6.4f, 0.55f, 0.35f), bund);
                CreateBlock("Fuel bund S", new Vector3(-34f, 0.35f, 19.85f), new Vector3(6.4f, 0.55f, 0.35f), bund);
                CreateBlock("Fuel bund W", new Vector3(-37.05f, 0.35f, 22f), new Vector3(0.35f, 0.55f, 4.0f), bund);
                CreateBlock("Fuel bund E", new Vector3(-30.95f, 0.35f, 22f), new Vector3(0.35f, 0.55f, 4.0f), bund);
                CreateBlock("Fuel pump", new Vector3(-34f, 0.7f, 19.6f), new Vector3(1.2f, 1.2f, 0.8f), new Color(0.25f, 0.28f, 0.3f));
                CreateBlock("Fuel hose reel", new Vector3(-33.1f, 0.45f, 19.8f), new Vector3(0.55f, 0.55f, 0.55f), new Color(0.35f, 0.2f, 0.12f));
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
            tank.GetComponent<Renderer>().sharedMaterial = AirsideMaterialLibrary.CreateShared(
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

        /// <summary>Soft low whoosh at rotate — quieter than touchdown (presentation only).</summary>
        private static AudioClip CreateRotateClip()
        {
            const int sampleRate = 22050;
            var samples = new float[sampleRate / 3];
            for (var i = 0; i < samples.Length; i++)
            {
                var time = i / (float)sampleRate;
                var envelope = Mathf.Exp(-time * 9f) * (1f - time * 2.2f);
                if (envelope < 0f)
                    envelope = 0f;
                var rumble = Mathf.Sin(time * 2f * Mathf.PI * 70f) * 0.45f;
                var air = Mathf.Sin(time * 2f * Mathf.PI * (180f + time * 220f)) * 0.12f;
                samples[i] = (rumble + air) * envelope;
            }

            var clip = AudioClip.Create("Rotate whoosh", samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private Vector3 PositionFor(AircraftPhase phase, float progress, TaxiRoute taxiRoute, float laneOffset = 0f)
        {
            // Every phase hands over where the previous one ended: landing rolls out to
            // the A1 entry TaxiIn starts from, taxi-out stops at the runway hold-short
            // point takeoff lines up from, and takeoff runs straight into the climb-out.
            var t = Mathf.Clamp01(progress);
            // Number-two holds off the flare gate, but compressing progress rather than
            // clamping it means it keeps creeping down the approach instead of stopping
            // dead in mid-air the instant it reaches the hold point.
            if (phase == AircraftPhase.Approach && laneOffset != 0f && t > 0.82f)
                t = 0.82f + (t - 0.82f) * 0.08f;
            return phase switch
            {
                AircraftPhase.Approach => AirsideFlightPath.Approach(t, laneOffset),
                AircraftPhase.Landing => AirsideFlightPath.Landing(t, laneOffset),
                AircraftPhase.TaxiIn => AirsideFlightPath.OnRunwayHold(),
                AircraftPhase.AtStand => AirsideFlightPath.OnRunwayHold(),
                AircraftPhase.Pushback => AirsideFlightPath.OnRunwayHold(),
                AircraftPhase.TaxiOut => AirsideFlightPath.OnRunwayHold(),
                AircraftPhase.Takeoff => AirsideFlightPath.Takeoff(t),
                _ => AirsideFlightPath.Departed(t)
            };
        }

        private float ApproachLaneOffset(CommercialFlight flight)
        {
            if (_simulation.Flights.Count < 2)
                return 0f;
            // Number-two / later flights take a parallel final left of centreline.
            var index = 0;
            for (var i = 0; i < _simulation.Flights.Count; i++)
            {
                if (ReferenceEquals(_simulation.Flights[i], flight))
                {
                    index = i;
                    break;
                }
            }
            return index == 0 ? 0f : -4.5f * index;
        }

        /// <summary>
        /// Reverse taxi from the throat pushback left the aircraft on, out to the runway
        /// hold-short point. It used to replay the stand lead-in — a 5 m jump backwards
        /// into the bay — and then run all the way onto the runway centreline.
        /// </summary>
        private static Vector3 TaxiOutPosition(TaxiRoute route, float t)
        {
            return TaxiVisualPath.TaxiOutPosition(route, t);
        }

        private Vector3 PositionAlongTaxiRoute(TaxiRoute route, float progress, bool reverse)
        {
            return TaxiVisualPath.PositionAt(route, progress, reverse);
        }

        private static TaxiRoute TaxiRouteFor(CommercialFlight flight, AircraftPhase phase) =>
            phase is AircraftPhase.TaxiOut or AircraftPhase.Pushback or AircraftPhase.AtStand
                ? flight.DepartureRoute
                : flight.ArrivalRoute;

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
            AirsideRuntimeQuality.StripVisualCollider(block);
            var renderer = block.GetComponent<Renderer>();
            renderer.sharedMaterial = CreateSharedSurfaceMaterial(color, artTextureRelativePath, textureTiling);
            if (_airfieldRoot != null)
                block.transform.SetParent(_airfieldRoot, true);
            AirsideSceneIndex.Remember(block);
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
            var material = AirsideMaterialLibrary.CreateShared(
                tint, AirsideMaterialLibrary.SurfaceKind.Default, texture, Vector2.one);
            var renderer = quad.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            SetRendererColor(renderer, tint);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            AirsideStaticWorld.Attach(quad.transform);
        }

        private static Material CreateSharedSurfaceMaterial(Color color, string artTextureRelativePath = null, Vector2? textureTiling = null)
        {
            var kind = AirsideMaterialLibrary.InferFromTexturePath(artTextureRelativePath);
            if (kind == AirsideMaterialLibrary.SurfaceKind.Default)
                kind = InferSurfaceKindFromColor(color);
            var albedo = TryLoadArtTexture(artTextureRelativePath);
            return AirsideMaterialLibrary.CreateShared(color, kind, albedo, textureTiling);
        }

        private static Material CreateMaterial(Color color, string artTextureRelativePath = null, Vector2? textureTiling = null)
        {
            return CreateSharedSurfaceMaterial(color, artTextureRelativePath, textureTiling);
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
            return AirsideArtTextures.Load(artRelativePath);
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
                    $"{flight.AircraftId}: {FormatFlightPhase(flight)} {op.SecondsRemaining(_clock.Now)}s";
            }

            return string.Join("  ·  ", parts);
        }

        private string FormatFlightPhase(CommercialFlight flight)
        {
            if (string.Equals(_simulation.Atc.LastClearedFlight, flight.AircraftId, StringComparison.Ordinal))
            {
                switch (_simulation.Atc.ActiveClearance)
                {
                    case AtcClearance.LineUpAndWait:
                        return "Line up and wait 09";
                    case AtcClearance.NumberTwoLanding:
                        return "Number two — approach";
                    case AtcClearance.MinimumApproachSpeed:
                        return "Min approach speed";
                    case AtcClearance.HoldShortAlpha:
                        return "Hold short Alpha";
                    case AtcClearance.ContinueTaxi:
                        return _simulation.Atc.LastInstruction != null
                            && _simulation.Atc.LastInstruction.IndexOf("to the apron", StringComparison.Ordinal) >= 0
                            ? "Continue taxi to apron"
                            : "Continue taxi via Alpha";
                    case AtcClearance.GiveWayTaxiing:
                        return "Give way — taxiing traffic";
                    case AtcClearance.OrbitLeft:
                        return "Orbit left — sequencing";
                    case AtcClearance.RadarContact:
                        return "Radar contact";
                    case AtcClearance.NumberTwoDeparture:
                        return "Number two — departure";
                    case AtcClearance.GoAround:
                        return "Go around — left circuit 1000 ft";
                    case AtcClearance.PushbackApproved:
                        return "Pushback approved";
                    case AtcClearance.ReadyForDeparture:
                        return "Hold short — expect departure";
                    case AtcClearance.RunwayVacated:
                        return "Runway vacated";
                    case AtcClearance.ExpectLanding:
                        return "Expect landing clearance";
                    case AtcClearance.GroundHold:
                        return "Hold position";
                    case AtcClearance.HoldApron:
                        return "Hold on apron";
                    case AtcClearance.ReportEstablished:
                        return "Report established final";
                    case AtcClearance.ShortFinal:
                        return "Short final 09";
                    case AtcClearance.ClearedToLand:
                        return "Number one — cleared to land";
                    case AtcClearance.ClearedForTakeoff:
                        return "Number one — cleared for takeoff";
                    case AtcClearance.FrequencyChange:
                        return "Frequency change approved";
                    case AtcClearance.TrafficAdvisory:
                        return "Traffic — hold short";
                    case AtcClearance.ReportVacated:
                        return "Report runway vacated";
                    case AtcClearance.TaxiToHoldShort:
                        return "Taxi to holding point 09";
                    case AtcClearance.OnStand:
                        return "On stand — shutdown";
                    case AtcClearance.TaxiToStand:
                        return "Taxi to stand via Alpha";
                    case AtcClearance.ContactGround:
                        return "Contact Ground";
                    case AtcClearance.EngineStart:
                        return "Engine start approved";
                    case AtcClearance.ReportAirborne:
                        return "Report airborne";
                    case AtcClearance.JoinLeftDownwind:
                        return "Join left downwind 09";
                    case AtcClearance.ReportMidDownwind:
                        return "Report mid-downwind 09";
                    case AtcClearance.ReportBase:
                        return "Report base 09";
                    case AtcClearance.ReportFinal:
                        return "Report final 09";
                    case AtcClearance.ContinueApproach:
                        return "Continue approach 09";
                    case AtcClearance.ConditionalLanding:
                        return "Cleared when vacated";
                    case AtcClearance.HoldShortRunway:
                        return "Hold short runway 09";
                }
            }

            return FormatPhase(flight.Operation.Phase);
        }

        private static string FormatPhase(AircraftPhase phase) => phase switch
        {
            AircraftPhase.Approach => "On approach",
            AircraftPhase.Landing => "Cleared to land",
            AircraftPhase.TaxiIn => "Taxi via Alpha",
            AircraftPhase.AtStand => "Turnaround at stand",
            AircraftPhase.Pushback => "Pushback approved",
            AircraftPhase.TaxiOut => "Taxi — hold short 09",
            AircraftPhase.Takeoff => "Cleared for takeoff",
            AircraftPhase.Departed => "Departed",
            _ => phase.ToString()
        };

    }
}
