#!/usr/bin/env python3
"""Place the trees around Adelaide Airport where the satellite sees tree canopy (ADR 0160).

OpenStreetMap maps few individual trees here, but Sentinel-2's near-infrared band sees them all.
Over the same nine clear summer scenes as the ground imagery (ADR 0157) this takes the per-pixel
median NDVI and brightness. In an Adelaide summer, evergreen canopy (eucalypts, street trees)
has NDVI > 0.35 and is dark, irrigated turf (fairways, ovals) is as green but bright, and the
dry grass is neither. Each 10 m canopy pixel may hold a tree, jittered inside it, if it is clear
of buildings (AdelaideBuildings, the terminals and the suburbs of ADR 0159), road carriageways,
water, the airside and the landside precinct. Trees thin out with the suburbs, 1.1-1.8 km out.

Output: game/Airside/Assets/Airside/Art/Terrain/adelaide_trees_v01.bin, little-endian:
  char[4] "ATRE", int32 version (1), int32 count, then per tree:
    float x, float z (runway frame), float height, float crown radius, byte colour index
Run after generate-adelaide-satellite-s2.py (it reuses that scene list and band cache) and
generate-adelaide-suburbs.py: python3 scripts/generate-adelaide-trees.py
"""
from __future__ import annotations

import hashlib
import importlib.util
import json
import math
import os
import struct
from pathlib import Path

import numpy as np
from pyproj import Transformer
from shapely.geometry import LineString, Point, Polygon
from shapely.strtree import STRtree

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "game/Airside/Assets/Airside/Art/Terrain/adelaide_trees_v01.bin"
SCENES = ROOT / "docs/data/sentinel-2/adelaide-l2a-v02-scenes.json"
SUBURBS_BIN = ROOT / "game/Airside/Assets/Airside/Art/Terrain/osm_adelaide_suburbs_v01.bin"

NDVI_TREE = 0.35
DARK_REFLECTANCE = 0.055
TREE_CHANCE = 0.7            # canopy pixels are 10 m; not every one is its own crown
CELL = 10.0
COLOURS = 4                  # AirsideAdelaideSuburbs.TreeColours


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, ROOT / path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def stable(key, n):
    return int(hashlib.sha1(str(key).encode()).hexdigest()[:8], 16) % n


def canopy(s2, layout, to_utm):
    """Median NDVI and visible brightness over the near square (cached)."""
    cache = ROOT / "work/cache/sentinel-2/ndvi_near.npz"
    if cache.exists():
        data = np.load(cache)
        return data["ndvi"], data["lum"], tuple(data["bounds"])
    scenes = json.loads(SCENES.read_text())["scenes"]
    bounds = s2.utm_bounds(layout, to_utm, s2.EXTENT_METRES)
    ndvi, lum = [], []
    for scene in scenes:
        assets = s2.fetch_scene(scene["id"])["assets"]
        bands = {b: s2.read_band(assets[b]["href"], bounds, f"{scene['id']}_{b}.npy", 10.0).astype(np.float32)
                 for b in ("nir", "red", "green", "blue")}
        h = min(v.shape[0] for v in bands.values())
        w = min(v.shape[1] for v in bands.values())
        nir, red, green, blue = (bands[b][:h, :w] for b in ("nir", "red", "green", "blue"))
        ndvi.append((nir - red) / np.maximum(nir + red, 1.0))
        lum.append((red + green + blue) / 3.0 * s2.REFLECTANCE_SCALE)
    ndvi, lum = np.median(np.stack(ndvi), 0), np.median(np.stack(lum), 0)
    cache.parent.mkdir(parents=True, exist_ok=True)
    np.savez_compressed(cache, ndvi=ndvi, lum=lum, bounds=np.array(bounds))
    return ndvi, lum, bounds


def suburb_footprints():
    data = SUBURBS_BIN.read_bytes()
    count = struct.unpack("<i", data[8:12])[0]
    at, polys = 12, []
    for _ in range(count):
        kind = data[at]
        at += 11
        if kind in (1, 2):
            cx, cz, hl, hw, a = struct.unpack("<fffff", data[at:at + 20])
            at += 20
            ux, uz = math.cos(a), math.sin(a)
            polys.append(Polygon([(cx + sl * hl * ux - sw * hw * uz, cz + sl * hl * uz + sw * hw * ux)
                                  for sl, sw in ((1, 1), (-1, 1), (-1, -1), (1, -1))]))
        else:
            n = data[at]
            at += 1
            pts = [struct.unpack("<ff", data[at + 8 * k:at + 8 * k + 8]) for k in range(n)]
            at += 8 * n
            at += 1 + 3 * data[at]
            polys.append(Polygon(pts))
    return polys


