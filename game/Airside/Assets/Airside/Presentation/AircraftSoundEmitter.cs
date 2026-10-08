using System.Collections.Generic;
using Airside.Domain;
using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>Independent rotating cores, exhaust, starts and airframe voices; bounded by audibility.</summary>
    public sealed class AircraftSoundEmitter : MonoBehaviour
    {
        public const string TouchdownResource = "Airside/Audio/aircraft_touchdown_v01";
        public const string RollResource = "Airside/Audio/aircraft_tyre_roll_v01";
        public const int MaximumExteriorAircraft = 6;
        private static readonly Dictionary<string, AudioClip> Clips = new();
        private static readonly List<AircraftSoundEmitter> Audible = new();
        private readonly AircraftAudioContact _contact = new();
        private readonly List<AudioSource> _sources = new();
        private readonly List<AudioLowPassFilter> _filters = new();
        private AudioSource _idle, _power, _reverse, _wheels, _touchdown;
        private AudioSource _leftCore, _rightCore, _leftStarter, _rightStarter, _apu, _mechanical;
        private AudioClip _gearClip, _flapClip, _doorClip;
        private GameObject _host;
        private AircraftType _type;
        private EngineClass _kind;
        private float _range, _score, _heardAt, _previousLeft, _previousRight, _previousDoor;
        private bool _observed, _phaseObserved, _leftStarting, _rightStarting;
        private AircraftPhase _phase;
        private string _pendingMechanism;
        private float _mechanismUntil;
        public bool InteriorListening { get; set; }
        public bool PassengerListening { get; set; }
        public bool FollowListening { get; set; }

        public void Configure(AircraftType type, AudioClip fallbackEngine, AudioClip fallbackTouchdown)
        {
            if (_idle != null && _type?.Id == type?.Id) return;
            StopVoices();
            if (_host != null) { _host.SetActive(false); AirsideDestroy(_host); }
            _sources.Clear(); _filters.Clear();
            _observed = _phaseObserved = _leftStarting = _rightStarting = false;
            _pendingMechanism = null;
            _type = type;
            _kind = EngineVoice.ClassOf(type);
            var profile = AircraftAudioProfiles.For(type);
            _host = new GameObject("Aircraft sound");
            _host.transform.SetParent(transform, false);
            _range = AircraftAudioMix.AudibleDistance(_kind);
            _idle = Source("Engine body", Load(profile.Resource("idle"), fallbackEngine), true, 115);
            _power = Source("Exhaust power", Load(profile.Resource("power"), fallbackEngine), true, 85);
            _reverse = Source("Reverse wash", Load(profile.Resource("reverse"), fallbackEngine), true, 75);
            _wheels = Source("Tyre roll", Load(RollResource, null), true, 125);
            _touchdown = Source("Tyre contact", Load(TouchdownResource, fallbackTouchdown), false, 55);
            var core = Load(profile.Resource("core"), null);
            _leftCore = Source("Left rotating core", core, true, 80);
            _rightCore = Source("Right rotating core", core, true, 80);
            var family = _kind == EngineClass.Rotorcraft ? "rotor" : _kind == EngineClass.Turboprop ? "prop" : "jet";
            var starter = Load("Airside/Audio/aircraft_starter_" + family + "_v02", null);
            _leftStarter = Source("Left starter", starter, true, 90);
            _rightStarter = Source("Right starter", starter, true, 90);
            // Spread two independent engines across the airframe, not two coincident mono voices.
            var spacing = _kind == EngineClass.Turboprop ? 3.5f : _kind == EngineClass.Widebody ? 8f : 5f;
            _leftCore.transform.localPosition = _leftStarter.transform.localPosition = new Vector3(-spacing, 1f, 0f);
            _rightCore.transform.localPosition = _rightStarter.transform.localPosition = new Vector3(spacing, 1f, 0f);
            _apu = Source("Auxiliary power", Load("Airside/Audio/aircraft_apu_v02", null), true, 145);
            _mechanical = Source("Airframe mechanism", null, false, 65);
            _gearClip = Load("Airside/Audio/aircraft_gear_v02", null);
            _flapClip = Load("Airside/Audio/aircraft_flap_v02", null);
            _doorClip = Load("Airside/Audio/aircraft_door_v02", null);
        }

        /// <summary>Consume phase/door edges even offscreen or muted. Restored flights do not replay cues.</summary>
        public void ObserveState(AircraftPhase phase, EngineState engines)
        {
            if (_phaseObserved)
            {
                if (_phase != phase)
                {
                    if (phase == AircraftPhase.Approach || phase == AircraftPhase.Departed)
                        _pendingMechanism = "gear";
                    else if (phase == AircraftPhase.Takeoff || phase == AircraftPhase.GoAround)
                        _pendingMechanism = "flap";
                }
                if ((_previousDoor > 0.5f) != (engines.PassengerDoor > 0.5f))
                    _pendingMechanism = "door";
            }
            _previousDoor = engines.PassengerDoor;
            _phase = phase;
            _phaseObserved = true;
        }

        public void Apply(string aircraftId, float power, float rotation, float left, float right,
            float reverse, float groundSpeed, bool grounded, bool landing, Vector3 listener,
            float zoomGain, bool muted, float deltaSeconds)
        {
            var touchdown = _contact.Observe(landing, grounded);
            // Engine starts and wind-downs are distinct: only rising individual spool drives the starter.
            if (_observed)
            {
                if (Mathf.Abs(left - _previousLeft) > 0.00001f) _leftStarting = left > _previousLeft;
                if (Mathf.Abs(right - _previousRight) > 0.00001f) _rightStarting = right > _previousRight;
            }
            _previousLeft = left; _previousRight = right; _observed = true;
            var mechanism = _pendingMechanism;
            _pendingMechanism = null;
            if (_idle == null) return;
            var distance = Vector3.Distance(listener, transform.position);
            _score = (InteriorListening || FollowListening ? 100f : 0f) + 1f - distance / _range;
            _heardAt = Time.unscaledTime;
            if (muted || !isActiveAndEnabled || distance >= _range || !ClaimVoice())
            {
                StopVoices();
                return;
            }
            var mix = AircraftAudioMix.For(_type, aircraftId, power, rotation, left, right,
                reverse, groundSpeed, grounded);
            var cameraGain = AircraftAudioDynamics.InteriorEngineGain(InteriorListening, PassengerListening);
            // Six aircraft keep a shared headroom budget; the followed airframe retains full presence.
            var trafficGain = FollowListening || InteriorListening ? 0.82f : 0.78f / Mathf.Sqrt(Mathf.Max(1, Audible.Count));
            var gain = zoomGain * cameraGain * trafficGain;
            var cutoff = EngineVoice.LowPassHz(distance, _range, power);
            var doppler = InteriorListening || FollowListening ? 0f : 0.2f;
            for (var i = 0; i < _sources.Count; i++)
            {
                var source = _sources[i];
                source.dopplerLevel = doppler;
                source.spatialBlend = InteriorListening ? 0f : 1f;
                source.panStereo = 0f;
                _filters[i].cutoffFrequency = InteriorListening
                    ? Mathf.Min(cutoff, PassengerListening ? 1100f : 1550f) : cutoff;
            }
            // Inside the hull retain two engines' stereo identity without camera-distance attenuation.
            _leftCore.panStereo = _leftStarter.panStereo = InteriorListening ? -0.28f : 0f;
            _rightCore.panStereo = _rightStarter.panStereo = InteriorListening ? 0.28f : 0f;
            // Forward/inlet core and aft exhaust have different directivity outside the airframe.
            var towards = listener - transform.position;
            var aft = towards.sqrMagnitude > 1f ? (1f - Vector3.Dot(transform.forward, towards.normalized)) * 0.5f : 0.5f;
            var exhaustGain = InteriorListening ? 1f : Mathf.Lerp(0.65f, 1.12f, aft);
            ApplyLoop(_idle, mix.Idle * gain, mix.Pitch, deltaSeconds);
            ApplyLoop(_power, mix.Power * gain * exhaustGain, mix.Pitch, deltaSeconds);
            ApplyLoop(_reverse, mix.Reverse * gain, mix.Pitch * 0.96f, deltaSeconds);
            ApplyLoop(_wheels, mix.Wheels * gain * (InteriorListening ? 2.2f : 1f), mix.WheelPitch, deltaSeconds);
            var coreGain = AircraftAudioProfiles.For(_type).Gain * (0.06f + 0.14f * power) * gain;
            ApplyLoop(_leftCore, coreGain * left * Mathf.Sqrt(Mathf.Clamp01(rotation)),
                AircraftAudioDynamics.CorePitch(_kind, _kind == EngineClass.Turboprop ? rotation * left : rotation, left) * 0.997f, deltaSeconds);
            ApplyLoop(_rightCore, coreGain * right * Mathf.Sqrt(Mathf.Clamp01(rotation)),
                AircraftAudioDynamics.CorePitch(_kind, _kind == EngineClass.Turboprop ? rotation * right : rotation, right) * 1.003f, deltaSeconds);
            ApplyLoop(_leftStarter, AircraftAudioDynamics.Starter(left, _leftStarting) * gain,
                0.45f + left * 1.15f, deltaSeconds);
            ApplyLoop(_rightStarter, AircraftAudioDynamics.Starter(right, _rightStarting) * gain,
                0.46f + right * 1.15f, deltaSeconds);
            var apu = grounded && _kind != EngineClass.Rotorcraft
                ? Mathf.Clamp01(1f - Mathf.Max(left, right)) * 0.07f : 0f;
            ApplyLoop(_apu, apu * gain, 1f, deltaSeconds);
            if (touchdown && _touchdown.clip != null)
            {
                _touchdown.pitch = Mathf.Clamp(AircraftAudioProfiles.For(_type).Pitch, 0.8f, 1.15f);
                _touchdown.volume = 0.38f * gain * (InteriorListening ? 1.6f : 1f);
                _touchdown.PlayOneShot(_touchdown.clip);
            }
            if (mechanism != null && Time.unscaledTime >= _mechanismUntil)
            {
                var clip = mechanism == "gear" ? _gearClip : mechanism == "door" ? _doorClip : _flapClip;
                if (clip != null)
                {
                    _mechanical.clip = clip;
                    _mechanical.volume = (InteriorListening ? 0.34f : 0.12f) * zoomGain;
                    _mechanical.PlayOneShot(clip);
                    _mechanismUntil = Time.unscaledTime + clip.length;
                }
            }
        }

        private bool ClaimVoice()
        {
            for (var i = Audible.Count - 1; i >= 0; i--)
                if (Audible[i] == null || !Audible[i].isActiveAndEnabled || Time.unscaledTime - Audible[i]._heardAt > 0.5f)
                    Audible.RemoveAt(i);
            if (Audible.Contains(this)) return true;
            if (Audible.Count < MaximumExteriorAircraft) { Audible.Add(this); return true; }
            var lowest = 0;
            for (var i = 1; i < Audible.Count; i++) if (Audible[i]._score < Audible[lowest]._score) lowest = i;
            if (Audible[lowest]._score >= _score) return false;
            Audible[lowest].StopVoices();
            Audible.Add(this);
            return true;
        }

        public void StopVoices()
        {
            Audible.Remove(this);
            foreach (var source in _sources) { if (source == null) continue; source.Stop(); source.volume = 0f; }
        }

        private void OnDisable() => StopVoices();
        private void OnDestroy() => Audible.Remove(this);

        private static void ApplyLoop(AudioSource source, float volume, float pitch, float dt)
        {
            if (source.clip == null) return;
            source.volume = AircraftAudioMix.SmoothTowards(source.volume, volume, dt, volume > source.volume ? 0.32f : 0.55f);
            source.pitch = AircraftAudioMix.SmoothTowards(source.pitch, pitch, dt, 0.45f);
            if (source.volume <= 0.001f && volume <= 0.001f) { source.Stop(); return; }
            if (!source.isPlaying && AirsidePrototype.CanStartAudio(source))
            {
                source.time = source.clip.length * (0.15f + 0.65f * Mathf.Repeat(source.GetInstanceID() * 0.618f, 1f));
                source.Play();
            }
        }

        private AudioSource Source(string name, AudioClip clip, bool loop, int priority)
        {
            // Unity filters are per GameObject: separate hosts are essential for engine/directivity bands.
            var host = new GameObject(name);
            host.transform.SetParent(_host.transform, false);
            var source = host.AddComponent<AudioSource>();
            _filters.Add(host.AddComponent<AudioLowPassFilter>());
            source.playOnAwake = false; source.loop = loop; source.clip = clip; source.volume = 0f;
            source.spatialBlend = 1f; source.dopplerLevel = 0.2f; source.priority = priority;
            source.minDistance = 25f; source.maxDistance = _range;
            source.rolloffMode = AudioRolloffMode.Custom;
            source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(0.04f, 0.72f), new Keyframe(0.15f, 0.32f),
                new Keyframe(0.4f, 0.10f), new Keyframe(0.75f, 0.025f), new Keyframe(1f, 0f)));
            _sources.Add(source);
            return source;
        }

        private static AudioClip Load(string name, AudioClip fallback)
        {
            if (Clips.TryGetValue(name, out var clip)) return clip != null ? clip : fallback;
            clip = Resources.Load<AudioClip>(name);
            Clips[name] = clip; // Cache a miss too: absent optional resources must not hit disk every frame.
            return clip != null ? clip : fallback;
        }

        private static void AirsideDestroy(GameObject go)
        {
            if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
        }
    }
}
