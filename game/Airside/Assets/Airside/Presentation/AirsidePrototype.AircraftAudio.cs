using Airside.Domain;
using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    public sealed partial class AirsidePrototype
    {
        private Transform _audioListener;
        private float _aircraftZoomGain = 1f;
        private AircraftViewMode _outstationAudioMode;
        private bool SoundInteriorListening => InteriorListening
            || (WatchingOutstation && _outstationAudioMode != AircraftViewMode.Exterior);
        private bool SoundPassengerListening => WatchingOutstation
            ? _outstationAudioMode != AircraftViewMode.Cockpit && _outstationAudioMode != AircraftViewMode.Exterior
            : InteriorListening && _aircraftViewMode != AircraftViewMode.Cockpit;
        private string SoundAircraftId => WatchingOutstation ? _outstationWatchId : _cockpitAircraftId;
        // Ground weather fades with height; Adelaide waves never travel with an enroute camera.
        private float ExteriorWeatherGain => SoundInteriorListening ? 0f
            : Mathf.Clamp01(1f - (_audioListener != null ? _audioListener.position.y : 0f) / 1400f);
        private float CoastAudioGain => AirportPresentationVisible && !InCockpit && !WatchingOutstation
            ? ExteriorWeatherGain : 0f;

        /// <summary>
        /// Aircraft are heard from the ground point the camera looks at (ADR 0196), so the
        /// 2.4 km overview still hears what is near its focus; zoom distance only fades them.
        /// </summary>
        private void EnsureFocusAudioListener(Camera camera)
        {
            foreach (var existing in camera.GetComponents<AudioListener>())
                existing.enabled = false;
            if (_audioListener != null)
                return;
            var host = new GameObject("Focus audio listener");
            host.transform.SetParent(transform, false);
            host.AddComponent<AudioListener>();
            host.AddComponent<AirsideAudioLimiter>();
            _audioListener = host.transform;
            UpdateFocusAudioListener();
        }

        private void UpdateFocusAudioListener()
        {
            if (_audioListener == null || _mainCamera == null)
                return;
            if ((InCockpit || WatchingOutstation) && _cameraController != null && _cameraController.IsCockpit)
            {
                _audioListener.SetPositionAndRotation(_mainCamera.transform.position, _mainCamera.transform.rotation);
                _aircraftZoomGain = SoundInteriorListening ? 1f : AircraftAudioMix.ZoomGain(AirsideCameraController.CurrentDistance);
                return;
            }
            var cameraTransform = _mainCamera.transform;
            var distance = AirsideCameraController.CurrentDistance;
            var focus = _cameraController != null ? _cameraController.FocusPoint : cameraTransform.position;
            _audioListener.SetPositionAndRotation(
                focus + Vector3.up * AircraftAudioMix.ListenerLift(distance),
                Quaternion.Euler(0f, cameraTransform.eulerAngles.y, 0f));
            _aircraftZoomGain = AircraftAudioMix.ZoomGain(distance);
        }

        private void UpdateEngineAudio()
        {
            for (var i = 0; i < VisualFlights.Count && i < _commercialAircraft.Length; i++)
            {
                var flight = VisualFlights[i];
                var phase = flight.Operation.Phase;
                var type = FleetMode && _fleetAircraftById.TryGetValue(flight.AircraftId, out var fleet)
                    ? fleet.Type : AircraftType.Atr42;
                // A helicopter's rotor sound is driven by its own pose in UpdateHelicopterView (ADR 0207).
                if (type.IsRotorcraft)
                    continue;
                var progress = VisualPhaseProgress(flight, 0f);
                JourneyAudioPhase(flight, type, ref phase, ref progress);
                var engines = FleetEngines(flight) ?? (AirsideReusableMotion.PropellersSpinning(phase)
                    ? EngineState.Running : EngineState.ColdAndOpen);
                UpdateAircraftSound(_commercialAircraft[i], type, phase, flight.AircraftId, progress,
                    engines, FleetTireRollSpeed(flight, phase, progress, type));
            }
            if (WatchingOutstation && _outstationWatchView != null)
            {
                var aircraft = WatchedOutstation();
                if (OutstationJourney.TryFor(aircraft, _preciseTime, out var journey) && journey.Airborne)
                    UpdateAircraftSound(_outstationWatchView, aircraft.Type, AircraftPhase.Departed,
                        aircraft.Registration, (float)journey.Progress, EngineState.Running,
                        (float)journey.SpeedKnots / 1.943844f);
            }
        }

        private void JourneyAudioPhase(CommercialFlight flight, AircraftType type,
            ref AircraftPhase phase, ref float progress)
        {
            if (!WatchingJourney(flight.AircraftId)
                || !_fleetAircraftById.TryGetValue(flight.AircraftId, out var aircraft)) return;
            if (aircraft.State == FleetState.AtDestination) { phase = AircraftPhase.AtStand; progress = 1f; return; }
            if (!TryEnroute(aircraft, out var profile, out var elapsed)) return;
            var rotate = RegionalFlightPath.RotateSeconds(type);
            var performance = AircraftPerformance.For(type);
            phase = RegionalFlightPath.JourneyPhase(aircraft.State, elapsed, profile.LegSeconds, rotate);
            if (aircraft.State == FleetState.Inbound || phase == AircraftPhase.Approach) progress = 1f;
            if (aircraft.State == FleetState.Inbound && elapsed < RegionalFlightPath.DepartureSeconds)
                progress = elapsed < rotate ? (float)(elapsed / rotate) * performance.RotateProgress
                    : (float)((elapsed - rotate) / (RegionalFlightPath.DepartureSeconds - rotate));
            else if (aircraft.State == FleetState.Outbound
                && profile.LegSeconds - elapsed <= RegionalFlightPath.RolloutSeconds)
                progress = Mathf.Lerp(performance.TouchdownProgress, 1f,
                    1f - (float)((profile.LegSeconds - elapsed) / RegionalFlightPath.RolloutSeconds));
        }

        private void UpdateAircraftSound(Transform view, AircraftType type, AircraftPhase phase,
            string aircraftId, float progress, EngineState engines, float groundSpeed)
        {
            if (view == null) return;
            var id = view.GetInstanceID();
            if (!_engineAudio.TryGetValue(id, out var emitter) || emitter == null)
            {
                emitter = view.GetComponent<AircraftSoundEmitter>();
                if (emitter == null) return;
                _engineAudio[id] = emitter;
            }
            emitter.Configure(type, LoadEngineClip(type), CreateTouchdownClip());
            emitter.InteriorListening = SoundInteriorListening && aircraftId == SoundAircraftId;
            emitter.PassengerListening = SoundPassengerListening && aircraftId == SoundAircraftId;
            emitter.FollowListening = aircraftId == SoundAircraftId
                || (aircraftId == _selectedAircraftId && _cameraController != null && _cameraController.IsFollowing);
            emitter.ObserveState(phase, engines);
            var power = EnginePower(view);
            var prop = EngineVoice.ClassOf(type) == EngineClass.Turboprop;
            var rotation = prop
                ? (_propRpm.TryGetValue(id, out var rpm) ? rpm / AirsidePropellerDynamics.GovernedRpm : 0f)
                : power;
            // A missing animated fan/prop assembly must not make a valid aircraft silent.
            if (!_propPower.ContainsKey(id))
            {
                power = prop ? AirsidePropellerDynamics.PowerFractionForPhase(phase, progress)
                             : AirsidePropellerDynamics.JetN1ForPhase(phase, progress);
                rotation = prop ? AirsidePropellerDynamics.NpFractionForPhase(phase) : power;
                rotation *= Mathf.Max(engines.Left, engines.Right);
            }
            var grounded = AircraftAudioMix.Grounded(type, phase, progress);
            var reverse = AircraftAudioMix.ReverseDemand(type, phase, progress);
            emitter.Apply(aircraftId, power, rotation, engines.Left, engines.Right, reverse, groundSpeed,
                grounded, phase == AircraftPhase.Landing,
                _audioListener != null ? _audioListener.position : view.position,
                _aircraftZoomGain * AmbientDuck, _audioMuted, Time.unscaledDeltaTime);
        }
    }
}
