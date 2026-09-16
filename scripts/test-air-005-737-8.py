#!/usr/bin/env python3
"""Geometry regressions for AIR-005's close-view readability pass; no Unity required."""
import importlib.util
from pathlib import Path

import numpy as np


SCRIPTS = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location(
    "air_005", SCRIPTS / "generate-air-005-narrowbody-737-8.py"
)
module = importlib.util.module_from_spec(spec)
assert spec.loader is not None
spec.loader.exec_module(module)

meshes = module.narrowbody_737_8_meshes()
module.validate_meshes(meshes)
vertices = np.concatenate([verts for verts, _ in meshes.values()])

np.testing.assert_allclose(
    np.ptp(vertices, axis=0),
    [module.TARGET_SPAN_M, module.TARGET_HEIGHT_M, module.TARGET_LENGTH_M],
    atol=0.003,
)
assert abs(float(vertices[:, 1].min())) < 0.001, "tyres must sit at local y=0"

# The crown stays aircraft skin; glass is carried by three compact fitted panes.
assert "flightdeck_crown" in meshes
assert "cockpit" not in meshes, "the obsolete all-dark cockpit visor must not return"
assert {"windscreen_c", "windscreen_l", "windscreen_r"} <= set(meshes)
for pane in ("windscreen_c", "windscreen_l", "windscreen_r"):
    pane_vertices, _ = meshes[pane]
    assert np.ptp(pane_vertices, axis=0)[2] < 0.30, f"{pane} must stay a compact fitted pane"

# The wing-body fairing is tapered and short enough not to form a long flat slab.
fairing_vertices, _ = meshes["belly_fairing"]
assert np.ptp(fairing_vertices, axis=0)[2] < 13.0
assert np.ptp(fairing_vertices, axis=0)[0] < 1.4

# Split tips retain the full span but are deliberately restrained in vertical height.
for tip in ("winglet_left", "winglet_right"):
    tip_vertices, _ = meshes[tip]
    assert tip_vertices[:, 1].max() <= 7.26, f"{tip} grew into a tall plate"
    assert np.ptp(tip_vertices, axis=0)[1] < 2.0

# Engines use open annular lips, swept tapered blades and one continuous
# serrated nozzle per side. The obsolete floating chevron boxes must stay gone.
for side, short in (("left", "l"), ("right", "r")):
    lip_vertices, _ = meshes[f"nacelle_{side}"]
    centre = np.array([-5.35 if side == "left" else 5.35, 3.13])
    radii = np.linalg.norm(lip_vertices[:, :2] - centre, axis=1)
    assert radii.min() > 0.80, f"{side} inlet is capped instead of open"
    assert f"exhaust_chevron_{side}" in meshes
    assert not any(name.startswith(f"exhaust_chevron_{side}_") for name in meshes)
    for index in range(1, 13):
        blade, _ = meshes[f"fan_blade_{short}{index}"]
        radial = np.linalg.norm(blade[:, :2] - centre, axis=1)
        assert radial.max() > 0.74 and radial.min() < 0.22

for name, (part_vertices, indices) in meshes.items():
    assert np.isfinite(part_vertices).all(), name
    assert len(indices) % 3 == 0 and int(indices.max()) < len(part_vertices), name

print("PASS: AIR-005 bounds, fitted glass, tapered fairing, restrained tips and mesh integrity.")
