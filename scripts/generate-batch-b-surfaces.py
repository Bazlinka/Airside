#!/usr/bin/env python3
"""Generate Batch B surface textures and simple glTF world kits for Airside."""

from __future__ import annotations

import json
import struct
import uuid
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path("/workspace/game/Airside/Assets/Airside/Art")
SIZE = 2048
RNG = np.random.default_rng(20260906)


def new_guid() -> str:
    return uuid.uuid4().hex


def write_folder_meta(path: Path, guid: str | None = None) -> str:
    path.mkdir(parents=True, exist_ok=True)
    meta = path.with_suffix(path.suffix + ".meta") if path.suffix else Path(str(path) + ".meta")
    # Folder metas are path.meta next to the folder
    meta = Path(str(path) + ".meta")
    g = guid or new_guid()
    if not meta.exists():
        meta.write_text(
            "fileFormatVersion: 2\n"
            f"guid: {g}\n"
            "folderAsset: yes\n"
            "DefaultImporter:\n"
            "  externalObjects: {}\n"
            "  userData: \n"
            "  assetBundleName: \n"
            "  assetBundleVariant: \n",
            encoding="utf-8",
        )
    else:
        for line in meta.read_text(encoding="utf-8").splitlines():
            if line.startswith("guid:"):
                return line.split(":", 1)[1].strip()
    return g


def write_texture_meta(png_path: Path, *, srgb: bool, alpha: bool) -> str:
    g = new_guid()
    # Unity TextureImporter: sRGB for basecolour; Non-Color for masks/data
    color_space = 1 if srgb else 0
    alpha_usage = 1 if alpha else 0
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
    externalNormalMap: 0
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
  maxTextureSize: 2048
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
  alphaUsage: {alpha_usage}
  alphaIsTransparency: {1 if alpha else 0}
  spriteTessellationDetail: -1
  textureType: 0
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
    return g


def write_default_meta(path: Path) -> str:
    g = new_guid()
    meta = Path(str(path) + ".meta")
    meta.write_text(
        "fileFormatVersion: 2\n"
        f"guid: {g}\n"
        "DefaultImporter:\n"
        "  externalObjects: {}\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n",
        encoding="utf-8",
    )
    return g


def wrap_noise(shape: tuple[int, int], scale: float, octaves: int = 4) -> np.ndarray:
    """Tileable value noise via FFT-friendly wrapped grids."""
    h, w = shape
    acc = np.zeros((h, w), dtype=np.float64)
    amp = 1.0
    total = 0.0
    for o in range(octaves):
        # Grid size must divide texture for perfect wrap
        cells = max(2, int(round(scale * (2**o))))
        while h % cells != 0:
            cells -= 1
            if cells < 2:
                cells = 2
                break
        gh = cells
        gw = cells
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


def hex_to_rgb(h: str) -> np.ndarray:
    h = h.lstrip("#")
    return np.array([int(h[i : i + 2], 16) for i in (0, 2, 4)], dtype=np.float64)


def save_rgb(path: Path, rgb: np.ndarray, *, srgb: bool = True) -> str:
    rgb_u8 = np.clip(rgb, 0, 255).astype(np.uint8)
    Image.fromarray(rgb_u8, mode="RGB").save(path, optimize=True)
    return write_texture_meta(path, srgb=srgb, alpha=False)


def save_rgba(path: Path, rgba: np.ndarray, *, srgb: bool = True) -> str:
    rgba_u8 = np.clip(rgba, 0, 255).astype(np.uint8)
    Image.fromarray(rgba_u8, mode="RGBA").save(path, optimize=True)
    return write_texture_meta(path, srgb=srgb, alpha=True)


def make_asphalt() -> np.ndarray:
    base = hex_to_rgb("#343B40")
    n = wrap_noise((SIZE, SIZE), scale=8, octaves=5)
    fine = wrap_noise((SIZE, SIZE), scale=64, octaves=3)
    mix = 0.55 * n + 0.45 * fine
    mix = (mix - mix.mean()) / (mix.std() + 1e-6)
    rgb = base + mix[..., None] * np.array([10.0, 10.0, 11.0])
    # sparse brighter aggregate flecks
    fleck = wrap_noise((SIZE, SIZE), scale=128, octaves=1)
    rgb += (fleck > 0.82)[..., None] * np.array([8.0, 8.0, 7.0])
    return rgb


