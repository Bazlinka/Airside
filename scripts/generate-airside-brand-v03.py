#!/usr/bin/env python3
"""Generate Airside's v03 control-vector identity assets.

The symbol is an original route switch: two controlled paths trade sides through
the centre, with one amber control point.  It is deliberately not an aircraft,
wing, letter A, or perspective runway.
"""

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[1]
SCALE = 4
INK = (23, 36, 42, 255)
AQUA = (63, 208, 201, 255)
AMBER = (255, 181, 71, 255)
CLOUD = (238, 241, 236, 255)

RUNTIME = ROOT / "game/Airside/Assets/Airside/Art/Brand"
STREAMING = ROOT / "game/Airside/Assets/StreamingAssets/Airside/Art/Brand"
CANDIDATES = ROOT / "docs/art/candidates"
SOURCES = ROOT / "docs/art/source"


def scaled(points):
    return [(round(x * SCALE), round(y * SCALE)) for x, y in points]


def rounded_line(draw, points, fill, width):
    pts = scaled(points)
    line_width = round(width * SCALE)
    draw.line(pts, fill=fill, width=line_width, joint="curve")
    radius = line_width // 2
    for x, y in (pts[0], pts[-1]):
        draw.ellipse((x - radius, y - radius, x + radius, y + radius), fill=fill)


def bezier(p0, p1, p2, p3, steps=80):
    points = []
    for step in range(steps + 1):
        t = step / steps
        mt = 1.0 - t
        points.append(
            (
                mt**3 * p0[0] + 3 * mt**2 * t * p1[0] + 3 * mt * t**2 * p2[0] + t**3 * p3[0],
                mt**3 * p0[1] + 3 * mt**2 * t * p1[1] + 3 * mt * t**2 * p2[1] + t**3 * p3[1],
            )
        )
    return points


def draw_mark(draw, origin=(0, 0), scale=1.0):
    ox, oy = origin
    font = ImageFont.truetype(
        "/System/Library/Fonts/Avenir Next.ttc", round(550 * scale * SCALE), index=0
    )

    # A clear AS monogram rather than another runway disguised as a letter A.
    draw.text(((ox + 170 * scale) * SCALE, (oy + 194 * scale) * SCALE), "A", font=font, fill=AQUA)
    draw.text(((ox + 445 * scale) * SCALE, (oy + 194 * scale) * SCALE), "S", font=font, fill=CLOUD)

    # A single controlled route climbs through the monogram to its amber destination.
    route = bezier(
        (ox + 230 * scale, oy + 744 * scale),
        (ox + 390 * scale, oy + 558 * scale),
        (ox + 625 * scale, oy + 530 * scale),
        (ox + 752 * scale, oy + 382 * scale),
    )
    rounded_line(draw, route, CLOUD, 24 * scale)
    cx = (ox + 752 * scale) * SCALE
    cy = (oy + 382 * scale) * SCALE
    radius = 28 * scale * SCALE
    draw.ellipse((cx - radius, cy - radius, cx + radius, cy + radius), fill=AMBER)


def canvas(size=(1024, 1024), background=(0, 0, 0, 0)):
    return Image.new("RGBA", (size[0] * SCALE, size[1] * SCALE), background)


def finish(image, size):
    return image.resize(size, Image.Resampling.LANCZOS)


def save_all(image, filename, candidate=True):
    for folder in (RUNTIME, STREAMING):
        folder.mkdir(parents=True, exist_ok=True)
        image.save(folder / filename, optimize=True)
    if candidate:
        CANDIDATES.mkdir(parents=True, exist_ok=True)
        image.save(CANDIDATES / filename, optimize=True)


def generate_mark():
    image = canvas()
    draw_mark(ImageDraw.Draw(image))
    result = finish(image, (1024, 1024))
    save_all(result, "airside_brand_mark_v03.png")


