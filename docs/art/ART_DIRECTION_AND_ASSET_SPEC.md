# Airside art direction and production asset specification

**Status:** Approved production direction  
**Approved by:** Bailey, 6 September 2026  
**Applies to:** Mac game visuals, generated reference images, runtime art, animation and VFX

This is the canonical visual and asset contract for Airside. Every contributor
(ChatGPT, Claude, Cursor and Codex) must read it before generating, importing or
integrating art. `GAME.md` remains the authority for current gameplay state;
this document is the authority for how that gameplay should look and move.

## Visual promise

Airside is a **premium architectural miniature of a believable Australian
airport**. It is stylised realism, not photorealism and not a toy-city cartoon.

The player normally sees the airport from an elevated three-quarter camera.
Shapes, colour blocks, markings and movement must read clearly from that view,
while close follow views reward inspection with restrained detail. The world
should feel warm, calm and quietly busy in normal operation; disruptions should
be visible without turning the screen into an alarm panel.

### Visual principles

1. **Readable operations first.** Aircraft, stands, routes, vehicles, holds and
   active tasks remain legible at the normal overview zoom.
2. **Believable proportions.** Simplification is welcome; incorrect operational
   relationships are not. Vehicles approach the correct aircraft zone, markings
   have a plausible purpose, and buildings have credible scale.
3. **One coherent miniature.** Clean geometry, softened edges, subtle surface
   variation and consistent material response. Avoid mixing photographic cut-outs,
   flat clip-art, pixel art and realistic PBR scans.
4. **Australian regional character.** Dry-to-green grass, strong light, practical
   terminals, corrugated metal, eucalyptus/sand accents and big skies. Do not use
   flags, clichés or real airline branding as shorthand.
5. **Motion explains state.** Animation communicates what is happening: taxi,
   hold, service, delay, construction and weather. Idle motion is restrained.
6. **Calm interface.** Dark ink, warm off-white panels, clear hierarchy and colour
   used for state rather than decoration. Text is rendered in Unity, never baked
   into generated images.

## Palette

| Role | Colour | Hex |
|---|---|---|
| UI ink / deep shadow | Runway Ink | `#17242A` |
| Main dark surface | Tarmac | `#343B40` |
| Structural mid-tone | Concrete | `#9CA3A2` |
| Primary natural | Eucalyptus | `#4F6F60` |
| Secondary landscape | Dry Grass | `#8A8A58` |
| Warm ground accent | Sand | `#C8B286` |
| Navigation / selected | Coastal Blue | `#39708A` |
| Operational caution | Safety Yellow | `#F2C14B` |
| Delay / danger only | Signal Red | `#C95D50` |
| Positive / on time | Clear Green | `#5F8B68` |
| Panel background | Cloud | `#EEF1EC` |
| Day sky accent | Open Sky | `#A7C9D9` |

Materials may move lighter or darker with lighting, but their identity should
remain recognisable. Signal Red is reserved for a problem requiring attention;
it must not become a general brand colour.

## Lighting and camera target

- Daylight is bright with slightly warm direct light and cool, soft shadows.
- Dawn and dusk use long warm light without orange fog over the whole scene.
- Night is deep blue-grey with readable pools of apron, terminal and runway light.
- Weather changes contrast, sky, wetness and visibility; it does not recolour every
  asset.
- Normal assets are authored for the existing 48-degree field of view and must
  remain identifiable from the default overview.
- Fine texture detail must not carry gameplay information. Silhouette, motion and
  state colour do that work.

## Ownership and source rules

- All new fictional brands, liveries and original designs are Airside project
  assets.
- Generated images must be original, must not request imitation of a named living
  artist, and must not contain real airline logos, aircraft trademarks used as
  branding, watermarks or signatures.
- The fictional airlines already in simulation are: Coastline Regional, Emu Air,
  Southern Cross Link, Gulf Connect and Redgum Air. Use only these names unless
  gameplay data is changed in the same approved task.
- Every delivered file must be entered in
  `docs/data/ASSET_AND_DATA_REGISTER.md` with generator/source, prompt or source
  evidence, licence/terms, cost, attribution and fallback.
- Editable source and reference images live under `docs/art/`. Only approved,
  runtime-ready files live under Unity's `Assets/` tree.
- No contributor may silently replace an approved file with a differently styled
  generation. Create a versioned candidate, review it, then promote it.

## Folder contract

