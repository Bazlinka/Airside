#!/usr/bin/env python3
"""Generate AIR-001 v06 as a cleaner production candidate (FBX + glTF).

v06 keeps the stable AIR-001 runtime part names and animation contract while
rebuilding the large forms that made v05 read as assembled blocks: one continuous
fuselage, a true high wing, lofted nacelles, swept/tapered tail surfaces, twisted
propeller blades, attached landing gear and restrained surface details.

The model remains project-owned procedural geometry. It does not overwrite v05.
"""

from __future__ import annotations

import importlib.util
from pathlib import Path

import numpy as np


SCRIPTS = Path(__file__).resolve().parent
REPO = SCRIPTS.parent
AIRCRAFT = REPO / "game" / "Airside" / "Assets" / "Airside" / "Art" / "Models" / "Aircraft"
BASENAME = "mdl_regional_turboprop_01_v06"

_SPEC = importlib.util.spec_from_file_location("air_001_v05", SCRIPTS / "generate-air-001-v05.py")
_v05 = importlib.util.module_from_spec(_SPEC)
assert _SPEC.loader is not None
_SPEC.loader.exec_module(_v05)


def translated(mesh: tuple[np.ndarray, np.ndarray], x: float, y: float, z: float):
    verts, indices = mesh
    moved = verts.copy()
    moved += np.array([x, y, z], np.float32)
    return moved, indices.copy()


def rotated_box(
    cx: float,
    cy: float,
    cz: float,
    sx: float,
    sy: float,
    sz: float,
    *,
    z_degrees: float = 0.0,
):
    verts, indices = _v05.box(cx, cy, cz, sx, sy, sz)
    angle = np.deg2rad(z_degrees)
    cosine, sine = float(np.cos(angle)), float(np.sin(angle))
    centred = verts - np.array([cx, cy, cz], np.float32)
    x = centred[:, 0].copy()
    y = centred[:, 1].copy()
    centred[:, 0] = x * cosine - y * sine
    centred[:, 1] = x * sine + y * cosine
    return centred + np.array([cx, cy, cz], np.float32), indices


def outward_winding(mesh: tuple[np.ndarray, np.ndarray]):
    """Keep closed procedural parts front-facing under Unity's one-sided lighting."""
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


def nacelle(x: float):
    local = _v05.oval_lathe_fuselage(
        [
            (2.68, 0.10, 0.11, 1.33),
            (2.48, 0.25, 0.28, 1.34),
            (2.15, 0.40, 0.43, 1.34),
            (1.55, 0.48, 0.50, 1.32),
            (0.75, 0.50, 0.48, 1.28),
            (0.05, 0.40, 0.37, 1.23),
            (-0.42, 0.22, 0.20, 1.19),
        ],
        segments=28,
    )
    return translated(local, x, 0.0, 0.0)


def propeller_blade(
    cx: float,
    cy: float,
    cz: float,
    angle_degrees: float,
    *,
    radial_start: float = 0.18,
    radial_end: float = 1.28,
    tip: bool = False,
):
    """Loft a tapered blade with visible pitch instead of a flat rectangular fan."""
    angle = np.deg2rad(angle_degrees)
    radial = np.array([-np.sin(angle), np.cos(angle), 0.0])
    tangent = np.array([np.cos(angle), np.sin(angle), 0.0])
    axis = np.array([0.0, 0.0, 1.0])
    if tip:
        stations = [(radial_start, 0.080, 10.0, 0.022), (radial_end, 0.040, 7.0, 0.016)]
    else:
        stations = [
            (radial_start, 0.105, 34.0, 0.036),
            (0.52, 0.190, 25.0, 0.032),
            (0.92, 0.135, 16.0, 0.024),
            (radial_end, 0.055, 8.0, 0.016),
        ]

    loops = []
    centre = np.array([cx, cy, cz], np.float64)
    for radius, half_chord, twist_degrees, half_thickness in stations:
        twist = np.deg2rad(twist_degrees)
        chord_axis = tangent * np.cos(twist) + axis * np.sin(twist)
        thickness_axis = -tangent * np.sin(twist) + axis * np.cos(twist)
        station_centre = centre + radial * radius
        loops.append(
            np.asarray(
                [
                    station_centre - chord_axis * half_chord - thickness_axis * half_thickness,
                    station_centre + chord_axis * half_chord - thickness_axis * half_thickness,
                    station_centre + chord_axis * half_chord + thickness_axis * half_thickness,
                    station_centre - chord_axis * half_chord + thickness_axis * half_thickness,
                ],
                np.float32,
            )
        )

    vertices: list = []
    indices: list = []
    for index in range(len(loops) - 1):
        inner, outer = loops[index], loops[index + 1]
        for corner in range(4):
            following = (corner + 1) % 4
            base = len(vertices)
            vertices.extend([inner[corner], inner[following], outer[following], outer[corner]])
            indices.extend([base, base + 1, base + 2, base, base + 2, base + 3])
    for loop, reverse in ((loops[0], True), (loops[-1], False)):
        centre_point = loop.mean(axis=0)
        for corner in range(4):
            following = (corner + 1) % 4
            base = len(vertices)
            vertices.extend([centre_point, loop[following] if reverse else loop[corner], loop[corner] if reverse else loop[following]])
            indices.extend([base, base + 1, base + 2])
    return _v05._orient_outward(np.asarray(vertices, np.float32), np.asarray(indices, np.uint16))


