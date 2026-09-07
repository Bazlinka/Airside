#!/usr/bin/env python3
"""Generate aircraft skin surface PBR companions (0025 item 4).

256² tileable white/aluminium skin maps so AircraftSkin materials catch light
under follow camera. Project-owned; no third-party packs.
"""

from __future__ import annotations

import uuid
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path("/workspace/game/Airside/Assets/Airside/Art/Textures/Surfaces")
SIZE = 256
RNG = np.random.default_rng(202609072)


def write_texture_meta(png_path: Path, *, srgb: bool, normal: bool = False, alpha: bool = False) -> None:
    meta = png_path.with_suffix(".png.meta")
    meta.write_text(
        f"""fileFormatVersion: 2
guid: {uuid.uuid4().hex}
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
    print(f"wrote {path.name}")


def save_mask(path: Path, height: np.ndarray, *, metallic: float, smooth_base: float, smooth_var: float) -> None:
    smooth = np.clip(smooth_base + (height - 0.5) * smooth_var, 0.05, 0.95)
    rgba = np.zeros((SIZE, SIZE, 4), dtype=np.uint8)
    m = int(metallic * 255)
    rgba[..., 0] = m
    rgba[..., 1] = m
    rgba[..., 2] = m
    rgba[..., 3] = (smooth * 255).astype(np.uint8)
    Image.fromarray(rgba, mode="RGBA").save(path, optimize=True)
    write_texture_meta(path, srgb=False, alpha=True)
    print(f"wrote {path.name}")


def main() -> None:
    ROOT.mkdir(parents=True, exist_ok=True)
    # Soft panel seams + rivet grit.
    h = 0.55 * noise(8) + 0.3 * noise(32) + 0.15 * noise(64)
    h = (h - h.min()) / (h.max() - h.min() + 1e-8)
    # Horizontal panel lines.
    y = np.arange(SIZE)[:, None]
    seams = (np.abs(np.sin(2 * np.pi * y * 6 / SIZE)) < 0.08).astype(np.float32) * 0.12
    h = np.clip(h - seams, 0, 1)

    base = np.array([0.92, 0.93, 0.94], dtype=np.float32)
    dark = np.array([0.78, 0.8, 0.82], dtype=np.float32)
    rgb = (base * (0.7 + 0.3 * h[..., None]) + dark * (0.3 - 0.2 * h[..., None])) * 255.0
    save_rgb(ROOT / "tx_aircraft_skin_basecolor_v01.png", rgb, srgb=True)
    save_rgb(ROOT / "tx_aircraft_skin_normal_v01.png", height_to_normal(h, 1.8), srgb=False, normal=True)
    ao = np.clip(1.0 - (1.0 - h) * 0.22, 0.6, 1.0) * 255.0
    save_rgb(ROOT / "tx_aircraft_skin_ao_v01.png", np.stack([ao, ao, ao], axis=-1), srgb=False)
    save_mask(
        ROOT / "tx_aircraft_skin_mask_v01.png",
        h,
        metallic=0.18,
        smooth_base=0.55,
        smooth_var=0.12,
    )
    print("Aircraft skin surfaces ready under", ROOT)


if __name__ == "__main__":
    main()
