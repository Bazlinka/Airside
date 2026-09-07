#!/usr/bin/env python3
"""Generate the review candidate for Airside's light wordmark."""

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "docs/art/candidates/airside_wordmark_light_v01.png"
WIDTH, HEIGHT = 2048, 512
CLOUD = (238, 241, 236, 255)
COASTAL_BLUE = (57, 112, 138, 255)
SAFETY_YELLOW = (242, 193, 75, 255)


def draw_wordmark() -> None:
    image = Image.new("RGBA", (WIDTH, HEIGHT), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)

    # A compact wayfinding mark: an abstract A enclosing a runway centreline.
    draw.polygon([(92, 400), (270, 80), (448, 400), (365, 400), (270, 226), (175, 400)], fill=COASTAL_BLUE)
    draw.rounded_rectangle((246, 142, 294, 402), radius=18, fill=CLOUD)
    for top in (168, 246, 324):
        draw.rounded_rectangle((258, top, 282, top + 44), radius=8, fill=SAFETY_YELLOW)

    font_path = "/System/Library/Fonts/Avenir Next.ttc"
    font = ImageFont.truetype(font_path, 252, index=5)
    x = 520
    y = 116
    tracking = 22
    for letter in "AIRSIDE":
        draw.text((x, y), letter, font=font, fill=CLOUD, stroke_width=0)
        box = draw.textbbox((x, y), letter, font=font)
        x = box[2] + tracking

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    image.save(OUTPUT, optimize=True)


if __name__ == "__main__":
    draw_wordmark()
