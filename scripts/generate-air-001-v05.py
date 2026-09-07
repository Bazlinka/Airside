#!/usr/bin/env python3
"""Generate Batch F1 AIR-001 v05 authored turboprop (FBX + companion glTF).

Decision 0027 / Batch F packet — AIR-001 v05 only. Distinct id
`mdl_regional_turboprop_01_v05` — does not overwrite lofted/authored/v04 files.
Companion glTF keeps StreamingAssets/ArtGltfLoader working until Mac Editor
bakes the imported FBX into Resources.

Do not answer with cuboid greybox densify: lathed fuselage, six-blade props,
readable cockpit glass, separated gear/doors/control surfaces/lights.
"""

from __future__ import annotations

import importlib.util
from pathlib import Path

import numpy as np

ROOT = Path("/workspace/game/Airside/Assets/Airside/Art")
AIRCRAFT = ROOT / "Models" / "Aircraft"
BASENAME = "mdl_regional_turboprop_01_v05"

_SPEC = importlib.util.spec_from_file_location(
    "authored_fbx", Path("/workspace/scripts/generate-authored-fbx-turboprop-terminal.py")
)
_auth = importlib.util.module_from_spec(_SPEC)
assert _SPEC.loader is not None
_SPEC.loader.exec_module(_auth)

box = _auth.box
cylinder = _auth.cylinder
prop_blade = _auth.prop_blade
write_kit = _auth.write_kit


def oval_lathe_fuselage(
    stations: list[tuple[float, float, float, float]],
    *,
    segments: int = 28,
) -> tuple[np.ndarray, np.ndarray]:
    """stations: (z, radius_x, radius_y, y_center) — oval cabin cross-section."""
    segs = max(12, segments)
    rings = []
    for z, rx, ry, cy in stations:
        ring = []
        for i in range(segs):
            ang = 2.0 * np.pi * i / segs
            ring.append([rx * np.cos(ang), cy + ry * np.sin(ang), z])
        rings.append(np.asarray(ring, np.float32))

    verts: list = []
    indices: list = []
    for r in range(len(rings) - 1):
        for i in range(segs):
            j = (i + 1) % segs
            a, b = rings[r][i], rings[r][j]
            c, d = rings[r + 1][j], rings[r + 1][i]
            base = len(verts)
            verts.extend([a, b, c, d])
            indices.extend([base, base + 1, base + 2, base, base + 2, base + 3])

    for ring, z_sign in ((rings[0], -1.0), (rings[-1], 1.0)):
        tip = ring.mean(axis=0).copy()
        tip[2] += z_sign * 0.12
        for i in range(segs):
            j = (i + 1) % segs
            base = len(verts)
            if z_sign < 0:
                verts.extend([tip, ring[j], ring[i]])
            else:
                verts.extend([tip, ring[i], ring[j]])
            indices.extend([base, base + 1, base + 2])

    return np.asarray(verts, np.float32), np.asarray(indices, np.uint16)


def windscreen_pane(
    cx: float,
    cy: float,
    cz: float,
    width: float,
    height: float,
    depth: float,
    *,
    pitch_deg: float,
) -> tuple[np.ndarray, np.ndarray]:
    """Thin pane tilted about local X (pitch) for readable cockpit glass."""
    verts, indices = box(cx, cy, cz, width, height, depth)
    pitch = np.deg2rad(pitch_deg)
    ca, sa = float(np.cos(pitch)), float(np.sin(pitch))
    out = verts.copy()
    for i, v in enumerate(verts):
        y, z = v[1] - cy, v[2] - cz
        out[i, 1] = cy + y * ca - z * sa
        out[i, 2] = cz + y * sa + z * ca
    return out, indices


