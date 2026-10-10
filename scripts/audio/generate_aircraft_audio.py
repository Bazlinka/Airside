#!/usr/bin/env python3
"""ADR 0192: derive fleet layers from the registered, immutable CC0 family beds.

Needs Python + numpy. No network; --check compares every byte and never writes.
The tyre source was decoded from the retained HQ preview using macOS afconvert.
Profile frequencies are sound-design values, not engine specification claims.
"""
import argparse
import hashlib
import io
import json
from pathlib import Path
import wave

import numpy as np

ROOT = Path(__file__).resolve().parents[2]
AUDIO = ROOT / 'game/Airside/Assets/Resources/Airside/Audio'
RATE = 22050
_PROFILE_DATA = json.loads((ROOT / 'docs/data/AIRCRAFT_AUDIO_PROFILES.json').read_text())
PROFILES = _PROFILE_DATA['profiles']
# ADR 0207: helicopter profiles are listed beside the fleet but their loops are synthesised by generate_rotorcraft_audio.py.
ROTORCRAFT_PROFILES = _PROFILE_DATA.get('rotorcraft', [])
SOURCES = {'twin': 'eng_dash8_300_twin.wav', 'q400': 'eng_dash8_q400_pw100.wav',
           'jet': 'eng_jet_turbine.wav'}


def read(path):
    with wave.open(str(path)) as f:
        assert f.getframerate() == RATE and f.getsampwidth() == 2
        x = np.frombuffer(f.readframes(f.getnframes()), dtype='<i2').astype(np.float64) / 32768
        return x.reshape(-1, f.getnchannels()).mean(axis=1)


def wav(x):
    stream = io.BytesIO()
    with wave.open(stream, 'wb') as f:
        f.setparams((1, 2, RATE, len(x), 'NONE', 'not compressed'))
        f.writeframes(np.rint(np.clip(x, -0.95, 0.95) * 32767).astype('<i2').tobytes())
    return stream.getvalue()


def level(x, db=-18):
    x = x - x.mean()
    x *= 10 ** (db / 20) / max(1e-9, np.sqrt(np.mean(x * x)))
    x *= min(1, 0.82 / max(1e-9, np.max(np.abs(x))))
    return x


def filter_band(x, lo, hi):
    """Circular FFT filter: leaves the periodic waveform and its seam intact."""
    freqs = np.fft.rfftfreq(len(x), 1 / RATE)
    gain = 1 / np.sqrt(1 + (freqs / hi) ** 6)
    gain *= 1 - 1 / np.sqrt(1 + (freqs / lo) ** 4)
    return np.fft.irfft(np.fft.rfft(x) * gain, n=len(x))


def suppress_whine(x, n=2048, hop=512, window=63, ceiling=1.6, floor_hz=500):
    """Clip narrowband peaks to the local noise floor, per STFT frame.

    The recorded jet bed carries a gliding ~2 kHz turbine whine, 20+ dB over its own noise;
    looped every 8 s it reads as an engine repeatedly spooling up. Phase is kept, so the
    broadband rumble and hiss are untouched.
    """
    win = np.hanning(n + 1)[:-1]
    pad = np.pad(x, (n, n + hop))
    out = np.zeros_like(pad)
    norm = np.zeros_like(pad)
    start = int(floor_hz / (RATE / n))
    half = window // 2
    for i in range(0, len(pad) - n, hop):
        spec = np.fft.rfft(pad[i:i + n] * win)
        mag = np.abs(spec)
        padded = np.pad(mag, half, mode='edge')
        floor = np.median(np.lib.stride_tricks.sliding_window_view(padded, window), axis=1)
        gain = np.ones_like(mag)
        gain[start:] = np.minimum(1, ceiling * floor[start:] / np.maximum(mag[start:], 1e-12))
        out[i:i + n] += np.fft.irfft(spec * gain, n=n) * win
        norm[i:i + n] += win * win
    return (out / np.maximum(norm, 1e-9))[n:n + len(x)]


