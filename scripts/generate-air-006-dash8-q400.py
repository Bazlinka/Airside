#!/usr/bin/env python3
"""Generate AIR-006: an original Dash 8-400-class regional turboprop kit.

Visual revision focused on a believable aircraft read: long slender fuselage,
cabin-fitted windows, pitched flight-deck panes, continuous nacelles with
main-gear bays, six-blade props, high wing with tip fences, and a joined
T-tail. Project-owned, unbranded procedural geometry at the official
32.83 × 28.42 × 8.34 m envelope.

Coordinates follow Airside's regional-aircraft convention: X is span, Y is up,
+Z is forward, the model is centred longitudinally on the motion root, and the
tyres touch local Y=0. The companion glTF stays inside the deliberately small
POSITION + uint16-index contract consumed by ArtGltfLoader.
"""

from __future__ import annotations

import importlib.util
from pathlib import Path

import numpy as np


SCRIPTS = Path(__file__).resolve().parent
REPO = SCRIPTS.parent
AIRCRAFT = REPO / "game" / "Airside" / "Assets" / "Airside" / "Art" / "Models" / "Aircraft"
BASENAME = "mdl_dash8_q400_v01"

_SPEC = importlib.util.spec_from_file_location(
    "airside_air001_v06", SCRIPTS / "generate-air-001-v06.py"
)
_v06 = importlib.util.module_from_spec(_SPEC)
assert _SPEC.loader is not None
_SPEC.loader.exec_module(_v06)
_v05 = _v06._v05

box = _v05.box
cylinder = _v05.cylinder
oval_lathe_fuselage = _v05.oval_lathe_fuselage
lofted_aerofoil = _v05.lofted_aerofoil
write_kit = _v05.write_kit
windscreen_pane = _v05.windscreen_pane
outward_winding = _v06.outward_winding
propeller_blade = _v06.propeller_blade

TARGET_LENGTH_M = 32.83
TARGET_SPAN_M = 28.42
TARGET_HEIGHT_M = 8.34
HALF_LENGTH = TARGET_LENGTH_M / 2.0
HALF_SPAN = TARGET_SPAN_M / 2.0
PROP_RADIUS = 2.05

# z, lateral radius, vertical radius, centre height.
# Long parallel cabin, rounded flight-deck shoulders, soft radome (not a needle),
# and an upswept rear pressure body into the T-tail root.
STATIONS = np.array(
    [
        (-HALF_LENGTH + 0.12, 0.05, 0.04, 2.58),
        (-15.90, 0.18, 0.15, 2.55),
        (-15.35, 0.42, 0.36, 2.50),
        (-14.70, 0.72, 0.64, 2.42),
        (-13.90, 0.98, 0.90, 2.34),
        (-12.90, 1.18, 1.12, 2.28),
        (-11.50, 1.30, 1.26, 2.24),
        (-9.60, 1.35, 1.32, 2.22),
        (-6.50, 1.36, 1.34, 2.22),
        (-2.50, 1.36, 1.34, 2.22),
        (2.50, 1.36, 1.34, 2.22),
        (7.20, 1.36, 1.34, 2.22),
        (10.00, 1.35, 1.33, 2.21),
        (11.60, 1.32, 1.28, 2.16),
        (12.70, 1.24, 1.18, 2.08),
        (13.50, 1.12, 1.04, 1.96),
        (14.15, 0.96, 0.88, 1.82),
        (14.70, 0.76, 0.68, 1.68),
        (15.15, 0.54, 0.48, 1.54),
        (15.50, 0.34, 0.30, 1.44),
        (15.80, 0.18, 0.16, 1.36),
        (HALF_LENGTH - 0.12, 0.05, 0.04, 1.30),
    ],
    dtype=np.float32,
)

FUSE_SEGMENTS = 64


def translated(mesh, x, y, z):
    vertices, indices = mesh
    moved = vertices.copy()
    moved += np.array((x, y, z), np.float32)
    return moved, indices.copy()


