# Airside — current state

The living status board. **Current state only.** Keep under ~250 lines; history and superseded handoffs live in [`docs/history/`](docs/history/).
Working rules are in [`AGENTS.md`](AGENTS.md); the one-page map of docs is [`docs/README.md`](docs/README.md).

## Where to resume

**Economy v2, steps 1-3 (9-11 Oct, Claude):** real Australian dollars. Flight cost/pay from `FlightCostModel`; aircraft "prices" are lease
deposits (`LeaseTerms`); opening cash A$1.5M; save v24 scales old money x35 once; v25 adds daily lease + insurance (founding Saab: insurance
only; catch-up safe; cash never below zero); v26 adds a tier-capped bank loan (8.5%/yr, auto-covers a daily shortfall, BUY borrows a
deposit gap). Casual `CareerBot` pacing OK (Domestic 33 h). Not done: explicit borrow/repay buttons, native check of wide money and the new
card text in the HUD (no Unity run). Open: a competent `CareerBot` stalled on a stand wait after the ground-crew merge; cause unattributed.
Plan: `docs/plans/economy-competitors-walkable-roadmap.md`; decision/evidence: `docs/decisions/2026-10-09-economy-v2-real-dollar-scale.md`.

**Four-engine fleet (#778 / PR #781, Codex, 11 Oct):** passenger B748 747-8
(Bailey's selected variant) and A388 A380-800 now have purchase/lease, flight,
visual/audio and service profiles with distinct original 3D kits. Code F
18R lease/shared overflow and persisted departure-line return reservations;
A380 Super wake. Existing aircraft/timetable and save compatibility preserved.
Economy v2 shared lease/cost inputs retained (A$4.86M / A$4.05M deposits).

Both selected native round trips and nine feature/disk-save steps per aircraft
pass on clean universal 46ff53f1. Final wordmark-only 80c5d09c universal build
and native builder frames inspected. 63 final pure, 11 native reservation and
10 title checks pass; earlier geometry/native evidence and actual CI limits are
recorded in `docs/testing/four-engine-fleet-2026-10-10.md`.
Shared representative cockpit families/main-deck views; exact instruments,
upper-deck traversal, exhaustive listening and performance remain unverified.
Prior economy/ground-team/gear/sky state and limits retained verbatim in
`docs/history/game-handoff-before-four-engine-fleet-2026-10-10.md`.

**Next:** this authorised fleet implementation and QA are complete; choose the
next approved work. Standing-cost/loan work remains with its owning task.
Bailey can personally playtest the isolated stamped 80c5d09c build. Preserve
personal saves, active apps and other tools' generated/source edits.
Standing policy: focused checks chosen automatically; broad suites/soaks on request.

*One block, replaced at the end of each session. Updated 2026-10-11.*

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
