# Batch C models generation record

**Date:** 2026-09-06  
**Generator:** Airside procedural Python (metre-scale box kits + RGBA livery atlases)  
**Status:** Approved by Bailey 2026-09-06 — ready for Integration  
**Repository paths:** under `game/Airside/Assets/Airside/Art/`

No third-party model packs. Kits follow Batch A REF-001/003/005 proportions and the
Batch C task packet. Runtime presentation also gained richer primitive stand-ins
(props, engines, gear, terminal caps, service vehicles) with colour fallback; glTF
files are the durable art sources for Unity import.

## Delivered files

| ID | File | Notes |
|---|---|---|
| AIR-001 | `Models/Aircraft/mdl_regional_turboprop_01_v01.gltf` (+ `.bin`) | Separated fuselage, wings, engines, props, gear, door, tail |
| AIR-002 | `Textures/Decals/dc_livery_coastline_regional_v01.png` | Blue/coastal fictional atlas |
| AIR-003 | `Textures/Decals/dc_livery_emu_air_v01.png` | Ochre/gold fictional atlas |
| AIR-004 | `Textures/Decals/dc_livery_airside_traffic_v01.png` | Neutral traffic atlas |
| BLD-001 | `Models/Buildings/mdl_terminal_regional_small_v01.gltf` | Body, glass, end caps, service wing |
| BLD-002 | `Models/Buildings/mdl_hangar_small_v01.gltf` | Shell, door opening, ridge |
| BLD-003 | `Models/Buildings/mdl_operations_shed_v01.gltf` | Shed + porch |
| VEH-001 | `Models/Vehicles/mdl_fuel_truck_small_v01.gltf` | Cab, tank, wheels, hose mount |
| VEH-002 | `Models/Vehicles/mdl_baggage_tug_train_v01.gltf` | Tug + three carts + hitches |
| VEH-003 | `Models/Vehicles/mdl_passenger_bus_apron_v01.gltf` | Body, roof, door, wheels |
| PRP-001 | `Models/Props/mdl_service_equipment_kit_v01.gltf` | Stairs, chocks, cone, towbar, bin, GPU |

glTF is an allowed source format per the art contract.

## Rights, cost and fallback

Project-owned procedural generation. No purchase cost. Fallback: richer Unity
primitives in `AirsidePrototype.cs` until Integration/Verified.
