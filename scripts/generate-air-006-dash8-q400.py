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
        (13.50, 1.14, 1.06, 1.98),
        (14.10, 1.00, 0.92, 1.88),
        (14.55, 0.84, 0.76, 1.74),
        (14.95, 0.64, 0.58, 1.60),
        (15.30, 0.42, 0.38, 1.48),
        (15.55, 0.24, 0.22, 1.38),
        (15.75, 0.12, 0.11, 1.33),
        (HALF_LENGTH - 0.12, 0.05, 0.05, 1.30),
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


def _loft_rings(rings):
    """Connect successive closed rings into a watertight loft."""
    segs = len(rings[0])
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
        tip[2] += z_sign * 0.06
        for i in range(segs):
            j = (i + 1) % segs
            base = len(verts)
            if z_sign > 0:
                verts.extend([tip, ring[i], ring[j]])
            else:
                verts.extend([tip, ring[j], ring[i]])
            indices.extend([base, base + 1, base + 2])
    return np.asarray(verts, np.float32), np.asarray(indices, np.uint16)


def nacelle_pod(x):
    """Single aerodynamic nacelle: intake lip, core, gear-bay belly, exhaust.

    Stations follow the Dash 8-400 elongated nacelle that also houses the
    rearward main gear (Airport Planning / Aeroplane General references).
    """
    stations = [
        (5.62, 0.12, 0.10, 3.58),  # intake lip highlight
        (5.40, 0.38, 0.34, 3.56),
        (5.10, 0.56, 0.50, 3.54),
        (4.60, 0.68, 0.60, 3.52),
        (3.80, 0.76, 0.68, 3.50),
        (2.70, 0.82, 0.74, 3.48),
        (1.50, 0.84, 0.78, 3.46),
        (0.30, 0.84, 0.86, 3.38),
        (-0.80, 0.82, 0.98, 3.18),  # gear-bay deepen
        (-1.80, 0.78, 1.10, 2.96),
        (-2.70, 0.70, 1.14, 2.76),
        (-3.50, 0.56, 1.00, 2.56),
        (-4.15, 0.38, 0.68, 2.44),
        (-4.65, 0.22, 0.36, 2.38),
        (-5.10, 0.09, 0.12, 2.34),  # exhaust taper
    ]
    segs = 56
    rings = []
    for z, rx, ry, cy in stations:
        ring = []
        for i in range(segs):
            ang = 2.0 * np.pi * i / segs
            # Soft flat on the top where the wing / pylon lands.
            top_flat = 0.88 if np.sin(ang) > 0.50 else 1.0
            # Slight belly keel through the gear bay so doors read under the pod.
            belly = 1.08 if (z < -0.5 and np.sin(ang) < -0.35) else 1.0
            ring.append(
                [
                    x + rx * np.cos(ang),
                    cy + ry * top_flat * belly * np.sin(ang),
                    z,
                ]
            )
        rings.append(np.asarray(ring, np.float32))
    return _loft_rings(rings)


def nacelle_wing_fillet(x, side):
    """Blend the nacelle crown into the wing undersurface (one assembly read)."""
    z_stations = np.linspace(-1.80, 3.40, 14)
    segs = 20
    rings = []
    for z in z_stations:
        t = float(np.clip((z + 1.80) / 5.20, 0.0, 1.0))
        envelope = np.sin(np.pi * t) ** 0.70
        cx = x
        cy = 3.85 + 0.35 * envelope
        rx = 0.55 + 0.35 * envelope
        ry = 0.22 + 0.28 * envelope
        ring = []
        for i in range(segs):
            ang = 2.0 * np.pi * i / segs
            # Bias upward into the wing, keep a soft oval footprint.
            y_scale = 0.45 + 0.55 * max(0.0, np.sin(ang))
            ring.append(
                [
                    cx + rx * np.cos(ang),
                    cy + ry * y_scale * np.sin(ang),
                    float(z),
                ]
            )
        rings.append(np.asarray(ring, np.float32))
    return _loft_rings(rings)


def nacelle_gear_fairing(x):
    """Rounded rear nacelle belly enclosing the Q400 main-gear bay."""
    local = oval_lathe_fuselage(
        [
            (-3.50, 0.16, 0.18, 2.42),
            (-2.88, 0.46, 0.44, 2.38),
            (-2.15, 0.64, 0.56, 2.40),
            (-1.40, 0.60, 0.54, 2.48),
            (-0.72, 0.34, 0.32, 2.66),
        ],
        segments=28,
    )
    return translated(local, x, 0.0, 0.0)


