#!/usr/bin/env python3
"""Generate Batch F2 vehicle + character kits (FBX + companion glTF).

Decision 0027 / Batch F packet — F2 turnaround read:
  VEH-001 v05  mdl_fuel_truck_small_v05
  VEH-002 v05  mdl_baggage_tug_train_v05
  VEH-003 v05  mdl_passenger_bus_apron_v05
  VEH-004      mdl_pushback_tug_v02
  CHR-001      mdl_ramp_crew_kit_v01
  CHR-002      mdl_passenger_kit_v01

Distinct ids — does not overwrite authored_v01 / v04. Motion part names stay
compatible with BuildServiceVehicle / UpdateApronLife / BuildPushbackTug.
ASCII FBX when assimp is unavailable (same as BLD-001 v05).
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
VEHICLES = ROOT / "Models" / "Vehicles"
CHARACTERS = ROOT / "Models" / "Characters"
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
    cx: float,
    cy: float,
    cz: float,
    radius: float,
    length: float,
    *,
    axis: str = "y",
    segments: int = 14,
) -> tuple[np.ndarray, np.ndarray]:
    segs = max(6, segments)
    rings = []
    if axis == "y":
        y0, y1 = cy - length * 0.5, cy + length * 0.5
        for y in (y0, y1):
            ring = []
            for i in range(segs):
                ang = 2.0 * np.pi * i / segs
                ring.append([cx + radius * np.cos(ang), y, cz + radius * np.sin(ang)])
            rings.append(np.asarray(ring, np.float32))
    elif axis == "z":
        z0, z1 = cz - length * 0.5, cz + length * 0.5
        for z in (z0, z1):
            ring = []
            for i in range(segs):
                ang = 2.0 * np.pi * i / segs
                ring.append([cx + radius * np.cos(ang), cy + radius * np.sin(ang), z])
            rings.append(np.asarray(ring, np.float32))
    else:  # x
        x0, x1 = cx - length * 0.5, cx + length * 0.5
        for x in (x0, x1):
            ring = []
            for i in range(segs):
                ang = 2.0 * np.pi * i / segs
                ring.append([x, cy + radius * np.sin(ang), cz + radius * np.cos(ang)])
            rings.append(np.asarray(ring, np.float32))

    verts: list = []
    indices: list = []
    for i in range(segs):
        j = (i + 1) % segs
        a, b = rings[0][i], rings[0][j]
        c, d = rings[1][j], rings[1][i]
        base = len(verts)
        verts.extend([a, b, c, d])
        indices.extend([base, base + 1, base + 2, base, base + 2, base + 3])
    for ring, outward in ((rings[0], -1), (rings[1], 1)):
        center = ring.mean(axis=0)
        for i in range(segs):
            j = (i + 1) % segs
            base = len(verts)
            if outward < 0:
                verts.extend([center, ring[j], ring[i]])
            else:
                verts.extend([center, ring[i], ring[j]])
            indices.extend([base, base + 1, base + 2])
    return np.asarray(verts, np.float32), np.asarray(indices, np.uint16)


def oval_tank(
    cx: float,
    cy: float,
    cz: float,
    length: float,
    rx: float,
    ry: float,
    *,
    stations: int = 10,
    segments: int = 18,
) -> tuple[np.ndarray, np.ndarray]:
    """Horizontal oval tank along X — readable cylindrical silhouette."""
    segs = max(10, segments)
    rings = []
    for s in range(stations):
        t = s / (stations - 1)
        x = cx - length * 0.5 + t * length
        # Soften ends slightly.
        taper = 1.0 - 0.08 * (abs(t - 0.5) * 2.0) ** 2
        ring = []
        for i in range(segs):
            ang = 2.0 * np.pi * i / segs
            ring.append(
                [
                    x,
                    cy + ry * taper * np.sin(ang),
                    cz + rx * taper * np.cos(ang),
                ]
            )
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
    for ring in (rings[0], rings[-1]):
        tip = ring.mean(axis=0).copy()
        for i in range(segs):
            j = (i + 1) % segs
            base = len(verts)
            verts.extend([tip, ring[i], ring[j]])
            indices.extend([base, base + 1, base + 2])
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
    lines = ["# Airside Batch F2"]
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


# ---------------------------------------------------------------------------
# VEH-001 — fuel truck v05 (Safety Yellow regional tanker)
# ---------------------------------------------------------------------------


def fuel_truck_v05() -> dict[str, tuple[np.ndarray, np.ndarray]]:
    meshes: dict[str, tuple[np.ndarray, np.ndarray]] = {
        # Cab — soft shell + readable glazing.
        "cab": box(1.05, 0.95, 0, 1.55, 1.55, 1.6),
        "cab_roof": box(1.05, 1.78, 0, 1.5, 0.14, 1.5),
        "cab_visor": box(1.62, 1.58, 0, 0.4, 0.08, 1.35),
        "cab_window": box(1.62, 1.28, 0, 0.04, 0.75, 1.25),
        "cab_door": box(1.05, 0.95, 0.82, 1.25, 1.25, 0.08),
        "cab_door_r": box(1.05, 0.95, -0.82, 1.25, 1.25, 0.08),
        "door_glass": box(1.05, 1.22, 0.84, 0.72, 0.58, 0.04),
        "door_glass_r": box(1.05, 1.22, -0.84, 0.72, 0.58, 0.04),
        "door_handle_l": box(1.38, 0.95, 0.88, 0.08, 0.16, 0.05),
        "door_handle_r": box(1.38, 0.95, -0.88, 0.08, 0.16, 0.05),
        "cab_stripe": box(1.05, 0.68, 0.84, 1.4, 0.12, 0.04),
        # Oval tank — signature silhouette upgrade over box densify.
        "tank": oval_tank(-0.5, 0.95, 0, 2.65, 0.68, 0.62, stations=12, segments=20),
        "tank_end_f": cylinder(0.82, 0.95, 0, 0.6, 0.18, axis="x", segments=18),
        "tank_end_r": cylinder(-1.78, 0.95, 0, 0.6, 0.18, axis="x", segments=18),
        "tank_band": box(-0.5, 0.95, 0, 2.7, 0.18, 1.42),
        "tank_band_2": box(-0.5, 0.95, 0, 0.14, 1.28, 1.28),
        "tank_band_3": box(0.35, 0.95, 0, 0.12, 1.22, 1.22),
        "tank_band_4": box(-1.3, 0.95, 0, 0.12, 1.22, 1.22),
        "tank_cap": cylinder(-0.5, 1.62, 0, 0.22, 0.18, axis="y", segments=12),
        "tank_cap_b": cylinder(0.3, 1.62, 0, 0.16, 0.14, axis="y", segments=10),
        "tank_valve_top": cylinder(-0.15, 1.72, 0, 0.07, 0.16, axis="y", segments=8),
        "tank_walkway": box(-0.5, 1.55, 0.58, 2.3, 0.05, 0.32),
        "tank_rail_l": box(-0.5, 1.62, 0.74, 2.1, 0.05, 0.05),
        "tank_rail_r": box(-0.5, 1.62, -0.74, 2.1, 0.05, 0.05),
        "tank_ladder": box(-1.55, 1.15, 0.78, 0.08, 1.05, 0.32),
        "tank_stripe": box(-0.5, 0.68, 0.74, 2.4, 0.12, 0.04),
        "fuel_hazard": box(-0.5, 1.25, 0.74, 0.55, 0.35, 0.04),
        # Chassis / wheels.
        "chassis": box(0.05, 0.34, 0, 3.75, 0.24, 1.12),
        "chassis_rail_l": box(0.05, 0.44, 0.52, 3.5, 0.1, 0.08),
        "chassis_rail_r": box(0.05, 0.44, -0.52, 3.5, 0.1, 0.08),
        "fender_fl": box(1.4, 0.48, 0.72, 0.75, 0.22, 0.22),
        "fender_fr": box(1.4, 0.48, -0.72, 0.75, 0.22, 0.22),
        "fender_rl": box(-1.2, 0.48, 0.72, 0.75, 0.22, 0.22),
        "fender_rr": box(-1.2, 0.48, -0.72, 0.75, 0.22, 0.22),
        "wheel_fl": cylinder(1.4, 0.28, 0.6, 0.3, 0.24, axis="z", segments=16),
        "wheel_fr": cylinder(1.4, 0.28, -0.6, 0.3, 0.24, axis="z", segments=16),
        "wheel_rl": cylinder(-1.2, 0.28, 0.6, 0.3, 0.24, axis="z", segments=16),
        "wheel_rr": cylinder(-1.2, 0.28, -0.6, 0.3, 0.24, axis="z", segments=16),
        "hub_fl": cylinder(1.4, 0.28, 0.6, 0.12, 0.12, axis="z", segments=10),
        "hub_fr": cylinder(1.4, 0.28, -0.6, 0.12, 0.12, axis="z", segments=10),
        "hub_rl": cylinder(-1.2, 0.28, 0.6, 0.12, 0.12, axis="z", segments=10),
        "hub_rr": cylinder(-1.2, 0.28, -0.6, 0.12, 0.12, axis="z", segments=10),
        "mudflap_l": box(-1.2, 0.22, 0.78, 0.35, 0.35, 0.04),
        "mudflap_r": box(-1.2, 0.22, -0.78, 0.35, 0.35, 0.04),
        "spare_wheel": cylinder(-1.75, 0.9, 0.0, 0.24, 0.12, axis="z", segments=14),
        # Hose reel / pump — separated motion roots.
        "hose_mount": box(-1.62, 0.72, 0.78, 0.42, 0.42, 0.42),
        "hose_reel": cylinder(-1.62, 0.88, 0.38, 0.3, 0.38, axis="z", segments=14),
        "hose_coil_a": cylinder(-1.82, 0.72, 0.22, 0.13, 0.18, axis="z", segments=12),
        "hose_coil_b": cylinder(-1.82, 0.72, 0.02, 0.13, 0.18, axis="z", segments=12),
        "hose_coil_c": cylinder(-1.82, 0.72, -0.18, 0.12, 0.16, axis="z", segments=12),
        "hose": cylinder(-1.78, 0.55, 0.58, 0.065, 0.75, axis="z", segments=10),
        "hose_nozzle": box(-1.95, 0.52, 0.78, 0.28, 0.18, 0.18),
        "hose_guard": box(-1.78, 0.72, 0.78, 0.52, 0.52, 0.08),
        "hose_tray": box(-1.62, 0.52, 0.55, 0.55, 0.12, 0.55),
        "pump_cabinet": box(-1.62, 0.88, -0.55, 0.58, 0.72, 0.58),
        "pump_cabinet_door": box(-1.62, 0.88, -0.82, 0.5, 0.62, 0.05),
        "pump_gauge": box(-1.62, 1.12, -0.86, 0.22, 0.18, 0.05),
        "pump_valve": cylinder(-1.62, 0.52, -0.55, 0.08, 0.22, axis="y", segments=8),
        "pump_hose_out": box(-1.92, 0.52, -0.55, 0.35, 0.1, 0.1),
        # Lights / trim.
        "bumper": box(1.92, 0.4, 0, 0.22, 0.35, 1.45),
        "bumper_rear": box(-1.95, 0.4, 0, 0.22, 0.35, 1.35),
        "grill": box(1.95, 0.88, 0, 0.08, 0.48, 1.05),
        "light_bar": box(1.88, 0.58, 0, 0.12, 0.16, 1.25),
        "headlight_l": box(2.02, 0.68, 0.42, 0.1, 0.14, 0.16),
        "headlight_r": box(2.02, 0.68, -0.42, 0.1, 0.14, 0.16),
        "taillight_l": box(-2.05, 0.55, 0.48, 0.08, 0.12, 0.12),
        "taillight_r": box(-2.05, 0.55, -0.48, 0.08, 0.12, 0.12),
        "beacon": box(1.05, 1.92, 0, 0.26, 0.2, 0.26),
        "beacon_guard": box(1.05, 2.02, 0, 0.36, 0.06, 0.36),
        "mirror_l": box(1.75, 1.38, 0.9, 0.12, 0.26, 0.18),
        "mirror_r": box(1.75, 1.38, -0.9, 0.12, 0.26, 0.18),
        "step": box(1.58, 0.45, 0.9, 0.35, 0.14, 0.32),
        "step_r": box(1.58, 0.45, -0.9, 0.35, 0.14, 0.32),
        "exhaust": cylinder(-1.65, 1.4, -0.58, 0.06, 0.95, axis="y", segments=8),
        "number_plate": box(2.02, 0.52, 0, 0.04, 0.16, 0.42),
        "wiper": box(1.78, 1.55, 0, 0.06, 0.05, 0.75),
        "window_mullion": box(1.63, 1.28, 0, 0.04, 0.75, 0.05),
        "window_mullion_2": box(1.63, 1.28, 0.42, 0.04, 0.75, 0.05),
        "window_mullion_3": box(1.63, 1.28, -0.42, 0.04, 0.75, 0.05),
        "window_sill": box(1.63, 0.9, 0, 0.05, 0.05, 1.2),
        "window_header": box(1.63, 1.66, 0, 0.05, 0.05, 1.2),
    }
    for i, z in enumerate([-0.48, -0.16, 0.16, 0.48], start=1):
        meshes[f"glass_pane_{i}"] = box(1.64, 1.42, z, 0.04, 0.38, 0.3)
        meshes[f"glass_pane_lo_{i}"] = box(1.64, 1.12, z, 0.04, 0.28, 0.3)
    # Yellow chevron bands on tank ends for overview read.
    for i, x in enumerate([0.55, -1.55], start=1):
        meshes[f"hazard_chevron_{i}"] = box(x, 0.95, 0.72, 0.35, 0.55, 0.04)
    return meshes


# ---------------------------------------------------------------------------
# VEH-002 — baggage tug + three carts v05
# ---------------------------------------------------------------------------


def baggage_tug_v05() -> dict[str, tuple[np.ndarray, np.ndarray]]:
    meshes: dict[str, tuple[np.ndarray, np.ndarray]] = {
        "tug": box(3.55, 0.52, 0, 1.75, 0.9, 1.2),
        "tug_cab": box(4.15, 0.98, 0, 0.95, 0.9, 1.1),
        "tug_window": box(4.58, 1.2, 0, 0.04, 0.5, 0.9),
        "tug_bumper": box(4.62, 0.4, 0, 0.22, 0.35, 1.05),
        "tug_seat": box(3.9, 0.88, 0, 0.48, 0.38, 0.72),
        "tug_rollbar": box(3.72, 1.42, 0, 0.08, 0.78, 0.95),
        "tug_rollbar_top": box(3.9, 1.78, 0, 0.55, 0.08, 0.95),
        "tug_floor": box(3.95, 0.34, 0, 0.95, 0.08, 1.05),
        "tug_steering": box(4.3, 1.08, 0.15, 0.12, 0.38, 0.12),
        "tug_steering_wheel": cylinder(4.35, 1.15, 0.15, 0.14, 0.04, axis="x", segments=12),
        "counterweight": box(3.1, 0.55, 0, 0.5, 0.55, 0.95),
        "tug_stripe": box(3.55, 0.55, 0.62, 1.55, 0.12, 0.04),
        "tug_mirror_l": box(4.45, 1.25, 0.58, 0.1, 0.18, 0.08),
        "tug_mirror_r": box(4.45, 1.25, -0.58, 0.1, 0.18, 0.08),
        "beacon": box(4.15, 1.55, 0, 0.22, 0.16, 0.22),
        "headlight_l": box(4.68, 0.68, 0.38, 0.1, 0.12, 0.14),
        "headlight_r": box(4.68, 0.68, -0.38, 0.1, 0.12, 0.14),
        "taillight_l": box(2.95, 0.55, 0.58, 0.08, 0.1, 0.1),
        "taillight_r": box(2.95, 0.55, -0.58, 0.08, 0.1, 0.1),
        "number_plate": box(4.72, 0.5, 0, 0.04, 0.14, 0.35),
        "tug_exhaust": box(3.0, 0.7, -0.48, 0.28, 0.1, 0.1),
        "wheel_fl": cylinder(4.05, 0.22, 0.52, 0.22, 0.2, axis="z", segments=14),
        "wheel_fr": cylinder(4.05, 0.22, -0.52, 0.22, 0.2, axis="z", segments=14),
        "wheel_rl": cylinder(3.15, 0.22, 0.52, 0.22, 0.2, axis="z", segments=14),
        "wheel_rr": cylinder(3.15, 0.22, -0.52, 0.22, 0.2, axis="z", segments=14),
        "tug_hub_fl": cylinder(4.05, 0.22, 0.52, 0.1, 0.1, axis="z", segments=8),
        "tug_hub_fr": cylinder(4.05, 0.22, -0.52, 0.1, 0.1, axis="z", segments=8),
        "tug_hub_rl": cylinder(3.15, 0.22, 0.52, 0.1, 0.1, axis="z", segments=8),
        "tug_hub_rr": cylinder(3.15, 0.22, -0.52, 0.1, 0.1, axis="z", segments=8),
        "glass_pane_1": box(4.59, 1.3, -0.28, 0.04, 0.3, 0.38),
        "glass_pane_2": box(4.59, 1.3, 0.28, 0.04, 0.3, 0.38),
        "glass_pane_lo_1": box(4.59, 1.02, -0.28, 0.04, 0.22, 0.38),
        "glass_pane_lo_2": box(4.59, 1.02, 0.28, 0.04, 0.22, 0.38),
        "window_mullion": box(4.59, 1.18, 0, 0.04, 0.5, 0.04),
    }
    # Three articulated carts with removable cargo + hitch pivots.
    cart_xs = (1.8, 0.15, -1.5)
    for idx, cx in enumerate(cart_xs, start=1):
        meshes[f"cart_{idx}"] = box(cx, 0.52, 0, 1.55, 0.78, 1.08)
        meshes[f"cart_bed_{idx}"] = box(cx, 0.22, 0, 1.48, 0.08, 1.02)
        meshes[f"cart_rail_{idx}"] = box(cx, 0.88, 0.5, 1.42, 0.08, 0.08)
        meshes[f"cart_rail_{idx}b"] = box(cx, 0.88, -0.5, 1.42, 0.08, 0.08)
        meshes[f"cart_gate_{idx}"] = box(cx, 0.72, -0.58, 1.35, 0.48, 0.06)
        meshes[f"cart_canopy_{idx}"] = box(cx, 1.28, 0, 1.4, 0.06, 0.98)
        meshes[f"cart_post_{idx}l"] = box(cx - 0.55, 0.98, 0.45, 0.05, 0.58, 0.05)
        meshes[f"cart_post_{idx}r"] = box(cx - 0.55, 0.98, -0.45, 0.05, 0.58, 0.05)
        meshes[f"cart_post_{idx}fl"] = box(cx + 0.55, 0.98, 0.45, 0.05, 0.58, 0.05)
        meshes[f"cart_post_{idx}fr"] = box(cx + 0.55, 0.98, -0.45, 0.05, 0.58, 0.05)
        meshes[f"cargo_{idx}"] = box(cx, 0.98, 0, 1.22, 0.48, 0.88)
        meshes[f"cargo_bag_{idx}a"] = box(cx - 0.3, 1.08, 0.2, 0.42, 0.28, 0.35)
        meshes[f"cargo_bag_{idx}b"] = box(cx + 0.3, 1.08, -0.18, 0.4, 0.26, 0.32)
        meshes[f"cargo_bag_{idx}c"] = box(cx, 1.18, 0.0, 0.36, 0.24, 0.3)
        meshes[f"cart_wheel_{idx}l"] = cylinder(cx, 0.18, 0.5, 0.17, 0.15, axis="z", segments=12)
        meshes[f"cart_wheel_{idx}r"] = cylinder(cx, 0.18, -0.5, 0.17, 0.15, axis="z", segments=12)
        meshes[f"cart_hub_{idx}l"] = cylinder(cx, 0.18, 0.5, 0.08, 0.08, axis="z", segments=8)
        meshes[f"cart_hub_{idx}r"] = cylinder(cx, 0.18, -0.5, 0.08, 0.08, axis="z", segments=8)
    hitch_xs = (2.7, 0.95, -0.65)
    for i, hx in enumerate(hitch_xs, start=1):
        meshes[f"hitch_{i}"] = box(hx, 0.35, 0, 0.48, 0.2, 0.22)
        meshes[f"hitch_pin_{i}"] = cylinder(hx, 0.48, 0, 0.045, 0.2, axis="y", segments=8)
        meshes[f"tow_pivot_{i}"] = cylinder(hx, 0.35, 0, 0.08, 0.12, axis="y", segments=10)
    meshes["cargo_tag_1"] = box(1.8, 1.18, 0.42, 0.35, 0.12, 0.04)
    meshes["cargo_tag_2"] = box(0.15, 1.18, 0.42, 0.35, 0.12, 0.04)
    return meshes


# ---------------------------------------------------------------------------
# VEH-003 — apron passenger bus v05 (Coastal Blue accent)
# ---------------------------------------------------------------------------


def passenger_bus_v05() -> dict[str, tuple[np.ndarray, np.ndarray]]:
    meshes: dict[str, tuple[np.ndarray, np.ndarray]] = {
        "bus_body": box(0, 0.98, 0, 4.5, 1.7, 1.7),
        "cabin_roof": box(0, 1.95, 0, 4.3, 0.28, 1.58),
        # Rounded nose / tail — silhouette upgrade.
        "nose_round": cylinder(2.15, 0.95, 0, 0.82, 1.55, axis="x", segments=16),
        "tail_round": cylinder(-2.15, 0.95, 0, 0.78, 1.5, axis="x", segments=16),
        "windows": box(0, 1.38, 0, 3.9, 0.58, 1.68),
        "windshield": box(2.28, 1.4, 0, 0.05, 0.75, 1.4),
        "rear_window": box(-2.28, 1.4, 0, 0.05, 0.6, 1.25),
        "door": box(0.3, 0.98, 0.88, 1.1, 1.4, 0.1),
        "door_frame": box(0.3, 0.98, 0.95, 1.2, 1.5, 0.06),
        "door_glass": box(0.3, 1.28, 0.93, 0.72, 0.58, 0.05),
        "door_handle": box(0.62, 1.02, 0.98, 0.08, 0.2, 0.06),
        "door_hinge_t": box(-0.18, 1.48, 0.96, 0.08, 0.12, 0.08),
        "door_hinge_b": box(-0.18, 0.55, 0.96, 0.08, 0.12, 0.08),
        "bumper_front": box(2.35, 0.42, 0, 0.28, 0.42, 1.55),
        "bumper_rear": box(-2.35, 0.42, 0, 0.28, 0.42, 1.55),
        "wheel_fl": cylinder(1.5, 0.3, 0.75, 0.3, 0.26, axis="z", segments=16),
        "wheel_fr": cylinder(1.5, 0.3, -0.75, 0.3, 0.26, axis="z", segments=16),
        "wheel_rl": cylinder(-1.5, 0.3, 0.75, 0.3, 0.26, axis="z", segments=16),
        "wheel_rr": cylinder(-1.5, 0.3, -0.75, 0.3, 0.26, axis="z", segments=16),
        "wheel_hub_fl": cylinder(1.5, 0.3, 0.75, 0.14, 0.14, axis="z", segments=10),
        "wheel_hub_fr": cylinder(1.5, 0.3, -0.75, 0.14, 0.14, axis="z", segments=10),
        "wheel_hub_rl": cylinder(-1.5, 0.3, 0.75, 0.14, 0.14, axis="z", segments=10),
        "wheel_hub_rr": cylinder(-1.5, 0.3, -0.75, 0.14, 0.14, axis="z", segments=10),
        "wheel_arch_fl": box(1.5, 0.58, 0.82, 0.75, 0.38, 0.14),
        "wheel_arch_fr": box(1.5, 0.58, -0.82, 0.75, 0.38, 0.14),
        "wheel_arch_rl": box(-1.5, 0.58, 0.82, 0.75, 0.38, 0.14),
        "wheel_arch_rr": box(-1.5, 0.58, -0.82, 0.75, 0.38, 0.14),
        "beacon": box(0, 2.2, 0, 0.3, 0.2, 0.3),
        "mirror_l": box(2.18, 1.58, 0.95, 0.12, 0.32, 0.2),
        "mirror_r": box(2.18, 1.58, -0.95, 0.12, 0.32, 0.2),
        "step": box(0.3, 0.32, 1.0, 0.95, 0.14, 0.38),
        "headlight_l": box(2.42, 0.72, 0.55, 0.12, 0.18, 0.2),
        "headlight_r": box(2.42, 0.72, -0.55, 0.12, 0.18, 0.2),
        "taillight_l": box(-2.42, 0.72, 0.55, 0.1, 0.16, 0.16),
        "taillight_r": box(-2.42, 0.72, -0.55, 0.1, 0.16, 0.16),
        "stripe": box(0, 0.72, 0.86, 4.1, 0.14, 0.06),
        "stripe_b": box(0, 0.72, -0.86, 4.1, 0.14, 0.06),
        "stripe_upper": box(0, 1.78, 0.88, 4.1, 0.08, 0.04),
        "body_panel_l": box(0, 0.52, 0.88, 4.1, 0.38, 0.05),
        "body_panel_r": box(0, 0.52, -0.88, 4.1, 0.38, 0.05),
        "skirt_l": box(0, 0.26, 0.8, 3.7, 0.12, 0.06),
        "skirt_r": box(0, 0.26, -0.8, 3.7, 0.12, 0.06),
        "wiper": box(2.22, 1.58, 0, 0.08, 0.06, 0.95),
        "wiper_b": box(2.22, 1.48, 0.22, 0.06, 0.05, 0.7),
        "roof_rack": box(0, 2.18, 0, 3.3, 0.08, 0.95),
        "roof_vent": box(-0.8, 2.15, 0, 0.48, 0.14, 0.48),
        "grill": box(2.45, 0.88, 0, 0.08, 0.38, 0.95),
        "number_plate": box(2.48, 0.48, 0, 0.04, 0.16, 0.4),
        "mudflap_l": box(-1.5, 0.22, 0.88, 0.4, 0.35, 0.04),
        "mudflap_r": box(-1.5, 0.22, -0.88, 0.4, 0.35, 0.04),
        "destination_board": box(2.22, 1.9, 0, 0.08, 0.24, 0.95),
        "destination_board_hood": box(2.18, 2.04, 0, 0.12, 0.06, 1.0),
        "destination_digit": box(2.26, 1.9, 0, 0.04, 0.16, 0.55),
        "exhaust_pipe": box(-2.22, 0.32, -0.55, 0.35, 0.1, 0.1),
        "fuel_filler": box(-2.22, 0.98, 0.72, 0.12, 0.12, 0.12),
        "seat_row_1": box(-0.85, 0.88, 0.38, 1.85, 0.48, 0.48),
        "seat_row_2": box(-0.85, 0.88, -0.38, 1.85, 0.48, 0.48),
        "seat_row_3": box(0.65, 0.88, 0.38, 1.25, 0.48, 0.48),
        "seat_row_4": box(0.65, 0.88, -0.38, 1.25, 0.48, 0.48),
        "seat_back_1": box(-0.85, 1.2, 0.55, 1.85, 0.38, 0.08),
        "seat_back_2": box(-0.85, 1.2, -0.55, 1.85, 0.38, 0.08),
        "window_sill": box(0, 1.08, 0.9, 3.9, 0.06, 0.06),
        "window_sill_b": box(0, 1.08, -0.9, 3.9, 0.06, 0.06),
        "window_header": box(0, 1.7, 0.9, 3.9, 0.06, 0.06),
        "window_header_b": box(0, 1.7, -0.9, 3.9, 0.06, 0.06),
    }
    for i, x in enumerate([-2.0, -1.35, -0.7, 0.0, 0.95, 1.55, 2.05], start=1):
        meshes[f"window_mullion_{i}"] = box(x, 1.38, 0.9, 0.07, 0.58, 0.06)
        meshes[f"window_mullion_r{i}"] = box(x, 1.38, -0.9, 0.07, 0.58, 0.06)
    pane_xs = [-1.85, -1.5, -0.95, -0.35, 0.95, 1.55, 1.9]
    for i, x in enumerate(pane_xs, start=1):
        meshes[f"glass_pane_{i}"] = box(x, 1.38, 0.9, 0.48, 0.5, 0.04)
        meshes[f"glass_pane_lo_{i}"] = box(x, 1.38, -0.9, 0.48, 0.5, 0.04)
    for i, z in enumerate([-0.48, 0.0, 0.48], start=1):
        meshes[f"glass_pane_front_{i}"] = box(2.3, 1.4, z, 0.04, 0.6, 0.42)
    return meshes


# ---------------------------------------------------------------------------
# VEH-004 — pushback tug v02 (dedicated vehicle, not towbar-only kit)
# ---------------------------------------------------------------------------


def pushback_tug_v02() -> dict[str, tuple[np.ndarray, np.ndarray]]:
    meshes: dict[str, tuple[np.ndarray, np.ndarray]] = {
        "tug_body": box(0.15, 0.48, 0, 2.35, 0.85, 1.25),
        "tug_cab": box(0.7, 0.95, 0, 0.95, 0.75, 1.1),
        "tug_seat": box(0.55, 0.85, 0, 0.45, 0.35, 0.7),
        "tug_rollbar": box(0.35, 1.35, 0, 0.08, 0.65, 0.95),
        "tug_rollbar_top": box(0.55, 1.65, 0, 0.55, 0.08, 0.95),
        "tug_floor": box(0.7, 0.28, 0, 0.9, 0.08, 1.05),
        "tug_steering": box(0.95, 1.05, 0.12, 0.1, 0.32, 0.1),
        "counterweight": box(-0.75, 0.55, 0, 0.7, 0.65, 1.05),
        "bumper_front": box(1.25, 0.38, 0, 0.2, 0.32, 1.15),
        "bumper_rear": box(-1.15, 0.38, 0, 0.2, 0.32, 1.1),
        "wheel_fl": cylinder(0.75, 0.22, 0.52, 0.24, 0.2, axis="z", segments=14),
        "wheel_fr": cylinder(0.75, 0.22, -0.52, 0.24, 0.2, axis="z", segments=14),
        "wheel_rl": cylinder(-0.65, 0.22, 0.52, 0.24, 0.2, axis="z", segments=14),
        "wheel_rr": cylinder(-0.65, 0.22, -0.52, 0.24, 0.2, axis="z", segments=14),
        "hub_fl": cylinder(0.75, 0.22, 0.52, 0.1, 0.1, axis="z", segments=8),
        "hub_fr": cylinder(0.75, 0.22, -0.52, 0.1, 0.1, axis="z", segments=8),
        "hub_rl": cylinder(-0.65, 0.22, 0.52, 0.1, 0.1, axis="z", segments=8),
        "hub_rr": cylinder(-0.65, 0.22, -0.52, 0.1, 0.1, axis="z", segments=8),
        "beacon": box(0.7, 1.45, 0, 0.2, 0.15, 0.2),
        "headlight_l": box(1.32, 0.55, 0.4, 0.1, 0.12, 0.14),
        "headlight_r": box(1.32, 0.55, -0.4, 0.1, 0.12, 0.14),
        "taillight_l": box(-1.22, 0.5, 0.45, 0.08, 0.1, 0.1),
        "taillight_r": box(-1.22, 0.5, -0.45, 0.08, 0.1, 0.1),
        "stripe": box(0.15, 0.55, 0.64, 2.1, 0.12, 0.04),
        "number_plate": box(1.35, 0.42, 0, 0.04, 0.12, 0.32),
        # Towbar assembly — names match existing BuildPushbackTug hooks.
        "towbar": box(-1.85, 0.28, 0, 1.35, 0.12, 0.12),
        "towbar_head": box(-2.55, 0.32, 0, 0.35, 0.28, 0.35),
        "towbar_eye": cylinder(-2.72, 0.32, 0, 0.1, 0.08, axis="z", segments=10),
        "towbar_wheel": cylinder(-2.15, 0.16, 0, 0.14, 0.12, axis="z", segments=12),
        "towbar_handle": box(-1.35, 0.48, 0.18, 0.35, 0.08, 0.08),
        "tow_pivot": cylinder(-1.2, 0.28, 0, 0.1, 0.16, axis="y", segments=10),
        "glass_pane_1": box(1.05, 1.15, 0, 0.04, 0.35, 0.7),
    }
    return meshes


# ---------------------------------------------------------------------------
# CHR-001 / CHR-002 — ramp crew + passengers (shared low-detail figures)
# ---------------------------------------------------------------------------


def _figure_parts(
    prefix: str,
    *,
    seated: bool = False,
    hi_vis: bool = False,
    wand: bool = False,
    scale: float = 1.0,
) -> dict[str, tuple[np.ndarray, np.ndarray]]:
    """Named parts UpdateApronLife / PlacePerson can find via substring."""
    s = scale
    body_h = (0.55 if seated else 0.85) * s
    body_y = (0.55 if seated else 0.9) * s
    meshes: dict[str, tuple[np.ndarray, np.ndarray]] = {
        f"{prefix}_torso": box(0, body_y, 0, 0.38 * s, body_h, 0.22 * s),
        f"{prefix}_head": cylinder(0, body_y + body_h * 0.55 + 0.16 * s, 0, 0.11 * s, 0.2 * s, axis="y", segments=10),
    }
    if hi_vis:
        meshes[f"{prefix}_vest"] = box(0, body_y + 0.05 * s, 0.12 * s, 0.36 * s, 0.12 * s, 0.04 * s)
        meshes[f"{prefix}_hat"] = cylinder(
            0, body_y + body_h * 0.55 + 0.28 * s, 0, 0.13 * s, 0.1 * s, axis="y", segments=10
        )
    if not seated:
        meshes[f"{prefix}_leg_l"] = cylinder(-0.1 * s, 0.35 * s, 0, 0.07 * s, 0.7 * s, axis="y", segments=8)
        meshes[f"{prefix}_leg_r"] = cylinder(0.1 * s, 0.35 * s, 0, 0.07 * s, 0.7 * s, axis="y", segments=8)
        meshes[f"{prefix}_arm_l"] = cylinder(-0.28 * s, body_y + 0.02 * s, 0, 0.055 * s, 0.55 * s, axis="y", segments=8)
        meshes[f"{prefix}_arm_r"] = cylinder(0.28 * s, body_y + 0.02 * s, 0, 0.055 * s, 0.55 * s, axis="y", segments=8)
        meshes[f"{prefix}_shoe_l"] = box(-0.1 * s, 0.05 * s, 0.04 * s, 0.14 * s, 0.08 * s, 0.22 * s)
        meshes[f"{prefix}_shoe_r"] = box(0.1 * s, 0.05 * s, 0.04 * s, 0.14 * s, 0.08 * s, 0.22 * s)
        if wand:
            meshes[f"{prefix}_wand"] = box(0.42 * s, body_y + 0.35 * s, 0.05 * s, 0.05 * s, 0.55 * s, 0.05 * s)
            meshes[f"{prefix}_wand_tip"] = box(0.42 * s, body_y + 0.65 * s, 0.05 * s, 0.08 * s, 0.08 * s, 0.08 * s)
    else:
        meshes[f"{prefix}_legs"] = box(0, 0.28 * s, 0.2 * s, 0.4 * s, 0.2 * s, 0.55 * s)
        meshes[f"{prefix}_arm_l"] = box(-0.28 * s, body_y, 0.05 * s, 0.12 * s, 0.4 * s, 0.12 * s)
        meshes[f"{prefix}_arm_r"] = box(0.28 * s, body_y, 0.05 * s, 0.12 * s, 0.4 * s, 0.12 * s)
    return meshes


def ramp_crew_kit() -> dict[str, tuple[np.ndarray, np.ndarray]]:
    # Figures share the origin — PlacePerson extracts named parts only.
    meshes: dict[str, tuple[np.ndarray, np.ndarray]] = {}
    roles = [
        ("marshaller", True, True),
        ("fueler", True, False),
        ("ramp", True, False),
    ]
    for role, hi, wand in roles:
        meshes.update(_figure_parts(role, seated=False, hi_vis=hi, wand=wand))
    return meshes


def passenger_kit() -> dict[str, tuple[np.ndarray, np.ndarray]]:
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
        meshes.update(_figure_parts(prefix, seated=seated, hi_vis=False, wand=False, scale=scale))
    return meshes


def main() -> None:
    VEHICLES.mkdir(parents=True, exist_ok=True)
    CHARACTERS.mkdir(parents=True, exist_ok=True)

    write_kit(VEHICLES, "mdl_fuel_truck_small_v05", fuel_truck_v05())
    write_kit(VEHICLES, "mdl_baggage_tug_train_v05", baggage_tug_v05())
    write_kit(VEHICLES, "mdl_passenger_bus_apron_v05", passenger_bus_v05())
    write_kit(VEHICLES, "mdl_pushback_tug_v02", pushback_tug_v02())
    write_kit(CHARACTERS, "mdl_ramp_crew_kit_v01", ramp_crew_kit())
    write_kit(CHARACTERS, "mdl_passenger_kit_v01", passenger_kit())


if __name__ == "__main__":
    main()
