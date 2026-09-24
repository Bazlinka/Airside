#!/usr/bin/env python3
"""Original ATR 42 cockpit, crew and swept-prop finishing geometry.

This modifies the generated AIR-001 v03 kit before the shared glazing pass. It
does not use or embed manufacturer artwork, airline marks or a downloaded model.
"""

from __future__ import annotations

import math

import numpy as np


# z station, circumferential angle, longitudinal/arc half-size.
PANES = {
    "cockpit_glass_front_l": (9.27, 105.0, 0.62, 0.285),
    "cockpit_glass_front_r": (9.27, 75.0, 0.62, 0.285),
    "cockpit_glass_side_l": (8.91, 135.0, 0.57, 0.295),
    "cockpit_glass_side_r": (8.91, 45.0, 0.57, 0.295),
}

def _shape(name):
    """Eight-sided, sloped panes rather than capsule-like painted windows."""
    if "_front_" in name:
        return np.array(((-1.0, -0.72), (-1.0, 0.68), (-0.77, 0.96),
                         (0.68, 1.0), (1.0, 0.71), (1.0, -0.72),
                         (0.67, -1.0), (-0.76, -0.96)), np.float64)
    return np.array(((-1.0, -0.78), (-1.0, 0.55), (-0.74, 0.94),
                     (0.66, 1.0), (1.0, 0.65), (1.0, -0.67),
                     (0.68, -1.0), (-0.77, -0.97)), np.float64)


def _inside_polygon(u, v, polygon):
    inside = False
    for i, (x0, y0) in enumerate(polygon):
        x1, y1 = polygon[(i + 1) % len(polygon)]
        if (y0 > v) != (y1 > v) and u < (x1 - x0) * (v - y0) / (y1 - y0) + x0:
            inside = not inside
    return inside


def _pane(source, name, zc, angle_c, half_z, half_arc):
    polygon = _shape(name) * (half_z, half_arc)
    outline = []
    for i, p in enumerate(polygon):
        following = polygon[(i + 1) % len(polygon)]
        steps = max(1, int(math.ceil(np.linalg.norm(following - p) / 0.075)))
        outline.extend(p + (following - p) * k / steps for k in range(steps))
    outline = np.asarray(outline, np.float64)
    n = len(outline)
    rings = [outline, outline * (2.0 / 3.0), outline * (1.0 / 3.0)]
    uv = np.vstack((*rings, np.zeros((1, 2))))
    direction = -1.0 if angle_c > 90.0 else 1.0
    degrees_per_metre = 1.0 / source.skin.metres_per_degree(source._skin, zc, angle_c)
    vertices = []
    for offset in (0.028, 0.006):
        for u, v in uv:
            vertices.append(source._skin(zc + u, angle_c + direction * v * degrees_per_metre, offset))
    vertices = np.asarray(vertices, np.float32)
    centre = 3 * n
    count = centre + 1
    front = []
    for ring in range(2):
        first, following = ring * n, (ring + 1) * n
        for i in range(n):
            j = (i + 1) % n
            front.extend((first + i, first + j, following + j,
                          first + i, following + j, following + i))
    for i in range(n):
        front.extend((2 * n + i, 2 * n + (i + 1) % n, centre))
    triangles = np.asarray(front, np.uint16).reshape(-1, 3)
    back = triangles[:, ::-1] + count
    rim = []
    for i in range(n):
        j = (i + 1) % n
        rim.extend(((j, i, count + i), (j, count + i, count + j)))
    indices = np.concatenate((triangles, back, np.asarray(rim, np.uint16))).reshape(-1)
    return source.skin._outward_fixed(vertices, indices)


def _opening(z: float, angle: float, source) -> bool:
    """An inset polygonal cutout lets the outer glass/frame mask its edge."""
    for name, (zc, ac, half_z, half_arc) in PANES.items():
        u = z - zc
        direction = -1.0 if ac > 90.0 else 1.0
        v = direction * (angle - ac) * source.skin.metres_per_degree(source._skin, zc, ac)
        polygon = _shape(name) * (half_z * 0.88, half_arc * 0.88)
        if _inside_polygon(u, v, polygon):
            return True
    return False


def _open_fuselage(source):
    """Retessellate only where needed and leave four actual cockpit apertures."""
    stations = source.STATIONS
    zs = []
    for a, b in zip(stations[:-1, 0], stations[1:, 0]):
        steps = 8 if b > 8.1 and a < 10.1 else 3
        zs.extend(np.linspace(float(a), float(b), steps, endpoint=False))
    zs.append(float(stations[-1, 0]))
    segments = 192
    verts = np.asarray(
        [source.surface(z, 2.0 * math.pi * i / segments)
         for z in zs for i in range(segments)], np.float32)
    faces = []
    for j in range(len(zs) - 1):
        zmid = (zs[j] + zs[j + 1]) * 0.5
        for i in range(segments):
            angle = 360.0 * (i + 0.5) / segments
            if _opening(zmid, angle, source):
                continue
            a = j * segments + i
            b = j * segments + (i + 1) % segments
            c = b + segments
            d = a + segments
            faces.extend((a, b, c, a, c, d))
    for ring, cy in ((0, float(stations[0, 3])),
                     (len(zs) - 1, float(stations[-1, 3]))):
        centre = len(verts)
        verts = np.vstack((verts, (0.0, cy, zs[ring]))).astype(np.float32)
        for i in range(segments):
            edge = [ring * segments + i, ring * segments + (i + 1) % segments]
            if ring == 0:
                edge.reverse()
            faces.extend((centre, *edge))
    return verts, np.asarray(faces, np.uint16)