```text
docs/art/
  ART_DIRECTION_AND_ASSET_SPEC.md   this contract
  candidates/                       generated assets awaiting review/approval
  reference/                        approved visual targets and concept sheets
  prompts/                          exact generation prompts and model/settings notes

game/Airside/Assets/Airside/Art/
  Animation/Aircraft/
  Animation/Vehicles/
  Animation/World/
  Brand/
  Materials/
  Models/Aircraft/
  Models/Buildings/
  Models/Props/
  Models/Vehicles/
  Textures/Decals/
  Textures/Environment/
  Textures/Surfaces/
  UI/Icons/
  UI/Illustrations/
  UI/Panels/
  VFX/
```

Unity `.meta` files must be committed with their assets. Moving an integrated
asset must be done inside Unity so its GUID remains stable.

## File and import rules

- Lowercase snake_case names: `category_subject_variant_v01.ext`.
- Increment the version for a visibly different candidate. Do not use
  `final_final2`.
- Reference images: PNG, sRGB, normally 3840×2160 or 2048×2048.
- UI illustrations: PNG, sRGB; transparent only where needed. No baked text.
- Icons: master as SVG when hand-built; runtime PNG at 128×128 when raster is
  required. Keep a 16 px safe area and test at 24 px.
- Surface textures: tileable PNG, power-of-two, normally 2048×2048. Base colour
  uses sRGB; mask/normal/roughness data is imported as non-colour.
- Models: FBX or glTF source with metres as units, sensible pivots and named
  materials. Runtime prefabs own colliders, LODs and behaviour components.
- First-playable world assets target two LODs plus a simple collider. Prefer
  shared materials and atlases over unique 4K textures.
- UI panels should be nine-sliced or drawn by UI Toolkit/uGUI. Generated panels
  are texture accents, not screenshots masquerading as an interface.

## Status lifecycle

`Planned → Generated/Modelled → Review → Approved → Integrated → Verified`

- **Generated/Modelled:** a candidate exists but code must not depend on it.
- **Approved:** Bailey has accepted the look and the file is registered.
- **Integrated:** Runtime code references the approved asset with a documented
  fallback. For filesystem-loaded kits/PNGs this also requires a sync into
  `StreamingAssets/Airside/Art` (`scripts/sync-art-streaming-assets.sh`) so
  **packaged builds** load the same files as the Editor (decision 0025).
  Integrated does **not** mean final fidelity — current Batch C kits are still
  greybox-scale placeholders relative to REF screenshots.
- **Verified:** checked at overview/follow views, day/dusk/night and at target Mac
  performance.

The manifest below is authoritative. Contributors update its status and evidence
in the same commit as each asset batch.

## First-playable asset manifest

### Batch A — lock the look before production

| ID | Deliverable and exact path | Type | Requirement | Status |
|---|---|---|---|---|
| REF-001 | `docs/art/reference/ref_airport_first_playable_day_v01.png` | Reference image | Default Kingscote airfield, elevated three-quarter view, runway, A taxiway, two stands, terminal, hangar, three aircraft and service activity | Approved |
| REF-002 | `docs/art/reference/ref_airport_first_playable_dusk_v01.png` | Reference image | Same composition and asset design as REF-001 at dusk; apron/runway lighting readable | Approved |
| REF-003 | `docs/art/reference/ref_turnaround_service_zones_v01.png` | Concept sheet | One fictional regional turboprop at stand with fuel truck, baggage train and passenger bus in safe readable positions; no text baked into final runtime art | Approved |
| REF-004 | `docs/art/reference/ref_operations_hud_v01.png` | UI reference | Operations HUD, route offer and welcome-back panel over gameplay; approved reference is 1280×720 (runtime HUD targets desktop ~2560×1440) | Approved |
| REF-005 | `docs/art/reference/ref_asset_scale_and_palette_v01.png` | Style sheet | Aircraft, vehicles, person, terminal module, materials and palette in one consistent scale reference | Approved |
| BRD-001 | `game/Airside/Assets/Airside/Art/Brand/airside_wordmark_light_v01.png` | Runtime image | Transparent wordmark; simple aviation/wayfinding character; no tiny tagline | Generated — review candidate at `docs/art/candidates/airside_wordmark_light_v01.png`; not integrated |
| UI-ILL-001 | `game/Airside/Assets/Airside/Art/UI/Illustrations/ui_splash_airport_dawn_v01.png` | Runtime image | 3840×2160, composition leaves quiet areas for Unity-rendered title and controls | Generated — review candidate at `docs/art/candidates/ui_splash_airport_dawn_v01.png`; not integrated |

