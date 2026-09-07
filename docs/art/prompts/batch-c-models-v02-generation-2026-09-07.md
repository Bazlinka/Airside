# Batch C v02 — richer procedural kits

**Date:** 2026-09-07  
**Generator:** `scripts/generate-batch-c-models-v02.py`  
**Decision:** 0025 item 2 (replace placeholder 3D set)  
**Format:** Same POSITION+indices box glTF kits as v01 (`ArtGltfLoader`)

## What changed vs v01

| Kit | Extra readable parts |
|---|---|
| Turboprop | Cockpit, cabin windows, wingtip, nacelles, cross prop blades + spinners, rudder, tires, antenna |
| Terminal | Roof slab, canopy + posts, entrance, service door, roof plant |
| Hangar | Door tracks, roof panels, buttresses, side vent |
| Ops shed | Porch roof, door, windows, roof ridge |
| Fuel truck | Cab window, tank band, mirrors, beacon |
| Baggage train | Tug cab, cargo blocks, wheels |
| Apron bus | Windows, bumpers, beacon |

v01 Approved files are **not** overwritten. Runtime prefers `*_v02` via
`PreferArtKit`, then falls back to `*_v01`. Re-run
`scripts/sync-art-streaming-assets.sh` after regenerating.

## Honesty

Still procedural greybox — not authored meshes or URP materials. Improves
silhouette readability toward the REF target without claiming screenshot parity.
