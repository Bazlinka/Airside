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


# Seal and skin-cut extents as fractions of the pane outline. The skin is opened inside
# SKIN_CUT_*; the opaque seal covers from just outside the pane edge to SEAL_INNER_*, which
# is inside the cut, so no ragged skin edge or white fuselage shows round a pane.
SEAL_OUTER = 1.03
SEAL_INNER_CABIN = 0.70
SEAL_INNER_FLIGHTDECK = 0.74
SKIN_CUT_CABIN = 0.72
SKIN_CUT_FLIGHTDECK = 0.76

# The flight-deck surround: border beyond the outermost pane, mesh spacing, height above
# the skin (under the 7 mm glass front, so it never covers a pane).
MASK_BORDER = 0.035
MASK_SPACING = 0.035
MASK_OFFSET = 0.004
GLASS_FRONT = 0.007
SEAL_PROUD = 0.004


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
    """Make the rubber seal and recessed interior for one aircraft_skin.skin_patch pane."""
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

    # skin_patch lays the front as outline rings at scales 1, 1 - 1/rings, ... on the curved
    # skin. Interpolating those rings keeps the seal on the pane's surface; scaling the chord
    # to the centre instead sagged it under the skin on tight flight-deck noses.
    layers = [vertices[k * n:(k + 1) * n].astype(np.float64) for k in range(rings)]
    layers.append(np.repeat(centre[None], n, axis=0))
    scales = [1.0 - k / rings for k in range(rings)] + [0.0]

    def on_surface(scale: float):
        k = min(max(int(np.floor((1.0 - scale) * rings)), 0), rings - 1)
        f = (scales[k] - scale) / (scales[k] - scales[k + 1])
        return layers[k] + (layers[k + 1] - layers[k]) * f

    def ring(outer_scale: float, inner_scale: float, offset: float):
        a = on_surface(outer_scale) + normal * offset
        b = on_surface(inner_scale) + normal * offset
        points = np.vstack((a, b)).astype(np.float32)
        faces = []
        for i in range(n):
            j = (i + 1) % n
            faces.extend((i, j, n + j, i, n + j, n + i))
        return points, np.asarray(faces, np.uint16)

    # A real pane is a dark opening with a thin black seal, flush with the skin: no light
    # metal reveal and no painted-on glint (every pane caught the "sun" in the same corner
    # from every angle). The seal runs inward past the skin cut so the aperture edge stays
    # hidden; the glazing shader supplies the actual sky reflection.
    gasket = ring(SEAL_OUTER, SEAL_INNER_FLIGHTDECK if cockpit else SEAL_INNER_CABIN, SEAL_PROUD)

    # The dark cabin/flight deck lies behind an actual skin opening. It stops the
    # opposite white fuselage shining through the translucent outer glazing.
    face_tris = indices.reshape(-1, 3)
    face_tris = face_tris[np.all(face_tris < front_count, axis=1)]
    depth = 0.43 if cockpit else 0.13
    interior = (vertices[:front_count] - normal * depth,
                face_tris.reshape(-1).astype(np.uint16))
    return gasket, interior, (centre, normal, outer, cockpit)


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
            polygon *= SKIN_CUT_FLIGHTDECK if cockpit else SKIN_CUT_CABIN
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
            inside = _inside_polygon(points, polygon)
            if name.startswith("cockpit_mask_"):
                # A painted mask is dark under the glass anyway; cut only faces wholly inside
                # so its coarse faces never leave a hole past the pane (the A350's white notch).
                corners = vertices[triangles[candidates]].astype(np.float64) - centre
                for k in range(3):
                    inside &= _inside_polygon(np.column_stack(
                        (corners[:, k] @ tangent, corners[:, k] @ up)), polygon)
            remove = candidates[inside]
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

def _convex_hull(points: np.ndarray) -> np.ndarray:
    """Counter-clockwise 2-D convex hull (monotone chain)."""
    pts = sorted(set(map(tuple, np.round(points, 6))))
    if len(pts) < 3:
        raise ValueError("flight-deck mask needs at least three outline points")

    def cross(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])

    lower, upper = [], []
    for p in pts:
        while len(lower) >= 2 and cross(lower[-2], lower[-1], p) <= 0:
            lower.pop()
        lower.append(p)
    for p in reversed(pts):
        while len(upper) >= 2 and cross(upper[-2], upper[-1], p) <= 0:
            upper.pop()
        upper.append(p)
    return np.asarray(lower[:-1] + upper[:-1], np.float64)