def flatten_surges(x, n=2048, hop=512, smooth_s=0.6, max_db=12):
    """Hold each frequency band at its long-term level, removing slow swells.

    The jet bed's 150-2000 Hz rumble swings ~8 dB over a few seconds; on an 8 s loop
    that is a repeating spool-up. Only changes slower than smooth_s are flattened, so
    fast turbulence stays.
    """
    win = np.hanning(n + 1)[:-1]
    pad = np.pad(x, (n, n + hop))
    starts = range(0, len(pad) - n, hop)
    specs = np.array([np.fft.rfft(pad[i:i + n] * win) for i in starts])
    freqs = np.fft.rfftfreq(n, 1 / RATE)
    edges = [0, 150, 300, 600, 1000, 2000, 4000, RATE / 2 + 1]
    k = max(1, int(smooth_s * RATE / hop))
    kernel = np.ones(k) / k
    lim = 10 ** (max_db / 20)
    for lo, hi in zip(edges[:-1], edges[1:]):
        band = (freqs >= lo) & (freqs < hi)
        energy = np.sqrt(np.mean(np.abs(specs[:, band]) ** 2, axis=1))
        padded = np.pad(energy, (k, k), mode='reflect')
        local = np.convolve(padded, kernel, mode='same')[k:-k]
        gain = np.clip(np.mean(energy) / np.maximum(local, 1e-9), 1 / lim, lim)
        specs[:, band] *= gain[:, None]
    out = np.zeros_like(pad)
    norm = np.zeros_like(pad)
    for row, i in zip(specs, starts):
        out[i:i + n] += np.fft.irfft(row, n=n) * win
        norm[i:i + n] += win * win
    return (out / np.maximum(norm, 1e-9))[n:n + len(x)]


def loop(x, seconds=8):
    count, fade = int(seconds * RATE), int(0.65 * RATE)
    x = np.resize(x, count + fade).copy()
    t = np.arange(fade) / fade
    x[:fade] = x[:fade] * np.sin(t * np.pi / 2) + x[count:] * np.cos(t * np.pi / 2)
    return x[:count]


def periodic_noise(count, seed):
    # Integer RNG and explicit conversion are stable across numpy versions/platforms.
    state = seed
    values = np.empty(count, dtype=np.float64)
    for i in range(count):
        state = (1664525 * state + 1013904223) & 0xffffffff
        values[i] = state / 2147483648 - 1
    return values


def outputs():
    bases = {key: read(AUDIO / name) for key, name in SOURCES.items()}
    bases['jet'] = flatten_surges(suppress_whine(bases['jet']))
    result = {}
    count = RATE * 8
    t = np.arange(count) / RATE
    for p in PROFILES:
        base = loop(bases[p['family']])
        body = p['body_hz']
        # Multiples of 1/8 Hz give all tones an exact period at the loop boundary.
        harmonics = (np.sin(2 * np.pi * body * t)
                     + 0.30 * np.sin(2 * np.pi * body * 2 * t)
                     + 0.14 * np.sin(2 * np.pi * body * 3 * t))
        harmonics *= 0.012 if p['family'] == 'jet' else 0.022
        idle = level(filter_band(base, 38, p['brightness_hz'] * 0.43) + harmonics, -20)
        loaded = level(filter_band(base, 35, p['brightness_hz'] * 2) + harmonics * 0.8)
        # Decorrelate reverse from the forward bed; turbulence becomes broad and rough,
        # without reversing a recording or pretending it is a recorded reverser event.
        noise = filter_band(periodic_noise(count, sum(map(ord, p['id']))), 70, 4200)
        flutter = 1 + 0.12 * np.sin(2 * np.pi * 19 * t) + 0.07 * np.sin(2 * np.pi * 31 * t)
        reverse = level((filter_band(np.roll(base, 23711), 85, 3100) * 0.60
                         + noise * 0.36) * flutter + harmonics * 0.4)
        for role, data in [('idle', idle), ('power', loaded), ('reverse', reverse)]:
            result[f"eng_{p['id'].lower()}_{role}_v01.wav"] = (data, True)

    # Rubber/tarmac broadband roll with tiny irregular axle pulses; no tonal motor.
    noise = periodic_noise(count, 191)
    roll = filter_band(noise, 45, 1800)
    roll *= 1 + 0.06 * np.sin(2 * np.pi * 13 * t) + 0.04 * np.sin(2 * np.pi * 7.375 * t)
    result['aircraft_tyre_roll_v01.wav'] = (level(roll, -22), True)

    tyre = read(ROOT / 'docs/data/audio/src_aircraft_tyre_skid_479498.wav')
    tyre = tyre[int(0.16 * RATE):int(0.64 * RATE)]
    tyre = filter_band(tyre, 160, 6400)
    tyre = level(tyre, -18)
    tyre[:220] *= np.linspace(0, 1, 220)
    tyre[-1100:] *= np.linspace(1, 0, 1100)
    # A short oleo/gear thump and staggered left/right wheel spin-up, not a single beep.
    touch = np.zeros(int(0.95 * RATE))
    touch[:len(tyre)] += tyre * 0.75
    offset = int(0.075 * RATE)
    touch[offset:offset + len(tyre)] += tyre * 0.45
    tt = np.arange(len(touch)) / RATE
    touch += np.sin(2 * np.pi * 68 * tt) * np.exp(-tt * 13) * 0.10
    touch[:220] *= np.linspace(0, 1, 220)
    touch[-1100:] *= np.linspace(1, 0, 1100)
    result['aircraft_touchdown_v01.wav'] = (touch, False)
    return result


