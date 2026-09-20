#!/usr/bin/env python3
"""AIR-001 v03: controlled visual-fidelity pass for the ATR 42-600 starter.

Project-owned geometry. Leaves the approved v02 kit untouched and writes a new
`mdl_atr42_starter_v03` kit. Exact 22.67 × 24.57 × 7.59 m envelope, centred
regional-aircraft root, tyre contact at y = 0, and v01/v02 moving-part names are
preserved. Does not modify Q400, Saab or 737 assets.
"""

from __future__ import annotations

import importlib.util
import uuid
from pathlib import Path

import numpy as np

SCRIPTS = Path(__file__).resolve().parent
REPO = SCRIPTS.parent
AIRCRAFT = REPO / "game" / "Airside" / "Assets" / "Airside" / "Art" / "Models" / "Aircraft"
BASENAME = "mdl_atr42_starter_v03"

TARGET_LENGTH = 22.67
TARGET_SPAN = 24.57
TARGET_HEIGHT = 7.59
PROP_DIAMETER = 3.93

_spec = importlib.util.spec_from_file_location(
    "atr_v02", SCRIPTS / "generate-air-001-atr42-v02.py"
)
v02 = importlib.util.module_from_spec(_spec)
assert _spec.loader is not None
_spec.loader.exec_module(v02)
v01 = v02.v01

_SKIN_SPEC = importlib.util.spec_from_file_location(
    "airside_aircraft_skin", SCRIPTS / "aircraft_skin.py"
)
skin = importlib.util.module_from_spec(_SKIN_SPEC)
assert _SKIN_SPEC.loader is not None
_SKIN_SPEC.loader.exec_module(skin)
_box = v01._v05.box

# Smoother nose → cabin blend; tip-to-tip z still spans TARGET_LENGTH.
HALF = TARGET_LENGTH * 0.5
STATIONS = np.array(
    [
        (-HALF + 0.001, 0.05, 0.04, 2.22),
        (-10.05, 0.22, 0.17, 2.21),
        (-9.45, 0.46, 0.35, 2.17),
        (-8.70, 0.74, 0.58, 2.08),
        (-7.85, 1.02, 0.86, 1.98),
        (-7.00, 1.24, 1.16, 1.90),
        (-6.15, 1.35, 1.31, 1.85),
        (-4.70, 1.40, 1.36, 1.84),
        (4.70, 1.40, 1.36, 1.84),
        (6.40, 1.395, 1.35, 1.84),
        (7.35, 1.38, 1.32, 1.84),
        # Soft brow into the flight deck — no angular step.
        (8.15, 1.34, 1.27, 1.81),
        (8.85, 1.28, 1.18, 1.74),
        (9.55, 1.18, 1.05, 1.64),
        (10.20, 1.04, 0.90, 1.50),
        (10.80, 0.86, 0.72, 1.36),
        (11.30, 0.64, 0.52, 1.24),
        (11.70, 0.40, 0.32, 1.16),
        (11.98, 0.20, 0.16, 1.11),
        (HALF - 0.001, 0.05, 0.045, 1.09),
    ],
    dtype=np.float32,
)


def surface(z, theta, offset=0.0):
    rx, ry, cy = [np.interp(z, STATIONS[:, 0], STATIONS[:, i]) for i in (1, 2, 3)]
    return np.array(
        [(rx + offset) * np.cos(theta), cy + (ry + offset) * np.sin(theta), z],
        np.float32,
    )


def _skin(z, angle_deg, offset=0.0):
    """Fuselage skin in the shared (z, degrees, offset) form used by aircraft_skin."""
    return surface(z, np.deg2rad(angle_deg), offset)


def fuselage_body():
    zs = np.concatenate(
        [
            np.linspace(a, b, 3, endpoint=False)
            for a, b in zip(STATIONS[:-1, 0], STATIONS[1:, 0])
        ]
        + [[float(STATIONS[-1, 0])]]
    )
    thetas = np.linspace(0.0, 2.0 * np.pi, 72, endpoint=False)
    verts = np.array(
        [surface(float(z), float(t)) for z in zs for t in thetas], np.float32
    )
    faces: list[int] = []
    rings = len(zs)
    segs = len(thetas)
    for j in range(rings - 1):
        for i in range(segs):
            a = j * segs + i
            b = j * segs + (i + 1) % segs
            c = b + segs
            d = a + segs
            faces.extend([a, b, c, a, c, d])
    for ring, tip_y, tip_z in (
        (0, float(STATIONS[0, 3]), float(zs[0])),
        (rings - 1, float(STATIONS[-1, 3]), float(zs[-1])),
    ):
        centre = len(verts)
        verts = np.vstack([verts, [0.0, tip_y, tip_z]]).astype(np.float32)
        for i in range(segs):
            edge = [ring * segs + i, ring * segs + (i + 1) % segs]
            if ring == 0:
                edge.reverse()
            faces.extend([centre, *edge])
    return verts, np.asarray(faces, np.uint16)


