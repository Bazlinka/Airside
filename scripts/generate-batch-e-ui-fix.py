#!/usr/bin/env python3
"""Regenerate Batch E UI-ICO-003 service icons and UI-PNL-002 dark panel.

Project-owned procedural assets matching the Batch E monochrome line style and
Runway Ink panel spec. Writes candidates, runtime slices, and prints SHA-256.
"""

from __future__ import annotations

import hashlib
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path("/workspace")
CANDIDATES = ROOT / "docs/art/candidates"
RUNTIME_ICONS = ROOT / "game/Airside/Assets/Airside/Art/UI/Icons"
RUNTIME_PANELS = ROOT / "game/Airside/Assets/Airside/Art/UI/Panels"

INK = (23, 36, 42, 255)  # Runway Ink #17242A
CLEAR = (0, 0, 0, 0)


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def new_sheet(width: int = 2172, height: int = 724) -> Image.Image:
    return Image.new("RGBA", (width, height), CLEAR)


def cell_box(index: int, cells: int = 7, width: int = 2172, height: int = 724, pad: int = 40):
    cell_w = width // cells
    x0 = index * cell_w + pad
    y0 = pad
    x1 = (index + 1) * cell_w - pad
    y1 = height - pad
    return x0, y0, x1, y1


def stroke(draw: ImageDraw.ImageDraw, xy, width: int = 6):
    draw.line(xy, fill=INK, width=width, joint="curve")


def draw_fuel(draw, box):
    x0, y0, x1, y1 = box
    cx, cy = (x0 + x1) // 2, (y0 + y1) // 2
    # truck cab + tank
    draw.rectangle([cx - 90, cy - 20, cx - 20, cy + 55], outline=INK, width=6)
    draw.rectangle([cx - 20, cy - 5, cx + 95, cy + 55], outline=INK, width=6)
    draw.ellipse([cx - 70, cy + 45, cx - 40, cy + 75], outline=INK, width=6)
    draw.ellipse([cx + 40, cy + 45, cx + 70, cy + 75], outline=INK, width=6)
    stroke(draw, [(cx + 95, cy + 10), (cx + 115, cy + 10), (cx + 115, cy + 35)])


def draw_baggage(draw, box):
    x0, y0, x1, y1 = box
    cx, cy = (x0 + x1) // 2, (y0 + y1) // 2
    draw.rectangle([cx - 100, cy - 10, cx - 40, cy + 40], outline=INK, width=6)
    draw.rectangle([cx - 30, cy, cx + 30, cy + 40], outline=INK, width=6)
    draw.rectangle([cx + 40, cy, cx + 100, cy + 40], outline=INK, width=6)
    stroke(draw, [(cx - 40, cy + 15), (cx - 30, cy + 15)])
    stroke(draw, [(cx + 30, cy + 15), (cx + 40, cy + 15)])
    for ox in (-85, -15, 55):
        draw.ellipse([cx + ox, cy + 35, cx + ox + 22, cy + 57], outline=INK, width=5)


def draw_passengers(draw, box):
    x0, y0, x1, y1 = box
    cx, cy = (x0 + x1) // 2, (y0 + y1) // 2
    # person
    draw.ellipse([cx - 18, cy - 70, cx + 18, cy - 34], outline=INK, width=6)
    stroke(draw, [(cx, cy - 34), (cx, cy + 20)])
    stroke(draw, [(cx - 35, cy - 5), (cx + 35, cy - 5)])
    stroke(draw, [(cx, cy + 20), (cx - 28, cy + 70)])
    stroke(draw, [(cx, cy + 20), (cx + 28, cy + 70)])
    # stairs hint
    stroke(draw, [(cx + 50, cy + 70), (cx + 50, cy - 20), (cx + 95, cy - 20)])


def draw_cleaning(draw, box):
    x0, y0, x1, y1 = box
    cx, cy = (x0 + x1) // 2, (y0 + y1) // 2
    draw.ellipse([cx - 55, cy - 55, cx + 55, cy + 55], outline=INK, width=6)
    stroke(draw, [(cx - 25, cy - 10), (cx + 25, cy + 25)])
    stroke(draw, [(cx + 25, cy - 10), (cx - 25, cy + 25)])
    stroke(draw, [(cx, cy - 70), (cx, cy - 55)])


def draw_catering(draw, box):
    x0, y0, x1, y1 = box
    cx, cy = (x0 + x1) // 2, (y0 + y1) // 2
    draw.rectangle([cx - 70, cy - 20, cx + 70, cy + 50], outline=INK, width=6)
    stroke(draw, [(cx - 70, cy), (cx + 70, cy)])
    stroke(draw, [(cx, cy - 20), (cx, cy + 50)])
    draw.ellipse([cx - 20, cy - 55, cx + 20, cy - 20], outline=INK, width=6)
    stroke(draw, [(cx, cy - 55), (cx, cy - 75)])