def generate_icon():
    image = canvas()
    draw = ImageDraw.Draw(image)
    draw.rounded_rectangle((0, 0, 1024 * SCALE, 1024 * SCALE), radius=190 * SCALE, fill=INK)
    draw_mark(draw, origin=(72, 72), scale=0.86)
    result = finish(image, (1024, 1024))
    save_all(result, "airside_app_icon_v03.png")


def generate_wordmark():
    width, height = 2048, 512
    image = canvas((width, height))
    draw = ImageDraw.Draw(image)
    draw_mark(draw, origin=(18, -22), scale=0.50)

    font = ImageFont.truetype("/System/Library/Fonts/Avenir Next.ttc", 206 * SCALE, index=0)
    x = 548 * SCALE
    y = 122 * SCALE
    tracking = 8 * SCALE
    for index, letter in enumerate("AIRSIDE"):
        color = CLOUD if index < 3 else AQUA
        draw.text((x, y), letter, font=font, fill=color)
        box = draw.textbbox((x, y), letter, font=font)
        x = box[2] + tracking

    result = finish(image, (width, height))
    save_all(result, "airside_wordmark_light_v03.png")


def generate_svg_sources():
    SOURCES.mkdir(parents=True, exist_ok=True)
    mark = """<text x=\"170\" y=\"700\" font-family=\"Avenir Next\" font-size=\"550\" font-weight=\"700\" fill=\"#3FD0C9\">A</text>
<text x=\"445\" y=\"700\" font-family=\"Avenir Next\" font-size=\"550\" font-weight=\"700\" fill=\"#EEF1EC\">S</text>
<path d=\"M230 744C390 558 625 530 752 382\" fill=\"none\" stroke=\"#EEF1EC\" stroke-width=\"24\" stroke-linecap=\"round\"/>
<circle cx=\"752\" cy=\"382\" r=\"28\" fill=\"#FFB547\"/>"""
    (SOURCES / "airside_brand_mark_v03.svg").write_text(
        f'<svg xmlns="http://www.w3.org/2000/svg" width="1024" height="1024" viewBox="0 0 1024 1024">\n  <!-- AIRSIDE v03 control-vector mark. -->\n{mark}\n</svg>\n'
    )
    icon_mark = mark
    (SOURCES / "airside_app_icon_v03.svg").write_text(
        f'<svg xmlns="http://www.w3.org/2000/svg" width="1024" height="1024" viewBox="0 0 1024 1024">\n  <!-- AIRSIDE v03 app icon with a Dock-safe rounded field. -->\n  <rect width="1024" height="1024" rx="190" fill="#17242A"/>\n{icon_mark}\n</svg>\n'
    )
    (SOURCES / "airside_wordmark_light_v03.svg").write_text(
        """<svg xmlns="http://www.w3.org/2000/svg" width="2048" height="512" viewBox="0 0 2048 512">
  <!-- Editable wordmark source. Runtime PNG text is rasterised with Avenir Next Bold. -->
  <g transform="translate(18 -22) scale(.50)">
    <text x="170" y="700" font-family="Avenir Next" font-size="550" font-weight="700" fill="#3FD0C9">A</text>
    <text x="445" y="700" font-family="Avenir Next" font-size="550" font-weight="700" fill="#EEF1EC">S</text>
    <path d="M230 744C390 558 625 530 752 382" fill="none" stroke="#EEF1EC" stroke-width="24" stroke-linecap="round"/>
    <circle cx="752" cy="382" r="28" fill="#FFB547"/>
  </g>
  <text x="548" y="330" font-family="Avenir Next" font-size="206" font-weight="700" letter-spacing="8" fill="#EEF1EC">AIR</text>
  <text x="910" y="330" font-family="Avenir Next" font-size="206" font-weight="700" letter-spacing="8" fill="#3FD0C9">SIDE</text>
</svg>
"""
    )


def main():
    generate_mark()
    generate_icon()
    generate_wordmark()
    generate_svg_sources()


if __name__ == "__main__":
    main()
