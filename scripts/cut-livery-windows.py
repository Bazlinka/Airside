#!/usr/bin/env python3
"""Cut the cabin windows out of the livery paint, in place.

The paint shells (`livery_*`) are clipped from the hull, a centimetre or so proud of it.
Where a livery band crosses the window row the paint ends in a staircase-edged hole that
is smaller than the glass pane, so the paint covers part of the pane and the window reads
as a jagged star (A350, 787-9; worst on the dark bands). This subtracts each pane's own
outline (convex hull of its outer lite, in the paint's z/y projection) from every paint
triangle it overlaps, so the glass shows as the clean oval the glazing pass modelled.

Only paint triangles that overlap a pane change; every other mesh is retained, as are GUIDs.
`--check` fails if a kit still has paint covering a pane or the pass is not idempotent.

Usage: python3 scripts/cut-livery-windows.py [A359 B789 ...] [--check]
"""
from pathlib import Path
import argparse
import importlib.util
import json
import numpy as np

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent
ART = ROOT / 'game/Airside/Assets/Airside/Art'
PAINT = ('livery_stripe', 'livery_stripe_lower', 'livery_secondary')
GROW = 1.0  # paint hole = pane outline; the gasket ring sits over the seam


def module(file):
    spec = importlib.util.spec_from_file_location(file.replace('-', '_'), HERE / file)
    m = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(m)
    return m


def readkit(path):
    doc = json.loads(path.read_text())
    blob = path.with_name(doc['buffers'][0]['uri']).read_bytes()

    def acc(k):
        a = doc['accessors'][k]
        v = doc['bufferViews'][a['bufferView']]
        n = {'SCALAR': 1, 'VEC3': 3}[a['type']]
        dtype = {5126: '<f4', 5123: '<u2', 5125: '<u4'}[a['componentType']]
        return np.frombuffer(blob, dtype=dtype, count=a['count'] * n,
                             offset=v.get('byteOffset', 0) + a.get('byteOffset', 0)).reshape(-1, n).copy()
    result = {}
    for node in doc['nodes']:
        p = doc['meshes'][node['mesh']]['primitives'][0]
        result[node['name']] = (acc(p['attributes']['POSITION']), acc(p['indices']).reshape(-1))
    return result


def hull2d(points):
    pts = sorted(set(map(tuple, np.round(points, 6))))
    if len(pts) < 3:
        return None

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
    return np.array(lower[:-1] + upper[:-1], float)  # counter-clockwise


def pane_holes(meshes):
    """Convex (z, y) outlines of every cabin pane, with the side (-1 left, +1 right) of its hull."""
    polish = module('polish-aircraft-glazing.py')
    holes = []
    for name, (vertices, indices) in meshes.items():
        if not name.startswith('cabin_window_'):
            continue
        for pane_vertices, _ in polish.components(vertices, indices):
            side = -1 if pane_vertices[:, 0].mean() < 0 else 1
            outline = hull2d(pane_vertices[:, (2, 1)].astype(float))
            if outline is None:
                continue
            centre = outline.mean(0)
            outline = centre + (outline - centre) * GROW
            holes.append((side, outline, outline.min(0), outline.max(0)))
    return holes


def clip_half(points, a, b, keep_inside):
    """Keep the part of a convex 3D polygon on one side of the directed edge a->b in (z, y)."""
    edge = b - a
    out = []
    n = len(points)
    for i in range(n):
        p, q = points[i], points[(i + 1) % n]
        dp = edge[0] * (p[1] - a[1]) - edge[1] * (p[2] - a[0])
        dq = edge[0] * (q[1] - a[1]) - edge[1] * (q[2] - a[0])
        if not keep_inside:
            dp, dq = -dp, -dq
        pin, qin = dp >= -1e-9, dq >= -1e-9
        if pin:
            out.append(p)
        if pin != qin:
            out.append(p + (q - p) * dp / (dp - dq))
    return out


