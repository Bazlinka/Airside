#!/usr/bin/env python3
"""Emit Resources prefab for PRP-002 fence/gate kit v02."""

from __future__ import annotations

import importlib.util
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
_SPEC = importlib.util.spec_from_file_location(
    "authored_prefabs", REPO / "scripts/generate-authored-resources-prefabs.py"
)
_mod = importlib.util.module_from_spec(_SPEC)
assert _SPEC.loader is not None
_SPEC.loader.exec_module(_mod)
_mod.OUT_DIR = REPO / "game/Airside/Assets/Resources/Airside/Prefabs"
_mod.OUT_DIR.mkdir(parents=True, exist_ok=True)


def main() -> None:
    _mod.emit_prefab(
        "mdl_airfield_fence_gate_kit_v02",
        "d6e7f8a9b0c149567890abcdef101234",
        [
            ("fence_bay", (0.0, 0.75, 0.0), (3.85, 1.25, 0.06), "cube"),
            ("fence_bay_rail_top", (0.0, 1.38, 0.0), (3.95, 0.08, 0.08), "cylinder", (0.0, 0.0, 90.0)),
            ("fence_bay_rail_mid", (0.0, 0.75, 0.0), (3.9, 0.06, 0.06), "cylinder", (0.0, 0.0, 90.0)),
            ("fence_bay_rail_bot", (0.0, 0.22, 0.0), (3.9, 0.06, 0.06), "cylinder", (0.0, 0.0, 90.0)),
            ("fence_bay_post_l", (-1.98, 0.75, 0.0), (0.14, 1.5, 0.14), "cylinder"),
            ("fence_bay_post_r", (1.98, 0.75, 0.0), (0.14, 1.5, 0.14), "cylinder"),
            ("fence_bay_cap_l", (-1.98, 1.52, 0.0), (0.18, 0.08, 0.18), "cylinder"),
            ("fence_bay_cap_r", (1.98, 1.52, 0.0), (0.18, 0.08, 0.18), "cylinder"),
            ("fence_corner", (5.0, 0.78, 0.0), (0.2, 1.55, 0.2), "cylinder"),
            ("gate_vehicle_leaf_l", (8.0, 0.85, 0.0), (2.15, 1.45, 0.08), "cube"),
            ("gate_vehicle_leaf_r", (10.5, 0.85, 0.0), (2.15, 1.45, 0.08), "cube"),
            ("gate_post", (12.5, 0.95, 0.0), (0.22, 1.9, 0.22), "cylinder"),
            ("gate_sign", (14.5, 2.05, 0.0), (1.7, 0.55, 0.06), "cube"),
            ("gate_vehicle_chevron", (8.0, 0.95, 0.08), (1.7, 0.28, 0.04), "cube"),
            ("gate_latch", (11.2, 0.95, 0.0), (0.4, 0.2, 0.14), "cube"),
        ],
        base=(0.62, 0.64, 0.66),
        accent=(0.95, 0.76, 0.29),
        step=(0.55, 0.56, 0.58),
    )
    print("Wrote mdl_airfield_fence_gate_kit_v02 Resources prefab")


if __name__ == "__main__":
    main()
