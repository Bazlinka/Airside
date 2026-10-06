#!/usr/bin/env python3
"""Render OSM / SA LiDAR / Overture building outlines side by side for one patch (map accuracy pass, ADR 0236).

Run: work/venv/bin/python scripts/compare-building-sources.py X Z [half_metres] -> work/compare-<X>-<Z>.png
X, Z are runway-frame metres (generate-ypad-layout.py). Red = OSM, green = SA LiDAR 2022, blue = Overture.
"""
import glob, importlib.util, json, math, sys
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("l", ROOT / "scripts/generate-ypad-layout.py")
L = importlib.util.module_from_spec(spec); spec.loader.exec_module(L)
ls, las = 111_320.0 * math.cos(math.radians(L.LAT0)), 110_574.0


def local(lon, lat):
    e, n = (lon - L.LON0) * ls - L.MID[0], (lat - L.LAT0) * las - L.MID[1]
    return e * L.U[0] + n * L.U[1], e * L.N[0] + n * L.N[1]


cx, cz = float(sys.argv[1]), float(sys.argv[2]); half = float(sys.argv[3]) if len(sys.argv) > 3 else 250.0
S = 1600; k = S / (2 * half)
osm = json.load(open(sorted(glob.glob(str(ROOT / "docs/data/osm/adelaide-suburb-buildings-*.json")))[-1]))["buildings"]
sa = json.load(open(ROOT / "work/cache/sa-open/ypad-lidar-buildings-2022.json"))["features"]
ov = json.load(open(sorted(glob.glob(str(ROOT / "work/cache/overture/ypad-buildings-*.json")))[-1]))["features"]
panels = []
for name, rings, col in (("OSM", [b["geometry"] for b in osm], (230, 50, 50)), ("SA LiDAR", [f["ring"] for f in sa], (30, 160, 60)),
                         ("Overture", [f["ring"] for f in ov], (40, 90, 230))):
    im = Image.new("RGB", (S, S), (245, 245, 240)); d = ImageDraw.Draw(im); n = 0
    for ring in rings:
        pts = [local(*p) for p in ring]
        if not any(abs(x - cx) < half and abs(z - cz) < half for x, z in pts): continue
        d.polygon([((x - cx + half) * k, (z - cz + half) * k) for x, z in pts], outline=col, fill=tuple(int(c * .25 + 190) for c in col)); n += 1
    d.text((10, 10), f"{name}: {n}", fill=(0, 0, 0)); panels.append(im)
out = Image.new("RGB", (S * 3, S)); [out.paste(p, (i * S, 0)) for i, p in enumerate(panels)]
dest = ROOT / f"work/compare-{int(cx)}-{int(cz)}.png"; out.resize((S * 3 // 2, S // 2)).save(dest); print(dest)
