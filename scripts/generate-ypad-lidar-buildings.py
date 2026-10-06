#!/usr/bin/env python3
"""Vectorise the SA Government LiDAR building footprints around Adelaide Airport (map accuracy pass, ADR 0236).

Source: "Metropolitan Adelaide tree canopy, green spaces and built environment 2022" — Building footprints
(UrbanBuildings2022.tif), Government of South Australia, Department for Environment and Water, CC BY 4.0
(https://data.sa.gov.au/data/dataset/0f7ab193-326b-4894-aec4-298b5e6ea0ba). A 0.5 m binary raster (EPSG:7854,
GDA2020 / MGA zone 54) derived from 2022 LiDAR and high-resolution multispectral imagery. Download
UrbanBuildings2022.zip from the dataset page into work/cache/sa-open/ and unzip it.

The raster is cut to the game's area (the airfield rectangle plus a band), polygonised, cleaned (opened to drop
fence/car noise, small specks removed), and simplified to 0.8 m. Output is lon/lat so every other generator can
project it into the runway frame the usual way.

Output: work/cache/sa-open/ypad-lidar-buildings-2022.json (git-ignored, 35 MB, reproducible from the raster)
  {"source","licence","features":[{"id","area","ring":[[lon,lat],...]}]}   (outer ring, 6 dp, ids "sa22-<n>")

Run: work/venv/bin/python scripts/generate-ypad-lidar-buildings.py [--half-x 6500 --half-z 5500]
Needs numpy, rasterio, pyproj, shapely, scipy.
"""
from __future__ import annotations

import argparse
import importlib.util
import json
import math
from pathlib import Path

import numpy as np
import rasterio
from pyproj import Transformer
from rasterio import features
from rasterio.windows import from_bounds
from scipy import ndimage
from shapely.geometry import Polygon, shape
from shapely.ops import transform as shp_transform

ROOT = Path(__file__).resolve().parents[1]
RASTER = ROOT / "work/cache/sa-open/UrbanBuildings2022.tif"
OUTPUT = ROOT / "work/cache/sa-open/ypad-lidar-buildings-2022.json"
MIN_AREA = 9.0          # m², below this is a shed roof speck or noise
SIMPLIFY = 0.8          # m — above the 0.5 m pixel stair-step, below a wall jog


def load_layout():
    spec = importlib.util.spec_from_file_location("ypad_layout", ROOT / "scripts/generate-ypad-layout.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--half-x", type=float, default=6500.0, help="half width of the cut along the runway frame x, m")
    ap.add_argument("--half-z", type=float, default=5500.0, help="half width across (z), m")
    args = ap.parse_args()
    layout = load_layout()
    to_mga = Transformer.from_crs(4326, 7854, always_xy=True)
    to_ll = Transformer.from_crs(7854, 4326, always_xy=True)
    lon_scale = 111_320.0 * math.cos(math.radians(layout.LAT0))
    lat_scale = 110_574.0

    def frame_to_lonlat(x, z):
        east = layout.MID[0] + x * layout.U[0] + z * layout.N[0]
        north = layout.MID[1] + x * layout.U[1] + z * layout.N[1]
        return layout.LON0 + east / lon_scale, layout.LAT0 + north / lat_scale

    corners = [to_mga.transform(*frame_to_lonlat(sx * args.half_x, sz * args.half_z)) for sx in (-1, 1) for sz in (-1, 1)]
    xs, ys = [c[0] for c in corners], [c[1] for c in corners]
    cut = Polygon([corners[0], corners[1], corners[3], corners[2]])

    with rasterio.open(RASTER) as src:
        window = from_bounds(min(xs), min(ys), max(xs), max(ys), src.transform)
        window = window.round_offsets().round_lengths()
        data = src.read(1, window=window)
        transform = src.window_transform(window)
    print(f"cut {data.shape[1]}x{data.shape[0]} px, {int((data > 0).sum())} building px")
    mask = ndimage.binary_opening(data > 0, structure=np.ones((3, 3)))     # 1.5 m: drops fences, wires, cars
    labelled = mask.astype(np.uint8)

    feats = []
    for geom, value in features.shapes(labelled, mask=mask, transform=transform):
        poly = shape(geom)
        if poly.area < MIN_AREA or not poly.intersects(cut):
            continue
        poly = poly.simplify(SIMPLIFY, preserve_topology=True)
        if poly.is_empty:
            continue
        parts = [poly] if poly.geom_type == "Polygon" else list(poly.geoms)
        for part in parts:
            if part.area < MIN_AREA or not cut.contains(part.centroid):
                continue
            ring = [list(map(lambda v: round(v, 6), to_ll.transform(x, y))) for x, y in part.exterior.coords]
            feats.append({"id": f"sa22-{len(feats)}", "area": round(part.area, 1), "ring": ring})
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(json.dumps({
        "source": "Government of South Australia, Department for Environment and Water — Metropolitan Adelaide "
                  "tree canopy, green spaces and built environment 2022, Building footprints",
        "licence": "CC BY 4.0",
        "dataset": "https://data.sa.gov.au/data/dataset/0f7ab193-326b-4894-aec4-298b5e6ea0ba",
        "cut_half_metres": [args.half_x, args.half_z],
        "features": feats,
    }, separators=(",", ":")))
    print(f"{len(feats)} footprints -> {OUTPUT} ({OUTPUT.stat().st_size/1e6:.1f} MB)")


if __name__ == "__main__":
    main()
