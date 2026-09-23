#!/usr/bin/env python3
"""AIR-014 — Airbus A220-300-class regional jet, lofted from its own dimensions.

Until now the A220 was `generate-air-adelaide-fleet.py` taking the 737-8 mesh and scaling it
on three axes, which gives the right bounding box and the wrong aeroplane. The A220's identity
is almost the opposite of a 737's: a slim five-abreast tube, an unusually high-aspect-ratio
wing with raked tips rather than winglets, and very large geared-turbofan nacelles that look
oversized for the airframe. None of that survives a scale factor.

This builds the type from its own tables:

  fuselage    3.50 m five-abreast tube with the long, sharply pointed and drooped A220 nose
  wing        35.10 m span, high aspect ratio, raked tips (no winglet fence)
  engines     PW1500G-class: 1.85 m fan in a short, fat, large-diameter cowl
  tail        conventional swept fin, tailplane low on the rear fuselage

Envelope is held to the published 38.70 x 35.10 x 11.50 m that the runtime visual profile,
the Hangar thumbnail and `scripts/test-air-adelaide-fleet.py` already expect. Main and nose
tyre radii match `AircraftVisualProfiles.AirbusA220300` so the wheel roll stays correct.

Run: python3 scripts/generate-air-014-a220-300.py
"""
import importlib.util
from pathlib import Path

import numpy as np

SCRIPTS = Path(__file__).resolve().parent
REPO = SCRIPTS.parent
AIRCRAFT = REPO / "game" / "Airside" / "Assets" / "Airside" / "Art" / "Models" / "Aircraft"
BASENAME = "mdl_a220_300_v01"


def _load(name, filename):
    spec = importlib.util.spec_from_file_location(name, SCRIPTS / filename)
    module = importlib.util.module_from_spec(spec)
    assert spec.loader is not None
    spec.loader.exec_module(module)
    return module


_auth = _load("airside_authored_fbx", "generate-authored-fbx-turboprop-terminal.py")
_v05 = _load("airside_turboprop_v05", "generate-air-001-v05.py")
skin = _load("airside_aircraft_skin", "aircraft_skin.py")

box = _auth.box
cylinder = _auth.cylinder
write_kit = _auth.write_kit
oval_lathe_fuselage = _v05.oval_lathe_fuselage
lofted_aerofoil = _v05.lofted_aerofoil
orient_outward = _v05._orient_outward

TARGET_LENGTH_M = 38.70
TARGET_SPAN_M = 35.10
TARGET_HEIGHT_M = 11.50
HALF_LENGTH = TARGET_LENGTH_M / 2.0
HALF_SPAN = TARGET_SPAN_M / 2.0
FUSE_DIAM = 3.50
FUSE_RX = FUSE_DIAM / 2.0
FUSE_SEGMENTS = 60

CABIN_CY = 3.34
MAIN_TIRE_R = 0.58       # AircraftVisualProfiles.AirbusA220300
NOSE_TIRE_R = 0.50

# Stations nose->tail, (z, rx, ry, cy). The A220 nose is long, finely pointed and clearly
# drooped — the single most recognisable thing about the type from the side, and the exact
# opposite of the 737's short blunt radome.
STATIONS = np.array(
    [
        (HALF_LENGTH - 0.10, 0.30, 0.27, CABIN_CY - 0.52),
        (18.95, 0.55, 0.50, CABIN_CY - 0.47),
        (18.50, 0.86, 0.79, CABIN_CY - 0.38),
        (17.95, 1.16, 1.07, CABIN_CY - 0.28),
        (17.20, 1.44, 1.36, CABIN_CY - 0.17),
        (16.30, 1.63, 1.58, CABIN_CY - 0.08),
        (15.20, 1.72, 1.70, CABIN_CY - 0.02),
        (13.40, 1.75, 1.76, CABIN_CY),
        (8.00, 1.75, 1.78, CABIN_CY),
        (0.00, 1.75, 1.78, CABIN_CY),
        (-7.00, 1.75, 1.78, CABIN_CY),
        (-11.20, 1.73, 1.75, CABIN_CY + 0.03),
        (-14.00, 1.58, 1.60, CABIN_CY + 0.18),
        (-16.10, 1.24, 1.26, CABIN_CY + 0.42),
        (-17.70, 0.80, 0.82, CABIN_CY + 0.68),
        (-18.75, 0.40, 0.41, CABIN_CY + 0.88),
        (-HALF_LENGTH + 0.10, 0.17, 0.17, CABIN_CY + 1.00),
    ],
    dtype=np.float32,
)

