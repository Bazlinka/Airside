#!/usr/bin/env python3
"""Bake a coarse land-cover map of greater Adelaide into Airside's runway frame (ADR 0190).

Source: ESA WorldCover 2021 v200 (10 m), cloud-optimised GeoTIFFs on the AWS Registry of Open Data
(https://registry.opendata.aws/esa-worldcover-vito/). Licence CC BY 4.0, attribution
"© ESA WorldCover project 2021 / Contains modified Copernicus Sentinel data (2021) processed by ESA WorldCover consortium".

It colours the far terrain seen when the camera is zoomed out (beyond the satellite image at 30 km): suburbs, crops, trees,
water, instead of one flat plain colour. The grid is the far DEM's: 769 x 769 cells of 250 m, +-96 km. Each cell takes the
majority class of a 5 x 5 sub-grid of a ~45 m read of the source.

Classes (one byte per cell):
  0 water / sea   1 tree   2 shrub   3 grass   4 crop   5 built-up   6 bare   7 wetland / mangrove

Output: game/Airside/Assets/Airside/Art/Terrain/landcover_adelaide_far_v01.bin, little-endian:
  char[4]  "ALCV"
  int32    version (1)
  int32    count (cells per side)
  float32  spacing metres
  float32  origin metres (x and z of cell [0, 0]; the grid is square and centred)
  raw-deflate (no zlib header) of uint8[count * count], row-major, z rows from -extent up

Run: python3 scripts/generate-adelaide-landcover.py [--check]
Needs numpy, rasterio, pillow.
"""
from __future__ import annotations

import importlib.util
import math
import os
import struct
import sys
import zlib
from pathlib import Path

import numpy as np
import rasterio
from PIL import Image
from rasterio.enums import Resampling
from rasterio.errors import RasterioIOError
from rasterio.windows import from_bounds

ROOT = Path(__file__).resolve().parents[1]
LAYOUT_PATH = ROOT / "scripts/generate-ypad-layout.py"
OUTPUT = ROOT / "game/Airside/Assets/Airside/Art/Terrain/landcover_adelaide_far_v01.bin"
PREVIEW = ROOT / "docs/testing/map-2026-09-29/landcover-far.png"
CACHE = ROOT / "work/cache/worldcover"

EXTENT_METRES = 96_000.0
SPACING = 250.0
COUNT = int(round(2 * EXTENT_METRES / SPACING)) + 1     # 769, the far DEM's grid
STEP_DEGREES = 0.0004                                   # ~44 m north-south
URL = "https://esa-worldcover.s3.eu-central-1.amazonaws.com/v200/2021/map/ESA_WorldCover_10m_2021_v200_{0}_Map.tif"

CLASS_OF = {10: 1, 20: 2, 30: 3, 40: 4, 50: 5, 60: 6, 70: 6, 80: 0, 90: 7, 95: 7, 100: 6}
PREVIEW_COLOURS = np.array([(40, 80, 130), (30, 90, 40), (110, 120, 60), (150, 160, 80), (200, 180, 90), (150, 150, 155),
                            (190, 170, 130), (60, 110, 110)], dtype=np.uint8)


def load_layout():
    spec = importlib.util.spec_from_file_location("ypad_layout", LAYOUT_PATH)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def local_to_lonlat(layout, x, z):
    """Runway frame -> lon/lat, the convention every YPAD generator shares."""
    lon_scale = 111_320.0 * math.cos(math.radians(layout.LAT0))
    lat_scale = 110_574.0
    east = layout.MID[0] + x * layout.U[0] + z * layout.N[0]
    north = layout.MID[1] + x * layout.U[1] + z * layout.N[1]
    return layout.LON0 + east / lon_scale, layout.LAT0 + north / lat_scale


