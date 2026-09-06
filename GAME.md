# Airside active brief

## Vision

Airside is a real-time, persistent airport management game for Mac. The player designs and manages the system while aircraft, passengers and ground services operate automatically. Watching the airport work should be satisfying, and every delay should have an understandable cause.

## Current milestone

Phase one: prove one complete aircraft cycle. The current prototype repeatedly approaches, lands, taxis to one of two stands, waits, pushes back, taxis out and departs. The simulation owns timing and reservations; Unity interpolates the visual movement.

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

Run checks with `scripts/test-unity.sh`. Build the local Mac app with `scripts/build-mac.sh`.

## Current evidence

- Seven edit-mode tests pass.
- A fifty-cycle simulation completes without reservation conflicts.
- Large and one-second time steps reach identical simulation state.
- The project compiles in Unity 6.3 LTS on the development Mac.

## Next work

Add a real taxi graph with named nodes and segments, record transition events, measure the prototype frame rate, and run a visual fifty-cycle soak. Then begin the turnaround task and ground-service slice.
