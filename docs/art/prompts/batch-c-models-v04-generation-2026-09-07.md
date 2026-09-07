# Batch C v04 denser kits — generation record

**Date:** 2026-09-07  
**Generator:** `scripts/generate-batch-c-models-v04.py`  
**Decision:** 0025 item 2 (replace placeholder 3D / denser procedural kits)  
**Status:** Integrated (prefer v04→v03→v02→v01; Play unverified)

## What

Sibling `*_v04.gltf` kits with more segmented parts than v03 for silhouette
readability. Still procedural greybox cuboids — not authored FBX.

| Kit | Meshes (approx) |
|---|---|
| `mdl_regional_turboprop_01_v04` | 68 (winglets, gear doors, spoilers, window panes, taxi light) |
| `mdl_terminal_regional_small_v04` | 31 (extra mullions/columns, landside glass, baggage door) |
| `mdl_hangar_small_v04` | 26 (crane beam, more ribs, personnel door) |
| `mdl_operations_shed_v04` | 15 (antenna dish, second AC, radio rack) |
| `mdl_fuel_truck_small_v04` | 19 |
| `mdl_baggage_tug_train_v04` | 20 |
| `mdl_passenger_bus_apron_v04` | 19 |

## Runtime

`PreferArtKit` prefers v04 first. StreamingAssets synced via
`scripts/sync-art-streaming-assets.sh`. Motion code keys on existing name
prefixes; spoilers deploy on landing.

## Licence

Project-owned procedural; no third-party pack.