def make_concrete() -> np.ndarray:
    base = hex_to_rgb("#9CA3A2")
    n = wrap_noise((SIZE, SIZE), scale=4, octaves=4)
    mix = (n - n.mean()) / (n.std() + 1e-6)
    rgb = base + mix[..., None] * np.array([14.0, 13.0, 12.0])
    return rgb


def make_grass() -> np.ndarray:
    dry = hex_to_rgb("#8A8A58")
    euc = hex_to_rgb("#4F6F60")
    n = wrap_noise((SIZE, SIZE), scale=16, octaves=5)
    blend = np.clip((n - 0.35) / 0.4, 0, 1)[..., None]
    rgb = dry * (1 - blend) + euc * blend
    detail = wrap_noise((SIZE, SIZE), scale=96, octaves=2)
    d = (detail - detail.mean()) / (detail.std() + 1e-6)
    rgb = rgb + d[..., None] * 8.0
    return rgb


def make_corrugated() -> np.ndarray:
    base = hex_to_rgb("#9CA3A2") * 0.85
    x = np.arange(SIZE)[None, :]
    # vertical corrugation ridges
    wave = 0.5 + 0.5 * np.sin(2 * np.pi * x * 48 / SIZE)
    wave = np.repeat(wave, SIZE, axis=0)
    n = wrap_noise((SIZE, SIZE), scale=6, octaves=3)
    n = (n - n.mean()) / (n.std() + 1e-6)
    shade = 0.78 + 0.28 * wave + 0.04 * n
    rgb = base * shade[..., None]
    return rgb


def make_glass_mask() -> np.ndarray:
    # Greyscale window variation mask (non-colour intent; still saved RGB for Unity)
    rgb = np.zeros((SIZE, SIZE, 3), dtype=np.float64)
    pane_w, pane_h = 128, 192
    for y in range(0, SIZE, pane_h):
        for x in range(0, SIZE, pane_w):
            val = 40 + int(RNG.integers(0, 80))
            rgb[y : y + pane_h - 8, x : x + pane_w - 8] = val
            # mullions
            rgb[y : y + pane_h, x : x + 6] = 200
            rgb[y : y + 6, x : x + pane_w] = 200
    return rgb


def make_runway_wear() -> np.ndarray:
    rgba = np.zeros((SIZE, SIZE, 4), dtype=np.float64)
    # central rubber streak band
    cy = SIZE // 2
    yy = np.arange(SIZE)[:, None]
    xx = np.arange(SIZE)[None, :]
    band = np.exp(-0.5 * ((yy - cy) / 90.0) ** 2)
    n = wrap_noise((SIZE, SIZE), scale=20, octaves=4)
    alpha = np.clip(band * (0.15 + 0.35 * n) * 255.0, 0, 90)
    # dark rubber colour
    rgba[..., 0] = 30
    rgba[..., 1] = 30
    rgba[..., 2] = 32
    rgba[..., 3] = alpha * (0.4 + 0.6 * (xx / SIZE))  # uneven along length
    # soft edge falloff left/right already via noise; clamp
    rgba[..., 3] = np.clip(rgba[..., 3], 0, 100)
    return rgba


def make_apron_stains() -> np.ndarray:
    rgba = np.zeros((SIZE, SIZE, 4), dtype=np.float64)
    n = wrap_noise((SIZE, SIZE), scale=10, octaves=4)
    spots = wrap_noise((SIZE, SIZE), scale=5, octaves=2)
    mask = (spots > 0.72) * (0.2 + 0.5 * n)
    rgba[..., 0] = 55
    rgba[..., 1] = 48
    rgba[..., 2] = 40
    rgba[..., 3] = np.clip(mask * 140.0, 0, 110)
    return rgba


