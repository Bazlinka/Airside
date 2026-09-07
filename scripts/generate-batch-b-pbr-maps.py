#!/usr/bin/env python3
"""Generate Batch B companion PBR maps (normal / AO / mask) for Airside surfaces.

Decision 0025 item 4 — authored URP Lit companions for the existing basecolour
set. Project-owned procedural generation; tileable; no third-party packs.
"""

from __future__ import annotations

import uuid
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path("/workspace/game/Airside/Assets/Airside/Art/Textures/Surfaces")
SIZE = 1024
RNG = np.random.default_rng(20260907)


def new_guid() -> str:
    return uuid.uuid4().hex


def write_texture_meta(png_path: Path, *, srgb: bool, normal: bool = False, alpha: bool = False) -> None:
    meta = png_path.with_suffix(".png.meta")
    g = new_guid()
    if meta.exists():
        for line in meta.read_text(encoding="utf-8").splitlines():
            if line.startswith("guid: "):
                g = line.split(":", 1)[1].strip()
                break
    color_space = 1 if srgb else 0
    # Unity TextureImporter.textureType: 0 Default, 1 NormalMap
    texture_type = 1 if normal else 0
    meta.write_text(
        f"""fileFormatVersion: 2
guid: {g}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 1
    sRGBTexture: {color_space}
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: {1 if normal else 0}
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 1024
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 0
    wrapV: 0
    wrapW: 0
  nPOTScale: 1
  lightmap: 0
  compressionQuality: 50
  spriteMode: 0
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: {1 if alpha else 0}
  alphaIsTransparency: 0
  spriteTessellationDetail: -1
  textureType: {texture_type}
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 0
  cookieLightType: 0
  platformSettings: []
  userData: 
  assetBundleName: 
  assetBundleVariant: 
""",
        encoding="utf-8",
    )


def wrap_noise(shape: tuple[int, int], scale: float, octaves: int = 4) -> np.ndarray:
    h, w = shape
    acc = np.zeros((h, w), dtype=np.float64)
    amp = 1.0
    total = 0.0
    for o in range(octaves):
        cells = max(2, int(round(scale * (2**o))))
        while h % cells != 0:
            cells -= 1
            if cells < 2:
                cells = 2
                break
        gh = gw = cells
        grid = RNG.random((gh, gw))
        ys = np.linspace(0, gh, h, endpoint=False)
        xs = np.linspace(0, gw, w, endpoint=False)
        yy, xx = np.meshgrid(ys, xs, indexing="ij")
        y0 = np.floor(yy).astype(int) % gh
        x0 = np.floor(xx).astype(int) % gw
        fy = yy - np.floor(yy)
        fx = xx - np.floor(xx)
        y1 = (y0 + 1) % gh
        x1 = (x0 + 1) % gw
        layer = (
            grid[y0, x0] * (1 - fy) * (1 - fx)
            + grid[y1, x0] * fy * (1 - fx)
            + grid[y0, x1] * (1 - fy) * fx
            + grid[y1, x1] * fy * fx
        )
        acc += layer * amp
        total += amp
        amp *= 0.5
    return acc / total


def height_to_normal(height: np.ndarray, strength: float = 2.0) -> np.ndarray:
    """Convert a height field to a tangent-space RGB normal map (0–255)."""
    h = height.astype(np.float64)
    # Soft blur for stable derivatives.
    pad = np.pad(h, 1, mode="wrap")
    blurred = (
        pad[0:-2, 0:-2]
        + pad[0:-2, 1:-1]
        + pad[0:-2, 2:]
        + pad[1:-1, 0:-2]
        + pad[1:-1, 1:-1]
        + pad[1:-1, 2:]
        + pad[2:, 0:-2]
        + pad[2:, 1:-1]
        + pad[2:, 2:]
    ) / 9.0
    dx = (np.roll(blurred, 1, axis=1) - np.roll(blurred, -1, axis=1)) * strength
    dy = (np.roll(blurred, 1, axis=0) - np.roll(blurred, -1, axis=0)) * strength
    nx = -dx
    ny = -dy
    nz = np.ones_like(blurred)
    length = np.sqrt(nx * nx + ny * ny + nz * nz) + 1e-8
    nx, ny, nz = nx / length, ny / length, nz / length
    rgb = np.stack(
        [
            (nx * 0.5 + 0.5) * 255.0,
            (ny * 0.5 + 0.5) * 255.0,
            (nz * 0.5 + 0.5) * 255.0,
        ],
        axis=-1,
    )
    return rgb


def save_rgb(path: Path, rgb: np.ndarray, *, srgb: bool, normal: bool = False) -> None:
    Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8), mode="RGB").save(path, optimize=True)
    write_texture_meta(path, srgb=srgb, normal=normal)


def save_rgba(path: Path, rgba: np.ndarray) -> None:
    Image.fromarray(np.clip(rgba, 0, 255).astype(np.uint8), mode="RGBA").save(path, optimize=True)
    write_texture_meta(path, srgb=False, alpha=True)


def asphalt_height() -> np.ndarray:
    n = wrap_noise((SIZE, SIZE), scale=8, octaves=5)
    fine = wrap_noise((SIZE, SIZE), scale=64, octaves=3)
    mix = 0.55 * n + 0.45 * fine
    return (mix - mix.min()) / (mix.max() - mix.min() + 1e-8)


def concrete_height() -> np.ndarray:
    n = wrap_noise((SIZE, SIZE), scale=4, octaves=4)
    fine = wrap_noise((SIZE, SIZE), scale=48, octaves=3)
    mix = 0.7 * n + 0.3 * fine
    return (mix - mix.min()) / (mix.max() - mix.min() + 1e-8)


