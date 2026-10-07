#!/usr/bin/env python3
"""Bake a clean, native-resolution Sentinel-2 image of Adelaide into Airside's runway frame.

Replaces the 2021 ESA WorldCover WMS composite (tx_adelaide_sentinel2_2021_v01.png), which
had a smeared tile gap across the Gulf, a flat hazy tone and ~12 m output pixels.

Source: Copernicus Sentinel-2 Level-2A surface reflectance, served as cloud-optimised
GeoTIFFs by Element 84 Earth Search on the AWS Registry of Open Data
(https://registry.opendata.aws/sentinel-2-l2a-cogs/). The Copernicus Sentinel data terms
allow free use, modification and redistribution with attribution:
"Contains modified Copernicus Sentinel data <years>".

Method:
  1. Search MGRS tile 54HTG for summer scenes with < 1 % cloud and full tile coverage.
  2. Read the 10 m red/green/blue bands and the 20 m scene classification for the area,
     drop cloud, cloud shadow, cirrus, saturated and no-data pixels.
  3. Per-pixel median across the scenes: removes the odd cloud, cars, boats and glint,
     and gives one even summer light.
  4. Reflectance -> display colour, then map each channel's distribution onto the previous
     texture's so the tone the ground/surroundings shaders were tuned against holds.
  5. Resample once (bicubic) into the runway-local square used by CoastGrid and write a
     4096 px JPEG (5.9 m per pixel over the same +/-12 km; the source is 10 m).

Run: python3 scripts/generate-adelaide-satellite-s2.py [--scenes ID ID ...]
Needs numpy, scipy, pillow, rasterio, pyproj (pip install numpy scipy pillow rasterio pyproj).
"""
from __future__ import annotations

import argparse
import importlib.util
import json
import math
import os
import sys
import urllib.request
from pathlib import Path

import numpy as np
import rasterio
from PIL import Image
from scipy.ndimage import gaussian_filter
from pyproj import Transformer
from rasterio.enums import Resampling
from rasterio.windows import from_bounds

ROOT = Path(__file__).resolve().parents[1]
LAYOUT_PATH = ROOT / "scripts/generate-ypad-layout.py"
ENV = ROOT / "game/Airside/Assets/Airside/Art/Textures/Environment"
OUTPUT = ENV / "tx_adelaide_sentinel2_l2a_v02.jpg"
FAR_OUTPUT = ENV / "tx_adelaide_sentinel2_l2a_far_v02.jpg"
# v01, kept out of the build as the tone reference the shaders were tuned against.
PREVIOUS = ROOT / "docs/data/esa-worldcover/tx_adelaide_sentinel2_2021_v01.png"
MANIFEST = ROOT / "docs/data/sentinel-2/adelaide-l2a-v02-scenes.json"
CACHE = ROOT / "work/cache/sentinel-2"

EXTENT_METRES = 12_000.0      # half size, same square as CoastGrid and the v01 texture
SIZE = 4096
FAR_EXTENT_METRES = 30_500.0  # the far terrain ring is a 30 km disc (the camera far clip)
FAR_SIZE = 4096               # v02 (ADR 0248): ~15 m/px; v01 was 2048 px from a 40 m source
FAR_SOURCE_METRES = 20.0
TILE = "54HTG"
UTM = "EPSG:32754"
STAC = "https://earth-search.aws.element84.com/v1/search"
MAX_SCENES = 9
SUMMER_MONTHS = {12, 1, 2, 3}

# Sentinel-2 scene classification: keep vegetation, bare, water, dark area, unclassified.
BAD_SCL = {0, 1, 3, 8, 9, 10}
# Earth Search's COGs are already harmonised: the +1000 DN offset of processing baseline
# 04.00 is removed even though the item metadata still lists offset -0.1 (open Gulf reads
# DN ~70 in red, which is only plausible as 0.007 reflectance). Subtracting it again crushed
# green and blue and turned dry grass red.
REFLECTANCE_SCALE, REFLECTANCE_OFFSET = 0.0001, 0.0
GAIN = 3.0
WATER_SCL = 6
LUMA = np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)
# A little more saturation and contrast than v01, which was deliberately flattened and hazy.
SATURATION = 1.12
WATER_SATURATION = 0.8
# The sand shallows are far brighter than the open Gulf; a quarter under v01's near-shore
# mean keeps them from glaring next to the stylised water they dissolve into.
WATER_EXPOSURE = 0.75
CONTRAST = 1.05


