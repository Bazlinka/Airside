using System;

namespace Airside.Presentation
{
    /// <summary>
    /// What the pilot's head and body feel on top of the rigid aircraft pose: runway joint and
    /// centreline-light thumps that scale with ground speed, tyre and engine vibration, the
    /// touchdown jolt and nose-wheel slam, lift-off unstick, gear retract/extend thumps and buffet,
    /// low-level approach turbulence, takeoff push-back and braking lean, and the gaze leading into
    /// a turn. Driven only by observed kinematics, so it works for local circuits, fleet traffic and
    /// regional journeys alike. Presentation only: never feeds back into the simulation, and every
    /// input is a plain float so the headless harness can test it. Offsets are in the seat frame
    /// (Right, Up, Forward metres); angles in degrees.
    /// </summary>
    public sealed class CockpitMotion
    {
        public struct Sample
        {
            public float DeltaSeconds;      // real (unscaled) frame time
            public float SimRate;           // 0 paused, 1 realtime, >1 fast-forward (damps shake)
            public float Time;              // monotonic real seconds, for noise phase
            public float GroundSpeed;       // m/s over the ground
            public float VerticalSpeed;     // m/s, up positive
            public float HeightAgl;         // aircraft root above the tarmac, metres
            public float PitchUpDegrees;    // body pitch, nose-up positive
            public float BankLeftDegrees;   // Unity z roll: positive is left wing down
            public float Spool;             // 0..1 engine spool
            public bool Turboprop;
            public float WeatherTurbulence; // 0 smooth air .. 1 developed storm body
        }

        public readonly struct Pose
        {
            public readonly float Right, Up, Forward, PitchDownDegrees, YawDegrees, RollDegrees;
            public Pose(float right, float up, float forward, float pitchDown, float yaw, float roll)
            { Right = right; Up = up; Forward = forward; PitchDownDegrees = pitchDown; YawDegrees = yaw; RollDegrees = roll; }
        }

        private struct Spring
        {
            public float X, V, Omega, Zeta;
            public void Step(float dt)
            {
                var a = -Omega * Omega * X - 2f * Zeta * Omega * V;
                V += a * dt; X += V * dt;
            }
        }

        public const float JointSpacingMetres = 7.2f;       // runway/taxiway slab joints
        public const float CentreLightSpacingMetres = 15.2f;
        public const float GearRetractDelaySeconds = 5.5f;
        public const float GearExtendHeightMetres = 330f;   // about 1,000 ft above the runway

        private Spring _heave = new() { Omega = 20f, Zeta = 0.30f };
        private Spring _pitch = new() { Omega = 15f, Zeta = 0.38f };
        private Spring _roll = new() { Omega = 13f, Zeta = 0.40f };
        private Spring _sway = new() { Omega = 11f, Zeta = 0.45f };
        private bool _started, _airborne, _noseDown, _gearDownDone, _gearUpDone, _everHigh;
        private float _prevVerticalSpeed, _prevSpeed, _surge, _heaveG, _lookYaw;
        private float _jointDistance, _lightDistance, _sinceLiftoff, _buffet, _firmness = 0.8f;
        private float _noseSettle, _rollSign = 1f, _sinceTouchdown, _weatherTurbulence;
        private bool _spoilersDone, _bounceDone;
        private bool _landed;
        private HapticKind _haptic;

        /// <summary>
        /// Continuous trackpad rumble is disabled. Discrete contact/gear taps remain available via TakeHaptic.
        /// </summary>
        public float Rumble01 { get; private set; }

        /// <summary>The strongest trackpad tap this step's events called for since the last call, then cleared.</summary>
        public HapticKind TakeHaptic()
        {
            var kind = _haptic;
            _haptic = HapticKind.None;
            return kind;
        }

        private void Tap(HapticKind kind)
        {
            if (kind > _haptic) _haptic = kind;
        }

