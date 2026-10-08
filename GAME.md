# Airside — current state

The living status board. **Current state only.** Keep this file under ~250 lines: history, evidence and
superseded handoffs live in [`docs/history/`](docs/history/) (the full pre-restructure log is
[`GAME-handoff-log-through-2026-10-07.md`](docs/history/GAME-handoff-log-through-2026-10-07.md)).
Working rules are in [`AGENTS.md`](AGENTS.md); the one-page map of docs is [`docs/README.md`](docs/README.md).

## Where to resume

**Flight transitions (8 Oct, Claude):** full-journey legs took pitch from the route slope alone, so no flare, no rotation and a snap at cruise-to-approach.
`RegionalFlightPath.ApproachPitchDegrees` / `DeparturePitchDegrees` now blend route pitch into the authored attitude (flare over 300 m, ~7 s; was 150 m). Presentation only.
Unity compile and appearance unverified — watch a regional arrival and departure from the follow camera.
**Economy rebalance (8 Oct, Claude):** flight dispatch cost raised (`FlightEconomics`, ADR 2026-10-08-flight-cost-rebalance); pay/prices/start funds unchanged.
Next in `docs/plans/economy_realism_plan.md`: standing per-aircraft daily cost, then fuel price. Pacing vs ADR 0120 unverified — run the career bot.

**Flight-view moving map (8 Oct, Claude):** `AirsidePrototype.FlightMap.cs` + `CockpitMovingMap.cs`. Panel bottom-right in every aircraft view (N toggles,
+/−/scroll scale, A auto): airfield layout (runway-up) near the field, South Australia coast (north-up) with great-circle route elsewhere. Unity compile/appearance unverified.

**Landing-light visibility (8 Oct, Claude):** lit landing/taxi lamps now draw a camera-facing flare (`UpdateLampFlare`, AirsidePrototype.Lights.cs);
the 20 cm lens was invisible at play distances. Unity compile and appearance unverified — check at overview/follow, day and night.

