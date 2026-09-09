# First-playable art sourcing checklist

**Status:** Active refine pass (2026-09-09)  
**Authority:** `docs/art/ART_DIRECTION_AND_ASSET_SPEC.md` + `docs/data/ASSET_AND_DATA_REGISTER.md`  
**Goal:** Source / author every first-playable visual and audio part, commit it to
git with a register row, and replace procedural placeholder kits until overview
and follow match REF-001 / REF-003 / REF-005.

## How to use this list

| Column | Meaning |
|---|---|
| **ID** | Manifest / register ID (or `PART-*` for a named mesh inside a kit) |
| **Item** | What to source or author |
| **In git?** | File(s) already under `Art/` / Resources / StreamingAssets |
| **Quality** | `OK` usable · `Placeholder` densified procedural · `Missing` no real asset · `Skip` later backlog |
| **Source from** | Suggested production route |
| **Target path** | Exact runtime / docs path (relative to `game/Airside/Assets/Airside/Art/` unless noted) |

**Priority order for Bailey sourcing:** P0 aircraft parts → P0 buildings → P1
vehicles/GSE wheels & connections → P1 vegetation/characters → P2 materials/
animation clips → P3 audio.

**Rules (do not skip):**
1. No real airline logos, trademarks, watermarks or signatures.
2. Metres, ground pivots, named children (`propeller_*`, `wheel_*`, `gear_*`,
   `hose_root`, `door_*`, `light_*`).
3. Register every new file in `docs/data/ASSET_AND_DATA_REGISTER.md` in the same
   commit.
4. Keep primitive / previous-kit fallback until Mac Play verifies the replacement.
5. Sync filesystem kits with `scripts/sync-art-streaming-assets.sh`.

---

## P0 — Hero aircraft (AIR-001)

| ID | Item | In git? | Quality | Source from | Target path |
|---|---|---|---|---|---|
| AIR-001 | Regional high-wing twin turboprop (hero mesh) | Yes (`v06` preferred) | Placeholder | Authored FBX / Blender / kitbash cleaned to REF-003/005 | `Models/Aircraft/mdl_regional_turboprop_01_v07.fbx` (+ glTF + Resources prefab) |
| PART-AIR-FUSE | Continuous fuselage / body | Inside AIR-001 | Placeholder | Same hero authoring pass | child under AIR-001 |
| PART-AIR-WING | High wing + tips | Inside AIR-001 | Placeholder | Same | child under AIR-001 |
| PART-AIR-ENG-L/R | Nacelles / engines ×2 | Inside AIR-001 | Placeholder | Same — separate spin roots | `engine_left` / `engine_right` |
| PART-AIR-PROP-L/R | Propellers — **6 twisted blades each**, spin pivot | Inside AIR-001 | Placeholder — **critical** | Authored blades + hub; must spin clean at overview | `propeller_left` / `propeller_right` |
| PART-AIR-GEAR | Nose + main gear legs/wheels + gear doors | Inside AIR-001 | Placeholder — **critical** | Separate pivots; retract-friendly | `gear_*`, `gear_door_*`, `wheel_*` |
| PART-AIR-DOOR | Cabin / cargo doors | Inside AIR-001 | Placeholder | Hinge pivots for stand open/close | `door_fwd`, `cargo_door` |
| PART-AIR-GLASS | Cockpit windscreen / cabin windows | Inside AIR-001 | Placeholder | Glass material slots | named glass meshes |
| PART-AIR-CTRL | Flaps / aileron / elevator / rudder (follow life) | Partial in denser kits | Placeholder | Optional for first playable follow | control-surface children |
| PART-AIR-LIGHT | Nav / beacon / landing / taxi sockets | Partial | Placeholder | Emissive sockets, not baked beams | `light_*` |
| AIR-002 | Coastline Regional livery atlas | Yes | OK | Keep / refine fictional atlas | `Textures/Decals/dc_livery_coastline_regional_v01.png` |
| AIR-003 | Emu Air livery atlas | Yes | OK | Keep / refine fictional atlas | `Textures/Decals/dc_livery_emu_air_v01.png` |
| AIR-004 | Ground-traffic neutral livery | Yes | OK | Keep / refine | `Textures/Decals/dc_livery_airside_traffic_v01.png` |
| TEX-AIR-SKIN | Aircraft skin PBR (base/normal/AO/mask) | Yes | Placeholder | Hand-paint or authored maps | `Textures/Surfaces/tx_aircraft_skin_*` |

