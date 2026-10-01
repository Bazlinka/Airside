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
        private double _cockpitNextReadout;
        private bool InCockpit => !string.IsNullOrEmpty(_cockpitAircraftId);

        private string CockpitReason(FleetAircraft aircraft)
        {
            if (aircraft == null) return "Select an aircraft";
            var visible = IsFleetFlightVisible(aircraft.Registration)
                && _fleetViewById.TryGetValue(aircraft.Registration, out var view)
                && view != null && view.gameObject.activeInHierarchy;
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
            if (overview || !TryFollowFleetAircraft(id)) ResetView();
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
                ShowToast("Aircraft has left the local area.");
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
            if (elapsed > 0 && elapsed < 0.75)
            {
                var delta = view.position - _cockpitPreviousPosition;
                delta.y = 0f;
                var speed = CircuitProfile.ToKnots(delta.magnitude / (float)elapsed);
                _cockpitGroundKnots = Mathf.Lerp(_cockpitGroundKnots, speed, 1f - Mathf.Exp(-6f * (float)elapsed));
            }
            _cockpitPreviousTime = _preciseTime;
            _cockpitPreviousPosition = view.position;
            if (_preciseTime < _cockpitNextReadout) return;
            _cockpitNextReadout = _preciseTime + 0.1;
            var height = Mathf.Max(0f, view.position.y - AirsideFlightPath.GroundY) * 3.28084f;
            if (_cockpitInterior is JetCockpitInterior jet)
                jet.SetFlightState(view, EngineStartSequence.For(aircraft, _preciseTime), AircraftStatus.TagPhase(aircraft, _clock.Now));
            _cockpitInterior.SetReadout($"GS {_cockpitGroundKnots:0} kt\nHEIGHT {height:0} ft\nHDG {view.eulerAngles.y:000}°");
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
                    $"{aircraft.Registration} · {AircraftStatus.TagPhase(aircraft, _clock.Now)}\nRight-drag to look · scroll to zoom");
            if (GUI.Button(new Rect(strip.xMax - 246f, strip.y + 10f, 112f, 36f), "Recenter", button))
                _cameraController.RecenterCockpit();
            if (GUI.Button(new Rect(strip.xMax - 124f, strip.y + 10f, 112f, 36f), "Exit (Esc)", button))
                ExitCockpit(false);
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
