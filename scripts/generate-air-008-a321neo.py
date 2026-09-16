#!/usr/bin/env python3
"""Generate AIR-008: an original A321neo-class narrowbody kit.

The kit deliberately reuses the project's proven narrowbody topology, then applies
the official A321neo 44.51 x 35.80 x 11.76 m envelope and removes the 737-style
lower scimitar tips.  The remaining tall upper tips read as Airbus sharklets.  It
is unbranded project-owned geometry, not a downloaded or ripped airline model.
"""

from __future__ import annotations

import importlib.util
from pathlib import Path

import numpy as np


SCRIPTS = Path(__file__).resolve().parent
REPO = SCRIPTS.parent
AIRCRAFT = REPO / "game" / "Airside" / "Assets" / "Airside" / "Art" / "Models" / "Aircraft"
BASENAME = "mdl_a321neo_v01"

_BASE_SPEC = importlib.util.spec_from_file_location(
    "airside_737_8", SCRIPTS / "generate-air-005-narrowbody-737-8.py"
)
_base = importlib.util.module_from_spec(_BASE_SPEC)
assert _BASE_SPEC.loader is not None
_BASE_SPEC.loader.exec_module(_base)

TARGET_LENGTH_M = 44.51
TARGET_SPAN_M = 35.80
TARGET_HEIGHT_M = 11.76


def a321neo_meshes():
    meshes = _base.narrowbody_737_8_meshes()
    meshes.pop("wingtip_left", None)
    meshes.pop("wingtip_right", None)

    scale = np.array(
        (
            TARGET_SPAN_M / _base.TARGET_SPAN_M,
            TARGET_HEIGHT_M / _base.TARGET_HEIGHT_M,
            TARGET_LENGTH_M / _base.TARGET_LENGTH_M,
        ),
        dtype=np.float32,
    )
    return {
        name: (vertices * scale, indices)
        for name, (vertices, indices) in meshes.items()
    }


def bounds(meshes):
    vertices = np.concatenate([vertices for vertices, _ in meshes.values()], axis=0)
    return vertices.min(axis=0), vertices.max(axis=0)


def validate_meshes(meshes):
    if "wingtip_left" in meshes or "wingtip_right" in meshes:
        raise ValueError("AIR-008 must use single Airbus-style sharklets")
    for name, (vertices, indices) in meshes.items():
        if not len(vertices) or not len(indices) or len(indices) % 3:
            raise ValueError(f"{name}: invalid triangle mesh")
        if not np.isfinite(vertices).all() or int(np.max(indices)) >= len(vertices):
            raise ValueError(f"{name}: invalid geometry")
    minimum, maximum = bounds(meshes)
    dimensions = maximum - minimum
    expected = np.array((TARGET_SPAN_M, TARGET_HEIGHT_M, TARGET_LENGTH_M), np.float32)
    if not np.allclose(dimensions, expected, rtol=0.0, atol=0.02):
        raise ValueError(f"AIR-008 bounds {dimensions.tolist()} do not match {expected.tolist()}")
    if abs(float(minimum[1])) > 0.02 or abs(float(maximum[2])) > 0.02:
        raise ValueError("AIR-008 must sit on y=0 with its nose-stop at z=0")
    return minimum, maximum


def main():
    AIRCRAFT.mkdir(parents=True, exist_ok=True)
    meshes = a321neo_meshes()
    minimum, maximum = validate_meshes(meshes)
    _base.write_kit(AIRCRAFT, BASENAME, meshes)
    dimensions = maximum - minimum
    triangles = sum(len(indices) // 3 for _, indices in meshes.values())
    print(
        f"AIR-008 ready: {BASENAME} ({len(meshes)} meshes, {triangles} triangles; "
        f"{dimensions[0]:.2f} m span x {dimensions[1]:.2f} m height x {dimensions[2]:.2f} m length)"
    )


if __name__ == "__main__":
    main()