## P0 — Buildings (BLD + ARFF shed)

| ID | Item | In git? | Quality | Source from | Target path |
|---|---|---|---|---|---|
| BLD-001 | Small regional terminal | Yes (`v05`) | Placeholder | Authored modular terminal vs REF-001/005 + building board | `Models/Buildings/mdl_terminal_regional_small_v06.fbx` |
| BLD-002 | Small hangar (sliding doors) | Yes (`v05`) | Placeholder | Authored corrugated hangar | `Models/Buildings/mdl_hangar_small_v06.fbx` |
| BLD-003 | Operations / crew shed | Yes (`v05`) | Placeholder | Authored shed + porch | `Models/Buildings/mdl_operations_shed_v06.fbx` |
| BLD-ARFF | ARFF / rescue open-bay shed | Yes (`mdl_arff_shed_v02`) | Placeholder | Authored vs ARFF fidelity board | `Models/Buildings/mdl_arff_shed_v03.fbx` (or Props) |
| TEX-ENV-001 | Terminal glass variation mask | Yes | OK | Keep / light refine | `Textures/Environment/tx_terminal_glass_mask_v01.png` |

## P1 — Turnaround vehicles (VEH)

| ID | Item | In git? | Quality | Source from | Target path |
|---|---|---|---|---|---|
| VEH-001 | Fuel truck | Yes (`v06`) | Placeholder | Authored rigid tanker vs REF-003 | `Models/Vehicles/mdl_fuel_truck_small_v07.fbx` |
| PART-FUEL-WHEEL | Fuel truck wheels (all axles) | Inside VEH-001 | Placeholder — **critical** | Separate roll pivots | `wheel_*` |
| PART-FUEL-HOSE | Hose reel + hose root | Inside VEH-001 | Placeholder — **critical** | Deployable hose for service loop | `hose_root` |
| VEH-002 | Baggage tug + 3 carts | Yes (`v06`) | Placeholder | Authored tug/train vs REF-003 | `Models/Vehicles/mdl_baggage_tug_train_v07.fbx` |
| PART-BAG-WHEEL | Tug + cart wheels | Inside VEH-002 | Placeholder — **critical** | Separate pivots | `wheel_*` |
| PART-BAG-HITCH | Hitches / articulations | Inside VEH-002 | Placeholder | Pivot-friendly hitches | hitch children |
| PART-BAG-LOAD | Removable cargo crates | Inside VEH-002 | Placeholder | Bob/service readable loads | crate meshes |
| VEH-003 | Apron passenger bus | Yes (`v06`) | Placeholder | Authored bus vs REF-003 | `Models/Vehicles/mdl_passenger_bus_apron_v07.fbx` |
| PART-BUS-WHEEL | Bus wheels | Inside VEH-003 | Placeholder — **critical** | Separate roll pivots | `wheel_*` |
| PART-BUS-DOOR | Bus door(s) | Inside VEH-003 | Placeholder | Open/close for boarding | `door_*` |
| VEH-004 | Pushback tug | Yes (`v03`) | Placeholder | Authored tug + towbar | `Models/Vehicles/mdl_pushback_tug_v04.fbx` |
| PART-PUSH-WHEEL | Pushback wheels | Inside VEH-004 | Placeholder — **critical** | Separate pivots | `wheel_*` |
| PART-PUSH-BAR | Towbar pivot | Inside VEH-004 | Placeholder — **critical** | Connects to nose gear | towbar child |
| VEH-CAR | Landside parked car | Yes (`mdl_parked_car_v02`) | Placeholder | Authored compact car | `Models/Vehicles/mdl_parked_car_v03.fbx` |
| VEH-ARFF | ARFF / rescue truck | Yes (`mdl_arff_truck_v02`) | Placeholder | Authored vs ARFF board | `Models/Vehicles/mdl_arff_truck_v03.fbx` |

