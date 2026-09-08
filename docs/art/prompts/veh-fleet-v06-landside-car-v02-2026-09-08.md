# Vehicle fleet v06 + landside car v02 — generation notes

**Date:** 2026-09-08  
**Authority:** REF-001 / REF-003 / REF-005 + `docs/art/ART_DIRECTION_AND_ASSET_SPEC.md`  
**Scripts:** `scripts/generate-veh-fleet-v06.py`, `scripts/generate-veh-fleet-v06-prefabs.py`

## Deliverables

| ID | Basename | Meshes | Notes |
|---|---|---|---|
| Landside car | `mdl_parked_car_v02` | 49 | Lofted body along Z; separate glass, bumpers, wheels/hubs |
| VEH-001 | `mdl_fuel_truck_small_v06` | 103 | Denser oval tank, cab fairing, mid axle, hose pivot |
| VEH-002 | `mdl_baggage_tug_train_v06` | 108 | Open ROPS cab, Coastal Blue cargo crates + lids |
| VEH-003 | `mdl_passenger_bus_apron_v06` | 97 | Two-tone body (`bus_body` / `bus_body_upper`), ribbon glass |
| VEH-004 | `mdl_pushback_tug_v03` | 35 | Extra glass panes, mid towbar pivot, heavier counterweight |

ASCII FBX uses `UnitScaleFactor=100` (metres). StreamingAssets synced via
`scripts/sync-art-streaming-assets.sh`. Resources prefabs are pipeline-proof
Cube/Cylinder hierarchies; Mac FBX bake via `Airside/Art/Bake Authored FBX Prefabs`.

## PreferArtKit order

- Fuel / baggage / bus: `*_v06` → `*_v05` → authored → v04…v01
- Pushback: `mdl_pushback_tug_v03` → v02 → v01 prefab → service-equipment towbar
- Parked car: `mdl_parked_car_v02` → v01 prefab → procedural cuboids

## Materials

`AirsideMaterialLibrary.InferFromMeshName` + `AirsideRuntimeMaterialBinder` map
wheel→Rubber, glass→Glass, cab/body/car_*→PaintedMetal/AircraftSkin, hubs/bumpers/
hose→Metal. UV-less meshes use `useTextures: false` (flat Lit, no black texel).

## Unchanged

Simulation, reservations, save schema, economy, companion, UI icons.
