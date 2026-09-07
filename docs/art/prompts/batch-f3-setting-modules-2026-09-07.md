# Batch F3 setting modules — generation evidence

**Date:** 2026-09-07  
**Generator:** `scripts/generate-batch-f3-setting-modules.py` +
`scripts/generate-batch-f3-prefabs.py`  
**Decision / packet:** 0027 /
`docs/art/prompts/batch-f-first-playable-visual-assets-task-packet.md`

## Deliverables

| ID | Basename | Meshes | Notes |
|---|---|---|---|
| VEG-001 | `mdl_eucalyptus_kit_v01` | 30 | tree_a/b/c silhouettes with trunk/flare/fork/canopy (+ LOD1) |
| VEG-002 | `mdl_kingscote_scrub_kit_v01` | 24 | scrub_a–e clusters + rock + grass tufts |
| PRP-002 | `mdl_airfield_fence_gate_kit_v01` | 24 | Modular bay/corner/end + vehicle/pedestrian gate panels |
| PRP-003 | `mdl_terminal_forecourt_kit_v01` | 18 | Kerb, bollard, planter, bench, sign, trolley rail |
| WLD-004 | `mdl_kingscote_context_terrain_v01` | 13 | Paddock/coast/hill/dune accents — no operational geometry |

FBX via ASCII exporter. Companion glTF synced to StreamingAssets. Pipeline-proof
Resources prefabs for `airside-prefab/<key>`.

## Runtime wiring

- `PlaceTree` / `PlaceShrubClump` prefer VEG kits; primitives remain fallback
- `BuildPerimeterFence` prefers PRP-002 modular bays; CreateBlock ribbon fallback
- `BuildTerminalLandsideCanopy` prefers PRP-003 forecourt pieces
- `BuildDistantHills` places WLD-004 hill/dune accents (runway/apron unchanged)

## Licence

Project-owned procedural. Cost: $0.
