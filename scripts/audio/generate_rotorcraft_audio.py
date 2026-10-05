#!/usr/bin/env python3
"""ADR 0207: synthesise the Bell 412 rotor and turbine loops (project-authored, no samples).

Needs Python + numpy. No network; --check compares every byte and never writes.
Every component is periodic in the 8 s loop, so there is no seam and no loop-boundary click:
main rotor 5.5 Hz (a 4-blade rotor passes a blade 22 times a second), tail rotor 26.875 Hz (2 blades,
53.75 Hz), turbine whine on exact multiples of 1/8 Hz. Frequencies are sound-design values for a
medium twin helicopter, not engine or rotor specification claims.
"""
import argparse
import hashlib
import json
from pathlib import Path
import sys

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
from generate_aircraft_audio import AUDIO, RATE, ROOT, filter_band, level, periodic_noise, wav  # noqa: E402

SECONDS = 8
COUNT = RATE * SECONDS
MAIN_HZ = 5.5            # rev/s: 44 revs in the loop
BLADES = 4
TAIL_HZ = 26.875         # rev/s: 215 revs in the loop
TURBINE_HZ = (1150.0, 2300.0, 3450.0)


def pulse_train(rate_hz, amplitude, tone_hz, noise_seed, decay_s, spread=0.18, rev_hz=MAIN_HZ):
    """A circular train of blade-slap bursts: a low thump plus a short broadband crack.

    Placement wraps round the loop so the last burst's tail runs into the first sample.
    `amplitude` varies burst to burst (blades are never identical) and by a once-per-revolution swell.
    """
    out = np.zeros(COUNT)
    n = int(rate_hz * SECONDS)
    tail = int(decay_s * 9 * RATE)
    t = np.arange(tail) / RATE
    crack_noise = periodic_noise(COUNT, noise_seed)
    rng_state = noise_seed
    for k in range(n):
        rng_state = (1664525 * rng_state + 1013904223) & 0xffffffff
        jitter = 1 + spread * (rng_state / 2147483648 - 1)
        when = k / rate_hz
        swell = 1 + 0.28 * np.sin(2 * np.pi * rev_hz * when)
        start = int(round(when * RATE))
        thump = np.exp(-t / decay_s) * np.sin(2 * np.pi * tone_hz * t)
        idx = (start + np.arange(tail)) % COUNT
        crack = crack_noise[idx] * np.exp(-t / (decay_s * 0.28))
        out[idx] += amplitude * jitter * swell * (thump + 0.55 * crack)
    return out


def turbine(brightness):
    t = np.arange(COUNT) / RATE
    tones = sum(w * np.sin(2 * np.pi * f * t + p) for f, w, p in zip(TURBINE_HZ, (1.0, 0.45, 0.2), (0.0, 1.3, 2.1)))
    hiss = filter_band(periodic_noise(COUNT, 977), 700, brightness)
    return tones * 0.014 + hiss * 0.30


def rumble():
    return filter_band(periodic_noise(COUNT, 313), 22, 180)


def tail_rotor(amplitude):
    t = np.arange(COUNT) / RATE
    phase = 2 * np.pi * TAIL_HZ * t
    buzz = np.sin(phase * 2) + 0.55 * np.sin(phase * 4 + 0.7) + 0.3 * np.sin(phase * 6 + 1.9)
    buzz *= 0.55 + 0.45 * np.abs(np.sin(phase))
    return filter_band(buzz, 260, 2600) * amplitude


def outputs():
    t = np.arange(COUNT) / RATE
    # Idle on the pad: turbine whine and a soft, slow thump from the rotor at flat pitch.
    idle_pulse = pulse_train(MAIN_HZ * BLADES, 0.30, 58.0, 41, 0.024)
    idle = turbine(5200) * 1.0 + idle_pulse * 0.9 + rumble() * 0.5 + tail_rotor(0.04)
    idle = level(idle, -20)
    # Under load: hard blade slap, loud turbine and a tail-rotor buzz — the sound heard at a hover or in the climb.
    power_pulse = pulse_train(MAIN_HZ * BLADES, 1.0, 74.0, 59, 0.020, spread=0.24)
    am = 0.62 + 0.38 * np.abs(np.sin(np.pi * MAIN_HZ * BLADES * t)) ** 2
    broad = filter_band(periodic_noise(COUNT, 1301), 90, 3600) * am
    loaded = turbine(7400) * 0.7 + power_pulse * 1.0 + broad * 0.22 + rumble() * 0.35 + tail_rotor(0.14)
    loaded = level(loaded, -17)
    return {'eng_b412_idle_v01.wav': (idle, True), 'eng_b412_power_v01.wav': (loaded, True)}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--check', action='store_true')
    args = ap.parse_args()
    failed, records = [], []
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
        spectrum = np.abs(np.fft.rfft(x))
        freqs = np.fft.rfftfreq(len(x), 1 / RATE)
        records.append(dict(file=name, sha256=hashlib.sha256(data).hexdigest(), seconds=round(len(x) / RATE, 3),
                            rms_db=round(float(20 * np.log10(np.sqrt(np.mean(x * x)))), 2),
                            peak=round(float(np.max(abs(x))), 5), seam=round(float(seam), 6), looping=looping,
                            blade_pass_hz=MAIN_HZ * BLADES,
                            blade_pass_line=round(float(spectrum[np.argmin(abs(freqs - MAIN_HZ * BLADES))]
                                                        / np.median(spectrum[(freqs > 15) & (freqs < 30)])), 2)))
    manifest = ROOT / 'docs/data/audio/rotorcraft_audio_manifest_v01.json'
    body = (json.dumps(records, indent=2) + '\n').encode()
    if args.check:
        if not manifest.exists() or manifest.read_bytes() != body:
            failed.append(str(manifest.relative_to(ROOT)))
    else:
        manifest.write_bytes(body)
    if failed:
        raise SystemExit('Stale rotorcraft audio: ' + ', '.join(failed))
    print('rotorcraft audio byte check passed' if args.check else 'Generated rotorcraft audio; seam/level checks passed')


if __name__ == '__main__':
    main()
