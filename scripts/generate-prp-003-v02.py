#!/usr/bin/env python3
"""Generate PRP-003 terminal forecourt kit v02 (FBX + companion glTF).

REF setting fidelity jump over Batch F3 v01 slab furniture:
  - Slatted bench seat/back; footed tubular legs
  - Nest trolley into rail + posts (basket / handle / casters silhouette)
  - Rimmed planter + multi-blob scrub; richer bollards and parking sign
  - Chamfered kerb profiles

Keeps every TryPlaceForecourtFromKit / PlaceLuggageTrolley / PlaceLandsideBench
extract name. Distinct id — does not overwrite v01. ASCII FBX UnitScaleFactor=100.
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
BASENAME = "mdl_terminal_forecourt_kit_v02"

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
ellipsoid = _f3.ellipsoid


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
    lines = ["# Airside PRP-003 v02"]
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


def slatted_bench_seat() -> tuple[np.ndarray, np.ndarray]:
    parts = [box(0, 0.36, 0, 2.35, 0.06, 0.52)]
    for z in (-0.18, -0.06, 0.06, 0.18):
        parts.append(box(0, 0.42, z, 2.28, 0.04, 0.1))
    parts.append(box(0, 0.42, 0.24, 2.2, 0.03, 0.05))  # front nose
    return merge(*parts)


def slatted_bench_back() -> tuple[np.ndarray, np.ndarray]:
    parts = [box(0, 0.62, -0.24, 2.35, 0.08, 0.06)]
    for y in (0.5, 0.65, 0.8, 0.95):
        parts.append(box(0, y, -0.2, 2.28, 0.1, 0.04))
    parts.append(box(-1.15, 0.7, -0.18, 0.08, 0.55, 0.12))
    parts.append(box(1.15, 0.7, -0.18, 0.08, 0.55, 0.12))
    return merge(*parts)


def bench_leg(side: float) -> tuple[np.ndarray, np.ndarray]:
    x = side * 0.95
    return merge(
        box(x, 0.2, 0, 0.1, 0.38, 0.42),
        box(x, 0.04, 0, 0.2, 0.06, 0.52),
        box(x, 0.55, 0, 0.08, 0.28, 0.48),  # arm stub nested in leg extract
        box(x, 0.72, 0, 0.12, 0.06, 0.5),
    )


def trolley_rail_dense() -> tuple[np.ndarray, np.ndarray]:
    """Basket rim + handle + cross braces live in trolley_rail extract."""
    return merge(
        box(0, 0.55, 0, 1.75, 0.06, 0.06),
        box(0, 0.72, 0, 1.65, 0.05, 0.05),
        box(0, 0.4, 0.2, 1.55, 0.35, 0.04),  # front mesh
        box(0, 0.85, -0.28, 1.5, 0.07, 0.07),  # handle
        cylinder(0, 0.85, -0.36, 0.05, 0.9, axis="x", segments=10),
        box(0, 0.62, -0.2, 1.4, 0.05, 0.05),
        box(-0.2, 0.5, 0.0, 0.28, 0.28, 0.22),  # nested bag silhouette
        box(0.22, 0.48, -0.05, 0.25, 0.24, 0.2),
    )


def trolley_post_dense(side: float) -> tuple[np.ndarray, np.ndarray]:
    x = side * 0.82
    return merge(
        box(x, 0.42, 0, 0.07, 0.78, 0.07),
        box(x, 0.5, 0.18, 0.05, 0.45, 0.04),  # side mesh
        box(x, 0.12, 0.16, 0.1, 0.08, 0.1),  # caster block
        box(x, 0.1, 0.16, 0.12, 0.12, 0.12),  # wheel
        box(x, 0.12, -0.16, 0.1, 0.08, 0.1),
        box(x, 0.1, -0.16, 0.12, 0.12, 0.12),
        box(x, 0.55, -0.22, 0.06, 0.55, 0.06),  # upright to handle
    )


def planter_rimmed() -> tuple[np.ndarray, np.ndarray]:
    return merge(
        box(0, 0.28, 0, 1.45, 0.42, 1.05),
        box(0, 0.52, 0, 1.55, 0.08, 1.15),  # rim
        box(0, 0.08, 0, 1.5, 0.1, 1.1),  # base flare
        box(-0.65, 0.35, 0.45, 0.08, 0.3, 0.08),  # corner post hint
        box(0.65, 0.35, 0.45, 0.08, 0.3, 0.08),
        box(-0.65, 0.35, -0.45, 0.08, 0.3, 0.08),
        box(0.65, 0.35, -0.45, 0.08, 0.3, 0.08),
    )


def planter_scrub_dense() -> tuple[np.ndarray, np.ndarray]:
    return merge(
        ellipsoid(0, 0.88, 0, 0.5, 0.32, 0.38, segments=8),
        ellipsoid(-0.28, 0.78, 0.12, 0.28, 0.22, 0.25, segments=7),
        ellipsoid(0.3, 0.82, -0.1, 0.26, 0.2, 0.24, segments=7),
        ellipsoid(0.05, 1.05, 0.05, 0.22, 0.18, 0.2, segments=6),
    )


def bollard_rich(height: float = 0.9, radius: float = 0.1) -> tuple[np.ndarray, np.ndarray]:
    return merge(
        cylinder(0, height * 0.5, 0, radius, height, segments=12),
        cylinder(0, 0.08, 0, radius * 1.35, 0.12, segments=12),
        cylinder(0, height * 0.55, 0, radius * 1.15, 0.06, segments=10),  # collar
        box(0, height * 0.35, 0, radius * 0.4, height * 0.5, radius * 0.05),  # stripe hint
    )


def parking_sign_face() -> tuple[np.ndarray, np.ndarray]:
    return merge(
        box(0, 2.0, 0, 0.06, 0.72, 0.92),
        box(0, 2.05, 0.04, 0.04, 0.35, 0.35),  # P badge block
        box(0, 1.78, 0.04, 0.04, 0.12, 0.55),  # chevron bar
        box(-0.12, 1.78, 0.04, 0.12, 0.08, 0.2),
        box(0.12, 1.78, 0.04, 0.12, 0.08, 0.2),
    )


def forecourt_kit_v02() -> dict[str, tuple[np.ndarray, np.ndarray]]:
    return {
        "kerb_straight": merge(
            box(0, 0.1, 0, 4.0, 0.18, 0.32),
            box(0, 0.2, 0.08, 4.0, 0.06, 0.18),  # chamfer lip
            box(0, 0.04, -0.12, 4.0, 0.06, 0.12),
        ),
        "kerb_corner": merge(
            box(0, 0.1, 0, 0.85, 0.18, 0.85),
            box(0, 0.2, 0.12, 0.85, 0.06, 0.4),
            box(0.12, 0.2, 0, 0.4, 0.06, 0.85),
        ),
        "bollard": bollard_rich(0.9, 0.1),
        "bollard_cap": merge(
            cylinder(0, 0.94, 0, 0.13, 0.1, segments=12),
            cylinder(0, 1.0, 0, 0.08, 0.06, segments=10),
            box(0, 0.92, 0, 0.18, 0.04, 0.18),
        ),
        "planter": planter_rimmed(),
        "planter_soil": merge(
            box(0, 0.58, 0, 1.2, 0.1, 0.8),
            ellipsoid(0, 0.64, 0, 0.55, 0.08, 0.35, segments=8),
        ),
        "planter_scrub": planter_scrub_dense(),
        "bench_seat": slatted_bench_seat(),
        "bench_back": slatted_bench_back(),
        "bench_leg_l": bench_leg(-1.0),
        "bench_leg_r": bench_leg(1.0),
        "sign_post": merge(
            cylinder(0, 1.1, 0, 0.055, 2.2, segments=12),
            cylinder(0, 0.08, 0, 0.12, 0.12, segments=10),
            cylinder(0, 2.25, 0, 0.07, 0.08, segments=10),
        ),
        "sign_face": parking_sign_face(),
        "sign_frame": merge(
            box(0, 2.0, -0.03, 0.05, 0.88, 1.05),
            box(0, 2.45, -0.02, 0.06, 0.06, 1.05),
            box(0, 1.55, -0.02, 0.06, 0.06, 1.05),
            box(0, 2.0, -0.02, 0.06, 0.88, 0.06),
            box(0, 2.0, -0.02, 0.06, 0.88, 0.06),
        ),
        "dropoff_bollard": bollard_rich(0.82, 0.095),
        "trolley_rail": trolley_rail_dense(),
        "trolley_post_l": trolley_post_dense(-1.0),
        "trolley_post_r": trolley_post_dense(1.0),
    }


def main() -> None:
    PROPS.mkdir(parents=True, exist_ok=True)
    meshes = forecourt_kit_v02()
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
