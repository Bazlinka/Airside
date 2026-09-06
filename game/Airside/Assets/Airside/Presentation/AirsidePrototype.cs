using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Airside.Domain;
using Airside.Persistence;
using Airside.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

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
        private Transform _rainRoot;
        private Transform _touchdownSmoke;
        private float _touchdownSmokeRemaining;
        private readonly Dictionary<string, AircraftPhase> _previousPhases = new Dictionary<string, AircraftPhase>();
        private readonly List<(Renderer Renderer, Color DryColor)> _wetSurfaces = new List<(Renderer, Color)>();
        private Transform _fuelTruck;
        private Transform _baggageCart;
        private Transform _passengerBus;
        private AirsideCameraController _cameraController;
        private double _preciseTime;
        private bool _paused;
        private bool _audioMuted;
        private int _speed = 1;
        private long _nextAutosaveSecond;
        private bool _showAwaySummary;
        private const float EngineVolumeRunning = 0.11f;
        private const float EngineVolumeIdle = 0.02f;
        private const float EngineVolumePausedScale = 0.28f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartPrototype()
        {
            if (FindFirstObjectByType<AirsidePrototype>() == null)
                new GameObject("Airside Prototype").AddComponent<AirsidePrototype>();
        }

        private void Awake()
        {
            var savePath = System.IO.Path.Combine(Application.persistentDataPath, "airside-save-v1.json");
            _session = PersistentAirportSession.LoadOrCreate(savePath, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), 24031996);
            _clock = _session.Clock;
            _simulation = _session.Simulation;
            _preciseTime = _clock.Now.ElapsedSeconds;
            _nextAutosaveSecond = _clock.Now.ElapsedSeconds + 15;
            _showAwaySummary = _session.LastAwaySummary.HasReport;

            BuildLightingAndCamera();
            BuildAirfield();
            _apronLights = BuildApronLights();
            _rainRoot = BuildRainRoot();
            _touchdownSmoke = BuildTouchdownSmoke();
            CollectWetSurfaces();
            _commercialAircraft = Array.Empty<Transform>();
            SyncCommercialAircraftViews();
            _groundTraffic = new Transform[_simulation.GroundTraffic.Count];
            for (var index = 0; index < _groundTraffic.Length; index++)
                _groundTraffic[index] = BuildGroundTrafficAircraft(_simulation.GroundTraffic[index].Id.Value);
            _fuelTruck = BuildServiceVehicle("Fuel truck", new Color(0.92f, 0.78f, 0.18f), new Vector3(3.1f, 1.25f, 1.35f));
            _baggageCart = BuildServiceVehicle("Baggage cart", new Color(0.91f, 0.38f, 0.12f), new Vector3(2.3f, 0.8f, 1.15f));
            _passengerBus = BuildServiceVehicle("Passenger bus", new Color(0.17f, 0.58f, 0.78f), new Vector3(3.8f, 1.5f, 1.45f));
            if (_commercialAircraft.Length > 0)
                _cameraController.SetFollowTarget(_commercialAircraft[0]);
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
            UpdateEngineAudio();
            UpdateWeatherPresentation();
            UpdateTouchdownSmoke();
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
            var landingLights = phase is AircraftPhase.Approach or AircraftPhase.Landing or AircraftPhase.Takeoff || (night && !airborne);

            for (var i = 0; i < aircraft.childCount; i++)
            {
                var child = aircraft.GetChild(i);
                if (child.name.StartsWith("Gear", StringComparison.Ordinal))
                    child.gameObject.SetActive(!airborne);
                else if (child.name.StartsWith("NavLight", StringComparison.Ordinal))
                    child.gameObject.SetActive(enginesOn || night);
                else if (child.name.StartsWith("Beacon", StringComparison.Ordinal))
                {
                    // Presentation-only strobe while engines are running.
                    child.gameObject.SetActive(enginesOn && (Mathf.FloorToInt(Time.unscaledTime * 2f) % 2 == 0));
                }
                else if (child.name.StartsWith("LandingLight", StringComparison.Ordinal))
                    child.gameObject.SetActive(landingLights);
            }
        }

        private static void UpdateCabinDoor(Transform aircraft, AircraftPhase phase)
        {
            // Presentation-only: cabin door swings open at stand, closes before pushback.
            var targetY = phase == AircraftPhase.AtStand ? -85f : 0f;
            for (var i = 0; i < aircraft.childCount; i++)
            {
                var child = aircraft.GetChild(i);
                if (!child.name.StartsWith("CabinDoor", StringComparison.Ordinal))
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
            for (var i = 0; i < aircraft.childCount; i++)
            {
                var child = aircraft.GetChild(i);
                if (!child.name.StartsWith("EngineHeat", StringComparison.Ordinal))
                    continue;

                child.gameObject.SetActive(enginesOn);
                if (!enginesOn)
                    continue;

                var pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 7f + child.GetInstanceID() * 0.01f);
                child.localScale = new Vector3(0.35f * pulse, 0.35f * pulse, 0.7f);
                var renderer = child.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var color = renderer.material.color;
                    color.a = 0.12f + 0.1f * pulse;
                    renderer.material.color = color;
                }
            }
        }

        private static void SpinPropellers(Transform aircraft, AircraftPhase phase)
        {
            // Presentation-only: props spin whenever the aircraft is not parked at stand.
            if (phase == AircraftPhase.AtStand)
                return;

            var degrees = Time.unscaledDeltaTime * 720f;
            for (var i = 0; i < aircraft.childCount; i++)
            {
                var child = aircraft.GetChild(i);
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
                _commercialAircraft[index] = BuildAircraft($"Commercial {flight.AircraftId}", color);
            }

            if (needed > 0)
                _cameraController.SetFollowTarget(_commercialAircraft[0]);
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
            for (var i = 0; i < vehicle.childCount; i++)
            {
                var child = vehicle.GetChild(i);
                if (child.name.IndexOf("wheel", StringComparison.OrdinalIgnoreCase) >= 0)
                    child.Rotate(Vector3.right, degrees, Space.Self);
            }
        }

        private static void ResetServiceLoopParts(Transform vehicle)
        {
            if (vehicle == null)
                return;

            for (var i = 0; i < vehicle.childCount; i++)
            {
                var child = vehicle.GetChild(i);
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

            for (var i = 0; i < vehicle.childCount; i++)
            {
                var child = vehicle.GetChild(i);
                if (!child.name.StartsWith(partPrefix, StringComparison.Ordinal))
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

            if (_rainRoot != null)
                _rainRoot.gameObject.SetActive(raining);

            if (raining && _rainRoot != null)
            {
                for (var i = 0; i < _rainRoot.childCount; i++)
                {
                    var drop = _rainRoot.GetChild(i);
                    var pos = drop.localPosition;
                    pos.y -= Time.unscaledDeltaTime * (12f + (i % 5));
                    if (pos.y < 0.5f)
                        pos.y = 18f + (i % 7);
                    pos.x += Time.unscaledDeltaTime * -1.5f;
                    if (pos.x < -40f)
                        pos.x += 80f;
                    drop.localPosition = pos;
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
                    _touchdownSmoke.position = _commercialAircraft[index].position + Vector3.up * 0.2f;
                    _touchdownSmoke.localScale = new Vector3(1.2f, 0.4f, 1.2f);
                    _touchdownSmoke.gameObject.SetActive(true);
                    _touchdownSmokeRemaining = 0.85f;
                }

                _previousPhases[id] = phase;
            }

            if (_touchdownSmokeRemaining <= 0f)
            {
                _touchdownSmoke.gameObject.SetActive(false);
                return;
            }

            _touchdownSmokeRemaining -= Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(_touchdownSmokeRemaining / 0.85f);
            _touchdownSmoke.localScale = Vector3.Lerp(new Vector3(2.4f, 0.2f, 2.4f), new Vector3(1.2f, 0.4f, 1.2f), t);
            var renderer = _touchdownSmoke.GetComponent<Renderer>();
            if (renderer != null)
            {
                var color = renderer.material.color;
                color.a = t * 0.45f;
                renderer.material.color = color;
            }
        }

        private void CollectWetSurfaces()
        {
            _wetSurfaces.Clear();
            foreach (var name in new[] { "Runway", "Taxiway A", "Apron" })
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
            for (var i = 0; i < 48; i++)
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
            var smoke = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            smoke.name = "Touchdown smoke";
            smoke.transform.localScale = new Vector3(1.2f, 0.4f, 1.2f);
            smoke.GetComponent<Renderer>().material = CreateMaterial(new Color(0.85f, 0.85f, 0.88f, 0.4f));
            var collider = smoke.GetComponent<Collider>();
            if (collider != null)
                Object.Destroy(collider);
            smoke.SetActive(false);
            return smoke.transform;
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
            var caution = AirsideTheme.TextStyle(new GUIStyle(small), AirsideTheme.SafetyYellow);
            var onTime = AirsideTheme.TextStyle(new GUIStyle(small), AirsideTheme.ClearGreen);
            var delayed = AirsideTheme.TextStyle(new GUIStyle(small), AirsideTheme.SignalRed);
            var button = AirsideTheme.TextStyle(new GUIStyle(GUI.skin.button), AirsideTheme.CoastalBlue);

            var timeOfDay = _simulation.TimeOfDay;

            GUI.Box(new Rect(22, 22, 410, 520), string.Empty, panel);
            GUI.Label(new Rect(42, 36, 320, 34), "AIRSIDE", title);
            GUI.Label(new Rect(42, 58, 380, 18), $"{_simulation.Location.Name}  ·  {_simulation.Location.Region}", small);
            GUI.Label(new Rect(42, 76, 380, 25), CommercialFlightHudLine(), detail);
            GUI.Label(new Rect(42, 104, 320, 25), $"{FormatPhase(_simulation.ActiveAircraft.Phase)}  ·  {_simulation.ActiveAircraft.SecondsRemaining(_clock.Now)}s", detail);
            var weatherLabel = Weather.Describe(_simulation.CurrentWeather);
            if (Weather.IsAdverse(_simulation.CurrentWeather))
                weatherLabel += " · wet apron";
            GUI.Label(new Rect(42, 132, 380, 22), $"{(_paused ? "PAUSED" : $"{_speed}× time")}{(_audioMuted ? "  ·  MUTED" : string.Empty)}  ·  Day {timeOfDay.DaysElapsed + 1} {timeOfDay.Clock} {timeOfDay.Phase}  ·  {weatherLabel}", small);
            GUI.Label(new Rect(42, 156, 390, 22), $"Cash: ${_simulation.Economy.Cash:N0}  ·  Cycles {_simulation.CompletedCycles}  ·  Reputation {_simulation.Reputation.Score} ({_simulation.Reputation.Band})", small);
            var finance = _simulation.DailyFinance;
            var runway = finance.CashRunwayDays is int days
                ? $"  ·  ~{days}d runway"
                : "  ·  cash building";
            GUI.Label(new Rect(42, 176, 390, 22),
                $"Day est. {finance.ExpectedNet:+$#,0;-$#,0;$0} (in ${finance.ExpectedFlightIncome:N0} / out ${finance.ExpectedOperatingCost:N0}){runway}", small);
            if (_simulation.TrafficWaits.HasWarning(_clock.Now))
                GUI.Label(new Rect(42, 198, 360, 22), $"TRAFFIC: {_simulation.TrafficWaits.Describe(_clock.Now)}", caution);

            var lineY = 180f;
            if (_simulation.ActiveAircraft.Phase == AircraftPhase.AtStand && _simulation.ActiveTurnaround != null)
            {
                foreach (var task in _simulation.ActiveTurnaround.Tasks(_clock.Now))
                {
                    var mark = task.State == TurnaroundTaskState.Complete ? "✓" : task.State == TurnaroundTaskState.Active ? "●" : "○";
                    var time = task.State == TurnaroundTaskState.Complete ? string.Empty : $"  {task.SecondsRemaining}s";
                    GUI.Label(new Rect(42, lineY, 350, 20), $"{mark} {task.Name}{time}", small);
                    lineY += 19f;
                }

                if (_simulation.CurrentDelaySeconds > 0)
                    GUI.Label(new Rect(42, 298, 360, 22), $"DELAY +{_simulation.CurrentDelaySeconds}s · {_simulation.CurrentDelayCause}", delayed);

                var alreadyAssigned = _simulation.ActiveTurnaround != null && _simulation.ActiveTurnaround.PriorityCrewEnabled;
                GUI.enabled = !alreadyAssigned && _simulation.Economy.Cash >= AirportEconomy.PriorityCrewCost;
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

            var staffing = _simulation.Staffing;
            GUI.Label(new Rect(42, 360, 380, 20),
                $"Ground crew: {staffing.GroundCrew}  ·  payroll ${staffing.DailyWage:N0}/day{(staffing.IsUnderstaffed ? "  ·  UNDERSTAFFED" : string.Empty)}",
                staffing.IsUnderstaffed ? caution : small);
            GUI.enabled = staffing.GroundCrew < AirportStaffing.MaximumGroundCrew && _simulation.Economy.Cash >= AirportStaffing.HireCost;
            if (GUI.Button(new Rect(42, 380, 150, 24), $"Hire crew · ${AirportStaffing.HireCost}", button))
                _session.HireGroundCrew();
            GUI.enabled = staffing.GroundCrew > AirportStaffing.MinimumGroundCrew;
            if (GUI.Button(new Rect(198, 380, 110, 24), "Release crew", button))
                _session.ReleaseGroundCrew();
            GUI.enabled = true;

            var capacity = _simulation.Capacity;
            GUI.Label(new Rect(42, 408, 380, 20),
                $"Stands: {capacity.StandCount} / {AirportCapacity.MaximumStands}", small);
            GUI.enabled = capacity.CanExpand && _simulation.Economy.Cash >= AirportCapacity.ThirdStandCost;
            if (GUI.Button(new Rect(42, 426, 220, 24),
                    capacity.HasThirdStand ? "Stand 3 built" : $"Build stand 3 · ${AirportCapacity.ThirdStandCost:N0}", button))
                _session.BuildThirdStand();
            GUI.enabled = true;

            var research = _simulation.Research;
            if (research.IsResearching)
            {
                var progress = (float)research.Progress01(_clock.Now);
                var pct = (int)(progress * 100);
                GUI.Label(new Rect(42, 454, 380, 20),
                    $"Research: {research.ActiveProjectName} {pct}% · {research.SecondsRemaining(_clock.Now)}s left", small);
                AirsideTheme.DrawProgressBar(
                    new Rect(42, 476, 280, 8),
                    progress,
                    AirsideTheme.CoastalBlue,
                    new Color(AirsideTheme.Tarmac.r, AirsideTheme.Tarmac.g, AirsideTheme.Tarmac.b, 0.85f));
            }
            else if (research.CanStartOperationsEfficiency)
            {
                GUI.Label(new Rect(42, 454, 380, 20),
                    $"Research: {AirportResearch.OperationsEfficiencyName} · -${AirportResearch.OperationsEfficiencyDailyDiscount}/day when done", small);
                GUI.enabled = _simulation.Economy.Cash >= AirportResearch.OperationsEfficiencyCost;
                if (GUI.Button(new Rect(42, 472, 260, 24), $"Start research · ${AirportResearch.OperationsEfficiencyCost:N0}", button))
                    _session.StartOperationsResearch();
                GUI.enabled = true;
            }
            else if (research.CanStartPassengerServices)
            {
                GUI.Label(new Rect(42, 454, 380, 20),
                    $"Research: {AirportResearch.PassengerServicesName} · +${AirportResearch.PassengerServicesRouteBonus}/flight when done", small);
                GUI.enabled = _simulation.Economy.Cash >= AirportResearch.PassengerServicesCost;
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
                GUI.Label(new Rect(42, 454, 380, 20),
                    $"Research: {ops}{(ops.Length > 0 && pax.Length > 0 ? " · " : string.Empty)}{pax}", small);
            }

            GUI.Label(new Rect(42, 500, 380, 25), "Space pause · Tab speed · P priority · M mute · F follow · O overview", small);

            var historyLeft = Screen.width / scale - 362;
            var accepted = _simulation.Routes.Accepted;
            var listedRoutes = accepted.Count == 0
                ? 1
                : Math.Min(4, accepted.Count) + (accepted.Count > 4 ? 1 : 0);
            // Header through routes summary (~80), schedule lines, fleet (2), event tail (4).
            var opsHeight = 80f + listedRoutes * 18f + 4f + 2 * 18f + 6f + 4 * 20f + 16f;
            GUI.Box(new Rect(historyLeft, 22, 340, opsHeight), string.Empty, panel);
            GUI.Label(new Rect(historyLeft + 20, 36, 300, 26), "OPERATIONS", detail);
            GUI.Label(new Rect(historyLeft + 20, 62, 320, 20),
                $"Routes {_simulation.Routes.Accepted.Count}  ·  {_simulation.Routes.ScheduledFlightsPerDay}/{_simulation.MaxScheduledFlightsPerDay} scheduled flights/day  ·  ${_simulation.Routes.IncomePerFlight + _simulation.Research.RouteIncomeBonus:N0}/flight", small);

            var trafficY = 80f;
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
            DrawRouteOffer(scale, panel, detail, small, caution, button, offerTop: 22f + opsHeight + 12f);
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

            if (_showAwaySummary)
                DrawAwaySummary(scale, panel, title, detail, small, button);
            GUI.matrix = previousMatrix;
        }

        private void DrawRouteOffer(float scale, GUIStyle panel, GUIStyle detail, GUIStyle small, GUIStyle caution, GUIStyle button, float offerTop = 244f)
        {
            var proposal = _simulation.Routes.Pending;
            if (proposal == null)
                return;

            var left = Screen.width / scale - 362;
            var top = offerTop;
            GUI.Box(new Rect(left, top, 340, 156), string.Empty, panel);
            GUI.Label(new Rect(left + 20, top + 14, 300, 24), "ROUTE OFFER", detail);
            GUI.Label(new Rect(left + 20, top + 42, 310, 20), $"{proposal.Airline}", small);
            GUI.Label(new Rect(left + 20, top + 62, 310, 20),
                $"{proposal.FlightsPerDay}/day to {proposal.Destination}", small);
            var payout = proposal.IncomePerFlight + _simulation.Reputation.IncomeBonus;
            GUI.Label(new Rect(left + 20, top + 82, 310, 20),
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
            GUI.Label(new Rect(left + 20, top + 102, 310, 20), status, blocked ? caution : small);

            GUI.enabled = meetsReputation && fitsCapacity;
            if (GUI.Button(new Rect(left + 20, top + 124, 150, 24), "Accept route", button))
                _session.AcceptRoute();
            GUI.enabled = true;
            if (GUI.Button(new Rect(left + 178, top + 124, 130, 24), "Decline", button))
                _session.DeclineRoute();
        }

        private void DrawAwaySummary(float scale, GUIStyle panel, GUIStyle title, GUIStyle detail, GUIStyle small, GUIStyle button)
        {
            var summary = _session.LastAwaySummary;
            var width = 430f;
            var height = 316f;
            var left = (Screen.width / scale - width) * 0.5f;
            var top = (Screen.height / scale - height) * 0.5f;
            GUI.Box(new Rect(left, top, width, height), string.Empty, panel);
            GUI.Label(new Rect(left + 24, top + 20, width - 48, 34), "WELCOME BACK", title);
            GUI.Label(new Rect(left + 24, top + 60, width - 48, 26), $"Airport operated for {FormatDuration(summary.AwaySeconds)}", detail);
            GUI.Label(new Rect(left + 24, top + 94, width - 48, 24), $"Flights completed: {summary.FlightsCompleted}", detail);
            GUI.Label(new Rect(left + 24, top + 122, width - 48, 24), $"Cash change: {summary.CashChange:+$#,0;-$#,0;$0}", detail);
            GUI.Label(new Rect(left + 24, top + 150, width - 48, 24), $"Route income: ${summary.RouteIncome:N0}", detail);
            GUI.Label(new Rect(left + 24, top + 178, width - 48, 24), $"Delay + running costs: ${summary.DelayCost + summary.OperatingCost:N0}", detail);
            GUI.Label(new Rect(left + 24, top + 206, width - 48, 24), $"Reputation: {summary.ReputationChange:+0;-0;0}  (now {_simulation.Reputation.Score})", detail);
            if (summary.RecoveredPreviousSave)
                GUI.Label(new Rect(left + 24, top + 234, width - 48, 20), "Recovered the previous safe copy.", small);
            else if (summary.ClockMovedBackwards)
                GUI.Label(new Rect(left + 24, top + 234, width - 48, 20), "Device clock moved backwards; no time was added.", small);
            if (GUI.Button(new Rect(left + 125, top + 264, 180, 30), "Continue operations", button))
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
            camera.fieldOfView = 48f;
            camera.clearFlags = CameraClearFlags.Skybox;

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

            RenderSettings.ambientLight = Color.Lerp(
                new Color(0.12f, 0.15f, 0.24f),
                new Color(0.46f, 0.53f, 0.61f),
                daylight);

            // Apron floods come up as daylight falls (presentation only).
            if (_apronLights != null)
            {
                var flood = Mathf.Lerp(1.35f, 0.05f, daylight);
                foreach (var light in _apronLights)
                    light.intensity = flood;
            }
        }

        private static Light[] BuildApronLights()
        {
            var positions = new[]
            {
                new Vector3(12f, 5.5f, 12f),
                new Vector3(28f, 5.5f, 12f),
                new Vector3(20f, 5.5f, 22f),
                new Vector3(-18f, 4.5f, 16f)
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
            // Batch C building silhouettes (procedural stand-ins for Approved glTF kits).
            CreateBlock("Terminal", new Vector3(26f, 2.2f, 27f), new Vector3(22f, 4.5f, 5f), new Color(0.68f, 0.72f, 0.75f));
            CreateBlock("Terminal glass", new Vector3(26f, 2.4f, 24.45f), new Vector3(17f, 2.2f, 0.12f), new Color(0.16f, 0.38f, 0.5f),
                "Textures/Environment/tx_terminal_glass_mask_v01.png", new Vector2(3f, 1.5f));
            CreateBlock("Terminal end L", new Vector3(14.8f, 2.0f, 27f), new Vector3(1.2f, 4.0f, 5.2f), new Color(0.62f, 0.66f, 0.69f));
            CreateBlock("Terminal end R", new Vector3(37.2f, 2.0f, 27f), new Vector3(1.2f, 4.0f, 5.2f), new Color(0.62f, 0.66f, 0.69f));
            CreateBlock("Terminal service", new Vector3(32f, 1.4f, 30.5f), new Vector3(8f, 2.8f, 3f), new Color(0.58f, 0.62f, 0.64f));
            CreateBlock("Hangar", new Vector3(-20f, 2.5f, 20f), new Vector3(14f, 5f, 9f), new Color(0.45f, 0.5f, 0.54f),
                "Textures/Surfaces/tx_corrugated_metal_basecolor_v01.png", new Vector2(2.5f, 1.5f));
            CreateBlock("Hangar door", new Vector3(-20f, 2.0f, 24.6f), new Vector3(8f, 4f, 0.2f), new Color(0.22f, 0.24f, 0.26f));
            CreateBlock("Ops shed", new Vector3(-8f, 1.4f, 26f), new Vector3(6f, 2.8f, 4f), new Color(0.55f, 0.58f, 0.52f),
                "Textures/Surfaces/tx_corrugated_metal_basecolor_v01.png", new Vector2(1.5f, 1.2f));
            CreateBlock("Edge light L", new Vector3(-30f, 0.2f, -3.4f), new Vector3(0.2f, 0.4f, 0.2f), new Color(0.95f, 0.95f, 0.85f));
            CreateBlock("Edge light R", new Vector3(-30f, 0.2f, 3.4f), new Vector3(0.2f, 0.4f, 0.2f), new Color(0.95f, 0.95f, 0.85f));
            CreateBlock("Windsock pole", new Vector3(-12f, 1.6f, 12f), new Vector3(0.12f, 3.2f, 0.12f), new Color(0.75f, 0.75f, 0.72f));

            CreateDecalQuad("Runway wear", new Vector3(0f, 0.02f, 0f), new Vector3(60f, 1f, 2.4f),
                "Textures/Decals/dc_runway_wear_v01.png");
            CreateDecalQuad("Apron stains", new Vector3(20f, 0.06f, 17f), new Vector3(18f, 1f, 10f),
                "Textures/Decals/dc_apron_stains_v01.png");

            for (var x = -34; x <= 34; x += 8)
                CreateBlock("Runway marking", new Vector3(x, 0.02f, 0f), new Vector3(3.5f, 0.03f, 0.28f), Color.white);

            for (var x = -36; x <= 36; x += 6)
            {
                CreateBlock("Runway edge L", new Vector3(x, 0.05f, -3.4f), new Vector3(0.25f, 0.1f, 0.25f), new Color(1f, 1f, 0.85f));
                CreateBlock("Runway edge R", new Vector3(x, 0.05f, 3.4f), new Vector3(0.25f, 0.1f, 0.25f), new Color(1f, 1f, 0.85f));
            }
            for (var x = -4; x <= 28; x += 4)
                CreateBlock("Taxi centre", new Vector3(x, 0.04f, 9f), new Vector3(1.2f, 0.03f, 0.18f), new Color(0.95f, 0.85f, 0.2f));

            BuildStandMarking(17f, 14f, "Stand 1");
            BuildStandMarking(17f, 20f, "Stand 2");
        }

        private static void BuildStandMarking(float x, float z, string name)
        {
            CreateBlock(name, new Vector3(x, 0.08f, z), new Vector3(0.18f, 0.03f, 4.2f), new Color(0.96f, 0.77f, 0.12f));
            CreateBlock($"{name} stop", new Vector3(x, 0.08f, z + 1.9f), new Vector3(3.4f, 0.03f, 0.18f), new Color(0.96f, 0.77f, 0.12f));
        }

        private static Transform BuildAircraft(string name, Color accent)
        {
            // Batch C turboprop silhouette with separated props/engines/gear (primitive fallback).
            var root = new GameObject(name).transform;
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Fuselage";
            body.transform.SetParent(root, false);
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            body.transform.localScale = new Vector3(0.72f, 2.8f, 0.72f);
            body.GetComponent<Renderer>().material = CreateMaterial(new Color(0.93f, 0.95f, 0.97f));

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
            ParentBlock(root, "NavLight L", new Vector3(-3.7f, 0.08f, 0.2f), new Vector3(0.12f, 0.12f, 0.12f), new Color(0.1f, 0.9f, 0.2f));
            ParentBlock(root, "NavLight R", new Vector3(3.7f, 0.08f, 0.2f), new Vector3(0.12f, 0.12f, 0.12f), new Color(0.9f, 0.12f, 0.12f));
            ParentBlock(root, "Beacon", new Vector3(0f, 0.85f, 0.2f), new Vector3(0.14f, 0.14f, 0.14f), new Color(0.95f, 0.2f, 0.15f));
            ParentBlock(root, "LandingLight", new Vector3(0f, -0.15f, 2.5f), new Vector3(0.18f, 0.12f, 0.2f), new Color(0.95f, 0.95f, 0.85f));
            ParentBlock(root, "CabinDoor", new Vector3(0.55f, 0.05f, 0.35f), new Vector3(0.08f, 0.85f, 0.55f), new Color(0.78f, 0.8f, 0.83f));
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

        private static void ParentBlock(Transform parent, string name, Vector3 localPosition, Vector3 scale, Color color)
        {
            var block = CreateBlock(name, localPosition, scale, color);
            block.transform.SetParent(parent, false);
            block.transform.localPosition = localPosition;
        }

        private static Transform BuildGroundTrafficAircraft(string label)
        {
            var root = BuildAircraft($"Ground traffic {label}", new Color(0.82f, 0.55f, 0.16f));
            root.localScale = new Vector3(0.82f, 0.82f, 0.82f);
            root.position = new Vector3(8f, 0.7f, 9f);
            return root;
        }

        private static Transform BuildServiceVehicle(string name, Color color, Vector3 scale)
        {
            var root = new GameObject(name).transform;
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
            // Batch D service-loop hooks (presentation only).
            ParentBlock(root, "Hose", new Vector3(scale.x * 0.45f, 0.15f, 0.2f),
                new Vector3(0.12f, 0.12f, 0.4f), new Color(0.25f, 0.25f, 0.28f));
            ParentBlock(root, "Cargo", new Vector3(0f, 0.35f, 0f),
                new Vector3(0.55f, 0.35f, 0.45f), new Color(0.75f, 0.55f, 0.2f));
            ParentBlock(root, "Door", new Vector3(scale.x * 0.2f, 0.25f, scale.z * 0.45f),
                new Vector3(0.08f, 0.7f, 0.45f), new Color(0.2f, 0.22f, 0.25f));
            root.gameObject.SetActive(false);
            return root;
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


        private string CommercialFlightHudLine()
        {
            if (_simulation.Flights.Count == 0)
                return "No commercial flights";

            var parts = new string[_simulation.Flights.Count];
            for (var index = 0; index < _simulation.Flights.Count; index++)
            {
                var flight = _simulation.Flights[index];
                parts[index] =
                    $"{flight.AircraftId} @ {flight.AssignedStand.Value} · {FormatPhase(flight.Operation.Phase)}";
            }

            return _simulation.Flights.Count == 1
                ? $"Flight {parts[0]}"
                : $"Flights {string.Join(" · ", parts)}";
        }

        private static string FormatPhase(AircraftPhase phase) => phase switch
        {
            AircraftPhase.TaxiIn => "Taxiing to stand",
            AircraftPhase.AtStand => "Turnaround at stand",
            AircraftPhase.TaxiOut => "Taxiing to runway",
            _ => phase.ToString()
        };
    }
}
