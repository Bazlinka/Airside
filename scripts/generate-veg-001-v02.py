#!/usr/bin/env python3
"""Generate VEG-001 eucalyptus kit v02 (FBX + companion glTF).

REF setting fidelity jump over Batch F3 v01 thin cylinders / single ellipsoids:
  - Multi-lobe canopies (nested ellipsoids inside each canopy extract)
  - Tapered trunk with bark collar rings
  - Wider buttress flare; irregular peeled-bark slabs
  - Primary fork + thinner secondary limb
  - Richer lod1 far silhouette

Keeps every TryPlaceTreeFromKit extract name (tree_a|b|c × 10).
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
ENV = ROOT / "Models" / "Environment"
BASENAME = "mdl_eucalyptus_kit_v02"

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
    lines = ["# Airside VEG-001 v02"]
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


def multi_lobe_canopy(
    cx: float,
    cy: float,
    cz: float,
    rx: float,
    ry: float,
    rz: float,
    *,
    lobes: int = 4,
) -> tuple[np.ndarray, np.ndarray]:
    """Nest several ellipsoids so overview reads as a eucalyptus crown, not one blob."""
    parts = [ellipsoid(cx, cy, cz, rx, ry, rz, segments=9)]
    offsets = [
        (0.35, 0.12, -0.2, 0.55),
        (-0.3, -0.08, 0.25, 0.48),
        (0.15, 0.28, 0.2, 0.42),
        (-0.2, 0.18, -0.28, 0.4),
        (0.05, -0.22, 0.05, 0.45),
    ]
    for i in range(min(lobes, len(offsets))):
        ox, oy, oz, s = offsets[i]
        parts.append(
            ellipsoid(
                cx + ox * rx,
                cy + oy * ry,
                cz + oz * rz,
                rx * s,
                ry * s * 0.9,
                rz * s,
                segments=7,
            )
        )
    return merge(*parts)


def tapered_trunk(trunk_h: float) -> tuple[np.ndarray, np.ndarray]:
    return merge(
        cylinder(0, trunk_h * 0.5, 0, 0.12, trunk_h, segments=12),
        cylinder(0, trunk_h * 0.22, 0, 0.14, trunk_h * 0.12, segments=10),  # low collar
        cylinder(0, trunk_h * 0.48, 0, 0.13, trunk_h * 0.08, segments=10),  # mid collar
        cylinder(0, trunk_h * 0.85, 0, 0.09, trunk_h * 0.18, segments=10),  # taper tip
    )


def buttress_flare() -> tuple[np.ndarray, np.ndarray]:
    return merge(
        cylinder(0, 0.1, 0, 0.26, 0.2, segments=12),
        cylinder(0, 0.04, 0, 0.32, 0.08, segments=12),
        box(0.18, 0.08, 0, 0.22, 0.1, 0.1),
        box(-0.16, 0.08, 0.12, 0.2, 0.1, 0.1),
        box(0.05, 0.08, -0.18, 0.18, 0.1, 0.12),
    )


def bark_slab(cy: float, size: float) -> tuple[np.ndarray, np.ndarray]:
    return merge(
        box(0.08, cy, 0, size * 0.55, size * 0.35, size * 0.12),
        box(-0.1, cy + 0.04, 0.06, size * 0.45, size * 0.28, size * 0.1),
        box(0.02, cy - 0.03, -0.08, size * 0.4, size * 0.22, size * 0.1),
    )


def forked_limb(lean: float, trunk_h: float) -> tuple[np.ndarray, np.ndarray]:
    ox = 0.22 + lean * 0.02
    return merge(
        cylinder(ox, trunk_h * 0.78, -0.12, 0.065, trunk_h * 0.32, segments=10),
        cylinder(ox + 0.18, trunk_h * 0.92, -0.22, 0.04, trunk_h * 0.22, segments=8),
        cylinder(ox - 0.08, trunk_h * 0.88, 0.1, 0.035, trunk_h * 0.18, segments=8),
    )


def denser_lod1(trunk_h: float, canopy_scale: float) -> tuple[np.ndarray, np.ndarray]:
    s = canopy_scale
    return merge(
        ellipsoid(0, trunk_h * 0.9, 0, 1.15 * s, 1.45 * s, 1.05 * s, segments=8),
        ellipsoid(0.4 * s, trunk_h * 0.75, -0.25 * s, 0.7 * s, 0.85 * s, 0.65 * s, segments=7),
        ellipsoid(-0.35 * s, trunk_h * 0.8, 0.3 * s, 0.65 * s, 0.8 * s, 0.6 * s, segments=7),
        ellipsoid(0.1 * s, trunk_h * 1.05, 0.1 * s, 0.55 * s, 0.7 * s, 0.5 * s, segments=6),
    )


def eucalyptus_kit_v02() -> dict[str, tuple[np.ndarray, np.ndarray]]:
    meshes: dict[str, tuple[np.ndarray, np.ndarray]] = {}

    def add_tree(prefix: str, trunk_h: float, lean: float, canopy_scale: float) -> None:
        s = canopy_scale
        meshes[f"{prefix}_trunk"] = tapered_trunk(trunk_h)
        meshes[f"{prefix}_flare"] = buttress_flare()
        meshes[f"{prefix}_bark_low"] = bark_slab(trunk_h * 0.28, 0.28)
        meshes[f"{prefix}_bark_mid"] = bark_slab(trunk_h * 0.55, 0.24)
        meshes[f"{prefix}_fork"] = forked_limb(lean, trunk_h)
        meshes[f"{prefix}_canopy"] = multi_lobe_canopy(
            0, trunk_h + 0.15, 0, 0.95 * s, 0.72 * s, 0.9 * s, lobes=5
        )
        meshes[f"{prefix}_canopy_b"] = multi_lobe_canopy(
            0.55 * s, trunk_h - 0.35, -0.35 * s, 0.65 * s, 0.52 * s, 0.6 * s, lobes=4
        )
        meshes[f"{prefix}_canopy_c"] = multi_lobe_canopy(
            -0.45 * s, trunk_h - 0.25, 0.4 * s, 0.55 * s, 0.48 * s, 0.52 * s, lobes=4
        )
        meshes[f"{prefix}_canopy_d"] = multi_lobe_canopy(
            0.25 * s, trunk_h + 0.35, 0.25 * s, 0.48 * s, 0.4 * s, 0.45 * s, lobes=3
        )
        meshes[f"{prefix}_lod1"] = denser_lod1(trunk_h, canopy_scale)

    add_tree("tree_a", 3.1, 2.0, 1.0)
    add_tree("tree_b", 2.6, -3.0, 0.9)
    add_tree("tree_c", 3.5, 1.0, 1.15)
    return meshes


def main() -> None:
    ENV.mkdir(parents=True, exist_ok=True)
    meshes = eucalyptus_kit_v02()
    gltf = ENV / f"{BASENAME}.gltf"
    fbx = ENV / f"{BASENAME}.fbx"
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
