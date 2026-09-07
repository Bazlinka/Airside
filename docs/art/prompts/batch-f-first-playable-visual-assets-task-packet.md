# Batch F — first-playable visual fidelity closure

**Status:** Planned; implementation-ready asset design  
**Date:** 2026-09-07  
**Decision / contract:** decisions 0022, 0024–0027 and
`docs/art/ART_DIRECTION_AND_ASSET_SPEC.md`  
**Visual authority:** REF-001 through REF-005  
**Runtime contract:** `ArtPresentationLoader` → Addressables/Resources prefab →
existing glTF → procedural fallback

## Player-visible outcome

The first playable should read as the airport shown in the approved references,
not as a dense collection of improved greybox parts. The aircraft, terminal,
turnaround activity and Australian coastal setting must be recognisable from the
default overview; follow view adds believable detail without requiring
photorealism.

This packet designs the remaining first-playable visual assets. It does not
authorise new simulation systems or a broad post-first-playable content batch.

## Audit findings

| Observed gap | Repository evidence | Player impact |
|---|---|---|
| Hero aircraft and buildings are still procedural box-based kits | AIR-001/BLD-001…003 prefer lofted/v04 glTF placeholders; decision 0025 records roughly 20% reference fidelity | The airport silhouette does not yet match REF-001/003/005 |
| Most people and vegetation are primitives | `PlacePerson`, `PlaceTree` and `PlaceShrub` construct blocks, cylinders and spheres | The world looks populated but not authored |
| Fence, gate, terminal forecourt and terrain relief are assembled from blocks | `BuildPerimeterFence`, `BuildTerminalLandsideCanopy`, `BuildTerrainMicroRelief` | Foreground and landside areas expose the greybox most clearly |
| Many Resources prefabs are pipeline proofs, not finished art | Prefab README states they use built-in cube/cylinder meshes | Prefab loading works, but visual quality does not automatically improve |
| Authored Unity materials are still missing | MAT-001 remains partial; runtime creates Lit materials and loads PNG maps | Surfaces do not yet share a deliberate, inspectable material set |
| Animation/VFX intent exists without asset files | Batch D runtime behaviours exist, while its `.anim`, controller and prefab deliverables remain Planned | Motion works, but cannot yet be tuned as reusable authored assets |
| HUD art covers operations, service, weather and economy only | Batch E has no system/control icon family | Pause, speed, follow, overview, audio and save states rely mainly on text |

## Production rule

Do not answer this packet by adding more procedural density. An asset counts
towards Batch F only when it is an authored source mesh/material/clip/prefab (or a
hand-cleaned vector/UI export), follows the hierarchy below, and can visibly
replace its fallback. Existing primitives stay until the replacement passes the
camera and packaged-build checks.

## Ordered slices

Complete one slice, verify it in the packaged Mac build, then start the next.
Slice F1 is the only recommended first implementation task.

### F1 — hero read: aircraft, terminal and core materials

| ID | Exact authoring/runtime paths | Required design | Priority |
|---|---|---|---|
| AIR-001 v05 | `Art/Models/Aircraft/mdl_regional_turboprop_01_v05.fbx`; `Resources/Airside/Prefabs/mdl_regional_turboprop_01_v05.prefab` | Authored fictional twin turboprop matching REF-003/005; rounded fuselage, readable cockpit/windows, six-blade propellers, separated gear/doors/control surfaces/lights; livery slots preserved | P0 — **Integrated** (prefer v05; Mac FBX bake upgrades Resources); Bailey accepted |
| BLD-001 v05 | `Art/Models/Buildings/mdl_terminal_regional_small_v05.fbx`; `Resources/Airside/Prefabs/mdl_terminal_regional_small_v05.prefab` | Practical small regional terminal matching REF-001/005: glazed airside face, shallow roof, canopy, service side, rooftop plant and modular end caps | P0 — **Integrated** (prefer v05; Mac FBX bake); Bailey playtest pending |
| MAT-001 | `Art/Materials/mat_{asphalt,concrete,grass,corrugated_metal,glass,painted_line,aircraft,wet}_v01.mat` | Shared URP Lit material family using existing Batch B maps; consistent roughness, restrained normal strength, glass/emission profiles and wet variants | P0 — **Integrated** (Art + Resources; runtime instantiate) |

