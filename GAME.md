# Airside — current state

The living status board. **Current state only.** Keep this file under ~250 lines: history, evidence and
superseded handoffs live in [`docs/history/`](docs/history/) (the full pre-restructure log is
[`GAME-handoff-log-through-2026-10-07.md`](docs/history/GAME-handoff-log-through-2026-10-07.md)).
Working rules are in [`AGENTS.md`](AGENTS.md); the one-page map of docs is [`docs/README.md`](docs/README.md).

## Where to resume

**Mac notifications (8 Oct, Codex, #645):** Options → Notifications adds an opt-in switch, permission/settings
shortcut and SEND TEST. Important player events only while Airside runs in the background: stand-needed
arrivals, late settlements, contracts and career milestones. Bursts group/deduplicate; clicking a banner
activates Airside. Career-event presentation drains in Update so minimising does not stop it. Existing
toasts remain. Original universal UserNotifications plugin is compiled/imported before Mac builds;
Xcode command-line tools/macOS 11+ required. No remote push, closed-game alerts or save migration.
Decision/evidence: `docs/decisions/2026-10-08-macos-notifications.md`.

**Toast redesign (#642 / PR #644):** merged status/wrapped-message/repeat/lifetime cards, eased motion and
panel/screen-bounded stacks. Previous focused checks 22/22 pass; Mac appearance remains unverified.

**Turn banking (8 Oct, Claude):** `CoordinatedBank` in AirsidePrototype.cs replaces the yaw-lag bank for airborne phases; physical bank from sim-time ground speed and heading rate, clamped ±25°, so regional/en-route turns now roll too. SID-arc bank still takes priority. Unity compile and look unverified; watch roll-in/out smoothness and ground-phase wings-level.


**Adelaide opening (#626 / PR #638):** merged T1 dawn artwork, v04 AIRSIDE vector/text fallback and
skippable centre-opening Continue reveal. Still needs Mac appearance/build checks. Provenance:
`docs/art/prompts/adelaide-opening-2026-10-08.md`.

**Control-tower view (8 Oct, Claude):** click the Adelaide tower in the overview to stand in its cab (`AirsidePrototype.Tower.cs`, `ControlTowerView.cs`,
`AirsideCameraController.Tower.cs`). Reuses the passenger-seat camera: 360° drag-look, scroll zoom, Home recentres, Esc / LEAVE TOWER glides back to the
previous overview. Presentation only; saves unchanged. Headless geometry/pick tests pass; Unity compile, cab-interior appearance (glass is back-face
culled from inside), ground-level terrain/LOD streaming and night look are unverified. Not available at Parafield or during flight views.

**Flight transitions (8 Oct, Claude):** full-journey legs took pitch from the route slope alone, so no flare, no rotation and a snap at cruise-to-approach.
`RegionalFlightPath.ApproachPitchDegrees` / `DeparturePitchDegrees` now blend route pitch into the authored attitude (flare over 300 m, ~7 s; was 150 m). Presentation only.
Unity compile and appearance unverified — watch a regional arrival and departure from the follow camera.

**Checks:** Mac notification policy/buffer plus Options painter checks 8/8 pass; edited C# and shell syntax
checks pass. These do not verify native Mac/Unity compilation, plugin packaging/loading, permission or
actual notification delivery. Updated native Options/settings coverage is not run here. Existing opening
FlightManual page 7 failure and satellite JPEG mirror mismatch remain unrelated.

**Next:** Bailey chooses the Mac build/playtest timing. In the next Mac build, first enable Options →
Notifications, allow the macOS prompt, send a test, then check background delivery and click activation. Check the title and Continue, skip, animation-off,
new-airline and returned-airline paths in the next Mac build. Also inspect notification severity,
long messages/repeats, entrance/expiry, stacked notices and workspace/flight-view placement. The v03 Dock/app icon is retained.
Standing policy: quick relevant checks only; broad suites/builds/player reviews only on request;
merge completed authorised work without repeated approval.

**Other active context:** current main includes flight-transition pitch/flare easing, aircraft-relative
exit glides, the flight-view moving map, aerodrome beacon and landing-light flare. These still need
Unity appearance checks. The new flight-dispatch cost rebalance leaves prices/pay/start funds unchanged;
next economy work is standing aircraft daily cost then fuel price; career pacing remains unverified. Existing audio/terrain/aircraft/save/native validation limits, other tools’
branches and earlier status are preserved in `docs/history/game-handoff-before-adelaide-opening-2026-10-08.md`.
Do not treat the new title illustration as a capture of the live game or a change to the 3D airport.

*One block, replaced at the end of each session. Updated 2026-10-08.*

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