def surface(z, theta, offset=0.0):
    rx = float(np.interp(z, STATIONS[:, 0], STATIONS[:, 1]))
    ry = float(np.interp(z, STATIONS[:, 0], STATIONS[:, 2]))
    cy = float(np.interp(z, STATIONS[:, 0], STATIONS[:, 3]))
    return np.array(
        [(rx + offset) * np.cos(theta), cy + (ry + offset) * np.sin(theta), z],
        np.float32,
    )


def fuselage_body():
    zs = np.concatenate(
        [
            np.linspace(a, b, 3, endpoint=False)
            for a, b in zip(STATIONS[:-1, 0], STATIONS[1:, 0])
        ]
        + [[float(STATIONS[-1, 0])]]
    )
    thetas = np.linspace(0.0, 2.0 * np.pi, FUSE_SEGMENTS, endpoint=False)
    verts = np.array(
        [surface(float(z), float(t)) for z in zs for t in thetas], np.float32
    )
    faces: list[int] = []
    rings = len(zs)
    for j in range(rings - 1):
        for i in range(FUSE_SEGMENTS):
            a = j * FUSE_SEGMENTS + i
            b = j * FUSE_SEGMENTS + (i + 1) % FUSE_SEGMENTS
            c = b + FUSE_SEGMENTS
            d = a + FUSE_SEGMENTS
            faces.extend([a, b, c, a, c, d])
    for ring, tip_y, tip_z in (
        (0, float(STATIONS[0, 3]), float(zs[0]) - 0.12),
        (rings - 1, float(STATIONS[-1, 3]), float(zs[-1]) + 0.12),
    ):
        centre = len(verts)
        verts = np.vstack([verts, [0.0, tip_y, tip_z]]).astype(np.float32)
        for i in range(FUSE_SEGMENTS):
            edge = [ring * FUSE_SEGMENTS + i, ring * FUSE_SEGMENTS + (i + 1) % FUSE_SEGMENTS]
            if ring == 0:
                edge.reverse()
            faces.extend([centre, *edge])
    return verts, np.asarray(faces, np.uint16)


def surface_quad(corners, offset=0.022):
    """Flat pane fitted to the local fuselage surface."""
    sampled = np.asarray(
        [surface(z, np.deg2rad(theta), offset) for z, theta in corners], np.float64
    )
    centre = sampled.mean(axis=0)
    _, _, basis = np.linalg.svd(sampled - centre, full_matrices=False)
    normal = basis[-1]
    mean_theta = np.deg2rad(np.mean([theta for _, theta in corners]))
    outward = np.array([np.cos(mean_theta), np.sin(mean_theta), 0.0])
    if np.dot(normal, outward) < 0:
        normal = -normal
    front = sampled - ((sampled - centre) @ normal)[:, None] * normal
    front = front + normal * 0.008
    back = front - normal * 0.010
    verts = np.vstack([front, back]).astype(np.float32)
    faces: list[int] = []
    count = len(corners)
    for i in range(1, count - 1):
        faces.extend([0, i, i + 1, count, count + i + 1, count + i])
    for i in range(count):
        j = (i + 1) % count
        faces.extend([i, count + j, j, i, count + i, count + j])
    return verts, np.asarray(faces, np.uint16)


def cabin_window(z, side):
    # Slightly proud of the skin so glass reads at overview distance, but close
    # enough that panes look let into the cabin rather than stuck on.
    if side < 0:
        return surface_quad(
            [
                (z - 0.20, 147.5),
                (z - 0.16, 145.8),
                (z + 0.16, 145.8),
                (z + 0.20, 147.5),
                (z + 0.20, 157.5),
                (z + 0.16, 159.2),
                (z - 0.16, 159.2),
                (z - 0.20, 157.5),
            ],
            0.012,
        )
    return surface_quad(
        [
            (z - 0.20, 32.5),
            (z - 0.16, 34.2),
            (z + 0.16, 34.2),
            (z + 0.20, 32.5),
            (z + 0.20, 22.5),
            (z + 0.16, 20.8),
            (z - 0.16, 20.8),
            (z - 0.20, 22.5),
        ],
        0.012,
    )


