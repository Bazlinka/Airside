#!/usr/bin/env python3
"""AIR-013 — Embraer E190-class regional jet, lofted from its own dimensions.

Until now the E190 was `generate-air-adelaide-fleet.py` taking the 737-8 mesh and scaling it
on three axes. That gives the right bounding box and the wrong aeroplane: a 737 squashed to
28.72 m of span still has a 737's six-abreast cross-section, a 737's wing planform and a 737's
nacelle proportions. The E-Jet's whole identity is the opposite — a slim four-abreast tube,
a big wing-body fairing, comparatively small engines and a tall fin.

This builds the type from its own tables:

  fuselage    3.01 m double-bubble tube, 2+2 cabin, ogival nose
  wing        28.72 m span, modest sweep, mounted well aft with a deep root fairing
  engines     CF34-class: 1.16 m fan, slim nacelles on short pylons, no chevrons
  tips        small canted winglets (the E-Jet fence, not a 737 split scimitar)
  tail        tall swept fin with a long dorsal; tailplane low on the rear fuselage

Envelope is held to the published 36.24 x 28.72 x 10.55 m that the runtime visual profile,
the Hangar thumbnail and `scripts/test-air-adelaide-fleet.py` all already expect. Main and
nose tyre radii match `AircraftVisualProfiles.EmbraerE190` so the wheel roll stays correct.

Run: python3 scripts/generate-air-013-e190.py
"""
import importlib.util
from pathlib import Path

import numpy as np

SCRIPTS = Path(__file__).resolve().parent
REPO = SCRIPTS.parent
AIRCRAFT = REPO / "game" / "Airside" / "Assets" / "Airside" / "Art" / "Models" / "Aircraft"
BASENAME = "mdl_e190_v01"


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

TARGET_LENGTH_M = 36.24
TARGET_SPAN_M = 28.72
TARGET_HEIGHT_M = 10.55
HALF_LENGTH = TARGET_LENGTH_M / 2.0
HALF_SPAN = TARGET_SPAN_M / 2.0
FUSE_DIAM = 3.01
FUSE_RX = FUSE_DIAM / 2.0
FUSE_SEGMENTS = 56

CABIN_CY = 3.52          # cabin axis height above ground with the gear extended
MAIN_TIRE_R = 0.55       # AircraftVisualProfiles.EmbraerE190
NOSE_TIRE_R = 0.46

# Stations nose->tail in centred aircraft space, (z, rx, ry, cy).
# The E-Jet section is a shallow double bubble: slightly taller than wide through the cabin,
# which is what makes a four-abreast tube read as an E-Jet rather than a thin 737.
STATIONS = np.array(
    [
        (HALF_LENGTH - 0.10, 0.40, 0.36, CABIN_CY - 0.30),
        (17.72, 0.66, 0.60, CABIN_CY - 0.26),
        (17.30, 0.94, 0.86, CABIN_CY - 0.19),
        (16.80, 1.16, 1.09, CABIN_CY - 0.12),
        (16.10, 1.36, 1.31, CABIN_CY - 0.06),
        (15.20, 1.46, 1.45, CABIN_CY - 0.02),
        (14.10, 1.50, 1.52, CABIN_CY),
        (12.00, 1.505, 1.55, CABIN_CY),
        (6.00, 1.505, 1.55, CABIN_CY),
        (0.00, 1.505, 1.55, CABIN_CY),
        (-6.00, 1.505, 1.55, CABIN_CY),
        (-10.40, 1.50, 1.53, CABIN_CY + 0.02),
        (-13.10, 1.38, 1.40, CABIN_CY + 0.14),
        (-15.10, 1.10, 1.12, CABIN_CY + 0.34),
        (-16.60, 0.72, 0.74, CABIN_CY + 0.56),
        (-17.60, 0.38, 0.39, CABIN_CY + 0.74),
        (-HALF_LENGTH + 0.10, 0.16, 0.16, CABIN_CY + 0.86),
    ],
    dtype=np.float32,
)

# (|x|, chord y, leading-edge z, chord, thickness). Mounted aft of mid-fuselage with a deep
# root chord — the E-Jet carries a much larger wing-body fairing than a 737 for its size.
WING_STATIONS = (
    (1.42, 2.62, 2.35, 6.35, 0.46),
    (4.10, 2.86, 1.35, 4.95, 0.34),
    (8.20, 3.22, -0.05, 3.45, 0.22),
    (12.10, 3.54, -1.30, 2.35, 0.14),
    (13.90, 3.68, -1.86, 1.80, 0.105),
)