def overlaps(points, outline, tolerance=1e-7):
    """True when a convex 3D polygon and a convex (z, y) outline share area, not just an edge."""
    inside = list(points)
    for a, b in zip(outline, np.roll(outline, -1, axis=0)):
        inside = clip_half(inside, a, b, keep_inside=True)
        if len(inside) < 3:
            return False
    z = np.array([p[2] for p in inside]); y = np.array([p[1] for p in inside])
    area = 0.5 * abs(np.dot(z, np.roll(y, -1)) - np.dot(y, np.roll(z, -1)))
    return area > tolerance


def subtract_hole(points, outline):
    """Convex polygon minus a convex polygon, as a list of convex pieces (3D points, same winding)."""
    pieces = []
    remaining = list(points)
    for a, b in zip(outline, np.roll(outline, -1, axis=0)):
        if len(remaining) < 3:
            break
        outside = clip_half(remaining, a, b, keep_inside=False)
        if len(outside) >= 3:
            pieces.append(outside)
        remaining = clip_half(remaining, a, b, keep_inside=True)
    return pieces  # whatever stays in `remaining` is inside the hole and is dropped


def cut_mesh(mesh, holes):
    vertices, indices = mesh
    tris = vertices[indices.reshape(-1, 3)].astype(float)
    zy = tris[:, :, (2, 1)]
    lo, hi = zy.min(1), zy.max(1)
    xsign = np.sign(tris[:, :, 0].mean(1))
    out_vertices, out_faces = [], []
    changed = 0

    def emit(points):
        start = sum(len(v) for v in out_vertices)
        out_vertices.append(np.array(points, float))
        for i in range(1, len(points) - 1):
            if np.linalg.norm(np.cross(points[i] - points[0], points[i + 1] - points[0])) > 1e-9:
                out_faces.extend((start, start + i, start + i + 1))
    for t in range(len(tris)):
        pieces = [list(tris[t])]
        touched = False
        for side, outline, hlo, hhi in holes:
            if side != xsign[t] or np.any(hi[t] < hlo) or np.any(lo[t] > hhi):
                continue
            next_pieces = []
            for piece in pieces:
                z = np.array([p[2] for p in piece]); y = np.array([p[1] for p in piece])
                if z.max() < hlo[0] or z.min() > hhi[0] or y.max() < hlo[1] or y.min() > hhi[1]:
                    next_pieces.append(piece)
                    continue
                if not overlaps(piece, outline):
                    next_pieces.append(piece)
                    continue
                touched = True
                next_pieces.extend(subtract_hole(piece, outline))
            pieces = next_pieces
        if not touched:
            emit(list(tris[t]))
        else:
            changed += 1
            for piece in pieces:
                emit(piece)
    if not changed:
        return mesh, 0
    new_vertices = np.concatenate(out_vertices).astype(np.float32).reshape(-1, 3)
    wide = len(new_vertices) > 65535
    return (new_vertices, np.asarray(out_faces, np.uint32 if wide else np.uint16)), changed


def apply(meshes):
    holes = pane_holes(meshes)
    total = 0
    for name in PAINT:
        if name not in meshes:
            continue
        meshes[name], n = cut_mesh(meshes[name], holes)
        total += n
    return total


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('types', nargs='*')
    parser.add_argument('--check', action='store_true', help='fail if any paint still overlaps a pane')
    args = parser.parse_args()
    glazing = module('polish-aircraft-glazing.py')
    writer = module('generate-authored-fbx-turboprop-terminal.py')
    for cid, (_, _, basename) in glazing.SOURCES.items():
        if args.types and cid not in args.types:
            continue
        path = ART / 'Models/Aircraft' / (basename + '.gltf')
        meshes = readkit(path)
        changed = apply(meshes)
        if args.check:
            again = apply(dict(meshes))
            if changed or again:
                raise AssertionError(f'{cid}: {changed} paint triangles still cover a window pane')
            print(f'{cid}: paint clear of window panes')
        elif changed:
            writer.write_kit(path.parent, path.stem, meshes)
            print(f'{cid}: cut {changed} paint triangles around window panes', flush=True)
        else:
            print(f'{cid}: no paint over windows')


if __name__ == '__main__':
    main()
