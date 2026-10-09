using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    public sealed partial class AirsidePrototype
    {
        // Hidden, opt-in command automation. Never used by ordinary player launches.
        private const string AgentGameplayFlag = "-airsideAgentGameplay";
        private static string AgentGameplayPlanPath
        {
            get
            {
                var args = Environment.GetCommandLineArgs();
                var i = Array.IndexOf(args, AgentGameplayFlag);
                if (i < 0) return null;
                if (i + 1 >= args.Length || args[i + 1].StartsWith("-"))
                    throw new ArgumentException("Agent gameplay needs a plan path");
                return Path.GetFullPath(args[i + 1]);
            }
        }
        private bool _agentGameplayActive;
        private FleetAircraft _agentGameplayAircraft;
        private AgentGameplayPlan _agentGameplayPlan;
        private readonly List<AgentGameplayResult> _agentGameplayResults = new();
        private string _agentGameplayError;
        private readonly List<string> _agentRuntimeErrors = new();
        private float _agentGameplayStarted;
        private static string AgentGameplaySavePath
        {
            get
            {
                if (AgentGameplayPlanPath != null)
                    return Path.Combine(Path.GetDirectoryName(AgentGameplayPlanPath), "test-save.json");
                var args = Environment.GetCommandLineArgs();
                var index = Array.IndexOf(args, "-airsideAgentSaveDirectory");
                if (index < 0) return null;
                if (index + 1 >= args.Length || args[index + 1].StartsWith("-"))
                    throw new ArgumentException("Agent save directory is missing");
                return Path.Combine(Path.GetFullPath(args[index + 1]), "test-save.json");
            }
        }

        [Serializable] private sealed class AgentGameplayPlan
        {
            public int protocol;
            public string expectedCommit, issue, aircraftType;
            public AgentGameplayStep[] steps;
        }
        [Serializable] private sealed class AgentGameplayStep
        {
            public string id, action, value, expectState;
            public bool capture;
            public float settleSeconds;
        }
        [Serializable] private sealed class AgentGameplayResult
        {
            public string id, action, status, detail, screenshot;
            public float realSeconds;
            public long simulationSeconds;
            public string aircraft, state, weather, workspace;
            public string[] fleetStates;
            public float cameraPitch, cameraYaw, cameraDistance;
        }
        [Serializable] private sealed class AgentGameplayReport
        {
            public int protocol = 1;
            public string status, error, commit, savePath;
            public bool dirty;
            public AgentGameplayResult[] steps;
            public string[] runtimeErrors;
            public float p95FrameMs, worstFrameMs;
            public int measuredFrames;
        }

        private bool InitializeAgentGameplay()
        {
            if (AgentGameplayPlanPath == null) return true;
            try
            {
                if (!SoakMode) throw new InvalidOperationException("Agent gameplay requires isolated soak mode");
                var plan = JsonUtility.FromJson<AgentGameplayPlan>(File.ReadAllText(AgentGameplayPlanPath));
                if (plan == null || plan.protocol != 1 || plan.steps == null
                    || plan.steps.Length < 1 || plan.steps.Length > 64)
                    throw new ArgumentException("Invalid agent gameplay plan");
                if (BuildIdentityReader.Current.Dirty
                    || BuildIdentityReader.Current.CommitFull != plan.expectedCommit)
                    throw new InvalidOperationException("Agent gameplay build does not match requested clean commit");
                var ids = new HashSet<string>();
                foreach (var step in plan.steps)
                {
                    if (step == null || string.IsNullOrEmpty(step.id)
                        || !step.id.All(c => char.IsLetterOrDigit(c) || c == '-') || !ids.Add(step.id)
                        || !float.IsFinite(step.settleSeconds) || step.settleSeconds < 0.1f || step.settleSeconds > 15f)
                        throw new ArgumentException("Invalid step ID or settle duration");
                    if (!new[] { "workspace", "planner", "book", "cancel", "save", "follow", "view", "overview", "menu", "weather", "time", "camera", "snapshot" }.Contains(step.action))
                        throw new ArgumentException("Unknown agent gameplay action: " + step.action);
                }
                Application.logMessageReceived += RecordAgentRuntimeError;
                _agentGameplayPlan = plan;
                _agentGameplayActive = true;
                _agentGameplayStarted = Time.realtimeSinceStartup;
                Debug.Log("[Airside agent] protocol 1 started; isolated save " + AgentGameplaySavePath);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("[Airside agent] startup failed: " + e.Message);
                enabled = false;
                Application.Quit(2);
                return false;
            }
        }

        private IEnumerator RunAgentGameplay()
        {
            // Let initial fleet views and HUD populate before selecting a subject.
            yield return new WaitForSecondsRealtime(2);
            _agentGameplayAircraft = _operations.FleetOf(_operations.PlayerAirline)
                .FirstOrDefault(a => a.State == FleetState.AtStand
                    && (string.IsNullOrEmpty(_agentGameplayPlan.aircraftType) ? !a.Type.IsRotorcraft
                        : a.Type.Id == _agentGameplayPlan.aircraftType));
            if (_agentGameplayAircraft == null) _agentGameplayError = "No parked test aircraft";
            foreach (var step in _agentGameplayPlan.steps)
            {
                if (_agentGameplayError != null) break;
                var result = new AgentGameplayResult { id = step.id, action = step.action, status = "failed" };
                _agentGameplayResults.Add(result);
                try { result.detail = ApplyAgentGameplayStep(step); }
                catch (Exception e) { _agentGameplayError = e.Message; result.detail = e.Message; }
                if (_agentGameplayError == null)
                {
                    yield return new WaitForSecondsRealtime(step.settleSeconds);
                    try { VerifyAgentGameplayState(step); }
                    catch (Exception e) { _agentGameplayError = e.Message; result.detail = e.Message; }
                }
                // Failed actions/state checks still need their visible context for diagnosis.
                if (step.capture || _agentGameplayError != null || _agentRuntimeErrors.Count > 0)
                {
                    result.screenshot = step.id + ".png";
                    var captured = false;
                    yield return ReviewFrameCapture.Capture(
                        Path.Combine(Path.GetDirectoryName(AgentGameplayPlanPath), result.screenshot), ok => captured = ok);
                    if (!captured)
                    {
                        _agentGameplayError = (_agentGameplayError == null ? "" : _agentGameplayError + "; ")
                            + "Screenshot failed: " + step.id;
                        result.detail = _agentGameplayError;
                    }
                }
                result.fleetStates = _operations.Fleet.Select(a => a.Registration + " " + a.State + " trips " + a.CompletedTrips).ToArray();
                result.aircraft = _agentGameplayAircraft.Registration;
                result.state = _agentGameplayAircraft.State.ToString();
                result.weather = CurrentWeather.ToString();
                result.workspace = _activeWorkspace.ToString();
                result.cameraPitch = AirsideCameraController.CurrentPitch;
                result.cameraYaw = AirsideCameraController.CurrentYaw;
                result.cameraDistance = AirsideCameraController.CurrentDistance;
                if (_agentRuntimeErrors.Count > 0 && _agentGameplayError == null)
                { _agentGameplayError = "Runtime error during scenario"; result.detail = _agentGameplayError; }
                result.status = _agentGameplayError == null ? "passed" : "failed";
                result.realSeconds = Time.realtimeSinceStartup - _agentGameplayStarted;
                result.simulationSeconds = _clock.Now.ElapsedSeconds;
                if (_agentGameplayError != null) break;
                Debug.Log("[Airside agent] PASS " + step.id + " " + result.detail);
            }
            var frameSamples = _soakFrameMs.Take(_soakFrameSamples).Skip(1).Where(x => x > 0 && float.IsFinite(x)).OrderBy(x => x).ToArray();
            var report = new AgentGameplayReport
            {
                status = _agentGameplayError == null ? "passed" : "failed",
                error = _agentGameplayError, commit = BuildIdentityReader.Current.CommitFull,
                dirty = BuildIdentityReader.Current.Dirty, savePath = AgentGameplaySavePath,
                steps = _agentGameplayResults.ToArray(), runtimeErrors = _agentRuntimeErrors.ToArray(),
                measuredFrames = frameSamples.Length,
                p95FrameMs = frameSamples.Length == 0 ? 0 : frameSamples[(int)((frameSamples.Length - 1) * 0.95)],
                worstFrameMs = frameSamples.Length == 0 ? 0 : frameSamples[frameSamples.Length - 1]
            };
            try
            {
                var path = Path.Combine(Path.GetDirectoryName(AgentGameplayPlanPath), "gameplay-report.json");
                File.WriteAllText(path + ".tmp", JsonUtility.ToJson(report, true));
                File.Move(path + ".tmp", path);
            }
            catch (Exception e) { _agentGameplayError = "Cannot write agent report: " + e.Message; }
            Application.logMessageReceived -= RecordAgentRuntimeError;
            Debug.Log("[Airside agent] COMPLETE " + (_agentGameplayError == null ? "passed" : "failed"));
            Application.Quit(_agentGameplayError == null ? 0 : 2);
        }

        private void RecordAgentRuntimeError(string message, string stack, LogType type)
        {
            if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                && _agentRuntimeErrors.Count < 100)
                _agentRuntimeErrors.Add(message + "\n" + stack);
        }

        private void VerifyAgentGameplayState(AgentGameplayStep step)
        {
            if (!string.IsNullOrEmpty(step.expectState) && _agentGameplayAircraft.State.ToString() != step.expectState)
                throw new InvalidOperationException("Expected " + step.expectState + "; observed " + _agentGameplayAircraft.State);
            if (step.action == "follow" && !_cameraController.IsFollowing)
                throw new InvalidOperationException("Follow lost before capture");
            if (step.action == "view" && (!InCockpit || _cockpitAircraftId != _agentGameplayAircraft.Registration
                || _aircraftViewMode.ToString() != step.value))
                throw new InvalidOperationException("Flight view lost before capture");
            if (step.action == "workspace" && _activeWorkspace.ToString() != step.value)
                throw new InvalidOperationException("Workspace changed before capture");
            if (step.action == "planner" && (_activeWorkspace != HudWorkspace.Map || _mapAircraft != _agentGameplayAircraft))
                throw new InvalidOperationException("Planner changed before capture");
            if (step.action == "menu" && _menuOpen != (step.value == "open"))
                throw new InvalidOperationException("Menu changed before capture");
        }

        private string ApplyAgentGameplayStep(AgentGameplayStep step)
        {
            var aircraft = _agentGameplayAircraft;
            switch (step.action)
            {
                case "workspace":
                    if (!Enum.TryParse(step.value, out HudWorkspace workspace)) throw new ArgumentException("Unknown workspace");
                    if (_activeWorkspace != workspace) SetWorkspace(workspace);
                    if (_activeWorkspace != workspace) throw new InvalidOperationException("Workspace did not open");
                    return "Opened " + workspace;
                case "planner":
                    OpenPlanner(aircraft);
                    if (_activeWorkspace != HudWorkspace.Map || _mapAircraft != aircraft)
                        throw new InvalidOperationException("Planner subject mismatch");
                    return "Planner selected " + aircraft.Registration;
                case "book":
                    var destinations = _operations.MapDestinations().Where(d => _operations.CanOperate(aircraft, d)
                        && _operations.CareerState.CanAfford(_operations.DispatchCost(aircraft.Type, _operations.DistanceKm(d)))).ToList();
                    if (destinations.Count == 0) throw new InvalidOperationException("No operable test destination");
                    var destination = destinations[0];
                    var departure = _clock.Now.Advance(Math.Max(900, DeparturePrep.LeadSeconds(aircraft.Type, aircraft.BaseLevel) + 120));
                    var booked = _operations.ScheduleDeparture(aircraft, destination, departure);
                    if (!booked.Accepted || !aircraft.Scheduled.HasValue)
                        throw new InvalidOperationException("Booking refused: " + booked.Reason);
                    _selectedAircraftId = aircraft.Registration;
                    return "Booked " + aircraft.Registration + " to " + destination.Code;
                case "cancel":
                    CancelPlannedFlight(aircraft);
                    if (aircraft.Scheduled.HasValue) throw new InvalidOperationException("Cancellation did not clear booking");
                    return "Cancelled " + aircraft.Registration;
                case "save":
                    SaveAirline();
                    if (!AirlineSaveFile.TryRead(SavePath, out var data, out var error, out _))
                        throw new InvalidOperationException("Save read failed: " + error);
                    var restored = AirlineSave.Restore(data, new ManualSimulationClock(_clock.Now));
                    var savedAircraft = restored.Fleet.FirstOrDefault(a => a.Registration == aircraft.Registration);
                    if (restored.Fleet.Count != _operations.Fleet.Count || savedAircraft == null
                        || savedAircraft.State != aircraft.State || savedAircraft.Scheduled.HasValue != aircraft.Scheduled.HasValue)
                        throw new InvalidOperationException("Restored fleet/subject mismatch");
                    if (aircraft.Scheduled.HasValue && (savedAircraft.Scheduled.Value.Destination.Code != aircraft.Scheduled.Value.Destination.Code
                        || savedAircraft.Scheduled.Value.DepartAt.ElapsedSeconds != aircraft.Scheduled.Value.DepartAt.ElapsedSeconds))
                        throw new InvalidOperationException("Restored booking mismatch");
                    return "Disk save read and restore verified; personal save untouched";
                case "follow":
                    if (!TryFollowFleetAircraft(aircraft.Registration) || !_cameraController.IsFollowing)
                        throw new InvalidOperationException("Follow failed");
                    return "Following " + aircraft.Registration;
                case "view":
                    if (!Enum.TryParse(step.value, out AircraftViewMode view) || !EnterFlightView(aircraft, view)
                        || _cockpitAircraftId != aircraft.Registration || _aircraftViewMode != view)
                        throw new InvalidOperationException("Flight view unavailable: " + step.value);
                    return "Entered " + view + " for " + aircraft.Registration;
                case "overview":
                    ExitCockpit(true);
                    _cameraController.ReleaseFollow();
                    _activeWorkspace = HudWorkspace.None;
                    if (InCockpit || _cameraController.IsFollowing) throw new InvalidOperationException("Overview restoration failed");
                    return "Returned to overview";
                case "menu":
                    _menuOpen = step.value == "open";
                    return _menuOpen ? "Menu open; live airline clock continues" : "Menu closed";
                case "time":
                    if (!TimeSpan.TryParseExact(step.value, @"hh\:mm", CultureInfo.InvariantCulture, out var localTime))
                        throw new ArgumentException("Time must be HH:mm");
                    ReviewLocalTime = localTime;
                    return "Visual local time " + step.value;
                case "camera":
                    var coordinates = step.value.Split(',');
                    if (coordinates.Length != 3) throw new ArgumentException("Camera needs pitch,yaw,distance");
                    var pitch = float.Parse(coordinates[0], CultureInfo.InvariantCulture);
                    var yaw = float.Parse(coordinates[1], CultureInfo.InvariantCulture);
                    var distance = float.Parse(coordinates[2], CultureInfo.InvariantCulture);
                    _cameraController.ApplyAgentReviewPose(pitch, yaw, distance);
                    return "Camera pose " + step.value;
                case "snapshot": DiagnoseAgentRenderers(aircraft); return "Observed " + aircraft.Registration + " " + aircraft.State;
                case "weather":
                    if (!Enum.TryParse(step.value, true, out WeatherKind weather)) throw new ArgumentException("Unknown weather");
                    SetReviewWeatherToken(step.value);
                    if (CurrentWeather != weather) throw new InvalidOperationException("Weather override failed");
                    return "Visual weather " + weather + "; operational weather unchanged";
                default: throw new ArgumentException("Unsupported agent action");
            }
        }
            // TEMPORARY DIAGNOSTIC (finding 15): log tail renderers of the subject.
        private void DiagnoseAgentRenderers(FleetAircraft aircraft)
        {
            if (!_fleetViewById.TryGetValue(aircraft.Registration, out var view) || view == null) return;
            var camera = _cameraController != null ? _cameraController.GetComponent<Camera>() : null;
            var planes = camera != null ? GeometryUtility.CalculateFrustumPlanes(camera) : null;
            Debug.Log("[Airside diag] view " + view.name + " pos " + view.position + " rot " + view.eulerAngles + " scale " + view.lossyScale
                + " camera " + (camera != null ? camera.transform.position.ToString() : "none") + " near " + (camera != null ? camera.nearClipPlane : 0) + " far " + (camera != null ? camera.farClipPlane : 0)
                + " cockpit " + InCockpit + " mode " + _aircraftViewMode);
            foreach (var r in view.GetComponentsInChildren<Renderer>(true))
            {
                var n = r.name.ToLowerInvariant();
                if (!(n.Contains("tail") || n.Contains("elev") || n.Contains("fin") || n.Contains("rudder") || n.Contains("stab"))) continue;
                var mf = r.GetComponent<MeshFilter>();
                var m = r.sharedMaterial;
                Debug.Log("[Airside diag] " + r.name + " enabled " + r.enabled + " active " + r.gameObject.activeInHierarchy + " visible " + r.isVisible
                    + " inFrustum " + (planes != null && GeometryUtility.TestPlanesAABB(planes, r.bounds))
                    + " centre " + view.InverseTransformPoint(r.bounds.center).ToString("F2") + " size " + r.bounds.size.ToString("F2")
                    + " scale " + r.transform.lossyScale.ToString("F2") + " verts " + (mf != null && mf.sharedMesh != null ? mf.sharedMesh.vertexCount : -1)
                    + " shader " + (m != null ? m.shader.name : "none") + " queue " + (m != null ? m.renderQueue : -1)
                    + " shadow " + r.shadowCastingMode + " layer " + r.gameObject.layer + " mats " + r.sharedMaterials.Length);
            }
        }
    }
}