def draw_inspection(draw, box):
    x0, y0, x1, y1 = box
    cx, cy = (x0 + x1) // 2, (y0 + y1) // 2
    draw.ellipse([cx - 50, cy - 50, cx + 30, cy + 30], outline=INK, width=6)
    stroke(draw, [(cx + 20, cy + 20), (cx + 70, cy + 70)], width=8)
    stroke(draw, [(cx - 25, cy - 10), (cx - 5, cy + 10)])
    stroke(draw, [(cx - 5, cy - 10), (cx - 25, cy + 10)])


def draw_priority(draw, box):
    x0, y0, x1, y1 = box
    cx, cy = (x0 + x1) // 2, (y0 + y1) // 2
    # lightning / fast-track chevron
    stroke(draw, [(cx - 20, cy - 70), (cx + 35, cy - 10), (cx + 5, cy - 10), (cx + 40, cy + 70), (cx - 15, cy + 5), (cx + 10, cy + 5), (cx - 20, cy - 70)], width=7)


SERVICE_ICONS = [
    ("fuel", draw_fuel),
    ("baggage", draw_baggage),
    ("passengers", draw_passengers),
    ("cleaning", draw_cleaning),
    ("catering", draw_catering),
    ("inspection", draw_inspection),
    ("priority", draw_priority),
]


def slice_icons(sheet: Image.Image, names: list[str]) -> None:
    RUNTIME_ICONS.mkdir(parents=True, exist_ok=True)
    w, h = sheet.size
    cell_w = w // len(names)
    for i, name in enumerate(names):
        x0 = i * cell_w
        crop = sheet.crop((x0, 0, x0 + cell_w, h)).convert("RGBA")
        # trim transparent margins then pad to square
        bbox = crop.getbbox()
        if bbox is None:
            icon = Image.new("RGBA", (256, 256), CLEAR)
        else:
            trimmed = crop.crop(bbox)
            side = max(trimmed.size) + 32
            icon = Image.new("RGBA", (side, side), CLEAR)
            ox = (side - trimmed.size[0]) // 2
            oy = (side - trimmed.size[1]) // 2
            icon.paste(trimmed, (ox, oy), trimmed)
        out = RUNTIME_ICONS / f"ui_service_{name}_v01.png"
        icon.save(out, optimize=True)
        print(f"  sliced {out.name} {icon.size} {sha256(out)[:12]}")


def make_dark_panel() -> Image.Image:
    """128×128 Runway Ink panel at ~88% alpha with a subtle brighter edge."""
    img = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
    px = img.load()
    for y in range(128):
        for x in range(128):
            edge = min(x, y, 127 - x, 127 - y)
            if edge < 3:
                # slightly lighter edge for nine-slice readability
                px[x, y] = (40, 55, 62, 240)
            elif edge < 8:
                t = (edge - 3) / 5.0
                a = int(240 - t * 15)
                r = int(40 - t * 17)
                g = int(55 - t * 19)
                b = int(62 - t * 20)
                px[x, y] = (r, g, b, a)
            else:
                px[x, y] = (23, 36, 42, 224)  # ~88% of 255
    return img


def main() -> None:
    CANDIDATES.mkdir(parents=True, exist_ok=True)
    RUNTIME_PANELS.mkdir(parents=True, exist_ok=True)

    sheet = new_sheet()
    draw = ImageDraw.Draw(sheet)
    for i, (name, fn) in enumerate(SERVICE_ICONS):
        fn(draw, cell_box(i))
    sheet_path = CANDIDATES / "ui_service_icon_sheet_v01.png"
    sheet.save(sheet_path, optimize=True)
    print("service sheet", sheet_path, sheet.size, sha256(sheet_path))
    slice_icons(sheet, [n for n, _ in SERVICE_ICONS])

    panel = make_dark_panel()
    panel_cand = CANDIDATES / "ui_panel_9slice_dark_v01.png"
    panel_runtime = RUNTIME_PANELS / "ui_panel_9slice_dark_v01.png"
    panel.save(panel_cand, optimize=True)
    panel.save(panel_runtime, optimize=True)
    alphas = [panel.getpixel((x, y))[3] for y in range(128) for x in range(128)]
    print(
        "dark panel",
        panel_runtime,
        "alpha mean",
        sum(alphas) / len(alphas),
        "max",
        max(alphas),
        sha256(panel_runtime),
    )


if __name__ == "__main__":
    main()
