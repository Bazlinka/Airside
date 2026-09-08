#!/usr/bin/env python3
"""Emit Resources prefabs for CHR-001/002 character kits v02."""

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


def emit_crew() -> None:
    parts = []
    for i, role in enumerate(("marshaller", "fueler", "ramp")):
        ox = i * 2.4
        parts += [
            (f"{role}_torso", (ox, 0.95, 0.0), (0.4, 0.9, 0.24), "cube"),
            (f"{role}_head", (ox, 1.72, 0.0), (0.24, 0.22, 0.24), "cylinder"),
            (f"{role}_leg_l", (ox - 0.1, 0.4, 0.0), (0.14, 0.75, 0.14), "cylinder"),
            (f"{role}_leg_r", (ox + 0.1, 0.4, 0.0), (0.14, 0.75, 0.14), "cylinder"),
            (f"{role}_arm_l", (ox - 0.3, 0.95, 0.0), (0.12, 0.6, 0.12), "cylinder"),
            (f"{role}_arm_r", (ox + 0.3, 0.95, 0.0), (0.12, 0.6, 0.12), "cylinder"),
            (f"{role}_vest", (ox, 0.98, 0.14), (0.38, 0.45, 0.05), "cube"),
            (f"{role}_hat", (ox, 1.86, 0.0), (0.28, 0.12, 0.28), "cylinder"),
            (f"{role}_shoe_l", (ox - 0.1, 0.05, 0.06), (0.15, 0.08, 0.26), "cube"),
            (f"{role}_shoe_r", (ox + 0.1, 0.05, 0.06), (0.15, 0.08, 0.26), "cube"),
        ]
        if role == "marshaller":
            parts += [
                (f"{role}_wand", (ox + 0.42, 1.2, 0.05), (0.05, 0.55, 0.05), "cylinder"),
                (f"{role}_wand_tip", (ox + 0.42, 1.52, 0.05), (0.09, 0.09, 0.09), "cube"),
            ]
    _mod.emit_prefab(
        "mdl_ramp_crew_kit_v02",
        "b7c8d9e0f1a2494567890abcdef01234",
        parts,
        base=(0.95, 0.72, 0.12),
        accent=(0.95, 0.2, 0.15),
        step=(0.25, 0.28, 0.32),
    )


def emit_passengers() -> None:
    parts = []
    variants = [
        ("stand_a", False),
        ("stand_b", False),
        ("walk_c", False),
        ("walk_d", False),
        ("sit_e", True),
        ("sit_f", True),
    ]
    for i, (prefix, seated) in enumerate(variants):
        ox = i * 2.2
        body_h = 0.55 if seated else 0.9
        body_y = 0.55 if seated else 0.95
        parts += [
            (f"{prefix}_torso", (ox, body_y, 0.0), (0.4, body_h, 0.24), "cube"),
            (f"{prefix}_head", (ox, body_y + body_h * 0.52 + 0.18, 0.0), (0.24, 0.22, 0.24), "cylinder"),
        ]
        if seated:
            parts += [
                (f"{prefix}_legs", (ox, 0.3, 0.22), (0.42, 0.22, 0.58), "cube"),
                (f"{prefix}_arm_l", (ox - 0.28, body_y, 0.08), (0.12, 0.12, 0.4), "cylinder"),
                (f"{prefix}_arm_r", (ox + 0.28, body_y, 0.08), (0.12, 0.12, 0.4), "cylinder"),
            ]
        else:
            parts += [
                (f"{prefix}_leg_l", (ox - 0.1, 0.4, 0.0), (0.14, 0.75, 0.14), "cylinder"),
                (f"{prefix}_leg_r", (ox + 0.1, 0.4, 0.0), (0.14, 0.75, 0.14), "cylinder"),
                (f"{prefix}_arm_l", (ox - 0.3, body_y, 0.0), (0.12, 0.6, 0.12), "cylinder"),
                (f"{prefix}_arm_r", (ox + 0.3, body_y, 0.0), (0.12, 0.6, 0.12), "cylinder"),
                (f"{prefix}_shoe_l", (ox - 0.1, 0.05, 0.06), (0.15, 0.08, 0.26), "cube"),
                (f"{prefix}_shoe_r", (ox + 0.1, 0.05, 0.06), (0.15, 0.08, 0.26), "cube"),
            ]
    _mod.emit_prefab(
        "mdl_passenger_kit_v02",
        "c8d9e0f1a2b349567890abcdef012345",
        parts,
        base=(0.45, 0.28, 0.25),
        accent=(0.2, 0.35, 0.4),
        step=(0.3, 0.32, 0.35),
    )


def main() -> None:
    emit_crew()
    emit_passengers()
    print("Wrote CHR kit v02 Resources prefabs")


if __name__ == "__main__":
    main()
