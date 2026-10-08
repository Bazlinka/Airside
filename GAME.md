# Airside — current state

The living status board. **Current state only.** Keep this file under ~250 lines: history, evidence and
superseded handoffs live in [`docs/history/`](docs/history/) (the full pre-restructure log is
[`GAME-handoff-log-through-2026-10-07.md`](docs/history/GAME-handoff-log-through-2026-10-07.md)).
Working rules are in [`AGENTS.md`](AGENTS.md); the one-page map of docs is [`docs/README.md`](docs/README.md).

## Where to resume

**Aircraft bodies (8 Oct, Codex, #651):** all 13 scheduled aircraft plus Bell 412
and Parafield trainer have continuous body contours, rounded tips and fitted
skin details at their existing asset paths. Bell/trainer glazing and doors now
follow the shell; A320 inherited doors are refitted and v02 source-adapted engines,
fans and wheels retained. Regeneration: shared body pass before glazing/paint,
Bell/trainer generators, A320 free-source adaptation, runtime art sync.
Bounded numeric/fit evidence and limits:
`docs/testing/aircraft-bodies-2026-10-08/README.md`.
Native Unity appearance, doors/cabin views and performance are unverified.
No simulation/save changes; these remain representative models, not manufacturer CAD.
The WIP 787-window-height proposal (#543) is separate.

**Flight times, maps and HUD redesign (8 Oct, Claude, #621/#624/#632/#641/#646/#652/#656/#659):** all merged; Unity compile and appearance
unverified (pure maths and draw lists checked headlessly: `dotnet test` filters plus `scripts/render-hud-mockups.py`).
- *Flight times:* ATR 42/Dash 8 planning cruise 510/620 km/h; A350/787-9 practical range 13,500 km, 787-10 11,200 km; jets get a cruise-time
  factor of 1.0 (600 km) to 1.08 (2,500 km+) in `LegTiming.AirborneSeconds` for routing/headwind (no wind is modelled; HUD Mach reads lower on long legs).
- *Maps:* flight-view moving map simplified (one-line footer, no rings) with worker-thread textures and a windowed anti-aliased coast.
  Route Map has an async-baked land fill (`RouteMapLandLayer`, `RouteMapLandWindow`), faint coast/borders, rimmed dots, codes then names by zoom,
  and on-field aircraft collapse to dots plus "N on field" below zoom 60.
- *HUD:* ADR `2026-10-08-hud-chrome-redesign.md`. Floating capsule + action group, 72 pt rail, new Glass palette, milestone card, flight-view
  instrument tiles, Operations rows, Map plan pane, Fleet/Contracts cards, Career stat cards. Not yet redesigned: selected-aircraft card,
  airport-movements board, radar, Fleet detail pane, Career layout (roadmap/activity/right column), setup/splash/menu screens.
- *Open/unverified:* look at the Route Map and HUD in the Mac build; check `PC_RPAsset`/`packages-lock` local edits are intentional (left uncommitted).
  `Contracts_LayoutKeepsBothColumnsInsideTheSurface` and `GrowingOverview_LastAircraftRemainsReachableInCompactWindows` fail on main (layout-only,
  compact windows; not caused by the HUD painter work) and need a cause found. Next approved work: Bailey's call — remaining HUD screens above.

**Regional departure climb (8 Oct, Claude):** the route profile starts at the height a local climb-out reaches, but a regional leg begins at brake release, so the old code had the aircraft gain the whole gap in 120 s (up to ~5,200 ft/min on a 737). `RegionalFlightPath.ClimbLagSeconds` + `ClimbAltitudeFeet` start the route climb late (finishing out of the cruise), and `DepartureClimbHeight` replaces the smoothstep with one steady rate. Peak now <= 3,000 ft/min in `RegionalDepartureClimbTests`. Board/card/map altitude text now uses `BoardAltitudeFeet` (same lag). Unity unverified.

**Next:** Bailey chooses Mac build/playtest timing. Check the fleet's nose and
tail contours, opened/shut doors and interior glazing at overview/follow distances,
then day/dusk/night and performance. Standing policy: quick relevant checks only;
broad suites/builds/player reviews only on request; merge completed authorised
work without repeated approval.

**Regional flight updates (8 Oct, Claude):** regional departure altitude now
uses a delayed route climb and a steady local climb-out rate (peak <= 3,000 ft/min);
Operations/card/map text shares that drawn altitude. Regional landings integrate
route/approach/touchdown speeds and brake at the aircraft's rollout deceleration.
`FlightSpeedEnvelope` holds stall margins, the 250 kt CAS cap and acceleration
limits; its bank-dependent minimum is not yet enforced on the live bank. Unity
appearance remains unverified. Main also includes HUD stage 3 (Map/Fleet/Contracts/
Career pages) and road turning heads/give-way teeth from other tools.

**Other current work:** main includes opt-in Mac notifications (#645), redesigned
toast cards (#642 / #644), turn banking, flight-transition pitch/flare easing,
aircraft-relative exit glides, control-tower view, flight moving map, aerodrome
beacon, landing-light flare and the Adelaide opening (#626 / #638). Their native
appearance/packaging/performance checks remain open. Mac notifications need
Options → Notifications → allow permission → SEND TEST, then background delivery
and click activation. Check title/Continue, skip, animation-off and returned-airline
paths in the next Mac build. The v03 Dock/app icon is retained. Flight-dispatch
cost rebalance retains prices/pay/start funds; next economy work is standing
aircraft daily cost and fuel price, with career pacing unverified.

Existing opening FlightManual page 7 failure and satellite JPEG mirror mismatch
are unrelated. Earlier audio/terrain/aircraft/save/native validation limits and
other tools' branches remain in
`docs/history/game-handoff-before-adelaide-opening-2026-10-08.md`.
Do not treat the title illustration or generated Hangar thumbnails as native captures.

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
