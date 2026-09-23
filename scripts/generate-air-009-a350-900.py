#!/usr/bin/env python3
"""Generate AIR-009: an original, unbranded A350-900-class widebody kit.

Purpose-built geometry captures the A350's 5.96 m cabin, long tapered nose,
black-mask flight deck, high-aspect-ratio wing with raked tips, large turbofans
and ten-wheel landing gear inside the official 66.80 x 64.75 x 17.05 m envelope.

Coordinates follow Airside convention: X span, Y up, +Z forward. The local
origin is the nose-stop datum (nose at Z=0, tail at negative Z); tyres touch Y=0.
"""

from __future__ import annotations

import importlib.util
from pathlib import Path

import numpy as np

SCRIPTS = Path(__file__).resolve().parent
REPO = SCRIPTS.parent
AIRCRAFT = REPO / "game" / "Airside" / "Assets" / "Airside" / "Art" / "Models" / "Aircraft"
BASENAME = "mdl_a350_900_v01"

_BASE_SPEC = importlib.util.spec_from_file_location(
    "airside_737_8", SCRIPTS / "generate-air-005-narrowbody-737-8.py"
)
base = importlib.util.module_from_spec(_BASE_SPEC)
assert _BASE_SPEC.loader is not None
_BASE_SPEC.loader.exec_module(base)

_SKIN_SPEC = importlib.util.spec_from_file_location(
    "airside_aircraft_skin", SCRIPTS / "aircraft_skin.py"
)
skin = importlib.util.module_from_spec(_SKIN_SPEC)
assert _SKIN_SPEC.loader is not None
_SKIN_SPEC.loader.exec_module(skin)

box = base.box
cylinder = base.cylinder
lofted_aerofoil = base.lofted_aerofoil
oval_lathe_fuselage = base.oval_lathe_fuselage
orient_outward = base.orient_outward
write_kit = base.write_kit

TARGET_LENGTH_M = 66.80
TARGET_SPAN_M = 64.75
TARGET_HEIGHT_M = 17.05
HALF_LENGTH = TARGET_LENGTH_M / 2.0
HALF_SPAN = TARGET_SPAN_M / 2.0
FUSE_RX = 2.98
FUSE_RY = 3.02

# Tail-to-nose stations: (z, radius x, radius y, centre y). The 0.12 m inset is
# restored by oval_lathe_fuselage's end caps, yielding the exact length.
FUSELAGE_STATIONS = [
    (-HALF_LENGTH + 0.12, 0.10, 0.08, 6.12),
    (-32.55, 0.48, 0.42, 6.17),
    (-31.35, 1.25, 1.18, 6.20),
    (-29.40, 2.15, 2.10, 6.22),
    (-26.60, 2.75, 2.78, 6.24),
    (-22.00, FUSE_RX, FUSE_RY, 6.25),
    (-10.00, FUSE_RX, FUSE_RY, 6.25),
    (4.00, FUSE_RX, FUSE_RY, 6.25),
    (17.00, FUSE_RX, FUSE_RY, 6.25),
    (24.00, 2.92, 2.98, 6.27),
    (27.60, 2.72, 2.72, 6.30),
    (29.70, 2.30, 2.20, 6.32),
    (31.20, 1.62, 1.48, 6.30),
    (32.25, 0.82, 0.70, 6.24),
    (HALF_LENGTH - 0.12, 0.10, 0.08, 6.16),
]


def _surface(z: float, angle_deg: float, offset: float = 0.0) -> np.ndarray:
    zs = np.asarray([s[0] for s in FUSELAGE_STATIONS], dtype=np.float32)
    rx = float(np.interp(z, zs, [s[1] for s in FUSELAGE_STATIONS]))
    ry = float(np.interp(z, zs, [s[2] for s in FUSELAGE_STATIONS]))
    cy = float(np.interp(z, zs, [s[3] for s in FUSELAGE_STATIONS]))
    angle = np.deg2rad(angle_deg)
    return np.asarray(
        [(rx + offset) * np.cos(angle), cy + (ry + offset) * np.sin(angle), z],
        dtype=np.float32,
    )


