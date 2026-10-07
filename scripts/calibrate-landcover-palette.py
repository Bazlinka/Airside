#!/usr/bin/env python3
"""Measure the far Sentinel-2 image's mean colour per WorldCover class (ADR 0190 palette).

The far terrain fades its satellite drape into land-cover vertex colours before its 30 km
edge, so the land-cover palette must match what the drape looks like in game: linear
albedo times the material's satellite tint at the far strength, over the old plain vertex
colour. Prints a C# table for AdelaideFarLandCover.Base.

  python3 scripts/calibrate-landcover-palette.py [--inner 15000] [--outer 30000]
"""
import argparse
import struct
import zlib
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / "game/Airside/Assets/Airside/Art"
SATELLITE = ART / "Textures/Environment/tx_adelaide_sentinel2_l2a_far_v02.jpg"
LANDCOVER = ART / "Terrain/landcover_adelaide_far_v01.bin"
DEM = ART / "Terrain/dem_adelaide_runway_far_v01.bin"
EXTENT = 30500.0                      # AirsideAdelaideFarTerrain.SatelliteExtentMetres
TINT = np.array([0.56, 0.58, 0.56])   # AirsideAdelaideSurroundings.BuildMaterial _SatelliteTint
STRENGTH = 0.92                       # AirsideAdelaideSurroundings.SatelliteFarStrength
PLAIN_SRGB = np.array([0.555, 0.57, 0.42])  # AirsideAdelaideSurroundings.Plain, the far ring's old vertex colour
NAMES = ["water", "tree", "shrub", "grass", "crop", "built", "bare", "wetland"]


def load_landcover():
    data = LANDCOVER.read_bytes()
    assert data[:4] == b"ALCV"
    _, count, spacing, origin = struct.unpack("<iiff", data[4:20])
    cells = np.frombuffer(zlib.decompress(data[20:], -15), dtype=np.uint8).reshape(count, count)
    return cells, spacing, origin


def srgb_to_linear(c):
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--inner", type=float, default=15000.0)
    parser.add_argument("--outer", type=float, default=30000.0)
    args = parser.parse_args()

    image = np.asarray(Image.open(SATELLITE).convert("RGB"), dtype=np.float64) / 255.0
    h, w, _ = image.shape
    # The project renders in Gamma colour space: the shader uses texel values as stored, while the
    # far ring's plain vertex colour is Color.linear of its sRGB value.
    linear = image * TINT
    cells, spacing, origin = load_landcover()
    count = cells.shape[0]

    # Unity samples uv = xz / (2 * extent) + 0.5 with v = 0 on the image's bottom row.
    cols = (np.arange(w) + 0.5) / w * 2 * EXTENT - EXTENT
    rows = EXTENT - (np.arange(h) + 0.5) / h * 2 * EXTENT
    x, z = np.meshgrid(cols, rows)
    r = np.hypot(x, z)
    xi = np.clip(np.rint((x - origin) / spacing).astype(int), 0, count - 1)
    zi = np.clip(np.rint((z - origin) / spacing).astype(int), 0, count - 1)
    cls = cells[zi, xi]
    band = (r >= args.inner) & (r <= args.outer)

    print(f"// Far drape as rendered, {args.inner / 1000:.0f}-{args.outer / 1000:.0f} km, per class (shader rgb)")
    for c, name in enumerate(NAMES):
        mask = band & (cls == c)
        n = int(mask.sum())
        if n < 200:
            print(f"// {name}: too few pixels ({n})")
            continue
        mean = STRENGTH * linear[mask].mean(axis=0) + (1 - STRENGTH) * srgb_to_linear(PLAIN_SRGB)
        print(f"new[] {{ {mean[0]:.3f}f, {mean[1]:.3f}f, {mean[2]:.3f}f }},   // {name} ({n} px)")


if __name__ == "__main__":
    main()
