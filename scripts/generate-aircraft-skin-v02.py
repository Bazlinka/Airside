#!/usr/bin/env python3
"""tx_aircraft_skin_*_v02 — a real airliner skin at a known metre scale.

The v01 set is 256x256, its basecolor is near-flat white (std 6.5/255) and its mask is a
single constant value, so it carried no panel, rivet or wear information at all. It could
not have: until ArtGltfLoader.BuildMetreUvs, aircraft UVs normalised each part's own
bounding box to 0..1, so texel density varied by over a hundred times across one airframe
and the fuselage was unwrapped down its own length.

With UVs now in metres and AircraftSkin tiling at 0.5 (one repeat every two metres), this
tile is authored as exactly 2.032 m square at 1024 px — about 2 mm per texel. Everything is
placed in real units:

  frames      0.508 m pitch, matching the cabin window pitch the generators already use
  stringers   0.254 m pitch, shallower than the frames
  rivets      0.0635 m pitch along every frame line
  panels      a coarser 1.016 m lap-joint grid with a slight edge step

Every pitch is a harmonic of the frame pitch and divides the tile a whole number of times,
so the tile wraps without a seam.

Outputs basecolor / normal / ao / mask. Mask is URP Lit's _MetallicGlossMap: R metallic,
A smoothness. Run: python3 scripts/generate-aircraft-skin-v02.py
"""
import os
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(HERE)
OUT = os.path.join(REPO, "game", "Airside", "Assets", "Airside", "Art", "Textures", "Surfaces")

# Every pitch is a harmonic of the frame pitch and divides the tile a whole number of
# times, so the tile repeats without a seam. A tile that is merely "about 2 m" leaves a
# visible ring every repeat down the fuselage, which is worse than no detail at all.
FRAME_PITCH_M = 0.508                  # cabin frame pitch, as used by the generators
PX = 1024
TILE_M = FRAME_PITCH_M * 4             # 2.032 m
PPM = PX / TILE_M                      # pixels per metre
STRINGER_PITCH_M = FRAME_PITCH_M / 2   # 0.254
RIVET_PITCH_M = FRAME_PITCH_M / 8      # 0.0635
PANEL_PITCH_M = TILE_M / 2             # 1.016

rng = np.random.default_rng(20260922)


def _axis_lines(pitch_m, width_px, softness=1.0):
    """Periodic line mask along one axis, 0..1, seamless across the tile."""
    coord = np.arange(PX, dtype=np.float64) / PPM
    phase = np.mod(coord, pitch_m)
    dist = np.minimum(phase, pitch_m - phase) * PPM      # px to nearest line
    return np.clip(1.0 - dist / max(width_px * softness, 1e-6), 0.0, 1.0)


def _rivets():
    """Dotted rivet rows sitting on the frame lines."""
    fy = _axis_lines(FRAME_PITCH_M, 1.6)[:, None]
    along = _axis_lines(RIVET_PITCH_M, 1.1)[None, :]
    return fy * along


def _panels():
    """Coarse lap-joint grid: a wider, softer seam than the frames."""
    u = _axis_lines(PANEL_PITCH_M, 3.0)[None, :]
    v = _axis_lines(PANEL_PITCH_M, 3.0)[:, None]
    return np.clip(u + v, 0.0, 1.0)


def _mottle(cycles, amp, harmonics=24):
    """
    Low-frequency paint/wear variation. Built from sinusoids at whole cycles per tile so it
    is periodic by construction — a resized random field is not, and left a seam.

    Enough harmonics, with 1/f amplitude falloff and independent phases, that it reads as
    organic weathering. A handful of low harmonics instead produced an obvious repeating
    interference pattern, which is worse on a fuselage than no variation at all.
    """
    u = np.arange(PX, dtype=np.float64)[None, :] / PX * 2.0 * np.pi
    v = np.arange(PX, dtype=np.float64)[:, None] / PX * 2.0 * np.pi
    field = np.zeros((PX, PX), dtype=np.float64)
    weight = 0.0
    for _ in range(harmonics):
        ku = int(rng.integers(1, cycles + 1))
        kv = int(rng.integers(1, cycles + 1))
        w = 1.0 / (ku + kv)
        pu = rng.random() * 2.0 * np.pi
        pv = rng.random() * 2.0 * np.pi
        field += w * np.sin(ku * u + pu) * np.cos(kv * v + pv)
        weight += w
    field /= max(weight, 1e-6)
    # Normalise to a predictable amplitude regardless of how the phases landed.
    peak = np.abs(field).max()
    return field / max(peak, 1e-6) * amp


