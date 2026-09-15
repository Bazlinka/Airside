#!/usr/bin/env python3
"""Geometry regressions for AIR-007's close-view readability pass; no Unity required."""
import importlib.util
from pathlib import Path

import numpy as np


SCRIPTS = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location(
    "air_007", SCRIPTS / "generate-air-007-saab-340b.py"
)
module = importlib.util.module_from_spec(spec)
assert spec.loader is not None
spec.loader.exec_module(module)

meshes = module.saab_meshes()
module.validate(meshes)
vertices = np.concatenate([verts for verts, _ in meshes.values()])

np.testing.assert_allclose(
    np.ptp(vertices, axis=0),
    [module.TARGET_SPAN_M, module.TARGET_HEIGHT_M, module.TARGET_LENGTH_M],
    atol=0.003,
)
assert abs(float(vertices[:, 1].min())) < 0.001, "tyres must sit at local y=0"

# Four small panels follow the rounded nose rather than projecting as a boxy mask.
for pane in ("windscreen_l", "windscreen_r", "cockpit_side_l", "cockpit_side_r"):
    pane_vertices, _ = meshes[pane]
    assert len(pane_vertices) == 8, f"{pane} must remain a thin fitted panel"
    assert np.ptp(pane_vertices, axis=0)[2] < 1.2, f"{pane} is too long for the flight deck"

# Main-gear fairings are a curved nacelle continuation, not a six-faced block.
for fairing in ("gear_fairing_left", "gear_fairing_right"):
    fairing_vertices, _ = meshes[fairing]
    assert len(fairing_vertices) > 100, f"{fairing} must retain its rounded profile"
    assert np.ptp(fairing_vertices, axis=0)[2] < 2.4

# Hubs stay visibly smaller than the 3.35 m four-blade propeller diameter.
for hub in ("prop_hub_left", "prop_hub_right"):
    hub_vertices, _ = meshes[hub]
    assert np.ptp(hub_vertices, axis=0)[0] < 0.36

for name, (part_vertices, indices) in meshes.items():
    assert np.isfinite(part_vertices).all(), name
    assert len(indices) % 3 == 0 and int(indices.max()) < len(part_vertices), name

print("PASS: AIR-007 bounds, fitted cockpit, curved gear bays, compact hubs and mesh integrity.")
