# Batch B surfaces generation record

**Date:** 2026-09-06  
**Generator:** Airside procedural Python (seeded `20260906`) via numpy + Pillow  
**Status:** Approved by Bailey 2026-09-06 — surfaces/decals Integrated on greybox  
**Repository paths:** under `game/Airside/Assets/Airside/Art/`

No third-party texture packs were used. Colours target the approved Airside
palette and Batch A material language (REF-001 / REF-002 / REF-005). Surfaces are
2048×2048 tileable PNGs; world kits are metre-scale glTF 2.0 with companion
`.bin` buffers (allowed source format per the art contract).

## Delivered files

| ID | File | Notes |
|---|---|---|
| TEX-SRF-001 | `Textures/Surfaces/tx_asphalt_runway_basecolor_v01.png` | Tarmac `#343B40` family; wrap noise aggregate; no paint |
| TEX-SRF-002 | `Textures/Surfaces/tx_concrete_apron_basecolor_v01.png` | Concrete `#9CA3A2` family; large quiet variation |
| TEX-SRF-003 | `Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png` | Dry Grass / Eucalyptus blend; no flowers |
| TEX-SRF-004 | `Textures/Surfaces/tx_corrugated_metal_basecolor_v01.png` | Vertical corrugation albedo, tintable |
| TEX-ENV-001 | `Textures/Environment/tx_terminal_glass_mask_v01.png` | Pane/mullion greyscale mask; import as non-sRGB |
| TEX-DEC-001 | `Textures/Decals/dc_runway_wear_v01.png` | Transparent rubber streak; no text |
| TEX-DEC-002 | `Textures/Decals/dc_apron_stains_v01.png` | Transparent sparse stains; no text |
| MAT-001 | *(deferred)* | Create `.mat` in Unity 6.3 after import — see `Materials/README_mat_airfield_surface_library_v01.md` |
| WLD-001 | `Models/Props/mdl_airfield_markings_kit_v01.gltf` (+ `.bin`) | Centre/edge/threshold, taxi centre, two stand stops |
| WLD-002 | `Models/Props/mdl_airfield_lighting_kit_v01.gltf` (+ `.bin`) | Edge, taxi, flood, obstruction placeholder meshes |
| WLD-003 | `Models/Props/mdl_airfield_props_kit_v01.gltf` (+ `.bin`) | Windsock pole, cone, barrier, sign, dolly boxes |

Unity `.meta` files with stable GUIDs were written beside each asset. Folder
contract directories under `Art/` were created for later batches.

## Method

Wrapped multi-octave value noise (periodic grid upsample) so left/right and
top/bottom edges match. Verified asphalt edge mean absolute RGB difference
≈ 0.6/255. Decals use straight alpha. Kits are simple precision quads/boxes for
layout and scale review — not final hero LODs.

## Rights, cost and fallback

Project-owned procedural generation for Airside. No purchase cost. No attribution
requirement. Fallback until Integrated: Unity primitives in `AirsidePrototype.cs`.

## Review ask

Bailey approved Batch B on 2026-09-06. Surfaces/decals are Integrated on the
greybox. WLD kits remain Approved for later prefab placement. Request `_v02`
only if Play soak shows tiling or look problems.