## P1 — Stand GSE & props (PRP)

| ID | Item | In git? | Quality | Source from | Target path |
|---|---|---|---|---|---|
| PRP-001 | Service equipment kit (master) | Yes (`v03`) | Placeholder | Authored kit vs turnaround board | `Models/Props/mdl_service_equipment_kit_v04.fbx` |
| PART-STAIRS | Passenger stairs | Yes (`mdl_passenger_stairs_v02`) | Placeholder — **critical** | Authored stairs; tip stops at door | `Models/Props/mdl_passenger_stairs_v03.fbx` |
| PART-GPU | GPU / ground power unit | Inside PRP-001 | Placeholder | Authored cart clear of prop disc | GPU mesh in kit |
| PART-BELT | Belt loader | Inside PRP-001 | Placeholder | Authored loader | belt mesh in kit |
| PART-CHOCK | Wheel chocks | Inside PRP-001 | Placeholder | Authored pair | chock meshes |
| PART-CONE | Safety cones | Inside PRP-001 / WLD-003 | Placeholder | Authored cones | cone meshes |
| PART-TOWBAR | Stand towbar | Inside PRP-001 | Placeholder | Authored bar | towbar mesh |
| PART-BIN | Service / FOD bins | Yes (kit + `mdl_fod_bin_v01`) | Placeholder | Authored bins | kit / `mdl_fod_bin_v02` |
| PRP-HYDRANT | Fire hydrant | Yes (`mdl_fire_hydrant_v01`) | Placeholder | Authored hydrant | `mdl_fire_hydrant_v02` |
| PRP-EXT | Extinguisher cabinet | Yes (`mdl_extinguisher_cabinet_v01`) | Placeholder | Authored cabinet | `mdl_extinguisher_cabinet_v02` |
| PRP-002 | Fence / gate kit | Yes (`v02`) | Placeholder | Authored chain-link modules | `Models/Props/mdl_airfield_fence_gate_kit_v03.fbx` |
| PRP-003 | Terminal forecourt kit | Yes (`v02`) | Placeholder | Authored bench/trolley/planter/bollard/kerb/sign | `Models/Props/mdl_terminal_forecourt_kit_v03.fbx` |
| PART-BENCH | Landside bench | In PRP-003 / prefab | Placeholder | Authored | bench mesh |
| PART-TROLLEY | Luggage trolley | In PRP-003 / prefab | Placeholder | Authored | trolley mesh |
| PART-BOAT | Coast boat (context) | Yes (`mdl_coast_boat_v01`) | Placeholder | Optional polish | `mdl_coast_boat_v02` |

## P1 — World kits (WLD)

| ID | Item | In git? | Quality | Source from | Target path |
|---|---|---|---|---|---|
| WLD-001 | Airfield markings kit | Yes | Placeholder | Precision geometry (not AI text) | `Models/Props/mdl_airfield_markings_kit_v02.fbx` |
| WLD-002 | Airfield lighting kit | Yes (`v02`/authored) | Placeholder | Edge / taxi / flood / obstruction | `Models/Props/mdl_airfield_lighting_kit_v03.fbx` |
| WLD-003 | Airfield props kit | Yes (`v02`/authored) | Placeholder | Windsock, barriers, signs, dollies | `Models/Props/mdl_airfield_props_kit_v03.fbx` |
| PART-WINDSOCK | Windsock | Inside WLD-003 | Placeholder | Cloth-readable sock + pole | windsock mesh |
| WLD-004 | Context terrain modules | Yes (`v02`) | Placeholder | Authored berms/hills/dunes/water shelves | `Models/Environment/mdl_kingscote_context_terrain_v03.fbx` |

## P1 — Vegetation (VEG)

