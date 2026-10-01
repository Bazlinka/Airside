using Airside.Domain;
using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    public sealed partial class AirsidePrototype
    {
        private Transform _audioListener;
        private float _aircraftZoomGain = 1f;

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
            host.AddComponent<AudioListener>();
            _audioListener = host.transform;
            UpdateFocusAudioListener();
        }

        private void UpdateFocusAudioListener()
        {
            if (_audioListener == null || _mainCamera == null)
                return;
            if (InCockpit && _cameraController != null && _cameraController.IsCockpit)
            {
                _audioListener.SetPositionAndRotation(_cameraController.CockpitPosition, _cameraController.CockpitRotation);
                _aircraftZoomGain = 0.32f;
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
                var progress = VisualPhaseProgress(flight, 0f);
                var engines = FleetEngines(flight) ?? (AirsideReusableMotion.PropellersSpinning(phase)
                    ? EngineState.Running : EngineState.ColdAndOpen);
                UpdateAircraftSound(_commercialAircraft[i], type, phase, flight.AircraftId, progress,
                    engines, FleetTireRollSpeed(flight, phase, progress, type));
            }
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
            emitter.InteriorListening = InCockpit;
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
                _aircraftZoomGain, _audioMuted, Time.unscaledDeltaTime);
        }
    }
}
