#!/usr/bin/env python3
"""Generate AIR-005: an original 737-8-class narrowbody kit.

Visual revision: smooth slender fuselage with fitted cabin glazing, flush
flight-deck panes, low swept wing with restrained split winglets, large forward-
hung turbofans with chevron nozzles, a tapered wing-body fairing and a joined
conventional tail. Project-owned, unbranded procedural geometry at the
official 39.47 × 35.92 × 12.42 m envelope.

Coordinates follow Airside aircraft convention: X is span, Y is up, +Z is
forward. The local origin is the nose-stop datum (nose at Z=0; tail on
negative Z) so a gate route can place the root on its stop. Tyres touch
local Y=0. The companion glTF stays inside the deliberately small POSITION +
uint16-index contract consumed by ArtGltfLoader.
"""

from __future__ import annotations

import importlib.util
from pathlib import Path

import numpy as np


SCRIPTS = Path(__file__).resolve().parent
REPO = SCRIPTS.parent
AIRCRAFT = REPO / "game" / "Airside" / "Assets" / "Airside" / "Art" / "Models" / "Aircraft"
BASENAME = "mdl_737_8_narrowbody_v01"

_AUTH_SPEC = importlib.util.spec_from_file_location(
    "airside_authored_fbx", SCRIPTS / "generate-authored-fbx-turboprop-terminal.py"
)
_auth = importlib.util.module_from_spec(_AUTH_SPEC)
assert _AUTH_SPEC.loader is not None
_AUTH_SPEC.loader.exec_module(_auth)

_V05_SPEC = importlib.util.spec_from_file_location(
    "airside_turboprop_v05", SCRIPTS / "generate-air-001-v05.py"
)
_v05 = importlib.util.module_from_spec(_V05_SPEC)
assert _V05_SPEC.loader is not None
_V05_SPEC.loader.exec_module(_v05)

_SKIN_SPEC = importlib.util.spec_from_file_location(
    "airside_aircraft_skin", SCRIPTS / "aircraft_skin.py"
)
skin = importlib.util.module_from_spec(_SKIN_SPEC)
assert _SKIN_SPEC.loader is not None
_SKIN_SPEC.loader.exec_module(skin)

box = _auth.box
cylinder = _auth.cylinder
write_kit = _auth.write_kit
oval_lathe_fuselage = _v05.oval_lathe_fuselage
lofted_aerofoil = _v05.lofted_aerofoil
windscreen_pane = _v05.windscreen_pane
orient_outward = _v05._orient_outward

TARGET_LENGTH_M = 39.47
TARGET_SPAN_M = 35.92
TARGET_HEIGHT_M = 12.42
HALF_LENGTH = TARGET_LENGTH_M / 2.0
HALF_SPAN = TARGET_SPAN_M / 2.0
FUSE_DIAM = 3.76
FUSE_RX = FUSE_DIAM / 2.0
FUSE_SEGMENTS = 64

# Stations in centred aircraft space (z=0 mid-fuselage). Soft 737-class nose,
# parallel cabin, and a tapered rear pressure body into the fin root.
# (z, rx, ry, cy)
STATIONS = np.array(
    [
        (HALF_LENGTH - 0.12, 0.30, 0.28, 4.14),
        (19.35, 0.62, 0.58, 4.16),
        (19.00, 0.98, 0.92, 4.20),
        (18.55, 1.32, 1.24, 4.25),
        (17.95, 1.60, 1.52, 4.29),
        (17.20, 1.78, 1.72, 4.31),
        (16.20, 1.86, 1.84, 4.31),
        (15.00, 1.82, 1.78, 4.32),
        (13.20, 1.88, 1.86, 4.31),
        (10.00, 1.88, 1.88, 4.30),
        (5.00, 1.88, 1.88, 4.30),
        (0.00, 1.88, 1.88, 4.30),
        (-5.00, 1.88, 1.88, 4.30),
        (-9.50, 1.87, 1.86, 4.30),
        (-13.20, 1.78, 1.74, 4.27),
        (-15.60, 1.55, 1.50, 4.22),
        (-17.40, 1.15, 1.12, 4.16),
        (-18.60, 0.70, 0.68, 4.10),
        (-19.25, 0.42, 0.40, 4.06),
        (-HALF_LENGTH + 0.12, 0.18, 0.16, 4.04),
    ],
    dtype=np.float32,
)

