using System;
using System.IO;
using System.Linq;
using Airside.Simulation;
using Unity.Profiling;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Unattended soak mode for packaged builds (PROJECT_PLAN: a 30-minute soak with
    /// no crash, deadlock or unexplained stop). Launch with
    /// <c>-airsideSoak [-airsideSoakMinutes N]</c>: a fresh airline flies itself in live time,
    /// a heartbeat is logged to Player.log every real minute, and the app quits with
    /// a verdict when time is up. Soak saves go to their own file, never the player's.
    /// </summary>
    public sealed partial class AirsidePrototype
    {
        private const string SoakFlag = "-airsideSoak";
        private const string SoakMinutesFlag = "-airsideSoakMinutes";
        private const string SoakHeartbeatFlag = "-airsideSoakHeartbeatSeconds";
        private const string SoakHideWorldFlag = "-airsideSoakHideWorld";
        private const string SoakHideAircraftFlag = "-airsideSoakHideAircraft";
        private const string SoakHideWorldPrefixFlag = "-airsideSoakHideWorldPrefix";
        private const string SoakLogTag = "[Airside soak]";

        private static bool? _soakRequested;
        private float _soakMinutes = 30f;
        private float _soakStartedAt = -1f;
        private float _soakNextHeartbeat;
        private float _soakHeartbeatSeconds = 60f;
        private long _soakLastClock = -1;
        private int _soakStalledBeats;
        private int _soakFrames;
        private readonly float[] _soakFrameMs = new float[8192];
        private int _soakFrameSamples;
        private int _soakSlowFrames;
        private float _soakWorstFrameMs;
        private float _soakLastHeartbeatAt;
        private ProfilerRecorder _soakMainThreadRecorder;
        private ProfilerRecorder _soakRenderThreadRecorder;
        private ProfilerRecorder _soakSystemMemoryRecorder;
        private ProfilerRecorder _soakDrawCallsRecorder;
        private ProfilerRecorder _soakBatchesRecorder;
        private ProfilerRecorder _soakSetPassRecorder;
        private long _soakDrawCalls;
        private long _soakBatches;
        private long _soakSetPass;
        private int _soakRenderStatsSamples;
        private long _soakMainThreadNs;
        private long _soakRenderThreadNs;
        private int _soakProfilerSamples;
        private long _soakFleetTicks;
        private long _soakSkyTicks;
        private long _soakGroundTicks;
        private long _soakOpsTicks;
        private int _soakOpsCalls;
        private long _soakUpdateTicks;
        private long _soakHudTicks;
        private int _soakHudCalls;
        private bool _soakRenderSetDescribed;
        private readonly SeededRandomSource _soakChoices = new(31337);

        private static bool SoakMode
        {
            get
            {
                _soakRequested ??= Array.IndexOf(Environment.GetCommandLineArgs(), SoakFlag) >= 0;
                return _soakRequested.Value;
            }
        }

        private static string SavePath => SoakMode
            ? Path.Combine(Application.persistentDataPath, "airline-save-soak.json")
            : AirlineSaveFile.DefaultPath;

        // Review shots for packaged-build checks (HUD fit at several window sizes, panels):
        //   -airsideReviewPanel plan|operations|map|fleet|contracts|stats|devtools|help
        //   -airsideReviewShot <path.png> [-airsideReviewDelay seconds]   capture, then quit
        //   -airsideReviewAircraft <registration>   follow a live 3D aircraft in the shot
        //   -airsideReviewFollowZoom 0.35   bounded close-up of the followed aircraft
        //   -airsideReviewTime HH:mm   override local lighting time only (not the sim clock)
        //   -airsideReviewWeather cloudy|overcast|rain|storm|...   deterministic visual QA
        private const string ReviewPanelFlag = "-airsideReviewPanel";
        private const string ReviewShotFlag = "-airsideReviewShot";
        private const string ReviewDelayFlag = "-airsideReviewDelay";
        private const string ReviewAircraftFlag = "-airsideReviewAircraft";
        private float _reviewShotAt = -1f;
        private bool _reviewShotTaken;
        private bool _reviewFollowStarted;
        private string _reviewAircraftId;

        private void OpenReviewPanel(string[] args)
        {
            var index = Array.IndexOf(args, ReviewPanelFlag);
            if (index < 0 || index + 1 >= args.Length)
                return;
            switch (args[index + 1])
            {
                case "plan": OpenPlanner(null); break;
                case "hangar":
                case "fleet": SetWorkspace(HudWorkspace.Fleet); break;
                case "flights":
                case "operations": SetWorkspace(HudWorkspace.Operations); break;
                case "operations-all":
                    _operationsAllMovements = true;
                    SetWorkspace(HudWorkspace.Operations);
                    break;
                case "map": OpenPlanner(null); break;
                case "contracts": SetWorkspace(HudWorkspace.Contracts); break;
                case "stats": SetWorkspace(HudWorkspace.Stats); break;
                case "devtools": ToggleDevTools(); break;
                case "help": ToggleControlsHelp(); break;
            }
        }

        /// <summary>Read the finished frame (3D and HUD) back and write it as PNG. The ScreenCapture module is not in this project.</summary>
        private System.Collections.IEnumerator CaptureReviewShot(string path)
        {
            yield return new WaitForEndOfFrame();
            var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Destroy(texture);
            Debug.Log($"{SoakLogTag} review shot {path} at {Screen.width}x{Screen.height}");
        }

        private void DriveReviewShot()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, ReviewShotFlag);
            if (index < 0 || index + 1 >= args.Length)
                return;
            if (_reviewShotAt < 0f)
            {
                var delayIndex = Array.IndexOf(args, ReviewDelayFlag);
                var delay = delayIndex >= 0 && delayIndex + 1 < args.Length
                            && float.TryParse(args[delayIndex + 1], System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out var d)
                    ? d
                    : 20f;
                _reviewShotAt = Time.unscaledTime + delay;
            }

            if (!_reviewShotTaken && Time.unscaledTime >= _reviewShotAt)
            {
                _reviewShotTaken = true;
                StartCoroutine(CaptureReviewShot(args[index + 1]));
            }
            else if (_reviewShotTaken && Time.unscaledTime >= _reviewShotAt + 3f)
            {
                Application.Quit();
            }
        }

        private void DriveSoak()
        {
            if (!SoakMode)
                return;

            _soakFrames++;
            var frameMs = Time.unscaledDeltaTime * 1000f;
            if (_soakFrameSamples < _soakFrameMs.Length)
                _soakFrameMs[_soakFrameSamples++] = frameMs;
            if (frameMs > 33.3f)
                _soakSlowFrames++;
            if (frameMs > _soakWorstFrameMs)
                _soakWorstFrameMs = frameMs;
            if (_soakStartedAt < 0f)
            {
                var args = Environment.GetCommandLineArgs();
                var index = Array.IndexOf(args, SoakMinutesFlag);
                // Invariant culture, like every other numeric launch flag: under a comma-decimal
                // macOS locale "-airsideSoakMinutes 0.5" failed to parse and ran the full 30.
                if (index >= 0 && index + 1 < args.Length
                    && float.TryParse(args[index + 1], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var minutes)
                    && minutes > 0f)
                    _soakMinutes = minutes;

                var heartbeatIndex = Array.IndexOf(args, SoakHeartbeatFlag);
                if (heartbeatIndex >= 0 && heartbeatIndex + 1 < args.Length
                    && float.TryParse(args[heartbeatIndex + 1], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var heartbeatSeconds)
                    && heartbeatSeconds >= 5f)
                    _soakHeartbeatSeconds = heartbeatSeconds;

                _soakStartedAt = Time.unscaledTime;
                _soakNextHeartbeat = _soakStartedAt + _soakHeartbeatSeconds;
                _soakLastHeartbeatAt = _soakStartedAt;
                _soakMainThreadRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "CPU Main Thread Frame Time", 1);
                _soakRenderThreadRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "CPU Render Thread Frame Time", 1);
                _soakSystemMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory", 1);
                _soakDrawCallsRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 1);
                _soakBatchesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count", 1);
                _soakSetPassRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count", 1);
                _saveProbed = true; // never offer or read the player's save
                StartAirline("Soak Air");
                ApplySoakRenderIsolation(args);
                Debug.Log($"{SoakLogTag} started for {_soakMinutes:0} min in live time");
                var followIndex = Array.IndexOf(args, ReviewAircraftFlag);
                _reviewAircraftId = followIndex >= 0 && followIndex + 1 < args.Length
                    ? args[followIndex + 1] : null;
                OpenReviewPanel(args);
            }
            if (_soakMainThreadRecorder.Valid && _soakRenderThreadRecorder.Valid)
            {
                _soakMainThreadNs += _soakMainThreadRecorder.LastValue;
                _soakRenderThreadNs += _soakRenderThreadRecorder.LastValue;
                _soakProfilerSamples++;
            }
            if (_soakDrawCallsRecorder.Valid && _soakBatchesRecorder.Valid && _soakSetPassRecorder.Valid)
            {
                _soakDrawCalls += _soakDrawCallsRecorder.LastValue;
                _soakBatches += _soakBatchesRecorder.LastValue;
                _soakSetPass += _soakSetPassRecorder.LastValue;
                _soakRenderStatsSamples++;
            }
            DriveReviewShot();
            if (!_soakRenderSetDescribed && Time.unscaledTime >= _soakStartedAt + 2f)
            {
                _soakRenderSetDescribed = true;
                DescribeSoakRenderSet();
            }
            if (!_reviewFollowStarted && !string.IsNullOrEmpty(_reviewAircraftId))
            {
                _reviewFollowStarted = TryFollowFleetAircraft(_reviewAircraftId);
                if (_reviewFollowStarted)
                    Debug.Log($"{SoakLogTag} following {_reviewAircraftId}");
            }

            if (_awaySummary != null)
                _awaySummary = null;
            _menuOpen = false;
            _optionsOpen = false;

            foreach (var aircraft in _operations.FleetOf(_operations.PlayerAirline))
            {
                if (aircraft.State == FleetState.AtStand && !aircraft.Scheduled.HasValue)
                {
                    // An aircraft type that cannot reach anything in the catalogue would have
                    // indexed an empty list and ended the unattended run with an exception.
                    var reachable = _operations.MapDestinations().Where(d => _operations.CanOperate(aircraft, d)
                        && _operations.CareerState.CanAfford(
                            _operations.DispatchCost(aircraft.Type, _operations.DistanceKm(d)))).ToList();
                    if (reachable.Count == 0)
                        continue;
                    var destination = reachable[_soakChoices.NextInt(0, reachable.Count)];
                    // The first flight leaves four minutes in, so every soak covers a full
                    // engine start early; later ones are spread over half an hour.
                    var delay = aircraft.CompletedTrips == 0
                        ? 4 * 60
                        : _soakChoices.NextInt((int)EngineStartSequence.MinimumDepartureLeadSeconds, 1800);
                    _operations.ScheduleDeparture(aircraft, destination, _clock.Now.Advance(delay));
                }
                else if (aircraft.State == FleetState.AwaitingStand)
                {
                    foreach (var stand in _operations.FreeStandsFor(aircraft.Type))
                    {
                        if (_operations.AssignStand(aircraft, stand).Accepted)
                            break;
                    }
                }
            }

            var now = Time.unscaledTime;
            if (now >= _soakNextHeartbeat)
            {
                _soakNextHeartbeat += _soakHeartbeatSeconds;
                var clock = _clock.Now.ElapsedSeconds;
                _soakStalledBeats = clock == _soakLastClock ? _soakStalledBeats + 1 : 0;
                _soakLastClock = clock;
                var trips = _operations.Fleet.Sum(a => a.CompletedTrips);
                var states = string.Join(", ", _operations.Fleet.Select(a =>
                {
                    var e = EngineStartSequence.For(a, _preciseTime);
                    return $"{a.Registration} {a.State} eng L{e.Left:0.00}/R{e.Right:0.00}{(e.Beacon ? " beacon" : "")}{(e.DoorsOpen ? " doors" : "")}";
                }));
                Array.Sort(_soakFrameMs, 0, _soakFrameSamples);
                var p95 = _soakFrameSamples > 0
                    ? _soakFrameMs[Math.Max(0, (int)Math.Ceiling(_soakFrameSamples * 0.95) - 1)]
                    : 0f;
                var mainMs = _soakProfilerSamples > 0 ? _soakMainThreadNs / (1_000_000f * _soakProfilerSamples) : 0f;
                var renderMs = _soakProfilerSamples > 0 ? _soakRenderThreadNs / (1_000_000f * _soakProfilerSamples) : 0f;
                var systemMb = _soakSystemMemoryRecorder.Valid
                    ? _soakSystemMemoryRecorder.LastValue / (1024 * 1024) : -1;
                var stageScale = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                var stageFrames = Math.Max(1, _soakFrames);
                var fleetMs = _soakFleetTicks * stageScale / stageFrames;
                var skyMs = _soakSkyTicks * stageScale / stageFrames;
                var groundMs = _soakGroundTicks * stageScale / stageFrames;
                var opsMs = _soakOpsTicks * stageScale / Math.Max(1, _soakOpsCalls);
                var updateMs = _soakUpdateTicks * stageScale / stageFrames;
                var hudMs = _soakHudTicks * stageScale / stageFrames;
                Debug.Log($"{SoakLogTag} {(now - _soakStartedAt) / 60f:0} min · sim {AirlineClockText()} · trips {trips} · " +
                          $"focus {Application.isFocused} · vsync {QualitySettings.vSyncCount} · target {Application.targetFrameRate} · " +
                          $"fps {_soakFrames / Math.Max(0.01f, now - _soakLastHeartbeatAt):0} · p95 {p95:0.0} ms · " +
                          $">33ms {_soakSlowFrames} · worst {_soakWorstFrameMs:0.0} ms · cpu main/render {mainMs:0.0}/{renderMs:0.0} ms · " +
                          $"stage update/hud {updateMs:0.00}/{hudMs:0.00} ms ({_soakHudCalls} GUI) · fleet/sky/ground {fleetMs:0.00}/{skyMs:0.00}/{groundMs:0.00} ms · ops {opsMs:0.00} ms/{_soakOpsCalls} updates · " +
                          $"draw/batch/setpass {(_soakRenderStatsSamples > 0 ? _soakDrawCalls / _soakRenderStatsSamples : -1)}/{(_soakRenderStatsSamples > 0 ? _soakBatches / _soakRenderStatsSamples : -1)}/{(_soakRenderStatsSamples > 0 ? _soakSetPass / _soakRenderStatsSamples : -1)} · " +
                          $"system/gc {systemMb}/{GC.GetTotalMemory(false) / (1024 * 1024)} MB · {states}");
                _soakFrames = 0;
                _soakFrameSamples = 0;
                _soakFleetTicks = 0;
                _soakSkyTicks = 0;
                _soakUpdateTicks = 0;
                _soakHudTicks = 0;
                _soakHudCalls = 0;
                _soakGroundTicks = 0;
                _soakOpsTicks = 0;
                _soakOpsCalls = 0;
                _soakSlowFrames = 0;
                _soakWorstFrameMs = 0f;
                _soakLastHeartbeatAt = now;
                _soakMainThreadNs = 0;
                _soakRenderThreadNs = 0;
                _soakProfilerSamples = 0;
                _soakDrawCalls = 0;
                _soakBatches = 0;
                _soakSetPass = 0;
                _soakRenderStatsSamples = 0;
                if (_soakStalledBeats >= 2)
                    Debug.LogError($"{SoakLogTag} STALL: simulation clock has not moved for two minutes");
            }

            if (now - _soakStartedAt >= _soakMinutes * 60f)
            {
                var trips = _operations.Fleet.Sum(a => a.CompletedTrips);
                Debug.Log($"{SoakLogTag} COMPLETE after {_soakMinutes:0} min · sim {AirlineClockText()} · trips {trips} · " +
                          $"stalls {(_soakStalledBeats > 0 ? "yes" : "none")}");
                QuitGame();
            }
        }

        // A/B only: isolate the renderer cost without changing simulation, saves or player launches.
        // Keep both flags off for the acceptance soak.
        private void ApplySoakRenderIsolation(string[] args)
        {
            var hideWorld = Array.IndexOf(args, SoakHideWorldFlag) >= 0;
            var hideAircraft = Array.IndexOf(args, SoakHideAircraftFlag) >= 0;
            var prefixIndex = Array.IndexOf(args, SoakHideWorldPrefixFlag);
            var hiddenPrefix = prefixIndex >= 0 && prefixIndex + 1 < args.Length
                ? args[prefixIndex + 1] : null;
            var hiddenPrefixes = hiddenPrefix?.Split(',');
            var worldGroups = new System.Collections.Generic.Dictionary<string, int>(StringComparer.Ordinal);
            var remainingWorld = new System.Collections.Generic.List<string>();
            var aircraftRenderers = new System.Collections.Generic.HashSet<Renderer>();
            if (_commercialAircraft != null)
                foreach (var aircraft in _commercialAircraft)
                    if (aircraft != null)
                        foreach (var renderer in aircraft.GetComponentsInChildren<Renderer>(true))
                            aircraftRenderers.Add(renderer);

            var worldCount = 0;
            foreach (var renderer in AirsideSceneIndex.Renderers)
            {
                if (renderer == null || aircraftRenderers.Contains(renderer))
                    continue;
                worldCount++;
                var name = renderer.gameObject.name;
                var separator = name.IndexOf(' ');
                var group = separator > 0 ? name.Substring(0, separator) : name;
                worldGroups.TryGetValue(group, out var groupCount);
                worldGroups[group] = groupCount + 1;
                var selectedForHiding = hideWorld || (hiddenPrefixes != null && hiddenPrefixes.Any(prefix =>
                    name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));
                if (selectedForHiding)
                    renderer.enabled = false;
                else if (hiddenPrefixes != null && remainingWorld.Count < 80)
                    remainingWorld.Add(name);
            }
            Debug.Log($"{SoakLogTag} largest world renderer groups: " +
                      string.Join(", ", worldGroups.OrderByDescending(pair => pair.Value)
                          .Take(20).Select(pair => $"{pair.Key} {pair.Value}")));
            if (hiddenPrefixes != null)
                Debug.Log($"{SoakLogTag} remaining world: " + string.Join(", ", remainingWorld));
            if (hideAircraft)
                foreach (var renderer in aircraftRenderers)
                    if (renderer != null)
                        renderer.enabled = false;
            Debug.Log($"{SoakLogTag} render set: world {worldCount} ({(hideWorld ? "hidden" : "visible")}), " +
                      $"aircraft {aircraftRenderers.Count} ({(hideAircraft ? "hidden" : "visible")}), " +
                      $"world prefix {hiddenPrefix ?? "none"}");
        }

        private static void DescribeSoakRenderSet()
        {
            var transparentRenderers = 0;
            var transparentTriangles = 0L;
            var activeRenderers = 0;
            var groups = new System.Collections.Generic.Dictionary<string, (int Renderers, long Triangles)>(
                StringComparer.Ordinal);
            var renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var renderer in renderers)
            {
                if (renderer == null || !renderer.enabled || renderer.forceRenderingOff
                    || !renderer.gameObject.activeInHierarchy)
                    continue;
                activeRenderers++;
                var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                var triangles = 0L;
                if (mesh != null)
                {
                    // Runtime static batching gives every source renderer the same combined
                    // mesh. Count only the submesh range owned by this renderer, otherwise
                    // the diagnostic multiplies the entire airport mesh by every fixture.
                    var firstSubMesh = renderer is MeshRenderer meshRenderer && meshRenderer.isPartOfStaticBatch
                        ? meshRenderer.subMeshStartIndex
                        : 0;
                    var subMeshCount = Math.Max(1, renderer.sharedMaterials.Length);
                    var lastSubMesh = Math.Min(mesh.subMeshCount, firstSubMesh + subMeshCount);
                    for (var subMesh = firstSubMesh; subMesh < lastSubMesh; subMesh++)
                        if (mesh.GetTopology(subMesh) == MeshTopology.Triangles)
                            triangles += (long)mesh.GetIndexCount(subMesh) / 3L;
                }
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null || material.renderQueue < (int)UnityEngine.Rendering.RenderQueue.Transparent)
                        continue;
                    transparentRenderers++;
                    transparentTriangles += triangles;
                    var name = renderer.gameObject.name;
                    var separator = name.IndexOf(' ');
                    var prefix = separator > 0 ? name.Substring(0, separator) : name;
                    var key = $"{material.name} / {material.shader.name} / {prefix}";
                    groups.TryGetValue(key, out var group);
                    groups[key] = (group.Renderers + 1, group.Triangles + triangles);
                }
            }

            Debug.Log($"{SoakLogTag} active renderers {activeRenderers}; transparent material slots " +
                      $"{transparentRenderers}; transparent triangles {transparentTriangles}");
            Debug.Log($"{SoakLogTag} largest transparent groups: " +
                      string.Join(", ", groups.OrderByDescending(pair => pair.Value.Renderers)
                          .ThenByDescending(pair => pair.Value.Triangles)
                          .Take(40)
                          .Select(pair => $"{pair.Key} {pair.Value.Renderers}/{pair.Value.Triangles}")));
        }

        private void DisposeSoakRecorders()
        {
            if (_soakMainThreadRecorder.Valid)
                _soakMainThreadRecorder.Dispose();
            if (_soakRenderThreadRecorder.Valid)
                _soakRenderThreadRecorder.Dispose();
            if (_soakSystemMemoryRecorder.Valid)
                _soakSystemMemoryRecorder.Dispose();
            if (_soakDrawCallsRecorder.Valid)
                _soakDrawCallsRecorder.Dispose();
            if (_soakBatchesRecorder.Valid)
                _soakBatchesRecorder.Dispose();
            if (_soakSetPassRecorder.Valid)
                _soakSetPassRecorder.Dispose();
        }

        private string AirlineClockText() =>
            $"{_operations.Clock.DateText(_clock.Now)} {_operations.Clock.TimeText(_clock.Now)}";
    }
}
