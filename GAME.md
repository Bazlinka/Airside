# Airside — current state

The living status board. **Current state only.** Keep this file under ~250 lines: history, evidence and
superseded handoffs live in [`docs/history/`](docs/history/) (the full pre-restructure log is
[`GAME-handoff-log-through-2026-10-07.md`](docs/history/GAME-handoff-log-through-2026-10-07.md)).
Working rules are in [`AGENTS.md`](AGENTS.md); the one-page map of docs is [`docs/README.md`](docs/README.md).

## Where to resume

*One block, replaced (not stacked) at the end of every session. Updated 2026-10-07.*

**Latest merged work (7 Oct):** far-terrain satellite → land-cover hand-over at 30 km and the white "cloud" fix on
the outer terrain (Cursor); selected-aircraft panel only on selection; save-load fix for blank `JsonUtility`
records (v22 saves failed to load); render cost trim (ADR 0246), far terrain at full 125 m DEM (ADR 0247) and a
4096 px far satellite (ADR 0248), repo tidy (ADR 0249) and state-wide land-cover colours (ADR 0250) (Claude).

**Current implementation (#570):** a quieter Fleet workspace with Available/All bases, correct selection after airport changes, optional market/details, reviewed network bookings and cancellations, and airborne Melbourne exterior/cockpit/window views. The flight planner compares expected round-trip profit using production economics. Native Unity behavior is unverified; evidence and checks: `docs/testing/fleet-flight-clarity-2026-10-07/`. Bailey explicitly authorized implementation and merge without another review pause.

**Next approved work:** verify the fleet/flight changes in the Mac build.

**Wide overview (ADR 0250/0251), run on the Mac 7 Oct (Cursor):**
zoom to 450 km; past 60 km the overview streams a fine 16 km ring plus a coarse 64 km ring that grows with the zoom (5 to 8 tiles, up to
289) so the ground under the camera stays covered, fading into the sky before its edge. Coarse vertices average the land cover round them;
the baked ADR 0210 haze is gone (it made a pale square). Tiles build in ≤70 ms; the only long frame is the first render (~2.5 s at startup),
now logged as `[Airside soak] hitch`. Still unchecked: depth precision, sky/stars at the 1,170 km clip, dusk/night. Packet:
`docs/plans/south-australia-overview-streaming.md`.

**Open — needs the Mac (nothing below is verified in Unity):**
- ADR 0246/0247/0248/0250/0251 (SSAO half-res, MSAA budget, 3 shadow cascades/110 m, full-DEM far mesh, 4096 px far image, state-wide land-cover colours, zoom to 450 km with coarse-ring streaming):
  compare day/dusk/night for AO banding, shimmer and shadow pop-in; check the seam where the near satellite ends,
  startup-to-title time and memory (far image decodes to ~64 MB); at 100–450 km zoom check coastline blockiness, the 40 m join between
  coarse and fine tiles, sky/stars/sun at a 1,170 km far clip, depth precision and frame time while tiles stream. Each is a one-line revert (see the ADRs).
- **24 native EditMode failures** on `main` (Unity 2265 passed / 24 failed): `PassengerFlightViewTests`
  (window sightlines, all 13 types + two exterior cases), `PresentationLayoutTests` (HUD layout ×4),
  `FieldMiniMapTests.PanelFor_ShowsOnTheDesktopWindow`, `AirsideSettingsTests.Defaults_MatchAPlayableAdelaideSession`,
  `CockpitCameraTests.CockpitTracksSeatAfterAircraftPoseAndRestoresCameraSettings`,
  `PresentationBugSweepTests.CameraShellAnchor_FollowsCameraMovedAfterPlacement`,
  `GroundSeparationTests.BusyDay_NoAircraftDriveThroughEachOther`. (The three save/resume failures were fixed.)
- Maintenance journey and refined interface (ADR 0245) still wait on a native playtest: prop/jet startup, gear/tug
  alignment, swept doorway clearance, busy taxi traffic, save/reload through every phase. Do not merge on offline
  painter previews alone.

**Watch:**
- Headless-green PRs can still break UnityEngine tests: the dotnet harness skips every test that touches `UnityEngine`. Since ADR 0252 it
  no longer misses missing `using`s or NUnit APIs Unity lacks (`Is.AnyOf`), but run `scripts/test-unity.sh` before merging behaviour changes.
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
  (1440p+), plus SMAA (high) on the runtime camera; Medium uses 2× MSAA + SMAA (ADR 0246). SSAO runs at half
  resolution; High shadows are 3 cascades to 110 m. Vsync is on (`vSyncCount` 1).
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

Open `game/Airside` in Unity 6.3 LTS and press Play.

Career pacing report (no Unity needed): `scripts/career-balance.sh [out-dir] [seeds] [hours]`
plays every difficulty with a simulated player and writes a report (ADR 0125).

The game opens on the **title screen** (ADR 0122): the dawn illustration of the airport,
the live Adelaide clock and one card — **Continue** your saved airline, **New airline**,
**How to play**, Options or Quit. Enter continues; Esc steps back or opens the menu.
New airline is a three-step setup (ADR 0123/0127) with a live preview: name and flight code,
livery (twelve colours or your own hue/shade) and a briefing with an optional first-flight coach. The **Flight Manual** (How to play, the
**?** on the rail, or F1; ←/→ to page) explains the rules and lists the controls.
Choosing one dissolves the art into the live airport while the camera glides down (any
key skips). Time is **live Adelaide time**: one second in the game is one real second.
There is no pause, no time rates and no skip; the menu does not stop the airport.

The in-game HUD is the **Glass Cockpit** (ADR 0122): a vertical navigation rail on the
left (Ops, Map, Fleet, Contracts, Career), a floating status capsule at the top (airline,
Adelaide time, funds, reliability gauge, tier), the **career ring** bottom-left (steps done
in the current stage, the pinned goal and one next action — click it for the Career track),
your live flight tiles top-right, the airfield radar bottom-right and the selected-aircraft
card bottom-centre. Toasts appear under the capsule. A workspace opens as one glass sheet
right of the rail. **Career** opens on the tier track; **Airline** on its header flips to the
profile (name, livery, base, achievements, history).
A waiting aircraft's card says what holds it; when that is another aircraft the line is a
link (tap it, **‹** goes back). Each paid flight's toast says whether it pushed on time and,
if not, what made it late (ADR 0128).

Simulation:

- Escape: steps back on the title screen's new-airline form; clears aircraft selection
  and returns to overview when one is selected; otherwise opens or closes the menu
  (Resume, Options, Quit) — time keeps running
- Tab: open or close the destinations map (scroll to zoom, drag to pan; state labels appear when zoomed)
- H: open or close Fleet (every aircraft at every base, its profile, the base strip and the market; clicking a row stays in the sheet)
- M: mute audio

Camera:

- Click an on-field aircraft to select and follow it. A coastal-blue ring marks
  the selected aircraft; a details card sits above the control bar. Fleet-panel
  rows also select. Dragging to pan does not select.
- F: toggle follow. Turning it off hands the camera back **where it is** —
  position, angle and zoom are kept and you are free to move from there. It
  does not drag you back to the overview.
- R / Overview: reset to the overview framing and clear the current aircraft
  selection
- Right-drag: orbit / look around
- Left-drag or middle-drag: pan across the field by grabbing the ground under the
  cursor (drops follow, since panning a followed aircraft would only fight the
  follow). A left press on a HUD panel stays a click, and a left press only becomes
  a drag after a few pixels of movement so a plain click can still select an aircraft.
- Scroll: zoom toward the ground under the pointer while free. While following this
  biases the phase framing rather than setting an absolute distance, so it survives
  the follow easing instead of being erased on the next frame.
- WASD: pan (drops follow, same as drag)
- Q / E: orbit left / right without a mouse
- Z / X: lower / raise the camera

Follow, reset and direct aircraft selection are owned by `AirsidePrototype`;
camera movement is read by `AirsideCameraController`. Exactly one owner each —
two owners is why F used to toggle follow off in `Update` and straight back on
in `LateUpdate`.

Run checks with `scripts/test-unity.sh`. Build the local Mac app with `scripts/build-mac.sh`.

Soak a packaged build unattended (PROJECT_PLAN acceptance): launch with
`-airsideSoak -airsideSoakMinutes 30`. A fresh airline flies itself in live time
(its first departure at four minutes, so every soak covers an engine start), a
`[Airside soak]` heartbeat goes to `~/Library/Logs/DefaultCompany/Airside/Player.log`
every minute (live time, trips, fps, memory, fleet states with engine spool, beacon
and doors), a STALL error is logged
if the clock stops, and the app quits with a COMPLETE line. Soak saves go to
`airline-save-soak.json`, never the player's save.
Without a Mac Unity editor, `scripts/test-domain.sh` runs the same
Domain/Simulation EditMode tests headlessly via `dotnet test` (.NET 8 SDK) — a
fast supplementary check, not a replacement for a real Unity run before merging.
It does not cover Presentation, which needs UnityEngine.

## Handoff protocol (short form)

1. Start: read this file, `git pull --rebase origin main`, skim `CHANGELOG.md` (newest ~20 lines).
2. End: **replace** the "Where to resume" block above (do not add a new dated block on top); put the evidence and
   detail in the PR / `docs/testing/<topic>/README.md` / an ADR and link it in one line.
3. Anything that stops being current moves to `docs/history/` — not deleted, not left here.
