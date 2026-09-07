#!/usr/bin/env python3
"""Generate the Batch C v03 aircraft kit: lofted geometry instead of boxes.

v01/v02 built every part from axis-aligned cubes, so the "regional turboprop" was
29 boxes with no curvature and proportions (15.05 span x 9.82 long) that no
turboprop has. This generator adds real shape primitives -- lathed bodies of
revolution, tapered aerofoil slabs, swept fins -- and rebuilds the aircraft to
ATR-72 proportions taken from REF-005, scaled so the wingspan still matches the
hangar it parks beside.

Output stays in the POSITION + uint16 index format ArtGltfLoader understands:
it recalculates normals and generates planar UVs at load, so lathed parts come
out smooth and slab parts come out flat-shaded.

Run:  python3 scripts/generate-batch-c-models-v03.py
Then: scripts/sync-art-streaming-assets.sh
"""

from __future__ import annotations

import json
import math
import uuid
from pathlib import Path

import numpy as np

ROOT = Path(__file__).resolve().parent.parent / "game/Airside/Assets/Airside/Art"

# ---------------------------------------------------------------------------
# Proportions
#
# ATR-72-600: span 27.05 m, length 27.17 m, height 7.65 m, fuselage dia 2.865 m,
# propeller dia 3.93 m. The kit keeps the v02 wingspan (~15) so it still reads at
# the same size against the hangar and terminal, and everything else is derived
# from the real aircraft rather than eyeballed.
# ---------------------------------------------------------------------------
SPAN = 15.0
K = SPAN / 27.05           # metres of real aircraft per kit unit
LENGTH = 27.17 * K         # ~15.07
HEIGHT = 7.65 * K          # ~4.24
FUSELAGE_R = 2.865 * K / 2  # ~0.79
PROP_R = 3.93 * K / 2      # ~1.09

# The airfield has a 7-wide runway, a 4-wide taxiway and stands 6 apart. Fit the
# aircraft to that: a 5.6 span sits on the runway with the small overhang a real
# turboprop has and leaves clearance between adjacent stands. The building kits are
# ~2.7x too large for this airfield and want their own pass (see GAME.md); when the
# airfield grows to meet them, raise this one number.
TARGET_SPAN = 5.6
OUTPUT_SCALE = TARGET_SPAN / SPAN

HALF_SPAN = SPAN / 2
NOSE_Z = LENGTH / 2
TAIL_Z = -LENGTH / 2
# Height of the fuselage centreline above the ground line (y = 0 is the tarmac).
BODY_Y = 1.55


# ---------------------------------------------------------------------------
# Mesh primitives
# ---------------------------------------------------------------------------
def _tris(quads: list[tuple[int, int, int, int]]) -> list[int]:
    out: list[int] = []
    for a, b, c, d in quads:
        out.extend([a, b, c, a, c, d])
    return out


def lathe(sections, segments: int = 20, cap_ends: bool = True):
    """Body of revolution about the Z axis.

    `sections` is a list of (z, radius_x, radius_y, y_centre). Rings share their
    vertices so RecalculateNormals gives a smooth, round surface -- this is what
    makes a fuselage read as a fuselage rather than a crate.
    """
    verts: list[list[float]] = []
    quads: list[tuple[int, int, int, int]] = []
    ring_start: list[int] = []

    for z, rx, ry, cy in sections:
        ring_start.append(len(verts))
        if rx <= 1e-5 and ry <= 1e-5:
            verts.append([0.0, cy, z])          # pole
            continue
        for s in range(segments):
            a = 2.0 * math.pi * s / segments
            verts.append([math.cos(a) * rx, cy + math.sin(a) * ry, z])

    def ring_len(index: int) -> int:
        nxt = ring_start[index + 1] if index + 1 < len(ring_start) else len(verts)
        return nxt - ring_start[index]

    for i in range(len(sections) - 1):
        a0, b0 = ring_start[i], ring_start[i + 1]
        na, nb = ring_len(i), ring_len(i + 1)
        if na == 1 and nb == 1:
            continue
        if na == 1:                              # pole fan forward
            for s in range(segments):
                quads.append((a0, b0 + s, b0 + (s + 1) % segments, b0 + s))
            continue
        if nb == 1:                              # fan into pole
            for s in range(segments):
                quads.append((a0 + s, a0 + (s + 1) % segments, b0, b0))
            continue
        for s in range(segments):
            t = (s + 1) % segments
            quads.append((a0 + s, a0 + t, b0 + t, b0 + s))

    if cap_ends:
        for index in (0, len(sections) - 1):
            n = ring_len(index)
            if n <= 2:
                continue
            base = ring_start[index]
            centre = len(verts)
            z = sections[index][0]
            verts.append([0.0, sections[index][3], z])
            for s in range(n):
                t = (s + 1) % n
                if index == 0:
                    quads.append((centre, base + t, base + s, centre))
                else:
                    quads.append((centre, base + s, base + t, centre))

    return np.asarray(verts, np.float32), np.asarray(_tris(quads), np.uint16)


