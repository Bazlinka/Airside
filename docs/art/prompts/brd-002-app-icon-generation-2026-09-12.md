# BRD-002 — Airside app icon generation — 2026-09-12

**Status:** Integrated on `main` (Bailey requested generation and merge).  
**Asset:** BRD-002  
**Visual authority:** BRD-001 wordmark mark, approved Airside palette

## BRD-002 — macOS / Standalone app icon

- **Candidate:** `docs/art/candidates/airside_app_icon_v01.png`
- **Runtime:** `game/Airside/Assets/Airside/Art/Brand/airside_app_icon_v01.png`
- **Player Settings:** default Standalone icon via `ProjectSettings.asset`
  `m_BuildTargetIcons` → GUID `d7022c2c4a14457c8d291f16ba58e04a`
- **Generator:** Cursor built-in image generation (ChatGPT / OpenAI image path)
- **Dimensions:** 1024×1024 RGB
- **SHA-256:** `b544d454da56a4637b8b0b05f2d0b0dca23ed1be3fff06f8c95a09108e2041bb`
- **Input reference:** BRD-001 `airside_wordmark_light_v01.png` (runway-A mark only)
- **Edit chain:** one reference-guided 1:1 generation; no crop, inpaint or
  palette remap after generation
- **Cost:** no per-asset cost surfaced by the built-in generator
- **Attribution:** none known
- **Fallback:** Unity default application icon

### Exact prompt

```text
Square macOS/iOS-style app icon for the premium airport-management game Airside. Center the existing brand mark only: a tall stylized letter-A triangle in Coastal Blue #39708A with a white runway centreline strip and three small Safety Yellow #F2C14B threshold bars inside it. Place that mark large and centered on a solid rounded-square-friendly field of Runway Ink #17242A (deep blue-black), filling most of the canvas with generous but not sparse margins so it reads at 16–32 px. Soft subtle inner vignette only; no gloss, no skeuomorphic bevel, no drop shadow outside the square, no photoreal lighting. Flat premium wayfinding graphic, clean vector-like edges, calm Australian regional airport identity. Absolutely no text, no wordmark letters, no AIRSIDE spelling, no aircraft silhouette, no runway photo, no UI chrome, no watermark, no signature, no real airline logos or trademarks. Exact brand colours from the reference mark. Output a full-bleed square icon suitable as a desktop game application icon.
```

## Review checks

- Square 1024×1024, no baked title text.
- Mark matches BRD-001 runway-A geometry language (triangle + centreline + three
  yellow threshold bars).
- Palette stays in Runway Ink / Coastal Blue / Cloud / Safety Yellow.
- Wired as the default Player icon for Standalone builds.
- Packaged Mac Dock / Finder appearance still needs a Unity Mac build to verify.
