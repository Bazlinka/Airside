#!/usr/bin/env python3
"""Generate Batch F3 setting modules (FBX + companion glTF).

Decision 0027 / Batch F packet — F3 setting read:
  VEG-001  mdl_eucalyptus_kit_v01
  VEG-002  mdl_kingscote_scrub_kit_v01
  PRP-002  mdl_airfield_fence_gate_kit_v01
  PRP-003  mdl_terminal_forecourt_kit_v01
  WLD-004  mdl_kingscote_context_terrain_v01

Authored replacements for procedural trees/scrub/fence/forecourt/context.
ASCII FBX when assimp unavailable. Does not move operational geometry.
"""

from __future__ import annotations

import importlib.util
import subprocess
import tempfile
import uuid
from pathlib import Path

import numpy as np

REPO = Path(__file__).resolve().parents[1]
ROOT = REPO / "game/Airside/Assets/Airside/Art"
ENV = ROOT / "Models" / "Environment"
PROPS = ROOT / "Models" / "Props"

_batch_spec = importlib.util.spec_from_file_location(
    "batch_c_v01", REPO / "scripts/generate-batch-c-models.py"
)
_batch = importlib.util.module_from_spec(_batch_spec)
assert _batch_spec.loader is not None
_batch_spec.loader.exec_module(_batch)
_batch.ROOT = ROOT

_ascii_spec = importlib.util.spec_from_file_location(
    "ascii_fbx", REPO / "scripts/write_ascii_fbx.py"
)
_ascii = importlib.util.module_from_spec(_ascii_spec)
assert _ascii_spec.loader is not None
_ascii_spec.loader.exec_module(_ascii)

box = _batch.box
pack_gltf = _batch.pack_gltf
write_default_meta = _batch.write_default_meta


def new_guid() -> str:
    return uuid.uuid4().hex


def cylinder(
    cx: float, cy: float, cz: float, radius: float, length: float,
    *, axis: str = "y", segments: int = 12,
) -> tuple[np.ndarray, np.ndarray]:
    segs = max(6, segments)
    rings = []
    if axis == "y":
        y0, y1 = cy - length * 0.5, cy + length * 0.5
        for y in (y0, y1):
            ring = [[cx + radius * np.cos(2 * np.pi * i / segs), y, cz + radius * np.sin(2 * np.pi * i / segs)] for i in range(segs)]
            rings.append(np.asarray(ring, np.float32))
    elif axis == "z":
        z0, z1 = cz - length * 0.5, cz + length * 0.5
        for z in (z0, z1):
            ring = [[cx + radius * np.cos(2 * np.pi * i / segs), cy + radius * np.sin(2 * np.pi * i / segs), z] for i in range(segs)]
            rings.append(np.asarray(ring, np.float32))
    else:
        x0, x1 = cx - length * 0.5, cx + length * 0.5
        for x in (x0, x1):
            ring = [[x, cy + radius * np.sin(2 * np.pi * i / segs), cz + radius * np.cos(2 * np.pi * i / segs)] for i in range(segs)]
            rings.append(np.asarray(ring, np.float32))
    verts: list = []
    indices: list = []
    for i in range(segs):
        j = (i + 1) % segs
        a, b, c, d = rings[0][i], rings[0][j], rings[1][j], rings[1][i]
        base = len(verts)
        verts.extend([a, b, c, d])
        indices.extend([base, base + 1, base + 2, base, base + 2, base + 3])
    for ring in (rings[0], rings[1]):
        tip = ring.mean(axis=0)
        for i in range(segs):
            j = (i + 1) % segs
            base = len(verts)
            verts.extend([tip, ring[i], ring[j]])
            indices.extend([base, base + 1, base + 2])
    return np.asarray(verts, np.float32), np.asarray(indices, np.uint16)