def pack_gltf(path: Path, meshes: dict[str, tuple[np.ndarray, np.ndarray]]) -> None:
    """Write a minimal glTF 2.0 (.gltf + .bin) with named meshes (metres)."""
    bin_parts: list[bytes] = []
    buffer_views = []
    accessors = []
    gltf_meshes = []
    nodes = []
    offset = 0

    for name, (verts, indices) in meshes.items():
        v = np.asarray(verts, dtype=np.float32)
        i = np.asarray(indices, dtype=np.uint16)
        v_bytes = v.tobytes()
        # pad to 4
        pad = (4 - (len(v_bytes) % 4)) % 4
        v_bytes += b"\x00" * pad
        i_bytes = i.tobytes()
        pad_i = (4 - (len(i_bytes) % 4)) % 4
        i_bytes += b"\x00" * pad_i

        vmin = v.min(axis=0).tolist()
        vmax = v.max(axis=0).tolist()

        bv_v = len(buffer_views)
        buffer_views.append(
            {"buffer": 0, "byteOffset": offset, "byteLength": len(v_bytes) - pad, "target": 34962}
        )
        # store full padded length in buffer sequentially
        bin_parts.append(v_bytes)
        acc_v = len(accessors)
        accessors.append(
            {
                "bufferView": bv_v,
                "componentType": 5126,
                "count": len(v),
                "type": "VEC3",
                "max": vmax,
                "min": vmin,
            }
        )
        offset += len(v_bytes)

        bv_i = len(buffer_views)
        buffer_views.append(
            {"buffer": 0, "byteOffset": offset, "byteLength": len(i_bytes) - pad_i, "target": 34963}
        )
        bin_parts.append(i_bytes)
        acc_i = len(accessors)
        accessors.append(
            {
                "bufferView": bv_i,
                "componentType": 5123,
                "count": len(i),
                "type": "SCALAR",
            }
        )
        offset += len(i_bytes)

        mesh_index = len(gltf_meshes)
        gltf_meshes.append(
            {
                "name": name,
                "primitives": [{"attributes": {"POSITION": acc_v}, "indices": acc_i, "mode": 4}],
            }
        )
        nodes.append({"name": name, "mesh": mesh_index})

    bin_path = path.with_suffix(".bin")
    blob = b"".join(bin_parts)
    bin_path.write_bytes(blob)

    doc = {
        "asset": {"version": "2.0", "generator": "Airside Batch B procedural kit"},
        "buffers": [{"uri": bin_path.name, "byteLength": len(blob)}],
        "bufferViews": buffer_views,
        "accessors": accessors,
        "meshes": gltf_meshes,
        "nodes": nodes,
        "scenes": [{"name": path.stem, "nodes": list(range(len(nodes)))}],
        "scene": 0,
    }
    path.write_text(json.dumps(doc, indent=2), encoding="utf-8")
    write_default_meta(path)
    write_default_meta(bin_path)


def quad(x0, z0, x1, z1, y=0.01):
    verts = np.array(
        [
            [x0, y, z0],
            [x1, y, z0],
            [x1, y, z1],
            [x0, y, z1],
        ],
        dtype=np.float32,
    )
    indices = np.array([0, 1, 2, 0, 2, 3], dtype=np.uint16)
    return verts, indices


def box(cx, cy, cz, sx, sy, sz):
    x0, x1 = cx - sx / 2, cx + sx / 2
    y0, y1 = cy - sy / 2, cy + sy / 2
    z0, z1 = cz - sz / 2, cz + sz / 2
    corners = np.array(
        [
            [x0, y0, z0],
            [x1, y0, z0],
            [x1, y1, z0],
            [x0, y1, z0],
            [x0, y0, z1],
            [x1, y0, z1],
            [x1, y1, z1],
            [x0, y1, z1],
        ],
        dtype=np.float32,
    )
    faces = [
        (0, 1, 2, 3),
        (4, 7, 6, 5),
        (0, 4, 5, 1),
        (2, 6, 7, 3),
        (0, 3, 7, 4),
        (1, 5, 6, 2),
    ]
    verts = []
    indices = []
    for a, b, c, d in faces:
        base = len(verts)
        verts.extend([corners[a], corners[b], corners[c], corners[d]])
        indices.extend([base, base + 1, base + 2, base, base + 2, base + 3])
    return np.array(verts, dtype=np.float32), np.array(indices, dtype=np.uint16)