def generated_profiles():
    lines = ['// GENERATED by scripts/audio/generate_aircraft_audio.py; tuning is in docs/data/AIRCRAFT_AUDIO_PROFILES.json.',
             'using Airside.Domain;', '', 'namespace Airside.Presentation', '{',
             '    public static class AircraftAudioProfiles', '    {',
             '        public static AircraftAudioProfile For(AircraftType type)', '        {',
             '            return (type?.Id ?? "ATR42") switch', '            {']
    for p in PROFILES:
        lines.append(f'                "{p["id"]}" => new("{p["id"].lower()}", '
                     f'{p["pitch"]:.3f}f, {p["gain"]:.3f}f),')
    for p in ROTORCRAFT_PROFILES:
        lines.append(f'                "{p["id"]}" => new("{p["id"].lower()}", '
                     f'{p["pitch"]:.3f}f, {p["gain"]:.3f}f),')
    lines += ['                _ => new("atr42", 1f, 0.86f)', '            };',
              '        }', '    }', '}', '']
    return '\n'.join(lines).encode()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--check', action='store_true')
    args = ap.parse_args()
    failed = []
    records = []
    for name, (x, looping) in outputs().items():
        data = wav(x)
        path = AUDIO / name
        if args.check:
            if not path.exists() or path.read_bytes() != data:
                failed.append(name)
        else:
            path.write_bytes(data)
        quantized = np.rint(x * 32767) / 32768
        seam = abs(quantized[0] - quantized[-1])
        step = np.mean(np.abs(np.diff(quantized)))
        assert not looping or seam < max(0.006, step * 3), (name, seam, step)
        assert np.max(abs(x)) < 0.83 and np.all(np.isfinite(x))
        records.append(dict(file=name, sha256=hashlib.sha256(data).hexdigest(),
                            seconds=round(len(x) / RATE, 3),
                            rms_db=round(float(20 * np.log10(np.sqrt(np.mean(x * x)))), 2),
                            peak=round(float(np.max(abs(x))), 5),
                            seam=round(float(seam), 6), looping=looping))
    derived = ROOT / 'game/Airside/Assets/Airside/Presentation/AircraftAudioProfiles.Generated.cs'
    manifest = ROOT / 'docs/data/audio/aircraft_audio_manifest_v01.json'
    for path, data in [(derived, generated_profiles()),
                       (manifest, (json.dumps(records, indent=2) + '\n').encode())]:
        if args.check:
            if not path.exists() or path.read_bytes() != data:
                failed.append(str(path.relative_to(ROOT)))
        else:
            path.write_bytes(data)
    if failed:
        raise SystemExit('Stale aircraft audio: ' + ', '.join(failed))
    print(f'{len(records)} aircraft AudioClips + {len(PROFILES)} profiles: byte check passed' if args.check
          else f'Generated {len(records)} aircraft AudioClips + {len(PROFILES)} profiles; seam/level checks passed')


if __name__ == '__main__':
    main()