F1 must update preference order to v05 → current preferred kit → older fallbacks.
Do not overwrite the lofted or v04 files.

### F2 — turnaround read: vehicles, people and active equipment

| ID | Exact authoring/runtime paths | Required design | Priority |
|---|---|---|---|
| VEH-001 v05 | `Art/Models/Vehicles/mdl_fuel_truck_small_v05.fbx`; matching Resources prefab | Regional rigid fuel truck; separate wheels, cabinet doors, hose reel and hose root; yellow project livery | P1 |
| VEH-002 v05 | `Art/Models/Vehicles/mdl_baggage_tug_train_v05.fbx`; matching Resources prefab | Tug plus three articulated carts; separate wheels, tow pivots and removable baggage loads | P1 |
| VEH-003 v05 | `Art/Models/Vehicles/mdl_passenger_bus_apron_v05.fbx`; matching Resources prefab | Compact apron bus; separate wheels and doors; clear glazing and Coastal Blue accent | P1 |
| VEH-004 | `Art/Models/Vehicles/mdl_pushback_tug_v02.fbx`; `Resources/Airside/Prefabs/mdl_pushback_tug_v02.prefab` | Authored pushback tug with towbar connection and wheel pivots; replaces the current primitive prefab proof | P1 |
| CHR-001 | `Art/Models/Characters/mdl_ramp_crew_kit_v01.fbx`; `Resources/Airside/Prefabs/mdl_ramp_crew_kit_v01.prefab` | One low-detail shared rig with marshaller, fueler and ramp-worker material variants; high-vis accents; marshalling-wand sockets | P1 |
| CHR-002 | `Art/Models/Characters/mdl_passenger_kit_v01.fbx`; `Resources/Airside/Prefabs/mdl_passenger_kit_v01.prefab` | Six restrained silhouettes sharing one rig/material atlas; standing, walking and seated variants; no individual passenger simulation implied | P1 |

F2 assets mirror existing turnaround state only. People remain presentation
instances; they do not reserve zones or complete tasks.

### F3 — setting read: vegetation, boundary and landside modules

| ID | Exact authoring/runtime paths | Required design | Priority |
|---|---|---|---|
| VEG-001 | `Art/Models/Environment/mdl_eucalyptus_kit_v01.fbx`; `Resources/Airside/Prefabs/mdl_eucalyptus_kit_v01.prefab` | Three eucalyptus silhouettes plus two LODs, shared leaf atlas and simple trunk material; readable as a belt from overview | P1 |
| VEG-002 | `Art/Models/Environment/mdl_kingscote_scrub_kit_v01.fbx`; matching Resources prefab | Mallee/shrub/grass/rock cluster variants for fence and dune edges; no billboard-photo look | P1 |
| PRP-002 | `Art/Models/Props/mdl_airfield_fence_gate_kit_v01.fbx`; matching Resources prefab | Modular chain-link bay, corner, end, pedestrian gate and two-leaf vehicle gate; mesh uses alpha/cutout rather than hundreds of bars | P1 |
| PRP-003 | `Art/Models/Props/mdl_terminal_forecourt_kit_v01.fbx`; matching Resources prefab | Kerbs, bollards, planter, parking sign frame, bench and drop-off furniture matching the terminal scale | P2 |
| WLD-004 | `Art/Models/Environment/mdl_kingscote_context_terrain_v01.fbx`; matching Resources prefab | Low-poly terrain/coast/hill context with smooth silhouette and zones for grass, dry grass, sand and water; no real map data | P2 |

Road/runway/taxi/stand geometry remains code-owned for operational precision.
WLD-004 replaces only non-operational context and may never change reservation
paths or stand positions.

### F4 — reusable motion, effects and interface finish

