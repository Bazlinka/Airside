#!/usr/bin/env python3
"""Emit Resources prefabs for Batch F3 setting modules."""

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
        "mdl_eucalyptus_kit_v01",
        "b3c4d5e6f7a84901234567890abcd100",
        [
            ("tree_a_trunk", (0.0, 1.55, 0.0), (0.22, 3.1, 0.22), "cylinder"),
            ("tree_a_flare", (0.0, 0.1, 0.0), (0.44, 0.18, 0.44), "cylinder"),
            ("tree_a_canopy", (0.0, 3.25, 0.0), (1.9, 1.4, 1.8), "cube"),
            ("tree_a_canopy_b", (0.55, 2.75, -0.35), (1.3, 1.0, 1.2), "cube"),
            ("tree_b_trunk", (3.0, 1.3, 0.0), (0.2, 2.6, 0.2), "cylinder"),
            ("tree_b_canopy", (3.0, 2.85, 0.0), (1.7, 1.2, 1.6), "cube"),
            ("tree_c_trunk", (6.0, 1.75, 0.0), (0.24, 3.5, 0.24), "cylinder"),
            ("tree_c_canopy", (6.0, 3.6, 0.0), (2.1, 1.5, 2.0), "cube"),
        ],
        base=(0.32, 0.24, 0.15),
        accent=(0.31, 0.44, 0.38),
        step=(0.45, 0.42, 0.28),
    )
    _mod.emit_prefab(
        "mdl_kingscote_scrub_kit_v01",
        "c4d5e6f7a8b9491234567890abcd1011",
        [
            ("scrub_a_core", (0.0, 0.38, 0.0), (1.3, 0.8, 1.1), "cube"),
            ("scrub_a_side", (0.4, 0.32, -0.25), (0.9, 0.6, 0.8), "cube"),
            ("scrub_b_core", (2.5, 0.32, 0.0), (1.1, 0.7, 0.95), "cube"),
            ("rock_a", (5.0, 0.18, 0.0), (0.7, 0.4, 0.55), "cube"),
            ("grass_tuft_a", (6.5, 0.2, 0.0), (0.35, 0.4, 0.12), "cube"),
        ],
        base=(0.54, 0.54, 0.35),
        accent=(0.31, 0.44, 0.38),
        step=(0.45, 0.4, 0.32),
    )
    _mod.emit_prefab(
        "mdl_airfield_fence_gate_kit_v01",
        "d5e6f7a8b9c049234567890abcd1012",
        [
            ("fence_bay", (0.0, 0.7, 0.0), (4.0, 1.35, 0.06), "cube"),
            ("fence_bay_post_l", (-2.0, 0.7, 0.0), (0.12, 1.4, 0.12), "cube"),
            ("fence_bay_post_r", (2.0, 0.7, 0.0), (0.12, 1.4, 0.12), "cube"),
            ("fence_corner", (5.0, 0.7, 0.0), (0.18, 1.45, 0.18), "cube"),
            ("gate_vehicle_leaf_l", (8.0, 0.85, 0.0), (2.2, 1.5, 0.08), "cube"),
            ("gate_vehicle_leaf_r", (10.5, 0.85, 0.0), (2.2, 1.5, 0.08), "cube"),
            ("gate_post", (12.5, 0.9, 0.0), (0.22, 1.8, 0.22), "cube"),
            ("gate_sign", (14.5, 2.0, 0.0), (1.6, 0.55, 0.06), "cube"),
        ],
        base=(0.62, 0.64, 0.66),
        accent=(0.95, 0.76, 0.29),
        step=(0.55, 0.56, 0.58),
    )
    _mod.emit_prefab(
        "mdl_terminal_forecourt_kit_v01",
        "e6f7a8b9c0d14934567890abcd1013",
        [
            ("kerb_straight", (0.0, 0.12, 0.0), (4.0, 0.22, 0.35), "cube"),
            ("bollard", (5.0, 0.45, 0.0), (0.2, 0.9, 0.2), "cylinder"),
            ("planter", (7.0, 0.35, 0.0), (1.4, 0.5, 1.0), "cube"),
            ("bench_seat", (10.0, 0.4, 0.0), (2.4, 0.12, 0.55), "cube"),
            ("sign_post", (13.0, 1.1, 0.0), (0.12, 2.2, 0.12), "cylinder"),
            ("sign_face", (13.0, 2.0, 0.0), (0.08, 0.7, 0.9), "cube"),
        ],
        base=(0.61, 0.64, 0.63),
        accent=(0.95, 0.76, 0.29),
        step=(0.4, 0.32, 0.22),
    )
    _mod.emit_prefab(
        "mdl_kingscote_context_terrain_v01",
        "f7a8b9c0d1e2494567890abcd1014",
        [
            ("paddock_n", (0.0, -0.4, 0.0), (40.0, 0.6, 16.0), "cube"),
            ("paddock_s", (0.0, -0.4, 20.0), (40.0, 0.6, 14.0), "cube"),
            ("coast_sand", (0.0, -0.2, 40.0), (50.0, 0.25, 8.0), "cube"),
            ("hill_a", (0.0, 1.2, 55.0), (16.0, 4.4, 10.0), "cube"),
            ("dune_a", (0.0, 0.45, 70.0), (10.0, 1.4, 5.0), "cube"),
        ],
        base=(0.54, 0.54, 0.35),
        accent=(0.78, 0.7, 0.53),
        step=(0.22, 0.42, 0.58),
    )
    print("Wrote Batch F3 Resources prefabs")


if __name__ == "__main__":
    main()