def _resample(polygon: np.ndarray, spacing: float) -> np.ndarray:
    out = []
    for a, b in zip(polygon, np.roll(polygon, -1, axis=0)):
        steps = max(1, int(np.ceil(np.linalg.norm(b - a) / spacing)))
        out.extend(a + (b - a) * t / steps for t in range(steps))
    return np.asarray(out, np.float64)


def _first_hits(origins: np.ndarray, direction: np.ndarray, triangles: np.ndarray):
    """Nearest ray hit per origin (Moller-Trumbore); inf where the ray misses."""
    best_t = np.full(len(origins), np.inf)
    best_tri = np.full(len(origins), -1)
    a, b, c = triangles[:, 0], triangles[:, 1], triangles[:, 2]
    e1, e2 = b - a, c - a
    pvec = np.cross(direction, e2)
    det = np.einsum("tk,tk->t", e1, pvec)
    usable = np.abs(det) > 1e-12
    a, e1, e2, pvec, det = a[usable], e1[usable], e2[usable], pvec[usable], det[usable]
    index = np.flatnonzero(usable)
    inv = 1.0 / det
    for start in range(0, len(origins), 256):
        o = origins[start:start + 256, None, :]
        tvec = o - a[None]
        u = np.einsum("rtk,tk->rt", tvec, pvec) * inv
        qvec = np.cross(tvec, e1[None])
        v = (qvec @ direction) * inv
        t = np.einsum("rtk,tk->rt", qvec, e2) * inv
        hit = (u >= -1e-7) & (v >= -1e-7) & (u + v <= 1 + 1e-7) & (t > 0)
        t = np.where(hit, t, np.inf)
        nearest = np.argmin(t, axis=1)
        rows = np.arange(len(t))
        best_t[start:start + 256] = t[rows, nearest]
        best_tri[start:start + 256] = np.where(np.isfinite(t[rows, nearest]), index[nearest], -1)
    return best_t, best_tri


