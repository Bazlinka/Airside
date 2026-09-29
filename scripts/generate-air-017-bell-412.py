#!/usr/bin/env python3
"""Generate AIR-017, an original Bell 412EP-class Adelaide rescue helicopter.

The model follows the public Bell 412 dimensional envelope and the four-blade,
twin-engine, skid-gear silhouette. It contains no copied geometry, logos,
registration or service marks. See ADR 0186 and the asset register.
"""

from __future__ import annotations

import argparse
import importlib.util
from pathlib import Path

import numpy as np


SCRIPTS = Path(__file__).resolve().parent
REPO = SCRIPTS.parent
AIRCRAFT = REPO / "game" / "Airside" / "Assets" / "Airside" / "Art" / "Models" / "Aircraft"
BASENAME = "mdl_bell_412_rescue_v01"
EXPECTED_PARTS = 43

_SPEC = importlib.util.spec_from_file_location("air_001_v05", SCRIPTS / "generate-air-001-v05.py")
_base = importlib.util.module_from_spec(_SPEC)
assert _SPEC.loader is not None
_SPEC.loader.exec_module(_base)


def translated(mesh, x: float, y: float, z: float):
    vertices, indices = mesh
    moved = vertices.copy()
    moved += np.array([x, y, z], dtype=np.float32)
    return moved, indices.copy()


def rotated(mesh, *, x_degrees: float = 0.0, y_degrees: float = 0.0, z_degrees: float = 0.0,
            pivot=(0.0, 0.0, 0.0)):
    vertices, indices = mesh
    xyz = vertices.astype(np.float64) - np.asarray(pivot, dtype=np.float64)
    for axis, degrees in ((0, x_degrees), (1, y_degrees), (2, z_degrees)):
        if not degrees:
            continue
        angle = np.deg2rad(degrees)
        c, s = float(np.cos(angle)), float(np.sin(angle))
        if axis == 0:
            y, z = xyz[:, 1].copy(), xyz[:, 2].copy()
            xyz[:, 1], xyz[:, 2] = y * c - z * s, y * s + z * c
        elif axis == 1:
            x, z = xyz[:, 0].copy(), xyz[:, 2].copy()
            xyz[:, 0], xyz[:, 2] = x * c + z * s, -x * s + z * c
        else:
            x, y = xyz[:, 0].copy(), xyz[:, 1].copy()
            xyz[:, 0], xyz[:, 1] = x * c - y * s, x * s + y * c
    xyz += np.asarray(pivot, dtype=np.float64)
    return xyz.astype(np.float32), indices.copy()


def tapered_blade(cx: float, cy: float, cz: float, length: float, chord: float, angle: float):
    """Thin main-rotor blade in the horizontal XZ plane."""
    mesh = _base.box(0.0, cy, cz + length * 0.5, chord, 0.055, length)
    mesh = rotated(mesh, y_degrees=angle, pivot=(0.0, cy, cz))
    return translated(mesh, cx, 0.0, 0.0)