def _surface_patch(corners, offset=0.018):
    points = np.asarray([_surface(z, angle, offset) for z, angle in corners], np.float32)
    centre = points.mean(axis=0)
    _, _, basis = np.linalg.svd(points - centre, full_matrices=False)
    normal = basis[-1]
    mean_angle = np.deg2rad(np.mean([angle for _, angle in corners]))
    if np.dot(normal, [np.cos(mean_angle), np.sin(mean_angle), 0.0]) < 0:
        normal = -normal
    front = points - ((points - centre) @ normal)[:, None] * normal + normal * 0.008
    back = front - normal * 0.012
    verts = np.vstack((front, back)).astype(np.float32)
    count = len(corners)
    indices: list[int] = []
    for i in range(1, count - 1):
        indices.extend((0, i, i + 1, count, count + i + 1, count + i))
    for i in range(count):
        j = (i + 1) % count
        indices.extend((i, count + j, j, i, count + i, count + j))
    return verts, np.asarray(indices, np.uint16)


# Passenger windows: tall rounded panes at the real 0.51 m frame pitch, centred ~0.9 m above the
# cabin axis (they used to sit ~1.4 m up, level with the crown). Two panes share a node so the
# part count stays where it was. Doors are 1.07 m x 1.9 m and follow the skin curve.
WINDOW_PITCH = 0.508
WINDOW_ANGLE_DEG = 17.0


def _window(z: float, side: float):
    angle = 180.0 - WINDOW_ANGLE_DEG if side < 0 else WINDOW_ANGLE_DEG
    return skin.window(_surface, z, angle, width=0.25, height=0.36)


def _window_pair(z: float, side: float, skip):
    panes = [_window(z - k * WINDOW_PITCH, side) for k in range(2) if not skip(z - k * WINDOW_PITCH)]
    return skin.merge_meshes(panes) if panes else None


def _door(z: float, side: float):
    return skin.skin_patch(_surface, z, 180.0 if side < 0 else 0.0, 0.53, 0.95,
                           front=0.006, radius=0.14, rings=3, max_edge=0.16)


def _pane(z: float, angle: float, half_len: float, half_arc: float, front: float):
    return skin.skin_patch(_surface, z, angle, half_len, half_arc, front=front, radius=0.08, rings=2, max_edge=0.14)


WING_STATIONS = (
    (2.35, 6.35, 11.8, 13.2, 0.92),
    (8.00, 6.55, 9.4, 10.8, 0.72),
    (16.00, 7.05, 5.9, 7.4, 0.46),
    (24.00, 7.85, 1.9, 4.2, 0.25),
    (30.80, 9.10, -1.9, 2.15, 0.14),
    (HALF_SPAN, 10.45, -3.5, 0.72, 0.08),
)


def _wing(side: float):
    return lofted_aerofoil(
        [(side * x, y, leading, chord, thickness)
         for x, y, leading, chord, thickness in WING_STATIONS], chord_points=28)


def _wing_control(side: float, inner: int, outer: int, rear_fraction: float):
    stations = []
    for x, y, leading, chord, thickness in WING_STATIONS[inner:outer + 1]:
        control_chord = chord * rear_fraction
        trailing = leading - chord
        stations.append((side * x, y - thickness * 0.08, trailing + control_chord,
                         control_chord, max(0.055, thickness * 0.20)))
    return lofted_aerofoil(stations, chord_points=12)


def _nacelle(cx: float):
    stations = [
        (-2.20, 0.62, 0.55, 3.90), (-1.55, 1.02, 0.92, 3.94),
        (0.10, 1.50, 1.40, 4.00), (2.50, 1.82, 1.74, 4.05),
        (5.50, 1.92, 1.85, 4.08), (7.45, 1.88, 1.82, 4.07),
        (8.00, 1.78, 1.72, 4.05),
    ]
    verts, indices = oval_lathe_fuselage(stations, segments=64)
    verts[:, 0] += cx
    return verts, indices