# (|x|, chord y, leading-edge z, chord, thickness). High aspect ratio: the tip chord is a much
# smaller fraction of the root chord than a 737's, which is what makes the A220 wing read long
# and slender.
WING_STATIONS = (
    (1.62, 2.44, 3.05, 6.90, 0.48),
    (4.90, 2.72, 1.70, 5.05, 0.34),
    (9.80, 3.14, -0.15, 3.30, 0.21),
    (14.40, 3.52, -1.75, 2.05, 0.12),
    (16.60, 3.72, -2.55, 1.35, 0.085),
)

_STATION_Z = STATIONS[::-1, 0]
_STATION_RX = STATIONS[::-1, 1]
_STATION_RY = STATIONS[::-1, 2]
_STATION_CY = STATIONS[::-1, 3]

WINDOW_PITCH = 0.787
WINDOW_ANGLE_DEG = 15.0
WINDOW_FIRST_Z, WINDOW_LAST_Z = 12.90, -10.80


def surface(z: float, theta: float, offset: float = 0.0) -> np.ndarray:
    rx = float(np.interp(z, _STATION_Z, _STATION_RX))
    ry = float(np.interp(z, _STATION_Z, _STATION_RY))
    cy = float(np.interp(z, _STATION_Z, _STATION_CY))
    return np.array(
        [(rx + offset) * np.cos(theta), cy + (ry + offset) * np.sin(theta), z],
        np.float32,
    )


def _skin(z: float, angle_deg: float, offset: float = 0.0) -> np.ndarray:
    return surface(z, np.deg2rad(angle_deg), offset)


def _belly_y(z: float) -> float:
    """Bottom of the fuselage at station z. Belly-mounted parts are placed from this rather
    than from a guessed height — the first pass hung the nose gear door and the taxi light
    about 45 cm inside the fuselage, which the floating-part audit caught."""
    return float(_skin(z, 270.0)[1])


def fuselage_body() -> tuple[np.ndarray, np.ndarray]:
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
        (0, float(STATIONS[0, 3]), float(zs[0]) + 0.10),
        (rings - 1, float(STATIONS[-1, 3]), float(zs[-1]) - 0.10),
    ):
        centre = len(verts)
        verts = np.vstack([verts, [0.0, tip_y, tip_z]]).astype(np.float32)
        for i in range(FUSE_SEGMENTS):
            edge = [ring * FUSE_SEGMENTS + i, ring * FUSE_SEGMENTS + (i + 1) % FUSE_SEGMENTS]
            if ring == rings - 1:
                edge.reverse()
            faces.extend([centre, *edge])
    return verts, np.asarray(faces, np.uint16)


def cabin_window(z: float, side: float) -> tuple[np.ndarray, np.ndarray]:
    """Two adjacent passenger windows from station `z`, nose side first."""
    angle = 180.0 - WINDOW_ANGLE_DEG if side < 0 else WINDOW_ANGLE_DEG
    return skin.merge_meshes(
        [skin.window(_skin, z - k * WINDOW_PITCH, angle, width=0.25, height=0.36) for k in range(2)]
    )


def wing_station(x_abs: float) -> tuple[float, float, float, float]:
    xs = [s[0] for s in WING_STATIONS]
    return (
        float(np.interp(x_abs, xs, [s[1] for s in WING_STATIONS])),
        float(np.interp(x_abs, xs, [s[2] for s in WING_STATIONS])),
        float(np.interp(x_abs, xs, [s[3] for s in WING_STATIONS])),
        float(np.interp(x_abs, xs, [s[4] for s in WING_STATIONS])),
    )


