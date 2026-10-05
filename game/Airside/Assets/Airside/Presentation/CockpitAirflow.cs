using System;

namespace Airside.Presentation
{
    /// <summary>Original filtered-noise airflow bed; a speed proxy, not a recorded aircraft voice.</summary>
    public static class CockpitAirflow
    {
        public const int SampleRate = 22050;
        public const int LoopSeconds = 4;

        public static float Volume(float groundKnots)
        {
            var speed = Math.Max(0f, Math.Min(1f, groundKnots / 300f));
            return 0.012f + 0.108f * speed * speed;
        }

        public static float[] Samples()
        {
            var samples = new float[SampleRate * LoopSeconds];
            uint noise = 0x6a09e667;
            for (var i = 0; i < samples.Length; i++)
            {
                noise ^= noise << 13; noise ^= noise >> 17; noise ^= noise << 5;
                samples[i] = (noise & 0xffffff) / 8388607.5f - 1f;
            }
            // Warm the filter with one whole period, then render the same period. The state
            // at the wrap is stationary, rather than a new attack every four seconds.
            var low = 0f;
            for (var i = 0; i < samples.Length; i++) low += (samples[i] - low) * 0.08f;
            var peak = 0f;
            for (var i = 0; i < samples.Length; i++)
            {
                low += (samples[i] - low) * 0.08f;
                samples[i] = low;
                peak = Math.Max(peak, Math.Abs(low));
            }
            for (var i = 0; i < samples.Length; i++) samples[i] *= 0.55f / Math.Max(peak, 0.001f);
            return samples;
        }
    }
}
