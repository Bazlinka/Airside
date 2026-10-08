using System;

namespace Airside.Presentation
{
    /// <summary>Stereo-linked final headroom protection. Audio-thread only, no allocation or Unity APIs.</summary>
    public sealed class AudioPeakLimiter
    {
        public const float Ceiling = 0.90f;
        private readonly double _release;
        private float _gain = 1f;

        public AudioPeakLimiter(int sampleRate) => _release = Math.Exp(-1.0 / (Math.Max(8000, sampleRate) * 0.09));

        public void Process(float[] samples, int channels)
        {
            if (samples == null || channels < 1) return;
            for (var frame = 0; frame + channels <= samples.Length; frame += channels)
            {
                var peak = 0f;
                for (var channel = 0; channel < channels; channel++)
                {
                    var value = samples[frame + channel];
                    if (float.IsNaN(value) || float.IsInfinity(value)) value = samples[frame + channel] = 0f;
                    peak = Math.Max(peak, Math.Abs(value));
                }
                var target = peak > Ceiling ? Ceiling / peak : 1f;
                // Instant linked attack guarantees headroom; 90 ms release avoids pumping/crackling.
                _gain = target < _gain ? target : (float)(target + (_gain - target) * _release);
                for (var channel = 0; channel < channels; channel++) samples[frame + channel] *= _gain;
            }
        }
    }
}
