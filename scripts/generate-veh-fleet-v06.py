#!/usr/bin/env python3
"""Generate landside car v02 + turnaround fleet v06 (FBX + companion glTF).

REF-003 / REF-005 fidelity jump over Batch F2 v05 / parked-car cube proof:
  mdl_parked_car_v02              — stylised regional car kit
  mdl_fuel_truck_small_v06        — VEH-001
  mdl_baggage_tug_train_v06       — VEH-002
  mdl_passenger_bus_apron_v06     — VEH-003
  mdl_pushback_tug_v03            — VEH-004

Distinct ids — does not overwrite v05 / pushback v02 / parked v01.
Motion part names stay compatible with BuildServiceVehicle / BuildPushbackTug.
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
VEHICLES = ROOT / "Models" / "Vehicles"

_batch_spec = importlib.util.spec_from_file_location(
    "batch_c_v01", REPO / "scripts/generate-batch-c-models.py"
)
_batch = importlib.util.module_from_spec(_batch_spec)
assert _batch_spec.loader is not None
_batch_spec.loader.exec_module(_batch)
_batch.ROOT = ROOT

_f2_spec = importlib.util.spec_from_file_location(
    "batch_f2", REPO / "scripts/generate-batch-f2-vehicles-characters.py"
)
_f2 = importlib.util.module_from_spec(_f2_spec)
assert _f2_spec.loader is not None
_f2_spec.loader.exec_module(_f2)

_ascii_spec = importlib.util.spec_from_file_location(
    "ascii_fbx", REPO / "scripts/write_ascii_fbx.py"
)
_ascii = importlib.util.module_from_spec(_ascii_spec)
assert _ascii_spec.loader is not None
_ascii_spec.loader.exec_module(_ascii)

box = _batch.box
pack_gltf = _batch.pack_gltf
write_default_meta = _batch.write_default_meta
cylinder = _f2.cylinder
oval_tank = _f2.oval_tank
write_fbx_model_meta = _f2.write_fbx_model_meta
write_obj = _f2.write_obj


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


def loft_profile_z(
    stations: list[tuple[float, float, float, float, float]],
    *,
    segments: int = 20,
) -> tuple[np.ndarray, np.ndarray]:
    """stations: (z, cx, cy, rx, ry) — oval cross-section along Z (car length)."""
    segs = max(10, segments)
    rings = []
    for z, cx, cy, rx, ry in stations:
        ring = []
        for i in range(segs):
            ang = 2.0 * np.pi * i / segs
            # Flatten underside slightly so cars sit on tyres.
            sy = np.sin(ang)
            if sy < 0:
                sy *= 0.55
            ring.append([cx + rx * np.cos(ang), cy + ry * sy, z])
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
    for ring, sign in ((rings[0], -1.0), (rings[-1], 1.0)):
        tip = ring.mean(axis=0).copy()
        tip[2] += sign * 0.04
        for i in range(segs):
            j = (i + 1) % segs
            base = len(verts)
            if sign < 0:
                verts.extend([tip, ring[j], ring[i]])
            else:
                verts.extend([tip, ring[i], ring[j]])
            indices.extend([base, base + 1, base + 2])
    return outward_winding((np.asarray(verts, np.float32), np.asarray(indices, np.uint16)))


def soft_box(
    cx: float,
    cy: float,
    cz: float,
    sx: float,
    sy: float,
    sz: float,
) -> tuple[np.ndarray, np.ndarray]:
    """Single closed box with outward winding — use loft/cylinder for true soft forms."""
    return outward_winding(box(cx, cy, cz, sx, sy, sz))

def tilted_pane(
    cx: float,
    cy: float,
    cz: float,
    sx: float,
    sy: float,
    sz: float,
    *,
    pitch_deg: float = 0.0,
) -> tuple[np.ndarray, np.ndarray]:
    verts, indices = box(cx, cy, cz, sx, sy, sz)
    pitch = np.deg2rad(pitch_deg)
    ca, sa = float(np.cos(pitch)), float(np.sin(pitch))
    out = verts.copy()
    for i, v in enumerate(verts):
        y, z = v[1] - cy, v[2] - cz
        out[i, 1] = cy + y * ca - z * sa
        out[i, 2] = cz + y * sa + z * ca
    return outward_winding((out, indices))


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
    # Ensure outward winding on every part.
    clean = {k: outward_winding(v) for k, v in meshes.items()}
    pack_gltf(gltf, clean)
    export_fbx(clean, fbx)
    for meta, text in preserved.items():
        meta.write_text(text, encoding="utf-8")
    if not Path(str(gltf) + ".meta").exists():
        write_default_meta(gltf)
    if not Path(str(bin_path) + ".meta").exists():
        write_default_meta(bin_path)
    print(f"Wrote {basename} ({len(clean)} meshes)")


# ---------------------------------------------------------------------------
# Landside parked car v02 — Australian-practical regional hatch / sedan
# Body length along Z to match PlaceParkedCar yaw conventions.
# ---------------------------------------------------------------------------


def parked_car_v02() -> dict[str, tuple[np.ndarray, np.ndarray]]:
    meshes: dict[str, tuple[np.ndarray, np.ndarray]] = {
        # Soft lofted body shell — believable silhouette, not a cube.
        "car_body": loft_profile_z(
            [
                (1.85, 0.0, 0.42, 0.55, 0.22),  # nose tip
                (1.55, 0.0, 0.48, 0.78, 0.28),
                (1.15, 0.0, 0.52, 0.86, 0.32),  # hood
                (0.55, 0.0, 0.58, 0.88, 0.38),  # A-pillar base
                (0.05, 0.0, 0.62, 0.88, 0.42),  # greenhouse
                (-0.55, 0.0, 0.62, 0.88, 0.42),
                (-1.05, 0.0, 0.58, 0.86, 0.36),  # C-pillar
                (-1.45, 0.0, 0.52, 0.82, 0.30),  # boot
                (-1.85, 0.0, 0.44, 0.70, 0.24),
            ],
            segments=22,
        ),
        "car_hood": loft_profile_z(
            [
                (1.55, 0.0, 0.62, 0.72, 0.08),
                (1.15, 0.0, 0.68, 0.78, 0.10),
                (0.65, 0.0, 0.72, 0.80, 0.10),
            ],
            segments=16,
        ),
        "car_roof": loft_profile_z(
            [
                (0.45, 0.0, 1.05, 0.70, 0.10),
                (0.0, 0.0, 1.12, 0.72, 0.12),
                (-0.55, 0.0, 1.08, 0.70, 0.10),
                (-0.95, 0.0, 0.98, 0.62, 0.08),
            ],
            segments=16,
        ),
        "car_boot": loft_profile_z(
            [
                (-1.05, 0.0, 0.62, 0.78, 0.12),
                (-1.45, 0.0, 0.58, 0.76, 0.14),
                (-1.75, 0.0, 0.50, 0.68, 0.10),
            ],
            segments=14,
        ),
        # Glass — separate readable panes (names hit Glass SurfaceKind).
        "glass_front": tilted_pane(0.0, 1.02, 0.62, 1.28, 0.38, 0.06, pitch_deg=-28.0),
        "glass_rear": tilted_pane(0.0, 1.00, -1.05, 1.22, 0.34, 0.06, pitch_deg=22.0),
        "glass_side_l": box(-0.82, 0.98, -0.15, 0.05, 0.32, 1.15),
        "glass_side_r": box(0.82, 0.98, -0.15, 0.05, 0.32, 1.15),
        "glass_pane_fl": box(-0.82, 0.98, 0.35, 0.05, 0.30, 0.42),
        "glass_pane_fr": box(0.82, 0.98, 0.35, 0.05, 0.30, 0.42),
        "glass_pane_rl": box(-0.82, 0.98, -0.55, 0.05, 0.30, 0.42),
        "glass_pane_rr": box(0.82, 0.98, -0.55, 0.05, 0.30, 0.42),
        "window_mullion_a": box(-0.82, 0.98, 0.05, 0.06, 0.32, 0.05),
        "window_mullion_b": box(0.82, 0.98, 0.05, 0.06, 0.32, 0.05),
        "window_sill_l": box(-0.82, 0.82, -0.1, 0.06, 0.05, 1.2),
        "window_sill_r": box(0.82, 0.82, -0.1, 0.06, 0.05, 1.2),
        # Doors / bumpers / trim.
        "car_door_l": box(-0.90, 0.58, 0.05, 0.08, 0.55, 1.05),
        "car_door_r": box(0.90, 0.58, 0.05, 0.08, 0.55, 1.05),
        "door_handle_l": box(-0.94, 0.62, 0.15, 0.05, 0.08, 0.12),
        "door_handle_r": box(0.94, 0.62, 0.15, 0.05, 0.08, 0.12),
        "car_bumper_front": soft_box(0.0, 0.32, 1.92, 1.72, 0.28, 0.22),
        "car_bumper_rear": soft_box(0.0, 0.32, -1.92, 1.72, 0.28, 0.22),
        "car_grille": soft_box(0.0, 0.48, 1.98, 0.95, 0.20, 0.06),
        "car_grille_bar_1": soft_box(0.0, 0.52, 2.00, 0.85, 0.03, 0.04),
        "car_grille_bar_2": soft_box(0.0, 0.44, 2.00, 0.85, 0.03, 0.04),
        "car_headlight_l": soft_box(-0.55, 0.48, 1.98, 0.28, 0.16, 0.08),
        "car_headlight_r": soft_box(0.55, 0.48, 1.98, 0.28, 0.16, 0.08),
        "car_taillight_l": soft_box(-0.55, 0.50, -1.98, 0.30, 0.14, 0.06),
        "car_taillight_r": soft_box(0.55, 0.50, -1.98, 0.30, 0.14, 0.06),
        "car_mirror_l": soft_box(-0.98, 0.88, 0.48, 0.16, 0.10, 0.18),
        "car_mirror_r": soft_box(0.98, 0.88, 0.48, 0.16, 0.10, 0.18),
        "car_number_plate": soft_box(0.0, 0.34, 2.02, 0.38, 0.10, 0.03),
        "car_stripe": soft_box(0.0, 0.55, 0.0, 1.78, 0.06, 2.6),
        "car_skirt_l": soft_box(-0.88, 0.28, 0.0, 0.06, 0.12, 2.8),
        "car_skirt_r": soft_box(0.88, 0.28, 0.0, 0.06, 0.12, 2.8),
        "car_wheel_arch_fl": cylinder(-0.78, 0.38, 1.05, 0.32, 0.22, axis="z", segments=14),
        "car_wheel_arch_fr": cylinder(0.78, 0.38, 1.05, 0.32, 0.22, axis="z", segments=14),
        "car_wheel_arch_rl": cylinder(-0.78, 0.38, -1.05, 0.32, 0.22, axis="z", segments=14),
        "car_wheel_arch_rr": cylinder(0.78, 0.38, -1.05, 0.32, 0.22, axis="z", segments=14),
        # wheel_* → Rubber via InferFromMeshName / binder fix; hubs stay metal.
        "wheel_fl": cylinder(-0.78, 0.17, 1.05, 0.28, 0.20, axis="x", segments=16),
        "wheel_fr": cylinder(0.78, 0.17, 1.05, 0.28, 0.20, axis="x", segments=16),
        "wheel_rl": cylinder(-0.78, 0.17, -1.05, 0.28, 0.20, axis="x", segments=16),
        "wheel_rr": cylinder(0.78, 0.17, -1.05, 0.28, 0.20, axis="x", segments=16),
        "hub_fl": cylinder(-0.78, 0.17, 1.05, 0.12, 0.10, axis="x", segments=10),
        "hub_fr": cylinder(0.78, 0.17, 1.05, 0.12, 0.10, axis="x", segments=10),
        "hub_rl": cylinder(-0.78, 0.17, -1.05, 0.12, 0.10, axis="x", segments=10),
        "hub_rr": cylinder(0.78, 0.17, -1.05, 0.12, 0.10, axis="x", segments=10),
        "wiper": soft_box(0.0, 1.12, 0.55, 0.85, 0.03, 0.04),
        "antenna": cylinder(-0.35, 1.25, -0.85, 0.015, 0.35, axis="y", segments=6),
    }
    return meshes

# ---------------------------------------------------------------------------
# VEH-001 fuel truck v06 — rounded cab, denser oval tank, dual-axle read
# ---------------------------------------------------------------------------


def fuel_truck_v06() -> dict[str, tuple[np.ndarray, np.ndarray]]:
    # Start from v05 then replace key silhouette parts with softer volumes.
    meshes = _f2.fuel_truck_v05()
    meshes["cab"] = soft_box(1.05, 0.95, 0, 1.55, 1.55, 1.6)
    meshes["cab_roof"] = soft_box(1.05, 1.82, 0, 1.42, 0.14, 1.42)
    meshes["cab_fairing"] = cylinder(1.72, 1.35, 0, 0.55, 1.35, axis="x", segments=16)
    meshes["tank"] = oval_tank(-0.5, 0.98, 0, 2.75, 0.72, 0.68, stations=16, segments=24)
    meshes["tank_end_f"] = cylinder(0.88, 0.98, 0, 0.64, 0.22, axis="x", segments=20)
    meshes["tank_end_r"] = cylinder(-1.85, 0.98, 0, 0.64, 0.22, axis="x", segments=20)
    # Mid axle for regional tanker silhouette vs turboprop.
    meshes["wheel_ml"] = cylinder(-0.35, 0.28, 0.6, 0.3, 0.24, axis="z", segments=16)
    meshes["wheel_mr"] = cylinder(-0.35, 0.28, -0.6, 0.3, 0.24, axis="z", segments=16)
    meshes["hub_ml"] = cylinder(-0.35, 0.28, 0.6, 0.12, 0.12, axis="z", segments=10)
    meshes["hub_mr"] = cylinder(-0.35, 0.28, -0.6, 0.12, 0.12, axis="z", segments=10)
    meshes["fender_ml"] = soft_box(-0.35, 0.48, 0.72, 0.75, 0.22, 0.22)
    meshes["fender_mr"] = soft_box(-0.35, 0.48, -0.72, 0.75, 0.22, 0.22)
    meshes["hose"] = cylinder(-1.85, 0.55, 0.62, 0.07, 0.95, axis="z", segments=12)
    meshes["hose_pivot"] = cylinder(-1.62, 0.72, 0.78, 0.08, 0.14, axis="y", segments=10)
    meshes["hose_nozzle"] = soft_box(-2.05, 0.52, 0.88, 0.32, 0.18, 0.18)
    meshes["bumper"] = soft_box(1.95, 0.4, 0, 0.26, 0.38, 1.48)
    meshes["bumper_rear"] = soft_box(-2.0, 0.4, 0, 0.26, 0.38, 1.38)
    for i, z in enumerate([-0.55, -0.22, 0.22, 0.55], start=5):
        meshes[f"glass_pane_{i}"] = soft_box(1.66, 1.35, z, 0.04, 0.42, 0.28)
    meshes["cab_window"] = soft_box(1.66, 1.28, 0, 0.04, 0.78, 1.28)
    return meshes

# ---------------------------------------------------------------------------
# VEH-002 baggage tug train v06 — open ROPS cab, blue cargo crates
# ---------------------------------------------------------------------------


def baggage_tug_v06() -> dict[str, tuple[np.ndarray, np.ndarray]]:
    meshes = _f2.baggage_tug_v05()
    meshes["tug"] = soft_box(3.55, 0.48, 0, 1.75, 0.78, 1.15)
    meshes["tug_cab"] = soft_box(4.18, 0.92, 0, 0.88, 0.72, 1.05)
    # Open-cab ROPS — REF-003 orange tug with visible seat / steering.
    meshes["tug_rollbar"] = cylinder(3.72, 1.35, 0, 0.04, 0.95, axis="z", segments=10)
    meshes["tug_rollbar_l"] = cylinder(3.72, 1.15, 0.42, 0.035, 0.85, axis="y", segments=8)
    meshes["tug_rollbar_r"] = cylinder(3.72, 1.15, -0.42, 0.035, 0.85, axis="y", segments=8)
    meshes["tug_rollbar_top"] = soft_box(3.95, 1.72, 0, 0.58, 0.06, 0.95)
    meshes["tug_seat"] = soft_box(3.92, 0.82, 0, 0.42, 0.32, 0.65)
    meshes["tug_seat_back"] = soft_box(3.72, 1.05, 0, 0.08, 0.42, 0.62)
    meshes["tug_steering_wheel"] = cylinder(4.38, 1.12, 0.12, 0.15, 0.04, axis="x", segments=14)
    meshes["counterweight"] = soft_box(3.05, 0.52, 0, 0.55, 0.55, 0.95)
    for idx, cx in enumerate((1.8, 0.15, -1.5), start=1):
        meshes[f"cart_{idx}"] = soft_box(cx, 0.48, 0, 1.52, 0.72, 1.05)
        # Coastal Blue baggage crates — REF-003/005 (colour applied at load).
        meshes[f"cargo_{idx}"] = soft_box(cx, 0.95, 0, 1.18, 0.42, 0.82)
        meshes[f"cargo_lid_{idx}"] = soft_box(cx, 1.18, 0, 1.12, 0.06, 0.78)
        meshes[f"cargo_latch_{idx}"] = soft_box(cx + 0.45, 1.05, 0.42, 0.12, 0.08, 0.05)
    meshes["hitch_1"] = soft_box(2.7, 0.35, 0, 0.48, 0.18, 0.2)
    meshes["tow_pivot_1"] = cylinder(2.7, 0.35, 0, 0.09, 0.14, axis="y", segments=12)
    return meshes

# ---------------------------------------------------------------------------
# VEH-003 apron bus v06 — two-tone Coastal Blue / white, ribbon glass
# ---------------------------------------------------------------------------


def passenger_bus_v06() -> dict[str, tuple[np.ndarray, np.ndarray]]:
    meshes = _f2.passenger_bus_v05()
    # Two-tone body split — lower Coastal Blue / upper white at load time.
    meshes["bus_body"] = soft_box(0, 0.72, 0, 4.5, 1.15, 1.72)
    meshes["bus_body_upper"] = soft_box(0, 1.55, 0, 4.35, 0.85, 1.62)
    meshes["cabin_roof"] = soft_box(0, 2.02, 0, 4.25, 0.22, 1.52)
    meshes["nose_round"] = cylinder(2.18, 0.95, 0, 0.88, 1.55, axis="x", segments=20)
    meshes["tail_round"] = cylinder(-2.18, 0.95, 0, 0.84, 1.5, axis="x", segments=20)
    meshes["windshield"] = soft_box(2.32, 1.42, 0, 0.05, 0.82, 1.42)
    for i, x in enumerate([-1.9, -1.35, -0.8, -0.25, 0.85, 1.4, 1.9], start=1):
        meshes[f"glass_pane_{i}"] = soft_box(x, 1.42, 0.9, 0.5, 0.55, 0.04)
        meshes[f"glass_pane_lo_{i}"] = soft_box(x, 1.42, -0.9, 0.5, 0.55, 0.04)
    meshes["door"] = soft_box(0.35, 0.98, 0.9, 1.05, 1.45, 0.1)
    meshes["door_glass"] = soft_box(0.35, 1.28, 0.95, 0.7, 0.62, 0.04)
    meshes["bumper_front"] = soft_box(2.42, 0.38, 0, 0.32, 0.4, 1.58)
    meshes["bumper_rear"] = soft_box(-2.42, 0.38, 0, 0.32, 0.4, 1.58)
    meshes["stripe"] = soft_box(0, 0.95, 0.88, 4.2, 0.16, 0.05)
    meshes["stripe_b"] = soft_box(0, 0.95, -0.88, 4.2, 0.16, 0.05)
    meshes["stripe_upper"] = soft_box(0, 1.78, 0.88, 4.15, 0.06, 0.04)
    return meshes

# ---------------------------------------------------------------------------
# VEH-004 pushback tug v03 — heavier cab, clearer glass, towbar pivots
# ---------------------------------------------------------------------------


def pushback_tug_v03() -> dict[str, tuple[np.ndarray, np.ndarray]]:
    meshes = _f2.pushback_tug_v02()
    meshes["tug_body"] = soft_box(0.15, 0.48, 0, 2.4, 0.88, 1.28)
    meshes["tug_cab"] = soft_box(0.72, 0.98, 0, 0.98, 0.82, 1.12)
    meshes["tug_seat"] = soft_box(0.55, 0.88, 0, 0.42, 0.32, 0.68)
    meshes["tug_rollbar"] = cylinder(0.32, 1.32, 0, 0.04, 0.95, axis="z", segments=10)
    meshes["tug_rollbar_top"] = soft_box(0.55, 1.68, 0, 0.58, 0.07, 0.95)
    meshes["counterweight"] = soft_box(-0.78, 0.55, 0, 0.78, 0.72, 1.12)
    meshes["bumper_front"] = soft_box(1.28, 0.38, 0, 0.24, 0.34, 1.18)
    meshes["glass_pane_1"] = soft_box(1.12, 1.18, 0, 0.04, 0.42, 0.85)
    meshes["glass_pane_2"] = soft_box(0.72, 1.18, 0.58, 0.55, 0.38, 0.04)
    meshes["glass_pane_3"] = soft_box(0.72, 1.18, -0.58, 0.55, 0.38, 0.04)
    meshes["towbar"] = soft_box(-1.9, 0.28, 0, 1.45, 0.12, 0.12)
    meshes["towbar_head"] = soft_box(-2.62, 0.34, 0, 0.38, 0.3, 0.38)
    meshes["tow_pivot"] = cylinder(-1.18, 0.28, 0, 0.11, 0.18, axis="y", segments=12)
    meshes["towbar_pivot_mid"] = cylinder(-1.95, 0.28, 0, 0.08, 0.14, axis="y", segments=10)
    meshes["towbar_eye"] = cylinder(-2.82, 0.34, 0, 0.11, 0.08, axis="z", segments=12)
    meshes["towbar_wheel"] = cylinder(-2.2, 0.15, 0, 0.15, 0.12, axis="z", segments=14)
    meshes["stripe"] = soft_box(0.15, 0.58, 0.66, 2.15, 0.14, 0.04)
    meshes["beacon"] = cylinder(0.72, 1.52, 0, 0.12, 0.18, axis="y", segments=10)
    return meshes

def main() -> None:
    VEHICLES.mkdir(parents=True, exist_ok=True)
    write_kit(VEHICLES, "mdl_parked_car_v02", parked_car_v02())
    write_kit(VEHICLES, "mdl_fuel_truck_small_v06", fuel_truck_v06())
    write_kit(VEHICLES, "mdl_baggage_tug_train_v06", baggage_tug_v06())
    write_kit(VEHICLES, "mdl_passenger_bus_apron_v06", passenger_bus_v06())
    write_kit(VEHICLES, "mdl_pushback_tug_v03", pushback_tug_v03())


if __name__ == "__main__":
    main()