**Gate:** Bailey approved REF-001 through REF-005 on 6 September 2026. Batches
B–D may now use them as production targets. UI-ILL-001 and any later day/dusk
masters must inherit that approved design rather than reinvent it.

For a machine-friendly map of the approved references, candidates and remaining
gaps, see `docs/art/AI_IMAGE_REFERENCE_INDEX.md`.

### Batch B — world surfaces, markings and environment

Production task packet:
`docs/art/prompts/batch-b-surfaces-task-packet.md`.
Generation evidence:
`docs/art/prompts/batch-b-surfaces-generation-2026-09-06.md`.

| ID | Runtime file | Requirement | Status |
|---|---|---|---|
| TEX-SRF-001 | `Textures/Surfaces/tx_asphalt_runway_basecolor_v01.png` | Seamless, restrained aggregate, no painted markings | Approved · Integrated |
| TEX-SRF-002 | `Textures/Surfaces/tx_concrete_apron_basecolor_v01.png` | Seamless large slab variation; joints supplied separately or shader-scaled | Approved · Integrated |
| TEX-SRF-003 | `Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png` | Seamless dry-green regional grass, no obvious flowers or objects | Approved · Integrated |
| TEX-SRF-004 | `Textures/Surfaces/tx_corrugated_metal_basecolor_v01.png` | Neutral building material that can be tinted | Approved · Integrated |
| TEX-ENV-001 | `Textures/Environment/tx_terminal_glass_mask_v01.png` | Window variation mask; no fake people or unreadable signage | Approved · Integrated |
| TEX-DEC-001 | `Textures/Decals/dc_runway_wear_v01.png` | Transparent subtle rubber/wear pass | Approved · Integrated |
| TEX-DEC-002 | `Textures/Decals/dc_apron_stains_v01.png` | Transparent restrained service wear | Approved · Integrated |
| MAT-001 | `Materials/mat_airfield_surface_library_v01.mat` | Shared asphalt, concrete, grass, glass, painted line and metal materials | Planned (Editor .mat optional; prototype loads PNGs) |
| WLD-001 | `Models/Props/mdl_airfield_markings_kit_v01.gltf` | Runway centre/edge/threshold, taxi centreline and two stand stop markings; precision geometry, not AI-painted text | Approved · Integrated |
| WLD-002 | `Models/Props/mdl_airfield_lighting_kit_v01.gltf` | Runway edge, taxiway, apron floodlight and obstruction lights | Approved · Integrated |
| WLD-003 | `Models/Props/mdl_airfield_props_kit_v01.gltf` | Windsock, cones, barriers, signs and baggage dollies | Approved · Integrated |

**Batch B gate:** Bailey approved Batch B on 6 September 2026. Greybox surfaces
and decals are Integrated in `AirsidePrototype` with solid-colour fallback.
WLD kits load via `ArtGltfLoader` at runtime (primitive fallback when missing);
Verified only after Unity Play.

Paths in this and later tables are relative to
`game/Airside/Assets/Airside/Art/`.

### Batch C — first-playable 3D set

**Fidelity note (2026-09-07):** Current glTF kits are low-poly greybox stand-ins
(e.g. turboprop ~336 vertices / 14 meshes; terminal ~120 vertices / 5 meshes)
with no authored URP materials. Status "Integrated" means the runtime loader
can place them (when StreamingAssets is synced); it does **not** mean they match
REF screenshots. Replacing this set is backlog item 2 in decision 0025.

Production task packet:
`docs/art/prompts/batch-c-models-task-packet.md`.
Generation evidence:
`docs/art/prompts/batch-c-models-generation-2026-09-06.md`.

These are modelled assets. Image generation supplies approved concept/orthographic
references but **does not substitute a flat image for a 3D object**.

