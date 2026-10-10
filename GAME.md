# Airside — current state

The living status board. **Current state only.** Keep under ~250 lines; history and superseded handoffs live in [`docs/history/`](docs/history/).
Working rules are in [`AGENTS.md`](AGENTS.md); the one-page map of docs is [`docs/README.md`](docs/README.md).

## Where to resume

**Flight crews and ground vehicles (10 Oct, Codex, #779):** captain walkarounds now
set a minimum player preparation budget; uniformed pilots/cabin crew board using existing
stairs/bridges. Service vehicles gain seated drivers, opaque body shading, steering and
small lamp sources; motion follows simulation-clock advancement. No hiring/economy/save
schema change. 176 focused checks and updated capture-delay lock pass; native review pending.
Prior crew CI comparison: six stale timing fixtures fixed; ten failures reproduced on
pre-crew main. Evidence: `docs/testing/flight-crew-vehicles-2026-10-10.md`.


**Economy v2, step 1 (9 Oct, Claude):** real Australian dollars. Flight cost/pay come from `FlightCostModel`; aircraft "prices" are lease
deposits (`LeaseTerms`); opening cash A$1.5M; save v24 scales old money x35 once on load. Real `CareerBot` pacing matches the old economy
(Regional 10.9 h, Domestic 30 h, International 74 h; 5 aircraft at 150 h). Not done: daily lease/insurance, bank loan + recovery, "deposit/lease"
HUD wording, native check of wide money in the HUD (unverified; no Unity run). Plan: `docs/plans/economy-competitors-walkable-roadmap.md`;
decision/evidence: `docs/decisions/2026-10-09-economy-v2-real-dollar-scale.md`. Main CI is red on 10 older, unrelated headless tests.

**Working ground teams (9 Oct, Codex, #774 / PR #776):** airport-provided larger service teams;
shared bag allowance and physical carrying trips determine minimum player baggage
preparation, including equipment setup and clearing. Reuses shipped characters/tools.
Fuel/catering reserve setup/clear windows within existing budgets. Ambient airlines
receive more visible workers; their scheduling is unchanged. No hiring/economy/save-schema
changes. 160 focused/related checks pass, final native service frames inspected;
clean universal bbe40abd build, nine booking/save/view actions and 40× ADL–KGC
round trip pass, zero runtime errors. Whole subsequently merged main not rebuilt.
Decision: `2026-10-09-airport-ground-teams`; evidence: `docs/testing/ground-crew-2026-10-09.md`.


**Landing gear improvements (9 Oct, Codex, #757):** actual top-attachment pivots, one retained
gear timeline, clear door/leg sequencing, centred nose steering during fold, enclosed
stowed wheel envelopes and widebody bogie beams carried with their axles/wheels.
Missing widebody leaves fitted to existing bay/fuselage skin; ATR sponsons stay on the body.
47 headless and 75 native focused checks pass; all 15 airframes inspected up/down/mid,
with driven extension probes. Clean universal build/A350 view checks and 40× ADL–KGC
round trip pass; no runtime errors. Return gear close-up/performance unverified.
Simulation, paths, datums and saves retained.
Evidence/limits: `docs/testing/landing-gear-2026-10-09/README.md`.
Fleet intake/control refinements remain verified as recorded in `docs/testing/fleet-refinements-2026-10-09/README.md`.

**City and town lights (9 Oct, Codex, #760):** warm varied windows on existing
Adelaide/regional building facades; urban street sources/pools and mapped lamp
extensions; distant lights only on mapped built-up land. Existing airport lighting,
layouts, simulation and saves retained. Household occupancy and unsurveyed lamp
spacing are inferred; regional detail remains limited to shipped OSM snapshots.
Final clean universal Mac build and nine native steps pass, zero runtime errors;
day/dusk/night and horizon frames inspected. Focused 40× Whyalla arrival/origin
probe has no runtime errors; regional close windows/GPU cost remain unverified; `docs/testing/city-town-lights-2026-10-09.md`.

**Directional twilight sky (9 Oct, Codex, #758 / #773):** cool zenith/horizon gradients,
sun-facing amber/rose dawn and dusk, blue-hour evenings; broad orange fog reduced.
Existing celestial clock, weather/altitude, stars, night readability and saves retained.
Refinement narrows amber, softens dawn, adds an opposing rose band and cools blue hour.
Clean universal bb4a17ea build and 31 native steps pass, zero runtime errors; before/after
cycle, follow, opposite horizon and tower-weather frames inspected. Seasonal extremes/GPU
performance unverified; combined subsequent gear main not rebuilt.
Evidence: `docs/testing/sky-refinement-2026-10-09/README.md`.
Decision: `2026-10-09-directional-twilight-sky`.

**Tower cab and rain motion (9 Oct, Codex, #756):** original interior ceiling, window
framing and low controller consoles now enclose the existing tower eye. Rain retains
normal fleet-relative speeds and full elapsed presentation time; streaks reflect a fixed
exposure and floating-origin recentering preserves observer motion. Simulation, saves,
external assets and personal game unchanged. Native baseline reproduced the open roof
and a synthetic 220 m/s observer was limited to 90 m/s. Fixed clean universal Mac build and identical 17-step scenario pass, zero runtime
errors; day/night cab and rain frames inspected. Four tower and 17 runner regressions
pass. Synthetic 220 m/s rain probe reaches 216.7 m/s (previously 90); real flight/cloud
crossing and control hit-testing unverified.
Decision/evidence: `docs/testing/tower-rain-2026-10-09/README.md`.

**Next:** ground-team implementation complete. Bailey can use the isolated stamped ground-team
bbe40abd build for personal playtesting; it predates the subsequent sky/economy merge.
The separate sky bb4a17ea build/evidence retains the limits recorded above.
Other active state and prior verification limits are preserved in
`docs/history/game-handoff-before-tower-rain-2026-10-09.md`.
Standing policy: agents choose necessary focused runtime checks; broad suites/soaks on request.
Personal saves/running game and generated pipeline/package edits must be preserved.

*One block, replaced at the end of each session. Updated 2026-10-09.*

## Current milestone

Airside is a live-time airline management game at autonomous Adelaide Airport (YPAD); see `docs/product/PROJECT_PLAN.md` (v4.0).
Grow an airline from a Saab 340 starter fleet while Adelaide runs around it: real runways, taxiways, stands, roads, car parks and terminal,
a 24-hour day, live weather and curfew. Title/setup, Glass Cockpit HUD and a saved 100–150-hour self-led career (ADR 0120) are present.

The ground and surroundings are built from open data (OpenStreetMap, Sentinel-2, Copernicus DEM); the map overhaul
(ADR 0184, PR #459) completed the road network, car parks and precinct furniture. The newest work is listed in the
"Where to resume" log at the top of this file.

The aircraft are flown to real reference speeds on a true 3° glideslope with a flare (ADR 0044); phase durations are
derived from those speeds, never picked. The camera orbits, zooms and pans freely, with follow and reset shortcuts.
## Visual asset contract

The approved visual direction, asset paths, animation responsibilities and production order live in
`docs/art/ART_DIRECTION_AND_ASSET_SPEC.md` (decision 0022). Approved art replaces procedural primitives
in batches, with primitives kept as fallbacks during integration.

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