def airfoil_wing(
    cx: float,
    cy: float,
    cz: float,
    span: float,
    root_chord: float,
    tip_chord: float,
    root_t: float,
    tip_t: float,
    *,
    side: float,
    dihedral: float = 0.04,
) -> tuple[np.ndarray, np.ndarray]:
    """Tapered wing with simple airfoil thickness and slight dihedral."""
    x0, x1 = cx, cx + side * span
    y1 = cy + abs(span) * dihedral
    z_rf, z_ra = cz + root_chord * 0.35, cz - root_chord * 0.65
    z_tf, z_ta = cz + tip_chord * 0.3, cz - tip_chord * 0.7
    hr, ht = root_t / 2, tip_t / 2
    corners = np.array(
        [
            [x0, cy - hr, z_ra],
            [x0, cy - hr * 0.4, z_rf],
            [x1, y1 - ht * 0.4, z_tf],
            [x1, y1 - ht, z_ta],
            [x0, cy + hr, z_ra],
            [x0, cy + hr * 0.55, z_rf],
            [x1, y1 + ht * 0.55, z_tf],
            [x1, y1 + ht, z_ta],
        ],
        dtype=np.float32,
    )
    faces = [
        (0, 1, 2, 3),
        (4, 7, 6, 5),
        (0, 4, 5, 1),
        (3, 2, 6, 7),
        (1, 5, 6, 2),
        (0, 3, 7, 4),
    ]
    verts: list = []
    indices: list = []
    for a, b, c, d in faces:
        base = len(verts)
        verts.extend([corners[a], corners[b], corners[c], corners[d]])
        indices.extend([base, base + 1, base + 2, base, base + 2, base + 3])
    return np.asarray(verts, np.float32), np.asarray(indices, np.uint16)


def six_blade_set(cx: float, cy: float, cz: float, prefix: str) -> dict:
    """Six tapered blades at 60° — Batch F AIR-001 requirement."""
    meshes = {}
    suffixes = ["", "_b", "_c", "_d", "_e", "_f"]
    for i, suf in enumerate(suffixes):
        ang = i * 60.0
        meshes[f"{prefix}{suf}"] = prop_blade(
            cx, cy, cz, ang, length=1.22, root_chord=0.14, tip_chord=0.038, thickness=0.032
        )
        # Yellow tip markers (REF-005 / packet readable props)
        tip = prop_blade(
            cx, cy, cz, ang, length=0.22, root_chord=0.04, tip_chord=0.03, thickness=0.028
        )
        # Shift tip mesh outward along blade
        a = np.deg2rad(ang)
        tip_v, tip_i = tip
        tip_v = tip_v.copy()
        tip_v[:, 0] += (-np.sin(a)) * 1.05
        tip_v[:, 1] += (np.cos(a)) * 1.05
        meshes[f"{prefix}_tip{suf}"] = (tip_v, tip_i)
    return meshes


