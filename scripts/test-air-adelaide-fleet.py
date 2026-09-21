#!/usr/bin/env python3
"""Geometry regressions for AIR-011 through AIR-016."""

import importlib.util
from pathlib import Path


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

print("PASS: six Adelaide fleet models have exact envelopes, valid geometry and family cues.")
