#!/usr/bin/env python3
"""Geometry regressions for AIR-009's A350-900-class runtime model."""
import importlib.util
from pathlib import Path

import numpy as np

SCRIPTS = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("air_009", SCRIPTS / "generate-air-009-a350-900.py")
module = importlib.util.module_from_spec(spec)
assert spec.loader is not None
spec.loader.exec_module(module)

meshes = module.a350_900_meshes()
minimum, maximum = module.validate_meshes(meshes)
np.testing.assert_allclose(maximum - minimum, [64.75, 17.05, 66.80], atol=0.025)
assert abs(float(maximum[0]) - 32.375) < 0.02
assert abs(float(minimum[0]) + 32.375) < 0.02
assert abs(float(minimum[1])) < 0.02
assert abs(float(maximum[2])) < 0.02
assert {"cockpit_mask_left", "cockpit_mask_right", "wingtip_left", "wingtip_right"} <= set(meshes)
assert sum(name.startswith("tire_") for name in meshes) == 10
assert sum(name.startswith("fan_blade_l") for name in meshes) == 18
assert sum(name.startswith("fan_blade_r") for name in meshes) == 18
fuselage_vertices = meshes["fuselage"][0]
assert fuselage_vertices[:, 0].max() - fuselage_vertices[:, 0].min() > 5.9

print("PASS: AIR-009 purpose-built A350 envelope, widebody cabin, raked tips, large fans and ten-wheel gear.")
