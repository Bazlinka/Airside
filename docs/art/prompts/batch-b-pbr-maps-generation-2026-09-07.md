# Batch B surface PBR companions generation record

**Date:** 2026-09-07  
**Generator:** Airside procedural Python (seeded `20260907`) via
`scripts/generate-batch-b-pbr-maps.py` (numpy + Pillow)  
**Decision:** 0025 item 4 (coherent URP material library — normals, roughness/mask, AO)  
**Status:** Integrated at runtime via `AirsideMaterialLibrary` (StreamingAssets); Play unverified

Companion maps for the Approved Batch B basecolours. Project-owned; no third-party
texture packs. 1024×1024 tileable PNGs under `Textures/Surfaces/`.

## Delivered files

| Stem | Normal | AO | Mask (R metallic / A smoothness) |
|---|---|---|---|
| `tx_asphalt_runway` | `…_normal_v01.png` | `…_ao_v01.png` | `…_mask_v01.png` |
| `tx_concrete_apron` | same pattern | | |
| `tx_grass_kingscote` | same pattern | | |
| `tx_corrugated_metal` | same pattern | | |

Runtime: `AirsideMaterialLibrary.Create` prefers these maps when
`ArtRuntimePaths` resolves them, else shared procedural 64² fallbacks.

## Method

Height fields match Batch B surface character (asphalt aggregate, soft concrete,
grass detail, corrugated ridges). Normals from finite differences; AO from
recess darkening; mask encodes URP `_MetallicGlossMap` channels. Normals /
AO / masks are non-sRGB; normals marked `textureType: NormalMap` in `.meta`.

## Rights

Project-owned procedural generation. No purchase cost. No attribution.
Fallback: procedural shared maps already in `AirsideMaterialLibrary`.
