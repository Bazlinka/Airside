#!/usr/bin/env python3
"""PRP-001 mdl_service_equipment_kit_v03 — denser stand GSE (stairs/GPU/belt/chocks).

Keeps every TryPlaceNamedMesh / BuildStairs / BuildGpuCart / PlaceBeltLoader /
PlaceFodBin extract name from authored_v01, with tubular rails, higher-segment
wheels, canopy, and Safety-Yellow stripe mass so overview/follow reads as
equipment rather than grey boxes. Does not overwrite v01/v02/authored.
"""
from __future__ import annotations

import importlib.util
from pathlib import Path

import numpy as np

REPO = Path(__file__).resolve().parents[1]
PROPS = REPO / "game/Airside/Assets/Airside/Art/Models/Props"
STAIRS_OUT = REPO / "game/Airside/Assets/Airside/Art/Models/Props"

_SPEC = importlib.util.spec_from_file_location(
    "batch_c", REPO / "scripts/generate-batch-c-models.py"
)
_batch = importlib.util.module_from_spec(_SPEC)
assert _SPEC.loader is not None
_SPEC.loader.exec_module(_batch)
box = _batch.box
pack_gltf = _batch.pack_gltf

_AUTH = importlib.util.spec_from_file_location(
    "authored", REPO / "scripts/generate-authored-fbx-turboprop-terminal.py"
)
_authored = importlib.util.module_from_spec(_AUTH)
assert _AUTH.loader is not None
_AUTH.loader.exec_module(_authored)
cylinder = _authored.cylinder


def merge_parts(*parts: tuple[np.ndarray, np.ndarray]) -> tuple[np.ndarray, np.ndarray]:
    verts: list[np.ndarray] = []
    indices: list[np.ndarray] = []
    base = 0
    for v, i in parts:
        vv = np.asarray(v, dtype=np.float32)
        ii = np.asarray(i, dtype=np.uint16) + base
        verts.append(vv)
        indices.append(ii)
        base += len(vv)
    return np.vstack(verts), np.concatenate(indices)


