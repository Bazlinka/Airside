#!/usr/bin/env python3
"""ADR 0136 — repair the recorded audio beds in place (CC0 sources, see ASSET_AND_DATA_REGISTER.md).

* Engine loops: loudness-matched to a common RMS and made seamless with an equal-power crossfade of
  the tail into the head (the shipped files clicked at every wrap; the jet was 12–20 dB hotter than
  the turboprops, so jets roared and Saabs were near silent at the same source volume).
* Coast: the 4 s single wave faded to silence each loop, a regular pulse. Rebuilt as a 16 s bed of
  overlapping waves at irregular spacing, looped the same way.

Idempotent in intent but not in effect: run it on the original files only (it keeps no backup).
Usage: scripts/audio/process_beds.py <Audio dir> [--report]
"""
import math, struct, sys, os

def read_wav(path):
    b = open(path, 'rb').read()
    i = 12; fmt = None; data = None
    while i < len(b):
        cid = b[i:i+4]; size = struct.unpack('<I', b[i+4:i+8])[0]; body = b[i+8:i+8+size]
        if cid == b'fmt ': fmt = struct.unpack('<HHIIHH', body[:16])
        if cid == b'data': data = body
        i += 8 + size + (size & 1)
    _, ch, sr, _, _, bits = fmt
    assert bits == 16, f'{path}: {bits}-bit not supported'
    n = len(data) // 2 // ch
    vals = struct.unpack('<%dh' % (n * ch), data[:n * ch * 2])
    chans = [[vals[k * ch + c] / 32768.0 for k in range(n)] for c in range(ch)]
    return chans, sr

def write_wav(path, chans, sr):
    ch = len(chans); n = len(chans[0])
    frames = bytearray()
    for k in range(n):
        for c in range(ch):
            v = max(-1.0, min(1.0, chans[c][k]))
            frames += struct.pack('<h', int(round(v * 32767)))
    header = struct.pack('<4sI4s4sIHHIIHH4sI', b'RIFF', 36 + len(frames), b'WAVE', b'fmt ', 16, 1, ch, sr,
                         sr * ch * 2, ch * 2, 16, b'data', len(frames))
    open(path, 'wb').write(header + frames)

def rms(x): return math.sqrt(sum(v * v for v in x) / len(x))

def loop_crossfade(x, fade):
    """Seamless loop: the last `fade` samples are blended into the first, and dropped from the end."""
    n = len(x); out = x[:n - fade]
    for i in range(fade):
        t = i / fade
        out[i] = x[i] * math.sin(t * math.pi / 2) + x[n - fade + i] * math.cos(t * math.pi / 2)
    return out

def gain_to(chans, target_db):
    g = 10 ** (target_db / 20) / max(1e-9, rms(chans[0]))
    return [[v * g for v in c] for c in chans]

def process_engine(path, target_db):
    chans, sr = read_wav(path)
    chans = gain_to(chans, target_db)
    peak = max(abs(v) for c in chans for v in c)
    if peak > 0.89:  # keep 1 dB of headroom
        chans = [[v * 0.89 / peak for v in c] for c in chans]
    chans = [loop_crossfade(c, int(sr * 0.6)) for c in chans]
    write_wav(path, chans, sr)

def process_coast(path, seconds=16.0):
    chans, sr = read_wav(path)
    wave_len = len(chans[0]); total = int(sr * seconds) + int(sr * 1.0)
    bed = [[0.0] * total for _ in chans]
    for offset, g in [(0.0, 1.0), (3.1, 0.8), (6.9, 0.95), (9.4, 0.7), (13.2, 0.9), (15.6, 0.85)]:
        start = int(offset * sr)
        for c in range(len(chans)):
            for k in range(wave_len):
                if start + k < total:
                    bed[c][start + k] += chans[c][k] * g
    bed = [loop_crossfade(c, int(sr * 1.0)) for c in bed]
    bed = gain_to(bed, -24.0)
    write_wav(path, bed, sr)

def report(path):
    chans, sr = read_wav(path); x = chans[0]; n = len(x)
    step = sum(abs(x[i + 1] - x[i]) for i in range(n - 1)) / (n - 1)
    print(f'{os.path.basename(path)}: {n / sr:.1f}s rms {20 * math.log10(rms(x)):.1f} dBFS '
          f'peak {max(abs(v) for v in x):.2f} seam {abs(x[-1] - x[0]):.4f} (typical step {step:.4f})')

if __name__ == '__main__':
    audio = sys.argv[1]
    names = ['eng_jet_turbine.wav', 'eng_dash8_q400_pw100.wav', 'eng_dash8_300_twin.wav', 'coast_wave_01.wav']
    if '--report' not in sys.argv:
        process_engine(os.path.join(audio, 'eng_jet_turbine.wav'), -20.0)
        process_engine(os.path.join(audio, 'eng_dash8_q400_pw100.wav'), -20.0)
        process_engine(os.path.join(audio, 'eng_dash8_300_twin.wav'), -20.0)
        process_coast(os.path.join(audio, 'coast_wave_01.wav'))
    for name in names:
        report(os.path.join(audio, name))
