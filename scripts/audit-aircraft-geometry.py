#!/usr/bin/env python3
"""Audit the runtime aircraft glTFs for gaps, floating parts and loose door/window fits.

Reads the exact `.gltf`/`.bin` the game loads (see render-aircraft-thumbnails.py) and:

  views     multi-angle z-buffered renders on a magenta background, so any hole you can
            see straight through the airframe shows up as background colour.
  floating  parts with no other part within --tol metres (a wick hovering above a winglet).
  flush     how far each door / window / hatch sits proud of (+) or below (-) the fuselage.

Usage:
  python3 scripts/audit-aircraft-geometry.py floating [--tol 0.06]
  python3 scripts/audit-aircraft-geometry.py flush
  python3 scripts/audit-aircraft-geometry.py views OUT_DIR [ATR42 B38M ...]
"""
import argparse
import importlib.util
import os
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("thumbs", os.path.join(HERE, "render-aircraft-thumbnails.py"))
thumbs = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(thumbs)

MODELS = {cid: os.path.join(thumbs.ART, model) for cid, model, _ in thumbs.MODELS}

# Parts that legitimately float or sit apart: everything else must touch the airframe.
INTENTIONALLY_LOOSE = ("fan_blade_", "propeller_", "fan_", "pilot_",
                       "glazing_flightdeck_left_interior", "glazing_flightdeck_right_interior",
                       "glazing_cabin_left_interior", "glazing_cabin_right_interior")


VIEWS = {
    "front_left_high": (38.0, 18.0),
    "rear_right_high": (-142.0, 18.0),
    "side_left": (90.0, 0.0),
    "side_right": (-90.0, 0.0),
    "top": (0.0, 89.0),
    "under": (0.0, -89.0),
    "front": (0.0, 0.0),
    "rear": (180.0, 0.0),
}


def refine(tris, max_edge=1.0):
    """Bisect the longest edge until every triangle edge is <= max_edge.

    The lofted fuselages use single quads up to 13 m long; sampling those directly leaves
    holes in the point cloud that read as gaps (a beacon sitting on the crown looked loose).
    """
    tris = np.asarray(tris, np.float64)
    while True:
        edges = np.stack([np.linalg.norm(tris[:, 1] - tris[:, 0], axis=1),
                          np.linalg.norm(tris[:, 2] - tris[:, 1], axis=1),
                          np.linalg.norm(tris[:, 0] - tris[:, 2], axis=1)], axis=1)
        big = edges.max(axis=1) > max_edge
        if not big.any():
            return tris
        keep, split, e = tris[~big], tris[big], edges[big]
        k = e.argmax(axis=1)
        a = np.empty((len(split), 3)); b = np.empty_like(a); c = np.empty_like(a)
        for kk in range(3):
            m = k == kk
            a[m], b[m], c[m] = split[m, kk], split[m, (kk + 1) % 3], split[m, (kk + 2) % 3]
        mid = (a + b) / 2.0
        tris = np.concatenate([keep, np.stack([a, mid, c], axis=1), np.stack([mid, b, c], axis=1)])


def sample_triangles(tris, spacing):
    """Points spread over the triangles, roughly `spacing` metres apart."""
    tris = refine(tris, max(1.0, spacing * 40))
    edge = np.maximum(
        np.linalg.norm(tris[:, 1] - tris[:, 0], axis=1),
        np.maximum(np.linalg.norm(tris[:, 2] - tris[:, 1], axis=1), np.linalg.norm(tris[:, 0] - tris[:, 2], axis=1)),
    )
    steps = np.clip(np.ceil(edge / spacing).astype(int), 1, 60)
    out = [tris.reshape(-1, 3)]
    for n in np.unique(steps):
        if n == 1:
            continue
        group = tris[steps == n]
        u, v = np.meshgrid(np.arange(n + 1) / n, np.arange(n + 1) / n)
        keep = (u + v) <= 1.0 + 1e-9
        u, v = u[keep], v[keep]
        pts = (group[:, None, 0, :] * (1 - u - v)[None, :, None]
               + group[:, None, 1, :] * u[None, :, None]
               + group[:, None, 2, :] * v[None, :, None])
        out.append(pts.reshape(-1, 3))
    return np.concatenate(out)


def cell_keys(points, cell):
    ijk = np.floor(points / cell).astype(np.int64) + 4096
    return (ijk[:, 0] << 42) | (ijk[:, 1] << 21) | ijk[:, 2]


def connectivity(cid, tol):
    """Parts that do not chain back to the fuselage through parts within `tol` metres.

    A gear leg, its door and its wheels can all touch each other and still hover together a
    metre under the wing, so 'nothing nearby' is not enough: every part must be reachable
    from the fuselage by hopping between touching parts.
    """
    parts = thumbs.load_parts(MODELS[cid])
    cell = tol / 2.0          # a 3x3x3 neighbourhood reaches at least `tol`
    spacing = cell / 1.5
    keys, pids = [], []
    for pid, (name, tris) in enumerate(parts):
        k = np.unique(cell_keys(sample_triangles(tris, spacing), cell))
        keys.append(k)
        pids.append(np.full(len(k), pid))
    all_keys, all_pids = np.concatenate(keys), np.concatenate(pids)
    order = np.argsort(all_keys, kind="stable")
    all_keys, all_pids = all_keys[order], all_pids[order]
    uniq, start = np.unique(all_keys, return_index=True)
    lo = np.minimum.reduceat(all_pids, start)
    hi = np.maximum.reduceat(all_pids, start)

    parent = list(range(len(parts)))

    def find(x):
        while parent[x] != x:
            parent[x] = parent[parent[x]]
            x = parent[x]
        return x

    for pid, own in enumerate(keys):
        i, j, k = (own >> 42) & 0x1FFFFF, (own >> 21) & 0x1FFFFF, own & 0x1FFFFF
        linked = set()
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                for dz in (-1, 0, 1):
                    q = ((i + dx) << 42) | ((j + dy) << 21) | (k + dz)
                    idx = np.searchsorted(uniq, q)
                    idx[idx >= len(uniq)] = len(uniq) - 1
                    hit = uniq[idx] == q
                    linked.update(lo[idx[hit]].tolist())
                    linked.update(hi[idx[hit]].tolist())
        for other in linked:
            if other != pid:
                parent[find(pid)] = find(other)

    names = [n for n, _ in parts]
    root = find(names.index("fuselage"))
    problems = []
    for pid, (name, tris) in enumerate(parts):
        if name.startswith(INTENTIONALLY_LOOSE) or find(pid) == root:
            continue
        problems.append((name, sample_triangles(tris, 0.5).mean(axis=0)))
    return problems


