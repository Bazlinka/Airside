#!/usr/bin/env python3
"""Generate PRP-002 airfield fence/gate kit v02 (FBX + companion glTF).

REF setting fidelity jump over Batch F3 v01 solid slabs:
  - Chain-link silhouette (cross-bar mesh) instead of opaque panel
  - Tubular rails / rounder posts with caps
  - Gate leaves as frame + mesh infill; chevron / latch / sign preserved

Keeps every TryBuildPerimeterFenceFromKit extract name.
Distinct id — does not overwrite v01. ASCII FBX UnitScaleFactor=100.
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
PROPS = ROOT / "Models" / "Props"
BASENAME = "mdl_airfield_fence_gate_kit_v02"

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

_f3_spec = importlib.util.spec_from_file_location(
    "batch_f3", REPO / "scripts/generate-batch-f3-setting-modules.py"
)
_f3 = importlib.util.module_from_spec(_f3_spec)
assert _f3_spec.loader is not None
_f3_spec.loader.exec_module(_f3)

box = _batch.box
pack_gltf = _batch.pack_gltf
write_default_meta = _batch.write_default_meta
cylinder = _f3.cylinder


def new_guid() -> str:
    return uuid.uuid4().hex


def merge(*parts: tuple[np.ndarray, np.ndarray]) -> tuple[np.ndarray, np.ndarray]:
    verts: list[np.ndarray] = []
    indices: list[np.ndarray] = []
    base = 0
    for v, i in parts:
        vv = np.asarray(v, dtype=np.float32)
        ii = np.asarray(i, dtype=np.uint16) + base
        verts.append(vv)
        indices.append(ii)
        base += len(vv)
    return np.vstack(verts), np.concatenate(indices)


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
    lines = ["# Airside PRP-002 v02"]
    v_offset = 1
    for name, (verts, indices) in meshes.items():
        lines.append(f"o {name}")
        for v in verts:
            lines.append(f"v {v[0]:.6f} {v[1]:.6f} {v[2]:.6f}")
        for i in range(0, len(indices), 3):
            a = int(indices[i]) + v_offset
            b = int(indices[i + 1]) + v_offset
            c = int(indices[i + 2]) + v_offset
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
                capture_output=True,
                text=True,
                check=False,
            )
            if result.returncode == 0 and fbx_path.exists():
                write_fbx_model_meta(fbx_path)
                return
    _ascii.write_ascii_fbx(fbx_path, meshes)
    write_fbx_model_meta(fbx_path)


def chain_link_panel(
    cx: float,
    cy: float,
    cz: float,
    width: float,
    height: float,
    *,
    spacing: float = 0.28,
) -> tuple[np.ndarray, np.ndarray]:
    """Cross-bar lattice — overview reads as mesh, not a solid slab."""
    parts = []
    x0, x1 = cx - width * 0.5, cx + width * 0.5
    y0, y1 = cy - height * 0.5, cy + height * 0.5
    # Vertical wires
    x = x0 + 0.08
    while x <= x1 - 0.08:
        parts.append(box(x, cy, cz, 0.025, height * 0.92, 0.02))
        x += spacing
    # Horizontal wires
    y = y0 + 0.1
    while y <= y1 - 0.08:
        parts.append(box(cx, y, cz, width * 0.92, 0.025, 0.02))
        y += spacing
    # Border frame
    parts.append(box(cx, y1 - 0.03, cz, width, 0.05, 0.04))
    parts.append(box(cx, y0 + 0.03, cz, width, 0.05, 0.04))
    parts.append(box(x0 + 0.03, cy, cz, 0.05, height, 0.04))
    parts.append(box(x1 - 0.03, cy, cz, 0.05, height, 0.04))
    return merge(*parts)


def fence_gate_kit_v02() -> dict[str, tuple[np.ndarray, np.ndarray]]:
    meshes: dict[str, tuple[np.ndarray, np.ndarray]] = {
        "fence_bay": chain_link_panel(0, 0.75, 0, 3.85, 1.25, spacing=0.26),
        "fence_bay_rail_top": merge(
            cylinder(0, 1.38, 0, 0.035, 3.95, axis="x", segments=12),
            box(0, 1.38, 0, 3.95, 0.04, 0.06),
        ),
        "fence_bay_rail_mid": cylinder(0, 0.75, 0, 0.03, 3.9, axis="x", segments=12),
        "fence_bay_rail_bot": cylinder(0, 0.22, 0, 0.03, 3.9, axis="x", segments=12),
        "fence_bay_post_l": merge(
            cylinder(-1.98, 0.75, 0, 0.07, 1.5, axis="y", segments=12),
            box(-1.98, 0.08, 0, 0.18, 0.12, 0.18),
        ),
        "fence_bay_post_r": merge(
            cylinder(1.98, 0.75, 0, 0.07, 1.5, axis="y", segments=12),
            box(1.98, 0.08, 0, 0.18, 0.12, 0.18),
        ),
        "fence_bay_cap_l": merge(
            cylinder(-1.98, 1.52, 0, 0.09, 0.08, axis="y", segments=12),
            box(-1.98, 1.48, 0, 0.14, 0.04, 0.14),
        ),
        "fence_bay_cap_r": merge(
            cylinder(1.98, 1.52, 0, 0.09, 0.08, axis="y", segments=12),
            box(1.98, 1.48, 0, 0.14, 0.04, 0.14),
        ),
        "fence_corner": merge(
            cylinder(0, 0.78, 0, 0.1, 1.55, axis="y", segments=12),
            box(0, 0.08, 0, 0.22, 0.14, 0.22),
        ),
        "fence_corner_brace": merge(
            box(0.45, 0.9, 0.45, 1.1, 0.06, 0.06),
            box(0.45, 0.5, 0.45, 1.1, 0.06, 0.06),
        ),
        "fence_end": merge(
            cylinder(0, 0.75, 0, 0.08, 1.5, axis="y", segments=12),
            box(0, 0.08, 0, 0.18, 0.12, 0.18),
        ),
        "fence_end_brace": box(0.4, 0.75, 0, 0.95, 0.07, 0.07),
        # Gate leaves — frame + mesh (same names as v01).
        "gate_vehicle_leaf_l": merge(
            chain_link_panel(0, 0.85, 0, 2.05, 1.35, spacing=0.22),
            box(0, 1.55, 0, 2.15, 0.08, 0.06),
            box(0, 0.18, 0, 2.15, 0.08, 0.06),
            box(-1.0, 0.85, 0, 0.08, 1.45, 0.06),
            box(1.0, 0.85, 0, 0.08, 1.45, 0.06),
        ),
        "gate_vehicle_leaf_r": merge(
            chain_link_panel(0, 0.85, 0, 2.05, 1.35, spacing=0.22),
            box(0, 1.55, 0, 2.15, 0.08, 0.06),
            box(0, 0.18, 0, 2.15, 0.08, 0.06),
            box(-1.0, 0.85, 0, 0.08, 1.45, 0.06),
            box(1.0, 0.85, 0, 0.08, 1.45, 0.06),
        ),
        "gate_vehicle_rail": cylinder(0, 1.45, 0, 0.03, 2.0, axis="x", segments=10),
        "gate_vehicle_chevron": merge(
            box(0, 0.95, 0.06, 1.7, 0.28, 0.04),
            box(-0.45, 0.95, 0.07, 0.55, 0.12, 0.03),
            box(0.45, 0.95, 0.07, 0.55, 0.12, 0.03),
        ),
        "gate_post": merge(
            cylinder(0, 0.95, 0, 0.11, 1.9, axis="y", segments=12),
            box(0, 0.1, 0, 0.28, 0.16, 0.28),
            box(0, 1.9, 0, 0.2, 0.1, 0.2),
        ),
        "gate_post_light": merge(
            box(0, 1.95, 0, 0.2, 0.16, 0.2),
            cylinder(0, 2.08, 0, 0.08, 0.1, axis="y", segments=10),
        ),
        "gate_pedestrian": merge(
            chain_link_panel(0, 0.8, 0, 1.1, 1.25, spacing=0.2),
            box(0, 1.45, 0, 1.2, 0.06, 0.05),
            box(0, 0.18, 0, 1.2, 0.06, 0.05),
        ),
        "gate_pedestrian_frame": box(0, 0.8, 0, 1.35, 1.55, 0.05),
        "gate_sign": box(0, 2.05, 0, 1.7, 0.55, 0.06),
        "gate_sign_frame": box(0, 2.05, -0.04, 1.85, 0.7, 0.05),
        "gate_latch": merge(
            box(0, 0.95, 0, 0.4, 0.2, 0.14),
            cylinder(0.18, 0.95, 0.08, 0.04, 0.12, axis="z", segments=8),
        ),
        "gate_stop": merge(
            box(0, 0.08, 0, 0.4, 0.12, 0.4),
            cylinder(0, 0.16, 0, 0.12, 0.08, axis="y", segments=10),
        ),
    }
    return meshes


def main() -> None:
    PROPS.mkdir(parents=True, exist_ok=True)
    meshes = fence_gate_kit_v02()
    gltf = PROPS / f"{BASENAME}.gltf"
    fbx = PROPS / f"{BASENAME}.fbx"
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
    print(f"Wrote {BASENAME} ({len(meshes)} meshes)")


if __name__ == "__main__":
    main()
