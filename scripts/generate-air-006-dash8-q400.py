#!/usr/bin/env python3
"""Generate AIR-006: an original Dash 8-400-class regional turboprop kit.

The model is project-owned, unbranded procedural geometry. It uses the official
Dash 8-400 external envelope (32.83 m length, 28.42 m span, 8.34 m height) and
prioritises the type's gameplay-readable cues: a long high-wing fuselage, large
six-blade propellers, elongated nacelles with main gear, and a tall T-tail.

Coordinates follow the regional-aircraft convention used by Airside: X is span,
Y is up, +Z is forward, the model is centred longitudinally on the motion root,
and the tyres touch local Y=0. The companion glTF stays inside the deliberately
small POSITION + uint16-index contract consumed by ArtGltfLoader.
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
outward_winding = _v06.outward_winding
propeller_blade = _v06.propeller_blade

TARGET_LENGTH_M = 32.83
TARGET_SPAN_M = 28.42
TARGET_HEIGHT_M = 8.34
HALF_LENGTH = TARGET_LENGTH_M / 2.0
HALF_SPAN = TARGET_SPAN_M / 2.0
PROP_RADIUS = 2.05


def translated(mesh: tuple[np.ndarray, np.ndarray], x: float, y: float, z: float):
    vertices, indices = mesh
    moved = vertices.copy()
    moved += np.array((x, y, z), np.float32)
    return moved, indices.copy()


def panel(
    side: float,
    x_inner: float,
    x_outer: float,
    y_inner: float,
    y_outer: float,
    leading_inner: float,
    leading_outer: float,
    chord_inner: float,
    chord_outer: float,
    thickness: float,
):
    """Small closed tapered aerofoil panel used for moving wing surfaces."""
    return lofted_aerofoil(
        [
            (side * x_inner, y_inner, leading_inner, chord_inner, thickness),
            (side * x_outer, y_outer, leading_outer, chord_outer, thickness * 0.65),
        ],
        chord_points=10,
    )


def wheel_set(
    meshes: dict[str, tuple[np.ndarray, np.ndarray]],
    prefix: str,
    x: float,
    z: float,
    radius: float,
    width: float,
) -> None:
    meshes[f"tire_{prefix}"] = cylinder(x, radius, z, radius, width, axis="x", segments=20)
    meshes[f"wheel_{prefix}"] = cylinder(x, radius, z, radius * 0.62, width + 0.025, axis="x", segments=16)
    meshes[f"rim_{prefix}"] = cylinder(x, radius, z, radius * 0.34, width + 0.045, axis="x", segments=14)


def q400_meshes() -> dict[str, tuple[np.ndarray, np.ndarray]]:
    meshes: dict[str, tuple[np.ndarray, np.ndarray]] = {}

    # The lathe helper adds a 0.12 m cap beyond each end station. Setting the
    # stations 0.12 m inboard therefore makes the total length exact.
    meshes["fuselage"] = oval_lathe_fuselage(
        [
            (-HALF_LENGTH + 0.12, 0.05, 0.05, 2.46),
            (-15.55, 0.24, 0.22, 2.48),
            (-14.65, 0.62, 0.55, 2.48),
            (-13.45, 1.02, 0.94, 2.46),
            (-11.70, 1.29, 1.24, 2.42),
            (-8.80, 1.36, 1.36, 2.40),
            (8.90, 1.36, 1.36, 2.40),
            (10.80, 1.31, 1.29, 2.38),
            (12.25, 1.17, 1.10, 2.30),
            (13.45, 0.92, 0.80, 2.17),
            (14.45, 0.61, 0.50, 2.03),
            (15.35, 0.28, 0.22, 1.92),
            (HALF_LENGTH - 0.12, 0.05, 0.05, 1.88),
        ],
        segments=40,
    )

    # High wing. The outer stations set the exact 28.42 m span.
    wing_root = (1.18, 4.54, 3.10, 4.15, 0.38)
    wing_mid = (6.20, 4.67, 2.72, 2.86, 0.25)
    wing_tip = (HALF_SPAN, 4.88, 1.76, 1.18, 0.10)
    for side, name in ((-1.0, "left"), (1.0, "right")):
        meshes[f"wing_{name}"] = lofted_aerofoil(
            [(side * x, y, z, chord, thick) for x, y, z, chord, thick in (wing_root, wing_mid, wing_tip)],
            chord_points=16,
        )
        meshes[f"flap_{name}"] = panel(side, 1.65, 7.05, 4.42, 4.60, 0.10, 0.27, 1.12, 0.72, 0.10)
        meshes[f"aileron_{name}"] = panel(side, 7.45, 13.55, 4.68, 4.84, 0.34, 0.64, 0.66, 0.30, 0.065)
        meshes[f"wing_root_{name}"] = box(side * 1.20, 4.38, 0.92, 0.32, 0.42, 3.55)

    # Long powerplant nacelles sit beneath the wing. Their aft bodies house the
    # distinctive, long-travel main undercarriage.
    for side, name in ((-1.0, "left"), (1.0, "right")):
        x = side * 4.35
        pod = oval_lathe_fuselage(
            [
                (-4.35, 0.18, 0.16, 3.45),
                (-3.75, 0.48, 0.42, 3.48),
                (-1.60, 0.72, 0.68, 3.52),
                (2.95, 0.76, 0.72, 3.56),
                (4.58, 0.65, 0.62, 3.58),
                (5.22, 0.28, 0.27, 3.60),
            ],
            segments=30,
        )
        meshes[f"engine_{name}"] = translated(pod, x, 0.0, 0.0)
        meshes[f"nacelle_{name}"] = cylinder(x, 3.55, 1.10, 0.68, 6.95, axis="z", segments=28)
        meshes[f"intake_{name}"] = cylinder(x, 3.60, 4.66, 0.49, 0.18, axis="z", segments=26)
        meshes[f"exhaust_{name}"] = cylinder(x, 3.45, -3.92, 0.18, 0.54, axis="z", segments=18)
        meshes[f"pylon_{name}"] = box(x, 4.25, 0.45, 0.54, 1.18, 3.20)
        meshes[f"gear_fairing_{name}"] = box(x, 2.80, -1.90, 1.05, 1.40, 2.85)

        # Six swept blades plus separate ochre tips. The first blade keeps the
        # canonical propeller_left/right name used as the animation parent.
        suffixes = ("", "_b", "_c", "_d", "_e", "_f")
        for index, suffix in enumerate(suffixes):
            angle = index * 60.0
            meshes[f"propeller_{name}{suffix}"] = propeller_blade(
                x, 3.60, 5.42, angle, radial_start=0.24, radial_end=1.84
            )
            meshes[f"propeller_{name}_tip{suffix}"] = propeller_blade(
                x, 3.60, 5.42, angle, radial_start=1.82, radial_end=PROP_RADIUS, tip=True
            )
        meshes[f"prop_hub_{name}"] = cylinder(x, 3.60, 5.38, 0.27, 0.30, axis="z", segments=24)
        meshes[f"spinner_{name}"] = translated(
            oval_lathe_fuselage(
                [(5.30, 0.26, 0.26, 3.60), (5.64, 0.32, 0.32, 3.60), (5.98, 0.04, 0.04, 3.60)],
                segments=24,
            ),
            x,
            0.0,
            0.0,
        )

    # Tall swept fin and high-mounted horizontal stabiliser establish the Q400
    # T-tail read. The fin's tip station owns the exact 8.34 m total height.
    meshes["tail_fin"] = lofted_aerofoil(
        [(3.54, 0.0, -10.15, 5.00, 0.30), (8.34, 0.0, -13.75, 1.72, 0.13)],
        chord_points=18,
        vertical=True,
    )
    meshes["dorsal_fin"] = lofted_aerofoil(
        [(3.45, 0.0, -8.70, 2.45, 0.18), (5.15, 0.0, -11.35, 1.25, 0.10)],
        chord_points=12,
        vertical=True,
    )
    meshes["rudder"] = box(0.0, 6.02, -14.50, 0.075, 4.25, 0.58)
    meshes["tailplane"] = lofted_aerofoil(
        [(-6.65, 7.88, -13.20, 1.35, 0.10), (-0.25, 7.82, -11.92, 2.65, 0.19),
         (0.25, 7.82, -11.92, 2.65, 0.19), (6.65, 7.88, -13.20, 1.35, 0.10)],
        chord_points=14,
    )
    meshes["elevator_left"] = box(-3.38, 7.79, -14.05, 6.15, 0.07, 0.54)
    meshes["elevator_right"] = box(3.38, 7.79, -14.05, 6.15, 0.07, 0.54)

    # Two-sided cabin window rows, four-pane flight deck and restrained fictional
    # colour geometry. There is deliberately no text, logo or registration.
    window_z = np.linspace(10.10, -8.15, 20)
    for index, z in enumerate(window_z, start=1):
        meshes[f"cabin_window_{index}"] = box(-1.345, 2.76, float(z), 0.035, 0.42, 0.48)
        meshes[f"cabin_window_r{index}"] = box(1.345, 2.76, float(z), 0.035, 0.42, 0.48)
    meshes["windscreen_l"] = box(-0.58, 2.95, 14.00, 0.78, 0.64, 0.08)
    meshes["windscreen_r"] = box(0.58, 2.95, 14.00, 0.78, 0.64, 0.08)
    meshes["cockpit_side_l"] = box(-1.18, 2.87, 13.52, 0.055, 0.58, 0.75)
    meshes["cockpit_side_r"] = box(1.18, 2.87, 13.52, 0.055, 0.58, 0.75)
    meshes["livery_stripe"] = box(-1.352, 2.18, 0.80, 0.035, 0.16, 22.8)
    meshes["livery_stripe_lower"] = box(1.352, 2.18, 0.80, 0.035, 0.16, 22.8)
    meshes["door_outline_fwd"] = box(-1.365, 2.30, 11.00, 0.035, 1.82, 0.98)
    meshes["door_fwd"] = box(-1.385, 2.30, 11.00, 0.035, 1.65, 0.82)
    meshes["cargo_door_outline"] = box(1.365, 2.20, -9.55, 0.035, 1.55, 1.35)
    meshes["cargo_door"] = box(1.385, 2.20, -9.55, 0.035, 1.40, 1.20)

    # Long main legs descend from the nacelles, unlike the ATR's compact
    # fuselage-side gear. Four main wheels and two nose wheels touch Y=0.
    for side, name in ((-1.0, "left"), (1.0, "right")):
        x = side * 4.35
        meshes[f"gear_{name}"] = box(x, 1.47, -1.72, 0.20, 2.25, 0.32)
        meshes[f"gear_oleo_{name}"] = cylinder(x, 1.18, -1.72, 0.085, 1.48, axis="y", segments=16)
        meshes[f"gear_door_{name}"] = box(x + side * 0.42, 2.22, -1.72, 0.10, 1.55, 1.28)
        wheel_set(meshes, f"{name}_forward", x, -1.38, 0.46, 0.27)
        wheel_set(meshes, f"{name}_aft", x, -2.08, 0.46, 0.27)
    meshes["gear_nose"] = box(0.0, 1.05, 11.25, 0.16, 1.45, 0.22)
    meshes["gear_oleo_nose"] = cylinder(0.0, 0.79, 11.25, 0.06, 0.92, axis="y", segments=16)
    meshes["gear_door_nose"] = box(0.0, 1.42, 11.05, 0.58, 0.07, 1.28)
    wheel_set(meshes, "nose_left", -0.19, 11.28, 0.33, 0.17)
    wheel_set(meshes, "nose_right", 0.19, 11.28, 0.33, 0.17)

    meshes["nav_light_left"] = box(-14.165, 4.88, 1.18, 0.09, 0.09, 0.09)
    meshes["nav_light_right"] = box(14.165, 4.88, 1.18, 0.09, 0.09, 0.09)
    meshes["tail_nav_light"] = box(0.0, 7.86, -14.55, 0.09, 0.09, 0.09)
    meshes["beacon_top"] = box(0.0, 3.78, -0.75, 0.12, 0.12, 0.12)
    meshes["landing_light_l"] = box(-4.35, 3.18, 5.32, 0.20, 0.15, 0.09)
    meshes["landing_light_r"] = box(4.35, 3.18, 5.32, 0.20, 0.15, 0.09)
    meshes["taxi_light"] = box(0.0, 0.86, 11.40, 0.16, 0.12, 0.12)

    return {name: outward_winding(mesh) for name, mesh in meshes.items()}


def bounds(meshes: dict[str, tuple[np.ndarray, np.ndarray]]) -> tuple[np.ndarray, np.ndarray]:
    vertices = np.concatenate([part for part, _ in meshes.values()], axis=0)
    return vertices.min(axis=0), vertices.max(axis=0)


def validate(meshes: dict[str, tuple[np.ndarray, np.ndarray]]) -> tuple[np.ndarray, np.ndarray]:
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
    if not np.allclose(dimensions, expected, rtol=0.0, atol=0.015):
        raise ValueError(f"AIR-006 bounds {dimensions.tolist()} do not match {expected.tolist()}")
    if abs(float(minimum[1])) > 0.015:
        raise ValueError(f"AIR-006 tyres must touch local y=0, got {minimum[1]:.4f}")
    return minimum, maximum


def main() -> None:
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
