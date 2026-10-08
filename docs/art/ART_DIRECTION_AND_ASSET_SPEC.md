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
| BRD-001 | `game/Airside/Assets/Airside/Art/Brand/airside_wordmark_light_v01.png` | Runtime image | Legacy transparent wordmark retained for compatibility; no tiny tagline | Retired from opening |
| BRD-002 | `game/Airside/Assets/Airside/Art/Brand/airside_app_icon_v02.png` | Runtime image | Former approach-runway icon | Retired; v03 is the Player Settings icon |
| BRD-003 | `game/Airside/Assets/Airside/Art/Brand/airside_brand_mark_v02.png` | Runtime image | Former transparent approach-runway mark | Retired from launch |
| BRD-004 | `game/Airside/Assets/Airside/Art/Brand/airside_wordmark_light_v03.png` | Runtime image | 2048×512 transparent AS control-vector lockup with exact AIRSIDE spelling | Superseded on title by BRD-007; retained for compatibility |
| BRD-005 | `game/Airside/Assets/Airside/Art/Brand/airside_app_icon_v03.png` | Runtime image | 1024² rounded Standalone/macOS app icon; no baked wordmark | Integrated (Player Settings default icon; Dock/Finder verify on Mac build) |
| BRD-006 | `game/Airside/Assets/Airside/Art/Brand/airside_brand_mark_v03.png` | Runtime image | 1024² transparent AS control-vector mark for the launch sequence | Integrated (opening; packaged via StreamingAssets) |
| UI-ILL-001 | `game/Airside/Assets/Airside/Art/UI/Illustrations/ui_splash_airport_dawn_v01.png` | Runtime image | 3840×2160, composition leaves quiet areas for Unity-rendered title and controls | Legacy fallback; title superseded by UI-ILL-002 |
| BRD-007 | `game/Airside/Assets/Airside/Art/Brand/airside_wordmark_light_v04.png` | Runtime image | 1800×360 transparent original departure-vector lockup; editable SVG/strokes; native-text missing-file fallback | Integrated at Bailey’s 8 October redesign request; Unity appearance unverified |
| UI-ILL-002 | `game/Airside/Assets/Airside/Art/UI/Illustrations/ui_splash_adelaide_t1_dawn_v02.png` | Runtime image | 1672×941 Adelaide T1 dawn interpretation, low linear glass concourse/jetbridges/solar roof; no baked UI | Integrated at Bailey’s 8 October redesign request; Unity appearance unverified |

**Gate:** Bailey approved REF-001 through REF-005 on 6 September 2026. Bailey
approved BRD-001 and UI-ILL-001 on 7 September 2026 for runtime use. Bailey
requested BRD-002 on 12 September 2026 and had it merged to `main` as the
Standalone Player icon. Bailey requested the full v03 redesign on 28 September
2026; BRD-004 through BRD-006 supersede BRD-001 through BRD-003 at runtime.
Bailey requested the title/identity redesign on 8 October 2026; BRD-007 and UI-ILL-002 replace the opening assets under that authorisation. Evidence: `docs/art/prompts/adelaide-opening-2026-10-08.md`. The v03 app icon remains in Player Settings.
Batches B–D may now use REF masters as production
targets. UI-ILL-001 inherits that approved design rather than reinventing it.

For a machine-friendly map of the approved references, candidates and remaining
gaps, see `docs/art/AI_IMAGE_REFERENCE_INDEX.md`. For Bailey’s source-everything
refine pass (every wheel, prop, engine, tree, GSE part, audio bed, …), see
`docs/art/FIRST_PLAYABLE_ART_SOURCING_CHECKLIST.md`.

### Batch B — world surfaces, markings and environment

Production task packet:
`docs/art/prompts/batch-b-surfaces-task-packet.md`.
Generation evidence:
`docs/art/prompts/batch-b-surfaces-generation-2026-09-06.md` and
`docs/art/prompts/batch-b-pbr-maps-generation-2026-09-07.md` (normal/AO/mask).