        /// <summary>How hard this landing feels, in metres per second of sink. Deterministic per flight.</summary>
        public void Reset(int seed)
        {
            _heave = new Spring { Omega = 20f, Zeta = 0.30f };
            _pitch = new Spring { Omega = 15f, Zeta = 0.38f };
            _roll = new Spring { Omega = 13f, Zeta = 0.40f };
            _sway = new Spring { Omega = 11f, Zeta = 0.45f };
            _started = _airborne = _noseDown = _landed = _gearDownDone = _gearUpDone = _everHigh = false;
            _prevVerticalSpeed = _prevSpeed = _surge = _heaveG = _lookYaw = 0f;
            _jointDistance = _lightDistance = _sinceLiftoff = _buffet = _noseSettle = _sinceTouchdown = 0f;
            _spoilersDone = _bounceDone = false;
            _haptic = HapticKind.None;
            Rumble01 = 0f; _weatherTurbulence = 0f;
            // Most landings are smooth (0.5 m/s, ~100 fpm); a few are firm (up to ~1.5 m/s, ~300 fpm).
            var h = (uint)seed * 2654435761u;
            var u = ((h >> 8) & 0xFFFF) / 65535f;
            _firmness = 0.5f + 1.0f * u * u;
        }

        public Pose Step(Sample s)
        {
            var dt = Math.Min(Math.Max(s.DeltaSeconds, 0f), 0.05f);
            var rate = Clamp01(s.SimRate);
            var scale = Math.Max(s.SimRate, 0.25f);          // simulation seconds per real second
            var evt = 1f / Math.Max(1f, s.SimRate);          // fast-forward must not turn thumps into a buzz
            var onGround = s.HeightAgl < 0.35f;
            if (!_started)
            {
                _started = true; _airborne = !onGround; _prevSpeed = s.GroundSpeed;
                _prevVerticalSpeed = s.VerticalSpeed; _gearUpDone = !onGround; _gearDownDone = !onGround && s.HeightAgl < GearExtendHeightMetres;
            }

            // Smoothed sink and acceleration (low-pass), measured in simulation time via the speeds given.
            var accel = dt > 1e-5f && rate > 0f ? (s.GroundSpeed - _prevSpeed) / (dt * scale) : 0f;
            accel = Clamp(accel, -8f, 8f);
            var vertAccel = dt > 1e-5f && rate > 0f ? (s.VerticalSpeed - _prevVerticalSpeed) / (dt * scale) : 0f;
            vertAccel = Clamp(vertAccel, -6f, 6f);
            _prevSpeed = s.GroundSpeed; _prevVerticalSpeed = s.VerticalSpeed;
            var kInertia = 1f - (float)Math.Exp(-4f * dt);
            _surge += (accel - _surge) * kInertia;
            _heaveG += (vertAccel - _heaveG) * kInertia;

            // ---- discrete events -------------------------------------------------------------
            if (onGround && _airborne)
            {
                // Touchdown: main gear first. Heave kick scales with sink, a nose-down rock follows.
                var sink = _firmness;
                _heave.V -= 0.30f + 0.55f * sink;
                _pitch.V += 3f + 4.5f * sink;           // degrees/second, nose drops
                _roll.V += (0.4f + 0.6f * sink) * _rollSign;
                _airborne = false; _noseDown = false; _landed = true; _noseSettle = 0f; _sinceLiftoff = 0f;
                _rollSign = -_rollSign; _sinceTouchdown = 0f; _spoilersDone = _bounceDone = false;
                _gearDownDone = true; _everHigh = false;
                Tap(sink > 0.9f ? HapticKind.Heavy : HapticKind.Medium);
            }
            else if (!onGround && !_airborne)
            {
                _heave.V += 0.22f;                      // unstick as the wheels unload
                _airborne = true; _landed = false; _sinceLiftoff = 0f; _gearUpDone = false;
                Tap(HapticKind.Tick);
            }
            if (onGround && _landed && !_noseDown && s.GroundSpeed > 20f && s.PitchUpDegrees < 1.2f
                && _noseSettle > 0.5f)
            {
                // Nose wheel arrives: a second, smaller slam.
                _noseDown = true;
                _heave.V -= 0.20f + 0.20f * _firmness;
                _pitch.V += 2.2f;
                Tap(HapticKind.Medium);
            }
            if (onGround && !_noseDown) _noseSettle += dt;
            if (onGround && _landed && rate > 0f)
            {
                _sinceTouchdown += dt * scale;
                if (!_spoilersDone && _sinceTouchdown > 0.4f)
                {                                       // ground spoilers rise: lift dumps, the airframe settles
                    _spoilersDone = true; _heave.V -= evt * 0.09f; _pitch.V += evt * 1.1f;
                    Tap(HapticKind.Tick);
                }
                if (!_bounceDone && _sinceTouchdown > 0.9f && _firmness > 1.25f && s.GroundSpeed > 40f)
                {                                       // a firm arrival skips once before settling
                    _bounceDone = true; _heave.V += 0.35f; _pitch.V -= 2.0f;
                    Tap(HapticKind.Medium);
                }
            }
            if (_airborne)
            {
                _sinceLiftoff += dt * scale;
                if (s.HeightAgl > 450f) _everHigh = true;
                if (!_gearUpDone && _sinceLiftoff > GearRetractDelaySeconds && s.VerticalSpeed > 1f)
                { _gearUpDone = true; _heave.V -= 0.16f; _roll.V += 0.5f; _buffet = 1.2f; Tap(HapticKind.Medium); }
                if (!_gearDownDone && s.VerticalSpeed < -1f && s.HeightAgl < GearExtendHeightMetres && _everHigh)
                { _gearDownDone = true; _heave.V -= 0.20f; _pitch.V += 1.5f; _buffet = 3.5f; Tap(HapticKind.Medium); }
            }
            _buffet = Math.Max(0f, _buffet - dt);

            // ---- ground texture: slab joints and centreline lights tied to distance travelled ---
            if (onGround && rate > 0f)
            {
                var step = s.GroundSpeed * dt * scale;
                _jointDistance += step; _lightDistance += step;
                while (_jointDistance >= JointSpacingMetres)
                {
                    _jointDistance -= JointSpacingMetres; _heave.V -= evt * 0.035f / (1f + s.GroundSpeed / 25f);
                    // Slab joints move the suspension; avoid a repeated trackpad tap every few metres.
                }
                while (_lightDistance >= CentreLightSpacingMetres)
                { _lightDistance -= CentreLightSpacingMetres; if (s.GroundSpeed > 12f) { _heave.V -= evt * 0.016f; _pitch.V += evt * 0.15f; } }
            }

            // ---- continuous vibration ----------------------------------------------------------
            var t = s.Time;
            var speed01 = Smooth(s.GroundSpeed / 75f);
            var rumble = onGround ? 0.00015f + 0.0012f * speed01 : 0f;
            var power = Clamp01(s.Spool) * (onGround ? (_surge > 0.6f ? 1f : _surge < -0.8f ? 0.85f : 0.3f)
                                                      : (s.VerticalSpeed > 1.5f ? 0.9f : 0.5f));
            // Reverse thrust / beta: strong low-frequency roar through the seat while decelerating hard.
            var reversing = onGround && _landed && _surge < -0.8f && s.GroundSpeed > 25f && _sinceTouchdown > 1.0f;
            var reverse = reversing ? (s.Turboprop ? 0.0040f : 0.0030f) * Smooth(s.GroundSpeed / 50f) : 0f;
            var engine = Clamp01(s.Spool) * (s.Turboprop ? 0.00035f : 0.00012f) * (0.45f + 0.55f * power);
            var low = Math.Max(0f, 1f - s.HeightAgl / 600f);
            _weatherTurbulence += (Clamp01(s.WeatherTurbulence) - _weatherTurbulence)
                * (1f - (float)Math.Exp(-dt * 0.65f));
            var turbulence = onGround ? 0f : 0.00010f * low + 0.003f * _weatherTurbulence
                + (_buffet > 0f ? 0.0006f * Math.Min(1f, _buffet) : 0f);
            var noise = Noise(t, 5.3f, 8.1f, 12.7f);
            var slow = Noise(t, 0.13f, 0.27f, 0.43f);
            var throb = s.Turboprop ? (float)Math.Sin(t * 6.2f) * (float)Math.Sin(t * 0.8f) * engine * 0.6f : 0f;
            var amp = rate;
            Rumble01 = 0f; // Continuous trackpad tapping is fatiguing; retain discrete contact/gear events.
            var heaveShake = (rumble * noise + engine * Noise(t + 1.7f, 7.7f, 11.3f, 14.9f) + reverse * Noise(t + 4.4f, 9.4f, 13.3f, 17.1f) + throb + turbulence * slow) * amp;
            var pitchShake = (rumble * 38f * Noise(t + 3.1f, 4.7f, 9.2f, 13.1f) + turbulence * 25f * Noise(t + 5f, 0.13f, 0.29f, 0.41f)) * amp;
            var rollShake = (rumble * 20f * Noise(t + 6.4f, 3.9f, 7.3f, 11.7f) + turbulence * 55f * Noise(t + 9f, 0.11f, 0.23f, 0.37f)) * amp;
            var swayShake = (rumble * 0.6f * Noise(t + 8.2f, 4.1f, 6.8f, 10.3f) + turbulence * 0.8f * Noise(t + 2f, 0.29f, 0.59f, 0.97f)) * amp;

            // Rudder work on the roll and weather-vaning in rough air: a slow yaw wander.
            var yawShake = ((onGround ? 0.35f * speed01 : turbulence * 60f) * Noise(t + 11f, 0.21f, 0.47f, 0.83f)) * amp;

            var sub = Math.Max(1, (int)Math.Ceiling(dt / (1f / 120f)));
            var h = dt / sub;
            for (var i = 0; i < sub; i++) { _heave.Step(h); _pitch.Step(h); _roll.Step(h); _sway.Step(h); }
            if (rate <= 0f)
            { // paused: let everything settle rather than freeze mid-jolt
                var damp = (float)Math.Exp(-6f * dt);
                _heave.X *= damp; _heave.V *= damp; _pitch.X *= damp; _pitch.V *= damp;
                _roll.X *= damp; _roll.V *= damp; _sway.X *= damp; _sway.V *= damp;
            }

            // ---- head and body inertia --------------------------------------------------------
            var forward = Clamp(-_surge * 0.012f, -0.05f, 0.05f);   // pushed back under thrust, forward on brakes
            var up = Clamp(-_heaveG * 0.010f, -0.04f, 0.04f);
            var bank = s.BankLeftDegrees;
            var sway = -(float)Math.Sin(bank * Math.PI / 180.0) * 0.03f;
            // Gaze leads into a turn: eyes and head go to the inside of the bank (left bank -> look left).
            var lookTarget = Clamp(-bank * 0.40f, -9f, 9f);
            _lookYaw += (lookTarget - _lookYaw) * (1f - (float)Math.Exp(-1.6f * dt));
            var surgePitch = Clamp(-_surge * 0.16f, -1.8f, 1.8f);    // braking tips the head forward/down
            var counterRoll = -bank * 0.08f;

            return new Pose(
                Clamp(sway + _sway.X + swayShake, -0.08f, 0.08f),
                Clamp(up + _heave.X + heaveShake, -0.14f, 0.14f),
                forward,
                Clamp(surgePitch + _pitch.X + pitchShake, -4f, 4f),
                Clamp(_lookYaw + yawShake, -10f, 10f),
                Clamp(counterRoll + _roll.X + rollShake, -3f, 3f));
        }

        private static float Noise(float t, float f1, float f2, float f3) =>
            ((float)Math.Sin(t * f1 * 6.2831853f) + 0.7f * (float)Math.Sin(t * f2 * 6.2831853f + 1.3f)
             + 0.5f * (float)Math.Sin(t * f3 * 6.2831853f + 2.9f)) * 0.45f;
        private static float Smooth(float v) { v = Clamp01(v); return v * v * (3f - 2f * v); }
        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