def surface_quad(corners, offset=0.022):
    sampled = np.asarray(
        [surface(z, np.deg2rad(theta), offset) for z, theta in corners], np.float64
    )
    centre = sampled.mean(axis=0)
    _, _, basis = np.linalg.svd(sampled - centre, full_matrices=False)
    normal = basis[-1]
    mean_theta = np.deg2rad(np.mean([theta for _, theta in corners]))
    outward = np.array([np.cos(mean_theta), np.sin(mean_theta), 0.0])
    if np.dot(normal, outward) < 0:
        normal = -normal
    front = sampled - ((sampled - centre) @ normal)[:, None] * normal
    front = front + normal * 0.009
    back = front - normal * 0.011
    verts = np.vstack([front, back]).astype(np.float32)
    count = len(corners)
    faces: list[int] = []
    for i in range(1, count - 1):
        faces.extend([0, i, i + 1, count, count + i + 1, count + i])
    for i in range(count):
        j = (i + 1) % count
        faces.extend([i, count + j, j, i, count + i, count + j])
    return verts, np.asarray(faces, np.uint16)


def fitted_cockpit_panel(corners, offset=0.018):
    """Thin glazing that follows the local nose curvature without flattening it."""
    sampled = np.asarray(
        [surface(z, np.deg2rad(theta), offset) for z, theta in corners], np.float32
    )
    normal = np.cross(sampled[1] - sampled[0], sampled[2] - sampled[0])
    normal /= np.linalg.norm(normal)
    mean_theta = np.deg2rad(np.mean([theta for _, theta in corners]))
    outward = np.array([np.cos(mean_theta), np.sin(mean_theta), 0.0])
    if np.dot(normal, outward) < 0.0:
        normal = -normal
    front = sampled + normal * 0.004
    back = sampled - normal * 0.008
    verts = np.vstack((front, back)).astype(np.float32)
    indices = np.asarray(
        [0, 1, 2, 0, 2, 3, 4, 6, 5, 4, 7, 6,
         0, 4, 5, 0, 5, 1, 1, 5, 6, 1, 6, 2,
         2, 6, 7, 2, 7, 3, 3, 7, 4, 3, 4, 0],
        dtype=np.uint16,
    )
    return verts, indices


def pillar_strip(z0, z1, theta0, theta1, offset=0.018):
    """Opaque body-colour strip between glazing panes (credible mullion)."""
    return surface_quad(
        [(z0, theta0), (z0, theta1), (z1, theta1), (z1, theta0)],
        offset=offset,
    )


def oval_pod(stations, segments=48):
    return v01._v05.oval_lathe_fuselage(stations, segments=segments)


def engine_pod(x):
    pod = oval_pod(
        [
            (-1.72, 0.12, 0.10, 2.78),
            (-1.28, 0.36, 0.32, 2.80),
            (-0.55, 0.54, 0.50, 2.82),
            (0.35, 0.64, 0.58, 2.84),
            (1.55, 0.67, 0.61, 2.85),
            (3.35, 0.62, 0.56, 2.84),
            (4.45, 0.46, 0.42, 2.83),
            (5.05, 0.18, 0.16, 2.81),
        ],
        segments=52,
    )
    return v01.translated(pod, x, 0.0, 0.0)


def wing_nacelle_blend(x):
    fair = oval_pod(
        [
            (-0.55, 0.10, 0.08, 3.00),
            (0.15, 0.28, 0.18, 3.08),
            (0.95, 0.34, 0.22, 3.14),
            (1.75, 0.22, 0.14, 3.10),
            (2.25, 0.08, 0.05, 3.04),
        ],
        segments=32,
    )
    return v01.translated(fair, x, 0.0, 0.0)


def gear_sponson(side):
    """Fuselage-side main-gear blister — distinct from Q400 nacelle-mounted gear."""
    fair = oval_pod(
        [
            (0.55, 0.28, 0.34, 1.42),
            (-0.05, 0.40, 0.46, 1.46),
            (-0.70, 0.44, 0.50, 1.48),
            (-1.25, 0.32, 0.38, 1.50),
            (-1.65, 0.14, 0.18, 1.48),
        ],
        segments=36,
    )
    return v01.translated(fair, side * 1.22, 0.0, 0.15)


