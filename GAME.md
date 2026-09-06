# Airside active brief

## Vision

Airside is a real-time, persistent airport management game for Mac. The player designs and manages the system while aircraft, passengers and ground services operate automatically. Watching the airport work should be satisfying, and every delay should have an understandable cause.

## Where to resume — session handoff

This block is the first thing to read and the last thing to update. Any tool
(Claude, Cursor, ChatGPT via a person) overwrites it when it stops work, so the
next session can continue without seeing the previous conversation. Keep it short.

- **Last updated:** 2026-09-06 by Claude (repo + collaboration setup)
- **Branch / working tree:** `main`, clean, everything pushed to `origin`
- **Do this next:** Introduce a second simultaneous aircraft using the segment
  reservations, then run a longer visual soak with opposing ground traffic.
- **In progress / half-done:** nothing — safe to start fresh
- **Watch out for:** `TrafficWaitMonitor` and the atomic segment reservations
  landed but the Unity edit-mode tests were not re-run in the setup environment;
  run `scripts/test-unity.sh` before building on that code.
- **Open questions for Bailey:** none

Full start-of-session and end-of-session checklists are in `AGENTS.md` →
"Session handoff protocol".

## Current milestone

Phase five: prepare the airfield for simultaneous traffic. Taxiing aircraft now reserve only the segment they occupy and release it before moving onward. Reservation checks are atomic, and a traffic wait monitor explains any aircraft blocked on the same resource for ten seconds or more.

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
- The project compiles in Unity 6.3 LTS on the development Mac.

## Next work

Introduce a second simultaneous aircraft using the segment reservations, then run a longer visual soak with opposing ground traffic.
