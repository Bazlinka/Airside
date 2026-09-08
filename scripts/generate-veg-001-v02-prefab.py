#!/usr/bin/env python3
"""Emit Resources prefab for VEG-001 eucalyptus kit v02."""

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
        "mdl_eucalyptus_kit_v02",
        "c8d9e0f1a2b3456789abcdef01234567",
        [
            ("tree_a_trunk", (0.0, 1.55, 0.0), (0.24, 3.1, 0.24), "cylinder"),
            ("tree_a_flare", (0.0, 0.1, 0.0), (0.55, 0.22, 0.55), "cylinder"),
            ("tree_a_bark_low", (0.0, 0.87, 0.0), (0.3, 0.12, 0.3), "cube"),
            ("tree_a_bark_mid", (0.0, 1.7, 0.0), (0.28, 0.1, 0.28), "cube"),
            ("tree_a_fork", (0.25, 2.4, -0.12), (0.14, 0.9, 0.14), "cylinder"),
            ("tree_a_canopy", (0.0, 3.25, 0.0), (2.0, 1.5, 1.9), "cube"),
            ("tree_a_canopy_b", (0.55, 2.75, -0.35), (1.4, 1.1, 1.3), "cube"),
            ("tree_a_canopy_c", (-0.45, 2.85, 0.4), (1.2, 1.0, 1.15), "cube"),
            ("tree_a_canopy_d", (0.25, 3.45, 0.25), (1.0, 0.85, 0.95), "cube"),
            ("tree_a_lod1", (0.0, 2.6, 0.0), (2.3, 3.0, 2.2), "cube"),
            ("tree_b_trunk", (4.0, 1.3, 0.0), (0.22, 2.6, 0.22), "cylinder"),
            ("tree_b_flare", (4.0, 0.1, 0.0), (0.5, 0.2, 0.5), "cylinder"),
            ("tree_b_canopy", (4.0, 2.85, 0.0), (1.8, 1.3, 1.7), "cube"),
            ("tree_b_canopy_b", (4.5, 2.4, -0.3), (1.2, 0.95, 1.1), "cube"),
            ("tree_c_trunk", (8.0, 1.75, 0.0), (0.26, 3.5, 0.26), "cylinder"),
            ("tree_c_flare", (8.0, 0.1, 0.0), (0.58, 0.22, 0.58), "cylinder"),
            ("tree_c_canopy", (8.0, 3.65, 0.0), (2.2, 1.6, 2.1), "cube"),
            ("tree_c_canopy_b", (8.6, 3.1, -0.4), (1.5, 1.15, 1.35), "cube"),
        ],
        base=(0.32, 0.24, 0.15),
        accent=(0.31, 0.44, 0.38),
        step=(0.45, 0.42, 0.28),
    )
    print("Wrote mdl_eucalyptus_kit_v02 Resources prefab")


if __name__ == "__main__":
    main()
