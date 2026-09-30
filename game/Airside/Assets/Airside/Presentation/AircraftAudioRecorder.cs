using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>No allocation or Unity API calls on the DSP thread; writing happens after capture stops.</summary>
    public sealed class AircraftAudioRecorder : MonoBehaviour
    {
        private readonly object _gate = new();
        private float[] _samples;
        private int _count;
        private int _channels;
        private int _rate;
        private volatile bool _recording;
        private volatile bool _deviceChanged;

        private void OnEnable() => AudioSettings.OnAudioConfigurationChanged += DeviceChanged;
        private void OnDisable() => AudioSettings.OnAudioConfigurationChanged -= DeviceChanged;
        private void DeviceChanged(bool changed)
        {
            if (_recording) _deviceChanged = true;
        }

        public void Begin(float seconds)
        {
            _recording = false;
            lock (_gate)
            {
                _rate = AudioSettings.outputSampleRate;
                _samples = new float[(int)(seconds * _rate) * 8];
                _count = 0;
                _deviceChanged = false;
                _channels = 0;
                _recording = true;
            }
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (!_recording) return;
            lock (_gate)
            {
                if (!_recording) return;
                _channels = channels;
                var count = Math.Min(data.Length, _samples.Length - _count);
                Array.Copy(data, 0, _samples, _count, count);
                _count += count;
            }
        }

        public bool Save(string path)
        {
            _recording = false;
            lock (_gate)
            {
                if (_deviceChanged)
                {
                    Debug.LogError("[Aircraft audio] rejected capture: audio device changed during " + path);
                    return false;
                }
                if (_count == 0 || _channels == 0)
                {
                    Debug.LogError("[Aircraft audio] no listener samples captured: " + path);
                    return false;
                }
                using var writer = new BinaryWriter(File.Create(path));
                var bytes = _count * 2;
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + bytes);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)_channels); writer.Write(_rate);
                writer.Write(_rate * _channels * 2); writer.Write((short)(_channels * 2)); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(bytes);
                float peak = 0f;
                for (var i = 0; i < _count; i++)
                {
                    peak = Mathf.Max(peak, Mathf.Abs(_samples[i]));
                    writer.Write((short)(Mathf.Clamp(_samples[i], -1f, 1f) * 32767f));
                }
                Debug.Log("[Aircraft audio] wrote " + path + " peak=" + peak.ToString("F4", CultureInfo.InvariantCulture));
                return true;
            }
        }
    }
}
