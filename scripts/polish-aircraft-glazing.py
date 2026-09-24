#!/usr/bin/env python3
"""Rebuild the thirteen runtime aircraft with fitted, transparent glazing.

The original generators remain the source for each airframe. This finishing pass
opens the skin beneath each pane, adds recessed cabin/cockpit surfaces and two
crew silhouettes, and merges glazing detail by group to protect draw-call cost.
It writes the same glTF, bin and editable FBX paths; sync and render afterwards.
"""

from __future__ import annotations

import argparse
import importlib.util
from pathlib import Path

import numpy as np


HERE = Path(__file__).resolve().parent
ROOT = HERE.parent
AIRCRAFT = ROOT / "game/Airside/Assets/Airside/Art/Models/Aircraft"

# Keep this list in the same order as render-aircraft-thumbnails.py.
SOURCES = {
    "ATR42": ("generate-air-001-atr42-v03.py", "final_meshes", "mdl_atr42_starter_v03"),
    "SF34": ("generate-air-007-saab-340b.py", "saab_meshes", "mdl_saab_340b_v01"),
    "DH8D": ("generate-air-006-dash8-q400.py", "q400_meshes", "mdl_dash8_q400_v01"),
    "E190": ("generate-air-013-e190.py", "e190_meshes", "mdl_e190_v01"),
    "A223": ("generate-air-014-a220-300.py", "a220_300_meshes", "mdl_a220_300_v01"),
    "A320": ("generate-air-adelaide-fleet.py", "airbus_a320_200_meshes", "mdl_a320_200_v01"),
    "B738": ("generate-air-adelaide-fleet.py", "boeing_737_800_meshes", "mdl_737_800_v01"),
    "B38M": ("generate-air-005-narrowbody-737-8.py", "narrowbody_737_8_meshes", "mdl_737_8_narrowbody_v01"),
    "A21N": ("generate-air-008-a321neo.py", "a321neo_meshes", "mdl_a321neo_v01"),
    "A359": ("generate-air-009-a350-900.py", "a350_900_meshes", "mdl_a350_900_v01"),
    "A339": ("generate-air-adelaide-fleet.py", "airbus_a330_900neo_meshes", "mdl_a330_900neo_v01"),
    "B789": ("generate-air-adelaide-fleet.py", "boeing_787_9_meshes", "mdl_787_9_v01"),
    "B78X": ("generate-air-010-787-10.py", "boeing_787_10_meshes", "mdl_787_10_v01"),
}


def load_module(filename: str):
    name = "airside_glazing_" + filename.replace("-", "_").replace(".", "_")
    spec = importlib.util.spec_from_file_location(name, HERE / filename)
    module = importlib.util.module_from_spec(spec)
    assert spec.loader is not None
    spec.loader.exec_module(module)
    return module


def components(vertices: np.ndarray, indices: np.ndarray):
    """Split a two-pane node into its separate closed skin-patch shells."""
    parent = np.arange(len(vertices))

    def root(i):
        while parent[i] != i:
            parent[i] = parent[parent[i]]
            i = parent[i]
        return i

    for tri in indices.reshape(-1, 3):
        a, b, c = (int(v) for v in tri)
        parent[root(b)] = root(a)
        parent[root(c)] = root(a)

    groups: dict[int, list[int]] = {}
    for i in range(len(vertices)):
        groups.setdefault(root(i), []).append(i)
    for group in sorted(groups.values(), key=min):
        start = min(group)
        end = max(group) + 1
        if len(group) != end - start:
            raise ValueError("window component vertices must be contiguous")
        # The generators concatenate complete pane shells without interleaving vertices.
        tris = indices.reshape(-1, 3)
        used = np.all((tris >= start) & (tris < end), axis=1)
        yield vertices[start:end], (tris[used] - start).reshape(-1)


