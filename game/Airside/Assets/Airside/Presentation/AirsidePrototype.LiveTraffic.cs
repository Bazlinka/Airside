using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Airside.Domain;
using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Real aircraft around Adelaide, drawn in the sky from adsb.lol (ADR 0081). Presentation
    /// only: it reads nothing back into the simulation, and while the feed is down the
    /// authored sky traffic takes its place.
    /// </summary>
    public sealed partial class AirsidePrototype
    {
        private static readonly HttpClient LiveHttp = CreateLiveHttp();

        private Task<List<LiveAircraft>> _liveFetch;
        private List<LiveAircraft> _liveAircraft = new();
        private float _liveFetchedAt = float.NegativeInfinity;
        private float _nextLivePollAt;
        private int _liveFailures;
        private Transform _liveTrafficRoot;
        private readonly Dictionary<string, Transform> _liveViews = new();
        private readonly HashSet<string> _liveShown = new();
        private readonly List<string> _liveGone = new();
        private readonly List<GroundObstacle> _liveObstacles = new();
        private readonly Dictionary<string, float> _liveStandAsideUntil = new();

        /// <summary>A real aircraft that clashed stays hidden this long, so it does not flicker.</summary>
        private const float LiveStandAsideSeconds = 10f;

        /// <summary>Live aircraft drawn this frame, for tags and the mini-map.</summary>
        private readonly List<(LiveAircraft Aircraft, Transform View)> _liveDrawn = new();

        /// <summary>The feed answered recently enough to stand in for the authored sky traffic.</summary>
        private bool LiveTrafficHealthy =>
            AirsideSettings.Current.LiveTraffic && Time.realtimeSinceStartup - _liveFetchedAt < 60f;

        /// <summary>For the Options row: what the live feed is doing right now.</summary>
        private string LiveTrafficStatus
        {
            get
            {
                if (!AirsideSettings.Current.LiveTraffic)
                    return "Off";
                if (LiveTrafficHealthy)
                    return $"On · {_liveShown.Count} in view";
                return _liveFailures > 0 ? "On · offline" : "On · connecting";
            }
        }

        private static HttpClient CreateLiveHttp()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Airside/1.0 (+https://github.com/Bazlinka/Airside)");
            return client;
        }

        private void UpdateLiveTraffic()
        {
            if (!FleetMode || !AirsideSettings.Current.LiveTraffic)
            {
                HideLiveTraffic();
                return;
            }

            PollLiveTraffic();
            DrawLiveTraffic();
        }

        private void PollLiveTraffic()
        {
            var now = Time.realtimeSinceStartup;
            if (_liveFetch != null)
            {
                if (!_liveFetch.IsCompleted)
                    return;
                if (_liveFetch.Status == TaskStatus.RanToCompletion && _liveFetch.Result != null)
                {
                    _liveAircraft = _liveFetch.Result;
                    _liveFetchedAt = now;
                    _liveFailures = 0;
                }
                else
                {
                    _liveFailures++;
                }

                _liveFetch = null;
                // Back off while offline rather than hammering a feed that is not there.
                _nextLivePollAt = now + (_liveFailures == 0
                    ? LiveTraffic.PollSeconds
                    : Mathf.Min(120f, LiveTraffic.PollSeconds * (1 << Mathf.Min(_liveFailures, 4))));
                return;
            }

            if (now < _nextLivePollAt)
                return;
            var url = LiveTraffic.RequestUrl();
            _liveFetch = Task.Run(async () => LiveTraffic.Parse(await LiveHttp.GetStringAsync(url)));
        }

        private void DrawLiveTraffic()
        {
            _liveShown.Clear();
            if (!LiveTrafficHealthy)
            {
                HideLiveTraffic();
                return;
            }

            if (_liveTrafficRoot == null)
            {
                _liveTrafficRoot = new GameObject("Live traffic").transform;
                _liveTrafficRoot.SetParent(transform, false);
            }

            var since = Time.realtimeSinceStartup - _liveFetchedAt;
            CollectLiveObstacles(out var mainStripInUse, out var crossStripInUse);
            _liveDrawn.Clear();
            foreach (var aircraft in _liveAircraft)
            {
                var onField = false;
                var onGround = false;
                if (!LiveTraffic.TryPose(aircraft, since, out var pose))
                {
                    if (!LiveTraffic.TryFieldPose(aircraft, since, out pose, out onGround))
                        continue;
                    onField = true;
                }

                var type = LiveTraffic.ModelFor(aircraft.TypeCode);
                if (onField && StandsAside(aircraft.Hex, pose, type, mainStripInUse, crossStripInUse))
                    continue;
                _liveShown.Add(aircraft.Hex);
                var target = new Vector3((float)pose.X, AirsideFlightPath.GroundY + (float)pose.Y, (float)pose.Z);
                if (!_liveViews.TryGetValue(aircraft.Hex, out var view) || view == null)
                {
                    view = BuildAircraftForType($"Live {aircraft.Label}", type, ColorForSkyAirline(aircraft.Callsign), null);
                    view.SetParent(_liveTrafficRoot, false);
                    _liveViews[aircraft.Hex] = view;
                    view.position = target;
                }

                view.gameObject.SetActive(true);
                _liveDrawn.Add((aircraft, view));
                // Each report nudges the dead-reckoned track; ease onto it instead of jumping.
                var ease = 1f - Mathf.Exp(-3f * PresentationDeltaTime);
                view.position = Vector3.Distance(view.position, target) > 400f
                    ? target
                    : Vector3.Lerp(view.position, target, ease);
                // Stopped on the ground the reported track is noise; keep the last heading.
                if (!(onGround && aircraft.GroundSpeedKnots < 2.0))
                    view.rotation = Quaternion.Slerp(view.rotation,
                        Quaternion.Euler(0f, pose.YawDegrees, 0f) * Quaternion.Euler(-pose.PitchDegrees, 0f, 0f), ease);

                // Gear, lights and engines read the phase: taxiing on the ground, landing or
                // climbing low over the field, cruising otherwise.
                var phase = onGround ? AircraftPhase.TaxiIn
                    : onField ? (aircraft.VerticalRateFpm < 0 ? AircraftPhase.Landing : AircraftPhase.Takeoff)
                    : AircraftPhase.Circuit;
                var parts = PartsFor(view);
                UpdateAircraftLightsAndGear(parts.LightsAndGear, phase, PresentationDaylight, 0.5f,
                    PresentationDeltaTime, PresentationClock, null);
                SpinJetFans(view, parts.FanLeft, parts.FanRight, phase, null);
                SpinPropellers(view, parts.Propellers, phase, null);
            }

            _liveGone.Clear();
            foreach (var pair in _liveViews)
            {
                if (_liveShown.Contains(pair.Key))
                    continue;
                if (pair.Value != null)
                    pair.Value.gameObject.SetActive(false);
                _liveGone.Add(pair.Key);
            }

            // Aircraft leave the feed for good; do not keep their models forever.
            if (_liveViews.Count > 48)
            {
                foreach (var id in _liveGone)
                {
                    if (_liveViews.TryGetValue(id, out var view) && view != null)
                    {
                        AirsideNamedChildren.Forget(view);
                        ForgetAircraftViewParts(view);
                        Destroy(view.gameObject);
                    }

                    _liveViews.Remove(id);
                }
            }
        }

        /// <summary>The game's own aircraft on the field, and which strips it is using.</summary>
        private void CollectLiveObstacles(out bool mainStripInUse, out bool crossStripInUse)
        {
            _liveObstacles.Clear();
            mainStripInUse = false;
            crossStripInUse = false;
            foreach (var pair in _fleetViewById)
            {
                var view = pair.Value;
                if (view == null || !view.gameObject.activeInHierarchy
                    || !_fleetAircraftById.TryGetValue(pair.Key, out var aircraft))
                    continue;
                _liveObstacles.Add(new GroundObstacle(view.position.x, view.position.z,
                    LiveGroundClearance.HalfSpan(aircraft.Type)));
            }

            if (_operations == null)
                return;
            foreach (var aircraft in _operations.Fleet)
            {
                var usingStrip = aircraft.State is FleetState.TakingOff or FleetState.HoldingForLanding
                                 || aircraft.State == FleetState.Landing && _operations.IsOccupyingRunway(aircraft);
                if (!usingStrip)
                    continue;
                if (RunwayWeather.IsMainRunway(aircraft.AssignedRunway))
                    mainStripInUse = true;
                else
                    crossStripInUse = true;
            }
        }

        /// <summary>True while a real aircraft on the field must stay hidden for the game's traffic.</summary>
        private bool StandsAside(string hex, LiveTrafficPose pose, AircraftType type, bool mainStripInUse,
            bool crossStripInUse)
        {
            var now = Time.realtimeSinceStartup;
            if (LiveGroundClearance.Clashes(pose.X, pose.Z, LiveGroundClearance.HalfSpan(type), _liveObstacles,
                    mainStripInUse, crossStripInUse))
            {
                _liveStandAsideUntil[hex] = now + LiveStandAsideSeconds;
                return true;
            }

            if (_liveStandAsideUntil.TryGetValue(hex, out var until))
            {
                if (now < until)
                    return true;
                _liveStandAsideUntil.Remove(hex);
            }

            return false;
        }

        private void HideLiveTraffic()
        {
            _liveDrawn.Clear();
            _liveShown.Clear();
            foreach (var pair in _liveViews)
                if (pair.Value != null)
                    pair.Value.gameObject.SetActive(false);
        }
    }
}
