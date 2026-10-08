#!/usr/bin/env python3
"""Author the v02 aircraft/soundscape bank from registered CC0 sources, offline.

python + numpy only; source capture/crop lineage is docs/data/audio/audio_overhaul_sources.json.
--check regenerates and compares bytes without writes. Frequencies are sound design,
not a manufacturer recording claim. Circular FFT and periodic modulation avoid clicks.
"""
import argparse
import hashlib
import json
from pathlib import Path
import wave
import numpy as np
from generate_aircraft_audio import read, wav, level, filter_band, loop

ROOT = Path(__file__).resolve().parents[2]
AUDIO = ROOT / 'game/Airside/Assets/Resources/Airside/Audio'
MANIFEST = ROOT / 'docs/data/audio/audio_overhaul_manifest_v02.json'
RATE = 22050
N = RATE * 8
T = np.arange(N) / RATE


def noise(seed, lo, hi):
    # PCG seed is explicit; no runtime random source or simulation state involved.
    return filter_band(np.random.Generator(np.random.PCG64(seed)).uniform(-1, 1, N), lo, hi)


def tone(hz):
    # Quantise to an integer number of cycles per bank period.
    return np.sin(2 * np.pi * round(hz * 8) / 8 * T)


def periodic(x):
    # Rotate the existing circular signal to a low-slope join; never flatten/fade its energy.
    # Integer-cycle harmonics and circular FFT bands retain every original sample.
    join = int(np.argmin(np.abs(x - np.roll(x, 1))))
    return np.roll(x, -join)