def bell_412_meshes():
    meshes = {}

    # Rounded cabin and nose: broad utility cabin, raised twin-engine doghouse.
    meshes["fuselage"] = _base.oval_lathe_fuselage(
        [
            (-2.55, 0.55, 0.62, 1.95),
            (-2.10, 1.16, 1.28, 1.92),
            (0.75, 1.35, 1.50, 1.88),
            (2.25, 1.24, 1.30, 1.82),
            (3.20, 0.72, 0.82, 1.78),
            (3.62, 0.18, 0.22, 1.74),
        ],
        segments=28,
    )
    meshes["belly"] = _base.oval_lathe_fuselage(
        [(-2.0, 0.88, 0.35, 0.82), (1.9, 0.95, 0.42, 0.80)], segments=20
    )
    meshes["engine_left"] = translated(
        _base.oval_lathe_fuselage([(-1.55, 0.42, 0.42, 3.38), (0.35, 0.48, 0.50, 3.42)], segments=20),
        -0.58, 0.0, 0.0,
    )
    meshes["engine_right"] = translated(
        _base.oval_lathe_fuselage([(-1.55, 0.42, 0.42, 3.38), (0.35, 0.48, 0.50, 3.42)], segments=20),
        0.58, 0.0, 0.0,
    )
    meshes["engine_exhaust_left"] = translated(
        _base.oval_lathe_fuselage([(-1.86, 0.23, 0.23, 3.42), (-1.45, 0.30, 0.30, 3.42)], segments=18),
        -0.58, 0.0, 0.0,
    )
    meshes["engine_exhaust_right"] = translated(
        _base.oval_lathe_fuselage([(-1.86, 0.23, 0.23, 3.42), (-1.45, 0.30, 0.30, 3.42)], segments=18),
        0.58, 0.0, 0.0,
    )

    # Tapering tail boom reaches the documented overall envelope without becoming a box.
    meshes["tail_boom"] = _base.oval_lathe_fuselage(
        [(-9.75, 0.20, 0.24, 2.52), (-7.7, 0.34, 0.38, 2.43), (-2.0, 0.72, 0.70, 2.05)],
        segments=20,
    )
    meshes["tail_fin"] = rotated(_base.box(0.0, 3.35, -9.32, 0.16, 2.15, 1.55), x_degrees=-12.0,
                                  pivot=(0.0, 3.35, -9.32))
    meshes["tailplane_left"] = rotated(_base.box(-1.05, 2.62, -8.15, 2.0, 0.12, 0.72), y_degrees=4.0,
                                        pivot=(0.0, 2.62, -8.15))
    meshes["tailplane_right"] = rotated(_base.box(1.05, 2.62, -8.15, 2.0, 0.12, 0.72), y_degrees=-4.0,
                                         pivot=(0.0, 2.62, -8.15))

    # Bell 412 identity: four-blade main rotor and a two-blade tail rotor.
    meshes["rotor_mast"] = _base.box(0.0, 4.02, -0.28, 0.16, 1.05, 0.16)
    meshes["rotor_hub"] = _base.box(0.0, 4.52, -0.28, 0.62, 0.18, 0.62)
    for index, angle in enumerate((0.0, 90.0, 180.0, 270.0), start=1):
        meshes[f"main_rotor_blade_{index}"] = tapered_blade(0.0, 4.60, -0.28, 6.95, 0.34, angle)
    meshes["tail_rotor_hub"] = _base.box(0.18, 3.05, -9.66, 0.28, 0.22, 0.22)
    meshes["tail_rotor_blade_1"] = rotated(_base.box(0.28, 3.05, -9.66, 0.08, 2.65, 0.22),
                                             z_degrees=28.0, pivot=(0.28, 3.05, -9.66))
    meshes["tail_rotor_blade_2"] = rotated(_base.box(0.30, 3.05, -9.66, 0.08, 2.65, 0.22),
                                             z_degrees=118.0, pivot=(0.30, 3.05, -9.66))

    # High skid gear, cross tubes and visible boarding steps.
    for side, x in (("left", -1.22), ("right", 1.22)):
        meshes[f"landing_skid_{side}"] = _base.box(x, 0.27, 0.05, 0.13, 0.13, 5.45)
        meshes[f"skid_strut_{side}_front"] = rotated(_base.box(x * 0.72, 0.63, 1.48, 0.10, 0.95, 0.10),
                                                       z_degrees=side == "left" and -24.0 or 24.0,
                                                       pivot=(x * 0.72, 0.63, 1.48))
        meshes[f"skid_strut_{side}_rear"] = rotated(_base.box(x * 0.72, 0.63, -1.32, 0.10, 0.95, 0.10),
                                                      z_degrees=side == "left" and -24.0 or 24.0,
                                                      pivot=(x * 0.72, 0.63, -1.32))
    meshes["step_left"] = _base.box(-1.36, 0.62, 0.05, 0.28, 0.10, 1.85)
    meshes["step_right"] = _base.box(1.36, 0.62, 0.05, 0.28, 0.10, 1.85)

    # Glazing and sliding-door read; shallow panels sit proud of the curved shell.
    meshes["cockpit_glass_left"] = rotated(_base.box(-0.67, 2.42, 2.83, 1.04, 0.82, 0.07),
                                            y_degrees=-14.0, x_degrees=-20.0,
                                            pivot=(-0.67, 2.42, 2.83))
    meshes["cockpit_glass_right"] = rotated(_base.box(0.67, 2.42, 2.83, 1.04, 0.82, 0.07),
                                             y_degrees=14.0, x_degrees=-20.0,
                                             pivot=(0.67, 2.42, 2.83))
    meshes["cabin_window_left"] = _base.box(-1.36, 2.23, 0.20, 0.05, 0.88, 2.45)
    meshes["cabin_window_right"] = _base.box(1.36, 2.23, 0.20, 0.05, 0.88, 2.45)
    meshes["sliding_door_left"] = _base.box(-1.39, 1.57, -0.10, 0.045, 1.72, 1.72)
    meshes["sliding_door_right"] = _base.box(1.39, 1.57, -0.10, 0.045, 1.72, 1.72)

    # Original broad rescue blocking and operational details; deliberately no marks or text.
    meshes["rescue_red_belly"] = _base.box(0.0, 1.02, 0.15, 2.55, 0.30, 4.30)
    meshes["rescue_red_tail"] = _base.box(0.0, 2.25, -5.65, 0.62, 0.24, 5.75)
    meshes["nose_searchlight"] = _base.box(-0.55, 1.12, 3.18, 0.34, 0.34, 0.25)
    meshes["camera_pod"] = _base.oval_lathe_fuselage([(2.75, 0.24, 0.24, 0.98), (3.20, 0.12, 0.12, 0.98)], segments=16)
    meshes["wire_strike_upper"] = rotated(_base.box(0.0, 3.52, 2.03, 0.06, 0.78, 0.06), x_degrees=-22.0,
                                           pivot=(0.0, 3.52, 2.03))
    meshes["wire_strike_lower"] = rotated(_base.box(0.0, 0.88, 2.83, 0.06, 0.68, 0.06), x_degrees=24.0,
                                           pivot=(0.0, 0.88, 2.83))
    meshes["beacon_top"] = _base.box(0.0, 3.86, -1.25, 0.20, 0.16, 0.20)
    meshes["beacon_belly"] = _base.box(0.0, 0.60, -0.60, 0.18, 0.14, 0.18)
    meshes["nav_light_left"] = _base.box(-1.24, 2.58, -8.15, 0.16, 0.13, 0.18)
    meshes["nav_light_right"] = _base.box(1.24, 2.58, -8.15, 0.16, 0.13, 0.18)
    return meshes


