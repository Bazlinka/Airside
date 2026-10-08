# Airside — current state

The living status board. **Current state only.** Keep this file under ~250 lines: history, evidence and
superseded handoffs live in [`docs/history/`](docs/history/) (the full pre-restructure log is
[`GAME-handoff-log-through-2026-10-07.md`](docs/history/GAME-handoff-log-through-2026-10-07.md)).
Working rules are in [`AGENTS.md`](AGENTS.md); the one-page map of docs is [`docs/README.md`](docs/README.md).

## Where to resume

**Aircraft continuity (9 Oct, Codex, #672):** full route poses are now available to
ordinary overview/follow views, rather than hiding departures after the local climb
projection ends. Ordinary follow streams journey terrain and shifts the render origin.
Aircraft identity lookup includes every physically present fleet view independently
of follow-cycle filtering; direct follow can select a present aircraft outside that cycle.
Parked models remain visible. The proposed two-hour activity window remains a product
recommendation pending Bailey's choice; Fleet inventory must always retain them.
Audit coverage and remaining limitations: `docs/testing/aircraft-continuity-2026-10-09.md`.
Unity rendering, camera transitions and performance remain unverified.
Cloud continuity and weather variety are the next authorised implementation.

**Storm movement commitment (8 Oct, Codex, #670):** ready fixed-wing departures wait
at their gate/bay; taxi-released departures continue under normal runway/traffic rules.
An inbound already on the shared 32 km extended final before storm onset keeps its
arrival timer/estimate and joins landing rather than disappearing when that timer is
postponed. Later inbounds remain held before final. No save schema/camera/rendering
changes; curfew, separation and rotorcraft rules retained. Cockpit exit restores
exterior rendering; no fault found there. Native cockpit→tower and storm landing
appearance remains unverified. Focused checks: 48/48 pass; a larger arrival-estimate
selection hit the 60 s limit and remains unverified. Decision: `2026-10-08-storm-movement-commitment`.

**Night final visibility (8 Oct, Codex, #668):** aircraft flares use an original additive
light-source shader with the same haze transmission as runway point lights, rather than
URP surface fog. Distant halos sit outside the fuselage in camera depth and use corrected
projected size. Night airborne position/strobe glows remain inside 6 km for side-on finals;
individual landing flares still carry the close nose-on approach. Graphics toggles,
weather attenuation, lamp policy, simulation and saves stay unchanged. Source/geometry
checks and Roslyn C# syntax pass; asset metadata passes except the known satellite
JPEG mirror mismatch. Native shader compilation and Mac night overview/tower/follow appearance
(clear/fog, head-on/side/aft and near/far handoff) remain unverified.

**Weather and aircraft vibration (8 Oct, Codex, #666):** sixteen bounded cloud volumes now
include broad storm towers/anvils reaching roughly 10 km; lit deck tops remain below a
high observer. Fog clears above its shallow ground bank; rain, wipers, interior audio,
sky and cloud immersion share observer altitude across camera views. Cloud geometry
and noise survive flight-origin shifts; integrated wind travel and 12-second weather
transitions reduce jumps. Lightning follows displayed storms, glows within clouds and
has a brief channel with independent distance-delayed thunder. Rapid duplicate camera
shake and continuous trackpad/joint tapping removed; reduced engine/runway buzz,
slow weather gusts and discrete landing/gear contact retained. Vibration switch remains.
No operations, schedule, economy, saves or new external assets changed.
Decision/limits: `docs/decisions/2026-10-08-weather-altitude-and-restraint.md`.

**Checks:** 90 focused headless regressions and six static shader/producer contracts pass;
changed C# syntax parsed. Generated harness is current and presentation map refreshed.
Unity compilation, shaders, actual appearance/audio, packaging and GPU performance are
unverified. No broad suite/build/player testing run under the standing policy.

**Next:** Bailey chooses the Mac build/playtest timing. Check fog ground/climb; storm
below/inside/above, cloud silhouettes and deck crossings; cockpit/exterior and long
journey origin shifts; live/fallback weather; quiet cruise, storm gusts, landing/gear
thumps and Vibration off. Check lightning/thunder alignment and transparent rendering.
Standing policy: quick relevant checks only; broad suites/builds/player reviews only
on request; merge completed authorised work without repeated approval.

**Other current context:** aircraft body realism (#651), regional departure/landing
speeds/altitude, maps/HUD redesign, notifications/registration (#645/#663), toasts,
control-tower view, Adelaide opening and earlier lighting/audio/native changes retain
their existing Unity/Mac verification limits. Remaining HUD surfaces and compact-window
layout failures, WIP 787-window proposal (#543), known FlightManual page 7 failure and
satellite JPEG mirror mismatch remain separate. The preceding status and exact other
tools' open work are preserved in
`docs/history/game-handoff-before-weather-realism-2026-10-08.md`.

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
