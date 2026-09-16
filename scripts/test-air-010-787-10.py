#!/usr/bin/env python3
"""Geometry regressions for AIR-010's Boeing 787-10-class runtime model."""
import importlib.util
from pathlib import Path

import numpy as np

SCRIPTS = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("air_010", SCRIPTS / "generate-air-010-787-10.py")
module = importlib.util.module_from_spec(spec)
assert spec.loader is not None
spec.loader.exec_module(module)

meshes = module.boeing_787_10_meshes()
minimum, maximum = module.validate_meshes(meshes)
np.testing.assert_allclose(maximum - minimum, [60.12, 17.02, 68.30], atol=0.025)
assert abs(float(minimum[1])) < 0.02
assert abs(float(maximum[2])) < 0.02
assert "cockpit_mask_left" not in meshes and "cockpit_mask_right" not in meshes
assert sum(name.startswith("windscreen_") for name in meshes) == 4
assert {"exhaust_chevron_left", "exhaust_chevron_right"} <= set(meshes)
assert sum(name.startswith("tire_") for name in meshes) == 10
assert sum(name.startswith("fan_blade_l") for name in meshes) == 18

print("PASS: AIR-010 exact 787-10 envelope, narrow widebody cabin, four-pane flight deck, chevrons and ten-wheel gear.")
