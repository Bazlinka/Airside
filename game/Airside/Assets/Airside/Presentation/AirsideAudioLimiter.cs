using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>Final listener bus ceiling, including aircraft, weather, PA and overlapping UI cues.</summary>
    [RequireComponent(typeof(AudioListener))]
    [DisallowMultipleComponent]
    public sealed class AirsideAudioLimiter : MonoBehaviour
    {
        private AudioPeakLimiter _limiter;
        private void OnEnable()
        {
            Configure(false);
            AudioSettings.OnAudioConfigurationChanged += Configure;
        }
        private void OnDisable() => AudioSettings.OnAudioConfigurationChanged -= Configure;
        private void Configure(bool deviceChanged) => _limiter = new AudioPeakLimiter(AudioSettings.outputSampleRate);
        private void OnAudioFilterRead(float[] data, int channels) => _limiter?.Process(data, channels);
    }
}