def main():
    os.environ.setdefault("GDAL_DISABLE_READDIR_ON_OPEN", "EMPTY_DIR")
    os.environ.setdefault("AWS_NO_SIGN_REQUEST", "YES")
    if os.path.exists("/root/.ccr/ca-bundle.crt"):
        os.environ.setdefault("CURL_CA_BUNDLE", "/root/.ccr/ca-bundle.crt")
    s2 = module("s2", "scripts/generate-adelaide-satellite-s2.py")
    sub = module("suburbs", "scripts/generate-adelaide-suburbs.py")
    layout = s2.load_layout()
    to_utm = Transformer.from_crs("EPSG:4326", s2.UTM, always_xy=True)
    ndvi, lum, bounds = canopy(s2, layout, to_utm)

    keep_out = sub.airport_keep_out()
    aerodrome = json.loads(sub.AERODROME_RAW.read_text())["elements"][0]
    airside = Polygon([sub.local(layout, p["lon"], p["lat"]) for p in aerodrome["geometry"]]).buffer(-15.0)
    buildings = STRtree(suburb_footprints())
    streets = json.loads(sub.STREETS_SNAPSHOT.read_text())["streets"]
    roads, clearance = [], []
    for way in streets:
        kind = way["tags"].get("highway")
        if kind not in sub.CLEARANCE:
            continue
        roads.append(LineString([sub.local(layout, lon, lat) for lon, lat in way["geometry"]]))
        clearance.append(sub.CLEARANCE[kind] - 1.0)
    road_tree = STRtree(roads)
    landcover = sub.landcover_grid()

    reach = sub.AIRFIELD_HALF_X + sub.TAPER_END_METRES
    reach_z = sub.AIRFIELD_HALF_Z + sub.TAPER_END_METRES
    xs = np.arange(-reach, reach, CELL) + CELL / 2
    zs = np.arange(-reach_z, reach_z, CELL) + CELL / 2
    gx, gz = np.meshgrid(xs, zs)
    lon, lat = s2.local_to_lonlat(layout, gx, gz)
    east, north = to_utm.transform(lon, lat)
    col = np.clip(((east - bounds[0]) / 10.0).astype(int), 0, ndvi.shape[1] - 1)
    row = np.clip(((bounds[3] - north) / 10.0).astype(int), 0, ndvi.shape[0] - 1)
    tree_px = (ndvi[row, col] > NDVI_TREE) & (lum[row, col] < DARK_REFLECTANCE)

    out = bytearray()
    count = 0
    rejected = {"taper": 0, "chance": 0, "keep-out": 0, "building": 0, "road": 0, "water": 0}
    for zi, xi in zip(*np.nonzero(tree_px)):
        x0, z0 = float(gx[zi, xi]), float(gz[zi, xi])
        key = f"{xi}:{zi}"
        if not sub.keep_at(key, x0, z0):
            rejected["taper"] += 1
            continue
        if stable("c" + key, 1000) / 1000.0 >= TREE_CHANCE:
            rejected["chance"] += 1
            continue
        x = x0 + (stable("x" + key, 1000) / 1000.0 - 0.5) * CELL * 0.8
        z = z0 + (stable("z" + key, 1000) / 1000.0 - 0.5) * CELL * 0.8
        radius = 2.6 + 1.8 * stable("r" + key, 1000) / 1000.0
        height = 7.0 + 7.0 * stable("h" + key, 1000) / 1000.0
        crown = Point(x, z).buffer(radius * 0.6, resolution=4)
        if keep_out.intersects(crown) or airside.contains(Point(x, z)):
            rejected["keep-out"] += 1
            continue
        if landcover(x, z) == 5:          # AdelaideLandCover.Kind.Water
            rejected["water"] += 1
            continue
        if len(buildings.query(crown, predicate="intersects")) > 0:
            rejected["building"] += 1
            continue
        if any(Point(x, z).distance(roads[i]) < clearance[i]
               for i in road_tree.query(Point(x, z), predicate="dwithin", distance=15.0)):
            rejected["road"] += 1
            continue
        out += struct.pack("<ffffB", x, z, height, radius, stable("k" + key, COLOURS))
        count += 1

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_bytes(b"ATRE" + struct.pack("<ii", 1, count) + bytes(out))
    print(f"Wrote {OUTPUT}: {count} trees from {int(tree_px.sum())} canopy pixels "
          f"(rejected {rejected}), {OUTPUT.stat().st_size} bytes")


if __name__ == "__main__":
    main()