def wing_slab(side, x_in, x_out, *, from_te, to_te, thickness, surface_side="upper"):
    corners = []
    for x_abs in (x_in, x_out):
        y, z_le, chord, thick = wing_station(x_abs)
        z_te = z_le - chord
        corners.append((side * x_abs, y, z_te + chord * from_te, z_te + chord * to_te, thick))
    verts: list = []
    for x, y, z_a, z_b, thick in corners:
        offset = thick * (0.5 if surface_side == "upper" else -0.5)
        verts.extend([[x, y + offset, z_a], [x, y + offset, z_b]])
    top = np.asarray(verts, np.float32)
    bottom = top.copy()
    bottom[:, 1] -= thickness
    allv = np.vstack([top, bottom]).astype(np.float32)
    quads = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 2, 6, 4), (1, 5, 7, 3), (0, 4, 5, 1), (2, 3, 7, 6)]
    idx: list[int] = []
    for a, b, c, d in quads:
        idx.extend([a, b, c, a, c, d])
    return orient_outward(allv, np.asarray(idx, np.uint16))


def nacelle_pod(x: float) -> tuple[np.ndarray, np.ndarray]:
    """PW1500G-class cowl: short, fat and large-diameter — the A220's signature."""
    stations = [
        (4.62, 1.06, 1.02, 2.28),
        (4.35, 1.14, 1.10, 2.29),
        (3.70, 1.17, 1.13, 2.29),
        (2.85, 1.14, 1.09, 2.28),
        (1.85, 1.03, 0.97, 2.26),
        (1.00, 0.86, 0.80, 2.23),
        (0.25, 0.63, 0.59, 2.20),
        (-0.25, 0.40, 0.38, 2.17),
        (-0.58, 0.24, 0.23, 2.15),
    ]
    segs = 52
    rings = []
    for z, rx, ry, cy in stations:
        ring = []
        for i in range(segs):
            ang = 2.0 * np.pi * i / segs
            c, s = np.cos(ang), np.sin(ang)
            # A mild flat to the lower cowl: the A220's big fan sits close to the ground and
            # the cowl is scalloped for it, but nothing like the 737's pronounced chord.
            if s < 0.0:
                flat = abs(s) ** 0.82
                ring.append([x + rx * c * (1.0 + 0.03 * (1.0 - abs(s))), cy - ry * flat, z])
            else:
                ring.append([x + rx * c, cy + ry * s, z])
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
    tip = rings[-1].mean(axis=0).copy()
    tip[2] -= 0.05
    for i in range(segs):
        j = (i + 1) % segs
        base = len(verts)
        verts.extend([tip, rings[-1][j], rings[-1][i]])
        indices.extend([base, base + 1, base + 2])
    return np.asarray(verts, np.float32), np.asarray(indices, np.uint16)


def annulus(cx, cy, z_front, z_back, outer_radius, inner_radius, segments=52):
    verts: list[list[float]] = []
    indices: list[int] = []
    for z, r in ((z_front, outer_radius), (z_back, outer_radius),
                 (z_back, inner_radius), (z_front, inner_radius)):
        for i in range(segments):
            ang = 2.0 * np.pi * i / segments
            verts.append([cx + r * np.cos(ang), cy + r * np.sin(ang), z])
    for ring in range(4):
        nxt = (ring + 1) % 4
        for i in range(segments):
            j = (i + 1) % segments
            a = ring * segments + i
            b = ring * segments + j
            c = nxt * segments + j
            d = nxt * segments + i
            indices.extend([a, b, c, a, c, d])
    return orient_outward(np.asarray(verts, np.float32), np.asarray(indices, np.uint16))