| ID | Exact path | Required design | Priority |
|---|---|---|---|
| ANM-AIR-001…004 | Existing Batch D paths | Promote current phase-driven propeller, gear, door and light behaviour into reusable authored clips/controller without changing timing authority | P2 |
| ANM-VEH-001…004 | Existing Batch D paths | Reusable wheel/door/hose/cart clips mapped to task progress and abort-safe | P2 |
| VFX-001…004 | Existing Batch D paths | Prefabise current restrained smoke, heat, rain and wet response with performance tiers | P2 |
| UI-ICO-005 | `Art/UI/Icons/ui_system_{play,pause,speed,follow,overview,audio_on,audio_off,save}_v01.png` | Eight hand-cleaned 128 px transparent line icons; same 2 px visual weight and safe area as Batch E; legible at 24 px | P2 |

Panel fills, buttons, focus states, progress tracks and status chips should remain
UI Toolkit/uGUI/USS constructs. Do not generate raster screenshots for them.

## Shared 3D contract

- Unity scale is metres. Import at scale 1 and apply source transforms.
- Root pivot sits on the ground plane; aircraft longitudinal origin stays compatible
  with the existing movement root. Vehicle roots sit between axles.
- Preserve predictable child names used by presentation hooks:
  `propeller_left/right`, `gear_*`, `gear_door_*`, `door_fwd`,
  `cargo_door`, `wheel_*`, `hose_root`, `door_*`, `light_*`.
- Mesh renderers must not contain colliders. Add simple child collision proxies only
  where selection or camera obstruction needs them.
- P0/P1 assets ship with LOD0 and LOD1 plus a simple fallback/collider mesh.
  Small props may use one mesh when their screen size makes LOD pointless.
- Prefer shared materials and atlases. Default maximums: 2048² hero aircraft and
  terminal atlas; 1024² vehicle/character/vegetation atlas; 512² small prop atlas.
- Use alpha cutout for fence mesh and foliage; avoid transparent overdraw layers.
- Use URP Lit-compatible materials. No unique 4K texture per object, baked real
  brand, photo cut-out, watermark or unreadable micro-detail.
- Animated pivots are authored in the asset. Runtime code must not compensate for
  a bad pivot with per-model magic offsets.
- Each prefab key is `airside-prefab/<basename>`. Resources remains available
  until real Editor Addressables groups are built and verified.

## Integration hooks and fallbacks

| Asset family | Existing hook | Required fallback |
|---|---|---|
| AIR/BLD/VEH | `ArtPresentationLoader`, `PreferArtKit`, `BuildServiceVehicle` | Current preferred glTF, then primitive |
| CHR | `BuildApronLife` / `PlacePerson` | Existing block figures |
| VEG | `BuildVegetation` / `PlaceTree` / `PlaceShrub` | Existing sphere/cylinder vegetation |
| PRP fence/forecourt | `BuildPerimeterFence`, `BuildTerminalLandsideCanopy` | Existing procedural modules |
| WLD context | `BuildEnvironmentContext` | Existing textured blocks and relief |
| UI system icons | Toolkit/uGUI control elements | Existing text labels |
| Animation/VFX | Existing presentation update methods | Existing runtime behaviour |

## Acceptance criteria per slice

- Asset source, prefab, Unity `.meta`, evidence and register entry are committed.
- Every new prefab resolves through its Addressables key and direct Resources path.
- Packaged macOS build visibly uses it; deliberately removing it proves the fallback.
- Default overview, aircraft follow and landside-facing views are checked at day,
  dusk and night.
- Check 1280×720, 1440×900 and the development Retina resolution.
- No new unexplained magenta materials, z-fighting, hovering roots or moving-part
  pivot errors.
- P0/P1 replacement improves silhouette/material fidelity against REF-001/003/005,
  not merely mesh count.
- A 30-minute soak shows no material-instance leak or material increase without
  bound and no meaningful frame-pacing regression on the target Mac.
- Simulation, save, reservation, economy and seeded outcomes remain unchanged.

## Cursor handoff: first task

Batch F1 (AIR-001 v05, BLD-001 v05, MAT-001) is integrated on
`cursor/batch-f1-terminal-materials`. Next implementation tip after Bailey accepts
the packaged Mac playtest is **Batch F2** on a **new** branch. Do not begin F2–F4
in the F1 branch.
