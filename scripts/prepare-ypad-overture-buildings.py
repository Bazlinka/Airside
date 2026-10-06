#!/usr/bin/env python3
"""Trim and validate Overture buildings for the Adelaide suburbs generator (map accuracy pass, ADR 0236).

Inputs (both git-ignored caches, see their fetch scripts):
  work/cache/overture/ypad-buildings-<release>.json   scripts/fetch-ypad-overture.py
  work/cache/sa-open/UrbanBuildings2022.tif           SA Government LiDAR building raster (CC BY 4.0)

Overture's Microsoft ML footprints are clean individual houses but sometimes hallucinate on solar farms,
shade sails and yards. The SA LiDAR raster is independent ground truth for "something roof-high is here", so each
footprint is scored by the fraction of sample points inside it that the LiDAR calls a building. Features that
carry an OpenStreetMap source are kept regardless (a mapper saw them); Microsoft-only features need
LiDAR cover >= MIN_COVER. The SA raster merges terraces into blobs and has sea-clutter, so it is a validator
only, never a source of outlines.

Only the band the suburbs generator uses is kept (BAND_METRES outside the airfield rectangle), which makes the
committed snapshot small. Output is gzip JSON so it is cheap to commit:
  docs/data/overture/ypad-suburb-buildings-<release>.json.gz
  {"release","licence","buildings":[{"id","h","f","c","s","cov","g":[[lon,lat],...]}]}   (h height m, f floors,
  c class, s 1 when an OSM source, cov LiDAR cover 0-1)

Run: work/venv/bin/python scripts/prepare-ypad-overture-buildings.py
"""
from __future__ import annotations

import glob
import gzip
import importlib.util
import json
import math
from pathlib import Path

import numpy as np
import rasterio
import shapely
from pyproj import Transformer
from rasterio.windows import from_bounds
from shapely.geometry import Polygon

ROOT = Path(__file__).resolve().parents[1]
RASTER = ROOT / "work/cache/sa-open/UrbanBuildings2022.tif"
BAND_METRES = 2100.0
HALF_X, HALF_Z = 1950.0, 1400.0
MIN_COVER = 0.35
MIN_AREA = 12.0


def main():
    spec = importlib.util.spec_from_file_location("ypad_layout", ROOT / "scripts/generate-ypad-layout.py")
    L = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(L)
    ls, las = 111_320.0 * math.cos(math.radians(L.LAT0)), 110_574.0

    def local(lon, lat):
        e, n = (lon - L.LON0) * ls - L.MID[0], (lat - L.LAT0) * las - L.MID[1]
        return e * L.U[0] + n * L.U[1], e * L.N[0] + n * L.N[1]

    def outside(x, z):
        return math.hypot(max(abs(x) - HALF_X, 0.0), max(abs(z) - HALF_Z, 0.0))

    src_path = sorted(glob.glob(str(ROOT / "work/cache/overture/ypad-buildings-*.json")))[-1]
    src = json.load(open(src_path))
    band = []
    for f in src["features"]:
        pts = [local(lon, lat) for lon, lat in f["ring"]]
        cx, cz = sum(p[0] for p in pts) / len(pts), sum(p[1] for p in pts) / len(pts)
        if outside(cx, cz) > BAND_METRES:
            continue
        poly = Polygon(pts)
        if not poly.is_valid or poly.area < MIN_AREA:
            continue
        band.append((f, poly))
    print(f"{len(band)} of {len(src['features'])} Overture buildings inside the {BAND_METRES:.0f} m band")

    to_mga = Transformer.from_crs(4326, 7854, always_xy=True)
    allx, ally = [], []
    for f, _ in band:
        for lon, lat in f["ring"]:
            x, y = to_mga.transform(lon, lat)
            allx.append(x); ally.append(y)
    with rasterio.open(RASTER) as r:
        win = from_bounds(min(allx) - 20, min(ally) - 20, max(allx) + 20, max(ally) + 20, r.transform).round_offsets().round_lengths()
        mask = r.read(1, window=win) > 0
        inv = ~r.window_transform(win)
    print(f"LiDAR window {mask.shape[1]}x{mask.shape[0]}")

    out, dropped = [], 0
    for f, poly in band:
        mx = [to_mga.transform(lon, lat) for lon, lat in f["ring"]]
        gp = Polygon(mx)
        minx, miny, maxx, maxy = gp.bounds
        gx, gy = np.meshgrid(np.linspace(minx, maxx, 7), np.linspace(miny, maxy, 7))
        inside = shapely.contains_xy(gp, gx.ravel(), gy.ravel())
        if inside.sum() < 3:
            gx = np.array([gp.representative_point().x] * 3); gy = np.array([gp.representative_point().y] * 3)
        else:
            gx, gy = gx.ravel()[inside], gy.ravel()[inside]
        cols, rows = inv * (gx, gy)
        cols, rows = np.floor(cols).astype(int), np.floor(rows).astype(int)
        ok = (rows >= 0) & (rows < mask.shape[0]) & (cols >= 0) & (cols < mask.shape[1])
        cover = float(mask[rows[ok], cols[ok]].mean()) if ok.any() else 0.0
        osm = "OpenStreetMap" in f["sources"]
        if not osm and cover < MIN_COVER:
            dropped += 1
            continue
        out.append({"id": f["id"], "h": f["height"], "f": f["floors"], "c": f["class"], "s": 1 if osm else 0,
                    "cov": round(cover, 2), "g": f["ring"]})
    dest = ROOT / f"docs/data/overture/ypad-suburb-buildings-{src['release']}.json.gz"
    dest.parent.mkdir(parents=True, exist_ok=True)
    with gzip.open(dest, "wt", compresslevel=9) as fh:
        json.dump({"release": src["release"], "licence": "ODbL 1.0 (Overture Maps buildings theme)",
                   "validated_with": "SA Government LiDAR building footprints 2022 (CC BY 4.0)",
                   "min_cover": MIN_COVER, "band_metres": BAND_METRES, "buildings": out}, fh, separators=(",", ":"))
    print(f"kept {len(out)}, dropped {dropped} unsupported ML footprints -> {dest} ({dest.stat().st_size/1e6:.1f} MB)")


if __name__ == "__main__":
    main()
