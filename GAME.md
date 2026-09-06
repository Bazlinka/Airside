# Airside active brief

## Vision

Airside is a real-time, persistent airport management game for Mac. The player designs and manages the system while aircraft, passengers and ground services operate automatically. Watching the airport work should be satisfying, and every delay should have an understandable cause.

## Where to resume — session handoff

This block is the first thing to read and the last thing to update. Any tool
(Claude, Cursor, ChatGPT via a person) overwrites it when it stops work, so the
next session can continue without seeing the previous conversation. Keep it short.

- **Last updated:** 2026-09-06 by Claude (ground-traffic fleet + corridor lock)
- **Branch / working tree:** `main`, clean, pushed to `origin`
- **Do this next:** Visual soak — run the Mac build for a long session and watch
  the primary flight, `GT-201` (to a stand) and `GT-202` (run-up bay) share
  A1/A2; confirm the queue behaviour reads well and nothing stutters or sticks.
  Then: a fairer corridor hand-off (fleet order currently wins after a primary
  preemption), or add a third fleet aircraft, or start promoting the primary
  flight into the same aircraft list (decision 0008).
- **In progress / half-done:** nothing — 28/28 edit-mode tests pass, macOS build ok
- **Watch out for:** the fleet is deadlock-free *by construction* — the primary
  flight is never blocked, at most one fleet aircraft holds the corridor lock,
  and repositioning aircraft never touch a stand. Keep those three properties
  when changing `GroundTrafficAircraft` or `SynchronizeAllTraffic`. Cosmetic:
  a fleet aircraft snaps to its leg start if the primary preempts a segment
  under it. Decisions 0006–0008.
- **Open questions for Bailey:** none

Full start-of-session and end-of-session checklists are in `AGENTS.md` →
"Session handoff protocol".

## Current milestone

Phase five: simultaneous traffic. A ground-traffic fleet shares the airfield with the primary flight: `GT-201` runs a repeating arrival / stand dwell / departure schedule on whichever stand the primary flight is not using, and `GT-202` repositions in and out via a run-up bay without using a stand. Fleet aircraft reserve a single-file corridor lock for the whole time they are on the A1/A2 taxiway, so they queue rather than meet head-on. The primary flight keeps absolute priority on the segments themselves; a hold beyond ten seconds is explained by the traffic wait monitor. The design is deadlock-free by construction.

## Invariants

- Domain and simulation rules remain independent of Unity scenes.
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
- Named taxi routes connect both stands through shared reserved segments.
- The event history produces an ordered, player-readable account of each flight.
- Taxi movements release shared segments progressively instead of locking the whole route.
- A competing owner cannot enter an occupied segment, and prolonged waits produce a diagnostic.
- A ground-traffic fleet (`GT-201` arrive/depart, `GT-202` repositioning) shares the taxi segments and stands through the reservation table without ever blocking the primary flight; a single-file corridor lock keeps at most one fleet aircraft on the A1/A2 taxiway at a time (28 edit-mode tests, including multi-cycle soaks asserting the corridor invariant and zero primary-flight conflicts).
- Fleet aircraft move identically under large and small time steps.
- The project compiles in Unity 6.3 LTS and builds a macOS player.

## Next work

Run a long visual soak of the two-aircraft build. Then replace the second aircraft's fixed shuttle with its own arrival/departure schedule, still governed by the segment reservations, working toward several simultaneous aircraft.
