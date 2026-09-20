#!/usr/bin/env python3
"""Generate AIR-007: an original Saab 340B-class regional turboprop kit.

Project-owned, unbranded procedural geometry at the official Saab 340B /
EASA TCDS EASA.A.068 envelope (19.73 m length, 21.44 m standard span — not the
22.75 m extended-tip option — and 6.97 m height). Silhouette cues that separate
it from the high-wing ATR and long high-wing Q400:

* compact low wing with engines in under-wing nacelles
* relatively narrow circular fuselage
* conventional empennage (fin + low horizontal tail — not a T-tail)
* Dowty four-blade propellers (EASA TCDS: 4 blades, 3.35 m diameter)
* retractable tricycle gear with mains retracting into the nacelles

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
BASENAME = "mdl_saab_340b_v01"

_SPEC = importlib.util.spec_from_file_location(
    "airside_air001_v06", SCRIPTS / "generate-air-001-v06.py"
)
_v06 = importlib.util.module_from_spec(_SPEC)
assert _SPEC.loader is not None
_SPEC.loader.exec_module(_v06)
_v05 = _v06._v05

_SKIN_SPEC = importlib.util.spec_from_file_location(
    "airside_aircraft_skin", SCRIPTS / "aircraft_skin.py"
)
skin = importlib.util.module_from_spec(_SKIN_SPEC)
assert _SKIN_SPEC.loader is not None
_SKIN_SPEC.loader.exec_module(skin)

box = _v05.box
cylinder = _v05.cylinder
oval_lathe_fuselage = _v05.oval_lathe_fuselage
lofted_aerofoil = _v05.lofted_aerofoil
write_kit = _v05.write_kit
outward_winding = _v06.outward_winding
propeller_blade = _v06.propeller_blade

# Official Saab 340B / EASA.A.068 envelope (standard wing, not extended tips).
TARGET_LENGTH_M = 19.73
TARGET_SPAN_M = 21.44
TARGET_HEIGHT_M = 6.97
HALF_LENGTH = TARGET_LENGTH_M / 2.0
HALF_SPAN = TARGET_SPAN_M / 2.0
PROP_RADIUS = 3.35 / 2.0  # Dowty R.354/4… family, 132 in / 3.35 m

# Tail-to-nose stations for the narrow circular body.  The same profile feeds
# the skin and the small fitted flight-deck panes so glazing follows the nose
# rather than sitting on it as a rectangular mask.
FUSE_STATIONS = np.array(
    [
        (-HALF_LENGTH + 0.12, 0.04, 0.04, 1.78),
        (-9.35, 0.18, 0.16, 1.80),
        (-8.70, 0.48, 0.44, 1.82),
        (-7.80, 0.82, 0.78, 1.88),
        (-6.40, 1.08, 1.05, 1.95),
        (-4.20, 1.14, 1.12, 2.00),
        (3.80, 1.14, 1.12, 2.00),
        (5.60, 1.10, 1.08, 1.96),
        (6.90, 0.98, 0.94, 1.88),
        (7.80, 0.78, 0.72, 1.76),
        (8.55, 0.52, 0.46, 1.64),
        (9.15, 0.26, 0.22, 1.52),
        (HALF_LENGTH - 0.12, 0.05, 0.04, 1.46),
    ],
    dtype=np.float32,
)


def fuselage_surface(z: float, angle_degrees: float, offset: float = 0.0) -> np.ndarray:
    rx = float(np.interp(z, FUSE_STATIONS[:, 0], FUSE_STATIONS[:, 1]))
    ry = float(np.interp(z, FUSE_STATIONS[:, 0], FUSE_STATIONS[:, 2]))
    cy = float(np.interp(z, FUSE_STATIONS[:, 0], FUSE_STATIONS[:, 3]))
    angle = np.deg2rad(angle_degrees)
    return np.array(
        [(rx + offset) * np.cos(angle), cy + (ry + offset) * np.sin(angle), z],
        dtype=np.float32,
    )


def fitted_panel(corners, offset: float = 0.014):
    """A small glazed quad fitted to the curved fuselage, with a real thin edge."""
    sampled = np.asarray(
        [fuselage_surface(z, angle, offset) for z, angle in corners], dtype=np.float32
    )
    normal = np.cross(sampled[1] - sampled[0], sampled[2] - sampled[0])
    normal /= np.linalg.norm(normal)
    outward = sampled.mean(axis=0).copy()
    outward[2] = 0.0
    outward[1] -= 1.75
    if np.dot(normal, outward) < 0.0:
        normal = -normal
    front = sampled + normal * 0.004
    back = sampled - normal * 0.008
    vertices = np.vstack((front, back)).astype(np.float32)
    indices = np.asarray(
        [0, 1, 2, 0, 2, 3, 4, 6, 5, 4, 7, 6,
         0, 4, 5, 0, 5, 1, 1, 5, 6, 1, 6, 2,
         2, 6, 7, 2, 7, 3, 3, 7, 4, 3, 4, 0],
        dtype=np.uint16,
    )
    return vertices, indices


def translated(mesh, x, y, z):
    vertices, indices = mesh
    moved = vertices.copy()
    moved += np.array((x, y, z), np.float32)
    return moved, indices.copy()


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
        chord_points=10,
    )


def wheel_set(meshes, prefix, x, z, radius, width):
    # cy = radius puts the tyre contact patch on y=0 when the axis is X.
    meshes[f"tire_{prefix}"] = cylinder(
        x, radius, z, radius, width, axis="x", segments=28
    )
    meshes[f"wheel_{prefix}"] = cylinder(
        x, radius, z, radius * 0.62, width + 0.025, axis="x", segments=24
    )
    meshes[f"rim_{prefix}"] = cylinder(
        x, radius, z, radius * 0.34, width + 0.045, axis="x", segments=20
    )


def saab_meshes():
    meshes = {}

    # Narrow circular fuselage. The lathe helper adds a 0.12 m tip beyond each
    # end station, so stations are inset by 0.12 m to hit exact length.
    meshes["fuselage"] = oval_lathe_fuselage(FUSE_STATIONS.tolist(), segments=48)

    # Low wing — the Saab's defining contrast with ATR / Q400 high wings.
    # Outer tip stations own the exact 21.44 m standard span.
    wing_root = (0.95, 1.92, 1.55, 2.85, 0.32)
    wing_mid = (4.80, 2.05, 1.15, 1.95, 0.20)
    wing_tip = (HALF_SPAN, 2.18, 0.72, 1.05, 0.09)
    for side, name in ((-1.0, "left"), (1.0, "right")):
        meshes[f"wing_{name}"] = lofted_aerofoil(
            [
                (side * x, y, z, chord, thick)
                for x, y, z, chord, thick in (wing_root, wing_mid, wing_tip)
            ],
            chord_points=18,
        )
        meshes[f"flap_{name}"] = panel(
            side, 1.10, 5.40, 1.85, 1.98, -0.55, -0.20, 0.95, 0.62, 0.09
        )
        meshes[f"aileron_{name}"] = panel(
            side, 5.70, 10.20, 2.05, 2.15, -0.05, 0.25, 0.55, 0.28, 0.06
        )
        meshes[f"wing_fairing_{name}"] = box(
            side * 0.85, 1.72, 0.55, 0.55, 0.38, 2.40
        )

    # Under-wing nacelles house the CT7-class engines and the main gear bays.
    for side, name in ((-1.0, "left"), (1.0, "right")):
        x = side * 3.55
        meshes[f"engine_{name}"] = translated(
            oval_lathe_fuselage(
                [
                    (-2.55, 0.16, 0.14, 1.55),
                    (-2.05, 0.42, 0.38, 1.58),
                    (-0.80, 0.58, 0.55, 1.62),
                    (0.90, 0.62, 0.60, 1.68),
                    (2.10, 0.55, 0.52, 1.72),
                    (2.85, 0.28, 0.26, 1.74),
                ],
                segments=40,
            ),
            x,
            0.0,
            0.0,
        )
        meshes[f"intake_{name}"] = cylinder(
            x, 1.74, 2.55, 0.34, 0.14, axis="z", segments=36
        )
        meshes[f"exhaust_{name}"] = cylinder(
            x, 1.48, -2.35, 0.16, 0.42, axis="z", segments=24
        )
        meshes[f"pylon_{name}"] = box(x, 1.85, 0.35, 0.42, 0.55, 1.85)
        meshes[f"gear_fairing_{name}"] = translated(
            oval_lathe_fuselage(
                [
                    (-1.70, 0.16, 0.20, 1.20),
                    (-1.05, 0.34, 0.36, 1.14),
                    (-0.30, 0.40, 0.42, 1.15),
                    (0.38, 0.26, 0.28, 1.23),
                ],
                segments=28,
            ),
            x,
            0.0,
            0.0,
        )

        # Four Dowty blades + tips. propeller_{side} is the spin parent; _b/_c/_d
        # nest under it as Blade / Blade 2 / Blade 3 (see NestCrossPropellerBlades).
        for index, suffix in enumerate(("", "_b", "_c", "_d")):
            angle = index * 90.0
            meshes[f"propeller_{name}{suffix}"] = propeller_blade(
                x, 1.74, 2.95, angle, radial_start=0.20, radial_end=1.45
            )
            meshes[f"propeller_{name}_tip{suffix}"] = propeller_blade(
                x,
                1.74,
                2.95,
                angle,
                radial_start=1.42,
                radial_end=PROP_RADIUS,
                tip=True,
            )
        meshes[f"prop_hub_{name}"] = cylinder(
            x, 1.74, 2.88, 0.17, 0.22, axis="z", segments=32
        )
        meshes[f"spinner_{name}"] = translated(
            oval_lathe_fuselage(
                [
                    (2.78, 0.20, 0.20, 1.74),
                    (3.05, 0.21, 0.21, 1.74),
                    (3.32, 0.04, 0.04, 1.74),
                ],
                segments=32,
            ),
            x,
            0.0,
            0.0,
        )

    # Conventional empennage — swept fin with a low-mounted horizontal tail
    # (not the Q400 T-tail). Fin tip owns the exact 6.97 m height.
    # Empennage must stay inside ±HALF_LENGTH. Chord trails aft (−Z), so leading
    # edges are placed so the trailing tip lands on z = −HALF_LENGTH.
    meshes["tail_fin"] = lofted_aerofoil(
        [
            (2.55, 0.0, -6.10, 3.10, 0.24),
            (4.60, 0.0, -7.70, 1.90, 0.16),
            (TARGET_HEIGHT_M, 0.0, -8.85, 1.00, 0.09),
        ],
        chord_points=16,
        vertical=True,
    )
    meshes["dorsal_fin"] = lofted_aerofoil(
        [
            (2.45, 0.0, -4.90, 1.55, 0.14),
            (3.55, 0.0, -6.70, 0.90, 0.09),
        ],
        chord_points=10,
        vertical=True,
    )
    meshes["rudder"] = lofted_aerofoil(
        [
            (3.20, 0.0, -8.95, 0.42, 0.06),
            (6.20, 0.0, -9.20, 0.34, 0.05),
        ],
        chord_points=10,
        vertical=True,
    )
    # Horizontal stabiliser sits on the rear fuselage (true conventional
    # empennage — not mid-fin cruciform and not a T-tail).
    meshes["tailplane"] = lofted_aerofoil(
        [
            (-4.55, 2.66, -8.35, 0.95, 0.08),
            (-0.20, 2.62, -7.65, 1.65, 0.14),
            (0.20, 2.62, -7.65, 1.65, 0.14),
            (4.55, 2.66, -8.35, 0.95, 0.08),
        ],
        chord_points=12,
    )
    meshes["elevator_left"] = box(-2.30, 2.62, -8.95, 4.20, 0.06, 0.38)
    meshes["elevator_right"] = box(2.30, 2.62, -8.95, 4.20, 0.06, 0.38)
    meshes["tail_root_fairing"] = box(0.0, 2.75, -6.40, 0.55, 0.85, 1.60)

    # Passenger windows: tall rounded panes at the 0.51 m frame pitch, ~0.3 m above the cabin
    # axis, two per node, sampled from the skin (the old flat boxes hovered ~5 cm off it).
    def window_pair(z, side):
        angle = 180.0 - 15.0 if side < 0 else 15.0
        panes = []
        for k in range(2):
            zk = z - k * 0.508
            if side < 0 and zk > 6.55 - 0.325 - 0.35:      # forward passenger door (left)
                continue
            if side > 0 and zk < -5.60 + 0.46 + 0.35:      # aft cargo door (right)
                continue
            panes.append(skin.window(fuselage_surface, zk, angle, width=0.24, height=0.34))
        return skin.merge_meshes(panes) if panes else None

    counts = {-1: 0, 1: 0}
    for z in np.arange(6.00, -5.30, -1.016):
        for side, suffix in ((-1, ""), (1, "r")):
            pair = window_pair(float(z), side)
            if pair is None:
                continue
            counts[side] += 1
            meshes[f"cabin_window_{suffix}{counts[side]}"] = pair

    # Four compact panes follow the rounded nose; gaps are the pillars.
    def pane(z, angle, half_len, half_arc, front):
        return skin.skin_patch(fuselage_surface, z, angle, half_len, half_arc,
                               front=front, radius=0.06, rings=2, max_edge=0.12)

    meshes["windscreen_l"] = pane(8.20, 108.0, 0.40, 0.14, 0.012)
    meshes["windscreen_r"] = pane(8.20, 72.0, 0.40, 0.14, 0.012)
    meshes["cockpit_side_l"] = pane(7.70, 136.0, 0.50, 0.24, 0.010)
    meshes["cockpit_side_r"] = pane(7.70, 44.0, 0.50, 0.24, 0.010)

    meshes["livery_stripe"] = box(-1.135, 1.78, 0.40, 0.03, 0.12, 13.5)
    meshes["livery_stripe_lower"] = box(1.135, 1.78, 0.40, 0.03, 0.12, 13.5)

    # Forward left passenger door and aft right cargo door, curved with the skin.
    meshes["door_outline_fwd"], meshes["door_fwd"], _handle = skin.door_set(
        fuselage_surface, 6.55, 180.0, 0.325, 0.65)
    meshes["cargo_door_outline"], meshes["cargo_door"], _latch = skin.door_set(
        fuselage_surface, -5.60, 0.0, 0.46, 0.56)

    meshes["belly_fairing"] = oval_lathe_fuselage(
        [
            (-3.50, 0.22, 0.08, 0.95),
            (-0.50, 0.38, 0.14, 0.92),
            (3.50, 0.40, 0.15, 0.92),
            (5.80, 0.22, 0.08, 0.98),
        ],
        segments=32,
    )
    meshes["radome"] = oval_lathe_fuselage(
        [
            (8.20, 0.72, 0.62, 1.62),
            (8.80, 0.48, 0.42, 1.52),
            (9.25, 0.24, 0.20, 1.46),
            (HALF_LENGTH - 0.12, 0.04, 0.03, 1.46),
        ],
        segments=36,
    )

    # Tricycle gear: twin-wheel nose and twin side-by-side mains that retract
    # forward into the nacelles (Saab 340B / EASA.A.068 undercarriage layout;
    # Dowty/AP Precision Hydraulics family).
    for side, name in ((-1.0, "left"), (1.0, "right")):
        x = side * 3.55
        meshes[f"gear_{name}"] = box(x, 0.95, -0.55, 0.16, 1.55, 0.26)
        meshes[f"gear_oleo_{name}"] = cylinder(
            x, 0.78, -0.55, 0.07, 1.15, axis="y", segments=20
        )
        meshes[f"gear_door_{name}"] = box(
            x + side * 0.48, 1.15, -0.55, 0.08, 1.15, 1.05
        )
        wheel_set(meshes, f"{name}_inboard", x - side * 0.14, -0.55, 0.38, 0.18)
        wheel_set(meshes, f"{name}_outboard", x + side * 0.14, -0.55, 0.38, 0.18)

    meshes["gear_nose"] = box(0.0, 0.72, 7.15, 0.12, 1.05, 0.18)
    meshes["gear_oleo_nose"] = cylinder(
        0.0, 0.58, 7.15, 0.05, 0.72, axis="y", segments=20
    )
    meshes["gear_door_nose"] = box(0.0, 1.05, 6.95, 0.48, 0.06, 0.95)
    wheel_set(meshes, "nose_left", -0.16, 7.18, 0.28, 0.14)
    wheel_set(meshes, "nose_right", 0.16, 7.18, 0.28, 0.14)

    meshes["nav_light_left"] = box(-HALF_SPAN + 0.04, 2.20, 0.55, 0.08, 0.08, 0.08)
    meshes["nav_light_right"] = box(HALF_SPAN - 0.04, 2.20, 0.55, 0.08, 0.08, 0.08)
    meshes["static_wick_left"] = box(-HALF_SPAN + 0.10, 2.15, 0.30, 0.03, 0.02, 0.15)
    meshes["static_wick_right"] = box(HALF_SPAN - 0.10, 2.15, 0.30, 0.03, 0.02, 0.15)
    meshes["tail_nav_light"] = box(0.0, 6.75, -9.72, 0.08, 0.08, 0.08)
    meshes["beacon_top"] = box(0.0, 3.15, -0.40, 0.10, 0.10, 0.10)
    meshes["landing_light_l"] = box(-3.55, 1.45, 2.70, 0.16, 0.12, 0.08)
    meshes["landing_light_r"] = box(3.55, 1.45, 2.70, 0.16, 0.12, 0.08)
    meshes["taxi_light"] = box(0.0, 0.68, 7.29, 0.14, 0.10, 0.10)

    return {name: outward_winding(mesh) for name, mesh in meshes.items()}


def bounds(meshes):
    vertices = np.concatenate([part for part, _ in meshes.values()], axis=0)
    return vertices.min(axis=0), vertices.max(axis=0)


def validate(meshes):
    if not meshes:
        raise ValueError("AIR-007 emitted no meshes")
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
            f"AIR-007 bounds {dimensions.tolist()} do not match {expected.tolist()}"
        )
    if abs(float(minimum[1])) > 0.02:
        raise ValueError(f"AIR-007 tyres must touch local y=0, got {minimum[1]:.4f}")
    return minimum, maximum


def main():
    AIRCRAFT.mkdir(parents=True, exist_ok=True)
    meshes = saab_meshes()
    minimum, maximum = validate(meshes)
    write_kit(AIRCRAFT, BASENAME, meshes)
    dimensions = maximum - minimum
    triangles = sum(len(indices) // 3 for _, indices in meshes.values())
    print(
        f"AIR-007 ready: {BASENAME} ({len(meshes)} meshes, {triangles} triangles; "
        f"{dimensions[0]:.2f} m span x {dimensions[1]:.2f} m height x "
        f"{dimensions[2]:.2f} m length)"
    )


if __name__ == "__main__":
    main()
