#!/usr/bin/env python3
"""Generate sand + water surface basecolour and PBR companions (0025 item 4).

512² proved heavy under concurrent agent load; 256² tileable maps are enough
for the distant coastal strip and stay light to regenerate.
"""

from __future__ import annotations

import uuid
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path("/workspace/game/Airside/Assets/Airside/Art/Textures/Surfaces")
SIZE = 256
RNG = np.random.default_rng(202609071)


def new_guid() -> str:
    return uuid.uuid4().hex


def write_texture_meta(png_path: Path, *, srgb: bool, normal: bool = False, alpha: bool = False) -> None:
    g = new_guid()
    meta = png_path.with_suffix(".png.meta")
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
    sRGBTexture: {1 if srgb else 0}
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
  maxTextureSize: 512
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
  textureType: {1 if normal else 0}
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


def noise(cells: int) -> np.ndarray:
    """Nearest-neighbour upsampled tileable noise to SIZE×SIZE."""
    cells = max(2, min(cells, SIZE))
    while SIZE % cells != 0:
        cells -= 1
        if cells < 2:
            cells = 2
            break
    grid = RNG.random((cells, cells), dtype=np.float32)
    reps = SIZE // cells
    return np.repeat(np.repeat(grid, reps, axis=0), reps, axis=1)


def height_to_normal(height: np.ndarray, strength: float) -> np.ndarray:
    dx = (np.roll(height, 1, axis=1) - np.roll(height, -1, axis=1)) * strength
    dy = (np.roll(height, 1, axis=0) - np.roll(height, -1, axis=0)) * strength
    nx, ny, nz = -dx, -dy, np.ones_like(height)
    length = np.sqrt(nx * nx + ny * ny + nz * nz) + 1e-8
    return np.stack(
        [(nx / length) * 0.5 + 0.5, (ny / length) * 0.5 + 0.5, (nz / length) * 0.5 + 0.5],
        axis=-1,
    ) * 255.0


def save_rgb(path: Path, rgb: np.ndarray, *, srgb: bool, normal: bool = False) -> None:
    Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8), mode="RGB").save(path, optimize=True)
    write_texture_meta(path, srgb=srgb, normal=normal)
    print(f"wrote {path.name} ({path.stat().st_size} bytes)")


def save_mask(path: Path, height: np.ndarray, *, metallic: float, smooth_base: float, smooth_var: float) -> None:
    smooth = np.clip(smooth_base + (height - 0.5) * smooth_var, 0.05, 0.95)
    rgba = np.zeros((SIZE, SIZE, 4), dtype=np.uint8)
    rgba[..., 0] = int(metallic * 255)
    rgba[..., 1] = int(metallic * 255)
    rgba[..., 2] = int(metallic * 255)
    rgba[..., 3] = (smooth * 255).astype(np.uint8)
    Image.fromarray(rgba, mode="RGBA").save(path, optimize=True)
    write_texture_meta(path, srgb=False, alpha=True)
    print(f"wrote {path.name} ({path.stat().st_size} bytes)")


def emit_pbr(stem: str, height: np.ndarray, *, normal_strength: float, metallic: float, smooth_base: float, smooth_var: float, ao_contrast: float) -> None:
    save_rgb(ROOT / f"{stem}_normal_v01.png", height_to_normal(height, normal_strength), srgb=False, normal=True)
    ao = np.clip(1.0 - (1.0 - height) * ao_contrast, 0.55, 1.0) * 255.0
    save_rgb(ROOT / f"{stem}_ao_v01.png", np.stack([ao, ao, ao], axis=-1), srgb=False)
    save_mask(
        ROOT / f"{stem}_mask_v01.png",
        height,
        metallic=metallic,
        smooth_base=smooth_base,
        smooth_var=smooth_var,
    )


def main() -> None:
    ROOT.mkdir(parents=True, exist_ok=True)

    sand_h = 0.6 * noise(8) + 0.4 * noise(32)
    sand_h = (sand_h - sand_h.min()) / (sand_h.max() - sand_h.min() + 1e-8)
    base = np.array([0.82, 0.72, 0.52], dtype=np.float32)
    dark = np.array([0.62, 0.52, 0.36], dtype=np.float32)
    t = sand_h[..., None]
    save_rgb(ROOT / "tx_sand_coast_basecolor_v01.png", (base * t + dark * (1 - t)) * 255.0, srgb=True)
    emit_pbr(
        "tx_sand_coast",
        sand_h,
        normal_strength=2.8,
        metallic=0.0,
        smooth_base=0.18,
        smooth_var=0.08,
        ao_contrast=0.32,
    )

    x = np.arange(SIZE, dtype=np.float32)[None, :]
    y = np.arange(SIZE, dtype=np.float32)[:, None]
    water_h = (
        0.55 * (0.5 + 0.5 * np.sin(2 * np.pi * (x * 3 + y * 1.2) / SIZE))
        + 0.3 * (0.5 + 0.5 * np.sin(2 * np.pi * (x * -1.5 + y * 4) / SIZE))
        + 0.15 * noise(32)
    )
    water_h = (water_h - water_h.min()) / (water_h.max() - water_h.min() + 1e-8)
    deep = np.array([0.14, 0.32, 0.48], dtype=np.float32)
    shallow = np.array([0.28, 0.52, 0.62], dtype=np.float32)
    foam = np.array([0.75, 0.85, 0.9], dtype=np.float32)
    wh = water_h[..., None]
    rgb = deep * (1 - wh) + shallow * wh
    crest = np.clip((water_h - 0.72) / 0.28, 0, 1)[..., None]
    rgb = rgb * (1 - crest * 0.35) + foam * crest * 0.35
    save_rgb(ROOT / "tx_water_coast_basecolor_v01.png", rgb * 255.0, srgb=True)
    emit_pbr(
        "tx_water_coast",
        water_h,
        normal_strength=1.6,
        metallic=0.02,
        smooth_base=0.72,
        smooth_var=0.12,
        ao_contrast=0.15,
    )
    print("Sand + water surfaces ready under", ROOT)


if __name__ == "__main__":
    main()
