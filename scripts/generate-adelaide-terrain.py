#!/usr/bin/env python3
"""Bake real Adelaide terrain heights into Airside's runway frame (plan P6, ADR 0158).

Source: Copernicus DEM GLO-30 (30 m), cloud-optimised GeoTIFFs on the AWS Registry of Open
Data (https://registry.opendata.aws/copernicus-dem/). Licence: free for any use with the
attribution "© DLR e.V. 2010-2014 and © Airbus Defence and Space GmbH 2014-2018 provided
under COPERNICUS by the European Union and ESA; all rights reserved".

GLO-30 is a surface model: roofs and tree canopy are in it. Each output cell therefore takes
a low percentile of the source samples it covers on the plain (below 80 m), which finds the
ground between houses, and the median in the Hills; then a light blur. The plain is 0-60 m and gentle; the Hills (Mount Lofty 727 m) keep their
shape because a 120 m cell on a hillside is still hillside.

Output: game/Airside/Assets/Airside/Art/Terrain/dem_adelaide_runway_v01.bin, little-endian:
  char[4]  "ADEM"
  int32    version (1)
  int32    count (samples per side)
  float32  spacing metres
  float32  origin metres (x and z of sample [0, 0]; the grid is square and centred)
  float32  height scale (metres per stored unit)
  int16[count * count]  heights above sea level, row-major, z rows from -extent up
Sea and anything below 0 m are stored as 0.

Run: python3 scripts/generate-adelaide-terrain.py
Needs numpy, scipy, rasterio, pyproj, pillow.
"""
from __future__ import annotations

import importlib.util
import math
import os
import struct
from pathlib import Path

import numpy as np
import rasterio
from PIL import Image
from rasterio.windows import from_bounds
from scipy.ndimage import gaussian_filter

ROOT = Path(__file__).resolve().parents[1]
LAYOUT_PATH = ROOT / "scripts/generate-ypad-layout.py"
OUTPUT = ROOT / "game/Airside/Assets/Airside/Art/Terrain/dem_adelaide_runway_v01.bin"
PREVIEW = ROOT / "docs/testing/surroundings-2026-09-28/terrain-hillshade.jpg"
CACHE = ROOT / "work/cache/copernicus-dem"

EXTENT_METRES = 32_000.0
SPACING = 125.0
COUNT = int(round(2 * EXTENT_METRES / SPACING)) + 1   # 513
HEIGHT_SCALE = 0.05                                     # 5 cm steps, int16 covers 1.6 km
GROUND_PERCENTILE = 20
TILES = ["S35_00_E138_00", "S36_00_E138_00"]
URL = "https://copernicus-dem-30m.s3.amazonaws.com/Copernicus_DSM_COG_10_{0}_DEM/Copernicus_DSM_COG_10_{0}_DEM.tif"


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
    """The DEM over a lon/lat box as one array plus its geotransform (1 arc-second grid)."""
    CACHE.mkdir(parents=True, exist_ok=True)
    cached = CACHE / f"mosaic_{lon_min:.3f}_{lat_min:.3f}_{lon_max:.3f}_{lat_max:.3f}.npz"
    if cached.exists():
        data = np.load(cached)
        return data["heights"], tuple(data["box"])
    step = 1.0 / 3600.0
    width = int(math.ceil((lon_max - lon_min) / step))
    height = int(math.ceil((lat_max - lat_min) / step))
    mosaic = np.full((height, width), np.nan, dtype=np.float32)
    for tile in TILES:
        with rasterio.open(URL.format(tile)) as src:
            b = src.bounds
            left, right = max(lon_min, b.left), min(lon_max, b.right)
            bottom, top = max(lat_min, b.bottom), min(lat_max, b.top)
            if left >= right or bottom >= top:
                continue
            window = from_bounds(left, bottom, right, top, transform=src.transform)
            part = src.read(1, window=window).astype(np.float32)
            row = int(round((lat_max - top) / step))
            col = int(round((left - lon_min) / step))
            h = min(part.shape[0], height - row)
            w = min(part.shape[1], width - col)
            mosaic[row:row + h, col:col + w] = part[:h, :w]
    mosaic = np.nan_to_num(mosaic, nan=0.0)
    box = (lon_min, lat_min, lon_max, lat_max)
    np.savez_compressed(cached, heights=mosaic, box=np.array(box))
    return mosaic, box


