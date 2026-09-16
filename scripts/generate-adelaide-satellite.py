#!/usr/bin/env python3
"""Bake ESA WorldCover Sentinel-2 imagery into Airside's runway-local frame.

The downloaded WMS image is geographic north-up. Airside uses an x/z frame aligned
to runway 05/23, so this script rotates and resamples the imagery into the same
24 km square used by CoastGrid. The output is presentation-only ground albedo.

Source: ESA WorldCover Sentinel-2 RGB median composite, 2021, 10 m.
Licence: CC BY 4.0.
"""
from __future__ import annotations

import argparse
import importlib.util
import math
from pathlib import Path
from urllib.request import urlretrieve

from PIL import Image, ImageEnhance

ROOT = Path(__file__).resolve().parents[1]
LAYOUT_PATH = ROOT / "scripts/generate-ypad-layout.py"
OUTPUT = ROOT / "game/Airside/Assets/Airside/Art/Textures/Environment/tx_adelaide_sentinel2_2021_v01.png"

EXTENT_METRES = 12_000.0
SIZE = 2048
LON_MIN, LAT_MIN = 138.3448, -35.1026
LON_MAX, LAT_MAX = 138.7156, -34.7966
WMS_URL = (
    "https://titiler.terrascope.be/wms?service=WMS&version=1.3.0&request=GetMap"
    "&layers=esa-worldcover-s2rgbnir-10m-2021-v2_tcc&styles="
    "&crs=EPSG%3A4326&bbox=-35.1026,138.3448,-34.7966,138.7156"
    "&width=2048&height=2048&format=image%2Fpng&transparent=false&time=2021-01-01"
)


def load_layout():
    spec = importlib.util.spec_from_file_location("ypad_layout", LAYOUT_PATH)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def affine_for_local_frame(layout, source_size: tuple[int, int]):
    """Pillow output-pixel -> WMS source-pixel affine coefficients."""
    width, height = source_size
    metres_per_pixel = 2.0 * EXTENT_METRES / SIZE
    lon_scale = 111_320.0 * math.cos(math.radians(layout.LAT0))
    lat_scale = 110_574.0

    # Output top-left is local (-extent, +extent). Image rows run towards -z.
    def source_pixel(px: float, py: float):
        x = -EXTENT_METRES + px * metres_per_pixel
        z = EXTENT_METRES - py * metres_per_pixel
        east = layout.MID[0] + x * layout.U[0] + z * layout.N[0]
        north = layout.MID[1] + x * layout.U[1] + z * layout.N[1]
        lon = layout.LON0 + east / lon_scale
        lat = layout.LAT0 + north / lat_scale
        sx = (lon - LON_MIN) / (LON_MAX - LON_MIN) * width
        sy = (LAT_MAX - lat) / (LAT_MAX - LAT_MIN) * height
        return sx, sy

    p00 = source_pixel(0.0, 0.0)
    p10 = source_pixel(1.0, 0.0)
    p01 = source_pixel(0.0, 1.0)
    return (
        p10[0] - p00[0], p01[0] - p00[0], p00[0],
        p10[1] - p00[1], p01[1] - p00[1], p00[1],
    )


def fill_wms_gaps(image: Image.Image) -> Image.Image:
    """Extend neighbouring Gulf pixels through pure-black WMS tile gaps."""
    pixels = image.load()
    for y in range(image.height):
        left = [-1] * image.width
        right = [-1] * image.width
        nearest = -1
        for x in range(image.width):
            r, g, b = pixels[x, y]
            if r > 2 or g > 2 or b > 2:
                nearest = x
            left[x] = nearest
        nearest = -1
        for x in range(image.width - 1, -1, -1):
            r, g, b = pixels[x, y]
            if r > 2 or g > 2 or b > 2:
                nearest = x
            right[x] = nearest
        for x in range(image.width):
            r, g, b = pixels[x, y]
            if r <= 2 and g <= 2 and b <= 2:
                candidates = [p for p in (left[x], right[x]) if p >= 0]
                if candidates:
                    source_x = min(candidates, key=lambda p: abs(p - x))
                    sr, sg, sb = pixels[source_x, y]
                else:
                    sr, sg, sb = (10, 29, 42)
                grain = ((x * 17 + y * 29) & 7) - 3
                pixels[x, y] = (max(4, sr + grain), max(10, sg + grain), max(16, sb + grain))
    return image


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path, help="Use an existing WMS PNG instead of downloading")
    args = parser.parse_args()

    source_path = args.source or ROOT / "work/cache/adelaide-esa-worldcover-2021.png"
    if not source_path.exists():
        source_path.parent.mkdir(parents=True, exist_ok=True)
        print("Downloading ESA WorldCover Adelaide composite...")
        urlretrieve(WMS_URL, source_path)

    source = fill_wms_gaps(Image.open(source_path).convert("RGB"))
    layout = load_layout()
    transformed = source.transform(
        (SIZE, SIZE), Image.Transform.AFFINE, affine_for_local_frame(layout, source.size),
        resample=Image.Resampling.BICUBIC,
    )
    # Preserve recognisable detail but sit behind the game's operational markings.
    transformed = ImageEnhance.Color(transformed).enhance(0.78)
    transformed = ImageEnhance.Contrast(transformed).enhance(0.88)
    transformed = ImageEnhance.Brightness(transformed).enhance(0.90)

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    transformed.save(OUTPUT, optimize=True)
    print(f"Wrote {OUTPUT} ({SIZE}x{SIZE}, runway-local +/-{EXTENT_METRES:.0f} m)")


if __name__ == "__main__":
    main()
