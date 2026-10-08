#!/usr/bin/env python3
"""Render the original v04 departure-vector wordmark from editable vector strokes."""
import argparse
import io
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
SCALE = 3
CLOUD = '#EEF1EC'
AQUA = '#3FD0C9'
AMBER = '#FFB547'
# Hand-drafted uppercase glyphs: no system font or third-party logo dependency.
GLYPHS = {
    'A': [[(0, 100), (36, 0), (72, 100)], [(13, 65), (59, 65)]],
    'I': [[(20, 0), (20, 100)]],
    'R': [[(0, 100), (0, 0), (46, 0), (64, 14), (64, 39), (46, 52), (0, 52)], [(35, 52), (72, 100)]],
    'S': [[(68, 0), (18, 0), (0, 15), (0, 36), (18, 50), (50, 50), (68, 65), (68, 85), (50, 100), (0, 100)]],
    'D': [[(0, 0), (0, 100), (42, 100), (68, 78), (68, 22), (42, 0), (0, 0)]],
    'E': [[(68, 0), (0, 0), (0, 100), (68, 100)], [(0, 50), (54, 50)]]
}
WIDTHS = {'A': 72, 'I': 40, 'R': 72, 'S': 68, 'D': 68, 'E': 68}
POLYGONS = [
    (AQUA, [(18, 144), (206, 28), (124, 213), (105, 137)]),
    (CLOUD, [(18, 144), (105, 137), (124, 213), (80, 169)]),
    (AMBER, [(191, 37), (206, 28), (198, 46), (185, 55)])
]


def generate():
    image = Image.new('RGBA', (1800 * SCALE, 360 * SCALE))
    draw = ImageDraw.Draw(image)
    svg = ['<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1800 360">']
    for colour, points in POLYGONS:
        translated = [(x * 1.15 + 32, y * 1.15 + 38) for x, y in points]
        draw.polygon([(round(x * SCALE), round(y * SCALE)) for x, y in translated], fill=colour)
        svg.append(f'<polygon fill="{colour}" points="' + ' '.join(f'{x:.2f},{y:.2f}' for x, y in translated) + '"/>')
    x = 354
    for letter in 'AIRSIDE':
        for stroke in GLYPHS[letter]:
            points = [(x + a * 2.18, 68 + b * 2.18) for a, b in stroke]
            draw.line([(round(a * SCALE), round(b * SCALE)) for a, b in points], fill=CLOUD,
                      width=round(18 * SCALE), joint='curve')
            # Same round caps in the raster and editable SVG source.
            for a, b in (points[0], points[-1]):
                draw.ellipse(((a - 9) * SCALE, (b - 9) * SCALE, (a + 9) * SCALE, (b + 9) * SCALE), fill=CLOUD)
            svg.append(f'<polyline fill="none" stroke="{CLOUD}" stroke-width="18" stroke-linecap="round" stroke-linejoin="round" points="' +
                       ' '.join(f'{a:.2f},{b:.2f}' for a, b in points) + '"/>')
        x += WIDTHS[letter] * 2.18 + 50
    svg.append('</svg>\n')
    output = io.BytesIO()
    image.resize((1800, 360), Image.Resampling.LANCZOS).save(output, format='PNG')
    return output.getvalue(), '\n'.join(svg).encode()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    png, svg = generate()
    targets = {
        ROOT / 'docs/art/source/airside_wordmark_light_v04.svg': svg,
        ROOT / 'game/Airside/Assets/Airside/Art/Brand/airside_wordmark_light_v04.png': png,
        ROOT / 'game/Airside/Assets/StreamingAssets/Airside/Art/Brand/airside_wordmark_light_v04.png': png
    }
    for path, content in targets.items():
        if args.check:
            if not path.exists() or path.read_bytes() != content:
                raise SystemExit(f'Out of date: {path.relative_to(ROOT)}')
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(content)
    print('v04 vector wordmark matches source' if args.check else 'Wrote v04 vector wordmark and source')


if __name__ == '__main__':
    main()
