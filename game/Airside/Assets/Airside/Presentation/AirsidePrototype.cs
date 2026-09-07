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
        private Light[] _apronLights;
        private Light _aerodromeBeacon;
        private Transform _rainRoot;
        private Transform _touchdownSmoke;
        private float _touchdownSmokeRemaining;
        private AudioSource _touchdownAudio;
        private AudioClip _touchdownClip;
        private readonly Dictionary<string, AircraftPhase> _previousPhases = new Dictionary<string, AircraftPhase>();
        private readonly List<(Renderer Renderer, Color DryColor)> _wetSurfaces = new List<(Renderer, Color)>();
        private readonly List<Renderer> _holdShortRenderers = new List<Renderer>();
        private Transform _fuelTruck;
        private Transform _baggageCart;
        private Transform _passengerBus;
        private Transform _stairs;
        private Transform _chocks;
        private Transform _gpuCart;
        private Transform _pushbackTug;
        private Transform _windsockSock;
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
        private int _acceptedRouteCountSeen;
        private readonly List<Renderer> _nightGlowRenderers = new List<Renderer>();
        private const float EngineVolumeRunning = 0.11f;
        private const float EngineVolumeIdle = 0.02f;
        private const float EngineVolumePausedScale = 0.28f;
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
            _seenEventCount = _simulation.EventLog.Events.Count;

            BuildLightingAndCamera();
            BuildAirfield();
            CollectNightGlowWindows();
            _apronLights = BuildApronLights();
            _aerodromeBeacon = BuildAerodromeBeacon();
            _rainRoot = BuildRainRoot();
            _touchdownSmoke = BuildTouchdownSmoke();
            _touchdownClip = CreateTouchdownClip();
            _touchdownAudio = gameObject.AddComponent<AudioSource>();
            _touchdownAudio.playOnAwake = false;
            _touchdownAudio.spatialBlend = 0.55f;
            _touchdownAudio.volume = 0.22f;
            CollectWetSurfaces();
            CollectHoldShortMarkings();
            _commercialAircraft = Array.Empty<Transform>();
            SyncCommercialAircraftViews();
            _groundTraffic = new Transform[_simulation.GroundTraffic.Count];
            for (var index = 0; index < _groundTraffic.Length; index++)
                _groundTraffic[index] = BuildGroundTrafficAircraft(_simulation.GroundTraffic[index].Id.Value);
            _fuelTruck = BuildServiceVehicle("Fuel truck", new Color(0.92f, 0.78f, 0.18f), new Vector3(3.1f, 1.25f, 1.35f),
                "Models/Vehicles/mdl_fuel_truck_small_v01.gltf");
            _baggageCart = BuildServiceVehicle("Baggage cart", new Color(0.91f, 0.38f, 0.12f), new Vector3(2.3f, 0.8f, 1.15f),
                "Models/Vehicles/mdl_baggage_tug_train_v01.gltf");
            _passengerBus = BuildServiceVehicle("Passenger bus", new Color(0.17f, 0.58f, 0.78f), new Vector3(3.8f, 1.5f, 1.45f),
                "Models/Vehicles/mdl_passenger_bus_apron_v01.gltf");
            _stairs = BuildStairs();
            _chocks = BuildChocks();
            _gpuCart = BuildGpuCart();
            _pushbackTug = BuildPushbackTug();
            _windsockSock = BuildWindsock();
            EnsureStandThreeVisual();
            if (_commercialAircraft.Length > 0)
                _cameraController.SetFollowTargets(_commercialAircraft);
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
            UpdateWeatherPresentation();
            UpdateTouchdownSmoke();
            UpdateTrafficWaitPresentation();
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
                if (direction.sqrMagnitude > 0.001f)
                    view.rotation = Quaternion.Slerp(view.rotation, Quaternion.LookRotation(direction.normalized), Time.unscaledDeltaTime * 5f);

                SpinPropellers(view, phase);
                UpdateAircraftLightsAndGear(view, phase, (float)_simulation.TimeOfDay.Daylight);
                UpdateCabinDoor(view, phase);
                UpdateEngineHeat(view, phase);
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
                if (child.name.StartsWith("Gear", StringComparison.Ordinal))
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
                    child.gameObject.SetActive(enginesOn || night);
                else if (child.name.StartsWith("Beacon", StringComparison.Ordinal))
                {
                    child.gameObject.SetActive(enginesOn && (Mathf.FloorToInt(Time.unscaledTime * 2f) % 2 == 0));
                }
                else if (child.name.StartsWith("LandingLight", StringComparison.Ordinal))
                    child.gameObject.SetActive(landingLights);
                else if (child.name.StartsWith("TaxiLight", StringComparison.Ordinal))
                    child.gameObject.SetActive(taxiLights);
            }
        }

        private static void UpdateCabinDoor(Transform aircraft, AircraftPhase phase)
        {
            // Presentation-only: cabin door swings open at stand, closes before pushback.
            var targetY = phase == AircraftPhase.AtStand ? -85f : 0f;
            foreach (var child in aircraft.GetComponentsInChildren<Transform>(true))
            {
                if (child == aircraft || !child.name.StartsWith("CabinDoor", StringComparison.Ordinal))
                    continue;
                var euler = child.localEulerAngles;
                var current = euler.y > 180f ? euler.y - 360f : euler.y;
                euler.y = Mathf.MoveTowards(current, targetY, Time.unscaledDeltaTime * 120f);
                child.localEulerAngles = euler;
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
                return;

            var rpm = phase switch
            {
                AircraftPhase.Takeoff => 1400f,
                AircraftPhase.Approach or AircraftPhase.Landing => 1100f,
                AircraftPhase.TaxiIn or AircraftPhase.TaxiOut or AircraftPhase.Pushback => 420f,
                _ => 720f
            };
            var degrees = Time.unscaledDeltaTime * rpm;
            foreach (var child in aircraft.GetComponentsInChildren<Transform>(true))
            {
                if (child == aircraft)
                    continue;
                if (child.name.StartsWith("Propeller", StringComparison.Ordinal))
                    child.Rotate(Vector3.forward, degrees, Space.Self);
            }
        }

        private static void SpinGroundTrafficPropellers(Transform aircraft, bool enginesOn)
        {
            if (!enginesOn)
                return;
            var degrees = Time.unscaledDeltaTime * 520f;
            foreach (var child in aircraft.GetComponentsInChildren<Transform>(true))
            {
                if (child == aircraft)
                    continue;
                if (child.name.StartsWith("Propeller", StringComparison.Ordinal))
                    child.Rotate(Vector3.forward, degrees, Space.Self);
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
                if (direction.sqrMagnitude > 0.0004f)
                    view.rotation = Quaternion.Slerp(
                        view.rotation,
                        Quaternion.LookRotation(direction.normalized),
                        Time.unscaledDeltaTime * 4f);

                SpinGroundTrafficPropellers(view, enginesOn: !traffic.IsHolding);
                UpdateAircraftLightsAndGear(
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

            if (servicing == null)
            {
                UpdateVehicle(_fuelTruck, false, Vector3.zero);
                UpdateVehicle(_baggageCart, false, Vector3.zero);
                UpdateVehicle(_passengerBus, false, Vector3.zero);
                return;
            }

            var standZ = AirportTaxiNetwork.StandZ(servicing.AssignedStand);
            var fuelActive = TaskActive(servicing, "Refuel");
            var bagActive = TaskActive(servicing, "Unload bags") || TaskActive(servicing, "Load bags");
            var paxActive = TaskActive(servicing, "Passengers off") || TaskActive(servicing, "Board passengers");
            UpdateVehicle(_fuelTruck, fuelActive, new Vector3(13.3f, 0.55f, standZ + 1.8f));
            UpdateVehicle(_baggageCart, bagActive, new Vector3(20.2f, 0.42f, standZ - 1.8f));
            UpdateVehicle(_passengerBus, paxActive, new Vector3(13f, 0.68f, standZ - 2.2f));
            AnimateServiceLoops(_fuelTruck, fuelActive, "Hose");
            AnimateServiceLoops(_baggageCart, bagActive, "Cargo");
            AnimateServiceLoops(_passengerBus, paxActive, "Door");
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
                PlaceProp(_stairs, true, new Vector3(17.9f, 0.55f, z + 0.15f), Quaternion.Euler(0f, -8f, 0f));
                PlaceProp(_chocks, true, new Vector3(17f, 0.12f, z + 1.55f), Quaternion.identity);
                PlaceProp(_gpuCart, true, new Vector3(15.2f, 0.35f, z + 2.4f), Quaternion.Euler(0f, 90f, 0f));
            }
            else
            {
                PlaceProp(_stairs, false, Vector3.zero, Quaternion.identity);
                PlaceProp(_chocks, false, Vector3.zero, Quaternion.identity);
                PlaceProp(_gpuCart, false, Vector3.zero, Quaternion.identity);
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

        private static void UpdateVehicle(Transform vehicle, bool active, Vector3 position)
        {
            if (!active)
            {
                ResetServiceLoopParts(vehicle);
                vehicle.gameObject.SetActive(false);
                return;
            }

            vehicle.gameObject.SetActive(true);
            var previous = vehicle.position;
            vehicle.position = position;
            // Presentation-only: wheels roll while the vehicle is on a service task.
            var travel = Vector3.Distance(previous, position);
            var degrees = Time.unscaledDeltaTime * 360f + travel * 40f;
            foreach (var child in vehicle.GetComponentsInChildren<Transform>(true))
            {
                if (child == vehicle)
                    continue;
                if (child.name.IndexOf("wheel", StringComparison.OrdinalIgnoreCase) >= 0)
                    child.Rotate(Vector3.right, degrees, Space.Self);
            }
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
                RenderSettings.ambientLight *= 0.92f;
                RenderSettings.fog = foggy || raining;
                RenderSettings.fogColor = new Color(0.55f, 0.6f, 0.66f);
                RenderSettings.fogDensity = weather == WeatherKind.Storm ? 0.012f : foggy ? 0.02f : 0.006f;
            }
            else
            {
                RenderSettings.fog = false;
            }

            // Darken paved surfaces when wet (presentation only — no sim effect).
            var wetness = wet ? (weather == WeatherKind.Storm ? 0.55f : raining ? 0.4f : 0.28f) : 0f;
            for (var i = 0; i < _wetSurfaces.Count; i++)
            {
                var (renderer, dry) = _wetSurfaces[i];
                if (renderer == null)
                    continue;
                renderer.material.color = Color.Lerp(dry, dry * 0.55f, wetness);
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
                    _touchdownSmoke.gameObject.SetActive(true);
                    _touchdownSmokeRemaining = 0.95f;
                    if (_touchdownAudio != null && _touchdownClip != null && !_audioMuted)
                    {
                        _touchdownAudio.transform.position = _touchdownSmoke.position;
                        _touchdownAudio.PlayOneShot(_touchdownClip, 0.35f);
                    }
                }

                _previousPhases[id] = phase;
            }

            if (_touchdownSmokeRemaining <= 0f)
            {
                _touchdownSmoke.gameObject.SetActive(false);
                return;
            }

            _touchdownSmokeRemaining -= Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(_touchdownSmokeRemaining / 0.95f);
            for (var i = 0; i < _touchdownSmoke.childCount; i++)
            {
                var puff = _touchdownSmoke.GetChild(i);
                puff.localScale = Vector3.Lerp(new Vector3(2.2f, 0.2f, 2.2f), new Vector3(1.0f, 0.35f, 1.0f), t);
                var renderer = puff.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var color = renderer.material.color;
                    color.a = t * 0.45f;
                    renderer.material.color = color;
                }
            }
        }

        private void CollectHoldShortMarkings()
        {
            _holdShortRenderers.Clear();
            foreach (var name in new[] { "Hold short A", "Hold short B" })
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
                if (renderer != null)
                    renderer.material.color = color;
            }
        }

        private void CollectWetSurfaces()
        {
            _wetSurfaces.Clear();
            foreach (var name in new[] { "Runway", "Taxiway A", "Apron", "Stand 3 apron pad" })
            {
                var go = GameObject.Find(name);
                if (go == null)
                    continue;
                var renderer = go.GetComponent<Renderer>();
                if (renderer == null)
                    continue;
                _wetSurfaces.Add((renderer, renderer.material.color));
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
            for (var i = 0; i < 2; i++)
            {
                var smoke = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                smoke.name = i == 0 ? "Smoke L" : "Smoke R";
                smoke.transform.SetParent(root, false);
                smoke.transform.localPosition = new Vector3(i == 0 ? -0.7f : 0.7f, 0.15f, 0f);
                smoke.transform.localScale = new Vector3(1.0f, 0.35f, 1.0f);
                smoke.GetComponent<Renderer>().material = CreateMaterial(new Color(0.85f, 0.85f, 0.88f, 0.4f));
                var collider = smoke.GetComponent<Collider>();
                if (collider != null)
                    Object.Destroy(collider);
            }

            root.gameObject.SetActive(false);
            return root;
        }

        private void OnGUI()
        {
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
            var detail = AirsideTheme.TextStyle(new GUIStyle(GUI.skin.label) { fontSize = 16 });
            var small = AirsideTheme.TextStyle(new GUIStyle(GUI.skin.label) { fontSize = 13 });
            var caution = AirsideTheme.CautionStyle(small);
            var onTime = AirsideTheme.TextStyle(new GUIStyle(small), AirsideTheme.ClearGreen);
            var delayed = AirsideTheme.TextStyle(new GUIStyle(small), AirsideTheme.SignalRed);
            var button = AirsideTheme.TextStyle(new GUIStyle(GUI.skin.button), AirsideTheme.CoastalBlue);

            var timeOfDay = _simulation.TimeOfDay;

            GUI.Box(new Rect(22, 22, 410, 540), string.Empty, panel);
            GUI.Label(new Rect(42, 36, 320, 34), "AIRSIDE", title);
            GUI.Label(new Rect(42, 58, 380, 18), $"{_simulation.Location.Name}  ·  {_simulation.Location.Region}", small);
            GUI.Label(new Rect(42, 76, 380, 25), CommercialFlightHudLine(), detail);
            var phaseLineX = 42f;
            var phaseIcon = _simulation.Flights.Count > 0
                ? AirsideTheme.OperationIcon(_simulation.Flights[0].Operation.Phase)
                : null;
            if (phaseIcon != null)
            {
                GUI.DrawTexture(new Rect(42, 104, 20, 20), phaseIcon, ScaleMode.ScaleToFit, alphaBlend: true);
                phaseLineX = 68f;
            }
            GUI.Label(new Rect(phaseLineX, 104, 380 - (phaseLineX - 42), 25), CommercialPhaseHudLine(), detail);
            var weatherLabel = Weather.Describe(_simulation.CurrentWeather);
            if (Weather.IsAdverse(_simulation.CurrentWeather))
                weatherLabel += " · wet apron";
            var clockStyle = _paused || _speed > 1 ? caution : small;
            var weatherLineX = 42f;
            var weatherIcon = AirsideTheme.WeatherIcon(_simulation.CurrentWeather);
            if (weatherIcon != null)
            {
                GUI.DrawTexture(new Rect(42, 132, 20, 20), weatherIcon, ScaleMode.ScaleToFit, alphaBlend: true);
                weatherLineX = 68f;
            }
            GUI.Label(new Rect(weatherLineX, 132, 380 - (weatherLineX - 42), 22),
                $"{(_paused ? "PAUSED" : $"{_speed}× time")}{(_audioMuted ? "  ·  MUTED" : string.Empty)}  ·  Day {timeOfDay.DaysElapsed + 1} {timeOfDay.Clock} {timeOfDay.Phase}  ·  {weatherLabel}", clockStyle);
            var cashStyle = _simulation.Economy.Cash < 0 ? delayed : small;
            var reputationStyle = ReputationBandStyle(small, onTime, caution, delayed);
            var cashIcon = AirsideTheme.Icon("economy", "cash");
            var cashX = 42f;
            if (cashIcon != null)
            {
                GUI.DrawTexture(new Rect(42, 156, 18, 18), cashIcon, ScaleMode.ScaleToFit, alphaBlend: true);
                cashX = 64f;
            }
            GUI.Label(new Rect(cashX, 156, 200 - (cashX - 42), 22), $"Cash: ${_simulation.Economy.Cash:N0}  ·  Cycles {_simulation.CompletedCycles}", cashStyle);
            var repIcon = AirsideTheme.Icon("economy", "reputation");
            var repX = 242f;
            if (repIcon != null)
            {
                GUI.DrawTexture(new Rect(242, 156, 18, 18), repIcon, ScaleMode.ScaleToFit, alphaBlend: true);
                repX = 264f;
            }
            GUI.Label(new Rect(repX, 156, 190 - (repX - 242), 22),
                $"Rep {_simulation.Reputation.Score} ({_simulation.Reputation.Band})", reputationStyle);
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
                GUI.DrawTexture(new Rect(42, 176, 18, 18), incomeIcon, ScaleMode.ScaleToFit, alphaBlend: true);
                financeX = 64f;
            }
            GUI.Label(new Rect(financeX, 176, 390 - (financeX - 42), 22),
                $"Day est. {finance.ExpectedNet:+$#,0;-$#,0;$0} (in ${finance.ExpectedFlightIncome:N0} / out ${finance.ExpectedOperatingCost:N0}){runway}", financeStyle);
            if (_simulation.IsInsolvent)
            {
                GUI.Label(new Rect(42, 198, 360, 22), "INSOLVENT — operations frozen", delayed);
            }
            else if (_simulation.Economy.ConsecutiveNegativeDays > 0)
            {
                var left = AirportEconomy.InsolvencyConsecutiveDays - _simulation.Economy.ConsecutiveNegativeDays;
                GUI.Label(new Rect(42, 198, 360, 22),
                    $"Cash warning: {_simulation.Economy.ConsecutiveNegativeDays} negative day close(s) · {left} more → insolvent", caution);
            }
            else if (_simulation.TrafficWaits.HasWarning(_clock.Now))
                GUI.Label(new Rect(42, 198, 360, 22), $"TRAFFIC: {_simulation.TrafficWaits.Describe(_clock.Now)}", caution);

            var lineY = 180f;
            if (_simulation.ActiveAircraft.Phase == AircraftPhase.AtStand && _simulation.ActiveTurnaround != null)
            {
                foreach (var task in _simulation.ActiveTurnaround.Tasks(_clock.Now))
                {
                    var mark = task.State == TurnaroundTaskState.Complete ? "✓" : task.State == TurnaroundTaskState.Active ? "●" : "○";
                    var time = task.State == TurnaroundTaskState.Complete ? string.Empty : $"  {task.SecondsRemaining}s";
                    var taskIcon = AirsideTheme.ServiceIconForTask(task.Name);
                    var taskX = 42f;
                    if (taskIcon != null)
                    {
                        GUI.DrawTexture(new Rect(42, lineY, 16, 16), taskIcon, ScaleMode.ScaleToFit, alphaBlend: true);
                        taskX = 62f;
                    }
                    GUI.Label(new Rect(taskX, lineY, 350 - (taskX - 42), 20), $"{mark} {task.Name}{time}", small);
                    lineY += 19f;
                }

                if (_simulation.CurrentDelaySeconds > 0)
                    GUI.Label(new Rect(42, 298, 360, 22), $"DELAY +{_simulation.CurrentDelaySeconds}s · {_simulation.CurrentDelayCause}", delayed);

                var alreadyAssigned = _simulation.ActiveTurnaround != null && _simulation.ActiveTurnaround.PriorityCrewEnabled;
                GUI.enabled = !_simulation.IsInsolvent && !alreadyAssigned && _simulation.Economy.Cash >= AirportEconomy.PriorityCrewCost;
                if (GUI.Button(new Rect(42, 326, 190, 27), alreadyAssigned ? "Priority crew active" : "Hire priority crew · $300", button))
                    _session.EnablePriorityCrew();
                GUI.enabled = true;
            }
            else
            {
                var onSchedule = _simulation.LastDelaySeconds <= 0;
                GUI.Label(new Rect(42, 184, 350, 22), onSchedule
                    ? "Operations running to schedule"
                    : $"Last flight delay: {_simulation.LastDelaySeconds}s · {_simulation.LastDelayCause}", onSchedule ? onTime : delayed);
            }

            var earlySession = _simulation.Routes.Accepted.Count == 0;
            var staffing = _simulation.Staffing;
            GUI.Label(new Rect(42, 360, 380, 20),
                $"Ground crew: {staffing.GroundCrew}  ·  payroll ${staffing.DailyWage:N0}/day{(staffing.IsUnderstaffed ? "  ·  UNDERSTAFFED" : string.Empty)}",
                staffing.IsUnderstaffed ? caution : small);
            if (earlySession)
            {
                GUI.Label(new Rect(42, 380, 360, 22), "Crew / stand / research unlock after you accept a route", small);
            }
            else
            {
                GUI.enabled = !_simulation.IsInsolvent && staffing.GroundCrew < AirportStaffing.MaximumGroundCrew && _simulation.Economy.Cash >= AirportStaffing.HireCost;
                if (GUI.Button(new Rect(42, 380, 150, 24), $"Hire crew · ${AirportStaffing.HireCost}", button))
                    _session.HireGroundCrew();
                GUI.enabled = !_simulation.IsInsolvent && staffing.GroundCrew > AirportStaffing.MinimumGroundCrew;
                if (GUI.Button(new Rect(198, 380, 110, 24), "Release crew", button))
                    _session.ReleaseGroundCrew();
                GUI.enabled = true;

                var capacity = _simulation.Capacity;
                GUI.Label(new Rect(42, 408, 380, 20),
                    $"Stands: {capacity.StandCount} / {AirportCapacity.MaximumStands}", small);
                GUI.enabled = !_simulation.IsInsolvent && capacity.CanExpand && _simulation.Economy.Cash >= AirportCapacity.ThirdStandCost;
                if (GUI.Button(new Rect(42, 426, 220, 24),
                        capacity.HasThirdStand ? "Stand 3 built" : $"Build stand 3 · ${AirportCapacity.ThirdStandCost:N0}", button))
                    _session.BuildThirdStand();
                GUI.enabled = true;

                var research = _simulation.Research;
                var researchIcon = AirsideTheme.Icon("economy", "research");
                var researchLabelX = 42f;
                if (researchIcon != null)
                {
                    GUI.DrawTexture(new Rect(42, 454, 18, 18), researchIcon, ScaleMode.ScaleToFit, alphaBlend: true);
                    researchLabelX = 64f;
                }
                if (research.IsResearching)
                {
                    var progress = (float)research.Progress01(_clock.Now);
                    var pct = (int)(progress * 100);
                    GUI.Label(new Rect(researchLabelX, 454, 380 - (researchLabelX - 42), 20),
                        $"Research: {research.ActiveProjectName} {pct}% · {research.SecondsRemaining(_clock.Now)}s left", small);
                    AirsideTheme.DrawProgressBar(
                        new Rect(42, 476, 280, 8),
                        progress,
                        AirsideTheme.CoastalBlue,
                        new Color(AirsideTheme.Tarmac.r, AirsideTheme.Tarmac.g, AirsideTheme.Tarmac.b, 0.85f));
                }
                else if (research.CanStartOperationsEfficiency)
                {
                    GUI.Label(new Rect(researchLabelX, 454, 380 - (researchLabelX - 42), 20),
                        $"Research: {AirportResearch.OperationsEfficiencyName} · -${AirportResearch.OperationsEfficiencyDailyDiscount}/day when done", small);
                    GUI.enabled = !_simulation.IsInsolvent && _simulation.Economy.Cash >= AirportResearch.OperationsEfficiencyCost;
                    if (GUI.Button(new Rect(42, 472, 260, 24), $"Start research · ${AirportResearch.OperationsEfficiencyCost:N0}", button))
                        _session.StartOperationsResearch();
                    GUI.enabled = true;
                }
                else if (research.CanStartPassengerServices)
                {
                    GUI.Label(new Rect(researchLabelX, 454, 380 - (researchLabelX - 42), 20),
                        $"Research: {AirportResearch.PassengerServicesName} · +${AirportResearch.PassengerServicesRouteBonus}/flight when done", small);
                    GUI.enabled = !_simulation.IsInsolvent && _simulation.Economy.Cash >= AirportResearch.PassengerServicesCost;
                    if (GUI.Button(new Rect(42, 472, 280, 24), $"Start research · ${AirportResearch.PassengerServicesCost:N0}", button))
                        _session.StartPassengerServicesResearch();
                    GUI.enabled = true;
                }
                else
                {
                    var ops = research.OperationsEfficiencyComplete
                        ? $"{AirportResearch.OperationsEfficiencyName} ✓"
                        : string.Empty;
                    var pax = research.PassengerServicesComplete
                        ? $"{AirportResearch.PassengerServicesName} ✓ (+${AirportResearch.PassengerServicesRouteBonus}/flt)"
                        : string.Empty;
                    GUI.Label(new Rect(researchLabelX, 454, 380 - (researchLabelX - 42), 20),
                        $"Research: {ops}{(ops.Length > 0 && pax.Length > 0 ? " · " : string.Empty)}{pax}", small);
                }
            }

            GUI.Label(new Rect(42, 500, 380, 22), FirstSessionCoachLine(),
                _simulation.Routes.Pending != null && _simulation.Routes.Accepted.Count == 0 ? caution : small);
            GUI.Label(new Rect(42, 518, 380, 22), "Space pause · Tab speed · P priority · M mute · F follow/cycle · O overview", small);

            var historyLeft = Screen.width / scale - 362;
            var accepted = _simulation.Routes.Accepted;
            var pendingOffer = _simulation.Routes.Pending;
            var firstDecisionOffer = pendingOffer != null && accepted.Count == 0;
            var offerHeight = pendingOffer == null ? 0f : (firstDecisionOffer ? 176f : 156f);
            var opsTop = 22f + (offerHeight > 0f ? offerHeight + 12f : 0f);
            // Pin the actionable offer above operations so status detail never buries it.
            if (pendingOffer != null)
                DrawRouteOffer(scale, panel, detail, small, caution, button, offerTop: 22f);

            var listedRoutes = accepted.Count == 0
                ? 1
                : Math.Min(4, accepted.Count) + (accepted.Count > 4 ? 1 : 0);
            // Header through routes summary (~80), schedule lines, fleet (2), event tail (4).
            var opsHeight = 80f + listedRoutes * 18f + 4f + 2 * 18f + 6f + 4 * 20f + 16f;
            GUI.Box(new Rect(historyLeft, opsTop, 340, opsHeight), string.Empty, panel);
            GUI.Label(new Rect(historyLeft + 20, opsTop + 14, 300, 26), "OPERATIONS", detail);
            GUI.Label(new Rect(historyLeft + 20, opsTop + 40, 320, 20),
                $"Routes {_simulation.Routes.Accepted.Count}  ·  {_simulation.Routes.ScheduledFlightsPerDay}/{_simulation.MaxScheduledFlightsPerDay} scheduled flights/day  ·  ${_simulation.Routes.IncomePerFlight + _simulation.Research.RouteIncomeBonus:N0}/flight", small);

            var trafficY = opsTop + 58f;
            if (accepted.Count == 0)
            {
                GUI.Label(new Rect(historyLeft + 20, trafficY, 310, 20), "No accepted routes yet", small);
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

            DrawResearchToast(scale, panel, onTime);
            DrawSaveIndicator(scale, panel, small, onTime);
            DrawOpsToast(scale, panel, detail, onTime);
            if (_paused && !_showAwaySummary && !_showOpeningBriefing)
                DrawPauseOverlay(scale, panel, title, caution, small, button);

            if (_showOpeningBriefing)
                DrawOpeningBriefing(scale, panel, title, detail, small, button);
            if (_showAwaySummary)
                DrawAwaySummary(scale, panel, title, detail, small, button);
            if (_simulation.IsInsolvent)
                DrawInsolvencyOverlay(scale, panel, title, detail, small, delayed, button);
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
            var height = firstDecision ? 176f : 156f;
            GUI.Box(new Rect(left, top, 340, height), string.Empty, panel);
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
            if (GUI.Button(new Rect(left + 20, buttonTop, 150, 24), "Accept route", button))
                _session.AcceptRoute();
            GUI.enabled = true;
            if (GUI.Button(new Rect(left + 178, buttonTop, 130, 24), "Decline", button))
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
            var width = 500f;
            var height = 360f;
            var left = (Screen.width / scale - width) * 0.5f;
            var top = (Screen.height / scale - height) * 0.5f;
            GUI.Box(new Rect(left, top, width, height), string.Empty, panel);
            GUI.Label(new Rect(left + 24, top + 18, width - 48, 34), "AIRSIDE", title);
            GUI.Label(new Rect(left + 24, top + 56, width - 48, 24), "You run this regional airport", detail);
            GUI.Label(new Rect(left + 24, top + 92, width - 48, 44),
                $"Aircraft move on their own. Your job is cash, reputation and capacity at {_simulation.Location.Name}.", detail);
            GUI.Label(new Rect(left + 24, top + 148, width - 48, 22), "First useful decision", detail);
            GUI.Label(new Rect(left + 24, top + 176, width - 48, 44),
                $"In about {AirportRoutes.FirstOfferAfterSeconds} seconds an airline will offer a scheduled route. Accept it to earn money on every completed flight.", small);
            GUI.Label(new Rect(left + 24, top + 230, width - 48, 40),
                "Watch the right-hand OPERATIONS panel. Watch cash and delays on the left.", small);
            GUI.Label(new Rect(left + 24, top + 278, width - 48, 20),
                "Space / Enter to begin  ·  Tab = 4× speed", small);
            if (GUI.Button(new Rect(left + 140, top + 308, 220, 34), "Begin operations", button))
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
                return "Tip: This is your first useful decision — Accept the route offer.";
            if (_simulation.Routes.Pending != null)
                return "Tip: Accept a route offer to earn recurring flight income.";
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
            ApplyDayCycle();
        }

        private void ApplyDayCycle()
        {
            var cycle = _simulation.TimeOfDay;
            var daylight = (float)cycle.Daylight;

            var elevation = (float)cycle.SunElevationDegrees;
            _sun.transform.rotation = Quaternion.Euler(Mathf.Max(-6f, elevation), -28f - (float)cycle.Fraction * 90f, 0f);

            var day = new Color(1f, 0.95f, 0.86f);
            var goldenHour = new Color(1f, 0.66f, 0.42f);
            var night = new Color(0.32f, 0.4f, 0.62f);
            var warm = Mathf.Clamp01(Mathf.Min(daylight, 1f - daylight) * 3f); // strong near dawn/dusk
            _sun.color = Color.Lerp(Color.Lerp(night, day, daylight), goldenHour, warm * daylight);
            _sun.intensity = Mathf.Lerp(0.12f, 1.3f, daylight);

            var ambientDay = new Color(0.46f, 0.53f, 0.61f);
            var ambientDusk = new Color(0.55f, 0.42f, 0.38f);
            var ambientNight = new Color(0.12f, 0.15f, 0.24f);
            RenderSettings.ambientLight = Color.Lerp(
                Color.Lerp(ambientNight, ambientDay, daylight),
                ambientDusk,
                warm * 0.55f);

            if (_mainCamera != null)
            {
                var skyDay = new Color(0.55f, 0.72f, 0.88f);
                var skyDusk = new Color(0.78f, 0.48f, 0.36f);
                var skyNight = new Color(0.06f, 0.08f, 0.14f);
                _mainCamera.backgroundColor = Color.Lerp(
                    Color.Lerp(skyNight, skyDay, daylight),
                    skyDusk,
                    warm * 0.7f);
            }

            // Apron floods come up as daylight falls (presentation only).
            if (_apronLights != null)
            {
                var flood = Mathf.Lerp(1.35f, 0.05f, daylight);
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
                }
            }

            UpdateNightGlow(daylight);
            UpdateAerodromeBeacon(daylight);
        }

        private void CollectNightGlowWindows()
        {
            _nightGlowRenderers.Clear();
            foreach (var name in new[] { "Terminal window glow L", "Terminal window glow R", "Hangar window glow" })
            {
                var go = GameObject.Find(name);
                if (go == null)
                    continue;
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null)
                    _nightGlowRenderers.Add(renderer);
            }
            UpdateNightGlow((float)_simulation.TimeOfDay.Daylight);
        }

        private void UpdateNightGlow(float daylight)
        {
            // Presentation-only: terminal/hangar windows warm up as daylight falls.
            var glow = Mathf.Lerp(0.95f, 0.08f, daylight);
            var color = new Color(1f, 0.82f, 0.45f, 1f) * (0.35f + glow);
            color.a = 1f;
            foreach (var renderer in _nightGlowRenderers)
            {
                if (renderer == null)
                    continue;
                renderer.material.color = color;
            }
        }

        private static Light[] BuildApronLights()
        {
            var positions = new[]
            {
                new Vector3(12f, 5.5f, 12f),
                new Vector3(28f, 5.5f, 12f),
                new Vector3(20f, 5.5f, 22f),
                new Vector3(-18f, 4.5f, 16f),
                new Vector3(8f, 4.8f, 9f),
                new Vector3(32f, 5.2f, 18f),
                new Vector3(17f, 5.0f, 26f),
                new Vector3(-8f, 4.2f, 22f)
            };
            var lights = new Light[positions.Length];
            for (var i = 0; i < positions.Length; i++)
            {
                var go = new GameObject($"Apron flood {i + 1}");
                go.transform.position = positions[i];
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.92f, 0.78f);
                light.range = 28f;
                light.intensity = 0.05f;
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
            CreateBlock("Grass", new Vector3(0f, -0.65f, 4f), new Vector3(94f, 1f, 66f), new Color(0.16f, 0.34f, 0.21f),
                "Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png", new Vector2(12f, 8f));
            CreateBlock("Runway", new Vector3(0f, -0.08f, 0f), new Vector3(78f, 0.15f, 7f), new Color(0.105f, 0.12f, 0.14f),
                "Textures/Surfaces/tx_asphalt_runway_basecolor_v01.png", new Vector2(10f, 1.2f));
            CreateBlock("Taxiway A", new Vector3(8f, -0.02f, 9f), new Vector3(48f, 0.12f, 4f), new Color(0.22f, 0.24f, 0.26f),
                "Textures/Surfaces/tx_asphalt_runway_basecolor_v01.png", new Vector2(6f, 0.8f));
            CreateBlock("Apron", new Vector3(20f, 0f, 17f), new Vector3(28f, 0.12f, 14f), new Color(0.34f, 0.36f, 0.37f),
                "Textures/Surfaces/tx_concrete_apron_basecolor_v01.png", new Vector2(4f, 2f));
            // Batch C buildings (Approved glTF) with primitive silhouette fallback.
            PlaceBuildingOrFallback(
                "Models/Buildings/mdl_terminal_regional_small_v01.gltf",
                new Vector3(26f, 0f, 27f),
                name => name switch
                {
                    "glass_front" => new Color(0.16f, 0.38f, 0.5f),
                    "end_cap_left" or "end_cap_right" => new Color(0.62f, 0.66f, 0.69f),
                    "service_wing" => new Color(0.58f, 0.62f, 0.64f),
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
                "Textures/Environment/tx_terminal_glass_mask_v01.png");
            // Warm interior spill at dusk/night (presentation only).
            CreateBlock("Terminal window glow L", new Vector3(20f, 2.35f, 24.5f), new Vector3(5.5f, 1.6f, 0.08f), new Color(1f, 0.82f, 0.45f));
            CreateBlock("Terminal window glow R", new Vector3(32f, 2.35f, 24.5f), new Vector3(5.5f, 1.6f, 0.08f), new Color(1f, 0.82f, 0.45f));
            CreateBlock("Hangar window glow", new Vector3(-20f, 3.2f, 24.55f), new Vector3(4.5f, 1.8f, 0.08f), new Color(1f, 0.75f, 0.35f));
            PlaceBuildingOrFallback(
                "Models/Buildings/mdl_hangar_small_v01.gltf",
                new Vector3(-20f, 0f, 20f),
                name => name switch
                {
                    "door_opening" => new Color(0.22f, 0.24f, 0.26f),
                    "roof_ridge" => new Color(0.4f, 0.44f, 0.48f),
                    _ => new Color(0.45f, 0.5f, 0.54f)
                },
                () =>
                {
                    CreateBlock("Hangar", new Vector3(-20f, 2.5f, 20f), new Vector3(14f, 5f, 9f), new Color(0.45f, 0.5f, 0.54f),
                        "Textures/Surfaces/tx_corrugated_metal_basecolor_v01.png", new Vector2(2.5f, 1.5f));
                    CreateBlock("Hangar door", new Vector3(-20f, 2.0f, 24.6f), new Vector3(8f, 4f, 0.2f), new Color(0.22f, 0.24f, 0.26f));
                });
            PlaceBuildingOrFallback(
                "Models/Buildings/mdl_operations_shed_v01.gltf",
                new Vector3(-8f, 0f, 26f),
                _ => new Color(0.55f, 0.58f, 0.52f),
                () => CreateBlock("Ops shed", new Vector3(-8f, 1.4f, 26f), new Vector3(6f, 2.8f, 4f), new Color(0.55f, 0.58f, 0.52f),
                    "Textures/Surfaces/tx_corrugated_metal_basecolor_v01.png", new Vector2(1.5f, 1.2f)));

            CreateDecalQuad("Runway wear", new Vector3(0f, 0.02f, 0f), new Vector3(60f, 1f, 2.4f),
                "Textures/Decals/dc_runway_wear_v01.png");
            CreateDecalQuad("Apron stains", new Vector3(20f, 0.06f, 17f), new Vector3(18f, 1f, 10f),
                "Textures/Decals/dc_apron_stains_v01.png");

            PlaceWorldMarkings();
            PlaceWorldLighting();
            PlaceWorldProps();

            BuildStandMarking(17f, 14f, "Stand 1");
            BuildStandMarking(17f, 20f, "Stand 2");
            // Simple painted stand digits (WLD-001 language; not real typography assets).
            CreateBlock("Stand number 1", new Vector3(14.2f, 0.09f, 14f), new Vector3(0.35f, 0.04f, 1.2f), Color.white);
            CreateBlock("Stand number 2 stem", new Vector3(14.2f, 0.09f, 20.35f), new Vector3(0.9f, 0.04f, 0.28f), Color.white);
            CreateBlock("Stand number 2 mid", new Vector3(14.2f, 0.09f, 20f), new Vector3(0.9f, 0.04f, 0.28f), Color.white);
            CreateBlock("Stand number 2 base", new Vector3(14.2f, 0.09f, 19.65f), new Vector3(0.9f, 0.04f, 0.28f), Color.white);

        }

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
            var usedArt = ArtGltfLoader.TryInstantiate(
                "Models/Aircraft/mdl_regional_turboprop_01_v01.gltf",
                root,
                out _,
                RenameAircraftPart,
                kitName => AircraftPartColor(kitName, accent),
                localPosition: new Vector3(0f, -0.7f, 0f));

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
            "nose" => "Nose",
            "wing_left" => "Wing L",
            "wing_right" => "Wing R",
            "engine_left" => "Engine L",
            "engine_right" => "Engine R",
            "propeller_left" => "Propeller L",
            "propeller_right" => "Propeller R",
            "tail_fin" => "Tail",
            "tailplane" => "Tailplane",
            "gear_nose" => "Gear nose",
            "gear_left" => "Gear L",
            "gear_right" => "Gear R",
            "door_fwd" => "CabinDoor",
            _ => kitName
        };

        private static Color? AircraftPartColor(string kitName, Color accent) => kitName switch
        {
            "fuselage" or "nose" => new Color(0.93f, 0.95f, 0.97f),
            "wing_left" or "wing_right" or "tail_fin" or "tailplane" => accent,
            "engine_left" or "engine_right" => accent * 0.85f,
            "propeller_left" or "propeller_right" => new Color(0.2f, 0.2f, 0.22f),
            "gear_nose" or "gear_left" or "gear_right" => new Color(0.25f, 0.25f, 0.28f),
            "door_fwd" => new Color(0.78f, 0.8f, 0.83f),
            _ => null
        };

        private static void ApplyLiveryDecal(Transform aircraft, string artRelativePath)
        {
            if (string.IsNullOrEmpty(artRelativePath))
                return;
            var texture = TryLoadArtTexture(artRelativePath);
            if (texture == null)
                return;

            foreach (var child in aircraft.GetComponentsInChildren<Transform>(true))
            {
                if (child.name != "Fuselage")
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
            var usedArt = !string.IsNullOrEmpty(artRelativePath) && ArtGltfLoader.TryInstantiate(
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
                    "wheel_fl" or "wheel_fr" or "wheel_rl" or "wheel_rr" => new Color(0.15f, 0.15f, 0.16f),
                    "hose_mount" => new Color(0.25f, 0.25f, 0.28f),
                    "door" => new Color(0.2f, 0.22f, 0.25f),
                    "cab" => color * 0.82f,
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
            var root = new GameObject("Passenger stairs").transform;
            if (ArtGltfLoader.TryPlaceNamedMesh(
                    "Models/Props/mdl_service_equipment_kit_v01.gltf",
                    "stairs",
                    Vector3.zero,
                    Quaternion.identity,
                    new Color(0.7f, 0.72f, 0.74f),
                    out var stairs))
            {
                stairs.SetParent(root, false);
                stairs.localPosition = new Vector3(0f, -0.55f, 0f);
            }
            else
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
            var root = new GameObject("Wheel chocks").transform;
            var kit = "Models/Props/mdl_service_equipment_kit_v01.gltf";
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
            var root = new GameObject("GPU cart").transform;
            if (ArtGltfLoader.TryPlaceNamedMesh(
                    "Models/Props/mdl_service_equipment_kit_v01.gltf",
                    "gpu",
                    Vector3.zero,
                    Quaternion.identity,
                    new Color(0.25f, 0.55f, 0.35f),
                    out var gpu))
            {
                gpu.SetParent(root, false);
                gpu.localPosition = new Vector3(0f, -0.55f, 0f);
            }
            else
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
            var root = new GameObject("Pushback tug").transform;
            var kit = "Models/Props/mdl_service_equipment_kit_v01.gltf";
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
            }

            root.gameObject.SetActive(false);
            return root;
        }

        private static Transform BuildWindsock()
        {
            // WLD-003 windsock — kit pole when available, animated sock always procedural.
            const string propsKit = "Models/Props/mdl_airfield_props_kit_v01.gltf";
            if (!ArtGltfLoader.TryPlaceNamedMesh(propsKit, "windsock_pole", new Vector3(-12f, 0f, 12f), Quaternion.identity,
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
            if (ArtGltfLoader.TryPlaceNamedMesh(
                    "Models/Props/mdl_airfield_props_kit_v01.gltf",
                    "cone",
                    position + new Vector3(0f, -0.25f, 0f),
                    Quaternion.identity,
                    new Color(0.95f, 0.45f, 0.08f),
                    out _))
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
            if (ArtGltfLoader.TryPlaceNamedMesh(
                    "Models/Props/mdl_airfield_props_kit_v01.gltf",
                    "barrier",
                    position + new Vector3(0f, -0.45f, 0f),
                    Quaternion.Euler(0f, yawDegrees, 0f),
                    new Color(0.9f, 0.55f, 0.12f),
                    out _))
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
            string glassTextureRelativePath = null)
        {
            if (ArtGltfLoader.TryInstantiate(artRelativePath, null, out var root, rename: null, colorFor: colorFor))
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
            CreateBlock("Runway number 09 bar", new Vector3(-34f, 0.04f, -1.1f), new Vector3(1.6f, 0.03f, 0.35f), Color.white);
            CreateBlock("Runway number 09 stem", new Vector3(-34f, 0.04f, 1.1f), new Vector3(0.35f, 0.03f, 1.8f), Color.white);
            CreateBlock("Runway number 27 bar", new Vector3(34f, 0.04f, 1.1f), new Vector3(1.6f, 0.03f, 0.35f), Color.white);
            CreateBlock("Runway number 27 stem", new Vector3(34f, 0.04f, -1.1f), new Vector3(0.35f, 0.03f, 1.8f), Color.white);

            for (var x = -4; x <= 28; x += 4)
                CreateBlock("Taxi centre", new Vector3(x, 0.04f, 9f), new Vector3(1.2f, 0.03f, 0.18f), new Color(0.95f, 0.85f, 0.2f));
        }

        private static void PlaceWorldLighting()
        {
            const string kit = "Models/Props/mdl_airfield_lighting_kit_v01.gltf";
            var edgeColor = new Color(1f, 1f, 0.85f);
            var taxiColor = new Color(0.2f, 0.85f, 0.35f);
            var obstruction = new Color(0.95f, 0.35f, 0.12f);

            for (var x = -36; x <= 36; x += 6)
            {
                if (!ArtGltfLoader.TryPlaceNamedMesh(kit, "runway_edge_light", new Vector3(x, 0f, -3.4f), Quaternion.identity, edgeColor, out _))
                    CreateBlock("Runway edge L", new Vector3(x, 0.05f, -3.4f), new Vector3(0.25f, 0.1f, 0.25f), edgeColor);
                if (!ArtGltfLoader.TryPlaceNamedMesh(kit, "runway_edge_light", new Vector3(x, 0f, 3.4f), Quaternion.identity, edgeColor, out _))
                    CreateBlock("Runway edge R", new Vector3(x, 0.05f, 3.4f), new Vector3(0.25f, 0.1f, 0.25f), edgeColor);
            }

            for (var x = -8; x <= 28; x += 8)
            {
                if (!ArtGltfLoader.TryPlaceNamedMesh(kit, "taxiway_light", new Vector3(x, 0f, 11.1f), Quaternion.identity, taxiColor, out _))
                    CreateBlock("Taxi light N", new Vector3(x, 0.18f, 11.1f), new Vector3(0.18f, 0.35f, 0.18f), taxiColor);
                if (!ArtGltfLoader.TryPlaceNamedMesh(kit, "taxiway_light", new Vector3(x, 0f, 6.9f), Quaternion.identity, taxiColor, out _))
                    CreateBlock("Taxi light S", new Vector3(x, 0.18f, 6.9f), new Vector3(0.18f, 0.35f, 0.18f), taxiColor);
            }

            if (!ArtGltfLoader.TryPlaceNamedMesh(kit, "obstruction_light", new Vector3(-20f, 5.0f, 20f), Quaternion.identity, obstruction, out _))
                CreateBlock("Hangar obstruction", new Vector3(-20f, 5.2f, 20f), new Vector3(0.25f, 0.25f, 0.25f), obstruction);
            if (!ArtGltfLoader.TryPlaceNamedMesh(kit, "obstruction_light", new Vector3(26f, 4.5f, 27f), Quaternion.identity, obstruction, out _))
                CreateBlock("Terminal roof light", new Vector3(26f, 4.7f, 27f), new Vector3(0.22f, 0.22f, 0.22f), obstruction);

            // Corner flood poles on the apron.
            ArtGltfLoader.TryPlaceNamedMesh(kit, "apron_floodlight", new Vector3(8f, 0f, 12f), Quaternion.identity,
                new Color(0.75f, 0.78f, 0.8f), out _);
            ArtGltfLoader.TryPlaceNamedMesh(kit, "apron_floodlight", new Vector3(32f, 0f, 12f), Quaternion.identity,
                new Color(0.75f, 0.78f, 0.8f), out _);
        }

        private static void PlaceWorldProps()
        {
            const string kit = "Models/Props/mdl_airfield_props_kit_v01.gltf";
            CreateCone(new Vector3(12.5f, 0.25f, 12.2f));
            CreateCone(new Vector3(12.5f, 0.25f, 15.8f));
            CreateCone(new Vector3(23.5f, 0.25f, 12.2f));
            CreateCone(new Vector3(23.5f, 0.25f, 21.8f));
            CreateCone(new Vector3(-6f, 0.25f, 11f));
            CreateBarrier(new Vector3(-14f, 0.45f, 14f), 0f);
            CreateBarrier(new Vector3(-22f, 0.45f, 25.5f), 90f);

            if (!ArtGltfLoader.TryPlaceNamedMesh(kit, "sign_board", new Vector3(10f, 0f, 22f), Quaternion.Euler(0f, 90f, 0f),
                    new Color(0.12f, 0.35f, 0.55f), out _))
            {
                CreateBlock("Airside sign", new Vector3(10f, 1.1f, 22f), new Vector3(0.12f, 2.0f, 1.4f), new Color(0.12f, 0.35f, 0.55f));
                CreateBlock("Airside sign face", new Vector3(10.08f, 1.35f, 22f), new Vector3(0.04f, 0.9f, 1.1f), new Color(0.95f, 0.95f, 0.92f));
            }

            if (!ArtGltfLoader.TryPlaceNamedMesh(kit, "baggage_dolly", new Vector3(30f, 0f, 22f), Quaternion.identity,
                    new Color(0.55f, 0.35f, 0.18f), out _))
                CreateBlock("Dolly A", new Vector3(30f, 0.35f, 22f), new Vector3(1.6f, 0.7f, 0.9f), new Color(0.55f, 0.35f, 0.18f));
            if (!ArtGltfLoader.TryPlaceNamedMesh(kit, "baggage_dolly", new Vector3(32.2f, 0f, 22f), Quaternion.identity,
                    new Color(0.55f, 0.35f, 0.18f), out _))
                CreateBlock("Dolly B", new Vector3(32.2f, 0.35f, 22f), new Vector3(1.6f, 0.7f, 0.9f), new Color(0.55f, 0.35f, 0.18f));
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
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { color = color };
            if (color.a < 0.99f)
            {
                // Presentation translucency for heat shimmer / rain streaks.
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.renderQueue = 3000;
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }

            var texture = TryLoadArtTexture(artTextureRelativePath);
            if (texture == null)
                return material;

            material.mainTexture = texture;
            var tiling = textureTiling ?? Vector2.one;
            material.mainTextureScale = tiling;
            return material;
        }

        /// <summary>
        /// Loads Batch B PNGs from the Art folder on disk (Editor / unpacked data).
        /// Returns null when missing so solid-colour primitives remain the fallback.
        /// </summary>
        private static Texture2D TryLoadArtTexture(string artRelativePath)
        {
            if (string.IsNullOrEmpty(artRelativePath))
                return null;

            var fullPath = Path.Combine(Application.dataPath, "Airside", "Art", artRelativePath);
            if (!File.Exists(fullPath))
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
