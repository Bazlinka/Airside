#!/usr/bin/env python3
"""Skin-conforming doors, windows and panels for the authored aircraft kits.

Every generator used to lay these on the fuselage as a *flat* quad least-squares fitted
through its corners. A window is small enough to survive that, but a 1.9 m airliner door
spans ~30 degrees of a 1.9 m radius fuselage, so its flat plane sagged ~6 cm *below* the
skin at the centre and only the top and bottom edges poked out: the door read as a
missing panel and the frame as two stray slats.

Here a patch is a thin closed shell whose front and back faces are sampled from the real
fuselage surface function at fixed offsets, so it follows the curve everywhere and sits a
few millimetres proud of the skin all the way across.

`surface(z, angle_deg, offset)` is each generator's own fuselage skin: a point on the
fuselage at station `z`, cross-section angle `angle_deg` (0 = +X side, 90 = crown,
180 = -X side, anything else per the generator) pushed `offset` metres along the local
outward radius. The returned meshes are `(vertices, uint16 indices)` like every kit mesh.
"""
from __future__ import annotations

import math
from typing import Callable

import numpy as np

Surface = Callable[[float, float, float], np.ndarray]


def metres_per_degree(surface: Surface, z: float, angle_deg: float) -> float:
    a = np.asarray(surface(z, angle_deg - 0.5, 0.0), np.float64)
    b = np.asarray(surface(z, angle_deg + 0.5, 0.0), np.float64)
    return float(np.linalg.norm(b - a))


def rounded_outline(half_u, half_v, radius, corner_segments=3, max_edge=0.12) -> np.ndarray:
    """Counter-clockwise rounded-rectangle outline in (u, v) metres."""
    r = max(1e-4, min(radius, half_u, half_v))
    corners = (
        (half_u - r, half_v - r, 0.0),
        (-(half_u - r), half_v - r, 90.0),
        (-(half_u - r), -(half_v - r), 180.0),
        (half_u - r, -(half_v - r), 270.0),
    )
    raw = []
    for cu, cv, start in corners:
        for k in range(corner_segments + 1):
            a = math.radians(start + 90.0 * k / corner_segments)
            raw.append((cu + r * math.cos(a), cv + r * math.sin(a)))
    out = []
    for i, p in enumerate(raw):
        q = raw[(i + 1) % len(raw)]
        n = max(1, int(math.ceil(math.hypot(q[0] - p[0], q[1] - p[1]) / max_edge)))
        for t in range(n):
            out.append((p[0] + (q[0] - p[0]) * t / n, p[1] + (q[1] - p[1]) * t / n))
    return np.asarray(out, np.float64)


def _outward_fixed(verts: np.ndarray, indices: np.ndarray):
    tris = indices.reshape(-1, 3).astype(np.int64)
    a, b, c = verts[tris[:, 0]], verts[tris[:, 1]], verts[tris[:, 2]]
    volume = float(np.einsum("ij,ij->i", a, np.cross(b, c)).sum()) / 6.0
    if volume < 0.0:
        tris = tris[:, ::-1]
    return verts.astype(np.float32), np.ascontiguousarray(tris).reshape(-1).astype(np.uint16)


def skin_patch(
    surface: Surface,
    z_c: float,
    angle_c: float,
    half_len: float,
    half_arc: float,
    *,
    front: float = 0.006,
    back: float = -0.004,
    radius: float | None = None,
    rings: int = 2,
    corner_segments: int = 3,
    max_edge: float = 0.12,
) -> tuple[np.ndarray, np.ndarray]:
    """A rounded-rectangle shell that hugs the fuselage.

    `half_len` runs along the fuselage (z), `half_arc` along the skin (metres of arc).
    `front`/`back` are outward offsets from the skin; the back sits just inside so the
    shell is closed but nothing shows through.
    """
    mpd = metres_per_degree(surface, z_c, angle_c)
    outline = rounded_outline(
        half_len, half_arc, min(half_len, half_arc) * 0.55 if radius is None else radius,
        corner_segments, max_edge,
    )
    n = len(outline)
    layers = [outline * (1.0 - k / (rings + 1.0)) for k in range(rings + 1)]
    layers.append(np.zeros((1, 2)))
    uv = np.concatenate(layers)                      # ring0..ringN, then the centre
    z = z_c + uv[:, 0]
    angle = angle_c + uv[:, 1] / mpd
    top = np.asarray([surface(float(zi), float(ai), front) for zi, ai in zip(z, angle)], np.float64)
    bot = np.asarray([surface(float(zi), float(ai), back) for zi, ai in zip(z, angle)], np.float64)
    verts = np.vstack([top, bot])
    count = len(uv)
    centre = count - 1

    faces: list[int] = []
    for k in range(rings):
        base, nxt = k * n, (k + 1) * n
        for i in range(n):
            j = (i + 1) % n
            faces += [base + i, base + j, nxt + j, base + i, nxt + j, nxt + i]
    last = rings * n
    for i in range(n):
        faces += [last + i, last + (i + 1) % n, centre]
    front_faces = np.asarray(faces).reshape(-1, 3)
    back_faces = front_faces[:, ::-1] + count
    side = []
    for i in range(n):
        j = (i + 1) % n
        side += [(j, i, count + i), (j, count + i, count + j)]
    indices = np.concatenate([front_faces, back_faces, np.asarray(side)]).reshape(-1)
    return _outward_fixed(verts, indices)