def turboprop_v05_meshes() -> dict[str, tuple[np.ndarray, np.ndarray]]:
    # Oval lathed cabin — REF-003/005 high-wing regional silhouette (not cuboid densify).
    fuselage = {
        "fuselage": oval_lathe_fuselage(
            [
                (5.55, 0.14, 0.14, 1.02),
                (5.25, 0.22, 0.24, 1.05),
                (4.9, 0.34, 0.36, 1.1),
                (4.45, 0.46, 0.48, 1.14),
                (3.9, 0.56, 0.58, 1.18),
                (3.25, 0.64, 0.66, 1.2),
                (2.55, 0.7, 0.72, 1.2),
                (1.75, 0.74, 0.74, 1.19),
                (0.9, 0.76, 0.75, 1.18),
                (0.05, 0.76, 0.75, 1.18),
                (-0.8, 0.74, 0.73, 1.16),
                (-1.6, 0.7, 0.7, 1.14),
                (-2.35, 0.64, 0.64, 1.12),
                (-3.05, 0.54, 0.54, 1.08),
                (-3.65, 0.42, 0.42, 1.04),
                (-4.15, 0.3, 0.3, 1.0),
                (-4.55, 0.18, 0.18, 0.96),
            ],
            segments=28,
        ),
        "belly_fairing": cylinder(0, 0.5, 0.15, 0.4, 4.4, axis="z", segments=18),
        "radome": cylinder(0, 1.12, 5.15, 0.2, 0.5, axis="z", segments=16),
        "cockpit": oval_lathe_fuselage(
            [
                (4.35, 0.48, 0.42, 1.35),
                (3.95, 0.52, 0.48, 1.42),
                (3.45, 0.5, 0.46, 1.4),
                (3.05, 0.42, 0.38, 1.32),
            ],
            segments=20,
        ),
        "cockpit_frame": box(0, 1.78, 3.7, 1.05, 0.07, 1.25),
        "cockpit_glare": box(0, 1.68, 4.15, 0.9, 0.22, 0.1),
        "windscreen_c": windscreen_pane(0, 1.62, 4.05, 0.55, 0.38, 0.04, pitch_deg=-28),
        "windscreen_l": windscreen_pane(-0.38, 1.58, 3.95, 0.32, 0.36, 0.04, pitch_deg=-22),
        "windscreen_r": windscreen_pane(0.38, 1.58, 3.95, 0.32, 0.36, 0.04, pitch_deg=-22),
        "windscreen_pillar_l": box(-0.45, 1.68, 4.0, 0.05, 0.42, 0.5),
        "windscreen_pillar_r": box(0.45, 1.68, 4.0, 0.05, 0.42, 0.5),
        "windscreen_pillar_c": box(0, 1.72, 4.15, 0.04, 0.4, 0.35),
        "cabin_window_band": box(0, 1.4, 0.35, 1.52, 0.1, 4.4),
    }

    # Readable cabin windows both sides
    for i, z in enumerate((2.15, 1.35, 0.55, -0.25, -1.05, -1.85, -2.55)):
        w = 0.42 if i < 6 else 0.32
        h = 0.28 if i < 6 else 0.22
        fuselage[f"cabin_window_{i + 1}"] = box(-0.76, 1.4, z, 0.04, h, w)
        fuselage[f"cabin_window_r{i + 1}"] = box(0.76, 1.4, z, 0.04, h, w)
        if i % 2 == 0:
            fuselage[f"cabin_window_frame_{i + 1}"] = box(-0.78, 1.4, z, 0.035, h + 0.06, w + 0.08)
            fuselage[f"cabin_window_frame_r{i + 1}"] = box(0.78, 1.4, z, 0.035, h + 0.06, w + 0.08)

    fuselage.update(
        {
            "livery_stripe": box(0, 1.02, 0.4, 1.56, 0.12, 6.0),
            "livery_stripe_lower": box(0, 0.78, 0.3, 1.52, 0.07, 5.6),
            "livery_tail_sweep": box(0, 1.55, -3.2, 0.2, 1.6, 1.4),
            "door_frame_fwd": box(-0.74, 1.12, 2.2, 0.12, 1.2, 1.35),
            "door_handle_fwd": box(-0.8, 1.08, 2.5, 0.05, 0.1, 0.08),
            "inspection_panel_fwd": box(0.74, 1.0, 1.55, 0.04, 0.42, 0.65),
            "inspection_panel_aft": box(-0.74, 0.95, -2.05, 0.04, 0.38, 0.55),
            "cargo_sill": box(0.74, 0.55, -1.5, 0.08, 0.08, 1.55),
        }
    )

    wings = {
        "wing_left": airfoil_wing(-0.65, 1.22, 0.35, 6.85, 2.05, 0.78, 0.18, 0.08, side=-1),
        "wing_right": airfoil_wing(0.65, 1.22, 0.35, 6.85, 2.05, 0.78, 0.18, 0.08, side=1),
        "wing_root_left": box(-1.4, 1.18, 0.4, 1.8, 0.26, 1.65),
        "wing_root_right": box(1.4, 1.18, 0.4, 1.8, 0.26, 1.65),
        "wing_fairing_left": box(-1.95, 1.05, 0.5, 1.15, 0.2, 1.15),
        "wing_fairing_right": box(1.95, 1.05, 0.5, 1.15, 0.2, 1.15),
        "flap_left": box(-3.35, 1.14, -0.45, 3.1, 0.06, 0.45),
        "flap_right": box(3.35, 1.14, -0.45, 3.1, 0.06, 0.45),
        "flap_track_l1": box(-2.5, 1.05, -0.62, 0.07, 0.12, 0.32),
        "flap_track_l2": box(-3.7, 1.05, -0.62, 0.07, 0.12, 0.32),
        "flap_track_r1": box(2.5, 1.05, -0.62, 0.07, 0.12, 0.32),
        "flap_track_r2": box(3.7, 1.05, -0.62, 0.07, 0.12, 0.32),
        "flap_fairing_l": box(-3.35, 1.0, -0.28, 2.5, 0.09, 0.22),
        "flap_fairing_r": box(3.35, 1.0, -0.28, 2.5, 0.09, 0.22),
        "spoiler_left": box(-3.5, 1.3, 0.0, 2.5, 0.035, 0.32),
        "spoiler_right": box(3.5, 1.3, 0.0, 2.5, 0.035, 0.32),
        "aileron_left": box(-6.25, 1.2, 0.1, 1.75, 0.05, 0.48),
        "aileron_right": box(6.25, 1.2, 0.1, 1.75, 0.05, 0.48),
        "winglet_left": box(-7.55, 1.55, 0.4, 0.09, 0.55, 0.38),
        "winglet_right": box(7.55, 1.55, 0.4, 0.09, 0.55, 0.38),
        "wing_fence_left": box(-4.7, 1.35, 0.5, 0.05, 0.26, 0.85),
        "wing_fence_right": box(4.7, 1.35, 0.5, 0.05, 0.26, 0.85),
        "wing_fence_mid_l": box(-2.9, 1.32, 0.45, 0.045, 0.2, 0.65),
        "wing_fence_mid_r": box(2.9, 1.32, 0.45, 0.045, 0.2, 0.65),
        "static_wick_left": box(-7.7, 1.22, 0.12, 0.035, 0.035, 0.26),
        "static_wick_right": box(7.7, 1.22, 0.12, 0.035, 0.035, 0.26),
        "pitot": box(0.16, 1.38, 4.65, 0.035, 0.035, 0.32),
        "pitot_b": box(-0.18, 1.35, 4.6, 0.03, 0.03, 0.26),
        "vor_antenna": box(0, 0.32, -1.15, 0.48, 0.04, 0.04),
    }

    engines = {
        "engine_left": cylinder(-2.45, 0.95, 1.05, 0.4, 2.2, axis="z", segments=18),
        "engine_right": cylinder(2.45, 0.95, 1.05, 0.4, 2.2, axis="z", segments=18),
        "pylon_left": box(-2.45, 1.12, 0.75, 0.38, 0.38, 1.45),
        "pylon_right": box(2.45, 1.12, 0.75, 0.38, 0.38, 1.45),
        "nacelle_left": cylinder(-2.45, 0.58, 0.55, 0.3, 1.25, axis="z", segments=16),
        "nacelle_right": cylinder(2.45, 0.58, 0.55, 0.3, 1.25, axis="z", segments=16),
        "intake_left": cylinder(-2.45, 1.05, 2.1, 0.3, 0.38, axis="z", segments=16),
        "intake_right": cylinder(2.45, 1.05, 2.1, 0.3, 0.38, axis="z", segments=16),
        "exhaust_left": cylinder(-2.45, 0.78, -0.25, 0.17, 0.48, axis="z", segments=12),
        "exhaust_right": cylinder(2.45, 0.78, -0.25, 0.17, 0.48, axis="z", segments=12),
        "exhaust_stack_l": box(-2.6, 0.62, -0.4, 0.12, 0.16, 0.32),
        "exhaust_stack_r": box(2.6, 0.62, -0.4, 0.12, 0.16, 0.32),
        "oil_cooler_l": box(-2.45, 0.62, 1.25, 0.48, 0.16, 0.55),
        "oil_cooler_r": box(2.45, 0.62, 1.25, 0.48, 0.16, 0.55),
        "cowl_flap_l": box(-2.45, 0.78, 1.65, 0.52, 0.07, 0.32),
        "cowl_flap_r": box(2.45, 0.78, 1.65, 0.52, 0.07, 0.32),
        "spinner_left": cylinder(-2.45, 0.95, 2.5, 0.17, 0.4, axis="z", segments=16),
        "spinner_right": cylinder(2.45, 0.95, 2.5, 0.17, 0.4, axis="z", segments=16),
        "spinner_stripe_l": cylinder(-2.45, 0.95, 2.58, 0.175, 0.05, axis="z", segments=16),
        "spinner_stripe_r": cylinder(2.45, 0.95, 2.58, 0.175, 0.05, axis="z", segments=16),
        "prop_hub_left": cylinder(-2.45, 0.95, 2.35, 0.13, 0.2, axis="z", segments=14),
        "prop_hub_right": cylinder(2.45, 0.95, 2.35, 0.13, 0.2, axis="z", segments=14),
        "hub_cap_left": cylinder(-2.45, 0.95, 2.62, 0.08, 0.1, axis="z", segments=12),
        "hub_cap_right": cylinder(2.45, 0.95, 2.62, 0.08, 0.1, axis="z", segments=12),
    }
    engines.update(six_blade_set(-2.45, 0.95, 2.32, "propeller_left"))
    engines.update(six_blade_set(2.45, 0.95, 2.32, "propeller_right"))

    empennage = {
        "tail_fin": box(0, 2.55, -3.75, 0.11, 2.35, 1.55),
        "tail_fin_tip": box(0, 3.6, -3.45, 0.09, 0.32, 0.65),
        "tailplane": box(0, 1.85, -3.95, 3.6, 0.09, 1.05),
        "tailplane_tip_l": box(-1.95, 1.88, -3.95, 0.32, 0.1, 0.65),
        "tailplane_tip_r": box(1.95, 1.88, -3.95, 0.32, 0.1, 0.65),
        "elevator_left": box(-1.15, 1.82, -4.35, 1.35, 0.045, 0.42),
        "elevator_right": box(1.15, 1.82, -4.35, 1.35, 0.045, 0.42),
        "rudder": box(0, 2.55, -4.45, 0.08, 1.7, 0.48),
        "dorsal_fin": box(0, 1.85, -2.85, 0.08, 0.55, 0.9),
        "hf_antenna": box(0, 2.2, -2.15, 0.035, 0.035, 1.55),
        "tail_nav_light": box(0, 3.7, -3.15, 0.07, 0.07, 0.07),
    }

    gear = {
        "gear_nose": box(0, 0.4, 3.2, 0.11, 0.75, 0.3),
        "gear_left": box(-1.2, 0.34, -0.3, 0.11, 0.75, 0.4),
        "gear_right": box(1.2, 0.34, -0.3, 0.11, 0.75, 0.4),
        "gear_oleo_nose": cylinder(0, 0.36, 3.2, 0.045, 0.58, axis="y", segments=10),
        "gear_oleo_left": cylinder(-1.2, 0.32, -0.3, 0.045, 0.58, axis="y", segments=10),
        "gear_oleo_right": cylinder(1.2, 0.32, -0.3, 0.045, 0.58, axis="y", segments=10),
        "gear_scissors_nose": box(0, 0.48, 3.05, 0.055, 0.32, 0.18),
        "gear_scissors_left": box(-1.2, 0.42, -0.45, 0.055, 0.32, 0.2),
        "gear_scissors_right": box(1.2, 0.42, -0.45, 0.055, 0.32, 0.2),
        "gear_door_nose": box(0, 0.58, 3.2, 0.52, 0.045, 0.68),
        "gear_door_left": box(-1.2, 0.58, -0.3, 0.62, 0.045, 0.82),
        "gear_door_right": box(1.2, 0.58, -0.3, 0.62, 0.045, 0.82),
        # tire_* nests under gear; wheel_* names preserved as packet contract aliases (rims).
        "tire_nose": cylinder(0, 0.12, 3.2, 0.13, 0.18, axis="x", segments=16),
        "tire_left": cylinder(-1.2, 0.12, -0.3, 0.15, 0.17, axis="x", segments=16),
        "tire_right": cylinder(1.2, 0.12, -0.3, 0.15, 0.17, axis="x", segments=16),
        "wheel_nose": cylinder(0, 0.12, 3.2, 0.07, 0.1, axis="x", segments=12),
        "wheel_left": cylinder(-1.2, 0.12, -0.3, 0.08, 0.09, axis="x", segments=12),
        "wheel_right": cylinder(1.2, 0.12, -0.3, 0.08, 0.09, axis="x", segments=12),
        "rim_nose": cylinder(0, 0.12, 3.2, 0.05, 0.06, axis="x", segments=10),
        "rim_left": cylinder(-1.2, 0.12, -0.3, 0.055, 0.055, axis="x", segments=10),
        "rim_right": cylinder(1.2, 0.12, -0.3, 0.055, 0.055, axis="x", segments=10),
        "door_fwd": box(-0.74, 1.12, 2.2, 0.06, 1.05, 1.2),
        "cargo_door": box(0.74, 1.02, -1.5, 0.06, 0.95, 1.55),
        "cargo_door_latch": box(0.8, 1.02, -1.15, 0.045, 0.14, 0.1),
        "antenna": box(0, 2.1, 1.15, 0.04, 0.48, 0.04),
        "antenna_aft": box(0, 2.0, -1.75, 0.035, 0.32, 0.035),
        "nav_light_left": box(-7.55, 1.25, 0.45, 0.09, 0.09, 0.09),
        "nav_light_right": box(7.55, 1.25, 0.45, 0.09, 0.09, 0.09),
        "beacon_top": box(0, 3.05, -3.4, 0.1, 0.1, 0.1),
        "landing_light_l": box(-2.6, 1.05, 2.15, 0.15, 0.09, 0.09),
        "landing_light_r": box(2.6, 1.05, 2.15, 0.15, 0.09, 0.09),
        "taxi_light": box(0, 0.58, 3.6, 0.13, 0.08, 0.09),
    }

    meshes: dict[str, tuple[np.ndarray, np.ndarray]] = {}
    for part in (fuselage, wings, engines, empennage, gear):
        meshes.update(part)
    return meshes


def main() -> None:
    AIRCRAFT.mkdir(parents=True, exist_ok=True)
    meshes = turboprop_v05_meshes()
    write_kit(AIRCRAFT, BASENAME, meshes)
    print(f"AIR-001 v05 ready: {BASENAME} ({len(meshes)} meshes)")


if __name__ == "__main__":
    main()