def floating(cid, tol):
    return connectivity(cid, tol)


def flush(cid):
    """Signed distance of door/window vertices from the fuselage skin (outward positive)."""
    parts = dict(thumbs.load_parts(MODELS[cid]))
    fuselage = np.concatenate([parts[name] for name in ("fuselage", "fuselage_port") if name in parts])
    dense = sample_triangles(fuselage, 0.03)
    tri_n = np.cross(fuselage[:, 1] - fuselage[:, 0], fuselage[:, 2] - fuselage[:, 0])
    centroid = fuselage.mean(axis=1)
    # outward normal per triangle: away from the fuselage axis (x, y about the section centre)
    axis_y = np.interp(centroid[:, 2], *_axis_profile(dense))
    radial = np.stack([centroid[:, 0], centroid[:, 1] - axis_y, np.zeros(len(centroid))], axis=1)
    sign = np.sign(np.einsum("ij,ij->i", tri_n, radial))
    tri_n = tri_n / np.maximum(np.linalg.norm(tri_n, axis=1, keepdims=True), 1e-12) * sign[:, None]
    rows = []
    for name, tris in parts.items():
        if not name.startswith(("door", "cargo_door", "cabin_window", "cockpit_side", "hatch", "inspection_panel")):
            continue
        pts = tris.reshape(-1, 3)
        # nearest fuselage sample (brute force in chunks — parts are small)
        dist = np.empty(len(pts))
        signed = np.empty(len(pts))
        for i in range(0, len(pts), 256):
            block = pts[i:i + 256]
            d = np.linalg.norm(block[:, None, :] - dense[None, ::4, :], axis=2)
            j = d.argmin(axis=1)
            near = dense[::4][j]
            # sign from the nearest sample's radial direction
            ay = np.interp(near[:, 2], *_axis_profile(dense))
            out = np.stack([near[:, 0], near[:, 1] - ay, np.zeros(len(near))], axis=1)
            out /= np.maximum(np.linalg.norm(out, axis=1, keepdims=True), 1e-12)
            rel = block - near
            signed[i:i + 256] = np.einsum("ij,ij->i", rel, out)
            dist[i:i + 256] = d.min(axis=1)
        rows.append((name, signed.min(), signed.max()))
    return rows


def _axis_profile(dense):
    """Fuselage centre height per z, from the min/max y of the dense skin samples."""
    zs = np.round(dense[:, 2], 1)
    keys = np.unique(zs)
    ymid = np.array([(dense[zs == k, 1].min() + dense[zs == k, 1].max()) / 2 for k in keys])
    return keys, ymid


def views(cid, out_dir, width=1500, height=750, only=None):
    parts = thumbs.load_parts(MODELS[cid])
    os.makedirs(out_dir, exist_ok=True)
    for name, (az, el) in VIEWS.items():
        if only and name not in only:
            continue
        tris = np.concatenate([t for _, t in parts])
        cols = np.concatenate([np.tile(np.array(thumbs.colour(n), float), (len(t), 1)) for n, t in parts])
        image, _ = thumbs.rasterise(tris, cols, thumbs.view_matrix(az, el), width, height, 2, margin=0.03)
        canvas = Image.new("RGB", image.size, (255, 0, 255))
        canvas.paste(image, (0, 0), image)
        canvas.save(os.path.join(out_dir, f"{cid}_{name}.png"))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("mode", choices=("floating", "flush", "views"))
    ap.add_argument("args", nargs="*")
    ap.add_argument("--tol", type=float, default=0.06)
    ns = ap.parse_args()
    if ns.mode == "views":
        out_dir, ids = ns.args[0], ns.args[1:] or list(MODELS)
        for cid in ids:
            views(cid, out_dir)
            print("rendered", cid)
        return 0
    ids = ns.args or list(MODELS)
    bad = 0
    for cid in ids:
        if ns.mode == "floating":
            for name, centre in floating(cid, ns.tol):
                bad += 1
                print(f"{cid}: FLOATING {name} centre=({centre[0]:.2f}, {centre[1]:.2f}, {centre[2]:.2f})")
        else:
            for name, lo, hi in flush(cid):
                print(f"{cid}: {name:<22} proud {lo * 100:+6.1f} .. {hi * 100:+6.1f} cm")
    if ns.mode == "floating":
        print("no floating parts" if not bad else f"{bad} floating part(s)")
    return 1 if (ns.mode == "floating" and bad) else 0


if __name__ == "__main__":
    sys.exit(main())
