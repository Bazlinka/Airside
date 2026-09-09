#!/usr/bin/env python3
"""Generate seamless procedural surface texture maps v02.

Distinct ids — does not overwrite v01. Does NOT crop the reference board;
procedural tileable maps using Airside palette targets:
  Runway Ink #17242A, Tarmac #343B40, Concrete warmer worn ~#A3A8A4,
  Eucalyptus #4F6F60, Dry Grass #8A8A58, Sand #C8B286, Coastal Blue #39708A.

Board notes (Bailey-approved surface fidelity):
  - Worn concrete: richer fine aggregate, NO slab joint grid
  - Grass: denser high-frequency blade-like streaks
  - Asphalt: darker with rubber streaks
  - Wet concrete: cooler/darker than dry, broad damp sheen, NO puddle shapes
"""

from __future__ import annotations

import importlib.util
from pathlib import Path

import numpy as np
from PIL import Image

REPO = Path(__file__).resolve().parents[1]
ROOT = REPO / "game/Airside/Assets/Airside/Art/Textures/Surfaces"
SIZE = 1024
RNG = np.random.default_rng(20260909)

_bb_spec = importlib.util.spec_from_file_location(
    "batch_b_surfaces", REPO / "scripts/generate-batch-b-surfaces.py"
)
_bb = importlib.util.module_from_spec(_bb_spec)
assert _bb_spec.loader is not None
_bb_spec.loader.exec_module(_bb)

_pbr_spec = importlib.util.spec_from_file_location(
    "batch_b_pbr", REPO / "scripts/generate-batch-b-pbr-maps.py"
)
_pbr = importlib.util.module_from_spec(_pbr_spec)
assert _pbr_spec.loader is not None
_pbr_spec.loader.exec_module(_pbr)

_sw_spec = importlib.util.spec_from_file_location(
    "sand_water", REPO / "scripts/generate-sand-water-surfaces.py"
)
_sw = importlib.util.module_from_spec(_sw_spec)
assert _sw_spec.loader is not None
_sw_spec.loader.exec_module(_sw)

# Point imported modules at v02 size / seed where they use module globals.
_bb.SIZE = SIZE
_bb.RNG = RNG
_pbr.SIZE = SIZE
_pbr.RNG = RNG
_pbr.ROOT = ROOT
_sw.SIZE = SIZE
_sw.RNG = RNG
_sw.ROOT = ROOT

hex_to_rgb = _bb.hex_to_rgb
wrap_noise = _bb.wrap_noise
write_texture_meta = _pbr.write_texture_meta
height_to_normal = _pbr.height_to_normal
make_ao = _pbr.make_ao
make_mask = _pbr.make_mask


def save_rgb(path: Path, rgb: np.ndarray, *, srgb: bool, normal: bool = False) -> None:
    Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8), mode="RGB").save(path, optimize=True)
    write_texture_meta(path, srgb=srgb, normal=normal)
    print(f"wrote {path.name} ({path.stat().st_size} bytes)")


def save_rgba(path: Path, rgba: np.ndarray) -> None:
    Image.fromarray(np.clip(rgba, 0, 255).astype(np.uint8), mode="RGBA").save(path, optimize=True)
    write_texture_meta(path, srgb=False, alpha=True)
    print(f"wrote {path.name} ({path.stat().st_size} bytes)")


def emit_set(
    stem: str,
    basecolor: np.ndarray,
    height: np.ndarray,
    *,
    normal_strength: float,
    metallic: float,
    smooth_base: float,
    smooth_var: float,
    ao_contrast: float,
) -> None:
    save_rgb(ROOT / f"{stem}_basecolor_v02.png", basecolor, srgb=True)
    save_rgb(
        ROOT / f"{stem}_normal_v02.png",
        height_to_normal(height, normal_strength),
        srgb=False,
        normal=True,
    )
    save_rgb(ROOT / f"{stem}_ao_v02.png", make_ao(height, ao_contrast), srgb=False)
    save_rgba(
        ROOT / f"{stem}_mask_v02.png",
        make_mask(height, metallic=metallic, smooth_base=smooth_base, smooth_var=smooth_var),
    )


