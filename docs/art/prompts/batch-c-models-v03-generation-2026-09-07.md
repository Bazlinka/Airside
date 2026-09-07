# Batch C v03 — denser procedural kits

**Date:** 2026-09-07  
**Generator:** `scripts/generate-batch-c-models-v03.py`  
**Decision:** 0025 item 2 (replace placeholder 3D set)  
**Format:** Same POSITION+indices box glTF kits as v01/v02 (`ArtGltfLoader`)

## What changed vs v02

| Kit | Extra readable parts |
|---|---|
| Turboprop | Fuselage aft + belly, flaps, ailerons, elevators, exhausts, cargo door, nav/landing lights, beacon |
| Terminal | Window mullions, extra canopy posts, entrance frame, columns, signage, second plant, landside awning |
| Hangar | Door panels, roof ribs, office lean-to + windows |
| Ops shed | Side window, roof panel, AC unit, antenna mast |
| Fuel / bag / bus | Hose reel, mirrors, bumpers, steps, beacons, extra wheels |

Runtime prefers `*_v03` → `*_v02` → `*_v01` via `PreferArtKit`. Re-run
`scripts/sync-art-streaming-assets.sh` after regenerating.

## Honesty

Still procedural greybox boxes — not authored meshes or PBR. Improves part
count and control-surface motion toward the REF target without claiming
screenshot parity. Production path remains Unity prefabs / Addressables (ADR 0026).
