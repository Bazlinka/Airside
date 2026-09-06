# Airside active brief

## Vision

Airside is a real-time, persistent airport management game for Mac. The player designs and manages the system while aircraft, passengers and ground services operate automatically. Watching the airport work should be satisfying, and every delay should have an understandable cause.

## Where to resume — session handoff

This block is the first thing to read and the last thing to update. Any tool
(Claude, Cursor, ChatGPT via a person) overwrites it when it stops work, so the
next session can continue without seeing the previous conversation. Keep it short.

<<<<<<< HEAD
- **Last updated:** 2026-09-06 by Cursor (third-stand capacity upgrade)
- **Branch / working tree:** `cursor/third-stand-capacity-38b9` (PR against `main`)
- **Do this next:** Confirm Unity edit-mode tests (`scripts/test-unity.sh`) and a
  short Play soak of the third stand. Then the overdue visual soak, or the
  concurrent-flights design pass (`docs/product/concurrent-flights-brief.md`).
  Remaining phase-four fillers: research, daily report panel, insolvency HUD
  (simulation insolvency may land from a parallel PR).
- **In progress / half-done:** nothing once this PR merges. Buildable third stand
  (`build-stand`, $8000) expands capacity to 3; taxi + ground traffic + HUD
  updated. No save-schema change. 65 Domain/Simulation/Persistence tests green
  under a local dotnet harness.
- **Watch out for:** with two stands, primary stand choice and ground-traffic
  alternate-stand selection stay seed-identical to before. Do not persist
  `StandCount` — rebuild via command replay. Keep corridor invariants
  (decisions 0006–0009).
=======
- **Last updated:** 2026-09-06 by Cursor (insolvency / game-over)
- **Branch / working tree:** `cursor/insolvency-game-over-38b9` (PR against `main`)
- **Do this next:** Confirm edit-mode tests in Unity 6.3 LTS (`scripts/test-unity.sh`)
  and a short Play-mode soak. Then either a **HUD insolvency banner** (presentation
  follow-up) or the overdue visual soak / concurrent-flights fork — see
  `docs/product/concurrent-flights-brief.md`.
- **In progress / half-done:** nothing on this branch once merged. Simulation-only
  insolvency: three consecutive negative day closes → `IsInsolvent`, event log,
  frozen update. No save-schema change. Presentation intentionally untouched.
- **Watch out for:** insolvency is rebuilt by replay (like reputation). Do not
  persist `IsInsolvent`. Day-end check runs after operating-cost settlement; a
  positive midnight cash balance resets the consecutive counter. Keep the fleet
  corridor invariants (decisions 0006–0009) when touching ground traffic.
>>>>>>> origin/main
- **Open questions for Bailey:** none

Full start-of-session and end-of-session checklists are in `AGENTS.md` →
"Session handoff protocol".

## Current milestone

Toward the first playable airport. The airport sits at a named location
(Kingscote, Kangaroo Island by default; Port Lincoln and Coober Pedy also
available) and runs a day/night cycle — one simulated day every 20 real minutes,
driving the sun and ambient light and shown on the HUD. Airlines now propose
scheduled routes on a timer; the player accepts (or declines) an offer and every
completed flight then pays a recurring per-flight amount. The airport's reputation
(0–100) rises with on-time departures and falls with delays; airlines gate their
proposals on it and pay more when it is high. Deterministic weather changes
through the day and, with a base fee and crew payroll, is charged as a daily
running cost — so the airport now has expenses it must cover, not just income.
The player employs ground crew: the baseline runs turnarounds normally, extra
<<<<<<< HEAD
crew speed them up, and understaffing stretches them into delays.
The player can buy a third stand for 8000 — the first buildable capacity upgrade.
=======
crew speed them up, and understaffing stretches them into delays. If cash stays
negative across three consecutive day closes, the airport is declared insolvent
and the simulation stops.
>>>>>>> origin/main

Still current: simultaneous traffic. A ground-traffic fleet shares the airfield with the primary flight: `GT-201` runs a repeating arrival / stand dwell / departure schedule on whichever stand the primary flight is not using, and `GT-202` repositions in and out via a run-up bay without using a stand. Fleet aircraft reserve a single-file corridor lock for the whole time they are on the A1/A2 taxiway, so they queue rather than meet head-on. The primary flight keeps absolute priority on the segments themselves; a hold beyond ten seconds is explained by the traffic wait monitor. The design is deadlock-free by construction.

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

- Twenty edit-mode tests pass.
- A fifty-cycle simulation completes without reservation conflicts.
- Large and one-second time steps reach identical simulation state.
- Turnaround dependencies, disruptions, priority crews and delay costs are covered by tests.
- Continuous play and offline replay produce matching operational and financial state.
- Save recovery, backward clock handling and a bounded thirty-day absence are covered by tests.
- The airport has a real-world location and a deterministic day/night cycle; a schema-1 save migrates to schema 2 (adding the location) on load.
- Airlines propose routes on a schedule; accepting one is a persisted command that survives reload and offline catch-up and pays out on every completed flight.
- Reputation moves with on-time vs delayed departures, gates which proposals can be accepted, and raises the per-flight payment locked in at acceptance.
- Weather is deterministic from the timeline; each simulated midnight the airport pays a base running cost, a weather surcharge and crew payroll, identical under live play and offline catch-up.
- Ground-crew headcount is a persisted decision (replayed on load); the baseline leaves turnaround timing byte-identical to before, extra crew shorten it, understaffing lengthens it.
<<<<<<< HEAD
- A buildable third stand (8000, `build-stand`) expands capacity; taxi, ground traffic and the HUD use it; two-stand seeds stay identical.
=======
- Three consecutive negative day closes declare insolvency: the simulation freezes, commands refuse, and an `"Insolvent"` event is logged (identical under large and small time steps; rebuilt by replay).
>>>>>>> origin/main
- Named taxi routes connect both stands through shared reserved segments.
- The event history produces an ordered, player-readable account of each flight.
- Taxi movements release shared segments progressively instead of locking the whole route.
- A competing owner cannot enter an occupied segment, and prolonged waits produce a diagnostic.
- A ground-traffic fleet (`GT-201` arrive/depart, `GT-202` repositioning) shares the taxi segments and stands through the reservation table without ever blocking the primary flight; a single-file corridor lock keeps at most one fleet aircraft on the A1/A2 taxiway at a time, and a free corridor goes to the longest-waiting aircraft (30 edit-mode tests, including a forty-cycle soak asserting the corridor invariant, no starvation, and zero primary-flight conflicts).
- Fleet aircraft move identically under large and small time steps.
- The project compiles in Unity 6.3 LTS and builds a macOS player.

## Next work

<<<<<<< HEAD
Confirm Unity edit-mode tests and a short Play soak for the third stand. Then the
overdue visual soak of the two-aircraft build, or the concurrent-flights design
pass (`docs/product/concurrent-flights-brief.md`). Remaining phase-four fillers:
research, a daily report panel, insolvency presentation follow-up.
=======
Confirm Unity edit-mode tests and a short Play soak for insolvency. Optional
presentation follow-up: a clear HUD banner when `IsInsolvent`. Then the overdue
visual soak of the two-aircraft build, or the concurrent-flights design pass
(`docs/product/concurrent-flights-brief.md`).
>>>>>>> origin/main
