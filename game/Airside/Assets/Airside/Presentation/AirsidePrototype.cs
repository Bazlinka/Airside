using System;
using System.Collections.Generic;
using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace Airside.Presentation
{
    public sealed partial class AirsidePrototype : MonoBehaviour
    {
        private ManualSimulationClock _clock;
        private AirportSimulation _simulation;
        private Transform[] _commercialAircraft;
        // Each commercial keeps a stable livery slot (0 = Coastline Regional blue,
        // 1 = first traffic accent) for its lifetime. Slots are keyed by AircraftId so a
        // respawn-driven re-sort of the flight list can no longer repaint a plane
        // or leave two aircraft in the same livery.
        private readonly Dictionary<string, int> _commercialLiverySlot = new();
        // Damped roll angle per airframe so banking eases in and out of a turn.
        private readonly Dictionary<string, float> _bankDegrees = new();
        // Whole-aircraft spool, keyed by instance id; engine audio reads it.
        private readonly Dictionary<int, float> _propRpm = new();
        // Per-engine spools (id*2+1 left, id*2+2 right). Each lives in its own dictionary:
        // sharing one made the 737's fans damp toward prop RPM and fan RPM alternately
        // every frame, and a per-engine key could land on another aircraft's audio key.
        private readonly Dictionary<int, float> _enginePropRpm = new();
        /// <summary>Spool acceleration (rpm per second) per engine, so the propeller speeds up and settles smoothly.</summary>
        private readonly Dictionary<int, float> _enginePropRpmRate = new();
        private readonly Dictionary<int, float> _jetFanRpm = new();
        // ADR 0151: shaft power / N1 as a 0..1 fraction. A governed propeller barely changes
        // speed between taxi and takeoff, so the engine note, the exhaust and the heat haze read
        // power from here rather than trying to infer it from the spool.
        private readonly Dictionary<int, float> _propPower = new();

        private Light _sun;
        private Light _fillLight;
        private Light[] _apronLights;
        private Light[] _landsideLights;
        private Light[] _thresholdLights;
        private Light[] _alsLights;
        private Light[] _runwayEdgeLights;
        private Light[] _standLights;
        private Light _aerodromeBeacon;
        private ReflectionProbe _apronProbe;
        private ReflectionProbe _terminalProbe;
        private Transform _rainRoot;
        private Transform _touchdownSmoke;
        private Renderer[] _touchdownSmokeRenderers;

        /// <summary>
        /// Tyre smoke: a small pool of puffs emitted from the main-gear contact
        /// patches — a hard burst as the wheels spin up at touchdown, then a
        /// thinning trail while the rollout scrubs speed off. Presentation only.
        /// </summary>
        private WheelPuff[] _wheelPuffs;
        private float _wheelSmokeEmitCooldown;

        private struct WheelPuff
        {
            public Transform Transform;
            public Renderer Renderer;
            public float Age;
            public float Life;
            public Vector3 Drift;
            public float StartRadius;
            public float EndRadius;
            public float StartAlpha;
        }
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
        private readonly Dictionary<int, AircraftSoundEmitter> _engineAudio = new();
        private Transform _cloudRoot;
        private Transform _cloudUmbraRoot;
        private int _cloudTintKey = int.MinValue;
        private Transform _birdFlockRoot;
        private Transform[] _birdWingL;
        private float[] _birdPhaseSeed;
        private Transform[] _birdWingR;
        private Transform _apronLifeRoot;
        // Role flags are read from the name once; Object.name allocates on every access.
        private readonly List<(Transform Person, Vector3 BasePos, bool Walker, bool Sitter, bool Marshaller)> _apronPeople =
            new List<(Transform, Vector3, bool, bool, bool)>();
        private Transform _hangarDoor;
        private float _hangarDoorClosedX = -20f;
        private readonly List<(Transform Panel, float ClosedX, float OpenDelta)> _hangarDoorPanels =
            new List<(Transform, float, float)>();
        private Light _hangarBayLight;
        private AirsideDayVolume _dayVolume;
        private float _touchdownSmokeRemaining;
        private AudioSource _ambientWindAudio;
        private AudioSource _ambientRainAudio;
        private AudioSource _ambientCoastAudio;
        private AudioSource _thunderAudio;
        private AudioClip _thunderClip;
        // ADR 0059: a storm strike's flash and its thunder, decoupled so thunder can lag the
        // flash the way sound lags light. Both are presentation-only — Lightning.StrikesAt
        // decides *when*, this only decides how it looks/sounds.
        private float _lightningFlashAt = float.NegativeInfinity;
        private float _lightningDistance01;
        private float _thunderPlayAt = float.PositiveInfinity;
        private AudioSource _uiAudio;
        private AudioClip _uiClickClip;
        private readonly Dictionary<string, AircraftPhase> _previousPhases = new Dictionary<string, AircraftPhase>();
        private readonly HashSet<string> _touchdownFired = new HashSet<string>();
        private readonly HashSet<string> _rotateFired = new HashSet<string>();
        private readonly List<(Material Material, Color DryColor, float DrySmoothness, float DryMetallic, float DryBumpScale, bool Paved, Texture DryAlbedo)> _wetSurfaces =
            new List<(Material, Color, float, float, float, bool, Texture)>();
        private static readonly MaterialPropertyBlock RendererTintBlock = new();
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
        private static readonly int MainTexStId = Shader.PropertyToID("_MainTex_ST");
        private static readonly int CloudAtlasRectId = Shader.PropertyToID("_AtlasRect");
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
        // Parallel to _airfieldLightRenderers. Object.name allocates a new string on every
        // read, so the taxi/edge split is resolved once at collect time, not every frame.
        private readonly List<bool> _airfieldLightIsTaxi = new List<bool>();
        private readonly List<Renderer> _nightGlowRenderers = new List<Renderer>();
        // Parallel to _nightGlowRenderers: 0 window, 1 airside glazing, 2 interior card.
        // Resolved once — the glow pass read gameObject.name (a fresh string) twice per pane.
        private readonly List<byte> _nightGlowKind = new List<byte>();
        // Daylight each static tint pass last wrote. NaN forces the next pass (ADR 0101).
        private float _airfieldLightsAppliedDaylight = float.NaN;
        private float _nightGlowAppliedDaylight = float.NaN;
        private Transform _fuelTruck;
        private Transform _cateringTruck;
        private Transform _baggageCart;
        private Transform _passengerBus;
        private Transform _stairs;
        private Transform _chocks;
        private Transform _wetPuddleRoot;
        private Transform _gpuCart;
        private Transform _windsockSock;
        private Quaternion[] _windsockSegmentRest;
        private Transform _terminalFlag;
        private Transform _coastFoam;
        private readonly List<Transform> _coastFoamLayers = new List<Transform>();
        private readonly List<Renderer> _coastFoamRenderers = new List<Renderer>();
        private readonly List<(Transform Boat, Vector3 BasePos, float BaseYaw)> _coastBoats =
            new List<(Transform, Vector3, float)>();
        private readonly List<Renderer> _coastWaterRenderers = new List<Renderer>();
        // Parallel to _coastWaterRenderers: shallows bob, deep water only scrolls. Resolved at
        // collect time because Object.name allocates a string on every read.
        private readonly List<bool> _coastWaterIsShallows = new List<bool>();
        private Transform _jettyDeck;
        private Transform _opsAntennaDish;
        private Transform _starFieldRoot;
        private Camera _mainCamera;
        private AirsideCameraController _cameraController;
        private double _preciseTime;
        private float _presentationClock;
        private bool _audioMuted;

        /// <summary>
        /// Menu visibility. Time is live Adelaide time (ADR 0045), so the menu does not
        /// pause anything: the airport keeps running behind it.
        /// </summary>
        private bool _menuOpen;
        private bool _optionsOpen;
        private const float EngineVolumeRunning = 0.06f;
        private const float AmbientWindVolume = 0.045f;
        private const float AmbientRainVolume = 0.07f;
        private const float AmbientStormVolume = 0.11f;
        private const float AmbientCoastVolume = 0.016f; // ADR 0136: the rebuilt bed is ~7 dB hotter than the old single wave
        // Live Adelaide time drives sun, floods and aircraft lamps. Set true only to
        // force noon while debugging lighting (was pinned through the 24 h day cutover).
        private static readonly bool PinDaylightPresentation =
            AirsideBareField.HasLaunchFlag("-airsidePinDaylight");
        private static readonly TimeSpan? ReviewLocalTime =
            DaylightPresentation.ReviewLocalTime(Environment.GetCommandLineArgs());
        // Mutable so multi-shot review soaks can switch clear→storm between PNGs.
        private static WeatherKind? ReviewWeather = ReviewWeatherOverride(Environment.GetCommandLineArgs());

        /// <summary>Override review weather mid-soak (presentation QA only; not sim weather).</summary>
        private static void SetReviewWeatherToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return;
            if (Enum.TryParse(token, true, out WeatherKind weather))
                ReviewWeather = weather;
        }

        /// <summary>Sky over the field: the demo circuit's weather, or the airline clock's in airline mode.</summary>
        private WeatherKind CurrentWeather => ReviewWeather
            ?? (LiveWeatherHealthy ? _liveWeatherSnapshot.Value.Kind
                : FleetMode ? Weather.At(_clock.Now) : _simulation.CurrentWeather);

        private WeatherLook CurrentWeatherLook => ReviewWeather.HasValue
            ? WeatherLook.For(ReviewWeather.Value)
            : LiveWeatherHealthy ? _liveWeatherSnapshot.Value.Look
            // ADR 0143: eased between hours so the sky never snaps.
            : FleetMode ? Weather.LookAt(_clock.Now) : WeatherLook.For(CurrentWeather);

        /// <summary>This frame's sky, fog, mist and cloud layers (ADR 0143).</summary>
        private AtmosphereLook _atmosphere;

        private static Color ToColor(Rgb rgb) => new(rgb.R, rgb.G, rgb.B);

        /// <summary>Actual Adelaide wind for cloth/weather motion; never runway selection.</summary>
        private SurfaceWind PresentationWind => LiveWeatherHealthy
            ? _liveWeatherSnapshot.Value.Wind
            : _operations != null ? _operations.Wind : RunwayWeather.At(AirlineClock.Default, _clock.Now);

        private float PresentationDaylight =>
            DaylightPresentation.Resolve(PinDaylightPresentation, PresentationCelestial.Daylight);

        private long _celestialSecond = long.MinValue;
        private CelestialSky _celestial;

        /// <summary>
        /// Time of day for sun, sky, floods and lamps: the real Adelaide wall clock the HUD
        /// shows. Celestial position is cached once per real second.
        /// </summary>
        private CelestialSky PresentationCelestial
        {
            get
            {
                var utc = PresentationUtc();
                var second = utc.Ticks / TimeSpan.TicksPerSecond;
                if (second != _celestialSecond)
                {
                    _celestialSecond = second;
                    _celestial = CelestialSky.AtAdelaide(utc);
                }

                return _celestial;
            }
        }

        private DateTime PresentationUtc()
        {
            if (FleetMode && _operations?.Clock != null)
            {
                var clock = _operations.Clock;
                if (ReviewLocalTime.HasValue)
                {
                    var local = clock.LocalAt(_clock.Now).Date + ReviewLocalTime.Value;
                    return TimeZoneInfo.ConvertTimeToUtc(
                        DateTime.SpecifyKind(local, DateTimeKind.Unspecified), AirlineClock.Adelaide);
                }

                return clock.UtcAt(_clock.Now);
            }

            var utc = DateTime.UtcNow;
            if (!ReviewLocalTime.HasValue)
                return utc;
            var adelaide = TimeZoneInfo.ConvertTimeFromUtc(utc, AirlineClock.Adelaide).Date + ReviewLocalTime.Value;
            return TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(adelaide, DateTimeKind.Unspecified), AirlineClock.Adelaide);
        }

        private float _apronProbeRefreshAt;
        private int _probeBand = int.MinValue;
        /// <summary>
        /// The live prototype. Held statically so a second bootstrap — a scene reload,
        /// a duplicate component dropped in a scene, or the runtime hook firing twice —
        /// is destroyed on arrival instead of running a second world and a second HUD
        /// on top of the first (ADR 0041).
        /// </summary>
        private static AirsidePrototype _active;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartPrototype()
        {
            if (_active != null || FindFirstObjectByType<AirsidePrototype>() != null)
                return;
            new GameObject("Airside Prototype").AddComponent<AirsidePrototype>();
        }

        private void Awake()
        {
            if (_active != null && _active != this)
            {
                // A second bootstrap must not build a second world on top of the first.
                // Destroy is deferred to the end of the frame, so disable first —
                // otherwise this component's Update/OnGUI run once against a world it
                // never built and dereference a null simulation.
                enabled = false;
                Destroy(gameObject);
                return;
            }

            _active = this;
            if (AircraftAudioReview.TryStart(gameObject))
            {
                enabled = false;
                return;
            }
            // Automated packaged review/soak runs may not own foreground focus. Keep their
            // clock and capture coroutine moving; ordinary player launches retain Unity's
            // normal pause-when-backgrounded behaviour.
            if (SoakMode)
                Application.runInBackground = true;
            Debug.Log("Airside " + BuildIdentityReader.Current.FullLabel);
            ApplyLoadedSettings();
            _clock = new ManualSimulationClock(new SimulationTime(0));
            _simulation = new AirportSimulation(_clock, new SeededRandomSource(24031996), new ReservationTable());
            _preciseTime = _clock.Now.ElapsedSeconds;

            BuildLightingAndCamera();
            ApplyMasterMute();
            // The title screen opens first; the camera glide plays when the player continues.
            if (_cameraController != null)
            {
                _cameraController.PointerOverHud = IsPointerOverHud;
                _cameraController.FieldClick = TrySelectAircraftAtScreen;
            }
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
                // Operational YPAD lighting is not decoration. The focused release keeps
                // real-metre runway edges, thresholds, PAPI, HIAL, apron floods and
                // stand markers, and omits only landside streetlights.
                _apronLights = AirsideBareField.Enabled
                    ? BuildApronLights()
                    : Array.Empty<Light>();
                _landsideLights = Array.Empty<Light>();
                _thresholdLights = AirsideBareField.Enabled
                    ? BuildYpadThresholdPapiAndApproachLights()
                    : Array.Empty<Light>();
                _alsLights = Array.Empty<Light>();
                _runwayEdgeLights = AirsideBareField.Enabled
                    ? BuildYpadRunwayEdgeLights()
                    : Array.Empty<Light>();
                _standLights = AirsideBareField.Enabled
                    ? BuildStandLighting()
                    : Array.Empty<Light>();
                // Decision 0025 item 5's realtime apron/terminal reflection probes only ever
                // existed on the legacy 1:20 miniature circuit (built in the branch above) —
                // the real Adelaide bare-field world never got its own, so wet asphalt and
                // the 28 terminal glazing bays picked up no local floodlight reflections at
                // all. One real-scale probe covers both (the whole apron/glazing frontage is
                // under 50 m deep along Z, so a single box-projected probe reaches both).
                _apronProbe = AirsideBareField.Enabled ? BuildBareApronReflectionProbe() : null;
            }
            // Every lens queued above becomes one merged mesh per colour (ADR 0124).
            FlushYpadLenses();
            _rainRoot = AirsideFocusMode.ShowEnvironment || AirsideBareField.Enabled
                ? BuildRainRoot()
                : null;
            // Touchdown smoke is circuit presentation, independent of disabled world props.
            _touchdownSmoke = BuildTouchdownSmoke();
            BuildWheelSmoke();
            _skidMarkRoot = null;
            _taxiSprayRoot = AirsideFocusMode.ShowEnvironment || AirsideBareField.Enabled
                ? BuildTaxiSprayRoot()
                : null;
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
            _thunderAudio = gameObject.AddComponent<AudioSource>();
            _thunderAudio.loop = false;
            _thunderAudio.playOnAwake = false;
            _thunderAudio.spatialBlend = 0f;
            _uiAudio = gameObject.AddComponent<AudioSource>();
            _uiAudio.playOnAwake = false;
            _uiAudio.spatialBlend = 0f;
            _uiAudio.volume = 0.3f;
            AirsideSceneIndex.Capture();
            if (AirsideFocusMode.ShowBuildings || AirsideBareField.Enabled)
                CollectNightGlowWindows();
            _fuelFarmLight = AirsideFocusMode.ShowDecorativeLights
                ? AirsideSceneIndex.FindLight("Fuel farm light") : null;
            _arffBayLight = AirsideFocusMode.ShowDecorativeLights
                ? AirsideSceneIndex.FindLight("ARFF bay light") : null;
            var worldRenderers = AirsideSceneIndex.Renderers;
            CollectWetSurfaces(worldRenderers);
            if (AirsideFocusMode.ShowEnvironment || AirsideBareField.Enabled)
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

            if (AirsideFocusMode.ShowEnvironment || AirsideBareField.Enabled)
            {
                // Bare Adelaide also builds YPAD shore foam; collect it so the pulse runs.
                CollectCoastalMotionTargets();
                _horizonDome = AirsideSceneIndex.Find("Horizon dome");
                _cloudRoot = AirsideSceneIndex.Find("Cloud bands");
                _cloudUmbraRoot = AirsideSceneIndex.Find("Cloud umbras");
            }
            _commercialAircraft = Array.Empty<Transform>();
            _commercialAircraftIds = Array.Empty<string>();
            SyncCommercialAircraftViews();
            if (AirsideFocusMode.ShowTurnaroundVehicles)
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
                // Was the one turnaround vehicle with no art path at all, so it fell back to a
                // flat-shaded box. VEH-004 gives it the scissor-lift hi-loader silhouette.
                _cateringTruck = BuildServiceVehicle("Catering truck", new Color(0.82f, 0.86f, 0.88f),
                    new Vector3(2.9f, 1.55f, 1.3f),
                    PreferArtKit("Models/Vehicles/mdl_catering_truck_v01.gltf"));
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
                OrientPlusXKitToForward(_cateringTruck);
                OrientPlusXKitToForward(_baggageCart);
                OrientPlusXKitToForward(_passengerBus);
            }

            if (AirsideFocusMode.ShowTurnaroundVehicles)
            {
                _stairs = BuildStairs();
                _chocks = BuildChocks();
                _gpuCart = BuildGpuCart();
                // Pushback tugs are a fleet pool now (AirsidePrototype.PushbackTugs.cs, ADR 0126).
            }

            if (AirsideFocusMode.ShowWorldProps)
                _windsockSock = BuildWindsock();
            if (AirsideFocusMode.ShowBuildings)
                EnsureStandThreeVisual();
            if (AirsideFocusMode.ShowTerminal && _terminalGroundY.HasValue)
                BuildAerobridges(_terminalGroundY.Value);
            if (_commercialAircraft.Length > 0)
                _cameraController.SetFollowTargets((Transform[])_commercialAircraft.Clone());
            AirsideRuntimeQuality.AfterWorldBuilt();
            AirsideStaticWorld.Finalize(_airfieldRoot);
            if (_apronProbe != null)
            {
                _apronProbe.RenderProbe();
                // ApplyDayCycle runs on the first frame and uses this same band. Mark it
                // now so the initial 128px cubemap capture is not immediately requested
                // a second time while the player is still reaching its first interactive
                // frame.
                _probeBand = AirsideRuntimeQuality.ProbeBand(PresentationDaylight, 0f);
                _apronProbeRefreshAt = Time.unscaledTime + 30f;
            }
            if (_terminalProbe != null)
                _terminalProbe.RenderProbe();
        }

        private void OnDestroy()
        {
            ReleaseCockpitAirflow();
            if (_cockpitInterior != null)
            {
                _cockpitInterior.Leave();
                Destroy(_cockpitInterior.gameObject);
            }
            _cameraController?.EndCockpit();
            ResetFlightWorld();
            if (_flightTerrain != null) Destroy(_flightTerrain.gameObject);
            DisposeSoakRecorders();
            if (_active == this)
                _active = null;
            if (_miniMapTexture != null)
                Destroy(_miniMapTexture);
        }

        private void Update()
        {
            // A duplicate instance is disabled in Awake before this can run, but a
            // half-built world must never hard-crash every frame.
            if (_simulation == null)
                return;

            var soakUpdateStarted = SoakMode ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
            ReadSimulationControls();
            DriveSoak();
            AirsideFramePacing.Tick(AirsideSettings.Current.UncappedFrameRate, SoakMode);

            // Live time: once an airline runs, simulation time is read off the real clock.
            // Before that the demo circuit simply runs at 1x.
            _preciseTime = FleetMode
                ? FlightJourneyPresentationTime(_operations.Clock.SecondsAt(DateTime.UtcNow))
                : _preciseTime + Time.unscaledDeltaTime;

            var wholeSeconds = (long)Math.Floor(_preciseTime);
            if (wholeSeconds > _clock.Now.ElapsedSeconds)
            {
                _clock.Set(new SimulationTime(wholeSeconds));
                // The demo circuit is not drawn once an airline runs. Its update steps one
                // simulated second at a time with allocating reservation queries, so after the
                // Mac slept for hours it spent a long frame re-flying a hidden circuit.
                if (!FleetMode)
                    _simulation.Update();
                UpdateAirlineOperations();
                // ADR 0059: Lightning.StrikesAt is a pure function of the simulated second, so
                // it must be asked exactly once per second — asking every frame would re-ask
                // the same answer for as long as that second stays current and never notice
                // the edge, and a big time-scale jump must never re-fire every second it skips.
                if (Lightning.StrikesAt(_clock.Now))
                {
                    _lightningDistance01 = Lightning.DistanceFor(_clock.Now);
                    _lightningFlashAt = Time.unscaledTime;
                    _thunderPlayAt = Time.unscaledTime + Lightning.ThunderDelaySeconds(_lightningDistance01);
                }
            }

            UpdateFlightWorld();
            UpdateLiveWeather();
            ApplyDayCycle();
            AdvancePresentationClock();
            var soakStageStarted = SoakMode ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
            UpdateAircraftVisual();
            UpdateCockpitView();
            TraceFlightJourneyReview();
            if (SoakMode)
                _soakFleetTicks += System.Diagnostics.Stopwatch.GetTimestamp() - soakStageStarted;
            if (AirportPresentationVisible)
            {
                UpdateAerobridges();
                UpdateBoardingPresentation();
            }
            soakStageStarted = SoakMode ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
            UpdateFocusAudioListener();
            UpdateLiveTraffic();
            UpdateSkyTraffic();
            if (SoakMode)
                _soakSkyTicks += System.Diagnostics.Stopwatch.GetTimestamp() - soakStageStarted;
            UpdateWindsock();
            UpdateTerminalFlag();
            UpdateEngineAudio();
            UpdateAmbientAudio();
            UpdateSoundscape();
            UpdateWeatherPresentation();
            UpdateTouchdownSmoke();
            UpdateWheelSmoke();
            UpdateCloudDrift();
            UpdateAtmosphereLayers();
            if (AirportPresentationVisible) UpdateBirdFlock();
            UpdateHangarDoor();
            if (AirportPresentationVisible) UpdateCoastalMotion();
            UpdateOpsAntenna();
            UpdateStarField();
            soakStageStarted = SoakMode ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
            if (AirportPresentationVisible)
            {
                UpdateApronLife();
                UpdateGateServicing();
                UpdatePushbackTugs();
            }
            if (SoakMode)
                _soakGroundTicks += System.Diagnostics.Stopwatch.GetTimestamp() - soakStageStarted;
            if (SoakMode)
                _soakUpdateTicks += System.Diagnostics.Stopwatch.GetTimestamp() - soakUpdateStarted;
        }

        private readonly List<FleetAircraft> _gateServicingCandidates = new();

        /// <summary>
        /// Visible turnaround activity. A real player departure owns the GSE team while it is
        /// preparing: the equipment shown is the exact active DeparturePrep stage. When no
        /// player aircraft is turning, the same authored vehicles fall back to the older
        /// ambient terminal cycle so Adelaide never becomes visually dead.
        /// </summary>
        private void UpdateGateServicing()
        {
            if (!FleetMode || _operations == null)
                return;

            FleetAircraft playerTurn = null;
            foreach (var aircraft in _operations.FleetOf(_operations.PlayerAirline))
            {
                // A helicopter is fuelled and briefed at its pad with no vehicles to draw (ADR 0207).
                if (aircraft.State != FleetState.AtStand || !aircraft.Scheduled.HasValue || aircraft.Type.IsRotorcraft)
                    continue;
                var prep = DeparturePrep.For(aircraft, _clock.Now, _operations.CareerState.BaseLevel);
                if (prep.Ready)
                    continue;
                if (playerTurn == null
                    || aircraft.Scheduled.Value.DepartAt.CompareTo(playerTurn.Scheduled.Value.DepartAt) < 0)
                    playerTurn = aircraft;
            }

            if (playerTurn != null)
            {
                UpdatePlayerTurnaroundServicing(playerTurn);
                UpdateAmbientServicing(playerTurn, null);
                return;
            }

            SetEquipmentVisible(_chocks, false);
            SetEquipmentVisible(_gpuCart, false);

            var candidates = _gateServicingCandidates;
            candidates.Clear();
            foreach (var aircraft in _operations.Fleet)
                if (aircraft.State == FleetState.AtStand && AdelaideGround.IsTerminalGate(aircraft.Stand))
                    candidates.Add(aircraft);

            if (candidates.Count == 0)
            {
                HideTurnaroundEquipment();
                UpdateAmbientServicing(null, null);
                return;
            }

            var cycleIndex = (int)(_preciseTime / 120.0) % candidates.Count;
            var parked = candidates[cycleIndex];
            var pose = AdelaideGround.StandPose(parked.Stand);
            var nose = new Vector3(pose.NoseX, 0f, pose.NoseZ);
            var side = new Vector3(-nose.z, 0f, nose.x);
            var stop = new Vector3(pose.X, AirsideFlightPath.GroundY, pose.Z);
            var cycle = (float)(_preciseTime % 120.0);
            var layout = AircraftLayout.For(parked.Type);
            var ground = AirsideFlightPath.GroundY;
            var fuel = layout.FuelTruck;
            var bags = layout.BaggageTrain;
            var bagSide = AircraftLayout.SideOf(layout.CargoDoor);

            SetEquipmentVisible(_cateringTruck, false);
            SetEquipmentVisible(_stairs, false);
            UpdateVehicle(_fuelTruck, cycle < 72f, LayoutToWorld(pose, fuel, ground),
                LayoutToWorld(pose, (layout.HalfSpan + 14f, fuel.Z - 16f), ground));
            UpdateVehicle(_baggageCart, cycle >= 18f && cycle < 96f, LayoutToWorld(pose, bags, ground),
                LayoutToWorld(pose, (bagSide * (layout.HalfSpan + 12f), bags.Z - 14f), ground));
            UpdateVehicle(_passengerBus, cycle >= 48f,
                LayoutToWorld(pose, (-(layout.HalfSpan + 6f), layout.PassengerDoor.Z - 8f), ground),
                LayoutToWorld(pose, (-(layout.HalfSpan + 16f), layout.TailZ - 10f), ground));
            UpdateAmbientServicing(null, parked);
        }

        // ---- AI turnarounds (ADR 0126) ---------------------------------------------------

        private const int AmbientServiceSets = 4;
        private readonly List<(Transform Fuel, Transform Bags, string Registration)> _ambientSets = new();
        private readonly List<FleetAircraft> _ambientWanted = new();

        /// <summary>
        /// A small pool of fuel trucks and baggage trains for AI aircraft on stand, besides the full
        /// sequenced set the player's own turnaround (or the rotating showcase) already uses. Each works
        /// its aircraft on <see cref="ApronServiceSchedule"/>'s window and drives off when it ends.
        /// </summary>
        private void UpdateAmbientServicing(FleetAircraft skipA, FleetAircraft skipB)
        {
            var wanted = _ambientWanted;
            wanted.Clear();
            foreach (var aircraft in _operations.Fleet)
            {
                if (wanted.Count >= AmbientServiceSets)
                    break;
                if (aircraft.Airline.IsPlayer || aircraft.State != FleetState.AtStand || aircraft.Type.IsRotorcraft
                    || aircraft == skipA || aircraft == skipB || string.IsNullOrEmpty(aircraft.Stand.Value))
                    continue;
                var onStand = _preciseTime - aircraft.StateStartedAt.ElapsedSeconds;
                double? toDeparture = aircraft.Scheduled.HasValue
                    ? aircraft.Scheduled.Value.DepartAt.ElapsedSeconds - _preciseTime
                    : null;
                if (ApronServiceSchedule.FuelAlongside(onStand, toDeparture)
                    || ApronServiceSchedule.BaggageAlongside(onStand, toDeparture))
                    wanted.Add(aircraft);
            }

            while (_ambientSets.Count < wanted.Count)
                _ambientSets.Add((BuildAmbientVehicle("Fuel truck"), BuildAmbientVehicle("Baggage cart"), null));

            for (var i = 0; i < _ambientSets.Count; i++)
            {
                var set = _ambientSets[i];
                if (i >= wanted.Count)
                {
                    DriveOffAndHide(set.Fuel);
                    DriveOffAndHide(set.Bags);
                    continue;
                }

                var aircraft = wanted[i];
                if (set.Registration != aircraft.Registration)
                {
                    // A new job: start from this stand's park points rather than gliding across the apron.
                    set = (set.Fuel, set.Bags, aircraft.Registration);
                    _ambientSets[i] = set;
                    if (set.Fuel != null) set.Fuel.position = Vector3.zero;
                    if (set.Bags != null) set.Bags.position = Vector3.zero;
                }

                var pose = AdelaideGround.StandPose(aircraft.Stand);
                var nose = new Vector3(pose.NoseX, 0f, pose.NoseZ);
                var side = new Vector3(-nose.z, 0f, nose.x);
                var stop = new Vector3(pose.X, AirsideFlightPath.GroundY, pose.Z);
                var onStand = _preciseTime - aircraft.StateStartedAt.ElapsedSeconds;
                double? toDeparture = aircraft.Scheduled.HasValue
                    ? aircraft.Scheduled.Value.DepartAt.ElapsedSeconds - _preciseTime
                    : null;
                var layout = AircraftLayout.For(aircraft.Type);
                var fuel = layout.FuelTruck;
                var bags = layout.BaggageTrain;
                var bagSide = AircraftLayout.SideOf(layout.CargoDoor);
                ServeOrPark(set.Fuel, ApronServiceSchedule.FuelAlongside(onStand, toDeparture),
                    LayoutToWorld(pose, fuel, stop.y), LayoutToWorld(pose, (layout.HalfSpan + 14f, fuel.Z - 16f), stop.y));
                ServeOrPark(set.Bags, ApronServiceSchedule.BaggageAlongside(onStand, toDeparture),
                    LayoutToWorld(pose, bags, stop.y),
                    LayoutToWorld(pose, (bagSide * (layout.HalfSpan + 12f), bags.Z - 14f), stop.y));
            }
        }

        /// <summary>Drive to the aircraft while serving; drive back to the park point and vanish when done.</summary>
        private void ServeOrPark(Transform vehicle, bool serving, Vector3 service, Vector3 park)
        {
            if (vehicle == null)
                return;
            if (!serving && (!vehicle.gameObject.activeSelf || Vector3.Distance(vehicle.position, park) < 0.1f))
            {
                vehicle.gameObject.SetActive(false);
                return;
            }

            UpdateVehicle(vehicle, serving, service, park);
        }

        private static void DriveOffAndHide(Transform vehicle)
        {
            if (vehicle != null)
                vehicle.gameObject.SetActive(false);
        }

        private static void SetEquipmentVisible(Transform equipment, bool visible)
        {
            if (equipment != null)
                equipment.gameObject.SetActive(visible);
        }

        /// <summary>
        /// Keyboard shortcuts for the five controls the bar carries, plus Escape for
        /// the menu and R to reset the view. Camera movement itself (orbit, pan,
        /// zoom, height) is read by AirsideCameraController; follow and reset live
        /// here so there is exactly one owner of each.
        /// </summary>
        /// <summary>
        /// ADR 0152 — the visual clock, advanced by the frame and slewed onto the wall clock
        /// rather than snapped to it.
        ///
        /// Aircraft positions are a direct function of this time with no smoothing anywhere
        /// after it, so every irregularity in it is drawn. Reading <see cref="DateTime.UtcNow"/>
        /// straight into it meant a frame that took 60 ms moved a departing aircraft the whole
        /// 60 ms in one step — at 70 m/s, a four-metre snap — and the field currently runs with
        /// p95 frame times over 33 ms, so that happened constantly. It was worst on the takeoff
        /// roll, where the aircraft is fastest, and plainly visible on a taxi.
        ///
        /// Advancing by <see cref="Time.unscaledDeltaTime"/> instead paces the motion with the
        /// frames that draw it, and a bounded slew keeps it honest against real time: drift is
        /// eased out over about a second, and anything past <see cref="LiveClockResyncSeconds"/>
        /// — a sleep, a load, a long stall — snaps, because that is a real jump in time and not
        /// a pacing wobble. Never runs backwards: simulated seconds are derived from it.
        /// </summary>
        private double LivePresentationTime(double wallClockSeconds)
        {
            if (_preciseTime <= 0.0 || Math.Abs(wallClockSeconds - _preciseTime) > LiveClockResyncSeconds)
                return wallClockSeconds;

            var advanced = _preciseTime + Time.unscaledDeltaTime;
            var drift = wallClockSeconds - advanced;
            advanced += drift * Math.Min(1.0, Time.unscaledDeltaTime * LiveClockSlewRate);
            return Math.Max(_preciseTime, advanced);
        }

        /// <summary>Beyond this much difference the wall clock has genuinely jumped: snap to it.</summary>
        private const double LiveClockResyncSeconds = 0.75;

        /// <summary>How quickly accumulated drift is eased out (per second).</summary>
        private const double LiveClockSlewRate = 1.5;

        private void ReadSimulationControls()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            // Any key or click during the launch intro skips it and does nothing else.
            if (ReadIntroSkip(keyboard))
                return;

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                // An open menu closes first. Esc used to clear a selection (and reset the view)
                // behind the menu while leaving the menu itself open.
                if (_menuOpen)
                {
                    if (_optionsOpen)
                    {
                        _optionsOpen = false;
                        PlayUiClick();
                        return;
                    }
                    ToggleMenu();
                    return;
                }

                if (InCockpit)
                {
                    ExitCockpit(false);
                    return;
                }
                if (TryCloseControlsHelp())
                    return;
                if (TrySplashBack())
                    return;
                if (TryCloseAirlineOverlay())
                    return;
                if (ClearAircraftSelection())
                {
                    ResetView();
                    return;
                }
                ToggleMenu();
            }

            // While the menu is up it owns the keyboard, so a stray hotkey cannot
            // change speed or camera behind it.
            if (_menuOpen)
                return;

            // Typing the airline name must not follow, reset the view or mute.
            if (!InCockpit && ReadAirlineControls(keyboard))
                return;

            if (keyboard.fKey.wasPressedThisFrame)
                ToggleFollow();
            if (keyboard.rKey.wasPressedThisFrame)
                ResetView();
            if (keyboard.mKey.wasPressedThisFrame)
            {
                // Muting used to be silent in both senses: nothing on screen said the
                // sound was off, so a stray M looked like broken audio.
                _audioMuted = !_audioMuted;
                ApplySettingsAndSave();
                ApplyMasterMute();
                ShowToast(_audioMuted ? "Sound off (M)." : "Sound on (M).");
                PlayUiClick();
            }
        }

        private void ToggleMenu()
        {
            _menuOpen = !_menuOpen;
            if (!_menuOpen)
                _optionsOpen = false;
            PlayUiClick();
        }

        private void ApplyLoadedSettings()
        {
            var settings = AirsideSettings.Load();
            _audioMuted = !settings.SoundOn;
            _fieldTagsVisible = settings.FieldTags;
            _miniMapVisible = settings.MiniMap;
            ApplyMasterMute();
        }

        private void ApplySettingsAndSave()
        {
            var settings = AirsideSettings.Current;
            settings.SoundOn = !_audioMuted;
            settings.FieldTags = _fieldTagsVisible;
            settings.MiniMap = _miniMapVisible;
            settings.Save();
        }

        private void ToggleFollow()
        {
            if (_cameraController == null)
                return;
            if (InCockpit) { ExitCockpit(false); return; }

            // Turning follow off hands the camera back where it is — free to orbit,
            // pan and zoom from there. It is not a request to be dragged back to the
            // overview; R does that explicitly.
            if (_cameraController.IsFollowing)
                _cameraController.ReleaseFollow();
            // F is documented as "Follow selected aircraft": with a selection on the field,
            // follow that one instead of whichever aircraft happens to be first in the list.
            else if (string.IsNullOrEmpty(_selectedAircraftId) || !TryFollowFleetAircraft(_selectedAircraftId))
                _cameraController.StartFollowFirst();
            PlayUiClick();
        }

        private void ResetView()
        {
            if (_cameraController == null)
                return;

            if (InCockpit) { ExitCockpit(true); return; }
            ClearAircraftSelection();
            _cameraController.ReturnToOverview();
            PlayUiClick();
        }

        private void RestartCircuit()
        {
            _simulation.RestartCircuit();
            _preciseTime = _clock.Now.ElapsedSeconds;
            _previousPhases.Clear();
            _touchdownFired.Clear();
            _rotateFired.Clear();
            ClearWheelSmoke();
            _menuOpen = false;
            PlayUiClick();
        }

        private void QuitGame()
        {
            PlayUiClick();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnGUI()
        {
            if (_simulation == null)
                return;
            var soakHudStarted = SoakMode ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;

            var scale = HudLayout.ScaleFor(Screen.width, Screen.height);
            var previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            var layout = HudLayout.Create(Screen.width / scale, Screen.height / scale);
            if (IntroActive)
            {
                DrawIntro(layout);
                GUI.matrix = previousMatrix;
                if (SoakMode)
                {
                    _soakHudTicks += System.Diagnostics.Stopwatch.GetTimestamp() - soakHudStarted;
                    _soakHudCalls++;
                }
                return;
            }

            // Built once: OnGUI runs several times a frame and these were new every pass.
            var panel = _hudPanelStyle ??= AirsideTheme.PanelStyle(new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(18, 18, 14, 14)
            });
            var title = _hudTitleStyle ??= AirsideTheme.TextStyle(new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold });
            var button = _hudButtonStyle ??= AirsideTheme.ButtonStyle(
                new GUIStyle(GUI.skin.button) { fontSize = 15, fontStyle = FontStyle.Bold },
                AirsideTheme.InstrumentText);

            // Follow / Overview live on the circuit HUD only. The airline overview
            // uses the selected-aircraft card and Esc/R instead (ADR 0053). The
            // live speed / altitude / heading strip stays up in both modes.
            if (!AirlineModalOpen && !_menuOpen && !InCockpit)
            {
                DrawSpeedReadout(layout, panel);
                if (!FleetMode)
                    DrawControlBar(layout, button);
            }
            if (InCockpit && !_menuOpen) DrawCockpitHud(layout, panel, button);
            else DrawAirlineHud(layout, panel, title, button);
            DrawMapCredit(layout);
            DrawBuildStamp(layout);
            if (_menuOpen && _optionsOpen)
                DrawOptionsMenu(layout, panel, title, button);
            else if (_menuOpen)
                DrawPauseMenu(layout, panel, title, button);

            if (SoakMode)
            {
                _soakHudTicks += System.Diagnostics.Stopwatch.GetTimestamp() - soakHudStarted;
                _soakHudCalls++;
            }
            GUI.matrix = previousMatrix;
        }

        /// <summary>
        /// Live airspeed, read from the same visual progress that places the aircraft,
        /// so the number always agrees with what is on screen rather than with the
        /// simulation a fraction of a second behind it.
        /// </summary>
        private GUIStyle _hudPanelStyle;
        private GUIStyle _hudTitleStyle;
        private GUIStyle _hudButtonStyle;
        private GUIStyle _creditStyle;
        private GUIStyle _creditShadowStyle;

        /// <summary>
        /// ODbL attribution for the OSM-derived airfield layout and coastline, small and
        /// always on screen in the bottom-right corner.
        /// </summary>
        private void DrawMapCredit(HudLayout layout)
        {
            var text = MapAttribution.FieldCredit(usesOsmLayout: true, usesOsmCoast: true,
                usesLiveTraffic: LiveTrafficHealthy, usesLiveWeather: LiveWeatherHealthy,
                usesSatellite: AirsideBareField.Enabled);
            if (string.IsNullOrEmpty(text))
                return;
            _creditStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.LowerRight,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                normal = { textColor = new Color(0.93f, 0.95f, 0.92f, 0.72f) }
            };
            var rect = layout.MapCredit;
            var shadow = _creditShadowStyle ??= new GUIStyle(_creditStyle) { normal = { textColor = new Color(0f, 0f, 0f, 0.55f) } };
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, shadow);
            GUI.Label(rect, text, _creditStyle);
        }

        private GUIStyle _buildStampStyle;
        private GUIStyle _buildStampShadowStyle;

        /// <summary>
        /// Short git stamp on the bottom edge, left of the map credit and under every HUD
        /// panel, so two Macs can be compared without opening the pause menu.
        /// </summary>
        private void DrawBuildStamp(HudLayout layout)
        {
            var text = BuildIdentityReader.Current.ShortLabel;
            if (string.IsNullOrEmpty(text))
                return;
            _buildStampStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.LowerRight,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                clipping = TextClipping.Clip,
                normal = { textColor = new Color(0.93f, 0.95f, 0.92f, 0.72f) }
            };
            var rect = layout.BuildStamp;
            if (rect.width <= 0f)
                return;
            var shadow = _buildStampShadowStyle ??= new GUIStyle(_buildStampStyle)
            {
                normal = { textColor = new Color(0f, 0f, 0f, 0.55f) }
            };
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, shadow);
            GUI.Label(rect, text, _buildStampStyle);
        }

        private void DrawSpeedReadout(HudLayout layout, GUIStyle panel)
        {
            if (!TryReadoutFlight(out var flight, out var view, out var fleetAircraft))
                return;

            // Taxiing fleet aircraft move along the Adelaide ground routes, which the
            // circuit speed schedule knows nothing about, so measure them directly.
            var type = FleetMode && fleetAircraft != null ? fleetAircraft.Type : AircraftType.Atr42;
            var knots = type.IsRotorcraft
                ? CircuitProfile.ToKnots(HelicopterTrack.For(fleetAircraft, _preciseTime).SpeedMetresPerSecond)
                : flight != null && FleetGroundSpeed(flight) is { } groundSpeed
                ? CircuitProfile.ToKnots(groundSpeed)
                : flight != null
                    ? AirsideFlightPath.AirspeedKnots(flight.Operation.Phase, VisualPhaseProgress(flight, 0f), type)
                    : 0f;

            // Once an airline is running, several aircraft share the field (the player's
            // and every AI carrier's) — with no callsign shown, this box read as an
            // unexplained, seemingly random speed with no indication whose it was.
            var label = FleetMode && fleetAircraft != null ? FlightNumber.OrRegistration(fleetAircraft) : null;

            var rect = SpeedReadoutRect(layout);
            if (rect.width <= 0f)
                return;
            GUI.Box(rect, GUIContent.none, panel);
            GUI.Label(rect, ReadoutText(knots, view, label), _speedReadoutStyle ??= SpeedReadoutStyle());
        }

        private Transform _readoutView;
        private float _readoutLastHeight;
        private float _readoutLastTime;
        private float _readoutVerticalFpm;

        /// <summary>
        /// Speed, plus height above the field and vertical speed once airborne — read off the
        /// aircraft as drawn, so the numbers are the motion on screen. Below 10 ft it is on
        /// the wheels and only speed shows. <paramref name="label"/> is the flight number or
        /// registration this reading is for — with several airlines sharing the field, an
        /// unlabelled number gave no way to tell whose it was, or that it might not even be
        /// the player's own aircraft.
        /// </summary>
        private string ReadoutText(float knots, Transform view, string label = null)
        {
            var prefix = string.IsNullOrEmpty(label) ? string.Empty : $"{label}  ";
            var speed = $"{prefix}{Mathf.RoundToInt(knots)} kt";
            if (view == null)
                return speed;

            var heightMetres = Mathf.Max(0f, view.position.y - AirsideFlightPath.GroundY);
            var now = Time.unscaledTime;
            if (view != _readoutView)
            {
                _readoutView = view;
                _readoutVerticalFpm = 0f;
            }
            else if (now - _readoutLastTime > 0.0001f)
            {
                var fpm = (heightMetres - _readoutLastHeight) / (now - _readoutLastTime) * 196.85f;
                _readoutVerticalFpm = Mathf.Lerp(_readoutVerticalFpm, fpm, 1f - Mathf.Exp(-(now - _readoutLastTime) * 3f));
            }
            _readoutLastHeight = heightMetres;
            _readoutLastTime = now;

            var heading = Mathf.RoundToInt(RunwayWeather.TrueFromUnityYaw(view.eulerAngles.y));
            if (heading >= 360)
                heading -= 360;
            var feet = heightMetres * 3.28084f;
            if (feet < 10f)
                return $"{speed}  ·  HDG {heading:000}";
            var arrow = _readoutVerticalFpm > 150f ? " ▲" : _readoutVerticalFpm < -150f ? " ▼" : string.Empty;
            return $"{speed}  ·  {Mathf.RoundToInt(feet / 10f) * 10:#,0} ft{arrow}  ·  HDG {heading:000}";
        }

        /// <summary>
        /// The aircraft the readout describes: only the one being followed or
        /// explicitly selected. The operations card may still highlight a
        /// priority aircraft; this strip must not, and it must not fall back to
        /// the first visible plane on the field.
        /// </summary>
        private bool TryReadoutFlight(out CommercialFlight flight, out Transform view, out FleetAircraft fleetAircraft)
        {
            flight = null;
            view = null;
            fleetAircraft = WatchedAircraft();
            return TryReadoutFor(fleetAircraft, out flight, out view);
        }

        private bool TryReadoutFor(FleetAircraft aircraft, out CommercialFlight flight, out Transform view)
        {
            flight = null;
            view = null;
            if (aircraft == null || !_fleetViewById.TryGetValue(aircraft.Registration, out view)
                || view == null || !view.gameObject.activeSelf)
                return false;

            var flights = VisualFlights;
            for (var i = 0; i < flights.Count; i++)
            {
                if (flights[i].AircraftId != aircraft.Registration)
                    continue;
                flight = flights[i];
                return true;
            }

            return true;
        }

        private GUIStyle _speedReadoutStyle;

        private static GUIStyle SpeedReadoutStyle() =>
            AirsideTheme.TextStyle(
                new GUIStyle(GUI.skin.label)
                {
                    fontSize = 17,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                },
                AirsideTheme.Aqua);

        private void DrawControlBar(HudLayout layout, GUIStyle button)
        {
            var following = _cameraController != null && _cameraController.IsFollowing;

            if (GUI.Button(layout.ButtonAt(0),
                    new GUIContent(following ? "Follow on" : "Follow", AirsideTheme.SystemIcon("follow")), button))
                ToggleFollow();
            if (GUI.Button(layout.ButtonAt(1),
                    new GUIContent("Overview", AirsideTheme.SystemIcon("overview")), button))
                ResetView();
        }

        private void DrawPauseMenu(HudLayout layout, GUIStyle panel, GUIStyle title, GUIStyle button)
        {
            var rect = layout.PauseMenu;
            GUI.Box(rect, GUIContent.none, panel);
            // Live Adelaide time never pauses (ADR 0045), so the panel must not claim to.
            GUI.Label(new Rect(rect.x + 20f, rect.y + 16f, rect.width - 40f, 30f), "Adelaide Airport", title);

            var row = new Rect(rect.x + 20f, rect.y + 62f, rect.width - 40f, 42f);
            if (GUI.Button(row, new GUIContent("Resume", AirsideTheme.SystemIcon("play")), button))
                ToggleMenu();

            row.y += 52f;
            if (GUI.Button(row, "Options", button))
            {
                _optionsOpen = true;
                PlayUiClick();
            }

            row.y += 52f;
            // Once an airline runs the field draws the fleets, not the demo circuit, so a
            // restart there silently reset an aircraft nobody can see.
            if (!FleetMode)
            {
                if (GUI.Button(row, "Restart circuit", button))
                    RestartCircuit();
                row.y += 52f;
            }

            if (GUI.Button(row, "Quit", button))
                QuitGame();

            var stamp = BuildIdentityReader.Current.FullLabel;
            if (!string.IsNullOrEmpty(stamp))
            {
                _pauseStampStyle ??= new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    wordWrap = true,
                    alignment = TextAnchor.UpperLeft,
                    normal = { textColor = new Color(0.93f, 0.95f, 0.92f, 0.78f) }
                };
                GUI.Label(new Rect(rect.x + 20f, rect.y + rect.height - 80f, rect.width - 40f, 64f), stamp, _pauseStampStyle);
            }
        }

        private GUIStyle _pauseStampStyle;
        private GUIStyle _optionsNoteStyle;

        private void DrawOptionsMenu(HudLayout layout, GUIStyle panel, GUIStyle title, GUIStyle button)
        {
            var rect = layout.OptionsMenu;
            GUI.Box(rect, GUIContent.none, panel);
            GUI.Label(new Rect(rect.x + 20f, rect.y + 16f, rect.width - 40f, 30f), "Options", title);

            var settings = AirsideSettings.Current;
            var row = new Rect(rect.x + 20f, rect.y + 62f, rect.width - 40f, 38f);
            if (GUI.Button(row, settings.SoundOn ? "Sound  ·  On" : "Sound  ·  Off", button))
            {
                _audioMuted = !_audioMuted;
                ApplySettingsAndSave();
                ApplyMasterMute();
                PlayUiClick();
            }

            row.y += 46f;
            if (GUI.Button(row, settings.FieldTags ? "Aircraft tags  ·  On" : "Aircraft tags  ·  Off", button))
            {
                _fieldTagsVisible = !settings.FieldTags;
                ApplySettingsAndSave();
                PlayUiClick();
            }

            row.y += 46f;
            if (GUI.Button(row, settings.MiniMap ? "Airport map  ·  On" : "Airport map  ·  Off", button))
            {
                _miniMapVisible = !settings.MiniMap;
                ApplySettingsAndSave();
                PlayUiClick();
            }

            row.y += 46f;
            if (GUI.Button(row, settings.FollowOnSelect ? "Follow on select  ·  On" : "Follow on select  ·  Off", button))
            {
                settings.FollowOnSelect = !settings.FollowOnSelect;
                settings.Save();
                PlayUiClick();
            }

            row.y += 46f;
            if (GUI.Button(row, settings.InvertOrbit ? "Invert orbit  ·  On" : "Invert orbit  ·  Off", button))
            {
                settings.InvertOrbit = !settings.InvertOrbit;
                settings.Save();
                PlayUiClick();
            }

            row.y += 46f;
            if (GUI.Button(row, $"Camera speed  ·  {AirsideSettings.CameraSpeedLabels[settings.CameraSpeedIndex]}", button))
            {
                settings.CycleCameraSpeed().Save();
                PlayUiClick();
            }

            row.y += 46f;
            if (GUI.Button(row, $"Night brightness  ·  {NightVisibility.Labels[NightVisibility.Clamp(settings.NightBrightness)]}", button))
            {
                settings.CycleNightBrightness().Save();
                PlayUiClick();
            }

            row.y += 46f;
            if (GUI.Button(row, $"Live Adelaide sky traffic  ·  {LiveTrafficStatus}", button))
            {
                settings.LiveTraffic = !settings.LiveTraffic;
                settings.Save();
                PlayUiClick();
            }

            row.y += 46f;
            if (GUI.Button(row, $"Live Adelaide weather  ·  {LiveWeatherStatus}", button))
            {
                settings.LiveWeather = !settings.LiveWeather;
                settings.Save();
                if (settings.LiveWeather)
                    _nextLiveWeatherPollAt = 0f;
                PlayUiClick();
            }

            row.y += 46f;
            if (GUI.Button(row, settings.UncappedFrameRate ? "Frame rate  ·  Display max" : "Frame rate  ·  60 fps", button))
            {
                settings.UncappedFrameRate = !settings.UncappedFrameRate;
                settings.Save();
                AirsideFramePacing.Apply(settings.UncappedFrameRate, SoakMode);
                PlayUiClick();
            }

            // Graphics tests (ADR 0155): each switches one heavier effect off to find a slowdown.
            row.y += 50f;
            GUI.Label(new Rect(row.x, row.y, row.width, 24f), "Graphics tests  ·  turn one off to compare", _optionsNoteStyle ??=
                AirsideTheme.TextStyle(new GUIStyle(GUI.skin.label) { fontSize = 13 }));
            row.y += 28f;
            if (GUI.Button(row, settings.WeatherLayers ? "Weather layers  ·  On" : "Weather layers  ·  Off", button))
            {
                settings.WeatherLayers = !settings.WeatherLayers;
                settings.Save();
                PlayUiClick();
            }

            row.y += 46f;
            if (GUI.Button(row, settings.PropellerBlur ? "Propeller blur  ·  On" : "Propeller blur  ·  Off", button))
            {
                settings.PropellerBlur = !settings.PropellerBlur;
                settings.Save();
                PlayUiClick();
            }

            row.y += 46f;
            if (GUI.Button(row, settings.DistantGlows ? "Distant aircraft glow  ·  On" : "Distant aircraft glow  ·  Off", button))
            {
                settings.DistantGlows = !settings.DistantGlows;
                settings.Save();
                PlayUiClick();
            }

            row.y += 46f;
            if (GUI.Button(row, settings.AircraftLights ? "Aircraft lights  ·  On" : "Aircraft lights  ·  Off", button))
            {
                settings.AircraftLights = !settings.AircraftLights;
                settings.Save();
                PlayUiClick();
            }

            row.y += 46f;
            if (GUI.Button(row, settings.SuburbBuildings ? "Suburbs and trees  ·  On" : "Suburbs and trees  ·  Off (next launch)", button))
            {
                settings.SuburbBuildings = !settings.SuburbBuildings;
                settings.Save();
                PlayUiClick();
            }

            row.y += 56f;
            if (GUI.Button(row, "Back", button))
            {
                _optionsOpen = false;
                PlayUiClick();
            }
        }

        private static float PhasePitchDegrees(AircraftPhase phase, float progress) =>
            AirsideFlightPath.PitchDegrees(phase, progress);

        private static float TurnBankDegrees(Transform view, Quaternion targetRotation, AircraftPhase phase)
        {
            // Ground phases stay wings-level so wingtips do not dig into the apron.
            if (phase is AircraftPhase.TaxiIn or AircraftPhase.TaxiOut or AircraftPhase.Pushback
                or AircraftPhase.Landing or AircraftPhase.AtStand)
                return 0f;

            var yawDelta = Mathf.DeltaAngle(view.eulerAngles.y, targetRotation.eulerAngles.y);
            var limit = phase is AircraftPhase.Takeoff or AircraftPhase.Departed ? 24f : 16f;
            return Mathf.Clamp(-yawDelta * 2.8f, -limit, limit);
        }

        /// <summary>
        /// Coordinated bank from the SID heading change, so the wings roll with the
        /// turn instead of waiting on the damped pose to catch up.
        /// </summary>
        private float DepartureBankDegrees(CommercialFlight flight, AircraftPhase phase, float progress)
        {
            // Rolled in, held at a normal 25° and rolled out with the arc itself (ADR 0166).
            return TryDepartureArc(flight, phase, progress, out _, out var along, out var relative, out var radius)
                ? DepartureTurn.ArcBank(relative, radius, along)
                : 0f;
        }

        /// <summary>
        /// Roll in and out of a turn instead of snapping to the instantaneous yaw error.
        /// The raw value jitters frame to frame because it is a difference of two poses
        /// that are themselves being damped, which made the wings twitch on every turn.
        /// </summary>
        private float SmoothedBankDegrees(string aircraftId, Transform view, Quaternion heading, AircraftPhase phase,
            float commandedBank = 0f)
        {
            var visual = TurnBankDegrees(view, heading, phase);
            var target = Mathf.Abs(commandedBank) > 0.4f ? commandedBank : visual;
            if (!_bankDegrees.TryGetValue(aircraftId, out var current))
                current = target;

            var dt = PresentationDeltaTime;
            // Roll in a little slower than the aircraft rolls out — matches how a turn reads.
            var rate = Mathf.Abs(target) > Mathf.Abs(current) ? 2.2f : 3.2f;
            current = Mathf.Lerp(current, target, AirsideFlightPath.DampFactor(rate, dt));
            _bankDegrees[aircraftId] = current;
            return current;
        }

        private void ApplyMasterMute()
        {
            AudioListener.volume = _audioMuted ? 0f : 1f;
            AudioListener.pause = _audioMuted;
            if (_audioMuted)
                foreach (var emitter in _engineAudio.Values)
                    if (emitter != null)
                        emitter.StopVoices();
        }

        private void PlayUiClick() => PlayMoment(ref _uiClickClip, HudSounds.UiClick, "UI click", 0.7f);

        private void UpdateAmbientAudio()
        {
            if (_ambientWindAudio == null || _ambientRainAudio == null)
                return;

            EnsureAmbientClips();

            var weather = CurrentWeather;
            var raining = weather == WeatherKind.Rain || weather == WeatherKind.Storm;
            var storm = weather == WeatherKind.Storm;
            var windTarget = _audioMuted ? 0f : AmbientWindVolume * AmbientDuck;
            var rainTarget = _audioMuted || !raining ? 0f : (storm ? AmbientStormVolume : AmbientRainVolume) * AmbientDuck;
            var coastTarget = _audioMuted || AirsideFocusMode.BareWorld ? 0f
                : AmbientCoastVolume * (storm ? 1.45f : raining ? 1.2f : 1f) * AmbientDuck;
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

            // ADR 0059: the strike already fixed its own moment and delay (Update()); this
            // only fires the one-shot once real time actually reaches it, so pausing or a
            // slow frame delays thunder along with everything else instead of it arriving
            // early. The clip itself is pre-warmed in EnsureAmbientClips — synthesising it
            // here, on the first storm's first strike, cost a synchronous ~53k-sample
            // generation loop at exactly the moment the clap needed to play on time.
            if (_thunderAudio != null && Time.unscaledTime >= _thunderPlayAt)
            {
                _thunderPlayAt = float.PositiveInfinity;
                if (!_audioMuted && _thunderClip != null)
                {
                    var volume = Mathf.Lerp(0.55f, 0.16f, _lightningDistance01);
                    _thunderAudio.pitch = Mathf.Lerp(0.92f, 1.05f, 1f - _lightningDistance01);
                    _thunderAudio.PlayOneShot(_thunderClip, volume);
                }
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

            // ADR 0059: a one-shot, so no clip/Play() call here — just pre-generate it now,
            // the same first few frames the other ambient beds warm up, instead of paying the
            // synthesis cost mid-storm on whichever frame the first strike actually lands.
            _thunderClip ??= Resources.Load<AudioClip>("Airside/Audio/thunder_crack_01") ?? CreateThunderClip();
        }

        private const string LampPivotName = "Lamp pivot";

        private static void UpdateCabinDoor(CabinDoorPart[] parts, AircraftPhase phase, EngineState? engines = null)
        {
            // Fleet aircraft: the door positions come straight from the departure countdown and
            // the deplaning/boarding windows (ADR 0177) — a deterministic 0..1 per door that
            // already eases over the door's own time, so they are right at any game speed and
            // hold still when paused. The demo circuit keeps the old open-at-stand swing.
            float passenger, cargo;
            var timed = engines.HasValue;
            if (timed)
            {
                passenger = engines.Value.PassengerDoor;
                cargo = engines.Value.CargoDoor;
            }
            else
            {
                passenger = cargo = AirsideReusableMotion.CabinDoorBias(phase);
            }

            for (var i = 0; i < parts.Length; i++)
            {
                var child = parts[i].Transform;
                if (child == null)
                    continue;
                var euler = child.localEulerAngles;
                switch (parts[i].Kind)
                {
                    case CabinDoorKind.Airstair:
                    {
                        // Saab / Dash 8 / ATR airstair door (ADR 0114): hinged at the sill, it
                        // folds down and out until its steps rest on the apron.
                        var target = Mathf.Lerp(0f, parts[i].OpenDegrees, passenger);
                        ShowDoorway(parts[i].Doorway, passenger);
                        euler.z = timed ? target : Mathf.MoveTowards(Signed(euler.z), target, Time.unscaledDeltaTime * 55f);
                        break;
                    }
                    case CabinDoorKind.Cabin:
                    {
                        // A jet's plug door swings out and round against the fuselage.
                        var target = Mathf.Lerp(0f, -85f, passenger);
                        ShowDoorway(parts[i].Doorway, passenger);
                        euler.y = timed ? target : Mathf.MoveTowards(Signed(euler.y), target, Time.unscaledDeltaTime * 120f);
                        break;
                    }
                    default:
                    {
                        var target = Mathf.Lerp(0f, 70f, cargo);
                        ShowDoorway(parts[i].Doorway, cargo);
                        euler.y = timed ? target : Mathf.MoveTowards(Signed(euler.y), target, Time.unscaledDeltaTime * 100f);
                        break;
                    }
                }

                child.localEulerAngles = euler;
            }

            static float Signed(float degrees) => degrees > 180f ? degrees - 360f : degrees;
        }

        /// <summary>Sim-rate presentation dt — freezes when paused, scales with the selected rate.</summary>
        private float PresentationDeltaTime => Time.unscaledDeltaTime;

        private static readonly Dictionary<int, int> JetFanBladeCounts = new();

        /// <summary>Blade counts per propeller, read from the model when its disc is built.</summary>
        private static readonly Dictionary<int, int> PropBladeCounts = new();

        private double _propClockSeen = double.NaN;
        private int _propClockFrame = -1;
        private float _propDelta;

        /// <summary>
        /// Real seconds for propeller motion this frame, or 0 while the game is paused (the presentation
        /// clock did not move). Props used to keep spinning on unscaled time through a pause.
        /// </summary>
        private float PropDeltaTime
        {
            get
            {
                if (_propClockFrame == Time.frameCount)
                    return _propDelta;
                _propClockFrame = Time.frameCount;
                var moved = double.IsNaN(_propClockSeen) || _preciseTime != _propClockSeen;
                _propClockSeen = _preciseTime;
                _propDelta = moved ? Time.unscaledDeltaTime : 0f;
                return _propDelta;
            }
        }

        /// <summary>
        /// How much of a blur disc the camera can see. Face-on it is the whole propeller; edge-on
        /// there is almost nothing in the line of sight, so a real disc nearly vanishes.
        /// </summary>
        private float DiscViewFade(Transform hub)
        {
            if (_mainCamera == null)
                return 1f;
            var toCamera = _mainCamera.transform.position - hub.position;
            var distanceSquared = toCamera.sqrMagnitude;
            if (distanceSquared < 0.0001f)
                return 1f;
            return AirsidePropellerDynamics.DiscViewFade(
                Vector3.Dot(hub.forward, toCamera / Mathf.Sqrt(distanceSquared)));
        }

        /// <summary>
        /// Rotate each blade about its own radial axis. ADR 0151: the blade meshes are lofted with
        /// their twist already in them, so this applies the difference from that authored pitch —
        /// feathered when the engine is stopped, flat for a start, coarsening with power, negative
        /// in reverse. The radial axis of each blade is resolved once per model.
        /// </summary>
        private void ApplyBladePitch(Transform propeller, float offsetDegrees)
        {
            var id = propeller.GetInstanceID();
            if (!_propBladeRigs.TryGetValue(id, out var rig))
            {
                rig = BuildBladeRig(propeller);
                _propBladeRigs[id] = rig;
            }

            if (rig.Length == 0)
                return;
            if (_propBladePitch.TryGetValue(id, out var applied)
                && Mathf.Abs(applied - offsetDegrees) < 0.05f)
                return;
            _propBladePitch[id] = offsetDegrees;

            for (var i = 0; i < rig.Length; i++)
            {
                var blade = rig[i].Transform;
                if (blade == null)
                    continue;
                blade.localRotation = Quaternion.AngleAxis(offsetDegrees, rig[i].RadialAxis) * rig[i].BaseRotation;
            }
        }

        private static PropBladePart[] BuildBladeRig(Transform propeller)
        {
            var parts = new List<PropBladePart>();
            for (var i = 0; i < propeller.childCount; i++)
            {
                var child = propeller.GetChild(i);
                var name = child.name;
                // The nested rig names blades "Blade"/"Blade 2"… and their painted ends "Tip"/"Tip 2"…
                if (!name.StartsWith("Blade", StringComparison.Ordinal)
                    && !name.StartsWith("Tip", StringComparison.Ordinal))
                    continue;
                if (TryBladeRadialAxis(child, out var radial))
                    parts.Add(new PropBladePart(child, child.localRotation, radial));
            }

            return parts.Count == 0 ? Array.Empty<PropBladePart>() : parts.ToArray();
        }

        /// <summary>
        /// The radial axis a blade pitches about, in the propeller's own space. The authored
        /// aircraft bake each blade's vertices out along the blade, so the mesh centre gives the
        /// direction; the primitive fallback kit centres its box on the hub, so there the longest
        /// scaled axis of the box is the blade. Anything that resolves along the spin axis is not
        /// a blade and is left alone.
        /// </summary>
        private static bool TryBladeRadialAxis(Transform blade, out Vector3 radial)
        {
            radial = Vector3.right;
            var filter = blade.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
                return false;

            var bounds = filter.sharedMesh.bounds;
            var scale = blade.localScale;
            var candidate = blade.localRotation * Vector3.Scale(bounds.center, scale);
            if (candidate.sqrMagnitude < 0.0225f)
            {
                var extents = Vector3.Scale(bounds.extents, scale);
                if (extents.magnitude < 0.02f)
                    return false;
                var axis = extents.x >= extents.y && extents.x >= extents.z ? Vector3.right
                    : extents.y >= extents.z ? Vector3.up
                    : Vector3.forward;
                candidate = blade.localRotation * axis;
            }

            // Pitch is about the radius, so drop any component along the spin axis (local Z).
            candidate.z = 0f;
            if (candidate.sqrMagnitude < 1e-4f)
                return false;
            radial = candidate.normalized;
            return true;
        }

        private readonly struct PropBladePart
        {
            public readonly Transform Transform;
            public readonly Quaternion BaseRotation;
            public readonly Vector3 RadialAxis;

            public PropBladePart(Transform transform, Quaternion baseRotation, Vector3 radialAxis)
            {
                Transform = transform;
                BaseRotation = baseRotation;
                RadialAxis = radialAxis;
            }
        }

        /// <summary>Blades and their pitch axes per propeller, resolved once per model.</summary>
        private readonly Dictionary<int, PropBladePart[]> _propBladeRigs = new();

        /// <summary>The pitch already written to each propeller, so a held angle costs nothing.</summary>
        private readonly Dictionary<int, float> _propBladePitch = new();

        private static int CountBlades(Transform propeller)
        {
            var count = 0;
            foreach (var renderer in propeller.GetComponentsInChildren<Renderer>(true))
                if (renderer != null && renderer.name.IndexOf("blade", StringComparison.OrdinalIgnoreCase) >= 0)
                    count++;
            return count >= 2 && count <= 8 ? count : 4;
        }

        private static readonly Dictionary<int, Material> PropBlurMaterials = new();

        private static readonly Dictionary<int, Material> JetFanBlurMaterials = new();

        private static Material BlurDiscMaterial(Dictionary<int, Material> cache, int key, string textureName,
            string materialName, Color fallback, Func<float, float> alphaAt, Func<float, Color> tintAt)
        {
            if (cache.TryGetValue(key, out var cached) && cached != null)
                return cached;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                return AirsideMaterialLibrary.CreateShared(fallback, AirsideMaterialLibrary.SurfaceKind.Glass);
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = textureName, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var dx = (x + 0.5f) / size * 2f - 1f;
                var dy = (y + 0.5f) / size * 2f - 1f;
                var r = Mathf.Sqrt(dx * dx + dy * dy);
                var alpha = alphaAt(r);
                Color32 tint = tintAt(r);
                pixels[y * size + x] = new Color32(tint.r, tint.g, tint.b, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f));
            }

            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            var material = new Material(shader) { name = materialName };
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", fallback);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            cache[key] = material;
            return material;
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

        private string[] _commercialAircraftIds = Array.Empty<string>();
        private readonly Dictionary<string, Transform> _syncViewsById = new();
        private readonly HashSet<string> _syncLiveIds = new();
        private readonly List<string> _syncStaleSlots = new();
        private readonly HashSet<Transform> _syncKept = new();
        // Tracks slots already handed out so a new one is found in O(1) instead of
        // rescanning every current assignment for every candidate slot.
        private readonly HashSet<int> _syncUsedSlots = new();
        private Transform[] _syncNextViews = Array.Empty<Transform>();
        private string[] _syncNextIds = Array.Empty<string>();

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
            if (TryFleetGroundProgress(flight, lookAheadSeconds, out var groundProgress))
                return groundProgress;

            var operation = flight.Operation;
            var phase = operation.Phase;
            if (FleetMode && _fleetAircraftById.TryGetValue(flight.AircraftId, out var holding)
                && holding.State is FleetState.HoldingForLanding or FleetState.Inbound
                && phase == AircraftPhase.Approach)
            {
                var pinned = (float)ApproachHold.HoldingFinalProgress(
                    FleetVisual.QueueSlot(_operations.Fleet, holding));
                // A tiny look-ahead along final so facing is not frozen on the last rotation.
                if (lookAheadSeconds > 0f)
                    return Mathf.Min(0.98f, pinned + 0.008f);
                return pinned;
            }

            if (phase == AircraftPhase.Circuit)
            {
                var slot = 0;
                if (FleetMode && _fleetAircraftById.TryGetValue(flight.AircraftId, out var circuit))
                    slot = FleetVisual.QueueSlot(_operations.Fleet, circuit);
                return (float)CircuitTraffic.HoldingProgress(_preciseTime + lookAheadSeconds, slot);
            }

            if (phase == AircraftPhase.GoAround)
            {
                var elapsed = _preciseTime - operation.PhaseStartedAt.ElapsedSeconds + lookAheadSeconds;
                return (float)CircuitTraffic.GoAroundProgress(elapsed);
            }

            var type = FleetMode && _fleetAircraftById.TryGetValue(flight.AircraftId, out var aircraft)
                ? aircraft.Type : AircraftType.Atr42;
            var duration = AirsideFlightPath.PhaseSeconds(phase, type);
            var progress = PhaseProgressNow(flight, duration);

            if (lookAheadSeconds <= 0f || duration <= 0f)
                return progress;

            return Mathf.Clamp01(progress + lookAheadSeconds / duration);
        }

        private float PhaseProgressNow(CommercialFlight flight, float duration)
        {
            var operation = flight.Operation;

            return AirsideAircraftMotion.PhaseProgress(
                _preciseTime, operation.PhaseStartedAt.ElapsedSeconds, duration);
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

        private static void ResetServiceLoopParts(Transform vehicle)
        {
            if (vehicle == null)
                return;

            var namedChildren13 = AirsideNamedChildren.Get(vehicle);
            var childNames13 = AirsideNamedChildren.Names(vehicle);
            for (var childIndex13 = 0; childIndex13 < namedChildren13.Length; childIndex13++)
            {
                var child = namedChildren13[childIndex13];
                var childName = childNames13[childIndex13];
                if (child == vehicle)
                    continue;
                // Exact names only — densified Hose reel/guard/tray and Cargo tags must not reset.
                if (childName == "Hose")
                {
                    child.localScale = new Vector3(0.12f, 0.12f, 0.4f);
                    child.localPosition = new Vector3(child.localPosition.x, child.localPosition.y, 0.4f);
                }
                else if (childName == "Cargo")
                {
                    var pos = child.localPosition;
                    pos.y = 0.35f;
                    child.localPosition = pos;
                }
                else if (childName == "Door")
                {
                    child.localEulerAngles = Vector3.zero;
                }
            }
        }

        private static void AnimateServiceLoops(Transform vehicle, bool active, string partPrefix)
        {
            if (!active || vehicle == null || !vehicle.gameObject.activeSelf)
                return;

            var namedChildren14 = AirsideNamedChildren.Get(vehicle);
            var childNames14 = AirsideNamedChildren.Names(vehicle);
            for (var childIndex14 = 0; childIndex14 < namedChildren14.Length; childIndex14++)
            {
                var child = namedChildren14[childIndex14];
                var childName = childNames14[childIndex14];
                // Exact part names only so densified accessories (Hose reel, Cargo tag) stay put.
                // Cargo bags nest under Cargo so they bob with the crate (0025 item 7).
                if (child == vehicle || childName != partPrefix)
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

        private static readonly Color WheelSmokeColor = new Color(0.82f, 0.80f, 0.78f, 0.55f);

        /// <summary>Ground speed at which tyre smoke reads as full strength.</summary>
        private const float TouchdownSmokeReferenceSpeed = 45f;

        private float _weatherGloom;
        private bool _weatherGloomReady;

        /// <summary>The current daylight value (0 night, 1 day), updated once per frame here
        /// so other Presentation types that are not <see cref="AirsidePrototype"/> itself —
        /// e.g. <see cref="AircraftIdentitySideVisibility"/> tinting fuselage titles with the
        /// same day/night grade as the rest of the airframe — can read it without needing
        /// access to a private instance member.</summary>
        internal static float CurrentDaylight { get; private set; }

        // Per-light REIL flags for _alsLights / _runwayEdgeLights: 0 not a REIL, 1 left, 2 right.
        // Resolved once — reading Light.name every frame allocated a string per lamp.
        private byte[] _alsReilSide;
        private byte[] _runwayEdgeReilSide;

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

        /// <summary>Deterministic 32-bit FNV-1a over the UTF-16 code units of <paramref name="text"/>.
        /// The implementation lives in <see cref="StableHash"/>, which is UnityEngine-free so
        /// the headless harness gets the same numbers a player build does.</summary>
        internal static uint StableNameHash(string text) => StableHash.Of(text);

        private enum ApronBodyPartKind { Torso, Wand, Arm, Leg }

        private readonly struct ApronBodyPart
        {
            public readonly Transform Transform;
            public readonly ApronBodyPartKind Kind;
            public readonly bool IsLeft;

            public ApronBodyPart(Transform transform, ApronBodyPartKind kind, bool isLeft)
            {
                Transform = transform;
                Kind = kind;
                IsLeft = isLeft;
            }
        }

        private readonly Dictionary<int, ApronBodyPart[]> _apronBodyParts = new();

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
                    DestroyPresentationObject(root.gameObject);
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
            DestroyPresentationObject(dome.GetComponent<Collider>());
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

        private const float StarDistanceScale = 4f;

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

        public const int AdelaideCloudClusterCount = 16;

        private static void PlaceContactShadow(string name, Vector3 position, Vector3 scale, float alpha)
        {
            var shadow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shadow.name = name;
            DestroyPresentationObject(shadow.GetComponent<Collider>());
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

        private void CollectCoastalMotionTargets()
        {
            _coastBoats.Clear();
            _coastFoamLayers.Clear();
            _coastFoamRenderers.Clear();
            _coastWaterRenderers.Clear();
            _coastWaterIsShallows.Clear();
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
                    _coastWaterIsShallows.Add(n.IndexOf("shallow", StringComparison.OrdinalIgnoreCase) >= 0);
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

        /// <summary>How close an aircraft has to be for the hangar to open for it.</summary>
        public const float HangarDoorOpensWithinMetres = 110f;

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
                if (_coastWaterIsShallows[i])
                {
                    var p = renderer.transform.position;
                    p.y = -0.35f + Mathf.Sin(t * 0.65f + i) * 0.03f;
                    renderer.transform.position = p;
                }
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
                DestroyPresentationObject(body.GetComponent<Collider>());
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
                _birdPhaseSeed = new float[nBirds];
                for (var i = 0; i < nBirds; i++)
                {
                    var bird = _birdFlockRoot.GetChild(i);
                    // The builder stores each bird's orbit offset in its roll. Read it once:
                    // the loop below turns birds with LookRotation (zero roll), so reading it
                    // every frame decayed the offsets, and birds jittered and bunched up.
                    _birdPhaseSeed[i] = bird.localEulerAngles.z * Mathf.Deg2Rad;
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
                var phase = _birdPhaseSeed[i] + t + i * 0.35f;
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

        /// <summary>AIR-007 original, unbranded Saab 340B-class turboprop.</summary>
        private static Transform BuildSaab340(
            string name,
            Color accent,
            string liveryDecalRelativePath = null)
        {
            var profile = AircraftVisualProfiles.Saab340;
            var root = new GameObject(name).transform;
            AircraftVisualProfileComponent.Ensure(root, profile);

            var usedArt = ArtPresentationLoader.TryInstantiate(
                profile.ArtRelativePath,
                root,
                out _,
                RenameAircraftPart,
                kitName => Saab340PartColor(kitName, accent),
                localPosition: new Vector3(0f, profile.ModelGroundOffsetMetres, 0f));

            if (usedArt)
            {
                NestCrossPropellerBlades(root);
                RebakePropellerPivots(root);
                RebakeAircraftArticulatedPivots(root);
                NestLandingGearParts(root);
                RebakeWheelPivots(root);
                RigLandingGearArticulation(root);
                NestCabinDoorParts(root);
                AttachDoorways(root);
                ConvertToAirstairDoor(root);
                NestFlapParts(root);
                NestWingMountedParts(root);
                AirsideAircraftRenderBatcher.CombineStaticGlazing(root);
                EnsureAircraftLod(root);
            }
            else
            {
                // True-scale primitive fallback: compact low-wing, conventional tail,
                // four-blade props and nacelle-mounted mains if the art kit is missing.
                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "Fuselage";
                body.transform.SetParent(root, false);
                body.transform.localPosition = new Vector3(0f, 2.0f, 0f);
                body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                body.transform.localScale = new Vector3(1.14f, 9.7f, 1.14f);
                body.GetComponent<Renderer>().sharedMaterial = CreateMaterial(new Color(0.93f, 0.95f, 0.97f));
                ParentBlock(root, "Livery stripe", new Vector3(0f, 1.78f, 0.4f), new Vector3(2.3f, 0.12f, 13.5f), accent);
                ParentBlock(root, "Wing L", new Vector3(-5.5f, 1.95f, 0.4f), new Vector3(10.2f, 0.22f, 2.2f), accent);
                ParentBlock(root, "Wing R", new Vector3(5.5f, 1.95f, 0.4f), new Vector3(10.2f, 0.22f, 2.2f), accent);
                ParentBlock(root, "Engine L", new Vector3(-3.55f, 1.65f, 0.3f), new Vector3(1.15f, 1.05f, 5.2f), accent * 0.72f);
                ParentBlock(root, "Engine R", new Vector3(3.55f, 1.65f, 0.3f), new Vector3(1.15f, 1.05f, 5.2f), accent * 0.72f);
                ParentBlock(root, "Propeller L", new Vector3(-3.55f, 1.74f, 2.95f), new Vector3(0.12f, 3.2f, 0.2f), new Color(0.2f, 0.2f, 0.22f));
                ParentBlock(root, "Propeller R", new Vector3(3.55f, 1.74f, 2.95f), new Vector3(0.12f, 3.2f, 0.2f), new Color(0.2f, 0.2f, 0.22f));
                ParentBlock(root, "Tail", new Vector3(0f, 4.5f, -7.6f), new Vector3(0.28f, 3.8f, 2.8f), accent);
                ParentBlock(root, "Tailplane", new Vector3(0f, 4.8f, -8.2f), new Vector3(9.0f, 0.18f, 1.6f), accent);
                ParentBlock(root, "Gear nose", new Vector3(0f, 0.75f, 7.15f), new Vector3(0.16f, 1.0f, 0.18f), new Color(0.25f, 0.25f, 0.28f));
                ParentBlock(root, "Gear L", new Vector3(-3.55f, 0.95f, -0.55f), new Vector3(0.18f, 1.5f, 0.26f), new Color(0.25f, 0.25f, 0.28f));
                ParentBlock(root, "Gear R", new Vector3(3.55f, 0.95f, -0.55f), new Vector3(0.18f, 1.5f, 0.26f), new Color(0.25f, 0.25f, 0.28f));
                ParentBlock(root, "CabinDoor", new Vector3(-1.16f, 1.95f, 6.55f), new Vector3(0.06f, 1.3f, 0.65f), new Color(0.78f, 0.8f, 0.83f));
            }

            ApplyLiveryDecal(root, liveryDecalRelativePath);
            // Blur discs: ApplyPropBlurToHub hides the blades above 1 000 RPM and shows the
            // disc instead. Without one the Saab's propellers vanished on takeoff and approach.
            EnsurePropDiscs(root);
            EnsureGroundShadow(root);
            if (!HasNamedChild(root, "NavLight L"))
                ParentBlock(root, "NavLight L", new Vector3(-10.68f, 2.2f, 0.55f), new Vector3(0.12f, 0.12f, 0.12f), new Color(0.1f, 0.9f, 0.2f));
            if (!HasNamedChild(root, "NavLight R"))
                ParentBlock(root, "NavLight R", new Vector3(10.68f, 2.2f, 0.55f), new Vector3(0.12f, 0.12f, 0.12f), new Color(0.9f, 0.12f, 0.12f));
            if (!HasNamedChild(root, "Beacon"))
                ParentBlock(root, "Beacon", new Vector3(0f, 3.15f, -0.4f), new Vector3(0.14f, 0.14f, 0.14f), new Color(0.95f, 0.2f, 0.15f));
            if (!HasNamedChild(root, "LandingLight") && !HasNamedChild(root, "LandingLight L"))
                ParentBlock(root, "LandingLight", new Vector3(0f, 0.7f, 8.4f), new Vector3(0.18f, 0.12f, 0.18f), new Color(0.95f, 0.95f, 0.85f));
            if (!HasNamedChild(root, "TaxiLight"))
                ParentBlock(root, "TaxiLight", new Vector3(0f, 0.68f, 7.35f), new Vector3(0.14f, 0.1f, 0.14f), new Color(0.95f, 0.92f, 0.7f));

            AttachEngineAudio(root, AircraftType.Saab340, 12f, 220f, 0.11f);
            return root;
        }

/// <summary>AIR-006 original, unbranded Dash 8-400-class turboprop.</summary>
        private static Transform BuildDash8Q400(
            string name,
            Color accent,
            string liveryDecalRelativePath = null)
        {
            var profile = AircraftVisualProfiles.Dash8Q400;
            var root = new GameObject(name).transform;
            AircraftVisualProfileComponent.Ensure(root, profile);

            var usedArt = ArtPresentationLoader.TryInstantiate(
                profile.ArtRelativePath,
                root,
                out _,
                RenameAircraftPart,
                kitName => Dash8Q400PartColor(kitName, accent),
                localPosition: new Vector3(0f, profile.ModelGroundOffsetMetres, 0f));

            if (usedArt)
            {
                NestCrossPropellerBlades(root);
                RebakePropellerPivots(root);
                RebakeAircraftArticulatedPivots(root);
                NestLandingGearParts(root);
                RebakeWheelPivots(root);
                RigLandingGearArticulation(root);
                NestCabinDoorParts(root);
                AttachDoorways(root);
                ConvertToAirstairDoor(root);
                NestFlapParts(root);
                NestWingMountedParts(root);
                AirsideAircraftRenderBatcher.CombineStaticGlazing(root);
                EnsureAircraftLod(root);
            }
            else
            {
                // True-scale primitive fallback. It keeps the Q400's long high-wing,
                // nacelle-gear and T-tail read if the art bundle is unavailable.
                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "Fuselage";
                body.transform.SetParent(root, false);
                body.transform.localPosition = new Vector3(0f, 2.4f, 0f);
                body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                body.transform.localScale = new Vector3(1.38f, 16.35f, 1.38f);
                body.GetComponent<Renderer>().sharedMaterial = CreateMaterial(new Color(0.93f, 0.95f, 0.97f));
                ParentBlock(root, "Livery stripe", new Vector3(0f, 2.2f, 0.8f), new Vector3(2.82f, 0.16f, 22.8f), accent);
                ParentBlock(root, "Wing L", new Vector3(-7.7f, 4.55f, 0.5f), new Vector3(13f, 0.32f, 3.2f), accent);
                ParentBlock(root, "Wing R", new Vector3(7.7f, 4.55f, 0.5f), new Vector3(13f, 0.32f, 3.2f), accent);
                ParentBlock(root, "Engine L", new Vector3(-4.35f, 3.45f, 0.4f), new Vector3(1.45f, 1.35f, 8.7f), accent * 0.72f);
                ParentBlock(root, "Engine R", new Vector3(4.35f, 3.45f, 0.4f), new Vector3(1.45f, 1.35f, 8.7f), accent * 0.72f);
                ParentBlock(root, "Propeller L", new Vector3(-4.35f, 3.6f, 5.45f), new Vector3(0.14f, 4.1f, 0.24f), new Color(0.2f, 0.2f, 0.22f));
                ParentBlock(root, "Propeller R", new Vector3(4.35f, 3.6f, 5.45f), new Vector3(0.14f, 4.1f, 0.24f), new Color(0.2f, 0.2f, 0.22f));
                ParentBlock(root, "Tail", new Vector3(0f, 5.9f, -12.1f), new Vector3(0.35f, 4.9f, 4.2f), accent);
                ParentBlock(root, "Tailplane", new Vector3(0f, 7.85f, -12.9f), new Vector3(13.3f, 0.24f, 2.4f), accent);
                ParentBlock(root, "Gear nose", new Vector3(0f, 1.05f, 11.25f), new Vector3(0.22f, 1.45f, 0.22f), new Color(0.25f, 0.25f, 0.28f));
                ParentBlock(root, "Gear L", new Vector3(-4.35f, 1.5f, -1.7f), new Vector3(0.24f, 2.3f, 0.32f), new Color(0.25f, 0.25f, 0.28f));
                ParentBlock(root, "Gear R", new Vector3(4.35f, 1.5f, -1.7f), new Vector3(0.24f, 2.3f, 0.32f), new Color(0.25f, 0.25f, 0.28f));
                ParentBlock(root, "CabinDoor", new Vector3(-1.39f, 2.3f, 11f), new Vector3(0.08f, 1.65f, 0.82f), new Color(0.78f, 0.8f, 0.83f));
            }

            ApplyLiveryDecal(root, liveryDecalRelativePath);
            // Same as the Saab: the Q400 needs blur discs or its props disappear at power.
            EnsurePropDiscs(root);
            EnsureGroundShadow(root);
            if (!HasNamedChild(root, "NavLight L"))
                ParentBlock(root, "NavLight L", new Vector3(-14.15f, 4.85f, 1.2f), new Vector3(0.14f, 0.14f, 0.14f), new Color(0.1f, 0.9f, 0.2f));
            if (!HasNamedChild(root, "NavLight R"))
                ParentBlock(root, "NavLight R", new Vector3(14.15f, 4.85f, 1.2f), new Vector3(0.14f, 0.14f, 0.14f), new Color(0.9f, 0.12f, 0.12f));
            if (!HasNamedChild(root, "Beacon"))
                ParentBlock(root, "Beacon", new Vector3(0f, 3.8f, -0.75f), new Vector3(0.16f, 0.16f, 0.16f), new Color(0.95f, 0.2f, 0.15f));
            if (!HasNamedChild(root, "LandingLight") && !HasNamedChild(root, "LandingLight L"))
                ParentBlock(root, "LandingLight", new Vector3(0f, 1.15f, 13.8f), new Vector3(0.2f, 0.14f, 0.2f), new Color(0.95f, 0.95f, 0.85f));
            if (!HasNamedChild(root, "TaxiLight"))
                ParentBlock(root, "TaxiLight", new Vector3(0f, 0.85f, 11.4f), new Vector3(0.16f, 0.12f, 0.16f), new Color(0.95f, 0.92f, 0.7f));

            AttachEngineAudio(root, AircraftType.Dash8Q400, 14f, 250f, 0.11f);
            return root;
        }

        /// <summary>AIR-005 original, unbranded 737-8-class narrowbody.</summary>
        private static Transform BuildNarrowbody7378(
            string name,
            Color accent,
            string liveryDecalRelativePath = null,
            AircraftVisualProfile? profileOverride = null)
        {
            var profile = profileOverride ?? AircraftVisualProfiles.Boeing7378;
            var root = new GameObject(name).transform;
            AircraftVisualProfileComponent.Ensure(root, profile);
            var a350 = profile.ArtRelativePath.Contains("mdl_a350_900", StringComparison.Ordinal)
                       || profile.ArtRelativePath.Contains("mdl_a330_900", StringComparison.Ordinal);
            var boeing787 = profile.ArtRelativePath.Contains("mdl_787_", StringComparison.Ordinal);

            var usedArt = ArtPresentationLoader.TryInstantiate(
                profile.ArtRelativePath,
                root,
                out _,
                RenameAircraftPart,
                kitName => a350
                    ? AirbusA350900PartColor(kitName, accent)
                    : boeing787
                        ? Boeing78710PartColor(kitName, accent)
                        : Boeing7378PartColor(kitName, accent),
                localPosition: new Vector3(0f, profile.ModelGroundOffsetMetres, 0f));

            if (usedArt)
            {
                NestJetFanBlades(root);
                RebakeJetFanPivots(root);
                RebakeAircraftArticulatedPivots(root);
                NestLandingGearParts(root);
                RebakeWheelPivots(root);
                RigLandingGearArticulation(root);
                NestCabinDoorParts(root);
                AttachDoorways(root);
                NestFlapParts(root);
                NestWingMountedParts(root);
                EnsureJetFanDiscs(root);
                AirsideAircraftRenderBatcher.CombineStaticGlazing(root);
                EnsureAircraftLod(root);
            }
            else
            {
                // True-scale primitive fallback: it preserves the 39.5 m narrowbody
                // footprint if the runtime art bundle is unavailable.
                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "Fuselage";
                body.transform.SetParent(root, false);
                body.transform.localPosition = new Vector3(0f, 3.15f, -19.7f);
                body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                body.transform.localScale = new Vector3(2.05f, 19.7f, 2.05f);
                body.GetComponent<Renderer>().sharedMaterial = CreateMaterial(new Color(0.93f, 0.95f, 0.97f));
                ParentBlock(root, "Livery stripe", new Vector3(0f, 3.2f, -18.5f), new Vector3(4.2f, 0.32f, 31f), accent);
                ParentBlock(root, "Wing L", new Vector3(-9f, 3.15f, -22f), new Vector3(18f, 0.28f, 6.2f), new Color(0.86f, 0.89f, 0.92f));
                ParentBlock(root, "Wing R", new Vector3(9f, 3.15f, -22f), new Vector3(18f, 0.28f, 6.2f), new Color(0.86f, 0.89f, 0.92f));
                ParentBlock(root, "Nacelle L", new Vector3(-6.1f, 2.05f, -18.2f), new Vector3(2.8f, 2.8f, 4.5f), accent * 0.72f);
                ParentBlock(root, "Nacelle R", new Vector3(6.1f, 2.05f, -18.2f), new Vector3(2.8f, 2.8f, 4.5f), accent * 0.72f);
                ParentBlock(root, "Tail", new Vector3(0f, 7.1f, -36.5f), new Vector3(0.45f, 8.5f, 5.2f), accent);
                ParentBlock(root, "Tailplane", new Vector3(0f, 6f, -35.2f), new Vector3(13f, 0.3f, 4.4f), new Color(0.86f, 0.89f, 0.92f));
                ParentBlock(root, "Gear nose", new Vector3(0f, 0.9f, -4.5f), new Vector3(0.35f, 1.8f, 0.35f), new Color(0.25f, 0.25f, 0.28f));
                ParentBlock(root, "Gear L", new Vector3(-3.2f, 0.9f, -22f), new Vector3(0.38f, 1.8f, 0.38f), new Color(0.25f, 0.25f, 0.28f));
                ParentBlock(root, "Gear R", new Vector3(3.2f, 0.9f, -22f), new Vector3(0.38f, 1.8f, 0.38f), new Color(0.25f, 0.25f, 0.28f));
                ParentBlock(root, "CabinDoor", new Vector3(2.02f, 3.4f, -5.2f), new Vector3(0.08f, 2.2f, 1.15f), new Color(0.78f, 0.8f, 0.83f));
            }

            ApplyLiveryDecal(root, liveryDecalRelativePath);
            EnsureGroundShadow(root);
            if (!HasNamedChild(root, "NavLight L"))
                ParentBlock(root, "NavLight L", new Vector3(-17.9f, 3.3f, -21.8f), new Vector3(0.16f, 0.16f, 0.16f), new Color(0.1f, 0.9f, 0.2f));
            if (!HasNamedChild(root, "NavLight R"))
                ParentBlock(root, "NavLight R", new Vector3(17.9f, 3.3f, -21.8f), new Vector3(0.16f, 0.16f, 0.16f), new Color(0.9f, 0.12f, 0.12f));
            if (!HasNamedChild(root, "Beacon"))
                ParentBlock(root, "Beacon", new Vector3(0f, 5.35f, -18f), new Vector3(0.18f, 0.18f, 0.18f), new Color(0.95f, 0.2f, 0.15f));
            if (!HasNamedChild(root, "LandingLight") && !HasNamedChild(root, "LandingLight L"))
                ParentBlock(root, "LandingLight", new Vector3(0f, 1.15f, -3.2f), new Vector3(0.2f, 0.14f, 0.2f), new Color(0.95f, 0.95f, 0.85f));
            if (!HasNamedChild(root, "TaxiLight"))
                ParentBlock(root, "TaxiLight", new Vector3(0f, 0.8f, -4.2f), new Vector3(0.16f, 0.12f, 0.16f), new Color(0.95f, 0.92f, 0.7f));

            AttachEngineAudio(root, AircraftType.Boeing7378, 16f, 300f, EngineVolumeRunning);
            return root;
        }

        private static string FriendlyPartSuffix(string suffix) =>
            suffix.Replace('_', ' ');

        /// <summary>
        /// Aircraft glass seen from outside by day: near-black and neutral, with the smooth
        /// glazing material supplying the sky reflection. A blue-teal tint read as a toy.
        /// </summary>
        private static readonly Color AircraftGlass = new(0.06f, 0.07f, 0.08f, 0.72f);

        /// <summary>Seals, the painted flight-deck surround and windscreen posts.</summary>
        private static readonly Color AircraftGlazingSurround = new(0.035f, 0.037f, 0.04f);

        private static Color? Saab340PartColor(string kitName, Color accent)
        {
            if (EnginePaint(kitName, turboprop: true) is { } saabEngine)
                return saabEngine;
            // The Saab is a compact commuter aircraft, not a flying colour block.
            // Reserve the operator accent for the vertical tail while keeping the
            // broad lifting surfaces a restrained painted-aluminium grey.
            switch (kitName)
            {
                case "wing_left":
                case "wing_right":
                case "wing_fairing_left":
                case "wing_fairing_right":
                case "flap_left":
                case "flap_right":
                case "aileron_left":
                case "aileron_right":
                case "tailplane":
                case "elevator_left":
                case "elevator_right":
                    return new Color(0.76f, 0.79f, 0.82f);
            }

            return AircraftPartColor(kitName, accent);
        }

        private static Color? Boeing7378PartColor(string kitName, Color accent)
        {
            if (EnginePaint(kitName, turboprop: false) is { } jetEngine)
                return jetEngine;
            // Keep the broad lifting surfaces visually light. Airline colour is
            // strongest on the fin and split winglets, where it reads as livery
            // rather than making the whole aircraft look moulded from plastic.
            switch (kitName)
            {
                case "wing_left":
                case "wing_right":
                case "wing_root_left":
                case "wing_root_right":
                case "wing_fairing_left":
                case "wing_fairing_right":
                case "flap_left":
                case "flap_right":
                case "flap_fairing_l":
                case "flap_fairing_r":
                case "spoiler_left":
                case "spoiler_right":
                case "aileron_left":
                case "aileron_right":
                case "tailplane":
                case "tailplane_tip_l":
                case "tailplane_tip_r":
                case "elevator_left":
                case "elevator_right":
                    return new Color(0.58f, 0.62f, 0.67f);
            }

            return AircraftPartColor(kitName, accent);
        }

        private static Color? AirbusA350900PartColor(string kitName, Color accent)
        {
            // The A350's identity comes from its dark wraparound flight-deck mask,
            // long pale composite wing and raked tips. Keep airline colour on the
            // fin/rudder instead of reusing the narrowbody colour hierarchy.
            if (kitName.StartsWith("door_", StringComparison.Ordinal))
                return new Color(0.89f, 0.92f, 0.94f);

            switch (kitName)
            {
                case "wing_left":
                case "wing_right":
                case "wingtip_left":
                case "wingtip_right":
                case "flap_left":
                case "flap_right":
                case "spoiler_left":
                case "spoiler_right":
                case "aileron_left":
                case "aileron_right":
                case "tailplane":
                case "elevator_left":
                case "elevator_right":
                    return new Color(0.72f, 0.76f, 0.80f);
                case "engine_left":
                case "engine_right":
                case "pylon_left":
                case "pylon_right":
                    return new Color(0.88f, 0.91f, 0.93f);
                case "nacelle_left":
                case "nacelle_right":
                case "intake_left":
                case "intake_right":
                    return new Color(0.48f, 0.52f, 0.56f);
                case "tail_fin":
                case "rudder":
                    return accent;
            }

            return AircraftPartColor(kitName, accent);
        }

        private static Color? Boeing78710PartColor(string kitName, Color accent)
        {
            if (kitName.StartsWith("door_", StringComparison.Ordinal))
                return new Color(0.89f, 0.92f, 0.94f);

            switch (kitName)
            {
                case "wing_left":
                case "wing_right":
                case "wingtip_left":
                case "wingtip_right":
                case "flap_left":
                case "flap_right":
                case "spoiler_left":
                case "spoiler_right":
                case "aileron_left":
                case "aileron_right":
                case "tailplane":
                case "elevator_left":
                case "elevator_right":
                    return new Color(0.70f, 0.74f, 0.78f);
                case "engine_left":
                case "engine_right":
                case "pylon_left":
                case "pylon_right":
                    return new Color(0.88f, 0.91f, 0.93f);
                case "nacelle_left":
                case "nacelle_right":
                case "intake_left":
                case "intake_right":
                    return new Color(0.48f, 0.52f, 0.56f);
                case "exhaust_chevron_left":
                case "exhaust_chevron_right":
                    return new Color(0.34f, 0.36f, 0.38f);
                case "tail_fin":
                case "rudder":
                    return accent;
            }

            return AircraftPartColor(kitName, accent);
        }

        private static Color? Dash8Q400PartColor(string kitName, Color accent)
        {
            if (EnginePaint(kitName, turboprop: true) is { } q400Engine)
                return q400Engine;
            // Preserve airline colour on the tall fin and compact tip devices,
            // not across the Q400's entire high wing and T-tail.
            switch (kitName)
            {
                case "wing_left":
                case "wing_right":
                case "wing_fairing_left":
                case "wing_fairing_right":
                case "wing_centre_saddle":
                case "wing_fence_left":
                case "wing_fence_right":
                case "flap_left":
                case "flap_right":
                case "spoiler_left":
                case "spoiler_right":
                case "aileron_left":
                case "aileron_right":
                case "tailplane":
                case "tailplane_saddle":
                case "tailplane_tip_l":
                case "tailplane_tip_r":
                case "elevator_left":
                case "elevator_right":
                    return new Color(0.56f, 0.60f, 0.65f);
            }

            return AircraftPartColor(kitName, accent);
        }

        private static Color? Atr42PartColor(string kitName, Color accent)
        {
            if (EnginePaint(kitName, turboprop: true) is { } atrEngine)
                return atrEngine;
            // Keep the ATR's operator identity on its fin and compact wingtips.
            // The broad high wing and T-tail planes should read as aircraft
            // structure, not as two large blocks of airline colour.
            switch (kitName)
            {
                case "wing_left":
                case "wing_right":
                case "wing_root_left":
                case "wing_root_right":
                case "wing_fairing_left":
                case "wing_fairing_right":
                case "wing_centre_saddle":
                case "flap_left":
                case "flap_right":
                case "spoiler_left":
                case "spoiler_right":
                case "aileron_left":
                case "aileron_right":
                case "tailplane":
                case "tailplane_saddle":
                case "tailplane_tip_l":
                case "tailplane_tip_r":
                case "elevator_left":
                case "elevator_right":
                    return new Color(0.58f, 0.62f, 0.67f);
            }

            return AircraftPartColor(kitName, accent);
        }

        /// <summary>
        /// Move every rolling gear part's transform onto its own axle, so the ground
        /// roll turns it on the spot.
        ///
        /// The kit authors each tyre/wheel/rim as a flat node at the aircraft origin
        /// with the mesh baked at its aircraft-space position, so a spin about the
        /// node's own X axis sweeps the part on a circle of radius sqrt(y² + z²)
        /// about the fuselage centreline — roughly 0.4 m for the forward mains, 0.8 m
        /// for the aft mains and 8.3 m for the nose wheels, which carries them up and
        /// over the aeroplane. This is the landing-gear mirror of
        /// <see cref="RebakePropellerPivots"/>. Must run after
        /// <c>NestLandingGearParts</c>, so the parts are already under their leg.
        /// </summary>
        private static void RebakeWheelPivots(Transform aircraft)
        {
            var namedChildren19 = AirsideNamedChildren.Get(aircraft);
            var childNames19 = AirsideNamedChildren.Names(aircraft);
            for (var childIndex19 = 0; childIndex19 < namedChildren19.Length; childIndex19++)
            {
                var child = namedChildren19[childIndex19];
                var childName = childNames19[childIndex19];
                if (child == aircraft || !AirsideAircraftParts.RollsInPlace(childName))
                    continue;
                RebakeWheelPivot(child);
            }
        }

        private static void RebakeWheelPivot(Transform wheel)
        {
            var renderer = wheel.GetComponent<Renderer>();
            if (renderer == null)
                return;

            // A wheel turns about its lateral axis, so the axle sits at the centre of
            // the mesh bounds. Skip when the node is already on that axle — the
            // Resources/prefab path authors wheel verts locally and must not be moved.
            var axleWorld = renderer.bounds.center;
            if ((wheel.position - axleWorld).sqrMagnitude < 0.0025f)
                return;

            RebakeOwnMeshToPivot(wheel, axleWorld);
        }

        /// <summary>
        /// Re-origin one part: move its transform to <paramref name="pivotWorld"/> and
        /// shift its mesh vertices by the same amount, so the part does not appear to
        /// move but now rotates about that point.
        ///
        /// The mesh is cloned first. Kit meshes are shared through the loader's cache,
        /// and editing one in place would drag every other instance and every other
        /// aircraft with it.
        /// </summary>
        private static void RebakeOwnMeshToPivot(Transform part, Vector3 pivotWorld)
        {
            var filter = part.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
                return;

            var source = filter.sharedMesh;
            var local = source.vertices;
            var world = new Vector3[local.Length];
            for (var v = 0; v < local.Length; v++)
                world[v] = part.TransformPoint(local[v]);

            part.position = pivotWorld;

            var mesh = Object.Instantiate(source);
            mesh.name = source.name + " axle-pivot";
            var rebaked = new Vector3[world.Length];
            for (var v = 0; v < rebaked.Length; v++)
                rebaked[v] = part.InverseTransformPoint(world[v]);
            mesh.vertices = rebaked;
            mesh.RecalculateBounds();
            filter.sharedMesh = mesh;
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
        /// Nest flap tracks/fairings under Flap L/R so approach deploy carries them (0025 item 7).
        /// </summary>
        private static void NestFlapParts(Transform aircraft)
        {
            Transform flapL = null, flapR = null;
            var leftExtras = new List<Transform>();
            var rightExtras = new List<Transform>();
            var namedChildren22 = AirsideNamedChildren.Get(aircraft);
            var childNames22 = AirsideNamedChildren.Names(aircraft);
            for (var childIndex22 = 0; childIndex22 < namedChildren22.Length; childIndex22++)
            {
                var child = namedChildren22[childIndex22];
                var childName = childNames22[childIndex22];
                if (childName == "Flap L")
                    flapL = child;
                else if (childName == "Flap R")
                    flapR = child;
                else if (childName is "Flap track L1" or "Flap track L2" or "Flap fairing L")
                    leftExtras.Add(child);
                else if (childName is "Flap track R1" or "Flap track R2" or "Flap fairing R")
                    rightExtras.Add(child);
            }

            foreach (var extra in leftExtras)
                NestUnderProp(flapL, extra, extra.name);
            foreach (var extra in rightExtras)
                NestUnderProp(flapR, extra, extra.name);
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
            var namedChildren24 = AirsideNamedChildren.Get(aircraft);
            var childNames24 = AirsideNamedChildren.Names(aircraft);
            for (var childIndex24 = 0; childIndex24 < namedChildren24.Length; childIndex24++)
            {
                var child = namedChildren24[childIndex24];
                var childName = childNames24[childIndex24];
                if (childName.StartsWith("CabinDoor", StringComparison.Ordinal))
                    door = child;
                else if (childName == "Door handle")
                    handle = child;
                else if (childName == "Door frame")
                    frame = child;
                else if (childName == "Cargo door latch")
                    latch = child;
            }

            NestUnderProp(door, handle, "Handle");
            NestUnderProp(door, frame, "Frame");
            // Cargo latch stays with cargo door if present.
            Transform cargo = null;
            var namedChildren25 = AirsideNamedChildren.Get(aircraft);
            var childNames25 = AirsideNamedChildren.Names(aircraft);
            for (var childIndex25 = 0; childIndex25 < namedChildren25.Length; childIndex25++)
            {
                var child = namedChildren25[childIndex25];
                var childName = childNames25[childIndex25];
                if (childName == "Cargo door")
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
            var namedChildren26 = AirsideNamedChildren.Get(vehicle);
            var childNames26 = AirsideNamedChildren.Names(vehicle);
            for (var childIndex26 = 0; childIndex26 < namedChildren26.Length; childIndex26++)
            {
                var child = namedChildren26[childIndex26];
                var childName = childNames26[childIndex26];
                if (child == vehicle)
                    continue;
                if (childName == "Door")
                    door = child;
                else if (childName is "Door glass" or "Door handle" or "Door frame")
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
            RendererTintBlock.SetColor(ColorId, color);
            RendererTintBlock.SetColor(BaseColorId, color);
            if (emission.HasValue)
            {
                var shared = renderer.sharedMaterial;
                if (shared != null && shared.HasProperty(EmissionColorId))
                {
                    // Night-glow, nav-light and cabin-window passes call this for many renderers
                    // every frame. EnableKeyword on the shared material each time is a native
                    // keyword write per call; once is enough.
                    if (!shared.IsKeywordEnabled("_EMISSION"))
                        shared.EnableKeyword("_EMISSION");
                    RendererTintBlock.SetColor(EmissionColorId, emission.Value);
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

        private enum ControlSurfaceKind { Rudder, Elevator, Aileron, Wing, Flap, Spoiler }

        /// <summary>
        /// A classified control surface. <see cref="Factor"/> means the elevator multiplier
        /// (1 for a dedicated elevator mesh, 0.35 for the whole tailplane) on
        /// <see cref="ControlSurfaceKind.Elevator"/>, or the L/R sign on
        /// <see cref="ControlSurfaceKind.Aileron"/>/<see cref="ControlSurfaceKind.Wing"/>.
        /// </summary>
        private sealed class ControlSurfacePart
        {
            public readonly Transform Transform;
            public readonly ControlSurfaceKind Kind;
            public readonly float Factor;
            /// <summary>Hinge line in the part's own frame, oriented +X (+Y for the rudder); swept surfaces slant it.</summary>
            public readonly Vector3 HingeAxis;
            public readonly Quaternion RestRotation;
            public readonly Vector3 RestPosition;
            public readonly float ChordMetres;
            /// <summary>Current trailing-edge-down deflection in degrees (a raised spoiler is negative).</summary>
            public float Deflection;

            public ControlSurfacePart(Transform transform, ControlSurfaceKind kind, float factor)
            {
                Transform = transform;
                Kind = kind;
                Factor = factor;
                RestRotation = transform != null ? transform.localRotation : Quaternion.identity;
                RestPosition = transform != null ? transform.localPosition : Vector3.zero;
                HingeAxis = ControlSurfaceHinge(transform, kind, out ChordMetres);
            }
        }

        private enum LightGearKind { GearDoor, GearStrut, GearTruck, NavigationLight, Beacon, LandingLight, TaxiLight }

        private enum GearRole { None, Nose, MainLeft, MainRight }

        private sealed class LightGearPart
        {
            public readonly Transform Transform;
            public readonly LightGearKind Kind;
            public readonly AircraftNavigationLight NavLight;
            // Resolved on first use, then reused every frame: the lights pass used to run
            // GetComponent<Light>, GetComponent<Renderer> and a by-name Transform.Find for
            // the wingtip strobe on every lamp of every visible aircraft, every frame.
            public Light Light;
            public Light Strobe;
            public Renderer Lamp;
            public bool LampResolved;

            // Landing-gear articulation (struts, trucks and belly doors). Retract is the part's own
            // 0 (down and locked) .. 1 (up and locked) travel; the pass slews it toward the phase's target.
            public GearRole Role;
            public GearRetractStyle Style;
            public Quaternion Rest = Quaternion.identity;
            public float Retract;
            public bool RetractSeeded;
            public float SteerDegrees;
            /// <summary>Belly doors: -1 when hinged on the left edge, +1 on the right (free edge swings down).</summary>
            public float DoorSign = -1f;

            public LightGearPart(Transform transform, LightGearKind kind, AircraftNavigationLight navLight = AircraftNavigationLight.None)
            {
                Transform = transform;
                Kind = kind;
                NavLight = navLight;
                if (transform != null)
                    Rest = transform.localRotation;
            }
        }

        private enum CabinDoorKind { Cabin, Cargo, Airstair }

        private readonly struct CabinDoorPart
        {
            public readonly Transform Transform;
            public readonly CabinDoorKind Kind;
            /// <summary>Airstair only: signed fold about the fuselage axis that puts the steps on the ground.</summary>
            public readonly float OpenDegrees;
            /// <summary>The hollow behind the leaf, shown while it is open; null when the kit has none.</summary>
            public readonly GameObject Doorway;

            public CabinDoorPart(Transform transform, CabinDoorKind kind, float openDegrees = 0f)
            {
                Transform = transform;
                Kind = kind;
                OpenDegrees = openDegrees;
                Doorway = transform != null && transform.TryGetComponent<AircraftDoorway>(out var doorway)
                    ? doorway.Shell
                    : null;
            }
        }

        private readonly struct PropellerPart
        {
            public readonly Transform Transform;
            public readonly bool IsLeft;

            public PropellerPart(Transform transform, bool isLeft)
            {
                Transform = transform;
                IsLeft = isLeft;
            }
        }

        /// <summary>
        /// Components the per-frame aircraft passes need, resolved once per view. Each frame
        /// used to repeat Transform.Find over the aircraft's children and GetComponent for the
        /// shadow, selection marker and visual profile of every aircraft — and, separately,
        /// the control-surface, light/gear, cabin-door, window-glow, engine-heat and propeller
        /// passes each re-classified every named child by string every single frame. All of
        /// that classification now happens once here instead; the per-frame passes only touch
        /// the handful of children that actually matched.
        /// </summary>
        private struct AircraftViewParts
        {
            public Transform Owner;
            public AircraftVisualProfileComponent Profile;
            public Transform Shadow;
            public Renderer ShadowRenderer;
            public Transform Marker;
            public Renderer MarkerRenderer;
            public Transform GearNose;
            /// <summary>The nose strut as a gear-rig part, so steering and retraction compose in one place.</summary>
            public LightGearPart GearNosePart;
            public AircraftArticulationState Articulation;
            public WheelPart[] Wheels;
            public float WheelbaseMetres;
            /// <summary>Main-gear centre along the fuselage (root local Z); pitch pivots here.</summary>
            public float MainGearZMetres;
            /// <summary>Carries "Fan L"/"Fan R" turbofan assemblies (the 737).</summary>
            public bool HasFans;
            public Transform FanLeft;
            public Transform FanRight;
            /// <summary>Carries separate "Elevator" meshes, so the tailplane itself stays still.</summary>
            public bool HasSeparateElevators;
            public ControlSurfacePart[] ControlSurfaces;
            public LightGearPart[] LightsAndGear;
            public CabinDoorPart[] CabinDoors;
            public (Transform Transform, Renderer Renderer, Color BaseColor)[] CabinWindowGlass;
            public int CabinWindowGlowState;
            public (Transform Transform, Renderer Renderer)[] EngineHeatVents;
            public PropellerPart[] Propellers;
        }

        private readonly Dictionary<int, AircraftViewParts> _aircraftViewParts = new();

        private AircraftViewParts PartsFor(Transform aircraft)
        {
            var id = aircraft.GetInstanceID();
            if (_aircraftViewParts.TryGetValue(id, out var parts) && parts.Owner == aircraft)
                return parts;

            parts = new AircraftViewParts
            {
                Owner = aircraft,
                Profile = aircraft.GetComponent<AircraftVisualProfileComponent>(),
                Shadow = aircraft.Find("GroundShadow"),
                Marker = aircraft.Find(AircraftPickRouting.MarkerChildName),
                Articulation = new AircraftArticulationState()
            };
            parts.ShadowRenderer = parts.Shadow != null ? parts.Shadow.GetComponent<Renderer>() : null;
            parts.MarkerRenderer = parts.Marker != null ? parts.Marker.GetComponent<Renderer>() : null;
            // All of these used to be rediscovered by scanning every child name on every frame,
            // once per aircraft per pass (fans/elevators/gear here, plus six more full scans
            // spread across the per-frame update methods). One pass now resolves everything.
            var children = AirsideNamedChildren.Get(aircraft);
            var names = AirsideNamedChildren.Names(aircraft);
            Transform mainLeft = null, mainRight = null;
            for (var i = 0; i < names.Length; i++)
            {
                if (children[i] == null)
                    continue;
                if (names[i] is "Fan L" or "Fan R")
                    parts.HasFans = true;
                else if (names[i].StartsWith("Elevator", StringComparison.Ordinal))
                    parts.HasSeparateElevators = true;
                else if (names[i] == "Gear nose")
                    parts.GearNose = children[i];
                else if (names[i] == "Gear L")
                    mainLeft = children[i];
                else if (names[i] == "Gear R")
                    mainRight = children[i];
            }
            if (mainLeft != null || mainRight != null)
            {
                var mainCentre = mainLeft != null && mainRight != null
                    ? (mainLeft.position + mainRight.position) * 0.5f
                    : (mainLeft != null ? mainLeft.position : mainRight.position);
                parts.MainGearZMetres = aircraft.InverseTransformPoint(mainCentre).z;
            }
            if (parts.GearNose != null && (mainLeft != null || mainRight != null))
            {
                var main = mainLeft != null && mainRight != null
                    ? (mainLeft.position + mainRight.position) * 0.5f
                    : (mainLeft != null ? mainLeft.position : mainRight.position);
                var delta = parts.GearNose.position - main;
                delta.y = 0f;
                parts.WheelbaseMetres = delta.magnitude;
            }

            ClassifyAnimatedParts(children, names, ref parts);

            _aircraftViewParts[id] = parts;
            return parts;
        }

        /// <summary>
        /// Second pass, kept separate from the gear/fan pass above only for readability —
        /// reproduces exactly the per-frame predicates that used to run every frame in
        /// <c>UpdateControlSurfaces</c>, <c>UpdateAircraftLightsAndGear</c>, <c>UpdateCabinDoor</c>,
        /// <c>UpdateCabinWindowGlow</c>, <c>UpdateEngineHeat</c> and the propeller spin passes,
        /// so behaviour is unchanged — only how often the classification runs.
        /// </summary>
        private static void ClassifyAnimatedParts(Transform[] children, string[] names, ref AircraftViewParts parts)
        {
            var controlSurfaces = new List<ControlSurfacePart>();
            var lightsAndGear = new List<LightGearPart>();
            var cabinDoors = new List<CabinDoorPart>();
            var cabinWindowGlass = new List<(Transform, Renderer, Color)>();
            var engineHeatVents = new List<(Transform, Renderer)>();
            var propellers = new List<PropellerPart>();
            var wheels = new List<WheelPart>();

            // Which way the mains fold depends on the airframe, not on a type id the traffic and circuit
            // paths do not carry: a jet swings them inboard, as does an ATR on its fuselage sponsons, a
            // Dash 8 goes aft into its nacelle (it keeps inner nacelle doors), a Saab forward.
            var turboprop = false;
            var innerNacelleDoors = false;
            var sponsonLegs = false;
            for (var i = 0; i < names.Length; i++)
            {
                if (names[i].StartsWith("Propeller", StringComparison.Ordinal))
                    turboprop = true;
                else if (names[i].StartsWith("gear_door_inner", StringComparison.OrdinalIgnoreCase))
                    innerNacelleDoors = true;
                else if (names[i] == "Gear L" && children[i] != null && parts.Owner != null)
                    sponsonLegs = Mathf.Abs(parts.Owner.InverseTransformPoint(children[i].position).x) < 2.5f;
            }
            var mainGearStyle = AircraftArticulation.MainGearStyle(turboprop, innerNacelleDoors, sponsonLegs);

            for (var i = 0; i < names.Length; i++)
            {
                var child = children[i];
                var childName = names[i];
                if (child == null || child == parts.Owner)
                    continue;

                // -- control surfaces (UpdateControlSurfaces) --
                if (childName.StartsWith("Rudder", StringComparison.Ordinal))
                    controlSurfaces.Add(new ControlSurfacePart(child, ControlSurfaceKind.Rudder, 0f));
                else if (childName.IndexOf("elevator", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         (!parts.HasSeparateElevators && childName.StartsWith("Tailplane", StringComparison.Ordinal)))
                {
                    var factor = childName.StartsWith("Tailplane", StringComparison.Ordinal) ? 0.35f : 1f;
                    controlSurfaces.Add(new ControlSurfacePart(child, ControlSurfaceKind.Elevator, factor));
                }
                else if (childName.StartsWith("Aileron", StringComparison.Ordinal))
                {
                    var side = childName.IndexOf(" L", StringComparison.Ordinal) >= 0 ? 1f : -1f;
                    controlSurfaces.Add(new ControlSurfacePart(child, ControlSurfaceKind.Aileron, side));
                }
                else if (childName is "Wing L" or "Wing R")
                {
                    var side = childName == "Wing L" ? -1f : 1f;
                    controlSurfaces.Add(new ControlSurfacePart(child, ControlSurfaceKind.Wing, side));
                }
                else if (childName is "Flap L" or "Flap R")
                    controlSurfaces.Add(new ControlSurfacePart(child, ControlSurfaceKind.Flap, 0f));
                else if (childName.StartsWith("Spoiler", StringComparison.Ordinal))
                {
                    var side = childName.IndexOf(" L", StringComparison.Ordinal) >= 0 ? 1f : -1f;
                    controlSurfaces.Add(new ControlSurfacePart(child, ControlSurfaceKind.Spoiler, side));
                }

                // -- lights and gear (UpdateAircraftLightsAndGear) --
                if (AirsideAircraftParts.IsGearDoor(childName))
                {
                    // A plate carried on a leg rides it; only belly panels swing open on their own.
                    if (child.parent == null || !AirsideAircraftParts.IsGearStrut(child.parent.name))
                        lightsAndGear.Add(new LightGearPart(child, LightGearKind.GearDoor)
                        {
                            DoorSign = BellyDoorHingeSign(child)
                        });
                }
                else if (AirsideAircraftParts.IsGearStrut(childName))
                {
                    var role = childName == "Gear nose" ? GearRole.Nose
                        : childName == "Gear L" ? GearRole.MainLeft
                        : GearRole.MainRight;
                    lightsAndGear.Add(new LightGearPart(child, LightGearKind.GearStrut)
                    {
                        Role = role,
                        Style = role == GearRole.Nose ? GearRetractStyle.Forward : mainGearStyle
                    });
                }
                else if (childName.StartsWith("Truck ", StringComparison.Ordinal))
                    lightsAndGear.Add(new LightGearPart(child, LightGearKind.GearTruck));
                else if (AirsideAircraftParts.NavigationLightFor(childName) is var navigationLight
                         && navigationLight != AircraftNavigationLight.None)
                    lightsAndGear.Add(new LightGearPart(child, LightGearKind.NavigationLight, navigationLight));
                else if (childName.StartsWith("Beacon", StringComparison.Ordinal))
                    lightsAndGear.Add(new LightGearPart(child, LightGearKind.Beacon));
                else if (childName.StartsWith("LandingLight", StringComparison.Ordinal))
                    lightsAndGear.Add(new LightGearPart(child, LightGearKind.LandingLight));
                else if (childName.StartsWith("TaxiLight", StringComparison.Ordinal))
                    lightsAndGear.Add(new LightGearPart(child, LightGearKind.TaxiLight));

                // -- cabin/cargo doors (UpdateCabinDoor) --
                if (childName.StartsWith("CabinDoor", StringComparison.Ordinal))
                {
                    var airstair = child.GetComponent<AirstairDoor>();
                    cabinDoors.Add(airstair != null
                        ? new CabinDoorPart(child, CabinDoorKind.Airstair, airstair.OpenDegrees)
                        : new CabinDoorPart(child, CabinDoorKind.Cabin));
                }
                else if (childName.StartsWith("Cargo door", StringComparison.OrdinalIgnoreCase)
                         || childName.Equals("CargoDoor", StringComparison.OrdinalIgnoreCase))
                    cabinDoors.Add(new CabinDoorPart(child, CabinDoorKind.Cargo));

                // -- cabin/cockpit glass (UpdateCabinWindowGlow) --
                if (childName == "Cockpit"
                    || childName == "Cockpit glare"
                    || ((childName.StartsWith("Cabin window", StringComparison.OrdinalIgnoreCase)
                         || childName.StartsWith("Cabin windows", StringComparison.OrdinalIgnoreCase)
                         || childName.IndexOf("cabin_window", StringComparison.OrdinalIgnoreCase) >= 0)
                        && childName.IndexOf("frame", StringComparison.OrdinalIgnoreCase) < 0))
                {
                    var glassRenderer = child.GetComponent<Renderer>();
                    if (glassRenderer != null)
                        cabinWindowGlass.Add((child, glassRenderer, GetRendererColor(glassRenderer)));
                }

                // -- engine heat shimmer (UpdateEngineHeat) --
                if (childName.StartsWith("EngineHeat", StringComparison.Ordinal))
                    engineHeatVents.Add((child, child.GetComponent<Renderer>()));

                // -- tyres (RollLandingGearTires) --
                if (AirsideAircraftParts.RollsInPlace(childName))
                    wheels.Add(new WheelPart(child,
                        childName.IndexOf("nose", StringComparison.OrdinalIgnoreCase) >= 0));

                // -- propellers (SpinPropellers / SpinPropellersPerEngine) --
                if (childName.StartsWith("Propeller", StringComparison.Ordinal))
                    propellers.Add(new PropellerPart(child, childName.EndsWith(" L", StringComparison.Ordinal)));

                // -- turbofans (SpinJetFans) --
                if (childName == "Fan L")
                    parts.FanLeft = child;
                else if (childName == "Fan R")
                    parts.FanRight = child;
            }

            parts.ControlSurfaces = controlSurfaces.ToArray();
            parts.LightsAndGear = lightsAndGear.ToArray();
            parts.CabinDoors = cabinDoors.ToArray();
            parts.CabinWindowGlass = cabinWindowGlass.ToArray();
            parts.EngineHeatVents = engineHeatVents.ToArray();
            parts.Propellers = propellers.ToArray();
            parts.Wheels = wheels.ToArray();
            foreach (var gear in lightsAndGear)
            {
                if (gear.Kind == LightGearKind.GearStrut && gear.Role == GearRole.Nose)
                    parts.GearNosePart = gear;
            }
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

        private static void ParentBlock(Transform parent, string name, Vector3 localPosition, Vector3 scale, Color color)
        {
            var block = CreateBlock(name, localPosition, scale, color);
            block.transform.SetParent(parent, false);
            block.transform.localPosition = localPosition;
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


        private static AudioClip _engineClip;
        private static AudioClip _fallbackTouchdownClip;
        private static readonly Dictionary<string, AudioClip> _engineClipByResource = new();

        /// <summary>
        /// The nearest frequency that completes a whole number of cycles in a buffer of
        /// <paramref name="seconds"/>, so a looped bed does not jump at the wrap. The gust,
        /// hush and swell tones were all cut mid-cycle, which clicked once per loop.
        /// </summary>
        public static float LoopFrequency(float desiredHz, float seconds)
        {
            if (seconds <= 0f)
                return desiredHz;
            var cycles = Mathf.Max(1f, Mathf.Round(desiredHz * seconds));
            return cycles / seconds;
        }

        /// <summary>
        /// Blend the tail of a looping buffer into its head. Filtered noise starts from a
        /// silent filter state and ends wherever it happens to be, so even a whole number of
        /// cycles left a step at the loop point.
        /// </summary>
        public static void CrossfadeLoop(float[] samples, int fadeSamples)
        {
            if (samples == null || fadeSamples <= 1 || samples.Length < fadeSamples * 2)
                return;
            var start = samples.Length - fadeSamples;
            for (var i = 0; i < fadeSamples; i++)
            {
                var t = i / (float)fadeSamples;
                samples[start + i] = Mathf.Lerp(samples[start + i], samples[i], t);
            }
        }

        /// <summary>
        /// No CC0 thunder sample has been sourced yet (unlike rain/wind/coast — see
        /// docs/data/ASSET_AND_DATA_REGISTER.md), so this procedural clap is the only
        /// implementation for now: a sharp crack (filtered noise transient) into a
        /// low-frequency rumble that decays over ~2.4 s, mirroring <see cref="CreateTouchdownClip"/>'s
        /// envelope-plus-tone approach. <c>Resources.Load</c> still checks for
        /// "Airside/Audio/thunder_crack_01" first so a real clip can replace this without a
        /// code change.
        /// </summary>
        private static AudioClip CreateThunderClip()
        {
            const int sampleRate = 22050;
            const float seconds = 2.4f;
            var samples = new float[(int)(sampleRate * seconds)];
            var rumbleState = 0f;
            for (var i = 0; i < samples.Length; i++)
            {
                var time = i / (float)sampleRate;
                // The crack: a fast-decaying burst of filtered noise in the first ~80 ms.
                var crackEnvelope = Mathf.Exp(-time * 40f);
                var crack = (UnityEngine.Random.value * 2f - 1f) * crackEnvelope;
                // The rumble: low-passed noise (a leaky integrator) shaped by a slower
                // envelope with a couple of soft secondary swells, like a rolling boom.
                var white = UnityEngine.Random.value * 2f - 1f;
                rumbleState = rumbleState * 0.985f + white * 0.015f;
                var rumbleEnvelope = Mathf.Exp(-time * 1.6f)
                    * (1f + 0.35f * Mathf.Sin(time * 2f * Mathf.PI * 2.2f) * Mathf.Exp(-time * 0.8f));
                var rumble = rumbleState * Mathf.Max(0f, rumbleEnvelope) * 3.2f;
                samples[i] = Mathf.Clamp(crack * 0.6f + rumble, -1f, 1f);
            }

            var clip = AudioClip.Create("Thunder clap", samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>Soft coastal wave bed for Kangaroo Island ambience (presentation only).</summary>
        private static AudioClip CreateCoastClip()
        {
            const int sampleRate = 22050;
            const float seconds = 3f;
            var samples = new float[(int)(sampleRate * seconds)];
            var state = 0f;
            var swellHz = LoopFrequency(0.22f, seconds);
            var washHz = LoopFrequency(0.55f, seconds);
            for (var i = 0; i < samples.Length; i++)
            {
                var t = i / (float)sampleRate;
                var white = UnityEngine.Random.value * 2f - 1f;
                state = state * 0.96f + white * 0.04f;
                var swell = Mathf.Sin(t * 2f * Mathf.PI * swellHz) * 0.5f + 0.5f;
                var wash = Mathf.Sin(t * 2f * Mathf.PI * washHz + 1.3f) * 0.35f + 0.65f;
                samples[i] = state * 0.4f * swell * wash;
            }

            CrossfadeLoop(samples, sampleRate / 8);
            var clip = AudioClip.Create("Ambient coast", samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private Vector3 PositionFor(AircraftPhase phase, float progress, TaxiRoute taxiRoute, float laneOffset = 0f,
            AircraftType type = null, RunwayDirection runway = RunwayDirection.Runway05)
        {
            // Every phase hands over where the previous one ended: landing rolls out to
            // the A1 entry TaxiIn starts from, taxi-out stops at the runway hold-short
            // point takeoff lines up from, and takeoff runs straight into the climb-out.
            var t = Mathf.Clamp01(progress);
            // Number-two holds off the flare gate, but compressing progress rather than
            // clamping it means it keeps creeping down the approach instead of stopping
            // dead in mid-air the instant it reaches the hold point.
            t = ApproachHold.ApproachVisualProgress(t, laneOffset);
            return phase switch
            {
                AircraftPhase.Approach => AirsideFlightPath.Approach(t, laneOffset, type),
                AircraftPhase.Landing => AirsideFlightPath.Landing(t, laneOffset, type),
                AircraftPhase.TaxiIn => AirsideFlightPath.OnRunwayHold(),
                AircraftPhase.AtStand => AirsideFlightPath.OnRunwayHold(),
                AircraftPhase.Pushback => AirsideFlightPath.OnRunwayHold(),
                AircraftPhase.TaxiOut => AirsideFlightPath.OnRunwayHold(),
                AircraftPhase.Takeoff => AirsideFlightPath.Takeoff(t, TakeoffOffsetX, type, runway),
                AircraftPhase.Circuit => AirsideFlightPath.Circuit(t),
                AircraftPhase.GoAround => AirsideFlightPath.GoAround(t),
                _ => AirsideFlightPath.Departed(t, TakeoffOffsetX, type, runway)
            };
        }

        /// <summary>
        /// Go-around poses in world space for the assigned runway. 12/30 use a
        /// cross-strip circuit; remapping the 05 racetrack put regionals through
        /// the terminal.
        /// </summary>
        private Vector3? FleetGoAroundWorldPosition(CommercialFlight flight, float lookAheadSeconds)
        {
            if (flight.Operation.Phase != AircraftPhase.GoAround)
                return null;
            if (!FleetMode || !_fleetAircraftById.TryGetValue(flight.AircraftId, out var aircraft))
                return null;
            var elapsed = _preciseTime - flight.Operation.PhaseStartedAt.ElapsedSeconds + lookAheadSeconds;
            CircuitTraffic.GoAroundOnRunway(elapsed, aircraft.AssignedRunway, out var x, out var y, out var z);
            return new Vector3((float)x, (float)y, (float)z);
        }

        /// <summary>
        /// Smooths the cut when a go-around's racetrack ends and the aircraft re-enters the
        /// ordinary landing queue (ADR 0068). Without this, the instant `FleetState` flips
        /// from GoAround to HoldingForLanding, the drawn position jumped straight from the
        /// circuit (up to 305 m, out over the racetrack) to the pinned approach-queue point —
        /// a 400-1,100 m horizontal snap and a 210-250 m altitude drop in one frame, gear
        /// included, verified with real numbers before this fix. The two systems have no
        /// shared parameterisation to interpolate through physically, so this blends the two
        /// endpoints' world positions over <see cref="GoAroundRejoin.BlendSeconds"/> instead —
        /// not a real rejoin flight path, but a slide is a world apart from a teleport.
        /// Returns null once the blend window has passed, so the ordinary pinned position
        /// takes over exactly as before for the rest of the approach.
        /// </summary>
        private Vector3? FleetGoAroundRejoinWorldPosition(CommercialFlight flight, TaxiRoute route,
            float laneOffset, AircraftType type, float lookAheadSeconds)
        {
            if (flight.Operation.Phase != AircraftPhase.Approach)
                return null;
            if (!FleetMode || !_fleetAircraftById.TryGetValue(flight.AircraftId, out var aircraft))
                return null;
            if (!aircraft.WentAroundThisTrip || aircraft.State != FleetState.HoldingForLanding)
                return null;

            var elapsed = _preciseTime - aircraft.StateStartedAt.ElapsedSeconds + lookAheadSeconds;
            if (elapsed >= GoAroundRejoin.BlendSeconds)
                return null;

            CircuitTraffic.GoAroundOnRunway(CircuitTraffic.LoopSeconds, aircraft.AssignedRunway,
                out var gx, out var gy, out var gz);
            var from = new Vector3((float)gx, (float)gy, (float)gz);

            // Rejoin onto the extended final the arrival will fly in on, not a fixed point.
            var pinnedProgress = (float)ApproachHold.HoldingFinalProgress(
                FleetVisual.QueueSlot(_operations.Fleet, aircraft));
            var to = FleetArrivalFinalPosition(flight, (float)lookAheadSeconds)
                     ?? RunwayPosition(flight, PositionFor(AircraftPhase.Approach, pinnedProgress, route, laneOffset, type));

            var blend = (float)GoAroundRejoin.Blend01(elapsed);
            return Vector3.Lerp(from, to, blend);
        }

        /// <summary>
        /// ADR 0166 — the departure's turn onto its destination, as one flown arc. Along-track
        /// metres past the turn start (the far threshold, <see cref="DepartureTurn.TurnStartProgress"/>
        /// of the climb-out), the bearing to the destination, and the type's own turn radius.
        /// False on the roll, the initial climb and anything without a destination.
        /// </summary>
        private bool TryDepartureArc(CommercialFlight flight, AircraftPhase phase, float progress,
            out float xStart, out float along, out double relative, out float radius)
        {
            xStart = along = radius = 0f;
            relative = 0.0;
            if (phase != AircraftPhase.Departed || !FleetMode || _operations == null
                || !_fleetAircraftById.TryGetValue(flight.AircraftId, out var aircraft))
                return false;
            var dest = aircraft.CurrentDestination ?? aircraft.Scheduled?.Destination;
            if (!dest.HasValue)
                return false;
            // Same path the climb-out is drawn on, including the runway's own roll-in.
            var runway = aircraft.AssignedRunway;
            xStart = AirsideFlightPath.Departed(DepartureTurn.TurnStartProgress, TakeoffOffsetX, aircraft.Type, runway).x;
            along = AirsideFlightPath.Departed(progress, TakeoffOffsetX, aircraft.Type, runway).x - xStart;
            relative = DepartureTurn.RelativeRadiansFor(aircraft.AssignedRunway, _operations.Home, dest.Value);
            radius = DepartureTurn.TurnRadiusMetres(
                AircraftPerformance.For(aircraft.Type).AirspeedKnots(AircraftPhase.Departed, DepartureTurn.TurnStartProgress));
            return true;
        }

        /// <summary>
        /// Past the far threshold the climb-out flies a real turn: the position follows the arc,
        /// so the aircraft goes where its nose points (it used to yaw while sliding sideways and
        /// carrying on down the runway line, which read as drifting).
        /// </summary>
        private Vector3 ApplyDepartureTurn(CommercialFlight flight, AircraftPhase phase, float progress,
            Vector3 position)
        {
            if (!TryDepartureArc(flight, phase, progress, out var xStart, out var along, out var relative, out var radius)
                || along <= 0f)
                return position;
            var (forward, sideways, _) = DepartureTurn.Arc(relative, radius, along);
            return new Vector3(xStart + forward, position.y, position.z + sideways);
        }

        /// <summary>The nose along the arc: the runway heading turned by the arc's own yaw.</summary>
        private Quaternion DepartureLookRotation(CommercialFlight flight, AircraftPhase phase, float progress,
            Quaternion fallback)
        {
            if (!TryDepartureArc(flight, phase, progress, out _, out var along, out var relative, out var radius)
                || along <= 0f)
                return fallback;
            var yaw = DepartureTurn.Arc(relative, radius, along).yawDegrees;
            if (Mathf.Abs(yaw) < 0.05f || !_fleetAircraftById.TryGetValue(flight.AircraftId, out var aircraft))
                return fallback;
            RunwayFrame.Forward(aircraft.AssignedRunway, out var fx, out var fz);
            var runwayAlong = new Vector3(fx, 0f, fz);
            if (runwayAlong.sqrMagnitude < 0.001f)
                return fallback;
            return Quaternion.LookRotation(runwayAlong) * Quaternion.Euler(0f, yaw, 0f);
        }

        private float ApproachLaneOffset(CommercialFlight flight)
        {
            // The tower clears one fleet arrival at a time, so there is never a number two.
            if (FleetMode)
            {
                // A hand-flown final is not pixel-perfect until it settles onto the
                // centreline. AirsideFlightPath damps this offset through the flare.
                var seed = StableRegistrationHash(flight.AircraftId);
                var direction = (seed & 1) == 0 ? -1f : 1f;
                var drift = Mathf.Sin((float)_preciseTime * 0.025f + seed * 0.001f) * 0.05f;
                return direction * 0.14f + drift;
            }
            if (VisualFlights.Count < 2)
                return 0f;
            // Number-two / later flights take a parallel final left of centreline.
            var index = 0;
            for (var i = 0; i < VisualFlights.Count; i++)
            {
                if (ReferenceEquals(VisualFlights[i], flight))
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
            // Untextured blocks get chamfered edges (ADR 0124); art-textured ones keep the
            // primitive's exact UV layout.
            if (artTextureRelativePath == null)
            {
                var bevelled = BevelledCubeMesh(scale);
                if (bevelled != null)
                    block.GetComponent<MeshFilter>().sharedMesh = bevelled;
            }
            var renderer = block.GetComponent<Renderer>();
            renderer.sharedMaterial = CreateSharedSurfaceMaterial(color, artTextureRelativePath, textureTiling);
            if (_airfieldRoot != null)
                block.transform.SetParent(_airfieldRoot, true);
            AirsideSceneIndex.Remember(block);
            return block;
        }

        private static readonly Dictionary<(int, int, int), Mesh> BevelledCubes = new();

        /// <summary>
        /// A shared chamfered unit cube whose bevel is the same number of world metres on every
        /// edge once scaled by <paramref name="scale"/>; null for blocks too thin to show one.
        /// </summary>
        private static Mesh BevelledCubeMesh(Vector3 scale)
        {
            var local = BevelledBox.LocalBevelFor(scale.x, scale.y, scale.z);
            if (!local.HasValue)
                return null;
            var key = (BevelledBox.Quantise(local.Value.x), BevelledBox.Quantise(local.Value.y),
                BevelledBox.Quantise(local.Value.z));
            if (BevelledCubes.TryGetValue(key, out var cached) && cached != null)
                return cached;
            var mesh = GeometryMesh(BevelledBox.Build(key.Item1 / 1000f, key.Item2 / 1000f, key.Item3 / 1000f), "Bevelled cube");
            BevelledCubes[key] = mesh;
            return mesh;
        }

        private static Mesh GeometryMesh(BevelledBox.Geometry g, string name)
        {
            var vertices = new Vector3[g.VertexCount];
            var normals = new Vector3[g.VertexCount];
            var uvs = new Vector2[g.VertexCount];
            for (var i = 0; i < g.VertexCount; i++)
            {
                vertices[i] = new Vector3(g.Positions[i * 3], g.Positions[i * 3 + 1], g.Positions[i * 3 + 2]);
                normals[i] = new Vector3(g.Normals[i * 3], g.Normals[i * 3 + 1], g.Normals[i * 3 + 2]);
                uvs[i] = new Vector2(g.Uvs[i * 2], g.Uvs[i * 2 + 1]);
            }

            var mesh = new Mesh { name = name };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = g.Triangles.ToArray();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        private static void DestroyPresentationObject(UnityEngine.Object target)
        {
            if (target == null)
                return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(target);
            else
                UnityEngine.Object.DestroyImmediate(target);
        }

        private static void CreateDecalQuad(string name, Vector3 position, Vector3 scale, string artTextureRelativePath)
        {
            var texture = TryLoadArtTexture(artTextureRelativePath);
            if (texture == null)
                return;

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            DestroyPresentationObject(quad.GetComponent<Collider>());
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

        private static Material CreateMaterial(Color color, string artTextureRelativePath = null, Vector2? textureTiling = null)
        {
            return CreateSharedSurfaceMaterial(color, artTextureRelativePath, textureTiling);
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



    }
}
