using System;
using Airside.Domain;
using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    public sealed partial class AirsidePrototype
    {
        internal static Mesh CockpitBoxMesh(Vector3 size) => BevelledCubeMesh(size);
        private enum AircraftViewMode { Cockpit, LeftWindow, RightWindow, Exterior }
        private AircraftViewMode _aircraftViewMode;
        private bool InteriorListening => InCockpit && _aircraftViewMode != AircraftViewMode.Exterior;
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
            var visible = CanWatchJourney(aircraft) || (IsFleetFlightVisible(aircraft.Registration)
                && _fleetViewById.TryGetValue(aircraft.Registration, out var view)
                && view != null && view.gameObject.activeInHierarchy);
            return CockpitAvailability.Reason(aircraft.Type, visible, EngineStartSequence.For(aircraft, _preciseTime));
        }

        private string FlightViewReason(FleetAircraft aircraft, AircraftViewMode mode)
        {
            if (mode == AircraftViewMode.Cockpit) return CockpitReason(aircraft);
            if (aircraft != null && aircraft.IsFreighter && mode != AircraftViewMode.Exterior) return "Cargo cabin";
            if (aircraft == null || !PassengerCabinProfile.TryFor(aircraft.Type.Id, out _)) return "Passenger view unavailable";
            return CanWatchJourney(aircraft) || (IsFleetFlightVisible(aircraft.Registration)
                && _fleetViewById.TryGetValue(aircraft.Registration, out var view)
                && view != null && view.gameObject.activeInHierarchy) ? "" : "Aircraft outside the supported view";
        }
        private bool EnterCockpit(FleetAircraft aircraft) => EnterFlightView(aircraft, AircraftViewMode.Cockpit);
        private bool EnterFlightView(FleetAircraft aircraft, AircraftViewMode mode)
        {
            if (_cameraController == null || FlightViewReason(aircraft, mode).Length != 0) return false;
            if (InCockpit && _cockpitAircraftId == aircraft.Registration
                && _cockpitInterior is PassengerCabinInterior cabin
                && (mode == AircraftViewMode.LeftWindow || mode == AircraftViewMode.RightWindow))
            {
                _aircraftViewMode=mode; cabin.SelectSide(mode == AircraftViewMode.RightWindow);
                return _cameraController.StartPassenger(cabin.Seat);
            }
            // Switching seats/cameras retains registration, render origin and the terrain window.
            if (InCockpit && _cockpitAircraftId != aircraft.Registration) ExitCockpit(true);
            _cockpitAircraftId=aircraft.Registration; _aircraftViewMode=mode;
            _selectedAircraftId=aircraft.Registration; _activeWorkspace=HudWorkspace.None;
            _devToolsOpen=_controlsHelpOpen=false;
            UpdateFlightWorld(); UpdateAircraftVisual();
            if (!_fleetViewById.TryGetValue(aircraft.Registration,out var view) || view == null)
            { ExitCockpit(true); return false; }
            BindCockpitView(view);
            if (!StartFlightCamera(aircraft)) { ExitCockpit(true); return false; }
            PlayUiClick(); return true;
        }
        private bool StartFlightCamera(FleetAircraft aircraft)
        {
            if (_aircraftViewMode == AircraftViewMode.Exterior)
            {
                var profile=AircraftVisualProfiles.For(aircraft.Type);
                return _cameraController.StartFlightExterior(_cockpitView,
                    profile.VisualCentreOffsetMetres+Vector3.up*profile.PickCentreYMetres,
                    Mathf.Max(profile.PickSizeMetres.x,profile.PickSizeMetres.z));
            }
            return _cockpitInterior != null && (_cockpitInterior is PassengerCabinInterior
                ? _cameraController.StartPassenger(_cockpitInterior.Seat)
                : _cameraController.StartCockpit(_cockpitInterior.Seat));
        }

        private void BindCockpitView(Transform view)
        {
            if (_cockpitInterior != null)
            {
                _cockpitInterior.Leave();
                Destroy(_cockpitInterior.gameObject);
            }
            _cockpitView = view;
            _cockpitInterior=null;
            var type = _fleetAircraftById[_cockpitAircraftId].Type;
            if (_aircraftViewMode == AircraftViewMode.Exterior) return;
            if (_aircraftViewMode == AircraftViewMode.LeftWindow || _aircraftViewMode == AircraftViewMode.RightWindow)
                _cockpitInterior=PassengerCabinInterior.Build(view,type,_aircraftViewMode == AircraftViewMode.RightWindow);
            else _cockpitInterior = type.Id == AircraftType.Saab340.Id
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
            if (FlightViewReason(aircraft, _aircraftViewMode).Length != 0)
            {
                ExitCockpit(false);
                ShowToast("Engines stopped. Returned to external view.");
                return;
            }
            if (view != _cockpitView || (_aircraftViewMode != AircraftViewMode.Exterior && _cockpitInterior == null))
            {
                BindCockpitView(view);
                StartFlightCamera(aircraft);
            }
            if (_aircraftViewMode == AircraftViewMode.Exterior) return;
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
            _cameraController.KeyboardCaptured=_menuOpen || GUIUtility.keyboardControl != 0;
            var strip=new Rect(18,18,Mathf.Min(860,layout.Viewport.x-36),92);
            _hudPanels.Add(strip); GUI.Box(strip,GUIContent.none,panel);
            if (_fleetAircraftById.TryGetValue(_cockpitAircraftId,out var aircraft))
            {
                GUI.Label(new Rect(strip.x+12,strip.y+7,strip.width-24,23),
                    $"{aircraft.Registration} · {aircraft.Type.Name} · {AircraftStatus.TagPhase(aircraft,_clock.Now)}");
                var labels=new[]{"Cockpit","Left window","Right window","Outside"};
                var width=(strip.width-24-3*6)/4;
                for(int i=0;i<4;i++)
                {
                    var mode=(AircraftViewMode)i; var old=GUI.enabled;
                    GUI.enabled=FlightViewReason(aircraft,mode).Length == 0;
                    if(GUI.Button(new Rect(strip.x+12+i*(width+6),strip.y+33,width,25),
                        (_aircraftViewMode == mode ? "● " : "")+labels[i],button)) EnterFlightView(aircraft,mode);
                    GUI.enabled=old;
                }
            }
            GUI.Label(new Rect(strip.x+12,strip.y+64,strip.width-210,20),
                _aircraftViewMode == AircraftViewMode.Exterior ? "Right-drag to orbit · scroll for distance" : "Right-drag to look · scroll to zoom");
            if(GUI.Button(new Rect(strip.xMax-202,strip.y+63,90,23),"Recenter",button)) _cameraController.RecenterCockpit();
            if(GUI.Button(new Rect(strip.xMax-106,strip.y+63,94,23),"Overview (Esc)",button)) ExitCockpit(true);
            DrawToast(AirlineHudLayout.Create(layout,false).Toast);
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
        private static AircraftViewMode ReadFlightReviewMode()
        {
            var args=Environment.GetCommandLineArgs();var i=Array.IndexOf(args,"-airsideReviewFlightView");
            return i>=0 && i+1<args.Length && Enum.TryParse<AircraftViewMode>(args[i+1],true,out var mode)
                ? mode : AircraftViewMode.Cockpit;
        }
        private static readonly bool FlightViewCycleReview=Array.IndexOf(Environment.GetCommandLineArgs(),"-airsideReviewViewCycle")>=0;
        private float _viewCycleStarted=-1;
        private int _viewCycleShot=-1;
        private void UpdateFlightViewReview()
        {
            if(!FlightViewCycleReview || !SoakMode || !InCockpit || !_cockpitReviewStarted) return;
            if(_viewCycleStarted<0)
            {
                if(_flightOriginX == 0 && _flightOriginZ == 0) return;
                _viewCycleStarted=Time.unscaledTime;
            }
            var slot=(int)((Time.unscaledTime-_viewCycleStarted)/6f);
            if(slot<=_viewCycleShot || slot>4 || !_fleetAircraftById.TryGetValue(_cockpitAircraftId,out var aircraft)) return;
            _viewCycleShot=slot;
            var mode=slot == 0 ? AircraftViewMode.LeftWindow : slot == 1 ? AircraftViewMode.RightWindow
                : slot == 2 ? AircraftViewMode.Exterior : AircraftViewMode.Cockpit;
            var output=System.IO.Path.Combine(Application.persistentDataPath,"FlightViewReview");
            var args=Environment.GetCommandLineArgs();var i=Array.IndexOf(args,"-airsideReviewViewOutput");
            if(i>=0 && i+1<args.Length) output=args[i+1];
            System.IO.Directory.CreateDirectory(output);
            if(slot == 4) ExitCockpit(true);
            else if(!EnterFlightView(aircraft,mode))
            { Debug.LogError("[Airside view] switch failed "+mode);Application.Quit(2);return; }
            Debug.Log($"[Airside view] {slot} {mode} origin {_flightOriginX},{_flightOriginZ} interior {InteriorListening}");
            StartCoroutine(CaptureFlightViewReview(System.IO.Path.Combine(output,slot == 4 ? "4-overview.png" : slot+"-"+mode+".png"),slot == 4));
        }
        private System.Collections.IEnumerator CaptureFlightViewReview(string path,bool quit)
        {
            yield return CaptureReviewShot(path);
            if(quit){Debug.Log("[Airside view] COMPLETE five view captures");Application.Quit();}
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
            var reviewMode=ReadFlightReviewMode();
            _cockpitReviewStarted = EnterFlightView(best,reviewMode);
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
