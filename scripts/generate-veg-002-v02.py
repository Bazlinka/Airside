#!/usr/bin/env python3
"""Generate VEG-002 Kingscote scrub kit v02 (FBX + companion glTF).

REF setting fidelity jump over Batch F3 v01 single ellipsoids / thin grass boxes:
  - Multi-lobe scrub cores and side foliage (nested ellipsoids)
  - denser grass_tuft_c blades
  - rock_c pale limestone
  - dune_mix_a/b sand mound + sparse scrub lobes

Keeps every TryPlaceScrubFromKit extract name (scrub_a|b|c|d|e × parts,
rock_a/b, grass_tuft_a/b). Distinct id — does not overwrite v01.
ASCII FBX UnitScaleFactor=100.
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
BASENAME = "mdl_kingscote_scrub_kit_v02"

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
    lines = ["# Airside VEG-002 v02"]
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


def multi_lobe_foliage(
    cx: float,
    cy: float,
    cz: float,
    rx: float,
    ry: float,
    rz: float,
    *,
    lobes: int = 4,
) -> tuple[np.ndarray, np.ndarray]:
    """Nest several ellipsoids so scrub reads as coastal bush, not one blob."""
    parts = [ellipsoid(cx, cy, cz, rx, ry, rz, segments=9)]
    offsets = [
        (0.32, 0.1, -0.22, 0.52),
        (-0.28, -0.06, 0.24, 0.46),
        (0.14, 0.22, 0.18, 0.4),
        (-0.18, 0.16, -0.26, 0.38),
        (0.06, -0.18, 0.08, 0.42),
    ]
    for i in range(min(lobes, len(offsets))):
        ox, oy, oz, s = offsets[i]
        parts.append(
            ellipsoid(
                cx + ox * rx,
                cy + oy * ry,
                cz + oz * rz,
                rx * s,
                ry * s * 0.88,
                rz * s,
                segments=7,
            )
        )
    return merge(*parts)


def denser_grass_tuft(*, scale: float = 1.0) -> tuple[np.ndarray, np.ndarray]:
    """Thin boxes + small ellipsoids for a denser coastal grass patch."""
    s = scale
    blades = [
        box(-0.08 * s, 0.22 * s, 0.0, 0.05 * s, 0.44 * s, 0.04 * s),
        box(0.06 * s, 0.2 * s, -0.04 * s, 0.045 * s, 0.4 * s, 0.035 * s),
        box(0.0, 0.18 * s, 0.06 * s, 0.04 * s, 0.36 * s, 0.04 * s),
        box(-0.12 * s, 0.16 * s, 0.05 * s, 0.035 * s, 0.32 * s, 0.03 * s),
        box(0.12 * s, 0.17 * s, 0.02 * s, 0.035 * s, 0.34 * s, 0.03 * s),
        box(0.02 * s, 0.24 * s, -0.08 * s, 0.04 * s, 0.38 * s, 0.035 * s),
        ellipsoid(0.0, 0.1 * s, 0.0, 0.14 * s, 0.08 * s, 0.12 * s, segments=6),
        ellipsoid(-0.05 * s, 0.12 * s, 0.04 * s, 0.08 * s, 0.1 * s, 0.07 * s, segments=5),
    ]
    return merge(*blades)


def pale_rock(*, scale: float = 1.0) -> tuple[np.ndarray, np.ndarray]:
    s = scale
    return merge(
        ellipsoid(0, 0.16 * s, 0, 0.32 * s, 0.18 * s, 0.26 * s, segments=8),
        ellipsoid(0.12 * s, 0.12 * s, -0.08 * s, 0.18 * s, 0.12 * s, 0.15 * s, segments=7),
        ellipsoid(-0.1 * s, 0.1 * s, 0.1 * s, 0.15 * s, 0.1 * s, 0.14 * s, segments=6),
    )


def dune_mix(*, scale: float = 1.0, lean: float = 0.0) -> tuple[np.ndarray, np.ndarray]:
    """Sand mound ellipsoid plus sparse scrub lobes — distinct from shrubs."""
    s = scale
    ox = lean
    return merge(
        ellipsoid(ox, 0.18 * s, 0, 0.9 * s, 0.22 * s, 0.55 * s, segments=9),
        ellipsoid(ox + 0.25 * s, 0.12 * s, -0.15 * s, 0.45 * s, 0.14 * s, 0.3 * s, segments=7),
        ellipsoid(ox - 0.2 * s, 0.14 * s, 0.12 * s, 0.4 * s, 0.12 * s, 0.28 * s, segments=7),
        # sparse scrub on the crest
        multi_lobe_foliage(ox + 0.1 * s, 0.42 * s, 0.05 * s, 0.22 * s, 0.16 * s, 0.2 * s, lobes=3),
        multi_lobe_foliage(ox - 0.15 * s, 0.38 * s, -0.08 * s, 0.18 * s, 0.14 * s, 0.16 * s, lobes=2),
    )


def scrub_kit_v02() -> dict[str, tuple[np.ndarray, np.ndarray]]:
    meshes: dict[str, tuple[np.ndarray, np.ndarray]] = {}
    variants = [
        ("scrub_a", 1.0, 0.0),
        ("scrub_b", 0.85, 0.2),
        ("scrub_c", 1.1, -0.15),
        ("scrub_d", 0.75, 0.1),
        ("scrub_e", 0.95, -0.25),
    ]
    for prefix, s, ox in variants:
        meshes[f"{prefix}_core"] = multi_lobe_foliage(
            ox, 0.38 * s, 0, 0.65 * s, 0.4 * s, 0.55 * s, lobes=5
        )
        meshes[f"{prefix}_side"] = multi_lobe_foliage(
            ox + 0.4 * s, 0.32 * s, -0.25 * s, 0.45 * s, 0.3 * s, 0.4 * s, lobes=4
        )
        meshes[f"{prefix}_side_b"] = multi_lobe_foliage(
            ox - 0.35 * s, 0.3 * s, 0.2 * s, 0.4 * s, 0.28 * s, 0.35 * s, lobes=4
        )
        meshes[f"{prefix}_tuft"] = multi_lobe_foliage(
            ox + 0.1 * s, 0.55 * s, 0.15 * s, 0.28 * s, 0.22 * s, 0.25 * s, lobes=3
        )

    # Legacy rock / grass names (densified but same extract ids).
    meshes["rock_a"] = merge(
        ellipsoid(0, 0.18, 0, 0.35, 0.2, 0.28, segments=8),
        ellipsoid(0.12, 0.14, -0.08, 0.18, 0.12, 0.16, segments=7),
        ellipsoid(-0.1, 0.12, 0.1, 0.16, 0.1, 0.14, segments=6),
    )
    meshes["rock_b"] = merge(
        ellipsoid(0, 0.14, 0, 0.28, 0.15, 0.22, segments=7),
        ellipsoid(0.08, 0.1, 0.06, 0.14, 0.09, 0.12, segments=6),
    )
    meshes["grass_tuft_a"] = merge(
        box(0, 0.2, 0, 0.35, 0.4, 0.12),
        box(-0.08, 0.22, 0.04, 0.06, 0.36, 0.05),
        box(0.1, 0.18, -0.03, 0.05, 0.32, 0.04),
        ellipsoid(0, 0.08, 0, 0.12, 0.06, 0.1, segments=5),
    )
    meshes["grass_tuft_b"] = merge(
        box(0, 0.18, 0, 0.28, 0.35, 0.1),
        box(0.06, 0.2, 0.02, 0.05, 0.3, 0.04),
        box(-0.05, 0.16, -0.04, 0.045, 0.28, 0.035),
    )

    # New denser extras (PreferArtKit / placement can opt in).
    meshes["grass_tuft_c"] = denser_grass_tuft(scale=1.05)
    meshes["rock_c"] = pale_rock(scale=1.1)
    meshes["dune_mix_a"] = dune_mix(scale=1.0, lean=0.0)
    meshes["dune_mix_b"] = dune_mix(scale=0.85, lean=0.15)
    return meshes


def main() -> None:
    ENV.mkdir(parents=True, exist_ok=True)
    meshes = scrub_kit_v02()
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
