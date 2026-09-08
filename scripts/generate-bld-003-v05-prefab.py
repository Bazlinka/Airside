#!/usr/bin/env python3
"""Emit Resources prefab for BLD-003 operations shed v05."""

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
        "mdl_operations_shed_v05",
        "a1b2c3d4e5f60718293a4b5c6d7e8f90",
        [
            ("shed_body", (0.0, 1.4, 0.0), (6.0, 2.8, 4.0), "cube"),
            ("porch", (0.0, 1.05, 2.4), (3.25, 2.1, 1.45), "cube"),
            ("porch_roof", (0.0, 2.3, 2.55), (3.7, 0.16, 1.75), "cube"),
            ("porch_post_l", (-1.35, 1.05, 3.05), (0.18, 2.0, 0.18), "cylinder"),
            ("porch_post_r", (1.35, 1.05, 3.05), (0.18, 2.0, 0.18), "cylinder"),
            ("door", (0.0, 1.0, 2.95), (1.1, 1.9, 0.1), "cube"),
            ("door_frame", (0.0, 1.0, 3.0), (1.25, 2.05, 0.06), "cube"),
            ("window_l", (-1.8, 1.6, 2.0), (1.0, 0.9, 0.05), "cube"),
            ("window_r", (1.8, 1.6, 2.0), (1.0, 0.9, 0.05), "cube"),
            ("glass_pane_1", (-2.05, 1.85, 2.02), (0.42, 0.42, 0.05), "cube"),
            ("glass_pane_2", (-1.55, 1.85, 2.02), (0.42, 0.42, 0.05), "cube"),
            ("roof_panel_l", (-1.6, 3.05, 0.0), (3.35, 0.14, 4.15), "cube", (0.0, 0.0, 14.0)),
            ("roof_panel_r", (1.6, 3.05, 0.0), (3.35, 0.14, 4.15), "cube", (0.0, 0.0, -14.0)),
            ("roof_ridge", (0.0, 3.45, 0.0), (0.42, 0.22, 4.25), "cube"),
            ("roof_ridge_cap", (0.0, 3.55, 0.0), (0.2, 0.2, 4.1), "cylinder", (90.0, 0.0, 0.0)),
            ("gable_apex_front", (0.0, 3.4, 2.05), (0.28, 0.28, 0.16), "cube"),
            ("antenna_mast", (1.85, 3.95, -0.45), (0.11, 1.55, 0.11), "cylinder"),
            ("antenna_dish", (1.85, 4.65, -0.45), (0.56, 0.1, 0.56), "cylinder"),
            ("ac_unit", (-1.55, 3.35, -0.75), (1.35, 0.5, 1.0), "cube"),
            ("radio_rack", (-2.25, 1.45, -1.65), (0.9, 1.75, 0.55), "cube"),
            ("signage", (0.0, 2.65, 2.15), (1.8, 0.4, 0.09), "cube"),
            ("plinth", (0.0, 0.1, 0.0), (6.35, 0.22, 4.35), "cube"),
            ("interior_glow", (0.0, 1.55, 1.7), (3.2, 1.0, 0.06), "cube"),
            ("step", (0.0, 0.16, 3.15), (1.55, 0.28, 0.6), "cube"),
        ],
        base=(0.55, 0.58, 0.52),
        accent=(0.48, 0.5, 0.46),
        step=(0.35, 0.38, 0.34),
    )
    print("Wrote mdl_operations_shed_v05 Resources prefab")


if __name__ == "__main__":
    main()
