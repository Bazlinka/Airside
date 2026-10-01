#!/usr/bin/env python3
"""Fill digital silence in coast_wave_01.wav with a quiet sea-hiss floor.

process_beds.py overlaps six copies of a 4 s wave; their silent tails line up twice per 16 s loop,
leaving ~265 ms of exact zeros (-100 dBFS) in the bed. It reads as the sea cutting out. The gap is
filled with noise matched to the 1 s of bed just before it and crossfaded in/out. Idempotent.
Needs numpy. Usage: scripts/audio/fill_coast_gaps.py [path]
"""
import sys
import wave
from pathlib import Path

import numpy as np

ROOT = Path(__file__).resolve().parents[2]
DEFAULT = ROOT / 'game/Airside/Assets/Resources/Airside/Audio/coast_wave_01.wav'
MIN_GAP_S, FADE_S = 0.05, 0.04


def main():
    path = Path(sys.argv[1]) if len(sys.argv) > 1 else DEFAULT
    with wave.open(str(path)) as f:
        rate, ch = f.getframerate(), f.getnchannels()
        x = np.frombuffer(f.readframes(f.getnframes()), '<i2').astype(np.float64).reshape(-1, ch)
    quiet = np.flatnonzero(np.abs(x).max(axis=1) < 3)
    runs = [r for r in np.split(quiet, np.flatnonzero(np.diff(quiet) > 1) + 1)
            if len(r) > MIN_GAP_S * rate]
    rng = np.random.default_rng(136)
    fade = int(FADE_S * rate)
    for run in runs:
        a, b = run[0], run[-1] + 1
        ref = x[max(0, a - rate):a]
        level = max(np.sqrt(np.mean(ref ** 2)), 30.0)
        # Low-passed noise: sea hiss, not a click or a tone.
        noise = rng.standard_normal((b - a + 2 * fade, ch))
        noise = np.apply_along_axis(lambda v: np.convolve(v, np.ones(6) / 6, 'same'), 0, noise)
        noise *= level / np.sqrt(np.mean(noise ** 2))
        env = np.ones(len(noise))
        env[:2 * fade] = np.linspace(0, 1, 2 * fade) ** 2
        env[-2 * fade:] = np.linspace(1, 0, 2 * fade) ** 2
        lo, hi = max(0, a - fade), min(len(x), b + fade)
        x[lo:hi] += (noise * env[:, None])[:hi - lo]
    with wave.open(str(path), 'wb') as f:
        f.setparams((ch, 2, rate, len(x), 'NONE', 'not compressed'))
        f.writeframes(np.rint(np.clip(x, -32768, 32767)).astype('<i2').tobytes())
    print(f'{path.name}: filled {len(runs)} silent gap(s)')


if __name__ == '__main__':
    main()