def service_equipment_v03() -> dict[str, tuple[np.ndarray, np.ndarray]]:
    """Named multi-part kit — denser geometry, same extract contract as authored_v01."""
    # Combined silhouette fallbacks (v01 / early PreferArtKit paths).
    stairs_silhouette = merge_parts(
        box(0, 0.12, 0, 1.45, 0.22, 4.1),
        box(0, 1.55, 0.1, 1.35, 2.6, 0.12),
        box(0, 2.85, 1.6, 1.4, 0.1, 1.0),
        box(-0.7, 1.6, 0.1, 0.08, 2.5, 3.6),
        box(0.7, 1.6, 0.1, 0.08, 2.5, 3.6),
    )
    gpu_silhouette = merge_parts(
        box(0, 0.55, 0, 1.65, 1.0, 2.2),
        cylinder(0.45, 1.25, 0.5, 0.1, 0.45, axis="y", segments=12),
        box(0, 0.15, 0, 1.7, 0.2, 2.25),
    )
    chocks_silhouette = merge_parts(
        box(-0.38, 0.12, 0, 0.32, 0.24, 0.48),
        box(0.38, 0.12, 0, 0.32, 0.24, 0.48),
        cylinder(0, 0.08, 0, 0.03, 0.75, axis="x", segments=8),
    )

    meshes: dict[str, tuple[np.ndarray, np.ndarray]] = {
        "stairs": stairs_silhouette,
        # Chassis / stringers — longer run for regional turboprop door height.
        "stairs_base": merge_parts(
            box(0, 0.12, 0.05, 1.5, 0.2, 4.0),
            box(0, 0.28, -1.85, 1.5, 0.12, 0.35),
        ),
        # Tubular handrails (cylinder along Z) instead of flat slabs.
        "stairs_rail_l": merge_parts(
            cylinder(-0.72, 1.55, 0.05, 0.038, 3.7, axis="z", segments=12),
            cylinder(-0.72, 1.15, 0.05, 0.028, 3.5, axis="z", segments=10),
        ),
        "stairs_rail_r": merge_parts(
            cylinder(0.72, 1.55, 0.05, 0.038, 3.7, axis="z", segments=12),
            cylinder(0.72, 1.15, 0.05, 0.028, 3.5, axis="z", segments=10),
        ),
        "stairs_rail_mid": cylinder(0, 1.85, 1.55, 0.03, 1.35, axis="x", segments=10),
        "stairs_rail_cross": cylinder(0, 1.35, 0.55, 0.025, 1.35, axis="x", segments=10),
        # Twelve treads mapped into the six extract slots + denser platforms.
        "stairs_tread_1": box(0, 0.42, -1.45, 1.28, 0.07, 0.32),
        "stairs_tread_2": box(0, 0.78, -0.85, 1.28, 0.07, 0.32),
        "stairs_tread_3": box(0, 1.14, -0.25, 1.28, 0.07, 0.32),
        "stairs_tread_4": box(0, 1.5, 0.35, 1.28, 0.07, 0.32),
        "stairs_tread_5": box(0, 1.86, 0.95, 1.28, 0.07, 0.32),
        "stairs_tread_6": box(0, 2.22, 1.45, 1.28, 0.07, 0.3),
        "stairs_nosing_1": box(0, 0.47, -1.3, 1.3, 0.035, 0.07),
        "stairs_nosing_2": box(0, 0.83, -0.7, 1.3, 0.035, 0.07),
        "stairs_nosing_3": box(0, 1.19, -0.1, 1.3, 0.035, 0.07),
        "stairs_nosing_4": box(0, 1.55, 0.5, 1.3, 0.035, 0.07),
        "stairs_nosing_5": box(0, 1.91, 1.1, 1.3, 0.035, 0.07),
        "stairs_platform": merge_parts(
            box(0, 2.55, 1.85, 1.4, 0.1, 1.0),
            box(0, 2.62, 2.28, 1.4, 0.04, 0.08),
        ),
        "stairs_side_panel_l": box(-0.78, 1.4, 0.1, 0.06, 2.4, 3.6),
        "stairs_side_panel_r": box(0.78, 1.4, 0.1, 0.06, 2.4, 3.6),
        "stairs_post_1l": cylinder(-0.72, 0.75, -1.2, 0.03, 0.95, axis="y", segments=10),
        "stairs_post_1r": cylinder(0.72, 0.75, -1.2, 0.03, 0.95, axis="y", segments=10),
        "stairs_post_2l": cylinder(-0.72, 1.35, 0.1, 0.03, 0.95, axis="y", segments=10),
        "stairs_post_2r": cylinder(0.72, 1.35, 0.1, 0.03, 0.95, axis="y", segments=10),
        "stairs_post_3l": cylinder(-0.72, 1.95, 1.3, 0.03, 0.95, axis="y", segments=10),
        "stairs_post_3r": cylinder(0.72, 1.95, 1.3, 0.03, 0.95, axis="y", segments=10),
        "stairs_handle": merge_parts(
            box(0, 3.05, 1.55, 1.45, 0.06, 1.45),
            box(-0.68, 2.8, 1.55, 0.06, 0.45, 1.35),
            box(0.68, 2.8, 1.55, 0.06, 0.45, 1.35),
        ),
        "stairs_brace": box(0, 0.55, 0.2, 1.35, 0.08, 0.08),
        "stairs_wheel_l": cylinder(-0.62, 0.18, -1.55, 0.16, 0.12, axis="x", segments=14),
        "stairs_wheel_r": cylinder(0.62, 0.18, -1.55, 0.16, 0.12, axis="x", segments=14),
        "stairs_wheel_rl": cylinder(-0.62, 0.18, 1.55, 0.16, 0.12, axis="x", segments=14),
        "stairs_wheel_rr": cylinder(0.62, 0.18, 1.55, 0.16, 0.12, axis="x", segments=14),
        "stairs_hub_fl": cylinder(-0.62, 0.18, -1.55, 0.06, 0.14, axis="x", segments=10),
        "stairs_hub_fr": cylinder(0.62, 0.18, -1.55, 0.06, 0.14, axis="x", segments=10),
        "stairs_hub_rl": cylinder(-0.62, 0.18, 1.55, 0.06, 0.14, axis="x", segments=10),
        "stairs_hub_rr": cylinder(0.62, 0.18, 1.55, 0.06, 0.14, axis="x", segments=10),
        # Chocks
        "chocks": chocks_silhouette,
        "chock_a": merge_parts(
            box(-0.38, 0.12, 0, 0.32, 0.24, 0.48),
            box(-0.38, 0.06, 0.18, 0.32, 0.1, 0.14),
        ),
        "chock_b": merge_parts(
            box(0.38, 0.12, 0, 0.32, 0.24, 0.48),
            box(0.38, 0.06, 0.18, 0.32, 0.1, 0.14),
        ),
        "chock_rope": cylinder(0, 0.08, 0, 0.03, 0.75, axis="x", segments=8),
        "chock_handle": box(0, 0.22, 0, 0.08, 0.2, 0.08),
        # GPU — longer chassis, exhaust, reel, beacon
        "gpu": gpu_silhouette,
        "gpu_body": merge_parts(
            box(0, 0.55, 0, 1.65, 0.95, 2.15),
            box(0, 0.12, 0, 1.7, 0.18, 2.2),
        ),
        "gpu_cab": box(0.55, 0.95, 0.75, 0.55, 0.65, 0.65),
        "gpu_vent": merge_parts(
            box(-0.55, 0.85, -0.85, 0.55, 0.45, 0.55),
            box(-0.55, 0.85, -1.05, 0.45, 0.35, 0.06),
        ),
        "gpu_panel": box(-0.55, 0.85, 0.95, 0.45, 0.4, 0.08),
        "gpu_panel_b": box(-0.55, 0.85, -0.95, 0.45, 0.4, 0.08),
        "gpu_grille": box(0, 0.7, -1.05, 1.15, 0.5, 0.06),
        "gpu_grille_2": box(0, 0.7, 1.05, 1.15, 0.5, 0.06),
        "gpu_slot_1": box(-0.7, 0.9, 1.02, 0.22, 0.08, 0.04),
        "gpu_slot_2": box(-0.7, 0.72, 1.02, 0.22, 0.08, 0.04),
        "gpu_cable": cylinder(0.7, 0.45, 1.15, 0.05, 0.55, axis="z", segments=10),
        "gpu_cable_reel": cylinder(0.7, 0.7, 0.95, 0.2, 0.18, axis="x", segments=14),
        "gpu_hitch": box(0, 0.35, -1.25, 0.28, 0.14, 0.35),
        "gpu_beacon": cylinder(0, 1.2, -0.2, 0.08, 0.14, axis="y", segments=10),
        "gpu_exhaust": cylinder(0.5, 1.25, 0.35, 0.09, 0.5, axis="y", segments=12),
        "gpu_light": box(0.55, 1.25, 0.75, 0.12, 0.1, 0.12),
        "gpu_stripe": box(0, 0.45, 1.08, 1.4, 0.1, 0.05),
        "gpu_handle": box(0.85, 0.85, 0.4, 0.08, 0.3, 0.35),
        "gpu_wheel_fl": cylinder(0.65, 0.2, 0.75, 0.18, 0.14, axis="x", segments=14),
        "gpu_wheel_fr": cylinder(0.65, 0.2, -0.75, 0.18, 0.14, axis="x", segments=14),
        "gpu_wheel_rl": cylinder(-0.65, 0.2, 0.75, 0.18, 0.14, axis="x", segments=14),
        "gpu_wheel_rr": cylinder(-0.65, 0.2, -0.75, 0.18, 0.14, axis="x", segments=14),
        "gpu_hub_fl": cylinder(0.65, 0.2, 0.75, 0.07, 0.16, axis="x", segments=10),
        "gpu_hub_fr": cylinder(0.65, 0.2, -0.75, 0.07, 0.16, axis="x", segments=10),
        "gpu_hub_rl": cylinder(-0.65, 0.2, 0.75, 0.07, 0.16, axis="x", segments=10),
        "gpu_hub_rr": cylinder(-0.65, 0.2, -0.75, 0.07, 0.16, axis="x", segments=10),
        # Cone / towbar / bin (apron kit companions)
        "cone": cylinder(1.2, 0.35, 0, 0.16, 0.7, axis="y", segments=12),
        "cone_stripe": cylinder(1.2, 0.4, 0, 0.17, 0.12, axis="y", segments=12),
        "cone_base": box(1.2, 0.04, 0, 0.42, 0.08, 0.42),
        "cone_tip": cylinder(1.2, 0.68, 0, 0.07, 0.16, axis="y", segments=10),
        "towbar": box(0, 0.2, -1.5, 2.6, 0.1, 0.1),
        "towbar_head": box(1.25, 0.25, -1.5, 0.28, 0.22, 0.22),
        "towbar_wheel": cylinder(-1.05, 0.12, -1.5, 0.09, 0.1, axis="z", segments=10),
        "towbar_handle": box(-1.25, 0.35, -1.5, 0.35, 0.08, 0.08),
        "towbar_eye": cylinder(1.4, 0.25, -1.5, 0.08, 0.06, axis="x", segments=10),
        "bin": box(-1.2, 0.45, 0.8, 0.75, 0.95, 0.7),
        "bin_lid": box(-1.2, 0.98, 0.8, 0.78, 0.08, 0.74),
        "bin_handle": box(-1.2, 1.05, 0.8, 0.35, 0.06, 0.06),
        "bin_stripe": box(-1.2, 0.55, 1.12, 0.55, 0.12, 0.04),
        # Belt loader — denser boom / rollers / cab
        "belt_loader_chassis": merge_parts(
            box(2.8, 0.38, 0, 1.75, 0.5, 0.95),
            box(2.8, 0.12, 0, 1.8, 0.14, 1.0),
        ),
        "belt_loader_cab": box(3.45, 0.85, 0, 0.6, 0.7, 0.75),
        "belt_loader_cab_glass": box(3.72, 0.95, 0, 0.04, 0.35, 0.55),
        "belt_loader_boom": box(2.05, 1.05, 0, 2.7, 0.16, 0.42),
        "belt_loader_belt": box(2.05, 1.16, 0, 2.5, 0.06, 0.32),
        "belt_loader_rail_l": box(2.05, 1.28, 0.2, 2.5, 0.1, 0.05),
        "belt_loader_rail_r": box(2.05, 1.28, -0.2, 2.5, 0.1, 0.05),
        "belt_loader_wheel_fl": cylinder(3.4, 0.18, 0.4, 0.14, 0.14, axis="z", segments=14),
        "belt_loader_wheel_fr": cylinder(3.4, 0.18, -0.4, 0.14, 0.14, axis="z", segments=14),
        "belt_loader_wheel_rl": cylinder(2.25, 0.18, 0.4, 0.14, 0.14, axis="z", segments=14),
        "belt_loader_wheel_rr": cylinder(2.25, 0.18, -0.4, 0.14, 0.14, axis="z", segments=14),
        "belt_loader_hub_fl": cylinder(3.4, 0.18, 0.4, 0.06, 0.08, axis="z", segments=10),
        "belt_loader_hub_fr": cylinder(3.4, 0.18, -0.4, 0.06, 0.08, axis="z", segments=10),
        "belt_loader_hitch": box(3.85, 0.35, 0, 0.28, 0.15, 0.22),
        "belt_loader_light": box(3.5, 1.2, 0, 0.12, 0.1, 0.12),
        "belt_loader_hinge": box(3.15, 0.9, 0, 0.28, 0.28, 0.42),
        "belt_loader_support": box(2.4, 0.7, 0, 0.12, 0.5, 0.12),
        "belt_loader_roller_1": cylinder(1.1, 1.16, 0, 0.06, 0.32, axis="z", segments=10),
        "belt_loader_roller_2": cylinder(1.7, 1.16, 0, 0.06, 0.32, axis="z", segments=10),
        "belt_loader_roller_3": cylinder(2.3, 1.16, 0, 0.06, 0.32, axis="z", segments=10),
        "belt_loader_roller_4": cylinder(2.9, 1.16, 0, 0.06, 0.32, axis="z", segments=10),
        "belt_loader_bumper": box(3.7, 0.35, 0, 0.12, 0.28, 0.85),
        "belt_loader_stripe": box(2.8, 0.5, 0.48, 1.5, 0.08, 0.04),
        # Pushback tug leftover name from v02 kit (unused when VEH-004 wins).
        "pushback_tug": box(0, 0.7, 0, 1.7, 1.25, 2.3),
        "barrier": box(0, 0.55, 0, 1.6, 1.1, 0.1),
        "windsock": cylinder(0, 1.5, 0, 0.05, 3.0, axis="y", segments=8),
    }
    return meshes


def passenger_stairs_v02() -> dict[str, tuple[np.ndarray, np.ndarray]]:
    """Standalone stairs kit used if PreferArtKit ever points at a stairs-only file."""
    full = service_equipment_v03()
    keys = [k for k in full if k.startswith("stairs")]
    return {k: full[k] for k in keys}


def main() -> None:
    PROPS.mkdir(parents=True, exist_ok=True)
    kit = service_equipment_v03()
    out = PROPS / "mdl_service_equipment_kit_v03.gltf"
    pack_gltf(out, kit)
    print(f"Wrote {out} ({len(kit)} meshes)")

    stairs = passenger_stairs_v02()
    stairs_path = STAIRS_OUT / "mdl_passenger_stairs_v02.gltf"
    pack_gltf(stairs_path, stairs)
    print(f"Wrote {stairs_path} ({len(stairs)} meshes)")


if __name__ == "__main__":
    main()
