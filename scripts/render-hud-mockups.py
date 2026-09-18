#!/usr/bin/env python3
"""Rasterise the HUD draw lists exported by scripts/hud-mockup into PNGs.

The draw list is the same one the runtime HUD paints through IMGUI
(Airside.Presentation.HudDrawList), produced by the same painters from a real
headless airline. Rendering it here is how a workspace can be compared against its
concept reference on a machine with no Unity editor.

Deliberately not a pixel-exact Unity emulator: IMGUI's glyph metrics differ from
DejaVu's. It is exact about geometry, colour, ordering and text content, which is
what the layout work is about.

Usage:
  dotnet run --project scripts/hud-mockup/HudMockup.csproj -- work/hud-mockups/draw-lists.json
  python3 scripts/render-hud-mockups.py work/hud-mockups/draw-lists.json work/hud-mockups
"""

import json
import math
import os
import sys

from PIL import Image, ImageDraw, ImageFont

SCALE = 1.4

REGULAR = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"
BOLD = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"

# Airside.Presentation.AirsidePalette, kept in step by palette_check() below.
PALETTE = {
    "RunwayInk": "#17242A",
    "Tarmac": "#343B40",
    "Concrete": "#9CA3A2",
    "CoastalBlue": "#39708A",
    "CoastalBlueStrong": "#2E86B0",
    "SafetyYellow": "#F2C14B",
    "SignalRed": "#C95D50",
    "ClearGreen": "#5F8B68",
    "Cloud": "#EEF1EC",
    "OpenSky": "#A7C9D9",
}

TONE = {
    "Default": PALETTE["Cloud"],
    "Muted": PALETTE["Concrete"],
    "Accent": PALETTE["CoastalBlueStrong"],
    "Caution": PALETTE["SafetyYellow"],
    "Positive": PALETTE["ClearGreen"],
    "Negative": PALETTE["SignalRed"],
}

STYLE_BOLD = 1
STYLE_CAPTION = 2
STYLE_WRAP = 4

_font_cache = {}


def font(size_px, bold):
    key = (int(round(size_px)), bold)
    if key not in _font_cache:
        _font_cache[key] = ImageFont.truetype(BOLD if bold else REGULAR, max(7, key[0]))
    return _font_cache[key]


def rgb(hex_colour):
    h = hex_colour.lstrip("#")
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def colour_of(command):
    explicit = command.get("Colour")
    if explicit:
        return rgb(explicit)
    return rgb(TONE.get(command.get("Tone", "Default"), TONE["Default"]))


def box(command):
    x, y, w, h = command["Box"]
    return x * SCALE, y * SCALE, w * SCALE, h * SCALE


def backdrop(width, height):
    """A calm airfield-green backdrop so translucent panels read as they do in game."""
    image = Image.new("RGB", (int(width), int(height)), (118, 132, 88))
    draw = ImageDraw.Draw(image)
    for y in range(int(height)):
        t = y / max(1.0, height)
        draw.line(
            [(0, y), (width, y)],
            fill=(
                int(126 - 26 * t),
                int(140 - 24 * t),
                int(96 - 18 * t),
            ),
        )
    # A hint of runway and taxiway so the HUD is judged over something, not over flat colour.
    draw.polygon(
        [(0.06 * width, height), (0.30 * width, 0.30 * height),
         (0.35 * width, 0.30 * height), (0.16 * width, height)],
        fill=(58, 62, 64),
    )
    draw.polygon(
        [(0.72 * width, 0.02 * height), (0.99 * width, 0.02 * height),
         (0.99 * width, 0.22 * height), (0.80 * width, 0.22 * height)],
        fill=(52, 96, 122),
    )
    return image


def blend(base, layer):
    return Image.alpha_composite(base, layer)


def draw_rect(image, rect, colour, alpha):
    if alpha >= 0.999:
        ImageDraw.Draw(image).rectangle(rect, fill=colour + (255,))
        return
    layer = Image.new("RGBA", image.size, (0, 0, 0, 0))
    ImageDraw.Draw(layer).rectangle(rect, fill=colour + (int(255 * max(0.0, min(1.0, alpha))),))
    image.alpha_composite(layer)


def text_width(draw, value, f, tracking):
    return draw.textlength(value, font=f) + tracking * max(0, len(value) - 1)