def make_asphalt_v02() -> tuple[np.ndarray, np.ndarray]:
    # Darker runway read: lean harder into Runway Ink with rubber streaks.
    ink = hex_to_rgb("#17242A")
    tarmac = hex_to_rgb("#343B40")
    n = wrap_noise((SIZE, SIZE), scale=8, octaves=5)
    fine = wrap_noise((SIZE, SIZE), scale=88, octaves=4)
    mid = wrap_noise((SIZE, SIZE), scale=24, octaves=3)
    blend = np.clip(0.55 * n + 0.28 * mid + 0.17 * fine, 0, 1)
    # Bias toward ink (darker overall).
    rgb = ink * (1.0 - 0.55 * blend[..., None]) + tarmac * (0.55 * blend[..., None])
    mix = (blend - blend.mean()) / (blend.std() + 1e-6)
    rgb = rgb + mix[..., None] * np.array([8.0, 8.0, 9.0])
    fleck = wrap_noise((SIZE, SIZE), scale=160, octaves=1)
    rgb += (fleck > 0.82)[..., None] * np.array([7.0, 7.0, 6.0])
    # Rubber streaks — soft dark bands, non-lettering.
    rubber = wrap_noise((SIZE, SIZE), scale=14, octaves=3)
    streak = wrap_noise((SIZE, SIZE), scale=6, octaves=2)
    yy = np.linspace(0, 1, SIZE)[:, None]
    band_a = np.exp(-0.5 * ((yy - 0.42) / 0.09) ** 2)
    band_b = np.exp(-0.5 * ((yy - 0.58) / 0.1) ** 2)
    band = np.clip(band_a + 0.85 * band_b, 0, 1)
    rubber_mask = band * np.clip((rubber - 0.42) / 0.35, 0, 1) * (0.55 + 0.45 * streak)
    rgb -= rubber_mask[..., None] * np.array([14.0, 14.0, 15.0])
    height = 0.45 * n + 0.4 * fine + 0.15 * mid
    height = (height - height.min()) / (height.max() - height.min() + 1e-8)
    return rgb, height


def make_concrete_v02() -> tuple[np.ndarray, np.ndarray]:
    # Warmer worn grey ~#A3A8A4 — richer fine aggregate, NO joint grid.
    base = hex_to_rgb("#A3A8A4")
    n = wrap_noise((SIZE, SIZE), scale=5, octaves=5)
    fine = wrap_noise((SIZE, SIZE), scale=72, octaves=4)
    grit = wrap_noise((SIZE, SIZE), scale=140, octaves=2)
    pebble = wrap_noise((SIZE, SIZE), scale=200, octaves=1)
    mix = (n - n.mean()) / (n.std() + 1e-6)
    fine_m = (fine - fine.mean()) / (fine.std() + 1e-6)
    rgb = base + mix[..., None] * np.array([14.0, 13.0, 11.0])
    rgb = rgb + fine_m[..., None] * np.array([9.0, 8.5, 7.5])
    # Fine aggregate flecks (warmer / cooler chips).
    rgb += (grit > 0.78)[..., None] * np.array([10.0, 9.0, 7.0])
    rgb -= (grit < 0.22)[..., None] * np.array([8.0, 8.0, 7.0])
    rgb += (pebble > 0.9)[..., None] * np.array([6.0, 5.5, 4.5])
    # Soft service wear — broad, no cracks / joints / tire trails.
    wear = wrap_noise((SIZE, SIZE), scale=3, octaves=3)
    rgb -= np.clip((wear - 0.55) / 0.4, 0, 1)[..., None] * np.array([6.0, 6.0, 5.5])
    height = 0.5 * n + 0.35 * fine + 0.15 * grit
    height = (height - height.min()) / (height.max() - height.min() + 1e-8)
    return rgb, height


def make_wet_concrete_v02() -> tuple[np.ndarray, np.ndarray]:
    """Cooler/darker than dry concrete, broad damp sheen — no puddle shapes."""
    dry, height = make_concrete_v02()
    # Cooler + darker shift from dry family.
    cool = np.array([0.88, 0.92, 0.98])
    rgb = dry * 0.78 * cool
    # Soft reflected sky tone (Coastal Blue family, very restrained).
    sky = hex_to_rgb("#39708A")
    sheen_n = wrap_noise((SIZE, SIZE), scale=4, octaves=3)
    sheen = np.clip((sheen_n - sheen_n.mean()) / (sheen_n.std() + 1e-6) * 0.12 + 0.55, 0.35, 0.85)
    # Broad damp sheen only — no discrete puddle blobs.
    rgb = rgb * (0.92 + 0.08 * sheen[..., None]) + sky * (0.04 * sheen[..., None])
    # Slightly flatter height so normals read smoother/wet.
    height = 0.75 * height + 0.25 * sheen_n
    height = (height - height.min()) / (height.max() - height.min() + 1e-8)
    return rgb, height


