# Airside — current state

The living status board. **Current state only.** Keep this file under ~250 lines: history, evidence and
superseded handoffs live in [`docs/history/`](docs/history/) (the full pre-restructure log is
[`GAME-handoff-log-through-2026-10-07.md`](docs/history/GAME-handoff-log-through-2026-10-07.md)).
Working rules are in [`AGENTS.md`](AGENTS.md); the one-page map of docs is [`docs/README.md`](docs/README.md).

## Where to resume

**Physical ground detail (9 Oct, Codex, #693):** mapped roadside grass clears airport
surfaces and nearby roads; coastal scrub gains varied lobes/colour and face normals.
Drains gain flush rims/grates; taxiway wear gains tapered, varied patches. No layout,
aircraft, simulation or save changes. Geometry checks pass 11/11 and C# syntax parses;
Unity appearance/performance unverified. Evidence: `docs/testing/physical-ground-detail-2026-10-09.md`.
Spatial puddles remain future work.

**Agent gameplay runner (9 Oct, Codex, #696):** hidden opt-in QA batches selected
workspaces/planner, booking/cancellation, save restoration, cameras and menu in one
private-save session; full profile adds visual weather and a 40× regional round trip.
`python3 scripts/agent-gameplay.py --plan` previews; default smoke requires a fresh
stamped build. No automatic builds/full tests or player controls. Nine runner
regressions and cached-reference Unity C# compile pass. Actual packaged scenarios,
visual quality and performance remain unverified. Instructions/limits:
`docs/testing/agent-gameplay/README.md`; decision `2026-10-09-agent-gameplay-runner`.

**Cloud lighting/depth (9 Oct, Codex, #697):** volume clouds now use bounded
sun-direction/colour scattering, two local sun-density probes and sky fill for
sunlit thin edges and deeper storm interiors. Eroded edges ease smoothly; reveal
and wrap fades thin optical depth, with premultiplied radiance compositing.
Sixteen volumes/view samples retained; one additional density probe per occupied
sample adds shader work. Placement, morphology, shared weather, lightning, camera
origin/depth handling and saves unchanged. Quick source/diff checks only; native
shader compilation, appearance and GPU cost unverified. Transparent intersection
sorting remains a limitation. Decision: `2026-10-09-cloud-lighting-and-depth`.

**Dash 8 wing-body fairing (9 Oct, Codex, #691):** the three overlapping roof
pods are replaced by one closed, smooth crown fairing with hull-buried ends and
wing-profile joints. Existing AIR-006 model/FBX, packaged mirrors and hangar
thumbnail updated; every other finished model node is preserved exactly.
Focused manifold/winding/end-cap/wing-join checks pass; Unity import, native
appearance and performance remain unverified. Full suites/builds were skipped.
Evidence: `docs/testing/dash8-wing-fairing-2026-10-09.md`. Existing duplicate-file
metadata and Sentinel texture mirror audit failures remain outside this scope.

**Flight tracker and HUD views (9 Oct, Claude):** after booking, a "Your flights" card (bottom-left) follows each planned or moving flight through
Booked, Ready, Taxi, Flying, Landing, Arrived; the booked aircraft is selected without moving the camera. Aircraft labels, airport map, tracker and
career card can be shown per view (overview / follow) from Options > Views; L and N toggle for the current view. ADR `2026-10-09-flight-tracker-and-hud-views.md`.
Headless draw-list/layout/step tests pass; **Unity unverified** (check the card's position with the map and selected card at 800x600, clicking a row,
the Views tab, and that the first-flight guide still shows with the career card hidden).

**Ground character (9 Oct, Codex, #687):** grass/soil gains local bare/matted islands,
dry/damp variation and interrupted mowing. Pavement differentiates sparse asphalt
repairs/sealed cracks from concrete slab joints and age variation; fine detail is
filtered and fades with distance. Shared absolute-world character retains the
Adelaide edge handover. Apron patches have varied size/age and stay wholly inside
the mapped apron. Terrain heights, operational layouts, markings and saves retained.
Focused apron checks pass 4/4, changed C# 9 syntax parses and generated harness is
current. Native shaders, wet/dry/day/night appearance and GPU performance remain
unverified; no player build or full suite run. Evidence: `docs/testing/ground-character-2026-10-09.md`.
Decision: `2026-10-09-ground-character`.

**Connected aircraft tails (9 Oct, Codex, #684):** all 15 active airframes plus the
A320 authored fallback now have curved hull-fitted fin/stabiliser roots and tapered
aerofoils. Rudder/elevator hinges share the fixed surface geometry; T-tails retain
a centre bullet. Per-type profiles correct stabiliser proportions, with unchanged
rotors, wings, engines, landing gear, simulation and saves. Runtime models, editable
FBXs, packaged mirrors and hangar thumbnails updated. Geometry checks and actual
side/rear/top asset renders: `docs/testing/aircraft-tails-2026-10-09/`. Native Unity
import, lighting, control deflection and performance remain unverified.
Decision: `2026-10-09-connected-aircraft-tails`.

**Weather clock (9 Oct, Claude):** `Weather` read local hour from `AirlineClock.Default`, so saves with another epoch had fog hours
hours off the HUD clock (civil helicopters held in fog). `AirlineOperations.Clock` now calls `Weather.UseClock`. Weather
sequences differ from before for non-default-epoch saves; saves unchanged. Quick headless tests only; Unity unverified.

**Linear renderer (9 Oct, Codex, #681):** Unity now uses Linear colour rendering.
Existing linear mesh palettes and Gamma fallback shader branch are retained. The
far land-cover palette/calibration now match sRGB-decoded satellite imagery and
material tint; crop variants retain their relative colour differences. HDR/ACES,
AA, shadows, weather geometry, simulation and saves are unchanged. Palette generation,
Python syntax and diff/source checks pass. Unity/shader compilation, Mac appearance
and GPU timings remain unverified. Compare day/dusk/night aircraft, glass, lamps,
wet pavement and the satellite-to-land-cover handover before judging the visual gain.
Decision: `2026-10-09-linear-colour-rendering`.

**Aircraft continuity (9 Oct, Codex, #672):** full route poses are now available to
ordinary overview/follow views, rather than hiding departures after the local climb
projection ends. Ordinary follow streams journey terrain and shifts the render origin.
Aircraft identity lookup includes every physically present fleet view independently
of follow-cycle filtering; direct follow can select a present aircraft outside that cycle.
Parked models remain visible and explicitly selectable. Active-aircraft counts and
automatic camera cycling now use a two-hour window before published departure; overdue
weather waits and moving aircraft stay active. Fleet inventory retains idle aircraft.
Long-idle known outstation ground aircraft likewise activate near their next departure.
Audit coverage and remaining limitations: `docs/testing/aircraft-continuity-2026-10-09.md`.
Established final poses now survive missing/postponed ETAs in a continuous holding
orbit (#677), climbing to at least 1.5 km. A usable ETA rejoins through bounded pose
slew; real clearance retains the held pose through the handoff. Map/follow/selected
status use the actual held pose. This represents existing operational delays; it does
not grant runway/stand/curfew clearance or create a new saved state. Focused holding
and approach checks pass 16/16; changed C# syntax parses. Rendered holding/rejoin and
terrain performance remain unverified. Save v23 now retains final/holding pose and
phase, guarded by registration/type/destination/state/start-time after offline catch-up;
changed journeys are not resurrected. Old saves rebuild their unsaved presentation.
Live-feed expiry and unsupported outstation presentation retain explicit limits.

**Shared operational weather and reload continuity (9 Oct, Codex, #679):** validated
live weather/wind samples enter a saved timeline at processed simulation time. Ground
stops, runway/rotor wind rules, final-entry commitment, sky/rain and wind motion read
that same timeline. Expiry, disabling live weather and unknown offline periods fall
back to the deterministic forecast. Historical samples replay on catch-up; expiry is
an explicit event boundary. Save v23 adds flat optional observation/arrival records;
v1–22 retain their prior forecast and rebuild presentation. Review-only weather pins
remain cosmetic. Focused continuity, save, Operations, runway and hold checks pass
53/53; new Unity JsonUtility round-trip test is added but unrun here. Unity compile,
serialization, actual weather releases, reload/holding appearance and performance
remain unverified.

**Cloud continuity/weather variety (9 Oct, Codex, #674):** clouds recycle/fade around
the watched area, not the orbiting lens. The wider footprint keeps storm bodies away
from wrap seams; the volume proxy survives a far-plane-clipped exit face. Thin high
wisps, cumulus, broad stratiform banks and storm towers share persistent geometry and
smooth morphology. Authored rain includes drizzle/showers/continuous-rain profiles;
weather summaries reflect continuous live conditions too. The existing operational
weather enum/chain, RNG and save schema are unchanged. Original shader source only;
sixteen-volume budget and atlas fallback retained. Decision: `2026-10-09-cloud-continuity-and-variety`.
Focused coverage/profile regressions pass 11/11; changed Unity-facing C# syntax
parses and the generated headless harness is current. Native shader compilation,
orbit/pan/zoom appearance and GPU performance remain unverified.

**Terminal doors and more people (9 Oct, Claude):** each regional-stand walk now starts at a terminal door (`AdelaideTerminalDoors`, six doors on
the OSM terminal outline; airside doors are derived, not surveyed) with a sliding-glass door prop, a six-person boarding queue and a gate agent at the
door, 18 landside walkers (OSM entrances, car-park bays) and airside staff (`AdelaideAmbientPeople`), person cap 110 with distance-throttled posing.
ADR `2026-10-09-terminal-doors-and-people.md`. Pure tests pass and the whole Presentation assembly compiles against Unity's own libraries
(quick compile recipe in the PR); look and frame time in the Mac build unverified. Aerobridge and bus flows unchanged. Still open from the 8 Oct
HUD/map work (ADR `2026-10-08-hud-chrome-redesign.md`): unredesigned screens (selected-aircraft card, movements board, radar, Fleet detail,
Career layout, setup/menu), and two layout tests failing on main in compact windows
(`Contracts_LayoutKeepsBothColumnsInsideTheSurface`, `GrowingOverview_LastAircraftRemainsReachableInCompactWindows`).

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

*One block, replaced at the end of each session. Updated 2026-10-09.*

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
