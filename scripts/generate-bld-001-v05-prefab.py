#!/usr/bin/env python3
"""Emit Resources prefab for Batch F1 BLD-001 v05 regional terminal.

Pipeline-proof Cube/Cylinder hierarchy so airside-prefab/mdl_terminal_regional_small_v05
resolves. ArtPresentationLoader yields to StreamingAssets glTF until Mac FBX bake.
"""

from __future__ import annotations

import importlib.util
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
_SPEC = importlib.util.spec_from_file_location(
    "authored_prefabs", REPO / "scripts/generate-authored-resources-prefabs.py"
)
_mod = importlib.util.module_from_spec(_SPEC)
assert _SPEC.loader is not None
# Prefer local OUT_DIR over /workspace
_SPEC.loader.exec_module(_mod)
_mod.OUT_DIR = REPO / "game/Airside/Assets/Resources/Airside/Prefabs"
_mod.OUT_DIR.mkdir(parents=True, exist_ok=True)


def main() -> None:
    # Stable unique GUID — must not collide with hangar authored prefab.
    guid = "a7e2c91f4b6d48e0a3f5c8d1e9b02467"
    _mod.emit_prefab(
        "mdl_terminal_regional_small_v05",
        guid,
        [
            ("terminal_body", (0.0, 2.05, 0.15), (20.2, 3.85, 4.2), "cube"),
            ("plinth", (0.0, 0.16, 0.05), (21.0, 0.32, 4.9), "cube"),
            ("roof_panel_l", (-5.2, 4.35, 0.05), (10.6, 0.16, 5.15), "cube", (8.0, 0.0, 0.0)),
            ("roof_panel_r", (5.2, 4.35, 0.05), (10.6, 0.16, 5.15), "cube", (-8.0, 0.0, 0.0)),
            ("roof_ridge", (0.0, 4.72, 0.05), (0.55, 0.18, 5.0), "cube"),
            ("roof_plant", (-4.6, 4.95, -0.55), (2.4, 0.55, 1.5), "cube"),
            ("roof_vent_a", (-2.2, 5.15, 0.7), (0.48, 0.5, 0.48), "cylinder"),
            ("end_cap_left", (-10.7, 2.0, 0.0), (1.0, 3.85, 4.85), "cube"),
            ("end_cap_right", (10.7, 2.0, 0.0), (1.0, 3.85, 4.85), "cube"),
            ("glass_front", (0.0, 2.3, -2.32), (16.2, 2.35, 0.08), "cube"),
            ("window_mullion_1", (-6.0, 2.3, -2.42), (0.11, 2.45, 0.16), "cube"),
            ("window_mullion_2", (-3.0, 2.3, -2.42), (0.11, 2.45, 0.16), "cube"),
            ("window_mullion_3", (0.0, 2.3, -2.42), (0.11, 2.45, 0.16), "cube"),
            ("window_mullion_4", (3.0, 2.3, -2.42), (0.11, 2.45, 0.16), "cube"),
            ("window_mullion_5", (6.0, 2.3, -2.42), (0.11, 2.45, 0.16), "cube"),
            ("entrance", (0.0, 1.3, -2.35), (2.5, 2.35, 0.1), "cube"),
            ("landside_glass", (0.0, 2.15, 2.4), (12.2, 1.75, 0.08), "cube"),
            ("canopy", (0.0, 3.5, -3.25), (14.4, 0.14, 2.35), "cube"),
            ("canopy_post_l", (-6.6, 1.65, -3.75), (0.22, 3.25, 0.22), "cylinder"),
            ("canopy_post_r", (6.6, 1.65, -3.75), (0.22, 3.25, 0.22), "cylinder"),
            ("canopy_post_ml", (-2.2, 1.65, -3.75), (0.22, 3.25, 0.22), "cylinder"),
            ("canopy_post_mr", (2.2, 1.65, -3.75), (0.22, 3.25, 0.22), "cylinder"),
            ("service_wing", (7.6, 1.3, 2.85), (7.6, 2.5, 2.85), "cube"),
            ("service_door", (9.6, 1.05, 4.2), (1.55, 1.95, 0.09), "cube"),
            ("baggage_door", (5.5, 0.95, 4.2), (2.45, 1.75, 0.09), "cube"),
            ("signage_bar", (0.0, 3.85, -2.38), (10.2, 0.32, 0.18), "cube"),
            ("column_l", (-8.4, 1.95, -1.45), (0.4, 3.7, 0.4), "cylinder"),
            ("column_r", (8.4, 1.95, -1.45), (0.4, 3.7, 0.4), "cylinder"),
        ],
        base=(0.7, 0.74, 0.76),
        accent=(0.15, 0.36, 0.52),
        step=(0.55, 0.58, 0.6),
    )
    print("Wrote mdl_terminal_regional_small_v05.prefab")


if __name__ == "__main__":
    main()