def make_grass_v02() -> tuple[np.ndarray, np.ndarray]:
    dry = hex_to_rgb("#8A8A58")
    euc = hex_to_rgb("#4F6F60")
    n = wrap_noise((SIZE, SIZE), scale=16, octaves=5)
    patch = wrap_noise((SIZE, SIZE), scale=6, octaves=3)
    blend = np.clip((0.55 * n + 0.45 * patch - 0.28) / 0.48, 0, 1)[..., None]
    rgb = dry * (1 - blend) + euc * blend
    # Denser high-frequency grass detail with blade-like streaks.
    detail = wrap_noise((SIZE, SIZE), scale=130, octaves=4)
    d = (detail - detail.mean()) / (detail.std() + 1e-6)
    rgb = rgb + d[..., None] * 12.0
    # Blade-like anisotropic streaks (short, non-directional overall).
    blade_a = wrap_noise((SIZE, SIZE), scale=180, octaves=2)
    blade_b = wrap_noise((SIZE, SIZE), scale=160, octaves=2)
    xx = np.linspace(0, 1, SIZE)[None, :]
    yy = np.linspace(0, 1, SIZE)[:, None]
    streak_dir = 0.5 + 0.5 * np.sin(2 * np.pi * (xx * 37 + yy * 11 + blade_a * 0.4))
    streak_cross = 0.5 + 0.5 * np.sin(2 * np.pi * (xx * -9 + yy * 41 + blade_b * 0.35))
    blades = np.clip((streak_dir * streak_cross - 0.42) / 0.35, 0, 1)
    rgb += blades[..., None] * np.array([6.0, 8.0, 5.0])
    rgb -= (blades > 0.7)[..., None] * np.array([4.0, 3.0, 4.0]) * (1.0 - blend[..., 0:1])
    # Micro flecks for denser turf read at overview.
    fleck = wrap_noise((SIZE, SIZE), scale=220, octaves=1)
    rgb += (fleck > 0.88)[..., None] * np.array([5.0, 7.0, 4.0])
    height = 0.45 * n + 0.35 * detail + 0.2 * blades
    height = (height - height.min()) / (height.max() - height.min() + 1e-8)
    return rgb, height


def make_sand_v02() -> tuple[np.ndarray, np.ndarray]:
    sand = hex_to_rgb("#C8B286")
    dark = sand * 0.78
    n = wrap_noise((SIZE, SIZE), scale=10, octaves=5)
    ripples = wrap_noise((SIZE, SIZE), scale=48, octaves=3)
    mix = 0.55 * n + 0.45 * ripples
    t = (mix - mix.min()) / (mix.max() - mix.min() + 1e-8)
    rgb = sand * t[..., None] + dark * (1 - t[..., None])
    fleck = wrap_noise((SIZE, SIZE), scale=96, octaves=1)
    rgb += (fleck > 0.85)[..., None] * np.array([8.0, 7.0, 5.0])
    height = t
    return rgb, height


def make_water_v02() -> tuple[np.ndarray, np.ndarray]:
    deep = hex_to_rgb("#39708A") * 0.55
    shallow = hex_to_rgb("#39708A")
    foam = np.array([0.78, 0.88, 0.92], dtype=np.float64) * 255.0
    x = np.arange(SIZE, dtype=np.float64)[None, :]
    y = np.arange(SIZE, dtype=np.float64)[:, None]
    wave = (
        0.5 * (0.5 + 0.5 * np.sin(2 * np.pi * (x * 4 + y * 1.4) / SIZE))
        + 0.3 * (0.5 + 0.5 * np.sin(2 * np.pi * (x * -1.8 + y * 5) / SIZE))
        + 0.2 * wrap_noise((SIZE, SIZE), scale=32, octaves=3)
    )
    h = (wave - wave.min()) / (wave.max() - wave.min() + 1e-8)
    rgb = deep * (1 - h[..., None]) + shallow * h[..., None]
    crest = np.clip((h - 0.7) / 0.3, 0, 1)[..., None]
    rgb = rgb * (1 - crest * 0.4) + foam * crest * 0.4
    return rgb, h


def main() -> None:
    ROOT.mkdir(parents=True, exist_ok=True)

    asphalt, ah = make_asphalt_v02()
    emit_set(
        "tx_asphalt_runway",
        asphalt,
        ah,
        normal_strength=2.8,
        metallic=0.02,
        smooth_base=0.22,
        smooth_var=0.12,
        ao_contrast=0.32,
    )

    concrete, ch = make_concrete_v02()
    emit_set(
        "tx_concrete_apron",
        concrete,
        ch,
        normal_strength=3.0,
        metallic=0.03,
        smooth_base=0.28,
        smooth_var=0.1,
        ao_contrast=0.22,
    )

    wet, wh = make_wet_concrete_v02()
    emit_set(
        "tx_wet_concrete",
        wet,
        wh,
        normal_strength=2.2,
        metallic=0.04,
        # Higher smoothness = broad damp sheen (no puddle shapes in albedo).
        smooth_base=0.55,
        smooth_var=0.08,
        ao_contrast=0.18,
    )

    grass, gh = make_grass_v02()
    emit_set(
        "tx_grass_kingscote",
        grass,
        gh,
        normal_strength=3.8,
        metallic=0.0,
        smooth_base=0.14,
        smooth_var=0.1,
        ao_contrast=0.45,
    )

    sand, sh = make_sand_v02()
    emit_set(
        "tx_sand_coast",
        sand,
        sh,
        normal_strength=3.0,
        metallic=0.0,
        smooth_base=0.2,
        smooth_var=0.08,
        ao_contrast=0.34,
    )

    water, wh2 = make_water_v02()
    emit_set(
        "tx_water_coast",
        water,
        wh2,
        normal_strength=1.8,
        metallic=0.02,
        smooth_base=0.72,
        smooth_var=0.1,
        ao_contrast=0.16,
    )

    print("Surface texture v02 maps ready under", ROOT)


if __name__ == "__main__":
    main()