def _annulus(cx, cy, z_front, z_back, outer, inner, segments=64):
    verts: list[list[float]] = []
    indices: list[int] = []
    for z in (z_front, z_back):
        for radius in (outer, inner):
            for i in range(segments):
                angle = 2.0 * np.pi * i / segments
                verts.append([cx + radius * np.cos(angle), cy + radius * np.sin(angle), z])
    def ring(z_index, radius_index):
        return (z_index * 2 + radius_index) * segments
    for i in range(segments):
        j = (i + 1) % segments
        of, inf = ring(0, 0), ring(0, 1)
        ob, inb = ring(1, 0), ring(1, 1)
        indices.extend((of+i, of+j, ob+j, of+i, ob+j, ob+i))
        indices.extend((inf+i, inb+j, inf+j, inf+i, inb+i, inb+j))
        indices.extend((of+i, inf+j, of+j, of+i, inf+i, inf+j))
        indices.extend((ob+i, ob+j, inb+j, ob+i, inb+j, inb+i))
    return orient_outward(np.asarray(verts, np.float32), np.asarray(indices, np.uint16))


def _fan_blade(cx: float, cy: float, z: float, angle_deg: float):
    angle = np.deg2rad(angle_deg)
    corners = []
    for radius, sweep in ((0.25, -7.0), (0.25, 7.0), (1.42, 17.0), (1.42, 5.0)):
        a = angle + np.deg2rad(sweep)
        corners.append((cx + radius * np.cos(a), cy + radius * np.sin(a)))
    points = np.asarray(
        [(x, y, z - 0.045) for x, y in corners] + [(x, y, z + 0.045) for x, y in corners],
        np.float32)
    return base._closed_prism(points)


def _wheel(meshes, key: str, x: float, z: float, radius: float, width: float):
    meshes[f"tire_{key}"] = cylinder(x, radius, z, radius, width, axis="x", segments=36)
    meshes[f"wheel_{key}"] = cylinder(x, radius, z, radius * 0.61, width + 0.04, axis="x", segments=32)
    meshes[f"rim_{key}"] = cylinder(x, radius, z, radius * 0.34, width + 0.07, axis="x", segments=28)