def flightdeck_mask(meshes, lites, panes, *, offset=MASK_OFFSET, extra=None):
    """A dark painted surround hugging the nose round a group of flight-deck panes.

    Airliner windscreens read as one dark band broken by thin posts, not as separate
    windows in white skin. The convex hull of the panes (plus a narrow border) is
    projected onto the skin along the group's mean normal, a few millimetres proud.
    It runs on under the glass rim and is removed only over the skin openings, so the
    crew stay visible through the windscreen.
    """
    normal = np.mean([n for _, n, _ in panes], axis=0)
    normal /= np.linalg.norm(normal)
    centre = np.mean([c for c, _, _ in panes], axis=0)
    tangent = np.array((0.0, 0.0, 1.0)) - normal * normal[2]
    if np.linalg.norm(tangent) < 1e-6:
        tangent = np.array((1.0, 0.0, 0.0)) - normal * normal[0]
    tangent /= np.linalg.norm(tangent)
    up = np.cross(normal, tangent)
    shapes = [o for _, _, o in panes] + ([extra] if extra is not None else [])
    outline = np.vstack([np.column_stack(((o - centre) @ tangent, (o - centre) @ up))
                         for o in shapes])
    angles = np.linspace(0.0, 2.0 * np.pi, 16, endpoint=False)
    border = np.column_stack((np.cos(angles), np.sin(angles))) * MASK_BORDER
    hull = _convex_hull((outline[:, None, :] + border[None]).reshape(-1, 2))
    hull = _resample(hull, MASK_SPACING)
    middle = hull.mean(axis=0)
    radius = np.max(np.linalg.norm(hull - middle, axis=1))
    rings = max(2, int(np.ceil(radius / MASK_SPACING)))
    n = len(hull)
    uv = np.vstack([middle + (hull - middle) * (1.0 - k / rings) for k in range(rings)]
                   + [middle[None]])

    # Skin sets the mask height (a few millimetres proud). Glass and seals only matter where
    # the skin is cut away beneath them: there the mask drops under the glass. Seals also
    # cover the ragged edge of the skin opening.
    skin_names = ("fuselage", "fuselage_port", "nose", "radome", "flightdeck_crown",
                  "cockpit_mask_left", "cockpit_mask_right")
    seal_names = [k for k in meshes if k.startswith("glazing_flightdeck_") and k.endswith("_gasket")]
    skin_tris = [meshes[k][0][meshes[k][1].reshape(-1, 3)] for k in skin_names if k in meshes]
    glass_tris = [v[i.reshape(-1, 3)] for v, i in lites]
    glass_tris += [meshes[k][0][meshes[k][1].reshape(-1, 3)] for k in seal_names]

    lo, hi = uv.min(axis=0) - 0.2, uv.max(axis=0) + 0.2

    def nearby(tris):
        tris = np.vstack(tris).astype(np.float64)
        rel = tris - centre
        tu, tv = rel @ tangent, rel @ up
        keep = ((tu.max(axis=1) > lo[0]) & (tu.min(axis=1) < hi[0])
                & (tv.max(axis=1) > lo[1]) & (tv.min(axis=1) < hi[1]))
        return tris[keep]

    reach = 4.0
    origins = centre + np.outer(uv[:, 0], tangent) + np.outer(uv[:, 1], up) + normal * reach

    def cast(tris):
        # The refined skin has hairline T-junction cracks; a ray through one would drop a
        # mask vertex deep inside the nose. Keep the nearest of three rays a millimetre apart.
        t, tri = _first_hits(origins, -normal, tris)
        for jitter in (tangent * 0.001 + up * 0.0006, -tangent * 0.0006 - up * 0.001):
            t2, tri2 = _first_hits(origins + jitter, -normal, tris)
            closer = t2 < t
            t, tri = np.where(closer, t2, t), np.where(closer, tri2, tri)
        face = tris[np.maximum(tri, 0)]
        face_normal = np.cross(face[:, 1] - face[:, 0], face[:, 2] - face[:, 0])
        face_normal /= np.maximum(np.linalg.norm(face_normal, axis=1, keepdims=True), 1e-12)
        face_normal *= np.sign(face_normal @ normal)[:, None]
        hit = origins - normal * np.where(np.isfinite(t), t, 0.0)[:, None]
        return t, hit, face_normal

    skin_t, skin_hit, skin_normal = cast(nearby(skin_tris))
    glass_t, glass_hit, glass_normal = cast(nearby(glass_tris))
    # Skin lying just under the glass is still skin; a cut shows as a jump of centimetres.
    cut = (glass_t < skin_t) & ~(skin_t - glass_t < 0.03)
    valid = np.isfinite(skin_t) | np.isfinite(glass_t)
    points = np.where(cut[:, None], glass_hit - glass_normal * (GLASS_FRONT - offset),
                      skin_hit + skin_normal * offset)

    faces = []
    for k in range(rings - 1):
        base, nxt = k * n, (k + 1) * n
        for i in range(n):
            j = (i + 1) % n
            faces += [(base + i, base + j, nxt + j), (base + i, nxt + j, nxt + i)]
    last, centre_index = (rings - 1) * n, rings * n
    faces += [(last + i, last + (i + 1) % n, centre_index) for i in range(n)]
    faces = np.asarray(faces)
    # Keep the mask under the glass rim too (it hides white skin through the translucent
    # lite); drop it only over the openings, where the crew show through.
    keep = valid[faces].all(axis=1) & ~cut[faces].all(axis=1)
    faces = faces[keep]
    # Wind outward so the painted face is the front face.
    tri_normal = np.cross(points[faces[:, 1]] - points[faces[:, 0]], points[faces[:, 2]] - points[faces[:, 0]])
    flip = tri_normal @ normal < 0
    faces[flip] = faces[flip][:, ::-1]
    used, compact = np.unique(faces.reshape(-1), return_inverse=True)
    return points[used].astype(np.float32), compact.astype(np.uint16)


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
    deck_panes: dict[str, list] = {}
    deck_lites = []
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
            "gasket": [], "interior": []})
        outer_lites = []
        for pane_vertices, pane_indices in components(vertices, indices):
            front_count = len(pane_vertices) // 2
            front_faces = pane_indices.reshape(-1, 3)
            front_faces = front_faces[np.all(front_faces < front_count, axis=1)]
            outer_lites.append((pane_vertices[:front_count], front_faces.reshape(-1)))
            gasket, interior, pane = pane_detail(
                pane_vertices, pane_indices, cockpit=kind == "flightdeck")
            group["gasket"].append(gasket)
            group["interior"].append(interior)
            panes.append(pane)
            if kind == "flightdeck":
                deck_panes.setdefault(side, []).append((name, pane))
            if kind == "flightdeck" and (side not in flightdeck
                                         or pane[0][2] > flightdeck[side][0][2]):
                flightdeck[side] = pane
            count += 1
        # A closed shell renders twice through itself, making translucent glazing opaque.
        # Keep one outward-facing lite; the recessed cabin provides the depth behind it.
        meshes[name] = skin.merge_meshes(outer_lites)
        if kind == "flightdeck":
            deck_lites.extend(outer_lites)
    if not count:
        raise ValueError("aircraft has no curvature-fitted glazing")
    for group_name, parts in groups.items():
        for key, pieces in parts.items():
            if pieces:
                meshes[f"glazing_{group_name}_{key}"] = skin.merge_meshes(pieces)
    removed = open_skin(meshes, panes)
    if removed < count:
        raise ValueError(f"only {removed} skin triangles opened for {count} panes")
    def extra_points(*names):
        # The A350 paints its own wraparound mask, whose lower edge dips under the skin in
        # places; the turboprops model windscreen posts and (Q400) a centre frame. The
        # surround covers them all.
        points = [meshes[k][0].astype(np.float64) for k in names if k in meshes]
        return np.vstack(points) if points else None

    for side, group in deck_panes.items():
        meshes[f"glazing_flightdeck_{side}_mask"] = flightdeck_mask(
            meshes, deck_lites, [pane[:3] for _, pane in group],
            extra=extra_points(f"cockpit_mask_{side}", f"windscreen_pillar_{side[0]}"))
    # The centre post: the two front windscreens seen together, projected forward.
    front = [pane[:3] for group in deck_panes.values()
             for pane in [max(group, key=lambda item: item[1][0][2])[1]]]
    front += [pane[:3] for group in deck_panes.values() for name, pane in group
              if name == "windscreen_c"]
    if len(deck_panes) == 2:
        meshes["glazing_flightdeck_centre_mask"] = flightdeck_mask(
            meshes, deck_lites, front, offset=MASK_OFFSET - 0.0005,
            extra=extra_points("windscreen_pillar_c", "cockpit_sill", "cockpit_glare"))
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
    parser.add_argument("types", nargs="*", help=f"fleet IDs ({', '.join(SOURCES)}); default all")
    parser.add_argument("--output-dir", type=Path, default=AIRCRAFT)
    parser.add_argument("--baseline", action="store_true", help="write generator output without glazing for drift audit")
    args = parser.parse_args()
    unknown = sorted(set(args.types) - set(SOURCES))
    if unknown:
        parser.error(f"unknown fleet IDs: {', '.join(unknown)}")
    if args.baseline and args.output_dir.resolve() == AIRCRAFT.resolve():
        parser.error("--baseline requires a separate --output-dir to protect production kits")
    writer = load_module("generate-authored-fbx-turboprop-terminal.py").write_kit
    args.output_dir.mkdir(parents=True, exist_ok=True)
    for type_id in args.types or SOURCES:
        filename, function, basename = SOURCES[type_id]
        meshes = getattr(load_module(filename), function)()
        count = 0 if args.baseline else polish(meshes)
        if not args.baseline:
            load_module("finish-aircraft-liveries.py").finish(meshes, type_id)
        writer(args.output_dir, basename, meshes)
        print(f"{type_id}: {count} fitted panes, {len(meshes)} named meshes -> {args.output_dir / basename}")


if __name__ == "__main__":
    main()
