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

def first_font(*paths):
    for path in paths:
        if os.path.isfile(path):
            return path
    raise FileNotFoundError(f"No HUD review font found in: {', '.join(paths)}")


REGULAR = first_font(
    "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
    "/System/Library/Fonts/Supplemental/Arial.ttf",
)
BOLD = first_font(
    "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
    "/System/Library/Fonts/Supplemental/Arial Bold.ttf",
)

# Airside.Presentation.AirsidePalette (Glass Cockpit, ADR 0122), kept in step by palette_check() below.
PALETTE = {
    "Glass": "#0E1216",
    "GlassRaised": "#1B222A",
    "GlassEdge": "#FFFFFF",
    "InstrumentText": "#E8EDF1",
    "InstrumentMuted": "#8793A0",
    "Aqua": "#3FD0C9",
    "Amber": "#FFB547",
    "GoGreen": "#4CD37A",
    "WarnRed": "#FF5F56",
    "RouteMagenta": "#E15AA8",
    "OnAccent": "#0B0F12",
}

TONE = {
    "Default": PALETTE["InstrumentText"],
    "Muted": PALETTE["InstrumentMuted"],
    "Accent": PALETTE["Aqua"],
    "Caution": PALETTE["Amber"],
    "Positive": PALETTE["GoGreen"],
    "Negative": PALETTE["WarnRed"],
    "Route": PALETTE["RouteMagenta"],
}

ART_DIR = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                       "game/Airside/Assets/Airside/Art")
ICON_DIR = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                        "game/Airside/Assets/Airside/Art/UI/Icons")

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


def draw_rect(image, rect, colour, alpha, radius=0.0, outline=None, width=1):
    layer = Image.new("RGBA", image.size, (0, 0, 0, 0))
    a = int(255 * max(0.0, min(1.0, alpha)))
    d = ImageDraw.Draw(layer)
    x0, y0, x1, y1 = rect
    if x1 - x0 < 0.5 or y1 - y0 < 0.5:
        return
    r = max(0.0, min(radius, (x1 - x0) / 2.0, (y1 - y0) / 2.0))
    if outline is None:
        d.rounded_rectangle(rect, radius=r, fill=colour + (a,))
    else:
        d.rounded_rectangle(rect, radius=r, outline=colour + (a,), width=width)
    image.alpha_composite(layer)


def fill_radius(w, h):
    """The painter's rule: fills of real size are softly rounded, rules stay square."""
    if w < 4 * SCALE or h < 4 * SCALE:
        return 0.0
    return min(6.0 * SCALE, w / 2.0, h / 2.0)


def glass(image, rect, alpha, radius):
    x0, y0, x1, y1 = rect
    for spread, a in ((10, 0.05), (6, 0.07), (3, 0.09)):
        s = spread * SCALE
        draw_rect(image, (x0 - s * 0.4, y0 + s * 0.2, x1 + s * 0.4, y1 + s), (0, 0, 0), a, radius + s)
    draw_rect(image, rect, rgb(PALETTE["Glass"]), alpha, radius)
    draw_rect(image, rect, rgb(PALETTE["GlassEdge"]), 0.09, radius, outline=True)


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
    radius = h / 2.0

    if style == 0:  # Primary — amber pill
        draw_rect(image, rect, rgb(PALETTE["Amber"]), 1.0 if enabled else 0.25, radius)
        text_colour = PALETTE["OnAccent"] if enabled else PALETTE["InstrumentMuted"]
    elif style == 2:  # Destructive — red-outlined pill
        draw_rect(image, rect, rgb(PALETTE["WarnRed"]), 0.10, radius)
        draw_rect(image, rect, rgb(PALETTE["WarnRed"]), 0.85, radius, outline=True, width=max(1, int(SCALE)))
        text_colour = PALETTE["WarnRed"]
    else:  # Secondary — raised glass pill
        draw_rect(image, rect, rgb(PALETTE["GlassRaised"]), 0.95 if enabled else 0.5, radius)
        draw_rect(image, rect, rgb(PALETTE["GlassEdge"]), 0.14, radius, outline=True)
        text_colour = PALETTE["InstrumentText"] if enabled else PALETTE["InstrumentMuted"]

    glyph = label in ("×", "?")
    size = 18.0 if glyph else 11.0
    draw_text(image, {
        "Box": [command["Box"][0], command["Box"][1] + (command["Box"][3] - size * 1.15) / 2.0,
                command["Box"][2], size * 1.3],
        "Text": label,
        "FontSize": size,
        "Style": STYLE_BOLD | (0 if glyph else STYLE_CAPTION),
        "Align": "Center",
        "Colour": text_colour,
        "Value": 1.0 if enabled else 0.55,
    })


_icon_cache = {}


def draw_icon(image, command):
    x, y, w, h = box(command)
    category, _, name = (command.get("Text") or "/").partition("/")
    path = os.path.join(ICON_DIR, f"ui_{category}_{name}_v01.png")
    if not os.path.exists(path):
        return
    if path not in _icon_cache:
        _icon_cache[path] = Image.open(path).convert("RGBA")
    src = _icon_cache[path].resize((max(1, int(w)), max(1, int(h))), Image.LANCZOS)
    alpha = src.getchannel("A").point(lambda v: int(v * max(0.0, min(1.0, command.get("Value", 1.0) or 1.0))))
    tint = Image.new("RGBA", src.size, colour_of(command) + (255,))
    tint.putalpha(alpha)
    image.alpha_composite(tint, (int(x), int(y)))