def ellipsoid(cx, cy, cz, rx, ry, rz, *, segments: int = 10) -> tuple[np.ndarray, np.ndarray]:
    segs = max(6, segments)
    rings = []
    for iy in range(segs + 1):
        v = iy / segs
        y = cy + ry * np.cos(np.pi * v)
        r_scale = np.sin(np.pi * v)
        ring = []
        for ix in range(segs):
            u = 2 * np.pi * ix / segs
            ring.append([cx + rx * r_scale * np.cos(u), y, cz + rz * r_scale * np.sin(u)])
        rings.append(np.asarray(ring, np.float32))
    verts: list = []
    indices: list = []
    for r in range(len(rings) - 1):
        for i in range(segs):
            j = (i + 1) % segs
            a, b = rings[r][i], rings[r][j]
            c, d = rings[r + 1][j], rings[r + 1][i]
            base = len(verts)
            verts.extend([a, b, c, d])
            indices.extend([base, base + 1, base + 2, base, base + 2, base + 3])
    return np.asarray(verts, np.float32), np.asarray(indices, np.uint16)


def write_fbx_model_meta(path: Path) -> None:
    meta_path = Path(str(path) + ".meta")
    if meta_path.exists():
        return
    meta_path.write_text(
        f"""fileFormatVersion: 2
guid: {new_guid()}
ModelImporter:
  serializedVersion: 22200
  internalIDToNameTable: []
  externalObjects: {{}}
  materials:
    materialImportMode: 1
    materialName: 0
    materialSearch: 1
    materialLocation: 1
  animations:
    legacyGenerateAnimations: 4
    bakeSimulation: 0
    resampleCurves: 1
    optimizeGameObjects: 0
    motionNodeName: 
    animationImportErrors: 
    animationImportWarnings: 
    animationRetargetingWarnings: 
    animationDoRetargetingWarnings: 0
    importAnimatedCustomProperties: 0
    importConstraints: 0
    animationCompression: 1
    animationRotationError: 0.5
    animationPositionError: 0.5
    animationScaleError: 0.5
    animationWrapMode: 0
    extraExposedTransformPaths: []
    extraUserProperties: []
    clipAnimations: []
    isReadable: 1
  meshes:
    lODScreenPercentages: []
    globalScale: 1
    meshCompression: 0
    addColliders: 0
    useFileUnits: 1
    keepQuads: 0
    weldVertices: 1
    bakeAxisConversion: 0
    secondaryUVAngleDistortion: 8
    secondaryUVAreaDistortion: 15.000001
    secondaryUVHardAngle: 88
    secondaryUVMarginMethod: 1
    secondaryUVMinLightmapResolution: 40
    secondaryUVMinObjectScale: 1
    secondaryUVPackMargin: 4
    useFileScale: 1
  tangentSpace:
    normalSmoothAngle: 60
    normalImportMode: 0
    tangentImportMode: 3
    normalCalculationMode: 4
    legacyComputeAllNormalsFromSmoothingGroupsWhenMeshHasBlendShapes: 0
    blendShapeNormalImportMode: 1
    normalSmoothingSource: 0
  referencedClips: []
  importAnimation: 0
  humanoidOversampling: 1
  additionalBone: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
""",
        encoding="utf-8",
    )


def write_obj(path: Path, meshes: dict) -> None:
    lines = ["# Airside Batch F3"]
    v_offset = 1
    for name, (verts, indices) in meshes.items():
        lines.append(f"o {name}")
        for v in verts:
            lines.append(f"v {v[0]:.6f} {v[1]:.6f} {v[2]:.6f}")
        for i in range(0, len(indices), 3):
            a, b, c = int(indices[i]) + v_offset, int(indices[i + 1]) + v_offset, int(indices[i + 2]) + v_offset
            lines.append(f"f {a} {b} {c}")
        v_offset += len(verts)
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def export_fbx(meshes: dict, fbx_path: Path) -> None:
    assimp = subprocess.run(["which", "assimp"], capture_output=True, text=True)
    if assimp.returncode == 0 and assimp.stdout.strip():
        with tempfile.TemporaryDirectory() as tmp:
            obj_path = Path(tmp) / (fbx_path.stem + ".obj")
            write_obj(obj_path, meshes)
            result = subprocess.run(
                ["assimp", "export", str(obj_path), str(fbx_path)],
                capture_output=True, text=True, check=False,
            )
            if result.returncode == 0 and fbx_path.exists():
                write_fbx_model_meta(fbx_path)
                return
    _ascii.write_ascii_fbx(fbx_path, meshes)
    write_fbx_model_meta(fbx_path)