| ID | Item | In git? | Quality | Source from | Target path |
|---|---|---|---|---|---|
| VEG-001 | Eucalyptus kit (trees) | Yes (`v02`) | Placeholder — **critical** | Authored trunks + canopy LOD0/1 vs REF setting | `Models/Environment/mdl_eucalyptus_kit_v03.fbx` |
| PART-TREE-TRUNK | Tree trunks / forks | Inside VEG-001 | Placeholder | Bark readable at overview | trunk meshes |
| PART-TREE-CANOPY | Canopy / leaf clusters + LOD | Inside VEG-001 | Placeholder | Cutout foliage, not photo billboards | canopy meshes |
| VEG-002 | Coastal scrub kit | Yes (`v02`) | Placeholder — **critical** | Authored scrub/grass/rock vs scrub sheet | `Models/Environment/mdl_kingscote_scrub_kit_v03.fbx` |
| PART-SCRUB | Scrub / mallee forms | Inside VEG-002 | Placeholder | Multiple silhouettes | scrub meshes |
| PART-GRASS | Grass tufts | Inside VEG-002 | Placeholder | Clumps for fence/dune edges | grass meshes |
| PART-ROCK | Rock groups | Inside VEG-002 | Placeholder | Coastal rocks | rock meshes |

## P1 — Characters (CHR)

| ID | Item | In git? | Quality | Source from | Target path |
|---|---|---|---|---|---|
| CHR-001 | Ramp crew kit | Yes (`v02`) | Placeholder | Authored marshaller/fueler/ramp vs silhouette sheet | `Models/Characters/mdl_ramp_crew_kit_v03.fbx` |
| PART-WAND | Marshalling wands | Inside CHR-001 | Placeholder | Wand sockets for dual-wand pose | wand meshes |
| CHR-002 | Passenger silhouette kit | Yes (`v02`) | Placeholder | Authored stand/walk/sit set | `Models/Characters/mdl_passenger_kit_v03.fbx` |

## P2 — Surfaces & materials (TEX / MAT)

| ID | Item | In git? | Quality | Source from | Target path |
|---|---|---|---|---|---|
| TEX-SRF-001 | Asphalt runway base + PBR | Yes (`v01`/`v02`) | Placeholder | Hand-authored tileables vs surface board | `Textures/Surfaces/tx_asphalt_runway_*` |
| TEX-SRF-002 | Concrete apron base + PBR | Yes | Placeholder | Same | `Textures/Surfaces/tx_concrete_apron_*` |
| TEX-SRF-003 | Grass Kingscote base + PBR | Yes | Placeholder | Same | `Textures/Surfaces/tx_grass_kingscote_*` |
| TEX-SRF-004 | Corrugated metal base + PBR | Yes | Placeholder | Same | `Textures/Surfaces/tx_corrugated_metal_*` |
| TEX-WET | Wet concrete variant | Yes (`v02`) | Placeholder | Cool sheen map | `Textures/Surfaces/tx_wet_concrete_*` |
| TEX-SAND | Coast sand PBR | Yes | Placeholder | Same family | `Textures/Surfaces/tx_sand_coast_*` |
| TEX-WATER | Coast water PBR | Yes | Placeholder | Same family | `Textures/Surfaces/tx_water_coast_*` |
| TEX-DEC-001 | Runway wear decal | Yes | OK | Keep / light refine | `Textures/Decals/dc_runway_wear_v01.png` |
| TEX-DEC-002 | Apron stains decal | Yes | OK | Keep / light refine | `Textures/Decals/dc_apron_stains_v01.png` |
| MAT-001 | URP Lit material family (8) | Yes | Placeholder | Tune roughness/normal/glass/wet in Editor | `Materials/mat_*_v01.mat` (+ Resources) |

## P2 — Animation clips (runtime exists; authored files thin)