def _aerofoil(chord: float, thickness: float, points: int = 8):
    """Half-decent symmetric aerofoil section in the (chord, thickness) plane."""
    out = []
    for i in range(points + 1):                  # upper surface, LE -> TE
        t = i / points
        out.append((t * chord, _thk(t) * thickness))
    for i in range(points - 1, 0, -1):           # lower surface, TE -> LE
        t = i / points
        out.append((t * chord, -_thk(t) * thickness))
    return out


def _thk(t: float) -> float:
    # NACA-ish half-thickness distribution, normalised to 1 at max.
    return 2.9 * (0.2969 * math.sqrt(max(t, 0.0)) - 0.126 * t - 0.3516 * t * t
                  + 0.2843 * t ** 3 - 0.1015 * t ** 4)


def aerofoil_surface(root_chord, tip_chord, half_span, sweep, thickness,
                     dihedral, root_x, direction, y, z_le, vertical=False):
    """A tapered, swept aerofoil panel lofted from root to tip.

    `direction` is +1/-1 along X for a wing, ignored for a vertical fin (which is
    lofted up the Y axis instead). Sections are duplicated per quad so the panel
    keeps crisp flat-shaded facets rather than smearing across its edges.
    """
    stations = 6
    rings = []
    for i in range(stations + 1):
        f = i / stations
        chord = root_chord + (tip_chord - root_chord) * f
        thick = thickness * (1.0 - 0.45 * f)
        section = _aerofoil(chord, thick)
        offset = half_span * f
        ring = []
        for c, t in section:
            cz = z_le - c - sweep * f
            if vertical:
                ring.append([root_x, y + offset, cz])
                ring[-1][0] = root_x + t
            else:
                ring.append([root_x + direction * offset,
                             y + dihedral * f + t,
                             cz])
        rings.append(ring)

    verts: list[list[float]] = []
    quads: list[tuple[int, int, int, int]] = []
    n = len(rings[0])
    for i in range(stations):
        for s in range(n):
            t = (s + 1) % n
            base = len(verts)
            verts.extend([rings[i][s], rings[i][t], rings[i + 1][t], rings[i + 1][s]])
            quads.append((base, base + 1, base + 2, base + 3))
    # Cap the tip.
    base = len(verts)
    verts.extend(rings[-1])
    for s in range(1, n - 1):
        quads.append((base, base + s, base + s + 1, base))
    return np.asarray(verts, np.float32), np.asarray(_tris(quads), np.uint16)


