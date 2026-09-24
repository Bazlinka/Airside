#!/usr/bin/env python3
"""Rebuild the thirteen runtime aircraft with fitted glazing detail.

The original generators remain the source for each airframe. This finishing pass adds
one merged trim, gasket and reflection mesh per glazing group, so even the long jets
do not pay a draw call for each passenger window. It writes the same glTF, bin and
editable FBX paths; run the asset sync and thumbnail renderer afterwards.
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

    # The broad white metal surround and thin black rubber seal make the glazing
    # read as an inset opening rather than a dark decal laid over white paint.
    trim = ring(1.11 if not cockpit else 1.035, 0.88 if not cockpit else 0.925, 0.011)
    gasket = ring(0.91 if not cockpit else 0.942, 0.79 if not cockpit else 0.870, 0.014)

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
    # Select only the upper front quadrant, avoiding an artificial stripe over
    # every pane and preserving enough dark glass to read from overview.
    selected = (v > -0.05) & (u > -0.50)
    highlight_vertices = []
    highlight_indices = []
    for i in range(n):
        j = (i + 1) % n
        if not (selected[i] and selected[j]):
            continue
        base = len(highlight_vertices)
        for k in (i, j):
            a = outer[k] * 0.70 + inner[k] * 0.30 + normal * 0.019
            b = outer[k] * 0.28 + inner[k] * 0.72 + normal * 0.019
            highlight_vertices.extend((a, b))
        highlight_indices.extend((base, base + 2, base + 3, base, base + 3, base + 1))
    if not highlight_vertices:
        raise ValueError("glazing reflection has no visible faces")
    reflection = (np.asarray(highlight_vertices, np.float32),
                  np.asarray(highlight_indices, np.uint16))
    return trim, gasket, reflection


def polish(meshes: dict[str, tuple[np.ndarray, np.ndarray]]):
    skin = load_module("aircraft_skin.py")
    groups: dict[str, dict[str, list]] = {}
    count = 0
    for name, (vertices, indices) in list(meshes.items()):
        if name.startswith("cabin_window_"):
            kind = "cabin"
        elif name.startswith(("windscreen_", "cockpit_side_", "cockpit_glass_")) and not name.startswith("windscreen_pillar"):
            kind = "flightdeck"
        else:
            continue
        side = "right" if name.startswith("cabin_window_r") or name.endswith("_r") or "right" in name else "left"
        group = groups.setdefault(f"{kind}_{side}", {"trim": [], "gasket": [], "reflection": []})
        for pane_vertices, pane_indices in components(vertices, indices):
            trim, gasket, reflection = pane_detail(pane_vertices, pane_indices,
                                                     cockpit=kind == "flightdeck")
            for key, mesh in (("trim", trim), ("gasket", gasket), ("reflection", reflection)):
                group[key].append(mesh)
            count += 1
    if not count:
        raise ValueError("aircraft has no curvature-fitted glazing")
    for group_name, parts in groups.items():
        for key, pieces in parts.items():
            if pieces:
                meshes[f"glazing_{group_name}_{key}"] = skin.merge_meshes(pieces)
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
        generator = load_module(filename)
        meshes = getattr(generator, function)()
        atr = load_module("atr_aircraft_authenticity.py") if type_id == "ATR42" and not args.baseline else None
        if atr is not None:
            atr.enhance(meshes, generator)
        count = 0 if args.baseline else polish(meshes)
        if atr is not None:
            atr.finalize(meshes)
        writer(args.output_dir, basename, meshes)
        print(f"{type_id}: {count} fitted panes, {len(meshes)} named meshes -> {args.output_dir / basename}")


if __name__ == "__main__":
    main()