| ID | Item | In git? | Quality | Source from | Target path |
|---|---|---|---|---|---|
| ANM-AIR-001 | Propeller spin clip | Runtime only | Missing `.anim` | Unity clip from AIR-001 pivots | `Animation/Aircraft/anm_propeller_spin_v01.anim` |
| ANM-AIR-002 | Gear deploy/retract clip | Runtime only | Missing `.anim` | Unity clip | `Animation/Aircraft/anm_gear_cycle_v01.anim` |
| ANM-AIR-003 | Cabin door cycle clip | Runtime only | Missing `.anim` | Unity clip | `Animation/Aircraft/anm_cabin_door_cycle_v01.anim` |
| ANM-AIR-004 | Aircraft lights controller | Runtime only | Missing controller | Animator controller | `Animation/Aircraft/anm_aircraft_lights_v01.controller` |
| ANM-VEH-001 | Vehicle wheel spin clip | Runtime only | Missing `.anim` | Unity clip from `wheel_*` | `Animation/Vehicles/anm_vehicle_wheels_v01.anim` |
| ANM-VEH-002 | Fuel hose service clip | Runtime only | Missing `.anim` | Unity clip | `Animation/Vehicles/anm_fuel_service_v01.anim` |
| ANM-VEH-003 | Baggage service clip | Runtime only | Missing `.anim` | Unity clip | `Animation/Vehicles/anm_baggage_service_v01.anim` |
| ANM-VEH-004 | Bus door / settle clip | Runtime only | Missing `.anim` | Unity clip | `Animation/Vehicles/anm_bus_service_v01.anim` |

## P2 — VFX

| ID | Item | In git? | Quality | Source from | Target path |
|---|---|---|---|---|---|
| VFX-001 | Touchdown smoke | Yes | Placeholder | Refine particle prefab | `VFX/vfx_touchdown_smoke_v01.prefab` |
| VFX-002 | Engine heat shimmer | Yes | Placeholder | Refine | `VFX/vfx_engine_heat_v01.prefab` |
| VFX-003 | Rain | Yes | Placeholder | Refine performance tiers | `VFX/vfx_rain_airfield_v01.prefab` |
| VFX-004 | Wet surface response | Yes | Placeholder | Refine | `VFX/vfx_wet_surface_response_v01.prefab` |
| VFX-005 | Construction dust | No | Skip | Later construction milestone | `VFX/vfx_construction_dust_v01.prefab` |

## P2 — UI & brand

| ID | Item | In git? | Quality | Source from | Target path |
|---|---|---|---|---|---|
| BRD-001 | Airside wordmark | Yes | OK | Keep | `Brand/airside_wordmark_light_v01.png` |
| UI-ILL-001 | Dawn splash | Yes | OK | Keep | `UI/Illustrations/ui_splash_airport_dawn_v01.png` |
| UI-ICO-001 | Weather icons (7) | Yes | OK | Keep / light cleanup | `UI/Icons/ui_weather_*_v01.png` |
| UI-ICO-002 | Operation icons (7) | Yes | OK | Keep | `UI/Icons/ui_operation_*_v01.png` |
| UI-ICO-003 | Service icons (7) | Yes | OK | Keep | `UI/Icons/ui_service_*_v01.png` |
| UI-ICO-004 | Economy icons (7) | Yes | OK | Keep | `UI/Icons/ui_economy_*_v01.png` |
| UI-ICO-005 | System icons (8) | Yes | OK | Keep | `UI/Icons/ui_system_*_v01.png` |
| UI-PNL-001 | Light 9-slice panel | Yes | OK unused | Optional | `UI/Panels/ui_panel_9slice_light_v01.png` |
| UI-PNL-002 | Dark 9-slice panel | Yes | OK | Keep | `UI/Panels/ui_panel_9slice_dark_v01.png` |
| UI-PNL-003 | Alert stripe | Yes | OK | Keep | `UI/Panels/ui_alert_stripe_v01.png` |

## P3 — Audio (procedural only today — treat as missing)

| ID | Item | In git? | Quality | Source from | Target path |
|---|---|---|---|---|---|
| AUD-001 | Engine / prop loop | Procedural sine | Missing | Project-owned or cleared licence loop | `Audio/` (new folder — add to AGENTS map when created) |
| AUD-002 | Ambient wind / coast bed | No | Missing | Same | `Audio/` |
| AUD-003 | Touchdown / rollout one-shot | No | Missing | Same | `Audio/` |
| AUD-004 | Soft rain bed | No | Missing | Same | `Audio/` |
| AUD-005 | UI click / alert (minimal) | Yes (`Resources/Airside/Audio/ui_select_005.ogg`) | Partial — click integrated; alert still missing | Kenney Interface Sounds (CC0); register row | `../../Resources/Airside/Audio/` (runtime-loadable AudioClip path) |

