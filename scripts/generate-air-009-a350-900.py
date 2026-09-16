#!/usr/bin/env python3
"""Generate an original, unbranded A350-900-class widebody kit."""

from __future__ import annotations

import importlib.util
from pathlib import Path
import numpy as np

SCRIPTS = Path(__file__).resolve().parent
REPO = SCRIPTS.parent
AIRCRAFT = REPO / "game" / "Airside" / "Assets" / "Airside" / "Art" / "Models" / "Aircraft"
BASENAME = "mdl_a350_900_v01"

spec = importlib.util.spec_from_file_location("airside_737_8", SCRIPTS / "generate-air-005-narrowbody-737-8.py")
base = importlib.util.module_from_spec(spec)
assert spec.loader is not None
spec.loader.exec_module(base)

TARGET_LENGTH_M = 66.80
TARGET_SPAN_M = 64.75
TARGET_HEIGHT_M = 17.05


def meshes():
    source = base.narrowbody_737_8_meshes()
    # Remove the 737 lower split tips. The tall upper tips form the A350's swept winglets.
    source.pop("wingtip_left", None)
    source.pop("wingtip_right", None)
    scale = np.array((TARGET_SPAN_M / base.TARGET_SPAN_M,
                      TARGET_HEIGHT_M / base.TARGET_HEIGHT_M,
                      TARGET_LENGTH_M / base.TARGET_LENGTH_M), dtype=np.float32)
    return {name: (vertices * scale, indices) for name, (vertices, indices) in source.items()}


def main():
    AIRCRAFT.mkdir(parents=True, exist_ok=True)
    kit = meshes()
    vertices = np.concatenate([v for v, _ in kit.values()], axis=0)
    dimensions = vertices.max(axis=0) - vertices.min(axis=0)
    expected = np.array((TARGET_SPAN_M, TARGET_HEIGHT_M, TARGET_LENGTH_M), np.float32)
    if not np.allclose(dimensions, expected, rtol=0.0, atol=0.02):
        raise ValueError(f"A350 bounds {dimensions.tolist()} do not match {expected.tolist()}")
    base.write_kit(AIRCRAFT, BASENAME, kit)
    print(f"AIR-009 ready: {BASENAME} ({dimensions[0]:.2f} x {dimensions[1]:.2f} x {dimensions[2]:.2f} m)")


if __name__ == "__main__":
    main()