def window(surface, z, angle, *, width=0.24, height=0.34, front=0.007, back=-0.004):
    """One airliner passenger window: a tall rounded rectangle, flush with the skin."""
    return skin_patch(surface, z, angle, width / 2, height / 2, front=front, back=back,
                      radius=width * 0.42, rings=1, corner_segments=2)


def door_set(surface, z, angle, half_len, half_arc, *, base=0.004):
    """(outline, panel, handle) meshes for one entry or service door, all curved with the skin.

    The outline is a slightly larger seam ring under the panel; the handle a small proud bar.
    All three stay within ~2 cm of the skin, so the door reads as flush rather than as a slab.
    """
    outline = skin_patch(surface, z, angle, half_len + 0.05, half_arc + 0.05,
                         front=base + 0.002, back=-0.004, radius=0.14, rings=3, max_edge=0.14)
    panel = skin_patch(surface, z, angle, half_len, half_arc,
                       front=base + 0.006, back=-0.004, radius=0.12, rings=3, max_edge=0.14)
    handle = skin_patch(surface, z + half_len * 0.55, angle, 0.07, 0.045,
                        front=base + 0.016, back=-0.004, radius=0.02, rings=0, corner_segments=1)
    return outline, panel, handle


def merge_meshes(meshes) -> tuple[np.ndarray, np.ndarray]:
    """Join several (verts, indices) meshes into one node (e.g. two windows sharing a part)."""
    verts, indices, base = [], [], 0
    for v, i in meshes:
        verts.append(v)
        indices.append(i.astype(np.int64) + base)
        base += len(v)
    return np.concatenate(verts).astype(np.float32), np.concatenate(indices).astype(np.uint16)


def x_tube(x_in, x_out, cy, cz, ry, rz, *, segments=32, stations=9, dome=0.30):
    """Horizontal oval pod along X (a main-gear bay blister) from inside the fuselage to a rounded end.

    `x_in` should sit inside the fuselage so the pod grows out of the hull; the last `dome` of
    its length closes to a rounded end over the gear leg.
    """
    xs = np.linspace(x_in, x_out, stations)
    rings = []
    for i, x in enumerate(xs):
        t = i / (stations - 1.0)
        k = 1.0 if t <= 1.0 - dome else math.sqrt(max(0.0, 1.0 - ((t - (1.0 - dome)) / dome) ** 2))
        k = max(k, 0.12)
        a = np.linspace(0.0, 2.0 * math.pi, segments, endpoint=False)
        rings.append(np.stack([np.full(segments, x), cy + ry * k * np.sin(a), cz + rz * k * np.cos(a)], axis=1))
    verts = np.concatenate(rings)
    faces = []
    for r in range(stations - 1):
        for i in range(segments):
            j = (i + 1) % segments
            a0, b0, a1, b1 = r * segments + i, r * segments + j, (r + 1) * segments + i, (r + 1) * segments + j
            faces += [a0, b0, b1, a0, b1, a1]
    for ring, flip in ((0, True), (stations - 1, False)):
        centre = len(verts)
        verts = np.vstack([verts, rings[ring].mean(axis=0)])
        for i in range(segments):
            j = (i + 1) % segments
            tri = [centre, ring * segments + i, ring * segments + j]
            faces += tri[::-1] if flip else tri
    return _outward_fixed(verts, np.asarray(faces))