def turbofan_blade(cx, cy, z, angle_deg):
    ang = np.deg2rad(angle_deg)
    root, tip = 0.26, 0.86
    twist = np.deg2rad(30.0)
    pts = []
    for radius, sweep in ((root, 0.0), (tip, twist)):
        for depth in (-0.038, 0.038):
            px = cx + radius * np.cos(ang)
            py = cy + radius * np.sin(ang)
            pts.append([px + depth * np.sin(ang + sweep),
                        py - depth * np.cos(ang + sweep),
                        z + depth * 0.7])
    verts = np.asarray(pts * 2, np.float32)
    verts[4:, 2] += 0.065
    quads = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 2, 6, 4), (1, 5, 7, 3), (0, 4, 5, 1), (2, 3, 7, 6)]
    idx: list[int] = []
    for a, b, c, d in quads:
        idx.extend([a, b, c, a, c, d])
    return orient_outward(verts, np.asarray(idx, np.uint16))


def wheel_set(meshes, prefix, x, z, radius, width):
    meshes[f"tire_{prefix}"] = cylinder(x, radius, z, radius, width, axis="x", segments=32)
    meshes[f"wheel_{prefix}"] = cylinder(x, radius, z, radius * 0.62, width + 0.030, axis="x", segments=28)
    meshes[f"rim_{prefix}"] = cylinder(x, radius, z, radius * 0.36, width + 0.050, axis="x", segments=24)


