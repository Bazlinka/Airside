#!/usr/bin/env python3
"""Every runtime aircraft wears fitted paint that lies on its skin (ADR 0112).

  * both sides carry a skin-conforming operator sash (livery_stripe / livery_stripe_lower),
    not a buried box, so no type needs the repeating barcode decal;
  * every sash vertex sits on the fuselage skin (within 3 % inside, for faceting, to 8 % proud);
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



BIN = 0.25


def sections(fuselage):
    z0 = fuselage[:, 2].min()
    index = np.floor((fuselage[:, 2] - z0) / BIN).astype(int)
    table = {}
    for i in np.unique(index):
        sel = fuselage[index == i]
        table[i] = (np.abs(sel[:, 0]).max(), (sel[:, 1].min() + sel[:, 1].max()) / 2, (sel[:, 1].max() - sel[:, 1].min()) / 2)
    return z0, table


def main():
    failures = []
    for cid, model, _ in thumbs.MODELS:
        parts = dict(thumbs.load_parts(os.path.join(thumbs.ART, model)))
        filename, function, _ = glazing.SOURCES[cid]
        source = getattr(glazing.load_module(filename), function)()
        fuselage = source["fuselage"][0]
        z0, table = sections(fuselage)
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
            radii = []
            for x, y, z in ribbon:
                i = int(np.floor((z - z0) / BIN))
                if i not in table:
                    continue
                rx, cy, ry = table[i]
                radii.append(np.hypot(x / rx, (y - cy) / ry))
            radii = np.asarray(radii)
            if radii.min() < 0.97 or radii.max() > 1.08:
                failures.append(f"{cid}/{name}: off the skin (radius ratio {radii.min():.3f}-{radii.max():.3f})")
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
