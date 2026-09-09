#!/usr/bin/env python3
"""Emit Resources prefab for VEG-002 Kingscote scrub kit v02."""

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
        "mdl_kingscote_scrub_kit_v02",
        "a1b2c3d4e5f60718293a4b5c6d7e8f90",
        [
            ("scrub_a_core", (0.0, 0.38, 0.0), (1.3, 0.8, 1.1), "cube"),
            ("scrub_a_side", (0.4, 0.32, -0.25), (0.9, 0.6, 0.8), "cube"),
            ("scrub_a_side_b", (-0.35, 0.3, 0.2), (0.8, 0.56, 0.7), "cube"),
            ("scrub_a_tuft", (0.1, 0.55, 0.15), (0.56, 0.44, 0.5), "cube"),
            ("scrub_b_core", (2.5, 0.32, 0.0), (1.1, 0.7, 0.95), "cube"),
            ("scrub_b_side", (2.85, 0.27, -0.2), (0.75, 0.5, 0.68), "cube"),
            ("scrub_c_core", (5.0, 0.42, 0.0), (1.4, 0.88, 1.2), "cube"),
            ("scrub_c_side", (5.45, 0.35, -0.28), (1.0, 0.66, 0.88), "cube"),
            ("scrub_d_core", (7.5, 0.28, 0.0), (0.98, 0.6, 0.82), "cube"),
            ("scrub_e_core", (9.5, 0.36, 0.0), (1.24, 0.76, 1.05), "cube"),
            ("rock_a", (11.5, 0.18, 0.0), (0.7, 0.4, 0.55), "cube"),
            ("rock_b", (12.5, 0.14, 0.0), (0.56, 0.3, 0.44), "cube"),
            ("rock_c", (13.5, 0.18, 0.0), (0.7, 0.4, 0.58), "cube"),
            ("grass_tuft_a", (14.8, 0.2, 0.0), (0.35, 0.4, 0.12), "cube"),
            ("grass_tuft_b", (15.5, 0.18, 0.0), (0.28, 0.35, 0.1), "cube"),
            ("grass_tuft_c", (16.2, 0.22, 0.0), (0.38, 0.44, 0.2), "cube"),
            ("dune_mix_a", (18.5, 0.22, 0.0), (1.8, 0.55, 1.1), "cube"),
            ("dune_mix_b", (21.0, 0.18, 0.0), (1.5, 0.48, 0.95), "cube"),
        ],
        base=(0.54, 0.54, 0.35),
        accent=(0.31, 0.44, 0.38),
        step=(0.45, 0.4, 0.32),
    )
    print("Wrote mdl_kingscote_scrub_kit_v02 Resources prefab")


if __name__ == "__main__":
    main()