| ID | Runtime file | Requirement | Status |
|---|---|---|---|
| TEX-SRF-001 | `Textures/Surfaces/tx_asphalt_runway_basecolor_v01.png` | Seamless, restrained aggregate, no painted markings | Approved · Integrated |
| TEX-SRF-002 | `Textures/Surfaces/tx_concrete_apron_basecolor_v01.png` | Seamless large slab variation; joints supplied separately or shader-scaled | Approved · Integrated |
| TEX-SRF-003 | `Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png` | Seamless dry-green regional grass, no obvious flowers or objects | Approved · Integrated |
| TEX-SRF-004 | `Textures/Surfaces/tx_corrugated_metal_basecolor_v01.png` | Neutral building material that can be tinted | Approved · Integrated |
| TEX-SRF-005 | `Textures/Surfaces/tx_*_{normal,ao,mask}_v01.png` | Companion PBR maps for asphalt/concrete/grass/metal (1024²) | Integrated (runtime Lit; Play unverified) |
| TEX-ENV-001 | `Textures/Environment/tx_terminal_glass_mask_v01.png` | Window variation mask; no fake people or unreadable signage | Approved · Integrated |
| TEX-DEC-001 | `Textures/Decals/dc_runway_wear_v01.png` | Transparent subtle rubber/wear pass | Approved · Integrated |
| TEX-DEC-002 | `Textures/Decals/dc_apron_stains_v01.png` | Transparent restrained service wear | Approved · Integrated |
| MAT-001 | `Materials/mat_{asphalt,concrete,grass,corrugated_metal,glass,painted_line,aircraft,wet}_v01.mat` | Shared URP Lit family using Batch B maps; Resources mirror for runtime | Integrated (Editor `.mat` + runtime instantiate; Play soak on packaged build) |
| WLD-001 | `Models/Props/mdl_airfield_markings_kit_v01.gltf` | Runway centre/edge/threshold, taxi centreline and two stand stop markings; precision geometry, not AI-painted text | Approved · Integrated |
| WLD-002 | `Models/Props/mdl_airfield_lighting_kit_v01.gltf` | Runway edge, taxiway, apron floodlight and obstruction lights | Approved · Integrated |
| WLD-003 | `Models/Props/mdl_airfield_props_kit_v01.gltf` | Windsock, cones, barriers, signs and baggage dollies | Approved · Integrated |

**Batch B gate:** Bailey approved Batch B on 6 September 2026. Greybox surfaces
and decals are Integrated in `AirsidePrototype` with solid-colour fallback.
WLD kits load via `ArtGltfLoader` at runtime (primitive fallback when missing);
Verified in a packaged Unity build on 16 September 2026. The real Adelaide apron
presentation has a deterministic 18 m expansion-joint overlay clipped to the OSM
apron polygons. It adds no external asset. Noon, 17:30 golden-hour and 23:30 night
captures passed at overview and follow distance; the detail reads up close without
turning into an overview grid. Evidence is kept locally under `work/review/apron-*.png`.
The same packaged review added sparse procedural taxiway-edge dust/scuff strips. They
use no external asset, remain inside the asphalt edge, and passed close-day, overview
and 23:30 night checks (`work/review/taxi-edge-*.png`).
The real Adelaide OSM terminal shell now carries procedural architectural breakup: 28
separated airside glass bays, a six-part projecting brow, seven skylight strips and three
roof-plant blocks. This detail uses real-world coordinates, adds no external asset and does
not participate in routing, collision or save state. Packaged close day/night evidence is
under `work/review/terminal-facade-final-*.png`.
The focused Adelaide world keeps seven real-coordinate terminal roof floods, with 115 m throw
aimed south over the stands, plus night-only warm interior cards behind the 28 blue glass bays.
The 23:30 ambient key/fill/trilight is lifted enough to preserve pavement and aircraft silhouettes
without making night read as day. Matched packaged evidence is under
`work/review/night-lighting-accepted.png` and `work/review/night-lighting-day-accepted.png`.
ADR 0124 extends the procedural pass to every surveyed building and fixture, still with no
external asset: parapets, storey window bands (lit and dark panes), hangar doors and roof
monitors, fire-station bays, freight doors, rooftop plant, a glazed tower cab with mast and
red obstruction light, and a terminal kerb canopy (`Presentation/BuildingDetail.cs`, merged
one mesh per material); chamfered edges on every procedural block (`BevelledBox`) and
rounded aerobridge tunnels; domed airfield light fixtures merged per colour with additive
night halos (`AirfieldFixture`), blue taxiway edges and red stop bars. Offline geometry
previews are in `docs/testing/building-detail-2026-09-27/`. ADR 0185 completes the first
hero-silhouette pass without changing those surveyed shells: Terminal 1 has raised glass
lanterns and full-height airside piers, operational hangars receive fitted gable/barrel/sawtooth
caps, and the moving aerobridges add glazed rotundas, portal frames, detailed cabs and wheeled
bogies. Packaged Mac day/night overview and airside-facing close captures passed on 30 September
2026; future imported replacements must preserve the same footprints, sites and fallbacks.

