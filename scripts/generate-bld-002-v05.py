#!/usr/bin/env python3
"""Generate BLD-002 hangar v05 (FBX + companion glTF).

REF-001 / REF-005 fidelity jump over authored_v01 box densify:
  - True dual-pitch corrugated roof (rotated panels + ridge)
  - Stronger gable ends and wall corrugation
  - Sliding door panels / bars / tracks keep motion names
  - Office lean-to with readable glass

Distinct id `mdl_hangar_small_v05` — does not overwrite authored_v01 / v04.
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
BUILDINGS = ROOT / "Models" / "Buildings"
BASENAME = "mdl_hangar_small_v05"

_authored_spec = importlib.util.spec_from_file_location(
    "authored_fbx", REPO / "scripts/generate-authored-fbx-turboprop-terminal.py"
)
_authored = importlib.util.module_from_spec(_authored_spec)
assert _authored_spec.loader is not None
_authored_spec.loader.exec_module(_authored)

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
cylinder = _authored.cylinder


def new_guid() -> str:
    return uuid.uuid4().hex


def outward_winding(mesh: tuple[np.ndarray, np.ndarray]):
    vertices, indices = mesh
    triangles = indices.reshape(-1, 3)
    signed_volume = np.einsum(
        "ij,ij->i",
        vertices[triangles[:, 0]],
        np.cross(vertices[triangles[:, 1]], vertices[triangles[:, 2]]),
    ).sum() / 6.0
    if signed_volume < -1e-7:
        triangles = triangles[:, [0, 2, 1]]
    return vertices, triangles.reshape(-1).astype(indices.dtype)


def rotated_box(
    cx: float,
    cy: float,
    cz: float,
    sx: float,
    sy: float,
    sz: float,
    *,
    z_degrees: float = 0.0,
) -> tuple[np.ndarray, np.ndarray]:
    """Rotate a box about local Z (pitch roof panels toward centre)."""
    verts, indices = box(cx, cy, cz, sx, sy, sz)
    angle = np.deg2rad(z_degrees)
    cosine, sine = float(np.cos(angle)), float(np.sin(angle))
    centred = verts - np.array([cx, cy, cz], np.float32)
    x = centred[:, 0].copy()
    y = centred[:, 1].copy()
    centred[:, 0] = x * cosine - y * sine
    centred[:, 1] = x * sine + y * cosine
    return outward_winding((centred + np.array([cx, cy, cz], np.float32), indices))


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
    lines = ["# Airside BLD-002 v05"]
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


def hangar_v05() -> dict[str, tuple[np.ndarray, np.ndarray]]:
    meshes = _authored.hangar_meshes()
    # Signature upgrade: pitched roof panels (REF-001 gable hangar).
    meshes["roof_panel_l"] = rotated_box(-3.55, 4.55, 0, 7.4, 0.18, 9.3, z_degrees=16.0)
    meshes["roof_panel_r"] = rotated_box(3.55, 4.55, 0, 7.4, 0.18, 9.3, z_degrees=-16.0)
    meshes["roof_ridge"] = box(0, 5.45, 0, 0.55, 0.28, 9.4)
    meshes["roof_ridge_cap"] = cylinder(0, 5.55, 0, 0.14, 9.2, axis="z", segments=12)
    meshes["roof_vent_ridge"] = cylinder(0, 5.72, 0, 0.18, 3.5, axis="z", segments=10)
    # Gable end frames — readable triangular silhouette from overview.
    meshes["gable_front_l"] = rotated_box(-3.6, 4.2, 4.55, 7.2, 0.12, 0.18, z_degrees=16.0)
    meshes["gable_front_r"] = rotated_box(3.6, 4.2, 4.55, 7.2, 0.12, 0.18, z_degrees=-16.0)
    meshes["gable_back_l"] = rotated_box(-3.6, 4.2, -4.55, 7.2, 0.12, 0.18, z_degrees=16.0)
    meshes["gable_back_r"] = rotated_box(3.6, 4.2, -4.55, 7.2, 0.12, 0.18, z_degrees=-16.0)
    meshes["gable_apex_front"] = box(0, 5.35, 4.55, 0.35, 0.35, 0.2)
    meshes["gable_apex_back"] = box(0, 5.35, -4.55, 0.35, 0.35, 0.2)
    # Extra corrugation + cladding depth.
    for i, z in enumerate((-3.8, -2.8, -1.8, -0.8, 0.8, 1.8, 2.8, 3.8), start=13):
        meshes[f"wall_rib_l_{i}"] = box(-7.14, 2.5, z, 0.12, 4.6, 0.16)
        meshes[f"wall_rib_r_{i}"] = box(7.14, 2.5, z, 0.12, 4.6, 0.16)
    meshes["cladding_face_front"] = box(0, 2.5, 4.52, 14.0, 4.6, 0.08)
    meshes["cladding_face_back"] = box(0, 2.5, -4.52, 14.0, 4.6, 0.08)
    # Deeper door track + Safety Yellow warning stripes already named.
    meshes["door_track_mid"] = box(0, 4.45, 4.58, 9.8, 0.16, 0.28)
    meshes["door_header"] = box(0, 4.15, 4.65, 9.6, 0.22, 0.18)
    meshes["door_threshold"] = box(0, 0.08, 4.85, 9.4, 0.12, 0.35)
    # Soften office lean silhouette.
    meshes["office_lean"] = box(5.9, 1.45, -3.45, 3.6, 2.7, 3.15)
    meshes["office_roof"] = rotated_box(5.9, 2.95, -3.45, 3.7, 0.14, 3.25, z_degrees=-6.0)
    meshes["office_awning"] = box(5.9, 2.6, -5.2, 2.6, 0.08, 0.65)
    # Keep skylights sitting on pitched roof.
    meshes["skylight_l"] = rotated_box(-3.0, 5.15, -1.2, 2.2, 0.05, 1.3, z_degrees=16.0)
    meshes["skylight_r"] = rotated_box(3.0, 5.15, -1.2, 2.2, 0.05, 1.3, z_degrees=-16.0)
    meshes["skylight_mid"] = box(0.0, 5.35, 1.0, 1.8, 0.05, 1.1)
    return {k: outward_winding(v) for k, v in meshes.items()}


def main() -> None:
    BUILDINGS.mkdir(parents=True, exist_ok=True)
    meshes = hangar_v05()
    gltf = BUILDINGS / f"{BASENAME}.gltf"
    fbx = BUILDINGS / f"{BASENAME}.fbx"
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
