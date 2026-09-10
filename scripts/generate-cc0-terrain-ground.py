#!/usr/bin/env python3
"""Process the CC0 ground sources into Airside runtime maps.

Sources are downloaded by hand into work/ground-src (git-ignored) and are never
committed; only the processed runtime maps and their .meta files ship. Re-running
this script is deterministic: same inputs, same bytes out, so the hashes recorded
in docs/data/ASSET_AND_DATA_REGISTER.md stay verifiable.

Two different mask conventions are in play and must not be mixed up:

  * Surfaces/  `*_mask_*`    -> Unity _MetallicGlossMap. RGB = metallic, A = smoothness.
                               This is what AirsideMaterialLibrary already binds.
  * Terrain/   `*_maskmap_*` -> URP TerrainLayer mask map. R = metallic, G = AO,
                               B = height, A = smoothness.

Normals are taken from each source's OpenGL/Y+ variant because Unity expects Y+;
no green-channel flip is applied anywhere.

Usage: python3 scripts/generate-cc0-terrain-ground.py
"""

from __future__ import annotations

import hashlib
import io
import json
import uuid
import zipfile
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "work" / "ground-src"
ART = ROOT / "game" / "Airside" / "Assets" / "Airside" / "Art" / "Textures"
TERRAIN_OUT = ART / "Terrain"
SURFACE_OUT = ART / "Surfaces"

# Runtime maps ship at 1024 to match the already-registered asphalt v03 set and to
# hold the packaged-Mac memory target; the 2K downloads stay in work/ as evidence.
SIZE = 1024

# Approved palette targets from docs/art/ART_DIRECTION_AND_ASSET_SPEC.md. Grading is
# multiplicative toward the target mean so local texture detail survives; a flat
# additive shift washes the material out and reads as painted card.
GRADE_STRENGTH = 0.75


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def load(path: Path) -> Image.Image:
    return Image.open(io.BytesIO(path.read_bytes()))


def to_gray(img: Image.Image) -> np.ndarray:
    return np.asarray(img.convert("L").resize((SIZE, SIZE), Image.LANCZOS)).astype(np.float32) / 255.0


def grade(rgb: np.ndarray, target_hex: str | None) -> np.ndarray:
    """Pull the image mean toward target while preserving contrast."""
    if target_hex is None:
        return rgb
    target = np.array([int(target_hex[i : i + 2], 16) for i in (1, 3, 5)], dtype=np.float32) / 255.0
    mean = rgb.reshape(-1, 3).mean(axis=0)
    mean = np.maximum(mean, 1e-4)
    gain = 1.0 + (target / mean - 1.0) * GRADE_STRENGTH
    return np.clip(rgb * gain, 0.0, 1.0)


