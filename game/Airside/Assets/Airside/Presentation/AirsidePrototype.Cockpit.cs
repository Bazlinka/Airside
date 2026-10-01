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
        private SaabCockpitInterior _cockpitInterior;
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
            _cockpitInterior = SaabCockpitInterior.Build(view);
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

        // Reproducible packaged review: only a real eligible SF34, never force-start engines.
        private static readonly bool CockpitReview = Array.IndexOf(Environment.GetCommandLineArgs(), "-airsideReviewCockpit") >= 0;
        private bool _cockpitReviewStarted;
        private void TryStartCockpitReview()
        {
            if (!CockpitReview || _cockpitReviewStarted || !SoakMode || _menuOpen) return;
            foreach (var aircraft in _fleetAircraftById.Values)
                if (aircraft.Airline.IsPlayer && CockpitAvailability.Supported(aircraft.Type) && CockpitReason(aircraft).Length == 0)
                {
                    _cockpitReviewStarted = EnterCockpit(aircraft);
                    if (_cockpitReviewStarted) Debug.Log($"[Airside cockpit] review entered {aircraft.Registration}");
                    return;
                }
        }
    }
}
