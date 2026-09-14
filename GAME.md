## Where to resume — session handoff

- **2026-09-14 Cursor Controls help (`cursor/controls-help-601f`):**
  **F1** opens a Controls sheet (camera, airline panels, playtest tools). Esc closes
  help before clearing selection / opening the menu. Presentation only — no sim
  changes. Stacks on the Dev Tools branch.
- **Task packet / acceptance:** player-visible outcome is an on-screen controls
  reference for playtesters; scope is airline HUD presentation. Acceptance: F1
  toggles the sheet, bindings cover Tab/H/T/F8/F1/Esc/camera, Esc closes help first.
- **Evidence:** `scripts/test-domain.sh` **194/194** on this Linux host (includes
  ControlsHelp tests). Unity EditMode / Mac build require Mac Unity 6.3 LTS.
  No manual playtest claimed.
- **Next:** Mac Unity + packaged verification of the stacked UI (map/hangar/flights/
  dev tools/controls), then Bailey's external playtest. Night lighting still pinned.

- **2026-09-14 Cursor Dev Tools (`cursor/dev-test-tools-601f`):**
  **Dev tools (F8)** opens a playtest panel: fleet status lines, next-event time,
  **Auto-schedule idle player** (soak-style departures) and **Assign free stands**.
  Mutually exclusive with Map / Hangar / Flights. Presentation only — live Adelaide
  time stays on (no pause/skip). Stacks on the flights-board branch.
- **Task packet / acceptance:** player-visible outcome is a gated playtest tools
  panel; scope is airline HUD presentation; simulation invariants untouched.
  Acceptance: F8 toggles the panel, fleet list is readable, auto-schedule books
  idle player aircraft with soak delays, assign stands parks awaiting aircraft,
  mutual exclusion with map/hangar/flights.
- **Evidence:** `scripts/test-domain.sh` **192/192** on this Linux host (includes
  DevTools helper tests). Unity EditMode / Mac build require Mac Unity 6.3 LTS.
  No manual playtest claimed.
- **Next:** Mac Unity + packaged verification of Dev Tools (and flights/map/hangar),
  then Bailey's external playtest when free. Night lighting remains pinned until
  a dedicated rework.

- **2026-09-14 Cursor flights board (`cursor/flights-board-601f`):**
  **Flights (T)** opens a time-ordered board of every player and AI movement
  (route, phase, next time). Click a row to select/follow, or open the map when
  away. Mutually exclusive with Map (Tab) and Hangar (H). Presentation/UI only —
  schedules, reservations, saves and airport geometry unchanged. Stacks on the
  immersive map + hangar branch.
- **Task packet / acceptance:** player-visible outcome is an all-flights timetable
  board; scope is airline HUD presentation; simulation invariants untouched.
  Acceptance: T / clock button toggles the board, rows sort by next interesting
  time with idle aircraft last, player+AI included, click selects, mutual exclusion
  with map/hangar, fleet and hangar still work.
- **Evidence:** `scripts/test-domain.sh` **188/188** on this Linux host (includes
  FlightBoard helper tests). Unity EditMode / Mac build require Mac Unity 6.3 LTS.
  No manual playtest claimed.
- **Next:** Mac Unity + packaged verification of Flights board (and map/hangar),
  then Bailey's external playtest when free.

- **2026-09-14 Cursor immersive map + hangar (`cursor/immersive-map-hangar-601f`):**
  the Australia destinations map now zooms and pans (scroll + drag), with a denser
  coastline, state borders, and state/region labels that appear as you zoom in.
  **Hangar (H)** lists every player and AI aircraft with live status and progress;
  click a row to follow it on the field or track it on the map when away.
  Presentation/UI only — schedules, reservations, saves and airport geometry unchanged.
- **Evidence:** `scripts/test-domain.sh` **184/184** (map/hangar slice). Unity EditMode
  / Mac build require Mac Unity 6.3 LTS. No manual playtest claimed.

- **2026-09-14 Cursor direct aircraft selection (`cursor/direct-aircraft-selection-601f`):**
  clicking a visible aircraft on the Adelaide field selects and follows that exact
  transform. Invisible `AircraftPick` proxies handle the pick; fleet-panel selection
  remains with a whole-row Select affordance. Merged via #223.

- **2026-09-14 Codex selectable aircraft (`codex/selectable-aircraft`):** fleet
  registrations are now presentation-only selection controls. Selecting an aircraft
  that is at Adelaide follows that exact 3D aircraft and highlights a compact identity
  card; selecting an off-map aircraft opens its route tracker instead. Overview, `R`
  and `Esc` clear the selection. Simulation, schedules, reservations, saves, aircraft
  models and airport geometry are unchanged.
- **Task packet / acceptance:** player-visible outcome is exact player-or-AI aircraft
  selection from the fleet panel; scope is airline HUD, fleet-view lookup and existing
  follow camera only; deterministic simulation and save invariants remain untouched.
  Acceptance is exact-transform follow, off-field rejection, selected-state identity,
  and a clean overview exit.
- **Evidence:** Unity EditMode **243/243**, including exact registered-target and
  off-field rejection checks. Per Bailey's preference, no manual playtest was run.
- **Watch — unverified in packaged play:** Bailey could not see or use the fleet-panel
  registration click target/highlight/details card in the packaged app. Treat that
  slice as incomplete; direct 3D selection above is the primary fix.

- **2026-09-14 Codex YPAD regional-stand wayfinding (`codex/ypad-wayfinding`):**
  the real 50A–50D regional bays now have procedural yellow lead-in lines, stop
  bars and Unity-rendered identifiers. `AdelaideStandMarkings` derives every
  position and label from Claude's generated `AdelaideLayout`; no stand coordinate
  is copied and no external asset is added. Simulation, routes, timing,
  reservations, saves and HUD are unchanged.