def equalise_tile(rgb: np.ndarray, strength: float = 0.92) -> np.ndarray:
    """Flatten each tile's low-frequency luminance so repeats stop being visible.

    A photographic ground texture carries a large-scale bright or dark blotch. Tiled
    across a 256 m terrain that blotch is the tell: the eye locks onto it and the
    ground reads as a grid, which is exactly the "giant repeated texture pattern" this
    change is meant to remove. Dividing out a wrapped low-pass of the luminance leaves
    the fine grass and gravel detail intact but makes every repeat photometrically
    interchangeable, so all large-scale variation comes from the splatmap instead.

    The blur wraps, so the texture stays seamless.
    """
    lum = rgb @ np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)
    low = wrapped_blur(lum, SIZE // 8)
    ratio = np.maximum(lum.mean(), 1e-4) / np.maximum(low, 1e-4)
    ratio = 1.0 + (ratio - 1.0) * strength
    return np.clip(rgb * ratio[..., None], 0.0, 1.0)


def wrapped_blur(a: np.ndarray, radius: int) -> np.ndarray:
    """Separable box blur with wrap-around, run twice for a smoother kernel."""
    out = a.astype(np.float32)
    for _ in range(2):
        for axis in (0, 1):
            pad_width = [(0, 0), (0, 0)]
            pad_width[axis] = (radius, radius)
            cum = np.cumsum(np.pad(out, pad_width, mode="wrap"), axis=axis)
            lo = [slice(None), slice(None)]
            hi = [slice(None), slice(None)]
            hi[axis] = slice(2 * radius, None)
            lo[axis] = slice(None, -2 * radius)
            out = (cum[tuple(hi)] - cum[tuple(lo)]) / (2 * radius)
    return out


def save_png(arr: np.ndarray, path: Path) -> None:
    mode = "RGBA" if arr.shape[2] == 4 else "RGB"
    img = Image.fromarray((np.clip(arr, 0, 1) * 255.0 + 0.5).astype(np.uint8), mode)
    path.parent.mkdir(parents=True, exist_ok=True)
    img.save(path, format="PNG", optimize=True)


def meta(path: Path, *, srgb: bool, normal_map: bool, has_alpha: bool) -> None:
    """Write the Unity importer sidecar, matching the existing Surfaces meta format.

    The GUID is derived from the asset path so a re-run reproduces the same file and
    does not orphan references from the baked TerrainLayers.
    """
    rel = path.relative_to(ROOT / "game" / "Airside").as_posix()
    guid = uuid.UUID(hashlib.md5(f"airside:{rel}".encode()).hexdigest()).hex
    body = f"""fileFormatVersion: 2
guid: {guid}
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
    externalNormalMap: {1 if normal_map else 0}
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
  maxTextureSize: {SIZE}
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 4
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
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: {1 if has_alpha else 0}
  alphaIsTransparency: 0
  spriteTessellationDetail: -1
  textureType: {1 if normal_map else 0}
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
"""
    path.with_suffix(path.suffix + ".meta").write_text(body)


class AmbientCg:
    """Reads maps straight out of the downloaded ambientCG zip."""

    def __init__(self, asset_id: str):
        self.asset_id = asset_id
        self.zip_path = SRC / f"{asset_id}_2K-JPG.zip"
        self.zf = zipfile.ZipFile(self.zip_path)
        self.names = self.zf.namelist()

    def maybe(self, suffix: str) -> Image.Image | None:
        want = f"{self.asset_id}_2K-JPG_{suffix}.jpg"
        if want not in self.names:
            return None
        return Image.open(io.BytesIO(self.zf.read(want)))

    def need(self, suffix: str) -> Image.Image:
        img = self.maybe(suffix)
        if img is None:
            raise SystemExit(f"{self.zip_path.name} is missing {suffix}")
        return img


def ao_from_height(height: np.ndarray) -> np.ndarray:
    """Cheap concavity AO for sources that ship no AmbientOcclusion map.

    Ground003 has no AO map. Rather than write a flat white channel, approximate it
    from the displacement: pixels below their local average sit in a dip and are
    occluded. Recorded as derived in the asset register.
    """
    pad = np.pad(height, 8, mode="wrap")
    box = np.zeros_like(height)
    for dy in (-8, -4, 0, 4, 8):
        for dx in (-8, -4, 0, 4, 8):
            box += pad[8 + dy : 8 + dy + SIZE, 8 + dx : 8 + dx + SIZE]
    box /= 25.0
    return np.clip(0.5 + (height - box) * 2.5, 0.35, 1.0)


def build_terrain_layer(name: str, src, grade_target: str | None, records: list) -> None:
    color = np.asarray(src.need("Color").convert("RGB").resize((SIZE, SIZE), Image.LANCZOS)).astype(np.float32) / 255.0
    color = grade(equalise_tile(color), grade_target)

    normal = np.asarray(src.need("NormalGL").convert("RGB").resize((SIZE, SIZE), Image.LANCZOS)).astype(np.float32) / 255.0

    rough = to_gray(src.need("Roughness"))
    height = to_gray(src.need("Displacement"))
    ao_img = src.maybe("AmbientOcclusion")
    ao = to_gray(ao_img) if ao_img is not None else ao_from_height(height)

    # URP TerrainLayer mask map packing. Ground is dielectric, so metallic is 0.
    maskmap = np.stack([np.zeros_like(rough), ao, height, 1.0 - rough], axis=2)

    outputs = {
        "basecolor": (TERRAIN_OUT / f"tx_ground_{name}_basecolor_v01.png", color, dict(srgb=True, normal_map=False, has_alpha=False)),
        "normal": (TERRAIN_OUT / f"tx_ground_{name}_normal_v01.png", normal, dict(srgb=False, normal_map=True, has_alpha=False)),
        "maskmap": (TERRAIN_OUT / f"tx_ground_{name}_maskmap_v01.png", maskmap, dict(srgb=False, normal_map=False, has_alpha=True)),
    }
    for kind, (path, arr, opts) in outputs.items():
        save_png(arr, path)
        meta(path, **opts)
        records.append({"layer": name, "map": kind, "file": path.relative_to(ROOT).as_posix(), "sha256": sha256(path)})
    print(f"  {name}: basecolor/normal/maskmap  (AO {'source' if ao_img is not None else 'derived from displacement'})")


class PolyHaven:
    def __init__(self, asset: str):
        self.asset = asset

    def need(self, suffix: str) -> Image.Image:
        path = SRC / f"{self.asset}_{suffix}_2k.jpg"
        if not path.exists():
            raise SystemExit(f"missing {path}")
        return load(path)

    def maybe(self, suffix: str) -> Image.Image | None:
        path = SRC / f"{self.asset}_{suffix}_2k.jpg"
        return load(path) if path.exists() else None


def build_terrain_layer_polyhaven(name: str, asset: str, grade_target: str | None, records: list) -> None:
    ph = PolyHaven(asset)
    color = np.asarray(ph.need("diff").convert("RGB").resize((SIZE, SIZE), Image.LANCZOS)).astype(np.float32) / 255.0
    color = grade(equalise_tile(color), grade_target)
    normal = np.asarray(ph.need("nor_gl").convert("RGB").resize((SIZE, SIZE), Image.LANCZOS)).astype(np.float32) / 255.0
    rough = to_gray(ph.need("rough"))
    height = to_gray(ph.need("disp"))
    ao_img = ph.maybe("ao")
    ao = to_gray(ao_img) if ao_img is not None else ao_from_height(height)
    maskmap = np.stack([np.zeros_like(rough), ao, height, 1.0 - rough], axis=2)

    for kind, path, arr, opts in [
        ("basecolor", TERRAIN_OUT / f"tx_ground_{name}_basecolor_v01.png", color, dict(srgb=True, normal_map=False, has_alpha=False)),
        ("normal", TERRAIN_OUT / f"tx_ground_{name}_normal_v01.png", normal, dict(srgb=False, normal_map=True, has_alpha=False)),
        ("maskmap", TERRAIN_OUT / f"tx_ground_{name}_maskmap_v01.png", maskmap, dict(srgb=False, normal_map=False, has_alpha=True)),
    ]:
        save_png(arr, path)
        meta(path, **opts)
        records.append({"layer": name, "map": kind, "file": path.relative_to(ROOT).as_posix(), "sha256": sha256(path)})
    print(f"  {name}: basecolor/normal/maskmap  (AO {'source' if ao_img is not None else 'derived'})")


def build_apron_concrete_v03(records: list) -> None:
    """Worn concrete for the apron, in the existing Surfaces v03 convention.

    These are runtime-loaded through ArtRuntimePaths/StreamingAssets, unlike the
    Terrain layers, so they keep the basecolor/normal/ao/mask split and the
    _MetallicGlossMap packing that AirsideMaterialLibrary already binds.
    """
    ph = PolyHaven("worn_concrete_floor")
    color = np.asarray(ph.need("diff").convert("RGB").resize((SIZE, SIZE), Image.LANCZOS)).astype(np.float32) / 255.0
    # The apron tiles at roughly 4.7 x 4 m, so the source photograph's large stain
    # blotches would repeat several times across one stand. FREE_GROUND_SOLUTION is
    # explicit that stains must not be baked into every repeated concrete tile, so
    # the low-frequency luminance goes the same way as the terrain layers' and only
    # the fine crazing and grit detail is kept. Graded pale per the reference board.
    color = grade(equalise_tile(color, strength=0.85), "#B3B0A8")
    normal = np.asarray(ph.need("nor_gl").convert("RGB").resize((SIZE, SIZE), Image.LANCZOS)).astype(np.float32) / 255.0
    rough = to_gray(ph.need("rough"))
    ao = to_gray(ph.need("ao"))

    smooth = np.clip(1.0 - rough, 0.0, 1.0)
    mask = np.stack([np.zeros_like(smooth), np.zeros_like(smooth), np.zeros_like(smooth), smooth], axis=2)

    for kind, path, arr, opts in [
        ("basecolor", SURFACE_OUT / "tx_concrete_apron_basecolor_v03.png", color, dict(srgb=True, normal_map=False, has_alpha=False)),
        ("normal", SURFACE_OUT / "tx_concrete_apron_normal_v03.png", normal, dict(srgb=False, normal_map=True, has_alpha=False)),
        ("ao", SURFACE_OUT / "tx_concrete_apron_ao_v03.png", np.stack([ao] * 3, axis=2), dict(srgb=False, normal_map=False, has_alpha=False)),
        ("mask", SURFACE_OUT / "tx_concrete_apron_mask_v03.png", mask, dict(srgb=False, normal_map=False, has_alpha=True)),
    ]:
        save_png(arr, path)
        meta(path, **opts)
        records.append({"layer": "apron_concrete_v03", "map": kind, "file": path.relative_to(ROOT).as_posix(), "sha256": sha256(path)})
    print("  apron concrete v03: basecolor/normal/ao/mask")


def main() -> None:
    if not SRC.exists():
        raise SystemExit(f"missing source dir {SRC}; download the CC0 sets first")
    TERRAIN_OUT.mkdir(parents=True, exist_ok=True)

    records: list = []
    sources: list = []

    print("Terrain layers:")
    # Dominant dry grass, pulled toward the approved Dry Grass swatch rather than
    # the source's greener cast.
    build_terrain_layer("drygrass", AmbientCg("Ground013"), "#8A8A58", records)
    # Accent only. Graded toward eucalyptus rather than left as bright lawn green.
    # Green grass has to read as "darker irregular ground variation" against the dry
    # grass from the overview camera. An earlier target sat only 14 luminance points
    # below dry grass and in the same hue, so the two layers blended into one uniform
    # field and the splatmap's patches became invisible.
    build_terrain_layer("greengrass", AmbientCg("Ground003"), "#5C7040", records)
    build_terrain_layer("worndirt", AmbientCg("Ground030"), "#9A7B5A", records)
    build_terrain_layer_polyhaven("coastsand", "coast_sand_01", "#C8B286", records)

    print("Surfaces:")
    build_apron_concrete_v03(records)

    for name in ("Ground013", "Ground003", "Ground030"):
        p = SRC / f"{name}_2K-JPG.zip"
        sources.append({"source": name, "file": p.name, "sha256": sha256(p)})
    for asset in ("coast_sand_01", "worn_concrete_floor"):
        for m in ("diff", "nor_gl", "rough", "ao", "disp"):
            p = SRC / f"{asset}_{m}_2k.jpg"
            if p.exists():
                sources.append({"source": asset, "file": p.name, "sha256": sha256(p)})

    manifest = {"processed": records, "sources": sources, "runtime_size": SIZE}
    out = SRC / "ground-hashes.json"
    out.write_text(json.dumps(manifest, indent=2) + "\n")
    print(f"\n{len(records)} runtime maps written; hash manifest -> {out}")


if __name__ == "__main__":
    main()