def blades(count: int, radius: float, root_w: float, tip_w: float, thickness: float,
           cx: float, cy: float, cz: float, phase: float = 0.0):
    """A propeller hub of `count` tapered, twisted blades in the XY plane."""
    verts: list[list[float]] = []
    quads: list[tuple[int, int, int, int]] = []
    hub = 0.16
    for b in range(count):
        a = phase + 2.0 * math.pi * b / count
        ca, sa = math.cos(a), math.sin(a)
        stations = 4
        rings = []
        for i in range(stations + 1):
            f = i / stations
            r = hub + (radius - hub) * f
            w = root_w + (tip_w - root_w) * f
            twist = math.radians(24.0 * (1.0 - f))   # coarse at the root
            th = thickness * (1.0 - 0.55 * f)
            ring = []
            for sx, sz in ((-w / 2, -th / 2), (w / 2, -th / 2), (w / 2, th / 2), (-w / 2, th / 2)):
                # rotate the section by the twist about the blade's own radial axis
                rz = sz * math.cos(twist) - sx * math.sin(twist) * 0.0
                rx = sx * math.cos(twist)
                ring.append([cx + ca * r - sa * rx, cy + sa * r + ca * rx, cz + rz])
            rings.append(ring)
        for i in range(stations):
            for s in range(4):
                t = (s + 1) % 4
                base = len(verts)
                verts.extend([rings[i][s], rings[i][t], rings[i + 1][t], rings[i + 1][s]])
                quads.append((base, base + 1, base + 2, base + 3))
        base = len(verts)
        verts.extend(rings[-1])
        quads.append((base, base + 1, base + 2, base + 3))
    return np.asarray(verts, np.float32), np.asarray(_tris(quads), np.uint16)


def merge(*meshes):
    """Combine several meshes into one part (e.g. a window band per side)."""
    verts: list = []
    indices: list = []
    for v, i in meshes:
        base = len(verts)
        verts.extend(np.asarray(v, np.float32).tolist())
        indices.extend((np.asarray(i, np.int32) + base).tolist())
    return np.asarray(verts, np.float32), np.asarray(indices, np.uint16)


def band(z0, z1, radius, y_centre, a0_deg, a1_deg, segments=10):
    """A curved strip hugging a fuselage of the given radius (window bands, doors)."""
    verts: list[list[float]] = []
    quads: list[tuple[int, int, int, int]] = []
    for s in range(segments):
        f0 = s / segments
        f1 = (s + 1) / segments
        a0 = math.radians(a0_deg + (a1_deg - a0_deg) * f0)
        a1 = math.radians(a0_deg + (a1_deg - a0_deg) * f1)
        p0 = (math.cos(a0) * radius, y_centre + math.sin(a0) * radius)
        p1 = (math.cos(a1) * radius, y_centre + math.sin(a1) * radius)
        base = len(verts)
        verts.extend([[p0[0], p0[1], z0], [p1[0], p1[1], z0],
                      [p1[0], p1[1], z1], [p0[0], p0[1], z1]])
        quads.append((base, base + 1, base + 2, base + 3))
    return np.asarray(verts, np.float32), np.asarray(_tris(quads), np.uint16)


def tube(cx, cy, cz, radius, length, axis="z", segments=12):
    """Round strut/leg/tyre."""
    sections = []
    steps = 2
    for i in range(steps + 1):
        f = i / steps
        sections.append((cz - length / 2 + length * f, radius, radius, 0.0))
    verts, indices = lathe(sections, segments=segments)
    verts = np.asarray(verts, np.float32).copy()
    if axis == "y":
        verts = verts[:, [0, 2, 1]] * np.array([1, 1, 1], np.float32)
        verts[:, 1] -= cz
        verts[:, 1] += cy
        verts[:, 2] += cz - cz
    elif axis == "x":
        verts = verts[:, [2, 1, 0]]
        verts[:, 0] += cx - cz
    if axis == "y":
        verts[:, 0] += cx
        verts[:, 2] += cz
    elif axis == "x":
        verts[:, 1] += cy
        verts[:, 2] += cz
    else:
        verts[:, 0] += cx
        verts[:, 1] += cy
    return verts, indices