def pane_detail(vertices: np.ndarray, indices: np.ndarray, *, cockpit: bool):
    """Make three surface-following details from one aircraft_skin.skin_patch pane."""
    if len(indices) < 6 or len(vertices) % 2:
        raise ValueError("expected a closed skin-patch pane")
    first = sorted(int(i) for i in indices[:3])
    n = first[-1] - 1  # first quad spans vertices 0, 1, n+1
    front_count = len(vertices) // 2
    if first[:2] != [0, 1] or n < 8 or (front_count - 1) % n:
        raise ValueError("unsupported glazing topology")
    rings = (front_count - 1) // n
    if rings < 2:
        raise ValueError("glazing needs at least an outer and inner surface ring")

    centre = vertices[front_count - 1].astype(np.float64)
    back_centre = vertices[-1].astype(np.float64)
    normal = centre - back_centre
    normal /= np.linalg.norm(normal)
    outer = vertices[:n].astype(np.float64)
    inner = vertices[n:2 * n].astype(np.float64)

    def ring(outer_scale: float, inner_scale: float, offset: float):
        a = centre + (outer - centre) * outer_scale + normal * offset
        b = centre + (outer - centre) * inner_scale + normal * offset
        points = np.vstack((a, b)).astype(np.float32)
        faces = []
        for i in range(n):
            j = (i + 1) % n
            faces.extend((i, j, n + j, i, n + j, n + i))
        return points, np.asarray(faces, np.uint16)

    # Restrained metal reveal and rubber seal, not a raised applique.
    trim = ring(1.055 if not cockpit else 1.025, 0.935 if not cockpit else 0.955, 0.008)
    gasket = ring(0.945 if not cockpit else 0.962, 0.835 if not cockpit else 0.885, 0.011)

    # A short, curved upper/front reflection uses the sampled shell rings. Unlike
    # a flat quad it remains on the nose and cabin curvature at close follow zoom.
    tangent_z = np.array((0.0, 0.0, 1.0)) - normal * normal[2]
    tangent_z /= np.linalg.norm(tangent_z)
    vertical = np.cross(normal, tangent_z)
    if vertical[1] < 0:
        vertical = -vertical
    radial = outer - centre
    u = radial @ tangent_z
    v = radial @ vertical
    u /= max(np.max(np.abs(u)), 1e-6)
    v /= max(np.max(np.abs(v)), 1e-6)
    # Small upper-corner glint; broad cyan bands looked like painted plastic.
    selected = (v > 0.38) & (u > 0.12)
    highlight_vertices = []
    highlight_indices = []
    for i in range(n):
        j = (i + 1) % n
        if not (selected[i] and selected[j]):
            continue
        base = len(highlight_vertices)
        for k in (i, j):
            a = outer[k] * 0.80 + inner[k] * 0.20 + normal * 0.026
            b = outer[k] * 0.60 + inner[k] * 0.40 + normal * 0.026
            highlight_vertices.extend((a, b))
        highlight_indices.extend((base, base + 2, base + 3, base, base + 3, base + 1))
    reflection = (np.asarray(highlight_vertices, np.float32),
                  np.asarray(highlight_indices, np.uint16)) if highlight_vertices else None

    # The dark cabin/flight deck lies behind an actual skin opening. It stops the
    # opposite white fuselage shining through the translucent outer glazing.
    face_tris = indices.reshape(-1, 3)
    face_tris = face_tris[np.all(face_tris < front_count, axis=1)]
    depth = 0.43 if cockpit else 0.13
    interior = (vertices[:front_count] - normal * depth,
                face_tris.reshape(-1).astype(np.uint16))
    return trim, gasket, reflection, interior, (centre, normal, outer, cockpit)


def _inside_polygon(points: np.ndarray, polygon: np.ndarray) -> np.ndarray:
    """Vectorised even/odd test for a pane outline in its tangent plane."""
    x, y = points[:, 0], points[:, 1]
    inside = np.zeros(len(points), dtype=bool)
    for a, b in zip(polygon, np.roll(polygon, -1, axis=0)):
        crossing = (a[1] > y) != (b[1] > y)
        edge_x = (b[0] - a[0]) * (y - a[1]) / (b[1] - a[1] + 1e-12) + a[0]
        inside ^= crossing & (x < edge_x)
    return inside