_STATION_Z = STATIONS[::-1, 0]
_STATION_RX = STATIONS[::-1, 1]
_STATION_RY = STATIONS[::-1, 2]
_STATION_CY = STATIONS[::-1, 3]

WINDOW_PITCH = 0.787          # E-Jet frame pitch is wider than the 737's 0.508
WINDOW_ANGLE_DEG = 16.0
WINDOW_FIRST_Z, WINDOW_LAST_Z = 12.10, -9.60


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
    """Two adjacent passenger windows from station `z`, nose side first.

    Two panes share one node so a full-length cabin does not cost a draw call per window —
    the same arrangement AIR-005 uses, and why the loop below steps by twice the pitch.
    """
    angle = 180.0 - WINDOW_ANGLE_DEG if side < 0 else WINDOW_ANGLE_DEG
    return skin.merge_meshes(
        [skin.window(_skin, z - k * WINDOW_PITCH, angle, width=0.23, height=0.33) for k in range(2)]
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
    """Flat panel following the wing planform between two fractions of chord from the TE."""
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
    """CF34-class nacelle: slim, long and round — not a 737's flattened high-bypass cowl."""
    stations = [
        (3.96, 0.72, 0.70, 2.30),
        (3.70, 0.77, 0.75, 2.31),
        (3.05, 0.79, 0.77, 2.31),
        (2.10, 0.77, 0.75, 2.30),
        (1.05, 0.70, 0.67, 2.28),
        (0.15, 0.58, 0.55, 2.25),
        (-0.55, 0.42, 0.40, 2.22),
        (-1.00, 0.26, 0.25, 2.20),
    ]
    segs = 48
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
    tip = rings[-1].mean(axis=0).copy()
    tip[2] -= 0.05
    for i in range(segs):
        j = (i + 1) % segs
        base = len(verts)
        verts.extend([tip, rings[-1][j], rings[-1][i]])
        indices.extend([base, base + 1, base + 2])
    return np.asarray(verts, np.float32), np.asarray(indices, np.uint16)


def annulus(cx, cy, z_front, z_back, outer_radius, inner_radius, segments=48):
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
    root, tip = 0.17, 0.56
    twist = np.deg2rad(26.0)
    pts = []
    for radius, sweep in ((root, 0.0), (tip, twist)):
        for depth in (-0.030, 0.030):
            px = cx + radius * np.cos(ang)
            py = cy + radius * np.sin(ang)
            pts.append([px + depth * np.sin(ang + sweep),
                        py - depth * np.cos(ang + sweep),
                        z + depth * 0.7])
    verts = np.asarray(pts * 2, np.float32)
    verts[4:, 2] += 0.055
    quads = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 2, 6, 4), (1, 5, 7, 3), (0, 4, 5, 1), (2, 3, 7, 6)]
    idx: list[int] = []
    for a, b, c, d in quads:
        idx.extend([a, b, c, a, c, d])
    return orient_outward(verts, np.asarray(idx, np.uint16))


def wheel_set(meshes, prefix, x, z, radius, width):
    meshes[f"tire_{prefix}"] = cylinder(x, radius, z, radius, width, axis="x", segments=32)
    meshes[f"wheel_{prefix}"] = cylinder(x, radius, z, radius * 0.62, width + 0.030, axis="x", segments=28)
    meshes[f"rim_{prefix}"] = cylinder(x, radius, z, radius * 0.36, width + 0.050, axis="x", segments=24)