def grass_height() -> np.ndarray:
    n = wrap_noise((SIZE, SIZE), scale=16, octaves=5)
    detail = wrap_noise((SIZE, SIZE), scale=96, octaves=2)
    mix = 0.65 * n + 0.35 * detail
    return (mix - mix.min()) / (mix.max() - mix.min() + 1e-8)


def corrugated_height() -> np.ndarray:
    x = np.arange(SIZE)[None, :]
    wave = 0.5 + 0.5 * np.sin(2 * np.pi * x * 48 / SIZE)
    wave = np.repeat(wave, SIZE, axis=0)
    n = wrap_noise((SIZE, SIZE), scale=6, octaves=3)
    n = (n - n.mean()) / (n.std() + 1e-6)
    h = 0.75 * wave + 0.08 * n
    return (h - h.min()) / (h.max() - h.min() + 1e-8)


def make_ao(height: np.ndarray, contrast: float = 0.35) -> np.ndarray:
    # Darker in recesses — invert-ish height with soft clamp.
    ao = 1.0 - (1.0 - height) * contrast
    ao = np.clip(ao, 0.55, 1.0)
    v = ao * 255.0
    return np.stack([v, v, v], axis=-1)


def make_mask(height: np.ndarray, *, metallic: float, smooth_base: float, smooth_var: float) -> np.ndarray:
    """URP MetallicGlossMap: R ≈ metallic, A ≈ smoothness."""
    smooth = np.clip(smooth_base + (height - 0.5) * smooth_var, 0.05, 0.95)
    rgba = np.zeros((SIZE, SIZE, 4), dtype=np.float64)
    rgba[..., 0] = metallic * 255.0
    rgba[..., 1] = metallic * 255.0
    rgba[..., 2] = metallic * 255.0
    rgba[..., 3] = smooth * 255.0
    return rgba


def emit(stem: str, height: np.ndarray, *, normal_strength: float, metallic: float, smooth_base: float, smooth_var: float, ao_contrast: float) -> None:
    save_rgb(ROOT / f"{stem}_normal_v01.png", height_to_normal(height, normal_strength), srgb=False, normal=True)
    save_rgb(ROOT / f"{stem}_ao_v01.png", make_ao(height, ao_contrast), srgb=False)
    save_rgba(ROOT / f"{stem}_mask_v01.png", make_mask(height, metallic=metallic, smooth_base=smooth_base, smooth_var=smooth_var))
    print(f"wrote {stem} normal/ao/mask")


def glass_height() -> np.ndarray:
    # Very soft micro-ripples — glass should stay almost flat.
    n = wrap_noise((SIZE, SIZE), scale=3, octaves=2)
    return (n - n.min()) / (n.max() - n.min() + 1e-8) * 0.15 + 0.425


def rubber_height() -> np.ndarray:
    # Circumferential tread bands + fine grit.
    y = np.arange(SIZE)[:, None]
    bands = 0.5 + 0.5 * np.sin(2 * np.pi * y * 28 / SIZE)
    bands = np.repeat(bands, SIZE, axis=1)
    grit = wrap_noise((SIZE, SIZE), scale=48, octaves=3)
    grit = (grit - grit.min()) / (grit.max() - grit.min() + 1e-8)
    h = 0.65 * bands + 0.35 * grit
    return (h - h.min()) / (h.max() - h.min() + 1e-8)


def painted_line_height() -> np.ndarray:
    # Nearly flat paint with faint brush noise.
    n = wrap_noise((SIZE, SIZE), scale=10, octaves=3)
    return (n - n.min()) / (n.max() - n.min() + 1e-8) * 0.2 + 0.4


def plastic_height() -> np.ndarray:
    n = wrap_noise((SIZE, SIZE), scale=12, octaves=4)
    return (n - n.min()) / (n.max() - n.min() + 1e-8)


def main() -> None:
    ROOT.mkdir(parents=True, exist_ok=True)
    emit(
        "tx_asphalt_runway",
        asphalt_height(),
        normal_strength=2.4,
        metallic=0.02,
        smooth_base=0.22,
        smooth_var=0.12,
        ao_contrast=0.28,
    )
    emit(
        "tx_concrete_apron",
        concrete_height(),
        normal_strength=8.0,
        metallic=0.03,
        smooth_base=0.28,
        smooth_var=0.1,
        ao_contrast=0.22,
    )
    emit(
        "tx_grass_kingscote",
        grass_height(),
        normal_strength=3.2,
        metallic=0.0,
        smooth_base=0.16,
        smooth_var=0.08,
        ao_contrast=0.4,
    )
    emit(
        "tx_corrugated_metal",
        corrugated_height(),
        normal_strength=4.5,
        metallic=0.55,
        smooth_base=0.42,
        smooth_var=0.18,
        ao_contrast=0.35,
    )
    # MAT-001 — glass / rubber / painted line / plastic companions (0025 item 4).
    emit(
        "tx_glass_pane",
        glass_height(),
        normal_strength=0.6,
        metallic=0.04,
        smooth_base=0.9,
        smooth_var=0.02,
        ao_contrast=0.08,
    )
    emit(
        "tx_rubber_tire",
        rubber_height(),
        normal_strength=3.8,
        metallic=0.01,
        smooth_base=0.12,
        smooth_var=0.08,
        ao_contrast=0.45,
    )
    emit(
        "tx_painted_line",
        painted_line_height(),
        normal_strength=0.8,
        metallic=0.02,
        smooth_base=0.24,
        smooth_var=0.04,
        ao_contrast=0.12,
    )
    emit(
        "tx_plastic_trim",
        plastic_height(),
        normal_strength=1.4,
        metallic=0.05,
        smooth_base=0.4,
        smooth_var=0.1,
        ao_contrast=0.22,
    )
    print("Batch B PBR companions ready under", ROOT)


if __name__ == "__main__":
    main()