| ID | Runtime file | Required states / notes | Status |
|---|---|---|---|
| AIR-001 | `Models/Aircraft/mdl_regional_turboprop_01_v01.gltf` | Fictional twin turboprop; gear, propellers, doors and control surfaces separated; primary and traffic liveries use material variants | Approved · Integrated |
| AIR-002 | `Textures/Decals/dc_livery_coastline_regional_v01.png` | Fictional blue/coastal identity, transparent decal atlas | Approved · Integrated |
| AIR-003 | `Textures/Decals/dc_livery_emu_air_v01.png` | Fictional ochre/gold identity; no real airline resemblance | Approved · Integrated |
| AIR-004 | `Textures/Decals/dc_livery_airside_traffic_v01.png` | Neutral traffic livery used by GT-201/GT-202 when no airline is assigned | Approved · Integrated |
| BLD-001 | `Models/Buildings/mdl_terminal_regional_small_v01.gltf` | Small practical terminal, glass frontage, service side, modular end caps | Approved · Integrated |
| BLD-002 | `Models/Buildings/mdl_hangar_small_v01.gltf` | Corrugated metal hangar with readable door opening | Approved · Integrated |
| BLD-003 | `Models/Buildings/mdl_operations_shed_v01.gltf` | Compact service/crew building used as visual support, non-interactive initially | Approved · Integrated |
| VEH-001 | `Models/Vehicles/mdl_fuel_truck_small_v01.gltf` | Cab, wheels and hose connection separated | Approved · Integrated |
| VEH-002 | `Models/Vehicles/mdl_baggage_tug_train_v01.gltf` | Tug plus three low-detail carts; articulation points defined | Approved · Integrated |
| VEH-003 | `Models/Vehicles/mdl_passenger_bus_apron_v01.gltf` | Compact apron bus with doors and wheels separated | Approved · Integrated |
| PRP-001 | `Models/Props/mdl_service_equipment_kit_v01.gltf` | Stairs, chocks, cones, towbar, bins and ground-power unit | Approved · Integrated |

### Batch D — animation, feedback and weather

Production task packet:
`docs/art/prompts/batch-d-animation-task-packet.md`.

Global aircraft movement remains driven by deterministic simulation and the
existing presentation paths. Animation decorates that state; it must never decide
simulation timing or resource ownership.

| ID | Clip/prefab | Trigger and behaviour | Status |
|---|---|---|---|
| ANM-AIR-001 | `Animation/Aircraft/anm_propeller_spin_v01.anim` | Loops while engines are active; visual speed may smooth but follows phase state | **Integrated (runtime)** — phase RPM in `SpinPropellers`; clip file still Planned |
| ANM-AIR-002 | `Animation/Aircraft/anm_gear_cycle_v01.anim` | Deploy/retract only at explicit presentation phase boundaries | **Integrated (runtime)** — soft pitch retract/deploy; clip file still Planned |
| ANM-AIR-003 | `Animation/Aircraft/anm_cabin_door_cycle_v01.anim` | Opens at stand after safe arrival; closes before pushback | **Integrated (runtime)** — existing door swing; clip file still Planned |
| ANM-AIR-004 | `Animation/Aircraft/anm_aircraft_lights_v01.controller` | Nav steady, beacon pulse, landing/taxi lights by phase and day/night | **Integrated (runtime)** — landing vs taxi lights split; controller file still Planned |
| ANM-VEH-001 | `Animation/Vehicles/anm_vehicle_wheels_v01.anim` | Wheel rotation derived from presentation movement | **Integrated (runtime)** — wheel spin from travel; clip file still Planned |
| ANM-VEH-002 | `Animation/Vehicles/anm_fuel_service_v01.anim` | Park, deploy hose, service loop, retract; duration mapped to task progress | **Integrated (runtime)** — hose loop; clip file still Planned |
| ANM-VEH-003 | `Animation/Vehicles/anm_baggage_service_v01.anim` | Tug arrival, cart activity and departure mapped to baggage task | **Integrated (runtime)** — cargo bob; clip file still Planned |
| ANM-VEH-004 | `Animation/Vehicles/anm_bus_service_v01.anim` | Door open/close and subtle suspension settle mapped to boarding/deboarding | **Integrated (runtime)** — door swing; clip file still Planned |
| VFX-001 | `VFX/vfx_touchdown_smoke_v01.prefab` | Brief restrained wheel smoke on touchdown | **Integrated (runtime)** — dual wheel puffs; prefab file still Planned |
| VFX-002 | `VFX/vfx_engine_heat_v01.prefab` | Subtle close-view heat distortion only | **Integrated (runtime)** — heat quads + takeoff boost; prefab still Planned |
| VFX-003 | `VFX/vfx_rain_airfield_v01.prefab` | Camera/world rain with performance tier; weather state controls it | **Integrated (runtime)** — denser rain + storm intensity; prefab still Planned |
| VFX-004 | `VFX/vfx_wet_surface_response_v01.prefab` | Material wetness and muted reflection, not a full-screen filter | **Integrated (runtime)** — paved surface darken incl. Stand 3; prefab still Planned |
| VFX-005 | `VFX/vfx_construction_dust_v01.prefab` | Reserved for visible construction milestone, not integrated early | Planned |