Paths in this and later tables are relative to
`game/Airside/Assets/Airside/Art/`.

### Batch C — first-playable 3D set

**Fidelity note (2026-09-07):** v01 glTF kits were low-poly greybox stand-ins
(e.g. turboprop ~336 vertices / 14 meshes). **v02 kits** (`*_v02.gltf`) add more
readable parts (cross props, canopy, hangar buttresses, vehicle detail) while
staying in the same POSITION+indices box format `ArtGltfLoader` understands.
Runtime prefers v02 when present and falls back to Approved v01. Status
"Integrated" still does **not** mean REF-screenshot fidelity — authored meshes /
URP materials remain the longer-term path (decision 0025).

Production task packet:
`docs/art/prompts/batch-c-models-task-packet.md`.
Generation evidence:
`docs/art/prompts/batch-c-models-generation-2026-09-06.md` (v01) and
`docs/art/prompts/batch-c-models-v02-generation-2026-09-07.md` (v02).

These are modelled assets. Image generation supplies approved concept/orthographic
references but **does not substitute a flat image for a 3D object**.

| ID | Runtime file | Required states / notes | Status |
|---|---|---|---|
| AIR-001 | `Models/Aircraft/mdl_atr42_starter_v03.gltf` (+ `.bin`; `.fbx` editable source; v02/v01/v06 fallback) | Fictional ATR 42-600-class regional turboprop at 22.67 × 24.57 × 7.59 m: stocky high-wing fuselage with dark curvature-fitted cockpit/cabin glazing, neutral-finished wing and T-tail planes with accent fin/wingtips, compact blended nacelles, fuselage-side main-gear sponsons and six 3.93 m props; 183 named parts / 21,648 triangles (raised segment counts on nacelles/wheels/gear/props, 2026-09-17); same envelope and moving-part names as v02 | Integrated for Emu Air / player under ADR 0051; 2026-09-16 packaged daylight QA passed after livery-hierarchy pass (v02 retained fallback); Hangar thumbnail regenerated; dusk/night recheck pending |
| AIR-002 | `Textures/Decals/dc_livery_coastline_regional_v01.png` | Fictional blue/coastal identity, transparent decal atlas | Approved · Integrated |
| AIR-003 | `Textures/Decals/dc_livery_emu_air_v01.png` | Fictional ochre/gold identity; no real airline resemblance | Approved · Integrated |
| AIR-004 | `Textures/Decals/dc_livery_airside_traffic_v01.png` | Neutral traffic livery used by GT-201/GT-202 when no airline is assigned | Approved · Integrated |
| AIR-005 | `Models/Aircraft/mdl_737_8_narrowbody_v01.gltf` (+ `.bin`; `.fbx` editable source) | Original unbranded 737-8-class narrowbody at real reference scale: slender fuselage with dark fitted cabin glazing, a skin-coloured flight-deck crown with three compact panes, neutral-finished low swept wing with accent split winglets, large forward-hung turbofans with chevron nozzles, tapered wing-body fairing, joined conventional tail and tricycle gear; 183 named parts / 36,008 triangles (blunt drooped radome, wraparound flight-deck band and flat-bottomed lower cowl, 2026-09-22, ADR 0097; raised segment counts 2026-09-17); dedicated pick, shadow and follow framing profile | Integrated as the operational AI 737-8 VH-WTJ (fictional Wattlebird Jet) at Gate 13 under ADR 0047; 2026-09-16 packaged daylight follow QA passed after livery-hierarchy pass (same path); Hangar thumbnail regenerated; dusk/night recheck pending |
| AIR-006 | `Models/Aircraft/mdl_dash8_q400_v01.gltf` (+ `.bin`; `.fbx` editable source) | Original unbranded Dash 8-400-class regional turboprop at 32.83 × 28.42 × 8.34 m: long high-wing fuselage with dark fitted cabin glazing and framed flight deck, neutral-finished wing and T-tail surfaces with accent fin/tip devices, continuous nacelles with rounded main-gear bays and thin open doors, pitched six-blade props and continuous wing-root saddle; 182 named parts / 30,968 triangles (raised segment counts on nacelles/wheels/gear/props, 2026-09-17); dedicated pick, shadow and follow framing profile | Integrated for QantasLink under ADR 0049; 2026-09-16 packaged daylight QA passed after high-wing livery-hierarchy pass (same path); Hangar thumbnail regenerated; dusk/night recheck pending |
| AIR-007 | `Models/Aircraft/mdl_saab_340b_v01.gltf` (+ `.bin`; `.fbx` editable source) | Original unbranded Saab 340B-class regional turboprop at 19.73 × 21.44 × 6.97 m (standard wing): compact neutral-finished low wing, narrow fuselage, readable skin-proud cabin glazing, rounded flight deck with four fitted panes, conventional tail, four-blade Dowty props with compact hubs, nacelle-mounted twin main gear with curved bay fairings; 120 named parts / 8,588 triangles (raised segment counts on nacelles/wheels/gear/props plus wingtip static wicks, 2026-09-17); dedicated pick, shadow and follow framing profile | Integrated for Rex's existing Saab 340Bs under ADR 0050; 2026-09-16 packaged daylight and 23:30 follow QA passed after silhouette/material hierarchy pass (same path); own 480 × 320 runtime-model thumbnail |
| AIR-009 | `Models/Aircraft/mdl_a350_900_v01.gltf` (+ `.bin`; `.fbx` editable source) | Purpose-built, unbranded A350-900-class widebody at 66.80 × 64.75 × 17.05 m: 5.96 m cabin, long tapered nose, dark wraparound cockpit mask with six panes, high-aspect-ratio wing and raked tips, large 18-blade high-bypass fans, four main doors per side and ten-wheel gear; 229 named parts / 16,440 triangles (raised segment counts on nacelles/wheels/fans plus wingtip static wicks, 2026-09-17); dedicated pick, shadow and follow framing | Integrated for Cathay Pacific's Adelaide service at Gate 18; same AIR-009 asset id replaces the initial scaled-narrowbody geometry; deterministic geometry and generated thumbnail passed, packaged day/dusk/night follow QA pending |
| AIR-010 | `Models/Aircraft/mdl_787_10_v01.gltf` (+ `.bin`; `.fbx` editable source) | Original unbranded Boeing 787-10-class widebody at 68.30 × 60.12 × 17.02 m: 5.77 m cabin, four-pane flight deck without the A350 mask, swept/raked high-aspect-ratio wing, 18-blade fans, chevron nacelle trailing edges, four main doors per side and ten-wheel gear; 227 named parts / 16,504 triangles (raised segment counts on nacelles/wheels/fans plus wingtip static wicks inherited from AIR-009, 2026-09-17); dedicated pick, shadow and follow framing | Integrated for Singapore Airlines' published Adelaide type at Gate 20; deterministic geometry and generated thumbnail passed, packaged day/dusk/night follow QA pending |
| AIR-017 | `Models/Aircraft/mdl_bell_412_rescue_v01.gltf` (+ `.bin`; `.fbx` editable source) | Original unbranded Bell 412EP-class rescue helicopter at 13.90 × 16.97 × 4.45 m: rounded utility cabin, twin engine housings, tapering tail boom, four-blade main rotor, two-blade tail rotor, high skids, glazing, sliding doors, wire-strike cutters, searchlight/camera pod and restrained red/white rescue blocking; 43 named parts / 1,324 triangles | Integrated as static presentation on Adelaide's OSM Helipad West under ADR 0186; procedural fallback retained; Unity EditMode asset/geometry checks and packaged day/night close review passed |
| BLD-001 | `Models/Buildings/mdl_terminal_regional_small_v05.gltf` (+ `.fbx`; fallbacks authored→v04→…→v01) | Batch F1 authored small regional terminal: pitched roof, glazed airside frontage, canopy, service side, rooftop plant, soft end caps (~202 meshes) | Integrated (v05 preferred; Mac FBX bake in Resources; Bailey playtest pending) |
| BLD-002 | `Models/Buildings/mdl_hangar_small_v05.gltf` (+ `.fbx`; fallbacks authored→v04→…→v01) | REF-001 pitched corrugated hangar: dual-pitch roof, gable ends, sliding door panels/bars/tracks, skylights, office lean (193 meshes) | Integrated (v05 preferred; Mac FBX bake / overview pending) |
| BLD-003 | `Models/Buildings/mdl_operations_shed_v05.gltf` (+ `.fbx`; fallbacks authored→v04→…→v01) | Compact ops/crew shed: dual-pitch roof, denser porch and corrugation, antenna/AC silhouette (~160 meshes) | Integrated (v05 preferred; Mac FBX bake / overview pending) |
| VEH-001 | `Models/Vehicles/mdl_fuel_truck_small_v06.gltf` (+ `.fbx`; fallbacks v05→authored→v04→…→v01) | REF-003 fidelity jump: denser oval tank, cab fairing, mid axle, hose pivot, Safety Yellow (103 meshes) | Integrated (v06 preferred; Mac FBX bake / Play pending) |
| VEH-002 | `Models/Vehicles/mdl_baggage_tug_train_v06.gltf` (+ `.fbx`; fallbacks v05→authored→v04→…→v01) | Open ROPS tug + three carts with Coastal Blue cargo crates and hitch pivots (108 meshes) | Integrated (v06 preferred; Mac FBX bake / Play pending) |
| VEH-003 | `Models/Vehicles/mdl_passenger_bus_apron_v06.gltf` (+ `.fbx`; fallbacks v05→authored→v04→…→v01) | Two-tone Coastal Blue / white apron bus; ribbon glazing; rounded nose/tail (97 meshes) | Integrated (v06 preferred; Mac FBX bake / Play pending) |
| VEH-004 | `Models/Vehicles/mdl_pushback_tug_v03.gltf` (+ `.fbx`; fallbacks v02→v01 prefab) | Pushback tug with readable glass, mid towbar pivot and separated wheels | Integrated (v03 preferred; Mac FBX bake / Play pending) |
| CHR-001 | `Models/Characters/mdl_ramp_crew_kit_v02.gltf` (+ `.fbx`; fallback v01) | Marshaller / fueler / ramp with denser limbs, hi-vis vest/hat, wand sockets (32 meshes) | Integrated (v02 preferred; Mac overview/follow pending) |
| CHR-002 | `Models/Characters/mdl_passenger_kit_v02.gltf` (+ `.fbx`; fallback v01) | Six stand/walk/sit silhouettes with tapered limbs (42 meshes); presentation-only | Integrated (v02 preferred; Mac overview/follow pending) |
| PRP-001 | `Models/Props/mdl_service_equipment_kit_v03.gltf` (+ stairs `mdl_passenger_stairs_v02`; fallbacks authored→v02→v01) | Stand GSE: tubular-rail stairs, GPU, belt loader, chocks, cones, towbar, bins (106 meshes) | Integrated (v03 preferred; Mac overview/follow pending) |

