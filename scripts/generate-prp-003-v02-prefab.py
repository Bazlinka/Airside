#!/usr/bin/env python3
"""Emit Resources prefab for PRP-003 terminal forecourt kit v02."""

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
        "mdl_terminal_forecourt_kit_v02",
        "a1b2c3d4e5f607182930aabbccddeeff",
        [
            ("kerb_straight", (0.0, 0.12, 0.0), (4.0, 0.22, 0.35), "cube"),
            ("kerb_corner", (5.0, 0.12, 0.0), (0.85, 0.22, 0.85), "cube"),
            ("bollard", (7.0, 0.45, 0.0), (0.22, 0.9, 0.22), "cylinder"),
            ("bollard_cap", (7.0, 0.95, 0.0), (0.26, 0.1, 0.26), "cylinder"),
            ("planter", (9.5, 0.35, 0.0), (1.5, 0.55, 1.1), "cube"),
            ("planter_soil", (9.5, 0.58, 0.0), (1.2, 0.1, 0.8), "cube"),
            ("planter_scrub", (9.5, 0.9, 0.0), (1.0, 0.55, 0.8), "cube"),
            ("bench_seat", (13.0, 0.4, 0.0), (2.4, 0.12, 0.55), "cube"),
            ("bench_back", (13.0, 0.7, -0.22), (2.4, 0.5, 0.08), "cube"),
            ("bench_leg_l", (12.05, 0.2, 0.0), (0.12, 0.4, 0.45), "cube"),
            ("bench_leg_r", (13.95, 0.2, 0.0), (0.12, 0.4, 0.45), "cube"),
            ("sign_post", (16.5, 1.1, 0.0), (0.12, 2.2, 0.12), "cylinder"),
            ("sign_face", (16.5, 2.0, 0.0), (0.08, 0.7, 0.9), "cube"),
            ("sign_frame", (16.5, 2.0, -0.03), (0.06, 0.85, 1.0), "cube"),
            ("dropoff_bollard", (18.5, 0.4, 0.0), (0.2, 0.82, 0.2), "cylinder"),
            ("trolley_rail", (21.0, 0.55, 0.0), (1.8, 0.1, 0.1), "cube"),
            ("trolley_post_l", (20.15, 0.4, 0.0), (0.1, 0.8, 0.1), "cube"),
            ("trolley_post_r", (21.85, 0.4, 0.0), (0.1, 0.8, 0.1), "cube"),
        ],
        base=(0.61, 0.64, 0.63),
        accent=(0.95, 0.76, 0.29),
        step=(0.4, 0.32, 0.22),
    )
    print("Wrote mdl_terminal_forecourt_kit_v02 Resources prefab")


if __name__ == "__main__":
    main()