def e190_meshes() -> dict[str, tuple[np.ndarray, np.ndarray]]:
    meshes: dict[str, tuple[np.ndarray, np.ndarray]] = {}
    NOSE_Z = 12.60
    MAIN_Z = -1.55

    meshes["fuselage"] = fuselage_body()

    nose_z = [HALF_LENGTH - 0.10, 17.90, 17.72, 17.50, 17.20, 16.80]
    meshes["radome"] = oval_lathe_fuselage(
        [
            (z, float(np.interp(z, _STATION_Z, _STATION_RX)) + 0.007,
             float(np.interp(z, _STATION_Z, _STATION_RY)) + 0.007,
             float(np.interp(z, _STATION_Z, _STATION_CY)))
            for z in nose_z
        ],
        segments=56,
    )
    meshes["flightdeck_crown"] = skin.skin_patch(
        _skin, 16.45, 90.0, 0.58, 0.86, front=0.005, radius=0.26, rings=3, max_edge=0.15)
    meshes["windscreen_c"] = skin.skin_patch(
        _skin, 16.50, 90.0, 0.27, 0.30, front=0.011, radius=0.09, rings=2, max_edge=0.13)
    meshes["windscreen_l"] = skin.skin_patch(
        _skin, 16.44, 90.0 + 25.0, 0.27, 0.23, front=0.011, radius=0.08, rings=2, max_edge=0.13)
    meshes["windscreen_r"] = skin.skin_patch(
        _skin, 16.44, 90.0 - 25.0, 0.27, 0.23, front=0.011, radius=0.08, rings=2, max_edge=0.13)
    meshes["cockpit_side_l"] = skin.skin_patch(
        _skin, 15.90, 180.0 - 46.0, 0.40, 0.26, front=0.009, radius=0.09, rings=2)
    meshes["cockpit_side_r"] = skin.skin_patch(
        _skin, 15.90, 46.0, 0.40, 0.26, front=0.009, radius=0.09, rings=2)

    # The deep E-Jet wing-body fairing: longer and fuller than a 737 keel for the size, and
    # the feature that most identifies the type in a side view.
    meshes["belly_fairing"] = oval_lathe_fuselage(
        [
            (-7.40, 0.20, 0.09, 2.18),
            (-5.10, 0.60, 0.30, 2.02),
            (-2.10, 0.86, 0.44, 1.93),
            (1.20, 0.88, 0.45, 1.93),
            (4.10, 0.66, 0.32, 2.02),
            (6.20, 0.24, 0.11, 2.18),
        ],
        segments=40,
    )

    meshes["livery_stripe"] = box(0.0, 3.30, 0.80, 3.03, 0.10, 25.0)
    meshes["livery_tail_sweep"] = lofted_aerofoil(
        [(5.10, 0.0, -12.60, 2.10, 0.07), (9.30, 0.0, -14.60, 0.95, 0.045)],
        chord_points=10,
        vertical=True,
    )

    door_outline, door_panel, door_handle = skin.door_set(_skin, 12.60, 180.0 - 18.0, 0.42, 0.80)
    meshes["door_outline_fwd"], meshes["door_fwd"], meshes["door_handle_fwd"] = (
        door_outline, door_panel, door_handle)
    cargo_outline, cargo_panel, cargo_latch = skin.door_set(_skin, 6.40, -30.0, 0.52, 0.44)
    meshes["cargo_door_outline"], meshes["cargo_door"], meshes["cargo_door_latch"] = (
        cargo_outline, cargo_panel, cargo_latch)
    meshes["door_service_aft"] = skin.skin_patch(
        _skin, -10.10, 20.0, 0.38, 0.70, front=0.004, radius=0.10, rings=2)

    meshes["antenna"] = box(0.0, 5.20, 6.40, 0.05, 0.44, 0.05)
    meshes["antenna_aft"] = box(0.0, 5.12, -5.60, 0.04, 0.32, 0.04)
    for name, angle in (("pitot", 188.0), ("pitot_b", -8.0)):
        base = _skin(16.60, angle, 0.0)
        meshes[name] = box(float(base[0]), float(base[1]), float(base[2]) + 0.12, 0.032, 0.032, 0.38)

    pair_pitch = 2.0 * WINDOW_PITCH
    for index, z in enumerate(np.arange(WINDOW_FIRST_Z, WINDOW_LAST_Z, -pair_pitch), start=1):
        meshes[f"cabin_window_{index}"] = cabin_window(float(z), -1.0)
        meshes[f"cabin_window_r{index}"] = cabin_window(float(z), 1.0)

    for side, suffix in ((-1.0, "left"), (1.0, "right")):
        wing_stations = [
            (side * x, y, z_le, chord, thickness)
            for x, y, z_le, chord, thickness in WING_STATIONS
        ]
        meshes[f"wing_{suffix}"] = lofted_aerofoil(wing_stations, chord_points=20)
        meshes[f"wing_root_{suffix}"] = wing_slab(
            side, 1.42, 2.90, from_te=0.04, to_te=0.90, thickness=0.22)
        meshes[f"wing_fairing_{suffix}"] = wing_slab(
            side, 1.45, 3.60, from_te=0.02, to_te=0.30, thickness=0.20, surface_side="lower")
        meshes[f"flap_{suffix}"] = wing_slab(
            side, 1.90, 9.60, from_te=0.00, to_te=0.34, thickness=0.07, surface_side="lower")
        meshes[f"spoiler_{suffix}"] = wing_slab(
            side, 3.40, 9.20, from_te=0.34, to_te=0.60, thickness=0.035, surface_side="upper")
        meshes[f"aileron_{suffix}"] = wing_slab(
            side, 10.00, 13.40, from_te=0.00, to_te=0.38, thickness=0.055)
        meshes[f"flap_track_{suffix[0]}1"] = wing_slab(
            side, 3.60, 3.82, from_te=-0.11, to_te=0.13, thickness=0.14, surface_side="lower")
        meshes[f"flap_track_{suffix[0]}2"] = wing_slab(
            side, 6.80, 7.02, from_te=-0.11, to_te=0.13, thickness=0.14, surface_side="lower")

    # Small canted E-Jet winglets: a single upward fence, not a split scimitar. The tip
    # station owns the exact 28.72 m span.
    tip_x = HALF_SPAN - 0.025
    winglet_stations = (
        (3.72, 13.86, -1.88, 1.72, 0.095),
        (4.55, 14.18, -2.12, 1.10, 0.070),
        (5.20, tip_x, -2.32, 0.62, 0.050),
    )
    for side, suffix in ((-1.0, "left"), (1.0, "right")):
        meshes[f"winglet_{suffix}"] = lofted_aerofoil(
            [(h, side * x, z_le, chord, t) for h, x, z_le, chord, t in winglet_stations],
            chord_points=12,
            vertical=True,
        )
    # Wicks trail off the winglet trailing edge. Positions are interpolated from the winglet
    # stations rather than guessed, so they cannot end up hanging in the air beside the tip —
    # which is exactly what the first pass did (audit-aircraft-geometry flagged both).
    wick_h = 5.10
    _wh = [s[0] for s in winglet_stations]
    wick_x = float(np.interp(wick_h, _wh, [s[1] for s in winglet_stations]))
    wick_te = float(np.interp(wick_h, _wh, [s[2] for s in winglet_stations])) - float(
        np.interp(wick_h, _wh, [s[3] for s in winglet_stations]))
    for side, suffix in ((-1.0, "left"), (1.0, "right")):
        meshes[f"static_wick_{suffix}"] = box(
            side * wick_x, wick_h, wick_te + 0.02, 0.045, 0.028, 0.25)

    # CF34-class engines: small relative to the airframe and hung close under the wing.
    for side, suffix in ((-1.0, "left"), (1.0, "right")):
        x = side * 4.05
        meshes[f"engine_{suffix}"] = nacelle_pod(x)
        meshes[f"nacelle_{suffix}"] = annulus(x, 2.30, 4.06, 3.82, 0.77, 0.60, segments=48)
        meshes[f"intake_{suffix}"] = annulus(x, 2.30, 4.09, 4.00, 0.71, 0.63, segments=48)
        meshes[f"fan_{suffix}"] = cylinder(x, 2.30, 3.78, 0.58, 0.030, axis="z", segments=48)
        for bi, ang in enumerate(np.linspace(0, 360, 12, endpoint=False)):
            meshes[f"fan_blade_{suffix[0]}{bi+1}"] = turbofan_blade(x, 2.30, 3.83, float(ang))
        meshes[f"pylon_{suffix}"] = box(x, 2.86, 2.20, 0.30, 1.30, 2.20)
        meshes[f"exhaust_{suffix}"] = cylinder(x, 2.22, -0.70, 0.35, 0.50, axis="z", segments=36)
        meshes[f"exhaust_stack_{'l' if side < 0 else 'r'}"] = cylinder(
            x, 2.22, -1.05, 0.26, 0.30, axis="z", segments=28)

    # Tall swept fin with the long dorsal that runs well forward on an E-Jet. Tip owns 10.55 m.
    meshes["tail_fin"] = lofted_aerofoil(
        [
            (4.60, 0.0, -11.40, 5.30, 0.26),
            (6.80, 0.0, -12.70, 3.55, 0.19),
            (8.80, 0.0, -13.80, 2.40, 0.14),
            (10.55, 0.0, -14.70, 1.45, 0.095),
        ],
        chord_points=16,
        vertical=True,
    )
    meshes["tail_fin_tip"] = box(0.0, 10.44, -14.92, 0.11, 0.17, 0.72)
    meshes["rudder"] = lofted_aerofoil(
        [(5.20, 0.0, -15.95, 0.72, 0.07), (9.70, 0.0, -16.28, 0.46, 0.05)],
        chord_points=10,
        vertical=True,
    )
    meshes["dorsal_fin"] = lofted_aerofoil(
        [(4.40, 0.0, -9.10, 3.20, 0.16), (5.90, 0.0, -11.90, 1.35, 0.09)],
        chord_points=12,
        vertical=True,
    )
    meshes["tailplane"] = lofted_aerofoil(
        [
            (-5.55, 4.76, -14.35, 1.70, 0.105),
            (-1.90, 4.70, -13.25, 2.70, 0.145),
            (-0.26, 4.68, -12.98, 3.05, 0.155),
            (0.26, 4.68, -12.98, 3.05, 0.155),
            (1.90, 4.70, -13.25, 2.70, 0.145),
            (5.55, 4.76, -14.35, 1.70, 0.105),
        ],
        chord_points=14,
    )
    meshes["tailplane_tip_l"] = box(-5.50, 4.74, -14.95, 0.24, 0.10, 0.76)
    meshes["tailplane_tip_r"] = box(5.50, 4.74, -14.95, 0.24, 0.10, 0.76)
    meshes["elevator_left"] = box(-2.80, 4.68, -15.60, 5.10, 0.06, 0.58)
    meshes["elevator_right"] = box(2.80, 4.68, -15.60, 5.10, 0.06, 0.58)
    meshes["tail_nav_light"] = box(0.0, 4.40, -18.06, 0.08, 0.08, 0.09)
    meshes["beacon_top"] = box(0.0, 10.46, -14.80, 0.09, 0.11, 0.09)

    # Tricycle gear. E190 wheelbase is about 14.1 m; mains retract into the wing-body fairing.
    MAIN_X = 2.32
    WING_UNDERSIDE = 2.70
    leg_top = WING_UNDERSIDE + 0.06
    leg_h = leg_top - 0.48
    leg_c = 0.48 + leg_h / 2.0
    meshes["gear_nose"] = skin.merge_meshes([
        box(0.0, 1.52, NOSE_Z, 0.16, 2.30, 0.34),
        box(0.0, 0.48, NOSE_Z, 0.62, 0.09, 0.09),
    ])
    meshes["gear_oleo_nose"] = cylinder(0.0, 1.32, NOSE_Z, 0.08, 1.95, axis="y", segments=24)
    meshes["gear_scissors_nose"] = box(0.0, 1.70, NOSE_Z - 0.18, 0.12, 0.50, 0.28)
    meshes["gear_door_nose"] = box(0.0, 2.02, NOSE_Z + 0.76, 0.78, 0.05, 1.00)
    for side, suffix in ((-1.0, "left"), (1.0, "right")):
        x = side * MAIN_X
        meshes[f"gear_{suffix}"] = skin.merge_meshes([
            box(x, leg_c, MAIN_Z, 0.18, leg_h, 0.40),
            box(x, 0.55, MAIN_Z, 0.78, 0.11, 0.11),
        ])
        oleo_h = max(0.40, leg_top - 0.28 - 0.55)
        meshes[f"gear_oleo_{suffix}"] = cylinder(
            x, 0.55 + oleo_h / 2.0, MAIN_Z, 0.09, oleo_h, axis="y", segments=24)
        meshes[f"gear_scissors_{suffix}"] = box(x, 1.60, MAIN_Z - 0.20, 0.12, 0.50, 0.30)
        meshes[f"gear_door_{suffix}"] = box(x + side * 0.11, 2.05, MAIN_Z, 0.045, 1.20, 1.05)

    # Nav lights sit on the winglet, landing lights on the wing-root lower surface just aft of
    # the leading edge. Both are placed from the station tables: the first pass put the landing
    # lights *ahead* of the leading edge, where they floated clear of the wing entirely.
    nav_h = 4.30
    nav_x = float(np.interp(nav_h, _wh, [s[1] for s in winglet_stations]))
    nav_z = float(np.interp(nav_h, _wh, [s[2] for s in winglet_stations])) - 0.20
    meshes["nav_light_left"] = box(-nav_x, nav_h, nav_z, 0.08, 0.08, 0.08)
    meshes["nav_light_right"] = box(nav_x, nav_h, nav_z, 0.08, 0.08, 0.08)

    ll_y, ll_z_le, _, ll_thick = wing_station(2.60)
    ll_y -= ll_thick * 0.5
    meshes["landing_light_l"] = box(-2.60, ll_y, ll_z_le - 0.30, 0.21, 0.13, 0.08)
    meshes["landing_light_r"] = box(2.60, ll_y, ll_z_le - 0.30, 0.21, 0.13, 0.08)
    meshes["taxi_light"] = box(0.0, 2.04, NOSE_Z + 0.72, 0.14, 0.11, 0.11)

    wheel_set(meshes, "nose_left", -0.26, NOSE_Z, NOSE_TIRE_R, 0.20)
    wheel_set(meshes, "nose_right", 0.26, NOSE_Z, NOSE_TIRE_R, 0.20)
    wheel_set(meshes, "left_inboard", -2.06, MAIN_Z, MAIN_TIRE_R, 0.23)
    wheel_set(meshes, "left_outboard", -2.58, MAIN_Z, MAIN_TIRE_R, 0.23)
    wheel_set(meshes, "right_inboard", 2.06, MAIN_Z, MAIN_TIRE_R, 0.23)
    wheel_set(meshes, "right_outboard", 2.58, MAIN_Z, MAIN_TIRE_R, 0.23)

    # Shift from the mid-fuselage origin used above to the nose-stop datum the gate routes
    # and AircraftVisualProfiles.EmbraerE190 (visual centre -18.12 m) both expect.
    nose_stop = np.array((0.0, 0.0, -HALF_LENGTH), dtype=np.float32)
    shifted = {name: (verts + nose_stop, indices) for name, (verts, indices) in meshes.items()}
    return {name: orient_outward(v, i) for name, (v, i) in shifted.items()}


