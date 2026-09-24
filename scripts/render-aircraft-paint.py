#!/usr/bin/env python3
"""Render each aircraft with its fitted livery, fuselage title and registration (ADR 0112).

Titles are runtime TextMesh paint, so this reproduces what AddAircraftIdentityText does —
the AircraftIdentityMarkings layout (read straight from AircraftTitlePaint.cs), the same
yaw-then-lean rotation and nose/aft anchoring — and z-buffers the glyphs with the real
mesh. A title that stood off the skin or poked through a wing root shows up here.

  python3 scripts/render-aircraft-paint.py OUT_DIR [TYPE ...]
"""
import importlib.util
import math
import os
import re
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageOps

HERE = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("thumbs", os.path.join(HERE, "render-aircraft-thumbnails.py"))
thumbs = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(thumbs)
_spec2 = importlib.util.spec_from_file_location("layoutgen", os.path.join(HERE, "generate-aircraft-title-layout.py"))
layoutgen = importlib.util.module_from_spec(_spec2)
_spec2.loader.exec_module(layoutgen)

FONT = next(p for p in ("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
                        "/System/Library/Fonts/Supplemental/Arial Bold.ttf") if os.path.exists(p))
LINE_PER_C = 6.4            # TextMesh line box metres per character size at font size 64
CAP_OF_LINE = 0.716
ADVANCE = 0.62              # AircraftTitlePaint.AverageAdvanceFraction

# (airline title, accent hex, registration) per type — representative Adelaide operators.
OPERATORS = {
    "ATR42": ("SOUTHERN CROSS", "#39708A", "VH-PAX"), "SF34": ("REX", "#D2491E", "VH-ZRC"),
    "DH8D": ("QANTASLINK", "#D8141E", "VH-QOK"), "E190": ("AIRSIDE", "#1F3A93", "VH-PEA"),
    "A223": ("AIRSIDE", "#1F3A93", "VH-PAB"), "A320": ("JETSTAR", "#F26623", "VH-VFH"),
    "B738": ("QANTAS", "#E4002B", "VH-VZX"), "B38M": ("VIRGIN", "#D71920", "VH-8IA"),
    "A21N": ("AIR NZ", "#111111", "ZK-NNA"), "A359": ("EMIRATES", "#D71921", "A6-EVA"),
    "A339": ("MALAYSIA", "#ED1B2F", "9M-MAB"), "B789": ("QANTAS", "#E4002B", "VH-ZNA"),
    "B78X": ("SINGAPORE", "#1B3F8B", "9V-SCA"),
}
INK = (41, 46, 51)
VIEWS = {"side": (90.0, 0.0), "front_high": (38.0, 22.0), "rear_low": (140.0, -8.0),
         "rear_high": (150.0, 32.0)}


def hex_rgb(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def reads_on_white(rgb):
    lum = (0.2126 * rgb[0] + 0.7152 * rgb[1] + 0.0722 * rgb[2]) / 255.0
    return 0.14 <= lum <= 0.72


def cs_layouts():
    text = open(layoutgen.CS_FILE).read()
    out = {}
    for member, args in re.findall(r"AircraftType\.(\w+)\)\)\s*return new AircraftIdentityMarkingLayout\(([^;]+)\);", text):
        out[member] = [float(a.strip().rstrip("f")) for a in args.split(",")]
    return out


def glyph_quads(text, width, cap, bold=True):
    """Text as unit-cell quads in [0,1]x[-0.5,0.5], from a PIL raster of the word."""
    font = ImageFont.truetype(FONT, 64)
    box = font.getbbox(text)
    img = Image.new("L", (box[2] - box[0] + 4, 64), 0)
    ImageDraw.Draw(img).text((2 - box[0], 32), text, font=font, fill=255, anchor="lm")
    ink = np.asarray(img) > 128
    rows = np.where(ink.any(axis=1))[0]
    ink = ink[rows.min():rows.max() + 1]
    h, w = ink.shape
    tris = []
    for y, x in zip(*np.nonzero(ink)):
        x0, x1 = x / w * width, (x + 1) / w * width
        y0, y1 = cap / 2 - y / h * cap, cap / 2 - (y + 1) / h * cap
        a, b, c, d = (x0, y0), (x1, y0), (x1, y1), (x0, y1)
        tris += [(a, b, c), (a, c, d)]
    return np.asarray(tris, float)


def label_tris(text, c, pos, side, tilt, anchor_at_nose, offset):
    line = LINE_PER_C * c
    cap = line * CAP_OF_LINE
    width = len(text) * line * ADVANCE
    q = glyph_quads(text, width, cap)
    nose_is_left = side < 0
    left_anchor = anchor_at_nose == nose_is_left
    x = q[..., 0] if left_anchor else q[..., 0] - width
    local = np.stack([x, q[..., 1], np.zeros_like(x)], axis=-1)
    yaw = math.radians(90.0 if side < 0 else -90.0)
    ry = np.array([[math.cos(yaw), 0, math.sin(yaw)], [0, 1, 0], [-math.sin(yaw), 0, math.cos(yaw)]])
    roll = math.radians(side * tilt)
    rz = np.array([[math.cos(roll), -math.sin(roll), 0], [math.sin(roll), math.cos(roll), 0], [0, 0, 1]])
    world = local @ (rz @ ry).T + np.array(pos)
    world[..., 1] -= offset      # art root -> model file frame
    return world


def paint_colour(accent):
    def colour(name):
        if name.startswith(("livery_", "tail_fin", "rudder", "winglet", "dorsal")):
            return accent
        if name.startswith(("engine_", "nacelle_", "pylon_", "intake_", "cowl_", "oil_cooler")):
            return (214, 218, 222)
        return thumbs.colour(name)
    return colour


def render(cid, out_dir):
    member, offset = layoutgen.TYPES[cid]
    l = cs_layouts()[member]
    side_x, oy, oz, rx, ry, rz, oc, rc, otilt, rtilt, budget = l
    title, accent_hex, reg = OPERATORS[cid]
    accent = hex_rgb(accent_hex)
    ink = accent if reads_on_white(accent) else INK
    fitted = min(oc, budget / max(1e-6, len(title) * LINE_PER_C * ADVANCE)) if len(title) * LINE_PER_C * oc * ADVANCE > budget else oc
    path = [os.path.join(thumbs.ART, m) for c, m, _ in thumbs.MODELS if c == cid][0]
    parts = [(n, t) for n, t in thumbs.load_parts(path)]
    for side in (-1, 1):
        parts.append((f"title{side}", label_tris(title, fitted, (side * side_x, oy, oz), side, otilt, True, offset)))
        parts.append((f"reg{side}", label_tris(reg, rc, (side * rx, ry, rz), side, rtilt, False, offset)))
    base = paint_colour(accent)
    colour = lambda n: ink if n.startswith("title") else INK if n.startswith("reg") else base(n)
    files = []
    for view, (az, el) in VIEWS.items():
        out = os.path.join(out_dir, f"{cid}_{view}.png")
        thumbs.render_view(parts, out, az, el, width=900, height=520, supersample=2, colour_fn=colour)
        # The rasteriser is right-handed and Unity is left-handed, so its frame is Unity's
        # mirror image; flip it back so the paint reads the way the game draws it.
        ImageOps.mirror(Image.open(out)).save(out)
        files.append(out)
    return files


def main():
    out_dir = sys.argv[1]
    for cid in (sys.argv[2:] or layoutgen.TYPES):
        print(cid, render(cid, out_dir)[0])


if __name__ == "__main__":
    main()
