#!/usr/bin/env python3
"""Generate AIR-005: an original 737-8-class narrowbody aircraft kit.

The asset is deliberately an unbranded, stylised interpretation rather than a
copy of any airline's aircraft.  It is authored in metres with +Z forward and
Y-up, matching the project's aircraft kit convention.  Its local origin is the
nose-stop datum (nose at Z=0; tail behind it on negative Z), so a terminal-gate
route can position the root directly at its stop mark.  The visible envelope is
kept to the published 737-8 class dimensions so future terminal-gate routes can
use a credible code-C footprint:

* length: 39.47 m
* wingspan: 35.92 m
* height: 12.42 m

The project runtime reads its simple POSITION + uint16-index glTF kit directly;
the existing ``write_kit`` helper also exports an editable FBX companion and
keeps Unity ``.meta`` GUIDs stable on regeneration.
"""

from __future__ import annotations

import importlib.util
from pathlib import Path

import numpy as np


SCRIPTS = Path(__file__).resolve().parent
REPO = SCRIPTS.parent
AIRCRAFT = REPO / "game" / "Airside" / "Assets" / "Airside" / "Art" / "Models" / "Aircraft"
BASENAME = "mdl_737_8_narrowbody_v01"

# The aircraft kits share a deliberately small glTF/FBX export contract.  Reuse
# it instead of creating a second exporter whose binary layout ArtGltfLoader does
# not understand.
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

box = _auth.box
cylinder = _auth.cylinder
write_kit = _auth.write_kit
oval_lathe_fuselage = _v05.oval_lathe_fuselage
lofted_aerofoil = _v05.lofted_aerofoil
orient_outward = _v05._orient_outward


TARGET_LENGTH_M = 39.47
TARGET_SPAN_M = 35.92
TARGET_HEIGHT_M = 12.42
HALF_LENGTH = TARGET_LENGTH_M / 2.0
HALF_SPAN = TARGET_SPAN_M / 2.0


def _lerp(a: float, b: float, t: float) -> float:
    return a + (b - a) * t


# (absolute x, chord-line y, leading-edge z, chord, thickness).  The low wing,
# swept leading edge, tapered tip and modest dihedral are more important to the
# overview silhouette than hidden panel detail.
WING_STATIONS = (
    (1.45, 4.25, 5.30, 7.30, 0.46),
    (8.25, 4.73, 3.37, 4.80, 0.29),
    (16.62, 5.32, 1.00, 2.60, 0.14),
)


def wing_station(x_abs: float) -> tuple[float, float, float, float]:
    """Interpolate the main-wing planform at |x|."""
    x = min(max(abs(x_abs), WING_STATIONS[0][0]), WING_STATIONS[-1][0])
    for left, right in zip(WING_STATIONS, WING_STATIONS[1:]):
        if x <= right[0]:
            t = (x - left[0]) / (right[0] - left[0])
            return tuple(_lerp(left[i], right[i], t) for i in range(1, 5))  # type: ignore[return-value]
    return WING_STATIONS[-1][1:]