def a350_900_meshes():
    meshes: dict[str, tuple[np.ndarray, np.ndarray]] = {}
    meshes["fuselage"] = oval_lathe_fuselage(FUSELAGE_STATIONS, segments=72)

    # A350 flight deck: dark wraparound mask with six inset panes, all skin-conforming.
    meshes["cockpit_mask_left"] = skin.skin_patch(
        _surface, 30.05, 128.0, 1.45, 0.85, front=0.012, radius=0.45, rings=3, max_edge=0.2)
    meshes["cockpit_mask_right"] = skin.skin_patch(
        _surface, 30.05, 52.0, 1.45, 0.85, front=0.012, radius=0.45, rings=3, max_edge=0.2)
    for side, angles in (("l", (116, 132, 146)), ("r", (64, 48, 34))):
        for index, angle in enumerate(angles, 1):
            meshes[f"windscreen_{side}{index}"] = _pane(30.25, float(angle), 0.72, 0.24, 0.024)

    door_z = (27.1, 10.8, -10.5, -26.2)

    def in_door_zone(z: float) -> bool:
        return min(abs(z - door) for door in door_z) < 0.53 + 0.32

    for side, suffix in ((-1.0, ""), (1.0, "r")):
        index = 0
        for z in np.arange(24.9, -25.1, -2.0 * WINDOW_PITCH):
            pair = _window_pair(float(z), side, in_door_zone)
            if pair is None:
                continue
            index += 1
            meshes[f"cabin_window_{suffix}{index}"] = pair
        for door_index, z in enumerate(door_z, 1):
            meshes[f"door_{'left' if side < 0 else 'right'}_{door_index}"] = _door(z, side)

    meshes["belly_fairing"] = oval_lathe_fuselage(
        [(-12.0, 0.35, 0.10, 3.32), (-7.0, 1.05, 0.38, 3.18),
         (1.0, 1.28, 0.52, 3.10), (10.5, 0.92, 0.31, 3.22),
         (15.0, 0.30, 0.09, 3.36)], segments=44)

    for side, suffix in ((-1.0, "left"), (1.0, "right")):
        meshes[f"wing_{suffix}"] = _wing(side)
        meshes[f"flap_{suffix}"] = _wing_control(side, 0, 3, 0.28)
        meshes[f"aileron_{suffix}"] = _wing_control(side, 3, 5, 0.34)
        meshes[f"spoiler_{suffix}"] = _wing_control(side, 1, 3, 0.47)
        meshes[f"wingtip_{suffix}"] = lofted_aerofoil(
            [(side * 30.0, 8.95, -1.45, 2.3, 0.13),
             (side * 31.4, 9.65, -2.55, 1.45, 0.09),
             (side * HALF_SPAN, 10.45, -3.50, 0.72, 0.055)], chord_points=14)
        # Tip station: x=32.375, y=10.45, chord -3.50..-4.22. Light on the tip, wick off its trailing edge.
        meshes[f"nav_light_{suffix}"] = box(side * 32.30, 10.45, -3.80, 0.10, 0.10, 0.12)
        meshes[f"static_wick_{suffix}"] = box(side * 32.30, 10.44, -4.28, 0.03, 0.02, 0.20)

    for side, suffix in ((-1.0, "left"), (1.0, "right")):
        x = side * 10.75
        meshes[f"engine_{suffix}"] = _nacelle(x)
        meshes[f"nacelle_{suffix}"] = _annulus(x, 4.05, 8.18, 7.55, 1.88, 1.48)
        meshes[f"intake_{suffix}"] = _annulus(x, 4.05, 8.23, 8.08, 1.75, 1.54)
        meshes[f"fan_{suffix}"] = cylinder(x, 4.05, 7.48, 1.43, 0.06, axis="z", segments=64)
        for index, angle in enumerate(np.linspace(0, 360, 18, endpoint=False), 1):
            meshes[f"fan_blade_{suffix[0]}{index}"] = _fan_blade(x, 4.05, 7.53, float(angle))
        meshes[f"pylon_{suffix}"] = box(x, 6.15, 4.6, 0.70, 3.00, 4.20)
        meshes[f"exhaust_{suffix}"] = cylinder(x, 3.92, -2.0, 0.74, 0.72, axis="z", segments=44)

    meshes["tail_fin"] = lofted_aerofoil(
        [(7.85, 0.0, -22.6, 10.8, 0.48), (11.8, 0.0, -26.2, 7.2, 0.30),
         (15.0, 0.0, -29.0, 4.4, 0.20), (TARGET_HEIGHT_M, 0.0, -31.3, 2.1, 0.10)],
        chord_points=22, vertical=True)
    meshes["rudder"] = lofted_aerofoil(
        [(8.8, 0.0, -31.6, 1.8, 0.12), (15.8, 0.0, -32.4, 1.0, 0.07)],
        chord_points=12, vertical=True)
    meshes["tailplane"] = lofted_aerofoil(
        [(-12.9, 8.0, -29.5, 3.3, 0.16), (-3.0, 7.85, -26.2, 6.5, 0.30),
         (3.0, 7.85, -26.2, 6.5, 0.30), (12.9, 8.0, -29.5, 3.3, 0.16)],
        chord_points=18)
    meshes["elevator_left"] = box(-6.4, 7.95, -31.0, 11.6, 0.08, 1.15)
    meshes["elevator_right"] = box(6.4, 7.95, -31.0, 11.6, 0.08, 1.15)

    # Fitted operator sash (ADR 0112): skin-conforming like the A330/A320/737 kits, so the
    # widebody no longer needs the repeating traffic decal that barcoded its fuselage.
    meshes["livery_stripe"] = skin.livery_ribbon(
        _surface, 23.5, -27.5, -1, half_width=0.40, rise_degrees=18.0, samples=96)
    meshes["livery_stripe_lower"] = skin.livery_ribbon(
        _surface, 23.5, -27.5, 1, half_width=0.40, rise_degrees=18.0, samples=96)

    # Published A350-900 wheelbase is 28.66 m (nose gear at 25.2 -> mains at -3.46); the mains
    # used to sit at -5.8, 4.6 m behind the wing's trailing edge with nothing above them. A
    # gear-bay pod now grows out of the fuselage belly over each leg, and every wheel truck
    # has an axle so no tyre floats beside its strut.
    nose_z = 25.2
    main_z = -3.46
    main_x = 4.35
    meshes["gear_nose"] = skin.merge_meshes([
        box(0.0, 2.15, nose_z, 0.24, 3.25, 0.45),
        box(0.0, 0.58, nose_z, 0.96, 0.12, 0.12),
    ])
    for side, prefix in ((-1.0, "left"), (1.0, "right")):
        meshes[f"gear_{prefix}"] = skin.merge_meshes([
            box(side * main_x, 2.45, main_z, 0.28, 3.55, 0.52),
            box(side * main_x, 0.72, main_z, 0.20, 0.16, 1.70),                 # bogie beam
            box(side * main_x, 0.72, main_z + 0.78, 1.16, 0.14, 0.14),          # forward axle
            box(side * main_x, 0.72, main_z - 0.78, 1.16, 0.14, 0.14),          # aft axle
        ])
        meshes[f"gear_fairing_{prefix}"] = skin.x_tube(
            side * 1.8, side * (main_x + 0.55), 4.15, main_z, 0.48, 1.7)
    _wheel(meshes, "nose_left", -0.34, nose_z, 0.58, 0.25)
    _wheel(meshes, "nose_right", 0.34, nose_z, 0.58, 0.25)
    for side, prefix in ((-1.0, "left"), (1.0, "right")):
        for fore_aft, z_offset in (("forward", 0.78), ("aft", -0.78)):
            _wheel(meshes, f"{prefix}_{fore_aft}_inboard", side * 4.02, main_z + z_offset, 0.72, 0.29)
            _wheel(meshes, f"{prefix}_{fore_aft}_outboard", side * 4.72, main_z + z_offset, 0.72, 0.29)

    meshes["beacon_top"] = box(0.0, 9.31, 2.0, 0.12, 0.12, 0.12)
    meshes["beacon_bottom"] = box(0.0, 3.22, 1.0, 0.12, 0.12, 0.12)
    # Landing lights on the wing-root leading-edge underside (they hung 0.5 m below the wing).
    meshes["landing_light_l"] = box(-7.8, 6.40, 9.36, 0.28, 0.16, 0.12)
    meshes["landing_light_r"] = box(7.8, 6.40, 9.36, 0.28, 0.16, 0.12)
    meshes["taxi_light"] = box(0.0, 1.75, nose_z + 0.25, 0.18, 0.14, 0.14)

    shift = np.asarray((0.0, 0.0, -HALF_LENGTH), np.float32)
    return {name: orient_outward(vertices + shift, indices)
            for name, (vertices, indices) in meshes.items()}