def write_kit(folder: Path, basename: str, meshes: dict) -> None:
    folder.mkdir(parents=True, exist_ok=True)
    gltf = folder / f"{basename}.gltf"
    fbx = folder / f"{basename}.fbx"
    bin_path = gltf.with_suffix(".bin")
    preserved = {}
    for p in (gltf, bin_path, fbx):
        meta = Path(str(p) + ".meta")
        if meta.exists():
            preserved[meta] = meta.read_text(encoding="utf-8")
    pack_gltf(gltf, meshes)
    export_fbx(meshes, fbx)
    for meta, text in preserved.items():
        meta.write_text(text, encoding="utf-8")
    if not Path(str(gltf) + ".meta").exists():
        write_default_meta(gltf)
    if not Path(str(bin_path) + ".meta").exists():
        write_default_meta(bin_path)
    print(f"Wrote {basename} ({len(meshes)} meshes)")


# --- VEG-001 -----------------------------------------------------------------


def eucalyptus_kit() -> dict:
    """Three silhouettes at origin; PlaceTree picks tree_a/b/c by hash."""
    meshes: dict = {}

    def add_tree(prefix: str, trunk_h: float, lean: float, canopy_scale: float) -> None:
        meshes[f"{prefix}_trunk"] = cylinder(0, trunk_h * 0.5, 0, 0.11, trunk_h, segments=10)
        meshes[f"{prefix}_flare"] = cylinder(0, 0.1, 0, 0.22, 0.18, segments=10)
        meshes[f"{prefix}_bark_low"] = box(0, trunk_h * 0.28, 0, 0.26, 0.08, 0.26)
        meshes[f"{prefix}_bark_mid"] = box(0, trunk_h * 0.55, 0, 0.24, 0.07, 0.24)
        meshes[f"{prefix}_fork"] = cylinder(0.22 + lean * 0.02, trunk_h * 0.78, -0.12, 0.06, trunk_h * 0.28, segments=8)
        meshes[f"{prefix}_canopy"] = ellipsoid(0, trunk_h + 0.15, 0, 0.95 * canopy_scale, 0.7 * canopy_scale, 0.9 * canopy_scale, segments=10)
        meshes[f"{prefix}_canopy_b"] = ellipsoid(0.55 * canopy_scale, trunk_h - 0.35, -0.35 * canopy_scale, 0.65 * canopy_scale, 0.5 * canopy_scale, 0.6 * canopy_scale, segments=8)
        meshes[f"{prefix}_canopy_c"] = ellipsoid(-0.45 * canopy_scale, trunk_h - 0.25, 0.4 * canopy_scale, 0.55 * canopy_scale, 0.45 * canopy_scale, 0.52 * canopy_scale, segments=8)
        meshes[f"{prefix}_canopy_d"] = ellipsoid(0.25 * canopy_scale, trunk_h + 0.35, 0.25 * canopy_scale, 0.45 * canopy_scale, 0.38 * canopy_scale, 0.42 * canopy_scale, segments=8)
        meshes[f"{prefix}_lod1"] = ellipsoid(0, trunk_h * 0.85, 0, 1.1 * canopy_scale, 1.4 * canopy_scale, 1.0 * canopy_scale, segments=6)

    add_tree("tree_a", 3.1, 2.0, 1.0)
    add_tree("tree_b", 2.6, -3.0, 0.9)
    add_tree("tree_c", 3.5, 1.0, 1.15)
    return meshes


# --- VEG-002 -----------------------------------------------------------------


