using Airside.Domain;
using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    public sealed partial class AirsidePrototype
    {
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
                _mainCamera != null ? _mainCamera.transform.position : view.position,
                _audioMuted, Time.unscaledDeltaTime);
        }
    }
}