def door_patch(z, half_z, half_h, side, depth=0.018):
    theta0 = 180.0 if side < 0 else 0.0
    deg = half_h / 1.34 * 57.3 * 0.55
    return surface_quad(
        [
            (z - half_z, theta0 - deg),
            (z - half_z, theta0 + deg),
            (z + half_z, theta0 + deg),
            (z + half_z, theta0 - deg),
        ],
        depth,
    )


def panel(
    side,
    x_inner,
    x_outer,
    y_inner,
    y_outer,
    leading_inner,
    leading_outer,
    chord_inner,
    chord_outer,
    thickness,
):
    return lofted_aerofoil(
        [
            (side * x_inner, y_inner, leading_inner, chord_inner, thickness),
            (side * x_outer, y_outer, leading_outer, chord_outer, thickness * 0.65),
        ],
        chord_points=12,
    )


def wheel_set(meshes, prefix, x, z, radius, width):
    # cy = radius puts the tyre contact patch on y=0 when axis is X.
    # Segment counts are multiples of 4 so a vertex lands exactly at bottom.
    meshes[f"tire_{prefix}"] = cylinder(
        x, radius, z, radius, width, axis="x", segments=24
    )
    meshes[f"wheel_{prefix}"] = cylinder(
        x, radius, z, radius * 0.62, width + 0.025, axis="x", segments=20
    )
    meshes[f"rim_{prefix}"] = cylinder(
        x, radius, z, radius * 0.34, width + 0.045, axis="x", segments=16
    )


def nacelle_pod(x):
    """One continuous nacelle: intake, core, and long main-gear bay."""
    stations = [
        (5.40, 0.16, 0.14, 3.62),
        (5.15, 0.38, 0.34, 3.60),
        (4.70, 0.55, 0.50, 3.58),
        (4.00, 0.66, 0.60, 3.54),
        (2.80, 0.72, 0.66, 3.50),
        (1.20, 0.74, 0.70, 3.48),
        (-0.20, 0.75, 0.74, 3.44),
        (-1.40, 0.74, 0.82, 3.30),
        (-2.50, 0.68, 0.92, 3.05),
        (-3.40, 0.55, 0.95, 2.78),
        (-4.20, 0.38, 0.72, 2.55),
        (-4.85, 0.22, 0.38, 2.42),
        (-5.25, 0.10, 0.14, 2.36),
    ]
    segs = 40
    rings = []
    for z, rx, ry, cy in stations:
        ring = []
        for i in range(segs):
            ang = 2.0 * np.pi * i / segs
            ring.append([x + rx * np.cos(ang), cy + ry * np.sin(ang), z])
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
    for ring, z_sign in ((rings[0], 1.0), (rings[-1], -1.0)):
        tip = ring.mean(axis=0).copy()
        tip[2] += z_sign * 0.08
        for i in range(segs):
            j = (i + 1) % segs
            base = len(verts)
            if z_sign > 0:
                verts.extend([tip, ring[i], ring[j]])
            else:
                verts.extend([tip, ring[j], ring[i]])
            indices.extend([base, base + 1, base + 2])
    return np.asarray(verts, np.float32), np.asarray(indices, np.uint16)


