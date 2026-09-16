#!/usr/bin/env python3
"""Geometry regressions for AIR-008's A321neo-class runtime model."""
import importlib.util
from pathlib import Path

import numpy as np


SCRIPTS = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location(
    "air_008", SCRIPTS / "generate-air-008-a321neo.py"
)
module = importlib.util.module_from_spec(spec)
assert spec.loader is not None
spec.loader.exec_module(module)

meshes = module.a321neo_meshes()
minimum, maximum = module.validate_meshes(meshes)
np.testing.assert_allclose(
    maximum - minimum,
    [module.TARGET_SPAN_M, module.TARGET_HEIGHT_M, module.TARGET_LENGTH_M],
    atol=0.003,
)
assert abs(float(minimum[1])) < 0.001, "tyres must sit at local y=0"
assert abs(float(maximum[2])) < 0.001, "nose-stop datum must remain local z=0"
assert {"winglet_left", "winglet_right"} <= set(meshes)
assert "wingtip_left" not in meshes and "wingtip_right" not in meshes
for side, short in (("left", "l"), ("right", "r")):
    assert f"nacelle_{side}" in meshes and f"fan_{side}" in meshes
    for index in range(1, 13):
        assert f"fan_blade_{short}{index}" in meshes

print("PASS: AIR-008 exact A321neo envelope, sharklets, open turbofans and mesh integrity.")