### Batch D — animation, feedback and weather

Production task packet:
`docs/art/prompts/batch-d-animation-task-packet.md`.

Global aircraft movement remains driven by deterministic simulation and the
existing presentation paths. Animation decorates that state; it must never decide
simulation timing or resource ownership.

| ID | Clip/prefab | Trigger and behaviour | Status |
|---|---|---|---|
| ANM-AIR-001 | `Animation/Aircraft/anm_propeller_spin_v01.anim` | Loops while engines are active; visual speed may smooth but follows phase state | **Integrated** — `AirsideReusableMotion` + `SpinPropellers`; Unity `.anim` optional later |
| ANM-AIR-002 | `Animation/Aircraft/anm_gear_cycle_v01.anim` | Deploy/retract only at explicit presentation phase boundaries | **Integrated** — `AirsideReusableMotion.GearBias`; soft pitch retract/deploy |
| ANM-AIR-003 | `Animation/Aircraft/anm_cabin_door_cycle_v01.anim` | Opens at stand after safe arrival; closes before pushback | **Integrated** — `CabinDoorBias` + existing door swing |
| ANM-AIR-004 | `Animation/Aircraft/anm_aircraft_lights_v01.controller` | Nav steady, beacon pulse, landing/taxi lights by phase and day/night | **Integrated (runtime)** — landing vs taxi lights split; controller file still Planned |
| ANM-VEH-001 | `Animation/Vehicles/anm_vehicle_wheels_v01.anim` | Wheel rotation derived from presentation movement | **Integrated (runtime)** — wheel spin from travel; rates in `AirsideReusableMotion` |
| ANM-VEH-002 | `Animation/Vehicles/anm_fuel_service_v01.anim` | Park, deploy hose, service loop, retract; duration mapped to task progress | **Integrated (runtime)** — hose loop; clip file still Planned |
| ANM-VEH-003 | `Animation/Vehicles/anm_baggage_service_v01.anim` | Tug arrival, cart activity and departure mapped to baggage task | **Integrated (runtime)** — cargo bob; clip file still Planned |
| ANM-VEH-004 | `Animation/Vehicles/anm_bus_service_v01.anim` | Door open/close and subtle suspension settle mapped to boarding/deboarding | **Integrated (runtime)** — door swing; clip file still Planned |
| VFX-001 | `VFX/vfx_touchdown_smoke_v01.prefab` | Brief restrained wheel smoke on touchdown | **Integrated** — Resources + Art/VFX prefab; `BuildTouchdownSmoke` prefers kit |
| VFX-002 | `VFX/vfx_engine_heat_v01.prefab` | Subtle close-view heat distortion only | **Integrated** — Resources + Art/VFX prefab; runtime heat quads remain |
| VFX-003 | `VFX/vfx_rain_airfield_v01.prefab` | Camera/world rain with performance tier; weather state controls it | **Integrated** — Resources + Art/VFX prefab; dense procedural rain remains primary |
| VFX-004 | `VFX/vfx_wet_surface_response_v01.prefab` | Material wetness and muted reflection, not a full-screen filter | **Integrated** — Resources + Art/VFX prefab; material wet response remains |
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



