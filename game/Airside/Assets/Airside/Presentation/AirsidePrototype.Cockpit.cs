using System;
using Airside.Domain;
using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    public sealed partial class AirsidePrototype
    {
        internal static Mesh CockpitBoxMesh(Vector3 size) => BevelledCubeMesh(size);
        private string _cockpitAircraftId;
        private CockpitInterior _cockpitInterior;
        private Transform _cockpitView;
        private Vector3 _cockpitPreviousPosition;
        private double _cockpitPreviousTime;
        private float _cockpitGroundKnots;
        private readonly CockpitMotion _cockpitMotion = new();
        private readonly CockpitCallouts _cockpitCallouts = new();
        private string _cockpitCallText;
        private float _cockpitCallUntil;
        private GUIStyle _calloutStyle;
        private bool _cockpitIsJet;
        private float _cockpitGearHeight, _cockpitVerticalSpeed;
        private static int StableHash(string text)
        {
            var hash = 17;
            foreach (var c in text) hash = hash * 31 + c;
            return hash;
        }
        private double _cockpitNextReadout;
        private bool InCockpit => !string.IsNullOrEmpty(_cockpitAircraftId);

        private string CockpitReason(FleetAircraft aircraft)
        {
            if (aircraft == null) return "Select an aircraft";
            var visible = CanWatchJourney(aircraft) || (IsFleetFlightVisible(aircraft.Registration)
                && _fleetViewById.TryGetValue(aircraft.Registration, out var view)
                && view != null && view.gameObject.activeInHierarchy);
            return CockpitAvailability.Reason(aircraft.Type, visible, EngineStartSequence.For(aircraft, _preciseTime));
        }

        private bool EnterCockpit(FleetAircraft aircraft)
        {
            if (_cameraController == null || CockpitReason(aircraft).Length != 0) return false;
            if (InCockpit) ExitCockpit(false);
            _cockpitAircraftId = aircraft.Registration;
            _selectedAircraftId = aircraft.Registration;
            _activeWorkspace = HudWorkspace.None;
            _devToolsOpen = _controlsHelpOpen = false;
            UpdateFlightWorld();
            UpdateAircraftVisual();
            BindCockpitView(_fleetViewById[aircraft.Registration]);
            if (_cockpitInterior == null || !_cameraController.StartCockpit(_cockpitInterior.Seat))
            {
                ExitCockpit(false);
                return false;
            }
            PlayUiClick();
            return true;
        }

        private void BindCockpitView(Transform view)
        {
            if (_cockpitInterior != null)
            {
                _cockpitInterior.Leave();
                Destroy(_cockpitInterior.gameObject);
            }
            _cockpitView = view;
            var type = _fleetAircraftById[_cockpitAircraftId].Type;
            _cockpitInterior = type.Id == AircraftType.Saab340.Id
                ? SaabCockpitInterior.Build(view)
                : JetCockpitProfile.TryFor(type.Id, out _) ? JetCockpitInterior.Build(view, type)
                : TurbopropCockpitInterior.Create(view, type);
            if (_cockpitInterior == null) return;
            _cockpitInterior.Enter();
            _cockpitPreviousPosition = view.position;
            _cockpitPreviousTime = _preciseTime;
            _cockpitGroundKnots = 0f;
            _cockpitNextReadout = 0;
            _cockpitMotion.Reset(StableHash(_cockpitAircraftId));
            _cockpitCallouts.Reset();
            _cockpitIsJet = JetCockpitProfile.TryFor(_fleetAircraftById[_cockpitAircraftId].Type.Id, out _);
            _cockpitCallText = null;
            _cockpitGearHeight = Mathf.Max(0f, view.position.y - AirsideFlightPath.GroundY);
            _cockpitVerticalSpeed = 0f;
        }

        private void ExitCockpit(bool overview)
        {
            if (!InCockpit) return;
            var id = _cockpitAircraftId;
            _cockpitAircraftId = null;
            if (_cockpitInterior != null)
            {
                _cockpitInterior.Leave();
                Destroy(_cockpitInterior.gameObject);
            }
            _cockpitInterior = null;
            _cockpitView = null;
            _cameraController?.EndCockpit();
            var wasRemote = _flightOriginX != 0 || _flightOriginZ != 0;
            ResetFlightWorld();
            UpdateAircraftVisual();
            if (overview || wasRemote || !TryFollowFleetAircraft(id)) ResetView();
        }

        /// <summary>Runs after final aircraft poses; resolve registration every frame, never a fleet slot.</summary>
        private void UpdateCockpitView()
        {
            if (!InCockpit) { TryStartCockpitReview(); return; }
            if (!_fleetAircraftById.TryGetValue(_cockpitAircraftId, out var aircraft)
                || !IsFleetFlightVisible(_cockpitAircraftId)
                || !_fleetViewById.TryGetValue(_cockpitAircraftId, out var view)
                || view == null || !view.gameObject.activeInHierarchy)
            {
                ExitCockpit(true);
                ShowToast("Flight left the supported region or reached its destination.");
                return;
            }
            if (CockpitReason(aircraft).Length != 0)
            {
                ExitCockpit(false);
                ShowToast("Engines stopped. Returned to external view.");
                return;
            }
            if (view != _cockpitView || _cockpitInterior == null)
            {
                BindCockpitView(view);
                _cameraController.StartCockpit(_cockpitInterior.Seat);
            }
            var elapsed = _preciseTime - _cockpitPreviousTime;
            var parts = PartsFor(view);
            // Wheel height: take the gear-pivot lift back out so a pitched-up roll still reads as on the ground.
            var lift = parts.Profile != null
                ? AircraftGearPivot.LiftMetres(view.rotation,
                    new Vector3(0f, parts.Profile.ModelGroundOffsetMetres, parts.MainGearZMetres)) : 0f;
            var gearHeight = Mathf.Max(0f, view.position.y - lift - AirsideFlightPath.GroundY);
            var simRate = 0f;
            if (elapsed > 0 && elapsed < 0.75)
            {
                var delta = view.position - _cockpitPreviousPosition;
                delta.y = 0f;
                var speed = CircuitProfile.ToKnots(delta.magnitude / (float)elapsed);
                _cockpitGroundKnots = Mathf.Lerp(_cockpitGroundKnots, speed, 1f - Mathf.Exp(-6f * (float)elapsed));
                var rawVertical = (gearHeight - _cockpitGearHeight) / (float)elapsed;
                _cockpitVerticalSpeed = Mathf.Lerp(_cockpitVerticalSpeed, rawVertical, 1f - Mathf.Exp(-10f * (float)elapsed));
                simRate = Time.unscaledDeltaTime > 1e-4f ? (float)elapsed / Time.unscaledDeltaTime : 1f;
            }
            _cockpitPreviousTime = _preciseTime;
            _cockpitPreviousPosition = view.position;
            _cockpitGearHeight = gearHeight;
            var spool = EngineStartSequence.For(aircraft, _preciseTime);
            var pitchUp = -Mathf.DeltaAngle(0f, view.eulerAngles.x);
            var bankLeft = Mathf.DeltaAngle(0f, view.eulerAngles.z);
            // Head and body motion every frame: rumble, touchdown jolt, thumps, g-load and gaze into turns.
            var motion = _cockpitMotion.Step(new CockpitMotion.Sample
            {
                DeltaSeconds = Time.unscaledDeltaTime, SimRate = simRate, Time = Time.unscaledTime,
                GroundSpeed = _cockpitGroundKnots / 1.943844f, VerticalSpeed = _cockpitVerticalSpeed,
                HeightAgl = gearHeight, PitchUpDegrees = pitchUp, BankLeftDegrees = bankLeft,
                Spool = (spool.Left + spool.Right) * 0.5f,
                Turboprop = !_cockpitIsJet,
            });
            _cameraController.SetCockpitMotion(new Vector3(motion.Right, motion.Up, motion.Forward),
                new Vector3(motion.PitchDownDegrees, motion.YawDegrees, motion.RollDegrees));
            _cockpitInterior.SetAttitude(pitchUp, bankLeft);
            _cockpitInterior.SetEnvironment(PresentationDaylight, CurrentWeatherLook.Precipitation, Time.unscaledTime);
            _cockpitCallouts.DeltaSeconds = (float)Math.Max(0.0, elapsed);
            var call = _cockpitCallouts.Step(new CockpitCallouts.Sample
            {
                GroundKnots = _cockpitGroundKnots, RotateKnots = AircraftPerformance.For(aircraft.Type).RotateKnots,
                HeightFeet = gearHeight * 3.28084f, VerticalFeetPerMinute = _cockpitVerticalSpeed * 196.85f,
                Jet = _cockpitIsJet,
            });
            if (call != null) { _cockpitCallText = call; _cockpitCallUntil = Time.unscaledTime + 2.2f; }
            if (_preciseTime < _cockpitNextReadout) return;
            _cockpitNextReadout = _preciseTime + 0.1;
            if (_cockpitInterior is JetCockpitInterior jet)
                jet.SetFlightState(view, spool, AircraftStatus.TagPhase(aircraft, _clock.Now));
            var height = gearHeight * 3.28084f;
            var vs = _cockpitVerticalSpeed * 196.85f;   // ft/min
            _cockpitInterior.SetReadout($"GS {_cockpitGroundKnots:0} kt\nHEIGHT {height:0} ft  VS {vs:+0;-0;0}\nHDG {view.eulerAngles.y:000}°");
        }

        private void DrawCockpitHud(HudLayout layout, GUIStyle panel, GUIStyle button)
        {
            _hudPanels.Clear();
            _cameraController.KeyboardCaptured = _menuOpen || GUIUtility.keyboardControl != 0;
            var strip = new Rect(18f, 18f, Mathf.Min(700f, layout.Viewport.x - 36f), 58f);
            _hudPanels.Add(strip);
            GUI.Box(strip, GUIContent.none, panel);
            if (_fleetAircraftById.TryGetValue(_cockpitAircraftId, out var aircraft))
                GUI.Label(new Rect(strip.x + 12f, strip.y + 8f, strip.width - 270f, 42f),
                    $"{aircraft.Registration} · {AircraftStatus.TagPhase(aircraft, _clock.Now)}\nDrag or arrows to look · 1-5 glance · scroll zoom");
            if (GUI.Button(new Rect(strip.xMax - 246f, strip.y + 10f, 112f, 36f), "Recenter", button))
                _cameraController.RecenterCockpit();
            if (GUI.Button(new Rect(strip.xMax - 124f, strip.y + 10f, 112f, 36f), "Exit (Esc)", button))
                ExitCockpit(false);
            if (!string.IsNullOrEmpty(_cockpitCallText) && Time.unscaledTime < _cockpitCallUntil)
            {
                _calloutStyle ??= new GUIStyle(GUI.skin.label)
                { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                _calloutStyle.fontSize = Mathf.RoundToInt(Mathf.Clamp(layout.Viewport.y * 0.045f, 22f, 44f));
                var area = new Rect(0f, layout.Viewport.y * 0.62f, layout.Viewport.x, 60f);
                var fade = Mathf.Clamp01((_cockpitCallUntil - Time.unscaledTime) / 0.6f);
                _calloutStyle.normal.textColor = new Color(0f, 0f, 0f, 0.7f * fade);
                GUI.Label(new Rect(area.x + 2f, area.y + 2f, area.width, area.height), _cockpitCallText, _calloutStyle);
                _calloutStyle.normal.textColor = new Color(0.92f, 0.96f, 0.9f, fade);
                GUI.Label(area, _cockpitCallText, _calloutStyle);
            }
            var placement = AirlineHudLayout.Create(layout, false);
            DrawToast(placement.Toast);
        }

        // Reproducible packaged review: only a real eligible aircraft, never force-start engines.
        private static readonly bool CockpitReview = Array.IndexOf(Environment.GetCommandLineArgs(), "-airsideReviewCockpit") >= 0;
        private static readonly string CockpitReviewType = ReadCockpitReviewType();
        private static readonly bool CockpitReviewArrivals = Array.IndexOf(Environment.GetCommandLineArgs(), "-airsideReviewCockpitArrivals") >= 0;
        private static readonly bool CockpitReviewAnyPhase = Array.IndexOf(Environment.GetCommandLineArgs(), "-airsideReviewCockpitAnyPhase") >= 0;
        private static string ReadCockpitReviewType()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-airsideReviewCockpitType");
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
        private bool _cockpitReviewStarted;
        private float _cockpitReviewEnteredAt = -1f;
        private void TryStartCockpitReview()
        {
            if (!CockpitReview || _cockpitReviewStarted || !SoakMode || _menuOpen) return;
            FleetAircraft best = null;
            foreach (var aircraft in _fleetAircraftById.Values)
            {
                if (FlightJourneyReviewActive && aircraft.Registration != _reviewJourneyAircraftId) continue;
                // Prefer an actual departure so the first still covers engine startup,
                // rather than jumping into an inbound already at full power.
                if (CockpitReviewType != null && aircraft.Type.Id != CockpitReviewType) continue;
                var phase = CockpitReviewAnyPhase || (CockpitReviewArrivals
                    ? aircraft.State is FleetState.Inbound or FleetState.Landing or FleetState.TaxiIn
                    : aircraft.State is FleetState.AtStand or FleetState.TaxiOut);
                if (!phase
                    || !CockpitAvailability.Supported(aircraft.Type) || CockpitReason(aircraft).Length != 0) continue;
                if (best == null || (aircraft.Airline.IsPlayer && !best.Airline.IsPlayer)
                    || (aircraft.Airline.IsPlayer == best.Airline.IsPlayer
                        && string.CompareOrdinal(aircraft.Registration, best.Registration) < 0)) best = aircraft;
            }
            if (best == null) return;
            _cockpitReviewStarted = EnterCockpit(best);
            if (_cockpitReviewStarted)
            {
                _cockpitReviewEnteredAt = Time.unscaledTime;
                var engines = EngineStartSequence.For(best, _preciseTime);
                Debug.Log($"[Airside cockpit] review entered {best.Registration} {best.State} "
                    + $"eng L{engines.Left:0.000}/R{engines.Right:0.000}");
            }
        }
    }
}
