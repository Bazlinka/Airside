# Changelog

One line per merged change, newest first. Update this in the same commit as the
change it describes.

## Unreleased

- **Toolkit HUD readability (0025 item 6).** Status panel gains a coastal accent
  bar and stronger type hierarchy (location/phase Open Sky, cash bold, coach
  urgent yellow vs calm Open Sky). Presentation only.
  Evidence: `scripts/test-domain.sh`.

- **Night glow flicker + hold-short polish (0025 items 5+7).** Building window
  PointLights and emissive quads flicker softly at dusk; hold-short bars C/D join
  the traffic-wait pulse with emission; touchdown-zone marks added beside aiming
  points. Presentation only. Evidence: `scripts/test-domain.sh`.

- **Runway/taxi markings + fence densify (0025 item 3).** Continuous runway edge
  stripes, taxi edge lines, apron lead-in chevrons, mid-span fence posts and gate
  chevrons. Coast boat bob no longer drifts yaw. Presentation only.
  Evidence: `scripts/test-domain.sh`.

- **Stand GSE deploy + hangar door dedupe (0025 item 7).** Stairs roll in from
  the apron edge before pitching up; chocks settle with a roll; when authored
  hangar door panels are present the greybox slab is hidden so doors do not
  double up. Presentation only. Evidence: `scripts/test-domain.sh` 105/105.

- **Hangar kit doors + coastal motion (0025 items 3+7).** Authored hangar
  `door_panel_*` / `door_rib_*` slide with the greybox slab (opens for day and
  active stand traffic); coast boats bob, foam pulses, jetty breathes.
  Presentation only. Evidence: `scripts/test-domain.sh`.

- **Wet materials + day profiles + coast densify (0025 items 3–5).** URP Lit wet
  variants darken albedo, flatten bump, raise gloss and enable clear-coat sheen;
  day volume adds ShadowsMidtonesHighlights + rain/fog/storm gloom; coast foam
  ribbon, service-lane markings and southern headlands. Tire binder no longer
  paints wheels as metal before Rubber. Presentation only.
  Evidence: `scripts/test-domain.sh`.

- **Authored airfield props kit.** `mdl_airfield_props_kit_authored_v01` (cylindrical
  poles/cones/dolly wheels) preferred ahead of v02/v01 for PlaceWorldProps.
  Evidence: `scripts/test-domain.sh` 105/105.

- **Apron lighting + probes for authored kits (0025 item 5).** Apron floods are
  SpotLights aimed at stands/hangar/terminal; terminal gets its own realtime
  reflection probe; apron probe box expanded. Authored airfield lighting kit
  (`mdl_airfield_lighting_kit_authored_v01`, cylindrical poles/lenses) preferred
  ahead of v02/v01. Presentation only. Evidence: `scripts/test-domain.sh` 105/105.

- **Authored PRP-001 service equipment kit.** `mdl_service_equipment_kit_authored_v01`
  (stairs/chocks/GPU extract names + denser rails/treads/wheels) preferred ahead of
  v02/v01 for stand GSE mesh extraction. Evidence: `scripts/test-domain.sh` 105/105.

- **Material binder fix for authored kits.** `AirsideRuntimeMaterialBinder` no
  longer treats every name containing `cabin` as glass (CabinDoor was wrong);
  fuselage/wing/tire kinds route correctly; InferFromMeshName covers authored
  mullions, canopy posts, hangar door panels and vehicle tanks. Presentation only.

- **Authored service vehicles (VEH-001…003).** Fuel truck (cylindrical tank), baggage
  tug train and apron bus ship as `*_authored_v01` FBX + glTF + Resources prefabs
  with PreferArtKit ahead of v04. Completes the Batch C first-playable model set
  on the authored track. Evidence: `scripts/test-domain.sh` 105/105.

- **Authored ops shed + full building Resources set.** `mdl_operations_shed_authored_v01`
  joins turboprop/terminal/hangar with FBX + glTF + Resources prefab so
  `airside-prefab/*_authored_v01` covers AIR-001 and BLD-001…003. PreferArtKit
  prefers authored. Evidence: `scripts/test-domain.sh` 105/105; art sync updated.

- **Authored Resources prefabs + hangar kit.** Addressables keys
  `airside-prefab/mdl_regional_turboprop_01_authored_v01`,
  `…/mdl_terminal_regional_small_authored_v01`, and
  `…/mdl_hangar_small_authored_v01` resolve via Resources (round fuselage /
  canopy / hangar columns; gear/cargo/prop names for motion). FBX sources +
  companion glTF remain; ModelImporter bake can overwrite later. PreferArtKit
  prefers authored → prior kits → primitives. Evidence: `scripts/test-domain.sh`
  105/105; art sync 145 files.

- **Authored FBX turboprop + terminal (0025 item 2).** Distinct
  `mdl_regional_turboprop_01_authored_v01` and
  `mdl_terminal_regional_small_authored_v01` ship as Unity-importable `.fbx`
  (lathed fuselage / cylindrical engines; canopy posts) plus companion glTF for
  StreamingAssets. PreferArtKit prefers authored → lofted/v04 → … → primitives.
  Mac menu **Airside → Art → Bake Authored FBX Prefabs** can replace Resources
  prefabs with ModelImporter meshes. No simulation change.
  Evidence: `scripts/test-domain.sh`; art sync 143 files.

- **Motion, brand overlays, wet/coast polish (0025 items 4–5+7–8).** Gear doors
  animate separately from struts; cargo doors open at stand; soft coastal ambient
  audio joins wind/rain; wet response covers more ground surfaces and refreshes the
  apron reflection probe; night bloom/grain capped to avoid smear; Toolkit pause /
  away / insolvency overlays use the BRD-001 wordmark. Presentation only.
  Evidence: `scripts/test-domain.sh` 105/105; `scripts/test-unity.sh` 116/116;
  Play soak: gear/cargo doors in hierarchy, BRD-001 wordmark on HUD + 3 overlays.

- **Addressables Resources provider + lofted turboprop + env densify (0025 items 1–3+6).**
  Runtime `airside-prefab/<key>` keys now load via `AirsideResourcesProvider`
  (`Resources.Load`) instead of the missing LegacyResourcesProvider stub — StreamingAssets
  glTF and direct Resources fallbacks stay intact. Hero aircraft prefers distinct
  `mdl_regional_turboprop_01_lofted_v01` (79 stepped-fuselage meshes; does not race
  `*_v04`). South fence, denser vegetation belt, access-road shoulders; Toolkit HUD
  early-outs residual IMGUI when active. Evidence: `scripts/test-domain.sh` 105/105;
  `scripts/test-unity.sh` 116/116; Play soak loaded lofted kit, denser env, Batch C/WLD/PRP.

