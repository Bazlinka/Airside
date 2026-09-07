#!/usr/bin/env python3
"""Generate lofted Batch C turboprop kit with a distinct asset id (decision 0025 item 2).

Does NOT race v04 filenames — writes mdl_regional_turboprop_01_lofted_v01 so Prefer
can pick it without colliding with the existing greybox v04 kit. Still procedural
box geometry (ArtGltfLoader format), with stepped fuselage loft rings for a
rounder silhouette from overview/follow cameras.
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
    # Stepped fuselage loft: overlapping rings that read as a tapered tube.
    fuselage = {
        "nose_tip": box(0, 1.05, 5.35, 0.55, 0.55, 0.45),
        "nose_ring_a": box(0, 1.05, 5.0, 0.78, 0.78, 0.5),
        "nose_ring_b": box(0, 1.08, 4.55, 1.0, 1.0, 0.55),
        "cockpit_loft": box(0, 1.35, 3.7, 1.08, 0.95, 1.15),
        "cockpit_frame": box(0, 1.7, 3.7, 1.14, 0.08, 1.2),
        "cabin_ring_fwd": box(0, 1.18, 2.55, 1.32, 1.32, 1.35),
        "cabin_ring_mid": box(0, 1.18, 1.0, 1.36, 1.36, 1.55),
        "cabin_ring_aft": box(0, 1.15, -0.55, 1.32, 1.3, 1.55),
        "cabin_ring_tail": box(0, 1.12, -2.1, 1.18, 1.18, 1.45),
        "tail_cone": box(0, 1.05, -3.35, 0.95, 0.95, 1.2),
        "belly_fairing": box(0, 0.52, 0.3, 0.9, 0.32, 4.0),
        "cabin_window_band": box(0, 1.38, 0.5, 1.42, 0.14, 4.4),
        "cabin_window_1": box(-0.72, 1.38, 2.0, 0.06, 0.28, 0.5),
        "cabin_window_2": box(-0.72, 1.38, 1.1, 0.06, 0.28, 0.5),
        "cabin_window_3": box(-0.72, 1.38, 0.2, 0.06, 0.28, 0.5),
        "cabin_window_4": box(-0.72, 1.38, -0.7, 0.06, 0.28, 0.5),
        "cabin_window_5": box(-0.72, 1.38, -1.6, 0.06, 0.28, 0.5),
        "cabin_window_r1": box(0.72, 1.38, 2.0, 0.06, 0.28, 0.5),
        "cabin_window_r2": box(0.72, 1.38, 1.1, 0.06, 0.28, 0.5),
        "cabin_window_r3": box(0.72, 1.38, 0.2, 0.06, 0.28, 0.5),
        "cabin_window_r4": box(0.72, 1.38, -0.7, 0.06, 0.28, 0.5),
        "cabin_window_r5": box(0.72, 1.38, -1.6, 0.06, 0.28, 0.5),
    }

    wings = {
        "wing_left": box(-4.2, 1.05, 0.4, 6.5, 0.16, 1.9),
        "wing_right": box(4.2, 1.05, 0.4, 6.5, 0.16, 1.9),
        "wing_root_left": box(-1.35, 1.08, 0.45, 1.7, 0.24, 1.55),
        "wing_root_right": box(1.35, 1.08, 0.45, 1.7, 0.24, 1.55),
        "wing_fairing_left": box(-2.4, 0.95, 0.55, 1.1, 0.35, 1.1),
        "wing_fairing_right": box(2.4, 0.95, 0.55, 1.1, 0.35, 1.1),
        "flap_left": box(-3.2, 1.0, -0.35, 3.2, 0.08, 0.45),
        "flap_right": box(3.2, 1.0, -0.35, 3.2, 0.08, 0.45),
        "spoiler_left": box(-3.0, 1.12, 0.05, 2.4, 0.05, 0.35),
        "spoiler_right": box(3.0, 1.12, 0.05, 2.4, 0.05, 0.35),
        "aileron_left": box(-6.2, 1.02, 0.15, 1.8, 0.07, 0.55),
        "aileron_right": box(6.2, 1.02, 0.15, 1.8, 0.07, 0.55),
        "wingtip_left": box(-7.35, 1.05, 0.55, 0.35, 0.12, 0.7),
        "wingtip_right": box(7.35, 1.05, 0.55, 0.35, 0.12, 0.7),
        "winglet_left": box(-7.45, 1.4, 0.45, 0.12, 0.65, 0.45),
        "winglet_right": box(7.45, 1.4, 0.45, 0.12, 0.65, 0.45),
    }

    engines = {
        "engine_left": box(-2.4, 0.85, 1.05, 0.78, 0.78, 2.15),
        "engine_right": box(2.4, 0.85, 1.05, 0.78, 0.78, 2.15),
        "nacelle_left": box(-2.4, 0.52, 0.55, 0.58, 0.38, 1.25),
        "nacelle_right": box(2.4, 0.52, 0.55, 0.58, 0.38, 1.25),
        "intake_left": box(-2.4, 0.95, 2.0, 0.58, 0.48, 0.38),
        "intake_right": box(2.4, 0.95, 2.0, 0.58, 0.48, 0.38),
        "exhaust_left": box(-2.4, 0.7, -0.2, 0.38, 0.28, 0.55),
        "exhaust_right": box(2.4, 0.7, -0.2, 0.38, 0.28, 0.55),
        "propeller_left": box(-2.4, 0.85, 2.25, 0.08, 2.45, 0.18),
        "propeller_left_b": box(-2.4, 0.85, 2.25, 2.45, 0.08, 0.18),
        "propeller_right": box(2.4, 0.85, 2.25, 0.08, 2.45, 0.18),
        "propeller_right_b": box(2.4, 0.85, 2.25, 2.45, 0.08, 0.18),
        "spinner_left": box(-2.4, 0.85, 2.42, 0.3, 0.3, 0.38),
        "spinner_right": box(2.4, 0.85, 2.42, 0.3, 0.3, 0.38),
    }

    empennage = {
        "tail_fin": box(0, 2.5, -3.75, 0.14, 2.35, 1.55),
        "tail_fin_tip": box(0, 3.55, -3.55, 0.12, 0.4, 0.75),
        "tailplane": box(0, 1.78, -3.9, 3.5, 0.12, 1.05),
        "elevator_left": box(-1.15, 1.75, -4.3, 1.35, 0.06, 0.42),
        "elevator_right": box(1.15, 1.75, -4.3, 1.35, 0.06, 0.42),
        "rudder": box(0, 2.55, -4.4, 0.1, 1.7, 0.48),
        "dorsal_fin": box(0, 1.85, -2.85, 0.1, 0.55, 0.9),
    }

    gear = {
        "gear_nose": box(0, 0.38, 3.2, 0.14, 0.75, 0.35),
        "gear_left": box(-1.15, 0.32, -0.35, 0.14, 0.75, 0.45),
        "gear_right": box(1.15, 0.32, -0.35, 0.14, 0.75, 0.45),
        "gear_door_nose": box(0, 0.55, 3.2, 0.55, 0.06, 0.7),
        "gear_door_left": box(-1.15, 0.55, -0.35, 0.65, 0.06, 0.85),
        "gear_door_right": box(1.15, 0.55, -0.35, 0.65, 0.06, 0.85),
        "tire_nose": box(0, 0.12, 3.2, 0.22, 0.22, 0.28),
        "tire_left": box(-1.15, 0.12, -0.35, 0.28, 0.28, 0.22),
        "tire_right": box(1.15, 0.12, -0.35, 0.28, 0.28, 0.22),
        "door_fwd": box(-0.7, 1.1, 2.15, 0.08, 1.05, 1.25),
        "cargo_door": box(0.7, 1.0, -1.5, 0.08, 0.95, 1.6),
        "antenna": box(0, 2.1, 1.3, 0.06, 0.55, 0.06),
        "antenna_aft": box(0, 2.2, -1.7, 0.05, 0.4, 0.05),
        "pitot": box(0.45, 1.15, 5.05, 0.04, 0.04, 0.35),
        "nav_light_left": box(-7.4, 1.08, 0.55, 0.12, 0.12, 0.12),
        "nav_light_right": box(7.4, 1.08, 0.55, 0.12, 0.12, 0.12),
        "beacon_top": box(0, 3.05, -3.4, 0.14, 0.14, 0.14),
        "landing_light_l": box(-2.6, 0.95, 2.05, 0.18, 0.12, 0.12),
        "landing_light_r": box(2.6, 0.95, 2.05, 0.18, 0.12, 0.12),
        "taxi_light": box(0, 0.55, 3.6, 0.16, 0.1, 0.12),
    }

    meshes = {}
    meshes.update(fuselage)
    meshes.update(wings)
    meshes.update(engines)
    meshes.update(empennage)
    meshes.update(gear)

    out = ROOT / "Models" / "Aircraft" / "mdl_regional_turboprop_01_lofted_v01.gltf"
    pack_gltf(out, meshes)
    print(f"Wrote {out.name} ({len(meshes)} meshes)")


if __name__ == "__main__":
    main()
