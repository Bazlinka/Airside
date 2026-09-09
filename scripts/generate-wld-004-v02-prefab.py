#!/usr/bin/env python3
"""Emit Resources prefab for WLD-004 Kingscote context terrain v02."""

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
        "mdl_kingscote_context_terrain_v02",
        "b2c3d4e5f60718293a4b5c6d7e8f901a",
        [
            ("paddock_n", (0.0, -0.4, 0.0), (40.0, 0.6, 16.0), "cube"),
            ("paddock_s", (0.0, -0.4, 20.0), (40.0, 0.6, 14.0), "cube"),
            ("paddock_e", (25.0, -0.4, 10.0), (14.0, 0.6, 30.0), "cube"),
            ("paddock_w", (-25.0, -0.4, 10.0), (14.0, 0.6, 30.0), "cube"),
            ("coast_sand", (0.0, -0.2, 40.0), (50.0, 0.25, 8.0), "cube"),
            ("coast_shallows", (0.0, -0.45, 48.0), (52.0, 0.15, 8.0), "cube"),
            ("coast_water", (0.0, -0.55, 58.0), (55.0, 0.12, 12.0), "cube"),
            ("hill_a", (0.0, 1.2, 75.0), (16.0, 4.4, 10.0), "cube"),
            ("hill_b", (18.0, 0.9, 72.0), (12.0, 3.2, 8.0), "cube"),
            ("hill_c", (-20.0, 1.5, 78.0), (20.0, 5.6, 12.0), "cube"),
            ("dune_a", (0.0, 0.45, 52.0), (10.0, 1.4, 5.0), "cube"),
            ("dune_b", (12.0, 0.35, 50.0), (8.0, 1.1, 4.0), "cube"),
            ("berm", (0.0, 0.35, 30.0), (12.0, 0.7, 2.5), "cube"),
        ],
        base=(0.54, 0.54, 0.35),
        accent=(0.78, 0.7, 0.53),
        step=(0.22, 0.42, 0.58),
    )
    print("Wrote mdl_kingscote_context_terrain_v02 Resources prefab")


if __name__ == "__main__":
    main()
