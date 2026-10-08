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
        private readonly FlightTelemetrySampler _cockpitTelemetry = new();
        private double _cockpitPreviousTime;
        private float _cockpitGroundKnots;
        private readonly CockpitMotion _cockpitMotion = new();
        private readonly CockpitHapticScheduler _cockpitHaptics = new();
        private bool _cockpitControlsHinted;
        private readonly CockpitCallouts _cockpitCallouts = new();
        private string _cockpitCallText;
        private float _cockpitCallUntil;
        private GUIStyle _calloutStyle;
        private bool _cockpitIsJet;
        private float _cockpitGearHeight, _cockpitVerticalSpeed;
        private static int CockpitSeedHash(string text)
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

        private string FlightViewReason(FleetAircraft aircraft, AircraftViewMode mode)
        {
            if (mode == AircraftViewMode.Cockpit) return CockpitReason(aircraft);
            if (aircraft != null && aircraft.IsFreighter && mode != AircraftViewMode.Exterior) return "Cargo cabin";
            if (aircraft == null) return "Select an aircraft";
            if (mode != AircraftViewMode.Exterior && !PassengerCabinProfile.TryFor(aircraft.Type.Id, out _)) return "Passenger view unavailable";
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
                var started = _cameraController.StartPassenger(cabin.Seat);
                if (started) PlayUiClick();
                return started;
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
            PlayUiClick();
            // Once per run: how to look round, since the free-camera keys do not apply in here.
            if (!_cockpitControlsHinted && CockpitControlHints.EntryToast((int)mode) is { } hint)
            { _cockpitControlsHinted = true; ShowToast(hint); }
            return true;
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
            ReleaseCockpitAirflow();
            if (_cockpitInterior != null)
            {
                _cockpitInterior.Leave();
                Destroy(_cockpitInterior.gameObject);
            }
            _cockpitView = view;
            _cockpitInterior=null;
            var type = _fleetAircraftById[_cockpitAircraftId].Type;
            var parts=PartsFor(view);
            var lift=parts.Profile!=null ? AircraftGearPivot.LiftMetres(view.rotation,
                new Vector3(0f,parts.Profile.ModelGroundOffsetMetres,parts.MainGearZMetres)) : 0f;
            _cockpitTelemetry.Reset(view.position.x,view.position.z,_flightOriginX,_flightOriginZ,
                Mathf.Max(0f,view.position.y-lift-AirsideFlightPath.GroundY),_preciseTime);
            _cockpitPreviousTime = _preciseTime;
            _cockpitGroundKnots = 0f;
            _cockpitNextReadout = 0;
            _cockpitMotion.Reset(CockpitSeedHash(_cockpitAircraftId));
            _cockpitHaptics.Clear();
            _cockpitCallouts.Reset();
            _cockpitIsJet = JetCockpitProfile.TryFor(_fleetAircraftById[_cockpitAircraftId].Type.Id, out _);
            _cockpitCallText = null;
            _cockpitGearHeight = Mathf.Max(0f, view.position.y - AirsideFlightPath.GroundY);
            _cockpitVerticalSpeed = 0f;
            if (_aircraftViewMode == AircraftViewMode.Exterior) return;
            if (_aircraftViewMode == AircraftViewMode.LeftWindow || _aircraftViewMode == AircraftViewMode.RightWindow)
                _cockpitInterior=PassengerCabinInterior.Build(view,type,_aircraftViewMode == AircraftViewMode.RightWindow);
            else _cockpitInterior = type.Id == AircraftType.Bell412.Id
                ? BellCockpitInterior.Build(view)
                : type.Id == AircraftType.Saab340.Id
                ? SaabCockpitInterior.Build(view)
                : JetCockpitProfile.TryFor(type.Id, out _) ? JetCockpitInterior.Build(view, type)
                : TurbopropCockpitInterior.Create(view, type);
            if (_cockpitInterior == null) return;
            _cockpitInterior.Enter(false);
        }

        /// <summary>Hide the airframe once the glide is this far in: the lens is by then inside the skin, which is
        /// back-face culled, so the swap lands under the camera instead of a visible pop at the very end.</summary>
        private const float ExteriorHideProgress = 0.6f;

        private void ExitCockpit(bool overview)
        {
            if (!InCockpit) return;
            var id = _cockpitAircraftId;
            ReleaseCockpitAirflow();
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
            if (_cockpitInterior != null && _cameraController != null)
                _cockpitInterior.SetExteriorHidden(_cameraController.SeatBlendProgress >= ExteriorHideProgress);
            var elapsed = _preciseTime - _cockpitPreviousTime;
            var parts = PartsFor(view);
            // Wheel height: take the gear-pivot lift back out so a pitched-up roll still reads as on the ground.
            var lift = parts.Profile != null
                ? AircraftGearPivot.LiftMetres(view.rotation,
                    new Vector3(0f, parts.Profile.ModelGroundOffsetMetres, parts.MainGearZMetres)) : 0f;
            var gearHeight = Mathf.Max(0f, view.position.y - lift - AirsideFlightPath.GroundY);
            var simRate = 0f;
            _cockpitTelemetry.Sample(view.position.x,view.position.z,_flightOriginX,_flightOriginZ,gearHeight,_preciseTime);
            _cockpitGroundKnots=(float)_cockpitTelemetry.GroundKnots;
            _cockpitVerticalSpeed=(float)_cockpitTelemetry.VerticalMetresPerSecond;
            if(elapsed>0) simRate=Time.unscaledDeltaTime>1e-4f ? (float)elapsed/Time.unscaledDeltaTime : 1f;
            _cockpitPreviousTime = _preciseTime;
            _cockpitGearHeight = gearHeight;
            if (_aircraftViewMode == AircraftViewMode.Exterior) return;
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
            // Trackpad taps for the same events the camera shakes to. Only while the window has focus,
            // and only with the Vibration toggle on (it governs both the shake and the trackpad).
            _cockpitHaptics.Post(_cockpitMotion.TakeHaptic());
            var tap = _cockpitHaptics.Step(Time.unscaledDeltaTime, _cockpitMotion.Rumble01,
                _cameraController.CockpitMotionEnabled && Application.isFocused);
            if (tap != HapticKind.None) MacTrackpadHaptics.Perform(tap);
            _cockpitInterior.SetAttitude(pitchUp, bankLeft);
            var observerRain = CockpitObserverWeather.Rain(CurrentWeatherLook.Precipitation,
                _cockpitView != null ? _cockpitView.position.y : 0f, InCockpit && _cockpitView != null);
            _cockpitInterior.SetEnvironment(PresentationDaylight, observerRain, Time.unscaledTime);
            _cockpitCallouts.DeltaSeconds = (float)Math.Max(0.0, elapsed);
            var call = aircraft.Type.IsRotorcraft ? null : _cockpitCallouts.Step(new CockpitCallouts.Sample
            {
                GroundKnots = (float)FlightAtmosphere.CalibratedKnots(_cockpitGroundKnots,gearHeight+FlightAtmosphere.FieldElevationMetres), RotateKnots = AircraftPerformance.For(aircraft.Type).RotateKnots,
                HeightFeet = gearHeight * 3.28084f, VerticalFeetPerMinute = _cockpitVerticalSpeed * 196.85f,
                Jet = _cockpitIsJet,
            });
            if (call != null) { _cockpitCallText = call; _cockpitCallUntil = Time.unscaledTime + 2.2f; }
            // Cloud/storm buffet from the shared weather envelope; engine and runway feel come from CockpitMotion.
            var cloud = CockpitWeatherEnvelope.InCloud(view.position.y, CurrentWeatherLook.CloudCover);
            var inAir = gearHeight > 5f;
            _cameraController.SetCockpitRumble(inAir ? cloud * (CurrentWeather == WeatherKind.Storm ? 0.65f : 0.25f) : 0f);
            if (_preciseTime < _cockpitNextReadout) return;
            _cockpitNextReadout = _preciseTime + 0.1;
            if (_cockpitInterior is JetCockpitInterior jet)
                jet.SetFlightState(view, spool, AircraftStatus.TagPhase(aircraft, _clock.Now));
            var height = gearHeight * 3.28084f;
            var vs = _cockpitVerticalSpeed * 196.85f;   // ft/min
            var altitude=gearHeight+FlightAtmosphere.FieldElevationMetres;
            var cas=FlightAtmosphere.CalibratedKnots(_cockpitGroundKnots,altitude);
            var mach=FlightAtmosphere.Mach(_cockpitGroundKnots,altitude);
            _cockpitInterior.SetReadout($"IAS {cas:0} kt  M {mach:0.00}  GS {_cockpitGroundKnots:0}\nHEIGHT {height:0} ft  VS {vs:+0;-0;0}\nHDG {FlightViewInformation.TrueHeading(view.forward.x, view.forward.z):000}° T");
        }

        private readonly HudDrawList _flightViewDrawList = new();
        private readonly FlightViewHudData _flightViewHud = new();

        private void DrawCockpitHud(HudLayout layout, GUIStyle panel, GUIStyle button)
        {
            _hudPanels.Clear();
            _cameraController.KeyboardCaptured = _menuOpen || GUIUtility.keyboardControl != 0;
            var placement = new FlightViewHudLayout(layout.Viewport.x, layout.Viewport.y);
            _hudPanels.Add(HudPainter.ToRect(placement.Identity));
            _hudPanels.Add(HudPainter.ToRect(placement.Controls));
            if (_fleetAircraftById.TryGetValue(_cockpitAircraftId, out var aircraft) && _cockpitView != null)
            {
                var hasProfile = TryEnroute(aircraft, out var profile, out var elapsed);
                FlightViewInformation.Fill(_flightViewHud, aircraft, _operations.Home, _preciseTime,
                    _cockpitView.position.x, _cockpitView.position.z, _flightOriginX, _flightOriginZ,
                    _cockpitGearHeight, _cockpitView.forward.x, _cockpitView.forward.z,
                    _cockpitGroundKnots, _cockpitVerticalSpeed, hasProfile ? profile : null, elapsed);
                _flightViewHud.SelectedView = (int)_aircraftViewMode;
                _flightViewHud.MotionEnabled = _cameraController.CockpitMotionEnabled;
                _flightViewHud.PassengerIsCargo = aircraft.IsFreighter;
                for (var i = 0; i < 4; i++)
                    _flightViewHud.Available[i] = FlightViewReason(aircraft, (AircraftViewMode)i).Length == 0;
                _flightViewDrawList.Clear();
                FlightViewHudPainter.Paint(_flightViewDrawList, placement, _flightViewHud);
                var action = _hudPainter.Draw(_flightViewDrawList);
                var view = HudAction.Payload(action, FlightViewHudPainter.ViewPrefix);
                if (int.TryParse(view, out var index) && index >= 0 && index < 4 && index != (int)_aircraftViewMode)
                    EnterFlightView(aircraft, (AircraftViewMode)index);
                else if (action == FlightViewHudPainter.Overview) { ExitCockpit(true); PlayUiClick(); }
                else if (action == FlightViewHudPainter.Recenter) { _cameraController.RecenterCockpit(); PlayUiClick(); }
                else if (action == FlightViewHudPainter.Motion)
                {
                    _cameraController.CockpitMotionEnabled = !_cameraController.CockpitMotionEnabled;
                    AirsideSettings.Current.Save();
                    PlayUiClick();
                }
                if (InCockpit && _cockpitView != null)
                    DrawFlightMap(layout, placement, aircraft);
            }
            if (_aircraftViewMode == AircraftViewMode.Cockpit && !string.IsNullOrEmpty(_cockpitCallText) && Time.unscaledTime < _cockpitCallUntil)
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
            DrawToast(HudPainter.ToRect(placement.Toast));
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
