#!/usr/bin/env python3
"""Generate AIR-010: an original, unbranded Boeing 787-10-class widebody.

AIR-010 shares the project's widebody loft primitives with AIR-009, then applies
the 787-10's own 68.30 x 60.12 x 17.02 m envelope and type-defining details:
a narrower 5.77 m cabin, four-pane Boeing flight deck without an A350 mask,
swept/raked tips, chevron exhausts, large high-bypass fans and ten-wheel gear.
"""

from __future__ import annotations

import importlib.util
from pathlib import Path

import numpy as np

SCRIPTS = Path(__file__).resolve().parent
REPO = SCRIPTS.parent
AIRCRAFT = REPO / "game" / "Airside" / "Assets" / "Airside" / "Art" / "Models" / "Aircraft"
BASENAME = "mdl_787_10_v01"

_A350_SPEC = importlib.util.spec_from_file_location(
    "airside_a350", SCRIPTS / "generate-air-009-a350-900.py"
)
a350 = importlib.util.module_from_spec(_A350_SPEC)
assert _A350_SPEC.loader is not None
_A350_SPEC.loader.exec_module(a350)

TARGET_LENGTH_M = 68.30
TARGET_SPAN_M = 60.12
TARGET_HEIGHT_M = 17.02
FUSE_WIDTH_M = 5.77

SCALE = np.asarray(
    (TARGET_SPAN_M / a350.TARGET_SPAN_M,
     TARGET_HEIGHT_M / a350.TARGET_HEIGHT_M,
     TARGET_LENGTH_M / a350.TARGET_LENGTH_M),
    np.float32,
)
FUSE_SCALE = np.asarray(
    (FUSE_WIDTH_M / (a350.FUSE_RX * 2.0), SCALE[1], SCALE[2]), np.float32)


def _scaled_pane(z, angle, half_len, half_arc, front):
    """A skin-conforming windscreen pane in AIR-010's nose-stop frame."""
    vertices, indices = a350.skin.skin_patch(
        a350._surface, z, angle, half_len, half_arc, front=front, radius=0.09, rings=2, max_edge=0.14)
    # AIR-009 patches are returned in centred coordinates. Shift to its nose-stop
    # datum before scaling into AIR-010's nose-stop frame.
    vertices = vertices + np.asarray((0.0, 0.0, -a350.HALF_LENGTH), np.float32)
    return vertices * FUSE_SCALE, indices


def _chevron_ring(cx: float, cy: float, z: float, radius: float, teeth=14):
    vertices: list[list[float]] = []
    indices: list[int] = []
    count = teeth * 2
    for index in range(count):
        angle = 2.0 * np.pi * index / count
        rear_z = z - (0.48 if index % 2 == 0 else 0.18)
        rear_radius = radius * (0.80 if index % 2 == 0 else 0.90)
        vertices.append([cx + radius * np.cos(angle), cy + radius * np.sin(angle), z + 0.10])
        vertices.append([cx + rear_radius * np.cos(angle), cy + rear_radius * np.sin(angle), rear_z])
    for index in range(count):
        following = (index + 1) % count
        a, b, c, d = 2 * index, 2 * following, 2 * following + 1, 2 * index + 1
        indices.extend((a, b, c, a, c, d))
    return a350.orient_outward(np.asarray(vertices, np.float32), np.asarray(indices, np.uint16))


def boeing_787_10_meshes():
    source = a350.a350_900_meshes()
    meshes = {}
    for name, (vertices, indices) in source.items():
        if name.startswith("cockpit_mask_") or name.startswith("windscreen_"):
            continue
        fuselage_attached = (
            name == "fuselage" or name == "belly_fairing"
            or name.startswith("cabin_window_") or name.startswith("door_")
            or name.startswith("beacon_") or name == "taxi_light"
            or name.startswith("livery_")
        )
        meshes[name] = (vertices * (FUSE_SCALE if fuselage_attached else SCALE), indices.copy())

    # Four fitted panes with a skin-coloured centre pillar; the 787 has no A350
    # black raccoon mask. These stay curvature-fitted to the shared nose loft.
    meshes["windscreen_left_outer"] = _scaled_pane(30.10, 129.0, 0.75, 0.40, 0.046)
    meshes["windscreen_left_inner"] = _scaled_pane(30.30, 150.0, 0.75, 0.28, 0.046)
    meshes["windscreen_right_inner"] = _scaled_pane(30.30, 30.0, 0.75, 0.28, 0.046)
    meshes["windscreen_right_outer"] = _scaled_pane(30.10, 51.0, 0.75, 0.40, 0.046)

    # The 787's serrated nacelle trailing edge is a defining silhouette cue.
    for side, suffix in ((-1.0, "left"), (1.0, "right")):
        engine_x = side * 10.75 * float(SCALE[0])
        engine_y = 3.92 * float(SCALE[1])
        exhaust_z = (-2.15 - a350.HALF_LENGTH) * float(SCALE[2])
        meshes[f"exhaust_chevron_{suffix}"] = _chevron_ring(
            engine_x, engine_y, exhaust_z, 0.78 * float(SCALE[1]))

    return meshes


def validate_meshes(meshes):
    if not meshes:
        raise ValueError("AIR-010 emitted no meshes")
    for name, (vertices, indices) in meshes.items():
        if len(vertices) == 0 or len(indices) == 0 or len(indices) % 3:
            raise ValueError(f"{name}: invalid triangle mesh")
        if not np.isfinite(vertices).all() or int(indices.max()) >= len(vertices):
            raise ValueError(f"{name}: invalid vertex/index data")
    all_vertices = np.concatenate([vertices for vertices, _ in meshes.values()])
    minimum, maximum = all_vertices.min(axis=0), all_vertices.max(axis=0)
    expected = np.asarray((TARGET_SPAN_M, TARGET_HEIGHT_M, TARGET_LENGTH_M), np.float32)
    if not np.allclose(maximum - minimum, expected, atol=0.025, rtol=0.0):
        raise ValueError(f"AIR-010 bounds {(maximum - minimum).tolist()} do not match {expected.tolist()}")
    if abs(float(minimum[1])) > 0.02 or abs(float(maximum[2])) > 0.02:
        raise ValueError(f"AIR-010 datum invalid: min y={minimum[1]:.3f}, max z={maximum[2]:.3f}")
    fuselage = meshes["fuselage"][0]
    width = float(fuselage[:, 0].max() - fuselage[:, 0].min())
    if abs(width - FUSE_WIDTH_M) > 0.03:
        raise ValueError(f"AIR-010 fuselage width {width:.3f} does not match {FUSE_WIDTH_M:.2f}")
    return minimum, maximum


def main():
    AIRCRAFT.mkdir(parents=True, exist_ok=True)
    meshes = boeing_787_10_meshes()
    minimum, maximum = validate_meshes(meshes)
    a350.write_kit(AIRCRAFT, BASENAME, meshes)
    triangles = sum(len(indices) // 3 for _, indices in meshes.values())
    dimensions = maximum - minimum
    print(f"AIR-010 ready: {BASENAME} ({len(meshes)} meshes, {triangles} triangles; "
          f"{dimensions[0]:.2f} x {dimensions[1]:.2f} x {dimensions[2]:.2f} m)")


if __name__ == "__main__":
    main()
