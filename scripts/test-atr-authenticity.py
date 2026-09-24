#!/usr/bin/env python3
"""Deterministic source checks for AIR-001's open cockpit and swept propellers."""

import importlib.util
from pathlib import Path

import numpy as np


HERE = Path(__file__).resolve().parent


def load(filename):
    spec = importlib.util.spec_from_file_location(filename.replace("-", "_"), HERE / filename)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


source = load("generate-air-001-atr42-v03.py")
atr = load("atr_aircraft_authenticity.py")
glazing = load("polish-aircraft-glazing.py")
meshes = source.final_meshes()
baseline_blade = meshes["propeller_left"][0].copy()
atr.enhance(meshes, source)
assert glazing.polish(meshes) == 43
atr.finalize(meshes)

for name in atr.PANES:
    vertices, indices = meshes[name]
    assert len(vertices) > 80 and len(indices) > 80, name
    assert np.isfinite(vertices).all() and indices.max() < len(vertices), name
    # The actual cutout is inset inside the glass perimeter; a pane remains one
    # surface so alpha does not accumulate through front and back sheets.
    z, angle, _, _ = atr.PANES[name]
    assert atr._opening(z, angle, source), name
    assert not atr._opening(z + 1.5, angle, source), name

for side in ("left", "right"):
    for part in ("seat", "torso", "head"):
        assert f"pilot_{side}_{part}" in meshes
    assert meshes[f"pilot_{side}_head"][0][:, 2].mean() > 8.8

all_vertices = np.concatenate([vertices for vertices, _ in meshes.values()])
np.testing.assert_allclose(np.ptp(all_vertices, axis=0), [24.57, 7.59, 22.67], atol=0.004)
assert abs(all_vertices[:, 1].min()) < 0.001
for name, (vertices, indices) in meshes.items():
    assert np.isfinite(vertices).all() and len(indices) % 3 == 0 and indices.max() < len(vertices), name
    triangles = vertices[indices.reshape(-1, 3)]
    area = np.linalg.norm(np.cross(triangles[:, 1] - triangles[:, 0],
                                   triangles[:, 2] - triangles[:, 0]), axis=1)
    assert np.all(area > 1e-9), name

new_blade = meshes["propeller_left"][0]
assert np.max(np.linalg.norm(new_blade - baseline_blade, axis=1)) > 0.10
assert sum(name.startswith("propeller_left") and "tip" not in name for name in meshes) == 6
assert sum(name.startswith("propeller_right") and "tip" not in name for name in meshes) == 6
print("PASS: four open fitted panes, two seated pilots, swept six-blade props and exact ATR envelope.")