def _refine_near_pane(vertices, triangles, centre, normal, tangent, up, polygon, edge_limit):
    """Split only skin faces crossing one aperture, sharing their edge midpoints."""
    tri_points = vertices[triangles].astype(np.float64)
    relative = tri_points - centre
    u, v, d = relative @ tangent, relative @ up, relative @ normal
    limit = np.max(np.abs(polygon), axis=0) + edge_limit
    nearby = ((u.min(axis=1) < limit[0]) & (u.max(axis=1) > -limit[0])
              & (v.min(axis=1) < limit[1]) & (v.max(axis=1) > -limit[1])
              & (d.min(axis=1) < 0.10) & (d.max(axis=1) > -0.22))
    if not nearby.any():
        return vertices, triangles
    points = vertices.tolist()
    mids = {}
    refined = []

    def midpoint(a, b):
        key = (min(a, b), max(a, b))
        if key not in mids:
            mids[key] = len(points)
            points.append(((np.asarray(points[a]) + points[b]) * 0.5).tolist())
        return mids[key]

    def split(a, b, c):
        # A widebody loft may have metre-wide source triangles. Recurse only
        # into the sliver touching this aperture, not the entire source face.
        patch = np.asarray((points[a], points[b], points[c])) - centre
        pu, pv, pd = patch @ tangent, patch @ up, patch @ normal
        if (pu.min() >= limit[0] or pu.max() <= -limit[0]
                or pv.min() >= limit[1] or pv.max() <= -limit[1]
                or pd.min() >= 0.10 or pd.max() <= -0.22):
            refined.append((a, b, c))
            return
        edges = ((a, b, c), (b, c, a), (c, a, b))
        lengths = [np.linalg.norm(np.asarray(points[p]) - points[q]) for p, q, _ in edges]
        longest = int(np.argmax(lengths))
        if lengths[longest] <= edge_limit:
            refined.append((a, b, c))
            return
        p, q, other = edges[longest]
        mid = midpoint(p, q)
        split(p, mid, other)
        split(mid, q, other)

    for tri, near in zip(triangles, nearby):
        a, b, c = (int(value) for value in tri)
        if near:
            split(a, b, c)
        else:
            refined.append((a, b, c))
    # Work in 32-bit indices until all panes are cut; oversized widebody
    # fuselages are partitioned into two 16-bit runtime nodes below.
    return np.asarray(points, np.float32), np.asarray(refined, np.uint32)


def open_skin(meshes, panes):
    """Cut smooth, bounded apertures inside each window's rubber gasket."""
    shells = ("fuselage", "nose", "radome", "flightdeck_crown",
              "cockpit_mask_left", "cockpit_mask_right")
    total = 0
    for name in shells:
        if name not in meshes:
            continue
        vertices, indices = meshes[name]
        triangles = indices.reshape(-1, 3)
        for centre, normal, outer, cockpit in panes:
            tangent = np.array((0.0, 0.0, 1.0)) - normal * normal[2]
            tangent /= np.linalg.norm(tangent)
            up = np.cross(normal, tangent)
            polygon = np.column_stack(((outer - centre) @ tangent, (outer - centre) @ up))
            polygon *= 0.76 if cockpit else 0.72
            edge_limit = 0.13 if cockpit else 0.10
            vertices, triangles = _refine_near_pane(
                vertices, triangles, centre, normal, tangent, up, polygon, edge_limit)
            centres = vertices[triangles].mean(axis=1).astype(np.float64)
            relative = centres - centre
            depth = relative @ normal
            near = (depth > -0.18) & (depth < 0.06)
            if not near.any():
                continue
            points = np.column_stack((relative[near] @ tangent, relative[near] @ up))
            candidates = np.flatnonzero(near)
            remove = candidates[_inside_polygon(points, polygon)]
            if len(remove):
                keep = np.ones(len(triangles), dtype=bool)
                keep[remove] = False
                triangles = triangles[keep]
                total += len(remove)
        if len(vertices) < 65536:
            meshes[name] = vertices, triangles.reshape(-1).astype(np.uint16)
            continue
        if name != "fuselage" or "fuselage_port" in meshes:
            raise ValueError(f"{name} exceeds the 16-bit runtime mesh limit")

        # Keep both sides full-length: title/paint fit still sees the complete
        # aircraft silhouette through the starboard "fuselage" node. The port
        # node touches the same centre seam and has identical white skin paint.
        side = vertices[triangles].mean(axis=1)[:, 0] >= 0.0
        if not side.any() or side.all():
            raise ValueError("widebody fuselage could not be split by side")
        for key, selected in (("fuselage", triangles[side]),
                              ("fuselage_port", triangles[~side])):
            used, compact = np.unique(selected.reshape(-1), return_inverse=True)
            if len(used) >= 65536:
                raise ValueError(f"{key} still exceeds the 16-bit runtime mesh limit")
            meshes[key] = vertices[used], compact.astype(np.uint16)
    return total