def validate(meshes):
    if len(meshes) != EXPECTED_PARTS:
        raise ValueError(f"expected {EXPECTED_PARTS} parts, got {len(meshes)}")
    required = {
        "fuselage", "tail_boom", "engine_left", "engine_right", "landing_skid_left",
        "landing_skid_right", "cockpit_glass_left", "cockpit_glass_right", "tail_rotor_blade_1",
        "tail_rotor_blade_2", "main_rotor_blade_1", "main_rotor_blade_2", "main_rotor_blade_3",
        "main_rotor_blade_4",
    }
    if not required <= set(meshes):
        raise ValueError(f"missing required parts: {sorted(required - set(meshes))}")
    all_vertices = np.concatenate([vertices for vertices, _ in meshes.values()])
    minimum, maximum = all_vertices.min(axis=0), all_vertices.max(axis=0)
    size = maximum - minimum
    if not 13.8 <= size[0] <= 14.4:
        raise ValueError(f"main rotor span outside Bell 412 class: {size[0]:.2f} m")
    if not 16.5 <= size[2] <= 17.3:
        raise ValueError(f"overall length outside Bell 412 class: {size[2]:.2f} m")
    if not 4.4 <= size[1] <= 4.9:
        raise ValueError(f"overall height outside Bell 412 class: {size[1]:.2f} m")
    return minimum, maximum


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true", help="validate geometry and committed kit presence without rewriting")
    args = parser.parse_args()
    meshes = bell_412_meshes()
    minimum, maximum = validate(meshes)
    gltf = AIRCRAFT / f"{BASENAME}.gltf"
    binary = AIRCRAFT / f"{BASENAME}.bin"
    fbx = AIRCRAFT / f"{BASENAME}.fbx"
    if args.check:
        if (not gltf.exists() or not binary.exists() or not fbx.exists()
                or not Path(str(gltf) + ".meta").exists()
                or not Path(str(binary) + ".meta").exists()
                or not Path(str(fbx) + ".meta").exists()):
            raise SystemExit("AIR-017 committed kit or Unity metadata is missing")
    else:
        _base.write_kit(AIRCRAFT, BASENAME, meshes)
    triangles = sum(len(indices) // 3 for _, indices in meshes.values())
    size = maximum - minimum
    print(f"AIR-017 ready: {BASENAME} ({len(meshes)} parts, {triangles} triangles; "
          f"{size[0]:.2f} x {size[2]:.2f} x {size[1]:.2f} m W/L/H)")


if __name__ == "__main__":
    main()
