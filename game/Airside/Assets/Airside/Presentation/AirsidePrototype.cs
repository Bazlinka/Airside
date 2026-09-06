using System;
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
        private Transform _aircraft;
        private Transform[] _groundTraffic;
        private Light _sun;
        private Transform _fuelTruck;
        private Transform _baggageCart;
        private Transform _passengerBus;
        private AirsideCameraController _cameraController;
        private double _preciseTime;
        private bool _paused;
        private int _speed = 1;
        private long _nextAutosaveSecond;
        private bool _showAwaySummary;

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
            _aircraft = BuildAircraft();
            _groundTraffic = new Transform[_simulation.GroundTraffic.Count];
            for (var index = 0; index < _groundTraffic.Length; index++)
                _groundTraffic[index] = BuildGroundTrafficAircraft(_simulation.GroundTraffic[index].Id.Value);
            _fuelTruck = BuildServiceVehicle("Fuel truck", new Color(0.92f, 0.78f, 0.18f), new Vector3(3.1f, 1.25f, 1.35f));
            _baggageCart = BuildServiceVehicle("Baggage cart", new Color(0.91f, 0.38f, 0.12f), new Vector3(2.3f, 0.8f, 1.15f));
            _passengerBus = BuildServiceVehicle("Passenger bus", new Color(0.17f, 0.58f, 0.78f), new Vector3(3.8f, 1.5f, 1.45f));
            _cameraController.SetFollowTarget(_aircraft);
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
        }

        private void UpdateAircraftVisual()
        {
            var standZ = _simulation.AssignedStand.Equals(AirportSimulation.StandOne) ? 14f : 20f;
            var phase = _simulation.ActiveAircraft.Phase;
            var progress = VisualPhaseProgress(0f);
            var position = PositionFor(phase, progress, standZ);
            var next = PositionFor(phase, VisualPhaseProgress(0.15f), standZ);
            _aircraft.position = position;

            var direction = next - position;
            if (direction.sqrMagnitude > 0.001f)
                _aircraft.rotation = Quaternion.Slerp(_aircraft.rotation, Quaternion.LookRotation(direction.normalized), Time.unscaledDeltaTime * 5f);
        }

        private void UpdateGroundTrafficVisual()
        {
            for (var index = 0; index < _groundTraffic.Length; index++)
            {
                var view = _groundTraffic[index];
                var point = _simulation.GroundTraffic[index].Position;
                var target = new Vector3(point.X, 0.7f, point.Z);
                var previous = view.position;
                view.position = Vector3.Lerp(previous, target, Time.unscaledDeltaTime * 3f);

                var direction = target - previous;
                if (direction.sqrMagnitude > 0.0004f)
                    view.rotation = Quaternion.Slerp(
                        view.rotation,
                        Quaternion.LookRotation(direction.normalized),
                        Time.unscaledDeltaTime * 4f);
            }
        }

        private float VisualPhaseProgress(float lookAheadSeconds)
        {
            if (_simulation.ActiveAircraft.IsComplete)
                return 1f;

            var elapsed = _preciseTime + lookAheadSeconds - _simulation.ActiveAircraft.PhaseStartedAt.ElapsedSeconds;
            return Mathf.Clamp01((float)(elapsed / _simulation.ActiveAircraft.PhaseDurationSeconds));
        }

        private void UpdateServiceVehicles()
        {
            var atStand = _simulation.ActiveAircraft.Phase == AircraftPhase.AtStand && _simulation.ActiveTurnaround != null;
            var standZ = _simulation.AssignedStand.Equals(AirportSimulation.StandOne) ? 14f : 20f;
            UpdateVehicle(_fuelTruck, atStand && TaskActive("Refuel"), new Vector3(13.3f, 0.55f, standZ + 1.8f));
            UpdateVehicle(_baggageCart, atStand && (TaskActive("Unload bags") || TaskActive("Load bags")), new Vector3(20.2f, 0.42f, standZ - 1.8f));
            UpdateVehicle(_passengerBus, atStand && (TaskActive("Passengers off") || TaskActive("Board passengers")), new Vector3(13f, 0.68f, standZ - 2.2f));
        }

        private bool TaskActive(string name)
        {
            return _simulation.ActiveTurnaround.Tasks(_clock.Now).Any(task => task.Name == name && task.State == TurnaroundTaskState.Active);
        }

        private static void UpdateVehicle(Transform vehicle, bool active, Vector3 position)
        {
            vehicle.gameObject.SetActive(active);
            if (active)
                vehicle.position = position;
        }

        private void OnGUI()
        {
            var scale = Mathf.Clamp(Screen.height / 720f, 0.8f, 1.35f);
            var previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            var panel = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(18, 18, 14, 14)
            };
            var title = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
            var detail = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            var small = new GUIStyle(GUI.skin.label) { fontSize = 13 };

            var timeOfDay = _simulation.TimeOfDay;

            GUI.Box(new Rect(22, 22, 410, 382), string.Empty, panel);
            GUI.Label(new Rect(42, 36, 320, 34), "AIRSIDE", title);
            GUI.Label(new Rect(42, 58, 380, 18), $"{_simulation.Location.Name}  ·  {_simulation.Location.Region}", small);
            GUI.Label(new Rect(42, 76, 320, 25), $"Flight {_simulation.ActiveAircraft.AircraftId}  ·  {_simulation.AssignedStand}", detail);
            GUI.Label(new Rect(42, 104, 320, 25), $"{FormatPhase(_simulation.ActiveAircraft.Phase)}  ·  {_simulation.ActiveAircraft.SecondsRemaining(_clock.Now)}s", detail);
            GUI.Label(new Rect(42, 132, 360, 22), $"{(_paused ? "PAUSED" : $"{_speed}× time")}  ·  Day {timeOfDay.DaysElapsed + 1} {timeOfDay.Clock} {timeOfDay.Phase}  ·  Cycles {_simulation.CompletedCycles}", small);
            GUI.Label(new Rect(42, 156, 360, 22), $"Cash: ${_simulation.Economy.Cash:N0}  ·  Reserved: {ReservationSummary()}", small);
            if (_simulation.TrafficWaits.HasWarning(_clock.Now))
                GUI.Label(new Rect(42, 178, 360, 22), $"TRAFFIC: {_simulation.TrafficWaits.Describe(_clock.Now)}", small);

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
                    GUI.Label(new Rect(42, 298, 360, 22), $"DELAY +{_simulation.CurrentDelaySeconds}s · {_simulation.CurrentDelayCause}", small);

                var alreadyAssigned = _simulation.ActiveTurnaround.PriorityCrewEnabled;
                GUI.enabled = !alreadyAssigned && _simulation.Economy.Cash >= AirportEconomy.PriorityCrewCost;
                if (GUI.Button(new Rect(42, 326, 190, 27), alreadyAssigned ? "Priority crew active" : "Hire priority crew · $300"))
                    _session.EnablePriorityCrew();
                GUI.enabled = true;
            }
            else
            {
                GUI.Label(new Rect(42, 184, 350, 22), _simulation.LastDelaySeconds > 0
                    ? $"Last flight delay: {_simulation.LastDelaySeconds}s · {_simulation.LastDelayCause}"
                    : "Operations running to schedule", small);
            }

            GUI.Label(new Rect(42, 368, 370, 25), "Space pause · Tab speed · P priority crew · F follow · O overview", small);

            var historyLeft = Screen.width / scale - 362;
            GUI.Box(new Rect(historyLeft, 22, 340, 190), string.Empty, panel);
            GUI.Label(new Rect(historyLeft + 20, 36, 300, 26), "OPERATIONS", detail);
            GUI.Label(new Rect(historyLeft + 20, 62, 300, 20), $"Taxi route: {_simulation.ActiveTaxiRoute.Name}", small);
            var trafficY = 80f;
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

            if (_showAwaySummary)
                DrawAwaySummary(scale, panel, title, detail, small);
            GUI.matrix = previousMatrix;
        }

        private void DrawAwaySummary(float scale, GUIStyle panel, GUIStyle title, GUIStyle detail, GUIStyle small)
        {
            var summary = _session.LastAwaySummary;
            var width = 430f;
            var height = 270f;
            var left = (Screen.width / scale - width) * 0.5f;
            var top = (Screen.height / scale - height) * 0.5f;
            GUI.Box(new Rect(left, top, width, height), string.Empty, panel);
            GUI.Label(new Rect(left + 24, top + 20, width - 48, 34), "WELCOME BACK", title);
            GUI.Label(new Rect(left + 24, top + 62, width - 48, 26), $"Airport operated for {FormatDuration(summary.AwaySeconds)}", detail);
            GUI.Label(new Rect(left + 24, top + 98, width - 48, 24), $"Flights completed: {summary.FlightsCompleted}", detail);
            GUI.Label(new Rect(left + 24, top + 128, width - 48, 24), $"Cash change: {summary.CashChange:+$#,0;-$#,0;$0}", detail);
            GUI.Label(new Rect(left + 24, top + 158, width - 48, 24), $"Delay costs: ${summary.DelayCost:N0}", detail);
            if (summary.RecoveredPreviousSave)
                GUI.Label(new Rect(left + 24, top + 188, width - 48, 20), "Recovered the previous safe copy.", small);
            else if (summary.ClockMovedBackwards)
                GUI.Label(new Rect(left + 24, top + 188, width - 48, 20), "Device clock moved backwards; no time was added.", small);
            if (GUI.Button(new Rect(left + 125, top + 220, 180, 30), "Continue operations"))
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

        private string ReservationSummary()
        {
            var resources = _simulation.Reservations.OccupiedResources.Select(resource => resource.Value).ToArray();
            return resources.Length == 0 ? "none" : string.Join(", ", resources);
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
        }

        private static void BuildAirfield()
        {
            CreateBlock("Grass", new Vector3(0f, -0.65f, 4f), new Vector3(94f, 1f, 66f), new Color(0.16f, 0.34f, 0.21f));
            CreateBlock("Runway", new Vector3(0f, -0.08f, 0f), new Vector3(78f, 0.15f, 7f), new Color(0.105f, 0.12f, 0.14f));
            CreateBlock("Taxiway A", new Vector3(8f, -0.02f, 9f), new Vector3(48f, 0.12f, 4f), new Color(0.22f, 0.24f, 0.26f));
            CreateBlock("Apron", new Vector3(20f, 0f, 17f), new Vector3(28f, 0.12f, 14f), new Color(0.34f, 0.36f, 0.37f));
            CreateBlock("Terminal", new Vector3(26f, 2.2f, 27f), new Vector3(22f, 4.5f, 5f), new Color(0.68f, 0.72f, 0.75f));
            CreateBlock("Terminal glass", new Vector3(26f, 2.4f, 24.45f), new Vector3(17f, 2.2f, 0.12f), new Color(0.16f, 0.38f, 0.5f));
            CreateBlock("Hangar", new Vector3(-20f, 2.5f, 20f), new Vector3(14f, 5f, 9f), new Color(0.45f, 0.5f, 0.54f));

            for (var x = -34; x <= 34; x += 8)
                CreateBlock("Runway marking", new Vector3(x, 0.02f, 0f), new Vector3(3.5f, 0.03f, 0.28f), Color.white);

            BuildStandMarking(17f, 14f, "Stand 1");
            BuildStandMarking(17f, 20f, "Stand 2");
        }

        private static void BuildStandMarking(float x, float z, string name)
        {
            CreateBlock(name, new Vector3(x, 0.08f, z), new Vector3(0.18f, 0.03f, 4.2f), new Color(0.96f, 0.77f, 0.12f));
            CreateBlock($"{name} stop", new Vector3(x, 0.08f, z + 1.9f), new Vector3(3.4f, 0.03f, 0.18f), new Color(0.96f, 0.77f, 0.12f));
        }

        private static Transform BuildAircraft()
        {
            var root = new GameObject("Active flight").transform;
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Fuselage";
            body.transform.SetParent(root, false);
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            body.transform.localScale = new Vector3(0.72f, 2.8f, 0.72f);
            body.GetComponent<Renderer>().material = CreateMaterial(new Color(0.93f, 0.95f, 0.97f));

            var wings = CreateBlock("Wings", Vector3.zero, new Vector3(7f, 0.12f, 2.2f), new Color(0.12f, 0.43f, 0.76f));
            wings.transform.SetParent(root, false);
            var tail = CreateBlock("Tail", new Vector3(0f, 0.65f, -2f), new Vector3(0.16f, 1.6f, 1.1f), new Color(0.12f, 0.43f, 0.76f));
            tail.transform.SetParent(root, false);

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

        private static Transform BuildGroundTrafficAircraft(string label)
        {
            var root = new GameObject($"Ground traffic {label}").transform;
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Fuselage";
            body.transform.SetParent(root, false);
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            body.transform.localScale = new Vector3(0.58f, 2.1f, 0.58f);
            body.GetComponent<Renderer>().material = CreateMaterial(new Color(0.96f, 0.86f, 0.5f));

            var wings = CreateBlock("Wings", Vector3.zero, new Vector3(5.4f, 0.1f, 1.7f), new Color(0.82f, 0.55f, 0.16f));
            wings.transform.SetParent(root, false);
            var tail = CreateBlock("Tail", new Vector3(0f, 0.5f, -1.5f), new Vector3(0.14f, 1.2f, 0.9f), new Color(0.82f, 0.55f, 0.16f));
            tail.transform.SetParent(root, false);

            root.position = new Vector3(8f, 0.7f, 9f);
            return root;
        }

        private static Transform BuildServiceVehicle(string name, Color color, Vector3 scale)
        {
            var root = new GameObject(name).transform;
            var body = CreateBlock($"{name} body", Vector3.zero, scale, color);
            body.transform.SetParent(root, false);
            var cab = CreateBlock($"{name} cab", new Vector3(scale.x * 0.28f, scale.y * 0.42f, 0f), new Vector3(scale.x * 0.34f, scale.y * 0.62f, scale.z * 0.86f), color * 0.82f);
            cab.transform.SetParent(root, false);
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

        private Vector3 PositionFor(AircraftPhase phase, float progress, float standZ)
        {
            return phase switch
            {
                AircraftPhase.Approach => Smooth(new Vector3(-52f, 14f, 0f), new Vector3(-35f, 2f, 0f), progress),
                AircraftPhase.Landing => Smooth(new Vector3(-35f, 2f, 0f), new Vector3(-24f, 0.7f, 0f), progress),
                AircraftPhase.TaxiIn => PositionAlongTaxiRoute(progress, false),
                AircraftPhase.AtStand => new Vector3(17f, 0.7f, standZ),
                AircraftPhase.Pushback => Smooth(new Vector3(17f, 0.7f, standZ), new Vector3(12f, 0.7f, standZ - 2f), progress),
                AircraftPhase.TaxiOut => progress < 0.15f
                    ? Smooth(new Vector3(12f, 0.7f, standZ - 2f), new Vector3(17f, 0.7f, standZ), progress / 0.15f)
                    : PositionAlongTaxiRoute((progress - 0.15f) / 0.85f, true),
                AircraftPhase.Takeoff => Smooth(new Vector3(28f, 0.7f, 0f), new Vector3(48f, 12f, 0f), progress),
                _ => new Vector3(52f, 15f, 0f)
            };
        }

        private Vector3 PositionAlongTaxiRoute(float progress, bool reverse)
        {
            var points = _simulation.ActiveTaxiRoute.Points;
            var segmentLengths = new float[points.Count - 1];
            var totalLength = 0f;
            for (var index = 0; index < segmentLengths.Length; index++)
            {
                var from = points[index];
                var to = points[index + 1];
                segmentLengths[index] = Vector2.Distance(new Vector2(from.X, from.Z), new Vector2(to.X, to.Z));
                totalLength += segmentLengths[index];
            }

            var distance = Mathf.Clamp01(reverse ? 1f - progress : progress) * totalLength;
            for (var index = 0; index < segmentLengths.Length; index++)
            {
                if (distance > segmentLengths[index])
                {
                    distance -= segmentLengths[index];
                    continue;
                }

                var from = points[index];
                var to = points[index + 1];
                var localProgress = segmentLengths[index] <= 0f ? 1f : distance / segmentLengths[index];
                return Smooth(new Vector3(from.X, 0.7f, from.Z), new Vector3(to.X, 0.7f, to.Z), localProgress);
            }

            var last = points[points.Count - 1];
            return new Vector3(last.X, 0.7f, last.Z);
        }

        private static Vector3 Smooth(Vector3 from, Vector3 to, float progress) => Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, progress));

        private static GameObject CreateBlock(string name, Vector3 position, Vector3 scale, Color color)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.position = position;
            block.transform.localScale = scale;
            block.GetComponent<Renderer>().material = CreateMaterial(color);
            return block;
        }

        private static Material CreateMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            return new Material(shader) { color = color };
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
