#!/usr/bin/env python3
"""Generate denser WLD/PRP v02 glTF kits (decision 0025 item 2).

Sibling *_v02 kits for airfield lighting and props — more segmented parts than
the thin v01 Batch B kits. Does not overwrite v01. PreferArtKit callers pick
v02 first when present.
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
    props = ROOT / "Models" / "Props"

    # Keep v01 mesh names so TryPlaceNamedMesh fallbacks still work, plus denser
    # multi-part assemblies for PreferArtKit / PlaceWorldLighting upgrades.
    pack_gltf(
        props / "mdl_airfield_lighting_kit_v02.gltf",
        {
            # Legacy single-piece names (v01 contract).
            "runway_edge_light": box(0, 0.18, 0, 0.14, 0.36, 0.14),
            "taxiway_light": box(0, 0.14, 0, 0.12, 0.28, 0.12),
            "apron_floodlight": box(0, 2.0, 0, 0.28, 4.0, 0.28),
            "obstruction_light": box(0, 0.45, 0, 0.18, 0.9, 0.18),
            # Denser multi-part edge lamp.
            "edge_base": box(0, 0.04, 0, 0.28, 0.08, 0.28),
            "edge_stem": box(0, 0.22, 0, 0.08, 0.28, 0.08),
            "edge_lens": box(0, 0.4, 0, 0.16, 0.14, 0.16),
            # Denser taxi lamp.
            "taxi_base": box(0, 0.04, 0, 0.22, 0.08, 0.22),
            "taxi_stem": box(0, 0.2, 0, 0.07, 0.24, 0.07),
            "taxi_lens": box(0, 0.36, 0, 0.14, 0.12, 0.14),
            # Denser flood mast.
            "flood_base": box(0, 0.12, 0, 0.7, 0.24, 0.7),
            "flood_pole": box(0, 2.1, 0, 0.22, 4.0, 0.22),
            "flood_arm": box(0.55, 4.05, 0, 1.0, 0.14, 0.18),
            "flood_head": box(1.05, 4.0, 0, 0.55, 0.35, 0.45),
            "flood_lamp": box(1.25, 3.85, 0, 0.28, 0.18, 0.28),
            # Obstruction beacon.
            "obst_base": box(0, 0.08, 0, 0.3, 0.12, 0.3),
            "obst_stem": box(0, 0.35, 0, 0.1, 0.5, 0.1),
            "obst_lens": box(0, 0.7, 0, 0.22, 0.22, 0.22),
        },
    )

    pack_gltf(
        props / "mdl_airfield_props_kit_v02.gltf",
        {
            # Legacy single-piece names.
            "windsock_pole": box(0, 1.5, 0, 0.1, 3.0, 0.1),
            "cone": box(0, 0.28, 0, 0.32, 0.55, 0.32),
            "barrier": box(0, 0.55, 0, 1.6, 1.1, 0.1),
            "sign_board": box(0, 1.1, 0, 1.3, 0.9, 0.08),
            "baggage_dolly": box(0, 0.4, 0, 1.5, 0.55, 0.85),
            # Denser windsock.
            "sock_pole": box(0, 1.6, 0, 0.1, 3.2, 0.1),
            "sock_base": box(0, 0.08, 0, 0.45, 0.16, 0.45),
            "sock_frame": box(0, 3.1, 0.35, 0.08, 0.08, 0.7),
            "sock_fabric": box(0, 3.05, 0.85, 0.35, 0.35, 1.1),
            # Denser cone.
            "cone_base": box(0, 0.05, 0, 0.4, 0.08, 0.4),
            "cone_body": box(0, 0.35, 0, 0.28, 0.55, 0.28),
            "cone_stripe": box(0, 0.35, 0, 0.3, 0.12, 0.3),
            "cone_tip": box(0, 0.7, 0, 0.12, 0.18, 0.12),
            # Denser barrier.
            "barrier_rail": box(0, 0.85, 0, 1.7, 0.12, 0.1),
            "barrier_rail_low": box(0, 0.35, 0, 1.7, 0.12, 0.1),
            "barrier_leg_l": box(-0.7, 0.45, 0, 0.12, 0.9, 0.12),
            "barrier_leg_r": box(0.7, 0.45, 0, 0.12, 0.9, 0.12),
            "barrier_foot_l": box(-0.7, 0.06, 0, 0.35, 0.1, 0.35),
            "barrier_foot_r": box(0.7, 0.06, 0, 0.35, 0.1, 0.35),
            "barrier_stripe": box(0, 0.85, 0, 1.75, 0.18, 0.06),
            # Denser sign.
            "sign_post": box(0, 1.15, 0, 0.12, 2.3, 0.12),
            "sign_face": box(0.08, 1.45, 0, 0.06, 0.95, 1.2),
            "sign_back": box(0, 1.45, 0, 0.14, 1.05, 1.3),
            "sign_stripe": box(0.1, 1.85, 0, 0.04, 0.12, 1.15),
            # Denser dolly.
            "dolly_bed": box(0, 0.45, 0, 1.5, 0.18, 0.85),
            "dolly_rail": box(0, 0.75, 0, 1.4, 0.45, 0.08),
            "dolly_wheel_fl": box(-0.55, 0.15, 0.3, 0.18, 0.18, 0.12),
            "dolly_wheel_fr": box(0.55, 0.15, 0.3, 0.18, 0.18, 0.12),
            "dolly_wheel_rl": box(-0.55, 0.15, -0.3, 0.18, 0.18, 0.12),
            "dolly_wheel_rr": box(0.55, 0.15, -0.3, 0.18, 0.18, 0.12),
            "dolly_hitch": box(0, 0.4, 0.95, 0.25, 0.2, 0.25),
        },
    )

    pack_gltf(
        props / "mdl_service_equipment_kit_v02.gltf",
        {
            # Keep v01 extract names used by stairs/GPU/chocks fallbacks.
            "stairs": box(0, 0.9, 0, 1.2, 1.8, 2.4),
            "chocks": box(0, 0.15, 0, 0.6, 0.3, 0.35),
            "gpu": box(0, 0.55, 0, 1.4, 1.1, 0.9),
            "pushback_tug": box(0, 0.7, 0, 1.6, 1.2, 2.2),
            "cone": box(0, 0.28, 0, 0.32, 0.55, 0.32),
            "barrier": box(0, 0.55, 0, 1.6, 1.1, 0.1),
            "windsock": box(0, 1.5, 0, 0.1, 3.0, 0.1),
            # Denser stairs.
            "stairs_base": box(0, 0.12, 0, 1.3, 0.2, 2.5),
            "stairs_rail_l": box(-0.55, 1.0, 0, 0.08, 1.6, 2.3),
            "stairs_rail_r": box(0.55, 1.0, 0, 0.08, 1.6, 2.3),
            "stairs_tread_1": box(0, 0.35, 0.8, 1.1, 0.08, 0.4),
            "stairs_tread_2": box(0, 0.7, 0.3, 1.1, 0.08, 0.4),
            "stairs_tread_3": box(0, 1.05, -0.2, 1.1, 0.08, 0.4),
            "stairs_tread_4": box(0, 1.4, -0.7, 1.1, 0.08, 0.4),
            "stairs_platform": box(0, 1.7, -1.1, 1.2, 0.12, 0.7),
            "stairs_wheel_l": box(-0.5, 0.2, 1.0, 0.2, 0.2, 0.15),
            "stairs_wheel_r": box(0.5, 0.2, 1.0, 0.2, 0.2, 0.15),
            # Denser GPU.
            "gpu_body": box(0, 0.55, 0, 1.4, 0.9, 0.85),
            "gpu_cab": box(0.4, 0.85, 0, 0.55, 0.55, 0.7),
            "gpu_vent": box(-0.4, 0.9, 0, 0.45, 0.25, 0.7),
            "gpu_cable": box(0.85, 0.45, 0, 0.35, 0.2, 0.2),
            "gpu_wheel_fl": box(0.45, 0.15, 0.35, 0.18, 0.18, 0.12),
            "gpu_wheel_fr": box(0.45, 0.15, -0.35, 0.18, 0.18, 0.12),
            "gpu_wheel_rl": box(-0.45, 0.15, 0.35, 0.18, 0.18, 0.12),
            "gpu_wheel_rr": box(-0.45, 0.15, -0.35, 0.18, 0.18, 0.12),
        },
    )

    print("WLD/PRP v02 kits written under", props)


if __name__ == "__main__":
    main()