- **Unity 6.3 HUD startup: font + PanelSettings theme.** Editor Play threw
  `ArgumentException: Arial.ttf is no longer a valid built in font` while building
  the Canvas HUD; the packaged player logged `No Theme Style Sheet set to
  PanelSettings` for the runtime Toolkit UIDocument. Canvas text now loads
  `LegacyRuntime.ttf`, and Toolkit PanelSettings assigns
  `Resources/Airside/UI/AirsideRuntimeTheme.tss` (imports Unity's default theme).
  Play soak 2026-09-07: zero Arial / PanelSettings warnings. No simulation or art-path changes.

- **Verified decision 0025 packaged-art delivery on a real macOS build.** From clean
  `main` at `33a961a`, the art sync copied 137 files without repository drift,
  Unity 6000.3.23f1 passed 116/116 EditMode tests, and the Mac build succeeded.
  Every built StreamingAssets art file matched its source hash; the player visibly
  used Batch C model geometry, WLD/PRP lighting and markings, and Batch B aircraft
  surfaces instead of primitive fallbacks. Full Editor/player parity and Batch E
  remain unverified because Editor Play throws on removed built-in `Arial.ttf` at
  `AirsideCanvasHud.cs:1000`; no art-path missing-file error was logged.

- **Anti-aliasing and the template post-processing profile.** The game shipped with
  no anti-aliasing at all: `PC_RPAsset` had `m_MSAA: 1` and nothing set camera
  antialiasing — on a world made entirely of hard box edges and thin poles. MSAA is
  now 4x and the camera runs SMAA (high). Separately, Unity's template
  `DefaultVolumeProfile` is still wired as URP's global default and overrides
  DepthOfField, MotionBlur, LensDistortion, ChromaticAberration, ScreenSpaceLensFlare
  and PaniniProjection (alongside literal `CopyPasteTestComponent1/2/3` and
  `TestVolume`). Harmless while post-processing was off; rendering since
  `AirsideDayVolume` switched the post stack on. Each is now pinned to its no-op value
  in the day volume's own profile. Film grain is untouched — that one is the day
  volume's, and stays day-driven.

- **`runInBackground`.** A real-time, persistent simulation froze whenever the window
  lost focus — clock, flights and economy all stopped mid-session.

- **Four simulation bugs that contradicted the design brief.** Ground traffic taxied
  to and parked on Stand 3 before the player built it (`AlternateStand` walked
  Stand 1/2/3 with no knowledge of `Capacity.StandCount`, so with both baseline
  stands occupied GT-201 reserved `LEAD-IN-3` and parked on bare ground); it now
  holds off-field, leaving the corridor free, when every built stand is taken.
  Understaffing delays were reported with an empty cause, against the brief's "every
  delay should have an understandable cause" — `TurnaroundWorkflow.OverrunCause` now
  names cleaning disruption, understaffing or a generic extended turnaround. The
  daily finance brief halved the moment a second commercial started operating,
  because the projection assumed one aircraft. And a player command issued at
  simulation second 0 was dropped on reload — `ReplayTo` advances the clock then
  applies commands at the new second, so it never visited second 0, replaying the
  cash it cost but not its effect. Eight new EditMode tests, each verified failing
  against the previous code.

- **Fixed the main build.** `AirsidePrefabAddressables` implements `IResourceLocator`
  but did not import `UnityEngine.AddressableAssets.ResourceLocators`, so every
  EditMode run and player build had been failing with CS0246 since PR #97.

- **Aircraft skin PBR + wider livery coverage (0025 item 4).** Authored
  `tx_aircraft_skin_*` maps for AircraftSkin materials; livery decals cover
  segmented fuselage/nose parts on denser turboprop kits. Presentation only.

- **Denser WLD/PRP kits v02 (0025 item 2).** Prefer `*_v02` airfield lighting
  (multi-part flood masts, edge/taxi/obst), props and service equipment kits over
  thin v01 Batch B boxes. Presentation only.

- **ARFF shed prefab + textured distant hills (0025 items 1+3).** Resources
  `mdl_arff_shed_v01` replaces flat rescue-shed blocks; distant hills are segmented
  and grass/sand-mapped so the horizon is not four unlit slabs. Presentation only.

- **Apron safety props + stand boxes (0025 items 1+3).** Resources
  `mdl_fire_hydrant_v01`, `mdl_extinguisher_cabinet_v01`, `mdl_fod_bin_v01` on the
  apron edge; painted stand bay boxes for stands 1–3. Presentation only.

- **Coast sand + water PBR surfaces (0025 item 4).** Authored `tx_sand_coast_*` and
  `tx_water_coast_*` basecolour/normal/AO/mask maps on coast strip and dunes;
  material library wires Sand/Water stems. Presentation only.

- **Landside furniture + coast boat prefabs (0025 items 1+3).** Resources
  `mdl_luggage_trolley_v01`, `mdl_landside_bench_v01`, `mdl_coast_boat_v01`; denser
  car-park stall lines, kerbs, drop-off zebra and parking sign. Presentation only.

- **Landside parked-car Resources prefab (0025 items 1+3).** `mdl_parked_car_v01`
  fills car-park bays and kerbside drop-off (tinted body colours; Addressables key
  auto-registered). Presentation only.

- **Denser runway edge lights + REIL blink (0025 items 3+5).** Edge PointLights every
  8 m, more taxi centreline lamps, REIL flashers at both thresholds, and a second
  hold-short pair. Presentation only.

- **Batch C v04 denser hero kits (0025 item 2).** Prefer `*_v04` turboprop (68 meshes:
  winglets, gear doors, spoilers, window panes), terminal/hangar/ops and service
  vehicles; spoilers deploy on landing. Still procedural greybox. Presentation only.

- **Readable runway threshold digits (0025 item 3).** Block-style 09 / 27 markings plus
  side threshold stripes so runway ends read from overview/follow. Presentation only.

- **Terminal landside canopy (0025 item 3).** Steel posts, soffit slab, landside glass
  curtain, entrance doors, bench/planter and dusk canopy under-glow so the terminal
  entrance reads from landside and overview. Presentation only.

- **ALS chase flash + ARFF lightbar blink (0025 items 5+7).** Approach lamps run a
  far-to-threshold sequence at night; ARFF truck lightbar pulses amber/red at dusk.
  Presentation only.

- **ARFF truck Resources prefab (0025 items 1+3).** `mdl_arff_truck_v01` parks on the
  rescue apron in front of the ARFF shed (Addressables key auto-registered).
  Presentation only.

- **Perimeter fence, ALS bars + ARFF shed (0025 items 3+5).** Chain-link style multi-rail
  fence with open vehicle gate; five approach light bars west of threshold with night
  PointLights; red ARFF rescue shed + bay spill. Presentation only.

- **UI Toolkit overlays (0025 item 6).** Runtime `AirsideToolkitHud` owns briefing
  (dawn splash), pause, away summary and insolvency overlays with action buttons;
  Canvas HUD is now a fallback only when Toolkit fails to build. Presentation only.

- **UI Toolkit left status panel (0025 item 6).** Runtime `AirsideToolkitHud` now owns
  the full left status/decision panel (cash, turnaround, crew, stands, research,
  coach, wait meter) with action buttons; Canvas keeps overlays only and hides its
  left copy. Presentation only.

- **UI Toolkit OPERATIONS + route offer (0025 item 6).** Runtime `AirsideToolkitHud`
  now owns the OPERATIONS panel and Accept/Decline route card (pulse accent, daily
  report); Canvas keeps left status + overlays and hides its right-column copies.
  Presentation only.

- **Hangar bay props + runway aiming points (0025 items 1+3).** Resources prefab
  `mdl_hangar_bay_props_v01` (workbench/shelves/drum/cart) fills the open hangar;
  white aiming-point pairs on the runway. Presentation only.

- **UI Toolkit toasts + aircraft nav/beacon lights (0025 items 5+6+7).** Runtime
  `AirsideToolkitHud` (UIDocument) owns ops/research toasts and the Saved chip;
  Canvas keeps panels/overlays. Wingtip nav and anti-collision beacon cast real
  PointLights. Presentation only.

- **Addressables-first Resources locator (0025 item 1 / ADR 0026).** Runtime
  `AirsidePrefabAddressables` exposes every `Resources/Airside/Prefabs` asset under
  `airside-prefab/<key>` via LegacyResourcesProvider; loader prefers Addressables
  then Resources then glTF. Presentation only.

- **Landing skids + window PointLights + wet taxi spray (0025 items 5+7).** Rubber
  skid streaks fade after touchdown; denser smoke puffs; terminal/hangar/ops window
  PointLight spill at dusk; amber fuel-farm lamp; mist spray under gear when wet.
  Presentation only.

- **Parked GA prefab + denser apron life (0025 items 1+7).** Resources prefab
  `mdl_parked_ga_v01` (three parked GA on the west apron); more staff/passenger
  silhouettes with walker shuffle, marshaller arm wave and stride. Presentation only.

- **Phase-aware follow camera + fuel farm prefab + night windows (0025 items 1+5+7).**
  Follow framing/FOV lean into taxi, stand, approach, landing and takeoff; Resources
  prefab `mdl_fuel_farm_v01`; stronger terminal/hangar/ops window emission at dusk.
  Presentation only.

- **Sign/dolly/windsock prefabs + cabin glow + GSE headlights (0025 items 1+5+7).**
  Resources prefabs for airside sign, baggage dolly and windsock pole; denser apron
  placement; cabin/cockpit emissive at night/stand; fuel/baggage/bus/tug headlamp
  SpotLights. Presentation only.

- **Apron GSE prefabs + aircraft landing SpotLights (0025 items 1+5+7).** Resources
  prefabs for pushback tug, safety cone and work barrier (denser apron placement);
  approach/landing/takeoff landing lamps and night taxi lamps cast real SpotLights.
  Presentation only.

- **GSE prefabs + wet apron puddles (0025 items 1+4+7).** Resources prefabs for wheel
  chocks and GPU cart; soft reflective puddle discs appear on wet weather. Presentation only.

- **First Resources prefab + Addressables try (0025 item 1).** `mdl_passenger_stairs_v01`
  lands under `Resources/Airside/Prefabs/` with runtime Lit binder; loader probes
  `airside-prefab/<key>` Addressables before glTF. Presentation only.

- **Bird wing flaps (0025 item 7).** Coastal flock rebuilt as body + hinged wing quads
  (18 birds) with flap animation; denser orbit over the shore. Presentation only.

- **Runway edge PointLights (0025 item 5).** Sparse warm point lights every 12 m along
  both runway edges plus green taxi centreline hints; day-cycle intensity. Presentation only.

- **Cloud umbras + building contact shadows (0025 items 3+5).** Soft ground discs drift
  under cloud bands; terminal/hangar/ops/car-park contact blobs ground Lit surfaces;
  night film grain on the day Volume. Presentation only.

- **Denser landside vegetation (0025 item 3).** More eucalyptus belts, dual-canopy trees,
  shrub clusters and a tighter coastal scrub strip so overview reads as KI bush, not a
  sparse prop ring. Presentation only.

- **Canvas research toast + save indicator (0025 item 6).** Research-complete banner
  (top), ops toast (bottom) and Saved chip move onto runtime uGUI; IMGUI gated when
  Canvas is active. Presentation only.

- **Threshold approach lights + apron reflection probe (0025 item 5).** Point lights at
  both runway ends plus a compact PAPI ladder; realtime apron ReflectionProbe so wet
  Lit surfaces pick up floods at night. Presentation only.

- **Batch B surface PBR maps (0025 item 4).** Authored normal / AO / metallic-smoothness
  masks for asphalt, concrete, grass and corrugated metal; `AirsideMaterialLibrary`
  prefers them via StreamingAssets with procedural fallbacks. Presentation only.

- **Batch C v03 denser kits (0025 item 2).** Procedural turboprop/terminal/hangar/ops/
  GSE kits with more segmented parts; runtime prefers v03→v02→v01; flaps/ailerons/
  elevators animate. Still greybox — not authored meshes. StreamingAssets synced.

- **Canvas away + insolvency overlays (0025 item 6).** Welcome-back summary and
  insolvency cards move onto runtime uGUI; IMGUI duplicates gated when Canvas is
  active. Presentation only.

- **Prefab/Addressables art loader scaffold (0025 item 1–2 / ADR 0026).**
  `ArtPresentationLoader` prefers `Resources/Airside/Prefabs/<kit-basename>` then
  StreamingAssets glTF; Addressables package added to the Unity manifest; wet
  material variants centralized; Canvas owns opening briefing + pause overlays.
  Presentation only.

- **Terrain micro-relief (0025 item 3).** Grass berms, scattered mounds and coastal
  dunes break the flat ground slab so overview reads as a regional airfield site.
  Presentation only.

- **Atmospheric fog + apron life figures (0025 items 5+7).** Soft exponential fog on
  clear days (weather still thickens it); seven stylised staff/passenger silhouettes
  on apron and landside with idle lean and marshaller wave on approach. Presentation only.

- **URP day volume + richer GSE motion (0025 items 5+7).** Runtime global Volume with
  ACES tonemap, day-driven color/exposure, bloom and vignette; service vehicles park
  on the apron and drive into stand tasks; stairs/chocks deploy; GPU and beacons pulse.
  Presentation only — not a full probe bake.

- **Hangar bay interior light.** Warm point light inside the hangar brightens with
  the sliding door by day and keeps a soft night glow when closed. Presentation only.

- **Canvas OPERATIONS panel (0025 item 6).** Runtime uGUI hosts the OPERATIONS log
  (routes, ground traffic, event tail) and daily report under the route offer;
  IMGUI ops panel gated when Canvas is active. Presentation only.

- **Canvas left HUD migration (0025 item 6).** Runtime uGUI left panel now owns
  status, coach, wait meter, turnaround tasks, and hire / release / stand /
  research / priority-crew buttons; IMGUI left panel is gated when Canvas is
  active. Operations log remains IMGUI. Presentation only.

- **Sky bird flock + hangar field restore.** Twelve presentation birds orbit the
  southern coast; also restores `_hangarDoor` / `_touchdownSmokeRemaining` fields
  dropped during the Canvas HUD wiring. Presentation only.

- **Ambient wind + rain audio (0025 item 7).** Soft looping wind bed and rain/
  storm ambience that respect mute and pause; procedural clips, presentation only.

- **Canvas HUD foundation (0025 item 6).** Runtime uGUI Canvas hosts the route
  offer panel and ops toast with EventSystem + Input System UI module. Status /
  research remain IMGUI until the interactive left panel migrates. Presentation only.

- **Hangar door day/night slide.** Hangar door slab opens through the day and
  closes at night (presentation only; always placed even when the hangar kit
  provides an opening).

- **Coast jetty + fishing boats.** Timber jetty into the shallows and three boat
  silhouettes so the southern KI shoreline reads as a living coast. Presentation only.

- **HUD contrast polish (0025 item 6 interim).** Panel fill 94% opaque, Coastal Blue
  panel frames, larger body type, themed progress bar for the first-offer wait,
  framed OPERATIONS panel. Still IMGUI — Toolkit/uGUI rebuild remains next.

- **Ground shadows + drifting cloud bands.** Soft elliptical shadows under
  commercials/ground traffic that soften with altitude; ten translucent cloud
  blobs drift east and tint with day/dusk. Presentation only.

- **URP material library spike (0025 item 4).** Shared `AirsideMaterialLibrary`
  profiles (asphalt/concrete/grass/metal/aircraft/glass/rubber) with procedural
  normal + soft AO maps; glTF kits and CreateBlock route through it. Not a full
  authored PBR set — Addressables still the production path. Presentation only.

- **Landside streetlights.** Six poles along the access road and car park with
  warm point lights that come up at dusk/night. Presentation only.

- **More building night glow.** Ops shed + terminal landside window spill; night
  glow quads now emit so dusk/night interiors punch through. Presentation only.

- **Control surfaces + touchdown camera shake.** Rudder/elevator (and soft
  tailplane) deflect with attitude; follow camera pulses on commercial
  touchdown with the existing smoke/chirp. Presentation only.

- **Aircraft attitude pitch + turn bank.** Takeoff nose-up, approach pitch and
  landing flare; gentle bank into turns for commercials and ground traffic.
  Presentation only (Batch D motion life).

- **First-session UX polish.** Enter accepts a ready route offer; waiting countdown
  + progress bar before the first airline; taller Accept button with pulse stripe
  on the first-decision panel; clearer empty OPERATIONS copy. Presentation only.

- **Denser apron props + fuel farm + parked GA.** More cones/barriers/signs/dollies,
  four apron floodlights, a small fuel farm west of the hangar, and two static GA
  aircraft so the airfield reads busier from overview. Presentation only.

- **Prop disc blur + tire roll (0025 item 7).** High-RPM takeoff/approach hides
  blade meshes and shows a translucent prop disc; landing-gear tires roll on
  ground phases. Presentation only (Batch D motion life).

- **Landside parking life.** Parked cars in the car park, kerbside drop-off/taxi,
  luggage trolleys, bench, extra trees and GA tie-down markers so the terminal
  approach reads as a working regional airfield. Presentation only.

- **Lighting profiles + wet surface gloss.** Cool fill light opposite the sun,
  horizon dome follows sky colour, nav/edge lights emit at dusk/night, and wet
  weather darkens/glosses paved surfaces (VFX-004 greybox). Presentation only.

- **Follow-camera framing.** Look-ahead along aircraft heading, altitude-based
  distance/pitch, and gentle yaw ease so F-follow fills the frame for taxi and
  flight. Overview (O) restores the default pitch. Presentation only.

- **glTF kit UVs + building surface textures.** `ArtGltfLoader` generates planar
  UVs so Batch B basecolours tile on box kits; hangar/ops/terminal meshes get
  corrugated/concrete textures with soft URP Lit response. Presentation only.

- **Left HUD sequential layout + first-decision coach.** Status panel rows no longer
  overlap; height shrinks in the first session; the coach tip uses a Safety Yellow
  stripe when a route offer needs Accept. Presentation only.

- **Richer Batch C v02 kits (0025 item 2).** Procedural `*_v02.gltf` turboprop,
  terminal, hangar, ops shed and service vehicles with more readable parts.
  Runtime prefers v02 and falls back to Approved v01; StreamingAssets synced.
  Presentation only; `scripts/test-domain.sh` unchanged in behaviour.

- **Regional airfield environment greybox (0025 item 3).** Outer paddock, coast
  sand/shallows, access road + car park, perimeter fence, eucalyptus clumps,
  distant hills and a soft horizon dome. Hangar/ops/terminal glTF kits now get
  Batch B surface textures when present. Presentation only; simulation unchanged.

- **Warm key / cool ambient lighting pass.** Soft directional shadows, warmer sun at
  day/dawn, cooler ambient fill, Open Sky camera backdrop. Presentation only —
  not a full URP post stack. `scripts/test-domain.sh` 97/97.

- **Approve and integrate brand wordmark + dawn splash.** Bailey Approve for BRD-001
  and UI-ILL-001; promoted into `Art/Brand` and `Art/UI/Illustrations`, synced to
  StreamingAssets, drawn on the opening briefing (full-bleed splash) and left HUD
  title. Also lands `.cursor/environment.json` for the headless domain harness
  (supersedes draft PR #34). `scripts/test-domain.sh` 97/97.

- **Fix packaged-build art loading (StreamingAssets).** Runtime glTF/PNG loaders
  resolve via `ArtRuntimePaths` (StreamingAssets first, Editor Assets fallback).
  Synced 63 art files into `Assets/StreamingAssets/Airside/Art` with
  `scripts/sync-art-streaming-assets.sh`. Records Bailey's ~20% visual assessment
  and presentation backlog as decision **0025**. Does not replace placeholder
  models — only stops art from silently vanishing in player builds.
  `scripts/test-domain.sh` 97/97.

- **First route-income consequence toast.** When the first accepted-route payout hits
  cash, a HUD toast confirms the decision→money loop. Presentation only.
  `scripts/test-domain.sh` 97/97.

- **First-session HUD declutter.** Until the first route is accepted, hide hire/release
  crew, stand-3 build and research start controls; show one unlock line instead so the
  route offer stays the only early decision. Presentation only. `scripts/test-domain.sh` 97/97.

- **First-session countdown tip and auto-follow on Begin.** Coach line counts down to
  the first route offer; dismissing the opening briefing starts camera follow on the
  lead commercial so the aircraft cycle is visible immediately. Presentation only.
  `scripts/test-domain.sh` 97/97.

- **First-session offer pacing and HUD priority.** First route offer arrives at 12s
  (was 25s); pending offers pin above the OPERATIONS panel so detail never buries
  the decision; first-offer coach tip uses caution colour. Domain constant +
  Presentation. `scripts/test-domain.sh` 97/97.

- **First-session opening briefing and clean new-game path.** New games (no away
  report, no accepted routes) open paused on a role + first-decision briefing;
  first route offer is labelled FIRST DECISION with consequence toasts on offer and
  accept; Pause / Insolvent offer Start new airport (wipes save and reloads).
  Presentation only. `scripts/test-domain.sh` 97/97.

- **Add review-ready brand and splash candidates.** Added the transparent
  `airside_wordmark_light_v01.png`, the 3840×2160
  `ui_splash_airport_dawn_v01.png`, exact generation evidence, and a compact AI
  image reference index. Both remain candidates until Bailey approves them;
  neither is wired into Unity.

- **Focus product plan on the Mac first playable.** Replaces the broad blueprint with
  a delivery plan that makes the first playable the only active target; defers companion,
  CloudKit, cargo/GA and extra art batches until external playtest confirms the loop.

- **Pulse hold-short markings during traffic waits.** When the traffic wait monitor
  warns, hold-short bars flash Safety Yellow → orange so the delay cause is visible
  in-world, not only on the HUD. Presentation only. `scripts/test-domain.sh` 97/97.

- **Touchdown chirp and day-est income icon.** Soft procedural squeal on landing
  transition plus income icon on the day-estimate HUD line. Presentation only.
  `scripts/test-domain.sh` 97/97.

- **Batch D runtime alive-airport + first-session coach tips.** Phase-based prop RPM,
  soft gear retract, split landing/taxi lights, dual touchdown smoke, denser storm rain,
  stronger engine heat on takeoff/approach, ground-traffic props/lights/heat, wet Stand 3
  apron, route-offer icon, and contextual left-panel tips. Presentation only — Unity
  .anim/.prefab files remain Planned; runtime behaviour Integrated. `scripts/test-domain.sh` 97/97.

- **Restore Unity macOS compilation.** Qualify Unity Object calls and include the
  built-in image conversion module required by PNG loading. Retain Unity-generated
  metadata for the new UI assets. Unity 6000.3.23f1 macOS build succeeded and all
  107 EditMode tests passed on 2026-09-07; live visual verification remains pending.

- **Integrate Batch C models, WLD kits, and Batch E cleanup.** Runtime
  `ArtGltfLoader` loads Approved Batch C / WLD / PRP glTF kits from disk with
  primitive fallbacks (aircraft, buildings, vehicles, service gear, markings,
  lights, apron props). Regenerated UI-ICO-003 service icon sheet and UI-PNL-002
  dark panel (~89% mean alpha); HUD draws operation/economy/service icons and
  prefers the dark panel. Presentation only; `scripts/test-domain.sh` 97/97.
  Unity Play Verified still Bailey-owned.

- **Merge wave: Approve Batch C/E and land overnight polish + Batch E HUD.** Bailey
  Approve recorded for Batch C models and Batch E UI. Merged draft PRs #19–#30
  (and #31 Approve) onto `main`: WLD greybox, Stand 3 traffic fix, insolvency HUD,
  follow cycle, research bar/mute, toasts, status colours, autosave chip, beacon,
  pause overlay, and Batch E icon/panel runtime wiring. Domain harness should be
  re-run on `main` after the wave.

- **Integrate Batch E UI candidates into the runtime HUD.** Verified all 7 candidates
  against their recorded SHA-256 hashes: `ui_service_icon_sheet_v01.png` is corrupted
  as committed (invalid PNG signature, hash mismatch — needs regeneration) and
  `ui_panel_9slice_dark_v01.png` measures ~9% average alpha (max 56%), too faint for
  the "WCAG-aware contrast" it was specified for, so the HUD keeps its existing
  procedural Runway Ink panel instead of regressing to it. The other 4 verified
  correct: sliced the weather/operation/economy icon sheets (7 icons each, single row,
  real alpha) into 21 individual files under `Art/UI/Icons/`, and copied the alert
  stripe and light panel into `Art/UI/Panels/`. Added `AirsideTheme.Icon`/`WeatherIcon`/
  `AlertStripeBackground`/`CautionStyle` (all fallback-safe if a file is missing); the
  HUD now draws the weather icon live and uses the alert stripe behind caution text.
  Done on Bailey's direct instruction, ahead of the usual formal-approval gate for new
  runtime art — still needs a Unity Play check. See the integration review in
  `docs/art/prompts/batch-e-ui-generation-2026-09-06.md`. Presentation only; no
  simulation code changed; `scripts/test-domain.sh` 96/96 pass (unaffected).
- **Pause overlay and speed caution colour.** While paused, a translucent dimmer
  and centred PAUSED chip appear (hidden under the away summary). 4× speed and
  pause tint the clock line Safety Yellow. Presentation only.
  `scripts/test-domain.sh` 96/96.
- **Ops-event toast.** When the operational event log gains an entry, a Clear Green
  chip flashes the latest flight · title for ~4.5s (skips history already present
  on load). Presentation only. `scripts/test-domain.sh` 96/96.
- **Aerodrome beacon and dual-flight phase HUD.** Night white/green pulsing
  aerodrome beacon mast (presentation greybox). Dual commercials show per-aircraft
  phase + countdown on the HUD; FormatPhase covers the full operation cycle.
  `scripts/test-domain.sh` 96/96 (Presentation not covered).
- **Autosave indicator.** After each autosave (and pause/quit saves), a short
  Clear Green "Saved" chip appears bottom-right for ~1.6s. Presentation only.
- **HUD status colours for cash, reputation and day estimate.** Negative cash and
  negative day-est. use Signal Red; Trusted reputation and healthy day-est. use
  Clear Green; Provisional reputation and ≤3-day cash runway use Safety Yellow.
  Presentation only. `scripts/test-domain.sh` 96/96 (Presentation not covered).
- **Research-complete HUD toast.** When `AirportResearch` finishes a project, the HUD
  shows a centred Clear Green banner for eight unscaled seconds naming the unlock
  and its permanent bonus. Presentation only — driven by `LastCompletedProjectId`
  after each sim tick. `scripts/test-domain.sh` unchanged (Presentation not covered).
- **Research progress bar, engine mute, Stand 3 presentation Z.** While a research
  project is active the HUD draws a Coastal Blue progress bar under the research
  line (`AirportResearch.Progress01`). Press **M** to mute engine loops; engines
  also drop to a quiet idle volume when paused or when props are off. Commercial
  aircraft and service vehicles at Stand 3 now use `AirportTaxiNetwork.StandZ`
  instead of the old Stand-1/2 ternary (presentation only). Help line lists mute.
  `scripts/test-domain.sh` unchanged (Presentation not covered); needs Play check.
- **Cycle the follow camera across dual commercials.** With two aircraft on the field,
  pressing F while already following advances to the next commercial (and wraps). First
  F still enters follow; O returns to overview. Help strip updated. Presentation only;
- **Show insolvency on the HUD.** Simulation already froze after three consecutive negative
  day closes, but the player only saw frozen cash with no explanation. Presentation now
  turns cash Signal Red when negative, warns on consecutive negative closes, auto-pauses
  visuals when insolvent, blocks ops hotkeys, and shows a centred AIRSIDE insolvency
  overlay. Presentation only — `scripts/test-domain.sh` 96/96.
- **Fix ground-traffic Stand 3 circuit.** Arrive/depart fleet aircraft mapped any non-Stand-1
  target onto Stand 2's lead-in and apron Z, so a Stand 3 assignment reserved the wrong
  taxi segment and parked at Stand 2's position. `BuildCircuit` now uses
  `AirportTaxiNetwork.LeadInFor` / `StandZ` for all three stands. Regression:
  `GroundTraffic_WhenStandsOneAndTwoAreBusy_UsesStandThreeLeadInAndPosition`.
  `scripts/test-domain.sh` 97/97.
- **Overnight WLD greybox + miniature look polish (presentation only).** Places Approved
  Batch B WLD intent with primitive stand-ins: animated windsock, threshold / hold-short
  markings, taxi edge lights, obstruction lights, cones, barriers, airside sign and
  dollies; stand equipment (stairs, chocks, GPU, pushback tug) tracks turnaround /
  pushback; Stand 3 apron pad appears when built; aircraft use `AirportTaxiNetwork.StandZ`
  (fixes Stand 3 drawing on Stand 2); tighter camera FOV / overview framing; dusk sky and
  ambient; away-summary branded to AIRSIDE with cash colour; night terminal/hangar window; night terminal/hangar window glow; painted stand digits; stylised runway end designators.
  glow; painted stand digits. `scripts/test-domain.sh` 96/96. Needs Unity Play soak.
  Does not integrate Batch C/E assets.
- **Approve Batch C models and Batch E UI candidates.** Bailey confirmed the
  Generated/Modelled Batch C set (AIR/BLD/VEH/PRP) and Batch E icon/panel
  candidates. Status moved to Approved in the art register. Integration
  (runtime wiring) follows; primitives remain fallback until Integration is
  Verified. Decision 0022 lifecycle unchanged.
- Generated Batch E UI candidates under `docs/art/candidates/`: four transparent
  seven-icon sheets plus light/dark nine-slice and caution-stripe textures. Every
  request repeats the decision-0022 anchor; exact prompts and processing evidence
  are recorded. Candidates are not approved, imported or used by runtime code.
- **Apply the approved Airside palette to the runtime HUD.** The REF-004 operations
  HUD reference (ChatGPT-generated, Approved) specified translucent Runway Ink
  panels, Cloud text, Coastal Blue for buttons, Safety Yellow for caution, Clear
  Green for on-time and Signal Red reserved for delay — none of which had reached
  `AirsidePrototype.OnGUI()`, which still rendered on Unity's plain default grey
  IMGUI skin. Added `AirsideTheme` (the palette from
  `docs/art/ART_DIRECTION_AND_ASSET_SPEC.md`, a themed panel background texture,
  and themed label/button styles) and wired it through every panel, label and
  button in the HUD, the route-offer card and the away-summary popup. Delay text
  is Signal Red, on-schedule/understaffed/caution states use Clear Green/Safety
  Yellow, buttons use Coastal Blue. Presentation only — no simulation or save
  behaviour changed; `scripts/test-domain.sh` 96/96 pass (this file has no
  EditMode coverage, since IMGUI rendering isn't unit-testable without Unity —
  needs a Play-mode check on the next Unity session).
- **Fix a zero-seed crash-on-save landmine.** `AirsideSaveData.Validate()` treats
  `randomSeed == 0` as corruption (rejecting the save and falling back to the
  previous snapshot), but `PersistentAirportSession.LoadOrCreate` would happily
  persist a literal 0 if ever called with a zero `newGameSeed` — the very next
  autosave would then throw `InvalidOperationException` and never recover.
  Unreachable today (the only call site is a hardcoded non-zero literal), but a
  real landmine for any future random seed source. Remapped 0 to a fixed
  non-zero fallback at creation, the same way `SeededRandomSource` already
  tolerates a zero seed internally. Added
  `NewGameWithZeroSeed_SavesWithoutThrowingAndPersistsANonZeroSeed`, confirmed
  it reproduces the crash without the fix. 96/96 tests pass.
- **Fix dead/confused branch in `AirportResearch.Progress01`.** The `!IsResearching`
  path had an unreachable condition (always evaluated false given the guard above
  it) that only ever mattered if a future caller queried progress outside of
  `IsResearching` — no current call site does. Simplified to what it actually
  computed (1.0 only when both projects are complete, 0.0 otherwise) and added
  `Progress01_WhenIdle_ReflectsOnlyWhetherBothProjectsAreComplete`, the first
  test coverage for that branch. No behaviour change for any current caller;
  95/95 tests pass.
- **Headless Domain/Simulation/Persistence test harness.** `scripts/test-domain.sh`
  runs the 94 EditMode NUnit tests via `dotnet test` against a hand-authored
  `scripts/dotnet-harness/Harness.csproj` that compiles Domain/Simulation/
  Persistence straight from the Unity project (a `HarnessSaveRepository.cs`
  stands in for the one file that needs `UnityEngine.JsonUtility`, using
  `System.Text.Json` with `IncludeFields = true` instead). Supplementary to
  `scripts/test-unity.sh`, not a replacement — Presentation and the real Unity
  compile still need a Mac editor. No simulation code changed; 94/94 pass.
- **Playtest HUD and taxi visuals.** HUD scaling uses resolution-aware `HudLayout`
  (Retina-safe). Taxi drawing follows reservation segment windows; yielded ground
  traffic snaps to its hold point instead of lerping through released space.
  EditMode layout/taxi visual regressions added. Simulation rules unchanged.

- **Passenger Services research.** After Operations Efficiency, unlock a second
  project (3500, one sim day) that permanently adds +$75 route income per
  departed commercial. Persisted via `start-research-passenger-services` (no
  save-schema field). Daily finance brief includes the route bonus and the Ops
  Efficiency operating-cost discount. See `docs/decisions/0023-passenger-services-research.md`.
- Batch D greybox hooks: gear retracts when airborne; nav/beacon/landing lights
  follow phase and night (beacon strobes); cabin door opens at stand; service-vehicle
  wheels spin on task; fuel hose / bag bob / bus door service loops; rain streaks + fog;
  paved surfaces darken when wet; engine heat shimmer while engines run; brief touchdown
  smoke puff; runway edge + taxi centreline markers.
- Added Batch D animation/VFX task packet and night apron flood lights that
  brighten as daylight falls (presentation only).
- Generated Batch C first-playable 3D kits (turboprop, terminal/hangar/ops shed,
  fuel truck, baggage train, apron bus, service equipment) plus livery atlases.
  Presentation uses richer primitive stand-ins with spinning props and dual-flight
  service-vehicle targeting; HUD lists each flight’s phase. Status
  Generated/Modelled — not yet Approved.
  Evidence: `docs/art/prompts/batch-c-models-generation-2026-09-06.md`.
- Bailey approved Batch B. Greybox now loads Batch B surface/decal textures in
  `AirsidePrototype` (solid-colour fallback if a PNG is missing). Added Batch C
  model production packet. WLD kits Approved but not yet placed as prefabs.
- Generated Batch B world-surface candidates: 2048 tileable asphalt, concrete,
  grass and corrugated metal; glass mask; runway/apron decals; metre-scale glTF
  markings, lighting and props kits under `game/Airside/Assets/Airside/Art/`.
  Later Approved and surface-integrated; see bullet above.
  Evidence: `docs/art/prompts/batch-b-surfaces-generation-2026-09-06.md`.
- Prepared Batch B production task packet (world surfaces, decals, markings,
  lighting, props) and cleaned Batch A doc inconsistencies: art pipeline cites
  decision 0022, REF-004 delivery size, and the Batch A gate sentence.
- Added and approved Batch A visual references: daytime and dusk airport masters, turnaround service-zone composition, operations HUD direction, and the shared scale/palette sheet. Recorded prompt/source evidence; these five images are now the production visual authority.
- **Daily finance brief.** HUD shows a deterministic day estimate: expected
  operating cost (base + current weather + payroll) versus expected flight
  income at today's cadence, plus cash runway days when the net is negative.
  No save-schema change. See `docs/decisions/0021-daily-finance-brief.md`.
- **Accept-route schedule capacity.** Accepting a route now refuses when
  `ScheduledFlightsPerDay + pending` would exceed `StandCount × 6` (12/day on
  two stands). Offer stays pending; HUD disables Accept with a "Schedule full"
  reason. Operations panel lists accepted routes (airline, frequency,
  destination, payout) so the player can see what fills the cap. No save-schema
  change. See `docs/decisions/0020-accept-route-capacity.md`.
- **Concurrent commercial flights (slice 1).** Primary loop promoted to
  `CommercialFlight` list. When `ScheduledFlightsPerDay >= 4`, a second commercial
  spawns on a half-cycle stagger onto the free stand; fleet yields to any
  commercial. Single-flight path stays seed-identical below the threshold. No
  save-schema bump. HUD/world show both aircraft. 57/57 tests. See decision 0019
  and `docs/product/concurrent-flights-slice1-packet.md`.
- Added the approved Airside art direction and production asset contract: exact paths, staged first-playable manifest, image-generation rules, 3D/animation/VFX requirements, licensing workflow and cross-tool integration rules (decision 0022).
- **Concurrent flights design.** Decision 0019 locks promotion-to-list model,
  commercial FIFO priority, stand-based concurrency cap, schedule cadence
  (`ScheduledFlightsPerDay >= 4` → second flight), and per-flight settlement.
  First-slice task packet ready. Brief marked designed. No gameplay code in this
  change.
- **Daily operations report.** At each simulated midnight the sim publishes a
  `DailyReport` (flights, income, delays, running cost, net cash, reputation,
  weather, crew). Keeps the latest seven; rebuilt by replay. HUD shows the
  latest card. See `docs/decisions/0018-daily-operations-report.md`.
- **Research progression.** `AirportResearch` — first project Operations Efficiency
  (2500, one simulated day) permanently cuts base daily running cost by 100.
  Start is a persisted `start-research` command (replayed on load). Does not
  change flight timing. HUD shows progress / complete. See
  `docs/decisions/0017-research-operations-efficiency.md`.
- **Buildable third stand.** `AirportCapacity` — first capacity upgrade. Spend
  8000 (`build-stand` command, replayed on load) to unlock Stand 3; taxi network
  gains lead-in geometry; primary flights and ground traffic use the new stand.
  Two-stand behaviour stays seed-identical. HUD shows stand count and a build
  button. See `docs/decisions/0016-third-stand-capacity.md`.
- **Insolvency / game-over.** Cash negative at three consecutive simulated day
  closes declares the airport insolvent: simulation freezes, player commands
  refuse, and an `"Insolvent"` event is logged. Tracked on `AirportEconomy`
  (`ConsecutiveNegativeDays`, `IsInsolvent`); rebuilt by replay, no save-schema
  change. Presentation untouched. See `docs/decisions/0015-insolvency-game-over.md`.
- Test line.
- **Staffing by role.** `AirportStaffing` — ground crew, baseline 4. The baseline
  runs turnarounds unchanged (`TurnaroundWorkflow` gains an optional
  `staffingFactor` that short-circuits at 1.0, so every seed/timing test is
  byte-identical); extra crew shorten turnarounds, understaffing lengthens them
  into delays. Hire (120, persisted `hire-crew`/`release-crew` commands) and a
  daily wage settled with the running costs. `AirportEconomy.TrySpend` generalises
  the old priority-crew purchase. HUD shows headcount/payroll with hire/release
  and an understaffed warning. 60/60 tests. See `docs/decisions/0014-staffing-by-role.md`.
- **Simulated weather + daily running costs.** `Weather` (deterministic from the
  timeline, biased mild) changes every 5 simulated minutes. Each simulated
  midnight the airport pays `BaseDailyOperatingCost` (400) plus a weather
  surcharge (0–160) via `AirportEconomy.PayOperatingCosts` — the economy now has
  a drain, so cash can fall and route income / on-time performance matter for
  solvency. Weather does not affect flight timing (keeps every seed/timing test
  green). HUD shows current weather; away summary reports operating cost. 56/56
  tests; macOS build runs. See `docs/decisions/0013-weather-and-daily-running-costs.md`.
- Route proposals can now be **declined** (a persisted `decline-route` command).
  The HUD offer panel gains a Decline button; `AirportRoutes` exposes
  `OffersDeclined` and `ScheduledFlightsPerDay` (sum of accepted routes'
  flights/day), shown on the HUD. 51/51 tests. Groundwork for
  `docs/product/concurrent-flights-brief.md`.
- The "welcome back" away summary now also reports **route income earned** and the
  **reputation change** while the player was away (design pillar: a short visit
  should reveal what changed). `AirportEconomy` tracks `TotalRouteIncome`.
  48/48 tests.
- **Reputation** (0–100, starts 50). On-time departures raise it, delays lower it
  in proportion to the delay. `AirportRoutes.Accept` now takes the score and
  refuses proposals above the airport's reputation; an accepted route locks in a
  per-flight bonus of `3 × (score − 50)`. Reputation is rebuilt by replay — no
  persisted field. HUD shows score + band and disables offers the airport can't
  meet. 47/47 tests; macOS build runs. See `docs/decisions/0012-reputation.md`.
- **Airline route proposals.** `AirportRoutes` (simulation) offers one scheduled
  service at a time on a timer; it lapses if unaccepted. Accepting is a persisted
  `accept-route` command (replayed on load / offline catch-up); every completed
  flight then pays `Routes.IncomePerFlight` on top of turnaround revenue.
  Proposal content comes from the timeline, not the random source, so existing
  seed-based outcomes are unchanged. HUD shows the offer with an Accept button.
  42/42 tests; macOS build runs. See `docs/decisions/0011-airline-route-proposals.md`.
- Airport **location** + **day/night cycle**. `AirportLocation` (domain) carries
  id/name/region/UTC offset/latitude; ships with Kingscote (default), Port Lincoln
  and Coober Pedy. `DayCycle` derives local time from the sim clock — one
  simulated day per 20 real minutes from an 08:00 start — and drives the sun and
  ambient light and a HUD line (location, day, clock, phase). **Save schema → v2**
  (adds `locationId`); schema-1 saves migrate on load. 37/37 tests; macOS build
  runs. See `docs/decisions/0010-location-and-day-cycle.md`.
- Fair corridor hand-off: when the shared A1/A2 corridor is free and more than one
  fleet aircraft is queued, it goes to the one that has waited longest (fleet
  order breaks ties), instead of fleet order alone. Reuses the traffic monitor's
  wait timestamps — no new state. A forty-cycle soak asserts neither fleet
  aircraft is starved. 30/30 edit-mode tests; macOS build runs.
  See `docs/decisions/0009-fair-corridor-handoff.md`.
- Ground-traffic **fleet**: `AirportSimulation.GroundTraffic` is now a list.
  `GT-201` runs an arrival/stand/departure schedule (parking on whichever stand
  the primary flight is not using); `GT-202` repositions in and out through a
  run-up bay without a stand, starting 25s later. Every fleet aircraft reserves a
  single-file `TAXI-CORRIDOR` lock while on A1/A2, so at most one is on the shared
  taxiway at a time — they queue instead of meeting head-on. The primary flight
  keeps priority and is never blocked (`ReservationConflicts` stays zero).
  Deadlock-free by construction. Presentation renders one model per fleet aircraft
  and lists them in the HUD. 28/28 edit-mode tests; macOS build runs.
  See `docs/decisions/0008-ground-traffic-fleet-and-corridor-lock.md`.
- Earlier the same day: single second aircraft — shared segment reservations
  (`0006`), then an arrival/stand/departure schedule (`0007`).
- Second aircraft first introduced: shared taxi-segment reservations with the
  primary flight, priority-and-yield rule, per-tick reservation time so segments
  are correct during offline catch-up.
  See `docs/decisions/0006-second-aircraft-priority-and-yield.md`.
- Repo set up for shared work: added `.gitattributes` (Unity merge/binary rules),
  expanded `AGENTS.md` into the shared contract, added `CLAUDE.md` and Cursor
  rules, this changelog, and `LICENSES.md`. Removed a stale duplicate
  `AirsidePrototype 2.cs`. Pushed to a private GitHub `origin`.
- Segment clearance and traffic wait diagnostics: taxiing aircraft reserve only
  the segment they occupy and release it before moving on; a monitor explains any
  aircraft blocked on one resource for ten seconds or more
  (`docs/decisions/0005-segment-clearance-and-wait-diagnostics.md`).

## 2026-09-06

- Add named taxi routes and operations history.
- Add persistent saves and offline catch-up.
- Add operational turnaround and economy loop.
- Complete deterministic movement milestone.
- Open the Airside prototype by default.
- Add first playable Airside prototype.
- Target current Unity 6.3 LTS patch.
- Create Airside project foundation.