def _closed_prism(corners: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
    """Make a closed slab from four lower then four upper corner positions."""
    assert corners.shape == (8, 3)
    faces = (
        (0, 1, 2, 3),  # lower
        (4, 7, 6, 5),  # upper
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
    """Tapered control surface that follows the actual wing sweep/dihedral."""
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


def windshield_pane(
    cx: float,
    cy: float,
    cz: float,
    sx: float,
    sy: float,
    sz: float,
    pitch_deg: float,
) -> tuple[np.ndarray, np.ndarray]:
    """Thin cockpit pane pitched into the 737-class forward fuselage."""
    verts, indices = box(cx, cy, cz, sx, sy, sz)
    angle = np.deg2rad(pitch_deg)
    cosine, sine = float(np.cos(angle)), float(np.sin(angle))
    transformed = verts.copy()
    for i, vertex in enumerate(verts):
        y, z = vertex[1] - cy, vertex[2] - cz
        transformed[i, 1] = cy + y * cosine - z * sine
        transformed[i, 2] = cz + y * sine + z * cosine
    return transformed, indices


def chevron_finger(
    cx: float,
    cy: float,
    cz: float,
    angle_deg: float,
    *,
    radius: float,
) -> tuple[np.ndarray, np.ndarray]:
    """One small scallop at an exhaust lip; six read as a chevron nozzle."""
    angle = np.deg2rad(angle_deg)
    dx, dy = float(np.cos(angle)), float(np.sin(angle))
    # A short cuboid rotated around the exhaust centreline.  It deliberately
    # extends aft, making the serrated silhouette legible without texture maps.
    verts, indices = box(0.0, 0.0, -0.17, 0.12, 0.15, 0.38)
    out = verts.copy()
    for i, vertex in enumerate(verts):
        out[i, 0] = cx + dx * (radius + vertex[0])
        out[i, 1] = cy + dy * (radius + vertex[1])
        out[i, 2] = cz + vertex[2]
    return out, indices


def narrowbody_737_8_meshes() -> dict[str, tuple[np.ndarray, np.ndarray]]:
    """Original, unbranded 737-8-class mesh kit at true metric scale."""
    meshes: dict[str, tuple[np.ndarray, np.ndarray]] = {}

    # Fuselage: a 3.76 m-diameter, oval lathe with the nose/tail extremities
    # set exactly to the target length.  +Z is forward in the Airside scene.
    meshes["fuselage"] = oval_lathe_fuselage(
        [
            (HALF_LENGTH, 0.055, 0.055, 4.20),
            (19.48, 0.18, 0.20, 4.21),
            (19.18, 0.39, 0.42, 4.25),
            (18.65, 0.78, 0.84, 4.31),
            (17.92, 1.22, 1.30, 4.34),
            (16.92, 1.58, 1.64, 4.34),
            (15.60, 1.81, 1.84, 4.33),
            (13.20, 1.88, 1.88, 4.31),
            (9.00, 1.88, 1.88, 4.30),
            (4.20, 1.88, 1.88, 4.30),
            (-0.80, 1.88, 1.88, 4.30),
            (-5.70, 1.87, 1.86, 4.30),
            (-10.20, 1.82, 1.79, 4.28),
            (-13.60, 1.65, 1.62, 4.25),
            (-15.85, 1.36, 1.34, 4.20),
            (-17.55, 0.89, 0.92, 4.15),
            (-18.75, 0.43, 0.48, 4.11),
            (-19.42, 0.16, 0.20, 4.08),
            (-HALF_LENGTH, 0.055, 0.055, 4.06),
        ],
        segments=36,
    )
    meshes.update(
        {
            "radome": cylinder(0.0, 4.24, 19.14, 0.39, 0.92, axis="z", segments=24),
            "belly_fairing": cylinder(0.0, 2.60, 0.25, 0.92, 12.3, axis="z", segments=24),
            "cockpit": oval_lathe_fuselage(
                [
                    (18.95, 0.63, 0.46, 4.92),
                    (18.38, 1.12, 0.66, 4.98),
                    (17.56, 1.42, 0.62, 4.96),
                    (16.78, 1.60, 0.48, 4.83),
                ],
                segments=24,
            ),
            "cockpit_frame": box(0.0, 5.58, 18.07, 2.55, 0.08, 1.48),
            "cockpit_glare": box(0.0, 5.38, 18.57, 1.95, 0.18, 0.52),
            "windscreen_c": windshield_pane(0.0, 5.34, 18.47, 0.83, 0.74, 0.055, -29.0),
            "windscreen_l": windshield_pane(-0.72, 5.23, 18.30, 0.58, 0.68, 0.055, -24.0),
            "windscreen_r": windshield_pane(0.72, 5.23, 18.30, 0.58, 0.68, 0.055, -24.0),
            "cockpit_side_l": box(-1.76, 5.14, 17.82, 0.055, 0.62, 1.08),
            "cockpit_side_r": box(1.76, 5.14, 17.82, 0.055, 0.62, 1.08),
            "windscreen_pillar_l": box(-0.43, 5.36, 18.40, 0.055, 0.78, 0.32),
            "windscreen_pillar_r": box(0.43, 5.36, 18.40, 0.055, 0.78, 0.32),
            "windscreen_pillar_c": box(0.0, 5.39, 18.48, 0.045, 0.75, 0.24),
            # Fictional accent geometry only: it has no text, logo, registration
            # or real-airline colour scheme baked into the asset.
            "livery_stripe": box(0.0, 4.02, 2.20, 3.77, 0.12, 27.8),
            "livery_stripe_lower": box(0.0, 3.67, 2.30, 3.68, 0.06, 27.2),
            "livery_tail_sweep": box(0.0, 6.60, -14.10, 0.16, 4.10, 4.80),
            "door_outline_fwd": box(-1.865, 4.73, 14.72, 0.035, 1.96, 1.17),
            "door_fwd": box(-1.895, 4.73, 14.72, 0.045, 1.79, 1.00),
            "door_handle_fwd": box(-1.93, 4.76, 15.05, 0.035, 0.10, 0.14),
            "cargo_door_outline": box(1.865, 3.72, 7.48, 0.035, 1.17, 1.58),
            "cargo_door": box(1.895, 3.72, 7.48, 0.045, 1.02, 1.42),
            "cargo_door_latch": box(1.93, 3.75, 7.84, 0.035, 0.12, 0.12),
            "door_service_aft": box(1.89, 4.65, -12.05, 0.045, 1.66, 0.93),
            "antenna": box(0.0, 6.24, 8.25, 0.055, 0.54, 0.055),
            "antenna_aft": box(0.0, 6.15, -6.80, 0.045, 0.38, 0.045),
            "pitot": box(-1.72, 5.25, 19.16, 0.035, 0.035, 0.46),
            "pitot_b": box(1.72, 5.25, 19.16, 0.035, 0.035, 0.46),
        }
    )

    # Twenty-eight individual window openings per side give the long narrowbody
    # cabin its read at follow distance.  The loader's cabin_window_* prefix is
    # intentionally used so these receive the existing glazed material.
    window_zs = np.linspace(15.42, -10.96, 28)
    for index, z in enumerate(window_zs, start=1):
        meshes[f"cabin_window_{index}"] = box(-1.875, 5.00, float(z), 0.045, 0.33, 0.47)
        meshes[f"cabin_window_r{index}"] = box(1.875, 5.00, float(z), 0.045, 0.33, 0.47)

    # Main low wing and control surfaces.  These use lofted aerofoils rather
    # than plain boxes so the 35.92 m planform still reads as an aircraft wing
    # under the overview camera.
    for side, suffix in ((-1.0, "left"), (1.0, "right")):
        wing_stations = [
            (side * x, y, z_le, chord, thickness)
            for x, y, z_le, chord, thickness in WING_STATIONS
        ]
        meshes[f"wing_{suffix}"] = lofted_aerofoil(wing_stations, chord_points=18)
        meshes[f"wing_root_{suffix}"] = wing_slab(
            side, 1.45, 3.15, from_te=0.05, to_te=0.92, thickness=0.24
        )
        meshes[f"wing_fairing_{suffix}"] = wing_slab(
            side, 1.55, 4.00, from_te=0.02, to_te=0.26, thickness=0.22, surface="lower"
        )
        meshes[f"flap_{suffix}"] = wing_slab(
            side, 2.05, 12.70, from_te=0.00, to_te=0.34, thickness=0.075, surface="lower"
        )
        meshes[f"spoiler_{suffix}"] = wing_slab(
            side, 4.05, 12.40, from_te=0.34, to_te=0.60, thickness=0.042, surface="upper"
        )
        meshes[f"aileron_{suffix}"] = wing_slab(
            side, 12.80, 16.22, from_te=0.00, to_te=0.40, thickness=0.065
        )

    # The two pieces of each advanced-technology winglet create the distinctive
    # split/dual-feather silhouette without depending on a real manufacturer's
    # branded geometry.  The outer vertical surfaces set the exact 35.92 m span.
    meshes["winglet_left"] = lofted_aerofoil(
        [(5.32, -16.58, 0.77, 1.50, 0.13), (8.85, -17.895, -0.16, 0.76, 0.13)],
        chord_points=14,
        vertical=True,
    )
    meshes["winglet_right"] = lofted_aerofoil(
        [(5.32, 16.58, 0.77, 1.50, 0.13), (8.85, 17.895, -0.16, 0.76, 0.13)],
        chord_points=14,
        vertical=True,
    )
    meshes["wingtip_left"] = lofted_aerofoil(
        [(5.27, -16.56, 0.48, 1.32, 0.12), (3.72, -17.66, -0.02, 0.63, 0.10)],
        chord_points=12,
        vertical=True,
    )
    meshes["wingtip_right"] = lofted_aerofoil(
        [(5.27, 16.56, 0.48, 1.32, 0.12), (3.72, 17.66, -0.02, 0.63, 0.10)],
        chord_points=12,
        vertical=True,
    )
    meshes.update(
        {
            "flap_track_l1": wing_slab(-1.0, 4.55, 4.75, from_te=-0.13, to_te=0.13, thickness=0.18, surface="lower"),
            "flap_track_l2": wing_slab(-1.0, 8.35, 8.57, from_te=-0.13, to_te=0.13, thickness=0.18, surface="lower"),
            "flap_track_r1": wing_slab(1.0, 4.55, 4.75, from_te=-0.13, to_te=0.13, thickness=0.18, surface="lower"),
            "flap_track_r2": wing_slab(1.0, 8.35, 8.57, from_te=-0.13, to_te=0.13, thickness=0.18, surface="lower"),
            "flap_fairing_l": wing_slab(-1.0, 4.05, 10.90, from_te=-0.03, to_te=0.13, thickness=0.13, surface="lower"),
            "flap_fairing_r": wing_slab(1.0, 4.05, 10.90, from_te=-0.03, to_te=0.13, thickness=0.13, surface="lower"),
            "static_wick_left": box(-17.93, 8.86, -0.48, 0.06, 0.035, 0.31),
            "static_wick_right": box(17.93, 8.86, -0.48, 0.06, 0.035, 0.31),
        }
    )

    # High-bypass turbofans: large, low-slung, short nacelles are a key
    # 737-8-class cue.  Six small exhaust fingers form a readable chevron at
    # close follow range; no engine branding or texture is involved.
    for side, suffix in ((-1.0, "left"), (1.0, "right")):
        x = side * 5.24
        meshes[f"engine_{suffix}"] = cylinder(x, 3.15, 2.57, 1.06, 4.44, axis="z", segments=28)
        meshes[f"nacelle_{suffix}"] = cylinder(x, 3.08, 2.65, 0.96, 4.16, axis="z", segments=28)
        meshes[f"intake_{suffix}"] = cylinder(x, 3.15, 4.72, 0.93, 0.22, axis="z", segments=28)
        meshes[f"fan_{suffix}"] = cylinder(x, 3.15, 4.85, 0.72, 0.035, axis="z", segments=24)
        meshes[f"pylon_{suffix}"] = box(x, 4.16, 2.88, 0.42, 1.82, 1.86)
        meshes[f"exhaust_{suffix}"] = cylinder(x, 3.14, 0.55, 0.56, 0.58, axis="z", segments=22)
        meshes[f"exhaust_stack_{'l' if side < 0 else 'r'}"] = cylinder(x, 3.14, 0.25, 0.46, 0.25, axis="z", segments=20)
        for index, angle in enumerate(range(0, 360, 60), start=1):
            meshes[f"exhaust_chevron_{suffix}_{index}"] = chevron_finger(
                x, 3.14, 0.03, float(angle), radius=0.47
            )

    # Conventional swept tail, with its top deliberately defining the published
    # 12.42 m height.  It stays entirely inside the fuselage's length envelope.
    meshes.update(
        {
            "tail_fin": lofted_aerofoil(
                [(5.92, 0.0, -12.72, 6.34, 0.28), (12.42, 0.0, -15.48, 2.42, 0.14)],
                chord_points=18,
                vertical=True,
            ),
            "tail_fin_tip": box(0.0, 12.33, -16.05, 0.13, 0.18, 0.72),
            "rudder": box(0.0, 8.95, -17.78, 0.085, 5.50, 0.68),
            "dorsal_fin": box(0.0, 5.95, -12.28, 0.10, 1.45, 2.28),
            "tailplane": lofted_aerofoil(
                [
                    (-7.05, 7.25, -16.02, 2.25, 0.14),
                    (-0.35, 7.10, -14.32, 4.00, 0.18),
                    (0.35, 7.10, -14.32, 4.00, 0.18),
                    (7.05, 7.25, -16.02, 2.25, 0.14),
                ],
                chord_points=16,
            ),
            "tailplane_tip_l": box(-7.08, 7.12, -16.90, 0.22, 0.14, 0.95),
            "tailplane_tip_r": box(7.08, 7.12, -16.90, 0.22, 0.14, 0.95),
            "elevator_left": box(-3.58, 7.07, -17.50, 6.45, 0.075, 0.72),
            "elevator_right": box(3.58, 7.07, -17.50, 6.45, 0.075, 0.72),
            "tail_nav_light": box(0.0, 8.50, -19.37, 0.09, 0.09, 0.10),
            "beacon_top": box(0.0, 12.365, -16.05, 0.10, 0.11, 0.10),
        }
    )

    # Tricycle gear: wheelbase is 15.60 m and the main-strut centres are 5.72 m
    # apart, matching the 737-8 class published airport-planning envelope.  The
    # tyres touch y=0 so callers can place the kit directly on a ground plane.
    NOSE_Z = 13.35
    MAIN_Z = -2.25
    MAIN_X = 2.86
    meshes.update(
        {
            "gear_nose": box(0.0, 1.89, NOSE_Z, 0.19, 3.12, 0.40),
            "gear_oleo_nose": cylinder(0.0, 1.67, NOSE_Z, 0.10, 2.75, axis="y", segments=14),
            "gear_scissors_nose": box(0.0, 2.12, NOSE_Z - 0.21, 0.15, 0.62, 0.34),
            "gear_door_nose": box(0.0, 3.15, NOSE_Z, 0.92, 0.075, 1.22),
            "gear_left": box(-MAIN_X, 1.73, MAIN_Z, 0.22, 2.46, 0.48),
            "gear_right": box(MAIN_X, 1.73, MAIN_Z, 0.22, 2.46, 0.48),
            "gear_oleo_left": cylinder(-MAIN_X, 1.67, MAIN_Z, 0.11, 2.15, axis="y", segments=14),
            "gear_oleo_right": cylinder(MAIN_X, 1.67, MAIN_Z, 0.11, 2.15, axis="y", segments=14),
            "gear_scissors_left": box(-MAIN_X, 2.03, MAIN_Z - 0.25, 0.16, 0.64, 0.38),
            "gear_scissors_right": box(MAIN_X, 2.03, MAIN_Z - 0.25, 0.16, 0.64, 0.38),
            "gear_door_left": box(-MAIN_X, 3.00, MAIN_Z, 1.24, 0.075, 1.44),
            "gear_door_right": box(MAIN_X, 3.00, MAIN_Z, 1.24, 0.075, 1.44),
            "nav_light_left": box(-17.90, 8.53, -0.27, 0.09, 0.09, 0.09),
            "nav_light_right": box(17.90, 8.53, -0.27, 0.09, 0.09, 0.09),
            "landing_light_l": box(-5.24, 2.70, 4.79, 0.25, 0.16, 0.09),
            "landing_light_r": box(5.24, 2.70, 4.79, 0.25, 0.16, 0.09),
            "taxi_light": box(0.0, 2.71, 13.73, 0.18, 0.12, 0.13),
        }
    )

    def wheel_set(prefix: str, x: float, z: float, radius: float, width: float) -> None:
        meshes[f"tire_{prefix}"] = cylinder(x, radius, z, radius, width, axis="x", segments=20)
        meshes[f"wheel_{prefix}"] = cylinder(x, radius, z, radius * 0.63, width + 0.035, axis="x", segments=16)
        meshes[f"rim_{prefix}"] = cylinder(x, radius, z, radius * 0.38, width + 0.06, axis="x", segments=14)

    wheel_set("nose_left", -0.31, NOSE_Z, 0.55, 0.23)
    wheel_set("nose_right", 0.31, NOSE_Z, 0.55, 0.23)
    wheel_set("left_inboard", -2.55, MAIN_Z, 0.62, 0.25)
    wheel_set("left_outboard", -3.17, MAIN_Z, 0.62, 0.25)
    wheel_set("right_inboard", 2.55, MAIN_Z, 0.62, 0.25)
    wheel_set("right_outboard", 3.17, MAIN_Z, 0.62, 0.25)

    # The construction coordinates above are centred around the fuselage for
    # readability.  Gate routes, however, use an aircraft nose-stop datum.  Put
    # that datum at the root without disturbing any internal relationships.
    nose_stop_offset = np.array((0.0, 0.0, -HALF_LENGTH), dtype=np.float32)
    return {
        name: (verts + nose_stop_offset, indices)
        for name, (verts, indices) in meshes.items()
    }


def _bounds(meshes: dict[str, tuple[np.ndarray, np.ndarray]]) -> tuple[np.ndarray, np.ndarray]:
    vertices = np.concatenate([verts for verts, _ in meshes.values()], axis=0)
    return vertices.min(axis=0), vertices.max(axis=0)


def validate_meshes(meshes: dict[str, tuple[np.ndarray, np.ndarray]]) -> tuple[np.ndarray, np.ndarray]:
    """Fail early before emitting an invalid kit or a wildly scaled aircraft."""
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
    if not np.allclose(dimensions, expected, rtol=0.0, atol=0.015):
        raise ValueError(f"AIR-005 bounds {dimensions.tolist()} do not match {expected.tolist()}")
    if abs(float(minimum[1])) > 0.015:
        raise ValueError(f"AIR-005 tyres must touch local y=0, got {minimum[1]:.4f}")
    if abs(float(maximum[2])) > 0.015:
        raise ValueError(f"AIR-005 nose-stop datum must be local z=0, got {maximum[2]:.4f}")
    if abs(float(minimum[2]) + TARGET_LENGTH_M) > 0.015:
        raise ValueError(f"AIR-005 tail must end at -{TARGET_LENGTH_M:.2f} m, got {minimum[2]:.4f}")
    return minimum, maximum


def main() -> None:
    AIRCRAFT.mkdir(parents=True, exist_ok=True)
    meshes = narrowbody_737_8_meshes()
    minimum, maximum = validate_meshes(meshes)
    write_kit(AIRCRAFT, BASENAME, meshes)
    dimensions = maximum - minimum
    print(
        f"AIR-005 ready: {BASENAME} ({len(meshes)} meshes, "
        f"bounds {dimensions[0]:.2f} m span × {dimensions[1]:.2f} m height × "
        f"{dimensions[2]:.2f} m length)"
    )


if __name__ == "__main__":
    main()
