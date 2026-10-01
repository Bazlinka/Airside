using System.Collections.Generic;
using Airside.Domain;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>ADR 0192: one spatial voice per aircraft, blending idle, load, reverse and tyres.</summary>
    public sealed class AircraftSoundEmitter : MonoBehaviour
    {
        public const string TouchdownResource = "Airside/Audio/aircraft_touchdown_v01";
        public const string RollResource = "Airside/Audio/aircraft_tyre_roll_v01";
        private static readonly Dictionary<string, AudioClip> Clips = new();
        private readonly AircraftAudioContact _contact = new();
        private AudioSource _idle;
        private AudioSource _power;
        private AudioSource _reverse;
        private AudioSource _wheels;
        private AudioSource _touchdown;
        private AudioLowPassFilter[] _filters;
        private AircraftType _type;
        private float _range;
        public bool InteriorListening { get; set; }

        public void Configure(AircraftType type, AudioClip fallbackEngine, AudioClip fallbackTouchdown)
        {
            if (_idle != null && _type?.Id == type?.Id)
                return;
            if (_idle != null)
            {
                StopVoices();
                foreach (Transform child in transform)
                    if (child.name == "Aircraft sound")
                        AirsideDestroy(child.gameObject);
            }
            _type = type;
            var profile = AircraftAudioProfiles.For(type);
            var host = new GameObject("Aircraft sound");
            host.transform.SetParent(transform, false);
            _range = AircraftAudioMix.AudibleDistance(EngineVoice.ClassOf(type));
            _idle = Source(host, Load(profile.Resource("idle"), fallbackEngine), true, 115);
            _power = Source(host, Load(profile.Resource("power"), fallbackEngine), true, 85);
            _reverse = Source(host, Load(profile.Resource("reverse"), fallbackEngine), true, 75);
            // Missing optional tyre layers go silent; engine operation still survives.
            _wheels = Source(host, Load(RollResource, null), true, 125);
            _touchdown = Source(host, Load(TouchdownResource, fallbackTouchdown), false, 55);
            _filters = new[] { Filter(_idle), Filter(_power), Filter(_reverse), Filter(_wheels), Filter(_touchdown) };
        }

        public void Apply(string aircraftId, float power, float rotation, float left, float right,
            float reverse, float groundSpeed, bool grounded, bool landing, Vector3 listener,
            float zoomGain, bool muted, float deltaSeconds)
        {
            // Consume contact even while muted, so unmute cannot produce an old chirp.
            var touchdown = _contact.Observe(landing, grounded);
            if (_idle == null)
                return;
            var distance = Vector3.Distance(listener, transform.position);
            if (muted || !isActiveAndEnabled || distance >= _range)
            {
                StopVoices();
                return;
            }
            var mix = AircraftAudioMix.For(_type, aircraftId, power, rotation, left, right,
                reverse, groundSpeed, grounded);
            var cutoff = EngineVoice.LowPassHz(distance, _range, power);
            if (InteriorListening) cutoff = Mathf.Min(cutoff, 1800f);
            for (var i = 0; i < _filters.Length; i++)
                _filters[i].cutoffFrequency = cutoff;
            // The listener rides this airframe. Pose updates/floating-origin steps must
            // not pitch-bend its engine, reverse or tyre voices as if it flew past.
            var doppler = InteriorListening ? 0f : 0.2f;
            _idle.dopplerLevel = _power.dopplerLevel = _reverse.dopplerLevel = doppler;
            _wheels.dopplerLevel = _touchdown.dopplerLevel = doppler;
            ApplyLoop(_idle, mix.Idle * zoomGain, mix.Pitch, deltaSeconds);
            ApplyLoop(_power, mix.Power * zoomGain, mix.Pitch, deltaSeconds);
            ApplyLoop(_reverse, mix.Reverse * zoomGain, mix.Pitch * 0.96f, deltaSeconds);
            ApplyLoop(_wheels, mix.Wheels * zoomGain, mix.WheelPitch, deltaSeconds);
            if (touchdown && _touchdown.clip != null)
            {
                var profile = AircraftAudioProfiles.For(_type);
                _touchdown.pitch = Mathf.Clamp(profile.Pitch, 0.8f, 1.15f);
                _touchdown.volume = 0.36f * profile.Gain * zoomGain;
                _touchdown.PlayOneShot(_touchdown.clip);
            }
        }

        public void StopVoices()
        {
            Stop(_idle); Stop(_power); Stop(_reverse); Stop(_wheels); Stop(_touchdown);
        }

        private void OnDisable() => StopVoices();

        private static void Stop(AudioSource source)
        {
            if (source == null) return;
            source.Stop();
            source.volume = 0f;
        }

        private static void ApplyLoop(AudioSource source, float volume, float pitch, float dt)
        {
            if (source.clip == null)
                return;
            source.volume = AircraftAudioMix.SmoothTowards(source.volume, volume, dt, 0.28f);
            source.pitch = AircraftAudioMix.SmoothTowards(source.pitch, pitch, dt, 0.65f);
            if (source.volume <= 0.001f && volume <= 0.001f)
            {
                source.Stop();
                return;
            }
            if (!source.isPlaying && AirsidePrototype.CanStartAudio(source))
            {
                // Different layers/types/airframes never start on an identical loop phase.
                source.time = source.clip.length * (0.15f + 0.65f * Mathf.Repeat(source.GetInstanceID() * 0.618f, 1f));
                source.Play();
            }
        }

        private AudioSource Source(GameObject host, AudioClip clip, bool loop, int priority)
        {
            var source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.clip = clip;
            source.volume = 0f;
            source.spatialBlend = 1f;
            // Stronger doppler on time-warped or teleporting traffic sounds like a siren.
            source.dopplerLevel = 0.2f;
            source.priority = priority;
            source.minDistance = 25f;
            source.maxDistance = _range;
            source.rolloffMode = AudioRolloffMode.Custom;
            source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(0.04f, 0.72f), new Keyframe(0.15f, 0.32f),
                new Keyframe(0.4f, 0.10f), new Keyframe(0.75f, 0.025f), new Keyframe(1f, 0f)));
            return source;
        }

        private static AudioLowPassFilter Filter(AudioSource source)
        {
            // All sources share their host, so one filter applies to the combined aircraft voice.
            var filter = source.GetComponent<AudioLowPassFilter>();
            return filter != null ? filter : source.gameObject.AddComponent<AudioLowPassFilter>();
        }

        private static AudioClip Load(string name, AudioClip fallback)
        {
            if (Clips.TryGetValue(name, out var clip) && clip != null)
                return clip;
            clip = Resources.Load<AudioClip>(name);
            if (clip != null) Clips[name] = clip;
            return clip != null ? clip : fallback;
        }

        private static void AirsideDestroy(GameObject go)
        {
            go.SetActive(false);
            if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
        }
    }
}