# Low swept wing: ( |x|, chord y, leading-edge z, chord, thickness )
WING_STATIONS = (
    (1.55, 4.20, 6.10, 7.60, 0.52),
    (5.20, 4.45, 4.85, 5.80, 0.38),
    (10.40, 4.85, 3.10, 3.85, 0.24),
    (15.40, 5.25, 1.45, 2.45, 0.14),
    (17.40, 5.40, 0.85, 1.85, 0.11),
)


# STATIONS are authored nose→tail (decreasing z). np.interp requires increasing xp.
_STATION_Z = STATIONS[::-1, 0]
_STATION_RX = STATIONS[::-1, 1]
_STATION_RY = STATIONS[::-1, 2]
_STATION_CY = STATIONS[::-1, 3]


def surface(z: float, theta: float, offset: float = 0.0) -> np.ndarray:
    rx = float(np.interp(z, _STATION_Z, _STATION_RX))
    ry = float(np.interp(z, _STATION_Z, _STATION_RY))
    cy = float(np.interp(z, _STATION_Z, _STATION_CY))
    return np.array(
        [(rx + offset) * np.cos(theta), cy + (ry + offset) * np.sin(theta), z],
        np.float32,
    )


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
        (0, float(STATIONS[0, 3]), float(zs[0]) + 0.12),
        (rings - 1, float(STATIONS[-1, 3]), float(zs[-1]) - 0.12),
    ):
        centre = len(verts)
        verts = np.vstack([verts, [0.0, tip_y, tip_z]]).astype(np.float32)
        for i in range(FUSE_SEGMENTS):
            edge = [ring * FUSE_SEGMENTS + i, ring * FUSE_SEGMENTS + (i + 1) % FUSE_SEGMENTS]
            if ring == rings - 1:
                edge.reverse()
            faces.extend([centre, *edge])
    return verts, np.asarray(faces, np.uint16)


def surface_quad(corners, offset=0.018) -> tuple[np.ndarray, np.ndarray]:
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
    front = front + normal * 0.006
    back = front - normal * 0.009
    verts = np.vstack([front, back]).astype(np.float32)
    faces: list[int] = []
    count = len(corners)
    for i in range(1, count - 1):
        faces.extend([0, i, i + 1, count, count + i + 1, count + i])
    for i in range(count):
        j = (i + 1) % count
        faces.extend([i, count + j, j, i, count + i, count + j])
    return verts, np.asarray(faces, np.uint16)


def _skin(z: float, angle_deg: float, offset: float = 0.0) -> np.ndarray:
    """Fuselage skin in the shared (z, degrees, offset) form used by aircraft_skin."""
    return surface(z, np.deg2rad(angle_deg), offset)


# Passenger windows: tall rounded panes at real 0.51 m frame pitch, centred ~0.5 m above the
# cabin axis. Two panes share one node so the part count (and draw calls) stay where they were.
WINDOW_PITCH = 0.508
WINDOW_ANGLE_DEG = 15.5
WINDOW_FIRST_Z, WINDOW_LAST_Z = 13.60, -11.10


def cabin_window(z: float, side: float) -> tuple[np.ndarray, np.ndarray]:
    """Two adjacent passenger windows starting at station `z` (nose side first)."""
    angle = 180.0 - WINDOW_ANGLE_DEG if side < 0 else WINDOW_ANGLE_DEG
    return skin.merge_meshes(
        [skin.window(_skin, z - k * WINDOW_PITCH, angle, width=0.25, height=0.36) for k in range(2)]
    )


def door_patch(z, half_z, half_arc, side, front=0.004, angle=None):
    """Curved door-sized shell that follows the skin (see aircraft_skin.py)."""
    a = angle if angle is not None else (180.0 if side < 0 else 0.0)
    return skin.skin_patch(_skin, z, a, half_z, half_arc, front=front, radius=0.12, rings=3, max_edge=0.14)


def entry_door(z, half_z, half_arc, side, angle=None):
    a = angle if angle is not None else (180.0 if side < 0 else 0.0)
    return skin.door_set(_skin, z, a, half_z, half_arc)


def _lerp(a: float, b: float, t: float) -> float:
    return a + (b - a) * t


