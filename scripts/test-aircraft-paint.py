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
        fuselage = parts["fuselage"].reshape(-1, 3)
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
    check = subprocess.run([sys.executable, os.path.join(HERE, "generate-aircraft-title-layout.py"), "--check"],
                           capture_output=True, text=True)
    if check.returncode != 0:
        failures.append(check.stdout.strip())
    if failures:
        print("FAIL:\n  " + "\n  ".join(failures))
        return 1
    print(f"PASS: {len(thumbs.MODELS)} aircraft wear a fitted two-sided sash on the skin; title layouts match.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
