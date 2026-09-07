#!/usr/bin/env python3
"""Generate denser Batch C v04 metre-scale glTF kits (decision 0025 item 2).

Same POSITION+indices box format as ArtGltfLoader. Does not overwrite v01–v03 —
writes sibling *_v04.gltf/.bin + .meta. More segmented parts for silhouette
readability (still procedural greybox, not authored meshes).
"""

from __future__ import annotations

import importlib.util
from pathlib import Path

ROOT = Path("/workspace/game/Airside/Assets/Airside/Art")

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
        ROOT / "Models" / "Aircraft" / "mdl_regional_turboprop_01_v04.gltf",
        {
            # Fuselage broken into readable segments.
            "fuselage": box(0, 1.15, 0.1, 1.35, 1.35, 5.2),
            "fuselage_mid": box(0, 1.18, 1.6, 1.32, 1.28, 1.4),
            "fuselage_aft": box(0, 1.15, -3.0, 1.15, 1.2, 2.2),
            "belly_fairing": box(0, 0.55, 0.2, 0.95, 0.35, 3.8),
            "nose": box(0, 1.05, 4.55, 1.05, 1.05, 1.4),
            "radome": box(0, 1.05, 5.15, 0.85, 0.85, 0.55),
            "cockpit": box(0, 1.55, 3.55, 1.05, 0.55, 1.1),
            "cockpit_frame": box(0, 1.55, 3.55, 1.12, 0.08, 1.15),
            "cabin_windows": box(0, 1.35, 0.4, 1.42, 0.35, 4.2),
            "cabin_window_band": box(0, 1.35, 0.4, 1.44, 0.12, 4.3),
            "cabin_window_1": box(-0.72, 1.35, 1.8, 0.06, 0.28, 0.55),
            "cabin_window_2": box(-0.72, 1.35, 0.9, 0.06, 0.28, 0.55),
            "cabin_window_3": box(-0.72, 1.35, 0.0, 0.06, 0.28, 0.55),
            "cabin_window_4": box(-0.72, 1.35, -0.9, 0.06, 0.28, 0.55),
            "wing_left": box(-4.2, 1.05, 0.4, 6.5, 0.16, 1.9),
            "wing_right": box(4.2, 1.05, 0.4, 6.5, 0.16, 1.9),
            "wing_root_left": box(-1.4, 1.08, 0.45, 1.6, 0.22, 1.5),
            "wing_root_right": box(1.4, 1.08, 0.45, 1.6, 0.22, 1.5),
            "flap_left": box(-3.2, 1.0, -0.35, 3.2, 0.08, 0.45),
            "flap_right": box(3.2, 1.0, -0.35, 3.2, 0.08, 0.45),
            "spoiler_left": box(-3.0, 1.12, 0.05, 2.4, 0.05, 0.35),
            "spoiler_right": box(3.0, 1.12, 0.05, 2.4, 0.05, 0.35),
            "aileron_left": box(-6.2, 1.02, 0.15, 1.8, 0.07, 0.55),
            "aileron_right": box(6.2, 1.02, 0.15, 1.8, 0.07, 0.55),
            "wingtip_left": box(-7.35, 1.05, 0.55, 0.35, 0.12, 0.7),
            "wingtip_right": box(7.35, 1.05, 0.55, 0.35, 0.12, 0.7),
            "winglet_left": box(-7.45, 1.35, 0.45, 0.12, 0.55, 0.45),
            "winglet_right": box(7.45, 1.35, 0.45, 0.12, 0.55, 0.45),
            "engine_left": box(-2.4, 0.85, 1.05, 0.75, 0.75, 2.1),
            "engine_right": box(2.4, 0.85, 1.05, 0.75, 0.75, 2.1),
            "nacelle_left": box(-2.4, 0.55, 0.55, 0.55, 0.35, 1.2),
            "nacelle_right": box(2.4, 0.55, 0.55, 0.55, 0.35, 1.2),
            "intake_left": box(-2.4, 0.95, 1.95, 0.55, 0.45, 0.35),
            "intake_right": box(2.4, 0.95, 1.95, 0.55, 0.45, 0.35),
            "exhaust_left": box(-2.4, 0.7, -0.15, 0.35, 0.25, 0.55),
            "exhaust_right": box(2.4, 0.7, -0.15, 0.35, 0.25, 0.55),
            "propeller_left": box(-2.4, 0.85, 2.2, 0.08, 2.35, 0.18),
            "propeller_left_b": box(-2.4, 0.85, 2.2, 2.35, 0.08, 0.18),
            "propeller_right": box(2.4, 0.85, 2.2, 0.08, 2.35, 0.18),
            "propeller_right_b": box(2.4, 0.85, 2.2, 2.35, 0.08, 0.18),
            "spinner_left": box(-2.4, 0.85, 2.35, 0.28, 0.28, 0.35),
            "spinner_right": box(2.4, 0.85, 2.35, 0.28, 0.28, 0.35),
            "tail_fin": box(0, 2.45, -3.7, 0.14, 2.2, 1.5),
            "tail_fin_tip": box(0, 3.45, -3.55, 0.12, 0.35, 0.7),
            "tailplane": box(0, 1.75, -3.85, 3.4, 0.12, 1.0),
            "elevator_left": box(-1.1, 1.72, -4.25, 1.3, 0.06, 0.4),
            "elevator_right": box(1.1, 1.72, -4.25, 1.3, 0.06, 0.4),
            "rudder": box(0, 2.5, -4.35, 0.1, 1.6, 0.45),
            "gear_nose": box(0, 0.38, 3.15, 0.14, 0.75, 0.35),
            "gear_left": box(-1.15, 0.32, -0.35, 0.14, 0.75, 0.45),
            "gear_right": box(1.15, 0.32, -0.35, 0.14, 0.75, 0.45),
            "gear_door_nose": box(0, 0.55, 3.15, 0.55, 0.06, 0.7),
            "gear_door_left": box(-1.15, 0.55, -0.35, 0.65, 0.06, 0.85),
            "gear_door_right": box(1.15, 0.55, -0.35, 0.65, 0.06, 0.85),
            "tire_nose": box(0, 0.12, 3.15, 0.22, 0.22, 0.28),
            "tire_left": box(-1.15, 0.12, -0.35, 0.28, 0.28, 0.22),
            "tire_right": box(1.15, 0.12, -0.35, 0.28, 0.28, 0.22),
            "door_fwd": box(-0.7, 1.1, 2.0, 0.08, 1.05, 1.25),
            "cargo_door": box(0.7, 1.0, -1.6, 0.08, 0.95, 1.6),
            "antenna": box(0, 2.05, 1.2, 0.06, 0.55, 0.06),
            "antenna_aft": box(0, 2.15, -1.8, 0.05, 0.4, 0.05),
            "pitot": box(0.45, 1.15, 4.9, 0.04, 0.04, 0.35),
            "nav_light_left": box(-7.4, 1.08, 0.55, 0.12, 0.12, 0.12),
            "nav_light_right": box(7.4, 1.08, 0.55, 0.12, 0.12, 0.12),
            "beacon_top": box(0, 2.95, -3.4, 0.14, 0.14, 0.14),
            "landing_light_l": box(-2.6, 0.95, 2.0, 0.18, 0.12, 0.12),
            "landing_light_r": box(2.6, 0.95, 2.0, 0.18, 0.12, 0.12),
            "taxi_light": box(0, 0.55, 3.55, 0.16, 0.1, 0.12),
        },
    )

    pack_gltf(
        ROOT / "Models" / "Buildings" / "mdl_terminal_regional_small_v04.gltf",
        {
            "terminal_body": box(0, 2.2, 0, 22, 4.4, 5),
            "roof_slab": box(0, 4.55, 0.2, 22.6, 0.35, 5.6),
            "glass_front": box(0, 2.4, 2.45, 17, 2.2, 0.12),
            "window_mullion_1": box(-5.5, 2.4, 2.5, 0.12, 2.2, 0.1),
            "window_mullion_2": box(0, 2.4, 2.5, 0.12, 2.2, 0.1),
            "window_mullion_3": box(5.5, 2.4, 2.5, 0.12, 2.2, 0.1),
            "window_mullion_4": box(-2.75, 2.4, 2.5, 0.1, 2.2, 0.1),
            "window_mullion_5": box(2.75, 2.4, 2.5, 0.1, 2.2, 0.1),
            "canopy": box(0, 3.55, 3.4, 14, 0.18, 2.2),
            "canopy_post_l": box(-6.5, 1.7, 3.9, 0.25, 3.4, 0.25),
            "canopy_post_r": box(6.5, 1.7, 3.9, 0.25, 3.4, 0.25),
            "canopy_post_ml": box(-2.2, 1.7, 3.9, 0.2, 3.4, 0.2),
            "canopy_post_mr": box(2.2, 1.7, 3.9, 0.2, 3.4, 0.2),
            "canopy_beam": box(0, 3.4, 3.9, 13.5, 0.12, 0.2),
            "entrance": box(0, 1.4, 2.55, 3.2, 2.6, 0.2),
            "entrance_frame": box(0, 1.4, 2.65, 3.5, 2.8, 0.08),
            "end_cap_left": box(-11.5, 2.0, 0, 1.2, 4.0, 5.2),
            "end_cap_right": box(11.5, 2.0, 0, 1.2, 4.0, 5.2),
            "service_wing": box(6, 1.4, -3.2, 8, 2.8, 3),
            "service_door": box(4.5, 1.1, -4.65, 1.4, 2.1, 0.12),
            "baggage_door": box(8.0, 1.0, -4.65, 1.8, 1.8, 0.12),
            "roof_plant": box(-4, 5.0, -0.8, 3.5, 0.9, 2.2),
            "roof_plant_b": box(3.5, 4.95, -0.5, 2.4, 0.7, 1.6),
            "roof_plant_c": box(0, 4.9, -1.2, 2.0, 0.55, 1.4),
            "signage_bar": box(0, 3.9, 2.7, 8.0, 0.35, 0.2),
            "landside_awning": box(0, 2.8, -2.8, 12, 0.15, 1.8),
            "landside_glass": box(0, 2.1, -2.55, 10, 2.2, 0.1),
            "column_l": box(-8, 1.5, 2.2, 0.35, 3.0, 0.35),
            "column_r": box(8, 1.5, 2.2, 0.35, 3.0, 0.35),
            "column_ml": box(-4, 1.5, 2.2, 0.3, 3.0, 0.3),
            "column_mr": box(4, 1.5, 2.2, 0.3, 3.0, 0.3),
        },
    )

    pack_gltf(
        ROOT / "Models" / "Buildings" / "mdl_hangar_small_v04.gltf",
        {
            "hangar_shell": box(0, 2.5, 0, 14, 5, 9),
            "door_opening": box(0, 2.0, 4.6, 8, 4, 0.2),
            "door_track_l": box(-4.2, 2.0, 4.55, 0.25, 4.2, 0.25),
            "door_track_r": box(4.2, 2.0, 4.55, 0.25, 4.2, 0.25),
            "door_panel_l": box(-2.0, 2.0, 4.5, 3.8, 3.9, 0.12),
            "door_panel_r": box(2.0, 2.0, 4.5, 3.8, 3.9, 0.12),
            "door_rib_l": box(-2.0, 2.0, 4.45, 0.12, 3.7, 0.08),
            "door_rib_r": box(2.0, 2.0, 4.45, 0.12, 3.7, 0.08),
            "roof_ridge": box(0, 5.15, 0, 14.4, 0.35, 1.4),
            "roof_panel_l": box(-3.5, 4.7, 0, 7, 0.2, 8.8),
            "roof_panel_r": box(3.5, 4.7, 0, 7, 0.2, 8.8),
            "roof_rib_1": box(-5.0, 4.85, 0, 0.2, 0.25, 8.6),
            "roof_rib_2": box(-1.5, 4.85, 0, 0.2, 0.25, 8.6),
            "roof_rib_3": box(1.5, 4.85, 0, 0.2, 0.25, 8.6),
            "roof_rib_4": box(5.0, 4.85, 0, 0.2, 0.25, 8.6),
            "roof_rib_5": box(-3.25, 4.85, 0, 0.15, 0.22, 8.6),
            "roof_rib_6": box(3.25, 4.85, 0, 0.15, 0.22, 8.6),
            "crane_beam": box(0, 4.3, 0, 12.5, 0.2, 0.35),
            "buttress_l": box(-7.2, 2.0, 0, 0.5, 4.0, 8.5),
            "buttress_r": box(7.2, 2.0, 0, 0.5, 4.0, 8.5),
            "side_vent": box(-7.15, 3.2, 2.5, 0.15, 1.2, 2.0),
            "side_vent_b": box(7.15, 3.2, 2.5, 0.15, 1.2, 2.0),
            "office_lean": box(5.5, 1.2, -3.5, 3.5, 2.4, 2.8),
            "office_window": box(5.5, 1.6, -4.85, 2.2, 1.0, 0.1),
            "side_window": box(-7.1, 2.4, -1.5, 0.12, 1.4, 2.5),
            "personnel_door": box(-5.5, 1.1, 4.55, 1.0, 2.1, 0.12),
        },
    )

    pack_gltf(
        ROOT / "Models" / "Buildings" / "mdl_operations_shed_v04.gltf",
        {
            "shed_body": box(0, 1.4, 0, 6, 2.8, 4),
            "porch": box(0, 1.0, 2.3, 3, 2.0, 1.2),
            "porch_roof": box(0, 2.15, 2.4, 3.4, 0.18, 1.5),
            "door": box(0, 1.0, 2.85, 1.1, 1.9, 0.1),
            "window_l": box(-1.8, 1.6, 2.05, 1.0, 0.9, 0.08),
            "window_r": box(1.8, 1.6, 2.05, 1.0, 0.9, 0.08),
            "window_side": box(-3.05, 1.6, 0, 0.08, 0.9, 1.4),
            "window_side_b": box(3.05, 1.6, 0, 0.08, 0.9, 1.4),
            "roof_ridge": box(0, 2.95, 0, 6.2, 0.25, 1.0),
            "roof_panel": box(0, 2.85, 0, 6.0, 0.12, 3.8),
            "antenna_mast": box(1.8, 3.6, -0.5, 0.08, 1.2, 0.08),
            "antenna_dish": box(1.8, 4.15, -0.5, 0.45, 0.12, 0.45),
            "ac_unit": box(-1.5, 3.15, -0.8, 1.2, 0.45, 0.9),
            "ac_unit_b": box(0.5, 3.1, -1.0, 0.9, 0.35, 0.7),
            "radio_rack": box(-2.2, 1.4, -1.6, 0.8, 1.6, 0.5),
        },
    )

    pack_gltf(
        ROOT / "Models" / "Vehicles" / "mdl_fuel_truck_small_v04.gltf",
        {
            "cab": box(1.05, 0.95, 0, 1.5, 1.5, 1.55),
            "cab_window": box(1.55, 1.25, 0, 0.08, 0.7, 1.2),
            "cab_door": box(1.05, 0.95, 0.78, 1.2, 1.2, 0.08),
            "tank": box(-0.45, 0.9, 0, 2.5, 1.25, 1.35),
            "tank_band": box(-0.45, 0.9, 0, 2.55, 0.2, 1.4),
            "tank_band_b": box(-0.45, 1.25, 0, 2.55, 0.12, 1.4),
            "tank_cap": box(-0.45, 1.55, 0, 0.45, 0.2, 0.45),
            "wheel_fl": box(1.35, 0.28, 0.58, 0.38, 0.55, 0.22),
            "wheel_fr": box(1.35, 0.28, -0.58, 0.38, 0.55, 0.22),
            "wheel_rl": box(-1.15, 0.28, 0.58, 0.38, 0.55, 0.22),
            "wheel_rr": box(-1.15, 0.28, -0.58, 0.38, 0.55, 0.22),
            "hose_mount": box(-1.55, 0.7, 0.75, 0.4, 0.4, 0.4),
            "hose_reel": box(-1.55, 0.85, 0.35, 0.55, 0.55, 0.35),
            "hose_nozzle": box(-1.85, 0.55, 0.75, 0.25, 0.2, 0.2),
            "mirror_l": box(1.7, 1.35, 0.85, 0.12, 0.25, 0.18),
            "mirror_r": box(1.7, 1.35, -0.85, 0.12, 0.25, 0.18),
            "beacon": box(1.05, 1.85, 0, 0.25, 0.2, 0.25),
            "bumper": box(1.85, 0.4, 0, 0.2, 0.35, 1.4),
            "step": box(1.55, 0.45, 0.85, 0.35, 0.15, 0.35),
        },
    )

    pack_gltf(
        ROOT / "Models" / "Vehicles" / "mdl_baggage_tug_train_v04.gltf",
        {
            "tug": box(3.6, 0.55, 0, 1.7, 0.95, 1.15),
            "tug_cab": box(4.1, 0.95, 0, 0.9, 0.85, 1.05),
            "tug_window": box(4.5, 1.15, 0, 0.08, 0.45, 0.85),
            "tug_seat": box(4.0, 0.75, 0, 0.55, 0.35, 0.7),
            "cart_1": box(1.8, 0.5, 0, 1.5, 0.75, 1.05),
            "cart_2": box(0.2, 0.5, 0, 1.5, 0.75, 1.05),
            "cart_3": box(-1.4, 0.5, 0, 1.5, 0.75, 1.05),
            "cargo_1": box(1.8, 0.95, 0, 1.2, 0.45, 0.85),
            "cargo_2": box(0.2, 0.95, 0, 1.2, 0.45, 0.85),
            "cargo_3": box(-1.4, 0.95, 0, 1.2, 0.45, 0.85),
            "cargo_strap_1": box(1.8, 1.15, 0, 1.25, 0.06, 0.9),
            "cargo_strap_2": box(0.2, 1.15, 0, 1.25, 0.06, 0.9),
            "hitch_1": box(2.7, 0.35, 0, 0.45, 0.2, 0.2),
            "hitch_2": box(1.0, 0.35, 0, 0.45, 0.2, 0.2),
            "hitch_3": box(-0.6, 0.35, 0, 0.45, 0.2, 0.2),
            "wheel_fl": box(4.0, 0.22, 0.5, 0.3, 0.4, 0.18),
            "wheel_fr": box(4.0, 0.22, -0.5, 0.3, 0.4, 0.18),
            "wheel_rl": box(3.2, 0.22, 0.5, 0.3, 0.4, 0.18),
            "wheel_rr": box(3.2, 0.22, -0.5, 0.3, 0.4, 0.18),
            "beacon": box(4.1, 1.5, 0, 0.2, 0.15, 0.2),
        },
    )

    pack_gltf(
        ROOT / "Models" / "Vehicles" / "mdl_passenger_bus_apron_v04.gltf",
        {
            "bus_body": box(0, 0.95, 0, 4.4, 1.65, 1.65),
            "cabin_roof": box(0, 1.9, 0, 4.2, 0.28, 1.55),
            "windows": box(0, 1.35, 0, 3.8, 0.55, 1.7),
            "window_mullion": box(0, 1.35, 0, 0.08, 0.55, 1.72),
            "window_mullion_2": box(-1.2, 1.35, 0, 0.08, 0.55, 1.72),
            "window_mullion_3": box(1.2, 1.35, 0, 0.08, 0.55, 1.72),
            "door": box(0.25, 0.95, 0.85, 1.05, 1.35, 0.1),
            "door_window": box(0.25, 1.35, 0.9, 0.7, 0.45, 0.06),
            "bumper_front": box(2.25, 0.45, 0, 0.25, 0.45, 1.5),
            "bumper_rear": box(-2.25, 0.45, 0, 0.25, 0.45, 1.5),
            "wheel_fl": box(1.45, 0.3, 0.72, 0.42, 0.55, 0.24),
            "wheel_fr": box(1.45, 0.3, -0.72, 0.42, 0.55, 0.24),
            "wheel_rl": box(-1.45, 0.3, 0.72, 0.42, 0.55, 0.24),
            "wheel_rr": box(-1.45, 0.3, -0.72, 0.42, 0.55, 0.24),
            "beacon": box(0, 2.15, 0, 0.28, 0.2, 0.28),
            "mirror_l": box(2.1, 1.55, 0.9, 0.12, 0.3, 0.2),
            "step": box(0.25, 0.35, 0.95, 0.9, 0.15, 0.35),
            "headlight_l": box(2.3, 0.7, 0.55, 0.12, 0.18, 0.2),
            "headlight_r": box(2.3, 0.7, -0.55, 0.12, 0.18, 0.2),
        },
    )

    print("Batch C v04 kits written under", ROOT)


if __name__ == "__main__":
    main()