def wing_station(x_abs: float) -> tuple[float, float, float, float]:
    x = min(max(abs(x_abs), WING_STATIONS[0][0]), WING_STATIONS[-1][0])
    for left, right in zip(WING_STATIONS, WING_STATIONS[1:]):
        if x <= right[0]:
            t = (x - left[0]) / (right[0] - left[0])
            return tuple(_lerp(left[i], right[i], t) for i in range(1, 5))  # type: ignore[return-value]
    return WING_STATIONS[-1][1:]


def _closed_prism(corners: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
    faces = (
        (0, 1, 2, 3),
        (4, 7, 6, 5),
        (0, 4, 5, 1),
        (1, 5, 6, 2),
        (2, 6, 7, 3),
        (3, 7, 4, 0),
    )
    verts: list[np.ndarray] = []
    indices: list[int] = []
    for a, b, c, d in faces:
        base = len(verts)
        verts.extend((corners[a], corners[b], corners[c], corners[d]))
        indices.extend((base, base + 1, base + 2, base, base + 2, base + 3))
    return orient_outward(
        np.asarray(verts, dtype=np.float32), np.asarray(indices, dtype=np.uint16)
    )


def wing_slab(
    side: float,
    x_in: float,
    x_out: float,
    *,
    from_te: float,
    to_te: float,
    thickness: float,
    surface: str = "chord",
) -> tuple[np.ndarray, np.ndarray]:
    stations = []
    for x_abs in (x_in, x_out):
        y, z_le, chord, wing_t = wing_station(x_abs)
        z_te = z_le - chord
        z_rear = z_te + from_te * chord
        z_front = z_te + to_te * chord
        if surface == "upper":
            y += wing_t * 0.52 + thickness * 0.5
        elif surface == "lower":
            y -= wing_t * 0.52 + thickness * 0.5
        stations.append((side * x_abs, y, z_rear, z_front))
    (x0, y0, z0_rear, z0_front), (x1, y1, z1_rear, z1_front) = stations
    h = thickness / 2.0
    return _closed_prism(
        np.asarray(
            (
                (x0, y0 - h, z0_rear),
                (x1, y1 - h, z1_rear),
                (x1, y1 - h, z1_front),
                (x0, y0 - h, z0_front),
                (x0, y0 + h, z0_rear),
                (x1, y1 + h, z1_rear),
                (x1, y1 + h, z1_front),
                (x0, y0 + h, z0_front),
            ),
            dtype=np.float32,
        )
    )


def nacelle_pod(x: float) -> tuple[np.ndarray, np.ndarray]:
    """Open-front high-bypass nacelle shell with a subtly flattened lower cowl."""
    # Stations: (z, rx, ry, cy) in centred aircraft space.
    stations = [
        (5.48, 1.02, 0.94, 3.13),
        (5.12, 1.08, 1.00, 3.15),
        (4.20, 1.10, 1.02, 3.15),
        (3.00, 1.07, 0.99, 3.14),
        (1.60, 0.97, 0.88, 3.12),
        (0.60, 0.79, 0.68, 3.10),
        (-0.20, 0.58, 0.52, 3.05),
        (-0.70, 0.38, 0.34, 3.02),
        (-1.05, 0.22, 0.20, 3.00),
    ]
    # Fidelity pass: engine nacelles are large, round and close to camera in every
    # follow/apron view, so raised from 40 to 56 (was the single biggest lever
    # among the round parts still on the runtime-smoothing fix's benefit curve).
    segs = 56
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
    # Keep the inlet physically open; only close the narrow tail where the exhaust
    # hardware takes over. The old front cap overlapped the intake and fan as three
    # coplanar discs, which made the engines flicker and read like solid triangles.
    for ring, z_sign in ((rings[-1], -1.0),):
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


def annulus(cx, cy, z_front, z_back, outer_radius, inner_radius, segments=56):
    """Closed intake-lip ring with an actual opening through its centre."""
    verts: list[list[float]] = []
    indices: list[int] = []
    for z in (z_front, z_back):
        for radius in (outer_radius, inner_radius):
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


def turbofan_blade(cx, cy, z, angle_deg):
    """One swept, tapered fan blade; thin in Z but not a billboard rectangle."""
    angle = np.deg2rad(angle_deg)
    corners = []
    for radius, offset in ((0.17, -8.0), (0.17, 8.0), (0.78, 16.0), (0.78, 5.0)):
        a = angle + np.deg2rad(offset)
        corners.append((cx + radius * np.cos(a), cy + radius * np.sin(a)))
    depth = 0.045
    points = np.asarray(
        [(x, y, z - depth) for x, y in corners] + [(x, y, z + depth) for x, y in corners],
        dtype=np.float32,
    )
    return _closed_prism(points)


def serrated_nozzle(cx, cy, z, *, radius, teeth=12):
    """Continuous chevron nozzle ring rather than floating box fingers."""
    verts: list[list[float]] = []
    indices: list[int] = []
    for i in range(teeth * 2):
        angle = 2.0 * np.pi * i / (teeth * 2)
        rear_z = z - (0.34 if i % 2 == 0 else 0.12)
        rear_radius = radius * (0.82 if i % 2 == 0 else 0.90)
        verts.append([cx + radius * np.cos(angle), cy + radius * np.sin(angle), z + 0.10])
        verts.append([cx + rear_radius * np.cos(angle), cy + rear_radius * np.sin(angle), rear_z])
    count = teeth * 2
    for i in range(count):
        j = (i + 1) % count
        a, b, c, d = 2*i, 2*j, 2*j+1, 2*i+1
        indices.extend((a, b, c, a, c, d))
    return orient_outward(np.asarray(verts, np.float32), np.asarray(indices, np.uint16))


def wheel_set(meshes, prefix, x, z, radius, width):
    # Segment counts are multiples of 4 so a vertex lands exactly on y=0.
    meshes[f"tire_{prefix}"] = cylinder(
        x, radius, z, radius, width, axis="x", segments=32
    )
    meshes[f"wheel_{prefix}"] = cylinder(
        x, radius, z, radius * 0.62, width + 0.030, axis="x", segments=28
    )
    meshes[f"rim_{prefix}"] = cylinder(
        x, radius, z, radius * 0.36, width + 0.050, axis="x", segments=24
    )


def narrowbody_737_8_meshes() -> dict[str, tuple[np.ndarray, np.ndarray]]:
    meshes: dict[str, tuple[np.ndarray, np.ndarray]] = {}
    NOSE_Z = 14.55
    MAIN_Z = -0.75

    meshes["fuselage"] = fuselage_body()

    # Nose furniture. Everything is sampled from the fuselage skin (aircraft_skin.py), so the
    # cap, brow panel, windscreens and cockpit side windows sit a few millimetres proud of
    # the nose and nothing overhangs it. The old hood was wider than the skin, read as a
    # visor lump and swallowed the cockpit side windows.
    nose_z = [19.615, 19.45, 19.30, 19.10, 18.85, 18.55]
    meshes["radome"] = oval_lathe_fuselage(
        [
            (z, float(np.interp(z, _STATION_Z, _STATION_RX)) + 0.008,
             float(np.interp(z, _STATION_Z, _STATION_RY)) + 0.008,
             float(np.interp(z, _STATION_Z, _STATION_CY)))
            for z in nose_z
        ],
        segments=64,
    )
    meshes["flightdeck_crown"] = skin.skin_patch(
        _skin, 18.20, 90.0, 0.62, 0.98, front=0.005, radius=0.30, rings=3, max_edge=0.16)
    meshes["windscreen_c"] = skin.skin_patch(
        _skin, 18.22, 90.0, 0.26, 0.36, front=0.012, radius=0.10, rings=2, max_edge=0.14)
    meshes["windscreen_l"] = skin.skin_patch(
        _skin, 18.16, 90.0 + 26.0, 0.26, 0.21, front=0.012, radius=0.09, rings=2, max_edge=0.14)
    meshes["windscreen_r"] = skin.skin_patch(
        _skin, 18.16, 90.0 - 26.0, 0.26, 0.21, front=0.012, radius=0.09, rings=2, max_edge=0.14)
    meshes["cockpit_side_l"] = skin.skin_patch(
        _skin, 17.35, 180.0 - 40.0, 0.40, 0.27, front=0.010, radius=0.10, rings=2)
    meshes["cockpit_side_r"] = skin.skin_patch(
        _skin, 17.35, 40.0, 0.40, 0.27, front=0.010, radius=0.10, rings=2)

    # A short, tapered keel follows the wing root.  The previous 21.5 m oval
    # showed as a flat, dark rectangular slab under the fuselage.
    meshes["belly_fairing"] = oval_lathe_fuselage(
        [
            (-5.30, 0.18, 0.08, 2.88),
            (-3.55, 0.46, 0.19, 2.78),
            (-0.60, 0.64, 0.27, 2.72),
            (2.30, 0.62, 0.25, 2.74),
            (4.85, 0.40, 0.17, 2.81),
            (6.70, 0.15, 0.06, 2.90),
        ],
        segments=40,
    )

    meshes["livery_stripe"] = box(0.0, 4.05, 1.50, 3.78, 0.11, 28.5)
    meshes["livery_stripe_lower"] = box(0.0, 3.70, 1.60, 3.70, 0.055, 27.8)
    meshes["livery_tail_sweep"] = lofted_aerofoil(
        [(6.20, 0.0, -13.20, 2.40, 0.08), (11.20, 0.0, -15.60, 1.10, 0.05)],
        chord_points=10,
        vertical=True,
    )

    # Doors follow the fuselage curve and sit a few millimetres proud, so they read as flush.
    door_outline, door_panel, door_handle = entry_door(14.55, 0.46, 0.92, -1.0)
    meshes["door_outline_fwd"], meshes["door_fwd"], meshes["door_handle_fwd"] = (
        door_outline, door_panel, door_handle)
    cargo_outline, cargo_panel, cargo_latch = entry_door(7.90, 0.62, 0.52, 1.0, angle=-27.0)
    meshes["cargo_door_outline"], meshes["cargo_door"], meshes["cargo_door_latch"] = (
        cargo_outline, cargo_panel, cargo_latch)
    meshes["door_service_aft"] = door_patch(-11.90, 0.40, 0.80, 1.0)

    meshes["antenna"] = box(0.0, 6.22, 8.00, 0.05, 0.50, 0.05)
    meshes["antenna_aft"] = box(0.0, 6.12, -6.50, 0.04, 0.35, 0.04)
    # Pitot probes stand off the nose skin just below the cockpit side windows.
    for name, angle in (("pitot", 186.0), ("pitot_b", -6.0)):
        base = _skin(18.30, angle, 0.0)
        meshes[name] = box(float(base[0]), float(base[1]), float(base[2]) + 0.14, 0.035, 0.035, 0.42)

    pair_pitch = 2.0 * WINDOW_PITCH
    for index, z in enumerate(np.arange(WINDOW_FIRST_Z, WINDOW_LAST_Z, -pair_pitch), start=1):
        meshes[f"cabin_window_{index}"] = cabin_window(float(z), -1.0)
        meshes[f"cabin_window_r{index}"] = cabin_window(float(z), 1.0)

    # Low swept wing + dual-feather tip treatment.
    for side, suffix in ((-1.0, "left"), (1.0, "right")):
        wing_stations = [
            (side * x, y, z_le, chord, thickness)
            for x, y, z_le, chord, thickness in WING_STATIONS
        ]
        meshes[f"wing_{suffix}"] = lofted_aerofoil(wing_stations, chord_points=22)
        meshes[f"wing_root_{suffix}"] = wing_slab(
            side, 1.55, 3.40, from_te=0.04, to_te=0.92, thickness=0.26
        )
        meshes[f"wing_fairing_{suffix}"] = wing_slab(
            side, 1.60, 4.20, from_te=0.02, to_te=0.28, thickness=0.24, surface="lower"
        )
        meshes[f"flap_{suffix}"] = wing_slab(
            side, 2.20, 12.40, from_te=0.00, to_te=0.36, thickness=0.08, surface="lower"
        )
        meshes[f"spoiler_{suffix}"] = wing_slab(
            side, 4.20, 12.00, from_te=0.36, to_te=0.62, thickness=0.04, surface="upper"
        )
        meshes[f"aileron_{suffix}"] = wing_slab(
            side, 12.50, 16.80, from_te=0.00, to_te=0.40, thickness=0.065
        )

    # Restrained split-scimitar tips.  They retain a clear upper/lower feather
    # read without becoming tall vertical plates at the overview camera.
    # Outer tip stations own the exact 35.92 m span. With vertical=True the
    # aerofoil thickness spreads in X around offset_c, so tip offset sits
    # slightly inboard of HALF_SPAN.
    tip_x = HALF_SPAN - 0.0275
    meshes["winglet_left"] = lofted_aerofoil(
        [
            (5.45, -17.10, 0.55, 1.38, 0.11),
            (6.45, -17.48, 0.12, 0.88, 0.075),
            (7.25, -tip_x, -0.20, 0.42, 0.055),
        ],
        chord_points=14,
        vertical=True,
    )
    meshes["winglet_right"] = lofted_aerofoil(
        [
            (5.45, 17.10, 0.55, 1.38, 0.11),
            (6.45, 17.48, 0.12, 0.88, 0.075),
            (7.25, tip_x, -0.20, 0.42, 0.055),
        ],
        chord_points=14,
        vertical=True,
    )
    meshes["wingtip_left"] = lofted_aerofoil(
        [
            (5.35, -17.05, 0.35, 1.12, 0.09),
            (4.40, -17.43, -0.05, 0.60, 0.065),
            (3.65, -(tip_x - 0.02), -0.24, 0.34, 0.050),
        ],
        chord_points=12,
        vertical=True,
    )
    meshes["wingtip_right"] = lofted_aerofoil(
        [
            (5.35, 17.05, 0.35, 1.12, 0.09),
            (4.40, 17.43, -0.05, 0.60, 0.065),
            (3.65, tip_x - 0.02, -0.24, 0.34, 0.050),
        ],
        chord_points=12,
        vertical=True,
    )
    meshes["flap_track_l1"] = wing_slab(
        -1.0, 4.50, 4.75, from_te=-0.12, to_te=0.14, thickness=0.16, surface="lower"
    )
    meshes["flap_track_l2"] = wing_slab(
        -1.0, 8.40, 8.65, from_te=-0.12, to_te=0.14, thickness=0.16, surface="lower"
    )
    meshes["flap_track_r1"] = wing_slab(
        1.0, 4.50, 4.75, from_te=-0.12, to_te=0.14, thickness=0.16, surface="lower"
    )
    meshes["flap_track_r2"] = wing_slab(
        1.0, 8.40, 8.65, from_te=-0.12, to_te=0.14, thickness=0.16, surface="lower"
    )
    meshes["flap_fairing_l"] = wing_slab(
        -1.0, 4.00, 10.80, from_te=-0.03, to_te=0.14, thickness=0.12, surface="lower"
    )
    meshes["flap_fairing_r"] = wing_slab(
        1.0, 4.00, 10.80, from_te=-0.03, to_te=0.14, thickness=0.12, surface="lower"
    )
    # Wicks trail off the winglet tips' trailing edges (tip at y=7.25, chord ends at z=-0.62).
    meshes["static_wick_left"] = box(-17.90, 7.10, -0.72, 0.05, 0.03, 0.28)
    meshes["static_wick_right"] = box(17.90, 7.10, -0.72, 0.05, 0.03, 0.28)

    # Large high-bypass turbofans hung well forward of the wing.
    for side, suffix in ((-1.0, "left"), (1.0, "right")):
        x = side * 5.35
        meshes[f"engine_{suffix}"] = nacelle_pod(x)
        meshes[f"nacelle_{suffix}"] = annulus(
            x, 3.13, 5.62, 5.30, 1.08, 0.82, segments=56
        )
        meshes[f"intake_{suffix}"] = annulus(
            x, 3.13, 5.66, 5.55, 1.00, 0.86, segments=56
        )
        meshes[f"fan_{suffix}"] = cylinder(
            x, 3.13, 5.23, 0.79, 0.035, axis="z", segments=56
        )
        # Proper swept fan blades instead of twelve unrotated rectangular bars.
        for bi, ang in enumerate(np.linspace(0, 360, 12, endpoint=False)):
            meshes[f"fan_blade_{suffix[0]}{bi+1}"] = turbofan_blade(
                x, 3.13, 5.29, float(ang)
            )
        meshes[f"pylon_{suffix}"] = box(x, 4.28, 3.20, 0.38, 1.85, 2.75)
        meshes[f"exhaust_{suffix}"] = cylinder(
            x, 3.02, 0.10, 0.48, 0.55, axis="z", segments=40
        )
        meshes[f"exhaust_stack_{'l' if side < 0 else 'r'}"] = cylinder(
            x, 3.02, -0.35, 0.36, 0.35, axis="z", segments=32
        )
        meshes[f"exhaust_chevron_{suffix}"] = serrated_nozzle(
            x, 3.02, -0.53, radius=0.42
        )

    # Conventional swept fin — tip owns the exact 12.42 m height.
    meshes["tail_fin"] = lofted_aerofoil(
        [
            (5.70, 0.0, -12.40, 6.60, 0.30),
            (8.40, 0.0, -14.00, 4.40, 0.22),
            (10.60, 0.0, -15.20, 3.00, 0.16),
            (12.42, 0.0, -16.10, 1.85, 0.11),
        ],
        chord_points=18,
        vertical=True,
    )
    meshes["tail_fin_tip"] = box(0.0, 12.30, -16.35, 0.12, 0.20, 0.85)
    meshes["rudder"] = lofted_aerofoil(
        [
            (6.40, 0.0, -17.55, 0.85, 0.08),
            (11.40, 0.0, -17.95, 0.55, 0.06),
        ],
        chord_points=10,
        vertical=True,
    )
    meshes["dorsal_fin"] = lofted_aerofoil(
        [
            (5.40, 0.0, -10.80, 2.60, 0.18),
            (7.20, 0.0, -13.20, 1.40, 0.10),
        ],
        chord_points=12,
        vertical=True,
    )
    meshes["tailplane"] = lofted_aerofoil(
        [
            (-7.20, 6.20, -15.80, 2.10, 0.13),
            (-2.40, 6.10, -14.40, 3.40, 0.18),
            (-0.30, 6.05, -14.00, 3.90, 0.19),
            (0.30, 6.05, -14.00, 3.90, 0.19),
            (2.40, 6.10, -14.40, 3.40, 0.18),
            (7.20, 6.20, -15.80, 2.10, 0.13),
        ],
        chord_points=16,
    )
    meshes["tailplane_tip_l"] = box(-7.15, 6.17, -16.55, 0.28, 0.12, 0.90)
    meshes["tailplane_tip_r"] = box(7.15, 6.17, -16.55, 0.28, 0.12, 0.90)
    meshes["elevator_left"] = box(-3.60, 6.05, -17.35, 6.60, 0.07, 0.70)
    meshes["elevator_right"] = box(3.60, 6.05, -17.35, 6.60, 0.07, 0.70)
    meshes["tail_nav_light"] = box(0.0, 4.05, -19.66, 0.09, 0.09, 0.10)
    meshes["beacon_top"] = box(0.0, 12.35, -16.20, 0.10, 0.12, 0.10)

    # Tricycle gear. Real 737-800 mains sit just behind the wing root and the nose gear ~5 m aft
    # of the nose; the old spots (13.20 / -2.40) left the main legs hanging 1 m under the wing
    # and 1.2 m behind its trailing edge. Wheelbase is now ~15.3 m (published 737-800: 15.6 m).
    MAIN_X = 2.86
    WING_UNDERSIDE = 4.05
    leg_top = WING_UNDERSIDE + 0.08
    leg_h = leg_top - 0.52
    leg_c = 0.52 + leg_h / 2.0
    meshes["gear_nose"] = skin.merge_meshes([
        box(0.0, 1.85, NOSE_Z, 0.18, 3.00, 0.38),
        box(0.0, 0.55, NOSE_Z, 0.72, 0.10, 0.10),          # axle joins the twin tyres
    ])
    meshes["gear_oleo_nose"] = cylinder(
        0.0, 1.60, NOSE_Z, 0.09, 2.55, axis="y", segments=24
    )
    meshes["gear_scissors_nose"] = box(0.0, 2.05, NOSE_Z - 0.20, 0.14, 0.58, 0.32)
    meshes["gear_door_nose"] = box(0.0, 2.50, NOSE_Z + 0.90, 0.88, 0.06, 1.15)   # hangs on the belly
    for side, suffix in ((-1.0, "left"), (1.0, "right")):
        x = side * MAIN_X
        meshes[f"gear_{suffix}"] = skin.merge_meshes([
            box(x, leg_c, MAIN_Z, 0.20, leg_h, 0.45),
            box(x, 0.62, MAIN_Z, 0.88, 0.12, 0.12),         # axle through both wheels
        ])
        oleo_h = leg_top - 0.30 - 0.60
        meshes[f"gear_oleo_{suffix}"] = cylinder(
            x, 0.60 + oleo_h / 2.0, MAIN_Z, 0.10, oleo_h, axis="y", segments=24
        )
        meshes[f"gear_scissors_{suffix}"] = box(x, 1.95, MAIN_Z - 0.22, 0.14, 0.58, 0.35)
        # Leg-mounted door plate (hangs beside the strut, touching it).
        meshes[f"gear_door_{suffix}"] = box(x + side * 0.125, 2.75, MAIN_Z, 0.05, 1.50, 1.20)
    meshes["nav_light_left"] = box(-17.89, 6.95, -0.30, 0.09, 0.09, 0.09)
    meshes["nav_light_right"] = box(17.89, 6.95, -0.30, 0.09, 0.09, 0.09)
    # Landing lights sit on the wing-root leading edge underside (they were buried in the nacelles).
    meshes["landing_light_l"] = box(-3.30, 4.15, 5.40, 0.24, 0.15, 0.09)
    meshes["landing_light_r"] = box(3.30, 4.15, 5.40, 0.24, 0.15, 0.09)
    meshes["taxi_light"] = box(0.0, 2.52, NOSE_Z + 0.85, 0.16, 0.12, 0.12)

    wheel_set(meshes, "nose_left", -0.30, NOSE_Z, 0.55, 0.22)
    wheel_set(meshes, "nose_right", 0.30, NOSE_Z, 0.55, 0.22)
    wheel_set(meshes, "left_inboard", -2.55, MAIN_Z, 0.62, 0.25)
    wheel_set(meshes, "left_outboard", -3.17, MAIN_Z, 0.62, 0.25)
    wheel_set(meshes, "right_inboard", 2.55, MAIN_Z, 0.62, 0.25)
    wheel_set(meshes, "right_outboard", 3.17, MAIN_Z, 0.62, 0.25)

    # Shift from mid-fuselage origin to the nose-stop datum used by gate routes.
    nose_stop = np.array((0.0, 0.0, -HALF_LENGTH), dtype=np.float32)
    shifted = {
        name: (verts + nose_stop, indices) for name, (verts, indices) in meshes.items()
    }
    return {name: orient_outward(v, i) for name, (v, i) in shifted.items()}


def _bounds(meshes):
    vertices = np.concatenate([verts for verts, _ in meshes.values()], axis=0)
    return vertices.min(axis=0), vertices.max(axis=0)


def validate_meshes(meshes):
    if not meshes:
        raise ValueError("AIR-005 emitted no meshes")
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
    dimensions = maximum - minimum
    expected = np.array((TARGET_SPAN_M, TARGET_HEIGHT_M, TARGET_LENGTH_M), dtype=np.float32)
    if not np.allclose(dimensions, expected, rtol=0.0, atol=0.02):
        raise ValueError(
            f"AIR-005 bounds {dimensions.tolist()} do not match {expected.tolist()}"
        )
    if abs(float(minimum[1])) > 0.02:
        raise ValueError(f"AIR-005 tyres must touch local y=0, got {minimum[1]:.4f}")
    if abs(float(maximum[2])) > 0.02:
        raise ValueError(f"AIR-005 nose-stop datum must be local z=0, got {maximum[2]:.4f}")
    if abs(float(minimum[2]) + TARGET_LENGTH_M) > 0.02:
        raise ValueError(
            f"AIR-005 tail must end at -{TARGET_LENGTH_M:.2f} m, got {minimum[2]:.4f}"
        )
    fuse_verts, _ = meshes["fuselage"]
    fuse_rx = float(max(abs(fuse_verts[:, 0].min()), abs(fuse_verts[:, 0].max())))
    if abs(fuse_rx - FUSE_RX) > 0.05:
        raise ValueError(
            f"AIR-005 fuselage half-width must be ~{FUSE_RX:.2f} m, got {fuse_rx:.4f}"
        )
    return minimum, maximum


def main() -> None:
    AIRCRAFT.mkdir(parents=True, exist_ok=True)
    meshes = narrowbody_737_8_meshes()
    minimum, maximum = validate_meshes(meshes)
    write_kit(AIRCRAFT, BASENAME, meshes)
    dimensions = maximum - minimum
    triangles = sum(len(indices) // 3 for _, indices in meshes.values())
    print(
        f"AIR-005 ready: {BASENAME} ({len(meshes)} meshes, {triangles} triangles; "
        f"{dimensions[0]:.2f} m span × {dimensions[1]:.2f} m height × "
        f"{dimensions[2]:.2f} m length)"
    )


if __name__ == "__main__":
    main()
