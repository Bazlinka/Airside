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
        private readonly List<(Renderer Renderer, Color DryColor, float DrySmoothness, float DryMetallic, float DryBumpScale)> _wetSurfaces =
            new List<(Renderer, Color, float, float, float)>();
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
        private Transform _coastFoam;
        private readonly List<(Transform Boat, Vector3 BasePos, float BaseYaw)> _coastBoats =
            new List<(Transform, Vector3, float)>();
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
            _fuelTruck = BuildServiceVehicle("Fuel truck", new Color(0.92f, 0.78f, 0.18f), new Vector3(3.1f, 1.25f, 1.35f),
                PreferArtKit(
                    "Models/Vehicles/mdl_fuel_truck_small_authored_v01.gltf",
                    "Models/Vehicles/mdl_fuel_truck_small_v04.gltf",
                    "Models/Vehicles/mdl_fuel_truck_small_v03.gltf",
                    "Models/Vehicles/mdl_fuel_truck_small_v02.gltf",
                    "Models/Vehicles/mdl_fuel_truck_small_v01.gltf"));
            _baggageCart = BuildServiceVehicle("Baggage cart", new Color(0.91f, 0.38f, 0.12f), new Vector3(2.3f, 0.8f, 1.15f),
                PreferArtKit(
                    "Models/Vehicles/mdl_baggage_tug_train_authored_v01.gltf",
                    "Models/Vehicles/mdl_baggage_tug_train_v04.gltf",
                    "Models/Vehicles/mdl_baggage_tug_train_v03.gltf",
                    "Models/Vehicles/mdl_baggage_tug_train_v02.gltf",
                    "Models/Vehicles/mdl_baggage_tug_train_v01.gltf"));
            _passengerBus = BuildServiceVehicle("Passenger bus", new Color(0.17f, 0.58f, 0.78f), new Vector3(3.8f, 1.5f, 1.45f),
                PreferArtKit(
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
                    onContinueAway: () => { _showAwaySummary = false; });
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

            _canvasHud.SetVisible(true);

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

            var toolkitActive = _toolkitHud != null && _toolkitHud.IsActive;
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
            if (toolkitOwnsOps && _toolkitHud != null)
                _toolkitHud.SyncOps(summary, body, report);
            else
                _canvasHud.SyncOps(summary, body, report);
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
                $"Cash: ${_simulation.Economy.Cash:N0}  ·  Cycles {_simulation.CompletedCycles}  ·  Rep {_simulation.Reputation.Score} ({_simulation.Reputation.Band})";
            if (_simulation.Reputation.Band == "Trusted")
                cashColor = _simulation.Economy.Cash < 0 ? AirsideTheme.SignalRed : AirsideTheme.ClearGreen;
            else if (_simulation.Reputation.Band == "Provisional" || _simulation.Reputation.Band == "At Risk")
                cashColor = _simulation.Economy.Cash < 0 ? AirsideTheme.SignalRed : AirsideTheme.SafetyYellow;

            var finance = _simulation.DailyFinance;
            var runway = finance.CashRunwayDays is int days
                ? $"  ·  ~{days}d runway"
                : "  ·  cash building";
            var financeColor = finance.ExpectedNet < 0 ? AirsideTheme.SignalRed
                : finance.CashRunwayDays is int runwayDays && runwayDays <= 3 ? AirsideTheme.SafetyYellow
                : AirsideTheme.ClearGreen;
            var financeLine =
                $"Day est. {finance.ExpectedNet:+$#,0;-$#,0;$0} (in ${finance.ExpectedFlightIncome:N0} / out ${finance.ExpectedOperatingCost:N0}){runway}";

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
                $"Ground crew: {staffing.GroundCrew}  ·  payroll ${staffing.DailyWage:N0}/day{(staffing.IsUnderstaffed ? "  ·  UNDERSTAFFED" : string.Empty)}";
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
                var next = PositionFor(phase, VisualPhaseProgress(flight, 0.15f), standZ, flight.TaxiRoute);
                view.position = position;

                var direction = next - position;
                var targetRotation = direction.sqrMagnitude > 0.001f
                    ? Quaternion.LookRotation(direction.normalized)
                    : view.rotation;
                var pitch = PhasePitchDegrees(phase, progress);
                var bank = TurnBankDegrees(view, targetRotation, phase);
                targetRotation *= Quaternion.Euler(pitch, 0f, bank);
                view.rotation = Quaternion.Slerp(view.rotation, targetRotation, Time.unscaledDeltaTime * 5f);

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
            // Presentation-only attitude: nose-up takeoff, approach pitch, landing flare.
            var t = Mathf.Clamp01(progress);
            return phase switch
            {
                AircraftPhase.Takeoff => Mathf.Lerp(0f, -11f, Mathf.SmoothStep(0f, 1f, t)),
                AircraftPhase.Approach => Mathf.Lerp(-3f, -7f, t),
                AircraftPhase.Landing => Mathf.Lerp(-6f, 1.5f, Mathf.SmoothStep(0f, 1f, t)),
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
                else if (child.name.StartsWith("Flap", StringComparison.Ordinal))
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
                else if (child.name.StartsWith("Gear", StringComparison.Ordinal))
                {
                    // Soft retract/deploy instead of a hard pop (Batch D ANM-AIR-002 language).
                    child.gameObject.SetActive(true);
                    var euler = child.localEulerAngles;
                    var current = euler.x > 180f ? euler.x - 360f : euler.x;
                    var target = airborne ? -80f : 0f;
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
                    var beaconOn = enginesOn && (Mathf.FloorToInt(Time.unscaledTime * 2f) % 2 == 0);
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
                light.intensity = 1.8f;
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
            var cabinTargetY = phase == AircraftPhase.AtStand ? -85f : 0f;
            var cargoTargetY = phase == AircraftPhase.AtStand ? 70f : 0f;
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
                if (!(n.StartsWith("Cabin window", StringComparison.OrdinalIgnoreCase)
                      || n.StartsWith("Cabin windows", StringComparison.OrdinalIgnoreCase)
                      || n.StartsWith("Cockpit", StringComparison.OrdinalIgnoreCase)
                      || n.IndexOf("cabin_window", StringComparison.OrdinalIgnoreCase) >= 0))
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

                var pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 7f + child.GetInstanceID() * 0.01f);
                child.localScale = new Vector3(0.35f * pulse * intensity, 0.35f * pulse * intensity, 0.7f);
                var renderer = child.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var color = renderer.material.color;
                    color.a = (0.12f + 0.1f * pulse) * intensity;
                    renderer.material.color = color;
                }
            }
        }

        private static void SpinPropellers(Transform aircraft, AircraftPhase phase)
        {
            // Presentation-only: RPM follows phase (Batch D ANM-AIR-001).
            if (phase == AircraftPhase.AtStand || phase == AircraftPhase.Departed)
            {
                ApplyPropBlur(aircraft, highRpm: false);
                return;
            }

            var rpm = phase switch
            {
                AircraftPhase.Takeoff => 1400f,
                AircraftPhase.Approach or AircraftPhase.Landing => 1100f,
                AircraftPhase.TaxiIn or AircraftPhase.TaxiOut or AircraftPhase.Pushback => 420f,
                _ => 720f
            };
            var degrees = Time.unscaledDeltaTime * rpm;
            var highRpm = rpm >= 1000f;
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

            var degrees = Time.unscaledDeltaTime * 520f;
            foreach (var child in aircraft.GetComponentsInChildren<Transform>(true))
            {
                if (child == aircraft)
                    continue;
                if (!child.name.StartsWith("Propeller", StringComparison.Ordinal))
                    continue;
                child.Rotate(Vector3.forward, degrees, Space.Self);
                ApplyPropBlurToHub(child, highRpm: false);
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
                AircraftPhase.Takeoff => 1.6f,
                AircraftPhase.Landing => 1.35f,
                AircraftPhase.Pushback => 0.55f,
                _ => 1f
            };
            var degrees = Time.unscaledDeltaTime * 380f * speed;
            foreach (var child in aircraft.GetComponentsInChildren<Transform>(true))
            {
                if (child == aircraft)
                    continue;
                if (child.name.StartsWith("Tire", StringComparison.Ordinal) ||
                    child.name.IndexOf("wheel", StringComparison.OrdinalIgnoreCase) >= 0)
                    child.Rotate(Vector3.right, degrees, Space.Self);
            }
        }

        private void SyncCommercialAircraftViews()
        {
            var needed = _simulation.Flights.Count;
            if (_commercialAircraft.Length == needed)
                return;

            foreach (var existing in _commercialAircraft)
            {
                if (existing != null)
                    Destroy(existing.gameObject);
            }

            _commercialAircraft = new Transform[needed];
            for (var index = 0; index < needed; index++)
            {
                var flight = _simulation.Flights[index];
                var color = index == 0
                    ? new Color(0.12f, 0.43f, 0.76f)
                    : new Color(0.18f, 0.55f, 0.48f);
                var livery = index == 0
                    ? "Textures/Decals/dc_livery_coastline_regional_v01.png"
                    : "Textures/Decals/dc_livery_emu_air_v01.png";
                _commercialAircraft[index] = BuildAircraft($"Commercial {flight.AircraftId}", color, livery);
            }

            if (needed > 0)
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
            var wind = 12f + Mathf.Sin(Time.unscaledTime * 0.7f) * 8f;
            var sway = Mathf.Sin(Time.unscaledTime * 2.4f) * 6f;
            _windsockSock.localRotation = Quaternion.Euler(0f, wind, sway);
            var stretch = 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 3.1f);
            _windsockSock.localScale = new Vector3(0.55f * stretch, 0.55f, 1.35f);
        }

        private void EnsureStandThreeVisual()
        {
            if (_standThreeVisualBuilt || !_simulation.Capacity.HasThirdStand)
                return;

            BuildStandMarking(17f, 26f, "Stand 3");
            CreateBlock("Stand 3 apron pad", new Vector3(20f, 0.01f, 26f), new Vector3(16f, 0.08f, 6f),
                new Color(0.34f, 0.36f, 0.37f),
                "Textures/Surfaces/tx_concrete_apron_basecolor_v01.png", new Vector2(2f, 1f));
            CreateCone(new Vector3(14.5f, 0.25f, 24.2f));
            CreateCone(new Vector3(14.5f, 0.25f, 27.8f));
            CreateBlock("Stand number 3", new Vector3(14.2f, 0.09f, 26.4f), new Vector3(0.9f, 0.04f, 0.28f), Color.white);
            CreateBlock("Stand number 3 mid", new Vector3(14.2f, 0.09f, 26f), new Vector3(0.9f, 0.04f, 0.28f), Color.white);
            CreateBlock("Stand number 3 stem", new Vector3(14.55f, 0.09f, 25.7f), new Vector3(0.28f, 0.04f, 1.0f), Color.white);
            _standThreeVisualBuilt = true;
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

            // Presentation-only: wheels roll while the vehicle is moving.
            var degrees = travel * 120f + (active && travel > 0.001f ? Time.unscaledDeltaTime * 180f : 0f);
            if (degrees <= 0f)
                return;
            foreach (var child in vehicle.GetComponentsInChildren<Transform>(true))
            {
                if (child == vehicle)
                    continue;
                if (child.name.IndexOf("wheel", StringComparison.OrdinalIgnoreCase) >= 0)
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
                var on = Mathf.FloorToInt(Time.unscaledTime * 3f) % 2 == 0;
                var renderer = child.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var color = on ? new Color(1f, 0.35f, 0.08f) : new Color(0.35f, 0.12f, 0.05f);
                    renderer.material.color = color;
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

            ParentBlock(vehicle, "Headlight L", new Vector3(0.55f, 0.25f, 0.35f),
                new Vector3(0.12f, 0.1f, 0.12f), new Color(0.95f, 0.92f, 0.75f));
            ParentBlock(vehicle, "Headlight R", new Vector3(0.55f, 0.25f, -0.35f),
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
                light.intensity = 0.35f + 0.2f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f));
        }

        private static void ResetServiceLoopParts(Transform vehicle)
        {
            if (vehicle == null)
                return;

            foreach (var child in vehicle.GetComponentsInChildren<Transform>(true))
            {
                if (child == vehicle)
                    continue;
                if (child.name.StartsWith("Hose", StringComparison.Ordinal))
                {
                    child.localScale = new Vector3(0.12f, 0.12f, 0.4f);
                    child.localPosition = new Vector3(child.localPosition.x, child.localPosition.y, 0.4f);
                }
                else if (child.name.StartsWith("Cargo", StringComparison.Ordinal))
                {
                    var pos = child.localPosition;
                    pos.y = 0.35f;
                    child.localPosition = pos;
                }
                else if (child.name.StartsWith("Door", StringComparison.Ordinal))
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
                if (child == vehicle || !child.name.StartsWith(partPrefix, StringComparison.Ordinal))
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
                    pos.y = 0.35f + Mathf.Sin(Time.unscaledTime * 6f) * 0.08f;
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
            var foggy = weather == WeatherKind.Fog || weather == WeatherKind.Storm;
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
                RenderSettings.fogDensity = weather == WeatherKind.Storm
                    ? Mathf.Max(baseDensity, 0.016f)
                    : foggy ? Mathf.Max(baseDensity, 0.024f)
                    : raining ? Mathf.Max(baseDensity, 0.009f)
                    : baseDensity;
            }
            // Clear weather keeps the soft day fog applied in ApplyDayCycle.

            // Darken + gloss paved surfaces when wet (VFX-004 / material wet variants).
            var wetness = wet ? (weather == WeatherKind.Storm ? 0.72f : raining ? 0.52f : 0.34f) : 0f;
            for (var i = 0; i < _wetSurfaces.Count; i++)
            {
                var (renderer, dry, drySmooth, dryMetallic, dryBump) = _wetSurfaces[i];
                if (renderer == null)
                    continue;
                AirsideMaterialLibrary.ApplyWetness(
                    renderer.material, wetness, dry, drySmooth, dryMetallic, dryBump);
            }

            UpdateWetPuddles(wetness, storm);
            UpdateTaxiSpray(wetness, raining || storm);

            // Refresh apron probe when wetness or dusk shifts so Lit pavement picks up floods.
            if (_apronProbe != null && Time.unscaledTime >= _apronProbeRefreshAt)
            {
                _apronProbe.intensity = Mathf.Lerp(0.75f, 1.15f, wetness);
                _apronProbe.RenderProbe();
                _apronProbeRefreshAt = Time.unscaledTime + (wetness > 0.05f ? 4.5f : 12f);
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
                    renderer.material.color = color;
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
                var puddle = _wetPuddleRoot.GetChild(i);
                var renderer = puddle.GetComponent<Renderer>();
                if (renderer == null)
                    continue;
                var color = renderer.material.color;
                color.a = alpha * (0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 0.7f + i));
                renderer.material.color = color;
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
                if (_previousPhases.TryGetValue(id, out var previous) &&
                    previous == AircraftPhase.Approach &&
                    phase == AircraftPhase.Landing &&
                    index < _commercialAircraft.Length)
                {
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
                        renderer.material.color = color;
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

                renderer.material.color = color;
                // Stretch slightly as the mark ages so it reads as a rollout streak.
                var scale = mark.localScale;
                scale.z = Mathf.MoveTowards(scale.z, 5.2f, Time.unscaledDeltaTime * 0.08f);
                mark.localScale = scale;
            }
        }

        private void CollectHoldShortMarkings()
        {
            _holdShortRenderers.Clear();
            foreach (var name in new[] { "Hold short A", "Hold short B", "Hold short C", "Hold short D" })
            {
                var go = GameObject.Find(name);
                if (go == null)
                    continue;
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null)
                    _holdShortRenderers.Add(renderer);
            }
        }

        private void UpdateTrafficWaitPresentation()
        {
            // Presentation-only: pulse hold-short bars when a traffic wait is active.
            var warning = _simulation.TrafficWaits.HasWarning(_clock.Now);
            var pulse = warning
                ? 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4.5f))
                : 1f;
            var baseColor = new Color(0.95f, 0.82f, 0.12f);
            var hot = new Color(1f, 0.45f, 0.12f);
            var color = warning ? Color.Lerp(baseColor, hot, pulse) : baseColor;
            for (var i = 0; i < _holdShortRenderers.Count; i++)
            {
                var renderer = _holdShortRenderers[i];
                if (renderer == null)
                    continue;
                renderer.material.color = color;
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

        private void CollectWetSurfaces()
        {
            _wetSurfaces.Clear();
            foreach (var name in new[]
                     {
                         "Runway", "Taxiway A", "Apron", "Stand 3 apron pad",
                         "Access road", "Access road turn", "Car park", "Service lane",
                         "Fuel pad", "Coast sand", "Coast shallows", "Coast foam",
                         "Coast foam inner", "Coast foam outer", "Coast water",
                         "Outer paddock N", "Outer paddock S", "Outer paddock E", "Outer paddock W",
                         "Relief berm N", "Relief berm S",
                         "Access road shoulder L", "Access road shoulder R",
                         "Runway shoulder N", "Runway shoulder S",
                         "Grass"
                     })
            {
                var go = GameObject.Find(name);
                if (go == null)
                    continue;
                var renderer = go.GetComponent<Renderer>();
                if (renderer == null)
                    continue;
                var mat = renderer.material;
                var drySmooth = 0.28f;
                if (mat.HasProperty("_Smoothness"))
                    drySmooth = mat.GetFloat("_Smoothness");
                else if (mat.HasProperty("_Glossiness"))
                    drySmooth = mat.GetFloat("_Glossiness");
                var dryMetallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0.02f;
                var dryBump = mat.HasProperty("_BumpScale") ? mat.GetFloat("_BumpScale") : 0.5f;
                _wetSurfaces.Add((renderer, mat.color, drySmooth, dryMetallic, dryBump));
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
            for (var i = 0; i < spots.Length; i++)
            {
                var puddle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                puddle.name = $"Puddle {i}";
                Object.Destroy(puddle.GetComponent<Collider>());
                puddle.transform.SetParent(root, false);
                puddle.transform.position = spots[i];
                var radius = 1.2f + (i % 3) * 0.55f;
                puddle.transform.localScale = new Vector3(radius, 0.015f, radius * (0.7f + (i % 2) * 0.25f));
                var material = AirsideMaterialLibrary.Create(
                    new Color(0.22f, 0.3f, 0.36f, 0.32f),
                    AirsideMaterialLibrary.SurfaceKind.Water);
                if (material.HasProperty("_Smoothness"))
                    material.SetFloat("_Smoothness", 0.96f);
                if (material.HasProperty("_ClearCoatMask"))
                {
                    material.SetFloat("_ClearCoatMask", 1f);
                    if (material.HasProperty("_ClearCoatSmoothness"))
                        material.SetFloat("_ClearCoatSmoothness", 0.98f);
                    material.EnableKeyword("_CLEARCOAT");
                }
                puddle.GetComponent<Renderer>().material = material;
                puddle.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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
            var rng = new System.Random(42);
            for (var i = 0; i < 96; i++)
            {
                var drop = GameObject.CreatePrimitive(PrimitiveType.Cube);
                drop.name = $"Rain {i}";
                drop.transform.SetParent(root, false);
                drop.transform.localPosition = new Vector3(
                    (float)(rng.NextDouble() * 80f - 40f),
                    (float)(rng.NextDouble() * 16f + 2f),
                    (float)(rng.NextDouble() * 50f - 10f));
                drop.transform.localScale = new Vector3(0.04f, 0.55f, 0.04f);
                drop.transform.localRotation = Quaternion.Euler(12f, 0f, 8f);
                drop.GetComponent<Renderer>().material = CreateMaterial(new Color(0.7f, 0.78f, 0.88f, 0.35f));
                var collider = drop.GetComponent<Collider>();
                if (collider != null)
                    Object.Destroy(collider);
            }

            root.gameObject.SetActive(false);
            return root;
        }

        private static Transform BuildTouchdownSmoke()
        {
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
                var pulse = 0.35f + 0.25f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.2f));
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
            // Slightly tighter FOV reads more like an architectural miniature (decision 0022).
            camera.fieldOfView = 42f;
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
            _sun.shadowStrength = 0.72f;
            _sun.shadowBias = 0.04f;

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
            _sun.intensity = Mathf.Lerp(0.08f, 1.5f, daylight);
            _sun.shadowStrength = Mathf.Lerp(0.35f, 0.78f, daylight);

            // Weather gloom cools the post stack (rain/fog/storm) without fighting day fog.
            var weather = _simulation.CurrentWeather;
            var weatherGloom = weather == WeatherKind.Storm ? 0.55f
                : weather == WeatherKind.Fog ? 0.42f
                : weather == WeatherKind.Rain ? 0.28f
                : 0f;
            if (weatherGloom > 0f)
                _sun.intensity *= Mathf.Lerp(1f, 0.72f, weatherGloom);
            _dayVolume?.Apply(daylight, warm, weatherGloom);

            if (_fillLight != null)
            {
                _fillLight.transform.rotation = Quaternion.Euler(25f, 140f - (float)cycle.Fraction * 40f, 0f);
                _fillLight.color = Color.Lerp(
                    new Color(0.25f, 0.32f, 0.55f),
                    new Color(0.55f, 0.65f, 0.85f),
                    daylight);
                _fillLight.intensity = Mathf.Lerp(0.35f, 0.18f, daylight);
            }

            var ambientDay = new Color(0.38f, 0.48f, 0.62f);   // cool shadows
            var ambientDusk = new Color(0.48f, 0.36f, 0.42f);
            var ambientNight = new Color(0.1f, 0.13f, 0.22f);
            RenderSettings.ambientLight = Color.Lerp(
                Color.Lerp(ambientNight, ambientDay, daylight),
                ambientDusk,
                warm * 0.6f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.subtractiveShadowColor = Color.Lerp(
                new Color(0.22f, 0.28f, 0.4f),
                new Color(0.35f, 0.28f, 0.32f),
                warm);

            var skyDay = AirsideTheme.OpenSky;
            var skyDusk = new Color(0.78f, 0.48f, 0.36f);
            var skyNight = new Color(0.05f, 0.07f, 0.12f);
            var sky = Color.Lerp(Color.Lerp(skyNight, skyDay, daylight), skyDusk, warm * 0.7f);
            if (_mainCamera != null)
                _mainCamera.backgroundColor = sky;
            if (_horizonDome != null)
            {
                var domeRenderer = _horizonDome.GetComponent<Renderer>();
                if (domeRenderer != null)
                    domeRenderer.material.color = sky;
            }

            UpdateSunAndMoonDiscs(daylight, warm, elevation);

            // Soft exponential fog for depth on clear days; weather can thicken it later.
            if (!Weather.IsAdverse(_simulation.CurrentWeather))
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = Color.Lerp(
                    new Color(0.08f, 0.1f, 0.16f),
                    Color.Lerp(skyDay * 0.92f, skyDusk * 0.85f, warm),
                    Mathf.Clamp01(daylight + warm * 0.25f));
                RenderSettings.fogDensity = Mathf.Lerp(0.0065f, 0.0032f, daylight);
            }

            // Apron floods come up as daylight falls (presentation only).
            if (_apronLights != null)
            {
                var flood = Mathf.Lerp(2.35f, 0.05f, daylight);
                for (var i = 0; i < _apronLights.Length; i++)
                {
                    var light = _apronLights[i];
                    if (light == null)
                        continue;
                    // Tiny phase offset flicker so floods don't feel static at night.
                    var flicker = daylight < 0.4f
                        ? 1f + 0.04f * Mathf.Sin(Time.unscaledTime * 2.1f + i * 1.7f)
                        : 1f;
                    light.intensity = flood * flicker;
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
                    light.intensity = street;
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
                var chase = Time.unscaledTime * 3.1f;
                for (var i = 0; i < _alsLights.Length; i++)
                {
                    var light = _alsLights[i];
                    if (light == null)
                        continue;
                    if (!nightChase)
                    {
                        light.intensity = alsBase;
                        light.enabled = alsBase > 0.05f;
                        continue;
                    }

                    // Chase from far approach (high index) toward the threshold (index 0).
                    var step = (_alsLights.Length - 1 - i) * 0.42f;
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
                    ? new Color(0.2f, 0.85f, 0.35f)
                    : warmWhite;
                var color = baseColor * intensity;
                color.a = 1f;
                renderer.material.color = color;
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
                         "Ops shed window glow"
                     })
            {
                var go = GameObject.Find(name);
                if (go == null)
                    continue;
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null)
                    _nightGlowRenderers.Add(renderer);

                // Real PointLight spill so dusk buildings light the apron (0025 item 5).
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

                lights.Add(light);
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
                    ? 1f + 0.06f * Mathf.Sin(Time.unscaledTime * (1.7f + i * 0.37f) + i)
                    : 1f;
                var color = new Color(1f, 0.82f, 0.45f, 1f) * (0.28f + glow * 0.85f) * flicker;
                color.a = 1f;
                var emission = new Color(1f, 0.72f, 0.32f) * (0.2f + glow * 2.4f) * flicker;
                renderer.material.color = color;
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
                        ? 1f + 0.05f * Mathf.Sin(Time.unscaledTime * (1.5f + i * 0.41f) + i * 0.7f)
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
            var blink = 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6.5f));
            var amber = new Color(1f, 0.35f, 0.12f) * (0.15f + night * 1.8f * blink);
            _arffLightbarRenderer.material.color = Color.Lerp(new Color(0.95f, 0.85f, 0.2f), amber, night);
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

            return lights.ToArray();
        }

        private static Light[] BuildApronLights()
        {
            // Spot floods aimed at stand / hangar apron so authored metal picks up
            // directional wash at dusk (0025 item 5) — fewer omnidirectional spills.
            var specs = new[]
            {
                (new Vector3(8f, 7.2f, 12f), new Vector3(17f, 0.2f, 14f)),
                (new Vector3(32f, 7.2f, 12f), new Vector3(17f, 0.2f, 20f)),
                (new Vector3(8f, 7.2f, 22f), new Vector3(26f, 0.2f, 24f)),
                (new Vector3(32f, 7.2f, 22f), new Vector3(20f, 0.2f, 17f)),
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
                light.color = new Color(1f, 0.93f, 0.8f);
                light.range = 34f;
                light.spotAngle = 78f;
                light.innerSpotAngle = 42f;
                light.intensity = 0.05f;
                light.shadows = LightShadows.None;
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
            for (var x = -36; x <= 36; x += 8)
            {
                lights.Add(CreateEdgePointLight($"Runway edge point L {x}", new Vector3(x, 0.55f, -3.4f)));
                lights.Add(CreateEdgePointLight($"Runway edge point R {x}", new Vector3(x, 0.55f, 3.4f)));
            }

            // Green taxi centreline hints along A1 / stand lead-in.
            for (var x = -12; x <= 28; x += 8)
            {
                lights.Add(CreateEdgePointLight($"Taxi point {x}", new Vector3(x, 0.45f, 9f),
                    new Color(0.25f, 0.9f, 0.4f), range: 7.5f));
            }

            // REIL-style white flashers just beyond each threshold (blinked later).
            lights.Add(CreateEdgePointLight("REIL W L", new Vector3(-44f, 1.6f, -2.8f),
                new Color(1f, 1f, 0.95f), range: 16f));
            lights.Add(CreateEdgePointLight("REIL W R", new Vector3(-44f, 1.6f, 2.8f),
                new Color(1f, 1f, 0.95f), range: 16f));
            lights.Add(CreateEdgePointLight("REIL E L", new Vector3(44f, 1.6f, -2.8f),
                new Color(1f, 1f, 0.95f), range: 16f));
            lights.Add(CreateEdgePointLight("REIL E R", new Vector3(44f, 1.6f, 2.8f),
                new Color(1f, 1f, 0.95f), range: 16f));
            CreateBlock("REIL post W L", new Vector3(-44f, 0.8f, -2.8f), new Vector3(0.18f, 1.6f, 0.18f), new Color(0.4f, 0.42f, 0.44f));
            CreateBlock("REIL post W R", new Vector3(-44f, 0.8f, 2.8f), new Vector3(0.18f, 1.6f, 0.18f), new Color(0.4f, 0.42f, 0.44f));
            CreateBlock("REIL post E L", new Vector3(44f, 0.8f, -2.8f), new Vector3(0.18f, 1.6f, 0.18f), new Color(0.4f, 0.42f, 0.44f));
            CreateBlock("REIL post E R", new Vector3(44f, 0.8f, 2.8f), new Vector3(0.18f, 1.6f, 0.18f), new Color(0.4f, 0.42f, 0.44f));
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
            for (var i = 0; i < specs.Length; i++)
            {
                var spec = specs[i];
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
            var lights = new Light[positions.Length];
            for (var i = 0; i < positions.Length; i++)
            {
                var pos = positions[i];
                CreateBlock($"Streetlight pole {i}", pos + new Vector3(0f, 2.2f, 0f), new Vector3(0.14f, 4.4f, 0.14f),
                    new Color(0.35f, 0.36f, 0.38f));
                CreateBlock($"Streetlight head {i}", pos + new Vector3(0.35f, 4.35f, 0f), new Vector3(0.7f, 0.18f, 0.35f),
                    new Color(0.25f, 0.26f, 0.28f));
                CreateBlock($"Streetlight lamp {i}", pos + new Vector3(0.55f, 4.2f, 0f), new Vector3(0.28f, 0.16f, 0.28f),
                    new Color(1f, 0.92f, 0.7f));

                var go = new GameObject($"Landside streetlight {i + 1}");
                go.transform.position = pos + new Vector3(0.55f, 4.1f, 0f);
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
            // Presentation-only rotating aerodrome beacon (greybox mast + point light).
            var mast = new GameObject("Aerodrome beacon").transform;
            mast.position = new Vector3(38f, 0f, 18f);
            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Beacon mast";
            Object.Destroy(pole.GetComponent<Collider>());
            pole.transform.SetParent(mast, false);
            pole.transform.localPosition = new Vector3(0f, 4.5f, 0f);
            pole.transform.localScale = new Vector3(0.18f, 4.5f, 0.18f);
            pole.GetComponent<Renderer>().material.color = new Color(0.55f, 0.56f, 0.58f);

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Beacon head";
            Object.Destroy(head.GetComponent<Collider>());
            head.transform.SetParent(mast, false);
            head.transform.localPosition = new Vector3(0f, 9.1f, 0f);
            head.transform.localScale = new Vector3(0.55f, 0.55f, 0.55f);
            head.GetComponent<Renderer>().material.color = new Color(0.95f, 0.95f, 0.9f);

            var lightGo = new GameObject("Beacon light");
            lightGo.transform.SetParent(mast, false);
            lightGo.transform.localPosition = new Vector3(0f, 9.1f, 0f);
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

            var pulse = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3.2f));
            _aerodromeBeacon.intensity = pulse * Mathf.Lerp(2.4f, 0.2f, daylight / 0.38f);
            _aerodromeBeacon.color = Mathf.FloorToInt(Time.unscaledTime * 1.6f) % 2 == 0
                ? new Color(0.95f, 0.98f, 1f)
                : new Color(0.35f, 0.95f, 0.55f);
        }

        private static void BuildAirfield()
        {
            // Batch B surfaces (Approved): textured when Art PNGs load; solid colours remain fallback.
            CreateBlock("Grass", new Vector3(0f, -0.65f, 4f), new Vector3(94f, 1f, 66f), Shade(AirsideTheme.Eucalyptus, 0.55f),
                "Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png", new Vector2(12f, 8f));
            CreateBlock("Runway", new Vector3(0f, -0.08f, 0f), new Vector3(78f, 0.15f, 7f), new Color(0.105f, 0.12f, 0.14f),
                "Textures/Surfaces/tx_asphalt_runway_basecolor_v01.png", new Vector2(10f, 1.2f));
            CreateBlock("Runway shoulder N", new Vector3(0f, -0.1f, 4.2f), new Vector3(76f, 0.08f, 1.4f), new Color(0.28f, 0.3f, 0.28f),
                "Textures/Surfaces/tx_concrete_apron_basecolor_v01.png", new Vector2(8f, 0.3f));
            CreateBlock("Runway shoulder S", new Vector3(0f, -0.1f, -4.2f), new Vector3(76f, 0.08f, 1.4f), new Color(0.28f, 0.3f, 0.28f),
                "Textures/Surfaces/tx_concrete_apron_basecolor_v01.png", new Vector2(8f, 0.3f));
            CreateBlock("Taxiway A", new Vector3(8f, -0.02f, 9f), new Vector3(48f, 0.12f, 4f), new Color(0.22f, 0.24f, 0.26f),
                "Textures/Surfaces/tx_asphalt_runway_basecolor_v01.png", new Vector2(6f, 0.8f));
            CreateBlock("Apron", new Vector3(20f, 0f, 17f), new Vector3(28f, 0.12f, 14f), new Color(0.34f, 0.36f, 0.37f),
                "Textures/Surfaces/tx_concrete_apron_basecolor_v01.png", new Vector2(4f, 2f));
            // Batch C buildings — prefer richer v03 kits (0025 item 2) with v02/v01 fallback.
            PlaceBuildingOrFallback(
                PreferArtKit(
                    "Models/Buildings/mdl_terminal_regional_small_authored_v01.gltf",
                    "Models/Buildings/mdl_terminal_regional_small_v04.gltf",
                    "Models/Buildings/mdl_terminal_regional_small_v03.gltf",
                    "Models/Buildings/mdl_terminal_regional_small_v02.gltf",
                    "Models/Buildings/mdl_terminal_regional_small_v01.gltf"),
                new Vector3(26f, 0f, 27f),
                name => name switch
                {
                    "glass_front" or "windows" or "entrance" or "cabin_windows" or "landside_glass"
                        or "window_mullion_1" or "window_mullion_2" or "window_mullion_3"
                        or "window_mullion_4" or "window_mullion_5"
                        or "window_mullion_6" or "window_mullion_7"
                        or "window_mullion_8" or "window_mullion_9"
                        or "window_transom" or "window_sill" or "window_header"
                        or "landside_mullion_1" or "landside_mullion_2" or "landside_mullion_3"
                        or "entrance_transom" => new Color(0.16f, 0.38f, 0.5f),
                    "canopy" or "canopy_post_l" or "canopy_post_r" or "canopy_post_ml" or "canopy_post_mr"
                        or "canopy_beam" or "canopy_edge" or "roof_slab" or "roof_plant" or "roof_plant_b"
                        or "roof_plant_c" or "roof_parapet" or "landside_awning" or "signage_bar"
                        or "signage_cap" or "hvac_duct" or "flag_pole" => new Color(0.55f, 0.58f, 0.6f),
                    "end_cap_left" or "end_cap_right" or "column_l" or "column_r" or "column_ml" or "column_mr"
                        or "entrance_frame" or "buttress_r" => new Color(0.62f, 0.66f, 0.69f),
                    "service_wing" or "service_door" or "baggage_door" or "baggage_ramp" => new Color(0.58f, 0.62f, 0.64f),
                    _ => new Color(0.68f, 0.72f, 0.75f)
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
                surfaceMeshNames: new[] { "terminal_body", "end_cap", "service_wing", "roof", "canopy", "buttress" });
            // Warm interior spill at dusk/night (presentation only).
            CreateBlock("Terminal window glow L", new Vector3(20f, 2.35f, 24.5f), new Vector3(5.5f, 1.6f, 0.08f), new Color(1f, 0.82f, 0.45f));
            CreateBlock("Terminal window glow R", new Vector3(32f, 2.35f, 24.5f), new Vector3(5.5f, 1.6f, 0.08f), new Color(1f, 0.82f, 0.45f));
            CreateBlock("Terminal landside glow", new Vector3(26f, 2.2f, 29.4f), new Vector3(10f, 1.4f, 0.08f), new Color(1f, 0.8f, 0.42f));
            BuildTerminalLandsideCanopy();
            CreateBlock("Hangar window glow", new Vector3(-20f, 3.2f, 24.55f), new Vector3(4.5f, 1.8f, 0.08f), new Color(1f, 0.75f, 0.35f));
            CreateBlock("Ops shed window glow", new Vector3(-8f, 1.5f, 24.1f), new Vector3(3.2f, 1.1f, 0.08f), new Color(1f, 0.78f, 0.4f));
            PlaceBuildingOrFallback(
                PreferArtKit(
                    "Models/Buildings/mdl_hangar_small_authored_v01.gltf",
                    "Models/Buildings/mdl_hangar_small_v04.gltf",
                    "Models/Buildings/mdl_hangar_small_v03.gltf",
                    "Models/Buildings/mdl_hangar_small_v02.gltf",
                    "Models/Buildings/mdl_hangar_small_v01.gltf"),
                new Vector3(-20f, 0f, 20f),
                name => name switch
                {
                    "door_opening" or "door_panel_l" or "door_panel_r" or "door_rib_l" or "door_rib_r"
                        or "door_bar_l1" or "door_bar_l2" or "door_bar_l3"
                        or "door_bar_r1" or "door_bar_r2" or "door_bar_r3"
                        or "door_handle_l" or "door_handle_r"
                        or "personnel_door" or "personnel_frame" => new Color(0.22f, 0.24f, 0.26f),
                    "roof_ridge" or "roof_panel_l" or "roof_panel_r" or "crane_beam" or "crane_trolley"
                        or "crane_hook" or "gutter_front"
                        or "roof_rib_1" or "roof_rib_2" or "roof_rib_3" or "roof_rib_4"
                        or "roof_rib_5" or "roof_rib_6" or "roof_rib_7"
                        or "skylight_l" or "skylight_r" or "skylight_mid"
                        or "flood_can_l" or "flood_can_r" or "downpipe_l" or "downpipe_r"
                        => new Color(0.4f, 0.44f, 0.48f),
                    "buttress_l" or "buttress_r" or "door_track_l" or "door_track_r" or "door_track_mid"
                        or "side_vent" or "side_vent_b" or "office_lean" or "office_window" or "office_door"
                        or "side_window" or "side_window_b"
                        or "column_ml" or "column_mr" => new Color(0.42f, 0.46f, 0.5f),
                    _ => new Color(0.45f, 0.5f, 0.54f)
                },
                () =>
                {
                    CreateBlock("Hangar", new Vector3(-20f, 2.5f, 20f), new Vector3(14f, 5f, 9f), new Color(0.45f, 0.5f, 0.54f),
                        "Textures/Surfaces/tx_corrugated_metal_basecolor_v01.png", new Vector2(2.5f, 1.5f));
                    CreateBlock("Hangar door", new Vector3(-20f, 2.0f, 24.6f), new Vector3(8f, 4f, 0.2f), new Color(0.22f, 0.24f, 0.26f));
                },
                surfaceTextureRelativePath: "Textures/Surfaces/tx_corrugated_metal_basecolor_v01.png",
                surfaceTextureTiling: new Vector2(2.5f, 1.5f),
                surfaceMeshNames: new[] { "hangar_shell", "roof", "buttress", "door_track", "side_vent" });
            // Sliding door slab always present (covers kit opening or fallback hangar).
            if (GameObject.Find("Hangar door") == null)
                CreateBlock("Hangar door", new Vector3(-20f, 2.0f, 24.6f), new Vector3(8f, 4f, 0.2f), new Color(0.22f, 0.24f, 0.26f));
            BuildHangarBayInterior();
            PlaceBuildingOrFallback(
                PreferArtKit(
                    "Models/Buildings/mdl_operations_shed_authored_v01.gltf",
                    "Models/Buildings/mdl_operations_shed_v04.gltf",
                    "Models/Buildings/mdl_operations_shed_v03.gltf",
                    "Models/Buildings/mdl_operations_shed_v02.gltf",
                    "Models/Buildings/mdl_operations_shed_v01.gltf"),
                new Vector3(-8f, 0f, 26f),
                name => name switch
                {
                    "window_l" or "window_r" or "window_side" or "window_side_b"
                        or "window_mullion_l" or "window_mullion_r"
                        or "window_transom_l" or "window_transom_r" => new Color(0.2f, 0.4f, 0.5f),
                    "door" or "door_frame" or "door_knob" => new Color(0.35f, 0.38f, 0.34f),
                    "porch_roof" or "porch_beam" or "roof_ridge" or "roof_panel" or "roof_gutter"
                        or "roof_fascia" or "roof_downpipe_l" or "roof_downpipe_r"
                        or "antenna_mast" or "antenna_dish" or "antenna_boom" or "antenna_guy"
                        or "ac_unit" or "ac_unit_b" or "ac_grille" or "radio_rack"
                        or "vent_pipe" or "wall_vent" or "signage" or "flood_can"
                        or "porch_post_l" or "porch_post_r"
                        or "window_sill_l" or "window_sill_r"
                        or "step_rail_l" or "step_rail_r" => new Color(0.48f, 0.5f, 0.46f),
                    _ => new Color(0.55f, 0.58f, 0.52f)
                },
                () => CreateBlock("Ops shed", new Vector3(-8f, 1.4f, 26f), new Vector3(6f, 2.8f, 4f), new Color(0.55f, 0.58f, 0.52f),
                    "Textures/Surfaces/tx_corrugated_metal_basecolor_v01.png", new Vector2(1.5f, 1.2f)),
                surfaceTextureRelativePath: "Textures/Surfaces/tx_corrugated_metal_basecolor_v01.png",
                surfaceTextureTiling: new Vector2(1.5f, 1.2f),
                surfaceMeshNames: new[] { "shed_body", "porch", "roof" });

            CreateDecalQuad("Runway wear", new Vector3(0f, 0.02f, 0f), new Vector3(60f, 1f, 2.4f),
                "Textures/Decals/dc_runway_wear_v01.png");
            CreateDecalQuad("Apron stains", new Vector3(20f, 0.06f, 17f), new Vector3(18f, 1f, 10f),
                "Textures/Decals/dc_apron_stains_v01.png");

            PlaceWorldMarkings();
            PlaceWorldLighting();
            PlaceWorldProps();
            BuildEnvironmentContext();

            BuildStandMarking(17f, 14f, "Stand 1");
            BuildStandMarking(17f, 20f, "Stand 2");
            // Simple painted stand digits (WLD-001 language; not real typography assets).
            CreateBlock("Stand number 1", new Vector3(14.2f, 0.09f, 14f), new Vector3(0.35f, 0.04f, 1.2f), Color.white);
            CreateBlock("Stand number 2 stem", new Vector3(14.2f, 0.09f, 20.35f), new Vector3(0.9f, 0.04f, 0.28f), Color.white);
            CreateBlock("Stand number 2 mid", new Vector3(14.2f, 0.09f, 20f), new Vector3(0.9f, 0.04f, 0.28f), Color.white);
            CreateBlock("Stand number 2 base", new Vector3(14.2f, 0.09f, 19.65f), new Vector3(0.9f, 0.04f, 0.28f), Color.white);

        }

        /// <summary>
        /// Decision 0025 item 3 — regional environment greybox around the operating
        /// airfield: coast, access road, car park, fencing, vegetation and a soft
        /// horizon dome. Presentation only; primitives + existing Batch B surfaces.
        /// </summary>
        private static void BuildEnvironmentContext()
        {
            // Outer paddock + dry-grass fringe so the airfield is not a floating island.
            CreateBlock("Outer paddock N", new Vector3(0f, -0.85f, 48f), new Vector3(140f, 0.8f, 40f), Shade(AirsideTheme.DryGrass, 0.7f),
                "Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png", new Vector2(18f, 6f));
            CreateBlock("Outer paddock S", new Vector3(0f, -0.85f, -36f), new Vector3(140f, 0.8f, 36f), Shade(AirsideTheme.DryGrass, 0.65f),
                "Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png", new Vector2(18f, 5f));
            CreateBlock("Outer paddock E", new Vector3(68f, -0.85f, 4f), new Vector3(36f, 0.8f, 90f), Shade(AirsideTheme.Eucalyptus, 0.45f),
                "Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png", new Vector2(5f, 12f));
            CreateBlock("Outer paddock W", new Vector3(-68f, -0.85f, 4f), new Vector3(36f, 0.8f, 90f), Shade(AirsideTheme.Eucalyptus, 0.45f),
                "Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png", new Vector2(5f, 12f));

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
            // Horizontal bay rows + vertical stall dividers so the park reads from overview.
            for (var i = 0; i < 5; i++)
            {
                var z = 42f + i * 2.0f;
                CreateBlock($"Bay line {i}", new Vector3(48f, 0.05f, z), new Vector3(14f, 0.02f, 0.08f), Color.white);
            }

            for (var i = 0; i < 5; i++)
            {
                var x = 41.5f + i * 3.6f;
                CreateBlock($"Stall line {i}", new Vector3(x, 0.05f, 46f), new Vector3(0.08f, 0.02f, 10f), Color.white);
            }

            CreateBlock("Car park kerb N", new Vector3(48f, 0.12f, 52.2f), new Vector3(18.5f, 0.2f, 0.35f), AirsideTheme.Concrete);
            CreateBlock("Car park kerb S", new Vector3(48f, 0.12f, 39.8f), new Vector3(18.5f, 0.2f, 0.35f), AirsideTheme.Concrete);
            CreateBlock("Access centreline", new Vector3(26f, 0.05f, 38f), new Vector3(0.12f, 0.02f, 18f), new Color(0.95f, 0.85f, 0.2f));
            CreateBlock("Access edge L", new Vector3(23.1f, 0.05f, 38f), new Vector3(0.1f, 0.02f, 18f), Color.white);
            CreateBlock("Access edge R", new Vector3(28.9f, 0.05f, 38f), new Vector3(0.1f, 0.02f, 18f), Color.white);
            CreateBlock("Access road shoulder L", new Vector3(22.2f, -0.01f, 38f), new Vector3(1.2f, 0.06f, 20f), Shade(AirsideTheme.Concrete, 0.85f),
                "Textures/Surfaces/tx_concrete_apron_basecolor_v01.png", new Vector2(0.4f, 3f));
            CreateBlock("Access road shoulder R", new Vector3(29.8f, -0.01f, 38f), new Vector3(1.2f, 0.06f, 20f), Shade(AirsideTheme.Concrete, 0.85f),
                "Textures/Surfaces/tx_concrete_apron_basecolor_v01.png", new Vector2(0.4f, 3f));
            CreateBlock("Drop-off zebra", new Vector3(26f, 0.05f, 34.5f), new Vector3(5.5f, 0.02f, 0.35f), Color.white);
            CreateBlock("Parking sign post", new Vector3(39.5f, 1.1f, 40.5f), new Vector3(0.12f, 2.2f, 0.12f), new Color(0.45f, 0.46f, 0.48f));
            CreateBlock("Parking sign face", new Vector3(39.5f, 2.0f, 40.5f), new Vector3(0.08f, 0.7f, 0.9f), AirsideTheme.SafetyYellow);

            // Hangar service lane.
            CreateBlock("Service lane", new Vector3(-20f, -0.02f, 28.5f), new Vector3(18f, 0.08f, 3.2f), new Color(0.24f, 0.26f, 0.28f),
                "Textures/Surfaces/tx_asphalt_runway_basecolor_v01.png", new Vector2(3f, 0.6f));
            CreateBlock("Service lane centreline", new Vector3(-20f, 0.04f, 28.5f), new Vector3(14f, 0.02f, 0.1f),
                new Color(0.95f, 0.85f, 0.2f));
            CreateBlock("Service lane edge N", new Vector3(-20f, 0.04f, 29.9f), new Vector3(16f, 0.02f, 0.08f), Color.white);
            CreateBlock("Service lane edge S", new Vector3(-20f, 0.04f, 27.1f), new Vector3(16f, 0.02f, 0.08f), Color.white);

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
            // North/south berms framing the runway strip.
            CreateBlock("Relief berm N", new Vector3(0f, 0.15f, 30f), new Vector3(70f, 0.55f, 4.5f), grass,
                "Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png", new Vector2(8f, 1.2f));
            CreateBlock("Relief berm S", new Vector3(0f, 0.12f, -22f), new Vector3(64f, 0.45f, 5f), dry,
                "Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png", new Vector2(7f, 1f));
            // Scattered mounds — fixed offsets keep layout stable across runs.
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
                new Vector3(-6f, 0.12f, -40f)
            };
            for (var i = 0; i < mounds.Length; i++)
            {
                var pos = mounds[i];
                var size = new Vector3(4.5f + (i % 3) * 1.2f, 0.35f + (i % 4) * 0.08f, 3.2f + (i % 2) * 1.1f);
                var color = i % 3 == 0 ? sand : i % 3 == 1 ? dry : grass;
                CreateBlock($"Relief mound {i}", pos, size, color,
                    "Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png", new Vector2(1.5f, 1.2f));
            }

            // Coastal dune rise between apron grass and sand strip.
            CreateBlock("Coast dune L", new Vector3(-28f, 0.35f, -42f), new Vector3(18f, 0.9f, 5f), sand,
                "Textures/Surfaces/tx_sand_coast_basecolor_v01.png", new Vector2(3f, 1.2f));
            CreateBlock("Coast dune R", new Vector3(24f, 0.3f, -43f), new Vector3(16f, 0.75f, 4.5f), sand,
                "Textures/Surfaces/tx_sand_coast_basecolor_v01.png", new Vector2(2.8f, 1.1f));
            CreateBlock("Coast dune mid", new Vector3(0f, 0.22f, -41f), new Vector3(22f, 0.55f, 3.5f), Shade(sand, 0.9f),
                "Textures/Surfaces/tx_sand_coast_basecolor_v01.png", new Vector2(3.5f, 1f));
        }

        /// <summary>
        /// Stylised staff / passenger silhouettes — readable life, not characters.
        /// </summary>
        private static void BuildApronLife()
        {
            var root = new GameObject("Apron life").transform;
            PlacePerson(root, "Marshaller", new Vector3(14.5f, 0f, 16.5f), 200f, new Color(0.85f, 0.55f, 0.12f));
            PlacePerson(root, "Fueler", new Vector3(-3.2f, 0f, 13.2f), 90f, new Color(0.2f, 0.35f, 0.55f));
            PlacePerson(root, "Ops walker", new Vector3(22f, 0f, 22.5f), 15f, new Color(0.25f, 0.28f, 0.32f));
            PlacePerson(root, "Ramp walker", new Vector3(18.5f, 0f, 14.2f), 95f, new Color(0.55f, 0.35f, 0.18f));
            PlacePerson(root, "Hangar tech", new Vector3(-16f, 0f, 18.5f), 270f, new Color(0.35f, 0.4f, 0.45f));
            PlacePerson(root, "Hangar tech B", new Vector3(-18.5f, 0f, 17.2f), 200f, new Color(0.4f, 0.42f, 0.38f));
            PlacePerson(root, "Landside passenger A", new Vector3(26.5f, 0f, 31.8f), 180f, new Color(0.45f, 0.22f, 0.2f));
            PlacePerson(root, "Landside passenger B", new Vector3(27.8f, 0f, 31.6f), 175f, new Color(0.2f, 0.35f, 0.4f));
            PlacePerson(root, "Landside passenger C", new Vector3(25.6f, 0f, 31.4f), 190f, new Color(0.3f, 0.32f, 0.45f));
            PlacePerson(root, "Landside passenger D", new Vector3(28.5f, 0f, 32.2f), 160f, new Color(0.5f, 0.45f, 0.35f));
            PlacePerson(root, "Bench sitter", new Vector3(29.5f, 0.15f, 31.5f), 0f, new Color(0.35f, 0.3f, 0.28f), seated: true);
            PlacePerson(root, "Gate attendant", new Vector3(24.2f, 0f, 30.8f), 200f, new Color(0.55f, 0.58f, 0.62f));
            PlacePerson(root, "Car park walker", new Vector3(34f, 0f, 34f), 220f, new Color(0.4f, 0.25f, 0.3f));
            PlacePerson(root, "Fuel pad walker", new Vector3(-32f, 0f, 20.5f), 110f, new Color(0.55f, 0.4f, 0.2f));
            PlacePerson(root, "Stand 2 marshaller", new Vector3(22.5f, 0f, 16.8f), 185f, new Color(0.9f, 0.5f, 0.1f));
        }

        private static void PlacePerson(Transform parent, string name, Vector3 position, float yaw, Color clothes, bool seated = false)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.position = position;
            root.rotation = Quaternion.Euler(0f, yaw, 0f);
            var bodyH = seated ? 0.55f : 0.85f;
            var bodyY = seated ? 0.55f : 0.9f;
            ParentBlock(root, $"{name} torso", new Vector3(0f, bodyY, 0f), new Vector3(0.38f, bodyH, 0.22f), clothes);
            ParentBlock(root, $"{name} head", new Vector3(0f, bodyY + bodyH * 0.55f + 0.18f, 0f), new Vector3(0.22f, 0.22f, 0.22f),
                new Color(0.78f, 0.62f, 0.5f));
            if (!seated)
            {
                ParentBlock(root, $"{name} leg L", new Vector3(-0.1f, 0.35f, 0f), new Vector3(0.14f, 0.7f, 0.14f), Shade(clothes, 0.7f));
                ParentBlock(root, $"{name} leg R", new Vector3(0.1f, 0.35f, 0f), new Vector3(0.14f, 0.7f, 0.14f), Shade(clothes, 0.7f));
                ParentBlock(root, $"{name} arm L", new Vector3(-0.28f, bodyY + 0.05f, 0f), new Vector3(0.12f, 0.55f, 0.12f), Shade(clothes, 0.85f));
                ParentBlock(root, $"{name} arm R", new Vector3(0.28f, bodyY + 0.05f, 0f), new Vector3(0.12f, 0.55f, 0.12f), Shade(clothes, 0.85f));
            }
            else
            {
                ParentBlock(root, $"{name} legs", new Vector3(0f, 0.28f, 0.2f), new Vector3(0.4f, 0.2f, 0.55f), Shade(clothes, 0.7f));
            }
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

            // Soft idle lean on torsos so figures don't read as frozen props.
            for (var i = 0; i < _apronLifeRoot.childCount; i++)
            {
                var person = _apronLifeRoot.GetChild(i);
                if (person.name.IndexOf("sitter", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                var wave = false;
                if (person.name.StartsWith("Marshaller", StringComparison.Ordinal))
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

                // Short shuffle for walkers so the apron edge reads busy.
                if (person.name.IndexOf("walker", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    float baseX = 22f, baseZ = 22.5f;
                    if (person.name.StartsWith("Ramp", StringComparison.Ordinal))
                    {
                        baseX = 18.5f;
                        baseZ = 14.2f;
                    }
                    else if (person.name.StartsWith("Car park", StringComparison.Ordinal))
                    {
                        baseX = 34f;
                        baseZ = 34f;
                    }
                    else if (person.name.StartsWith("Ops", StringComparison.Ordinal))
                    {
                        baseX = 22f;
                        baseZ = 22.5f;
                    }

                    var ox = Mathf.Sin(Time.unscaledTime * 0.28f + i) * 1.8f;
                    var oz = Mathf.Cos(Time.unscaledTime * 0.22f + i * 0.7f) * 1.1f;
                    person.position = new Vector3(baseX + ox, 0f, baseZ + oz);
                    var look = new Vector3(-oz, 0f, ox);
                    if (look.sqrMagnitude > 0.0001f)
                        person.rotation = Quaternion.Slerp(person.rotation, Quaternion.LookRotation(look.normalized),
                            Time.unscaledDeltaTime * 2f);
                }

                foreach (var child in person.GetComponentsInChildren<Transform>(true))
                {
                    if (child == person)
                        continue;
                    if (child.name.IndexOf("torso", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var lean = Mathf.Sin(Time.unscaledTime * 0.9f + i * 1.3f) * 4f;
                        if (wave)
                            lean += Mathf.Sin(Time.unscaledTime * 4f) * 16f;
                        child.localEulerAngles = new Vector3(0f, 0f, lean);
                    }
                    else if (wave && child.name.IndexOf("arm", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var swing = Mathf.Sin(Time.unscaledTime * 5f + (child.name.Contains("L") ? 0f : 1.2f)) * 35f;
                        child.localEulerAngles = new Vector3(swing, 0f, child.name.Contains("L") ? -12f : 12f);
                    }
                    else if (person.name.IndexOf("walker", StringComparison.OrdinalIgnoreCase) >= 0
                             && child.name.IndexOf("leg", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var stride = Mathf.Sin(Time.unscaledTime * 5.5f + (child.name.Contains("L") ? 0f : 3.14f)) * 18f;
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
            // Timber jetty reaching into the shallows.
            CreateBlock("Jetty deck", new Vector3(-18f, -0.15f, -52f), new Vector3(2.4f, 0.18f, 14f), new Color(0.45f, 0.32f, 0.18f));
            for (var i = 0; i < 5; i++)
            {
                var z = -46f - i * 2.5f;
                CreateBlock($"Jetty pile L {i}", new Vector3(-19f, -0.55f, z), new Vector3(0.28f, 0.9f, 0.28f), new Color(0.35f, 0.26f, 0.16f));
                CreateBlock($"Jetty pile R {i}", new Vector3(-17f, -0.55f, z), new Vector3(0.28f, 0.9f, 0.28f), new Color(0.35f, 0.26f, 0.16f));
            }

            PlaceCoastBoat("Coast boat A", new Vector3(-12f, -0.55f, -62f), 12f, new Color(0.85f, 0.88f, 0.9f));
            PlaceCoastBoat("Coast boat B", new Vector3(22f, -0.5f, -68f), -20f, new Color(0.75f, 0.35f, 0.22f));
            PlaceCoastBoat("Coast boat C", new Vector3(55f, -0.45f, -74f), 5f, new Color(0.2f, 0.35f, 0.45f));
            PlaceCoastBoat("Coast boat D", new Vector3(-40f, -0.5f, -70f), -8f, new Color(0.55f, 0.2f, 0.18f));
            PlaceCoastBoat("Coast boat E", new Vector3(8f, -0.48f, -78f), 28f, new Color(0.92f, 0.9f, 0.82f));
            PlaceCoastBoat("Coast boat F", new Vector3(-58f, -0.52f, -66f), -15f, new Color(0.15f, 0.28f, 0.22f));

            // Rock outcrops along the sand so the shoreline is not a flat ribbon.
            var rock = new Color(0.42f, 0.4f, 0.38f);
            CreateBlock("Coast rock A", new Vector3(-28f, -0.25f, -50f), new Vector3(2.8f, 0.9f, 2.2f), rock);
            CreateBlock("Coast rock B", new Vector3(18f, -0.2f, -49f), new Vector3(2.2f, 0.7f, 1.8f), Shade(rock, 0.9f));
            CreateBlock("Coast rock C", new Vector3(42f, -0.3f, -51.5f), new Vector3(3.4f, 1.1f, 2.6f), Shade(rock, 1.1f));
            CreateBlock("Coast rock D", new Vector3(-52f, -0.22f, -48.5f), new Vector3(2.0f, 0.65f, 1.6f), Shade(rock, 0.85f));
            CreateBlock("Coast rock E", new Vector3(68f, -0.28f, -53f), new Vector3(2.6f, 0.85f, 2.0f), rock);
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
                    if (n.Contains("hull") || n.Contains("bow") || n.Contains("roof"))
                        renderer.material.color = n.Contains("roof") ? Shade(hull, 0.85f) : hull;
                }
            }
            else
            {
                root = new GameObject(name).transform;
                ParentBlock(root, $"{name} hull", Vector3.zero, new Vector3(1.4f, 0.55f, 4.2f), hull);
                ParentBlock(root, $"{name} roof", new Vector3(0f, 0.45f, -0.4f), new Vector3(1.1f, 0.7f, 1.6f), Shade(hull, 0.85f));
                ParentBlock(root, $"{name} mast", new Vector3(0f, 1.4f, 0.2f), new Vector3(0.1f, 2.2f, 0.1f), new Color(0.75f, 0.75f, 0.72f));
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

            // Car park bays — two rows facing the terminal.
            for (var i = 0; i < 8; i++)
            {
                var row = i < 4 ? 0 : 1;
                var slot = i % 4;
                var x = 42f + slot * 3.6f;
                var z = 43.2f + row * 4.2f;
                PlaceParkedCar($"Parked car {i}", new Vector3(x, 0f, z), 90f, carColors[i]);
            }

            // Kerbside drop-off on the access road.
            PlaceParkedCar("Drop-off car", new Vector3(23.5f, 0f, 36f), 0f, carColors[2]);
            PlaceParkedCar("Taxi wait", new Vector3(28.5f, 0f, 36.5f), 8f, new Color(0.92f, 0.78f, 0.15f));

            // Landside furniture: luggage trolley cluster + bench near terminal doors.
            PlaceLuggageTrolley("Luggage trolley A", new Vector3(24f, 0f, 31.5f), -15f);
            PlaceLuggageTrolley("Luggage trolley B", new Vector3(25.2f, 0f, 31.5f), 8f);
            PlaceLuggageTrolley("Luggage trolley C", new Vector3(24.6f, 0f, 30.6f), 175f);
            PlaceLandsideBench("Landside bench", new Vector3(29.5f, 0f, 31.2f), 0f);
            PlaceLandsideBench("Car park bench", new Vector3(40.5f, 0f, 40.2f), 90f);

            // Extra trees framing the car park.
            PlaceTree(new Vector3(58f, 0f, 48f), 1.1f);
            PlaceTree(new Vector3(44f, 0f, 54f), 0.95f);
            PlaceTree(new Vector3(20f, 0f, 44f), 0.85f);
            PlaceTree(new Vector3(56f, 0f, 40f), 0.9f);
            PlaceTree(new Vector3(34f, 0f, 52f), 1.05f);
            PlaceTree(new Vector3(52f, 0f, 56f), 0.88f);
            PlaceTree(new Vector3(38f, 0f, 56f), 1.0f);

            // Overflow bay row + roadside van so landside reads busier (0025 item 3).
            PlaceParkedCar("Overflow car A", new Vector3(42f, 0f, 51.2f), 90f, new Color(0.45f, 0.2f, 0.18f));
            PlaceParkedCar("Overflow car B", new Vector3(45.6f, 0f, 51.2f), 90f, new Color(0.7f, 0.72f, 0.75f));
            PlaceParkedCar("Staff ute", new Vector3(49.2f, 0f, 51.2f), 90f, new Color(0.55f, 0.55f, 0.22f));
            PlaceLuggageTrolley("Luggage trolley D", new Vector3(23.4f, 0f, 30.2f), 40f);
            PlaceLandsideBench("Access bench", new Vector3(22f, 0f, 40.5f), 90f);

            // Small general-aviation tie-down markers west of hangar (life, not sim).
            for (var i = 0; i < 6; i++)
            {
                CreateBlock($"Tie-down {i}", new Vector3(-30f - i * 3.5f, 0.08f, 14f), new Vector3(0.35f, 0.08f, 0.35f),
                    new Color(0.55f, 0.55f, 0.5f));
                CreateBlock($"Tie-down rope {i}", new Vector3(-30f - i * 3.5f, 0.04f, 14.55f),
                    new Vector3(0.06f, 0.04f, 0.9f), new Color(0.35f, 0.35f, 0.32f));
            }
        }

        /// <summary>
        /// Decision 0025 items 1+3 — Resources landside parked car with procedural fallback.
        /// </summary>
        private static void PlaceParkedCar(string name, Vector3 position, float yawDegrees, Color body)
        {
            Transform root;
            if (ArtPresentationLoader.TryInstantiatePrefab("mdl_parked_car_v01", out var prefabRoot))
            {
                prefabRoot.name = name;
                root = prefabRoot;
                TintParkedCarBody(root, body);
            }
            else
            {
                root = new GameObject(name).transform;
                ParentBlock(root, $"{name} body", new Vector3(0f, 0.45f, 0f), new Vector3(1.7f, 0.55f, 3.6f), body);
                ParentBlock(root, $"{name} roof", new Vector3(0f, 0.9f, -0.15f), new Vector3(1.55f, 0.5f, 1.8f), Shade(body, 0.85f));
                ParentBlock(root, $"{name} window", new Vector3(0f, 1.0f, -0.1f), new Vector3(1.45f, 0.28f, 1.5f), new Color(0.2f, 0.35f, 0.45f));
                var wheel = new Color(0.12f, 0.12f, 0.13f);
                ParentBlock(root, $"{name} wheel FL", new Vector3(-0.7f, 0.17f, 1.1f), new Vector3(0.28f, 0.28f, 0.35f), wheel);
                ParentBlock(root, $"{name} wheel FR", new Vector3(0.7f, 0.17f, 1.1f), new Vector3(0.28f, 0.28f, 0.35f), wheel);
                ParentBlock(root, $"{name} wheel RL", new Vector3(-0.7f, 0.17f, -1.1f), new Vector3(0.28f, 0.28f, 0.35f), wheel);
                ParentBlock(root, $"{name} wheel RR", new Vector3(0.7f, 0.17f, -1.1f), new Vector3(0.28f, 0.28f, 0.35f), wheel);
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
                if (n.Contains("wheel") || n.Contains("window") || n.Contains("headlight") || n.Contains("stripe"))
                    continue;
                if (n.Contains("body") || n.Contains("roof") || n.Contains("bumper"))
                {
                    var color = n.Contains("roof") ? Shade(body, 0.85f) : body;
                    renderer.material.color = color;
                }
            }
        }

        /// <summary>
        /// Decision 0025 items 1+3 — Resources luggage trolley with procedural fallback.
        /// </summary>
        private static void PlaceLuggageTrolley(string name, Vector3 position, float yawDegrees)
        {
            Transform root;
            if (ArtPresentationLoader.TryInstantiatePrefab("mdl_luggage_trolley_v01", out var prefabRoot))
            {
                prefabRoot.name = name;
                root = prefabRoot;
            }
            else
            {
                root = new GameObject(name).transform;
                ParentBlock(root, $"{name} basket", new Vector3(0f, 0.45f, 0f), new Vector3(0.8f, 0.55f, 0.45f), new Color(0.7f, 0.72f, 0.75f));
                ParentBlock(root, $"{name} handle", new Vector3(0f, 0.85f, -0.35f), new Vector3(0.7f, 0.08f, 0.08f), new Color(0.7f, 0.72f, 0.75f));
            }

            root.position = position;
            root.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
        }

        /// <summary>
        /// Decision 0025 items 1+3 — Resources landside bench with procedural fallback.
        /// </summary>
        private static void PlaceLandsideBench(string name, Vector3 position, float yawDegrees)
        {
            Transform root;
            if (ArtPresentationLoader.TryInstantiatePrefab("mdl_landside_bench_v01", out var prefabRoot))
            {
                prefabRoot.name = name;
                root = prefabRoot;
            }
            else
            {
                root = new GameObject(name).transform;
                var wood = new Color(0.45f, 0.32f, 0.18f);
                ParentBlock(root, $"{name} seat", new Vector3(0f, 0.35f, 0f), new Vector3(2.2f, 0.12f, 0.55f), wood);
                ParentBlock(root, $"{name} back", new Vector3(0f, 0.7f, -0.22f), new Vector3(2.2f, 0.55f, 0.1f), wood);
            }

            root.position = position;
            root.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
        }

        private static void BuildPerimeterFence()
        {
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
            CreateBlock("Gate stop L", new Vector3(23.1f, 0.08f, 34.4f), new Vector3(0.35f, 0.12f, 0.35f), AirsideTheme.Concrete);
            CreateBlock("Gate stop R", new Vector3(28.9f, 0.08f, 34.4f), new Vector3(0.35f, 0.12f, 0.35f), AirsideTheme.Concrete);
            CreateBlock("Gate sign", new Vector3(26f, 2.0f, 34.2f), new Vector3(1.6f, 0.55f, 0.06f), AirsideTheme.SafetyYellow);

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
        }

        /// <summary>
        /// Decision 0025 items 3+5 — short approach light bars west of the threshold
        /// so night approaches read as a lit path, not a bare runway end.
        /// </summary>
        private static void BuildApproachLightBars()
        {
            var bar = new Color(0.85f, 0.88f, 0.9f);
            var stem = new Color(0.35f, 0.36f, 0.38f);
            // Simple ALS centreline + bar pairs west of runway 09 threshold (~x=-36).
            for (var i = 0; i < 8; i++)
            {
                var x = -40f - i * 5f;
                CreateBlock($"ALS stem {i}", new Vector3(x, 0.35f, 0f), new Vector3(0.12f, 0.7f, 0.12f), stem);
                CreateBlock($"ALS centre {i}", new Vector3(x, 0.75f, 0f), new Vector3(0.35f, 0.18f, 0.35f), bar);
                CreateBlock($"ALS bar L {i}", new Vector3(x, 0.7f, -1.4f - i * 0.12f), new Vector3(0.25f, 0.14f, 2.2f + i * 0.18f), bar);
                CreateBlock($"ALS bar R {i}", new Vector3(x, 0.7f, 1.4f + i * 0.12f), new Vector3(0.25f, 0.14f, 2.2f + i * 0.18f), bar);
                // Crossbar densify every other station.
                if (i % 2 == 0)
                    CreateBlock($"ALS cross {i}", new Vector3(x, 0.68f, 0f), new Vector3(0.18f, 0.12f, 3.6f + i * 0.15f), bar);

                var lampGo = new GameObject($"ALS lamp {i}");
                lampGo.transform.position = new Vector3(x, 0.95f, 0f);
                var light = lampGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.95f, 0.85f);
                light.range = 8f;
                light.intensity = 0f;
                light.shadows = LightShadows.None;
            }

            // Far REIL pair markers beyond the ALS fan.
            CreateBlock("ALS REIL L", new Vector3(-78f, 0.8f, -2.8f), new Vector3(0.4f, 0.4f, 0.4f), new Color(1f, 1f, 0.9f));
            CreateBlock("ALS REIL R", new Vector3(-78f, 0.8f, 2.8f), new Vector3(0.4f, 0.4f, 0.4f), new Color(1f, 1f, 0.9f));
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
            }

            // Soft bay spill at dusk.
            var bay = new GameObject("ARFF bay light");
            bay.transform.position = new Vector3(-28f, 2.4f, 27.8f);
            var light = bay.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.85f, 0.55f);
            light.range = 10f;
            light.intensity = 0f;
            light.shadows = LightShadows.None;

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
            CreateBlock("Terminal bench", new Vector3(21f, 0.35f, 31.6f), new Vector3(2.4f, 0.35f, 0.55f), new Color(0.4f, 0.32f, 0.22f));
            CreateBlock("Terminal planter", new Vector3(31.5f, 0.35f, 31.8f), new Vector3(1.4f, 0.5f, 1.0f), AirsideTheme.Concrete);
            CreateBlock("Terminal planter scrub", new Vector3(31.5f, 0.85f, 31.8f), new Vector3(1.1f, 0.55f, 0.7f), Shade(AirsideTheme.Eucalyptus, 0.85f));
            CreateBlock("Terminal canopy glow", new Vector3(26f, 3.35f, 31.2f), new Vector3(12f, 0.06f, 3.2f), new Color(1f, 0.85f, 0.55f));
        }

        private static void BuildVegetation()
        {
            // Stylised eucalyptus clumps — denser belts so overview reads as KI bush, not
            // a handful of props (0025 item 3). Presentation only.
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
                (new Vector3(30f, 0f, 58f), 1.08f)
            };
            for (var i = 0; i < trees.Length; i++)
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

            // Dense coastal scrub belt between berms and sand.
            for (var x = -55; x <= 55; x += 5)
            {
                var zJitter = ((x * 13) % 7) * 0.15f;
                CreateBlock($"Coast scrub {x}", new Vector3(x, 0.28f, -39.5f + zJitter),
                    new Vector3(2.4f + (x % 3) * 0.4f, 0.45f + (Mathf.Abs(x) % 5) * 0.05f, 1.5f),
                    Shade(AirsideTheme.Eucalyptus, 0.72f + (x % 4) * 0.04f));
                if (x % 10 == 0)
                    PlaceShrub(new Vector3(x + 1.5f, 0f, -37.5f), 0.65f);
            }
        }

        private static void PlaceShrub(Vector3 basePosition, float scale)
        {
            var bush = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bush.name = "Shrub";
            Object.Destroy(bush.GetComponent<Collider>());
            bush.transform.position = basePosition + new Vector3(0f, 0.45f * scale, 0f);
            bush.transform.localScale = new Vector3(1.4f * scale, 0.85f * scale, 1.2f * scale);
            bush.GetComponent<Renderer>().material = AirsideMaterialLibrary.Create(
                Shade(AirsideTheme.DryGrass, 0.85f), AirsideMaterialLibrary.SurfaceKind.Grass);
        }

        private static void PlaceTree(Vector3 basePosition, float scale)
        {
            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Tree trunk";
            Object.Destroy(trunk.GetComponent<Collider>());
            trunk.transform.position = basePosition + new Vector3(0f, 1.1f * scale, 0f);
            trunk.transform.localScale = new Vector3(0.28f * scale, 1.1f * scale, 0.28f * scale);
            trunk.GetComponent<Renderer>().material = CreateMaterial(new Color(0.35f, 0.26f, 0.16f));

            var canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            canopy.name = "Tree canopy";
            Object.Destroy(canopy.GetComponent<Collider>());
            canopy.transform.position = basePosition + new Vector3(0f, 2.6f * scale, 0f);
            canopy.transform.localScale = new Vector3(2.2f * scale, 1.8f * scale, 2.2f * scale);
            canopy.GetComponent<Renderer>().material = AirsideMaterialLibrary.Create(
                Shade(AirsideTheme.Eucalyptus, 0.9f), AirsideMaterialLibrary.SurfaceKind.Grass);

            // Secondary canopy blob so clumps read denser from overview.
            var canopyB = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            canopyB.name = "Tree canopy B";
            Object.Destroy(canopyB.GetComponent<Collider>());
            canopyB.transform.position = basePosition + new Vector3(0.55f * scale, 2.2f * scale, -0.4f * scale);
            canopyB.transform.localScale = new Vector3(1.5f * scale, 1.2f * scale, 1.5f * scale);
            canopyB.GetComponent<Renderer>().material = AirsideMaterialLibrary.Create(
                Shade(AirsideTheme.Eucalyptus, 0.78f), AirsideMaterialLibrary.SurfaceKind.Grass);
        }

        private static void BuildDistantHills()
        {
            // Textured + segmented so the horizon is not four flat unlit slabs (0025 item 3).
            CreateBlock("Hill far NW", new Vector3(-90f, 2f, 70f), new Vector3(50f, 8f, 28f), Shade(AirsideTheme.Eucalyptus, 0.4f),
                "Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png", new Vector2(6f, 3f));
            CreateBlock("Hill far NW ridge", new Vector3(-78f, 5.2f, 72f), new Vector3(22f, 3.5f, 12f), Shade(AirsideTheme.Eucalyptus, 0.48f),
                "Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png", new Vector2(3f, 1.5f));
            CreateBlock("Hill far NE", new Vector3(95f, 1.5f, 65f), new Vector3(44f, 6f, 24f), Shade(AirsideTheme.DryGrass, 0.55f),
                "Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png", new Vector2(5f, 2.5f));
            CreateBlock("Hill far NE spur", new Vector3(108f, 3.2f, 58f), new Vector3(18f, 3.2f, 14f), Shade(AirsideTheme.DryGrass, 0.62f),
                "Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png", new Vector2(2.5f, 1.5f));
            CreateBlock("Hill far W", new Vector3(-100f, 1.2f, 10f), new Vector3(30f, 5f, 40f), Shade(AirsideTheme.Eucalyptus, 0.35f),
                "Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png", new Vector2(4f, 5f));
            CreateBlock("Hill far W shoulder", new Vector3(-88f, 2.8f, -8f), new Vector3(16f, 3.5f, 18f), Shade(AirsideTheme.Eucalyptus, 0.42f),
                "Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png", new Vector2(2f, 2.2f));
            CreateBlock("Hill far E", new Vector3(105f, 1.0f, 5f), new Vector3(28f, 4.5f, 36f), Shade(AirsideTheme.DryGrass, 0.5f),
                "Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png", new Vector2(3.5f, 4.5f));
            CreateBlock("Hill far E shoulder", new Vector3(92f, 2.4f, -12f), new Vector3(14f, 3.0f, 16f), Shade(AirsideTheme.DryGrass, 0.58f),
                "Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png", new Vector2(2f, 2f));
            CreateBlock("Hill far S", new Vector3(0f, 0.8f, -95f), new Vector3(70f, 3.5f, 18f), Shade(AirsideTheme.Sand, 0.75f),
                "Textures/Surfaces/tx_sand_coast_basecolor_v01.png", new Vector2(8f, 2f));
            // Extra coastal headlands so the southern horizon is not a single slab.
            CreateBlock("Hill far SW headland", new Vector3(-55f, 1.4f, -88f), new Vector3(28f, 4.2f, 14f),
                Shade(AirsideTheme.Sand, 0.68f),
                "Textures/Surfaces/tx_sand_coast_basecolor_v01.png", new Vector2(4f, 2f));
            CreateBlock("Hill far SE headland", new Vector3(58f, 1.2f, -90f), new Vector3(26f, 3.8f, 12f),
                Shade(AirsideTheme.Sand, 0.72f),
                "Textures/Surfaces/tx_sand_coast_basecolor_v01.png", new Vector2(3.5f, 1.8f));
            CreateBlock("Hill far N spur", new Vector3(12f, 3.8f, 78f), new Vector3(24f, 4.5f, 16f),
                Shade(AirsideTheme.Eucalyptus, 0.5f),
                "Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png", new Vector2(3f, 2f));
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
            var twinkle = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 1.7f);
            for (var i = 0; i < _starFieldRoot.childCount; i++)
            {
                var star = _starFieldRoot.GetChild(i);
                var renderer = star.GetComponent<Renderer>();
                if (renderer == null || !renderer.material.HasProperty("_EmissionColor"))
                    continue;
                var phase = 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * (1.2f + i * 0.11f) + i);
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
                        renderer.material.color = sunColor;
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
                        renderer.material.color = c;
                        if (renderer.material.HasProperty("_EmissionColor"))
                            renderer.material.SetColor("_EmissionColor", c * 0.7f);
                    }
                }
            }
        }

        private static void BuildCloudBands()
        {
            // Soft translucent cloud blobs so the sky is not empty — presentation only.
            var cloudRoot = new GameObject("Cloud bands").transform;
            var umbraRoot = new GameObject("Cloud umbras").transform;
            var rng = new System.Random(90210);
            for (var i = 0; i < 22; i++)
            {
                var cloud = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                cloud.name = $"Cloud {i}";
                Object.Destroy(cloud.GetComponent<Collider>());
                var x = (float)(rng.NextDouble() * 220f - 110f);
                var z = (float)(rng.NextDouble() * 200f - 100f);
                var y = 24f + (float)rng.NextDouble() * 26f;
                cloud.transform.SetParent(cloudRoot, false);
                cloud.transform.position = new Vector3(x, y, z);
                var sx = 14f + (float)rng.NextDouble() * 28f;
                var sy = 3.2f + (float)rng.NextDouble() * 4.2f;
                var sz = 8f + (float)rng.NextDouble() * 18f;
                cloud.transform.localScale = new Vector3(sx, sy, sz);
                var alpha = 0.14f + (float)rng.NextDouble() * 0.18f;
                cloud.GetComponent<Renderer>().material = AirsideMaterialLibrary.Create(
                    new Color(0.95f, 0.96f, 0.98f, alpha),
                    AirsideMaterialLibrary.SurfaceKind.Glass);
                cloud.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                cloud.GetComponent<Renderer>().receiveShadows = false;

                // Soft ground umbra under each cloud — drifts with UpdateCloudDrift.
                var umbra = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                umbra.name = $"Cloud umbra {i}";
                Object.Destroy(umbra.GetComponent<Collider>());
                umbra.transform.SetParent(umbraRoot, false);
                umbra.transform.position = new Vector3(x, 0.06f, z);
                umbra.transform.localScale = new Vector3(sx * 0.85f, 0.02f, sz * 0.85f);
                var umbraMat = AirsideMaterialLibrary.Create(
                    new Color(0.05f, 0.07f, 0.1f, 0.22f),
                    AirsideMaterialLibrary.SurfaceKind.Glass);
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
            PlaceContactShadow("Terminal contact", new Vector3(26f, 0.04f, 27f), new Vector3(24f, 0.03f, 7f), 0.28f);
            PlaceContactShadow("Hangar contact", new Vector3(-20f, 0.04f, 20f), new Vector3(16f, 0.03f, 11f), 0.3f);
            PlaceContactShadow("Ops contact", new Vector3(-8f, 0.04f, 26f), new Vector3(8f, 0.03f, 5.5f), 0.26f);
            PlaceContactShadow("Car park contact", new Vector3(48f, 0.04f, 46f), new Vector3(18f, 0.02f, 12f), 0.12f);
            PlaceContactShadow("Fuel farm contact", new Vector3(-34f, 0.04f, 22f), new Vector3(9f, 0.02f, 7f), 0.22f);
            PlaceContactShadow("ARFF contact", new Vector3(-28f, 0.04f, 30f), new Vector3(9f, 0.02f, 7f), 0.2f);
            PlaceContactShadow("Canopy contact", new Vector3(26f, 0.04f, 31.5f), new Vector3(16f, 0.02f, 5f), 0.14f);
            PlaceContactShadow("Flood NE contact", new Vector3(32f, 0.04f, 22f), new Vector3(2.2f, 0.02f, 2.2f), 0.18f);
            PlaceContactShadow("Flood NW contact", new Vector3(8f, 0.04f, 22f), new Vector3(2.2f, 0.02f, 2.2f), 0.18f);
            PlaceContactShadow("Flood SE contact", new Vector3(32f, 0.04f, 12f), new Vector3(2.2f, 0.02f, 2.2f), 0.18f);
            PlaceContactShadow("Flood SW contact", new Vector3(8f, 0.04f, 12f), new Vector3(2.2f, 0.02f, 2.2f), 0.18f);
            PlaceContactShadow("Windsock contact", new Vector3(-12f, 0.04f, 12f), new Vector3(1.4f, 0.02f, 1.4f), 0.16f);
            PlaceContactShadow("Dolly cluster contact", new Vector3(31f, 0.04f, 20f), new Vector3(8f, 0.02f, 6f), 0.1f);
        }

        private static void PlaceContactShadow(string name, Vector3 position, Vector3 scale, float alpha)
        {
            var shadow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shadow.name = name;
            Object.Destroy(shadow.GetComponent<Collider>());
            shadow.transform.position = position;
            shadow.transform.localScale = scale;
            var material = AirsideMaterialLibrary.Create(
                new Color(0.04f, 0.05f, 0.07f, alpha),
                AirsideMaterialLibrary.SurfaceKind.Glass);
            shadow.GetComponent<Renderer>().material = material;
            shadow.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shadow.GetComponent<Renderer>().receiveShadows = false;
        }

        private void CollectHangarDoorPanels()
        {
            _hangarDoorPanels.Clear();
            // Authored / kit hangar doors — slide L/R panels instead of a single greybox slab.
            foreach (var name in new[]
                     {
                         "door_panel_l", "door_panel_r", "door_rib_l", "door_rib_r",
                         "door_bar_l1", "door_bar_l2", "door_bar_l3",
                         "door_bar_r1", "door_bar_r2", "door_bar_r3",
                         "door_handle_l", "door_handle_r"
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
            _coastFoam = GameObject.Find("Coast foam")?.transform;
            _jettyDeck = GameObject.Find("Jetty deck")?.transform;
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
                var daySpill = openAmount * 1.35f;
                var nightGlow = (1f - daylight) * 0.55f;
                _hangarBayLight.intensity = Mathf.Max(0.08f, daySpill + nightGlow);
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
                var bob = Mathf.Sin(t * 0.85f + i * 1.4f) * 0.08f;
                var yawSway = Mathf.Sin(t * 0.35f + i) * 2.2f;
                boat.position = basePos + new Vector3(0f, bob, 0f);
                boat.rotation = Quaternion.Euler(
                    Mathf.Sin(t * 0.7f + i) * 2.5f,
                    baseYaw + yawSway,
                    Mathf.Cos(t * 0.55f + i * 0.8f) * 3f);
            }

            if (_coastFoam != null)
            {
                var pulse = 0.92f + 0.08f * Mathf.Sin(t * 1.6f);
                var scale = _coastFoam.localScale;
                scale.z = 2.2f * pulse;
                _coastFoam.localScale = scale;
                var renderer = _coastFoam.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var c = renderer.material.color;
                    c.a = 0.55f + 0.3f * (0.5f + 0.5f * Mathf.Sin(t * 1.4f));
                    renderer.material.color = c;
                }
            }

            if (_jettyDeck != null)
            {
                var pos = _jettyDeck.position;
                pos.y = -0.15f + Mathf.Sin(t * 0.9f) * 0.02f;
                _jettyDeck.position = pos;
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
            var overcast = _simulation.CurrentWeather is WeatherKind.Overcast or WeatherKind.Rain or WeatherKind.Storm or WeatherKind.Fog;
            var umbraAlpha = overcast
                ? Mathf.Lerp(0.06f, 0.18f, daylight)
                : Mathf.Lerp(0.04f, 0.26f, daylight);
            for (var i = 0; i < _cloudRoot.childCount; i++)
            {
                var cloud = _cloudRoot.GetChild(i);
                var p = cloud.position;
                p.x += drift;
                if (p.x > 100f)
                    p.x = -100f;
                cloud.position = p;

                var renderer = cloud.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var color = renderer.material.color;
                    var dusk = Mathf.Clamp01(Mathf.Min(daylight, 1f - daylight) * 3f);
                    var tint = Color.Lerp(new Color(0.55f, 0.6f, 0.75f), new Color(0.95f, 0.96f, 0.98f), daylight);
                    tint = Color.Lerp(tint, new Color(0.95f, 0.7f, 0.55f), dusk * 0.55f);
                    tint.a = color.a;
                    renderer.material.color = tint;
                }

                if (_cloudUmbraRoot == null || i >= _cloudUmbraRoot.childCount)
                    continue;
                var umbra = _cloudUmbraRoot.GetChild(i);
                umbra.position = new Vector3(p.x, 0.06f, p.z);
                umbra.rotation = Quaternion.identity;
                var scale = cloud.localScale;
                umbra.localScale = new Vector3(scale.x * 0.85f, 0.02f, scale.z * 0.85f);
                var umbraRenderer = umbra.GetComponent<Renderer>();
                if (umbraRenderer == null)
                    continue;
                var umbraColor = umbraRenderer.material.color;
                umbraColor.a = umbraAlpha;
                umbraRenderer.material.color = umbraColor;
            }
        }

        private static void BuildBirdFlock()
        {
            // Stylised coastal flock — body + hinged wing quads so flaps read from overview
            // (0025 item 7). Presentation only.
            var root = new GameObject("Bird flock").transform;
            var rng = new System.Random(4242);
            for (var i = 0; i < 18; i++)
            {
                var bird = new GameObject($"Bird {i}").transform;
                bird.SetParent(root, false);
                // Seed orbit phase in unused euler z for UpdateBirdFlock.
                bird.localEulerAngles = new Vector3(0f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f);

                var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                body.name = "Body";
                Object.Destroy(body.GetComponent<Collider>());
                body.transform.SetParent(bird, false);
                body.transform.localScale = new Vector3(0.18f, 0.08f, 0.5f);
                body.GetComponent<Renderer>().material = AirsideMaterialLibrary.Create(
                    new Color(0.12f, 0.12f, 0.14f),
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
            wing.transform.localScale = new Vector3(0.42f, 0.03f, 0.18f);
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
            var t = Time.unscaledTime * 0.22f;
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
                var flap = Mathf.Sin(Time.unscaledTime * 10f + i * 0.7f) * 38f;
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
                NestCrossPropellerBlades(root);

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
            ParentBlock(root, "NavLight L", new Vector3(-3.7f, 0.08f, 0.2f), new Vector3(0.12f, 0.12f, 0.12f), new Color(0.1f, 0.9f, 0.2f));
            ParentBlock(root, "NavLight R", new Vector3(3.7f, 0.08f, 0.2f), new Vector3(0.12f, 0.12f, 0.12f), new Color(0.9f, 0.12f, 0.12f));
            ParentBlock(root, "Beacon", new Vector3(0f, 0.85f, 0.2f), new Vector3(0.14f, 0.14f, 0.14f), new Color(0.95f, 0.2f, 0.15f));
            ParentBlock(root, "LandingLight", new Vector3(0f, -0.15f, 2.5f), new Vector3(0.18f, 0.12f, 0.2f), new Color(0.95f, 0.95f, 0.85f));
            ParentBlock(root, "TaxiLight", new Vector3(0f, -0.2f, 2.2f), new Vector3(0.14f, 0.1f, 0.16f), new Color(0.95f, 0.92f, 0.7f));
            ParentBlock(root, "EngineHeat L", new Vector3(-1.35f, -0.05f, 0.15f), new Vector3(0.35f, 0.35f, 0.7f), new Color(0.95f, 0.55f, 0.2f, 0.15f));
            ParentBlock(root, "EngineHeat R", new Vector3(1.35f, -0.05f, 0.15f), new Vector3(0.35f, 0.35f, 0.7f), new Color(0.95f, 0.55f, 0.2f, 0.15f));

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
            "cabin_window_r1" => "Cabin window R1",
            "cabin_window_r2" => "Cabin window R2",
            "cabin_window_r3" => "Cabin window R3",
            "cabin_window_r4" => "Cabin window R4",
            "cabin_window_r5" => "Cabin window R5",
            "cabin_window_r6" => "Cabin window R6",
            "cockpit_glare" => "Cockpit glare",
            "windscreen_pillar_l" => "Windscreen pillar L",
            "windscreen_pillar_r" => "Windscreen pillar R",
            "livery_stripe" => "Livery stripe",
            "livery_stripe_lower" => "Livery stripe lower",
            "door_frame_fwd" => "Door frame",
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
            "propeller_left" => "Propeller L",
            "propeller_right" => "Propeller R",
            "propeller_left_b" => "PropBlade L",
            "propeller_right_b" => "PropBlade R",
            "spinner_left" => "Spinner L",
            "spinner_right" => "Spinner R",
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
            "gear_scissors_nose" => "Gear scissors nose",
            "gear_scissors_left" => "Gear scissors L",
            "gear_scissors_right" => "Gear scissors R",
            "gear_door_nose" => "Gear door nose",
            "gear_door_left" => "Gear door L",
            "gear_door_right" => "Gear door R",
            "tire_nose" => "Tire nose",
            "tire_left" => "Tire L",
            "tire_right" => "Tire R",
            "door_fwd" => "CabinDoor",
            "cargo_door" => "Cargo door",
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
                or "cabin_window_6"
                or "cabin_window_r1" or "cabin_window_r2" or "cabin_window_r3" or "cabin_window_r4" or "cabin_window_r5"
                or "cabin_window_r6" or "cockpit_glare"
                => new Color(0.18f, 0.35f, 0.48f),
            "cockpit_frame" or "windscreen_pillar_l" or "windscreen_pillar_r" => new Color(0.75f, 0.78f, 0.82f),
            "livery_stripe" or "livery_stripe_lower" => new Color(0.15f, 0.35f, 0.65f),
            "wing_left" or "wing_right" or "wing_root_left" or "wing_root_right"
                or "wing_fairing_left" or "wing_fairing_right"
                or "wingtip_left" or "wingtip_right" or "winglet_left" or "winglet_right"
                or "wing_fence_left" or "wing_fence_right" or "wing_fence_mid_l" or "wing_fence_mid_r"
                or "flap_left" or "flap_right" or "spoiler_left" or "spoiler_right"
                or "aileron_left" or "aileron_right"
                or "tail_fin" or "tail_fin_tip" or "tailplane" or "dorsal_fin"
                or "tailplane_tip_l" or "tailplane_tip_r"
                or "elevator_left" or "elevator_right" or "rudder" => accent,
            "engine_left" or "engine_right" or "pylon_left" or "pylon_right"
                or "nacelle_left" or "nacelle_right"
                or "intake_left" or "intake_right" or "exhaust_left" or "exhaust_right"
                or "exhaust_stack_l" or "exhaust_stack_r" => accent * 0.85f,
            "propeller_left" or "propeller_right" or "propeller_left_b" or "propeller_right_b"
                or "spinner_left" or "spinner_right" or "prop_hub_left" or "prop_hub_right"
                or "hub_cap_left" or "hub_cap_right"
                => new Color(0.2f, 0.2f, 0.22f),
            "gear_nose" or "gear_left" or "gear_right"
                or "gear_scissors_nose" or "gear_scissors_left" or "gear_scissors_right"
                or "gear_door_nose" or "gear_door_left" or "gear_door_right" => new Color(0.25f, 0.25f, 0.28f),
            "tire_nose" or "tire_left" or "tire_right" => new Color(0.12f, 0.12f, 0.13f),
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
        /// Parent the second blade under each propeller so SpinPropellers rotates the
        /// whole cross as one unit (v02 kits only).
        /// </summary>
        private static void NestCrossPropellerBlades(Transform aircraft)
        {
            Transform propL = null, propR = null, bladeL = null, bladeR = null;
            foreach (var child in aircraft.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "Propeller L") propL = child;
                else if (child.name == "Propeller R") propR = child;
                else if (child.name == "PropBlade L") bladeL = child;
                else if (child.name == "PropBlade R") bladeR = child;
            }

            if (propL != null && bladeL != null)
            {
                bladeL.SetParent(propL, true);
                bladeL.name = "Blade";
            }

            if (propR != null && bladeR != null)
            {
                bladeR.SetParent(propR, true);
                bladeR.name = "Blade";
            }
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

                var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                disc.name = "PropDisc";
                Object.Destroy(disc.GetComponent<Collider>());
                disc.transform.SetParent(child, false);
                disc.transform.localPosition = Vector3.zero;
                // Cylinder axis → local Z so the face is perpendicular to the spin axis.
                disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                disc.transform.localScale = new Vector3(1.2f, 0.012f, 1.2f);
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
            shadow.transform.localScale = new Vector3(3.4f, 0.02f, 1.9f);
            var material = AirsideMaterialLibrary.Create(new Color(0.05f, 0.06f, 0.08f, 0.35f),
                AirsideMaterialLibrary.SurfaceKind.Glass);
            shadow.GetComponent<Renderer>().material = material;
            shadow.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shadow.GetComponent<Renderer>().receiveShadows = false;
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
            var width = Mathf.Lerp(3.4f, 8f, t);
            var depth = width * 0.55f;
            var sx = aircraft.lossyScale.x > 0.001f ? width / aircraft.lossyScale.x : width;
            var sy = aircraft.lossyScale.y > 0.001f ? 0.04f / aircraft.lossyScale.y : 0.04f;
            var sz = aircraft.lossyScale.z > 0.001f ? depth / aircraft.lossyScale.z : depth;
            shadow.localScale = new Vector3(sx, sy, sz);

            var renderer = shadow.GetComponent<Renderer>();
            if (renderer == null)
                return;
            var color = renderer.material.color;
            color.a = Mathf.Lerp(0.38f, 0.06f, t);
            renderer.material.color = color;
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
                    "hose_mount" => "Hose",
                    "door" => "Door",
                    var n when n.StartsWith("cart_", StringComparison.Ordinal) => "Cargo",
                    _ => $"{name} {kitName}"
                },
                kitName => kitName switch
                {
                    "wheel_fl" or "wheel_fr" or "wheel_rl" or "wheel_rr"
                        or "cart_wheel_1l" or "cart_wheel_1r" or "cart_wheel_2l" or "cart_wheel_2r"
                        or "cart_wheel_3l" or "cart_wheel_3r" => new Color(0.15f, 0.15f, 0.16f),
                    "hose_mount" or "hose" or "hose_reel" or "hose_nozzle" or "hose_guard"
                        or "exhaust" => new Color(0.25f, 0.25f, 0.28f),
                    "door" or "cab_door" or "cab_door_r" or "door_frame" or "door_glass"
                        => new Color(0.2f, 0.22f, 0.25f),
                    "cab" or "tug_cab" or "cab_roof" or "tug_seat" or "tug_rollbar" => color * 0.82f,
                    "cab_window" or "windows" or "tug_window" or "window_mullion"
                        or "window_mullion_2" or "window_mullion_3" or "window_mullion_4"
                        or "window_mullion_5" or "window_sill" => new Color(0.2f, 0.4f, 0.55f),
                    "beacon" or "headlight_l" or "headlight_r" => new Color(0.95f, 0.35f, 0.12f),
                    "taillight_l" or "taillight_r" => new Color(0.85f, 0.15f, 0.12f),
                    "mirror_l" or "mirror_r" or "bumper" or "bumper_front" or "bumper_rear"
                        or "tug_bumper" or "tank_band" or "tank_band_2" or "tank_band_3"
                        or "grill" or "light_bar" or "fender_fl" or "fender_fr"
                        or "wheel_arch_fl" or "wheel_arch_fr" or "wheel_arch_rl" or "wheel_arch_rr"
                        or "chassis" or "step" or "step_r" or "roof_rack"
                        => color * 0.7f,
                    "cargo_1" or "cargo_2" or "cargo_3" => new Color(0.75f, 0.55f, 0.2f),
                    "stripe" or "stripe_b" => new Color(0.95f, 0.85f, 0.2f),
                    "cart_rail_1" or "cart_rail_2" or "cart_rail_3"
                        or "cart_rail_1b" or "cart_rail_2b" or "cart_rail_3b"
                        or "hitch_1" or "hitch_2" or "hitch_3" => color * 0.6f,
                    _ => color
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

            root.gameObject.SetActive(false);
            return root;
        }


        private static Transform BuildStairs()
        {
            // Prefer the Resources / Addressables prefab (0025 item 1), then service-kit
            // glTF mesh, then procedural cuboids.
            if (ArtPresentationLoader.TryInstantiatePrefab("mdl_passenger_stairs_v01", out var prefabRoot))
            {
                prefabRoot.name = "Passenger stairs";
                prefabRoot.gameObject.SetActive(false);
                return prefabRoot;
            }

            var root = new GameObject("Passenger stairs").transform;
            var kit = PreferArtKit(
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
            PlacePart("stairs_rail_l", new Color(0.85f, 0.55f, 0.15f));
            PlacePart("stairs_rail_r", new Color(0.85f, 0.55f, 0.15f));
            PlacePart("stairs_rail_mid", new Color(0.85f, 0.55f, 0.15f));
            PlacePart("stairs_tread_1", new Color(0.62f, 0.63f, 0.65f));
            PlacePart("stairs_tread_2", new Color(0.62f, 0.63f, 0.65f));
            PlacePart("stairs_tread_3", new Color(0.62f, 0.63f, 0.65f));
            PlacePart("stairs_tread_4", new Color(0.62f, 0.63f, 0.65f));
            PlacePart("stairs_tread_5", new Color(0.62f, 0.63f, 0.65f));
            PlacePart("stairs_platform", new Color(0.7f, 0.72f, 0.74f));
            PlacePart("stairs_handle", new Color(0.75f, 0.5f, 0.15f));
            PlacePart("stairs_brace", new Color(0.5f, 0.5f, 0.52f));
            PlacePart("stairs_wheel_l", new Color(0.15f, 0.15f, 0.16f));
            PlacePart("stairs_wheel_r", new Color(0.15f, 0.15f, 0.16f));
            PlacePart("stairs_wheel_rl", new Color(0.15f, 0.15f, 0.16f));
            PlacePart("stairs_wheel_rr", new Color(0.15f, 0.15f, 0.16f));
            if (!placed && ArtGltfLoader.TryPlaceNamedMesh(kit, "stairs", Vector3.zero, Quaternion.identity,
                    new Color(0.7f, 0.72f, 0.74f), out var stairs))
            {
                stairs.SetParent(root, false);
                stairs.localPosition = new Vector3(0f, -0.55f, 0f);
                placed = true;
            }

            if (!placed)
            {
                ParentBlock(root, "Stairs base", Vector3.zero, new Vector3(1.1f, 0.2f, 2.4f), new Color(0.7f, 0.72f, 0.74f));
                ParentBlock(root, "Stairs rail L", new Vector3(-0.45f, 0.55f, 0f), new Vector3(0.08f, 1.0f, 2.2f), new Color(0.85f, 0.55f, 0.15f));
                ParentBlock(root, "Stairs rail R", new Vector3(0.45f, 0.55f, 0f), new Vector3(0.08f, 1.0f, 2.2f), new Color(0.85f, 0.55f, 0.15f));
                for (var i = 0; i < 5; i++)
                    ParentBlock(root, $"Step {i}", new Vector3(0f, 0.15f + i * 0.18f, -0.9f + i * 0.35f),
                        new Vector3(0.95f, 0.08f, 0.32f), new Color(0.55f, 0.56f, 0.58f));
            }

            root.gameObject.SetActive(false);
            return root;
        }

        private static Transform BuildChocks()
        {
            if (ArtPresentationLoader.TryInstantiatePrefab("mdl_wheel_chocks_v01", out var prefabRoot))
            {
                prefabRoot.name = "Wheel chocks";
                prefabRoot.gameObject.SetActive(false);
                return prefabRoot;
            }

            var root = new GameObject("Wheel chocks").transform;
            var kit = PreferArtKit(
                "Models/Props/mdl_service_equipment_kit_authored_v01.gltf",
                "Models/Props/mdl_service_equipment_kit_v02.gltf",
                "Models/Props/mdl_service_equipment_kit_v01.gltf");
            var placed = ArtGltfLoader.TryPlaceNamedMesh(kit, "chock_a", new Vector3(-0.55f, 0f, 0f), Quaternion.identity,
                new Color(0.85f, 0.2f, 0.15f), out var a);
            placed = ArtGltfLoader.TryPlaceNamedMesh(kit, "chock_b", new Vector3(0.55f, 0f, 0f), Quaternion.identity,
                new Color(0.85f, 0.2f, 0.15f), out var b) || placed;
            if (placed)
            {
                if (a != null) { a.SetParent(root, false); a.localPosition = new Vector3(-0.55f, -0.55f, 0f); }
                if (b != null) { b.SetParent(root, false); b.localPosition = new Vector3(0.55f, -0.55f, 0f); }
            }
            else
            {
                ParentBlock(root, "Chock L", new Vector3(-0.55f, 0f, 0f), new Vector3(0.35f, 0.22f, 0.45f), new Color(0.85f, 0.2f, 0.15f));
                ParentBlock(root, "Chock R", new Vector3(0.55f, 0f, 0f), new Vector3(0.35f, 0.22f, 0.45f), new Color(0.85f, 0.2f, 0.15f));
            }

            root.gameObject.SetActive(false);
            return root;
        }

        private static Transform BuildGpuCart()
        {
            if (ArtPresentationLoader.TryInstantiatePrefab("mdl_gpu_cart_v01", out var prefabRoot))
            {
                prefabRoot.name = "GPU cart";
                prefabRoot.gameObject.SetActive(false);
                return prefabRoot;
            }

            var root = new GameObject("GPU cart").transform;
            var kit = PreferArtKit(
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

            PlaceGpu("gpu_body", new Color(0.25f, 0.55f, 0.35f));
            PlaceGpu("gpu_cab", new Color(0.22f, 0.48f, 0.32f));
            PlaceGpu("gpu_vent", new Color(0.35f, 0.38f, 0.36f));
            PlaceGpu("gpu_panel", new Color(0.2f, 0.22f, 0.24f));
            PlaceGpu("gpu_grille", new Color(0.18f, 0.2f, 0.2f));
            PlaceGpu("gpu_cable", new Color(0.2f, 0.2f, 0.22f));
            PlaceGpu("gpu_hitch", new Color(0.3f, 0.3f, 0.32f));
            PlaceGpu("gpu_beacon", new Color(0.95f, 0.35f, 0.12f));
            PlaceGpu("gpu_wheel_fl", new Color(0.15f, 0.15f, 0.16f));
            PlaceGpu("gpu_wheel_fr", new Color(0.15f, 0.15f, 0.16f));
            PlaceGpu("gpu_wheel_rl", new Color(0.15f, 0.15f, 0.16f));
            PlaceGpu("gpu_wheel_rr", new Color(0.15f, 0.15f, 0.16f));
            if (!placed && ArtGltfLoader.TryPlaceNamedMesh(kit, "gpu", Vector3.zero, Quaternion.identity,
                    new Color(0.25f, 0.55f, 0.35f), out var gpu))
            {
                gpu.SetParent(root, false);
                gpu.localPosition = new Vector3(0f, -0.55f, 0f);
                placed = true;
            }

            if (!placed)
            {
                ParentBlock(root, "GPU body", Vector3.zero, new Vector3(1.4f, 0.7f, 0.9f), new Color(0.25f, 0.55f, 0.35f));
                ParentBlock(root, "GPU cable", new Vector3(0.85f, 0.1f, 0f), new Vector3(0.7f, 0.08f, 0.08f), new Color(0.2f, 0.2f, 0.22f));
                ParentBlock(root, "GPU wheel L", new Vector3(0.4f, -0.28f, 0.35f), new Vector3(0.22f, 0.22f, 0.14f), new Color(0.15f, 0.15f, 0.16f));
                ParentBlock(root, "GPU wheel R", new Vector3(0.4f, -0.28f, -0.35f), new Vector3(0.22f, 0.22f, 0.14f), new Color(0.15f, 0.15f, 0.16f));
            }

            root.gameObject.SetActive(false);
            return root;
        }

        private static Transform BuildPushbackTug()
        {
            if (ArtPresentationLoader.TryInstantiatePrefab("mdl_pushback_tug_v01", out var prefabRoot))
            {
                prefabRoot.name = "Pushback tug";
                prefabRoot.gameObject.SetActive(false);
                return prefabRoot;
            }

            var root = new GameObject("Pushback tug").transform;
            var kit = PreferArtKit(
                "Models/Props/mdl_service_equipment_kit_authored_v01.gltf",
                "Models/Props/mdl_service_equipment_kit_v02.gltf",
                "Models/Props/mdl_service_equipment_kit_v01.gltf");
            if (ArtGltfLoader.TryPlaceNamedMesh(kit, "towbar", Vector3.zero, Quaternion.identity,
                    new Color(0.82f, 0.62f, 0.18f), out var towbar))
            {
                towbar.SetParent(root, false);
                towbar.localPosition = new Vector3(0f, -0.55f, 0f);
            }
            else
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
            // WLD-003 windsock — Resources / kit pole when available; animated sock always procedural.
            var propsKit = PreferArtKit(
                "Models/Props/mdl_airfield_props_kit_authored_v01.gltf",
                "Models/Props/mdl_airfield_props_kit_v02.gltf",
                "Models/Props/mdl_airfield_props_kit_v01.gltf");
            if (ArtPresentationLoader.TryInstantiatePrefab("mdl_windsock_pole_v01", out var polePrefab))
            {
                polePrefab.name = "Windsock pole";
                polePrefab.position = new Vector3(-12f, 0f, 12f);
            }
            else if (!ArtGltfLoader.TryPlaceNamedMesh(propsKit, "windsock_pole", new Vector3(-12f, 0f, 12f), Quaternion.identity,
                    new Color(0.75f, 0.75f, 0.72f), out _))
            {
                CreateBlock("Windsock pole", new Vector3(-12f, 1.6f, 12f), new Vector3(0.12f, 3.2f, 0.12f), new Color(0.75f, 0.75f, 0.72f));
                CreateBlock("Windsock hinge", new Vector3(-12f, 3.15f, 12f), new Vector3(0.22f, 0.22f, 0.22f), new Color(0.55f, 0.55f, 0.52f));
            }

            var sock = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            sock.name = "Windsock sock";
            Object.Destroy(sock.GetComponent<Collider>());
            sock.transform.position = new Vector3(-11.2f, 3.05f, 12f);
            sock.transform.localScale = new Vector3(0.55f, 0.55f, 1.35f);
            sock.transform.rotation = Quaternion.Euler(0f, 12f, 90f);
            sock.GetComponent<Renderer>().material = CreateMaterial(new Color(0.92f, 0.55f, 0.12f));
            var stripe = CreateBlock("Windsock stripe", new Vector3(-10.6f, 3.05f, 12f), new Vector3(0.35f, 0.52f, 0.52f),
                new Color(0.95f, 0.95f, 0.92f));
            stripe.transform.SetParent(sock.transform, true);
            return sock.transform;
        }

        private static void CreateCone(Vector3 position)
        {
            if (ArtPresentationLoader.TryInstantiatePrefab("mdl_safety_cone_v01", out var prefabRoot))
            {
                prefabRoot.name = "Safety cone";
                prefabRoot.position = position;
                return;
            }

            var kit = PreferArtKit(
                "Models/Props/mdl_airfield_props_kit_authored_v01.gltf",
                "Models/Props/mdl_airfield_props_kit_v02.gltf",
                "Models/Props/mdl_airfield_props_kit_v01.gltf");
            var origin = position + new Vector3(0f, -0.25f, 0f);
            if (ArtGltfLoader.TryPlaceNamedMesh(kit, "cone_base", origin, Quaternion.identity, new Color(0.2f, 0.2f, 0.22f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "cone_body", origin, Quaternion.identity, new Color(0.95f, 0.45f, 0.08f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "cone_stripe", origin, Quaternion.identity, Color.white, out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "cone_tip", origin, Quaternion.identity, new Color(0.95f, 0.45f, 0.08f), out _))
                return;

            if (ArtGltfLoader.TryPlaceNamedMesh(kit, "cone", origin, Quaternion.identity, new Color(0.95f, 0.45f, 0.08f), out _))
                return;

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
            if (ArtPresentationLoader.TryInstantiatePrefab("mdl_work_barrier_v01", out var prefabRoot))
            {
                prefabRoot.name = "Barrier";
                prefabRoot.position = position;
                prefabRoot.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
                return;
            }

            var kit = PreferArtKit(
                "Models/Props/mdl_airfield_props_kit_authored_v01.gltf",
                "Models/Props/mdl_airfield_props_kit_v02.gltf",
                "Models/Props/mdl_airfield_props_kit_v01.gltf");
            var origin = position + new Vector3(0f, -0.45f, 0f);
            var rot = Quaternion.Euler(0f, yawDegrees, 0f);
            if (ArtGltfLoader.TryPlaceNamedMesh(kit, "barrier_rail", origin, rot, new Color(0.9f, 0.55f, 0.12f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "barrier_rail_low", origin, rot, new Color(0.9f, 0.55f, 0.12f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "barrier_stripe", origin, rot, Color.white, out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "barrier_leg_l", origin, rot, new Color(0.25f, 0.25f, 0.28f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "barrier_leg_r", origin, rot, new Color(0.25f, 0.25f, 0.28f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "barrier_foot_l", origin, rot, new Color(0.3f, 0.3f, 0.32f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "barrier_foot_r", origin, rot, new Color(0.3f, 0.3f, 0.32f), out _))
                return;

            if (ArtGltfLoader.TryPlaceNamedMesh(kit, "barrier", origin, rot, new Color(0.9f, 0.55f, 0.12f), out _))
                return;

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
                        if (child.name != "glass_front")
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
                                or "window_l" or "window_r" or "cabin_windows" or "cockpit")
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

            if (!usedCentre)
            {
                for (var x = -34; x <= 34; x += 8)
                    CreateBlock("Runway marking", new Vector3(x, 0.02f, 0f), new Vector3(3.5f, 0.03f, 0.28f), Color.white);
            }

            // Threshold bars + hold-short remain greybox-scale to match the 7 m runway.
            for (var z = -2.4f; z <= 2.4f; z += 0.8f)
            {
                CreateBlock("Threshold W", new Vector3(-36f, 0.03f, z), new Vector3(2.2f, 0.02f, 0.35f), Color.white);
                CreateBlock("Threshold E", new Vector3(36f, 0.03f, z), new Vector3(2.2f, 0.02f, 0.35f), Color.white);
            }
            CreateBlock("Hold short A", new Vector3(-12f, 0.05f, 6.6f), new Vector3(4.2f, 0.03f, 0.22f), new Color(0.95f, 0.82f, 0.12f));
            CreateBlock("Hold short B", new Vector3(-12f, 0.05f, 7.1f), new Vector3(4.2f, 0.03f, 0.22f), new Color(0.95f, 0.82f, 0.12f));
            // Second hold-short pair nearer the apron lead-in.
            CreateBlock("Hold short C", new Vector3(4f, 0.05f, 6.6f), new Vector3(3.6f, 0.03f, 0.2f), new Color(0.95f, 0.82f, 0.12f));
            CreateBlock("Hold short D", new Vector3(4f, 0.05f, 7.1f), new Vector3(3.6f, 0.03f, 0.2f), new Color(0.95f, 0.82f, 0.12f));
            // Readable block digits for 09 / 27 (facing inbound traffic).
            PlaceRunwayDigit('0', new Vector3(-34.6f, 0.04f, 0f), yaw: 90f);
            PlaceRunwayDigit('9', new Vector3(-32.6f, 0.04f, 0f), yaw: 90f);
            PlaceRunwayDigit('2', new Vector3(32.6f, 0.04f, 0f), yaw: -90f);
            PlaceRunwayDigit('7', new Vector3(34.6f, 0.04f, 0f), yaw: -90f);
            // Side stripes beside threshold bars.
            CreateBlock("Threshold stripe W L", new Vector3(-36f, 0.03f, -3.05f), new Vector3(2.2f, 0.02f, 0.45f), Color.white);
            CreateBlock("Threshold stripe W R", new Vector3(-36f, 0.03f, 3.05f), new Vector3(2.2f, 0.02f, 0.45f), Color.white);
            CreateBlock("Threshold stripe E L", new Vector3(36f, 0.03f, -3.05f), new Vector3(2.2f, 0.02f, 0.45f), Color.white);
            CreateBlock("Threshold stripe E R", new Vector3(36f, 0.03f, 3.05f), new Vector3(2.2f, 0.02f, 0.45f), Color.white);

            // Aiming-point pairs (WLD markings language) — readable from overview/follow.
            foreach (var x in new[] { -18f, 18f })
            {
                CreateBlock($"Aiming point {x} L", new Vector3(x, 0.035f, -1.55f), new Vector3(2.8f, 0.025f, 1.1f), Color.white);
                CreateBlock($"Aiming point {x} R", new Vector3(x, 0.035f, 1.55f), new Vector3(2.8f, 0.025f, 1.1f), Color.white);
            }

            // Continuous runway edge stripes so the strip reads at dusk without relying on lights alone.
            CreateBlock("Runway edge L", new Vector3(0f, 0.025f, -3.35f), new Vector3(72f, 0.02f, 0.22f), Color.white);
            CreateBlock("Runway edge R", new Vector3(0f, 0.025f, 3.35f), new Vector3(72f, 0.02f, 0.22f), Color.white);
            // Touchdown zone marks between threshold and aiming points.
            foreach (var x in new[] { -30f, -28f, -26f, -24f, -22f, 22f, 24f, 26f, 28f, 30f })
            {
                CreateBlock($"TDZ {x} L", new Vector3(x, 0.03f, -1.4f), new Vector3(1.4f, 0.02f, 0.5f), Color.white);
                CreateBlock($"TDZ {x} R", new Vector3(x, 0.03f, 1.4f), new Vector3(1.4f, 0.02f, 0.5f), Color.white);
            }
            // Stand bay numbers on the apron (readable from overview).
            PlaceRunwayDigit('1', new Vector3(14f, 0.04f, 14f), yaw: 0f);
            PlaceRunwayDigit('2', new Vector3(22f, 0.04f, 14f), yaw: 0f);
            PlaceRunwayDigit('3', new Vector3(30f, 0.04f, 14f), yaw: 0f);
            // Taxiway centreline dashes along Taxiway A.
            for (var x = -6; x <= 28; x += 6)
                CreateBlock($"Taxi centre {x}", new Vector3(x, 0.035f, 9f), new Vector3(2.4f, 0.02f, 0.16f),
                    new Color(0.95f, 0.85f, 0.2f));
            // Taxiway edge lines along Taxiway A.
            CreateBlock("Taxi edge N", new Vector3(8f, 0.035f, 10.85f), new Vector3(44f, 0.02f, 0.14f), Color.white);
            CreateBlock("Taxi edge S", new Vector3(8f, 0.035f, 7.15f), new Vector3(44f, 0.02f, 0.14f), Color.white);
            // Apron lead-in chevrons from taxi to stand lead.
            for (var i = 0; i < 4; i++)
            {
                var z = 11.2f + i * 0.85f;
                CreateBlock($"Apron chevron {i}", new Vector3(14f + i * 0.4f, 0.04f, z), new Vector3(1.1f, 0.02f, 0.16f),
                    new Color(0.95f, 0.85f, 0.2f));
            }

            for (var x = -4; x <= 28; x += 4)
                CreateBlock("Taxi centre", new Vector3(x, 0.04f, 9f), new Vector3(1.2f, 0.03f, 0.18f), new Color(0.95f, 0.85f, 0.2f));
        }

        /// <summary>
        /// Decision 0025 item 3 — block runway digits readable from overview.
        /// Local +Z is digit height; yaw rotates onto the runway axis.
        /// </summary>
        private static void PlaceRunwayDigit(char digit, Vector3 centre, float yaw)
        {
            var root = new GameObject($"Runway digit {digit}").transform;
            root.position = centre;
            root.rotation = Quaternion.Euler(0f, yaw, 0f);
            void Seg(string name, float x, float z, float sx, float sz)
            {
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
            ParentBlock(root, "Workbench leg L", new Vector3(-1f, 0.4f, 0f), new Vector3(0.12f, 0.8f, 0.8f), new Color(0.25f, 0.25f, 0.28f));
            ParentBlock(root, "Workbench leg R", new Vector3(1f, 0.4f, 0f), new Vector3(0.12f, 0.8f, 0.8f), new Color(0.25f, 0.25f, 0.28f));
            ParentBlock(root, "Shelf frame", new Vector3(-2.2f, 1.1f, -0.1f), new Vector3(0.9f, 1.8f, 0.45f), new Color(0.4f, 0.42f, 0.4f));
            ParentBlock(root, "Oil drum", new Vector3(-1.6f, 0.55f, 0.9f), new Vector3(0.55f, 1.1f, 0.55f), new Color(0.85f, 0.55f, 0.18f));
            ParentBlock(root, "Tool cart body", new Vector3(1.8f, 0.55f, 0.6f), new Vector3(0.9f, 0.7f, 0.7f), new Color(0.35f, 0.45f, 0.55f));
            ParentBlock(root, "Crate stack", new Vector3(2.3f, 0.45f, -0.5f), new Vector3(0.7f, 0.9f, 0.55f), new Color(0.55f, 0.4f, 0.22f));
        }

        private static void PlaceWorldLighting()
        {
            var kit = PreferArtKit(
                "Models/Props/mdl_airfield_lighting_kit_authored_v01.gltf",
                "Models/Props/mdl_airfield_lighting_kit_v02.gltf",
                "Models/Props/mdl_airfield_lighting_kit_v01.gltf");
            var edgeColor = new Color(1f, 1f, 0.85f);
            var taxiColor = new Color(0.2f, 0.85f, 0.35f);
            var obstruction = new Color(0.95f, 0.35f, 0.12f);

            for (var x = -36; x <= 36; x += 6)
            {
                PlaceEdgeLamp(kit, new Vector3(x, 0f, -3.4f), edgeColor);
                PlaceEdgeLamp(kit, new Vector3(x, 0f, 3.4f), edgeColor);
            }

            for (var x = -8; x <= 28; x += 8)
            {
                PlaceTaxiLamp(kit, new Vector3(x, 0f, 11.1f), taxiColor);
                PlaceTaxiLamp(kit, new Vector3(x, 0f, 6.9f), taxiColor);
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
            if (ArtGltfLoader.TryPlaceNamedMesh(kit, "edge_base", position, Quaternion.identity, new Color(0.35f, 0.36f, 0.38f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "edge_stem", position, Quaternion.identity, new Color(0.45f, 0.46f, 0.48f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "edge_collar", position, Quaternion.identity, new Color(0.4f, 0.42f, 0.44f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "edge_lens", position, Quaternion.identity, color, out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "edge_glare", position, Quaternion.identity, new Color(1f, 1f, 0.9f), out _))
                return;
            if (!ArtGltfLoader.TryPlaceNamedMesh(kit, "runway_edge_light", position, Quaternion.identity, color, out _))
                CreateBlock("Runway edge", position + new Vector3(0f, 0.05f, 0f), new Vector3(0.25f, 0.1f, 0.25f), color);
        }

        private static void PlaceTaxiLamp(string kit, Vector3 position, Color color)
        {
            if (ArtGltfLoader.TryPlaceNamedMesh(kit, "taxi_base", position, Quaternion.identity, new Color(0.3f, 0.32f, 0.34f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "taxi_stem", position, Quaternion.identity, new Color(0.4f, 0.42f, 0.44f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "taxi_collar", position, Quaternion.identity, new Color(0.38f, 0.4f, 0.42f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "taxi_lens", position, Quaternion.identity, color, out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "taxi_glare", position, Quaternion.identity, new Color(0.55f, 1f, 0.65f), out _))
                return;
            if (!ArtGltfLoader.TryPlaceNamedMesh(kit, "taxiway_light", position, Quaternion.identity, color, out _))
                CreateBlock("Taxi light", position + new Vector3(0f, 0.18f, 0f), new Vector3(0.18f, 0.35f, 0.18f), color);
        }

        private static void PlaceObstructionLamp(string kit, Vector3 position, Color color, string fallbackName)
        {
            if (ArtGltfLoader.TryPlaceNamedMesh(kit, "obst_base", position, Quaternion.identity, new Color(0.35f, 0.36f, 0.38f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "obst_stem", position, Quaternion.identity, new Color(0.4f, 0.42f, 0.44f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "obst_guard", position, Quaternion.identity, new Color(0.45f, 0.46f, 0.48f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "obst_lens", position, Quaternion.identity, color, out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "obst_ring", position, Quaternion.identity, new Color(0.9f, 0.4f, 0.15f), out _))
                return;
            if (!ArtGltfLoader.TryPlaceNamedMesh(kit, "obstruction_light", position, Quaternion.identity, color, out _))
                CreateBlock(fallbackName, position + new Vector3(0f, 0.2f, 0f), new Vector3(0.22f, 0.22f, 0.22f), color);
        }

        private static void PlaceFloodMast(string kit, Vector3 position, Color color)
        {
            var placed = false;
            placed |= ArtGltfLoader.TryPlaceNamedMesh(kit, "flood_base", position, Quaternion.identity, new Color(0.3f, 0.32f, 0.34f), out _);
            placed |= ArtGltfLoader.TryPlaceNamedMesh(kit, "flood_pole", position, Quaternion.identity, color, out _);
            placed |= ArtGltfLoader.TryPlaceNamedMesh(kit, "flood_brace", position, Quaternion.identity, Shade(color, 0.9f), out _);
            placed |= ArtGltfLoader.TryPlaceNamedMesh(kit, "flood_brace_b", position, Quaternion.identity, Shade(color, 0.88f), out _);
            placed |= ArtGltfLoader.TryPlaceNamedMesh(kit, "flood_ladder", position, Quaternion.identity, new Color(0.35f, 0.36f, 0.38f), out _);
            placed |= ArtGltfLoader.TryPlaceNamedMesh(kit, "flood_arm", position, Quaternion.identity, Shade(color, 0.85f), out _);
            placed |= ArtGltfLoader.TryPlaceNamedMesh(kit, "flood_head", position, Quaternion.identity, new Color(0.25f, 0.26f, 0.28f), out _);
            placed |= ArtGltfLoader.TryPlaceNamedMesh(kit, "flood_lamp", position, Quaternion.identity, new Color(1f, 0.95f, 0.8f), out _);
            placed |= ArtGltfLoader.TryPlaceNamedMesh(kit, "flood_visor", position, Quaternion.identity, new Color(0.2f, 0.21f, 0.22f), out _);
            if (!placed)
                ArtGltfLoader.TryPlaceNamedMesh(kit, "apron_floodlight", position, Quaternion.identity, color, out _);
        }

        private static void PlaceWorldProps()
        {
            var kit = PreferArtKit(
                "Models/Props/mdl_airfield_props_kit_authored_v01.gltf",
                "Models/Props/mdl_airfield_props_kit_v02.gltf",
                "Models/Props/mdl_airfield_props_kit_v01.gltf");

            // Stand lead-in cones (denser apron edge read).
            CreateCone(new Vector3(12.5f, 0.25f, 12.2f));
            CreateCone(new Vector3(12.5f, 0.25f, 15.8f));
            CreateCone(new Vector3(12.5f, 0.25f, 18.2f));
            CreateCone(new Vector3(12.5f, 0.25f, 21.8f));
            CreateCone(new Vector3(23.5f, 0.25f, 12.2f));
            CreateCone(new Vector3(23.5f, 0.25f, 15.8f));
            CreateCone(new Vector3(23.5f, 0.25f, 18.2f));
            CreateCone(new Vector3(23.5f, 0.25f, 21.8f));
            CreateCone(new Vector3(-6f, 0.25f, 11f));
            CreateCone(new Vector3(-10f, 0.25f, 11f));
            CreateCone(new Vector3(4f, 0.25f, 7.2f));
            CreateCone(new Vector3(4f, 0.25f, 10.8f));
            CreateCone(new Vector3(18f, 0.25f, 11.2f));
            CreateCone(new Vector3(28f, 0.25f, 11.2f));
            CreateCone(new Vector3(8f, 0.25f, 23.5f));
            CreateCone(new Vector3(30f, 0.25f, 23.5f));

            // Worksite / hangar barriers.
            CreateBarrier(new Vector3(-14f, 0.45f, 14f), 0f);
            CreateBarrier(new Vector3(-22f, 0.45f, 25.5f), 90f);
            CreateBarrier(new Vector3(-28f, 0.45f, 18f), 0f);
            CreateBarrier(new Vector3(36f, 0.45f, 18f), 90f);
            CreateBarrier(new Vector3(6f, 0.45f, 24.5f), 0f);
            CreateBarrier(new Vector3(34f, 0.45f, 24.5f), 0f);

            PlaceSignBoard(kit, new Vector3(10f, 0f, 22f), 90f);
            PlaceSignBoard(kit, new Vector3(-4f, 0f, 12f), 0f);
            PlaceSignBoard(kit, new Vector3(18f, 0f, 11.5f), 0f);
            PlaceSignBoard(kit, new Vector3(28f, 0f, 12f), 0f);
            PlaceSignBoard(kit, new Vector3(-18f, 0f, 16f), 90f);

            PlaceBaggageDolly(kit, new Vector3(30f, 0f, 22f));
            PlaceBaggageDolly(kit, new Vector3(32.2f, 0f, 22f));
            PlaceBaggageDolly(kit, new Vector3(28f, 0f, 19.5f));
            PlaceBaggageDolly(kit, new Vector3(34f, 0f, 19.5f));
            PlaceBaggageDolly(kit, new Vector3(31f, 0f, 17.2f));
            PlaceBaggageDolly(kit, new Vector3(33.5f, 0f, 17.2f));

            BuildApronSafetyProps();
            BuildFuelFarm();
            BuildParkedGaAircraft();
        }

        /// <summary>
        /// Decision 0025 items 1+3 — fire hydrants, extinguisher cabinets and FOD bins
        /// so the apron edge reads as a working safety-equipped airfield.
        /// </summary>
        private static void BuildApronSafetyProps()
        {
            PlaceFireHydrant("Hydrant apron NE", new Vector3(34f, 0f, 23.5f), 0f);
            PlaceFireHydrant("Hydrant apron NW", new Vector3(8.5f, 0f, 23.5f), 0f);
            PlaceFireHydrant("Hydrant taxi", new Vector3(-2f, 0f, 11.5f), 90f);
            PlaceFireHydrant("Hydrant hangar", new Vector3(-14f, 0f, 16f), 0f);

            PlaceExtinguisherCabinet("Extinguisher terminal", new Vector3(20f, 0f, 24.2f), 180f);
            PlaceExtinguisherCabinet("Extinguisher hangar", new Vector3(-15.5f, 0f, 24.2f), 180f);
            PlaceExtinguisherCabinet("Extinguisher ops", new Vector3(-5f, 0f, 24.2f), 180f);

            PlaceFodBin("FOD bin A", new Vector3(36f, 0f, 20f), 270f);
            PlaceFodBin("FOD bin B", new Vector3(10f, 0f, 11.2f), 0f);
            PlaceFodBin("FOD bin C", new Vector3(-24f, 0f, 16.5f), 90f);

            // Stand lead-in / box paint so stands read as marked bays from overview.
            foreach (var z in new[] { 14f, 20f, 26f })
            {
                CreateBlock($"Stand box front {z}", new Vector3(20f, 0.04f, z - 2.6f), new Vector3(10f, 0.02f, 0.12f), Color.white);
                CreateBlock($"Stand box back {z}", new Vector3(20f, 0.04f, z + 2.6f), new Vector3(10f, 0.02f, 0.12f), Color.white);
                CreateBlock($"Stand box L {z}", new Vector3(14.8f, 0.04f, z), new Vector3(0.12f, 0.02f, 5.2f), Color.white);
                CreateBlock($"Stand box R {z}", new Vector3(25.2f, 0.04f, z), new Vector3(0.12f, 0.02f, 5.2f), Color.white);
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
            Transform root;
            if (ArtPresentationLoader.TryInstantiatePrefab("mdl_fod_bin_v01", out var prefabRoot))
            {
                prefabRoot.name = name;
                root = prefabRoot;
            }
            else
            {
                root = new GameObject(name).transform;
                ParentBlock(root, $"{name} body", new Vector3(0f, 0.45f, 0f), new Vector3(0.7f, 0.75f, 0.55f), new Color(0.95f, 0.75f, 0.15f));
                ParentBlock(root, $"{name} lid", new Vector3(0f, 0.88f, 0f), new Vector3(0.75f, 0.1f, 0.6f), new Color(0.2f, 0.22f, 0.25f));
            }

            root.position = position;
            root.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
        }

        private static void PlaceSignBoard(string kit, Vector3 position, float yawDegrees)
        {
            if (ArtPresentationLoader.TryInstantiatePrefab("mdl_airside_sign_v01", out var prefabRoot))
            {
                prefabRoot.name = "Airside sign";
                prefabRoot.position = position;
                prefabRoot.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
                return;
            }

            var rot = Quaternion.Euler(0f, yawDegrees, 0f);
            if (ArtGltfLoader.TryPlaceNamedMesh(kit, "sign_post", position, rot, new Color(0.35f, 0.36f, 0.38f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "sign_face", position, rot, new Color(0.95f, 0.95f, 0.92f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "sign_cap", position, rot, new Color(0.12f, 0.35f, 0.55f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "sign_brace", position, rot, new Color(0.4f, 0.42f, 0.44f), out _))
                return;

            if (ArtGltfLoader.TryPlaceNamedMesh(kit, "sign_board", position, rot, new Color(0.12f, 0.35f, 0.55f), out _))
                return;

            var root = new GameObject("Airside sign").transform;
            root.position = position;
            root.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
            ParentBlock(root, "Airside sign post", new Vector3(0f, 1.1f, 0f), new Vector3(0.12f, 2.0f, 0.12f), new Color(0.35f, 0.36f, 0.38f));
            ParentBlock(root, "Airside sign face", new Vector3(0.08f, 1.35f, 0f), new Vector3(0.04f, 0.9f, 1.1f), new Color(0.95f, 0.95f, 0.92f));
            ParentBlock(root, "Airside sign back", new Vector3(0f, 1.35f, 0f), new Vector3(0.12f, 1.0f, 1.2f), new Color(0.12f, 0.35f, 0.55f));
        }

        private static void PlaceBaggageDolly(string kit, Vector3 position)
        {
            if (ArtPresentationLoader.TryInstantiatePrefab("mdl_baggage_dolly_v01", out var prefabRoot))
            {
                prefabRoot.name = "Baggage dolly";
                prefabRoot.position = position;
                return;
            }

            if (ArtGltfLoader.TryPlaceNamedMesh(kit, "dolly_bed", position, Quaternion.identity, new Color(0.55f, 0.35f, 0.18f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "dolly_rail_l", position, Quaternion.identity, new Color(0.45f, 0.3f, 0.16f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "dolly_rail_r", position, Quaternion.identity, new Color(0.45f, 0.3f, 0.16f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "dolly_rail_mid", position, Quaternion.identity, new Color(0.45f, 0.3f, 0.16f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "dolly_handle", position, Quaternion.identity, new Color(0.4f, 0.4f, 0.42f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "dolly_cargo", position, Quaternion.identity, new Color(0.7f, 0.55f, 0.25f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "dolly_wheel_fl", position, Quaternion.identity, new Color(0.15f, 0.15f, 0.16f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "dolly_wheel_fr", position, Quaternion.identity, new Color(0.15f, 0.15f, 0.16f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "dolly_wheel_rl", position, Quaternion.identity, new Color(0.15f, 0.15f, 0.16f), out _)
                | ArtGltfLoader.TryPlaceNamedMesh(kit, "dolly_wheel_rr", position, Quaternion.identity, new Color(0.15f, 0.15f, 0.16f), out _))
                return;

            if (ArtGltfLoader.TryPlaceNamedMesh(kit, "baggage_dolly", position, Quaternion.identity,
                    new Color(0.55f, 0.35f, 0.18f), out _))
                return;
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
            }

            CreateCone(new Vector3(-30.5f, 0.25f, 19.2f));
            CreateCone(new Vector3(-37.5f, 0.25f, 19.2f));
            CreateBarrier(new Vector3(-34f, 0.45f, 18.6f), 0f);

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
            for (var i = 0; i < spots.Length; i++)
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
                    ParentBlock(root, "GA wing", new Vector3(0f, 0.05f, 0.2f), new Vector3(3.2f, 0.08f, 0.7f), new Color(0.85f, 0.55f, 0.2f));
                    ParentBlock(root, "GA wing strut L", new Vector3(-0.9f, -0.15f, 0.15f), new Vector3(0.06f, 0.45f, 0.06f), new Color(0.4f, 0.4f, 0.42f));
                    ParentBlock(root, "GA wing strut R", new Vector3(0.9f, -0.15f, 0.15f), new Vector3(0.06f, 0.45f, 0.06f), new Color(0.4f, 0.4f, 0.42f));
                    ParentBlock(root, "GA tail", new Vector3(0f, 0.55f, -1.0f), new Vector3(0.1f, 0.9f, 0.55f), new Color(0.85f, 0.55f, 0.2f));
                    ParentBlock(root, "GA tailplane", new Vector3(0f, 0.35f, -1.05f), new Vector3(1.1f, 0.06f, 0.4f), new Color(0.85f, 0.55f, 0.2f));
                    ParentBlock(root, "GA canopy", new Vector3(0f, 0.35f, 0.55f), new Vector3(0.45f, 0.28f, 0.7f), new Color(0.2f, 0.35f, 0.45f));
                    ParentBlock(root, "GA prop", new Vector3(0f, 0f, 1.25f), new Vector3(0.06f, 0.9f, 0.12f), new Color(0.2f, 0.2f, 0.22f));
                    ParentBlock(root, "GA gear nose", new Vector3(0f, -0.35f, 0.85f), new Vector3(0.08f, 0.35f, 0.08f), new Color(0.3f, 0.3f, 0.32f));
                    ParentBlock(root, "GA gear L", new Vector3(-0.55f, -0.35f, -0.15f), new Vector3(0.08f, 0.35f, 0.08f), new Color(0.3f, 0.3f, 0.32f));
                    ParentBlock(root, "GA gear R", new Vector3(0.55f, -0.35f, -0.15f), new Vector3(0.08f, 0.35f, 0.08f), new Color(0.3f, 0.3f, 0.32f));
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
            return phase switch
            {
                AircraftPhase.Approach => Smooth(new Vector3(-52f, 14f, 0f), new Vector3(-35f, 2f, 0f), progress),
                AircraftPhase.Landing => Smooth(new Vector3(-35f, 2f, 0f), new Vector3(-24f, 0.7f, 0f), progress),
                AircraftPhase.TaxiIn => PositionAlongTaxiRoute(taxiRoute, progress, false),
                AircraftPhase.AtStand => new Vector3(17f, 0.7f, standZ),
                AircraftPhase.Pushback => Smooth(new Vector3(17f, 0.7f, standZ), new Vector3(12f, 0.7f, standZ - 2f), progress),
                AircraftPhase.TaxiOut => progress < 0.15f
                    ? Smooth(new Vector3(12f, 0.7f, standZ - 2f), new Vector3(17f, 0.7f, standZ), progress / 0.15f)
                    : PositionAlongTaxiRoute(taxiRoute, (progress - 0.15f) / 0.85f, true),
                AircraftPhase.Takeoff => Smooth(new Vector3(28f, 0.7f, 0f), new Vector3(48f, 12f, 0f), progress),
                _ => new Vector3(52f, 15f, 0f)
            };
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
            var material = CreateMaterial(Color.white, artTextureRelativePath, Vector2.one);
            material.SetFloat("_Surface", 1f); // URP transparent hint when available
            if (material.HasProperty("_Mode"))
                material.SetFloat("_Mode", 3f);
            material.color = new Color(1f, 1f, 1f, 1f);
            material.mainTexture = texture;
            quad.GetComponent<Renderer>().material = material;
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
            // Heuristic for untextured primitives (cars, props, glow quads).
            if (color.a < 0.99f)
                return AirsideMaterialLibrary.SurfaceKind.Glass;
            if (color.r > 0.85f && color.g > 0.85f && color.b > 0.85f)
                return AirsideMaterialLibrary.SurfaceKind.AircraftSkin;
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
