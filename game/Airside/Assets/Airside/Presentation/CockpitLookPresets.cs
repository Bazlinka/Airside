namespace Airside.Presentation
{
    /// <summary>Glance targets for the cockpit camera: forward, left window (wing and engine),
    /// instrument panel, right window, overhead (keys 1-5). Degrees of head yaw (right positive) and pitch
    /// (down positive), inside the camera's look limits. Independent of Unity.</summary>
    public static class CockpitLookPresets
    {
        public const float MaxYaw = 95f, MinPitch = -35f, MaxPitch = 45f;
        public static readonly (string Name, float Yaw, float Pitch)[] All =
        {
            ("Forward", 0f, 0f),
            ("Left window", -70f, 0f),
            ("Instrument panel", 0f, 32f),
            ("Right window", 70f, 0f),
            ("Overhead", 0f, -30f),
        };

        /// <summary>Moves toward a target without overshoot; <paramref name="rate"/> is 1/seconds.</summary>
        public static float Ease(float current, float target, float rate, float deltaSeconds)
        {
            var k = 1f - (float)System.Math.Exp(-rate * System.Math.Max(0f, deltaSeconds));
            return current + (target - current) * k;
        }
    }
}
