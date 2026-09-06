# Batch C — first-playable 3D set

**Status:** Ready for production (task packet; models not yet delivered)  
**Date:** 2026-09-06  
**Decision / contract:** `docs/decisions/0022-art-direction-and-asset-pipeline.md`,
`docs/art/ART_DIRECTION_AND_ASSET_SPEC.md`  
**Visual authority:** Approved Batch A references (REF-001/002/003/005). Do **not**
redesign aircraft, buildings, vehicles, liveries, palette or proportions from those
refs. Batch B Approved surfaces are the ground context these models sit on.

## Player-visible outcome

When Batch C is later Integrated, greybox aircraft, terminal, hangar and service
vehicles are replaced by readable metre-scale 3D models that match the approved
miniature look — while simulation behaviour and primitive fallbacks stay until
each asset is Verified.

This packet authorises **modelling, review and registration**. It does **not**
authorise removing `CreatePrimitive` aircraft/building fallbacks.

## Scope

| In scope | Out of scope |
|---|---|
| AIR-001…004 aircraft + livery decals | Batch D animation / VFX |
| BLD-001…003 terminal, hangar, ops shed | Batch B rework (already Approved) |
| VEH-001…003 fuel truck, baggage train, bus | Brand wordmark / splash |
| PRP-001 service equipment kit | Simulation / Domain / saves |
| Prompt/evidence + asset register rows | Real airline marks, watermarks, signatures |
| | Runtime Integration (separate PR) |

Runtime paths are relative to `game/Airside/Assets/Airside/Art/`.

## Deliverables (exact manifest paths)

| ID | Exact path | Requirement |
|---|---|---|
| AIR-001 | `Models/Aircraft/mdl_regional_turboprop_01_v01.fbx` | Fictional twin turboprop; gear, propellers, doors and control surfaces **separated**; material variants for primary + traffic |
| AIR-002 | `Textures/Decals/dc_livery_coastline_regional_v01.png` | Fictional blue/coastal identity; transparent atlas; no real airline resemblance |
| AIR-003 | `Textures/Decals/dc_livery_emu_air_v01.png` | Fictional ochre/gold identity; no real airline resemblance |
| AIR-004 | `Textures/Decals/dc_livery_airside_traffic_v01.png` | Neutral traffic livery for GT-201/GT-202 |
| BLD-001 | `Models/Buildings/mdl_terminal_regional_small_v01.fbx` | Small practical terminal; glass frontage; service side; modular end caps |
| BLD-002 | `Models/Buildings/mdl_hangar_small_v01.fbx` | Corrugated metal hangar; readable door opening |
| BLD-003 | `Models/Buildings/mdl_operations_shed_v01.fbx` | Compact service/crew building; visual support only |
| VEH-001 | `Models/Vehicles/mdl_fuel_truck_small_v01.fbx` | Cab, wheels, hose connection separated |
| VEH-002 | `Models/Vehicles/mdl_baggage_tug_train_v01.fbx` | Tug + three low-detail carts; articulation points |
| VEH-003 | `Models/Vehicles/mdl_passenger_bus_apron_v01.fbx` | Compact apron bus; doors and wheels separated |
| PRP-001 | `Models/Props/mdl_service_equipment_kit_v01.fbx` | Stairs, chocks, cones, towbar, bins, GPU |

glTF is acceptable as a modelling source if Unity import is documented; promote to
the manifest FBX path (or update the manifest in the same commit) before Approved.

## Style rules

- Match REF-001 / REF-003 / REF-005 silhouettes, proportions and palette.
- **3D models only** for aircraft, buildings and vehicles — images are references.
- Metres; clean topology; named materials; prefer shared atlases over unique 4K maps.
- No real airline branding, watermarks, signatures or living-artist imitation.
- Fictional airlines in sim: Coastline Regional, Emu Air, Southern Cross Link,
  Gulf Connect, Redgum Air.
- snake_case `_v01` names; bump version for a visibly different candidate.

## Evidence and register

1. Add evidence under `docs/art/prompts/` (DCC/tool, date, poly budget, edit chain).
2. Add a row to `docs/data/ASSET_AND_DATA_REGISTER.md`.
3. Manifest: `Generated/Modelled` → `Review` → `Approved` only after Bailey accepts.
4. Fallback until Integrated: primitives in `AirsidePrototype.cs`.

## Acceptance criteria

- [ ] Every ID exists at the exact path (or path updated in the same commit).
- [ ] AIR-001 has separated moving parts; liveries are material/decal variants.
- [ ] Buildings read clearly from elevated three-quarter overview.
- [ ] Vehicles match REF-003 service zones.
- [ ] Scale matches REF-005 (~1.7 m human reference).
- [ ] Register + prompt evidence committed with the files.
- [ ] No Domain/Simulation/Persistence behaviour change in this batch.

## Must remain unchanged

- Approved Batch A refs and Batch B Approved/Integrated surfaces.
- Deterministic simulation, clock, seeded random, reservations, saves.
- Primitive fallbacks until a later Integration task wires Approved models.

## Handoff after this packet

1. Produce models (aircraft + terminal first is fine).
2. Review → Approved.
3. Separate narrow PR: Unity import + presentation wiring with fallbacks.
4. Then Batch D animation/VFX.
