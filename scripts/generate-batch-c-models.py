#!/usr/bin/env python3
"""Generate Batch C metre-scale glTF kits and livery decal atlases for Airside."""

from __future__ import annotations

import importlib.util
import json
import uuid
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

# Repo-relative (was hard-coded to the /workspace container path, so this
# module could not be imported or re-run on a developer machine).
SCRIPTS = Path(__file__).resolve().parent
REPO = SCRIPTS.parent
ROOT = REPO / "game" / "Airside" / "Assets" / "Airside" / "Art"

# Loaded by path because the generators are hyphenated scripts, not a package.
_ATTR_SPEC = importlib.util.spec_from_file_location(
    "mesh_attributes", SCRIPTS / "mesh_attributes.py"
)
mesh_attributes = importlib.util.module_from_spec(_ATTR_SPEC)
assert _ATTR_SPEC.loader is not None
_ATTR_SPEC.loader.exec_module(mesh_attributes)


def new_guid() -> str:
    return uuid.uuid4().hex


def write_folder_meta(path: Path) -> None:
    path.mkdir(parents=True, exist_ok=True)
    meta = Path(str(path) + ".meta")
    if meta.exists():
        return
    meta.write_text(
        "fileFormatVersion: 2\n"
        f"guid: {new_guid()}\n"
        "folderAsset: yes\n"
        "DefaultImporter:\n"
        "  externalObjects: {}\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n",
        encoding="utf-8",
    )


def write_default_meta(path: Path) -> None:
    # Never reissue a GUID for an asset Unity already knows about: a regenerate
    # would silently break every reference to it. write_kit worked around this by
    # saving and restoring the .meta around pack_gltf, but generators that call
    # pack_gltf directly (generate-wld-prp-kits-v02.py) had no such guard.
    meta = Path(str(path) + ".meta")
    if meta.exists():
        return
    meta.write_text(
        "fileFormatVersion: 2\n"
        f"guid: {new_guid()}\n"
        "DefaultImporter:\n"
        "  externalObjects: {}\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n",
        encoding="utf-8",
    )


def write_texture_meta(png_path: Path) -> None:
    png_path.with_suffix(".png.meta").write_text(
        f"""fileFormatVersion: 2
guid: {new_guid()}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 1
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
  alphaUsage: 1
  alphaIsTransparency: 1
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
  sRGBTexture: 1
  platformSettings: []
  userData: 
  assetBundleName: 
  assetBundleVariant: 
""",
        encoding="utf-8",
    )


def box(cx: float, cy: float, cz: float, sx: float, sy: float, sz: float):
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
        (3, 7, 6, 2),
        (0, 3, 7, 4),
        (1, 5, 6, 2),
    ]
    verts: list = []
    indices: list = []
    for a, b, c, d in faces:
        base = len(verts)
        verts.extend([corners[a], corners[b], corners[c], corners[d]])
        indices.extend([base, base + 1, base + 2, base, base + 2, base + 3])
    return np.asarray(verts, np.float32), np.asarray(indices, np.uint16)