### Batch F — first-playable visual fidelity closure

Decision 0027 and the implementation-ready packet
`docs/art/prompts/batch-f-first-playable-visual-assets-task-packet.md` define
the remaining first-playable visual assets after the repository-wide gap audit.

| Slice | Asset IDs | Player-visible purpose | Status |
|---|---|---|---|
| F1 hero read | AIR-001 v06, BLD-001…003 v05, MAT-001 | Authored turboprop + terminal/hangar/ops shed with a coherent URP material family | **AIR-001 v06 + BLD-001/002 v05 Integrated**; **BLD-003 v05 preferred** (Mac overview pending); MAT-001 Integrated |
| F2 turnaround read | VEH-001…004 authored revisions, CHR-001…002 v02; landside `mdl_parked_car_v02` | Replace procedural turnaround vehicles and block people where activity must read; landside cars prefer lofted v02 | **Integrated** — fleet v06 / pushback v03 / car v02 / CHR v02 preferred (Mac overview vs REF-003 pending) |
| F3 setting read | VEG-001…002, PRP-002…003, WLD-004 | Replace sphere vegetation, block fencing/forecourt and slab-like context | **VEG-001 v02 preferred** (Mac overview pending); **VEG-002 scrub v02 preferred** (Approved style sheet); **WLD-004 terrain v02 preferred** (Approved catalogue); **PRP-003 v02 preferred** (merged #155); **PRP-002 v02 preferred** (merged #154) |
| F4 reusable finish | Existing Batch D asset files, UI-ICO-005 | Promote runtime motion/VFX to reusable assets and add system-control icons | **Integrated** — UI-ICO-005 + VFX prefabs + `AirsideReusableMotion` |

Batch F is intentionally ordered. It does not authorise another broad procedural
kit pass: each slice must improve silhouette/material fidelity against
REF-001/003/005, retain the current fallback, and pass the packaged Mac camera
matrix before the next begins.

## Later production backlog

Do not generate or integrate this set until the related gameplay milestone is
approved. ADR 0046 is the explicit exception for Bailey's one-at-a-time Adelaide
aircraft rollout, beginning with AIR-005. AIR-013 (E190) and AIR-014 (A220-300) are now
lofted from their own dimension tables rather than scaled from AIR-005 (ADR 0098); AIR-011
(A320) now has its own rounded fuselage, wing, flight deck and livery geometry (ADR 0105),
while inherited running gear and nacelles remain. AIR-015 (A330-900) now has its own
rounder nose and four-pane flight deck plus fitted colour (ADR 0106), while its
wing, engines and gear still inherit the A350 kit. AIR-016 (787-9) remains an
axis-scaled copy. Each later type
still needs its own
reviewable slice. The broader backlog includes terminal interiors and passenger
agents; narrow-body, wide-body, cargo and general-aviation fleets; modular terminal construction;
baggage systems; emergency services; cargo buildings; rail/surface access;
seasonal biome variants; research/construction illustrations; and iPhone companion
art. Each later system extends this manifest instead of creating a separate style.

All 13 current aircraft share the fitted glazing revision in ADR 0117. The existing
window shapes and shell curvature remain authored per type; a separate narrow trim,
dark gasket and restrained upper reflection make the cabin and cockpit openings
readable at close range without changing dimensions, liveries or aircraft identity.
Regenerate models with `scripts/polish-aircraft-glazing.py` and then the Hangar
thumbnails and StreamingAssets mirror; the prior git revision is the fallback.

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

## Fleet appearance revision — 28 September 2026 (ADR 0150)

Bailey requested the whole fleet appearance and livery revision. All 13 current
aircraft retain their asset IDs and model paths. `finish-aircraft-liveries.py`
adds type-fitted original paint, fin symbols and independently attached cowl bands
to the existing authored geometry. It also fits spoilers and pylon tops to the
wings. The glazing generator calls this pass automatically.

`AircraftLiveryPaint` supplies the same clean aircraft finish to glTF and prefab
models. Aircraft do not use the coarse v01 skin or corrugated building textures.
Original Coastline/Emu/Southern Cross colourways are review examples; runtime
identity continues to come from each live airline, including player custom
colours. Full compositions are in ADR 0150. Review evidence and limitations are
in `docs/testing/aircraft-liveries-2026-09-28/README.md`.


## Aircraft audio revision — 30 September 2026 (ADR 0192)

| ID | Exact runtime paths (under `Assets/Resources/Airside/Audio/`) | Required behaviour | Status |
|---|---|---|---|
| AUD-010 | `eng_{atr42,sf34,dh8d,e190,a223,a320,b738,b38m,a21n,a339,a359,b789,b78x}_{idle,power,reverse}_v01.wav` | 39 representative family-derived loops; engine start, governed prop load / jet N1 and reverse after each type's visual contact | Integrated; generator/manifest in `docs/data/audio/`; original recorded beds retained fallback |
| AUD-011 | `aircraft_touchdown_v01.wav` | Registered CC0 tyre contact + original oleo thump; one spatial event per aircraft per landing | Integrated; source and processing in asset register |
| AUD-012 | `aircraft_tyre_roll_v01.wav` | Original rolling noise follows actual tyre speed; absent airborne or standing still | Integrated; silence fallback |

Profile tuning lives in `docs/data/AIRCRAFT_AUDIO_PROFILES.json`. These are
representative sound-design voices, not exact recordings of each engine variant.
The static AIR-017 rescue helicopter remains parked and engine-off. No sound
controls simulation timing. Built-player DSP captures and final verification
are recorded in `docs/testing/audio-2026-09-30/README.md`.

## Shared interface refresh — 6 October 2026 (ADR 0231)

Bailey requested a substantial HUD/viewing and welcome-back redesign. The runtime
interface now uses a restrained slate palette: `AirsidePalette.GlassHex` #121B22,
raised surface #1D2A33, warm-white text #F2F0E8, muted text #A4AFB6, sea-glass
selection #9FC8C5 and soft gold action #D8BE8A. Positive, negative and route hues
are likewise muted. This supersedes the brighter Glass Cockpit UI accents while
preserving the airport/world palette above. Shared controls have stable hover
geometry and modest corners. Original icons/brand art remain available; no new
external art or font is added. Runtime text stays in Unity.

Evidence: `docs/testing/interface-refresh-2026-10-06/`. Rendered previews show
shared layout and palette, not native runtime verification.

## Fleet surface details — 7 October 2026

Task #578 extends AIR-001 and AIR-005…017 with runtime-derived door seams, latch
surrounds/bars, thresholds, hinge marks and forward service hatches. Bell sliding
doors carry an upper rail. Existing exact model paths, IDs, fictional liveries,
colour customization and imported geometry remain authoritative. The shared
`AircraftSurfaceDetails` pass applies to glTF and readable prefab source meshes;
geometry is clipped to the real source triangles and attached to each moving leaf.
No reference-image or external model generation is involved. Status: **Integrated,
unverified in Unity**. Native overview/follow day/dusk/night, door motion and
performance checks remain open. ADR `2026-10-07-fleet-surface-details`; proof and
plan: `docs/testing/fleet-surface-details-2026-10-07/`.

## Parafield light trainer — 8 October 2026

Bailey approved one light-aircraft type for the independent Parafield first version.
AIR-YPPF-001: `Models/Aircraft/mdl_parafield_trainer_v01.gltf` and matching `.bin`.
Original unbranded four-seat high-wing trainer: 11 m span, 8.3 m length, fixed
tricycle gear, cream body, coastal-blue/eucalyptus trim. Runtime geometry is true
3D with lathed body, airfoil wing/tail, struts, glazing and animated two-blade prop.
Source: `scripts/generate-parafield-trainer.py`; zero cost; project-owned; simple
procedural trainer remains the missing-asset fallback. Integrated for native
review; evidence: `docs/testing/parafield-2026-10-08/`.

## Aircraft body contours — 8 October 2026

Task #651 refines AIR-001, AIR-005…017 and AIR-YPPF-001 at their existing exact
runtime model paths. Continuous cubic nose/cabin/tail contours, rounded tips,
and carried skin details replace straight station transitions. Bell/trainer
window slabs become curved skin panels. A320 v02 retains registered free-source
running gear and engines. Project-owned derivative geometry, zero cost; previous
git assets remain fallback. Integrated, native appearance/performance unverified.
Decision: `2026-10-08-aircraft-body-realism`; bounded numeric evidence:
`docs/testing/aircraft-bodies-2026-10-08/`.


## Connected aircraft tails — 9 October 2026

Task #684 revises the existing aircraft manifest IDs and runtime paths, including
both A320 versions and the Parafield trainer. Hull-sampled roots replace floating
fin/stabiliser seams; fixed and moving aerofoils share a matched hinge. The
project-owned per-type stations live in `scripts/aircraft_tail_profiles.json`;
`scripts/aircraft_tail.py` is shared by finishing and generators. Published
stabiliser dimensions and original approximate fin profiles are distinguished in
`docs/testing/aircraft-tails-2026-10-09/`. No external geometry enters production.
Runtime glTF/bin, editable FBX, packaged mirrors and existing hangar thumbnails
are updated. Integrated with geometry/render evidence; native lighting, moving
control surfaces and performance remain unverified. Prior git assets are fallback.