# ---------------------------------------------------------------------------
# The aircraft
# ---------------------------------------------------------------------------
def turboprop() -> dict:
    r = FUSELAGE_R
    fuselage_sections = [
        (NOSE_Z - 0.05, 0.06, 0.06, BODY_Y - 0.10),
        (NOSE_Z - 0.55, 0.34, 0.32, BODY_Y - 0.08),
        (NOSE_Z - 1.25, 0.60, 0.58, BODY_Y - 0.04),
        (NOSE_Z - 2.10, 0.74, 0.73, BODY_Y),
        (NOSE_Z - 3.10, r, r, BODY_Y),
        (0.0, r, r, BODY_Y),
        (TAIL_Z + 4.60, r * 0.96, r * 0.96, BODY_Y + 0.04),
        (TAIL_Z + 3.10, r * 0.82, r * 0.80, BODY_Y + 0.26),
        (TAIL_Z + 1.70, r * 0.55, r * 0.52, BODY_Y + 0.66),
        (TAIL_Z + 0.60, r * 0.26, r * 0.24, BODY_Y + 1.00),
        (TAIL_Z, 0.05, 0.05, BODY_Y + 1.16),
    ]

    wing_y = BODY_Y + r * 0.86          # high wing, riding on top of the barrel
    wing_le = 1.10
    engine_x = 2.55
    nacelle_y = wing_y - 0.30
    prop_z = 2.95

    fin_root_z = TAIL_Z + 2.70
    fin_height = HEIGHT - (BODY_Y + 0.30)

    parts: dict = {
        "fuselage": lathe(fuselage_sections, segments=22),
        # Nose cone and cockpit glazing sit proud of the barrel so the livery reads.
        "nose": lathe(
            [
                (NOSE_Z - 0.02, 0.05, 0.05, BODY_Y - 0.10),
                (NOSE_Z - 0.50, 0.33, 0.31, BODY_Y - 0.08),
                (NOSE_Z - 1.20, 0.58, 0.56, BODY_Y - 0.04),
            ],
            segments=20,
        ),
        "cockpit": band(NOSE_Z - 2.35, NOSE_Z - 1.15, 0.755, BODY_Y + 0.06, 34, 146, segments=10),
        "cabin_windows": merge(
            band(-4.20, 3.10, r + 0.012, BODY_Y, 8, 30, segments=6),
            band(-4.20, 3.10, r + 0.012, BODY_Y, 150, 172, segments=6)),
        "wing_left": aerofoil_surface(1.45, 0.82, HALF_SPAN - 0.45, 0.30, 0.42, 0.36,
                                      -0.45, -1, wing_y, wing_le),
        "wing_right": aerofoil_surface(1.45, 0.82, HALF_SPAN - 0.45, 0.30, 0.42, 0.36,
                                       0.45, +1, wing_y, wing_le),
        # Engine nacelle: cowl forward of the wing, main-gear fairing behind it.
        "engine_left": lathe(
            [(prop_z - 0.25, 0.24, 0.24, 0.0), (prop_z - 0.60, 0.35, 0.34, 0.0),
             (prop_z - 1.15, 0.38, 0.37, 0.0), (wing_le - 0.05, 0.38, 0.37, 0.0)],
            segments=16),
        "engine_right": lathe(
            [(prop_z - 0.25, 0.24, 0.24, 0.0), (prop_z - 0.60, 0.35, 0.34, 0.0),
             (prop_z - 1.15, 0.38, 0.37, 0.0), (wing_le - 0.05, 0.38, 0.37, 0.0)],
            segments=16),
        "nacelle_left": lathe(
            [(wing_le - 0.05, 0.38, 0.37, 0.0), (wing_le - 0.95, 0.40, 0.44, -0.06),
             (wing_le - 1.70, 0.30, 0.34, -0.10), (wing_le - 2.10, 0.12, 0.14, -0.10)],
            segments=16),
        "nacelle_right": lathe(
            [(wing_le - 0.05, 0.38, 0.37, 0.0), (wing_le - 0.95, 0.40, 0.44, -0.06),
             (wing_le - 1.70, 0.30, 0.34, -0.10), (wing_le - 2.10, 0.12, 0.14, -0.10)],
            segments=16),
        "spinner_left": lathe(
            [(prop_z + 0.50, 0.03, 0.03, 0.0), (prop_z + 0.28, 0.13, 0.13, 0.0),
             (prop_z + 0.04, 0.19, 0.19, 0.0), (prop_z - 0.26, 0.23, 0.23, 0.0)],
            segments=16),
        "spinner_right": lathe(
            [(prop_z + 0.50, 0.03, 0.03, 0.0), (prop_z + 0.28, 0.13, 0.13, 0.0),
             (prop_z + 0.04, 0.19, 0.19, 0.0), (prop_z - 0.26, 0.23, 0.23, 0.0)],
            segments=16),
        # Six blades, delivered as two three-blade hubs 60 degrees apart. The
        # runtime nests "_b" under its parent so the whole disc turns as one.
        "propeller_left": blades(3, PROP_R, 0.30, 0.16, 0.075, 0, 0, prop_z + 0.10),
        "propeller_left_b": blades(3, PROP_R, 0.30, 0.16, 0.075, 0, 0, prop_z + 0.10,
                                   phase=math.pi / 3),
        "propeller_right": blades(3, PROP_R, 0.30, 0.16, 0.075, 0, 0, prop_z + 0.10),
        "propeller_right_b": blades(3, PROP_R, 0.30, 0.16, 0.075, 0, 0, prop_z + 0.10,
                                    phase=math.pi / 3),
        # T-tail: swept fin carrying the tailplane at its top, like the reference.
        "tail_fin": aerofoil_surface(2.25, 1.15, fin_height, 1.00, 0.40, 0.0,
                                     0.0, +1, BODY_Y + 0.30, fin_root_z, vertical=True),
        "rudder": aerofoil_surface(0.62, 0.44, fin_height * 0.88, 0.62, 0.22, 0.0,
                                   0.0, +1, BODY_Y + 0.40, fin_root_z - 2.20, vertical=True),
        # Tailplane rides the fin tip, so it follows the fin's sweep aft.
        "tailplane": aerofoil_surface(0.95, 0.62, 2.75, 0.28, 0.30, 0.08,
                                      0.0, +1, HEIGHT - 0.18, fin_root_z - 1.05),
        "tailplane_left": aerofoil_surface(0.95, 0.62, 2.75, 0.28, 0.30, 0.08,
                                           0.0, -1, HEIGHT - 0.18, fin_root_z - 1.05),
        "door_fwd": band(1.55, 2.95, r + 0.02, BODY_Y - 0.30, 150, 176, segments=4),
        "antenna": aerofoil_surface(0.45, 0.14, 0.34, 0.20, 0.11, 0.0,
                                    0.0, +1, BODY_Y + r * 0.95, 0.4, vertical=True),
    }

    # Mirror the right-hand nacelle/engine/prop parts into place.
    for name, dx in (("engine_left", -engine_x), ("engine_right", engine_x),
                     ("nacelle_left", -engine_x), ("nacelle_right", engine_x),
                     ("spinner_left", -engine_x), ("spinner_right", engine_x),
                     ("propeller_left", -engine_x), ("propeller_left_b", -engine_x),
                     ("propeller_right", engine_x), ("propeller_right_b", engine_x)):
        verts, indices = parts[name]
        verts = np.asarray(verts, np.float32).copy()
        verts[:, 0] += dx
        verts[:, 1] += nacelle_y
        parts[name] = (verts, indices)

    # Undercarriage: nose leg under the flight deck, mains in the nacelles.
    gear = {
        "gear_nose": tube(0.0, 0.52, NOSE_Z - 2.60, 0.09, 1.05, axis="y"),
        "gear_left": tube(-engine_x, 0.55, -0.35, 0.10, 1.10, axis="y"),
        "gear_right": tube(engine_x, 0.55, -0.35, 0.10, 1.10, axis="y"),
        "tire_nose": tube(0.0, 0.30, NOSE_Z - 2.60, 0.30, 0.22, axis="x", segments=14),
        "tire_left": tube(-engine_x, 0.34, -0.35, 0.34, 0.26, axis="x", segments=14),
        "tire_right": tube(engine_x, 0.34, -0.35, 0.34, 0.26, axis="x", segments=14),
    }
    parts.update(gear)
    return parts


