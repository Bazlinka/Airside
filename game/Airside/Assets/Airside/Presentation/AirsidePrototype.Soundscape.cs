using Airside.Domain;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// ADR 0136 — the working airport under the weather: an apron bed (quieter at night, off while
    /// muted), the terminal PA chime every few minutes in open hours, and a short dip of the ambience
    /// under the celebration fanfares so they read. Presentation only; the sounds come from
    /// <see cref="HudSounds"/>.
    /// </summary>
    public sealed partial class AirsidePrototype
    {
        private const float ApronBedVolume = 0.035f;
        private const float PaChimeVolume = 0.22f;
        private const float PaChimeMinSeconds = 240f;
        private const float PaChimeSpreadSeconds = 240f;

        private AudioSource _apronAudio;
        private AudioSource _paAudio;
        private AudioClip _paChimeClip;
        private AudioClip _whooshClip;
        private float _nextPaChimeAt = 90f;
        private float _duckUntil;

        /// <summary>0..1: how far the ambience is pulled down right now (1 = not at all).</summary>
        private float AmbientDuck => Time.unscaledTime < _duckUntil ? 0.5f : 1f;

        /// <summary>Pull the ambience down for <paramref name="seconds"/> so a fanfare reads over it.</summary>
        private void DuckAmbience(float seconds) => _duckUntil = Mathf.Max(_duckUntil, Time.unscaledTime + seconds);

        private void UpdateSoundscape()
        {
            if (_apronAudio == null)
            {
                _apronAudio = gameObject.AddComponent<AudioSource>();
                _apronAudio.playOnAwake = false;
                _apronAudio.loop = true;
                _apronAudio.spatialBlend = 0f;
                _apronAudio.volume = 0f;
                var samples = HudSounds.ApronBed();
                var clip = AudioClip.Create("Apron bed", samples.Length, 1, HudSounds.SampleRate, false);
                clip.SetData(samples, 0);
                _apronAudio.clip = clip;
                _paAudio = gameObject.AddComponent<AudioSource>();
                _paAudio.playOnAwake = false;
                _paAudio.spatialBlend = 0f;
            }

            var open = _operations == null || !AirportCurfew.IsClosed(_clock.Now, _operations.Clock);
            var target = _audioMuted || AirsideFocusMode.BareWorld ? 0f : ApronBedVolume * (open ? 1f : 0.35f) * AmbientDuck;
            _apronAudio.volume = Mathf.MoveTowards(_apronAudio.volume, target, Time.unscaledDeltaTime * 0.05f);
            if (target > 0f && !_apronAudio.isPlaying && CanStartAudio(_apronAudio))
                _apronAudio.Play();

            // The terminal chime: every four to eight minutes while the airport is open.
            var now = Time.unscaledTime;
            if (now >= _nextPaChimeAt)
            {
                _nextPaChimeAt = now + PaChimeMinSeconds + Mathf.PerlinNoise(now * 0.01f, 3.3f) * PaChimeSpreadSeconds;
                if (open && !_audioMuted && !AirsideFocusMode.BareWorld && _paAudio != null)
                {
                    if (_paChimeClip == null)
                    {
                        var samples = HudSounds.PaChime();
                        _paChimeClip = AudioClip.Create("PA chime", samples.Length, 1, HudSounds.SampleRate, false);
                        _paChimeClip.SetData(samples, 0);
                    }

                    _paAudio.PlayOneShot(_paChimeClip, PaChimeVolume);
                }
            }
        }

        /// <summary>A soft whoosh as a workspace sheet slides in.</summary>
        private void PlayPanelWhoosh() => PlayMoment(ref _whooshClip, HudSounds.PanelWhoosh, "Panel whoosh", 0.45f);
    }
}
