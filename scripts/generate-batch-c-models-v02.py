#!/usr/bin/env python3
"""Generate richer Batch C v02 metre-scale glTF kits (decision 0025 item 2).

Keeps the same POSITION+indices box kit format that ArtGltfLoader understands.
Does not overwrite Approved v01 files — writes sibling *_v02.gltf/.bin + .meta.
"""

from __future__ import annotations

import importlib.util
from pathlib import Path

ROOT = Path("/workspace/game/Airside/Assets/Airside/Art")

# Reuse pack/box helpers from the v01 generator without duplicating the binary format.
_SPEC = importlib.util.spec_from_file_location(
    "batch_c_v01", Path("/workspace/scripts/generate-batch-c-models.py")
)
_v01 = importlib.util.module_from_spec(_SPEC)
assert _SPEC.loader is not None
_SPEC.loader.exec_module(_v01)
box = _v01.box
pack_gltf = _v01.pack_gltf


def main() -> None:
    pack_gltf(
        ROOT / "Models" / "Aircraft" / "mdl_regional_turboprop_01_v02.gltf",
        {
            "fuselage": box(0, 1.15, 0.1, 1.35, 1.35, 7.6),
            "nose": box(0, 1.05, 4.55, 1.05, 1.05, 1.4),
            "cockpit": box(0, 1.55, 3.55, 1.05, 0.55, 1.1),
            "cabin_windows": box(0, 1.35, 0.4, 1.42, 0.35, 4.2),
            "wing_left": box(-4.2, 1.05, 0.4, 6.5, 0.16, 1.9),
            "wing_right": box(4.2, 1.05, 0.4, 6.5, 0.16, 1.9),
            "wingtip_left": box(-7.35, 1.05, 0.55, 0.35, 0.12, 0.7),
            "wingtip_right": box(7.35, 1.05, 0.55, 0.35, 0.12, 0.7),
            "engine_left": box(-2.4, 0.85, 1.05, 0.75, 0.75, 2.1),
            "engine_right": box(2.4, 0.85, 1.05, 0.75, 0.75, 2.1),
            "nacelle_left": box(-2.4, 0.55, 0.55, 0.55, 0.35, 1.2),
            "nacelle_right": box(2.4, 0.55, 0.55, 0.55, 0.35, 1.2),
            "propeller_left": box(-2.4, 0.85, 2.2, 0.08, 2.35, 0.18),
            "propeller_left_b": box(-2.4, 0.85, 2.2, 2.35, 0.08, 0.18),
            "propeller_right": box(2.4, 0.85, 2.2, 0.08, 2.35, 0.18),
            "propeller_right_b": box(2.4, 0.85, 2.2, 2.35, 0.08, 0.18),
            "spinner_left": box(-2.4, 0.85, 2.35, 0.28, 0.28, 0.35),
            "spinner_right": box(2.4, 0.85, 2.35, 0.28, 0.28, 0.35),
            "tail_fin": box(0, 2.45, -3.7, 0.14, 2.2, 1.5),
            "tailplane": box(0, 1.75, -3.85, 3.4, 0.12, 1.0),
            "rudder": box(0, 2.5, -4.35, 0.1, 1.6, 0.45),
            "gear_nose": box(0, 0.38, 3.15, 0.14, 0.75, 0.35),
            "gear_left": box(-1.15, 0.32, -0.35, 0.14, 0.75, 0.45),
            "gear_right": box(1.15, 0.32, -0.35, 0.14, 0.75, 0.45),
            "tire_nose": box(0, 0.12, 3.15, 0.22, 0.22, 0.28),
            "tire_left": box(-1.15, 0.12, -0.35, 0.28, 0.28, 0.22),
            "tire_right": box(1.15, 0.12, -0.35, 0.28, 0.28, 0.22),
            "door_fwd": box(-0.7, 1.1, 2.0, 0.08, 1.05, 1.25),
            "antenna": box(0, 2.05, 1.2, 0.06, 0.55, 0.06),
        },
    )

    pack_gltf(
        ROOT / "Models" / "Buildings" / "mdl_terminal_regional_small_v02.gltf",
        {
            "terminal_body": box(0, 2.2, 0, 22, 4.4, 5),
            "roof_slab": box(0, 4.55, 0.2, 22.6, 0.35, 5.6),
            "glass_front": box(0, 2.4, 2.45, 17, 2.2, 0.12),
            "canopy": box(0, 3.55, 3.4, 14, 0.18, 2.2),
            "canopy_post_l": box(-6.5, 1.7, 3.9, 0.25, 3.4, 0.25),
            "canopy_post_r": box(6.5, 1.7, 3.9, 0.25, 3.4, 0.25),
            "entrance": box(0, 1.4, 2.55, 3.2, 2.6, 0.2),
            "end_cap_left": box(-11.5, 2.0, 0, 1.2, 4.0, 5.2),
            "end_cap_right": box(11.5, 2.0, 0, 1.2, 4.0, 5.2),
            "service_wing": box(6, 1.4, -3.2, 8, 2.8, 3),
            "service_door": box(4.5, 1.1, -4.65, 1.4, 2.1, 0.12),
            "roof_plant": box(-4, 5.0, -0.8, 3.5, 0.9, 2.2),
        },
    )

    pack_gltf(
        ROOT / "Models" / "Buildings" / "mdl_hangar_small_v02.gltf",
        {
            "hangar_shell": box(0, 2.5, 0, 14, 5, 9),
            "door_opening": box(0, 2.0, 4.6, 8, 4, 0.2),
            "door_track_l": box(-4.2, 2.0, 4.55, 0.25, 4.2, 0.25),
            "door_track_r": box(4.2, 2.0, 4.55, 0.25, 4.2, 0.25),
            "roof_ridge": box(0, 5.15, 0, 14.4, 0.35, 1.4),
            "roof_panel_l": box(-3.5, 4.7, 0, 7, 0.2, 8.8),
            "roof_panel_r": box(3.5, 4.7, 0, 7, 0.2, 8.8),
            "buttress_l": box(-7.2, 2.0, 0, 0.5, 4.0, 8.5),
            "buttress_r": box(7.2, 2.0, 0, 0.5, 4.0, 8.5),
            "side_vent": box(-7.15, 3.2, 2.5, 0.15, 1.2, 2.0),
        },
    )

    pack_gltf(
        ROOT / "Models" / "Buildings" / "mdl_operations_shed_v02.gltf",
        {
            "shed_body": box(0, 1.4, 0, 6, 2.8, 4),
            "porch": box(0, 1.0, 2.3, 3, 2.0, 1.2),
            "porch_roof": box(0, 2.15, 2.4, 3.4, 0.18, 1.5),
            "door": box(0, 1.0, 2.85, 1.1, 1.9, 0.1),
            "window_l": box(-1.8, 1.6, 2.05, 1.0, 0.9, 0.08),
            "window_r": box(1.8, 1.6, 2.05, 1.0, 0.9, 0.08),
            "roof_ridge": box(0, 2.95, 0, 6.2, 0.25, 1.0),
        },
    )

    pack_gltf(
        ROOT / "Models" / "Vehicles" / "mdl_fuel_truck_small_v02.gltf",
        {
            "cab": box(1.05, 0.95, 0, 1.5, 1.5, 1.55),
            "cab_window": box(1.55, 1.25, 0, 0.08, 0.7, 1.2),
            "tank": box(-0.45, 0.9, 0, 2.5, 1.25, 1.35),
            "tank_band": box(-0.45, 0.9, 0, 2.55, 0.2, 1.4),
            "wheel_fl": box(1.35, 0.28, 0.58, 0.38, 0.55, 0.22),
            "wheel_fr": box(1.35, 0.28, -0.58, 0.38, 0.55, 0.22),
            "wheel_rl": box(-1.15, 0.28, 0.58, 0.38, 0.55, 0.22),
            "wheel_rr": box(-1.15, 0.28, -0.58, 0.38, 0.55, 0.22),
            "hose_mount": box(-1.55, 0.7, 0.75, 0.4, 0.4, 0.4),
            "mirror_l": box(1.7, 1.35, 0.85, 0.12, 0.25, 0.18),
            "beacon": box(1.05, 1.85, 0, 0.25, 0.2, 0.25),
        },
    )

    pack_gltf(
        ROOT / "Models" / "Vehicles" / "mdl_baggage_tug_train_v02.gltf",
        {
            "tug": box(3.6, 0.55, 0, 1.7, 0.95, 1.15),
            "tug_cab": box(4.1, 0.95, 0, 0.9, 0.85, 1.05),
            "cart_1": box(1.8, 0.5, 0, 1.5, 0.75, 1.05),
            "cart_2": box(0.2, 0.5, 0, 1.5, 0.75, 1.05),
            "cart_3": box(-1.4, 0.5, 0, 1.5, 0.75, 1.05),
            "cargo_1": box(1.8, 0.95, 0, 1.2, 0.45, 0.85),
            "cargo_2": box(0.2, 0.95, 0, 1.2, 0.45, 0.85),
            "cargo_3": box(-1.4, 0.95, 0, 1.2, 0.45, 0.85),
            "hitch_1": box(2.7, 0.35, 0, 0.45, 0.2, 0.2),
            "hitch_2": box(1.0, 0.35, 0, 0.45, 0.2, 0.2),
            "hitch_3": box(-0.6, 0.35, 0, 0.45, 0.2, 0.2),
            "wheel_fl": box(4.0, 0.22, 0.5, 0.3, 0.4, 0.18),
            "wheel_fr": box(4.0, 0.22, -0.5, 0.3, 0.4, 0.18),
        },
    )

    pack_gltf(
        ROOT / "Models" / "Vehicles" / "mdl_passenger_bus_apron_v02.gltf",
        {
            "bus_body": box(0, 0.95, 0, 4.4, 1.65, 1.65),
            "cabin_roof": box(0, 1.9, 0, 4.2, 0.28, 1.55),
            "windows": box(0, 1.35, 0, 3.8, 0.55, 1.7),
            "door": box(0.25, 0.95, 0.85, 1.05, 1.35, 0.1),
            "bumper_front": box(2.25, 0.45, 0, 0.25, 0.45, 1.5),
            "bumper_rear": box(-2.25, 0.45, 0, 0.25, 0.45, 1.5),
            "wheel_fl": box(1.45, 0.3, 0.72, 0.42, 0.55, 0.24),
            "wheel_fr": box(1.45, 0.3, -0.72, 0.42, 0.55, 0.24),
            "wheel_rl": box(-1.45, 0.3, 0.72, 0.42, 0.55, 0.24),
            "wheel_rr": box(-1.45, 0.3, -0.72, 0.42, 0.55, 0.24),
            "beacon": box(0, 2.15, 0, 0.28, 0.2, 0.28),
        },
    )

    print("Batch C v02 kits written under", ROOT)


if __name__ == "__main__":
    main()
