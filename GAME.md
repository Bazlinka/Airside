# Airside active brief

## Vision

Airside is a real-time, persistent airport management game for Mac. The player designs and manages the system while aircraft, passengers and ground services operate automatically. Watching the airport work should be satisfying, and every delay should have an understandable cause.

## Where to resume — session handoff

This block is the first thing to read and the last thing to update. Any tool
(Claude, Cursor, ChatGPT via a person) overwrites it when it stops work, so the
next session can continue without seeing the previous conversation. Keep it short.

- **Last updated:** 2026-09-06 by Cursor (merging phase-four stack; Bailey said ship it)
- **Branch / working tree:** `cursor/third-stand-capacity-38b9` → merging to `main`
- **Do this next:** Keep merging research → daily report → concurrent flights →
  accept-capacity → daily P&L. **Visual soak in Unity** when Bailey can play.
- **In progress / half-done:** insolvency is already on `main`. Third stand landing
  now. Do not persist `StandCount` or `IsInsolvent` — rebuild via replay.
- **Watch out for:** fleet corridor invariants (decisions 0006–0009). Two-stand
  behaviour must stay seed-identical until the third stand is built.
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
crew speed them up, and understaffing stretches them into delays. The player can
buy a third stand for 8000 — the first buildable capacity upgrade. If cash stays
negative across three consecutive day closes, the airport is declared insolvent
and the simulation stops.

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
- A buildable third stand (8000, `build-stand`) expands capacity; taxi, ground traffic and the HUD use it; two-stand seeds stay identical.
- Three consecutive negative day closes declare insolvency: the simulation freezes, commands refuse, and an `"Insolvent"` event is logged (identical under large and small time steps; rebuilt by replay).
- Named taxi routes connect both stands through shared reserved segments.
- The event history produces an ordered, player-readable account of each flight.
- Taxi movements release shared segments progressively instead of locking the whole route.
- A competing owner cannot enter an occupied segment, and prolonged waits produce a diagnostic.
- A ground-traffic fleet (`GT-201` arrive/depart, `GT-202` repositioning) shares the taxi segments and stands through the reservation table without ever blocking the primary flight; a single-file corridor lock keeps at most one fleet aircraft on the A1/A2 taxiway at a time, and a free corridor goes to the longest-waiting aircraft (30 edit-mode tests, including a forty-cycle soak asserting the corridor invariant, no starvation, and zero primary-flight conflicts).
- Fleet aircraft move identically under large and small time steps.
- The project compiles in Unity 6.3 LTS and builds a macOS player.

## Next work

Keep merging the remaining phase-four stack onto `main`. Visual soak of the
build in Unity when Bailey can play. Concurrent-flights design/implementation
PRs are next after research and the daily report.