## Reference boards (keep in git — modelling targets, not runtime meshes)

| ID | Item | In git? | Quality | Source from | Target path |
|---|---|---|---|---|---|
| REF-001 | Day overview master | Yes | Approved | Do not restyle | `docs/art/reference/ref_airport_first_playable_day_v01.png` |
| REF-002 | Dusk overview master | Yes | Approved | Do not restyle | `docs/art/reference/ref_airport_first_playable_dusk_v01.png` |
| REF-003 | Turnaround service zones | Yes | Approved | Do not restyle | `docs/art/reference/ref_turnaround_service_zones_v01.png` |
| REF-004 | Operations HUD reference | Yes | Approved | Layout only; no baked text in runtime | `docs/art/reference/ref_operations_hud_v01.png` |
| REF-005 | Scale & palette sheet | Yes | Approved | Do not restyle | `docs/art/reference/ref_asset_scale_and_palette_v01.png` |
| REF-BLD | Building fidelity board | Yes | Approved | Modelling target | `docs/art/reference/ref_regional_airport_building_fidelity_board_v01.png` |
| REF-TURN | Turnaround day/dusk board | Yes | Approved | Modelling target | `docs/art/reference/ref_turnaround_service_set_day_dusk_v01.png` |
| REF-SURF | Surface texture board | Yes | Approved | Material target only | `docs/art/reference/ref_airfield_surface_texture_board_v01.png` |
| REF-ARFF | ARFF facility board | Yes | Approved | Modelling target | `docs/art/reference/ref_regional_arff_facility_fidelity_v01.png` |
| REF-SCRUB | Coastal scrub sheet | Yes | Approved | Modelling target | `docs/art/reference/ref_kingscote_coastal_scrub_style_sheet_v01.png` |
| REF-TERRAIN | Context terrain catalogue | Yes | Approved | Modelling target | `docs/art/reference/ref_kingscote_context_terrain_catalogue_v01.png` |
| REF-CHR | Character silhouette sheet | Yes | Approved | Modelling target | `docs/art/reference/ref_airside_character_silhouette_kit_v01.png` |

---

## Explicitly out of scope for this refine pass

Do **not** source these until the first playable is proven with external playtests:

- Narrow-body / wide-body / cargo / GA fleets
- Terminal interiors & passenger agents as gameplay
- Baggage systems, cargo buildings, rail/road access
- Seasonal biome packs
- Research/construction illustration sets beyond current icons
- iPhone Companion / CloudKit art
- Real map / real airline data

---

## Intake checklist (per asset Bailey brings in)

1. Drop source under `docs/art/candidates/` (or author FBX into a WIP folder).
2. Name `category_subject_variant_vNN.ext` — never overwrite an Approved file silently.
3. Add register row (source, licence, cost, attribution, fallback, prompt/evidence).
4. Promote to `Art/...` + Unity `.meta` + Resources prefab if required.
5. Run `scripts/sync-art-streaming-assets.sh` for filesystem-loaded kits.
6. Wire PreferArtKit / PreferSurface only after fallback still works.
7. Mac Play: overview + follow, day/dusk/night; update this file’s Quality column.
8. Update `GAME.md` / `CHANGELOG.md` in the same commit.

## Counts (first playable)

| Bucket | Rows |
|---|---|
| P0 aircraft + buildings | ~20 |
| P1 vehicles / GSE / world / veg / characters | ~55 |
| P2 materials / anim / VFX / UI | ~35 |
| P3 audio | 5 |
| Reference boards | 12 |
| **Total sourcing rows** | **~127** |

Many kit rows already have a Placeholder file in git — that does **not** mean
they are finished. Treat every Placeholder and Missing row as work Bailey still
needs to source or author for the refine pass.