def wing_fairing(side):
    """Half of a continuous high-wing / fuselage saddle.

    Real Dash 8 centre wing sits across the cabin crown as one piece. Each
    side mesh runs from past the centreline out to the wing root so the pair
    forms a continuous saddle with no centre valley or stepped root.
    """
    z_stations = np.linspace(-2.40, 3.55, 22)
    segs = 28
    rings = []
    for z in z_stations:
        t = float(np.clip((z + 2.40) / 5.95, 0.0, 1.0))
        # Peak under the spar, soft taper fore/aft (not a hard step).
        envelope = float(np.sin(np.pi * t) ** 0.70)
        if z > 2.10:
            envelope *= float(np.clip(1.0 - (z - 2.10) / 1.70, 0.05, 1.0))
        if z < -1.60:
            envelope *= float(np.clip(1.0 - (-1.60 - z) / 0.90, 0.08, 1.0))
        crown_y = float(surface(float(z), np.deg2rad(90.0), offset=0.0)[1])
        # Inner edge past centreline so left/right halves overlap into one roof.
        x_inner = side * (0.55)
        x_outer = side * (1.55 + 0.55 * envelope)
        y_inner = crown_y + 0.06 + 0.42 * envelope
        y_outer = 4.35 + 0.22 * envelope
        # Build a rounded half-ring: flat belly on the cabin, arched crown into
        # the wing root, closed at the centreline.
        ring = []
        for i in range(segs):
            u = i / float(segs)
            # Parametric half-oval from belly (u≈0/1) through outboard crown.
            ang = 2.0 * np.pi * u
            # Map angle into a spanwise blend: cos pushes outboard, sin lifts.
            span = 0.5 + 0.5 * np.cos(ang)  # 1 at centreline-ish, 0 at outboard
            # Remap so the mesh covers inner→outer continuously.
            s = 0.5 - 0.5 * np.cos(ang)  # 0..1..0 around; use abs of lateral
            # Prefer a simple elliptical section centred between inner and outer.
            cx = 0.5 * (x_inner + x_outer)
            rx = 0.5 * abs(x_outer - x_inner) + 0.08
            cy = 0.5 * (y_inner + y_outer)
            ry = 0.5 * abs(y_outer - y_inner) + 0.10 + 0.18 * envelope
            # Flatten the belly so it seats on the fuselage roof without a step.
            y_scale = 0.35 + 0.65 * max(0.0, np.sin(ang))
            # Soften the inboard side so the two halves meet as one surface.
            x = cx + rx * np.cos(ang)
            if side > 0:
                x = max(x, -0.05)
            else:
                x = min(x, 0.05)
            ring.append(
                [
                    float(x),
                    float(cy + ry * y_scale * np.sin(ang)),
                    float(z),
                ]
            )
        rings.append(np.asarray(ring, np.float32))
    return _loft_rings(rings)


def wing_centre_saddle():
    """Full-span centre wing box that kills the left/right valley at the crown."""
    z_stations = np.linspace(-2.20, 3.40, 20)
    segs = 32
    rings = []
    for z in z_stations:
        t = float(np.clip((z + 2.20) / 5.60, 0.0, 1.0))
        envelope = float(np.sin(np.pi * t) ** 0.65)
        if z > 2.00:
            envelope *= float(np.clip(1.0 - (z - 2.00) / 1.60, 0.06, 1.0))
        crown_y = float(surface(float(z), np.deg2rad(90.0), offset=0.0)[1])
        # Wide flattened oval sitting on the cabin roof — continuous across X=0.
        rx = 1.15 + 0.55 * envelope
        ry = 0.22 + 0.48 * envelope
        cy = crown_y + 0.16 + 0.30 * envelope
        ring = []
        for i in range(segs):
            ang = 2.0 * np.pi * i / segs
            # Squash the belly hard onto the crown; keep a soft arched roof.
            y_scale = 0.22 + 0.78 * max(0.0, np.sin(ang))
            # Mild leading/trailing taper already in envelope; keep sides round.
            ring.append(
                [
                    rx * np.cos(ang) * (0.92 + 0.08 * abs(np.sin(ang))),
                    cy + ry * y_scale * np.sin(ang),
                    float(z),
                ]
            )
        rings.append(np.asarray(ring, np.float32))
    return _loft_rings(rings)