def v06_meshes():
    # The approved reference is a high-wing regional turboprop. Keep all attached
    # surfaces driven from one planform so dihedral and taper cannot drift apart.
    _v05.WING.update(
        {
            "root_x": 0.50,
            "tip_x": 7.45,
            "root_y": 1.78,
            "dihedral": 0.026,
            "cz": 0.35,
            "root_chord": 2.15,
            "tip_chord": 0.72,
            "root_thickness": 0.24,
            "tip_thickness": 0.065,
            "root_le_frac": 0.38,
            "tip_le_frac": 0.30,
        }
    )
    meshes = _v05.turboprop_v05_meshes()

    # One clean body replaces the overlapping cabin/cockpit/radome stack.
    meshes["fuselage"] = _v05.oval_lathe_fuselage(
        [
            (5.78, 0.04, 0.05, 1.12),
            (5.58, 0.18, 0.18, 1.14),
            (5.30, 0.32, 0.31, 1.18),
            (4.92, 0.47, 0.43, 1.24),
            (4.48, 0.59, 0.55, 1.31),
            (3.98, 0.68, 0.66, 1.33),
            (3.35, 0.75, 0.74, 1.31),
            (2.55, 0.79, 0.79, 1.26),
            (1.65, 0.81, 0.81, 1.23),
            (0.70, 0.82, 0.82, 1.21),
            (-0.25, 0.82, 0.81, 1.20),
            (-1.20, 0.80, 0.79, 1.19),
            (-2.05, 0.76, 0.75, 1.18),
            (-2.80, 0.68, 0.67, 1.17),
            (-3.42, 0.57, 0.56, 1.15),
            (-3.92, 0.45, 0.44, 1.12),
            (-4.33, 0.33, 0.32, 1.08),
            (-4.66, 0.22, 0.21, 1.04),
            (-4.92, 0.08, 0.08, 1.00),
        ],
        segments=40,
    )
    for obsolete in ("cockpit", "radome", "cabin_window_band"):
        meshes.pop(obsolete, None)
    meshes["belly_fairing"] = _v05.oval_lathe_fuselage(
        [(2.3, 0.42, 0.25, 0.64), (1.7, 0.55, 0.30, 0.61), (-1.4, 0.54, 0.29, 0.60), (-2.0, 0.35, 0.20, 0.64)],
        segments=28,
    )
    meshes["cockpit_frame"] = _v05.box(0.0, 1.79, 4.30, 1.08, 0.055, 0.62)
    meshes["cockpit_glare"] = _v05.box(0.0, 1.64, 4.62, 0.94, 0.12, 0.10)
    meshes["windscreen_c"] = _v05.windscreen_pane(0.0, 1.66, 4.58, 0.52, 0.34, 0.025, pitch_deg=-35)
    meshes["windscreen_l"] = _v05.windscreen_pane(-0.38, 1.62, 4.45, 0.31, 0.33, 0.025, pitch_deg=-29)
    meshes["windscreen_r"] = _v05.windscreen_pane(0.38, 1.62, 4.45, 0.31, 0.33, 0.025, pitch_deg=-29)
    meshes["windscreen_pillar_l"] = _v05.box(-0.51, 1.70, 4.40, 0.045, 0.39, 0.32)
    meshes["windscreen_pillar_r"] = _v05.box(0.51, 1.70, 4.40, 0.045, 0.39, 0.32)
    meshes["windscreen_pillar_c"] = _v05.box(0.0, 1.72, 4.57, 0.035, 0.38, 0.25)

    # Narrow individual panes sit just outside the body. Remove the oversized
    # alternating frames from v05, which read as dark blocks from follow camera.
    for key in list(meshes):
        if key.startswith("cabin_window_"):
            meshes.pop(key)
    for index, z in enumerate((2.42, 1.62, 0.82, 0.02, -0.78, -1.58, -2.34), start=1):
        width = 0.38 if index < 7 else 0.30
        meshes[f"cabin_window_{index}"] = _v05.box(-0.805, 1.43, z, 0.025, 0.27, width)
        meshes[f"cabin_window_r{index}"] = _v05.box(0.805, 1.43, z, 0.025, 0.27, width)

    # Three-station wing loft adds a softer root-to-tip transition. Control
    # surfaces from v05 already track this shared high-wing planform.
    root = _v05.wing_station(0.50)
    middle = _v05.wing_station(3.65)
    tip = _v05.wing_station(7.45)
    for side, key in ((-1.0, "wing_left"), (1.0, "wing_right")):
        meshes[key] = _v05.lofted_aerofoil(
            [
                (side * 0.50, root[0], root[1], root[2], root[3]),
                (side * 3.65, middle[0], middle[1] - 0.10, middle[2], middle[3]),
                (side * 7.45, tip[0], tip[1] - 0.18, tip[2], tip[3]),
            ],
            chord_points=22,
        )
    for blocky_root in ("wing_root_left", "wing_root_right", "wing_fairing_left", "wing_fairing_right"):
        meshes.pop(blocky_root, None)

    # Continuous nacelles replace stacked cylinders; the pylon is now a short,
    # attached transition from the high wing into each engine.
    for side, suffix in ((-1.0, "left"), (1.0, "right")):
        x = side * 2.45
        meshes[f"engine_{suffix}"] = nacelle(x)
        meshes[f"pylon_{suffix}"] = _v05.box(x, 1.63, 0.80, 0.42, 0.42, 1.40)
        meshes[f"nacelle_{suffix}"] = _v05.oval_lathe_fuselage(
            [(0.35, 0.30, 0.23, 1.12), (-0.25, 0.24, 0.19, 1.10), (-0.58, 0.10, 0.09, 1.08)],
            segments=24,
        )
        meshes[f"nacelle_{suffix}"] = translated(meshes[f"nacelle_{suffix}"], x, 0.0, 0.0)
        meshes[f"intake_{suffix}"] = translated(_v05.cylinder(0, 1.34, 2.20, 0.31, 0.08, axis="z", segments=28), x, 0, 0)

    # Six twisted blades and separate yellow tips preserve runtime propeller names.
    suffixes = ("", "_b", "_c", "_d", "_e", "_f")
    for side, prefix in ((-1.0, "left"), (1.0, "right")):
        x = side * 2.45
        for blade_index, suffix in enumerate(suffixes):
            angle = blade_index * 60.0
            meshes[f"propeller_{prefix}{suffix}"] = propeller_blade(x, 1.34, 2.64, angle, radial_end=1.27)
            meshes[f"propeller_{prefix}_tip{suffix}"] = propeller_blade(
                x, 1.34, 2.64, angle, radial_start=1.08, radial_end=1.30, tip=True
            )
        meshes[f"spinner_{prefix}"] = translated(_v05.oval_lathe_fuselage(
            [(2.54, 0.20, 0.20, 1.34), (2.72, 0.22, 0.22, 1.34), (2.92, 0.03, 0.03, 1.34)],
            segments=24,
        ), x, 0, 0)
        meshes[f"prop_hub_{prefix}"] = _v05.cylinder(x, 1.34, 2.61, 0.14, 0.18, axis="z", segments=20)
        meshes[f"hub_cap_{prefix}"] = _v05.cylinder(x, 1.34, 2.83, 0.08, 0.09, axis="z", segments=16)

    # Swept, tapered empennage replaces the rectangular v05 slab forms.
    meshes["tail_fin"] = _v05.lofted_aerofoil(
        [(1.55, 0.0, -2.72, 2.05, 0.18), (2.75, 0.0, -3.02, 1.30, 0.13), (3.88, 0.0, -3.35, 0.58, 0.07)],
        chord_points=18,
        vertical=True,
    )
    meshes["tailplane"] = _v05.lofted_aerofoil(
        [(-2.42, 2.05, -3.54, 0.62, 0.055), (0.0, 2.02, -3.12, 1.42, 0.13), (2.42, 2.05, -3.54, 0.62, 0.055)],
        chord_points=18,
    )
    meshes["dorsal_fin"] = _v05.lofted_aerofoil(
        [(1.42, 0.0, -2.20, 1.15, 0.10), (1.82, 0.0, -2.48, 0.72, 0.065)],
        chord_points=12,
        vertical=True,
    )
    for obsolete in ("tail_fin_tip", "tailplane_tip_l", "tailplane_tip_r"):
        meshes.pop(obsolete, None)
    meshes["elevator_left"] = _v05.box(-1.18, 2.01, -4.00, 1.92, 0.045, 0.36)
    meshes["elevator_right"] = _v05.box(1.18, 2.01, -4.00, 1.92, 0.045, 0.36)
    meshes["rudder"] = rotated_box(0.0, 2.75, -3.94, 0.055, 1.82, 0.38, z_degrees=0.0)

    # Gear now reaches its nacelle mounts. Slightly angled braces stop it reading
    # as disconnected vertical sticks while preserving existing animation names.
    meshes["gear_left"] = rotated_box(-2.45, 0.76, 0.35, 0.11, 1.18, 0.30, z_degrees=-8.0)
    meshes["gear_right"] = rotated_box(2.45, 0.76, 0.35, 0.11, 1.18, 0.30, z_degrees=8.0)
    meshes["gear_oleo_left"] = _v05.cylinder(-2.45, 0.63, 0.35, 0.045, 0.98, axis="y", segments=14)
    meshes["gear_oleo_right"] = _v05.cylinder(2.45, 0.63, 0.35, 0.045, 0.98, axis="y", segments=14)
    meshes["gear_scissors_left"] = rotated_box(-2.45, 0.43, 0.20, 0.06, 0.34, 0.18, z_degrees=-18.0)
    meshes["gear_scissors_right"] = rotated_box(2.45, 0.43, 0.20, 0.06, 0.34, 0.18, z_degrees=18.0)
    meshes["tire_left"] = _v05.cylinder(-2.45, 0.16, 0.35, 0.17, 0.20, axis="x", segments=22)
    meshes["tire_right"] = _v05.cylinder(2.45, 0.16, 0.35, 0.17, 0.20, axis="x", segments=22)
    meshes["tire_nose"] = _v05.cylinder(0.0, 0.15, 3.35, 0.14, 0.18, axis="x", segments=20)
    meshes["gear_nose"] = rotated_box(0.0, 0.62, 3.33, 0.10, 0.94, 0.22, z_degrees=0.0)
    meshes["gear_oleo_nose"] = _v05.cylinder(0.0, 0.56, 3.33, 0.040, 0.78, axis="y", segments=14)

    # Remove a few legacy slab overlays that compete with the cleaner silhouette.
    for obsolete in ("livery_stripe_lower", "livery_tail_sweep", "spinner_stripe_l", "spinner_stripe_r"):
        meshes.pop(obsolete, None)
    meshes["livery_stripe"] = _v05.box(0.0, 1.06, 0.25, 1.66, 0.075, 5.65)
    return {name: outward_winding(mesh) for name, mesh in meshes.items()}


def main() -> None:
    AIRCRAFT.mkdir(parents=True, exist_ok=True)
    meshes = v06_meshes()
    _v05.write_kit(AIRCRAFT, BASENAME, meshes)
    vertices = sum(len(vertices) for vertices, _ in meshes.values())
    triangles = sum(len(indices) // 3 for _, indices in meshes.values())
    print(f"AIR-001 v06 ready: {BASENAME} ({len(meshes)} parts, {vertices} vertices, {triangles} triangles)")


if __name__ == "__main__":
    main()