def validate_meshes(meshes):
    if not meshes:
        raise ValueError("AIR-009 emitted no meshes")
    for name, (vertices, indices) in meshes.items():
        if len(vertices) == 0 or len(indices) == 0 or len(indices) % 3:
            raise ValueError(f"{name}: invalid triangle mesh")
        if not np.isfinite(vertices).all() or int(indices.max()) >= len(vertices):
            raise ValueError(f"{name}: invalid vertex/index data")
    vertices = np.concatenate([v for v, _ in meshes.values()])
    minimum, maximum = vertices.min(axis=0), vertices.max(axis=0)
    dimensions = maximum - minimum
    expected = np.asarray((TARGET_SPAN_M, TARGET_HEIGHT_M, TARGET_LENGTH_M), np.float32)
    if not np.allclose(dimensions, expected, atol=0.025, rtol=0.0):
        raise ValueError(f"AIR-009 bounds {dimensions.tolist()} do not match {expected.tolist()}")
    if abs(float(minimum[1])) > 0.02 or abs(float(maximum[2])) > 0.02:
        raise ValueError(f"AIR-009 datum invalid: min y={minimum[1]:.3f}, max z={maximum[2]:.3f}")
    return minimum, maximum


def main():
    AIRCRAFT.mkdir(parents=True, exist_ok=True)
    kit = a350_900_meshes()
    minimum, maximum = validate_meshes(kit)
    write_kit(AIRCRAFT, BASENAME, kit)
    triangles = sum(len(indices) // 3 for _, indices in kit.values())
    dimensions = maximum - minimum
    print(f"AIR-009 ready: {BASENAME} ({len(kit)} meshes, {triangles} triangles; "
          f"{dimensions[0]:.2f} x {dimensions[1]:.2f} x {dimensions[2]:.2f} m)")


if __name__ == "__main__":
    main()