def pack_gltf(path: Path, meshes: dict[str, tuple[np.ndarray, np.ndarray]]) -> None:
    """Write a kit as glTF with a full vertex format.

    Generators hand in bare (positions, indices); normals, UVs and tangents are
    derived here by ``mesh_attributes`` so every kit in the project gains them
    from the one writer. See that module for what each pass does.
    """
    bin_parts: list[bytes] = []
    buffer_views, accessors, gltf_meshes, nodes = [], [], [], []
    offset = 0

    def add_accessor(data: np.ndarray, kind: str, target: int, with_bounds: bool) -> int:
        """Append one bufferView + accessor, keeping the 4-byte alignment glTF wants."""
        nonlocal offset
        raw = data.tobytes()
        padding = (4 - (len(raw) % 4)) % 4
        buffer_views.append(
            {
                "buffer": 0,
                "byteOffset": offset,
                "byteLength": len(raw),
                "target": target,
            }
        )
        bin_parts.append(raw + b"\x00" * padding)
        offset += len(raw) + padding

        component = {
            np.dtype(np.float32): 5126,
            np.dtype(np.uint16): 5123,
            np.dtype(np.uint32): 5125,
        }[data.dtype]
        accessor = {
            "bufferView": len(buffer_views) - 1,
            "componentType": component,
            "count": len(data),
            "type": kind,
        }
        if with_bounds:
            accessor["min"] = data.min(axis=0).tolist()
            accessor["max"] = data.max(axis=0).tolist()
        accessors.append(accessor)
        return len(accessors) - 1

    for name, (verts, indices) in meshes.items():
        pos, nrm, uv, tan, idx = mesh_attributes.build_attributes(verts, indices)
        if len(idx) == 0:
            continue
        # uint16 wherever it fits, as before; wider only when a split mesh needs it.
        idx = idx.astype(np.uint32 if len(pos) > 65535 else np.uint16)

        attributes = {
            "POSITION": add_accessor(pos, "VEC3", 34962, with_bounds=True),
            "NORMAL": add_accessor(nrm, "VEC3", 34962, with_bounds=False),
            "TEXCOORD_0": add_accessor(uv, "VEC2", 34962, with_bounds=False),
            "TANGENT": add_accessor(tan, "VEC4", 34962, with_bounds=False),
        }
        acc_i = add_accessor(idx, "SCALAR", 34963, with_bounds=False)

        mesh_index = len(gltf_meshes)
        gltf_meshes.append(
            {
                "name": name,
                "primitives": [{"attributes": attributes, "indices": acc_i, "mode": 4}],
            }
        )
        nodes.append({"name": name, "mesh": mesh_index})

    bin_path = path.with_suffix(".bin")
    blob = b"".join(bin_parts)
    bin_path.write_bytes(blob)
    doc = {
        "asset": {"version": "2.0", "generator": "Airside Batch C procedural kit"},
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


def save_livery(path: Path, primary, secondary, stripe) -> None:
    img = Image.new("RGBA", (1024, 1024), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    draw.rectangle([40, 200, 980, 520], fill=(*primary, 255))
    draw.rectangle([40, 360, 980, 420], fill=(*stripe, 255))
    draw.rectangle([40, 480, 980, 520], fill=(*secondary, 220))
    draw.polygon([(700, 40), (960, 40), (960, 190), (760, 190)], fill=(*primary, 255))
    draw.rectangle([760, 90, 960, 130], fill=(*stripe, 255))
    draw.rectangle([80, 600, 940, 680], fill=(*secondary, 200))
    draw.rectangle([80, 720, 940, 760], fill=(*stripe, 180))
    img.save(path, optimize=True)
    write_texture_meta(path)


def main() -> None:
    for folder in [
        ROOT / "Models" / "Aircraft",
        ROOT / "Models" / "Buildings",
        ROOT / "Models" / "Vehicles",
        ROOT / "Models" / "Props",
        ROOT / "Textures" / "Decals",
    ]:
        write_folder_meta(folder)

    pack_gltf(
        ROOT / "Models" / "Aircraft" / "mdl_regional_turboprop_01_v01.gltf",
        {
            "fuselage": box(0, 1.1, 0, 1.4, 1.4, 9.0),
            "nose": box(0, 1.1, 4.7, 1.1, 1.1, 1.2),
            "wing_left": box(-4.2, 1.05, 0.4, 6.5, 0.18, 1.8),
            "wing_right": box(4.2, 1.05, 0.4, 6.5, 0.18, 1.8),
            "engine_left": box(-2.4, 0.85, 1.0, 0.7, 0.7, 2.0),
            "engine_right": box(2.4, 0.85, 1.0, 0.7, 0.7, 2.0),
            "propeller_left": box(-2.4, 0.85, 2.15, 0.08, 2.2, 0.25),
            "propeller_right": box(2.4, 0.85, 2.15, 0.08, 2.2, 0.25),
            "tail_fin": box(0, 2.3, -3.8, 0.16, 2.0, 1.4),
            "tailplane": box(0, 1.7, -3.9, 3.2, 0.12, 0.9),
            "gear_nose": box(0, 0.35, 3.2, 0.15, 0.7, 0.4),
            "gear_left": box(-1.1, 0.3, -0.4, 0.15, 0.7, 0.5),
            "gear_right": box(1.1, 0.3, -0.4, 0.15, 0.7, 0.5),
            "door_fwd": box(-0.72, 1.1, 2.0, 0.08, 1.0, 1.2),
        },
    )

    save_livery(
        ROOT / "Textures" / "Decals" / "dc_livery_coastline_regional_v01.png",
        (57, 112, 138),
        (167, 201, 217),
        (242, 193, 75),
    )
    save_livery(
        ROOT / "Textures" / "Decals" / "dc_livery_emu_air_v01.png",
        (166, 110, 48),
        (200, 178, 134),
        (242, 193, 75),
    )
    save_livery(
        ROOT / "Textures" / "Decals" / "dc_livery_airside_traffic_v01.png",
        (154, 163, 162),
        (238, 241, 236),
        (57, 112, 138),
    )

    pack_gltf(
        ROOT / "Models" / "Buildings" / "mdl_terminal_regional_small_v01.gltf",
        {
            "terminal_body": box(0, 2.2, 0, 22, 4.4, 5),
            "glass_front": box(0, 2.4, 2.45, 17, 2.2, 0.12),
            "end_cap_left": box(-11.5, 2.0, 0, 1.2, 4.0, 5.2),
            "end_cap_right": box(11.5, 2.0, 0, 1.2, 4.0, 5.2),
            "service_wing": box(6, 1.4, -3.2, 8, 2.8, 3),
        },
    )
    pack_gltf(
        ROOT / "Models" / "Buildings" / "mdl_hangar_small_v01.gltf",
        {
            "hangar_shell": box(0, 2.5, 0, 14, 5, 9),
            "door_opening": box(0, 2.0, 4.6, 8, 4, 0.2),
            "roof_ridge": box(0, 5.1, 0, 14.2, 0.3, 1.2),
        },
    )
    pack_gltf(
        ROOT / "Models" / "Buildings" / "mdl_operations_shed_v01.gltf",
        {
            "shed_body": box(0, 1.4, 0, 6, 2.8, 4),
            "porch": box(0, 1.0, 2.3, 3, 2.0, 1.2),
        },
    )

    pack_gltf(
        ROOT / "Models" / "Vehicles" / "mdl_fuel_truck_small_v01.gltf",
        {
            "cab": box(1.0, 0.9, 0, 1.4, 1.4, 1.5),
            "tank": box(-0.4, 0.85, 0, 2.4, 1.2, 1.3),
            "wheel_fl": box(1.3, 0.28, 0.55, 0.35, 0.55, 0.2),
            "wheel_fr": box(1.3, 0.28, -0.55, 0.35, 0.55, 0.2),
            "wheel_rl": box(-1.1, 0.28, 0.55, 0.35, 0.55, 0.2),
            "wheel_rr": box(-1.1, 0.28, -0.55, 0.35, 0.55, 0.2),
            "hose_mount": box(-1.5, 0.7, 0.7, 0.35, 0.35, 0.35),
        },
    )
    pack_gltf(
        ROOT / "Models" / "Vehicles" / "mdl_baggage_tug_train_v01.gltf",
        {
            "tug": box(3.6, 0.55, 0, 1.6, 0.9, 1.1),
            "cart_1": box(1.8, 0.45, 0, 1.5, 0.7, 1.0),
            "cart_2": box(0.2, 0.45, 0, 1.5, 0.7, 1.0),
            "cart_3": box(-1.4, 0.45, 0, 1.5, 0.7, 1.0),
            "hitch_1": box(2.7, 0.35, 0, 0.4, 0.2, 0.2),
            "hitch_2": box(1.0, 0.35, 0, 0.4, 0.2, 0.2),
            "hitch_3": box(-0.6, 0.35, 0, 0.4, 0.2, 0.2),
        },
    )
    pack_gltf(
        ROOT / "Models" / "Vehicles" / "mdl_passenger_bus_apron_v01.gltf",
        {
            "bus_body": box(0, 0.95, 0, 4.2, 1.6, 1.6),
            "cabin_roof": box(0, 1.85, 0, 4.0, 0.25, 1.5),
            "door": box(0.2, 0.9, 0.82, 1.0, 1.3, 0.08),
            "wheel_fl": box(1.4, 0.3, 0.7, 0.4, 0.55, 0.22),
            "wheel_fr": box(1.4, 0.3, -0.7, 0.4, 0.55, 0.22),
            "wheel_rl": box(-1.4, 0.3, 0.7, 0.4, 0.55, 0.22),
            "wheel_rr": box(-1.4, 0.3, -0.7, 0.4, 0.55, 0.22),
        },
    )
    pack_gltf(
        ROOT / "Models" / "Props" / "mdl_service_equipment_kit_v01.gltf",
        {
            "stairs": box(0, 0.9, 0, 1.2, 1.8, 2.4),
            "chock_a": box(-0.4, 0.12, 0, 0.35, 0.24, 0.2),
            "chock_b": box(0.4, 0.12, 0, 0.35, 0.24, 0.2),
            "cone": box(1.2, 0.35, 0, 0.3, 0.7, 0.3),
            "towbar": box(0, 0.2, -1.5, 2.5, 0.12, 0.12),
            "bin": box(-1.2, 0.45, 0.8, 0.7, 0.9, 0.7),
            "gpu": box(1.5, 0.5, -1.0, 1.1, 1.0, 0.8),
        },
    )

    print("Batch C written under", ROOT)


if __name__ == "__main__":
    main()