def _bounds(meshes):
    allv = np.vstack([v for v, _ in meshes.values()])
    return allv.min(axis=0), allv.max(axis=0)


def validate_meshes(meshes):
    if not meshes:
        raise ValueError("AIR-013 emitted no meshes")
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
            raise ValueError(f"AIR-013 {label} is {got:.3f} m, expected {want:.2f} m")
    if abs(float(minimum[1])) > 0.02:
        raise ValueError(f"AIR-013 tyres must touch local y=0, got {minimum[1]:.4f}")
    if abs(float(maximum[2])) > 0.02:
        raise ValueError(f"AIR-013 nose-stop datum must be local z=0, got {maximum[2]:.4f}")
    if abs(float(minimum[2]) + TARGET_LENGTH_M) > 0.02:
        raise ValueError(f"AIR-013 tail must end at -{TARGET_LENGTH_M:.2f} m, got {minimum[2]:.4f}")
    fuse_verts, _ = meshes["fuselage"]
    fuse_rx = float(max(abs(fuse_verts[:, 0].min()), abs(fuse_verts[:, 0].max())))
    if abs(fuse_rx - FUSE_RX) > 0.05:
        raise ValueError(f"AIR-013 fuselage half-width must be ~{FUSE_RX:.2f} m, got {fuse_rx:.4f}")
    return span, height, length


def main() -> None:
    meshes = e190_meshes()
    span, height, length = validate_meshes(meshes)
    AIRCRAFT.mkdir(parents=True, exist_ok=True)
    write_kit(AIRCRAFT, BASENAME, meshes)
    triangles = sum(len(i) // 3 for _, i in meshes.values())
    print(
        f"AIR-013 ready: {BASENAME} ({len(meshes)} meshes, {triangles} triangles; "
        f"{span:.2f} m span × {height:.2f} m height × {length:.2f} m length)"
    )


if __name__ == "__main__":
    main()
