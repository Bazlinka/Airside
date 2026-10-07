#!/usr/bin/env python3
"""Bake a state-wide land-cover map of South Australia for the streamed flight/overview terrain (ADR 0250).

Source: ESA WorldCover 2021 v200 (10 m), cloud-optimised GeoTIFFs on the AWS Registry of Open Data
(https://registry.opendata.aws/esa-worldcover-vito/). Licence CC BY 4.0, attribution
"© ESA WorldCover project 2021 / Contains modified Copernicus Sentinel data (2021) processed by ESA WorldCover consortium".
No satellite imagery is shipped: only one class byte per ~1 km cell.

Coverage is `FlightWorldGrid.Covered` (128-142 E, 39-25 S). Each 0.01 degree cell (~1.1 km north-south, ~0.9 km east-west)
takes the majority class of a 4 x 4 sub-grid read from the COG overviews (0.0025 degree), ties to the lower id so the file is
deterministic. Classes are the same eight as `AdelaideFarLandCover` (and ADR 0190):
  0 water / sea   1 tree   2 shrub   3 grass   4 crop   5 built-up   6 bare   7 wetland / mangrove
Open sea, where WorldCover has no tile, is water.

Output: game/Airside/Assets/Airside/Art/Terrain/landcover_south_australia_v01.bin, little-endian:
  char[4]   "SALC"
  int32     version (1)
  int32     width  (cells, west to east)
  int32     height (cells, south to north)
  float64   west longitude of cell [0, 0]
  float64   south latitude of cell [0, 0]
  float64   step in degrees (cell [ix, iz] covers west + ix * step .. + step, south + iz * step .. + step)
  raw-deflate (no zlib header) of uint8[width * height], row-major, row 0 = southernmost

Run: python3 scripts/generate-sa-landcover.py [--check]
Needs numpy, rasterio, pillow. Downloads overview reads of ~30 tiles (a few minutes).
"""
from __future__ import annotations

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
OUTPUT = ROOT / "game/Airside/Assets/Airside/Art/Terrain/landcover_south_australia_v01.bin"
STREAMING = ROOT / "game/Airside/Assets/StreamingAssets/Airside/Art/Terrain/landcover_south_australia_v01.bin"
PREVIEW = ROOT / "docs/testing/sa-landcover-2026-10-07/landcover-south-australia.png"

WEST, EAST, SOUTH, NORTH = 128.0, 142.0, -39.0, -25.0
STEP = 0.01
SUB = 4
READ_STEP = STEP / SUB
WIDTH = int(round((EAST - WEST) / STEP))
HEIGHT = int(round((NORTH - SOUTH) / STEP))
URL = "https://esa-worldcover.s3.eu-central-1.amazonaws.com/v200/2021/map/ESA_WorldCover_10m_2021_v200_{0}_Map.tif"

CLASS_OF = {10: 1, 20: 2, 30: 3, 40: 4, 50: 5, 60: 6, 70: 6, 80: 0, 90: 7, 95: 7, 100: 6}
CLASS_NAMES = "water tree shrub grass crop built bare wetland".split()
PREVIEW_COLOURS = np.array([(40, 80, 130), (30, 90, 40), (110, 120, 60), (150, 160, 80), (200, 180, 90), (150, 150, 155),
                            (190, 170, 130), (60, 110, 110)], dtype=np.uint8)


def read_mosaic():
    """Our class ids over the box at READ_STEP, north-up (row 0 = NORTH)."""
    width = int(round((EAST - WEST) / READ_STEP))
    height = int(round((NORTH - SOUTH) / READ_STEP))
    mosaic = np.zeros((height, width), dtype=np.uint8)
    lut = np.zeros(256, dtype=np.uint8)
    for source, ours in CLASS_OF.items():
        lut[source] = ours
    for lat0 in range(int(math.floor(SOUTH / 3.0)) * 3, int(math.ceil(NORTH / 3.0)) * 3, 3):
        for lon0 in range(int(math.floor(WEST / 3.0)) * 3, int(math.ceil(EAST / 3.0)) * 3, 3):
            name = f"{'S' if lat0 < 0 else 'N'}{abs(lat0):02d}{'E' if lon0 >= 0 else 'W'}{abs(lon0):03d}"
            left, right = max(WEST, lon0), min(EAST, lon0 + 3)
            bottom, top = max(SOUTH, lat0), min(NORTH, lat0 + 3)
            if left >= right or bottom >= top:
                continue
            try:
                src = rasterio.open(URL.format(name))
            except RasterioIOError:
                print(f"  no land-cover tile {name} (open sea): water")
                continue
            with src:
                row = int(round((NORTH - top) / READ_STEP))
                col = int(round((left - WEST) / READ_STEP))
                h = min(height - row, int(round((top - bottom) / READ_STEP)))
                w = min(width - col, int(round((right - left) / READ_STEP)))
                window = from_bounds(left, bottom, right, top, transform=src.transform)
                part = src.read(1, window=window, out_shape=(h, w), resampling=Resampling.nearest)
                mosaic[row:row + h, col:col + w] = lut[part]
                print(f"  {name}: {w}x{h}")
    return mosaic


def build():
    mosaic = read_mosaic()
    votes = np.empty((8, HEIGHT, WIDTH), dtype=np.uint16)
    blocks = mosaic.reshape(HEIGHT, SUB, WIDTH, SUB)
    for k in range(8):
        votes[k] = (blocks == k).sum(axis=(1, 3))
    # argmax picks the lowest id on a tie: deterministic.
    north_up = votes.argmax(axis=0).astype(np.uint8)
    return np.flipud(north_up)          # row 0 = south


def encode(cells):
    compressor = zlib.compressobj(9, zlib.DEFLATED, -15)
    payload = compressor.compress(np.ascontiguousarray(cells).tobytes()) + compressor.flush()
    return b"SALC" + struct.pack("<iiiddd", 1, WIDTH, HEIGHT, WEST, SOUTH, STEP) + payload


def main():
    os.environ.setdefault("GDAL_DISABLE_READDIR_ON_OPEN", "EMPTY_DIR")
    os.environ.setdefault("AWS_NO_SIGN_REQUEST", "YES")
    if os.path.exists("/root/.ccr/ca-bundle.crt"):
        os.environ.setdefault("CURL_CA_BUNDLE", "/root/.ccr/ca-bundle.crt")

    cells = build()
    blob = encode(cells)
    if "--check" in sys.argv:
        if OUTPUT.read_bytes() != blob:
            sys.exit(f"{OUTPUT.name} is out of date: run scripts/generate-sa-landcover.py")
        print(f"{OUTPUT.name} is up to date")
        return
    for path in (OUTPUT, STREAMING):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(blob)
    shares = np.bincount(cells.ravel(), minlength=8) / cells.size * 100
    print(f"Wrote {OUTPUT} ({WIDTH}x{HEIGHT} at {STEP} deg, {len(blob)} bytes) and the StreamingAssets copy")
    print("  " + ", ".join(f"{n} {s:.1f}%" for n, s in zip(CLASS_NAMES, shares)))
    PREVIEW.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(np.flipud(PREVIEW_COLOURS[cells])).save(PREVIEW, optimize=True)
    print(f"  preview {PREVIEW}")


if __name__ == "__main__":
    main()