def draw_text(image, command):
    x, y, w, h = box(command)
    value = command.get("Text") or ""
    style = command.get("Style", 0)
    bold = bool(style & STYLE_BOLD)
    caption = bool(style & STYLE_CAPTION)
    wrap = bool(style & STYLE_WRAP)
    size = command.get("FontSize", 12.0) * SCALE
    f = font(size, bold)
    tracking = size * 0.10 if caption else 0.0
    alpha = command.get("Value", 1.0) or 1.0
    colour = colour_of(command) + (int(255 * max(0.0, min(1.0, alpha))),)

    layer = Image.new("RGBA", image.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(layer)

    lines = [value]
    if not wrap:
        # IMGUI clips non-wrapping labels to their rect (TextClipping.Clip); do the same, or
        # the mockup shows an overrun the game does not have.
        while len(lines[0]) > 1 and text_width(draw, lines[0], f, tracking) > w:
            lines[0] = lines[0][:-1]
    if wrap:
        lines = []
        current = ""
        for word in value.split(" "):
            candidate = word if not current else current + " " + word
            if text_width(draw, candidate, f, tracking) <= w or not current:
                current = candidate
            else:
                lines.append(current)
                current = word
        if current:
            lines.append(current)

    line_height = size * 1.25
    cy = y
    for line in lines:
        if cy > y + max(h, line_height) + line_height:
            break
        width = text_width(draw, line, f, tracking)
        if command.get("Align") == "Center":
            cx = x + (w - width) / 2.0
        elif command.get("Align") == "Right":
            cx = x + w - width
        else:
            cx = x
        if tracking:
            for ch in line:
                draw.text((cx, cy), ch, font=f, fill=colour)
                cx += draw.textlength(ch, font=f) + tracking
        else:
            draw.text((cx, cy), line, font=f, fill=colour)
        cy += line_height

    image.alpha_composite(layer)


def draw_button(image, command):
    x, y, w, h = box(command)
    rect = (x, y, x + w, y + h)
    style = int(command.get("Value", 0))
    enabled = command.get("Enabled", True)
    label = command.get("Text") or ""

    if style == 0:  # Primary
        fill = rgb(PALETTE["CoastalBlueStrong"])
        draw_rect(image, rect, fill, 1.0 if enabled else 0.28)
        text_colour = PALETTE["Cloud"] if enabled else PALETTE["Concrete"]
        border = None
    elif style == 2:  # Destructive
        draw_rect(image, rect, rgb(PALETTE["Tarmac"]), 0.45)
        border = rgb(PALETTE["SignalRed"])
        text_colour = PALETTE["SignalRed"]
    else:  # Secondary
        draw_rect(image, rect, rgb(PALETTE["Tarmac"]), 0.85 if enabled else 0.45)
        border = rgb(PALETTE["Concrete"])
        text_colour = PALETTE["Cloud"] if enabled else PALETTE["Concrete"]

    if border:
        layer = Image.new("RGBA", image.size, (0, 0, 0, 0))
        ImageDraw.Draw(layer).rectangle(rect, outline=border + (150 if enabled else 80,), width=1)
        image.alpha_composite(layer)

    draw_text(image, {
        "Box": [command["Box"][0], command["Box"][1] + (command["Box"][3] - 13) / 2.0,
                command["Box"][2], 16],
        "Text": label,
        "FontSize": 12.0,
        "Style": STYLE_BOLD | STYLE_CAPTION,
        "Align": "Center",
        "Colour": text_colour,
        "Value": 1.0 if enabled else 0.55,
    })


def render(page, width, height):
    image = backdrop(width * SCALE, height * SCALE).convert("RGBA")

    for command in page["Commands"]:
        kind = command["Kind"]
        x, y, w, h = box(command)
        rect = (x, y, x + w, y + h)

        if kind == "Surface":
            draw_rect(image, rect, rgb(PALETTE["RunwayInk"]), command.get("Value", 0.96))
            layer = Image.new("RGBA", image.size, (0, 0, 0, 0))
            ImageDraw.Draw(layer).rectangle(rect, outline=rgb(PALETTE["CoastalBlue"]) + (120,), width=1)
            image.alpha_composite(layer)
        elif kind in ("Fill", "Hairline"):
            draw_rect(image, rect, colour_of(command), command.get("Value", 1.0))
        elif kind == "Outline":
            layer = Image.new("RGBA", image.size, (0, 0, 0, 0))
            ImageDraw.Draw(layer).rectangle(
                rect, outline=colour_of(command) + (int(255 * command.get("Value", 1.0)),), width=1)
            image.alpha_composite(layer)
        elif kind == "Text":
            draw_text(image, command)
        elif kind == "Bar":
            draw_rect(image, rect, rgb(PALETTE["Tarmac"]), 1.0)
            progress = max(0.0, min(1.0, command.get("Value", 0.0)))
            if progress > 0.0:
                draw_rect(image, (x, y, x + w * progress, y + h), colour_of(command), 1.0)
        elif kind == "Button":
            draw_button(image, command)
        elif kind == "Dot":
            layer = Image.new("RGBA", image.size, (0, 0, 0, 0))
            ImageDraw.Draw(layer).ellipse(rect, fill=colour_of(command) + (255,))
            image.alpha_composite(layer)
        elif kind == "Line":
            layer = Image.new("RGBA", image.size, (0, 0, 0, 0))
            thickness = max(1, int(round(command.get("Value", 1.0) * SCALE)))
            ImageDraw.Draw(layer).line([(x, y), (x + w, y + h)],
                                       fill=colour_of(command) + (190,), width=thickness)
            image.alpha_composite(layer)
        elif kind == "Hotspot":
            continue

    return image.convert("RGB")


def palette_check(repo_root):
    """Fail loudly if the C# palette and this renderer drift apart."""
    source = os.path.join(repo_root, "game/Airside/Assets/Airside/Presentation/AirsidePalette.cs")
    if not os.path.exists(source):
        return
    with open(source, encoding="utf-8") as handle:
        text = handle.read()
    for name, value in PALETTE.items():
        if f'{name}Hex = "{value}"' not in text:
            raise SystemExit(f"Palette drift: {name} is not {value} in AirsidePalette.cs")


def main():
    source = sys.argv[1] if len(sys.argv) > 1 else "work/hud-mockups/draw-lists.json"
    out_dir = sys.argv[2] if len(sys.argv) > 2 else "work/hud-mockups"
    repo_root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    palette_check(repo_root)

    with open(source, encoding="utf-8") as handle:
        document = json.load(handle)
    width, height = document["Viewport"]
    os.makedirs(out_dir, exist_ok=True)

    for page in document["Pages"]:
        image = render(page, width, height)
        path = os.path.join(out_dir, f"hud-{page['Name']}.png")
        image.save(path)
        print(f"{path}  {image.width}x{image.height}  {len(page['Commands'])} commands")

    return 0


if __name__ == "__main__":
    sys.exit(main())
