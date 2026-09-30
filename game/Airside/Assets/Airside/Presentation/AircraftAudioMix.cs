using System;
using Airside.Domain;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>Representative per-type tuning; not an engine-variant specification (ADR 0192).</summary>
    public readonly struct AircraftAudioProfile
    {
        public AircraftAudioProfile(string key, float pitch, float gain)
        {
            Key = key;
            Pitch = pitch;
            Gain = gain;
        }

        public string Key { get; }
        public float Pitch { get; }
        public float Gain { get; }
        public string Resource(string layer) => $"Airside/Audio/eng_{Key}_{layer}_v01";
    }

    /// <summary>Layer targets, derived from presentation inputs only. No commands, clock or saved state.</summary>
    public readonly struct AircraftAudioMix
    {
        public AircraftAudioMix(float pitch, float idle, float power, float reverse, float wheels, float wheelPitch)
        {
            Pitch = pitch;
            Idle = idle;
            Power = power;
            Reverse = reverse;
            Wheels = wheels;
            WheelPitch = wheelPitch;
        }

        public float Pitch { get; }
        public float Idle { get; }
        public float Power { get; }
        public float Reverse { get; }
        public float Wheels { get; }
        public float WheelPitch { get; }

        /// <param name="rotation">Governed prop speed or jet N1 as a fraction of full speed.</param>
        /// <param name="grounded">Actual tyre contact, independent of phase entry.</param>
        public static AircraftAudioMix For(AircraftType type, string aircraftId, float power,
            float rotation, float left, float right, float reverse, float groundSpeed, bool grounded)
        {
            var profile = AircraftAudioProfiles.For(type);
            var prop = EngineVoice.ClassOf(type) == EngineClass.Turboprop;
            power = Clamp(power);
            rotation = Clamp(rotation);
            left = Clamp(left);
            right = Clamp(right);
            // Two independent starts: one lit engine has less energy than both, rather
            // than max(left,right) making the second start acoustically disappear.
            var running = (float)Math.Sqrt((left * left + right * right) * 0.5f);
            var load = Smooth(Clamp((power - (prop ? 0.06f : 0.21f)) / (prop ? 0.94f : 0.74f)));
            var detune = 1f + (EngineVoice.Detune(aircraftId) - 1f) * 0.35f;
            // The prop governor holds RPM; load mostly changes the spectrum and level.
            // Jet N1 has a much wider pitch sweep. No forced approach/takeoff volume floor.
            var revs = prop ? Lerp(0.76f, 1.04f, rotation) * Lerp(0.98f, 1.03f, load)
                            : Lerp(0.76f, 1.23f, rotation);
            var pitch = profile.Pitch * detune * revs;
            var energy = running * profile.Gain;
            var idle = 0.17f * energy * (float)Math.Sqrt(1f - load);
            var loaded = 0.44f * energy * (float)Math.Sqrt(load) * Lerp(0.45f, 1f, load);
            reverse = grounded ? Clamp(reverse) : 0f;
            var reversed = 0.30f * energy * reverse;
            // Withdraw some forward exhaust under reverse instead of doubling its energy.
            loaded *= 1f - reverse * 0.45f;
            var speed = Math.Abs(groundSpeed);
            var roll = grounded ? Clamp((speed - 0.8f) / 65f) : 0f;
            var wheels = 0.10f * profile.Gain * (float)Math.Pow(roll, 1.25);
            return new AircraftAudioMix(pitch, idle, loaded, reversed, wheels, Lerp(0.55f, 1.25f, roll));
        }

        public static float SmoothTowards(float current, float target, float deltaSeconds, float seconds)
            => Lerp(current, target, 1f - (float)Math.Exp(-Math.Max(0f, deltaSeconds) / Math.Max(0.01f, seconds)));

        public static float AudibleDistance(EngineClass kind) => kind switch
        {
            EngineClass.Turboprop => 1000f,
            EngineClass.RegionalJet => 1300f,
            EngineClass.Widebody => 2200f,
            _ => 1700f
        };

        public static float ReverseForRollout(float rollout01)
        {
            if (rollout01 <= 0f || rollout01 >= 0.55f) return 0f;
            return Smooth(Clamp(rollout01 / 0.09f))
                   * (1f - Smooth(Clamp((rollout01 - 0.30f) / 0.25f)));
        }

        public static bool Grounded(AircraftType type, AircraftPhase phase, float progress)
        {
            var performance = AircraftPerformance.For(type);
            return phase switch
            {
                AircraftPhase.Takeoff => progress < performance.RotateProgress,
                AircraftPhase.Landing => progress >= performance.TouchdownProgress,
                AircraftPhase.Approach or AircraftPhase.Departed or AircraftPhase.Circuit or AircraftPhase.GoAround => false,
                _ => true
            };
        }

        public static float ReverseDemand(AircraftType type, AircraftPhase phase, float progress)
        {
            if (phase != AircraftPhase.Landing || !Grounded(type, phase, progress)) return 0f;
            var contact = AircraftPerformance.For(type).TouchdownProgress;
            return ReverseForRollout((progress - contact) / Math.Max(0.0001f, 1f - contact));
        }

        private static float Clamp(float v) => Math.Max(0f, Math.Min(1f, v));
        private static float Smooth(float t) => t * t * (3f - 2f * t);
        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }

    /// <summary>Contact edge detector: initial late restoration and muted landings never replay.</summary>
    public sealed class AircraftAudioContact
    {
        private bool _observed;
        private bool _down;

        public bool Observe(bool landing, bool down)
        {
            var contact = landing && down;
            var fired = _observed && contact && !_down;
            _observed = true;
            _down = contact;
            return fired;
        }
    }
}