def wing_fairing(side):
    # Low, elongated wing-root saddle — fills the wing/cabin join without a
    # chunky "block on the roof" silhouette.
    fair = [
        (-2.20, 0.08, 0.05, 4.28),
        (-1.40, 0.22, 0.12, 4.38),
        (-0.40, 0.30, 0.16, 4.44),
        (0.80, 0.28, 0.15, 4.44),
        (1.70, 0.16, 0.09, 4.38),
        (2.30, 0.06, 0.04, 4.30),
    ]
    segs = 28
    rings = []
    for z, rx, ry, cy in fair:
        ring = []
        for i in range(segs):
            ang = 2.0 * np.pi * i / segs
            ring.append(
                [side * (1.12 + abs(rx * np.cos(ang))), cy + ry * np.sin(ang), z]
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
    return np.asarray(verts, np.float32), np.asarray(indices, np.uint16)


def q400_meshes():
    meshes = {}

    meshes["fuselage"] = fuselage_body()

    wing_root = (1.15, 4.48, 3.25, 4.35, 0.42)
    wing_mid = (6.40, 4.62, 2.85, 2.95, 0.26)
    wing_outer = (11.80, 4.78, 2.15, 1.65, 0.14)
    wing_tip = (HALF_SPAN, 4.92, 1.72, 1.05, 0.09)
    for side, name in ((-1.0, "left"), (1.0, "right")):
        meshes[f"wing_{name}"] = lofted_aerofoil(
            [
                (side * x, y, z, chord, thick)
                for x, y, z, chord, thick in (
                    wing_root,
                    wing_mid,
                    wing_outer,
                    wing_tip,
                )
            ],
            chord_points=22,
        )
        meshes[f"flap_{name}"] = panel(
            side, 1.55, 7.20, 4.38, 4.55, -0.05, 0.18, 1.20, 0.78, 0.11
        )
        meshes[f"aileron_{name}"] = panel(
            side, 7.60, 13.40, 4.70, 4.86, 0.28, 0.58, 0.70, 0.34, 0.07
        )
        meshes[f"spoiler_{name}"] = panel(
            side, 2.40, 6.80, 4.62, 4.72, 1.55, 1.35, 0.55, 0.42, 0.04
        )
        meshes[f"wing_fairing_{name}"] = wing_fairing(side)
        meshes[f"winglet_{name}"] = lofted_aerofoil(
            [
                (side * (HALF_SPAN - 0.08), 4.88, 1.55, 0.95, 0.08),
                (side * (HALF_SPAN - 0.02), 5.35, 1.35, 0.55, 0.05),
            ],
            chord_points=10,
        )
        meshes[f"wing_fence_{name}"] = box(side * 7.35, 4.78, 1.85, 0.04, 0.28, 0.85)
        meshes[f"flap_track_{name[0]}1"] = box(side * 3.20, 4.22, -0.55, 0.10, 0.16, 0.55)
        meshes[f"flap_track_{name[0]}2"] = box(side * 5.80, 4.35, -0.20, 0.10, 0.14, 0.48)

    for side, name in ((-1.0, "left"), (1.0, "right")):
        x = side * 4.35
        meshes[f"engine_{name}"] = nacelle_pod(x)
        meshes[f"intake_{name}"] = cylinder(
            x, 3.62, 5.15, 0.42, 0.22, axis="z", segments=28
        )
        meshes[f"exhaust_{name}"] = cylinder(
            x, 3.35, -3.55, 0.16, 0.70, axis="z", segments=18
        )
        meshes[f"exhaust_stack_{name[0]}"] = box(
            x + side * 0.55, 3.85, -1.80, 0.18, 0.22, 1.10
        )
        meshes[f"pylon_{name}"] = box(x, 4.20, 0.55, 0.48, 0.95, 2.85)
        meshes[f"oil_cooler_{name[0]}"] = box(x, 3.05, 2.40, 0.55, 0.22, 0.85)
        meshes[f"cowl_flap_{name[0]}"] = box(x, 3.15, 1.10, 0.70, 0.08, 0.55)

        for index, suffix in enumerate(("", "_b", "_c", "_d", "_e", "_f")):
            angle = index * 60.0
            meshes[f"propeller_{name}{suffix}"] = propeller_blade(
                x, 3.60, 5.55, angle, radial_start=0.28, radial_end=1.82
            )
            meshes[f"propeller_{name}_tip{suffix}"] = propeller_blade(
                x,
                3.60,
                5.55,
                angle,
                radial_start=1.80,
                radial_end=PROP_RADIUS,
                tip=True,
            )
        meshes[f"prop_hub_{name}"] = cylinder(
            x, 3.60, 5.50, 0.30, 0.34, axis="z", segments=26
        )
        meshes[f"spinner_{name}"] = translated(
            oval_lathe_fuselage(
                [
                    (5.40, 0.28, 0.28, 3.60),
                    (5.75, 0.34, 0.34, 3.60),
                    (6.05, 0.18, 0.18, 3.60),
                    (6.28, 0.04, 0.04, 3.60),
                ],
                segments=28,
            ),
            x,
            0.0,
            0.0,
        )
        meshes[f"spinner_stripe_{name[0]}"] = cylinder(
            x, 3.60, 5.72, 0.33, 0.06, axis="z", segments=24
        )

    # Fin tip owns the exact 8.34 m height.
    meshes["tail_fin"] = lofted_aerofoil(
        [
            (3.35, 0.0, -9.60, 5.40, 0.32),
            (5.40, 0.0, -11.40, 3.60, 0.22),
            (7.20, 0.0, -12.90, 2.40, 0.15),
            (8.34, 0.0, -14.05, 1.45, 0.10),
        ],
        chord_points=18,
        vertical=True,
    )
    meshes["dorsal_fin"] = lofted_aerofoil(
        [
            (3.20, 0.0, -7.80, 2.80, 0.20),
            (4.80, 0.0, -10.40, 1.60, 0.12),
        ],
        chord_points=12,
        vertical=True,
    )
    meshes["tail_root_fairing"] = lofted_aerofoil(
        [
            (2.55, 0.0, -8.40, 2.20, 0.28),
            (3.55, 0.0, -10.20, 1.40, 0.16),
        ],
        chord_points=10,
        vertical=True,
    )
    meshes["rudder"] = lofted_aerofoil(
        [
            (4.10, 0.0, -14.35, 0.72, 0.08),
            (7.85, 0.0, -14.85, 0.55, 0.06),
        ],
        chord_points=10,
        vertical=True,
    )
    meshes["tailplane"] = lofted_aerofoil(
        [
            (-6.80, 7.95, -13.45, 1.25, 0.09),
            (-2.40, 7.90, -12.35, 2.20, 0.16),
            (-0.20, 7.88, -11.95, 2.55, 0.18),
            (0.20, 7.88, -11.95, 2.55, 0.18),
            (2.40, 7.90, -12.35, 2.20, 0.16),
            (6.80, 7.95, -13.45, 1.25, 0.09),
        ],
        chord_points=14,
    )
    meshes["tailplane_saddle"] = box(0.0, 7.72, -12.55, 0.85, 0.35, 1.80)
    meshes["elevator_left"] = panel(
        -1.0, 0.40, 6.40, 7.88, 7.93, -14.35, -14.65, 0.55, 0.38, 0.06
    )
    meshes["elevator_right"] = panel(
        1.0, 0.40, 6.40, 7.88, 7.93, -14.35, -14.65, 0.55, 0.38, 0.06
    )
    meshes["tailplane_tip_l"] = box(-6.70, 7.95, -13.55, 0.35, 0.08, 0.55)
    meshes["tailplane_tip_r"] = box(6.70, 7.95, -13.55, 0.35, 0.08, 0.55)

    for index, z in enumerate(np.linspace(10.35, -8.60, 18), start=1):
        meshes[f"cabin_window_{index}"] = cabin_window(float(z), -1.0)
        meshes[f"cabin_window_r{index}"] = cabin_window(float(z), 1.0)

    meshes["windscreen_l"] = windscreen_pane(
        -0.48, 2.72, 14.05, 0.92, 0.78, 0.055, pitch_deg=22
    )
    meshes["windscreen_r"] = windscreen_pane(
        0.48, 2.72, 14.05, 0.92, 0.78, 0.055, pitch_deg=22
    )
    meshes["cockpit_side_l"] = surface_quad(
        [(12.95, 122), (12.95, 152), (13.95, 148), (14.25, 120)], 0.014
    )
    meshes["cockpit_side_r"] = surface_quad(
        [(12.95, 58), (12.95, 28), (14.25, 60), (13.95, 32)], 0.014
    )
    meshes["windscreen_pillar_l"] = box(-0.92, 2.78, 13.85, 0.05, 0.72, 0.50)
    meshes["windscreen_pillar_r"] = box(0.92, 2.78, 13.85, 0.05, 0.72, 0.50)
    meshes["windscreen_pillar_c"] = box(0.0, 2.88, 14.15, 0.045, 0.58, 0.32)
    meshes["cockpit_glare"] = box(0.0, 3.18, 13.45, 1.60, 0.07, 0.90)

    meshes["livery_stripe"] = box(-1.355, 2.05, 0.60, 0.035, 0.14, 23.5)
    meshes["livery_stripe_lower"] = box(1.355, 2.05, 0.60, 0.035, 0.14, 23.5)
    meshes["livery_tail_sweep"] = lofted_aerofoil(
        [(3.80, 0.0, -11.80, 1.80, 0.06), (7.40, 0.0, -13.60, 0.90, 0.04)],
        chord_points=8,
        vertical=True,
    )

    meshes["door_outline_fwd"] = door_patch(11.05, 0.52, 0.95, -1.0, 0.012)
    meshes["door_fwd"] = door_patch(11.05, 0.44, 0.85, -1.0, 0.022)
    meshes["door_handle_fwd"] = door_patch(11.28, 0.06, 0.05, -1.0, 0.030)
    meshes["cargo_door_outline"] = door_patch(-9.40, 0.72, 0.80, 1.0, 0.012)
    meshes["cargo_door"] = door_patch(-9.40, 0.64, 0.72, 1.0, 0.022)
    meshes["cargo_door_latch"] = door_patch(-9.00, 0.07, 0.05, 1.0, 0.030)

    meshes["belly_fairing"] = oval_lathe_fuselage(
        [
            (-6.50, 0.35, 0.12, 1.05),
            (-2.00, 0.55, 0.22, 1.00),
            (4.00, 0.58, 0.24, 1.00),
            (8.50, 0.40, 0.16, 1.05),
            (11.00, 0.18, 0.08, 1.15),
        ],
        segments=28,
    )
    # oval_lathe adds a 0.12 m tip beyond the last station — end inboard so the
    # tip lands exactly on the envelope nose.
    meshes["radome"] = oval_lathe_fuselage(
        [
            (14.40, 0.72, 0.62, 1.70),
            (15.00, 0.48, 0.42, 1.52),
            (15.45, 0.26, 0.22, 1.40),
            (HALF_LENGTH - 0.12, 0.05, 0.04, 1.30),
        ],
        segments=36,
    )

    for side, name in ((-1.0, "left"), (1.0, "right")):
        x = side * 4.35
        meshes[f"gear_{name}"] = box(x, 1.40, -1.85, 0.18, 2.15, 0.28)
        meshes[f"gear_oleo_{name}"] = cylinder(
            x, 1.15, -1.85, 0.08, 1.55, axis="y", segments=16
        )
        meshes[f"gear_scissors_{name}"] = box(
            x + side * 0.12, 1.55, -1.55, 0.06, 0.55, 0.35
        )
        meshes[f"gear_door_{name}"] = box(
            x + side * 0.48, 2.35, -1.85, 0.08, 1.45, 1.35
        )
        meshes[f"gear_fairing_{name}"] = box(x, 2.55, -2.10, 0.95, 0.85, 2.40)
        wheel_set(meshes, f"{name}_forward", x, -1.45, 0.46, 0.28)
        wheel_set(meshes, f"{name}_aft", x, -2.25, 0.46, 0.28)

    meshes["gear_nose"] = box(0.0, 0.95, 12.15, 0.14, 1.25, 0.20)
    meshes["gear_oleo_nose"] = cylinder(
        0.0, 0.72, 12.15, 0.055, 0.85, axis="y", segments=16
    )
    meshes["gear_scissors_nose"] = box(0.0, 1.05, 12.35, 0.05, 0.40, 0.28)
    meshes["gear_door_nose"] = box(0.0, 1.35, 11.95, 0.55, 0.06, 1.15)
    wheel_set(meshes, "nose_left", -0.20, 12.18, 0.32, 0.16)
    wheel_set(meshes, "nose_right", 0.20, 12.18, 0.32, 0.16)

    meshes["nav_light_left"] = box(-HALF_SPAN + 0.05, 4.95, 1.55, 0.10, 0.10, 0.10)
    meshes["nav_light_right"] = box(HALF_SPAN - 0.05, 4.95, 1.55, 0.10, 0.10, 0.10)
    meshes["tail_nav_light"] = box(0.0, 8.20, -14.55, 0.10, 0.10, 0.10)
    meshes["beacon_top"] = box(0.0, 3.62, -1.10, 0.12, 0.12, 0.12)
    meshes["landing_light_l"] = box(-4.35, 3.15, 5.05, 0.22, 0.16, 0.10)
    meshes["landing_light_r"] = box(4.35, 3.15, 5.05, 0.22, 0.16, 0.10)
    meshes["taxi_light"] = box(0.0, 0.78, 12.35, 0.16, 0.12, 0.12)
    meshes["pitot"] = box(-0.35, 1.55, 14.80, 0.04, 0.04, 0.35)
    meshes["pitot_b"] = box(0.35, 1.55, 14.80, 0.04, 0.04, 0.35)
    meshes["antenna"] = box(0.0, 3.60, 4.50, 0.04, 0.35, 0.08)
    meshes["antenna_aft"] = box(0.0, 3.55, -6.80, 0.04, 0.28, 0.08)
    meshes["hf_antenna"] = box(0.0, 7.55, -12.20, 0.03, 0.55, 0.06)
    meshes["static_wick_left"] = box(-HALF_SPAN + 0.15, 4.90, 0.85, 0.02, 0.02, 0.18)
    meshes["static_wick_right"] = box(HALF_SPAN - 0.15, 4.90, 0.85, 0.02, 0.02, 0.18)

    return {name: outward_winding(mesh) for name, mesh in meshes.items()}


def bounds(meshes):
    vertices = np.concatenate([part for part, _ in meshes.values()], axis=0)
    return vertices.min(axis=0), vertices.max(axis=0)


def validate(meshes):
    if not meshes:
        raise ValueError("AIR-006 emitted no meshes")
    for name, (vertices, indices) in meshes.items():
        if len(vertices) == 0 or len(indices) == 0 or len(indices) % 3:
            raise ValueError(f"{name}: empty or non-triangle mesh")
        if len(vertices) > np.iinfo(np.uint16).max or int(indices.max()) >= len(vertices):
            raise ValueError(f"{name}: uint16 index contract violated")
        if not np.isfinite(vertices).all():
            raise ValueError(f"{name}: non-finite vertex")

    minimum, maximum = bounds(meshes)
    dimensions = maximum - minimum
    expected = np.array((TARGET_SPAN_M, TARGET_HEIGHT_M, TARGET_LENGTH_M), np.float32)
    if not np.allclose(dimensions, expected, rtol=0.0, atol=0.02):
        raise ValueError(
            f"AIR-006 bounds {dimensions.tolist()} do not match {expected.tolist()}"
        )
    if abs(float(minimum[1])) > 0.02:
        raise ValueError(f"AIR-006 tyres must touch local y=0, got {minimum[1]:.4f}")
    return minimum, maximum


def main():
    AIRCRAFT.mkdir(parents=True, exist_ok=True)
    meshes = q400_meshes()
    minimum, maximum = validate(meshes)
    write_kit(AIRCRAFT, BASENAME, meshes)
    dimensions = maximum - minimum
    triangles = sum(len(indices) // 3 for _, indices in meshes.values())
    print(
        f"AIR-006 ready: {BASENAME} ({len(meshes)} meshes, {triangles} triangles; "
        f"{dimensions[0]:.2f} m span x {dimensions[1]:.2f} m height x "
        f"{dimensions[2]:.2f} m length)"
    )


if __name__ == "__main__":
    main()