def tail_root_fillet():
    """Smooth dorsal blend from rear fuselage into the fin leading edge."""
    z_stations = np.linspace(-7.80, -12.70, 16)
    segs = 20
    rings = []
    for z in z_stations:
        t = float((z + 7.80) / (-12.70 + 7.80))
        height = 0.18 + 2.85 * (t ** 0.85)
        half_w = 0.55 * (1.0 - 0.62 * t)
        cy = float(surface(float(z), np.deg2rad(90.0), offset=0.0)[1]) + 0.04
        ring = []
        for i in range(segs):
            ang = 2.0 * np.pi * i / segs
            # Flatten the underside so it sits on the fuselage crown.
            y = cy + height * max(0.0, np.sin(ang)) * 0.95
            x = half_w * np.cos(ang)
            ring.append([x, y, float(z)])
        rings.append(np.asarray(ring, np.float32))
    return _loft_rings(rings)


def tailplane_saddle_mesh():
    """Rounded T-tail bullet fairing (Dash 8 empennage junction)."""
    stations = [
        (-11.35, 0.62, 0.26, 7.48),
        (-11.85, 0.82, 0.38, 7.62),
        (-12.40, 0.88, 0.42, 7.74),
        (-12.95, 0.72, 0.34, 7.84),
        (-13.40, 0.42, 0.18, 7.90),
        (-13.75, 0.16, 0.08, 7.92),
    ]
    segs = 32
    rings = []
    for z, rx, ry, cy in stations:
        ring = []
        for i in range(segs):
            ang = 2.0 * np.pi * i / segs
            ring.append([rx * np.cos(ang), cy + ry * np.sin(ang), z])
        rings.append(np.asarray(ring, np.float32))
    return _loft_rings(rings)


def pitched_prop_blade(cx, cy, cz, angle_degrees, *, radial_start=0.20, radial_end=1.90, tip=False):
    """Dowty R408-class blade: broad chord, clear twist, solid root into the hub."""
    angle = np.deg2rad(angle_degrees)
    radial = np.array([-np.sin(angle), np.cos(angle), 0.0])
    tangent = np.array([np.cos(angle), np.sin(angle), 0.0])
    axis = np.array([0.0, 0.0, 1.0])
    if tip:
        stations = [
            (radial_start, 0.110, 11.0, 0.022),
            (0.5 * (radial_start + radial_end), 0.075, 8.0, 0.016),
            (radial_end, 0.040, 5.0, 0.012),
        ]
    else:
        # Scimitar planform + strong pitch so all six blades read at Hangar range.
        stations = [
            (radial_start, 0.145, 46.0, 0.055),
            (0.45, 0.255, 36.0, 0.048),
            (0.85, 0.220, 26.0, 0.038),
            (1.25, 0.155, 16.0, 0.028),
            (1.55, 0.100, 11.0, 0.020),
            (radial_end, 0.060, 7.0, 0.014),
        ]
    loops = []
    centre = np.array([cx, cy, cz], np.float64)
    for radius, half_chord, twist_degrees, half_thickness in stations:
        twist = np.deg2rad(twist_degrees)
        chord_axis = tangent * np.cos(twist) + axis * np.sin(twist)
        thickness_axis = -tangent * np.sin(twist) + axis * np.cos(twist)
        # Slight forward rake toward the tip (scimitar).
        rake = 0.04 * max(0.0, radius - radial_start)
        station_centre = centre + radial * radius + axis * rake
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
            a = loop[following] if reverse else loop[corner]
            b = loop[corner] if reverse else loop[following]
            vertices.extend([centre_point, a, b])
            indices.extend([base, base + 1, base + 2])
    return np.asarray(vertices, np.float32), np.asarray(indices, np.uint16)


