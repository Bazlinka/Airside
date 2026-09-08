#!/usr/bin/env python3
"""Emit Resources prefab for BLD-002 hangar v05."""

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
        "mdl_hangar_small_v05",
        "f6a7b8c9d0e14c2345678905f0123456",
        [
            ("hangar_shell", (0.0, 2.5, 0.0), (14.0, 5.0, 9.0), "cube"),
            ("roof_panel_l", (-3.55, 4.55, 0.0), (7.4, 0.18, 9.3), "cube", (0.0, 0.0, 16.0)),
            ("roof_panel_r", (3.55, 4.55, 0.0), (7.4, 0.18, 9.3), "cube", (0.0, 0.0, -16.0)),
            ("roof_ridge", (0.0, 5.45, 0.0), (0.55, 0.28, 9.4), "cube"),
            ("roof_ridge_cap", (0.0, 5.55, 0.0), (0.28, 0.28, 9.2), "cylinder", (90.0, 0.0, 0.0)),
            ("door_opening", (0.0, 2.2, 4.6), (9.5, 4.2, 0.15), "cube"),
            ("door_panel_l", (-2.4, 2.0, 4.7), (4.6, 3.9, 0.12), "cube"),
            ("door_panel_r", (2.4, 2.0, 4.7), (4.6, 3.9, 0.12), "cube"),
            ("door_bar_l1", (-2.4, 1.2, 4.76), (4.4, 0.08, 0.06), "cube"),
            ("door_bar_r1", (2.4, 1.2, 4.76), (4.4, 0.08, 0.06), "cube"),
            ("door_track_mid", (0.0, 4.45, 4.58), (9.8, 0.16, 0.28), "cube"),
            ("door_header", (0.0, 4.15, 4.65), (9.6, 0.22, 0.18), "cube"),
            ("buttress_l", (-7.2, 1.5, 2.5), (1.0, 3.0, 2.5), "cube"),
            ("buttress_r", (7.2, 1.5, 2.5), (1.0, 3.0, 2.5), "cube"),
            ("personnel_door", (-5.5, 1.1, 4.65), (1.1, 2.1, 0.1), "cube"),
            ("office_lean", (5.9, 1.45, -3.45), (3.6, 2.7, 3.15), "cube"),
            ("office_window", (5.9, 1.8, -5.05), (2.2, 1.2, 0.08), "cube"),
            ("office_roof", (5.9, 2.95, -3.45), (3.7, 0.14, 3.25), "cube", (0.0, 0.0, -6.0)),
            ("crane_beam", (0.0, 4.4, 0.0), (12.0, 0.2, 0.35), "cube"),
            ("column_l", (-6.2, 2.4, -2.0), (0.4, 4.6, 0.4), "cylinder"),
            ("column_r", (6.2, 2.4, -2.0), (0.4, 4.6, 0.4), "cylinder"),
            ("skylight_l", (-3.0, 5.15, -1.2), (2.2, 0.05, 1.3), "cube", (0.0, 0.0, 16.0)),
            ("skylight_r", (3.0, 5.15, -1.2), (2.2, 0.05, 1.3), "cube", (0.0, 0.0, -16.0)),
            ("gable_apex_front", (0.0, 5.35, 4.55), (0.35, 0.35, 0.2), "cube"),
            ("plinth", (0.0, 0.12, 0.0), (14.4, 0.24, 9.4), "cube"),
        ],
        base=(0.45, 0.5, 0.54),
        accent=(0.22, 0.24, 0.26),
        step=(0.4, 0.44, 0.48),
    )
    print("Wrote mdl_hangar_small_v05 Resources prefab")


if __name__ == "__main__":
    main()
