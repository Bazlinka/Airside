# WLD/PRP denser kits v02 — generation record

**Date:** 2026-09-07  
**Generator:** `scripts/generate-wld-prp-kits-v02.py`  
**Decision:** 0025 item 2  
**Status:** Integrated (prefer v02→v01; Play unverified)

## Why

Batch B WLD/PRP kits were still 4–7 single boxes while Batch C heroes sit at
dozens of meshes. Overview/apron clutter gains more from denser lighting masts
and props than another turboprop segment pass.

## Outputs (`Models/Props/`)

| Kit | Notes |
|---|---|
| `mdl_airfield_lighting_kit_v02` | Legacy names + multi-part edge/taxi/flood/obst |
| `mdl_airfield_props_kit_v02` | Legacy names + denser sock/cone/barrier/sign/dolly |
| `mdl_service_equipment_kit_v02` | Legacy stairs/gpu/chocks + denser stairs/GPU parts |

Does not overwrite v01. StreamingAssets synced via
`scripts/sync-art-streaming-assets.sh`.

## Runtime

`PreferArtKit` / `PlaceWorldLighting` / props and service fallbacks prefer v02
first. Flood masts place `flood_base`/`flood_pole`/`flood_arm`/`flood_head`/
`flood_lamp` when present.