def load_layout():
    spec = importlib.util.spec_from_file_location("ypad_layout", LAYOUT_PATH)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def local_to_lonlat(layout, x, z):
    """The runway frame -> lon/lat, exactly as generate-adelaide-satellite.py (v01) did."""
    lon_scale = 111_320.0 * math.cos(math.radians(layout.LAT0))
    lat_scale = 110_574.0
    east = layout.MID[0] + x * layout.U[0] + z * layout.N[0]
    north = layout.MID[1] + x * layout.U[1] + z * layout.N[1]
    return layout.LON0 + east / lon_scale, layout.LAT0 + north / lat_scale


def search_scenes():
    body = {
        "collections": ["sentinel-2-l2a"],
        "intersects": {"type": "Point", "coordinates": [138.53, -34.945]},
        "datetime": "2023-11-01T00:00:00Z/2026-04-30T00:00:00Z",
        "query": {"eo:cloud_cover": {"lt": 1}},
        "limit": 200,
    }
    request = urllib.request.Request(STAC, data=json.dumps(body).encode(),
                                     headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(request, timeout=120) as response:
        features = json.load(response)["features"]
    picked = []
    for f in features:
        p = f["properties"]
        month = int(p["datetime"][5:7])
        if not f["id"].startswith(("S2A_" + TILE, "S2B_" + TILE, "S2C_" + TILE)):
            continue
        if month not in SUMMER_MONTHS or p.get("s2:nodata_pixel_percentage", 100) > 1.0:
            continue
        picked.append(f)
    picked.sort(key=lambda f: f["properties"]["eo:cloud_cover"])
    return picked[:MAX_SCENES]


def fetch_scene(scene_id):
    request = urllib.request.Request(
        f"https://earth-search.aws.element84.com/v1/collections/sentinel-2-l2a/items/{scene_id}")
    with urllib.request.urlopen(request, timeout=120) as response:
        return json.load(response)


def utm_bounds(layout, to_utm, extent, margin=400.0):
    xs, ys = [], []
    for x in (-extent, extent):
        for z in (-extent, extent):
            lon, lat = local_to_lonlat(layout, x, z)
            e, n = to_utm.transform(lon, lat)
            xs.append(e)
            ys.append(n)
    # Snap to an 80 m grid so the 10/20/40 m windows line up exactly.
    left = math.floor((min(xs) - margin) / 80) * 80
    bottom = math.floor((min(ys) - margin) / 80) * 80
    right = math.ceil((max(xs) + margin) / 80) * 80
    top = math.ceil((max(ys) + margin) / 80) * 80
    return left, bottom, right, top


def read_band(href, bounds, cache_name, pixel_metres, nearest=False):
    """A band over bounds at pixel_metres (10 = native; coarser reads the COG overviews)."""
    cached = CACHE / cache_name
    if cached.exists():
        return np.load(cached)
    width = int(round((bounds[2] - bounds[0]) / pixel_metres))
    height = int(round((bounds[3] - bounds[1]) / pixel_metres))
    with rasterio.open(href) as src:
        window = from_bounds(*bounds, transform=src.transform)
        data = src.read(1, window=window, boundless=True, fill_value=0, out_shape=(height, width),
                        resampling=Resampling.nearest if nearest else Resampling.average)
    CACHE.mkdir(parents=True, exist_ok=True)
    np.save(cached, data)
    return data


def bake(layout, to_utm, scenes, extent, size, pixel_metres):
    """Cloud-masked median of the scenes, rotated into the runway square: (rgb, water)."""
    bounds = utm_bounds(layout, to_utm, extent)
    width = int(round((bounds[2] - bounds[0]) / pixel_metres))
    height = int(round((bounds[3] - bounds[1]) / pixel_metres))
    print(f"UTM window {bounds} -> {width}x{height} px at {pixel_metres:.0f} m")
    tag = "" if pixel_metres == 10 else f"_{pixel_metres:.0f}m_{bounds[0]}_{bounds[3]}"
    stack = np.full((len(scenes), 3, height, width), np.nan, dtype=np.float32)
    water = np.zeros((height, width), dtype=np.float32)
    for i, scene in enumerate(scenes):
        assets = scene["assets"]
        scl = read_band(assets["scl"]["href"], bounds, f"{scene['id']}_SCL{tag}.npy", pixel_metres, nearest=True)
        scl = scl[:height, :width]
        bad = np.isin(scl, list(BAD_SCL))
        water += (scl == WATER_SCL).astype(np.float32) / len(scenes)
        for c, band in enumerate(("red", "green", "blue")):
            dn = read_band(assets[band]["href"], bounds, f"{scene['id']}_{band}{tag}.npy", pixel_metres)
            dn = dn[:height, :width]
            refl = dn.astype(np.float32) * REFLECTANCE_SCALE + REFLECTANCE_OFFSET
            refl[bad | (dn == 0)] = np.nan
            stack[i, c] = refl
        print(f"  {scene['id']}: {100.0 * bad.mean():.2f}% masked")

    median = np.empty((3, height, width), dtype=np.float32)
    for row in range(0, height, 256):
        median[:, row:row + 256] = np.nanmedian(stack[:, :, row:row + 256], axis=0)
    del stack
    # A pixel no clear scene saw (should be none): fill with the overall median.
    missing = np.isnan(median)
    if missing.any():
        median[missing] = np.nanmedian(median)
        print(f"  filled {missing.sum()} unobserved samples")
    display = np.concatenate([np.clip(median * GAIN, 0.0, 1.0), water[None]], axis=0)

    # Resample into the runway-local square. Local -> UTM is affine to well under a pixel
    # across the square (the v01 bake made the same approximation), so one affine transform.
    metres_per_pixel = 2.0 * extent / size

    def utm_pixel(px, py):
        x = -extent + px * metres_per_pixel
        z = extent - py * metres_per_pixel
        lon, lat = local_to_lonlat(layout, x, z)
        e, n = to_utm.transform(lon, lat)
        return (e - bounds[0]) / pixel_metres, (bounds[3] - n) / pixel_metres

    p00, p10, p01 = utm_pixel(0, 0), utm_pixel(1, 0), utm_pixel(0, 1)
    affine = (p10[0] - p00[0], p01[0] - p00[0], p00[0], p10[1] - p00[1], p01[1] - p00[1], p00[1])
    channels = []
    for c in range(4):
        band = Image.fromarray(display[c].astype(np.float32), mode="F")
        channels.append(np.asarray(band.transform((size, size), Image.Transform.AFFINE, affine,
                                                  resample=Image.Resampling.BICUBIC)))
    return np.clip(np.stack(channels[:3], axis=-1), 0.0, 1.0), channels[3]


def saturate(img, factor):
    grey = (img @ LUMA)[..., None]
    return grey + (img - grey) * factor


def mean_saturation(img, mask):
    grey = (img @ LUMA)[..., None]
    return float(np.abs(img - grey)[mask].mean())


def fit_tone(rgb, water, previous):
    """Tone fitted on the near square against v01; returned as parameters so the far ring
    image gets exactly the same mapping and the two meet without a colour step.

    Keep the look the ground/surroundings shaders were tuned against without bending hues: one
    brightness curve for land and a plain exposure scale for water, applied equally to all three
    channels; saturation set to v01's plus a little. v01's own Gulf held the smeared tile gap, so
    its water reference is only the near-shore band that the game actually shows.
    """
    is_water = water > 0.5
    land = ~is_water
    luma = rgb @ LUMA
    previous_luma = previous @ LUMA
    reach = int(500 / (2 * EXTENT_METRES / SIZE))
    grown = land.copy()
    for _ in range(reach):
        grown[1:, :] |= grown[:-1, :]; grown[:-1, :] |= grown[1:, :]
        grown[:, 1:] |= grown[:, :-1]; grown[:, :-1] |= grown[:, 1:]
    near_shore = grown & is_water
    quantiles = np.linspace(0.0, 1.0, 513)
    src_q = np.quantile(luma[land][::7], quantiles)
    ref_q = np.quantile(previous_luma[land][::7], quantiles)
    src_q = np.maximum.accumulate(src_q + np.arange(src_q.size) * 1e-9)
    curved = rgb * (np.interp(luma, src_q, ref_q) / np.maximum(luma, 1e-4))[..., None]
    target = mean_saturation(previous, land) * SATURATION
    saturation = target / max(mean_saturation(curved, land), 1e-4)
    mean = float((saturate(curved, saturation) @ LUMA)[land].mean())
    scale = WATER_EXPOSURE * float(previous_luma[near_shore].mean()) / max(float(luma[is_water].mean()), 1e-4)
    print(f"  tone fitted to v01 (land saturation x{saturation:.2f}, water x{scale:.2f})")
    return {"src_q": src_q, "ref_q": ref_q, "saturation": saturation, "mean": mean, "water_scale": scale}


def apply_tone(rgb, water, tone, blur_px):
    luma = rgb @ LUMA
    land_rgb = rgb * (np.interp(luma, tone["src_q"], tone["ref_q"]) / np.maximum(luma, 1e-4))[..., None]
    land_rgb = saturate(land_rgb, tone["saturation"])
    land_rgb = tone["mean"] + (land_rgb - tone["mean"]) * CONTRAST
    # Water: a plain exposure scale so the shallows keep their real variation.
    water_rgb = saturate(rgb * tone["water_scale"], WATER_SATURATION)
    # Blend on a softened water fraction, so the beach and river banks have no seam.
    weight = gaussian_filter((water > 0.5).astype(np.float32), blur_px)[..., None]
    return land_rgb * (1.0 - weight) + water_rgb * weight


def save(rgb, path, quality):
    out = Image.fromarray(np.clip(rgb * 255.0 + 0.5, 0, 255).astype(np.uint8), mode="RGB")
    path.parent.mkdir(parents=True, exist_ok=True)
    out.save(path, quality=quality, subsampling=0, optimize=True)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--scenes", nargs="*", help="Scene ids to use instead of searching")
    parser.add_argument("--far-only", action="store_true",
                        help="Write only the far ring image; the near texture and manifest are left untouched "
                             "(the near square is still baked, to fit the same tone)")
    args = parser.parse_args()

    os.environ.setdefault("GDAL_DISABLE_READDIR_ON_OPEN", "EMPTY_DIR")
    os.environ.setdefault("AWS_NO_SIGN_REQUEST", "YES")
    if os.path.exists("/root/.ccr/ca-bundle.crt"):
        os.environ.setdefault("CURL_CA_BUNDLE", "/root/.ccr/ca-bundle.crt")

    layout = load_layout()
    to_utm = Transformer.from_crs("EPSG:4326", UTM, always_xy=True)
    scenes = [fetch_scene(s) for s in args.scenes] if args.scenes else search_scenes()
    if not scenes:
        sys.exit("No scenes found")
    print("Scenes:", ", ".join(s["id"] for s in scenes))

    near, near_water = bake(layout, to_utm, scenes, EXTENT_METRES, SIZE, 10.0)
    previous = np.asarray(Image.open(PREVIOUS).convert("RGB").resize((SIZE, SIZE), Image.Resampling.BILINEAR),
                          dtype=np.float32) / 255.0
    tone = fit_tone(near, near_water, previous)
    if not args.far_only:
        save(apply_tone(near, near_water, tone, 2.0), OUTPUT, 90)
        print(f"Wrote {OUTPUT} ({SIZE}x{SIZE}, runway-local +/-{EXTENT_METRES:.0f} m)")
    del near, near_water

    # The far ring (plan P6): the same scenes and tone, 20 m source over the 30 km disc.
    far, far_water = bake(layout, to_utm, scenes, FAR_EXTENT_METRES, FAR_SIZE, FAR_SOURCE_METRES)
    save(apply_tone(far, far_water, tone, 1.0), FAR_OUTPUT, 88)
    print(f"Wrote {FAR_OUTPUT} ({FAR_SIZE}x{FAR_SIZE}, runway-local +/-{FAR_EXTENT_METRES:.0f} m)")

    if args.far_only:
        return

    years = sorted({s["properties"]["datetime"][:4] for s in scenes})
    MANIFEST.parent.mkdir(parents=True, exist_ok=True)
    MANIFEST.write_text(json.dumps({
        "outputs": [
            {"path": str(OUTPUT.relative_to(ROOT)), "size": SIZE, "half_extent_metres": EXTENT_METRES,
             "source_metres": 10},
            {"path": str(FAR_OUTPUT.relative_to(ROOT)), "size": FAR_SIZE,
             "half_extent_metres": FAR_EXTENT_METRES, "source_metres": int(FAR_SOURCE_METRES)},
        ],
        "tile": TILE,
        "gain": GAIN,
        "contrast": CONTRAST,
        "saturation": SATURATION,
        "scenes": [{"id": s["id"], "datetime": s["properties"]["datetime"],
                    "cloud_cover": s["properties"]["eo:cloud_cover"]} for s in scenes],
        "attribution": f"Contains modified Copernicus Sentinel data {years[0]}-{years[-1]}",
    }, indent=2) + "\n")


if __name__ == "__main__":
    main()
