# Airside active brief

## Vision

Airside is a real-time, persistent airport management game for Mac. The player designs and manages the system while aircraft, passengers and ground services operate automatically. Watching the airport work should be satisfying, and every delay should have an understandable cause.

## Where to resume — session handoff

This block is the first thing to read and the last thing to update. Any tool
(Claude, Cursor, ChatGPT via a person) overwrites it when it stops work, so the
next session can continue without seeing the previous conversation. Keep it short.

- **Last updated:** 2026-09-06 by Claude (rebasing economics-foundation
  branch onto Cursor's merged Batch C/D + Passenger Services work)
- **Branch / working tree:** `claude/game-dev-status-pd3sg4`, rebased onto
  `main` (which now includes Cursor's merged #15 Batch C/D + Passenger
  Services, and #16 playtest HUD/taxi visuals). Pushed; **not yet merged to
  `main`**. **Not Unity-verified** — this session has no Unity executable or
  `dotnet`/NUnit runner, so `scripts/test-unity.sh` (or Cursor's local
  Domain/Simulation/Persistence harness) could not be run against it. Compile
  and run edit-mode tests before trusting this branch or merging it.
- **Do this next:**
  1. Open in Unity 6.3 LTS, confirm it compiles, run `scripts/test-unity.sh`.
     Fix anything that doesn't compile or pass before merging. **In
     particular, visually check the HUD** — the left panel box grew from
     height 520 to 616 and gained two new label+button rows (check-in
     expansion, GA apron expansion) at fixed pixel `Rect`s in the pre-rebase
     layout, placed by calculation, not by looking at it render — confirm
     nothing overlaps or clips off-panel at the default and a couple of other
     window sizes.
  2. Bailey review of Batch C look (approve or request `_v02`) — separate
     from this branch, still outstanding on `main`.
  3. Then either continue Batch D animation/VFX polish or Batch B/C follow-up
     on `main`, independent of this branch.
  4. Consider step 17's one remaining gap: airline profiles are still flat
     (name/destination only) — no per-airline service level, price sensitivity
     or facility requirements. Deliberately not attempted: the existing
     route-generation formula and its reputation/income numbers are tightly
     asserted by several tests (`AirportRoutesTests`, `ReputationTests`) that
     this session could not run, so a change risked an unverifiable
     regression for a "nice to have."
  5. Maintenance/incidents were deliberately skipped on this branch — any
     periodic incident cost needs re-running `DailyReportTests`/`StaffingTests`
     and the soak tests, which this session can't do.
- **Operations-panel layout budget:** the left HUD box (still the literal
  `Rect(22, 22, 410, 520)` in the pre-rebase `AirsidePrototype.cs` — `HudLayout.cs`
  exists with resolution-independent math but isn't wired into `OnGUI` yet,
  per `PresentationLayoutTests.cs`) is near its height ceiling. This branch's
  terminal/GA buttons pushed it to 616 tall against a `Screen.height / 720f`
  canvas; cargo/land/baggage were left without buttons for the same reason.
  Whoever wires `HudLayout.Create` into `OnGUI` should fold all five
  buildable-upgrade rows into that pass rather than stacking more literals.
- **In progress / half-done:** this branch's terminal-capacity,
  general-aviation, cargo, land-reservation and baggage foundations
  (decisions renumbered 0028–0032 after this rebase, since Cursor's Passenger
  Services research took 0023 on `main` first) are implemented at the
  domain/simulation layer and unit-tested on paper, but **unverified in
  Unity** — no compile or edit-mode test run happened this session. Terminal
  and GA have HUD buttons in the pre-rebase HUD; cargo, land and baggage do
  not. Everything else on `main`: Batch C 3D models and Batch D
  animation/VFX greybox hooks generated and integrated (primitives still the
  fallback pending Bailey's approval), Passenger Services research, playtest
  HUD/taxi-visual fixes.
- **Watch out for:** fleet corridor invariants (0006–0009). Concurrent
  commercials: fleet yields to any commercial. `DailyReport`'s constructor
  signature changed on this branch (added `generalAviationIncome`,
  `cargoIncome`) — its one call site (`AirportSimulation.SettleDaysUpTo`) was
  updated in the same change, but double-check no other call site was missed
  after the rebase. Decision numbering: art pipeline is 0022, Passenger
  Services research is 0023 (both on `main`); this branch's five foundations
  are renumbered 0028–0032 to avoid colliding with 0023.
- **Open questions for Bailey:** Approve Batch C look, or request `_v02`?
  (carried from `main`, unrelated to this branch's work)
- **Visual assets:** Batch A Approved; Batch B Approved (surfaces
  Integrated); Batch C Generated/Modelled, pending Bailey approval; no
  runtime art from this branch's work (it's economics-only). Batch C task
  packet this branch wrote is now superseded by Cursor's actual Batch C
  generation on `main` — safe to disregard.

Full start-of-session and end-of-session checklists are in `AGENTS.md` →
"Session handoff protocol".

## Current milestone

Toward the first playable airport. The airport sits at a named location
(Kingscote, Kangaroo Island by default; Port Lincoln and Coober Pedy also
available) and runs a day/night cycle — one simulated day every 20 real minutes,
driving the sun and ambient light and shown on the HUD. Airlines propose scheduled routes on a timer; the player accepts (or declines) an offer and every
completed flight then pays a recurring per-flight amount. Schedule demand is capped by stand
capacity (`StandCount × 6` flights/day) so acceptance cannot outrun the airfield. The airport's reputation
(0–100) rises with on-time departures and falls with delays; airlines gate their
proposals on it and pay more when it is high. Deterministic weather changes
through the day and, with a base fee and crew payroll, is charged as a daily
running cost — so the airport now has expenses it must cover, not just income.
The player employs ground crew: the baseline runs turnarounds normally, extra
crew speed them up, and understaffing stretches them into delays. The player can
buy a third stand for 8000 — the first buildable capacity upgrade. Research unlocks
progression: Operations Efficiency (−$100/day running cost), then Passenger Services
(+$75 route income per departed commercial). If cash stays negative across three
consecutive day closes, the airport is declared insolvent and the simulation stops.

When accepted route demand reaches four flights/day, a second commercial aircraft operates alongside the first (stands never double-book). Still current: simultaneous traffic. A ground-traffic fleet shares the airfield with the primary flight: `GT-201` runs a repeating arrival / stand dwell / departure schedule on whichever stand the primary flight is not using, and `GT-202` repositions in and out via a run-up bay without using a stand. Fleet aircraft reserve a single-file corridor lock for the whole time they are on the A1/A2 taxiway, so they queue rather than meet head-on. The primary flight keeps absolute priority on the segments themselves; a hold beyond ten seconds is explained by the traffic wait monitor. The design is deadlock-free by construction.

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

## Invariants

- Domain and simulation rules remain independent of Unity scenes.
- Any save-schema change ships with an explicit version bump and a migration path (see `AirsideSaveData.Migrate`).
- Time comes from an injected clock.
- Random choices come from a seeded source.
- Runways, taxiways and stands must be reserved before use.
- Frame rate must not change simulation outcomes.
- No external data or asset enters the project without a recorded licence.

## Run it

Open `game/Airside` in Unity 6.3 LTS and press Play.

- Space: pause or resume simulation
- Tab: switch between 1× and 4× time
- Right-drag: orbit camera
- Scroll: zoom
- WASD: pan overview
- F: follow aircraft
- O: return to overview
- P: hire a priority turnaround crew while the aircraft is at stand

Run checks with `scripts/test-unity.sh`. Build the local Mac app with `scripts/build-mac.sh`.

## Current evidence

- Local harness compiles Domain/Simulation/Persistence and runs 94 deterministic NUnit tests (including concurrent-flight soak, research progression, and step identity). Unity edit-mode via `scripts/test-unity.sh` still needs a Mac editor.
- A fifty-cycle simulation completes without reservation conflicts (single and dual commercial).
- Large and one-second time steps reach identical simulation state.
- When scheduled demand ≥ 4 flights/day a second commercial operates on a half-cycle stagger; fleet yields to any commercial; HUD/world show both.
- Turnaround dependencies, disruptions, priority crews and delay costs are covered by tests.
- Continuous play and offline replay produce matching operational and financial state.
- Save recovery, backward clock handling and a bounded thirty-day absence are covered by tests.
- The airport has a real-world location and a deterministic day/night cycle; a schema-1 save migrates to schema 2 (adding the location) on load.
- Airlines propose routes on a schedule; accepting one is a persisted command that survives reload and offline catch-up and pays out on every completed flight. Acceptance also refuses when the projected schedule would exceed stand capacity (12 flights/day on two stands).
- Reputation moves with on-time vs delayed departures, gates which proposals can be accepted, and raises the per-flight payment locked in at acceptance.
- Weather is deterministic from the timeline; each simulated midnight the airport pays a base running cost, a weather surcharge and crew payroll, identical under live play and offline catch-up.
- Ground-crew headcount is a persisted decision (replayed on load); the baseline leaves turnaround timing byte-identical to before, extra crew shorten it, understaffing lengthens it.
- Each midnight publishes a daily operations report (flights, income, delays, running cost, net cash, reputation); latest seven kept; HUD shows the latest.
- Operations Efficiency research (2500, one simulated day) permanently reduces base daily running cost by 100; start is command-replayed. The daily finance brief subtracts that discount from expected operating cost.
- Passenger Services research (3500, one simulated day) unlocks after Ops Efficiency and permanently adds +$75 route income per departed commercial; start command `start-research-passenger-services` is replayed on load (decision 0023). Local harness: 94 deterministic Domain/Simulation/Persistence tests pass (Unity edit-mode still needs Mac).
- A buildable third stand (8000, `build-stand`) expands capacity; taxi, ground traffic and the HUD use it; two-stand seeds stay identical.
- **Unverified in Unity (written this session, not yet compiled/tested there):**
  a terminal check-in capacity (`AirportTerminal`, decision 0028) gates
  scheduled flights/day alongside stand count, with a buildable expansion
  (`expand-checkin`, 6000); and a general-aviation daily landing-fee income
  (`AirportGeneralAviation`, decision 0029) settles at every midnight, with a
  buildable apron expansion (`expand-ga-apron`, 4000); cargo
  (`AirportCargo`, decision 0030) settles its own daily contract income the
  same way, with a buildable warehouse expansion (`expand-cargo-warehouse`,
  5000); and a one-off second-runway land reservation (`AirportLand`,
  decision 0031, `reserve-second-runway-land`, 10000) with no coupled effect
  yet. All four follow the third-stand pattern (persisted command, replayed
  on load, no save-schema change) and ship with new EditMode tests. Terminal
  and GA have HUD buttons; cargo and land do not (operations-panel height
  budget — see the layout note above).
- Three consecutive negative day closes declare insolvency: the simulation freezes, commands refuse, and an `"Insolvent"` event is logged (identical under large and small time steps; rebuilt by replay).
- Named taxi routes connect both stands through shared reserved segments.
- The event history produces an ordered, player-readable account of each flight.
- Taxi movements release shared segments progressively instead of locking the whole route.
- A competing owner cannot enter an occupied segment, and prolonged waits produce a diagnostic.
- A ground-traffic fleet (`GT-201` arrive/depart, `GT-202` repositioning) shares the taxi segments and stands through the reservation table without ever blocking the primary flight; a single-file corridor lock keeps at most one fleet aircraft on the A1/A2 taxiway at a time, and a free corridor goes to the longest-waiting aircraft (30 edit-mode tests, including a forty-cycle soak asserting the corridor invariant, no starvation, and zero primary-flight conflicts).
- Fleet aircraft move identically under large and small time steps.
- The project compiles in Unity 6.3 LTS and builds a macOS player.

## Next work

1. **Bailey review of Batch C** when convenient (not blocking further work).
2. Get `claude/game-dev-status-pd3sg4` (this branch) compiled and edit-mode
   tested in Unity 6.3 LTS, then merge — it adds terminal/GA/cargo/land/
   baggage economic foundations on top of Cursor's Batch C/D + Passenger
   Services work.
3. **Batch C Integration** after Approve (wire glTF prefabs; primitives stay
   fallback), and wire `HudLayout.Create` into `OnGUI` while also adding this
   branch's cargo/land/baggage buttons in the same pass.
4. **Unity Play soak** whenever Bailey has the editor (textures, lights, dual
   commercials, and this branch's new systems).
5. Longer term: airline profiles (service level, price sensitivity, facility
   requirements) to round out project-plan step 17; maintenance/incidents
   once someone can re-run the full suite after adding a recurring cost; real
   per-flight baggage/passenger-flow depth and a visible GA/cargo aircraft
   loop are the natural next layer on top of the economic foundations landed
   on this branch (project-plan steps 19–21 continued).

Implement concurrent-flights **slice 1**
(`docs/product/concurrent-flights-slice1-packet.md`). Merge open PRs #3–#6 when
ready. Unity edit-mode + Play soak when Bailey can run the editor again.
Confirm Unity tests when available. Merge open feature PRs (insolvency, third
stand, research). Then the concurrent-flights design pass
(`docs/product/concurrent-flights-brief.md`) or the overdue visual soak.
Confirm Unity edit-mode tests and a short Play soak for research. Merge or soak
open capacity / insolvency PRs. Then the overdue visual soak, or the
concurrent-flights design pass (`docs/product/concurrent-flights-brief.md`).
Remaining phase-four filler: a daily report panel.
Keep merging the remaining phase-four stack onto `main`. Visual soak of the
build in Unity when Bailey can play. Concurrent-flights design/implementation
PRs are next after research and the daily report.
Merge open feature PRs (#3–#7), rebase this slice onto `main`, then Unity Play soak of dual commercials. Tune threshold/stagger after Play if needed.