def q400_meshes():
    meshes = {}

    meshes["fuselage"] = fuselage_body()

    wing_root = (1.55, 4.52, 3.25, 4.35, 0.40)
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

    # Continuous centre wing box across the cabin crown — kills the left/right
    # valley so the high-wing root reads as one fuselage saddle.
    meshes["wing_centre_saddle"] = wing_centre_saddle()

    for side, name in ((-1.0, "left"), (1.0, "right")):
        x = side * 4.35
        # One continuous nacelle assembly — intake lip and exhaust are part of
        # the loft; keep only a short wing pylon and a recessed cooler scoop.
        meshes[f"engine_{name}"] = nacelle_pod(x)
        meshes[f"nacelle_fillet_{name}"] = nacelle_wing_fillet(x, side)
        meshes[f"intake_{name}"] = cylinder(
            x, 3.56, 5.42, 0.36, 0.14, axis="z", segments=32
        )
        meshes[f"exhaust_{name}"] = cylinder(
            x, 2.42, -4.85, 0.14, 0.55, axis="z", segments=20
        )
        meshes[f"exhaust_stack_{name[0]}"] = box(
            x + side * 0.42, 3.55, -2.10, 0.14, 0.16, 0.85
        )
        meshes[f"pylon_{name}"] = box(x, 4.05, 0.35, 0.38, 0.72, 2.40)
        meshes[f"oil_cooler_{name[0]}"] = box(x, 2.88, 1.85, 0.48, 0.16, 0.70)
        meshes[f"cowl_flap_{name[0]}"] = box(x, 3.05, 0.85, 0.55, 0.06, 0.42)

        # Six pitched blades with clear tips, rooted into a credible hub/spinner.
        for index, suffix in enumerate(("", "_b", "_c", "_d", "_e", "_f")):
            angle = index * 60.0
            meshes[f"propeller_{name}{suffix}"] = pitched_prop_blade(
                x, 3.56, 5.62, angle, radial_start=0.22, radial_end=1.78
            )
            meshes[f"propeller_{name}_tip{suffix}"] = pitched_prop_blade(
                x,
                3.56,
                5.62,
                angle,
                radial_start=1.72,
                radial_end=PROP_RADIUS,
                tip=True,
            )
        meshes[f"prop_hub_{name}"] = cylinder(
            x, 3.56, 5.48, 0.36, 0.42, axis="z", segments=28
        )
        meshes[f"spinner_{name}"] = translated(
            oval_lathe_fuselage(
                [
                    (5.28, 0.34, 0.34, 3.56),
                    (5.55, 0.40, 0.40, 3.56),
                    (5.88, 0.28, 0.28, 3.56),
                    (6.12, 0.14, 0.14, 3.56),
                    (6.30, 0.03, 0.03, 3.56),
                ],
                segments=32,
            ),
            x,
            0.0,
            0.0,
        )
        meshes[f"spinner_stripe_{name[0]}"] = cylinder(
            x, 3.56, 5.68, 0.38, 0.05, axis="z", segments=24
        )

    # Fin tip owns the exact 8.34 m height. Soft dorsal + root fillet clean the
    # fin-to-fuselage join; rounded saddle cleans the T-tail junction.
    meshes["tail_fin"] = lofted_aerofoil(
        [
            (3.20, 0.0, -9.40, 5.20, 0.30),
            (5.20, 0.0, -11.20, 3.50, 0.20),
            (7.05, 0.0, -12.80, 2.30, 0.14),
            (8.34, 0.0, -14.05, 1.40, 0.09),
        ],
        chord_points=20,
        vertical=True,
    )
    meshes["dorsal_fin"] = lofted_aerofoil(
        [
            (2.95, 0.0, -7.40, 2.60, 0.18),
            (4.55, 0.0, -10.10, 1.50, 0.11),
        ],
        chord_points=14,
        vertical=True,
    )
    meshes["tail_root_fairing"] = tail_root_fillet()
    meshes["rudder"] = lofted_aerofoil(
        [
            (4.00, 0.0, -14.25, 0.70, 0.07),
            (7.75, 0.0, -14.80, 0.52, 0.05),
        ],
        chord_points=12,
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
        chord_points=16,
    )
    meshes["tailplane_saddle"] = tailplane_saddle_mesh()
    meshes["elevator_left"] = panel(
        -1.0, 0.40, 6.40, 7.88, 7.93, -14.35, -14.65, 0.55, 0.38, 0.06
    )
    meshes["elevator_right"] = panel(
        1.0, 0.40, 6.40, 7.88, 7.93, -14.35, -14.65, 0.55, 0.38, 0.06
    )
    meshes["tailplane_tip_l"] = box(-6.70, 7.95, -13.55, 0.35, 0.08, 0.55)
    meshes["tailplane_tip_r"] = box(6.70, 7.95, -13.55, 0.35, 0.08, 0.55)

    # Even cabin pitch; panes sit just outside the curved skin.
    for index, z in enumerate(np.linspace(10.20, -8.40, 18), start=1):
        meshes[f"cabin_window_{index}"] = cabin_window(float(z), -1.0)
        meshes[f"cabin_window_r{index}"] = cabin_window(float(z), 1.0)

    # Four discrete panes fitted to the nose curvature, separated by skin-coloured
    # pillars / sill / brow so the flight deck reads as framed glass — not a mask.
    meshes["windscreen_l"] = surface_quad(
        [(13.85, 114), (13.90, 134), (14.75, 130), (14.90, 112)], 0.014
    )
    meshes["windscreen_r"] = surface_quad(
        [(13.85, 66), (13.90, 46), (14.90, 68), (14.75, 50)], 0.014
    )
    meshes["cockpit_side_l"] = surface_quad(
        [(13.05, 132), (13.10, 148), (13.85, 144), (14.05, 128)], 0.014
    )
    meshes["cockpit_side_r"] = surface_quad(
        [(13.05, 48), (13.10, 32), (14.05, 52), (13.85, 36)], 0.014
    )
    meshes["windscreen_pillar_l"] = surface_quad(
        [(13.70, 132), (13.80, 142), (14.80, 138), (14.70, 128)], 0.034
    )
    meshes["windscreen_pillar_r"] = surface_quad(
        [(13.70, 48), (13.80, 38), (14.80, 42), (14.70, 52)], 0.034
    )
    meshes["windscreen_pillar_c"] = surface_quad(
        [(14.05, 98), (14.05, 82), (14.95, 84), (14.95, 96)], 0.032
    )
    # Slim brow + sill hug the crown / belt — frame the panes without a dark slab.
    meshes["cockpit_glare"] = surface_quad(
        [(13.55, 84), (13.55, 96), (14.55, 94), (14.55, 86)], 0.036
    )
    meshes["cockpit_sill"] = surface_quad(
        [(13.55, 108), (13.55, 72), (14.70, 74), (14.70, 106)], 0.028
    )

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
    # Soft radome continues the softened fuselage stations into the nose tip.
    meshes["radome"] = oval_lathe_fuselage(
        [
            (14.10, 0.98, 0.88, 1.86),
            (14.60, 0.78, 0.70, 1.70),
            (15.05, 0.52, 0.46, 1.54),
            (15.40, 0.28, 0.24, 1.40),
            (15.70, 0.12, 0.10, 1.33),
            (HALF_LENGTH - 0.12, 0.04, 0.04, 1.30),
        ],
        segments=40,
    )

    # Long nacelle-mounted mains — thicker oleos and open bay doors that stay
    # readable at Hangar / follow distance.
    for side, name in ((-1.0, "left"), (1.0, "right")):
        x = side * 4.35
        meshes[f"gear_{name}"] = box(x, 1.35, -1.90, 0.24, 2.35, 0.38)
        meshes[f"gear_oleo_{name}"] = cylinder(
            x, 1.15, -1.90, 0.11, 1.75, axis="y", segments=18
        )
        meshes[f"gear_scissors_{name}"] = box(
            x + side * 0.16, 1.45, -1.50, 0.09, 0.70, 0.45
        )
        # Open main-gear doors hang below the nacelle so the bay reads clearly.
        meshes[f"gear_door_{name}"] = box(
            x + side * 0.72, 1.82, -1.90, 0.10, 1.88, 1.50
        )
        meshes[f"gear_door_inner_{name[0]}"] = box(
            x - side * 0.48, 2.00, -1.90, 0.08, 1.42, 1.22
        )
        meshes[f"gear_fairing_{name}"] = nacelle_gear_fairing(x)
        wheel_set(meshes, f"{name}_forward", x, -1.40, 0.50, 0.34)
        wheel_set(meshes, f"{name}_aft", x, -2.40, 0.50, 0.34)

    meshes["gear_nose"] = box(0.0, 0.95, 12.15, 0.18, 1.30, 0.26)
    meshes["gear_oleo_nose"] = cylinder(
        0.0, 0.68, 12.15, 0.07, 1.00, axis="y", segments=16
    )
    meshes["gear_scissors_nose"] = box(0.0, 0.98, 12.40, 0.07, 0.52, 0.34)
    meshes["gear_door_nose"] = box(0.0, 1.15, 11.85, 0.78, 0.08, 1.45)
    meshes["gear_door_nose_l"] = box(-0.42, 1.35, 11.90, 0.10, 0.85, 1.20)
    meshes["gear_door_nose_r"] = box(0.42, 1.35, 11.90, 0.10, 0.85, 1.20)
    wheel_set(meshes, "nose_left", -0.22, 12.18, 0.34, 0.18)
    wheel_set(meshes, "nose_right", 0.22, 12.18, 0.34, 0.18)

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