- **Evidence:** clean inherited baseline **238/238**; after implementation Unity
  EditMode **241/241**, including exact bay/reference, stop-point and finite-geometry
  checks. Per Bailey's request, no manual/external playtest was run; packaged
  camera-matrix verification remains outstanding.
- **Next:** merge/review `feature/ypad-real-layout` and this dependent branch in
  order. Then inspect the stand paint in the packaged overview/follow camera pass
  before adding more taxiway signs or apron detail.

- **2026-09-14 Claude real YPAD layout wired in (Bailey: "use the real OSM layout and wire it in", `feature/ypad-real-layout`):**
  the OpenStreetMap layout is now the single source of truth for Adelaide airside.
  - **Rendering:** `AirsidePrototype.YpadPavement.cs` builds every OSM taxiway as a
    ribbon with rounded joints and sealed shoulders, the real apron polygons
    (ear-clipped), yellow centrelines, hold bars at the 14 real holding positions, and
    extruded terminal / RFDS footprints. The rectangle-and-fillet skeleton is deleted.
    12/30 sits at its real crossing (centre 253, 410; yaw 73.2°, ~377 m NE of the
    05/23 midpoint). Ground and plateau grew to cover it (3 900 × 2 800 m).
  - **Ground motion:** `Simulation/GroundMotion.cs` gives every route a speed profile —
    15 kt taxi, 2 kt pushback, 8 kt lineup, cornering speed from turn radius at
    0.35 m/s² lateral, accel/brake limits — and durations come from it
    (`AdelaideGround`). Taxi-out 50D–50A ≈ 8–9 min (pushback, 25 s tug disconnect,
    3.3–3.4 km via T4-K-A-F2-F3-F6), taxi-in ≈ 4 min via E2-E1-A-K-T4, vacate E2 and
    lineup at the 05 threshold. Replaces the fixed 7/5 min, 60/90 s constants.
  - **Bays** BAY-1..4 = real 50D, 50C, 50B, 50A; pushback is a tail-first curve onto T4.
  - **Takeoff** now rolls from the 05 threshold (`CircuitProfile.TakeoffStartX` −1 500);
    roll/climb distances unchanged. The demo circuit keeps rolling from where it
    stops via `AirsideFlightPath.CircuitTakeoffOffsetX`.
  - **Fixed in passing:** the oriented-strip distance used the wrong rotation sign for
    Unity yaw (invisible while 12/30 was centred on the origin).
  - Default overview centres on (150, 350) so the terminal is in frame.
- **Evidence:** `scripts/test-unity.sh` exit 0, EditMode **238/238** (new
  `GroundMotionTests`; `AdelaidePavementTests` rewritten for the real layout: 12/30
  crossing, terminal side, apron strip clearance, routes on pavement/plateau and
  joined end to end, bay spacing). Packaged app: overview from the south-west matches
  Bailey's aerial photo (12/30 crossing the upper third, terminal beyond it left of
  05/23, RFDS left, GA apron top-left, F looping round the 05 end); VH-PAX pushback
  at 2 kt tail-first onto T4 past a parked Emu Air ATR, 0 kt tug pause, then taxi on
  the yellow centreline at 11 kt through the curve.
- **Codex branch `codex/adelaide-airside-realism`:** superseded — its
  `AdelaideAirsideLayout` constants describe the old invented geometry. Merging it
  now would conflict with this in `AirsidePrototype.FleetVisuals.cs` (its constants
  block no longer exists here) and `AirsideAdelaidePavement.cs`.