def _ellipsoid(cx, cy, cz, rx, ry, rz, *, segments=16, bands=8):
    vertices = [(cx, cy + ry, cz)]
    for band in range(1, bands):
        latitude = math.pi * band / bands
        for segment in range(segments):
            azimuth = 2.0 * math.pi * segment / segments
            vertices.append((cx + rx * math.sin(latitude) * math.cos(azimuth),
                             cy + ry * math.cos(latitude),
                             cz + rz * math.sin(latitude) * math.sin(azimuth)))
    bottom = len(vertices)
    vertices.append((cx, cy - ry, cz))
    faces = []
    for i in range(segments):
        faces.extend((0, 1 + (i + 1) % segments, 1 + i))
    for band in range(bands - 2):
        first = 1 + band * segments
        next_ring = first + segments
        for i in range(segments):
            j = (i + 1) % segments
            faces.extend((first + i, first + j, next_ring + j,
                          first + i, next_ring + j, next_ring + i))
    first = 1 + (bands - 2) * segments
    for i in range(segments):
        faces.extend((bottom, first + i, first + (i + 1) % segments))
    return source_mesh(np.asarray(vertices, np.float32), np.asarray(faces, np.uint16))


def source_mesh(vertices, indices):
    # Convex generated pieces need outward faces for Unity back-face culling.
    tri = indices.reshape(-1, 3).astype(np.int64)
    volume = float(np.einsum("ij,ij->i", vertices[tri[:, 0]],
                             np.cross(vertices[tri[:, 1]], vertices[tri[:, 2]])).sum())
    if volume < 0:
        tri = tri[:, ::-1]
    return vertices, tri.reshape(-1).astype(np.uint16)


def _bulkhead(source):
    z = 8.08
    rx, ry, cy = (float(np.interp(z, source.STATIONS[:, 0], source.STATIONS[:, i]))
                  for i in (1, 2, 3))
    segments = 48
    vertices = [(0.0, cy, z)]
    for i in range(segments):
        theta = 2.0 * math.pi * i / segments
        vertices.append((rx * 0.995 * math.cos(theta),
                         cy + ry * 0.995 * math.sin(theta), z))
    indices = []
    for i in range(segments):
        indices.extend((0, i + 1, (i + 1) % segments + 1))
    return np.asarray(vertices, np.float32), np.asarray(indices, np.uint16)


def _sweep_prop(mesh, cx: float, cy: float, cz: float, angle_degrees: float):
    verts, indices = mesh
    theta = math.radians(angle_degrees)
    radial = np.array((-math.sin(theta), math.cos(theta), 0.0), np.float32)
    tangent = np.array((math.cos(theta), math.sin(theta), 0.0), np.float32)
    shifted = verts.copy()
    radius = (shifted - np.array((cx, cy, cz), np.float32)) @ radial
    sweep = 0.15 * np.clip(radius / 1.965, 0.0, 1.0) ** 2.1
    shifted += sweep[:, None] * tangent
    return shifted, indices


def enhance(meshes, source):
    """Apply to the ATR generator output before shared frame/reflection assembly."""
    meshes["fuselage"] = _open_fuselage(source)
    for name in ("windscreen_l", "windscreen_r", "cockpit_side_l", "cockpit_side_r",
                 "windscreen_pillar_l", "windscreen_pillar_r", "windscreen_pillar_c",
                 "cockpit_sill_l", "cockpit_sill_r", "cockpit_glare_l", "cockpit_glare_r"):
        meshes.pop(name)
    for name, (z, angle, half_z, half_arc) in PANES.items():
        meshes[name] = _pane(source, name, z, angle, half_z, half_arc)

    meshes["cockpit_bulkhead"] = _bulkhead(source)
    meshes["cockpit_instrument_panel"] = source._box(0.0, 1.90, 9.43, 2.30, 0.23, 0.43)
    for side, label in ((-1.0, "left"), (1.0, "right")):
        x = side * 0.40
        meshes[f"pilot_{label}_seat"] = source._box(x, 2.04, 8.72, 0.44, 0.61, 1.30)
        meshes[f"pilot_{label}_torso"] = _ellipsoid(x, 2.18, 8.97, 0.22, 0.28, 0.21)
        meshes[f"pilot_{label}_head"] = _ellipsoid(x, 2.51, 9.08, 0.155, 0.16, 0.15)

    # The six blades already carry pitch and yellow tips. Add the swept outer
    # planform seen on modern regional props without changing their 3.93 m disc.
    suffixes = ("", "_b", "_c", "_d", "_e", "_f")
    for side, label in ((-1.0, "left"), (1.0, "right")):
        cx = side * 3.99
        for blade_index, suffix in enumerate(suffixes):
            angle = blade_index * 60.0
            for name in (f"propeller_{label}{suffix}",
                         f"propeller_{label}_tip{suffix}"):
                meshes[name] = _sweep_prop(meshes[name], cx, 2.82, 5.58, angle)
    return meshes


def finalize(meshes):
    """Use one transparent front sheet, not two stacked alpha surfaces per pane."""
    for name in PANES:
        vertices, indices = meshes[name]
        front = len(vertices) // 2
        triangles = indices.reshape(-1, 3)
        triangles = triangles[np.all(triangles < front, axis=1)]
        meshes[name] = vertices[:front], triangles.reshape(-1).astype(np.uint16)
    return meshes
