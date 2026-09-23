#!/usr/bin/env python3
"""Geometry regressions for AIR-006's nacelle-bay polish; no Unity required."""
import importlib.util
from pathlib import Path

import numpy as np


SCRIPTS = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location(
    "air_006", SCRIPTS / "generate-air-006-dash8-q400.py"
)
module = importlib.util.module_from_spec(spec)
assert spec.loader is not None
spec.loader.exec_module(module)

meshes = module.q400_meshes()
assert np.all(np.diff(module.STATIONS[:, 0]) > 0)
assert float(module.surface(15.55, 0.0)[0]) > 0.40, "Q400 nose must stay rounded ahead of the flight deck"
module.validate(meshes)
vertices = np.concatenate([verts for verts, _ in meshes.values()])

np.testing.assert_allclose(
    np.ptp(vertices, axis=0),
    [module.TARGET_SPAN_M, module.TARGET_HEIGHT_M, module.TARGET_LENGTH_M],
    atol=0.003,
)
assert abs(float(vertices[:, 1].min())) < 0.001, "tyres must sit at local y=0"

# The gear enclosure is a rounded continuation of each nacelle, not a six-faced box.
for fairing in ("gear_fairing_left", "gear_fairing_right"):
    fairing_vertices, _ = meshes[fairing]
    assert len(fairing_vertices) > 500, f"{fairing} must retain a rounded bay profile"
    assert np.ptp(fairing_vertices, axis=0)[2] < 3.1

# Open doors remain thin readable sheets below that curved bay.
for door in ("gear_door_left", "gear_door_right", "gear_door_inner_l", "gear_door_inner_r"):
    door_vertices, _ = meshes[door]
    assert np.ptp(door_vertices, axis=0)[0] <= 0.11, f"{door} must stay a thin door sheet"

assert "wing_centre_saddle" in meshes and len(meshes["wing_centre_saddle"][0]) > 2000

for name, (part_vertices, indices) in meshes.items():
    assert np.isfinite(part_vertices).all(), name
    assert len(indices) % 3 == 0 and int(indices.max()) < len(part_vertices), name

print("PASS: AIR-006 bounds, rounded nacelle bays, thin doors, saddle and mesh integrity.")
