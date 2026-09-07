#!/usr/bin/env python3
"""Emit Resources prefabs for Batch F2 vehicles + character kits.

Pipeline-proof Cube/Cylinder hierarchy so airside-prefab/<key> resolves.
ArtPresentationLoader yields to StreamingAssets glTF until Mac FBX bake.
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

zcyl = (90.0, 0.0, 0.0)
xcyl = (0.0, 0.0, 90.0)


def emit_fuel() -> None:
    _mod.emit_prefab(
        "mdl_fuel_truck_small_v05",
        "b1c2d3e4f5a6478901234567890abcde",
        [
            ("cab", (1.05, 0.95, 0.0), (1.55, 1.55, 1.6), "cube"),
            ("cab_window", (1.62, 1.28, 0.0), (0.04, 0.75, 1.25), "cube"),
            ("cab_door", (1.05, 0.95, 0.82), (1.25, 1.25, 0.08), "cube"),
            ("tank", (-0.5, 0.95, 0.0), (2.65, 1.2, 1.3), "cylinder", xcyl),
            ("chassis", (0.05, 0.34, 0.0), (3.75, 0.24, 1.12), "cube"),
            ("wheel_fl", (1.4, 0.28, 0.6), (0.24, 0.3, 0.3), "cylinder", zcyl),
            ("wheel_fr", (1.4, 0.28, -0.6), (0.24, 0.3, 0.3), "cylinder", zcyl),
            ("wheel_rl", (-1.2, 0.28, 0.6), (0.24, 0.3, 0.3), "cylinder", zcyl),
            ("wheel_rr", (-1.2, 0.28, -0.6), (0.24, 0.3, 0.3), "cylinder", zcyl),
            ("hose", (-1.78, 0.55, 0.58), (0.75, 0.13, 0.13), "cylinder", zcyl),
            ("hose_reel", (-1.62, 0.88, 0.38), (0.38, 0.3, 0.3), "cylinder", zcyl),
            ("hose_nozzle", (-1.95, 0.52, 0.78), (0.28, 0.18, 0.18), "cube"),
            ("pump_cabinet", (-1.62, 0.88, -0.55), (0.58, 0.72, 0.58), "cube"),
            ("beacon", (1.05, 1.92, 0.0), (0.26, 0.2, 0.26), "cube"),
            ("headlight_l", (2.02, 0.68, 0.42), (0.1, 0.14, 0.16), "cube"),
            ("headlight_r", (2.02, 0.68, -0.42), (0.1, 0.14, 0.16), "cube"),
            ("glass_pane_1", (1.64, 1.42, 0.0), (0.04, 0.38, 1.0), "cube"),
            ("cab_stripe", (1.05, 0.68, 0.84), (1.4, 0.12, 0.04), "cube"),
        ],
        base=(0.92, 0.78, 0.18),
        accent=(0.95, 0.85, 0.2),
        step=(0.2, 0.22, 0.25),
    )


def emit_baggage() -> None:
    parts = [
        ("tug", (3.55, 0.52, 0.0), (1.75, 0.9, 1.2), "cube"),
        ("tug_cab", (4.15, 0.98, 0.0), (0.95, 0.9, 1.1), "cube"),
        ("tug_window", (4.58, 1.2, 0.0), (0.04, 0.5, 0.9), "cube"),
        ("wheel_fl", (4.05, 0.22, 0.52), (0.2, 0.22, 0.22), "cylinder", zcyl),
        ("wheel_fr", (4.05, 0.22, -0.52), (0.2, 0.22, 0.22), "cylinder", zcyl),
        ("wheel_rl", (3.15, 0.22, 0.52), (0.2, 0.22, 0.22), "cylinder", zcyl),
        ("wheel_rr", (3.15, 0.22, -0.52), (0.2, 0.22, 0.22), "cylinder", zcyl),
        ("beacon", (4.15, 1.55, 0.0), (0.22, 0.16, 0.22), "cube"),
        ("headlight_l", (4.68, 0.68, 0.38), (0.1, 0.12, 0.14), "cube"),
        ("glass_pane_1", (4.59, 1.3, 0.0), (0.04, 0.3, 0.7), "cube"),
    ]
    for idx, cx in enumerate((1.8, 0.15, -1.5), start=1):
        parts += [
            (f"cart_{idx}", (cx, 0.52, 0.0), (1.55, 0.78, 1.08), "cube"),
            (f"cargo_{idx}", (cx, 0.98, 0.0), (1.22, 0.48, 0.88), "cube"),
            (f"cart_wheel_{idx}l", (cx, 0.18, 0.5), (0.15, 0.17, 0.17), "cylinder", zcyl),
            (f"cart_wheel_{idx}r", (cx, 0.18, -0.5), (0.15, 0.17, 0.17), "cylinder", zcyl),
            (f"hitch_{idx}", (2.7 - (idx - 1) * 1.75, 0.35, 0.0), (0.48, 0.2, 0.22), "cube"),
        ]
    _mod.emit_prefab(
        "mdl_baggage_tug_train_v05",
        "c2d3e4f5a6b748901234567890abcdef",
        parts,
        base=(0.91, 0.38, 0.12),
        accent=(0.95, 0.85, 0.2),
        step=(0.2, 0.22, 0.25),
    )


def emit_bus() -> None:
    _mod.emit_prefab(
        "mdl_passenger_bus_apron_v05",
        "d3e4f5a6b7c84901234567890abcdef0",
        [
            ("bus_body", (0.0, 0.98, 0.0), (4.5, 1.7, 1.7), "cube"),
            ("cabin_roof", (0.0, 1.95, 0.0), (4.3, 0.28, 1.58), "cube"),
            ("nose_round", (2.15, 0.95, 0.0), (1.55, 1.64, 1.64), "cylinder", xcyl),
            ("windshield", (2.28, 1.4, 0.0), (0.05, 0.75, 1.4), "cube"),
            ("door", (0.3, 0.98, 0.88), (1.1, 1.4, 0.1), "cube"),
            ("door_glass", (0.3, 1.28, 0.93), (0.72, 0.58, 0.05), "cube"),
            ("wheel_fl", (1.5, 0.3, 0.75), (0.26, 0.3, 0.3), "cylinder", zcyl),
            ("wheel_fr", (1.5, 0.3, -0.75), (0.26, 0.3, 0.3), "cylinder", zcyl),
            ("wheel_rl", (-1.5, 0.3, 0.75), (0.26, 0.3, 0.3), "cylinder", zcyl),
            ("wheel_rr", (-1.5, 0.3, -0.75), (0.26, 0.3, 0.3), "cylinder", zcyl),
            ("beacon", (0.0, 2.2, 0.0), (0.3, 0.2, 0.3), "cube"),
            ("stripe", (0.0, 0.72, 0.86), (4.1, 0.14, 0.06), "cube"),
            ("headlight_l", (2.42, 0.72, 0.55), (0.12, 0.18, 0.2), "cube"),
            ("glass_pane_1", (0.0, 1.38, 0.9), (3.5, 0.5, 0.04), "cube"),
            ("bumper_front", (2.35, 0.42, 0.0), (0.28, 0.42, 1.55), "cube"),
        ],
        base=(0.17, 0.58, 0.78),
        accent=(0.95, 0.85, 0.2),
        step=(0.2, 0.22, 0.25),
    )


def emit_pushback() -> None:
    _mod.emit_prefab(
        "mdl_pushback_tug_v02",
        "e4f5a6b7c8d0491234567890abcdef01",
        [
            ("tug_body", (0.15, 0.48, 0.0), (2.35, 0.85, 1.25), "cube"),
            ("tug_cab", (0.7, 0.95, 0.0), (0.95, 0.75, 1.1), "cube"),
            ("counterweight", (-0.75, 0.55, 0.0), (0.7, 0.65, 1.05), "cube"),
            ("wheel_fl", (0.75, 0.22, 0.52), (0.2, 0.24, 0.24), "cylinder", zcyl),
            ("wheel_fr", (0.75, 0.22, -0.52), (0.2, 0.24, 0.24), "cylinder", zcyl),
            ("wheel_rl", (-0.65, 0.22, 0.52), (0.2, 0.24, 0.24), "cylinder", zcyl),
            ("wheel_rr", (-0.65, 0.22, -0.52), (0.2, 0.24, 0.24), "cylinder", zcyl),
            ("towbar", (-1.85, 0.28, 0.0), (1.35, 0.12, 0.12), "cube"),
            ("towbar_head", (-2.55, 0.32, 0.0), (0.35, 0.28, 0.35), "cube"),
            ("towbar_wheel", (-2.15, 0.16, 0.0), (0.12, 0.14, 0.14), "cylinder", zcyl),
            ("towbar_handle", (-1.35, 0.48, 0.18), (0.35, 0.08, 0.08), "cube"),
            ("towbar_eye", (-2.72, 0.32, 0.0), (0.08, 0.1, 0.1), "cylinder", zcyl),
            ("beacon", (0.7, 1.45, 0.0), (0.2, 0.15, 0.2), "cube"),
            ("stripe", (0.15, 0.55, 0.64), (2.1, 0.12, 0.04), "cube"),
        ],
        base=(0.82, 0.62, 0.18),
        accent=(0.95, 0.35, 0.12),
        step=(0.2, 0.22, 0.25),
    )


def emit_crew() -> None:
    parts = []
    for i, role in enumerate(("marshaller", "fueler", "ramp")):
        ox = i * 2.5
        parts += [
            (f"{role}_torso", (ox, 0.9, 0.0), (0.38, 0.85, 0.22), "cube"),
            (f"{role}_head", (ox, 1.55, 0.0), (0.22, 0.2, 0.22), "cylinder"),
            (f"{role}_leg_l", (ox - 0.1, 0.35, 0.0), (0.14, 0.7, 0.14), "cylinder"),
            (f"{role}_leg_r", (ox + 0.1, 0.35, 0.0), (0.14, 0.7, 0.14), "cylinder"),
            (f"{role}_arm_l", (ox - 0.28, 0.92, 0.0), (0.12, 0.55, 0.12), "cylinder"),
            (f"{role}_arm_r", (ox + 0.28, 0.92, 0.0), (0.12, 0.55, 0.12), "cylinder"),
            (f"{role}_vest", (ox, 0.95, 0.12), (0.36, 0.12, 0.04), "cube"),
            (f"{role}_hat", (ox, 1.72, 0.0), (0.26, 0.1, 0.26), "cylinder"),
        ]
        if role == "marshaller":
            parts += [
                (f"{role}_wand", (ox + 0.42, 1.25, 0.05), (0.05, 0.55, 0.05), "cube"),
                (f"{role}_wand_tip", (ox + 0.42, 1.55, 0.05), (0.08, 0.08, 0.08), "cube"),
            ]
    _mod.emit_prefab(
        "mdl_ramp_crew_kit_v01",
        "f5a6b7c8d9e049234567890abcdef012",
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
        body_h = 0.55 if seated else 0.85
        body_y = 0.55 if seated else 0.9
        parts += [
            (f"{prefix}_torso", (ox, body_y, 0.0), (0.38, body_h, 0.22), "cube"),
            (f"{prefix}_head", (ox, body_y + body_h * 0.55 + 0.16, 0.0), (0.22, 0.2, 0.22), "cylinder"),
        ]
        if seated:
            parts.append((f"{prefix}_legs", (ox, 0.28, 0.2), (0.4, 0.2, 0.55), "cube"))
        else:
            parts += [
                (f"{prefix}_leg_l", (ox - 0.1, 0.35, 0.0), (0.14, 0.7, 0.14), "cylinder"),
                (f"{prefix}_leg_r", (ox + 0.1, 0.35, 0.0), (0.14, 0.7, 0.14), "cylinder"),
                (f"{prefix}_arm_l", (ox - 0.28, body_y, 0.0), (0.12, 0.55, 0.12), "cylinder"),
                (f"{prefix}_arm_r", (ox + 0.28, body_y, 0.0), (0.12, 0.55, 0.12), "cylinder"),
            ]
    _mod.emit_prefab(
        "mdl_passenger_kit_v01",
        "a6b7c8d9e0f14934567890abcdef0123",
        parts,
        base=(0.45, 0.28, 0.25),
        accent=(0.2, 0.35, 0.4),
        step=(0.3, 0.32, 0.35),
    )


def main() -> None:
    emit_fuel()
    emit_baggage()
    emit_bus()
    emit_pushback()
    emit_crew()
    emit_passengers()
    print("Wrote Batch F2 Resources prefabs")


if __name__ == "__main__":
    main()