def write_default_importer_meta(path: Path) -> None:
    path.write_text(
        "fileFormatVersion: 2\n"
        f"guid: {uuid.uuid4().hex}\n"
        "DefaultImporter:\n"
        "  externalObjects: {}\n"
        "  userData:\n"
        "  assetBundleName:\n"
        "  assetBundleVariant:\n"
    )


def final_meshes():
    meshes = v02.final_meshes()

    # --- Fuselage: smoother nose into cabin, same envelope ---
    meshes["fuselage"] = fuselage_body()

    # --- Fitted cockpit glazing, cabin windows and doors ---
    # Everything below is sampled from the fuselage skin (aircraft_skin.py): the old flat
    # quads sank into the curve, which drew the cockpit as jagged dark shards, the windows
    # as chunky blocks with oversized dark "frame" slabs, and the door outlines as stray slats.
    for name in list(meshes):
        if name.startswith(("cockpit_", "windscreen_", "cabin_window_", "door_", "cargo_door")):
            del meshes[name]

    def pane(z, angle, half_len, half_arc, front, radius=0.07):
        return skin.skin_patch(_skin, z, angle, half_len, half_arc,
                               front=front, radius=radius, rings=2, max_edge=0.12)

    meshes["windscreen_l"] = pane(9.25, 104.5, 0.55, 0.22, 0.014)
    meshes["windscreen_r"] = pane(9.25, 75.5, 0.55, 0.22, 0.014)
    meshes["cockpit_side_l"] = pane(8.95, 133.0, 0.55, 0.27, 0.012)
    meshes["cockpit_side_r"] = pane(8.95, 47.0, 0.55, 0.27, 0.012)
    for name, (z, angle, hl, ha) in {
        "windscreen_pillar_c": (9.25, 90.0, 0.56, 0.045),
        "windscreen_pillar_l": (9.10, 118.5, 0.60, 0.045),
        "windscreen_pillar_r": (9.10, 61.5, 0.60, 0.045),
        "cockpit_sill_l": (9.10, 150.0, 0.75, 0.04),
        "cockpit_sill_r": (9.10, 30.0, 0.75, 0.04),
        "cockpit_glare_l": (9.95, 115.0, 0.22, 0.07),
        "cockpit_glare_r": (9.95, 65.0, 0.22, 0.07),
    }.items():
        meshes[name] = skin.skin_patch(_skin, z, angle, hl, ha, front=0.010, radius=0.03,
                                       rings=0, corner_segments=2, max_edge=0.2)

    # Doors: forward-left passenger door and the rear-right cargo door, curved with the skin.
    meshes["door_outline_fwd"], meshes["door_fwd"], meshes["door_handle_fwd"] = skin.door_set(
        _skin, 6.25, 180.0, 0.39, 0.64)
    meshes["cargo_door_outline"], meshes["cargo_door"], meshes["cargo_door_latch"] = skin.door_set(
        _skin, -3.25, 0.0, 0.77, 0.69)

    # Windows: tall rounded panes at the 0.51 m frame pitch, ~0.35 m above the cabin axis. Two
    # panes share a node. The right-side band skips the cargo door.
    def cargo_zone(z):
        return -4.02 - 0.40 < z < -2.48 + 0.40

    def pair(z, side):
        angle = 180.0 - 14.0 if side < 0 else 14.0
        panes = [skin.window(_skin, z - k * 0.508, angle, width=0.24, height=0.34)
                 for k in range(2) if not (side > 0 and cargo_zone(z - k * 0.508))]
        return skin.merge_meshes(panes) if panes else None

    index = {-1: 0, 1: 0}
    for z in np.arange(5.35, -5.05, -1.016):
        for side, suffix in ((-1, ""), (1, "r")):
            merged = pair(float(z), side)
            if merged is None:
                continue
            index[side] += 1
            meshes[f"cabin_window_{suffix}{index[side]}"] = merged

    # --- Compact nacelles blended into the high wing ---
    for name in list(meshes):
        if name.startswith(("engine_", "nacelle_", "pylon_", "intake_", "exhaust_")):
            meshes.pop(name, None)
    for side, label in ((-1, "left"), (1, "right")):
        x = side * 3.99
        meshes[f"engine_{label}"] = engine_pod(x)
        meshes[f"intake_{label}"] = v01._v05.cylinder(
            x, 2.83, 4.92, 0.36, 0.11, axis="z", segments=36
        )
        meshes[f"exhaust_{label}"] = v01._v05.cylinder(
            x, 2.80, -1.48, 0.13, 0.46, axis="z", segments=28
        )
        meshes[f"pylon_{label}"] = v01.panel(x, 3.10, 0.85, 0.42, 0.28, 1.55)
        meshes[f"pylon_{label}_blend"] = wing_nacelle_blend(x)

    # --- Fuselage-side main-gear sponsons ---
    meshes["gear_fairing_left"] = gear_sponson(-1)
    meshes["gear_fairing_right"] = gear_sponson(1)

    # --- Wing-root saddles ---
    for side, label in ((-1, "left"), (1, "right")):
        fairing = oval_pod(
            [
                (-1.85, 0.12, 0.08, 2.92),
                (-1.25, 0.38, 0.22, 3.02),
                (0.15, 0.48, 0.28, 3.12),
                (1.35, 0.40, 0.24, 3.10),
                (1.95, 0.14, 0.09, 3.00),
            ],
            segments=36,
        )
        meshes[f"wing_root_{label}"] = v01.translated(fairing, side * 1.10, 0.0, 0.0)

    # --- Fin / T-tail connections ---
    meshes["tail_fin"] = v02.profile_prism(
        [
            (-9.70, 2.18),
            (-6.05, 2.68),
            (-6.35, 3.35),
            (-6.75, 4.25),
            (-7.15, 5.25),
            (-7.50, 6.25),
            (-7.82, 7.25),
            (-7.98, 7.59),
            (-8.85, 7.59),
            (-9.55, 4.80),
            (-9.85, 2.55),
        ],
        0.15,
    )
    meshes["tail_root_fairing"] = v02.profile_prism(
        [
            (-10.05, 2.10),
            (-9.35, 2.95),
            (-8.15, 3.55),
            (-6.55, 3.05),
            (-5.65, 2.45),
            (-5.85, 2.20),
            (-8.55, 2.15),
        ],
        0.28,
    )
    if "tailplane" in meshes:
        shift = 7.57 - float(meshes["tailplane"][0][:, 1].max())
        for name in ("tailplane", "elevator_left", "elevator_right"):
            if name in meshes:
                meshes[name] = v01.move_y(meshes[name], shift)
    meshes["tailplane_saddle"] = oval_pod(
        [
            (-9.60, 0.10, 0.04, 7.40),
            (-9.08, 0.40, 0.10, 7.46),
            (-8.28, 0.70, 0.12, 7.46),
            (-7.42, 0.60, 0.10, 7.45),
            (-6.60, 0.24, 0.05, 7.42),
        ],
        segments=38,
    )

    for name in list(meshes):
        if name.startswith("inspection_panel"):
            del meshes[name]

    # --- Props: six readable 3.93 m blades with connected hubs ---
    for name in list(meshes):
        if name.startswith(("propeller_", "spinner_", "prop_hub_", "hub_cap_")):
            del meshes[name]
    suffixes = ("", "_b", "_c", "_d", "_e", "_f")
    prop_y = 2.82
    prop_z = 5.58
    for side, side_name in ((-1.0, "left"), (1.0, "right")):
        x = side * 3.99
        for blade_index, suffix in enumerate(suffixes):
            angle = blade_index * 60.0
            meshes[f"propeller_{side_name}{suffix}"] = v01._v06.propeller_blade(
                x, prop_y, prop_z, angle, radial_start=0.20, radial_end=1.78
            )
            meshes[f"propeller_{side_name}_tip{suffix}"] = v01._v06.propeller_blade(
                x,
                prop_y,
                prop_z,
                angle,
                radial_start=1.74,
                radial_end=PROP_DIAMETER * 0.5,
                tip=True,
            )
        spinner = oval_pod(
            [
                (5.24, 0.30, 0.30, prop_y),
                (5.70, 0.36, 0.36, prop_y),
                (6.05, 0.04, 0.04, prop_y),
            ],
            segments=38,
        )
        meshes[f"spinner_{side_name}"] = v01.translated(spinner, x, 0.0, 0.0)
        meshes[f"prop_hub_{side_name}"] = v01._v05.cylinder(
            x, prop_y, 5.52, 0.24, 0.28, axis="z", segments=32
        )
        meshes[f"hub_cap_{side_name}"] = v01._v05.cylinder(
            x, prop_y, 5.88, 0.12, 0.12, axis="z", segments=28
        )

    pre_envelope_fuselage = meshes["fuselage"][0].copy()
    # Enforce tyre contact and exact envelope after edits.
    all_verts = np.concatenate([v for v, _ in meshes.values()])
    y_min = float(all_verts[:, 1].min())
    if abs(y_min) > 1e-4:
        for name, (verts, idx) in list(meshes.items()):
            shifted = verts.copy()
            shifted[:, 1] -= y_min
            meshes[name] = (shifted, idx)
        all_verts = np.concatenate([v for v, _ in meshes.values()])

    span, height, length = np.ptp(all_verts, axis=0)
    sx = TARGET_SPAN / float(span) if abs(span - TARGET_SPAN) > 0.002 else 1.0
    sy = TARGET_HEIGHT / float(height) if abs(height - TARGET_HEIGHT) > 0.002 else 1.0
    sz = TARGET_LENGTH / float(length) if abs(length - TARGET_LENGTH) > 0.002 else 1.0
    if sx != 1.0 or sy != 1.0 or sz != 1.0:
        scale = np.array([sx, sy, sz], np.float32)
        for name, (verts, idx) in list(meshes.items()):
            meshes[name] = (verts * scale, idx)
        all_verts = np.concatenate([v for v, _ in meshes.values()])
        y_min = float(all_verts[:, 1].min())
        for name, (verts, idx) in list(meshes.items()):
            shifted = verts.copy()
            shifted[:, 1] -= y_min
            meshes[name] = (shifted, idx)

    # --- Seat every light, probe and wick on the airframe (they hovered or sat buried) ---
    # These use final-model coordinates, so they go in after the envelope scaling above. The
    # fuselage's own before/after vertices give the transform for anything placed on its skin.
    post = meshes["fuselage"][0]
    fit = [np.polyfit(pre_envelope_fuselage[:, axis], post[:, axis], 1) for axis in range(3)]

    def to_final(point):
        return np.array([fit[i][0] * point[i] + fit[i][1] for i in range(3)], np.float64)

    meshes["nav_light_left"] = _box(-12.22, 3.24, 0.45, 0.13, 0.12, 0.13)
    meshes["nav_light_right"] = _box(12.22, 3.24, 0.45, 0.13, 0.12, 0.13)
    meshes["static_wick_left"] = _box(-12.20, 3.24, -0.26, 0.05, 0.03, 0.18)
    meshes["static_wick_right"] = _box(12.20, 3.24, -0.26, 0.05, 0.03, 0.18)
    for name, angle in (("pitot", 184.0), ("pitot_b", -4.0)):
        base = to_final(_skin(9.10, angle, 0.0))
        meshes[name] = _box(float(base[0]), float(base[1]), float(base[2]) + 0.30, 0.035, 0.035, 0.66)
    meshes["beacon_top"] = _box(0.0, 3.24, -3.0, 0.15, 0.15, 0.20)
    meshes["landing_light_l"] = _box(-2.60, 2.93, 1.30, 0.40, 0.11, 0.20)
    meshes["landing_light_r"] = _box(2.60, 2.93, 1.30, 0.40, 0.11, 0.20)
    meshes["taxi_light"] = _box(0.0, 1.00, 7.94, 0.22, 0.12, 0.19)
    meshes["gear_door_nose"] = _box(0.0, 0.57, 7.60, 0.54, 0.06, 1.20)   # hangs on the belly

    return {name: v01._v06.outward_winding(mesh) for name, mesh in meshes.items()}


def main():
    AIRCRAFT.mkdir(parents=True, exist_ok=True)
    meshes = final_meshes()
    try:
        v01._v05.write_kit(AIRCRAFT, BASENAME, meshes)
    except Exception:
        v01._v05._auth.pack_gltf(AIRCRAFT / f"{BASENAME}.gltf", meshes)
        for ext in (".gltf", ".bin"):
            meta = AIRCRAFT / f"{BASENAME}{ext}.meta"
            if not meta.exists():
                write_default_importer_meta(meta)

    for ext in (".gltf", ".bin", ".fbx"):
        meta = AIRCRAFT / f"{BASENAME}{ext}.meta"
        if (AIRCRAFT / f"{BASENAME}{ext}").exists() and not meta.exists():
            write_default_importer_meta(meta)

    verts = np.concatenate([v for v, _ in meshes.values()])
    tris = sum(len(i) // 3 for _, i in meshes.values())
    print(
        f"{BASENAME}: {len(meshes)} parts; bounds {np.ptp(verts, axis=0)}; "
        f"{tris} triangles; y_min={verts[:, 1].min():.4f}"
    )


if __name__ == "__main__":
    main()