_art_cache = {}


def draw_image(image, command):
    x, y, w, h = box(command)
    path = os.path.join(ART_DIR, command.get("Text") or "")
    if not os.path.exists(path) or w < 1 or h < 1:
        return
    if path not in _art_cache:
        _art_cache[path] = Image.open(path).convert("RGBA")
    src = _art_cache[path]
    # Cover-crop, like IMGUI ScaleAndCrop.
    scale = max(w / src.width, h / src.height)
    resized = src.resize((max(1, int(src.width * scale)), max(1, int(src.height * scale))), Image.LANCZOS)
    left = (resized.width - int(w)) // 2
    top = (resized.height - int(h)) // 2
    cropped = resized.crop((left, top, left + int(w), top + int(h)))
    ox, oy = int(x), int(y)
    # Clip to the canvas.
    cx0, cy0 = max(0, -ox), max(0, -oy)
    cropped = cropped.crop((cx0, cy0, min(cropped.width, image.width - ox), min(cropped.height, image.height - oy)))
    image.alpha_composite(cropped, (max(0, ox), max(0, oy)))


def draw_ring(image, command):
    x, y, w, h = box(command)
    thickness = max(1, int(command.get("FontSize", 6.0) * SCALE))
    progress = max(0.0, min(1.0, command.get("Value", 0.0)))
    if progress <= 0.0:
        return
    layer = Image.new("RGBA", image.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    alpha = 90 if command.get("Tone") == "Muted" else 255
    if progress >= 0.999:
        d.ellipse((x, y, x + w, y + h), outline=colour_of(command) + (alpha,), width=thickness)
    else:
        d.arc((x, y, x + w, y + h), start=-90, end=-90 + 360 * progress, fill=colour_of(command) + (alpha,),
              width=thickness)
    image.alpha_composite(layer)


def draw_pill(image, command):
    x, y, w, h = box(command)
    rect = (x, y, x + w, y + h)
    filled = (command.get("Value", 0.0) or 0.0) >= 0.5
    colour = colour_of(command)
    if filled:
        draw_rect(image, rect, colour, 1.0, h / 2.0)
        text_colour = PALETTE["OnAccent"]
    else:
        draw_rect(image, rect, colour, 0.16, h / 2.0)
        text_colour = "#%02X%02X%02X" % colour
    size = command.get("FontSize", 10.0)
    draw_text(image, {
        "Box": [command["Box"][0], command["Box"][1] + (command["Box"][3] - size * 1.15) / 2.0,
                command["Box"][2], size * 1.3],
        "Text": command.get("Text") or "",
        "FontSize": size,
        "Style": STYLE_BOLD | STYLE_CAPTION,
        "Align": "Center",
        "Colour": text_colour,
        "Value": 1.0,
    })


def render(page, width, height):
    image = backdrop(width * SCALE, height * SCALE).convert("RGBA")

    for command in page["Commands"]:
        kind = command["Kind"]
        x, y, w, h = box(command)
        rect = (x, y, x + w, y + h)

        if kind == "Surface":
            glass(image, rect, command.get("Value", 0.9), 14 * SCALE)
        elif kind == "Card":
            draw_rect(image, rect, rgb(PALETTE["GlassRaised"]), 0.9 * (command.get("Value", 1.0) or 1.0), 10 * SCALE)
            draw_rect(image, rect, rgb(PALETTE["GlassEdge"]), 0.06, 10 * SCALE, outline=True)
        elif kind == "Fill":
            draw_rect(image, rect, colour_of(command), command.get("Value", 1.0), fill_radius(w, h))
        elif kind == "Hairline":
            draw_rect(image, rect, colour_of(command), command.get("Value", 1.0))
        elif kind == "Outline":
            draw_rect(image, rect, colour_of(command), command.get("Value", 1.0), min(10 * SCALE, h / 2.0),
                      outline=True, width=max(1, int(1.5 * SCALE)))
        elif kind == "Text":
            draw_text(image, command)
        elif kind == "Bar":
            draw_rect(image, rect, rgb(PALETTE["GlassEdge"]), 0.10, h / 2.0)
            progress = max(0.0, min(1.0, command.get("Value", 0.0)))
            if progress > 0.0:
                draw_rect(image, (x, y, x + max(h, w * progress), y + h), colour_of(command), 1.0, h / 2.0)
        elif kind == "Button":
            draw_button(image, command)
        elif kind == "Pill":
            draw_pill(image, command)
        elif kind == "Ring":
            draw_ring(image, command)
        elif kind == "Icon":
            draw_icon(image, command)
        elif kind == "Image":
            draw_image(image, command)
        elif kind == "Gradient":
            layer = Image.new("RGBA", image.size, (0, 0, 0, 0))
            d = ImageDraw.Draw(layer)
            colour = colour_of(command)
            top = command.get("Value", 1.0)
            steps = max(1, int(w))
            for i in range(steps):
                t = i / max(1, steps - 1)
                d.line([(x + i, y), (x + i, y + h)], fill=colour + (int(255 * top * (1 - t * t)),))
            image.alpha_composite(layer)
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
