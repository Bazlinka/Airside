#!/usr/bin/env python3
"""Draw each type's fuselage titles to scale against its real airframe length.

Fuselage titles are runtime TextMesh geometry, so they cannot be rendered by the
offline aircraft rasteriser. What can be shown without a Unity editor is the
arithmetic: how big the painted title actually comes out in metres beside the
aeroplane it is painted on, and how big the removed backing plate was.

Usage:
  dotnet run --project scripts/hud-mockup/HudMockup.csproj -- work/hud-mockups/draw-lists.json
  python3 scripts/render-aircraft-titles.py work/hud-mockups/title-metrics.json out.png
"""

import json
import sys

from PIL import Image, ImageDraw, ImageFont

REGULAR = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"
BOLD = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"

INK = (23, 36, 42)
CLOUD = (238, 241, 236)
CONCRETE = (156, 163, 162)
SKIN = (222, 226, 228)
LIVERY = (31, 58, 147)
RED = (201, 93, 80)
GREEN = (95, 139, 104)

ROW_HEIGHT = 130
MARGIN = 40
LABEL_WIDTH = 190
PX_PER_METRE = 11.0


def main():
    source = sys.argv[1] if len(sys.argv) > 1 else "work/hud-mockups/title-metrics.json"
    out = sys.argv[2] if len(sys.argv) > 2 else "work/hud-mockups/aircraft-titles.png"
    with open(source, encoding="utf-8") as handle:
        types = json.load(handle)

    longest = max(t["LengthMetres"] for t in types)
    width = int(MARGIN * 2 + LABEL_WIDTH + longest * PX_PER_METRE) + 60
    height = MARGIN * 2 + 70 + ROW_HEIGHT * len(types)
    image = Image.new("RGB", (width, height), INK)
    draw = ImageDraw.Draw(image)

    title_font = ImageFont.truetype(BOLD, 21)
    head_font = ImageFont.truetype(BOLD, 13)
    small_font = ImageFont.truetype(REGULAR, 12)

    draw.text((MARGIN, MARGIN - 8), "FUSELAGE TITLES, TO SCALE", font=title_font, fill=CLOUD)
    draw.text((MARGIN, MARGIN + 20),
              "Each aeroplane is drawn at its catalogue length. Blue is the painted airline "
              "title; the red outline is the backing plate that used to sit behind it.",
              font=small_font, fill=CONCRETE)

    y = MARGIN + 60
    for spec in types:
        length_px = spec["LengthMetres"] * PX_PER_METRE
        body_h = max(14.0, spec["HeightMetres"] * PX_PER_METRE * 0.42)
        x0 = MARGIN + LABEL_WIDTH
        cy = y + ROW_HEIGHT / 2

        draw.text((MARGIN, cy - 22), spec["Type"], font=head_font, fill=CLOUD)
        draw.text((MARGIN, cy - 4),
                  f"{spec['LengthMetres']:.1f} m airframe", font=small_font, fill=CONCRETE)
        draw.text((MARGIN, cy + 12),
                  f"title {spec['TitleWidthMetres']:.2f} x {spec['TitleHeightMetres']:.2f} m",
                  font=small_font, fill=GREEN)

        # Fuselage side, nose to the right.
        draw.rounded_rectangle([x0, cy - body_h / 2, x0 + length_px, cy + body_h / 2],
                               radius=body_h / 2, fill=SKIN)

        # Painted title, forward third of the fuselage.
        tw = spec["TitleWidthMetres"] * PX_PER_METRE
        th = spec["TitleHeightMetres"] * PX_PER_METRE
        tx = x0 + length_px * 0.20
        draw.rectangle([tx, cy - th / 2, tx + tw, cy + th / 2], fill=LIVERY)
        label_font = ImageFont.truetype(BOLD, max(7, int(th * 0.62)))
        draw.text((tx + 4, cy - th / 2 + 1), spec["Title"], font=label_font, fill=CLOUD)

        # Budget marker: how far a title is allowed to run before it is shrunk.
        bx = x0 + length_px * 0.20 + spec["BudgetMetres"] * PX_PER_METRE
        draw.line([(bx, cy - body_h / 2 - 6), (bx, cy + body_h / 2 + 6)], fill=GREEN, width=1)

        # The removed backing plate, at the size it actually came out.
        pw = spec["OldPlateWidthMetres"] * PX_PER_METRE
        ph = spec["OldPlateHeightMetres"] * PX_PER_METRE
        px = tx + tw / 2 - pw / 2
        draw.rectangle([px, cy - ph / 2, px + pw, cy + ph / 2], outline=RED, width=2)

        y += ROW_HEIGHT

    draw.text((MARGIN, height - MARGIN + 6),
              "Green tick = the length budget a long airline name is shrunk to fit.",
              font=small_font, fill=CONCRETE)
    image.save(out)
    print(f"{out}  {image.width}x{image.height}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