def scrub_kit() -> dict:
    meshes: dict = {}
    variants = [
        ("scrub_a", 1.0, 0.0),
        ("scrub_b", 0.85, 0.2),
        ("scrub_c", 1.1, -0.15),
        ("scrub_d", 0.75, 0.1),
        ("scrub_e", 0.95, -0.25),
    ]
    for prefix, s, ox in variants:
        meshes[f"{prefix}_core"] = ellipsoid(ox, 0.38 * s, 0, 0.65 * s, 0.4 * s, 0.55 * s, segments=8)
        meshes[f"{prefix}_side"] = ellipsoid(ox + 0.4 * s, 0.32 * s, -0.25 * s, 0.45 * s, 0.3 * s, 0.4 * s, segments=7)
        meshes[f"{prefix}_side_b"] = ellipsoid(ox - 0.35 * s, 0.3 * s, 0.2 * s, 0.4 * s, 0.28 * s, 0.35 * s, segments=7)
        meshes[f"{prefix}_tuft"] = ellipsoid(ox + 0.1 * s, 0.55 * s, 0.15 * s, 0.28 * s, 0.22 * s, 0.25 * s, segments=6)
    meshes["rock_a"] = ellipsoid(0, 0.18, 0, 0.35, 0.2, 0.28, segments=8)
    meshes["rock_b"] = ellipsoid(0, 0.14, 0, 0.28, 0.15, 0.22, segments=7)
    meshes["grass_tuft_a"] = box(0, 0.2, 0, 0.35, 0.4, 0.12)
    meshes["grass_tuft_b"] = box(0, 0.18, 0, 0.28, 0.35, 0.1)
    return meshes


# --- PRP-002 -----------------------------------------------------------------


def fence_gate_kit() -> dict:
    """Modular panels — thin mesh slabs stand in for chain-link cutout."""
    meshes: dict = {
        "fence_bay": box(0, 0.7, 0, 4.0, 1.35, 0.06),
        "fence_bay_rail_top": box(0, 1.3, 0, 4.0, 0.06, 0.08),
        "fence_bay_rail_mid": box(0, 0.7, 0, 4.0, 0.06, 0.08),
        "fence_bay_rail_bot": box(0, 0.22, 0, 4.0, 0.06, 0.08),
        "fence_bay_post_l": box(-2.0, 0.7, 0, 0.12, 1.4, 0.12),
        "fence_bay_post_r": box(2.0, 0.7, 0, 0.12, 1.4, 0.12),
        "fence_bay_cap_l": box(-2.0, 1.42, 0, 0.16, 0.08, 0.16),
        "fence_bay_cap_r": box(2.0, 1.42, 0, 0.16, 0.08, 0.16),
        "fence_corner": box(0, 0.7, 0, 0.18, 1.45, 0.18),
        "fence_corner_brace": box(0.5, 0.7, 0.5, 1.2, 0.08, 0.08),
        "fence_end": box(0, 0.7, 0, 0.16, 1.4, 0.16),
        "fence_end_brace": box(0.4, 0.7, 0, 0.9, 0.08, 0.08),
        "gate_vehicle_leaf_l": box(0, 0.85, 0, 2.2, 1.5, 0.08),
        "gate_vehicle_leaf_r": box(0, 0.85, 0, 2.2, 1.5, 0.08),
        "gate_vehicle_rail": box(0, 1.4, 0, 2.0, 0.06, 0.06),
        "gate_vehicle_chevron": box(0, 0.85, 0.05, 1.8, 0.35, 0.04),
        "gate_post": box(0, 0.9, 0, 0.22, 1.8, 0.22),
        "gate_post_light": box(0, 1.85, 0, 0.18, 0.18, 0.18),
        "gate_pedestrian": box(0, 0.8, 0, 1.2, 1.4, 0.08),
        "gate_pedestrian_frame": box(0, 0.8, 0, 1.35, 1.55, 0.06),
        "gate_sign": box(0, 2.0, 0, 1.6, 0.55, 0.06),
        "gate_sign_frame": box(0, 2.0, -0.04, 1.75, 0.68, 0.04),
        "gate_latch": box(0, 0.95, 0, 0.35, 0.18, 0.12),
        "gate_stop": box(0, 0.08, 0, 0.35, 0.12, 0.35),
    }
    return meshes


