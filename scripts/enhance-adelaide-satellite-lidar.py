#!/usr/bin/env python3
"""Add LiDAR-derived roof and tree-crown detail to the Sentinel-2 ground texture (ADR 0237).

Sentinel-2 is 10 m, so a street of houses is one smear. The Government of South Australia's 2022 LiDAR products
are 0.5 m: the building footprint raster and the tree canopy height model. Averaged into each texture pixel they give
the true share of roof and of canopy, which this script turns into detail (an unsharp-mask style transfer:
high-pass of the share, pushing colour toward a roof grey or a leaf green). The Sentinel colour stays the base, so
the tone the ground shaders were tuned against holds; only block-scale structure sharpens.

Input:  tx_adelaide_sentinel2_l2a_v02.jpg (first run copies the untouched base to work/cache/sentinel-2/near_base.jpg;
        every run starts from that copy, so re-running never stacks detail).
        work/cache/sa-open/UrbanBuildings2022.tif and UrbanCanopyHeight2022.tif (+ .ovr) — CC BY 4.0, Government of
        South Australia, DEW; see scripts/generate-ypad-lidar-buildings.py for the download.
Output: the same JPEG, overwritten (GUID kept). Sea is skipped (the raster has false returns offshore).

Run: work/venv/bin/python scripts/enhance-adelaide-satellite-lidar.py [--strength 1.0] [--preview]
"""
from __future__ import annotations

import argparse
import importlib.util
import shutil
from pathlib import Path

import numpy as np
import rasterio
from PIL import Image
from pyproj import Transformer
from rasterio.windows import Window
from scipy import ndimage

ROOT = Path(__file__).resolve().parents[1]
TEXTURE = ROOT / "game/Airside/Assets/Airside/Art/Textures/Environment/tx_adelaide_sentinel2_l2a_v02.jpg"
BASE = ROOT / "work/cache/sentinel-2/near_base.jpg"
EXTENT, SIZE = 12_000.0, 4096
ROOF = np.array([0.40, 0.37, 0.35], np.float32)      # weathered tile / colorbond seen from above
LEAF = np.array([0.15, 0.25, 0.11], np.float32)      # dark evergreen canopy
COARSE = 4                                            # decimation of the 0.5 m rasters: 2 m cells


def layout():
    spec = importlib.util.spec_from_file_location("l", ROOT / "scripts/generate-ypad-layout.py")
    m = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(m)
    return m


def share(path, mga, canopy):
    """Fraction of 0.5 m cells that are building (or canopy) per texture pixel, sampled from a 2 m average."""
    ex, ey = mga
    with rasterio.open(path) as src:
        c0, r0 = ~src.transform * (ex.min() - 20, ey.max() + 20)
        c1, r1 = ~src.transform * (ex.max() + 20, ey.min() - 20)
        c0, r0 = int(max(c0, 0)), int(max(r0, 0))
        c1, r1 = int(min(c1, src.width)), int(min(r1, src.height))
        if c1 <= c0 or r1 <= r0:
            return np.zeros(ex.shape, np.float32), np.zeros(ex.shape, bool)
        shape = ((r1 - r0) // COARSE, (c1 - c0) // COARSE)
        data = src.read(1, window=Window(c0, r0, c1 - c0, r1 - r0), out_shape=shape,
                        resampling=rasterio.enums.Resampling.average).astype(np.float32)
        nodata = src.nodata
        t = src.transform
    if canopy:
        data = np.where(data > 0, 1.0, 0.0) if nodata is None else ((data > 0) & (data < 60000)).astype(np.float32)
    else:
        data = np.clip(data, 0, 1)
    cols = ((ex - (t.c + c0 * t.a)) / (t.a * COARSE)) - 0.5
    rows = ((ey - (t.f + r0 * t.e)) / (t.e * COARSE)) - 0.5
    inside = (cols >= 0) & (rows >= 0) & (cols < shape[1] - 1) & (rows < shape[0] - 1)
    out = ndimage.map_coordinates(data, [rows, cols], order=1, mode="nearest").astype(np.float32)
    return out, inside


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--strength", type=float, default=1.0)
    ap.add_argument("--preview", action="store_true", help="also write a before/after crop beside the texture in work/")
    args = ap.parse_args()
    L = layout()
    if not BASE.exists():
        BASE.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(TEXTURE, BASE)
    base = np.asarray(Image.open(BASE).convert("RGB"), np.float32) / 255.0

    mpp = 2 * EXTENT / SIZE
    px = (np.arange(SIZE) + 0.5) * mpp - EXTENT
    gx, gz = np.meshgrid(px, EXTENT - (np.arange(SIZE) + 0.5) * mpp)
    import math
    ls = 111_320.0 * math.cos(math.radians(L.LAT0))
    east = L.MID[0] + gx * L.U[0] + gz * L.N[0]
    north = L.MID[1] + gx * L.U[1] + gz * L.N[1]
    ex, ey = Transformer.from_crs(4326, 7854, always_xy=True).transform(L.LON0 + east / ls, L.LAT0 + north / 110_574.0)
    ex, ey = np.asarray(ex), np.asarray(ey)

    b, b_in = share(ROOT / "work/cache/sa-open/UrbanBuildings2022.tif", (ex, ey), False)
    t, t_in = share(ROOT / "work/cache/sa-open/UrbanCanopyHeight2022.tif", (ex, ey), True)
    print(f"building share mean {b.mean():.3f}, canopy share mean {t.mean():.3f}, covered {b_in.mean():.2f}")

    water = (base[..., 2] > base[..., 0] + 0.03) & (base[..., 2] > base[..., 1])
    water = ndimage.binary_dilation(water, iterations=3)
    sigma = 10.0 / mpp * 1.6                               # the scale Sentinel already resolves (10 m)
    out = base.copy()
    for share_map, inside, target, gain in ((b, b_in, ROOF, 0.55), (t, t_in, LEAF, 0.6)):
        hf = share_map - ndimage.gaussian_filter(share_map, sigma)
        hf = np.where(inside & ~water, hf, 0.0)[..., None] * args.strength * gain
        out += hf * (target - out)
    out = np.clip(out, 0.0, 1.0)
    Image.fromarray((out * 255 + 0.5).astype(np.uint8), "RGB").save(TEXTURE, quality=90, subsampling=0, optimize=True)
    print(f"wrote {TEXTURE} ({TEXTURE.stat().st_size / 1e6:.1f} MB); mean abs change {np.abs(out - base).mean():.4f}")
    if args.preview:
        cx, cz = 2048 + int(-2600 / mpp), 2048 + int(1200 / mpp)
        sl = (slice(cz - 160, cz + 160), slice(cx - 240, cx + 240))
        both = np.concatenate([base[sl], out[sl]], axis=1)
        Image.fromarray((both * 255).astype(np.uint8)).resize((both.shape[1] * 2, both.shape[0] * 2), Image.NEAREST).save(
            ROOT / "work/lidar-texture-preview.png")
        print("preview work/lidar-texture-preview.png")


if __name__ == "__main__":
    main()