def build():
    frames_v = _axis_lines(FRAME_PITCH_M, 1.5)[:, None] * np.ones((1, PX))
    stringers_u = _axis_lines(STRINGER_PITCH_M, 1.1)[None, :] * np.ones((PX, 1))
    rivets = _rivets()
    panels = _panels()

    # ---- basecolor: white airframe paint, seams a touch darker, subtle mottle.
    base = np.full((PX, PX, 3), 0.937, dtype=np.float64)
    base += _mottle(9, 0.022)[..., None]
    base -= (frames_v * 0.055 + stringers_u * 0.030)[..., None]
    base -= (panels * 0.028)[..., None]
    base -= (rivets * 0.065)[..., None]
    # Very slight cool cast in the recesses, the way white paint reads in shadow.
    base[..., 2] += (frames_v + stringers_u) * 0.012
    base = np.clip(base, 0.0, 1.0)

    # ---- normal: seams are grooves, rivets are bumps.
    height = -(frames_v * 0.55 + stringers_u * 0.30 + panels * 0.22)
    height += rivets * 0.85
    height += _mottle(14, 0.06)
    # Central differences with np.roll, not np.gradient: gradient falls back to one-sided
    # differences at the array edges, which breaks periodicity and leaves a lit seam.
    gx = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) * 0.5
    gy = (np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)) * 0.5
    strength = 2.6
    nx, ny, nz = -gx * strength, -gy * strength, np.ones_like(height)
    norm = np.sqrt(nx * nx + ny * ny + nz * nz)
    normal = np.stack([nx / norm, ny / norm, nz / norm], axis=-1)
    normal = (normal * 0.5 + 0.5)

    # ---- ao: contact darkening in the seams only.
    ao = 1.0 - np.clip(frames_v * 0.46 + stringers_u * 0.26 + panels * 0.20, 0.0, 0.58)
    ao += _mottle(8, 0.03)
    ao = np.clip(ao, 0.0, 1.0)

    # ---- mask: R metallic, A smoothness (URP Lit _MetallicGlossMap).
    # Painted skin is mostly dielectric; bare rivets and seam wear lift metallic a little.
    metallic = np.clip(0.10 + rivets * 0.45 + frames_v * 0.10, 0.0, 1.0)
    # Gloss falls in the seams and where the paint has weathered.
    smooth = np.clip(0.72 - frames_v * 0.22 - stringers_u * 0.12 - panels * 0.10
                     + _mottle(10, 0.12), 0.0, 1.0)
    mask = np.stack([metallic,
                     np.zeros_like(metallic),
                     np.zeros_like(metallic),
                     smooth], axis=-1)

    return {
        "basecolor": (base, "RGB"),
        "normal": (normal, "RGB"),
        "ao": (np.repeat(ao[..., None], 3, axis=-1), "RGB"),
        "mask": (mask, "RGBA"),
    }


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, (data, mode) in build().items():
        arr = (np.clip(data, 0.0, 1.0) * 255.0 + 0.5).astype(np.uint8)
        path = os.path.join(OUT, f"tx_aircraft_skin_{name}_v02.png")
        Image.fromarray(arr, mode).save(path, optimize=True)
        print(f"wrote {os.path.relpath(path, REPO)}  {arr.shape} {mode}")


if __name__ == "__main__":
    main()
