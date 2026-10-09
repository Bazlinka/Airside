using Airside.Domain;
using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    public sealed partial class AirsidePrototype
    {
        private AudioSource _cockpitAirflow, _cabinRumble, _cabinRain;
        private AudioClip _cockpitAirflowClip;
        private string _cabinAudioFamily;

        private void UpdateCockpitAirflow()
        {
            if (!SoundInteriorListening)
            {
                if (_cockpitAirflow != null) ReleaseCockpitAirflow();
                return;
            }
            var type = WatchingOutstation ? WatchedOutstation()?.Type
                : _fleetAircraftById.TryGetValue(_cockpitAircraftId, out var aircraft) ? aircraft.Type : AircraftType.Atr42;
            var kind = EngineVoice.ClassOf(type);
            var family = kind == EngineClass.Rotorcraft ? "rotor" : kind == EngineClass.Turboprop ? "prop" : "jet";
            if (_cockpitAirflow != null && family != _cabinAudioFamily) ReleaseCockpitAirflow();
            if (_cockpitAirflow == null)
            {
                _cabinAudioFamily = family;
                var clip = Resources.Load<AudioClip>("Airside/Audio/aircraft_airflow_v02");
                if (clip == null)
                {
                    var samples = CockpitAirflow.Samples();
                    _cockpitAirflowClip = AudioClip.Create("Cockpit filtered airflow fallback", samples.Length,
                        1, CockpitAirflow.SampleRate, false);
                    _cockpitAirflowClip.SetData(samples, 0);
                    clip = _cockpitAirflowClip;
                }
                _cockpitAirflow = InteriorSource(clip, 130, 2600f);
                _cabinRumble = InteriorSource(Resources.Load<AudioClip>("Airside/Audio/aircraft_cabin_" + family + "_v02"), 120, 850f);
                _cabinRain = InteriorSource(_ambientRainAudio != null ? _ambientRainAudio.clip : null, 140, 1400f);
            }
            var knots = _cockpitGroundKnots;
            var height = _cockpitGearHeight;
            if (WatchingOutstation && OutstationJourney.TryFor(WatchedOutstation(), _preciseTime, out var journey))
            {
                knots = (float)journey.SpeedKnots;
                height = (float)(journey.AltitudeFeet / EnrouteProfile.FeetPerMetre);
            }
            // A real cabin's pressure/pack bed stays under the wind, while speed adds rushing air.
            var passenger = SoundPassengerListening;
            InteriorLoop(_cockpitAirflow, _audioMuted ? 0f : AircraftAudioDynamics.AirflowGain(knots, passenger), 1f);
            InteriorLoop(_cabinRumble, _audioMuted ? 0f : (passenger ? 0.24f : 0.17f), 1f);
            var rain = CockpitObserverWeather.Rain(CurrentWeatherLook.Precipitation, height, true, _stormDepth);
            InteriorLoop(_cabinRain, _audioMuted ? 0f : 0.12f * rain, 1f);
        }

        private AudioSource InteriorSource(AudioClip clip, int priority, float cutoff)
        {
            var host = new GameObject("Interior sound");
            host.transform.SetParent(transform, false);
            // Unity rejects (and returns null for) an audio filter added to a host that has no
            // AudioSource/AudioListener yet, so the source must exist before its filter.
            var source = host.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = true; source.spatialBlend = 0f;
            source.dopplerLevel = 0f; source.priority = priority; source.volume = 0f; source.clip = clip;
            var filter = host.AddComponent<AudioLowPassFilter>();
            if (filter != null) filter.cutoffFrequency = cutoff;
            return source;
        }

        private void InteriorLoop(AudioSource source, float volume, float pitch)
        {
            if (source == null || source.clip == null) return;
            if (_audioMuted) { source.Stop(); source.volume = 0f; return; }
            source.volume = AircraftAudioMix.SmoothTowards(source.volume, volume * AmbientDuck, Time.unscaledDeltaTime, 0.85f);
            source.pitch = pitch;
            if (volume <= 0.001f && source.volume <= 0.001f) source.Stop();
            else if (!source.isPlaying && CanStartAudio(source)) source.Play();
        }

        private void ReleaseCockpitAirflow()
        {
            ReleaseInteriorSource(_cockpitAirflow);
            ReleaseInteriorSource(_cabinRumble);
            ReleaseInteriorSource(_cabinRain);
            if (_cockpitAirflowClip != null) DestroyPresentationObject(_cockpitAirflowClip);
            _cockpitAirflow = _cabinRumble = _cabinRain = null;
            _cockpitAirflowClip = null;
            _cabinAudioFamily = null;
        }

        private void ReleaseInteriorSource(AudioSource source)
        {
            if (source == null) return;
            source.Stop(); source.clip = null;
            DestroyPresentationObject(source.gameObject);
        }
    }
}