def main() -> None:
    # Folders
    folders = [
        ROOT,
        ROOT / "Textures",
        ROOT / "Textures" / "Surfaces",
        ROOT / "Textures" / "Environment",
        ROOT / "Textures" / "Decals",
        ROOT / "Materials",
        ROOT / "Models",
        ROOT / "Models" / "Props",
        ROOT / "Models" / "Aircraft",
        ROOT / "Models" / "Buildings",
        ROOT / "Models" / "Vehicles",
        ROOT / "Animation",
        ROOT / "Animation" / "Aircraft",
        ROOT / "Animation" / "Vehicles",
        ROOT / "Animation" / "World",
        ROOT / "Brand",
        ROOT / "UI",
        ROOT / "UI" / "Icons",
        ROOT / "UI" / "Illustrations",
        ROOT / "UI" / "Panels",
        ROOT / "VFX",
    ]
    for f in folders:
        write_folder_meta(f)

    guids = {}
    surfaces = ROOT / "Textures" / "Surfaces"
    guids["TEX-SRF-001"] = save_rgb(surfaces / "tx_asphalt_runway_basecolor_v01.png", make_asphalt())
    guids["TEX-SRF-002"] = save_rgb(surfaces / "tx_concrete_apron_basecolor_v01.png", make_concrete())
    guids["TEX-SRF-003"] = save_rgb(surfaces / "tx_grass_kingscote_basecolor_v01.png", make_grass())
    guids["TEX-SRF-004"] = save_rgb(surfaces / "tx_corrugated_metal_basecolor_v01.png", make_corrugated())

    env = ROOT / "Textures" / "Environment"
    guids["TEX-ENV-001"] = save_rgb(
        env / "tx_terminal_glass_mask_v01.png", make_glass_mask(), srgb=False
    )

    decals = ROOT / "Textures" / "Decals"
    guids["TEX-DEC-001"] = save_rgba(decals / "dc_runway_wear_v01.png", make_runway_wear())
    guids["TEX-DEC-002"] = save_rgba(decals / "dc_apron_stains_v01.png", make_apron_stains())

    props = ROOT / "Models" / "Props"

    # Markings kit — flat precision strips (metres)
    markings = {
        "runway_centreline": quad(-0.15, -30, 0.15, 30, 0.02),
        "runway_edge_left": quad(-15.0, -30, -14.7, 30, 0.02),
        "runway_edge_right": quad(14.7, -30, 15.0, 30, 0.02),
        "runway_threshold": quad(-15.0, -30, 15.0, -28.5, 0.025),
        "taxi_centreline": quad(-20.0, -0.1, 0.0, 0.1, 0.02),
        "stand_stop_a": quad(-8.0, 8.0, -4.0, 8.3, 0.03),
        "stand_stop_b": quad(4.0, 8.0, 8.0, 8.3, 0.03),
    }
    pack_gltf(props / "mdl_airfield_markings_kit_v01.gltf", markings)
    guids["WLD-001"] = "gltf"

    lighting = {
        "runway_edge_light": box(0, 0.15, 0, 0.12, 0.3, 0.12),
        "taxiway_light": box(0, 0.12, 0, 0.1, 0.24, 0.1),
        "apron_floodlight": box(0, 2.0, 0, 0.25, 4.0, 0.25),
        "obstruction_light": box(0, 0.4, 0, 0.15, 0.8, 0.15),
    }
    pack_gltf(props / "mdl_airfield_lighting_kit_v01.gltf", lighting)
    guids["WLD-002"] = "gltf"

    airfield_props = {
        "windsock_pole": box(0, 1.5, 0, 0.08, 3.0, 0.08),
        "cone": box(0, 0.25, 0, 0.3, 0.5, 0.3),
        "barrier": box(0, 0.5, 0, 1.5, 1.0, 0.08),
        "sign_board": box(0, 1.0, 0, 1.2, 0.8, 0.06),
        "baggage_dolly": box(0, 0.35, 0, 1.4, 0.5, 0.8),
    }
    pack_gltf(props / "mdl_airfield_props_kit_v01.gltf", airfield_props)
    guids["WLD-003"] = "gltf"

    # MAT-001 deferred marker
    mat_note = ROOT / "Materials" / "README_mat_airfield_surface_library_v01.md"
    mat_note.write_text(
        "# MAT-001 deferred\n\n"
        "Create `mat_airfield_surface_library_v01.mat` in Unity 6.3 LTS after import,\n"
        "linking TEX-SRF-001…004, painted-line colour, glass and metal slots.\n"
        "Do not invent shader GUIDs outside the Editor.\n",
        encoding="utf-8",
    )
    write_default_meta(mat_note)

    out = Path("/workspace/work/batch-b/guids.json")
    out.write_text(json.dumps(guids, indent=2), encoding="utf-8")
    print("Wrote Batch B assets under", ROOT)
    print(json.dumps(guids, indent=2))


if __name__ == "__main__":
    main()
