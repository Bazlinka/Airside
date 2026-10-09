using Airside.Domain;
using Airside.Simulation;
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
        private const float ApronBedVolume = 0.18f;
        private const float PaChimeVolume = 0.27f;
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
                var origin = AirportSoundOrigin();
                _apronAudio = AirportSource("Working apron", origin, true, 2400f, 160);
                _apronAudio.clip = Resources.Load<AudioClip>("Airside/Audio/airport_apron_v02");
                if (_apronAudio.clip == null)
                {
                    var samples = HudSounds.ApronBed();
                    var clip = AudioClip.Create("Apron fallback", samples.Length, 1, HudSounds.SampleRate, false);
                    clip.SetData(samples, 0);
                    _apronAudio.clip = clip;
                }
                _paAudio = AirportSource("Terminal loudspeaker", origin + Vector3.up * 4f, false, 800f, 120);
                var echo = _paAudio.gameObject.AddComponent<AudioEchoFilter>();
                echo.delay = 82f; echo.decayRatio = 0.18f; echo.wetMix = 0.16f; echo.dryMix = 1f;
            }

            var open = _operations == null || !AirportCurfew.IsClosed(_clock.Now, _operations.Clock);
            var localAirport = AirportPresentationVisible && !InCockpit && !WatchingOutstation;
            var target = _audioMuted || !localAirport || !AirsideFocusMode.ShowTerminal ? 0f
                : ApronBedVolume * (open ? 1f : 0.28f) * AmbientDuck * ExteriorWeatherGain;
            _apronAudio.volume = Mathf.MoveTowards(_apronAudio.volume, target, Time.unscaledDeltaTime * 0.05f);
            if (target > 0f && !_apronAudio.isPlaying && CanStartAudio(_apronAudio))
                _apronAudio.Play();

            // The cockpit cannot hear a non-spatial terminal loudspeaker or apron bed.
            if (!localAirport || _audioMuted)
            {
                _apronAudio.Stop();
                _apronAudio.volume = 0f;
                if (_paAudio.isPlaying) _paAudio.Stop();
            }
            UpdateCockpitAirflow();

            // The terminal chime: every four to eight minutes while the airport is open.
            var now = Time.unscaledTime;
            if (now >= _nextPaChimeAt)
            {
                _nextPaChimeAt = now + PaChimeMinSeconds + Mathf.PerlinNoise(now * 0.01f, 3.3f) * PaChimeSpreadSeconds;
                if (open && !_audioMuted && localAirport && AirsideFocusMode.ShowTerminal && _paAudio != null)
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

        private Vector3 AirportSoundOrigin()
        {
            if (!AirsideBareField.Enabled)
                return _terminalProbe != null ? _terminalProbe.transform.position : new Vector3(26f, 3.2f, 27f);
            // The release scene is at published metres; the legacy reflection probe is miniature-scale.
            var gates = AdelaideLayout.TerminalGates;
            var x = 0f; var z = 0f;
            foreach (var gate in gates) { x += gate.NoseX; z += gate.NoseZ; }
            return gates.Length > 0 ? new Vector3(x / gates.Length, AirsideAdelaideGround.PavementWorldY + 2f,
                z / gates.Length + 20f) : new Vector3(1300f, AirsideFlightPath.GroundY + 2f, 436f);
        }

        private AudioSource AirportSource(string name, Vector3 position, bool loop, float range, int priority)
        {
            var host = new GameObject(name);
            host.transform.SetParent(transform, false);
            host.transform.position = position;
            var source = host.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = loop; source.spatialBlend = 1f;
            source.volume = 0f; source.dopplerLevel = 0f; source.priority = priority;
            source.minDistance = 80f; source.maxDistance = range;
            source.rolloffMode = AudioRolloffMode.Linear;
            // Nonloop PA receives its gain through PlayOneShot; source volume must not remain zero.
            if (!loop) source.volume = 1f;
            var filter = host.AddComponent<AudioLowPassFilter>();
            filter.cutoffFrequency = loop ? 2200f : 3600f;
            return source;
        }

        /// <summary>A soft whoosh as a workspace sheet slides in.</summary>
        private void PlayPanelWhoosh() => PlayMoment(ref _whooshClip, HudSounds.PanelWhoosh, "Panel whoosh", 0.45f);
    }
}
