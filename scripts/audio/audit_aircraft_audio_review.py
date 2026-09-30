#!/usr/bin/env python3
"""Measure actual packaged-listener captures; never normalise the review recordings.

Usage: python3 scripts/audio/audit_aircraft_audio_review.py PATH [--single ID]
"""
import argparse
import hashlib
import json
from pathlib import Path
import wave
import numpy as np


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('directory', type=Path)
    ap.add_argument('--single')
    args = ap.parse_args()
    root = Path(__file__).resolve().parents[2]
    profiles = json.loads((root / 'docs/data/AIRCRAFT_AUDIO_PROFILES.json').read_text())['profiles']
    ids = [args.single] if args.single else [p['id'] for p in profiles]
    report = []
    for ident in ids:
        path = args.directory / (ident + '.wav')
        with wave.open(str(path)) as f:
            rate = f.getframerate()
            channels = f.getnchannels()
            assert f.getsampwidth() == 2
            data = np.frombuffer(f.readframes(f.getnframes()), dtype='<i2').astype(np.float64) / 32768
            samples = data.reshape(-1, channels)
        def rms(a, b):
            segment = samples[int(a * rate):int(b * rate)]
            return float(np.sqrt(np.mean(segment * segment)))
        peak = float(np.max(abs(samples)))
        seconds = len(samples) / rate
        idle, takeoff, flare, reverse = rms(4.5, 5.8), rms(10.7, 12.2), rms(17, 17.4), rms(19, 20.5)
        quiet = float(np.max(abs(samples[int(27.4 * rate):])))
        assert 27.5 < seconds < 29.5, (ident, 'duration', seconds)
        assert 0.005 < peak < 0.98, (ident, 'silent or clipped', peak)
        assert takeoff > idle * 1.8, (ident, 'no convincing takeoff rise', idle, takeoff)
        assert reverse > flare * 1.25, (ident, 'no reverse build', flare, reverse)
        assert quiet < 0.0005, (ident, 'mute leak', quiet)
        report.append(dict(type=ident, seconds=round(seconds, 3), channels=channels,
                           peak=round(peak, 5), idle_rms=round(idle, 6),
                           takeoff_rms=round(takeoff, 6), reverse_rms=round(reverse, 6),
                           takeoff_over_idle=round(takeoff / idle, 2),
                           mute_peak=round(quiet, 6), sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
    assert len({r['sha256'] for r in report}) == len(ids), 'identical voices'
    text = json.dumps(report, indent=2) + '\n'
    (args.directory / 'measurements.json').write_text(text)
    print(f'{len(report)} packaged aircraft captures passed: audible, distinct, no clipping, power/reverse rise, silent mute tail')


if __name__ == '__main__':
    main()
