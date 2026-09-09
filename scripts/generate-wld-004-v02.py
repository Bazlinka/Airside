#!/usr/bin/env python3
"""Generate WLD-004 Kingscote context terrain v02 (FBX + companion glTF).

REF setting fidelity jump over Batch F3 v01 single ellipsoids / flat berm:
  - Multi-lobe softer hills and dunes (merged ellipsoids)
  - Slightly more faceted berm edge
  - Operational blank preserved (no runway/apron meshes)

Keeps every v01 mesh name so PreferArtKit can fall back cleanly.
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
BASENAME = "mdl_kingscote_context_terrain_v02"

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
    lines = ["# Airside WLD-004 v02"]
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


def multi_lobe_landform(
    cx: float,
    cy: float,
    cz: float,
    rx: float,
    ry: float,
    rz: float,
    *,
    lobes: int = 4,
    segments: int = 10,
) -> tuple[np.ndarray, np.ndarray]:
    """Softer multi-lobe hill / dune silhouette."""
    parts = [ellipsoid(cx, cy, cz, rx, ry, rz, segments=segments)]
    offsets = [
        (0.38, -0.08, -0.22, 0.55),
        (-0.32, -0.1, 0.28, 0.48),
        (0.18, 0.12, 0.2, 0.4),
        (-0.2, 0.05, -0.3, 0.42),
        (0.08, -0.15, 0.05, 0.45),
    ]
    for i in range(min(lobes, len(offsets))):
        ox, oy, oz, s = offsets[i]
        parts.append(
            ellipsoid(
                cx + ox * rx,
                cy + oy * ry,
                cz + oz * rz,
                rx * s,
                ry * s * 0.85,
                rz * s,
                segments=max(7, segments - 2),
            )
        )
    return merge(*parts)


def faceted_berm() -> tuple[np.ndarray, np.ndarray]:
    """Slightly more faceted berm edge than a single box."""
    return merge(
        box(0, 0.35, 0, 12, 0.7, 2.5),
        box(0, 0.55, -0.9, 11.5, 0.35, 0.7),
        box(0, 0.48, 0.95, 11.2, 0.28, 0.55),
        box(-5.5, 0.4, 0, 1.2, 0.55, 2.3),
        box(5.5, 0.4, 0, 1.2, 0.55, 2.3),
        box(-2.5, 0.62, -0.4, 2.0, 0.22, 1.4),
        box(2.8, 0.58, 0.35, 1.8, 0.2, 1.3),
        ellipsoid(0, 0.2, 0, 5.5, 0.18, 1.1, segments=8),
    )


def context_terrain_v02() -> dict[str, tuple[np.ndarray, np.ndarray]]:
    """Low-poly outer context chunks — do not replace runway/apron/stands."""
    return {
        "paddock_n": merge(
            box(0, -0.4, 0, 40, 0.6, 16),
            ellipsoid(0, -0.15, -2, 12, 0.25, 5, segments=8),
            ellipsoid(8, -0.12, 3, 8, 0.2, 4, segments=7),
        ),
        "paddock_s": merge(
            box(0, -0.4, 0, 40, 0.6, 14),
            ellipsoid(-6, -0.14, -1, 10, 0.22, 4.5, segments=8),
            ellipsoid(5, -0.12, 2, 9, 0.18, 3.5, segments=7),
        ),
        "paddock_e": merge(
            box(0, -0.4, 0, 14, 0.6, 30),
            ellipsoid(2, -0.14, 0, 4, 0.2, 10, segments=8),
        ),
        "paddock_w": merge(
            box(0, -0.4, 0, 14, 0.6, 30),
            ellipsoid(-2, -0.14, 0, 4, 0.2, 10, segments=8),
        ),
        "coast_sand": merge(
            box(0, -0.2, 0, 50, 0.25, 8),
            ellipsoid(0, -0.05, -1.5, 18, 0.18, 3, segments=8),
            ellipsoid(12, -0.08, 1.2, 10, 0.15, 2.5, segments=7),
            ellipsoid(-14, -0.06, 0.5, 9, 0.14, 2.2, segments=7),
        ),
        "coast_shallows": merge(
            box(0, -0.45, 0, 52, 0.15, 8),
            ellipsoid(0, -0.38, 1, 16, 0.1, 2.5, segments=7),
        ),
        "coast_water": merge(
            box(0, -0.55, 0, 55, 0.12, 12),
            ellipsoid(0, -0.5, 2, 20, 0.08, 4, segments=7),
        ),
        "hill_a": multi_lobe_landform(0, 1.2, 0, 8, 2.2, 5, lobes=5, segments=11),
        "hill_b": multi_lobe_landform(0, 0.9, 0, 6, 1.6, 4, lobes=4, segments=10),
        "hill_c": multi_lobe_landform(0, 1.5, 0, 10, 2.8, 6, lobes=5, segments=11),
        "dune_a": multi_lobe_landform(0, 0.45, 0, 5, 0.7, 2.5, lobes=4, segments=9),
        "dune_b": multi_lobe_landform(0, 0.35, 0, 4, 0.55, 2.0, lobes=4, segments=9),
        "berm": faceted_berm(),
    }


def main() -> None:
    ENV.mkdir(parents=True, exist_ok=True)
    meshes = context_terrain_v02()
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
