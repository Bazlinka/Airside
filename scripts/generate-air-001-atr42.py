#!/usr/bin/env python3
"""Generate the production AIR-001 ATR 42-class starter aircraft.

The shipping model is intentionally fictional and unbranded, but follows the
ATR 42-600 three-view dimensions: 24.57 m span, 22.67 m length, 7.59 m height
and 3.93 m six-blade propellers. Every gameplay-relevant moving surface is a
separate, predictably named mesh for Unity pivot rebaking and animation.
"""

from __future__ import annotations

import importlib.util
from pathlib import Path

import numpy as np


SCRIPTS = Path(__file__).resolve().parent
REPO = SCRIPTS.parent
AIRCRAFT = REPO / "game" / "Airside" / "Assets" / "Airside" / "Art" / "Models" / "Aircraft"
BASENAME = "mdl_atr42_starter_v01"

_SPEC = importlib.util.spec_from_file_location("air_001_v06", SCRIPTS / "generate-air-001-v06.py")
_v06 = importlib.util.module_from_spec(_SPEC)
assert _SPEC.loader is not None
_SPEC.loader.exec_module(_v06)
_v05 = _v06._v05

TARGET_SPAN = 24.57
TARGET_LENGTH = 22.67
TARGET_HEIGHT = 7.59
PROP_DIAMETER = 3.93

X_SCALE = TARGET_SPAN / 15.09
Y_SCALE = PROP_DIAMETER / 2.60
Z_SCALE = TARGET_LENGTH / 10.70
GROUND_SHIFT = 0.014


def scale_mesh(mesh: tuple[np.ndarray, np.ndarray]):
    vertices, indices = mesh
    scaled = vertices.copy()
    scaled *= np.array([X_SCALE, Y_SCALE, Z_SCALE], np.float32)
    scaled[:, 1] += GROUND_SHIFT
    return scaled, indices.copy()


def move_y(mesh: tuple[np.ndarray, np.ndarray], offset: float):
    vertices, indices = mesh
    moved = vertices.copy()
    moved[:, 1] += offset
    return moved, indices.copy()


def stretch_y(mesh: tuple[np.ndarray, np.ndarray], base_y: float, factor: float):
    vertices, indices = mesh
    stretched = vertices.copy()
    stretched[:, 1] = base_y + (stretched[:, 1] - base_y) * factor
    return stretched, indices.copy()


def translated(mesh: tuple[np.ndarray, np.ndarray], x: float, y: float, z: float):
    vertices, indices = mesh
    moved = vertices.copy()
    moved += np.array([x, y, z], np.float32)
    return moved, indices.copy()


def panel(cx: float, cy: float, cz: float, sx: float, sy: float, sz: float):
    return _v05.box(cx, cy, cz, sx, sy, sz)


def wheel_parts(meshes: dict, prefix: str, x: float, y: float, z: float, radius: float, width: float):
    meshes[f"tire_{prefix}"] = _v05.cylinder(x, y, z, radius, width, axis="x", segments=28)
    meshes[f"wheel_{prefix}"] = _v05.cylinder(x, y, z, radius * 0.57, width * 1.04, axis="x", segments=24)
    meshes[f"rim_{prefix}"] = _v05.cylinder(x, y, z, radius * 0.29, width * 1.08, axis="x", segments=20)


