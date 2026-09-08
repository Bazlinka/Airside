#!/usr/bin/env python3
"""Emit Resources prefabs for landside car v02 + turnaround fleet v06 / pushback v03.

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


def emit_parked_car() -> None:
    _mod.emit_prefab(
        "mdl_parked_car_v02",
        "a7b8c9d0e1f2031425263748596a7b8c",
        [
            ("car_body", (0.0, 0.45, 0.0), (1.7, 0.55, 3.6), "cube"),
            ("car_hood", (0.0, 0.62, 1.05), (1.5, 0.2, 1.05), "cube"),
            ("car_boot", (0.0, 0.58, -1.35), (1.5, 0.24, 0.85), "cube"),
            ("car_roof", (0.0, 1.05, -0.2), (1.4, 0.28, 1.55), "cube"),
            ("glass_front", (0.0, 1.02, 0.62), (1.28, 0.36, 0.06), "cube"),
            ("glass_rear", (0.0, 1.0, -1.05), (1.22, 0.32, 0.06), "cube"),
            ("glass_side_l", (-0.82, 0.98, -0.15), (0.05, 0.32, 1.15), "cube"),
            ("glass_side_r", (0.82, 0.98, -0.15), (0.05, 0.32, 1.15), "cube"),
            ("glass_pane_fl", (-0.82, 0.98, 0.35), (0.05, 0.3, 0.42), "cube"),
            ("glass_pane_fr", (0.82, 0.98, 0.35), (0.05, 0.3, 0.42), "cube"),
            ("car_door_l", (-0.9, 0.58, 0.05), (0.08, 0.55, 1.05), "cube"),
            ("car_door_r", (0.9, 0.58, 0.05), (0.08, 0.55, 1.05), "cube"),
            ("car_bumper_front", (0.0, 0.32, 1.92), (1.72, 0.28, 0.22), "cube"),
            ("car_bumper_rear", (0.0, 0.32, -1.92), (1.72, 0.28, 0.22), "cube"),
            ("car_grille", (0.0, 0.48, 1.98), (0.95, 0.2, 0.06), "cube"),
            ("car_headlight_l", (-0.55, 0.48, 1.98), (0.28, 0.16, 0.08), "cube"),
            ("car_headlight_r", (0.55, 0.48, 1.98), (0.28, 0.16, 0.08), "cube"),
            ("car_taillight_l", (-0.55, 0.5, -1.98), (0.3, 0.14, 0.06), "cube"),
            ("car_taillight_r", (0.55, 0.5, -1.98), (0.3, 0.14, 0.06), "cube"),
            ("car_mirror_l", (-0.98, 0.88, 0.48), (0.16, 0.1, 0.18), "cube"),
            ("car_mirror_r", (0.98, 0.88, 0.48), (0.16, 0.1, 0.18), "cube"),
            ("car_stripe", (0.0, 0.55, 0.0), (1.78, 0.06, 2.6), "cube"),
            ("car_wheel_arch_fl", (-0.78, 0.38, 1.05), (0.35, 0.28, 0.55), "cube"),
            ("car_wheel_arch_fr", (0.78, 0.38, 1.05), (0.35, 0.28, 0.55), "cube"),
            ("car_wheel_arch_rl", (-0.78, 0.38, -1.05), (0.35, 0.28, 0.55), "cube"),
            ("car_wheel_arch_rr", (0.78, 0.38, -1.05), (0.35, 0.28, 0.55), "cube"),
            ("wheel_fl", (-0.78, 0.17, 1.05), (0.2, 0.28, 0.28), "cylinder", xcyl),
            ("wheel_fr", (0.78, 0.17, 1.05), (0.2, 0.28, 0.28), "cylinder", xcyl),
            ("wheel_rl", (-0.78, 0.17, -1.05), (0.2, 0.28, 0.28), "cylinder", xcyl),
            ("wheel_rr", (0.78, 0.17, -1.05), (0.2, 0.28, 0.28), "cylinder", xcyl),
            ("hub_fl", (-0.78, 0.17, 1.05), (0.1, 0.12, 0.12), "cylinder", xcyl),
            ("hub_fr", (0.78, 0.17, 1.05), (0.1, 0.12, 0.12), "cylinder", xcyl),
        ],
        base=(0.35, 0.4, 0.38),
        accent=(0.85, 0.85, 0.88),
        step=(0.12, 0.12, 0.13),
    )


def emit_fuel() -> None:
    _mod.emit_prefab(
        "mdl_fuel_truck_small_v06",
        "b2c3d4e5f6a7489012345678901bcdef",
        [
            ("cab", (1.05, 0.95, 0.0), (1.55, 1.55, 1.6), "cube"),
            ("cab_fairing", (1.72, 1.35, 0.0), (0.55, 1.1, 1.35), "cylinder", xcyl),
            ("cab_window", (1.66, 1.28, 0.0), (0.04, 0.78, 1.28), "cube"),
            ("cab_door", (1.05, 0.95, 0.82), (1.25, 1.25, 0.08), "cube"),
            ("tank", (-0.5, 0.98, 0.0), (2.75, 1.28, 1.35), "cylinder", xcyl),
            ("chassis", (0.05, 0.34, 0.0), (3.85, 0.24, 1.12), "cube"),
            ("wheel_fl", (1.4, 0.28, 0.6), (0.24, 0.3, 0.3), "cylinder", zcyl),
            ("wheel_fr", (1.4, 0.28, -0.6), (0.24, 0.3, 0.3), "cylinder", zcyl),
            ("wheel_ml", (-0.35, 0.28, 0.6), (0.24, 0.3, 0.3), "cylinder", zcyl),
            ("wheel_mr", (-0.35, 0.28, -0.6), (0.24, 0.3, 0.3), "cylinder", zcyl),
            ("wheel_rl", (-1.2, 0.28, 0.6), (0.24, 0.3, 0.3), "cylinder", zcyl),
            ("wheel_rr", (-1.2, 0.28, -0.6), (0.24, 0.3, 0.3), "cylinder", zcyl),
            ("hose", (-1.85, 0.55, 0.62), (0.95, 0.14, 0.14), "cylinder", zcyl),
            ("hose_reel", (-1.62, 0.88, 0.38), (0.38, 0.3, 0.3), "cylinder", zcyl),
            ("hose_nozzle", (-2.05, 0.52, 0.88), (0.32, 0.18, 0.18), "cube"),
            ("hose_pivot", (-1.62, 0.72, 0.78), (0.14, 0.16, 0.14), "cylinder"),
            ("pump_cabinet", (-1.62, 0.88, -0.55), (0.58, 0.72, 0.58), "cube"),
            ("beacon", (1.05, 1.95, 0.0), (0.26, 0.2, 0.26), "cube"),
            ("headlight_l", (2.02, 0.68, 0.42), (0.1, 0.14, 0.16), "cube"),
            ("headlight_r", (2.02, 0.68, -0.42), (0.1, 0.14, 0.16), "cube"),
            ("glass_pane_1", (1.66, 1.42, 0.0), (0.04, 0.38, 1.0), "cube"),
            ("cab_stripe", (1.05, 0.68, 0.84), (1.4, 0.12, 0.04), "cube"),
            ("bumper", (1.95, 0.4, 0.0), (0.26, 0.38, 1.48), "cube"),
        ],
        base=(0.95, 0.76, 0.12),
        accent=(0.95, 0.85, 0.2),
        step=(0.15, 0.15, 0.16),
    )


def emit_baggage() -> None:
    parts = [
        ("tug", (3.55, 0.48, 0.0), (1.75, 0.78, 1.15), "cube"),
        ("tug_cab", (4.18, 0.92, 0.0), (0.88, 0.72, 1.05), "cube"),
        ("tug_window", (4.58, 1.15, 0.0), (0.04, 0.45, 0.85), "cube"),
        ("tug_seat", (3.92, 0.82, 0.0), (0.42, 0.32, 0.65), "cube"),
        ("tug_rollbar_top", (3.95, 1.72, 0.0), (0.58, 0.06, 0.95), "cube"),
        ("wheel_fl", (4.05, 0.22, 0.52), (0.2, 0.22, 0.22), "cylinder", zcyl),
        ("wheel_fr", (4.05, 0.22, -0.52), (0.2, 0.22, 0.22), "cylinder", zcyl),
        ("wheel_rl", (3.15, 0.22, 0.52), (0.2, 0.22, 0.22), "cylinder", zcyl),
        ("wheel_rr", (3.15, 0.22, -0.52), (0.2, 0.22, 0.22), "cylinder", zcyl),
        ("beacon", (4.15, 1.55, 0.0), (0.22, 0.16, 0.22), "cube"),
        ("headlight_l", (4.68, 0.68, 0.38), (0.1, 0.12, 0.14), "cube"),
        ("glass_pane_1", (4.59, 1.25, 0.0), (0.04, 0.3, 0.7), "cube"),
    ]
    for idx, cx in enumerate((1.8, 0.15, -1.5), start=1):
        parts += [
            (f"cart_{idx}", (cx, 0.48, 0.0), (1.52, 0.72, 1.05), "cube"),
            (f"cargo_{idx}", (cx, 0.95, 0.0), (1.18, 0.42, 0.82), "cube"),
            (f"cargo_lid_{idx}", (cx, 1.18, 0.0), (1.12, 0.06, 0.78), "cube"),
            (f"cart_wheel_{idx}l", (cx, 0.18, 0.5), (0.15, 0.17, 0.17), "cylinder", zcyl),
            (f"cart_wheel_{idx}r", (cx, 0.18, -0.5), (0.15, 0.17, 0.17), "cylinder", zcyl),
            (f"hitch_{idx}", (2.7 - (idx - 1) * 1.75, 0.35, 0.0), (0.48, 0.18, 0.2), "cube"),
        ]
    _mod.emit_prefab(
        "mdl_baggage_tug_train_v06",
        "c3d4e5f6a7b849012345678902cdef01",
        parts,
        base=(0.91, 0.38, 0.12),
        accent=(0.22, 0.44, 0.55),
        step=(0.15, 0.15, 0.16),
    )


def emit_bus() -> None:
    _mod.emit_prefab(
        "mdl_passenger_bus_apron_v06",
        "d4e5f6a7b8c94a012345678903def012",
        [
            ("bus_body", (0.0, 0.72, 0.0), (4.5, 1.15, 1.72), "cube"),
            ("bus_body_upper", (0.0, 1.55, 0.0), (4.35, 0.85, 1.62), "cube"),
            ("cabin_roof", (0.0, 2.02, 0.0), (4.25, 0.22, 1.52), "cube"),
            ("nose_round", (2.18, 0.95, 0.0), (1.55, 1.7, 1.7), "cylinder", xcyl),
            ("windshield", (2.32, 1.42, 0.0), (0.05, 0.82, 1.42), "cube"),
            ("door", (0.35, 0.98, 0.9), (1.05, 1.45, 0.1), "cube"),
            ("door_glass", (0.35, 1.28, 0.95), (0.7, 0.62, 0.04), "cube"),
            ("wheel_fl", (1.5, 0.3, 0.75), (0.26, 0.3, 0.3), "cylinder", zcyl),
            ("wheel_fr", (1.5, 0.3, -0.75), (0.26, 0.3, 0.3), "cylinder", zcyl),
            ("wheel_rl", (-1.5, 0.3, 0.75), (0.26, 0.3, 0.3), "cylinder", zcyl),
            ("wheel_rr", (-1.5, 0.3, -0.75), (0.26, 0.3, 0.3), "cylinder", zcyl),
            ("beacon", (0.0, 2.2, 0.0), (0.3, 0.2, 0.3), "cube"),
            ("stripe", (0.0, 0.95, 0.88), (4.2, 0.16, 0.05), "cube"),
            ("headlight_l", (2.42, 0.72, 0.55), (0.12, 0.18, 0.2), "cube"),
            ("glass_pane_1", (0.0, 1.42, 0.9), (3.6, 0.55, 0.04), "cube"),
            ("bumper_front", (2.42, 0.38, 0.0), (0.32, 0.4, 1.58), "cube"),
        ],
        base=(0.22, 0.44, 0.55),
        accent=(0.95, 0.95, 0.96),
        step=(0.15, 0.15, 0.16),
    )


def emit_pushback() -> None:
    _mod.emit_prefab(
        "mdl_pushback_tug_v03",
        "e5f6a7b8c9d04b12345678904ef01234",
        [
            ("tug_body", (0.15, 0.48, 0.0), (2.4, 0.88, 1.28), "cube"),
            ("tug_cab", (0.72, 0.98, 0.0), (0.98, 0.82, 1.12), "cube"),
            ("counterweight", (-0.78, 0.55, 0.0), (0.78, 0.72, 1.12), "cube"),
            ("wheel_fl", (0.75, 0.22, 0.52), (0.2, 0.24, 0.24), "cylinder", zcyl),
            ("wheel_fr", (0.75, 0.22, -0.52), (0.2, 0.24, 0.24), "cylinder", zcyl),
            ("wheel_rl", (-0.65, 0.22, 0.52), (0.2, 0.24, 0.24), "cylinder", zcyl),
            ("wheel_rr", (-0.65, 0.22, -0.52), (0.2, 0.24, 0.24), "cylinder", zcyl),
            ("towbar", (-1.9, 0.28, 0.0), (1.45, 0.12, 0.12), "cube"),
            ("towbar_head", (-2.62, 0.34, 0.0), (0.38, 0.3, 0.38), "cube"),
            ("towbar_wheel", (-2.2, 0.15, 0.0), (0.12, 0.15, 0.15), "cylinder", zcyl),
            ("towbar_handle", (-1.35, 0.48, 0.18), (0.35, 0.08, 0.08), "cube"),
            ("towbar_eye", (-2.82, 0.34, 0.0), (0.08, 0.11, 0.11), "cylinder", zcyl),
            ("tow_pivot", (-1.18, 0.28, 0.0), (0.18, 0.22, 0.18), "cylinder"),
            ("beacon", (0.72, 1.52, 0.0), (0.22, 0.18, 0.22), "cylinder"),
            ("stripe", (0.15, 0.58, 0.66), (2.15, 0.14, 0.04), "cube"),
            ("glass_pane_1", (1.12, 1.18, 0.0), (0.04, 0.42, 0.85), "cube"),
        ],
        base=(0.82, 0.62, 0.18),
        accent=(0.95, 0.35, 0.12),
        step=(0.15, 0.15, 0.16),
    )


def main() -> None:
    emit_parked_car()
    emit_fuel()
    emit_baggage()
    emit_bus()
    emit_pushback()
    print("Wrote fleet v06 / car v02 / pushback v03 Resources prefabs")


if __name__ == "__main__":
    main()
