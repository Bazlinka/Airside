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

# Repo-relative so this runs on any checkout. It previously hard-coded
# /workspace paths from the container it was first written in, which meant it
# could not be re-run on a developer machine at all.
SCRIPTS = Path(__file__).resolve().parent
REPO = SCRIPTS.parent
ROOT = REPO / "game" / "Airside" / "Assets" / "Airside" / "Art"
AIRCRAFT = ROOT / "Models" / "Aircraft"
BASENAME = "mdl_regional_turboprop_01_v05"

_SPEC = importlib.util.spec_from_file_location(
    "authored_fbx", SCRIPTS / "generate-authored-fbx-turboprop-terminal.py"
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


# --- Wing planform -----------------------------------------------------------
# Single source of truth for the wing surface. Everything mounted on the wing is
# derived from this rather than hard-coded, because hard-coding is exactly how
# they drifted: dihedral was added to the wing and every flap, aileron, spoiler,
# track, fairing and static wick stayed at its old flat-wing height, leaving them
# floating 9-23 cm below the surface with clear air in between.
WING = {
    "root_x": 0.65,
    "tip_x": 7.5,
    "root_y": 1.22,
    "dihedral": 0.04,
    "cz": 0.35,
    "root_chord": 2.05,
    "tip_chord": 0.78,
    "root_thickness": 0.18,
    "tip_thickness": 0.08,
    "root_le_frac": 0.35,
    "tip_le_frac": 0.30,
}


# Main gear station. Matches nacelle_left/right (x=+-2.45, z centred 0.55) so the
# leg runs up inside the nacelle instead of ending in mid-air.
MAIN_GEAR_X = 2.45
MAIN_GEAR_Z = 0.35


def _lerp(a: float, b: float, t: float) -> float:
    return a + (b - a) * t


def wing_station(x_abs: float) -> tuple[float, float, float, float]:
    """Wing geometry at spanwise station |x|.

    Returns (chord_line_y, leading_edge_z, chord, thickness). Stations outboard
    of the tip clamp to the tip, so tip-mounted parts (winglet, wick, nav light)
    can pass their own x without special-casing.
    """
    w = WING
    span = w["tip_x"] - w["root_x"]
    reach = min(max(abs(x_abs), w["root_x"]), w["tip_x"])
    t = (reach - w["root_x"]) / span
    y = w["root_y"] + (reach - w["root_x"]) * w["dihedral"]
    chord = _lerp(w["root_chord"], w["tip_chord"], t)
    le_frac = _lerp(w["root_le_frac"], w["tip_le_frac"], t)
    thickness = _lerp(w["root_thickness"], w["tip_thickness"], t)
    return y, w["cz"] + chord * le_frac, chord, thickness


def naca_half_thickness(x_norm: np.ndarray, thickness_ratio: float) -> np.ndarray:
    """NACA 4-digit symmetric half-thickness over normalised chord 0 (LE) to 1 (TE)."""
    t = thickness_ratio
    x = np.asarray(x_norm, np.float64)
    yt = 5.0 * t * (
        0.2969 * np.sqrt(np.clip(x, 0.0, None))
        - 0.1260 * x
        - 0.3516 * x**2
        + 0.2843 * x**3
        - 0.1015 * x**4
    )
    yt[-1] = 0.0  # close the trailing edge exactly
    return yt


def _orient_outward(
    verts: np.ndarray, indices: np.ndarray
) -> tuple[np.ndarray, np.ndarray]:
    """Flip triangle winding if a closed hull came out inside-out.

    ArtGltfLoader calls RecalculateNormals, so winding alone decides which way a
    surface faces. Deriving it from the signed volume keeps that correct whatever
    sign or side the caller passed, instead of relying on the argument order.
    """
    tris = indices.reshape(-1, 3).astype(np.int64)
    a, b, c = verts[tris[:, 0]], verts[tris[:, 1]], verts[tris[:, 2]]
    volume = float(np.einsum("ij,ij->i", a, np.cross(b, c)).sum()) / 6.0
    if volume < 0.0:
        indices = np.ascontiguousarray(tris[:, ::-1]).reshape(-1).astype(np.uint16)
    return verts, indices


def _section_loop(
    span_c: float,
    offset_c: float,
    z_le: float,
    chord: float,
    thickness: float,
    chord_points: int,
    vertical: bool,
) -> np.ndarray:
    """One closed aerofoil outline: upper LE->TE then lower TE->LE."""
    beta = np.linspace(0.0, np.pi, chord_points)
    xn = (1.0 - np.cos(beta)) / 2.0  # cosine spacing packs points at the leading edge
    yt = naca_half_thickness(xn, thickness / chord) * chord
    z = z_le - xn * chord
    zs = np.concatenate([z, z[-2:0:-1]])
    offs = np.concatenate([offset_c + yt, (offset_c - yt)[-2:0:-1]])
    pts = np.empty((len(zs), 3), np.float32)
    if vertical:
        pts[:, 0] = offs
        pts[:, 1] = span_c
    else:
        pts[:, 0] = span_c
        pts[:, 1] = offs
    pts[:, 2] = zs
    return pts


def lofted_aerofoil(
    stations: list[tuple[float, float, float, float, float]],
    *,
    chord_points: int = 16,
    vertical: bool = False,
) -> tuple[np.ndarray, np.ndarray]:
    """Loft a real aerofoil section between stations.

    stations: (span_coord, offset_coord, leading_edge_z, chord, thickness).
    `vertical` swaps the span and thickness axes, for fins.

    Replaces the previous 8-corner tapered slab, which carried only two chordwise
    points and so read as a flat plank rather than a wing.
    """
    loops = [_section_loop(*st, chord_points, vertical) for st in stations]
    n = len(loops[0])
    verts: list = []
    indices: list = []
    for r in range(len(loops) - 1):
        inner, outer = loops[r], loops[r + 1]
        for i in range(n):
            j = (i + 1) % n
            base = len(verts)
            verts.extend([inner[i], inner[j], outer[j], outer[i]])
            indices.extend([base, base + 1, base + 2, base, base + 2, base + 3])
    for ring in (loops[0], loops[-1]):
        centre = ring.mean(axis=0)
        for i in range(n):
            j = (i + 1) % n
            base = len(verts)
            verts.extend([centre, ring[i], ring[j]])
            indices.extend([base, base + 1, base + 2])
    return _orient_outward(
        np.asarray(verts, np.float32), np.asarray(indices, np.uint16)
    )


def wing_slab(
    x_in: float,
    x_out: float,
    side: float,
    *,
    from_te: float,
    to_te: float,
    thickness: float,
    surface: str = "chord",
    chord_frac: bool = False,
) -> tuple[np.ndarray, np.ndarray]:
    """A control surface / fairing / fence that tracks the wing.

    `from_te` and `to_te` are distances forward of the *local* trailing edge (or
    fractions of local chord when `chord_frac`), so a part follows chord taper and
    leading-edge sweep as well as dihedral. `surface` sits it on the chord line,
    or flush against the upper or lower skin at its own mid-chord.
    """
    rings = []
    for x_abs in (x_in, x_out):
        y, z_le, chord, thick_abs = wing_station(x_abs)
        z_te = z_le - chord
        a, b = (from_te * chord, to_te * chord) if chord_frac else (from_te, to_te)
        z_a, z_b = z_te + a, z_te + b
        if surface == "chord":
            centre = y
        else:
            xn = min(max((z_le - (z_a + z_b) / 2.0) / chord, 0.0), 1.0)
            skin = float(
                naca_half_thickness(np.array([xn, 1.0]), thick_abs / chord)[0]
            ) * chord
            reach = skin + thickness / 2.0
            centre = y + reach if surface == "upper" else y - reach
        rings.append((side * x_abs, centre, z_a, z_b))

    (x0, y0, z0a, z0b), (x1, y1, z1a, z1b) = rings
    h = thickness / 2.0
    corners = np.array(
        [
            [x0, y0 - h, z0a],
            [x0, y0 - h, z0b],
            [x1, y1 - h, z1b],
            [x1, y1 - h, z1a],
            [x0, y0 + h, z0a],
            [x0, y0 + h, z0b],
            [x1, y1 + h, z1b],
            [x1, y1 + h, z1a],
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
    for a_i, b_i, c_i, d_i in faces:
        base = len(verts)
        verts.extend([corners[a_i], corners[b_i], corners[c_i], corners[d_i]])
        indices.extend([base, base + 1, base + 2, base, base + 2, base + 3])
    return _orient_outward(
        np.asarray(verts, np.float32), np.asarray(indices, np.uint16)
    )


def wing_aerofoil(side: float) -> tuple[np.ndarray, np.ndarray]:
    """The main wing as a lofted aerofoil, root to tip."""
    root_y, root_le, root_c, root_t = wing_station(WING["root_x"])
    tip_y, tip_le, tip_c, tip_t = wing_station(WING["tip_x"])
    return lofted_aerofoil(
        [
            (side * WING["root_x"], root_y, root_le, root_c, root_t),
            (side * WING["tip_x"], tip_y, tip_le, tip_c, tip_t),
        ]
    )


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
        # Lofted aerofoil, not a tapered plank (see lofted_aerofoil). Same planform
        # numbers as before, so nothing mounted on the wing shifts.
        "wing_left": wing_aerofoil(-1),
        "wing_right": wing_aerofoil(1),
        "wing_root_left": box(-1.4, 1.18, 0.4, 1.8, 0.26, 1.65),
        "wing_root_right": box(1.4, 1.18, 0.4, 1.8, 0.26, 1.65),
        "wing_fairing_left": box(-1.95, 1.05, 0.5, 1.15, 0.2, 1.15),
        "wing_fairing_right": box(1.95, 1.05, 0.5, 1.15, 0.2, 1.15),
        # Everything below rides the wing surface via wing_slab, so it tracks
        # dihedral, chord taper and leading-edge sweep instead of sitting at a
        # fixed height the wing no longer has.
        "flap_left": wing_slab(1.8, 4.9, -1, from_te=0.0, to_te=0.45, thickness=0.06),
        "flap_right": wing_slab(1.8, 4.9, 1, from_te=0.0, to_te=0.45, thickness=0.06),
        "flap_track_l1": wing_slab(2.465, 2.535, -1, from_te=-0.14, to_te=0.18,
                                   thickness=0.12, surface="lower"),
        "flap_track_l2": wing_slab(3.665, 3.735, -1, from_te=-0.14, to_te=0.18,
                                   thickness=0.12, surface="lower"),
        "flap_track_r1": wing_slab(2.465, 2.535, 1, from_te=-0.14, to_te=0.18,
                                   thickness=0.12, surface="lower"),
        "flap_track_r2": wing_slab(3.665, 3.735, 1, from_te=-0.14, to_te=0.18,
                                   thickness=0.12, surface="lower"),
        "flap_fairing_l": wing_slab(2.1, 4.6, -1, from_te=0.02, to_te=0.24,
                                    thickness=0.09, surface="lower"),
        "flap_fairing_r": wing_slab(2.1, 4.6, 1, from_te=0.02, to_te=0.24,
                                    thickness=0.09, surface="lower"),
        "spoiler_left": wing_slab(2.25, 4.75, -1, from_te=0.45, to_te=0.77,
                                  thickness=0.035, surface="upper"),
        "spoiler_right": wing_slab(2.25, 4.75, 1, from_te=0.45, to_te=0.77,
                                   thickness=0.035, surface="upper"),
        "aileron_left": wing_slab(5.375, 7.125, -1, from_te=0.0, to_te=0.48, thickness=0.05),
        "aileron_right": wing_slab(5.375, 7.125, 1, from_te=0.0, to_te=0.48, thickness=0.05),
        "winglet_left": wing_slab(7.455, 7.545, -1, from_te=0.45, to_te=1.0,
                                  thickness=0.55, surface="upper", chord_frac=True),
        "winglet_right": wing_slab(7.455, 7.545, 1, from_te=0.45, to_te=1.0,
                                   thickness=0.55, surface="upper", chord_frac=True),
        "wing_fence_left": wing_slab(4.675, 4.725, -1, from_te=0.35, to_te=1.0,
                                     thickness=0.26, surface="upper", chord_frac=True),
        "wing_fence_right": wing_slab(4.675, 4.725, 1, from_te=0.35, to_te=1.0,
                                      thickness=0.26, surface="upper", chord_frac=True),
        "wing_fence_mid_l": wing_slab(2.878, 2.922, -1, from_te=0.4, to_te=0.95,
                                      thickness=0.2, surface="upper", chord_frac=True),
        "wing_fence_mid_r": wing_slab(2.878, 2.922, 1, from_te=0.4, to_te=0.95,
                                      thickness=0.2, surface="upper", chord_frac=True),
        "static_wick_left": wing_slab(7.38, 7.42, -1, from_te=-0.26, to_te=0.02,
                                      thickness=0.035),
        "static_wick_right": wing_slab(7.38, 7.42, 1, from_te=-0.26, to_te=0.02,
                                       thickness=0.035),
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
        # Lofted sections; planform (span, chord, position) unchanged so the
        # rudder, tip and beacon stay attached where they were placed.
        "tail_fin": lofted_aerofoil(
            [(1.375, 0.0, -2.975, 1.55, 0.11), (3.725, 0.0, -2.975, 1.55, 0.11)],
            vertical=True,
        ),
        "tail_fin_tip": box(0, 3.6, -3.45, 0.09, 0.32, 0.65),
        "tailplane": lofted_aerofoil(
            [(-1.8, 1.85, -3.425, 1.05, 0.09), (1.8, 1.85, -3.425, 1.05, 0.09)]
        ),
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
        # Main gear moved from x=+-1.2 / z=-0.3 to under the nacelles
        # (x=+-MAIN_GEAR_X, z=MAIN_GEAR_Z). It used to hang 44 cm below the wing
        # and 44 cm outboard of the fuselage, attached to nothing at all -- the
        # aircraft read as hovering on disconnected stilts. The leg now runs up
        # into the nacelle, which is where a turboprop's main gear retracts.
        "gear_left": box(-MAIN_GEAR_X, 0.34, MAIN_GEAR_Z, 0.11, 0.75, 0.4),
        "gear_right": box(MAIN_GEAR_X, 0.34, MAIN_GEAR_Z, 0.11, 0.75, 0.4),
        "gear_oleo_nose": cylinder(0, 0.36, 3.2, 0.045, 0.58, axis="y", segments=10),
        "gear_oleo_left": cylinder(-MAIN_GEAR_X, 0.32, MAIN_GEAR_Z, 0.045, 0.58, axis="y", segments=10),
        "gear_oleo_right": cylinder(MAIN_GEAR_X, 0.32, MAIN_GEAR_Z, 0.045, 0.58, axis="y", segments=10),
        "gear_scissors_nose": box(0, 0.48, 3.05, 0.055, 0.32, 0.18),
        "gear_scissors_left": box(-MAIN_GEAR_X, 0.42, MAIN_GEAR_Z - 0.15, 0.055, 0.32, 0.2),
        "gear_scissors_right": box(MAIN_GEAR_X, 0.42, MAIN_GEAR_Z - 0.15, 0.055, 0.32, 0.2),
        "gear_door_nose": box(0, 0.58, 3.2, 0.52, 0.045, 0.68),
        "gear_door_left": box(-MAIN_GEAR_X, 0.58, MAIN_GEAR_Z, 0.62, 0.045, 0.82),
        "gear_door_right": box(MAIN_GEAR_X, 0.58, MAIN_GEAR_Z, 0.62, 0.045, 0.82),
        # tire_* nests under gear; wheel_* names preserved as packet contract aliases (rims).
        "tire_nose": cylinder(0, 0.12, 3.2, 0.13, 0.18, axis="x", segments=16),
        "tire_left": cylinder(-MAIN_GEAR_X, 0.12, MAIN_GEAR_Z, 0.15, 0.17, axis="x", segments=16),
        "tire_right": cylinder(MAIN_GEAR_X, 0.12, MAIN_GEAR_Z, 0.15, 0.17, axis="x", segments=16),
        # wheel_* / rim_* are narrower and shorter than the tire that encloses
        # them, so they were never visible. Stepped out past the tire tread so the
        # hub actually reads, keeping the packet's contract names.
        "wheel_nose": cylinder(0, 0.12, 3.2, 0.075, 0.2, axis="x", segments=12),
        "wheel_left": cylinder(-MAIN_GEAR_X, 0.12, MAIN_GEAR_Z, 0.085, 0.19, axis="x", segments=12),
        "wheel_right": cylinder(MAIN_GEAR_X, 0.12, MAIN_GEAR_Z, 0.085, 0.19, axis="x", segments=12),
        "rim_nose": cylinder(0, 0.12, 3.2, 0.05, 0.21, axis="x", segments=10),
        "rim_left": cylinder(-MAIN_GEAR_X, 0.12, MAIN_GEAR_Z, 0.055, 0.2, axis="x", segments=10),
        "rim_right": cylinder(MAIN_GEAR_X, 0.12, MAIN_GEAR_Z, 0.055, 0.2, axis="x", segments=10),
        "door_fwd": box(-0.74, 1.12, 2.2, 0.06, 1.05, 1.2),
        "cargo_door": box(0.74, 1.02, -1.5, 0.06, 0.95, 1.55),
        "cargo_door_latch": box(0.8, 1.02, -1.15, 0.045, 0.14, 0.1),
        "antenna": box(0, 2.1, 1.15, 0.04, 0.48, 0.04),
        "antenna_aft": box(0, 2.0, -1.75, 0.035, 0.32, 0.035),
        # On the wing tip, touching it (was 24 cm below the tip and 5 cm outboard
        # of it, so the lamp floated free of the wing entirely).
        "nav_light_left": box(-7.5, 1.494, 0.45, 0.09, 0.09, 0.09),
        "nav_light_right": box(7.5, 1.494, 0.45, 0.09, 0.09, 0.09),
        "beacon_top": box(0, 3.05, -3.4, 0.1, 0.1, 0.1),
        # Were buried entirely inside intake_left/right and could never be seen,
        # while UpdateAircraftLightsAndGear drives their emission. Moved into the
        # wing leading edge, where a turboprop carries them.
        "landing_light_l": wing_slab(2.45, 2.75, -1, from_te=0.90, to_te=1.0,
                                     thickness=0.09, surface="lower", chord_frac=True),
        "landing_light_r": wing_slab(2.45, 2.75, 1, from_te=0.90, to_te=1.0,
                                     thickness=0.09, surface="lower", chord_frac=True),
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
