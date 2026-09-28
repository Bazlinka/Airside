#!/usr/bin/env python3
"""Every runtime aircraft wears fitted paint that lies on its skin (ADR 0112).

  * both sides carry a skin-conforming operator sash (livery_stripe / livery_stripe_lower),
    not a buried box, so no type needs the repeating barcode decal;
  * every sash vertex sits 3-25 mm outside the actual fuselage triangles;
  * the fuselage title layout in AircraftTitlePaint.cs matches the meshes
    (scripts/generate-aircraft-title-layout.py --check).
"""
import importlib.util
import os
import subprocess
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("thumbs", os.path.join(HERE, "render-aircraft-thumbnails.py"))
thumbs = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(thumbs)
_glazing_spec = importlib.util.spec_from_file_location("glazing", os.path.join(HERE, "polish-aircraft-glazing.py"))
glazing = importlib.util.module_from_spec(_glazing_spec)
_glazing_spec.loader.exec_module(glazing)



def fitted_offsets(mesh, ribbon):
    """Ray-test the real loft, including tapered/non-elliptical regional noses.

    The old quarter-metre ellipse estimate wrongly rejected paint clipped exactly
    onto a tapered triangle, and could accept paint hovering above that triangle.
    """
    v, indices = mesh
    tris = v[indices.reshape(-1, 3)].astype(np.float64)
    grid = {}
    cell = 0.5
    for i, tri in enumerate(tris):
        lo = np.floor((tri[:, (2, 1)].min(0)-1e-5) / cell).astype(int)
        hi = np.floor((tri[:, (2, 1)].max(0)+1e-5) / cell).astype(int)
        for z in range(lo[0], hi[0] + 1):
            for y in range(lo[1], hi[1] + 1):
                grid.setdefault((z, y), []).append(i)
    offsets = []
    for p in np.unique(ribbon.reshape(-1, 3), axis=0):
        key = tuple(np.floor(p[[2, 1]] / cell).astype(int))
        t = tris[grid.get(key, [])]
        if not len(t):
            offsets.append(float('inf')); continue
        a, b, c = t[:, 0, :], t[:, 1, :], t[:, 2, :]
        d = (b[:, 1]-c[:, 1])*(a[:, 2]-c[:, 2]) + (c[:, 2]-b[:, 2])*(a[:, 1]-c[:, 1])
        valid = np.abs(d) > 1e-9
        d = np.where(valid, d, 1.)
        u = ((b[:, 1]-c[:, 1])*(p[2]-c[:, 2]) + (c[:, 2]-b[:, 2])*(p[1]-c[:, 1])) / d
        w = ((c[:, 1]-a[:, 1])*(p[2]-c[:, 2]) + (a[:, 2]-c[:, 2])*(p[1]-c[:, 1])) / d
        valid &= (u >= -1e-3) & (w >= -1e-3) & (u+w <= 1.001)
        x = u*a[:, 0]+w*b[:, 0]+(1-u-w)*c[:, 0]
        # At the crown the original loft vertex is exactly on x=0 (or a few
        # float ulps to either side); a side-only ray must include that boundary.
        valid &= x*np.sign(p[0]) >= -1e-5
        offsets.append(np.min(np.abs(p[0])-np.abs(x[valid])) if valid.any() else float('inf'))
    return np.asarray(offsets)


def main():
    failures = []
    for cid, model, _ in thumbs.MODELS:
        parts = dict(thumbs.load_parts(os.path.join(thumbs.ART, model)))
        filename, function, _ = glazing.SOURCES[cid]
        source = getattr(glazing.load_module(filename), function)()
        for name in ("livery_stripe", "livery_stripe_lower"):
            if name not in parts:
                failures.append(f"{cid}: no {name}")
                continue
            ribbon = parts[name].reshape(-1, 3)
            if len(np.unique(ribbon, axis=0)) < 100:
                failures.append(f"{cid}/{name}: a box, not a skin-conforming sash")
                continue
            sides = np.sign(ribbon[:, 0])
            if len(np.unique(sides[np.abs(ribbon[:, 0]) > 0.2])) != 1:
                failures.append(f"{cid}/{name}: wraps both sides")
            offsets = fitted_offsets(source["fuselage"], ribbon)
            if not np.all(np.isfinite(offsets)) or offsets.min() < 0.003 or offsets.max() > 0.025:
                failures.append(f"{cid}/{name}: paint must be 3-25 mm outside actual skin ({offsets.min():.4f}-{offsets.max():.4f} m)")
        for role in ("livery_secondary", "livery_emblem", "livery_cowl_left", "livery_cowl_right"):
            if role not in parts or not np.isfinite(parts[role]).all():
                failures.append(f"{cid}: missing or invalid {role}")
        for group in ("cabin_left", "cabin_right", "flightdeck_left", "flightdeck_right"):
            if f"glazing_{group}_interior" not in parts:
                failures.append(f"{cid}: no recessed {group} interior")
        for side in ("left", "right"):
            for role in ("head", "uniform"):
                if f"pilot_{side}_{role}" not in parts:
                    failures.append(f"{cid}: no {side} pilot {role}")
        for pane_name, (vertices, indices) in source.items():
            if not (pane_name.startswith(("cabin_window_", "cockpit_side_", "windscreen_"))
                    and not pane_name.startswith("windscreen_pillar")):
                continue
            front_faces = 0
            for component_vertices, component_indices in glazing.components(vertices, indices):
                front_count = len(component_vertices) // 2
                front_faces += np.all(component_indices.reshape(-1, 3) < front_count,
                                      axis=1).sum()
            if pane_name not in parts:
                failures.append(f"{cid}: missing pane {pane_name}")
            elif len(parts[pane_name]) != front_faces:
                failures.append(f"{cid}/{pane_name}: pane is not a single outer lite")

    check = subprocess.run([sys.executable, os.path.join(HERE, "generate-aircraft-title-layout.py"), "--check"],
                           capture_output=True, text=True)
    if check.returncode != 0:
        failures.append(check.stdout.strip())
    if failures:
        print("FAIL:\n  " + "\n  ".join(failures))
        return 1
    print(f"PASS: {len(thumbs.MODELS)} aircraft have fitted paint, single-layer panes, recessed interiors and pilots; title layouts match.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
