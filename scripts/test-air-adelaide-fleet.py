#!/usr/bin/env python3
"""Geometry regressions for AIR-011 through AIR-016."""

import importlib.util
from pathlib import Path
import numpy as np


scripts = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("adelaide_fleet", scripts / "generate-air-adelaide-fleet.py")
fleet = importlib.util.module_from_spec(spec)
assert spec.loader is not None
spec.loader.exec_module(fleet)

for type_id, builder in fleet.BUILDERS.items():
    meshes = builder()
    fleet.validate(type_id, meshes)

assert not any(name.startswith("exhaust_chevron_") for name in fleet.boeing_737_800_meshes())
assert not any(name.startswith("cockpit_mask_") for name in fleet.airbus_a330_900neo_meshes())
assert any(name.startswith("exhaust_chevron_") for name in fleet.boeing_787_9_meshes())
assert fleet.SPECS["E190"][1] < fleet.SPECS["A223"][1] < fleet.SPECS["A320"][1]

a320 = fleet.airbus_a320_200_meshes()
assert "winglet_left" in a320 and "winglet_right" in a320
assert "wingtip_left" not in a320 and "wing_root_left" not in a320
assert "windscreen_l" in a320 and "windscreen_r" in a320
assert len([name for name in a320 if name.startswith("cabin_window_")]) >= 38
for name in ("livery_stripe", "livery_stripe_lower"):
    vertices, _ = a320[name]
    assert vertices.shape[0] >= 128 and np.ptp(vertices[:, 2]) > 25.0

print("PASS: six Adelaide fleet models have exact envelopes, valid geometry and family cues.")
