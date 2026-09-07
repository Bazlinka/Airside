#!/usr/bin/env python3
"""Generate Batch F4 UI-ICO-005 system-control icons (128×128 transparent line art).

Project-owned procedural assets matching Batch E Runway Ink stroke style.
Writes candidates sheet + runtime slices under Art/UI/Icons.
"""

from __future__ import annotations

import hashlib
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path("/workspace")
CANDIDATES = ROOT / "docs/art/candidates"
RUNTIME_ICONS = ROOT / "game/Airside/Assets/Airside/Art/UI/Icons"

INK = (23, 36, 42, 255)  # Runway Ink #17242A
CLEAR = (0, 0, 0, 0)
SIZE = 128
PAD = 22  # safe area so icons stay legible at 24 px HUD slots
STROKE = 5


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def new_icon() -> tuple[Image.Image, ImageDraw.ImageDraw]:
    img = Image.new("RGBA", (SIZE, SIZE), CLEAR)
    return img, ImageDraw.Draw(img)


def stroke(draw: ImageDraw.ImageDraw, xy, width: int = STROKE) -> None:
    draw.line(xy, fill=INK, width=width, joint="curve")


def draw_play(draw: ImageDraw.ImageDraw) -> None:
    # Right-pointing triangle
    pts = [(PAD + 8, PAD), (SIZE - PAD, SIZE // 2), (PAD + 8, SIZE - PAD)]
    draw.polygon(pts, outline=INK)
    stroke(draw, [pts[0], pts[1]], STROKE)
    stroke(draw, [pts[1], pts[2]], STROKE)
    stroke(draw, [pts[2], pts[0]], STROKE)


def draw_pause(draw: ImageDraw.ImageDraw) -> None:
    w = 14
    gap = 18
    left = SIZE // 2 - gap // 2 - w
    right = SIZE // 2 + gap // 2
    draw.rectangle([left, PAD, left + w, SIZE - PAD], outline=INK, width=STROKE)
    draw.rectangle([right, PAD, right + w, SIZE - PAD], outline=INK, width=STROKE)


def draw_speed(draw: ImageDraw.ImageDraw) -> None:
    # Double chevron >>
    for ox in (-18, 10):
        stroke(
            draw,
            [
                (SIZE // 2 + ox - 10, PAD + 8),
                (SIZE // 2 + ox + 18, SIZE // 2),
                (SIZE // 2 + ox - 10, SIZE - PAD - 8),
            ],
            STROKE + 1,
        )


def draw_follow(draw: ImageDraw.ImageDraw) -> None:
    # Crosshair / target reticle
    cx = cy = SIZE // 2
    r = 34
    draw.ellipse([cx - r, cy - r, cx + r, cy + r], outline=INK, width=STROKE)
    draw.ellipse([cx - 10, cy - 10, cx + 10, cy + 10], outline=INK, width=STROKE)
    stroke(draw, [(cx, PAD + 4), (cx, cy - 14)])
    stroke(draw, [(cx, cy + 14), (cx, SIZE - PAD - 4)])
    stroke(draw, [(PAD + 4, cy), (cx - 14, cy)])
    stroke(draw, [(cx + 14, cy), (SIZE - PAD - 4, cy)])


def draw_overview(draw: ImageDraw.ImageDraw) -> None:
    # Map / overview rectangle with horizon
    draw.rectangle([PAD, PAD + 6, SIZE - PAD, SIZE - PAD - 6], outline=INK, width=STROKE)
    stroke(draw, [(PAD + 8, SIZE // 2), (SIZE - PAD - 8, SIZE // 2)])
    # small aircraft mark
    cx, cy = SIZE // 2, SIZE // 2 - 14
    stroke(draw, [(cx - 16, cy), (cx + 16, cy)])
    stroke(draw, [(cx, cy - 10), (cx, cy + 14)])


def draw_audio_on(draw: ImageDraw.ImageDraw) -> None:
    # Speaker + waves
    draw.polygon(
        [(PAD + 6, SIZE // 2 - 14), (PAD + 28, SIZE // 2 - 14), (PAD + 48, PAD + 10),
         (PAD + 48, SIZE - PAD - 10), (PAD + 28, SIZE // 2 + 14), (PAD + 6, SIZE // 2 + 14)],
        outline=INK,
    )
    stroke(draw, [(PAD + 6, SIZE // 2 - 14), (PAD + 28, SIZE // 2 - 14), (PAD + 48, PAD + 10),
                  (PAD + 48, SIZE - PAD - 10), (PAD + 28, SIZE // 2 + 14), (PAD + 6, SIZE // 2 + 14),
                  (PAD + 6, SIZE // 2 - 14)])
    for r in (18, 30):
        draw.arc(
            [PAD + 52 - 4, SIZE // 2 - r, PAD + 52 + r * 2 - 8, SIZE // 2 + r],
            start=-55,
            end=55,
            fill=INK,
            width=STROKE,
        )


def draw_audio_off(draw: ImageDraw.ImageDraw) -> None:
    draw_audio_on(draw)
    # Mute slash
    stroke(draw, [(PAD + 4, SIZE - PAD - 4), (SIZE - PAD - 4, PAD + 4)], STROKE + 1)


def draw_save(draw: ImageDraw.ImageDraw) -> None:
    # Floppy / save plate
    draw.rounded_rectangle(
        [PAD + 4, PAD + 4, SIZE - PAD - 4, SIZE - PAD - 4],
        radius=6,
        outline=INK,
        width=STROKE,
    )
    draw.rectangle([PAD + 22, PAD + 4, SIZE - PAD - 22, PAD + 28], outline=INK, width=STROKE)
    draw.rectangle([PAD + 18, SIZE // 2 + 4, SIZE - PAD - 18, SIZE - PAD - 12], outline=INK, width=STROKE)
    stroke(draw, [(PAD + 28, SIZE // 2 + 18), (SIZE - PAD - 28, SIZE // 2 + 18)])


ICONS = [
    ("play", draw_play),
    ("pause", draw_pause),
    ("speed", draw_speed),
    ("follow", draw_follow),
    ("overview", draw_overview),
    ("audio_on", draw_audio_on),
    ("audio_off", draw_audio_off),
    ("save", draw_save),
]


def write_meta(path: Path, guid: str) -> None:
    # Match existing UI icon importer settings (Batch E).
    path.write_text(
        "fileFormatVersion: 2\n"
        f"guid: {guid}\n"
        "TextureImporter:\n"
        "  internalIDToNameTable: []\n"
        "  externalObjects: {}\n"
        "  serializedVersion: 13\n"
        "  mipmaps:\n"
        "    mipMapMode: 0\n"
        "    enableMipMap: 0\n"
        "    sRGBTexture: 1\n"
        "    linearTexture: 0\n"
        "    fadeOut: 0\n"
        "    borderMipMap: 0\n"
        "    mipMapsPreserveCoverage: 0\n"
        "    alphaTestReferenceValue: 0.5\n"
        "    mipMapFadeDistanceStart: 1\n"
        "    mipMapFadeDistanceEnd: 3\n"
        "  bumpmap:\n"
        "    convertToNormalMap: 0\n"
        "    externalNormalMap: 0\n"
        "    heightScale: 0.25\n"
        "    normalMapFilter: 0\n"
        "    flipGreenChannel: 0\n"
        "  isReadable: 0\n"
        "  streamingMipmaps: 0\n"
        "  streamingMipmapsPriority: 0\n"
        "  vTOnly: 0\n"
        "  ignoreMipmapLimit: 0\n"
        "  grayScaleToAlpha: 0\n"
        "  generateCubemap: 6\n"
        "  cubemapConvolution: 0\n"
        "  seamlessCubemap: 0\n"
        "  textureFormat: 1\n"
        "  maxTextureSize: 128\n"
        "  textureSettings:\n"
        "    serializedVersion: 2\n"
        "    filterMode: 1\n"
        "    aniso: 1\n"
        "    mipBias: 0\n"
        "    wrapU: 1\n"
        "    wrapV: 1\n"
        "    wrapW: 1\n"
        "  nPOTScale: 0\n"
        "  lightmap: 0\n"
        "  compressionQuality: 50\n"
        "  spriteMode: 0\n"
        "  spriteExtrude: 1\n"
        "  spriteMeshType: 1\n"
        "  alignment: 0\n"
        "  spritePivot: {x: 0.5, y: 0.5}\n"
        "  spritePixelsToUnits: 100\n"
        "  spriteBorder: {x: 0, y: 0, z: 0, w: 0}\n"
        "  spriteGenerateFallbackPhysicsShape: 1\n"
        "  alphaUsage: 1\n"
        "  alphaIsTransparency: 1\n"
        "  spriteTessellationDetail: -1\n"
        "  textureType: 0\n"
        "  textureShape: 1\n"
        "  singleChannelComponent: 0\n"
        "  flipbookRows: 1\n"
        "  flipbookColumns: 1\n"
        "  maxTextureSizeSet: 0\n"
        "  compressionQualitySet: 0\n"
        "  textureFormatSet: 0\n"
        "  ignorePngGamma: 0\n"
        "  applyGammaDecoding: 0\n"
        "  swizzle: 50462976\n"
        "  cookieLightType: 0\n"
        "  platformSettings:\n"
        "  - serializedVersion: 4\n"
        "    buildTarget: DefaultTexturePlatform\n"
        "    maxTextureSize: 128\n"
        "    resizeAlgorithm: 0\n"
        "    textureFormat: -1\n"
        "    textureCompression: 1\n"
        "    compressionQuality: 50\n"
        "    crunchedCompression: 0\n"
        "    allowsAlphaSplitting: 0\n"
        "    overridden: 0\n"
        "    ignorePlatformSupport: 0\n"
        "    androidETC2FallbackOverride: 0\n"
        "    forceMaximumCompressionQuality_BC6H_BC7: 0\n"
        "  spriteSheet:\n"
        "    serializedVersion: 2\n"
        "    sprites: []\n"
        "    outline: []\n"
        "    customData: \n"
        "    physicsShape: []\n"
        "    bones: []\n"
        "    spriteID: \n"
        "    internalID: 0\n"
        "    vertices: []\n"
        "    indices: \n"
        "    edges: []\n"
        "    weights: []\n"
        "    secondaryTextures: []\n"
        "    spriteCustomMetadata:\n"
        "      entries: []\n"
        "    nameFileIdTable: {}\n"
        "  mipmapLimitGroupName: \n"
        "  pSDRemoveMatte: 0\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n",
        encoding="utf-8",
    )


# Stable GUIDs for the eight system icons (do not reshuffle once committed).
GUIDS = {
    "play": "a1b2c3d4e5f6478901234567890ab001",
    "pause": "a1b2c3d4e5f6478901234567890ab002",
    "speed": "a1b2c3d4e5f6478901234567890ab003",
    "follow": "a1b2c3d4e5f6478901234567890ab004",
    "overview": "a1b2c3d4e5f6478901234567890ab005",
    "audio_on": "a1b2c3d4e5f6478901234567890ab006",
    "audio_off": "a1b2c3d4e5f6478901234567890ab007",
    "save": "a1b2c3d4e5f6478901234567890ab008",
}


def main() -> None:
    CANDIDATES.mkdir(parents=True, exist_ok=True)
    RUNTIME_ICONS.mkdir(parents=True, exist_ok=True)

    sheet = Image.new("RGBA", (SIZE * len(ICONS), SIZE), CLEAR)
    for i, (name, fn) in enumerate(ICONS):
        img, draw = new_icon()
        fn(draw)
        sheet.paste(img, (i * SIZE, 0), img)
        out = RUNTIME_ICONS / f"ui_system_{name}_v01.png"
        img.save(out, optimize=True)
        write_meta(out.with_suffix(".png.meta"), GUIDS[name])
        print(f"  {out.name} {img.size} {sha256(out)[:12]}")

    sheet_path = CANDIDATES / "ui_system_icon_sheet_v01.png"
    sheet.save(sheet_path, optimize=True)
    print("sheet", sheet_path, sheet.size, sha256(sheet_path))


if __name__ == "__main__":
    main()