def ellipsoid(centre, radii, sides=12, rings=8):
    """Low-poly original cockpit crew silhouette; no borrowed character art."""
    vertices = []
    for j in range(rings + 1):
        lat = -np.pi / 2 + np.pi * j / rings
        for i in range(sides):
            lon = 2 * np.pi * i / sides
            vertices.append(centre + radii * np.array((np.cos(lat) * np.cos(lon),
                                                        np.sin(lat), np.cos(lat) * np.sin(lon))))
    faces = []
    for j in range(rings):
        for i in range(sides):
            a, b = j * sides + i, j * sides + (i + 1) % sides
            c, d = a + sides, b + sides
            faces.extend((a, b, d, a, d, c))
    return np.asarray(vertices, np.float32), np.asarray(faces, np.uint16)


def polish(meshes: dict[str, tuple[np.ndarray, np.ndarray]]):
    skin = load_module("aircraft_skin.py")
    groups: dict[str, dict[str, list]] = {}
    panes = []
    flightdeck = {}
    count = 0
    for name, (vertices, indices) in list(meshes.items()):
        if name.startswith("cabin_window_"):
            kind = "cabin"
        elif name.startswith(("windscreen_", "cockpit_side_")) and not name.startswith("windscreen_pillar"):
            kind = "flightdeck"
        else:
            continue
        side = "right" if name.startswith(("cabin_window_r", "windscreen_r", "cockpit_side_r")) \
            or name.endswith("_r") or "right" in name else "left"
        group = groups.setdefault(f"{kind}_{side}", {
            "trim": [], "gasket": [], "reflection": [], "interior": []})
        outer_lites = []
        for pane_vertices, pane_indices in components(vertices, indices):
            front_count = len(pane_vertices) // 2
            front_faces = pane_indices.reshape(-1, 3)
            front_faces = front_faces[np.all(front_faces < front_count, axis=1)]
            outer_lites.append((pane_vertices[:front_count], front_faces.reshape(-1)))
            trim, gasket, reflection, interior, pane = pane_detail(
                pane_vertices, pane_indices, cockpit=kind == "flightdeck")
            for key, mesh in (("trim", trim), ("gasket", gasket),
                              ("reflection", reflection), ("interior", interior)):
                if mesh is not None:
                    group[key].append(mesh)
            panes.append(pane)
            if kind == "flightdeck" and (side not in flightdeck
                                         or pane[0][2] > flightdeck[side][0][2]):
                flightdeck[side] = pane
            count += 1
        # A closed shell renders twice through itself, making translucent glazing opaque.
        # Keep one outward-facing lite; the recessed cabin provides the depth behind it.
        meshes[name] = skin.merge_meshes(outer_lites)
    if not count:
        raise ValueError("aircraft has no curvature-fitted glazing")
    for group_name, parts in groups.items():
        for key, pieces in parts.items():
            if pieces:
                meshes[f"glazing_{group_name}_{key}"] = skin.merge_meshes(pieces)
    removed = open_skin(meshes, panes)
    if removed < count:
        raise ValueError(f"only {removed} skin triangles opened for {count} panes")
    for side, (centre, normal, outer, _) in flightdeck.items():
        inner = centre - normal * 0.26
        size = min(0.12, max(0.065, np.ptp(outer[:, 1]) * 0.19))
        meshes[f"pilot_{side}_head"] = ellipsoid(
            inner - np.array((0, size * 0.10, 0)),
            np.array((size * 0.82, size, size * 0.82)))
        meshes[f"pilot_{side}_uniform"] = ellipsoid(
            inner - np.array((0, size * 2.0, 0.07)),
            np.array((size * 1.35, size * 1.55, size)))
    return count


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("types", nargs="*", choices=tuple(SOURCES), help="fleet IDs; default all")
    parser.add_argument("--output-dir", type=Path, default=AIRCRAFT)
    parser.add_argument("--baseline", action="store_true", help="write generator output without glazing for drift audit")
    args = parser.parse_args()
    if args.baseline and args.output_dir.resolve() == AIRCRAFT.resolve():
        parser.error("--baseline requires a separate --output-dir to protect production kits")
    writer = load_module("generate-authored-fbx-turboprop-terminal.py").write_kit
    args.output_dir.mkdir(parents=True, exist_ok=True)
    for type_id in args.types or SOURCES:
        filename, function, basename = SOURCES[type_id]
        meshes = getattr(load_module(filename), function)()
        count = 0 if args.baseline else polish(meshes)
        writer(args.output_dir, basename, meshes)
        print(f"{type_id}: {count} fitted panes, {len(meshes)} named meshes -> {args.output_dir / basename}")


if __name__ == "__main__":
    main()
