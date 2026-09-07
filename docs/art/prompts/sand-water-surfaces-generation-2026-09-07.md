# Sand + water coastal surface generation — 2026-09-07

Decision **0025** item 4. Project-owned procedural maps for the KI coastal
strip so sand and water no longer read as flat untextured slabs.

## Generator

`scripts/generate-sand-water-surfaces.py` — 256² tileable PNGs (kept small for
agent regenerability; distant coast does not need 1024).

## Outputs (`Textures/Surfaces/`)

| Stem | basecolour | normal | AO | mask (metallic/smooth) |
|---|---|---|---|---|
| `tx_sand_coast` | `…_basecolor_v01.png` | `…_normal_v01.png` | `…_ao_v01.png` | `…_mask_v01.png` |
| `tx_water_coast` | `…_basecolor_v01.png` | `…_normal_v01.png` | `…_ao_v01.png` | `…_mask_v01.png` |

## Runtime wiring

- `AirsideMaterialLibrary.AuthoredStemByKind` maps `Sand` → `tx_sand_coast`,
  `Water` → `tx_water_coast`.
- Coast sand / dunes / shallows / water `CreateBlock` calls pass the basecolour
  paths so albedo + authored PBR companions attach via `InferFromTexturePath`.
- Synced to StreamingAssets via `scripts/sync-art-streaming-assets.sh`.

## Licence

Project-owned. No third-party packs.
