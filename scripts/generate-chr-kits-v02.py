#!/usr/bin/env python3
"""Generate CHR-001/002 character kits v02 (FBX + companion glTF).

REF-003 apron-life fidelity jump over Batch F2 v01 box figures:
  - Higher-segment limbs, tapered arms/legs, neck + shoulders
  - Hi-vis vest mass + hard-hat brim; marshaller wand sockets
  - Same extract names for UpdateApronLife / TryPlaceCharacterFromKit

Distinct ids — does not overwrite v01.
ASCII FBX UnitScaleFactor=100 (metres).
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
CHARACTERS = ROOT / "Models" / "Characters"

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

_f2_spec = importlib.util.spec_from_file_location(
    "batch_f2", REPO / "scripts/generate-batch-f2-vehicles-characters.py"
)
_f2 = importlib.util.module_from_spec(_f2_spec)
assert _f2_spec.loader is not None
_f2_spec.loader.exec_module(_f2)

box = _batch.box
pack_gltf = _batch.pack_gltf
write_default_meta = _batch.write_default_meta
cylinder = _f2.cylinder


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
    lines = ["# Airside CHR kits v02"]
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


def write_kit(basename: str, meshes: dict) -> None:
    CHARACTERS.mkdir(parents=True, exist_ok=True)
    gltf = CHARACTERS / f"{basename}.gltf"
    fbx = CHARACTERS / f"{basename}.fbx"
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


def figure_parts_v02(
    prefix: str,
    *,
    seated: bool = False,
    hi_vis: bool = False,
    wand: bool = False,
    scale: float = 1.0,
) -> dict[str, tuple[np.ndarray, np.ndarray]]:
    """Denser figure under the same extract names as v01."""
    s = scale
    body_h = (0.55 if seated else 0.9) * s
    body_y = (0.55 if seated else 0.95) * s
    head_y = body_y + body_h * 0.52 + 0.18 * s
    segs = 14

    torso = merge(
        box(0, body_y, 0, 0.4 * s, body_h, 0.24 * s),
        # Shoulders
        cylinder(-0.22 * s, body_y + body_h * 0.32, 0, 0.08 * s, 0.16 * s, axis="x", segments=segs),
        cylinder(0.22 * s, body_y + body_h * 0.32, 0, 0.08 * s, 0.16 * s, axis="x", segments=segs),
        # Hip flare
        box(0, body_y - body_h * 0.35, 0, 0.36 * s, 0.12 * s, 0.22 * s),
    )
    head = merge(
        cylinder(0, head_y, 0, 0.12 * s, 0.22 * s, axis="y", segments=segs),
        # Neck
        cylinder(0, head_y - 0.14 * s, 0, 0.06 * s, 0.1 * s, axis="y", segments=10),
    )

    meshes: dict[str, tuple[np.ndarray, np.ndarray]] = {
        f"{prefix}_torso": torso,
        f"{prefix}_head": head,
    }

    if hi_vis:
        meshes[f"{prefix}_vest"] = merge(
            box(0, body_y + 0.02 * s, 0.13 * s, 0.38 * s, 0.45 * s, 0.05 * s),
            box(0, body_y + 0.12 * s, 0.14 * s, 0.36 * s, 0.08 * s, 0.04 * s),
            box(0, body_y - 0.08 * s, 0.14 * s, 0.36 * s, 0.08 * s, 0.04 * s),
        )
        meshes[f"{prefix}_hat"] = merge(
            cylinder(0, head_y + 0.14 * s, 0, 0.14 * s, 0.1 * s, axis="y", segments=segs),
            # Brim
            cylinder(0, head_y + 0.1 * s, 0, 0.18 * s, 0.03 * s, axis="y", segments=segs),
        )

    if not seated:
        # Tapered limbs: thigh + shin / upper + forearm under one name.
        meshes[f"{prefix}_leg_l"] = merge(
            cylinder(-0.1 * s, 0.55 * s, 0, 0.08 * s, 0.45 * s, axis="y", segments=segs),
            cylinder(-0.1 * s, 0.22 * s, 0.02 * s, 0.065 * s, 0.4 * s, axis="y", segments=segs),
        )
        meshes[f"{prefix}_leg_r"] = merge(
            cylinder(0.1 * s, 0.55 * s, 0, 0.08 * s, 0.45 * s, axis="y", segments=segs),
            cylinder(0.1 * s, 0.22 * s, 0.02 * s, 0.065 * s, 0.4 * s, axis="y", segments=segs),
        )
        meshes[f"{prefix}_arm_l"] = merge(
            cylinder(-0.3 * s, body_y + 0.05 * s, 0, 0.06 * s, 0.35 * s, axis="y", segments=segs),
            cylinder(-0.3 * s, body_y - 0.22 * s, 0.02 * s, 0.05 * s, 0.32 * s, axis="y", segments=segs),
        )
        meshes[f"{prefix}_arm_r"] = merge(
            cylinder(0.3 * s, body_y + 0.05 * s, 0, 0.06 * s, 0.35 * s, axis="y", segments=segs),
            cylinder(0.3 * s, body_y - 0.22 * s, 0.02 * s, 0.05 * s, 0.32 * s, axis="y", segments=segs),
        )
        meshes[f"{prefix}_shoe_l"] = merge(
            box(-0.1 * s, 0.05 * s, 0.06 * s, 0.15 * s, 0.08 * s, 0.26 * s),
            box(-0.1 * s, 0.07 * s, 0.14 * s, 0.12 * s, 0.06 * s, 0.1 * s),
        )
        meshes[f"{prefix}_shoe_r"] = merge(
            box(0.1 * s, 0.05 * s, 0.06 * s, 0.15 * s, 0.08 * s, 0.26 * s),
            box(0.1 * s, 0.07 * s, 0.14 * s, 0.12 * s, 0.06 * s, 0.1 * s),
        )
        if wand:
            meshes[f"{prefix}_wand"] = merge(
                cylinder(0.42 * s, body_y + 0.2 * s, 0.05 * s, 0.025 * s, 0.55 * s, axis="y", segments=10),
                box(0.42 * s, body_y - 0.05 * s, 0.05 * s, 0.06 * s, 0.08 * s, 0.06 * s),
            )
            meshes[f"{prefix}_wand_tip"] = merge(
                box(0.42 * s, body_y + 0.52 * s, 0.05 * s, 0.09 * s, 0.09 * s, 0.09 * s),
                cylinder(0.42 * s, body_y + 0.58 * s, 0.05 * s, 0.05 * s, 0.04 * s, axis="y", segments=10),
            )
    else:
        meshes[f"{prefix}_legs"] = merge(
            box(0, 0.3 * s, 0.22 * s, 0.42 * s, 0.22 * s, 0.58 * s),
            cylinder(-0.12 * s, 0.22 * s, 0.35 * s, 0.07 * s, 0.35 * s, axis="z", segments=segs),
            cylinder(0.12 * s, 0.22 * s, 0.35 * s, 0.07 * s, 0.35 * s, axis="z", segments=segs),
        )
        meshes[f"{prefix}_arm_l"] = merge(
            cylinder(-0.28 * s, body_y, 0.08 * s, 0.055 * s, 0.4 * s, axis="z", segments=segs),
            box(-0.28 * s, body_y, 0.28 * s, 0.1 * s, 0.1 * s, 0.1 * s),
        )
        meshes[f"{prefix}_arm_r"] = merge(
            cylinder(0.28 * s, body_y, 0.08 * s, 0.055 * s, 0.4 * s, axis="z", segments=segs),
            box(0.28 * s, body_y, 0.28 * s, 0.1 * s, 0.1 * s, 0.1 * s),
        )

    return meshes


def ramp_crew_kit_v02() -> dict[str, tuple[np.ndarray, np.ndarray]]:
    meshes: dict[str, tuple[np.ndarray, np.ndarray]] = {}
    for role, hi, wand in (("marshaller", True, True), ("fueler", True, False), ("ramp", True, False)):
        meshes.update(figure_parts_v02(role, seated=False, hi_vis=hi, wand=wand))
    return meshes


def passenger_kit_v02() -> dict[str, tuple[np.ndarray, np.ndarray]]:
    meshes: dict[str, tuple[np.ndarray, np.ndarray]] = {}
    variants = [
        ("stand_a", False, 1.0),
        ("stand_b", False, 0.95),
        ("walk_c", False, 1.02),
        ("walk_d", False, 0.98),
        ("sit_e", True, 1.0),
        ("sit_f", True, 0.96),
    ]
    for prefix, seated, scale in variants:
        meshes.update(figure_parts_v02(prefix, seated=seated, hi_vis=False, wand=False, scale=scale))
    return meshes


def main() -> None:
    write_kit("mdl_ramp_crew_kit_v02", ramp_crew_kit_v02())
    write_kit("mdl_passenger_kit_v02", passenger_kit_v02())


if __name__ == "__main__":
    main()
