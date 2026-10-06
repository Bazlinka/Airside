#!/usr/bin/env python3
"""Fit original representative 787 openings to the actual authored hull.

These .270 x .470 m clear apertures are project art dimensions, not certified
Boeing/operator measurements. Preserve each pane station and connected topology.
Shared by both generators and the surgical shipped-kit update.
"""
import importlib.util
from pathlib import Path
import numpy as np

_SPEC = importlib.util.spec_from_file_location('skin_787_window_fit', Path(__file__).with_name('aircraft_skin.py'))
skin = importlib.util.module_from_spec(_SPEC)
_SPEC.loader.exec_module(skin)
# .94 inner seal leaves a .270 x .470 m clear contour inside the glass.
WIDTH = .270 / .94
HEIGHT = .500


def components(indices):
    parent = {int(i): int(i) for i in indices}
    def find(i):
        while parent[i] != i:
            parent[i] = parent[parent[i]]
            i = parent[i]
        return i
    for triangle in indices.reshape(-1, 3):
        for i in triangle[1:]:
            parent[find(int(i))] = find(int(triangle[0]))
    groups = {}
    for i in sorted(parent):
        groups.setdefault(find(i), []).append(i)
    return [np.asarray(g, dtype=int) for g in groups.values()]


def hull_x(triangles, y, z, side):
    """Outermost intersection of an X ray with the metre-authored triangle hull."""
    a = triangles[:, 0]; u = triangles[:, 1] - a; v = triangles[:, 2] - a
    determinant = u[:, 1] * v[:, 2] - u[:, 2] * v[:, 1]
    valid = np.abs(determinant) > 1e-10
    qy = y - a[:, 1]; qz = z - a[:, 2]
    denominator = np.where(valid, determinant, 1.)
    s = (qy * v[:, 2] - qz * v[:, 1]) / denominator
    t = (u[:, 1] * qz - u[:, 2] * qy) / denominator
    valid &= (s >= -1e-6) & (t >= -1e-6) & (s + t <= 1.000001)
    hits = a[:, 0] + s * u[:, 0] + t * v[:, 0]
    valid &= hits * side > 0
    if not valid.any():
        raise ValueError(f'No original hull intersection at Y={y:.6f}, Z={z:.6f}, side={side}')
    return side * np.max(hits[valid] * side)


def fit_windows(meshes):
    hull_vertices, hull_indices = meshes['fuselage']
    triangles = hull_vertices[hull_indices.reshape(-1, 3)].astype(float)
    for name, (vertices, indices) in list(meshes.items()):
        if not name.startswith('cabin_window_') or 'frame' in name:
            continue
        fitted = vertices.copy().astype(float)
        for component in components(indices):
            pane = vertices[component].astype(float)
            front_count = len(component) if len(component) % 2 else len(component) // 2
            # Finished kits retain only the front lite; preserve its exact datum.
            front = pane[:front_count]
            lo = front.min(axis=0); hi = front.max(axis=0); centre = (lo + hi) * .5
            side = -1 if centre[0] < 0 else 1
            fitted[component, 1] = centre[1] + (pane[:, 1] - centre[1]) * HEIGHT / (hi[1] - lo[1])
            fitted[component, 2] = centre[2] + (pane[:, 2] - centre[2]) * WIDTH / (hi[2] - lo[2])
            # skin_patch emits complete front then back layers, joined at the rim.
            if not np.array_equal(component, np.arange(component[0], component[-1] + 1)):
                raise ValueError(f'{name}: unexpected skin_patch component layout')
            # The cut-through shipped kits retain front-only odd-sized pane meshes;
            # generators retain the closed shell's even front/back vertex layout.
            half = len(component) if len(component) % 2 else len(component) // 2
            for position, index in enumerate(component):
                offset = .007 if position < half else -.004
                fitted[index, 0] = hull_x(triangles, fitted[index, 1], fitted[index, 2], side) + side * offset
        # Existing skin helper verifies outward faces against the fuselage axis.
        result, faces = skin._outward_fixed(fitted, indices)
        meshes[name] = (result.astype(np.float32), faces.astype(indices.dtype))
    return meshes
