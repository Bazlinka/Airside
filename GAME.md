# Airside active brief

## Vision

Airside is a real-time, persistent airport management game for Mac. The player designs and manages the system while aircraft, passengers and ground services operate automatically. Watching the airport work should be satisfying, and every delay should have an understandable cause.

## Where to resume — session handoff

This block is the first thing to read and the last thing to update. Any tool
(Claude, Cursor, ChatGPT via a person) overwrites it when it stops work, so the
next session can continue without seeing the previous conversation. Keep it short.

- **Last updated:** 2026-09-06 by Claude (second aircraft picks a free stand)
- **Branch / working tree:** `main`, clean, pushed to `origin`
- **Do this next:** Visual soak — run the Mac build for a long session and watch
  the two aircraft share A1/A2 and use opposite stands; confirm no stutter or
  stuck traffic. Then move toward a third aircraft (the leg model + priority rule
  in `GroundTrafficAircraft` should generalise), or vary the second aircraft's
  schedule timings.
- **In progress / half-done:** nothing — 26/26 edit-mode tests pass, macOS build ok
- **Watch out for:** the second aircraft (`GT-201`) always yields to the primary
  flight by design, so `ReservationConflicts` stays zero and only the traffic
  wait monitor records its waits. It locks its stand choice at the start of each
  arrival — a later primary reassignment to that stand is resolved by yielding,
  not by switching mid-taxi. Schedule is a data-driven leg list — decisions
  0006 and 0007.
- **Open questions for Bailey:** none

Full start-of-session and end-of-session checklists are in `AGENTS.md` →
"Session handoff protocol".

## Current milestone

Phase five: simultaneous traffic. A second aircraft (`GT-201`) runs its own repeating arrival, stand dwell and departure schedule, parking on whichever stand the primary flight is not using and reserving the shared taxi segments A1 and A2 and the stand through the same reservation table as the primary flight. The primary flight has priority: the second aircraft releases any resource the flight needs and holds position until it is free, and a hold beyond ten seconds is explained by the traffic wait monitor.

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
- A second aircraft runs its own arrival, stand and departure schedule, parking on whichever stand the primary flight is not assigned, sharing the taxi segments and stands through the reservation table without ever blocking the primary flight (26 edit-mode tests, including multi-cycle soaks with the second aircraft active).
- The second aircraft moves identically under large and small time steps.
- The project compiles in Unity 6.3 LTS and builds a macOS player.

## Next work

Run a long visual soak of the two-aircraft build. Then replace the second aircraft's fixed shuttle with its own arrival/departure schedule, still governed by the segment reservations, working toward several simultaneous aircraft.