def final_meshes():
    meshes = {name: scale_mesh(mesh) for name, mesh in _v06.v06_meshes().items()}

    # Raise the nacelles to meet the true high wing and provide realistic prop
    # clearance. The engine spacing remains about 8 m centre-to-centre.
    engine_parts = (
        "engine_", "pylon_", "nacelle_", "intake_", "exhaust_",
        "oil_cooler_", "cowl_flap_",
    )
    for name in list(meshes):
        if name.startswith(engine_parts):
            meshes[name] = move_y(meshes[name], 0.72)

    # The ATR's horizontal tail sits high on the fin. Stretch the fin to the
    # official height and place the tailplane/elevators at a legible T-tail-like
    # shoulder without creating ornamental moving parts.
    fin_factor = 1.477
    for name in ("tail_fin", "rudder"):
        meshes[name] = stretch_y(meshes[name], 2.36, fin_factor)
    for name in ("tailplane", "elevator_left", "elevator_right"):
        meshes[name] = move_y(meshes[name], 2.28)
    meshes["hf_antenna"] = move_y(meshes["hf_antenna"], 2.05)

    # The ATR 42 has clean tapered tips, not winglets or decorative fences.
    for name in (
        "winglet_left", "winglet_right", "wing_fence_left", "wing_fence_right",
        "wing_fence_mid_l", "wing_fence_mid_r",
    ):
        meshes.pop(name, None)

    # Replace the old seven-window shorthand with a full, evenly pitched cabin.
    for name in list(meshes):
        if name.startswith("cabin_window_") or name.startswith("cabin_window_frame_"):
            del meshes[name]
    window_z = (5.85, 4.90, 3.95, 3.00, 2.05, 1.10, 0.15, -0.80, -1.75, -2.70, -3.65, -4.60, -5.55)
    for index, z in enumerate(window_z, start=1):
        meshes[f"cabin_window_{index}"] = panel(-1.345, 2.47, z, 0.035, 0.43, 0.52)
        meshes[f"cabin_window_r{index}"] = panel(1.345, 2.47, z, 0.035, 0.43, 0.52)

    # Rebuild the complete six-blade propeller sets at 3.93 m diameter. Yellow
    # tip pieces are separate so the animation blur remains readable at speed.
    for name in list(meshes):
        if name.startswith(("propeller_", "spinner_", "prop_hub_", "hub_cap_")):
            del meshes[name]
    suffixes = ("", "_b", "_c", "_d", "_e", "_f")
    prop_y = 2.80
    prop_z = 5.62
    for side, side_name in ((-1.0, "left"), (1.0, "right")):
        x = side * 3.99
        for blade_index, suffix in enumerate(suffixes):
            angle = blade_index * 60.0
            meshes[f"propeller_{side_name}{suffix}"] = _v06.propeller_blade(
                x, prop_y, prop_z, angle, radial_start=0.22, radial_end=1.76
            )
            meshes[f"propeller_{side_name}_tip{suffix}"] = _v06.propeller_blade(
                x, prop_y, prop_z, angle, radial_start=1.72,
                radial_end=PROP_DIAMETER * 0.5, tip=True
            )
        spinner = _v05.oval_lathe_fuselage(
            [(5.42, 0.29, 0.29, prop_y), (5.74, 0.34, 0.34, prop_y), (6.10, 0.035, 0.035, prop_y)],
            segments=28,
        )
        meshes[f"spinner_{side_name}"] = translated(spinner, x, 0.0, 0.0)
        meshes[f"prop_hub_{side_name}"] = _v05.cylinder(x, prop_y, 5.56, 0.22, 0.25, axis="z", segments=24)
        meshes[f"hub_cap_{side_name}"] = _v05.cylinder(x, prop_y, 5.92, 0.11, 0.11, axis="z", segments=20)

    # ATR-pattern undercarriage: twin nose wheels and two tandem wheels per main
    # leg. The main legs emerge from compact fuselage-side sponsons rather than
    # the engine nacelles.
    gear_prefixes = (
        "gear_nose", "gear_left", "gear_right", "gear_oleo_", "gear_scissors_", "gear_door_",
        "tire_", "wheel_", "rim_",
    )
    for name in list(meshes):
        if name.startswith(gear_prefixes):
            del meshes[name]
    meshes["gear_fairing_left"] = _v05.oval_lathe_fuselage(
        [(0.35, 0.34, 0.42, 1.46), (-0.30, 0.42, 0.48, 1.48), (-1.05, 0.28, 0.34, 1.50)], segments=24
    )
    meshes["gear_fairing_left"] = translated(meshes["gear_fairing_left"], -1.20, 0.0, 0.0)
    meshes["gear_fairing_right"] = translated(
        _v05.oval_lathe_fuselage(
            [(0.35, 0.34, 0.42, 1.46), (-0.30, 0.42, 0.48, 1.48), (-1.05, 0.28, 0.34, 1.50)], segments=24
        ), 1.20, 0.0, 0.0
    )
    meshes["gear_left"] = _v06.rotated_box(-1.70, 1.02, -0.40, 0.16, 1.35, 0.24, z_degrees=-15.0)
    meshes["gear_right"] = _v06.rotated_box(1.70, 1.02, -0.40, 0.16, 1.35, 0.24, z_degrees=15.0)
    meshes["gear_oleo_left"] = _v05.cylinder(-2.05, 0.72, -0.40, 0.065, 0.70, axis="y", segments=18)
    meshes["gear_oleo_right"] = _v05.cylinder(2.05, 0.72, -0.40, 0.065, 0.70, axis="y", segments=18)
    meshes["gear_scissors_left"] = _v06.rotated_box(-2.02, 0.66, -0.64, 0.07, 0.34, 0.16, z_degrees=-12.0)
    meshes["gear_scissors_right"] = _v06.rotated_box(2.02, 0.66, -0.64, 0.07, 0.34, 0.16, z_degrees=12.0)
    meshes["gear_door_left"] = panel(-1.37, 1.37, -0.42, 0.06, 0.62, 1.38)
    meshes["gear_door_right"] = panel(1.37, 1.37, -0.42, 0.06, 0.62, 1.38)
    wheel_parts(meshes, "left_forward", -2.05, 0.37, -0.05, 0.37, 0.22)
    wheel_parts(meshes, "left_aft", -2.05, 0.37, -0.75, 0.37, 0.22)
    wheel_parts(meshes, "right_forward", 2.05, 0.37, -0.05, 0.37, 0.22)
    wheel_parts(meshes, "right_aft", 2.05, 0.37, -0.75, 0.37, 0.22)

    meshes["gear_nose"] = panel(0.0, 1.03, 7.30, 0.15, 1.36, 0.22)
    meshes["gear_oleo_nose"] = _v05.cylinder(0.0, 0.77, 7.30, 0.052, 0.92, axis="y", segments=18)
    meshes["gear_scissors_nose"] = _v06.rotated_box(0.0, 0.65, 7.14, 0.07, 0.35, 0.15)
    meshes["gear_door_nose"] = panel(0.0, 1.37, 7.10, 0.54, 0.06, 1.24)
    wheel_parts(meshes, "nose_left", -0.18, 0.31, 7.34, 0.31, 0.16)
    wheel_parts(meshes, "nose_right", 0.18, 0.31, 7.34, 0.31, 0.16)

    # Keep overall height exact after procedural surface changes.
    top = meshes["tail_fin"][0][:, 1].max()
    meshes["tail_fin"] = stretch_y(meshes["tail_fin"], 2.36, (TARGET_HEIGHT - 2.36) / (top - 2.36))

    return {name: _v06.outward_winding(mesh) for name, mesh in meshes.items()}


def main() -> None:
    AIRCRAFT.mkdir(parents=True, exist_ok=True)
    meshes = final_meshes()
    _v05.write_kit(AIRCRAFT, BASENAME, meshes)
    vertex_count = sum(len(vertices) for vertices, _ in meshes.values())
    triangles = sum(len(indices) // 3 for _, indices in meshes.values())
    all_vertices = np.concatenate([part_vertices for part_vertices, _ in meshes.values()])
    bounds = np.ptp(all_vertices, axis=0)
    print(
        f"AIR-001 ATR 42 starter ready: {BASENAME} "
        f"({len(meshes)} parts, {vertex_count} vertices, {triangles} triangles; "
        f"{bounds[0]:.2f} x {bounds[1]:.2f} x {bounds[2]:.2f} m)"
    )


if __name__ == "__main__":
    main()
