#!/usr/bin/env python3
"""Geometry regressions for the AIR-001 v03 fidelity pass; no Unity required."""
import importlib.util
from collections import Counter
from pathlib import Path
import numpy as np

SCRIPTS = Path(__file__).resolve().parent


def load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


v03 = load("atr_v03", SCRIPTS / "generate-air-001-atr42-v03.py")
v02 = load("atr_v02", SCRIPTS / "generate-air-001-atr42-v02.py")
v01 = v02.v01

meshes = v03.final_meshes()
assert np.all(np.diff(v03.STATIONS[:, 0]) > 0), "ATR nose stations must advance monotonically"
baseline = v02.final_meshes()
verts = np.concatenate([v for v, _ in meshes.values()])

np.testing.assert_allclose(np.ptp(verts, axis=0), [24.57, 7.59, 22.67], atol=0.003)
assert abs(verts[:, 1].min()) < 0.001, "tyres must sit on y=0"

moving = (
    "gear_", "tire_", "wheel_", "rim_", "propeller_", "spinner_", "prop_hub_",
    "hub_cap_", "flap_", "aileron_", "elevator_", "rudder", "spoiler_",
    "door_fwd", "cargo_door",
)
assert {n for n in baseline if n.startswith(moving)} <= set(meshes)

for name, (v, indices) in meshes.items():
    assert np.isfinite(v).all() and len(indices) % 3 == 0 and indices.max() < len(v), name
    tri = v[indices.reshape(-1, 3)]
    assert np.all(np.linalg.norm(np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0]), axis=1) > 1e-9), name

# Two windows share each node at the real 0.51 m pitch; the right band skips the cargo door.
assert sum(n.startswith("cabin_window_") for n in meshes) >= 18
assert not any(n.startswith("cabin_window_frame_") for n in meshes), \
    "the dark frame slabs read as huge black rectangles behind each window"
assert "windscreen_l" in meshes and "windscreen_r" in meshes
assert "cockpit_side_l" in meshes and "cockpit_side_r" in meshes
assert "windscreen_pillar_c" in meshes
for pane in ("windscreen_l", "windscreen_r", "cockpit_side_l", "cockpit_side_r"):
    pane_vertices, _ = meshes[pane]
    # A curved shell that follows the skin (not one flat quad), but still a thin panel.
    assert len(pane_vertices) < 400, f"{pane} must remain a thin fitted panel"
assert np.ptp(meshes["tailplane_saddle"][0], axis=0)[0] > 1.2, "tailplane saddle must blend into the fin"
assert "gear_fairing_left" in meshes and "gear_fairing_right" in meshes
assert sum(n.startswith("propeller_left") and "tip" not in n for n in meshes) == 6
assert sum(n.startswith("propeller_right") and "tip" not in n for n in meshes) == 6

# Shorter / stockier than the Q400 envelope.
assert np.ptp(verts[:, 2]) < 30.0
assert np.ptp(verts[:, 0]) < 27.0

# Fuselage-side sponsons stay inboard of the nacelle centres (~±3.99 m).
for side, name in ((-1, "gear_fairing_left"), (1, "gear_fairing_right")):
    xs = meshes[name][0][:, 0]
    assert abs(xs.mean()) < 2.2, name
    assert (xs * side).min() > 0.4, name

print(
    "PASS: dimensions, ground contact, articulation names, fitted glazing, "
    "fuselage-side gear sponsons, six-blade props and triangle integrity."
)