def a220_300_meshes() -> dict[str, tuple[np.ndarray, np.ndarray]]:
    meshes: dict[str, tuple[np.ndarray, np.ndarray]] = {}
    NOSE_Z = 13.70
    MAIN_Z = -1.30

    meshes["fuselage"] = fuselage_body()

    nose_z = [HALF_LENGTH - 0.10, 19.15, 18.95, 18.70, 18.35, 17.95]
    meshes["radome"] = oval_lathe_fuselage(
        [
            (z, float(np.interp(z, _STATION_Z, _STATION_RX)) + 0.007,
             float(np.interp(z, _STATION_Z, _STATION_RY)) + 0.007,
             float(np.interp(z, _STATION_Z, _STATION_CY)))
            for z in nose_z
        ],
        segments=60,
    )
    # The A220 flight deck is large and set well forward on a slim nose, which is part of why
    # the type looks long-nosed even though it is shorter than a 737-800.
    meshes["flightdeck_crown"] = skin.skin_patch(
        _skin, 17.35, 90.0, 0.64, 0.96, front=0.005, radius=0.28, rings=3, max_edge=0.15)
    meshes["windscreen_c"] = skin.skin_patch(
        _skin, 17.40, 90.0, 0.31, 0.33, front=0.011, radius=0.10, rings=2, max_edge=0.13)
    meshes["windscreen_l"] = skin.skin_patch(
        _skin, 17.33, 90.0 + 25.0, 0.31, 0.26, front=0.011, radius=0.09, rings=2, max_edge=0.13)
    meshes["windscreen_r"] = skin.skin_patch(
        _skin, 17.33, 90.0 - 25.0, 0.31, 0.26, front=0.011, radius=0.09, rings=2, max_edge=0.13)
    meshes["cockpit_side_l"] = skin.skin_patch(
        _skin, 16.72, 180.0 - 45.0, 0.44, 0.29, front=0.009, radius=0.10, rings=2)
    meshes["cockpit_side_r"] = skin.skin_patch(
        _skin, 16.72, 45.0, 0.44, 0.29, front=0.009, radius=0.10, rings=2)

    meshes["belly_fairing"] = oval_lathe_fuselage(
        [
            (-6.80, 0.20, 0.09, 2.10),
            (-4.40, 0.58, 0.28, 1.95),
            (-1.30, 0.82, 0.41, 1.87),
            (2.20, 0.82, 0.41, 1.87),
            (5.20, 0.60, 0.29, 1.95),
            (7.30, 0.22, 0.10, 2.10),
        ],
        segments=40,
    )

    # Operator sash follows the tube (ADR 0112) instead of a buried box.
    meshes["livery_stripe"] = skin.livery_ribbon(_skin, 12.6, -14.4, -1, half_width=0.26, rise_degrees=20.0)
    meshes["livery_stripe_lower"] = skin.livery_ribbon(_skin, 12.6, -14.4, 1, half_width=0.26, rise_degrees=20.0)
    meshes["livery_tail_sweep"] = lofted_aerofoil(
        [(5.40, 0.0, -13.40, 2.20, 0.075), (9.90, 0.0, -15.50, 1.00, 0.05)],
        chord_points=10,
        vertical=True,
    )

    door_outline, door_panel, door_handle = skin.door_set(_skin, 13.40, 180.0 - 17.0, 0.46, 0.86)
    meshes["door_outline_fwd"], meshes["door_fwd"], meshes["door_handle_fwd"] = (
        door_outline, door_panel, door_handle)
    cargo_outline, cargo_panel, cargo_latch = skin.door_set(_skin, 7.00, -29.0, 0.56, 0.48)
    meshes["cargo_door_outline"], meshes["cargo_door"], meshes["cargo_door_latch"] = (
        cargo_outline, cargo_panel, cargo_latch)
    meshes["door_service_aft"] = skin.skin_patch(
        _skin, -11.30, 19.0, 0.40, 0.74, front=0.004, radius=0.10, rings=2)

    meshes["antenna"] = box(0.0, 5.22, 7.00, 0.05, 0.46, 0.05)
    meshes["antenna_aft"] = box(0.0, 5.14, -6.10, 0.04, 0.33, 0.04)
    for name, angle in (("pitot", 187.0), ("pitot_b", -7.0)):
        base = _skin(17.55, angle, 0.0)
        meshes[name] = box(float(base[0]), float(base[1]), float(base[2]) + 0.13, 0.033, 0.033, 0.40)

    pair_pitch = 2.0 * WINDOW_PITCH
    for index, z in enumerate(np.arange(WINDOW_FIRST_Z, WINDOW_LAST_Z, -pair_pitch), start=1):
        meshes[f"cabin_window_{index}"] = cabin_window(float(z), -1.0)
        meshes[f"cabin_window_r{index}"] = cabin_window(float(z), 1.0)

    for side, suffix in ((-1.0, "left"), (1.0, "right")):
        wing_stations = [
            (side * x, y, z_le, chord, thickness)
            for x, y, z_le, chord, thickness in WING_STATIONS
        ]
        meshes[f"wing_{suffix}"] = lofted_aerofoil(wing_stations, chord_points=22)
        meshes[f"wing_root_{suffix}"] = wing_slab(
            side, 1.62, 3.20, from_te=0.04, to_te=0.90, thickness=0.24)
        meshes[f"wing_fairing_{suffix}"] = wing_slab(
            side, 1.66, 4.00, from_te=0.02, to_te=0.28, thickness=0.22, surface_side="lower")
        meshes[f"flap_{suffix}"] = wing_slab(
            side, 2.20, 11.20, from_te=0.00, to_te=0.34, thickness=0.075, surface_side="lower")
        meshes[f"spoiler_{suffix}"] = wing_slab(
            side, 3.90, 10.80, from_te=0.34, to_te=0.60, thickness=0.038, surface_side="upper")
        meshes[f"aileron_{suffix}"] = wing_slab(
            side, 11.70, 15.90, from_te=0.00, to_te=0.38, thickness=0.055)
        meshes[f"flap_track_{suffix[0]}1"] = wing_slab(
            side, 4.20, 4.44, from_te=-0.11, to_te=0.13, thickness=0.15, surface_side="lower")
        meshes[f"flap_track_{suffix[0]}2"] = wing_slab(
            side, 8.00, 8.24, from_te=-0.11, to_te=0.13, thickness=0.15, surface_side="lower")

    # Raked wingtip, not a winglet. The A220 sweeps the outer panel back and up into a
    # continuous rake — there is no vertical fence to see, which is the clearest way to tell
    # it apart from the 737 and the E-Jet at a distance. The tip owns the exact 35.10 m span.
    # The rake is in the wing plane, so thickness spreads in Y and this x is the exact tip.
    tip_x = HALF_SPAN
    rake_stations = (
        (16.60, 3.72, -2.55, 1.35, 0.085),
        (17.10, 3.90, -2.98, 1.02, 0.068),
        (tip_x, 4.12, -3.40, 0.68, 0.050),
    )
    for side, suffix in ((-1.0, "left"), (1.0, "right")):
        meshes[f"wingtip_{suffix}"] = lofted_aerofoil(
            [(side * x, y, z_le, chord, t) for x, y, z_le, chord, t in rake_stations],
            chord_points=14,
        )
    meshes["static_wick_left"] = box(-17.20, 4.02, -3.80, 0.045, 0.028, 0.26)
    meshes["static_wick_right"] = box(17.20, 4.02, -3.80, 0.045, 0.028, 0.26)

    # Geared turbofans: large diameter, short cowl, slung close under a high wing root.
    for side, suffix in ((-1.0, "left"), (1.0, "right")):
        x = side * 4.85
        meshes[f"engine_{suffix}"] = nacelle_pod(x)
        meshes[f"nacelle_{suffix}"] = annulus(x, 2.28, 4.74, 4.46, 1.14, 0.90, segments=52)
        meshes[f"intake_{suffix}"] = annulus(x, 2.28, 4.78, 4.68, 1.06, 0.94, segments=52)
        meshes[f"fan_{suffix}"] = cylinder(x, 2.28, 4.40, 0.88, 0.035, axis="z", segments=52)
        for bi, ang in enumerate(np.linspace(0, 360, 14, endpoint=False)):
            meshes[f"fan_blade_{suffix[0]}{bi+1}"] = turbofan_blade(x, 2.28, 4.46, float(ang))
        meshes[f"pylon_{suffix}"] = box(x, 2.94, 2.50, 0.34, 1.35, 2.40)
        meshes[f"exhaust_{suffix}"] = cylinder(x, 2.18, -0.30, 0.44, 0.52, axis="z", segments=40)
        meshes[f"exhaust_stack_{'l' if side < 0 else 'r'}"] = cylinder(
            x, 2.18, -0.68, 0.32, 0.32, axis="z", segments=28)

    # Conventional swept fin; tip owns the exact 11.50 m height.
    meshes["tail_fin"] = lofted_aerofoil(
        [
            (5.00, 0.0, -12.10, 5.60, 0.28),
            (7.30, 0.0, -13.45, 3.80, 0.20),
            (9.50, 0.0, -14.60, 2.55, 0.145),
            (11.50, 0.0, -15.60, 1.55, 0.10),
        ],
        chord_points=17,
        vertical=True,
    )
    meshes["tail_fin_tip"] = box(0.0, 11.38, -15.84, 0.11, 0.18, 0.76)
    meshes["rudder"] = lofted_aerofoil(
        [(5.60, 0.0, -16.95, 0.76, 0.07), (10.60, 0.0, -17.32, 0.48, 0.05)],
        chord_points=10,
        vertical=True,
    )
    meshes["dorsal_fin"] = lofted_aerofoil(
        [(4.80, 0.0, -10.30, 2.70, 0.16), (6.30, 0.0, -12.60, 1.30, 0.09)],
        chord_points=12,
        vertical=True,
    )
    meshes["tailplane"] = lofted_aerofoil(
        [
            (-6.10, 4.62, -15.10, 1.80, 0.11),
            (-2.10, 4.55, -13.95, 2.90, 0.15),
            (-0.28, 4.53, -13.65, 3.25, 0.16),
            (0.28, 4.53, -13.65, 3.25, 0.16),
            (2.10, 4.55, -13.95, 2.90, 0.15),
            (6.10, 4.62, -15.10, 1.80, 0.11),
        ],
        chord_points=15,
    )
    meshes["tailplane_tip_l"] = box(-6.05, 4.60, -15.72, 0.25, 0.10, 0.80)
    meshes["tailplane_tip_r"] = box(6.05, 4.60, -15.72, 0.25, 0.10, 0.80)
    meshes["elevator_left"] = box(-3.05, 4.53, -16.42, 5.60, 0.06, 0.62)
    meshes["elevator_right"] = box(3.05, 4.53, -16.42, 5.60, 0.06, 0.62)
    # At the tail-cone apex. At z=-19.20 the cone is a ~0.19 m ring and a light on the axis
    # floated inside it with no surface within tolerance.
    tail_apex_y = float(STATIONS[-1, 3])
    tail_apex_z = float(STATIONS[-1, 0]) - 0.10
    meshes["tail_nav_light"] = box(0.0, tail_apex_y, tail_apex_z + 0.05, 0.08, 0.08, 0.10)
    meshes["beacon_top"] = box(0.0, 11.40, -15.70, 0.09, 0.11, 0.09)

    MAIN_X = 2.48
    WING_UNDERSIDE = 2.56
    leg_top = WING_UNDERSIDE + 0.06
    leg_h = leg_top - 0.52
    leg_c = 0.52 + leg_h / 2.0
    meshes["gear_nose"] = skin.merge_meshes([
        box(0.0, 1.58, NOSE_Z, 0.17, 2.30, 0.36),
        box(0.0, 0.52, NOSE_Z, 0.66, 0.10, 0.10),
    ])
    meshes["gear_oleo_nose"] = cylinder(0.0, 1.38, NOSE_Z, 0.085, 1.95, axis="y", segments=24)
    meshes["gear_scissors_nose"] = box(0.0, 1.76, NOSE_Z - 0.19, 0.13, 0.52, 0.30)
    door_z = NOSE_Z + 0.80
    meshes["gear_door_nose"] = box(0.0, _belly_y(door_z) + 0.04, door_z, 0.82, 0.05, 1.05)
    for side, suffix in ((-1.0, "left"), (1.0, "right")):
        x = side * MAIN_X
        meshes[f"gear_{suffix}"] = skin.merge_meshes([
            box(x, leg_c, MAIN_Z, 0.19, leg_h, 0.42),
            box(x, 0.58, MAIN_Z, 0.82, 0.11, 0.11),
        ])
        oleo_h = max(0.40, leg_top - 0.28 - 0.58)
        meshes[f"gear_oleo_{suffix}"] = cylinder(
            x, 0.58 + oleo_h / 2.0, MAIN_Z, 0.095, oleo_h, axis="y", segments=24)
        meshes[f"gear_scissors_{suffix}"] = box(x, 1.68, MAIN_Z - 0.21, 0.13, 0.52, 0.32)
        meshes[f"gear_door_{suffix}"] = box(x + side * 0.115, 2.00, MAIN_Z, 0.045, 1.25, 1.10)

    # Nav lights ride the raked tip and landing lights the wing-root lower surface, both
    # interpolated from the planform. The first pass put the nav lights 32 cm above the wing
    # and the landing lights ahead of the leading edge, so all four floated.
    # Interpolated along the *rake* stations, not WING_STATIONS: the planform table stops at
    # x=16.60, so np.interp would clamp and place the light 27 cm below the raked tip.
    nav_x = 17.19
    _rx = [s[0] for s in rake_stations]
    nav_y = float(np.interp(nav_x, _rx, [s[1] for s in rake_stations]))
    nav_z = float(np.interp(nav_x, _rx, [s[2] for s in rake_stations])) - float(
        np.interp(nav_x, _rx, [s[3] for s in rake_stations])) * 0.5
    meshes["nav_light_left"] = box(-nav_x, nav_y, nav_z, 0.08, 0.08, 0.08)
    meshes["nav_light_right"] = box(nav_x, nav_y, nav_z, 0.08, 0.08, 0.08)

    ll_y, ll_z_le, _, ll_thick = wing_station(2.90)
    ll_y -= ll_thick * 0.5
    meshes["landing_light_l"] = box(-2.90, ll_y, ll_z_le - 0.32, 0.22, 0.14, 0.085)
    meshes["landing_light_r"] = box(2.90, ll_y, ll_z_le - 0.32, 0.22, 0.14, 0.085)
    taxi_z = NOSE_Z + 0.76
    meshes["taxi_light"] = box(0.0, _belly_y(taxi_z) + 0.05, taxi_z, 0.15, 0.11, 0.11)

    wheel_set(meshes, "nose_left", -0.28, NOSE_Z, NOSE_TIRE_R, 0.21)
    wheel_set(meshes, "nose_right", 0.28, NOSE_Z, NOSE_TIRE_R, 0.21)
    wheel_set(meshes, "left_inboard", -2.20, MAIN_Z, MAIN_TIRE_R, 0.24)
    wheel_set(meshes, "left_outboard", -2.76, MAIN_Z, MAIN_TIRE_R, 0.24)
    wheel_set(meshes, "right_inboard", 2.20, MAIN_Z, MAIN_TIRE_R, 0.24)
    wheel_set(meshes, "right_outboard", 2.76, MAIN_Z, MAIN_TIRE_R, 0.24)

    # Shift from the mid-fuselage origin used above to the nose-stop datum the gate routes
    # and AircraftVisualProfiles.AirbusA220300 (visual centre -19.35 m) both expect.
    nose_stop = np.array((0.0, 0.0, -HALF_LENGTH), dtype=np.float32)
    shifted = {name: (verts + nose_stop, indices) for name, (verts, indices) in meshes.items()}
    return {name: orient_outward(v, i) for name, (v, i) in shifted.items()}


