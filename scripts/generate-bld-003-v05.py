#!/usr/bin/env python3
"""Generate BLD-003 operations shed v05 (FBX + companion glTF).

REF-001 / regional ops-building fidelity jump over authored_v01 densify:
  - Dual-pitch corrugated roof + ridge / vents
  - Stronger porch canopy, posts, fascia
  - Denser wall corrugation and cladding
  - Antenna / AC / radio rack remain readable from overview

Preserves night-glow / glass / door extract names:
  interior_glow, window_l/r, window_side*, glass_pane*, door*

Distinct id `mdl_operations_shed_v05` — does not overwrite authored_v01 / v04.
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
BASENAME = "mdl_operations_shed_v05"

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
    lines = ["# Airside BLD-003 v05"]
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


def ops_shed_v05() -> dict[str, tuple[np.ndarray, np.ndarray]]:
    meshes = _authored.ops_shed_meshes()
    # Signature upgrade: dual-pitch roof (matches hangar/terminal vernacular).
    meshes["roof_panel_l"] = rotated_box(-1.6, 3.05, 0.0, 3.35, 0.14, 4.15, z_degrees=14.0)
    meshes["roof_panel_r"] = rotated_box(1.6, 3.05, 0.0, 3.35, 0.14, 4.15, z_degrees=-14.0)
    meshes["roof_ridge"] = box(0.0, 3.45, 0.0, 0.42, 0.22, 4.25)
    meshes["roof_ridge_cap"] = cylinder(0.0, 3.55, 0.0, 0.1, 4.1, axis="z", segments=12)
    # Flat under-roof kept for prefix/surface fallbacks but lowered as soffit.
    meshes["roof_panel"] = box(0.0, 2.72, 0.0, 5.9, 0.08, 3.7)
    meshes["roof_gutter"] = box(0.0, 2.7, 2.08, 6.15, 0.1, 0.14)
    meshes["roof_fascia"] = box(0.0, 2.85, 2.12, 6.2, 0.2, 0.1)
    meshes["roof_eave_back"] = box(0.0, 2.85, -2.12, 6.2, 0.18, 0.1)
    meshes["roof_flash_front"] = box(0.0, 3.15, 1.95, 6.05, 0.06, 0.18)
    meshes["roof_flash_back"] = box(0.0, 3.15, -1.95, 6.05, 0.06, 0.18)
    meshes["roof_vent_a"] = cylinder(-1.1, 3.55, 0.55, 0.16, 0.4, axis="y", segments=10)
    meshes["roof_vent_b"] = cylinder(1.15, 3.55, -0.7, 0.16, 0.4, axis="y", segments=10)
    meshes["roof_downpipe_l"] = cylinder(-2.95, 1.45, 2.05, 0.055, 2.7, axis="y", segments=10)
    meshes["roof_downpipe_r"] = cylinder(2.95, 1.45, 2.05, 0.055, 2.7, axis="y", segments=10)
    # Gable ends — triangular silhouette from apron overview.
    meshes["gable_front_l"] = rotated_box(-1.65, 2.95, 2.05, 3.3, 0.1, 0.16, z_degrees=14.0)
    meshes["gable_front_r"] = rotated_box(1.65, 2.95, 2.05, 3.3, 0.1, 0.16, z_degrees=-14.0)
    meshes["gable_back_l"] = rotated_box(-1.65, 2.95, -2.05, 3.3, 0.1, 0.16, z_degrees=14.0)
    meshes["gable_back_r"] = rotated_box(1.65, 2.95, -2.05, 3.3, 0.1, 0.16, z_degrees=-14.0)
    meshes["gable_apex_front"] = box(0.0, 3.4, 2.05, 0.28, 0.28, 0.16)
    meshes["gable_apex_back"] = box(0.0, 3.4, -2.05, 0.28, 0.28, 0.16)
    # Stronger porch / entry.
    meshes["porch"] = box(0.0, 1.05, 2.4, 3.25, 2.1, 1.45)
    meshes["porch_roof"] = box(0.0, 2.3, 2.55, 3.7, 0.16, 1.75)
    meshes["porch_beam"] = box(0.0, 2.1, 3.05, 3.35, 0.12, 0.14)
    meshes["porch_fascia"] = box(0.0, 2.2, 3.35, 3.55, 0.14, 0.12)
    meshes["porch_soffit"] = box(0.0, 2.15, 2.6, 3.45, 0.06, 1.5)
    meshes["porch_post_l"] = cylinder(-1.35, 1.05, 3.05, 0.09, 2.0, axis="y", segments=10)
    meshes["porch_post_r"] = cylinder(1.35, 1.05, 3.05, 0.09, 2.0, axis="y", segments=10)
    meshes["porch_post_mid_l"] = cylinder(-0.7, 1.05, 3.05, 0.06, 2.0, axis="y", segments=8)
    meshes["porch_post_mid_r"] = cylinder(0.7, 1.05, 3.05, 0.06, 2.0, axis="y", segments=8)
    meshes["step"] = box(0.0, 0.16, 3.15, 1.55, 0.28, 0.6)
    meshes["step_rail_l"] = box(-0.72, 0.6, 3.2, 0.07, 0.8, 0.07)
    meshes["step_rail_r"] = box(0.72, 0.6, 3.2, 0.07, 0.8, 0.07)
    # Extra corrugation + cladding depth.
    for i, x in enumerate((-2.9, -2.2, -1.4, -0.6, 0.6, 1.4, 2.2, 2.9), start=12):
        meshes[f"wall_rib_{i}"] = box(x, 1.4, 2.04, 0.09, 2.55, 0.07)
    for i, z in enumerate((-1.7, -1.0, -0.3, 0.3, 1.0, 1.7), start=10):
        meshes[f"wall_rib_l_{i}"] = box(-3.06, 1.4, z, 0.07, 2.55, 0.09)
        meshes[f"wall_rib_r_{i}"] = box(3.06, 1.4, z, 0.07, 2.55, 0.09)
    meshes["cladding_face_front"] = box(0.0, 1.4, 2.02, 5.95, 2.55, 0.06)
    meshes["cladding_face_back"] = box(0.0, 1.4, -2.02, 5.95, 2.55, 0.06)
    meshes["cladding_face_l"] = box(-3.04, 1.4, 0.0, 0.07, 2.65, 3.95)
    meshes["cladding_face_r"] = box(3.04, 1.4, 0.0, 0.07, 2.65, 3.95)
    meshes["girth_band_1"] = box(0.0, 0.85, 0.0, 6.1, 0.09, 4.1)
    meshes["girth_band_2"] = box(0.0, 1.95, 0.0, 6.1, 0.09, 4.1)
    meshes["girth_band_3"] = box(0.0, 2.55, 0.0, 6.1, 0.08, 4.1)
    # Antenna / AC silhouette boost (overview readable).
    meshes["antenna_mast"] = cylinder(1.85, 3.95, -0.45, 0.055, 1.55, axis="y", segments=10)
    meshes["antenna_dish"] = cylinder(1.85, 4.65, -0.45, 0.28, 0.1, axis="y", segments=14)
    meshes["antenna_boom"] = box(1.85, 4.45, -0.25, 0.07, 0.07, 0.55)
    meshes["antenna_guy"] = box(1.85, 3.7, -0.1, 0.03, 1.1, 0.03)
    meshes["antenna_guy_b"] = box(1.55, 3.55, -0.15, 0.03, 0.95, 0.03)
    meshes["ac_unit"] = box(-1.55, 3.35, -0.75, 1.35, 0.5, 1.0)
    meshes["ac_unit_b"] = box(0.45, 3.3, -0.95, 1.0, 0.4, 0.8)
    meshes["ac_grille"] = box(-1.55, 3.35, -1.28, 1.15, 0.38, 0.08)
    meshes["radio_rack"] = box(-2.25, 1.45, -1.65, 0.9, 1.75, 0.55)
    meshes["radio_antenna_whip"] = cylinder(-2.25, 2.55, -1.65, 0.035, 1.05, axis="y", segments=8)
    # Signage / flood cans — Safety Yellow friendly masses.
    meshes["signage"] = box(0.0, 2.65, 2.15, 1.8, 0.4, 0.09)
    meshes["signage_glyph"] = box(0.0, 2.65, 2.2, 1.25, 0.2, 0.04)
    meshes["flood_can"] = box(-2.5, 2.85, 2.1, 0.28, 0.2, 0.22)
    meshes["flood_can_b"] = box(2.5, 2.85, 2.1, 0.28, 0.2, 0.22)
    meshes["plinth"] = box(0.0, 0.1, 0.0, 6.35, 0.22, 4.35)
    return {k: outward_winding(v) for k, v in meshes.items()}


def main() -> None:
    BUILDINGS.mkdir(parents=True, exist_ok=True)
    meshes = ops_shed_v05()
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