### Batch E — operational interface

Bailey Approved 2026-09-06. Runtime Integration landed via PR #19 (partial — see row notes).
Unverified in Unity Play until Bailey soaks.

| ID | Runtime file/group | Requirement | Status |
|---|---|---|---|
| UI-ICO-001 | `UI/Icons/ui_weather_{clear,overcast,rain,fog,storm,heat,wind}_v01.png` | Clear, overcast, rain, fog, storm, heat and wind; monochrome-capable | **Approved · Integrated** — wired via `AirsideTheme.WeatherIcon`. Unverified in Unity Play |
| UI-ICO-002 | `UI/Icons/ui_operation_{arrival,departure,stand,taxi,hold,turnaround,completed}_v01.png` | Arrival, departure, stand, taxi, hold, turnaround and completed | **Approved · Integrated** — `AirsideTheme.OperationIcon` draws the active phase in the HUD |
| UI-ICO-003 | `UI/Icons/ui_service_*_v01.png` | Fuel, baggage, passengers, cleaning, catering, inspection and priority crew | **Approved · Integrated** — regenerated project-owned sheet (`scripts/generate-batch-e-ui-fix.py`); turnaround tasks draw matching icons |
| UI-ICO-004 | `UI/Icons/ui_economy_{cash,cost,income,payroll,reputation,route,research}_v01.png` | Cash, cost, income, payroll, reputation, route and research | **Approved · Integrated** — cash, reputation and research icons drawn in the HUD |
| UI-PNL-001 | `UI/Panels/ui_panel_9slice_light_v01.png` | 64×64 or 128×128 nine-slice, subtle edge and no baked text | **Approved** — in Assets; unused (HUD uses dark panel) |
| UI-PNL-002 | `UI/Panels/ui_panel_9slice_dark_v01.png` | Dark translucent operations panel, WCAG-aware text contrast | **Approved · Integrated** — regenerated at ~89% mean alpha; `AirsideTheme.PanelBackground` prefers it when opacity ≥ 50% |
| UI-PNL-003 | `UI/Panels/ui_alert_stripe_v01.png` | Caution texture used sparingly; warning colour still supplied by Unity | **Approved · Integrated** — `AirsideTheme.CautionStyle`. Unverified in Unity Play |


## Later production backlog

Do not generate or integrate this set until the related gameplay milestone is
approved. It includes terminal interiors and passenger agents; narrow-body,
wide-body, cargo and general-aviation fleets; modular terminal construction;
baggage systems; emergency services; cargo buildings; rail/surface access;
seasonal biome variants; research/construction illustrations; and iPhone companion
art. Each later system extends this manifest instead of creating a separate style.

## Generation recipe

Every image-generation request must repeat these anchors:

- premium stylised-realism airport-management game;
- architectural miniature viewed from an elevated three-quarter camera;
- believable Australian regional airport scale and operations;
- clean simplified geometry, softened edges, subtle PBR material response;
- Runway Ink / eucalyptus / concrete / safety-yellow palette;
- warm natural light with cool soft shadows;
- no real airline logos, no trademarks, no watermark, no signature;
- no baked UI text unless the output is explicitly a non-runtime layout reference;
- match the approved REF-001 asset shapes and proportions.

The exact prompt, generator/model, date, dimensions and any edit chain are saved
under `docs/art/prompts/` and referenced from the asset register.

## Integration acceptance criteria

A first-playable visual asset is complete only when:

1. its ID, exact path, status and evidence are current in this document;
2. its rights/source record is current in the asset register;
3. the Unity reference uses the approved file and retains a primitive/material
   fallback until the prefab or UI element loads successfully;
4. missing art cannot change simulation state, timing, saves or offline replay;
5. it is checked in the default overview and follow camera at day, dusk and night;
6. active/holding/delayed states remain distinguishable without reading the HUD;
7. no real brand, watermark, baked placeholder text or inconsistent art style is
   visible; and
8. `GAME.md` and `CHANGELOG.md` record the integration and verification.
