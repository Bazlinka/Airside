# Batch F2 vehicles + characters — generation evidence

**Date:** 2026-09-07  
**Generator:** `scripts/generate-batch-f2-vehicles-characters.py` +
`scripts/generate-batch-f2-prefabs.py`  
**Decision / packet:** 0027 /
`docs/art/prompts/batch-f-first-playable-visual-assets-task-packet.md`

## Deliverables

| ID | Basename | Meshes | Notes |
|---|---|---|---|
| VEH-001 v05 | `mdl_fuel_truck_small_v05` | 91 | Oval lathe tank, hose reel/coils, Safety Yellow chevrons, separated doors/wheels |
| VEH-002 v05 | `mdl_baggage_tug_train_v05` | 99 | ROPS cab, three carts with hitch pivots + removable cargo bags |
| VEH-003 v05 | `mdl_passenger_bus_apron_v05` | 96 | Rounded nose/tail cylinders, Coastal Blue stripes, dual-side glazing |
| VEH-004 | `mdl_pushback_tug_v02` | 32 | Dedicated tug + towbar assembly (replaces towbar-only kit / v01 proof) |
| CHR-001 | `mdl_ramp_crew_kit_v01` | 32 | marshaller / fueler / ramp with hi-vis + wand sockets |
| CHR-002 | `mdl_passenger_kit_v01` | 42 | six silhouettes: stand_a/b, walk_c/d, sit_e/f |

FBX via ASCII exporter (assimp unavailable in cloud). Companion glTF + `.bin`
synced to StreamingAssets. Pipeline-proof Resources prefabs emit for
`airside-prefab/<key>`; Mac bake menu includes the new FBX paths.

## Runtime preference

PreferArtKit: `*_v05` → authored_v01 → v04… for service vehicles.
Pushback: `mdl_pushback_tug_v02` → v01 prefab → service-equipment towbar parts.
PlacePerson: CHR kits first; block figures remain fallback. Motion names
(Hose/Cargo/Door/wheel/torso/wand/arm/leg) preserved.

## Licence

Project-owned procedural. No third-party packs. Cost: $0.