- **Next:** watch a full real-time arrival (E2 vacate, stand, taxi-in) and a departure
  through lineup and takeoff at the 05 threshold; then AI timetable + all flights on
  the map, dev test tools, hangar screen (Bailey's list).

- **2026-09-14 Claude live real time, typing, engine start, intro (Bailey's requests, `feature/live-real-time`):**
  - **Live Adelaide time (Bailey chose "always real time").** `AirlineClock` is now an
    instance with a UTC epoch: one simulated second per real second, shown in
    Adelaide local time with daylight saving. Pause, 2×–60× and Skip are gone; the
    menu no longer stops anything. Saves are **v3** (`EpochUtcTicks`); v2 aligns so
    its save moment reads as when it was saved; v1 uses a default epoch. Continue
    always syncs to now (`AwayCatchUp.LiveTarget`, which re-aligns past the 7-day cap
    or a backwards device clock). Emu Air's first departures are at +10 and +20 min.
  - **Typing no longer moves the camera:** `AirsideCameraController.KeyboardCaptured`
    while the start/away panels are up or a text field has focus.
  - **Engine start/shutdown:** `EngineStartSequence` — beacon T−3 min, doors close
    T−160 s, No.2 (right) T−120 s, No.1 (left) T−70 s, 30 s spool; after parking No.1
    then No.2 wind down, beacon off, doors open. Drives each propeller, beacon, doors,
    heat shimmer and engine note. Parked fleet props no longer spin (they did before,
    via the demo circuit's SkipGroundTaxi RPM). Soonest departure is "In 3 min".
  - **Launch intro:** 7 s eased camera glide in with wordmark and live local time;
    any key/click skips; capped timestep so it is not spent behind the loading hitch.
- **Evidence:** Unity EditMode **242/242** (new: `LiveClockTests`, `EngineStartSequenceTests`).
  Packaged app: intro plays; holding D/W in the name field leaves the camera still;
  Bailey's v2 save continued at 10:04 Mon 14 Sep matching the Mac clock; soak log
  showed beacon T−3, R spool 0.74 → 1, L 0.26 → 1, then TaxiOut; sub-second frames
  show blades turning at the stand. (Integer-second screenshots strobe-alias a
  6-blade prop at idle — 2,520°/s is a multiple of 60° — and look frozen.)
- **Next:** align the airfield with real YPAD geometry (OSM), then derive every
  ground speed from real route lengths (Bailey, same day).

- **2026-09-14 Claude soak and playtest packaging (PROJECT_PLAN step 3 acceptance, `test/soak`):**
  - `AirlineSoakTests`: 30 simulated days, player flown continuously, checked at
    every event for runway double-occupancy, stand double-booking, stuck waits and
    the airport stopping; every aircraft flies > 60 trips.
  - Soak mode for packaged builds (`-airsideSoak -airsideSoakMinutes N`, see Run it).
  - **Packaged soak, 30 min real time:** COMPLETE, no stalls, 120 fps throughout,
    managed heap flat at 4 MB, 0 exceptions, 0 "Not allowed". It ran at 1× after
    the first landing because the soak driver did not re-assert 60× after the
    game's deliberate drop to 1× for a stand choice — driver fixed.
  - **Packaged soak, 10 min at 60×:** 10 simulated hours, 7 trips, COMPLETE, no
    stalls, 120 fps, 0 exceptions. Logs kept in `work/soak/` (not committed).
  - `scripts/package-playtest.sh` builds from a clean tree and zips
    `work/playtest/Airside-<commit>.zip` with `docs/testing/PLAYTEST_TESTER_NOTE.md`
    as "READ ME FIRST" (unsigned-app open steps, no-coaching brief, five questions).
- **Next — Bailey:** send the zip to one person who has not seen the game (PROJECT_PLAN
  step 4); observe without coaching; bring back their answers.

- **2026-09-14 Claude first-flight guide (PROJECT_PLAN step 3, `feature/first-flight-guide`):**
  a numbered card under the clock walks a new player through one round trip —
  plan → wait for departure → departing → away → landing → choose a stand →
  taxiing in — with a pulsing highlight on Plan flight and the stand buttons, and
  a toast when the first trip completes. `FirstFlightGuide` derives the step from
  fleet state only (no save field), so Continue lands on the right step and the
  guide never returns once any player aircraft has flown a trip.
  `AirlineHudLayout` gained a guide slot (map and stacked fleet move below it),
  tested at all six sizes with and without it. **Readability:** the map panel is
  now near-opaque (runways read through it) and the ADL label sits left of its dot.
- **Evidence:** Unity EditMode **233/233**. Packaged app: new airline → step 1 with
  Plan flight highlighted → map → Melbourne "Now" → step 3 Departing; Continue
  restores step 3; map readable.
- **Next:** 30-minute soak, then a packaged build for one external playtester.

- **2026-09-14 Claude real 24-hour day (Bailey's decision, `feature/real-24h-day`):**
  `DayCycle.DaySeconds` is now 86,400 (was 1,200), so lighting time, the airline
  clock and real-length flights agree; `AirlineClock` reads it. Weather now changes
  hourly (was every 5 simulated minutes, which flickered at 60×). Lighting is still
  pinned to day by `PinDaylightPresentation` pending the night-lighting rework —
  unpinning it now gives a real sunrise/sunset.
- **Evidence:** Unity EditMode **232/232** (one helper test re-expressed in hours).
- **Resolved — iCloud (2026-09-14):** `~/Documents` is iCloud Drive and created
  `FleetVisual 2.cs`-style duplicates that broke the Unity compile. On Bailey's Mac
  the repo now lives at **`~/Code/Airside`** (outside iCloud); the old
  `~/Documents/Codex/Airside` path is a symlink to it. If duplicate-type compile
  errors reappear, look for `* 2.*` files first.

- **2026-09-14 Claude away catch-up and summary (PROJECT_PLAN core rules, `feature/away-catch-up`):**
  the airport keeps running while the game is closed. Saves are now **version 2**
  with `SavedAtUtcTicks` (v1 still loads, without catch-up). On Continue the
  restored `AirlineOperations` is advanced by real time away (≥ 60 s, capped at 7
  days) through the same event-driven update as live play, then an away summary
  says what each of your aircraft did and what needs you, plus the AI airline's
  trips. The game stays paused under the summary until dismissed. Airline time
  formatting moved to `Domain/AirlineClock`.
- **Evidence:** Unity EditMode **232/232**; `CatchUp_ReachesTheSameStateAsPlayingLive`
  compares catch-up against 13-second live stepping over 5 h 17 min. Packaged app:
  save back-dated 3 h 20 min → "You were away 3 h 21 min / VH-PAX has landed and is
  waiting for you to choose a stand / Emu Air flew 1 trip", clock 11:35, stand
  choice offered.
- **Next per PROJECT_PLAN step 3:** opening guidance for a first-time player, then a
  30-minute soak and a packaged build for one external playtester.

- **2026-09-14 Claude airline HUD layout (PROJECT_PLAN step 2, `fix/airline-hud-layout`):**
  the airline panels had hard-coded positions and were never in the fits-on-screen
  contract. `AirlineHudLayout` now places clock, fleet, toast, map and start panel
  from `HudLayout` (which gained `Viewport`): side by side on wide windows; on narrow
  ones the fleet stacks under the clock, the toast drops above the speed readout,
  and the open map covers the fleet. Tested at all six target sizes for bounds and
  overlaps. **Also fixed:** every map line (coastline, routes) scattered across the
  screen whenever the HUD scale was not 1 — `GUIUtility.RotateAroundPivot` takes a
  screen-space pivot; lines now rotate in GUI space.
- **Evidence:** Unity EditMode **228/228**. Packaged app at 800×500 (HUD scale
  0.55): start panel, clock/fleet/toast/map/readout/bar all clear, coastline
  correct.

- **2026-09-14 Claude trustworthy build (PROJECT_PLAN step 1, `fix/trustworthy-build`):**
  money is deferred (plan: no new economy subsystem before the first session is
  playable). Three fixes so the exact packaged build can be trusted:
  1. **Every aircraft pivot rebake was dead in packaged players.** `ArtGltfLoader`
     uploaded kit part meshes as no-longer-readable; the editor still allows
     reading them, players refuse (`Player.log`: 1,000 "Not allowed to access
     vertices" across props, spinners, rudder, spoilers, tyres, wheels, rims). Kit
     part meshes now stay readable; combined static kits still drop their CPU copy.
     Locked by `Atr42GltfKit_PartMeshesStayReadableForPivotRebakes`.
  2. **Airspeed readout followed the hidden demo circuit** in airline mode (118 kt
     while your aircraft taxied). It now reads the followed aircraft, else the first
     on the field, and measures taxi speed along the ground route.
  3. **Stale assertion:** `FlightPath_AirPhasesMoveAtAircraftSpeedNotTaxiSpeed`
     demanded 80 m/s mid climb-out from the pre-ADR-0044 curve; it now uses
     `CircuitProfile.InitialClimbKnots`.
- **Evidence:** Unity EditMode **221/221, 0 failed, 0 skipped**. Packaged Mac app:
  Player.log 0 "Not allowed" (was 1,000), 0 exceptions; props on their hubs;
  readout 0 kt on a parked aircraft.

- **2026-09-14 Claude airline save/load (ADR 0045, `feature/save-load`):** the
  airline game now persists. `AirlineSave` (Simulation) captures and restores
  `AirlineOperations` — clock, tower, RNG state, airlines and every aircraft's
  exact state — and rejects anything it cannot trust (version, unknown
  type/destination/airline, missing player, double-parked stand, clock mismatch).
  `AirlineSaveFile` writes JSON atomically to
  `persistentDataPath/airline-save.json`. Autosave after every command, within 2 s
  of any fleet event, every 20 s, on focus loss and on quit. The start screen
  offers **Continue** with day, time and trips; a new airline replaces the save.
  Continue rebuilds the clock and circuit at the saved time rather than stepping
  from zero. Only the airline layer is saved; the demo circuit is not (ADR 0041
  still stands for it).
- **Evidence:** Unity EditMode **216/217** (same pre-existing flight-path failure).
  `ResumedGame_ContinuesExactlyLikeOneThatNeverStopped` runs original and restored
  games 27 h further through JSON and they match, AI choices included. Packaged
  app: started a green airline, skipped to 08:52, quit, relaunched, Continue →
  same time and states, fleets drawn in 3D.
- **Watch:** old `airside-save-v1.json*` files from the removed Persistence
  assembly are still in the player's data folder; nothing reads them.
- **Next:** superseded — see the trustworthy-build entry above.

- **2026-09-13 Claude fleets in 3D (ADR 0045, `feature/fleet-3d-aircraft`):** once
  an airline starts, the field draws the fleets instead of the demo circuit. Bays
  on the terminal apron (BAY-1..4 at X −950/−800/−650/−500), pushback and taxi via
  the A↔F link at X −300 to hold at E2, lineup and backtrack, the existing
  takeoff/climb-out and approach/landing curves, backtrack-vacate to F, a queue
  for the stand on F, taxi in. Liveries: Emu Air decal (airline colour is now its
  brown #A66F32); player gets the traffic decal repainted in their colour.
  `FleetVisual` (Simulation, tested) maps fleet state to circuit phases;
  `AirsidePrototype.FleetVisuals.cs` holds routes and models; the prototype reads
  `VisualFlights` everywhere it used `_simulation.Flights`.
- **Evidence:** Unity EditMode **213/214** (same pre-existing
  `FlightPath_AirPhasesMoveAtAircraftSpeedNotTaxiSpeed` failure). Packaged Mac app
  watched end to end with Follow: pushback → taxi → hold bars → lineup → takeoff →
  hidden while away → approach → touchdown → rollout → vacate → wait on F →
  BAY-4 → parked. No exceptions in Player.log.
- **Runway time grew:** tower runway occupancy now includes a 60 s lineup and the
  whole approach plus a 90 s vacate, since all of it is drawn.
- **Watch:** Follow with nothing on the field leaves the camera where it was until
  an aircraft returns. Black upper fuselage is in the decal art (demo plane too).
- **Next:** save/load, then money.

- **2026-09-13 Claude player airline, first slice (ADR 0045, `feature/player-airline`):**
  playable at Adelaide. Start screen (name + livery), fleet panel, Australia-wide
  destinations map with range locks and live off-map tracking, schedule a
  departure, tower-sequenced runway, stand choice on landing, 10×/30×/60× and
  Skip (N). Emu Air flies two ATRs on its own. Logic is `AirlineOperations`
  (event-driven, UnityEngine-free); HUD is `AirsidePrototype.Airline.cs`.
- **Evidence:** Unity EditMode **211/212** — the 17 new airline tests pass; the one
  failure (`FlightPath_AirPhasesMoveAtAircraftSpeedNotTaxiSpeed`) also fails on
  clean `main` after #206 and is not from this branch. `scripts/build-mac.sh`
  succeeds; the packaged app was driven end to end (start → plan MEL → off-map
  tracking → landed → BAY-4 → taxi in).
- **Not yet:** the 3D aircraft are still the demo circuit and do not follow the
  fleets; no saving; no money. Next slice: drive the 3D runway movements from
  `AirlineOperations` (Takeoff/Landing states), then save/load.
- **Watch:** `DayCycle` is still one day per 20 real minutes while airline time is
  a real 24 h clock (HUD clock uses 24 h; lighting is pinned to day anyway) —
  Bailey to decide. Packaged `Player.log` shows ~536 "Not allowed to access
  vertices … isReadable is false" for the tyre/wheel/rim axle-pivot rebake, so
  the ADR 0042 wheel fix likely does not apply in builds (editor only).

- **2026-09-13 Claude player-airline design (ADR 0045, docs only):** Bailey's
  next direction is agreed — Adelaide starter airport that runs itself; the
  player runs one airline (own name/colours, 1 ATR to start) alongside AI Emu
  Air; player picks destination + departure time and the arrival stand, tower
  handles the runway; Australia-wide destinations map with range-locked far
  destinations; real-length flights with 10×/30×/60× and skip-to-next-event;
  money deferred. **Nothing built.** Next step: Bailey says go, then start the
  first slice in ADR 0045's order.

- **2026-09-13 Claude realistic circuit performance (ADR 0044):** the flight
  model is now derived from ATR 42 reference speeds rather than hand-picked
  durations. The old curve passed rotate at 179 kt and left the field at 257;
  it is 100 and 120 now. Real 3° glideslope, real flare from 30 ft floating
  300 m onto the touchdown markings with the sink arrested 584 → 60 ft/min.
  Circuit is 182 s (was 162). `CircuitProfile` is the single source of truth and
  is UnityEngine-free, so the speeds are covered headlessly.
- **Evidence:** `scripts/test-domain.sh` **140/140**; path curves replicated
  numerically — speeds within 2.3 kt, seams continuous to 0.0000 m. A live
  airspeed readout sits above the control bar; its schedule lives in
  `CircuitProfile` so the number on screen is test-covered.
- **Next gate — required:** Unity Play, and this one needs *judgement* rather
  than a checklist: does the round-out read as a landing, does the float look
  right, and is a 35-second takeoff roll (up from 19) too long to watch at 1x?
  The numbers are defensible; the feel is Bailey's call.

- **2026-09-13 Claude consolidation-graft audit (ADR 0043):** `c23cfa1` grafted
  73 branch tips into `main`'s history while discarding every one of their trees,
  so those commits read like delivered work and contain nothing. Audited all of
  them. Only the Pass A wheel fix was a real loss and it is already restored.
  Pass B is superseded, Pass C and Pass D are moot on the v02 aircraft, and the
  Adelaide landside branch is moot behind the bare-field flags. Nothing further
  restored — see the ADR before re-chasing any of those four commits.
- **Caveat that still matters:** the audit only finds lost *declarations*. A lost
  edit to an existing function is invisible to it — the `Landing(a)` compile break
  was that shape. If another defect of that kind appears, suspect the graft and
  diff the branch tip against its merge-base rather than trusting ancestry.

- **2026-09-13 Claude wheels, tyre smoke and camera (ADR 0042):** the wheel spin
  bug was a lost fix, not a new one — `RebakeWheelPivots` / `AirsideAircraftParts`
  were grafted into main's history by the consolidation without ever entering its
  tree, so the roll pass ran without the pivot rebake that makes it correct.
  Restored and locked by `AircraftPartsTests`. Rims now roll too (the old filter
  never matched them). Tyre smoke added at the real contact patches. Follow-off
  now releases the camera in place; F previously could not turn follow off at all
  because the camera and the prototype both handled the key in different phases of
  the frame.
- **Evidence:** `scripts/test-domain.sh` **121/121**; axle geometry checked
  against the shipped glTF.
- **Next gate — required:** Unity Play. Confirm the wheels turn on their axles
  (not orbiting), that tyre smoke reads well at touchdown and fades through the
  rollout, and that F / R / scroll / middle-drag behave as documented under
  "Run it".

- **2026-09-12 Claude strip to a bare circuit sandbox (ADR 0041):** the whole
  objective layer is gone — economy, routes, reputation, staffing, research,
  capacity, daily reports, turnarounds, event log, ATC phraseology, ground
  traffic — and so is the `Persistence` assembly. The game starts fresh on
  approach every launch and saves nothing. `AirportSimulation` now only drives
  one aircraft round the circuit. Both old HUDs are deleted; the HUD is one
  IMGUI bar with pause, follow, 1×, 2×, 4× plus a pause menu (Resume, Restart
  circuit, Quit). `forceSingleInstance` is on and Quit really quits.
- **Evidence:** `scripts/test-domain.sh` **89/89**. `HudLayout` checked from
  320×240 to 3456×2168 against a Rect/Mathf shim; those cases are committed in
  `PresentationLayoutTests`.
- **Next gate — required:** Presentation could not be compiled on the Linux VM
  (no Unity, no committed UnityEngine shim). A real Unity compile,
  `scripts/test-unity.sh`, `scripts/build-mac.sh` and a packaged-player check of
  the control bar, pause menu, 2× rate and single-instance quit must all pass
  before this is trusted. Aircraft visuals were deliberately untouched, so any
  change in how the aeroplane looks or animates is a regression.

- **2026-09-12 Cursor app icon (main):** BRD-002 `airside_app_icon_v01.png` is
  on `main` under Brand + candidates, wired as the default Standalone Player
  icon (`guid d7022c2c4a14457c8d291f16ba58e04a`). Prompt evidence in
  `docs/art/prompts/brd-002-app-icon-generation-2026-09-12.md`. Next verify:
  Mac Unity build → Dock / Finder / .app icon. No simulation change.
- **2026-09-12 Codex consolidation and Mac validation:** `main` now records every
  remaining remote branch tip in its history while retaining the latest Adelaide
  pavement and AIR-001 v02 tree. Obsolete branch snapshots can be deleted without
  losing their commits. Draft PR #190's v06-only wheel/gear/window work is retained
  in history and superseded by the production-proportioned v02 aircraft.
- **Repairs made during the gate:** restored Unity compilation after the landing
  path gained a lane offset; matched the 600 m flare/1,050 m rollout to approach
  speed with a 60-second landing phase; removed the takeoff/departure speed drop;
  and refreshed stale route, reputation, taxi-fillet and legacy-terrain assertions.
- **Evidence:** Unity 6.3 LTS EditMode **300/300 passed** and
  `scripts/build-mac.sh` produced a universal Mac app. `scripts/test-domain.sh`
  could not run because the standalone .NET 8 SDK is not installed on this Mac;
  Unity compiled and executed the same project tests. Next gate is packaged-player
  visual inspection of the Adelaide pavement and AIR-001 v02 motion.
- **2026-09-12 Codex aircraft finish:** branch `feature/atr42-visual-finish`,
  layered on the movement-fix branch below. AIR-001 v02 is preferred, with v01
  and v06 fallbacks. GlTF and bin ship in both Art and StreamingAssets; the new
  version key avoids silently loading the old v01 Resources prefab.
- **Player outcome / scope:** stout cabin, blunt drooped nose and rising rear pressure cone, fitted rounded
  planar cabin glazing, four broad planar cockpit panes with a narrow centre post, correctly placed passenger/cargo doors,
  reference-shaped swept fin, joined dorsal fairing and fin-crown T-tail, seated antenna, reference-area tapered high wing, compact single-piece nacelles and wing-root fairings. Existing moving-part names, props,
  gear, 24.57 × 7.59 × 22.67 m envelope and simulation remain intact.
- **Evidence:** `python scripts/test-air-001-atr42-v02.py` passes bounds, ground
  contact, articulation names, finite/nondegenerate triangles, skin winding,
  glazing clearance, complete feature inventory and positive overlap through the
  fuselage–dorsal-fairing–fin–tail-saddle–tailplane junction. 150 parts / 14,456 triangles.
  Static mesh review: `docs/art/candidates/air_001_atr42_v02_mesh_review.png`.
  Colours/lighting there are approximate, not a Unity screenshot.
- **Next / remaining gate:** Unity compile/EditMode and packaged Mac overview +
  follow at day/dusk/night; inspect gear, props, flaps, elevators and doors in
  motion. No Unity/.NET on this host. No v02 FBX/prefab bake claimed: the supported
  glTF runtime path supplies v02. Keep draft until the Unity gate passes.
- **Decision:** ADR 0040; no save migration or simulation change.

### Previous movement handoff

- **2026-09-12 Codex movement follow-up:** `feature/aircraft-movement-fixes`,
  based on the pavement branch below. Draft pending Unity validation.
- **Player outcome / scope:** commercial taxi entry retains the runway; ATC starts
  landing separation only after taxiing clear; bare-circuit holds do not claim
  runway vacation. Flight curves brake to rest, accelerate from rest and blend
  takeoff pitch into departure. Scope: CommercialFlight, AirportSimulation,
  AirsideFlightPath and their existing EditMode tests.
- **Acceptance / evidence:** four focused regression tests added for future taxi
  entry reservations, actual-vacate separation timing (including no repeated
  reset), circuit hold ownership and speed/pitch seams. `git diff --check` passed.
  Neither test suite ran: .NET SDK and Unity editor are absent from this host.
  Numerical endpoint checks passed; these are not C# compilation or Unity proof.
- **Next:** run `scripts/test-domain.sh`, then `scripts/test-unity.sh` and Mac
  overview/follow playtest: landing → rollout stop → takeoff → climb. Exercise
  the full taxi loop with traffic to verify separation starts after runway exit.
  Keep the draft unmerged until Unity checks pass, per AGENTS.md.
- **Constraints:** deterministic clock, reservations-before-use and save schema
  preserved. Existing circuit-only scene and geometry remain as below; wiring
  the new Adelaide taxi pavement into simulation is still a separate task.

### Prior pavement handoff (still applicable)

- **Last updated:** 2026-09-11 (Claude — YPAD silhouette geometry corrections)
- **Branch:** `claude/adelaide-pavement-review-41kjaa`
- **Do next:** On a Mac with Unity 6.3 LTS: checkout this branch, run
  `scripts/test-unity.sh`, `scripts/build-mac.sh`, then Play overview + follow.
  **`AirsidePrototype` has not been compiled anywhere** — it needs UnityEngine,
  and there is no editor on this Cloud Linux VM. Its five rewritten builders
  were Roslyn-parsed and type-checked against a UnityEngine shim, which catches
  syntax and signature errors but not everything. Treat the first Unity compile
  as the real check.
  Then confirm by eye, in this order:
  1. **Stubs read as taxiways, not blobs** — D/E/D2/E2, A–F links and apron
     entries should be ~23 m wide with a flare only at the junctions.
  2. **The fence sits on the ground** all the way round, not floating.
  3. Taxiway F now sits much further out (182.5 m from the runway centreline),
     so the overview framing changes — check the camera still reads well.
  4. Hold-short bars at 90 m from the runway centreline; solid taxi centrelines.
  5. Rain: 12/30 and the fillets should darken along with 05/23.
  6. Circuit still 05/23 only. Inspect Player.log.
- **In progress / half-done:** Geometry corrections implemented and
  headless-checked (236 passed). Unity Play / Mac build still required.
- **Watch for / assumptions:**
  - Layout: `AirsideAdelaidePavement` — F at 182.5 m, A at 290 m, apron at 450 m,
    RFDS at −230 m; true concave fillets (ADR 0039 supersedes 0036/0037/0038 here)
  - Fence: `AirsideAdelaidePerimeter.FenceBaseY` seats it on the landform (ADR 0039)
  - Sim taxi graph + `SkipGroundTaxi` unchanged (aircraft does not use new taxi)
  - Pre-existing headless failures, also red on `main` — exactly four:
    `Taxiing_ReleasesEachSegmentBeforeReservingTheNext`,
    `TaxiRoutes_UseDoglegThroatBeforeStandLeadIn`,
    `AwaySummary_ReportsRouteIncomeAndReputationChange`,
    `Weights_KeepDryGrassDominantAcrossTheOverviewCore` (the last is
    `AirsideTerrainField`, the 1:20 world — not the Adelaide ground)
  - Save schema / circuit skip unchanged; **no buildings** (apron pads empty)
- **Known unverified:** `CrossYawDegrees = 73°` and the decision to cross both
  runways at their midpoints are **not** checked against the published YPAD DAP.
  73° is plausible (the designators allow 61°–79°) but the comment that justified
  it was arithmetically wrong. Confirm before hanging sim topology off it.
- **Decisions:** ADR 0039 (geometry corrections). ADR 0036/0037/0038 still apply
  except where 0039 supersedes them.
- **Open question for Bailey:** next — (a) wire sim taxi onto F/A/D/E, (b) more
  DAP taxilane detail on the apron pad, or (c) first landside building?
- **Diminishing returns:** bare-circuit *aircraft* polish is done; keep pavement
  densification only while it still changes the overview read.

---

## Current milestone

A bare circuit sandbox. One ATR-class aircraft flies a continuous circuit at
Adelaide (YPAD) — approach, landing, rollout to rest, takeoff, fly-out — and
recycles onto a fresh approach. The airport sits at a named location (Kingscote
by default; Port Lincoln and Coober Pedy also available) and runs a day/night
cycle, a real 24-hour day, driving the sun and ambient
light. Deterministic weather changes through the day and drives the wet-surface,
rain and spray presentation.

The aircraft is flown to ATR 42 reference speeds — Vr 100 kt, Vapp 110,
touchdown 95, climb-out 170 — on a true 3° glideslope with a real flare
(ADR 0044). Phase durations are derived from those speeds, never picked.

There are no objectives, no economy, no scoring and no progression, and nothing
is saved between runs (ADR 0041). The player has exactly five controls — pause,
follow, and 1×/2×/4× time — plus a pause menu, and free camera orbit, zoom and
pan. What is on screen is the aeroplane, the runway and the ground.

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

- Anti-aliasing is on: High keeps 4× MSAA on the PC pipeline plus SMAA (high) on
  the runtime camera; Medium uses 2× MSAA + SMAA. Vsync is on (`vSyncCount` 1).
- The post stack runs a deliberate grade only — the template default profile's depth of field, motion blur, lens distortion, chromatic aberration, lens flare and panini are pinned off.
- The simulation keeps running when the window loses focus (`runInBackground`).

## Invariants

- Domain and simulation rules remain independent of Unity scenes.
- Nothing persists between runs; there is no save file and no schema to migrate.
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

The game opens with a short intro (any key skips). Time is **live Adelaide time**:
once an airline starts, one second in the game is one real second, and the clock
shows the real local time. There is no pause, no time rates and no skip; the menu
does not stop the airport. On-screen controls at the bottom centre are **Follow ·
Overview**, with live airspeed in knots just above.

Airline (ADR 0045): name your airline and pick a livery on the start screen. The
fleet panel (top right) plans flights and offers stands when your aircraft lands;
click a registration (or click the aircraft on the field) to follow it. The map
(top left, or Tab) shows every destination — green in range, grey locked — and
tracks aircraft that are away.

Simulation:

- Escape: clears aircraft selection and returns to overview when one is selected;
  otherwise opens or closes the menu (Resume, Restart circuit, Quit) — time keeps running
- Tab: open or close the destinations map (scroll to zoom, drag to pan; state labels appear when zoomed)
- H: open or close the Hangar (all aircraft + flight progress)
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
- Left-drag or middle-drag: pan across the field (drops follow, since panning a
  followed aircraft would only fight the follow). A left press on a HUD panel stays
  a click, and a left press only becomes a drag after a few pixels of movement so
  a plain click can still select an aircraft.
- Scroll: zoom. While following this biases the phase framing rather than
  setting an absolute distance, so it survives the follow easing instead of
  being erased on the next frame.
- WASD: pan (free camera only)
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

## Current evidence

- **Circuit visible polish** on `cursor/circuit-visible-polish-0c44`: bare HUD
  hides economy chrome; coast muted; engine range 220 m; gear doors transit-only;
  glass prop discs; landing follow framing; soft rotate cue.
  **Verified headless:** `scripts/test-domain.sh` **214 passed** (1 new FocusMode HUD test; 4 pre-existing failures also red on main). **Not yet
  verified:** Unity EditMode / Play, `scripts/test-unity.sh`, `scripts/build-mac.sh`.


- **Plane / ground dynamics polish** on `cursor/plane-ground-dynamics-polish-0c44`:
  Adelaide authored ground mesh (ADR 0035) replaces the 16 m tiled grass cube;
  runway keeps 3 100 × 45 m with multi-scale asphalt + outside dirt shoulders;
  touchdown smoke restored independent of world props; gear eases; tires spin from
  distance/radius; oleo settle; softer prop disc; ATR material/LOD polish.
  **Verified headless:** `scripts/test-domain.sh` **213 passed** (6 new Adelaide
  ground tests); 4 pre-existing failures also red on `main`. **Not yet verified:**
  Unity EditMode / Play, `scripts/test-unity.sh`, `scripts/build-mac.sh`, packaged
  overview/follow loops (no Unity editor on this Cloud Linux VM).

- **Final AIR-001 ATR 42-class starter** on `feature/atr42-final-aircraft`:
  production identity `mdl_atr42_starter_v01`, exact 22.67 × 24.57 × 7.59 m
  three-view envelope and 3.93 m six-blade props. Six-wheel gear, doors and
  restrained flight controls are separate and runtime-pivoted. The Resources
  prefab and StreamingAssets fallback are integrated; targeted aircraft Unity
  tests pass and the packaged Mac follow view has been inspected. Decision 0034.

- **Circuit flight-state cues** on `cursor/bare-adelaide-field-bc75`: ATR circuit
  gear / lights / props / one-shot touchdown at `TouchdownProgress`. Cabin doors
  stay shut on the circuit.

- **Real-metre runway markings** on `cursor/bare-adelaide-field-bc75`: the 3 100 ×
  45 m slab now has ICAO-ish threshold bars (12 per end), aiming points at 400 m,
  dashed centreline (30/20), 0.90 m edge lines, and TDZ pairs at 150/300/600/750/900 m.
  Numbers live in `AirsideRunwayMarkings` (no UnityEngine). The 300 m pair is
  centred on `AirsideFlightPath.TouchdownX` (-1250). Paint is combined per family,
  not hundreds of cubes. WLD-001 is not used.

- **Bare Adelaide field** on `cursor/bare-adelaide-field-bc75`: visible world is
  one 3 100 × 45 m runway (YPAD 05/23), 3 400 × 2 309 m / 785 ha empty ground,
  one turboprop and pinned daylight. No buildings, cars, signs, taxiways or
  decorative lights. Decision 0032. `scripts/test-domain.sh` is the headless
  check for the new metre constants.

- CC0 Unity Terrain ground on `feature/cc0-terrain-ground`: 256 × 220 × 8 m
  TerrainData (heightmap 257, alphamap 256), four CC0 TerrainLayers on the
  built-in URP Terrain Lit shader, plus a Poly Haven worn-concrete apron.
  Operational plateau dead level at **-0.0450 min and max** across X [-64, 66] ×
  Z [-12, 60], lowest pad **3.5 cm** proud; normalized heights **0.1053–0.5538**
  (no clamping); relief **3.57 m over 220 m**; overview core **67.1% dry grass,
  15.0% green, 17.8% worn dirt**; dirt shoulder **2.20–3.20 m**; lag correlation
  decays monotonically **0.799 at 11 m → 0.430 at 32 m** with no resurgence at
  any tile size. Albedo tile-scale luminance spread **0.5–2.0 points** with
  detail std **8.9–23.8**. `scripts/test-domain.sh` **200 passed** (19 new
  terrain tests, mutation-checked). Decision 0031. **The bake has not been run:
  needs `scripts/bake-terrain.sh` on the Mac, then Unity compile, Mac build and
  packaged day/dusk/night/rain QA.**

- Runtime airfield performance **P0–P2 plus GPU-state + paint/probe/kit-combine
  pass, merged to `main` as PR #183**: combined operational pads (6)
  replace 745 Terrain11 tiles; textures/materials cached; Addressables on demand;
  High/Medium ladder; probe bands. Per-frame `Renderer.material` clones removed;
  scene index; one star mesh; probes `RenderProbe` after world combine. Taxi
  paint is strips not 1 m cubes; kit fence/forecourt/GSE/planters/chocks/belt
  loader stamp cached combined meshes; static combine skips moving GSE/clouds/
  birds/boats/`antenna_dish`. Ambient audio `Resources.Load` is deferred off
  Awake. Medium thins fillet lights, fence rails, window PointLights and scrub.
  `AirsidePrototype.cs` brace depth 0. Decision 0029. Mac Play visual-first-frame
  still required.

- Layering / collision / route **100-fix** on `cursor/layering-collision-bugfix-100-d7f0`: dogleg lead-ins, apron throat, stand spacing 14/24/34, GT off-field + run-up bay, selective yield, `scripts/test-domain.sh` **177 passed** (`CollisionPass100Tests`).

- Fidelity-board integration **merged via #167**: scrub/terrain v02, surface
  `tx_*_v02` + wet concrete, ARFF prefab v02 densify, CHR dual wands,
  turnaround GSE zone layout, docs Approved · Integrated.
- `scripts/test-domain.sh`: **136/136** on the integrate branch before merge.
- Day/night readability merged via #158 (Mac noon/midnight overview pending).
- 50-item bugfix pass merged via #157.
- Eucalyptus VEG-001 v02 merged via #156 (Mac overview vs REF still pending).
- Forecourt PRP-003 v02 merged via #155 (Mac overview vs REF still pending).
- Fence/gate PRP-002 v02 merged via #154 (Mac overview vs REF still pending).
- Character kits CHR-001/002 v02 merged via #153 (Mac overview/follow vs REF-003
  still pending).

## Next work

1. Watch the loop in Unity Play (F, one circuit, no HUD). Then **one taxiway
   and one stand** only when Bailey says so.
2. No new economy systems; no Companion/CloudKit; no buildings/GSE restore.
3. **Player airline at Adelaide (ADR 0045).** First slice built on
   `feature/player-airline` (everything except save/load); fleets drawn in 3D on
   `feature/fleet-3d-aircraft`; save/load on `feature/save-load`. Next: economy.