**Working policy (8 Oct, Bailey, #611):** quick relevant checks by default; broad
tests/builds/playtesting only on request. Merge completed authorised work into main
without repeated approval. Canonical instructions: AGENTS.md testing and merge policy.

**Aircraft and soundscape audio (8 Oct, Codex, #614 / PR #616):** v02 bank with 68 clips,
independent twin cores/starters, spatial exhaust, cabin/airflow/rain, gear/door cues,
regional contact/reverse, network-flight audio, spatial local PA/apron, refined UI
and bounded traffic/master headroom. 67 pure numeric assertions and bank byte/seam
checks pass; Unity listening/compile/performance unverified. Evidence:
`docs/testing/audio-overhaul-2026-10-08/`. No simulation/save changes.

**Australia flight ground (8 Oct, Codex cloud Agent 1, #613 / PR #615):** full Australian journeys are watchable;
real country-wide elevation/WorldCover plus mapped airport pavement/buildings/roads,
painted OurAirports runways and altitude/proximity terrain density. One airport data set,
49 near tiles, interleaved cruise horizon and bounded geometry builds. Saves/simulation unchanged.
Offline pure-source/data and syntax checks recorded in `docs/testing/australia-flight-ground-2026-10-08/`;
Unity appearance, transitions and performance unverified. ADR `2026-10-08-australia-flight-ground.md`.

**ATR 42 hold door (8 Oct, Claude):** `RelocateAtrDoors` flipped the door 180° in its transform and `UpdateCabinDoor` overwrote that yaw, so the shut door stood mirrored ahead of the cockpit. The flip is now baked into the mesh (`RebakePartPivot` with a rotation). Look at the ATR on the apron and while loading. Windscreen white patches in the same screenshot are not investigated.

*One block, replaced (not stacked) at the end of every session. Updated 2026-10-08.*

**Blur root cause (8 Oct, Claude):** Unity had saved a maximised 1600x900 window in `com.DefaultCompany.Airside.plist`, so the player rendered at 1600x900 and was stretched
with black bars on a 3456x2168 Retina screen. `AirsideDisplay.EnsureNativeResolution` now resets any non-windowed render size below 90% of the display at
startup. Confirmed by screenshot: clearing the saved size gave full-screen, sharper output.

**Enroute flap review (8 Oct, Codex, #586 / PR #588):** retains animated rig rest poses
when follow targets refresh and selects flap/gear phase from the regional journey.
Updated against current main, preserving aircraft-specific return rotation timing.
Native review with #604: 296/296 passed; packaged flight journeys unverified.
Evidence: `docs/testing/enroute-flaps-2026-10-08/`.
**Next approved work:** review the connected player-flow study requested by Bailey; existing native verification remains open. **Done in code, unverified (ADR 0250/0251):** state-wide land-cover
colours on the streamed terrain, and the wide overview — zoom to 450 km, and past 60 km the overview camera streams a fine 16 km ring plus
a coarse 64 km ring (121 tiles, 2 km cells) so the state can be dragged under the camera. Packet: `docs/plans/south-australia-overview-streaming.md`.

**Save recovery (8 Oct, Codex, #599):** merged in PR #600. Retains a readable previous
save and recovers missing/unreadable primary JSON with a warning; save schema unchanged.
Native compatibility 163/163 passed. Packaged recovery/title journey unverified.
Evidence: `docs/testing/save-recovery-2026-10-08/`.

**Opening and Options (8 Oct, Codex, #601):** merged in PR #603.
Clearer title, shorter/skippable entry, grouped Options with setting explanations,
opening-animation/cockpit-motion controls and direct return to title. Validation
complete for affected checks: native 152/152, focused headless 20/20; all 12 native UI
views inspected. Broad regressions retain 3 existing failures; packaged
handoff/interaction unverified. Evidence: `docs/testing/opening-options-2026-10-08/`.
**Crisp HUD (8 Oct, Claude):** branch `claude/crisp-hud-20261008`. The HUD is laid out at 1440x900 and enlarged by `GUI.matrix` (2.25x on the
3456x2168 Retina surface), which stretched text and button art. `HudPainter` text, pills and buttons now draw at device pixels (font size and
rounded button textures scaled, matrix undone for that draw). Panels already used crisp shader-rounded rects. Menus, help and dev tools still
use the old path. 105 Unity EditMode tests (HUD layout + new `HudCrispnessTests`) pass; needs a Mac look at Operations/Fleet.

**Airport templates (8 Oct, Claude):** branch `claude/airport-templates-20261008`. Generic runways, terminals and gates for all 19
Australian destinations (`AirportTemplates`) and a deterministic runway/gate planner (`AirportArrivalPlanner`); the network flight
HUD shows the landing runway and gate. Runways cross-checked against OSM/OurAirports; real gate numbers and terminals from OSM for MEL, SYD, BNE, PER, CBR, OOL, DRW, ASP (airlines prefer their own terminal); HBA, KGC and the small fields stay generic.
ADR `2026-10-08-airport-templates`. Headless tests pass; unverified in Unity. Gate occupancy is not simulated away from Adelaide.

**World lighting pass (8 Oct, Codex, #595):** terrain, roads and airport pavement
share URP surface lighting. Corrected Gamma vertex palette/texture blending,
excluded solid roads/props from satellite-edge/water treatment, normalised pavement
scan grain around authored colour and reduced daytime sun/grade washout.
Native day/dusk/night comparison views and focused checks:
`docs/testing/world-lighting-2026-10-08/`. Packaged build/soak deferred; review pending.

**Parafield first working airport (8 Oct, Codex, #593):** independent YPPF with four mapped
runways, taxiways, apron/hangars and four original light trainers on a reserved training circuit.
Operations → WATCH PARAFIELD; ADELAIDE/R returns to Adelaide. Player bases/economics/saves
unchanged. Five focused traffic checks and asset audit pass; Unity compiled. Bailey requested
immediate merge after a brief pass; full suites stopped and Mac/visual review deferred.
Evidence and limits: `docs/testing/parafield-2026-10-08/`.

**Ocean halo fix (8 Oct, Codex):** far and outer ocean meshes keep one constant overlap height,
removing the artificial sloped bands that catch water reflections. Land overlap stays unchanged.
Bailey requested a quick fix without tests; Mac rebuild passed. Visual confirmation remains pending.

**Fleet flight performance (8 Oct, Codex):** branch `fix/fleet-flight-performance-20261008`, based on merged lighting PR #587.
Research: `docs/data/FLIGHT_PERFORMANCE_RESEARCH.md`; decision `2026-10-08-fleet-flight-performance`.
Derived altitude rates vary with height and capture level cruise; normal upper levels are separate from
certified ceilings. CAS/Mach limits and integrated distance share a speed schedule. Camera telemetry
handles accelerated clocks and origin shifts, shows IAS (CAS approximation), Mach and GS. Inbound height
reserves the same extended final as its map track; regional departure uses the type's roll/Vr.
New jet schedules allow twenty minutes for climb/descent instead of ten. No save-schema change.
Bell remains on its own VTOL model with the common telemetry fix.
Evidence: `docs/testing/flight-performance-2026-10-08/`: 166/166 focused native Unity checks passed.
User requested immediate merge: full headless run stopped, Mac build and packaged camera journey
unverified. Do not infer a packaged flight playtest from deterministic/native math checks.

Merged lighting #587 retains 37 passing native checks and 70 reviewed night fixtures; full-flight/night
performance acceptance remains separate in `docs/testing/aircraft-lighting-2026-10-08/`.
**Free visual upgrades (8 Oct, Codex, task #585):** free A320 source parts fitted into
v02; Kenney service vehicle/foliage derivatives; consistent scanned pavement,
feathered wear, close grass grain, facade fittings, weather-responsive terminal
reflections, paused-clock conveyor ribs and scanned cabin fabric/fittings.
Source inputs, attribution and offline regeneration committed. Aircraft metrics,
working cabin apertures, doors and simulation/save contracts retained. All ten
workstreams and acceptance evidence: `docs/decisions/2026-10-08-free-visual-upgrade.md`,
`docs/testing/free-visual-upgrade-2026-10-08/`. Merged in #589; its build and runtime acceptance evidence remains in that packet.

**Earlier merged work:** #587 fits all 14 families’ exterior lights and night beams;
#580 fixes camera-shell ownership, parked-aircraft clearance, rotor queues and query allocation.
Their native follow-ups remain in their linked testing packets.

**Previous merged work:** #552 native fixture/blank-save/camera-origin repairs (native rerun pending);
#570 fleet/flight clarity and Melbourne views; arrivals gated by runway only with 4-minute go-around;
clean-image rendering and far-terrain hand-over. Their native follow-ups remain below.

**Current implementation (#577):** Operations opens on My airline, listing every owned aircraft across all bases with live route/status/next-event times and scroll access. Selecting an aircraft opens its correct Fleet profile; Airport movements retains the Adelaide board. Native Unity verification remains pending; evidence: `docs/testing/network-operations-2026-10-07/`.

**Next approved work:** Mac follow/overview day/dusk/night check of the door detail pass (including ATR airstairs), the all-base
Operations and fleet/flight journeys, and the pending native fixtures. No native visual claim.

**Wide overview (ADR 0250/0251), run on the Mac 7 Oct (Cursor):**
zoom to 450 km; past 60 km the overview streams a fine 16 km ring plus a coarse 64 km ring that grows with the zoom (5 to 8 tiles, up to
289) so the ground under the camera stays covered, fading into the sky before its edge. Coarse vertices average the land cover round them;
the baked ADR 0210 haze is gone (it made a pale square). Tiles build in ≤70 ms; the only long frame is the first render (~2.5 s at startup),
now logged as `[Airside soak] hitch`. Still unchecked: depth precision, sky/stars at the 1,170 km clip, dusk/night. Packet:
`docs/plans/south-australia-overview-streaming.md`.

**Open validation and regressions:**
- Clean-image pass (full-res SSAO, near clip scales with distance, ground mip bias 0, 16x aniso) is unverified: compare ground-marking flicker, AO softness and frame time. ADR 0246/0247/0248/0250/0251 (SSAO half-res — now reverted, MSAA budget, 3 shadow cascades/110 m, full-DEM far mesh, 4096 px far image, state-wide land-cover colours, zoom to 450 km with coarse-ring streaming):
  compare day/dusk/night for AO banding, shimmer and shadow pop-in; check the seam where the near satellite ends,
  startup-to-title time and memory (far image decodes to ~64 MB); at 100–450 km zoom check coastline blockiness, the 40 m join between
  coarse and fine tiles, sky/stars/sun at a 1,170 km far clip, depth precision and frame time while tiles stream. Each is a one-line revert (see the ADRs).
- **Current baseline regressions (8 Oct):** busy-day waiting/taxiing ground overlap, plus night-sky
  review framing and yaw. Full headless has the same 3 failures. Opening/Options' broad native run
  also exposed two obsolete menu/intro assertions; their updated checks pass in the final 152/152
  affected rerun. Full regression is not green. Evidence: `docs/testing/opening-options-2026-10-08/`.
- Maintenance journey and refined interface (ADR 0245) still wait on a native playtest: prop/jet startup, gear/tug
  alignment, swept doorway clearance, busy taxi traffic, save/reload through every phase. Do not merge on offline
  painter previews alone.

**Visual planning (7 Oct, Codex):** Bailey rejected the earlier photographic vision. Three grounded edits and a staged plan now live in
`docs/art/candidates/attainable_direction_20261007/README.md`, branch `feature/attainable-visual-direction-20261007`.
They use actual 6 October packaged captures/native aircraft references and propose a fresh horizontal dispatch interface.
Review candidates only; no runtime changes. Current-build baseline capture and native implementation validation remain necessary.
**Player-flow planning:** `docs/plans/player_flows_and_interface_contract.md` consolidates 24 tasks and recovery paths.
Further interface design must validate these before treating overview/Schedule mockups as a complete solution; no runtime changes.
**Player-flow study (7 Oct, Codex):** `docs/art/player-flow-prototype-2026-10-07/README.md`, branch
`codex/player-flow-prototype-20261007`, task #567. Connected sample journeys follow the 24-task contract;
12 browser journey groups/model integrity pass at five sizes; supplementary headless suite 1,842/1,842.
Evidence: `docs/testing/player-flow-study-2026-10-07/`. Review the local prototype/portable bundle.
Runtime Unity files and saves are unchanged; native interface/command wiring, appearance and performance remain unverified.
Earlier photographic vision was rejected; capture-based visual studies remain proposals subordinate to the flow contract.

**Watch (native checks remain open; current AGENTS.md merge policy applies):**
- Headless-green PRs can still break UnityEngine tests: the dotnet harness skips every test that touches `UnityEngine`. Since ADR 0252 it
  no longer misses missing `using`s or NUnit APIs Unity lacks (`Is.AnyOf`), native compilation remains separate from headless checks.
- `JsonUtility` cannot write null: any new nested save object needs the same blank-means-none handling on restore.
- The long-trip frame stall (earlier run: 172 s) is not accepted as fixed; no full-flight performance claim exists.

## Current milestone

Airside is a live-time airline management game set inside an autonomous Adelaide Airport (YPAD) — see
`docs/product/PROJECT_PLAN.md` (v4.0). The player owns and grows an airline from a Saab 340 starter fleet; Adelaide
keeps running around them with its real runways, taxiways, stands, roads, car parks and terminal, a real 24-hour day,
live weather and a curfew. There is a title screen, first-time airline setup, a Glass Cockpit HUD and a saved career
(a self-led 100–150 hour path, ADR 0120).

The ground and surroundings are built from open data (OpenStreetMap, Sentinel-2, Copernicus DEM); the map overhaul
(ADR 0184, PR #459) completed the road network, car parks and precinct furniture. The newest work is listed in the
"Where to resume" log at the top of this file.

The aircraft are flown to real reference speeds on a true 3° glideslope with a flare (ADR 0044); phase durations are
derived from those speeds, never picked. The camera orbits, zooms and pans freely, with follow and reset shortcuts.

## Visual asset contract

The approved visual direction, exact asset paths, animation responsibilities and
production order live in
`docs/art/ART_DIRECTION_AND_ASSET_SPEC.md` (decision 0022). The first playable
moves from procedural primitives to approved art in batches, with primitives kept
as fallbacks during integration.

The immediate visual target is a premium stylised-realism miniature of a regional
Australian airport. Generated images establish composition, palette, fictional
liveries and UI direction. Runtime aircraft, buildings and service vehicles remain
true 3D assets; animation and VFX mirror simulation state and never drive it.

- Anti-aliasing is on: High keeps 4× MSAA on the PC pipeline up to 2.5 M pixels (1080p), and 2× above that
  (1440p+), plus SMAA (high) on the runtime camera; Medium uses 2× MSAA + SMAA (ADR 0246). SSAO runs at full
  resolution (Medium samples; ADR 2026-10-07-clean-image-pass); High shadows are 3 cascades to 110 m. Vsync is on (`vSyncCount` 1).
- The post stack runs a deliberate grade only — the template default profile's depth of field, motion blur, lens distortion, chromatic aberration, lens flare and panini are pinned off.
- The simulation keeps running when the window loses focus (`runInBackground`).

## Invariants

- Domain and simulation rules remain independent of Unity scenes.
- Career progress persists in a versioned save; any schema change needs an explicit version and a migration.
- Time comes from an injected clock.
- Runways, taxiways and stands must be reserved before use, and a lone aircraft
  must never block itself (`AirportSimulation.ReservationConflicts` stays zero).
- Frame rate must not change simulation outcomes.
- Pausing and opening the pause menu freeze every presentation rate together
  (`SimulationFrozen`), not just the aircraft.
- Exactly one `AirsidePrototype` may exist; a duplicate bootstrap destroys itself.
- No external data or asset enters the project without a recorded licence.

## Run it

Open `game/Airside` in Unity 6.3 LTS and press Play. The in-game Flight Manual
contains the current controls and source credits; `README.md` covers first-run setup.
Follow AGENTS.md for quick checks and merge policy. Mac build/playtest, device audio
and flight streaming performance remain unverified until explicitly exercised.

Earlier handoffs and the previous full controls/build/soak notes are preserved
verbatim in [`docs/history/GAME-handoff-log-through-2026-10-08.md`](docs/history/GAME-handoff-log-through-2026-10-08.md).