# --- PRP-003 -----------------------------------------------------------------


def forecourt_kit() -> dict:
    return {
        "kerb_straight": box(0, 0.12, 0, 4.0, 0.22, 0.35),
        "kerb_corner": box(0, 0.12, 0, 0.8, 0.22, 0.8),
        "bollard": cylinder(0, 0.45, 0, 0.1, 0.9, segments=10),
        "bollard_cap": cylinder(0, 0.92, 0, 0.12, 0.08, segments=10),
        "planter": box(0, 0.35, 0, 1.4, 0.5, 1.0),
        "planter_soil": box(0, 0.58, 0, 1.15, 0.12, 0.75),
        "planter_scrub": ellipsoid(0, 0.85, 0, 0.55, 0.35, 0.4, segments=8),
        "bench_seat": box(0, 0.4, 0, 2.4, 0.12, 0.55),
        "bench_back": box(0, 0.7, -0.22, 2.4, 0.45, 0.08),
        "bench_leg_l": box(-0.95, 0.2, 0, 0.12, 0.4, 0.4),
        "bench_leg_r": box(0.95, 0.2, 0, 0.12, 0.4, 0.4),
        "sign_post": cylinder(0, 1.1, 0, 0.06, 2.2, segments=8),
        "sign_face": box(0, 2.0, 0, 0.08, 0.7, 0.9),
        "sign_frame": box(0, 2.0, -0.02, 0.06, 0.82, 1.0),
        "dropoff_bollard": cylinder(0, 0.4, 0, 0.09, 0.8, segments=10),
        "trolley_rail": box(0, 0.55, 0, 1.8, 0.08, 0.08),
        "trolley_post_l": box(-0.85, 0.4, 0, 0.08, 0.8, 0.08),
        "trolley_post_r": box(0.85, 0.4, 0, 0.08, 0.8, 0.08),
    }


# --- WLD-004 -----------------------------------------------------------------


def context_terrain() -> dict:
    """Low-poly outer context chunks — do not replace runway/apron/stands."""
    meshes: dict = {
        "paddock_n": box(0, -0.4, 0, 40, 0.6, 16),
        "paddock_s": box(0, -0.4, 0, 40, 0.6, 14),
        "paddock_e": box(0, -0.4, 0, 14, 0.6, 30),
        "paddock_w": box(0, -0.4, 0, 14, 0.6, 30),
        "coast_sand": box(0, -0.2, 0, 50, 0.25, 8),
        "coast_shallows": box(0, -0.45, 0, 52, 0.15, 8),
        "coast_water": box(0, -0.55, 0, 55, 0.12, 12),
        "hill_a": ellipsoid(0, 1.2, 0, 8, 2.2, 5, segments=10),
        "hill_b": ellipsoid(0, 0.9, 0, 6, 1.6, 4, segments=9),
        "hill_c": ellipsoid(0, 1.5, 0, 10, 2.8, 6, segments=10),
        "dune_a": ellipsoid(0, 0.45, 0, 5, 0.7, 2.5, segments=8),
        "dune_b": ellipsoid(0, 0.35, 0, 4, 0.55, 2.0, segments=8),
        "berm": box(0, 0.35, 0, 12, 0.7, 2.5),
    }
    return meshes


def main() -> None:
    ENV.mkdir(parents=True, exist_ok=True)
    PROPS.mkdir(parents=True, exist_ok=True)
    write_kit(ENV, "mdl_eucalyptus_kit_v01", eucalyptus_kit())
    write_kit(ENV, "mdl_kingscote_scrub_kit_v01", scrub_kit())
    write_kit(PROPS, "mdl_airfield_fence_gate_kit_v01", fence_gate_kit())
    write_kit(PROPS, "mdl_terminal_forecourt_kit_v01", forecourt_kit())
    write_kit(ENV, "mdl_kingscote_context_terrain_v01", context_terrain())


if __name__ == "__main__":
    main()