def bank():
    profiles = json.loads((ROOT / 'docs/data/AIRCRAFT_AUDIO_PROFILES.json').read_text())['profiles']
    result = {}
    for idx, p in enumerate(profiles):
        key = p['id'].lower()
        prop = p['family'] != 'jet'
        bed = read(AUDIO / f'eng_{key}_power_v01.wav')
        body = p['body_hz']
        # A separately controlled rotation signature gives governor/power independent motion.
        if prop:
            blade = {'atr42': 80, 'sf34': 92, 'dh8d': 102}[key]
            pulse = sum(tone(blade * harmonic) / harmonic ** 1.22 for harmonic in range(1, 9))
            core = pulse * (0.80 + 0.12 * tone(0.625)) + noise(620 + idx, 60, 1300) * 0.35
            power = filter_band(bed, 32, p['brightness_hz'] * 1.8) + noise(400 + idx, 65, 2700) * 0.10
            idle = filter_band(bed, 30, 720) + tone(body / 2) * 0.025
        else:
            # Older direct-drive families have a rougher fan buzz; geared/neo voices are silkier.
            rough = key in ('a320', 'b738', 'e190')
            fan = sum(tone(body * harmonic) / harmonic ** (1.2 if rough else 1.9)
                      for harmonic in range(1, 13))
            core = fan * 0.24 + tone(p['brightness_hz'] * 0.62) * (0.035 if rough else 0.018)
            core += noise(620 + idx, 90, 1500) * 0.25
            power = filter_band(bed, 25, p['brightness_hz'] * 1.5)
            power += noise(400 + idx, 28, 1800) * (0.24 if body < 125 else 0.14)
            idle = filter_band(bed, 35, 950) + tone(body * 0.5) * 0.012
        # Reverse is its own broadband exhaust/propwash, no forward fan whistle.
        reverse = noise(800 + idx, 35, 4400 if prop else 5800)
        reverse *= 1 + 0.20 * tone(19.375 if prop else 7.625) + 0.09 * tone(31.125)
        reverse += filter_band(np.roll(bed, 23471 + idx * 719), 35, 2400) * 0.65
        for role, data, db in [('idle', idle, -20), ('power', power, -18),
                               ('core', core, -20), ('reverse', reverse, -18)]:
            result[f'eng_{key}_{role}_v02.wav'] = (periodic(level(data, db)), True)
    # Bell's blade slap is deliberately non-sinusoidal: four broad pulses per revolution.
    rotor = sum(tone(22 * h) / h ** 1.15 for h in range(1, 16))
    rotor *= 0.8 + 0.16 * tone(5.5)
    turbulence = noise(911, 35, 1600)
    for role, data in [('idle', rotor * 0.45 + turbulence), ('power', rotor + turbulence * 0.7),
                       ('core', rotor + tone(121) * 0.18), ('reverse', turbulence)]:
        result[f'eng_b412_{role}_v02.wav'] = (periodic(level(data, -20 if role == 'core' else -18)), True)
    # Starter spools are short circular texture beds; runtime envelopes and pitch follow each engine.
    for idx, kind in enumerate(['prop', 'jet', 'rotor']):
        x = noise(1000 + idx, 250, 4200) * 0.6 + tone(440 + idx * 180) * 0.045
        result[f'aircraft_starter_{kind}_v02.wav'] = (periodic(level(x, -23)), True)
    apu = noise(1010, 70, 2800) + tone(188) * 0.10 + tone(752) * 0.025
    result['aircraft_apu_v02.wav'] = (periodic(level(apu, -25)), True)
    cabin_source = loop(read(ROOT / 'docs/data/audio/src_jet_cabin_richwise_451741.wav'))
    # Remove speech intelligibility, retain real airframe/air-conditioning texture.
    for idx, kind in enumerate(['jet', 'prop', 'rotor']):
        x = filter_band(cabin_source, 25, 620) + noise(1050 + idx, 45, 1200) * 0.08
        if kind == 'prop': x += filter_band(read(AUDIO / 'eng_dash8_300_twin.wav')[:N], 30, 320) * 0.30
        if kind == 'rotor': x += rotor * 0.025
        result[f'aircraft_cabin_{kind}_v02.wav'] = (periodic(level(x, -24)), True)
    airflow = noise(1100, 160, 2200) * (0.91 + 0.09 * tone(0.375))
    result['aircraft_airflow_v02.wav'] = (periodic(level(airflow, -23)), True)
    # Original mechanical one-shots: brief servo, linkage and latch; no alarm tones.
    for idx, role in enumerate(['gear', 'flap', 'door']):
        seconds = [2.2, 1.4, 0.65][idx]
        length = int(seconds * RATE)
        t = T[:length]
        x = noise(1200 + idx, 90, 2300)[:length]
        envelope = np.minimum(t / 0.06, 1) * np.minimum((seconds - t) / 0.14, 1)
        x = x * envelope * 0.30 + np.sin(2 * np.pi * (165 + idx * 70) * t) * envelope * 0.035
        for offset in ([0.07, 1.91] if idx == 0 else [0.09, seconds - 0.24]):
            age = np.maximum(0, t - offset)
            x += (t >= offset) * np.sin(2 * np.pi * 86 * age) * np.exp(-age * 35) * 0.16
        x[0] = x[-1] = 0
        result[f'aircraft_{role}_v02.wav'] = (level(x, -23), False)
    # Working apron: layered power-unit/ventilation and irregular distant service traffic.
    x = noise(1300, 45, 1400) + tone(100) * 0.07 + tone(250) * 0.025
    x += noise(1301, 80, 900) * (0.30 + 0.18 * tone(0.25))
    result['airport_apron_v02.wav'] = (periodic(level(x, -24)), True)
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    manifest = {'version': 2, 'rate': RATE, 'sources': 'audio_overhaul_sources.json', 'clips': []}
    failures = []
    for name, (x, looping) in bank().items():
        data = wav(x)
        item = {'file': name, 'sha256': hashlib.sha256(data).hexdigest(), 'seconds': len(x) / RATE,
                'loop': looping, 'rms_dbfs': round(float(20 * np.log10(np.sqrt(np.mean(x * x)))), 3),
                'peak': round(float(np.max(np.abs(x))), 6), 'seam': round(float(abs(x[0] - x[-1])), 6),
                'mean_step': round(float(np.mean(np.abs(np.diff(x)))), 6)}
        manifest['clips'].append(item)
        if args.check:
            if not (AUDIO / name).exists() or (AUDIO / name).read_bytes() != data: failures.append(name)
        else: (AUDIO / name).write_bytes(data)
        assert np.isfinite(x).all() and item['peak'] <= 0.82 + 1e-6, name
        assert not looping or item['seam'] < max(0.006, item['mean_step'] * 3), name
    encoded = json.dumps(manifest, indent=2) + '\n'
    if args.check:
        if not MANIFEST.exists() or MANIFEST.read_text() != encoded: failures.append(str(MANIFEST))
    else: MANIFEST.write_text(encoded)
    if failures: raise SystemExit('Stale outputs: ' + ', '.join(failures))
    print(f"{'Checked' if args.check else 'Generated'} {len(manifest['clips'])} v02 clips; finite, headroom and loop joins passed")


if __name__ == '__main__': main()