# ---------------------------------------------------------------------------
# glTF packing (same on-disk format as v01/v02)
# ---------------------------------------------------------------------------
def new_guid() -> str:
    return uuid.uuid4().hex


def write_default_meta(path: Path) -> None:
    meta = Path(str(path) + ".meta")
    if meta.exists():
        return
    meta.write_text(
        "fileFormatVersion: 2\n"
        f"guid: {new_guid()}\n"
        "DefaultImporter:\n"
        "  externalObjects: {}\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n",
        encoding="utf-8",
    )


def pack_gltf(path: Path, meshes: dict) -> None:
    bin_parts: list[bytes] = []
    buffer_views, accessors, gltf_meshes, nodes = [], [], [], []
    offset = 0
    for name, (verts, indices) in meshes.items():
        v = np.asarray(verts, dtype=np.float32)
        i = np.asarray(indices, dtype=np.uint16)
        if len(v) > 65535:
            raise ValueError(f"{name}: {len(v)} vertices exceeds the uint16 index format")
        v_bytes = v.tobytes()
        v_bytes += b"\x00" * ((4 - (len(v_bytes) % 4)) % 4)
        i_bytes = i.tobytes()
        i_bytes += b"\x00" * ((4 - (len(i_bytes) % 4)) % 4)

        bv_v = len(buffer_views)
        buffer_views.append({"buffer": 0, "byteOffset": offset,
                             "byteLength": len(v) * 12, "target": 34962})
        bin_parts.append(v_bytes)
        accessors.append({"bufferView": bv_v, "componentType": 5126, "count": len(v),
                          "type": "VEC3",
                          "max": v.max(axis=0).tolist(), "min": v.min(axis=0).tolist()})
        offset += len(v_bytes)
        acc_v = len(accessors) - 1

        bv_i = len(buffer_views)
        buffer_views.append({"buffer": 0, "byteOffset": offset,
                             "byteLength": len(i) * 2, "target": 34963})
        bin_parts.append(i_bytes)
        accessors.append({"bufferView": bv_i, "componentType": 5123,
                          "count": len(i), "type": "SCALAR"})
        offset += len(i_bytes)
        acc_i = len(accessors) - 1

        mesh_index = len(gltf_meshes)
        gltf_meshes.append({"name": name, "primitives": [
            {"attributes": {"POSITION": acc_v}, "indices": acc_i, "mode": 4}]})
        nodes.append({"name": name, "mesh": mesh_index})

    bin_path = path.with_suffix(".bin")
    blob = b"".join(bin_parts)
    bin_path.write_bytes(blob)
    doc = {
        "asset": {"version": "2.0", "generator": "Airside Batch C v03 lofted kit"},
        "buffers": [{"uri": bin_path.name, "byteLength": len(blob)}],
        "bufferViews": buffer_views,
        "accessors": accessors,
        "meshes": gltf_meshes,
        "nodes": nodes,
        "scenes": [{"name": path.stem, "nodes": list(range(len(nodes)))}],
        "scene": 0,
    }
    path.write_text(json.dumps(doc, indent=1), encoding="utf-8")
    write_default_meta(path)
    write_default_meta(bin_path)


def main() -> None:
    target = ROOT / "Models" / "Aircraft" / "mdl_regional_turboprop_01_v03.gltf"
    parts = turboprop()
    parts = {name: (np.asarray(v, np.float32) * OUTPUT_SCALE, i)
             for name, (v, i) in parts.items()}
    pack_gltf(target, parts)

    lo = np.array([1e9] * 3)
    hi = np.array([-1e9] * 3)
    tris = 0
    for verts, indices in parts.values():
        v = np.asarray(verts)
        lo = np.minimum(lo, v.min(axis=0))
        hi = np.maximum(hi, v.max(axis=0))
        tris += len(indices) // 3
    print(f"wrote {target.relative_to(ROOT.parent.parent.parent)}")
    print(f"  parts {len(parts)}  triangles {tris}")
    print(f"  span {hi[0] - lo[0]:.2f}  length {hi[2] - lo[2]:.2f}  height {hi[1] - lo[1]:.2f}")
    print(f"  ground line y {lo[1]:.3f} .. {hi[1]:.2f}")


if __name__ == "__main__":
    main()