def main():
    os.environ.setdefault("GDAL_DISABLE_READDIR_ON_OPEN", "EMPTY_DIR")
    os.environ.setdefault("AWS_NO_SIGN_REQUEST", "YES")
    if os.path.exists("/root/.ccr/ca-bundle.crt"):
        os.environ.setdefault("CURL_CA_BUNDLE", "/root/.ccr/ca-bundle.crt")

    layout = load_layout()
    corners = [local_to_lonlat(layout, sx * EXTENT_METRES * 1.02, sz * EXTENT_METRES * 1.02)
               for sx in (-1, 1) for sz in (-1, 1)]
    lon_min, lon_max = min(c[0] for c in corners), max(c[0] for c in corners)
    lat_min, lat_max = min(c[1] for c in corners), max(c[1] for c in corners)
    dem, (b_lon_min, b_lat_min, b_lon_max, b_lat_max) = read_mosaic(lon_min, lat_min, lon_max, lat_max)
    rows, cols = dem.shape
    print(f"DEM mosaic {cols}x{rows}, {dem.min():.0f}..{dem.max():.0f} m")

    # Each output cell: sample the source on a 5 x 5 sub-grid across the cell and keep a low
    # percentile, which sees past roofs and canopy to the ground.
    sub = 5
    offsets = (np.arange(sub) + 0.5) / sub - 0.5
    axis = -EXTENT_METRES + np.arange(COUNT) * SPACING
    samples = []
    for oz in offsets:
        for ox in offsets:
            gx, gz = np.meshgrid(axis + ox * SPACING, axis + oz * SPACING)
            lon, lat = local_to_lonlat(layout, gx, gz)
            c = np.clip(((lon - b_lon_min) / (b_lon_max - b_lon_min) * cols).astype(int), 0, cols - 1)
            r = np.clip(((b_lat_max - lat) / (b_lat_max - b_lat_min) * rows).astype(int), 0, rows - 1)
            samples.append(dem[r, c])
    stack = np.stack(samples)
    # Roofs and street trees only matter on the plain; in the Hills a low percentile shaves
    # the summits (Mount Lofty came out 50 m short), so above 80 m take the median.
    median = np.median(stack, axis=0)
    ground = np.where(median > 80.0, median, np.percentile(stack, GROUND_PERCENTILE, axis=0))
    ground = gaussian_filter(ground, 0.8)
    ground = np.clip(ground, 0.0, None)

    stored = np.clip(np.round(ground / HEIGHT_SCALE), 0, 32767).astype("<i2")
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    with open(OUTPUT, "wb") as f:
        f.write(b"ADEM")
        f.write(struct.pack("<iifff", 1, COUNT, SPACING, -EXTENT_METRES, HEIGHT_SCALE))
        f.write(stored.tobytes())

    def at(x, z):
        i = int(round((x + EXTENT_METRES) / SPACING))
        k = int(round((z + EXTENT_METRES) / SPACING))
        return float(ground[k, i])

    print(f"Wrote {OUTPUT} ({COUNT}x{COUNT} at {SPACING:.0f} m, {OUTPUT.stat().st_size} bytes)")
    print(f"  airfield centre {at(0, 0):.1f} m, max {ground.max():.0f} m")

    # Hillshade preview (north-up is not the frame: +x right is along 05->23, +z up).
    gy, gx = np.gradient(ground, SPACING)
    azimuth, altitude = math.radians(315), math.radians(35)
    slope = np.arctan(np.hypot(gx, gy) * 2.0)
    aspect = np.arctan2(-gx, gy)
    shade = np.sin(altitude) * np.cos(slope) + np.cos(altitude) * np.sin(slope) * np.cos(azimuth - aspect)
    shade = np.clip(shade, 0, 1)
    tint = np.clip(ground / 700.0, 0, 1)
    rgb = np.stack([0.35 + 0.5 * tint, 0.45 + 0.3 * tint, 0.3 + 0.2 * tint], axis=-1) * shade[..., None]
    rgb[ground <= 0.01] = (0.15, 0.3, 0.45)
    img = Image.fromarray((np.flipud(rgb) * 255).clip(0, 255).astype(np.uint8))
    PREVIEW.parent.mkdir(parents=True, exist_ok=True)
    img.save(PREVIEW, quality=88)
    print(f"  preview {PREVIEW}")


if __name__ == "__main__":
    main()
