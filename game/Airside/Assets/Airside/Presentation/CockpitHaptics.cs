using System;

namespace Airside.Presentation
{
    /// <summary>How hard a trackpad tap should feel. The Mac trackpad only offers three feels, so strength is a pattern choice.</summary>
    public enum HapticKind { None = 0, Tick = 1, Medium = 2, Heavy = 3 }

    /// <summary>
    /// Turns cockpit motion events and a continuous rumble level into spaced trackpad taps. Pure and
    /// clock-free (the caller supplies real frame time) so it is unit tested without Unity or a trackpad.
    /// Presentation only: never feeds back into the simulation.
    /// </summary>
    public sealed class CockpitHapticScheduler
    {
        /// <summary>The actuator smears taps closer together than this, so they are spaced out.</summary>
        public const float MinGapSeconds = 0.06f;
        /// <summary>A hard landing thumps twice: the strike, then the airframe settling.</summary>
        public const float EchoDelaySeconds = 0.09f;
        public const float RumbleFloor = 0.12f;
        public const float RumbleMinHz = 2f;
        public const float RumbleMaxHz = 10f;

        private HapticKind _pending;
        private float _sinceTap = 1f;
        private float _rumblePhase;
        private float _echoIn = -1f;

        /// <summary>Queue an event tap. The strongest one waiting wins; weaker ones are dropped, not stacked.</summary>
        public void Post(HapticKind kind)
        {
            if (kind > _pending) _pending = kind;
        }

        public void Clear()
        {
            _pending = HapticKind.None;
            _sinceTap = 1f;
            _rumblePhase = 0f;
            _echoIn = -1f;
        }

        /// <summary>Advances by real seconds and returns the tap to play this frame, if any.</summary>
        public HapticKind Step(float deltaSeconds, float rumble01, bool enabled)
        {
            var dt = Math.Min(Math.Max(deltaSeconds, 0f), 0.1f);
            if (!enabled)
            {
                Clear();
                return HapticKind.None;
            }

            _sinceTap += dt;
            if (_echoIn >= 0f)
            {
                _echoIn -= dt;
                if (_echoIn < 0f) Post(HapticKind.Medium);
            }

            if (_pending != HapticKind.None)
            {
                if (_sinceTap < MinGapSeconds) return HapticKind.None;
                var tap = _pending;
                _pending = HapticKind.None;
                _sinceTap = 0f;
                if (tap == HapticKind.Heavy) _echoIn = EchoDelaySeconds;
                return tap;
            }

            var level = Math.Min(Math.Max(rumble01, 0f), 1f);
            if (level <= RumbleFloor)
            {
                _rumblePhase = 0f;
                return HapticKind.None;
            }

            var hz = RumbleMinHz + (RumbleMaxHz - RumbleMinHz) * (level - RumbleFloor) / (1f - RumbleFloor);
            _rumblePhase += dt * hz;
            if (_rumblePhase < 1f) return HapticKind.None;
            _rumblePhase -= (float)Math.Floor(_rumblePhase);
            if (_sinceTap < MinGapSeconds) return HapticKind.None;
            _sinceTap = 0f;
            return HapticKind.Tick;
        }
    }
}
