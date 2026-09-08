#!/usr/bin/env python3
"""Emit mdl_passenger_stairs_v02 Resources prefab — denser pipeline-proof stairs.

Companion to generate-prp-001-v03.py. ArtPresentationLoader yields to
StreamingAssets glTF when present; this prefab covers Editor/Addressables
probe until Mac FBX bake.
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
_SPEC.loader.exec_module(_mod)
_mod.OUT_DIR = REPO / "game/Airside/Assets/Resources/Airside/Prefabs"
_mod.OUT_DIR.mkdir(parents=True, exist_ok=True)

xcyl = (0.0, 0.0, 90.0)
zcyl = (90.0, 0.0, 0.0)


def main() -> None:
    steps = []
    for i in range(8):
        y = 0.35 + i * 0.28
        z = -1.4 + i * 0.42
        steps.append((f"Step {i}", (0.0, y, z), (1.2, 0.07, 0.3), "cube"))
        steps.append((f"Nosing {i}", (0.0, y + 0.04, z + 0.12), (1.22, 0.035, 0.07), "cube"))

    parts = [
        ("Stairs base", (0.0, 0.12, 0.0), (1.45, 0.2, 3.8), "cube"),
        ("Stairs bumper", (0.0, 0.28, -1.75), (1.45, 0.12, 0.3), "cube"),
        ("Stairs side L", (-0.72, 1.35, 0.1), (0.06, 2.3, 3.4), "cube"),
        ("Stairs side R", (0.72, 1.35, 0.1), (0.06, 2.3, 3.4), "cube"),
        ("Stairs rail L", (-0.72, 1.55, 0.1), (0.08, 0.08, 3.5), "cylinder", zcyl),
        ("Stairs rail R", (0.72, 1.55, 0.1), (0.08, 0.08, 3.5), "cylinder", zcyl),
        ("Stairs rail mid", (0.0, 1.85, 1.4), (1.3, 0.06, 0.06), "cylinder", xcyl),
        ("Stairs platform", (0.0, 2.45, 1.7), (1.35, 0.1, 0.95), "cube"),
        ("Stairs canopy", (0.0, 2.95, 1.45), (1.4, 0.06, 1.35), "cube"),
        ("Stairs canopy L", (-0.65, 2.7, 1.45), (0.06, 0.45, 1.25), "cube"),
        ("Stairs canopy R", (0.65, 2.7, 1.45), (0.06, 0.45, 1.25), "cube"),
        ("Stairs brace", (0.0, 0.55, 0.2), (1.3, 0.08, 0.08), "cube"),
        ("Stairs wheel FL", (-0.6, 0.18, -1.4), (0.14, 0.28, 0.28), "cylinder", xcyl),
        ("Stairs wheel FR", (0.6, 0.18, -1.4), (0.14, 0.28, 0.28), "cylinder", xcyl),
        ("Stairs wheel RL", (-0.6, 0.18, 1.4), (0.14, 0.28, 0.28), "cylinder", xcyl),
        ("Stairs wheel RR", (0.6, 0.18, 1.4), (0.14, 0.28, 0.28), "cylinder", xcyl),
        ("Stairs hub FL", (-0.6, 0.18, -1.4), (0.08, 0.12, 0.12), "cylinder", xcyl),
        ("Stairs hub FR", (0.6, 0.18, -1.4), (0.08, 0.12, 0.12), "cylinder", xcyl),
        *steps,
    ]
    _mod.emit_prefab(
        "mdl_passenger_stairs_v02",
        "e2f3a4b5c6d70819203b4c5d6e7f8091",
        parts,
        base=(0.7, 0.72, 0.74),
        accent=(0.92, 0.72, 0.12),
        step=(0.55, 0.56, 0.58),
    )
    print("wrote mdl_passenger_stairs_v02.prefab")


if __name__ == "__main__":
    main()