def _bounds(meshes):
    allv = np.vstack([v for v, _ in meshes.values()])
    return allv.min(axis=0), allv.max(axis=0)


def validate_meshes(meshes):
    if not meshes:
        raise ValueError("AIR-014 emitted no meshes")
    for name, (verts, indices) in meshes.items():
        if len(verts) == 0 or len(indices) == 0 or len(indices) % 3 != 0:
            raise ValueError(f"{name}: empty or non-triangle mesh")
        if len(verts) > np.iinfo(np.uint16).max:
            raise ValueError(f"{name}: exceeds uint16 glTF index limit")
        if not np.isfinite(verts).all():
            raise ValueError(f"{name}: non-finite vertex")
        if int(np.max(indices)) >= len(verts):
            raise ValueError(f"{name}: index out of range")

    minimum, maximum = _bounds(meshes)
    span = float(maximum[0] - minimum[0])
    height = float(maximum[1] - minimum[1])
    length = float(maximum[2] - minimum[2])
    for label, got, want in (("span", span, TARGET_SPAN_M),
                             ("height", height, TARGET_HEIGHT_M),
                             ("length", length, TARGET_LENGTH_M)):
        if abs(got - want) > 0.02:
            raise ValueError(f"AIR-014 {label} is {got:.3f} m, expected {want:.2f} m")
    if abs(float(minimum[1])) > 0.02:
        raise ValueError(f"AIR-014 tyres must touch local y=0, got {minimum[1]:.4f}")
    if abs(float(maximum[2])) > 0.02:
        raise ValueError(f"AIR-014 nose-stop datum must be local z=0, got {maximum[2]:.4f}")
    if abs(float(minimum[2]) + TARGET_LENGTH_M) > 0.02:
        raise ValueError(f"AIR-014 tail must end at -{TARGET_LENGTH_M:.2f} m, got {minimum[2]:.4f}")
    fuse_verts, _ = meshes["fuselage"]
    fuse_rx = float(max(abs(fuse_verts[:, 0].min()), abs(fuse_verts[:, 0].max())))
    if abs(fuse_rx - FUSE_RX) > 0.05:
        raise ValueError(f"AIR-014 fuselage half-width must be ~{FUSE_RX:.2f} m, got {fuse_rx:.4f}")
    return span, height, length


def main() -> None:
    meshes = a220_300_meshes()
    span, height, length = validate_meshes(meshes)
    AIRCRAFT.mkdir(parents=True, exist_ok=True)
    write_kit(AIRCRAFT, BASENAME, meshes)
    triangles = sum(len(i) // 3 for _, i in meshes.values())
    print(
        f"AIR-014 ready: {BASENAME} ({len(meshes)} meshes, {triangles} triangles; "
        f"{span:.2f} m span × {height:.2f} m height × {length:.2f} m length)"
    )


if __name__ == "__main__":
    main()