def read_mosaic(lon_min, lat_min, lon_max, lat_max):
    """Our class ids over a lon/lat box as one array (open sea, where no tile exists, is water)."""
    width = int(math.ceil((lon_max - lon_min) / STEP_DEGREES))
    height = int(math.ceil((lat_max - lat_min) / STEP_DEGREES))
    mosaic = np.zeros((height, width), dtype=np.uint8)
    lut = np.zeros(256, dtype=np.uint8)
    for source, ours in CLASS_OF.items():
        lut[source] = ours
    for lat0 in range(int(math.floor(lat_min / 3.0)) * 3, int(math.ceil(lat_max / 3.0)) * 3, 3):
        for lon0 in range(int(math.floor(lon_min / 3.0)) * 3, int(math.ceil(lon_max / 3.0)) * 3, 3):
            name = f"{'S' if lat0 < 0 else 'N'}{abs(lat0):02d}{'E' if lon0 >= 0 else 'W'}{abs(lon0):03d}"
            try:
                src = rasterio.open(URL.format(name))
            except RasterioIOError:
                print(f"  no land-cover tile {name} (open sea): water")
                continue
            with src:
                left, right = max(lon_min, lon0), min(lon_max, lon0 + 3)
                bottom, top = max(lat_min, lat0), min(lat_max, lat0 + 3)
                if left >= right or bottom >= top:
                    continue
                row = int(round((lat_max - top) / STEP_DEGREES))
                col = int(round((left - lon_min) / STEP_DEGREES))
                h = min(height - row, int(round((top - bottom) / STEP_DEGREES)))
                w = min(width - col, int(round((right - left) / STEP_DEGREES)))
                window = from_bounds(left, bottom, right, top, transform=src.transform)
                part = src.read(1, window=window, out_shape=(h, w), resampling=Resampling.nearest)
                mosaic[row:row + h, col:col + w] = lut[part]
                print(f"  {name}: {w}x{h}")
    return mosaic


def build():
    layout = load_layout()
    corners = [local_to_lonlat(layout, sx * EXTENT_METRES * 1.02, sz * EXTENT_METRES * 1.02)
               for sx in (-1, 1) for sz in (-1, 1)]
    lon_min, lon_max = min(c[0] for c in corners), max(c[0] for c in corners)
    lat_min, lat_max = min(c[1] for c in corners), max(c[1] for c in corners)
    mosaic = read_mosaic(lon_min, lat_min, lon_max, lat_max)
    rows, cols = mosaic.shape

    sub = 5
    offsets = (np.arange(sub) + 0.5) / sub - 0.5
    axis = -EXTENT_METRES + np.arange(COUNT) * SPACING
    votes = np.zeros((8, COUNT, COUNT), dtype=np.uint8)
    for oz in offsets:
        for ox in offsets:
            gx, gz = np.meshgrid(axis + ox * SPACING, axis + oz * SPACING)
            lon, lat = local_to_lonlat(layout, gx, gz)
            c = np.clip(((lon - lon_min) / (lon_max - lon_min) * cols).astype(int), 0, cols - 1)
            r = np.clip(((lat_max - lat) / (lat_max - lat_min) * rows).astype(int), 0, rows - 1)
            cls = mosaic[r, c]
            for k in range(8):
                votes[k] += (cls == k)
    # Ties go to the lower id, which keeps the result deterministic.
    return votes.argmax(axis=0).astype(np.uint8)


def encode(cells):
    compressor = zlib.compressobj(9, zlib.DEFLATED, -15)
    payload = compressor.compress(cells.tobytes()) + compressor.flush()
    return b"ALCV" + struct.pack("<iiff", 1, COUNT, SPACING, -EXTENT_METRES) + payload


def main():
    os.environ.setdefault("GDAL_DISABLE_READDIR_ON_OPEN", "EMPTY_DIR")
    os.environ.setdefault("AWS_NO_SIGN_REQUEST", "YES")
    if os.path.exists("/root/.ccr/ca-bundle.crt"):
        os.environ.setdefault("CURL_CA_BUNDLE", "/root/.ccr/ca-bundle.crt")

    cells = build()
    blob = encode(cells)
    if "--check" in sys.argv:
        if OUTPUT.read_bytes() != blob:
            sys.exit(f"{OUTPUT.name} is out of date: run scripts/generate-adelaide-landcover.py")
        print(f"{OUTPUT.name} is up to date")
        return
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_bytes(blob)
    shares = np.bincount(cells.ravel(), minlength=8) / cells.size * 100
    print(f"Wrote {OUTPUT} ({COUNT}x{COUNT} at {SPACING:.0f} m, {len(blob)} bytes)")
    print("  " + ", ".join(f"{name} {share:.1f}%" for name, share in
                           zip("water tree shrub grass crop built bare wetland".split(), shares)))
    PREVIEW.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(np.flipud(PREVIEW_COLOURS[cells])).save(PREVIEW, optimize=True)
    print(f"  preview {PREVIEW}")


if __name__ == "__main__":
    main()
